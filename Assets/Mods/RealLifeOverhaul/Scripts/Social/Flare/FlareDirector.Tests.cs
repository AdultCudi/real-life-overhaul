using System;
using UI.Notification;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The test tools of the options page: a draft of every kind now, the daily round now, followers enough to reach the
    /// next tier, and the account started again. Each says in a notice why not when it cannot run.
    /// </summary>
    public sealed partial class FlareDirector
    {
        private bool _testViral;   // the next post goes viral (a test)

        private bool TestReady(string test)
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Running || _file == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            return true;
        }

        // A test tool runs here: a fault in one switches Flare off for the session like any other, and never reaches the options page.
        private void Guarded(Action test)
        {
            try
            {
                test();
            }
            catch (Exception e)
            {
                Fault("a test tool", e);
            }
        }

        /// <summary>Test: a draft of every kind, as if each had just happened (the first friend met is in the outing and the selfie).</summary>
        public void TestDrafts()
        {
            if (!TestReady("Flare drafts"))
            {
                return;
            }
            Guarded(() =>
            {
                ReadPeople();
                int day = Save.Day, hour = Save.Hour, friend = _people.Count > 0 ? _people[0].Index : -1;
                var shops = FlareGame.Businesses();
                string place = shops.Count > 0 ? FlareGame.Place(shops[0]) : null, name = shops.Count > 0 ? FlareGame.Name(shops[0]) : FlareText.T("reallife_flare_test_shop");
                _file.Drafts.Clear();
                FlareRules.Draft(_file, FlareSource.Selfie, day, hour, friend, place, "RealLifeOverhaul_Selfie_test.png", 0);
                FlareRules.Draft(_file, FlareSource.Outing, day, hour, friend, null, null, 1);
                FlareRules.Draft(_file, FlareSource.Promotion, day, hour, -1, null, FlareText.T("reallife_flare_test_employer"), 1);
                FlareRules.Draft(_file, FlareSource.Opened, day, hour, -1, place, name, 0);
                FlareRules.Draft(_file, FlareSource.RecordDay, day, hour, -1, place, name, 0);
                FlareRules.Draft(_file, FlareSource.Voted, day, hour, -1, null, FlareText.T("reallife_flare_test_candidate"), 0);
                FlareRules.Draft(_file, FlareSource.GoodDeed, day, hour, -1, null, null, 0);
                FlareRules.Draft(_file, FlareSource.Arrested, day, hour, -1, null, null, 0);
                _file.LastPostMinute = -1;   // the test may post at once
                _file.PostsToday = 0;
                Logger?.Info("Flare: test drafts made (" + _file.Drafts.Count + ", friend " + (friend >= 0 ? PersonName(friend) : "none") + ", business " + (place ?? "none") + ").");
                Changed();
                Open(TabPost);
            });
        }

        /// <summary>Test: Flare's daily round now, as at the start of a new day.</summary>
        public void TestDay()
        {
            if (!TestReady("Flare day"))
            {
                return;
            }
            Guarded(() =>
            {
                _file.SettledDay = Save.Day - 1;
                PlayDay(Save.Day);
                Tell(NotificationType.Info, "reallife_flare_test_day_done", _openFeed, "followers", FlareText.N(_file.Followers));
            });
        }

        /// <summary>Test: followers enough for the next tier.</summary>
        public void TestFollowers()
        {
            if (!TestReady("Flare followers"))
            {
                return;
            }
            Guarded(() =>
            {
                int next = FlareRules.NextTierAt(_file.Followers);
                int before = _file.Followers;
                _file.Followers = next > 0 ? next : _file.Followers + 10000;
                Logger?.Info("Flare: test followers " + before + " to " + _file.Followers + " (" + _file.Tier + ").");
                Tell(NotificationType.Info, "reallife_flare_test_followers_done", _openFeed, "followers", FlareText.N(_file.Followers), "tier", FlareText.Tier(_file.Tier));
                Changed();
            });
        }

        /// <summary>Test: the next post goes viral, with all that comes of it (the talk, the game's hype, the paper).</summary>
        public void TestViral()
        {
            if (!TestReady("Flare viral"))
            {
                return;
            }
            _testViral = true;
            _file.LastPostMinute = -1;
            Logger?.Info("Flare: test: the next post goes viral.");
            Tell(NotificationType.Info, "reallife_flare_test_viral_done", null);
        }

        /// <summary>Test: the account starts again, as for a save that has never had Flare.</summary>
        public void TestBack()
        {
            if (!TestReady("Flare start again"))
            {
                return;
            }
            Guarded(() =>
            {
                Save.modData.Remove(FlareStore.AccountKey);
                Save.modData.Remove(FlareStore.PostsKey);
                Forget();
                Tell(NotificationType.Info, "reallife_flare_test_back_done", null);
            });
        }
    }
}
