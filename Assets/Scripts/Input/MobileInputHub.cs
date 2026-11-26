using UnityEngine;
using UnityEngine.EventSystems;

namespace FWF.Input
{
    /// <summary>
    /// Single source of truth for touch input. UI widgets write into it,
    /// gameplay code reads from it. No per-frame allocations.
    /// Attach to the HUD canvas root.
    /// </summary>
    public class MobileInputHub : MonoBehaviour
    {
        public static MobileInputHub Instance { get; private set; }

        public Vector2 Move;          // -1..1, from left joystick
        public Vector2 LookDelta;     // pixels this frame, from right look pad
        public bool FireHeld;         // fire button held
        public bool AimHeld;          // ADS button held

        // One-shot flags: set by UI, consumed (cleared) by the controller.
        public bool JumpPressed;
        public bool CrouchTogglePressed;

        void Awake() => Instance = this;

        // Hook these to UI Buttons / EventTriggers
        public void OnJumpButton()   => JumpPressed = true;
        public void OnCrouchButton() => CrouchTogglePressed = true;
        public void SetFire(bool held) => FireHeld = held;
        public void SetAim(bool held)  => AimHeld = held;

        // Called by the controller at the end of its frame
        public void ConsumeFrame()
        {
            JumpPressed = false;
            CrouchTogglePressed = false;
            LookDelta = Vector2.zero;
        }
    }
}
