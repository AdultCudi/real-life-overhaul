using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ui;

// The tooltip card (Scripts/Ui/UiTooltipCard, UiTooltipRules, UiNetWorthTip, Ui/Tooltips) and the days of net worth behind the money pill's card
// (Economy/Finance/WorthBook, WorthStore, WorthWatch). The card is placed as the game's own tooltip is and stays on the screen; a customer is
// shared among the people at the till and none is lost or made; a fortune's days keep to their caps, the card's highest, lowest and change
// are what the days say, a save written on day 30 plays on as one never saved, an older save and a damaged line load; the card holds no target;
// and the live code does what its words promise (the game's tooltip kept alive and hidden, nothing per frame, the money pill wired).
internal static partial class Program
{
    private static void TooltipChecks()
    {
        int before = _run, failedBefore = _failed;
        var watch = Stopwatch.StartNew();
        TooltipPlace();
        TooltipMood();
        TooltipServed();
        TooltipDays();
        TooltipDaysRun();
        TooltipDaysSave();
        TooltipCard();
        TooltipWorthCard();
        TooltipSource();
        TooltipWords();
        Console.WriteLine("  tooltips: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // ------------------------------------------------------------------------------------------------- placement

    private static void TooltipPlace()
    {
        const float W = 1920f, H = 1080f, gap = UiTooltipRules.Gap;
        float x, y;
        UiTooltipRules.Place(960f, 600f, 240f, 160f, W, H, gap, out x, out y);
        Check(Math.Abs(x - 840f) < 0.01f && Math.Abs(y - 440f) < 0.01f, "tooltip placement: centred over the pointer with its foot on it, as the game places its own (" + x + ", " + y + ")");
        UiTooltipRules.Place(960f, 100f, 240f, 160f, W, H, gap, out x, out y);
        Check(Math.Abs(y - (100f + gap)) < 0.01f, "tooltip placement: where it would not clear the top it hangs below the pointer by the gap (" + y + ")");
        UiTooltipRules.Place(960f, 150f, 240f, 160f, W, H, gap, out x, out y);
        Check(y >= 0f && y <= 150f, "tooltip placement: a little short of the top the card is held flush against it (" + y + ")");
        UiTooltipRules.Place(5f, 600f, 240f, 160f, W, H, gap, out x, out y);
        Check(x == 0f, "tooltip placement: held at the left edge");
        UiTooltipRules.Place(1915f, 600f, 240f, 160f, W, H, gap, out x, out y);
        Check(Math.Abs(x - (W - 240f)) < 0.01f, "tooltip placement: held at the right edge");
        UiTooltipRules.Place(960f, 1075f, 240f, 160f, W, H, gap, out x, out y);
        Check(y + 160f <= H + 0.01f, "tooltip placement: never past the foot of the screen");
        // Every pointer over a screen and every card size: always inside it, and never over the pointer when it hangs below.
        bool inside = true, clear = true;
        float[] sizes = { 192f, 240f, 320f };
        float[] highs = { 40f, 120f, 260f, 480f };
        for (int w = 0; w < sizes.Length; w++)
        {
            for (int h = 0; h < highs.Length; h++)
            {
                for (float px = 0f; px <= W; px += 90f)
                {
                    for (float py = 0f; py <= H; py += 60f)
                    {
                        UiTooltipRules.Place(px, py, sizes[w], highs[h], W, H, gap, out x, out y);
                        inside &= x >= -0.01f && y >= -0.01f && x + sizes[w] <= W + 0.01f && y + highs[h] <= H + 0.01f;
                        clear &= !(py > y + 0.01f && py < y + highs[h] - 0.01f && px >= x && px <= x + sizes[w]) || py - highs[h] < gap;   // over the pointer (its foot may stand on it) only where the screen leaves no room
                    }
                }
            }
        }
        Check(inside, "tooltip placement: the card is inside the screen for every pointer, width and height");
        Check(clear, "tooltip placement: the card stands off the pointer wherever the screen leaves room");
        UiTooltipRules.OverPill(1700f, 1040f, 140f, 280f, 300f, W, gap, out x, out y);
        Check(Math.Abs(x + 280f - 1840f) < 0.01f && y + 300f <= 1040f, "tooltip placement: the money's card stands over its pill, its right edge on the pill's (" + x + ", " + y + ")");
        UiTooltipRules.OverPill(20f, 30f, 140f, 280f, 300f, W, gap, out x, out y);
        Check(x >= UiHudRules.Margin && y >= UiHudRules.Margin, "tooltip placement: the money's card is held inside the screen");
    }

    // ------------------------------------------------------------------------------------------------- mood

    private static void TooltipMood()
    {
        Check(UiTooltipRules.MoodStep(0) == 0 && UiTooltipRules.MoodStep(29) == 0 && UiTooltipRules.MoodStep(30) == 1 && UiTooltipRules.MoodStep(59) == 1 && UiTooltipRules.MoodStep(60) == 2
              && UiTooltipRules.MoodStep(100) == 2, "tooltip mood: low under 30, fair under 60 and good above, the game's own steps for an employee");
        Check(UiTooltipRules.MoodTone(10) == UiRole.Danger && UiTooltipRules.MoodTone(45) == UiRole.Warn && UiTooltipRules.MoodTone(80) == UiRole.Ok, "tooltip mood: the bar is red, yellow or green by the step");
    }

    // ------------------------------------------------------------------------------------------------- customers served

    private static void TooltipServed()
    {
        var perHour = new int[24];
        perHour[9] = 7;
        perHour[10] = 12;
        perHour[11] = 5;
        perHour[20] = 3;   // nobody at the till then
        var crew = new[]
        {
            new UiTipShift { Who = 11, From = 8, To = 12 }, new UiTipShift { Who = 22, From = 9, To = 12 }, new UiTipShift { Who = 33, From = 10, To = 12 }
        };
        int a = UiTooltipRules.Served(perHour, crew, 3, 11), b = UiTooltipRules.Served(perHour, crew, 3, 22), c = UiTooltipRules.Served(perHour, crew, 3, 33);
        Check(a + b + c == 7 + 12 + 5, "tooltip customers: every customer of the hours with someone at the till is theirs, none lost or made (" + a + " + " + b + " + " + c + ")");
        Check(UiTooltipRules.Served(perHour, crew, 3, 99) == 0, "tooltip customers: a person with no shift served none");
        Check(UiTooltipRules.Served(perHour, crew, 3, 11) == a && UiTooltipRules.Served(perHour, crew, 3, 22) == b, "tooltip customers: the same day's figures are the same each time");
        Check(Math.Abs(a - b) <= 4 && Math.Abs(b - c) <= 4, "tooltip customers: shared as evenly as their hours go (" + a + ", " + b + ", " + c + ")");
        var alone = new[] { new UiTipShift { Who = 5, From = 8, To = 22 } };
        Check(UiTooltipRules.Served(perHour, alone, 1, 5) == 7 + 12 + 5 + 3, "tooltip customers: one person at the till served every customer of their hours");
        // The odd customer of an hour does not always go to the same person.
        var even = new int[24];
        for (int h = 8; h < 18; h++)
        {
            even[h] = 1;
        }
        var two = new[] { new UiTipShift { Who = 1, From = 8, To = 18 }, new UiTipShift { Who = 2, From = 8, To = 18 } };
        int one = UiTooltipRules.Served(even, two, 2, 1), other = UiTooltipRules.Served(even, two, 2, 2);
        Check(one == 5 && other == 5, "tooltip customers: a lone customer an hour is turn about between two people (" + one + ", " + other + ")");
        Check(UiTooltipRules.Served(new int[24], crew, 3, 11) == 0 && UiTooltipRules.Served(perHour, crew, 0, 11) == 0, "tooltip customers: no customers or no crew is nothing");
        // Over every hour and crew of up to six: the shares always add up to what the hours with a crew held.
        var rng = new Random(77);
        bool sums = true;
        for (int trial = 0; trial < 400; trial++)
        {
            var hours = new int[24];
            int total = 0;
            for (int h = 0; h < 24; h++)
            {
                hours[h] = rng.Next(0, 9);
            }
            int n = rng.Next(1, 7);
            var team = new UiTipShift[n];
            for (int i = 0; i < n; i++)
            {
                int from = rng.Next(0, 20);
                team[i] = new UiTipShift { Who = 100 + i * 7, From = from, To = Math.Min(24, from + rng.Next(2, 10)) };
            }
            int shared = 0;
            for (int i = 0; i < n; i++)
            {
                shared += UiTooltipRules.Served(hours, team, n, team[i].Who);
            }
            for (int h = 0; h < 24; h++)
            {
                bool covered = false;
                for (int i = 0; i < n; i++)
                {
                    covered |= h >= team[i].From && h < team[i].To;
                }
                total += covered ? hours[h] : 0;
            }
            sums &= shared == total;
        }
        Check(sums, "tooltip customers: over 400 random days and crews the shares add up to the customers of the hours someone stood at the till");
    }

    // ------------------------------------------------------------------------------------------------- the days of net worth

    private static void TooltipDays()
    {
        var ring = new NetWorthRing();
        ring.Push(10, 100);
        ring.Push(11, 110);
        ring.Push(11, 115);
        ring.Push(9, 5);
        Check(ring.Count == 2 && ring.WorthAt(1) == 115 && ring.LastDay == 11, "worth days: a day again takes the later figure and an earlier day than the last is not kept");
        var big = new NetWorthRing();
        for (int d = 1; d <= 200; d++)
        {
            big.Push(d, d * 10L);
        }
        Check(big.Count == NetWorthRing.Most && big.DayAt(0) == 141 && big.LastDay == 200, "worth days: the player's days stop at sixty, the oldest dropped (" + big.Count + ")");
        var book = new WorthBook();
        var worths = new long[WorthBook.Tycoons];
        var active = new bool[WorthBook.Tycoons];
        for (int d = 1; d <= 90; d++)
        {
            for (int i = 0; i < 3; i++)
            {
                worths[i] = 1000L * (i + 1) + d;
                active[i] = i != 1 || d > 40;
            }
            book.Day(d, 500L + d, worths, active, 3);
        }
        Check(book.Player.Count == 60 && book.Rivals[0].Count == WorthBook.TycoonDays && book.Rivals[1].Count == WorthBook.TycoonDays && book.Rivals[2].Count == WorthBook.TycoonDays && book.Rivals[3].Count == 0,
            "worth days: each active tycoon has thirty days, one who came forward on day 41 only since, an unused place none (" + book.Rivals[1].Count + ")");
        Check(book.Rivals[0].WorthAt(book.Rivals[0].Count - 1) == 1090L && book.Rivals[1].DayAt(0) == 61, "worth days: a tycoon's figure is their own worth that day");

        // The summary: highest, lowest and change are what the days say, today's live figure taken in.
        var r = new NetWorthRing();
        long[] w = { 100, 140, 90, 120, 160, 130 };
        for (int i = 0; i < w.Length; i++)
        {
            r.Push(10 + i, w[i]);
        }
        var line = new float[WorthBook.LineLength(r, 16)];
        WorthSummary s = WorthBook.Summary(r, 16, 150, line);
        Check(s.Has && s.Now == 150 && s.High == 160 && s.HighDay == 14 && s.Low == 90 && s.LowDay == 12 && s.Change == 50 && s.FromDay == 10 && s.Points == 7,
            "worth days: highest and lowest with their days and the change from the first day of the window (" + s.High + " on " + s.HighDay + ", " + s.Low + " on " + s.LowDay + ", " + s.Change + ")");
        Check(line.Length == 7 && line[0] == 100 && line[6] == 150, "worth days: the line is the days oldest first and today's live figure last");
        WorthSummary live = WorthBook.Summary(r, 16, 80, null);
        Check(live.Low == 80 && live.LowDay == 16 && live.Change == -20, "worth days: today's own figure counts as the lowest when it is, and the change turns negative");
        Check(!WorthBook.Summary(new NetWorthRing(), 5, 77, null).Has && WorthBook.Summary(new NetWorthRing(), 5, 77, null).High == 77, "worth days: a save with no days says so and shows today alone");
        var far = new NetWorthRing();
        far.Push(2, 500);
        Check(!WorthBook.Summary(far, 100, 600, null).Has, "worth days: a figure more than thirty days old is out of the window");
    }

    // 120 game days of a seeded city: the player's worth and three tycoons', a time skip over a week, a tycoon who comes forward late.
    private static void TooltipDaysRun()
    {
        var rng = new Random(2026);
        var book = new WorthBook();
        var worths = new long[WorthBook.Tycoons];
        var active = new bool[WorthBook.Tycoons];
        long player = 50000, p0 = 0;
        bool gap = false;
        var seen = new List<long>();
        var seenDay = new List<int>();
        bool dayOk = true, lineOk = true, summaryOk = true;
        for (int day = 1; day <= 120; day++)
        {
            if (day >= 50 && day < 57)
            {
                continue;   // a time skip: the interface saw nothing of those days
            }
            player += rng.Next(-2500, 3500);
            for (int i = 0; i < 3; i++)
            {
                worths[i] = 200000L * (i + 1) + rng.Next(0, 50000) + day * 100L;
                active[i] = i == 0 || day >= 30 * i;
            }
            book.Day(day, player, worths, active, 3);
            seen.Add(player);
            seenDay.Add(day);
            if (day > 5)
            {
                var line = new float[WorthBook.LineLength(book.Player, day + 1)];
                WorthSummary s = WorthBook.Summary(book.Player, day + 1, player + 123, line);   // the next morning: today's figure is not yet in the ring
                long high = player + 123, low = player + 123, start = player + 123;
                int first = day + 1 - WorthBook.Window + 1, n = 0;
                for (int i = 0; i < seen.Count; i++)
                {
                    if (seenDay[i] >= first)
                    {
                        if (n++ == 0)
                        {
                            start = seen[i];
                        }
                        high = Math.Max(high, seen[i]);
                        low = Math.Min(low, seen[i]);
                    }
                }
                summaryOk &= s.High == high && s.Low == low && s.Change == player + 123 - start && s.Points == n + 1;
                lineOk &= line.Length == n + 1 && line[line.Length - 1] == player + 123;
            }
            dayOk &= book.Player.Count <= NetWorthRing.Most && book.Rivals[0].Count <= WorthBook.TycoonDays && book.Rivals[1].Count <= WorthBook.TycoonDays;
            if (day == 80)
            {
                for (int i = 1; i < book.Player.Count; i++)
                {
                    gap |= book.Player.DayAt(i) - book.Player.DayAt(i - 1) > 1;
                }
            }
            p0 = player;
        }
        Check(dayOk, "worth days run: over 120 days the rings keep to their caps");
        Check(summaryOk, "worth days run: every day's highest, lowest, change and days on the line are what a straight count of the days says");
        Check(lineOk, "worth days run: the line ends on today's live figure every day");
        Check(book.Player.Count == 60 && book.Player.LastDay == 120 && book.Rivals[2].Count > 0 && book.Rivals[2].DayAt(0) >= 60 && book.Rivals[1].Count == WorthBook.TycoonDays,
            "worth days run: the rivals took their own snapshot each day from the day they came forward (" + book.Rivals[2].Count + " days for the third, " + book.Rivals[1].Count + " for the second)");
        Check(p0 != 0, "worth days run: the run played to the end");
        Check(gap, "worth days run: the skipped week is left a gap in the days, not filled with figures nobody saw");
    }

    private static void TooltipDaysSave()
    {
        // A save written on day 30 plays the next 30 days as the one never saved.
        var rng = new Random(9);
        var straight = new WorthBook();
        var resumed = new WorthBook();
        var worths = new long[WorthBook.Tycoons];
        var active = new bool[WorthBook.Tycoons];
        long player = 90000;
        string line = null;
        bool same = true;
        for (int day = 1; day <= 60; day++)
        {
            player += rng.Next(-1000, 2000);
            worths[0] = 777L * day;
            worths[1] = 12345L + day;
            active[0] = true;
            active[1] = day > 10;
            straight.Day(day, player, worths, active, 2);
            if (day == 30)
            {
                line = WorthStore.Write(straight);
                Check(WorthStore.Read(line, out resumed) && resumed.Player.Count == 30, "worth days save: a line written on day 30 reads back, thirty days long");
            }
            else if (day > 30)
            {
                resumed.Day(day, player, worths, active, 2);
            }
        }
        same &= WorthStore.Write(straight) == WorthStore.Write(resumed);
        Check(same, "worth days save: the next thirty days from a save are the days of one never saved");
        Check(WorthStore.Read(WorthStore.Write(straight), out WorthBook again) && WorthStore.Write(again) == WorthStore.Write(straight), "worth days save: written, read and written again is the same line");
        Check(WorthStore.Key == "reallife:worth_days" && WorthStore.Write(new WorthBook()).StartsWith("WD|1"), "worth days save: under its own reallife: key, a line with a version");

        // An older save, a later version's, a damaged line.
        Check(WorthStore.Read(null, out WorthBook none) && none.Empty && WorthStore.Read(string.Empty, out WorthBook blank) && blank.Empty, "worth days save: an older save with no line loads as no days");
        Check(!WorthStore.Read("WD|2\nP|1:5", out WorthBook later) && later.Empty, "worth days save: a later version's line is refused, not read");
        Check(!WorthStore.Read("WD|1\nP|3:5,2:6", out WorthBook back) && back.Empty, "worth days save: days that run backward are a damaged line");
        Check(!WorthStore.Read("WD|1\nP|x:5", out _) && !WorthStore.Read("junk", out _) && !WorthStore.Read("WD|1\nT|-1|1:2", out _), "worth days save: a damaged line is refused");
        Check(WorthStore.Read("WD|1\nP|1:5,2:6\nZ|future line\nT|9|1:1", out WorthBook skip) && skip.Player.Count == 2, "worth days save: a line this version does not know, or a tycoon past its places, is skipped");
        Check(WorthStore.Read("WD|1\r\nP|1:5,2:6\r\n", out WorthBook crlf) && crlf.Player.Count == 2, "worth days save: a line with Windows line ends reads");
    }

    // ------------------------------------------------------------------------------------------------- the card

    private static void TooltipCard()
    {
        UiWords en = (key, english) => english;
        bool widths = true, targets = true, steady = true, inside = true;
        foreach (string state in UiSamples.TooltipStates)
        {
            UiTooltipModel m = UiSamples.Tooltip(en, state);
            float w = UiTooltipCard.Width(m), h = UiTooltipCard.Height(m, w);
            widths &= w >= UiTooltipCard.MinWidth && w <= UiTooltipCard.MaxWidth;
            var s = new UiScene();
            s.Begin("tooltip", w, h, true);
            UiTooltipCard.Lay(s, UiAt.Screen, m, 0f, 0f, w);
            int items = s.ItemCount;
            targets &= s.NodeCount == 0;
            s.Begin("tooltip", w, h, true);
            UiTooltipCard.Lay(s, UiAt.Screen, m, 0f, 0f, w);
            steady &= s.ItemCount == items && items > 2;
            for (int i = 1; i < s.ItemCount; i++)
            {
                inside &= s.Items[i].X >= -0.01f && s.Items[i].Y >= -0.01f && s.Items[i].X + s.Items[i].W <= w + 0.5f && s.Items[i].Y + s.Items[i].H <= h + 0.5f;
            }
        }
        Check(widths, "tooltip card: every state's width is between the narrowest and the widest card");
        Check(targets, "tooltip card: it holds no target and no focus: a layout has no node");
        Check(steady, "tooltip card: laying the same words again gives the same card");
        Check(inside, "tooltip card: every piece of every state lies inside the card");
        // Long words wrap inside the widest card, and a long header is cut, never run out of the card.
        var wordy = new UiTooltipModel();
        wordy.Header("A very long name of a business that goes on and on and on", "an extra long second word");
        wordy.Text("A line of words that is far longer than the widest card, which must wrap to a second and a third line rather than run out of the panel at its right edge.");
        float lw = UiTooltipCard.Width(wordy), lh = UiTooltipCard.Height(wordy, lw);
        var ls = new UiScene();
        ls.Begin("tooltip", lw, lh, true);
        UiTooltipCard.Lay(ls, UiAt.Screen, wordy, 0f, 0f, lw);
        bool fits = lw == UiTooltipCard.MaxWidth && lh > 60f;
        for (int i = 1; i < ls.ItemCount; i++)
        {
            fits &= ls.Items[i].X + ls.Items[i].W <= lw + 0.5f && ls.Items[i].Y + ls.Items[i].H <= lh + 0.5f;
        }
        Check(fits, "tooltip card: long words wrap and a long name is cut inside the widest card");
        Check(new UiTooltipModel().Empty && !UiSamples.Tooltip(en, "employee").Empty, "tooltip card: a tooltip with nothing to say is empty, one with words is not");
        // Every state's colours come from the kit's roles: no spine, no pure white.
        bool spines = false;
        foreach (string state in UiSamples.TooltipStates)
        {
            UiTooltipModel m = UiSamples.Tooltip(en, state);
            float w = UiTooltipCard.Width(m), h = UiTooltipCard.Height(m, w);
            var s = new UiScene();
            s.Begin("tooltip", w, h, true);
            UiTooltipCard.Lay(s, UiAt.Screen, m, 0f, 0f, w);
            for (int i = 1; i < s.ItemCount; i++)
            {
                UiItem it = s.Items[i];
                spines |= it.Kind == UiKind.Box && (it.Flags & UiFlags.Decor) == 0 && it.W <= 8f && it.H > 24f && it.Fill != UiRole.Track && it.Fill != UiRole.Panel;
            }
        }
        Check(!spines, "tooltip card: no coloured bar down an edge of the card");
    }

    private static void TooltipWorthCard()
    {
        UiWords en = (key, english) => english;
        UiTooltipModel m = UiSamples.Tooltip(en, "worth");
        Check(m.Blocks[0].Kind == UiTipKind.Header && m.Blocks[0].Title == "Net worth" && m.Blocks[0].Sub == "Day 288" && m.Blocks[0].Detail == "The last 30 days", "net worth card: its header names it, its day and the thirty days");
        UiTipBlock chart = null, rows = null;
        for (int i = 0; i < m.Blocks.Count; i++)
        {
            chart = m.Blocks[i].Kind == UiTipKind.Chart ? m.Blocks[i] : chart;
            rows = m.Blocks[i].Kind == UiTipKind.Rows ? m.Blocks[i] : rows;
        }
        Check(chart != null && chart.Points.Length == 30 && chart.From == "Day 259" && chart.To == "Day 288" && chart.High == "$1.93B" && chart.Low == "$1.71B", "net worth card: a line of the thirty days with its highest and lowest at the right and its first and last day under it");
        Check(rows != null && rows.Lines.Count == 4 && rows.Lines[0].Left == "Now" && rows.Lines[1].Left == "Highest" && rows.Lines[1].Right.EndsWith("on day 281") && rows.Lines[2].Right.EndsWith("on day 262")
              && rows.Lines[3].Left == "Change" && rows.Lines[3].Right.StartsWith("+") && rows.Lines[3].Ink == UiRole.OkText, "net worth card: Now, Highest and Lowest with their days and the change, a rise in green");
        Check(rows != null && rows.Lines[0].Right == "$1.9B" && rows.Lines[1].Right == "$1.93B on day 281" && rows.Lines[2].Right == "$1.71B on day 262" && rows.Lines[3].Right.StartsWith("+$"),
            "net worth card: Now, Highest, Lowest and Change are short amounts, so a big fortune's rows fit the card");
        UiTooltipModel early = UiSamples.Tooltip(en, "worth_early");
        bool any = false, said = false;
        for (int i = 0; i < early.Blocks.Count; i++)
        {
            any |= early.Blocks[i].Kind == UiTipKind.Chart;
            said |= early.Blocks[i].Kind == UiTipKind.Text && early.Blocks[i].Text.Contains("second day");
        }
        Check(!any && said, "net worth card: a save with fewer than two days says its line starts later and draws none, not a made up one");
        var loss = new WorthSummary { Now = 1000, Has = true, High = 2000, Low = 900, HighDay = 3, LowDay = 8, Change = -400, FromDay = 1, Points = 8 };
        UiTooltipModel down = UiNetWorthTip.Model(en, loss, 9, new float[] { 1400, 2000, 900, 1000 }, a => "$" + a);
        UiTipBlock dr = down.Blocks[down.Blocks.Count - 1];
        Check(dr.Lines[3].Right == "-$400" && dr.Lines[3].Ink == UiRole.DangerText, "net worth card: a fall is written with its sign and in red");
    }

    // ------------------------------------------------------------------------------------------------- the live code's promises

    private static void TooltipSource()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string host = File.ReadAllText(System.IO.Path.Combine(scripts, @"Ui\Tooltips\TooltipHost.cs"));
        string read = File.ReadAllText(System.IO.Path.Combine(scripts, @"Ui\Tooltips\TooltipRead.cs"));
        string kit = File.ReadAllText(System.IO.Path.Combine(scripts, @"Ui\Tooltips\TooltipKit.cs"));
        string hud = File.ReadAllText(System.IO.Path.Combine(scripts, @"Ui\Hud\HudHost.cs"));
        string dir = File.ReadAllText(System.IO.Path.Combine(scripts, @"Ui\UiDirector.cs"));
        string watch = File.ReadAllText(System.IO.Path.Combine(scripts, @"Economy\Finance\WorthWatch.cs"));
        string guide = File.ReadAllText(System.IO.Path.Combine(scripts, @"Ui\UiGuide.cs"));
        string employee = File.ReadAllText(System.IO.Path.Combine(scripts, @"Ui\Tooltips\TooltipEmployee.cs"));
        string update = Between(host, "private void OnUpdate()", "// The canvas changed size");
        Check(!host.Contains("Harmony") && !read.Contains("Harmony") && !host.Contains("[HarmonyPatch"), "tooltips: no patch of the game: the card reads the clones the game's builder made");
        Check(read.Contains("header.name + CloneSuffix") && read.Contains("_content.GetChild(i)") && read.Contains("GetInstanceID()"), "tooltips: the content is read by the clones' names, as the game names them, and told changed by the last clone's id");
        Check(!host.Contains("_data.gameObject.SetActive") && !host.Contains("data.gameObject.SetActive"), "tooltips: the game's own tooltip objects are never switched off");
        Check(host.Contains("_conceal.Add(data)") && host.Contains("_conceal.Hide()") && host.Contains("_conceal.Clear()") && !host.Contains("Destroy(system") && !host.Contains("Destroy(_data"),
            "tooltips: the game's tooltip stays alive and is hidden under a fade group of the kit's, given back as found");
        Check(update.Contains("TooltipSystem.IsVisible") && update.Contains("_read.Signature()") && !update.Contains("FindObject") && !update.Contains("GetComponentsIn") && !update.Contains("new "),
            "tooltips: a frame reads a flag and two numbers and allocates nothing: no search, no new object");
        Check(host.Contains("if (m.x == _mouse.x && m.y == _mouse.y)") && host.Contains("UiTooltipRules.Place("), "tooltips: the card follows the mouse by the game's placing, and only when the mouse moved");
        Check(Between(host, "private void Lay()", "// The card follows the mouse") .Contains("_view.Write(_scene, false)") && !update.Contains("_view.Write"), "tooltips: the card is laid out when the words change, never in a frame's update");
        Check(host.Contains("raycaster.enabled = false") && host.Contains("group.interactable = false") && !host.Contains("blocksRaycasts = true"), "tooltips: the card takes no pointer, so the target under it keeps its hover");
        Check(host.Contains("UiOverCanvas.Make(canvas") && host.Contains("UiHudRules.Scale(size.x)"), "tooltips: the card stands on a canvas nested in the game's tooltip canvas and follows the UI zoom");
        Check(update.Contains("UiScreenSwitches.On(TooltipKit.Id)") && update.Contains("UiDirector.On") && kit.Contains("public const string Id = \"tooltips\""), "tooltips: the switch and the interface's master switch give the game's tooltip back at once");
        Check(kit.Contains("Fallback(") && host.Contains("Guard.Fail(FrameGuard") && host.Contains("TooltipKit.Fault("), "tooltips: a fault gives the game's tooltips back for the session with one warning");
        Check(read.Contains("return false;   // a clone of a template this version does not list") && read.Contains("HasClone(_imageParent, _image)") && host.Contains("_gameOwn = true"), "tooltips: a picture or a clone it does not know is left to the game's own tooltip");
        Check(hud.Contains("TooltipKit.Money(") && hud.Contains("UiDraw.MoneyPillCommand") && hud.Contains("TooltipKit.MoneyOut();"), "tooltips: the money pill's hover raises the net worth card and leaving or pressing it takes it away");
        Check(dir.Contains("TooltipKit.Install();") && dir.Contains("TooltipKit.Remove(why);") && dir.Contains("TooltipKit.Rearm();") && dir.Contains("Economy.WorthWatch.Tick();"), "tooltips: the interface's tick installs the card, takes it away and records the day");
        Check(watch.Contains("GoingPublic.IsGuest") && watch.Contains("SaveLines.Unreadable(") && watch.Contains("SaveLines.IsKept(") && watch.Contains("Meta.SaveTools.Dormant") && watch.Contains("SaveHook.Keep(OnSave)"),
            "tooltips: the days of net worth keep a guest's save, an unread line and a dormant save as they were, and are written as the game saves");
        Check(guide.Contains(".Data(Economy.WorthStore.Key)") && guide.Contains(".ResetData(Economy.WorthWatch.Forget)"), "tooltips: the days' key is declared by the interface's feature");
        Check(employee.Contains("CustomerDraw.Entries(reg)") && employee.Contains("UiTooltipRules.Served(") && employee.Contains("GetWorkShiftAssignedInThisMoment") && employee.Contains("e.isBeingReplaced"),
            "tooltips: an employee's shift and customers come from the game's own shifts and customer list, nothing saved");
        UiScreenDef def = UiScreens.Find("tooltips");
        Check(def != null && def.Section == UiScreenSwitches.Pause && UiScreenSwitches.OptionOf("tooltips") == "ui_screen_tooltips", "tooltips: the screen is registered with its own switch in the pause section");
    }

    // Every text the card and its tests use is in en, pt and zh-cn, with the same {names}.
    private static readonly string[] TooltipKeys =
    {
        "reallife_ui_screen_tooltips", "reallife_opt_tip_ui_screen_tooltips", "reallife_ui_tip_mood", "reallife_ui_tip_mood_low", "reallife_ui_tip_mood_fair", "reallife_ui_tip_mood_good",
        "reallife_ui_tip_today", "reallife_ui_tip_served", "reallife_ui_tip_shift_until", "reallife_ui_tip_shift_next", "reallife_ui_tip_shift_none", "reallife_ui_tip_shift_away",
        "reallife_ui_tip_day", "reallife_ui_tip_worth_title", "reallife_ui_tip_worth_span", "reallife_ui_tip_worth_now", "reallife_ui_tip_worth_high", "reallife_ui_tip_worth_low",
        "reallife_ui_tip_worth_change", "reallife_ui_tip_worth_on_day", "reallife_ui_tip_worth_early", "reallife_ui_test_tooltip", "reallife_ui_test_worth_card",
        "reallife_ui_test_tooltips_switch", "reallife_ui_test_tooltip_none", "reallife_ui_test_tooltip_off", "reallife_ui_test_tooltip_on_kit", "reallife_ui_test_tooltip_waiting",
        "reallife_ui_test_tooltip_on_game", "reallife_ui_test_tooltip_kit", "reallife_ui_test_tooltip_game", "reallife_ui_test_tooltip_title", "reallife_ui_test_tooltip_second",
        "reallife_ui_test_tooltip_line", "reallife_ui_test_tooltip_item_a", "reallife_ui_test_tooltip_item_b", "reallife_contract_part_ui_tooltips"
    };

    private static void TooltipWords()
    {
        string folder = System.IO.Path.Combine(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul")), "Locales");
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json")), pt = LoadWords(System.IO.Path.Combine(folder, "pt.json")), zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        Check(en != null && pt != null && zh != null, "tooltip words: the three locales are where the checks look for them");
        if (en == null || pt == null || zh == null)
        {
            return;
        }
        var holes = new Regex(@"\{[a-z_]+\}");
        var missing = new List<string>();
        foreach (string key in TooltipKeys)
        {
            if (!en.TryGetValue(key, out string e) || !pt.TryGetValue(key, out string p) || !zh.TryGetValue(key, out string z))
            {
                missing.Add(key);
                continue;
            }
            string want = Holes(holes, e);
            if (Holes(holes, p) != want || Holes(holes, z) != want)
            {
                missing.Add(key + " (its {names} differ)");
            }
        }
        Check(missing.Count == 0, "tooltip words: every text is in en.json, pt.json and zh-cn.json with the same {names} (" + TooltipKeys.Length + " keys" + (missing.Count > 0 ? "; " + string.Join(", ", missing) : string.Empty) + ")");
        bool short240 = true, noDash = true;
        foreach (string key in TooltipKeys)
        {
            if (en.TryGetValue(key, out string e))
            {
                short240 &= e.Length <= 240;
                noDash &= e.IndexOf('—') < 0 && e.IndexOf('–') < 0 && e.IndexOf(" - ", StringComparison.Ordinal) < 0 && e.IndexOf('!') < 0;
            }
        }
        Check(short240 && noDash, "tooltip words: each is at most 240 characters, with no dash and no exclamation mark");
    }
}
