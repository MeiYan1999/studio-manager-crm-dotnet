using StudioCore.Models;

namespace StudioCore.Services;

/// <summary>
/// Ghi nhật ký thao tác toàn studio. Mọi thay đổi quan trọng đều đi qua đây
/// để quản trị viên có thể tra lại ai đã làm gì và khi nào.
/// </summary>
public static class StudioAudit
{
    public const int MaxEntries = 2000;

    public static void Add(
        AppData data,
        StudioUser? actor,
        string module,
        string action,
        string summary,
        string entityId = "",
        IEnumerable<string>? details = null)
    {
        data.AuditLogs ??= [];
        var actorName = string.IsNullOrWhiteSpace(actor?.DisplayName)
            ? (string.IsNullOrWhiteSpace(actor?.Username) ? "Studio" : actor!.Username)
            : actor!.DisplayName;

        data.AuditLogs.Insert(0, new AuditLogEntry
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Module = AlbumText.TrimTo(module, 40),
            Action = AlbumText.TrimTo(action, 40),
            Summary = AlbumText.TrimTo(summary, 240),
            ActorId = actor?.Id ?? "",
            ActorName = AlbumText.TrimTo(actorName, 80),
            ActorRole = string.Equals(actor?.Role, "admin", StringComparison.Ordinal) ? "admin" : "employee",
            EntityId = AlbumText.TrimTo(entityId, 80),
            Details = details?
                .Where(detail => !string.IsNullOrWhiteSpace(detail))
                .Select(detail => AlbumText.TrimTo(detail, 240))
                .Distinct(StringComparer.Ordinal)
                .Take(12)
                .ToList() ?? [],
            CreatedAt = DateTimeOffset.UtcNow
        });

        if (data.AuditLogs.Count > MaxEntries)
        {
            data.AuditLogs.RemoveRange(MaxEntries, data.AuditLogs.Count - MaxEntries);
        }
    }
}
