using System;
using System.Collections.Generic;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Events
{
    /// <summary>The city events' test buttons on the options page.</summary>
    public sealed partial class EventDirector
    {
        private bool TestReady(string test, out PlayerController player)
        {
            player = PlayerHelper.PlayerController;
            TestRefusal why = _failed ? TestRefusal.FeatureOff : TestRefusals.Street();
            if (why == TestRefusal.None && (TrafficAccidents.IsActive || CrimeDirector.IsActive))
            {
                why = TestRefusal.SceneActive;
            }
            if (why == TestRefusal.None && PoliceAttention.IsBusy)
            {
                why = TestRefusal.PoliceBusy;
            }
            if (why == TestRefusal.None && (!ClipLibrary.Ready || !PropLibrary.Ready))
            {
                why = TestRefusal.NotLoaded;
            }
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, test, why, 0, _failed ? "city events faulted earlier this session" : null);
                return false;
            }
            EndScene(true, "a test button");
            People.WarmAll(player.transform.position);
            return true;
        }

        private static Vector3 Ahead(PlayerController player, float metres)
        {
            Camera camera = GameManager.GetMainCamera();
            Vector3 ahead = camera != null ? camera.transform.forward : player.transform.forward;
            ahead.y = 0f;
            ahead = ahead.sqrMagnitude > 0.01f ? ahead.normalized : Vector3.forward;
            return player.transform.position + ahead * metres;
        }

        /// <summary>Options-page test: stages a fire of a kind near the player, now.</summary>
        public void TestFire(FireKind kind)
        {
            TestFire(kind, false);
        }

        /// <summary>
        /// Options-page test: a bin fire with its engine sent at once and a stopped bus of the mod's own standing in the engine's lane
        /// ahead of it; the log's "Fire check (stopped bus)" line says PASS if the engine comes to a stop within the time wanted.
        /// </summary>
        public void TestFireBlocked()
        {
            TestFire(FireKind.Bin, true);
        }

        private void TestFire(FireKind kind, bool blocked)
        {
            try
            {
                if (!TestReady(blocked ? "Fire engine behind a bus" : "Fire", out PlayerController player))
                {
                    return;
                }
                if (_fire.Stage(kind, player.transform.position, Ahead(player, 14f), true, Time.time, blocked))
                {
                    Begin(_fire, 600f);
                    return;
                }
                EndSceneQuietly();
                TestRefusals.Tell(Logger, blocked ? "Fire engine behind a bus" : kind + " fire", _fire.Why, _fire.WhyMetres, _fire.Refusal);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Options-page test: holds a street event of a kind on the road ahead of the player, now.</summary>
        public void TestEvent(EventKind kind)
        {
            try
            {
                if (!TestReady("Street event", out PlayerController player))
                {
                    return;
                }
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    if (_street.Stage(kind, Ahead(player, 16f + attempt * 12f), true, 24, Time.time))
                    {
                        Begin(_street, 50f);
                        if (!EventCalendar.Quiet(kind))
                        {
                            var data = new Dictionary<string, string>(2) { { "from", Clock(TimeHelper.CurrentHour) }, { "to", Clock(TimeHelper.CurrentHour + 3) } };
                            Notifications.Show(NotificationType.Info, EventCalendar.Today(kind), data, 8f);
                        }
                        return;
                    }
                }
                EndSceneQuietly();
                TestRefusals.Tell(Logger, kind + " street event",
                    _street.Refusal != null && _street.Refusal.StartsWith("no plain", StringComparison.Ordinal) ? TestRefusal.NoRoad : TestRefusal.NothingFits,
                    30, _street.Refusal);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Options-page test: holds the weather's effects (not its looks) as given until cleared.</summary>
        public void TestWeather(Sky sky, Spell spell)
        {
            if (_failed || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, "Weather", _failed ? TestRefusal.FeatureOff : TestRefusal.NotLoaded);
                return;
            }
            Weather.Release();
            Weather.Reset();
            Weather.Force(sky, spell);
            _weather.Changed();
            _weather.Tick(this);
            var data = new Dictionary<string, string>(3)
            {
                { "grip", Mathf.RoundToInt(Weather.GripFactor * 100f).ToString() }, { "accidents", Mathf.RoundToInt(Weather.AccidentFactor * 100f).ToString() },
                { "crime", Mathf.RoundToInt(Weather.CrimeFactor * 100f).ToString() }
            };
            Notifications.Show(NotificationType.Info, "reallife_weather_test_on", data, 9f);
        }

        /// <summary>Options-page test: clears any fire or street event and gives the weather back to the game.</summary>
        public void TestClear()
        {
            try
            {
                EndScene(true, "the clear button");
                StopCrashFire();
                Weather.Release();
                Weather.Reset();
                _weather.Changed();   // fair weather for a moment: the game's own is read again within the second
                Notifications.Show(NotificationType.Info, "reallife_events_test_cleared", null, 5f);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // A stage that refused may have put part of itself out: it is taken back without a word.
        private void EndSceneQuietly()
        {
            SafetyNet();
            Cordon.Recheck();
        }
    }
}
