using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Basic wolf. Prowls around the clearing it spawned in, charges the player on sight,
/// alerts the rest of its pack, and lunges when close enough. Dies through its
/// Harvestable, which is what pays out the meat.
[RequireComponent(typeof(Harvestable))]
public class WolfAgent : MonoBehaviour
{
    private enum WolfState { Prowl, Chase, Strike, Dying }

    private static readonly Collider[] Buffer = new Collider[8];

    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform head;
    [SerializeField] private Transform tail;
    [SerializeField] private Transform[] legs;
    [SerializeField] private float prowlSpeed = 1.6f;
    [SerializeField] private float chaseSpeed = 4.2f;
    [SerializeField] private float turnSpeed = 7f;
    [SerializeField] private float detectRadius = 13f;
    [SerializeField] private float loseRadius = 24f;
    [SerializeField] private float strikeRadius = 1.6f;
    [SerializeField] private float strikeCooldown = 2f;
    [SerializeField] private float knockbackDistance = 1.9f;
    [SerializeField] private float bodyRadius = 0.45f;

    private Harvestable harvestable;
    private Transform player;
    private PlayerMovement playerMovement;
    private WolfState state;
    private Vector3 homeCenter;
    private float homeRadius = 9f;
    private Vector3 prowlTarget;
    private float stateTimer;
    private float nextStrikeTime;
    private int obstacleMask;
    private List<WolfAgent> pack;

    public event Action<WolfAgent> Died;

    public bool IsAlive => harvestable != null && harvestable.IsAlive;
    public Vector3 HomeCenter => homeCenter;

    public void Configure(Vector3 newHomeCenter, float newHomeRadius, List<WolfAgent> newPack,
        Transform newPlayer, float health, int meatReward)
    {
        homeCenter = newHomeCenter;
        homeRadius = Mathf.Max(3f, newHomeRadius);
        pack = newPack;
        player = newPlayer;
        playerMovement = newPlayer != null ? newPlayer.GetComponent<PlayerMovement>() : null;

        if (harvestable == null) harvestable = GetComponent<Harvestable>();
        harvestable.Initialize(health, health, ResourceType.Meat, meatReward);
        PickProwlTarget();
    }

    private void Awake()
    {
        harvestable = GetComponent<Harvestable>();
        if (visualRoot == null) visualRoot = transform;
        obstacleMask = Physics.DefaultRaycastLayers;
    }

    private void OnEnable()
    {
        harvestable.Died += HandleDied;
    }

    private void OnDisable()
    {
        harvestable.Died -= HandleDied;
    }

    private void Start()
    {
        if (player != null) return;
        PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
        if (movement == null) return;
        player = movement.transform;
        playerMovement = movement;
    }

    private void Update()
    {
        if (state == WolfState.Dying) return;

        float distanceToPlayer = PlayerDistance();
        UpdateState(distanceToPlayer);

        switch (state)
        {
            case WolfState.Prowl:
                Prowl();
                break;
            case WolfState.Chase:
                Chase();
                break;
            case WolfState.Strike:
                Strike(distanceToPlayer);
                break;
        }

        Animate();
    }

    private void UpdateState(float distanceToPlayer)
    {
        bool playerReachable = player != null && !CampArea.IsInside(player.position, 1f);

        if (!playerReachable)
        {
            if (state != WolfState.Prowl) SetState(WolfState.Prowl);
            return;
        }

        switch (state)
        {
            case WolfState.Prowl:
                if (distanceToPlayer <= detectRadius)
                {
                    SetState(WolfState.Chase);
                    AlertPack();
                }
                break;
            case WolfState.Chase:
                if (distanceToPlayer > loseRadius) SetState(WolfState.Prowl);
                else if (distanceToPlayer <= strikeRadius && Time.time >= nextStrikeTime) SetState(WolfState.Strike);
                break;
            case WolfState.Strike:
                if (stateTimer <= 0f) SetState(WolfState.Chase);
                break;
        }
    }

    private void SetState(WolfState newState)
    {
        state = newState;
        switch (newState)
        {
            case WolfState.Prowl:
                PickProwlTarget();
                break;
            case WolfState.Strike:
                stateTimer = 0.45f;
                nextStrikeTime = Time.time + strikeCooldown;
                DeliverStrike();
                break;
        }
    }

    /// Wakes up every packmate that has not noticed the player yet.
    public void AlertPack()
    {
        if (pack == null) return;
        for (int index = 0; index < pack.Count; index++)
        {
            WolfAgent mate = pack[index];
            if (mate == null || mate == this || !mate.IsAlive) continue;
            if (mate.state == WolfState.Prowl) mate.state = WolfState.Chase;
        }
    }

    private void Prowl()
    {
        stateTimer -= Time.deltaTime;
        Vector3 toTarget = prowlTarget - transform.position;
        toTarget.y = 0f;
        if (stateTimer <= 0f || toTarget.sqrMagnitude < 0.6f)
        {
            PickProwlTarget();
            return;
        }
        Move(toTarget.normalized, prowlSpeed);
    }

    private void Chase()
    {
        if (player == null) return;
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        Move(toPlayer.normalized + Separation() * 0.6f, chaseSpeed);
    }

    private void Strike(float distanceToPlayer)
    {
        stateTimer -= Time.deltaTime;
        if (player == null) return;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (distanceToPlayer > strikeRadius * 1.4f) Move(toPlayer.normalized, chaseSpeed * 0.6f);
        else FaceDirection(toPlayer.normalized);
    }

