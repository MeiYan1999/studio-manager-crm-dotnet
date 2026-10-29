using StudioCore.Models;
using StudioCore.Services;

namespace StudioCore.Api;

internal static class ApiMappings
{
    public static AlbumSummaryDto ToSummary(Album album)
    {
        var cover = FindCover(album);
        var selected = album.SelectedPhotoIds;
        return new AlbumSummaryDto(
            album.Id,
            album.ShareToken,
            album.Title,
            album.ClientName,
            album.CustomerId,
            album.EventDate,
            album.Location,
            cover?.ThumbUrl,
            album.Photos.Count,
            selected.Count,
            album.IsPublished,
            album.IsDemo,
            album.CreatedAt,
            album.SubmittedAt);
    }

    public static AlbumAdminDto ToAdminAlbum(Album album)
    {
        var cover = FindCover(album);
        var selected = album.Selections
            .GroupBy(selection => selection.PhotoId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().SelectedAt, StringComparer.Ordinal);

        return new AlbumAdminDto(
            album.Id,
            album.ShareToken,
            album.Title,
            album.ClientName,
            album.CustomerId,
            album.EventDate,
            album.Location,
            album.Description,
            album.DriveFolderUrl,
            album.DriveFolderId,
            album.IncludeSubfolders,
            album.AllowFavorites,
            album.AllowDownloads,
            album.IsPublished,
            album.IsDemo,
            album.CoverPhotoId,
            cover?.PreviewUrl,
            album.CreatedAt,
            album.UpdatedAt,
            album.FavoritesUpdatedAt,
            album.SubmittedAt,
            album.ClientNote,
            album.Photos.Count,
            selected.Count,
            album.Photos.Select(photo => ToAdminPhoto(photo, selected)).ToList());
    }

    public static PublicAlbumDto ToPublicAlbum(Album album, StudioSettings settings)
    {
        var cover = FindCover(album);
        var selected = album.SelectedPhotoIds;
        var selectedAt = album.Selections
            .GroupBy(selection => selection.PhotoId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().SelectedAt, StringComparer.Ordinal);

        return new PublicAlbumDto(
            album.ShareToken,
            album.Title,
            album.ClientName,
            album.EventDate,
            album.Location,
            album.Description,
            settings.StudioName,
            settings.ContactEmail,
            cover?.PreviewUrl,
            album.AllowFavorites,
            album.AllowDownloads,
            album.SubmittedAt,
            album.ClientNote,
            album.Photos.Select(photo => ToPublicPhoto(photo, album, selectedAt)).ToList(),
            selected.OrderBy(id => id, StringComparer.Ordinal).ToList());
    }

    public static DrivePreviewDto ToDrivePreview(DriveFolderResult folder)
    {
        var codes = AlbumText.CreateUniqueCodes(folder.Photos.Select(photo => photo.FileName));
        var previewItems = folder.Photos
            .Take(18)
            .Select((photo, index) => new DrivePreviewPhotoDto(
                photo.FileId,
                codes[index],
                photo.FileName,
                DriveThumbUrl(photo.FileId, 720),
                photo.Width,
                photo.Height))
            .ToList();

        return new DrivePreviewDto(
            folder.FolderId,
            folder.FolderName,
            folder.Photos.Count,
            previewItems,
            folder.Truncated);
    }

    public static Photo ToPhoto(DrivePhoto source, string code)
    {
        return new Photo
        {
            Id = Guid.NewGuid().ToString("N"),
            DriveFileId = source.FileId,
            Code = code,
            FileName = source.FileName,
            FolderPath = source.FolderPath,
            ThumbUrl = DriveThumbUrl(source.FileId, 720),
            PreviewUrl = DriveThumbUrl(source.FileId, 2400),
            DownloadUrl = $"https://drive.google.com/uc?export=download&id={Uri.EscapeDataString(source.FileId)}",
            Width = source.Width,
            Height = source.Height,
            SizeBytes = source.SizeBytes,
            TakenAt = source.CreatedAt
        };
    }

