using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Hustles;
using RealLifeOverhaul.Tech;

// Day by day runs of the work feature, through the directors' own code: CareersDirector.People.cs (StandingDay, RecordShift, StandingDecides,
// AlsoApplied, OtherGotIt, CampusEnrolled, CampusWhy, IntakeOf) and HustleDirector.Morning.cs (MorningOf) are linked as they ship and driven in the
// order the directors' day turn drives them; the labour market's runs call its rules as the tech director's day turn does. Each run is 60 to 120
// days from a seed, asserts each new item's state changes as it should and that the NPC side acts, writes the save on day 30, reads it back and plays
// the next 30 days to the same state, and plays an old save (the line before the new fields) on.
internal static partial class Program
{
    private static void WkRuns()
    {
        WkRunStanding();
        WkRunRoster();
        WkRunHiring();
        WkRunMarket();
        WkRunPoolMoves();
    }

    // ------------------------------------------------------------------------------------------------- standing at work

    // A save's day turn on the real director: the colleagues are seeded when the record starts (Standing), a worked day records its shift
    // (RecordShift), the week writes the ring (StandingDay), and a review that would promote lets the better record have the post (StandingDecides).
    private sealed class StandingRun
    {
        public CareersDirector D;
        public StudyFile S;
        public Job J;
        public int Contests, PlayerWins, Passed, Reviews;
        public int Top => J.Def.Ranks.Length - 1;

        public StandingRun(int seed)
        {
            S = new StudyFile { Seed = seed };
            J = new Job { Hired = 5, Pay = 1700 };
            J.Post.Prof = ProCatalogue.All[0].Id;
            J.Post.Street = "Pine";
            J.Post.Number = 3;
            J.Post.Employer = "Hudson Depot";
            J.Post.Rank = 1;
            S.Work.Job = J;
            D = new CareersDirector(S);
            SaveGameManager.Current = new GameInstance { Day = 1 };
        }

        public void Day(int day)
        {
            SaveGameManager.Current.Day = day;
            D.RunStandingDay(day);
            if (day % 7 < 5)
            {
                J.Due++;
                J.Worked++;
                D.RunRecordShift(J, day, 480 - (day % 3) * 60);
            }
            if (day % 28 == 27)
            {
                Reviews++;
                if (J.Post.Rank < Top && J.Worked > 10)
                {
                    Contests++;
                    bool mine = D.RunStandingDecides(J, day);
                    PlayerWins += mine ? 1 : 0;
                    Passed += mine ? 0 : 1;
                    J.Post.Rank += mine ? 1 : 0;
                }
                J.Due = J.Worked = 0;
            }
        }
    }

