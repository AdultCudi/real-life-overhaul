using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using RealLifeOverhaul;
using RealLifeOverhaul.Ui;

// Checks for what a screen of the RLO interface costs while it shows (Ui/Host/UiCost.cs, docs/standards/performance.md,
// RLO-PERF-025): its frames, its open, its layouts and its presses by its name, the game's own work a press hands to the
// game (the map's opening behind View on map) kept apart and the mod's other scripts the game runs inside it named apart,
// the line a screen writes as it closes and its verdict, the interface's share of the minute with and without a screen
// showing, and nothing measured or allocated while the session record is off. Compiled as they ship.
internal static partial class Program
{
    private static void UiCostRuns()
    {
        UiCostScreen();
        UiCostOver();
        UiCostMinute();
        UiCostOff();
        HotSpots.On = false;
        HotSpots.Reset();
        UiCost.Reset();
    }

    // A showing of Contacts: its open with a layout in it, ten frames, and View on map pressed (the click's own press
    // inside the screen's), whose map opening is the game's and whose handler of the map's event is the mod's.
    private static void UiCostScreen()
    {
        HotUse();
        UiCost.Reset();
        var cost = new UiCost("Contacts", false);
        int click = HotSpots.Slot("Check: UiHost.PointerPressed");
        int handler = HotSpots.Slot("Check: Map.OnToggle");
        _hotFrame = 500;
        cost.Shown(10.0);
        using (cost.Time(UiWork.Open))
        {
            Spin(0.5);
            using (cost.Time(UiWork.Layout))
            {
                Spin(0.3);
            }
        }
        for (int f = 501; f <= 510; f++)
        {
            _hotFrame = f;
            using (cost.Time(UiWork.Frame))
            {
                Spin(0.02);
            }
        }
        _hotFrame = 511;
        using (HotSpots.Time(click))
        {
            using (cost.Time(UiWork.Press))
            {
                Spin(0.2);   // the screen's own press
                using (cost.Time(UiWork.Press))   // (the press node inside the click: one press)
                {
                    using (HotSpots.Game())
                    {
                        Spin(25.0);   // the game's map opening
                        using (HotSpots.Time(handler))
                        {
                            Spin(1.0);   // a handler of the mod's on the map's event
                        }
                    }
                }
            }
        }
        _hotFrame = 600;
        string line = cost.Hidden(12.0);
        Check(line != null && line.StartsWith("UI check (Contacts, open 2.0 s, 100 frames): ", StringComparison.Ordinal) && line.EndsWith(": PASS.", StringComparison.Ordinal),
            "ui cost: a screen's line as it closes, by its name, with its showing and its verdict (" + line + ")");
        Check(cost.Presses == 1 && cost.Layouts == 1, "ui cost: the click and its press node are one press, the open's layout one layout (" + cost.Presses + ", " + cost.Layouts + ")");
        Check(cost.OpenMs >= 0.8 && cost.OpenMs < 7.0, "ui cost: the open with its layout in it (" + cost.OpenMs.ToString("F2") + " ms, wanted about 0.8)");
        Check(cost.PressMs >= 0.2 && cost.PressMs < 7.0, "ui cost: the press is the kit's own work alone, not the game's map nor the mod's handler the game ran (" + cost.PressMs.ToString("F2") + " ms, wanted about 0.2)");
        Check(cost.PressInsideMs >= 1.0 && cost.PressInsideMs < cost.PressGameMs, "ui cost: the mod's handler the game ran inside the press is named apart (" + cost.PressInsideMs.ToString("F2") + " ms)");
        Check(cost.PressGameMs >= 25.0, "ui cost: the game's own work the press handed to it is kept apart (" + cost.PressGameMs.ToString("F2") + " ms)");
        Check(cost.WorkMs >= 2.2 && cost.WorkMs < 25.0, "ui cost: the showing's own work is the open, the frames and the press with the handler, never the game's (" + cost.WorkMs.ToString("F2") + " ms)");
        Check(line != null && line.Contains("1 press, the kit's own ") && line.Contains("of the game's own work the press handed to it"),
            "ui cost: the line says the press's own work and the game's apart");
        Check(cost.Hidden(13.0) == null, "ui cost: a screen closed twice writes one line");

        var phone = new UiCost("the phone", true);
        phone.Shown(10.0);
        using (phone.Time(UiWork.Frame))
        {
            Spin(0.05);
        }
        Check(phone.Hidden(11.0) == null, "ui cost: a piece of the HUD writes no line of its own");
    }