    public static void UpdatePhotoFromDrive(Photo target, DrivePhoto source)
    {
        target.FileName = source.FileName;
        target.FolderPath = source.FolderPath;
        target.ThumbUrl = DriveThumbUrl(source.FileId, 720);
        target.PreviewUrl = DriveThumbUrl(source.FileId, 2400);
        target.DownloadUrl = $"https://drive.google.com/uc?export=download&id={Uri.EscapeDataString(source.FileId)}";
        target.Width = source.Width;
        target.Height = source.Height;
        target.SizeBytes = source.SizeBytes;
        target.TakenAt = source.CreatedAt;
    }

    public static AdminSettingsDto ToSettings(StudioSettings settings, string effectiveApiKey)
    {
        var hint = string.IsNullOrWhiteSpace(effectiveApiKey)
            ? null
            : (effectiveApiKey.Length <= 4 ? effectiveApiKey : "••••" + effectiveApiKey[^4..]);
        return new AdminSettingsDto(
            settings.StudioName,
            settings.ContactEmail,
            !string.IsNullOrWhiteSpace(effectiveApiKey),
            hint,
            settings.Website,
            settings.LogoUrl,
            settings.LoginImageUrl);
    }

    public static SessionUserDto ToSessionUser(StudioUser user) =>
        new(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Role,
            user.Permissions ?? [],
            user.MustChangePassword);

    public static StudioUserDto ToStudioUser(StudioUser user, string currentUserId) =>
        new(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Role,
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt,
            string.Equals(user.Id, currentUserId, StringComparison.Ordinal),
            user.Permissions ?? [],
            user.MustChangePassword,
            user.StaffId ?? "");

    public static TaskBoardDto ToBoard(AppData data) =>
        ToBoard(data.Board, data.Albums, data.Staff, data.Bookings);

