using UnityEngine;
using UnityEngine.EventSystems;

namespace FWF.Input
{
    /// <summary>Right-side drag area for camera look / aim.</summary>
    public class LookPad : MonoBehaviour, IDragHandler
    {
        public void OnDrag(PointerEventData e) => MobileInputHub.Instance.LookDelta += e.delta;
    }
}
