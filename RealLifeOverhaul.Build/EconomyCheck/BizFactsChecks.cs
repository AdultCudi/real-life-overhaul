using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// A business's and a building's facts on every place the kit shows them, as one table: each fact the game's building box (BuildingResume), its BizMan
// (BizManPresentation) or the mod's own earlier additions showed, the condition it showed under, and where it is on the kit now. The kit's screens meet the
// game, so the table is held against the sources: the file and the call that carries each row. The valuation and the market value are the game's own
// (CompetitionHelper.CalculateAiOwnedValuation, Building.GetMarketValue), which the checker cannot run, so their calls are read from the source.
internal static partial class Program
{
    private struct BizFactRow
    {
        public string Fact, Old, Now, File, Token;
    }

    private static BizFactRow BizRow(string fact, string old, string now, string file, string token) => new BizFactRow { Fact = fact, Old = old, Now = now, File = file, Token = token };

    private static BizFactRow[] BizFactTable() => new[]
    {
        // The game's building box: its card at a door, picked in the street and on the map.
        BizRow("address", "BuildingResume, always", "street panel title, map card address row", @"Ui\World\WorldBuilding.cs", "_info.Title = address"),
        BizRow("neighbourhood", "BuildingResume, always", "street panel row, map address row", @"Ui\World\WorldBuilding.cs", "reallife_ui_world_neighbourhood"),
        BizRow("business name", "BuildingResume, with a registration", "door card title, map card name", @"Ui\MapDock\MapLive.Place.cs", "m.Name ="),
        BizRow("business type", "BuildingResume, always", "map Type row, street Kind row", @"Ui\MapDock\MapLive.Place.cs", "reallife_map_row_type"),
        BizRow("traffic index", "BuildingResume, always", "map Business tab, street panel", @"Ui\MapDock\MapLive.Place.cs", "reallife_ui_world_traffic"),
        BizRow("traffic index (door)", "BuildingResume hover half", "door card line", @"Ui\World\WorldBuilding.cs", "BizFactsRules.Pair(UiText.T(\"reallife_ui_world_traffic\")"),
        BizRow("customer capacity", "BuildingResume, when the building has a figure", "map Business tab, door card, street panel", @"Ui\MapDock\MapLive.Place.cs", "building.GetCustomerCapacity >= 0"),
        BizRow("business owner", "BuildingResume, a named business not in a warehouse", "map chip and Owner row, street rows, door card", @"Ui\MapDock\MapLive.Picked.cs", "card.rivalBusinessOwner"),
        BizRow("building owner", "BuildingResume, when a rival, the player or a Hamptons villa owns it", "map Business row, street rows, door card", @"Ui\MapDock\MapLive.Place.cs", "card.rivalBuildingOwner"),
        BizRow("building area", "BuildingResume, always", "map Business tab, door card, street panel, BizMan building facts", @"Ui\MapDock\MapLive.Place.cs", "card.buildingAreaLabel"),
        BizRow("open state and hours logic", "BuildingResume.UpdateOpenStateLabel", "map chip and hours rows, street status, door card state", @"Ui\MapDock\MapLive.Picked.cs", "card.openStateLabel"),
        BizRow("BizMan, set destination, taxi", "BuildingResume buttons", "map and street buttons through the game's own", @"Ui\MapDock\MapLive.Picked.cs", "UiWidgets.Press(card.bizManButton)"),
        // BizMan's presentation.
        BizRow("building type, neighbourhood, total size, traffic, capacity", "BizManPresentation.LoadLeftSide", "BizMan building facts", @"Ui\BizMan\BizManScreen.Parts.cs", "Fact(\"bizman_building_total_size\""),
        BizRow("building area with its size class", "LoadLeftSide BuildingArea", "BizMan building facts, Floor area", @"Ui\BizMan\BizManScreen.Parts.cs", "WordsAt(bottom, \"BuildingArea\")"),
        BizRow("building market value", "LoadLeftSide MarketValue and the offer's valuation", "BizMan fact, or the offer's line when it holds it", @"Ui\BizMan\BizManScreen.Parts.cs", "Words(pres.buildingSideInfoValuation)"),
        BizRow("building price for sale", "forSaleBox BuildingPriceLabel, not a Hamptons house", "BizMan offer line, with the valuation under it", @"Ui\BizMan\BizManScreen.Parts.cs", "WordsAt(box, \"BuildingPriceLabel\")"),
        BizRow("Hamptons house price and valuation", "HamptonsPurchaseBoxUI", "BizMan offer lines", @"Ui\BizMan\BizManScreen.Parts.cs", "GameContract.VillaValuation"),
        BizRow("rival's business valuation", "SetAiOwned, no special service", "BizMan takeover offer line", @"Ui\BizMan\BizManScreen.Parts.cs", "Words(pres.businessSideOfferValuation)"),
        BizRow("rival's logo, type, name, corporation or description, hours", "SetAiOwned", "BizMan rival head and hours", @"Ui\BizMan\BizManScreen.Parts.cs", "pres.businessSideInfoCorporation"),
        BizRow("store inventory and employees", "SetAiOwned buttons", "BizMan buttons", @"Ui\BizMan\BizManScreen.Parts.cs", "BrowseInventory"),
        BizRow("rent, deposit, appliances, first day", "RentBuildingUI", "BizMan lease block", @"Ui\BizMan\BizManScreen.Parts.cs", "GameContract.RentDeposit"),
        // The valuation and the market value on the cards, by the game's own methods.
        BizRow("market value on a card", "BizManPresentation (Building.GetMarketValue)", "map Business row, street building rows, door card", @"Ui\BizMan\BizFacts.cs", "building.GetMarketValue()"),
        BizRow("rival's business valuation on a card", "BizManPresentation (CompetitionHelper)", "same cards, under the business owner", @"Ui\BizMan\BizFacts.cs", "CompetitionHelper.CalculateAiOwnedValuation(reg)"),
        BizRow("the player's own business value", "none in the game; the mod's sale page", "same cards, as Value if sold", @"Ui\BizMan\BizFacts.cs", "sale.Value(reg).Value"),
        BizRow("value beside the owner, street", "owner rows", "building and owners tabs", @"Ui\World\WorldBuilding.cs", "owners.Add(\"bizman_market_value\", worth.Market, 0)"),
        BizRow("value beside the owner, map picked block", "owner chip", "valuation in the owner chip", @"Ui\MapDock\MapLive.Picked.cs", "BizFactsRules.Commas(owner, BizFacts.ValuationSentence(worth))"),
        BizRow("market value beside the building owner, map card", "owner rows", "Business tab, under the owner sentence", @"Ui\MapDock\MapLive.Place.cs", "Label = UiText.T(\"bizman_market_value\"), Value = worth.Market"),
    };

