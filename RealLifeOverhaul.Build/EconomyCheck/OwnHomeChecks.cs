using System;
using System.Collections.Generic;
using RealLifeOverhaul.Construction;

// A building the player puts up on their own land stays theirs (Scripts/Construction, Scripts/Uptown). A player built a home
// on their own zone and found it owned by a person of the city's before the ribbon was cut: a block of homes with a flat sold
// off plan, or Sell the flats chosen, was handed to the district as its residents' (the city's) at the keys, and the
// whole-building check then gave that city building one of the city's landlords by name. Now every building of the player's own
// is handed over as theirs, a block on sale until its last flat is sold, its deed letting only the unsold flats meanwhile; then
// it goes to its residents. A block an earlier version gave away so is given back. The city's growth leaves a Let it grow
// building of the player's alone. First the game-free rules, then the sources (the game cannot run here), then the notices'
// texts and What's new.
internal static partial class Program
{
    private static void OwnHomes()
    {
        OwnHomeRules();
        OwnHomeSources();
        OwnHomeTexts();
    }

    // ---------------------------------------------------------------- who a finished building is handed to

    private static void OwnHomeRules()
    {
        Check(LandRules.HandedToPlayer(ProjectKind.Plot, AfterUse.Keep, 0, 0) && LandRules.HandedToPlayer(ProjectKind.Plot, AfterUse.Keep, 12, 0),
            "own home: a building the player keeps is handed over as theirs");
        Check(LandRules.HandedToPlayer(ProjectKind.Plot, AfterUse.SellFlats, 12, 1) && LandRules.HandedToPlayer(ProjectKind.Plot, AfterUse.SellFlats, 12, 0)
              && LandRules.HandedToPlayer(ProjectKind.Plot, AfterUse.SellFlats, 12, 11) && LandRules.HandedToPlayer(ProjectKind.Plot, AfterUse.SellFlats, 0, 0),
            "own home: a block with one flat of twelve sold off plan (the report's case), none sold yet, or one flat left is still the player's at the keys");
        Check(!LandRules.HandedToPlayer(ProjectKind.Plot, AfterUse.SellFlats, 12, 12) && !LandRules.HandedToPlayer(ProjectKind.Plot, AfterUse.SellFlats, 12, 13),
            "own home: a block with every flat sold is its residents'");
        bool onlyPlots = true;
        for (int k = 0; k <= (int)ProjectKind.Parking; k++)
        {
            ProjectKind kind = (ProjectKind)k;
            onlyPlots &= LandRules.HandedToPlayer(kind, AfterUse.Keep, 0, 0) == (kind == ProjectKind.Plot);
        }
        Check(onlyPlots, "own home: of the kinds of project only the player's own new building is handed over as theirs; works won in a tender (a public building) are the city's");
        Check(!LandRules.AllSold(0, 0) && !LandRules.AllSold(12, 11) && LandRules.AllSold(12, 12) && LandRules.AllSold(12, 20) && !LandRules.AllSold(0, 5),
            "own home: a block is all sold only with flats counted and every one of them sold");
        Check(LandRules.UnsoldShare(12, 0) == 100 && LandRules.UnsoldShare(12, 6) == 50 && LandRules.UnsoldShare(12, 12) == 0 && LandRules.UnsoldShare(12, 20) == 0
              && LandRules.UnsoldShare(12, -3) == 100 && LandRules.UnsoldShare(0, 5) == 100 && LandRules.UnsoldShare(3, 1) == 66,
            "own home: the share a block on sale may let is its unsold flats', 0 to 100, all of it with none counted");
        bool falls = true, zeroAtEnd = true;
        for (int flats = 1; flats <= 60; flats++)
        {
            int was = 101;
            for (int sold = 0; sold <= flats; sold++)
            {
                int share = LandRules.UnsoldShare(flats, sold);
                falls &= share <= was && share >= 0 && share <= 100;
                zeroAtEnd &= (share == 0) == LandRules.AllSold(flats, sold);
                was = share;
            }
        }
        Check(falls && zeroAtEnd, "own home: the share let falls with every flat sold, and is nothing exactly when the block is all sold, for blocks of 1 to 60 flats");
    }

    // ---------------------------------------------------------------- the sources

