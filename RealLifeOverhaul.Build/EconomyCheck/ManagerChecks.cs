using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

// Checks for the managers (Scripts/Economy/Company/ManagerRules, ManagerState, ManagerStore): which kinds each place has,
// the chance of a mistake and that the roll is the same for the same save, every kind of decision kept inside the player's
// limit with and without a mistake, a mistake's bound, the stock arithmetic over the game's weekly counts, who decides when a
// manager is off sick, the company asking the player again once the managers are off or the post is empty, a figure the
// player changed taken as theirs, the undo windows, the month's report, the save's line across a round trip and a damaged or
// later line, and the texts in every language with What's new.
internal static partial class Program
{
    private static void Managers()
    {
        ManagerKinds();
        ManagerMistakes();
        ManagerAnswers();
        ManagerStock();
        ManagerPrices();
        ManagerHiring();
        ManagerOffAndOverrides();
        ManagerTasks();   // the task switches and the weekly rota written only whole (ManagerTaskChecks.cs)
        ManagerDirectory();   // the Managers tab's directory and the bugs found behind it (ManagerDirectoryChecks.cs)
        ManagerSave();
        ManagerWords();
    }

    private static void ManagerKinds()
    {
        int all = ManagerRules.Kinds(ManagedKind.Shop);
        Check(all == (1 << (int)ManagerTask.Count) - 1, "managers: a business has every kind of decision");
        int works = ManagerRules.Kinds(ManagedKind.Works), store = ManagerRules.Kinds(ManagedKind.Warehouse);
        Check(ManagerRules.Has(works, ManagerTask.Stock) && ManagerRules.Has(works, ManagerTask.Rota) && ManagerRules.Has(works, ManagerTask.Hiring)
              && !ManagerRules.Has(works, ManagerTask.Raises) && !ManagerRules.Has(works, ManagerTask.Prices) && !ManagerRules.Has(works, ManagerTask.Claims),
            "managers: a factory or an office has stock, the rota and hiring, and nothing the company runs for businesses with customers");
        Check(ManagerRules.Has(store, ManagerTask.Stock) && ManagerRules.Has(store, ManagerTask.Hiring) && !ManagerRules.Has(store, ManagerTask.Rota)
              && !ManagerRules.Has(store, ManagerTask.Prices), "managers: a warehouse has stock and hiring, no rota (the game gives its drivers their vehicles)");
        Check(ManagerRules.Weekday(1) == 0 && ManagerRules.Weekday(7) == ManagerCatalogue.Sunday && ManagerRules.Weekday(8) == 0 && ManagerRules.Weekday(0) == 6,
            "managers: day 1 is a Monday and day 7 a Sunday, as the game's TimeHelper counts");
        foreach (int[] steps in new[] { ManagerCatalogue.WageBands, ManagerCatalogue.StockDays, ManagerCatalogue.PriceLows, ManagerCatalogue.PriceHighs, ManagerCatalogue.Budgets })
        {
            bool rising = true;
            for (int i = 1; i < steps.Length; i++)
            {
                rising &= steps[i] > steps[i - 1];
            }
            Check(rising, "managers: a limit's steps rise from one to the next (" + string.Join(", ", steps) + ")");
        }
        Check(ManagerCatalogue.Step(ManagerCatalogue.WageBands, -5) == ManagerCatalogue.WageBands[0] && ManagerCatalogue.Step(ManagerCatalogue.WageBands, 99) == ManagerCatalogue.WageBands[ManagerCatalogue.WageBands.Length - 1],
            "managers: a step out of its list is held to its ends");
        Check(ManagerCatalogue.PriceLows[ManagerCatalogue.PriceLows.Length - 1] < ManagerCatalogue.PriceHighs[0], "managers: the lowest price's every step is under the highest price's every step");
        Check(ManagerCatalogue.StockHour + 8 == 20 && ManagerCatalogue.RotaHour > ManagerCatalogue.StockHour,
            "managers: the week's orders are set eight hours before the game locks them (DeliveryHelper's Sunday at 20:00)");
        Check(ManagerRules.Allowance(0, 50, 1000) == 80 && ManagerRules.Allowance(1, 50, 1000) == 180 && ManagerRules.Allowance(0, 50, 2000) == 160
              && ManagerRules.Allowance(0, 0, 1000) < ManagerRules.Allowance(0, 100, 1000) && ManagerRules.Allowance(9, 500, 1000) == ManagerRules.Allowance(1, 100, 1000),
            "managers: the allowance by role and skill, at today's prices, its role and skill held to their bounds");
    }

