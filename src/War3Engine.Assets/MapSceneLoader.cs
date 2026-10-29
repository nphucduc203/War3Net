using System.Numerics;
using War3Engine.Core;
using War3Engine.Core.Map;
using War3Engine.Core.Models;
using War3Net.Build.Environment;
using War3Net.Build.Object;
using War3Net.Build.Widget;
using War3Net.Common.Extensions;

namespace War3Engine.Assets;

/// <summary>
/// Builds a renderable static map scene: terrain mesh from W3E plus doodad/unit placements.
/// Terrain heights follow the WC3 formula worldZ = W3EHeight + cliffLevel*128 - 256.
/// </summary>
public static class MapSceneLoader
{
    private const float TileSize = 128f;
    private const float HeightCorrection = TileSize * 2f;

    public static MapSceneData Load(AssetStore store, string mapPath, int maxPlacements = 2000, bool includeUnits = true)
    {
        store.MountMap(mapPath);
        var package = MapPackage.Load(mapPath);
        var map = package.Map;
        var environment = map.Environment ?? throw new InvalidDataException("war3map.w3e missing or unreadable");
        var tables = GameDataTables.Load(store);
        var terrain = BuildTerrain(store, environment, tables);

        var placements = new List<MapPlacement>();
        var unresolved = 0;
        var overrides = BuildObjectOverrides(map);
        var doodads = map.Doodads?.Doodads ?? new List<DoodadData>();
        foreach (var doodad in doodads)
        {
            var art = tables.FindDoodad(doodad.TypeId);
            string? file = null;
            if (overrides.TryGetValue(doodad.TypeId, out var objectArt))
            {
                file = objectArt.Model;
                art ??= objectArt.BaseId != doodad.TypeId ? tables.FindDoodad(objectArt.BaseId) : null;
            }

            file ??= art?.File;
            if (art?.Class == "P" || file is null)
            {
                // Pathing-only doodads (LOS/pathing blockers) have no visible model.
                if (file is null)
                {
                    unresolved++;
                }

                continue;
            }

            var modelPath = ResolveModelPath(store, file, doodad.Variation);
            if (modelPath is null)
            {
                unresolved++;
                continue;
            }

            var scale = doodad.Scale * (art?.DefaultScale ?? 1f);
            var world = Matrix4x4.CreateScale(scale)
                        * Matrix4x4.CreateRotationZ(doodad.Rotation)
                        * Matrix4x4.CreateTranslation(doodad.Position);
            placements.Add(new MapPlacement
            {
                ModelPath = modelPath,
                World = Wc3Space.Transform(world),
                TypeId = doodad.TypeId.ToRawcode(),
                IsUnit = false,
                ReplaceableId = art?.ReplaceableId ?? 0,
                ReplaceableTexture = art?.ReplaceableTexture,
            });
        }

        var units = includeUnits ? map.Units?.Units ?? new List<UnitData>() : new List<UnitData>();
        foreach (var unit in units)
        {
            var art = tables.FindUnit(unit.TypeId);
            string? file = null;
            if (overrides.TryGetValue(unit.TypeId, out var objectArt))
            {
                file = objectArt.Model;
                art ??= objectArt.BaseId != unit.TypeId ? tables.FindUnit(objectArt.BaseId) : null;
            }

            file ??= art?.File;
            if (file is null)
            {
                unresolved++;
                continue;
            }

            var modelPath = ResolveModelPath(store, file, unit.Variation);
            if (modelPath is null)
            {
                unresolved++;
                continue;
            }

            var scale = unit.Scale * (art?.ModelScale ?? 1f);
            var world = Matrix4x4.CreateScale(scale)
                        * Matrix4x4.CreateRotationZ(unit.Rotation)
                        * Matrix4x4.CreateTranslation(unit.Position);
            placements.Add(new MapPlacement
            {
                ModelPath = modelPath,
                World = Wc3Space.Transform(world),
                TypeId = unit.TypeId.ToRawcode(),
                IsUnit = true,
            });
        }

        if (placements.Count > maxPlacements)
        {
            placements = placements.Take(maxPlacements).ToList();
        }

        return new MapSceneData
        {
            Terrain = terrain,
            Water = BuildWater(environment, tables),
            Placements = placements.ToArray(),
            MapName = map.Info?.MapName ?? Path.GetFileNameWithoutExtension(mapPath),
            Tileset = environment.Tileset.ToString(),
            UnresolvedModels = unresolved,
            TotalDoodads = doodads.Count,
            TotalUnits = units.Count,
            Bounds = terrain.Bounds,
        };
    }

