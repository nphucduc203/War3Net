using System.Numerics;

namespace War3Engine.Rendering;

/// <summary>Orbit camera for inspecting a model. Position is in render space (Y up).</summary>
public sealed class OrbitCamera
{
    public Vector3 Target { get; set; } = Vector3.Zero;

    public float Distance { get; set; } = 200f;

    public float Yaw { get; set; } = 0.6f;

    public float Pitch { get; set; } = 0.15f;

    public float FieldOfView { get; set; } = MathF.PI / 4f;

    public float AspectRatio { get; set; } = 16f / 9f;

    public Vector3 Position
    {
        get
        {
            var orbit = new Vector3(
                MathF.Cos(Pitch) * MathF.Sin(Yaw),
                MathF.Sin(Pitch),
                MathF.Cos(Pitch) * MathF.Cos(Yaw));
            return Target + (orbit * Distance);
        }
    }

    public Matrix4x4 View => Matrix4x4.CreateLookAt(Position, Target, Vector3.UnitY);

    public Matrix4x4 Projection => Matrix4x4.CreatePerspectiveFieldOfView(
        FieldOfView,
        AspectRatio,
        MathF.Max(0.05f, Distance * 0.001f),
        MathF.Max(1000f, Distance * 50f));

    public void Orbit(float deltaX, float deltaY)
    {
        Yaw -= deltaX * 0.008f;
        Pitch = Math.Clamp(Pitch + (deltaY * 0.008f), -1.45f, 1.45f);
    }

    public void Zoom(float wheelDelta)
    {
        Distance = Math.Clamp(Distance * MathF.Exp(-wheelDelta * 0.1f), 1f, 1_000_000f);
    }

    public void Frame(Vector3 center, float radius)
    {
        Target = center;
        Distance = MathF.Max(radius * 2.0f, 8f);
    }
}
