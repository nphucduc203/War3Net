using System.Numerics;
using War3Engine.Assets;
using War3Engine.Core;
using War3Engine.Core.Animation;
using War3Engine.Core.Models;
using Xunit;

namespace War3Engine.Tests;

public class RealAssetTests
{
    private const string Footman = @"units\human\footman\footman.mdx";

    private static AssetStore? TryOpenStore()
    {
        var settings = EngineSettings.Load();
        try
        {
            return new AssetStore(settings.WarcraftPath);
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or FileNotFoundException)
        {
            Console.WriteLine($"SKIPPED (no local game data): {ex.Message}");
            return null;
        }
    }

    [Fact]
    public void FootmanModelParsesWithValidSkinning()
    {
        using var store = TryOpenStore();
        if (store is null)
        {
            return;
        }

        var model = MdxModelLoader.Load(store, Footman);
        Assert.Equal(800, model.FormatVersion);
        Assert.True(model.Geosets.Length > 0, "no geosets");
        Assert.True(model.Nodes.Length > 0, "no nodes");
        Assert.True(model.Sequences.Length > 0, "no sequences");
        Assert.Contains(model.Sequences, s => s.Name.StartsWith("Stand", StringComparison.Ordinal));
        Assert.Contains(model.Sequences, s => s.Name.Contains("Walk", StringComparison.Ordinal));

        var triangles = 0;
        foreach (var geoset in model.Geosets)
        {
            triangles += geoset.Indices.Length / 3;
            for (var v = 0; v < geoset.Positions.Length; v++)
            {
                var weights = geoset.BoneWeights[v];
                Assert.Equal(1f, weights.X + weights.Y + weights.Z + weights.W, 3);
                var indices = geoset.BoneIndices[v];
                Assert.InRange(indices.X, 0, model.Nodes.Length - 1);
                Assert.InRange(indices.Y, 0, model.Nodes.Length - 1);
            }
        }

        Console.WriteLine($"footman: nodes={model.Nodes.Length} geosets={model.Geosets.Length} triangles={triangles} sequences={model.Sequences.Length}");
        Assert.True(triangles > 100, $"unexpectedly small footman: {triangles} triangles");

        // Pivots are indexed by node object id; a naive array-index assignment leaves
        // hand pivots at origin and detaches limbs when animated.
        var hand = model.Nodes.First(n => n.Name.StartsWith("Hand Right Ref"));
        Assert.True(hand.Pivot.Length() > 10f, $"hand pivot suspiciously close to origin: {hand.Pivot}");
        Assert.All(model.Nodes, node => Assert.True(node.ParentIndex < model.Nodes.Length));
        Assert.Contains(model.Nodes, n => n.Name.Contains("Bone_Hand"));
    }

    [Fact]
    public void FootmanAnimatedPoseDiffersFromRestPose()
    {
        using var store = TryOpenStore();
        if (store is null)
        {
            return;
        }

        var model = MdxModelLoader.Load(store, Footman);
        var walk = model.FindSequence("Walk");
        Assert.NotNull(walk);

        var rest = PoseEvaluator.ComputeNodeWorlds(model, walk!.StartFrame, 0f);
        var later = PoseEvaluator.ComputeNodeWorlds(model, walk.StartFrame + ((walk.EndFrame - walk.StartFrame) / 2f), 0f);

        var maxDelta = 0f;
        for (var i = 0; i < rest.Length; i++)
        {
            maxDelta = MathF.Max(maxDelta, (rest[i].Translation - later[i].Translation).Length());
            maxDelta = MathF.Max(maxDelta, MathF.Abs(rest[i].M11 - later[i].M11));
            maxDelta = MathF.Max(maxDelta, MathF.Abs(rest[i].M22 - later[i].M22));
            maxDelta = MathF.Max(maxDelta, MathF.Abs(rest[i].M33 - later[i].M33));
        }

        Console.WriteLine($"walk max node-matrix delta between t0 and t50%: {maxDelta:F4}");
        Assert.True(maxDelta > 0.01f, "walk animation does not deform any node");
    }

    [Fact]
    public void TeamColorBlpDecodesWithVariance()
    {
        using var store = TryOpenStore();
        if (store is null)
        {
            return;
        }

        var teamBytes = store.ReadAllBytes(@"ReplaceableTextures\TeamColor\TeamColor00.blp", out var teamSource);
        var team = BlpDecoder.Decode(teamBytes);
        Assert.True(team.Width >= 8 && team.Height >= 8);
        Assert.Equal(team.Width * team.Height * 4, team.Rgba.Length);
        Assert.Contains(Enumerable.Range(0, team.Width * team.Height), i => team.Rgba[(i * 4) + 3] > 0);
        Console.WriteLine($"TeamColor00 <- {teamSource}: {team.Width}x{team.Height} firstPixel={team.Rgba[0]},{team.Rgba[1]},{team.Rgba[2]},{team.Rgba[3]}");

        // A real unit texture must produce many distinct colors (validates palette/DXT decoding).
        var skinBytes = store.ReadAllBytes(@"Textures\Footman.blp", out var skinSource);
        var skin = BlpDecoder.Decode(skinBytes);
        var distinct = new HashSet<uint>();
        for (var i = 0; i < skin.Rgba.Length; i += 4)
        {
            distinct.Add(BitConverter.ToUInt32(skin.Rgba, i));
        }

        Console.WriteLine($"Footman.blp <- {skinSource}: {skin.Width}x{skin.Height} distinctColors={distinct.Count}");
        Assert.True(distinct.Count > 100, $"footman texture looks broken: {distinct.Count} unique colors");
    }

    [Fact]
    public void FootmanTexturesResolveFromArchives()
    {
        using var store = TryOpenStore();
        if (store is null)
        {
            return;
        }

        var model = MdxModelLoader.Load(store, Footman);
        var resolved = 0;
        foreach (var texture in model.Textures)
        {
            var path = texture.ReplaceableId switch
            {
                1 => @"ReplaceableTextures\TeamColor\TeamColor00.blp",
                2 => @"ReplaceableTextures\TeamGlow\TeamGlow00.blp",
                _ => texture.Path,
            };

            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            if (store.TryOpen(path, out var stream))
            {
                stream.Dispose();
                resolved++;
            }
            else
            {
                Console.WriteLine($"UNRESOLVED: {path}");
            }
        }

        Assert.True(resolved > 0, "no footman textures resolved");
    }

    [Fact]
    public void TestMapMountsAndExposesCoreFiles()
    {
        var settings = EngineSettings.Load();
        if (!File.Exists(settings.MapPath))
        {
            Console.WriteLine($"SKIPPED (map not found): {settings.MapPath}");
            return;
        }

        using var store = TryOpenStore();
        if (store is null)
        {
            return;
        }

        store.MountMap(settings.MapPath);
        foreach (var name in new[] { "war3map.w3i", "war3map.w3e", "war3mapUnits.doo", "war3map.doo" })
        {
            Assert.True(store.Exists(name), $"missing map file: {name}");
        }

        // This fixture is a protected map: the JASS script may be absent while trigger
        // metadata (wtg/wct) still exists. Record which script form is present instead of assuming.
        var hasJass = store.Exists("war3map.j");
        var hasLua = store.Exists("war3map.lua");
        Console.WriteLine($"map script: jass={hasJass} lua={hasLua}");

        var names = store.ListFiles().ToList();
        Console.WriteLine($"map listfile entries: {names.Count}");
        Assert.True(names.Count > 10);
    }
}
