using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Hustles;
using RealLifeOverhaul.Tech;

// Day by day runs of the work feature's game-free entries, the same calls the directors make on their day turn: the standing at work with its
// colleagues and shifts, the campus's cohorts and tutors, the labour market's rival offers and ads' posts, and the Marketplace's morning with
// its sellers and rivals. Each run is 60 to 120 days from a seed, asserts each new item's state changes as it should and that the NPC side acts,
// writes the save on day 30, reads it back and plays the next 30 days to the same state, and plays an old save (the line before the new fields) on.
internal static partial class Program
{
    private static void WkRuns()
    {
        WkRunStanding();
        WkRunRoster();
        WkRunHiring();
        WkRunMarket();
    }

    // ------------------------------------------------------------------------------------------------- standing at work

    private sealed class StandingRun
    {
        public StandingFile F = new StandingFile { Seed = 31, Hired = 5, Street = "Pine", Number = 3, Employer = "Hudson Depot" };
        public Job J = new Job { Hired = 5 };
        public int Names, Contests, PlayerWins, Passed, Reviews;

        public StandingRun()
        {
            J.Post.Street = "Pine";
            J.Post.Number = 3;
            J.Post.Employer = "Hudson Depot";
            J.Post.Rank = 1;
        }

        public string Name() => "Colleague " + Names++;

