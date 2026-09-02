using UnityEngine;

/// Empty slab unlocked by the level two base upgrade. Standing on it offers the
/// lumber mill; buying it swaps the marker for a working building that makes wood
/// on its own.
public class BuildPlot : MonoBehaviour
{
    [SerializeField] private ResourceInventory wallet;
    [SerializeField] private Transform player;
    [SerializeField] private ActionPrompt prompt;
    [SerializeField] private GameplayHUD hud;
    [SerializeField] private GameObject emptyMarker;
    [SerializeField] private GameObject buildingPrefab;
    [SerializeField] private ScalePop markerPop;
    [SerializeField] private string buildingLabel = "LUMBER MILL";
    [SerializeField] private int price = 400;
    [SerializeField] private float woodPerSecond = 1f;
    [SerializeField] private int stockCap = 40;
    [SerializeField] private float interactionRadius = 2.6f;

    private GameObject building;
    private PassiveProducer producer;
    private bool unlocked;
    private bool playerWasInside;

    public bool IsBuilt => building != null;

    public void Configure(ResourceInventory newWallet, Transform newPlayer, ActionPrompt newPrompt,
        GameplayHUD newHud, GameObject newEmptyMarker, GameObject newBuildingPrefab, ScalePop newMarkerPop, int newPrice)
    {
        wallet = newWallet;
        player = newPlayer;
        prompt = newPrompt;
        hud = newHud;
        emptyMarker = newEmptyMarker;
        buildingPrefab = newBuildingPrefab;
        markerPop = newMarkerPop;
        price = newPrice;
    }

    /// No Awake: the plot starts as an inactive object, which already means locked.
    /// Deactivating from Awake would undo the activation that just woke it up.
    private void Start()
    {
        if (player == null)
        {
            PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
            if (movement != null) player = movement.transform;
        }
        if (wallet == null) wallet = FindFirstObjectByType<ResourceInventory>();
        if (hud == null) hud = FindFirstObjectByType<GameplayHUD>();
        if (prompt != null) prompt.Clicked += TryBuild;
    }

    private void OnDestroy()
    {
        if (prompt != null) prompt.Clicked -= TryBuild;
    }

    /// Called by the base when the park is enlarged.
    public void SetUnlocked(bool isUnlocked)
    {
        unlocked = isUnlocked;
        gameObject.SetActive(isUnlocked);
        if (isUnlocked && markerPop != null) markerPop.Pop(0.4f);
    }

    private void Update()
    {
        if (!unlocked || player == null || prompt == null) return;

        bool inside = ProximityZone.Contains(transform.position, player.position, interactionRadius);
        if (!inside)
        {
            if (playerWasInside) prompt.Hide();
            playerWasInside = false;
            return;
        }

        playerWasInside = true;
        if (building != null)
        {
            // Already built: the panel becomes a readout of what it is producing.
            int stock = producer != null ? producer.Stock : 0;
            prompt.Show(buildingLabel,
                "+" + woodPerSecond.ToString("0.#") + " WOOD / S",
                "STOCK  " + stock + " / " + stockCap, false);
            return;
        }

        bool affordable = wallet != null && wallet.Money >= price;
        prompt.Show("BUILD " + buildingLabel,
            "+" + woodPerSecond.ToString("0.#") + " WOOD / S, PASSIVE",
            "$" + price, affordable);
    }

    private void TryBuild()
    {
        if (!unlocked || building != null || !playerWasInside || wallet == null) return;
        if (!wallet.TrySpendMoney(price))
        {
            if (hud != null) hud.ShowNotEnoughMoney(price);
            return;
        }

        building = buildingPrefab != null
            ? Instantiate(buildingPrefab, transform.position, transform.rotation, transform.parent)
            : null;
        if (emptyMarker != null) emptyMarker.SetActive(false);

        if (building != null)
        {
            building.name = "LumberMill";
            producer = building.GetComponent<PassiveProducer>();
            ScalePop pop = building.GetComponent<ScalePop>();
            if (pop == null) pop = building.AddComponent<ScalePop>();
            if (producer == null) producer = building.AddComponent<PassiveProducer>();
            producer.Configure(ResourceType.Wood, woodPerSecond, stockCap,
                building.transform.Find("Stock"), pop);
            pop.Pop(0.5f);
        }

        GameAudio.PlayBuild();
        if (hud != null) hud.ShowBuilt(buildingLabel);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.98f, 0.78f, 0.22f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
