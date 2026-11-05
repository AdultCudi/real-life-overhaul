using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Ui;

// The Ventures app's samples: the facts each page reads, invented but shaped as the game's are (the investors' own names from the catalogue, a business with a long name, an
// empty file, a full one). Every word goes through the text source, so the checks measure the longest translation. Fixtures, never shipped.
internal static partial class UiSamples
{
    public static readonly string[] VenturesStates =
    {
        "investors", "offer", "offer_countered", "deal", "deal_demand", "deal_called", "pledge", "empty", "raise_business", "raise_type", "raise_who", "property", "property_empty", "off"
    };

    /// <summary>A recording stand-in for the director: each press says which action it was, so the checks can see a button reaches the one it was drawn for.</summary>
    public sealed class VenStub : IVenActions
    {
        public string Last;
        public int Count;

        private void Did(string what)
        {
            Last = what;
            Count++;
        }

        public void Accept(int offerId) => Did("accept");
        public void Refuse(int offerId) => Did("refuse");
        public void Counter(int offerId, bool moreMoney) => Did(moreMoney ? "counter_money" : "counter_share");
        public void Demand(int dealId, bool board) => Did(board ? "demand_seat" : "demand_costs");
        public void BuyOut(int dealId) => Did("buy_out");
        public void BuyBack(int dealId) => Did("buy_back");
        public void Report(int dealId) => Did("report");
        public void GiveUp(int pledgeId) => Did("give_up");
        public void Pitch(int investorId, string place, string type, int budgetStep) => Did("pitch");
        public void Locate(string place) => Did("locate");
        public void OpenBizMan() => Did("bizman");
    }

    public static VentWords VentWordsOf(UiWords t) => new VentWords(t, VentMoney).WithPrice(c => "$" + (c / 100m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));

    // The game's short money words: whole figures to a million, then millions to two places.
    private static string VentMoney(float v) => v >= 1000000f ? "$" + (v / 1000000f).ToString("0.##", CultureInfo.InvariantCulture) + "M" : "$" + ((long)v).ToString("N0", CultureInfo.InvariantCulture);

    private static VenInvestorFact VenWho(int id, string name, string kind, string temper, bool open = true, string openText = null, int portfolio = 14, params VenBackFact[] backs) =>
        new VenInvestorFact
        {
            Id = id, Name = name, Kind = kind, Temper = temper, Likes = "Food, Retail", Portfolio = portfolio, Backs = backs, Open = open, OpenText = openText ?? (open ? "Open now" : "From day 540"),
            Least = 40000, Most = 750000
        };

    /// <summary>The model of a state: the same page the game writes, from invented facts.</summary>
    public static VentModel Ventures(UiWords t, string state, VentView view = null, IVenActions act = null)
    {
        VentWords w = VentWordsOf(t);
        VenFacts f = VenturesFacts(state);
        view = view ?? new VentView();
        if (state.StartsWith("property", StringComparison.Ordinal))
        {
            view.Tab = 1;
        }
        else if (state == "raise_business")
        {
            view.Page = VenPages.PageBusiness;
        }
        else if (state == "raise_type")
        {
            view.Page = VenPages.PageType;
            view.S2 = "Coffee shop";
            view.A = 3;
        }
        else if (state == "raise_who")
        {
            view.Page = VenPages.PageWho;
            view.S1 = "10 Elm Street";
            view.B = 1;
        }
        else if (state == "deal" || state == "deal_demand" || state == "deal_called")
        {
            view.Pick = VenPages.DealKey + 7;
        }
        else if (state == "pledge")
        {
            view.Pick = VenPages.PledgeKey + 4;
        }
        else if (state == "offer_countered")
        {
            view.Pick = VenPages.OfferKey + 12;
        }
        var m = new VentModel();
        VenPages.Fill(m, view, f, act ?? new VenStub(), w);
        return m;
    }

