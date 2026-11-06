using System;
using UnityEngine;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>
    /// Keeps the game's wet look off the streets while snow falls in the place of its rain. The game writes its puddles
    /// and its rain's wetness into two global shader values every frame, from its weather (RainShaderValues); this writes
    /// them dry after it, in the frame's late step, on this machine's picture only. The director switches it on only while
    /// snow falls over the game's own rain. Cost a frame while on: two global values written, nothing allocated; while
    /// off it is disabled and costs nothing, and the game's own values are back the next frame.
    /// </summary>
    public sealed class SnowHush : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("seasons", "SnowHush", typeof(SnowHush));
        private static readonly int HotLateUpdate = HotSpots.Slot("Seasons: SnowHush.LateUpdate");

        // The game's own global values (BigAmbitions.DayNightCycle.RainShaderValues): its puddles and its rain's wetness.
        private static readonly int PuddlesId = Shader.PropertyToID("_hov_Puddles");
        private static readonly int RainId = Shader.PropertyToID("_Rain");

        /// <summary>On while snow falls over the game's own rain; off gives the game its wet look back.</summary>
        public void Hold(bool on)
        {
            if (enabled != on)
            {
                enabled = on;
            }
        }

        private void Awake()
        {
            enabled = false;
        }

        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnLateUpdate()
        {
            Shader.SetGlobalFloat(PuddlesId, 0f);
            Shader.SetGlobalFloat(RainId, 0f);
        }
    }
}
