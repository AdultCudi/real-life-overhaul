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

        /// <summary>Test: somebody the player has still to meet answers a post now and becomes a contact (met online).</summary>
        public void TestOnline()
        {
            if (!TestReady("Flare met online"))
            {
                return;
            }
            Guarded(() =>
            {
                string name = FlareBridges.MeetOnline != null ? FlareBridges.MeetOnline() : null;
                if (name == null)
                {
                    TestRefusals.Tell(Logger, "Flare met online", TestRefusal.AllMet);
                    return;
                }
                _file.OnlineDay = Save.Day;
                Logger?.Info("Flare: test: " + name + " answered a post and is a new contact, met online.");
            });
        }

        /// <summary>Test: the City Ledger's account posts this morning's headlines now, and the market's news of yesterday is read.</summary>
        public void TestCity()
        {
            if (!TestReady("Flare city accounts"))
            {
                return;
            }
            Guarded(() =>
            {
                _file.LedgerDay = -1;
                CityHour(Save.Day, Math.Max(7, Save.Hour));
                Market(Save.Day);
                Tell(NotificationType.Info, "reallife_flare_test_city_done", _openFeedTab);
                Changed();
            });
        }

        /// <summary>
        /// Test: a report of a broken road where the player stands, and the City Office's answer at once: a crew is sent (a
        /// site of the council's works near the player, with its days), or why not.
        /// </summary>
        public void TestReport()
        {
            if (!TestReady("Flare report"))
            {
                return;
            }
            Guarded(() =>
            {
                if (Following)
                {
                    TestRefusals.Tell(Logger, "Flare report", TestRefusal.NothingFits, 0, "this machine follows a host's feed: the host's City Office answers");
                    return;
                }
                _file.Net.ReportsToday = 0;
                var c = new FlareCompose { Kind = FlareSource.Report, Arg = (int)FlareReport.Road, Text = FlareText.T("reallife_flare_test_report_words") };
                string why;
                if (!Compose(c, out why))
                {
                    Tell(NotificationType.Warning, why ?? "reallife_flare_compose_need_district", null);
                    return;
                }
                FlarePost report = null;
                for (int i = _file.Posts.Count - 1; i >= 0 && report == null; i--)
                {
                    report = _file.Posts[i].Author == FlareAuthor.Player && _file.Posts[i].Source == FlareSource.Report ? _file.Posts[i] : null;
                }
                FlareCase cs = report != null && report.Case >= 0 ? _file.Net.Case(report.Case) : null;
                if (cs != null && cs.State == FlareCaseState.Open)
                {
                    cs.ReplyAt = Now;
                    Answer(cs, Save.Day);
                }
                Logger?.Info("Flare: test report: case " + (cs != null ? cs.Id + " " + cs.State + " " + cs.Order : "none") + ".");
                if (report != null)
                {
                    OpenPost(report.Id);
                }
            });
        }

        /// <summary>Test: the city's posts of today made again now (its people, the official accounts, the businesses, the invitations).</summary>
        public void TestCityDay()
        {
            if (!TestReady("Flare city posts"))
            {
                return;
            }
            Guarded(() =>
            {
                _file.Net.CityDay = -1;
                LiveDay(Save.Day);
                Tell(NotificationType.Info, "reallife_flare_test_cityday_done", _openFeedTab, "count", _made.Count.ToString());
                Changed();
            });
        }

        /// <summary>Test: the reactions to the player's posts of today come in now, as if eight hours had passed.</summary>
        public void TestReactions()
        {
            if (!TestReady("Flare reactions"))
            {
                return;
            }
            Guarded(() =>
            {
                long back = 8 * 60;
                int moved = 0;
                for (int i = 0; i < _file.Posts.Count; i++)
                {
                    FlarePost p = _file.Posts[i];
                    FlarePost parent = p.ReplyTo >= 0 ? _file.Post(p.ReplyTo) : null;
                    bool mine = p.Author == FlareAuthor.Player || (parent != null && parent.Author == FlareAuthor.Player);
                    if (mine && p.Day == Save.Day)
                    {
                        FlareLiveRules.SetAt(p, Math.Max(0, p.At - back));
                        moved++;
                    }
                }
                Logger?.Info("Flare: test reactions: " + moved + " post(s) of today and their replies moved eight hours back.");
                Tell(NotificationType.Info, "reallife_flare_test_reactions_done", _openFeed, "count", moved.ToString());
                Changed();
            });
        }

        /// <summary>Test: a post that goes badly (cheerful about an arrest) and is seen widely: a pile-on, and tomorrow's paper.</summary>
        public void TestPileOn()
        {
            if (!TestReady("Flare pile-on"))
            {
                return;
            }
            Guarded(() =>
            {
                ReadPeople();
                FlareDraft d = FlareRules.Draft(_file, FlareSource.Arrested, Save.Day, Save.Hour, -1, null, null, 0) ?? new FlareDraft { Id = -1, Source = FlareSource.Arrested, Day = Save.Day };
                FlarePost p = FlareRules.Post(_file, d, FlareTone.Cheerful, -1, _people, Save.Day, Save.Hour, FlareGame.MinuteOfDay);
                p.Reach = Math.Max(p.Reach, FlareLiveRules.PileOnFrom * 4);
                p.Minute = FlareGame.MinuteOfDay % 60;
                PostLive(p);
                Logger?.Info("Flare: test pile-on: post " + p.Id + " went badly, seen by " + p.Reach + ".");
                Changed();
                OpenPost(p.Id);
            });
        }

        /// <summary>Test: one of the city's people writes to the player now.</summary>
        public void TestDm()
        {
            if (!TestReady("Flare message"))
            {
                return;
            }
            Guarded(() =>
            {
                string with = "c" + (Save.Day % FlareLiveRules.Citizens);
                FlareLiveRules.Dm(_file.Net, with, AccountName(with), Now, false, null, "reallife_flare_dm_chat", null);
                Recount();
                Tell(NotificationType.Info, "reallife_flare_note_dm", _openMessages, "name", AccountName(with));
                Changed();
            });
        }

        /// <summary>Test: the City Office invites the city to an event in an hour; going to it brings a notification as it starts.</summary>
        public void TestInvite()
        {
            if (!TestReady("Flare invitation"))
            {
                return;
            }
            Guarded(() =>
            {
                int id = _file.NextId++;
                var p = new FlarePost
                {
                    Id = id, Author = FlareAuthor.Official, Who = (int)FlareOfficial.CityOffice, Source = FlareSource.Invite, When = Save.Day * 24 + Save.Hour + 1, Topic = "events",
                    Seed = FlareRules.Roll(_file.Seed, id, 17) * 1000, Reach = 4000, Likes = 120
                };
                FlareLiveRules.SetAt(p, Now);
                _file.Posts.Add(p);
                Shared_Changed(p);
                Changed();
                OpenPost(id);
            });
        }

        /// <summary>Test: three complaints about litter in the player's district this week that nothing was done about: in tomorrow's paper, and the mood falls.</summary>
        public void TestEscalate()
        {
            if (!TestReady("Flare ignored complaints"))
            {
                return;
            }
            Guarded(() =>
            {
                string district = FlareBridges.Ask(FlareBridges.PlayerDistrict, null);
                if (district == null)
                {
                    FlareGame.Districts(_districts);
                    district = _districts.Count > 0 ? _districts[0] : null;
                }
                if (district == null)
                {
                    TestRefusals.Tell(Logger, "Flare ignored complaints", TestRefusal.NothingFits, 0, "no district is known");
                    return;
                }
                for (int i = 0; i < FlareLiveRules.TallyToPaper; i++)
                {
                    FlareLiveRules.Complained(_file.Net, FlareReport.Litter, district, Save.Day, false);
                }
                bool again;
                FlareCase c = FlareLiveRules.File(_file.Net, FlareReport.Litter, district, null, null, 0f, 0f, Now, "c1", -1, _file.Seed, out again);
                Escalation(c, Save.Day);
                Tell(NotificationType.Info, "reallife_flare_test_escalate_done", null, "district", FlareGame.DistrictName(district));
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
                Save.modData.Remove(FlareLiveStore.Key);
                Forget();
                Tell(NotificationType.Info, "reallife_flare_test_back_done", null);
            });
        }
    }
}
