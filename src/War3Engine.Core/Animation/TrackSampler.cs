using System.Numerics;
using War3Engine.Core.Models;

namespace War3Engine.Core.Animation;

/// <summary>
/// Samples MDX keyframe tracks. Frames are milliseconds, matching the format.
/// Interpolation math follows the reference semantics in open-realm r_mdx_interpolation.c.
/// </summary>
public static class TrackSampler
{
    public static float Sample(Track<float> track, float frame)
    {
        if (!TryLocate(track, frame, out var i0, out var i1, out var t))
        {
            return 0f;
        }

        var k0 = track.Keys[i0];
        var k1 = track.Keys[i1];
        return track.Interpolation switch
        {
            InterpolationType.None => k0.Value,
            InterpolationType.Linear => k0.Value * (1f - t) + k1.Value * t,
            InterpolationType.Hermite => Hermite(k0.Value, k0.TanOut, k1.TanIn, k1.Value, t),
            InterpolationType.Bezier => Bezier(k0.Value, k0.TanOut, k1.TanIn, k1.Value, t),
            _ => k0.Value,
        };
    }

    public static uint Sample(Track<uint> track, float frame)
    {
        if (!TryLocate(track, frame, out var i0, out var i1, out var t))
        {
            return 0u;
        }

        var k0 = track.Keys[i0];
        var k1 = track.Keys[i1];
        return track.Interpolation switch
        {
            InterpolationType.None => k0.Value,
            InterpolationType.Linear => (uint)(k0.Value * (1f - t) + k1.Value * t),
            _ => k0.Value,
        };
    }

    public static Vector3 Sample(Track<Vector3> track, float frame)
    {
        if (!TryLocate(track, frame, out var i0, out var i1, out var t))
        {
            return Vector3.Zero;
        }

        var k0 = track.Keys[i0];
        var k1 = track.Keys[i1];
        return track.Interpolation switch
        {
            InterpolationType.None => k0.Value,
            InterpolationType.Linear => Vector3.Lerp(k0.Value, k1.Value, t),
            InterpolationType.Hermite => Hermite(k0.Value, k0.TanOut, k1.TanIn, k1.Value, t),
            InterpolationType.Bezier => Bezier(k0.Value, k0.TanOut, k1.TanIn, k1.Value, t),
            _ => k0.Value,
        };
    }

    public static Quaternion Sample(Track<Quaternion> track, float frame)
    {
        if (!TryLocate(track, frame, out var i0, out var i1, out var t))
        {
            return Quaternion.Identity;
        }

        var k0 = track.Keys[i0];
        var k1 = track.Keys[i1];
        return track.Interpolation switch
        {
            InterpolationType.None => k0.Value,
            InterpolationType.Linear => Slerp(k0.Value, k1.Value, t),
            // WC3 uses squad-style interpolation for hermite/bezier quaternion keys (sqlerp in the reference).
            _ => Slerp(Slerp(k0.Value, k1.Value, t), Slerp(k0.TanOut, k1.TanIn, t), 2f * t * (1f - t)),
        };
    }

    private static bool TryLocate<T>(Track<T> track, float frame, out int i0, out int i1, out float t)
        where T : struct
    {
        i0 = 0;
        i1 = 0;
        t = 0f;

        var keys = track.Keys;
        if (keys.Length == 0)
        {
            return false;
        }

        if (keys.Length == 1 || frame <= keys[0].Frame)
        {
            return true;
        }

        var last = keys.Length - 1;
        if (frame >= keys[last].Frame)
        {
            i0 = i1 = last;
            return true;
        }

        var lo = 0;
        var hi = last;
        while (lo + 1 < hi)
        {
            var mid = (lo + hi) / 2;
            if (keys[mid].Frame <= frame)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        i0 = lo;
        i1 = lo + 1;
        var span = keys[i1].Frame - keys[i0].Frame;
        t = span == 0 ? 1f : (frame - keys[i0].Frame) / span;
        return true;
    }

    private static float Hermite(float a, float outTan, float inTan, float b, float t)
    {
        var tt = t * t;
        var f1 = tt * ((2f * t) - 3f) + 1f;
        var f2 = tt * (t - 2f) + t;
        var f3 = tt * (t - 1f);
        var f4 = tt * (3f - (2f * t));
        return (a * f1) + (outTan * f2) + (inTan * f3) + (b * f4);
    }

    private static float Bezier(float a, float outTan, float inTan, float b, float t)
    {
        var inv = 1f - t;
        var inv2 = inv * inv;
        var t2 = t * t;
        var f1 = inv2 * inv;
        var f2 = 3f * t * inv2;
        var f3 = 3f * t2 * inv;
        var f4 = t2 * t;
        return (a * f1) + (outTan * f2) + (inTan * f3) + (b * f4);
    }

    private static Vector3 Hermite(Vector3 a, Vector3 outTan, Vector3 inTan, Vector3 b, float t)
    {
        var tt = t * t;
        var f1 = tt * ((2f * t) - 3f) + 1f;
        var f2 = tt * (t - 2f) + t;
        var f3 = tt * (t - 1f);
        var f4 = tt * (3f - (2f * t));
        return (a * f1) + (outTan * f2) + (inTan * f3) + (b * f4);
    }

    private static Vector3 Bezier(Vector3 a, Vector3 outTan, Vector3 inTan, Vector3 b, float t)
    {
        var inv = 1f - t;
        var inv2 = inv * inv;
        var t2 = t * t;
        var f1 = inv2 * inv;
        var f2 = 3f * t * inv2;
        var f3 = 3f * t2 * inv;
        var f4 = t2 * t;
        return (a * f1) + (outTan * f2) + (inTan * f3) + (b * f4);
    }

    public static Quaternion Slerp(Quaternion a, Quaternion b, float t)
    {
        var cosom = (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z) + (a.W * b.W);
        if (cosom < 0f)
        {
            cosom = -cosom;
            b = -b;
        }

        float scale0;
        float scale1;
        if (1f - cosom > 1e-6f)
        {
            var omega = MathF.Acos(cosom);
            var sinom = MathF.Sin(omega);
            scale0 = MathF.Sin((1f - t) * omega) / sinom;
            scale1 = MathF.Sin(t * omega) / sinom;
        }
        else
        {
            scale0 = 1f - t;
            scale1 = t;
        }

        return Quaternion.Normalize((a * scale0) + (b * scale1));
    }
}
