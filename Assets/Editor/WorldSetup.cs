using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Single entry point that builds the playable world: placeholder meshes, materials
/// and prefabs, the camp with its counter, shop and build plot, the two fence rings,
/// the streamed forest, the wolf packs, the HUD and every wiring between them.
/// Idempotent: running it twice produces the same scene.
[InitializeOnLoad]
public static class WorldSetup
{
    private const string CompletionMarker = "Library/project-o-world-setup.txt";
    private const string TestReport = "Library/project-o-world-selftest.txt";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string HarvestableLayerName = "Harvestable";

    private const string FencePrefabPath = "Assets/Prefabs/FenceSegmentPlaceholder.prefab";
    private const string TreePrefabPath = "Assets/Prefabs/ForestTreePlaceholder.prefab";
    private const string WolfPrefabPath = "Assets/Prefabs/WolfPlaceholder.prefab";
    private const string MillPrefabPath = "Assets/Prefabs/LumberMill.prefab";
    private static readonly string[] ToolPrefabPaths =
    {
        "Assets/Prefabs/Tool_Axe.prefab",
        "Assets/Prefabs/Tool_GoldenAxe.prefab",
        "Assets/Prefabs/Tool_Chainsaw.prefab",
        "Assets/Prefabs/Tool_Brushcutter.prefab",
        "Assets/Prefabs/Tool_FactorySaw.prefab"
    };

    private const float SmallRingRadius = 7.25f;
    private const int SmallRingSegments = 18;
    private const int SmallRingGapFirst = 13;
    private const float LargeRingRadius = 11f;
    private const int LargeRingSegments = 26;
    private const int LargeRingGapFirst = 19;
    private const float SmallCampRadius = 8.4f;
    private const float LargeCampRadius = 12f;
    private const float ForestClearRadius = 14f;
    private const int BaseUpgradeCost = 250;
    private const int MillPrice = 400;
    private const int CashPerBundle = 10;
    private const int WorldSeed = 260902;

    private static int harvestableLayer;

    static WorldSetup()
    {
        ScheduleSetup();
    }

    [MenuItem("Tools/Project O/Rebuild World")]
    public static void RebuildFromMenu()
    {
        ConfigureAndTest();
    }

    [MenuItem("Tools/Project O/Clear Setup Marker")]
    public static void ClearMarker()
    {
        if (File.Exists(CompletionMarker)) File.Delete(CompletionMarker);
        Debug.Log("[Project O] Setup marker cleared. The world will be rebuilt on the next reload.");
    }

    private static void ScheduleSetup()
    {
        if (File.Exists(CompletionMarker)) return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
            return;
        }

