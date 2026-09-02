using System.Collections.Generic;
using UnityEngine;

/// Idle building. It makes wood on its own into a small stock, shows the pile
/// growing on its deck, and pours it into the bag when the player walks over.
public class PassiveProducer : MonoBehaviour
{
    [SerializeField] private ResourceType resourceType = ResourceType.Wood;
    [SerializeField, Min(0.05f)] private float perSecond = 1f;
    [SerializeField, Min(1)] private int stockCap = 40;
    [SerializeField] private float collectRadius = 2.6f;
    [SerializeField, Min(0.01f)] private float collectInterval = 0.05f;
    [SerializeField] private Transform stockRoot;
    [SerializeField] private ScalePop buildingPop;

    private readonly List<Transform> stockItems = new List<Transform>();
    private ResourceInventory player;
    private float stock;
    private int shownStock = -1;
    private float nextCollectTime;

    public int Stock => Mathf.FloorToInt(stock);
    public int StockCap => stockCap;
    public float PerSecond => perSecond;
    public ResourceType Resource => resourceType;

    public void Configure(ResourceType type, float newPerSecond, int newStockCap,
        Transform newStockRoot, ScalePop newPop)
    {
        resourceType = type;
        perSecond = Mathf.Max(0.05f, newPerSecond);
        stockCap = Mathf.Max(1, newStockCap);
        stockRoot = newStockRoot;
        buildingPop = newPop;
    }

    private void Awake()
    {
        if (buildingPop == null) buildingPop = GetComponent<ScalePop>();
    }

    private void Start()
    {
        player = FindFirstObjectByType<ResourceInventory>();
    }

    private void Update()
    {
        if (stock < stockCap) stock = Mathf.Min(stockCap, stock + perSecond * Time.deltaTime);
        RefreshStockVisual();

        if (player == null || Stock <= 0) return;
        if (!ProximityZone.Contains(transform.position, player.transform.position, collectRadius)) return;
        if (player.IsFull || Time.time < nextCollectTime) return;

        nextCollectTime = Time.time + collectInterval;
        stock -= 1f;
        if (buildingPop != null) buildingPop.Pop(0.07f);
        ResourceFlyer.SpawnReward(transform.position + Vector3.up * 0.9f, player, resourceType, 1);
    }

    /// A little pile of logs on the deck, one visual per two units of stock.
    private void RefreshStockVisual()
    {
        if (stockRoot == null) return;

        int wanted = Mathf.Clamp(Stock / 2, 0, 10);
        if (wanted == shownStock) return;
        shownStock = wanted;

        while (stockItems.Count < wanted) stockItems.Add(CreateStockItem(stockItems.Count));
        for (int index = 0; index < stockItems.Count; index++)
        {
            bool visible = index < wanted;
            if (stockItems[index].gameObject.activeSelf != visible) stockItems[index].gameObject.SetActive(visible);
        }
    }

    private Transform CreateStockItem(int index)
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        item.name = "Stock_" + (index + 1).ToString("00");
        item.transform.SetParent(stockRoot, false);
        item.transform.localPosition = new Vector3(
            (index % 2 == 0 ? -0.16f : 0.16f), 0.1f + index / 2 * 0.2f, 0f);
        item.transform.localRotation = Quaternion.Euler(0f, Random.Range(-10f, 10f), 90f);
        item.transform.localScale = new Vector3(0.2f, 0.3f, 0.2f);
        item.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Solid(ResourceTypes.Tint(resourceType));

        Collider itemCollider = item.GetComponent<Collider>();
        if (itemCollider != null) Destroy(itemCollider);
        item.SetActive(false);
        return item.transform;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.86f, 0.62f, 0.24f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, collectRadius);
    }
}
