using System;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Civic life's test buttons.</summary>
    public sealed partial class CivicDirector
    {
        private int _testPolicy;

        private bool TestReady(string test, bool street)
        {
            if (!_loaded || _failed || SaveGameManager.Current == null || PlayerHelper.PlayerController == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.Civic)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            if (street)
            {
                TestRefusal why = TestRefusals.Street();
                if (why == TestRefusal.None && _worldFailed)
                {
                    why = TestRefusal.FeatureOff;
                }
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, test, why);
                    return false;
                }
            }
            return true;
        }

        /// <summary>Test: an election campaign starts now (election day in a week); posters and boards go up round the player at once.</summary>
        public void TestCampaign()
        {
            if (!ElectionTestReady("Civic campaign", false))
            {
                return;
            }
            try
            {
                Government.TestCampaign(TimeHelper.CurrentDay);
                if (!BuildingManager.IsInsideBuilding && !_worldFailed)
                {
                    _campaign.Burst(PlayerHelper.GetPosition());
                    _worldOut = true;
                }
                Logger?.Info("Civic test: campaign started; " + _campaign.Dressing + " poster(s) and board(s) put up round the player.");
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Test: today is election day. The first press opens the polls; a second press (or a press outside polling hours) counts the votes.</summary>
        public void TestElectionDay()
        {
            if (!ElectionTestReady("Civic election day", false))
            {
                return;
            }
            try
            {
                Government.TestElectionDay(TimeHelper.CurrentDay, TimeHelper.CurrentHour);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Test: the next policy of the catalogue comes into force now (each press, the next one).</summary>
        public void TestPolicy()
        {
            if (!ElectionTestReady("Civic policy", false))
            {
                return;
            }
            try
            {
                PolicyDef policy = CivicCatalogue.All[_testPolicy % CivicCatalogue.All.Length];
                _testPolicy++;
                Government.Set(policy.Lever, policy.Dir, true);
                OnLevers();
                Logger?.Info("Civic test: policy '" + policy.Name + "' in force.");
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        public void TestRevert()
        {
            if (!TestReady("Civic revert", false) || HostOwns("Civic revert", 'G'))
            {
                return;
            }
            try
            {
                Government.RevertAll();
                CivicText.Toast(NotificationType.Info, "reallife_civic_test_reverted", 8f);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Test: a site of a kind starts at the nearest building and is set out now.</summary>
        public void TestSite(SiteKind kind)
        {
            string test = "Civic " + kind;
            if (!TestReady(test, true) || HostOwns(test, 'W'))
            {
                return;
            }
            if (!Settings.CivicConstruction)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return;
            }
            try
            {
                Vector3 player = PlayerHelper.GetPosition();
                int day = TimeHelper.CurrentDay;
                CityBuildingController place = CivicCity.NearestBuilding(player, 0f, 70f);
                if (place == null)
                {
                    TestRefusals.Tell(Logger, test, TestRefusal.NothingFits, 0, "no building with a street door within 70 m");
                    return;
                }
                SiteState existing = Works.Nearest(place.entranceDoors[0].doorTransform.position, 2f);
                SiteState site = existing != null && existing.Kind == kind ? existing : null;
                if (site == null && existing != null)
                {
                    Works.State.Sites.Remove(existing);   // another kind of site at this building: the test replaces it
                }
                bool started = false;
                if (site == null)
                {
                    site = Works.StartAt(kind, place, day, false, 0f);
                    started = true;
                }
                if (site == null)
                {
                    TestRefusals.Tell(Logger, test, TestRefusal.NothingFits);
                    return;
                }
                if (_sites.DressNow(site, day))
                {
                    _worldOut = true;
                    CivicText.Toast(NotificationType.Success, "reallife_civic_test_site", "project", CivicWorks.ProjectName(site), "address", CivicWorks.PlaceText(site), 9f);
                    return;
                }
                if (started)
                {
                    Works.State.Sites.Remove(site);
                }
                Logger?.Info(test + " test: not started (" + _sites.LastRefusal + ").");
                Notifications.Show(NotificationType.Warning, RefusalKey(_sites.LastRefusal), null, 9f, null, null, true, false);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private static string RefusalKey(SiteRefusal why)
        {
            switch (why)
            {
                case SiteRefusal.NoLane: return "reallife_civic_site_refused_lane";
                case SiteRefusal.NoKerb: return "reallife_civic_site_refused_kerb";
                case SiteRefusal.Vehicles: return "reallife_civic_site_refused_vehicles";
                case SiteRefusal.NoWall: return "reallife_civic_site_refused_wall";
                case SiteRefusal.NoRoom: return "reallife_civic_site_refused_room";
                case SiteRefusal.CannotClose: return "reallife_civic_site_refused_close";
                default: return "reallife_civic_site_refused_pavement";
            }
        }

        /// <summary>Test: every site does a full working day now, and what stands in the street follows.</summary>
        public void TestAdvance()
        {
            if (!TestReady("Civic advance", false) || HostOwns("Civic advance", 'W'))
            {
                return;
            }
            try
            {
                int before = Works.State.Sites.Count;
                Works.AdvanceDay(TimeHelper.CurrentDay);
                if (!_worldFailed)
                {
                    _sites.RefreshNow(TimeHelper.CurrentDay);
                }
                CivicText.Toast(NotificationType.Info, "reallife_civic_test_advanced", "count", before.ToString(), "left", Works.State.Sites.Count.ToString(), 8f);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Test: a rally gathers near the player now (a campaign is started first if there is none).</summary>
        public void TestRally()
        {
            if (!TestReady("Civic rally", true))
            {
                return;
            }
            if (!Settings.CivicElections)
            {
                TestRefusals.Tell(Logger, "Civic rally", TestRefusal.FeatureOff);
                return;
            }
            if (!Government.Campaigning && HostOwns("Civic rally", 'G'))
            {
                return;   // a campaign is the host's to start
            }
            try
            {
                if (CivicBridges.SceneOn())
                {
                    TestRefusals.Tell(Logger, "Civic rally", TestRefusal.SceneActive);
                    return;
                }
                if (CivicBridges.WorkStopped())
                {
                    TestRefusals.Tell(Logger, "Civic rally", TestRefusal.NothingFits, 0, "it is raining: nobody holds a rally in the rain");
                    return;
                }
                if (!Government.Campaigning)
                {
                    Government.TestCampaign(TimeHelper.CurrentDay);
                }
                _campaign.ForceRally();
                _worldOut = true;
                CivicText.Toast(NotificationType.Info, "reallife_civic_test_rally_started", 9f);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private bool ElectionTestReady(string test, bool office)
        {
            if (!TestReady(test, false) || HostOwns(test, 'G'))
            {
                return false;
            }
            if (!Settings.CivicElections || (office && !Settings.CivicOffice))
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            return true;
        }

        /// <summary>Test: the player is a candidate for a race now (a campaign is started if none is on), papers filed, a platform drawn for them.</summary>
        public void TestStand(OfficeRace race)
        {
            string test = "Civic stand for " + race;
            if (!ElectionTestReady(test, true))
            {
                return;
            }
            try
            {
                CivicText.Toast(NotificationType.Info, Office.TestFile(race, TimeHelper.CurrentDay), 9f);
                Logger?.Info("Civic test: the player is a candidate for " + race + " in the election of day " + Government.State.ElectionDay + ".");
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Test: the player holds an office now, as if just elected; the council meets today.</summary>
        public void TestOffice(OfficeRace race)
        {
            string test = "Civic office " + race;
            if (!ElectionTestReady(test, true))
            {
                return;
            }
            try
            {
                Office.TestOffice(race, TimeHelper.CurrentDay);
                OnLevers();
                CivicText.Toast(NotificationType.Info, race == OfficeRace.Mayor ? "reallife_civic_office_test_mayor" : "reallife_civic_office_test_council", 9f);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Test: today's scene of the player's run or office now (the debate while a candidate, else a council meeting while in office).</summary>
        public void TestScene()
        {
            if (!ElectionTestReady("Civic office scene", true))
            {
                return;
            }
            try
            {
                if (_scenes.Active || !OfficeScenes.Free())
                {
                    TestRefusals.Tell(Logger, "Civic office scene", TestRefusal.ScreenBusy);
                    return;
                }
                int day = TimeHelper.CurrentDay;
                if (Office.Campaigning)
                {
                    Government.State.ElectionDay = day + CivicCatalogue.DebateBefore;
                    Office.State.ForDay = Government.State.ElectionDay;
                    Office.State.Debate = 0;
                    _scenes.Tick(day, 19);
                }
                else if (Office.State.InOffice)
                {
                    Office.State.NextMeeting = day;
                    _scenes.Tick(day, OfficeRules.MeetingHour);
                }
                else
                {
                    TestRefusals.Tell(Logger, "Civic office scene", TestRefusal.NothingFits, 0, "the player is neither a candidate nor in office");
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Test: the street is cleared of everything civic and the construction programme is emptied (it fills again within the hour).</summary>
        public void TestClear()
        {
            if (!_loaded)
            {
                TestRefusals.Tell(Logger, "Civic clear", TestRefusal.NotLoaded);
                return;
            }
            if (HostOwns("Civic clear", 'W'))
            {
                return;
            }
            try
            {
                Works.Clear();
                ClearWorld();
                CivicText.Toast(NotificationType.Info, "reallife_civic_test_cleared", 8f);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }
    }
}