    private static void WkRunStanding()
    {
        // a player the shift does not get on with is not promoted at a review, whoever else is up for the post; one who is liked is
        var cold = new StandingRun(31);
        for (int day = 1; day <= 60; day++)
        {
            cold.Day(day);
        }
        // a review that promotes promotes, whatever the teamwork: with nobody else up for the post, a player the shift does not get on with is promoted as one it likes
        cold.D.Standing.Peers.RemoveAll(p => p.Role == (int)PeerRole.UpForPost);
        cold.J.Favour = -LifeRules.FavourMost;
        bool coldPromoted = cold.D.RunStandingDecides(cold.J, 61);
        cold.J.Favour = LifeRules.FavourMost;
        bool warmPromoted = cold.D.RunStandingDecides(cold.J, 62);
        Check(coldPromoted && warmPromoted && StandingRules.TeamworkOf(-LifeRules.FavourMost) < 50,
            "work run: StandingDecides promotes a player whose teamwork is lowest as it promotes one whose teamwork is highest, when nobody else is up for the post (no teamwork gate)");
        var a = new StandingRun(31);
        for (int day = 1; day <= 120; day++)
        {
            a.Day(day);
        }
        StandingFile f = a.D.Standing;
        Peer up = f.UpFor();
        Check(f.Count == Peer.RingSize && f.Peers.Count == 3 && up != null && f.Peers.TrueForAll(p => p.Count > 0 && p.Count <= Peer.RingSize),
            "work run: over 120 days, through the director's own StandingDay, the player's ring and every colleague's fill to twelve weeks and the shift's three people stay");
        Check(f.Shifts.Count == StandingRules.ShiftsMost && f.Shifts[f.Shifts.Count - 1].Day >= 116 && f.Shifts[0].Day < f.Shifts[1].Day,
            "work run: RecordShift on each worked day leaves the last seven shifts, the latest, in order");
        Check(a.Reviews == 4 && a.Contests >= 1 && a.PlayerWins + a.Passed == a.Contests && a.D.NamesRolled > 3 && a.D.PassedCount == a.Passed,
            "work run: StandingDecides at each review that would promote gave the post to the better record (" + a.PlayerWins + " to the player, " + a.Passed + " passed over, told) and a new colleague took the place");
        bool moved = false;
        foreach (Peer p in f.Peers)
        {
            moved |= p.Score != p.Skill || p.Ring[0] != p.Ring[Peer.RingSize - 1];
        }
        Check(moved, "work run: the colleagues' weeks move (the NPC side acts) every week of the run");

        // a save on day 30, read back by a new director, plays the next 30 days to the same state as the run that never stopped
        var c = new StandingRun(31);
        var b = new StandingRun(31);
        for (int day = 1; day <= 30; day++)
        {
            c.Day(day);
            b.Day(day);
        }
        var data = new Dictionary<string, string>();
        b.D.RunPeopleWrite(data);
        var restarted = new StandingRun(31);
        restarted.J = b.J;
        restarted.S.Work.Job = b.J;
        restarted.D.NamesRolled = b.D.NamesRolled;   // the game remembers the names it rolled before the save
        for (int day = 31; day <= 60; day++)
        {
            c.Day(day);
            SaveGameManager.Current = new GameInstance { Day = day, modData = data };
            restarted.J.Post.Rank = c.J.Post.Rank;
            restarted.D.RunStandingDay(day);
            if (day % 7 < 5)
            {
                restarted.J.Due++;
                restarted.J.Worked++;
                restarted.D.RunRecordShift(restarted.J, day, 480 - (day % 3) * 60);
            }
            if (day % 28 == 27)
            {
                if (restarted.J.Post.Rank < restarted.Top && restarted.J.Worked > 10)
                {
                    restarted.D.RunStandingDecides(restarted.J, day);
                }
                restarted.J.Due = restarted.J.Worked = 0;
            }
        }
        var after = new Dictionary<string, string>();
        var wanted = new Dictionary<string, string>();
        restarted.D.RunPeopleWrite(after);
        c.D.RunPeopleWrite(wanted);
        SaveGameManager.Current = new GameInstance { Day = 61 };
        Check(after.TryGetValue(StandingStore.Key, out string got) && wanted.TryGetValue(StandingStore.Key, out string want) && got == want,
            "work run: a standing line saved on day 30 and read back by a new director plays the next 30 days to the same state");

        // an old line, from before the recent shifts and the applicants, loads and plays on
        string old = wanted[StandingStore.Key];
        int cut = old.IndexOf("~r:", StringComparison.Ordinal);
        var older = new StandingRun(31);
        older.J = c.J;
        older.S.Work.Job = c.J;
        var oldData = new Dictionary<string, string> { [StandingStore.Key] = cut > 0 ? old.Substring(0, cut) : old };
        for (int day = 61; day <= 90; day++)
        {
            SaveGameManager.Current = new GameInstance { Day = day, modData = oldData };
            older.D.RunStandingDay(day);
            older.D.RunRecordShift(c.J, day, 480);
        }
        StandingFile of = older.D.Standing;
        Check(of != null && of.Shifts.Count > 0 && of.Shifts.Count <= StandingRules.ShiftsMost && of.Count > 0, "work run: a standing line from before the recent shifts loads into the director and plays on");

        // the applicants: made from each post, the same every time, and the employer's weighing through the director is one of them
        var ap = new StandingRun(7);
        SaveGameManager.Current = new GameInstance { Day = 120 };
        int weighed = 0, other = 0, same = 0;
        for (int id = 1; id <= 40; id++)
        {
            var o = new Opening { Id = id, Prof = ap.J.Post.Prof, Rank = 1, From = 100 + id % 9, To = 140 };
            var first = new List<Peer>();
            var second = new List<Peer>();
            ap.D.AlsoApplied(o, first);
            ap.D.AlsoApplied(new Opening { Id = id + 77, From = 105 }, new List<Peer>());   // another post read in between
            ap.D.AlsoApplied(o, second);
            bool alike = first.Count == second.Count;
            for (int k = 0; alike && k < first.Count; k++)
            {
                alike = first[k].Name == second[k].Name && first[k].Score == second[k].Score;
            }
            same += alike ? 1 : 0;
            var application = new Application { Score = 60 + id % 20 };
            application.Post.Id = o.Id;
            application.Post.Prof = o.Prof;
            application.Post.Rank = o.Rank;
            application.Post.From = o.From;
            application.Post.To = o.To;
            string winner = ap.D.RunOtherGotIt(application);
            weighed++;
            if (winner != null)
            {
                other++;
                bool seen = false;
                for (int k = 0; k < first.Count; k++)
                {
                    seen |= first[k].Name == winner;
                }
                Check(seen, "work run: the applicant who got post " + id + " (" + winner + ") was one the player could see among the others");
            }
        }
        Check(same == 40 && weighed == 40 && other >= 1, "work run: forty posts each show the same others on every read, and OtherGotIt named one of them for the post in " + other + " of 40");
    }

