using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The fault count of one entry point of a feature (a director's Update, a component's FixedUpdate, a handler on
    /// one of the game's events). A fault now and then is reported and the feature goes on; <see cref="Most"/> of them
    /// within <see cref="Seconds"/> seconds switch it off for the session (<see cref="Off"/>), so that a feature broken
    /// by a game update says so once instead of throwing every frame. Core/Guard does the saying and the switching;
    /// this class is the arithmetic, with no Unity or game types (ContractCheck tests it).
    /// </summary>
    public sealed class FeatureGuard
    {
        /// <summary>Faults within the window that switch an entry point off.</summary>
        public const int Most = 5;

        /// <summary>The window, in seconds of real time.</summary>
        public const float Seconds = 30f;

        /// <summary>The feature of Meta/FeatureList it belongs to (null: the mod's own machinery).</summary>
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

        /// <summary>True once it has been switched off; for the rest of the session.</summary>
        public bool Off { get; private set; }

        /// <summary>Faults since the session began, counted after it was switched off too.</summary>
        public int Faults { get; private set; }

        /// <summary>
        /// Counts one fault at the given time (seconds of real time). True only for the fault that switches it off;
        /// every later call answers false.
        /// </summary>
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

        /// <summary>Switched off from outside (its feature was switched off by another of its entry points).</summary>
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

        /// <summary>A guard for an entry point, made once (a static of the class it guards); off from the start when its feature is off already.</summary>
        public static FeatureGuard For(string feature, string where)
        {
            var guard = new FeatureGuard(feature, where);
            lock (All)
            {
                All.Add(guard);
                if (feature != null && OffFeatures.Contains(feature))
                {
                    guard.SwitchOff();   // (a class first used after its feature was switched off, as the mod started or after its faults)
                }
            }
            return guard;
        }

        /// <summary>True when the feature was switched off this session after its faults.</summary>
        public static bool FeatureOff(string feature)
        {
            return feature != null && OffFeatures.Contains(feature);
        }

        /// <summary>True when any feature was switched off this session.</summary>
        public static bool AnyOff => OffFeatures.Count > 0;

        /// <summary>The features switched off this session, in no order.</summary>
        public static IEnumerable<string> OffList => OffFeatures;

        /// <summary>
        /// Switches the whole feature off: every guard of it says Off from now on. True the first time for that feature.
        /// </summary>
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

        /// <summary>For the checks: forget every feature switched off (the guards themselves stay as they are).</summary>
        internal static void ForgetForTests()
        {
            OffFeatures.Clear();
        }
    }
}
