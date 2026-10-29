using System.Text.Json;
using StudioCore.Models;
using Xunit;

namespace StudioCore.Tests;
public class PriceCatalogTests
{
    [Fact]
    public void LegacyDataGetsDefaultsAndEditsSurviveRoundTrip()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var data = JsonSerializer.Deserialize<AppData>("{}", options)!;
        Assert.Equal(5, data.PriceCatalog.Count);
        Assert.Equal(27, data.PriceCatalog.Sum(g => g.Items.Count));
        data.PriceCatalog[0].Items[0].PriceText = "Đã sửa";
        data.PriceCatalog[0].Items[0].Visible = false;
        var restored = JsonSerializer.Deserialize<AppData>(JsonSerializer.Serialize(data, options), options)!;
        Assert.Equal("Đã sửa", restored.PriceCatalog[0].Items[0].PriceText);
        Assert.False(restored.PriceCatalog[0].Items[0].Visible);
    }
}
