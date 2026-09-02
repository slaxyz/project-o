using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

public class PlayerMovement : MonoBehaviour
{
    private const int BufferSize = 12;

    [SerializeField] private VirtualJoystick joystick;
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float rotationSpeed = 14f;
    [SerializeField] private Vector2 minimumBounds = new Vector2(-240f, -240f);
    [SerializeField] private Vector2 maximumBounds = new Vector2(240f, 240f);
    [SerializeField] private float bobHeight = 0.08f;
    [SerializeField] private float bobSpeed = 10f;
    [SerializeField] private float bodyRadius = 0.34f;

    private static readonly Collider[] Buffer = new Collider[BufferSize];

    private readonly List<Transform> visualParts = new List<Transform>();
    private readonly List<Vector3> restingPositions = new List<Vector3>();
    private readonly List<Vector3> restingScales = new List<Vector3>();
    private Vector3 knockbackVelocity;
    private float knockbackTimer;

    public float MoveSpeed => moveSpeed;

    public void Configure(VirtualJoystick newJoystick, Vector2 minBounds, Vector2 maxBounds)
    {
        joystick = newJoystick;
        minimumBounds = minBounds;
        maximumBounds = maxBounds;
    }

    public void SetMovementBounds(Vector2 minBounds, Vector2 maxBounds)
    {
        minimumBounds = minBounds;
        maximumBounds = maxBounds;
    }

    public void IncreaseMoveSpeed(float amount)
    {
        moveSpeed += Mathf.Max(0f, amount);
    }

    /// Short push used by the wolves when a lunge connects.
    public void ApplyKnockback(Vector3 direction, float distance, float duration)
    {
        if (duration <= 0f) return;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        knockbackVelocity = direction.normalized * (distance / duration);
        knockbackTimer = duration;
    }

    private void Awake()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer part in renderers)
        {
            if (visualParts.Contains(part.transform)) continue;
            visualParts.Add(part.transform);
            restingPositions.Add(part.transform.localPosition);
            restingScales.Add(part.transform.localScale);
        }
    }

    private void Update()
    {
        Vector2 input = joystick != null ? joystick.Direction : Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (input.sqrMagnitude < 0.01f && Keyboard.current != null)
        {
            input.x = ReadAxis(Keyboard.current.aKey, Keyboard.current.dKey, Keyboard.current.leftArrowKey, Keyboard.current.rightArrowKey);
            input.y = ReadAxis(Keyboard.current.sKey, Keyboard.current.wKey, Keyboard.current.downArrowKey, Keyboard.current.upArrowKey);
        }
#endif

        input = Vector2.ClampMagnitude(input, 1f);
        Vector3 movement = new Vector3(input.x, 0f, input.y);
        bool isMoving = movement.sqrMagnitude > 0.001f;

        if (isMoving)
        {
            TryStep(movement * (moveSpeed * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(movement),
                rotationSpeed * Time.deltaTime);
        }

        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.deltaTime;
            TryStep(knockbackVelocity * Time.deltaTime);
            if (knockbackTimer <= 0f) knockbackVelocity = Vector3.zero;
        }

        Vector3 position = transform.position;
        position.x = Mathf.Clamp(position.x, minimumBounds.x, maximumBounds.x);
        position.z = Mathf.Clamp(position.z, minimumBounds.y, maximumBounds.y);
        transform.position = position;

        AnimateMovement(isMoving);
    }

    /// Moves by delta unless something solid is in the way. Slides along the free axis
    /// so brushing a tree or a closed gate does not stop the player dead.
    private void TryStep(Vector3 delta)
    {
        if (delta.sqrMagnitude < 0.0000001f) return;

        Vector3 position = transform.position;
        if (!IsBlocked(position + delta))
        {
            transform.position = position + delta;
            return;
        }

        Vector3 alongX = new Vector3(delta.x, 0f, 0f);
        if (alongX.sqrMagnitude > 0.0000001f && !IsBlocked(position + alongX))
        {
            transform.position = position + alongX;
            return;
        }

        Vector3 alongZ = new Vector3(0f, 0f, delta.z);
        if (alongZ.sqrMagnitude > 0.0000001f && !IsBlocked(position + alongZ)) transform.position = position + alongZ;
    }

    private bool IsBlocked(Vector3 position)
    {
        int count = Physics.OverlapSphereNonAlloc(position + Vector3.up * 0.45f, bodyRadius, Buffer,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int index = 0; index < count; index++)
        {
            Collider hit = Buffer[index];
            if (hit.transform.root == transform) continue;
            if (hit.GetComponentInParent<InfiniteGround>() != null) continue;
            return true;
        }
        return false;
    }

#if ENABLE_INPUT_SYSTEM
    private static float ReadAxis(ButtonControl negativeA, ButtonControl positiveA, ButtonControl negativeB, ButtonControl positiveB)
    {
        float negative = negativeA.isPressed || negativeB.isPressed ? 1f : 0f;
        float positive = positiveA.isPressed || positiveB.isPressed ? 1f : 0f;
        return positive - negative;
    }
#endif

    /// Bob plus squash and stretch: stretched at the top of the step, squashed as
    /// the foot lands.
    private void AnimateMovement(bool isMoving)
    {
        float cycle = Mathf.Sin(Time.time * bobSpeed);
        float verticalOffset = isMoving ? Mathf.Abs(cycle) * bobHeight : 0f;
        float stretch = isMoving ? cycle * 0.06f : 0f;

        for (int index = 0; index < visualParts.Count; index++)
        {
            visualParts[index].localPosition = restingPositions[index] + Vector3.up * verticalOffset;
            visualParts[index].localScale = Juice.Squash(restingScales[index], stretch);
        }
    }
}
