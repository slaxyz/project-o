using System;
using UnityEngine;

/// Adds extra gates at the fence segment closest to the player. The first gate
/// built by the world counts as the level-one entrance; level two unlocks one more.
public class FenceEntranceBuilder : MonoBehaviour
{
    [SerializeField] private ResourceInventory wallet;
    [SerializeField] private GameplayHUD hud;
    [SerializeField] private Transform player;
    [SerializeField] private CampBase campBase;
    [SerializeField] private CampArea campArea;
    [SerializeField] private GameObject smallRing;
    [SerializeField] private GameObject largeRing;
    [SerializeField] private int[] cashCosts = { 50, 100 };
    [SerializeField] private float placementRadius = 2.4f;
    [SerializeField] private float minimumGateSpacing = 3.2f;

    private int builtEntrances;
    private bool playerIsNearFence;

    public event Action Changed;
    public int BuiltEntrances => builtEntrances;
    public int EntranceLimit => campBase != null ? Mathf.Clamp(campBase.CurrentLevel, 1, 2) : 1;
    public int NextCost => cashCosts.Length == 0 ? 0 : cashCosts[Mathf.Clamp(builtEntrances - 1, 0, cashCosts.Length - 1)];
    public bool CanBuildAtPlayer => !IsAtLimit && playerIsNearFence;
    public bool CanAfford => wallet != null && wallet.Money >= NextCost;
    public bool IsAtLimit => builtEntrances >= EntranceLimit;

    public void Configure(ResourceInventory newWallet, GameplayHUD newHud, Transform newPlayer,
        CampBase newCampBase, CampArea newCampArea, GameObject newSmallRing, GameObject newLargeRing)
    {
        wallet = newWallet;
        hud = newHud;
        player = newPlayer;
        campBase = newCampBase;
        campArea = newCampArea;
        smallRing = newSmallRing;
        largeRing = newLargeRing;
    }

    private void Start()
    {
        if (wallet == null) wallet = FindFirstObjectByType<ResourceInventory>();
        if (hud == null) hud = FindFirstObjectByType<GameplayHUD>();
        if (player == null && wallet != null) player = wallet.transform;
        if (campBase == null) campBase = FindFirstObjectByType<CampBase>();
        if (campArea == null) campArea = FindFirstObjectByType<CampArea>();

        Transform activeRing = ActiveRing();
        builtEntrances = activeRing != null && activeRing.GetComponentInChildren<EnclosureGate>(true) != null ? 1 : 0;
        if (campBase != null) campBase.Upgraded += OnBaseUpgraded;
    }

    private void OnDestroy()
    {
        if (campBase != null) campBase.Upgraded -= OnBaseUpgraded;
    }

    private void Update()
    {
        Transform segment;
        bool wasNearFence = playerIsNearFence;
        playerIsNearFence = TryFindPlacementSegment(out segment);
        if (wasNearFence != playerIsNearFence) Changed?.Invoke();
    }

    public bool TryBuildAtPlayer()
    {
        Transform segment;
        if (!CanBuildAtPlayer || !TryFindPlacementSegment(out segment)) return false;
        if (wallet == null || !wallet.TrySpendMoney(NextCost))
        {
            if (hud != null) hud.ShowNotEnoughMoney(NextCost);
            return false;
        }

        Transform ring = segment.parent;
        float halfWidth = segment.lossyScale.x * 2.5f * 0.5f;
        Vector3 position = segment.position;
        Quaternion rotation = segment.rotation;
        UnityEngine.Object.Destroy(segment.gameObject);
        CreateGate(ring, position, rotation, halfWidth);
        builtEntrances++;
        GameAudio.PlayBuild();
        Changed?.Invoke();
        return true;
    }

    private void OnBaseUpgraded(int level)
    {
        Changed?.Invoke();
    }

    private Transform ActiveRing()
    {
        if (largeRing != null && largeRing.activeSelf) return largeRing.transform;
        return smallRing != null ? smallRing.transform : null;
    }

    private bool TryFindPlacementSegment(out Transform closest)
    {
        closest = null;
        Transform ring = ActiveRing();
        if (ring == null || player == null || campArea == null || IsAtLimit) return false;

        float bestDistance = placementRadius * placementRadius;
        for (int index = 0; index < ring.childCount; index++)
        {
            Transform child = ring.GetChild(index);
            if (!child.name.StartsWith("Fence_", StringComparison.Ordinal)) continue;
            float distance = FlatDistanceSquared(child.position, player.position);
            if (distance >= bestDistance || IsTooCloseToGate(ring, child.position)) continue;
            bestDistance = distance;
            closest = child;
        }
        return closest != null;
    }

    private bool IsTooCloseToGate(Transform ring, Vector3 position)
    {
        EnclosureGate[] gates = ring.GetComponentsInChildren<EnclosureGate>(true);
        for (int index = 0; index < gates.Length; index++)
        {
            if (FlatDistanceSquared(gates[index].transform.position, position)
                < minimumGateSpacing * minimumGateSpacing) return true;
        }
        return false;
    }

    private static float FlatDistanceSquared(Vector3 first, Vector3 second)
    {
        float x = first.x - second.x;
        float z = first.z - second.z;
        return x * x + z * z;
    }

    private static void CreateGate(Transform parent, Vector3 position, Quaternion rotation, float halfWidth)
    {
        GameObject gate = new GameObject("BuiltEntrance");
        gate.transform.SetParent(parent, true);
        gate.transform.SetPositionAndRotation(position, rotation);

        Material material = RuntimeMaterials.Solid(new Color(0.46f, 0.28f, 0.13f));
        AddPart(gate.transform, PrimitiveType.Cube, "PostLeft",
            new Vector3(-halfWidth, 0.78f, 0f), new Vector3(0.24f, 1.56f, 0.24f), material, true);
        AddPart(gate.transform, PrimitiveType.Cube, "PostRight",
            new Vector3(halfWidth, 0.78f, 0f), new Vector3(0.24f, 1.56f, 0.24f), material, true);
        CreateGatePanel(gate.transform, "HingeLeft", -halfWidth, 1f, halfWidth, material);
        CreateGatePanel(gate.transform, "HingeRight", halfWidth, -1f, halfWidth, material);

        EnclosureGate behaviour = gate.AddComponent<EnclosureGate>();
        PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
        behaviour.Configure(gate.transform.Find("HingeLeft"), gate.transform.Find("HingeRight"),
            movement != null ? movement.transform : null, 4.2f);
    }

    private static void CreateGatePanel(Transform parent, string name, float hingeX,
        float direction, float halfWidth, Material material)
    {
        GameObject hinge = new GameObject(name);
        hinge.transform.SetParent(parent, false);
        hinge.transform.localPosition = new Vector3(hingeX, 0f, 0f);
        float panelLength = halfWidth - 0.12f;
        float center = direction * panelLength * 0.5f;
        AddPart(hinge.transform, PrimitiveType.Cube, "RailTop",
            new Vector3(center, 0.74f, 0f), new Vector3(panelLength, 0.16f, 0.14f), material, true);
        AddPart(hinge.transform, PrimitiveType.Cube, "RailLow",
            new Vector3(center, 0.36f, 0f), new Vector3(panelLength, 0.16f, 0.14f), material, true);
        AddPart(hinge.transform, PrimitiveType.Cube, "EndPost",
            new Vector3(direction * panelLength, 0.55f, 0f), new Vector3(0.16f, 1.1f, 0.16f), material, false);
    }

    private static void AddPart(Transform parent, PrimitiveType type, string name, Vector3 position,
        Vector3 scale, Material material, bool withCollider)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        if (!withCollider)
        {
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }
    }
}
