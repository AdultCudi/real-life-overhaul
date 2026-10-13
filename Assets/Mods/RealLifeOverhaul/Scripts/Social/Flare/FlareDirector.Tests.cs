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

        /// <summary>Test: the influencers' month now: growth, stars, quitting, scandals and newcomers.</summary>
        public void TestMonth()
        {
            if (!TestReady("Flare month"))
            {
                return;
            }
            Guarded(() =>
            {
                ReadPeople();
                Month(Save.Day);
                Logger?.Info("Flare: test month: " + CastLine(_file) + ".");
                Tell(NotificationType.Info, "reallife_flare_test_month_done", _openFeedTab, "count", _file.ActiveInfluencers.ToString());
                Changed();
            });
        }

        /// <summary>Test: a scandal now for the influencer the player has hired, or else the first one.</summary>
        public void TestInfluencerScandal()
        {
            if (!TestReady("Flare influencer scandal"))
            {
                return;
            }
            Guarded(() =>
            {
                FlareInfluencer who = null;
                for (int i = 0; i < _file.Hires.Count && who == null; i++)
                {
                    who = _file.Hires[i].On(Save.Day) ? _file.Influencer(_file.Hires[i].Who[0]) : null;
                }
                for (int i = 0; i < _file.Influencers.Count && who == null; i++)
                {
                    who = _file.Influencers[i].Active ? _file.Influencers[i] : null;
                }
                if (who == null)
                {
                    TestRefusals.Tell(Logger, "Flare influencer scandal", TestRefusal.NothingFits);
                    return;
                }
                FlareCast.Scandal(who, Save.Day);
                Scandalised(who, Save.Day);
                Changed();
            });
        }

        /// <summary>Test: a newcomer arrives now, in the niche of the player's first business (or food).</summary>
        public void TestArrival()
        {
            if (!TestReady("Flare new influencer"))
            {
                return;
            }
            Guarded(() =>
            {
                FlareGame.Shops(_file, _shops);
                FlareGame.Districts(_districts);
                FlareNiche niche = _shops.Count > 0 ? _shops[0].Niche : FlareNiche.Food;
                FlareInfluencer inf = FlareCast.New(_file, niche, _shops.Count > 0 ? _shops[0].District : _districts.Count > 0 ? _districts[0] : null, Save.Day, 3000, -1, false);
                CastNews(new FlareCastNews { Kind = FlareCastNews.What.Arrived, Id = inf.Id, After = inf.Followers }, Save.Day);
                Tell(NotificationType.Info, "reallife_flare_test_arrival_done", _openFeedTab, "handle", inf.Handle);
                Changed();
            });
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
                Save.modData.Remove(FlareStore.CastKey);
                Forget();
                Tell(NotificationType.Info, "reallife_flare_test_back_done", null);
            });
        }
    }
}
