using UnityEngine;

/// Two-panel gate at the entrance of the enclosure. It swings open when the player
/// comes close and closes again once they are through, in either direction.
/// The panels carry colliders, so a closed gate really blocks the way.
public class EnclosureGate : MonoBehaviour
{
    [SerializeField] private Transform leftHinge;
    [SerializeField] private Transform rightHinge;
    [SerializeField] private Transform player;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [SerializeField, Min(0.5f)] private float openRadius = 4.2f;
    [SerializeField, Min(0.1f)] private float closeMargin = 1.4f;
    [SerializeField, Range(20f, 140f)] private float openAngle = 96f;
    [SerializeField, Min(30f)] private float angularSpeed = 260f;

    private float openness;
    private bool isOpening;

    public bool IsOpen => openness > 0.98f;

    public void Configure(Transform newLeftHinge, Transform newRightHinge, Transform newPlayer, float newOpenRadius)
    {
        leftHinge = newLeftHinge;
        rightHinge = newRightHinge;
        player = newPlayer;
        openRadius = Mathf.Max(0.5f, newOpenRadius);
        openness = 0f;
        ApplyRotation();
    }

    private void Start()
    {
        if (player == null)
        {
            PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
            if (movement != null) player = movement.transform;
        }
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        ApplyRotation();
    }

    private void Update()
    {
        if (player == null) return;

        // Hysteresis: the closing radius is wider than the opening radius so the
        // gate never flickers while the player brushes past the entrance.
        Vector3 offset = player.position - transform.position;
        offset.y = 0f;
        float distanceSquared = offset.sqrMagnitude;
        float threshold = isOpening ? openRadius + closeMargin : openRadius;
        bool shouldOpen = distanceSquared <= threshold * threshold;

        if (shouldOpen != isOpening)
        {
            isOpening = shouldOpen;
            PlaySound(shouldOpen ? openSound : closeSound);
        }

        float target = isOpening ? 1f : 0f;
        if (Mathf.Approximately(openness, target)) return;

        openness = Mathf.MoveTowards(openness, target, angularSpeed / Mathf.Max(1f, openAngle) * Time.deltaTime);
        ApplyRotation();
    }

    private void ApplyRotation()
    {
        // Overshoot slightly at the end of the swing so the panels bounce into place.
        float angle = openAngle * Juice.EaseOutBack(openness, 1.1f);
        if (leftHinge != null) leftHinge.localRotation = Quaternion.Euler(0f, -angle, 0f);
        if (rightHinge != null) rightHinge.localRotation = Quaternion.Euler(0f, angle, 0f);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, 0.55f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.98f, 0.78f, 0.22f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, openRadius);
    }
}