    private static VenFacts VenturesFacts(string state)
    {
        var a = VenWho(1, "Ingrid Ashdown", "Tycoon", "Patient", true, null, 14, new VenBackFact { Name = "Saltmarsh Studios", Pct = 14 }, new VenBackFact { Name = "Tidewater Interactive", Pct = 9 },
            new VenBackFact { Name = "Blue Kestrel Games", Pct = 11 });
        var b = VenWho(2, "Marcus Fenwick", "Angel investor", "Even", true, null, 6, new VenBackFact { Name = "Larkfield Foods", Pct = 5 });
        var c = VenWho(3, "Harrow Vale Capital", "Fund", "Demanding", false, "From day 612", 31, new VenBackFact { Name = "Garrow and Sons Logistics", Pct = 18 });
        c.Never = false;
        var d = VenWho(4, "Selma Okafor", "Angel investor", "Patient", true, null, 4);
        var e = VenWho(5, "Northgate Ventures", "Fund", "Even", false, "Not again", 22);
        e.Never = true;
        var f = new VenFacts
        {
            Day = 520, Reputation = 74, Invested = 750000, PaidOutTotal = 128400, TargetsMet = 3, PromisesKept = 5, PromisesTotal = 6, Investors = new[] { a, b, c, d, e },
            MonthNames = new[] { "M10", "M11", "M12", "M13", "M14", "M15", "M16", "M17" },
            Mine = new long[] { 0, 0, 2200000, 2600000, 3100000, 3500000, 4300000, 4800000 },
            Rival = new long[] { 6800000, 6900000, 7400000, 7600000, 8600000, 9000000, 9100000, 9300000 },
            RivalName = "Guo Interactive",
            RivalLine = "Guo Interactive raised $4,200,000 from Northgate Ventures last month and is valued at $9.3M. Investors are comparing the two of you.",
            HowText = "Investors come to a business that keeps a profit on 10 of its last 14 days, at least $150 a day. Raise money to go to them yourself.",
            RaiseNow = true, Budgets = new long[] { 10000, 20000, 35000, 50000, 75000, 100000, 150000, 250000, 500000 }
        };
        VenPromiseFact[] pledged =
        {
            new VenPromiseFact { Text = "Run 5 businesses within 6 months", State = 0 }, new VenPromiseFact { Text = "Keep team morale at 60 or more for 4 months", State = 0 },
            new VenPromiseFact { Text = "Send a report every quarter", State = 0 }
        };
        var offerA = new VenOfferFact
        {
            Id = 12, Who = a, For = "Pioneer Game Studio", Amount = 750000, ShareBp = 1200, Implied = 6250000, TargetPct = 15, TargetDays = 24, DaysLeft = 4, CanMoreMoney = true,
            BuyBack = "You may buy back after 3 years at 1.5 times the money", SaleLine = "They are paid out first, from the price.", Promises = pledged,
            TargetLine = "Profit up 15% within 24 days"
        };
        var offerB = new VenOfferFact
        {
            Id = 13, Who = b, For = "Hudson Street Bakery and Coffee Roasters", Amount = 400000, ShareBp = 800, Implied = 5000000, TargetPct = 10, TargetDays = 30, DaysLeft = 7, CanMoreMoney = false,
            Countered = state == "offer_countered", BuyBack = null, SaleLine = "They are paid out first, from the price.", Promises = new VenPromiseFact[0], TargetLine = "Profit up 10% within 30 days"
        };
        var offerC = new VenOfferFact
        {
            Id = 14, Who = c, For = "A new Cinema", ForNew = true, Amount = 2000000, ShareBp = 3500, Implied = 5700000, TargetPct = 25, TargetDays = 18, DaysLeft = 1, CanMoreMoney = true,
            BuyBack = "They may call it after 3 years at 2 times the money", SaleLine = "They are paid out first, from the price.", Promises = pledged, TargetLine = "$300 a day within 18 days of opening"
        };
        var dealA = new VenDealFact
        {
            Id = 7, Who = d, Business = "Hudson Street Bakery", Amount = 500000, PaidOut = 84000, ShareBp = 1000, ShareNowBp = 1000, StartDay = 301, Mood = 70, Implied = 5000000, MoodText = "Content",
            StateText = "Content", Stage = 0, CanSeat = true, BuyOut = 612000, TargetLine = "$1,840 a day by day 548, now $1,610", CostLine = null, BuyBack = "You may buy back from day 1381 at 1.5 times the money",
            SaleLine = "They are bought out first, for $612,000.", Day = "Day 301", Kept = 2, Total = 3, ReportDue = true,
            Promises = new[]
            {
                new VenPromiseFact { Text = "Run 5 businesses by day 482", State = 1 }, new VenPromiseFact { Text = "Keep team morale at 60 or more until day 421", State = 1 },
                new VenPromiseFact { Text = "Send a report every quarter, the next by day 540", State = 0 }
            }
        };
        var dealB = new VenDealFact
        {
            Id = 8, Who = e, Business = "Pearl Lane Cafe", Amount = 250000, PaidOut = 31000, ShareBp = 600, ShareNowBp = 600, StartDay = 188, Mood = 35, Implied = 4166666, MoodText = "Uneasy",
            StateText = "Warned", Stage = 0, CanSeat = false, BuyOut = 301000, TargetLine = "$960 a day by day 530, now $720", SaleLine = "They are bought out first, for $301,000.", Day = "Day 188", Kept = 1, Total = 2,
            Promises = new[] { new VenPromiseFact { Text = "Run 4 businesses by day 400", State = 2 }, new VenPromiseFact { Text = "Keep team morale at 60 or more until day 480", State = 1 } }
        };
        var pledge = new VenPledgeFact { Id = 4, Who = b, Type = "Wine bar", Amount = 120000, ShareBp = 1500, OpenBy = 548, DaysLeft = 28 };
        if (state == "empty" || state == "off" || state == "property_empty")
        {
            f.Ready = state != "off";
            f.OffTitle = "Ventures is not running";
            f.OffBody = "Load a save with the economy switched on to see investors and deals here.";
            f.Reputation = 70;
            f.Invested = 0;
            f.PaidOutTotal = 0;
            f.PromisesTotal = 0;
            f.Mine = new long[0];
            f.Rival = new long[0];
            f.MonthNames = new string[0];
            f.RivalLine = null;
            if (state == "property_empty")
            {
                return f;
            }
            return f;
        }
        f.Offers = new[] { offerA, offerB, offerC };
        f.Deals = new[] { dealA, dealB };
        f.Pledges = new[] { pledge };
        if (state == "deal_demand")
        {
            dealA.Stage = 1;
            dealA.StateText = "Demand";
            dealA.DemandLine = "They want an answer by day 540: a board seat at 4% more of the profit for 90 days, or a promise to cut costs by 10% within 30 days.";
        }
        else if (state == "deal_called")
        {
            dealA.Stage = 3;
            dealA.StateText = "Called";
            dealA.ShareNowBp = 0;
            dealA.CalledLine = "They want their money back: $416,000 owed, $13,867 a day.";
            dealA.BuyOut = 416000;
        }
        else if (state == "offer" || state == "investors")
        {
            // as built
        }
        f.RaiseBusinesses = new[]
        {
            new VenRaiseFact { Key = "10 Elm Street", Name = "Hudson Street Bakery", Line = "420 days old", AvgProfit = 1610, AgeDays = 420, Promised = 1000 },
            new VenRaiseFact { Key = "88 Broadway", Name = "Pioneer Game Studio", Line = "190 days old", AvgProfit = 2480, AgeDays = 190 },
            new VenRaiseFact { Key = "301 Lexington Avenue", Name = "Pearl Lane Cafe", Line = "35 days old", AvgProfit = -120, AgeDays = 35, Promised = 600 }
        };
        f.RaiseTypes = new[]
        {
            new VenRaiseFact { Key = "Coffee shop", Name = "Coffee shop", Sector = "Food", Liked = 3, Running = 2 },
            new VenRaiseFact { Key = "Cinema", Name = "Cinema", Sector = "Leisure", Liked = 1, Running = 0 },
            new VenRaiseFact { Key = "Hardware store", Name = "Hardware store", Sector = "Retail", Liked = 2, Running = 0 }
        };
        f.Property = new[]
        {
            new VenPropertyFact { Key = "88 Broadway", Address = "88 Broadway", District = "Midtown", Let = 100, Paid = 1850000, Worth = 2310000, Week = 11300, Month = 47800, Yield = "3.2%", YieldGrade = 32, Bought = "Day 120 for $1,850,000" },
            new VenPropertyFact { Key = "12 W 38th Street", Address = "12 W 38th Street", District = "Garment District", Let = 60, Paid = 980000, Worth = 1020000, Week = 4200, Month = 17900, Yield = "2.4%", YieldGrade = 24, Mortgage = 410000, Bought = "Day 300 for $980,000" },
            new VenPropertyFact { Key = "540 9th Avenue", Address = "540 9th Avenue", District = "Hell's Kitchen", Let = 0, Paid = 640000, Worth = 0, Week = 0, Month = 0, Yield = null, YieldGrade = -1, Bought = "Day 455 for $640,000" }
        };
        f.PropWeek = 15500;
        f.PropMonth = 65700;
        f.PropPaid = 3470000;
        if (state != "property")
        {
            return f;
        }
        return f;
    }
}
