using System;
using UnityEngine;
using UnityEngine.UI;

/// Reusable "walk up and buy it" panel: a title, a line of detail, a price and one
/// button. The base upgrade and the build plot both drive one of these instead of
/// each carrying its own copy of the same UI.
public class ActionPrompt : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Text titleText;
    [SerializeField] private Text detailText;
    [SerializeField] private Button actionButton;
    [SerializeField] private Text actionLabel;
    [SerializeField] private Color affordableColor = new Color(0.96f, 0.78f, 0.22f);
    [SerializeField] private Color lockedColor = new Color(0.4f, 0.44f, 0.48f, 0.85f);

    public event Action Clicked;

    private Image buttonBackground;
    private ScalePop panelPop;
    private string shownTitle;
    private string shownDetail;
    private string shownAction;

    public bool IsShown => panel != null && panel.activeSelf;

    public void Configure(GameObject newPanel, Text newTitle, Text newDetail, Button newButton, Text newLabel)
    {
        panel = newPanel;
        titleText = newTitle;
        detailText = newDetail;
        actionButton = newButton;
        actionLabel = newLabel;
    }

    private void Awake()
    {
        if (panel == null) panel = gameObject;
        panelPop = panel.GetComponent<ScalePop>();
        if (panelPop == null) panelPop = panel.AddComponent<ScalePop>();

        if (actionButton == null) return;
        buttonBackground = actionButton.GetComponent<Image>();
        actionButton.onClick.RemoveListener(RaiseClicked);
        actionButton.onClick.AddListener(RaiseClicked);
    }

    public void Show(string title, string detail, string action, bool affordable)
    {
        if (panel == null) return;

        if (!panel.activeSelf)
        {
            panel.SetActive(true);
            panelPop.Pop(0.24f);
        }

        if (shownTitle != title && titleText != null)
        {
            shownTitle = title;
            titleText.text = title;
        }
        if (shownDetail != detail && detailText != null)
        {
            shownDetail = detail;
            detailText.text = detail;
        }
        if (shownAction != action && actionLabel != null)
        {
            shownAction = action;
            actionLabel.text = action;
        }

        if (actionButton != null) actionButton.interactable = affordable;
        if (buttonBackground != null) buttonBackground.color = affordable ? affordableColor : lockedColor;
    }

    public void Hide()
    {
        if (panel == null || !panel.activeSelf) return;
        panel.SetActive(false);
    }

    private void RaiseClicked()
    {
        Clicked?.Invoke();
    }
}
