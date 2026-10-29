using War3Engine.Assets;
using War3Engine.Core;
using War3Engine.Core.Map;
using Xunit;

namespace War3Engine.Tests;

public class MapSceneTests
{
    private static (AssetStore Store, MapSceneData Scene)? TryLoad()
    {
        var settings = EngineSettings.Load();
        if (!File.Exists(settings.MapPath))
        {
            Console.WriteLine($"SKIPPED (map not found): {settings.MapPath}");
            return null;
        }

        AssetStore store;
        try
        {
            store = new AssetStore(settings.WarcraftPath);
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or FileNotFoundException)
        {
            Console.WriteLine($"SKIPPED (no game data): {ex.Message}");
            return null;
        }

        var scene = MapSceneLoader.Load(store, settings.MapPath);
        return (store, scene);
    }

    [Fact]
    public void TerrainBuildsCompleteGridWithRecoveredTextures()
    {
        var loaded = TryLoad();
        if (loaded is null)
        {
            return;
        }

        using var store = loaded.Value.Store;
        var scene = loaded.Value.Scene;
        var terrain = scene.Terrain;

        // Fixture map is 64x96 tiles with a 65x97 vertex grid.
        Assert.Equal(64 * 96, terrain.TileCount);
        Assert.Equal(0, terrain.Positions.Length % 6);
        Assert.Equal(7, terrain.TextureGroups.Count(g => g.Layer == 0));
        Assert.True(terrain.TextureGroups.Length >= 7, "splat layers missing");

        var baseTiles = terrain.TextureGroups.Where(g => g.Layer == 0).Sum(g => g.IndexCount / 6);
        Assert.Equal(terrain.TileCount, baseTiles);
        var baseTriangles = baseTiles * 2;
        Assert.True(terrain.Indices.Length / 3 > baseTriangles, "expected blended splat layers");

        foreach (var group in terrain.TextureGroups)
        {
            Assert.False(string.IsNullOrEmpty(group.TexturePath));
            Assert.True(store.Exists(group.TexturePath), $"missing terrain texture {group.TexturePath}");
        }

        Console.WriteLine($"terrain: tiles={terrain.TileCount} verts={terrain.Positions.Length} groups={terrain.TextureGroups.Length} bounds={terrain.Bounds.Minimum}..{terrain.Bounds.Maximum}");
    }

    [Fact]
    public void DoodadsResolveToModelsIncludingVariationsAndReplaceableTextures()
    {
        var loaded = TryLoad();
        if (loaded is null)
        {
            return;
        }

        using var store = loaded.Value.Store;
        var scene = loaded.Value.Scene;

        Assert.Equal(54, scene.TotalDoodads);
        Assert.True(scene.Placements.Length == 13, $"placements={scene.Placements.Length} unresolved={scene.UnresolvedModels} map={EngineSettings.Load().MapPath} types={string.Join(",", scene.Placements.Select(p => p.TypeId))}");
        Assert.True(scene.UnresolvedModels <= 1, $"unexpected unresolved doodads: {scene.UnresolvedModels}");

        var tree = scene.Placements.First(p => p.TypeId == "ATtr");
        Assert.EndsWith(".mdx", tree.ModelPath);
        Assert.Contains("AshenTree", tree.ModelPath);
        Assert.Equal(32, tree.ReplaceableId);
        Assert.NotNull(tree.ReplaceableTexture);
        Assert.True(store.Exists(tree.ReplaceableTexture!), $"missing tree texture {tree.ReplaceableTexture}");

        Assert.Contains(scene.Placements, p => p.TypeId == "LOca");
        Assert.Contains(scene.Placements, p => p.TypeId == "LWw0");
        Console.WriteLine($"placements: {string.Join(",", scene.Placements.GroupBy(p => p.TypeId).Select(g => $"{g.Key}={g.Count()}"))}");
    }

    [Fact]
    public void TerrainHeightsMatchDoodadPlacementTiers()
    {
        var loaded = TryLoad();
        if (loaded is null)
        {
            return;
        }

        using var store = loaded.Value.Store;
        var scene = loaded.Value.Scene;

        // The map is a multi-tier arena; converted heights must contain the expected tiers.
        var heights = scene.Terrain.Positions.Select(p => p.Y).Distinct().OrderBy(h => h).ToArray();
        Assert.True(heights.Length >= 2, "terrain is unexpectedly flat");
        Assert.InRange(heights.Min(), -600f, -100f);
        Assert.InRange(heights.Max(), -100f, 200f);
        Console.WriteLine($"distinct terrain heights (first 10): {string.Join(",", heights.Take(10))}");
    }
}
