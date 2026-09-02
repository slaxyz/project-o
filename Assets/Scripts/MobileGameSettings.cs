using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.EnhancedTouch;
#endif

public class MobileGameSettings : MonoBehaviour
{
    [SerializeField] private int targetFrameRate = 60;

    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFrameRate;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
#if ENABLE_INPUT_SYSTEM
        EnhancedTouchSupport.Enable();
#endif
    }
}
