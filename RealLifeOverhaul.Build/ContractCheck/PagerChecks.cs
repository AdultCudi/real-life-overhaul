using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.GameUi;

// The pages of the phone's apps (Core/GameUi/PagerRules.cs, PhoneGridPager.cs). The full menu's row of apps measured its
// room from the left edge of the money each time the menu opened, so the number of apps on a page, the number of pages
// and the row's length changed with the money and the date, and its pages were turned with small dots. The rules: a
// page is a set number of tiles, the HUD phone's eight and the full menu's as many as fit before the money's fixed box,
// worked out once for each screen (between two and the game's own seven); the game's own apps come first in their order;
// the pages fill in order and only the last is shorter; the last tile of every page is the "More apps" button, which
// shows the page ("2 / 3"), goes to the next and from the last to the first, and is not there when there is one page. And
// the sources: the count reads only the money's box and only when the bar's width changes, the game's apps are put first,
// the button stands on the last tile, the dots are gone, the texts are in all three languages, and What's new says so.
internal static partial class Program
{
    // The game's full menu row (Topbar/AppButtons, level4): a box 1400 wide centred at 1650 of the canvas of 3840, upper
    // centre, spacing 20; the buttons 220 wide; the money, date and time (RightData) end 100 from the canvas's right edge.
    private const float RowCentre = 1650f, RowSpacing = 20f, RowCell = 220f, RowPitch = RowCell + RowSpacing, DataRight = 100f, DataBox = 1350f;

    private static void PagerChecks(string scripts, string locales)
    {
        Section("the phone's pages: a set number of apps a page and the More apps button (Core/GameUi/PagerRules)");
        PagerCount();
        PagerOnce();
        PagerCentre();
        PagerGameFirst();
        PagerModOrder();
        PagerFilling();
        PagerButton();
        PagerEdges();
        PagerTiming();
        Section("the phone's pages: the pager follows the rules, in " + scripts);
        PagerSources(scripts, locales);
    }

    // The full menu's row on every screen the game allows: 4:3, 16:10 and 16:9 (its resolutions are at least 1280 by 720),
    // the interface at 50% to 200% (Options.cs: minUIZooming 0.5, maxUIZooming 2; UIs.SetUIZooming sets the reference
    // to 3840 by 2160 over the zoom, matched to the height, so the canvas is 2160 / zoom * aspect wide). The row is
    // anchored to the bar's left edge and the money, the date and the time (RightData) to its right: a box 1350 wide
    // ending 100 from the edge, as wide as they can ever be. A full page and its button stay on the screen and end clear
    // of that box, whatever the money, the date or the language.
    private static void PagerCount()
    {
        Check(PagerRules.StripTiles(7) == 7 && PagerRules.StripTiles(0) == PagerRules.GameRowApps, "the most a page of the full menu's row has is the game's own row (7)");
        float gameWidth = PagerRules.RowWidth(7, RowPitch, RowSpacing);
        Check(gameWidth == 1660f && PagerRules.Fit(gameWidth, RowPitch, RowSpacing) == 7 && PagerRules.Fit(gameWidth - 0.5f, RowPitch, RowSpacing) == 6,
            "the game's own row is 1660 wide and holds its seven, half a pixel less holds six");
        float begin = RowCentre - gameWidth * 0.5f;   // where the game's own row begins, and every page with it
        float[] aspects = { 4f / 3f, 16f / 10f, 16f / 9f };
        string[] named = { "4:3", "16:10", "16:9" };
        float[] zooms = { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 1.75f, 2f };
        int cases = 0, off = 0, under = 0, least = 0;
        string first = null;
        var atFull = new int[aspects.Length];
        for (int a = 0; a < aspects.Length; a++)
        {
            foreach (float zoom in zooms)
            {
                cases++;
                float canvas = 2160f / zoom * aspects[a];
                float data = canvas - DataRight - DataBox;   // the money's box: as wide as the money, the date and the time can ever be
                int tiles = PagerRules.StripTiles(data - begin, RowPitch, RowSpacing, 7);
                if (zoom == 1f)
                {
                    atFull[a] = tiles;
                }
                float end = begin + PagerRules.RowWidth(tiles, RowPitch, RowSpacing);   // a full page and its button
                bool fits = PagerRules.Fit(data - begin, RowPitch, RowSpacing) >= PagerRules.LeastTiles;
                bool onScreen = end <= canvas, clear = end <= data || !fits && tiles == PagerRules.LeastTiles;
                off += onScreen ? 0 : 1;
                under += clear ? 0 : 1;
                least += fits ? 0 : 1;
                if ((!onScreen || !clear) && first == null)
                {
                    first = named[a] + " at " + (zoom * 100f).ToString("0") + "%: " + tiles + " tiles end at " + end.ToString("0") + ", the canvas is " + canvas.ToString("0")
                            + " wide and the money's box begins at " + data.ToString("0");
                }
            }
        }
        Check(off == 0 && under == 0, "on every screen the game allows a full page and its button stay on the screen and clear of the money's box (the least page of two where the screen leaves no room for it): "
                                      + off + " off the screen, " + under + " under the box, of " + cases + (first != null ? " (first: " + first + ")" : string.Empty));
        Check(atFull[2] == 6 && atFull[1] == 5 && atFull[0] == 2, "at 100% a page is 6 tiles on 16:9 (5 apps and the button), 5 on 16:10, 2 on 4:3 ("
                                                                   + atFull[2] + ", " + atFull[1] + ", " + atFull[0] + ")");
        Check(PagerRules.StripTiles(1e9f, RowPitch, RowSpacing, 7) == 7 && PagerRules.StripTiles(-500f, RowPitch, RowSpacing, 7) == 2 && PagerRules.StripTiles(float.NaN, RowPitch, RowSpacing, 7) == 2,
            "a room with no end is the game's own row, no room the least page of two");
        Console.WriteLine("   " + cases + " screens (4:3, 16:10, 16:9 at 50% to 200%): " + off + " with the button off the screen, " + under + " under the money's box, "
                          + least + " with room for less than two (the least page); at 100% 16:9 " + atFull[2] + ", 16:10 " + atFull[1] + ", 4:3 " + atFull[0] + " tiles.");
    }