        EditorApplication.delayCall += ConfigureAndTest;
    }

    private static void HandlePlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.delayCall += ConfigureAndTest;
    }

    public static void ConfigureAndTest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            ScheduleSetup();
            return;
        }

        try
        {
            harvestableLayer = EnsureHarvestableLayer();
            Palette palette = BuildPalette();

            GameObject fencePrefab = CreateFencePrefab(palette);
            GameObject treePrefab = CreateForestTreePrefab(palette);
            GameObject wolfPrefab = CreateWolfPrefab(palette);
            GameObject millPrefab = CreateMillPrefab(palette);
            GameObject[] toolPrefabs = CreateToolPrefabs(palette);

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            PurgeStaleObjects();
            ConfigureCoreObjects(palette);

            GameObject enclosure = CreateEnclosure(fencePrefab, palette);
            GameObject camp = CreateCampFurniture(palette, millPrefab);
            GameObject forest = CreateForest(treePrefab);
            GameObject wolves = CreateWolfSpawner(wolfPrefab);
            ConfigureGameplaySystems(toolPrefabs, enclosure, camp);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            CleanUpLegacyAssets();
            RunSelfTest(scene, enclosure, camp, forest, wolves);

            File.WriteAllText(CompletionMarker, DateTime.UtcNow.ToString("O"));
            Debug.Log("[Project O] World setup complete. Self-test passed.");
        }
        catch (Exception exception)
        {
            File.WriteAllText(TestReport, "FAIL\n" + exception);
            Debug.LogException(exception);
        }
    }

    // ----- palette ------------------------------------------------------------

    private sealed class Palette
    {
        public Material field;
        public Material fence;
        public Material trunk;
        public Material leafLow;
        public Material leafHigh;
        public Material fur;
        public Material muzzle;
        public Material stone;
        public Material wood;
        public Material plank;
        public Material steel;
        public Material gold;
        public Material orange;
        public Material roof;
        public Material plot;
        public Material plinth;
        public Material accent;
    }

    private static Palette BuildPalette()
    {
        return new Palette
        {
            field = CreateMaterial("Field_Mat", new Color(0.32f, 0.58f, 0.28f)),
            fence = CreateMaterial("Fence_Mat", new Color(0.46f, 0.28f, 0.13f)),
            trunk = CreateMaterial("Trunk_Mat", new Color(0.36f, 0.22f, 0.11f)),
            leafLow = CreateMaterial("Tree_Mat", new Color(0.13f, 0.36f, 0.18f)),
            leafHigh = CreateMaterial("TreeLight_Mat", new Color(0.2f, 0.52f, 0.25f)),
            fur = CreateMaterial("WolfFur_Mat", new Color(0.29f, 0.31f, 0.35f)),
            muzzle = CreateMaterial("WolfMuzzle_Mat", new Color(0.14f, 0.15f, 0.17f)),
            stone = CreateMaterial("Stone_Mat", new Color(0.58f, 0.6f, 0.62f)),
            wood = CreateMaterial("CampWood_Mat", new Color(0.55f, 0.35f, 0.18f)),
            plank = CreateMaterial("CampPlank_Mat", new Color(0.72f, 0.52f, 0.3f)),
            steel = CreateMaterial("Steel_Mat", new Color(0.76f, 0.8f, 0.86f)),
            gold = CreateMaterial("Gold_Mat", new Color(0.95f, 0.76f, 0.2f)),
            orange = CreateMaterial("Machine_Mat", new Color(0.93f, 0.42f, 0.12f)),
            roof = CreateMaterial("Roof_Mat", new Color(0.24f, 0.45f, 0.72f)),
            plot = CreateMaterial("Plot_Mat", new Color(0.72f, 0.68f, 0.42f)),
            plinth = CreateMaterial("Plinth_Mat", new Color(0.86f, 0.8f, 0.62f)),
            accent = CreateMaterial("Accent_Mat", new Color(0.96f, 0.78f, 0.22f))
        };
    }

    // ----- layers -------------------------------------------------------------

    private static int EnsureHarvestableLayer()
    {
        int existing = LayerMask.NameToLayer(HarvestableLayerName);
        if (existing >= 0) return existing;

        UnityEngine.Object tagManagerAsset = AssetDatabase
            .LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")
            .FirstOrDefault();
        if (tagManagerAsset == null) return 0;

        SerializedObject tagManager = new SerializedObject(tagManagerAsset);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int index = 8; index < layers.arraySize; index++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            if (!string.IsNullOrEmpty(layer.stringValue)) continue;

            layer.stringValue = HarvestableLayerName;
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            return index;
        }

        throw new InvalidOperationException("No free user layer left for " + HarvestableLayerName + ".");
    }

    // ----- prefabs ------------------------------------------------------------

    private static GameObject CreateFencePrefab(Palette palette)
    {
        GameObject root = new GameObject("FenceSegmentPlaceholder");
        AddPrimitive(root.transform, PrimitiveType.Cube, "LeftPost", new Vector3(-1.25f, 0.7f, 0f), new Vector3(0.18f, 1.4f, 0.18f), palette.fence);
        AddPrimitive(root.transform, PrimitiveType.Cube, "RightPost", new Vector3(1.25f, 0.7f, 0f), new Vector3(0.18f, 1.4f, 0.18f), palette.fence);
        AddPrimitive(root.transform, PrimitiveType.Cube, "TopRail", new Vector3(0f, 0.9f, 0f), new Vector3(2.5f, 0.15f, 0.14f), palette.fence);
        AddPrimitive(root.transform, PrimitiveType.Cube, "BottomRail", new Vector3(0f, 0.48f, 0f), new Vector3(2.5f, 0.15f, 0.14f), palette.fence);

        BoxCollider barrier = root.AddComponent<BoxCollider>();
        barrier.center = new Vector3(0f, 0.7f, 0f);
        barrier.size = new Vector3(2.5f, 1.4f, 0.22f);
        return SavePrefab(root, FencePrefabPath);
    }

    /// Conifer built from stacked cones, so the canopy reads as a solid mass when
    /// the trees are packed shoulder to shoulder.
    private static GameObject CreateForestTreePrefab(Palette palette)
    {
        Mesh cone = EnsureConeMesh();
        GameObject root = new GameObject("ForestTreePlaceholder");
        root.layer = harvestableLayer;

        AddMesh(root.transform, "Trunk", EnsureTrunkMesh(), Vector3.zero, new Vector3(0.3f, 0.7f, 0.3f), palette.trunk);
        AddMesh(root.transform, "CrownLow", cone, new Vector3(0f, 0.42f, 0f), new Vector3(1.5f, 1.45f, 1.5f), palette.leafLow);
        AddMesh(root.transform, "CrownMid", cone, new Vector3(0f, 1.32f, 0f), new Vector3(1.08f, 1.2f, 1.08f), palette.leafLow);
        AddMesh(root.transform, "CrownTop", cone, new Vector3(0f, 2.05f, 0f), new Vector3(0.62f, 0.9f, 0.62f), palette.leafHigh);

        CapsuleCollider trunkCollider = root.AddComponent<CapsuleCollider>();
        trunkCollider.center = new Vector3(0f, 1.2f, 0f);
        trunkCollider.radius = 0.5f;
        trunkCollider.height = 2.6f;

        Harvestable harvestable = root.AddComponent<Harvestable>();
        harvestable.Initialize(2f, 2f, ResourceType.Wood, 1);
        return SavePrefab(root, TreePrefabPath);
    }

    private static GameObject CreateWolfPrefab(Palette palette)
    {
        GameObject root = new GameObject("WolfPlaceholder");
        root.layer = harvestableLayer;

        // Separate nodes: the wolf animates Visuals, the hit reaction moves Shake.
        GameObject shake = new GameObject("Shake");
        shake.transform.SetParent(root.transform, false);
        GameObject visual = new GameObject("Visuals");
        visual.transform.SetParent(shake.transform, false);

        Transform body = AddPrimitive(visual.transform, PrimitiveType.Capsule, "Body",
            new Vector3(0f, 0.62f, 0f), new Vector3(0.5f, 0.6f, 0.5f), palette.fur);
        body.localRotation = Quaternion.Euler(90f, 0f, 0f);
        AddPrimitive(visual.transform, PrimitiveType.Sphere, "Chest", new Vector3(0f, 0.66f, 0.3f), new Vector3(0.5f, 0.48f, 0.5f), palette.fur);
        AddPrimitive(visual.transform, PrimitiveType.Sphere, "Rump", new Vector3(0f, 0.6f, -0.32f), new Vector3(0.46f, 0.44f, 0.46f), palette.fur);

        GameObject headPivot = new GameObject("Head");
        headPivot.transform.SetParent(visual.transform, false);
        headPivot.transform.localPosition = new Vector3(0f, 0.78f, 0.56f);
        AddPrimitive(headPivot.transform, PrimitiveType.Sphere, "Skull", Vector3.zero, new Vector3(0.34f, 0.32f, 0.36f), palette.fur);
        AddPrimitive(headPivot.transform, PrimitiveType.Cube, "Snout", new Vector3(0f, -0.06f, 0.22f), new Vector3(0.15f, 0.12f, 0.24f), palette.muzzle);
        AddPrimitive(headPivot.transform, PrimitiveType.Cube, "EarLeft", new Vector3(-0.11f, 0.19f, -0.02f), new Vector3(0.08f, 0.16f, 0.05f), palette.fur);
        AddPrimitive(headPivot.transform, PrimitiveType.Cube, "EarRight", new Vector3(0.11f, 0.19f, -0.02f), new Vector3(0.08f, 0.16f, 0.05f), palette.fur);

        GameObject tailPivot = new GameObject("Tail");
        tailPivot.transform.SetParent(visual.transform, false);
        tailPivot.transform.localPosition = new Vector3(0f, 0.66f, -0.5f);
        Transform tailBone = AddPrimitive(tailPivot.transform, PrimitiveType.Cube, "TailBone",
            new Vector3(0f, 0.02f, -0.2f), new Vector3(0.1f, 0.1f, 0.42f), palette.fur);
        tailBone.localRotation = Quaternion.Euler(-18f, 0f, 0f);

        Transform[] legs = new Transform[4];
        legs[0] = AddPrimitive(visual.transform, PrimitiveType.Cube, "LegFrontLeft", new Vector3(-0.19f, 0.24f, 0.28f), new Vector3(0.12f, 0.48f, 0.12f), palette.muzzle);
        legs[1] = AddPrimitive(visual.transform, PrimitiveType.Cube, "LegFrontRight", new Vector3(0.19f, 0.24f, 0.28f), new Vector3(0.12f, 0.48f, 0.12f), palette.muzzle);
        legs[2] = AddPrimitive(visual.transform, PrimitiveType.Cube, "LegBackLeft", new Vector3(-0.19f, 0.24f, -0.28f), new Vector3(0.12f, 0.48f, 0.12f), palette.muzzle);
        legs[3] = AddPrimitive(visual.transform, PrimitiveType.Cube, "LegBackRight", new Vector3(0.19f, 0.24f, -0.28f), new Vector3(0.12f, 0.48f, 0.12f), palette.muzzle);

        CapsuleCollider bodyCollider = root.AddComponent<CapsuleCollider>();
        bodyCollider.center = new Vector3(0f, 0.55f, 0f);
        bodyCollider.radius = 0.4f;
        bodyCollider.height = 1.3f;

        Harvestable harvestable = root.AddComponent<Harvestable>();
        harvestable.Initialize(3f, 3f, ResourceType.Meat, 3);
        SetPrivateReference(harvestable, "shakeRoot", shake.transform);

        WolfAgent wolf = root.AddComponent<WolfAgent>();
        SerializedObject serializedWolf = new SerializedObject(wolf);
        serializedWolf.FindProperty("visualRoot").objectReferenceValue = visual.transform;
        serializedWolf.FindProperty("head").objectReferenceValue = headPivot.transform;
        serializedWolf.FindProperty("tail").objectReferenceValue = tailPivot.transform;
        SerializedProperty legsProperty = serializedWolf.FindProperty("legs");
        legsProperty.arraySize = legs.Length;
        for (int index = 0; index < legs.Length; index++)
            legsProperty.GetArrayElementAtIndex(index).objectReferenceValue = legs[index];
        serializedWolf.ApplyModifiedPropertiesWithoutUndo();

        return SavePrefab(root, WolfPrefabPath);
    }

    private static GameObject CreateMillPrefab(Palette palette)
    {
        GameObject root = new GameObject("LumberMill");
        AddPrimitive(root.transform, PrimitiveType.Cube, "Base", new Vector3(0f, 0.1f, 0f), new Vector3(2f, 0.2f, 2f), palette.stone);
        AddPrimitive(root.transform, PrimitiveType.Cube, "Shed", new Vector3(-0.2f, 0.65f, 0f), new Vector3(1.2f, 0.9f, 1.3f), palette.wood);
        AddPrimitive(root.transform, PrimitiveType.Cube, "Roof", new Vector3(-0.2f, 1.2f, 0f), new Vector3(1.45f, 0.2f, 1.55f), palette.roof);
        AddPrimitive(root.transform, PrimitiveType.Cube, "Bench", new Vector3(0.72f, 0.4f, 0f), new Vector3(0.5f, 0.4f, 1.2f), palette.plank);

        Transform blade = AddPrimitive(root.transform, PrimitiveType.Cylinder, "Blade",
            new Vector3(0.72f, 0.75f, 0f), new Vector3(0.62f, 0.04f, 0.62f), palette.steel);
        blade.localRotation = Quaternion.Euler(0f, 0f, 90f);

        GameObject stock = new GameObject("Stock");
        stock.transform.SetParent(root.transform, false);
        stock.transform.localPosition = new Vector3(-0.15f, 0.2f, -0.85f);

        BoxCollider millCollider = root.AddComponent<BoxCollider>();
        millCollider.center = new Vector3(-0.2f, 0.7f, 0f);
        millCollider.size = new Vector3(1.3f, 1.4f, 1.4f);

        root.AddComponent<ScalePop>();
        PassiveProducer producer = root.AddComponent<PassiveProducer>();
        producer.Configure(ResourceType.Wood, 1f, 40, stock.transform, root.GetComponent<ScalePop>());
        return SavePrefab(root, MillPrefabPath);
    }

    /// Five silhouettes, all hanging from the hand pivot so the swing reads.
    private static GameObject[] CreateToolPrefabs(Palette palette)
    {
        GameObject[] prefabs = new GameObject[Tools.Count];

        GameObject axe = new GameObject("Tool_Axe");
        AddHandle(axe.transform, palette.wood, 0.3f);
        AddPrimitive(axe.transform, PrimitiveType.Cube, "Blade", new Vector3(0f, -0.56f, 0.06f), new Vector3(0.2f, 0.22f, 0.07f), palette.steel);
        AddPrimitive(axe.transform, PrimitiveType.Cube, "Edge", new Vector3(0f, -0.56f, 0.14f), new Vector3(0.1f, 0.26f, 0.05f), palette.steel);
        prefabs[0] = SavePrefab(axe, ToolPrefabPaths[0]);

        GameObject golden = new GameObject("Tool_GoldenAxe");
        AddHandle(golden.transform, palette.wood, 0.32f);
        AddPrimitive(golden.transform, PrimitiveType.Cube, "Blade", new Vector3(0f, -0.6f, 0.02f), new Vector3(0.24f, 0.3f, 0.09f), palette.gold);
        AddPrimitive(golden.transform, PrimitiveType.Cube, "Edge", new Vector3(0f, -0.6f, 0.14f), new Vector3(0.12f, 0.34f, 0.06f), palette.gold);
        AddPrimitive(golden.transform, PrimitiveType.Cube, "BackEdge", new Vector3(0f, -0.6f, -0.12f), new Vector3(0.12f, 0.26f, 0.06f), palette.gold);
        prefabs[1] = SavePrefab(golden, ToolPrefabPaths[1]);

        GameObject chainsaw = new GameObject("Tool_Chainsaw");
        AddPrimitive(chainsaw.transform, PrimitiveType.Cube, "Body", new Vector3(0f, -0.22f, 0f), new Vector3(0.22f, 0.24f, 0.3f), palette.orange);
        AddPrimitive(chainsaw.transform, PrimitiveType.Cube, "Grip", new Vector3(0f, -0.05f, -0.04f), new Vector3(0.1f, 0.16f, 0.1f), palette.muzzle);
        AddPrimitive(chainsaw.transform, PrimitiveType.Cube, "Bar", new Vector3(0f, -0.6f, 0.06f), new Vector3(0.09f, 0.5f, 0.14f), palette.steel);
        AddPrimitive(chainsaw.transform, PrimitiveType.Cube, "Teeth", new Vector3(0f, -0.6f, 0.15f), new Vector3(0.11f, 0.52f, 0.04f), palette.muzzle);
        prefabs[2] = SavePrefab(chainsaw, ToolPrefabPaths[2]);

        GameObject brushcutter = new GameObject("Tool_Brushcutter");
        AddHandle(brushcutter.transform, palette.muzzle, 0.55f);
        AddPrimitive(brushcutter.transform, PrimitiveType.Cube, "Motor", new Vector3(0f, -0.16f, -0.1f), new Vector3(0.16f, 0.2f, 0.18f), palette.orange);
        Transform disc = AddPrimitive(brushcutter.transform, PrimitiveType.Cylinder, "Disc",
            new Vector3(0f, -1.06f, 0f), new Vector3(0.62f, 0.03f, 0.62f), palette.steel);
        disc.localRotation = Quaternion.identity;
        prefabs[3] = SavePrefab(brushcutter, ToolPrefabPaths[3]);

        GameObject saw = new GameObject("Tool_FactorySaw");
        AddPrimitive(saw.transform, PrimitiveType.Cube, "Frame", new Vector3(0f, -0.3f, 0f), new Vector3(0.18f, 0.6f, 0.18f), palette.orange);
        AddPrimitive(saw.transform, PrimitiveType.Cube, "Arm", new Vector3(0f, -0.62f, 0.28f), new Vector3(0.14f, 0.14f, 0.6f), palette.steel);
        Transform bigDisc = AddPrimitive(saw.transform, PrimitiveType.Cylinder, "Disc",
            new Vector3(0f, -0.62f, 0.62f), new Vector3(0.9f, 0.05f, 0.9f), palette.steel);
        bigDisc.localRotation = Quaternion.Euler(90f, 0f, 0f);
        AddPrimitive(bigDisc, PrimitiveType.Cylinder, "Hub", Vector3.zero, new Vector3(0.22f, 1.6f, 0.22f), palette.orange);
        prefabs[4] = SavePrefab(saw, ToolPrefabPaths[4]);

        return prefabs;
    }

    private static void AddHandle(Transform parent, Material material, float length)
    {
        Transform handle = AddPrimitive(parent, PrimitiveType.Cylinder, "Handle",
            new Vector3(0f, -length, 0f), new Vector3(0.055f, length, 0.055f), material);
        handle.localRotation = Quaternion.identity;
    }

    // ----- meshes -------------------------------------------------------------

    /// Unit cone: radius 0.5 at the base, apex one unit up, flat shaded so the
    /// facets catch the light the way the stylised reference does. Sides only:
    /// there are thousands on screen and the base is never visible from above.
    private static Mesh EnsureConeMesh()
    {
        const string path = "Assets/Models/FirCone.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        const int segments = 12;
        const float radius = 0.5f;
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> triangles = new List<int>();
        Vector3 apex = new Vector3(0f, 1f, 0f);

        for (int index = 0; index < segments; index++)
        {
            float first = index / (float)segments * Mathf.PI * 2f;
            float second = (index + 1) / (float)segments * Mathf.PI * 2f;
            Vector3 left = new Vector3(Mathf.Cos(first) * radius, 0f, Mathf.Sin(first) * radius);
            Vector3 right = new Vector3(Mathf.Cos(second) * radius, 0f, Mathf.Sin(second) * radius);

            // The winding (apex, right, left) puts the facet normal outwards.
            Vector3 normal = Vector3.Cross(right - apex, left - apex).normalized;
            AddTriangle(vertices, normals, triangles, apex, right, left, normal);
        }

        return SaveMesh("FirCone", path, vertices, normals, triangles);
    }

    /// Unit tube, radius 0.5 and one unit tall, six sided. Open at both ends: the
    /// bottom sits on the ground and the top is buried in the foliage.
    private static Mesh EnsureTrunkMesh()
    {
        const string path = "Assets/Models/FirTrunk.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        const int segments = 6;
        const float radius = 0.5f;
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int index = 0; index < segments; index++)
        {
            float first = index / (float)segments * Mathf.PI * 2f;
            float second = (index + 1) / (float)segments * Mathf.PI * 2f;
            Vector3 leftBottom = new Vector3(Mathf.Cos(first) * radius, 0f, Mathf.Sin(first) * radius);
            Vector3 rightBottom = new Vector3(Mathf.Cos(second) * radius, 0f, Mathf.Sin(second) * radius);
            Vector3 leftTop = leftBottom + Vector3.up;
            Vector3 rightTop = rightBottom + Vector3.up;

            Vector3 normal = ((leftBottom + rightBottom) * 0.5f).normalized;
            AddTriangle(vertices, normals, triangles, leftTop, rightTop, leftBottom, normal);
            AddTriangle(vertices, normals, triangles, rightTop, rightBottom, leftBottom, normal);
        }

        return SaveMesh("FirTrunk", path, vertices, normals, triangles);
    }

    private static void AddTriangle(List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
        Vector3 first, Vector3 second, Vector3 third, Vector3 normal)
    {
        int start = vertices.Count;
        vertices.Add(first);
        vertices.Add(second);
        vertices.Add(third);
        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);
        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
    }

    private static Mesh SaveMesh(string name, string path, List<Vector3> vertices,
        List<Vector3> normals, List<int> triangles)
    {
        Mesh mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // ----- scene --------------------------------------------------------------

    /// Removes the objects previous setup passes left saved in the scene, and the
    /// components whose scripts have since been deleted.
    private static void PurgeStaleObjects()
    {
        string[] obsoleteNames =
        {
            "EnvironmentPlaceholders", "ResourcePlaceholders", "ZoneProgression",
            "ZoneBarrierPlaceholder", "Zone2Content", "SheepFarm", "FieldFence",
            "CampFurniture", "WoodNodes", "ProceduralForest", "WolfPacks",
            "MobileGameSettings", "InfiniteGroundTiles", "GroundTile",
            "CarriedWood", "CarriedResources", "AxeOrbit", "ToolHand",
            "ResourcePickups", "ResourceFlyers"
        };

        foreach (GameObject candidate in FindSceneObjects(obsoleteNames))
        {
            if (candidate != null) UnityEngine.Object.DestroyImmediate(candidate);
        }

        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(item.gameObject);
            }
        }
    }

    private static void ConfigureCoreObjects(Palette palette)
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground != null)
        {
            ground.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            ground.transform.localScale = new Vector3(1.6f, 1f, 1.6f);
            ground.GetComponent<Renderer>().sharedMaterial = palette.field;
        }

        // The old red block in the middle becomes the cabin, and the separate
        // building on the left goes away: there is one base and it is the cabin.
        GameObject oldBuilding = GameObject.Find("UpgradeableBuildingPlaceholder");
        if (oldBuilding != null) UnityEngine.Object.DestroyImmediate(oldBuilding);

        GameObject camp = GameObject.Find("CampPlaceholder");
        if (camp != null)
        {
            camp.transform.position = Vector3.zero;
            camp.transform.localScale = Vector3.one;
            BuildCabin(camp, palette);

            CampArea area = camp.GetComponent<CampArea>();
            if (area == null) area = camp.AddComponent<CampArea>();
            area.Configure(SmallCampRadius);
        }

        GameObject player = GameObject.Find("PlayerPlaceholder");
        if (player != null)
        {
            player.transform.position = new Vector3(0f, 0f, -3f);
            player.transform.rotation = Quaternion.identity;
        }
    }

    /// The cabin at the centre of the camp, on a beige plinth. It is where cash is
    /// banked and where the base upgrade is bought.
    private static void BuildCabin(GameObject camp, Palette palette)
    {
        for (int index = camp.transform.childCount - 1; index >= 0; index--)
            UnityEngine.Object.DestroyImmediate(camp.transform.GetChild(index).gameObject);

        AddPrimitive(camp.transform, PrimitiveType.Cylinder, "Plinth",
            new Vector3(0f, 0.09f, 0f), new Vector3(4.2f, 0.18f, 4.2f), palette.plinth);
        AddPrimitive(camp.transform, PrimitiveType.Cylinder, "PlinthTop",
            new Vector3(0f, 0.2f, 0f), new Vector3(3.4f, 0.06f, 3.4f), palette.plot);

        GameObject shell = new GameObject("Cabin");
        shell.transform.SetParent(camp.transform, false);
        shell.transform.localPosition = new Vector3(0f, 0.22f, 0f);

        AddPrimitive(shell.transform, PrimitiveType.Cube, "Walls",
            new Vector3(0f, 0.72f, 0f), new Vector3(2.3f, 1.44f, 2f), palette.wood, true);
        AddPrimitive(shell.transform, PrimitiveType.Cube, "Beam",
            new Vector3(0f, 1.5f, 0f), new Vector3(2.5f, 0.16f, 2.2f), palette.plank);

        Transform roofLeft = AddPrimitive(shell.transform, PrimitiveType.Cube, "RoofLeft",
            new Vector3(-0.62f, 1.92f, 0f), new Vector3(1.5f, 0.16f, 2.3f), palette.roof);
        roofLeft.localRotation = Quaternion.Euler(0f, 0f, 34f);
        Transform roofRight = AddPrimitive(shell.transform, PrimitiveType.Cube, "RoofRight",
            new Vector3(0.62f, 1.92f, 0f), new Vector3(1.5f, 0.16f, 2.3f), palette.roof);
        roofRight.localRotation = Quaternion.Euler(0f, 0f, -34f);

        AddPrimitive(shell.transform, PrimitiveType.Cube, "Door",
            new Vector3(0f, 0.52f, -1.02f), new Vector3(0.72f, 1.04f, 0.1f), palette.plank);
        AddPrimitive(shell.transform, PrimitiveType.Cube, "Window",
            new Vector3(0.72f, 0.95f, -1.02f), new Vector3(0.44f, 0.44f, 0.08f), palette.accent);

        // Cash lands on the counter beside the door.
        GameObject till = new GameObject("Till");
        till.transform.SetParent(camp.transform, false);
        till.transform.localPosition = new Vector3(0f, 1.1f, -1.5f);

        shell.AddComponent<ScalePop>();
        camp.AddComponent<CashDeposit>();
    }

    /// Two rings: the small park, and the large one that replaces it at level two.
    private static GameObject CreateEnclosure(GameObject fencePrefab, Palette palette)
    {
        GameObject enclosure = new GameObject("FieldFence");
        GameObject small = CreateRing(enclosure.transform, "Ring_Small", fencePrefab, palette,
            SmallRingRadius, SmallRingSegments, SmallRingGapFirst);
        GameObject large = CreateRing(enclosure.transform, "Ring_Large", fencePrefab, palette,
            LargeRingRadius, LargeRingSegments, LargeRingGapFirst);

        small.SetActive(true);
        large.SetActive(false);

        GameObject buildSpot = new GameObject("FenceBuildSpot");
        buildSpot.transform.SetParent(enclosure.transform, false);
        AddPrimitive(buildSpot.transform, PrimitiveType.Cube, "Tile",
            new Vector3(0f, 0.035f, -SmallRingRadius + 1.35f), new Vector3(1.5f, 0.07f, 1.5f), palette.accent);
        return enclosure;
    }

    private static GameObject CreateRing(Transform parent, string name, GameObject fencePrefab,
        Palette palette, float radius, int segments, int gapFirst)
    {
        GameObject ring = new GameObject(name);
        ring.transform.SetParent(parent, false);

        // Segments are stretched to the exact arc length so they meet edge to edge.
        float arcLength = 2f * Mathf.PI * radius / segments;
        float stretch = arcLength / 2.5f;

        for (int index = 0; index < segments; index++)
        {
            if (index == gapFirst || index == gapFirst + 1) continue;

            float angle = index / (float)segments * Mathf.PI * 2f;
            Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Quaternion rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg - 90f, 0f);

            GameObject segment = (GameObject)PrefabUtility.InstantiatePrefab(fencePrefab);
            segment.name = "Fence_" + index.ToString("00");
            segment.transform.SetParent(ring.transform, false);
            segment.transform.SetPositionAndRotation(position, rotation);
            segment.transform.localScale = new Vector3(stretch, 1f, 1f);
        }

        // The gap is centred on the south of the ring, where the player starts.
        float halfWidth = radius * Mathf.Sin(Mathf.PI / segments);
        CreateGate(ring.transform, palette, radius, halfWidth);
        return ring;
    }

    private static void CreateGate(Transform parent, Palette palette, float radius, float halfWidth)
    {
        GameObject gate = new GameObject("EnclosureGate");
        gate.transform.SetParent(parent, false);
        gate.transform.position = new Vector3(0f, 0f, -radius);
        gate.transform.rotation = Quaternion.identity;

        AddPrimitive(gate.transform, PrimitiveType.Cube, "PostLeft",
            new Vector3(-halfWidth, 0.78f, 0f), new Vector3(0.24f, 1.56f, 0.24f), palette.fence, true);
        AddPrimitive(gate.transform, PrimitiveType.Cube, "PostRight",
            new Vector3(halfWidth, 0.78f, 0f), new Vector3(0.24f, 1.56f, 0.24f), palette.fence, true);

        Transform leftHinge = CreateGatePanel(gate.transform, "HingeLeft", -halfWidth, 1f, halfWidth, palette);
        Transform rightHinge = CreateGatePanel(gate.transform, "HingeRight", halfWidth, -1f, halfWidth, palette);

        AudioSource audioSource = gate.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.6f;

        GameObject player = GameObject.Find("PlayerPlaceholder");
        EnclosureGate gateBehaviour = gate.AddComponent<EnclosureGate>();
        gateBehaviour.Configure(leftHinge, rightHinge, player != null ? player.transform : null, 4.2f);
    }

    private static Transform CreateGatePanel(Transform parent, string name, float hingeX,
        float direction, float halfWidth, Palette palette)
    {
        GameObject hinge = new GameObject(name);
        hinge.transform.SetParent(parent, false);
        hinge.transform.localPosition = new Vector3(hingeX, 0f, 0f);

        float panelLength = halfWidth - 0.12f;
        float center = direction * panelLength * 0.5f;

        // Two rails carry the colliders, the posts are decoration only.
        AddPrimitive(hinge.transform, PrimitiveType.Cube, "RailTop",
            new Vector3(center, 0.74f, 0f), new Vector3(panelLength, 0.16f, 0.14f), palette.fence, true);
        AddPrimitive(hinge.transform, PrimitiveType.Cube, "RailLow",
            new Vector3(center, 0.36f, 0f), new Vector3(panelLength, 0.16f, 0.14f), palette.fence, true);
        AddPrimitive(hinge.transform, PrimitiveType.Cube, "Brace",
            new Vector3(center, 0.55f, 0f), new Vector3(0.14f, 0.62f, 0.12f), palette.fence);
        AddPrimitive(hinge.transform, PrimitiveType.Cube, "EndPost",
            new Vector3(direction * panelLength, 0.55f, 0f), new Vector3(0.16f, 1.1f, 0.16f), palette.fence);
        return hinge.transform;
    }

    /// Hatch set into the ground: a stone rim and two flaps that swing down out of
    /// the way. Resources fall through it and come back out as cash.
    private static void CreateTrapdoor(Transform parent, Palette palette)
    {
        GameObject hatch = new GameObject("ResourceTrapdoor");
        hatch.transform.SetParent(parent, false);
        hatch.transform.position = new Vector3(5f, 0f, 0.5f);

        GameObject rim = new GameObject("Rim");
        rim.transform.SetParent(hatch.transform, false);
        for (int index = 0; index < 4; index++)
        {
            bool alongX = index < 2;
            float offset = index % 2 == 0 ? -1.05f : 1.05f;
            AddPrimitive(rim.transform, PrimitiveType.Cube, "Kerb_" + index,
                alongX ? new Vector3(offset, 0.09f, 0f) : new Vector3(0f, 0.09f, offset),
                alongX ? new Vector3(0.22f, 0.18f, 2.3f) : new Vector3(2.3f, 0.18f, 0.22f),
                palette.stone);
        }
        AddPrimitive(rim.transform, PrimitiveType.Cube, "Shaft",
            new Vector3(0f, -0.4f, 0f), new Vector3(1.9f, 0.8f, 1.9f), palette.muzzle);
        ScalePop rimPop = rim.AddComponent<ScalePop>();

        // Each flap hangs off a hinge at the outer edge and rotates about it.
        Transform leftHinge = CreateHatchFlap(hatch.transform, "FlapLeft", -0.95f, 1f, palette);
        Transform rightHinge = CreateHatchFlap(hatch.transform, "FlapRight", 0.95f, -1f, palette);

        GameObject dropPoint = new GameObject("DropPoint");
        dropPoint.transform.SetParent(hatch.transform, false);
        dropPoint.transform.localPosition = new Vector3(0f, -0.15f, 0f);
        GameObject cashPoint = new GameObject("CashPoint");
        cashPoint.transform.SetParent(hatch.transform, false);
        cashPoint.transform.localPosition = new Vector3(0f, 0.5f, -0.9f);

        ResourceTrapdoor trapdoor = hatch.AddComponent<ResourceTrapdoor>();
        trapdoor.Configure(null, leftHinge, rightHinge, dropPoint.transform, cashPoint.transform, rimPop, 2.2f);
    }

    private static Transform CreateHatchFlap(Transform parent, string name, float hingeX,
        float direction, Palette palette)
    {
        GameObject hinge = new GameObject(name);
        hinge.transform.SetParent(parent, false);
        // Keep the closed flaps just above the stone rim to avoid z-fighting.
        hinge.transform.localPosition = new Vector3(hingeX, 0.24f, 0f);

        AddPrimitive(hinge.transform, PrimitiveType.Cube, "Panel",
            new Vector3(direction * 0.47f, 0f, 0f), new Vector3(0.94f, 0.1f, 1.9f), palette.plank);
        AddPrimitive(hinge.transform, PrimitiveType.Cube, "Band",
            new Vector3(direction * 0.47f, 0.06f, 0f), new Vector3(0.9f, 0.04f, 0.16f), palette.steel);
        return hinge.transform;
    }

    /// The trapdoor, the shop stand and the build plot.
    private static GameObject CreateCampFurniture(Palette palette, GameObject millPrefab)
    {
        GameObject root = new GameObject("CampFurniture");
        CreateTrapdoor(root.transform, palette);

        // Build plot, revealed by the level two upgrade.
        GameObject plot = new GameObject("BuildPlot");
        plot.transform.SetParent(root.transform, false);
        plot.transform.position = new Vector3(8.5f, 0f, 0f);
        GameObject marker = new GameObject("EmptyMarker");
        marker.transform.SetParent(plot.transform, false);
        AddPrimitive(marker.transform, PrimitiveType.Cube, "Slab", new Vector3(0f, 0.06f, 0f), new Vector3(2.6f, 0.12f, 2.6f), palette.plot);
        for (int index = 0; index < 4; index++)
        {
            float x = index % 2 == 0 ? -1.15f : 1.15f;
            float z = index < 2 ? -1.15f : 1.15f;
            AddPrimitive(marker.transform, PrimitiveType.Cube, "Corner_" + index,
                new Vector3(x, 0.28f, z), new Vector3(0.16f, 0.44f, 0.16f), palette.accent);
        }
        marker.AddComponent<ScalePop>();

        // An unbuilt plot is solid: the slab alone is too flat for the movement
        // check, so an invisible box keeps the player off it until it is built.
        GameObject blocker = new GameObject("Blocker");
        blocker.transform.SetParent(marker.transform, false);
        BoxCollider blockerCollider = blocker.AddComponent<BoxCollider>();
        blockerCollider.center = new Vector3(0f, 0.7f, 0f);
        blockerCollider.size = new Vector3(2.6f, 1.4f, 2.6f);
        plot.AddComponent<BuildPlot>();
        plot.SetActive(false);

        return root;
    }

    private static GameObject CreateForest(GameObject treePrefab)
    {
        GameObject forest = new GameObject("ProceduralForest");
        GameObject player = GameObject.Find("PlayerPlaceholder");
        GameObject camp = GameObject.Find("CampPlaceholder");

        ProceduralForest generator = forest.AddComponent<ProceduralForest>();
        generator.Configure(treePrefab,
            player != null ? player.transform : null,
            camp != null ? camp.transform : null,
            1.05f, ForestClearRadius, 30f, WorldSeed);
        return forest;
    }

    private static GameObject CreateWolfSpawner(GameObject wolfPrefab)
    {
        GameObject wolves = new GameObject("WolfPacks");
        GameObject player = GameObject.Find("PlayerPlaceholder");

        WolfPackSpawner spawner = wolves.AddComponent<WolfPackSpawner>();
        spawner.Configure(wolfPrefab, player != null ? player.transform : null, 3, 3);
        return wolves;
    }

    private static void ConfigureGameplaySystems(GameObject[] toolPrefabs, GameObject enclosure, GameObject campFurniture)
    {
        PlayerMovement movement = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
        ResourceInventory inventory = UnityEngine.Object.FindFirstObjectByType<ResourceInventory>();
        GameplayHUD hud = UnityEngine.Object.FindFirstObjectByType<GameplayHUD>();
        if (movement == null || inventory == null || hud == null)
        {
            throw new InvalidOperationException("Core gameplay systems are missing from SampleScene.");
        }

        HudWidgets widgets = BuildHud();
        hud.Bind(widgets.bindings);

        movement.SetMovementBounds(new Vector2(-240f, -240f), new Vector2(240f, 240f));
        inventory.Configure(hud, Bags.Get(BagTier.Canvas).capacity);
        inventory.GetComponent<CarriedResourceVisual>()?.SetItemsPerColumn(25);

        AudioClip chopSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Harvest.wav");
        AudioClip collectSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Collect.wav");
        AudioClip buildSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Build.wav");

        GameAudio audio = movement.GetComponent<GameAudio>();
        if (audio == null) audio = movement.gameObject.AddComponent<GameAudio>();
        audio.Configure(chopSound, collectSound, buildSound);

        EquipmentInventory equipment = movement.GetComponent<EquipmentInventory>();
        if (equipment == null) equipment = movement.gameObject.AddComponent<EquipmentInventory>();
        equipment.Configure(inventory);

        ToolSwing swing = movement.GetComponent<ToolSwing>();
        if (swing == null) swing = movement.gameObject.AddComponent<ToolSwing>();
        swing.Configure(inventory, equipment, toolPrefabs);

        widgets.shopPanel.Configure(widgets.shopWiring, equipment, inventory, hud);

        GameObject campObject = GameObject.Find("CampPlaceholder");
        FenceUpgradeProject fenceProject = enclosure.GetComponent<FenceUpgradeProject>();
        if (fenceProject == null) fenceProject = enclosure.AddComponent<FenceUpgradeProject>();
        fenceProject.Configure(inventory, movement.transform, campObject.GetComponent<CampArea>(),
            enclosure.transform.Find("Ring_Small").gameObject,
            enclosure.transform.Find("Ring_Large").gameObject,
            enclosure.transform.Find("FenceBuildSpot"));

        WireCampFurniture(campFurniture, enclosure, inventory, hud, widgets);

        CampBase campBase = campObject.GetComponent<CampBase>();
        FenceEntranceBuilder entranceBuilder = enclosure.GetComponent<FenceEntranceBuilder>();
        if (entranceBuilder == null) entranceBuilder = enclosure.AddComponent<FenceEntranceBuilder>();
        entranceBuilder.Configure(inventory, hud, movement.transform, campBase, campObject.GetComponent<CampArea>(),
            enclosure.transform.Find("Ring_Small").gameObject, enclosure.transform.Find("Ring_Large").gameObject);
        widgets.buildPanel.Configure(widgets.buildWiring, fenceProject, entranceBuilder);
        widgets.menuController.Configure(movement.transform, campObject.GetComponent<CampArea>(),
            widgets.menuTabs, widgets.shopTab, widgets.buildTab, widgets.shopPanel, widgets.buildPanel);

        GameObject ground = GameObject.Find("Ground");
        if (ground != null)
        {
            InfiniteGround infiniteGround = ground.GetComponent<InfiniteGround>();
            if (infiniteGround == null) infiniteGround = ground.AddComponent<InfiniteGround>();
            infiniteGround.Configure(movement.transform, ground.GetComponent<Renderer>().sharedMaterial, 20f, 2);
        }

        new GameObject("MobileGameSettings", typeof(MobileGameSettings));
    }

    private static void WireCampFurniture(GameObject campFurniture, GameObject enclosure,
        ResourceInventory inventory, GameplayHUD hud, HudWidgets widgets)
    {
        WireTrapdoor(campFurniture, inventory);
        WireCabin(inventory, hud, widgets, enclosure, campFurniture);

        Transform plot = campFurniture.transform.Find("BuildPlot");
        BuildPlot buildPlot = plot.GetComponent<BuildPlot>();
        GameObject millPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MillPrefabPath);
        Transform marker = plot.Find("EmptyMarker");
        buildPlot.Configure(inventory, inventory.transform, widgets.plotPrompt, hud,
            marker.gameObject, millPrefab, marker.GetComponent<ScalePop>(), MillPrice);
    }

    private static void WireTrapdoor(GameObject campFurniture, ResourceInventory inventory)
    {
        Transform hatch = campFurniture.transform.Find("ResourceTrapdoor");
        ResourceTrapdoor trapdoor = hatch.GetComponent<ResourceTrapdoor>();
        trapdoor.Configure(inventory, hatch.Find("FlapLeft"), hatch.Find("FlapRight"),
            hatch.Find("DropPoint"), hatch.Find("CashPoint"),
            hatch.Find("Rim").GetComponent<ScalePop>(), 2.2f);

        // The old deposit circle is what the trapdoor replaces.
        GameObject zone = FindSceneObjects(new[] { "DepositZoneIndicator" }).FirstOrDefault();
        if (zone != null) UnityEngine.Object.DestroyImmediate(zone);
    }

    /// The cabin at the centre is both the till and the base.
    private static void WireCabin(ResourceInventory inventory, GameplayHUD hud, HudWidgets widgets,
        GameObject enclosure, GameObject campFurniture)
    {
        GameObject camp = GameObject.Find("CampPlaceholder");
        if (camp == null) throw new InvalidOperationException("CampPlaceholder is missing from the scene.");

        ScalePop cabinPop = camp.transform.Find("Cabin").GetComponent<ScalePop>();
        CashDeposit till = camp.GetComponent<CashDeposit>();
        if (till == null) till = camp.AddComponent<CashDeposit>();
        till.Configure(inventory, camp.transform.Find("Till"), cabinPop, CashPerBundle, 2.4f);

        if (camp.GetComponent<AudioSource>() == null) camp.AddComponent<AudioSource>();
        CampBase campBase = camp.GetComponent<CampBase>();
        if (campBase == null) campBase = camp.AddComponent<CampBase>();

        campBase.Configure(inventory, hud, widgets.basePrompt,
            camp.GetComponent<CampArea>(),
            enclosure.transform.Find("Ring_Small").gameObject,
            enclosure.transform.Find("Ring_Large").gameObject,
            campFurniture.transform.Find("BuildPlot").GetComponent<BuildPlot>(),
            camp.GetComponentInChildren<ParticleSystem>(true),
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Build.wav"),
            BaseUpgradeCost);
    }

    private static Transform EnsureChild(Transform parent, string name, Vector3 localPosition)
    {
        Transform existing = parent.Find(name);
        if (existing == null)
        {
            existing = new GameObject(name).transform;
            existing.SetParent(parent, false);
        }
        existing.localPosition = localPosition;
        return existing;
    }

    // ----- HUD ----------------------------------------------------------------

    private sealed class HudWidgets
    {
        public GameplayHUD.Bindings bindings = new GameplayHUD.Bindings();
        public ActionPrompt basePrompt;
        public ActionPrompt plotPrompt;
        public EquipmentShopPanel shopPanel;
        public EquipmentShopPanel.Wiring shopWiring;
        public FenceBuildPanel buildPanel;
        public FenceBuildPanel.Wiring buildWiring;
        public CampSideMenuController menuController;
        public GameObject menuTabs;
        public Button shopTab;
        public Button buildTab;
    }

    private static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.11f, 0.78f);
    private static readonly Color BarColor = new Color(0.05f, 0.07f, 0.08f, 0.85f);
    private static readonly Color ButtonColor = new Color(0.12f, 0.16f, 0.2f, 0.94f);
    private static readonly Color TextColor = new Color(0.94f, 0.96f, 0.92f);
    private static readonly Color MutedColor = new Color(0.64f, 0.69f, 0.72f);
    private static readonly Color AccentColor = new Color(0.96f, 0.78f, 0.22f);
    private static readonly Color MoneyColor = new Color(0.34f, 0.82f, 0.42f);
    private static readonly Color WoodBarColor = new Color(0.72f, 0.47f, 0.22f);
    private static readonly Color MeatBarColor = new Color(0.85f, 0.32f, 0.32f);
    private static readonly Color DangerColor = new Color(0.78f, 0.22f, 0.22f, 0.92f);

    private static Font uiFont;
    private static Sprite roundedSprite;
    private static Sprite circleSprite;

    /// Rebuilds every widget under SafeArea. The joystick lives on its own canvas
    /// and is left alone.
    private static HudWidgets BuildHud()
    {
        GameObject safeArea = FindSceneObjects(new[] { "SafeArea" }).FirstOrDefault();
        if (safeArea == null) throw new InvalidOperationException("SafeArea is missing from the HUD canvas.");

        ResolveUiResources(safeArea);
        SortCanvases(safeArea);
        for (int index = safeArea.transform.childCount - 1; index >= 0; index--)
            UnityEngine.Object.DestroyImmediate(safeArea.transform.GetChild(index).gameObject);

        Transform root = safeArea.transform;
        HudWidgets widgets = new HudWidgets();

        BuildCarryPanel(root, widgets);
        BuildWalletChips(root, widgets);
        BuildCampCompass(root, widgets);
        BuildFullBanner(root, widgets);
        BuildToasts(root, widgets);
        widgets.basePrompt = BuildActionPrompt(root, "BasePrompt", 300f);
        widgets.plotPrompt = BuildActionPrompt(root, "PlotPrompt", 300f);
        BuildCampMenuTabs(root, widgets);
        BuildShopPanel(root, widgets);
        BuildFencePanel(root, widgets);

        return widgets;
    }

    /// The HUD carries the full screen shop, so it has to draw and take taps above
    /// the joystick, which lives on its own canvas.
    private static void SortCanvases(GameObject safeArea)
    {
        Canvas hudCanvas = safeArea.GetComponentInParent<Canvas>();
        if (hudCanvas != null)
        {
            hudCanvas.overrideSorting = false;
            hudCanvas.sortingOrder = 10;
        }

        GameObject controls = FindSceneObjects(new[] { "MobileControls" }).FirstOrDefault();
        Canvas joystickCanvas = controls != null ? controls.GetComponent<Canvas>() : null;
        if (joystickCanvas != null) joystickCanvas.sortingOrder = 0;
    }

    private static void ResolveUiResources(GameObject safeArea)
    {
        roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        // Reuse whatever font the HUD already used, so nothing changes visually.
        Text existing = safeArea.GetComponentInChildren<Text>(true);
        uiFont = existing != null && existing.font != null
            ? existing.font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static void BuildCarryPanel(Transform root, HudWidgets widgets)
    {
        RectTransform panel = NewPanel(root, "CarryPanel", new Vector2(0f, 1f),
            new Vector2(245f, -104f), new Vector2(430f, 150f), PanelColor);
        widgets.bindings.carryPop = panel.gameObject.AddComponent<ScalePop>();

        NewCircle(panel, "CarryIcon", new Vector2(-170f, 30f), 52f, AccentColor);
        widgets.bindings.carryLabel = NewText(panel, "CarryCount", new Vector2(-5f, 30f),
            new Vector2(250f, 56f), 40, TextAnchor.MiddleLeft, TextColor);

        RectTransform bar = NewPanel(panel, "CarryBar", new Vector2(0.5f, 0.5f),
            new Vector2(6f, -38f), new Vector2(360f, 24f), BarColor);
        widgets.bindings.carryBar = bar;
        widgets.bindings.carryBarBackground = bar.GetComponent<Image>();
        widgets.bindings.carryWoodFill = NewBarSegment(bar, "WoodFill", WoodBarColor);
        widgets.bindings.carryMeatFill = NewBarSegment(bar, "MeatFill", MeatBarColor);
    }

    /// Dollars are the only currency, so there is only one chip to show.
    private static void BuildWalletChips(Transform root, HudWidgets widgets)
    {
        RectTransform money = NewPanel(root, "MoneyChip", new Vector2(1f, 1f),
            new Vector2(-160f, -104f), new Vector2(260f, 64f), PanelColor);
        widgets.bindings.moneyPop = money.gameObject.AddComponent<ScalePop>();
        NewCircle(money, "Dot", new Vector2(-98f, 0f), 34f, MoneyColor);
        widgets.bindings.moneyText = NewText(money, "Value", new Vector2(34f, 0f),
            new Vector2(170f, 52f), 36, TextAnchor.MiddleRight, TextColor);
    }

    private static void BuildCampCompass(Transform root, HudWidgets widgets)
    {
        RectTransform panel = NewPanel(root, "CampCompass", new Vector2(0.5f, 1f),
            new Vector2(0f, -240f), new Vector2(300f, 62f), PanelColor);

        Text arrow = NewText(panel, "Arrow", new Vector2(-104f, 2f), new Vector2(52f, 52f),
            44, TextAnchor.MiddleCenter, AccentColor);
        arrow.text = "▲";

        widgets.bindings.campArrow = arrow.rectTransform;
        widgets.bindings.campCompass = panel.gameObject;
        widgets.bindings.campDistanceText = NewText(panel, "Distance", new Vector2(28f, 0f),
            new Vector2(190f, 44f), 28, TextAnchor.MiddleLeft, TextColor);
        widgets.bindings.campDistanceText.text = "CAMP";
    }

    private static void BuildFullBanner(Transform root, HudWidgets widgets)
    {
        RectTransform banner = NewPanel(root, "InventoryFullBanner", new Vector2(0.5f, 1f),
            new Vector2(0f, -320f), new Vector2(620f, 76f), DangerColor);

        NewText(banner, "Label", Vector2.zero, new Vector2(600f, 60f), 32, TextAnchor.MiddleCenter, Color.white)
            .text = "FULL - SELL AT THE COUNTER";

        widgets.bindings.inventoryFullBanner = banner.gameObject;
        banner.gameObject.AddComponent<ScalePop>();
        banner.gameObject.SetActive(false);
    }

    private static void BuildToasts(Transform root, HudWidgets widgets)
    {
        Text[] slots = new Text[3];
        for (int index = 0; index < slots.Length; index++)
        {
            slots[index] = NewText(root, "Toast_" + index, new Vector2(0.5f, 1f),
                new Vector2(0f, -420f - index * 66f), new Vector2(660f, 60f),
                34, TextAnchor.MiddleCenter, TextColor);
            slots[index].gameObject.SetActive(false);
        }
        widgets.bindings.toastSlots = slots;
    }

    private static ActionPrompt BuildActionPrompt(Transform root, string name, float y)
    {
        RectTransform panel = NewPanel(root, name, new Vector2(0.5f, 0f),
            new Vector2(0f, y), new Vector2(580f, 220f), PanelColor);

        Text title = NewText(panel, "Title", new Vector2(0f, 66f), new Vector2(540f, 54f),
            34, TextAnchor.MiddleCenter, AccentColor);
        Text detail = NewText(panel, "Detail", new Vector2(0f, 18f), new Vector2(540f, 44f),
            26, TextAnchor.MiddleCenter, MutedColor);

        RectTransform button = NewPanel(panel, "ActionButton", new Vector2(0.5f, 0.5f),
            new Vector2(0f, -56f), new Vector2(440f, 78f), AccentColor);
        Text label = NewText(button, "Label", Vector2.zero, new Vector2(420f, 60f),
            32, TextAnchor.MiddleCenter, new Color(0.08f, 0.1f, 0.12f));

        ActionPrompt prompt = panel.gameObject.AddComponent<ActionPrompt>();
        prompt.Configure(panel.gameObject, title, detail, MakeButton(button), label);
        panel.gameObject.SetActive(false);
        return prompt;
    }

    /// Side scrolling shop: a header with the wallet and a close button, then
    /// one card per piece of equipment, tools first and backpacks after.
    private static void BuildShopPanel(Transform root, HudWidgets widgets)
    {
        RectTransform panel = NewRect(root, "ShopPanel", new Vector2(1f, 0.5f),
            new Vector2(-370f, 0f), new Vector2(700f, 1760f));
        Image backdrop = panel.gameObject.AddComponent<Image>();
        backdrop.sprite = roundedSprite;
        backdrop.type = Image.Type.Sliced;
        backdrop.color = new Color(0.05f, 0.06f, 0.08f, 0.97f);
        // Opaque to taps, so the world behind the popup cannot be poked through it.
        backdrop.raycastTarget = true;

        RectTransform header = NewRect(panel, "Header", new Vector2(0.5f, 1f),
            new Vector2(0f, -74f), new Vector2(660f, 108f));
        NewText(header, "Title", new Vector2(-220f, 0f), new Vector2(220f, 70f),
            46, TextAnchor.MiddleLeft, AccentColor).text = "SHOP";
        Text money = NewText(header, "Money", new Vector2(100f, 0f), new Vector2(220f, 64f),
            40, TextAnchor.MiddleRight, MoneyColor);

        RectTransform close = NewPanel(header, "CloseButton", new Vector2(0.5f, 0.5f),
            new Vector2(286f, 0f), new Vector2(82f, 82f), new Color(0.32f, 0.16f, 0.18f, 0.96f));
        NewText(close, "Label", Vector2.zero, new Vector2(90f, 70f), 40, TextAnchor.MiddleCenter, TextColor)
            .text = "X";
        Button closeButton = MakeButton(close);
        close.gameObject.AddComponent<ScalePop>();

        RectTransform viewport = NewStretch(panel, "Viewport", 30f, 30f, 30f, 150f);
        viewport.gameObject.AddComponent<RectMask2D>();

        const float cardHeight = 150f;
        const float cardGap = 14f;
        const float headerHeight = 64f;
        float contentHeight = 2f * headerHeight + (Tools.Count + Bags.Count) * (cardHeight + cardGap);

        RectTransform content = NewRect(viewport, "Content", new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(640f, contentHeight));
        content.pivot = new Vector2(0.5f, 1f);

        float cursor = 0f;
        EquipmentShopPanel.Card[] toolCards = new EquipmentShopPanel.Card[Tools.Count];
        cursor = BuildShopSection(content, "TOOLS", toolCards, "ToolCard", cursor, cardHeight, cardGap, headerHeight);

        EquipmentShopPanel.Card[] bagCards = new EquipmentShopPanel.Card[Bags.Count];
        BuildShopSection(content, "BACKPACKS", bagCards, "BagCard", cursor, cardHeight, cardGap, headerHeight);

        ScrollRect scroll = panel.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.elasticity = 0.12f;
        scroll.scrollSensitivity = 40f;
        scroll.inertia = true;
        scroll.decelerationRate = 0.12f;

        widgets.shopWiring = new EquipmentShopPanel.Wiring
        {
            panel = panel.gameObject,
            moneyText = money,
            closeButton = closeButton,
            toolCards = toolCards,
            bagCards = bagCards
        };
        widgets.shopPanel = panel.gameObject.AddComponent<EquipmentShopPanel>();
        panel.gameObject.SetActive(false);
    }

    private static float BuildShopSection(Transform content, string title, EquipmentShopPanel.Card[] cards,
        string cardName, float cursor, float cardHeight, float cardGap, float headerHeight)
    {
        NewText(content, "Section_" + title, new Vector2(0.5f, 1f),
            new Vector2(0f, -(cursor + headerHeight * 0.5f)), new Vector2(600f, headerHeight),
            30, TextAnchor.MiddleLeft, MutedColor).text = title;
        cursor += headerHeight;

        for (int index = 0; index < cards.Length; index++)
        {
            RectTransform card = NewPanel(content, cardName + "_" + index, new Vector2(0.5f, 1f),
                new Vector2(0f, -(cursor + cardHeight * 0.5f)), new Vector2(620f, cardHeight), ButtonColor);

            cards[index] = new EquipmentShopPanel.Card
            {
                background = card.GetComponent<Image>(),
                swatch = NewSwatch(card),
                nameText = NewText(card, "Name", new Vector2(-72f, 26f), new Vector2(390f, 50f),
                    34, TextAnchor.MiddleLeft, TextColor),
                detailText = NewText(card, "Detail", new Vector2(-72f, -28f), new Vector2(390f, 40f),
                    19, TextAnchor.MiddleLeft, MutedColor),
                priceText = NewText(card, "Price", new Vector2(224f, 0f), new Vector2(150f, 56f),
                    28, TextAnchor.MiddleRight, AccentColor),
                button = MakeButton(card)
            };
            card.gameObject.AddComponent<ScalePop>();
            cursor += cardHeight + cardGap;
        }
        return cursor;
    }

    private static void BuildCampMenuTabs(Transform root, HudWidgets widgets)
    {
        RectTransform tabs = NewRect(root, "CampMenuTabs", new Vector2(1f, 0.5f),
            new Vector2(-92f, 40f), new Vector2(164f, 220f));
        RectTransform shop = NewPanel(tabs, "ShopTab", new Vector2(0.5f, 0.5f),
            new Vector2(0f, 56f), new Vector2(160f, 92f), ButtonColor);
        NewText(shop, "Label", Vector2.zero, new Vector2(144f, 66f), 26,
            TextAnchor.MiddleCenter, TextColor).text = "SHOP";
        RectTransform build = NewPanel(tabs, "BuildTab", new Vector2(0.5f, 0.5f),
            new Vector2(0f, -56f), new Vector2(160f, 92f), AccentColor);
        NewText(build, "Label", Vector2.zero, new Vector2(144f, 66f), 26,
            TextAnchor.MiddleCenter, new Color(0.08f, 0.1f, 0.12f)).text = "BUILD";

        widgets.menuTabs = tabs.gameObject;
        widgets.shopTab = MakeButton(shop);
        widgets.buildTab = MakeButton(build);
        RectTransform controller = NewRect(root, "CampMenuController", Vector2.zero, Vector2.zero, Vector2.zero);
        widgets.menuController = controller.gameObject.AddComponent<CampSideMenuController>();
    }

    private static void BuildFencePanel(Transform root, HudWidgets widgets)
    {
        RectTransform panel = NewRect(root, "BuildPanel", new Vector2(1f, 0.5f),
            new Vector2(-350f, 0f), new Vector2(660f, 1220f));
        Image background = panel.gameObject.AddComponent<Image>();
        background.sprite = roundedSprite;
        background.type = Image.Type.Sliced;
        background.color = new Color(0.05f, 0.06f, 0.08f, 0.97f);
        background.raycastTarget = true;

        NewText(panel, "Title", new Vector2(-150f, 370f), new Vector2(300f, 70f), 44,
            TextAnchor.MiddleLeft, AccentColor).text = "BUILD";
        RectTransform close = NewPanel(panel, "CloseButton", new Vector2(0.5f, 0.5f),
            new Vector2(272f, 370f), new Vector2(82f, 82f), new Color(0.32f, 0.16f, 0.18f, 0.96f));
        NewText(close, "Label", Vector2.zero, new Vector2(70f, 64f), 38,
            TextAnchor.MiddleCenter, TextColor).text = "X";

        RectTransform card = NewPanel(panel, "FenceCard", new Vector2(0.5f, 0.5f),
            new Vector2(0f, 80f), new Vector2(580f, 430f), ButtonColor);
        NewCircle(card, "FenceIcon", new Vector2(-220f, 150f), 52f, WoodBarColor);
        Text current = NewText(card, "Current", new Vector2(26f, 150f), new Vector2(420f, 52f),
            30, TextAnchor.MiddleLeft, TextColor);
        Text next = NewText(card, "Next", new Vector2(0f, 72f), new Vector2(520f, 54f),
            28, TextAnchor.MiddleLeft, AccentColor);
        Text cost = NewText(card, "Cost", new Vector2(0f, 10f), new Vector2(520f, 50f),
            28, TextAnchor.MiddleLeft, MutedColor);
        RectTransform start = NewPanel(card, "StartButton", new Vector2(0.5f, 0.5f),
            new Vector2(0f, -118f), new Vector2(480f, 88f), AccentColor);
        Text startLabel = NewText(start, "Label", Vector2.zero, new Vector2(450f, 64f),
            28, TextAnchor.MiddleCenter, new Color(0.08f, 0.1f, 0.12f));

        widgets.buildWiring = new FenceBuildPanel.Wiring
        {
            panel = panel.gameObject,
            currentText = current,
            nextText = next,
            costText = cost,
            startButton = MakeButton(start),
            startLabel = startLabel,
            closeButton = MakeButton(close),
            entranceStatusText = NewText(panel, "EntranceStatus", new Vector2(0f, -210f), new Vector2(560f, 54f),
                30, TextAnchor.MiddleLeft, TextColor),
            entranceCostText = NewText(panel, "EntranceCost", new Vector2(0f, -270f), new Vector2(560f, 48f),
                24, TextAnchor.MiddleLeft, MutedColor)
        };
        RectTransform entrance = NewPanel(panel, "EntranceButton", new Vector2(0.5f, 0.5f),
            new Vector2(0f, -390f), new Vector2(540f, 88f), AccentColor);
        widgets.buildWiring.entranceButton = MakeButton(entrance);
        widgets.buildWiring.entranceLabel = NewText(entrance, "Label", Vector2.zero, new Vector2(510f, 64f),
            27, TextAnchor.MiddleCenter, new Color(0.08f, 0.1f, 0.12f));
        widgets.buildPanel = panel.gameObject.AddComponent<FenceBuildPanel>();
        panel.gameObject.SetActive(false);
    }

    private static Image NewSwatch(Transform parent)
    {
        RectTransform rect = NewRect(parent, "Swatch", new Vector2(0.5f, 0.5f),
            new Vector2(-292f, 0f), new Vector2(18f, 112f));
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        return image;
    }

    /// Rect that fills its parent minus a padding on each side.
    private static RectTransform NewStretch(Transform parent, string name,
        float left, float bottom, float right, float top)
    {
        GameObject item = new GameObject(name, typeof(RectTransform));
        item.layer = parent.gameObject.layer;
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    // ----- UI primitives ------------------------------------------------------

    private static Button MakeButton(RectTransform rect)
    {
        Image background = rect.GetComponent<Image>();
        // A button only receives taps if its own graphic is a raycast target.
        background.raycastTarget = true;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(0.42f, 0.46f, 0.5f, 0.7f);
        button.colors = colors;
        return button;
    }

    private static RectTransform NewRect(Transform parent, string name, Vector2 anchor,
        Vector2 anchoredPosition, Vector2 size)
    {
        GameObject item = new GameObject(name, typeof(RectTransform));
        item.layer = parent.gameObject.layer;
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return rect;
    }

    private static RectTransform NewPanel(Transform parent, string name, Vector2 anchor,
        Vector2 anchoredPosition, Vector2 size, Color color)
    {
        RectTransform rect = NewRect(parent, name, anchor, anchoredPosition, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static RectTransform NewCircle(Transform parent, string name, Vector2 anchoredPosition,
        float diameter, Color color)
    {
        RectTransform rect = NewRect(parent, name, new Vector2(0.5f, 0.5f), anchoredPosition,
            new Vector2(diameter, diameter));
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = circleSprite;
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    /// Left anchored strip whose width the HUD drives to show a fill level.
    private static RectTransform NewBarSegment(Transform parent, string name, Color color)
    {
        GameObject item = new GameObject(name, typeof(RectTransform));
        item.layer = parent.gameObject.layer;
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 24f);

        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        rect.gameObject.SetActive(false);
        return rect;
    }

    private static Text NewText(Transform parent, string name, Vector2 anchoredPosition,
        Vector2 size, int fontSize, TextAnchor alignment, Color color)
    {
        RectTransform rect = NewRect(parent, name, new Vector2(0.5f, 0.5f), anchoredPosition, size);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = uiFont;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Text NewText(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition,
        Vector2 size, int fontSize, TextAnchor alignment, Color color)
    {
        Text text = NewText(parent, name, anchoredPosition, size, fontSize, alignment, color);
        text.rectTransform.anchorMin = anchor;
        text.rectTransform.anchorMax = anchor;
        text.rectTransform.anchoredPosition = anchoredPosition;
        return text;
    }

    // ----- helpers ------------------------------------------------------------

    private static Transform AddPrimitive(Transform parent, PrimitiveType type, string name,
        Vector3 position, Vector3 scale, Material material, bool keepCollider = false)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = name;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = position;
        primitive.transform.localScale = scale;
        primitive.GetComponent<Renderer>().sharedMaterial = material;

        Collider primitiveCollider = primitive.GetComponent<Collider>();
        if (primitiveCollider != null && !keepCollider) UnityEngine.Object.DestroyImmediate(primitiveCollider);
        return primitive.transform;
    }

    private static Transform AddMesh(Transform parent, string name, Mesh mesh,
        Vector3 position, Vector3 scale, Material material)
    {
        GameObject item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        item.transform.SetParent(parent, false);
        item.transform.localPosition = position;
        item.transform.localScale = scale;
        item.GetComponent<MeshFilter>().sharedMesh = mesh;
        item.GetComponent<MeshRenderer>().sharedMaterial = material;
        return item.transform;
    }

    private static void SetPrivateReference(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null) return;
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject SavePrefab(GameObject root, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static Material CreateMaterial(string name, Color color)
    {
        string path = "Assets/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.14f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Transform FindHudChild(GameObject safeArea, string name)
    {
        if (safeArea == null) return null;
        return safeArea.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == name);
    }

    private static List<GameObject> FindSceneObjects(IEnumerable<string> names)
    {
        HashSet<string> wanted = new HashSet<string>(names);
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(item => item.scene.IsValid() && wanted.Contains(item.name))
            .ToList();
    }

    /// The sheep became a planned camp upgrade, so their assets are no longer part
    /// of the build. The legacy markers belonged to the deleted setup scripts.
    private static void CleanUpLegacyAssets()
    {
        string[] assets =
        {
            "Assets/Prefabs/SheepPlaceholder.prefab",
            "Assets/Materials/SheepWool_Mat.mat",
            "Assets/Materials/SheepSkin_Mat.mat",
            "Assets/Materials/SheepFace_Mat.mat",
            "Assets/Materials/WoolParticle_Mat.mat"
        };
        foreach (string path in assets)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null) AssetDatabase.DeleteAsset(path);
        }

        string[] markers =
        {
            "Library/sheep-farm-refactor-complete.txt", "Library/sheep-farm-self-test.txt",
            "Library/prototype-self-test.txt", "Library/step2-placeholder-setup-complete.txt",
            "Library/step3-gameplay-setup-complete.txt", "Library/step4-ui-setup-complete.txt",
            "Library/step5-harvest-setup-complete.txt", "Library/step6-inventory-setup-complete.txt",
            "Library/step7-camp-upgrade-setup-complete.txt"
        };
        foreach (string path in markers)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ----- self test ----------------------------------------------------------

    private static void RunSelfTest(Scene scene, GameObject enclosure, GameObject campFurniture,
        GameObject forest, GameObject wolves)
    {
        List<string> results = new List<string>();

        Require(FindSceneObjects(new[] { "SheepFarm" }).Count == 0, "Sheep removed from the camp", results);

        Transform smallRing = enclosure.transform.Find("Ring_Small");
        Transform largeRing = enclosure.transform.Find("Ring_Large");
        Require(smallRing != null && smallRing.gameObject.activeSelf, "Small park active at level one", results);
        Require(largeRing != null && !largeRing.gameObject.activeSelf, "Large park waiting for the upgrade", results);
        Require(smallRing.childCount == SmallRingSegments - 1, "Small ring with a single gate gap", results);
        Require(largeRing.childCount == LargeRingSegments - 1, "Large ring with a single gate gap", results);
        Require(smallRing.GetComponentInChildren<EnclosureGate>(true) != null
            && largeRing.GetComponentInChildren<EnclosureGate>(true) != null, "Both rings have a gate", results);

        ResourceTrapdoor trapdoor = UnityEngine.Object.FindFirstObjectByType<ResourceTrapdoor>();
        Require(trapdoor != null, "Trapdoor in the ground", results);
        Require(trapdoor.transform.Find("FlapLeft") != null && trapdoor.transform.Find("FlapRight") != null,
            "Trapdoor has two flaps", results);
        Require(trapdoor.PriceOf(ResourceType.Meat) > trapdoor.PriceOf(ResourceType.Wood),
            "Meat is worth more than wood", results);
        Require(FindSceneObjects(new[] { "DepositZoneIndicator" }).Count == 0, "Old deposit circle removed", results);

        CashDeposit till = UnityEngine.Object.FindFirstObjectByType<CashDeposit>();
        Require(till != null, "Cabin banks the carried cash", results);
        Require(till.transform.position.sqrMagnitude < 0.01f, "Cabin sits at the centre of the camp", results);
        Require(till.transform.Find("Cabin") != null && till.transform.Find("Plinth") != null,
            "Cabin has a plinth", results);
        Require(FindSceneObjects(new[] { "UpgradeableBuildingPlaceholder" }).Count == 0,
            "Old building on the left removed", results);
        Require(FindSceneObjects(new[] { "ToolShopStand" }).All(item => !item.activeSelf), "Old shop stand removed", results);

        BuildPlot plot = campFurniture.GetComponentInChildren<BuildPlot>(true);
        Require(plot != null && !plot.gameObject.activeSelf, "Build plot hidden until level two", results);

        ProceduralForest generator = forest.GetComponent<ProceduralForest>();
        Require(generator != null, "Procedural forest", results);
        Require(generator.CampClearRadius > LargeRingRadius, "Clearing fits the enlarged park", results);

        // Two trunks block the way when they are closer than twice the sum of the
        // tree collider and the player radius. Anything above that is walkable.
        const float blockingSpacing = 2f * (0.5f + 0.34f);
        Require(generator.TreeSpacing < blockingSpacing, "Forest packed tight enough to block the player", results);
        Require(ForestLayout.PathHalfWidth < 2.5f, "Narrow exploration paths", results);
        Require(wolves.GetComponent<WolfPackSpawner>() != null, "Wolf pack spawner", results);

        Require(LayerMask.NameToLayer(HarvestableLayerName) >= 0, "Harvestable layer", results);
        Require(AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/FirCone.asset") != null, "Conifer cone mesh", results);
        Require(AssetDatabase.LoadAssetAtPath<GameObject>(FencePrefabPath) != null, "Fence prefab", results);
        Require(AssetDatabase.LoadAssetAtPath<GameObject>(WolfPrefabPath) != null, "Wolf prefab", results);
        Require(AssetDatabase.LoadAssetAtPath<GameObject>(MillPrefabPath) != null, "Lumber mill prefab", results);
        for (int index = 0; index < ToolPrefabPaths.Length; index++)
        {
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(ToolPrefabPaths[index]) != null,
                "Tool prefab " + Tools.Get((ToolTier)index).label, results);
        }
        Require(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SheepPlaceholder.prefab") == null,
            "Sheep prefab removed", results);

        GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefabPath);
        Require(treePrefab != null && treePrefab.GetComponent<Harvestable>() != null, "Trees have hit points", results);
        Require(treePrefab.layer == LayerMask.NameToLayer(HarvestableLayerName), "Trees on the harvestable layer", results);
        Require(treePrefab.GetComponentsInChildren<MeshFilter>(true).Length == 4, "Conifer trunk and three cones", results);

        PlayerMovement movement = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
        Require(movement.GetComponent<ToolSwing>() != null, "Player swings the tool", results);
        Require(movement.GetComponent<EquipmentInventory>() != null, "Equipment inventory", results);

        // Bags are equipment, not levels, so the starter bag has to be the 100 slot
        // one and every later bag has to be a real jump.
        Require(Bags.Get(BagTier.Canvas).capacity == 100 && Bags.Get(BagTier.Canvas).price == 0,
            "Starter bag holds one hundred", results);
        for (int index = 1; index < Bags.Count; index++)
        {
            BagStats previous = Bags.Get((BagTier)(index - 1));
            BagStats current = Bags.Get((BagTier)index);
            Require(current.price > previous.price && current.capacity > previous.capacity,
                "Bag " + current.label + " is a real step up", results);
        }
        Require(movement.GetComponent<GameAudio>() != null, "Gameplay audio", results);
        Require(UnityEngine.Object.FindFirstObjectByType<CampBase>() != null, "Camp base upgrade", results);
        Require(UnityEngine.Object.FindFirstObjectByType<CampArea>() != null, "Camp area", results);
        FenceUpgradeProject fenceProject = UnityEngine.Object.FindFirstObjectByType<FenceUpgradeProject>();
        Require(fenceProject != null && FenceUpgradeProject.LevelCount == 5, "Five fence levels", results);
        Require(FindSceneObjects(new[] { "FenceBuildSpot" }).Count == 1, "Fence build spot", results);
        CarriedResourceVisual carryVisual = movement.GetComponent<CarriedResourceVisual>();
        Require(carryVisual != null && carryVisual.ItemsPerColumn == 25
            && CarriedResourceVisual.WoodPerVisualLog == 10, "Carried stack layout", results);

        // Tool progression has to be strictly increasing, or the shop is pointless.
        for (int index = 1; index < Tools.Count; index++)
        {
            ToolStats previous = Tools.Get((ToolTier)(index - 1));
            ToolStats current = Tools.Get((ToolTier)index);
            Require(current.price > previous.price && ToolOutput(current) > ToolOutput(previous),
                "Tool tier " + current.label + " is a real step up", results);
        }

        GameObject safeArea = FindSceneObjects(new[] { "SafeArea" }).FirstOrDefault();
        Require(safeArea != null, "HUD safe area", results);
        Require(FindHudChild(safeArea, "CarryPanel") != null, "Carry panel with capacity bar", results);
        Require(FindHudChild(safeArea, "WoodFill") != null && FindHudChild(safeArea, "MeatFill") != null,
            "Capacity bar split by resource", results);
        Require(FindHudChild(safeArea, "MoneyChip") != null, "Money chip", results);
        Require(FindHudChild(safeArea, "XpChip") == null, "Experience removed from the HUD", results);
        Require(FindHudChild(safeArea, "CampCompass") != null, "Camp compass", results);
        Require(FindHudChild(safeArea, "Toast_0") != null && FindHudChild(safeArea, "Toast_2") != null,
            "Stacked feedback toasts", results);
        Require(FindHudChild(safeArea, "ShopPanel") != null, "Side shop panel", results);
        Require(FindHudChild(safeArea, "BuildPanel") != null, "Side build panel", results);
        Require(FindHudChild(safeArea, "EntranceButton") != null, "Entrance build button", results);
        Require(FindHudChild(safeArea, "CampMenuTabs") != null, "Camp side menu tabs", results);
        Require(FindHudChild(safeArea, "ToolCard_4") != null && FindHudChild(safeArea, "BagCard_4") != null,
            "Shop lists every tool and every bag", results);
        Require(FindHudChild(safeArea, "Viewport") != null && FindHudChild(safeArea, "Content") != null,
            "Shop scrolls", results);
        Require(FindHudChild(safeArea, "CloseButton") != null, "Shop can be closed by hand", results);
        Require(FindHudChild(safeArea, "BasePrompt") != null && FindHudChild(safeArea, "PlotPrompt") != null,
            "Base and plot prompts", results);

        // Every purchase is a card in the shop now: no level up buttons anywhere.
        Require(FindHudChild(safeArea, "ActionButtons") == null, "Right hand button column removed", results);
        Require(FindHudChild(safeArea, "UpgradeButton") == null
            && FindHudChild(safeArea, "BackpackButton") == null, "No level up buttons left", results);

        // The popup has to sit above the joystick canvas, or taps land behind it.
        Canvas hudCanvas = safeArea.GetComponentInParent<Canvas>();
        Canvas joystickCanvas = FindSceneObjects(new[] { "MobileControls" })
            .Select(item => item.GetComponent<Canvas>())
            .FirstOrDefault(item => item != null);
        Require(hudCanvas != null && joystickCanvas != null
            && hudCanvas.sortingOrder > joystickCanvas.sortingOrder, "Shop draws above the joystick", results);

        // The regression that made the HUD unclickable: a button whose own graphic
        // is not a raycast target never receives a tap.
        Button[] buttons = safeArea.GetComponentsInChildren<Button>(true);
        Require(buttons.Length >= 10, "Every interactive widget is a button", results);
        Require(buttons.All(button => button.targetGraphic != null && button.targetGraphic.raycastTarget),
            "HUD buttons receive taps", results);
        Require(FindSceneObjects(new[] { "JoystickBackground" }).Count == 1, "Joystick untouched", results);

        Require(FindSceneObjects(new[] { "GroundTile" }).Count == 0, "No ground tiles saved in the scene", results);
        Require(EditorBuildSettings.scenes.Any(item => item.enabled && item.path == scene.path),
            "Scene included in build", results);

        File.WriteAllLines(TestReport, new[] { "PASS", DateTime.UtcNow.ToString("O") }.Concat(results));
    }

    /// What a tool is actually worth in a dense forest: damage per second times how
    /// much of the swing sweeps. The brushcutter has less single target output than
    /// the chainsaw and is still the better tool, because it fells a whole row.
    private static float ToolOutput(ToolStats stats)
    {
        return stats.damage * stats.swingsPerSecond * (stats.arcDegrees / 100f) * stats.resourceBonus;
    }

    private static void Require(bool condition, string label, List<string> results)
    {
        if (!condition) throw new InvalidOperationException("Self-test failed: " + label);
        results.Add("PASS - " + label);
    }
}
