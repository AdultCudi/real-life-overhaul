using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// The game leaves the mod's buildings alone (Scripts/Core/HeldUnits*.cs): every address the mod holds or reserves,
// counted by claim, out of the game's night of letting while it stands empty, and on the game's ignore list only where
// the game's rivals' step has nothing of a rival's to blank. The rules are called; the game's side is read as text:
// every claim goes through the book into the one owner, only its Game file writes the game's two lists, and nothing of
// it reaches the save.
internal static partial class Program
{
    private static void HeldUnitsChecks()
    {
        // ---------------------------------------------------------------- the ignore list's one rule (moved from the conversions)
        bool ours = false;
        int add = HeldUnitsRules.IgnoreChange(true, false, ref ours);
        bool oursAfterAdd = ours;
        int off = HeldUnitsRules.IgnoreChange(false, true, ref ours);
        Check(add == 1 && oursAfterAdd && off == -1 && !ours, "held units: an address the mod put on the game's ignore list is the mod's, and it takes it off");
        ours = false;
        int theirs = HeldUnitsRules.IgnoreChange(true, true, ref ours);
        bool oursAfterTheirs = ours;
        int left = HeldUnitsRules.IgnoreChange(false, true, ref ours);
        Check(theirs == 0 && !oursAfterTheirs && left == 0 && !ours, "held units: an address on the list already (the game's own, another mod's) is never the mod's to take off");
        ours = true;
        int gone = HeldUnitsRules.IgnoreChange(false, false, ref ours);
        int again = HeldUnitsRules.IgnoreChange(true, true, ref ours);
        Check(gone == 0 && again == 0 && !ours, "held units: the mod's entry taken off by somebody else is no longer the mod's; one listed by another stays theirs");

        // ---------------------------------------------------------------- what may be listed, what sits out of the night
        Check(HeldUnitsRules.MayList(true, false, false, false, false) && !HeldUnitsRules.MayList(false, false, false, false, false),
            "held units: a held address with nothing of a rival's goes on the ignore list; one not held never does");
        Check(!HeldUnitsRules.MayList(true, true, false, false, false) && !HeldUnitsRules.MayList(true, false, true, false, false),
            "held units: an address with a rival landlord or a rival's business stays off the ignore list (the rivals' step would blank them, and the save would keep it)");
        Check(!HeldUnitsRules.MayList(true, false, false, true, false), "held units: an address for sale stays off the ignore list (a rival may buy it at midnight, before the mod looks again)");
        Check(!HeldUnitsRules.MayList(true, false, false, false, true), "held units: a guest in a Going Public session puts nothing on the ignore list: the rivals there are the host's");
        Check(HeldUnitsRules.OutOfPass(true, true, false, false) && !HeldUnitsRules.OutOfPass(false, true, false, false) && !HeldUnitsRules.OutOfPass(true, false, false, false),
            "held units: a held unit standing empty sits out of the game's night of letting; one with a business stays in, so the game may still close it");
        Check(!HeldUnitsRules.OutOfPass(true, true, true, false) && !HeldUnitsRules.OutOfPass(true, true, false, true),
            "held units: a player's own building (to let by the player) and a special service (never let by the game) stay as the game has them");

        // ---------------------------------------------------------------- the ledger: claims counted per address
        var ledger = new HeldLedger();
        Check(ledger.Hold("book:police_1", "ba:street_fourthstreet", 11, HeldKind.Police) && !ledger.Hold("book:police_1", "ba:street_fourthstreet", 11, HeldKind.Police)
              && ledger.Holds("ba:street_fourthstreet", 11) && ledger.Count == 1 && ledger.Claims == 1,
            "held units: a claim holds its address, and the same claim again changes nothing");
        ledger.Hold("offices:ba:street_fourthstreet#11", "ba:street_fourthstreet", 11, HeldKind.Conversions);
        HeldSpot shared = ledger.At("ba:street_fourthstreet", 11);
        Check(shared != null && shared.Holders == 2 && shared.Kind == HeldKind.Police, "held units: two features on one address count two claims; the first one's kind stands");
        ledger.Release("book:police_1");
        Check(ledger.Holds("ba:street_fourthstreet", 11) && shared.Holders == 1, "held units: one feature letting go leaves the address held for the other");
        ledger.Hold("offices:ba:street_fourthstreet#11", "ba:street_ninthavenue", 13, HeldKind.Conversions);
        Check(!ledger.Holds("ba:street_fourthstreet", 11) && ledger.Holds("ba:street_ninthavenue", 13) && ledger.Claims == 1,
            "held units: a claim moved to another address lets go of the old one (a police station moving, a hotel or tower giving up its unit)");
        Check(!ledger.Release("nobody") && !ledger.Hold(null, "x", 1, HeldKind.Police) && !ledger.Hold("a", null, 1, HeldKind.Police), "held units: nothing to let go, and a claim with no holder or street, change nothing");

        // The game's ignore list as a list of keys: the game's own 23 Third Street and another mod's entry are on it first.
        var list = new List<string> { HeldUnitsRules.Key("ba:street_thirdstreet", 23), HeldUnitsRules.Key("ba:street_othermod", 1) };
        var rivalOwned = new HashSet<string>(StringComparer.Ordinal);
        Func<HeldSpot, bool> mayList = s => HeldUnitsRules.MayList(true, rivalOwned.Contains(HeldUnitsRules.Key(s.Street, s.Number)), false, false, false);
        Func<HeldSpot, bool> listed = s => list.Contains(HeldUnitsRules.Key(s.Street, s.Number));
        Func<HeldSpot, bool, bool> ignore = (s, want) =>
        {
            string key = HeldUnitsRules.Key(s.Street, s.Number);
            int at = list.IndexOf(key);
            bool mine = s.Ours;
            int change = HeldUnitsRules.IgnoreChange(want, at >= 0, ref mine);
            if (change > 0)
            {
                list.Add(key);
            }
            else if (change < 0)
            {
                list.RemoveAt(at);
            }
            return mine;
        };
        var city = new HeldLedger();
        city.Hold("book:police_2", "ba:street_secondavenue", 2, HeldKind.Police);
        city.Hold("book:techhq_tower_0", "ba:street_tenthstreet", 12, HeldKind.Towers);
        city.Hold("reserve:utilities_office", "ba:street_thirdstreet", 23, HeldKind.Utilities);   // an address the game listed itself
        city.Hold("book:hotel_1", "ba:street_othermod", 1, HeldKind.Hotels);                       // and one another mod listed
        rivalOwned.Add(HeldUnitsRules.Key("ba:street_tenthstreet", 12));
        int changed = city.Apply(true, mayList, listed, ignore);
        Check(changed == 1 && list.Count == 3 && list.Contains(HeldUnitsRules.Key("ba:street_secondavenue", 2)) && !list.Contains(HeldUnitsRules.Key("ba:street_tenthstreet", 12)),
            "held units: the held addresses go on the list, but not one with a rival landlord (" + changed + " added, " + list.Count + " on the list)");
        city.Hold("book:courthouse", "ba:street_secondavenue", 2, HeldKind.Courthouse);
        city.Release("book:police_2");
        city.Apply(true, mayList, listed, ignore);
        Check(list.Contains(HeldUnitsRules.Key("ba:street_secondavenue", 2)), "held units: an address another claim still holds keeps its entry when the first claim lets go");
        rivalOwned.Add(HeldUnitsRules.Key("ba:street_secondavenue", 2));
        city.Apply(true, mayList, listed, ignore);
        Check(!list.Contains(HeldUnitsRules.Key("ba:street_secondavenue", 2)) && city.Holds("ba:street_secondavenue", 2),
            "held units: an address a rival comes to own leaves the list at the next look, and stays held");
        rivalOwned.Clear();
        city.Apply(true, mayList, listed, ignore);
        list.Remove(HeldUnitsRules.Key("ba:street_secondavenue", 2));   // somebody else took the mod's entry off
        city.Apply(true, mayList, listed, ignore);
        Check(list.Contains(HeldUnitsRules.Key("ba:street_secondavenue", 2)), "held units: the mod's entry taken off by somebody else while still held is put back");
        city.Apply(false, mayList, listed, ignore);
        Check(list.Count == 2 && city.Count == 4, "held units: not ready (loading, a fault, the save prepared for removal): every entry of the mod's off, the claims kept");
        city.Apply(true, mayList, listed, ignore);
        city.ReleaseAll();
        city.Apply(false, mayList, listed, ignore);
        Check(city.Count == 0 && city.Claims == 0 && list.Count == 2 && list[0] == HeldUnitsRules.Key("ba:street_thirdstreet", 23) && list[1] == HeldUnitsRules.Key("ba:street_othermod", 1),
            "held units: the city unloading empties the ledger and takes every entry of the mod's off; the game's own and another mod's stay, in their places");

        var stamp = new HeldLedger();
        int s0 = stamp.Stamp;
        stamp.Hold("a", "s", 1, HeldKind.Police);
        int s1 = stamp.Stamp;
        stamp.Hold("a", "s", 1, HeldKind.Police);
        Check(s1 != s0 && stamp.Stamp == s1, "held units: the ledger's stamp moves on a change only");
        var byKind = new int[(int)HeldKind.Count];
        city.Hold("book:police_1", "p", 1, HeldKind.Police);
        city.Hold("book:hotel_1", "h", 1, HeldKind.Hotels);
        city.Hold("reserve:player_office_1", "r", 1, HeldKind.Reserved);
        city.Tally(byKind);
        Check(HeldUnitsRules.Counts(byKind) == "police 1, towers 0, university 0, hotels 1, utilities 0, courthouse 0, reserved 1, conversions 0",
            "held units: the self-check counts the held addresses by feature, in the check line's order: " + HeldUnitsRules.Counts(byKind));

        // ---------------------------------------------------------------- the book's claims: every claimer, present and future
        var book = new SpecialNameBook();
        bool lawOn = true, uniOn = true, utilOn = true;
        book.Reserve(CityReserves.PlayerOfficeId + "1", CityReserves.PlayerOfficeStreets[0], CityReserves.PlayerOfficeNumbers[0], null);
        book.Reserve(CityReserves.UtilitiesId, CityReserves.UtilitiesStreet, CityReserves.UtilitiesNumber, () => utilOn, SpecialKind.Civic);
        book.Reserve("campus", "ba:street_secondavenue", 26, () => uniOn, SpecialKind.Campus);
        book.Set("police_1", "ba:street_fourthstreet", 11, SpecialKind.Police, "reallife_law_precinct_murrayhill", null, () => lawOn);
        book.Set("techhq_tower_0", "ba:street_tenthstreet", 12, SpecialKind.Other, "Harbourgate Tower", null, null, literal: true);
        book.Set("hotel_1", "ba:street_fourthavenue", 1, SpecialKind.Hotel, "reallife_hotel_1");
        book.Set("courthouse", "ba:street_fifthavenue", 70, SpecialKind.Court, "reallife_justice_door_courthouse");
        book.Set(CityReserves.UtilitiesId, CityReserves.UtilitiesStreet, CityReserves.UtilitiesNumber, SpecialKind.Civic, "reallife_util_desk_title", null, () => utilOn);
        var claims = new List<HeldClaim>();
        book.Holding(claims);
        var held = new HeldLedger();
        held.Sync(HeldUnitsRules.BookPrefix, claims);
        held.Sync(HeldUnitsRules.ReservePrefix, claims);
        held.Tally(byKind);
        Check(claims.Count == 8 && held.Count == 7 && held.At(CityReserves.UtilitiesStreet, CityReserves.UtilitiesNumber).Holders == 2
              && HeldUnitsRules.Counts(byKind) == "police 1, towers 1, university 1, hotels 1, utilities 1, courthouse 1, reserved 1, conversions 0",
            "held units: every entry and reservation of the book is a claim, each feature counted by what it is (" + HeldUnitsRules.Counts(byKind) + ")");
        lawOn = false;
        book.Holding(claims);
        held.Sync(HeldUnitsRules.BookPrefix, claims);
        Check(!held.Holds("ba:street_fourthstreet", 11) && held.Holds("ba:street_tenthstreet", 12), "held units: a feature's option switched off lets go of its building at once (the police stations off)");
        lawOn = true;
        book.Set("police_1", "ba:street_fifthavenue", 9, SpecialKind.Police, "reallife_law_precinct_murrayhill", null, () => lawOn);
        book.Remove("hotel_1");
        uniOn = false;
        book.Holding(claims);
        held.Sync(HeldUnitsRules.BookPrefix, claims);
        held.Sync(HeldUnitsRules.ReservePrefix, claims);
        Check(held.Holds("ba:street_fifthavenue", 9) && !held.Holds("ba:street_fourthstreet", 11) && !held.Holds("ba:street_fourthavenue", 1) && !held.Holds("ba:street_secondavenue", 26),
            "held units: a precinct moving holds its new unit and lets go of the old; a hotel given up and a reservation switched off are let go");
        Check(!book.Refuses(CityReserves.PlayerOfficeStreets[0], CityReserves.PlayerOfficeNumbers[0], false, false) && held.Holds(CityReserves.PlayerOfficeStreets[0], CityReserves.PlayerOfficeNumbers[0]),
            "held units: the offices kept for the player are held from the game's letting and still not refused to the player");
        book.Clear();
        book.Holding(claims);
        held.Sync(HeldUnitsRules.BookPrefix, claims);
        held.Sync(HeldUnitsRules.ReservePrefix, claims);
        Check(!held.Holds("ba:street_fifthavenue", 70) && held.Holds(CityReserves.UtilitiesStreet, CityReserves.UtilitiesNumber) && held.Holds(CityReserves.PlayerOfficeStreets[0], CityReserves.PlayerOfficeNumbers[0]),
            "held units: the book emptied lets go of every building put in it; the reservations, which outlive it, stay held");
        Check(HeldUnitsRules.KindOf(SpecialKind.Other) == HeldKind.Towers && HeldUnitsRules.ReservedKind(CityReserves.PlayerOfficeId + "2", SpecialKind.Other) == HeldKind.Reserved,
            "held units: the towers (SpecialKind.Other) and the second kept office are counted where they belong");

        HeldUnitsSources();
    }

