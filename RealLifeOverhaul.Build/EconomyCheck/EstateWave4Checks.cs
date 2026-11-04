using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ui;

// Wave 4 of the property business (Scripts/Economy/Estate/TenantRules.cs, ServiceRules.cs, ImproveRules.cs, EstateExtraStore.cs, RentMapData.cs): a unit let to a named person of the
// people pool, rent trouble following their job and wage, a unit's rent record of six months, booked servicing that holds a kind of fault down for a
// season, the improvements a tenant asks for and what refusing costs, the four terms behind a tenant's satisfaction, the city's rents by district and
// use with the other landlords and the monthly mean in a ring of twelve, and every one of them through the save and back, from a line that is not
// there, and in the time a day allows.
internal static partial class Program
{
    private sealed class TenantPoolStub : ITenantPool
    {
        public readonly List<TenantFact> People = new List<TenantFact>();

        public int Count => People.Count;

        public TenantFact At(int index) => People[index];

        public bool Find(int id, out TenantFact fact)
        {
            for (int i = 0; i < People.Count; i++)
            {
                if (People[i].Id == id)
                {
                    fact = People[i];
                    return true;
                }
            }
            fact = default(TenantFact);
            return false;
        }

        public static TenantPoolStub Of(int count, int pay = 90)
        {
            var pool = new TenantPoolStub();
            for (int i = 1; i <= count; i++)
            {
                pool.People.Add(new TenantFact { Id = i, Age = 18 + i % 50, Working = i % 5 != 0, PayADay = i % 5 != 0 ? pay + i % 40 : 0 });
            }
            return pool;
        }
    }

    private static void EstateWave4()
    {
        W4Tenants();
        W4RentRecord();
        W4Services();
        W4Improvements();
        W4Terms();
        W4RentMap();
        W4Store();
        W4Cost();
    }

    private static EstateHolding W4Let(int units = 2, int rent = 120)
    {
        EstateHolding h = Units(units, 1000000, 800);
        for (int u = 0; u < units; u++)
        {
            LetUnit(h, u, rent, 80, 1, 150);
        }
        return h;
    }

    // ------------------------------------------------------------------------------------------------- named tenants

