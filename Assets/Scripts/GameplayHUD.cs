using System;
using UnityEngine;
using UnityEngine.UI;

/// Portrait HUD. Every widget is built by WorldSetup and handed over through
/// Bindings, so the layout is reproducible and this class only has to drive it.
public class GameplayHUD : MonoBehaviour
{
    [Serializable]
    public class Bindings
    {
        public Text carryLabel;
        public RectTransform carryBar;
        public RectTransform carryWoodFill;
        public RectTransform carryMeatFill;
        public RectTransform carryCashFill;
        public Image carryBarBackground;
        public ScalePop carryPop;
        public GameObject inventoryFullBanner;
        public Text moneyText;
        public ScalePop moneyPop;
        public Text[] toastSlots;
        public GameObject campCompass;
        public RectTransform campArrow;
        public Text campDistanceText;
    }

    [SerializeField] private Bindings ui = new Bindings();
    [SerializeField] private float toastLifetime = 1.8f;
    [SerializeField] private float toastPopDuration = 0.26f;
    [SerializeField] private Color barIdleColor = new Color(0.05f, 0.07f, 0.08f, 0.85f);
    [SerializeField] private Color barFullColor = new Color(0.62f, 0.16f, 0.16f, 0.9f);

    private struct Toast
    {
        public string message;
        public Color color;
        public float bornAt;
    }

    private Toast[] toasts = new Toast[0];
    private Transform player;
    private CampArea camp;
    private int carriedTotal;
    private int inventoryCapacity = 50;
    private int shownMoney = -1;
    private int shownDistance = -1;

    public void Bind(Bindings bindings)
    {
        ui = bindings;
        toasts = new Toast[ui.toastSlots != null ? ui.toastSlots.Length : 0];
        SetCarried(0, 0, 0, inventoryCapacity);
        SetWallet(0);
        RefreshToasts();
    }

    private void Awake()
    {
        toasts = new Toast[ui.toastSlots != null ? ui.toastSlots.Length : 0];
    }

    private void Start()
    {
        PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
        if (movement != null) player = movement.transform;
        camp = FindFirstObjectByType<CampArea>();
        RefreshToasts();
    }

    private void Update()
    {
        RefreshToasts();
        RefreshCompass();
    }

    // ----- resources ----------------------------------------------------------

    public void SetCarried(int wood, int meat, int cash, int capacity)
    {
        inventoryCapacity = Mathf.Max(1, capacity);
        int total = Mathf.Clamp(wood + meat + cash, 0, inventoryCapacity);
        bool grew = total > carriedTotal;
        carriedTotal = total;

        if (ui.carryLabel != null) ui.carryLabel.text = carriedTotal + " / " + inventoryCapacity;
        if (grew && ui.carryPop != null) ui.carryPop.Pop(0.1f);

        // Two segments side by side, so the mix of wood and meat is readable.
        if (ui.carryBar != null)
        {
            float width = ui.carryBar.rect.width;
            float woodWidth = width * Mathf.Clamp01(wood / (float)inventoryCapacity);
            float meatWidth = width * Mathf.Clamp01(meat / (float)inventoryCapacity);
            float cashWidth = width * Mathf.Clamp01(cash / (float)inventoryCapacity);
            SetSegment(ui.carryWoodFill, 0f, woodWidth);
            SetSegment(ui.carryMeatFill, woodWidth, meatWidth);
            SetSegment(ui.carryCashFill, woodWidth + meatWidth, cashWidth);
        }

        bool isFull = carriedTotal >= inventoryCapacity;
        if (ui.carryBarBackground != null) ui.carryBarBackground.color = isFull ? barFullColor : barIdleColor;
        if (ui.inventoryFullBanner != null && ui.inventoryFullBanner.activeSelf != isFull)
            ui.inventoryFullBanner.SetActive(isFull);
    }

    private static void SetSegment(RectTransform segment, float offset, float width)
    {
        if (segment == null) return;

        Vector2 size = segment.sizeDelta;
        segment.sizeDelta = new Vector2(Mathf.Max(0f, width), size.y);
        segment.anchoredPosition = new Vector2(offset, 0f);

        bool visible = width > 0.5f;
        if (segment.gameObject.activeSelf != visible) segment.gameObject.SetActive(visible);
    }

