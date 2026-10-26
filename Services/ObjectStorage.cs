using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

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
                throw new InvalidOperationException(
                    "Storage:Provider=s3 yêu cầu Endpoint, Bucket, AccessKey và SecretKey.");
            }

            var config = new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true,
                AuthenticationRegion = configuration["Storage:Region"]?.Trim() ?? "auto"
            };
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

        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            DisablePayloadSigning = false
        }, cancellationToken);
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
