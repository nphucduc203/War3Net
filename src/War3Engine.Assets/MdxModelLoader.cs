using System.Numerics;
using War3Engine.Core;
using War3Engine.Core.Models;
using War3Net.Modeling;
using War3Net.Modeling.DataStructures;
using Extent = War3Engine.Core.Models.Extent;
using InterpolationType = War3Engine.Core.Models.InterpolationType;

namespace War3Engine.Assets;

/// <summary>
/// Converts a parsed MDX model into the engine runtime representation.
/// All geometry, pivots and animation vectors are converted from WC3 Z-up space
/// into render space (see <see cref="Wc3Space"/>); sources are never altered in place.
/// </summary>
public static class MdxModelLoader
{
    public const int MaxBones = 256;

    public static ModelData Load(AssetStore store, string assetPath)
    {
        var data = store.ReadAllBytes(assetPath, out _);
        return Load(data, assetPath);
    }

    public static ModelData Load(byte[] data, string name)
    {
        using var stream = new MemoryStream(data, writable: false);
        if (!BinaryModelParser.IsBinaryModel(stream))
        {
            throw new InvalidDataException($"Not an MDX (MDLX) file: {name}");
        }

        var raw = BinaryModelParser.Parse(stream, leaveOpen: false);
        return Convert(raw, name);
    }

    private static ModelData Convert(War3Net.Modeling.DataStructures.Model raw, string name)
    {
        var rawNodes = raw.GetNodes().ToArray();
        if (rawNodes.Length > MaxBones)
        {
            throw new NotSupportedException($"Model '{name}' has {rawNodes.Length} nodes, max supported bone palette is {MaxBones}");
        }

        var pivots = raw.PivotPoints ?? Array.Empty<Vector3>();
        var nodeIndexById = new Dictionary<uint, int>(rawNodes.Length);
        for (var i = 0; i < rawNodes.Length; i++)
        {
            nodeIndexById[rawNodes[i].ObjectId] = i;
        }

        var nodes = new NodeData[rawNodes.Length];
        for (var i = 0; i < rawNodes.Length; i++)
        {
            var node = rawNodes[i];
            var parentIndex = -1;
            if (node.ParentId is { } parentId &&
                parentId != uint.MaxValue &&
                nodeIndexById.TryGetValue(parentId, out var resolvedParent) &&
                resolvedParent != i)
            {
                parentIndex = resolvedParent;
            }

            nodes[i] = new NodeData
            {
                Name = node.Name ?? string.Empty,
                ObjectId = node.ObjectId,
                ParentIndex = parentIndex,
                Flags = (NodeFlags)(uint)node.Flags,
                // Pivot points are indexed by node object id, not by enumeration order
                // (War3Net yields nodes grouped by type).
                Pivot = node.ObjectId < pivots.Length ? Wc3Space.Position(pivots[(int)node.ObjectId]) : Vector3.Zero,
                Translations = ConvertVectorTrack(node.Translations),
                Rotations = ConvertQuaternionTrack(node.Rotations),
                Scalings = ConvertScaleTrack(node.Scalings),
            };
        }

        var geosets = ConvertGeosets(raw.Geosets, nodeIndexById, name);
        var bounds = GetBounds(raw, geosets);
        var geosetAnimations = ConvertGeosetAnimations(raw.GeosetAnimations);
        var globalSequences = raw.GlobalSequences?.Select(sequence => sequence.Duration).ToArray() ?? Array.Empty<int>();

        return new ModelData
        {
            Name = raw.ModelInfo?.Name ?? name,
            FormatVersion = (int)raw.Version!.Value.FormatVersion,
            Bounds = bounds,
            Sequences = ConvertSequences(raw.Sequences),
            Textures = ConvertTextures(raw.Textures),
            Materials = ConvertMaterials(raw.Materials),
            Geosets = geosets,
            GeosetAnimations = geosetAnimations,
            Nodes = nodes,
            GlobalSequenceDurations = globalSequences,
        };
    }

    private static SequenceData[] ConvertSequences(Sequence[]? sequences) =>
        sequences?.Select(sequence => new SequenceData
        {
            Name = sequence.Name?.Trim() ?? string.Empty,
            StartFrame = sequence.IntervalStart,
            EndFrame = sequence.IntervalEnd,
            MoveSpeed = sequence.MoveSpeed,
            Flags = (SequenceFlags)(uint)sequence.Flags,
            Rarity = sequence.Rarity,
            SyncPoint = sequence.SyncPoint,
            Bounds = ConvertBounds(sequence.Extent),
        }).ToArray() ?? Array.Empty<SequenceData>();

    private static TextureData[] ConvertTextures(Texture[]? textures) =>
        textures?.Select(texture => new TextureData
        {
            ReplaceableId = texture.ReplaceableId,
            Path = texture.FileName ?? string.Empty,
            Flags = (TextureFlags)(uint)texture.Flags,
        }).ToArray() ?? Array.Empty<TextureData>();

    private static MaterialData[] ConvertMaterials(Material[]? materials) =>
        materials?.Select(material => new MaterialData
        {
            PriorityPlane = material.PriorityPlane,
            Layers = material.Layers.Select(ConvertLayer).ToArray(),
        }).ToArray() ?? Array.Empty<MaterialData>();