    private void DeliverStrike()
    {
        if (playerMovement == null) return;
        Vector3 push = player.position - transform.position;
        push.y = 0f;
        if (push.sqrMagnitude < 0.0001f) return;
        playerMovement.ApplyKnockback(push.normalized, knockbackDistance, 0.22f);
    }

    private void PickProwlTarget()
    {
        float angle = UnityEngine.Random.value * Mathf.PI * 2f;
        float radius = UnityEngine.Random.Range(0.25f, 0.92f) * homeRadius;
        prowlTarget = homeCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        stateTimer = UnityEngine.Random.Range(2.4f, 5.2f);
    }

    private Vector3 Separation()
    {
        if (pack == null) return Vector3.zero;

        Vector3 separation = Vector3.zero;
        for (int index = 0; index < pack.Count; index++)
        {
            WolfAgent mate = pack[index];
            if (mate == null || mate == this || !mate.IsAlive) continue;

            Vector3 offset = transform.position - mate.transform.position;
            offset.y = 0f;
            float distanceSquared = offset.sqrMagnitude;
            if (distanceSquared > 2.6f || distanceSquared < 0.0001f) continue;
            separation += offset.normalized / Mathf.Sqrt(distanceSquared);
        }
        return separation;
    }

    private void Move(Vector3 direction, float speed)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        direction.Normalize();
        FaceDirection(direction);

        Vector3 next = transform.position + transform.forward * (speed * Time.deltaTime);
        next = CampArea.ClampOutside(next, 1.5f);
        if (IsBlocked(next)) next = SlideAround(direction, speed);
        transform.position = next;
    }

    /// Cheap obstacle handling: if the way ahead is taken, try to slip past on one side.
    private Vector3 SlideAround(Vector3 direction, float speed)
    {
        Vector3 side = Vector3.Cross(Vector3.up, direction);
        for (int sign = -1; sign <= 1; sign += 2)
        {
            Vector3 candidate = transform.position + (direction + side * sign).normalized * (speed * Time.deltaTime);
            candidate = CampArea.ClampOutside(candidate, 1.5f);
            if (!IsBlocked(candidate)) return candidate;
        }
        return transform.position;
    }

    private bool IsBlocked(Vector3 position)
    {
        int count = Physics.OverlapSphereNonAlloc(position + Vector3.up * 0.5f, bodyRadius, Buffer,
            obstacleMask, QueryTriggerInteraction.Ignore);
        for (int index = 0; index < count; index++)
        {
            Collider hit = Buffer[index];
            if (hit.transform.root == transform) continue;
            if (hit.GetComponentInParent<InfiniteGround>() != null) continue;
            if (hit.GetComponentInParent<WolfAgent>() != null) continue;
            // Wolves thread between trunks instead of getting stuck in the thicket.
            if (hit.GetComponentInParent<Harvestable>() != null) continue;
            return true;
        }
        return false;
    }

    private void FaceDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(direction, Vector3.up), turnSpeed * Time.deltaTime);
    }

    private float PlayerDistance()
    {
        if (player == null) return float.MaxValue;
        Vector3 offset = player.position - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    private void Animate()
    {
        bool running = state == WolfState.Chase;
        bool striking = state == WolfState.Strike;
        float speedFactor = running ? 13f : 6f;

        if (visualRoot != null)
        {
            float bob = Mathf.Abs(Mathf.Sin(Time.time * speedFactor)) * (running ? 0.07f : 0.025f);
            float lunge = striking ? Mathf.Sin(Mathf.Clamp01(1f - stateTimer / 0.45f) * Mathf.PI) * 0.35f : 0f;
            visualRoot.localPosition = new Vector3(0f, bob, lunge);
        }

        if (head != null)
        {
            float pitch = striking ? -18f : running ? -6f : Mathf.Sin(Time.time * 1.6f) * 4f;
            head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        if (tail != null) tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * speedFactor * 0.7f) * 22f, 0f);

        if (legs == null) return;
        float swing = Mathf.Sin(Time.time * speedFactor) * (running ? 34f : 14f);
        for (int index = 0; index < legs.Length; index++)
        {
            if (legs[index] == null) continue;
            float direction = index % 2 == 0 ? 1f : -1f;
            legs[index].localRotation = Quaternion.Euler(swing * direction, 0f, 0f);
        }
    }

    private void HandleDied(Harvestable source)
    {
        if (state == WolfState.Dying) return;
        state = WolfState.Dying;
        Died?.Invoke(this);
        StartCoroutine(DeathRoutine());
    }

    /// Yelps upward, flips over, then deflates into the grass.
    private IEnumerator DeathRoutine()
    {
        Collider bodyCollider = GetComponent<Collider>();
        if (bodyCollider != null) bodyCollider.enabled = false;

        Quaternion startRotation = transform.rotation;
        Quaternion endRotation = startRotation * Quaternion.Euler(0f, 0f, 96f);
        Vector3 startScale = transform.localScale;
        Vector3 startPosition = transform.position;
        float elapsed = 0f;
        const float duration = 0.55f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            transform.rotation = Quaternion.Slerp(startRotation, endRotation, Juice.EaseOutCubic(progress));
            transform.position = startPosition + Vector3.up * (Juice.Impulse(progress) * 0.45f);
            transform.localScale = Juice.Squash(startScale, 0.22f * Juice.Impulse(progress))
                * Mathf.Lerp(1f, 0.6f, Juice.EaseInCubic(progress));
            yield return null;
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.86f, 0.32f, 0.32f, 0.45f);
        Gizmos.DrawWireSphere(transform.position, detectRadius);
        Gizmos.color = new Color(0.4f, 0.6f, 0.9f, 0.35f);
        Gizmos.DrawWireSphere(homeCenter, homeRadius);
    }
}
