namespace StudioCore.Models;
public sealed class PriceGroup
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public List<PriceItem> Items { get; set; } = [];
}
public sealed class PriceItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string PriceText { get; set; } = "";
    public string Benefits { get; set; } = "";
    public bool Visible { get; set; } = true;
}
