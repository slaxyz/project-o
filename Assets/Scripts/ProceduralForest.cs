using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Streams an endless forest around the player in chunks.
/// Trees close enough to be hit become real GameObjects with a Harvestable and a
/// collider, everything further away is drawn with instanced meshes. Placement is
/// jittered and driven by noise, and it leaves the clearings and paths described by
/// ForestLayout untouched.
public class ProceduralForest : MonoBehaviour
{
    private const int MaxHarvestedMemory = 8192;

    [SerializeField] private GameObject treePrefab;
    [SerializeField] private Transform player;
    [SerializeField] private Transform camp;
    [SerializeField, Min(8f)] private float chunkSize = 16f;
    [SerializeField, Min(0.4f)] private float treeSpacing = 1.05f;
    [SerializeField, Range(0.1f, 1f)] private float baseDensity = 1f;
    [SerializeField, Min(1f)] private float campClearRadius = 9.5f;
    [SerializeField, Min(0f)] private float edgeSoftness = 1.2f;
    [SerializeField, Min(4f)] private float interactiveRadius = 6f;
    [SerializeField, Min(16f)] private float renderRadius = 30f;
    [SerializeField, Min(16)] private int maxInteractiveTrees = 200;
    [SerializeField, Min(1)] private int maxChunksPerStream = 4;
    [SerializeField, Min(1f)] private float treeHealth = 2f;
    [SerializeField, Min(1)] private int woodPerTree = 1;
    [SerializeField] private int seed = 260902;

    private struct TreeRecord
    {
        public Vector3 position;
        public float yaw;
        public float scale;
        public float health;
        public bool alive;
    }

    private struct Candidate
    {
        public Vector3Int id;
        public float distanceSquared;
    }

    private sealed class TreeVisualPart
    {
        public Mesh mesh;
        public Material material;
        public Matrix4x4 localMatrix;
    }

    private readonly Dictionary<Vector2Int, TreeRecord[]> chunks = new Dictionary<Vector2Int, TreeRecord[]>();
    private readonly Dictionary<Vector3Int, GameObject> liveTrees = new Dictionary<Vector3Int, GameObject>();
    private readonly HashSet<Vector3Int> harvested = new HashSet<Vector3Int>();
    private readonly List<TreeVisualPart> visualParts = new List<TreeVisualPart>();
    private readonly List<ForestLayout.Clearing> clearingBuffer = new List<ForestLayout.Clearing>();
    private readonly List<ForestLayout.Path> pathBuffer = new List<ForestLayout.Path>();
    private readonly List<Candidate> candidates = new List<Candidate>();
    private readonly List<Vector2Int> chunkScratch = new List<Vector2Int>();
    private readonly List<Vector3Int> treeScratch = new List<Vector3Int>();
    private readonly HashSet<Vector3Int> desiredLive = new HashSet<Vector3Int>();

    private List<List<Matrix4x4>>[] batches;
    private Vector3 lastStreamPosition;
    private bool renderDirty;
    private bool pendingStream;
    private bool built;

    public int LoadedChunkCount => chunks.Count;
    public int LiveTreeCount => liveTrees.Count;
    public float RenderRadius => renderRadius;
    public float TreeSpacing => treeSpacing;
    public float CampClearRadius => campClearRadius;

    public void Configure(GameObject newTreePrefab, Transform newPlayer, Transform newCamp,
        float newTreeSpacing, float newCampClearRadius, float newRenderRadius, int newSeed)
    {
        treePrefab = newTreePrefab;
        player = newPlayer;
        camp = newCamp;
        treeSpacing = Mathf.Max(0.4f, newTreeSpacing);
        campClearRadius = Mathf.Max(1f, newCampClearRadius);
        renderRadius = Mathf.Max(16f, newRenderRadius);
        seed = newSeed;
        built = false;
    }

