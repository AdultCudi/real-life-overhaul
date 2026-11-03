using System;
using System.Diagnostics;
using System.IO;
using RealLifeOverhaul;
using RealLifeOverhaul.Ui;

// The HUD's pieces and host (Scripts/Ui/UiHud*.cs, UiDraw.HudBar.cs, Scripts/Ui/Hud): the px scale under the game's UI
// zoom, the world prompt inside the screen above the bar at every frame of a camera's sweep, every pill whole on the bar's
// one centre line with the app row in every language at 1080, 1440, the zoom's 80% and 120% and the narrowest, the keys'
// hints as chips over the bar's right end clear of every other piece, the activity in each of its states, the money's
// changes merged per minute as the game merges them, no key taken while a piece is not focused, and what a frame costs.
internal static partial class Program
{
    private static void HudKitChecks()
    {
        int before = _run, failedBefore = _failed;
        var watch = Stopwatch.StartNew();
        HudScaleChecks();
        HudPromptChecks();
        HudPillChecks();
        HudActivityChecks();
        HudMoneyChecks();
        HudSourceChecks();
        HudCostChecks();
        KitMarkChecks();
        HudScreensChecks();   // the HUD's live pieces (UiChecks.HudScreens.cs)
        Console.WriteLine("  HUD kit: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // The UI zoom divides the game's 3840 wide canvas; the HUD grows with it until its layout would be narrower than it fits.
    private static void HudScaleChecks()
    {
        Check(UiHudRules.Scale(3840f) == UiTokens.CanvasPerPx && UiHudRules.Scale(3840f / 1.25f) == UiTokens.CanvasPerPx && UiHudRules.Scale(3840f / 1.5f) == UiTokens.CanvasPerPx,
            "hud: at the game's zoom up to 150% the HUD lays out at the kit's 2 canvas units a px, so it grows as the game's own HUD does");
        float at2 = 3840f / 2f / UiHudRules.Scale(3840f / 2f);
        Check(Math.Abs(at2 - UiHudRules.LeastWidth) < 0.01f, "hud: at the game's largest zoom (200%) the layout holds at " + UiHudRules.LeastWidth + " px wide (" + at2.ToString("0") + ")");
        Check(3840f * 2f / UiHudRules.Scale(3840f * 2f) == 3840f && UiHudRules.Scale(0f) == UiTokens.CanvasPerPx,
            "hud: the smallest zoom (50%) draws the HUD half size as the game's does, and a canvas not measured yet takes the kit's scale");
    }

    // A camera sweeping round a door and away from it: the box stays inside the screen above the bar on every frame, off the
    // left column whenever there is room beside it, and at the screen's foot on the point's side while the point is behind.
    private static void HudPromptChecks()
    {
        var door = UiSamples.HudDoor(UiWords("en"), UiDefaultKeys.Keyboard);
        var item = UiSamples.HudItem(UiWords("en"));
        var info = UiSamples.HudBusiness(UiWords("en"));
        int frames = 0, outside = 0, onPanel = 0, wrongSide = 0;
        foreach (float aspect in UiAspects)
        {
            UiFrame frame = UiFrame.FullMenu(aspect);
            foreach (UiWorldPromptModel model in new[] { door, item })
            {
                float w = UiWorldPrompt.Width, h = UiWorldPrompt.Height(model);
                var hud = new UiHudModel { Info = info, Prompt = model };
                UiHud.LeftColumn(hud, out float lx, out float ly, out float lw, out float lh);
                for (int i = 0; i < 720; i++)
                {
                    // Half a turn of the camera a second at 60 frames: the point crosses the screen, leaves it, goes behind.
                    double angle = i * Math.PI / 120.0;
                    float px = (float)(frame.Width / 2f + Math.Sin(angle) * frame.Width * 0.9f), py = (float)(frame.Height * 0.55f + Math.Cos(angle * 0.7) * frame.Height * 0.6f);
                    bool ahead = Math.Cos(angle) > -0.35;
                    UiHudRules.Beside(px, py, ahead, w, h, frame.Width, frame.BandY, lx, ly, lw, lh, out float x, out float y);
                    frames++;
                    outside += UiHudRules.Inside(x, y, w, h, frame.Width, frame.BandY) ? 0 : 1;
                    bool room = lx + lw + UiHudRules.Margin + w <= frame.Width - UiHudRules.Margin;
                    onPanel += room && UiHudRules.Overlaps(x, y, w, h, lx, ly, lw, lh) ? 1 : 0;
                    if (!ahead)
                    {
                        float mirrored = frame.Width - px;
                        wrongSide += (mirrored < frame.Width / 2f) != (x + w / 2f < frame.Width / 2f) && Math.Abs(mirrored - frame.Width / 2f) > w ? 1 : 0;
                    }
                }
            }
        }
        Check(outside == 0, "hud: the world prompt stands inside the screen, above the status bar, on every frame of a camera's sweep (" + outside + " of " + frames + " frames outside)");
        Check(onPanel == 0, "hud: the world prompt keeps off the left column's info panel whenever there is room beside it (" + onPanel + " frames on it)");
        Check(wrongSide == 0, "hud: a point behind the camera puts the box at the screen's foot on the side the point lies (" + wrongSide + " on the other side)");
        UiHudRules.Beside(-4000f, -4000f, true, 320f, 200f, 1920f, 980f, 0f, 0f, 0f, 0f, out float cx, out float cy);
        UiHudRules.Beside(9000f, 9000f, true, 320f, 200f, 1920f, 980f, 0f, 0f, 0f, 0f, out float fx, out float fy);
        Check(cx == UiHudRules.Margin && cy == UiHudRules.Margin && fx == 1920f - UiHudRules.Margin - 320f && fy == 980f - UiHudRules.Margin - 200f,
            "hud: a point far off the screen puts the box in the nearest corner, its margin kept");
        UiHudRules.ToLayout(960f, 540f, 1920f, 1080f, 1920f, 1080f, out float mx, out float my);
        UiHudRules.ToLayout(0f, 0f, 2560f, 1440f, 1920f, 1080f, out float bx, out float by);
        Check(mx == 960f && my == 540f && bx == 0f && by == 1080f, "hud: a camera's pixel (from the bottom left) becomes a px of the layout (from the top left) at any resolution");
    }

    // The strip's widths: 1080 (and 1440, which lays out the same), the game's UI zoom at 80% and 120%, an ultrawide, and the
    // narrowest the HUD lays out (a zoom of 150% and over).
    private static readonly float[] BarWidths = { 1920f, 1920f / 0.8f, 1920f / 1.2f, 2520f, UiHudRules.LeastWidth };

    // Every pill that stays is whole and none reaches the app row, in every language, paused or not, by day and at night, at
    // every width the strip takes; what gives way goes in its order, and at 1080 the bar keeps its apps and every pill.
    private static void HudPillChecks()
    {
        string crossed = null, lost = null, given = null, order = null;
        int widest = 0;
        foreach (string lang in ModLanguages.All)
        {
            UiWords t = UiWords(lang);
            foreach (bool night in new[] { false, true })
            {
                foreach (bool paused in new[] { false, true })
                {
                    UiStatusModel bar = UiSamples.HudBar(t, paused, false, night);
                    foreach (float width in BarWidths)
                    {
                        UiPillPlan p = UiDraw.PlanPills(bar, width);
                        string at = lang + " at " + width.ToString("0") + (paused ? " paused" : "") + (night ? " at night" : "");
                        float left = UiTokens.S4 + p.Time + (p.Weather > 0f ? UiTokens.S2 + p.Weather : 0f) + (p.Place > 0f ? UiTokens.S2 + p.Place : 0f);
                        float right = p.MoneyX - (p.Alerts > 0f ? p.Alerts + UiTokens.S2 : 0f) - (p.Needs > 0f ? p.Needs + UiTokens.S2 : 0f);
                        if ((left + UiTokens.S6 > p.RowX + 0.01f || p.RowX + p.RowW + UiTokens.S6 > right + 0.01f) && crossed == null)
                        {
                            crossed = at;
                        }
                        if ((p.Time == 0f || !p.Day || p.Money == 0f || p.Needs == 0f || p.Settings == 0f || (width >= 1600f && p.Alerts == 0f)
                             || Math.Abs(p.MoneyX + p.Money + UiTokens.S2 + p.Settings - (width - UiTokens.S4)) > 0.01f) && lost == null)
                        {
                            lost = at;
                        }
                        if (width == 1920f && !night && (p.Apps < bar.Apps.Length || p.Weather == 0f || !p.Words || p.Place == 0f || !p.District || p.Meter < 36f || p.Alerts == 0f || !p.MoneyDay)
                            && given == null)
                        {
                            given = at + " (" + p.Apps + " apps" + (p.Weather == 0f ? ", no weather" : !p.Words ? ", no weather words" : "") + (p.Place == 0f ? ", no place" : !p.District ? ", no district" : "")
                                    + (p.Meter < 36f ? ", short meters" : "") + (p.Alerts == 0f ? ", no bell" : "") + (!p.MoneyDay ? ", no money's day" : "") + ")";
                        }
                        if (((p.Place == 0f && p.District) || (!p.Day && (p.Place > 0f || p.Weather > 0f)) || (p.Weather == 0f && p.Words) || (!p.MoneyDay && (p.Needs > 0f || p.Alerts > 0f)))
                            && order == null)
                        {
                            order = at;
                        }
                        widest = width == 1920f ? Math.Max(widest, (int)left) : widest;
                    }
                }
            }
        }
        Check(crossed == null, "hud: the left and the right pills keep a gap from the app row, whatever the language, the width or the state" + (crossed == null ? "" : " (" + crossed + ")"));
        Check(lost == null, "hud: the time with its day, the needs, the money and the gear at the strip's corner stay at every width down to the narrowest, the bell down to the UI zoom's 120%"
                            + (lost == null ? "" : " (" + lost + ")"));
        Check(given == null, "hud: at 1080 by day the bar keeps every app and every pill whole, each with its second line, and the needs' full meters (the widest left group " + widest + " px)"
                             + (given == null ? "" : " (" + given + ")"));
        Check(order == null, "hud: on a narrower strip the district goes before the place, the weather's words before the weather, the day last, and the money's day only after the needs and the bell"
                             + (order == null ? "" : " (" + order + ")"));
        // At the narrowest the row hands its apps to All apps, down to four, before the needs give way.
        UiStatusModel full = UiSamples.HudBar(UiWords("en"), false, false, false);
        UiPillPlan narrow = UiDraw.PlanPills(full, UiHudRules.LeastWidth), wide = UiDraw.PlanPills(full, 1920f);
        Check(narrow.Apps >= 4 && narrow.Apps < wide.Apps && narrow.Needs > 0f && narrow.Meter <= wide.Meter,
            "hud: on the narrowest strip the row keeps " + narrow.Apps + " of " + wide.Apps + " apps, the rest under All apps, so the needs stay");
        full.Hygiene = full.Stress = full.HomeNeed = -1;
        UiPillPlan three = UiDraw.PlanPills(full, 1920f);
        Check(three.Needs == UiTokens.S3 * 2f + 3f * three.Meter + 2f * UiTokens.S2 && wide.Needs == UiTokens.S3 * 2f + 6f * wide.Meter + 5f * UiTokens.S2,
            "hud: the needs' pill holds a cell for each need it is given, the game's three alone or with the mod's three");
        var empty = new UiStatusModel();
        UiPillPlan none = UiDraw.PlanPills(empty, 1920f);
        Check(none.Time == 0f && none.Weather == 0f && none.Place == 0f && none.Money == 0f && none.Needs == 0f && none.Alerts == 0f && none.Settings == 0f && none.RowW == 0f,
            "hud: a field left empty leaves its pill out, and a bar with no apps has no row");
        // The pills are targets only on the HUD's own bar, so no window's page ever meets their commands.
        var s = new UiScene();
        UiStatusModel shown = UiSamples.HudBar(UiWords("en"), false, false, false);
        Func<UiStatusModel> was = UiChrome.Bar;
        UiChrome.Bar = () => shown;
        try
        {
            s.Begin("bar", 1920f, 1080f, true);
            UiDraw.StatusBar(s, UiFrame.FullMenu(16f / 9f), UiDefaultKeys.Keyboard);
            int pills = 0;
            for (int i = 0; i < s.NodeCount; i++)
            {
                pills += UiDraw.IsPill(s.Nodes[i].Command) ? 1 : 0;
            }
            shown.PillsPress = false;
            s.Begin("bar", 1920f, 1080f, true);
            UiDraw.StatusBar(s, UiFrame.FullMenu(16f / 9f), UiDefaultKeys.Keyboard);
            int elsewhere = 0;
            for (int i = 0; i < s.NodeCount; i++)
            {
                elsewhere += UiDraw.IsPill(s.Nodes[i].Command) ? 1 : 0;
            }
            Check(pills == 6 && elsewhere == 0,
                "hud: the pause and the play, the needs, the bell, the money and the gear are targets on the HUD's bar (" + pills + ") and pictures on every other bar (" + elsewhere + ")");
        }
        finally
        {
            UiChrome.Bar = was;
        }
        HudBarLineChecks();
        HudSquareChecks();
        HudChipChecks();
    }

    // The time's squares as the game's speed buttons (the bar the owner approved, 2 Nov): the one in force on the cyan,
    // the pause and the play pressable and the fast forward only shown, all three dim and inert while the game holds the
    // clock; and the gear's own command at the strip's corner.
    private static void HudSquareChecks()
    {
        string wrong = null;
        Func<UiStatusModel> was = UiChrome.Bar;
        try
        {
            foreach (int state in new[] { 0, 1, 2, 3 })   // walking, paused, fast-forwarding, the clock held
            {
                UiStatusModel bar = UiSamples.HudBar(UiWords("en"), state == 1, state == 2, false, state == 3);
                UiChrome.Bar = () => bar;
                var s = new UiScene();
                UiFrame frame = UiFrame.FullMenu(16f / 9f);
                s.Begin("bar", frame.Width, frame.Height, true);
                UiDraw.StatusBar(s, frame, UiDefaultKeys.Keyboard);
                int lit = -1, squares = 0, dim = 0;
                for (int i = 0; i < s.ItemCount; i++)
                {
                    ref UiItem it = ref s.Items[i];
                    if (it.Kind != UiKind.Icon || (it.Icon != UiIcon.Pause && it.Icon != UiIcon.Play && it.Icon != UiIcon.FastForward))
                    {
                        continue;
                    }
                    squares++;
                    dim += (it.Flags & UiFlags.Disabled) != 0 ? 1 : 0;
                    if (s.Items[it.Parent].Fill == UiRole.Accent)
                    {
                        lit = it.Icon == UiIcon.Pause ? 0 : it.Icon == UiIcon.Play ? 1 : 2;
                    }
                }
                int pause = s.FindNode(UiDraw.TimePillCommand, 0), play = s.FindNode(UiDraw.TimePillCommand, 1), gear = s.FindNode(UiDraw.SettingsPillCommand, 0);
                bool locked = state == 3;
                bool right = squares == 3 && lit == (state == 1 ? 0 : state == 2 ? 2 : 1) && pause >= 0 && play >= 0 && gear >= 0
                             && s.Nodes[pause].Disabled == locked && s.Nodes[play].Disabled == locked && dim == (locked ? 3 : state == 2 ? 0 : 1)
                             && s.Nodes[gear].X > s.Nodes[s.FindNode(UiDraw.MoneyPillCommand, 0)].X;
                if (!right && wrong == null)
                {
                    wrong = "state " + state + ": " + squares + " squares, the lit one " + lit + ", " + dim + " dim, pause " + pause + ", play " + play + ", gear " + gear;
                }
            }
        }
        finally
        {
            UiChrome.Bar = was;
        }
        Check(wrong == null, "hud: the time's squares light the game's pause, play or fast forward in force, only the pause and the play press, the held clock dims all three, and the gear ends the strip"
                             + (wrong == null ? "" : " (" + wrong + ")"));
    }

    // The game's control hints on the HUD as key chips (the bar the owner approved, 2 Nov: small key chips at the bottom
    // right, never a second strip): with the keys they stand over the bar's right end a step above it, inside the screen
    // and over nothing else of the HUD's (the money's change, the activity's strip, a tool's panel, the world prompt); the
    // bar itself then holds no prompt; with the mouse there are none.
    private static void HudChipChecks()
    {
        var frames = new[]
        {
            UiFrame.FullMenu(16f / 9f), new UiFrame { Width = 1920f / 0.8f, Height = 1080f / 0.8f, Band = UiFrame.BandHeight },
            new UiFrame { Width = 1920f / 1.2f, Height = 1080f / 1.2f, Band = UiFrame.BandHeight }, UiFrame.FullMenu(21f / 9f),
            new UiFrame { Width = UiHudRules.LeastWidth, Height = 720f, Band = UiFrame.BandHeight },
        };
        var scenes = new Action<UiScene, UiFrame, IUiKeys, UiWords>[]
        {
            (s, f, k, t) => UiSamples.HudBarScene(s, f, true, k, t, false, false, false), (s, f, k, t) => UiSamples.HudActivityScene(s, f, true, k, t, UiActivityState.Running),
            (s, f, k, t) => UiSamples.HudTools(s, f, true, k, t, true), (s, f, k, t) => UiSamples.HudPrompt(s, f, true, k, t, false),
        };
        string wrong = null;
        int laid = 0, chips = 0;
        foreach (string lang in ModLanguages.All)
        {
            UiWords t = UiWords(lang);
            foreach (UiFrame f in frames)
            {
                foreach (int input in new[] { 0, 1, 2 })   // the mouse, the keyboard, a pad
                {
                    for (int n = 0; n < scenes.Length; n++)
                    {
                        UiFrame frame = f;
                        frame.Pointer = input == 0;
                        var s = new UiScene();
                        scenes[n](s, frame, input == 2 ? UiDefaultKeys.XboxPad : UiDefaultKeys.Keyboard, t);
                        laid++;
                        string at = lang + " " + frame.Width.ToString("0") + (input == 0 ? " mouse" : input == 1 ? " keys" : " pad") + " scene " + n;
                        int found = 0;
                        for (int i = 0; i < s.ItemCount && wrong == null; i++)
                        {
                            ref UiItem it = ref s.Items[i];
                            if ((it.Flags & UiFlags.Prompt) == 0 || it.Parent >= 0)
                            {
                                continue;   // a chip's face; its cap and its words stand inside it
                            }
                            found++;
                            if (it.X < UiTokens.S4 - 0.5f || it.X + it.W > frame.Width - UiTokens.S4 + 0.5f || it.Y + it.H > frame.BandY - UiTokens.S3 + 0.5f || it.Y < 0f)
                            {
                                wrong = at + ": a chip at " + it.X.ToString("0") + "," + it.Y.ToString("0") + " stands outside the screen's room over the bar";
                            }
                            for (int j = 0; j < s.ItemCount && wrong == null; j++)
                            {
                                ref UiItem o = ref s.Items[j];
                                if (j == i || o.Parent >= 0 || o.Kind != UiKind.Box || (o.Flags & (UiFlags.Prompt | UiFlags.Decor)) != 0)
                                {
                                    continue;
                                }
                                if (UiHudRules.Overlaps(it.X, it.Y, it.W, it.H, o.X + 0.5f, o.Y + 0.5f, o.W - 1f, o.H - 1f))
                                {
                                    wrong = at + ": a chip stands over the piece at " + o.X.ToString("0") + "," + o.Y.ToString("0") + " (" + o.Fill + ")";
                                }
                            }
                        }
                        chips += found;
                        if (wrong == null && (input == 0) != (found == 0))
                        {
                            wrong = at + ": " + found + " chips " + (input == 0 ? "with the mouse leading" : "with the keys leading");
                        }
                    }
                }
            }
        }
        Check(wrong == null && chips > 0, "hud: the keys' hints stand as chips over the bar's right end, inside the screen and clear of every other piece, and none with the mouse ("
                                          + laid + " layouts, " + chips + " chips)" + (wrong == null ? "" : " (" + wrong + ")"));
    }

    // The strip's one line (the owner, 2 Nov: "the alignment is kinda off", "Day 2," cut): every pill, app tile and prompt
    // centred on the bar's centre line within a px, no pill's words wider than their room or crossing its edge, nothing on
    // the strip over anything else, laid out whole by UiDraw.StatusBar at 1920 x 1080 (2560 x 1440 lays out the same), the UI
    // zoom's 80% and 120%, an ultrawide and the narrowest, with the mouse and with the keys' prompts, in every language.
    private static void HudBarLineChecks()
    {
        var frames = new[]
        {
            UiFrame.FullMenu(16f / 9f), new UiFrame { Width = 1920f / 0.8f, Height = 1080f / 0.8f, Band = UiFrame.BandHeight },
            new UiFrame { Width = 1920f / 1.2f, Height = 1080f / 1.2f, Band = UiFrame.BandHeight }, UiFrame.FullMenu(21f / 9f),
            new UiFrame { Width = UiHudRules.LeastWidth, Height = 720f, Band = UiFrame.BandHeight },
        };
        string off = null, cut = null, over = null;
        int bars = 0, pieces = 0;
        Func<UiStatusModel> was = UiChrome.Bar;
        try
        {
            foreach (string lang in ModLanguages.All)
            {
                UiWords t = UiWords(lang);
                foreach (bool night in new[] { false, true })
                {
                    foreach (int input in new[] { 0, 1, 2 })   // the mouse, the keyboard's prompts, a pad's (All apps on View)
                    {
                        UiStatusModel bar = UiSamples.HudBar(t, night, false, night);
                        UiChrome.Bar = () => bar;
                        foreach (UiFrame f in frames)
                        {
                            UiFrame frame = f;
                            frame.Pointer = input == 0;
                            var s = new UiScene();
                            s.Begin("bar", frame.Width, frame.Height, true);
                            s.AddPrompt(UiAction.Move, t("reallife_ui_prompt_move", "Move"));
                            s.AddPrompt(UiAction.Back, t("input_key_menu", "Menu"));
                            UiDraw.StatusBar(s, frame, input == 2 ? UiDefaultKeys.XboxPad : UiDefaultKeys.Keyboard);
                            bars++;
                            string at = lang + " " + frame.Width.ToString("0") + " x " + frame.Height.ToString("0") + (input == 0 ? " mouse" : input == 1 ? " keys" : " pad") + (night ? " night" : "");
                            float mid = UiDraw.BarMiddle(frame);
                            int strip = -1;
                            for (int i = 0; i < s.ItemCount && strip < 0; i++)
                            {
                                strip = s.Items[i].Fill == UiRole.StatusBar ? i : -1;
                            }
                            for (int i = 0; i < s.ItemCount; i++)
                            {
                                ref UiItem it = ref s.Items[i];
                                bool piece = it.Parent == strip && it.Kind == UiKind.Box && it.Fill != UiRole.Line;   // a pill or a tile
                                bool prompt = (it.Flags & UiFlags.Prompt) != 0 && (it.Kind == UiKind.KeyCap || it.Kind == UiKind.Text);
                                if (piece || prompt)
                                {
                                    pieces++;
                                    if (Math.Abs(it.Y + it.H / 2f - mid) > 1f && off == null)
                                    {
                                        off = at + ": " + (prompt ? "a prompt" : it.Fill == UiRole.Pill ? "a pill" : "a tile") + " centred at " + (it.Y + it.H / 2f).ToString("0.#") + ", the line at " + mid.ToString("0.#");
                                    }
                                }
                                if (it.Kind == UiKind.Text && !string.IsNullOrEmpty(it.Text) && UiTextRules.Width(it.Text, it.Style) > it.W + 0.5f && cut == null)
                                {
                                    cut = at + ": \"" + it.Text + "\" is " + UiTextRules.Width(it.Text, it.Style).ToString("0.#") + " px in " + it.W.ToString("0.#");
                                }
                                int pill = Pill(s, i, strip);
                                if (pill >= 0 && pill != i && (it.Flags & UiFlags.Overflow) == 0 && (it.X < s.Items[pill].X - 0.5f || it.X + it.W > s.Items[pill].X + s.Items[pill].W + 0.5f)
                                    && cut == null)
                                {
                                    cut = at + ": " + (it.Kind == UiKind.Text ? "\"" + it.Text + "\"" : it.Kind.ToString()) + " crosses its pill's edge";
                                }
                            }
                            over = over ?? Crossing(s, strip, at);
                        }
                    }
                }
            }
        }
        finally
        {
            UiChrome.Bar = was;
        }
        Check(off == null && pieces > 0, "hud: the pills, the app tiles and the prompts share the strip's one centre line within 1 px (" + bars + " bars, " + pieces + " pieces)" + (off == null ? "" : " (" + off + ")"));
        Check(cut == null, "hud: no pill's words are wider than their room or cross the pill's edge, at 1080, 1440 and the UI zoom's 80% and 120%" + (cut == null ? "" : " (" + cut + ")"));
        Check(over == null, "hud: nothing on the strip stands over anything else: the pills, the tiles and the prompts each keep their own room" + (over == null ? "" : " (" + over + ")"));
    }

    // The pill an item is drawn in (the strip's child with the pill's fill it descends from), or -1.
    private static int Pill(UiScene s, int item, int strip)
    {
        int a = item;
        for (int guard = 0; a >= 0 && guard < 16; guard++)
        {
            if (s.Items[a].Parent == strip)
            {
                return s.Items[a].Fill == UiRole.Pill ? a : -1;
            }
            a = s.Items[a].Parent;
        }
        return -1;
    }

    // The first two of the strip's pieces (its pills and tiles, each prompt's cap with its words) that overlap, or null.
    private static string Crossing(UiScene s, int strip, string at)
    {
        var boxes = new System.Collections.Generic.List<float[]>();
        float px = -1f, pr = -1f;
        for (int i = 0; i < s.ItemCount; i++)
        {
            ref UiItem it = ref s.Items[i];
            if (it.Parent == strip && it.Kind == UiKind.Box && it.Fill != UiRole.Line)
            {
                boxes.Add(new[] { it.X, it.X + it.W });
            }
            else if ((it.Flags & UiFlags.Prompt) != 0)
            {
                px = px < 0f ? it.X : Math.Min(px, it.X);
                pr = Math.Max(pr, it.X + it.W);
            }
        }
        if (pr > px && px >= 0f)
        {
            boxes.Add(new[] { px, pr });   // the prompts as one run: their caps and words sit together
        }
        for (int a = 0; a < boxes.Count; a++)
        {
            for (int b = a + 1; b < boxes.Count; b++)
            {
                if (boxes[a][0] < boxes[b][1] - 0.5f && boxes[b][0] < boxes[a][1] - 0.5f)
                {
                    return at + ": " + boxes[a][0].ToString("0") + " to " + boxes[a][1].ToString("0") + " and " + boxes[b][0].ToString("0") + " to " + boxes[b][1].ToString("0");
                }
            }
        }
        return null;
    }

    // The activity in each of its states: the card before it starts, the strip above the bar after, its buttons as the state has them.
    private static void HudActivityChecks()
    {
        var s = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f);
        string wrong = null;
        foreach (UiActivityState state in (UiActivityState[])Enum.GetValues(typeof(UiActivityState)))
        {
            UiActivityModel a = UiSamples.HudActivity(UiWords("en"), state, UiDefaultKeys.Keyboard);
            if (state == UiActivityState.NotStarted)
            {
                UiActivity.LayCard(s, a, frame, true, UiDefaultKeys.Keyboard);
                bool start = s.FindNode(UiActivity.ButtonCommand, 1) >= 0, cancel = s.FindNode(UiActivity.ButtonCommand, 0) >= 0, steps = s.FindNode(UiActivity.StepCommand, 0) >= 0;
                int starter = s.FindNode(UiActivity.ButtonCommand, 1);
                if (!start || !cancel || !steps || s.Nodes[starter].Key != UiAction.Alt || UiActivity.Strip(state))
                {
                    wrong = "before it starts: Start on the game's Interact, Cancel and the duration's slider";
                }
                continue;
            }
            s.Begin("strip", frame.Width, frame.Height, true);
            UiActivity.StripPlace(frame, a, out float x, out float y);
            UiActivity.DrawStrip(s, UiAt.Screen, a, x, y);
            bool skip = s.FindNode(UiActivity.SkipCommand, 0) >= 0, fast = s.FindNode(UiActivity.FastCommand, 0) >= 0, out1 = s.FindNode(UiActivity.CancelCommand, 0) >= 0;
            bool walking = state == UiActivityState.Moving || state == UiActivityState.Waiting, done = state == UiActivityState.Finished;
            bool right = UiActivity.Strip(state) && y + UiActivity.Height(a) <= frame.BandY - UiTokens.S2 && Math.Abs(x + a.StripW / 2f - frame.Width / 2f) <= UiTokens.Grid
                         && (walking ? out1 && !skip && !fast : done ? !skip && !fast && !out1 : skip && fast && !out1);
            if (!right && wrong == null)
            {
                wrong = state + ": the strip centred above the bar with " + (walking ? "the way out" : done ? "no buttons" : "the time machine and fast forward");
            }
        }
        UiSamples.HudPickerScene(s, frame, true, UiDefaultKeys.XboxPad, UiWords("en"), false, true);
        int first = UiFocusRules.First(s);
        Check(first >= 0 && s.Nodes[first].Command == UiPicker.EmptyCommand, "hud: an empty picker starts the keys on its own way out, never on the bar below it");
        Check(wrong == null, "hud: the activity's every state: the card before it starts, then the strip centred above the bar with its own buttons" + (wrong == null ? "" : " (" + wrong + ")"));
    }

    // The game's top bar merges its floating money texts by sign within a minute of its clock (Topbar.Update): so does the bar.
    private static void HudMoneyChecks()
    {
        var flow = new UiMoneyFlow();
        flow.Feed(1000.0, 600);
        Check(flow.Waiting == 0, "hud: the first money read is where the money stands, not a change");
        flow.Feed(1012.5, 600);
        flow.Feed(1020.0, 600);
        flow.Feed(1020.2, 600);
        flow.Feed(990.0, 600);
        flow.Feed(1090.0, 601);
        Check(flow.Waiting == 3 && flow.Next(out double a) && Math.Abs(a - 20.0) < 1e-6 && flow.Next(out double b) && Math.Abs(b + 30.0) < 1e-6 && flow.Next(out double c) && Math.Abs(c - 100.0) < 1e-6
              && !flow.Next(out double _),
            "hud: changes of one sign in one minute are one (+$20), a change of the other sign or another minute is the next (-$30, +$100), and noise under half a cent is none");
        for (int i = 0; i < UiMoneyFlow.Most + 5; i++)
        {
            flow.Feed(2000.0 + (i % 2 == 0 ? i * 10.0 : -i), 700 + i);
        }
        double sum = 0;
        int taken = 0;
        while (flow.Next(out double x))
        {
            sum += x;
            taken++;
        }
        Check(taken == UiMoneyFlow.Most && Math.Abs(sum - (Last(UiMoneyFlow.Most + 5) - 1090.0)) < 1e-6,
            "hud: a flood of changes keeps at most " + UiMoneyFlow.Most + " waiting and the last carries the rest, so the sum stays true");
        flow.Reset();
        flow.Feed(5.0, 1);
        Check(flow.Waiting == 0, "hud: a new save's money is not read as a change");
        bool rises = true, fades = true;
        for (float t = 0f; t < UiHudRules.ChangeTime; t += 0.05f)
        {
            rises &= UiHudRules.ChangeLift(t + 0.05f) >= UiHudRules.ChangeLift(t);
            fades &= UiHudRules.ChangeAlpha(t + 0.05f) <= UiHudRules.ChangeAlpha(t);
        }
        Check(rises && fades && UiHudRules.ChangeLift(0.3f) == 0f && UiHudRules.ChangeAlpha(0.3f) == 1f && UiHudRules.ChangeAlpha(UiHudRules.ChangeTime) == 0f
              && !UiHudRules.ChangeShows(UiHudRules.ChangeTime) && Math.Abs(UiHudRules.ChangeLift(UiHudRules.ChangeTime) - UiHudRules.ChangeRise) < 0.01f,
            "hud: the money's change rests, then rises " + UiHudRules.ChangeRise + " px and fades out over " + UiHudRules.ChangeTime + " s, as the game's floating money lasts");
    }

    // The money the flood leaves: the last fed value, the start of the sum.
    private static double Last(int count)
    {
        int i = count - 1;
        return 2000.0 + (i % 2 == 0 ? i * 10.0 : -i);
    }

    // No key taken while a piece is not focused: the HUD's view and its followers read no input at all; only the modal piece's
    // UiHost does, and only while it shows. The follower costs one projection a frame; the pills' signature allocates nothing.
    private static void HudSourceChecks()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui\Hud"));
        string host = File.ReadAllText(System.IO.Path.Combine(folder, "HudHost.cs"));
        string screen = File.ReadAllText(System.IO.Path.Combine(folder, "HudScreen.cs"));
        string game = File.ReadAllText(System.IO.Path.Combine(folder, "HudStatusGame.cs"));
        bool reads = false;
        foreach (string code in new[] { host, screen })
        {
            reads |= code.Contains(".Read(") || code.Contains("TakeBack") || code.Contains("TakeAll") || code.Contains("Input.Get") || code.Contains("Keyboard.current")
                     || code.Contains("Gamepad.current") || code.Contains("PlayerAction.");
        }
        Check(!reads, "hud: the HUD's view and its world prompts read no key and take none from the game: the player walks with them");
        Check(host.Contains("_modal.Bind(screen, UiGame.Instance") && host.Contains("_modal.Hide();"), "hud: only a modal piece takes the keys, through the kit's host, and gives them back when it closes");
        Check(Occurrences(host, "WorldToScreenPoint(") == 1 && host.Contains("if (at != f.At)"), "hud: a world prompt costs one projection a frame and a move only when its place changed");
        Check(host.Contains("HotSpots.Time(HotUpdate)") && host.Contains("Guard.Fail(FrameGuard, e, this)") && host.Contains("if (FrameGuard.Off)"),
            "hud: the host's frame is timed and guarded, and a fault takes the HUD away rather than the game's frame");
        int sig = game.IndexOf("public static int Signature()", StringComparison.Ordinal), end = game.IndexOf("private static int Whole", StringComparison.Ordinal);
        Check(sig > 0 && end > sig && !game.Substring(sig, end - sig).Contains("new ") && !game.Substring(sig, end - sig).Contains("+ \""),
            "hud: the pills' signature, read every frame while the HUD shows, allocates nothing");
        string over = File.ReadAllText(System.IO.Path.Combine(folder, "..", "Host", "UiOverCanvas.cs"));
        Check(host.Contains("UiOverCanvas.Make(uis != null ? uis.playerHUD : null") && over.Contains("GetComponentsInParent<Canvas>(true)") && over.Contains("UiBindRules.Sorter(")
              && over.Contains("overrideSorting = true") && over.Contains("UiHostRules.Above("),
            "hud: the host nests in the game's HUD canvas (its scaler and UI zoom) through the host's library, walking switched off canvases too, sorted just above the HUD");
        Check(host.Contains("!UiScreenSwitches.On(\"hud\")") && host.Contains("!UiDirector.On"), "hud: the interface's switch or the HUD's own turned off takes the HUD away at once");
    }

