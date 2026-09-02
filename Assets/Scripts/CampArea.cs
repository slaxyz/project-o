using System.Collections.Generic;
using UnityEngine;

/// Marks the safe camp footprint. Axes retract inside it and wolves refuse to enter.
public class CampArea : MonoBehaviour
{
    private static readonly List<CampArea> Areas = new List<CampArea>();

    [SerializeField] private float radius = 8.4f;

    public float Radius => radius;
    public Vector3 Center => transform.position;

    public void Configure(float newRadius)
    {
        radius = Mathf.Max(1f, newRadius);
    }

    private void OnEnable()
    {
        if (!Areas.Contains(this)) Areas.Add(this);
    }

    private void OnDisable()
    {
        Areas.Remove(this);
    }

    public bool Contains(Vector3 position, float margin = 0f)
    {
        Vector3 offset = position - transform.position;
        offset.y = 0f;
        float limit = radius + margin;
        return offset.sqrMagnitude <= limit * limit;
    }

    public static bool IsInside(Vector3 position, float margin = 0f)
    {
        for (int index = 0; index < Areas.Count; index++)
        {
            if (Areas[index].Contains(position, margin)) return true;
        }
        return false;
    }

    /// Pushes a position out of every camp, used by wolves to keep their distance.
    public static Vector3 ClampOutside(Vector3 position, float margin)
    {
        for (int index = 0; index < Areas.Count; index++)
        {
            CampArea area = Areas[index];
            Vector3 offset = position - area.Center;
            offset.y = 0f;
            float limit = area.radius + margin;
            if (offset.sqrMagnitude >= limit * limit) continue;

            Vector3 direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector3.back;
            float height = position.y;
            position = area.Center + direction * limit;
            position.y = height;
        }
        return position;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.20f, 0.86f, 0.72f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
