using UnityEngine;
using UnityEngine.UI;

/// Bouncy scale punch, on demand. Drop it on anything that should react when it is
/// hit, gains something, or gets tapped: it snaps up, undershoots, then settles.
/// Works on 3D transforms and on RectTransforms alike.
[DisallowMultipleComponent]
public class ScalePop : MonoBehaviour
{
    [SerializeField, Min(0.05f)] private float duration = 0.26f;
    [SerializeField, Min(0f)] private float strength = 0.22f;
    [SerializeField] private bool squash;
    [SerializeField] private bool popOnClick = true;

    private Vector3 restingScale = Vector3.one;
    private float elapsed = -1f;
    private float activeStrength;

    public void Configure(float newDuration, float newStrength, bool newSquash)
    {
        duration = Mathf.Max(0.05f, newDuration);
        strength = Mathf.Max(0f, newStrength);
        squash = newSquash;
    }

    private void Awake()
    {
        restingScale = transform.localScale;

        // Convenience: a button pops itself when tapped, no extra wiring needed.
        if (!popOnClick) return;
        Button button = GetComponent<Button>();
        if (button == null) return;
        button.onClick.RemoveListener(PopDefault);
        button.onClick.AddListener(PopDefault);
    }

    /// Re-reads the resting scale. Call it after something else changes the scale
    /// for good, so the pop settles back to the new size.
    public void Capture()
    {
        if (elapsed >= 0f) transform.localScale = restingScale;
        restingScale = transform.localScale;
        elapsed = -1f;
    }

    public void PopDefault()
    {
        Pop(strength);
    }

    public void Pop(float customStrength)
    {
        activeStrength = Mathf.Max(0f, customStrength);
        if (activeStrength <= 0f) return;
        elapsed = 0f;
    }

    private void Update()
    {
        if (elapsed < 0f) return;

        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / duration);

        // Starts at full strength and rides EaseOutBack home, so it overshoots on
        // the way in and dips slightly under before settling.
        float amount = activeStrength * (1f - Juice.EaseOutBack(progress));
        transform.localScale = squash
            ? Juice.Squash(restingScale, -amount)
            : restingScale * (1f + amount);

        if (progress < 1f) return;
        transform.localScale = restingScale;
        elapsed = -1f;
    }
}
