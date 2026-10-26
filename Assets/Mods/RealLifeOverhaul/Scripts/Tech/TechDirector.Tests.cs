using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The test tools of the options page: each starts one thing now on the player's own people, for real.</summary>
    public sealed partial class TechDirector
    {
        // The most senior (or junior) professional with no notice running; for a rival's offer, also with no other rival's
        // offer open to them (the game's own, or the company's): one at a time.
        private Person TestPerson(bool senior, bool offer = false)
        {
            if (!_peopleOn || Save == null)
            {
                Notifications.ShowError("reallife_talent_test_off");
                return null;
            }
            ReadPeople(Save.Day);
            Person best = null;
            for (int i = 0; i < _people.Count; i++)
            {
                Person p = _people[i];
                if (offer && (Held(p.File) || (p.Game != null && TalentGame.GameOffer(p.Game))))
                {
                    continue;
                }
                if (p.File.NoticeEnd == 0 && (best == null || (senior ? p.File.Level > best.File.Level : p.File.Level < best.File.Level)))
                {
                    best = p;
                }
            }
            if (best == null)
            {
                Notifications.ShowError("reallife_talent_test_none");
            }
            return best;
        }

        /// <summary>The review of the most junior professional falls due today, and is given.</summary>
        public void TestReview()
        {
            Person p = TestPerson(false);
            if (p == null)
            {
                return;
            }
            p.File.ReviewDay = Save.Day;
            int rating = TalentRules.Review(p.File, Seed, Save.Day, Year, p.MainPoints);
            Tell(NotificationType.Info, p.File.RaiseAsk > 0 ? "reallife_talent_note_review_ask" : "reallife_talent_note_review",
                Data("name", p.Name, "rating", rating.ToString(), "percent", (p.File.RaiseAsk / 100).ToString()), TabTeam, Row(p.File));
            Changed();
        }

        /// <summary>A rival comes for the most senior professional with no other rival's offer open to them, whatever their level.</summary>
        public void TestOffer()
        {
            Person p = TestPerson(true, true);
            if (p == null)
            {
                return;
            }
            string rival = TechBridges.Rival != null && p.At != null ? TechBridges.Rival(p.At) : null;
            TalentRules.Offer(p.File, string.IsNullOrEmpty(rival) ? TechText.T("reallife_talent_rival_default") : rival, Save.Day);
            Tell(NotificationType.Warning, "reallife_talent_note_poach",
                Data("name", p.Name, "rival", p.File.PoachBy, "percent", (p.File.PoachBp / 100).ToString(), "days", TalentCatalogue.PoachAnswerDays.ToString()),
                TabTeam, Row(p.File));
            Changed();
        }

        /// <summary>The most junior professional finishes a course in their first specialism today.</summary>
        public void TestCourse()
        {
            Person p = TestPerson(false);
            if (p == null)
            {
                return;
            }
            TalentRules.StartCourse(p.File, 0, Save.Day - TalentCatalogue.CourseDays, -1);
            TalentRules.EndCourse(p.File, Seed, Save.Day);
            Tell(NotificationType.Success, "reallife_talent_note_course", Data("name", p.Name), TabTeam, Row(p.File));
            Changed();
        }

        /// <summary>Three applicants for the newest open ad, from the seekers who could do the job.</summary>
        public void TestApplicants()
        {
            if (!_hiringOn || Save == null)
            {
                Notifications.ShowError("reallife_hire_test_off");
                return;
            }
            JobAd ad = null;
            for (int i = _hiring.Ads.Count - 1; i >= 0 && ad == null; i--)
            {
                ad = !_hiring.Ads[i].Search && _hiring.Ads[i].Open(Save.Day) ? _hiring.Ads[i] : null;
            }
            if (ad == null)
            {
                Notifications.ShowError("reallife_hire_test_no_ad");
                return;
            }
            int made = 0;
            for (int i = 0; i < _hiring.Seekers.Count && made < 3; i++)
            {
                Seeker s = _hiring.Seekers[i];
                if (!s.Taken && !s.Passive && HiringRules.Matches(s, ad))
                {
                    _hiring.Applicants.Add(new Applicant { Id = _hiring.Next(), Seeker = s.Id, Ad = ad.Id, Day = Save.Day, Interest = HiringRules.Interest(s, ad) });
                    ad.Came++;
                    made++;
                }
            }
            for (; made < 3; made++)
            {
                Seeker s = HiringRules.NewSeeker(_hiring.Seed, _hiring.Next(), Save.Day, false, ad.Role, ad.Level, _rate,
                    ShortageOf(TalentCatalogue.Role(ad.Role).Family), _districts.Count > 0 ? _districts[0] : string.Empty);
                HiringGame.Name(s, NameSet());
                _hiring.Seekers.Add(s);
                _hiring.Applicants.Add(new Applicant { Id = _hiring.Next(), Seeker = s.Id, Ad = ad.Id, Day = Save.Day, Interest = HiringRules.Interest(s, ad) });
                ad.Came++;
            }
            Tell(NotificationType.Info, "reallife_hire_note_applied", Data("count", "3"), TabApplicants, null);
            Changed();
        }

        /// <summary>A day of the labour market now: seekers come and go, the rivals post and hire.</summary>
        public void TestMarketDay()
        {
            if (!_hiringOn || Save == null)
            {
                Notifications.ShowError("reallife_hire_test_off");
                return;
            }
            _notes.Clear();
            HiringRules.Day(_hiring, Save.Day, HiringGame.Cycle, HiringGame.Summer(Save.Day), _rivals, _rate, _districts, _notes);
            NameSeekers();
            for (int i = 0; i < _notes.Count; i++)
            {
                Said(_notes[i]);
            }
            Notifications.Show(NotificationType.Info,
                "reallife_hire_note_market_day", Data("count", HiringRules.Open(_hiring).ToString(), "rivals", _hiring.RivalAds.Count.ToString()), 8f, null, null, false, false);
            Changed();
        }

        /// <summary>The oldest search still looking delivers its candidates now.</summary>
        public void TestSearch()
        {
            if (!_hiringOn || Save == null)
            {
                Notifications.ShowError("reallife_hire_test_off");
                return;
            }
            for (int i = 0; i < _hiring.Ads.Count; i++)
            {
                JobAd ad = _hiring.Ads[i];
                if (ad.Search && !ad.CrewSearch && ad.Came == 0)
                {
                    ad.Due = Save.Day;
                    HiringRules.Deliver(_hiring, ad, Save.Day, _rate, ShortageOf(TalentCatalogue.Role(ad.Role).Family), _districts);
                    NameSeekers();
                    Tell(NotificationType.Success, "reallife_hire_note_search", Data("role", TechText.Role(ad.Role), "count", HiringCatalogue.SearchCandidates.ToString()), TabApplicants, null);
                    Changed();
                    return;
                }
            }
            Notifications.ShowError("reallife_hire_test_no_search");
        }

        /// <summary>
        /// Test: the oldest data centre crew search not yet delivered delivers now, by the day's own rule. PASS when it brings the
        /// candidates its brief asks for, of its role, junior to staff level, shortlisted for it, and nothing is charged.
        /// </summary>
        public void TestCrewSearch()
        {
            if (!_hiringOn || Save == null)
            {
                Notifications.ShowError("reallife_hire_test_off");
                return;
            }
            for (int i = 0; i < _hiring.Ads.Count; i++)
            {
                JobAd ad = _hiring.Ads[i];
                if (!ad.CrewSearch || ad.Came > 0)
                {
                    continue;
                }
                int paid = ad.FeePaid, before = _hiring.Applicants.Count;
                ad.Due = Save.Day;
                int found = HiringRules.DeliverCrew(_hiring, ad, Save.Day, _rate, ShortageOf(TalentCatalogue.Role(ad.Role).Family), _districts);
                NameSeekers();
                int fit = 0;
                for (int k = before; k < _hiring.Applicants.Count; k++)
                {
                    Applicant a = _hiring.Applicants[k];
                    Seeker s = _hiring.SeekerOf(a.Seeker);
                    fit += a.Ad == ad.Id && a.Stage == (int)ApplyStage.Shortlisted && s != null && s.P.Role == ad.Role && s.P.Level >= HiringCatalogue.CrewLeastLevel
                           && s.P.Level <= HiringCatalogue.CrewMostLevel && s.Passive ? 1 : 0;
                }
                bool ok = found == HiringRules.CrewCandidates(ad.Crew) && fit == found && ad.FeePaid == paid && ad.Hired == 0;
                Logger?.Info("Tech check (a crew search for " + ad.Crew + " at " + ad.Place + "): " + found + " candidate(s) of " + HiringRules.CrewCandidates(ad.Crew) + " wanted, "
                             + fit + " of the role and level shortlisted for it, nothing more charged (" + paid + " to " + ad.FeePaid + " cents): " + (ok ? "PASS" : "FAIL"));
                Notifications.Show(ok ? NotificationType.Success : NotificationType.Error, ok ? "reallife_crew_test_pass" : "reallife_crew_test_fail", null, 10f);
                Changed();
                return;
            }
            Notifications.ShowError("reallife_crew_test_none");
        }

        /// <summary>
        /// "Tech firms: connection speed and work": the curve through the firms' rules for every kind of firm (a slower line
        /// never works more, the usual plan works at the usual pace, a day's points scale by exactly the share and selling is
        /// left alone), and the player's first firm worked out now as its morning is, against its office's plan read afresh.
        /// </summary>
        public void TestNet()
        {
            if (!_firmsOn || Save == null)
            {
                Notifications.ShowError("reallife_tech_test_off");
                return;
            }
            bool curve = true;
            for (int kind = 0; kind < (int)FirmKind.Count; kind++)
            {
                int last = 0;
                for (int t = 0; t < NetSpeedRules.Floors.Length; t++)
                {
                    int net = FirmsRules.Connection(kind, NetSpeedRules.Floors[t]);
                    var c = new Crew { Engineering = 1000, Design = 500, Web = 200, Testing = 300, Selling = 77, Support = 3 };
                    FirmsRules.Connected(ref c, net);
                    curve &= net >= last && net >= NetSpeedRules.Least && net <= NetSpeedRules.Most && c.Engineering == net && c.Design == 500 * net / 1000
                             && c.Web == 200 * net / 1000 && c.Testing == 300 * net / 1000 && c.Selling == 77 && c.Support == 3;
                    last = net;
                }
                curve &= FirmsRules.Connection(kind, NetSpeedRules.Floors[NetSpeedRules.UsualTier]) == 1000 && FirmsRules.Connection(kind, 0) == 1000
                         && FirmsRules.Connection(kind, NetSpeedRules.Unknown) == 1000 && FirmsRules.Connection(kind, NetSpeedRules.Grace) == 1000;
            }
            bool live = true;
            Dictionary<string, string> data = null;
            string firmLine = "no firm of the player's";
            if (_firms.Count > 0)
            {
                Crews();   // the morning's own step, so what it read is what is checked
                FirmPlace firm = _firms[0];
                int mbps = TechBridges.PlanMbpsAt(firm.Reg), net = FirmsRules.Connection(firm.File.Kind, mbps);
                live = firm.Mbps == mbps && firm.Net == net;
                firmLine = firm.File.Name + " (kind " + firm.File.Kind + "): the plan reads " + mbps + " Mbps, the day's step read " + firm.Mbps + " Mbps and works at " + firm.Net
                           + " thousandths, " + net + " wanted";
                data = Data("name", firm.File.Name, "mbps", mbps > 0 ? mbps.ToString() : "-", "work", TechText.Mille(firm.Net));
            }
            bool ok = curve && live;
            Logger?.Info("Tech check (connection speed and work): every kind of firm along the tiers " + (curve ? "slower never more, the usual plan at 1000, points scaled exactly"
                         : "NOT as the curve says") + "; " + firmLine + ": " + (ok ? "PASS" : "FAIL"));
            Notifications.Show(ok ? NotificationType.Success : NotificationType.Error, !ok ? "reallife_tech_test_net_fail" : data != null ? "reallife_tech_test_net_pass"
                : "reallife_tech_test_net_pass_none", data, 10f);
            Changed();
        }

        private Product TestLive()
        {
            if (!_firmsOn || Save == null)
            {
                Notifications.ShowError("reallife_tech_test_off");
                return null;
            }
            for (int i = 0; i < _firmsFile.Products.Count; i++)
            {
                if (_firmsFile.Products[i].Live)
                {
                    return _firmsFile.Products[i];
                }
            }
            Notifications.ShowError("reallife_tech_test_no_product");
            return null;
        }

        /// <summary>A client offers the first firm that takes contracts a piece of work.</summary>
        public void TestContractOffer()
        {
            if (!_firmsOn || Save == null)
            {
                Notifications.ShowError("reallife_tech_test_off");
                return;
            }
            ReadFirms(Save.Day);
            for (int i = 0; i < _firms.Count; i++)
            {
                Firm firm = _firms[i].File;
                int kinds = FirmsCatalogue.TakesWork[firm.Kind];
                for (int k = 0; k < (int)WorkKind.Count; k++)
                {
                    if ((kinds & (1 << k)) == 0)
                    {
                        continue;
                    }
                    var w = new Contract { Id = _firmsFile.Next(), Kind = k, Stage = (int)WorkStage.Offered, End = Save.Day + FirmsCatalogue.OfferDays, Place = firm.Place,
                        Client = FirmsCatalogue.Clients[0], Points = k == (int)WorkKind.Managed ? 0 : FirmsCatalogue.WorkLeast[k] };
                    w.Value = FirmsRules.ValueOf((WorkKind)k, w.Points, false);
                    _firmsFile.Contracts.Add(w);
                    Worked(new WorkNote { Contract = w.Id, Kind = FirmsRules.NoteOffered });
                    ChangedTech();
                    return;
                }
            }
            Notifications.ShowError("reallife_tech_test_no_taker");
        }

        /// <summary>The council puts a job out to tender now, for a firm of the player's that takes on its work.</summary>
        public void TestTender()
        {
            if (!_firmsOn || Save == null)
            {
                Notifications.ShowError("reallife_tech_test_off");
                return;
            }
            ReadFirms(Save.Day);
            bool integration = false, managed = false;
            for (int i = 0; i < _firms.Count; i++)
            {
                integration |= FirmsRules.Takes(_firms[i].File.K, WorkKind.Integration);
                managed |= FirmsRules.Takes(_firms[i].File.K, WorkKind.Managed);
            }
            if (!integration && !managed)
            {
                Notifications.ShowError("reallife_tech_test_no_taker");
                return;
            }
            WorkKind kind = integration ? WorkKind.Integration : WorkKind.Managed;
            var t = new Contract
            {
                Id = _firmsFile.Next(), Kind = (int)kind, Client = FirmsCatalogue.Council, Council = true, Stage = (int)WorkStage.Tender, End = Save.Day + FirmsCatalogue.TenderDays,
                Points = kind == WorkKind.Managed ? 0 : FirmsCatalogue.WorkLeast[(int)kind], Rivals = 3
            };
            t.Value = FirmsRules.ValueOf(kind, t.Points, true);
            _firmsFile.Contracts.Add(t);
            Worked(new WorkNote { Contract = t.Id, Kind = FirmsRules.NoteTender });
            ChangedTech();
        }

        /// <summary>The first product on sale has an outage now.</summary>
        public void TestOutage()
        {
            Product p = TestLive();
            FirmPlace firm = p != null ? FirmOf(p.Place) : null;
            if (firm == null)
            {
                return;
            }
            long gone = p.Users * FirmsCatalogue.OutageChurn / 1000;
            p.Users -= gone;
            firm.File.Outages++;
            firm.File.Reputation = System.Math.Max(0, firm.File.Reputation - 30);
            TellTech(NotificationType.Warning, "reallife_tech_note_outage", Data("product", p.Name, "lost", gone.ToString()), TabProducts, TechApp.ProductRow + p.Id);
            ChangedTech();
        }

        /// <summary>The first product on sale is breached now: the fine, the insurer, the paper and, for a firm off the register, the court.</summary>
        public void TestBreach()
        {
            Product p = TestLive();
            FirmPlace firm = p != null ? FirmOf(p.Place) : null;
            if (firm == null)
            {
                return;
            }
            var r = new ProductDay { Breach = true, Affected = p.Users + p.Free };
            r.Fine = FirmsRules.Fine(r.Affected, firm.File.Revenue30 * 12, Save.Day < firm.File.RegisteredUntil);
            firm.File.Breaches++;
            Breach(firm, p, r, Save.Day);
            ChangedTech();
        }

        private bool TestVentures()
        {
            if (!_ventureOn || Save == null)
            {
                Notifications.ShowError("reallife_vc_test_off");
                return false;
            }
            return true;
        }

        /// <summary>The first startup that is not raising starts its raise and comes to the player.</summary>
        public void TestPitch()
        {
            if (!TestVentures())
            {
                return;
            }
            for (int i = 0; i < _venture.Startups.Count; i++)
            {
                Startup s = _venture.Startups[i];
                if (s.Alive && s.Ask == null && s.Stage < (int)RoundStage.Growth)
                {
                    int mood = VentureRules.Mood(HiringGame.Cycle);
                    s.State = (int)StartupState.Raising;
                    s.LastRound = Save.Day;
                    long round = VentureRules.RoundSize(s.Stage + 1);
                    s.Ask = new TermSheet { Amount = System.Math.Max(VentureCatalogue.TicketLeast, round * VentureCatalogue.AskPlayerShare / 1000),
                        Pre = VentureRules.PreMoney(s.Mrr, VentureRules.Growth(s, mood, HypeNow), mood, s.Stage + 1),
                        Lapse = Save.Day + 7, ProRata = true };
                    Heard(new VentureNote { Kind = VentureRules.NotePitch, Startup = s.Id, Amount = s.Ask.Amount }, Save.Day);
                    ChangedTech();
                    return;
                }
            }
            Notifications.ShowError("reallife_vc_test_none");
        }

        /// <summary>The first startup the player holds is bought now at four times its recurring revenue a year (at least half its value).</summary>
        public void TestSale()
        {
            if (!TestVentures())
            {
                return;
            }
            for (int i = 0; i < _venture.Startups.Count; i++)
            {
                Startup s = _venture.Startups[i];
                if (s.Alive && Holds(s))
                {
                    _ventureNotes.Clear();
                    VentureRules.Sell(_venture, s, System.Math.Max(s.Mrr * 12 * 4, s.Post / 2), VentureRules.NoteAcquired, _ventureNotes);
                    for (int n = 0; n < _ventureNotes.Count; n++)
                    {
                        Heard(_ventureNotes[n], Save.Day);
                    }
                    ChangedTech();
                    return;
                }
            }
            Notifications.ShowError("reallife_vc_test_no_holding");
        }

        /// <summary>
        /// The first startup the player holds raises its next round now, a venture firm leading all of it, so the follow-on it offers
        /// can be seen at once: taken once, then gone for that round.
        /// </summary>
        public void TestRound()
        {
            if (!TestVentures())
            {
                return;
            }
            for (int i = 0; i < _venture.Startups.Count; i++)
            {
                Startup s = _venture.Startups[i];
                int next = s.Stage + 1;
                int lead = VentureRules.AnyLead(next);
                if (s.Alive && Holds(s) && s.Stage < (int)RoundStage.Growth && lead >= 0)
                {
                    int mood = VentureRules.Mood(HiringGame.Cycle);
                    long pre = System.Math.Max(s.Post, VentureRules.PreMoney(s.Mrr, VentureRules.Growth(s, mood, HypeNow), mood, next));
                    _ventureNotes.Clear();
                    VentureRules.Led(s, lead, pre, VentureRules.RoundSize(next), Save.Day, _ventureNotes);
                    for (int n = 0; n < _ventureNotes.Count; n++)
                    {
                        Heard(_ventureNotes[n], Save.Day);
                    }
                    ChangedTech();
                    return;
                }
            }
            Notifications.ShowError("reallife_vc_test_no_holding");
        }

        /// <summary>The board of the first firm of the player's with investors looks at its target now.</summary>
        public void TestBoard()
        {
            if (!TestVentures())
            {
                return;
            }
            for (int i = 0; i < _venture.Caps.Count; i++)
            {
                Cap c = _venture.Caps[i];
                if (c.VcSeats > 0)
                {
                    c.TargetDay = Save.Day;
                    _venture.LastDay = Save.Day - 1;
                    CapsDay(Save.Day, VentureRules.Mood(HiringGame.Cycle));
                    _venture.LastDay = Save.Day;
                    ChangedTech();
                    return;
                }
            }
            Notifications.ShowError("reallife_vc_test_no_board");
        }

        /// <summary>The first data centre goes down now.</summary>
        public void TestCentreDown()
        {
            if (!_firmsOn || _infra == null || Save == null || _infra.Centres.Count == 0)
            {
                Notifications.ShowError("reallife_infra_test_none");
                return;
            }
            DataCentre d = _infra.Centres[0];
            d.DownUntil = Save.Day + 1;
            d.Outages++;
            _infra.Cloud.Reputation = System.Math.Max(0, _infra.Cloud.Reputation - 80);
            TellTech(NotificationType.Warning, "reallife_infra_note_outage", Data("name", d.Name), TabInfra, TechApp.CentreRow + d.Place);
            ChangedTech();
        }

        /// <summary>The rival clouds cut their prices now, as they would against a cloud that takes too much of the city.</summary>
        public void TestPriceWar()
        {
            if (!_firmsOn || _infra == null || Save == null || !_infra.Cloud.Open)
            {
                Notifications.ShowError("reallife_infra_test_no_cloud");
                return;
            }
            Cloud c = _infra.Cloud;
            c.LastCut = Save.Day - FirmsCatalogue.MonthDays;
            InfraRules.PriceWar(c, InfraCatalogue.CloudDemand, Save.Day);
            TechBridges.Stock?.Invoke(107, -30);
            TellTech(NotificationType.Info, "reallife_infra_note_price_war", Data("name", c.Name, "price", TechText.Mille(c.RivalPrice)), TabInfra, TechApp.CloudRow);
            ChangedTech();
        }

        private const string TestStreet = "Test Street (Real Life Overhaul)";   // no street of the city's has this name, so no building is found for it

        /// <summary>
        /// A registration whose building the city lacks (as a player's save held, left by another mod) is put in the save's
        /// list for the length of this call and taken out again before the game runs another frame: the tech hiring's
        /// districts and the walks of the player's businesses, offices of tech firms and warehouses read past it.
        /// PASS when none throws and none takes it in. Refused in a Going Public session, where the list is shared.
        /// </summary>
        public void TestOrphanRegistration()
        {
            GameInstance save = Save;
            if (save == null || save.BuildingRegistrations == null || GoingPublic.InSession)
            {
                Notifications.ShowError("reallife_hire_test_orphan_off");
                return;
            }
            var orphan = new BuildingRegistration
            {
                // a tech firm, so every walk below reaches its test of the building
                StreetName = TestStreet, StreetNumber = 1, RentedByPlayer = true, BusinessName = TestStreet, businessTypeName = FirmsGame.WebAgency
            };
            var districts = new List<string>(16);
            var regs = new List<BuildingRegistration>(32);
            string failed = null;
            int taken = 0;
            save.BuildingRegistrations.Add(orphan);
            try
            {
                failed = Walk("the tech hiring's districts", () => HiringGame.Districts(districts)) ?? failed;
                failed = Walk("the player's businesses", () => taken += Holds(regs, orphan, Economy.Books.Businesses)) ?? failed;
                failed = Walk("the tech firms", () => taken += Holds(regs, orphan, FirmsGame.Firms)) ?? failed;
                failed = Walk("the businesses with professionals", () => taken += Holds(regs, orphan, TalentGame.Businesses)) ?? failed;
                failed = Walk("the warehouses for data centres", () => taken += Holds(regs, orphan, InfraGame.Warehouses)) ?? failed;
            }
            finally
            {
                save.BuildingRegistrations.Remove(orphan);   // taken out on every path: the game never sees it
            }
            bool ok = failed == null && taken == 0 && GameRegistrations.WasTold(orphan);
            Logger?.Info("Buildings check (a registration with no building, at " + TestStreet + "): " + districts.Count + " district(s) read for hiring; "
                         + (failed != null ? failed + " threw" : "no walk threw") + "; " + taken + " walk(s) took it in; the warning " + (GameRegistrations.WasTold(orphan) ? "given" : "not given")
                         + "; the save's list as before: " + (ok ? "PASS" : "FAIL"));
            Notifications.Show(ok ? NotificationType.Success : NotificationType.Error, ok ? "reallife_hire_test_orphan_pass" : "reallife_hire_test_orphan_fail", null, 10f);
        }

        // One walk of the test: its name if it threw, else null. The fault is logged; nothing is switched off for a test.
        private static string Walk(string name, Action walk)
        {
            try
            {
                walk();
                return null;
            }
            catch (Exception e)
            {
                Logger?.Warn("Buildings check: " + name + " threw on a registration with no building: " + e);
                return name;
            }
        }

        // 1 if the walk put the registration with no building in its list, else 0.
        private static int Holds(List<BuildingRegistration> into, BuildingRegistration orphan, Action<List<BuildingRegistration>> walk)
        {
            walk(into);
            int n = into.Contains(orphan) ? 1 : 0;
            into.Clear();
            return n;
        }
    }
}
