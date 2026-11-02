using System;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What delivery trips ask of the rest of the mod, filled in by Wiring/LogisticsLinks. Each is optional.</summary>
    public static class LogisticsBridges
    {
        /// <summary>Metres of road between two places, measured a slice a call: -1 while still looking, 0 with no way. Default: 0, the straight line's guess stays.</summary>
        public static Func<Vector3, Vector3, int> Road;

        public static void Reset()
        {
            Road = null;
        }
    }
}
