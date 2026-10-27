using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Uptown;

// Build mode's design window ("Choose a building") as the owner saw it (27 Oct 2026, 0.59.0, a screenshot): the seventh card drew
// below the window over the footer and the toolbar, the card under the pointer grew and sat off the others, the price ran beside the
// storeys, and the heading said "For your 20 squares on reallife:street_uptown_second". Checked here: a street in words, never an
// id (StreetNameRules), the window's scale for a screen and the game's UI scale and the cards' grid (BuildModeCatalogue), the sources
// that clip the list, keep every card one size, outline the card picked, and print no street id; then the fix line's texts.
internal static partial class Program
{
    private static void BuildModePickerChecks()
    {
        BuildModeStreetNameChecks();
        BuildModeWindowFitChecks();
        BuildModePickerSourceChecks();
        BuildModePickerTextChecks();
    }

    private static void BuildModeStreetNameChecks()
    {
        const string raw = "reallife:street_uptown_second";
        Check(StreetNameRules.HasId(raw) && StreetNameRules.HasId("ba:street_secondstreet") && StreetNameRules.HasId("reallife_newbuildings_street_uptown_second")
              && StreetNameRules.HasId("ba:neighborhood_midtown") && !StreetNameRules.HasId("2nd Street") && !StreetNameRules.HasId("Alba: Hudson Yards") && !StreetNameRules.HasId(string.Empty)
              && !StreetNameRules.HasId(null), "street names: an id of the game's or the mod's is told from words, and a word holding 'ba:' is not an id");
        Check(StreetNameRules.Words(raw, "2nd Street") == "2nd Street" && StreetNameRules.Words(raw, null) == "Second" && StreetNameRules.Words(raw, raw) == "Second"
              && StreetNameRules.Words(raw, string.Empty) == "Second" && StreetNameRules.Words(raw, "reallife_newbuildings_street_uptown_second") == "Second",
            "street names: the street's text where the game has one, else a plain word, never the id ('" + StreetNameRules.Words(raw, raw) + "')");
        Check(StreetNameRules.Words(string.Empty, "2nd Street") == string.Empty && StreetNameRules.Words(null, null) == string.Empty && StreetNameRules.Words("Broadway", null) == "Broadway"
              && StreetNameRules.Words("ba:street_secondstreet", "ba:street_secondstreet") == "Secondstreet", "street names: no street gives no words, words already are kept, a game id is a word too");
        bool all = true;
        string bad = null;
        for (int i = 0; i < LayoutStreets.All.Length && all; i++)
        {
            LayoutStreet street = LayoutStreets.All[i];
            string plain = StreetNameRules.Words(street.Id, street.Id);
            string given = StreetNameRules.Words(street.Id, "Some Street");
            all = plain.Length > 0 && !StreetNameRules.HasId(plain) && given == "Some Street" && (street.Continues == null || !StreetNameRules.HasId(StreetNameRules.Words(street.Continues, street.Continues)));
            bad = all ? null : street.Id + " gave '" + plain + "'";
        }
        Check(all, "street names: every street of Uptown reads as a word with or without its text (" + LayoutStreets.All.Length + " streets" + (bad != null ? ", " + bad : string.Empty) + ")");
        Check(StreetNameRules.Address("reallife:street_uptown_second", 1010, "1010 2nd Street", "2nd Street") == "1010 2nd Street"
              && StreetNameRules.Address("reallife:street_uptown_second", 1010, "1010 reallife:street_uptown_second", "2nd Street") == "1010 2nd Street"
              && StreetNameRules.Address("reallife:street_uptown_second", 1010, "1010 reallife:street_uptown_second", null) == "1010 Second" && StreetNameRules.Address(null, 5, null, null) == string.Empty,
            "street names: an address keeps the game's words, and where its street is still an id the number and the street's words");
        // Every text with the street's name in it takes a name, the Uptown streets' own in the texts.
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (LanguagesRead("street names", words, codes))
        {
            bool texts = true;
            string missing = null;
            for (int i = 0; i < LayoutStreets.All.Length && texts; i++)
            {
                texts = words[0].TryGetValue(LayoutStreets.All[i].NameKey, out string name) && name.Length > 0 && !StreetNameRules.HasId(name);
                missing = texts ? null : LayoutStreets.All[i].NameKey;
            }
            Check(texts, "street names: every street of Uptown has its name in the texts" + (missing != null ? " (" + missing + " has none)" : string.Empty));
        }
    }

