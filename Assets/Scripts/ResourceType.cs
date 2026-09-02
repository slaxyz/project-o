using UnityEngine;

public enum ResourceType
{
    Wood = 0,
    Meat = 1,
    Cash = 2
}

public static class ResourceTypes
{
    public const int Count = 3;

    public static readonly ResourceType[] All = { ResourceType.Wood, ResourceType.Meat, ResourceType.Cash };

    /// What the trapdoor accepts. Cash is carried too, but it goes to the cabin.
    public static readonly ResourceType[] Sellable = { ResourceType.Wood, ResourceType.Meat };

    public static bool IsSellable(ResourceType type)
    {
        return type != ResourceType.Cash;
    }

    public static string Label(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Meat: return "MEAT";
            case ResourceType.Cash: return "CASH";
            default: return "WOOD";
        }
    }

    public static Color Tint(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Meat: return new Color(0.84f, 0.29f, 0.31f);
            case ResourceType.Cash: return new Color(0.35f, 0.72f, 0.36f);
            default: return new Color(0.52f, 0.29f, 0.12f);
        }
    }

    public static Color FeedbackTint(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Meat: return new Color(1f, 0.48f, 0.48f);
            case ResourceType.Cash: return new Color(0.46f, 0.92f, 0.48f);
            default: return new Color(0.96f, 0.78f, 0.38f);
        }
    }
}
