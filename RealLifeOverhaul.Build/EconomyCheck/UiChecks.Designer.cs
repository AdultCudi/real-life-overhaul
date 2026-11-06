using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The interior designer on the kit (Scripts/Ui/Designer): every state laid out at every frame in every language (the screen registry's checks), each tool's panel its
// own page, the rules the face stands on (the search, a requirement's count, the readings against where they began, a piece of floor's cost the game's way, the time of
// day's slider, a blueprint's name), the places of the info panel, the rising panel and the bar, a requirement flipping as furniture is placed and sold, and the wiring that
// keeps the room, its overlays, its tooltips and its keys the game's. Run alone with --designer-ui.
internal static partial class Program
{
    private static readonly UiWords DesignerWords = (key, english) => english;

    static partial void UiCasesDesigner(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id != "designer")
        {
            return;
        }
        foreach (string state in UiSamples.DesignerStates)
        {
            string st = state;
            cases.Add(new UiScreenCase
            {
                Def = def, Name = "designer (" + st + ")", Title = "Interior designer", Commands = UiDesignerPanels.Commands, Overlay = true,
                Lay = (s, f, d, k, t) => UiSamples.DesignerScene(s, f, d, k, t, st)
            });
        }
    }

    private static void DesignerKitAll()
    {
        DesignerPageChecks();
        DesignerRuleChecks();
        DesignerPlaceChecks();
        DesignerRequirementChecks();
        DesignerShieldChecks();
        DesignerWiringChecks();
    }

    // Each tool's panel is a page of its own: the tool in hand names its panel, no two states draw the same words, and a state with nothing to show says so.
    private static void DesignerPageChecks()
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        bool distinct = true, named = true;
        string where = null;
        foreach (string state in UiSamples.DesignerStates)
        {
            var scene = new UiScene();
            UiSamples.DesignerScene(scene, UiFrame.FullMenu(16f / 9f, "Interior designer"), true, UiDefaultKeys.Keyboard, DesignerWords, state);
            var words = new System.Text.StringBuilder();
            for (int i = 0; i < scene.ItemCount; i++)
            {
                if (scene.Items[i].Kind == UiKind.Text && scene.Items[i].Layer == 0 && !string.IsNullOrEmpty(scene.Items[i].Text))
                {
                    words.Append(scene.Items[i].Text).Append('|');
                }
            }
            string page = words.ToString();
            if (seen.TryGetValue(page, out string other))
            {
                distinct = false;
                where = where ?? state + " draws what " + other + " draws";
            }
            seen[page] = state;
            UiDesignerModel m = UiSamples.Designer(DesignerWords, state);
            named &= m.Tools.Tool >= 0 && m.Tools.Tool < m.Tools.Tools.Length;
        }
        Check(distinct, "designer pages: every state draws a page of its own" + (where != null ? " (" + where + ")" : string.Empty));
        Check(named, "designer pages: the tool in hand is one of the fourteen the row shows");
        UiDesignerModel furniture = UiSamples.Designer(DesignerWords, "furniture"), floors = UiSamples.Designer(DesignerWords, "floors"), walls = UiSamples.Designer(DesignerWords, "walls"),
            producer = UiSamples.Designer(DesignerWords, "producer"), queue = UiSamples.Designer(DesignerWords, "queue"), security = UiSamples.Designer(DesignerWords, "security"),
            time = UiSamples.Designer(DesignerWords, "time"), blueprint = UiSamples.Designer(DesignerWords, "blueprint"), palette = UiSamples.Designer(DesignerWords, "palette");
        Check(furniture.Panel == UiDesignerPanelKind.Furniture && floors.Panel == UiDesignerPanelKind.Materials && walls.Panel == UiDesignerPanelKind.Materials
              && producer.Panel == UiDesignerPanelKind.Items && queue.Panel == UiDesignerPanelKind.Items && security.Panel == UiDesignerPanelKind.Items && time.Panel == UiDesignerPanelKind.Time
              && blueprint.Panel == UiDesignerPanelKind.Blueprint && palette.Panel == UiDesignerPanelKind.Note,
            "designer pages: each tool rises its own kind of panel (the catalogue, the materials twice, the item lists thrice, the time, the blueprint, a note)");
        Check(floors.Materials.Tab == 0 && walls.Materials.Tab == 1 && floors.Materials.LayLabel != null && walls.Materials.LayLabel == null,
            "designer pages: Floors and Walls are two tabs of one panel, and only the floor offers to be laid whole");
        Check(security.Items.Meter >= 0 && producer.Items.Meter < 0 && queue.Items.Meter < 0, "designer pages: the security rating's meter stands on the Security tool's list alone");
    }

    private static void DesignerRuleChecks()
    {
        Check(DesignerRules.Matches("Flower stand", "flower") && DesignerRules.Matches("Flower stand", "WER ST") && DesignerRules.Matches("Flower stand", "  ") && !DesignerRules.Matches("Flower stand", "shelf")
              && DesignerRules.Matches("anything", null) && !DesignerRules.Matches(null, "a"),
            "designer rules: the search is the game's own, the name in lower case with its spaces out holding the words typed the same way");
        Check(DesignerRules.IsTagSearch("#desk") && DesignerRules.IsTagSearch("  #nightclub") && !DesignerRules.IsTagSearch("desk") && !DesignerRules.IsTagSearch(null),
            "designer rules: a hash asks the game for a tag, as its own search does");
        const string of = "{have} of {need}";
        Check(DesignerRules.CountWords(true, true, 6, 6, of) == "6" && DesignerRules.CountWords(true, false, 0, 1, of) == "0 of 1" && DesignerRules.CountWords(false, false, 0, 0, of) == null
              && DesignerRules.CountWords(true, true, 3, 0, of) == null && DesignerRules.MetWords(4, 6, of) == "4 of 6" && DesignerRules.MetWords(4, 6, "{have}/{need}") == "4/6",
            "designer rules: a requirement's count says what is placed once met and what is placed of what is needed while short, and nothing for a kind of thing");
        Check(DesignerRules.Fill(56, 80) == 70 && DesignerRules.Fill(80, 80) == 100 && DesignerRules.Fill(120, 80) == 100 && DesignerRules.Fill(0, 80) == 0 && DesignerRules.Fill(5, 9999) == 100
              && DesignerRules.Fill(0, 9999) == 0 && DesignerRules.Fill(5, 0) == 100,
            "designer rules: a meter fills to its share, full past its limit, and the game's no limit (9999) reads full once there is something");
        Check(DesignerRules.Change(64, 56, null) == "+8" && DesignerRules.Change(70, 74, "%") == "-4%" && DesignerRules.Change(5, 5, "%") == null,
            "designer rules: a reading's change is signed against where it began, and nothing while it has not moved");
        // The game's own price of changing a piece (InteriorElementTool.CalculateInteriorElementChangePrice): the same material costs nothing, back to the start pays back what this
        // time paid, a change over a change takes back the one before, and the room's own first material is never paid back.
        Check(DesignerRules.PieceCost("oak", 0, "tile", 0, "oak", 0, 32f, 32f) == 0f
              && DesignerRules.PieceCost("tile", 0, "tile", 0, "oak", 0, 32f, 28f) == -32f
              && DesignerRules.PieceCost("oak", 0, "tile", 0, "tile", 0, 28f, 32f) == 32f
              && DesignerRules.PieceCost("walnut", 0, "tile", 0, "oak", 0, 32f, 35f) == 3f
              && DesignerRules.PieceCost("oak", 1, "tile", 0, "oak", 0, 32f, 32f) == 0f,
            "designer rules: a piece of floor costs what the game's own tool charges: nothing for what it is, what this edit paid back going home, the new price less the last's");
        Check(DesignerRules.Share(870f, 300f, 1440f) == 0.5f && DesignerRules.Share(100f, 300f, 1440f) == 0f && DesignerRules.Share(5000f, 300f, 1440f) == 1f && DesignerRules.Share(1f, 5f, 5f) == 0f,
            "designer rules: the slider's share runs from its first minute to its last");
        Check(DesignerRules.Value(0.5f, 300f, 1440f) == 870f && DesignerRules.Value(0f, 300f, 1440f) == 300f && DesignerRules.Value(1f, 300f, 1440f) == 1440f && DesignerRules.Value(0.3333f, 300f, 1440f) % 10f == 0f,
            "designer rules: a place on the slider is a whole ten minutes between its ends");
        Check(DesignerRules.Step(870f, 10, 300f, 1440f) == 880f && DesignerRules.Step(870f, -10, 300f, 1440f) == 860f && DesignerRules.Step(1435f, 10, 300f, 1440f) == 1440f
              && DesignerRules.Step(300f, -10, 300f, 1440f) == 300f && DesignerRules.Step(873f, 10, 300f, 1440f) == 880f,
            "designer rules: the step buttons move the light ten minutes and stop at the day's ends, as the game's own do");
        Check(DesignerRules.Clock(870f) == "14:30" && DesignerRules.Clock(1440f) == "24:00" && DesignerRules.Clock(305f) == "05:05" && DesignerRules.Clock(-5f) == "00:00",
            "designer rules: a minute of the day reads as a clock");
        var marks = new int[12];
        int n = DesignerRules.Marks(300f, 1440f, marks);
        Check(n == 7 && marks[0] == 360 && marks[n - 1] == 1440 && DesignerRules.Marks(0f, 1440f, marks) == 9, "designer rules: the hours under the slider are every third one that stands on it");
        Check(DesignerRules.NameProblem("Bloom and Co florist, third layout") == 0 && DesignerRules.NameProblem("Bloom & Co v1.2") == 2 && DesignerRules.NameProblem("a/b") == 2
              && DesignerRules.NameProblem(string.Empty) == 1 && DesignerRules.NameProblem(null) == 1,
            "designer rules: a blueprint's name is refused empty, with a dot, or with a character a file name cannot hold, as the game refuses it");
        Check(DesignerRules.Initials("Bloom & Co") == "BC" && DesignerRules.Initials("florist") == "FL" && DesignerRules.Initials("  ") == "?" && DesignerRules.Initials("A") == "A" && DesignerRules.Initials("Quick Fix Plumbing") == "QF",
            "designer rules: a business's initials for its tile");
        var tally = new DesignerRules.Tally();
        tally.Add(DesignerRules.Cause.Furniture, 1240f, 1);
        tally.Add(DesignerRules.Cause.Furniture, 560f, 1);
        tally.Add(DesignerRules.Cause.Floor, 1180f, 64);
        tally.Add(DesignerRules.Cause.Sold, -900f, 2);
        Check(tally.Money[(int)DesignerRules.Cause.Furniture] == 1800f && tally.Count[(int)DesignerRules.Cause.Furniture] == 2 && tally.Count[(int)DesignerRules.Cause.Floor] == 64
              && tally.Total() == 2080f, "designer rules: this edit's money is told by cause and adds up to what the game takes on apply");
        tally.Clear();
        Check(tally.Total() == 0f && tally.Count[(int)DesignerRules.Cause.Sold] == 0, "designer rules: a new edit starts from nothing");
    }

    // The info panel, the rising panel and the bar's pieces never meet: at every frame the game gives, in every state, in every language.
    private static void DesignerPlaceChecks()
    {
        bool apart = true, inside = true, fits = true;
        string where = null;
        foreach (float aspect in UiAspects)
        {
            UiFrame f = UiFrame.FullMenu(aspect, "Interior designer");
            foreach (string state in UiSamples.DesignerStates)
            {
                UiDesignerModel m = UiSamples.Designer(DesignerWords, state);
                UiDesignerPanels.PanelBox(m, f, out float px, out float py, out float pw, out float ph);
                bool folded = m.Info.Folded || (pw > 0f && Margin() + UiDesignerPanels.InfoHeight(m.Info, false) + UiTokens.S3 > py && px < Margin() + UiDesignerPanels.InfoWidth);
                float infoH = UiDesignerPanels.InfoHeight(m.Info, folded);
                if (pw > 0f)
                {
                    bool meet = px < Margin() + UiDesignerPanels.InfoWidth && Margin() + infoH > py;
                    apart &= !meet;
                    inside &= px >= 0f && px + pw <= f.Width && py + ph <= f.BandY && py >= infoH * 0f;
                    if (meet || px < 0f || px + pw > f.Width)
                    {
                        where = where ?? state + " at " + aspect.ToString("0.00") + ": the rising panel meets the info panel or runs off the screen";
                    }
                }
                fits &= Margin() + infoH <= f.BandY;
                // The catalogue's tiles: two columns at least, the wheel moving whole rows.
                if (m.Panel == UiDesignerPanelKind.Furniture || m.Panel == UiDesignerPanelKind.Materials)
                {
                    fits &= UiDesignerPanels.Columns(pw) >= 2;
                }
            }
        }
        Check(apart && inside && fits, "designer places: no panel over another and all on the screen at every frame" + (where != null ? " (" + where + ")" : string.Empty));
        // The grid's own arithmetic: the rows in view, the scroll a whole row, never past the last.
        int cols = UiDesignerPanels.Columns(UiDesignerPanels.CatalogueWidth);
        Check(cols >= 5 && UiDesignerPanels.Visible(0, cols, 0f, out _, out _) == false, "designer places: the catalogue holds at least five tiles a row, and none in view while there are none");
        UiDesignerPanels.Visible(40, cols, 0f, out int first, out int last);
        Check(first == 0 && last == cols * UiDesignerPanels.Rows - 1, "designer places: the first rows are in view at the top (" + first + " to " + last + ")");
        float step = UiDesignerPanels.RowStep;
        Check(UiDesignerPanels.Scrolled(40, cols, -10f) == 0f && UiDesignerPanels.Scrolled(40, cols, step * 1.2f) == step && UiDesignerPanels.Scrolled(40, cols, step * 1000f) == ((40 + cols - 1) / cols - UiDesignerPanels.Rows) * step
              && UiDesignerPanels.Scrolled(3, cols, step * 5f) == 0f,
            "designer places: the wheel moves the tiles a whole row at a time and never past the last");
        Check(UiDesignerPanels.ItemsScrolled(3, 100f) == 0f && UiDesignerPanels.ItemsScrolled(20, 100f) == Math.Round(100f / UiDesignerPanels.ItemStep) * UiDesignerPanels.ItemStep
              && UiDesignerPanels.ItemsVisible(20, 0f, out int ifirst, out int ilast) && ifirst == 0 && ilast == UiDesignerPanels.ItemRows - 1, "designer places: an item list scrolls a whole row at a time");
        // The row of categories is one target: the face tells which tab a place across it means from where the layout stood each one, at every frame.
        bool tabs = true;
        foreach (float aspect in UiAspects)
        {
            UiFrame tf = UiFrame.FullMenu(aspect, "Interior designer");
            UiDesignerModel withTabs = UiSamples.Designer(DesignerWords, "furniture");
            UiDesignerPanels.Lay(new UiScene(), withTabs, tf, true, UiDefaultKeys.Keyboard);
            UiDesignerCatalogue c = withTabs.Catalogue;
            tabs &= c.TabCount == 8 && UiDesignerPanels.TabAt(c, c.TabLeft[0] - 8f) == -1 && UiDesignerPanels.TabAt(c, c.TabRight[c.TabCount - 1] + 40f) == -1;
            for (int i = 0; i < c.TabCount; i++)
            {
                tabs &= c.TabRight[i] > c.TabLeft[i] && UiDesignerPanels.TabAt(c, (c.TabLeft[i] + c.TabRight[i]) / 2f) == i && (i == 0 || c.TabLeft[i] >= c.TabRight[i - 1]);
            }
        }
        Check(tabs, "designer places: the eight categories stand in a row in order at every frame, and a place across it names the one it is over");
        // The bar: the tool row, its steps and the right end stand between the time and the screen's edge at the narrowest frame.
        UiFrame narrow = UiFrame.FullMenu(4f / 3f, "Interior designer");
        UiDesignerModel bar = UiSamples.Designer(DesignerWords, "second_floor");
        float row = UiDesignerPanels.RowWidth(bar);
        Check(row > 0f && row < narrow.Width - 400f, "designer places: the tool row and its steps leave room for the time and the right end at the narrowest frame (" + row.ToString("0") + " of " + narrow.Width.ToString("0") + " px)");
    }

    private static float Margin() => UiDesignerPanels.Margin;

    // Requirements on the designer's live item list: placing a thing a requirement wants flips it met before the edit is applied, and selling it flips it back. The counts are
    // the placed items that meet the requirement, from the same list the game's own check reads (the building registration's items, which the designer's own actions change).
    private static void DesignerRequirementChecks()
    {
        var flower = new DesignerRequirementRules.Rule { Names = new[] { "flowerstand" }, Mask = 0, Need = 2 };
        var lights = new DesignerRequirementRules.Rule { Names = null, Mask = 0x800, Need = 0 };
        var placed = new List<DesignerRequirementRules.Placed>();
        DesignerRequirementRules.Read(flower, placed, out int have0, out bool met0);
        placed.Add(new DesignerRequirementRules.Placed { Name = "flowerstand", Mask = 0 });
        DesignerRequirementRules.Read(flower, placed, out int have1, out bool met1);
        placed.Add(new DesignerRequirementRules.Placed { Name = "flowerstand", Mask = 0 });
        DesignerRequirementRules.Read(flower, placed, out int have2, out bool met2);
        placed.RemoveAt(1);
        DesignerRequirementRules.Read(flower, placed, out int have3, out bool met3);
        Check(have0 == 0 && !met0 && have1 == 1 && !met1 && have2 == 2 && met2 && have3 == 1 && !met3,
            "designer requirements: a count line goes from short to met as the second piece is placed, and back as one is sold, before anything is applied (" + have0 + ", " + have1 + ", " + have2 + ", " + have3 + ")");
        placed.Clear();
        DesignerRequirementRules.Read(lights, placed, out int l0, out bool lm0);
        placed.Add(new DesignerRequirementRules.Placed { Name = "lamp", Mask = 0x800 | 0x1 });
        DesignerRequirementRules.Read(lights, placed, out int l1, out bool lm1);
        placed.Clear();
        DesignerRequirementRules.Read(lights, placed, out int l2, out bool lm2);
        Check(l0 == 0 && !lm0 && l1 == 1 && lm1 && l2 == 0 && !lm2, "designer requirements: a kind of thing's line is met by the first piece of that kind placed and unmet as it is sold");
        Check(DesignerRequirementRules.Meets(flower, "flowerstand", 0) && !DesignerRequirementRules.Meets(flower, "chair", 0) && DesignerRequirementRules.Meets(lights, "lamp", 0x800 | 0x1)
              && !DesignerRequirementRules.Meets(lights, "lamp", 0x1), "designer requirements: an item fills a requirement by the same test that counts it (its name, or a flag of its type)");
        var all = new[] { flower, lights };
        placed.Clear();
        int unmet = DesignerRequirementRules.CountMet(all, placed, out int of);
        placed.Add(new DesignerRequirementRules.Placed { Name = "flowerstand", Mask = 0x800 });
        placed.Add(new DesignerRequirementRules.Placed { Name = "flowerstand", Mask = 0 });
        Check(unmet == 0 && of == 2 && DesignerRequirementRules.CountMet(all, placed, out _) == 2, "designer requirements: the figure at the head is how many lines are met of how many");
    }

    // The kit's boxes that are not targets let the mouse through to the room, where a click would place or sell something: a clear shield stands under the info panel, the
    // rising panel and the bar. Its boxes are the layout's own, inside the frame and covering what the layout draws, at every frame and in every state.
    private static void DesignerShieldChecks()
    {
        bool inside = true, covers = true, counted = true;
        string where = null;
        var rects = new float[12];
        foreach (float aspect in UiAspects)
        {
            UiFrame f = UiFrame.FullMenu(aspect, "Interior designer");
            foreach (string state in UiSamples.DesignerStates)
            {
                UiDesignerModel m = UiSamples.Designer(DesignerWords, state);
                int n = UiDesignerPanels.Blocks(m, f, rects);
                UiDesignerPanels.PanelBox(m, f, out float px, out float py, out float pw, out float ph);
                counted &= n == (pw > 0f ? 3 : 2);
                for (int i = 0; i < n; i++)
                {
                    float x = rects[i * 4], y = rects[i * 4 + 1], w = rects[i * 4 + 2], h = rects[i * 4 + 3];
                    bool fits = x >= 0f && y >= 0f && w > 0f && h > 0f && x + w <= f.Width + 0.01f && y + h <= f.Height + 0.01f;
                    inside &= fits;
                    if (!fits)
                    {
                        where = where ?? state + " at " + aspect.ToString("0.00") + ": box " + i;
                    }
                }
                // Every box the layout draws as a panel lies inside one of the shield's.
                var scene = new UiScene();
                UiSamples.DesignerScene(scene, f, true, UiDefaultKeys.Keyboard, DesignerWords, state);
                for (int i = 0; i < scene.ItemCount; i++)
                {
                    UiItem it = scene.Items[i];
                    if (it.Kind != UiKind.Box || (it.Flags & UiFlags.Panel) == 0)
                    {
                        continue;
                    }
                    bool held = false;
                    for (int r = 0; r < n; r++)
                    {
                        held |= it.X >= rects[r * 4] - 0.01f && it.Y >= rects[r * 4 + 1] - 0.01f && it.X + it.W <= rects[r * 4] + rects[r * 4 + 2] + 0.01f
                                && it.Y + it.H <= rects[r * 4 + 1] + rects[r * 4 + 3] + 0.01f;
                    }
                    covers &= held;
                    if (!held)
                    {
                        where = where ?? state + " at " + aspect.ToString("0.00") + ": a panel at " + it.X.ToString("0") + "," + it.Y.ToString("0") + " is not under a shield";
                    }
                }
            }
        }
        Check(counted && inside && covers, "designer shield: a shield stands under each panel and the bar, inside the frame, at every frame and in every state" + (where != null ? " (" + where + ")" : string.Empty));
        // The time slider's track, for the pointer: inside the rising panel, and the pointer's two ends are the day's two ends.
        bool track = true;
        foreach (float aspect in UiAspects)
        {
            UiFrame f = UiFrame.FullMenu(aspect, "Interior designer");
            UiDesignerModel m = UiSamples.Designer(DesignerWords, "time");
            UiDesignerPanels.PanelBox(m, f, out float px, out _, out float pw, out _);
            UiDesignerPanels.TimeSlider(m, f, out float sx, out float sw);
            track &= sw > 200f && sx > px && sx + sw < px + pw;
            track &= UiDraw.SliderShare(sw, m.Time.Figure, 0f) == 0f && UiDraw.SliderShare(sw, m.Time.Figure, 1f) == 1f;
        }
        Check(track, "designer shield: the time slider's track lies inside its panel at every frame, and the pointer's two ends are the day's two ends");
    }

    // What keeps the room, its world overlays, its tooltips and its keys the game's, and what gives the kit's face only while its switch is on.
    private static void DesignerWiringChecks()
    {
        string face = ModSource(@"Ui\Designer\DesignerFace.cs");
        string edit = ModSource(@"Ui\Designer\DesignerGame.Edit.cs");
        string game = ModSource(@"Ui\Designer\DesignerGame.cs") + ModSource(@"Ui\Designer\DesignerGame.Read.cs") + edit + ModSource(@"Ui\Designer\DesignerPanes.cs")
            + ModSource(@"Ui\Designer\DesignerGame.Catalogue.cs") + ModSource(@"Ui\Designer\DesignerGame.Materials.cs") + ModSource(@"Ui\Designer\DesignerGame.Sheets.cs");
        string kits = ModSource(@"Ui\Designer\DesignerKits.cs");
        string tips = ModSource(@"Ui\Designer\DesignerTips.cs");
        string director = ModSource(@"Ui\UiDirector.cs");
        Check(face.Length > 0 && game.Length > 0 && kits.Length > 0 && tips.Length > 0, "designer wiring: the face, the game's reads and presses, the host's wiring and the tips are all there to read");
        Check(face.Contains("DesignerKits.Shield(") && kits.Contains("image.color = new Color(0f, 0f, 0f, 0f)") && kits.Contains("SetAsFirstSibling()"),
            "designer wiring: a clear shield under the panels holds the clicks the kit's boxes do not take, so a click on a panel never reaches the room behind it");
        Check(kits.Contains("UiScreenSwitches.On(DesignerFace.ScreenId)") && director.Contains("DesignerKits.Install();") && director.Contains("DesignerKits.Remove(why);") && director.Contains("DesignerKits.Rearm();"),
            "designer wiring: the kit's face is put over the game's designer only while the interface and its own switch are on, and given back when either goes");
        Check(kits.Contains("UiGameKind.Overlay") && kits.Contains("Keys => false") && kits.Contains("Blocks => false"),
            "designer wiring: the face takes the pointer over its own targets and no key, and holds no click it does not take, so the room and the game's own keys stay the game's");
        Check(!face.Contains("IsOpen =") && !game.Contains("IsOpen =") && !game.Contains("SetActive(false)") && !face.Contains("SetActive(false)"),
            "designer wiring: the game's designer stays open and alive under the kit (IsOpen, Going Public's and Immersion's reads), and nothing of the game's is switched off");
        Check(game.Contains(".ToggleTool(") && !game.Contains("InteriorDesignerController.SetTool(") && game.Contains("toolToOpenAfterUsage"),
            "designer wiring: every tool is opened through the game's own ToggleTool, with the game's reset of the tool to come back to");
        Check(game.Contains(".HideWithConfirm()") && game.Contains(".OnCloseButton()") && game.Contains(".Undo()") && game.Contains(".Redo()") && game.Contains(".ShowUpperFloor()")
              && game.Contains(".ShowLowerFloor()") && game.Contains(".ToggleWalls()"),
            "designer wiring: apply, close, undo, redo, the floors and the walls are the game's own methods, so its question, its checks and Going Public's patches run");
        Check(game.Contains("onClickItemName") && game.Contains("ToggleFurnitureFavorite") && game.Contains("IsFurnitureFavorite") && !game.Contains("FilterByTags(") && !game.Contains("FilterByText("),
            "designer wiring: furniture is placed by the call the game's own tile makes, its filters are the game's own switches and search field, and its favourites are the game's own list");
        string requirements = MethodText(edit, "private static void ReadRequirements()");
        Check(requirements.Contains("reg.itemInstances.Values") && requirements.Contains("IsRequirementMet(reg)") && requirements.Contains("Placed.Clear();"),
            "designer wiring: the requirement lines read the building registration's live list of items, which the designer's own actions change as things are placed and sold, and the game's own test");
        string hints = ModSource(@"Ui\HudScreens\HudHints.cs");
        Check(hints.Contains("public static void Keep(bool on)") && hints.Contains("if (shown || _kept)") && !MethodText(hints, "public static int Mirror(").Contains("Walking")
              && !MethodText(hints, "public static int Mirror(").Contains("Back("),
            "designer wiring: the game's hint panel stays hidden while the designer's chips stand in its place, and the chips are the game's hint lines alone");
        Check(game.Contains("HudHints.Keep(true)") && game.Contains("HudHints.Keep(false)") && game.Contains("DesignerKits.Classic()") && game.Contains("BlueprintCreatorMode"),
            "designer wiring: the blueprint creator's own panels are left to the game, and the hint chips are given back with the designer");
        Check(game.Contains("ExecuteRevertibleAction") && game.Contains("new InteriorElementRevertibleAction("),
            "designer wiring: laying the whole floor is the game's own revertible action through the designer's own stack, so undo and redo are the game's");
        Check(tips.Contains("ItemInfoTooltip") && tips.Contains("TooltipTarget") && tips.Contains("TooltipSystem.AddLabel(") && face.Contains("DesignerTips.") && !face.Contains("UiDraw.Text("),
            "designer wiring: a tile's tooltip is the game's own ItemInfoTooltip and a tool's name goes through its TooltipSystem too, never a tooltip the kit draws");
        Check(face.Contains("IUiPointing") && face.Contains("IUiTyping") && face.Contains("public bool Back() => false;") && face.Contains("public bool Act(UiAction action) => false;"),
            "designer wiring: the face takes no Back and no action key, so Esc and the game's tool keys are the game's");
        string floors = MethodText(game, "private static bool HasFloors()");
        Check(floors.Contains("multipleHeightsBuildingController != null") && !floors.Contains("activeSelf") && !game.Contains("DesignerFloors"),
            "designer wiring: whether the building has floors is read from the building itself, not from the game's floor buttons, which the game switches only after the kit's first layout (an empty building then drew the game's old designer)");
        Check(!game.Contains("Harmony") && !face.Contains("Harmony") && !kits.Contains("Harmony"), "designer wiring: no Harmony patch");
        string[] commands =
        {
            "ToolCommand", "StepCommand", "ApplyCommand", "CloseCommand", "FoldCommand", "ShowCommand", "HowCommand", "CardCommand", "SearchCommand", "FavouritesCommand",
            "StarCommand", "PlaceCommand", "ClearCommand", "MaterialTabCommand", "MaterialCardCommand", "MaterialSearchCommand", "ShadeCommand", "LayCommand", "ItemCommand",
            "TimeSliderCommand", "TimeLessCommand", "TimeMoreCommand", "NameCommand", "SaveCommand", "TypeOptionCommand", "TypeContinueCommand", "TypeCancelCommand", "TypeFieldCommand", "TabStripCommand"
        };
        var missing = new List<string>();
        string press = MethodText(face, "private bool Do(int command, int arg)");
        foreach (string c in commands)
        {
            if (!press.Contains("UiDesignerPanels." + c))
            {
                missing.Add(c);
            }
        }
        Check(missing.Count == 0, "designer wiring: every command the panels draw has a handler in the face's Press (" + (missing.Count == 0 ? "all " + commands.Length : string.Join(", ", missing)) + ")");
        Check(commands.Length == UiDesignerPanels.Commands.Length, "designer wiring: the list of commands the checks hold is the panels' own");
        bool block = true;
        foreach (int c in UiDesignerPanels.Commands)
        {
            block &= c >= 9560 && c <= 9599;
        }
        Check(block, "designer wiring: every command stands in the designer's own block, 9560 to 9599, which no other part uses");
    }
}
