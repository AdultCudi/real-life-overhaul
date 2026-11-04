using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Ui;

// The keys' prompts and the app tray (4 Nov): two independent reviews saw the prompt bar drawn over the tray's last tile, and the tray
// pushed aside, in the Work and the Company apps. The prompts were laid on the status bar's own strip, sharing its width with the tray
// and the right pills, and the plan gave way to them: it shortened the row, dropped pills, and still ran past the room when the words
// were long. The prompts now stand as key chips in a lane over the bar (UiDraw.KeyChips). These lay the kit with the bar and the tray
// at the frames the game gives (1080, 720 and 4K all lay out at 1920 x 1080; then the UI zoom's two ends, 16:10, the ultrawide and the
// narrowest), with the keyboard and a pad, in every language, with the most key caps any screen shows, and fail on any chip over the
// bar, a window, another chip or the screen's edge, and on any piece of the bar that moves when the prompts change.
internal static partial class Program
{
    private const int KitPromptScenes = 4;   // the kit's window, a full app, a simple app, the HUD's bar
    private const int WindowPrompts = 7;     // the most prompts a window's screen shows (move, select, tabs, main, second, back, and All apps on a pad)

    private static void KitPromptChecks()
    {
        Func<UiStatusModel> was = UiChrome.Bar;
        try
        {
            KitPromptLay();
        }
        finally
        {
            UiChrome.Bar = was;
        }
        KitPromptSources();
    }

    private static void KitPromptLay()
    {
        var frames = new[]
        {
            UiFrame.FullMenu(16f / 9f), new UiFrame { Width = 1920f / 0.8f, Height = 1080f / 0.8f, Band = UiFrame.BandHeight },
            new UiFrame { Width = 1920f / 1.2f, Height = 1080f / 1.2f, Band = UiFrame.BandHeight }, UiFrame.FullMenu(16f / 10f), UiFrame.FullMenu(21f / 9f),
            new UiFrame { Width = UiHudRules.LeastWidth, Height = 720f, Band = UiFrame.BandHeight },
        };
        string moved = null, over = null, edge = null, lost = null, tray = null, crowded = null;
        int laid = 0, chips = 0, wrapped = 0, trays = 0, caps = 0;
        float above = float.MaxValue, below = float.MaxValue;   // the least px between the chips and a window's foot, and between the chips and the bar
        foreach (string lang in ModLanguages.All)
        {
            UiWords t = UiWords(lang);
            UiPrompt[] two = { P(UiAction.Move, t("reallife_ui_prompt_move", "Move")), P(UiAction.Back, t("reallife_ui_prompt_back", "Back")) };
            UiPrompt[] most =
            {
                P(UiAction.Move, t("reallife_ui_prompt_move", "Move")), P(UiAction.Confirm, t("reallife_ui_prompt_select", "Select")),
                P(UiAction.TabPrev, t("reallife_ui_market_prompt_district", "District")), P(UiAction.Alt, t("reallife_ui_prompt_choose", "Choose") + ", $1,850"),
                P(UiAction.Option, t("reallife_ui_shell_prompt_folder", "Folder")), P(UiAction.Back, t("reallife_ui_prompt_back", "Back")),
            };
            var crowd = new List<UiPrompt>(most);   // the HUD's hints run to nine numbered keys and the way back
            for (int n = 0; n < 9; n++)
            {
                crowd.Insert(crowd.Count - 1, P((UiAction)((int)UiAction.Pick1 + n), t("reallife_ui_prompt_choose", "Choose") + " " + (n + 1)));
            }
            UiPrompt[][] sets = { two, most, crowd.ToArray() };
            foreach (bool night in new[] { false, true })
            {
                UiStatusModel bar = UiSamples.HudBar(t, false, false, night);
                bar.PillsPress = false;   // the pills are pictures under an app, as in the full menu
                UiChrome.Bar = () => bar;
                foreach (UiFrame f in frames)
                {
                    foreach (UiPrompt[] set in sets)
                    {
                        for (int scene = 0; scene < KitPromptScenes; scene++)
                        {
                            UiFrame mouse = f;
                            mouse.Pointer = true;
                            var reference = new UiScene();
                            KitPromptScene(reference, mouse, UiDefaultKeys.Keyboard, t, set, scene);
                            KitChips(reference, mouse, 0, "the mouse", ref chips, ref wrapped, ref over, ref edge, ref lost, ref crowded, ref above, ref below, ref caps);
                            foreach (IUiKeys keys in new IUiKeys[] { UiDefaultKeys.Keyboard, UiDefaultKeys.XboxPad })
                            {
                                if (keys.Pad != UiPad.None && lang != "en" && lang != "pt" && lang != "zh-cn")
                                {
                                    continue;   // a pad's glyphs are the same in every language
                                }
                                UiFrame led = f;
                                led.Pointer = false;
                                var s = new UiScene();
                                KitPromptScene(s, led, keys, t, set, scene);
                                laid++;
                                string at = lang + " " + f.Width.ToString("0") + "x" + f.Height.ToString("0") + (keys.Pad != UiPad.None ? " pad" : " keys") + " " + set.Length + " prompts "
                                            + (night ? "night " : "") + "scene " + scene;
                                moved = moved ?? BarMoved(reference, s, led, at);
                                tray = tray ?? TrayOff(s, led, bar, at);
                                trays++;
                                // A pad adds All apps on View; only the crowd of the HUD's hints is wider than a window's screen is ever given.
                                KitChips(s, led, set.Length + (keys.Pad != UiPad.None ? 1 : 0), at, ref chips, ref wrapped, ref over, ref edge, ref lost, ref crowded, ref above, ref below, ref caps);
                            }
                        }
                    }
                }
            }
        }
        Check(moved == null && trays > 0, "kit prompts: the tray, the pills and the app tiles stand where the mouse leaves them whatever the prompts say, keys or pad, in every language ("
                                          + trays + " layouts)" + (moved == null ? "" : " (" + moved + ")"));
        Check(tray == null && trays > 0, "kit prompts: the tray stands centred on the screen with the same apps at every prompt count" + (tray == null ? "" : " (" + tray + ")"));
        Check(over == null && chips > 0 && below >= UiTokens.S2 - 0.01f && above >= UiTokens.S2 - 0.01f, "kit prompts: no chip stands over the bar, a window or another chip, the window's foot at least " + above.ToString("0")
                                          + " px and the bar at least " + below.ToString("0") + " px clear (" + laid + " layouts, " + chips + " chips)" + (over == null ? "" : " (" + over + ")"));
        Check(edge == null, "kit prompts: every chip stands inside the screen's width, a row too wide wrapping to the next (" + wrapped + " wrapped rows laid)" + (edge == null ? "" : " (" + edge + ")"));
        Check(lost == null && caps > 0, "kit prompts: every prompt is drawn once as a chip with its key cap and its words (" + caps + " caps)" + (lost == null ? "" : " (" + lost + ")"));
        Check(crowded == null, "kit prompts: with the mouse leading no chip is drawn" + (crowded == null ? "" : " (" + crowded + ")"));
        Console.WriteLine("  kit prompts: " + laid + " layouts with the keys, " + chips + " chips, " + caps + " key caps, the window's foot " + above.ToString("0") + " px and the bar " + below.ToString("0")
                          + " px clear of the chips at the least, " + wrapped + " wrapped rows");
    }