    public static TaskBoardDto ToBoard(
        TaskBoard board,
        IReadOnlyCollection<Album> albums,
        IReadOnlyCollection<StaffMember>? staff = null,
        IReadOnlyCollection<Booking>? bookings = null)
    {
        var labelsById = board.Labels
            .GroupBy(label => label.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => ToTaskLabel(group.First()), StringComparer.Ordinal);
        var albumsById = albums
            .GroupBy(album => album.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var staffById = (staff ?? [])
            .GroupBy(member => member.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var bookingsById = (bookings ?? [])
            .GroupBy(booking => booking.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        return new TaskBoardDto(
            board.Id,
            board.Title,
            board.Description,
            board.Theme,
            board.CardBackground,
            board.CardTextColor,
            board.Labels.Select(ToTaskLabel).ToList(),
            board.Lists
                .OrderBy(list => list.Position)
                .Select(list => new TaskListDto(
                    list.Id,
                    list.Title,
                    list.Color,
                    list.Cards
                        .OrderBy(card => card.Position)
                        .ThenBy(card => card.CreatedAt)
                        .Select(card => ToTaskCard(card, list.Id, labelsById, albumsById, staffById, bookingsById))
                        .ToList()))
                .ToList(),
            board.CreatedAt,
            board.UpdatedAt);
    }

    public static Photo? FindCover(Album album)
    {
        return album.Photos.FirstOrDefault(photo => photo.Id == album.CoverPhotoId)
               ?? album.Photos.FirstOrDefault();
    }

    private static TaskLabelDto ToTaskLabel(TaskLabel label) =>
        new(label.Id, label.Name, label.Color);

    private static TaskCardDto ToTaskCard(
        TaskCard card,
        string listId,
        IReadOnlyDictionary<string, TaskLabelDto> labelsById,
        IReadOnlyDictionary<string, Album> albumsById,
        IReadOnlyDictionary<string, StaffMember> staffById,
        IReadOnlyDictionary<string, Booking> bookingsById)
    {
        albumsById.TryGetValue(card.AlbumId, out var album);
        staffById.TryGetValue(card.AssigneeId, out var assignee);
        bookingsById.TryGetValue(card.BookingId, out var booking);
        var cover = album is null ? null : FindCover(album);
        var labels = card.LabelIds
            .Select(id => labelsById.GetValueOrDefault(id))
            .Where(label => label is not null)
            .Select(label => label!)
            .ToList();

        return new TaskCardDto(
            card.Id,
            listId,
            card.Title,
            card.Description,
            card.DueDate,
            card.IsDueComplete,
            labels,
            card.Checklist
                .Select(item => new TaskChecklistItemDto(item.Id, item.Text, item.Done))
                .ToList(),
            card.Activities
                .OrderByDescending(activity => activity.CreatedAt)
                .Select(activity => new TaskActivityDto(
                    activity.Id,
                    activity.Type,
                    activity.ActorId,
                    activity.ActorName,
                    activity.ActorRole,
                    activity.Summary,
                    activity.Details,
                    activity.CreatedAt))
                .ToList(),
            card.AlbumId,
            album?.Title,
            cover?.ThumbUrl,
            card.AssigneeId,
            assignee?.Name,
            assignee?.Role,
            card.Priority,
            card.BookingId,
            booking?.Code,
            card.CreatedAt,
            card.UpdatedAt);
    }

    public static CustomerDto ToCustomer(
        Customer customer,
        IReadOnlyCollection<Album> albums,
        IReadOnlyCollection<Booking> bookings)
    {
        var customerAlbums = albums
            .Where(album => string.Equals(album.CustomerId, customer.Id, StringComparison.Ordinal))
            .ToList();
        var customerBookings = bookings
            .Where(booking => string.Equals(booking.CustomerId, customer.Id, StringComparison.Ordinal))
            .ToList();
        var lastBooking = customerBookings
            .Select(booking => ParseDate(booking.Date))
            .Where(date => date is not null)
            .Max();

        return new CustomerDto(
            customer.Id,
            customer.Name,
            customer.Type,
            customer.Phone,
            customer.Email,
            customer.Address,
            customer.Source,
            customer.Notes,
            customer.IsActive,
            customerAlbums.Count,
            customerBookings.Count,
            lastBooking,
            customer.CreatedAt,
            customer.UpdatedAt);
    }

    public static ServicePackageDto ToPackage(
        ServicePackage package,
        IReadOnlyCollection<Booking> bookings)
    {
        var count = bookings.Count(booking =>
            string.Equals(booking.PackageId, package.Id, StringComparison.Ordinal));
        return new ServicePackageDto(
            package.Id,
            package.Name,
            package.Category,
            package.Price,
            package.Hours,
            package.Includes,
            package.Notes,
            package.IsActive,
            count,
            package.CreatedAt,
            package.UpdatedAt);
    }

    public static StaffDto ToStaff(
        StaffMember member,
        IReadOnlyCollection<StudioUser> users,
        IReadOnlyCollection<Booking> bookings)
    {
        var user = users.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, member.UserId, StringComparison.Ordinal));
        var count = bookings.Count(booking =>
            string.Equals(booking.PhotographerId, member.Id, StringComparison.Ordinal));
        return new StaffDto(
            member.Id,
            member.Name,
            member.Role,
            member.Phone,
            member.Email,
            member.UserId,
            user?.Username,
            member.IsActive,
            member.BaseSalary,
            member.CommissionRate,
            PayModes.Normalize(member.PayMode),
            member.HourlyRate,
            StaffShifts.Normalize(member.Shift),
            member.Notes,
            count,
            member.CreatedAt,
            member.UpdatedAt);
    }

    public static BookingDto ToBooking(Booking booking, AppData data)
    {
        var customer = data.Customers.FirstOrDefault(item =>
            string.Equals(item.Id, booking.CustomerId, StringComparison.Ordinal));
        var package = data.Packages.FirstOrDefault(item =>
            string.Equals(item.Id, booking.PackageId, StringComparison.Ordinal));
        var photographer = data.Staff.FirstOrDefault(item =>
            string.Equals(item.Id, booking.PhotographerId, StringComparison.Ordinal));
        var album = data.Albums.FirstOrDefault(item =>
            string.Equals(item.Id, booking.AlbumId, StringComparison.Ordinal));

        return new BookingDto(
            booking.Id,
            booking.Code,
            booking.CustomerId,
            customer?.Name,
            customer?.Phone,
            booking.PackageId,
            package?.Name,
            booking.Date,
            booking.StartTime,
            booking.Hours,
            booking.Location,
            booking.PhotographerId,
            photographer?.Name,
            booking.Status,
            booking.Notes,
            booking.AlbumId,
            album?.Title,
            booking.Price,
            booking.StatusHistory
                .OrderBy(entry => entry.ChangedAt)
                .Select(entry => new BookingStatusDto(entry.Status, entry.ActorName, entry.ChangedAt))
                .ToList(),
            booking.CreatedAt,
            booking.UpdatedAt);
    }

    public static AuditLogDto ToAuditLog(AuditLogEntry entry) =>
        new(
            entry.Id,
            entry.Module,
            entry.Action,
            entry.Summary,
            entry.ActorId,
            entry.ActorName,
            entry.ActorRole,
            entry.EntityId,
            entry.Details,
            entry.CreatedAt);

    public static CustomerDetailDto ToCustomerDetail(Customer customer, AppData data)
    {
        var bookings = data.Bookings
            .Where(booking => string.Equals(booking.CustomerId, customer.Id, StringComparison.Ordinal))
            .OrderByDescending(booking => booking.Date, StringComparer.Ordinal)
            .ThenByDescending(booking => booking.StartTime, StringComparer.Ordinal)
            .Select(booking => ToBooking(booking, data))
            .ToList();
        var albums = data.Albums
            .Where(album => string.Equals(album.CustomerId, customer.Id, StringComparison.Ordinal))
            .OrderByDescending(album => album.CreatedAt)
            .Select(ToSummary)
            .ToList();

        return new CustomerDetailDto(
            ToCustomer(customer, data.Albums, data.Bookings),
            bookings,
            albums);
    }

    public static CrmSummaryDto ToCrmSummary(AppData data, string today)
    {
        var month = today.Length >= 7 ? today[..7] : today;
        var upcoming = data.Bookings
            .Where(booking =>
                !string.Equals(booking.Status, BookingStatuses.Cancelled, StringComparison.Ordinal) &&
                string.CompareOrdinal(booking.Date, today) >= 0)
            .OrderBy(booking => booking.Date, StringComparer.Ordinal)
            .ThenBy(booking => booking.StartTime, StringComparer.Ordinal)
            .ToList();

        return new CrmSummaryDto(
            data.Customers.Count,
            data.Customers.Count(customer => customer.IsActive),
            data.Packages.Count,
            data.Staff.Count(member => member.IsActive),
            upcoming.Count,
            data.Bookings.Count(booking => booking.Date.StartsWith(month, StringComparison.Ordinal)),
            data.Bookings.Count(booking => string.Equals(booking.Date, today, StringComparison.Ordinal)),
            data.Bookings.Count(booking => string.Equals(booking.Status, BookingStatuses.Pending, StringComparison.Ordinal)),
            upcoming.Take(5).Select(booking => ToBooking(booking, data)).ToList());
    }

    public static OrderDto ToOrder(
        Order order,
        AppData data,
        IReadOnlyCollection<Payment>? payments = null)
    {
        var customer = data.Customers.FirstOrDefault(item =>
            string.Equals(item.Id, order.CustomerId, StringComparison.Ordinal));
        var booking = data.Bookings.FirstOrDefault(item =>
            string.Equals(item.Id, order.BookingId, StringComparison.Ordinal));
        var linked = payments ?? data.Payments
            .Where(payment => string.Equals(payment.OrderId, order.Id, StringComparison.Ordinal))
            .ToList();
        var subtotal = RoundMoney(order.Items.Sum(item => item.Quantity * item.UnitPrice));
        var total = Math.Max(0, RoundMoney(subtotal - order.Discount));
        var paid = Math.Max(0, RoundMoney(linked.Sum(payment => payment.Amount)));
        var remaining = Math.Max(0, RoundMoney(total - paid));
        var paymentStatus = total <= 0
            ? "unpaid"
            : paid >= total
                ? "paid"
                : paid > 0
                    ? "deposit"
                    : "unpaid";

        return new OrderDto(
            order.Id,
            order.Code,
            order.CustomerId,
            customer?.Name,
            customer?.Phone,
            order.BookingId,
            booking?.Code,
            order.Items
                .Select(item => new OrderItemDto(
                    item.Name,
                    item.Quantity,
                    item.UnitPrice,
                    RoundMoney(item.Quantity * item.UnitPrice)))
                .ToList(),
            subtotal,
            order.Discount,
            total,
            paid,
            remaining,
            paymentStatus,
            order.Status,
            order.Notes,
            linked.Count,
            order.CreatedAt,
            order.UpdatedAt);
    }

    public static PaymentDto ToPayment(Payment payment, AppData data)
    {
        var order = data.Orders.FirstOrDefault(item =>
            string.Equals(item.Id, payment.OrderId, StringComparison.Ordinal));
        var customer = data.Customers.FirstOrDefault(item =>
            string.Equals(item.Id, payment.CustomerId, StringComparison.Ordinal));

        return new PaymentDto(
            payment.Id,
            payment.Code,
            payment.OrderId,
            order?.Code,
            payment.CustomerId,
            customer?.Name,
            payment.Amount,
            payment.Method,
            payment.PaidAt,
            payment.Note,
            payment.ActorName,
            payment.CreatedAt);
    }

    public static ContractDto ToContract(Contract contract, AppData data, string today)
    {
        var customer = data.Customers.FirstOrDefault(item =>
            string.Equals(item.Id, contract.CustomerId, StringComparison.Ordinal));
        var booking = data.Bookings.FirstOrDefault(item =>
            string.Equals(item.Id, contract.BookingId, StringComparison.Ordinal));
        var paid = RoundMoney(contract.Schedule
            .Where(item => item.Paid)
            .Sum(item => item.Amount));
        var total = Math.Max(0, contract.Total);
        var remaining = Math.Max(0, RoundMoney(total - paid));
        var progress = total > 0
            ? (int)Math.Clamp(Math.Round(paid / total * 100, MidpointRounding.AwayFromZero), 0, 100)
            : 0;

        return new ContractDto(
            contract.Id,
            contract.Code,
            contract.CustomerId,
            customer?.Name,
            customer?.Phone,
            contract.BookingId,
            booking?.Code,
            contract.Service,
            total,
            paid,
            remaining,
            progress,
            contract.Status,
            contract.Schedule
                .Select(item => new ContractScheduleDto(
                    item.Label,
                    item.Amount,
                    item.DueDate,
                    item.Paid,
                    item.PaidAt,
                    !item.Paid &&
                    item.DueDate.Length > 0 &&
                    string.CompareOrdinal(item.DueDate, today) < 0))
                .ToList(),
            contract.Terms,
            contract.CreatedAt,
            contract.UpdatedAt);
    }

    public static InvoiceDto ToInvoice(Order order, AppData data)
    {
        var customer = data.Customers.FirstOrDefault(item =>
            string.Equals(item.Id, order.CustomerId, StringComparison.Ordinal));
        var linked = data.Payments
            .Where(payment => string.Equals(payment.OrderId, order.Id, StringComparison.Ordinal))
            .ToList();
        var subtotal = RoundMoney(order.Items.Sum(item => item.Quantity * item.UnitPrice));
        var total = Math.Max(0, RoundMoney(subtotal - order.Discount));
        var paid = Math.Max(0, RoundMoney(linked.Sum(payment => payment.Amount)));

        return new InvoiceDto(
            order.Code,
            order.CreatedAt,
            data.Settings.StudioName,
            data.Settings.ContactEmail,
            customer?.Name ?? "Khách lẻ",
            customer?.Phone ?? "",
            customer?.Address ?? "",
            order.Items
                .Select(item => new OrderItemDto(
                    item.Name,
                    item.Quantity,
                    item.UnitPrice,
                    RoundMoney(item.Quantity * item.UnitPrice)))
                .ToList(),
            subtotal,
            order.Discount,
            total,
            paid,
            Math.Max(0, RoundMoney(total - paid)),
            order.Notes);
    }

    public static decimal RoundMoney(decimal value) =>
        Math.Round(value, 0, MidpointRounding.AwayFromZero);

    public static BoardContextDto ToBoardContext(AppData data)
    {
        return new BoardContextDto(
            data.Albums
                .OrderByDescending(album => album.CreatedAt)
                .Select(album => new AlbumOptionDto(album.Id, album.Title, album.ClientName, album.CustomerId))
                .ToList(),
            data.Staff
                .Where(member => member.IsActive)
                .OrderBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
                .Select(member => new StaffOptionDto(member.Id, member.Name, member.Role))
                .ToList(),
            data.Bookings
                .OrderByDescending(booking => booking.Date, StringComparer.Ordinal)
                .ThenByDescending(booking => booking.StartTime, StringComparer.Ordinal)
                .Take(300)
                .Select(booking =>
                {
                    var customer = data.Customers.FirstOrDefault(item =>
                        string.Equals(item.Id, booking.CustomerId, StringComparison.Ordinal));
                    return new BookingOptionDto(
                        booking.Id,
                        booking.Code,
                        customer?.Name,
                        booking.Date,
                        booking.Status);
                })
                .ToList());
    }

    public static FinanceEntryDto ToFinanceEntry(ExpenseEntry entry) =>
        new(
            entry.Id,
            FinanceEntryKinds.Normalize(entry.Kind),
            entry.Date,
            entry.Category,
            RoundMoney(entry.Amount),
            entry.Note,
            (entry.Photos ?? [])
                .Select(photo => new FinancePhotoDto(
                    photo.Id,
                    $"/api/admin/finance/photos/{Uri.EscapeDataString(photo.Id)}",
                    photo.OriginalName,
                    photo.ContentType,
                    photo.Size,
                    photo.Caption))
                .ToList(),
            entry.CreatedByName,
            entry.CreatedAt);

    public static PayrollDto ToPayroll(PayrollEntry payroll, AppData data)
    {
        var staff = data.Staff.FirstOrDefault(member =>
            string.Equals(member.Id, payroll.StaffId, StringComparison.Ordinal));
        return new PayrollDto(
            payroll.Id,
            payroll.Month,
            payroll.StaffId,
            staff?.Name ?? "Nhân sự",
            RoundMoney(payroll.Base),
            Math.Round(payroll.Hours, 2),
            RoundMoney(payroll.CommissionBase),
            RoundMoney(payroll.Commission),
            RoundMoney(payroll.Total),
            payroll.Paid,
            payroll.PaidAt,
            payroll.ComputedAt);
    }

    public static AttendanceDto ToAttendance(AttendanceRecord record, AppData data)
    {
        var staff = data.Staff.FirstOrDefault(member =>
            string.Equals(member.Id, record.StaffId, StringComparison.Ordinal));
        var shift = EffectiveShift(data, record.StaffId, record.Date, staff?.Shift);
        var (late, early) = OperationsMath.Lateness(record, shift);
        return new AttendanceDto(
            record.Id,
            record.StaffId,
            staff?.Name ?? "Nhân sự",
            record.Date,
            record.In,
            record.Out,
            OperationsMath.WorkedHours(record),
            late,
            early,
            OperationsMath.AttendanceStatus(record, shift),
            shift,
            record.Note);
    }

    /// <summary>
    /// Ca hiệu lực của một nhân sự trong ngày: ưu tiên lịch chia ca, sau đó
    /// mới tới ca mặc định trong hồ sơ. Ngày nghỉ vẫn đối chiếu với ca hồ sơ.
    /// </summary>
    public static string EffectiveShift(AppData data, string staffId, string date, string? profileShift)
    {
        var assignment = data.ShiftAssignments.FirstOrDefault(item =>
            string.Equals(item.StaffId, staffId, StringComparison.Ordinal) &&
            string.Equals(item.Date, date, StringComparison.Ordinal));
        if (assignment is not null)
        {
            var normalized = ShiftAssignmentKinds.Normalize(assignment.Shift);
            if (normalized.Length > 0 && normalized != ShiftAssignmentKinds.Off)
            {
                return normalized;
            }
        }

        return StaffShifts.Normalize(profileShift);
    }

    public static AttendanceMonthDto ToAttendanceMonth(
        AppData data,
        string month,
        StaffMember staff,
        List<AttendanceRecord> records)
    {
        var hours = 0m;
        var lateCount = 0;
        var earlyCount = 0;
        var missingOut = 0;
        foreach (var record in records)
        {
            var shift = EffectiveShift(data, staff.Id, record.Date, staff.Shift);
            var status = OperationsMath.AttendanceStatus(record, shift);
            hours += OperationsMath.WorkedHours(record);
            if (status is "late" or "late_early")
            {
                lateCount++;
            }

            if (status is "early" or "late_early")
            {
                earlyCount++;
            }

            if (status == "open")
            {
                missingOut++;
            }
        }

        return new AttendanceMonthDto(
            month,
            staff.Id,
            staff.Name,
            staff.Shift,
            records.Count,
            Math.Round(hours, 2),
            lateCount,
            earlyCount,
            missingOut);
    }

    public static OutfitDto ToOutfit(Outfit outfit, IReadOnlyCollection<OutfitLoan> loans)
    {
        var loaned = loans
            .Where(loan => string.Equals(loan.OutfitId, outfit.Id, StringComparison.Ordinal) &&
                           string.IsNullOrWhiteSpace(loan.ReturnDate))
            .Sum(loan => loan.Quantity);
        return new OutfitDto(
            outfit.Id,
            outfit.Name,
            outfit.Category,
            outfit.Size,
            outfit.Color,
            outfit.Quantity,
            outfit.Note,
            loaned,
            outfit.CreatedAt);
    }

    public static ConceptDto ToConcept(StudioConcept concept) =>
        new(
            concept.Id,
            concept.Name,
            concept.Description,
            RoundMoney(concept.Fee),
            concept.Status,
            concept.CreatedAt);

    public static LoanDto ToLoan(OutfitLoan loan, AppData data, string today)
    {
        var outfit = data.Outfits.FirstOrDefault(item =>
            string.Equals(item.Id, loan.OutfitId, StringComparison.Ordinal));
        var customer = data.Customers.FirstOrDefault(item =>
            string.Equals(item.Id, loan.CustomerId, StringComparison.Ordinal));
        var staff = data.Staff.FirstOrDefault(item =>
            string.Equals(item.Id, loan.StaffId, StringComparison.Ordinal));
        var booking = data.Bookings.FirstOrDefault(item =>
            string.Equals(item.Id, loan.BookingId, StringComparison.Ordinal));
        var returned = !string.IsNullOrWhiteSpace(loan.ReturnDate);
        var overdue = !returned &&
                      !string.IsNullOrWhiteSpace(loan.DueDate) &&
                      string.CompareOrdinal(loan.DueDate, today) < 0;
        return new LoanDto(
            loan.Id,
            loan.OutfitId,
            outfit?.Name ?? "Trang phục",
            loan.Quantity,
            loan.BorrowerType,
            loan.CustomerId,
            customer?.Name,
            loan.StaffId,
            staff?.Name,
            loan.BookingId,
            booking?.Code,
            loan.BorrowDate,
            loan.DueDate,
            loan.ReturnDate,
            RoundMoney(loan.Deposit),
            loan.Notes,
            returned,
            overdue);
    }

    public static NotificationDto ToNotification(NotificationEntry notification) =>
        new(
            notification.Id,
            notification.Type,
            notification.Title,
            notification.Body,
            notification.Link,
            notification.IsRead,
            notification.CreatedAt);

    public static NotificationSettingsDto ToNotificationSettings(NotificationSettings settings) =>
        new(
            settings.Master,
            settings.Booking,
            settings.Payment,
            settings.Contract,
            settings.Wardrobe,
            settings.Payroll);

    private static DateTimeOffset? ParseDate(string? value)
    {
        return DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var date)
            ? new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : null;
    }