    private static void ManagerMistakes()
    {
        Check(ManagerRules.MistakeChance(0, 0) == ManagerCatalogue.MistakeMost && ManagerRules.MistakeChance(100, 0) == 0 && ManagerRules.MistakeChance(-40, 0) == ManagerCatalogue.MistakeMost
              && ManagerRules.MistakeChance(250, 0) == 0, "managers: a mistake's chance is the most at a skill of nothing and none at 100, out of bounds held");
        bool falling = true, reviewed = true;
        for (int s = 1; s <= 100; s++)
        {
            falling &= ManagerRules.MistakeChance(s, 0) <= ManagerRules.MistakeChance(s - 1, 0);
            reviewed &= ManagerRules.MistakeChance(s, 100) <= ManagerRules.MistakeChance(s, 0) && ManagerRules.MistakeChance(s, 50) <= ManagerRules.MistakeChance(s, 0);
        }
        Check(falling && reviewed, "managers: more skill never brings more mistakes, and a regional manager's review never adds any");
        Check(ManagerRules.MistakeChance(0, 100) == ManagerCatalogue.MistakeMost * (1000 - ManagerCatalogue.ReviewCut) / 1000, "managers: a regional manager at 100 takes the review's share away");
        // The roll: the same for the same save, day, place, kind and subject; spread evenly.
        Check(ManagerRules.Roll(7, 30, "Main%20St~3", 0, "abc") == ManagerRules.Roll(7, 30, "Main%20St~3", 0, "abc"), "managers: the same roll for the same save, day, place, kind and subject");
        int under = 0, differ = 0, outside = 0, n = 20000;
        for (int i = 0; i < n; i++)
        {
            int r = ManagerRules.Roll(12345, 100 + i / 50, "Harbour~" + (i % 50), i % 8, "emp" + i);
            outside += r < 0 || r > 999 ? 1 : 0;
            under += r < 300 ? 1 : 0;
            differ += r != ManagerRules.Roll(54321, 100 + i / 50, "Harbour~" + (i % 50), i % 8, "emp" + i) ? 1 : 0;
        }
        Check(outside == 0, "managers: a roll is always 0 to 999");
        Check(under > n * 27 / 100 && under < n * 33 / 100, "managers: a 30% chance comes about 30% of the time (" + under + " of " + n + ")");
        Check(differ > n * 95 / 100, "managers: another save rolls otherwise (" + differ + " of " + n + " differ)");
        Check(ManagerRules.FromAfar(50) == 50 - ManagerCatalogue.RegionalFar && ManagerRules.FromAfar(5) == 0 && ManagerRules.FromAfar(500) == 100 - ManagerCatalogue.RegionalFar,
            "managers: a regional manager stands in a little less skilled, never under nothing");
    }

    private static void ManagerAnswers()
    {
        // Raises: a full one only inside the ceiling, half only where that fits, a mistake never more generous.
        int tried = 0, over = 0, kinder = 0;
        int[] pays = { 0, 800, 1000, 1200, 1500, 3000 }, asks = { 0, 900, 1100, 1300, 1600, 4000 }, ceilings = { 0, 950, 1050, 1250, 1500, 9999 };
        foreach (int paid in pays)
        {
            foreach (int ask in asks)
            {
                foreach (int ceiling in ceilings)
                {
                    RaiseAnswer fair = ManagerRules.RaiseAnswerFor(paid, ask, ceiling, false), slip = ManagerRules.RaiseAnswerFor(paid, ask, ceiling, true);
                    int asked = Math.Max(0, ask - paid);
                    int raise = fair == RaiseAnswer.Agree ? asked : fair == RaiseAnswer.Less ? asked * CompanyCatalogue.RaiseLessShare / 100 : 0;
                    int slipRaise = slip == RaiseAnswer.Agree ? asked : slip == RaiseAnswer.Less ? asked * CompanyCatalogue.RaiseLessShare / 100 : 0;
                    tried++;
                    over += raise > 0 && paid + raise > ceiling ? 1 : 0;
                    kinder += slipRaise > raise || (int)slip < (int)fair ? 1 : 0;
                }
            }
        }
        Check(over == 0, "managers: a raise agreed never takes the pay past the wage band (" + tried + " tried)");
        Check(kinder == 0, "managers: a mistake over a raise is never more generous than the right answer");
        Check(ManagerRules.RaiseAnswerFor(1000, 1100, 1100, false) == RaiseAnswer.Agree && ManagerRules.RaiseAnswerFor(1000, 1100, 1050, false) == RaiseAnswer.Less
              && ManagerRules.RaiseAnswerFor(1000, 1100, 1040, false) == RaiseAnswer.Refuse && ManagerRules.RaiseAnswerFor(1000, 1100, 1100, true) == RaiseAnswer.Less
              && ManagerRules.RaiseAnswerFor(1000, 1100, 1050, true) == RaiseAnswer.Refuse, "managers: a raise in full, by half, or none, and one answer meaner in a mistake");
        Check(ManagerRules.Ceiling(1000, ManagerCatalogue.WageBandDefault) == 1050 && ManagerRules.Ceiling(2000, 0) == 1800 && ManagerRules.Ceiling(-5, 3) == 0,
            "managers: the ceiling is the band's share of the going rate");
        // Offers, overtime, claims.
        Check(ManagerRules.Match(1050, 1050, false) && !ManagerRules.Match(1051, 1050, false) && !ManagerRules.Match(1000, 1050, true) && !ManagerRules.Match(0, 1050, false),
            "managers: an offer is matched only inside the ceiling, never in a mistake");
        Check(ManagerRules.Overtime(100, 3, 300, false) && !ManagerRules.Overtime(101, 3, 300, false) && !ManagerRules.Overtime(100, 3, 300, true) && !ManagerRules.Overtime(0, 3, 300, false),
            "managers: overtime is agreed only when all its days fit the budget left, never in a mistake");
        Check(ManagerRules.Left(1000, 400) == 600 && ManagerRules.Left(1000, 2000) == 0 && ManagerRules.Left(1000, -50) == 1000, "managers: the budget left is never under nothing");
        int adds = 10 * CompanyCatalogue.ClaimLoading * 40 / 1000;
        Check(ManagerRules.Claim(adds, 10, 40, false) && !ManagerRules.Claim(adds - 1, 10, 40, false) && !ManagerRules.Claim(adds, 10, 40, true) && ManagerRules.Claim(5, 0, 40, false),
            "managers: a claim is put in when it pays what it adds to the next term or more; a mistake leaves it for the player");
    }