    // The game's side read as text: one owner, every claim through it, every way out, nothing saved.
    private static void HeldUnitsSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string Read(string file)
        {
            string path = System.IO.Path.Combine(scripts, file);
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }
        string owner = Read(@"Core\HeldUnits.cs"), game = Read(@"Core\HeldUnitsGame.cs"), rules = Read(@"Core\HeldUnitsRules.cs"), special = Read(@"Core\SpecialBuildings.cs");
        Check(owner.Length > 0 && game.Length > 0 && rules.Length > 0, "held units: the owner, its Game file and its rules are there");

        // Only the Game file writes the game's two lists; every other file of the mod only asks them.
        bool onlyAsks = true;
        string where = null;
        foreach (string path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            if (System.IO.Path.GetFileName(path) == "HeldUnitsGame.cs")
            {
                continue;
            }
            foreach (string line in File.ReadAllLines(path))
            {
                string code = line.TrimStart();
                if (code.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;   // a comment names them freely
                }
                if (Regex.IsMatch(code, @"IgnoredAddresses(?!\.Contains\()"))
                {
                    onlyAsks = false;
                    where = System.IO.Path.GetFileName(path) + " (the ignore list)";
                }
                // Uptown's game files write its own district's list alone, which no held unit is ever in (UptownLifeChecks holds them to it).
                bool uptown = path.Replace('\\', '/').Contains("/Uptown/LifeGame");
                if (!uptown && Regex.IsMatch(code, @"AllNeighbourhoodBuildings(?!\.TryGetValue\(|\)| == null)"))
                {
                    onlyAsks = false;
                    where = System.IO.Path.GetFileName(path) + " (the district lists)";
                }
                if (System.IO.Path.GetFileName(path) != "SpecialBuildings.cs" && code.Contains("new SpecialNameBook("))
                {
                    onlyAsks = false;
                    where = System.IO.Path.GetFileName(path) + " (a book of its own)";
                }
            }
        }
        Check(onlyAsks, "held units: only Core/HeldUnitsGame.cs writes the game's ignore list and district lists, and the one book of claims is Core/SpecialBuildings' (" + (where ?? "none elsewhere") + ")");
        Check(Regex.Matches(game, @"list\.Add\(address\)|list\.RemoveAt\(at\)").Count == 2 && MemberText(game, "public static bool Ignore(HeldSpot spot, bool want)").Contains("HeldUnitsRules.IgnoreChange(want, at >= 0, ref ours)"),
            "held units: the one write to the ignore list goes by the one rule, with the record of whether the entry is the mod's");

