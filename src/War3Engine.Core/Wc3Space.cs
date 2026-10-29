using System.Numerics;

namespace War3Engine.Core;

/// <summary>
/// Converts between WC3 model space (X east, Y north, Z up, right-handed)
/// and MonoGame render space (X right, Y up, Z toward viewer, right-handed).
/// The basis is a -90 degree rotation about X, so handedness and winding are preserved:
/// WC3 (x, y, z) becomes render (x, z, -y).
/// </summary>
public static class Wc3Space
{
    /// <summary>Row-vector basis: render = wc3 * ToRender.</summary>
    public static readonly Matrix4x4 ToRender = Matrix4x4.CreateRotationX(-MathF.PI / 2f);

    public static Vector3 Position(Vector3 wc3) => Vector3.Transform(wc3, ToRender);

    /// <summary>Basis change for a row-vector transform: M' = B^-1 * M * B.</summary>
    public static Matrix4x4 Transform(Matrix4x4 wc3) => Matrix4x4.Transpose(ToRender) * wc3 * ToRender;

    public static Quaternion Rotation(Quaternion wc3) =>
        Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(Transform(Matrix4x4.CreateFromQuaternion(wc3))));

    public static Matrix4x4 Scale(float scale) => Matrix4x4.CreateScale(scale);
}
