using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;

// A hundred days of the property business and of the car dealership as their directors run them, for what the interface conversion added. The
// directors call the game-free day entries (EstateDay: People, Services, Unit, Ring; DealerDay: Morning, with DealerRules and RivalRules for the
// hours), and so do these checks, over a seeded city with its other landlords, its other dealers and its people changing jobs: the state of each new
// feature changes as it should, the other side acts, a save written on day 30 reads back to the same next thirty days, and a save from before the
// features plays on.
internal static partial class Program
{
    private sealed class EstateRunState
    {
        public EstateState S;
        public TenantPoolStub Pool;
        public int Cash;
        public int Inspected, Passed, Asked, Accepted, Refused, Lapsed, Done, Skipped, Missed, Quit, Relet, CallsOfKind1, Named;
    }

    private static EstateRunState RunEstateNew(int seed, int units, EstateClimate c)
    {
        var r = new EstateRunState { Pool = TenantPoolStub.Of(600, 120), S = new EstateState { Seed = seed } };
        for (int b = 0; b < 2; b++)
        {
            EstateHolding h = Units(units - b, 1000000, 560);   // an older building, so something goes wrong in it now and then
            h.Place = "Run%20Street~" + b;
            h.Name = "Run Street " + b;
            r.S.Holdings.Add(h);
            int rent = Math.Max(40, EstateRules.UnitRent(h, c) * 13 / 10);   // a rent over the market's: the tenants are not easily content
            for (int u = 0; u < h.Units.Count; u++)
            {
                LetUnit(h, u, rent, 80, 1, 400);
            }
        }
        return r;
    }

    // One morning of the property business, as EstateDirector.HoldingDay runs it for a managed building (the game's money is the cash a run keeps).
    private static void RunEstateDay(EstateRunState r, int day, EstateClimate c, bool servicing, bool answer)
    {
        r.S.LastDay = day;
        for (int i = 0; i < r.S.Holdings.Count; i++)
        {
            EstateHolding h = r.S.Holdings[i];
            EstateRules.Lapse(h, day);
            EstateRules.Wear(h);
            h.Cond = Math.Max(h.Cond, day % 10 == 0 ? 560 : h.Cond);   // the landlord keeps the building up (works are checked with the estate's own)
            EstateDay.People(r.S, h, day, r.Pool);
            if (servicing && day >= 3 && h.Services.Count == 0 && !ServiceRules.Holds(h, 1, day + 14))
            {
                ServiceRules.Book(h, 1, day, ServiceRules.Earliest(day), "Run Trades|1", ServiceRules.Fee(h, 1, c.Index));
            }
            int done = EstateDay.Services(h, day, c.DaysPerYear, b => { r.Cash -= b.Fee; return true; }, (b, ok) => { r.Done += ok ? 1 : 0; });
            for (int u = 0; u < h.Units.Count; u++)
            {
                EstateUnit unit = h.Units[u];
                int callBefore = unit.Call, askedBefore = unit.Improve, refusedBefore = unit.Refused;
                bool wasLet = unit.Let;
                TenancyDay d = EstateDay.Unit(r.S, h, u, day, c, true);
                r.Missed += d.Missed ? 1 : 0;
                r.Quit += d.Quit ? 1 : 0;
                if (unit.Let)
                {
                    // The landlord's duties of the morning, as DutiesMorning runs them: a call raised, a request made, the tenant's mood, a complaint, quitting.
                    DutyDay duty = EstateRules.Duty(h, u, r.S.Seed, day, c);
                    r.CallsOfKind1 += callBefore == 0 && unit.Call == 1 ? 1 : 0;
                    r.Asked += askedBefore == 0 && unit.Improve > 0 ? 1 : 0;
                    r.Lapsed += duty.AskLapsed ? 1 : 0;
                    if (answer && unit.Call > 0 && unit.CallAnswer == (int)CallAnswer.Waiting && day - unit.CallDay >= 1 && EstateRules.SendContractor(unit, day))
                    {
                        r.Cash -= unit.CallCost;
                    }
                                    }
                if (d.Lump > 0)
                {
                    r.Cash += d.Lump;
                }
                if (unit.Let && unit.Rent > 0 && !d.Missed)
                {
                    r.Cash += unit.Rent;
                }
                // The player answers a request three days after it is made: the first kind yes, the second no, the rest left to lapse.
                if (answer && unit.Improve > 0 && day - unit.ImproveDay >= 3 && unit.Improve <= 2)
                {
                    if (unit.Improve == 1)
                    {
                        r.Cash -= unit.ImproveCost;
                        ImproveRules.Accept(h, unit);
                        r.Accepted++;
                    }
                    else
                    {
                        ImproveRules.Refuse(unit);
                        r.Refused++;
                    }
                }
                // The other side of a lease: a unit that stood empty for a week is let again to someone of the pool.
                if (!unit.Let && day % 7 == 0)
                {
                    LetUnit(h, u, 110, 70, day, 400);
                    EstateDay.Lease(r.S, h, u, day, r.Pool);
                    r.Relet++;
                }
            }
            Inspection insp = EstateDay.Inspect(r.S, h, day, c, false);
            r.Inspected += insp.Done ? 1 : 0;
            r.Passed += insp.Done && insp.Passed ? 1 : 0;
        }
    }