    private static void BizFactsAll()
    {
        // When the game's BizMan sets a rival's business its valuation.
        Check(BizFactsRules.ShowsValuation(false, true, false, false, false), "biz facts: a business that can be taken over, not for rent, not the player's, shows its valuation");
        Check(!BizFactsRules.ShowsValuation(true, true, false, false, false) && !BizFactsRules.ShowsValuation(false, false, false, false, false)
              && !BizFactsRules.ShowsValuation(false, true, true, false, false) && !BizFactsRules.ShowsValuation(false, true, false, true, false)
              && !BizFactsRules.ShowsValuation(false, true, false, false, true),
            "biz facts: no valuation for a special service, a building that cannot be taken over, a warehouse, a place to let or the player's own lease");
        Check(BizFactsRules.IsFigure(0f) && BizFactsRules.IsFigure(2400000f) && !BizFactsRules.IsFigure(-1f) && !BizFactsRules.IsFigure(float.NaN) && !BizFactsRules.IsFigure(float.PositiveInfinity),
            "biz facts: the game's dash for a market value under nothing is no figure, any other is");
        Check(BizFactsRules.IsValuation(1f) && !BizFactsRules.IsValuation(0f) && !BizFactsRules.IsValuation(-5f), "biz facts: a valuation of nothing, the game's answer for what it does not value, is left out");
        Check(BizFactsRules.Lines("a", null, " ", "b") == "a\nb" && BizFactsRules.Lines(null, "") == null && BizFactsRules.LineCount("a\nb\nc") == 3 && BizFactsRules.LineCount(null) == 0,
            "biz facts: a card's lines keep the ones with words, one under another");
        Check(BizFactsRules.Pair("Market value:", "$2.4M") == "Market value: $2.4M" && BizFactsRules.Pair("Market value", null) == null
              && BizFactsRules.Commas("Traffic index: 72", null, "Customer capacity: 48") == "Traffic index: 72, Customer capacity: 48",
            "biz facts: a label and its figure join as the game's rows do, an empty one leaves a gap nowhere");

        // Every row of the table is on the kit.
        var seen = new HashSet<string>();
        foreach (BizFactRow row in BizFactTable())
        {
            Check(seen.Add(row.Fact) && ModSource(row.File).Contains(row.Token), "biz facts: " + row.Fact + " (" + row.Old + ") is on the kit: " + row.Now);
        }

        // The valuation by the game's methods, once for a place, and the cards' reading of it.
        string facts = ModSource(@"Ui\BizMan\BizFacts.cs");
        Check(facts.Contains("ReferenceEquals(reg, _for) && day == _day && state == _state") && facts.Contains("TimeHelper.CurrentDay") && facts.Contains("catch (Exception)"),
            "biz facts: the figures are read once for a place and again when the day turns or the place changes hands, and a building the game cannot value shows none");
        Check(facts.Contains("ShowsValuation(") && facts.Contains("canbetakenover") && facts.Contains("SpecialService != null") && facts.Contains("ServicesGame.IsSellable(reg)"),
            "biz facts: a rival's business is valued where BizMan values it, the player's only where the mod's sale page does");
        string place = ModSource(@"Ui\MapDock\MapLive.Place.cs"), picked = ModSource(@"Ui\MapDock\MapLive.Picked.cs");
        Check(place.Contains("BizFacts.Read(_pickedBuilding)") && place.Contains("BizFacts.ValuationLabel(worth)") && place.Contains("bizman_market_value") && picked.Contains("BizFacts.Read(_pickedBuilding)"),
            "biz facts: the map's place card and its picked place read the figures");
        Check(place.Contains("PlaceChips(card, reg, default)"), "biz facts: the place card says each figure once, in its rows, not in its chips too");
        string world = ModSource(@"Ui\World\WorldBuilding.cs");
        Check(world.Contains("BizFacts.Read(_cbc)") && world.Contains("Valuation(trade, worth)") && world.Contains("Valuation(owners, worth)") && world.Contains("DoorLines("),
            "biz facts: the street's panel, its owners tab and its door card read the figures");

        // BizMan's offer: the owner, the price and the valuation, one a line so none is cut off.
        string parts = ModSource(@"Ui\BizMan\BizManScreen.Parts.cs"), pages = ModSource(@"Ui\BizManPages.cs");
        Check(parts.Contains("Line = Lines(WordsAt(box, \"RivalBuildingOwner\")") && pages.Contains("IndexOf('\\n', start)"),
            "biz facts: the offer for a building gives its owner, its price and its valuation a line each, so the valuation is not cut off the end of the owner's sentence");
        Check(ModSource(@"Ui\UiPromptCard.cs").Contains("MostSubLines = 7"), "biz facts: the door card has room for a building's seven lines");
        Check(parts.Contains("OfferSays(_m.Amount") && parts.Contains("a.Line.Contains(value)") && parts.Contains("?? Words(pres.buildingSideInfoValuation)"),
            "biz facts: the market value fact is left out only where the offer's own lines say it, and a villa on sale shows its valuation under its price");
        Check(parts.Contains("totalSqm == BuildingHelper.GetBuildingSquareMeters(") && parts.Contains("Fact(\"reallife_ui_world_area\", same ? null : area)"),
            "biz facts: the game's total size and floor area are one row while they are the same square metres, two where they differ");

        // The texts, in every language.
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            Check(words != null && words.ContainsKey("reallife_ui_biz_valuation") && words.ContainsKey("reallife_ui_biz_value_sold"), "biz facts: the two labels are in " + code);
        }
        foreach (string code in new[] { "en", "de", "es", "fr", "ko", "nl", "pl", "pt", "ru", "tr", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            string next = WhatsNewText(words, WhatsNewKey(LoadWords(System.IO.Path.Combine(DesignLocales, "en.json")), "A building for sale in BizMan no longer hides its value behind its price."));   // the row that holds it
            Check(words != null && next.Contains("BizMan") && !next.Contains('!') && !next.Contains((char)0x2014)
                  && !next.Contains((char)0x2013) && (code != "en" || next.Contains("A building for sale in BizMan no longer hides its value behind its price.")),
                "biz facts: What's new says so in " + code);
        }
    }
}
