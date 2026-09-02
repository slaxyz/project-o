using System.Collections.Generic;
using UnityEngine;

/// The player swings the tool held in hand. It winds up, chops through an arc in
/// front of the character and springs back, and the damage lands on the frame the
/// blade passes through. The tool is holstered while inside the camp.
public class ToolSwing : MonoBehaviour
{
    private const int BufferSize = 32;
    private static readonly Collider[] Buffer = new Collider[BufferSize];

    [SerializeField] private ResourceInventory inventory;
    [SerializeField] private EquipmentInventory equipment;
    [SerializeField] private GameObject[] toolPrefabs = new GameObject[Tools.Count];
    [SerializeField] private Vector3 handOffset = new Vector3(0.4f, 1.02f, 0.16f);
    [SerializeField, Min(0.5f)] private float holsterSpeed = 6f;
    [SerializeField, Min(0.05f)] private float minimumSwingDuration = 0.22f;
    [SerializeField] private float windUpAngle = -62f;
    [SerializeField] private float chopAngle = 82f;

    private readonly List<Harvestable> struck = new List<Harvestable>();
    private Transform hand;
    private ScalePop handPop;
    private GameObject heldTool;
    private int heldTier = -1;
    private float deployment = 1f;
    private float swingElapsed = -1f;
    private float swingDuration = 0.3f;
    private float nextSwingTime;
    private bool hitApplied;
    private int layerMask;

    public bool IsHolstered => deployment < 0.35f;

    public void Configure(ResourceInventory newInventory, EquipmentInventory newEquipment, GameObject[] newToolPrefabs)
    {
        inventory = newInventory;
        equipment = newEquipment;
        if (newToolPrefabs != null) toolPrefabs = newToolPrefabs;
    }

    private void Awake()
    {
        if (inventory == null) inventory = GetComponent<ResourceInventory>();
        if (equipment == null) equipment = GetComponent<EquipmentInventory>();

        int harvestableLayer = LayerMask.NameToLayer("Harvestable");
        layerMask = harvestableLayer >= 0 ? 1 << harvestableLayer : Physics.AllLayers;
        EnsureHand();
    }

    private void OnEnable()
    {
        if (equipment != null) equipment.Changed += RefreshHeldTool;
    }

    private void OnDisable()
    {
        if (equipment != null) equipment.Changed -= RefreshHeldTool;
    }

    private void Start()
    {
        RefreshHeldTool();
    }