    private void Start()
    {
        if (player == null)
        {
            PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
            if (movement != null) player = movement.transform;
        }
        if (camp == null)
        {
            CampArea area = FindFirstObjectByType<CampArea>();
            if (area != null) camp = area.transform;
        }
        Build();
    }

    private void Build()
    {
        if (treePrefab == null || player == null) return;

        ClearLiveTrees();
        chunks.Clear();
        harvested.Clear();
        CacheVisualParts();

        ForestLayout.Configure(seed, camp != null ? camp.position : Vector3.zero, campClearRadius);
        built = true;
        lastStreamPosition = player.position + Vector3.one * 1000f;
        Stream(player.position, int.MaxValue);
    }

    private void Update()
    {
        if (!built || player == null) return;

        Vector3 position = player.position;
        if (pendingStream || (position - lastStreamPosition).sqrMagnitude > 0.36f)
        {
            lastStreamPosition = position;
            pendingStream = false;
            Stream(position, maxChunksPerStream);
        }

        if (renderDirty) RebuildBatches(position);
        DrawBatches();
    }

    private void Stream(Vector3 playerPosition, int chunkBudget)
    {
        RefreshChunks(playerPosition, chunkBudget);
        RefreshInteractiveTrees(playerPosition);
    }

    private void RefreshChunks(Vector3 playerPosition, int chunkBudget)
    {
        int radiusInChunks = Mathf.CeilToInt(renderRadius / chunkSize);
        Vector2Int center = new Vector2Int(
            Mathf.FloorToInt(playerPosition.x / chunkSize),
            Mathf.FloorToInt(playerPosition.z / chunkSize));

        // The forest is dense, so a chunk is not cheap to build. Spread the work
        // over several frames and come back next frame if the budget runs out.
        int generated = 0;
        for (int x = -radiusInChunks; x <= radiusInChunks && generated < chunkBudget; x++)
        for (int z = -radiusInChunks; z <= radiusInChunks && generated < chunkBudget; z++)
        {
            Vector2Int coord = new Vector2Int(center.x + x, center.y + z);
            if (chunks.ContainsKey(coord)) continue;
            if (ChunkDistance(coord, playerPosition) > renderRadius) continue;

            chunks.Add(coord, GenerateChunk(coord));
            generated++;
            renderDirty = true;
        }
        if (generated >= chunkBudget) pendingStream = true;

        float unloadDistance = renderRadius + chunkSize * 0.5f;
        chunkScratch.Clear();
        foreach (KeyValuePair<Vector2Int, TreeRecord[]> entry in chunks)
        {
            if (ChunkDistance(entry.Key, playerPosition) > unloadDistance) chunkScratch.Add(entry.Key);
        }
        for (int index = 0; index < chunkScratch.Count; index++)
        {
            UnloadChunk(chunkScratch[index]);
            renderDirty = true;
        }

        if (harvested.Count > MaxHarvestedMemory) PruneHarvestedMemory(playerPosition);
    }

    private float ChunkDistance(Vector2Int coord, Vector3 playerPosition)
    {
        Vector3 chunkCenter = new Vector3((coord.x + 0.5f) * chunkSize, 0f, (coord.y + 0.5f) * chunkSize);
        Vector3 offset = chunkCenter - playerPosition;
        offset.y = 0f;
        return offset.magnitude - chunkSize * 0.71f;
    }

