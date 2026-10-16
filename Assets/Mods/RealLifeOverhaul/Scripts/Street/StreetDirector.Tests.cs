using System.Collections.Generic;
using Streets.Pedestrians;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Street
{
    /// <summary>The options page's tests of street talk: each works when pressed, or says why not. Nothing per frame.</summary>
    public sealed partial class StreetDirector
    {
        private const string TestName = "Street talk";
        private const float TestReach = 8f;   // metres: the test talks to the nearest walker this near, whichever way the player faces

        private bool TestReady()
        {
            TestRefusal why = TestRefusals.Street();
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, TestName, why);
                return false;
            }
            if (!Settings.StreetTalk || _failed || _save == null)
            {
                TestRefusals.Tell(Logger, TestName, Settings.StreetTalk ? TestRefusal.NotLoaded : TestRefusal.FeatureOff);
                return false;
            }
            if (_chat != null || StreetGame.DialogOpen)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.ScreenBusy);
                return false;
            }
            return true;
        }

        /// <summary>Options-page test: the nearest passer-by stops and talks to the player now.</summary>
        public void TestTalk()
        {
            if (!TestReady())
            {
                return;
            }
            PlayerController player = StreetGame.Player;
            List<Pedestrian> walkers = PerfGame.Pedestrians;
            Pedestrian best = null;
            float bestSqr = TestReach * TestReach;
            for (int i = 0; player != null && walkers != null && i < walkers.Count; i++)
            {
                Pedestrian p = walkers[i];
                if (p == null || !StreetGame.Usable(p))
                {
                    continue;
                }
                float sqr = (p.transform.position - player.transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = p;
                }
            }
            if (best == null || !Begin(best, Time.unscaledTime))
            {
                StreetGame.Notice(NotificationType.Warning, "reallife_street_test_nobody", null, 8f);
                Logger?.Info("Street talk: test, nobody within " + TestReach + " m to talk to.");
            }
        }

        /// <summary>Options-page test: everybody forgets today's talks with the player (a rude word too).</summary>
        public void TestForget()
        {
            if (!TestReady())
            {
                return;
            }
            int n = _memory.Count;
            _memory.Clear();
            StreetGame.Notice(NotificationType.Info, "reallife_street_test_forgot", new Dictionary<string, string>(1) { { "count", n.ToString() } }, 8f);
            Logger?.Info("Street talk: test, " + n + " people forgot today's talks.");
        }

        /// <summary>Everything of street talk off the street (the save is being prepared for the mod's removal): whoever was stopped walks on.</summary>
        public void StopAll()
        {
            Stop("taken off the street");
        }
    }
}