    private static void ManagerStock()
    {
        // The game's weekly counts turn over at 8 on a Monday (day 8 is a Monday).
        Check(ManagerRules.Turns(7, 12, 14, 12) == 1 && ManagerRules.Turns(8, 7, 8, 9) == 1 && ManagerRules.Turns(8, 8, 8, 9) == 0 && ManagerRules.Turns(7, 12, 21, 12) == 2
              && ManagerRules.Turns(9, 12, 14, 12) == 0 && ManagerRules.Turns(14, 12, 7, 12) == 0, "managers: the weeks the game's delivery counts turned over between two looks");
        Check(ManagerRules.Delivered(0, 10, 99, 25) == 15 && ManagerRules.Delivered(1, 10, 60, 5) == 55 && ManagerRules.Delivered(2, 10, 60, 5) == -1 && ManagerRules.Delivered(0, 30, 0, 10) == 0,
            "managers: what the contracts brought since a look, from the game's counts; unknown over two weeks");
        Check(ManagerRules.Use(100, 70, 30, 168) == 20000 && ManagerRules.Use(100, 0, 100, 24) == 0 && ManagerRules.Use(100, 0, 90, 11) == -1 && ManagerRules.Use(50, -1, 10, 48) == -1
              && ManagerRules.Use(10, 0, 50, 24) == 0, "managers: the use a day since a look, in thousandths; none known over too few hours");
        Check(ManagerRules.HoursToDelivery(7, 12) == 20 && ManagerRules.HoursToDelivery(8, 7) == 1 && ManagerRules.HoursToDelivery(8, 8) == 168 && ManagerRules.HoursToDelivery(8, 9) == 167,
            "managers: the hours to the next Monday at 8");
        Check(ManagerRules.Order(10000, 0, 0, 2, 0, 0) == 90 && ManagerRules.Order(10000, 100, 0, 2, 0, 0) == 0 && ManagerRules.Order(10000, 20, 48, 0, 0, 0) == 70
              && ManagerRules.Order(10000, 0, 0, 2, 50, 0) == 50 && ManagerRules.Order(-1, 0, 0, 2, 0, 0) == -1, "managers: the order lasts the week and the floor, less what is left at the delivery, inside the product's limit");
        int tried = 0, outside = 0, off = 0;
        foreach (int use in new[] { 0, 1, 500, 3000, 12000, 250000 })
        {
            foreach (int stock in new[] { 0, 3, 40, 400 })
            {
                foreach (int floor in ManagerCatalogue.StockDays)
                {
                    foreach (int most in new[] { 0, 20, 500 })
                    {
                        for (int skill = 0; skill <= 100; skill += 25)
                        {
                            for (int roll = 0; roll < 2; roll++)
                            {
                                int fair = ManagerRules.Order(use, stock, 20, floor, most, 0);
                                int slip = ManagerRules.Order(use, stock, 20, floor, most, ManagerRules.Slip(skill, roll, true));
                                tried++;
                                int cap = most > 0 ? most : ManagerCatalogue.OrderMost;
                                outside += fair < 0 || fair > cap || slip < 0 || slip > cap ? 1 : 0;
                                off += Math.Abs(slip - fair) > (long)fair * ManagerCatalogue.StockSlipMost / 1000 + 1 ? 1 : 0;
                            }
                        }
                    }
                }
            }
        }
        Check(outside == 0, "managers: an order is never under nothing or over the product's limit, with or without a mistake (" + tried + " tried)");
        Check(off == 0, "managers: a mistake moves an order by at most its share");
        Check(ManagerRules.Slip(0, 2, true) == ManagerCatalogue.StockSlipMost && ManagerRules.Slip(0, 3, true) == -ManagerCatalogue.StockSlipMost && ManagerRules.Slip(100, 2, true) == 0
              && ManagerRules.Slip(0, 2, false) == 0 && ManagerRules.Slip(50, 4, true) == ManagerCatalogue.StockSlipMost / 2, "managers: a mistake's share of an order shrinks with skill, up or down by the roll");
        Check(!ManagerRules.Moves(100, 104, ManagerCatalogue.StockStep) && ManagerRules.Moves(100, 105, ManagerCatalogue.StockStep) && ManagerRules.Moves(0, 1, ManagerCatalogue.StockStep)
              && !ManagerRules.Moves(5, 5, ManagerCatalogue.StockStep), "managers: an order changes only by its step or more");
        Check(ManagerRules.Undelivered(15, 14, 23) && ManagerRules.Undelivered(15, 15, 7) && !ManagerRules.Undelivered(15, 15, 8) && !ManagerRules.Undelivered(15, 16, 0),
            "managers: an order can be put back until its delivery comes at 8");
    }

