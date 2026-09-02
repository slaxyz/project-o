using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Pooled item that arcs from one place to another and pays out on arrival.
/// Used both ways: loot flying from a dead resource to the player, and carried
/// resources trickling from the player into the camp store.
public class ResourceFlyer : MonoBehaviour
{
    private const int MaxVisualsPerReward = 4;

    private static readonly Color BillTint = new Color(0.34f, 0.72f, 0.36f);
    private static readonly Stack<ResourceFlyer> Pool = new Stack<ResourceFlyer>();
    private static Transform poolRoot;

    private Renderer visual;
    private Transform followTarget;
    private Vector3 fixedTarget;
    private Vector3 startPosition;
    private Vector3 startScale;
    private Vector3 spin;
    private float duration;
    private float arcHeight;
    private Action onArrived;

    /// Loot: a few items burst out and home in on the player. The first one carries
    /// the whole reward so the HUD reports one clean total.
    public static void SpawnReward(Vector3 origin, ResourceInventory target, ResourceType type, int amount)
    {
        if (target == null) return;

        int visuals = Mathf.Clamp(amount, 1, MaxVisualsPerReward);
        for (int index = 0; index < visuals; index++)
        {
            int payloadAmount = index == 0 ? amount : 0;
            Vector3 spread = SpreadOffset(index, visuals);

            ResourceFlyer flyer = Rent(ResourceTypes.Tint(type));
            flyer.followTarget = target.transform;
            flyer.duration = 0.36f;
            flyer.arcHeight = 0.85f;
            flyer.onArrived = () =>
            {
                if (payloadAmount <= 0) return;
                target.TryAdd(type, payloadAmount);
                GameAudio.PlayCollect(1.1f);
            };
            flyer.Launch(origin + Vector3.up * 0.8f + spread, 0.34f);
        }
    }

    /// Sale: one carried item leaves the stack and lands on the counter.
    public static void SpawnDeposit(Vector3 origin, Vector3 destination, ResourceType type, Action onArrived)
    {
        ResourceFlyer flyer = Rent(ResourceTypes.Tint(type));
        flyer.followTarget = null;
        flyer.fixedTarget = destination;
        flyer.duration = 0.42f;
        flyer.arcHeight = 1.5f;
        flyer.onArrived = onArrived;
        flyer.Launch(origin, 0.28f);
    }

    /// Payout: a pack of bills leaves the counter and lands in the wallet.
    public static void SpawnBills(Vector3 origin, ResourceInventory wallet, int amount)
    {
        if (wallet == null || amount <= 0) return;

        ResourceFlyer flyer = Rent(BillTint);
        flyer.followTarget = wallet.transform;
        flyer.duration = 0.46f;
        flyer.arcHeight = 1.8f;
        flyer.onArrived = () =>
        {
            wallet.AddMoney(amount);
            GameAudio.PlayCollect(1.35f);
        };
        flyer.Launch(origin + Vector3.up * 0.35f, 0.42f);
    }

    private static Vector3 SpreadOffset(int index, int total)
    {
        if (total <= 1) return Vector3.zero;
        float angle = index / (float)total * Mathf.PI * 2f;
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.3f;
    }

    private void Launch(Vector3 origin, float size)
    {
        startPosition = origin;
        startScale = Vector3.one * size;
        spin = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f),
            UnityEngine.Random.Range(-1f, 1f)).normalized * UnityEngine.Random.Range(240f, 480f);

        transform.position = origin;
        transform.localScale = Vector3.zero;
        transform.rotation = UnityEngine.Random.rotation;
        gameObject.SetActive(true);
        StartCoroutine(FlyRoutine());
    }

    private static ResourceFlyer Rent(Color tint)
    {
        ResourceFlyer flyer = null;
        while (Pool.Count > 0)
        {
            ResourceFlyer pooled = Pool.Pop();
            if (pooled != null)
            {
                flyer = pooled;
                break;
            }
        }

        if (flyer == null)
        {
            if (poolRoot == null) poolRoot = new GameObject("ResourceFlyers").transform;
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = "ResourceFlyer";
            item.transform.SetParent(poolRoot, false);
            Collider itemCollider = item.GetComponent<Collider>();
            if (itemCollider != null) Destroy(itemCollider);

            flyer = item.AddComponent<ResourceFlyer>();
            flyer.visual = item.GetComponent<Renderer>();
            item.SetActive(false);
        }

        flyer.visual.sharedMaterial = RuntimeMaterials.Solid(tint);
        return flyer;
    }

    private IEnumerator FlyRoutine()
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            Vector3 destination = followTarget != null
                ? followTarget.position + Vector3.up * 1.05f
                : fixedTarget;
            transform.position = Juice.Arc(startPosition, destination, progress, arcHeight);
            transform.Rotate(spin * Time.deltaTime, Space.Self);

            // Pops out to full size, then shrinks away as it is absorbed.
            float scale = progress < 0.25f
                ? Juice.EaseOutBack(progress / 0.25f)
                : 1f - Juice.EaseInCubic((progress - 0.25f) / 0.75f) * 0.9f;
            transform.localScale = startScale * scale;
            yield return null;
        }

        Action callback = onArrived;
        onArrived = null;
        followTarget = null;
        gameObject.SetActive(false);
        Pool.Push(this);
        callback?.Invoke();
    }
}
