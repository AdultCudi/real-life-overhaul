using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Ventures;

// The cleaning service's and selling a business's checks (Scripts/Economy/Services: CleaningRules.cs, RoundRules.cs,
// ContractRules.cs, SaleRules.cs, ServicesStore.cs): the game's cleaning arithmetic, travel and its kept distances,
// the round (who goes where and when, within each shift, and what is left over and why), what a visit does to a
// floor, contracts (offers, the fee, a week kept and a week missed, a contract lost), a business's value line by
// line, the market (offers by asking price, each kind of buyer, a counter), the money on the day after investors and
// loans, the parts of a price, the clause, the save's line, the empty cases, determinism and how long the largest
// round takes.
internal static partial class Program
{
    private static void Services()
    {
        ServicesCleaning();
        ServicesTravel();
        ServicesJobs();
        ServicesRound();
        ServicesRoundEdges();
        ServicesVisits();
        ServicesContracts();
        ServicesValue();
        ServicesMarket();
        ServicesCounter();
        ServicesMoney();
        ServicesClause();
        ServicesSave();
        ServicesTiming();
    }

    // ---------------------------------------------------------------- making things by hand

    private static CleaningClient SvcShop(string place, int x, int z, int mode = 0, int worst = 2000, int clean = 60)
    {
        return new CleaningClient { Place = place, Name = place, X = x, Z = z, Mode = mode, Worst = worst, Clean = clean, Open = true, Force = 1 };
    }

    private static RoundCleaner SvcCleaner(string id, int from, int to, int x = 0, int z = 0, int skill = 50, int mood = 80)
    {
        return new RoundCleaner { Id = id, Name = id, From = from * 60, To = to * 60, X = x, Z = z, Skill = skill, Mood = mood };
    }

    // The rules of a round, checked by code that shares nothing with the planner: nobody in two places at once, every
    // visit inside its cleaner's shift with the way home, no visit before its hour, the way there really walked.
    private static string SvcWrong(List<RoundCleaner> cleaners, List<RoundJob> jobs, List<RoundVisit> visits, bool van)
    {
        var free = new int[cleaners.Count];
        var atX = new int[cleaners.Count];
        var atZ = new int[cleaners.Count];
        for (int c = 0; c < cleaners.Count; c++)
        {
            free[c] = cleaners[c].From;
            atX[c] = cleaners[c].X;
            atZ[c] = cleaners[c].Z;
        }
        foreach (RoundVisit v in visits)
        {
            RoundCleaner who = cleaners[v.Cleaner];
            RoundJob job = null;
            foreach (RoundJob j in jobs)
            {
                job = j.Client == v.Client && j.Slot == v.Slot ? j : job;
            }
            if (job == null || !job.Taken)
            {
                return "a visit for no job";
            }
            int way = TravelRules.Minutes(TravelRules.Metres(atX[v.Cleaner], atZ[v.Cleaner], job.X, job.Z), van);
            int home = TravelRules.Minutes(TravelRules.Metres(job.X, job.Z, who.X, who.Z), van);
            if (v.Leave < free[v.Cleaner])
            {
                return who.Id + " sets out at " + v.Leave + " while busy until " + free[v.Cleaner];
            }
            if (v.Start - v.Leave != way)
            {
                return who.Id + " takes " + (v.Start - v.Leave) + " minutes for a way of " + way;
            }
            if (v.Start < job.Earliest)
            {
                return "a visit at " + v.Start + " before its hour " + job.Earliest;
            }
            if (v.End - v.Start < ServicesCatalogue.VisitLeast || v.End - v.Start > ServicesCatalogue.VisitMost)
            {
                return "a visit of " + (v.End - v.Start) + " minutes";
            }
            if (v.End + home > who.To)
            {
                return who.Id + " cannot be back by the end of the shift";
            }
            free[v.Cleaner] = v.End;
            atX[v.Cleaner] = job.X;
            atZ[v.Cleaner] = job.Z;
        }
        return null;
    }

    // ---------------------------------------------------------------- the game's cleaning arithmetic

    private static void ServicesCleaning()
    {
        Check(CleaningRules.Force(0) == 1 && CleaningRules.Force(200) == 1 && CleaningRules.Force(201) == 2 && CleaningRules.Force(1000) == 5,
            "cleaning: a floor needs one cleaner for each 200 m2 begun (the game's 0.005 a square metre, rounded up)");
        Check(CleaningRules.Rate(50, 80, 1) == 4000 && CleaningRules.Rate(50, 80, 2) == 2000 && CleaningRules.Rate(100, 100, 1) == 10000,
            "cleaning: an hour takes skill by satisfaction over the force off every tile (50 by 80% is 40 points)");
        Check(CleaningRules.Rate(0, 80, 1) == 0 && CleaningRules.Rate(50, 0, 1) == 0 && CleaningRules.Rate(150, 200, 1) == 10000, "cleaning: no skill or no satisfaction cleans nothing, and neither counts past 100");
        Check(CleaningRules.Off(4000, 60) == 4000 && CleaningRules.Off(4000, 15) == 1000 && CleaningRules.Off(4000, 0) == 0 && CleaningRules.Off(0, 60) == 0, "cleaning: a part of an hour takes that part off");
        Check(CleaningRules.Minutes(2000, 4000) == 30 && CleaningRules.Minutes(2001, 4000) == 31 && CleaningRules.Minutes(0, 4000) == 0 && CleaningRules.Minutes(2000, 0) == 0,
            "cleaning: the minutes a tile takes are its dirt over the rate, rounded up");
        Check(CleaningRules.Planned(100, 4000) == ServicesCatalogue.VisitLeast && CleaningRules.Planned(100000, 100) == ServicesCatalogue.VisitMost && CleaningRules.Planned(-1, 4000) == ServicesCatalogue.VisitDefault
              && CleaningRules.Planned(2000, 0) == 0, "cleaning: a visit is planned between the shortest and the longest, at the usual length where the dirt is unknown");
        int[] tiles = { 0, 300, 1000, 5000 };
        Check(CleaningRules.Cleanliness(tiles) == 69, "cleaning: cleanliness is 100 less the mean dirt (15.75) and less the mean of the dirt that shows (15), cut to a whole number (" + CleaningRules.Cleanliness(tiles) + ")");
        Check(CleaningRules.Cleanliness(new[] { 0, 499, 499 }) == 100, "cleaning: a floor whose dirt does not show is at 100");
        Check(CleaningRules.Cleanliness(new int[0]) == 100 && CleaningRules.Cleanliness(new[] { 10000, 10000 }) == 0, "cleaning: no tiles is clean, and every tile at its dirtiest is 0");
        int worst = CleaningRules.Clean(tiles, 1000);
        Check(tiles[0] == 0 && tiles[1] == 0 && tiles[2] == 0 && tiles[3] == 4000 && worst == 4000, "cleaning: a visit takes the same off every tile and none goes under nothing");
        Check(CleaningRules.Even(400) == 100 && CleaningRules.Even(500) == 90 && CleaningRules.Even(1500) == 70 && CleaningRules.Even(6000) == 0, "cleaning: an evenly dirty floor loses two points of cleanliness a point of dirt once it shows");
        Check(CleaningRules.Hold(1000, 4000, 30) == 3000 && CleaningRules.Hold(1000, 4000, 90) == 5000 && CleaningRules.Hold(9000, 4000, 60) == 10000,
            "cleaning: the base gets back what the game is about to take off for the minutes its cleaner was out, an hour at most");
        Check(CleaningRules.Clean(CleaningRules.Hold(1000, 4000, 20), CleaningRules.Off(4000, 60)) == CleaningRules.Clean(1000, CleaningRules.Off(4000, 40)),
            "cleaning: held and then cleaned for the game's whole hour, a base tile ends where 40 minutes of cleaning would leave it");
        Check(CleaningRules.NeedDay(-1, 1000, 2500, 500) == 2000 && CleaningRules.NeedDay(2000, 1000, 1000, 0) == 1333 && CleaningRules.NeedDay(-1, 3000, 1000, 0) == 0,
            "cleaning: a day's dirt is the worst tile's change with what the service took off put back, averaged over three days");
        Check(CleaningRules.NeedMinutes(2000, 4000) == 30 && CleaningRules.NeedMinutes(-1, 4000) == 0, "cleaning: the minutes a day a business needs follow its measured dirt");
        Check(CleaningRules.OwnWeek(30, 1500) == 105 && CleaningRules.OwnWeek(61, 1500) == 210 && CleaningRules.OwnWeek(0, 1500) == 105, "cleaning: a cleaner of its own costs a shop whole hours a day, one at least, seven days");
    }