    // ------------------------------------------------------------------------------------------------- the campus

    private sealed class CampusRun
    {
        public CareersDirector D;
        public StudyFile S = new StudyFile { Seed = 44 };
        public int Followed, Full, Joined, Refused;
        public readonly CourseDef Business = StudyCatalogue.Find(StudyCatalogue.Business);

        public CampusRun()
        {
            D = new CareersDirector(S);
            D.StaffNames.AddRange(new[] { "Ava Lindqvist", "Tomas Reyes", "Hana Okada" });
            SaveGameManager.Current = new GameInstance { Day = 1 };
        }

        public void Day(int day)
        {
            SaveGameManager.Current.Day = day;
            Intake i = D.IntakeOf(Business);
            D.TutorOf(Business);
            if (day == 20)
            {
                if (D.RunCampusWhy(Business) == null)
                {
                    D.RunCampusEnrolled(Business, day);
                    Joined++;
                }
                else
                {
                    Refused++;
                }
            }
            if (day >= 20 && day < 80 && i.PlayerIn)
            {
                int before = D.FollowedCount;
                D.RunCampusEnrolled(Business, day);   // the director's own press again: a seat if one is left and a follower at most once
                Followed += D.FollowedCount - before;
            }
            Full += RosterRules.Left(i) == 0 ? 1 : 0;
        }
    }