    private static void OwnHomeSources()
    {
        string projects = ModSource(@"Construction\ConstructionDirector.Projects.cs");
        string day = ModSource(@"Construction\ConstructionDirector.Day.cs");
        string grow = ModSource(@"Construction\ConstructionDirector.Grow.cs");
        string asks = ModSource(@"Construction\ConstructionDirector.Asks.cs");
        string game = ModSource(@"Construction\ConstructionGame.Grow.cs");
        string residents = ModSource(@"Uptown\LifeDirector.Residents.cs");
        string buildings = ModSource(@"Uptown\LifeGame.Buildings.cs");
        string complete = ModSource(@"Uptown\LifeGame.Complete.cs");
        string letting = ModSource(@"Uptown\LifeGame.Grow.cs");
        Check(projects.Length > 0 && day.Length > 0 && grow.Length > 0 && asks.Length > 0 && game.Length > 0 && residents.Length > 0 && buildings.Length > 0 && complete.Length > 0
              && letting.Length > 0, "own home sources: the files are there");

        string hand = MethodText(projects, "private void HandToDistrict(ProjectFile p)");
        Check(hand.Contains("bool player = LandRules.HandedToPlayer(p.Kind, p.After, p.Flats, p.FlatsSold);") && !hand.Contains("FlatsOffPlan == 0") && !hand.Contains("AfterUse.Keep")
              && hand.Contains("FlatsHeld(p);"),
            "own home sources: the handover asks the one rule whose building it is, so a block with flats on sale is the player's at the keys, and holds its letting to the unsold flats");
        string held = MethodText(projects, "private void FlatsHeld(ProjectFile p)");
        int refused = held.IndexOf("if (why != null)", StringComparison.Ordinal), removed = held.IndexOf("_state.Lots.Remove(land);", StringComparison.Ordinal);
        Check(held.Contains("LandRules.AllSold(p.Flats, p.FlatsSold)") && held.Contains("ConstructionGame.DeedHeld(site.Street, site.Number, LandRules.UnsoldShare(p.Flats, p.FlatsSold));")
              && held.Contains("string why = ToResidentsLot(p.Lot);") && refused >= 0 && removed > refused && held.Contains("\"reallife_bld_notice_flats_residents\""),
            "own home sources: a block on sale lets only its unsold share, and with its last flat sold the player's land goes only once the district has given it to its residents");
        int lost = held.IndexOf("if (land == null)", StringComparison.Ordinal), back = held.IndexOf("GiveBack(p);", StringComparison.Ordinal);
        Check(lost >= 0 && back > lost && back < refused && ComesBefore(held, "if (!LandRules.AllSold(p.Flats, p.FlatsSold))", "GiveBack(p);"),
            "own home sources: a block an earlier version gave its residents at the keys with flats unsold is given back, and only such a block");
        string giveBack = MethodText(projects, "private void GiveBack(ProjectFile p)");
        int asked = giveBack.IndexOf("string why = GiveBackLot(p.Lot, p.Handed, p.Spent);", StringComparison.Ordinal), added = giveBack.IndexOf("_state.Lots.Add(new LotFile", StringComparison.Ordinal);
        Check(giveBack.Contains("GoingPublic.InSession") && asked >= 0 && added > giveBack.IndexOf("if (why != null)", asked, StringComparison.Ordinal)
              && giveBack.Contains("\"reallife_bld_notice_flats_back\""),
            "own home sources: a block is given back outside a Going Public session, its land the player's again only once the district has given back its record");
        Check(MethodText(projects, "private void FlatsHeldDay()").Contains("FlatsHeld(_state.Projects[i]);")
              && ComesBefore(MethodText(day, "private void DayWork(int day, int before)"), "GrowDeedsDay();", "FlatsHeldDay();")
              && MethodText(day, "private bool FlatSale(ProjectFile p)").Contains("FlatsHeld(p);"),
            "own home sources: the day's pass holds each block after the growth's lettings, and the last flat's sale gives it to its residents at once");
        Check(MethodText(asks, "internal static string ToResidentsLot(string lot)").Contains("ConstructionBridges.ToResidents(lot)")
              && MethodText(game, "internal static bool DeedHeld(string street, int number, int share)").Contains("deed.occupancy = Mathf.Min(deed.occupancy, Mathf.Clamp(share, 0, 100));"),
            "own home sources: the district is asked through its bridge, and the deed's tenants are only ever held down, never raised");
        string growDay = MethodText(grow, "private void GrowDay(int day)");
        Check(ComesBefore(growDay, "if (g.Owner == GrowOwner.Player)", "GrowStep(g, day, false);"),
            "own home sources: the city's growth never steps the player's own Let it grow building through its developer's letting");

        string toResidents = MethodText(residents, "public static string ToResidents(string lotId)");
        Check(toResidents.Contains("if (GoingPublic.InSession)") && ComesBefore(toResidents, "LifeGame.ToResidents(made, SaveGameManager.Current)", "f.Owner = FinishOwner.City;")
              && ComesBefore(toResidents, "f.Owner = FinishOwner.City;", "CompleteOne(made, f, \"its flats sold\");") && toResidents.Contains("WriteState();"),
            "own home sources: a block goes to its residents only outside a Going Public session, its deed first, then its record, and is checked whole after");
        string handBack = MethodText(residents, "public static string GiveBack(string lotId, int handed, double price)");
        Check(ComesBefore(handBack, "if (f.Day < handed || f.Day > handed + GiveBackDays)", "LifeGame.Settle(made, FinishOwner.Player, price, SaveGameManager.Current)")
              && handBack.Contains("if (GoingPublic.InSession)") && ComesBefore(handBack, "LifeGame.Settle(made, FinishOwner.Player, price, SaveGameManager.Current)", "f.Owner = FinishOwner.Player;"),
            "own home sources: only the record of the block's own handover is given back (never a building the city put up there since), its deed written before its record changes");
        string release = MethodText(buildings, "public static string ToResidents(MadeBuilding made, GameInstance save)");
        Check(ComesBefore(release, "if (GoingPublic.Foreign(reg))", "save.realEstate.RemoveAt(i);") && release.Contains("save.buildingsForSale.RemoveAt(i);"),
            "own home sources: the player's deed is given up as the game's own sale gives one up, never another player's");
        Check(MethodText(complete, "public static void MendOwner(MadeBuilding made, List<string> rivals)").Contains("|| Deed(made) != null ||")
              && MethodText(letting, "public static string LetOne(").Contains("reg.BuildingOwnedByPlayer"),
            "own home sources: no landlord is named for a building the player holds, and no tenant put in it");
    }