    private static void ServicesTravel()
    {
        Check(TravelRules.Metres(0, 0, 300, 400) == 650 && TravelRules.Metres(5, 5, 5, 5) == 0, "travel: the way is the straight line and three tenths more for the streets");
        Check(TravelRules.Minutes(650, false) == 186 && TravelRules.Minutes(650, true) == 82 && TravelRules.Minutes(0, false) == 0 && TravelRules.Minutes(1, true) == 1,
            "travel: on foot at 3.5 m a game minute, by van at 8, rounded up (" + TravelRules.Minutes(650, false) + ", " + TravelRules.Minutes(650, true) + ")");
        Check(TravelRules.Cost(1000, 1000) == 4 && TravelRules.Cost(2500, 1000) == 10 && TravelRules.Cost(1000, 1500) == 6 && TravelRules.Cost(0, 1000) == 0 && TravelRules.Cost(100, 1000) == 0,
            "travel: the van costs 4 a kilometre at a price index of 100, and follows prices");
        var cache = new TravelCache();
        int first = cache.Metres(0, 0, 300, 400), again = cache.Metres(300, 400, 0, 0);
        Check(first == 650 && again == 650 && cache.Misses == 1 && cache.Hits == 1 && cache.Count == 1, "travel: a pair of doors is worked out once, whichever way it is asked");
        Check(cache.Metres(7, 7, 7, 7) == 0 && cache.Count == 1, "travel: a door to itself is no way and is not kept");
        cache.Metres(-900, 1200, 40, -3000);
        Check(cache.Metres(40, -3000, -900, 1200) == TravelRules.Metres(-900, 1200, 40, -3000) && cache.Count == 2 && cache.Hits == 2, "travel: doors on the far side of the map's middle are kept apart");
        cache.Clear();
        Check(cache.Count == 0 && cache.Hits == 0 && cache.Misses == 0, "travel: the kept distances are thrown away when the places change");
    }

    // ---------------------------------------------------------------- what is due

    private static void ServicesJobs()
    {
        var clients = new List<CleaningClient>();
        var jobs = new List<RoundJob>();
        RoundRules.Jobs(clients, 0, jobs);
        Check(jobs.Count == 0, "round: no clients, nothing due");

        CleaningClient needed = SvcShop("a", 10, 10, 0, 2000, 60);
        clients.Add(needed);
        RoundRules.Jobs(clients, 600, jobs);
        Check(jobs.Count == 1 && jobs[0].Slot == -1 && jobs[0].Earliest == 600 && jobs[0].Need == 2000, "round: a floor under its threshold is due now, for the dirt that is on it");
        needed.Clean = 80;
        RoundRules.Jobs(clients, 600, jobs);
        Check(jobs.Count == 0, "round: a floor at its threshold is not due");
        needed.Clean = 60;
        needed.Pending = true;
        RoundRules.Jobs(clients, 600, jobs);
        Check(jobs.Count == 0, "round: a floor that somebody is already on the way to is not due a second time");
        needed.Pending = false;
        needed.Open = false;
        RoundRules.Jobs(clients, 600, jobs);
        Check(jobs.Count == 0, "round: a shop the game would not clean today is left alone");
        needed.Open = true;
        needed.Gone = true;
        RoundRules.Jobs(clients, 600, jobs);
        Check(jobs.Count == 0, "round: a business that is gone is not visited");
        needed.Gone = false;

        CleaningClient fixedHours = SvcShop("b", 50, 50, 3, 1800, 95);
        fixedHours.NeedDay = 3000;
        clients.Add(fixedHours);
        RoundRules.Jobs(clients, 0, jobs);
        Check(jobs.Count == 4 && jobs[1].Slot == 0 && jobs[1].Earliest == 9 * 60 && jobs[2].Earliest == 14 * 60 && jobs[3].Earliest == 19 * 60, "round: three visits a day are due at their three hours, whatever the floor looks like");
        Check(jobs[1].Need == 1800 && jobs[2].Need == 1000 && jobs[3].Need == 1000, "round: the next visit finds what is on the floor, later ones a share of a day's dirt");
        RoundRules.Jobs(clients, 14 * 60 + 20, jobs);
        Check(jobs.Count == 3 && jobs[1].Slot == 1 && jobs[1].Earliest == 14 * 60 + 20 && jobs[2].Slot == 2, "round: an hour that has passed is not made up; one that is running is due now");
        fixedHours.DoneMask = 1 << 1;
        RoundRules.Jobs(clients, 14 * 60 + 20, jobs);
        Check(jobs.Count == 2 && jobs[1].Slot == 2, "round: a visit that was made is not planned again");
        fixedHours.DoneMask = 0;
        fixedHours.NeedDay = -1;
        fixedHours.Worst = 0;
        RoundRules.Jobs(clients, 0, jobs);
        Check(jobs[1].Need == -1 && jobs[2].Need == -1, "round: where nothing has been measured yet a visit has the usual length");

        CleaningClient contract = new CleaningClient { Place = "c", Outside = true, Dirt = 1500, Threshold = 90, Force = 1, X = 5, Z = 5 };
        clients.Clear();
        clients.Add(contract);
        RoundRules.Jobs(clients, 100, jobs);
        Check(jobs.Count == 1 && jobs[0].Need == 1500, "round: a client under contract is due by the mod's own figure for its floor (cleanliness " + contract.Now + ")");
    }

    // ---------------------------------------------------------------- the round

    private static void ServicesRound()
    {
        var cache = new TravelCache();
        var cleaners = new List<RoundCleaner> { SvcCleaner("ann", 8, 16) };
        var clients = new List<CleaningClient> { SvcShop("near", 20, 0), SvcShop("far", 0, 150), SvcShop("mid", 60, 0) };
        var jobs = new List<RoundJob>();
        var visits = new List<RoundVisit>();
        RoundRules.Jobs(clients, 0, jobs);
        int taken = RoundRules.Plan(cleaners, jobs, cache, false, 0, visits);
        Check(taken == 3 && visits.Count == 3, "round: one cleaner with a day's shift takes three visits (" + taken + ")");
        Check(SvcWrong(cleaners, jobs, visits, false) == null, "round: one cleaner's round keeps the rules (" + SvcWrong(cleaners, jobs, visits, false) + ")");
        Check(visits[0].Client == 0 && visits[1].Client == 2 && visits[2].Client == 1, "round: of visits all due at once the one that can begin soonest is taken first: near, then the middle one, then the far one");
        Check(visits[0].Leave == 8 * 60 && visits[0].Start == 8 * 60 + TravelRules.Minutes(26, false), "round: nobody sets out before their shift begins");
        Check(visits[0].End - visits[0].Start == 30 && visits[0].Rate == 4000, "round: a visit lasts as long as the dirt takes at that cleaner's rate there (20 points at 40 an hour)");
        Check(cleaners[0].AwayFrom == 8 * 60 && cleaners[0].AwayTo == visits[2].End + TravelRules.Minutes(TravelRules.Metres(0, 150, 0, 0), false), "round: a cleaner is away from the moment they set out until they are back at base");
        Check(cleaners[0].Metres == 26 + TravelRules.Metres(20, 0, 60, 0) + TravelRules.Metres(60, 0, 0, 150) + 195 && cleaners[0].Visits == 3, "round: the way of a round is every leg and the way home");
        Check(cleaners[0].HomeMetres == 195 && cleaners[0].HomeMinutes == TravelRules.Minutes(195, false), "round: the way home is from the last place visited");

        // Two cleaners: the work is shared by who can start soonest.
        cleaners = new List<RoundCleaner> { SvcCleaner("ann", 8, 16, 0, 0), SvcCleaner("bob", 8, 16, 0, 160) };
        RoundRules.Jobs(clients, 0, jobs);
        visits.Clear();
        RoundRules.Plan(cleaners, jobs, cache, false, 0, visits);
        Check(visits.Count == 3 && SvcWrong(cleaners, jobs, visits, false) == null, "round: two cleaners' round keeps the rules (" + SvcWrong(cleaners, jobs, visits, false) + ")");
        int farBy = -1;
        foreach (RoundVisit v in visits)
        {
            farBy = v.Client == 1 ? v.Cleaner : farBy;
        }
        Check(farBy == 1 && cleaners[0].Visits == 2 && cleaners[1].Visits == 1, "round: the far shop goes to the cleaner based beside it");

        // A van: the same round, sooner.
        int footLeg = visits[visits.Count - 1].Start - visits[visits.Count - 1].Leave;
        cleaners = new List<RoundCleaner> { SvcCleaner("ann", 8, 16) };
        RoundRules.Jobs(clients, 0, jobs);
        visits.Clear();
        RoundRules.Plan(cleaners, jobs, cache, true, 0, visits);
        Check(visits.Count == 3 && SvcWrong(cleaners, jobs, visits, true) == null && visits[2].Start - visits[2].Leave == TravelRules.Minutes(TravelRules.Metres(60, 0, 0, 150), true),
            "round: by van the legs take the van's time (" + SvcWrong(cleaners, jobs, visits, true) + ")");
        Check(footLeg > 0 && visits[2].Start - visits[2].Leave < TravelRules.Minutes(TravelRules.Metres(60, 0, 0, 150), false), "round: a leg by van is shorter than the same leg on foot");

        // Fixed hours are waited for.
        clients = new List<CleaningClient> { SvcShop("a", 20, 0, 2, 1200, 95) };
        clients[0].Hours[0] = 10;
        clients[0].Hours[1] = 15;
        clients[0].NeedDay = 2400;
        cleaners = new List<RoundCleaner> { SvcCleaner("ann", 8, 18) };
        RoundRules.Jobs(clients, 0, jobs);
        visits.Clear();
        RoundRules.Plan(cleaners, jobs, cache, false, 0, visits);
        Check(visits.Count == 2 && visits[0].Start == 600 && visits[1].Start == 900 && visits[0].Leave == 600 - TravelRules.Minutes(26, false), "round: a visit at a fixed hour begins on the hour, and the cleaner sets out in time for it");
        Check(visits[1].Leave == visits[1].Start && visits[1].Metres == 0, "round: a second visit at the same place needs no way");

        // The same question, the same answer.
        var second = new List<RoundVisit>();
        cleaners = new List<RoundCleaner> { SvcCleaner("ann", 8, 18) };
        RoundRules.Jobs(clients, 0, jobs);
        RoundRules.Plan(cleaners, jobs, new TravelCache(), false, 0, second);
        bool same = second.Count == visits.Count;
        for (int i = 0; same && i < second.Count; i++)
        {
            same = second[i].Cleaner == visits[i].Cleaner && second[i].Client == visits[i].Client && second[i].Start == visits[i].Start && second[i].End == visits[i].End;
        }
        Check(same, "round: the same clients, crew and hour always give the same round");
    }

