using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using RealLifeOverhaul;
using RealLifeOverhaul.RivalsSeen;

// The rivals' head offices (Scripts/RivalsSeen/HeadOfficeRules.cs, SeenMap, SeenDirector.Head, the greeting's dealings; run with --rivalseen): who ranks above whom, an office
// building of the city for every live rival (one they own where they own one, else one in the district their rank fits), the same whatever order the city's offices come in,
// never one building for two rivals nor one another rival owns, kept once chosen and chosen again only when the building has stopped being an office or its rival is gone,
// the rivals' office in the table found without a business in it, where they are in office hours, the standing a talk tells, the save (round trip, a line from before, a
// newer part, doubles and the cap), the cost of a choice, the pages on both interfaces and the texts.
internal static partial class Program
{
    private static void SeenHeadChecks()
    {
        SeenHeadRank();
        SeenHeadChoose();
        SeenHeadKept();
        SeenHeadTable();
        SeenHeadTalk();
        SeenHeadSave();
        SeenHeadSources();
        SeenHeadTexts();
    }

    // Offices on five streets of four districts: a thousand dollars a square metre for each step of the district's standing, a third of them owned by a rival of the cast.
    private static List<OfficeSite> SeenHeadSites(List<SeenSubject> cast, int count = 120)
    {
        string[] districts = { "ba:neighborhood_midtown", "ba:neighborhood_garment", "ba:neighborhood_murrayhill", "ba:neighborhood_hellskitchen" };
        var sites = new List<OfficeSite>(count);
        for (int i = 0; i < count; i++)
        {
            int d = i % districts.Length;
            string owner = i % 3 == 0 ? cast[3 + (i / 3) % 12].Id : string.Empty;
            sites.Add(new OfficeSite { Key = "Office Row " + (i % 5) + "#" + (100 + i), District = districts[d], Owner = owner, Price = 9000 - d * 2000 + i % 7 });
        }
        return sites;
    }

    private static void SeenHeadRank()
    {
        var cast = SeenCast();
        cast[5].Gone = true;
        var order = new List<int>();
        HeadOfficeRules.RankOrder(cast, order);
        bool tycoonsFirst = order.Take(3).All(i => cast[i].Tycoon) && order.Skip(3).All(i => !cast[i].Tycoon);
        bool byFigure = true;
        for (int i = 1; i < 3; i++)
        {
            byFigure &= cast[order[i - 1]].Figure >= cast[order[i]].Figure;
        }
        for (int i = 4; i < order.Count; i++)
        {
            byFigure &= cast[order[i - 1]].Figure >= cast[order[i]].Figure;
        }
        Check(tycoonsFirst && byFigure && !order.Contains(5) && order.Count == cast.Count - 1, "head offices: the rich list's tycoons rank first by worth, then the game's rival owners by their weekly income, nobody gone among them");
        var tie = new List<SeenSubject> { SeenOwner("b", 100, 100), SeenOwner("a", 100, 100) };
        var tieOrder = new List<int>();
        HeadOfficeRules.RankOrder(tie, tieOrder);
        Check(tieOrder.Count == 2 && tie[tieOrder[0]].Key == "ga", "head offices: a tie in the figure is broken by the rival's key");
    }

