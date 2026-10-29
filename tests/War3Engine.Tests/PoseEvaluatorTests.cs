using System.Numerics;
using War3Engine.Core;
using War3Engine.Core.Animation;
using War3Engine.Core.Models;
using Xunit;

namespace War3Engine.Tests;

public class PoseEvaluatorTests
{
    [Fact]
    public void RestPoseIsIdentity()
    {
        var model = new ModelData
        {
            Nodes = new[]
            {
                new NodeData { Name = "root", Pivot = new Vector3(10, 20, 30) },
            },
        };

        var worlds = PoseEvaluator.ComputeNodeWorlds(model, 0f, 0f);
        Assert.Equal(Matrix4x4.Identity, worlds[0]);
    }

    [Fact]
    public void TranslationAtTimeAppliesToNodeAndChild()
    {
        var model = new ModelData
        {
            Nodes = new[]
            {
                new NodeData
                {
                    Name = "root",
                    Pivot = new Vector3(5, 0, 0),
                    Translations = new Track<Vector3>
                    {
                        Interpolation = InterpolationType.Linear,
                        Keys = new[]
                        {
                            new Key<Vector3>(0, Vector3.Zero, Vector3.Zero, Vector3.Zero),
                            new Key<Vector3>(1000, new Vector3(0, 10, 0), Vector3.Zero, Vector3.Zero),
                        },
                    },
                },
                new NodeData { Name = "child", ParentIndex = 0, Pivot = new Vector3(0, 1, 0) },
            },
        };

        var worlds = PoseEvaluator.ComputeNodeWorlds(model, 500f, 0f);
        var rootTranslation = worlds[0].Translation;
        Assert.Equal(0f, rootTranslation.X, 4);
        Assert.Equal(5f, rootTranslation.Y, 4);

        // Child rest pose is identity, so it inherits the parent's full transform.
        Assert.Equal(worlds[0], worlds[1]);
    }

    [Fact]
    public void RotationHappensAroundPivot()
    {
        var model = new ModelData
        {
            Nodes = new[]
            {
                new NodeData
                {
                    Name = "arm",
                    Pivot = new Vector3(10, 0, 0),
                    Rotations = new Track<Quaternion>
                    {
                        Interpolation = InterpolationType.None,
                        Keys = new[]
                        {
                            new Key<Quaternion>(0, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f), Quaternion.Identity, Quaternion.Identity),
                        },
                    },
                },
            },
        };

        var worlds = PoseEvaluator.ComputeNodeWorlds(model, 0f, 0f);

        // The pivot itself must not move under its own rotation.
        var pivotPoint = Vector3.Transform(new Vector3(10, 0, 0), worlds[0]);
        Assert.Equal(10f, pivotPoint.X, 4);
        Assert.Equal(0f, pivotPoint.Y, 4);

        // A point 10 units along +X rotates a quarter turn around the pivot.
        var tip = Vector3.Transform(new Vector3(20, 0, 0), worlds[0]);
        Assert.Equal(10f, tip.X, 4);
        Assert.Equal(10f, MathF.Abs(tip.Y), 4);
    }

    private static void AssertClose(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 4);
        Assert.Equal(expected.Y, actual.Y, 4);
        Assert.Equal(expected.Z, actual.Z, 4);
    }

    [Fact]
    public void Wc3SpacePreservesHandednessAndMapsAxes()
    {
        AssertClose(new Vector3(0, 1, 0), Wc3Space.Position(Vector3.UnitZ));
        AssertClose(new Vector3(0, 0, -1), Wc3Space.Position(Vector3.UnitY));
        AssertClose(new Vector3(1, 0, 0), Wc3Space.Position(Vector3.UnitX));

        var rotation = Wc3Space.Rotation(Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI));
        AssertClose(new Vector3(-1, 0, 0), Vector3.Transform(Vector3.UnitX, rotation));
    }
}
