using System.Collections.Generic;
using BAModAPI;
using Helpers;
using UI.Notification;

namespace RealLifeOverhaul
{
    /// <summary>Why an options-page test button did nothing. Each has its own sentence in en.json.</summary>
    public enum TestRefusal
    {
        None,
        NotLoaded,      // the save (or the mod's part of it) is not ready yet
        Indoors,        // the player is inside a building
        TrafficOff,     // out of doors, but the traffic system is not running: a menu, fast-forward, the first moments outside
        SceneActive,    // another scene of the same kind is on
        PoliceBusy,     // the police already have business with the player
        PoolBusy,       // every character (or ragdoll) the test needs is in use
        FeatureOff,     // its option is switched off
        NoMovingCar,    // no traffic car on the move within {metres}
        NobodyNear,     // no pedestrian within {metres}
        NotDriving,     // the test needs the player at the wheel
        NothingFits,    // nothing of the kind can be staged at this place and hour
        NoPrecinct,     // the city has no police precinct (none could be designated)
        ScreenBusy,     // a conversation or a time skip is going on
        NoBusStop,      // no bus stop within {metres}
        NoBusLine,      // this city has no bus line at all
        NoOwnCar,       // the test needs a car the player owns
        NoRoad,         // no plain stretch of road (no junction, no bend) within {metres}
        NoBin,          // no bin or dumpster beside a road within {metres}
        NoShopfront,    // no flat shopfront with pavement in front and a road beside it
        OnFootOnly,     // the test needs the player on foot
        NoPavement,     // the player is not on or beside a pavement
        NoWayIn,        // nowhere out of view to walk in from, or no way from there to the player
        NobodyKnown,    // the player has not met anybody the test could use
        AllMet,         // everybody of the cast has been met already
        NoVenue,        // no place of the kind within {metres}
        NoCar,          // none of the player's cars within {metres}
        InVehicle,      // the test needs the player on foot
        NoRoom,         // the ground where it would be put is not clear
        NoCarNear,      // not in a car, and no car of the player's within {metres}
        NotACar,        // the vehicle is not one the custom shop works on (a scooter, a hand truck)
        NotYours,       // the car is a job's or a hire company's
        OnJob,          // one of the mod's jobs is on
        NotOnFoot       // the test needs the player on foot, out of any vehicle
    }

    /// <summary>
    /// What every test button does when it cannot do what was asked: one line in the log with the
    /// reason and whatever was measured, one toast that names the reason. Called on a button
    /// press only.
    /// </summary>
    public static class TestRefusals
    {
        /// <summary>What must hold for anything staged on the street: a loaded save, out of doors, traffic running.</summary>
        public static TestRefusal Street()
        {
            if (!ModState.Loaded || SaveGameManager.Current == null || PlayerHelper.PlayerController == null)
            {
                return TestRefusal.NotLoaded;
            }
            if (BuildingManager.IsInsideBuilding)
            {
                return TestRefusal.Indoors;
            }
            return TrafficTools.Ready ? TestRefusal.None : TestRefusal.TrafficOff;
        }

        /// <summary>Says why a test did not start. <paramref name="metres"/> fills {metres} in the two sentences that have it.</summary>
        public static void Tell(IModLogger logger, string test, TestRefusal why, int metres = 0, string detail = null)
        {
            logger?.Info(test + " test: not started (" + why + (metres > 0 ? ", " + metres + " m" : string.Empty)
                         + (detail != null ? "; " + detail : string.Empty) + ").");
            Dictionary<string, string> data = null;
            if (why == TestRefusal.NoMovingCar || why == TestRefusal.NobodyNear || why == TestRefusal.NoBusStop || why == TestRefusal.NoRoad || why == TestRefusal.NoBin || why == TestRefusal.NoVenue || why == TestRefusal.NoCar || why == TestRefusal.NoCarNear)
            {
                data = new Dictionary<string, string>(1) { { "metres", metres.ToString() } };   // the game keeps the toast's data
            }
            Notifications.Show(NotificationType.Warning, Key(why), data, 8f);
        }

        /// <summary>
        /// The same for a reason that belongs to one feature and has its own sentence (a locale key), such as "no water
        /// in front of you" or "no bar within reach". One log line, one toast, one sentence per cause.
        /// </summary>
        public static void TellKey(IModLogger logger, string test, string sentenceKey, string detail = null)
        {
            logger?.Info(test + " test: not started (" + sentenceKey + (detail != null ? "; " + detail : string.Empty) + ").");
            Notifications.Show(NotificationType.Warning, sentenceKey ?? "reallife_test_refused_not_loaded", null, 8f);
        }

        private static string Key(TestRefusal why)
        {
            switch (why)
            {
                case TestRefusal.Indoors: return "reallife_test_refused_indoors";
                case TestRefusal.TrafficOff: return "reallife_test_refused_traffic_off";
                case TestRefusal.SceneActive: return "reallife_test_refused_scene_active";
                case TestRefusal.PoliceBusy: return "reallife_test_refused_police_busy";
                case TestRefusal.PoolBusy: return "reallife_test_refused_pool_busy";
                case TestRefusal.FeatureOff: return "reallife_test_refused_feature_off";
                case TestRefusal.NoMovingCar: return "reallife_test_refused_no_moving_car";
                case TestRefusal.NobodyNear: return "reallife_test_refused_nobody_near";
                case TestRefusal.NotDriving: return "reallife_test_refused_not_driving";
                case TestRefusal.NothingFits: return "reallife_test_refused_nothing_fits";
                case TestRefusal.NoPrecinct: return "reallife_test_refused_no_precinct";
                case TestRefusal.ScreenBusy: return "reallife_test_refused_screen_busy";
                case TestRefusal.NoBusStop: return "reallife_test_refused_no_bus_stop";
                case TestRefusal.NoBusLine: return "reallife_test_refused_no_bus_line";
                case TestRefusal.NoOwnCar: return "reallife_test_refused_no_own_car";
                case TestRefusal.NoRoad: return "reallife_test_refused_no_road";
                case TestRefusal.NoBin: return "reallife_test_refused_no_bin";
                case TestRefusal.NoShopfront: return "reallife_test_refused_no_shopfront";
                case TestRefusal.OnFootOnly: return "reallife_test_refused_on_foot_only";
                case TestRefusal.NoPavement: return "reallife_test_refused_no_pavement";
                case TestRefusal.NoWayIn: return "reallife_test_refused_no_way_in";
                case TestRefusal.NobodyKnown: return "reallife_test_refused_nobody_known";
                case TestRefusal.AllMet: return "reallife_test_refused_all_met";
                case TestRefusal.NoVenue: return "reallife_test_refused_no_venue";
                case TestRefusal.NoCar: return "reallife_test_refused_no_car";
                case TestRefusal.InVehicle: return "reallife_test_refused_in_vehicle";
                case TestRefusal.NoRoom: return "reallife_test_refused_no_room";
                case TestRefusal.NoCarNear: return "reallife_test_refused_no_car_near";
                case TestRefusal.NotACar: return "reallife_test_refused_not_a_car";
                case TestRefusal.NotYours: return "reallife_test_refused_not_yours";
                case TestRefusal.OnJob: return "reallife_test_refused_on_job";
                case TestRefusal.NotOnFoot: return "reallife_test_refused_not_on_foot";
                default: return "reallife_test_refused_not_loaded";
            }
        }
    }
}
