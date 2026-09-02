using UnityEngine;

/// Endless ground made of a small grid of planes that follows the player.
/// The tiles are created at runtime only: building them from an editor script used
/// to leave hundreds of leftover planes saved inside the scene.
public class InfiniteGround : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Material groundMaterial;
    [SerializeField] private float tileSize = 20f;
    [SerializeField, Min(1)] private int tileRadius = 2;

    private Transform tilesRoot;
    private Transform[,] tiles;
    private Vector2Int lastCenter;

    public void Configure(Transform newPlayer, Material newMaterial, float newTileSize, int newTileRadius)
    {
        player = newPlayer;
        groundMaterial = newMaterial;
        tileSize = Mathf.Max(4f, newTileSize);
        tileRadius = Mathf.Max(1, newTileRadius);
        if (Application.isPlaying) BuildTiles();
    }

    private void Start()
    {
        if (player == null)
        {
            PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
            if (movement != null) player = movement.transform;
        }
        if (groundMaterial == null)
        {
            Renderer selfRenderer = GetComponent<Renderer>();
            if (selfRenderer != null) groundMaterial = selfRenderer.sharedMaterial;
        }
        BuildTiles();
    }

    private void Update()
    {
        if (player == null || tiles == null) return;

        Vector2Int center = new Vector2Int(
            Mathf.FloorToInt(player.position.x / tileSize),
            Mathf.FloorToInt(player.position.z / tileSize));
        if (center == lastCenter) return;

        lastCenter = center;
        MoveTiles(center);
    }

    private void BuildTiles()
    {
        if (!Application.isPlaying || player == null || tileSize <= 0f) return;

        DiscardExistingTiles();
        tilesRoot = new GameObject("InfiniteGroundTiles").transform;
        tilesRoot.SetParent(transform, false);

        int tileCount = tileRadius * 2 + 1;
        tiles = new Transform[tileCount, tileCount];
        for (int x = 0; x < tileCount; x++)
        for (int z = 0; z < tileCount; z++)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Plane);
            tile.name = "GroundTile";
            tile.transform.SetParent(tilesRoot, false);
            tile.transform.localScale = Vector3.one * (tileSize / 10f);
            tile.GetComponent<Renderer>().sharedMaterial = groundMaterial;
            tiles[x, z] = tile.transform;
        }

        lastCenter = new Vector2Int(
            Mathf.FloorToInt(player.position.x / tileSize),
            Mathf.FloorToInt(player.position.z / tileSize));
        MoveTiles(lastCenter);
    }

    private void MoveTiles(Vector2Int center)
    {
        for (int x = -tileRadius; x <= tileRadius; x++)
        for (int z = -tileRadius; z <= tileRadius; z++)
        {
            Transform tile = tiles[x + tileRadius, z + tileRadius];
            tile.position = new Vector3((center.x + x) * tileSize, -0.08f, (center.y + z) * tileSize);
        }
    }

    /// Removes any tile root, including ones a previous build may have left behind.
    private void DiscardExistingTiles()
    {
        for (int index = transform.childCount - 1; index >= 0; index--)
        {
            Transform child = transform.GetChild(index);
            if (child.name != "InfiniteGroundTiles") continue;
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
        tilesRoot = null;
        tiles = null;
    }
}