    private static void W4Tenants()
    {
        TenantPoolStub pool = TenantPoolStub.Of(500);
        int a = TenantRules.Pick(pool, 7, 11, 40, 30, null), b = TenantRules.Pick(pool, 7, 11, 40, 30, null);
        Check(a > 0 && a == b, "a lease is let to the same person of the pool every time for the same save, unit and day (" + a + ")");
        bool anyDifferent = false;
        for (int day = 1; day < 60 && !anyDifferent; day++)
        {
            anyDifferent = TenantRules.Pick(pool, 7, 11, day, 30, null) != a;
        }
        Check(anyDifferent, "and other days and other units draw other people");
        TenantFact f;
        pool.Find(a, out f);
        Check(f.Age >= EstateCatalogue.AdultAge && f.Age <= EstateCatalogue.OldAge && f.Working && TenantRules.Affordable(30, f.PayADay),
            "the person is an adult in a job whose pay the rent is not too much of (age " + f.Age + ", pay " + f.PayADay + " a day)");
        var children = new TenantPoolStub();
        for (int i = 1; i <= 300; i++)
        {
            children.People.Add(new TenantFact { Id = i, Age = i % 2 == 0 ? 9 : 90, Working = true, PayADay = 500 });
        }
        Check(TenantRules.Pick(children, 1, 2, 3, 100, null) == 0, "a pool of children and the very old gives nobody: a lease is never let to them");
        Check(TenantRules.Pick(null, 1, 2, 3, 100, null) == 0 && TenantRules.Pick(new TenantPoolStub(), 1, 2, 3, 100, null) == 0, "no pool, or an empty one, names nobody and the unit keeps its description");
        bool distinct = true;
        var taken = new HashSet<int>();
        for (int unit = 0; unit < 12; unit++)
        {
            int id = TenantRules.Pick(pool, 9, 100 + unit, 5, 30, x => taken.Contains(x));
            distinct &= id > 0 && taken.Add(id);
        }
        Check(distinct, "twelve units are let to twelve different people");
        var poor = TenantPoolStub.Of(300, 20);   // pay 20 to 59 a day: a rent of 400 a day is too much of any of them
        int fallback = TenantRules.Pick(poor, 3, 4, 5, 400, null);
        Check(fallback > 0, "where nobody can afford the rent the unit is still let to an adult (the second pass)");

        // Strain: a lost job, a wage the rent is too much of, and nothing for one the pool no longer holds.
        var small = new TenantPoolStub();
        small.People.Add(new TenantFact { Id = 1, Age = 30, Working = true, PayADay = 400 });
        small.People.Add(new TenantFact { Id = 2, Age = 30, Working = false, PayADay = 0 });
        small.People.Add(new TenantFact { Id = 3, Age = 30, Working = true, PayADay = 150 });
        Check(TenantRules.Strain(small, 1, 100) == 0, "a tenant whose rent is a quarter of their pay is under no strain");
        Check(TenantRules.Strain(small, 2, 100) == EstateCatalogue.TenantStrainJob, "a tenant out of work strains the rent by the job's figure");
        int wage = TenantRules.Strain(small, 3, 100);
        Check(wage > 0 && wage <= EstateCatalogue.TenantStrainMost, "a rent two thirds of the pay strains it, within the most (" + wage + ")");
        Check(TenantRules.Strain(small, 99, 100) == 0 && TenantRules.Strain(null, 1, 100) == 0 && TenantRules.Strain(small, 0, 100) == 0, "a person the pool does not hold, or none, strains nothing");
        Check(TenantRules.Strained(100, 0) == 100 && TenantRules.Strained(100, 300) == 400 && TenantRules.Strained(900, 400) == 1000, "strain adds to a miss chance and never past certain");

        // Strain changes what happens: over a year a tenant out of work misses more rent days than one in work.
        EstateClimate c = Climate();
        int missedWell = 0, missedOut = 0;
        for (int run = 0; run < 40; run++)
        {
            EstateHolding well = Units(1, 1000000, 800), out_ = Units(1, 1000000, 800);
            LetUnit(well, 0, 100, 70, 1, 4000);
            LetUnit(out_, 0, 100, 70, 1, 4000);
            out_.Units[0].Strain = EstateCatalogue.TenantStrainJob;
            for (int day = 2; day < 120; day++)
            {
                TenancyDay w = EstateRules.Day(well, 0, run + 3, day, c);
                TenancyDay o = EstateRules.Day(out_, 0, run + 3, day, c);
                missedWell += w.Missed ? 1 : 0;
                missedOut += o.Missed ? 1 : 0;
            }
        }
        Check(missedOut > missedWell * 2, "a tenant out of work misses rent far more often than one in work (" + missedOut + " against " + missedWell + " missed days)");

        // Naming a lease writes the person, once; an empty unit is named for nobody; leaving clears it.
        EstateHolding h = W4Let(2);
        Check(TenantRules.Name(h, 0, pool, 5, 10, null) && h.Units[0].PersonId > 0 && h.Units[0].PersonSince == 10, "a let unit is given a person and the day they came");
        int first = h.Units[0].PersonId;
        Check(!TenantRules.Name(h, 0, pool, 5, 11, null) && h.Units[0].PersonId == first, "a named unit keeps its person");
        h.Units[1].Vacant();
        Check(!TenantRules.Name(h, 1, pool, 5, 10, null), "an empty unit is named for nobody");
        h.Units[0].Vacant();
        Check(h.Units[0].PersonId == 0 && h.Units[0].PaymentCount == 0 && h.Units[0].Improve == 0, "when the lease ends the person, the record and a request go with it");
        small.People.Add(new TenantFact { Id = 4, Age = 30, Working = true, PayADay = 90, Employee = true });
        Check(TenantRules.IsEmployee(small, 4) && !TenantRules.IsEmployee(small, 1), "a tenant may be one of the player's own staff, and it is told");
    }

    // ------------------------------------------------------------------------------------------------- the rent record