    public void SetWallet(int money)
    {
        if (ui.moneyText == null || shownMoney == money) return;
        shownMoney = money;
        ui.moneyText.text = "$" + Mathf.Max(0, money);
    }

    public void PulseMoney()
    {
        if (ui.moneyPop != null) ui.moneyPop.Pop(0.2f);
    }

    // ----- toasts -------------------------------------------------------------

    public void ShowResourceGained(ResourceType type, int amount)
    {
        Push("+" + amount + " " + ResourceTypes.Label(type), ResourceTypes.FeedbackTint(type));
    }

    public void ShowDropped(int itemCount)
    {
        Push("DROPPED " + itemCount, new Color(0.36f, 0.9f, 0.46f));
    }

    public void ShowUpgradeSuccess()
    {
        Push("PARK ENLARGED", new Color(0.98f, 0.48f, 0.28f));
    }

    public void ShowBought(string label)
    {
        Push(label + " BOUGHT", new Color(0.96f, 0.78f, 0.22f));
    }

    public void ShowEquipped(string label)
    {
        Push(label, new Color(0.86f, 0.92f, 1f));
    }

    public void ShowCashBanked(int amount)
    {
        Push("BANKED $" + amount, new Color(0.46f, 0.92f, 0.48f));
    }

    public void ShowBuilt(string label)
    {
        Push(label + " BUILT", new Color(0.36f, 0.9f, 0.46f));
    }

    public void ShowNotEnoughMoney(int cost)
    {
        Push("NEEDS $" + cost, new Color(0.94f, 0.42f, 0.42f));
    }

    /// Pushes older messages down instead of overwriting them, so two rewards
    /// landing on the same frame are both readable.
    private void Push(string message, Color color)
    {
        if (toasts.Length == 0) return;

        for (int index = toasts.Length - 1; index > 0; index--) toasts[index] = toasts[index - 1];
        toasts[0] = new Toast { message = message, color = color, bornAt = Time.time };
        RefreshToasts();
    }

    private void RefreshToasts()
    {
        if (ui.toastSlots == null) return;

        for (int index = 0; index < ui.toastSlots.Length && index < toasts.Length; index++)
        {
            Text slot = ui.toastSlots[index];
            if (slot == null) continue;

            Toast toast = toasts[index];
            float age = toast.bornAt <= 0f ? float.MaxValue : Time.time - toast.bornAt;
            bool alive = age < toastLifetime;

            if (slot.gameObject.activeSelf != alive) slot.gameObject.SetActive(alive);
            if (!alive) continue;

            if (slot.text != toast.message) slot.text = toast.message;

            float fade = Mathf.Clamp01((toastLifetime - age) / 0.45f);
            Color target = toast.color;
            target.a = fade;
            slot.color = target;

            // Pops in, then drifts up slightly as it fades.
            float grow = Juice.EaseOutBack(age / toastPopDuration);
            slot.rectTransform.localScale = Vector3.one * Mathf.Max(0.01f, grow);
        }
    }

    // ----- camp compass -------------------------------------------------------

    /// Small arrow pointing home. The forest is dense enough that losing the camp
    /// is otherwise very easy.
    private void RefreshCompass()
    {
        if (ui.campCompass == null || player == null || camp == null) return;

        Vector3 offset = camp.Center - player.position;
        offset.y = 0f;
        float distance = offset.magnitude;
        bool inside = distance <= camp.Radius;

        if (ui.campCompass.activeSelf == inside) ui.campCompass.SetActive(!inside);
        if (inside) return;

        // The camera looks straight down the Z axis with no yaw, so world +Z is up.
        if (ui.campArrow != null)
        {
            float angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            ui.campArrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }

        int meters = Mathf.RoundToInt(distance);
        if (meters == shownDistance || ui.campDistanceText == null) return;
        shownDistance = meters;
        ui.campDistanceText.text = "CAMP  " + meters + " m";
    }
}
