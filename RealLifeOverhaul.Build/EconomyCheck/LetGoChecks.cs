using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;
using RealLifeOverhaul.Utilities;
using UtilityAccount = RealLifeOverhaul.Utilities.Account;

// A building the player lets go (its lease ended, it was sold with it, shut down and handed back), and an underground car
// park another mod sells as a business: the utilities' accounts and bills (Scripts/Utilities/UtilitiesRules.Fate,
// WriteOff, Serves) and business pressure's closures (Scripts/Business/BusinessState.DropWhere), then the game's side read
// as text (the places read again each hour, nothing closed that is not the player's), the timing and the words.
internal static partial class Program
{
    private const string CarParkKey = "ba%3Astreet_parking~3";       // a car park's place, as UtilitiesGame.Place writes it
    private const string ShopKey = "ba%3Astreet_secondstreet~49";    // an ordinary building's

    private static void LetGo()
    {
        LetGoCarParks();
        LetGoFates();
        LetGoClosures();
        LetGoSources();
        LetGoSpeed();
        LetGoWords();
    }

    private static void LetGoCarParks()
    {
        Check(!UtilitiesRules.Serves("ba:street_parking", null) && !UtilitiesRules.Serves("ba:street_thirdavenue", "ba:buildingsize_parking")
              && !UtilitiesRules.Serves("ba:street_parking", "ba:buildingsize_parking"),
            "let go: an underground car park (the game's car park street, or its car park size) is no place the utilities supply");
        Check(UtilitiesRules.Serves("ba:street_secondstreet", "ba:buildingsize_h") && UtilitiesRules.Serves("ba:street_seventhavenue", "ba:buildingsize_q")
              && UtilitiesRules.Serves(null, null) && UtilitiesRules.Serves(string.Empty, string.Empty),
            "let go: a warehouse, a parking garage made in a warehouse, and a place with nothing known of it are supplied as before");
        Check(UtilitiesRules.Serves("ba:street_parkingx", null) && UtilitiesRules.Serves("BA:STREET_PARKING", null),
            "let go: only the game's own car park street is one (compared exactly, as the game names it)");
        Check(UtilitiesRules.CarParkPlace(CarParkKey) && UtilitiesRules.CarParkPlace(Uri.EscapeDataString("ba:street_parking") + "~12"),
            "let go: an account's place at a car park is known by its address alone (from a save made before the rule)");
        Check(!UtilitiesRules.CarParkPlace(ShopKey) && !UtilitiesRules.CarParkPlace(null) && !UtilitiesRules.CarParkPlace(string.Empty)
              && !UtilitiesRules.CarParkPlace("~3") && !UtilitiesRules.CarParkPlace("ba%3Astreet_parking"),
            "let go: no other place, nor a place written wrong, is taken for a car park");
    }

    private static UtilityAccount LetGoAccount(string place, SupplyState state, int owed, int accrued, int deposit)
    {
        return new UtilityAccount { Place = place, Service = Service.Power, Kind = PlaceKind.Storage, State = state, Owned = true, Owed = owed, Accrued = accrued,
            Deposit = deposit, DueDay = owed > 0 ? 180 : 0, LateCharged = owed > 0, CutForDebt = state == SupplyState.Cut, TemporaryUntil = state == SupplyState.Temporary ? 173 : 0 };
    }