    // The count is worked out for a bar's width and kept: every opening on the same screen, with any money, date and
    // language, keeps it; only another width works it out again. As the pager does: measure when Remeasure says so.
    private static void PagerOnce()
    {
        Check(PagerRules.Remeasure(3840f, float.NaN) && !PagerRules.Remeasure(3840f, 3840f) && !PagerRules.Remeasure(3840.3f, 3840f) && PagerRules.Remeasure(3456f, 3840f),
            "the row's tiles are worked out at the first opening and again only when the bar's width changes");
        float begin = RowCentre - PagerRules.RowWidth(7, RowPitch, RowSpacing) * 0.5f;
        float[] widths = { 3840f, 3840f, 3840f, 3456f, 3456f, 2880f, 3840f };
        float measuredFor = float.NaN;
        int tiles = 0, measured = 0, changed = 0, wrong = 0;
        var random = new Random(20261015);
        foreach (float width in widths)
        {
            for (int opening = 0; opening < 50; opening++)
            {
                // The money (0 to 760 wide), the date (any language, 260 to 700) and the time change at every opening; the box does not.
                float moneyWords = (float)random.NextDouble() * 760f, dateWords = 260f + (float)random.NextDouble() * 440f;
                float box = width - DataRight - DataBox;
                int before = tiles;
                if (PagerRules.Remeasure(width, measuredFor))
                {
                    tiles = PagerRules.StripTiles(box - begin, RowPitch, RowSpacing, 7);
                    measuredFor = width;
                    measured++;
                }
                changed += opening > 0 && tiles != before ? 1 : 0;
                wrong += moneyWords + dateWords < 0f ? 1 : 0;
            }
        }
        Check(measured == 4 && changed == 0 && wrong == 0, "350 openings on four changes of screen: the count is worked out 4 times, and never changes between openings on one screen (" + measured + " times, " + changed + " changes)");
    }

    // The money, the date and the time as the game draws them (Topbar/RightData, level4): the money's figure in 36 after
    // its icon's margin of 45, its change in 30 after a margin of 35, 20 between them; the date and the time each an icon
    // of 32, 20, then words in 36; 50 between the three; all ending 100 from the bar's right edge.
    private const float MoneyFixed = 45f + 20f + 35f + 50f + 32f + 20f + 50f + 32f + 20f, BigWords = 36f, SmallWords = 30f;

    // How wide words are drawn, never less: a letter or figure 0.62 of the size (the game's font sets "$1.9B +$16.9K",
    // "Saturday (Day 6)" and "04:03" in about 900 with its icons; this reckons 974), a space 0.3, a Chinese character the
    // whole size.
    private static float Drawn(string words, float size)
    {
        float width = 0f;
        foreach (char c in words)
        {
            width += c >= '\u2E80' ? 1f : c == ' ' ? 0.3f : 0.62f;
        }
        return width * size;
    }

