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
// page is a set number of tiles, the HUD phone's eight and the full menu's as many as the game's own row (worked out once,
// the narrowest room the row can ever have); the pages fill in order and only the last is shorter; the last tile of every
// page is the "More apps" button, which shows the page ("2 / 3"), goes to the next and from the last to the first, and is
// not there when there is one page. And the sources: the count is never measured from the money, the button stands on
// the last tile, the dots are gone, the texts are in all three languages, and What's new says so.
internal static partial class Program
{
    // The game's full menu row (Topbar/AppButtons, level4): a box 1400 wide centred at 1650 of the canvas of 3840, upper
    // centre, spacing 20; the buttons 220 wide; the money, date and time (RightData) end 100 from the canvas's right edge.
    private const float RowCentre = 1650f, RowSpacing = 20f, RowCell = 220f, RowPitch = RowCell + RowSpacing, DataRight = 100f, DataBox = 1350f;

    private static void PagerChecks(string scripts, string locales)
    {
        Section("the phone's pages: a set number of apps a page and the More apps button (Core/GameUi/PagerRules)");
        PagerCount();
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
        int tiles = PagerRules.StripTiles(7);
        Check(tiles >= PagerRules.LeastTiles && PagerRules.StripTiles(0) == PagerRules.StripTiles(1), "a page of the full menu's row has a set number of tiles, counted once (" + tiles + ")");
        float gameWidth = PagerRules.RowWidth(7, RowPitch, RowSpacing);
        Check(gameWidth == 1660f && PagerRules.Fit(gameWidth, RowPitch, RowSpacing) == 7 && PagerRules.Fit(gameWidth - 0.5f, RowPitch, RowSpacing) == 6,
            "the game's own row is 1660 wide and holds its seven, half a pixel less holds six");
        float begin = RowCentre - gameWidth * 0.5f;   // where the game's own row begins, and every page with it
        float[] aspects = { 4f / 3f, 16f / 10f, 16f / 9f };
        string[] named = { "4:3", "16:10", "16:9" };
        float[] zooms = { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 1.75f, 2f };
        int cases = 0, off = 0, under = 0;
        string first = null;
        for (int a = 0; a < aspects.Length; a++)
        {
            foreach (float zoom in zooms)
            {
                cases++;
                float canvas = 2160f / zoom * aspects[a];
                float end = begin + PagerRules.RowWidth(PagerRules.PerPage(21, tiles) + 1, RowPitch, RowSpacing);   // a full page and its button
                float data = canvas - DataRight - DataBox;
                bool onScreen = end <= canvas, clear = end <= data;
                off += onScreen ? 0 : 1;
                under += clear ? 0 : 1;
                if ((!onScreen || !clear) && first == null)
                {
                    first = named[a] + " at " + (zoom * 100f).ToString("0") + "%: the button ends at " + end.ToString("0") + ", the canvas is " + canvas.ToString("0")
                            + " wide and the money's box begins at " + data.ToString("0");
                }
            }
        }
        Check(off == 0 && under == 0, "on every screen the game allows a full page and its button stay on the screen and clear of the money's box: "
                                      + off + " off the screen, " + under + " under the box, of " + cases + (first != null ? " (first: " + first + ")" : string.Empty));
        Console.WriteLine("   " + cases + " screens (4:3, 16:10, 16:9 at 50% to 200%): " + off + " with the button off the screen, " + under + " under the money's box.");
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

    private static void PagerSources(string scripts, string locales)
    {
        string pager = Read(scripts, "Core/GameUi/PhoneGridPager.cs");
        if (pager == null)
        {
            Check(false, "the pager's source is not where it was");
            return;
        }
        // The count is worked out once from the game's own row, never measured from the money.
        string pages = Body(pager, "private void StripPages()");
        Check(pages != null && pages.Contains("_tiles = PagerRules.StripTiles(counted)") && pages.Contains("if (_gameApps < 0)") && pages.Contains("int own = _gameApps;")
              && pages.Contains("Pages = PagerRules.Pages(count, Tiles)") && !pages.Contains("RightLimit(") && !pages.Contains("_rightData"),
            "the full menu's row counts its tiles once from the game's own row and its pages by the rules, never from the money or the date");
        Check(Regex.Matches(pager, @"RightLimit\(").Count == 2 && Body(pager, "private void StripCheck()")?.Contains("RightLimit(strip)") == true,
            "the money's edge is read only for the line in Player.log (StripCheck), never for the count");
        Check(Regex.Matches(pager, @"_tiles = ").Count == 1 && Regex.Matches(pager, @"Enum\.TryParse").Count == 1, "the row's count and the game's own apps are counted in one place, once");
        string collect = Body(pager, "private void Collect()");
        Check(collect != null && collect.Contains("Ignored(placed)") && collect.Contains("child == _template") && !collect.Contains("AppButton"),
            "every child the layout places is one of the page's items, other mods' buttons too (Going Public's Business, Chat and Hub): none is laid out under the button");
        Check(pager.Contains("Guard.Listener<AppName>(BadgeChanged)") && pager.Contains("SmartphoneUI.OnUpdatedBadgeCount.AddListener(_onBadge)")
              && pager.Contains("SmartphoneUI.OnUpdatedBadgeCount.RemoveListener(_onBadge)") && Body(pager, "private void Paint()")?.Contains("BadgeUp(_apps[i])") == true
              && Body(pager, "private void Paint()")?.Contains("waiting == _badgeShown") == true,
            "the button's badge counts every badge up on the pages not showing, the game's own too, hears the game's word once and is written only when it changes");
        Check(Body(pager, "private bool MakeMore()")?.Contains("rect.anchorMin = row.anchorMin;") == true && Body(pager, "private void More()")?.Contains("SetSizeWithCurrentAnchors") == true,
            "the button is anchored as the row or grid is, so it moves with them");
        Check(Body(pager, "private bool SomeoneElse()")?.Contains("GetName()") == false && pager.Contains("KnownScripts[type] = known;"), "each kind of script on the grid is looked at once");
        string refresh = Body(pager, "public void Refresh()");
        Check(refresh != null && refresh.Contains("Pages = PagerRules.Pages(_apps.Count, Tiles)") && Body(pager, "private int PageOf(int index)")?.Contains("PagerRules.PageOf(index, _apps.Count, Tiles)") == true,
            "the HUD phone's pages and every app's page come from the rules");
        string place = Body(pager, "private void PlaceStripPage()");
        Check(place != null && place.Contains("float begin = _pagesLeft;") && !place.Contains("PageSize("), "a short page of the row stands where a full one does: same left edge");
        string grid = Body(pager, "private void PlaceGridPage()");
        Check(grid != null && grid.Contains("GridLeft(grid, built)") && grid.Contains("RestoreRow()"), "a short page of the HUD phone keeps the full page's left edge, and one page is the game's grid as built");

        // The button: on the last tile, shown with pages, turning on and round, its words from the texts.
        string more = Body(pager, "private void More()");
        Check(more != null && more.Contains("PagerRules.ButtonShown(Pages)") && more.Contains("Tile(PagerRules.ButtonTile(Tiles)"), "the More apps button stands on the page's last tile and only while there are pages");
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