    private static void LetGoFates()
    {
        // The report's car park: still the player's (held), cut for a bill owed. It was kept, billed and closed hour by hour.
        UtilityAccount cut = LetGoAccount(CarParkKey, SupplyState.Cut, 412, 950, 0);
        Check(UtilitiesRules.Fate(cut, true, false) == AccountFate.WriteOff, "let go: a car park the player still holds has its account written off, not kept to be billed and closed");
        Check(UtilitiesRules.Fate(cut, false, false) == AccountFate.WriteOff && UtilitiesRules.Fate(LetGoAccount(ShopKey, SupplyState.Cut, 412, 0, 0), true, true) == AccountFate.WriteOff,
            "let go: so is one the player has let go, and one of another car park the game's reading puts aside");
        Check(UtilitiesRules.Fate(LetGoAccount(CarParkKey, SupplyState.Closed, 412, 0, 0), false, false) == AccountFate.WriteOff,
            "let go: a last bill a save holds for a car park is not asked for either");

        UtilityAccount active = LetGoAccount(ShopKey, SupplyState.Active, 0, 300, 0);
        Check(UtilitiesRules.Fate(active, true, false) == AccountFate.Keep, "let go: an ordinary place the player holds keeps its account");
        Check(UtilitiesRules.Fate(active, false, false) == AccountFate.LastBill && UtilitiesRules.Fate(LetGoAccount(ShopKey, SupplyState.Temporary, 0, 0, 0), false, false) == AccountFate.LastBill
              && UtilitiesRules.Fate(LetGoAccount(ShopKey, SupplyState.Cut, 50, 0, 0), false, false) == AccountFate.LastBill
              && UtilitiesRules.Fate(LetGoAccount(ShopKey, SupplyState.Pending, 0, 0, 0), false, false) == AccountFate.LastBill,
            "let go: a place the player no longer holds has its last bill, however it was supplied");
        Check(UtilitiesRules.Fate(LetGoAccount(ShopKey, SupplyState.Closed, 60, 0, 0), false, false) == AccountFate.Keep
              && UtilitiesRules.Fate(LetGoAccount(ShopKey, SupplyState.Closed, 0, 0, 0), false, false) == AccountFate.TakeOut
              && UtilitiesRules.Fate(LetGoAccount(ShopKey, SupplyState.Closed, 0, 5, 0), false, false) == AccountFate.Keep
              && UtilitiesRules.Fate(null, true, false) == AccountFate.TakeOut,
            "let go: a last bill still owed is kept until paid, a settled one taken out, and nothing for nothing");
        Check(UtilitiesRules.Fate(LetGoAccount(ShopKey, SupplyState.Closed, 0, 0, 0), true, false) == AccountFate.TakeOut,
            "let go: a closed and settled account is taken out even if the place is the player's again (it starts afresh)");

        // What a last bill leaves: closing then asking again gives a place that is never billed or closed again.
        UtilityAccount last = LetGoAccount(ShopKey, SupplyState.Active, 0, 300, 0);
        UtilitiesRules.Bill(last, 175, 30);
        last.State = SupplyState.Closed;
        Check(UtilitiesRules.Fate(last, false, false) == AccountFate.Keep && !UtilitiesRules.Supplied(last, 175) && UtilitiesRules.Bill(last, 205, 30) == 0,
            "let go: after its last bill a place let go is never supplied or billed again, the bill still owed");

        int back = UtilitiesRules.WriteOff(cut);
        Check(back == 0 && cut.Owed == 0 && cut.Accrued == 0 && cut.Units == 0 && cut.DueDay == 0 && !cut.LateCharged && !cut.CutForDebt && cut.State == SupplyState.Closed
              && UtilitiesRules.Fate(cut, false, false) == AccountFate.WriteOff,
            "let go: a written off account owes nothing, has used nothing and is closed");
        UtilityAccount held = LetGoAccount(CarParkKey, SupplyState.Active, 80, 120, 340);
        Check(UtilitiesRules.WriteOff(held) == 340 && held.Deposit == 0 && UtilitiesRules.WriteOff(held) == 0 && UtilitiesRules.WriteOff(null) == 0,
            "let go: a deposit on a car park's account comes back once, and only once");
        UtilityAccount net = new UtilityAccount { Place = CarParkKey, Service = Service.Internet, State = SupplyState.Active, Provider = 1, ContractUntil = 400, OutageFrom = 10, OutageUntil = 14 };
        UtilitiesRules.WriteOff(net);
        Check(net.ContractUntil == 0 && net.OutageFrom == 0 && net.OutageUntil == 0 && net.State == SupplyState.Closed,
            "let go: an internet connection at a car park is written off with no contract left to leave");

        bool same = true;
        for (int i = 0; i < 50; i++)
        {
            same &= UtilitiesRules.Fate(LetGoAccount(CarParkKey, SupplyState.Cut, 412, 950, 0), true, false) == AccountFate.WriteOff
                    && UtilitiesRules.Fate(LetGoAccount(ShopKey, SupplyState.Active, 0, 300, 0), false, false) == AccountFate.LastBill;
        }
        Check(same, "let go: the same account and places give the same fate every time (the same on every machine)");
    }