    // The widest the money, the date and the time can be drawn in the seven languages the mod speaks: a money of ten
    // characters and its change, the longest day of the week in the game's own words and day 9999, the time 23:59.
    private static float WidestData(out string widestDate)
    {
        string managed = Environment.GetEnvironmentVariable("BA_MANAGED") ?? DefaultManaged;
        string locale = Path.GetFullPath(Path.Combine(managed, @"..\StreamingAssets\locale"));
        string[] days = { "common_monday", "common_tuesday", "common_wednesday", "common_thursday", "common_friday", "common_saturday", "common_sunday" };
        float date = 0f;
        widestDate = null;
        foreach (string lang in ModLanguages.All)
        {
            string path = Path.Combine(locale, lang + ".json");
            if (!File.Exists(path))
            {
                widestDate = null;
                return float.NaN;
            }
            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path)))
            {
                string format = doc.RootElement.TryGetProperty("topbar_date_format", out JsonElement f) ? f.GetString() : null;
                if (format == null)
                {
                    return float.NaN;
                }
                foreach (string day in days)
                {
                    string name = doc.RootElement.TryGetProperty(day, out JsonElement d) ? d.GetString() : string.Empty;
                    string shown = format.Replace("{DayOfWeek}", name).Replace("{CurrentNumberDay}", "9999");
                    if (Drawn(shown, BigWords) > date)
                    {
                        date = Drawn(shown, BigWords);
                        widestDate = shown;
                    }
                }
            }
        }
        return MoneyFixed + Drawn("$9,999.99M", BigWords) + Drawn("-$999.99K", SmallWords) + date + Drawn("23:59", BigWords);
    }

    // A full page and its button stand centred in the room from where the game's own row begins to the money's box, at
    // 16:9, 16:10 and 4:3; the button in the same place on every page, a short last page from the same left edge; and the
    // row never reaches the money, the date and the time drawn at their widest, on any screen the game allows.
    private static void PagerCentre()
    {
        float begin = RowCentre - PagerRules.RowWidth(7, RowPitch, RowSpacing) * 0.5f;
        float[] aspects = { 16f / 9f, 16f / 10f, 4f / 3f };
        string[] named = { "16:9", "16:10", "4:3" };
        var said = new List<string>();
        bool centred = true;
        for (int a = 0; a < aspects.Length; a++)
        {
            float canvas = 2160f * aspects[a];
            float room = canvas - DataRight - DataBox - begin;
            int tiles = PagerRules.StripTiles(room, RowPitch, RowSpacing, 7);
            float start = PagerRules.PageStart(begin, room, tiles, RowPitch, RowSpacing);
            float left = start - begin, right = begin + room - (start + PagerRules.RowWidth(tiles, RowPitch, RowSpacing));
            centred &= Math.Abs(left - right) <= 2f && left >= 0f && right >= 0f;
            said.Add(named[a] + " " + tiles + " tiles, " + left.ToString("0") + " left and " + right.ToString("0") + " right");
        }
        Check(centred, "at 100% a full page and its button stand centred in the room between the game's row's start and the money's box (" + string.Join("; ", said) + ")");
        Console.WriteLine("   centred at 100%: " + string.Join("; ", said) + ".");
        Check(PagerRules.PageStart(820f, 300f, 2, RowPitch, RowSpacing) == 820f && PagerRules.PageStart(820f, float.NaN, 6, RowPitch, RowSpacing) == 820f
              && PagerRules.PageStart(820f, 1420f, 6, RowPitch, RowSpacing) == 820f,
            "a page as wide as its room or wider, or a room not measured yet, starts where the game's own row starts");

        // The button in one place on every page of 21 apps, the apps from the page's left edge, at each screen's count.
        bool still = true;
        foreach (float aspect in aspects)
        {
            float room = 2160f * aspect - DataRight - DataBox - begin;
            int tiles = PagerRules.StripTiles(room, RowPitch, RowSpacing, 7);
            float start = PagerRules.PageStart(begin, room, tiles, RowPitch, RowSpacing);
            float button = float.NaN;
            for (int page = 0; page < PagerRules.Pages(21, tiles); page++)
            {
                float here = start + PagerRules.ButtonTile(tiles) * RowPitch;   // the button's place: no part of it is the page
                float firstApp = float.NaN, lastApp = float.NaN;
                for (int i = 0; i < 21; i++)
                {
                    if (PagerRules.PageOf(i, 21, tiles) == page)
                    {
                        float x = start + PagerRules.TileOf(i, 21, tiles) * RowPitch;
                        firstApp = float.IsNaN(firstApp) ? x : firstApp;
                        lastApp = x;
                    }
                }
                still &= (float.IsNaN(button) || here == button) && firstApp == start && lastApp < here;
                button = here;
            }
        }
        Check(still, "the More apps button stands in the same place on every page, and every page, the short last one too, begins at the full page's left edge");

        // Never over the money, the date and the time at their widest.
        float widest = WidestData(out string widestDate);
        if (float.IsNaN(widest))
        {
            Check(false, "the game's own words for the date are not in its StreamingAssets/locale");
            return;
        }
        Check(widest <= DataBox, "the money, the date and the time at their widest (" + widest.ToString("0") + ", the date \"" + widestDate + "\") fit the box the game keeps for them (" + DataBox + ")");
        int screens = 0, over = 0;
        string first = null;
        foreach (float aspect in new[] { 4f / 3f, 16f / 10f, 16f / 9f })
        {
            foreach (float zoom in new[] { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 1.75f, 2f })
            {
                float canvas = 2160f / zoom * aspect;
                float room = canvas - DataRight - DataBox - begin;
                if (PagerRules.Fit(room, RowPitch, RowSpacing) < PagerRules.LeastTiles)
                {
                    continue;   // (no room for the least page: the game's own row is over the money there too)
                }
                screens++;
                int tiles = PagerRules.StripTiles(room, RowPitch, RowSpacing, 7);
                float end = PagerRules.PageStart(begin, room, tiles, RowPitch, RowSpacing) + PagerRules.RowWidth(tiles, RowPitch, RowSpacing);
                float money = canvas - DataRight - widest;
                if (end > money)
                {
                    over++;
                    first = first ?? aspect.ToString("0.00") + " at " + zoom + "x: the row ends at " + end.ToString("0") + ", the money begins at " + money.ToString("0");
                }
            }
        }
        Check(over == 0, "on every screen with room for a page the row ends before the money, the date and the time at their widest: " + over + " of " + screens + " over"
                         + (first != null ? " (first: " + first + ")" : string.Empty));
        float atFull = 3840f - DataRight - widest - (PagerRules.PageStart(begin, 3840f - DataRight - DataBox - begin, 6, RowPitch, RowSpacing) + PagerRules.RowWidth(6, RowPitch, RowSpacing));
        Console.WriteLine("   the money, date and time at their widest: " + widest.ToString("0") + " (\"" + widestDate + "\"); at 16:9 100% the row ends " + atFull.ToString("0")
                          + " before them; a seventh tile would need " + (RowPitch - atFull).ToString("0") + " more.");
    }

    // The mod's apps in one fixed order on both phones, whatever order their features put them on in; the game's first,
    // other mods' last in the order they stand.
    private static void PagerModOrder()
    {
        string[] names = PagerRules.ModOrder;
        Check(names.Distinct().Count() == names.Length && names.All(n => n.StartsWith("RealLife", StringComparison.Ordinal)), "the mod's order names each of its apps once");
        bool ranks = true;
        for (int i = 0; i < names.Length; i++)
        {
            ranks &= PagerRules.ModRank(names[i]) == i * 100 && (i == 0 || PagerRules.ModRank(names[i]) > PagerRules.ModRank(names[i - 1]));
        }
        Check(ranks, "each app's place is its place in the table");
        Check(PagerRules.ModRank("RealLifeBanks") > PagerRules.ModRank("RealLifeFinance") && PagerRules.ModRank("RealLifeBanks") < PagerRules.ModRank("RealLifeOwnBank")
              && PagerRules.ModRank("RealLifeBank1") == -1 && PagerRules.ModRank("RealLifeBank") == -1 && PagerRules.IndexOf("RealLifeBanks") == Array.IndexOf(names, "RealLifeBanks"),
            "the one Banks app stands between Finance and the player's own bank; the old app of each bank has no place any more");
        Check(PagerRules.ModRank("Persona") == -1 && PagerRules.ModRank("GoingPublicHub") == -1 && PagerRules.ModRank(null) == -1 && PagerRules.ModRank(string.Empty) == -1,
            "the game's names and other mods' have no place among the mod's");
        // Every order the same apps could be put on in comes out the same.
        var random = new Random(20102026);
        string[] mixed = { "BizMan", "Persona", "RealLifeSettings", "RealLifeOwnBank", "OtherModHub", "RealLifePlanner", "RealLifeBanks", "RealLifeStocks", "OtherModChat", "RealLifeFlare", "RealLifeGuide" };
        bool[] gameOf = mixed.Select(n => n == "BizMan" || n == "Persona").ToArray();
        string want = null;
        int faults = 0;
        for (int round = 0; round < 400; round++)
        {
            int[] at = Enumerable.Range(0, mixed.Length).OrderBy(_ => random.Next()).ToArray();
            // the game's own keep their order among themselves, as the game makes them first; the rest come in any order
            var standing = at.Where(i => gameOf[i]).OrderBy(i => i).Concat(at.Where(i => !gameOf[i])).ToArray();
            var game = standing.Select(i => gameOf[i]).ToArray();
            var rank = standing.Select(i => gameOf[i] ? -1 : PagerRules.ModRank(mixed[i])).ToArray();
            var order = new int[standing.Length];
            int games = PagerRules.Arrange(game, rank, standing.Length, order);
            var mods = order.Skip(games).Select(o => mixed[standing[o]]).Where(n => n.StartsWith("RealLife", StringComparison.Ordinal));
            string got = string.Join(",", order.Take(games).Select(o => mixed[standing[o]])) + "|" + string.Join(",", mods);
            want ??= got;
            faults += got == want && order.Distinct().Count() == order.Length && games == 2
                      && order.Skip(games).Select(o => mixed[standing[o]]).SkipWhile(n => n.StartsWith("RealLife", StringComparison.Ordinal)).All(n => n.StartsWith("OtherMod", StringComparison.Ordinal)) ? 0 : 1;
        }
        Check(faults == 0 && want == "BizMan,Persona|RealLifePlanner,RealLifeFlare,RealLifeBanks,RealLifeOwnBank,RealLifeStocks,RealLifeGuide,RealLifeSettings",
            "400 orders of putting on: the game's first, the mod's always Planner, Flare, the banks, your bank, Stocks, Guide, options, other mods' last (" + want + "; " + faults + " wrong)");
        var two = new int[3];
        Check(PagerRules.Arrange(new[] { false, false, false }, new[] { 300, 300, -1 }, 3, two) == 0 && two.SequenceEqual(new[] { 0, 1, 2 }), "equal places keep the order they stand in");
        Check(PagerRules.Arrange(new[] { true, false }, null, 2, two) == 1, "with no places it is the game's first, the rest as they stand");
    }

    // The game's own apps first, in their order, then the others in theirs; stable.
    private static void PagerGameFirst()
    {
        var order = new int[8];
        int games = PagerRules.GameFirst(new[] { false, true, true, false, true, false, true, true }, 8, order);
        Check(games == 5 && order.SequenceEqual(new[] { 1, 2, 4, 6, 7, 0, 3, 5 }), "the game's apps come first in their own order, then the others in theirs (" + string.Join(", ", order) + ")");
        Check(PagerRules.GameFirst(null, 3, order) == 0 && PagerRules.GameFirst(new bool[2], 0, order) == 0 && PagerRules.GameFirst(new[] { true, true, true }, 5, new int[2]) == 2,
            "no items, or arrays shorter than the count, order what there is");
        var random = new Random(15102026);
        int faults = 0;
        for (int round = 0; round < 500; round++)
        {
            int count = 1 + random.Next(40);
            var game = new bool[count];
            for (int i = 0; i < count; i++)
            {
                game[i] = random.Next(3) == 0;
            }
            var a = new int[count];
            var b = new int[count];
            int g = PagerRules.GameFirst(game, count, a);
            PagerRules.GameFirst(game, count, b);
            bool ok = a.SequenceEqual(b) && a.Distinct().Count() == count && g == game.Count(x => x);
            for (int i = 1; i < count; i++)
            {
                ok &= game[a[i - 1]] || !game[a[i]];                                   // never one of the game's after another's
                ok &= game[a[i - 1]] != game[a[i]] || a[i - 1] < a[i];                 // each kind in the order it stands
            }
            faults += ok ? 0 : 1;
        }
        Check(faults == 0, "500 mixed rows of up to 40: the game's apps always first and in order, the others after in order, the same every time (" + faults + " wrong)");
        // The game's seven on pages of 6 tiles (5 and the button), with 14 of the mod's after them: the game's five on the first page, its last two on the second.
        bool pages = true;
        for (int i = 0; i < 7; i++)
        {
            pages &= PagerRules.PageOf(i, 21, 6) == (i < 5 ? 0 : 1);
        }
        Check(pages, "the game's seven on a 16:9 screen: five on the first page and the last two first on the second, before every app of the mod's");
    }

    // The pages fill in order, every one but the last full, and the button on the last tile of each.
    private static void PagerFilling()
    {
        int[] sizes = Sizes(21, 7);
        Check(PagerRules.Pages(21, 7) == 4 && sizes.SequenceEqual(new[] { 6, 6, 6, 3 }), "21 apps at 6 a page: 6, 6, 6 and 3 (" + string.Join(", ", sizes) + ")");
        bool order = true;
        for (int i = 0; i < 21; i++)
        {
            order &= PagerRules.PageOf(i, 21, 7) == i / 6 && PagerRules.TileOf(i, 21, 7) == i % 6;
        }
        Check(order, "21 apps: the first six on the first page in order, the next six on the second, and so on; each from the page's first tile");
        bool still = true;
        for (int page = 0; page < 4; page++)
        {
            still &= PagerRules.ButtonTile(7) == 6 && PagerRules.TileOf(page * 6 + PagerRules.PageSize(page, 21, 7) - 1, 21, 7) < PagerRules.ButtonTile(7);
        }
        Check(still, "on each of the four pages the button stands on the seventh tile, after the page's last app, the short last page too");
        bool free = true;
        foreach (int tiles in new[] { 7, PagerRules.PhoneTiles })
        {
            for (int count = tiles + 1; count <= 120; count++)
            {
                for (int i = 0; i < count; i++)
                {
                    free &= PagerRules.TileOf(i, count, tiles) != PagerRules.ButtonTile(tiles);
                }
            }
        }
        Check(free, "no child the layout places is ever given the button's tile, on the row or the phone, for any number of them");
        int[] phone = Sizes(9, PagerRules.PhoneTiles);
        Check(PagerRules.Pages(8, PagerRules.PhoneTiles) == 1 && phone.SequenceEqual(new[] { 7, 2 }) && PagerRules.PerPage(8, 8) == 8,
            "the HUD phone: the game's eight fill one page; nine are seven and two, the button on the eighth tile (" + string.Join(", ", phone) + ")");
        Check(Sizes(31, 8).SequenceEqual(new[] { 7, 7, 7, 7, 3 }) && Sizes(14, 7).SequenceEqual(new[] { 6, 6, 2 }) && Sizes(13, 7).SequenceEqual(new[] { 6, 6, 1 }),
            "31 on the phone are 7, 7, 7, 7 and 3; 14 on the row 6, 6 and 2; 13 are 6, 6 and 1");

        // Every count from none to 200 on pages of 2 to 12 tiles: as few pages as hold them, all full but the last, in order.
        int cases = 0, faults = 0;
        string first = null;
        for (int tiles = 2; tiles <= 12; tiles++)
        {
            for (int count = 0; count <= 200; count++)
            {
                cases++;
                string fault = FillFault(count, tiles);
                if (fault != null)
                {
                    faults++;
                    first = first ?? fault;
                }
            }
        }
        Check(faults == 0, "0 to 200 apps on pages of 2 to 12 tiles: " + faults + " of " + cases + " filled wrong" + (first != null ? " (first: " + first + ")" : string.Empty));
    }

    private static int[] Sizes(int count, int tiles)
    {
        int pages = PagerRules.Pages(count, tiles);
        var sizes = new int[pages];
        for (int p = 0; p < pages; p++)
        {
            sizes[p] = PagerRules.PageSize(p, count, tiles);
        }
        return sizes;
    }

    private static string FillFault(int count, int tiles)
    {
        int pages = PagerRules.Pages(count, tiles);
        int each = count <= tiles ? tiles : tiles - 1;
        int least = count <= tiles ? 1 : (count + each - 1) / each;
        if (pages != least)
        {
            return count + " at " + tiles + ": " + pages + " pages, wanted " + least;
        }
        if (PagerRules.ButtonShown(pages) != count > tiles)
        {
            return count + " at " + tiles + ": the button " + (PagerRules.ButtonShown(pages) ? "shown" : "hidden");
        }
        int sum = 0;
        for (int p = 0; p < pages; p++)
        {
            int size = PagerRules.PageSize(p, count, tiles);
            sum += size;
            if (p < pages - 1 && size != each || p == pages - 1 && count > 0 && (size < 1 || size > each))
            {
                return count + " at " + tiles + ": page " + p + " holds " + size;
            }
            if (pages > 1 && size > PagerRules.ButtonTile(tiles))
            {
                return count + " at " + tiles + ": page " + p + " runs onto the button's tile";
            }
        }
        if (sum != count || PagerRules.PageSize(pages, count, tiles) != 0)
        {
            return count + " at " + tiles + ": the pages hold " + sum;
        }
        for (int i = 0; i < count; i++)
        {
            int page = PagerRules.PageOf(i, count, tiles);
            if (page != i / each || PagerRules.TileOf(i, count, tiles) != i - page * each)
            {
                return count + " at " + tiles + ": app " + i + " on page " + page;
            }
        }
        return null;
    }

    // The button: shown only with more than one page, going on and round, showing the page from 1; the wheel stops at the ends.
    private static void PagerButton()
    {
        Check(PagerRules.Next(0, 4) == 1 && PagerRules.Next(2, 4) == 3 && PagerRules.Next(3, 4) == 0, "the button goes to the next page, and from the last back to the first");
        Check(PagerRules.Next(0, 1) == 0 && PagerRules.Next(5, 3) == 0 && PagerRules.Next(-1, 3) == 0, "with one page, or a page past the last, the button goes to the first");
        Check(PagerRules.Wheel(0, 4, true) == 1 && PagerRules.Wheel(3, 4, true) == 3 && PagerRules.Wheel(0, 4, false) == 0 && PagerRules.Wheel(2, 4, false) == 1,
            "the wheel turns one page on or back and stops at either end, as before");
        PagerRules.Shown(1, 3, out int shown, out int of);
        PagerRules.Shown(0, 1, out int alone, out int ofAlone);
        PagerRules.Shown(7, 3, out int past, out int ofPast);
        Check(shown == 2 && of == 3 && alone == 1 && ofAlone == 1 && past == 3 && ofPast == 3, "the page is shown counted from 1: the second of three is 2 of 3, never past the last");
        Check(!PagerRules.ButtonShown(PagerRules.Pages(7, 7)) && !PagerRules.ButtonShown(PagerRules.Pages(8, 8)) && !PagerRules.ButtonShown(PagerRules.Pages(0, 7))
              && PagerRules.ButtonShown(PagerRules.Pages(8, 7)) && PagerRules.ButtonShown(PagerRules.Pages(9, 8)),
            "one page has no button (the game's own seven on the row, the game's eight on the phone, none); one app more and it is there");
    }

    private static void PagerEdges()
    {
        Check(PagerRules.Pages(5, 0) == PagerRules.Pages(5, 2) && PagerRules.Pages(5, 1) == 5 && PagerRules.PerPage(5, -3) == 1, "fewer than two tiles a page count as two: one app and the button");
        Check(PagerRules.Pages(0, 7) == 1 && PagerRules.PageSize(0, 0, 7) == 0 && PagerRules.PageOf(0, 0, 7) == 0, "no apps are one empty page");
        Check(PagerRules.Pages(1, 7) == 1 && PagerRules.PageSize(0, 1, 7) == 1, "one app is one page of one");
        Check(PagerRules.PageOf(-1, 21, 7) == 0 && PagerRules.PageOf(50, 21, 7) == 3 && PagerRules.PageSize(-1, 21, 7) == 0 && PagerRules.TileOf(-2, 21, 7) == 0,
            "a place before the first or past the last is on the first or last page, a page before the first holds nothing");
        Check(PagerRules.Fit(0f, RowPitch, RowSpacing) == 0 && PagerRules.Fit(float.NaN, RowPitch, RowSpacing) == 0 && PagerRules.Fit(500f, 0f, 0f) == 0 && PagerRules.RowWidth(0, RowPitch, RowSpacing) == 0f,
            "no room, no pitch or no tiles fit nothing and are no width");
        Check(PagerRules.Pages(1000, 7) == 167 && PagerRules.PageSize(166, 1000, 7) == 4, "a thousand apps are 166 full pages and one of four");
        bool same = true;
        for (int round = 0; round < 3; round++)
        {
            same &= Sizes(47, 7).SequenceEqual(Sizes(47, 7)) && PagerRules.PageOf(33, 47, 7) == 5;
        }
        Check(same, "the same apps fall on the same pages every time");
    }

    private static void PagerTiming()
    {
        Pass(1000);   // warmed
        var clock = new Stopwatch();   // (made before the count: the clock itself is an allocation)
        long before = GC.GetAllocatedBytesForCurrentThread();
        clock.Start();
        int sum = Pass(20000);
        clock.Stop();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        double each = clock.Elapsed.TotalMilliseconds / 20000.0;
        Check(sum > 0 && each < 0.01, "the pages of 30 apps are worked out in " + each.ToString("0.00000") + " ms, wanted under 0.01 ms");
        Check(bytes == 0, "working out the pages allocates nothing (" + bytes + " bytes over 20,000 passes)");
        Console.WriteLine("   the pages of 30 apps worked out in " + each.ToString("0.00000") + " ms, " + bytes + " bytes.");
    }

    // One pass as the pager makes it: the pages, then each app's page.
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int Pass(int times)
    {
        int sum = 0;
        for (int t = 0; t < times; t++)
        {
            int pages = PagerRules.Pages(30, 7);
            for (int i = 0; i < 30; i++)
            {
                sum += PagerRules.PageOf(i, 30, 7);
            }
            sum += PagerRules.Next(t % pages, pages) + PagerRules.ButtonTile(7);
        }
        return sum;
    }

    // The pager's source: PhoneGridPager.cs and its parts, or null if any is missing.
    private static string PagerSource(string scripts)
    {
        string all = string.Empty;
        foreach (string part in new[] { "", ".View", ".Layout", ".More", ".Aside" })
        {
            string text = Read(scripts, "Core/GameUi/PhoneGridPager" + part + ".cs");
            if (text == null)
            {
                return null;
            }
            all += text + "\n";
        }
        return all;
    }

    private static void PagerSources(string scripts, string locales)
    {
        string pager = PagerSource(scripts);
        if (pager == null)
        {
            Check(false, "the pager's source is not where it was");
            return;
        }
        // The count reads the money's box, never its words, and only when the bar's width has changed.
        string pages = Body(pager, "private void StripPages()");
        Check(pages != null && Regex.IsMatch(pages, @"if \(PagerRules\.Remeasure\(barWidth, _measuredFor\) && strip\.gameObject\.activeInHierarchy\)\s*\{[^}]*_room = DataLeft\(strip\) - rowStart;\s*_tiles = PagerRules\.StripTiles\(_room, pitch, layout\.spacing, _gameApps\);\s*_measuredFor = barWidth;")
              && pages.Contains("_pagesLeft = PagerRules.PageStart(rowStart, _room, Tiles, pitch, layout.spacing);")
              && pages.Contains("float barWidth = strip.parent is RectTransform bar ? bar.rect.width : 0f;")
              && pages.Contains("Pages = PagerRules.Pages(count, ItemTiles)") && !pages.Contains("RightLimit(") && !pages.Contains("_rightData"),
            "the full menu's row works out its tiles from the money's box only when the bar's width has changed, never from the money or the date");
        string box = Body(pager, "private float DataLeft(RectTransform strip)");
        Check(box != null && box.Contains("_rightData.GetWorldCorners(Corners)") && !box.Contains("GetChild") && !box.Contains("InChildren") && !box.Contains("ForceRebuild"),
            "the money's box is read as the box itself, never the money's or the date's words");
        Check(pager.Contains("public sealed class RowBarWatch") && Body(pager, "private void OnRectTransformDimensionsChange()") != null && pager.Contains("watch.Pager = pager;"),
            "a change of the bar's width (resolution, shape, scale) has the row looked at again");
        string first = Body(pager, "private void GameFirst()");
        Check(Body(pager, "private void Collect()")?.Contains("GameFirst();") == true && first != null && first.Contains("PagerRules.Arrange(_isGame, _rank, count, _order)")
              && first.Contains("_apps[i].SetAsLastSibling()") && first.Contains("if (_gameApps < 0)") && first.Contains("PagerRules.ModRank(found.name)")
              && first.Contains("(at < lastGame || at < before)"),
            "the game's own apps are put first in their order, found once, the mod's in their fixed order, and buttons standing among the game's or out of that order are moved after them in it");
        // Every app of the mod's that puts buttons on the phones has its place in the fixed order.
        var ids = new List<string>();
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(file), "const string \\w*AppId = \"(RealLife\\w+)\""))
            {
                ids.Add(m.Groups[1].Value);
            }
        }
        var unplaced = ids.Where(id => !PagerRules.ModOrder.Contains(id)).ToList();
        Check(ids.Count >= 20 && unplaced.Count == 0, "every app of the mod's has its place in the one order of the phones (" + ids.Count + " apps; not placed: " + string.Join(", ", unplaced) + ")");
        Check(Regex.Matches(pager, @"RightLimit\(").Count == 2 && Body(pager, "private void StripCheck()")?.Contains("RightLimit(strip)") == true,
            "the money's edge is read only for the line in Player.log (StripCheck), never for the count");
        Check(Regex.Matches(pager, @"_tiles = ").Count == 1 && Regex.Matches(pager, @"Enum\.TryParse").Count == 1, "the row's count is set in one place, and the game's own apps are found in one place");
        string collect = Body(pager, "private void Collect()");
        Check(collect != null && collect.Contains("Ignored(placed)") && collect.Contains("child == _template") && !collect.Contains("AppButton"),
            "every child the layout places is one of the page's items, other mods' buttons too (Going Public's Business, Chat and Hub): none is laid out under the button");
        Check(pager.Contains("Guard.Listener<AppName>(BadgeChanged)") && pager.Contains("SmartphoneUI.OnUpdatedBadgeCount.AddListener(_onBadge)")
              && pager.Contains("SmartphoneUI.OnUpdatedBadgeCount.RemoveListener(_onBadge)") && Body(pager, "private void Paint()")?.Contains("BadgeUp(_items[i])") == true
              && Body(pager, "private void Paint()")?.Contains("waiting == _badgeShown") == true,
            "the button's badge counts every badge up on the pages not showing, the game's own too, hears the game's word once and is written only when it changes");
        Check(Body(pager, "private bool MakeMore()")?.Contains("rect.anchorMin = row.anchorMin;") == true && Body(pager, "private void More()")?.Contains("SetSizeWithCurrentAnchors") == true,
            "the button is anchored as the row or grid is, so it moves with them");
        Check(Body(pager, "private bool SomeoneElse()")?.Contains("GetName()") == false && pager.Contains("KnownScripts[type] = known;"), "each kind of script on the grid is looked at once");
        string refresh = Body(pager, "public void Refresh()");
        Check(refresh != null && refresh.Contains("Pages = PagerRules.Pages(_items.Count, ItemTiles)") && Body(pager, "private int PageOf(int index)")?.Contains("PagerRules.PageOf(index, _items.Count, ItemTiles)") == true,
            "the HUD phone's pages and every item's page come from the rules (the view's items: every app without folders)");
        string place = Body(pager, "private void PlaceStripPage()");
        Check(place != null && place.Contains("float begin = _pagesLeft;") && !place.Contains("PageSize("), "a short page of the row stands where a full one does: same left edge");
        string grid = Body(pager, "private void PlaceGridPage()");
        Check(grid != null && grid.Contains("GridLeft(grid, built)") && grid.Contains("RestoreRow()"), "a short page of the HUD phone keeps the full page's left edge, and one page is the game's grid as built");

        // The button: on the last tile, shown with pages, turning on and round, its words from the texts.
        string more = Body(pager, "private void More()");
        Check(more != null && more.Contains("PagerRules.ButtonShown(Pages)") && more.Contains("Tile(MoreTile,") && pager.Contains(": PagerRules.ButtonTile(Tiles);"),
            "the More apps button stands on the page's last tile (the last above the dock, on folders' pages the last after the Back tile) and only while there are pages");
        Check(Body(pager, "private void NextPage()")?.Contains("PagerRules.Next(_page, Pages)") == true && pager.Contains("UiClone.Rewire(copy.transform, NextPage)"),
            "the button goes to the next page and round, through the mod's guarded click");
        string make = Body(pager, "private bool MakeMore()");
        Check(make != null && make.Contains("UiClone.Asleep(_template.gameObject") && make.Contains("UiClone.StripGameScripts(copy, KeptOnMore)") && make.Contains("MoreIcon(picture.sprite)"),
            "the button is a copy of the game's app button, without the scripts that make it an app, with a painted icon");
        Check(pager.Contains("\"Icon-Arrows\"") && pager.Contains("AppIconPainter.Paint(like, glyph, MoreTop, MoreBottom, GlyphShare, MoreIconName, MoreInk)") && pager.Contains("DrawnGlyph()"),
            "its picture is the game's double chevron in dark ink on a light grey tile, or one drawn the same way");
        Check(!pager.Contains("PageDots") && !pager.Contains("DotSize") && !pager.Contains("_dotImages"), "the page dots are gone");
        Check(!Regex.IsMatch(pager, @"\bvoid (Update|LateUpdate|FixedUpdate)\("), "the pager has nothing that runs every frame");
        Check(Regex.Matches(pager, @"catch \(Exception fault\)\s*\{\s*Guard\.Fail\(FrameGuard, fault\);").Count == 2, "the wheel over the row and over the button are behind the guard");

        // The texts, in the three languages, and What's new.
        var keys = Regex.Matches(pager, @"""(reallife_apps_[a-z_]+)""").Select(m => m.Groups[1].Value).Distinct().ToList();
        Check(keys.Count == 2 && keys.Contains("reallife_apps_more") && keys.Contains("reallife_apps_page"), "the button's two texts are named in the pager (" + string.Join(", ", keys) + ")");
        foreach (string lang in new[] { "en", "pt", "zh-cn" })
        {
            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(locales, lang + ".json"))))
            {
                string name = doc.RootElement.TryGetProperty("reallife_apps_more", out JsonElement n) ? n.GetString() : null;
                string page = doc.RootElement.TryGetProperty("reallife_apps_page", out JsonElement p) ? p.GetString() : null;
                Check(!string.IsNullOrWhiteSpace(name) && page != null && page.Contains("{page}") && page.Contains("{pages}"),
                    "the button's name and its page with {page} and {pages} are in " + lang + ".json");
                if (lang == "en")
                {
                    Check(name == "More apps" && page.Replace("{page}", "2").Replace("{pages}", "3") == "2 / 3", "in English the button reads More apps and the second of three pages 2 / 3");
                }
                bool told = false, guide = true;
                foreach (JsonProperty row in doc.RootElement.EnumerateObject())
                {
                    if (row.Name.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Name.EndsWith("_title", StringComparison.Ordinal) && row.Value.ValueKind == JsonValueKind.String)
                    {
                        string said = row.Value.GetString();
                        int at = name != null ? said.IndexOf(name, StringComparison.Ordinal) : -1;
                        string fixedWord = lang == "en" ? "Fixed: " : lang == "pt" ? "Corrigido: " : "已修复：";
                        told |= at > 0 && said.LastIndexOf(fixedWord, at, StringComparison.Ordinal) >= 0;
                    }
                    if (row.Name == "reallife_guide_start" || row.Name == "reallife_guide_planner")
                    {
                        guide &= name != null && row.Value.GetString().Contains(name);
                    }
                }
                Check(told, "What's new in " + lang + ".json names the More apps button in a fix's line");
                Check(guide, "the Guide's start page and the Planner's page in " + lang + ".json turn the pages with the More apps button, not the dots");
            }
        }
    }
}