    private void Update()
    {
        if (hand == null) return;

        // Holstered in camp, drawn back out with a spring on the way out.
        bool inCamp = CampArea.IsInside(transform.position);
        deployment = Mathf.MoveTowards(deployment, inCamp ? 0f : 1f, holsterSpeed * Time.deltaTime);

        bool visible = deployment > 0.001f;
        if (hand.gameObject.activeSelf != visible) hand.gameObject.SetActive(visible);
        if (!visible)
        {
            swingElapsed = -1f;
            return;
        }

        hand.localScale = Vector3.one * Mathf.Max(0.01f, Juice.EaseOutBack(deployment));

        if (swingElapsed >= 0f) AdvanceSwing();
        else if (!IsHolstered && Time.time >= nextSwingTime && HasTargetInReach()) BeginSwing();
        else hand.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 2f) * 3f, 0f, 0f);
    }

    private void BeginSwing()
    {
        float rate = equipment != null ? Mathf.Max(0.2f, equipment.SwingsPerSecond) : 1.6f;
        swingDuration = Mathf.Max(minimumSwingDuration, 1f / rate);
        swingElapsed = 0f;
        hitApplied = false;
        nextSwingTime = Time.time + swingDuration;
    }

    private void AdvanceSwing()
    {
        swingElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(swingElapsed / swingDuration);

        float angle;
        if (progress < 0.3f)
        {
            angle = Mathf.Lerp(0f, windUpAngle, Juice.EaseOutCubic(progress / 0.3f));
        }
        else if (progress < 0.52f)
        {
            angle = Mathf.Lerp(windUpAngle, chopAngle, Juice.EaseInCubic((progress - 0.3f) / 0.22f));
        }
        else
        {
            angle = Mathf.Lerp(chopAngle, 0f, Juice.EaseOutBack((progress - 0.52f) / 0.48f));
        }
        hand.localRotation = Quaternion.Euler(angle, 0f, 0f);

        if (!hitApplied && progress >= 0.5f)
        {
            hitApplied = true;
            ApplyHit();
        }

        if (progress < 1f) return;
        swingElapsed = -1f;
        hand.localRotation = Quaternion.identity;
    }

    /// Damages everything inside the arc the blade just travelled through.
    private void ApplyHit()
    {
        float range = equipment != null ? equipment.Range : 2.2f;
        float halfArc = (equipment != null ? equipment.ArcDegrees : 100f) * 0.5f;
        float damage = equipment != null ? equipment.Damage : 1f;
        float bonus = equipment != null ? equipment.ResourceBonus : 1f;

        struck.Clear();
        Vector3 origin = transform.position + Vector3.up * 0.6f;
        int count = Physics.OverlapSphereNonAlloc(origin, range, Buffer, layerMask, QueryTriggerInteraction.Collide);

        for (int index = 0; index < count; index++)
        {
            Harvestable target = Buffer[index].GetComponentInParent<Harvestable>();
            if (target == null || !target.IsAlive || struck.Contains(target)) continue;

            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f && Vector3.Angle(transform.forward, toTarget) > halfArc) continue;

            struck.Add(target);
            target.TakeDamage(damage, origin, inventory, bonus);
        }

        if (struck.Count == 0) return;
        if (handPop != null) handPop.Pop(0.3f);
    }

    private bool HasTargetInReach()
    {
        float range = equipment != null ? equipment.Range : 2.2f;
        float halfArc = (equipment != null ? equipment.ArcDegrees : 100f) * 0.5f;
        Vector3 origin = transform.position + Vector3.up * 0.6f;
        int count = Physics.OverlapSphereNonAlloc(origin, range, Buffer, layerMask, QueryTriggerInteraction.Collide);

        for (int index = 0; index < count; index++)
        {
            Harvestable target = Buffer[index].GetComponentInParent<Harvestable>();
            if (target == null || !target.IsAlive) continue;

            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f && Vector3.Angle(transform.forward, toTarget) > halfArc) continue;
            return true;
        }
        return false;
    }

    private void RefreshHeldTool()
    {
        if (equipment == null || !Application.isPlaying) return;

        int tier = (int)equipment.EquippedTool;
        if (tier == heldTier && heldTool != null) return;

        EnsureHand();
        if (heldTool != null) Destroy(heldTool);
        heldTier = tier;

        GameObject prefab = toolPrefabs != null && tier < toolPrefabs.Length ? toolPrefabs[tier] : null;
        heldTool = prefab != null ? Instantiate(prefab, hand) : BuildFallbackTool();
        heldTool.transform.SetParent(hand, false);
        heldTool.transform.localPosition = Vector3.zero;
        heldTool.transform.localRotation = Quaternion.identity;
        heldTool.transform.localScale = Vector3.one;

        if (handPop != null) handPop.Pop(0.35f);
    }

    private void EnsureHand()
    {
        if (hand != null || !Application.isPlaying) return;

        hand = new GameObject("ToolHand").transform;
        hand.SetParent(transform, false);
        hand.localPosition = handOffset;
        hand.localRotation = Quaternion.identity;
        handPop = hand.gameObject.AddComponent<ScalePop>();
        handPop.Configure(0.24f, 0.3f, false);
    }

    /// Keeps the feature alive if no tool prefab is wired.
    private GameObject BuildFallbackTool()
    {
        GameObject root = new GameObject("Tool");
        GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade.name = "Blade";
        blade.transform.SetParent(root.transform, false);
        blade.transform.localPosition = new Vector3(0f, -0.35f, 0.1f);
        blade.transform.localScale = new Vector3(0.2f, 0.26f, 0.08f);
        blade.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Solid(new Color(0.78f, 0.82f, 0.88f));
        Destroy(blade.GetComponent<Collider>());
        return root;
    }

    private void OnDrawGizmosSelected()
    {
        float range = equipment != null ? equipment.Range : 2.2f;
        Gizmos.color = new Color(0.86f, 0.92f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.6f, range);
    }
}