    private sealed record ObjectArtOverride(string? Model, int BaseId);

    /// <summary>
    /// war3map.w3u/w3b/w3d model overrides. Object data references .mdl paths even when the
    /// shipped asset is .mdx, so extension normalization happens in ResolveModelPath.
    /// </summary>
    private static Dictionary<int, ObjectArtOverride> BuildObjectOverrides(War3Net.Build.Map map)
    {
        var result = new Dictionary<int, ObjectArtOverride>();

        void Add(IEnumerable<SimpleObjectModification>? modifications, string fileField)
        {
            if (modifications is null)
            {
                return;
            }

            foreach (var modification in modifications)
            {
                var id = modification.NewId != 0 ? modification.NewId : modification.OldId;
                var file = modification.Modifications
                    .FirstOrDefault(m => m.Id.ToRawcode() == fileField)?.Value as string;
                result[id] = new ObjectArtOverride(string.IsNullOrWhiteSpace(file) ? null : file, modification.OldId);
            }
        }

        void AddVariations(IEnumerable<VariationObjectModification>? modifications, string fileField)
        {
            if (modifications is null)
            {
                return;
            }

            foreach (var modification in modifications)
            {
                var id = modification.NewId != 0 ? modification.NewId : modification.OldId;
                var file = modification.Modifications
                    .FirstOrDefault(m => m.Id.ToRawcode() == fileField)?.Value as string;
                result[id] = new ObjectArtOverride(string.IsNullOrWhiteSpace(file) ? null : file, modification.OldId);
            }
        }

        Add(map.UnitObjectData?.NewUnits, "umdl");
        Add(map.UnitObjectData?.BaseUnits, "umdl");
        AddVariations(map.DoodadObjectData?.NewDoodads, "dfil");
        AddVariations(map.DoodadObjectData?.BaseDoodads, "dfil");
        Add(map.DestructableObjectData?.NewDestructables, "dfil");
        Add(map.DestructableObjectData?.BaseDestructables, "dfil");
        return result;
    }

    private static string? ResolveModelPath(AssetStore store, string file, int variation)
    {
        var candidates = new List<string>();
        var lower = file.ToLowerInvariant();
        if (lower.EndsWith(".mdl", StringComparison.Ordinal))
        {
            candidates.Add(file[..^4] + ".mdx");
        }
        else if (lower.EndsWith(".mdx", StringComparison.Ordinal))
        {
            candidates.Add(file);
        }
        else
        {
            if (variation > 0)
            {
                candidates.Add($"{file}{variation}.mdx");
            }

            candidates.Add(file + ".mdx");
        }

        return candidates.FirstOrDefault(store.Exists);
    }