    private TreeRecord[] GenerateChunk(Vector2Int coord)
    {
        Vector3 origin = new Vector3(coord.x * chunkSize, 0f, coord.y * chunkSize);
        Vector3 chunkCenter = origin + new Vector3(chunkSize, 0f, chunkSize) * 0.5f;
        ForestLayout.Collect(chunkCenter, chunkSize * 0.5f + ForestLayout.MaxOpenReach, clearingBuffer, pathBuffer);

        int slots = Mathf.Max(1, Mathf.CeilToInt(chunkSize / treeSpacing));
        float step = chunkSize / slots;
        List<TreeRecord> records = new List<TreeRecord>(slots * slots);

        for (int slotX = 0; slotX < slots; slotX++)
        for (int slotZ = 0; slotZ < slots; slotZ++)
        {
            int hashX = coord.x * slots + slotX;
            int hashZ = coord.y * slots + slotZ;

            // A strong jitter is what stops the forest from reading as a regular grid.
            float jitterX = (ForestLayout.Random01(hashX, hashZ, 31) - 0.5f) * 0.62f;
            float jitterZ = (ForestLayout.Random01(hashX, hashZ, 32) - 0.5f) * 0.62f;
            Vector3 position = origin + new Vector3(
                (slotX + 0.5f + jitterX) * step,
                0f,
                (slotZ + 0.5f + jitterZ) * step);

            float openDistance = ForestLayout.SignedDistanceToOpenGround(position, clearingBuffer, pathBuffer);
            if (openDistance <= 0f) continue;

            float density = baseDensity * ForestLayout.DensityAt(position);
            if (openDistance < edgeSoftness) density *= openDistance / edgeSoftness;
            if (ForestLayout.Random01(hashX, hashZ, 33) > density) continue;

            records.Add(new TreeRecord
            {
                position = position,
                yaw = ForestLayout.Random01(hashX, hashZ, 34) * 360f,
                scale = Mathf.Lerp(0.72f, 1.24f, ForestLayout.Random01(hashX, hashZ, 35)),
                health = treeHealth,
                alive = true
            });
        }

        TreeRecord[] chunk = records.ToArray();
        for (int index = 0; index < chunk.Length; index++)
        {
            if (harvested.Contains(new Vector3Int(coord.x, coord.y, index))) chunk[index].alive = false;
        }
        return chunk;
    }

    private void UnloadChunk(Vector2Int coord)
    {
        TreeRecord[] chunk = chunks[coord];
        for (int index = 0; index < chunk.Length; index++)
        {
            Vector3Int id = new Vector3Int(coord.x, coord.y, index);
            if (liveTrees.TryGetValue(id, out GameObject live)) DespawnTree(id, live, chunk);
        }
        chunks.Remove(coord);
    }

    private void RefreshInteractiveTrees(Vector3 playerPosition)
    {
        candidates.Clear();
        float maxDistanceSquared = interactiveRadius * interactiveRadius;

        foreach (KeyValuePair<Vector2Int, TreeRecord[]> entry in chunks)
        {
            if (ChunkDistance(entry.Key, playerPosition) > interactiveRadius) continue;

            TreeRecord[] chunk = entry.Value;
            for (int index = 0; index < chunk.Length; index++)
            {
                if (!chunk[index].alive) continue;
                Vector3 offset = chunk[index].position - playerPosition;
                offset.y = 0f;
                float distanceSquared = offset.sqrMagnitude;
                if (distanceSquared > maxDistanceSquared) continue;

                candidates.Add(new Candidate
                {
                    id = new Vector3Int(entry.Key.x, entry.Key.y, index),
                    distanceSquared = distanceSquared
                });
            }
        }

        if (candidates.Count > maxInteractiveTrees)
        {
            candidates.Sort((first, second) => first.distanceSquared.CompareTo(second.distanceSquared));
        }

        desiredLive.Clear();
        int allowed = Mathf.Min(maxInteractiveTrees, candidates.Count);
        for (int index = 0; index < allowed; index++) desiredLive.Add(candidates[index].id);

        treeScratch.Clear();
        foreach (KeyValuePair<Vector3Int, GameObject> entry in liveTrees)
        {
            if (!desiredLive.Contains(entry.Key)) treeScratch.Add(entry.Key);
        }
        for (int index = 0; index < treeScratch.Count; index++)
        {
            Vector3Int id = treeScratch[index];
            Vector2Int coord = new Vector2Int(id.x, id.y);
            chunks.TryGetValue(coord, out TreeRecord[] chunk);
            DespawnTree(id, liveTrees[id], chunk);
        }

        foreach (Vector3Int id in desiredLive)
        {
            if (liveTrees.ContainsKey(id)) continue;
            SpawnTree(id);
        }
    }