    private static void W4RentRecord()
    {
        EstateHolding h = W4Let(1, 100);
        EstateUnit u = h.Units[0];
        for (int day = 2; day <= 61; day++)
        {
            RentRecordRules.Day(u, false, 0, day, 30);
        }
        Check(u.PaymentCount == 2 && u.Payments[0].State == (byte)PaymentState.OnTime && u.Payments[0].Due == 61 && u.Payments[0].Paid == 61 && u.Payments[0].Amount == 3000,
            "a month paid on its days is one line of the record, on time, for the month's rent (" + u.PaymentCount + " lines)");
        for (int day = 62; day <= 91; day++)
        {
            RentRecordRules.Day(u, day == 70, 0, day, 30);
            if (day == 72)
            {
                u.Arrears = 0;
            }
            if (day == 72)
            {
                RentRecordRules.Day(u, false, 100, day, 30);
            }
        }
        Check(u.Payments[0].State == (byte)PaymentState.Late && u.Payments[0].Paid == 72 && u.Payments[1].State == (byte)PaymentState.OnTime,
            "a month with a day missed and made up is late, and says the day it was met (" + u.Payments[0].Paid + ")");
        u.Arrears = 100;
        for (int day = 92; day <= 121; day++)
        {
            RentRecordRules.Day(u, day == 100, 0, day, 30);
        }
        Check(u.Payments[0].State == (byte)PaymentState.Missed && u.Payments[0].Paid == 0, "a month ending with arrears still owed is missed, with no day it was met");
        for (int day = 122; day <= 122 + 30 * 8; day++)
        {
            RentRecordRules.Day(u, false, 0, day, 30);
        }
        Check(u.PaymentCount == EstateCatalogue.PaymentsKept && u.Payments[0].Due > u.Payments[1].Due && u.Payments[1].Due > u.Payments[2].Due, "the record keeps six months, newest first, and drops the oldest");
        EstateHolding empty = Units(1, 1000000, 800);
        RentRecordRules.Day(empty.Units[0], true, 0, 5, 30);
        Check(empty.Units[0].PaymentCount == 0 && empty.Units[0].PeriodFrom == 0, "an empty unit keeps no record");
    }

    // ------------------------------------------------------------------------------------------------- booked servicing

    private static void W4Services()
    {
        EstateHolding h = W4Let(2);
        EstateClimate c = Climate();
        int fee = ServiceRules.Fee(h, 1, c.Index);
        int call = EstateRules.CallCost(h, 1, c.Index);
        Check(fee > 0 && fee < call, "a booked visit costs less than a call out for the same fault (" + fee + " against " + call + ")");
        Check(!ServiceRules.Book(h, 1, 10, 12, "x", fee) && !ServiceRules.Book(h, 0, 10, 40, "x", fee) && !ServiceRules.Book(h, 9, 10, 40, "x", fee) && !ServiceRules.Book(h, 1, 10, 40, "x", 0),
            "a visit cannot be booked for a day that is too soon, for no kind, or for nothing");
        Check(ServiceRules.Book(h, 1, 10, 40, "Broadway|12", fee) && !ServiceRules.Book(h, 1, 10, 41, "x", fee), "a kind is booked once");
        ServiceRules.Book(h, 2, 10, 30, "a", fee);
        ServiceRules.Book(h, 3, 10, 35, "b", fee);
        ServiceRules.Book(h, 4, 10, 36, "c", fee);
        Check(h.Services.Count == EstateCatalogue.ServicesMost && !ServiceRules.Book(h, 5, 10, 50, "d", fee) && h.Services[0].Day <= h.Services[1].Day, "no more than the most are booked, in the order of their days");
        Check(ServiceRules.NextDue(h, 29) == null && ServiceRules.NextDue(h, 30) != null && h.Services.Count == EstateCatalogue.ServicesMost, "a visit is due on its day, and looking does not take it");
        ServiceBooking due = ServiceRules.NextDue(h, 31);
        Check(!ServiceRules.Holds(h, due.Kind, 31), "before the visit nothing is held down");
        ServiceRules.Complete(h, due, 31, 360);
        Check(ServiceRules.Holds(h, due.Kind, 31) && ServiceRules.Holds(h, due.Kind, 31 + ServiceRules.Season(360)) && !ServiceRules.Holds(h, due.Kind, 32 + ServiceRules.Season(360)),
            "after a visit the kind is held down for a season (" + ServiceRules.Season(360) + " days) and then not");
        Check(ServiceRules.Season(8) >= 14 && ServiceRules.Season(360) == 90, "a season is a quarter of the game's year, never under a fortnight");
        int free = 0;
        for (int day = 100; day < 4100; day++)
        {
            free += ServiceRules.Averts(h, due.Kind == 1 ? 6 : 1, 5, day % 60 + 40, day) ? 1 : 0;
        }
        Check(free == 0, "a kind that is not held down is never averted");
        int heldNow = 0;
        for (int key = 0; key < 4000; key++)
        {
            heldNow += ServiceRules.Averts(h, due.Kind, 5, 40, key) ? 1 : 0;
        }
        Check(heldNow > 2400 && heldNow < 3200, "while it holds, about " + EstateCatalogue.ServiceCut / 10 + " in a hundred of its calls are not made (" + heldNow + " of 4000)");
        // the whole rule: a unit with the heating service raises fewer heating calls over a year than the same unit without.
        int without = 0, with = 0;
        for (int run = 0; run < 30; run++)
        {
            EstateHolding a = Units(1, 1000000, 400), b = Units(1, 1000000, 400);
            LetUnit(a, 0, 100, 70, 1, 4000);
            LetUnit(b, 0, 100, 70, 1, 4000);
            b.ServiceUntil[1] = 10000;
            for (int day = 2; day < 150; day++)
            {
                if (a.Units[0].Call == 0 && EstateRules.RaiseCall(a, 0, run, day, c) && a.Units[0].Call == 1)
                {
                    without++;
                }
                a.Units[0].CallClosed();
                if (b.Units[0].Call == 0 && EstateRules.RaiseCall(b, 0, run, day, c) && b.Units[0].Call == 1)
                {
                    with++;
                }
                b.Units[0].CallClosed();
            }
        }
        Check(without > 0 && with < without * 6 / 10, "a booked boiler service means fewer heating calls (" + with + " with it against " + without + " without)");
        Check(ServiceRules.Cancel(h, 0) && h.Services.Count == EstateCatalogue.ServicesMost - 2 && !ServiceRules.Cancel(h, 9) && !ServiceRules.Cancel(null, 0), "a booking can be cancelled");
        string[] firms = { "Broadway|1", "Broadway|2", "Wall Street|9" };
        Check(ServiceRules.Contractor(firms, 3, 4, 5) == ServiceRules.Contractor(firms, 3, 4, 5) && Array.IndexOf(firms, ServiceRules.Contractor(firms, 3, 4, 5)) >= 0 && ServiceRules.Contractor(null, 3, 4, 5) == string.Empty
            && ServiceRules.Contractor(new string[0], 3, 4, 5) == string.Empty, "the contractor is one of the city's firms, the same one for the same booking, and none with no firms");
    }

