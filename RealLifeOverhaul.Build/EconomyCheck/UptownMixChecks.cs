using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Uptown;

// Uptown's mix (the owner, 25 Oct 2026: "Increase renting more in Uptown", a third free to rent): the share kept free and
// how many units the mix pass changes to stand at it, the kind a unit takes so a street holds many kinds, a street's
// surplus, the names of the city's shops of the mod's kinds, the census's verdict, the store's mark, and the sources: the
// district's targets for shops, offices and homes, the pass once a save after the leases, never a special building, a
// lease's or the player's, every game call seeded and put back, the lent layouts named so the game's own picks never meet
// them, the mod's kinds by the wiring; and the texts in every language.
internal static partial class Program
{
    private static void UptownMixChecks()
    {
        UptownMixShare();
        UptownMixKinds();
        UptownMixStore();
        UptownMixSources();
        UptownMixTexts();
    }

    private static void UptownMixShare()
    {
        Check(MixRules.ToLetShare == 33, "uptown mix: a third of Uptown's shops, offices and homes is kept free to rent");
        Check(MixRules.Kept(0, 33) == 0 && MixRules.Kept(100, 33) == 33 && MixRules.Kept(127, 33) == 42 && MixRules.Kept(3, 33) == 1 && MixRules.Kept(10, 150) == 10,
            "uptown mix: the units kept free are the share of them, rounded as the game rounds it, never more than all");
        Check(MixRules.Change(127, 23, 33) == -19 && MixRules.Change(127, 60, 33) == 18 && MixRules.Change(127, 42, 33) == 0 && MixRules.Change(0, 5, 33) == 0,
            "uptown mix: too few free lets that many businesses go, too many opens businesses, at the share nothing changes");
        Check(MixRules.Change(10, 99, 33) == 7, "uptown mix: never more free than there are units");
        Check(MixRules.ShareWhy("shops", 100, 33, 33) == null && MixRules.ShareWhy("shops", 100, 37, 33) == null && MixRules.ShareWhy("shops", 100, 45, 33) != null
              && MixRules.ShareWhy("homes", 0, 0, 33) == null && MixRules.ShareWhy("offices", 3, 2, 33) == null,
            "uptown mix: the census allows the game's days a few points either way, or one unit");
        Check(MixRules.Order("uptown_2_03_04", "open", "s") == MixRules.Order("uptown_2_03_04", "open", "s") && MixRules.Order("uptown_2_03_04", "open", "s") != MixRules.Order("uptown_2_03_04", "free", "s"),
            "uptown mix: each step's order is the lot's, the step's and the save's seed alone");
    }

    private static void UptownMixKinds()
    {
        var kinds = new List<string> { "ba:businesstype_bookstore", "ba:businesstype_florist", "ba:businesstype_giftshop" };
        var counts = new List<int> { 1, 0, 2 };
        Check(MixRules.Kind(kinds, counts, "uptown_1_02_01", "seed") == 1, "uptown mix: a unit takes the kind its street holds fewest of");
        counts = new List<int> { 0, 0, 0 };
        int a = MixRules.Kind(kinds, counts, "uptown_1_02_01", "seed");
        var reversed = new List<string>(kinds);
        reversed.Reverse();
        int b = MixRules.Kind(reversed, counts, "uptown_1_02_01", "seed");
        Check(a >= 0 && b >= 0 && kinds[a] == reversed[b], "uptown mix: among kinds a street holds alike, the hash picks the same kind in any order");
        var picks = new HashSet<string>();
        for (int i = 0; i < 40; i++)
        {
            picks.Add(kinds[MixRules.Kind(kinds, counts, "uptown_1_02_" + i, "seed")]);
        }
        Check(picks.Count == 3, "uptown mix: different lots take different kinds where their streets hold none");
        Check(MixRules.Kind(new List<string>(), counts, "x", "s") == -1 && MixRules.Kind(null, null, "x", "s") == -1, "uptown mix: no kind that fits is -1");
        Check(!MixRules.TooMany(2) && MixRules.TooMany(3), "uptown mix: a street keeps two of a kind, a third is given another");
        Check(MixRules.ShopName("reallife_icecreamparlour", "uptown_1_02_01", "s") != null && MixRules.ShopName("reallife_tobacconist", "uptown_1_02_01", "s") != null
              && MixRules.ShopName("reallife_teabar", "x", "s") != null && MixRules.ShopName("ba:businesstype_florist", "x", "s") == null
              && MixRules.ShopName("reallife_tobacconist", "uptown_1_02_01", "s") == MixRules.ShopName("reallife_tobacconist", "uptown_1_02_01", "s"),
            "uptown mix: the city's shops of the mod's kinds trade under a proper name picked by the lot, the game's own keep theirs");
        Check(MixRules.Spread(new Dictionary<string, int> { { "ba:businesstype_florist", 2 }, { "reallife_tobacconist", 3 }, { "ba:businesstype_bookstore", 2 } })
              == "reallife_tobacconist 3, bookstore 2, florist 2" && MixRules.Spread(null) == "none",
            "uptown mix: the census names each kind with its count, the most first, ties by name");
    }

