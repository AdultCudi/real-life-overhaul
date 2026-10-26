using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;

// A player's report (25 Oct 2026, the suggestions forum): staff hired through search firms "vanish", and some share a name; and
// two K1 tech firms (an AI lab and a software studio) "could not develop new products" at 24.
// Names: the game's RecruitmentHelper.GetRandomGenderAndName keeps its taken names in a cache built once from the hired staff
// only (CachedExistingEmployeeNames), so the mod's seekers (kept in the save for weeks), its payroll and the candidates waiting
// were never in it, and a new session started it afresh without them: the game's headhunters and the mod's search firms could
// hand out a name someone had. Now every name the sector makes is checked against all names in use (StaffNameRules), and
// doubled names are mended on load and each day. A hire the game turns down left a candidate in the staff app, and a start
// that could not be made was tried every day in silence or dropped without a word.
// Products: the sector held 16 products being built or on sale across ALL the player's firms, said nowhere, withdrawn products
// stayed in the list for ever and the save read back only 32, so the newest could be lost. Now 12 a firm, 36 together,
// 12 withdrawn kept, the numbers on the firm's, the products' and the new product's pages, and the Guide; a product holds
// 24 features at most and its page says so.
// First the rules on a model of the game's name cache (the old way and the new), the products' room, the save, the sources, the texts.
internal static partial class Program
{
    private static void HeadhunterAndProducts()
    {
        HeadhunterNameRules();
        HeadhunterNameModel();
        HeadhunterProductRoom();
        HeadhunterProductSave();
        HeadhunterSources();
        HeadhunterWords();
    }

    // ---------------------------------------------------------------- the name rules

