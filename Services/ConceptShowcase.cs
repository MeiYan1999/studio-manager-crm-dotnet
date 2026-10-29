using StudioCore.Models;

namespace StudioCore.Services;

public static class ConceptShowcase
{
    public static readonly IReadOnlyList<ConceptCategory> Categories = new List<ConceptCategory>
    {
        new("family", "Chụp ảnh gia đình", "Lưu giữ khoảnh khắc sum vầy."),
        new("children", "Chụp ảnh trẻ em", "Ghi dấu từng giai đoạn trưởng thành của con yêu."),
        new("newborn", "Chụp ảnh sơ sinh", "Ôm trọn những yêu thương đầu đời."),
        new("maternity", "Chụp ảnh mẹ bầu", "Khi tình yêu thương nảy mầm trước cả lần gặp gỡ đầu tiên."),
        new("beauty", "Chụp ảnh beauty", "Tự tin, rạng rỡ trong phiên bản đẹp nhất của chính bạn.")
    }.AsReadOnly();

    public static (string Category, string AlbumId) ValidatePublication(AppData data, string? category, string? albumId, bool published)
    {
        var key = (category ?? "").Trim().ToLowerInvariant();
        var id = (albumId ?? "").Trim();
        if (key.Length > 0 && !Categories.Any(item => item.Id == key))
            throw new ApiException(400, "Nhóm concept chưa hợp lệ.", "invalid_concept_category");
        var album = data.Albums.FirstOrDefault(item => item.Id == id);
        if (id.Length > 0 && album is null)
            throw new ApiException(400, "Không tìm thấy album ảnh của concept.", "invalid_concept_album");
        if (published && (key.Length == 0 || album is null || !album.IsPublished || album.Photos.Count == 0))
            throw new ApiException(400, "Chọn nhóm concept và album đã chia sẻ có ảnh trước khi công khai.", "invalid_concept_publication");
        return (key, id);
    }

    private static IEnumerable<(StudioConcept Concept, Album Album)> Visible(AppData data, string category)
    {
        foreach (var concept in data.Concepts.Where(item => item.IsPublished && item.Category == category && item.Status == ConceptStatuses.Ready))
        {
            var album = data.Albums.FirstOrDefault(item => item.Id == concept.AlbumId && item.IsPublished && item.Photos.Count > 0);
            if (album is not null) yield return (concept, album);
        }
    }

    public static List<PublicConceptCategory> List(AppData data) => Categories.Select(category =>
    {
        var visible = Visible(data, category.Id).ToList();
        var first = visible.FirstOrDefault().Album;
        var cover = first?.Photos.FirstOrDefault(photo => photo.Id == first.CoverPhotoId) ?? first?.Photos.FirstOrDefault();
        return new PublicConceptCategory(category.Id, category.Title, category.Description,
            cover is null ? null : (cover.PreviewUrl.Length > 0 ? cover.PreviewUrl : cover.ThumbUrl),
            visible.Count, visible.Sum(item => item.Album.Photos.Count));
    }).ToList();

    public static PublicConceptGallery Gallery(AppData data, string category)
    {
        var definition = Categories.FirstOrDefault(item => item.Id == category)
            ?? throw new ApiException(404, "Không tìm thấy nhóm concept.", "concept_category_not_found");
        var concepts = Visible(data, category).Select(item => new PublicConcept(
            item.Concept.Id, item.Concept.Name, item.Concept.Description,
            item.Album.Photos.Select((photo, index) => new ConceptGalleryPhoto(
                photo.ThumbUrl.Length > 0 ? photo.ThumbUrl : photo.PreviewUrl,
                photo.PreviewUrl.Length > 0 ? photo.PreviewUrl : photo.ThumbUrl,
                $"{item.Concept.Name} — ảnh {index + 1}")).ToList())).ToList();
        return new PublicConceptGallery(definition.Id, definition.Title, definition.Description, concepts);
    }
}
