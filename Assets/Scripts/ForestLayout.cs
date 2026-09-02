using System.Collections.Generic;
using UnityEngine;

/// Deterministic description of the open ground in the forest: clearings, the small
/// paths that link them and the camp footprint. Purely functional and seeded, so the
/// forest generator and the wolf spawner always agree on where the clearings are
/// without either of them owning the data.
public static class ForestLayout
{
    /// One clearing candidate per cell of this size.
    public const float CellSize = 56f;

    /// Narrow enough to read as a trail cut through the trees, wide enough to walk.
    public const float PathHalfWidth = 1.8f;

    /// Furthest a clearing or a path can reach past its own cell. Callers use it to
    /// size the area they have to collect, instead of scanning a whole cell around.
    public const float MaxOpenReach = 22f;

    private const float ClearingChance = 0.84f;
    private const float MinClearingRadius = 6f;
    private const float MaxClearingRadius = 9.5f;

    private static int seed = 260902;
    private static Vector3 campCenter = Vector3.zero;
    private static float campRadius = 11f;

    public struct Clearing
    {
        public Vector2Int cell;
        public Vector3 center;
        public float radius;
    }

    public struct Path
    {
        public Vector3 start;
        public Vector3 end;
    }

    public static Vector3 CampCenter => campCenter;
    public static float CampRadius => campRadius;

    public static void Configure(int newSeed, Vector3 newCampCenter, float newCampRadius)
    {
        seed = newSeed;
        campCenter = newCampCenter;
        campRadius = Mathf.Max(1f, newCampRadius);
    }

    public static Vector2Int CellOf(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / CellSize),
            Mathf.FloorToInt(position.z / CellSize));
    }

    /// The camp sits in cell zero, so that cell never grows a clearing of its own.
    public static bool TryGetClearing(Vector2Int cell, out Clearing clearing)
    {
        clearing = default;
        if (cell.x == 0 && cell.y == 0) return false;
        if (Random01(cell.x, cell.y, 11) > ClearingChance) return false;

        float offsetX = 0.2f + Random01(cell.x, cell.y, 12) * 0.6f;
        float offsetZ = 0.2f + Random01(cell.x, cell.y, 13) * 0.6f;
        clearing.cell = cell;
        clearing.center = new Vector3((cell.x + offsetX) * CellSize, 0f, (cell.y + offsetZ) * CellSize);
        clearing.radius = Mathf.Lerp(MinClearingRadius, MaxClearingRadius, Random01(cell.x, cell.y, 14));
        return true;
    }

    /// Collects every clearing and path segment that can influence a square area.
    /// Both lists are cleared first, and neither allocates when reused.
    public static void Collect(Vector3 center, float extent, List<Clearing> clearings, List<Path> paths)
    {
        clearings?.Clear();
        paths?.Clear();

        Vector2Int min = CellOf(center - new Vector3(extent, 0f, extent)) - Vector2Int.one;
        Vector2Int max = CellOf(center + new Vector3(extent, 0f, extent)) + Vector2Int.one;

        for (int cellX = min.x; cellX <= max.x; cellX++)
        for (int cellZ = min.y; cellZ <= max.y; cellZ++)
        {
            Vector2Int cell = new Vector2Int(cellX, cellZ);
            bool hasClearing = TryGetClearing(cell, out Clearing clearing);
            if (hasClearing) clearings?.Add(clearing);
            if (paths == null) continue;

            Vector3 origin = hasClearing ? clearing.center : (cell == Vector2Int.zero ? campCenter : Vector3.zero);
            bool originValid = hasClearing || cell == Vector2Int.zero;
            if (!originValid) continue;

            AddLink(cell, origin, new Vector2Int(cellX + 1, cellZ), paths);
            AddLink(cell, origin, new Vector2Int(cellX, cellZ + 1), paths);
            if (cell != Vector2Int.zero) continue;

            // The camp always keeps a way out towards its four neighbours.
            AddLink(cell, origin, new Vector2Int(-1, 0), paths);
            AddLink(cell, origin, new Vector2Int(0, -1), paths);

            // Plus a short trail straight out of the enclosure gate, which faces south,
            // so leaving the camp never means walking into a wall of trunks.
            paths.Add(new Path
            {
                start = campCenter,
                end = campCenter + new Vector3(0f, 0f, -(campRadius + 10f))
            });
        }
    }

    private static void AddLink(Vector2Int fromCell, Vector3 origin, Vector2Int toCell, List<Path> paths)
    {
        Vector3 destination;
        if (toCell == Vector2Int.zero) destination = campCenter;
        else if (TryGetClearing(toCell, out Clearing neighbour)) destination = neighbour.center;
        else return;

        // A jittered midpoint turns the straight link into a bending trail.
        Vector3 middle = (origin + destination) * 0.5f;
        Vector3 side = Vector3.Cross(Vector3.up, (destination - origin).normalized);
        float bend = (Random01(fromCell.x + toCell.x, fromCell.y + toCell.y, 21) - 0.5f) * CellSize * 0.34f;
        middle += side * bend;

        paths.Add(new Path { start = origin, end = middle });
        paths.Add(new Path { start = middle, end = destination });
    }

    /// Distance from a position to the nearest open ground. Negative inside it.
    /// Callers only care about the sign and small positive values, so it returns as
    /// soon as the position is known to be inside something open.
    public static float SignedDistanceToOpenGround(Vector3 position, List<Clearing> clearings, List<Path> paths)
    {
        Vector3 campOffset = position - campCenter;
        campOffset.y = 0f;
        float best = campOffset.magnitude - campRadius;
        if (best <= 0f) return best;

        if (clearings != null)
        {
            for (int index = 0; index < clearings.Count; index++)
            {
                Vector3 offset = position - clearings[index].center;
                offset.y = 0f;
                best = Mathf.Min(best, offset.magnitude - clearings[index].radius);
                if (best <= 0f) return best;
            }
        }

        if (paths != null)
        {
            for (int index = 0; index < paths.Count; index++)
            {
                float distance = DistanceToSegment(position, paths[index].start, paths[index].end);
                best = Mathf.Min(best, distance - PathHalfWidth);
                if (best <= 0f) return best;
            }
        }

        return best;
    }

    /// Clumpiness of the canopy. The forest is meant to be a wall you cannot walk
    /// through, so this stays high everywhere: the noise only decides where the
    /// thicket is merely thick and where it is completely solid.
    public static float DensityAt(Vector3 position)
    {
        float coarse = Mathf.PerlinNoise(position.x * 0.021f + 31.7f, position.z * 0.021f + 12.3f);
        float fine = Mathf.PerlinNoise(position.x * 0.085f + 71.1f, position.z * 0.085f + 5.9f);
        return Mathf.Clamp01(0.78f + coarse * 0.45f + fine * 0.15f - 0.18f);
    }

    private static float DistanceToSegment(Vector3 position, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        segment.y = 0f;
        Vector3 offset = position - start;
        offset.y = 0f;

        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared < 0.0001f) return offset.magnitude;

        float projection = Mathf.Clamp01(Vector3.Dot(offset, segment) / lengthSquared);
        return (offset - segment * projection).magnitude;
    }

    public static float Random01(int x, int y, int salt)
    {
        return (Hash(x, y, salt) & 0xFFFFFF) / (float)0x1000000;
    }

    private static uint Hash(int x, int y, int salt)
    {
        unchecked
        {
            uint hash = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(seed * 83492791) ^ (uint)(salt * 2654435761u);
            hash ^= hash >> 13;
            hash *= 0x5bd1e995;
            hash ^= hash >> 15;
            return hash;
        }
    }
}