    private static void ServicesRoundEdges()
    {
        var cache = new TravelCache();
        var jobs = new List<RoundJob>();
        var visits = new List<RoundVisit>();
        var clients = new List<CleaningClient> { SvcShop("a", 20, 0) };

        // No cleaners at all.
        RoundRules.Jobs(clients, 0, jobs);
        Check(RoundRules.Plan(new List<RoundCleaner>(), jobs, cache, false, 0, visits) == 0 && visits.Count == 0 && jobs[0].Why == RoundRules.NoCleaner, "round: with no crew nothing is visited, and the reason is that nobody is on a cleaning shift");

        // No clients.
        var cleaners = new List<RoundCleaner> { SvcCleaner("ann", 8, 16) };
        Check(RoundRules.Plan(cleaners, new List<RoundJob>(), cache, false, 0, visits) == 0 && cleaners[0].AwayFrom < 0 && RoundRules.Away(cleaners[0], 600) == 0, "round: with nothing due the crew stays at base");

        // One shop that is the base itself: no way at all.
        clients = new List<CleaningClient> { SvcShop("base", 0, 0) };
        cleaners = new List<RoundCleaner> { SvcCleaner("ann", 8, 16) };
        RoundRules.Jobs(clients, 0, jobs);
        RoundRules.Plan(cleaners, jobs, cache, false, 0, visits);
        Check(visits.Count == 1 && visits[0].Leave == visits[0].Start && cleaners[0].Metres == 0, "round: one shop at the cleaner's own door is visited without a step");
        visits.Clear();

        // Too far for anybody.
        clients = new List<CleaningClient> { SvcShop("moon", 5000, 0) };
        cleaners = new List<RoundCleaner> { SvcCleaner("ann", 0, 24) };
        RoundRules.Jobs(clients, 0, jobs);
        Check(RoundRules.Plan(cleaners, jobs, cache, false, 0, visits) == 0 && jobs[0].Why == RoundRules.TooFar, "round: a door further than anybody goes is left, and the reason says so");

        // A shift too short to get there and back.
        clients = new List<CleaningClient> { SvcShop("a", 100, 0) };
        cleaners = new List<RoundCleaner> { SvcCleaner("ann", 9, 10) };
        RoundRules.Jobs(clients, 0, jobs);
        Check(RoundRules.Plan(cleaners, jobs, cache, false, 0, visits) == 0 && jobs[0].Why == RoundRules.NoTime, "round: a shift too short to walk there, clean and walk back takes no visit");
        cleaners = new List<RoundCleaner> { SvcCleaner("ann", 9, 10) };
        RoundRules.Jobs(clients, 0, jobs);
        Check(RoundRules.Plan(cleaners, jobs, cache, true, 0, visits) == 1 && SvcWrong(cleaners, jobs, visits, true) == null, "round: the same shift by van does take it");
        Check(visits[0].End - visits[0].Start == 26 && visits[0].End + TravelRules.Minutes(130, true) == 600, "round: a visit is cut short so that the cleaner is back when the shift ends");
        visits.Clear();

        // Somebody with no skill, and somebody who is miserable.
        clients = new List<CleaningClient> { SvcShop("a", 10, 0) };
        cleaners = new List<RoundCleaner> { SvcCleaner("no skill", 8, 16, 0, 0, 0, 80), SvcCleaner("no heart", 8, 16, 0, 0, 50, 0) };
        RoundRules.Jobs(clients, 0, jobs);
        Check(RoundRules.Plan(cleaners, jobs, cache, false, 0, visits) == 0 && jobs[0].Why == RoundRules.NoCleaner, "round: a cleaner who would take nothing off the floor is sent nowhere");

        // Planned again in the middle of the day: a visit under way is kept, and its cleaner goes on from there.
        clients = new List<CleaningClient> { SvcShop("a", 100, 0), SvcShop("b", 110, 0) };
        cleaners = new List<RoundCleaner> { SvcCleaner("ann", 8, 18) };
        cleaners[0].FreeFrom = 700;
        cleaners[0].Out = true;
        cleaners[0].AtX = 100;
        cleaners[0].AtZ = 0;
        cleaners[0].AwayFrom = 620;
        clients[0].Pending = true;
        RoundRules.Jobs(clients, 650, jobs);
        RoundRules.Plan(cleaners, jobs, cache, false, 650, visits);
        Check(visits.Count == 1 && visits[0].Client == 1 && visits[0].Leave == 700 && visits[0].Start == 700 + TravelRules.Minutes(13, false) && cleaners[0].AwayFrom == 620,
            "round: planned again at midday, a cleaner goes on from the shop they are in when they have finished there");
        visits.Clear();

        // Out with nothing more to do: only the way home is left.
        cleaners = new List<RoundCleaner> { SvcCleaner("ann", 8, 18) };
        cleaners[0].FreeFrom = 700;
        cleaners[0].Out = true;
        cleaners[0].AtX = 100;
        RoundRules.Plan(cleaners, new List<RoundJob>(), cache, false, 650, visits);
        Check(cleaners[0].AwayTo == 700 + TravelRules.Minutes(130, false) && cleaners[0].Metres == 130, "round: a cleaner who is out with nothing more to do walks home");

        // After the shift.
        clients = new List<CleaningClient> { SvcShop("a", 10, 0) };
        cleaners = new List<RoundCleaner> { SvcCleaner("ann", 8, 12) };
        RoundRules.Jobs(clients, 13 * 60, jobs);
        Check(RoundRules.Plan(cleaners, jobs, cache, false, 13 * 60, visits) == 0 && jobs[0].Why == RoundRules.NoCleaner, "round: nothing is planned for a shift that is over");

        // Two shifts of one person are two spans of the same day.
        clients = new List<CleaningClient> { SvcShop("a", 10, 0, 2, 800, 95) };
        clients[0].Hours[0] = 9;
        clients[0].Hours[1] = 17;
        cleaners = new List<RoundCleaner> { SvcCleaner("ann", 8, 11), SvcCleaner("ann", 16, 19) };
        RoundRules.Jobs(clients, 0, jobs);
        RoundRules.Plan(cleaners, jobs, cache, false, 0, visits);
        Check(visits.Count == 2 && visits[0].Cleaner == 0 && visits[1].Cleaner == 1 && SvcWrong(cleaners, jobs, visits, false) == null, "round: a cleaner with a morning and an evening shift takes a visit in each");
        Check(RoundRules.Overlap(0, 10, 5, 20) == 5 && RoundRules.Overlap(0, 10, 10, 20) == 0 && RoundRules.Overlap(5, 6, 0, 100) == 1, "round: the minutes two spans share");
        Check(RoundRules.Away(cleaners[0], 9 * 60) == RoundRules.Overlap(cleaners[0].AwayFrom, cleaners[0].AwayTo, 540, 600) && RoundRules.Away(cleaners[0], 17 * 60) == 0 && RoundRules.Away(cleaners[1], 17 * 60) > 0,
            "round: each shift of the day has its own time away");
    }

