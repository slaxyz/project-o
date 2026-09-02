using System;
using UnityEngine;
using UnityEngine.UI;

/// Side shop. One scrolling list of equipment cards: the five tools, then
/// the five backpacks. A card you do not own shows its price, a card you own shows
/// EQUIP, and the one in use shows EQUIPPED. Nothing has levels.
public class EquipmentShopPanel : MonoBehaviour
{
    [Serializable]
    public class Card
    {
        public Button button;
        public Image background;
        public Image swatch;
        public Text nameText;
        public Text detailText;
        public Text priceText;
    }

    [Serializable]
    public class Wiring
    {
        public GameObject panel;
        public Text moneyText;
        public Button closeButton;
        public Card[] toolCards;
        public Card[] bagCards;
    }

    [SerializeField] private GameObject panel;
    [SerializeField] private Text moneyText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Card[] toolCards = new Card[Tools.Count];
    [SerializeField] private Card[] bagCards = new Card[Bags.Count];
    [SerializeField] private EquipmentInventory equipment;
    [SerializeField] private ResourceInventory wallet;
    [SerializeField] private GameplayHUD hud;

    private static readonly Color EquippedColor = new Color(0.18f, 0.42f, 0.26f, 0.96f);
    private static readonly Color OwnedColor = new Color(0.15f, 0.21f, 0.26f, 0.96f);
    private static readonly Color AffordableColor = new Color(0.17f, 0.29f, 0.4f, 0.96f);
    private static readonly Color LockedColor = new Color(0.12f, 0.13f, 0.15f, 0.94f);

    private ScalePop panelPop;
    private readonly ScalePop[] toolPops = new ScalePop[Tools.Count];
    private readonly ScalePop[] bagPops = new ScalePop[Bags.Count];
    private int shownMoney = -1;

    /// Closed by hand stays closed until the player walks away and comes back.
    public bool DismissedByPlayer { get; private set; }

    public bool IsOpen => panel != null && panel.activeSelf;

    public void Configure(Wiring wiring, EquipmentInventory newEquipment,
        ResourceInventory newWallet, GameplayHUD newHud)
    {
        panel = wiring.panel;
        moneyText = wiring.moneyText;
        closeButton = wiring.closeButton;
        toolCards = wiring.toolCards;
        bagCards = wiring.bagCards;
        equipment = newEquipment;
        wallet = newWallet;
        hud = newHud;
    }

    private void Awake()
    {
        if (panel == null) panel = gameObject;
        panelPop = panel.GetComponent<ScalePop>();
        if (panelPop == null) panelPop = panel.AddComponent<ScalePop>();

        for (int index = 0; index < toolCards.Length && index < Tools.Count; index++)
        {
            Card card = toolCards[index];
            if (card == null || card.button == null) continue;

            ToolTier tier = (ToolTier)index;
            toolPops[index] = card.button.GetComponent<ScalePop>();
            card.button.onClick.AddListener(() => OnToolClicked(tier));
        }

        for (int index = 0; index < bagCards.Length && index < Bags.Count; index++)
        {
            Card card = bagCards[index];
            if (card == null || card.button == null) continue;

            BagTier tier = (BagTier)index;
            bagPops[index] = card.button.GetComponent<ScalePop>();
            card.button.onClick.AddListener(() => OnBagClicked(tier));
        }

        if (closeButton != null) closeButton.onClick.AddListener(CloseByPlayer);
    }

    public void Open()
    {
        if (panel == null || panel.activeSelf || DismissedByPlayer) return;

        panel.SetActive(true);
        panelPop.Pop(0.16f);
        Refresh();
    }

    public void OpenFromMenu()
    {
        DismissedByPlayer = false;
        Open();
    }

    public void Close()
    {
        DismissedByPlayer = false;
        if (panel == null || !panel.activeSelf) return;
        panel.SetActive(false);
    }