    private static void ManagerPrices()
    {
        int tried = 0, outside = 0;
        foreach (int market in new[] { 1, 7, 99, 250, 1999, 50000 })
        {
            foreach (int suggested in new[] { 1, 5, 100, 300, 2500, 99999 })
            {
                for (int low = 0; low < ManagerCatalogue.PriceLows.Length; low++)
                {
                    for (int high = 0; high < ManagerCatalogue.PriceHighs.Length; high++)
                    {
                        int price = ManagerRules.Price(suggested, market, low, high);
                        int least = Math.Max(1, CompanyCatalogue.Share(market, ManagerCatalogue.PriceLows[low])), most = Math.Max(1, CompanyCatalogue.Share(market, ManagerCatalogue.PriceHighs[high]));
                        tried++;
                        outside += price < least || price > most ? 1 : 0;
                    }
                }
            }
        }
        Check(outside == 0, "managers: a price is always inside the price band around the market price (" + tried + " tried)");
        Check(ManagerRules.Price(500, 0, 2, 2) == -1 && ManagerRules.Price(0, 500, 2, 2) == -1 && ManagerRules.Price(2000, 1000, 2, 2) == 1300 && ManagerRules.Price(100, 1000, 2, 2) == 900
              && ManagerRules.Price(1100, 1000, 2, 2) == 1100, "managers: no price without a market price; the game's suggestion kept inside the band");
        Check(!ManagerRules.Moves(1000, 1009, ManagerCatalogue.PriceStep) && ManagerRules.Moves(1000, 1010, ManagerCatalogue.PriceStep), "managers: a price changes only by its step or more");
    }

    private static void ManagerHiring()
    {
        int[] wages = { 1500, 1200, 2600, 1300, 900 }, goings = { 1500, 1500, 1500, 1300, 1000 }, skills = { 40, 40, 90, 60, 10 };
        int best = ManagerRules.Pick(wages, goings, skills, 5, ManagerCatalogue.WageBandDefault, 100000, false);
        int slip = ManagerRules.Pick(wages, goings, skills, 5, ManagerCatalogue.WageBandDefault, 100000, true);
        Check(best == 3 && slip == 1, "managers: the most skilled candidate inside the band is hired (the dearest is outside it), a mistake the next best");
        Check(ManagerRules.Pick(wages, goings, skills, 5, ManagerCatalogue.WageBandDefault, ManagerRules.HireCost(1200), false) == 1
              && ManagerRules.Pick(wages, goings, skills, 5, ManagerCatalogue.WageBandDefault, ManagerRules.HireCost(900) - 1, false) == -1
              && ManagerRules.Pick(wages, goings, skills, 0, ManagerCatalogue.WageBandDefault, 100000, true) == -1,
            "managers: only a candidate whose week fits the budget left; none at all, nobody hired");
        int tried = 0, wrong = 0;
        var rnd = new Random(42);
        for (int t = 0; t < 2000; t++)
        {
            int n = rnd.Next(0, 6), band = rnd.Next(0, ManagerCatalogue.WageBands.Length), left = rnd.Next(0, 3000);
            var w = new int[n];
            var g = new int[n];
            var s = new int[n];
            for (int i = 0; i < n; i++)
            {
                w[i] = rnd.Next(1, 4000);
                g[i] = rnd.Next(500, 3000);
                s[i] = rnd.Next(0, 101);
            }
            foreach (bool mistake in new[] { false, true })
            {
                int pick = ManagerRules.Pick(w, g, s, n, band, left, mistake);
                int fair = ManagerRules.Pick(w, g, s, n, band, left, false);
                tried++;
                wrong += pick >= 0 && (w[pick] > ManagerRules.Ceiling(g[pick], band) || ManagerRules.HireCost(w[pick]) > left) ? 1 : 0;
                wrong += pick >= 0 && fair >= 0 && s[pick] > s[fair] ? 1 : 0;
                wrong += (pick < 0) != (fair < 0) ? 1 : 0;
            }
        }
        Check(wrong == 0, "managers: a hire is always inside the wage band and the budget, and a mistake is never a better candidate (" + tried + " tried)");
        Check(ManagerRules.HireCost(1500) == 600 && ManagerRules.HireCost(-3) == 0, "managers: a hire takes a week of its wage from the budget");
        Check(ManagerRules.InTime(ManagerTask.Hiring, 10, 10 + ManagerCatalogue.HireUndoDays) && !ManagerRules.InTime(ManagerTask.Hiring, 10, 11 + ManagerCatalogue.HireUndoDays)
              && ManagerRules.InTime(ManagerTask.Raises, 10, 10 + ManagerCatalogue.UndoDays) && !ManagerRules.InTime(ManagerTask.Prices, 10, 11 + ManagerCatalogue.UndoDays),
            "managers: a hire can be undone for its few days, a raise or a price for a fortnight");
    }

