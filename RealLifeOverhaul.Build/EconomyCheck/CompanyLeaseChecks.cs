using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Economy;

// Leases on rented premises and the two new kinds of manager's decision that go with them (the Company app on the kit): a term and a renewal day over the
// game's rent, the landlord's offer fourteen days before, an answer by the owner or by a manager inside a rise limit, what it adds to the rent, and the city's
// own leases renewing by the same rule from a seed alone. And a manager's marketing inside a month's limit.
internal static partial class Program
{
    private static void CompanyLeases()
    {
        int before = _run, failedBefore = _failed;
        LeaseTerms();
        LeaseOffers();
        LeaseAnswers();
        LeaseYears();
        LeaseCity();
        LeaseSaves();
        ManagerMarketing();
        Console.WriteLine("  leases and the manager's marketing and lease answers: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    private const int Seed = 4242, MonthDays = 30;

    private static void LeaseTerms()
    {
        bool allowed = true, same = true;
        var seen = new HashSet<int>();
        for (int p = 0; p < 200; p++)
        {
            string place = "Street" + p + "~" + (p * 7 % 90);
            int term = LeaseRules.Term(Seed, place, 0, MonthDays);
            allowed &= Array.IndexOf(LeaseRules.TermMonths, term / MonthDays) >= 0 && term % MonthDays == 0 && term >= MonthDays;
            same &= term == LeaseRules.Term(Seed, place, 0, MonthDays);
            seen.Add(term / MonthDays);
        }
        Check(allowed && same, "lease term: always one of 6, 12, 24 or 36 months of the game's days, and the same twice for a place");
        Check(seen.Count == 4, "lease term: a seeded spread, so premises do not all renew on one day (" + seen.Count + " different lengths in 200 places)");
        Check(LeaseRules.Term(Seed, "a~1", 0, 60) == 2 * LeaseRules.Term(Seed, "a~1", 0, 30), "lease term: it follows the game's month, whatever the year the player chose");
        Check(LeaseRules.Term(Seed, "a~1", 0, 0) >= 1, "lease term: a month of no days is still a day");
        LeaseFile begun = LeaseRules.Begin(Seed, "Main~1", 120, 50, MonthDays);
        Check(begun.Start == 50 && begun.Rent == 120 && begun.Term == LeaseRules.Term(Seed, "Main~1", 0, MonthDays) && begun.End == 50 + begun.Term && begun.Offer == 0 && begun.Rise == 0,
            "lease begin: an old save's premises get a lease starting on the day it loads, a seeded term, no rise yet");
    }

    private static void LeaseOffers()
    {
        // The rise: the city's trend, the landlord's own draw around it, a share of a rival's buying up; bounded.
        bool inRange = true, same = true, spread = true;
        int lowest = int.MaxValue, highest = int.MinValue;
        for (int n = 1; n <= 300; n++)
        {
            int rise = LeaseRules.Rise(Seed, "Main~" + n, n, 50, 0);
            inRange &= rise >= 0 && rise <= LeaseRules.RiseMost;
            same &= rise == LeaseRules.Rise(Seed, "Main~" + n, n, 50, 0);
            lowest = Math.Min(lowest, rise);
            highest = Math.Max(highest, rise);
        }
        spread = lowest >= 50 + LeaseRules.SpreadLow && highest <= 50 + LeaseRules.SpreadHigh && highest > lowest;
        Check(inRange && same && spread, "lease rise: always inside the bounds, the same twice, and a landlord's own draw stays " + LeaseRules.SpreadLow + " to +" + LeaseRules.SpreadHigh + " around the trend ("
                                         + lowest + " to " + highest + ")");
        Check(LeaseRules.Rise(Seed, "a~1", 1, 0, 0) >= 0 && LeaseRules.Rise(Seed, "a~1", 1, -500, 0) >= 0, "lease rise: never under nothing, whatever the trend");
        Check(LeaseRules.Rise(Seed, "a~1", 1, 5000, 5000) == LeaseRules.RiseMost, "lease rise: never over " + LeaseRules.RiseMost + " per mille");
        int calm = LeaseRules.Rise(Seed, "a~1", 2, 60, 0), pressed = LeaseRules.Rise(Seed, "a~1", 2, 60, 80);
        Check(pressed == Math.Min(LeaseRules.RiseMost, calm + 80 * LeaseRules.PressureShare / 1000), "lease rise: a rival buying up the district adds its share of what it adds, and nothing else changes");
        Check(LeaseRules.Rise(Seed, "a~1", 2, 60, -80) == calm, "lease rise: a pressure under nothing adds nothing");
        // The offer is the rent's rise in all over the game's rent.
        var l = new LeaseFile { Place = "Main~1", Start = 100, Term = 360, Rent = 200, Rise = 40 };
        int offer = LeaseRules.Offer(l, Seed, 30, 0);
        Check(offer == 40 + LeaseRules.Rise(Seed, "Main~1", 1, 30, 0) && offer >= 40, "lease offer: what is agreed already and the rise asked now (" + offer + ")");
        l.Offer = 120;
        Check(LeaseRules.RentAt(200, 120) == 224 && LeaseRules.RentAt(200, 0) == 200 && LeaseRules.RentAt(0, 500) == 0 && LeaseRules.RentAt(200, -50) == 200,
            "lease rent: the game's rent and the rise over it, whole money");
        Check(LeaseRules.OfferOver(l) == (int)((1120L - 1040) * 1000 / 1040), "lease offer: the page says how much more than the rent paid now (" + LeaseRules.OfferOver(l) + ")");
        l.Rise = 0;
        l.Offer = 80;
        Check(LeaseRules.OfferOver(l) == 80, "lease offer: 8% over a rent with no rise is 8%");
        Check(LeaseRules.Within(l, 80) && !LeaseRules.Within(l, 79) && LeaseRules.Within(l, 200), "manager limit: an offer is theirs to answer up to their limit and not a thousandth past it");
        l.Offer = 0;
        Check(!LeaseRules.Within(l, 1000), "manager limit: no offer, nothing to answer");
        // The day the offer is made: inside the notice, once.
        var due = new LeaseFile { Place = "Main~1", Start = 100, Term = 360 };
        Check(!LeaseRules.OfferDue(due, due.End - LeaseRules.NoticeDays - 1) && LeaseRules.OfferDue(due, due.End - LeaseRules.NoticeDays) && LeaseRules.OfferDue(due, due.End - 1)
              && !LeaseRules.OfferDue(due, due.End), "lease offer: made from fourteen days before the end, never earlier and never after it");
        due.Offer = 50;
        due.Asked = 300;
        Check(!LeaseRules.OfferDue(due, due.End - 5), "lease offer: made once");
        var shortLease = new LeaseFile { Place = "Main~1", Start = 100, Term = 10 };
        Check(LeaseRules.OfferDue(shortLease, 100) && !LeaseRules.OfferDue(shortLease, 99), "lease offer: a term shorter than the notice is asked for from its first day");
    }

    private static void LeaseAnswers()
    {
        LeaseFile Make()
        {
            return new LeaseFile { Place = "Main~1", Start = 100, Term = 360, Rent = 200, Rise = 0, Offer = 100, Asked = 446 };
        }
        // Accepted: nothing changes until the lease ends, then a new term over the agreed rise.
        LeaseFile a = Make();
        Check(LeaseRules.Waiting(a, 450) && LeaseRules.Respond(a, 450, true) && a.Answer == LeaseRules.Accepted && !LeaseRules.Waiting(a, 450), "lease answer: an offer waits for an answer, and an answer closes the question");
        Check(a.Rise == 0 && LeaseRules.TopUp(a) == 0 && !LeaseRules.Settle(a, 459, Seed, MonthDays), "lease answer: the rent runs as it does until the lease ends");
        Check(LeaseRules.Settle(a, 460, Seed, MonthDays), "lease answer: on its last day it is applied");
        Check(a.Rise == 100 && a.Renewals == 1 && a.Start == 460 && a.Term == LeaseRules.Term(Seed, "Main~1", 1, MonthDays) && a.Offer == 0 && a.Asked == 0 && a.Answer == 0 && !a.Hold && a.Last == 100,
            "lease answer: accepted, the rise is agreed, a new term begins where the old one ended, and the offer is spent (" + a.Rise + ")");
        Check(LeaseRules.TopUp(a) == 20, "lease answer: the rise is paid a day on top of the game's rent (200 at 10%)");
        // Declined: month to month at a premium over the rent paid now, asked again at the month's end.
        LeaseFile d = Make();
        d.Rise = 20;
        LeaseRules.Respond(d, 450, false);
        Check(LeaseRules.Settle(d, 460, Seed, MonthDays) && d.Hold && d.Rise == 20 && d.Renewals == 0 && d.Term == MonthDays && d.Start == 460 && d.Offer == 0,
            "lease answer: declined, the tenant stays month to month, the rise unchanged, a month's term");
        Check(d.Running == 20 + LeaseRules.HoldPremium && LeaseRules.TopUp(d) == 200 * (20 + LeaseRules.HoldPremium) / 1000, "lease answer: month to month pays a premium over the rent now");
        d.Offer = 70;
        d.Asked = d.End - 5;
        LeaseRules.Respond(d, d.End - 5, true);
        LeaseRules.Settle(d, d.End, Seed, MonthDays);
        Check(!d.Hold && d.Rise == 70 && d.Renewals == 1, "lease answer: a hold-over's next offer, accepted, ends the premium");
        // Unanswered: the landlord renews at their own figure.
        LeaseFile u = Make();
        Check(!LeaseRules.Settle(u, 459, Seed, MonthDays) && LeaseRules.Settle(u, 460, Seed, MonthDays) && u.Rise == 100 && u.Renewals == 1 && !u.Hold,
            "lease answer: left unanswered, the landlord renews at their own figure on the day");
        LeaseFile late = Make();
        Check(LeaseRules.Settle(late, 9000, Seed, MonthDays) && late.Start == 9000, "lease answer: days skipped past the whole new term start it today, never in the past");
        LeaseFile none = Make();
        none.Offer = 0;
        Check(!LeaseRules.Respond(none, 450, true) && !LeaseRules.Settle(none, 9000, Seed, MonthDays), "lease answer: no offer, no answer and nothing to settle");
        LeaseFile after = Make();
        Check(!LeaseRules.Respond(after, 460, true), "lease answer: none after the lease has ended");
        // Asking for less: once, deterministic, and only ever halves the rise.
        LeaseFile h = Make();
        bool came = LeaseRules.Haggle(h, Seed, 450);
        Check(h.Haggled && (came ? h.Offer == 50 : h.Offer == 100) && !LeaseRules.CanHaggle(h, 450) && !LeaseRules.Haggle(h, Seed, 451), "lease haggle: asked once, whatever the answer, and never again at this renewal");
        int comes = 0, total = 0;
        for (int day = 100; day < 700; day++)
        {
            var t = new LeaseFile { Place = "Main~" + day % 50, Start = 0, Term = 5000, Rent = 100, Offer = 80, Asked = 1 };
            total++;
            comes += LeaseRules.Haggle(t, Seed, day) ? 1 : 0;
        }
        Check(comes > total * 25 / 100 && comes < total * 55 / 100, "lease haggle: a landlord comes down about " + LeaseRules.HaggleChance / 10 + "% of the time (" + comes + " of " + total + ")");
        LeaseFile flat = Make();
        flat.Offer = 0;
        Check(!LeaseRules.CanHaggle(flat, 450), "lease haggle: nothing to ask about without an offer");
    }

    // Eight years of one lease with the rules alone: an offer once a term, accepted, the rent never falling; and the same history from the same seed.
    private static void LeaseYears()
    {
        long[] history = new long[2];
        for (int run = 0; run < 2; run++)
        {
            LeaseFile l = LeaseRules.Begin(Seed, "Broadway~12", 300, 1, MonthDays);
            int offers = 0, renewals = 0, rent = LeaseRules.RentAt(l.Rent, l.Running);
            bool never = true;
            long paid = 0;
            for (int day = 1; day <= 8 * 360; day++)
            {
                paid += LeaseRules.TopUp(l);
                if (LeaseRules.Settle(l, day, Seed, MonthDays))
                {
                    renewals++;
                }
                else if (LeaseRules.OfferDue(l, day))
                {
                    l.Offer = LeaseRules.Offer(l, Seed, 40, 0);
                    l.Asked = day;
                    offers++;
                    if (day % 2 == 0)
                    {
                        LeaseRules.Respond(l, day, true);
                    }
                }
                int now = LeaseRules.RentAt(l.Rent, l.Rise);
                never &= now >= rent;
                rent = now;
            }
            Check(offers >= renewals && offers <= renewals + 1 && renewals >= 2, "lease years: an offer for each renewal, the last perhaps still open (" + offers + " offers, " + renewals + " renewals)");
            Check(never && l.Rise >= 0 && l.Rise <= 4 * LeaseRules.RiseMost, "lease years: the rent never falls and the rise stays bounded (" + l.Rise + ")");
            history[run] = paid * 1000 + l.Rise;
        }
        Check(history[0] == history[1], "lease years: the same seed gives the same eight years twice");
        // The cost of the day's lease work for the most the save keeps.
        var many = new List<LeaseFile>();
        for (int i = 0; i < LeaseStore.LeasesMost; i++)
        {
            many.Add(LeaseRules.Begin(Seed, "P" + i, 100 + i, 1, MonthDays));
        }
        var watch = Stopwatch.StartNew();
        long sum = 0;
        for (int day = 1; day <= 365; day++)
        {
            for (int i = 0; i < many.Count; i++)
            {
                sum += LeaseRules.TopUp(many[i]);
                if (LeaseRules.Settle(many[i], day, Seed, MonthDays))
                {
                    sum++;
                }
                else if (LeaseRules.OfferDue(many[i], day))
                {
                    many[i].Offer = LeaseRules.Offer(many[i], Seed, 30, 0);
                    many[i].Asked = day;
                }
            }
        }
        watch.Stop();
        CheckSay(sum >= 0 && watch.ElapsedMilliseconds < 250, "lease cost: a year of the day's work for " + LeaseStore.LeasesMost + " leases is " + watch.ElapsedMilliseconds + " ms");
    }

    // The city's own leases: the same rule, from a seed alone, so what a landlord does to the player they do to the city's businesses.
    private static void LeaseCity()
    {
        // The very same function: a city lease of a district and a player's lease with the same key and renewal get the same rise.
        string key = "Midtown#3";
        var theirs = new LeaseFile { Place = key, Start = 0, Term = 360, Rent = 100, Rise = 0 };
        Check(LeaseRules.Offer(theirs, Seed, 35, 12) == LeaseRules.Rise(Seed, key, 1, 35, 12), "city leases: a business of the city and the player's meet one rule (the same rise for the same key, renewal, trend and pressure)");
        int renewed = 0, sum = 0, days = 0;
        bool deterministic = true;
        for (int day = 1; day <= 1080; day++)
        {
            int n, m;
            int s = LeaseRules.CityDay(Seed, "Midtown", day, MonthDays, 30, 0, out n);
            deterministic &= s == LeaseRules.CityDay(Seed, "Midtown", day, MonthDays, 30, 0, out m) && n == m;
            renewed += n;
            sum += s;
            days++;
        }
        Check(deterministic, "city leases: a district's renewals on a day are the same twice, from the seed alone");
        Check(renewed >= 4 && renewed <= LeaseRules.CityLeases * 6 && sum >= 0, "city leases: " + LeaseRules.CityLeases + " leases of a district renew about once a year each (" + renewed + " in three years)");
        int other = 0;
        long mine = 17, theirsDays = 17;
        for (int day = 1; day <= 1080; day++)
        {
            int n, m;
            LeaseRules.CityDay(Seed, "Harlem", day, MonthDays, 30, 0, out n);
            LeaseRules.CityDay(Seed, "Midtown", day, MonthDays, 30, 0, out m);
            other += n;
            theirsDays = theirsDays * 31 + (n > 0 ? day : 0);
            mine = mine * 31 + (m > 0 ? day : 0);
        }
        Check(other >= 4 && theirsDays != mine, "city leases: another district has its own leases and its own days (" + other + " renewals in three years, against " + renewed + ")");
        // A rival buying up the district raises what the city's landlords ask too.
        int calm = 0, pressed = 0;
        for (int day = 1; day <= 1080; day++)
        {
            int n;
            calm += LeaseRules.CityDay(Seed, "Midtown", day, MonthDays, 30, 0, out n);
            pressed += LeaseRules.CityDay(Seed, "Midtown", day, MonthDays, 30, 100, out n);
        }
        Check(pressed >= calm, "city leases: a rival buying up the district raises the rises the city's landlords ask as it does the player's (" + calm + " against " + pressed + ")");
        // The ring: a mean a month, twelve kept, a month with no renewal keeps the last figure.
        var ring = new List<int>();
        Check(LeaseRules.TrendOf(ring) == 0 && LeaseRules.TrendOf(null) == 0, "city trend: nothing kept, no trend: the landlord's own draw alone");
        LeaseRules.KeepTrend(ring, 90, 3);
        Check(ring.Count == 1 && ring[0] == 30 && LeaseRules.TrendOf(ring) == 30, "city trend: a month's mean rise");
        LeaseRules.KeepTrend(ring, 0, 0);
        Check(ring.Count == 2 && ring[1] == 30, "city trend: a month with no renewal keeps the last figure");
        for (int i = 0; i < 40; i++)
        {
            LeaseRules.KeepTrend(ring, 10 * (i % 7), 1);
        }
        Check(ring.Count == LeaseRules.TrendKept, "city trend: the ring keeps " + LeaseRules.TrendKept + " months");
    }

    private static void LeaseSaves()
    {
        var s = new LeaseState { TrendDay = 300 };
        s.Trend.Add(20);
        s.Trend.Add(35);
        s.Leases.Add(new LeaseFile { Place = "Main~1", Start = 100, Term = 360, Rent = 200, Rise = 40, Offer = 90, Asked = 450, Renewals = 2, Hold = false, Last = 30, Answer = LeaseRules.Accepted, Haggled = true });
        s.Leases.Add(new LeaseFile { Place = "Harbour Road~4", Start = 10, Term = 30, Rent = 75, Rise = 0, Renewals = 0, Hold = true });
        string line = LeaseStore.Write(s);
        LeaseState back = LeaseStore.Read(line, out int version);
        bool same = back != null && version == LeaseStore.Version && back.TrendDay == 300 && back.Trend.Count == 2 && back.Trend[1] == 35 && back.Leases.Count == 2;
        LeaseFile a = same ? back.Leases[0] : null;
        same = same && a.Place == "Main~1" && a.Start == 100 && a.Term == 360 && a.Rent == 200 && a.Rise == 40 && a.Offer == 90 && a.Asked == 450 && a.Renewals == 2 && a.Last == 30
               && a.Answer == LeaseRules.Accepted && a.Haggled && back.Leases[1].Hold && back.Leases[1].Place == "Harbour Road~4";
        Check(same, "lease save: every lease, the ring and the day round trip");
        Check(LeaseStore.Write(back) == line, "lease save: writing what was read gives the same line");
        Check(LeaseStore.Read("v=1", out _) == null && LeaseStore.Read("not a line", out _) == null && LeaseStore.Read("", out _) == null, "lease save: a line that is not the leases' is refused");
        string older = line.Replace("Main~1|1|1;", "Main~1;").Replace("Harbour Road~4|0|0", "Harbour Road~4");   // the records without their answer and their flag
        LeaseState old = LeaseStore.Read(older, out version);
        Check(old != null && old.Leases.Count == 2 && old.Leases[0].Answer == 0 && !old.Leases[0].Haggled, "lease save: a record from before the answer and the flag loads with neither");
        string wild = line.Replace("|450|", "|-5|").Replace("|90|", "|99999|");
        LeaseState bound = LeaseStore.Read(wild, out version);
        Check(bound != null && bound.Leases[0].Asked == 0 && bound.Leases[0].Offer <= 4 * LeaseRules.RiseMost, "lease save: figures out of range are bounded on reading");
        var big = new LeaseState();
        for (int i = 0; i < 200; i++)
        {
            big.Leases.Add(new LeaseFile { Place = "P" + i, Term = 30, Start = 1 });
        }
        Check(LeaseStore.Read(LeaseStore.Write(big), out version).Leases.Count == LeaseStore.LeasesMost, "lease save: at most " + LeaseStore.LeasesMost + " leases are kept");
        Check(LeaseStore.Key == "reallife:co_leases", "lease save: the key the brief names");
    }

    // A manager's marketing: the best effect for the money inside what is left of the month's limit, and never a booking past it.
    private static void ManagerMarketing()
    {
        int[] cost = { 150, 1200, 0, 2500 }, effect = { 5, 8, 12, 3 };
        Check(ManagerRules.Campaign(cost, effect, 4, 5000, 3, false) == 0, "marketing: flyers are the most customers for the money, so the best is flyers (5% for 150)");
        Check(ManagerRules.Campaign(cost, effect, 4, 100, 3, false) == -1, "marketing: nothing fits $100 of limit left");
        Check(ManagerRules.Campaign(cost, effect, 4, 5000, 6, false) == 1, "marketing: a campaign must add enough customers, here at least 6% (radio)");
        Check(ManagerRules.Campaign(cost, effect, 4, 5000, 20, false) == -1, "marketing: none adds as many as the manager wants");
        Check(ManagerRules.Campaign(cost, effect, 4, 5000, 3, true) == 0, "marketing: a mistake books the cheapest that fits");
        int[] c2 = { 1200, 600 }, e2 = { 8, 4 };
        Check(ManagerRules.Campaign(c2, e2, 2, 5000, 3, false) == 1, "marketing: equal effect for the money is a tie, which goes to the cheaper");
        bool never = true;
        for (int left = 0; left <= 3000; left += 50)
        {
            foreach (bool mistake in new[] { false, true })
            {
                int pick = ManagerRules.Campaign(cost, effect, 4, left, 3, mistake);
                never &= pick < 0 || cost[pick] <= left;
            }
        }
        Check(never, "marketing: whatever is left of the limit, a campaign past it is never booked, mistake or not");
        // The month's limit spent campaign by campaign never goes over.
        int limit = 2000, spent = 0, booked = 0;
        for (int i = 0; i < 30; i++)
        {
            int pick = ManagerRules.Campaign(cost, effect, 4, ManagerRules.Left(limit, spent), 3, false);
            if (pick < 0)
            {
                break;
            }
            spent += cost[pick];
            booked++;
        }
        Check(spent <= limit && booked >= 1, "marketing: a $2,000 month books campaigns up to the limit and stops (" + booked + " booked, " + spent + " spent)");
        // The kinds of decision a place has: marketing is a shop's, a lease any rented place's; the hours.
        Check(ManagerRules.Has(ManagerRules.Kinds(ManagedKind.Shop), ManagerTask.Marketing) && !ManagerRules.Has(ManagerRules.Kinds(ManagedKind.Works), ManagerTask.Marketing)
              && !ManagerRules.Has(ManagerRules.Kinds(ManagedKind.Warehouse), ManagerTask.Marketing), "marketing: only a business with customers has it");
        Check(ManagerRules.Has(ManagerRules.Kinds(ManagedKind.Shop), ManagerTask.Leases) && ManagerRules.Has(ManagerRules.Kinds(ManagedKind.Works), ManagerTask.Leases)
              && ManagerRules.Has(ManagerRules.Kinds(ManagedKind.Warehouse), ManagerTask.Leases), "leases: every kind of place has a lease to answer");
        Check(ManagerRules.Due(10, ManagerCatalogue.MarketHour, ManagerTask.Marketing) && !ManagerRules.Due(10, ManagerCatalogue.StaffHour, ManagerTask.Marketing)
              && ManagerRules.Due(10, ManagerCatalogue.StaffHour, ManagerTask.Leases) && !ManagerRules.Due(10, ManagerCatalogue.HireHour, ManagerTask.Leases),
            "managers: marketing is looked at at ten and a lease answered at nine");
        Check(ManagerCatalogue.MarketingSteps[ManagerCatalogue.MarketingDefault] == 500 && ManagerCatalogue.LeaseRises[ManagerCatalogue.LeaseDefault] == 50,
            "managers: the marketing limit starts at $500 a month and a lease rise at 5%");
        // The bits of the two new kinds are kept in a manager's tasks, and an older line has them off.
        var st = new ManagerState();
        var m = new ManagerFile { Place = "shop~1", Id = "m1", Name = "Ray", Tasks = ManagerRules.Bit(ManagerTask.Marketing) | ManagerRules.Bit(ManagerTask.Leases) | ManagerRules.Bit(ManagerTask.Raises) };
        st.Managers.Add(m);
        ManagerState back = ManagerStore.Read(ManagerStore.Write(st), out int version);
        Check(back != null && back.Managers[0].Tasks == m.Tasks && ManagerRules.Has(back.Managers[0].Tasks, ManagerTask.Marketing) && ManagerRules.Has(back.Managers[0].Tasks, ManagerTask.Leases),
            "managers: the marketing and lease switches are kept in the save");
        Decision d = st.Add(10, 9, "shop~1", ManagerTask.Leases, string.Empty, "Ray");
        d.A = 1;
        d.B = 100;
        d.C = 108;
        Decision d2 = st.Add(10, 10, "shop~1", ManagerTask.Marketing, string.Empty, "Ray");
        d2.A = 0;
        d2.B = 150;
        ManagerState back2 = ManagerStore.Read(ManagerStore.Write(st), out version);
        Check(back2 != null && back2.Decisions.Count == 2 && back2.Decisions[0].Task == (int)ManagerTask.Leases && back2.Decisions[1].Task == (int)ManagerTask.Marketing && back2.Decisions[0].C == 108,
            "managers: a lease answer and a campaign they booked are kept in the log");
        Check(ManagerCatalogue.MarketingSteps.Length == 6 && ManagerCatalogue.LeaseRises.Length == 6 && (int)ManagerTask.Count == 10, "managers: ten kinds of decision, six steps of each new limit");
    }
}