    private static void UptownMixStore()
    {
        var s = new LifeState { Mixed = true, Opened = true };
        LifeState back = LifeStore.Read(LifeStore.Write(s), out int _);
        Check(back != null && back.Mixed && back.Opened && LifeStore.Write(s).Contains("^x:1"), "uptown mix: the save's line says the mix pass has run, and reads it back");
        LifeState opened = LifeStore.Read("v=1^f:^o:1", out int _);
        Check(opened != null && opened.Opened && !opened.Mixed, "uptown mix: a save whose opening pass ran in an earlier build gets the mix pass once");
    }

    private static void UptownMixSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string Read(string file)
        {
            string path = System.IO.Path.Combine(scripts, file);
            return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : string.Empty;
        }
        string game = Read("Uptown/LifeGame.cs"), mix = Read("Uptown/LifeGame.Mix.cs"), director = Read("Uptown/LifeDirector.cs"), guide = Read("Uptown/LifeGuide.cs");
        string wiring = Read("Wiring/Wiring.UptownShopsLinks.cs"), wire = Read("Wiring/Wiring.cs");
        string targets = MemberText(game, "private static void Targets(DistrictFigures f)");
        Check(targets.Contains("Target(DistrictValues.OfficeType, f.EmptyOffices)") && targets.Contains("Target(DistrictValues.RetailType, f.EmptyShops)")
              && targets.Contains("Target(MixRules.HomeType, MixRules.ToLetShare)") && game.Contains("f.EmptyOffices = f.EmptyShops = MixRules.ToLetShare"),
            "uptown mix: the district's own targets keep a third of its offices, shops and homes to let, for the generator and the game's daily swaps");
        Check(MemberText(game, "public static void Unload()").Contains("was.Key.idealAvailableBuildingsInNeighborhood = was.Value"),
            "uptown mix: every type's targets are given back as the game had them when the city goes");
        string firstLife = MemberText(director, "private static bool FirstLife()");
        Check(firstLife.IndexOf("LifeGame.Mix(Standing, Standing, Another, save", StringComparison.Ordinal) > firstLife.IndexOf("Leases(save, out int renamed)", StringComparison.Ordinal)
              && firstLife.Contains("!_state.Mixed"), "uptown mix: the mix pass runs once a save, after the leases");
        string open = MemberText(mix, "private static bool Open(MadeBuilding made, string seed, GameInstance save)");
        string letGo = MemberText(mix, "private static void LetGo(MadeBuilding made, string seed)");
        Check(open.Contains("UnityEngine.Random.InitState(") && open.Contains("UnityEngine.Random.state = was") && open.Contains("MixRules.Kind(Kinds, KindCounts")
              && letGo.Contains("UnityEngine.Random.InitState(") && letGo.Contains("BusinessHelper.SetBuildingForRent(reg)") && letGo.Contains("UnityEngine.Random.state = was"),
            "uptown mix: a business opened or let go by the game's own calls, the engine's chance seeded and put back, the kind its street holds fewest of");
        string lent = MemberText(mix, "private static AiBusinessDefault LentDefault(DistrictShopKind kind, Building record, BuildingSizeInfo size, string lotId, string seed)");
        Check(lent.Contains("string name = LentPrefix + set.LayoutName") && lent.Contains("copy.buildingLayout = name") && lent.Contains("all.Remove(key)")
              && mix.Contains("LentPrefix = \"RealLifeLent\"") && MemberText(mix, "private static bool Shows(BusinessLayoutSet set, string[] goods)").Contains("playerItemPurchaserSettings.itemName"),
            "uptown mix: a shop of the mod's kinds borrows a layout showing one of its primary goods, under a name no AI business of the game's has");
        Check(MemberText(mix, "private static bool Ours(MadeBuilding made, string type, Func<MadeBuilding, bool> skip)").Contains("record.SpecialService == null")
              && MemberText(director, "private static bool Another(MadeBuilding made)").Contains("LeaseOf(made.Spec.Id)"),
            "uptown mix: never a special place, a lease's lot or the player's");
        Check(!mix.Contains("System.Random") && !mix.Contains("Random.Range(") && !Regex.IsMatch(mix, @"void (Late|Fixed)?Update\(") && !mix.Contains("Spec.Id ==")
              && !mix.Contains("\"uptown_"), "uptown mix: no unseeded chance, nothing a frame, and its rules by type and share, never by a lot's id");
        Check(wiring.Contains("DistrictShops.Kinds = ShopKinds") && wiring.Contains("DistrictShops.Kinds = null") && wire.Contains("UptownShopsLinks.Connect") && wire.Contains("UptownShopsLinks.Disconnect"),
            "uptown mix: the mod's kinds of shop reach the pass through the wiring, and are taken off again");
        // The builders' offices: the design studio's layouts filed under the builders' type by the studio's own names were
        // met by the game's generator, which picks a layout where an AI business names it, and found no AI business of the type.
        string leases = Read("Uptown/LifeGame.Leases.cs");
        string borrow = MemberText(leases, "private static string Borrow(string from, string to)");
        Check(borrow.Contains("string name = LentPrefix + set.LayoutName") && borrow.Contains("LayoutName = name") && !borrow.Contains("LayoutName = set.LayoutName")
              && borrow.Contains("LayoutKey(to, set.BuildingSize, set.BuildingVersion, name)")
              && MemberText(leases, "private static void MoveIn(BuildingRegistration reg, DistrictLease lease, int index, AiBusinessDefault from, GameInstance save)").Contains("Lent(from, lease.BusinessType)")
              && MemberText(leases, "private static AiBusinessDefault Lent(AiBusinessDefault from, string type)").Contains("copy.buildingLayout = name")
              && MemberText(leases, "private static void LentLayout(BuildingRegistration reg, Building record)").Contains("reg.Layout = LentPrefix + layout")
              && MemberText(leases, "public static string Lease(MadeBuilding made, DistrictLease lease, int index, GameInstance save, out bool movedIn, out bool renamed)").Contains("LentLayout(reg, record)"),
            "uptown mix: the builders' offices' layouts are lent under names no AI business of the game's has, and an office from before takes the lent name");
        Check(guide.Contains(".Check(\"reallife_uptown_test_mix\", LifeDirector.TestMixCensus, \"Uptown check (mix)\")"), "uptown mix: the census is a test in the run");
    }

    private static void UptownMixTexts()
    {
        string folder = LocalesFolder();
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json")), pt = LoadWords(System.IO.Path.Combine(folder, "pt.json"));
        Dictionary<string, string> zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        string[] keys = { "reallife_uptown_test_mix", "reallife_uptown_test_mix_done" };
        bool all = en != null && pt != null && zh != null;
        foreach (string key in keys)
        {
            all &= en != null && pt != null && zh != null && en.ContainsKey(key) && pt.ContainsKey(key) && zh.ContainsKey(key) && SameHoles(en[key], pt[key], zh[key]) && en[key].Length <= 240;
        }
        Check(all, "uptown mix: its texts in en, pt and zh-cn with the same {holes}, the notice at most 240 characters");
        var langs = new[] { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" };
        string missing = null;
        for (int i = 0; i < langs.Length; i++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, langs[i] + ".json"));
            bool found = words != null && WhatsNewHas(words, UptownMixMark[i]);
            missing = found ? missing : missing ?? langs[i];
        }
        Check(missing == null, "uptown mix: What's new says a third of Uptown is free to rent in all nine languages" + (missing != null ? " (not in " + missing + ")" : string.Empty));
    }

    // A few words of the mix's line in each language's What's new, in the order en, pt, zh-cn, de, fr, es, ru, nl, tr.
    private static readonly string[] UptownMixMark = { "A third of Uptown's shops", "Um terço das lojas", "上城三分之一的商店", "Ein Drittel von Uptowns Läden", "Un tiers des boutiques",
                                                       "Un tercio de las tiendas", "Треть магазинов", "Een derde van de winkels", "üçte biri artık kiralamanız için boş" };
}
