using UnityEngine;

public enum ToolTier
{
    Axe = 0,
    GoldenAxe = 1,
    Chainsaw = 2,
    Brushcutter = 3,
    FactorySaw = 4
}

public struct ToolStats
{
    public ToolTier tier;
    public string label;
    public string flavour;
    public int price;
    public float damage;
    public float swingsPerSecond;
    public float arcDegrees;
    public float range;
    public float resourceBonus;
    public Color tint;
}

/// The harvest tools and what makes each one worth buying. Every tier is a clear
/// step up in output, but they get there differently: the golden axe pays in extra
/// resources, the chainsaw in raw speed, the brushcutter in how many trees one
/// swing reaches, the factory saw in all three.
public static class Tools
{
    public const int Count = 5;

    private static readonly ToolStats[] Table =
    {
        new ToolStats
        {
            tier = ToolTier.Axe, label = "AXE", flavour = "Starter", price = 0,
            damage = 1f, swingsPerSecond = 1.6f, arcDegrees = 100f, range = 2.2f,
            resourceBonus = 1f, tint = new Color(0.78f, 0.82f, 0.88f)
        },
        new ToolStats
        {
            tier = ToolTier.GoldenAxe, label = "GOLDEN AXE", flavour = "+35% loot", price = 150,
            damage = 2f, swingsPerSecond = 1.8f, arcDegrees = 110f, range = 2.4f,
            resourceBonus = 1.35f, tint = new Color(0.96f, 0.78f, 0.22f)
        },
        new ToolStats
        {
            tier = ToolTier.Chainsaw, label = "CHAINSAW", flavour = "Very fast", price = 600,
            damage = 3f, swingsPerSecond = 3.2f, arcDegrees = 80f, range = 2.3f,
            resourceBonus = 1f, tint = new Color(0.95f, 0.45f, 0.12f)
        },
        new ToolStats
        {
            tier = ToolTier.Brushcutter, label = "BRUSHCUTTER", flavour = "Wide sweep", price = 1800,
            damage = 3f, swingsPerSecond = 2.6f, arcDegrees = 210f, range = 3f,
            resourceBonus = 1.15f, tint = new Color(0.36f, 0.78f, 0.4f)
        },
        new ToolStats
        {
            tier = ToolTier.FactorySaw, label = "FACTORY SAW", flavour = "Clears everything", price = 5000,
            damage = 7f, swingsPerSecond = 2.2f, arcDegrees = 280f, range = 3.9f,
            resourceBonus = 1.5f, tint = new Color(0.62f, 0.68f, 0.78f)
        }
    };

    public static ToolStats Get(ToolTier tier)
    {
        return Table[Mathf.Clamp((int)tier, 0, Count - 1)];
    }
}
