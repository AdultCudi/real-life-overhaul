using System;
using System.Collections.Generic;
using RealLifeOverhaul;

// A player with more than 2.1 billion in cash was charged 2.1 billion twice as "Vehicle costs". The life events held a loss to
// the money there is with Mathf.FloorToInt(Mathf.Max(money, 0f)); a float past an int is int.MinValue in the game's Mono, so
// the loss became -(-2147483648). Every cash idiom of the mod was that cast. Now money goes int through WholeMoney, a life
// event's amount is bound by what the event is written with (LifeMoneyRules), a car's costs by what the car is worth
// (VehicleCostRules), and a save that already holds the wrong charge has it paid back once (LifeMoneyRepair).
//
// Each rule is held on its worst input (a huge distance, no time, NaN, many missed days, a save's wild numbers, a purse past an
// int), and each check is made to fail once: the old way of working the same figure, written out below as the game's Mono does
// the cast, must break the very bound the new way keeps.
internal static partial class Program
{
    // The game's Mono: a double past an int (or NaN) cast to int is int.MinValue; .NET 10, which runs this checker, saturates instead.
    private static int MonoInt(double d)
    {
        return double.IsNaN(d) || d >= 2147483648.0 || d <= -2147483649.0 ? int.MinValue : (int)d;
    }

    // Life/LifeEventRunner.RollMoney's last line as it was: the loss held to the cash by a cast, in unchecked ints.
    private static int OldLifeLoss(int amount, float money)
    {
        return amount < 0 ? unchecked(-Math.Min(-amount, MonoInt(Math.Floor(Math.Max((double)money, 0.0))))) : amount;
    }

