using UnityEngine;

public enum BagTier
{
    Canvas = 0,
    Reinforced = 1,
    Hauler = 2,
    LoggerPack = 3,
    Trailer = 4
}

public struct BagStats
{
    public BagTier tier;
    public string label;
    public string flavour;
    public int price;
    public int capacity;
    public Color tint;
}

/// Backpacks are equipment, not levels: each one is a separate thing you buy once
/// and then equip. Capacity roughly doubles per tier so the jump always reads.
public static class Bags
{
    public const int Count = 5;

    private static readonly BagStats[] Table =
    {
        new BagStats
        {
            tier = BagTier.Canvas, label = "CANVAS BAG", flavour = "Starter",
            price = 0, capacity = 100, tint = new Color(0.72f, 0.62f, 0.42f)
        },
        new BagStats
        {
            tier = BagTier.Reinforced, label = "REINFORCED BAG", flavour = "Double load",
            price = 300, capacity = 200, tint = new Color(0.58f, 0.46f, 0.3f)
        },
        new BagStats
        {
            tier = BagTier.Hauler, label = "HAULER", flavour = "Four hundred",
            price = 1200, capacity = 400, tint = new Color(0.4f, 0.52f, 0.66f)
        },
        new BagStats
        {
            tier = BagTier.LoggerPack, label = "LOGGER PACK", flavour = "Eight hundred",
            price = 4000, capacity = 800, tint = new Color(0.36f, 0.62f, 0.44f)
        },
        new BagStats
        {
            tier = BagTier.Trailer, label = "TRAILER", flavour = "Clear a grove in one trip",
            price = 12000, capacity = 1600, tint = new Color(0.62f, 0.68f, 0.78f)
        }
    };

    public static BagStats Get(BagTier tier)
    {
        return Table[Mathf.Clamp((int)tier, 0, Count - 1)];
    }
}