    private static PhotoDto ToAdminPhoto(Photo photo, IReadOnlyDictionary<string, DateTimeOffset> selected)
    {
        selected.TryGetValue(photo.Id, out var selectedAt);
        var isSelected = selected.ContainsKey(photo.Id);
        return new PhotoDto(
            photo.Id,
            photo.Code,
            photo.FileName,
            photo.ThumbUrl,
            photo.PreviewUrl,
            photo.DownloadUrl,
            photo.Width,
            photo.Height,
            isSelected,
            isSelected ? selectedAt : null);
    }

    private static PhotoDto ToPublicPhoto(
        Photo photo,
        Album album,
        IReadOnlyDictionary<string, DateTimeOffset> selected)
    {
        selected.TryGetValue(photo.Id, out var selectedAt);
        var isSelected = selected.ContainsKey(photo.Id);
        return new PhotoDto(
            photo.Id,
            photo.Code,
            photo.FileName,
            photo.ThumbUrl,
            photo.PreviewUrl,
            album.AllowDownloads ? photo.DownloadUrl : null,
            photo.Width,
            photo.Height,
            isSelected,
            isSelected ? selectedAt : null);
    }

    private static string DriveThumbUrl(string fileId, int width) =>
        $"https://drive.google.com/thumbnail?id={Uri.EscapeDataString(fileId)}&sz=w{width}";
}