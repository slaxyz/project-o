using System;
using UnityEngine;
using UnityEngine.UI;

public class FenceUpgradeProject : MonoBehaviour
{
    public const int LevelCount = 5;
    private static readonly string[] LevelNames =
    {
        "FENCE", "VERTICAL LOGS", "REINFORCED LOGS", "STONE WALL", "STEEL WALL"
    };

    [SerializeField] private ResourceInventory inventory;
    [SerializeField] private Transform player;
    [SerializeField] private CampArea campArea;
    [SerializeField] private Transform deliverySpot;
    [SerializeField] private GameObject smallRing;
    [SerializeField] private GameObject largeRing;
    [SerializeField] private int[] woodCosts = { 100, 250, 500, 1000 };
    [SerializeField] private float deliveryRadius = 2.2f;
    [SerializeField] private float deliveryInterval = 0.08f;
    [SerializeField] private int woodPerDelivery = 10;

    private int currentLevel;
    private int deliveredWood;
    private bool projectActive;
    private float nextDeliveryTime;
    private GameObject bubble;
    private Text bubbleText;

    public event Action Changed;
    public int CurrentLevel => currentLevel;
    public string CurrentLevelName => LevelNames[currentLevel];
    public string NextLevelName => currentLevel < LevelNames.Length - 1 ? LevelNames[currentLevel + 1] : "MAX LEVEL";
    public bool IsProjectActive => projectActive;
    public bool IsMaxLevel => currentLevel >= LevelNames.Length - 1;
    public int RequiredWood => IsMaxLevel ? 0 : woodCosts[Mathf.Min(currentLevel, woodCosts.Length - 1)];
    public int DeliveredWood => deliveredWood;

    public void Configure(ResourceInventory newInventory, Transform newPlayer, CampArea newCampArea,
        GameObject newSmallRing, GameObject newLargeRing, Transform newDeliverySpot)
    {
        inventory = newInventory;
        player = newPlayer;
        campArea = newCampArea;
        smallRing = newSmallRing;
        largeRing = newLargeRing;
        deliverySpot = newDeliverySpot;
    }

    private void Start()
    {
        if (inventory == null) inventory = FindFirstObjectByType<ResourceInventory>();
        if (player == null && inventory != null) player = inventory.transform;
        if (campArea == null) campArea = FindFirstObjectByType<CampArea>();
        if (deliverySpot == null) deliverySpot = transform.Find("FenceBuildSpot");
        CreateBubble();
        ApplyFenceLevel();
    }

    public bool StartNextProject()
    {
        if (projectActive || IsMaxLevel) return false;
        deliveredWood = 0;
        projectActive = true;
        Changed?.Invoke();
        return true;
    }

    private void Update()
    {
        UpdateDeliverySpot();
        if (!projectActive || player == null || campArea == null)
        {
            SetBubbleVisible(false);
            return;
        }

        bool nearFence = deliverySpot != null
            && ProximityZone.Contains(deliverySpot.position, player.position, deliveryRadius);
        SetBubbleVisible(nearFence);

        if (nearFence)
        {
            bubble.transform.position = deliverySpot.position + Vector3.up * 2.2f;
            Camera camera = Camera.main;
            if (camera != null) bubble.transform.rotation = camera.transform.rotation;
        }

        if (!nearFence || Time.time < nextDeliveryTime || inventory.GetCarried(ResourceType.Wood) <= 0) return;
        nextDeliveryTime = Time.time + deliveryInterval;
        DeliverWood();
    }

    private void DeliverWood()
    {
        int remaining = RequiredWood - deliveredWood;
        int amount = Mathf.Min(woodPerDelivery, remaining);
        CarriedResourceVisual carry = inventory.GetComponent<CarriedResourceVisual>();
        Vector3 origin = carry != null ? carry.StackTop(ResourceType.Wood) : player.position + Vector3.up;
        int taken = inventory.TryTakeCarried(ResourceType.Wood, amount);
        if (taken <= 0) return;

        deliveredWood += taken;
        ResourceFlyer.SpawnDeposit(origin, bubble.transform.position, ResourceType.Wood, null);
        RefreshBubble();
        Changed?.Invoke();
        if (deliveredWood >= RequiredWood) CompleteProject();
    }

    private void UpdateDeliverySpot()
    {
        if (deliverySpot == null || campArea == null) return;
        float radius = largeRing != null && largeRing.activeSelf ? 11f : 7.25f;
        deliverySpot.position = campArea.Center + Vector3.back * (radius - 1.35f);
    }

    private void CompleteProject()
    {
        currentLevel++;
        projectActive = false;
        deliveredWood = 0;
        SetBubbleVisible(false);
        ApplyFenceLevel();
        GameAudio.PlayBuild();
        FindFirstObjectByType<GameplayHUD>()?.ShowBuilt(LevelNames[currentLevel]);
        Changed?.Invoke();
    }

    private void ApplyFenceLevel()
    {
        ApplyRing(smallRing);
        ApplyRing(largeRing);
    }