    private static void ManagerOffAndOverrides()
    {
        var m = new ManagerFile { Place = "Main%20St~3", Id = "e1", Tasks = ManagerRules.Kinds(ManagedKind.Shop), Seen = true };
        Check(ManagerRules.Answers(true, m, ManagerTask.Raises, "e2"), "managers: a manager answers a request at their place, and nobody is asked");
        Check(!ManagerRules.Answers(false, m, ManagerTask.Raises, "e2"), "managers: switched off, the company asks the player again");
        Check(!ManagerRules.Answers(true, null, ManagerTask.Raises, "e2"), "managers: with the post empty (stood down), the company asks the player again");
        Check(!ManagerRules.Answers(true, m, ManagerTask.Raises, "e1"), "managers: a manager's own request stays the player's");
        m.Tasks &= ~ManagerRules.Bit(ManagerTask.Offers);
        Check(!ManagerRules.Answers(true, m, ManagerTask.Offers, "e2") && ManagerRules.Answers(true, m, ManagerTask.Overtime, null), "managers: a kind taken back is the player's again");
        m.Seen = false;
        Check(!ManagerRules.Answers(true, m, ManagerTask.Overtime, null), "managers: a manager not found at work answers nothing");

        int skill, review;
        Check(ManagerRules.Decider(true, false, 70, true, false, 80, out skill, out review) == 1 && skill == 70 && review == 80, "managers: the manager decides, with the regional manager's review");
        Check(ManagerRules.Decider(true, true, 70, true, false, 80, out skill, out review) == 2 && skill == 80 - ManagerCatalogue.RegionalFar && review == 0,
            "managers: a manager off sick, the regional manager stands in a little less skilled");
        Check(ManagerRules.Decider(true, true, 70, false, false, 0, out skill, out review) == 0 && ManagerRules.Decider(true, true, 70, true, true, 80, out skill, out review) == 0
              && ManagerRules.Decider(false, false, 0, true, false, 80, out skill, out review) == 0, "managers: nobody to decide, the decisions wait");

        // A figure the player changed afterwards is theirs; what an undone decision set is not counted.
        var s = new ManagerState();
        Decision a = s.Add(10, 12, "P~1", ManagerTask.Stock, "W~2", "Ann");
        a.Detail = ManagerStore.Items(new List<ItemChange> { new ItemChange { Item = "ba:item_cola", Before = 10, After = 40 }, new ItemChange { Item = "ba:item_chips", Before = 5, After = 9 } });
        Decision b = s.Add(17, 12, "P~1", ManagerTask.Stock, "W~2", "Ann");
        b.Detail = ManagerStore.Items(new List<ItemChange> { new ItemChange { Item = "ba:item_cola", Before = 40, After = 44 } });
        Decision c = s.Add(17, 12, "P~1", ManagerTask.Stock, "W~3", "Ann");
        c.Detail = ManagerStore.Items(new List<ItemChange> { new ItemChange { Item = "ba:item_cola", Before = 1, After = 2 } });
        var set = new Dictionary<string, int>(StringComparer.Ordinal);
        var scratch = new List<ItemChange>();
        Decision last = s.LastSet(ManagerTask.Stock, "P~1", "W~2", set, scratch);
        Check(last == b && set["ba:item_cola"] == 44 && set["ba:item_chips"] == 9 && set.Count == 2, "managers: what a contract's decisions last set of each product, the newest over the older");
        Check(ManagerRules.Changed(true, 44, 50) && !ManagerRules.Changed(true, 44, 44) && !ManagerRules.Changed(false, 0, 50), "managers: a figure changed since the manager set it is taken as the player's");
        b.State = DecisionState.Undone;
        last = s.LastSet(ManagerTask.Stock, "P~1", "W~2", set, scratch);
        Check(last == a && set["ba:item_cola"] == 40, "managers: an undone decision's figures are not the manager's any more");
        Check(s.LastOf(ManagerTask.Stock, "P~1", "W~3") == c && s.LastOf(ManagerTask.Claims, "P~1", "W~3") == null, "managers: the latest decision on a subject, of its kind only");
        m.Held.Clear();
        m.Held.Add("ba:item_cola");
        Check(m.Holds("ba:item_cola") && !m.Holds("ba:item_chips"), "managers: a product the player took back is left to them");

        var month = new ManagerFile { Decided = 9, Mistakes = 2, Undone = 1, Paid = 700, Spent = 300 };
        month.EndMonth();
        Check(month.LastDecided == 9 && month.LastMistakes == 2 && month.LastUndone == 1 && month.LastPaid == 700 && month.LastSpent == 300
              && month.Decided + month.Mistakes + month.Undone + month.Paid + month.Spent == 0, "managers: the month's report keeps its figures as last month's and the budget is whole again");
        Check(ManagerRules.MonthOver(10, 40, 30) && !ManagerRules.MonthOver(10, 39, 30) && ManagerRules.MonthOver(10, 11, 0), "managers: a month ends after its days");

        // The same save makes the same decisions: a scripted month of requests answered twice over.
        string first = ScriptedMonth(99), again = ScriptedMonth(99), other = ScriptedMonth(100);
        Check(first == again, "managers: the same save's seed gives the same decisions, mistakes and all");
        Check(first != other, "managers: another save's seed gives other mistakes");
    }

