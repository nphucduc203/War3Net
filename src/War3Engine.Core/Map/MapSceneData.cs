using System.Numerics;
using War3Engine.Core.Models;

namespace War3Engine.Core.Map;

public sealed class TerrainMeshData
{
    public Vector3[] Positions { get; init; } = Array.Empty<Vector3>();

    public Vector3[] Normals { get; init; } = Array.Empty<Vector3>();

    public Vector2[] Uvs { get; init; } = Array.Empty<Vector2>();

    public int[] Indices { get; init; } = Array.Empty<int>();

    public TerrainTextureGroup[] TextureGroups { get; init; } = Array.Empty<TerrainTextureGroup>();

    public Extent Bounds { get; init; }

    public int TileCount { get; init; }
}

public readonly record struct TerrainTextureGroup(string TexturePath, int StartIndex, int IndexCount);

public sealed class MapPlacement
{
    public string ModelPath { get; init; } = string.Empty;

    public Matrix4x4 World { get; init; } = Matrix4x4.Identity;

    public string TypeId { get; init; } = string.Empty;

    public bool IsUnit { get; init; }

    /// <summary>Destructibles override a replaceable texture slot with their own art file.</summary>
    public int ReplaceableId { get; init; }

    public string? ReplaceableTexture { get; init; }
}

public sealed class MapSceneData
{
    public TerrainMeshData Terrain { get; init; } = new();

    public MapPlacement[] Placements { get; init; } = Array.Empty<MapPlacement>();

    public string MapName { get; init; } = string.Empty;

    public string Tileset { get; init; } = string.Empty;

    public int UnresolvedModels { get; init; }

    public int TotalDoodads { get; init; }

    public int TotalUnits { get; init; }

    public Extent Bounds { get; init; }
}