    private void SpawnTree(Vector3Int id)
    {
        Vector2Int coord = new Vector2Int(id.x, id.y);
        if (!chunks.TryGetValue(coord, out TreeRecord[] chunk) || id.z >= chunk.Length) return;

        TreeRecord record = chunk[id.z];
        GameObject tree = Instantiate(treePrefab, record.position, Quaternion.Euler(0f, record.yaw, 0f), transform);
        tree.name = "Tree_" + id.x + "_" + id.y + "_" + id.z;
        tree.transform.localScale = Vector3.one * record.scale;

        Harvestable harvestable = tree.GetComponent<Harvestable>();
        if (harvestable == null) harvestable = tree.AddComponent<Harvestable>();
        harvestable.Initialize(treeHealth, record.health, ResourceType.Wood, woodPerTree);
        harvestable.Died += _ => HandleTreeDied(id);

        liveTrees.Add(id, tree);
        renderDirty = true;
    }

    private void DespawnTree(Vector3Int id, GameObject tree, TreeRecord[] chunk)
    {
        if (tree != null)
        {
            // Keep the damage already dealt, so a half chopped tree stays half chopped.
            Harvestable harvestable = tree.GetComponent<Harvestable>();
            if (harvestable != null && chunk != null && id.z < chunk.Length) chunk[id.z].health = harvestable.Health;
            Destroy(tree);
        }
        liveTrees.Remove(id);
        renderDirty = true;
    }

    private void HandleTreeDied(Vector3Int id)
    {
        Vector2Int coord = new Vector2Int(id.x, id.y);
        if (chunks.TryGetValue(coord, out TreeRecord[] chunk) && id.z < chunk.Length)
        {
            chunk[id.z].alive = false;
            chunk[id.z].health = 0f;
        }
        harvested.Add(id);

        if (liveTrees.TryGetValue(id, out GameObject tree))
        {
            liveTrees.Remove(id);
            if (tree != null) StartCoroutine(SinkTree(tree));
        }
        renderDirty = true;
    }

    /// The tree stretches up, then topples and squashes into the ground.
    private IEnumerator SinkTree(GameObject tree)
    {
        Collider treeCollider = tree.GetComponent<Collider>();
        if (treeCollider != null) treeCollider.enabled = false;

        Transform trunk = tree.transform;
        Vector3 startScale = trunk.localScale;
        Vector3 startPosition = trunk.position;
        Quaternion startRotation = trunk.rotation;
        Vector3 fallAxis = new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f).normalized;
        if (fallAxis.sqrMagnitude < 0.0001f) fallAxis = Vector3.right;
        Quaternion fallRotation = Quaternion.AngleAxis(84f, fallAxis) * startRotation;

        const float snapDuration = 0.09f;
        const float fallDuration = 0.34f;
        float elapsed = 0f;