    // ---------------------------------------------------------------- what a visit does

    private static void ServicesVisits()
    {
        // A day by hand: a shop evenly at 30 points, a cleaner of 40 points an hour there, a 45 minute visit over two hours.
        int[] tiles = { 3000, 3000, 1000, 0 };
        var visit = new RoundVisit { Start = 9 * 60 + 40, End = 10 * 60 + 25, Rate = 4000 };
        int firstHour = RoundRules.Overlap(visit.Start, visit.End, 9 * 60, 10 * 60), secondHour = RoundRules.Overlap(visit.Start, visit.End, 10 * 60, 11 * 60);
        Check(firstHour == 20 && secondHour == 25 && RoundRules.Overlap(visit.Start, visit.End, 11 * 60, 12 * 60) == 0, "visit: a visit across the hour is 20 minutes of one hour and 25 of the next");
        CleaningRules.Clean(tiles, CleaningRules.Off(visit.Rate, firstHour));
        Check(tiles[0] == 1667 && tiles[2] == 0, "visit: the first hour's part comes off every tile (" + tiles[0] + ")");
        CleaningRules.Clean(tiles, CleaningRules.Off(visit.Rate, secondHour));
        Check(tiles[0] == 1 && tiles[1] == 1 && tiles[3] == 0 && CleaningRules.Cleanliness(tiles) == 100, "visit: after the whole visit the floor shows no dirt (" + tiles[0] + ")");

        // The same visit in a building twice the size takes half off.
        Check(CleaningRules.Rate(50, 80, CleaningRules.Force(400)) == 2000, "visit: a floor that needs two cleaners is cleaned at half the rate by one");

        // A client under contract.
        var client = new CleaningClient { Outside = true, Dirt = 2000, Standard = 80 };
        ContractRules.Visit(client, 4000, 15);
        Check(client.Dirt == 1000 && client.Now == 80, "visit: a quarter of an hour at 40 points an hour takes 10 points off a client's floor");
        ContractRules.Visit(client, 4000, 600);
        Check(client.Dirt == 0 && client.Now == 100, "visit: a floor is never cleaner than clean");

        // A whole day's balance: what the base loses is what the client gains.
        int away = 0;
        var who = new RoundCleaner { AwayFrom = 9 * 60 + 30, AwayTo = 11 * 60 + 10 };
        for (int hour = 0; hour < 24; hour++)
        {
            away += RoundRules.Away(who, hour * 60);
        }
        Check(away == 100 && RoundRules.Away(who, 9 * 60) == 30 && RoundRules.Away(who, 10 * 60) == 60 && RoundRules.Away(who, 11 * 60) == 10, "visit: the hours of a day add up to the minutes a cleaner was away");
        Check(RoundRules.Away(new RoundCleaner(), 600) == 0, "visit: a cleaner who never left is away for no minutes");

        // What is cleaned at each hour event: this hour's minutes, and whatever of the visit is still owed from before.
        Check(RoundRules.Due(580, 625, 0, 540) == 20 && RoundRules.Due(580, 625, 20, 600) == 25 && RoundRules.Due(580, 625, 45, 660) == 0, "visit: at each hour the minutes of that hour are cleaned, and none twice");
        Check(RoundRules.Due(640, 700, 0, 660) == 60 && RoundRules.Due(640, 700, 60, 720) == 0, "visit: a visit planned at twenty to, in the middle of an hour, is cleaned for its first twenty minutes with the next hour's forty");
        Check(RoundRules.Due(700, 760, 0, 600) == 0 && RoundRules.Due(600, 600, 0, 600) == 0 && RoundRules.Due(600, 660, 999, 600) == 0, "visit: a visit that has not begun, an empty one and one already done are owed nothing");
        int doneSoFar = 0, cleanedInAll = 0;
        for (int hour = 0; hour < 24; hour++)
        {
            int due = RoundRules.Due(9 * 60 + 40, 12 * 60 + 5, doneSoFar, hour * 60);
            doneSoFar += due;
            cleanedInAll += due;
            Check(due <= 60, "visit: no hour of a visit planned ahead cleans more than an hour (hour " + hour + ")");
        }
        Check(cleanedInAll == 145, "visit: over the day a visit is cleaned for exactly its length (" + cleanedInAll + ")");
    }

    // ---------------------------------------------------------------- contracts

