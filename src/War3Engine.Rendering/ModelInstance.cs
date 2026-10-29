using System.Numerics;
using War3Engine.Core.Animation;
using War3Engine.Core.Models;

namespace War3Engine.Rendering;

/// <summary>Runtime animation state for one placed model.</summary>
public sealed class ModelInstance
{
    public const int MaxBones = 128;

    private readonly Matrix4x4[] _bones;

    public ModelInstance(ModelData model)
    {
        Model = model;
        _bones = new Matrix4x4[model.Nodes.Length];
        SequenceIndex = model.Sequences.Length > 0 ? 0 : -1;
    }

    public ModelData Model { get; }

    public Matrix4x4 World { get; set; } = Matrix4x4.Identity;

    public int SequenceIndex { get; set; }

    public double SequenceTimeMs { get; set; }

    public double GlobalTimeMs { get; private set; }

    public float Speed { get; set; } = 1f;

    public SequenceData? Sequence => SequenceIndex >= 0 && SequenceIndex < Model.Sequences.Length
        ? Model.Sequences[SequenceIndex]
        : null;

    public float SequenceFrame
    {
        get
        {
            var sequence = Sequence;
            if (sequence is null)
            {
                return (float)SequenceTimeMs;
            }

            var length = Math.Max(1u, sequence.EndFrame - sequence.StartFrame);
            var local = sequence.NonLooping
                ? Math.Min(SequenceTimeMs, length)
                : SequenceTimeMs % length;
            return sequence.StartFrame + (float)local;
        }
    }

    public Matrix4x4[] Bones => _bones;

    public void Advance(double deltaSeconds)
    {
        if (Sequence is { } sequence)
        {
            var length = Math.Max(1u, sequence.EndFrame - sequence.StartFrame);
            SequenceTimeMs += deltaSeconds * 1000.0 * Speed;
            if (sequence.NonLooping)
            {
                SequenceTimeMs = Math.Min(SequenceTimeMs, length);
            }
            else
            {
                SequenceTimeMs %= length;
            }
        }

        GlobalTimeMs += deltaSeconds * 1000.0;
    }

    public void Evaluate()
    {
        var worlds = PoseEvaluator.ComputeNodeWorlds(Model, SequenceFrame, (float)GlobalTimeMs);
        Array.Copy(worlds, _bones, Math.Min(worlds.Length, _bones.Length));
    }

    public float TrackFrame<T>(Track<T> track)
        where T : struct =>
        PoseEvaluator.TrackFrame(track, Model, SequenceFrame, (float)GlobalTimeMs);

    public (float Alpha, Vector3 Color) GetGeosetState(int geosetIndex)
    {
        GeosetAnimationData? animation = null;
        foreach (var candidate in Model.GeosetAnimations)
        {
            if (candidate.GeosetId == geosetIndex || candidate.GeosetId < 0)
            {
                animation = candidate;
                if (candidate.GeosetId == geosetIndex)
                {
                    break;
                }
            }
        }

        if (animation is null)
        {
            return (1f, Vector3.One);
        }

        var alpha = animation.Alphas is { } alphas
            ? TrackSampler.Sample(alphas, TrackFrame(alphas))
            : animation.StaticAlpha;
        var color = animation.Colors is { } colors
            ? TrackSampler.Sample(colors, TrackFrame(colors))
            : animation.StaticColor;
        return (alpha, color);
    }
}
