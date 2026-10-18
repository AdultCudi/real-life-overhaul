using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// The mouse wheel over Newsstand's page: each notch sets where the page is to be, a few lines on, and
    /// <see cref="NewsstandGlide"/> takes it there. Dragging is the game's ScrollRect's; a drag stops a glide. The
    /// cost: nothing per frame (no Update); it answers only the wheel and the drag, while the page is open.
    /// </summary>
    public sealed class NewsstandWheel : MonoBehaviour, IScrollHandler, IBeginDragHandler
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ledger", "NewsstandWheel", typeof(NewsstandWheel));

        internal NewsstandGlide Glide;
        internal float Step = 120f;     // canvas units a notch
        private float _unit = 120f;     // the wheel's delta for one notch, as learnt

        public void OnScroll(PointerEventData data)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                if (Glide != null && data != null)
                {
                    // A notch is 1 from one input module, 6 or 120 from others: the smallest step seen is taken for one
                    // notch (between 1 and 120), so the page moves the same few lines a notch whatever the module.
                    float d = data.scrollDelta.y;
                    if (d == 0f)
                    {
                        return;
                    }
                    _unit = Mathf.Clamp(Mathf.Min(_unit, Mathf.Abs(d)), 1f, 120f);
                    Glide.By(-Mathf.Clamp(d / _unit, -5f, 5f) * Step);
                }
            }
            catch (System.Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        public void OnBeginDrag(PointerEventData data)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                if (Glide != null)
                {
                    Glide.Stop();
                }
            }
            catch (System.Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }
    }
}