    private static void SeenHeadChoose()
    {
        var cast = SeenCast();
        List<OfficeSite> sites = SeenHeadSites(cast);
        var file = new SeenFile { Seed = 4242 };
        Check(HeadOfficeRules.Needs(file, cast), "head offices: with none on record, the city's offices are wanted");
        int made = HeadOfficeRules.Choose(file, cast, sites);
        Check(made == cast.Count && file.Heads.Count == cast.Count && !HeadOfficeRules.Needs(file, cast), "head offices: every live rival is given one (" + made + " of " + cast.Count + ")");
        Check(file.Heads.Select(h => h.Office).Distinct().Count() == file.Heads.Count && file.Heads.Select(h => h.Key).Distinct().Count() == file.Heads.Count, "head offices: no building is two rivals' head office, no rival has two");
        bool real = file.Heads.All(h => sites.Any(s => s.Key == h.Office && s.District == h.District));
        Check(real, "head offices: every head office is an office building of the city, in the district the city has it in");
        bool ownedRight = true, otherOwns = false;
        foreach (SeenHead h in file.Heads)
        {
            SeenSubject s = cast.First(c => c.Key == h.Key);
            OfficeSite site = sites.First(x => x.Key == h.Office);
            bool owns = !string.IsNullOrEmpty(s.Id) && site.Owner == s.Id;
            ownedRight &= h.Owned == owns;
            otherOwns |= !string.IsNullOrEmpty(site.Owner) && site.Owner != s.Id;
            bool hasOne = !string.IsNullOrEmpty(s.Id) && sites.Any(x => x.Owner == s.Id);
            ownedRight &= !hasOne || h.Owned || sites.Where(x => x.Owner == s.Id).All(x => file.Heads.Any(o => o.Office == x.Key && o.Key != s.Key));   // (an owner's own buildings may only be gone to another rival's)
        }
        Check(ownedRight && !otherOwns, "head offices: a rival who owns an office building has one of theirs, and nobody has a building another rival owns");
        int ownedCount = file.Heads.Count(h => h.Owned);
        Check(ownedCount > 6 && ownedCount < cast.Count - 3, "head offices: some rivals have a building of their own and some one fitted to their rank (" + ownedCount + " of " + cast.Count + " own theirs)");
        // The same whatever order the city lists its offices in, and the same again.
        var reversed = new List<OfficeSite>(sites);
        reversed.Reverse();
        var again = new SeenFile { Seed = 4242 };
        HeadOfficeRules.Choose(again, cast, reversed);
        bool same = again.Heads.Count == file.Heads.Count;
        for (int i = 0; same && i < file.Heads.Count; i++)
        {
            SeenHead other = again.Head(file.Heads[i].Key);
            same = other != null && other.Office == file.Heads[i].Office && other.Owned == file.Heads[i].Owned;
        }
        Check(same, "head offices: the same save gives the same offices whatever order the city lists its buildings in");
        var other2 = new SeenFile { Seed = 4243 };
        HeadOfficeRules.Choose(other2, cast, sites);
        Check(other2.Heads.Count(h => file.Head(h.Key).Office != h.Office) > 0, "head offices: another save has other offices");
        // A rival with no building of theirs is put in the district their rank fits: richer rivals in the dearer districts.
        var poor = new List<SeenSubject>(cast);
        foreach (SeenSubject s in poor.Where(x => !x.Tycoon))
        {
            s.Id = "nobody-" + s.Id;   // (owning nothing in the city)
        }
        var fit = new SeenFile { Seed = 9 };
        HeadOfficeRules.Choose(fit, poor, sites.Select(x => { x.Owner = string.Empty; return x; }).ToList());
        var ladder = sites.GroupBy(x => x.District).OrderByDescending(g => g.Average(x => x.Price)).ThenBy(g => g.Key).Select(g => g.Key).ToList();
        var rank = new List<int>();
        HeadOfficeRules.RankOrder(poor, rank);
        int last = -1;
        bool monotone = true;
        foreach (int i in rank)
        {
            int at = ladder.IndexOf(fit.Head(poor[i].Key).District);
            monotone &= at >= last;
            last = Math.Max(last, at);
        }
        int top = ladder.IndexOf(fit.Head(poor[rank[0]].Key).District), bottom = ladder.IndexOf(fit.Head(poor[rank[rank.Count - 1]].Key).District);
        Check(monotone && top == 0 && bottom == ladder.Count - 1, "head offices: with no building of their own, the richest rival is in the dearest district, the poorest in the cheapest, and the rest down the districts in step");
        // Few offices: nobody shares, and whoever finds none goes without.
        var fewFile = new SeenFile { Seed = 3 };
        int few = HeadOfficeRules.Choose(fewFile, cast, SeenHeadSites(cast, 5));
        Check(few == 5 && fewFile.Heads.Select(h => h.Office).Distinct().Count() == 5, "head offices: with five offices in the city, five rivals have one and nobody shares");
        var none = new SeenFile { Seed = 3 };
        Check(HeadOfficeRules.Choose(none, cast, new List<OfficeSite>()) == 0 && HeadOfficeRules.Choose(none, cast, null) == 0 && none.Heads.Count == 0 && HeadOfficeRules.Needs(none, cast),
            "head offices: with no office in the city nobody has one and the choice is asked again");
        var sparse = new List<OfficeSite> { new OfficeSite { Key = string.Empty, District = "x" }, new OfficeSite { Key = "Row#1", District = null, Owner = null, Price = -5 } };
        Check(HeadOfficeRules.Choose(new SeenFile { Seed = 1 }, cast, sparse) == 1, "head offices: a site with no key is passed over and one with no district or price is still an office");
    }

