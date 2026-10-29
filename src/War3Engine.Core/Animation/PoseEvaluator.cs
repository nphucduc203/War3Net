using System.Numerics;
using War3Engine.Core.Models;

namespace War3Engine.Core.Animation;

/// <summary>
/// Evaluates the MDX node hierarchy into per-node world matrices for a given time.
/// Matrices are in WC3 model space (Z up) until converted by <see cref="Wc3Space"/>.
/// </summary>
public static class PoseEvaluator
{
    public static Matrix4x4[] ComputeNodeWorlds(ModelData model, float sequenceFrame, float globalFrame)
    {
        var world = new Matrix4x4[model.Nodes.Length];
        var visited = new bool[world.Length];

        void Visit(int index)
        {
            if (visited[index])
            {
                return;
            }

            visited[index] = true;
            var node = model.Nodes[index];
            var local = BuildLocal(node, model, sequenceFrame, globalFrame);
            if (node.ParentIndex >= 0 && node.ParentIndex < world.Length)
            {
                Visit(node.ParentIndex);
                world[index] = local * world[node.ParentIndex];
            }
            else
            {
                world[index] = local;
            }
        }

        for (var i = 0; i < world.Length; i++)
        {
            Visit(i);
        }

        return world;
    }

    public static Matrix4x4 BuildLocal(NodeData node, ModelData model, float sequenceFrame, float globalFrame)
    {
        var translation = Vector3.Zero;
        if (node.Translations is { Keys.Length: > 0 } translations)
        {
            translation = TrackSampler.Sample(translations, TrackFrame(translations, model, sequenceFrame, globalFrame));
        }

        var rotation = Quaternion.Identity;
        if (node.Rotations is { Keys.Length: > 0 } rotations)
        {
            rotation = TrackSampler.Sample(rotations, TrackFrame(rotations, model, sequenceFrame, globalFrame));
        }

        var scaling = Vector3.One;
        if (node.Scalings is { Keys.Length: > 0 } scalings)
        {
            scaling = TrackSampler.Sample(scalings, TrackFrame(scalings, model, sequenceFrame, globalFrame));
        }

        var pivot = node.Pivot;
        return Matrix4x4.CreateTranslation(-pivot)
               * Matrix4x4.CreateScale(scaling)
               * Matrix4x4.CreateFromQuaternion(rotation)
               * Matrix4x4.CreateTranslation(pivot + translation);
    }

    public static float TrackFrame<T>(Track<T> track, ModelData model, float sequenceFrame, float globalFrame)
        where T : struct
    {
        if (track.GlobalSequenceId < 0 || track.GlobalSequenceId >= model.GlobalSequenceDurations.Length)
        {
            return sequenceFrame;
        }

        var duration = model.GlobalSequenceDurations[track.GlobalSequenceId];
        return duration <= 0 ? 0f : globalFrame % duration;
    }
}
