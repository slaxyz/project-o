using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Imports the supplied free packs once, without opening their demo scenes.
/// The package paths stay in Downloads so the repository only receives imported assets.
[InitializeOnLoad]
public static class AssetPackageImporter
{
    private const string MarkerPath = "Library/project-o-asset-packages-imported.txt";
    private static readonly string[] PackagePaths =
    {
        @"C:\Users\Clement\Downloads\Unity_6_Apocalypse_Free_v1.0.unitypackage",
        @"C:\Users\Clement\Downloads\weapons_free.unitypackage",
        @"C:\Users\Clement\Downloads\animals_free.unitypackage",
        @"C:\Users\Clement\Downloads\Unity_6_Food_Free_v2.3.unitypackage"
    };

    static AssetPackageImporter()
    {
        EditorApplication.delayCall += ImportPackagesIfNeeded;
    }

    [MenuItem("Tools/Project O/Import Free Asset Packs")]
    public static void ImportPackagesFromMenu()
    {
        ImportPackagesIfNeeded(true);
    }

    private static void ImportPackagesIfNeeded()
    {
        ImportPackagesIfNeeded(false);
    }

    private static void ImportPackagesIfNeeded(bool force)
    {
        if (!force && File.Exists(MarkerPath)) return;

        for (int index = 0; index < PackagePaths.Length; index++)
        {
            string path = PackagePaths[index];
            if (!File.Exists(path))
            {
                Debug.LogWarning("[Project O] Asset package not found: " + path);
                continue;
            }

            Debug.Log("[Project O] Importing asset package: " + Path.GetFileName(path));
            AssetDatabase.ImportPackage(path, false);
        }

        File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("O"));
        AssetDatabase.Refresh();
        // Rebuild once the imported prefabs are available so the generated world
        // picks up their visuals during the same editor session.
        EditorApplication.delayCall += WorldSetup.RebuildFromMenu;
    }
}