    private static void SeenHeadKept()
    {
        var cast = SeenCast();
        List<OfficeSite> sites = SeenHeadSites(cast);
        var file = new SeenFile { Seed = 77 };
        HeadOfficeRules.Choose(file, cast, sites);
        var before = file.Heads.Select(h => h.Key + "=" + h.Office).ToList();
        Check(HeadOfficeRules.Choose(file, cast, sites) == 0 && file.Heads.Select(h => h.Key + "=" + h.Office).SequenceEqual(before), "head offices: a rival's office is theirs for good, a second choice changes nothing");
        // A rival who comes forward later is given one and nobody else's moves.
        var late = cast.Take(cast.Count - 1).ToList();
        var f2 = new SeenFile { Seed = 77 };
        HeadOfficeRules.Choose(f2, late, sites);
        var snapshot = f2.Heads.Select(h => h.Key + "=" + h.Office).ToList();
        HeadOfficeRules.Choose(f2, cast, sites);
        Check(f2.Heads.Count == cast.Count && snapshot.All(s => f2.Heads.Any(h => h.Key + "=" + h.Office == s)), "head offices: a rival who comes forward later is given an office and nobody's moves");
        // A building that has stopped being an office, or a rival gone, is let go and chosen again.
        cast[7].Gone = true;
        string goneKey = cast[7].Key;
        string gone = file.Heads.First(h => h.Key != goneKey).Office;
        int dropped = HeadOfficeRules.Drop(file, key => key != gone, cast);
        Check(dropped == 2 && file.Head(goneKey) == null && file.HeadAt(gone) == null, "head offices: an office the city no longer has, and the office of a rival gone, are let go");
        cast[7].Gone = false;
        sites.RemoveAll(s => s.Key == gone);
        int again = HeadOfficeRules.Choose(file, cast, sites);
        Check(again == 2 && file.Heads.Count == cast.Count && file.HeadAt(gone) == null, "head offices: a rival who lost theirs is given another, and the old building is not chosen again");
        // Applying the offices makes them the rivals' own premises.
        var s2 = new SeenSubject { Key = "gx", Id = "x", Office = "Old#1", Firm = "Firm" };
        var f3 = new SeenFile();
        f3.Heads.Add(new SeenHead { Key = "gx", Office = "New Row#9", District = "ba:neighborhood_midtown", Owned = true });
        HeadOfficeRules.Apply(f3, new[] { s2 });
        Check(s2.Office == "New Row#9" && s2.Head == "New Row#9" && s2.HeadDistrict == "ba:neighborhood_midtown" && s2.HeadOwned && s2.Firm == "Firm", "head offices: the head office is the rival's office for the day, the firm they run stays as it was");
        var s3 = new SeenSubject { Key = "gy", Office = "Keep#2" };
        HeadOfficeRules.Apply(f3, new[] { s3 });
        Check(s3.Office == "Keep#2" && s3.Head.Length == 0, "head offices: a rival with none is as their source had them");
    }

