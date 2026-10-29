using War3Engine.Assets;
using War3Engine.Compat;
using War3Engine.Core;
using Xunit;

namespace War3Engine.Tests;

public class CompatibilityTests
{
    [Fact]
    public void ClassifiesDeclaredNativesByProviderAndCountsCallSites()
    {
        const string script = """
            //globals from DzAPI:
            //endglobals from DzAPI
            //library YDWEBase
            native DzCreateFrame takes string frame, integer parent, integer id returns integer
            native EXGetUnitAbility takes unit u, integer abilcode returns ability
            native RequestExtraIntegerData takes integer dataType returns integer
            native CustomThing takes nothing returns nothing
            function Init takes nothing returns nothing
                local integer f = DzCreateFrame("x", 0, 0)
                call EXGetUnitAbility(null, 0)
                call RequestExtraIntegerData(1)
                set f = DzCreateFrame("y", 0, 0)
            endfunction
            """;

        var inventory = JassScriptInventory.Parse(script);
        Assert.Equal(4, inventory.Natives.Count);

        var byName = inventory.Natives.ToDictionary(n => n.Name);
        Assert.Equal(NativeProviders.DzApi, byName["DzCreateFrame"].Provider);
        Assert.Equal(2, byName["DzCreateFrame"].CallCount);
        Assert.Equal(NativeProviders.Japi, byName["EXGetUnitAbility"].Provider);
        Assert.Equal(1, byName["EXGetUnitAbility"].CallCount);
        Assert.Equal(NativeProviders.Extra, byName["RequestExtraIntegerData"].Provider);
        Assert.Equal(NativeProviders.Unknown, byName["CustomThing"].Provider);
        Assert.Equal(0, byName["CustomThing"].CallCount);

        Assert.Contains(inventory.Libraries, l => l.Name == "DzAPI");
        Assert.Contains(inventory.Libraries, l => l.Name == "YDWEBase");
    }

    [Fact]
    public void DetectsLocalYdweComponents()
    {
        var settings = EngineSettings.Load();
        if (!Directory.Exists(settings.WarcraftPath))
        {
            Console.WriteLine("SKIPPED (no game install)");
            return;
        }

        var components = YdweInstallation.Detect(settings.WarcraftPath);
        var game = components.FirstOrDefault(c => c.Name == "War3.exe");
        Assert.NotNull(game);
        Assert.StartsWith("1.27.0.52240", game!.Version);

        if (File.Exists(Path.Combine(settings.WarcraftPath, "dz_w3_plugin.dll")))
        {
            Assert.Contains(components, c => c.Name == "dz_w3_plugin.dll" && c.Kind == "runtime-plugin");
        }

        var ydweConfig = components.FirstOrDefault(c => c.Name == "YDWEConfig");
        if (ydweConfig is not null)
        {
            Assert.Contains(ydweConfig.Version, new[] { "2, 0, 0, 2485", "2.0.0.2485" });
        }

        Console.WriteLine($"detected {components.Count} components: {string.Join(", ", components.Select(c => $"{c.Name}={c.Version}"))}");
    }

    [Fact]
    public void YdweMapDependencyInventoryIsComplete()
    {
        var settings = EngineSettings.Load();
        if (!File.Exists(settings.YdweMapPath))
        {
            Console.WriteLine($"SKIPPED (YDWE map not found): {settings.YdweMapPath}");
            return;
        }

        using var store = new AssetStore(settings.WarcraftPath);
        store.MountMap(settings.YdweMapPath);
        var bytes = store.ReadAllBytes("war3map.j", out var source);
        Assert.Equal("map", source);
        var script = System.Text.Encoding.UTF8.GetString(bytes);
        var inventory = JassScriptInventory.Parse(script);

        Assert.Equal(265, inventory.Natives.Count);
        Assert.Equal(69, inventory.Natives.Count(n => n.CallCount > 0));
        Assert.Equal(229, inventory.Natives.Count(n => n.Provider == NativeProviders.DzApi));
        Assert.Equal(31, inventory.Natives.Count(n => n.Provider == NativeProviders.Japi));
        Assert.Equal(5, inventory.Natives.Count(n => n.Provider == NativeProviders.Extra));

        Assert.Contains(inventory.Libraries, l => l.Name == "DzAPI");
        Assert.Contains(inventory.Libraries, l => l.Name == "BzAPI");
        Assert.Contains(inventory.Libraries, l => l.Name == "LBKKAPI");
        Assert.Contains(inventory.Libraries, l => l.Name == "YDWEBase");
        Assert.Contains(inventory.Libraries, l => l.Name == "YDWETimerSystem");

        Console.WriteLine($"YDWE map: {inventory.Natives.Count} extension natives declared, {inventory.Natives.Count(n => n.CallCount > 0)} called, {inventory.Libraries.Count} libraries");
    }
}