        // Quick anticipation stretch before it goes.
        while (elapsed < snapDuration && trunk != null)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / snapDuration);
            trunk.localScale = Juice.Squash(startScale, 0.18f * Juice.Impulse(progress));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < fallDuration && trunk != null)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / fallDuration);
            float fall = Juice.EaseInCubic(progress);

            trunk.rotation = Quaternion.Slerp(startRotation, fallRotation, fall);
            trunk.position = startPosition - Vector3.up * (fall * 0.35f);
            trunk.localScale = Vector3.Lerp(startScale, startScale * 0.35f, Juice.EaseInCubic(progress * progress));
            yield return null;
        }

        if (trunk != null) Destroy(tree);
    }

    private void PruneHarvestedMemory(Vector3 playerPosition)
    {
        treeScratch.Clear();
        float limit = renderRadius * 4f;
        foreach (Vector3Int id in harvested)
        {
            if (ChunkDistance(new Vector2Int(id.x, id.y), playerPosition) > limit) treeScratch.Add(id);
        }
        for (int index = 0; index < treeScratch.Count; index++) harvested.Remove(treeScratch[index]);
    }

    private void CacheVisualParts()
    {
        visualParts.Clear();
        MeshFilter[] meshFilters = treePrefab.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter meshFilter in meshFilters)
        {
            MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
            if (meshRenderer == null || meshFilter.sharedMesh == null || meshRenderer.sharedMaterial == null) continue;

            meshRenderer.sharedMaterial.enableInstancing = true;
            visualParts.Add(new TreeVisualPart
            {
                mesh = meshFilter.sharedMesh,
                material = meshRenderer.sharedMaterial,
                localMatrix = Matrix4x4.TRS(meshFilter.transform.localPosition,
                    meshFilter.transform.localRotation, meshFilter.transform.localScale)
            });
        }

        batches = new List<List<Matrix4x4>>[visualParts.Count];
        for (int index = 0; index < batches.Length; index++) batches[index] = new List<List<Matrix4x4>>();
    }

    private void RebuildBatches(Vector3 playerPosition)
    {
        renderDirty = false;
        if (batches == null) return;

        for (int partIndex = 0; partIndex < batches.Length; partIndex++)
        {
            for (int batchIndex = 0; batchIndex < batches[partIndex].Count; batchIndex++)
                batches[partIndex][batchIndex].Clear();
        }

        float maxDistanceSquared = renderRadius * renderRadius;
        foreach (KeyValuePair<Vector2Int, TreeRecord[]> entry in chunks)
        {
            TreeRecord[] chunk = entry.Value;
            for (int index = 0; index < chunk.Length; index++)
            {
                if (!chunk[index].alive) continue;
                Vector3Int id = new Vector3Int(entry.Key.x, entry.Key.y, index);
                if (liveTrees.ContainsKey(id)) continue;

                Vector3 offset = chunk[index].position - playerPosition;
                offset.y = 0f;
                if (offset.sqrMagnitude > maxDistanceSquared) continue;

                Matrix4x4 root = Matrix4x4.TRS(chunk[index].position,
                    Quaternion.Euler(0f, chunk[index].yaw, 0f), Vector3.one * chunk[index].scale);
                for (int partIndex = 0; partIndex < visualParts.Count; partIndex++)
                    AppendMatrix(partIndex, root * visualParts[partIndex].localMatrix);
            }
        }
    }

    private void AppendMatrix(int partIndex, Matrix4x4 matrix)
    {
        List<List<Matrix4x4>> partBatches = batches[partIndex];
        for (int index = 0; index < partBatches.Count; index++)
        {
            if (partBatches[index].Count >= 1023) continue;
            partBatches[index].Add(matrix);
            return;
        }
        partBatches.Add(new List<Matrix4x4>(1023) { matrix });
    }

    private void DrawBatches()
    {
        if (batches == null) return;

        for (int partIndex = 0; partIndex < batches.Length; partIndex++)
        {
            TreeVisualPart part = visualParts[partIndex];
            List<List<Matrix4x4>> partBatches = batches[partIndex];
            for (int batchIndex = 0; batchIndex < partBatches.Count; batchIndex++)
            {
                if (partBatches[batchIndex].Count == 0) continue;
                Graphics.DrawMeshInstanced(part.mesh, 0, part.material, partBatches[batchIndex]);
            }
        }
    }

    private void ClearLiveTrees()
    {
        foreach (KeyValuePair<Vector3Int, GameObject> entry in liveTrees)
        {
            if (entry.Value != null) Destroy(entry.Value);
        }
        liveTrees.Clear();
    }

    private void OnDisable()
    {
        ClearLiveTrees();
    }
}
