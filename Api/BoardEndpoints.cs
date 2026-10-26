using System.Globalization;
using MayAlbum.Models;
using MayAlbum.Services;

namespace MayAlbum.Api;

internal static class BoardEndpoints
{
    private const string DefaultLabelColor = "#b25f46";
    private const string DefaultListColor = "#c3a47a";
    private const string DefaultCardBackground = "#fbf8f2";
    private const string DefaultCardTextColor = "#2b251e";
    private static readonly string[] AllowedThemes = ["ink", "olive", "clay", "dusk", "sand"];

    public static void Map(RouteGroupBuilder group)
    {
        var board = new StudioPermissionFilter(StudioPermissions.Board);
        group.MapGet("/board", GetBoard).AddEndpointFilter(board);
        group.MapGet("/board/context", GetBoardContext).AddEndpointFilter(board);
        group.MapPut("/board", UpdateBoard).AddEndpointFilter(board);
        group.MapPost("/board/labels", CreateLabel).AddEndpointFilter(board);
        group.MapPut("/board/labels/{id}", UpdateLabel).AddEndpointFilter(board);
        group.MapDelete("/board/labels/{id}", DeleteLabel).AddEndpointFilter(board);
        group.MapPost("/board/lists", CreateList).AddEndpointFilter(board);
        group.MapPut("/board/lists/{id}", UpdateList).AddEndpointFilter(board);
        group.MapPost("/board/lists/{id}/move", MoveList).AddEndpointFilter(board);
        group.MapDelete("/board/lists/{id}", DeleteList).AddEndpointFilter(board);
        group.MapPost("/board/lists/{listId}/cards", CreateCard).AddEndpointFilter(board);
        group.MapPost("/board/cards/{id}/move", MoveCard).AddEndpointFilter(board);
        group.MapPut("/board/cards/{id}", UpdateCard).AddEndpointFilter(board);
        group.MapDelete("/board/cards/{id}", DeleteCard).AddEndpointFilter(board);
    }

    private static IResult GetBoard(DataStore store) =>
        Results.Ok(store.Read(ApiMappings.ToBoard));

    private static IResult GetBoardContext(DataStore store) =>
        Results.Ok(store.Read(ApiMappings.ToBoardContext));

    private static IResult UpdateBoard(UpdateBoardRequest request, DataStore store)
    {
        var dto = store.Write(data =>
        {
            var board = data.Board;
            if (request.Title is not null)
            {
                board.Title = RequireText(
                    request.Title,
                    80,
                    "Tên bảng không được để trống.",
                    "invalid_board_title");
            }

            if (request.Description is not null)
            {
                board.Description = AlbumText.TrimTo(request.Description, 600);
            }

            if (request.Theme is not null)
            {
                board.Theme = NormalizeTheme(request.Theme);
            }

            if (request.CardBackground is not null)
            {
                board.CardBackground = NormalizeColor(request.CardBackground, DefaultCardBackground);
            }

            if (request.CardTextColor is not null)
            {
                board.CardTextColor = NormalizeColor(request.CardTextColor, DefaultCardTextColor);
            }

            board.UpdatedAt = DateTimeOffset.UtcNow;
            return ApiMappings.ToBoard(data);
        });

        return Results.Ok(dto);
    }

    private static IResult CreateLabel(CreateTaskLabelRequest request, DataStore store)
    {
        var name = RequireText(request.Name, 40, "Tên nhãn không được để trống.", "invalid_label_name");
        var color = NormalizeColor(request.Color, DefaultLabelColor);

        var dto = store.Write(data =>
        {
            var board = data.Board;
            board.Labels.Add(new TaskLabel
            {
                Id = NewShortId(),
                Name = name,
                Color = color
            });
            board.UpdatedAt = DateTimeOffset.UtcNow;
            return ApiMappings.ToBoard(data);
        });

        return Results.Ok(dto);
    }

    private static IResult UpdateLabel(string id, UpdateTaskLabelRequest request, DataStore store)
    {
        var dto = store.Write(data =>
        {
            var board = data.Board;
            var label = FindLabel(board, id);
            var name = request.Name is null
                ? null
                : RequireText(request.Name, 40, "Tên nhãn không được để trống.", "invalid_label_name");
            var color = request.Color is null ? null : NormalizeColor(request.Color, DefaultLabelColor);

            if (name is not null)
            {
                label.Name = name;
            }

            if (color is not null)
            {
                label.Color = color;
            }

            board.UpdatedAt = DateTimeOffset.UtcNow;
            return ApiMappings.ToBoard(data);
        });

        return Results.Ok(dto);
    }

