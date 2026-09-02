using UnityEngine;

/// The cabin till. Carried cash bundles fly off the player's back in visual batches
/// and become spendable dollars.
public class CashDeposit : MonoBehaviour
{
    [SerializeField] private ResourceInventory inventory;
    [SerializeField] private Transform dropPoint;
    [SerializeField] private ScalePop cabinPop;
    [SerializeField] private float depositRadius = 2.4f;
    [SerializeField, Min(0.01f)] private float itemInterval = 0.06f;
    [SerializeField, Min(0.1f)] private float maximumUnloadDuration = 3f;
    [SerializeField, Min(1)] private int valuePerBundle = 10;

    private CarriedResourceVisual carryVisual;
    private GameplayHUD hud;
    private float nextItemTime;
    private int depositedThisRun;
    private bool playerWasInside;
    private int unloadBatchSize = 1;
    private float unloadInterval = 0.06f;

    public void Configure(ResourceInventory newInventory, Transform newDropPoint,
        ScalePop newCabinPop, int newValuePerBundle, float newRadius)
    {
        inventory = newInventory;
        dropPoint = newDropPoint;
        cabinPop = newCabinPop;
        valuePerBundle = Mathf.Max(1, newValuePerBundle);
        depositRadius = newRadius;
    }

    private void Awake()
    {
        if (inventory == null) inventory = FindFirstObjectByType<ResourceInventory>();
        if (dropPoint == null) dropPoint = transform;
        if (cabinPop == null) cabinPop = GetComponent<ScalePop>();
    }

    private void Start()
    {
        if (inventory != null) carryVisual = inventory.GetComponent<CarriedResourceVisual>();
        hud = FindFirstObjectByType<GameplayHUD>();
    }

    private void Update()
    {
        if (inventory == null) return;

        bool inside = ProximityZone.Contains(transform.position, inventory.transform.position, depositRadius);
        if (!inside)
        {
            if (playerWasInside) FinishRun();
            playerWasInside = false;
            return;
        }

        if (!playerWasInside) BeginUnload();
        playerWasInside = true;
        if (inventory.GetCarried(ResourceType.Cash) <= 0)
        {
            FinishRun();
            return;
        }

        if (Time.time < nextItemTime) return;
        nextItemTime = Time.time + unloadInterval;
        SendBundleBatch();
    }

    private void BeginUnload()
    {
        int total = inventory.GetCarried(ResourceType.Cash);
        unloadInterval = Mathf.Min(itemInterval, maximumUnloadDuration);
        int maximumBeats = Mathf.Max(1, Mathf.FloorToInt(maximumUnloadDuration / unloadInterval));
        unloadBatchSize = Mathf.Max(1, Mathf.CeilToInt(total / (float)maximumBeats));
        nextItemTime = Time.time;
    }

    private void SendBundleBatch()
    {
        Vector3 origin = carryVisual != null
            ? carryVisual.StackTop(ResourceType.Cash)
            : inventory.transform.position + Vector3.up;

        int taken = inventory.TryTakeCarried(ResourceType.Cash, unloadBatchSize);
        if (taken <= 0) return;

        depositedThisRun += taken;
        float pitch = Mathf.Lerp(0.9f, 1.5f, Mathf.Clamp01(depositedThisRun / 16f));

        ResourceFlyer.SpawnDeposit(origin, dropPoint.position, ResourceType.Cash, () =>
        {
            inventory.AddMoney(valuePerBundle * taken);
            if (cabinPop != null) cabinPop.Pop(0.09f);
            GameAudio.PlayCollect(pitch);
        });
    }

    private void FinishRun()
    {
        if (depositedThisRun <= 0) return;

        int total = depositedThisRun * valuePerBundle;
        depositedThisRun = 0;
        if (cabinPop != null) cabinPop.Pop(0.24f);
        if (hud != null) hud.ShowCashBanked(total);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.35f, 0.72f, 0.36f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, depositRadius);
    }
}