    // ------------------------------------------------------------------------------------------------- improvements

    private static void W4Improvements()
    {
        EstateClimate c = Climate();
        EstateHolding h = W4Let(1);
        EstateUnit u = h.Units[0];
        u.Happy = 900;
        bool asked = false;
        for (int day = 1; day < 400; day++)
        {
            asked |= ImproveRules.Ask(h, 0, 3, day, c);
        }
        Check(!asked && u.Improve == 0, "a content tenant asks for nothing");
        u.Happy = 300;
        int day2 = 0;
        for (int day = 1; day < 400 && u.Improve == 0; day++)
        {
            if (ImproveRules.Ask(h, 0, 3, day, c))
            {
                day2 = day;
            }
        }
        Check(u.Improve >= 1 && u.Improve <= EstateCatalogue.ImproveKinds && u.ImproveCost > 0 && u.ImproveDay == day2, "a tenant under the line asks, for one of the kinds, with a quote (" + EstateText0(u.Improve) + ", $" + u.ImproveCost + ")");
        Check(!ImproveRules.Ask(h, 0, 3, day2 + 1, c), "and asks for one thing at a time");
        int kind = u.Improve, cost = u.ImproveCost, cond = h.Cond;
        int gained = ImproveRules.Accept(h, u);
        Check(gained == EstateCatalogue.ImproveGain && u.Happy == 300 + EstateCatalogue.ImproveGain && u.Improve == 0 && h.Cond > cond, "making it pleases the tenant and betters the building (satisfaction " + u.Happy + ")");
        u.Happy = 300;
        u.Improve = kind;
        u.ImproveDay = 5;
        u.ImproveCost = cost;
        ImproveRules.Refuse(u);
        Check(u.Improve == 0 && u.Refused == 1 && u.Happy == 300 - EstateCatalogue.ImproveRefusedLoss, "refusing costs satisfaction (" + u.Happy + ") and is remembered");
        Check(ImproveRules.QuitMore(u) == EstateCatalogue.ImproveRefusedQuit && ImproveRules.QuitMore(null) == 0, "each refusal makes them likelier to give notice");
        int quitFresh = 0, quitRefused = 0;
        for (int run = 0; run < 60; run++)
        {
            EstateHolding a = W4Let(1), b = W4Let(1);
            a.Units[0].Happy = b.Units[0].Happy = 100;
            b.Units[0].Refused = 6;
            for (int day = 2; day < 40; day++)
            {
                DutyDay x = EstateRules.Duty(a, 0, run, day, c), y = EstateRules.Duty(b, 0, run, day, c);
                quitFresh += x.Quits ? 1 : 0;
                quitRefused += y.Quits ? 1 : 0;
                a.Units[0].QuitDay = b.Units[0].QuitDay = 0;
                a.Units[0].Happy = b.Units[0].Happy = 100;
            }
        }
        Check(quitRefused > quitFresh, "a tenant who was refused often gives notice more often (" + quitRefused + " against " + quitFresh + ")");
        u.Improve = 2;
        u.ImproveDay = 10;
        u.Happy = 300;
        Check(!ImproveRules.Lapse(u, 10 + EstateCatalogue.ImproveDays - 1) && ImproveRules.Lapse(u, 10 + EstateCatalogue.ImproveDays) && u.Improve == 0 && u.Happy == 300 - EstateCatalogue.ImproveLapseLoss,
            "a request left " + EstateCatalogue.ImproveDays + " days lapses, with a small loss");
        Check(ImproveRules.Quote(h, 3, 1000) > ImproveRules.Quote(h, 4, 1000) && ImproveRules.Quote(h, 1, 2000) > ImproveRules.Quote(h, 1, 1000), "a kitchen costs more than redecorating, and prices lift a quote");
    }

