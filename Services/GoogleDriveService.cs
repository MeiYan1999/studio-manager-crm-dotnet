using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MayAlbum.Services;

public sealed class DriveException : Exception
{
    public DriveException(string message, string code) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed record DrivePhoto(
    string FileId,
    string FileName,
    string FolderPath,
    string MimeType,
    int Width,
    int Height,
    long SizeBytes,
    DateTimeOffset? CreatedAt);

public sealed record DriveFolderResult(
    string FolderId,
    string FolderName,
    List<DrivePhoto> Photos,
    bool Truncated);

public sealed partial class GoogleDriveService
{
    private const int MaxPhotos = 1500;
    private const int MaxFolders = 80;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoogleDriveService> _logger;

    public GoogleDriveService(IHttpClientFactory httpClientFactory, ILogger<GoogleDriveService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [GeneratedRegex(@"/folders/([a-zA-Z0-9_-]{10,})", RegexOptions.IgnoreCase)]
    private static partial Regex FolderPathRegex();

    [GeneratedRegex(@"[?&]id=([a-zA-Z0-9_-]{10,})", RegexOptions.IgnoreCase)]
    private static partial Regex FolderIdQueryRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9_-]{10,}$")]
    private static partial Regex RawIdRegex();

    public static string? ExtractFolderId(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var value = input.Trim();
        var pathMatch = FolderPathRegex().Match(value);
        if (pathMatch.Success)
        {
            return pathMatch.Groups[1].Value;
        }

        var queryMatch = FolderIdQueryRegex().Match(value);
        if (queryMatch.Success)
        {
            return queryMatch.Groups[1].Value;
        }

        return RawIdRegex().IsMatch(value) ? value : null;
    }

    public async Task<DriveFolderResult> LoadFolderAsync(
        string? folderInput,
        bool includeSubfolders,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        var folderId = ExtractFolderId(folderInput);
        if (folderId is null)
        {
            throw new DriveException(
                "Liên kết chưa đúng. Hãy dán liên kết thư mục Google Drive, dạng drive.google.com/drive/folders/…",
                "invalid_link");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new DriveException(
                "Chưa cấu hình Google Drive API key. Mở Cài đặt để kết nối Drive trước khi nhập album.",
                "missing_api_key");
        }

        using var client = _httpClientFactory.CreateClient("drive");

        var folder = await GetFolderAsync(client, folderId, apiKey, cancellationToken);
        var photos = new List<DrivePhoto>();
        var foldersVisited = 0;
        var truncated = false;

        var queue = new Queue<(string Id, string Path)>();
        queue.Enqueue((folderId, ""));
        var visited = new HashSet<string>(StringComparer.Ordinal) { folderId };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foldersVisited++;

            await foreach (var child in ListChildrenAsync(client, current.Id, apiKey, cancellationToken))
            {
                if (child.IsFolder)
                {
                    if (includeSubfolders &&
                        foldersVisited + queue.Count < MaxFolders &&
                        visited.Add(child.Id))
                    {
                        var path = string.IsNullOrEmpty(current.Path)
                            ? child.Name
                            : $"{current.Path}/{child.Name}";
                        queue.Enqueue((child.Id, path));
                    }

                    continue;
                }

                if (!child.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (photos.Count >= MaxPhotos)
                {
                    truncated = true;
                    break;
                }

                photos.Add(new DrivePhoto(
                    child.Id,
                    child.Name,
                    current.Path,
                    child.MimeType,
                    child.Width,
                    child.Height,
                    child.Size,
                    child.CreatedAt));
            }

            if (truncated)
            {
                break;
            }
        }

        photos.Sort(CompareDrivePhotos);
        return new DriveFolderResult(folder.Id, folder.Name, photos, truncated);
    }