        // Every change of the book reaches the owner: each mutating member calls the one hook, and the hook calls the owner.
        bool hooked = true;
        foreach (string member in new[] { "public static bool Set(string id, string street", "public static bool Reserve(", "public static void Unreserve(", "public static bool SetNamed(", "public static bool Remove(", "internal static void Clear()", "internal static void LookShown()" })
        {
            string body = MemberText(special, member);
            hooked &= body.Length > 0 && (body.Contains("Changed();") || body.Contains("HeldUnits.BookChanged();"));
        }
        Check(hooked && MemberText(special, "private static void Changed()").Contains("HeldUnits.BookChanged();"),
            "held units: every change of the book (set, named, reserved, unreserved, removed, emptied, an option switched) tells the owner at once");
        Check(Regex.Matches(special, @"Book\.(Set|Remove|Reserve|Unreserve|Clear)\(").Count == 6 && MemberText(owner, "internal static void BookChanged()").Contains("Apply();")
              && MemberText(owner, "private static void Apply()").Contains("SpecialBuildings.Holding(BookClaims)"),
            "held units: what the book holds is taken in from the book itself, so a claim made the way every feature makes one is covered, a later one included");

        // The night: out at the first hour event, back at the day event, back at the next look if the day event never came.
        string hour = MemberText(owner, "private void OnHour()"), day = MemberText(owner, "private void OnDay()"), tick = MemberText(owner, "private IEnumerator Tick()");
        Check(hour.Contains("save.Hour != 0") && hour.Contains("HeldUnitsGame.Out(Spots)") && day.Contains("HeldUnitsGame.Back()") && tick.Contains("if (HeldUnitsGame.IsOut)") && tick.Contains("HeldUnitsGame.Back()"),
            "held units: the held empty units are out of the district lists only from the night's first hour event to its day event, and put back at the next look if a step of the night threw");
        Check(MemberText(game, "public static int Back()").Contains("pair.Key.AddRange(pair.Value)") && MemberText(game, "public static int Out(").Contains("copy.AddRange(list)"),
            "held units: each district list is put back whole, in the order the game had it");