    // The scenes: the kit's window with the prompts it is given, a full app (the studio: tabs and a sidebar), a simple one (the contacts' list and
    // detail), and the HUD's own bar.
    private static void KitPromptScene(UiScene s, UiFrame f, IUiKeys keys, UiWords t, UiPrompt[] set, int scene)
    {
        switch (scene)
        {
            case 0:
                s.Begin("kit window", f.Width, f.Height, true);
                UiDraw.Window(s, f, "App", keys);
                foreach (UiPrompt p in set)
                {
                    s.AddPrompt(p.Action, p.Label);
                }
                UiDraw.PromptBar(s, f, keys);
                break;
            case 1:
                UiAppModel app = UiSamples.Studio(t);
                app.Prompts = set;
                app.PromptCount = set.Length;
                UiAppWindow.Lay(s, app, f, true, keys);
                break;
            case 2:
                UiListDetailModel list = UiSamples.ContactsWindow(t);
                list.Prompts = set;
                list.PromptCount = set.Length;
                UiListDetail.Lay(s, list, f, true, keys);
                break;
            default:
                UiHud.Lay(s, new UiHudModel { Prompts = set, PromptCount = set.Length }, f, true, keys);
                break;
        }
    }

    private static UiPrompt P(UiAction action, string label) => new UiPrompt { Action = action, Label = label };

