using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using MayAlbum.Models;

namespace MayAlbum.Services;

public sealed record StoredObject(Stream Content, string ContentType);

public interface IObjectStorage
{
    bool IsRemote { get; }
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);
    Task<StoredObject?> GetAsync(string key, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

public sealed class ObjectStorage : IObjectStorage, IDisposable
{
    private readonly string _localDirectory;
    private readonly string _bucket;
    private readonly Uri? _endpoint;
    private readonly string _region = "";
    private readonly string _accessKey = "";
    private readonly string _secretKey = "";
    private readonly HttpClient? _http;
    private readonly ILogger<ObjectStorage> _logger;

    public ObjectStorage(IConfiguration configuration, DataStore store, ILogger<ObjectStorage> logger)
    {
        _logger = logger;
        _localDirectory = Path.Combine(store.DataDirectory, "finance-photos");
        Directory.CreateDirectory(_localDirectory);

        var provider = configuration["Storage:Provider"]?.Trim();
        _bucket = configuration["Storage:Bucket"]?.Trim() ?? "";
        var endpoint = configuration["Storage:Endpoint"]?.Trim().TrimEnd('/') ?? "";
        _region = configuration["Storage:Region"]?.Trim() ?? "";
        _accessKey = configuration["Storage:AccessKey"]?.Trim() ?? "";
        _secretKey = configuration["Storage:SecretKey"]?.Trim() ?? "";

        if (string.Equals(provider, "s3", StringComparison.OrdinalIgnoreCase))
        {
            if (_bucket.Length == 0 || endpoint.Length == 0 || _region.Length == 0 ||
                _accessKey.Length == 0 || _secretKey.Length == 0 ||
                !Uri.TryCreate(endpoint, UriKind.Absolute, out _endpoint))
            {
                throw new ApiException(503,
                    "Chưa cấu hình đầy đủ kho ảnh. Kiểm tra Endpoint, Region, Bucket, Access Key và Secret Key trên Render.",
                    "storage_not_configured");
            }

            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _logger.LogInformation(
                "Kho ảnh S3: host {Host}, region {Region}, bucket {Bucket}, key …{KeySuffix}.",
                _endpoint.Host, _region, _bucket,
                _accessKey.Length > 4 ? _accessKey[^4..] : _accessKey);
        }
        else
        {
            _logger.LogInformation("Ảnh chứng từ dùng thư mục local {Directory}.", _localDirectory);
        }
    }

    public bool IsRemote => _http is not null;

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        key = SafeKey(key);
        if (_http is null)
        {
            await using var output = File.Create(Path.Combine(_localDirectory, key));
            await content.CopyToAsync(output, cancellationToken);
            return;
        }

        await using var memory = new MemoryStream();
        await content.CopyToAsync(memory, cancellationToken);
        using var response = await SendSignedAsync(HttpMethod.Put, key, memory.ToArray(), contentType, cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowStorageError(response, "upload", cancellationToken);
    }

    public async Task<StoredObject?> GetAsync(string key, CancellationToken cancellationToken)
    {
        key = SafeKey(key);
        if (_http is null)
        {
            var path = Path.Combine(_localDirectory, key);
            return File.Exists(path)
                ? new StoredObject(File.OpenRead(path), "application/octet-stream")
                : null;
        }

        using var response = await SendSignedAsync(HttpMethod.Get, key, [], null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
            await ThrowStorageError(response, "download", cancellationToken);

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return new StoredObject(new MemoryStream(bytes, writable: false), contentType);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        key = SafeKey(key);
        if (_http is null)
        {
            var path = Path.Combine(_localDirectory, key);
            if (File.Exists(path)) File.Delete(path);
            return;
        }

        using var response = await SendSignedAsync(HttpMethod.Delete, key, [], null, cancellationToken);
        if (response.StatusCode != HttpStatusCode.NotFound && !response.IsSuccessStatusCode)
            await ThrowStorageError(response, "delete", cancellationToken);
    }

    private async Task<HttpResponseMessage> SendSignedAsync(
        HttpMethod method, string key, byte[] payload, string? contentType, CancellationToken cancellationToken)
    {
        var path = _endpoint!.AbsolutePath.TrimEnd('/') + "/" +
                   Uri.EscapeDataString(_bucket) + "/" + Uri.EscapeDataString(key);
        var uri = new UriBuilder(_endpoint) { Path = path }.Uri;
        var now = DateTimeOffset.UtcNow;
        var amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'");
        var date = now.ToString("yyyyMMdd");
        var payloadHash = Hex(SHA256.HashData(payload));
        var host = uri.IsDefaultPort ? uri.Host : uri.Authority;
        const string signedHeaders = "host;x-amz-content-sha256;x-amz-date";
        var canonicalHeaders = $"host:{host}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{amzDate}\n";
        var canonicalRequest = $"{method.Method}\n{uri.AbsolutePath}\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
        var scope = $"{date}/{_region}/s3/aws4_request";
        var requestHash = Hex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)));
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{scope}\n{requestHash}";
        var signingKey = Hmac(Hmac(Hmac(Hmac(Encoding.UTF8.GetBytes("AWS4" + _secretKey), date), _region), "s3"), "aws4_request");
        var signature = Hex(Hmac(signingKey, stringToSign));

        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Host = host;
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        request.Headers.TryAddWithoutValidation("Authorization",
            $"AWS4-HMAC-SHA256 Credential={_accessKey}/{scope}, SignedHeaders={signedHeaders}, Signature={signature}");
        if (method == HttpMethod.Put)
        {
            request.Content = new ByteArrayContent(payload);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                contentType ?? "application/octet-stream");
        }

        try
        {
            return await _http!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Không kết nối được S3 host {Host}.", _endpoint.Host);
            throw new ApiException(502, "Không kết nối được kho ảnh. Kiểm tra Storage__Endpoint trên Render rồi thử lại.", "storage_unavailable");
        }
    }

    private async Task ThrowStorageError(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        string code;
        try
        {
            code = XDocument.Parse(body).Descendants().FirstOrDefault(node => node.Name.LocalName == "Code")?.Value
                   ?? response.ReasonPhrase ?? "Unknown";
        }
        catch
        {
            code = response.ReasonPhrase ?? "Unknown";
        }

        _logger.LogError("Storage {Operation} failed: HTTP {Status}, code {Code}, host {Host}, region {Region}, bucket {Bucket}",
            operation, (int)response.StatusCode, code, _endpoint!.Host, _region, _bucket);
        var message = code switch
        {
            "NoSuchBucket" => "Chưa tìm thấy bucket lưu ảnh. Kiểm tra Storage__Bucket trên Render.",
            "InvalidAccessKeyId" or "SignatureDoesNotMatch" or "InvalidSignatureException" =>
                "Không xác thực được kho ảnh. Kiểm tra Access Key, Secret Key, Endpoint và Region trên Render.",
            "AccessDenied" or "Forbidden" => "Kho ảnh từ chối quyền truy cập. Hãy tạo một cặp S3 Access Key mới trong đúng project Supabase.",
            _ => "Không tải được ảnh lên kho lưu trữ. Kiểm tra cấu hình Supabase Storage và nhật ký Render rồi thử lại."
        };
        throw new ApiException(502, message, "storage_request_failed");
    }

    private static byte[] Hmac(byte[] key, string value) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value));

    private static string Hex(byte[] value) => Convert.ToHexString(value).ToLowerInvariant();

    private static string SafeKey(string key)
    {
        var safe = Path.GetFileName(key ?? "");
        if (safe.Length == 0) throw new InvalidOperationException("Object key không hợp lệ.");
        return safe;
    }

    public void Dispose() => _http?.Dispose();
}