        // Every way out: the city unloading (and the save prepared for removal, which destroys the city's objects), a fault.
        string destroy = MemberText(owner, "private void OnDestroy()"), unload = MemberText(owner, "private static void Unload(string why)"), down = MemberText(owner, "private static void StandDown()");
        Check(destroy.Contains("Unload(") && unload.IndexOf("HeldUnitsGame.Back()", StringComparison.Ordinal) >= 0 && unload.IndexOf("Ledger.ReleaseAll()", StringComparison.Ordinal) < unload.IndexOf("Apply();", StringComparison.Ordinal)
              && down.Contains("HeldUnitsGame.Back()") && down.Contains("Ledger.Apply(false,") && owner.Contains("!Meta.SaveTools.Dormant"),
            "held units: the city unloading, the save prepared for removing the mod and a fault each put both lists back as the game had them");

        // Nothing of it reaches the save, and nothing is sent.
        string all = owner + game + rules;
        Check(!all.Contains("modData") && !all.Contains("SaveHook") && !all.Contains("onSaveGame") && !all.Contains("\"reallife:") && !all.Contains("Send("),
            "held units: nothing of the held addresses is written into the save or sent to another machine");
        Check(owner.Contains("Going Public: every machine holds the same addresses") && MemberText(game, "public static bool MayList(HeldSpot spot, GameInstance save)").Contains("GoingPublic.IsGuest"),
            "held units: a Going Public guest holds the same addresses and puts nothing on the ignore list");

