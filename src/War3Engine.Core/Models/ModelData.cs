using System.Numerics;

namespace War3Engine.Core.Models;

public enum LayerFilterMode : uint
{
    None = 0,
    Transparent = 1,
    Blend = 2,
    Additive = 3,
    AddAlpha = 4,
    Modulate = 5,
    Modulate2x = 6,
}

[Flags]
public enum LayerFlags : uint
{
    Unshaded = 1,
    SphereEnvMap = 2,
    TwoSided = 16,
    Unfogged = 32,
    NoDepthTest = 64,
    NoDepthSet = 128,
}

[Flags]
public enum NodeFlags : uint
{
    DontInheritTranslation = 1,
    DontInheritRotation = 2,
    DontInheritScaling = 4,
    Billboarded = 8,
    BillboardedLockX = 16,
    BillboardedLockY = 32,
    BillboardedLockZ = 64,
    CameraAnchored = 128,
}

[Flags]
public enum TextureFlags : uint
{
    WrapWidth = 1,
    WrapHeight = 2,
}

[Flags]
public enum SequenceFlags : uint
{
    NonLooping = 1,
}

public enum InterpolationType : uint
{
    None = 0,
    Linear = 1,
    Hermite = 2,
    Bezier = 3,
}

public readonly struct Key<T>
    where T : struct
{
    public Key(int frame, T value, T tanIn, T tanOut)
    {
        Frame = frame;
        Value = value;
        TanIn = tanIn;
        TanOut = tanOut;
    }

    public int Frame { get; }

    public T Value { get; }

    public T TanIn { get; }

    public T TanOut { get; }
}

public sealed class Track<T>
    where T : struct
{
    public InterpolationType Interpolation { get; init; }

    public int GlobalSequenceId { get; init; } = -1;

    public Key<T>[] Keys { get; init; } = Array.Empty<Key<T>>();
}

public readonly struct Extent
{
    public Extent(Vector3 minimum, Vector3 maximum, float radius)
    {
        Minimum = minimum;
        Maximum = maximum;
        Radius = radius;
    }

    public Vector3 Minimum { get; }

    public Vector3 Maximum { get; }

    public float Radius { get; }

    public Vector3 Center => (Minimum + Maximum) * 0.5f;
}

public sealed class SequenceData
{
    public string Name { get; init; } = string.Empty;

    public uint StartFrame { get; init; }

    public uint EndFrame { get; init; }

    public float MoveSpeed { get; init; }

    public SequenceFlags Flags { get; init; }

    public float Rarity { get; init; }

    public uint SyncPoint { get; init; }

    public Extent Bounds { get; init; }

    public bool NonLooping => (Flags & SequenceFlags.NonLooping) != 0;
}

public sealed class TextureData
{
    public uint ReplaceableId { get; init; }

    public string Path { get; init; } = string.Empty;

    public TextureFlags Flags { get; init; }
}

public sealed class LayerData
{
    public LayerFilterMode FilterMode { get; init; }

    public LayerFlags Flags { get; init; }

    public int TextureId { get; init; }

    public int TextureAnimationId { get; init; }

    public int CoordId { get; init; }

    public float Alpha { get; init; }

    public Track<uint>? TextureIds { get; init; }

    public Track<float>? Alphas { get; init; }
}

public sealed class MaterialData
{
    public uint PriorityPlane { get; init; }

    public LayerData[] Layers { get; init; } = Array.Empty<LayerData>();
}

public sealed class GeosetData
{
    public Vector3[] Positions { get; init; } = Array.Empty<Vector3>();

    public Vector3[] Normals { get; init; } = Array.Empty<Vector3>();

    public ushort[] Indices { get; init; } = Array.Empty<ushort>();

    /// <summary>Per-vertex bone palette indices, up to 4 per vertex.</summary>
    public Vector4[] BoneIndices { get; init; } = Array.Empty<Vector4>();

    /// <summary>Per-vertex influence weights; sums to 1.</summary>
    public Vector4[] BoneWeights { get; init; } = Array.Empty<Vector4>();

    public Vector2[][] TextureCoordinateSets { get; init; } = Array.Empty<Vector2[]>();

    public int MaterialId { get; init; }

    public Extent Bounds { get; init; }
}

public sealed class GeosetAnimationData
{
    public float StaticAlpha { get; init; } = 1f;

    public Vector3 StaticColor { get; init; } = Vector3.One;

    public int GeosetId { get; init; }

    public Track<float>? Alphas { get; init; }

    public Track<Vector3>? Colors { get; init; }
}

public sealed class NodeData
{
    public string Name { get; init; } = string.Empty;

    public uint ObjectId { get; init; }

    public int ParentIndex { get; init; } = -1;

    public NodeFlags Flags { get; init; }

    public Vector3 Pivot { get; init; }

    public Track<Vector3>? Translations { get; init; }

    public Track<Quaternion>? Rotations { get; init; }

    public Track<Vector3>? Scalings { get; init; }
}

public sealed class ModelData
{
    public string Name { get; init; } = string.Empty;

    public int FormatVersion { get; init; }

    public Extent Bounds { get; init; }

    public SequenceData[] Sequences { get; init; } = Array.Empty<SequenceData>();

    public TextureData[] Textures { get; init; } = Array.Empty<TextureData>();

    public MaterialData[] Materials { get; init; } = Array.Empty<MaterialData>();

    public GeosetData[] Geosets { get; init; } = Array.Empty<GeosetData>();

    public GeosetAnimationData[] GeosetAnimations { get; init; } = Array.Empty<GeosetAnimationData>();

    public NodeData[] Nodes { get; init; } = Array.Empty<NodeData>();

    /// <summary>Durations in milliseconds, indexed by global sequence id.</summary>
    public int[] GlobalSequenceDurations { get; init; } = Array.Empty<int>();

    public SequenceData? FindSequence(string? name) =>
        name is null
            ? null
            : Sequences.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
}
