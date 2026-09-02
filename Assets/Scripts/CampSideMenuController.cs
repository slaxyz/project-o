using UnityEngine;
using UnityEngine.UI;

public class CampSideMenuController : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private CampArea camp;
    [SerializeField] private GameObject tabs;
    [SerializeField] private Button shopButton;
    [SerializeField] private Button buildButton;
    [SerializeField] private EquipmentShopPanel shopPanel;
    [SerializeField] private FenceBuildPanel buildPanel;

    public void Configure(Transform newPlayer, CampArea newCamp, GameObject newTabs,
        Button newShopButton, Button newBuildButton, EquipmentShopPanel newShopPanel, FenceBuildPanel newBuildPanel)
    {
        player = newPlayer;
        camp = newCamp;
        tabs = newTabs;
        shopButton = newShopButton;
        buildButton = newBuildButton;
        shopPanel = newShopPanel;
        buildPanel = newBuildPanel;
    }

    private void Awake()
    {
        if (shopButton != null) shopButton.onClick.AddListener(OpenShop);
        if (buildButton != null) buildButton.onClick.AddListener(OpenBuild);
    }

    private void Update()
    {
        if (player == null || camp == null || tabs == null) return;
        bool inside = camp.Contains(player.position);
        if (tabs.activeSelf != inside) tabs.SetActive(inside);
        if (inside) return;
        shopPanel?.Close();
        buildPanel?.Close();
    }

    private void OpenShop()
    {
        buildPanel?.Close();
        shopPanel?.OpenFromMenu();
    }

    private void OpenBuild()
    {
        shopPanel?.Close();
        buildPanel?.Open();
    }
}