    // Forty employees' requests over thirty days at a manager of skill 40: every answer and mistake in one line.
    private static string ScriptedMonth(int seed)
    {
        var line = new System.Text.StringBuilder(1300);
        int mistakes = 0, n = 0;
        for (int day = 1; day <= 30; day++)
        {
            for (int e = 0; e < 40; e++)
            {
                string id = "emp" + e;
                bool slip = ManagerRules.Roll(seed, day, "Main%20St~3", (int)ManagerTask.Raises, id) < ManagerRules.MistakeChance(40, 0);
                RaiseAnswer answer = ManagerRules.RaiseAnswerFor(1000 + e * 10, 1100 + e * 12, 1180, slip);
                line.Append((int)answer);
                mistakes += slip ? 1 : 0;
                n++;
            }
        }
        Check(mistakes > n * ManagerRules.MistakeChance(40, 0) / 1000 * 8 / 10 && mistakes < n * ManagerRules.MistakeChance(40, 0) / 1000 * 12 / 10,
            "managers: a manager of skill 40 errs about as often as the chance says (" + mistakes + " of " + n + ")");
        return line.ToString();
    }

    private static void ManagerSave()
    {
        var s = new ManagerState { MonthFrom = 33, Today = 4 };
        var m = new ManagerFile
        {
            Place = "Main%20St~3", Id = "abc+/=", Role = ManagerCatalogue.RolePlace, From = 30, Tasks = 0x5b, WageBand = 5, StockDays = 4, PriceLow = 1, PriceHigh = 3, Budget = 6,
            Keep = 7, Spent = 1200, Decided = 11, Mistakes = 2, Undone = 1, Paid = 900, LastDecided = 20, LastMistakes = 4, LastUndone = 3, LastPaid = 1800, LastSpent = 2500,
            SnapDay = 40, SnapHour = 12, Name = "Ann|Lee"
        };
        m.Snaps.Add(new StockSnap { Item = "ba:item_cola", Stock = 55, Week = 120 });
        m.Snaps.Add(new StockSnap { Item = "ba:item_chips", Stock = 0, Week = 0 });
        m.Held.Add("ba:item_water");
        s.Managers.Add(m);
        s.Managers.Add(new ManagerFile { Place = "ba:neighborhood_midtown", Id = "hq1", Role = ManagerCatalogue.RoleRegion, Base = "Office%20Rd~9", From = 31 });
        for (int t = 0; t < (int)ManagerTask.Count; t++)
        {
            Decision d = s.Add(40, 9, "Main%20St~3", (ManagerTask)t, "sub|j;e^ct", "Ann | Bee");
            d.A = t;
            d.B = -t;
            d.C = 100 + t;
            d.D = 200;
            d.E = 300;
            d.F = 1;
            d.Mistake = t % 2 == 0;
            d.State = (DecisionState)(t % 4);
            d.Detail = t == (int)ManagerTask.Stock ? ManagerStore.Items(new List<ItemChange> { new ItemChange { Item = "ba:item_cola", Before = 10, After = 40 } }) : "Ann*Rival Ltd";
        }
        int version;
        string line = ManagerStore.Write(s);
        ManagerState r = ManagerStore.Read(line, out version);
        Check(r != null && version == ManagerStore.Version && ManagerStore.Write(r) == line, "managers: the save's line reads back to the same line");
        ManagerFile rm = r != null ? r.At("Main%20St~3") : null;
        Check(rm != null && rm.Id == "abc+/=" && rm.Tasks == 0x5b && rm.WageBand == 5 && rm.StockDays == 4 && rm.PriceLow == 1 && rm.PriceHigh == 3 && rm.Budget == 6 && rm.Keep == 7
              && rm.Spent == 1200 && rm.LastPaid == 1800 && rm.SnapDay == 40 && rm.SnapHour == 12 && rm.Snaps.Count == 2 && rm.Snap("ba:item_cola").Week == 120
              && rm.Holds("ba:item_water") && rm.Name == "Ann Lee", "managers: a manager's limits, month, stock looks, products left to the player and name come back");
        ManagerFile rr = r != null ? r.At("ba:neighborhood_midtown") : null;
        Check(rr != null && rr.Region && rr.Base == "Office%20Rd~9" && rr.Id == "hq1", "managers: a regional manager comes back with their headquarters");
        Decision rd = r != null ? r.Find(5) : null;
        Check(r != null && r.Decisions.Count == (int)ManagerTask.Count && r.NextId == 9 && r.MonthFrom == 33 && r.Today == s.Today && rd != null && rd.Task == 4 && rd.C == 104
              && rd.Subject == "sub j e ct" && rd.Who == "Ann   Bee", "managers: the decisions come back, the dividers in their words taken out");
        var items = new List<ItemChange>();
        ManagerStore.Items(rd != null ? rd.Detail : string.Empty, items);
        Check(items.Count == 1 && items[0].Item == "ba:item_cola" && items[0].Before == 10 && items[0].After == 40, "managers: a decision's products come back");
        ManagerStore.Items("ba:a*1*2/broken/ba:b*x*3/ba:c*4*5", items);
        Check(items.Count == 2 && items[1].Item == "ba:c", "managers: a product that cannot be read is left out");
        // Bounds, a later version, a damaged line.
        ManagerState later = ManagerStore.Read("v=3^h:5|7|0|99^m:" + "0|1|255|99|-4|9|9|99|500|0|0|0|0|0|0|0|0|0|0|0|30|0|0|0|P~1|e9|||" + "^z:what^d:1|2|3|99|0|0|0|0|0|0|0|0|P~1", out version);
        Check(later != null && version == 3 && later.Managers.Count == 1 && later.Managers[0].WageBand == ManagerCatalogue.WageBands.Length - 1 && later.Managers[0].StockDays == 0
              && later.Managers[0].Keep == ManagerCatalogue.KeepMost && later.Managers[0].Tasks == 0xff && later.Managers[0].SnapHour == 23 && later.Decisions.Count == 0,
            "managers: a later version's line is read as far as it can be, every figure held to its bounds and a decision of an unknown kind left out");
        Check(ManagerStore.Read("garbage", out version) == null && ManagerStore.Read("v=1^m:1|2", out version) == null && ManagerStore.Read(string.Empty, out version) == null,
            "managers: a line that is not theirs, or has no header, is not read (and is kept as it was by SaveLines)");
        var full = new ManagerState();
        for (int i = 0; i < ManagerCatalogue.DecisionsKept + 25; i++)
        {
            full.Add(i, 0, "P~1", ManagerTask.Prices, string.Empty, "x");
        }
        Check(full.Decisions.Count == ManagerCatalogue.DecisionsKept && full.Decisions[0].Id == 26 && ManagerStore.Read(ManagerStore.Write(full), out version).Decisions.Count == ManagerCatalogue.DecisionsKept,
            "managers: the log keeps its newest decisions only");
        Check(ManagerStore.Word("a/b*c|d") == "a b c d", "managers: a product's name cannot break a manager's lists");
    }

