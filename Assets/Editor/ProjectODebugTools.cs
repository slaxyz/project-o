using UnityEditor;
using UnityEngine;

/// Small Play Mode-only shortcuts for testing the current gameplay loop.
public static class ProjectODebugTools
{
    private const string MenuRoot = "Tools/Project O/Debug/";

    [MenuItem(MenuRoot + "9999 Cash")]
    private static void GiveCash()
    {
        ResourceInventory inventory = FindInventory();
        if (inventory == null) return;
        inventory.DebugSetMoney(9999);
        Debug.Log("[Project O] Debug: cash set to 9999.");
    }

    [MenuItem(MenuRoot + "Full Wood")]
    private static void FillWood()
    {
        ResourceInventory inventory = FindInventory();
        if (inventory == null) return;
        inventory.DebugFillWood();
        Debug.Log("[Project O] Debug: wood bag filled.");
    }

    [MenuItem(MenuRoot + "Spawn Animal")]
    private static void SpawnAnimal()
    {
        WolfPackSpawner spawner = Object.FindFirstObjectByType<WolfPackSpawner>();
        if (spawner == null || !spawner.DebugSpawnAnimal()) return;
        Debug.Log("[Project O] Debug: tiger spawned near the player.");
    }

    [MenuItem(MenuRoot + "9999 Cash", true)]
    [MenuItem(MenuRoot + "Full Wood", true)]
    [MenuItem(MenuRoot + "Spawn Animal", true)]
    private static bool ValidatePlayMode()
    {
        return EditorApplication.isPlaying;
    }

    private static ResourceInventory FindInventory()
    {
        if (!EditorApplication.isPlaying) return null;
        ResourceInventory inventory = Object.FindFirstObjectByType<ResourceInventory>();
        if (inventory == null) Debug.LogWarning("[Project O] Debug command needs SampleScene in Play Mode.");
        return inventory;
    }
}
