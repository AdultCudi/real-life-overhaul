using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    private static bool _bizManChecked;

    // BizMan, every state of it: the portfolio and each section of a place's page laid out by the screen checks in every
    // language and frame, with the keys, a pad and the mouse leading; its rules checked once beside them.
    static partial void UiCasesBizMan(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id != "bizman")
        {
            return;
        }
        foreach (string state in UiSamples.BizManStates)
        {
            string st = state;
            cases.Add(new UiScreenCase
            {
                Def = def, Name = "bizman (" + st + ")", Title = "BizMan", Commands = BizManPages.Commands,
                Lay = (s, f, dark, k, t) => BizManPages.Lay(s, UiSamples.BizMan(t, st), f, dark, k)
            });
        }
        if (!_bizManChecked)
        {
            _bizManChecked = true;
            BizManRuleChecks();
        }
    }

    private static void BizManRuleChecks()
    {
        // The kit draws the everyday tabs and the mod's own Interior tab, and the operations while their own switch is on;
        // another mod's tab, and the operations with their switch off, open the game's own view.
        Check(BizManRules.Drawn("Presentation", false) && BizManRules.Drawn("Insight", false) && BizManRules.Drawn("InventoryPricing", false) && BizManRules.Drawn("Deliveries", false)
              && BizManRules.Drawn("Marketing", false) && BizManRules.Drawn("Settings", false) && BizManRules.Drawn("RealEstate", false) && BizManRules.Drawn(BizManRules.Interiors, false),
            "bizman: the kit draws the presentation, insight, prices, deliveries, marketing, settings, real estate and the mod's Interior tab");
        Check(BizManRules.Drawn("Schedule", true) && BizManRules.Drawn("PricingManagers", true) && BizManRules.Drawn("LogisticsManagers", true) && BizManRules.Drawn("PurchasingAgents", true)
              && BizManRules.Drawn("HrManagers", true) && BizManRules.Drawn("Headhunters", true) && BizManRules.Drawn("Factory", true) && BizManRules.Drawn("Drivers", true)
              && BizManRules.Drawn("Inventory", true) && !BizManRules.Drawn("BizSuite_Tab", true),
            "bizman: the schedule, the managers, the factory and the warehouse's tabs are drawn while their switch is on; another mod's opens the game's own view");
        Check(!BizManRules.Drawn("Schedule", false) && !BizManRules.Drawn("PricingManagers", false) && !BizManRules.Drawn("Headhunters", false) && !BizManRules.Drawn("Factory", false)
              && !BizManRules.Drawn("Drivers", false) && !BizManRules.Drawn("Inventory", false) && BizManRules.PartOf("Schedule", false) == BizManPart.Classic,
            "bizman: with the operations' switch off, the schedule, the managers, the factory and the warehouse's tabs open the game's own view, with a handover");
        // Another mod's child of the tab bar is told from the game's and the mod's own.
        Check(UiHostRules.Own("Insight", BizManRules.GameTabs) && UiHostRules.Own(BizManRules.Interiors, BizManRules.GameTabs)
              && !UiHostRules.Own("EmpireMotorsManagementButton", BizManRules.GameTabs) && !UiHostRules.Own("MeaningfulAmbience", BizManRules.GameTabs),
            "bizman: another mod's tab in BizMan is told from the game's and the mod's own");
        // One glyph a role, each tab's and each kind's set.
        var seen = new HashSet<UiIcon>();
        bool unique = true;
        foreach (string tab in BizManRules.GameTabs)
        {
            unique &= seen.Add(BizManRules.TabIcon(tab));
        }
        Check(unique && BizManRules.TabIcon("SomeModTab") == UiIcon.Code && BizManRules.TabIcon(BizManRules.Interiors) == UiIcon.Sofa,
            "bizman: every tab has a glyph of its own, another mod's the other mods' glyph");
        Check(BizManRules.KindIcon("ba:businesstype_coffeeshop", null) == UiIcon.Coffee && BizManRules.KindIcon("ba:businesstype_fastfoodrestaurant", null) == UiIcon.ForkKnife
              && BizManRules.KindIcon("ba:businesstype_warehouse", null) == UiIcon.Package
              && BizManRules.KindIcon("ba:businesstype_empty", "ba:buildingtype_residential") == UiIcon.Home && BizManRules.KindIcon("ba:businesstype_somethingnew", null) == UiIcon.Store
              && BizManRules.KindIcon(null, null) == UiIcon.MapPin && BizManRules.KindIcon("ba:businesstype_headquarters", null) != UiIcon.Building,
            "bizman: each kind of business wears its own glyph, a kind the game adds later the store's");
        // An amount steps by about a hundredth of itself, on a round figure, never below its least and never below nothing.
        Check(BizManRules.Step(245000, 1) == 2000 && BizManRules.Step(3.40, 0.05) == 0.05 && BizManRules.Step(8400000, 1) == 50000 && BizManRules.Step(0, 1) == 1
              && System.Math.Abs(BizManRules.Stepped(245000, 1, 1) - 246000) < 0.01 && System.Math.Abs(BizManRules.Stepped(3.40, -1, 0.05) - 3.35) < 0.001
              && BizManRules.Stepped(0.5, -1, 1) == 0,
            "bizman: an offer steps by a round hundredth of itself, a price by its least, and nothing goes below nothing");
        // A table of amounts is stepped through by its figures, never by its rows.
        var s = new UiScene();
        BizManPages.Lay(s, UiSamples.BizMan(UiWords("en"), "store_pricing"), UiFrame.FullMenu(16f / 9f, "BizMan"), true, UiDefaultKeys.Keyboard);
        Check(s.FindNode(BizManPages.Goods + UiDataTable.Row, 0) < 0 && s.FindNode(BizManPages.Goods + UiDataTable.Type, 0) >= 0
              && s.FindNode(BizManPages.Goods + UiDataTable.More, 0) >= 0,
            "bizman: the prices' table is stepped by its figures, each row's minus, figure and plus a target, the row itself none");
        s = new UiScene();
        BizManPages.Lay(s, UiSamples.BizMan(UiWords("en"), "portfolio"), UiFrame.FullMenu(16f / 9f, "BizMan"), true, UiDefaultKeys.Keyboard);
        Check(s.FindNode(UiDataTable.First + UiDataTable.Row, 9) >= 0 && s.FindNode(UiDataTable.First + UiDataTable.Filter, 3) >= 0 && s.FindNode(UiDataTable.First + UiDataTable.Search, 0) >= 0,
            "bizman: the portfolio shows all ten businesses with the game's four filters and its search");
    }
}
