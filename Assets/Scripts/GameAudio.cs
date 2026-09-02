using UnityEngine;

/// One place for the short gameplay sounds, throttled so a burst of events does not
/// turn into a wall of noise. Lives on the player, next to its AudioSource.
[RequireComponent(typeof(AudioSource))]
public class GameAudio : MonoBehaviour
{
    private static GameAudio instance;

    [SerializeField] private AudioClip chopClip;
    [SerializeField] private AudioClip collectClip;
    [SerializeField] private AudioClip buildClip;
    [SerializeField, Min(0f)] private float minimumInterval = 0.06f;

    private AudioSource audioSource;
    private float nextChopTime;
    private float nextCollectTime;

    public void Configure(AudioClip chop, AudioClip collect, AudioClip build)
    {
        chopClip = chop;
        collectClip = collect;
        buildClip = build;
    }

    private void Awake()
    {
        instance = this;
        audioSource = GetComponent<AudioSource>();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public static void PlayChop(float pitch = 1f)
    {
        if (instance == null || Time.time < instance.nextChopTime) return;
        instance.nextChopTime = Time.time + instance.minimumInterval;
        instance.Play(instance.chopClip, 0.55f, pitch);
    }

    public static void PlayCollect(float pitch = 1f)
    {
        if (instance == null || Time.time < instance.nextCollectTime) return;
        instance.nextCollectTime = Time.time + instance.minimumInterval;
        instance.Play(instance.collectClip, 0.6f, pitch);
    }

    public static void PlayBuild()
    {
        if (instance == null) return;
        instance.Play(instance.buildClip, 0.8f, 1f);
    }

    private void Play(AudioClip clip, float volume, float pitch)
    {
        if (clip == null || audioSource == null) return;
        audioSource.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        audioSource.PlayOneShot(clip, volume);
    }
}
