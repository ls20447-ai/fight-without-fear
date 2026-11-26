using UnityEngine;
using UnityEngine.EventSystems;

namespace FWF.Input
{
    /// <summary>Touch button that forwards press/release to MobileInputHub.</summary>
    public class PressButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public enum Kind { Fire, Aim, Jump, Crouch }
        public Kind kind;

        public void OnPointerDown(PointerEventData e)
        {
            var hub = MobileInputHub.Instance;
            switch (kind)
            {
                case Kind.Fire:   hub.SetFire(true); break;
                case Kind.Aim:    hub.SetAim(true); break;
                case Kind.Jump:   hub.OnJumpButton(); break;
                case Kind.Crouch: hub.OnCrouchButton(); break;
            }
        }

        public void OnPointerUp(PointerEventData e)
        {
            var hub = MobileInputHub.Instance;
            if (kind == Kind.Fire) hub.SetFire(false);
            else if (kind == Kind.Aim) hub.SetAim(false);
        }
    }
}
