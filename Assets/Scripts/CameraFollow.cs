using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 13f, -11f);
    [SerializeField] private float smoothTime = 0.18f;

    private Vector3 velocity;

    public void Configure(Transform newTarget, Vector3 newOffset)
    {
        target = newTarget;
        offset = newOffset;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);
    }
}