    private static void ServicesContracts()
    {
        Check(ContractRules.DirtDay(0) == 600 && ContractRules.DirtDay(20) == 1100 && ContractRules.DirtDay(1000) == 3000, "contracts: a client's floor gains more dirt a day the more customers it has room for, within bounds");
        Check(ContractRules.MinutesDay(150, 20) == 17 && ContractRules.MinutesDay(350, 20) == 33, "contracts: the minutes a day a floor takes grow with the cleaners it needs (" + ContractRules.MinutesDay(150, 20) + ", " + ContractRules.MinutesDay(350, 20) + ")");
        int fee = ContractRules.Fee(900, 80, 100, 1000);
        Check(ContractRules.MinutesDay(900, 80) == 195 && fee == ServicesCatalogue.Round10(195L * 7 * 14 / 60 * 220 / 100) && fee == 690, "contracts: the fee is the week's cleaning at the reference wage with the margin (" + fee + ")");
        Check(ContractRules.Fee(150, 0, 100, 1000) == ServicesCatalogue.ContractFeeLeast, "contracts: no fee is under the least");
        Check(ContractRules.Fee(900, 80, 140, 1000) > ContractRules.Fee(900, 80, 100, 1000) && ContractRules.Fee(900, 80, 100, 1000) > ContractRules.Fee(900, 80, 60, 1000), "contracts: a richer district pays more");
        Check(ContractRules.Fee(900, 80, 100, 1500) > ContractRules.Fee(900, 80, 100, 1000), "contracts: the fee follows prices");

        var state = new ServicesState { Seed = 77 };
        var candidates = new List<ContractCandidate>();
        Check(ContractRules.Draw(state, candidates, 10, 1000) == null && state.LastOfferDay == 10, "contracts: a city with nobody to ask gives no offer, and the day is noted");
        Check(!ContractRules.OfferDue(state, 12) && ContractRules.OfferDue(state, 13), "contracts: a new offer is looked for every third day");
        for (int i = 0; i < 6; i++)
        {
            candidates.Add(new ContractCandidate { Place = "p" + i, Name = "Shop " + i, District = i < 4 ? "north" : "south", Sqm = 300, Capacity = 30, X = i * 10, Z = 0, Income = 100 });
        }
        CleaningOffer first = ContractRules.Draw(state, candidates, 13, 1000);
        CleaningOffer second = ContractRules.Draw(state, candidates, 16, 1000);
        Check(first != null && second != null && first.Place != second.Place && first.District != second.District, "contracts: two open offers are in two districts while there is another district to ask in");
        Check(first.Until == 13 + ServicesCatalogue.OfferOpenDays && Array.IndexOf(ServicesCatalogue.ContractStandards, first.Standard) >= 0 && first.Fee == ContractRules.Fee(300, 30, 100, 1000),
            "contracts: an offer names a fee, one of the three standards and a last day");
        CleaningOffer third = ContractRules.Draw(state, candidates, 19, 1000);
        Check(third != null && state.Offers.Count == 3 && ContractRules.Draw(state, candidates, 22, 1000) == null && !ContractRules.OfferDue(state, 30), "contracts: three offers at most are open at once");
        var again = new ServicesState { Seed = 77 };
        ContractRules.Draw(again, new List<ContractCandidate>(), 10, 1000);
        CleaningOffer firstAgain = ContractRules.Draw(again, candidates, 13, 1000);
        Check(firstAgain != null && firstAgain.Place == first.Place && firstAgain.Standard == first.Standard, "contracts: the same save draws the same offer on the same day");
        Check(ContractRules.Expire(state, first.Until) == 0 && ContractRules.Expire(state, first.Until + 1) == 1 && state.Offer(first.Place) == null, "contracts: an offer stands up to and with its last day");

        CleaningClient client = ContractRules.Accept(state, second, 20);
        Check(client != null && client.Outside && state.Offers.Count == 1 && state.Client(second.Place) == client && client.WeekFrom == 20 && client.Threshold > client.Standard - 1
              && client.Mode == ServicesCatalogue.ModeAsNeeded && client.Force == 2, "contracts: an offer taken up is a client served as needed, its first week beginning that day");
        Check(ContractRules.Accept(state, second, 20) == null && ContractRules.Running(state) == 1, "contracts: an offer cannot be taken up twice");
        Check(ContractRules.ThresholdFor(70) == 80 && ContractRules.ThresholdFor(80) == 90 && ContractRules.ThresholdFor(90) == 90, "contracts: the threshold is set above the standard, so that a visit falls due before it is missed");

        // A week kept.
        client.Standard = 80;
        client.Dirt = 0;
        client.DirtDay = 900;
        int feeGot, penalty;
        for (int day = 21; day <= 27; day++)
        {
            ContractRules.Morning(client);
            ContractRules.Visit(client, 4000, 15);   // a visit a day that takes off what the day brought
            Check(ContractRules.Week(client, day, out feeGot, out penalty) == (day == 27 ? ContractWeek.Paid : ContractWeek.None) && (day < 27 || (feeGot == client.Fee && penalty == 0)),
                "contracts: a week with the floor kept pays the fee on its seventh day (day " + day + ")");
        }
        Check(client.Earned == client.Fee && client.Missed == 0 && client.WeekFrom == 27 && client.DaysUnder == 0, "contracts: the week is settled and the next begins");

        // A week missed, and a second that loses the contract.
        ContractWeek result = ContractWeek.None;
        for (int day = 28; day <= 34; day++)
        {
            ContractRules.Morning(client);           // nobody comes
            result = ContractRules.Week(client, day, out feeGot, out penalty);
            Check(day < 34 || (result == ContractWeek.Missed && feeGot == 0 && penalty == ServicesCatalogue.Pct(client.Fee, 25)), "contracts: a week with the floor left is missed: no fee, and a quarter of it as a penalty");
        }
        Check(client.Missed == 1 && client.Dirt == 6300, "contracts: a floor nobody cleans gains a day's dirt every morning (" + client.Dirt + ")");
        for (int day = 35; day <= 41; day++)
        {
            ContractRules.Morning(client);
            result = ContractRules.Week(client, day, out feeGot, out penalty);
        }
        Check(result == ContractWeek.Lost && client.Missed == 2 && client.Dirt == ServicesCatalogue.DirtMost, "contracts: two missed weeks running lose the contract, and a floor is never dirtier than the dirtiest");

        // Two bad mornings are allowed, a third is not.
        var edge = new CleaningClient { Outside = true, Standard = 80, Fee = 400, WeekFrom = 0, DaysUnder = ServicesCatalogue.ContractDaysUnderMost };
        Check(ContractRules.Week(edge, 7, out feeGot, out penalty) == ContractWeek.Paid, "contracts: two mornings under the standard still keep the week");
        edge.DaysUnder = ServicesCatalogue.ContractDaysUnderMost + 1;
        Check(ContractRules.Week(edge, 14, out feeGot, out penalty) == ContractWeek.Missed && penalty == 100, "contracts: a third morning under the standard misses it");
        Check(ContractRules.Week(new CleaningClient { Fee = 400 }, 99, out feeGot, out penalty) == ContractWeek.None, "contracts: a shop of the player's own has no week to settle");
        var clean = new CleaningClient { Outside = true, Standard = 90, Dirt = 400, DirtDay = 0 };
        Check(!ContractRules.Morning(clean) && clean.DaysUnder == 0, "contracts: dirt that does not show keeps any standard");
    }

    // ---------------------------------------------------------------- what a business is worth

    private static void ServicesValue()
    {
        var plain = new SaleInputs { AvgProfit7 = 1000f, DaysOnRecord = 14, Sector = (int)Sector.Retail, Income = 100, Phase = 0, Fixtures = 20000, Deposit = 3000 };
        SaleBreakdown b = SaleRules.Value(plain);
        Check(b.Base == 1000 * VenturesCatalogue.OfferValueDays && b.Base == 60000, "value: the base is the investors' valuation, sixty days of the recent daily profit");
        Check(b.TypePct == 100 && b.DistrictPct == 100 && b.ClimatePct == 100 && b.HistoryPct == 100 && b.Goodwill == 60000 && b.Fixtures == 20000 && b.Value == 80000 && b.Net == 80000 && b.Deposit == 3000,
            "value: a plain shop in a middling district in steady times is worth its base and its stock and furniture");

        var better = plain;
        better.Sector = (int)Sector.Office;
        better.Income = 140;
        better.Phase = 1;
        SaleBreakdown rich = SaleRules.Value(better);
        Check(rich.TypePct == 110 && rich.DistrictPct == 120 && rich.ClimatePct == 112 && rich.Goodwill == ServicesCatalogue.Round10(60000L * 110 / 100 * 120 / 100 * 112 / 100),
            "value: an office in a rich district in a boom is worth more, line by line (" + rich.Goodwill + ")");
        var worse = plain;
        worse.Sector = (int)Sector.Industry;
        worse.Income = 60;
        worse.Phase = -1;
        SaleBreakdown poor = SaleRules.Value(worse);
        Check(poor.TypePct == 90 && poor.DistrictPct == 80 && poor.ClimatePct == 85 && poor.Goodwill < b.Goodwill && poor.Fixtures == b.Fixtures, "value: the stock and furniture are worth the same whatever the times");
        Check(SaleRules.DistrictPct(200) == ServicesCatalogue.DistrictMostPct && SaleRules.DistrictPct(0) == ServicesCatalogue.DistrictLeastPct, "value: a district counts within bounds");

        // No history, a little history, a loss.
        var fresh = plain;
        fresh.DaysOnRecord = 0;
        fresh.AvgProfit7 = 0f;
        SaleBreakdown opened = SaleRules.Value(fresh);
        Check(opened.Goodwill == 0 && opened.Value == 20000 && opened.HistoryPct == 0, "value: a business with no books is worth its stock and furniture and nothing more");
        var young = plain;
        young.DaysOnRecord = 3;
        Check(SaleRules.Value(young).HistoryPct == 42 && SaleRules.Value(young).Goodwill == ServicesCatalogue.Round10(60000L * 42 / 100), "value: three days of books count for three sevenths");
        var losing = plain;
        losing.AvgProfit7 = -500f;
        Check(SaleRules.Value(losing).Goodwill == 0 && SaleRules.Value(losing).Value == 20000, "value: a business that loses money has no goodwill, and is never worth less than its things");
        var empty = new SaleInputs();
        Check(SaleRules.Value(empty).Value == 0 && SaleRules.Quick(0) == 0 && SaleRules.Asking(0, 6) == 0, "value: nothing at all is worth nothing");

        // Investors and loans.
        var owing = plain;
        owing.Investors = 15000;
        owing.Loans = 30000;
        SaleBreakdown owed = SaleRules.Value(owing);
        Check(owed.Value == 80000 && owed.Net == 35000 && owed.Investors == 15000 && owed.Loans == 30000, "value: investors and lenders do not change the value, only what is left of it");
        var drowned = plain;
        drowned.Loans = 120000;
        Check(SaleRules.Value(drowned).Net == -40000, "value: a business in debt beyond its worth leaves less than nothing");

        // Asking prices and the quick sale.
        Check(SaleRules.Asking(80000, 0) == 64000 && SaleRules.Asking(80000, 2) == 80000 && SaleRules.Asking(80000, 6) == 120000 && SaleRules.Asking(80000, 99) == 120000 && SaleRules.Asking(80000, -5) == 64000,
            "value: the asking prices run from four fifths to one and a half times the value");
        Check(SaleRules.Quick(80000) == 56000 && SaleRules.Quick(80000) < SaleRules.Asking(80000, 0), "value: a quick sale pays seven tenths, less than the lowest asking price");
        Check(SaleRules.Asking(12345, 2) == 12340, "value: a price is a round figure");
    }