    private static void SeenHeadTable()
    {
        var table = new SeenTable();
        var s = SeenOwner("a", 1000, 1000);
        s.Head = "Office Row#7";
        s.Office = s.Head;
        var t = SeenTycoon(0, 50000000, 50000000);
        t.Office = "Tycoon Plaza#3";
        table.Begin(new List<SeenSubject> { s, t }, "ba:neighborhood_thehamptons");
        int head = table.OfficeAt("Office Row", 7), plain = table.OfficeAt("Tycoon Plaza", 3);
        Check(head >= 0 && plain >= 0 && table.IsHead(head) && !table.IsHead(plain) && !table.IsHead(-1) && !table.IsHead(99), "head offices: the table knows which of the day's offices is a head office, which stands without a business in it");
        var building = new object();
        table.SetOffice(head, building, 12f, 34f);
        Check(table.Office("Office Row#7", out object found, out float x, out float z) && ReferenceEquals(found, building) && x == 12f && z == 34f && !table.Office("Tycoon Plaza#3", out object _, out float _, out float _),
            "head offices: the building found at a head office is handed to the map and the world, and an office not found is none");
        // Where they are: at the head office in office hours of a working week, never on a Sunday, an hour earlier for one doing well.
        int seed = 5, who = SeenRules.Who("ga");
        int offices = 0, sundays = 0, early = 0, thrivingLeaves = 0;
        for (int day = 1; day <= 56; day++)
        {
            for (int hour = 9; hour < 18; hour++)
            {
                SeenWhere w = SeenRules.Where(seed, who, SeenForm.Steady, 2, false, false, day, hour);
                offices += SeenRules.AtOffice(w) ? 1 : 0;
                SeenWhere hot = SeenRules.Where(seed, who, SeenForm.Thriving, 2, false, false, day, hour, false);
                thrivingLeaves += hour == SeenCatalogue.ThrivingLeaves && SeenRules.AtOffice(hot) ? 1 : 0;
            }
            SeenWhere night = SeenRules.Where(seed, who, SeenForm.Steady, 2, false, false, day, 3);
            sundays += SeenRules.AtOffice(night) ? 1 : 0;
            early += SeenRules.AtOffice(SeenRules.Where(seed, who, SeenForm.Steady, 2, false, false, day, 7)) ? 1 : 0;
        }
        Check(offices > 56 * 5 / 2 && sundays == 0 && early == 0 && thrivingLeaves == 0 && SeenCatalogue.WorkFrom == 9 && SeenCatalogue.WorkTo == 18,
            "head offices: in office hours, on weekdays from " + SeenCatalogue.WorkFrom + ":00 to " + SeenCatalogue.WorkTo + ":00, a rival is at the head office, never at night, never early, and one doing well leaves an hour sooner (" + offices + ")");
        Check(SeenRules.AtOffice(new SeenWhere { Spot = SeenSpot.Office }) && SeenRules.AtOffice(new SeenWhere { Spot = SeenSpot.Late }) && !SeenRules.AtOffice(new SeenWhere { Spot = SeenSpot.Lunch })
              && !SeenRules.AtOffice(new SeenWhere { Spot = SeenSpot.Beach }), "head offices: the office and working late are at the head office, a meal and the beach are not");
    }

    private static void SeenHeadTalk()
    {
        Check(SeenCatalogue.StandingBars.Length == 3 && SeenCatalogue.StandingKeys.Length == 4 && SeenCatalogue.StandingBars.SequenceEqual(SeenCatalogue.StandingBars.OrderBy(b => b))
              && SeenCatalogue.StandingBars[1] == SeenRules.KnowsFrom, "standing: three bars give four steps, the second the social life's bar for a friend");
        Check(SeenRules.Standing(0) == 0 && SeenRules.Standing(99) == 0 && SeenRules.Standing(100) == 1 && SeenRules.Standing(299) == 1 && SeenRules.Standing(300) == 2 && SeenRules.Standing(599) == 2
              && SeenRules.Standing(600) == 3 && SeenRules.Standing(1000) == 3, "standing: how well a rival knows the player in four steps");
        var cast = SeenCast();
        var file = new SeenFile { Seed = 5, Day = 20, Hour = 10 };
        SeenSubject owner = cast[4];
        file.Records.Add(new SeenRecord { Key = owner.Key, Name = owner.Name, MarkDay = 8, Form = SeenForm.Steady, Tier = 2, Car = string.Empty, Known = 310 });
        SeenTalk talk = SeenRules.Hello(file, owner.Key, owner.Name, 20);
        Check(talk != null && talk.Key == owner.Key && talk.Standing == SeenRules.Standing(file.Of(owner.Key).Known) && !talk.Dealings && talk.Head.Length == 0 && talk.Known,
            "head offices: a greeting carries who it was with and how well they know the player, and goes on to dealings only where the director says (at the head office in office hours)");
        string dialog = ModSource(@"RivalsSeen\SeenDialog.cs"), world = ModSource(@"RivalsSeen\SeenDirector.World.cs");
        Check(dialog.Contains("_talk.Dealings") && dialog.Contains("SeenBridges.Offer") && dialog.Contains("SeenBridges.ShowOffer") && dialog.Contains("SeenBridges.Fund") && dialog.Contains("SeenBridges.ShowFund")
              && dialog.Contains("SeenBridges.Move") && dialog.Contains("SeenCatalogue.StandingKeys") && dialog.Contains("Then(() =>"), "head offices: the talk at a head office offers what the mod has between the player and a rival: a business on the market, a fund, a move, how they see the player");
        Check(world.Contains("Head = who.Head.Length > 0 && SeenRules.AtOffice(d.Where)") && world.Contains("talk.Dealings = s.Head && who != null && who.Head.Length > 0") && world.Contains("reallife_seen_spot_head"),
            "head offices: a rival met at their head office in office hours is told so on the game's panel and goes on to dealings");
        string link = ModSource(@"Wiring\Wiring.SeenLinks.cs");
        Check(link.Contains("SeenBridges.Offer = OfferOf") && link.Contains("SaleOf(rival)") && link.Contains("FundFrom(tycoon)") && link.Contains("SeenBridges.Move = MoveOf") && link.Contains("RivalBridges.ShowHead")
              && link.Contains("SeenBridges.ShowOffer = id => Economy.ServicesDirector.Instance?.ShowRival(id)"), "head offices: the talk's offers are read from Services, Schemes and the rivals through the bridges, and shown through their own pages");
    }