    private static string EstateText0(int kind) => "kind " + kind;

    // ------------------------------------------------------------------------------------------------- the four terms

    private static void W4Terms()
    {
        EstateClimate c = Climate();
        EstateHolding h = W4Let(1);
        int repairs, value, condition;
        HappyTerms.Of(h, 0, 5, c, out repairs, out value, out condition);
        Check(repairs == 100 && value >= 0 && value <= 100 && condition > 50 && condition <= 100, "with nothing open the repair term is full and the others are in range (" + repairs + ", " + value + ", " + condition + ")");
        EstateUnit u = h.Units[0];
        u.Call = 1;
        u.CallDay = 1;
        HappyTerms.Of(h, 0, 20, c, out repairs, out value, out condition);
        Check(repairs < 100, "an open call lowers the repair term (" + repairs + ")");
        u.CallClosed();
        u.Rent = EstateRules.UnitRent(h, c) * 14 / 10;
        int dear;
        HappyTerms.Of(h, 0, 20, c, out repairs, out dear, out condition);
        u.Rent = EstateRules.UnitRent(h, c) * 7 / 10;
        int cheap;
        HappyTerms.Of(h, 0, 20, c, out repairs, out cheap, out condition);
        Check(cheap > dear, "a rent under the market's reads as better value than one over it (" + cheap + " against " + dear + ")");
        h.Cond = 200;
        int poorCondition;
        HappyTerms.Of(h, 0, 20, c, out repairs, out value, out poorCondition);
        h.Cond = 1000;
        int goodCondition;
        HappyTerms.Of(h, 0, 20, c, out repairs, out value, out goodCondition);
        Check(goodCondition > poorCondition && goodCondition <= 100 && poorCondition >= 0, "the condition term follows the building's");
        h.Units[0].Vacant();
        HappyTerms.Of(h, 0, 20, c, out repairs, out value, out condition);
        Check(repairs == 0 && value == 0 && condition == 0, "an empty unit has no terms");
    }

    // ------------------------------------------------------------------------------------------------- the city's rents

