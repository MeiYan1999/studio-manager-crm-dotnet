using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
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
    private readonly AmazonS3Client? _s3;
    private readonly bool _disablePayloadSigning;
    private readonly ILogger<ObjectStorage> _logger;

    public ObjectStorage(IConfiguration configuration, DataStore store, ILogger<ObjectStorage> logger)
    {
        _logger = logger;
        _localDirectory = Path.Combine(store.DataDirectory, "finance-photos");
        Directory.CreateDirectory(_localDirectory);

        var provider = configuration["Storage:Provider"]?.Trim();
        _bucket = configuration["Storage:Bucket"]?.Trim() ?? "";
        var endpoint = configuration["Storage:Endpoint"]?.Trim() ?? "";
        var accessKey = configuration["Storage:AccessKey"]?.Trim() ?? "";
        var secretKey = configuration["Storage:SecretKey"]?.Trim() ?? "";

        if (string.Equals(provider, "s3", StringComparison.OrdinalIgnoreCase))
        {
            if (_bucket.Length == 0 || endpoint.Length == 0 || accessKey.Length == 0 || secretKey.Length == 0)
            {
                throw new ApiException(503,
                    "Chưa cấu hình đầy đủ kho ảnh. Kiểm tra Storage__Endpoint, Storage__Bucket, Storage__AccessKey và Storage__SecretKey trên Render.",
                    "storage_not_configured");
            }

            var config = new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true,
                AuthenticationRegion = configuration["Storage:Region"]?.Trim() ?? "auto",
                SignatureVersion = "4"
            };
            _disablePayloadSigning = endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            _s3 = new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), config);
            _logger.LogInformation("Ảnh chứng từ dùng object storage bucket {Bucket}.", _bucket);
        }
        else
        {
            _logger.LogInformation("Ảnh chứng từ dùng thư mục local {Directory}.", _localDirectory);
        }
    }

    public bool IsRemote => _s3 is not null;

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        key = SafeKey(key);
        if (_s3 is null)
        {
            await using var output = File.Create(Path.Combine(_localDirectory, key));
            await content.CopyToAsync(output, cancellationToken);
            return;
        }

        try
        {
            await _s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false,
                // S3-compatible providers may not support AWS streaming chunk signatures.
                UseChunkEncoding = false,
                DisablePayloadSigning = _disablePayloadSigning
            }, cancellationToken);
        }
        catch (AmazonS3Exception exception)
        {
            _logger.LogError("Storage upload failed: HTTP {Status}, code {Code}, request {RequestId}",
                (int)exception.StatusCode, exception.ErrorCode, exception.RequestId);
            var message = exception.ErrorCode switch
            {
                "NoSuchBucket" => "Chưa tìm thấy bucket lưu ảnh. Kiểm tra Storage__Bucket trên Render và tạo đúng bucket trong Supabase Storage.",
                "InvalidAccessKeyId" or "SignatureDoesNotMatch" or "InvalidSignatureException" =>
                    "Không xác thực được kho ảnh. Kiểm tra Access Key, Secret Key, Endpoint và Region của Supabase Storage trên Render.",
                "AccessDenied" => "Kho ảnh từ chối quyền tải lên. Kiểm tra khóa S3 và quyền truy cập bucket trong Supabase Storage.",
                _ => "Không tải được ảnh lên kho lưu trữ. Kiểm tra cấu hình Supabase Storage và nhật ký Render rồi thử lại."
            };
            throw new ApiException(502, message, "storage_upload_failed");
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning("Storage upload could not connect to the configured endpoint.");
            throw new ApiException(502, "Không kết nối được kho ảnh. Kiểm tra Storage__Endpoint trên Render rồi thử lại.", "storage_unavailable");
        }
    }

    public async Task<StoredObject?> GetAsync(string key, CancellationToken cancellationToken)
    {
        key = SafeKey(key);
        if (_s3 is null)
        {
            var path = Path.Combine(_localDirectory, key);
            return File.Exists(path)
                ? new StoredObject(File.OpenRead(path), "application/octet-stream")
                : null;
        }

        try
        {
            var response = await _s3.GetObjectAsync(_bucket, key, cancellationToken);
            return new StoredObject(response.ResponseStream, response.Headers.ContentType ?? "application/octet-stream");
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        key = SafeKey(key);
        if (_s3 is null)
        {
            var path = Path.Combine(_localDirectory, key);
            if (File.Exists(path)) File.Delete(path);
            return;
        }

        await _s3.DeleteObjectAsync(_bucket, key, cancellationToken);
    }

    private static string SafeKey(string key)
    {
        var safe = Path.GetFileName(key ?? "");
        if (safe.Length == 0) throw new InvalidOperationException("Object key không hợp lệ.");
        return safe;
    }

    public void Dispose() => _s3?.Dispose();
}