    // The strip's own pieces (every item standing on the bar but a prompt) as the mouse lays them, then with the prompts: not a tile, a pill or a count moves.
    private static string BarMoved(UiScene mouse, UiScene led, UiFrame f, string at)
    {
        var a = new List<float[]>();
        var b = new List<float[]>();
        BarItems(mouse, f, a);
        BarItems(led, f, b);
        if (a.Count != b.Count)
        {
            return at + ": the bar holds " + b.Count + " pieces with the prompts and " + a.Count + " with the mouse";
        }
        for (int i = 0; i < a.Count; i++)
        {
            for (int k = 0; k < 4; k++)
            {
                if (Math.Abs(a[i][k] - b[i][k]) > 0.01f)
                {
                    return at + ": a piece of the bar at " + a[i][0].ToString("0.#") + "," + a[i][1].ToString("0.#") + " (" + a[i][2].ToString("0.#") + "x" + a[i][3].ToString("0.#") + ") stands at "
                           + b[i][0].ToString("0.#") + "," + b[i][1].ToString("0.#") + " (" + b[i][2].ToString("0.#") + "x" + b[i][3].ToString("0.#") + ") with them";
                }
            }
        }
        return null;
    }

    private static void BarItems(UiScene s, UiFrame f, List<float[]> into)
    {
        for (int i = 0; i < s.ItemCount; i++)
        {
            ref UiItem it = ref s.Items[i];
            if ((it.Flags & UiFlags.Prompt) == 0 && it.Y >= f.BandY - 0.5f)
            {
                into.Add(new[] { it.X, it.Y, it.W, it.H });
            }
        }
    }

    // The tray: the app tiles of the bar's row (squares the toolbar's height on the strip), centred on the screen to the grid, as many as the plan says.
    private static string TrayOff(UiScene s, UiFrame f, UiStatusModel bar, string at)
    {
        float left = float.MaxValue, right = float.MinValue;
        int tiles = 0;
        for (int i = 0; i < s.ItemCount; i++)
        {
            ref UiItem it = ref s.Items[i];
            if (it.Kind == UiKind.Box && it.Parent >= 0 && Math.Abs(it.W - UiDraw.BarTile) < 0.01f && Math.Abs(it.H - UiDraw.BarTile) < 0.01f && (it.Fill == UiRole.Pill || it.Fill == UiRole.RowSel)
                && Math.Abs(it.Y + it.H / 2f - UiDraw.BarMiddle(f)) < 0.5f && s.Items[it.Parent].Fill == UiRole.StatusBar && it.X > f.Width * 0.2f && it.X + it.W < f.Width * 0.8f)
            {
                left = Math.Min(left, it.X);
                right = Math.Max(right, it.X + it.W);
                tiles++;
            }
        }
        int shown = UiDraw.AppsShown(bar, f.Width) + (bar.AllApps ? 1 : 0);
        if (tiles != shown)
        {
            return at + ": the row lays " + tiles + " tiles where the plan says " + shown;
        }
        float middle = (left + right) / 2f;
        return tiles == 0 || Math.Abs(middle - f.Width / 2f) <= UiTokens.Grid ? null : at + ": the tray's middle is at " + middle.ToString("0.#") + ", the screen's at " + (f.Width / 2f).ToString("0.#");
    }

