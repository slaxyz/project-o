using System;
using UnityEngine;
using UnityEngine.UI;

public class FenceBuildPanel : MonoBehaviour
{
    [Serializable]
    public class Wiring
    {
        public GameObject panel;
        public Text currentText;
        public Text nextText;
        public Text costText;
        public Button startButton;
        public Text startLabel;
        public Button closeButton;
        public Text entranceStatusText;
        public Text entranceCostText;
        public Button entranceButton;
        public Text entranceLabel;
    }

    [SerializeField] private Wiring ui = new Wiring();
    [SerializeField] private FenceUpgradeProject project;
    [SerializeField] private FenceEntranceBuilder entranceBuilder;

    public bool IsOpen => ui.panel != null && ui.panel.activeSelf;

    public void Configure(Wiring wiring, FenceUpgradeProject newProject, FenceEntranceBuilder newEntranceBuilder)
    {
        ui = wiring;
        project = newProject;
        entranceBuilder = newEntranceBuilder;
    }

    private void Awake()
    {
        if (ui.panel == null) ui.panel = gameObject;
        if (ui.startButton != null) ui.startButton.onClick.AddListener(StartProject);
        if (ui.closeButton != null) ui.closeButton.onClick.AddListener(Close);
        if (ui.entranceButton != null) ui.entranceButton.onClick.AddListener(BuildEntrance);
    }

    private void OnEnable()
    {
        if (project != null) project.Changed += Refresh;
        if (entranceBuilder != null) entranceBuilder.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (project != null) project.Changed -= Refresh;
        if (entranceBuilder != null) entranceBuilder.Changed -= Refresh;
    }

    public void Open()
    {
        if (ui.panel == null) return;
        ui.panel.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (ui.panel != null) ui.panel.SetActive(false);
    }

    private void StartProject()
    {
        project?.StartNextProject();
        Refresh();
    }

    private void BuildEntrance()
    {
        entranceBuilder?.TryBuildAtPlayer();
        Refresh();
    }

    private void Update()
    {
        if (IsOpen) Refresh();
    }

    private void Refresh()
    {
        if (project == null) return;
        if (ui.currentText != null) ui.currentText.text = "CURRENT   " + project.CurrentLevelName;
        if (ui.nextText != null) ui.nextText.text = project.IsMaxLevel ? "ALL LEVELS BUILT" : "NEXT   " + project.NextLevelName;
        if (ui.costText != null)
            ui.costText.text = project.IsProjectActive
                ? "WOOD   " + project.DeliveredWood + " / " + project.RequiredWood
                : project.IsMaxLevel ? string.Empty : "WOOD   0 / " + project.RequiredWood;
        if (ui.startLabel != null)
            ui.startLabel.text = project.IsProjectActive ? "PROJECT ACTIVE" : project.IsMaxLevel ? "MAX LEVEL" : "START PROJECT";
        if (ui.startButton != null) ui.startButton.interactable = !project.IsProjectActive && !project.IsMaxLevel;

        if (entranceBuilder == null) return;
        if (ui.entranceStatusText != null)
            ui.entranceStatusText.text = "ENTRANCES   " + entranceBuilder.BuiltEntrances + " / " + entranceBuilder.EntranceLimit;
        if (ui.entranceCostText != null)
            ui.entranceCostText.text = entranceBuilder.IsAtLimit
                ? "LEVEL LIMIT REACHED"
                : entranceBuilder.CanBuildAtPlayer
                    ? "CASH   $" + entranceBuilder.NextCost
                    : "APPROACH THE FENCE TO BUILD";
        if (ui.entranceLabel != null)
            ui.entranceLabel.text = entranceBuilder.IsAtLimit ? "MAX ENTRANCES" : "BUILD ENTRANCE";
        if (ui.entranceButton != null)
            ui.entranceButton.interactable = entranceBuilder.CanBuildAtPlayer
                && entranceBuilder.CanAfford;
    }
}