    private static void BuildModeWindowFitChecks()
    {
        // 3840 by 2160 canvas, 2400 by 1600 window: it fits where the toolbar leaves 1608 high; a larger UI scale makes the canvas smaller.
        float full = BuildModeCatalogue.Scale(3840f, 1608f, 2400f, 1600f);
        float one5 = BuildModeCatalogue.Scale(2560f, 880f, 2400f, 1600f);   // the game's UI scale 1.5: a canvas 2560 by 1440, the toolbar and the top bar the same size
        float two = BuildModeCatalogue.Scale(1920f, 440f, 2400f, 1600f);    // the UI scale 2
        float narrow = BuildModeCatalogue.Scale(1500f, 1608f, 2400f, 1600f);
        Check(Math.Abs(full - 1f) < 0.0001f && one5 > 0.5f && one5 < 0.6f && two >= BuildModeCatalogue.ScaleLeast && two < one5 && narrow < 0.6f && narrow > BuildModeCatalogue.ScaleLeast,
            "build mode window: drawn whole where the screen has room and smaller at a larger UI scale or a narrow canvas (" + full.ToString("0.00") + ", " + one5.ToString("0.00") + ", "
            + two.ToString("0.00") + ", " + narrow.ToString("0.00") + ")");
        bool fits = true;
        for (float w = 1920f; w <= 7680f && fits; w += 320f)
        {
            for (float free = 300f; free <= 2200f && fits; free += 100f)
            {
                float s = BuildModeCatalogue.Scale(w, free, 2400f, 1600f);
                fits = s >= BuildModeCatalogue.ScaleLeast - 0.0001f && s <= 1f + 0.0001f && (s <= BuildModeCatalogue.ScaleLeast + 0.0001f || (1600f * s <= free + 0.5f && 2400f * s <= w * BuildModeCatalogue.WideShare + 0.5f));
            }
        }
        Check(fits, "build mode window: whatever the canvas and the room above the toolbar, the scaled window fits both (never above 1, never below its least)");
        Check(BuildModeCatalogue.Scale(0f, 800f, 2400f, 1600f) == 1f && BuildModeCatalogue.Scale(3840f, 0f, 2400f, 1600f) == 1f && BuildModeCatalogue.Scale(3840f, -5f, 2400f, 1600f) == 1f,
            "build mode window: nothing measured, nothing scaled");
        // The grid: the same card width for every card, three to a row on the help page's width, fewer where the page is narrow.
        float page = 1542f;
        int columns = BuildModeCatalogue.Columns(page);
        float wide = BuildModeCatalogue.CardWide(page, columns);
        Check(columns == 3 && BuildModeCatalogue.Columns(900f) == 2 && BuildModeCatalogue.Columns(300f) == 1 && BuildModeCatalogue.Columns(0f) == 1 && BuildModeCatalogue.Columns(99999f) == BuildModeCatalogue.ColumnsMost,
            "build mode cards: three to a row on the page, fewer on a narrow one, never none or more than three");
        bool inside = true;
        for (float p = 400f; p <= 2400f && inside; p += 50f)
        {
            int n = BuildModeCatalogue.Columns(p);
            float w = BuildModeCatalogue.CardWide(p, n);
            inside = w * n + BuildModeCatalogue.CardGap * (n - 1) <= p + 0.001f && (n == 1 || w >= BuildModeCatalogue.CardMin - 1f) && w > 0f;
        }
        Check(inside && wide > 400f, "build mode cards: a row of cards and their gaps lies inside the page at every width, each at least its least width (" + wide + " at " + page + ")");
    }

