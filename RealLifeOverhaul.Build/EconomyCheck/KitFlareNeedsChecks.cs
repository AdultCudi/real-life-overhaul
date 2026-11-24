using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Ui;

// Two reports on the RLO interface (21 Nov 2026), mended together:
//  1. Flare's business page showed "None" for a business whose reviews were listed under it, and an address as "32 ba:street_fifthavenue".
//     The city keeps a business's stars by the population's key ("street|number", the street unescaped) and the page looked them up by the place as Flare writes it
//     ("street~number", the street escaped), which never matched a street id holding a colon; and the address was written from the key's raw parts.
//  2. The needs' bars on the bar were too small to read: a size option (0 to 4) and a place option (in the bar, or a row of their own along the top).
// Game-free parts are run; the screens' sources are read, as the checker cannot run the game's UI.
internal static partial class Program
{
    private static void KitFlareNeeds()
    {
        KitFlareRating();
        KitRawKeys();
        KitNeedsBars();
    }

    private static void KitFlareRating()
    {
        string rating = ModSource(@"Social\Flare\FlareDirector.Rating.cs");
        string desk = ModSource(@"Ui\Flare\FlareScreen.Desk.cs");
        string fill = ModSource(@"Ui\Flare\FlareScreen.Fill.cs");
        string sheet = ModSource(@"Ui\Flare\FlareScreen.Sheet.cs");
        // The rows are kept by the key; the place is turned into it, whatever the street's id holds.
        string cityStars = MemberText(rating, "public FlareStarRow CityStarsOf(");
        Check(cityStars.Contains("FlareCityPass.PlaceToKey(place)") && cityStars.Contains("FlareUseRules.StarsOf(_city.StarRows, key)") && !cityStars.Contains("Replace('~'"),
            "kit flare: a business's city stars are found by the population's key, the street unescaped, and not by the place's own text");
        var rows = new List<FlareStarRow> { new FlareStarRow { Key = "ba:street_fifthavenue|32", Five = 2, Four = 1 } };
        Check(FlareUseRules.StarsOf(rows, "ba:street_fifthavenue|32") != null && FlareUseRules.StarsOf(rows, "ba%3Astreet_fifthavenue~32") == null
              && FlareUseRules.StarsOf(rows, "ba:street_fifthavenue~32") == null,
            "kit flare: the place as Flare writes it is no key of the star rows (the fault: the street's id holds a colon, which the place escapes)");
        // One read for every page: the city's row (by the key) with the lasting review book, the old card reading it as the kit's pages do.
        string use = ModSource(@"Social\Flare\FlareDirector.Use.cs");
        Check(MemberText(use, "public FlareStarRow StarsFor(").Contains("return CityStarsOf(place);") && MemberText(use, "public FlareStarRow StarsWithPosts(").Contains("StarsFor(place)")
              && desk.Contains("_flare.StarsWithPosts(shop.Place, now)") && fill.Contains("_flare.StarsWithPosts(shops[i].Place, _flare.Now)")
              && ModSource(@"Social\Flare\FlareApp.Business.cs").Contains("_director.StarsWithPosts(row.Key, now)") && ModSource(@"Social\Flare\FlareApp.City.cs").Contains("_director.StarsWithPosts(row.Key, _director.Now)"),
            "kit flare: the kit's pages and the old page's card read one rating, the city's row by its key and the lasting review book together");
        Check(sheet.Contains("_flare.StarsWithPosts(shop.Place, _flare.Now)") && !sheet.Contains("_flare.StarsFor("),
            "kit flare: the reply sheet reads the rating the page shows, by the same key");
        // What the old page draws its stars from.
        string game = MemberText(rating, "public bool GameSatisfaction(");
        Check(game.Contains("FlareGame.Satisfaction(FlareGame.Business(place), out overall") && desk.Contains("_flare.GameSatisfaction(shop.Place, out int overall)")
              && desk.Contains("reallife_flare_kit_rating_game") && ModSource(@"Social\Flare\FlareApp.Business.cs").Contains("FlareGame.Satisfaction(shops[i], out overall, parts)"),
            "kit flare: with no review counted yet the page shows the game's own satisfaction as the stars, as the old page's business card does, and beside the reviews' rating when there are some");
    }