    private static void SeenHeadSave()
    {
        var cast = SeenCast();
        var file = new SeenFile { Seed = 12, Day = 9, Hour = 3 };
        HeadOfficeRules.Choose(file, cast, SeenHeadSites(cast));
        file.Heads[0].District = "ba:neighborhood_a b|c;d^e";
        string line = SeenStore.Write(file);
        SeenFile back = SeenStore.Read(line, out int version);
        Check(back != null && back.Heads.Count == file.Heads.Count && SeenStore.Write(back) == line && back.Heads[0].District == "ba:neighborhood_a b|c;d^e" && line.Contains("^o:"),
            "head offices: the offices are saved in a part of their own and read back to the same line, whatever characters a name holds");
        string without = line.Substring(0, line.IndexOf("^o:", StringComparison.Ordinal));
        SeenFile old = SeenStore.Read(without, out int v2);
        Check(old != null && old.Heads.Count == 0 && old.Seed == file.Seed && old.Day == file.Day, "head offices: a line from before them loads as it was, with no offices, and chooses them afresh");
        SeenFile newer = SeenStore.Read(line + "^z:what|ever;more", out int v3);
        Check(newer != null && newer.Heads.Count == file.Heads.Count, "head offices: a part a newer version adds is read past");
        SeenFile twice = SeenStore.Read("v=1^h:1|1|1|1^o:ga|Row#1|d|0;gb|Row#1|d|0;ga|Row#2|d|0;|Row#3|d|0;gc||d|0", out int v4);
        Check(twice != null && twice.Heads.Count == 1 && twice.Heads[0].Key == "ga", "head offices: one office to a rival and one rival to an office, and a record with no rival or no building is dropped");
        var big = new System.Text.StringBuilder("v=1^h:1|1|1|1^o:");
        for (int i = 0; i < SeenCatalogue.HeadsMost + 20; i++)
        {
            big.Append(i > 0 ? ";" : string.Empty).Append('g').Append(i).Append("|Row#").Append(i).Append("|d|0");
        }
        Check(SeenStore.Read(big.ToString(), out int v5).Heads.Count == SeenCatalogue.HeadsMost, "head offices: a line cannot hold more offices than the cap");
        string off = ModSource(@"RivalsSeen\SeenStore.cs");
        Check(off.Contains("case 'o': Heads(f, body); break;") && ModSource(@"RivalsSeen\SeenDirector.cs").Contains("SeenMap.Clear()"), "head offices: the part is read and written by the rivals seen's own store, and the map is cleared when the feature stops");
    }