    // The chips of a layout: how many, where, and whether any stands over what it must not. A set wider than a window's screen is ever given (the
    // HUD's nine numbered keys) wraps, and its upper rows may stand over a window's foot; every other rule holds for it.
    private static void KitChips(UiScene s, UiFrame f, int expected, string at, ref int chips, ref int wrapped, ref string over, ref string edge, ref string lost, ref string crowded,
        ref float above, ref float below, ref int caps)
    {
        var faces = new List<int>();
        int keyCaps = 0, words = 0;
        for (int i = 0; i < s.ItemCount; i++)
        {
            ref UiItem it = ref s.Items[i];
            if ((it.Flags & UiFlags.Prompt) == 0)
            {
                continue;
            }
            if (it.Parent < 0 && it.Kind == UiKind.Box)
            {
                faces.Add(i);
            }
            if (!f.Pointer && it.Y + it.H > f.BandY + 0.5f)
            {
                over = over ?? at + ": a " + it.Kind + " of a prompt stands on the bar's strip at " + it.X.ToString("0") + "," + it.Y.ToString("0");
            }
            keyCaps += it.Kind == UiKind.KeyCap ? 1 : 0;
            words += it.Kind == UiKind.Text && !string.IsNullOrEmpty(it.Text) ? 1 : 0;
        }
        if (f.Pointer)
        {
            crowded = crowded ?? (faces.Count == 0 ? null : at + ": " + faces.Count + " chips with the mouse leading");
            return;
        }
        bool window = expected <= WindowPrompts;
        chips += faces.Count;
        caps += keyCaps;
        if (faces.Count != expected || words != expected || keyCaps < expected)
        {
            lost = lost ?? at + ": " + faces.Count + " chips, " + words + " labels and " + keyCaps + " caps for " + expected + " prompts";
        }
        var rows = new List<float>();
        foreach (int c in faces)
        {
            ref UiItem chip = ref s.Items[c];
            if (!rows.Contains(chip.Y))
            {
                rows.Add(chip.Y);
            }
            if (chip.X < UiTokens.S4 - 0.5f || chip.X + chip.W > f.Width - UiTokens.S4 + 0.5f || chip.Y < 0f)
            {
                edge = edge ?? at + ": a chip at " + chip.X.ToString("0") + "," + chip.Y.ToString("0") + " (" + chip.W.ToString("0") + " wide) leaves the screen's " + f.Width.ToString("0");
            }
            below = Math.Min(below, f.BandY - (chip.Y + chip.H));
            for (int j = 0; j < s.ItemCount && over == null; j++)
            {
                ref UiItem o = ref s.Items[j];
                bool another = (o.Flags & UiFlags.Prompt) != 0;
                bool box = o.Kind == UiKind.Box && o.Parent < 0 && o.Clip < 0 && o.Fill != UiRole.Scrim && o.W > 0f && o.H > 0f;
                if (j == c || !box || (another && j < c) || (!window && !another && (o.Flags & UiFlags.Panel) != 0 && o.Y + o.H <= f.BandY - (UiTokens.Chip + UiTokens.S2 * 2f) + 0.5f))
                {
                    continue;
                }
                bool cross = chip.X < o.X + o.W - 0.5f && o.X < chip.X + chip.W - 0.5f && chip.Y < o.Y + o.H - 0.5f && o.Y < chip.Y + chip.H - 0.5f;
                if (cross)
                {
                    over = at + ": a chip at " + chip.X.ToString("0") + "," + chip.Y.ToString("0") + " (" + chip.W.ToString("0") + "x" + chip.H.ToString("0") + ") is over the "
                           + o.Fill + " box at " + o.X.ToString("0") + "," + o.Y.ToString("0") + " (" + o.W.ToString("0") + "x" + o.H.ToString("0") + ")";
                }
                else if (window && (o.Flags & UiFlags.Panel) != 0 && !another && o.Y + o.H <= chip.Y + 0.5f && o.X < chip.X + chip.W && o.X + o.W > chip.X)
                {
                    above = Math.Min(above, chip.Y - (o.Y + o.H));   // a panel standing straight over the chips: the window's foot
                }
            }
        }
        wrapped += Math.Max(0, rows.Count - 1);
    }

    // The plan takes no prompts, and the chips are placed and drawn once a layout, allocating and searching nothing.
    private static void KitPromptSources()
    {
        bool takesNone = typeof(UiPillPlan).GetField("Prompts") == null && typeof(UiPillPlan).GetField("PromptX") == null
                         && typeof(UiDraw).GetMethod("PlanPills").GetParameters().Length == 2;
        Check(takesNone, "kit prompts: the pill plan has no room for the prompts and takes none, so nothing they say can move the tray");
        string bar = ModSource(@"Ui\UiDraw.HudBar.cs");
        string place = MemberText(bar, "public static void KeyChipsPlace(");
        string draw = MemberText(bar, "public static void KeyChips(");
        string row = MemberText(bar, "private static int ChipsInRow(");
        Check(place.Length > 0 && draw.Length > 0 && row.Length > 0 && !place.Contains("new ") && !draw.Contains("new ") && !row.Contains("new ") && !place.Contains("Find") && !draw.Contains("Find")
              && !place.Contains("GetComponent") && !draw.Contains("Update") && !place.Contains("Physics"),
            "kit prompts: the chips are placed and drawn once a layout, allocating and searching nothing, and the status bar draws them only there");
        string chrome = MemberText(ModSource(@"Ui\UiDraw.Chrome.cs"), "public static void StatusBar(UiScene s, UiFrame frame, IUiKeys keys, bool prompts)");
        Check(chrome.Contains("KeyChips(s, ") && !chrome.Contains("Prompts(s, "), "kit prompts: the status bar sets the keys' prompts as chips and never on its own strip");
    }
}
