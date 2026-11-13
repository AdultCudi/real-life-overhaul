using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The start page as the game's own carries it, and the lists of BizMan's other tabs that run past their room: the rules of the
// description and the help page, the page laid wide and narrow, Enter and focus as the game's boxes wire them, and the reading of
// each piece from the game's own widgets (read from the sources, as the neighbouring checks do).
internal static partial class Program
{
    private static void StartBizParity()
    {
        // The game's help text of a kind: the paragraphs before its first list, the markdown out, a link as its words.
        string help = "**Gift Shop** businesses operate out of retail buildings.\n\nAll of this store's items can be ordered from [Wholesalers](wholesalers-locations).\n\n"
                      + "The business requires the following furniture to function:\n\n* [Cash Register](furniture-cashregister)\n* At least one product to sell";
        string[] about = BizManStartRules.Describe(help, 3);
        Check(about.Length == 2 && about[0] == "Gift Shop businesses operate out of retail buildings." && about[1] == "All of this store's items can be ordered from Wholesalers.",
            "start biz parity: the description is the help page's paragraphs before its first list, without markdown");
        Check(BizManStartRules.Describe("One.\n\nTwo.\n\nThree.\n\nFour.", 3).Length == 3 && BizManStartRules.Describe(null, 3).Length == 0 && BizManStartRules.Describe("* a\n* b", 3).Length == 0,
            "start biz parity: at most three paragraphs, none from nothing or from a list");
        Check(BizManStartRules.HelpSlug("ba:businesstype_giftshop") == "businesstypes-giftshop" && BizManStartRules.HelpKey("ba:businesstype_giftshop") == "help_ba:businesstype_giftshop_content"
              && BizManStartRules.HelpSlug("ba:itemname_apple") == null && BizManStartRules.HelpSlug(null) == null,
            "start biz parity: a kind's help page is the game's businesstypes slug and its help key");
        Check(BizManStartRules.ShowsStaff(true, "ba:buildingtype_retail") && BizManStartRules.ShowsStaff(false, "ba:buildingtype_office") && BizManStartRules.ShowsStaff(false, "ba:buildingtype_warehouse")
              && !BizManStartRules.ShowsStaff(false, "ba:buildingtype_retail"), "start biz parity: staff are listed for a headquarters, an office or a warehouse, by the game's rule and not its label");
        Check(BizManStartRules.Passage(new[] { "Rented", "You rent this place." }) == "Rented. You rent this place." && BizManStartRules.Passage(new string[0]) == null
              && BizManStartRules.Passage(new[] { "One sentence." }) == "One sentence.", "start biz parity: a panel's title and body read as one passage");
        // Enter does what the game's own box does with it.
        Check(BizManStartRules.Entered(true, BizManPages.FieldName, true) == BizManPages.DoStartHere && BizManStartRules.Entered(true, BizManPages.FieldName, false) < 0
              && BizManStartRules.Entered(true, BizManPages.FieldOffer, false) == BizManPages.DoTakeover && BizManStartRules.Entered(true, BizManPages.FieldBuy, false) == BizManPages.DoBuy
              && BizManStartRules.Entered(true, BizManPages.FieldSale, false) == BizManPages.DoMarkSale && BizManStartRules.Entered(false, BizManPages.FieldName, true) < 0,
            "start biz parity: Enter starts the business from its name, sends an offer from either offer, puts the building up for sale from its price, and does nothing in a search");
        Check(BizManStartRules.TwoColumns(900f, 500f, 400f) && !BizManStartRules.TwoColumns(500f, 900f, 400f) && !BizManStartRules.TwoColumns(900f, 300f, 400f),
            "start biz parity: the building's column needs a wide pane and its rows whole");

        UiWords words = UiWords("en");
        foreach (float aspect in new[] { 16f / 9f, 16f / 10f, 21f / 9f, 4f / 3f })
        {
            UiFrame frame = UiFrame.FullMenu(aspect, "BizMan");
            foreach (IUiKeys keys in new IUiKeys[] { UiDefaultKeys.Keyboard, UiDefaultKeys.XboxPad })
            {
                string where = " at " + aspect.ToString("0.00") + (keys.Pad != UiPad.None ? " with a pad" : "");
                var s = new UiScene();
                BizManModel m = StartParityModel(words);
                BizManPages.Lay(s, m, frame, true, keys);
                Check(s.FindNode(BizManPages.StartHelp, 1) >= 0 && s.FindNode(BizManPages.StartHelp, 2) >= 0, "start biz parity: the guide link and a requirement's help button are targets" + where);
                Check(s.FindNode(BizManPages.Action, BizManPages.DoStartHere) >= 0 && s.FindNode(BizManPages.Type, BizManPages.FieldName) >= 0, "start biz parity: the name box and Start business stand" + where);
                Check(UiFocusRules.Unreached(s, out string missed) == 0, "start biz parity: the d-pad reaches every target of the page" + where + " (" + missed + ")");
                // Every stop of the detail, a heading, a link or a help button, can be brought into view by the pad.
                bool every = true;
                int stops = s.Lists[m.StartList].Count;
                for (int stop = 0; stop < stops; stop++)
                {
                    m.StartOffset = 0f;
                    m.StartReveal = stop;
                    BizManPages.Lay(s, m, frame, true, keys);
                    UiClip c = s.Clips[m.StartClip];
                    every &= c.Offset >= 0f && c.Offset <= Math.Max(0f, c.ContentHeight - c.H) + 0.5f;
                }
                Check(every && stops >= 4, "start biz parity: every heading, link and help button is a stop the pad scrolls to" + where);
                // A page with no kind chosen still offers Start business, and the building where it is wide.
                m = StartParityModel(words);
                m.StartDetail = null;
                BizManPages.Lay(s, m, frame, true, keys);
                Check(s.FindNode(BizManPages.Action, BizManPages.DoStartHere) >= 0, "start biz parity: with no kind chosen the page still offers Start business" + where);
            }
        }
        // The pane is wide: the building's rows stand in a column of their own beside the kind's, not under them.
        {
            var s = new UiScene();
            BizManModel m = StartParityModel(words);
            BizManPages.Lay(s, m, UiFrame.FullMenu(21f / 9f, "BizMan"), true, UiDefaultKeys.Keyboard);
            float deposit = -1f;
            for (int i = 0; i < s.ItemCount; i++)
            {
                if (s.Items[i].Text == "Deposit" && s.Items[i].Clip < 0)
                {
                    deposit = s.Items[i].X;
                }
            }
            UiClip rows = s.Clips[m.StartClip];
            Check(deposit > 0f && deposit >= rows.X + rows.W, "start biz parity: at a wide window the building's lease stands in a column to the right of the kind's rows");
        }
        StartBizScrollLists(words);
        StartBizParitySources();
    }