    private static readonly string[] ManagerKeys =
    {
        "reallife_mgr_tab", "reallife_mgr_enabled", "reallife_opt_tip_economy_managers", "reallife_meta_title_economy_managers", "reallife_guide_economy_managers_about",
        "reallife_guide_economy_managers", "reallife_mgr_test_appoint", "reallife_mgr_test_decide", "reallife_mgr_test_undo", "reallife_transaction_co_manager",
        "reallife_transaction_co_manager_label", "reallife_mgr_col_place", "reallife_mgr_col_manager", "reallife_mgr_col_month", "reallife_mgr_col_what", "reallife_mgr_col_detail",
        "reallife_mgr_col_day", "reallife_mgr_col_where", "reallife_mgr_none", "reallife_mgr_in_post", "reallife_mgr_empty_post", "reallife_mgr_off_sick",
        "reallife_mgr_state_done", "reallife_mgr_state_undone", "reallife_mgr_state_overridden", "reallife_mgr_state_closed", "reallife_mgr_row_mistake", "reallife_mgr_row_day",
        "reallife_mgr_row_region", "reallife_mgr_row_no_places", "reallife_mgr_row_no_decisions", "reallife_mgr_row_no_staff", "reallife_mgr_row_no_office", "reallife_mgr_off",
        "reallife_mgr_off_row", "reallife_mgr_chip_count", "reallife_mgr_list_what", "reallife_mgr_sec_month", "reallife_mgr_sec_last", "reallife_mgr_sec_manager",
        "reallife_mgr_decided", "reallife_mgr_mistakes", "reallife_mgr_undone", "reallife_mgr_allowances", "reallife_mgr_month_from", "reallife_mgr_name", "reallife_mgr_skill",
        "reallife_mgr_mistake_chance", "reallife_mgr_allowance", "reallife_mgr_spent", "reallife_mgr_spent_value", "reallife_mgr_kind_shop", "reallife_mgr_kind_works",
        "reallife_mgr_kind_warehouse", "reallife_mgr_place_none", "reallife_mgr_place_reviewed", "reallife_mgr_place_stands_in", "reallife_mgr_place_waits",
        "reallife_mgr_place_last", "reallife_mgr_place_held", "reallife_mgr_place_pick", "reallife_mgr_appoint", "reallife_mgr_replace", "reallife_mgr_stand_down",
        "reallife_mgr_hand_back", "reallife_mgr_task_raises", "reallife_mgr_task_offers", "reallife_mgr_task_overtime", "reallife_mgr_task_claims", "reallife_mgr_task_stock",
        "reallife_mgr_task_prices", "reallife_mgr_task_rota", "reallife_mgr_task_hiring", "reallife_mgr_task_raises_what", "reallife_mgr_task_offers_what",
        "reallife_mgr_task_overtime_what", "reallife_mgr_task_claims_what", "reallife_mgr_task_stock_what", "reallife_mgr_task_prices_what", "reallife_mgr_task_rota_what",
        "reallife_mgr_task_hiring_what", "reallife_mgr_task_staff_waits", "reallife_mgr_task_claims_waits", "reallife_mgr_task_prices_waits", "reallife_mgr_task_rota_waits",
        "reallife_mgr_task_on", "reallife_mgr_task_off", "reallife_mgr_task_waits", "reallife_mgr_task_given", "reallife_mgr_task_give", "reallife_mgr_task_take",
        "reallife_mgr_task_no_manager", "reallife_mgr_limit_wage", "reallife_mgr_limit_wage_value", "reallife_mgr_limit_budget", "reallife_mgr_limit_budget_value",
        "reallife_mgr_limit_keep", "reallife_mgr_limit_keep_value", "reallife_mgr_limit_stock", "reallife_mgr_limit_stock_value", "reallife_mgr_limit_price_low",
        "reallife_mgr_limit_price_high", "reallife_mgr_limit_price_value", "reallife_mgr_dec_when", "reallife_mgr_chip_mistake", "reallife_mgr_dec_from_to",
        "reallife_mgr_dec_mistake", "reallife_mgr_dec_undo_what", "reallife_mgr_undo", "reallife_mgr_did_raise_agree", "reallife_mgr_did_raise_less", "reallife_mgr_did_raise_refuse",
        "reallife_mgr_did_offer_match", "reallife_mgr_did_offer_go", "reallife_mgr_did_overtime_agree", "reallife_mgr_did_overtime_refuse", "reallife_mgr_did_claim_put",
        "reallife_mgr_did_claim_left", "reallife_mgr_did_stock", "reallife_mgr_did_prices", "reallife_mgr_did_rota", "reallife_mgr_did_rota_missed",
        "reallife_mgr_did_rota_unwritten", "reallife_mgr_did_hire", "reallife_mgr_undo_changed", "reallife_mgr_undo_gone", "reallife_mgr_undo_late", "reallife_mgr_undo_left",
        "reallife_mgr_undo_locked", "reallife_mgr_undo_delivered", "reallife_mgr_undo_nothing", "reallife_mgr_undo_rota_gone", "reallife_mgr_undo_rota_off",
        "reallife_mgr_note_today", "reallife_mgr_note_month", "reallife_mgr_note_gone", "reallife_mgr_note_appointed", "reallife_mgr_note_stood_down",
        "reallife_mgr_note_cannot", "reallife_mgr_note_most", "reallife_mgr_note_undone", "reallife_mgr_appoint_title", "reallife_mgr_appoint_what", "reallife_mgr_appoint_hire",
        "reallife_mgr_appoint_replaces", "reallife_mgr_region_what", "reallife_mgr_region_pick", "reallife_mgr_region_places", "reallife_opt_sec_managers",
        "reallife_mgr_sec_tasks", "reallife_mgr_tasks_what", "reallife_mgr_task_rota_when", "reallife_mgr_did_rota_kept_out", "reallife_mgr_did_rota_kept_less"
    };