    private static LayerData ConvertLayer(Layer layer) => new()
    {
        FilterMode = (LayerFilterMode)(uint)layer.FilterMode,
        Flags = (LayerFlags)(uint)layer.ShadingFlags,
        TextureId = (int)layer.TextureId,
        TextureAnimationId = (int)layer.TextureAnimationId,
        CoordId = (int)layer.CoordId,
        Alpha = layer.Alpha,
        TextureIds = ConvertUIntTrack(layer.TextureIds),
        Alphas = ConvertFloatTrack(layer.Alphas),
    };

    private static GeosetData[] ConvertGeosets(Geoset[]? geosets, Dictionary<uint, int> nodeIndexById, string name)
    {
        if (geosets is null)
        {
            return Array.Empty<GeosetData>();
        }

        var result = new GeosetData[geosets.Length];
        for (var g = 0; g < geosets.Length; g++)
        {
            var geoset = geosets[g];
            var vertexCount = geoset.Vertices.Length;
            var positions = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            for (var i = 0; i < vertexCount; i++)
            {
                positions[i] = Wc3Space.Position(geoset.Vertices[i]);
                normals[i] = Wc3Space.Position(geoset.Normals[i]);
            }

            var boneIndices = new Vector4[vertexCount];
            var boneWeights = new Vector4[vertexCount];
            BuildSkinWeights(geoset, nodeIndexById, boneIndices, boneWeights, g, name);

            var uvSets = new Vector2[geoset.TextureCoordinateSets.Length][];
            for (var s = 0; s < uvSets.Length; s++)
            {
                uvSets[s] = geoset.TextureCoordinateSets[s].TextureCoordinates;
            }

            result[g] = new GeosetData
            {
                Positions = positions,
                Normals = normals,
                Indices = geoset.Faces,
                BoneIndices = boneIndices,
                BoneWeights = boneWeights,
                TextureCoordinateSets = uvSets,
                MaterialId = (int)geoset.MaterialId,
                Bounds = ConvertBounds(geoset.Extent),
            };
        }

        return result;
    }

    private static void BuildSkinWeights(
        Geoset geoset,
        Dictionary<uint, int> nodeIndexById,
        Vector4[] boneIndices,
        Vector4[] boneWeights,
        int geosetIndex,
        string name)
    {
        var matrixGroups = geoset.MatrixGroups;
        var matrixIndices = geoset.MatrixIndices;
        var vertexGroups = geoset.VertexGroups;

        // Matrix group sizes are stored once per group; vertices reference a group and are
        // weighted equally across every bone listed for it (classic MDX has no explicit weights).
        var groupOffsets = new int[matrixGroups.Length];
        var offset = 0;
        for (var i = 0; i < matrixGroups.Length; i++)
        {
            groupOffsets[i] = offset;
            offset += (int)matrixGroups[i];
        }

        for (var v = 0; v < boneIndices.Length; v++)
        {
            if (v >= vertexGroups.Length)
            {
                boneWeights[v] = new Vector4(1f, 0f, 0f, 0f);
                continue;
            }

            var group = vertexGroups[v];
            if (group >= matrixGroups.Length)
            {
                throw new InvalidDataException($"Geoset {geosetIndex} in '{name}': vertex {v} references matrix group {group}/{matrixGroups.Length}");
            }

            var size = (int)matrixGroups[group];
            if (size == 0)
            {
                boneWeights[v] = new Vector4(1f, 0f, 0f, 0f);
                continue;
            }

            var start = groupOffsets[group];
            var used = Math.Min(size, 4);
            var weight = 1f / used;
            var ids = Vector4.Zero;
            var weights = Vector4.Zero;
            for (var j = 0; j < used; j++)
            {
                var nodeId = matrixIndices[start + j];
                if (!nodeIndexById.TryGetValue(nodeId, out var nodeIndex))
                {
                    throw new InvalidDataException($"Geoset {geosetIndex} in '{name}': matrix index references unknown node {nodeId}");
                }

                switch (j)
                {
                    case 0: ids.X = nodeIndex; weights.X = weight; break;
                    case 1: ids.Y = nodeIndex; weights.Y = weight; break;
                    case 2: ids.Z = nodeIndex; weights.Z = weight; break;
                    default: ids.W = nodeIndex; weights.W = weight; break;
                }
            }

            boneIndices[v] = ids;
            boneWeights[v] = weights;
        }
    }

    private static GeosetAnimationData[] ConvertGeosetAnimations(GeosetAnimation[]? animations) =>
        animations?.Select(animation => new GeosetAnimationData
        {
            StaticAlpha = animation.Alpha,
            StaticColor = (animation.Flags & War3Net.Modeling.Enums.GeosetAnimationFlags.Color) != 0 ? animation.Color : Vector3.One,
            GeosetId = animation.GeosetId == uint.MaxValue ? -1 : (int)animation.GeosetId,
            Alphas = ConvertFloatTrack(animation.Alphas),
            Colors = ConvertVectorTrack(animation.Colors),
        }).ToArray() ?? Array.Empty<GeosetAnimationData>();

