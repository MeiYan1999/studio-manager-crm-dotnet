using System.Text.Json;
using StudioCore.Models;
using StudioCore.Services;
using Xunit;

namespace StudioCore.Tests;

public class ConceptShowcaseTests
{
    private static AppData Sample()
    {
        var data = new AppData();
        data.Albums.Add(new Album
        {
            Id = "album", IsPublished = true, ShareToken = "private-share-token",
            ClientName = "Private client", ClientNote = "Private note",
            Photos = [new Photo { Id = "photo", ThumbUrl = "/thumb.jpg", PreviewUrl = "/photo.jpg" }]
        });
        data.Concepts.Add(new StudioConcept
        {
            Id = "concept", Name = "Garden", Category = "family", AlbumId = "album", IsPublished = true,
            Fee = 987654m
        });
        return data;
    }

    [Fact]
    public void ExistingInternalConceptIsNotPublishedByDefault()
    {
        var old = JsonSerializer.Deserialize<StudioConcept>("""{"name":"Existing","fee":500000}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.False(old.IsPublished);
        Assert.Equal("", old.AlbumId);
        Assert.Equal("", old.Category);
        var data = Sample();
        data.Concepts[0].IsPublished = false;
        Assert.Empty(ConceptShowcase.Gallery(data, "family").Concepts);
        Assert.All(ConceptShowcase.List(data), category => Assert.Equal(0, category.PhotoCount));
    }

    [Fact]
    public void PublicGalleryOmitsClientDataShareTokensAndFees()
    {
        var data = Sample();
        var gallery = ConceptShowcase.Gallery(data, "family");
        Assert.Equal("/photo.jpg", Assert.Single(Assert.Single(gallery.Concepts).Photos).Url);
        var json = JsonSerializer.Serialize(gallery);
        Assert.DoesNotContain("Private client", json);
        Assert.DoesNotContain("Private note", json);
        Assert.DoesNotContain("private-share-token", json);
        Assert.DoesNotContain("987654", json);
        Assert.Empty(ConceptShowcase.Gallery(data, "children").Concepts);
    }

    [Fact]
    public void UnsharingDeletingOrMaintainingSourceHidesItsPhotos()
    {
        var data = Sample();
        data.Albums[0].IsPublished = false;
        Assert.Empty(ConceptShowcase.Gallery(data, "family").Concepts);
        data.Albums[0].IsPublished = true;
        data.Concepts[0].Status = ConceptStatuses.Maintenance;
        Assert.Empty(ConceptShowcase.Gallery(data, "family").Concepts);
        data.Concepts[0].Status = ConceptStatuses.Ready;
        data.Albums.Clear();
        Assert.Empty(ConceptShowcase.Gallery(data, "family").Concepts);
        Assert.Null(ConceptShowcase.List(data).First(item => item.Id == "family").CoverUrl);
    }

    [Fact]
    public void PublicationRequiresKnownCategoryAndSharedNonemptyAlbum()
    {
        var data = Sample();
        Assert.Throws<ApiException>(() => ConceptShowcase.ValidatePublication(data, "unknown", "album", true));
        Assert.Throws<ApiException>(() => ConceptShowcase.ValidatePublication(data, "family", "missing", true));
        data.Albums[0].IsPublished = false;
        Assert.Throws<ApiException>(() => ConceptShowcase.ValidatePublication(data, "family", "album", true));
        data.Albums[0].IsPublished = true;
        data.Albums[0].Photos.Clear();
        Assert.Throws<ApiException>(() => ConceptShowcase.ValidatePublication(data, "family", "album", true));
    }
}
