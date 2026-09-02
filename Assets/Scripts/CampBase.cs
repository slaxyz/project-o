using System;
using System.Collections;
using UnityEngine;
/// The camp base. Level two is bought with dollars: it swaps the small fence ring
/// for the large one, grows the safe area, and reveals the build plot.
[RequireComponent(typeof(AudioSource))]
public class CampBase : MonoBehaviour
{
    public event Action<int> Upgraded;
    [SerializeField] private ResourceInventory wallet;
    [SerializeField] private GameplayHUD hud;
    [SerializeField] private ActionPrompt prompt;
    [SerializeField] private CampArea campArea;
    [SerializeField] private GameObject smallRing;
    [SerializeField] private GameObject largeRing;
    [SerializeField] private BuildPlot buildPlot;
    [SerializeField] private ParticleSystem buildParticles;
    [SerializeField] private AudioClip buildSound;
    [SerializeField] private int upgradeCost = 250;
    [SerializeField] private int maximumLevel = 2;
    [SerializeField] private float interactionRadius = 2.8f;
    [SerializeField] private float smallCampRadius = 8.4f;
    [SerializeField] private float largeCampRadius = 12f;
    private int currentLevel = 1;
    private bool playerIsNearby;
    private bool isAnimating;
    private AudioSource audioSource;
    private ScalePop pop;
    private Vector3 levelOneScale;
    private Transform player;
    public int CurrentLevel => currentLevel;
    public int UpgradeCost => upgradeCost;
    public void Configure(ResourceInventory newWallet, GameplayHUD newHud, ActionPrompt newPrompt,
        CampArea newCampArea, GameObject newSmallRing, GameObject newLargeRing, BuildPlot newBuildPlot,
        ParticleSystem newParticles, AudioClip newBuildSound, int newUpgradeCost)
    {
        wallet = newWallet;
        hud = newHud;
        prompt = newPrompt;
        campArea = newCampArea;
        smallRing = newSmallRing;
        largeRing = newLargeRing;
        buildPlot = newBuildPlot;
        buildParticles = newParticles;
        buildSound = newBuildSound;
        upgradeCost = Mathf.Max(1, newUpgradeCost);
    }
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        levelOneScale = transform.localScale;
        pop = GetComponent<ScalePop>();
        if (pop == null) pop = gameObject.AddComponent<ScalePop>();
        ApplyLevel(1);
    }
    private void Start()
    {
        if (wallet == null) wallet = FindFirstObjectByType<ResourceInventory>();
        if (hud == null) hud = FindFirstObjectByType<GameplayHUD>();
        if (wallet != null) player = wallet.transform;
        if (prompt != null) prompt.Clicked += TryUpgrade;
    }
    private void OnDestroy()
    {
        if (prompt != null) prompt.Clicked -= TryUpgrade;
    }
    private void Update()
    {
        if (player == null || prompt == null) return;
        playerIsNearby = ProximityZone.Contains(transform.position, player.position, interactionRadius);
        if (!playerIsNearby)
        {
            prompt.Hide();
            return;
        }
        if (currentLevel >= maximumLevel)
        {
            prompt.Show("BASE", "PARK ENLARGED, PLOT OPEN", "MAX LEVEL", false);
            return;
        }
        bool affordable = !isAnimating && wallet != null && wallet.Money >= upgradeCost;
        prompt.Show("BASE LEVEL " + (currentLevel + 1),
            "BIGGER PARK, ONE BUILD PLOT", "$" + upgradeCost, affordable);
    }
    public void TryUpgrade()
    {
        if (!playerIsNearby || isAnimating || currentLevel >= maximumLevel || wallet == null) return;
        if (!wallet.TrySpendMoney(upgradeCost))
        {
            if (hud != null) hud.ShowNotEnoughMoney(upgradeCost);
            return;
        }
        currentLevel++;
        ApplyLevel(currentLevel);
        Upgraded?.Invoke(currentLevel);
        if (hud != null) hud.ShowUpgradeSuccess();
        if (buildParticles != null) buildParticles.Play();
        if (buildSound != null) audioSource.PlayOneShot(buildSound, 0.8f);
        else GameAudio.PlayBuild();
        StartCoroutine(GrowRoutine());
    }
    private void ApplyLevel(int level)
    {
        bool large = level >= 2;
        if (smallRing != null) smallRing.SetActive(!large);
        if (largeRing != null) largeRing.SetActive(large);
        if (campArea != null) campArea.Configure(large ? largeCampRadius : smallCampRadius);
        if (buildPlot != null) buildPlot.SetUnlocked(large);
    }
    /// The whole building springs up to its new size.
    private IEnumerator GrowRoutine()
    {
        isAnimating = true;
        Vector3 targetScale = levelOneScale * 1.24f;
        float elapsed = 0f;
        const float duration = 0.48f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.LerpUnclamped(levelOneScale, targetScale, Juice.EaseOutBack(progress));
            yield return null;
        }
        transform.localScale = targetScale;
        pop.Capture();
        pop.Pop(0.16f);
        isAnimating = false;
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.98f, 0.48f, 0.28f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
