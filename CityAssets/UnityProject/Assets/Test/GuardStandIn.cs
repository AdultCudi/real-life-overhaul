using System;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The two calls the mod's clip player makes of its guard (Core/Guard.cs, which is bound to the game: its log, its
    /// notices) for this test project, which has nothing of the game's: <see cref="For"/> and <see cref="Fail"/>, over the
    /// mod's own fault arithmetic (Core/FeatureGuard.cs, copied in by build_bundle.ps1 with Core/HotSpots.cs). A fault
    /// here is a test that fails: it is logged as an error, and the run says so.
    /// </summary>
    internal static class Guard
    {
        public static FeatureGuard For(string feature, string where, Type owner = null)
        {
            return FeatureGuards.For(feature, where);
        }

        public static void Fail(FeatureGuard guard, Exception e, Behaviour component = null)
        {
            Debug.LogError("[AnimBundle] FAILED: " + (guard != null ? guard.Where : "an entry point") + " threw: " + e);
            if (guard != null && guard.Count(Time.realtimeSinceStartup) && component != null)
            {
                component.enabled = false;
            }
        }
    }
}