    private static void SeenHeadSources()
    {
        string row = ModSource(@"RivalsSeen\SeenAppRow.cs");
        Check(row.Contains("\"ba:gameevent_rivalselectedinapp\"") && row.Contains("EventHold.Keep(") && row.Contains("GameContract.RivalsHoodText") && row.Contains("\"reallife_career_button_map\"") && row.Contains("ShowOwnerHead(_forId)")
              && row.Contains("if (_line == null || _button == null)") && !row.Contains("Update()") && ModSource(@"RivalsSeen\SeenDirector.cs").Contains("SeenAppRow.Keep();") && ModSource(@"RivalsSeen\SeenDirector.cs").Contains("SeenAppRow.Stop();")
              && ModSource(@"Core\GameContract.Persona.cs").Contains("\"neighborhoodLocalization\""),
            "head offices: the game's own Rivals app gets a head office line and Show on map on the old interface, copied once beside the game's own district text and Open in Contacts button, written on the game's own event for a rival chosen, never per frame, and the copies go with the feature and with the layout the kit hides");
        string game = ModSource(@"RivalsSeen\SeenGame.cs"), map = ModSource(@"RivalsSeen\SeenMap.cs"), head = ModSource(@"RivalsSeen\SeenDirector.Head.cs"), dir = ModSource(@"RivalsSeen\SeenDirector.cs");
        Check(game.Contains("SpecialBuildings.Holds(c)") && game.Contains("SpecialBuildings.TakenBy(r) != null") && game.Contains("SpecialBuildings.Players(r)") && game.Contains("GoingPublic.Mine(r)") && game.Contains("GameDlc.Open("),
            "head offices: an office the mod keeps, one the player has, one of another player's in a Going Public session and one a DLC keeps are never a rival's head office");
        Check(game.Contains("(!business && !Table.IsHead(office))"), "head offices: a head office is found in the city's table with no business in it, any other office only as before");
        Check(map.Contains("MapPlaces.Register(Id, \"reallife_seen_map_row\"") && map.Contains("MapIcon.Tower") && map.Contains("Available = () => SeenDirector.Instance != null && SeenDirector.Instance.Running")
              && map.Contains("reallife_seen_map_name") && map.Contains("CardTypeKey = \"reallife_seen_map_card\""), "head offices: every rival's head office is a marker of the game's own map with the rival's name, under a row of the map's filters of its own that goes with the feature");
        Check(head.Contains("HeadOfficeRules.Needs(_file, _subjects) && SeenGame.OfficeSites(_sites)") && head.Contains("HeadOfficeRules.Apply(_file, _subjects)") && head.Contains("HeadOfficeRules.Drop(_file, StillOffice, _subjects)")
              && dir.Contains("HeadOffices(day);") && dir.Contains("HeadMarks(day);"), "head offices: the city's offices are read only for a rival with none, on the day's pass, before the table looks for the rivals' offices");
        Check(head.Contains("Careers.DeskGame.Route(") && ModSource(@"Rivals\RivalPage.cs").Contains("\"reallife_career_button_map\"") && ModSource(@"Rivals\RivalPage.cs").Contains("reallife_rival_label_head")
              && ModSource(@"Ui\RivalsViews.cs").Contains("reallife_career_button_map") && ModSource(@"Ui\RivalsViews.cs").Contains("reallife_ui_rivals_head") && ModSource(@"Ui\Money\FinanceLive.Rivals.cs").Contains("FinanceAct.ShowHead")
              && ModSource(@"Ui\Money\FinanceLive.Rivals.cs").Contains("reallife_rival_label_head") && ModSource(@"Ui\Rivals\RivalsScreen.cs").Contains("ShowOwnerHead(_headId)"),
            "head offices: a head office row and Show on map, as the Careers pages' button and its route call set a place, on the old Finance page of a tycoon, the kit's Finance page and the kit's Rivals app");
        Check(ModSource(@"RivalsSeen\SeenGuide.cs").Contains("SeenDirector.Instance?.TestHead()") && ModSource(@"RivalsSeen\SeenGuide.cs").Contains("\"reallife_seen_test_btn_head\", \"reallife_seen_test_btn_back\"") && head.Contains("public void TestHead()"), "head offices: the test is on the options page and in the feature list");
        Check(ModSource(@"RivalsSeen\SeenGuide.cs").Contains("\"seen_head_from\"") && ModSource(@"Ui\UiScreens.Persona.cs").Contains("\"reallife_ui_rivals_head\""), "head offices: the Guide's page has the office hours as facts and the kit lists the new word");
    }

