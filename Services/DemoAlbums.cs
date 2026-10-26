using MayAlbum.Models;

namespace MayAlbum.Services;

internal static class DemoAlbums
{
    private sealed record DemoPhoto(string Code, string Asset, int Width, int Height);

    public static AppData Create()
    {
        var now = DateTimeOffset.UtcNow;
        var data = new AppData
        {
            Settings = new StudioSettings
            {
                StudioName = "Mây Studio",
                ContactEmail = "hello@may.studio"
            }
        };

        var gardenPhotos = new[]
        {
            new DemoPhoto("LM-001", "01", 1600, 1067),
            new DemoPhoto("LM-002", "03", 1600, 1067),
            new DemoPhoto("LM-003", "13", 1600, 1067),
            new DemoPhoto("LM-004", "14", 1600, 1068),
            new DemoPhoto("LM-005", "10", 1600, 2400),
            new DemoPhoto("LM-006", "04", 1600, 1067),
            new DemoPhoto("LM-007", "08", 1600, 2400),
            new DemoPhoto("LM-008", "18", 1600, 1068),
            new DemoPhoto("LM-009", "25", 1600, 1067),
            new DemoPhoto("LM-010", "06", 1600, 2400),
            new DemoPhoto("LM-011", "15", 1600, 1067),
            new DemoPhoto("LM-012", "16", 1600, 1067),
            new DemoPhoto("LM-013", "11", 1600, 2400),
            new DemoPhoto("LM-014", "02", 1600, 1068),
            new DemoPhoto("LM-015", "20", 1600, 1067),
            new DemoPhoto("LM-016", "19", 1600, 1068),
            new DemoPhoto("LM-017", "22", 1600, 2400),
            new DemoPhoto("LM-018", "05", 1600, 1067),
            new DemoPhoto("LM-019", "24", 1600, 2400),
            new DemoPhoto("LM-020", "23", 1600, 1068)
        };

        var seaPhotos = new[]
        {
            new DemoPhoto("HN-001", "07", 1600, 2400),
            new DemoPhoto("HN-002", "12", 1600, 1067),
            new DemoPhoto("HN-003", "09", 1600, 1067),
            new DemoPhoto("HN-004", "17", 1600, 1067),
            new DemoPhoto("HN-005", "01", 1600, 1067),
            new DemoPhoto("HN-006", "23", 1600, 1068),
            new DemoPhoto("HN-007", "28", 1600, 1064),
            new DemoPhoto("HN-008", "31", 1600, 1036),
            new DemoPhoto("HN-009", "33", 1600, 1067),
            new DemoPhoto("HN-010", "30", 1600, 1067),
            new DemoPhoto("HN-011", "35", 1600, 1067),
            new DemoPhoto("HN-012", "36", 1600, 1983)
        };

        var garden = new Album
        {
            Id = "demo-ngay-chung-doi",
            ShareToken = "demo-ngay-chung-doi",
            Title = "Ngày Mình Chung Đôi",
            ClientName = "Linh & Minh",
            EventDate = "2026-09-20",
            Location = "Đà Lạt",
            Description =
                "Một buổi sáng Đà Lạt nhiều sương, vài tia nắng đầu ngày, và câu chuyện của hai người vừa bắt đầu. " +
                "Cảm ơn Linh & Minh đã tin tưởng để Mây lưu lại ngày thật đẹp này.",
            AllowFavorites = true,
            AllowDownloads = true,
            IsPublished = true,
            IsDemo = true,
            CreatedAt = now.AddDays(-24),
            UpdatedAt = now.AddHours(-5),
            FavoritesUpdatedAt = now.AddHours(-5),
            Photos = BuildPhotos("demo-ngay-chung-doi", gardenPhotos, now.AddDays(-20)),
        };

        garden.CoverPhotoId = ByCode(garden, "LM-010").Id;
        MarkSelected(garden, now.AddHours(-5), "LM-002", "LM-008", "LM-010", "LM-013");

        var sea = new Album
        {
            Id = "demo-bien-nho",
            ShareToken = "demo-bien-nho",
            Title = "Biển Nhớ",
            ClientName = "Hà & Nam",
            EventDate = "2026-06-14",
            Location = "Phú Quốc",
            Description =
                "Nắng, gió và tiếng sóng. Một ngày thật dài ở Phú Quốc, nơi chỉ cần hai người ở cạnh nhau là đủ. " +
                "Cảm ơn Hà & Nam vì đã cùng Mây đi hết đường bờ biển.",
            AllowFavorites = true,
            AllowDownloads = false,
            IsPublished = true,
            IsDemo = true,
            CreatedAt = now.AddDays(-47),
            UpdatedAt = now.AddDays(-2),
            FavoritesUpdatedAt = now.AddDays(-2),
            SubmittedAt = now.AddDays(-2),
            ClientNote = "Tụi mình thích nhất những tấm có biển và nắng chiều ạ. Cảm ơn studio rất nhiều!",
            Photos = BuildPhotos("demo-bien-nho", seaPhotos, now.AddDays(-45)),
        };

        sea.CoverPhotoId = ByCode(sea, "HN-001").Id;
        MarkSelected(sea, now.AddDays(-3), "HN-001", "HN-004", "HN-010");

        data.Albums.Add(garden);
        data.Albums.Add(sea);
        data.Board = DemoBoard.Create();
        return data;
    }

    private static List<Photo> BuildPhotos(string albumId, DemoPhoto[] source, DateTimeOffset takenAt)
    {
        var result = new List<Photo>(source.Length);
        for (var index = 0; index < source.Length; index++)
        {
            var item = source[index];
            var thumb = $"/assets/img/demo/thumbs/{item.Asset}.jpg";
            var preview = $"/assets/img/demo/{item.Asset}.jpg";
            result.Add(new Photo
            {
                Id = $"{albumId}-p{index + 1:00}",
                Code = item.Code,
                FileName = $"{item.Code}.jpg",
                ThumbUrl = thumb,
                PreviewUrl = preview,
                DownloadUrl = preview,
                Width = item.Width,
                Height = item.Height,
                SizeBytes = 0,
                TakenAt = takenAt
            });
        }

        return result;
    }

    private static Photo ByCode(Album album, string code) =>
        album.Photos.First(photo => string.Equals(photo.Code, code, StringComparison.OrdinalIgnoreCase));

    private static void MarkSelected(Album album, DateTimeOffset selectedAt, params string[] codes)
    {
        foreach (var code in codes)
        {
            album.Selections.Add(new PhotoSelection
            {
                PhotoId = ByCode(album, code).Id,
                SelectedAt = selectedAt
            });
        }
    }
}