    // ---------------------------------------------------------------- the market

    private static void ServicesMarket()
    {
        var state = new ServicesState { Seed = 4242 };
        SaleListing listing = SaleRules.List(state, "shop~1", "Corner Shop", 80000, 2, 100);
        Check(listing != null && listing.Asking == 80000 && listing.NextOfferDay == 101 && state.Listing("shop~1") == listing, "market: a business listed today can have its first offer tomorrow");
        Check(!SaleRules.OfferDue(listing, 100) && SaleRules.OfferDue(listing, 101), "market: no offer on the day of listing");
        var offers = new List<SaleOffer>();
        for (int day = 101; day <= 130; day++)
        {
            SaleRules.ExpireOffers(state, day);
            if (SaleRules.OfferDue(listing, day))
            {
                SaleOffer o = SaleRules.Offer(state, listing, day);
                if (o != null)
                {
                    offers.Add(o);
                }
            }
        }
        Check(offers.Count == ServicesCatalogue.AskingOffersMost[2] && listing.OffersMade == 4, "market: at the value four offers come, one every other day (" + offers.Count + ")");
        bool inBounds = true, distinct = true, termsRight = true;
        for (int i = 0; i < offers.Count; i++)
        {
            SaleOffer o = offers[i];
            int low = ServicesCatalogue.Pct(80000, ServicesCatalogue.BuyerPct[o.Buyer] - ServicesCatalogue.JitterPct) - 10, high = ServicesCatalogue.Pct(80000, ServicesCatalogue.BuyerPct[o.Buyer] + ServicesCatalogue.JitterPct);
            inBounds &= o.Price >= Math.Min(low, listing.Asking) && o.Price <= Math.Min(high, listing.Asking) && o.Ceiling >= o.Price && o.Price % 10 == 0;
            inBounds &= o.Deadline - (101 + 2 * i) >= ServicesCatalogue.OfferDaysLeast && o.Deadline - (101 + 2 * i) <= ServicesCatalogue.OfferDaysMost;
            termsRight &= o.KeepsStaff == ServicesCatalogue.BuyerKeepsStaff[o.Buyer] && o.ClauseDays == ServicesCatalogue.BuyerClauseDays[o.Buyer]
                          && (o.Parts == ServicesCatalogue.BuyerInstalments[o.Buyer] || (o.Buyer == (int)BuyerKind.Chain && o.Parts == 0));
            for (int k = 0; k < i; k++)
            {
                distinct &= offers[k].Buyer != o.Buyer && offers[k].Id != o.Id;
            }
        }
        Check(inBounds, "market: every offer is its buyer's share of the value, give or take a twentieth, never above the asking price, with three to five days to answer");
        Check(termsRight, "market: each kind of buyer comes with its own terms: the staff, the parts of the price, the clause");
        Check(distinct, "market: four offers come from the four kinds of buyer");
        Check(!SaleRules.OfferDue(listing, 200), "market: a listing that has had its offers draws no more");
        Check(!SaleRules.Expired(listing, 130) && SaleRules.Expired(listing, 131), "market: a listing ends by itself after thirty days");

        // The same save, the same market.
        var again = new ServicesState { Seed = 4242 };
        SaleListing listingAgain = SaleRules.List(again, "shop~1", "Corner Shop", 80000, 2, 100);
        SaleOffer firstAgain = SaleRules.Offer(again, listingAgain, 101);
        Check(firstAgain != null && firstAgain.Buyer == offers[0].Buyer && firstAgain.Price == offers[0].Price && firstAgain.Deadline == offers[0].Deadline && firstAgain.Ceiling == offers[0].Ceiling,
            "market: the same save always gets the same offers");
        var other = new ServicesState { Seed = 4243 };
        SaleListing listingOther = SaleRules.List(other, "shop~1", "Corner Shop", 80000, 2, 100);
        SaleOffer firstOther = SaleRules.Offer(other, listingOther, 101);
        Check(firstOther == null || firstOther.Buyer != offers[0].Buyer || firstOther.Price != offers[0].Price || firstOther.Ceiling != offers[0].Ceiling, "market: another save gets another market");

        // The asking price changes how many come and how fast.
        int[] counts = new int[ServicesCatalogue.AskingPct.Length];
        int[] firstTwo = new int[ServicesCatalogue.AskingPct.Length];
        long cheapSum = 0, dearSum = 0;
        for (int step = 0; step < counts.Length; step++)
        {
            var s = new ServicesState { Seed = 9 };
            SaleListing l = SaleRules.List(s, "shop~2", "Shop", 100000, step, 0);
            int lastDay = 0;
            for (int day = 1; day <= ServicesCatalogue.ListingDays; day++)
            {
                if (SaleRules.OfferDue(l, day))
                {
                    SaleOffer o = SaleRules.Offer(s, l, day);
                    if (o != null)
                    {
                        counts[step]++;
                        firstTwo[step] = counts[step] == 2 ? day : firstTwo[step];
                        lastDay = day;
                        cheapSum += step == 0 ? o.Price : 0;
                        dearSum += step == 4 ? o.Price : 0;
                        Check(o.Price <= l.Asking, "market: an offer at asking step " + step + " is not above the asking price");
                    }
                }
            }
            Check(lastDay <= ServicesCatalogue.ListingDays, "market: offers come within the listing's days");
        }
        Check(counts[0] == 5 && counts[2] == 4 && counts[3] == 3 && counts[4] == 2 && counts[5] <= 1 && counts[6] <= 1, "market: the higher the asking price, the fewer offers (" + string.Join(",", counts) + ")");
        Check(firstTwo[0] == 2 && firstTwo[2] == 3 && firstTwo[3] == 4 && firstTwo[4] == 5, "market: the higher the asking price, the longer between offers (" + string.Join(",", firstTwo) + ")");
        Check(dearSum / 2 > cheapSum / 5, "market: offers follow the asking price part of the way (" + cheapSum / 5 + " at 80%, " + dearSum / 2 + " at 120%)");

        // Nothing to sell, and no room.
        var poor = new ServicesState { Seed = 1 };
        SaleListing worthless = SaleRules.List(poor, "x", "X", 0, 2, 0);
        Check(!SaleRules.OfferDue(worthless, 5) && SaleRules.Offer(poor, worthless, 5) == null && poor.SaleOffers.Count == 0, "market: nobody offers for a business worth nothing");
        SaleListing relisted = SaleRules.List(state, "shop~1", "Corner Shop", 90000, 4, 140);
        Check(relisted == listing && relisted.Asking == 108000 && relisted.ListedDay == 100 && state.Listings.Count == 1, "market: a new asking price changes the listing, it does not start a second one");
        state.Unlist("shop~1");
        Check(state.Listings.Count == 0 && state.OffersFor("shop~1") == 0, "market: a business taken off the market takes its offers with it");
    }

    private static void ServicesCounter()
    {
        var state = new ServicesState { Seed = 5 };
        var met = new SaleOffer { Id = 1, Price = 50000, Ceiling = 60000 };
        state.SaleOffers.Add(met);
        Check(SaleRules.Counter(state, met, 80000) == CounterResult.Accepted && met.Price == 55000 && met.Countered, "counter: a buyer who would pay a tenth more pays it");
        Check(SaleRules.Counter(state, met, 80000) == CounterResult.Refused && met.Price == 55000, "counter: an offer is countered once");

        var half = new SaleOffer { Id = 2, Price = 50000, Ceiling = 53000 };
        state.SaleOffers.Add(half);
        Check(SaleRules.Counter(state, half, 80000) == CounterResult.HalfWay && half.Price == 52500, "counter: a buyer who would pay half of it meets half way");

        var capped = new SaleOffer { Id = 3, Price = 50000, Ceiling = 90000 };
        state.SaleOffers.Add(capped);
        Check(SaleRules.Counter(state, capped, 52000) == CounterResult.Accepted && capped.Price == 52000, "counter: a counter never asks more than the asking price");

        var atAsking = new SaleOffer { Id = 4, Price = 50000, Ceiling = 90000 };
        state.SaleOffers.Add(atAsking);
        CounterResult none = SaleRules.Counter(state, atAsking, 50000);
        Check((none == CounterResult.Refused || none == CounterResult.Walked) && atAsking.Price == 50000, "counter: an offer already at the asking price has nothing to give");

        int walked = 0, refused = 0;
        for (int i = 0; i < 400; i++)
        {
            var s = new ServicesState { Seed = 1000 + i };
            var firm = new SaleOffer { Id = 10 + i, Price = 50000, Ceiling = 50000 };
            s.SaleOffers.Add(firm);
            CounterResult r = SaleRules.Counter(s, firm, 80000);
            walked += r == CounterResult.Walked ? 1 : 0;
            refused += r == CounterResult.Refused ? 1 : 0;
            Check(firm.Price == 50000 && (r == CounterResult.Walked) == (s.SaleOffers.Count == 0), "counter: a buyer at their ceiling either stays at their price or is gone");
        }
        Check(walked + refused == 400 && walked > 60 && walked < 140, "counter: about one buyer in four walks away from a counter they will not meet (" + walked + " of 400)");
        Check(SaleRules.Counter(state, null, 1) == CounterResult.Refused, "counter: no offer, no counter");
    }

