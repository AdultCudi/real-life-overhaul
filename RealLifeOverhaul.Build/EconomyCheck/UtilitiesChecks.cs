using System;
using RealLifeOverhaul;
using RealLifeOverhaul.Utilities;

// The city's power and water (Scripts/Utilities): use, prices, bills, what follows an unpaid one, deposits, the
// supply of a bought place, and the save.
internal static partial class Program
{
    private static void Utilities()
    {
        // ---------------------------------------------------------------- a day's use
        long homePower = UtilitiesRules.Use(Service.Power, PlaceKind.Home, 60, 1, true, 1);
        long homeWater = UtilitiesRules.Use(Service.Water, PlaceKind.Home, 60, 1, true, 1);
        Check(homePower == 11200 && homeWater == 150, "utilities: a 60 m2 home of one in spring uses 11.2 kWh and 150 litres a day");
        Check(UtilitiesRules.Use(Service.Power, PlaceKind.Home, 60, 3, true, 1) == 16800 && UtilitiesRules.Use(Service.Water, PlaceKind.Home, 60, 3, true, 1) == 450,
            "utilities: each person past the first adds a quarter of the power and their own water");
        Check(UtilitiesRules.Use(Service.Power, PlaceKind.Home, 60, 1, true, 0) == 14000 && UtilitiesRules.Use(Service.Power, PlaceKind.Home, 60, 1, true, 2) == 12880
              && UtilitiesRules.Use(Service.Water, PlaceKind.Home, 60, 1, true, 2) == 172,
            "utilities: heating in winter, cooling and showers in summer");
        long cafe = UtilitiesRules.Use(Service.Power, PlaceKind.FoodAndDrink, 100, 0, true, 1);
        Check(cafe == 90000 && UtilitiesRules.Use(Service.Power, PlaceKind.FoodAndDrink, 100, 0, false, 1) == 22500
              && UtilitiesRules.Use(Service.Water, PlaceKind.Salon, 100, 0, true, 1) == 6000 && UtilitiesRules.Use(Service.Power, PlaceKind.Office, 100, 0, true, 1) < cafe,
            "utilities: a business by its floor and kind, a quarter of it on a day it does not open");
        Check(UtilitiesRules.Use(Service.Power, PlaceKind.Home, -5, 0, true, 9) == 4000 * 1050 / 1000, "utilities: a nonsense size, people or season is brought within bounds");

        // ---------------------------------------------------------------- what it costs
        int day = UtilitiesRules.DayCents(Service.Power, homePower, 1000, false);
        Check(day == 168 + 50 && UtilitiesRules.DayCents(Service.Power, homePower, 1000, true) == 327
              && UtilitiesRules.DayCents(Service.Water, homeWater, 1000, false) == 30 + 30,
            "utilities: units at the tariff and the standing charge; the temporary supply half as much again");
        Check(UtilitiesRules.DayCents(Service.Power, homePower, 1500, false) == 327 && UtilitiesRules.DayCents(Service.Power, homePower, 99999, false) == 218 * 4,
            "utilities: at today's prices, within the index's bounds");
        Check(UtilitiesRules.Month(day, 30) == 65 && UtilitiesRules.Month(day, 5) == 11 && UtilitiesRules.Money(149) == 1 && UtilitiesRules.Money(150) == 2,
            "utilities: a month of it in whole money");
        Check(UtilitiesRules.DueIn(5) == 2 && UtilitiesRules.DueIn(10) == 5 && UtilitiesRules.DueIn(30) == 14, "utilities: a bill is due in half a month, two days to two weeks");
        Check(UtilitiesRules.Deposit(0, 65) == 130 && UtilitiesRules.Deposit(2, 65) == 65 && UtilitiesRules.Deposit(3, 65) == 0 && UtilitiesRules.Deposit(9, 65) == 0,
            "utilities: a deposit of two months on a very poor or poor rating, one on fair, none on good; never refused");

        // ---------------------------------------------------------------- the month and an unpaid bill
        var a = UtilitiesRules.Rented("Main%20Street~12", Service.Power, PlaceKind.Home, 10);
        Check(a.State == SupplyState.Active && !a.Owned && a.Deposit == 0 && UtilitiesRules.Supplied(a, 10), "utilities: a rented place is supplied at once, and just pays");
        for (int d = 10; d < 15; d++)
        {
            UtilitiesRules.AddDay(a, homePower, 1000, d);
        }
        Check(a.Accrued == 5 * 218 && a.Units == 5 * 11200, "utilities: the month's use adds up day by day");
        int bill = UtilitiesRules.Bill(a, 15, 5);
        Check(bill == 11 && a.Owed == 11 && a.DueDay == 17 && a.Accrued == 0 && a.Units == 0 && a.LastBill == 11, "utilities: the month billed, due in two days");
        Check(UtilitiesRules.Due(a, 17) == DueStep.None && UtilitiesRules.Due(a, 18) == DueStep.ChargeLate, "utilities: late the day after its due day");
        a.LateCharged = true;
        Check(UtilitiesRules.Due(a, 19) == DueStep.None && UtilitiesRules.Due(a, 20) == DueStep.Cut, "utilities: the late fee once; cut three days after the due day");
        UtilitiesRules.Cut(a, 20);
        Check(a.State == SupplyState.Cut && !UtilitiesRules.Supplied(a, 21) && !UtilitiesRules.Apply(a, 21), "utilities: cut, nothing supplied, and no new account while a bill is owed");
        int before = a.Accrued;
        UtilitiesRules.AddDay(a, homePower, 1000, 21);
        Check(a.Accrued == before, "utilities: nothing is used while cut");
        UtilitiesRules.Paid(a, 21);
        Check(a.Owed == 0 && a.DueDay == 0 && a.OnTime == 0 && UtilitiesRules.Apply(a, 21) && a.State == SupplyState.Pending && a.ConnectDay == 22
              && !UtilitiesRules.Supplied(a, 21) && UtilitiesRules.Turn(a, 22) && a.State == SupplyState.Active,
            "utilities: paid late, the row starts again; reconnected the next morning");

        // ---------------------------------------------------------------- a deposit comes back
        var g = UtilitiesRules.Rented("Park~1", Service.Water, PlaceKind.Home, 1);
        g.Deposit = 40;
        int back = 0;
        for (int i = 0; i < UtilitiesCatalogue.DepositBackBills; i++)
        {
            g.Accrued = 1000;
            UtilitiesRules.Bill(g, 10 + 10 * i, 10);
            back += UtilitiesRules.Paid(g, 10 + 10 * i + 1);
        }
        Check(back == 40 && g.Deposit == 0 && g.OnTime == UtilitiesCatalogue.DepositBackBills, "utilities: a deposit comes back after six bills paid on time in a row");

        // ---------------------------------------------------------------- a bought place
        var t = UtilitiesRules.Temporary("Dock%20Road~3", Service.Power, PlaceKind.Retail, 30);
        Check(t.State == SupplyState.Temporary && t.Owned && t.TemporaryUntil == 33 && UtilitiesRules.Supplied(t, 32) && !UtilitiesRules.Supplied(t, 33),
            "utilities: a bought place is supplied three days before it needs an account");
        UtilitiesRules.AddDay(t, 10000, 1000, 30);
        Check(t.Accrued == UtilitiesRules.DayCents(Service.Power, 10000, 1000, true), "utilities: at the temporary surcharge");
        Check(!UtilitiesRules.Turn(t, 32) && UtilitiesRules.Turn(t, 33) && t.State == SupplyState.Cut && !UtilitiesRules.Supplied(t, 33),
            "utilities: cut when the three days are over without an account");
        var p = UtilitiesRules.Temporary("Dock%20Road~3", Service.Water, PlaceKind.Retail, 30);
        Check(UtilitiesRules.Apply(p, 31) && p.State == SupplyState.Pending && UtilitiesRules.Supplied(p, 31) && UtilitiesRules.Turn(p, 32) && p.State == SupplyState.Active,
            "utilities: applied for within the three days, the temporary supply runs on until the account connects");
        p.Accrued = 0;
        UtilitiesRules.AddDay(p, 10000, 1000, 33);
        Check(p.Accrued == UtilitiesRules.DayCents(Service.Water, 10000, 1000, false), "utilities: at the tariff once connected");

        // ---------------------------------------------------------------- the save
        var s = new UtilitiesState { LastDay = 41, MonthStart = 36 };
        s.Accounts.Add(a);
        s.Accounts.Add(t);
        s.Accounts.Add(new RealLifeOverhaul.Utilities.Account { Place = "Odd~Street|with;dividers%~7", Service = Service.Water, Kind = PlaceKind.Salon, State = SupplyState.Closed, Owed = 12, DueDay = 50, AutoDebit = true, Deposit = 33 });
        int version;
        UtilitiesState r = UtilitiesStore.Read(UtilitiesStore.Write(s), out version);
        Check(r != null && version == UtilitiesStore.Version && r.LastDay == 41 && r.MonthStart == 36 && r.Accounts.Count == 3
              && r.Find("Odd~Street|with;dividers%~7", Service.Water) != null && r.Find("Odd~Street|with;dividers%~7", Service.Water).Owed == 12
              && r.Find("Main%20Street~12", Service.Power).State == SupplyState.Active && r.Find("Dock%20Road~3", Service.Power).State == SupplyState.Cut
              && UtilitiesStore.Write(r) == UtilitiesStore.Write(s),
            "utilities: the save round-trips exactly, a place with every divider in it too");
        Check(UtilitiesStore.Read("nonsense", out version) == null && UtilitiesStore.Read("v=1~a:only|3", out version).Accounts.Count == 0
              && UtilitiesStore.Read("v=1~a:Place~1|7|99|99|1|1|-4|0|0|-9|0|0|0|0|0|0|0|0", out version).Accounts.Count == 0,
            "utilities: a line that cannot be read gives nothing; a short record, or one whose place was not escaped, is passed over");
        UtilitiesState odd = UtilitiesStore.Read("v=1~a:" + UtilitiesStore.Escape("A~1") + "|7|99|99|1|1|-4|0|0|-9|0|0|0|0|0|0|0|0", out version);
        Check(odd != null && odd.Accounts.Count == 1 && odd.Accounts[0].Service == Service.Internet && odd.Accounts[0].Kind == PlaceKind.Industry
              && odd.Accounts[0].State == SupplyState.Closed && odd.Accounts[0].OpenedDay == 0 && odd.Accounts[0].Deposit == 0,
            "utilities: figures out of range are brought within it");
    }
}