    // Two kit fixes the HUD's bar and a chart needed (the econoview review): nothing on the status bar glares near white, its
    // news dots included; and a chart's axis never holds its end label in so far that it crowds its neighbour.
    private static void KitMarkChecks()
    {
        var s = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f);
        UiStatusModel bar = UiSamples.HudBar(UiWords("en"), false, false, false);
        Func<UiStatusModel> was = UiChrome.Bar;
        UiChrome.Bar = () => bar;
        string glare = null;
        int marks = 0;
        try
        {
            s.Begin("bar", frame.Width, frame.Height, true);
            UiDraw.StatusBar(s, frame, UiDefaultKeys.Keyboard);
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                if (it.Kind != UiKind.Box || it.Y < frame.BandY - 0.5f || it.Fill == UiRole.None)
                {
                    continue;
                }
                marks += (it.Flags & UiFlags.Overflow) != 0 ? 1 : 0;
                if (UiLightness(OverPill(UiTokens.Colour(it.Fill))) > 85f && glare == null)
                {
                    glare = it.Fill + " at " + it.X.ToString("0") + "," + it.Y.ToString("0");
                }
            }
        }
        finally
        {
            UiChrome.Bar = was;
        }
        Check(glare == null && marks > 0, "kit: no mark on the status bar is near white, the counts' dots on its tiles included (" + (glare ?? marks / 2 + " dots") + ")");
        string crowded = null;
        string[] words = { "Mar", "Day 412", "Week 12, Tuesday", "Day 410 morning" };
        for (int n = 3; n <= 14 && crowded == null; n++)
        {
            foreach (string word in words)
            {
                var points = new string[n];
                var values = new float[n];
                for (int i = 0; i < n; i++)
                {
                    points[i] = word;
                    values[i] = (i * 7) % 5;
                }
                var chart = new UiChartModel { Kind = UiChartKind.Line, Title = "Cash", Points = points, Series = new[] { new UiSeries { Label = "Cash", Values = values } }, Format = v => v.ToString("0") };
                for (float width = 300f; width <= 1200f && crowded == null; width += 20f)
                {
                    s.Begin("chart", 1920f, 1080f, true);
                    UiChart.Lay(s, UiAt.Screen, chart, 0f, 0f, width, 240f);
                    float lastRight = float.MinValue;
                    for (int i = 0; i < s.ItemCount; i++)
                    {
                        ref UiItem it = ref s.Items[i];
                        if (it.Kind == UiKind.Text && it.Ink == UiRole.Muted && it.Style == UiStyle.Small && (it.Flags & UiFlags.Centre) != 0)   // an axis label, cut or whole
                        {
                            if (it.X < lastRight + UiTokens.S2)   // a grid step of air between two labels at least
                            {
                                crowded = n + " points of \"" + word + "\" at " + width + " px: a label starts at " + it.X.ToString("0") + " against its neighbour's end " + lastRight.ToString("0");
                            }
                            lastRight = it.X + it.W;
                        }
                    }
                }
            }
        }
        Check(crowded == null, "kit: a chart's axis labels never crowd one another; a label held in at an end that would is dropped (" + (crowded ?? "none crowded") + ")");
    }

    // A colour as it shows on a pill of the bar: a see-through role (a meter's track) laid over the pill's slate.
    private static uint OverPill(uint rgba)
    {
        uint pill = UiTokens.Colour(UiRole.Pill);
        float a = (rgba & 0xFF) / 255f;
        uint Mix(int shift) => (uint)Math.Round(((rgba >> shift) & 0xFF) * a + ((pill >> shift) & 0xFF) * (1f - a));
        return (Mix(24) << 24) | (Mix(16) << 16) | (Mix(8) << 8) | 0xFF;
    }

    // What a frame of the HUD costs in its game-free parts, measured: placing a world prompt, the money's change, and the
    // flow fed; each must stay under a microsecond and allocate nothing. A layout of the busiest HUD, which runs only on a change.
    private static void HudCostChecks()
    {
        var flow = new UiMoneyFlow();
        const int n = 200000;
        float sink = 0f;
        for (int i = 0; i < 1000; i++)
        {
            UiHudRules.Beside(i, i, true, 320f, 200f, 1920f, 980f, 8f, 16f, 504f, 600f, out float wx, out float wy);
            sink += wx + wy;
        }
        var watch = Stopwatch.StartNew();
        long bytes = GC.GetAllocatedBytesForCurrentThread();   // after the watch, which is the measure's own
        for (int i = 0; i < n; i++)
        {
            UiHudRules.ToLayout(i % 2560, i % 1440, 2560f, 1440f, 1920f, 1080f, out float px, out float py);
            UiHudRules.Beside(px, py, (i & 7) != 0, 320f, 200f, 1920f, 980f, 8f, 16f, 504f, 600f, out float x, out float y);
            sink += x + y + UiHudRules.ChangeLift((i % 400) * 0.01f) + UiHudRules.ChangeAlpha((i % 400) * 0.01f);
            flow.Feed(1000.0 + (i % 3), i / 60);
            if ((i & 15) == 0)
            {
                flow.Next(out double _);
            }
        }
        watch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
        double each = watch.Elapsed.TotalMilliseconds * 1000.0 / n;
        Check(allocated == 0, "hud: a frame's game-free work (a prompt placed, the money's change, the flow fed) allocates nothing (" + allocated + " bytes over " + n + " frames)");
        Check(each < 1.0 && !float.IsNaN(sink), "hud: a frame's game-free work takes " + each.ToString("0.000") + " microseconds (under 1)");
        var s = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f);
        UiWords t = UiWords("en");
        UiSamples.HudSide(s, frame, true, UiDefaultKeys.Keyboard, t, false);
        var lay = Stopwatch.StartNew();
        const int layouts = 200;
        for (int i = 0; i < layouts; i++)
        {
            UiSamples.HudSide(s, frame, true, UiDefaultKeys.Keyboard, t, false);
        }
        lay.Stop();
        double ms = lay.Elapsed.TotalMilliseconds / layouts;
        Check(ms < 2.0, "hud: laying out the busiest HUD (side panels, the bar) takes " + ms.ToString("0.000") + " ms, and only when it changes (under 2)");
    }
}
