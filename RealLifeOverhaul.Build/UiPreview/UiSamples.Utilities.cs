using System;
using System.Globalization;
using RealLifeOverhaul.Ui;

// The Utilities screen's samples: a save's facts for each state, written through the real build (UtilitiesBuild), so a shot shows
// what ships. Bills paid and due, plans with one chosen, the company in full, not founded, and digging its first district.
internal static partial class UiSamples
{
    public static readonly string[] UtilitiesStates =
    {
        "accounts", "accounts_paid", "accounts_phone", "accounts_new", "internet", "internet_choose", "internet_cut", "internet_new", "network", "network_new", "network_nohq",
        "network_dig", "network_young", "network_day5", "network_dig_list", "network_settings", "network_found", "network_extend", "network_sell", "network_sold", "network_long"
    };

    /// <summary>Money as the game writes it: whole dollars, or cents when there are some.</summary>
    public static string UtilitiesMoney(float money)
    {
        string sign = money < 0f ? "-" : string.Empty;
        money = Math.Abs(money);
        return sign + (money == (float)Math.Floor(money) ? "$" + ((long)money).ToString("N0", CultureInfo.InvariantCulture) : "$" + money.ToString("N2", CultureInfo.InvariantCulture));
    }

    public static UtilitiesWords UtilitiesWordsOf(UiWords t) => new UtilitiesWords(t, UtilitiesMoney, () => CultureInfo.InvariantCulture);

