using System;
using UnityEngine;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>
    /// Keeps the lying snow round the player in every frame, in the frame's late step: after the player and their car have
    /// moved, and before HDRP starts its decal job, which reads the projector's place at the start of the frame's drawing
    /// (DecalSystem.StartDecalUpdateJobs, ahead of the pipeline's camera callbacks). On only while snow lies outdoors and
    /// its square has been shown; off it is disabled and costs nothing. Cost a frame while on: the camera's and the
    /// player's places read and a few float operations (<see cref="SnowCoverRules.Step"/>), nothing allocated; a move sets
    /// the projector's place and its pattern's offset, two writes HDRP takes in the same frame.
    /// </summary>
    public sealed class SnowCoverFollow : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("seasons", "SnowCoverFollow", typeof(SnowCoverFollow));
        private static readonly int HotLateUpdate = HotSpots.Slot("Seasons: SnowCoverFollow.LateUpdate");

        internal SnowLook Look;

        /// <summary>On while the snow's square lies round the player; off costs nothing.</summary>
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
            if (Look == null)
            {
                enabled = false;
                return;
            }
            Look.Keep(Time.deltaTime);
        }
    }
}
