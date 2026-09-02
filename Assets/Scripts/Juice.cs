using UnityEngine;

/// Shared easing curves. Everything that moves, grows or lands in the game goes
/// through one of these, so the whole feel stays consistent instead of every
/// script inventing its own lerp.
public static class Juice
{
    /// Overshoots past the target then settles. The default for anything arriving.
    public static float EaseOutBack(float time, float overshoot = 1.7f)
    {
        float clamped = Mathf.Clamp01(time) - 1f;
        return 1f + (overshoot + 1f) * clamped * clamped * clamped + overshoot * clamped * clamped;
    }

    public static float EaseOutCubic(float time)
    {
        float inverted = 1f - Mathf.Clamp01(time);
        return 1f - inverted * inverted * inverted;
    }

    public static float EaseInCubic(float time)
    {
        float clamped = Mathf.Clamp01(time);
        return clamped * clamped * clamped;
    }

    public static float EaseInOutCubic(float time)
    {
        float clamped = Mathf.Clamp01(time);
        return clamped < 0.5f
            ? 4f * clamped * clamped * clamped
            : 1f - Mathf.Pow(-2f * clamped + 2f, 3f) * 0.5f;
    }

    /// Rings a few times and dies out. For impacts.
    public static float DampedWobble(float time, float cycles = 3f)
    {
        float clamped = Mathf.Clamp01(time);
        return Mathf.Sin(clamped * Mathf.PI * cycles) * (1f - clamped);
    }

    /// Zero at both ends, one in the middle. For a single pulse.
    public static float Impulse(float time)
    {
        return Mathf.Sin(Mathf.Clamp01(time) * Mathf.PI);
    }

    /// Squash and stretch around a resting scale. Positive stretches upwards and
    /// pinches the sides, negative squashes down and spreads out.
    public static Vector3 Squash(Vector3 resting, float amount)
    {
        float sides = 1f - amount * 0.5f;
        return new Vector3(resting.x * sides, resting.y * (1f + amount), resting.z * sides);
    }

    /// Arc between two points, peaking at height in the middle.
    public static Vector3 Arc(Vector3 from, Vector3 to, float time, float height)
    {
        float eased = EaseOutCubic(time);
        return Vector3.Lerp(from, to, eased) + Vector3.up * (Impulse(time) * height);
    }
}