    private static void LetGoClosures()
    {
        var open = new List<string>(4);
        var dropped = new List<string>(4);
        BusinessState.Load(new Dictionary<string, string>(), 170);
        BusinessState.SetClosure(CarParkKey, ClosureSource.Utilities, 170, 20);
        BusinessState.Hold(CarParkKey, ClosureSource.Issue, 170, 22);
        BusinessState.SetClosure(ShopKey, ClosureSource.Utilities, 170, 20);

        // The player has let the car park go: its closures, both makers', are dropped at once; the shop's is kept.
        BusinessState.DropWhere(place => place == ShopKey, dropped);
        Check(dropped.Count == 1 && dropped[0] == CarParkKey, "let go: a building let go is dropped once, whoever kept it closed (" + dropped.Count + " dropped)");
        Check(BusinessState.ClosureAt(CarParkKey) < 0 && BusinessState.ClosureAt(ShopKey, ClosureSource.Utilities) >= 0 && BusinessState.Closures.Count == 1,
            "let go: nothing of the mod keeps the building let go closed; the business still the player's stays closed");

        // Its hour comes: nothing is reopened as the player's for the building let go; the shop opens.
        BusinessState.EndDue(170, 21, open);
        Check(open.Count == 1 && open[0] == ShopKey, "let go: when the hour comes only the player's own business opens (" + string.Join(", ", open) + ")");
        open.Clear();
        dropped.Clear();
        BusinessState.DropWhere(place => true, dropped);
        Check(dropped.Count == 0, "let go: with every building still the player's nothing is dropped");
        BusinessState.SetClosure(ShopKey, ClosureSource.Issue, 171, 3);
        BusinessState.DropWhere(place => false, dropped);
        BusinessState.DropWhere(place => false, dropped);
        Check(dropped.Count == 1 && BusinessState.Closures.Count == 0 && !BusinessState.Holds(ClosureSource.Issue) && !BusinessState.Holds(ClosureSource.Utilities),
            "let go: dropping twice drops nothing more, and leaves no maker holding a closure");

        // The save: a closure on a building let go before the save is gone after it, and a line without one reads as before.
        BusinessState.SetClosure(ShopKey, ClosureSource.Utilities, 171, 5);
        var data = new Dictionary<string, string>();
        BusinessState.Save(data);
        BusinessState.Load(data, 171);
        dropped.Clear();
        BusinessState.DropWhere(place => place != ShopKey, dropped);
        Check(dropped.Count == 1 && BusinessState.Closures.Count == 0, "let go: a closure read from the save on a building let go since is dropped as the save is read");
        BusinessState.Reset();
    }

