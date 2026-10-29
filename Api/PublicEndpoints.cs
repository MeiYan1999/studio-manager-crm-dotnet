using MayAlbum.Models;
using MayAlbum.Services;

namespace MayAlbum.Api;

public static class PublicEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/public/website", (DataStore store) => Results.Ok(store.Read(data => new
        {
            data.Settings.StudioName,
            data.Settings.LogoUrl,
            data.Settings.LoginImageUrl,
            data.Settings.Website
        })));

        app.MapGet("/api/public/logo", async (HttpContext context, DataStore store, IObjectStorage storage) =>
        {
            var logo = store.Read(data => new { data.Settings.LogoKey, data.Settings.LogoContentType });
            if (logo.LogoKey.Length == 0) return Results.NotFound();
            var stored = await storage.GetAsync(logo.LogoKey, context.RequestAborted);
            if (stored is null) return Results.NotFound();
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.CacheControl = "no-cache";
            return Results.Stream(stored.Content, logo.LogoContentType);
        });
        app.MapGet("/api/public/login-image", async (HttpContext context, DataStore store, IObjectStorage storage) =>
        {
            var logo = store.Read(data => new { data.Settings.LoginImageKey, data.Settings.LoginImageContentType });
            if (logo.LoginImageKey.Length == 0) return Results.NotFound();
            var stored = await storage.GetAsync(logo.LoginImageKey, context.RequestAborted);
            if (stored is null) return Results.NotFound();
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.CacheControl = "no-cache";
            return Results.Stream(stored.Content, logo.LoginImageContentType);
        });
        var group = app.MapGroup("/api/public/albums");

        group.MapGet("/{token}", (string token, DataStore store) =>
        {
            var payload = store.Read(data =>
            {
                var album = FindPublicAlbum(data, token);
                return ApiMappings.ToPublicAlbum(album, data.Settings);
            });

            return Results.Ok(payload);
        });

        group.MapPost("/{token}/favorites/toggle", (string token, ToggleFavoriteRequest request, DataStore store) =>
        {
            if (string.IsNullOrWhiteSpace(request.PhotoId))
            {
                throw new ApiException(400, "Thiếu thông tin ảnh.", "invalid_photo");
            }

            var response = store.Write(data =>
            {
                var album = FindPublicAlbum(data, token);
                if (!album.AllowFavorites)
                {
                    throw new ApiException(403, "Album này không nhận lựa chọn ảnh.", "favorites_disabled");
                }

                var photo = album.Photos.FirstOrDefault(item =>
                    string.Equals(item.Id, request.PhotoId, StringComparison.Ordinal));
                if (photo is null)
                {
                    throw new ApiException(404, "Không tìm thấy ảnh trong album.", "photo_not_found");
                }

                var now = DateTimeOffset.UtcNow;
                var existing = album.Selections.FirstOrDefault(selection => selection.PhotoId == photo.Id);
                if (existing is null)
                {
                    album.Selections.Add(new PhotoSelection
                    {
                        PhotoId = photo.Id,
                        SelectedAt = now
                    });
                }
                else
                {
                    album.Selections.Remove(existing);
                }

                album.FavoritesUpdatedAt = now;
                return new PublicFavoriteDto(
                    album.Selections.Select(selection => selection.PhotoId).ToList(),
                    album.Selections.Count,
                    album.SubmittedAt);
            });

            return Results.Ok(response);
        });

        group.MapPost("/{token}/submit", (string token, SubmitSelectionRequest request, DataStore store) =>
        {
            var note = AlbumText.TrimTo(request.Note, 600);
            var response = store.Write(data =>
            {
                var album = FindPublicAlbum(data, token);
                if (!album.AllowFavorites)
                {
                    throw new ApiException(403, "Album này không nhận lựa chọn ảnh.", "favorites_disabled");
                }

                if (album.Selections.Count == 0)
                {
                    throw new ApiException(
                        400,
                        "Bạn cần thả tim ít nhất một ảnh trước khi gửi lựa chọn.",
                        "empty_selection");
                }

                var now = DateTimeOffset.UtcNow;
                album.SubmittedAt = now;
                album.ClientNote = note;
                album.FavoritesUpdatedAt = now;
                return new PublicSubmissionDto(now, note, album.Selections.Count);
            });

            return Results.Ok(response);
        });
    }

    private static Album FindPublicAlbum(AppData data, string token)
    {
        var album = data.Albums.FirstOrDefault(item =>
            string.Equals(item.ShareToken, token, StringComparison.OrdinalIgnoreCase));

        if (album is null || !album.IsPublished)
        {
            throw new ApiException(
                404,
                "Album không tồn tại hoặc chưa được chia sẻ. Vui lòng liên hệ studio để nhận liên kết mới.",
                "album_not_found");
        }

        return album;
    }
}