    private static void WkRunRoster()
    {
        var run = new CampusRun();
        int seatsAt20 = 0, seatsAt27 = 0;
        for (int day = 1; day <= 120; day++)
        {
            run.Day(day);
            if (day == 20)
            {
                seatsAt20 = RosterRules.Left(run.D.Roster.Find(StudyCatalogue.Business, RosterRules.StartOf(20)));
            }
            if (day == 27)
            {
                seatsAt27 = RosterRules.Left(run.D.Roster.Find(StudyCatalogue.Business, RosterRules.StartOf(27)));
            }
        }
        RosterFile f = run.D.Roster;
        Check(f.Intakes.Count >= 4 && f.Intakes.TrueForAll(i => i.Students.Count <= i.Capacity) && f.Tutors.Count == 1,
            "campus run: over 120 days, through IntakeOf, a new cohort stands every 28 days, none over its seats, one tutor, and the NPC students are named from the mod's catalogue");
        Check(seatsAt27 <= seatsAt20, "campus run: the others fill seats while a cohort runs, never empty them (" + seatsAt20 + " left on day 20, " + seatsAt27 + " on day 27)");
        Check(run.Joined + run.Refused == 1, "campus run: CampusWhy on day 20 either let the player take a seat or refused for a reason");
        Check(run.Joined == 0 || run.Followed <= 1, "campus run: through CampusEnrolled an employee follows the same cohort at most once");

        // every timetabled course read many times keeps its students: no cohort is made again with other names
        var wide = new CampusRun();
        var first = new Dictionary<int, string>();
        int changed = 0, courses = 0;
        for (int round = 0; round < 5; round++)
        {
            SaveGameManager.Current.Day = 200 + round * 3;
            foreach (CourseDef c in StudyCatalogue.All)
            {
                Intake i = wide.D.IntakeOf(c);
                if (i == null || i.Students.Count == 0)
                {
                    continue;
                }
                if (round == 0)
                {
                    first[c.Id] = i.Students[0].Name;
                    courses++;
                }
                else if (first.TryGetValue(c.Id, out string name) && name != i.Students[0].Name)
                {
                    changed++;
                }
            }
        }
        Check(courses >= 20 && changed == 0 && wide.D.Roster.Intakes.Count >= courses && wide.D.NamesRolled == 0,
            "campus run: " + courses + " timetabled courses read five times through IntakeOf keep their first students and roll no name of the game's (" + changed + " changed)");

        // a save on day 30 read back by a new director plays the next 30 days to the same state
        var c2 = new CampusRun();
        var b = new CampusRun();
        for (int day = 1; day <= 30; day++)
        {
            c2.Day(day);
            b.Day(day);
        }
        var data = new Dictionary<string, string>();
        b.D.RunPeopleWrite(data);
        var again = new CampusRun();
        for (int day = 31; day <= 60; day++)
        {
            c2.Day(day);
            SaveGameManager.Current = new GameInstance { Day = day, modData = data };
            Intake i = again.D.IntakeOf(again.Business);
            again.D.TutorOf(again.Business);
            if (i.PlayerIn)
            {
                again.D.RunCampusEnrolled(again.Business, day);
            }
        }
        var written = new Dictionary<string, string>();
        var wanted = new Dictionary<string, string>();
        again.D.RunPeopleWrite(written);
        c2.D.RunPeopleWrite(wanted);
        SaveGameManager.Current = new GameInstance { Day = 61 };
        Check(written.TryGetValue(RosterStore.Key, out string wrote) && wanted.TryGetValue(RosterStore.Key, out string want) && wrote == want,
            "campus run: a roster saved on day 30 and read back by a new director plays the next 30 days to the same state");
        string line = wanted[RosterStore.Key];
        int t = line.IndexOf("~t:", StringComparison.Ordinal);
        var old = new CampusRun();
        for (int day = 61; day <= 90; day++)
        {
            SaveGameManager.Current = new GameInstance { Day = day, modData = new Dictionary<string, string> { [RosterStore.Key] = t > 0 ? line.Substring(0, t) : line } };
            old.D.IntakeOf(old.Business);
            old.D.TutorOf(old.Business);
        }
        Check(old.D.Roster.Tutors.Count == 1 && old.D.Roster.Intakes.Count >= 1, "campus run: a roster line without its tutors loads and plays on, the tutor made again");
        // marks over the run: a transcript across quarters gives an average, a rank and a class
        var marks = new List<Mark> { new Mark { Credits = 6, Points = 400, Start = 5 }, new Mark { Credits = 4, Points = 300, Start = 40 }, new Mark { Credits = 6, Points = 200, Start = 100 } };
        var q = new List<int>();
        var avg = new List<int>();
        Intake last2 = run.D.Roster.Find(StudyCatalogue.Business, RosterRules.StartOf(20));
        int gpa = RosterRules.Gpa(marks);
        var lines = new List<ClassLine>();
        int rank = RosterRules.Rank(last2, "You", gpa, lines);
        Check(RosterRules.ByTerm(marks, 60, q, avg) == 3 && rank >= 1 && rank <= lines.Count && lines.Count == last2.Students.Count + 1,
            "campus run: the transcript groups by quarter and ranks the player among the run's own classmates");
    }

