using UnityEngine;
using UnityEngine.EventSystems;

public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;

    public Vector2 Direction { get; private set; }

    public void Configure(RectTransform newBackground, RectTransform newHandle)
    {
        background = newBackground;
        handle = newHandle;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (background == null || handle == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint);

        Vector2 radius = background.rect.size * 0.5f;
        Vector2 normalized = new Vector2(localPoint.x / radius.x, localPoint.y / radius.y);
        Direction = Vector2.ClampMagnitude(normalized, 1f);
        handle.anchoredPosition = Direction * radius * 0.52f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Direction = Vector2.zero;
        if (handle != null) handle.anchoredPosition = Vector2.zero;
    }
}
