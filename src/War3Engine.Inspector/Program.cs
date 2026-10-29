using System.Numerics;
using War3Engine.Assets;
using War3Engine.Core;
using War3Engine.Compat;
using War3Engine.Core.Models;
using War3Net.Common.Extensions;

namespace War3Engine.Inspector;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            var settings = EngineSettings.Load();
            using var store = new AssetStore(settings.WarcraftPath);

            switch (args[0])
            {
                case "assets":
                    return ListAssets(store, args.ElementAtOrDefault(1));
                case "model":
                    return InspectModel(store, args.ElementAtOrDefault(1) ?? @"units\human\footman\footman.mdx");
                case "blp":
                    return InspectBlp(store, args.ElementAtOrDefault(1) ?? @"ReplaceableTextures\TeamColor\TeamColor00.blp");
                case "map":
                    return InspectMap(store, args.ElementAtOrDefault(1) ?? settings.MapPath);
                case "mapdata":
                    return InspectMapData(args.ElementAtOrDefault(1) ?? settings.MapPath);
                case "compat":
                    return InspectCompatibility(settings, store, args.ElementAtOrDefault(1), args.ElementAtOrDefault(2));
                case "extract":
                    if (File.Exists(settings.MapPath))
                    {
                        store.MountMap(settings.MapPath);
                    }

                    return Extract(store, args[1], args[2]);
                case "pose":
                    return InspectPose(store, args[1], args[2], float.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture));
                case "slk":
                    return InspectSlk(store, args[1], args.ElementAtOrDefault(2) ?? "");
                case "mpq":
                    return InspectMpq(args[1], args.ElementAtOrDefault(2), args.ElementAtOrDefault(3));
                default:
                    PrintUsage();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            if (Environment.GetEnvironmentVariable("WAR3ENGINE_DEBUG") == "1")
            {
                Console.Error.WriteLine(ex);
            }

            return 1;
        }
    }

    private static int InspectCompatibility(EngineSettings settings, AssetStore store, string? mapPath, string? outputPath)
    {
        var target = mapPath
                     ?? (string.IsNullOrWhiteSpace(settings.YdweMapPath) ? settings.MapPath : settings.YdweMapPath);
        if (!File.Exists(target))
        {
            Console.WriteLine($"map not found: {target}");
            return 1;
        }

        store.MountMap(target);
        string script = string.Empty;
        var language = "none";
        if (store.TryOpen("war3map.j", out var jassStream))
        {
            using (jassStream)
            using (var reader = new StreamReader(jassStream, System.Text.Encoding.UTF8, true))
            {
                script = reader.ReadToEnd();
            }

            language = "jass";
        }
        else if (store.TryOpen("war3map.lua", out var luaStream))
        {
            using (luaStream)
            using (var reader = new StreamReader(luaStream, System.Text.Encoding.UTF8, true))
            {
                script = reader.ReadToEnd();
            }

            language = "lua";
        }

        var inventory = JassScriptInventory.Parse(script);
        if (language == "lua")
        {
            Console.WriteLine("lua scripts are inventoried only; JASS native scanning skipped");
        }

        var components = YdweInstallation.Detect(settings.WarcraftPath);
        var imports = ReadMapImports(store);
        var enabledProfiles = settings.EnableYdweCompat
            ? CompatibilityProfiles.All.Where(p => !p.IsDefault).Select(p => p.Id).ToArray()
            : Array.Empty<string>();

        var report = new MapDependencyReport
        {
            MapPath = target,
            MapBytes = new FileInfo(target).Length,
            GameVersion = YdweInstallation.GameVersion(settings.WarcraftPath),
            EnabledProfiles = enabledProfiles,
            Script = inventory,
            Components = components,
            Imports = imports,
        };

        var text = report.ToText();
        Console.Write(text);
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            File.WriteAllText(outputPath, text);
            Console.WriteLine($"report written: {outputPath}");
        }

        return 0;
    }

    private static List<MapImportStatus> ReadMapImports(AssetStore store)
    {
        var result = new List<MapImportStatus>();
        if (!store.TryOpen("war3map.imp", out var stream))
        {
            return result;
        }

        using (stream)
        using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
        {
            reader.ReadInt32();
            var count = reader.ReadInt32();
            for (var i = 0; i < count; i++)
            {
                reader.ReadByte();
                var path = ReadNullTerminatedString(reader);
                if (path.Length == 0)
                {
                    continue;
                }

                var extension = Path.GetExtension(path).ToLowerInvariant();
                result.Add(new MapImportStatus(path, store.Exists(path), extension));
            }
        }

        return result;
    }

    private static string ReadNullTerminatedString(BinaryReader reader)
    {
        var bytes = new List<byte>();
        while (true)
        {
            var value = reader.ReadByte();
            if (value == 0)
            {
                break;
            }

            bytes.Add(value);
        }

        return System.Text.Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static int InspectMpq(string archivePath, string? file, string? output)
    {
        using var archive = War3Net.IO.Mpq.MpqArchive.Open(archivePath, loadListFile: true);
        if (file is null)
        {
            Console.WriteLine($"archive: {archivePath} entries={archive.Count}");
            var names = archive.Select(e => e.FileName).Where(n => !string.IsNullOrEmpty(n)).OrderBy(n => n).ToList();
            Console.WriteLine("names: " + names.Count);
            foreach (var name in names.Where(n => n!.Contains("Water", StringComparison.OrdinalIgnoreCase) || n!.EndsWith(".slk", StringComparison.OrdinalIgnoreCase)).Take(30))
            {
                Console.WriteLine("  " + name);
            }

            return 0;
        }

        using var stream = archive.OpenFile(file);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var bytes = memory.ToArray();
        if (output is not null)
        {
            File.WriteAllBytes(output, bytes);
            Console.WriteLine($"{file}: {bytes.Length} bytes -> {output}");
        }
        else
        {
            Console.WriteLine($"{file}: {bytes.Length} bytes");
            Console.WriteLine(System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 2000)));
        }

        return 0;
    }

    private static int InspectSlk(AssetStore store, string path, string filter)
    {
        var bytes = store.ReadAllBytes(path, out var source);
        using var stream = new MemoryStream(bytes, writable: false);
        var table = new War3Net.IO.Slk.SylkParser().Parse(stream, leaveOpen: false);
        Console.WriteLine($"slk {path} <- {source}: rows={table.Rows + 1} columns={table.Columns + 1}");
        var headers = Enumerable.Range(0, table.Columns + 1).Select(c => Convert.ToString(table[c, 0]) ?? "").ToArray();
        Console.WriteLine("columns: " + string.Join(", ", headers));
        var printed = 0;
        foreach (var row in Enumerable.Range(1, table.Rows))
        {
            var id = Convert.ToString(table[0, row]) ?? "";
            if (filter.Length > 0 && !id.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var values = Enumerable.Range(0, Math.Min(headers.Length, 14)).Select(c => Convert.ToString(table[c, row]) ?? "").ToArray();
            Console.WriteLine($"  {row}: " + string.Join(" | ", values));
            if (++printed >= 12)
            {
                break;
            }
        }

        return 0;
    }

    private static int InspectPose(AssetStore store, string assetPath, string animation, float timeMs)
    {
        var data = MdxModelLoader.Load(store, assetPath);
        var sequence = data.FindSequence(animation) ?? throw new InvalidOperationException($"sequence not found: {animation}");
        var frame = sequence.StartFrame + timeMs;
        var worlds = War3Engine.Core.Animation.PoseEvaluator.ComputeNodeWorlds(data, frame, timeMs);

        Console.WriteLine($"pose {animation} frame={frame} nodes={worlds.Length}");
        var maxTranslation = 0f;
        for (var i = 0; i < worlds.Length; i++)
        {
            var t = worlds[i].Translation;
            var len = t.Length();
            if (len > maxTranslation)
            {
                maxTranslation = len;
            }

            if (len > 500f || float.IsNaN(worlds[i].M11) || float.IsNaN(t.X))
            {
                Console.WriteLine($"  EXTREME node[{i}] {data.Nodes[i].Name} translation={t} M11={worlds[i].M11}");
            }
        }

        Console.WriteLine($"max node translation: {maxTranslation:F2}");

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var geoset in data.Geosets)
        {
            for (var v = 0; v < geoset.Positions.Length; v++)
            {
                var p = geoset.Positions[v];
                var ids = geoset.BoneIndices[v];
                var w = geoset.BoneWeights[v];
                var skinned =
                    Vector3.Transform(p, worlds[(int)ids.X]) * w.X +
                    Vector3.Transform(p, worlds[(int)ids.Y]) * w.Y +
                    Vector3.Transform(p, worlds[(int)ids.Z]) * w.Z +
                    Vector3.Transform(p, worlds[(int)ids.W]) * w.W;
                min = Vector3.Min(min, skinned);
                max = Vector3.Max(max, skinned);
            }
        }

        Console.WriteLine($"skinned bounds: {min} .. {max}");
        for (var g = 0; g < data.Geosets.Length; g++)
        {
            var geoset = data.Geosets[g];
            var gmin = new Vector3(float.MaxValue);
            var gmax = new Vector3(float.MinValue);
            for (var v = 0; v < geoset.Positions.Length; v++)
            {
                var p = geoset.Positions[v];
                var ids = geoset.BoneIndices[v];
                var w = geoset.BoneWeights[v];
                var skinned =
                    Vector3.Transform(p, worlds[(int)ids.X]) * w.X +
                    Vector3.Transform(p, worlds[(int)ids.Y]) * w.Y +
                    Vector3.Transform(p, worlds[(int)ids.Z]) * w.Z +
                    Vector3.Transform(p, worlds[(int)ids.W]) * w.W;
                gmin = Vector3.Min(gmin, skinned);
                gmax = Vector3.Max(gmax, skinned);
            }

            Console.WriteLine($"  geoset[{g}] {gmin} .. {gmax}  size={(gmax - gmin).Length():F1}");
        }

        for (var i = 0; i < data.Nodes.Length; i++)
        {
            if (data.Nodes[i].Name.Contains("Hand", StringComparison.OrdinalIgnoreCase) || data.Nodes[i].Name.Contains("Weapon", StringComparison.OrdinalIgnoreCase) || data.Nodes[i].Name.Contains("Shield", StringComparison.OrdinalIgnoreCase))
            {
                var m = worlds[i];
                var sx = new Vector3(m.M11, m.M12, m.M13).Length();
                var sy = new Vector3(m.M21, m.M22, m.M23).Length();
                var sz = new Vector3(m.M31, m.M32, m.M33).Length();
                Console.WriteLine($"  bone[{i}] {data.Nodes[i].Name} translation={m.Translation} scale=({sx:F2},{sy:F2},{sz:F2})");
            }
        }
        return 0;
    }

    private static int Extract(AssetStore store, string assetPath, string outputPath)
    {
        var bytes = store.ReadAllBytes(assetPath, out var source);
        File.WriteAllBytes(outputPath, bytes);
        Console.WriteLine($"{assetPath} <- {source}: {bytes.Length} bytes -> {outputPath}");
        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("War3Engine.Inspector <command> [arg]");
        Console.WriteLine("  assets [filter]   List archive file names (optionally filtered)");
        Console.WriteLine("  model [path]      Parse an MDX model and validate its runtime data");
        Console.WriteLine("  blp [path]        Decode a BLP texture and report pixel statistics");
        Console.WriteLine("  map [path]        Open a W3X map and list its map files");
    }

    private static int ListAssets(AssetStore store, string? filter)
    {
        Console.WriteLine($"archives: {string.Join(", ", store.ArchiveNames)}");
        var names = store.ListFiles().ToList();
        Console.WriteLine($"files: {names.Count}");
        var matches = filter is null
            ? names.Take(30)
            : names.Where(n => n.Contains(filter, StringComparison.OrdinalIgnoreCase)).Take(40);
        foreach (var name in matches)
        {
            Console.WriteLine($"  {name}");
        }

        return 0;
    }

    private static int InspectModel(AssetStore store, string path)
    {
        var data = MdxModelLoader.Load(store, path);
        Console.WriteLine($"model: {data.Name}");
        Console.WriteLine($"path: {path}");
        Console.WriteLine($"format: v{data.FormatVersion}");
        Console.WriteLine($"nodes: {data.Nodes.Length}");
        Console.WriteLine($"bounds: {data.Bounds.Minimum} .. {data.Bounds.Maximum} radius={data.Bounds.Radius:F2}");
        Console.WriteLine($"geosets: {data.Geosets.Length} materials: {data.Materials.Length} textures: {data.Textures.Length} sequences: {data.Sequences.Length} geosetAnims: {data.GeosetAnimations.Length}");

        var triangles = 0;
        var vertices = 0;
        for (var g = 0; g < data.Geosets.Length; g++)
        {
            var geoset = data.Geosets[g];
            triangles += geoset.Indices.Length / 3;
            vertices += geoset.Positions.Length;
            ValidateGeoset(g, geoset, data.Nodes.Length, data.Materials.Length);
            Console.WriteLine($"  geoset[{g}] verts={geoset.Positions.Length} tris={geoset.Indices.Length / 3} uvSets={geoset.TextureCoordinateSets.Length} material={geoset.MaterialId} bounds={geoset.Bounds.Minimum}..{geoset.Bounds.Maximum}");
        }

        Console.WriteLine($"total: vertices={vertices} triangles={triangles}");

        for (var m = 0; m < data.Materials.Length; m++)
        {
            var material = data.Materials[m];
            Console.WriteLine($"  material[{m}] priority={material.PriorityPlane} layers={material.Layers.Length}");
            foreach (var layer in material.Layers)
            {
                var texture = layer.TextureId < data.Textures.Length ? data.Textures[layer.TextureId].Path : "<out of range>";
                Console.WriteLine($"    layer filter={layer.FilterMode} flags={layer.Flags} tex={layer.TextureId}({texture}) coord={layer.CoordId} alpha={layer.Alpha:F2} alphaTrack={layer.Alphas is not null}");
            }
        }

        for (var a = 0; a < data.GeosetAnimations.Length; a++)
        {
            var anim = data.GeosetAnimations[a];
            Console.WriteLine($"  geosetAnim[{a}] geoset={anim.GeosetId} alpha={anim.StaticAlpha:F3} color={anim.StaticColor} alphaTrack={anim.Alphas is not null} colorTrack={anim.Colors is not null}");
        }

        foreach (var sequence in data.Sequences)
        {
            Console.WriteLine($"  seq {sequence.Name}: {sequence.StartFrame}-{sequence.EndFrame} ms flags={sequence.Flags}");
        }

        foreach (var node in data.Nodes)
        {
            var parent = node.ParentIndex >= 0 ? data.Nodes[node.ParentIndex].Name : "-";
            var animated = node.Translations is not null || node.Rotations is not null || node.Scalings is not null;
            Console.WriteLine($"  node {node.Name} id={node.ObjectId} parent={parent} pivot={node.Pivot} animated={animated} flags={node.Flags}");
        }

        return 0;
    }

    private static void ValidateGeoset(int index, GeosetData geoset, int nodeCount, int materialCount)
    {
        if (geoset.MaterialId < 0 || geoset.MaterialId >= materialCount)
        {
            throw new InvalidDataException($"geoset {index}: material id {geoset.MaterialId} out of range");
        }

        for (var v = 0; v < geoset.Positions.Length; v++)
        {
            var w = geoset.BoneWeights[v];
            var sum = w.X + w.Y + w.Z + w.W;
            if (MathF.Abs(sum - 1f) > 0.001f)
            {
                throw new InvalidDataException($"geoset {index}: vertex {v} weight sum {sum}");
            }

            var ids = geoset.BoneIndices[v];
            foreach (var id in new[] { ids.X, ids.Y, ids.Z, ids.W })
            {
                if (id < 0 || id >= nodeCount)
                {
                    throw new InvalidDataException($"geoset {index}: vertex {v} bone index {id} out of range");
                }
            }
        }

        foreach (var uv in geoset.TextureCoordinateSets)
        {
            if (uv.Length != geoset.Positions.Length)
            {
                throw new InvalidDataException($"geoset {index}: uv set length {uv.Length} != {geoset.Positions.Length}");
            }
        }
    }

    private static int InspectBlp(AssetStore store, string path)
    {
        var bytes = store.ReadAllBytes(path, out var source);
        var image = BlpDecoder.Decode(bytes);
        Console.WriteLine($"blp: {path} <- {source}");
        Console.WriteLine($"bytes: {bytes.Length} dimensions: {image.Width}x{image.Height}");

        long alpha0 = 0;
        long opaque = 0;
        var min = new Vector3(255);
        var max = Vector3.Zero;
        for (var i = 0; i < image.Rgba.Length; i += 4)
        {
            var r = image.Rgba[i];
            var g = image.Rgba[i + 1];
            var b = image.Rgba[i + 2];
            var a = image.Rgba[i + 3];
            if (a == 0)
            {
                alpha0++;
            }
            else if (a == 255)
            {
                opaque++;
            }

            min = Vector3.Min(min, new Vector3(r, g, b));
            max = Vector3.Max(max, new Vector3(r, g, b));
        }

        var pixels = image.Width * image.Height;
        Console.WriteLine($"pixels: {pixels} alpha0={alpha0} opaque={opaque} rgbMin={min} rgbMax={max}");
        Console.WriteLine($"firstPixel: R={image.Rgba[0]} G={image.Rgba[1]} B={image.Rgba[2]} A={image.Rgba[3]}");
        return 0;
    }

    private static int InspectMapData(string mapPath)
    {
        var package = MapPackage.Load(mapPath);
        var map = package.Map;
        var info = map.Info;
        var env = map.Environment;
        Console.WriteLine($"map: {info?.MapName ?? "?"} tileset={env?.Tileset} custom={env?.IsCustomTileset}");
        Console.WriteLine($"info: players={info?.Players.Count} playable={info?.PlayableMapAreaWidth}x{info?.PlayableMapAreaHeight} gameVersion={info?.GameVersion}");
        Console.WriteLine($"env: width={env?.Width} height={env?.Height} left={env?.Left} bottom={env?.Bottom} mapSize={env?.MapWidth}x{env?.MapHeight} tiles={env?.TerrainTiles.Count}");
        var tiles = env!.TerrainTiles;
        var heights = tiles.Select(t => t.Height).ToArray();
        var water = tiles.Count(t => t.IsWater);
        var cliffs = tiles.Count(t => t.CliffLevel > 0);
        var ramps = tiles.Count(t => t.IsRamp);
        var boundary = tiles.Count(t => t.IsBoundary);
        Console.WriteLine($" heights: min={heights.Min():F1} max={heights.Max():F1} waterVerts={water} cliffVerts={cliffs} ramps={ramps} boundary={boundary}");
        Console.WriteLine($" groundTypes used: {string.Join(",", tiles.Select(t => t.Texture).Distinct().OrderBy(x => x))}");
        Console.WriteLine($" terrainTypes: {string.Join(",", env.TerrainTypes)}");
        Console.WriteLine($" cliffTypes: {string.Join(",", env.CliffTypes)}");
        var doodads = map.Doodads?.Doodads ?? new();
        Console.WriteLine($"doodads: {doodads.Count}");
        foreach (var group in doodads.GroupBy(d => d.TypeId.ToRawcode()).OrderByDescending(g => g.Count()).Take(15))
        {
            Console.WriteLine($"  {group.Key}: {group.Count()} pos0={group.First().Position} rot0={group.First().Rotation} scale0={group.First().Scale} variation0={group.First().Variation}");
        }

        var units = map.Units?.Units ?? new();
        Console.WriteLine($"units: {units.Count}");
        foreach (var group in units.GroupBy(u => u.TypeId.ToRawcode()).OrderByDescending(g => g.Count()).Take(15))
        {
            Console.WriteLine($"  {group.Key}: {group.Count()} pos0={group.First().Position} rot0={group.First().Rotation} scale0={group.First().Scale}");
        }

        Console.WriteLine($"cameras: {map.Cameras?.Cameras.Count ?? 0} regions: {map.Regions?.Regions.Count ?? 0}");
        var unitsData = map.UnitObjectData;
        Console.WriteLine($"unit object data: base={unitsData?.BaseUnits.Count ?? 0} new={unitsData?.NewUnits.Count ?? 0}");
        if (unitsData is not null)
        {
            foreach (var mod in unitsData.NewUnits.Where(m => m.Modifications.Any(x => x.Id.ToRawcode().Contains("fi") || x.Id.ToRawcode().Contains("mdl") || x.Id.ToRawcode().Contains("mod"))).Take(5))
            {
                Console.WriteLine($"  MODEL {mod.NewId.ToRawcode()} <- {mod.OldId.ToRawcode()} [{string.Join(",", mod.Modifications.Where(x => x.Id.ToRawcode().Contains("fi") || x.Id.ToRawcode().Contains("mdl") || x.Id.ToRawcode().Contains("mod")).Select(m => m.Id.ToRawcode() + "=" + m.Value))}]");
            }

            foreach (var mod in unitsData.NewUnits.Take(3))
            {
                Console.WriteLine($"  {mod.NewId.ToRawcode()} <- {mod.OldId.ToRawcode()} mods={mod.Modifications.Count} [{string.Join(",", mod.Modifications.Take(8).Select(m => m.Id.ToRawcode() + "=" + m.Value))}]");
            }

            foreach (var mod in unitsData.BaseUnits.Take(2))
            {
                Console.WriteLine($"  base {mod.OldId.ToRawcode()} mods={mod.Modifications.Count} [{string.Join(",", mod.Modifications.Take(6).Select(m => m.Id.ToRawcode() + "=" + m.Value))}]");
            }
        }

        var doodadData = map.DoodadObjectData;
        Console.WriteLine($"doodad object data: base={doodadData?.BaseDoodads.Count ?? 0} new={doodadData?.NewDoodads.Count ?? 0}");
        if (doodadData is not null)
        {
            foreach (var mod in doodadData.NewDoodads.Take(3))
            {
                Console.WriteLine($"  {mod.NewId.ToRawcode()} <- {mod.OldId.ToRawcode()} mods={mod.Modifications.Count} [{string.Join(",", mod.Modifications.Take(8).Select(m => m.Id.ToRawcode() + "=" + m.Value))}]");
            }
        }
        return 0;
    }

    private static int InspectMap(AssetStore store, string mapPath)
    {
        if (!File.Exists(mapPath) && !Directory.Exists(mapPath))
        {
            Console.WriteLine($"map not found: {mapPath}");
            return 1;
        }

        store.MountMap(mapPath);
        Console.WriteLine($"map: {mapPath}");
        Console.WriteLine($"size: {new FileInfo(mapPath).Length} bytes");
        foreach (var name in AssetStore.KnownMapFiles)
        {
            var exists = store.Exists(name);
            Console.WriteLine($"  {(exists ? "present" : "missing ")} {name}");
        }

        var all = store.ListFiles().ToList();
        Console.WriteLine($"listfile names: {all.Count}");
        foreach (var name in all.Where(n => n.StartsWith("war3map", StringComparison.OrdinalIgnoreCase) || n.Contains("script", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine($"  {name}");
        }

        return 0;
    }
}