    // What the game gives a building again when a save is loaded (its value and the income its units' rent is judged against), as EstateDirector.Start reads it from the game.
    private static void RunGameRefresh(EstateState from, EstateState into)
    {
        for (int i = 0; i < into.Holdings.Count && i < from.Holdings.Count; i++)
        {
            into.Holdings[i].Income = from.Holdings[i].Income;
            into.Holdings[i].Base = from.Holdings[i].Base;
        }
    }

    private static string EstateDigest(EstateRunState r, bool strain = true)
    {
        var sb = new StringBuilder(2048);
        sb.Append(r.Cash).Append('|').Append(r.S.RentRingCount).Append('|').Append(r.S.RentRingMonth).Append('|');
        for (int i = 0; i < r.S.RentRingCount; i++)
        {
            sb.Append(r.S.RentRing[i]).Append(',');
        }
        for (int i = 0; i < r.S.Marks.Count; i++)
        {
            sb.Append(r.S.Marks[i].Id).Append(':').Append(r.S.Marks[i].Rent).Append('/').Append(r.S.Marks[i].Prev).Append(',');
        }
        for (int i = 0; i < r.S.Holdings.Count; i++)
        {
            EstateHolding h = r.S.Holdings[i];
            sb.Append('#').Append(h.Cond).Append('.').Append(h.CostYear).Append('.').Append(h.CallsYear).Append('.').Append(h.Services.Count).Append('.').Append(h.LastInspectDay);
            for (int k = 0; k < h.ServiceUntil.Length; k++)
            {
                sb.Append(h.ServiceUntil[k]).Append(',');
            }
            for (int u = 0; u < h.Units.Count; u++)
            {
                EstateUnit x = h.Units[u];
                sb.Append('/').Append(x.Let ? 1 : 0).Append('.').Append(x.Rent).Append('.').Append(x.Arrears).Append('.').Append(x.Happy).Append('.').Append(x.PersonId).Append('.').Append(strain ? x.Strain : 0)
                    .Append('.').Append(x.Call).Append('.').Append(x.Improve).Append('.').Append(x.ImproveDay).Append('.').Append(x.Refused).Append('.').Append(x.PaymentCount);
                for (int p = 0; p < x.PaymentCount; p++)
                {
                    sb.Append(':').Append(x.Payments[p].Due).Append('-').Append(x.Payments[p].Paid).Append('-').Append(x.Payments[p].State);
                }
            }
        }
        return sb.ToString();
    }

    // The city's rents as the day's read gives them: districts at a rent that climbs, and two other landlords whose rents climb at their own pace.
    private static RentMapData RunCity(EstateState s, int day, int monthDays)
    {
        var data = new RentMapData();
        data.Clear(day, monthDays);
        int month = day / monthDays;
        string[] districts = { "Midtown", "Uptown", "Hell's Kitchen" };
        for (int d = 0; d < districts.Length; d++)
        {
            data.Add(districts[d], (int)EstateUse.Homes, (90 + d * 12) * (1000 + month * 12) / 1000 * 40, 4000, 40, 2 + d);
            data.Add(districts[d], (int)EstateUse.Shops, 300 * (1000 + month * 8) / 1000 * 6, 900, 6, 1);
        }
        data.AddLandlord("rival:1", (int)EstateUse.Homes, 1200 * (1000 + month * 15) / 1000, 12);
        data.AddLandlord("rival:2", (int)EstateUse.Homes, 700 * (1000 - month * 5) / 1000, 8);
        return data;
    }

