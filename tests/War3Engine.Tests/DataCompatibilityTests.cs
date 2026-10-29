using War3Engine.Assets;
using War3Engine.Core;
using Xunit;

namespace War3Engine.Tests;

public class DataCompatibilityTests
{
    private static int Code(string id) => id[0] | (id[1] << 8) | (id[2] << 16) | (id[3] << 24);

    [Fact]
    public void BaseSlkAndCustomObjectDataResolveInOrder()
    {
        var settings = EngineSettings.Load();
        if (!File.Exists(settings.YdweMapPath))
        {
            Console.WriteLine("SKIPPED (YDWE map not found)");
            return;
        }

        using var store = new AssetStore(settings.WarcraftPath);
        var provider = JassSlkQueryProvider.Load(store, settings.YdweMapPath);

        // Base game SLK row.
        Assert.Equal("Artifact", provider.Query("item", Code("ckng"), "class"));
        Assert.Equal(126, provider.Query("item", Code("ckng"), "prio"));

        // Custom object-data row (w3t) overrides the base item.
        Assert.Equal("Purchasable", provider.Query("item", Code("I04A"), "class"));
        Assert.Equal(67, provider.Query("item", Code("I04A"), "prio"));
        Assert.Equal("Hút máu mặt nạ lv.8", provider.Query("item", Code("I04A"), "Name"));
        Assert.False(string.IsNullOrEmpty((string?)provider.Query("item", Code("I04A"), "Ubertip")));

        // Ubertip comes from utub; TRIGSTR values resolve through war3map.wts when present.
        var i01o = (string?)provider.Query("item", Code("I01O"), "Ubertip");
        Assert.False(string.IsNullOrEmpty(i01o));
        Assert.True(!i01o!.Contains("TRIGSTR_", StringComparison.Ordinal), $"i01o={i01o} wts1={provider.DebugWts(1)} wts0={provider.DebugWts(0)} count={provider.WtsEntries}");

        // I033 references TRIGSTR_000, which war3map.wts does not define; the raw reference
        // is returned rather than fabricating text.
        Assert.Equal("TRIGSTR_000", provider.Query("item", Code("I033"), "Ubertip"));
        Assert.Equal("TRIGSTR_002", provider.Query("item", Code("I010"), "Ubertip"));
        Assert.Contains("TRIGSTR_000", provider.UnresolvedTriggerStrings);
        Assert.Contains("TRIGSTR_002", provider.UnresolvedTriggerStrings);

        // Custom unit inherits weapsOn from its base unit (U00N <- Udea).
        Assert.Equal(
            provider.Query("unit", Code("Udea"), "weapsOn"),
            provider.Query("unit", Code("U00N"), "weapsOn"));

        Assert.True(provider.CustomFieldQueries > 0);
        Assert.True(provider.CustomFieldHits > 0);
        Assert.True(provider.WtsEntries > 0);
        Console.WriteLine($"data: customQueries={provider.CustomFieldQueries} customHits={provider.CustomFieldHits} wts={provider.WtsEntries} missing={provider.MissingFields.Count}");
    }
}