    private void CloseByPlayer()
    {
        DismissedByPlayer = true;
        if (panel != null) panel.SetActive(false);
    }

    private void Update()
    {
        if (IsOpen) Refresh();
    }

    private void OnToolClicked(ToolTier tier)
    {
        if (equipment == null) return;

        if (equipment.OwnsTool(tier))
        {
            if (equipment.EquipTool(tier) && hud != null) hud.ShowEquipped(Tools.Get(tier).label);
            return;
        }

        ToolStats stats = Tools.Get(tier);
        if (equipment.TryBuyTool(tier))
        {
            if (toolPops[(int)tier] != null) toolPops[(int)tier].Pop(0.32f);
            GameAudio.PlayBuild();
            if (hud != null) hud.ShowBought(stats.label);
            return;
        }

        if (hud != null) hud.ShowNotEnoughMoney(stats.price);
    }

    private void OnBagClicked(BagTier tier)
    {
        if (equipment == null) return;

        if (equipment.OwnsBag(tier))
        {
            if (equipment.EquipBag(tier) && hud != null) hud.ShowEquipped(Bags.Get(tier).label);
            return;
        }

        BagStats stats = Bags.Get(tier);
        if (equipment.TryBuyBag(tier))
        {
            if (bagPops[(int)tier] != null) bagPops[(int)tier].Pop(0.32f);
            GameAudio.PlayBuild();
            if (hud != null) hud.ShowBought(stats.label);
            return;
        }

        if (hud != null) hud.ShowNotEnoughMoney(stats.price);
    }

    private void Refresh()
    {
        if (equipment == null || wallet == null) return;

        if (moneyText != null && shownMoney != wallet.Money)
        {
            shownMoney = wallet.Money;
            moneyText.text = "$" + wallet.Money;
        }

        for (int index = 0; index < toolCards.Length && index < Tools.Count; index++)
        {
            ToolTier tier = (ToolTier)index;
            ToolStats stats = Tools.Get(tier);
            string detail = stats.damage.ToString("0.#") + " DMG   "
                + stats.swingsPerSecond.ToString("0.#") + "/S   "
                + Mathf.RoundToInt(stats.arcDegrees) + "°   " + stats.flavour;
            PaintCard(toolCards[index], stats.label, detail, stats.price, stats.tint,
                equipment.OwnsTool(tier), equipment.EquippedTool == tier);
        }

        for (int index = 0; index < bagCards.Length && index < Bags.Count; index++)
        {
            BagTier tier = (BagTier)index;
            BagStats stats = Bags.Get(tier);
            string detail = stats.capacity + " SLOTS   " + stats.flavour;
            PaintCard(bagCards[index], stats.label, detail, stats.price, stats.tint,
                equipment.OwnsBag(tier), equipment.EquippedBag == tier);
        }
    }

    private void PaintCard(Card card, string label, string detail, int price, Color tint,
        bool owned, bool equipped)
    {
        if (card == null) return;

        bool affordable = !owned && wallet.Money >= price;

        if (card.nameText != null) card.nameText.text = label;
        if (card.detailText != null) card.detailText.text = detail;
        if (card.swatch != null) card.swatch.color = owned ? tint : tint * 0.45f;
        if (card.priceText != null)
        {
            card.priceText.text = equipped ? "EQUIPPED" : owned ? "EQUIP" : "$" + price;
            card.priceText.color = equipped
                ? new Color(0.56f, 0.94f, 0.62f)
                : owned ? new Color(0.86f, 0.92f, 1f)
                : affordable ? new Color(0.96f, 0.78f, 0.22f) : new Color(0.55f, 0.58f, 0.62f);
        }
        if (card.background != null)
        {
            card.background.color = equipped ? EquippedColor
                : owned ? OwnedColor
                : affordable ? AffordableColor : LockedColor;
        }
        if (card.button != null) card.button.interactable = owned || affordable;
    }
}