    private static BizManModel StartParityModel(UiWords words)
    {
        BizManModel m = UiSamples.BizMan(words, "start");
        var rows = new List<BizManStartRow>(m.StartDetail.Rows);
        rows.Insert(0, new BizManStartRow { Kind = BizManStartKind.Heading, Label = "About this business" });
        rows.Insert(1, new BizManStartRow { Kind = BizManStartKind.Body, Label = "Gift Shop businesses operate out of retail buildings. All of this store's items can be ordered from Wholesalers." });
        rows.Insert(2, new BizManStartRow { Kind = BizManStartKind.Link, Label = "Read the full guide page", Arg = 1 });
        rows.Add(new BizManStartRow { Kind = BizManStartKind.Heading, Label = "What it needs" });
        rows.Add(new BizManStartRow { Kind = BizManStartKind.Item, Label = "Cash register", Arg = 2 });
        rows.Add(new BizManStartRow { Kind = BizManStartKind.Item, Label = "Shelf", Arg = 3 });
        m.StartDetail.Rows = rows.ToArray();
        m.StartPhoto = "bizman:photo";
        m.StartPhotoAspect = 1.6f;
        m.StartSide = new[]
        {
            new BizManStartRow { Kind = BizManStartKind.Heading, Label = "The building" },
            new BizManStartRow { Kind = BizManStartKind.Fact, Label = "Building type", Value = "Retail" },
            new BizManStartRow { Kind = BizManStartKind.Fact, Label = "Building area", Value = "120 m2 (Medium)" },
            new BizManStartRow { Kind = BizManStartKind.Fact, Label = "Traffic index", Value = "74" },
            new BizManStartRow { Kind = BizManStartKind.Heading, Label = "The lease" },
            new BizManStartRow { Kind = BizManStartKind.Fact, Label = "Daily rent", Value = "$320" },
            new BizManStartRow { Kind = BizManStartKind.Fact, Label = "Deposit", Value = "$2,240" },
        };
        return m;
    }