    private static void SeenHeadTexts()
    {
        Dictionary<string, string> en = LocaleFile("en");
        string[] keys =
        {
            "reallife_rival_label_head", "reallife_ui_rivals_head", "reallife_seen_map_row", "reallife_seen_map_name", "reallife_seen_map_card", "reallife_seen_head_gone", "reallife_seen_head_shown",
            "reallife_seen_spot_head", "reallife_seen_spot_head_late", "reallife_seen_btn_dealings", "reallife_seen_btn_deal_owner", "reallife_seen_btn_deal_tycoon", "reallife_seen_btn_standing",
            "reallife_seen_btn_show_sale", "reallife_seen_btn_show_fund", "reallife_seen_dealings_open", "reallife_seen_deal_none", "reallife_seen_deal_sell", "reallife_seen_deal_buy", "reallife_seen_deal_fund",
            "reallife_seen_deal_move", "reallife_seen_deal_none_tycoon", "reallife_seen_standing_0", "reallife_seen_standing_1", "reallife_seen_standing_2", "reallife_seen_standing_3",
            "reallife_seen_test_btn_head", "reallife_seen_test_head", "reallife_seen_test_head_none"
        };
        var lacking = keys.Where(k => !en.ContainsKey(k)).ToList();
        Check(lacking.Count == 0, "head offices: the words are in the English texts" + (lacking.Count > 0 ? " (lacking " + lacking[0] + ")" : string.Empty));
        string all = string.Join("\n", keys.Where(en.ContainsKey).Select(k => en[k]));
        Check(!all.Contains('—') && !all.Contains('–') && !all.Contains(" - ") && !all.Contains('!'), "head offices: the words have no dash and no exclamation mark");
        Check(keys.Where(en.ContainsKey).All(k => en[k].Length <= 240) && en["reallife_seen_dealings_open"].Contains("{address}") && en["reallife_seen_dealings_open"].Contains("{from}") && en["reallife_seen_dealings_open"].Contains("{to}")
              && en["reallife_seen_deal_sell"].Contains("{business}") && en["reallife_seen_deal_sell"].Contains("{amount}") && en["reallife_seen_deal_sell"].Contains("{place}") && en["reallife_seen_deal_sell"].Contains("{day}")
              && en["reallife_seen_deal_buy"].Contains("{other}") && en["reallife_seen_deal_move"].Contains("{line}") && en["reallife_seen_head_shown"].Contains("{name}") && en["reallife_seen_head_shown"].Contains("{address}")
              && en["reallife_seen_map_name"].Contains("{name}") && en["reallife_seen_test_head"].Contains("{count}") && en["reallife_seen_test_head"].Contains("{names}"), "head offices: each text names what its code fills in, and none is over 240 characters");
        string guide;
        Check(en.TryGetValue("reallife_guide_rivals_seen", out guide) && guide.Contains("# Head offices") && guide.Contains("{seen_head_from}") && guide.Contains("<b>Talk business with them</b>") && en["reallife_seen_btn_dealings"] == "Talk business with them"
              && guide.Contains("<b>Rivals' head offices</b>") && en["reallife_seen_map_row"] == "Rivals' head offices" && guide.Contains("<b>Show on map</b>") && en["reallife_career_button_map"] == "Show on map",
            "head offices: the Guide describes the head offices, the map marks and the talk, naming the buttons as they read");
        string docs = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\docs\features\rivals-seen.md"));
        Check(System.IO.File.Exists(docs) && System.IO.File.ReadAllText(docs).Contains("## Head offices"), "head offices: docs/features/rivals-seen.md describes them");
        // The cost of a choice: the whole cast over a city of offices, once.
        var cast = SeenCast();
        List<OfficeSite> sites = SeenHeadSites(cast, 600);
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 20; i++)
        {
            HeadOfficeRules.Choose(new SeenFile { Seed = i }, cast, sites);
        }
        Check(watch.Elapsed.TotalMilliseconds / 20 < 15, "head offices: choosing for the whole cast from six hundred offices takes " + (watch.Elapsed.TotalMilliseconds / 20).ToString("0.00", CultureInfo.InvariantCulture) + " ms, once");
    }
}
