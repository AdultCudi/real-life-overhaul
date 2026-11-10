using System;
using System.Collections;
using System.Collections.Generic;
using Helpers;
using Localizor;
using RealLifeOverhaul.Uptown;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Subway
{
    /// <summary>
    /// The stations' test entries (Options > Tests): the check of what stands, the player taken to each station in turn, a
    /// ride of the game's own from the nearest of its stations to one of the districts', and somebody coming up at the
    /// station the player is near. Each says why in a notice when it cannot run. On a press only.
    /// </summary>
    public sealed partial class StationsDirector
    {
        private const string TestName = "Subway";
        private const string NoticeId = "RealLifeOverhaul subway stations";   // one notice at a time: a new one replaces it
        private const float NoticeSeconds = 10f;
        private const float PickAfter = 1.5f;      // s of real time the game's map is given to open before a station is picked on it

        private int _goNext;      // the station the next press of "take me" goes to
        private int _rideNext;    // ... and the next ride ends at

        private static void Notice(string key, Dictionary<string, string> data, bool wrong = false)
        {
            Notifications.Show(wrong ? NotificationType.Warning : NotificationType.Info, key, data, NoticeSeconds, NoticeId, null, notificationSound: false, trackOnSaveGame: false);
        }

        // Why a test cannot run now, told; false when it can.
        private static bool Refused(bool wantsStations)
        {
            TestRefusal why = Instance == null || !ModState.Loaded || SaveGameManager.Current == null || PlayerHelper.PlayerController == null ? TestRefusal.NotLoaded
                : !Settings.UptownSubway ? TestRefusal.FeatureOff : TestRefusal.None;
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, TestName, why);
                return true;
            }
            if (wantsStations && !Standing)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.NothingFits, 0, NoneWhy());
                return true;
            }
            return false;
        }

        private static string NoneWhy()
        {
            StationsDirector director = Instance;
            return director != null && director._stoodWhy != null ? director._stoodWhy
                : !UptownLots.Ready ? "the districts do not stand at this load" + (UptownLots.Why != null ? " (" + UptownLots.Why + ")" : string.Empty)
                : director != null && director._closed ? "its feature was cleaned up: it stands again at the next load"
                : "the game's own stations are not loaded yet";
        }

        /// <summary>"Subway: check the stations of Uptown and Cedar Heights": what stands against what was wanted, one line and a notice.</summary>
        public static void TestStations()
        {
            try
            {
                if (Instance == null || !ModState.Loaded || !Settings.UptownSubway || !Standing)
                {
                    // The test run reads a line either way.
                    Logger?.Info("Subway check (stations): none stands (" + (!Settings.UptownSubway ? "its option is off" : NoneWhy()) + "): "
                                 + (!Settings.UptownSubway ? "PASS" : "FAIL"));
                    Refused(true);
                    return;
                }
                bool pass = StationsGame.Check(out string line) && StationsGame.Count == StationsRules.Sites.Count;
                Logger?.Info("Subway check (stations): " + line + (StationsGame.Count == StationsRules.Sites.Count ? string.Empty : " (of " + StationsRules.Sites.Count + " planned: FAIL)")
                             + "; people of the line so far: " + Instance._cameUp + " came up, " + Instance._wentDown + " went down.");
                Notice(pass ? StationsText.CheckDone : StationsText.CheckWrong, new Dictionary<string, string>(1) { { "count", StationsGame.Count.ToString(Inv) } }, !pass);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>"Subway: take me to the next station of the districts": on foot, where its riders come up; each press the next one.</summary>
        public static void TestGo()
        {
            try
            {
                if (Refused(true))
                {
                    return;
                }
                if (BuildingManager.IsInsideBuilding || UI.Load.LoadScene.isLoading)
                {
                    TestRefusals.Tell(Logger, TestName, TestRefusal.Indoors);
                    return;
                }
                if (PlayerHelper.IsUsingVehicle)
                {
                    TestRefusals.Tell(Logger, TestName, TestRefusal.OnFootOnly);
                    return;
                }
                if (SubwaySystem.IsRiding || CityMap.IsOpen)
                {
                    TestRefusals.Tell(Logger, TestName, TestRefusal.ScreenBusy);
                    return;
                }
                StationsDirector director = Instance;
                int index = director._goNext % StationsGame.Count;
                director._goNext = index + 1;
                StationEntrance station = StationsGame.StationAt(index);
                StationSite site = StationsGame.SiteAt(index);
                if (station == null || station.Arrival == null || site == null)
                {
                    TestRefusals.Tell(Logger, TestName, TestRefusal.NothingFits, 0, "the station is gone");
                    return;
                }
                NewGround.WalkGame.Warp(station.Arrival.position);
                Logger?.Info("Subway: test: the player put where " + site.Id + "'s riders come up, at (" + station.Arrival.position.x.ToString("F1", Inv) + ", "
                             + station.Arrival.position.z.ToString("F1", Inv) + ").");
                Notice(StationsText.GoDone, new Dictionary<string, string>(1) { { "name", StationsGame.NameOf(site) } });
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>
        /// "Subway: ride from the nearest of the game's stations to one of the districts'": as the game's entrance does it (its map in subway
        /// mode from that station), then the game's own travel to the districts' station, at the game's fare.
        /// </summary>
        public static void TestRide()
        {
            try
            {
                if (Refused(true))
                {
                    return;
                }
                CityManager city = InstanceBehavior<CityManager>.Instance;
                TestRefusal why = BuildingManager.IsInsideBuilding ? TestRefusal.Indoors
                    : PlayerHelper.IsUsingVehicle ? TestRefusal.OnFootOnly
                    : SubwaySystem.IsRiding || !TransitRide.CanStart() ? TestRefusal.SceneActive
                    : city == null || city.subwayStations == null || city.subwaySystem == null || city.cityMap == null ? TestRefusal.NothingFits : TestRefusal.None;
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, TestName, why);
                    return;
                }
                Vector3 at = PlayerHelper.PlayerController.transform.position;
                SubwayStation from = null;
                float best = float.MaxValue;
                for (int i = 0; i < city.subwayStations.Count; i++)
                {
                    SubwayStation station = city.subwayStations[i];
                    if (station == null || OwnStations.Is(station))
                    {
                        continue;
                    }
                    float sqr = CitySpots.Flat(station.transform.position - at);
                    if (sqr < best)
                    {
                        best = sqr;
                        from = station;
                    }
                }
                StationsDirector director = Instance;
                int index = director._rideNext % StationsGame.Count;
                director._rideNext = index + 1;
                StationEntrance to = StationsGame.StationAt(index);
                StationSite site = StationsGame.SiteAt(index);
                if (from == null || to == null || site == null || SaveGameManager.Current.Money < SubwayStation.PricePerRide)
                {
                    TestRefusals.Tell(Logger, TestName, TestRefusal.NothingFits, 0, "no station of the game's to ride from, or no money for the game's fare");
                    return;
                }
                // As the game's entrance does it (SubwayStation.Interact), then the game's own pick of a station on its open map.
                city.subwaySystem.lastSubwayStation = from;
                if (!CityMap.IsOpen)
                {
                    city.cityMap.Toggle();
                }
                if (!CityMap.IsOpen)
                {
                    city.subwaySystem.lastSubwayStation = null;
                    TestRefusals.Tell(Logger, TestName, TestRefusal.SceneActive, 0, "the game would not open its map now");
                    return;
                }
                city.cityMap.ToggleSubwayMode(true);
                Notice(StationsText.RideDone, new Dictionary<string, string>(2)
                {
                    { "from", GameMembers.NameKey(from).GetLocalization() }, { "name", StationsGame.NameOf(site) }
                });
                director.StartCoroutine(Guard.Routine(director.PickOnMap(to, site)));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The game's map holds the game's clock still, so this waits on the interface's.
        private IEnumerator PickOnMap(StationEntrance to, StationSite site)
        {
            float until = Time.unscaledTime + PickAfter;
            while (Time.unscaledTime < until)
            {
                yield return null;
            }
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (city != null && to != null && CityMap.IsOpen && city.cityMap.isSubwayMode && !SubwaySystem.IsRiding)
            {
                Logger?.Info("Subway: test: the game's own travel asked from " + OwnStations.LogName(city.subwaySystem.lastSubwayStation) + " to " + site.Id + ", at the game's fare.");
                city.subwaySystem.TravelTo(to);
            }
            else
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.SceneActive, 0, "the game's subway map was closed before the station was picked");
            }
        }

        /// <summary>"Subway: somebody comes up at the station nearest me": one rider of the line now, at the station the player stands near.</summary>
        public static void TestRider()
        {
            try
            {
                if (Refused(true))
                {
                    return;
                }
                StationsDirector director = Instance;
                TestRefusal why = !Settings.TransitPeople ? TestRefusal.FeatureOff
                    : BuildingManager.IsInsideBuilding ? TestRefusal.Indoors
                    : SubwaySystem.IsRiding || CityMap.IsOpen ? TestRefusal.ScreenBusy : TestRefusal.None;
                int near = why == TestRefusal.None ? Nearest(PlayerHelper.PlayerController.transform.position, StationsRules.RidersNear) : -1;
                if (why == TestRefusal.None && near < 0)
                {
                    TestRefusals.Tell(Logger, TestName, TestRefusal.NoVenue, Mathf.RoundToInt(StationsRules.RidersNear));
                    return;
                }
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, TestName, why);
                    return;
                }
                StationSite site = StationsGame.SiteAt(near);
                director.Drop(ref director._up);
                director._near = near;
                director._train = TrainNow();
                bool came = director.ComeUp(near, IndexOf(site), TimeHelper.CurrentDay, director._train + 7919, Time.unscaledTime);   // another rider than the train's own
                director._nextLook = 0f;
                Logger?.Info("Subway check (rider): somebody " + (came ? "comes up at " : "could not be put at ") + site.Id
                             + (came ? " and walks off along the pavement: PASS" : " (both people are out, or no pavement to walk off along): FAIL"));
                if (came)
                {
                    Notice(StationsText.RiderDone, new Dictionary<string, string>(1) { { "name", StationsGame.NameOf(site) } });
                }
                else
                {
                    TestRefusals.Tell(Logger, TestName, TestRefusal.PoolBusy);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