    private static IResult DeleteLabel(string id, DataStore store)
    {
        var dto = store.Write(data =>
        {
            var board = data.Board;
            var label = FindLabel(board, id);
            board.Labels.Remove(label);
            foreach (var card in board.Lists.SelectMany(list => list.Cards))
            {
                card.LabelIds.RemoveAll(labelId => string.Equals(labelId, label.Id, StringComparison.Ordinal));
            }

            board.UpdatedAt = DateTimeOffset.UtcNow;
            return ApiMappings.ToBoard(data);
        });

        return Results.Ok(dto);
    }

    private static IResult CreateList(CreateTaskListRequest request, DataStore store)
    {
        var title = RequireText(request.Title, 60, "Tên danh sách không được để trống.", "invalid_list_title");
        var color = NormalizeColor(request.Color, DefaultListColor);

        var dto = store.Write(data =>
        {
            var board = data.Board;
            var list = new TaskList
            {
                Id = NewShortId(),
                Title = title,
                Color = color,
                Position = board.Lists.Count,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            board.Lists.Add(list);
            board.UpdatedAt = DateTimeOffset.UtcNow;
            return ApiMappings.ToBoard(data);
        });

        return Results.Ok(dto);
    }

    private static IResult UpdateList(string id, UpdateTaskListRequest request, DataStore store)
    {
        var dto = store.Write(data =>
        {
            var board = data.Board;
            var list = FindList(board, id);
            var title = request.Title is null
                ? null
                : RequireText(request.Title, 60, "Tên danh sách không được để trống.", "invalid_list_title");
            var color = request.Color is null ? null : NormalizeColor(request.Color, DefaultListColor);

            if (title is not null)
            {
                list.Title = title;
            }

            if (color is not null)
            {
                list.Color = color;
            }

            list.UpdatedAt = DateTimeOffset.UtcNow;
            board.UpdatedAt = DateTimeOffset.UtcNow;
            return ApiMappings.ToBoard(data);
        });

        return Results.Ok(dto);
    }

    private static IResult MoveList(string id, MoveTaskListRequest request, DataStore store)
    {
        var dto = store.Write(data =>
        {
            var board = data.Board;
            var list = FindList(board, id);
            board.Lists.Remove(list);
            var index = Math.Clamp(request.Position, 0, board.Lists.Count);
            board.Lists.Insert(index, list);
            RenumberLists(board);
            list.UpdatedAt = DateTimeOffset.UtcNow;
            board.UpdatedAt = DateTimeOffset.UtcNow;
            return ApiMappings.ToBoard(data);
        });

        return Results.Ok(dto);
    }

    private static IResult DeleteList(string id, DataStore store)
    {
        var dto = store.Write(data =>
        {
            var board = data.Board;
            var list = FindList(board, id);
            board.Lists.Remove(list);
            RenumberLists(board);
            board.UpdatedAt = DateTimeOffset.UtcNow;
            return ApiMappings.ToBoard(data);
        });

        return Results.Ok(dto);
    }

    private static IResult CreateCard(
        string listId,
        CreateTaskCardRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var title = RequireText(request.Title, 160, "Tên thẻ không được để trống.", "invalid_card_title");
        var dto = store.Write(data =>
        {
            var board = data.Board;
            var list = FindList(board, listId);
            var cardId = NewShortId();
            var assigneeId = NormalizeStaffId(request.AssigneeId, data.Staff);
            var priority = NormalizePriority(request.Priority);
            var bookingId = NormalizeBookingId(request.BookingId, data.Bookings);
            var card = new TaskCard
            {
                Id = cardId,
                Title = title,
                Description = AlbumText.TrimTo(request.Description, 5000),
                DueDate = NormalizeDueDate(request.DueDate),
                IsDueComplete = request.IsDueComplete ?? false,
                LabelIds = SanitizeLabelIds(request.LabelIds, board),
                Checklist = BuildChecklist(cardId, [], request.Checklist),
                AlbumId = NormalizeAlbumId(request.AlbumId, data.Albums),
                AssigneeId = assigneeId,
                Priority = priority,
                BookingId = bookingId,
                Position = list.Cards.Count,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            list.Cards.Add(card);
            list.UpdatedAt = DateTimeOffset.UtcNow;
            board.UpdatedAt = DateTimeOffset.UtcNow;

            var details = new List<string>();
            if (card.DueDate.Length > 0)
            {
                details.Add($"Hạn: {FormatDueDate(card.DueDate)}");
            }

            var labelNames = board.Labels
                .Where(label => card.LabelIds.Contains(label.Id))
                .Select(label => label.Name)
                .ToList();
            if (labelNames.Count > 0)
            {
                details.Add($"Nhãn: {string.Join(", ", labelNames)}");
            }

            if (card.Checklist.Count > 0)
            {
                details.Add($"{card.Checklist.Count} việc trong checklist");
            }

            var albumTitle = data.Albums
                .FirstOrDefault(album => string.Equals(album.Id, card.AlbumId, StringComparison.Ordinal))
                ?.Title;
            if (!string.IsNullOrWhiteSpace(albumTitle))
            {
                details.Add($"Album liên kết: {Quote(albumTitle)}");
            }

            if (!string.IsNullOrWhiteSpace(card.AssigneeId))
            {
                var assignee = data.Staff.FirstOrDefault(member =>
                    string.Equals(member.Id, card.AssigneeId, StringComparison.Ordinal));
                if (assignee is not null)
                {
                    details.Add($"Phụ trách: {assignee.Name}");
                }
            }

            if (!string.Equals(card.Priority, "normal", StringComparison.Ordinal))
            {
                details.Add($"Độ ưu tiên: {PriorityLabel(card.Priority)}");
            }

            if (!string.IsNullOrWhiteSpace(card.BookingId))
            {
                var booking = data.Bookings.FirstOrDefault(item =>
                    string.Equals(item.Id, card.BookingId, StringComparison.Ordinal));
                if (booking is not null)
                {
                    details.Add($"Lịch chụp: {booking.Code}");
                }
            }

            AddActivity(
                card,
                actor,
                "created",
                $"đã tạo thẻ trong danh sách {Quote(list.Title)}",
                details);
            StudioAudit.Add(
                data,
                actor,
                "board",
                "create",
                $"đã tạo thẻ {Quote(card.Title)} trong danh sách {Quote(list.Title)}",
                card.Id,
                details);
            return ApiMappings.ToBoard(data);
        });

        return Results.Ok(dto);
    }

    private static IResult UpdateCard(
        string id,
        UpdateTaskCardRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var dto = store.Write(data =>
        {
            var board = data.Board;
            var source = FindCard(board, id);
            var card = source.Card;
            var title = request.Title is null
                ? null
                : RequireText(request.Title, 160, "Tên thẻ không được để trống.", "invalid_card_title");
            var dueDate = request.DueDate is null ? null : NormalizeDueDate(request.DueDate);
            var labelIds = request.LabelIds is null ? null : SanitizeLabelIds(request.LabelIds, board);
            var checklist = request.Checklist is null
                ? null
                : BuildChecklist(card.Id, card.Checklist, request.Checklist);
            var albumId = request.AlbumId is null ? null : NormalizeAlbumId(request.AlbumId, data.Albums);
            var assigneeId = request.AssigneeId is null ? null : NormalizeStaffId(request.AssigneeId, data.Staff);
            var priority = request.Priority is null ? null : NormalizePriority(request.Priority);
            var bookingId = request.BookingId is null ? null : NormalizeBookingId(request.BookingId, data.Bookings);
            TaskList? target = null;
            if (request.ListId is not null)
            {
                target = FindList(board, request.ListId);
            }

            var oldTitle = card.Title;
            var oldDescription = card.Description;
            var oldDueDate = card.DueDate;
            var oldDueComplete = card.IsDueComplete;
            var oldLabelIds = card.LabelIds.ToHashSet(StringComparer.Ordinal);
            var oldChecklist = card.Checklist
                .GroupBy(item => item.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var oldAlbumId = card.AlbumId;
            var oldAssigneeId = card.AssigneeId;
            var oldPriority = card.Priority;
            var oldBookingId = card.BookingId;
            var oldListTitle = source.List.Title;
            var movedList = target is not null &&
                            !string.Equals(target.Id, source.List.Id, StringComparison.Ordinal);

            if (title is not null)
            {
                card.Title = title;
            }

            if (request.Description is not null)
            {
                card.Description = AlbumText.TrimTo(request.Description, 5000);
            }

            if (dueDate is not null)
            {
                card.DueDate = dueDate;
            }

            if (request.IsDueComplete is not null)
            {
                card.IsDueComplete = request.IsDueComplete.Value;
            }

            if (labelIds is not null)
            {
                card.LabelIds = labelIds;
            }

            if (checklist is not null)
            {
                card.Checklist = checklist;
            }

            if (albumId is not null)
            {
                card.AlbumId = albumId;
            }

            if (assigneeId is not null)
            {
                card.AssigneeId = assigneeId;
            }

            if (priority is not null)
            {
                card.Priority = priority;
            }

            if (bookingId is not null)
            {
                card.BookingId = bookingId;
            }

            card.UpdatedAt = DateTimeOffset.UtcNow;

            if (movedList)
            {
                source.List.Cards.Remove(card);
                card.Position = target!.Cards.Count;
                target.Cards.Add(card);
                RenumberCards(source.List);
                RenumberCards(target);
                source.List.UpdatedAt = DateTimeOffset.UtcNow;
                target.UpdatedAt = DateTimeOffset.UtcNow;
            }

            var details = new List<string>();
            if (title is not null && !string.Equals(oldTitle, card.Title, StringComparison.Ordinal))
            {
                details.Add($"Đổi tên thẻ: {Quote(oldTitle)} → {Quote(card.Title)}");
            }

            if (request.Description is not null &&
                !string.Equals(oldDescription, card.Description, StringComparison.Ordinal))
            {
                details.Add("Cập nhật mô tả");
            }

            if (dueDate is not null && !string.Equals(oldDueDate, card.DueDate, StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(card.DueDate))
                {
                    details.Add("Xoá ngày đến hạn");
                }
                else if (string.IsNullOrWhiteSpace(oldDueDate))
                {
                    details.Add($"Đặt hạn {FormatDueDate(card.DueDate)}");
                }
                else
                {
                    details.Add($"Đổi hạn: {FormatDueDate(oldDueDate)} → {FormatDueDate(card.DueDate)}");
                }
            }

            if (request.IsDueComplete is not null && oldDueComplete != card.IsDueComplete)
            {
                details.Add(card.IsDueComplete ? "Đánh dấu thẻ đã hoàn tất" : "Bỏ đánh dấu hoàn tất");
            }

            if (labelIds is not null)
            {
                var newLabelIds = card.LabelIds.ToHashSet(StringComparer.Ordinal);
                var labelsById = board.Labels
                    .GroupBy(label => label.Id, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.Ordinal);
                foreach (var added in newLabelIds.Except(oldLabelIds, StringComparer.Ordinal))
                {
                    if (labelsById.TryGetValue(added, out var name))
                    {
                        details.Add($"Thêm nhãn {Quote(name)}");
                    }
                }

                foreach (var removed in oldLabelIds.Except(newLabelIds, StringComparer.Ordinal))
                {
                    if (labelsById.TryGetValue(removed, out var name))
                    {
                        details.Add($"Gỡ nhãn {Quote(name)}");
                    }
                }
            }

            if (checklist is not null)
            {
                details.AddRange(DescribeChecklistChanges(oldChecklist, card.Checklist));
            }

            if (albumId is not null && !string.Equals(oldAlbumId, card.AlbumId, StringComparison.Ordinal))
            {
                var oldAlbumTitle = data.Albums
                    .FirstOrDefault(album => string.Equals(album.Id, oldAlbumId, StringComparison.Ordinal))
                    ?.Title;
                var newAlbumTitle = data.Albums
                    .FirstOrDefault(album => string.Equals(album.Id, card.AlbumId, StringComparison.Ordinal))
                    ?.Title;
                if (string.IsNullOrWhiteSpace(card.AlbumId))
                {
                    details.Add("Bỏ liên kết album");
                }
                else if (string.IsNullOrWhiteSpace(oldAlbumId))
                {
                    details.Add($"Liên kết album {Quote(newAlbumTitle)}");
                }
                else
                {
                    details.Add($"Đổi album: {Quote(oldAlbumTitle)} → {Quote(newAlbumTitle)}");
                }
            }

            if (movedList)
            {
                details.Add($"Chuyển từ danh sách {Quote(oldListTitle)} sang {Quote(target!.Title)}");
            }

            if (assigneeId is not null && !string.Equals(oldAssigneeId, card.AssigneeId, StringComparison.Ordinal))
            {
                var assignee = data.Staff.FirstOrDefault(member =>
                    string.Equals(member.Id, card.AssigneeId, StringComparison.Ordinal));
                details.Add(string.IsNullOrWhiteSpace(card.AssigneeId)
                    ? "Bỏ người phụ trách"
                    : $"Giao cho {assignee?.Name ?? "thành viên"}");
            }

            if (priority is not null && !string.Equals(oldPriority, card.Priority, StringComparison.Ordinal))
            {
                details.Add($"Độ ưu tiên: {PriorityLabel(card.Priority)}");
            }

            if (bookingId is not null && !string.Equals(oldBookingId, card.BookingId, StringComparison.Ordinal))
            {
                var booking = data.Bookings.FirstOrDefault(item =>
                    string.Equals(item.Id, card.BookingId, StringComparison.Ordinal));
                details.Add(string.IsNullOrWhiteSpace(card.BookingId)
                    ? "Bỏ liên kết lịch chụp"
                    : $"Gắn lịch chụp {booking?.Code ?? ""}".Trim());
            }

            if (details.Count > 0)
            {
                var summary = movedList
                    ? (details.Count == 1 ? "đã di chuyển thẻ" : "đã cập nhật và di chuyển thẻ")
                    : "đã cập nhật thẻ";
                AddActivity(
                    card,
                    actor,
                    movedList && details.Count == 1 ? "moved" : "updated",
                    summary,
                    details);
                StudioAudit.Add(
                    data,
                    actor,
                    "board",
                    "update",
                    $"đã cập nhật thẻ {Quote(card.Title)}",
                    card.Id,
                    details);
            }

            board.UpdatedAt = DateTimeOffset.UtcNow;
            return ApiMappings.ToBoard(data);
        });

        return Results.Ok(dto);
    }

    private static IResult MoveCard(
        string id,
        MoveTaskCardRequest request,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var dto = store.Write(data =>
        {
            var board = data.Board;
            var target = FindList(board, request.ListId);
            var source = FindCard(board, id);
            var card = source.Card;
            var sameList = ReferenceEquals(source.List, target);
            var oldListTitle = source.List.Title;
            var oldOrder = source.List.Cards.Select(item => item.Id).ToList();

            source.List.Cards.Remove(card);
            var index = Math.Clamp(request.Position, 0, target.Cards.Count);
            target.Cards.Insert(index, card);

            RenumberCards(source.List);
            if (!sameList)
            {
                RenumberCards(target);
            }

            source.List.UpdatedAt = DateTimeOffset.UtcNow;
            target.UpdatedAt = DateTimeOffset.UtcNow;
            card.UpdatedAt = DateTimeOffset.UtcNow;
            board.UpdatedAt = DateTimeOffset.UtcNow;

            var changed = !sameList ||
                          !oldOrder.SequenceEqual(target.Cards.Select(item => item.Id));
            if (changed)
            {
                if (sameList)
                {
                    AddActivity(
                        card,
                        actor,
                        "moved",
                        "đã sắp xếp lại thẻ",
                        [$"Trong danh sách {Quote(oldListTitle)}"]);
                    StudioAudit.Add(
                        data,
                        actor,
                        "board",
                        "move",
                        $"đã sắp xếp lại thẻ {Quote(card.Title)}",
                        card.Id,
                        [$"Trong danh sách {Quote(oldListTitle)}"]);
                }
                else
                {
                    AddActivity(
                        card,
                        actor,
                        "moved",
                        "đã kéo thẻ",
                        [$"Từ danh sách {Quote(oldListTitle)} sang {Quote(target.Title)}"]);
                    StudioAudit.Add(
                        data,
                        actor,
                        "board",
                        "move",
                        $"đã kéo thẻ {Quote(card.Title)} sang danh sách {Quote(target.Title)}",
                        card.Id,
                        [$"Từ danh sách {Quote(oldListTitle)} sang {Quote(target.Title)}"]);
                }
            }

            return ApiMappings.ToBoard(data);
        });

        return Results.Ok(dto);
    }

    private static IResult DeleteCard(
        string id,
        HttpContext context,
        DataStore store,
        AdminSessionStore sessions)
    {
        var actor = sessions.RequireUser(context);
        var dto = store.Write(data =>
        {
            var board = data.Board;
            var source = FindCard(board, id);
            var title = source.Card.Title;
            source.List.Cards.Remove(source.Card);
            RenumberCards(source.List);
            source.List.UpdatedAt = DateTimeOffset.UtcNow;
            board.UpdatedAt = DateTimeOffset.UtcNow;
            StudioAudit.Add(
                data,
                actor,
                "board",
                "delete",
                $"đã xoá thẻ {Quote(title)} khỏi danh sách {Quote(source.List.Title)}",
                id);
            return ApiMappings.ToBoard(data);
        });

        return Results.Ok(dto);
    }

    private static void AddActivity(
        TaskCard card,
        StudioUser actor,
        string type,
        string summary,
        IEnumerable<string>? details = null)
    {
        card.Activities ??= [];
        card.Activities.Insert(0, new TaskActivity
        {
            Id = NewShortId(),
            Type = type,
            ActorId = actor.Id,
            ActorName = string.IsNullOrWhiteSpace(actor.DisplayName) ? actor.Username : actor.DisplayName,
            ActorRole = actor.Role,
            Summary = summary,
            Details = details?
                .Where(detail => !string.IsNullOrWhiteSpace(detail))
                .Select(detail => AlbumText.TrimTo(detail, 240))
                .Distinct(StringComparer.Ordinal)
                .Take(12)
                .ToList() ?? [],
            CreatedAt = DateTimeOffset.UtcNow
        });

        if (card.Activities.Count > 60)
        {
            card.Activities.RemoveRange(60, card.Activities.Count - 60);
        }
    }

    private static IEnumerable<string> DescribeChecklistChanges(
        IReadOnlyDictionary<string, TaskChecklistItem> before,
        IEnumerable<TaskChecklistItem> after)
    {
        var details = new List<string>();
        var afterItems = after
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var item in afterItems.Values)
        {
            if (!before.TryGetValue(item.Id, out var old))
            {
                details.Add($"Thêm việc {Quote(item.Text)}");
                continue;
            }

            if (!string.Equals(old.Text, item.Text, StringComparison.Ordinal))
            {
                details.Add($"Sửa việc {Quote(old.Text)} thành {Quote(item.Text)}");
            }

            if (old.Done != item.Done)
            {
                details.Add(item.Done
                    ? $"Hoàn tất việc {Quote(item.Text)}"
                    : $"Bỏ hoàn tất việc {Quote(item.Text)}");
            }
        }

        foreach (var old in before.Values)
        {
            if (!afterItems.ContainsKey(old.Id))
            {
                details.Add($"Xoá việc {Quote(old.Text)}");
            }
        }

        return details;
    }

    private static string Quote(string? value)
    {
        var text = AlbumText.TrimTo(value, 80);
        return string.IsNullOrWhiteSpace(text) ? "“Không tên”" : $"“{text}”";
    }

    private static string FormatDueDate(string value)
    {
        return DateTime.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : value;
    }

    private static string RequireText(string? value, int maxLength, string message, string code)
    {
        var text = AlbumText.TrimTo(value, maxLength);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ApiException(400, message, code);
        }

        return text;
    }

    private static string NormalizeTheme(string value)
    {
        var theme = value.Trim().ToLowerInvariant();
        if (!AllowedThemes.Contains(theme, StringComparer.Ordinal))
        {
            throw new ApiException(400, "Giao diện bảng không hợp lệ.", "invalid_board_theme");
        }

        return theme;
    }

    private static string NormalizeColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var color = value.Trim();
        if (color.Length != 7 ||
            color[0] != '#' ||
            !color.Skip(1).All(Uri.IsHexDigit))
        {
            throw new ApiException(400, "Màu sắc chưa đúng định dạng.", "invalid_color");
        }

        return color.ToLowerInvariant();
    }