    // ---------------------------------------------------------------- the notice and What's new

    private static void OwnHomeTexts()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("own home", langs, codes))
        {
            return;
        }
        foreach (string key in new[] { "reallife_bld_notice_flats_residents", "reallife_bld_notice_flats_back" })
        {
            bool notice = langs[0].TryGetValue(key, out string en) && en.Length <= 240 && en.Contains("{name}");
            for (int l = 0; l < codes.Length && l < 3; l++)
            {
                notice &= langs[l].TryGetValue(key, out string value) && value.Contains("{name}") && !value.Contains('!') && !value.Contains((char)0x2014) && !value.Contains((char)0x2013);
            }
            Check(notice, "own home: the notice " + key + " is in en, pt and zh-cn, names the block, in the style, at most 240 characters in English");
        }
        var lines = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["en"] = "Fixed: a building you put up on your own land now stays yours when it is handed over.",
            ["pt"] = "Corrigido: um prédio que você constrói no seu próprio terreno agora continua seu na entrega.",
            ["zh-cn"] = "已修复：你在自己土地上建造的楼宇交付后现在仍归你所有。",
            ["de"] = "Behoben: Ein Gebäude, das du auf deinem eigenen Land baust, bleibt bei der Übergabe jetzt deins.",
            ["fr"] = "Corrigé : un bâtiment que vous construisez sur votre propre terrain reste désormais à vous lors de la remise des clés.",
            ["es"] = "Corregido: un edificio que levantas en tu propio terreno ahora sigue siendo tuyo al entregarse.",
            ["ru"] = "Исправлено: здание, которое вы строите на своей земле, теперь остаётся вашим после сдачи.",
            ["nl"] = "Opgelost: een gebouw dat je op je eigen grond neerzet, blijft nu van jou bij de oplevering.",
            ["tr"] = "Düzeltildi: kendi arazinizde yaptığınız bir bina artık teslim edildiğinde sizin kalıyor."
        };
        var lacking = new List<string>();
        for (int l = 0; l < codes.Length; l++)
        {
            if (!lines.TryGetValue(codes[l], out string wanted) || !WhatsNewHas(langs[l], wanted))
            {
                lacking.Add(codes[l]);
            }
        }
        Check(lacking.Count == 0 && codes.Length == lines.Count,
            "own home: What's new has the fix's line, with its language's fix mark, in some reallife_changes_ row of every language (lacking: " + string.Join(", ", lacking) + ")");
    }
}