        // The texts: the test's in the three languages, the notices short enough, What's new in all nine.
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json")), pt = LoadWords(System.IO.Path.Combine(LocalesFolder(), "pt.json")), zh = LoadWords(System.IO.Path.Combine(LocalesFolder(), "zh-cn.json"));
        bool texts = en != null && pt != null && zh != null;
        foreach (string key in new[] { "reallife_held_test", "reallife_held_test_none", "reallife_held_test_missing", "reallife_held_test_pass", "reallife_held_test_fail" })
        {
            texts &= en != null && en.ContainsKey(key) && en[key].Length <= 240 && pt != null && pt.ContainsKey(key) && zh != null && zh.ContainsKey(key) && owner.Contains("\"" + key + "\"") == (key != "reallife_held_test");
        }
        Check(texts, "held units: the test's button and notices are in en, pt and zh-cn, each notice in en at or under 240 characters, and each used");
        bool news = true;
        // The fix line's opening in each language, in whichever row of What's new holds it (the next row, or the version's once numbered).
        var opens = new Dictionary<string, string>
        {
            { "en", "Fixed: the mod's police stations" }, { "pt", "Corrigido: as delegacias" }, { "zh-cn", "已修复：模组的警察局" }, { "de", "Behoben: Die Polizeiwachen" },
            { "fr", "Corrigé : les commissariats" }, { "es", "Corregido: las comisarías" }, { "ru", "Исправлено: полицейские участки" },
            { "nl", "Opgelost: de politiebureaus" }, { "tr", "Düzeltildi: modun polis karakolları" }
        };
        foreach (KeyValuePair<string, string> open in opens)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(LocalesFolder(), open.Key + ".json"));
            news &= words != null && WhatsNewHas(words, open.Value);
        }
        Check(news, "held units: What's new says it in all nine languages");
    }
}