    // ---------------------------------------------------------------- the money on the day

    private static void ServicesMoney()
    {
        var b = new SaleBreakdown { Fixtures = 20000, Value = 80000 };
        SaleMoney atOnce = SaleRules.Settle(76000, 0, b, 500);
        Check(atOnce.Now == 76000 && atOnce.PartsLeft == 0 && atOnce.Left == 76000 && atOnce.Short == 0 && atOnce.Gain == 56000, "money: a price paid at once is all there on the day; the gain is the price above the things");

        SaleMoney inParts = SaleRules.Settle(65610, 4, b, 0);
        Check(inParts.Part == 16402 && inParts.PartsLeft == 3 && inParts.Now == 16404 && inParts.Now + inParts.Part * inParts.PartsLeft == 65610, "money: a price in four parts adds up to the price, the first part carrying what does not divide");

        b.Investors = 15000;
        b.Loans = 30000;
        SaleMoney owing = SaleRules.Settle(76000, 0, b, 0);
        Check(owing.Investors == 15000 && owing.Loans == 30000 && owing.Left == 31000 && owing.Short == 0, "money: investors and lenders are paid out of the proceeds first");

        SaleMoney thin = SaleRules.Settle(65610, 4, b, 10000);
        Check(thin.Left == 16404 - 45000 && thin.Short == 45000 - 16404 - 10000, "money: when the first part does not cover them, the seller's cash must, and what that leaves short is said");
        SaleMoney covered = SaleRules.Settle(65610, 4, b, 40000);
        Check(covered.Short == 0 && covered.Left < 0, "money: with the cash there, a sale in parts completes and the difference is taken from the cash");

        b.Loans = 120000;
        SaleMoney drowned = SaleRules.Settle(80000, 0, b, 5000);
        Check(drowned.Short == 135000 - 80000 - 5000 && drowned.Left == -55000, "money: a business in debt beyond its worth cannot be sold without the cash to clear it");
        Check(SaleRules.Settle(80000, 0, b, 55000).Short == 0, "money: ...and can with it");
        Check(SaleRules.Settle(15000, 0, new SaleBreakdown { Fixtures = 20000 }, 0).Gain == 0 && SaleRules.Settle(-5, 0, new SaleBreakdown(), 0).Now == 0, "money: a sale under the worth of the things is no gain, and no price is under nothing");

        // The parts still to come.
        var state = new ServicesState { Seed = 1 };
        SaleDue due = SaleRules.Due(state, "Corner Shop", (int)BuyerKind.Staff, inParts, 50);
        Check(due != null && due.Left == 3 && due.NextDay == 57 && state.Dues.Count == 1 && SaleRules.Due(state, "x", 0, atOnce, 50) == null, "money: the parts of a price fall due a week apart; a price paid at once leaves none");
        int got = 0, days = 0;
        for (int day = 51; day <= 80; day++)
        {
            int part = SaleRules.TakeDue(due, day);
            got += part;
            days += part > 0 ? 1 : 0;
        }
        Check(got == 3 * 16402 && days == 3 && due.Left == 0 && SaleRules.TakeDue(due, 99) == 0, "money: three more parts come on days 57, 64 and 71, and then no more");
        var late = new SaleDue { Part = 100, Left = 2, NextDay = 10 };
        Check(SaleRules.TakeDue(late, 30) == 100 && late.NextDay == 31 && SaleRules.TakeDue(late, 31) == 100 && late.Left == 0, "money: parts missed while the feature was off are paid one a day from the first day back");

        // The tax return's figure.
        SaleRules.Record(state, "Corner Shop", 76000, 56000, 50);
        SaleRules.Record(state, "Bakery", 30000, 4000, 61);
        Check(SaleRules.Gains(state, 50, 60) == 56000 && SaleRules.Gains(state, 51, 61) == 4000 && SaleRules.Gains(state, 0, 999) == 60000 && SaleRules.Gains(state, 62, 70) == 0 && SaleRules.Gains(null, 0, 9) == 0,
            "money: the capital gains of a span of days are those of the sales completed in it");
        for (int i = 0; i < 40; i++)
        {
            SaleRules.Record(state, "S" + i, 10, 1, 100 + i);
        }
        Check(state.Records.Count == ServicesCatalogue.RecordsKept && state.Records[0].Day == 116, "money: the oldest notes of sales go when there are too many");
    }

    private static void ServicesClause()
    {
        var state = new ServicesState { Seed = 1 };
        Check(SaleRules.Clause(state, "ba:businesstype_giftshop", "north", "Gifts", 50000, 0, 10) == null && state.Clauses.Count == 0, "clause: a buyer who asks for none leaves none");
        SaleClause c = SaleRules.Clause(state, "ba:businesstype_giftshop", "north", "Gifts", 50000, 30, 10);
        Check(c != null && c.Until == 40 && state.Clauses.Count == 1, "clause: thirty days from the day of the sale");
        Check(SaleRules.Breaks(c, "ba:businesstype_giftshop", "north", 25), "clause: the same trade in the same district within the days breaks it");
        Check(!SaleRules.Breaks(c, "ba:businesstype_giftshop", "south", 25) && !SaleRules.Breaks(c, "ba:businesstype_coffeeshop", "north", 25), "clause: another district or another trade does not");
        Check(!SaleRules.Breaks(c, "ba:businesstype_giftshop", "north", 10) && !SaleRules.Breaks(c, "ba:businesstype_giftshop", "north", 5) && !SaleRules.Breaks(c, "ba:businesstype_giftshop", "north", 41),
            "clause: a business the seller already had, or one opened after the days, does not");
        Check(SaleRules.ClausePenalty(50000) == 7500 && SaleRules.ClausePenalty(0) == 0, "clause: breaking it costs fifteen per cent of the price");
        Check(SaleRules.ExpireClauses(state, 40) == 0 && SaleRules.ExpireClauses(state, 41) == 1 && state.Clauses.Count == 0, "clause: it lapses the day after its last");
    }

    // ---------------------------------------------------------------- the save

