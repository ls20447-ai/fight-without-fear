using UnityEngine;
using UnityEngine.EventSystems;

namespace FWF.Input
{
    /// <summary>Floating-style virtual joystick. Put on a transparent Image on the left half of screen.</summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] RectTransform background;
        [SerializeField] RectTransform handle;
        [SerializeField] float radius = 90f;
        [SerializeField] float deadZone = 0.1f;

        RectTransform _area;

        void Awake() => _area = (RectTransform)transform;

        public void OnPointerDown(PointerEventData e)
        {
            // Floating: joystick appears where the thumb lands
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, e.position, e.pressEventCamera, out var local);
            background.anchoredPosition = local;
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(background, e.position, e.pressEventCamera, out var local);
            Vector2 v = Vector2.ClampMagnitude(local / radius, 1f);
            handle.anchoredPosition = v * radius;
            MobileInputHub.Instance.Move = v.magnitude < deadZone ? Vector2.zero : v;
        }

        public void OnPointerUp(PointerEventData e)
        {
            handle.anchoredPosition = Vector2.zero;
            MobileInputHub.Instance.Move = Vector2.zero;
        }
    }
}
