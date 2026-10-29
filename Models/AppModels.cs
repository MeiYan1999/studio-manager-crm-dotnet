using System.Text.Json.Serialization;
using StudioCore.Services;

namespace StudioCore.Models;

public sealed class AppData
{
    public int SchemaVersion { get; set; } = 7;
    public StudioSettings Settings { get; set; } = new();
    public List<StudioUser> Users { get; set; } = [];
    public List<Album> Albums { get; set; } = [];
    public TaskBoard Board { get; set; } = new();
    public List<Customer> Customers { get; set; } = [];
    public List<ServicePackage> Packages { get; set; } = [];
    public List<PriceGroup> PriceCatalog { get; set; } = PriceCatalogDefaults.Create();
    public List<StaffMember> Staff { get; set; } = [];
    public List<Booking> Bookings { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
    public List<Contract> Contracts { get; set; } = [];
    public List<ExpenseEntry> Expenses { get; set; } = [];
    public List<PayrollEntry> Payrolls { get; set; } = [];
    public List<AttendanceRecord> Attendance { get; set; } = [];
    public List<ShiftAssignment> ShiftAssignments { get; set; } = [];
    public List<Outfit> Outfits { get; set; } = [];
    public List<StudioConcept> Concepts { get; set; } = [];
    public List<OutfitLoan> OutfitLoans { get; set; } = [];
    public List<NotificationEntry> Notifications { get; set; } = [];
    public NotificationSettings NotificationSettings { get; set; } = new();
    public string LastOverdueScan { get; set; } = "";
    public int OrderSequence { get; set; } = 1;
    public int PaymentSequence { get; set; } = 1;
    public int ContractSequence { get; set; } = 1;
    public List<AuditLogEntry> AuditLogs { get; set; } = [];
}

public sealed class WebsiteContent
{
    public string LoadingName { get; set; } = "";
    public string LoadingText { get; set; } = "Đang mở studio…";
    public string LoginName { get; set; } = "";
    public string LoginQuote { get; set; } = "Ảnh đẹp nhất là ảnh khiến ta nhớ lại cảm giác của ngày hôm đó.";
    public string LoginTitle { get; set; } = "Chào mừng trở lại.";
    public string BrandName { get; set; } = "StudioCore";
    public string BrandLabel { get; set; } = "album";
    public string HowItWorksLabel { get; set; } = "Cách hoạt động";
    public string SampleAlbumsLabel { get; set; } = "Concept";
    public string ForStudioLabel { get; set; } = "Dành cho studio";
    public string HeroTitle { get; set; } = "Mỗi khoảnh khắc, một lời nhớ.";
    public string HeroDescription { get; set; } = "StudioCore gom những ngày đẹp nhất vào một album riêng. Bạn chỉ cần mở liên kết, thả tim những tấm ảnh mình yêu và gửi lại cho studio.";
    public string FooterDescription { get; set; } = "Album ảnh riêng tư, được làm bằng sự chăm chút.";
    public string FooterPrivacy { get; set; } = "Album được chia sẻ riêng cho từng khách hàng.";
}

public sealed class StudioSettings
{
    public string HomeImageKey { get; set; } = "";
    public string HomeImageContentType { get; set; } = "image/png";
    public string HomeImageUrl => HomeImageKey.Length > 0 ? "/api/public/home-image?v=" + HomeImageKey : "";
    public string LoginImageKey { get; set; } = "";
    public string LoginImageContentType { get; set; } = "image/png";
    public string LoginImageUrl => LoginImageKey.Length > 0 ? "/api/public/login-image?v=" + LoginImageKey : "";
    public string LogoKey { get; set; } = "";
    public string LogoContentType { get; set; } = "image/png";
    public string LogoUrl => LogoKey.Length > 0 ? "/api/public/logo?v=" + LogoKey : "";
    public string StudioName { get; set; } = "StudioCore";
    public string ContactEmail { get; set; } = "hello@may.studio";
    public string GoogleApiKey { get; set; } = "";
    public WebsiteContent Website { get; set; } = new();
}

public sealed class StudioUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "employee";
    public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public string StaffId { get; set; } = "";
    public bool PermissionsConfigured { get; set; }
    public List<string> Permissions { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }
}

public static class StudioPermissions
{
    public const string Dashboard = "dashboard";
    public const string Albums = "albums";
    public const string Board = "board";
    public const string Tools = "tools";
    public const string Customers = "customers";
    public const string Packages = "packages";
    public const string Bookings = "bookings";
    public const string Staff = "staff";
    public const string Orders = "orders";
    public const string Contracts = "contracts";
    public const string Reports = "reports";
    public const string Finance = "finance";
    public const string Attendance = "attendance";
    public const string Wardrobe = "wardrobe";
    public const string Notifications = "notifications";
    public const string Audit = "audit";
    public const string Settings = "settings";
    public const string Accounts = "accounts";

    public static readonly string[] All =
    [
        Dashboard, Albums, Board, Tools, Customers, Packages, Bookings, Staff,
        Orders, Contracts, Reports, Finance, Attendance, Wardrobe, Notifications,
        Audit, Settings, Accounts
    ];

    public static readonly string[] EmployeeDefaults =
    [
        Dashboard, Albums, Board, Tools
    ];

    public static bool IsKnown(string? permission) =>
        !string.IsNullOrWhiteSpace(permission) && All.Contains(permission, StringComparer.Ordinal);

