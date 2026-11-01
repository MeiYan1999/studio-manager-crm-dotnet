using StudioCore.Models;
using StudioCore.Persistence;

namespace StudioCore.Services;

/// <summary>Local development fallback. Production uses CloudflareStorageService when ServiceUrl is configured.</summary>
public sealed class LocalStorageService : IStorageService
{
    private readonly string _root;
    private readonly string _legacyRoot;
    private readonly TenantContext _tenant;

    public LocalStorageService(DataStore store, TenantContext tenant)
    {
        _root = Path.Combine(store.DataDirectory, "objects");
        _legacyRoot = Path.Combine(store.DataDirectory, "finance-photos");
        _tenant = tenant;
        Directory.CreateDirectory(_root);
    }

    public bool IsRemote => false;

    public async Task<string> UploadAsync(
        string folderName,
        string originalFileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var key = CloudflareStorageService.BuildObjectKey(_tenant.TenantId, folderName, originalFileName);
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var output = File.Create(path);
        await content.CopyToAsync(output, cancellationToken);
        return key;
    }

    public Task<StoredObject?> GetAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var key = ValidateOwnedKey(objectKey);
        var path = LegacyOrCurrentPath(key);
        StoredObject? result = File.Exists(path)
            ? new StoredObject(File.OpenRead(path), "application/octet-stream")
            : null;
        return Task.FromResult(result);
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var key = ValidateOwnedKey(objectKey);
        var path = LegacyOrCurrentPath(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string ValidateOwnedKey(string objectKey)
    {
        var key = (objectKey ?? "").Trim().Replace('\\', '/').TrimStart('/');
        if (key.Length == 0 || key.Contains("..", StringComparison.Ordinal))
            throw new ApiException(400, "Object key không hợp lệ.", "invalid_storage_key");
        if (!key.Contains('/')) return Path.GetFileName(key);
        var prefix = CloudflareStorageService.SafeSegment(_tenant.TenantId, "tenant") + "/";
        if (!key.StartsWith(prefix, StringComparison.Ordinal))
            throw new ApiException(403, "Không được truy cập tệp của studio khác.", "cross_tenant_storage_access");
        return key;
    }

    private string ResolvePath(string key)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = Path.GetFullPath(_root) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.Ordinal))
            throw new ApiException(400, "Object key không hợp lệ.", "invalid_storage_key");
        return fullPath;
    }

    private string LegacyOrCurrentPath(string key) =>
        key.Contains('/') ? ResolvePath(key) : Path.Combine(_legacyRoot, Path.GetFileName(key));
}
