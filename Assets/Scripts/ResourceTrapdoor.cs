using UnityEngine;

/// Hatch in the ground. It swings open when the player stands in front of it, and
/// the bag empties into it in readable visual batches. Value builds up inside and comes
/// back out as bundles of cash that land on the player's back, to be carried to
/// the cabin.
public class ResourceTrapdoor : MonoBehaviour
{
    [SerializeField] private ResourceInventory inventory;
    [SerializeField] private Transform leftFlap;
    [SerializeField] private Transform rightFlap;
    [SerializeField] private Transform dropPoint;
    [SerializeField] private Transform cashPoint;
    [SerializeField] private ScalePop framePop;
    [SerializeField] private float triggerRadius = 2.4f;
    [SerializeField, Min(0.01f)] private float itemInterval = 0.05f;
    [SerializeField, Min(0.1f)] private float maximumUnloadDuration = 3f;
    [SerializeField, Range(20f, 130f)] private float openAngle = 104f;
    [SerializeField, Min(30f)] private float angularSpeed = 300f;
    [SerializeField] private int woodPrice = 1;
    [SerializeField] private int meatPrice = 3;
    [SerializeField, Min(1)] private int cashPerBundle = 10;

    private CarriedResourceVisual carryVisual;
    private GameplayHUD hud;
    private FenceUpgradeProject fenceProject;
    private float openness;
    private bool isOpen;
    private float nextItemTime;
    private int pendingValue;
    private int droppedThisRun;
    private int unloadBatchSize = 1;
    private float unloadInterval = 0.05f;

    public bool IsOpen => openness > 0.98f;
    public int CashPerBundle => cashPerBundle;

    public int PriceOf(ResourceType type) => type == ResourceType.Meat ? meatPrice : woodPrice;

    public void Configure(ResourceInventory newInventory, Transform newLeftFlap, Transform newRightFlap,
        Transform newDropPoint, Transform newCashPoint, ScalePop newFramePop, float newRadius)
    {
        inventory = newInventory;
        leftFlap = newLeftFlap;
        rightFlap = newRightFlap;
        dropPoint = newDropPoint;
        cashPoint = newCashPoint;
        framePop = newFramePop;
        triggerRadius = newRadius;
        openness = 0f;
        ApplyFlaps();
    }

    private void Awake()
    {
        if (inventory == null) inventory = FindFirstObjectByType<ResourceInventory>();
        if (dropPoint == null) dropPoint = transform;
        if (cashPoint == null) cashPoint = dropPoint;
        ApplyFlaps();
    }

    private void Start()
    {
        if (inventory != null) carryVisual = inventory.GetComponent<CarriedResourceVisual>();
        hud = FindFirstObjectByType<GameplayHUD>();
        fenceProject = FindFirstObjectByType<FenceUpgradeProject>();
    }

    private void Update()
    {
        if (inventory == null) return;

        bool playerIsClose = ProximityZone.Contains(transform.position, inventory.transform.position, triggerRadius);
        bool shouldOpen = playerIsClose && CarriedSellable() > 0;

        if (shouldOpen != isOpen)
        {
            isOpen = shouldOpen;
            if (isOpen)
            {
                BeginUnload();
                if (framePop != null) framePop.Pop(0.16f);
            }
            if (!isOpen) FinishRun();
        }

        float target = isOpen ? 1f : 0f;
        if (!Mathf.Approximately(openness, target))
        {
            openness = Mathf.MoveTowards(openness, target, angularSpeed / Mathf.Max(1f, openAngle) * Time.deltaTime);
            ApplyFlaps();
        }

        // Nothing falls in until the hatch is actually out of the way.
        if (!IsOpen || Time.time < nextItemTime) return;
        nextItemTime = Time.time + unloadInterval;
        DropBatch();
    }

    private int CarriedSellable()
    {
        int total = 0;
        for (int index = 0; index < ResourceTypes.Sellable.Length; index++)
        {
            ResourceType type = ResourceTypes.Sellable[index];
            if (type == ResourceType.Wood && fenceProject != null && fenceProject.IsProjectActive) continue;
            total += inventory.GetCarried(type);
        }
        return total;
    }

    private void BeginUnload()
    {
        int total = CarriedSellable();
        unloadInterval = Mathf.Min(itemInterval, maximumUnloadDuration);
        float openingDuration = openAngle / Mathf.Max(angularSpeed, 0.01f);
        float availableUnloadDuration = Mathf.Max(unloadInterval, maximumUnloadDuration - openingDuration);
        int maximumBeats = Mathf.Max(1, Mathf.FloorToInt(availableUnloadDuration / unloadInterval));
        unloadBatchSize = Mathf.Max(1, Mathf.CeilToInt(total / (float)maximumBeats));
        nextItemTime = Time.time;
    }

    private void DropBatch()
    {
        int remainingInBatch = unloadBatchSize;
        while (remainingInBatch > 0 && CarriedSellable() > 0)
        {
            ResourceType type = fenceProject != null && fenceProject.IsProjectActive
                ? ResourceType.Meat
                : inventory.LargestOf(ResourceTypes.Sellable);
            Vector3 origin = carryVisual != null
                ? carryVisual.StackTop(type)
                : inventory.transform.position + Vector3.up;

            int taken = inventory.TryTakeCarried(type, remainingInBatch);
            if (taken <= 0) break;
            remainingInBatch -= taken;

            int value = PriceOf(type) * taken;
            droppedThisRun += taken;
            float pitch = Mathf.Lerp(0.85f, 1.45f, Mathf.Clamp01(droppedThisRun / 24f));

            ResourceFlyer.SpawnDeposit(origin, dropPoint.position, type, () =>
            {
                if (framePop != null) framePop.Pop(0.06f);
                GameAudio.PlayCollect(pitch);
                AccumulateCash(value);
            });
        }
    }

    /// Items are worth a dollar or three, too little to read one by one, so the
    /// hatch pays out in bundles.
    private void AccumulateCash(int value)
    {
        pendingValue += value;
        while (pendingValue >= cashPerBundle)
        {
            pendingValue -= cashPerBundle;
            ReleaseBundle();
        }
    }

    private void ReleaseBundle()
    {
        // A full bag of cash means the cabin is overdue. Hold the value instead of
        // dropping it on the floor.
        if (inventory.FreeSpace <= 0)
        {
            pendingValue += cashPerBundle;
            return;
        }
        ResourceFlyer.SpawnReward(cashPoint.position, inventory, ResourceType.Cash, 1);
    }

    private void FinishRun()
    {
        if (droppedThisRun <= 0) return;

        int total = droppedThisRun;
        droppedThisRun = 0;
        if (framePop != null) framePop.Pop(0.22f);
        if (hud != null) hud.ShowDropped(total);
    }

    private void ApplyFlaps()
    {
        float angle = openAngle * Juice.EaseOutBack(openness, 1.1f);
        if (leftFlap != null) leftFlap.localRotation = Quaternion.Euler(0f, 0f, angle);
        if (rightFlap != null) rightFlap.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.36f, 0.86f, 0.42f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}
