using System.Collections.Generic;
using UnityEngine;

/// Keeps a few wolf packs alive around the player. Packs only appear in the clearings
/// described by ForestLayout, always three wolves at a time, and a cleared clearing
/// stays quiet for a while before a new pack moves in.
public class WolfPackSpawner : MonoBehaviour
{
    [SerializeField] private GameObject wolfPrefab;
    [SerializeField] private Transform player;
    [SerializeField, Min(1)] private int packSize = 3;
    [SerializeField, Min(1)] private int maxActivePacks = 3;
    [SerializeField, Min(4f)] private float minSpawnDistance = 26f;
    [SerializeField, Min(8f)] private float maxSpawnDistance = 90f;
    [SerializeField, Min(16f)] private float despawnDistance = 150f;
    [SerializeField, Min(1f)] private float respawnDelay = 40f;
    [SerializeField, Min(0.2f)] private float scanInterval = 1.1f;
    [SerializeField, Min(1f)] private float wolfHealth = 3f;
    [SerializeField, Min(1)] private int meatPerWolf = 3;

    private sealed class Pack
    {
        public Vector2Int cell;
        public Vector3 center;
        public float radius;
        public readonly List<WolfAgent> wolves = new List<WolfAgent>();
    }

    private readonly Dictionary<Vector2Int, Pack> packs = new Dictionary<Vector2Int, Pack>();
    private readonly Dictionary<Vector2Int, float> quietUntil = new Dictionary<Vector2Int, float>();
    private readonly List<ForestLayout.Clearing> clearingBuffer = new List<ForestLayout.Clearing>();
    private readonly List<Vector2Int> packScratch = new List<Vector2Int>();
    private float nextScanTime;

    public int ActivePackCount => packs.Count;

    public int ActiveWolfCount
    {
        get
        {
            int total = 0;
            foreach (KeyValuePair<Vector2Int, Pack> entry in packs) total += entry.Value.wolves.Count;
            return total;
        }
    }

    public bool DebugSpawnAnimal()
    {
        if (!Application.isPlaying || wolfPrefab == null || player == null) return false;

        Vector3 position = player.position + player.forward * 5f;
        position.y = 0f;
        List<WolfAgent> debugPack = new List<WolfAgent>();
        GameObject instance = Instantiate(wolfPrefab, position,
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
        instance.name = "DebugTiger";

        WolfAgent animal = instance.GetComponent<WolfAgent>();
        if (animal == null) animal = instance.AddComponent<WolfAgent>();
        animal.Configure(position, 6f, debugPack, player, wolfHealth, meatPerWolf);
        debugPack.Add(animal);
        return true;
    }

    public void Configure(GameObject newWolfPrefab, Transform newPlayer, int newPackSize, int newMaxActivePacks)
    {
        wolfPrefab = newWolfPrefab;
        player = newPlayer;
        packSize = Mathf.Max(1, newPackSize);
        maxActivePacks = Mathf.Max(1, newMaxActivePacks);
    }

    private void Start()
    {
        if (player != null) return;
        PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
        if (movement != null) player = movement.transform;
    }

    private void Update()
    {
        if (wolfPrefab == null || player == null || Time.time < nextScanTime) return;
        nextScanTime = Time.time + scanInterval;

        DespawnDistantPacks();
        SpawnNearbyPacks();
    }

    private void DespawnDistantPacks()
    {
        packScratch.Clear();
        foreach (KeyValuePair<Vector2Int, Pack> entry in packs)
        {
            Pack pack = entry.Value;
            pack.wolves.RemoveAll(wolf => wolf == null || !wolf.IsAlive);

            if (pack.wolves.Count == 0)
            {
                packScratch.Add(entry.Key);
                quietUntil[entry.Key] = Time.time + respawnDelay;
                continue;
            }

            if (PlanarDistance(pack.center, player.position) > despawnDistance) packScratch.Add(entry.Key);
        }

        for (int index = 0; index < packScratch.Count; index++)
        {
            Vector2Int cell = packScratch[index];
            Pack pack = packs[cell];
            for (int wolfIndex = 0; wolfIndex < pack.wolves.Count; wolfIndex++)
            {
                if (pack.wolves[wolfIndex] != null) Destroy(pack.wolves[wolfIndex].gameObject);
            }
            packs.Remove(cell);
        }
    }

    private void SpawnNearbyPacks()
    {
        if (packs.Count >= maxActivePacks) return;

        ForestLayout.Collect(player.position, maxSpawnDistance, clearingBuffer, null);
        for (int index = 0; index < clearingBuffer.Count && packs.Count < maxActivePacks; index++)
        {
            ForestLayout.Clearing clearing = clearingBuffer[index];
            if (packs.ContainsKey(clearing.cell)) continue;
            if (quietUntil.TryGetValue(clearing.cell, out float readyAt) && Time.time < readyAt) continue;

            float distance = PlanarDistance(clearing.center, player.position);
            if (distance < minSpawnDistance || distance > maxSpawnDistance) continue;

            packs.Add(clearing.cell, SpawnPack(clearing));
        }
    }

    private Pack SpawnPack(ForestLayout.Clearing clearing)
    {
        Pack pack = new Pack { cell = clearing.cell, center = clearing.center, radius = clearing.radius };
        float angleOffset = Random.value * Mathf.PI * 2f;

        for (int index = 0; index < packSize; index++)
        {
            float angle = angleOffset + index / (float)packSize * Mathf.PI * 2f;
            float radius = clearing.radius * Random.Range(0.28f, 0.62f);
            Vector3 position = clearing.center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            GameObject instance = Instantiate(wolfPrefab, position,
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
            instance.name = "Wolf_" + clearing.cell.x + "_" + clearing.cell.y + "_" + (index + 1);

            WolfAgent wolf = instance.GetComponent<WolfAgent>();
            if (wolf == null) wolf = instance.AddComponent<WolfAgent>();
            wolf.Configure(clearing.center, clearing.radius, pack.wolves, player,
                wolfHealth, meatPerWolf);
            pack.wolves.Add(wolf);
        }

        return pack;
    }

    private static float PlanarDistance(Vector3 first, Vector3 second)
    {
        float x = first.x - second.x;
        float z = first.z - second.z;
        return Mathf.Sqrt(x * x + z * z);
    }

    private void OnDrawGizmosSelected()
    {
        if (player == null) return;
        Gizmos.color = new Color(0.86f, 0.32f, 0.32f, 0.3f);
        Gizmos.DrawWireSphere(player.position, maxSpawnDistance);
        Gizmos.color = new Color(0.32f, 0.62f, 0.86f, 0.3f);
        Gizmos.DrawWireSphere(player.position, minSpawnDistance);
    }
}