    // The contracts, the campaigns and the agencies longer than their room scroll, none dropped, each reachable by the wheel and the pad.
    private static void StartBizScrollLists(UiWords words)
    {
        foreach (float aspect in new[] { 16f / 9f, 4f / 3f })
        {
            UiFrame frame = UiFrame.FullMenu(aspect, "BizMan");
            foreach (IUiKeys keys in new IUiKeys[] { UiDefaultKeys.Keyboard, UiDefaultKeys.XboxPad })
            {
                string where = " at " + aspect.ToString("0.00") + (keys.Pad != UiPad.None ? " with a pad" : "");
                var s = new UiScene();
                BizManModel m = UiSamples.BizMan(words, "store_deliveries");
                m.Items = LongItems(m.Items, 30);
                BizManPages.Lay(s, m, frame, true, keys);
                Check(m.ListClip[0] >= 0 && m.ListList[0] >= 0, "bizman lists: thirty contracts scroll in a region of their own" + where);
                m.ListReveal[0] = 29;
                BizManPages.Lay(s, m, frame, true, keys);
                Check(s.FindNode(BizManPages.Item, 29) >= 0 && m.ListOffset[0] > 0f, "bizman lists: the pad's step to the last contract scrolls it into view" + where);
                m = UiSamples.BizMan(words, "store_deliveries");
                BizManPages.Lay(s, m, frame, true, keys);
                Check(m.ListClip[0] == -1, "bizman lists: a list that fits is laid as it was, with no region" + where);
                Check(UiFocusRules.Unreached(s, out string missed) == 0, "bizman lists: the d-pad reaches every target of deliveries" + where + " (" + missed + ")");

                m = UiSamples.BizMan(words, "store_marketing");
                m.Others = LongItems(m.Others, 24);
                m.Items = LongItems(m.Items, 6);
                var toggles = new List<BizManToggle>();
                for (int g = 0; g < m.Items.Length; g++)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        toggles.Add(new BizManToggle { Words = "Small +12% $50/day", Group = g });
                    }
                }
                m.Toggles = toggles.ToArray();
                BizManPages.Lay(s, m, frame, true, keys);
                Check(m.ListClip[1] >= 0 && m.ListClip[2] >= 0, "bizman lists: campaigns and agencies past their room scroll" + where);
                m.ListReveal[1] = toggles.Count - 1;
                BizManPages.Lay(s, m, frame, true, keys);
                Check(s.FindNode(BizManPages.Toggle, toggles.Count - 1) >= 0, "bizman lists: the pad's step to the last campaign switch scrolls it into view" + where);
                m.ListReveal[2] = 23;
                BizManPages.Lay(s, m, frame, true, keys);
                Check(s.FindNode(BizManPages.ItemLink, 23) >= 0, "bizman lists: the pad's step to the last agency scrolls its Call into view" + where);
            }
        }
    }

    private static BizManItem[] LongItems(BizManItem[] like, int count)
    {
        var all = new BizManItem[count];
        for (int i = 0; i < count; i++)
        {
            all[i] = like.Length > 0 ? like[i % like.Length] : new BizManItem { Name = "Item" };
            all[i].Name = (all[i].Name ?? "Item") + " " + (i + 1);
            all[i].Link = like.Length > 0 && !string.IsNullOrEmpty(like[i % like.Length].Link) ? like[i % like.Length].Link : "Call";
        }
        return all;
    }

    private static void StartBizParitySources()
    {
        string screen = ModSource(@"Ui\BizMan\BizManScreen.cs");
        string read = ModSource(@"Ui\BizMan\BizManScreen.Start.cs");
        string page = ModSource(@"Ui\BizMan\BizManScreen.Page.cs");
        string parts = ModSource(@"Ui\BizMan\BizManScreen.Parts.cs");
        string press = ModSource(@"Ui\BizMan\BizManScreen.Press.cs");
        string host = ModSource(@"Ui\UiHost.cs");
        string plant = ModSource(@"Ui\BizMan\Ops\BizManScreen.Plant.cs");
        // Enter, the game's own length limit and the focus on opening.
        Check(screen.Contains("IUiTypingSubmit") && screen.Contains("IUiTypingQuiet") && screen.Contains("IUiTypingFocus") && screen.Contains("public int TypingLimit")
              && screen.Contains("f.characterLimit > 0 ? f.characterLimit") && screen.Contains("BizManStartRules.Entered(") && screen.Contains("Press(BizManPages.Action, what);"),
            "start biz parity: the screen hears Enter, takes the game's character limit and presses the game's own button for it");
        Check(host.Contains("_typeFocus.TakeFocus()") && host.Contains("screen as IUiTypingFocus") && read.Contains("_focusName = true;"), "start biz parity: the name box takes the keys as the page opens, as the game's own is focused");
        Check(read.Contains("BizManStartRules.ShowsStaff(bt.IsHeadquarters, bt.suitableBuildingType)") && !read.Contains("title == UiText.T(\"myemployees_title\")"),
            "start biz parity: staff or goods by the game's rule, not by comparing a label's words");
        Check(read.Contains("BizManStartRules.HelpKey(bt.businessTypeName)") && read.Contains("BizManStartKind.Link") && read.Contains("r.GetHelpLink(b.buildingRegistration)")
              && read.Contains("CinemaScreenLicensingFee") && read.Contains("TheaterStageLicensingFee") && read.Contains("reg.RentPerDay") && read.Contains("reg.lastDeposit") && read.Contains("\"BuildingArea\"")
              && read.Contains("GameContract.ShowPhoto"), "start biz parity: the kind's description and guide link, requirement help, licence fees, rent and deposit, size class and photo are read from the game");
        Check(read.Contains("bool rivals = false;") && read.Contains("rivals ? new[]"), "start biz parity: the competitors column stands only where the game shows its count");
        Check(read.Contains("reallife_ui_bizman_starting") && page.Contains("the game named the business"), "start biz parity: Start business waits while the game makes the logo");
        Check(page.Contains("HelpIconButton help = c.GetComponentInChildren<HelpIconButton>(true)") && press.Contains("UiFirmHead.First + UiFirmHead.Help") && read.Contains("help.Toggle(show: true"),
            "start biz parity: each requirement of the head with a help page carries the game's \"?\", which opens its help");
        Check(parts.Contains("BizManStartRules.Passage(SaidParts)") && parts.Contains("\"BuildingDescription\"") && parts.Contains("GameContract.PlacePhoto") && parts.Contains("inventory_pricing_covered_by_pricing_manager")
              && parts.Contains("\"LastPeriod\"") && parts.Contains("\"AmountLabel\"") && parts.Contains("ShelfTemplate") && parts.Contains("reallife_ui_bizman_capacity_open")
              && parts.Contains("bizman_settings_terminate_contract") && parts.Contains("bizman_shutdown_business_description"),
            "start biz parity: the rented place's whole words, the building's description, the estate photo, the manager's prices, last period, amounts sold, shelves, the open capacity and the settings' sentences are read from the game");
        Check(plant.Contains("common_required_production_machines") && plant.Contains("broken.HasProductionMachine(needed[i])"), "start biz parity: a machine missing its production machines lists them, each placed or missing");
        Check(Array.IndexOf(BizManPages.Commands, BizManPages.StartHelp) >= 0 && Array.IndexOf(BizManPages.Commands, UiFirmHead.First + UiFirmHead.Help) >= 0
              && BizManPages.StartHelp >= 9800 && BizManPages.StartHelp < 9820, "start biz parity: the help commands stand in the page's known commands and the job's block");
    }
}
