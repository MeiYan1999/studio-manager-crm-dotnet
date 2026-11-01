using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System.Net;
using StudioCore.Models;
using StudioCore.Persistence;

namespace StudioCore.Services;

public sealed record StoredObject(Stream Content, string ContentType);

public static class StorageFolders
{
    public const string Images = "images";
    public const string Albums = "albums";
    public const string Contracts = "contracts";
    public const string Finance = "finance";
}

public interface IStorageService
{
    bool IsRemote { get; }
    Task<string> UploadAsync(
        string folderName,
        string originalFileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);
    Task<StoredObject?> GetAsync(string objectKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cloudflare R2 storage through its S3-compatible API. Every new object is namespaced by tenant.
/// Bare keys are accepted for reads/deletes only, to keep media uploaded before multi-tenancy working.
/// </summary>
public sealed class CloudflareStorageService : IStorageService, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly string _bucketName;
    private readonly TenantContext _tenant;
    private readonly ILogger<CloudflareStorageService> _logger;

    public CloudflareStorageService(
        IConfiguration configuration,
        TenantContext tenant,
        ILogger<CloudflareStorageService> logger)
    {
        _tenant = tenant;
        _logger = logger;
        var section = configuration.GetSection("CloudflareR2");
        var serviceUrl = section["ServiceUrl"]?.Trim().TrimEnd('/') ?? "";
        var accessKey = section["AccessKey"]?.Trim() ?? "";
        var secretKey = section["SecretKey"]?.Trim() ?? "";
        _bucketName = section["BucketName"]?.Trim() ?? "";

        if (!Uri.TryCreate(serviceUrl, UriKind.Absolute, out _) ||
            accessKey.Length == 0 || secretKey.Length == 0 || _bucketName.Length == 0)
        {
            throw new ApiException(
                503,
                "Cloudflare R2 chưa được cấu hình đầy đủ.",
                "storage_not_configured");
        }

        _client = new AmazonS3Client(
            new BasicAWSCredentials(accessKey, secretKey),
            new AmazonS3Config
            {
                ServiceURL = serviceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = "auto"
            });
    }

    public bool IsRemote => true;

    public async Task<string> UploadAsync(
        string folderName,
        string originalFileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var objectKey = BuildObjectKey(_tenant.TenantId, folderName, originalFileName);
        try
        {
            await _client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = content,
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                AutoCloseStream = false,
                DisablePayloadSigning = false
            }, cancellationToken);
            return objectKey;
        }
        catch (AmazonS3Exception exception)
        {
            throw StorageException(exception, "upload");
        }
    }

    public async Task<StoredObject?> GetAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        objectKey = ValidateOwnedKey(objectKey);
        try
        {
            var response = await _client.GetObjectAsync(_bucketName, objectKey, cancellationToken);
            return new StoredObject(
                new ResponseStream(response),
                response.Headers.ContentType ?? "application/octet-stream");
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (AmazonS3Exception exception)
        {
            throw StorageException(exception, "download");
        }
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        objectKey = ValidateOwnedKey(objectKey);
        try
        {
            await _client.DeleteObjectAsync(_bucketName, objectKey, cancellationToken);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            // Delete remains idempotent.
        }
        catch (AmazonS3Exception exception)
        {
            throw StorageException(exception, "delete");
        }
    }

    internal static string BuildObjectKey(string tenantId, string folderName, string originalFileName)
    {
        var safeTenant = SafeSegment(tenantId, "tenant");
        var safeFolder = SafeSegment(folderName, "folder");
        var extension = Path.GetExtension(Path.GetFileName(originalFileName ?? "")).ToLowerInvariant();
        if (extension.Length > 12 || extension.Any(character => !char.IsLetterOrDigit(character) && character != '.'))
            extension = "";
        return $"{safeTenant}/{safeFolder}/{Guid.NewGuid():N}{extension}";
    }

    private string ValidateOwnedKey(string objectKey)
    {
        var normalized = (objectKey ?? "").Trim().Replace('\\', '/').TrimStart('/');
        if (normalized.Length == 0 || normalized.Contains("..", StringComparison.Ordinal))
            throw new ApiException(400, "Object key không hợp lệ.", "invalid_storage_key");

        // Compatibility for objects created by the old flat-key storage implementation.
        if (!normalized.Contains('/')) return Path.GetFileName(normalized);

        var tenantPrefix = SafeSegment(_tenant.TenantId, "tenant") + "/";
        if (!normalized.StartsWith(tenantPrefix, StringComparison.Ordinal))
            throw new ApiException(403, "Không được truy cập tệp của studio khác.", "cross_tenant_storage_access");
        return normalized;
    }

    private ApiException StorageException(AmazonS3Exception exception, string operation)
    {
        _logger.LogError(
            exception,
            "Cloudflare R2 {Operation} failed: HTTP {Status}, code {Code}, bucket {Bucket}",
            operation,
            (int)exception.StatusCode,
            exception.ErrorCode,
            _bucketName);
        var message = exception.ErrorCode switch
        {
            "NoSuchBucket" => "Không tìm thấy bucket Cloudflare R2 đã cấu hình.",
            "InvalidAccessKeyId" or "SignatureDoesNotMatch" or "InvalidSignatureException" =>
                "Không xác thực được Cloudflare R2. Kiểm tra Access Key, Secret Key và Service URL.",
            "AccessDenied" or "Forbidden" =>
                "Cloudflare R2 từ chối quyền truy cập. Kiểm tra quyền Object Read & Write của API token.",
            _ => "Không thể kết nối kho lưu trữ Cloudflare R2. Vui lòng thử lại."
        };
        return new ApiException(502, message, "storage_request_failed");
    }

    internal static string SafeSegment(string value, string label)
    {
        var safe = (value ?? "").Trim().ToLowerInvariant();
        if (safe.Length == 0 || safe.Length > 100 ||
            safe.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_'))
            throw new ApiException(400, $"{label} lưu trữ không hợp lệ.", "invalid_storage_path");
        return safe;
    }

    public void Dispose() => _client.Dispose();

    private sealed class ResponseStream(GetObjectResponse response) : Stream
    {
        private readonly Stream _inner = response.ResponseStream;
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);
        protected override void Dispose(bool disposing)
        {
            if (disposing) response.Dispose();
            base.Dispose(disposing);
        }
        public override async ValueTask DisposeAsync()
        {
            response.Dispose();
            await base.DisposeAsync();
        }
    }
}