    private static TerrainMeshData BuildTerrain(AssetStore store, MapEnvironment environment, GameDataTables tables)
    {
        var columns = (int)environment.Width + 1;
        var rows = (int)environment.Height + 1;
        var tiles = environment.TerrainTiles;
        if (tiles.Count != columns * rows)
        {
            throw new InvalidDataException($"W3E tile count {tiles.Count} != {columns}x{rows}");
        }

        var heights = new float[tiles.Count];
        for (var i = 0; i < tiles.Count; i++)
        {
            heights[i] = HeightOf(tiles[i]);
        }

        var normal = new Vector3[tiles.Count];
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                var left = heights[Index(Math.Max(0, x - 1), y, columns)];
                var right = heights[Index(Math.Min(columns - 1, x + 1), y, columns)];
                var down = heights[Index(x, Math.Max(0, y - 1), columns)];
                var up = heights[Index(x, Math.Min(rows - 1, y + 1), columns)];
                var dx = (right - left) / (TileSize * (x == 0 || x == columns - 1 ? 1f : 2f));
                var dy = (up - down) / (TileSize * (y == 0 || y == rows - 1 ? 1f : 2f));
                normal[Index(x, y, columns)] = Vector3.Normalize(new Vector3(-dx, -dy, 1f));
            }
        }

        var atlasSizes = new Dictionary<string, BlpImageSize>(StringComparer.OrdinalIgnoreCase);
        var groups = new Dictionary<(int Layer, string Path), List<float>>();

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        void AddVertex((int Layer, string Path) key, Vector3 position, Vector3 normalVector, Vector2 uv)
        {
            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<float>();
                groups[key] = list;
            }

            var renderPosition = Wc3Space.Position(position);
            var renderNormal = Wc3Space.Position(normalVector);
            list.AddRange(new[] { renderPosition.X, renderPosition.Y, renderPosition.Z, renderNormal.X, renderNormal.Y, renderNormal.Z, uv.X, uv.Y });
            min = Vector3.Min(min, renderPosition);
            max = Vector3.Max(max, renderPosition);
        }

        for (var y = 0; y < rows - 1; y++)
        {
            for (var x = 0; x < columns - 1; x++)
            {
                var c00 = tiles[Index(x, y, columns)];
                var c10 = tiles[Index(x + 1, y, columns)];
                var c11 = tiles[Index(x + 1, y + 1, columns)];
                var c01 = tiles[Index(x, y + 1, columns)];

                var p00 = new Vector3(environment.Left + (x * TileSize), environment.Bottom + (y * TileSize), heights[Index(x, y, columns)]);
                var p10 = new Vector3(environment.Left + ((x + 1) * TileSize), environment.Bottom + (y * TileSize), heights[Index(x + 1, y, columns)]);
                var p11 = new Vector3(environment.Left + ((x + 1) * TileSize), environment.Bottom + ((y + 1) * TileSize), heights[Index(x + 1, y + 1, columns)]);
                var p01 = new Vector3(environment.Left + (x * TileSize), environment.Bottom + ((y + 1) * TileSize), heights[Index(x, y + 1, columns)]);

                var n00 = normal[Index(x, y, columns)];
                var n10 = normal[Index(x + 1, y, columns)];
                var n11 = normal[Index(x + 1, y + 1, columns)];
                var n01 = normal[Index(x, y + 1, columns)];

                var layers = new[] { c00.Texture, c10.Texture, c11.Texture, c01.Texture }
                    .Distinct()
                    .OrderBy(v => v)
                    .ToArray();
                if (layers.Length == 0)
                {
                    layers = new[] { 0 };
                }

                for (var li = 0; li < layers.Length; li++)
                {
                    var layer = layers[li];
                    var texturePath = TerrainTexturePath(store, environment, tables, layer, atlasSizes);

                    // WC3 splat masks: bits 4=NE, 8=NW, 1=SE, 2=SW for corners covering this layer.
                    var cell = (c11.Texture >= layer ? 4 : 0)
                               + (c01.Texture >= layer ? 8 : 0)
                               + (c10.Texture >= layer ? 1 : 0)
                               + (c00.Texture >= layer ? 2 : 0);
                    var (u0, v0, u1, v1) = AtlasCell(texturePath, atlasSizes, cell);
                    var key = (li, texturePath ?? string.Empty);

                    AddVertex(key, p00, n00, new Vector2(u0, v0));
                    AddVertex(key, p10, n10, new Vector2(u1, v0));
                    AddVertex(key, p11, n11, new Vector2(u1, v1));
                    AddVertex(key, p00, n00, new Vector2(u0, v0));
                    AddVertex(key, p11, n11, new Vector2(u1, v1));
                    AddVertex(key, p01, n01, new Vector2(u0, v1));
                }
            }
        }

        // Cliff skirts: WC3 renders dedicated cliff models per height tier. Until those are
        // implemented, wall quads at per-tile tier boundaries keep levels visually solid.
        const int cliffLayer = 1000;
        var cliffPath = store.Exists(@"Textures\Cliff0.blp") ? @"Textures\Cliff0.blp" : null;
        if (cliffPath is not null)
        {
            var tileHeights = new float[(columns - 1) * (rows - 1)];
            for (var y = 0; y < rows - 1; y++)
            {
                for (var x = 0; x < columns - 1; x++)
                {
                    tileHeights[x + (y * (columns - 1))] =
                        (heights[Index(x, y, columns)] + heights[Index(x + 1, y, columns)] +
                         heights[Index(x + 1, y + 1, columns)] + heights[Index(x, y + 1, columns)]) / 4f;
                }
            }

            void AddTierWall(float x0, float y0, float x1, float y1, float lowZ, float highZ, Vector3 outward)
            {
                if (highZ - lowZ < 48f)
                {
                    return;
                }

                var v = (highZ - lowZ) / 128f;
                // AddVertex performs the WC3->render basis change.
                var a0 = new Vector3(x0, y0, lowZ);
                var a1 = new Vector3(x0, y0, highZ);
                var b0 = new Vector3(x1, y1, lowZ);
                var b1 = new Vector3(x1, y1, highZ);
                var key = (cliffLayer, cliffPath);
                AddVertex(key, a0, outward, new Vector2(0f, 0f));
                AddVertex(key, b0, outward, new Vector2(1f, 0f));
                AddVertex(key, b1, outward, new Vector2(1f, v));
                AddVertex(key, a0, outward, new Vector2(0f, 0f));
                AddVertex(key, b1, outward, new Vector2(1f, v));
                AddVertex(key, a1, outward, new Vector2(0f, v));
            }

            for (var y = 0; y < rows - 1; y++)
            {
                for (var x = 0; x < columns - 1; x++)
                {
                    var h = tileHeights[x + (y * (columns - 1))];
                    if (x > 0)
                    {
                        var neighbor = tileHeights[(x - 1) + (y * (columns - 1))];
                        if (MathF.Abs(h - neighbor) >= 48f)
                        {
                            var wx = environment.Left + (x * TileSize);
                            var wy = environment.Bottom + (y * TileSize);
                            var outward = h > neighbor ? new Vector3(-1, 0, 0) : new Vector3(1, 0, 0);
                            AddTierWall(wx, wy, wx, wy + TileSize, MathF.Min(h, neighbor), MathF.Max(h, neighbor), outward);
                        }
                    }

                    if (y > 0)
                    {
                        var neighbor = tileHeights[x + ((y - 1) * (columns - 1))];
                        if (MathF.Abs(h - neighbor) >= 48f)
                        {
                            var wx = environment.Left + (x * TileSize);
                            var wy = environment.Bottom + (y * TileSize);
                            var outward = h > neighbor ? new Vector3(0, -1, 0) : new Vector3(0, 1, 0);
                            AddTierWall(wx, wy, wx + TileSize, wy, MathF.Min(h, neighbor), MathF.Max(h, neighbor), outward);
                        }
                    }
                }
            }
        }

        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var indices = new List<int>();
        var textureGroups = new List<TerrainTextureGroup>();
        foreach (var ((layer, path), floats) in groups
                     .Where(g => g.Value.Count > 0)
                     .OrderBy(g => g.Key.Layer)
                     .ThenBy(g => g.Key.Path, StringComparer.OrdinalIgnoreCase))
        {
            var start = indices.Count;
            for (var i = 0; i < floats.Count; i += 8)
            {
                positions.Add(new Vector3(floats[i], floats[i + 1], floats[i + 2]));
                normals.Add(new Vector3(floats[i + 3], floats[i + 4], floats[i + 5]));
                uvs.Add(new Vector2(floats[i + 6], floats[i + 7]));
                indices.Add(indices.Count);
            }

            textureGroups.Add(new TerrainTextureGroup(path, start, indices.Count - start, layer > 0, layer));
        }

        return new TerrainMeshData
        {
            Positions = positions.ToArray(),
            Normals = normals.ToArray(),
            Uvs = uvs.ToArray(),
            Indices = indices.ToArray(),
            TextureGroups = textureGroups.ToArray(),
            Bounds = new Extent(min, max, (max - min).Length() * 0.5f),
            TileCount = (columns - 1) * (rows - 1),
        };
    }

    private static WaterMeshData? BuildWater(MapEnvironment environment, GameDataTables tables)
    {
        var columns = (int)environment.Width + 1;
        var rows = (int)environment.Height + 1;
        var tiles = environment.TerrainTiles;
        var tileset = ((char)(byte)environment.Tileset).ToString();
        var style = tables.FindWater(tileset);
        if (style is null)
        {
            return null;
        }

        var styleHeight = style.Value.HeightTiles * TileSize;
        var texturePath = style.Value.TextureBase + "00.blp";
        var positions = new List<Vector3>();
        var uvs = new List<Vector2>();
        var indices = new List<int>();

        for (var y = 0; y < rows - 1; y++)
        {
            for (var x = 0; x < columns - 1; x++)
            {
                var c00 = tiles[Index(x, y, columns)];
                var c10 = tiles[Index(x + 1, y, columns)];
                var c11 = tiles[Index(x + 1, y + 1, columns)];
                var c01 = tiles[Index(x, y + 1, columns)];
                // Retail rule: water if any corner is water-flagged and no corner is a map edge.
                if ((!c00.IsWater && !c10.IsWater && !c11.IsWater && !c01.IsWater)
                    || c00.IsBoundary || c10.IsBoundary || c11.IsBoundary || c01.IsBoundary)
                {
                    continue;
                }

                var x0 = environment.Left + (x * TileSize);
                var y0 = environment.Bottom + (y * TileSize);
                Vector2 Uv(float wx, float wy) => new(wx / 256f, wy / 256f);
                float W(TerrainTile t) => WaterOf(t) + styleHeight;
                var p00 = Wc3Space.Position(new Vector3(x0, y0, W(c00)));
                var p10 = Wc3Space.Position(new Vector3(x0 + TileSize, y0, W(c10)));
                var p11 = Wc3Space.Position(new Vector3(x0 + TileSize, y0 + TileSize, W(c11)));
                var p01 = Wc3Space.Position(new Vector3(x0, y0 + TileSize, W(c01)));
                var baseIndex = positions.Count;
                positions.AddRange(new[] { p00, p10, p11, p00, p11, p01 });
                uvs.AddRange(new[]
                {
                    Uv(x0, y0), Uv(x0 + TileSize, y0), Uv(x0 + TileSize, y0 + TileSize),
                    Uv(x0, y0), Uv(x0 + TileSize, y0 + TileSize), Uv(x0, y0 + TileSize),
                });
                for (var i = 0; i < 6; i++)
                {
                    indices.Add(baseIndex + i);
                }
            }
        }

        if (positions.Count == 0)
        {
            return null;
        }

        return new WaterMeshData
        {
            Positions = positions.ToArray(),
            Uvs = uvs.ToArray(),
            Indices = indices.ToArray(),
            TexturePath = texturePath,
        };
    }

    private static float WaterOf(TerrainTile tile) => (tile.WaterHeight * TileSize);

    private static float HeightOf(TerrainTile tile) =>
        (tile.Height * TileSize) + (tile.CliffLevel * TileSize) - HeightCorrection;

    private static int Index(int x, int y, int columns) => x + (y * columns);

    private static string? TerrainTexturePath(
        AssetStore store,
        MapEnvironment environment,
        GameDataTables tables,
        int textureIndex,
        Dictionary<string, BlpImageSize> atlasSizes)
    {
        if (textureIndex < 0 || textureIndex >= environment.TerrainTypes.Count)
        {
            return null;
        }

        var path = tables.FindTerrainTexture((int)environment.TerrainTypes[textureIndex]);
        if (path is null)
        {
            return null;
        }

        if (!atlasSizes.ContainsKey(path) && store.TryOpen(path, out var stream))
        {
            using (stream)
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                try
                {
                    atlasSizes[path] = BlpDecoder.GetSize(memory.ToArray());
                }
                catch (InvalidDataException)
                {
                    atlasSizes[path] = new BlpImageSize(256, 256);
                }
            }
        }

        return path;
    }

    /// <summary>
    /// Ground textures are 4x4 (or 8x4 extended) atlases; full-coverage tiles use cell 15 with a
    /// 5% inset to avoid bleeding into neighbouring cells (reference SetTileUV behavior).
    /// </summary>
    private static (float U0, float V0, float U1, float V1) AtlasCell(string? texturePath, Dictionary<string, BlpImageSize> sizes, int cell)
    {
        var uCell = 0.25f;
        var vCell = 0.25f;
        if (texturePath is not null && sizes.TryGetValue(texturePath, out var size) && size.Width > size.Height)
        {
            uCell = 0.125f;
        }

        var cellX = cell % 4;
        var cellY = cell / 4;
        var u0 = cellX * uCell;
        var v0 = cellY * vCell;
        var u1 = u0 + uCell;
        var v1 = v0 + vCell;

        var centerU = (u0 + u1) * 0.5f;
        var centerV = (v0 + v1) * 0.5f;
        u0 = Lerp(u0, centerU, 0.05f);
        u1 = Lerp(u1, centerU, 0.05f);
        v0 = Lerp(v0, centerV, 0.05f);
        v1 = Lerp(v1, centerV, 0.05f);
        return (u0, v0, u1, v1);
    }

    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);
}