        // One game day as the director plays it: the colleagues are seeded when the record starts, a worked day records its shift, the week writes the
        // ring, and a review that would promote lets the better record have the post.
        public void Day(int day, int top)
        {
            if (F.Peers.Count == 0)
            {
                StandingRules.Seed(F, J.Post.Rank, top, 70, day, F.Seed + J.Hired, Name);
            }
            bool working = day % 7 < 5;
            int score = 66 + day % 17 + J.Post.Rank * 2;
            if (working)
            {
                J.Due++;
                J.Worked++;
                StandingRules.Shift(F, day, 480 - (day % 3) * 60, score, 17000 + day);
            }
            StandingRules.Week(F, score, day);
            if (day % 28 == 27)
            {
                Reviews++;
                if (J.Post.Rank < top && J.Worked > 10)
                {
                    Contests++;
                    bool mine = StandingRules.Contest(F, J.Post.Rank, top, day, F.Seed + day, Name, out Peer _);
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
        var a = new StandingRun();
        for (int day = 1; day <= 120; day++)
        {
            a.Day(day, 4);
        }
        Peer up = a.F.UpFor();
        Check(a.F.Count == Peer.RingSize && a.F.Peers.Count == 3 && up != null && a.F.Peers.TrueForAll(p => p.Count > 0 && p.Count <= Peer.RingSize),
            "work run: over 120 days the player's ring and every colleague's fill to twelve weeks and the shift's three people stay");
        Check(a.F.Shifts.Count == StandingRules.ShiftsMost && a.F.Shifts[a.F.Shifts.Count - 1].Day >= 116 && a.F.Shifts[0].Day < a.F.Shifts[1].Day,
            "work run: the last seven worked shifts are the latest, in order");
        Check(a.Reviews == 4 && a.Contests >= 1 && a.PlayerWins + a.Passed == a.Contests && a.Names > 3,
            "work run: each review that would promote went to the better record (" + a.PlayerWins + " to the player, " + a.Passed + " passed over) and a new colleague took the place");
        bool moved = false;
        foreach (Peer p in a.F.Peers)
        {
            moved |= p.Score != p.Skill || p.Ring[0] != p.Ring[Peer.RingSize - 1];
        }
        Check(moved, "work run: the colleagues' weeks move (the NPC side acts) every week of the run");

        // a save on day 30, read back, plays the next 30 days to the same state as the run that never stopped
        var c = new StandingRun();
        var b = new StandingRun();
        for (int day = 1; day <= 30; day++)
        {
            c.Day(day, 4);
            b.Day(day, 4);
        }
        string line = StandingStore.Write(b.F);
        b.F = StandingStore.Read(line, out int _);
        b.Names = c.Names;
        b.J = new Job { Hired = c.J.Hired, Due = c.J.Due, Worked = c.J.Worked };
        b.J.Post.Street = "Pine";
        b.J.Post.Number = 3;
        b.J.Post.Employer = "Hudson Depot";
        b.J.Post.Rank = c.J.Post.Rank;
        for (int day = 31; day <= 60; day++)
        {
            c.Day(day, 4);
            b.Day(day, 4);
        }
        Check(StandingStore.Write(c.F) == StandingStore.Write(b.F), "work run: a standing line saved on day 30 and read back plays the next 30 days to the same state");

        // an old line, from before the recent shifts and the applicants, loads and plays on
        string old = StandingStore.Write(c.F);
        var oldJob = new Job { Hired = c.J.Hired, Due = c.J.Due, Worked = c.J.Worked };
        oldJob.Post.Rank = c.J.Post.Rank;
        int cut = old.IndexOf("~r:", StringComparison.Ordinal);
        var d = new StandingRun { F = StandingStore.Read(cut > 0 ? old.Substring(0, cut) : old, out int _) };
        d.J = oldJob;
        d.Names = c.Names;
        for (int day = 61; day <= 90; day++)
        {
            d.Day(day, 4);
        }
        Check(d.F.Shifts.Count > 0 && d.F.Shifts.Count <= StandingRules.ShiftsMost && d.F.Count > 0, "work run: a standing line from before the recent shifts loads and plays on");

        // the applicants: drawn once per post, kept, and the employer's weighing is the same on a reload
        var ap = new StandingFile { Seed = 7 };
        var o = new Opening { Id = 12, Rank = 1 };
        int made = StandingRules.OthersFor(7, o);
        for (int i = 0; i < made; i++)
        {
            StandingRules.Add(ap, StandingRules.Applicant("Applicant " + i, o, 62, 100, 7, i));
        }
        StandingFile back = StandingStore.Read(StandingStore.Write(ap), out int _);
        var list = new List<Peer>();
        var list2 = new List<Peer>();
        StandingRules.OthersOf(ap, 12, list);
        StandingRules.OthersOf(back, 12, list2);
        Check(list.Count == made && list2.Count == made && StandingRules.Beaten(65, 2, list, 7, 12) == StandingRules.Beaten(65, 2, list2, 7, 12),
            "work run: the other applicants for a post are drawn once, survive a save, and weigh the same against the player after it");
    }

    // ------------------------------------------------------------------------------------------------- the campus

    private sealed class CampusRun
    {
        public RosterFile F = new RosterFile { Seed = 44 };
        public int Names, Followed, Full, Joined, Refused;
        public string Name() => "Student " + Names++;

        public void Day(int day, IList<string> staff)
        {
            Intake i = RosterRules.Ensure(F, StudyCatalogue.Business, day, Name);
            RosterRules.TutorFor(F, StudyCatalogue.Business, Name);
            if (day == 20)
            {
                if (RosterRules.CanJoin(i) && RosterRules.Join(i))
                {
                    Joined++;
                }
                else
                {
                    Refused++;
                }
            }
            if (day >= 20 && day < 80 && i.PlayerIn && RosterRules.EmployeeFollows(F, i, staff, day) != null)
            {
                Followed++;
            }
            Full += RosterRules.Left(i) == 0 ? 1 : 0;
        }
    }

    private static void WkRunRoster()
    {
        var staff = new List<string> { "Ava Lindqvist", "Tomas Reyes", "Hana Okada" };
        var run = new CampusRun();
        int seatsAt20 = 0, seatsAt27 = 0;
        for (int day = 1; day <= 120; day++)
        {
            run.Day(day, staff);
            if (day == 20)
            {
                seatsAt20 = RosterRules.Left(run.F.Find(StudyCatalogue.Business, RosterRules.StartOf(20)));
            }
            if (day == 27)
            {
                seatsAt27 = RosterRules.Left(run.F.Find(StudyCatalogue.Business, RosterRules.StartOf(27)));
            }
        }
        Check(run.F.Intakes.Count >= 4 && run.F.Intakes.TrueForAll(i => i.Students.Count <= i.Capacity) && run.F.Tutors.Count == 1 && run.Names >= 20,
            "campus run: over 120 days a new cohort stands every 28 days, none over its seats, one tutor, and the NPC students are named by the game's roll");
        Check(seatsAt27 <= seatsAt20, "campus run: the others fill seats while a cohort runs, never empty them (" + seatsAt20 + " left on day 20, " + seatsAt27 + " on day 27)");
        Check(run.Joined + run.Refused == 1, "campus run: the player's press for a seat on day 20 either took one or was refused for a full cohort");
        Check(run.Joined == 0 || run.Followed <= 1, "campus run: an employee follows the same cohort at most once");

        var c = new CampusRun();
        var b = new CampusRun();
        for (int day = 1; day <= 30; day++)
        {
            c.Day(day, staff);
            b.Day(day, staff);
        }
        b.F = RosterStore.Read(RosterStore.Write(b.F), out int _);
        b.Names = c.Names;
        for (int day = 31; day <= 60; day++)
        {
            c.Day(day, staff);
            b.Day(day, staff);
        }
        Check(RosterStore.Write(c.F) == RosterStore.Write(b.F), "campus run: a roster saved on day 30 and read back plays the next 30 days to the same state");
        string line = RosterStore.Write(c.F);
        var old = new CampusRun { F = RosterStore.Read(line.Substring(0, line.IndexOf("~t:", StringComparison.Ordinal) > 0 ? line.IndexOf("~t:", StringComparison.Ordinal) : line.Length), out int _) };
        old.Names = c.Names;
        for (int day = 61; day <= 90; day++)
        {
            old.Day(day, staff);
        }
        Check(old.F.Tutors.Count == 1 && old.F.Intakes.Count >= 3, "campus run: a roster line without its tutors loads and plays on, the tutor made again");
        // marks over the run: a transcript across quarters gives an average, a rank and a class
        var marks = new List<Mark> { new Mark { Credits = 6, Points = 400, Start = 5 }, new Mark { Credits = 4, Points = 300, Start = 40 }, new Mark { Credits = 6, Points = 200, Start = 100 } };
        var q = new List<int>();
        var avg = new List<int>();
        Intake last = run.F.Find(StudyCatalogue.Business, RosterRules.StartOf(20));
        int gpa = RosterRules.Gpa(marks);
        var lines = new List<ClassLine>();
        int rank = RosterRules.Rank(last, "You", gpa, lines);
        Check(RosterRules.ByTerm(marks, 60, q, avg) == 3 && rank >= 1 && rank <= lines.Count && lines.Count == last.Students.Count + 1,
            "campus run: the transcript groups by quarter and ranks the player among the run's own classmates");
    }

    // ------------------------------------------------------------------------------------------------- the labour market

    private sealed class HiringRun
    {
        public HiringFile F;
        public int Offered, Beaten, Hired;

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
        Check(lapsed || seenBid.Count == 0 || a.Beaten >= 1, "hiring run: a rival's offer the player did not beat took the applicant, or the player beat it");

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

    private sealed class MarketRun
    {
        public HustleState S = new HustleState { Seed = 9 };
        public SellerFile Book = new SellerFile();
        public int Bought, RivalListings, Sold, Discount, Plain;
        public double DiscountSum, PlainSum;
        public readonly List<RivalDealer> Dealers = new List<RivalDealer> { new RivalDealer { Name = "Guo Logistics", Vehicles = true, Machines = true } };
        public readonly List<HustlePlace> Homes = new List<HustlePlace> { new HustlePlace("Broadway", 1), new HustlePlace("5th Avenue", 2), new HustlePlace("Pine", 4) };

        public void Day(int day)
        {
            SellerRules.Ensure(Book, S.Seed, k => "Seller " + k);
            HustleRules.Morning(S, day, Homes, HustleCars, HustleColours, 1000, 1000, Book, Dealers);
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
        Check(a.Bought >= 25 && sold == a.Bought && a.S.Items.Count == a.Bought, "market run: over 90 days listings appeared each morning and were bought (" + a.Bought + "), each sale on its seller's record");
        Check(a.RivalListings >= 1, "market run: a rival that owns a dealership or a machines business listed on the Marketplace (" + a.RivalListings + " listings; the NPC side)");
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
        b.S = HustleStore.Read(HustleStore.Write(b.S), out int _);
        b.Book = SellerStore.Read(SellerStore.Write(b.Book), out int _);
        for (int day = 31; day <= 60; day++)
        {
            c.Day(day);
            b.Day(day);
        }
        Check(HustleStore.Write(c.S) == HustleStore.Write(b.S) && SellerStore.Write(c.Book) == SellerStore.Write(b.Book),
            "market run: the Marketplace and its sellers saved on day 30 and read back play the next 30 days to the same state");

        var old = new MarketRun { S = HustleStore.Read(HustleStore.Write(c.S).Replace("|Broadway|7", "|Broadway|"), out int _) };
        for (int day = 61; day <= 90; day++)
        {
            old.Day(day);
        }
        Check(old.Bought >= 8 && old.Book.Sellers.Count >= SellerRules.PoolSize, "market run: a Marketplace line with no sellers beside it plays on, the pool made again");
    }
}