    // Over the budgets: a press of 9 ms of the kit's own, an open of 9 ms, and a screen of 0.8 ms a frame.
    private static void UiCostOver()
    {
        HotUse();
        UiCost.Reset();
        var cost = new UiCost("Slow", false);
        _hotFrame = 700;
        cost.Shown(1.0);
        using (cost.Time(UiWork.Press))
        {
            Spin(9.0);
        }
        _hotFrame = 1000;
        string line = cost.Hidden(6.0);
        Check(line != null && line.EndsWith(": OVER.", StringComparison.Ordinal), "ui cost: a press of 9 ms of the kit's own is over the budget of 8 (" + line + ")");

        cost.Shown(7.0);
        using (cost.Time(UiWork.Open))
        {
            Spin(9.0);
        }
        _hotFrame = 1300;
        line = cost.Hidden(12.0);
        Check(line != null && line.EndsWith(": OVER.", StringComparison.Ordinal) && cost.OpenMs >= 9.0, "ui cost: an open of 9 ms is over the budget of 8");

        cost.Shown(13.0);
        for (int f = 1301; f <= 1310; f++)
        {
            _hotFrame = f;
            using (cost.Time(UiWork.Frame))
            {
                Spin(0.8);
            }
        }
        line = cost.Hidden(14.0);
        Check(line != null && line.EndsWith(": OVER.", StringComparison.Ordinal), "ui cost: a screen of 0.8 ms a frame is over the budget of 0.5 a frame (" + line + ")");
    }

    // The minute of 200 frames: the phone's 0.02 ms in each of its first hundred, Contacts shown from frame 150 to 249 at
    // 0.3 ms a frame for fifty and then held under a box with no work for fifty, its open run inside the phone's frame and
    // a press of it handing the game some work; each screen counted once however they sit inside each other, every frame
    // a screen showed in told from the rest, and whose frame was the costliest.
    private static void UiCostMinute()
    {
        HotUse();
        UiCost.Reset();
        var phone = new UiCost("the phone", true);
        var contacts = new UiCost("Contacts", false);
        _hotFrame = 100;
        phone.Shown(0.0);
        for (int f = 100; f < 200; f++)
        {
            _hotFrame = f;
            if (f == 150)
            {
                contacts.Shown(1.0);
            }
            using (phone.Time(UiWork.Frame))
            {
                Spin(0.02);
                if (f == 160)
                {
                    using (contacts.Time(UiWork.Open))   // a screen the phone's press opened: counted once in the interface's share
                    {
                        Spin(20.0);
                    }
                }
            }
            if (f >= 150)
            {
                using (contacts.Time(UiWork.Frame))
                {
                    Spin(f == 160 ? 4.0 : 0.3);
                    if (f == 170)
                    {
                        using (HotSpots.Game())
                        {
                            Spin(1.0);
                        }
                    }
                }
            }
        }
        _hotFrame = 249;   // the frames 200 to 248 under a box: shown, with no work of the interface's
        contacts.Hidden(2.0);
        _hotFrame = 300;
        var into = new StringBuilder();
        UiCost.MinuteLine(into, 4.0);
        string text = into.ToString();
        Check(text.StartsWith("; the RLO interface: ", StringComparison.Ordinal) && text.Contains(" over the minute's 200 frames, paused ones in"),
            "ui cost: the minute's share over all its frames (" + text + ")");
        Check(text.Contains(" in the 100 with none of its screens showing, wanted under 0.1: ") && text.Contains(" in the 100 with one showing"),
            "ui cost: every frame a screen showed in, those under a box with no work among them, told from the rest (" + text + ")");
        double worst = UiMsBefore(text, " ms at most in a frame (");
        Check(worst >= 24.0 && worst < 34.0, "ui cost: a screen's work inside another's is counted once in the interface's frame (" + worst.ToString("F1", CultureInfo.InvariantCulture) + " ms at most, wanted about 24)");
        Check(text.Contains(" ms at most in a frame (Contacts)"), "ui cost: the costliest frame's screen named (Contacts, its open run inside the phone's frame)");
        Check(text.Contains("; the game's own work its presses handed to the game: "), "ui cost: the game's own work handed to it in the minute, apart");
        into.Length = 0;
        UiCost.MinuteLine(into, 4.0);
        Check(into.Length == 0, "ui cost: a minute in which nothing ran adds nothing");
        phone.Hidden(3.0);
    }

    // The figure just before the words in a line ("..., 24.0 ms at most in a frame" gives 24.0); -1 when it is not there.
    private static double UiMsBefore(string text, string words)
    {
        int end = text.IndexOf(words, StringComparison.Ordinal);
        if (end <= 0)
        {
            return -1.0;
        }
        int start = text.LastIndexOf(' ', end - 1) + 1;
        return double.TryParse(text.Substring(start, end - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double ms) ? ms : -1.0;
    }

    // Off, as the mod ships: a scope is an empty value, nothing is kept and nothing is allocated.
    private static void UiCostOff()
    {
        HotUse();
        UiCost.Reset();
        var cost = new UiCost("Contacts", false);
        cost.Shown(1.0);
        HotSpots.On = false;
        long allocated = UiOffScopes(cost);
        Check(allocated == 0, "ui cost: off, a million scopes allocate nothing (" + allocated + " bytes)");
        Check(cost.Hidden(2.0) == null && cost.WorkMs == 0.0, "ui cost: off, nothing is measured and no line is written");
    }

    // Compiled in full before it runs (see OffScopes in HotSpotCheck.cs).
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long UiOffScopes(UiCost cost)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000000; i++)
        {
            using var scope = cost.Time(UiWork.Frame);
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