    private static void KitRawKeys()
    {
        // The address: the game's own text, never the key's raw parts.
        string deskSource = ModSource(@"Ui\Flare\FlareScreen.Desk.cs");
        string line = MemberText(deskSource, "private string BusinessLine(");
        string flareGame = ModSource(@"Social\Flare\FlareGame.cs");
        string address = MemberText(flareGame, "public static string AddressOf(");
        Check(line.Contains("FlareGame.AddressOf(shop.Place)") && !line.Contains("Substring(0, cut)") && address.Contains("RealLifeOverhaul.Construction.ConstructionGame.AddressText("),
            "raw keys: a business's address on Flare's page is written by the mod's address helper, the street's words and not its id");
        // The other places that wrote a street and a number by hand where a player reads them.
        var sources = new Dictionary<string, string>
        {
            { @"Health\HealthDoctor.cs", "place = RealLifeOverhaul.Construction.ConstructionGame.AddressText(a.Street, a.Number)" },
            { @"Health\HealthDirector.Facts.cs", "RealLifeOverhaul.Construction.ConstructionGame.AddressText(a.Street, a.Number)" },
            { @"Ui\Ventures\HealthLive.cs", "RealLifeOverhaul.Construction.ConstructionGame.AddressText(a.Street, a.Number)" },
            { @"Ui\Activity\ActivityFacts.cs", "PostPlace(j.Post.Street, j.Post.Number)" },
            { @"Tech\TechDirector.BakeOff.cs", "RealLifeOverhaul.Construction.ConstructionGame.AddressText(reg.StreetName, reg.StreetNumber)" }
        };
        foreach (KeyValuePair<string, string> one in sources)
        {
            string code = ModSource(one.Key);
            Check(code.Contains(one.Value) && !code.Contains("a.Number + \" \" + a.Street") && !code.Contains("reg.StreetName + \" \" + reg.StreetNumber"),
                "raw keys: " + one.Key + " names an address through the mod's helper");
        }
        // The guard: a raw key among other words is found and put into words.
        Check(MapText.HasRawKey("32 ba:street_fifthavenue") && MapText.HasRawKey("Food in Garment District · 32 ba:street_fifthavenue · open") && MapText.HasRawKey("(reallife:street_uptown_second)")
              && !MapText.HasRawKey("32 Fifth Avenue") && !MapText.HasRawKey("Alba: Hudson Yards") && !MapText.HasRawKey("Open 9:00 to 17:30") && !MapText.HasRawKey("Elva's_shop 12") && !MapText.HasRawKey(null)
              && !MapText.HasRawKey("the ba: of it"),
            "raw keys: a key of the game's or the mod's inside a longer text is found, and ordinary words, times and a colon after a word are not");
        string said = MapText.Rekey("32 ba:street_fifthavenue, open", key => key == "ba:street_fifthavenue" ? "Fifth Avenue" : null);
        Check(said == "32 Fifth Avenue, open" && MapText.Rekey("32 ba:street_fifthavenue", key => null) == "32 ba:street_fifthavenue" && MapText.Rekey("plain words", key => "x") == "plain words",
            "raw keys: each raw key is replaced by its words, one left as it is where there are none, and a text with none is as it was");
        string widgets = ModSource(@"Ui\Host\UiWidgets.cs");
        string worded = MemberText(widgets, "private static string Worded(");
        Check(worded.Contains("MapText.HasRawKey(words) ? MapText.Rekey(words, KeyWords) : words") && widgets.Contains("StreetNameRules.HasId(key)")
              && widgets.Contains("ConstructionGame.StreetWords(key)"),
            "raw keys: a game label that holds a key among other words is put into words, a street's by the way the mod's addresses name one");
        string rules = ModSource(@"Ui\UiCheckRules.cs");
        Check(MemberText(rules, "public static int RawKeys(").Contains("MapText.HasRawKey(it.Text)"),
            "raw keys: the layout checks of every kit sample refuse a text holding 'ba:' or 'reallife:' and an identifier");
        // Nothing of the old pages or the kit builds an address from a street id and a number for a player to read.
        foreach (string file in new[] { @"Social\Flare\FlareApp.Business.cs", @"Social\Flare\FlareApp.City.cs", @"Ui\Flare\FlareScreen.Desk.cs", @"Ui\Flare\FlareScreen.Fill.cs", @"Ui\Flare\FlareScreen.Sheet.cs" })
        {
            string code = ModSource(file);
            Check(!code.Contains("key.Substring(0, cut)") && !code.Contains("Street + \" \" + "), "raw keys: " + file + " writes no address from a key's raw parts");
        }
    }

