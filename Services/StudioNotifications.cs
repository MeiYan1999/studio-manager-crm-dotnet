using MayAlbum.Models;

namespace MayAlbum.Services;

/// <summary>
/// Tạo thông báo trong hộp thư studio. Mỗi loại có công tắc riêng trong cài đặt;
/// công tắc tổng tắt thì không ghi thông báo nào.
/// </summary>
public static class StudioNotifications
{
    public const int MaxEntries = 500;

    public static void Push(
        AppData data,
        string type,
        string title,
        string body,
        string link = "")
    {
        data.NotificationSettings ??= new NotificationSettings();
        var settings = data.NotificationSettings;
        if (!settings.Master)
        {
            return;
        }

        var enabled = NotificationTypes.Normalize(type) switch
        {
            NotificationTypes.Booking => settings.Booking,
            NotificationTypes.Payment => settings.Payment,
            NotificationTypes.Contract => settings.Contract,
            NotificationTypes.Wardrobe => settings.Wardrobe,
            NotificationTypes.Payroll => settings.Payroll,
            _ => true
        };

        if (!enabled)
        {
            return;
        }

        data.Notifications ??= [];
        data.Notifications.Insert(0, new NotificationEntry
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Type = NotificationTypes.Normalize(type),
            Title = AlbumText.TrimTo(title, 160),
            Body = AlbumText.TrimTo(body, 600),
            Link = AlbumText.TrimTo(link, 200),
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow
        });

        if (data.Notifications.Count > MaxEntries)
        {
            data.Notifications.RemoveRange(MaxEntries, data.Notifications.Count - MaxEntries);
        }
    }

    /// <summary>
    /// Quét các mục quá hạn (trang phục chưa trả, hợp đồng chờ duyệt lâu,
    /// lương chưa chi) và tạo thông báo nhắc việc một lần mỗi ngày.
    /// </summary>
    public static void ScanOverdue(AppData data, string today)
    {
        data.NotificationSettings ??= new NotificationSettings();
        if (!data.NotificationSettings.Master)
        {
            return;
        }

        var existingToday = data.Notifications
            .Where(notification => notification.CreatedAt
                .ToOffset(TimeSpan.FromHours(7))
                .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) == today)
            .Select(notification => notification.Title)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var loan in data.OutfitLoans.Where(loan =>
                     string.IsNullOrWhiteSpace(loan.ReturnDate) &&
                     loan.DueDate.Length > 0 &&
                     string.CompareOrdinal(loan.DueDate, today) < 0))
        {
            var outfit = data.Outfits.FirstOrDefault(item =>
                string.Equals(item.Id, loan.OutfitId, StringComparison.Ordinal));
            var title = $"Trang phục “{outfit?.Name ?? "Trang phục"}” quá hạn trả";
            if (existingToday.Add(title))
            {
                Push(data, NotificationTypes.Wardrobe, title,
                    $"Hạn trả {loan.DueDate} đã qua. Vui lòng kiểm tra và nhắc khách trả trang phục.",
                    "/admin/wardrobe");
            }
        }

        foreach (var payroll in data.Payrolls.Where(payroll => !payroll.Paid && payroll.Month.Length == 7))
        {
            if (string.CompareOrdinal(payroll.Month, today[..7]) >= 0)
            {
                continue;
            }

            var staff = data.Staff.FirstOrDefault(member =>
                string.Equals(member.Id, payroll.StaffId, StringComparison.Ordinal));
            var title = $"Lương tháng {payroll.Month} chưa chi";
            if (existingToday.Add(title))
            {
                Push(data, NotificationTypes.Payroll, title,
                    $"Bảng lương của {staff?.Name ?? "nhân sự"} vẫn chưa được đánh dấu đã trả.",
                    "/admin/finance");
            }
        }
    }
}
