using System;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// On the game's object a kit draws over: tells its host the moment the game opens or closes the screen, and lets the
    /// host watch for a box or a hand-back while it shows. Cost a frame: one box test and a comparison while the screen
    /// shows; nothing while it is closed.
    /// </summary>
    public sealed class UiGameWatch : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "UiGameWatch", typeof(UiGameWatch));
        private static readonly int HotUpdate = HotSpots.Slot("Ui: UiGameWatch.Update");

        /// <summary>The host it tells; set as it is added.</summary>
        internal UiGameHost Host;

        private void OnEnable()
        {
            if (!FrameGuard.Off && Host != null)
            {
                Host.Opened();
            }
        }

        private void OnDisable()
        {
            if (!FrameGuard.Off && Host != null)
            {
                Host.Closed();
            }
        }

        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                Host?.Watch();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
                Host?.Fallback(e);
            }
        }
    }
}