    private static void VehicleCosts()
    {
        const float Rich = 5000000000f;   // the player's purse: 5 billion
        int[] levels = { 500, 1000, 1500, 4000 };

        // ---- the fault itself, and the fix
        Check(OldLifeLoss(-60, Rich) == int.MinValue, "vehicle costs: the old life event loss from a purse of 5 billion was -2,147,483,648 (the fault, as the game's Mono casts it)");
        Check(OldLifeLoss(-60, 25f) == -25, "vehicle costs: the old loss from a small purse was right (so only a purse past an int broke it)");
        bool kept = true;
        foreach (int level in levels)
        {
            int loss = LifeMoneyRules.Settle(-60, Rich, 120, level);
            kept &= loss <= 0 && -(long)loss <= LifeMoneyRules.Bound(120, level) && loss != int.MinValue;
            kept &= -(long)LifeMoneyRules.Settle(int.MinValue, Rich, 120, level) <= LifeMoneyRules.Bound(120, level);
        }
        Check(kept, "vehicle costs: a life event's loss from a purse past an int, or of any size, stays within what the event is written with at every price level");
        Check(LifeMoneyRules.Settle(-60, 25f, 120, 1000) == -25 && LifeMoneyRules.Settle(-60, 100f, 120, 1000) == -60 && LifeMoneyRules.Settle(-60, 0f, 120, 1000) == 0
              && LifeMoneyRules.Settle(-60, float.NaN, 120, 1000) == 0 && LifeMoneyRules.Settle(-60, -50f, 120, 1000) == 0,
            "vehicle costs: a loss is never past the purse (25 of 60, 60 of 100, none from nothing, from NaN or from debt)");
        Check(LifeMoneyRules.Settle(40, Rich, 120, 1000) == 40 && LifeMoneyRules.Settle(int.MaxValue, Rich, 120, 1000) == LifeMoneyRules.Bound(120, 1000),
            "vehicle costs: a gain is as rolled, and never past what the event is written with");
        Check(LifeMoneyRules.Bound(120, 1000) == 130 && LifeMoneyRules.Bound(-120, 2000) == 250 && LifeMoneyRules.Bound(0, 1000) == 10,
            "vehicle costs: the bound on one event is its worst figure at today's prices and ten for the rounding (130 at the start, 250 at double prices)");

        // ---- money into an int
        Check(WholeMoney.Floor(Rich) == int.MaxValue && WholeMoney.Floor(2147483647f) == int.MaxValue && WholeMoney.Floor(float.PositiveInfinity) == int.MaxValue
              && WholeMoney.Floor(float.NaN) == 0 && WholeMoney.Floor(-9f) == 0 && WholeMoney.Floor(0f) == 0 && WholeMoney.Floor(1234.99f) == 1234 && WholeMoney.Floor(1073741824f) == 1073741824,
            "vehicle costs: cash as whole money saturates at the largest int, is nothing for NaN or debt, and is the floor in between");
        Check(MonoInt(Rich) == int.MinValue && MonoInt(double.NaN) == int.MinValue && MonoInt(1234.99) == 1234,
            "vehicle costs: the check's own copy of the game's cast gives int.MinValue for a purse past an int and for NaN");
        Check(WholeMoney.Round(-1e30) == int.MinValue + 1 && WholeMoney.Round(1e30) == int.MaxValue && WholeMoney.Round(double.NaN) == 0 && WholeMoney.Round(2.5) == 3
              && WholeMoney.Round(-2.5) == -3 && WholeMoney.Round(double.NegativeInfinity) == int.MinValue + 1 && WholeMoney.Round(0f) == 0,
            "vehicle costs: money rounded to whole saturates at both ends, short of int.MinValue so that a negation still fits, and reads NaN as nothing");
        Check(WholeMoney.Add(int.MaxValue - 5, 100L) == int.MaxValue && WholeMoney.Add(10, -20L) == -10 && WholeMoney.Add(int.MinValue + 3, -100L) == int.MinValue + 1
              && WholeMoney.Add(100, 50L, 120) == 120 && WholeMoney.Add(100, -500L, 120) == 0 && WholeMoney.Add(0, long.MaxValue, 1000) == 1000,
            "vehicle costs: a sum of whole money stops at the ends of an int (or at the bound given) instead of wrapping round");
        Check(WholeMoney.Share(Rich, 0.5f) == int.MaxValue && WholeMoney.Share(1000f, 0.25f) == 250 && WholeMoney.Share(float.NaN, 0.5f) == 0 && WholeMoney.Share(-4f, 0.5f) == 0,
            "vehicle costs: a share of the purse saturates, and is nothing for NaN or debt");
        Check(WholeMoney.Take(500L, Rich) == 500 && WholeMoney.Take(500L, 120f) == 120 && WholeMoney.Take(-5L, 100f) == 0 && WholeMoney.Take(long.MaxValue, Rich) == int.MaxValue
              && WholeMoney.Take(500L, Rich, 130L) == 130 && WholeMoney.Take(2147483648L, Rich) == int.MaxValue,
            "vehicle costs: a loss taken from a purse is the loss, the purse or the bound, whichever is least, in long arithmetic");
        // The unguarded accumulation as it was: an int adding its interest every day wraps round to a credit.
        int wrapped = unchecked(int.MaxValue - 5 + 100);
        Check(wrapped < 0 && WholeMoney.Add(int.MaxValue - 5, 100L) > 0, "vehicle costs: what an unpaid car loan owes does not wrap round to a credit when its interest is added (the plain int sum does)");

        // ---- the car's costs, each on its worst input
        foreach (int level in levels)
        {
            Check(VehicleCostRules.Probe(level, out string first) == 0 && first.Length == 0, "vehicle costs: every guard holds on its worst input at price level " + level + " (" + first + ")");
        }
        float tick = VehicleCostRules.SpeedMost * VehicleCostRules.TickMost / 1000f;
        float naiveKm = 1e9f * 1e9f / 1000f;   // a speed from broken physics over a long hitch, unguarded
        Check(naiveKm > tick * 1000f && VehicleCostRules.Km(1e9f, 1e9f) <= tick + 1e-6f && VehicleCostRules.Km(float.PositiveInfinity, float.PositiveInfinity) <= tick + 1e-6f,
            "vehicle costs: a huge distance in one tick (the unguarded product is millions of kilometres) is held to what a car does in a tick");
        float naivePerSecond = 0f / 0f;   // a rate worked as distance over no time
        Check(float.IsNaN(naivePerSecond) && VehicleCostRules.Km(30f, 0f) == 0f && VehicleCostRules.Km(30f, -1f) == 0f && VehicleCostRules.Km(30f, float.NaN) == 0f,
            "vehicle costs: no elapsed time, a time that goes backwards or is no number adds no distance (a rate over no time is NaN)");
        float naiveClamp = Math.Max(40f, Math.Min(2500f, float.NaN));   // Math.Min(NaN, x) is NaN, so the clamp passes it on
        Check(float.IsNaN(naiveClamp) && VehicleCostRules.Bill(float.NaN, 40f, 2500f) == 40f && VehicleCostRules.Bill(-3f, 40f, 2500f) == 40f && VehicleCostRules.Bill(900f, 40f, 2500f) == 900f,
            "vehicle costs: a premium or service from a price that is NaN is the least, not NaN (the plain clamp passes NaN on)");
        float price = 20000f;
        Check(VehicleCostRules.Price(float.NaN) == 0f && VehicleCostRules.Price(-5f) == 0f && VehicleCostRules.Price(1e30f) == VehicleCostRules.PriceMost && VehicleCostRules.Price(price) == price,
            "vehicle costs: a list price that is NaN, negative or past any car's is read as nothing or the greatest");
        Check(VehicleCostRules.Repair(0.5f, price) == 5000f && VehicleCostRules.Repair(3f, price) == 10000f && VehicleCostRules.Repair(-1f, price) == 0f
              && VehicleCostRules.Repair(float.NaN, price) == 0f && VehicleCostRules.Repair(1f, float.PositiveInfinity) == VehicleCostRules.PriceMost * 0.5f,
            "vehicle costs: a repair is the damage (0 to 1) of half the price, so never more than half the car");
        // Damages for a crash: a speed that is NaN once rounded to int.MinValue and times ten to 0; an infinite one to the most.
        Check(VehicleCostRules.Liability(float.NaN, 2.5f, 150f, 3000f, 1000) == 150f && VehicleCostRules.Liability(float.PositiveInfinity, 2.5f, 150f, 3000f, 1000) == 3000f
              && VehicleCostRules.Liability(10f, 2.5f, 150f, 3000f, 1000) == 250f && VehicleCostRules.Liability(1e9f, 2.5f, 150f, 3000f, 2000) == 6000f,
            "vehicle costs: damages for a crash are between the least and the most at today's prices, and a speed that is no number is the least");
        Check(VehicleCostRules.OneCharge(5000f, 20000f, 1000) == 5000f && VehicleCostRules.OneCharge(1e9f, 20000f, 1000) == 20000f && VehicleCostRules.OneCharge(1e9f, 100f, 1000) == 400f
              && VehicleCostRules.OneCharge(1e9f, 20000f, 2000) == 40000f && VehicleCostRules.OneCharge(float.NaN, 20000f, 1000) == 0f && VehicleCostRules.OneCharge(float.PositiveInfinity, 100f, 1000) == 400f,
            "vehicle costs: one charge for a car is never more than the car is worth (or 400 for a cheap one), at today's prices");
        Check(VehicleCostRules.Within(100f, 20000f, 1000) && !VehicleCostRules.Within(float.NaN, 20000f, 1000) && !VehicleCostRules.Within(float.PositiveInfinity, 20000f, 1000)
              && !VehicleCostRules.Within(-1f, 20000f, 1000) && !VehicleCostRules.Within(20001f, 20000f, 1000) && VehicleCostRules.Within(400f, 100f, 1000) && !VehicleCostRules.Within(401f, 100f, 1000),
            "vehicle costs: the test on a player's car passes a real figure within the car's worth and fails NaN, infinity, a negative and a figure past the car");
        int naivePolicies = 100000 / 28;   // one policy for every four weeks missed
        Check(naivePolicies == 3571 && VehicleCostRules.PoliciesDue(100000) == 1 && VehicleCostRules.PoliciesDue(1) == 1 && VehicleCostRules.PoliciesDue(0) == 0 && VehicleCostRules.PoliciesDue(-4) == 0
              && VehicleCostRules.CatchUpDays(100000) == 60 && VehicleCostRules.CatchUpDays(7) == 7 && VehicleCostRules.CatchUpDays(-7) == 0,
            "vehicle costs: a car left unlooked at for 100,000 days renews one policy (not 3,571) and a catch-up counts at most 60 days");
        Check(VehicleCostRules.MendKm(float.NaN) == 0f && VehicleCostRules.MendKm(float.PositiveInfinity) == 0f && VehicleCostRules.MendKm(1e30f) == VehicleCostRules.KmMost
              && VehicleCostRules.MendKm(-9f) == 0f && VehicleCostRules.MendKm(321.5f) == 321.5f && VehicleCostRules.MendFraction(float.NaN) == 1f
              && VehicleCostRules.MendFraction(2f) == 1f && VehicleCostRules.MendFraction(-2f) == 0f && VehicleCostRules.MendFraction(0.4f) == 0.4f,
            "vehicle costs: an old save's car record (kilometres, tyres, brakes) that holds NaN, infinity or a wild number is put back within its bounds, and a good one is left alone");
        Check(VehicleCostRules.MendDay(-3, 500, 60) == 0 && VehicleCostRules.MendDay(2100000000, 500, 60) == 0 && VehicleCostRules.MendDay(480, 500, 60) == 480
              && VehicleCostRules.MendDay(500, 500, 60) == 500 && VehicleCostRules.MendDay(620, 500, 60) == 620 && VehicleCostRules.MendDay(621, 500, 60) == 0,
            "vehicle costs: a day in a car record that is not a day of the game (negative, or more than two years ahead) is read as never");

        // ---- a save that already holds the wrong charge
        Check(LifeMoneyRules.IsWrapped(-2147483648f, "reallife_transaction_vehicle") && LifeMoneyRules.IsWrapped(LifeMoneyRules.Wrapped, "reallife_transaction_travel_event")
              && !LifeMoneyRules.IsWrapped(-2147483648f, "ba:transaction_vehiclebought") && !LifeMoneyRules.IsWrapped(-2147483648f, null)
              && !LifeMoneyRules.IsWrapped(-100f, "reallife_transaction_vehicle") && !LifeMoneyRules.IsWrapped(2147483648f, "reallife_transaction_vehicle")
              && !LifeMoneyRules.IsWrapped(-2147483520f, "reallife_transaction_vehicle"),
            "vehicle costs: a wrong charge in the history is exactly minus 2,147,483,648 under one of the mod's own types, and nothing else is taken for one");
        Check(LifeMoneyRules.Due(2, 0) == 2 && LifeMoneyRules.Due(2, 2) == 0 && LifeMoneyRules.Due(1, 5) == 0 && LifeMoneyRules.Due(3, 1) == 2 && LifeMoneyRules.Due(0, 0) == 0 && LifeMoneyRules.Due(4, -3) == 4
              && LifeMoneyRules.RefundEach == -LifeMoneyRules.Wrapped,
            "vehicle costs: the charges to pay back are those in the history less those paid back already, so a second load pays back nothing, and each is paid back whole");

        // ---- the sources
        string runner = ModSource(@"Life\LifeEventRunner.cs");
        Check(runner.Contains("LifeMoneyRules.Settle(amount, money, outcome.Worst, Prices.Level)") && !runner.Contains("FloorToInt(Mathf.Max(money"),
            "vehicle costs: a life event's money is settled by LifeMoneyRules, with no cast of the purse");
        Check(ModSource(@"Life\LifeEvents.cs").Contains("LifeMoneyRepair.OnLoad();") && ModSource(@"Life\LifeMoneyRepair.cs").Contains("LifeMoneyRules.Due(found, refunded)")
              && ModSource(@"Life\LifeMoneyRepair.cs").Contains("save.modData[Key] = (refunded + due)"),
            "vehicle costs: the load pays back the wrong charges the history holds and keeps the number paid back in the save");
        Check(ModSource(@"Travel\TravelEffects.cs").Contains("WholeMoney.Take(-(long)amount, money)"), "vehicle costs: a trip's mishap is held to the purse in long arithmetic");
        var casts = new List<string>();
        foreach (string file in System.IO.Directory.GetFiles(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts")), "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string text = System.IO.File.ReadAllText(file);
            if (text.Contains("FloorToInt(Mathf.Max(0f, ") || text.Contains("FloorToInt(Mathf.Max(money") || text.Contains("RoundToInt(SaveGameManager.Current.Money)") && !file.EndsWith("GarageShop.cs", StringComparison.Ordinal))
            {
                casts.Add(System.IO.Path.GetFileName(file));
            }
        }
        Check(casts.Count == 0, "vehicle costs: no source casts the player's money to an int itself, every one goes through WholeMoney (" + string.Join(", ", casts) + ")");
        string cars = ModSource(@"Transport\CarOwnership.Tests.cs");
        Check(cars.Contains("public void TestCosts()") && cars.Contains("VehicleCostRules.Probe(level, out string first)") && cars.Contains("\"Vehicle cost check (")
              && ModSource(@"Core\RealLifeOverhaulMod.cs").Contains("AddButton(\"reallife_test_vehicle_costs\", () => CarOwnership.Instance?.TestCosts())"),
            "vehicle costs: the Options test runs the car cost rules on the player's cars, logs PASS or FAIL for each and for the worst inputs, and is a button");
        string garage = ModSource(@"Transport\CarGarage.cs");
        Check(garage.Contains("mended += Mend(record);") && garage.Contains("public static float PremiumOf(float price, int claims, float tuning)") && ModSource(@"Transport\CarOwnership.cs").Contains("CarRules.PremiumOf(")
              && ModSource(@"Transport\CarOwnership.cs").Contains("VehicleCostRules.Km(speed, DriveTick)") && ModSource(@"Traffic\CrashConsequences.cs").Contains("VehicleCostRules.Liability("),
            "vehicle costs: the premium, the kilometres, the crash damages and the saved car records go through the cost rules");

        // ---- the texts
        string[] codes = { "en", "pt", "zh-cn" };
        bool texts = true;
        string[] keys = { "reallife_test_vehicle_costs", "reallife_test_vehicle_costs_pass", "reallife_test_vehicle_costs_fail", "reallife_wrapped_refunded",
                          "reallife_transaction_wrapped_refund", "reallife_transaction_wrapped_refund_label" };
        foreach (string code in codes)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(LocalesFolder(), code + ".json"));
            foreach (string key in keys)
            {
                texts &= words != null && words.TryGetValue(key, out string v) && !string.IsNullOrWhiteSpace(v);
            }
        }
        Check(texts, "vehicle costs: the test's label and notices and the refund's notice and transaction have a text in English, Portuguese and Chinese");
        Check(FixLineEverywhere("a life event could take 2.1 billion at once", "um evento da vida podia levar 2,1 bilhões de uma vez", "生活事件可能一次扣走 21 亿",
                                "konnte ein Lebensereignis auf einmal 2,1 Milliarden abbuchen", "un événement de vie pouvait prélever 2,1 milliards d'un coup",
                                "un suceso de la vida podía quitar 2.100 millones de golpe", "событие из жизни могло разом списать 2,1 миллиарда",
                                "kon een levensgebeurtenis in één keer 2,1 miljard afschrijven", "bir yaşam olayı bir anda 2,1 milyar çekebiliyordu"),
            "vehicle costs: What's new has the line, with its fix mark, in some row of the nine languages written by hand");
        bool later = true;
        string[] laterCodes = { "pl", "ko" };
        string[] laterPieces = { "zdarzenie z życia mogło zabrać od razu 2,1 miliarda", "생활 이벤트가 한 번에 21억을 가져갈 수 있었고" };
        string[] laterMarks = { "Naprawiono: ", "수정: " };
        for (int l = 0; l < laterCodes.Length; l++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(LocalesFolder(), laterCodes[l] + ".json"));
            bool found = false;
            foreach (KeyValuePair<string, string> row in words ?? new Dictionary<string, string>())
            {
                int at = row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal) ? row.Value.IndexOf(laterPieces[l], StringComparison.Ordinal) : -1;
                found |= at > 0 && row.Value.LastIndexOf(laterMarks[l], at, StringComparison.Ordinal) >= 0;
            }
            later &= found;
        }
        Check(later, "vehicle costs: What's new has the line, with its fix mark, in some row of Polish and Korean too");
    }
}