    private static string NormalizeDueDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var normalized = AlbumText.NormalizeEventDate(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ApiException(400, "Ngày đến hạn chưa đúng định dạng.", "invalid_due_date");
        }

        return normalized;
    }

    private static string NormalizeAlbumId(string? value, IReadOnlyCollection<Album> albums)
    {
        var albumId = value?.Trim() ?? "";
        if (albumId.Length == 0)
        {
            return "";
        }

        if (!albums.Any(album => string.Equals(album.Id, albumId, StringComparison.Ordinal)))
        {
            throw new ApiException(400, "Album liên kết không còn tồn tại.", "invalid_album");
        }

        return albumId;
    }

    private static string NormalizeStaffId(string? value, IReadOnlyCollection<StaffMember> staff)
    {
        var staffId = value?.Trim() ?? "";
        if (staffId.Length == 0)
        {
            return "";
        }

        if (!staff.Any(member => string.Equals(member.Id, staffId, StringComparison.Ordinal)))
        {
            throw new ApiException(400, "Người phụ trách không còn tồn tại.", "invalid_assignee");
        }

        return staffId;
    }

    private static string NormalizeBookingId(string? value, IReadOnlyCollection<Booking> bookings)
    {
        var bookingId = value?.Trim() ?? "";
        if (bookingId.Length == 0)
        {
            return "";
        }

        if (!bookings.Any(booking => string.Equals(booking.Id, bookingId, StringComparison.Ordinal)))
        {
            throw new ApiException(400, "Lịch chụp liên kết không còn tồn tại.", "invalid_booking");
        }

        return bookingId;
    }

    private static string NormalizePriority(string? value)
    {
        var priority = (value ?? "").Trim().ToLowerInvariant();
        if (priority.Length == 0)
        {
            return "normal";
        }

        if (priority is not ("low" or "normal" or "high" or "urgent"))
        {
            throw new ApiException(400, "Độ ưu tiên không hợp lệ.", "invalid_priority");
        }

        return priority;
    }

    private static string PriorityLabel(string? value) => value switch
    {
        "low" => "Thấp",
        "high" => "Cao",
        "urgent" => "Khẩn cấp",
        _ => "Bình thường"
    };

    private static List<string> SanitizeLabelIds(IEnumerable<string>? values, TaskBoard board)
    {
        if (values is null)
        {
            return [];
        }

        var valid = board.Labels.Select(label => label.Id).ToHashSet(StringComparer.Ordinal);
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value) && valid.Contains(value))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static List<TaskChecklistItem> BuildChecklist(
        string cardId,
        IEnumerable<TaskChecklistItem> existing,
        IEnumerable<TaskChecklistItemRequest>? requested)
    {
        if (requested is null)
        {
            return existing.ToList();
        }

        var existingById = existing
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<TaskChecklistItem>();

        foreach (var input in requested.Take(100))
        {
            var text = AlbumText.TrimTo(input.Text, 180);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            string id;
            if (!string.IsNullOrWhiteSpace(input.Id) &&
                existingById.ContainsKey(input.Id) &&
                usedIds.Add(input.Id))
            {
                id = input.Id;
            }
            else
            {
                id = NewChecklistId(cardId, usedIds);
            }

            result.Add(new TaskChecklistItem
            {
                Id = id,
                Text = text,
                Done = input.Done
            });
        }

        return result;
    }

    private static string NewChecklistId(string cardId, HashSet<string> used)
    {
        string id;
        do
        {
            id = $"{cardId}-check-{Guid.NewGuid().ToString("N")[..6]}";
        }
        while (!used.Add(id));

        return id;
    }

    private static string NewShortId() => Guid.NewGuid().ToString("N")[..10];

    private static TaskLabel FindLabel(TaskBoard board, string id) =>
        board.Labels.FirstOrDefault(label => string.Equals(label.Id, id, StringComparison.Ordinal))
        ?? throw new ApiException(404, "Không tìm thấy nhãn.", "label_not_found");

    private static TaskList FindList(TaskBoard board, string id) =>
        board.Lists.FirstOrDefault(list => string.Equals(list.Id, id, StringComparison.Ordinal))
        ?? throw new ApiException(404, "Không tìm thấy danh sách.", "list_not_found");

    private static (TaskList List, TaskCard Card) FindCard(TaskBoard board, string id)
    {
        foreach (var list in board.Lists)
        {
            var card = list.Cards.FirstOrDefault(item =>
                string.Equals(item.Id, id, StringComparison.Ordinal));
            if (card is not null)
            {
                return (list, card);
            }
        }

        throw new ApiException(404, "Không tìm thấy thẻ.", "card_not_found");
    }

    private static void RenumberLists(TaskBoard board)
    {
        for (var index = 0; index < board.Lists.Count; index++)
        {
            board.Lists[index].Position = index;
        }
    }

    private static void RenumberCards(TaskList list)
    {
        for (var index = 0; index < list.Cards.Count; index++)
        {
            list.Cards[index].Position = index;
        }
    }
}