    // Every .cs under Scripts/Construction, by file name.
    private static Dictionary<string, string> ConstructionSources()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Construction"));
        var all = new Dictionary<string, string>(StringComparer.Ordinal);
        if (System.IO.Directory.Exists(folder))
        {
            foreach (string file in System.IO.Directory.GetFiles(folder, "*.cs"))
            {
                all[System.IO.Path.GetFileName(file)] = System.IO.File.ReadAllText(file);
            }
        }
        return all;
    }

    private static void BuildModePickerSourceChecks()
    {
        string panel = ModSource(@"Construction\BuildModePanel.cs");
        string kit = ModSource(@"Construction\BuildModeKit.cs");
        string window = ModSource(@"Construction\BuildMode.Window.cs");
        string zone = ModSource(@"Construction\ConstructionDirector.Zone.cs");
        string game = ModSource(@"Construction\ConstructionGame.cs");
        string guide = ModSource(@"Construction\ConstructionGuide.cs");
        string tests = ModSource(@"Construction\ConstructionDirector.BuildModePickerTests.cs");
        Check(panel.Contains("AddComponent<RectMask2D>()") && panel.Contains("ClipPage();") && panel.Contains("scroll.horizontal = false;")
              && MethodText(panel, "public string Overlap(float toolbarTop)").Contains("the list is not clipped to its area"),
            "build mode window: the list is a scroll view clipped to its area by a RectMask2D on the game's viewport, and the test fails if it is not");
        Check(panel.Contains("BuildModeKit.KeepSize(c.Button);") && kit.Contains("\"{\\\"scaleOnHover\\\":false}\"") && kit.Contains("effects.enabled = false;")
              && !panel.Contains("Chosen.GetComponent<Image>().color") && !panel.Contains("BuildModeBar.Paint(c.Button"),
            "build mode window: a card never changes size (the designer's hover scale of 1.1 is off on it) and the picked card is outlined, not tinted or resized");
        Check(panel.Contains("_kit.Frame(c.Button, DarkLook.Raised, false, CardEdge);") && panel.Contains("Row(c.Button.Root, \"Name\"") && panel.Contains("Row(c.Button.Root, \"Size\"")
              && panel.Contains("Row(c.Button.Root, \"Cost\"") && panel.Contains("BuildModeKit.Size(t.gameObject, -1f, high)") && panel.Contains("_gridLayout.cellSize = new Vector2(BuildModeCatalogue.CardWide(width, columns), CardHigh);"),
            "build mode window: every card has the same rows (name, kind, storeys and floor area, the price on a line of its own) at fixed heights, in a grid of one cell size");
        Check(panel.Contains("BuildModeCatalogue.Scale(units.width, free, _designSize.x, _designSize.y)") && panel.Contains("_root.localScale = new Vector3(s, s, 1f);")
              && window.Contains("_catalogue.Show(true, _bar != null ? _bar.ToolbarTop() : float.NaN);"),
            "build mode window: drawn at the scale that fits the screen and the game's UI scale, clear of the toolbar, each time it opens");
        string overlap = MethodText(panel, "public string Overlap(float toolbarTop)");
        Check(overlap.Contains("the heading shows an id") && overlap.Contains("the window covers the toolbar") && overlap.Contains("the cards are not all the same size") && overlap.Contains("a card lies outside the list's rectangle")
              && overlap.Contains("the window runs off the screen") && panel.Contains("the last card cannot be scrolled into view") && panel.Contains("the first card cannot be scrolled into view"),
            "build mode window: the test measures the heading, the screen, the toolbar, the clip, the cards' size and place, and the first and the last card by scrolling");
        Check(tests.Contains("StreetNameRules.HasId(heading)") && tests.Contains("NearestOwnFootprint(out int count)") && tests.Contains("_mode.CatalogueOverlap()") && guide.Contains("TestBuildModePickerNow()")
              && ModSource(@"TestRun\RunPlan.cs").Contains("\"reallife_bld_mode_test_picker\""),
            "build mode window: Options > Tests opens Choose a building on the player's nearest zoned land and measures it (PASS or FAIL in Player.log)");
        Check(window.Contains("private string StreetOf(LotView lot)") && window.Contains("ConstructionGame.StreetWords(lot.Street)") && !Regex.IsMatch(window, @"\?\s*_d\.LotWords\(first\.Id\)\s*:\s*first\.Street"),
            "build mode street: the heading and the panels name the street by its words (the map's and the addresses' own text), not its id");
        Check(zone.Contains("ConstructionGame.StreetWords(lot.Street)") && game.Contains("internal static string StreetWords(string street)") && game.Contains("StreetNameRules.HasId(written)")
              && game.Contains("StreetNameRules.HasId(words) ? string.Empty : words"),
            "construction texts: an address and a district never print an id, and the rezoning paper names its street in words");
        // No text of the construction code is given a street by its id: every "street" argument is words.
        var offenders = new List<string>();
        foreach (KeyValuePair<string, string> file in ConstructionSources())
        {
            foreach (Match m in Regex.Matches(file.Value, "\"(?:street|where|address|place|name)\",\\s*([A-Za-z_][\\w\\.\\[\\]]*)"))
            {
                string arg = m.Groups[1].Value;
                if (arg.EndsWith(".Street", StringComparison.Ordinal))
                {
                    offenders.Add(file.Key + ": " + m.Value);
                }
            }
        }
        Check(offenders.Count == 0, "construction texts: no text is given a street by its raw id" + (offenders.Count > 0 ? " (" + string.Join("; ", offenders) + ")" : string.Empty));
    }

    private static void BuildModePickerTextChecks()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("build mode window texts", words, codes))
        {
            return;
        }
        var fixes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Fixed: in build mode, the Choose a building window no longer draws its last designs over the footer and the toolbar." },
            { "pt", "Corrigido: no modo de construção, a janela Escolha um prédio não desenha mais os últimos projetos sobre o rodapé e a barra de ferramentas." },
            { "zh-cn", "已修复：建造模式下，选择一栋建筑的窗口不再把最后几个设计画到页脚和工具栏上。" },
            { "de", "Behoben: Im Baumodus zeichnet das Fenster Gebäude wählen die letzten Entwürfe nicht mehr über die Fußleiste und die Werkzeugleiste." },
            { "fr", "Corrigé : en mode construction, la fenêtre Choisir un bâtiment ne dessine plus les derniers projets sur le pied de page et la barre d'outils." },
            { "es", "Corregido: en el modo construcción, la ventana Elegir un edificio ya no dibuja los últimos diseños sobre el pie y la barra de herramientas." },
            { "ru", "Исправлено: в режиме строительства окно Выбор здания больше не рисует последние проекты поверх нижней строки и панели инструментов." },
            { "nl", "Opgelost: in bouwmodus tekent het venster Kies een gebouw de laatste ontwerpen niet meer over de voettekst en de werkbalk." },
            { "tr", "Düzeltildi: inşa modunda Bina seç penceresi artık son tasarımları alt bilginin ve araç çubuğunun üzerine çizmiyor." }
        };
        for (int l = 0; l < words.Length; l++)
        {
            Check(fixes.TryGetValue(codes[l], out string fix) && WhatsNewHas(words[l], fix), "build mode window texts: What's new has the fix for the overflowing design window in some row in " + codes[l]);
            Check(words[l].TryGetValue("reallife_bld_mode_test_picker", out string name) && name.Length > 0 && !name.Contains("  "), "build mode window texts: the test's name is there in " + codes[l]);
        }
    }
}
