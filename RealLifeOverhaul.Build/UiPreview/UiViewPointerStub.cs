using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    // The pointer part of the view reads the Input System, which the preview's project does not carry: this stands in for it with what the host names.
    internal struct UiPressReport
    {
        public int RayNode, HereNode, DownNode, Pressed;
        public string Why;
    }

    public sealed partial class UiView
    {
        internal System.Action<UiPressReport> PointerReport;

        internal void PointerPhase(UnityEngine.EventSystems.PointerEventData data, UiDrag phase)
        {
        }

        internal void PointerUp(UnityEngine.EventSystems.PointerEventData data)
        {
        }
    }
}