    private static void EstateRun()
    {
        EstateClimate c = Climate();
        c.DaysPerYear = 360;
        int monthDays = MacroCatalogue.MonthDays(c.DaysPerYear);

        // ---- one city, a hundred and twenty days, a save on day 30 and an old save at the same place
        EstateRunState run = RunEstateNew(11, 4, c);
        EstateRunState other = null;
        string saved = null, savedExtra = null;
        var extraData = new Dictionary<string, string>();
        var oldData = new Dictionary<string, string>();
        EstateRunState oldRun = null;
        bool namedAtOnce = false, namedDistinct = true, jobLoss = false;
        int lostAt = -1, ringDays = 0;
        var seenPeople = new HashSet<int>();
        for (int day = 2; day <= 121; day++)
        {
            if (day == 40)
            {
                // The people's side: the tenants of the first building lose their jobs in the pool.
                for (int u = 0; u < run.S.Holdings[0].Units.Count; u++)
                {
                    int id = run.S.Holdings[0].Units[u].PersonId;
                    for (int p = 0; p < run.Pool.People.Count; p++)
                    {
                        if (run.Pool.People[p].Id == id)
                        {
                            TenantFact f = run.Pool.People[p];
                            f.Working = false;
                            f.PayADay = 0;
                            run.Pool.People[p] = f;
                            jobLoss = true;
                        }
                    }
                }
                lostAt = day;
            }
            RunEstateDay(run, day, c, true, true);
            if (day == 2)
            {
                bool all = true;
                for (int h = 0; h < run.S.Holdings.Count; h++)
                {
                    for (int u = 0; u < run.S.Holdings[h].Units.Count; u++)
                    {
                        EstateUnit x = run.S.Holdings[h].Units[u];
                        all &= x.Let && x.PersonId > 0 && x.PersonSince == 2;
                        namedDistinct &= seenPeople.Add(x.PersonId);
                    }
                }
                namedAtOnce = all;
            }
            if (day % monthDays == 0)
            {
                EstateDay.Ring(run.S, RunCity(run.S, day, monthDays));
                ringDays++;
            }
            if (day == 30)
            {
                saved = EstateStore.Write(run.S);
                EstateExtraStore.Write(run.S, extraData);
                savedExtra = string.Join("~", new[] { extraData[EstateExtraStore.TenantsKey], extraData[EstateExtraStore.RentRingKey] });
                // Two readers of the same save: one with the new lines and one from before them.
                int version;
                other = new EstateRunState { S = EstateStore.Read(saved, out version), Pool = run.Pool, Cash = run.Cash };
                EstateExtraStore.Read(other.S, extraData);
                RunGameRefresh(run.S, other.S);
                string d0 = EstateDigest(run, false), d1 = EstateDigest(other, false);
                int k0 = 0;
                while (k0 < d0.Length && k0 < d1.Length && d0[k0] == d1[k0])
                {
                    k0++;
                }
                Check(d0 == d1, "a save written on day 30 reads back to the state it was written from" + (d0 == d1 ? string.Empty : " (they part at character " + k0 + ": " + d0.Substring(Math.Max(0, k0 - 40), Math.Min(100, d0.Length - Math.Max(0, k0 - 40))) + " / " + d1.Substring(Math.Max(0, k0 - 40), Math.Min(100, d1.Length - Math.Max(0, k0 - 40))) + ")"));
                oldData = new Dictionary<string, string>();
                oldRun = new EstateRunState { S = EstateStore.Read(saved, out version), Pool = run.Pool, Cash = run.Cash };
                EstateExtraStore.Read(oldRun.S, oldData);
                RunGameRefresh(run.S, oldRun.S);
            }
            else if (day > 30 && day <= 60 && other != null)
            {
                RunEstateDay(other, day, c, true, true);
                if (day % monthDays == 0)
                {
                    EstateDay.Ring(other.S, RunCity(other.S, day, monthDays));
                }
                string a = EstateDigest(run), b = EstateDigest(other);
                if (a != b)
                {
                    Check(false, "a save written on day 30 plays the same as the city it was written in: they part on day " + day + "\n  " + a.Substring(0, Math.Min(260, a.Length)) + "\n  " + b.Substring(0, Math.Min(260, b.Length)));
                    other = null;
                }
                else if (day == 60)
                {
                    Check(true, "a save written on day 30 plays the next thirty days exactly as the city it was written in (named tenants, records, requests, services, the ring and the marks; " + a.Length + " characters of state compared every day)");
                }
            }
            if (oldRun != null && day > 30 && day <= 40)
            {
                RunEstateDay(oldRun, day, c, true, true);
                if (day == 31)
                {
                    int unnamed = 0, named = 0;
                    for (int h = 0; h < oldRun.S.Holdings.Count; h++)
                    {
                        for (int u = 0; u < oldRun.S.Holdings[h].Units.Count; u++)
                        {
                            EstateUnit x = oldRun.S.Holdings[h].Units[u];
                            named += x.Let && x.PersonId > 0 ? 1 : 0;
                            unnamed += x.Let && x.PersonId <= 0 ? 1 : 0;
                        }
                    }
                    Check(oldRun.S.RentRingCount == 0 && unnamed == 0 && named > 0, "a save from before the features is loaded with no tenants named and no ring, and is named in its first morning (" + named + " tenants, none left unnamed)");
                }
            }
        }
        Console.WriteLine("  estate run: " + run.Asked + " asks, " + run.Accepted + " accepted, " + run.Refused + " refused, " + run.Lapsed + " lapsed, " + run.CallsOfKind1 + " calls of the first kind, " + run.Missed + " missed days, " + run.Relet + " relets, " + run.Quit + " quits, ring " + run.S.RentRingCount);
        Check(namedAtOnce && namedDistinct, "every let unit has its own person of the people pool from the first morning, with the day they came");
        Check(jobLoss && lostAt == 40, "the people's side acts: the tenants of the first building lose their jobs in the pool on day 40");
        int strained = 0, namedNow = 0;
        for (int u = 0; u < run.S.Holdings[0].Units.Count; u++)
        {
            EstateUnit x = run.S.Holdings[0].Units[u];
            strained += x.Let && x.Strain >= EstateCatalogue.TenantStrainJob ? 1 : 0;
            namedNow += x.Let && x.PersonId > 0 ? 1 : 0;
        }
        Check(strained > 0, "a tenant out of work is under the job's strain on the rent (" + strained + " of " + namedNow + " tenants of the first building)");
        Check(run.Done >= 4 && run.Cash != 0, "booked servicing is done and paid on its day: " + run.Done + " visits over a hundred and twenty days");
        Check(run.Inspected >= 2 && run.S.Holdings[0].LastInspectDay > 0, "the city inspects a let building and the day and result are kept: " + run.Inspected + " inspections, " + run.Passed + " passed, the last on day " + run.S.Holdings[0].LastInspectDay);
        Check(run.Asked >= 2, "a tenant who is not content asks for an improvement: " + run.Asked + " requests");
        Check(run.Accepted >= 1 && run.Refused >= 1, "a request is answered both ways (" + run.Accepted + " paid for, " + run.Refused + " refused)");
        Check(run.Lapsed >= 1, "a request nobody answers lapses (" + run.Lapsed + ")");
        Check(run.S.RentRingCount == ringDays && ringDays == 4 && run.S.Marks.Count == 2, "the city's rent ring has a month for each month of the run (" + run.S.RentRingCount + ") and the two other landlords are marked");
        int change = RentRingRules.Change(run.S), r1 = RentRingRules.LandlordChange(run.S, "rival:1"), r2 = RentRingRules.LandlordChange(run.S, "rival:2");
        Check(change > 0 && r1 > 0 && r2 < 0, "the rents the other landlords ask move with their own pace: the city " + change + " per thousand, the first landlord " + r1 + ", the second " + r2);
        bool recordsOk = true, filled = false, lateSeen = false;
        for (int h = 0; h < run.S.Holdings.Count; h++)
        {
            for (int u = 0; u < run.S.Holdings[h].Units.Count; u++)
            {
                EstateUnit x = run.S.Holdings[h].Units[u];
                recordsOk &= x.PaymentCount <= EstateCatalogue.PaymentsKept;
                for (int p = 1; p < x.PaymentCount; p++)
                {
                    recordsOk &= x.Payments[p].Due <= x.Payments[p - 1].Due;
                }
                filled |= x.PaymentCount >= 3;
                for (int p = 0; p < x.PaymentCount; p++)
                {
                    lateSeen |= x.Payments[p].State != (byte)PaymentState.OnTime;
                }
            }
        }
        Check(recordsOk && filled, "a unit's rent record fills month by month and keeps the last " + EstateCatalogue.PaymentsKept + ", the newest first");
        Check(lateSeen && run.Missed > 0, "...and shows the months paid late or not at all (" + run.Missed + " missed days in the run)");

        // ---- booked servicing keeps a kind of fault down: the same city, with and without
        int withCalls = 0, withoutCalls = 0;
        for (int seed = 1; seed <= 30; seed++)
        {
            EstateRunState a = RunEstateNew(seed, 6, c), b = RunEstateNew(seed, 6, c);
            for (int day = 2; day <= 121; day++)
            {
                RunEstateDay(a, day, c, true, true);
                RunEstateDay(b, day, c, false, true);
            }
            withCalls += a.CallsOfKind1;
            withoutCalls += b.CallsOfKind1;
        }
        Check(withoutCalls > 0 && withCalls < withoutCalls, "a building serviced for a kind of fault has fewer calls of it than one that is not (" + withCalls + " against " + withoutCalls + " over thirty cities)");

        // ---- the ring cap: a year and a half of months turns the ring over, never past twelve
        var s2 = new EstateState { Seed = 3 };
        for (int m = 1; m <= 40; m++)
        {
            EstateDay.Ring(s2, RunCity(s2, m * monthDays, monthDays));
        }
        Check(s2.RentRingCount == EstateCatalogue.RentRingKept && s2.RentRingMonth == 40 && !EstateDay.Ring(s2, RunCity(s2, 40 * monthDays, monthDays)), "the rent ring keeps twelve months and a month already written is not written twice");
    }
}
