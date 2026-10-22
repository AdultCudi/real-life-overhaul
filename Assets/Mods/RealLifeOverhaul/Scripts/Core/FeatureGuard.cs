using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The fault count of one entry point of a feature: <see cref="Most"/> faults within <see cref="Seconds"/> switch it
    /// off for the session, so a feature broken by a game update says so once instead of throwing every frame.
    /// Core/Guard does the saying and switching; this is the arithmetic, game-free (ContractCheck tests it).
    /// </summary>
    public sealed class FeatureGuard
    {
        public const int Most = 5;           // faults within the window that switch an entry point off
        public const float Seconds = 30f;    // the window, seconds of real time

        /// <summary>Its Meta/FeatureList id; null for the mod's own machinery.</summary>
        public readonly string Feature;

        /// <summary>What it is, in English, for Player.log: "LifeEvents.Update".</summary>
        public readonly string Where;

        private float _windowStart;
        private int _inWindow;

        public FeatureGuard(string feature, string where)
        {
            Feature = feature;
            Where = where;
        }

        /// <summary>True once switched off; for the rest of the session.</summary>
        public bool Off { get; private set; }

        /// <summary>Faults this session, counted after the switch-off too.</summary>
        public int Faults { get; private set; }

        /// <summary>Counts a fault at <paramref name="now"/> (seconds of real time); true only for the one that switches it off.</summary>
        public bool Count(float now)
        {
            Faults++;
            if (Off)
            {
                return false;
            }
            if (_inWindow == 0 || now - _windowStart > Seconds || now < _windowStart)
            {
                _windowStart = now;
                _inWindow = 0;
            }
            _inWindow++;
            if (_inWindow >= Most)
            {
                Off = true;
                return true;
            }
            return false;
        }

        /// <summary>Switched off from outside: another entry point of its feature switched the feature off.</summary>
        public void SwitchOff()
        {
            Off = true;
        }
    }

    /// <summary>
    /// The register of every <see cref="FeatureGuard"/> and of the features switched off this session. Game-free.
    /// </summary>
    public static class FeatureGuards
    {
        private static readonly List<FeatureGuard> All = new List<FeatureGuard>(128);
        private static readonly HashSet<string> OffFeatures = new HashSet<string>(StringComparer.Ordinal);

        public static IReadOnlyList<FeatureGuard> Guards => All;

        /// <summary>A guard for an entry point, made once as a static; off from the start if its feature is off already.</summary>
        public static FeatureGuard For(string feature, string where)
        {
            var guard = new FeatureGuard(feature, where);
            lock (All)
            {
                All.Add(guard);
                if (feature != null && OffFeatures.Contains(feature))
                {
                    guard.SwitchOff();   // a class first used after its feature was switched off
                }
            }
            return guard;
        }

        public static bool FeatureOff(string feature)
        {
            return feature != null && OffFeatures.Contains(feature);
        }

        public static bool AnyOff => OffFeatures.Count > 0;

        /// <summary>The features switched off this session, in no order.</summary>
        public static IEnumerable<string> OffList => OffFeatures;

        /// <summary>Switches every guard of a feature off; true the first time for that feature.</summary>
        public static bool SwitchOff(string feature)
        {
            lock (All)
            {
                if (feature == null || !OffFeatures.Add(feature))
                {
                    return false;
                }
                foreach (FeatureGuard guard in All)
                {
                    if (guard.Feature == feature)
                    {
                        guard.SwitchOff();
                    }
                }
            }
            return true;
        }

        /// <summary>For the checks: forgets the features switched off; the guards stay as they are.</summary>
        internal static void ForgetForTests()
        {
            OffFeatures.Clear();
        }
    }
}
