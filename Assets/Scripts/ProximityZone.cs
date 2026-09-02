using UnityEngine;

/// Shared "is the player standing here" test, so every interactive spot in the camp
/// measures distance the same way.
public static class ProximityZone
{
    public static bool Contains(Vector3 center, Vector3 playerPosition, float radius)
    {
        float x = playerPosition.x - center.x;
        float z = playerPosition.z - center.z;
        return x * x + z * z <= radius * radius;
    }
}