    public static UtilitiesFaceModel Utilities(UiWords t, string state)
    {
        UtilitiesFaceModel m = UtilitiesBuild.Make(UtilitiesFactsOf(state), UtilitiesViewOf(state), UtilitiesWordsOf(t));
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = W(t, "reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.Confirm, Label = W(t, "reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.Back, Label = W(t, "reallife_ui_prompt_back", "Back") }
        };
        return m;
    }

    public static UtilitiesView UtilitiesViewOf(string state)
    {
        var v = new UtilitiesView();
        if (state.StartsWith("internet", StringComparison.Ordinal))
        {
            v.Tab = 1;
            v.Option = state == "internet_choose" ? 3 : -1;
            v.Place = state == "internet_cut" ? 1 : 0;
        }
        else if (state.StartsWith("network", StringComparison.Ordinal))
        {
            v.Tab = 2;
            v.Sheet = state == "network_dig_list" ? UtilitiesAsk.DigList : state == "network_settings" ? UtilitiesAsk.Settings : state == "network_found" ? UtilitiesAsk.Found
                : state == "network_extend" ? UtilitiesAsk.Extend : state == "network_sell" ? UtilitiesAsk.Sell : UtilitiesAsk.None;
            v.District = state == "network_extend" ? 2 : 0;
        }
        else
        {
            v.Account = state == "accounts_paid" ? 3 : state == "accounts_phone" ? 8 : 0;
        }
        return v;
    }

    public static UtilitiesFacts UtilitiesFactsOf(string state)
    {
        var f = new UtilitiesFacts { Ready = true, Day = state == "network_day5" ? 5 : 122, Office = "the utilities office at 14 Mercer Street", InstallFee = 60, InstallDays = 2, ContractMonths = 12 };
        f.ByPhone = state == "accounts_phone";
        f.GraceDays = 7;
        Accounts(f, state);
        Places(f, state);
        Network(f.Network, state, f.Day);
        return f;
    }

    private static UtilityAccountFact Bill(string place, int service, bool home, string provider, UtilityBillState st, int sofar, int owed, int due, bool auto, string units)
    {
        string words = st == UtilityBillState.Paid || st == UtilityBillState.Due || st == UtilityBillState.Overdue ? "Supplied" : st == UtilityBillState.Cut ? "Cut off"
            : st == UtilityBillState.Temporary ? "Temporary supply until day 130" : "Connects on day 124";
        return new UtilityAccountFact
        {
            Key = place + "|" + service, Place = place, Service = service, Home = home, Provider = provider, State = st, StateText = words, SoFar = sofar, Owed = owed, DueDay = due,
            AutoDebit = auto, CanDebit = true, Units = units, LastBill = owed > 0 ? owed : sofar > 0 ? sofar + 12 : 0, LastBillDay = 118, Deposit = home ? 0 : 150, DepositLeft = 4
        };
    }

    private static void Accounts(UtilitiesFacts f, string state)
    {
        if (state == "accounts_new" || state.StartsWith("internet_new", StringComparison.Ordinal))
        {
            f.Accounts = new UtilityAccountFact[0];
            return;
        }
        const string power = "City Light and Power", water = "Riverside Water";
        UtilityAccountFact cut = Bill("Corner Garage", 0, false, power, UtilityBillState.Cut, 0, 0, 0, false, "0.0");
        cut.Offer = UtilityOffer.Reconnect;
        cut.ApplyCost = 95;
        cut.ApplyDeposit = 0;
        cut.ApplyFee = 95;
        UtilityAccountFact temporary = Bill("31 Hudson Street", 1, true, water, UtilityBillState.Temporary, 22, 0, 0, true, "4.10");
        temporary.Offer = UtilityOffer.Apply;
        temporary.ApplyCost = 140;
        temporary.ApplyDeposit = 140;
        f.Accounts = new[]
        {
            Bill("Bloom Cafe", 0, false, power, UtilityBillState.Overdue, 118, 312, 119, false, "412.5"),
            Bill("Bloom Cafe", 1, false, water, UtilityBillState.Due, 41, 84, 124, true, "18.40"),
            Bill("Pioneer HQ", 0, false, power, UtilityBillState.Due, 96, 146, 124, true, "207.0"),
            Bill("7 4th Avenue", 0, true, power, UtilityBillState.Paid, 38, 0, 0, true, "96.2"),
            Bill("7 4th Avenue", 1, true, water, UtilityBillState.Paid, 9, 0, 0, true, "3.80"),
            Bill("7 4th Avenue", 2, true, "Kestrel Cable", UtilityBillState.Paid, 14, 0, 0, true, null),
            Bill("Bloom Cafe", 2, false, "Kestrel Cable", UtilityBillState.Paid, 27, 0, 0, true, null),
            Bill("Pioneer HQ", 1, false, water, UtilityBillState.Paid, 12, 0, 0, true, "5.10"),
            cut,
            temporary
        };
        foreach (UtilityAccountFact a in f.Accounts)
        {
            f.Owed += a.Owed;
            f.Month += a.SoFar;
        }
    }

    // The providers that reach a place, each with its plans; the one the place has marked.
    private static UtilityOptionFact[] Options(bool business, int current)
    {
        string[] names = { "Kestrel Cable", "Brightvein Fibre", "Aerowave Wireless", "Copperhill DSL", "Your network" };
        string[] kinds = { "cable", "fibre", "fixed wireless", "copper line", "fibre" };
        int[][] speeds = { new[] { 100, 300, 1000 }, new[] { 300, 1000, 2000 }, new[] { 50, 150 }, new[] { 25, 75 }, new[] { 300, 1000, 2000 } };
        int[][] cents = { new[] { 4500, 6500, 8500 }, new[] { 6000, 8000, 11000 }, new[] { 3500, 5000 }, new[] { 2500, 3800 }, new[] { 0, 0, 0 } };
        var list = new System.Collections.Generic.List<UtilityOptionFact>(14);
        int at = 0;
        for (int p = 0; p < 5; p++)
        {
            for (int i = 0; i < speeds[p].Length; i++)
            {
                int price = cents[p][i] * (business ? 18 : 10) / 10;
                list.Add(new UtilityOptionFact
                {
                    Provider = p, Plan = i, ProviderName = names[p], Kind = kinds[p], Mbps = speeds[p][i], ContractCents = price, MonthlyCents = price * 12 / 10, Own = p == 4,
                    Current = at == current, Speed = business ? "At this speed its people would work at 96%. An online shop here could take 88% of the orders a day its site allows." : null
                });
                at++;
            }
        }
        return list.ToArray();
    }

    private static void Places(UtilitiesFacts f, string state)
    {
        if (state == "internet_new" || state == "accounts_new")
        {
            f.Places = new UtilityPlaceFact[0];
            return;
        }
        var home = new UtilityPlaceFact
        {
            Key = "7 4th Avenue", Name = "7 4th Avenue", Home = true, Connected = true, Provider = "Kestrel Cable", ProviderIndex = 0, Mbps = 300, PriceCents = 6500, StateText = "Supplied",
            ContractText = "until day 330; leaving now costs $208", OnContract = true, ExitFee = 208, District = "Midtown", Options = Options(false, 1)
        };
        var cafe = new UtilityPlaceFact
        {
            Key = "Bloom Cafe", Name = "Bloom Cafe", Connected = true, Provider = "Aerowave Wireless", ProviderIndex = 2, Mbps = 150, PriceCents = 9000, StateText = "Supplied",
            ContractText = "Month by month", District = "Garment District", Options = Options(true, 7)
        };
        var hq = new UtilityPlaceFact
        {
            Key = "Pioneer HQ", Name = "Pioneer HQ", Connected = true, Down = state != "internet_cut", Cut = state == "internet_cut", Needs = true, Provider = "Brightvein Fibre", ProviderIndex = 1,
            Mbps = 1000, PriceCents = 14400, StateText = "Supplied", ContractText = "Month by month", District = "Midtown", Options = Options(true, 4)
        };
        var empty = new UtilityPlaceFact { Key = "31 Hudson Street", Name = "31 Hudson Street", Home = true, District = "Murray Hill", Options = Options(false, -1) };
        f.Places = state == "internet_cut" ? new[] { home, hq, cafe, empty } : new[] { home, cafe, hq, empty };
        if (state == "internet_cut")
        {
            hq.Owes = true;
        }
    }

    private static readonly string[] SampleDistricts = { "Midtown", "Garment District", "Murray Hill", "Hell's Kitchen", "Lower Manhattan", "Industry City", "The Hamptons" };
    private static readonly int[] SampleHomes = { 6200, 4800, 3900, 5100, 2700, 4400, 2100 };

    private static void Network(UtilityNetworkFact n, string state, int day)
    {
        bool running = state == "network" || state == "network_dig" || state == "network_young" || state == "network_dig_list" || state == "network_settings" || state == "network_extend"
            || state == "network_sell" || state == "network_long";
        n.State = state == "network_sold" ? 2 : running ? 1 : 0;
        n.HasHeadquarters = state != "network_nohq";
        n.HeadquartersName = "Pioneer HQ";
        n.Licence = 1000000;
        n.DigLeast = 1000000;
        n.DigMost = 2400000;
        n.BuildMonths = 2;
        n.FoundedDay = 118;
        n.SoldDay = 121;
        n.SalePrice = 3480000;
        n.BuyerName = "Harbour Fibre";
        n.PriceShare = 1000;
        n.PriceLeast = 500;
        n.PriceMost = 2000;
        n.PriceStep = 50;
        n.Maintenance = 1000;
        n.MaintenanceLeast = 500;
        n.MaintenanceMost = 1500;
        n.MaintenanceStep = 100;
        n.Marketing = 2;
        n.OutagePerMille = 20;
        n.MarketingBudgets = new[] { 0, 10000, 25000, 50000, 100000, 250000, 500000 };
        n.SaleValue = 3480000;
        bool dig = state == "network_dig", young = state == "network_young";
        int[] customers = young ? new[] { 120, 0, 0, 0, 0, 0, 0 } : new[] { 3410, 2180, 1260, 1090, 480, 0, 0 };
        n.Districts = new UtilityDistrictFact[7];
        for (int i = 0; i < 7; i++)
        {
            int st = !running ? 0 : dig ? (i == 0 ? 1 : 0) : young ? (i == 0 ? 2 : 0) : i < 5 ? 2 : i == 5 ? 1 : 0;
            n.Districts[i] = new UtilityDistrictFact
            {
                Name = SampleDistricts[i], State = st, Homes = SampleHomes[i], Customers = st == 2 && !dig ? customers[i] : 0, ReadyDay = day + 3, DigCost = 1200000 + i * 120000, Upkeep = 9600 + i * 960,
                ExtendCost = state == "network_extend" && i == 2 ? 640000 : 0
            };
        }
        if (dig)
        {
            n.Districts[0].ReadyDay = day + 56;
        }
        int total = young ? 120 : running && !dig ? 8420 : 0;
        n.Customers = total;
        n.TakingsDay = young ? 202 : total > 0 ? 13754 : 0;
        n.UpkeepDay = young ? 310 : total > 0 ? 4310 : dig ? 120 : 0;
        n.Plans = new[]
        {
            new UtilityPlanFact { Mbps = 300, PriceCents = 5500, Customers = total * 500 / 1000 },
            new UtilityPlanFact { Mbps = 1000, PriceCents = 7500, Customers = total * 350 / 1000 },
            new UtilityPlanFact { Mbps = 2000, PriceCents = 10500, Customers = total * 150 / 1000 }
        };
        n.Rivals = new[]
        {
            new UtilityRivalFact { Name = "Kestrel Cable", Kind = "cable", Districts = 6, FromMbps = 100, ToMbps = 1000, FromCents = 4500 },
            new UtilityRivalFact { Name = "Brightvein Fibre", Kind = "fibre", Districts = 3, FromMbps = 300, ToMbps = 2000, FromCents = 6000 },
            new UtilityRivalFact { Name = "Aerowave Wireless", Kind = "fixed wireless", Districts = 7, FromMbps = 50, ToMbps = 150, FromCents = 3500 },
            new UtilityRivalFact { Name = "Copperhill DSL", Kind = "copper line", Districts = 7, FromMbps = 25, ToMbps = 75, FromCents = 2500 }
        };
        int days = young ? 3 : 60;
        n.Series = new int[days];
        for (int i = 0; i < days && total > 0; i++)
        {
            n.Series[i] = young ? 40 + i * 40 : 600 + (int)((total - 600) * Math.Pow(i / (double)(days - 1), 1.2));
        }
        n.SeriesFirstDay = day - days + 1;
        if (!running || dig)
        {
            n.Series = new int[0];
        }
    }
}