    private static void W4RentMap()
    {
        var m = new RentMapData();
        m.Clear(10, 30);
        m.Add("midtown", (int)EstateUse.Homes, 300, 1000, 10, 1);        // $10 a unit a day, $0.30 a m2 a day
        m.Add("midtown", (int)EstateUse.Shops, 500, 500, 2, 0);
        m.Add("uptown", (int)EstateUse.Homes, 100, 1000, 10, 3);
        m.Add("uptown", (int)EstateUse.Homes, 100, 1000, 10, 0);
        m.AddEmpty("uptown", (int)EstateUse.Homes, 4);
        Check(m.DistrictCount == 2 && m.PerSqm(0, (int)EstateUse.Homes) == 900 && m.PerSqm(1, (int)EstateUse.Homes) == 300, "rent a month for a square metre by district and use, in cents ("
            + m.PerSqm(0, 0) + ", " + m.PerSqm(1, 0) + ")");
        Check(m.PerSqm(0, (int)EstateUse.Offices) == -1 && m.PerSqm(7, 0) == -1 && m.PerSqm(0, 9) == -1, "a use or district with no building says so");
        Check(m.PerSqm((int)EstateUse.Homes) == 500, "rent a month for a square metre over the whole city for a use (" + m.PerSqm((int)EstateUse.Homes) + ")");
        Check(m.MeanUnit((int)EstateUse.Homes) == (300 + 100 + 100) * 30 / 30, "the mean rent of a unit a month is the rent over the units (" + m.MeanUnit((int)EstateUse.Homes) + ")");
        Check(m.EmptyShare() == 8 * 1000 / 32 && m.EmptyShare() > 0, "the share of units standing empty (" + m.EmptyShare() + " per thousand)");
        var order = new int[8];
        int n = m.Ranked((int)EstateUse.Homes, order);
        Check(n == 2 && order[0] == 0 && order[1] == 1, "the districts are ranked dearest a square metre first");
        m.AddEmpty("nowhere", 0, 3);
        m.Add("x", 9, 5, 5, 1, 0);
        m.Add("x", 0, 5, 0, 1, 0);
        m.Add("x", 0, -1, 5, 1, 0);
        Check(m.DistrictCount == 2, "a building with no floor, no units, no use or a rent under nothing is not counted, and no district is made for it");
        for (int i = 0; i < 30; i++)
        {
            m.AddLandlord("rival" + i, (int)EstateUse.Homes, 100, 2);
        }
        m.AddLandlord("rival0", (int)EstateUse.Homes, 300, 3);
        m.AddLandlord("rival0", (int)EstateUse.Shops, 900, 1);
        RentLandlord l = m.Landlord("rival0");
        Check(m.Landlords.Count == EstateCatalogue.LandlordsMost && l.Units == 6 && l.Flats == 5 && l.Mean(30) == 80 * 30,
            "the other landlords are counted from the buildings the game says rivals own, at most " + EstateCatalogue.LandlordsMost + " of them");
        m.ForSale[(int)EstateUse.Shops] = 4;
        m.ForSale[(int)EstateUse.Homes] = 6;
        Check(m.ForSaleAll() == 10, "the buildings for sale are counted by kind");

        // The ring: one value a month, twelve kept, the change on the month before.
        var s = new EstateState();
        Check(RentRingRules.Change(s) == int.MinValue, "with no month in the ring there is no change to show");
        Check(RentRingRules.Month(s, 5, 2800) && !RentRingRules.Month(s, 5, 2900) && !RentRingRules.Month(s, 4, 2900) && s.RentRingCount == 1 && s.RentRing[0] == 2800, "a month writes one value, once");
        Check(RentRingRules.Change(s) == int.MinValue, "one month has no change");
        RentRingRules.Month(s, 6, 2834);
        Check(RentRingRules.Change(s) == 12, "the change is the last month on the one before, in thousandths (+1.2%)");
        for (int month = 7; month < 30; month++)
        {
            RentRingRules.Month(s, month, 2800 + month);
        }
        Check(s.RentRingCount == EstateCatalogue.RentRingKept && s.RentRing[s.RentRingCount - 1] == 2829 && s.RentRing[0] == 2818, "twelve months are kept, the oldest dropped (" + s.RentRing[0] + " to " + s.RentRing[11] + ")");
        Check(!RentRingRules.Month(s, 40, 0) && !RentRingRules.Month(s, 40, -5), "a month with no figure is not written");
        RentRingRules.Mark(s, m);
        RentRingRules.Mark(s, m);
        Check(s.Marks.Count == EstateCatalogue.LandlordsMost && RentRingRules.LandlordChange(s, "rival0") == 0 && RentRingRules.LandlordChange(s, "nobody") == int.MinValue, "a landlord's change between two month turns is told once there are two");
        m.Landlord("rival0").FlatRent += 100;
        RentRingRules.Mark(s, m);
        Check(RentRingRules.LandlordChange(s, "rival0") > 0, "and moves with their rents");
        Check(m.Landlord("rival0").Mean(30) > 0, "(a rival's mean flat rent stands)");
    }

    // ------------------------------------------------------------------------------------------------- the save

