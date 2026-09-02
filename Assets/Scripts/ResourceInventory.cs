using System;
using UnityEngine;

/// What the player is carrying, plus the one persistent currency: dollars, earned
/// by selling resources at the camp. Everything is bought with them.
public class ResourceInventory : MonoBehaviour
{
    [SerializeField] private int capacity = 50;
    [SerializeField] private GameplayHUD hud;

    private readonly int[] carried = new int[ResourceTypes.Count];

    /// Raised whenever the carried amounts change, so the carry visual can follow.
    public event Action CarriedChanged;

    public int CarriedTotal { get; private set; }
    public int Money { get; private set; }
    public int Capacity => capacity;
    public bool IsFull => CarriedTotal >= capacity;
    public int FreeSpace => Mathf.Max(0, capacity - CarriedTotal);

    public int GetCarried(ResourceType type) => carried[(int)type];

    public void Configure(GameplayHUD newHud, int newCapacity)
    {
        hud = newHud;
        capacity = Mathf.Max(1, newCapacity);
        Array.Clear(carried, 0, carried.Length);
        CarriedTotal = 0;
        Money = 0;
        EnsureCarryVisual();
        RefreshHud();
        CarriedChanged?.Invoke();
    }

    public void SetCapacity(int newCapacity)
    {
        capacity = Mathf.Max(1, newCapacity);
        CarriedTotal = Mathf.Min(CarriedTotal, capacity);
        RefreshHud();
    }

    private void Start()
    {
        if (hud == null) hud = FindFirstObjectByType<GameplayHUD>();
        EnsureCarryVisual();
        RefreshHud();
        CarriedChanged?.Invoke();
    }

    /// Adds up to amount, clamped by the remaining space. Returns what was taken.
    public int TryAdd(ResourceType type, int amount)
    {
        int added = Mathf.Min(Mathf.Max(0, amount), FreeSpace);
        if (added <= 0) return 0;

        carried[(int)type] += added;
        CarriedTotal += added;
        RefreshHud();
        CarriedChanged?.Invoke();
        if (hud != null) hud.ShowResourceGained(type, added);
        return added;
    }

    /// Removes carried resources without paying for them. The sell counter takes
    /// them out one at a time and pays when each one lands.
    public int TryTakeCarried(ResourceType type, int amount)
    {
        int taken = Mathf.Min(Mathf.Max(0, amount), carried[(int)type]);
        if (taken <= 0) return 0;

        carried[(int)type] -= taken;
        CarriedTotal -= taken;
        RefreshHud();
        CarriedChanged?.Invoke();
        return taken;
    }

    /// The type with the most carried units within a set, so a drop run empties
    /// the big pile first.
    public ResourceType LargestOf(ResourceType[] set)
    {
        ResourceType best = set.Length > 0 ? set[0] : ResourceType.Wood;
        int bestAmount = -1;
        for (int index = 0; index < set.Length; index++)
        {
            int amount = carried[(int)set[index]];
            if (amount <= bestAmount) continue;
            best = set[index];
            bestAmount = amount;
        }
        return best;
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0) return;

        Money += amount;
        RefreshHud();
        if (hud != null) hud.PulseMoney();
    }

    public bool TrySpendMoney(int amount)
    {
        if (amount < 0 || Money < amount) return false;

        Money -= amount;
        RefreshHud();
        return true;
    }

    public void DebugSetMoney(int amount)
    {
        Money = Mathf.Max(0, amount);
        RefreshHud();
        hud?.PulseMoney();
    }

    public void DebugFillWood()
    {
        for (int index = 0; index < carried.Length; index++) carried[index] = 0;
        carried[(int)ResourceType.Wood] = capacity;
        CarriedTotal = capacity;
        RefreshHud();
        CarriedChanged?.Invoke();
    }

    private void RefreshHud()
    {
        if (hud == null) return;

        hud.SetCarried(carried[(int)ResourceType.Wood], carried[(int)ResourceType.Meat],
            carried[(int)ResourceType.Cash], capacity);
        hud.SetWallet(Money);
    }

    private void EnsureCarryVisual()
    {
        CarriedResourceVisual visual = GetComponent<CarriedResourceVisual>();
        if (visual == null) visual = gameObject.AddComponent<CarriedResourceVisual>();
        visual.Configure(this);
    }
}