    // The game's side, read as text: the hour reads the places again before its effects, the effects and the closures
    // never touch a building that is not the player's, and the car park is put aside where the places are read.
    private static void LetGoSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string Read(string file) => File.Exists(System.IO.Path.Combine(scripts, file)) ? File.ReadAllText(System.IO.Path.Combine(scripts, file)) : string.Empty;
        string director = Read(@"Utilities\UtilitiesDirector.cs"), gone = Read(@"Utilities\UtilitiesDirector.Gone.cs"), game = Read(@"Utilities\UtilitiesGame.cs"),
               business = Read(@"Business\BusinessDirector.cs"), businessGame = Read(@"Business\BusinessGame.cs");
        int hour = director.IndexOf("private void Hour()", StringComparison.Ordinal);
        int refresh = hour >= 0 ? director.IndexOf("\n            Refresh();", hour, StringComparison.Ordinal) : -1;   // (a call on a line of its own, not a comment)
        int effects = hour >= 0 ? director.IndexOf("Effects();", hour, StringComparison.Ordinal) : -1;
        Check(hour >= 0 && refresh > hour && effects > refresh, "let go: each game hour the utilities read the player's places again before they close anything");
        int loop = director.IndexOf("private void Effects()", StringComparison.Ordinal);
        Check(loop >= 0 && director.IndexOf("if (!GoingPublic.Mine(place.Reg))", loop, StringComparison.Ordinal) > loop,
            "let go: the hour's effects pass over a place that is no longer the player's");
        Check(director.Contains("ReadPlaces();") && !director.Contains("UtilitiesGame.Places(_places);") && gone.Contains("UtilitiesGame.Places(_places, _unserved);")
              && gone.Contains("UtilitiesRules.Fate(a, held, Unserved(place))"),
            "let go: every reading of the places goes one way, which puts car parks aside and brings each account to its fate");
        Check(game.Contains("UtilitiesRules.Serves(reg.StreetName, building.BuildingSize)"), "let go: the places read leave out an underground car park by the game's own address and size");
        Check(businessGame.Contains("if (!GoingPublic.Mine(reg) || reg.temporarilyClosed"), "let go: business pressure closes nothing that is not the player's");
        Check(business.Contains("|| reg == null || !GoingPublic.Mine(reg))") && business.Contains("BusinessState.DropWhere(StillHeld, _letGo);")
              && business.Contains("BusinessGame.Freed(_opening[i]);"),
            "let go: a closure is neither made nor kept on a building let go, and the switch it set is cleared rather than reopened as the player's");
        int slow = business.IndexOf("private void Slow()", StringComparison.Ordinal);
        int drop = slow >= 0 ? business.IndexOf("DropLetGo();", slow, StringComparison.Ordinal) : -1;
        int due = slow >= 0 ? business.IndexOf("BusinessState.EndDue(day, hour, _opening);", slow, StringComparison.Ordinal) : -1;
        Check(drop > slow && due > drop, "let go: the closures on buildings let go are dropped as the save is read and each hour, before any closure ends");
        Check(gone.Contains("Utilities check (a place let go): ") && business.Contains("Business check (a building let go): ")
              && Read(@"Economy\Estate\EstateGame.cs").Contains("Estate check (a building let go): "),
            "let go: with Developer diagnostics on, one line when the utilities, business pressure or the estate let a building go");
    }

    private static void LetGoSpeed()
    {
        var accounts = new List<UtilityAccount>(3000);
        for (int i = 0; i < 3000; i++)
        {
            accounts.Add(LetGoAccount(i % 10 == 0 ? CarParkKey : ShopKey, (SupplyState)(i % 5), i % 3, i % 7, 0));
        }
        LetGoFateAll(accounts, 1);
        var watch = Stopwatch.StartNew();
        int fates = LetGoFateAll(accounts, 10);
        watch.Stop();
        Check(watch.Elapsed.TotalMilliseconds < 20.0, "let go: 30,000 accounts brought to their fate in under 20 ms (" + watch.Elapsed.TotalMilliseconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
              + " ms; an hour's reading has a few dozen; " + fates + ")");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int LetGoFateAll(List<UtilityAccount> accounts, int rounds)
    {
        int sum = 0;
        for (int r = 0; r < rounds; r++)
        {
            for (int i = 0; i < accounts.Count; i++)
            {
                sum += (int)UtilitiesRules.Fate(accounts[i], (i & 1) == 0, false);
            }
        }
        return sum;
    }

    private static readonly string[] LetGoMarks = { "Fixed: ", "Corrigido: ", "已修复：", "Behoben: ", "Corrigé : ", "Corregido: ", "Исправлено: " };
    private static readonly string[] LetGoCodes = { "en", "pt", "zh-cn", "de", "fr", "es", "ru" };

    // What's new has the fix's line in all seven languages, each with its own fix mark, in some reallife_changes_ row
    // (never only the next one: the release numbers it).
    private static void LetGoWords()
    {
        string folder = LocalesFolder();
        var missing = new List<string>();
        for (int l = 0; l < LetGoCodes.Length; l++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, LetGoCodes[l] + ".json"));
            bool found = false;
            foreach (KeyValuePair<string, string> row in words ?? new Dictionary<string, string>())
            {
                found |= row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal) && row.Value != null
                         && row.Value.Contains(LetGoMarks[l]) && LetGoSaid(LetGoCodes[l], row.Value);
            }
            if (!found)
            {
                missing.Add(LetGoCodes[l]);
            }
        }
        Check(missing.Count == 0, "let go: What's new has the fix's line, with its fix mark, in every language (missing: " + string.Join(", ", missing) + ")");
    }

    // Words of the fix's line in each language, as written in its What's new row.
    private static bool LetGoSaid(string code, string row)
    {
        switch (code)
        {
            case "en": return row.Contains("Fixed: a building that is no longer yours is let go within the hour");
            case "pt": return row.Contains("Corrigido: um prédio que não é mais seu");
            case "zh-cn": return row.Contains("已修复：不再属于你的建筑");
            case "de": return row.Contains("Behoben: Ein Gebäude, das dir nicht mehr gehört");
            case "fr": return row.Contains("Corrigé : un bâtiment qui n'est plus à vous");
            case "es": return row.Contains("Corregido: un edificio que ya no es tuyo");
            case "ru": return row.Contains("Исправлено: здание, которое вам больше не принадлежит");
            default: return false;
        }
    }
}
