using System.Numerics;
using War3Engine.Core.Animation;
using War3Engine.Core.Models;
using Xunit;

namespace War3Engine.Tests;

public class TrackSamplerTests
{
    [Fact]
    public void LinearScalarInterpolates()
    {
        var track = new Track<float>
        {
            Interpolation = InterpolationType.Linear,
            Keys = new[]
            {
                new Key<float>(0, 0f, 0f, 0f),
                new Key<float>(1000, 10f, 0f, 0f),
            },
        };

        Assert.Equal(5f, TrackSampler.Sample(track, 500f), 4);
        Assert.Equal(0f, TrackSampler.Sample(track, -100f), 4);
        Assert.Equal(10f, TrackSampler.Sample(track, 5000f), 4);
    }

    [Fact]
    public void StepScalarHoldsPreviousKey()
    {
        var track = new Track<float>
        {
            Interpolation = InterpolationType.None,
            Keys = new[]
            {
                new Key<float>(0, 1f, 0f, 0f),
                new Key<float>(100, 2f, 0f, 0f),
            },
        };

        Assert.Equal(1f, TrackSampler.Sample(track, 99f));
        Assert.Equal(2f, TrackSampler.Sample(track, 100f));
    }

    [Fact]
    public void HermiteWithZeroTangentsMatchesSmoothstep()
    {
        var track = new Track<float>
        {
            Interpolation = InterpolationType.Hermite,
            Keys = new[]
            {
                new Key<float>(0, 0f, 0f, 0f),
                new Key<float>(1000, 1f, 0f, 0f),
            },
        };

        // Hermite with zero tangents equals smoothstep: 3t^2 - 2t^3.
        Assert.Equal(0.15625f, TrackSampler.Sample(track, 250f), 4);
        Assert.Equal(0.5f, TrackSampler.Sample(track, 500f), 4);
        Assert.Equal(0.84375f, TrackSampler.Sample(track, 750f), 4);
    }

    [Fact]
    public void BezierUsesInputAndOutputTangents()
    {
        var track = new Track<float>
        {
            Interpolation = InterpolationType.Bezier,
            Keys = new[]
            {
                new Key<float>(0, 0f, 0f, 3f),
                new Key<float>(1000, 0f, 3f, 0f),
            },
        };

        // Cubic Bezier with control points 0, 3, 3, 0 peaks at 2.25 at t=0.5.
        Assert.Equal(2.25f, TrackSampler.Sample(track, 500f), 4);
    }

    [Fact]
    public void VectorLinearInterpolatesComponents()
    {
        var track = new Track<Vector3>
        {
            Interpolation = InterpolationType.Linear,
            Keys = new[]
            {
                new Key<Vector3>(0, Vector3.Zero, Vector3.Zero, Vector3.Zero),
                new Key<Vector3>(100, new Vector3(10, 20, 30), Vector3.Zero, Vector3.Zero),
            },
        };

        var value = TrackSampler.Sample(track, 50f);
        Assert.Equal(5f, value.X, 4);
        Assert.Equal(10f, value.Y, 4);
        Assert.Equal(15f, value.Z, 4);
    }

    [Fact]
    public void QuaternionSlerpProducesMidpointRotation()
    {
        var a = Quaternion.Identity;
        var b = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI);
        var track = new Track<Quaternion>
        {
            Interpolation = InterpolationType.Linear,
            Keys = new[]
            {
                new Key<Quaternion>(0, a, a, a),
                new Key<Quaternion>(100, b, b, b),
            },
        };

        var mid = TrackSampler.Sample(track, 50f);
        var rotated = Vector3.Transform(Vector3.UnitX, mid);
        // Halfway between 0 and 180 degrees is a 90 degree rotation: X maps onto +/-Y.
        Assert.Equal(0f, rotated.X, 4);
        Assert.Equal(1f, MathF.Abs(rotated.Y), 4);
    }

    [Fact]
    public void GlobalSequenceWrapsInsidePoseEvaluator()
    {
        var model = new ModelData
        {
            GlobalSequenceDurations = new[] { 1000 },
        };

        var track = new Track<float> { GlobalSequenceId = 0 };
        Assert.Equal(250f, PoseEvaluator.TrackFrame(track, model, sequenceFrame: 9999f, globalFrame: 2250f));

        var localTrack = new Track<float>();
        Assert.Equal(9999f, PoseEvaluator.TrackFrame(localTrack, model, sequenceFrame: 9999f, globalFrame: 2250f));
    }
}