    private static void HeadhunterNameRules()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ada North", "Ben South" };
        string[] rolls = { "Ada North", "ben south", "Cy East" };
        int at = 0;
        string got = StaffNameRules.Fresh(() => rolls[at++], taken);
        Check(got == "Cy East" && at == 3 && taken.Contains("Cy East") && taken.Count == 3,
            "names: a roll that someone has (in any case) is passed over for the next, and the one chosen goes into the list");
        var every = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Dee West" };
        string initialled = StaffNameRules.Fresh(() => "Dee West", every);
        Check(initialled.Length > 0 && initialled != "Dee West" && initialled.StartsWith("Dee ", StringComparison.Ordinal) && initialled.EndsWith(" West", StringComparison.Ordinal) && every.Count == 2,
            "names: when every roll is taken the name gets a middle initial and is still one nobody has (" + initialled + ")");
        var empty = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string blank = StaffNameRules.Fresh(() => "?", empty);
        Check(StaffNameRules.IsName(blank) && blank != "?" && empty.Contains(blank), "names: a failed roll ('?') is never handed out as a name");
        var list = new List<string> { "Ada North", "ADA NORTH ", "?", "", "?", null, "Ben South", "ada north", "Ben South" };
        var doubles = new List<int>();
        int n = StaffNameRules.Doubles(list, doubles);
        Check(n == 3 && doubles[0] == 1 && doubles[1] == 7 && doubles[2] == 8,
            "names: the later bearers of a name are found (the first keeps it), in any case, blanks and '?' passed over");
        Check(StaffNameRules.Doubles(new List<string> { "A B", "C D" }, doubles) == 0 && doubles.Count == 0, "names: two different names are no double");
        // determinism: the same rolls, the same name
        var one = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "X Y" };
        var two = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "X Y" };
        int a = 0, b = 0;
        string[] seq = { "X Y", "X Y", "Q R" };
        Check(StaffNameRules.Fresh(() => seq[a++], one) == StaffNameRules.Fresh(() => seq[b++], two), "names: the same rolls give the same name");
    }

    // The game's cache as the decompiled RecruitmentHelper has it (built once from the hired staff, every roll added), a pool of six
    // names, and a session that starts again: the way the player's save went wrong, and the way it goes now.
    private static void HeadhunterNameModel()
    {
        string[] pool = { "P0", "P1", "P2", "P3", "P4", "P5" };
        int oldShared = 0, newShared = 0, mendedAway = 0, seekerClash = 0;
        for (int seed = 1; seed <= 60; seed++)
        {
            // session one: three seekers named by the game while it knew only the hired staff (none)
            var rng = new Random(seed);
            var seekers = new List<string>();
            var cache = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < 3; i++)
            {
                seekers.Add(OldRoll(rng, pool, cache));
            }
            // a new session: the cache starts again from the hired staff (nobody), the seekers are in the save; the game's headhunter makes three candidates
            var cache2 = new HashSet<string>(StringComparer.Ordinal);
            var candidates = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                candidates.Add(OldRoll(rng, pool, cache2));
            }
            var all = new List<string>(seekers);
            all.AddRange(candidates);
            oldShared += StaffNameRules.Doubles(all, new List<int>()) > 0 ? 1 : 0;

            // the new way: the mod's names are checked against everyone in use, and a mend gives later bearers new ones
            var rng2 = new Random(seed);
            var inUse = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seekers2 = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                seekers2.Add(StaffNameRules.Fresh(() => pool[rng2.Next(pool.Length)], inUse));
            }
            seekerClash += StaffNameRules.Doubles(seekers2, new List<int>()) > 0 ? 1 : 0;
            var cache3 = new HashSet<string>(StringComparer.Ordinal);
            var candidates2 = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                candidates2.Add(OldRoll(rng2, pool, cache3));   // the game's own headhunter still rolls as it did
            }
            var everyone = new List<string>(seekers2);
            everyone.AddRange(candidates2);
            var doubled = new List<int>();
            if (StaffNameRules.Doubles(everyone, doubled) > 0)
            {
                var taken = new HashSet<string>(everyone, StringComparer.OrdinalIgnoreCase);
                var spare = new[] { "S0", "S1", "S2", "S3", "S4", "S5", "S6", "S7" };
                int s = 0;
                for (int d = 0; d < doubled.Count; d++)
                {
                    everyone[doubled[d]] = StaffNameRules.Fresh(() => spare[s++ % spare.Length], taken);
                    mendedAway++;
                }
            }
            newShared += StaffNameRules.Doubles(everyone, new List<int>()) > 0 ? 1 : 0;
        }
        Check(oldShared > 0, "names model: the old way, the game's cache starting afresh without the seekers, does give a candidate a seeker's name (" + oldShared + " of 60 sessions)");
        Check(seekerClash == 0, "names model: three seekers named from a pool of six, against the list of those in use, never share a name in any of the 60 sessions");
        Check(newShared == 0 && mendedAway > 0, "names model: with the list of everyone in use and the mend, no name is shared in any of the 60 sessions (" + mendedAway + " mended)");
    }

    // RecruitmentHelper.GetRandomGenderAndName: roll until the cache lacks it, add it.
    private static string OldRoll(Random rng, string[] pool, HashSet<string> cache)
    {
        string name = pool[rng.Next(pool.Length)];
        while (cache.Contains(name) && cache.Count < pool.Length)
        {
            name = pool[rng.Next(pool.Length)];
        }
        cache.Add(name);
        return name;
    }

    // ---------------------------------------------------------------- the products' room

    private static FirmsFile ProductFile(params int[] livePerFirm)
    {
        var f = new FirmsFile();
        for (int firm = 0; firm < livePerFirm.Length; firm++)
        {
            f.Firms.Add(new Firm { Place = "St~" + (firm + 1), Kind = (int)FirmKind.Software });
            for (int i = 0; i < livePerFirm[firm]; i++)
            {
                f.Products.Add(FirmsRules.NewProduct(f, "St~" + (firm + 1), ProductKind.Saas, Segment.Consumer, 0, null));
            }
        }
        return f;
    }

    private static void HeadhunterProductRoom()
    {
        Check(FirmsCatalogue.ProductsPerFirm == 12 && FirmsCatalogue.ProductsMost == 36 && FirmsCatalogue.WithdrawnKept == 12 && FirmsCatalogue.FeaturesMost == 24
              && FirmsCatalogue.ProductsStored == 48,
            "products: 12 a firm, 36 together, 12 withdrawn kept, 24 features at most, 48 read back from the save");
        FirmsFile two = ProductFile(8, 8);
        Check(FirmsRules.LiveCount(two, null) == 16 && FirmsRules.Room(two, "St~1") == FirmsRules.RoomYes && FirmsRules.Room(two, "St~2") == FirmsRules.RoomYes,
            "products: two firms with 8 each (16 together, where the old limit stopped both) can each start another");
        FirmsFile one = ProductFile(12, 3);
        Check(FirmsRules.Room(one, "St~1") == FirmsRules.RoomFirmFull && FirmsRules.Room(one, "St~2") == FirmsRules.RoomYes,
            "products: a firm with 12 is full and says its own limit; another firm still has room");
        FirmsFile three = ProductFile(12, 12, 12);
        three.Firms.Add(new Firm { Place = "St~4", Kind = (int)FirmKind.Software });
        Check(FirmsRules.LiveCount(three, null) == 36 && FirmsRules.Room(three, "St~4") == FirmsRules.RoomAllFull && FirmsRules.Room(three, "St~1") == FirmsRules.RoomFirmFull,
            "products: 36 together stops a fourth firm, and says it is the whole limit; a full firm says its own first");
        three.Products[0].Stage = 2;
        Check(FirmsRules.LiveCount(three, null) == 35 && FirmsRules.Room(three, "St~4") == FirmsRules.RoomYes && FirmsRules.Room(three, "St~1") == FirmsRules.RoomYes,
            "products: withdrawing one makes room at once");
        // withdrawn products are kept 12, the oldest go, the live ones never, the one just withdrawn never
        FirmsFile pile = ProductFile(5);
        for (int i = 0; i < 20; i++)
        {
            Product p = FirmsRules.NewProduct(pile, "St~1", ProductKind.Saas, Segment.Consumer, 0, null);
            p.Stage = 2;
            pile.Products.Add(p);
        }
        int firstWithdrawn = pile.Products[5].Id;
        int gone = FirmsRules.TrimWithdrawn(pile);
        int kept = 0, live = 0;
        for (int i = 0; i < pile.Products.Count; i++)
        {
            kept += pile.Products[i].Stage >= 2 ? 1 : 0;
            live += pile.Products[i].Stage < 2 ? 1 : 0;
        }
        Check(gone == 8 && kept == 12 && live == 5 && pile.ProductOf(firstWithdrawn) == null && pile.Products[pile.Products.Count - 1].Stage == 2,
            "products: 20 withdrawn are trimmed to the newest 12 and the 5 live are untouched");
        Product justNow = pile.Products[5];   // the oldest withdrawn one still kept
        Product extra = FirmsRules.NewProduct(pile, "St~1", ProductKind.Saas, Segment.Consumer, 0, null);
        extra.Stage = 2;
        pile.Products.Add(extra);
        int second = pile.Products[6].Id;
        int again = FirmsRules.TrimWithdrawn(pile, justNow);
        Check(again == 1 && pile.ProductOf(justNow.Id) != null && pile.ProductOf(second) == null,
            "products: the product just withdrawn is never the one trimmed, though it is the oldest: the next oldest goes");
        Check(FirmsRules.TrimWithdrawn(pile) == 0, "products: nothing more to trim once it is trimmed");
    }

    private static void HeadhunterProductSave()
    {
        FirmsFile f = ProductFile(12, 12, 12);
        for (int i = 0; i < 12; i++)
        {
            Product p = FirmsRules.NewProduct(f, "St~1", ProductKind.Saas, Segment.Consumer, 0, null);
            p.Stage = 2;
            f.Products.Add(p);
        }
        string text = FirmsStore.Write(f);
        FirmsFile back = FirmsStore.Read(text, out int version);
        int live = 0, withdrawn = 0;
        for (int i = 0; i < back.Products.Count; i++)
        {
            live += back.Products[i].Stage < 2 ? 1 : 0;
            withdrawn += back.Products[i].Stage >= 2 ? 1 : 0;
        }
        Check(back.Products.Count == 48 && live == 36 && withdrawn == 12 && version == FirmsStore.Version,
            "products save: 36 live and 12 withdrawn products come back whole (the old read stopped at 32 and dropped the newest)");
        // an older version let the list grow: 10 live, 60 withdrawn, then 5 live late in the list. The old read stopped at 32 records and lost
        // the 5; now every live one comes back and the withdrawn ones are the newest 12.
        FirmsFile old = ProductFile(10);
        for (int i = 0; i < 60; i++)
        {
            Product p = FirmsRules.NewProduct(old, "St~1", ProductKind.Saas, Segment.Consumer, 0, null);
            p.Stage = 2;
            old.Products.Add(p);
        }
        for (int i = 0; i < 5; i++)
        {
            old.Products.Add(FirmsRules.NewProduct(old, "St~1", ProductKind.Saas, Segment.Consumer, 0, null));
        }
        int lateId = old.Products[old.Products.Count - 1].Id;
        FirmsFile read = FirmsStore.Read(FirmsStore.Write(old), out _);
        int liveRead = 0, wRead = 0;
        for (int i = 0; i < read.Products.Count; i++)
        {
            liveRead += read.Products[i].Stage < 2 ? 1 : 0;
            wRead += read.Products[i].Stage >= 2 ? 1 : 0;
        }
        Check(liveRead == 15 && wRead == FirmsCatalogue.WithdrawnKept && read.ProductOf(lateId) != null && read.Products.Count <= FirmsCatalogue.ProductsStored,
            "products save: an older save with many withdrawn products loads every live one, the one late in the list too, and the newest 12 withdrawn");
    }

    // ---------------------------------------------------------------- the sources

    private static void HeadhunterSources()
    {
        string game = ModSource(@"Tech\Hiring\HiringGame.cs");
        string names = ModSource(@"Tech\People\StaffNamesGame.cs");
        string name = MemberText(game, "public static void Name(Seeker s, HashSet<string> taken)");
        Check(CodeAt(name, "StaffNamesGame.Fresh(taken, out bool female)") > 0 && CodeAt(game, "RecruitmentHelper.GetRandomGenderAndName") < 0
              && CodeAt(names, "RecruitmentHelper.GetRandomGenderAndName()") > 0,
            "names sources: a seeker's name is made by the one place that rolls the game's names, against the list of those in use");
        string all = ModSource(@"Tech\TechDirector.Hiring.cs") + ModSource(@"Tech\TechDirector.Tests.cs") + ModSource(@"Tech\TechDirector.People.cs") + ModSource(@"Tech\TechDirector.Venture.cs")
                     + ModSource(@"Tech\Venture\VentureGame.cs");
        Check(!Regex.IsMatch(all, @"HiringGame\.Name\([a-zA-Z_\.\[\]]+\)") && !Regex.IsMatch(all, @"VentureGame\.Founder\(\)"),
            "names sources: no name is made without the list of everyone in use");
        string director = ModSource(@"Tech\TechDirector.Names.cs");
        Check(CodeAt(director, "StaffNamesGame.Names(_nameSet)") > 0 && CodeAt(director, "AddName(_talent.People[i].Name)") > 0 && CodeAt(director, "AddName(_hiring.Seekers[i].P.Name)") > 0,
            "names sources: the list holds the game's hired staff and candidates, the mod's payroll and the seekers");
        string people = ModSource(@"Tech\TechDirector.People.cs");
        string start = MemberText(people, "private void StartPeople()");
        string day = MemberText(people, "private void PeopleDay(int day)");
        Check(CodeAt(start, "MendNames(save.Day, false)") > 0 && CodeAt(start, "MendNames(save.Day, false)") < CodeAt(start, "ReadPeople(save.Day)")
              && CodeAt(day, "MendNames(day, true)") > 0 && CodeAt(day, "MendNames(day, true)") < CodeAt(day, "ReadPeople(day)"),
            "names sources: doubled names are mended as the people load, before they are read, and each game day, with a notice");
        Check(director.Contains("StaffNamesGame.Hired(_namedStaff, _namedMine)") && director.Contains("if (!_namedMine[at])") && names.Contains("GoingPublic.Foreign(TalentGame.Business(e))")
              && names.Contains("!GoingPublic.InSession") && !director.Contains("RemoveEmployee"),
            "names sources, Going Public: another player's staff are never renamed, candidates are left alone in a session, and nothing is sent");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs") + ModSource(@"Meta\FeatureList.Economy.cs");
        Check(mod.Contains("AddButton(\"reallife_names_test\", () => Tech.TechDirector.Instance?.TestNames())") && mod.Contains("\"reallife_talent_test_course\", \"reallife_names_test\""),
            "names sources: the names test is on the options page and in the feature's list of tests");
        string hiring = ModSource(@"Tech\TechDirector.Hiring.cs");
        string starts = MemberText(hiring, "private int Starts(int day)");
        Check(CodeAt(starts, "HiringGame.Discard(candidate)") > CodeAt(starts, "HiringGame.Hire(candidate, a.Offer)") && CodeAt(starts, "HiringGame.Arrived(candidate, at, s.P.Name, day)") > 0
              && CodeAt(starts, "reallife_hire_note_start_lost") > 0 && CodeAt(starts, "reallife_hire_note_start_failed") > 0 && CodeAt(starts, "HiringCatalogue.StartTries") > 0,
            "hire sources: a hire the game refuses discards its candidate, a start is checked on the staff list and the business, and a start that cannot be made is said");
        Check(CodeAt(MemberText(game, "public static void Discard(EmployeeInstance candidate)"), "EmployeeHelper.DiscardCandidate(candidate)") > 0
              && CodeAt(MemberText(game, "public static bool Arrived("), "Save.EmployeeInstances.Contains(e)") > 0 && HiringCatalogue.StartTries == 3,
            "hire sources: the game's own discard takes the candidate out, and the arrival check reads the save's list");
        string app = ModSource(@"Tech\TechDirector.FirmsApp.cs");
        string newProduct = MemberText(app, "internal Product NewProduct(");
        Check(CodeAt(newProduct, "FirmsRules.Room(_firmsFile, place) != FirmsRules.RoomYes") > 0 && CodeAt(newProduct, "FirmsCatalogue.ProductsMost") < 0
              && CodeAt(MemberText(app, "internal void Withdraw(int id)"), "FirmsRules.TrimWithdrawn(_firmsFile, p)") > 0,
            "products sources: a new product asks the room rule, and a withdrawal trims the list");
        string pages = ModSource(@"Tech\Firms\TechApp.Pages.cs");
        string page = MemberText(pages, "private void WriteNewProduct(InfoBox box, FirmsFile f, FirmPlace firm)");
        Check(CodeAt(page, "RoomLine(f, place)") > 0 && CodeAt(page, "reallife_tech_new_full_firm") > 0 && CodeAt(page, "reallife_tech_new_full_all") > 0
              && CodeAt(page, "if (room == FirmsRules.RoomYes)") > 0 && CodeAt(page, "if (room == FirmsRules.RoomYes)") < CodeAt(page, "\"reallife_tech_button_start\""),
            "products sources: the new product page says the count and which limit, and offers no Start button when there is no room");
        Check(CodeAt(pages, "reallife_tech_product_features_full") > 0 && CodeAt(pages, "reallife_tech_products_room_all") > 0 && CodeAt(pages, "box.Line(RoomLine(f, place));") > 0,
            "products sources: the firm's page, the products' tab and the product's page say the limits");
        Check(CodeAt(ModSource(@"Tech\Firms\FirmsStore.cs"), "FirmsRules.TrimWithdrawn(f);") > 0 && CodeAt(ModSource(@"Tech\Firms\FirmsStore.cs"), "FirmsCatalogue.ProductsStored") > 0,
            "products sources: the save reads back every stored product up to the limit and trims the withdrawn ones");
        string guide = ModSource(@"Tech\TechGuide.cs");
        Check(guide.Contains(".Fact(\"tech_products_firm\"") && guide.Contains(".Fact(\"tech_products_most\"") && guide.Contains(".Fact(\"tech_products_kept\"") && guide.Contains(".Fact(\"tech_features_most\""),
            "products sources: the Guide's four figures are facts filled from the catalogue");
        string rivals = ModSource(@"Tech\Firms\CompetitorRules.cs");
        Check(!rivals.Contains("ProductsMost") && !rivals.Contains("ProductsPerFirm"),
            "products, rivals: a rival company makes no products of its own in the sector, so it has no limit of its own to keep alike");
    }

    // ---------------------------------------------------------------- the texts

    private static void HeadhunterWords()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("names and products texts", words, codes))
        {
            return;
        }
        string[] keys =
        {
            "reallife_tech_products_room", "reallife_tech_new_full_firm", "reallife_tech_new_full_all", "reallife_tech_products_room_all", "reallife_tech_product_features_full",
            "reallife_names_note_mended", "reallife_names_test", "reallife_names_test_pass", "reallife_names_test_fail", "reallife_names_test_off",
            "reallife_hire_note_start_lost", "reallife_hire_note_start_failed"
        };
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            string missing = null;
            foreach (string key in keys)
            {
                if (!words[l].TryGetValue(key, out string value) || value.Length == 0 || value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Contains(" - ") || value.Contains("  "))
                {
                    missing = missing ?? key;
                }
                else if (l > 0 && NamesOf(words[0][key]) != NamesOf(value))
                {
                    missing = missing ?? key + " (its names in curly brackets)";
                }
            }
            Check(missing == null, "names and products texts: every key is in " + codes[l] + ".json, in the style, with the English names (" + (missing ?? "all") + ")");
        }
        foreach (string key in keys)
        {
            Check(words[0][key].Length <= 240, "names and products texts: " + key + " at most 240 characters (" + words[0][key].Length + ")");
        }
        // the figures the texts state are the catalogue's
        string en = words[0]["reallife_tech_new_full_firm"] + words[0]["reallife_tech_products_room"];
        Check(words[0]["reallife_tech_products_room"].Contains("{firm_most}") && words[0]["reallife_tech_products_room_all"].Contains("{kept}") && en.Length > 0,
            "names and products texts: the figures in the product pages are filled from the catalogue, not written in the texts");
        for (int l = 0; l < words.Length; l++)
        {
            string row = words[l].TryGetValue("reallife_changes_next", out string next) ? next : string.Empty;
            string mark = FixMarks[codes[l]];
            int firstFix = row.IndexOf(mark, StringComparison.Ordinal);
            bool both = true;
            int count = 0;
            for (int at = row.IndexOf(mark, StringComparison.Ordinal); at >= 0; at = row.IndexOf(mark, at + mark.Length, StringComparison.Ordinal))
            {
                count++;
            }
            string nameLine = mark + NameFixStarts[codes[l]], productLine = mark + ProductFixStarts[codes[l]];
            both = WhatsNewHas(words[l], nameLine) && WhatsNewHas(words[l], productLine) && HunterCount(row, nameLine) == 1 && HunterCount(row, productLine) == 1;
            int nameAt = row.IndexOf(nameLine, StringComparison.Ordinal), productAt = row.IndexOf(productLine, StringComparison.Ordinal);
            // each line once, the names line then the products line, at the end of the row, no double space (any row of What's new has them, as a release numbers the row)
            Check(both && nameAt >= 0 && productAt > nameAt && row.IndexOf(mark, productAt + mark.Length, StringComparison.Ordinal) < 0 && !row.Contains("  ") && firstFix >= 0 && count >= 2,
                "names and products texts: What's new's next row ends with the two fixes, once each, the names first, no double spaces, in " + codes[l]);
            string productFix = productAt >= 0 ? row.Substring(productAt) : string.Empty;
            Check(productFix.Contains("16") && productFix.Contains("12") && productFix.Contains("36") && productFix.Contains("24"),
                "names and products texts: the product fix states 16, 12, 36 and 24 in " + codes[l]);
        }
        // a line is never repeated within the next row
        for (int l = 0; l < words.Length; l++)
        {
            string row = words[l]["reallife_changes_next"];
            string mark = FixMarks[codes[l]];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool repeated = false;
            string[] parts = row.Split(new[] { mark }, StringSplitOptions.None);
            for (int p = 1; p < parts.Length; p++)
            {
                repeated |= !seen.Add(parts[p].Trim());
            }
            Check(!repeated, "names and products texts: no fix line of the next row is repeated in " + codes[l]);
        }
        // the Guide says the limits in every language, with the four facts
        for (int l = 0; l < words.Length; l++)
        {
            string page = words[l].TryGetValue("reallife_guide_tech_sector", out string g) ? g : string.Empty;
            Check(page.Contains("{tech_products_firm}") && page.Contains("{tech_products_most}") && page.Contains("{tech_features_most}") && page.Contains("{tech_products_kept}"),
                "names and products texts: the Guide's tech page states the product limits in " + codes[l]);
        }
        // the old sentence "a product can launch with" stays, the new bullet follows it
        string pageEn = words[0]["reallife_guide_tech_sector"];
        Check(pageEn.IndexOf("{tech_launch_features}", StringComparison.Ordinal) < pageEn.IndexOf("{tech_products_firm}", StringComparison.Ordinal)
              && pageEn.IndexOf("{tech_products_firm}", StringComparison.Ordinal) < pageEn.IndexOf("# Contracts", StringComparison.Ordinal),
            "names and products texts: the Guide's bullet stands in the Products part");
    }

    private static readonly Dictionary<string, string> NameFixStarts = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "en", "two people in your company" }, { "pt", "duas pessoas da sua empresa" }, { "zh-cn", "你公司里可能有两个人同名" }, { "de", "Zwei Leute in deiner Firma" },
        { "fr", "deux personnes de votre entreprise" }, { "es", "dos personas de tu empresa" }, { "ru", "у двух людей в вашей компании" }, { "nl", "twee mensen in je bedrijf" },
        { "tr", "şirketinizdeki iki kişi" }
    };

    private static readonly Dictionary<string, string> ProductFixStarts = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "en", "a limit on products in tech firms" }, { "pt", "o limite de produtos das empresas de tecnologia" }, { "zh-cn", "科技公司的产品数量上限偏低" },
        { "de", "Die Grenze für Produkte in Technikfirmen" }, { "fr", "la limite de produits des entreprises technologiques" }, { "es", "el límite de productos de las empresas tecnológicas" },
        { "ru", "предел числа продуктов в технологических фирмах" }, { "nl", "de grens voor producten bij techbedrijven" }, { "tr", "teknoloji şirketlerindeki ürün sınırı" }
    };

    private static int HunterCount(string text, string piece)
    {
        int n = 0;
        for (int at = text.IndexOf(piece, StringComparison.Ordinal); at >= 0; at = text.IndexOf(piece, at + piece.Length, StringComparison.Ordinal))
        {
            n++;
        }
        return n;
    }
}