    private static void ManagerWords()
    {
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("managers", langs, codes))
        {
            return;
        }
        Dictionary<string, string> en = langs[0];
        var missing = new List<string>();
        foreach (string key in ManagerKeys)
        {
            string lacking = LanguagesLacking(langs, codes, key);
            if (lacking.Length > 0)
            {
                missing.Add(key + " (" + lacking + ")");
            }
        }
        Check(missing.Count == 0, "managers: every text is in every language with the same {names} (" + ManagerKeys.Length + " keys" + (missing.Count > 0 ? "; " + string.Join(", ", missing) : string.Empty) + ")");
        int notices = 0, long240 = 0;
        foreach (string key in ManagerKeys)
        {
            if (key.StartsWith("reallife_mgr_note_", StringComparison.Ordinal) || key.StartsWith("reallife_mgr_undo_", StringComparison.Ordinal))
            {
                notices++;
                long240 += en.TryGetValue(key, out string text) && text.Length > 240 ? 1 : 0;
            }
        }
        Check(notices > 0 && long240 == 0, "managers: every notice is at most 240 characters in English (" + notices + ")");
        bool guide = Array.TrueForAll(langs, w => w.TryGetValue("reallife_guide_economy_managers", out string g) && w.TryGetValue("reallife_mgr_tab", out string tab)
                                                  && g.Contains("<b>" + tab + "</b>") && w.TryGetValue("reallife_mgr_undo", out string undo) && g.Contains("<b>" + undo + "</b>")
                                                  && w.TryGetValue("reallife_mgr_hand_back", out string back) && g.Contains("<b>" + back + "</b>"));
        Check(guide, "managers: the Guide page names the tab, Undo and Hand them back as the app does, in each language");
        bool switches = Array.TrueForAll(langs, w => w.TryGetValue("reallife_guide_economy_managers", out string g) && w.TryGetValue("reallife_mgr_task_rota", out string rota)
                                                     && g.Contains("<b>" + rota + "</b>") && g.Contains("{mgr_rota_hour}"));
        Check(switches, "manager switches: the Guide page names the weekly rota's switch as the panel does, with its hour, in each language");
        Check(AnyChangesRow(en, "a switch for every task they do") && AnyChangesRow(en, "Fixed: a manager no longer rewrites the rota out of nowhere"),
            "manager switches: What's new has the switches' line and the rota's fix (in the next row, or its version's row once it is made)");
        // In the next row until its version is made, and in that version's own row after (the release empties the next row).
        bool line = false;
        foreach (KeyValuePair<string, string> row in en)
        {
            if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.Contains("Managers for your empire"))
            {
                line = !row.Value.StartsWith("New: ", StringComparison.Ordinal);
                break;
            }
        }
        Check(line, "managers: What's new has the feature's line, with no prefix (in the next row, or its version's row once it is made)");
    }
}
