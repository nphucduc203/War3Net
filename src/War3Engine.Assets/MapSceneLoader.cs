using System.Numerics;
using War3Engine.Core;
using War3Engine.Core.Map;
using War3Engine.Core.Models;
using War3Net.Build.Environment;
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

    public static MapSceneData Load(AssetStore store, string mapPath, int maxPlacements = 2000)
    {
        var package = MapPackage.Load(mapPath);
        var map = package.Map;
        var environment = map.Environment ?? throw new InvalidDataException("war3map.w3e missing or unreadable");
        var tables = GameDataTables.Load(store);
        var terrain = BuildTerrain(store, environment, tables);

        var placements = new List<MapPlacement>();
        var unresolved = 0;
        var doodads = map.Doodads?.Doodads ?? new List<DoodadData>();
        foreach (var doodad in doodads)
        {
            var art = tables.FindDoodad(doodad.TypeId);
            if (art is null || art.Class == "P")
            {
                // Pathing-only doodads (LOS/pathing blockers) have no visible model.
                if (art is null)
                {
                    unresolved++;
                }

                continue;
            }

            var modelPath = ResolveModelPath(store, art.File, doodad.Variation);
            if (modelPath is null)
            {
                unresolved++;
                continue;
            }

            var scale = doodad.Scale * art.DefaultScale;
            var world = Matrix4x4.CreateScale(scale)
                        * Matrix4x4.CreateRotationZ(doodad.Rotation)
                        * Matrix4x4.CreateTranslation(doodad.Position);
            placements.Add(new MapPlacement
            {
                ModelPath = modelPath,
                World = Wc3Space.Transform(world),
                TypeId = doodad.TypeId.ToRawcode(),
                IsUnit = false,
                ReplaceableId = art.ReplaceableId,
                ReplaceableTexture = art.ReplaceableTexture,
            });
        }

        var units = map.Units?.Units ?? new List<UnitData>();
        foreach (var unit in units)
        {
            var art = tables.FindUnit(unit.TypeId);
            if (art is null)
            {
                unresolved++;
                continue;
            }

            var modelPath = ResolveModelPath(store, art.File, unit.Variation);
            if (modelPath is null)
            {
                unresolved++;
                continue;
            }

            var scale = unit.Scale * art.ModelScale;
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
            Placements = placements.ToArray(),
            MapName = map.Info?.MapName ?? Path.GetFileName(mapPath),
            Tileset = environment.Tileset.ToString(),
            UnresolvedModels = unresolved,
            TotalDoodads = doodads.Count,
            TotalUnits = units.Count,
            Bounds = terrain.Bounds,
        };
    }

    private static string? ResolveModelPath(AssetStore store, string file, int variation)
    {
        var candidates = new List<string>();
        if (variation > 0)
        {
            candidates.Add($"{file}{variation}.mdx");
            candidates.Add($"{file}{variation}.mdl");
        }

        candidates.Add(file + ".mdx");
        candidates.Add(file + ".mdl");
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
        var groups = new Dictionary<string, List<float>>(StringComparer.OrdinalIgnoreCase)
        {
            [string.Empty] = new(),
        };

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        void AddVertex(string? texturePath, Vector3 position, Vector3 normalVector, Vector2 uv)
        {
            if (!groups.TryGetValue(texturePath ?? string.Empty, out var list))
            {
                list = new List<float>();
                groups[texturePath ?? string.Empty] = list;
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
                var tile = tiles[Index(x, y, columns)];
                var texturePath = TerrainTexturePath(store, environment, tables, tile, atlasSizes);
                var (u0, v0, u1, v1) = AtlasCell(texturePath, atlasSizes);

                var p00 = new Vector3(environment.Left + (x * TileSize), environment.Bottom + (y * TileSize), heights[Index(x, y, columns)]);
                var p10 = new Vector3(environment.Left + ((x + 1) * TileSize), environment.Bottom + (y * TileSize), heights[Index(x + 1, y, columns)]);
                var p11 = new Vector3(environment.Left + ((x + 1) * TileSize), environment.Bottom + ((y + 1) * TileSize), heights[Index(x + 1, y + 1, columns)]);
                var p01 = new Vector3(environment.Left + (x * TileSize), environment.Bottom + ((y + 1) * TileSize), heights[Index(x, y + 1, columns)]);

                var n00 = normal[Index(x, y, columns)];
                var n10 = normal[Index(x + 1, y, columns)];
                var n11 = normal[Index(x + 1, y + 1, columns)];
                var n01 = normal[Index(x, y + 1, columns)];

                AddVertex(texturePath, p00, n00, new Vector2(u0, v0));
                AddVertex(texturePath, p10, n10, new Vector2(u1, v0));
                AddVertex(texturePath, p11, n11, new Vector2(u1, v1));
                AddVertex(texturePath, p00, n00, new Vector2(u0, v0));
                AddVertex(texturePath, p11, n11, new Vector2(u1, v1));
                AddVertex(texturePath, p01, n01, new Vector2(u0, v1));
            }
        }

        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var indices = new List<int>();
        var textureGroups = new List<TerrainTextureGroup>();
        foreach (var (path, floats) in groups.Where(g => g.Value.Count > 0).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var start = indices.Count;
            for (var i = 0; i < floats.Count; i += 8)
            {
                positions.Add(new Vector3(floats[i], floats[i + 1], floats[i + 2]));
                normals.Add(new Vector3(floats[i + 3], floats[i + 4], floats[i + 5]));
                uvs.Add(new Vector2(floats[i + 6], floats[i + 7]));
                indices.Add(indices.Count);
            }

            textureGroups.Add(new TerrainTextureGroup(path, start, indices.Count - start));
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

    private static float HeightOf(TerrainTile tile) =>
        (tile.Height * TileSize) + (tile.CliffLevel * TileSize) - HeightCorrection;

    private static int Index(int x, int y, int columns) => x + (y * columns);

    private static string? TerrainTexturePath(
        AssetStore store,
        MapEnvironment environment,
        GameDataTables tables,
        TerrainTile tile,
        Dictionary<string, BlpImageSize> atlasSizes)
    {
        if (tile.Texture < 0 || tile.Texture >= environment.TerrainTypes.Count)
        {
            return null;
        }

        var path = tables.FindTerrainTexture((int)environment.TerrainTypes[tile.Texture]);
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
    private static (float U0, float V0, float U1, float V1) AtlasCell(string? texturePath, Dictionary<string, BlpImageSize> sizes)
    {
        var uCell = 0.25f;
        var vCell = 0.25f;
        if (texturePath is not null && sizes.TryGetValue(texturePath, out var size) && size.Width > size.Height)
        {
            uCell = 0.125f;
        }

        const int cell = 15;
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
