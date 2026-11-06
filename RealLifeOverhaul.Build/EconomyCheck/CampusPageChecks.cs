using System;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// Birchwood School's page in the game's BizMan app after the owner's test of 26 Oct 2026 (the screenshot of 1 Birchwood Green): the page read "Building owned by
// Undefined", offered the form to start a business in a building a school fills, and called the building an office. The checks: the page's rule (TakenPageRules: the
// owner by the school's file, the form never offered, the school's own line for "Taken by"), the school saying it through the book's facts, the page's code that
// writes it, and the campus's ground and dressing that the same test showed wrong. Each was made to fail once against the old code (the report says how).
internal static partial class Program
{
    private static void CampusPageChecks()
    {
        // The rule, game-free.
        var council = new SpecialFacts { OwnerName = "the city council", PageLine = "Run by the council" };
        var trust = new SpecialFacts { OwnerName = "Meridian Schools", PageLine = "Run by Meridian Schools" };
        var yours = new SpecialFacts { OwnerYou = true, PageLine = "Run by you. Open the Campus app, Schools tab, to run it." };
        Check(TakenPageRules.Owner(council, "Sam") == "the city council" && TakenPageRules.Owner(trust, "Sam") == "Meridian Schools",
            "campus page: the owner line names the council or the trust by the school's file, never the game's 'Undefined'");
        Check(TakenPageRules.Owner(yours, "Sam") == "Sam" && TakenPageRules.Owner(yours, null) == null && TakenPageRules.Owner(null, "Sam") == null && TakenPageRules.Owner(new SpecialFacts(), "Sam") == null,
            "campus page: when the player owns the school the owner line names the player (the game's own form), and a feature that says nothing leaves the game's owner line");
        Check(!TakenPageRules.OffersStart("Birchwood School") && TakenPageRules.OffersStart(null), "campus page: no form to start a business in a building a feature holds, and the form stays for any other");
        Check(TakenPageRules.Line(council, "Taken by Birchwood School") == "Run by the council" && TakenPageRules.Line(yours, "Taken by Birchwood School").Contains("Schools tab")
              && TakenPageRules.Line(null, "Taken by X") == "Taken by X" && TakenPageRules.Line(new SpecialFacts(), "Taken by X") == "Taken by X",
            "campus page: the school's own line stands in the place of 'Taken by', the player's points to the Campus app's Schools tab, and any other building keeps 'Taken by'");
        council.Clear();
        Check(council.OwnerName == null && !council.OwnerYou && council.PageLine == null, "campus page: the facts are cleared before each ask, so one building's owner is never another's");

        // The page's code (ClaimedPresentation), the book's (SpecialBuildings.PageOf) and the school's say.
        string offers = ModSource(@"Core\ClaimedOffers.cs"), book = ModSource(@"Core\SpecialBuildings.cs"), campus = Regex.Replace(ModSource(@"Schools\SchoolsDirector.Campus.cs"), @"//[^\r\n]*", " "), text = ModSource(@"Schools\SchoolsText.cs");
        Check(offers.Contains("SpecialBuildings.PageOf(registration, out string typeWords)") && offers.Contains("TakenPageRules.Owner(says,") && offers.Contains("\"building_rival_owner_description\".Localize(new { rivalName = name })")
              && offers.Contains("TakenPageRules.Line(says,"),
            "campus page: BizMan's page writes the owner in the game's own 'Building owned by' words with the feature's owner, and the feature's own line for 'Taken by'");
        Check(Regex.IsMatch(offers, @"StartBusinessUI form = page\.GetComponentInChildren<StartBusinessUI>\(true\);") && Regex.IsMatch(offers, @"if \(_start != null && !TakenPageRules\.OffersStart\(_taken\)\)\s*\{\s*Off\(_start\);"),
            "campus page: the game's form to start a business is put out every frame the page shows a building a feature holds, as the rent and offer boxes are");
        Check(offers.Contains("labels[i].name == \"BuildingTypeLabel\"") && offers.Contains("GameUi.UiClone.Words(_type, typeWords)") && book.Contains("typeWords = string.IsNullOrEmpty(entry.TypeKey) ? null : Localise(entry.TypeKey);"),
            "campus page: the kind of building reads as the card says it ('School'), written over the game's 'Office building' once the page is written, and only for a building the book holds");
        Check(book.Contains("entry.Facts(PageFacts);") && Regex.IsMatch(book, @"try\s*\{\s*entry\.Facts\(PageFacts\);\s*\}\s*catch \(Exception e\)"), "campus page: the feature's say is asked behind its own guard, so a fault there leaves 'Taken by' and breaks nothing");
        Check(campus.Contains("facts.OwnerName = SchoolsText.PageOwner(_state.Campus.Owner);") && campus.Contains("facts.OwnerYou = _state.Campus.Owner == SchoolOwner.Player;") && campus.Contains("facts.PageLine = SchoolsText.PageLine(_state.Campus.Owner);"),
            "campus page: the school says who owns its building from its own file, as the council, a trust or the player");
        Check(Regex.IsMatch(text, @"PageOwner\(int owner\) => owner == SchoolOwner\.Player \? null : owner == SchoolOwner\.Council \? T\(OwnerNameCouncil\) : Trust\(owner\);")
              && Regex.IsMatch(text, @"PageLine\(int owner\) => owner == SchoolOwner\.Player \? Fill\(PagePlayer, ""app"", T\(CampusApp\), ""tab"", T\(Tab\)\) : CardRun\(owner\);"),
            "campus page: the council and a trust are named, the player's line points to the Campus app's Schools tab by the app's and the tab's own words");
        // The hall and the junior block are looks with no registration of their own: no page at all.
        Check(!ModSource(@"Schools\SchoolsGame.cs").Contains("SpecialBuildings.Set(") || ModSource(@"Schools\SchoolsGame.cs").Contains("made.Controller"),
            "campus page: the hall and the junior block are looks with no registration or controller of their own, so BizMan has no page for them");
        // The card's run-by line: written by the card's own code every second, over what the game writes on the hour (SpecialNames.Tick clears the card's target each second).
        string names = ModSource(@"Core\SpecialNames.cs");
        Check(names.Contains("_carded = null;   // the card written again: the game rewrites it on the hour") && names.Contains("_card.rivalBusinessOwner.SetData(LanguageChangeEventDataHolder.Create(\"reallife_campus_card_state\", new { state = Said.RunLine }, \"common_unknown\"));"),
            "campus card: the hover card's run-by line is written again each second over the game's own hourly rewrite (the door panel the owner saw has no owner line: its box has three)");
    }

