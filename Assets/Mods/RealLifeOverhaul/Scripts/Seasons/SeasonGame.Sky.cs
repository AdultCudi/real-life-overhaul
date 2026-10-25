using System;
using System.Reflection;
using BigAmbitions.DayNightCycle;
using UnityEngine;
using UnityEngine.UI.Extensions;
using Object = UnityEngine.Object;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>
    /// The seasons' sky and rain, through the game's own pieces. The game's light, sky colours, fog and exposure come every
    /// frame from its fair-weather settings (an EnvironmentSettings asset), blended into its rain settings as a shower
    /// comes (TimeOfDayController). An overcast snow day and summer's warmth are a copy of the fair-weather asset, blended
    /// part of the way to the rain's (as the game's own rain does) and warmed, put in the asset's place: written only when
    /// the look changes, the game reads it every frame for nothing more. The season's rain is the game's own rain, started
    /// with its own console command and ended at the season's hour by its own fade.
    /// </summary>
    public static partial class SeasonGame
    {
        private const int CurveSamples = 49;        // half-hourly over the day: the blended curves follow the game's closely
        private const float WarmKelvin = 650f;      // summer's sun this much warmer (lower) in colour temperature at full warmth
        private const float FadeMinutes = 30f;      // the game's own rain fades over 30 to 60 minutes; the season's over 30

        private static EnvironmentSettings _own;     // the game's fair-weather asset, while ours stands in its place
        private static EnvironmentSettings _made;    // the copy in its place
        private static float _skyOvercast = -1f, _skyWarmth = -1f;
        private static bool _skyFailed;

        private static bool _rainOurs;               // the rain falling is the season's, started by the mod
        private static float _rainEnd;               // the game's minutes it is to end at

        /// <summary>The overcast and warmth on the game's sky now (0 and 0: its own asset).</summary>
        public static float SkyOvercast => _made != null ? _skyOvercast : 0f;
        public static float SkyWarmth => _made != null ? _skyWarmth : 0f;

        /// <summary>Why the sky cannot be changed this session, for the log and the test; null while it can.</summary>
        public static string SkyMissing { get; private set; }

        /// <summary>
        /// The game's sky and light <paramref name="overcast"/> (0 to 1) of the way to its rain's, and its sun
        /// <paramref name="warmth"/> (0 to 1) warmer; both 0 puts the game's own asset back. Written only on a change.
        /// </summary>
        public static void Sky(float overcast, float warmth)
        {
            if (_skyFailed || (Mathf.Approximately(overcast, SkyOvercast) && Mathf.Approximately(warmth, SkyWarmth)))
            {
                return;
            }
            try
            {
                TimeOfDayController time = Clock();
                FieldInfo fair = GameContract.DaySettings.Field, wet = GameContract.RainSettings.Field;
                if (time == null || fair == null || wet == null)
                {
                    if (fair == null || wet == null)
                    {
                        Fail("the game's weather settings cannot be read in this build");
                    }
                    return;   // no city's clock yet: asked again in a second
                }
                if (overcast <= 0f && warmth <= 0f)
                {
                    RestoreSky();
                    return;
                }
                var now = fair.GetValue(time) as EnvironmentSettings;
                var rain = wet.GetValue(time) as EnvironmentSettings;
                if (now == null || rain == null)
                {
                    return;
                }
                if (_made == null || now != _made)
                {
                    DropSky();
                    _own = now;   // the game's, or another's put there since: kept as the own from now on
                    _made = Object.Instantiate(now);
                    _made.name = now.name + " (RealLifeOverhaul seasons)";
                }
                Blend(_made, _own, rain, overcast, warmth);
                fair.SetValue(time, _made);
                _skyOvercast = overcast;
                _skyWarmth = warmth;
            }
            catch (Exception e)
            {
                SeasonDirector.Logger?.Error(e);
                Fail("the game's weather settings could not be written (" + e.GetType().Name + ")");
            }
        }

        /// <summary>The game's own fair-weather asset back where it was, the copy gone. Safe at any time.</summary>
        public static void RestoreSky()
        {
            try
            {
                TimeOfDayController time = Clock();
                FieldInfo fair = GameContract.DaySettings.Field;
                if (time != null && fair != null && _made != null && fair.GetValue(time) as EnvironmentSettings == _made)
                {
                    fair.SetValue(time, _own);
                }
            }
            catch (Exception e)
            {
                SeasonDirector.Logger?.Error(e);
            }
            DropSky();
        }

        private static void DropSky()
        {
            if (_made != null)
            {
                Object.Destroy(_made);
            }
            _made = null;
            _own = null;
            _skyOvercast = _skyWarmth = -1f;
        }

        private static void Fail(string why)
        {
            _skyFailed = true;
            SkyMissing = why;
            SeasonDirector.Logger?.Warn("Seasons: the sky keeps the game's own look this session: " + why + ".");
            RestoreSky();
        }

        private static TimeOfDayController Clock()
        {
            GameManager manager = InstanceBehavior<GameManager>.IsInitialized ? InstanceBehavior<GameManager>.Instance : null;
            return manager != null ? manager.timeOfDayController : null;
        }

        // The light, exposure, fog and sky colours part of the way to the rain's; the sun's colour warmer. From the game's
        // own each time, never from the last blend, so nothing drifts.
        private static void Blend(EnvironmentSettings made, EnvironmentSettings own, EnvironmentSettings rain, float overcast, float warmth)
        {
            made.sunIntensity = Mix(own.sunIntensity, rain.sunIntensity, overcast, 0f);
            made.exposure = Mix(own.exposure, rain.exposure, overcast, 0f);
            made.fogAttDistance = Mix(own.fogAttDistance, rain.fogAttDistance, overcast, 0f);
            made.sunTemperature = Mix(own.sunTemperature, rain.sunTemperature, overcast, -WarmKelvin * warmth);
            made.dayVolumeTop = Mix(own.dayVolumeTop, rain.dayVolumeTop, overcast);
            made.dayVolumeMiddle = Mix(own.dayVolumeMiddle, rain.dayVolumeMiddle, overcast);
            made.dayVolumeBottom = Mix(own.dayVolumeBottom, rain.dayVolumeBottom, overcast);
        }

        private static AnimationCurve Mix(AnimationCurve own, AnimationCurve rain, float t, float add)
        {
            if (own == null)
            {
                return null;
            }
            var keys = new Keyframe[CurveSamples];
            for (int i = 0; i < CurveSamples; i++)
            {
                float hour = 24f * i / (CurveSamples - 1);
                float a = own.Evaluate(hour), b = rain != null ? rain.Evaluate(hour) : a;
                keys[i] = new Keyframe(hour, Mathf.Lerp(a, b, t) + add);
            }
            var curve = new AnimationCurve(keys) { preWrapMode = own.preWrapMode, postWrapMode = own.postWrapMode };
            for (int i = 0; i < CurveSamples; i++)
            {
                curve.SmoothTangents(i, 0f);
            }
            return curve;
        }

        // A sky gradient blended at the game's own key times (a gradient holds eight colour keys at most).
        private static HDRGradient Mix(HDRGradient own, HDRGradient rain, float t)
        {
            if (own == null || own.gradient == null)
            {
                return own;
            }
            GradientColorKey[] keys = own.gradient.colorKeys;
            for (int i = 0; i < keys.Length; i++)
            {
                Color b = rain != null && rain.gradient != null ? rain.gradient.Evaluate(keys[i].time) : keys[i].color;
                keys[i].color = Color.Lerp(keys[i].color, b, t);
            }
            var gradient = new Gradient { mode = own.gradient.mode };
            gradient.SetKeys(keys, own.gradient.alphaKeys);
            return new HDRGradient { gradient = gradient };
        }

        /// <summary>
        /// The season's rain at <paramref name="strength"/> (0.4 to 1, the game's own range) until <paramref name="endHour"/>
        /// of today, through the game's own rain; nothing if the game's own rain is already falling. True when it is the season's.
        /// </summary>
        public static bool StartRain(float strength, float endHour, bool instant = false)
        {
            if (_rainOurs || RainHelper.isRaining)
            {
                return _rainOurs;
            }
            FieldInfo end = GameContract.RainEnd.Field, fade = GameContract.RainFade.Field;
            if (end == null || fade == null)
            {
                return false;
            }
            float now = TimeHelper.NowInMinutes();
            float until = now + Mathf.Max(FadeMinutes + 30f, (endHour - Hour) * 60f);
            RainHelper.Command_ToggleRain(Mathf.Clamp(strength, 0.4f, 1f), instant);   // instant: the test's, with its drops at once
            if (!RainHelper.isRaining)
            {
                return false;   // the game's rain is not set up (a load under way)
            }
            // The game drew its own length for the shower; the season's ends it at its hour, with the game's own fade.
            end.SetValue(null, new Timestamp(until));
            fade.SetValue(null, new Timestamp(until - FadeMinutes));
            _rainOurs = true;
            _rainEnd = until;
            return true;
        }

        /// <summary>The season's rain over: its end left to the game's fade if near, else brought forward to now. Safe at any time.</summary>
        public static void EndRain()
        {
            if (!_rainOurs)
            {
                return;
            }
            _rainOurs = false;
            if (RainHelper.isRaining && TimeHelper.NowInMinutes() < _rainEnd - 1f)
            {
                RainHelper.Command_ToggleRain();   // raining: the game's own command stops it
            }
        }

        /// <summary>The season's own rain is falling (it ends by itself at its hour).</summary>
        public static bool RainOurs
        {
            get
            {
                if (_rainOurs && !RainHelper.isRaining)
                {
                    _rainOurs = false;   // over at its hour, or stopped by the game
                }
                return _rainOurs;
            }
        }

        public static bool RainFalling => RainHelper.AreRainDropsFalling;

        /// <summary>Every look of the seasons' on the game's things put back: the trees, the sky, the season's rain, people's clothes.</summary>
        public static void RestoreLooks()
        {
            ResetTrees();
            RestoreSky();
            EndRain();
            RestoreCoats();
        }

        /// <summary>The city is unloading: what could not be had this city may be tried again in the next.</summary>
        public static void ForgetFailures()
        {
            _skyFailed = false;
            SkyMissing = null;
        }
    }
}
