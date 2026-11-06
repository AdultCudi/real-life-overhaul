using System;
using System.Collections;
using BAModAPI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A test button that starts something the game only allows in play (a routine, a scene). The options page is a menu and
    /// the game is paused behind it, so the check that guards the start can never pass while the button is being pressed.
    /// The test is held instead and run once the player has closed the menus and the game is back in play, or dropped with
    /// a toast after <see cref="WaitSeconds"/>. Cost: nothing unless a test is waiting; then a quarter-second poll.
    /// </summary>
    public static class TestWait
    {
        public const float WaitSeconds = 20f;
        private const string WaitsKey = "reallife_home_test_waits";

        private static Waiter _waiter;

        /// <summary>
        /// Runs <paramref name="go"/> now when <paramref name="free"/> holds, otherwise when it first does. Pressing another
        /// test while one waits replaces it. Call only after the test's other refusals have passed.
        /// </summary>
        public static void Run(IModLogger logger, string test, Func<bool> free, Action go)
        {
            Cancel();
            if (free())
            {
                go();
                return;
            }
            var host = new GameObject("RLO test wait");
            _waiter = host.AddComponent<Waiter>();
            _waiter.Begin(logger, test, free, go);
            Notifications.Show(NotificationType.Info, WaitsKey, null, 8f);
            logger?.Info(test + " test: waits for the menus to close, up to " + WaitSeconds + " s.");
        }

        public static void Cancel()
        {
            if (_waiter != null)
            {
                UnityEngine.Object.Destroy(_waiter.gameObject);
                _waiter = null;
            }
        }

        /// <summary>The poll behind a waiting test. Cost a frame: nothing (a coroutine that wakes four times a second); it is destroyed when it ends.</summary>
        private sealed class Waiter : MonoBehaviour
        {
            public void Begin(IModLogger logger, string test, Func<bool> free, Action go)
            {
                StartCoroutine(Guard.Routine(Poll(logger, test, free, go)));
            }

            private IEnumerator Poll(IModLogger logger, string test, Func<bool> free, Action go)
            {
                var step = new WaitForSecondsRealtime(0.25f);
                float until = Time.unscaledTime + WaitSeconds;
                while (true)
                {
                    yield return step;
                    bool ready;
                    try
                    {
                        ready = free();
                    }
                    catch (Exception e)
                    {
                        logger?.Error(e);
                        break;
                    }
                    if (ready)
                    {
                        try
                        {
                            go();
                        }
                        catch (Exception e)
                        {
                            logger?.Error(e);
                        }
                        break;
                    }
                    if (Time.unscaledTime > until)
                    {
                        TestRefusals.Tell(logger, test, TestRefusal.ScreenBusy, 0, "still a menu or a conversation open after " + WaitSeconds + " s");
                        break;
                    }
                }
                _waiter = null;
                Destroy(gameObject);
            }
        }
    }
}
