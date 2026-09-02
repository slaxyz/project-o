using System.Collections.Generic;
using UnityEngine;

/// The tower of stuff on the player's back. Drawn with instanced meshes rather than
/// GameObjects, so the number of columns is genuinely unlimited: a bag of two
/// thousand logs costs a handful of draw calls instead of two thousand renderers.
public class CarriedResourceVisual : MonoBehaviour
{
    private const int BatchSize = 1023;
    public const int WoodPerVisualLog = 10;

    [SerializeField] private ResourceInventory inventory;
    [SerializeField, Min(1)] private int itemsPerColumn = 25;
    [SerializeField] private float itemSpacing = 0.22f;
    [SerializeField] private float columnSpacing = 0.26f;
    [SerializeField] private float popDuration = 0.24f;
    [SerializeField] private float swayAmount = 5f;

    private sealed class Pile
    {
        public Mesh mesh;
        public Material material;
        public Vector3 itemScale;
        public Vector3 baseEuler;
        public float sideOffset;
        public int firstSlot;
        public readonly List<float> appearTimes = new List<float>();
        public Matrix4x4[] matrices = new Matrix4x4[BatchSize];
        public int shown;
    }

    private readonly Pile[] piles = new Pile[ResourceTypes.Count];
    private Matrix4x4[] batch = new Matrix4x4[BatchSize];
    private Transform stackRoot;

    public int ItemsPerColumn => itemsPerColumn;

    public void SetItemsPerColumn(int amount)
    {
        itemsPerColumn = Mathf.Max(1, amount);
    }

    public void Configure(ResourceInventory newInventory)
    {
        if (inventory != null) inventory.CarriedChanged -= OnCarriedChanged;
        inventory = newInventory;
        if (inventory != null) inventory.CarriedChanged += OnCarriedChanged;
    }

    /// World position of the top of a pile, so a dropped item leaves from where the
    /// player was actually carrying it.
    public Vector3 StackTop(ResourceType type)
    {
        Pile pile = piles[(int)type];
        if (pile == null || stackRoot == null || pile.shown <= 0) return transform.position + Vector3.up;
        return stackRoot.TransformPoint(LocalPosition(pile, pile.firstSlot + pile.shown - 1));
    }

    private void Awake()
    {
        if (inventory == null) inventory = GetComponent<ResourceInventory>();
        BuildPiles();
    }

    private void OnDestroy()
    {
        if (inventory != null) inventory.CarriedChanged -= OnCarriedChanged;
    }

    private void BuildPiles()
    {
        if (!Application.isPlaying || piles[0] != null) return;

        stackRoot = new GameObject("CarriedResources").transform;
        stackRoot.SetParent(transform, false);
        stackRoot.localPosition = new Vector3(0f, 0.32f, -0.58f);

        piles[(int)ResourceType.Wood] = new Pile
        {
            mesh = PrimitiveMesh(PrimitiveType.Cylinder),
            itemScale = new Vector3(0.22f, 0.34f, 0.22f),
            baseEuler = new Vector3(0f, 0f, 90f),
            sideOffset = 0f
        };
        piles[(int)ResourceType.Meat] = new Pile
        {
            mesh = PrimitiveMesh(PrimitiveType.Cube),
            itemScale = new Vector3(0.24f, 0.18f, 0.3f),
            baseEuler = new Vector3(0f, 16f, 0f),
            sideOffset = 0f
        };
        piles[(int)ResourceType.Cash] = new Pile
        {
            mesh = PrimitiveMesh(PrimitiveType.Cube),
            itemScale = new Vector3(0.3f, 0.12f, 0.2f),
            baseEuler = new Vector3(0f, 6f, 0f),
            sideOffset = 0f
        };

        foreach (ResourceType type in ResourceTypes.All)
        {
            piles[(int)type].material = RuntimeMaterials.Solid(ResourceTypes.Tint(type));
        }
    }

    /// Grabs a built-in primitive mesh, then throws the object away. The mesh is an
    /// engine asset and outlives it.
    private static Mesh PrimitiveMesh(PrimitiveType type)
    {
        GameObject temporary = GameObject.CreatePrimitive(type);
        Mesh mesh = temporary.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temporary);
        return mesh;
    }

    private void OnCarriedChanged()
    {
        if (inventory == null || piles[0] == null) return;

        int firstSlot = 0;
        foreach (ResourceType type in ResourceTypes.All)
        {
            Pile pile = piles[(int)type];
            int carried = Mathf.Max(0, inventory.GetCarried(type));
            int target = type == ResourceType.Wood
                ? carried / WoodPerVisualLog
                : carried;
            pile.firstSlot = firstSlot;
            firstSlot += target;

            // Freshly added items remember when they appeared, so they can pop in.
            while (pile.appearTimes.Count < target) pile.appearTimes.Add(Time.time);
            for (int index = pile.shown; index < target && index < pile.appearTimes.Count; index++)
                pile.appearTimes[index] = Time.time;

            pile.shown = target;
        }
    }

    private void Update()
    {
        if (stackRoot == null || piles[0] == null) return;

        // The taller the tower, the more it leans as the player walks.
        int totalShown = 0;
        foreach (ResourceType type in ResourceTypes.All) totalShown += piles[(int)type].shown;
        int tallest = Mathf.Min(itemsPerColumn, totalShown);
        float lean = Mathf.Clamp01(tallest / (float)Mathf.Max(1, itemsPerColumn));
        stackRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 4.5f) * swayAmount * lean);

        Matrix4x4 root = stackRoot.localToWorldMatrix;
        foreach (ResourceType type in ResourceTypes.All)
        {
            Pile pile = piles[(int)type];
            if (pile.shown <= 0) continue;

            if (pile.matrices.Length < pile.shown) pile.matrices = new Matrix4x4[pile.shown];
            for (int index = 0; index < pile.shown; index++)
            {
                float age = Time.time - pile.appearTimes[index];
                float grow = age < popDuration ? Juice.EaseOutBack(age / popDuration) : 1f;

                pile.matrices[index] = root * Matrix4x4.TRS(
                    LocalPosition(pile, pile.firstSlot + index),
                    Quaternion.Euler(pile.baseEuler + new Vector3(0f, Jitter(index) * 24f, 0f)),
                    pile.itemScale * Mathf.Max(0.01f, grow));
            }

            DrawPile(pile);
        }
    }

    private Vector3 LocalPosition(Pile pile, int index)
    {
        int column = index / itemsPerColumn;
        int row = index % itemsPerColumn;
        return new Vector3(
            pile.sideOffset + Jitter(index) * 0.02f,
            (row + 0.5f) * itemSpacing,
            -column * columnSpacing);
    }

    /// Deterministic per slot, in -0.5 to 0.5, so a log never twitches between frames.
    private static float Jitter(int index)
    {
        return Mathf.Repeat(Mathf.Sin(index * 12.9898f) * 43758.5453f, 1f) - 0.5f;
    }

    /// Instanced draws are capped at 1023 matrices, so a very deep pile takes a few
    /// passes over the same array.
    private void DrawPile(Pile pile)
    {
        int drawn = 0;
        while (drawn < pile.shown)
        {
            int count = Mathf.Min(BatchSize, pile.shown - drawn);
            if (drawn == 0)
            {
                Graphics.DrawMeshInstanced(pile.mesh, 0, pile.material, pile.matrices, count);
            }
            else
            {
                if (batch.Length < count) batch = new Matrix4x4[count];
                System.Array.Copy(pile.matrices, drawn, batch, 0, count);
                Graphics.DrawMeshInstanced(pile.mesh, 0, pile.material, batch, count);
            }
            drawn += count;
        }
    }
}