    // ------------------------------------------------------------------------------------------------- the labour market

    private sealed class HiringRun
    {
        public HiringFile F;
        public int Offered, Beaten, Hired;
        public bool IgnoreRivals;   // the player lets a rival's open offer pass

        public void Day(int day)
        {
            HiringRules.Day(F, day, day % 4, false, HiringCatalogue.RivalFirms, (role, points) => 2000 + points * 30, new[] { "d1", "d2" }, null);
            HiringRules.Apply(F, day, 0);
            for (int i = 0; i < F.Applicants.Count; i++)
            {
                Applicant a = F.Applicants[i];
                Seeker s = F.SeekerOf(a.Seeker);
                JobAd ad = F.AdOf(a.Ad);
                if (s == null || ad == null || !a.Live || a.Stage == (int)ApplyStage.Accepted)
                {
                    continue;
                }
                bool competing = HireOffers.Competing(a, day);
                if (competing && IgnoreRivals)
                {
                    continue;
                }
                if (a.Stage < (int)ApplyStage.Offered && HireOffers.ToFill(F, ad) > 0 && (competing ? a.Id % 2 == 0 : a.Id % 3 == 0))
                {
                    HiringRules.Offer(a, competing ? a.RivalBid : s.Ask, day);
                    Offered++;
                }
                else if (a.Stage == (int)ApplyStage.Offered && a.OfferDay < day)
                {
                    ApplyStage answer = HiringRules.Answer(F, a, s, day, 0, 1000);
                    Hired += answer == ApplyStage.Accepted ? 1 : 0;
                    Beaten += answer == ApplyStage.Accepted && !string.IsNullOrEmpty(a.RivalBy) ? 1 : 0;
                }
            }
        }
    }