    private void ApplyRing(GameObject ring)
    {
        if (ring == null) return;
        for (int index = 0; index < ring.transform.childCount; index++)
        {
            Transform child = ring.transform.GetChild(index);
            if (child.name.StartsWith("Fence_", StringComparison.Ordinal)) ApplySegment(child);
        }

        EnclosureGate gate = ring.GetComponentInChildren<EnclosureGate>(true);
        if (gate == null) return;
        Color tint = LevelColor(currentLevel);
        foreach (Renderer rendererComponent in gate.GetComponentsInChildren<Renderer>(true))
            rendererComponent.sharedMaterial = RuntimeMaterials.Solid(tint);
    }

    private void ApplySegment(Transform segment)
    {
        Transform oldUpgrade = segment.Find("UpgradeVisual");
        if (oldUpgrade != null) Destroy(oldUpgrade.gameObject);

        foreach (Renderer rendererComponent in segment.GetComponentsInChildren<Renderer>(true))
            rendererComponent.enabled = currentLevel == 0;
        if (currentLevel == 0) return;

        Transform root = new GameObject("UpgradeVisual").transform;
        root.SetParent(segment, false);
        Material primary = RuntimeMaterials.Solid(LevelColor(currentLevel));

        if (currentLevel <= 2)
        {
            for (int index = 0; index < 6; index++)
                AddPart(root, PrimitiveType.Cylinder, "Log", new Vector3(-1.04f + index * 0.42f, 0.72f, 0f),
                    new Vector3(0.18f, 0.72f, 0.18f), primary);
            if (currentLevel == 2)
            {
                Material brace = RuntimeMaterials.Solid(new Color(0.22f, 0.16f, 0.11f));
                AddPart(root, PrimitiveType.Cube, "BraceTop", new Vector3(0f, 1.03f, -0.02f), new Vector3(2.5f, 0.16f, 0.2f), brace);
                AddPart(root, PrimitiveType.Cube, "BraceLow", new Vector3(0f, 0.43f, -0.02f), new Vector3(2.5f, 0.16f, 0.2f), brace);
            }
            return;
        }

        Vector3 wallScale = currentLevel == 3 ? new Vector3(2.5f, 1.35f, 0.38f) : new Vector3(2.5f, 1.55f, 0.3f);
        AddPart(root, PrimitiveType.Cube, "Wall", new Vector3(0f, wallScale.y * 0.5f, 0f), wallScale, primary);
    }

    private static void AddPart(Transform parent, PrimitiveType type, string name, Vector3 position,
        Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
    }

    private static Color LevelColor(int level)
    {
        if (level == 1) return new Color(0.5f, 0.27f, 0.1f);
        if (level == 2) return new Color(0.36f, 0.2f, 0.09f);
        if (level == 3) return new Color(0.48f, 0.5f, 0.53f);
        if (level >= 4) return new Color(0.25f, 0.3f, 0.34f);
        return new Color(0.46f, 0.28f, 0.13f);
    }

    private void CreateBubble()
    {
        if (bubble != null) return;
        bubble = new GameObject("FenceDeliveryBubble", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(Image));
        bubble.transform.SetParent(transform, false);
        Canvas canvas = bubble.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 30;
        RectTransform rect = bubble.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(220f, 76f);
        rect.localScale = Vector3.one * 0.008f;
        bubble.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.1f, 0.92f);

        GameObject icon = new GameObject("WoodIcon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(bubble.transform, false);
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(48f, 22f);
        iconRect.anchoredPosition = new Vector2(-76f, 0f);
        icon.GetComponent<Image>().color = ResourceTypes.Tint(ResourceType.Wood);

        GameObject endCap = new GameObject("EndCap", typeof(RectTransform), typeof(Image));
        endCap.transform.SetParent(icon.transform, false);
        RectTransform endCapRect = endCap.GetComponent<RectTransform>();
        endCapRect.sizeDelta = new Vector2(9f, 22f);
        endCapRect.anchoredPosition = new Vector2(19f, 0f);
        endCap.GetComponent<Image>().color = new Color(0.72f, 0.48f, 0.22f);

        GameObject label = new GameObject("Progress", typeof(RectTransform), typeof(Text));
        label.transform.SetParent(bubble.transform, false);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.sizeDelta = new Vector2(140f, 60f);
        labelRect.anchoredPosition = new Vector2(25f, 0f);
        bubbleText = label.GetComponent<Text>();
        bubbleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bubbleText.fontSize = 32;
        bubbleText.fontStyle = FontStyle.Bold;
        bubbleText.alignment = TextAnchor.MiddleCenter;
        bubbleText.color = Color.white;
        RefreshBubble();
        SetBubbleVisible(false);
    }

    private void RefreshBubble()
    {
        if (bubbleText != null) bubbleText.text = deliveredWood + " / " + RequiredWood;
    }

    private void SetBubbleVisible(bool visible)
    {
        if (bubble != null && bubble.activeSelf != visible) bubble.SetActive(visible);
    }
}
