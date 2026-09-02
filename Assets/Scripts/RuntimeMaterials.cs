using System.Collections.Generic;
using UnityEngine;

/// Shared cache for the few materials that gameplay code has to create at runtime
/// (carried resource stacks, resource pickups). Everything that lives in a prefab
/// uses a real material asset instead.
public static class RuntimeMaterials
{
    private static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>();
    private static Shader litShader;

    public static Material Solid(Color color)
    {
        int key = ((Color32)color).GetHashCode();
        if (Cache.TryGetValue(key, out Material cached) && cached != null) return cached;

        Material material = new Material(ResolveShader()) { name = "Runtime_" + key };
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);
        material.enableInstancing = true;
        Cache[key] = material;
        return material;
    }

    private static Shader ResolveShader()
    {
        if (litShader != null) return litShader;
        litShader = Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Universal Render Pipeline/Simple Lit")
            ?? Shader.Find("Standard");
        return litShader;
    }
}
