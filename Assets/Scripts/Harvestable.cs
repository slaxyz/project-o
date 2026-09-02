using System;
using UnityEngine;

/// Anything the axe can hit: trees, wolves, and whatever comes next.
/// Holds hit points, plays the hit reaction and pays out the reward on death.
[DisallowMultipleComponent]
public class Harvestable : MonoBehaviour
{
    [SerializeField] private float maxHealth = 3f;
    [SerializeField] private ResourceType resourceType = ResourceType.Wood;
    [SerializeField] private int resourceAmount = 2;
    [SerializeField] private Transform shakeRoot;
    [SerializeField] private float punchDuration = 0.26f;
    [SerializeField] private float punchStrength = 0.2f;
    [SerializeField] private float leanStrength = 9f;
    [SerializeField] private bool destroyOnDeath;

    /// Raised once, when health reaches zero. The owner decides what happens next.
    public event Action<Harvestable> Died;
    public event Action<Harvestable> Damaged;

    private float health;
    private Vector3 restingScale;
    private Vector3 restingLocalPosition;
    private Quaternion restingLocalRotation;
    private float punchElapsed = -1f;
    private Vector3 punchDirection;
    private bool dead;

    public bool IsAlive => !dead && health > 0f;
    public float Health => health;
    public float MaxHealth => maxHealth;
    public float HealthRatio => maxHealth <= 0f ? 0f : Mathf.Clamp01(health / maxHealth);
    public ResourceType Resource => resourceType;

    private void Awake()
    {
        CaptureRest();
        if (health <= 0f) health = maxHealth;
    }

    /// Restores a pooled or re-streamed instance to a known state.
    public void Initialize(float newMaxHealth, float currentHealth, ResourceType type, int amount)
    {
        maxHealth = Mathf.Max(1f, newMaxHealth);
        health = Mathf.Clamp(currentHealth <= 0f ? maxHealth : currentHealth, 0f, maxHealth);
        resourceType = type;
        resourceAmount = Mathf.Max(0, amount);
        dead = false;
        punchElapsed = -1f;
        CaptureRest();
    }

    /// shakeRoot stays as authored: some prefabs animate their own visual root and
    /// need a separate node for the hit reaction.
    private void CaptureRest()
    {
        if (shakeRoot == null) shakeRoot = transform;
        restingScale = shakeRoot.localScale;
        restingLocalPosition = shakeRoot.localPosition;
        restingLocalRotation = shakeRoot.localRotation;
    }

    /// Applies damage from a hit coming from origin. Returns true when it landed.
    public bool TakeDamage(float amount, Vector3 origin, ResourceInventory collector, float rewardMultiplier = 1f)
    {
        if (!IsAlive || amount <= 0f) return false;

        health = Mathf.Max(0f, health - amount);
        punchDirection = transform.position - origin;
        punchDirection.y = 0f;
        punchDirection = punchDirection.sqrMagnitude > 0.0001f ? punchDirection.normalized : Vector3.forward;
        punchElapsed = 0f;
        Damaged?.Invoke(this);

        // A hit on a nearly dead target rings harder, so the last blow reads.
        GameAudio.PlayChop(Mathf.Lerp(1.25f, 0.85f, HealthRatio));

        if (health > 0f) return true;

        // The death animation takes over from here, so stop reacting.
        dead = true;
        punchElapsed = -1f;
        ResetRest();

        if (collector != null && resourceAmount > 0)
        {
            int payout = Mathf.Max(1, Mathf.RoundToInt(resourceAmount * Mathf.Max(0.1f, rewardMultiplier)));
            ResourceFlyer.SpawnReward(transform.position, collector, resourceType, payout);
        }
        Died?.Invoke(this);
        if (destroyOnDeath) Destroy(gameObject);
        return true;
    }

    private void Update()
    {
        if (punchElapsed < 0f || shakeRoot == null) return;

        punchElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(punchElapsed / punchDuration);

        // Squashes down and spreads out on impact, then springs back through the
        // resting size with a small overshoot, while leaning away from the blow.
        float settle = 1f - Juice.EaseOutBack(progress);
        float wobble = Juice.DampedWobble(progress, 2.5f);

        shakeRoot.localScale = Juice.Squash(restingScale, -punchStrength * settle);
        shakeRoot.localPosition = restingLocalPosition + punchDirection * (wobble * punchStrength * 0.6f);

        Vector3 leanAxis = Vector3.Cross(Vector3.up, punchDirection);
        shakeRoot.localRotation = restingLocalRotation *
            Quaternion.AngleAxis(wobble * leanStrength, leanAxis);

        if (progress < 1f) return;
        ResetRest();
        punchElapsed = -1f;
    }

    private void ResetRest()
    {
        if (shakeRoot == null) return;
        shakeRoot.localScale = restingScale;
        shakeRoot.localPosition = restingLocalPosition;
        shakeRoot.localRotation = restingLocalRotation;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.98f, 0.42f, 0.24f, 0.55f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.9f);
    }
}
