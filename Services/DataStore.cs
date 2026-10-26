using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Globalization;
using MayAlbum.Models;

namespace MayAlbum.Services;

/// <summary>
/// A deliberately small JSON-backed store. It keeps the app portable for a
/// single-studio deployment, while still writing atomically and serializing
/// mutations so two requests cannot overwrite each other.
/// </summary>
public sealed class DataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly object _sync = new();
    private readonly string _storePath;
    private readonly string _configuredGoogleApiKey;
    private readonly ILogger<DataStore> _logger;
    private AppData _data;

    public DataStore(IWebHostEnvironment environment, IConfiguration configuration, ILogger<DataStore> logger)
    {
        _logger = logger;
        AdminCode = string.IsNullOrWhiteSpace(configuration["AdminCode"])
            ? "studio2026"
            : configuration["AdminCode"]!.Trim();
        _configuredGoogleApiKey = configuration["GoogleDrive:ApiKey"]?.Trim() ?? "";

        var dataDirectory = configuration["Data:Directory"];
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            dataDirectory = Path.Combine(environment.ContentRootPath, "data");
        }

        dataDirectory = Path.GetFullPath(dataDirectory, environment.ContentRootPath);
        Directory.CreateDirectory(dataDirectory);
        _storePath = Path.Combine(dataDirectory, "store.json");

        if (File.Exists(_storePath))
        {
            try
            {
                var json = File.ReadAllText(_storePath, Encoding.UTF8);
                _data = JsonSerializer.Deserialize<AppData>(json, JsonOptions)
                        ?? throw new InvalidOperationException("Tệp dữ liệu rỗng.");
                var migrated = Migrate(_data, AdminCode);
                Normalize(_data, AdminCode);
                if (migrated)
                {
                    SaveLocked();
                    _logger.LogInformation(
                        "Đã nâng cấp dữ liệu studio lên phiên bản {Version} tại {Path}",
                        _data.SchemaVersion,
                        _storePath);
                }

                _logger.LogInformation("Đã tải dữ liệu studio từ {Path}", _storePath);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Không thể đọc dữ liệu tại {_storePath}. Hãy kiểm tra hoặc khôi phục tệp JSON.", exception);
            }
        }
        else
        {
            _data = DemoAlbums.Create();
            Normalize(_data, AdminCode);
            SaveLocked();
            _logger.LogInformation("Đã tạo dữ liệu mẫu tại {Path}", _storePath);
        }
    }

    public string StorePath => _storePath;
    public string DataDirectory => Path.GetDirectoryName(_storePath) ?? AppContext.BaseDirectory;
    public string AdminCode { get; }

    public T Read<T>(Func<AppData, T> reader)
    {
        lock (_sync)
        {
            return reader(_data);
        }
    }

    /// <summary>
    /// Reads a deep copy so callers can simulate a write (for example a dry-run
    /// import) without touching the real store or writing to disk.
    /// </summary>
    public T ReadClone<T>(Func<AppData, T> reader)
    {
        lock (_sync)
        {
            var json = JsonSerializer.Serialize(_data, JsonOptions);
            var clone = JsonSerializer.Deserialize<AppData>(json, JsonOptions)
                        ?? throw new InvalidOperationException("Không thể sao chép dữ liệu studio.");
            return reader(clone);
        }
    }

    public T Write<T>(Func<AppData, T> writer)
    {
        lock (_sync)
        {
            var result = writer(_data);
            SaveLocked();
            return result;
        }
    }

    public void Write(Action<AppData> writer)
    {
        Write<object?>(data =>
        {
            writer(data);
            return null;
        });
    }

    public string GetEffectiveGoogleApiKey()
    {
        lock (_sync)
        {
            return string.IsNullOrWhiteSpace(_data.Settings.GoogleApiKey)
                ? _configuredGoogleApiKey
                : _data.Settings.GoogleApiKey.Trim();
        }
    }

    private void SaveLocked()
    {
        var json = JsonSerializer.Serialize(_data, JsonOptions);
        var tempPath = _storePath + ".tmp";
        File.WriteAllText(tempPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(tempPath, _storePath, overwrite: true);
    }

    private static bool Migrate(AppData data, string adminCode)
    {
        var migrated = false;

        // v2 introduces the studio task board. Keep every existing album,
        // selection and setting; only add the new board when it is absent.
        if (data.SchemaVersion < 2)
        {
            if (data.Board is null || data.Board.Lists.Count == 0)
            {
                data.Board = DemoBoard.Create();
            }

            data.SchemaVersion = 2;
            migrated = true;
        }

        // v3 adds individual studio accounts and per-card activity logs.
        // Existing cards are left untouched; their history starts empty.
        if (data.SchemaVersion < 3)
        {
            data.Users ??= [];
            if (data.Users.Count == 0)
            {
                data.Users.Add(CreateBootstrapUser(adminCode));
            }

            foreach (var card in data.Board?.Lists?.SelectMany(list => list.Cards) ?? Enumerable.Empty<TaskCard>())
            {
                card.Activities ??= [];
            }

            data.SchemaVersion = 3;
            migrated = true;
        }

        // v4 adds the studio CRM: customers, packages, staff, bookings,
        // a global audit log, granular permissions and richer task cards.
        // Existing data is preserved; new collections start empty.
        if (data.SchemaVersion < 4)
        {
            data.Customers ??= [];
            data.Packages ??= [];
            data.Staff ??= [];
            data.Bookings ??= [];
            data.AuditLogs ??= [];

            foreach (var user in data.Users)
            {
                user.Permissions ??= [];
                user.StaffId ??= "";
            }

            foreach (var album in data.Albums)
            {
                album.CustomerId ??= "";
            }

            foreach (var card in data.Board?.Lists?.SelectMany(list => list.Cards) ?? Enumerable.Empty<TaskCard>())
            {
                card.AssigneeId ??= "";
                card.Priority = string.IsNullOrWhiteSpace(card.Priority) ? "normal" : card.Priority;
                card.BookingId ??= "";
            }

            data.SchemaVersion = 4;
            migrated = true;
        }

        // v5 adds sales: orders, payments, invoices and contracts.
        // Existing CRM data is preserved; new collections start empty.
        if (data.SchemaVersion < 5)
        {
            data.Orders ??= [];
            data.Payments ??= [];
            data.Contracts ??= [];
            data.OrderSequence = Math.Max(1, data.OrderSequence);
            data.PaymentSequence = Math.Max(1, data.PaymentSequence);
            data.ContractSequence = Math.Max(1, data.ContractSequence);
            data.SchemaVersion = 5;
            migrated = true;
        }

        // v6 adds studio operations: expenses, payroll, attendance, wardrobe,
        // concepts, outfit loans and the notification inbox.
        // Existing data is preserved; new collections start empty.
        if (data.SchemaVersion < 6)
        {
            data.Expenses ??= [];
            data.Payrolls ??= [];
            data.Attendance ??= [];
            data.Outfits ??= [];
            data.Concepts ??= [];
            data.OutfitLoans ??= [];
            data.Notifications ??= [];
            data.NotificationSettings ??= new NotificationSettings();
            data.SchemaVersion = 6;
            migrated = true;
        }

        // v7 adds the weekly shift planner. Assignments are optional; days
        // without a record stay unscheduled until someone fills the week.
        if (data.SchemaVersion < 7)
        {
            data.ShiftAssignments ??= [];
            data.SchemaVersion = 7;
            migrated = true;
        }

        // v8 turns the expense ledger into a two-way cashbook (thu/chi) and
        // allows receipt photos with captions on each entry. Existing rows are
        // treated as expenses so historical totals stay unchanged.
        if (data.SchemaVersion < 8)
        {
            foreach (var entry in data.Expenses ?? [])
            {
                entry.Kind = FinanceEntryKinds.Normalize(entry.Kind);
                entry.Photos ??= [];
            }

            data.SchemaVersion = 8;
            migrated = true;
        }

        return migrated;
    }

    private static void Normalize(AppData data, string adminCode)
    {
        data.Settings ??= new StudioSettings();
        data.Users ??= [];
        data.Albums ??= [];
        data.Board ??= new TaskBoard();
        data.Customers ??= [];
        data.Packages ??= [];
        data.Staff ??= [];
        data.Bookings ??= [];
        data.Orders ??= [];
        data.Payments ??= [];
        data.Contracts ??= [];
        data.Expenses ??= [];
        data.Payrolls ??= [];
        data.Attendance ??= [];
        data.ShiftAssignments ??= [];
        data.Outfits ??= [];
        data.Concepts ??= [];
        data.OutfitLoans ??= [];
        data.Notifications ??= [];
        data.NotificationSettings ??= new NotificationSettings();
        data.AuditLogs ??= [];

        // CRM collections are normalized first so user/staff/booking references
        // can be validated against the final IDs.
        NormalizeCrm(data);
        NormalizeSales(data);
        NormalizeOperations(data);
        NormalizeUsers(data, adminCode);
        NormalizeStaffLinks(data);
        NormalizeBoard(data);
        NormalizeAlbums(data);
        NormalizeAuditLogs(data);
    }

    private static void NormalizeAlbums(AppData data)
    {
        var customerIds = data.Customers
            .Select(customer => customer.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var album in data.Albums)
        {
            album.Photos ??= [];
            album.Selections ??= [];
            if (album.Selections.Count > 0)
            {
                var validIds = album.Photos.Select(photo => photo.Id).ToHashSet(StringComparer.Ordinal);
                album.Selections.RemoveAll(selection => !validIds.Contains(selection.PhotoId));
            }

            if (!album.Photos.Any(photo => photo.Id == album.CoverPhotoId) && album.Photos.Count > 0)
            {
                album.CoverPhotoId = album.Photos[0].Id;
            }

            if (!string.IsNullOrWhiteSpace(album.CustomerId) && !customerIds.Contains(album.CustomerId))
            {
                album.CustomerId = "";
            }
        }
    }

    private static void NormalizeCrm(AppData data)
    {
        var now = DateTimeOffset.UtcNow;

        // ---------- customers ----------
        data.Customers.RemoveAll(customer => customer is null);
        var customerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var customer in data.Customers)
        {
            if (string.IsNullOrWhiteSpace(customer.Id) || !customerIds.Add(customer.Id))
            {
                customer.Id = Guid.NewGuid().ToString("N")[..12];
                customerIds.Add(customer.Id);
            }

            customer.Name = AlbumText.TrimTo(customer.Name, 120);
            if (string.IsNullOrWhiteSpace(customer.Name))
            {
                customer.Name = "Khách hàng";
            }

            customer.Type = string.Equals(customer.Type, "company", StringComparison.OrdinalIgnoreCase)
                ? "company"
                : "individual";
            customer.Phone = AlbumText.TrimTo(customer.Phone, 40);
            customer.Email = AlbumText.TrimTo(customer.Email, 160);
            customer.Address = AlbumText.TrimTo(customer.Address, 240);
            customer.Source = AlbumText.TrimTo(customer.Source, 80);
            customer.Notes = AlbumText.TrimTo(customer.Notes, 2000);
            if (customer.CreatedAt == default)
            {
                customer.CreatedAt = now;
            }

            if (customer.UpdatedAt == default)
            {
                customer.UpdatedAt = customer.CreatedAt;
            }
        }

        // ---------- packages ----------
        data.Packages.RemoveAll(package => package is null);
        var packageIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var package in data.Packages)
        {
            if (string.IsNullOrWhiteSpace(package.Id) || !packageIds.Add(package.Id))
            {
                package.Id = Guid.NewGuid().ToString("N")[..12];
                packageIds.Add(package.Id);
            }

            package.Name = AlbumText.TrimTo(package.Name, 120);
            if (string.IsNullOrWhiteSpace(package.Name))
            {
                package.Name = "Gói dịch vụ";
            }

            package.Category = AlbumText.TrimTo(package.Category, 60);
            package.Price = Math.Max(0, package.Price);
            package.Hours = Math.Clamp(package.Hours, 0, 1000);
            package.Includes = AlbumText.TrimTo(package.Includes, 4000);
            package.Notes = AlbumText.TrimTo(package.Notes, 2000);
            if (package.CreatedAt == default)
            {
                package.CreatedAt = now;
            }

            if (package.UpdatedAt == default)
            {
                package.UpdatedAt = package.CreatedAt;
            }
        }

        // ---------- staff ----------
        data.Staff.RemoveAll(member => member is null);
        var staffIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in data.Staff)
        {
            if (string.IsNullOrWhiteSpace(member.Id) || !staffIds.Add(member.Id))
            {
                member.Id = Guid.NewGuid().ToString("N")[..12];
                staffIds.Add(member.Id);
            }

            member.Name = AlbumText.TrimTo(member.Name, 120);
            if (string.IsNullOrWhiteSpace(member.Name))
            {
                member.Name = "Thành viên";
            }

            member.Role = string.IsNullOrWhiteSpace(member.Role)
                ? "photographer"
                : AlbumText.TrimTo(member.Role.Trim().ToLowerInvariant(), 40);
            member.Phone = AlbumText.TrimTo(member.Phone, 40);
            member.Email = AlbumText.TrimTo(member.Email, 160);
            member.UserId = AlbumText.TrimTo(member.UserId, 40);
            member.Notes = AlbumText.TrimTo(member.Notes, 2000);
            member.BaseSalary = Math.Max(0, member.BaseSalary);
            member.CommissionRate = Math.Clamp(member.CommissionRate, 0, 100);
            member.PayMode = PayModes.Normalize(member.PayMode);
            member.HourlyRate = Math.Max(0, member.HourlyRate);
            member.Shift = StaffShifts.Normalize(member.Shift);
            if (member.CreatedAt == default)
            {
                member.CreatedAt = now;
            }

            if (member.UpdatedAt == default)
            {
                member.UpdatedAt = member.CreatedAt;
            }
        }

        // ---------- bookings ----------
        data.Bookings.RemoveAll(booking => booking is null);
        var bookingIds = new HashSet<string>(StringComparer.Ordinal);
        var usedCodes = new HashSet<string>(
            data.Bookings.Select(booking => booking.Code).Where(code => !string.IsNullOrWhiteSpace(code)),
            StringComparer.OrdinalIgnoreCase);
        var albumIds = data.Albums
            .Select(album => album.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var booking in data.Bookings)
        {
            if (string.IsNullOrWhiteSpace(booking.Id) || !bookingIds.Add(booking.Id))
            {
                booking.Id = Guid.NewGuid().ToString("N")[..12];
                bookingIds.Add(booking.Id);
            }

            if (string.IsNullOrWhiteSpace(booking.Code) || !usedCodes.Add(booking.Code))
            {
                booking.Code = NextBookingCode(usedCodes);
            }

            if (!string.IsNullOrWhiteSpace(booking.CustomerId) && !customerIds.Contains(booking.CustomerId))
            {
                booking.CustomerId = "";
            }

            if (!string.IsNullOrWhiteSpace(booking.PackageId) && !packageIds.Contains(booking.PackageId))
            {
                booking.PackageId = "";
            }

            if (!string.IsNullOrWhiteSpace(booking.PhotographerId) && !staffIds.Contains(booking.PhotographerId))
            {
                booking.PhotographerId = "";
            }

            if (!string.IsNullOrWhiteSpace(booking.AlbumId) && !albumIds.Contains(booking.AlbumId))
            {
                booking.AlbumId = "";
            }

            booking.Date = AlbumText.NormalizeEventDate(booking.Date);
            booking.StartTime = NormalizeClock(booking.StartTime);
            booking.Hours = Math.Clamp(booking.Hours, 0, 1000);
            booking.Location = AlbumText.TrimTo(booking.Location, 160);
            booking.Notes = AlbumText.TrimTo(booking.Notes, 2000);
            booking.Price = Math.Max(0, booking.Price);
            booking.Status = BookingStatuses.IsKnown(booking.Status) ? booking.Status : BookingStatuses.Pending;
            if (booking.CreatedAt == default)
            {
                booking.CreatedAt = now;
            }

            if (booking.UpdatedAt == default)
            {
                booking.UpdatedAt = booking.CreatedAt;
            }

            NormalizeBookingHistory(booking);
        }
    }

    private static void NormalizeBookingHistory(Booking booking)
    {
        booking.StatusHistory ??= [];
        booking.StatusHistory.RemoveAll(entry => entry is null);
        foreach (var entry in booking.StatusHistory)
        {
            entry.Status = BookingStatuses.IsKnown(entry.Status) ? entry.Status : BookingStatuses.Pending;
            entry.ActorName = AlbumText.TrimTo(entry.ActorName, 80);
            if (string.IsNullOrWhiteSpace(entry.ActorName))
            {
                entry.ActorName = "Studio";
            }

            if (entry.ChangedAt == default)
            {
                entry.ChangedAt = booking.CreatedAt;
            }
        }

        if (booking.StatusHistory.Count == 0)
        {
            booking.StatusHistory.Add(new BookingStatusEntry
            {
                Status = booking.Status,
                ActorName = "Studio",
                ChangedAt = booking.CreatedAt
            });
        }
        else if (!string.Equals(
                     booking.StatusHistory[^1].Status,
                     booking.Status,
                     StringComparison.Ordinal))
        {
            booking.StatusHistory.Add(new BookingStatusEntry
            {
                Status = booking.Status,
                ActorName = "Studio",
                ChangedAt = booking.UpdatedAt
            });
        }

        booking.StatusHistory = booking.StatusHistory
            .OrderBy(entry => entry.ChangedAt)
            .TakeLast(60)
            .ToList();
    }

    private static void NormalizeSales(AppData data)
    {
        var now = DateTimeOffset.UtcNow;
        var today = now.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var customerIds = data.Customers
            .Select(customer => customer.Id)
            .ToHashSet(StringComparer.Ordinal);
        var bookingIds = data.Bookings
            .Select(booking => booking.Id)
            .ToHashSet(StringComparer.Ordinal);

        // ---------- orders ----------
        data.Orders.RemoveAll(order => order is null);
        var orderIds = new HashSet<string>(StringComparer.Ordinal);
        var orderCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orderSequence = MaxSequence(data.Orders.Select(order => order.Code), "DH-", data.OrderSequence);
        foreach (var order in data.Orders)
        {
            if (string.IsNullOrWhiteSpace(order.Id) || !orderIds.Add(order.Id))
            {
                order.Id = Guid.NewGuid().ToString("N")[..12];
                orderIds.Add(order.Id);
            }

            if (string.IsNullOrWhiteSpace(order.Code) || !orderCodes.Add(order.Code))
            {
                order.Code = NextCode("DH-", orderCodes, ref orderSequence);
            }

            if (!string.IsNullOrWhiteSpace(order.CustomerId) && !customerIds.Contains(order.CustomerId))
            {
                order.CustomerId = "";
            }

            if (!string.IsNullOrWhiteSpace(order.BookingId) && !bookingIds.Contains(order.BookingId))
            {
                order.BookingId = "";
            }

            var items = new List<OrderItem>();
            foreach (var item in order.Items ?? [])
            {
                if (item is null)
                {
                    continue;
                }

                var name = AlbumText.TrimTo(item.Name, 160);
                var quantity = Math.Clamp(item.Quantity, 0, 10000);
                if (name.Length == 0 || quantity <= 0)
                {
                    continue;
                }

                items.Add(new OrderItem
                {
                    Name = name,
                    Quantity = quantity,
                    UnitPrice = Math.Max(0, item.UnitPrice)
                });
            }

            order.Items = items;
            order.Discount = Math.Max(0, order.Discount);
            order.Status = OrderStatuses.IsKnown(order.Status) ? order.Status : OrderStatuses.Confirmed;
            order.Notes = AlbumText.TrimTo(order.Notes, 2000);
            if (order.CreatedAt == default)
            {
                order.CreatedAt = now;
            }

            if (order.UpdatedAt == default)
            {
                order.UpdatedAt = order.CreatedAt;
            }
        }

        data.OrderSequence = orderSequence;

        // ---------- payments ----------
        data.Payments.RemoveAll(payment => payment is null);
        var orderById = data.Orders
            .GroupBy(order => order.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var paymentIds = new HashSet<string>(StringComparer.Ordinal);
        var paymentCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paymentSequence = MaxSequence(
            data.Payments.Select(payment => payment.Code),
            "PT-",
            data.PaymentSequence);
        foreach (var payment in data.Payments.ToList())
        {
            if (payment.Amount <= 0)
            {
                data.Payments.Remove(payment);
                continue;
            }

            if (string.IsNullOrWhiteSpace(payment.Id) || !paymentIds.Add(payment.Id))
            {
                payment.Id = Guid.NewGuid().ToString("N")[..12];
                paymentIds.Add(payment.Id);
            }

            if (string.IsNullOrWhiteSpace(payment.Code) || !paymentCodes.Add(payment.Code))
            {
                payment.Code = NextCode("PT-", paymentCodes, ref paymentSequence);
            }

            if (!string.IsNullOrWhiteSpace(payment.OrderId) && !orderIds.Contains(payment.OrderId))
            {
                payment.OrderId = "";
            }

            if (payment.OrderId.Length > 0 && orderById.TryGetValue(payment.OrderId, out var order))
            {
                payment.CustomerId = order.CustomerId;
            }
            else if (!string.IsNullOrWhiteSpace(payment.CustomerId) && !customerIds.Contains(payment.CustomerId))
            {
                payment.CustomerId = "";
            }

            payment.Amount = Math.Max(0, payment.Amount);
            payment.Method = PaymentMethods.IsKnown(payment.Method) ? payment.Method : PaymentMethods.Other;
            payment.PaidAt = AlbumText.NormalizeEventDate(payment.PaidAt);
            if (payment.PaidAt.Length == 0)
            {
                payment.PaidAt = payment.OrderId.Length > 0 &&
                                 orderById.TryGetValue(payment.OrderId, out var linkedOrder) &&
                                 linkedOrder.CreatedAt != default
                    ? linkedOrder.CreatedAt.ToOffset(TimeSpan.FromHours(7))
                        .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : today;
            }

            payment.Note = AlbumText.TrimTo(payment.Note, 500);
            payment.ActorId = AlbumText.TrimTo(payment.ActorId, 40);
            payment.ActorName = AlbumText.TrimTo(payment.ActorName, 80);
            if (string.IsNullOrWhiteSpace(payment.ActorName))
            {
                payment.ActorName = "Studio";
            }

            if (payment.CreatedAt == default)
            {
                payment.CreatedAt = now;
            }
        }

        data.PaymentSequence = paymentSequence;

        // ---------- contracts ----------
        data.Contracts.RemoveAll(contract => contract is null);
        var contractIds = new HashSet<string>(StringComparer.Ordinal);
        var contractCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var contractSequence = MaxSequence(
            data.Contracts.Select(contract => contract.Code),
            "HD-",
            data.ContractSequence);
        foreach (var contract in data.Contracts)
        {
            if (string.IsNullOrWhiteSpace(contract.Id) || !contractIds.Add(contract.Id))
            {
                contract.Id = Guid.NewGuid().ToString("N")[..12];
                contractIds.Add(contract.Id);
            }

            if (string.IsNullOrWhiteSpace(contract.Code) || !contractCodes.Add(contract.Code))
            {
                contract.Code = NextCode("HD-", contractCodes, ref contractSequence);
            }

            if (!string.IsNullOrWhiteSpace(contract.CustomerId) && !customerIds.Contains(contract.CustomerId))
            {
                contract.CustomerId = "";
            }

            if (!string.IsNullOrWhiteSpace(contract.BookingId) && !bookingIds.Contains(contract.BookingId))
            {
                contract.BookingId = "";
            }

            contract.Service = AlbumText.TrimTo(contract.Service, 160);
            contract.Total = Math.Max(0, contract.Total);
            contract.Terms = AlbumText.TrimTo(contract.Terms, 8000);
            contract.Status = ContractStatuses.IsKnown(contract.Status)
                ? contract.Status
                : ContractStatuses.Pending;
            if (contract.CreatedAt == default)
            {
                contract.CreatedAt = now;
            }

            if (contract.UpdatedAt == default)
            {
                contract.UpdatedAt = contract.CreatedAt;
            }

            var schedule = new List<ContractScheduleItem>();
            foreach (var item in contract.Schedule ?? [])
            {
                if (item is null)
                {
                    continue;
                }

                var paid = item.Paid;
                var paidAt = AlbumText.NormalizeEventDate(item.PaidAt);
                if (paid && paidAt.Length == 0)
                {
                    var dueDate = AlbumText.NormalizeEventDate(item.DueDate);
                    paidAt = dueDate.Length > 0
                        ? dueDate
                        : contract.CreatedAt.ToOffset(TimeSpan.FromHours(7))
                            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }

                schedule.Add(new ContractScheduleItem
                {
                    Label = AlbumText.TrimTo(item.Label, 80) is { Length: > 0 } label ? label : "Thanh toán",
                    Amount = Math.Max(0, item.Amount),
                    DueDate = AlbumText.NormalizeEventDate(item.DueDate),
                    Paid = paid,
                    PaidAt = paid ? paidAt : ""
                });
            }

            contract.Schedule = schedule;
            if (contract.Schedule.Count == 0 && contract.Total > 0)
            {
                contract.Schedule.Add(new ContractScheduleItem
                {
                    Label = "Thanh toán toàn bộ (100%)",
                    Amount = contract.Total,
                    DueDate = contract.CreatedAt.ToOffset(TimeSpan.FromHours(7))
                        .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                });
            }

            if (contract.Schedule.Count > 0)
            {
                var scheduledTotal = contract.Schedule.Sum(item => item.Amount);
                if (contract.Total <= 0)
                {
                    contract.Total = scheduledTotal;
                }
                else if (scheduledTotal != contract.Total)
                {
                    var last = contract.Schedule[^1];
                    last.Amount = Math.Max(0, last.Amount + contract.Total - scheduledTotal);
                }
            }
        }

        data.ContractSequence = contractSequence;
    }

    private static void NormalizeOperations(AppData data)
    {
        var now = DateTimeOffset.UtcNow;
        var today = now.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var staffIds = data.Staff.Select(member => member.Id).ToHashSet(StringComparer.Ordinal);
        var customerIds = data.Customers.Select(customer => customer.Id).ToHashSet(StringComparer.Ordinal);
        var bookingIds = data.Bookings.Select(booking => booking.Id).ToHashSet(StringComparer.Ordinal);

        // ---------- expenses ----------
        data.Expenses.RemoveAll(expense => expense is null);
        var expenseIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var expense in data.Expenses)
        {
            if (string.IsNullOrWhiteSpace(expense.Id) || !expenseIds.Add(expense.Id))
            {
                expense.Id = Guid.NewGuid().ToString("N")[..12];
                expenseIds.Add(expense.Id);
            }

            expense.Date = AlbumText.NormalizeEventDate(expense.Date);
            if (expense.Date.Length == 0)
            {
                expense.Date = today;
            }

            expense.Category = FinanceEntryKinds.Normalize(expense.Kind) == FinanceEntryKinds.Income
                ? IncomeCategories.Normalize(expense.Category)
                : ExpenseCategories.Normalize(expense.Category);
            expense.Kind = FinanceEntryKinds.Normalize(expense.Kind);
            expense.Amount = Math.Max(0, expense.Amount);
            expense.Note = AlbumText.TrimTo(expense.Note, 500);
            expense.Photos ??= [];
            expense.Photos.RemoveAll(photo => photo is null);
            var photoIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var photo in expense.Photos)
            {
                if (string.IsNullOrWhiteSpace(photo.Id) || !photoIds.Add(photo.Id))
                {
                    photo.Id = Guid.NewGuid().ToString("N")[..12];
                    photoIds.Add(photo.Id);
                }

                photo.FileName = Path.GetFileName(photo.FileName ?? "");
                photo.OriginalName = AlbumText.TrimTo(photo.OriginalName, 180);
                photo.ContentType = AlbumText.TrimTo(photo.ContentType, 80);
                photo.Caption = AlbumText.TrimTo(photo.Caption, 120);
                photo.Size = Math.Max(0, photo.Size);
                if (photo.CreatedAt == default)
                {
                    photo.CreatedAt = expense.CreatedAt;
                }
            }
            expense.CreatedById = AlbumText.TrimTo(expense.CreatedById, 40);
            expense.CreatedByName = AlbumText.TrimTo(expense.CreatedByName, 80);
            if (expense.CreatedAt == default)
            {
                expense.CreatedAt = now;
            }

            if (expense.UpdatedAt == default)
            {
                expense.UpdatedAt = expense.CreatedAt;
            }
        }

        // ---------- payrolls ----------
        data.Payrolls.RemoveAll(payroll => payroll is null);
        var payrollIds = new HashSet<string>(StringComparer.Ordinal);
        var payrollKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var payroll in data.Payrolls.ToList())
        {
            if (!staffIds.Contains(payroll.StaffId))
            {
                data.Payrolls.Remove(payroll);
                continue;
            }

            if (string.IsNullOrWhiteSpace(payroll.Id) || !payrollIds.Add(payroll.Id))
            {
                payroll.Id = Guid.NewGuid().ToString("N")[..12];
                payrollIds.Add(payroll.Id);
            }

            payroll.Month = NormalizeMonth(payroll.Month);
            if (payroll.Month.Length == 0)
            {
                payroll.Month = today[..7];
            }

            var key = payroll.StaffId + "|" + payroll.Month;
            if (!payrollKeys.Add(key))
            {
                data.Payrolls.Remove(payroll);
                continue;
            }

            payroll.Base = Math.Max(0, payroll.Base);
            payroll.Hours = Math.Max(0, Math.Round(payroll.Hours, 2));
            payroll.CommissionBase = Math.Max(0, payroll.CommissionBase);
            payroll.Commission = Math.Max(0, payroll.Commission);
            payroll.Total = Math.Max(0, payroll.Total);
            payroll.PaidAt = AlbumText.NormalizeEventDate(payroll.PaidAt);
            if (!payroll.Paid)
            {
                payroll.PaidAt = "";
            }
            else if (payroll.PaidAt.Length == 0)
            {
                payroll.PaidAt = today;
            }

            if (payroll.ComputedAt == default)
            {
                payroll.ComputedAt = now;
            }

            if (payroll.UpdatedAt == default)
            {
                payroll.UpdatedAt = payroll.ComputedAt;
            }
        }

        // ---------- attendance ----------
        data.Attendance.RemoveAll(record => record is null);
        var attendanceIds = new HashSet<string>(StringComparer.Ordinal);
        var attendanceKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in data.Attendance.ToList())
        {
            if (!staffIds.Contains(record.StaffId))
            {
                data.Attendance.Remove(record);
                continue;
            }

            if (string.IsNullOrWhiteSpace(record.Id) || !attendanceIds.Add(record.Id))
            {
                record.Id = Guid.NewGuid().ToString("N")[..12];
                attendanceIds.Add(record.Id);
            }

            record.Date = AlbumText.NormalizeEventDate(record.Date);
            if (record.Date.Length == 0)
            {
                data.Attendance.Remove(record);
                continue;
            }

            var key = record.StaffId + "|" + record.Date;
            if (!attendanceKeys.Add(key))
            {
                data.Attendance.Remove(record);
                continue;
            }

            record.In = NormalizeClock(record.In);
            record.Out = NormalizeClock(record.Out);
            if (record.In.Length == 0 && record.Out.Length > 0)
            {
                record.In = record.Out;
                record.Out = "";
            }

            record.Note = AlbumText.TrimTo(record.Note, 500);
            if (record.CreatedAt == default)
            {
                record.CreatedAt = now;
            }

            if (record.UpdatedAt == default)
            {
                record.UpdatedAt = record.CreatedAt;
            }
        }

        // ---------- shift assignments ----------
        data.ShiftAssignments.RemoveAll(assignment => assignment is null);
        var shiftAssignmentIds = new HashSet<string>(StringComparer.Ordinal);
        var shiftAssignmentKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assignment in data.ShiftAssignments.ToList())
        {
            if (!staffIds.Contains(assignment.StaffId))
            {
                data.ShiftAssignments.Remove(assignment);
                continue;
            }

            if (string.IsNullOrWhiteSpace(assignment.Id) || !shiftAssignmentIds.Add(assignment.Id))
            {
                assignment.Id = Guid.NewGuid().ToString("N")[..12];
                shiftAssignmentIds.Add(assignment.Id);
            }

            assignment.Date = AlbumText.NormalizeEventDate(assignment.Date);
            if (assignment.Date.Length == 0)
            {
                data.ShiftAssignments.Remove(assignment);
                continue;
            }

            var shiftKey = assignment.StaffId + "|" + assignment.Date;
            if (!shiftAssignmentKeys.Add(shiftKey))
            {
                data.ShiftAssignments.Remove(assignment);
                continue;
            }

            assignment.Shift = ShiftAssignmentKinds.Normalize(assignment.Shift);
            assignment.Note = AlbumText.TrimTo(assignment.Note, 300);
            if (assignment.CreatedAt == default)
            {
                assignment.CreatedAt = now;
            }

            if (assignment.UpdatedAt == default)
            {
                assignment.UpdatedAt = assignment.CreatedAt;
            }
        }

        // ---------- outfits ----------
        data.Outfits.RemoveAll(outfit => outfit is null);
        var outfitIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var outfit in data.Outfits)
        {
            if (string.IsNullOrWhiteSpace(outfit.Id) || !outfitIds.Add(outfit.Id))
            {
                outfit.Id = Guid.NewGuid().ToString("N")[..12];
                outfitIds.Add(outfit.Id);
            }

            outfit.Name = AlbumText.TrimTo(outfit.Name, 120);
            if (string.IsNullOrWhiteSpace(outfit.Name))
            {
                outfit.Name = "Trang phục";
            }

            outfit.Category = OutfitCategories.Normalize(outfit.Category);
            outfit.Size = AlbumText.TrimTo(outfit.Size, 40);
            outfit.Color = AlbumText.TrimTo(outfit.Color, 60);
            outfit.Quantity = Math.Clamp(outfit.Quantity, 0, 10000);
            outfit.Note = AlbumText.TrimTo(outfit.Note, 1000);
            if (outfit.CreatedAt == default)
            {
                outfit.CreatedAt = now;
            }

            if (outfit.UpdatedAt == default)
            {
                outfit.UpdatedAt = outfit.CreatedAt;
            }
        }

        // ---------- concepts ----------
        data.Concepts.RemoveAll(concept => concept is null);
        var conceptIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var concept in data.Concepts)
        {
            if (string.IsNullOrWhiteSpace(concept.Id) || !conceptIds.Add(concept.Id))
            {
                concept.Id = Guid.NewGuid().ToString("N")[..12];
                conceptIds.Add(concept.Id);
            }

            concept.Name = AlbumText.TrimTo(concept.Name, 120);
            if (string.IsNullOrWhiteSpace(concept.Name))
            {
                concept.Name = "Concept";
            }

            concept.Description = AlbumText.TrimTo(concept.Description, 2000);
            concept.Fee = Math.Max(0, concept.Fee);
            concept.Status = ConceptStatuses.Normalize(concept.Status);
            if (concept.CreatedAt == default)
            {
                concept.CreatedAt = now;
            }

            if (concept.UpdatedAt == default)
            {
                concept.UpdatedAt = concept.CreatedAt;
            }
        }

        // ---------- outfit loans ----------
        data.OutfitLoans.RemoveAll(loan => loan is null);
        var loanIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var loan in data.OutfitLoans.ToList())
        {
            if (!outfitIds.Contains(loan.OutfitId))
            {
                data.OutfitLoans.Remove(loan);
                continue;
            }

            if (string.IsNullOrWhiteSpace(loan.Id) || !loanIds.Add(loan.Id))
            {
                loan.Id = Guid.NewGuid().ToString("N")[..12];
                loanIds.Add(loan.Id);
            }

            loan.Quantity = Math.Clamp(loan.Quantity, 1, 10000);
            loan.BorrowerType = LoanBorrowerTypes.Normalize(loan.BorrowerType);
            if (!customerIds.Contains(loan.CustomerId))
            {
                loan.CustomerId = "";
            }

            if (!staffIds.Contains(loan.StaffId))
            {
                loan.StaffId = "";
            }

            if (loan.BorrowerType == LoanBorrowerTypes.Staff && loan.StaffId.Length == 0)
            {
                loan.BorrowerType = LoanBorrowerTypes.Customer;
            }

            if (!bookingIds.Contains(loan.BookingId))
            {
                loan.BookingId = "";
            }

            loan.BorrowDate = AlbumText.NormalizeEventDate(loan.BorrowDate);
            if (loan.BorrowDate.Length == 0)
            {
                loan.BorrowDate = today;
            }

            loan.DueDate = AlbumText.NormalizeEventDate(loan.DueDate);
            loan.ReturnDate = AlbumText.NormalizeEventDate(loan.ReturnDate);
            loan.Deposit = Math.Max(0, loan.Deposit);
            loan.Notes = AlbumText.TrimTo(loan.Notes, 1000);
            if (loan.CreatedAt == default)
            {
                loan.CreatedAt = now;
            }

            if (loan.UpdatedAt == default)
            {
                loan.UpdatedAt = loan.CreatedAt;
            }
        }

        // ---------- notifications ----------
        data.Notifications.RemoveAll(notification => notification is null);
        var notificationIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var notification in data.Notifications)
        {
            if (string.IsNullOrWhiteSpace(notification.Id) || !notificationIds.Add(notification.Id))
            {
                notification.Id = Guid.NewGuid().ToString("N")[..12];
                notificationIds.Add(notification.Id);
            }

            notification.Type = NotificationTypes.Normalize(notification.Type);
            notification.Title = AlbumText.TrimTo(notification.Title, 160);
            notification.Body = AlbumText.TrimTo(notification.Body, 600);
            notification.Link = AlbumText.TrimTo(notification.Link, 200);
            if (notification.CreatedAt == default)
            {
                notification.CreatedAt = now;
            }
        }

        data.Notifications = data.Notifications
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(500)
            .ToList();

        data.NotificationSettings ??= new NotificationSettings();
    }

    private static string NormalizeMonth(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var trimmed = value.Trim();
        if (DateTime.TryParseExact(
                trimmed + "-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var month))
        {
            return month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }

        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }

        return "";
    }

    private static int MaxSequence(IEnumerable<string?> codes, string prefix, int configured)
    {
        var next = Math.Max(1, configured);
        foreach (var code in codes)
        {
            if (code is null ||
                !code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(code.AsSpan(prefix.Length), out var number))
            {
                continue;
            }

            if (number >= next)
            {
                next = number + 1;
            }
        }

        return next;
    }

    private static string NextCode(string prefix, HashSet<string> usedCodes, ref int sequence)
    {
        string candidate;
        do
        {
            candidate = $"{prefix}{sequence:0000}";
            sequence++;
        }
        while (!usedCodes.Add(candidate));

        return candidate;
    }

    private static void NormalizeStaffLinks(AppData data)
    {
        var staffById = data.Staff
            .GroupBy(member => member.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var userIds = data.Users
            .Select(user => user.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var user in data.Users)
        {
            if (string.IsNullOrWhiteSpace(user.StaffId))
            {
                continue;
            }

            if (!staffById.TryGetValue(user.StaffId, out var staff))
            {
                user.StaffId = "";
                continue;
            }

            if (string.IsNullOrWhiteSpace(staff.UserId))
            {
                staff.UserId = user.Id;
            }
            else if (!string.Equals(staff.UserId, user.Id, StringComparison.Ordinal))
            {
                user.StaffId = "";
            }
        }

        foreach (var staff in data.Staff)
        {
            if (string.IsNullOrWhiteSpace(staff.UserId))
            {
                continue;
            }

            if (!userIds.Contains(staff.UserId))
            {
                staff.UserId = "";
                continue;
            }

            var user = data.Users.First(candidate =>
                string.Equals(candidate.Id, staff.UserId, StringComparison.Ordinal));
            if (string.IsNullOrWhiteSpace(user.StaffId))
            {
                user.StaffId = staff.Id;
            }
            else if (!string.Equals(user.StaffId, staff.Id, StringComparison.Ordinal))
            {
                staff.UserId = "";
            }
        }
    }

    private static void NormalizeAuditLogs(AppData data)
    {
        data.AuditLogs.RemoveAll(entry => entry is null);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in data.AuditLogs)
        {
            if (string.IsNullOrWhiteSpace(entry.Id) || !ids.Add(entry.Id))
            {
                entry.Id = Guid.NewGuid().ToString("N")[..12];
                ids.Add(entry.Id);
            }

            entry.Module = AlbumText.TrimTo(entry.Module, 40);
            entry.Action = AlbumText.TrimTo(entry.Action, 40);
            entry.Summary = AlbumText.TrimTo(entry.Summary, 240);
            entry.ActorId = AlbumText.TrimTo(entry.ActorId, 40);
            entry.ActorName = AlbumText.TrimTo(entry.ActorName, 80);
            entry.ActorRole = string.Equals(entry.ActorRole, "admin", StringComparison.OrdinalIgnoreCase)
                ? "admin"
                : "employee";
            entry.EntityId = AlbumText.TrimTo(entry.EntityId, 80);
            entry.Details ??= [];
            entry.Details = entry.Details
                .Where(detail => !string.IsNullOrWhiteSpace(detail))
                .Select(detail => AlbumText.TrimTo(detail, 240))
                .Take(12)
                .ToList();
            if (entry.CreatedAt == default)
            {
                entry.CreatedAt = now;
            }
        }

        data.AuditLogs = data.AuditLogs
            .OrderByDescending(entry => entry.CreatedAt)
            .Take(StudioAudit.MaxEntries)
            .ToList();
    }

    private static string NextBookingCode(HashSet<string> usedCodes)
    {
        var number = 1;
        foreach (var code in usedCodes)
        {
            if (code.StartsWith("BK-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(code.AsSpan(3), out var existing) &&
                existing >= number)
            {
                number = existing + 1;
            }
        }

        string candidate;
        do
        {
            candidate = $"BK-{number:0000}";
            number++;
        }
        while (!usedCodes.Add(candidate));

        return candidate;
    }

    private static string NormalizeClock(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "09:00";
        }

        return TimeOnly.TryParse(value.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var time)
            ? time.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)
            : "09:00";
    }

    private static void NormalizeBoard(AppData data)
    {
        var board = data.Board;
        board.Labels ??= [];
        board.Lists ??= [];
        if (string.IsNullOrWhiteSpace(board.Id))
        {
            board.Id = "studio-board";
        }

        if (string.IsNullOrWhiteSpace(board.Title))
        {
            board.Title = "Bảng công việc";
        }

        if (string.IsNullOrWhiteSpace(board.Theme))
        {
            board.Theme = "ink";
        }

        board.CardBackground = NormalizeBoardColor(board.CardBackground, "#fbf8f2");
        board.CardTextColor = NormalizeBoardColor(board.CardTextColor, "#2b251e");

        var labelsById = new HashSet<string>(StringComparer.Ordinal);
        board.Labels.RemoveAll(label => label is null || string.IsNullOrWhiteSpace(label.Id));
        foreach (var label in board.Labels)
        {
            if (!labelsById.Add(label.Id))
            {
                label.Id = Guid.NewGuid().ToString("N")[..10];
                labelsById.Add(label.Id);
            }

            label.Name = AlbumText.TrimTo(label.Name, 40);
            if (string.IsNullOrWhiteSpace(label.Color))
            {
                label.Color = "#b25f46";
            }
        }

        var albumIds = new HashSet<string>(
            data.Albums.Select(album => album.Id).Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.Ordinal);
        var staffIds = new HashSet<string>(
            data.Staff.Select(member => member.Id).Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.Ordinal);
        var bookingIds = new HashSet<string>(
            data.Bookings.Select(booking => booking.Id).Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.Ordinal);
        var orderedLists = board.Lists
            .Where(item => item is not null)
            .OrderBy(item => item.Position)
            .ThenBy(item => item.CreatedAt)
            .ToList();
        board.Lists = orderedLists;

        var listIds = new HashSet<string>(StringComparer.Ordinal);
        for (var listIndex = 0; listIndex < board.Lists.Count; listIndex++)
        {
            var list = board.Lists[listIndex];
            if (string.IsNullOrWhiteSpace(list.Id) || !listIds.Add(list.Id))
            {
                list.Id = Guid.NewGuid().ToString("N")[..10];
                listIds.Add(list.Id);
            }

            list.Title = AlbumText.TrimTo(list.Title, 60);
            if (string.IsNullOrWhiteSpace(list.Title))
            {
                list.Title = "Danh sách";
            }

            list.Position = listIndex;
            list.Cards ??= [];
            list.Cards = list.Cards
                .Where(card => card is not null)
                .OrderBy(card => card.Position)
                .ThenBy(card => card.CreatedAt)
                .ToList();

            var cardIds = new HashSet<string>(StringComparer.Ordinal);
            for (var cardIndex = 0; cardIndex < list.Cards.Count; cardIndex++)
            {
                var card = list.Cards[cardIndex];
                if (string.IsNullOrWhiteSpace(card.Id) || !cardIds.Add(card.Id))
                {
                    card.Id = Guid.NewGuid().ToString("N")[..10];
                    cardIds.Add(card.Id);
                }

                card.Title = AlbumText.TrimTo(card.Title, 160);
                if (string.IsNullOrWhiteSpace(card.Title))
                {
                    card.Title = "Thẻ chưa đặt tên";
                }

                card.Position = cardIndex;
                card.LabelIds ??= [];
                card.LabelIds = card.LabelIds
                    .Where(labelsById.Contains)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                card.Checklist ??= [];
                card.Checklist = card.Checklist
                    .Where(item => item is not null)
                    .ToList();
                NormalizeActivities(card);
                if (!string.IsNullOrWhiteSpace(card.AlbumId) && !albumIds.Contains(card.AlbumId))
                {
                    card.AlbumId = "";
                }

                if (!string.IsNullOrWhiteSpace(card.AssigneeId) && !staffIds.Contains(card.AssigneeId))
                {
                    card.AssigneeId = "";
                }

                card.Priority = NormalizePriority(card.Priority);
                if (!string.IsNullOrWhiteSpace(card.BookingId) && !bookingIds.Contains(card.BookingId))
                {
                    card.BookingId = "";
                }
            }
        }
    }

    private static void NormalizeUsers(AppData data, string adminCode)
    {
        data.Users.RemoveAll(user => user is null);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var usernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;

        foreach (var user in data.Users)
        {
            if (string.IsNullOrWhiteSpace(user.Id) || !ids.Add(user.Id))
            {
                user.Id = Guid.NewGuid().ToString("N")[..12];
                ids.Add(user.Id);
            }

            var username = NormalizeUsername(user.Username);
            if (string.IsNullOrWhiteSpace(username))
            {
                username = "nhanvien-" + user.Id[..Math.Min(4, user.Id.Length)];
            }

            var baseUsername = username;
            var counter = 2;
            while (!usernames.Add(username))
            {
                username = $"{baseUsername}{counter:00}";
                counter++;
            }

            user.Username = username;
            user.DisplayName = AlbumText.TrimTo(user.DisplayName, 60);
            if (string.IsNullOrWhiteSpace(user.DisplayName))
            {
                user.DisplayName = user.Username;
            }

            user.Role = string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase)
                ? "admin"
                : "employee";
            user.Permissions ??= [];
            user.Permissions = StudioPermissions.Normalize(
                user.Permissions,
                user.Role == "admin",
                user.PermissionsConfigured);
            user.PermissionsConfigured = true;
            user.StaffId = AlbumText.TrimTo(user.StaffId, 40);
            if (user.CreatedAt == default)
            {
                user.CreatedAt = now;
            }

            if (user.UpdatedAt == default)
            {
                user.UpdatedAt = user.CreatedAt;
            }
        }

        if (data.Users.Count == 0)
        {
            data.Users.Add(CreateBootstrapUser(adminCode));
            return;
        }

        // Never leave the studio without a way back in. Reactivate an existing
        // administrator rather than replacing accounts or passwords.
        if (!data.Users.Any(user => user.IsActive && user.Role == "admin"))
        {
            var admin = data.Users.FirstOrDefault(user => user.Role == "admin") ?? data.Users[0];
            admin.Role = "admin";
            admin.IsActive = true;
            admin.UpdatedAt = now;
        }
    }

    private static void NormalizeActivities(TaskCard card)
    {
        card.Activities ??= [];
        card.Activities.RemoveAll(activity =>
            activity is null || string.IsNullOrWhiteSpace(activity.Summary));
        foreach (var activity in card.Activities)
        {
            if (string.IsNullOrWhiteSpace(activity.Id))
            {
                activity.Id = Guid.NewGuid().ToString("N")[..10];
            }

            activity.ActorName = AlbumText.TrimTo(activity.ActorName, 80);
            if (string.IsNullOrWhiteSpace(activity.ActorName))
            {
                activity.ActorName = "Studio";
            }

            activity.ActorRole = string.Equals(activity.ActorRole, "admin", StringComparison.OrdinalIgnoreCase)
                ? "admin"
                : "employee";
            activity.Summary = AlbumText.TrimTo(activity.Summary, 240);
            activity.Details ??= [];
            activity.Details = activity.Details
                .Where(detail => !string.IsNullOrWhiteSpace(detail))
                .Select(detail => AlbumText.TrimTo(detail, 240))
                .Take(12)
                .ToList();
            if (activity.CreatedAt == default)
            {
                activity.CreatedAt = card.UpdatedAt;
            }
        }

        card.Activities = card.Activities
            .OrderByDescending(activity => activity.CreatedAt)
            .Take(60)
            .ToList();
    }

    private static string NormalizeUsername(string? value)
    {
        var source = (value ?? "").Trim().ToLowerInvariant();
        var builder = new StringBuilder(source.Length);
        foreach (var character in source)
        {
            if (char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static StudioUser CreateBootstrapUser(string adminCode)
    {
        var now = DateTimeOffset.UtcNow;
        return new StudioUser
        {
            Id = "user-admin",
            Username = "admin",
            DisplayName = "Quản trị studio",
            Role = "admin",
            PasswordHash = PasswordHasher.Hash(
                string.IsNullOrWhiteSpace(adminCode) ? "studio2026" : adminCode),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static string NormalizePriority(string? value)
    {
        var priority = (value ?? "").Trim().ToLowerInvariant();
        return priority is "low" or "normal" or "high" or "urgent" ? priority : "normal";
    }

    private static string NormalizeBoardColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var color = value.Trim();
        return color.Length == 7 &&
               color[0] == '#' &&
               color.Skip(1).All(Uri.IsHexDigit)
            ? color
            : fallback;
    }
}