    private static void WkRunHiring()
    {
        HiringRun Start()
        {
            var r = new HiringRun { F = NewLabour(77, 1) };
            Family family = r.F.Seekers[0].P.Def.Family;
            r.F.Ads.Add(new JobAd { Id = r.F.Next(), Place = "Main St~1", Role = r.F.Seekers[0].P.Role, Level = r.F.Seekers[0].P.Level, PayLeast = 2000, PayMost = 9000, MinSkill = 0, From = 1, To = 200, Posts = 2 });
            for (int level = 0; level < TalentCatalogue.Levels; level++)
            {
                r.F.RivalAds.Add(new RivalAd { Rival = HiringCatalogue.RivalFirms[level % HiringCatalogue.RivalFirms.Length], Family = (int)family, Level = level, To = 400 });
            }
            return r;
        }

        HiringRun a = Start();
        bool offerMade = false, lapsed = false, postsHeld = true;
        var seenBid = new HashSet<int>();
        for (int day = 2; day <= 120; day++)
        {
            a.Day(day);
            foreach (Applicant ap in a.F.Applicants)
            {
                offerMade |= ap.RivalBid > 0 && ap.RivalBy.Length > 0 && ap.RivalLapse > 0;
                if (ap.RivalBid > 0 && ap.RivalLapse > 0)
                {
                    seenBid.Add(ap.Id);
                }
            }
            foreach (JobAd ad in a.F.Ads)
            {
                postsHeld &= HireOffers.Filled(a.F, ad) <= HireOffers.Posts(ad);
            }
        }
        foreach (int id in seenBid)
        {
            Applicant ap = a.F.ApplicantOf(id);
            lapsed |= ap != null && !ap.Live && a.F.SeekerOf(ap.Seeker) is Seeker s && s.Taken;
        }
        Check(offerMade && seenBid.Count >= 1, "hiring run: over 120 days a rival with an ad in an applicant's line made a named offer with a deadline (" + seenBid.Count + " made)");
        Check(a.Offered >= 1 && postsHeld, "hiring run: the player's offers were made and the ad never took more applicants than its posts");
        Check(a.Hired >= 1, "hiring run: the player hired at least one applicant (" + a.Hired + "), " + a.Beaten + " of them against a rival's open offer");
        // a rival's offer the player let pass takes the applicant at its deadline: asked of several worlds, since one may make a single offer and have it beaten
        int lapses = 0;
        for (int seed = 77; seed < 101 && lapses == 0; seed++)
        {
            HiringRun w = new HiringRun { F = NewLabour(seed, 1), IgnoreRivals = true };
            Family fam = w.F.Seekers[0].P.Def.Family;
            w.F.Ads.Add(new JobAd { Id = w.F.Next(), Place = "Main St~1", Role = w.F.Seekers[0].P.Role, Level = w.F.Seekers[0].P.Level, PayLeast = 2000, PayMost = 9000, From = 1, To = 200, Posts = 3 });
            for (int level = 0; level < TalentCatalogue.Levels; level++)
            {
                w.F.RivalAds.Add(new RivalAd { Rival = HiringCatalogue.RivalFirms[level % HiringCatalogue.RivalFirms.Length], Family = (int)fam, Level = level, To = 400 });
            }
            var bids = new HashSet<int>();
            for (int day = 2; day <= 120; day++)
            {
                w.Day(day);
                foreach (Applicant ap in w.F.Applicants)
                {
                    if (ap.RivalBid > 0 && ap.RivalLapse > 0)
                    {
                        bids.Add(ap.Id);
                    }
                }
            }
            foreach (int id in bids)
            {
                Applicant ap = w.F.ApplicantOf(id);
                lapses += ap != null && ap.Stage == (int)ApplyStage.Withdrawn && ap.RivalLapse > 0 ? 1 : 0;   // withdrawn by the day after the deadline: the rival had them
            }
        }
        Check(lapses >= 1, "hiring run: a rival's offer the player let pass took the applicant at its deadline (" + lapses + " found over the worlds tried)");

        HiringRun c = Start();
        HiringRun b = Start();
        for (int day = 2; day <= 30; day++)
        {
            c.Day(day);
            b.Day(day);
        }
        b.F = HiringStore.Read(HiringStore.Write(b.F), out int _);
        for (int day = 31; day <= 60; day++)
        {
            c.Day(day);
            b.Day(day);
        }
        Check(HiringStore.Write(c.F) == HiringStore.Write(b.F), "hiring run: a labour market saved on day 30 and read back plays the next 30 days to the same state");

        string line = HiringStore.Write(c.F);
        int q = line.IndexOf("~q:", StringComparison.Ordinal);
        var old = new HiringRun { F = HiringStore.Read(q > 0 ? line.Substring(0, q) + line.Substring(line.IndexOf('~', q + 1) > 0 ? line.IndexOf('~', q + 1) : line.Length) : line, out int _) };
        for (int day = 61; day <= 90; day++)
        {
            old.Day(day);
        }
        Check(old.F.Ads.Count >= 1 && old.F.Ads[0].Posts >= 1 && old.F.Seekers.Count > 0, "hiring run: a labour market line from before the ads' posts loads with one post each and plays on");
    }

    // ------------------------------------------------------------------------------------------------- the Marketplace

    // The Marketplace's morning on the real director: the sellers of the book and the rivals' shops make a day's listings (MorningOf).
    private sealed class MarketRun
    {
        public HustleDirector D;
        public int Bought, RivalListings, Discount, Plain;
        public double DiscountSum, PlainSum;

