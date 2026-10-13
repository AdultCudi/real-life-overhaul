using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The test tools of the options page: each starts one thing now on the player's own people, so it can be seen
    /// without waiting for it. What they change is real (a review given, an offer made, a course ended), as it would be
    /// had the day come.
    /// </summary>
    public sealed partial class TechDirector
    {
        private Person TestPerson(bool senior)
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

        /// <summary>A rival comes for the most senior professional, whatever their level.</summary>
        public void TestOffer()
        {
            Person p = TestPerson(true);
            if (p == null)
            {
                return;
            }
            string rival = TechBridges.Rival != null && p.At != null ? TechBridges.Rival(p.At) : null;
            TalentRules.Offer(p.File, string.IsNullOrEmpty(rival) ? TechText.T("reallife_talent_rival_default") : rival, Save.Day);
            Tell(NotificationType.Warning, "reallife_talent_note_poach", Data("name", p.Name, "rival", p.File.PoachBy, "percent", (p.File.PoachBp / 100).ToString(), "days", TalentCatalogue.PoachAnswerDays.ToString()),
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
                Seeker s = HiringRules.NewSeeker(_hiring.Seed, _hiring.Next(), Save.Day, false, ad.Role, ad.Level, _rate, ShortageOf(TalentCatalogue.Role(ad.Role).Family), _districts.Count > 0 ? _districts[0] : string.Empty);
                HiringGame.Name(s);
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
            Notifications.Show(NotificationType.Info, "reallife_hire_note_market_day", Data("count", HiringRules.Open(_hiring).ToString(), "rivals", _hiring.RivalAds.Count.ToString()), 8f, null, null, false, false);
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
                if (ad.Search && ad.Came == 0)
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
            Tell2(NotificationType.Warning, "reallife_tech_note_outage", Data("product", p.Name, "lost", gone.ToString()), TabProducts, TechApp.ProductRow + p.Id);
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
                    s.Ask = new TermSheet { Amount = System.Math.Max(VentureCatalogue.TicketLeast, round * VentureCatalogue.AskPlayerShare / 1000), Pre = VentureRules.PreMoney(s.Mrr, VentureRules.Growth(s, mood, HypeNow), mood, s.Stage + 1),
                        Lapse = Save.Day + 7, ProRata = true };
                    Heard(new VentureNote { Kind = VentureRules.NotePitch, Startup = s.Id, Amount = s.Ask.Amount }, Save.Day);
                    ChangedVenture();
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
                    ChangedVenture();
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
                    ChangedVenture();
                    return;
                }
            }
            Notifications.ShowError("reallife_vc_test_no_board");
        }
    }
}