    private static void W4Store()
    {
        EstateClimate c = Climate();
        var s = new EstateState { Seed = 5 };
        EstateHolding h = W4Let(3);
        h.Place = "St.%20Marks~12";
        s.Holdings.Add(h);
        for (int n = 0; n < 3; n++)
        {
            EstateUnit u = h.Units[n];
            u.PersonId = 100 + n;
            u.PersonSince = 20 + n;
            u.PeriodFrom = 30;
            u.PeriodLate = 2;
            u.PeriodPaid = 33;
            for (int p = 0; p < 4 + n; p++)
            {
                RentRecordRules.Push(u, new RentPayment { Due = 60 * p, Paid = 60 * p + 2, Amount = 3600, State = (byte)(p % 3) });
            }
        }
        h.Units[1].Improve = 3;
        h.Units[1].ImproveDay = 44;
        h.Units[1].ImproveCost = 2800;
        h.Units[1].Refused = 2;
        h.LastInspectDay = 90;
        h.LastInspectScore = 640;
        h.LastInspect = (int)InspectResult.Passed;
        ServiceRules.Book(h, 1, 10, 40, "Broad.way|12, x", 120);
        ServiceRules.Book(h, 5, 10, 33, "Wall Street|9", 90);
        h.ServiceUntil[2] = 150;
        RentRingRules.Month(s, 3, 2700);
        RentRingRules.Month(s, 4, 2760);
        s.Marks.Add(new LandlordMark { Id = "a.b|c;d", Rent = 2600, Prev = 2500 });
        var data = new Dictionary<string, string>();
        EstateExtraStore.Write(s, data);
        var back = new EstateState { Seed = 5 };
        EstateHolding hb = W4Let(3);
        hb.Place = h.Place;
        back.Holdings.Add(hb);
        EstateExtraStore.Read(back, data);
        bool same = true;
        for (int n = 0; n < 3; n++)
        {
            EstateUnit a = h.Units[n], b = hb.Units[n];
            same &= a.PersonId == b.PersonId && a.PersonSince == b.PersonSince && a.PaymentCount == b.PaymentCount && a.PeriodFrom == b.PeriodFrom && a.PeriodLate == b.PeriodLate
                && a.PeriodPaid == b.PeriodPaid && a.Improve == b.Improve && a.ImproveDay == b.ImproveDay && a.ImproveCost == b.ImproveCost && a.Refused == b.Refused;
            for (int p = 0; p < a.PaymentCount; p++)
            {
                same &= a.Payments[p].Due == b.Payments[p].Due && a.Payments[p].Paid == b.Payments[p].Paid && a.Payments[p].Amount == b.Payments[p].Amount && a.Payments[p].State == b.Payments[p].State;
            }
        }
        Check(same, "every unit's person, record and request are the same through the save and back");
        Check(hb.LastInspectDay == 90 && hb.LastInspectScore == 640 && hb.LastInspect == (int)InspectResult.Passed && hb.ServiceUntil[2] == 150 && hb.Services.Count == 2
            && hb.Services[0].Day == 33 && hb.Services[1].Contractor == "Broad.way|12, x" && hb.Services[1].Fee == 120, "the last inspection and the booked services are the same, a contractor's name with its punctuation too");
        Check(back.RentRingMonths[0] == 3 && back.RentRingMonths[1] == 4, "each entry of the ring keeps its own month through the save");
        var gap = new EstateState();
        RentRingRules.Month(gap, 3, 2700);
        RentRingRules.Month(gap, 4, 2760);
        RentRingRules.Month(gap, 9, 2800);   // months 5 to 8 were not read: the entry says month 9, and the one before it month 4
        Check(gap.RentRingMonths[2] == 9 && gap.RentRingMonths[1] == 4, "a month the read missed shows as itself in the ring, not as the next one");
        var older = new EstateState();
        EstateExtraStore.ReadRing(older, "v=1|8|3|2700,2760,2800|");
        Check(older.RentRingCount == 3 && older.RentRingMonths[0] == 6 && older.RentRingMonths[2] == 8, "a ring saved without months takes one for each month up to the last");
        var plural = new[] { UiPlural.Fix("1 day(s) left"), UiPlural.Fix("3 box(es), 2 warranty(ies)"), UiPlural.Fix("Day(s) and (s) and $5 more") };
        Check(plural[0] == "1 day left" && plural[1] == "3 boxes, 2 warranties" && plural[2] == "Day(s) and (s) and $5 more", "a counted noun takes its number's ending, a bracket with no count is left: " + string.Join(" / ", plural));
        Check(back.RentRingCount == 2 && back.RentRing[1] == 2760 && back.RentRingMonth == 4 && back.Marks.Count == 1 && back.Marks[0].Id == "a.b|c;d" && back.Marks[0].Prev == 2500, "the city's rent ring and the landlords' marks come back");

        // An old save: the two keys are not there.
        var old = new EstateState { Seed = 5 };
        EstateHolding ho = W4Let(2);
        old.Holdings.Add(ho);
        EstateExtraStore.Read(old, new Dictionary<string, string>());
        Check(ho.Units[0].PersonId == 0 && ho.Units[0].PaymentCount == 0 && old.RentRingCount == 0 && ho.Services.Count == 0, "a save from before has no named tenants, no record and no ring: it loads as it was");
        var junk = new Dictionary<string, string> { { EstateExtraStore.TenantsKey, "v=1^u:no|such|line|;;|||" }, { EstateExtraStore.RentRingKey, "v=1|x|y|z" } };
        EstateExtraStore.Read(old, junk);
        Check(old.RentRingCount == 0 && ho.Units[0].PersonId == 0, "a line that cannot be read changes nothing, and nothing is thrown");
        Check(!EstateExtraStore.ReadTenants(old, "rubbish") && !EstateExtraStore.ReadRing(old, string.Empty) && !EstateExtraStore.ReadTenants(old, null), "a line that is not the format is refused");
        var data2 = new Dictionary<string, string> { { EstateExtraStore.TenantsKey, "v=9^u:" + h.Place + "|0|7|8|9|1|1|99|99|99|99|999|1.2.3.4,5.6.7.8^b:" + h.Place + "|1|2|9|1,2|99.1.1.x" } };
        var capped = new EstateState();
        EstateHolding hc = W4Let(2);
        hc.Place = h.Place;
        capped.Holdings.Add(hc);
        EstateExtraStore.Read(capped, data2);
        Check(hc.Units[0].Improve == EstateCatalogue.ImproveKinds && hc.Units[0].PaymentCount == 2 && hc.LastInspect == 2 && hc.Services.Count == 0 && hc.Units[0].Payments[0].State <= 2,
            "a figure out of range is brought into range, a kind that does not exist is dropped and the payments kept are at most six");

        // Lines stay small: twenty buildings of twelve units.
        var big = new EstateState();
        for (int i = 0; i < 20; i++)
        {
            EstateHolding bh = W4Let(12);
            bh.Place = "Street" + i + "~" + i;
            for (int n = 0; n < 12; n++)
            {
                bh.Units[n].PersonId = 1000 + i * 12 + n;
                for (int p = 0; p < 6; p++)
                {
                    RentRecordRules.Push(bh.Units[n], new RentPayment { Due = p, Paid = p, Amount = 3000, State = 0 });
                }
            }
            big.Holdings.Add(bh);
        }
        string line = EstateExtraStore.WriteTenants(big);
        Check(line.Length < 60000, "the tenants' line for twenty buildings of twelve units is " + line.Length + " characters, under 60,000");
        Check(c.DaysPerYear > 0, "(the climate is as the checks make it)");
    }