        public MarketRun()
        {
            D = new HustleDirector(new HustleState { Seed = 9 }, new SellerFile(), new List<HustlePlace> { new HustlePlace("Broadway", 1), new HustlePlace("5th Avenue", 2), new HustlePlace("Pine", 4) },
                HustleCars, HustleColours);
            D.RivalShops.Add(new RivalDealer { Name = "Guo Logistics", Vehicles = true, Machines = true });
        }

        public HustleState S => D.RunState;
        public SellerFile Book => D.RunSellers;

        public void Day(int day)
        {
            D.RunMorning(day);
            foreach (Listing l in S.Listings)
            {
                ThingDef def = HustleCatalogue.Thing(l.Thing);
                Seller sel = Book.Of(l.SellerId);
                RivalListings += sel != null && sel.Rival.Length > 0 ? 1 : 0;
                int worth = HustleRules.SaidWorth(l, 1000);
                if (sel != null && worth > 0 && string.IsNullOrEmpty(sel.Rival) && (def == null || def.Kind != ThingKind.Stock))
                {
                    double ratio = l.Asking / (double)worth;
                    if (sel.Tag == (int)SellerTag.Discount)
                    {
                        Discount++;
                        DiscountSum += ratio;
                    }
                    else if (sel.Tag == (int)SellerTag.None)
                    {
                        Plain++;
                        PlainSum += ratio;
                    }
                }
            }
            if (S.Listings.Count > 0 && day % 3 == 0)
            {
                Listing pick = S.Listings[day % S.Listings.Count];
                SellerRules.NoteSale(Book, pick);
                HustleRules.Buy(S, pick, pick.Asking, false, 0, day);
                Bought++;
            }
        }
    }

    // The city's pool moves under the work apps (births, deaths, a population that comes up seconds after the load): the people the player was shown stay the same,
    // and nobody made before the pool is up is left without their id.
    private static void WkRunPoolMoves()
    {
        int size = 300;
        bool ready = true;
        PeoplePool.Pick = h => new PoolPerson { Id = (int)(h % size) + 1, Name = "Pool " + ((int)(h % size) + 1) };
        PeoplePool.Find = id => id >= 1 && id <= size ? new PoolPerson { Id = id, Name = "Pool " + id } : default(PoolPerson);
        PeoplePool.Wanted = () => true;
        PeoplePool.Ready = () => ready;
        try
        {
            // the others who applied: read, the pool grows and shrinks, read again
            var ap = new StandingRun(7);
            SaveGameManager.Current = new GameInstance { Day = 120 };
            var before = new List<string>();
            var after = new List<string>();
            var ids = new HashSet<string>();
            int dup = 0, withOthers = 0;
            for (int id = 1; id <= 30; id++)
            {
                var o = new Opening { Id = id, Prof = ap.J.Post.Prof, Rank = 1, From = 100 + id % 9, To = 140 };
                var first = new List<Peer>();
                ap.D.AlsoApplied(o, first);
                var seen = new HashSet<string>();
                foreach (Peer p in first)
                {
                    before.Add(id + ":" + p.Name);
                    dup += seen.Add(p.Name) ? 0 : 1;
                }
                withOthers += first.Count > 0 ? 1 : 0;
            }
            size = 337;   // births
            for (int id = 1; id <= 30; id++)
            {
                var second = new List<Peer>();
                ap.D.AlsoApplied(new Opening { Id = id, Prof = ap.J.Post.Prof, Rank = 1, From = 100 + id % 9, To = 140 }, second);
                foreach (Peer p in second)
                {
                    after.Add(id + ":" + p.Name);
                }
            }
            Check(withOthers >= 10 && before.Count == after.Count && string.Join("|", before) == string.Join("|", after) && dup == 0,
                "work pool run: thirty posts' other applicants are the same people after the pool grew (kept at the first read), and none is twice in one post (" + before.Count + " people)");

            // a job found before the population is up: no colleague is named from the catalogue for good
            ready = false;
            var cold = new StandingRun(31);
            StandingFile early = cold.D.Standing;
            Check(early != null && early.Peers.Count == 0, "work pool run: while the population is not up yet a new job's colleagues wait (none made with a catalogue name)");
            ready = true;
            StandingFile later = cold.D.Standing;
            bool tagged = later.Peers.Count == 3;
            var named = new HashSet<int>();
            foreach (Peer p in later.Peers)
            {
                tagged &= p.PersonId > 0 && named.Add(p.PersonId);
            }
            Check(tagged, "work pool run: once the population is up the colleagues are made, each a different person of the pool with an id");

            // the Marketplace's first morning before the population is up waits; the sellers it makes after have their ids and are all different
            ready = false;
            var cold2 = new MarketRun();
            cold2.D.RunMorning(1);
            Check(cold2.Book.Sellers.Count == 0 && cold2.S.Listings.Count == 0, "work pool run: the Marketplace's morning waits for the population instead of naming its sellers from the catalogue");
            ready = true;
            cold2.D.RunMorning(1);
            var sellerIds = new HashSet<int>();
            bool allIds = cold2.Book.Sellers.Count >= SellerRules.PoolSize;
            foreach (Seller s in cold2.Book.Sellers)
            {
                if (string.IsNullOrEmpty(s.Rival))
                {
                    allIds &= s.PersonId > 0 && sellerIds.Add(s.PersonId);
                }
            }
            Check(allIds, "work pool run: the sellers made once the population is up each have their pool id, and no two are the same person");
        }
        finally
        {
            PeoplePool.Pick = null;
            PeoplePool.Find = null;
            PeoplePool.Wanted = null;
            PeoplePool.Ready = null;
        }
    }

