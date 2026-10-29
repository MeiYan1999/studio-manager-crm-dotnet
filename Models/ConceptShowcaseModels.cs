namespace StudioCore.Models;

public sealed record ConceptCategory(string Id, string Title, string Description);
public sealed record PublicConceptCategory(string Id, string Title, string Description, string? CoverUrl, int ConceptCount, int PhotoCount);
public sealed record ConceptGalleryPhoto(string ThumbnailUrl, string Url, string Alt);
public sealed record PublicConcept(string Id, string Name, string Description, List<ConceptGalleryPhoto> Photos);
public sealed record PublicConceptGallery(string Id, string Title, string Description, List<PublicConcept> Concepts);
