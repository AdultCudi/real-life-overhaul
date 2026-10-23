using System.Collections.Generic;
using RealLifeOverhaul.FitOut;
using UI.Notification;

namespace RealLifeOverhaul.Floors
{
    /// <summary>The floors' test button (Options > Tests): a test office on a spare floor, on another structure than D1 and its building's own.</summary>
    public sealed partial class FloorsDirector
    {
        private const string TestId = "floors_test";
        private const string TestName = "Floors";      // the test's name in its log lines
        private const string TestFeature = "diagnostics";   // the feature its button stands under (Meta/FeatureList): it runs in any city
        private const string SchoolSize = "ba:buildingsize_d";   // the university's structure (D1): the one the floors were proven on already
        private const int SchoolVersion = 1;
        private static FloorPlace _test;               // the test's place while it stands

        /// <summary>
        /// The test button: a test office (FitOut's city office, the game's office designs first) on a spare floor of a building the
        /// mod holds, on the game's largest office structure that is neither D1 nor the building's own (a shop's if there is none),
        /// and the player taken there by the game's own door. It stands until the button is pressed again or the city goes.
        /// </summary>
        public static void TestPlace()
        {
            if (Instance == null || _broken || !FloorsGame.Loaded)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.NotLoaded);
                return;
            }
            if (_test != null && Guests.Contains(_test))
            {
                Release(_test, "the test office is taken away");
                Logger?.Info("Floors test: the test office is taken away.");
                Notifications.Show(NotificationType.Info, "reallife_floors_test_gone", null, 8f, trackOnSaveGame: false);
                return;
            }
            if (FloorsGame.InVehicle)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.InVehicle);
                return;
            }
            if (!FloorsGame.Free() || FloorsGame.Entering || Loan.Pending || Loan.Borrowed || _lifting)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.ScreenBusy);
                return;
            }
            Instance.TestNow();
        }

        private void TestNow()
        {
            if (_test == null)
            {
                _test = new FloorPlace(TestId, TestFeature, "reallife_floors_test_name") { Fit = FitKind.CivicOffice, LogName = "the test office" };
                _test.Designs.Add("LawFirm");
                _test.Designs.Add("Headquarters");
                _test.Designs.Add("MarketingAgency");
            }
            _test.Structure = null;
            Ask(_test);
            Book();
            FloorHost host = _test.Host;
            if (host == null)
            {
                Release(_test, "no spare floor");
                TestRefusals.TellKey(Logger, TestName, "reallife_floors_test_none", Hosts.Count + " building(s) held, none with a spare floor, and no empty office building free to take");
                return;
            }
            string own = OwnSize(host, out int ownVersion);
            FloorStructure structure = FloorsGame.OtherStructure(own, ownVersion, SchoolSize, SchoolVersion);
            if (structure == null)
            {
                Release(_test, "no other structure");
                TestRefusals.TellKey(Logger, TestName, "reallife_floors_test_none",
                    "the game lists no office or shop structure other than D1 and the building's own " + FloorsGame.Words(own, ownVersion));
                return;
            }
            _test.Structure = structure;
            int floor = _test.First;
            string why = _visit != null && ReferenceEquals(_visit.Host, host) ? LiftNow(floor, "the test") : EnterNow(host, floor, "the test", false);
            if (why != null)
            {
                Release(_test, "the test could not go in");
                TestRefusals.TellKey(Logger, TestName, "reallife_floors_test_none", "the way in was refused: " + why);
                return;
            }
            string address = FloorsGame.Address(host.Building);
            Logger?.Info("Floors test: a test office on floor " + floor + " of " + host.Name + " (" + address + "), on structure " + FloorsGame.Words(structure.Size, structure.Version)
                         + " (the building's own is " + FloorsGame.Words(own, ownVersion) + "), " + host.Floors + " floors there now; the player is taken there.");
            var data = new Dictionary<string, string>(2) { { "floor", floor.ToString() }, { "address", address } };   // the game keeps the toast's data
            Notifications.Show(NotificationType.Info, "reallife_floors_test_done", data, 12f, trackOnSaveGame: false);
        }
    }
}
