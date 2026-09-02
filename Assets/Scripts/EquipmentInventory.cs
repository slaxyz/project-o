using System;
using UnityEngine;

/// What the player owns and what is currently equipped. No levels anywhere: a tool
/// or a bag is bought once, and buying the next one is the upgrade.
public class EquipmentInventory : MonoBehaviour
{
    [SerializeField] private ResourceInventory wallet;

    private readonly bool[] ownedTools = new bool[Tools.Count];
    private readonly bool[] ownedBags = new bool[Bags.Count];

    public event Action Changed;

    public ToolTier EquippedTool { get; private set; } = ToolTier.Axe;
    public BagTier EquippedBag { get; private set; } = BagTier.Canvas;

    public ToolStats ToolStats => Tools.Get(EquippedTool);
    public BagStats BagStats => Bags.Get(EquippedBag);

    public float Damage => ToolStats.damage;
    public float SwingsPerSecond => ToolStats.swingsPerSecond;
    public float ArcDegrees => ToolStats.arcDegrees;
    public float Range => ToolStats.range;
    public float ResourceBonus => ToolStats.resourceBonus;

    public bool OwnsTool(ToolTier tier) => ownedTools[(int)tier];
    public bool OwnsBag(BagTier tier) => ownedBags[(int)tier];

    public void Configure(ResourceInventory newWallet)
    {
        wallet = newWallet;
    }

    private void Awake()
    {
        if (wallet == null) wallet = GetComponent<ResourceInventory>();

        // The starter kit is free, so the player is never stuck with nothing.
        ownedTools[(int)ToolTier.Axe] = true;
        ownedBags[(int)BagTier.Canvas] = true;
        EquippedTool = ToolTier.Axe;
        EquippedBag = BagTier.Canvas;
        ApplyBag();
    }

    public bool TryBuyTool(ToolTier tier)
    {
        if (ownedTools[(int)tier] || wallet == null) return false;
        if (!wallet.TrySpendMoney(Tools.Get(tier).price)) return false;

        ownedTools[(int)tier] = true;
        EquippedTool = tier;
        Changed?.Invoke();
        return true;
    }

    public bool EquipTool(ToolTier tier)
    {
        if (!ownedTools[(int)tier] || EquippedTool == tier) return false;

        EquippedTool = tier;
        Changed?.Invoke();
        return true;
    }

    public bool TryBuyBag(BagTier tier)
    {
        if (ownedBags[(int)tier] || wallet == null) return false;
        if (!wallet.TrySpendMoney(Bags.Get(tier).price)) return false;

        ownedBags[(int)tier] = true;
        EquippedBag = tier;
        ApplyBag();
        Changed?.Invoke();
        return true;
    }

    public bool EquipBag(BagTier tier)
    {
        if (!ownedBags[(int)tier] || EquippedBag == tier) return false;

        EquippedBag = tier;
        ApplyBag();
        Changed?.Invoke();
        return true;
    }

    private void ApplyBag()
    {
        if (wallet != null) wallet.SetCapacity(BagStats.capacity);
    }
}