    public static List<string> Normalize(IEnumerable<string>? permissions, bool isAdmin, bool configured)
    {
        if (isAdmin)
        {
            return [.. All];
        }

        if (!configured)
        {
            return [.. EmployeeDefaults];
        }

        return (permissions ?? [])
            .Where(IsKnown)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}

public sealed class Album
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ShareToken { get; set; } = "";
    public string Title { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string EventDate { get; set; } = "";
    public string Location { get; set; } = "";
    public string Description { get; set; } = "";
    public string DriveFolderUrl { get; set; } = "";
    public string DriveFolderId { get; set; } = "";
    public bool IncludeSubfolders { get; set; }
    public bool AllowFavorites { get; set; } = true;
    public bool AllowDownloads { get; set; }
    public bool IsPublished { get; set; } = true;
    public bool IsDemo { get; set; }
    public string CoverPhotoId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FavoritesUpdatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public string ClientNote { get; set; } = "";
    public List<Photo> Photos { get; set; } = [];
    public List<PhotoSelection> Selections { get; set; } = [];

    [JsonIgnore]
    public HashSet<string> SelectedPhotoIds =>
        Selections.Select(selection => selection.PhotoId).ToHashSet(StringComparer.Ordinal);
}

public sealed class Photo
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DriveFileId { get; set; } = "";
    public string Code { get; set; } = "";
    public string FileName { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public string ThumbUrl { get; set; } = "";
    public string PreviewUrl { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset? TakenAt { get; set; }
}

public sealed class PhotoSelection
{
    public string PhotoId { get; set; } = "";
    public DateTimeOffset SelectedAt { get; set; } = DateTimeOffset.UtcNow;
}

// ---------- Studio CRM ----------

public sealed class Customer
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Name { get; set; } = "";
    public string Type { get; set; } = "individual";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public string Source { get; set; } = "";
    public string Notes { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ServicePackage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    public decimal Hours { get; set; }
    public string Includes { get; set; } = "";
    public string Notes { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class StaffMember
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Name { get; set; } = "";
    public string Role { get; set; } = "photographer";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string UserId { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public decimal BaseSalary { get; set; }
    public decimal CommissionRate { get; set; }
    public string PayMode { get; set; } = PayModes.Monthly;
    public decimal HourlyRate { get; set; }
    public string Shift { get; set; } = StaffShifts.Normal;
    public string Notes { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class PayModes
{
    public const string Monthly = "monthly";
    public const string Hourly = "hourly";

    public static readonly string[] All = [Monthly, Hourly];

    public static bool IsKnown(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value, StringComparer.Ordinal);

    public static string Normalize(string? value) =>
        string.Equals(value, Hourly, StringComparison.OrdinalIgnoreCase) ? Hourly : Monthly;
}

public static class StaffShifts
{
    public const string Early = "ca1";
    public const string Normal = "normal";
    public const string Late = "ca2";

    public static readonly string[] All = [Early, Normal, Late];

    public static bool IsKnown(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value, StringComparer.Ordinal);

    public static string Normalize(string? value) =>
        IsKnown(value) ? value!.Trim().ToLowerInvariant() : Normal;

    public static string Label(string? value) => Normalize(value) switch
    {
        Early => "Ca 1 (7:30–17:30)",
        Late => "Ca 2 (9:00–19:00)",
        _ => "Ca thường (8:30–18:30)"
    };

    public static (string Start, string End) Window(string? value) => Normalize(value) switch
    {
        Early => ("07:30", "17:30"),
        Late => ("09:00", "19:00"),
        _ => ("08:30", "18:30")
    };
}

// ---------------------------------------------------------------- operations

/// <summary>
/// Một dòng trong sổ thu chi. Trước phiên bản 8 đây chỉ là khoản chi;
/// từ phiên bản 8 trường <see cref="Kind"/> phân biệt khoản thu và khoản chi.
/// </summary>
public sealed class ExpenseEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Kind { get; set; } = FinanceEntryKinds.Expense;
    public string Date { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Amount { get; set; }
    public string Note { get; set; } = "";
    public List<FinancePhoto> Photos { get; set; } = [];
    public string CreatedById { get; set; } = "";
    public string CreatedByName { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Ảnh chứng từ gắn với một khoản thu/chi. Tệp nằm trong thư mục dữ liệu
/// và chỉ được trả về qua endpoint có kiểm tra quyền tài chính.
/// </summary>
public sealed class FinancePhoto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string FileName { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public string Caption { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class FinanceEntryKinds
{
    public const string Income = "income";
    public const string Expense = "expense";

    public static readonly string[] All = [Income, Expense];

    public static string Normalize(string? value) =>
        string.Equals(value, Income, StringComparison.OrdinalIgnoreCase) ? Income : Expense;

    public static bool IsKnown(string? value) =>
        All.Contains(value ?? "", StringComparer.OrdinalIgnoreCase);

    public static string Label(string? value) =>
        Normalize(value) == Income ? "Khoản thu" : "Khoản chi";
}

public static class ExpenseCategories
{
    public static readonly string[] All =
    [
        "Thuê studio", "Nhân sự", "Quảng cáo", "Đồ nghề & thiết bị",
        "Điện nước", "Vận chuyển", "Thuế & phí", "Khác"
    ];

    public static string Normalize(string? value)
    {
        var trimmed = (value ?? "").Trim();
        if (trimmed.Length == 0)
        {
            return "Khác";
        }

        return All.FirstOrDefault(category =>
            string.Equals(category, trimmed, StringComparison.OrdinalIgnoreCase)) ?? AlbumText.TrimTo(trimmed, 60);
    }
}

public static class IncomeCategories
{
    public static readonly string[] All =
    [
        "Bán gói dịch vụ", "Cho thuê studio", "Bán váy & phụ kiện",
        "Dịch vụ chỉnh sửa", "Hoàn tiền", "Thu khác"
    ];

    public static string Normalize(string? value)
    {
        var trimmed = (value ?? "").Trim();
        if (trimmed.Length == 0)
        {
            return "Thu khác";
        }

        return All.FirstOrDefault(category =>
            string.Equals(category, trimmed, StringComparison.OrdinalIgnoreCase)) ?? AlbumText.TrimTo(trimmed, 60);
    }
}

public sealed class PayrollEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Month { get; set; } = "";
    public string StaffId { get; set; } = "";
    public decimal Base { get; set; }
    public decimal Hours { get; set; }
    public decimal CommissionBase { get; set; }
    public decimal Commission { get; set; }
    public decimal Total { get; set; }
    public bool Paid { get; set; }
    public string PaidAt { get; set; } = "";
    public DateTimeOffset ComputedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AttendanceRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string StaffId { get; set; } = "";
    public string Date { get; set; } = "";
    public string In { get; set; } = "";
    public string Out { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Một ô trong lịch chia ca: mỗi nhân sự có tối đa một bản ghi cho mỗi ngày.
/// <see cref="Shift"/> để trống nghĩa là dùng ca mặc định trong hồ sơ nhân sự,
/// "off" là ngày nghỉ. Không có bản ghi nghĩa là ngày chưa được phân ca.
/// </summary>
public sealed class ShiftAssignment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string StaffId { get; set; } = "";
    public string Date { get; set; } = "";
    public string Shift { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class ShiftAssignmentKinds
{
    public const string Profile = "";
    public const string Off = "off";

    public static bool IsKnown(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized == Off || StaffShifts.IsKnown(normalized);
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Profile;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized == Off)
        {
            return Off;
        }

        return StaffShifts.IsKnown(normalized) ? normalized : Profile;
    }

    public static string Label(string? value, string? profileShift)
    {
        var normalized = Normalize(value);
        return normalized switch
        {
            Off => "Nghỉ",
            Profile => StaffShifts.Label(profileShift),
            _ => StaffShifts.Label(normalized)
        };
    }
}

public sealed class Outfit
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Size { get; set; } = "";
    public string Color { get; set; } = "";
    public int Quantity { get; set; }
    public string Note { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class OutfitCategories
{
    public static readonly string[] All =
    [
        "Váy cưới", "Váy dạ hội", "Vest & Suit", "Áo dài", "Hanbok",
        "Thời trang", "Trẻ em", "Phụ kiện", "Khác"
    ];

    public static string Normalize(string? value)
    {
        var trimmed = (value ?? "").Trim();
        if (trimmed.Length == 0)
        {
            return "Khác";
        }

        return All.FirstOrDefault(category =>
            string.Equals(category, trimmed, StringComparison.OrdinalIgnoreCase)) ?? AlbumText.TrimTo(trimmed, 60);
    }
}

public sealed class StudioConcept
{
    public string Category { get; set; } = "";
    public string AlbumId { get; set; } = "";
    public bool IsPublished { get; set; }
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Fee { get; set; }
    public string Status { get; set; } = ConceptStatuses.Ready;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class ConceptStatuses
{
    public const string Ready = "ready";
    public const string Maintenance = "maintenance";

    public static readonly string[] All = [Ready, Maintenance];

    public static bool IsKnown(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value, StringComparer.Ordinal);

    public static string Normalize(string? value) =>
        string.Equals(value, Maintenance, StringComparison.OrdinalIgnoreCase) ? Maintenance : Ready;
}

public sealed class OutfitLoan
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string OutfitId { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public string BorrowerType { get; set; } = LoanBorrowerTypes.Customer;
    public string CustomerId { get; set; } = "";
    public string StaffId { get; set; } = "";
    public string BookingId { get; set; } = "";
    public string BorrowDate { get; set; } = "";
    public string DueDate { get; set; } = "";
    public string ReturnDate { get; set; } = "";
    public decimal Deposit { get; set; }
    public string Notes { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class LoanBorrowerTypes
{
    public const string Customer = "customer";
    public const string Staff = "staff";

    public static readonly string[] All = [Customer, Staff];

    public static bool IsKnown(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value, StringComparer.Ordinal);

    public static string Normalize(string? value) =>
        string.Equals(value, Staff, StringComparison.OrdinalIgnoreCase) ? Staff : Customer;
}

public sealed class NotificationEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Type { get; set; } = NotificationTypes.System;
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Link { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class NotificationTypes
{
    public const string Booking = "booking";
    public const string Payment = "payment";
    public const string Contract = "contract";
    public const string Wardrobe = "wardrobe";
    public const string Payroll = "payroll";
    public const string System = "system";

    public static readonly string[] All =
        [Booking, Payment, Contract, Wardrobe, Payroll, System];

    public static bool IsKnown(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value, StringComparer.Ordinal);

    public static string Normalize(string? value) =>
        IsKnown(value) ? value!.Trim().ToLowerInvariant() : System;
}

public sealed class NotificationSettings
{
    public bool Master { get; set; } = true;
    public bool Booking { get; set; } = true;
    public bool Payment { get; set; } = true;
    public bool Contract { get; set; } = true;
    public bool Wardrobe { get; set; } = true;
    public bool Payroll { get; set; } = true;
}

public static class BookingStatuses
{
    public const string Pending = "pending";
    public const string Confirmed = "confirmed";
    public const string Shooting = "shooting";
    public const string Editing = "editing";
    public const string Delivered = "delivered";
    public const string Cancelled = "cancelled";

    public static readonly string[] All =
    [
        Pending, Confirmed, Shooting, Editing, Delivered, Cancelled
    ];

    public static bool IsKnown(string? status) =>
        !string.IsNullOrWhiteSpace(status) && All.Contains(status, StringComparer.Ordinal);
}

public sealed class Booking
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Code { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string PackageId { get; set; } = "";
    public string Date { get; set; } = "";
    public string StartTime { get; set; } = "09:00";
    public decimal Hours { get; set; }
    public string Location { get; set; } = "";
    public string PhotographerId { get; set; } = "";
    public string Status { get; set; } = BookingStatuses.Pending;
    public string Notes { get; set; } = "";
    public string AlbumId { get; set; } = "";
    public decimal Price { get; set; }
    public List<BookingStatusEntry> StatusHistory { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class BookingStatusEntry
{
    public string Status { get; set; } = BookingStatuses.Pending;
    public string ActorName { get; set; } = "";
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AuditLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Module { get; set; } = "";
    public string Action { get; set; } = "";
    public string Summary { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string ActorName { get; set; } = "";
    public string ActorRole { get; set; } = "employee";
    public string EntityId { get; set; } = "";
    public List<string> Details { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

// ---------- Orders, payments & contracts ----------

public static class OrderStatuses
{
    public const string Draft = "draft";
    public const string Confirmed = "confirmed";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";

    public static readonly string[] All = [Draft, Confirmed, Completed, Cancelled];

    public static bool IsKnown(string? status) =>
        !string.IsNullOrWhiteSpace(status) && All.Contains(status, StringComparer.Ordinal);
}

public sealed class OrderItem
{
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}

public sealed class Order
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Code { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string BookingId { get; set; } = "";
    public List<OrderItem> Items { get; set; } = [];
    public decimal Discount { get; set; }
    public string Status { get; set; } = OrderStatuses.Confirmed;
    public string Notes { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class PaymentMethods
{
    public const string Cash = "cash";
    public const string Transfer = "transfer";
    public const string Card = "card";
    public const string Other = "other";

    public static readonly string[] All = [Cash, Transfer, Card, Other];

    public static bool IsKnown(string? method) =>
        !string.IsNullOrWhiteSpace(method) && All.Contains(method, StringComparer.Ordinal);
}

public sealed class Payment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Code { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public decimal Amount { get; set; }
    public string Method { get; set; } = PaymentMethods.Transfer;
    public string PaidAt { get; set; } = "";
    public string Note { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string ActorName { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class ContractStatuses
{
    public const string Draft = "draft";
    public const string Pending = "pending";
    public const string Signed = "signed";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";

    public static readonly string[] All = [Draft, Pending, Signed, Completed, Cancelled];

    public static bool IsKnown(string? status) =>
        !string.IsNullOrWhiteSpace(status) && All.Contains(status, StringComparer.Ordinal);
}

public sealed class ContractScheduleItem
{
    public string Label { get; set; } = "";
    public decimal Amount { get; set; }
    public string DueDate { get; set; } = "";
    public bool Paid { get; set; }
    public string PaidAt { get; set; } = "";
}

public sealed class Contract
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Code { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string BookingId { get; set; } = "";
    public string Service { get; set; } = "";
    public decimal Total { get; set; }
    public List<ContractScheduleItem> Schedule { get; set; } = [];
    public string Terms { get; set; } = "";
    public string Status { get; set; } = ContractStatuses.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

// ---------- Task board ----------

public sealed class TaskBoard
{
    public string Id { get; set; } = "studio-board";
    public string Title { get; set; } = "Bảng công việc";
    public string Description { get; set; } = "";
    public string Theme { get; set; } = "ink";
    public string CardBackground { get; set; } = "#fbf8f2";
    public string CardTextColor { get; set; } = "#2b251e";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<TaskLabel> Labels { get; set; } = [];
    public List<TaskList> Lists { get; set; } = [];
}

public sealed class TaskLabel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#b25f46";
}

public sealed class TaskList
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string Title { get; set; } = "";
    public string Color { get; set; } = "#c3a47a";
    public int Position { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<TaskCard> Cards { get; set; } = [];
}

public sealed class TaskCard
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string DueDate { get; set; } = "";
    public bool IsDueComplete { get; set; }
    public string AlbumId { get; set; } = "";
    public string AssigneeId { get; set; } = "";
    public string Priority { get; set; } = "normal";
    public string BookingId { get; set; } = "";
    public int Position { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<string> LabelIds { get; set; } = [];
    public List<TaskChecklistItem> Checklist { get; set; } = [];
    public List<TaskActivity> Activities { get; set; } = [];
}

public sealed class TaskChecklistItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string Text { get; set; } = "";
    public bool Done { get; set; }
}

public sealed class TaskActivity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string Type { get; set; } = "updated";
    public string ActorId { get; set; } = "";
    public string ActorName { get; set; } = "";
    public string ActorRole { get; set; } = "employee";
    public string Summary { get; set; } = "";
    public List<string> Details { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

// ---------- Requests ----------

public sealed class CreateAlbumRequest
{
    public string Title { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string EventDate { get; set; } = "";
    public string Location { get; set; } = "";
    public string Description { get; set; } = "";
    public string DriveFolderUrl { get; set; } = "";
    public bool IncludeSubfolders { get; set; }
    public bool AllowFavorites { get; set; } = true;
    public bool AllowDownloads { get; set; }
    public bool IsPublished { get; set; } = true;
    public string? CoverDriveFileId { get; set; }
}

public sealed class UpdateAlbumRequest
{
    public string? Title { get; set; }
    public string? ClientName { get; set; }
    public string? CustomerId { get; set; }
    public string? EventDate { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public string? CoverPhotoId { get; set; }
    public bool? AllowFavorites { get; set; }
    public bool? AllowDownloads { get; set; }
    public bool? IsPublished { get; set; }
}

public sealed class PreviewDriveRequest
{
    public string Url { get; set; } = "";
    public bool IncludeSubfolders { get; set; }
}

public sealed class ToggleFavoriteRequest
{
    public string PhotoId { get; set; } = "";
}

public sealed class SubmitSelectionRequest
{
    public string? Note { get; set; }
}

public sealed class SaveSettingsRequest
{
    public string? StudioName { get; set; }
    public string? ContactEmail { get; set; }
    public string? GoogleApiKey { get; set; }
    public WebsiteContent? Website { get; set; }
    public bool ClearGoogleApiKey { get; set; }
}

public sealed class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Code { get; set; }
}

public sealed class CreateStudioUserRequest
{
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "employee";
    public string Password { get; set; } = "";
    public string? StaffId { get; set; }
    public List<string>? Permissions { get; set; }
    public bool? MustChangePassword { get; set; }
}

public sealed class UpdateStudioUserRequest
{
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    public string? StaffId { get; set; }
    public List<string>? Permissions { get; set; }
    public bool? MustChangePassword { get; set; }
}

public sealed class ResetStudioUserPasswordRequest
{
    public string Password { get; set; } = "";
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

public sealed class CreateCustomerRequest
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "individual";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public string Source { get; set; } = "";
    public string Notes { get; set; } = "";
}

public sealed class UpdateCustomerRequest
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class CreatePackageRequest
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    public decimal Hours { get; set; }
    public string Includes { get; set; } = "";
    public string Notes { get; set; } = "";
}

public sealed class UpdatePackageRequest
{
    public string? Name { get; set; }
    public string? Category { get; set; }
    public decimal? Price { get; set; }
    public decimal? Hours { get; set; }
    public string? Includes { get; set; }
    public string? Notes { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class CreateStaffRequest
{
    public string Name { get; set; } = "";
    public string Role { get; set; } = "photographer";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string UserId { get; set; } = "";
    public decimal BaseSalary { get; set; }
    public decimal CommissionRate { get; set; }
    public string PayMode { get; set; } = PayModes.Monthly;
    public decimal HourlyRate { get; set; }
    public string Shift { get; set; } = StaffShifts.Normal;
    public string Notes { get; set; } = "";
}

public sealed class UpdateStaffRequest
{
    public string? Name { get; set; }
    public string? Role { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? UserId { get; set; }
    public decimal? BaseSalary { get; set; }
    public decimal? CommissionRate { get; set; }
    public string? PayMode { get; set; }
    public decimal? HourlyRate { get; set; }
    public string? Shift { get; set; }
    public string? Notes { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class CreateExpenseRequest
{
    public string Date { get; set; } = "";
    public string Category { get; set; } = "Khác";
    public decimal Amount { get; set; }
    public string Note { get; set; } = "";
}

public sealed class UpdateExpenseRequest
{
    public string? Date { get; set; }
    public string? Category { get; set; }
    public decimal? Amount { get; set; }
    public string? Note { get; set; }
}

public sealed class CreateFinanceEntryRequest
{
    public string Kind { get; set; } = FinanceEntryKinds.Expense;
    public string Date { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Amount { get; set; }
    public string Note { get; set; } = "";
}

public sealed class UpdateFinanceEntryRequest
{
    public string? Kind { get; set; }
    public string? Date { get; set; }
    public string? Category { get; set; }
    public decimal? Amount { get; set; }
    public string? Note { get; set; }
    public List<FinancePhotoCaptionRequest>? Photos { get; set; }
}

public sealed class FinancePhotoCaptionRequest
{
    public string Id { get; set; } = "";
    public string Caption { get; set; } = "";
}

public sealed class ComputePayrollRequest
{
    public string Month { get; set; } = "";
}

public sealed class TogglePayrollPaidRequest
{
    public bool Paid { get; set; } = true;
}

public sealed class CreateAttendanceRequest
{
    public string StaffId { get; set; } = "";
    public string Date { get; set; } = "";
    public string In { get; set; } = "";
    public string Out { get; set; } = "";
    public string Note { get; set; } = "";
}

public sealed class UpdateAttendanceRequest
{
    public string? In { get; set; }
    public string? Out { get; set; }
    public string? Note { get; set; }
}

public sealed class SaveShiftRequest
{
    public string StaffId { get; set; } = "";
    public string Date { get; set; } = "";
    public string Shift { get; set; } = "";
    public string Note { get; set; } = "";
}

public sealed class ShiftWeekRequest
{
    public string Week { get; set; } = "";
}

public sealed class CreateOutfitRequest
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Khác";
    public string Size { get; set; } = "";
    public string Color { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public string Note { get; set; } = "";
}

public sealed class UpdateOutfitRequest
{
    public string? Name { get; set; }
    public string? Category { get; set; }
    public string? Size { get; set; }
    public string? Color { get; set; }
    public int? Quantity { get; set; }
    public string? Note { get; set; }
}

public sealed class CreateConceptRequest
{
    public string? Category { get; set; }
    public string? AlbumId { get; set; }
    public bool? IsPublished { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Fee { get; set; }
    public string Status { get; set; } = ConceptStatuses.Ready;
}

public sealed class UpdateConceptRequest
{
    public string? Category { get; set; }
    public string? AlbumId { get; set; }
    public bool? IsPublished { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? Fee { get; set; }
    public string? Status { get; set; }
}

public sealed class CreateLoanRequest
{
    public string OutfitId { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public string BorrowerType { get; set; } = LoanBorrowerTypes.Customer;
    public string CustomerId { get; set; } = "";
    public string StaffId { get; set; } = "";
    public string BookingId { get; set; } = "";
    public string BorrowDate { get; set; } = "";
    public string DueDate { get; set; } = "";
    public decimal Deposit { get; set; }
    public string Notes { get; set; } = "";
}

public sealed class UpdateLoanRequest
{
    public string? OutfitId { get; set; }
    public int? Quantity { get; set; }
    public string? BorrowerType { get; set; }
    public string? CustomerId { get; set; }
    public string? StaffId { get; set; }
    public string? BookingId { get; set; }
    public string? BorrowDate { get; set; }
    public string? DueDate { get; set; }
    public decimal? Deposit { get; set; }
    public string? Notes { get; set; }
}

public sealed class ReturnLoanRequest
{
    public string ReturnDate { get; set; } = "";
}

public sealed class NotificationSettingsRequest
{
    public bool Master { get; set; } = true;
    public bool Booking { get; set; } = true;
    public bool Payment { get; set; } = true;
    public bool Contract { get; set; } = true;
    public bool Wardrobe { get; set; } = true;
    public bool Payroll { get; set; } = true;
}

public sealed class CreateBookingRequest
{
    public string CustomerId { get; set; } = "";
    public string PackageId { get; set; } = "";
    public string Date { get; set; } = "";
    public string StartTime { get; set; } = "09:00";
    public decimal? Hours { get; set; }
    public string Location { get; set; } = "";
    public string PhotographerId { get; set; } = "";
    public string Status { get; set; } = BookingStatuses.Pending;
    public string Notes { get; set; } = "";
    public string AlbumId { get; set; } = "";
    public decimal? Price { get; set; }
}

public sealed class UpdateBookingRequest
{
    public string? CustomerId { get; set; }
    public string? PackageId { get; set; }
    public string? Date { get; set; }
    public string? StartTime { get; set; }
    public decimal? Hours { get; set; }
    public string? Location { get; set; }
    public string? PhotographerId { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
    public string? AlbumId { get; set; }
    public decimal? Price { get; set; }
}

public sealed class UpdateBookingStatusRequest
{
    public string Status { get; set; } = "";
}

public sealed class OrderItemRequest
{
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}

public sealed class CreateOrderRequest
{
    public string CustomerId { get; set; } = "";
    public string BookingId { get; set; } = "";
    public List<OrderItemRequest> Items { get; set; } = [];
    public decimal Discount { get; set; }
    public string Status { get; set; } = OrderStatuses.Confirmed;
    public string Notes { get; set; } = "";
}

public sealed class UpdateOrderRequest
{
    public string? CustomerId { get; set; }
    public string? BookingId { get; set; }
    public List<OrderItemRequest>? Items { get; set; }
    public decimal? Discount { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpdateOrderStatusRequest
{
    public string Status { get; set; } = "";
}

public sealed class RecordPaymentRequest
{
    public decimal Amount { get; set; }
    public string Method { get; set; } = PaymentMethods.Transfer;
    public string PaidAt { get; set; } = "";
    public string Note { get; set; } = "";
}

public sealed class CreateContractRequest
{
    public string CustomerId { get; set; } = "";
    public string BookingId { get; set; } = "";
    public string Service { get; set; } = "";
    public decimal Total { get; set; }
    public string Preset { get; set; } = "30/40/30";
    public string Terms { get; set; } = "";
    public string Status { get; set; } = ContractStatuses.Pending;
}

public sealed class UpdateContractRequest
{
    public string? CustomerId { get; set; }
    public string? BookingId { get; set; }
    public string? Service { get; set; }
    public decimal? Total { get; set; }
    public string? Preset { get; set; }
    public string? Terms { get; set; }
    public string? Status { get; set; }
}

public sealed class ToggleContractScheduleRequest
{
    public bool Paid { get; set; }
}

public sealed class ImportCrmRequest
{
    public string Json { get; set; } = "";
    public bool DryRun { get; set; } = true;
    public bool ReplaceExisting { get; set; }
}

public sealed class UpdateBoardRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Theme { get; set; }
    public string? CardBackground { get; set; }
    public string? CardTextColor { get; set; }
}

public sealed class CreateTaskLabelRequest
{
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
}

public sealed class UpdateTaskLabelRequest
{
    public string? Name { get; set; }
    public string? Color { get; set; }
}

public sealed class CreateTaskListRequest
{
    public string Title { get; set; } = "";
    public string? Color { get; set; }
}

public sealed class UpdateTaskListRequest
{
    public string? Title { get; set; }
    public string? Color { get; set; }
}

public sealed class MoveTaskListRequest
{
    public int Position { get; set; }
}

public sealed class CreateTaskCardRequest
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? DueDate { get; set; }
    public bool? IsDueComplete { get; set; }
    public List<string>? LabelIds { get; set; }
    public List<TaskChecklistItemRequest>? Checklist { get; set; }
    public string? AlbumId { get; set; }
    public string? AssigneeId { get; set; }
    public string? Priority { get; set; }
    public string? BookingId { get; set; }
}

public sealed class UpdateTaskCardRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? DueDate { get; set; }
    public bool? IsDueComplete { get; set; }
    public List<string>? LabelIds { get; set; }
    public List<TaskChecklistItemRequest>? Checklist { get; set; }
    public string? AlbumId { get; set; }
    public string? AssigneeId { get; set; }
    public string? Priority { get; set; }
    public string? BookingId { get; set; }
    public string? ListId { get; set; }
}

public sealed class MoveTaskCardRequest
{
    public string ListId { get; set; } = "";
    public int Position { get; set; }
}

public sealed class TaskChecklistItemRequest
{
    public string? Id { get; set; }
    public string Text { get; set; } = "";
    public bool Done { get; set; }
}

// ---------- API DTOs ----------

public sealed record AlbumSummaryDto(
    string Id,
    string ShareToken,
    string Title,
    string ClientName,
    string CustomerId,
    string EventDate,
    string Location,
    string? CoverThumbUrl,
    int PhotoCount,
    int SelectedCount,
    bool IsPublished,
    bool IsDemo,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt);

public sealed record PhotoDto(
    string Id,
    string Code,
    string FileName,
    string ThumbUrl,
    string PreviewUrl,
    string? DownloadUrl,
    int Width,
    int Height,
    bool IsSelected,
    DateTimeOffset? SelectedAt);

public sealed record AlbumAdminDto(
    string Id,
    string ShareToken,
    string Title,
    string ClientName,
    string CustomerId,
    string EventDate,
    string Location,
    string Description,
    string DriveFolderUrl,
    string DriveFolderId,
    bool IncludeSubfolders,
    bool AllowFavorites,
    bool AllowDownloads,
    bool IsPublished,
    bool IsDemo,
    string CoverPhotoId,
    string? CoverUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? FavoritesUpdatedAt,
    DateTimeOffset? SubmittedAt,
    string ClientNote,
    int PhotoCount,
    int SelectedCount,
    List<PhotoDto> Photos);

public sealed record PublicAlbumDto(
    string ShareToken,
    string Title,
    string ClientName,
    string EventDate,
    string Location,
    string Description,
    string StudioName,
    string StudioEmail,
    string? CoverUrl,
    bool AllowFavorites,
    bool AllowDownloads,
    DateTimeOffset? SubmittedAt,
    string ClientNote,
    List<PhotoDto> Photos,
    List<string> SelectedPhotoIds);

public sealed record PublicFavoriteDto(
    List<string> SelectedPhotoIds,
    int SelectedCount,
    DateTimeOffset? SubmittedAt);

public sealed record PublicSubmissionDto(
    DateTimeOffset SubmittedAt,
    string ClientNote,
    int SelectedCount);

public sealed record DrivePreviewPhotoDto(
    string DriveFileId,
    string Code,
    string FileName,
    string ThumbUrl,
    int Width,
    int Height);

public sealed record DrivePreviewDto(
    string FolderId,
    string FolderName,
    int TotalCount,
    List<DrivePreviewPhotoDto> Photos,
    bool Truncated);

public sealed record ApiErrorDto(string Error, string? Code = null);

public sealed record SessionUserDto(
    string Id,
    string Username,
    string DisplayName,
    string Role,
    List<string> Permissions,
    bool MustChangePassword);

public sealed record SessionDto(bool Authenticated, string StudioName, SessionUserDto? User);

public sealed record AdminSettingsDto(
    string StudioName,
    string ContactEmail,
    bool GoogleConfigured,
    string? GoogleKeyHint,
    WebsiteContent Website,
    string LogoUrl,
    string LoginImageUrl,
    string HomeImageUrl);

public sealed record StudioUserDto(
    string Id,
    string Username,
    string DisplayName,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    bool IsCurrent,
    List<string> Permissions,
    bool MustChangePassword,
    string StaffId);

public sealed record CustomerDto(
    string Id,
    string Name,
    string Type,
    string Phone,
    string Email,
    string Address,
    string Source,
    string Notes,
    bool IsActive,
    int AlbumCount,
    int BookingCount,
    DateTimeOffset? LastBookingDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ServicePackageDto(
    string Id,
    string Name,
    string Category,
    decimal Price,
    decimal Hours,
    string Includes,
    string Notes,
    bool IsActive,
    int BookingCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record StaffDto(
    string Id,
    string Name,
    string Role,
    string Phone,
    string Email,
    string UserId,
    string? UserName,
    bool IsActive,
    decimal BaseSalary,
    decimal CommissionRate,
    string PayMode,
    decimal HourlyRate,
    string Shift,
    string Notes,
    int BookingCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record BookingStatusDto(
    string Status,
    string ActorName,
    DateTimeOffset ChangedAt);

public sealed record BookingDto(
    string Id,
    string Code,
    string CustomerId,
    string? CustomerName,
    string? CustomerPhone,
    string PackageId,
    string? PackageName,
    string Date,
    string StartTime,
    decimal Hours,
    string Location,
    string PhotographerId,
    string? PhotographerName,
    string Status,
    string Notes,
    string AlbumId,
    string? AlbumTitle,
    decimal Price,
    List<BookingStatusDto> StatusHistory,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AuditLogDto(
    string Id,
    string Module,
    string Action,
    string Summary,
    string ActorId,
    string ActorName,
    string ActorRole,
    string EntityId,
    List<string> Details,
    DateTimeOffset CreatedAt);

public sealed record CrmSummaryDto(
    int CustomerCount,
    int ActiveCustomerCount,
    int PackageCount,
    int StaffCount,
    int UpcomingBookingCount,
    int MonthBookingCount,
    int TodayBookingCount,
    int PendingBookingCount,
    List<BookingDto> NextBookings);

public sealed record OrderItemDto(
    string Name,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount);

public sealed record PaymentDto(
    string Id,
    string Code,
    string OrderId,
    string? OrderCode,
    string CustomerId,
    string? CustomerName,
    decimal Amount,
    string Method,
    string PaidAt,
    string Note,
    string ActorName,
    DateTimeOffset CreatedAt);

public sealed record OrderDto(
    string Id,
    string Code,
    string CustomerId,
    string? CustomerName,
    string? CustomerPhone,
    string BookingId,
    string? BookingCode,
    List<OrderItemDto> Items,
    decimal Subtotal,
    decimal Discount,
    decimal Total,
    decimal PaidAmount,
    decimal Remaining,
    string PaymentStatus,
    string Status,
    string Notes,
    int PaymentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record OrderDetailDto(
    OrderDto Order,
    List<PaymentDto> Payments);

public sealed record ContractScheduleDto(
    string Label,
    decimal Amount,
    string DueDate,
    bool Paid,
    string PaidAt,
    bool IsOverdue);

public sealed record ContractDto(
    string Id,
    string Code,
    string CustomerId,
    string? CustomerName,
    string? CustomerPhone,
    string BookingId,
    string? BookingCode,
    string Service,
    decimal Total,
    decimal PaidAmount,
    decimal Remaining,
    int ProgressPercent,
    string Status,
    List<ContractScheduleDto> Schedule,
    string Terms,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record InvoiceDto(
    string Code,
    DateTimeOffset CreatedAt,
    string StudioName,
    string StudioEmail,
    string CustomerName,
    string CustomerPhone,
    string CustomerAddress,
    List<OrderItemDto> Items,
    decimal Subtotal,
    decimal Discount,
    decimal Total,
    decimal PaidAmount,
    decimal Remaining,
    string Notes);

public sealed record ReportMonthDto(
    string Month,
    decimal OrderRevenue,
    decimal ContractRevenue,
    decimal Total);

public sealed record ReportMethodDto(
    string Method,
    int Count,
    decimal Amount);

public sealed record ReportCustomerDto(
    string CustomerId,
    string CustomerName,
    decimal Revenue,
    int OrderCount);

public sealed record ReportSummaryDto(
    string From,
    string To,
    decimal OrderRevenue,
    decimal ContractRevenue,
    decimal TotalRevenue,
    int OrderCount,
    int PaidOrderCount,
    int UnpaidOrderCount,
    decimal OrderDebt,
    decimal ContractPaid,
    decimal ContractDebt,
    int ContractCount,
    int PendingContractCount,
    List<ReportMonthDto> Months,
    List<ReportMethodDto> Methods,
    List<ReportCustomerDto> TopCustomers);

public sealed record SalesOverviewDto(
    int OrderCount,
    int OpenOrderCount,
    decimal MonthRevenue,
    decimal OutstandingDebt,
    int ContractCount,
    int PendingContractCount,
    decimal ContractOutstanding);

public sealed record CustomerDetailDto(
    CustomerDto Customer,
    List<BookingDto> Bookings,
    List<AlbumSummaryDto> Albums);

public sealed record AlbumOptionDto(
    string Id,
    string Title,
    string ClientName,
    string CustomerId);

public sealed record StaffOptionDto(
    string Id,
    string Name,
    string Role);

public sealed record BookingOptionDto(
    string Id,
    string Code,
    string? CustomerName,
    string Date,
    string Status);

public sealed record BoardContextDto(
    List<AlbumOptionDto> Albums,
    List<StaffOptionDto> Staff,
    List<BookingOptionDto> Bookings);

public sealed record ImportCrmResultDto(
    bool DryRun,
    int Customers,
    int Packages,
    int Staff,
    int Bookings,
    int Orders,
    int Payments,
    int Contracts,
    int Expenses,
    int Payrolls,
    int Attendance,
    int Outfits,
    int Concepts,
    int Loans,
    int Notifications,
    int Skipped,
    List<string> Warnings);

public sealed record FinancePhotoDto(
    string Id,
    string Url,
    string Name,
    string ContentType,
    long Size,
    string Caption);

public sealed record FinanceEntryDto(
    string Id,
    string Kind,
    string Date,
    string Category,
    decimal Amount,
    string Note,
    List<FinancePhotoDto> Photos,
    string CreatedByName,
    DateTimeOffset CreatedAt);

public sealed record PayrollDto(
    string Id,
    string Month,
    string StaffId,
    string StaffName,
    decimal Base,
    decimal Hours,
    decimal CommissionBase,
    decimal Commission,
    decimal Total,
    bool Paid,
    string PaidAt,
    DateTimeOffset ComputedAt);

public sealed record PayrollComputeDto(
    string Month,
    int Count,
    decimal Total,
    List<PayrollDto> Payrolls);

public sealed record AttendanceDto(
    string Id,
    string StaffId,
    string StaffName,
    string Date,
    string In,
    string Out,
    decimal Hours,
    int LateMinutes,
    int EarlyMinutes,
    string Status,
    string Shift,
    string Note);

public sealed record AttendanceMonthDto(
    string Month,
    string StaffId,
    string StaffName,
    string Shift,
    int DaysWorked,
    decimal Hours,
    int LateCount,
    int EarlyCount,
    int MissingOutCount);

public sealed record ShiftAssignmentDto(
    string Id,
    string StaffId,
    string Date,
    string Shift,
    string Note);

public sealed record ShiftStaffDto(
    string Id,
    string Name,
    string Role,
    string Shift,
    bool IsActive,
    List<ShiftAssignmentDto> Assignments);

public sealed record ShiftDayDto(
    string Date,
    string Weekday,
    string ShortDate,
    string Window,
    int Scheduled,
    int Off,
    bool IsToday);

public sealed record ShiftWeekDto(
    string WeekStart,
    string WeekEnd,
    string Label,
    List<ShiftDayDto> Days,
    List<ShiftStaffDto> Staff);

public sealed record OutfitDto(
    string Id,
    string Name,
    string Category,
    string Size,
    string Color,
    int Quantity,
    string Note,
    int LoanedQuantity,
    DateTimeOffset CreatedAt);

public sealed record ConceptDto(
    string Id,
    string Name,
    string Description,
    decimal Fee,
    string Status,
    DateTimeOffset CreatedAt,
    string Category,
    string AlbumId,
    bool IsPublished);

public sealed record LoanDto(
    string Id,
    string OutfitId,
    string OutfitName,
    int Quantity,
    string BorrowerType,
    string CustomerId,
    string? CustomerName,
    string StaffId,
    string? StaffName,
    string BookingId,
    string? BookingCode,
    string BorrowDate,
    string DueDate,
    string ReturnDate,
    decimal Deposit,
    string Notes,
    bool IsReturned,
    bool IsOverdue);

public sealed record NotificationDto(
    string Id,
    string Type,
    string Title,
    string Body,
    string Link,
    bool IsRead,
    DateTimeOffset CreatedAt);

public sealed record NotificationSettingsDto(
    bool Master,
    bool Booking,
    bool Payment,
    bool Contract,
    bool Wardrobe,
    bool Payroll);

public sealed record FinanceCategoryDto(string Category, decimal Amount);

public sealed record FinanceStaffDto(
    string StaffId,
    string StaffName,
    decimal Base,
    decimal Hours,
    decimal Commission,
    decimal Total,
    bool Paid);

public sealed record FinanceOverviewDto(
    string Month,
    decimal OrderRevenue,
    decimal ContractRevenue,
    decimal OtherIncome,
    decimal TotalRevenue,
    decimal ExpenseTotal,
    decimal PayrollTotal,
    decimal PayrollPaid,
    decimal Profit,
    decimal OutstandingDebt,
    decimal ContractOutstanding,
    int UnpaidPayrollCount,
    int PendingContractCount,
    List<FinanceCategoryDto> ExpenseBreakdown,
    List<FinanceCategoryDto> IncomeBreakdown,
    List<FinanceStaffDto> PayrollBreakdown,
    List<FinanceEntryDto> RecentEntries);

public sealed record TaskLabelDto(
    string Id,
    string Name,
    string Color);

public sealed record TaskChecklistItemDto(
    string Id,
    string Text,
    bool Done);

public sealed record TaskActivityDto(
    string Id,
    string Type,
    string ActorId,
    string ActorName,
    string ActorRole,
    string Summary,
    List<string> Details,
    DateTimeOffset CreatedAt);

public sealed record TaskCardDto(
    string Id,
    string ListId,
    string Title,
    string Description,
    string DueDate,
    bool IsDueComplete,
    List<TaskLabelDto> Labels,
    List<TaskChecklistItemDto> Checklist,
    List<TaskActivityDto> Activities,
    string AlbumId,
    string? AlbumTitle,
    string? AlbumThumbUrl,
    string AssigneeId,
    string? AssigneeName,
    string? AssigneeRole,
    string Priority,
    string BookingId,
    string? BookingCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TaskListDto(
    string Id,
    string Title,
    string Color,
    List<TaskCardDto> Cards);

public sealed record TaskBoardDto(
    string Id,
    string Title,
    string Description,
    string Theme,
    string CardBackground,
    string CardTextColor,
    List<TaskLabelDto> Labels,
    List<TaskListDto> Lists,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