    // ------------------------------------------------------------------------------------------------- cost

    private static void W4Cost()
    {
        TenantPoolStub pool = TenantPoolStub.Of(50000);
        EstateClimate c = Climate();
        var watch = Stopwatch.StartNew();
        int named = 0;
        for (int i = 0; i < 2000; i++)
        {
            named += TenantRules.Pick(pool, 3, i, i, 120, null) > 0 ? 1 : 0;
        }
        double pick = watch.Elapsed.TotalMilliseconds;
        EstateHolding h = W4Let(12);
        watch.Restart();
        for (int day = 2; day < 2 + 365; day++)
        {
            for (int u = 0; u < 12; u++)
            {
                RentRecordRules.Day(h.Units[u], day % 11 == 0, 0, day, 30);
                EstateRules.Duty(h, u, 1, day, c);
            }
        }
        double year = watch.Elapsed.TotalMilliseconds;
        Check(named == 2000 && pick < 400, "2,000 people are drawn from a pool of 50,000 in " + pick.ToString("0.0") + " ms (under 400)");
        Check(year < 400, "a year of a twelve unit building's record and duties is " + year.ToString("0.0") + " ms (under 400)");
        var m = new RentMapData();
        m.Clear(1, 30);
        watch.Restart();
        for (int i = 0; i < 20000; i++)
        {
            m.Add("d" + i % 12, i % 4, 100 + i % 50, 500, 3, i % 3 == 0 ? 1 : 0);
        }
        Check(watch.Elapsed.TotalMilliseconds < 300 && m.DistrictCount == 12, "20,000 buildings are added to the rent map in " + watch.Elapsed.TotalMilliseconds.ToString("0.0") + " ms");
    }
}