    private static void WkRunMarket()
    {
        var a = new MarketRun();
        for (int day = 1; day <= 90; day++)
        {
            a.Day(day);
        }
        int sold = 0;
        foreach (Seller s in a.Book.Sellers)
        {
            sold += s.Sold;
        }
        Check(a.Bought >= 25 && sold == a.Bought && a.S.Items.Count == a.Bought,
            "market run: over 90 days HustleDirector.MorningOf made listings each morning and they were bought (" + a.Bought + "), each sale on its seller's record");
        Check(a.RivalListings >= 1, "market run: a rival that owns a dealership or a machines business listed on the Marketplace through MorningOf (" + a.RivalListings + " listings; the NPC side)");
        Check(a.Discount >= 20 && a.Plain >= 20 && a.DiscountSum / a.Discount < a.PlainSum / a.Plain - 0.05,
            "market run: sellers tagged as discount sellers asked less against worth than plain sellers did (" + (a.DiscountSum / Math.Max(1, a.Discount)).ToString("0.00") + " against "
            + (a.PlainSum / Math.Max(1, a.Plain)).ToString("0.00") + ")");

        var c = new MarketRun();
        var b = new MarketRun();
        for (int day = 1; day <= 30; day++)
        {
            c.Day(day);
            b.Day(day);
        }
        b.D.RunState = HustleStore.Read(HustleStore.Write(b.S), out int _);
        b.D.RunSellers = SellerStore.Read(SellerStore.Write(b.Book), out int _);
        for (int day = 31; day <= 60; day++)
        {
            c.Day(day);
            b.Day(day);
        }
        Check(HustleStore.Write(c.S) == HustleStore.Write(b.S) && SellerStore.Write(c.Book) == SellerStore.Write(b.Book),
            "market run: the Marketplace and its sellers saved on day 30 and read back play the next 30 days to the same state");

        var old = new MarketRun();
        old.D.RunState = HustleStore.Read(HustleStore.Write(c.S).Replace("|Broadway|7", "|Broadway|"), out int _);
        for (int day = 61; day <= 90; day++)
        {
            old.Day(day);
        }
        Check(old.Bought >= 8 && old.Book.Sellers.Count >= SellerRules.PoolSize, "market run: a Marketplace line with no sellers beside it plays on, the pool made again");
    }
}