    private static void ServicesSave()
    {
        var s = new ServicesState { Seed = 31337, FirstDay = 3, LastDay = 44, NextId = 9, Van = true, LastOfferDay = 40, VanMetres = 1234 };
        s.VanCost[2] = 17;
        s.FeeIncome[5] = -40;
        s.Crew.Add("abc+/=");
        s.Crew.Add("def");
        var shop = new CleaningClient { Place = "Main%20St~12", Name = "Corner|Shop;^", District = "ba:neighborhood_midtown", X = -120, Z = 455, Mode = 2, Threshold = 70, NeedDay = 2100, WorstStart = 800,
            CleanedToday = 300, MinutesToday = 25, MinutesBefore = 40, CleanBefore = 77, LastDay = 43, LastMinute = 14 * 60, LastCleaner = "Ann" };
        shop.Hours[0] = 7;
        shop.Hours[1] = 13;
        shop.Hours[2] = 22;
        s.Clients.Add(shop);
        s.Clients.Add(new CleaningClient { Place = "Side~3", Name = "Bank", Outside = true, Dirt = 1500, DirtDay = 900, Sqm = 350, Capacity = 20, Fee = 420, Standard = 80, WeekFrom = 41, DaysUnder = 1, Missed = 1, Earned = 840 });
        s.Offers.Add(new CleaningOffer { Place = "Low~9", Name = "Deli", District = "d", Sqm = 150, Capacity = 12, X = 3, Z = -4, Fee = 200, Standard = 90, Until = 47 });
        s.Listings.Add(new SaleListing { Place = "Main%20St~12", Name = "Corner Shop", ListedDay = 42, Step = 3, Asking = 88000, Value = 80000, NextOfferDay = 45, OffersMade = 1 });
        s.SaleOffers.Add(new SaleOffer { Id = 8, Place = "Main%20St~12", Buyer = 3, Price = 86000, Ceiling = 93000, KeepsStaff = false, Parts = 2, ClauseDays = 60, Deadline = 47, Countered = true, Face = 2 });
        s.Dues.Add(new SaleDue { Name = "Old Shop", Buyer = 2, Part = 5000, Left = 2, NextDay = 46 });
        s.Clauses.Add(new SaleClause { Type = "ba:businesstype_giftshop", District = "d", Name = "Old Shop", FromDay = 30, Until = 60, Price = 40000 });
        s.Records.Add(new SaleRecord { Day = 30, Name = "Old Shop", Price = 40000, Gain = 25000 });

        string line = ServicesStore.Write(s);
        int version;
        ServicesState r = ServicesStore.Read(line, out version);
        Check(r != null && version == ServicesStore.Version && ServicesStore.Write(r) == line, "save: what is read back writes the same line again");
        Check(r.Seed == 31337 && r.FirstDay == 3 && r.LastDay == 44 && r.NextId == 9 && r.Van && r.LastOfferDay == 40 && r.VanMetres == 1234 && r.VanCost[2] == 17 && r.FeeIncome[5] == -40, "save: the header");
        Check(r.Crew.Count == 2 && r.Crew[0] == "abc+/=" && r.InCrew("def") && !r.InCrew("x") && !r.InCrew(null), "save: the crew, with an id as the game makes them");
        CleaningClient back = r.Client("Main%20St~12");
        Check(back != null && back.Name == "Corner Shop  " && back.X == -120 && back.Z == 455 && back.Mode == 2 && back.Hours[0] == 7 && back.Hours[2] == 22 && back.Threshold == 70 && back.NeedDay == 2100
              && back.CleanBefore == 77 && back.LastMinute == 840 && back.LastCleaner == "Ann" && !back.Outside, "save: a shop on the round, its name with the dividers taken out");
        CleaningClient bank = r.Client("Side~3");
        Check(bank != null && bank.Outside && bank.Dirt == 1500 && bank.Fee == 420 && bank.Standard == 80 && bank.WeekFrom == 41 && bank.DaysUnder == 1 && bank.Missed == 1 && bank.Earned == 840 && bank.Force == 2,
            "save: a client under contract, its floor and its terms");
        Check(r.Offers.Count == 1 && r.Offer("Low~9").Until == 47 && r.Offer("Low~9").Z == -4, "save: a contract offer");
        Check(r.Listing("Main%20St~12").Asking == 88000 && r.Listing("Main%20St~12").Step == 3 && r.SaleOffer(8).Ceiling == 93000 && r.SaleOffer(8).Countered && r.SaleOffer(8).Parts == 2 && r.OffersFor("Main%20St~12") == 1,
            "save: a business on the market and an offer for it");
        Check(r.Dues.Count == 1 && r.Dues[0].Left == 2 && r.Clauses.Count == 1 && r.Clauses[0].Until == 60 && r.Records.Count == 1 && SaleRules.Gains(r, 0, 99) == 25000, "save: parts to come, a clause and a completed sale");

        // A fresh state, a later version, rubbish.
        var blank = new ServicesState { Seed = 1 };
        ServicesState blankBack = ServicesStore.Read(ServicesStore.Write(blank), out version);
        Check(blankBack != null && blankBack.Clients.Count == 0 && blankBack.Crew.Count == 0 && blankBack.NextId == 1 && !blankBack.Van, "save: an empty state loads as an empty state");
        ServicesState later = ServicesStore.Read(line.Replace("v=1", "v=7") + "|99|98^z:something new;else^k:late", out version);
        Check(later != null && version == 7 && later.Clients.Count == 2 && later.Crew.Contains("late"), "save: a line written by a later version loads: unknown parts and extra fields are skipped");
        Check(ServicesStore.Read("", out version) == null && ServicesStore.Read("nonsense", out version) == null && ServicesStore.Read("v=x^h:1", out version) == null && ServicesStore.Read("v=1^c:a|b", out version) == null,
            "save: a line that is not this feature's, or has no header, is no state");
        ServicesState bent = ServicesStore.Read("v=1^h:5|-3|-9|0|1|-2|-7^c:p|n|d|0|1|2|9|99|-4|30|500|-8|99999^f:0|p|9|5|1|0|0|0|0|0|0;3|p|1|-5|1|1|40|-2|3|1|1", out version);
        Check(bent != null && bent.FirstDay == 0 && bent.NextId == 1 && bent.Clients[0].Mode == 3 && bent.Clients[0].Hours[0] == 23 && bent.Clients[0].Hours[1] == 0 && bent.Clients[0].Threshold == 100
              && bent.Clients[0].NeedDay == -1 && bent.Clients[0].WorstStart == ServicesCatalogue.DirtMost && bent.SaleOffers.Count == 1 && bent.SaleOffers[0].Price == 0 && bent.SaleOffers[0].Parts == 12,
            "save: a figure out of its bounds is put inside them, and an offer from no kind of buyer is dropped");
        var data = new Dictionary<string, string>();
        Check(ServicesStore.Load(data, out version) == null && ServicesStore.Load(null, out version) == null, "save: a save made without the feature has no state, and that is not an error");
        data[ServicesStore.Key] = line;
        Check(ServicesStore.Load(data, out version) != null && ServicesStore.Key == "reallife:economy_services", "save: the state is under its own key");
        Check(ServicesState.Week(s.VanCost) == 17 && ServicesState.Week(s.FeeIncome) == -40, "save: a week's figure is the sum of its seven days");
    }

    // ---------------------------------------------------------------- how long the largest round takes

    private static void ServicesTiming()
    {
        // The most there can be: 32 clients with three visits a day each, 12 cleaners, across a city of two kilometres.
        var clients = new List<CleaningClient>();
        for (int i = 0; i < ServicesCatalogue.ClientsMost; i++)
        {
            CleaningClient c = SvcShop("c" + i, (i * 137) % 900 - 450, (i * 241) % 1900 - 950, 3, 500 + (i * 97) % 3000, 70);
            c.Hours[0] = 6 + i % 4;
            c.Hours[1] = 12 + i % 5;
            c.Hours[2] = 18 + i % 4;
            c.NeedDay = 2000 + (i * 53) % 2000;
            c.Force = 1 + i % 3;
            clients.Add(c);
        }
        var jobs = new List<RoundJob>();
        var visits = new List<RoundVisit>();
        var cache = new TravelCache();
        List<RoundCleaner> cleaners = null;
        double worst = 0;
        var watch = new Stopwatch();
        for (int run = 0; run < 30; run++)
        {
            cleaners = new List<RoundCleaner>();
            for (int i = 0; i < ServicesCatalogue.CrewMost; i++)
            {
                cleaners.Add(SvcCleaner("w" + i, 5 + i % 3, 20 + i % 4, (i * 311) % 800 - 400, (i * 173) % 1600 - 800, 30 + i * 5, 60 + i * 3));
            }
            visits.Clear();
            watch.Restart();
            RoundRules.Jobs(clients, 0, jobs);
            RoundRules.Plan(cleaners, jobs, cache, true, 0, visits);
            watch.Stop();
            worst = run >= 10 ? Math.Max(worst, watch.Elapsed.TotalMilliseconds) : worst;   // (the first runs let the runtime compile it)
        }
        Check(jobs.Count == 96 && visits.Count > 40, "timing: the largest day has 96 visits due, and most are taken by van (" + visits.Count + ")");
        Check(SvcWrong(cleaners, jobs, visits, true) == null, "timing: the largest round keeps the rules (" + SvcWrong(cleaners, jobs, visits, true) + ")");
        Check(worst < 50.0, "timing: the largest round is planned in under 50 ms (" + worst.ToString("0.00") + " ms)");
        Check(cache.Count <= (96 + 12) * (96 + 12) && cache.Hits > cache.Misses, "timing: distances are asked for far more often than they are worked out (" + cache.Misses + " worked out, " + cache.Hits + " kept)");
    }
}