    private static void KitNeedsBars()
    {
        // The model: size 0 and the bar are what the bar always was.
        var plain = new UiStatusModel { Time = "14:32", Day = "Tuesday", Money = "$48,210", Energy = 72, Hunger = 48, Happiness = 64, Hygiene = 81, Stress = 34, HomeNeed = 70, PillsPress = true };
        UiPillPlan was = UiDraw.PlanPills(plain, 1920f);
        Check(was.NeedLevel == 0 && was.Needs > 0f, "kit needs: at size 0 the bar draws the small cells, as it always did");
        Check(UiDraw.PlanNeedsRow(plain, 1920f).Height == 0f, "kit needs: no row along the top unless the model puts the needs there");
        foreach (string lang in ModLanguages.All)
        {
            UiWords t = UiWords(lang);
            string[] words = { t("common_energy", "Energy"), t("common_hunger", "Hunger"), t("common_happiness", "Happiness"), t("reallife_home_strip_hygiene", "Hygiene"), t("reallife_home_strip_stress", "Stress"), t("reallife_home_strip_home", "Home") };
            foreach (float width in new[] { 1024f, 1280f, 1440f, 1728f, 1920f, 2520f, 3840f })
            {
                for (int size = 0; size <= UiDraw.NeedLevels; size++)
                {
                    foreach (int needs in new[] { 3, 6 })
                    {
                        var m = new UiStatusModel
                        {
                            Time = "14:32", Day = "Tuesday", Money = "$48,210", Energy = 72, Hunger = 48, Happiness = 64, Hygiene = needs > 3 ? 81 : -1, Stress = needs > 3 ? 34 : -1, HomeNeed = needs > 3 ? 70 : -1,
                            Player = "Sam Hartley", NeedsSize = size, NeedsTop = true, NeedLabels = words, PillsPress = true
                        };
                        UiNeedsRow row = UiDraw.PlanNeedsRow(m, width);
                        string where = lang + " " + width + " size " + size + " needs " + needs;
                        float edge = row.CellX + row.PerRow * row.CellW + (row.PerRow - 1) * UiTokens.S2;
                        Check(row.Height > 0f && row.Rows >= 1 && row.Rows <= 2 && row.PerRow * row.Rows >= needs && edge <= width - UiTokens.S4 + 0.01f,
                            "kit needs: the top row holds every need inside the screen on one row or two: " + where);
                        Check(row.Height + UiTokens.S4 + UiTokens.S2 <= 220f, "kit needs: the top row leaves the minimap and the left column their room: " + where + " (" + row.Height + ")");
                        Check(row.Level >= 1 && row.Level <= Math.Max(1, size), "kit needs: a size too large for the room steps down and never up: " + where);
                        m.NeedsTop = false;
                        UiPillPlan plan = UiDraw.PlanPills(m, width);
                        Check(plan.NeedLevel <= size && (size == 0 || plan.NeedLevel <= size), "kit needs: in the bar the size steps down to the one that fits: " + where);
                    }
                }
            }
        }
        // One width, one language, the sizes grow: the largest takes at least the room of the smallest.
        var big = new UiStatusModel { Energy = 72, Hunger = 48, Happiness = 64, Hygiene = 81, Stress = 34, HomeNeed = 70, NeedsTop = true, NeedsSize = 1, PillsPress = true };
        float h1 = UiDraw.PlanNeedsRow(big, 1920f).Height;
        big.NeedsSize = 4;
        Check(UiDraw.PlanNeedsRow(big, 1920f).Height > h1, "kit needs: the largest size is taller than the smallest");
        // The sources: the option, its place, what stands aside and what moves.
        string settings = ModSource(@"Core\Settings.cs");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        Check(settings.Contains("UiNeedsSizeId = \"ui_needs_size\"") && settings.Contains("UiNeedsSize = 0;") && settings.Contains("UiNeedsPlace = 0;") && settings.Contains("UiNeedsPlaceId = \"ui_needs_place\"") && settings.Contains("UiNeedsSize = System.Math.Max(0, System.Math.Min(4,")
              && settings.Contains("UiNeedsPlace = System.Math.Max(0, System.Math.Min(1,"), "kit needs: the two options are saved and read back inside their range");
        Check(mod.Contains(".AddSlider(Settings.UiNeedsSizeId, \"reallife_ui_needs_size\", 0, 4, Settings.UiNeedsSize,") && mod.Contains(".AddDropdown(Settings.UiNeedsPlaceId, \"reallife_ui_needs_place\""),
            "kit needs: the options stand under the RLO interface's, a slider and a choice");
        Check(ModSource(@"Ui\UiGuide.cs").Contains(".Owns(Settings.UiNeedsSizeId, Settings.UiNeedsPlaceId)") && ModSource(@"Meta\OptionsTree.cs").Contains(".Opt(\"ui_needs_size\", \"ui_interface\")"),
            "kit needs: the options belong to the RLO interface's feature and are on while it is");
        string status = ModSource(@"Ui\Hud\HudStatusGame.cs");
        Check(status.Contains("m.NeedsTop = ForHud && Settings.UiNeedsPlace == 1 && !UiMinimapBox.Dock;") && status.Contains("m.NeedsSize = Settings.UiNeedsSize;")
              && MemberText(status, "public static int Signature()").Contains("Settings.UiNeedsSize * 5 + Settings.UiNeedsPlace * 3 + (UiMinimapBox.Dock ? 1 : 0)"),
            "kit needs: the row is the HUD's own bar's alone, steps aside for the map's dock, and the option applies at the next look without a reload");
        string hud = ModSource(@"Ui\UiHud.cs");
        Check(hud.Contains("m.TopRow = needs.Height > 0f ? needs.Height + UiTokens.S2 : 0f;") && hud.Contains("y = UiTokens.S4 + m.TopRow;") && hud.Contains("frame.Width - Left, UiTokens.S4 + m.TopRow, keys)")
              && hud.Contains("frame.Width, UiTokens.S4 + m.TopRow, nx, ny, nw, nh)") && hud.Contains("UnderTopRow(m, frame.BandY, h)"),
            "kit needs: the info panel, the objectives, the side panels, the notice cards and the world prompt all stand under the row");
        string live = ModSource(@"Ui\HudScreens\HudLive.cs");
        Check(live.Contains("m.AvoidY + UiMapChrome.RouteTop - UiTokens.S4 : m.TopRow") && live.Contains("NoticeRoom.Extra = TopFoot;") && ModSource(@"Ui\Hud\HudHost.cs").Contains("HudLive.RowMoved();"),
            "kit needs: the objectives' room, the game's notifications and the minimap's column keep under the row");
        Check(ModSource(@"Ui\Hud\HudHost.cs").Contains("UiHud.UnderTopRow(hud, frame.BandY, h)") && ModSource(@"Core\GameUi\NoticeRoom.cs").Contains("public static Func<float> Extra"),
            "kit needs: a card over the world keeps under the row, and the game's notice stack under it too");
        Check(WhatsNewHas(LocaleFile("en"), "needs bars"), "kit needs: What's new tells of the options");
        Dictionary<string, string> en = LocaleFile("en");
        string lost = "none";
        foreach (string key in new[] { "reallife_ui_needs_size", "reallife_opt_tip_ui_needs_size", "reallife_ui_needs_place", "reallife_opt_tip_ui_needs_place", "reallife_ui_needs_place_0", "reallife_ui_needs_place_1", "reallife_flare_kit_rating_game" })
        {
            if (!en.ContainsKey(key) || en[key].Length == 0 || en[key].Contains("!") || en[key].Contains(" - ") || en[key].Contains("—") || en[key].Contains("–"))
            {
                lost = key;
            }
        }
        Check(lost == "none", "kit needs: the new texts are written, with no dash and no exclamation mark (" + lost + ")");
    }
}
