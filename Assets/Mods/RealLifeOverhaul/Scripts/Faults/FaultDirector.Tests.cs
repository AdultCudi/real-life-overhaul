using System.Collections.Generic;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Faults
{
    /// <summary>The options page's tests of the city's faults: each works when pressed, or says why not. Nothing per frame.</summary>
    public sealed partial class FaultDirector
    {
        private const string TestName = "City faults";

        // What must hold for a test: the faults running here (not a guest's: the host's are followed), the city read, the player outdoors.
        private bool TestReady()
        {
            TestRefusal why = TestRefusals.Street();
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, TestName, why);
                return false;
            }
            if (!Settings.CityFaults || !_loaded || _failed)
            {
                TestRefusals.Tell(Logger, TestName, Settings.CityFaults ? TestRefusal.NotLoaded : TestRefusal.FeatureOff);
                return false;
            }
            if (Following)
            {
                TestRefusals.TellKey(Logger, TestName, "reallife_faults_test_guest");
                return false;
            }
            if (!FaultGame.Ready)
            {
                TestRefusals.TellKey(Logger, TestName, "reallife_faults_test_city", "the city's doors are read behind the next black screen: walk through a door and press again");
                return false;
            }
            return true;
        }

        /// <summary>Options-page test: one fault of each kind near the player, now (a dark lamp, a pothole, litter, an unsafe place and a hot spot where they stand).</summary>
        public void TestNear()
        {
            if (!TestReady() || !FaultGame.Player(out Vector3 player))
            {
                return;
            }
            long now = FaultGame.Now();
            int made = 0;
            for (int k = 0; k < FaultRules.Kinds; k++)
            {
                var kind = (FaultKind)k;
                int variant = _dice.Next(1000);
                if (!FaultGame.PlaceNear(kind, player, variant, _file, out Vector3 at, out string address, out string district))
                {
                    Logger?.Info("City faults: test, no place near the player for " + kind + ".");
                    continue;
                }
                if (kind == FaultKind.Hotspot)
                {
                    at = player;
                }
                Fault f = FaultRules.Add(_file, kind, district, address, at.x, at.y, at.z, _dice.Next(360), variant, now);
                if (kind == FaultKind.Hotspot)
                {
                    f.Crimes = 1;
                    f.LastCrime = now;
                }
                made++;
                Logger?.Info("City faults: test, " + Words(kind) + " at " + Where(f) + ".");
            }
            Changed(true);
            Notifications.Show(NotificationType.Info, "reallife_faults_test_near_done", new Dictionary<string, string>(1) { { "count", made.ToString() } }, 8f);
        }

        /// <summary>Options-page test: the city's people report every fault not yet taken by a crew, now.</summary>
        public void TestReports()
        {
            if (!TestReady())
            {
                return;
            }
            int posted = 0;
            for (int i = 0; i < _file.Faults.Count; i++)
            {
                Fault f = _file.Faults[i];
                if (f.Fixing)
                {
                    continue;
                }
                f.Reports++;
                if (FaultBridges.CityReport != null && FaultBridges.CityReport(FaultRules.ReportOf(f.Kind), f.District, f.Address, new Vector3(f.X, f.Y, f.Z)))
                {
                    posted++;
                }
            }
            Logger?.Info("City faults: test, the city's people report " + posted + " fault(s) on Flare.");
            Changed(true);
            Notifications.Show(NotificationType.Info, "reallife_faults_test_reports_done", new Dictionary<string, string>(1) { { "count", posted.ToString() } }, 8f);
        }

        /// <summary>Options-page test: every fault in the city mended at once, everything put back.</summary>
        public void TestMend()
        {
            if (!TestReady())
            {
                return;
            }
            int day = (int)(FaultGame.Now() / 1440);
            int count = _file.Faults.Count;
            while (_file.Faults.Count > 0)
            {
                FaultRules.Fixed(_file, _file.Faults[0].Id, day);
            }
            Logger?.Info("City faults: test, all " + count + " fault(s) mended.");
            Changed(true);
            Notifications.Show(NotificationType.Info, "reallife_faults_test_mend_done", new Dictionary<string, string>(1) { { "count", count.ToString() } }, 8f);
        }
    }
}