    private async Task<FolderMetadata> GetFolderAsync(
        HttpClient client,
        string folderId,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var url = "https://www.googleapis.com/drive/v3/files/" +
                  $"{Uri.EscapeDataString(folderId)}" +
                  $"?fields={Uri.EscapeDataString("id,name,mimeType")}" +
                  $"&supportsAllDrives=true&key={Uri.EscapeDataString(apiKey)}";

        using var response = await SafeGetAsync(client, url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await BuildExceptionAsync(response, cancellationToken);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "Album" : "Album";
        var mimeType = root.TryGetProperty("mimeType", out var mimeElement) ? mimeElement.GetString() ?? "" : "";

        if (!string.Equals(mimeType, "application/vnd.google-apps.folder", StringComparison.Ordinal))
        {
            throw new DriveException(
                "Liên kết này là một tệp, không phải thư mục. Hãy dán liên kết thư mục chứa ảnh.",
                "not_a_folder");
        }

        return new FolderMetadata(folderId, name);
    }

    private async IAsyncEnumerable<DriveChild> ListChildrenAsync(
        HttpClient client,
        string folderId,
        string apiKey,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? pageToken = null;
        var pages = 0;

        do
        {
            pages++;
            var query = $"'{folderId}' in parents and trashed = false";
            var url =
                "https://www.googleapis.com/drive/v3/files" +
                $"?q={Uri.EscapeDataString(query)}" +
                $"&fields={Uri.EscapeDataString("nextPageToken,files(id,name,mimeType,size,createdTime,imageMediaMetadata(width,height))")}" +
                "&pageSize=1000&orderBy=name&supportsAllDrives=true&includeItemsFromAllDrives=true" +
                $"&key={Uri.EscapeDataString(apiKey)}";
            if (!string.IsNullOrWhiteSpace(pageToken))
            {
                url += $"&pageToken={Uri.EscapeDataString(pageToken)}";
            }

            using var response = await SafeGetAsync(client, url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw await BuildExceptionAsync(response, cancellationToken);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
            {
                foreach (var file in files.EnumerateArray())
                {
                    var id = file.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                    var name = file.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                    var mimeType = file.TryGetProperty("mimeType", out var mimeElement)
                        ? mimeElement.GetString()
                        : null;

                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || mimeType is null)
                    {
                        continue;
                    }

                    var width = 0;
                    var height = 0;
                    if (file.TryGetProperty("imageMediaMetadata", out var imageMeta) &&
                        imageMeta.ValueKind == JsonValueKind.Object)
                    {
                        if (imageMeta.TryGetProperty("width", out var widthElement))
                        {
                            width = widthElement.TryGetInt32(out var parsedWidth) ? parsedWidth : 0;
                        }

                        if (imageMeta.TryGetProperty("height", out var heightElement))
                        {
                            height = heightElement.TryGetInt32(out var parsedHeight) ? parsedHeight : 0;
                        }
                    }

                    long size = 0;
                    if (file.TryGetProperty("size", out var sizeElement))
                    {
                        if (sizeElement.ValueKind == JsonValueKind.String &&
                            long.TryParse(sizeElement.GetString(), out var parsedSize))
                        {
                            size = parsedSize;
                        }
                        else if (sizeElement.ValueKind == JsonValueKind.Number &&
                                 sizeElement.TryGetInt64(out parsedSize))
                        {
                            size = parsedSize;
                        }
                    }

                    DateTimeOffset? createdAt = null;
                    if (file.TryGetProperty("createdTime", out var createdElement) &&
                        DateTimeOffset.TryParse(createdElement.GetString(), out var parsedCreated))
                    {
                        createdAt = parsedCreated;
                    }

                    yield return new DriveChild(
                        id,
                        name,
                        mimeType,
                        string.Equals(mimeType, "application/vnd.google-apps.folder", StringComparison.Ordinal),
                        width,
                        height,
                        size,
                        createdAt);
                }
            }

            pageToken = root.TryGetProperty("nextPageToken", out var tokenElement)
                ? tokenElement.GetString()
                : null;
        } while (!string.IsNullOrWhiteSpace(pageToken) && pages < 10);
    }

    private async Task<HttpResponseMessage> SafeGetAsync(
        HttpClient client,
        string url,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DriveException("Google Drive phản hồi quá lâu. Vui lòng thử lại.", "drive_timeout");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Không kết nối được Google Drive API");
            throw new DriveException("Không kết nối được Google Drive. Kiểm tra mạng rồi thử lại.", "network_error");
        }
    }

    private static async Task<DriveException> BuildExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var status = response.StatusCode;
        var detail = "";
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var messageElement))
            {
                detail = messageElement.GetString() ?? "";
            }
        }
        catch (JsonException)
        {
            // The status code is enough to offer an actionable message.
        }

        var suffix = string.IsNullOrWhiteSpace(detail) ? "" : $" ({detail})";
        return status switch
        {
            HttpStatusCode.BadRequest => new DriveException(
                "Google từ chối API key. Hãy kiểm tra lại khóa trong Cài đặt và đảm bảo đã bật Google Drive API." + suffix,
                "invalid_api_key"),
            HttpStatusCode.Unauthorized => new DriveException(
                "API key Google không hợp lệ hoặc đã bị giới hạn." + suffix,
                "invalid_api_key"),
            HttpStatusCode.Forbidden => new DriveException(
                "Không có quyền truy cập thư mục. Hãy chia sẻ thư mục ở chế độ “Bất kỳ ai có liên kết” và thử lại." + suffix,
                "drive_forbidden"),
            HttpStatusCode.NotFound => new DriveException(
                "Không tìm thấy thư mục. Hãy kiểm tra liên kết và đảm bảo thư mục được chia sẻ công khai.",
                "drive_not_found"),
            HttpStatusCode.TooManyRequests => new DriveException(
                "Đã chạm giới hạn truy vấn Google Drive. Vui lòng đợi vài phút rồi thử lại.",
                "drive_quota"),
            _ => new DriveException(
                $"Google Drive trả về lỗi {(int)status}. Vui lòng thử lại." + suffix,
                "drive_error")
        };
    }

    private static int CompareDrivePhotos(DrivePhoto left, DrivePhoto right)
    {
        var folderComparison = NaturalStringComparer.Instance.Compare(left.FolderPath, right.FolderPath);
        return folderComparison != 0
            ? folderComparison
            : NaturalStringComparer.Instance.Compare(left.FileName, right.FileName);
    }

    private sealed record FolderMetadata(string Id, string Name);

    private sealed record DriveChild(
        string Id,
        string Name,
        string MimeType,
        bool IsFolder,
        int Width,
        int Height,
        long Size,
        DateTimeOffset? CreatedAt);
}
