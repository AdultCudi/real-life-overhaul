using System;
using System.Diagnostics;
using System.IO;
using RealLifeOverhaul;
using RealLifeOverhaul.Ui;

// The HUD's pieces and host (w0-hudkit; Scripts/Ui/UiHud*.cs, UiDraw.HudBar.cs, Scripts/Ui/Hud): the px scale under the
// game's UI zoom, the world prompt inside the screen above the bar at every frame of a camera's sweep, every pill whole on
// the bar in every language at 1080, 720 and the narrowest zoom, the activity in each of its states, the money's changes
// merged per minute as the game merges them, no key taken while a piece is not focused, and what a frame costs.
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

    // Every pill that stays is whole and none crosses another, in every language, on the bar at 1080 and 720 (one layout, the
    // canvas scales) and at the narrowest the UI zoom leaves it; the least needed go first.
    private static void HudPillChecks()
    {
        string longest = null, cut = null, crossed = null;
        float most = 0f;
        foreach (string lang in ModLanguages.All)
        {
            UiWords t = UiWords(lang);
            foreach (bool night in new[] { false, true })
            {
                foreach (bool paused in new[] { false, true })
                {
                    UiStatusModel bar = UiSamples.HudBar(t, paused, false, night);
                    foreach (float width in new[] { 1920f, 2520f, UiHudRules.LeastWidth })
                    {
                        UiPillPlan p = UiDraw.PlanPills(bar, width);
                        float left = UiTokens.S4 + p.Time + (p.Weather > 0f ? UiTokens.S2 + p.Weather : 0f) + (p.Place > 0f ? UiTokens.S2 + p.Place : 0f);
                        if (left + UiTokens.S8 > p.MoneyX + 0.01f && crossed == null)
                        {
                            crossed = lang + " at " + width + (paused ? " paused" : "") + (night ? " at night" : "");
                        }
                        if ((p.Time == 0f || p.Money == 0f || p.Needs == 0f || p.Weather == 0f || p.Alerts == 0f) && cut == null)
                        {
                            cut = lang + " at " + width;
                        }
                        if (width == 1920f && (!p.Day || !p.Temperature || p.Place == 0f) && cut == null)
                        {
                            cut = lang + " at 1920 gave up the " + (!p.Day ? "day" : !p.Temperature ? "temperature" : "place");
                        }
                        if (width == 1920f && left > most)
                        {
                            most = left;
                            longest = lang;
                        }
                    }
                }
            }
        }
        Check(crossed == null, "hud: the left pills never reach the money, whatever the language, the width or the state" + (crossed == null ? "" : " (" + crossed + ")"));
        Check(cut == null, "hud: the time, the weather, the money, the needs and the alerts always stay, and at 1080 nothing is given up" + (cut == null ? "" : " (" + cut + ")"));
        Check(longest != null, "hud: the widest left group at 1080 is " + (longest ?? "?") + "'s, " + most.ToString("0") + " px");
        UiStatusModel full = UiSamples.HudBar(UiWords("en"), false, false, false);
        UiPillPlan narrow = UiDraw.PlanPills(full, UiHudRules.LeastWidth), wide = UiDraw.PlanPills(full, 1920f);
        Check(wide.District && narrow.Meter <= wide.Meter && (!narrow.District || narrow.Place == wide.Place),
            "hud: on the narrowest bar the district goes before the place, and the meters shorten before the day's words go");
        full.Hygiene = full.Stress = full.HomeNeed = -1;
        Check(UiDraw.PlanPills(full, 1920f).Meter > wide.Meter, "hud: with only the game's three needs each meter is longer");
        var empty = new UiStatusModel();
        UiPillPlan none = UiDraw.PlanPills(empty, 1920f);
        Check(none.Time == 0f && none.Weather == 0f && none.Place == 0f && none.Money == 0f && none.Needs == 0f && none.Alerts == 0f, "hud: a field left empty leaves its pill out");
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
            Check(pills == 4 && elsewhere == 0, "hud: the time, the money, the needs and the alerts are targets on the HUD's bar (" + pills + ") and pictures on every other bar (" + elsewhere + ")");
        }
        finally
        {
            UiChrome.Bar = was;
        }
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
            bool right = UiActivity.Strip(state) && y + UiActivity.Height(a) <= frame.BandY - UiTokens.S2 && Math.Abs(x + UiActivity.StripWidth / 2f - frame.Width / 2f) <= UiTokens.Grid
                         && (walking ? out1 && !skip && !fast : done ? !skip && !fast && !out1 : skip && fast && !out1);
            if (!right && wrong == null)
            {
                wrong = state + ": the strip centred above the bar with " + (walking ? "the way out" : done ? "no buttons" : "the time machine and fast forward");
            }
        }
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