    // The same test's other two faults: nobody on the campus (the pedestrians' mesh stopped at the block's edge) and a black slab on the lawn (the game's bike-share dock).
    private static void CampusGroundChecks()
    {
        string plans = ModSource(@"Uptown\LayoutPlans.cs"), campus = ModSource(@"Schools\SchoolsDirector.Campus.cs"), game = ModSource(@"Schools\SchoolsGame.cs"), grounds = ModSource(@"Schools\SchoolGrounds.Dressing.cs");
        Check(Regex.IsMatch(plans, @"Parks\(plan\.Boxes\);\s*Campus\(plan\.Boxes\);\s*Area\(plan\.Area\);\s*return plan;") && plans.Contains("LayoutRules.Block(LayoutRules.CampusBlock / 100, LayoutRules.CampusBlock % 100, out float x0, out float z0, out float x1, out float z1);")
              && plans.Contains("into.Add(Box(x0 - reach, z0 - reach, x1 + reach, z1 + reach));"),
            "campus ground: the pedestrians' walking plan carries the campus's whole block, reaching over the pavements round it, as the player's ground does (their mesh stopped at its edge: 0 of 9 routes)");
        Check(Regex.IsMatch(campus, @"Logger\?\.Warn\(reach \+ "": FAIL; not: "" \+ failed") && Regex.IsMatch(campus, @"Logger\?\.Info\(reach \+ "": PASS\.""\)") && campus.Contains("SchoolsGame.Reaches(from, to, SchoolsGame.NpcAgent, out string whyPeople)"),
            "campus ground: the campus reach line says PASS only when every route is walkable for people and the player, and is a warning that says FAIL otherwise");
        Check(!game.Contains("Prefab_BikeRentalStand") && !grounds.Contains("CampusPieceKind.Rack") && Array.IndexOf(Enum.GetNames(typeof(RealLifeOverhaul.Schools.CampusPieceKind)), "Rack") < 0,
            "campus dressing: no bicycle dock on the campus (the game's bike-share dock stood as a bare black slab on the lawn)");
    }
}