    private static Extent GetBounds(War3Net.Modeling.DataStructures.Model raw, GeosetData[] geosets)
    {
        if (raw.ModelInfo?.Extent is { } extent && extent.BoundsRadius > 0)
        {
            return ConvertBounds(extent);
        }

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var geoset in geosets)
        {
            min = Vector3.Min(min, geoset.Bounds.Minimum);
            max = Vector3.Max(max, geoset.Bounds.Maximum);
        }

        if (min.X > max.X)
        {
            return new Extent(new Vector3(-32f), new Vector3(32f), 32f);
        }

        return new Extent(min, max, (max - min).Length() * 0.5f);
    }

    private static Extent ConvertBounds(War3Net.Modeling.DataStructures.Extent extent)
    {
        // The axis swap reverses Z, so re-normalize min/max after conversion.
        var a = Wc3Space.Position(extent.MinimumExtent);
        var b = Wc3Space.Position(extent.MaximumExtent);
        return new Extent(Vector3.Min(a, b), Vector3.Max(a, b), extent.BoundsRadius);
    }

    private static Track<Vector3>? ConvertVectorTrack(AnimationChannel<Vector3>? channel)
    {
        if (channel is not { Keys: { Length: > 0 } keys })
        {
            return null;
        }

        return new Track<Vector3>
        {
            Interpolation = ConvertInterpolation(channel.Value.InterpolationType),
            GlobalSequenceId = ConvertGlobalSequence(channel.Value.GlobalSequenceId),
            Keys = keys.Select(key => new Key<Vector3>(
                key.Frame,
                Wc3Space.Position(key.Value),
                Wc3Space.Position(key.TanIn),
                Wc3Space.Position(key.TanOut))).ToArray(),
        };
    }

    private static Track<Vector3>? ConvertScaleTrack(AnimationChannel<Vector3>? channel)
    {
        if (channel is not { Keys: { Length: > 0 } keys })
        {
            return null;
        }

        return new Track<Vector3>
        {
            Interpolation = ConvertInterpolation(channel.Value.InterpolationType),
            GlobalSequenceId = ConvertGlobalSequence(channel.Value.GlobalSequenceId),
            Keys = keys.Select(key => new Key<Vector3>(
                key.Frame,
                ConvertScale(key.Value),
                ConvertScale(key.TanIn),
                ConvertScale(key.TanOut))).ToArray(),
        };
    }

    private static Track<Quaternion>? ConvertQuaternionTrack(AnimationChannel<Quaternion>? channel)
    {
        if (channel is not { Keys: { Length: > 0 } keys })
        {
            return null;
        }

        return new Track<Quaternion>
        {
            Interpolation = ConvertInterpolation(channel.Value.InterpolationType),
            GlobalSequenceId = ConvertGlobalSequence(channel.Value.GlobalSequenceId),
            Keys = keys.Select(key => new Key<Quaternion>(
                key.Frame,
                Wc3Space.Rotation(key.Value),
                Wc3Space.Rotation(key.TanIn),
                Wc3Space.Rotation(key.TanOut))).ToArray(),
        };
    }

    private static Track<float>? ConvertFloatTrack(AnimationChannel<float>? channel)
    {
        if (channel is not { Keys: { Length: > 0 } keys })
        {
            return null;
        }

        return new Track<float>
        {
            Interpolation = ConvertInterpolation(channel.Value.InterpolationType),
            GlobalSequenceId = ConvertGlobalSequence(channel.Value.GlobalSequenceId),
            Keys = keys.Select(key => new Key<float>(key.Frame, key.Value, key.TanIn, key.TanOut)).ToArray(),
        };
    }

    private static Track<uint>? ConvertUIntTrack(AnimationChannel<uint>? channel)
    {
        if (channel is not { Keys: { Length: > 0 } keys })
        {
            return null;
        }

        return new Track<uint>
        {
            Interpolation = ConvertInterpolation(channel.Value.InterpolationType),
            GlobalSequenceId = ConvertGlobalSequence(channel.Value.GlobalSequenceId),
            Keys = keys.Select(key => new Key<uint>(key.Frame, key.Value, key.TanIn, key.TanOut)).ToArray(),
        };
    }

    private static Vector3 ConvertScale(Vector3 wc3Scale) => new(wc3Scale.X, wc3Scale.Z, wc3Scale.Y);

    private static InterpolationType ConvertInterpolation(War3Net.Modeling.Enums.InterpolationType type) => type switch
    {
        War3Net.Modeling.Enums.InterpolationType.None => InterpolationType.None,
        War3Net.Modeling.Enums.InterpolationType.Linear => InterpolationType.Linear,
        War3Net.Modeling.Enums.InterpolationType.Hermite => InterpolationType.Hermite,
        War3Net.Modeling.Enums.InterpolationType.Bezier => InterpolationType.Bezier,
        _ => InterpolationType.Linear,
    };

    private static int ConvertGlobalSequence(uint id) =>
        id == uint.MaxValue || id > int.MaxValue ? -1 : (int)id;
}
