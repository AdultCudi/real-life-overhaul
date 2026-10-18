using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Photos;

// The Gallery (Scripts/Photos/GalleryApp*.cs, GalleryRules.cs): why a photo would not open in 0.27.0 (the grid was a copy
// of the game's switched-off list background, never switched on) and that it opens now on the first tap, and on a second;
// the albums and their contents; the days and their order; the rows built for a view (few, whatever the album's size, found
// by a binary search that allocates nothing); the photo before and after at either end, after a deletion and in the strip;
// the favourites written beside the photos and read back; the look by the game's hour; and the texts in the three locales.
internal static partial class Program
{
    private static void Gallery()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        string scratch = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rlo_gallery_checks_" + Guid.NewGuid().ToString("N"));
        try
        {
            GalleryRootCause();
            GalleryTaps();
            GalleryAlbums();
            GalleryDays();
            GalleryRowsInView();
            GalleryRowsTimed();
            GalleryStepping();
            GalleryFavourites(System.IO.Path.Combine(scratch, "favourites"));
            GalleryLookAndSources();
            GalleryReviewFixes();
            GalleryWords();
        }
        finally
        {
            try
            {
                if (Directory.Exists(scratch))
                {
                    Directory.Delete(scratch, true);
                }
            }
            catch (IOException)
            {
            }
        }
        Console.WriteLine("  gallery: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // Photos of made-up days: 'perDay' photos on each of the days given, an hour apart, every third a selfie.
    private static List<PhotoEntry> GalleryPhotos(int[] days, int perDay)
    {
        var list = new List<PhotoEntry>();
        var start = new DateTime(2026, 10, 1, 9, 0, 0);
        int n = 0;
        foreach (int day in days)
        {
            for (int i = 0; i < perDay; i++, n++)
            {
                var e = new PhotoEntry();
                PhotoStoreRules.Parse(PhotoStoreRules.NameFor(start.AddMinutes(n * 7), n % 3 == 1, day, (8 + i) % 24, 1), e);
                list.Add(e);
            }
        }
        return list;
    }

    // ---------------------------------------------------------------- the root cause

    private static void GalleryRootCause()
    {
        string camera = Read(@"Photos\CameraApp.cs");
        string sheet = Between(camera, "internal static class GallerySheet", "\n}");
        // 0.27.0: GallerySheet.Make copied MyEmployees' ListBackground, which the game keeps switched off, and never switched
        // the copy on; the grid of photos was its child, so it was never drawn and no click reached a photo. A Player.log
        // shows it: a click over the grid met "Layout 30-70/Right/EmployeesList/EnhancedScroller" and the raycast
        // blocker, never the sheet or a photo, and the next click was the Gallery's own app button.
        Check(sheet.Contains("Instantiate(background.gameObject") && Regex.IsMatch(sheet, @"sheet\.SetActive\(true\);\s*(//[^\n]*)?\s*return rect;"),
            "gallery root cause: a copy of the game's switched-off list background is switched on before it is used (it held the grid of photos in 0.27.0)");
        string all = string.Join("\n", Directory.GetFiles(System.IO.Path.Combine(PhotoScripts, "Photos"), "*.cs").Select(File.ReadAllText));
        int copies = Regex.Matches(all, @"Find\(""ListBackground""\)").Count;
        Check(copies == 1, "gallery root cause: the Camera's page is the one place of the Camera and the Gallery that copies the game's list background (" + copies + ")");
        ListBackgroundsOn();
        string gallery = GalleryText();
        string build = Between(gallery, "private GameObject Build(FullMenu menu, Transform dormant)", "private void BuildHeader()");
        Check(Regex.IsMatch(build, @"for \(int i = 0; i < layout\.childCount; i\+\+\)\s*\{\s*layout\.GetChild\(i\)\.gameObject\.SetActive\(false\);")
              && build.Contains("_root = FlareKit.Rect(layout, \"Gallery\");") && !gallery.Contains("GallerySheet"),
            "gallery root cause: the Gallery draws its own screens in the game's frame, as Flare does, on a part of its own that is on (no copy of a switched-off part)");
        string tapped = Between(gallery, "private void Tapped(Slot slot)", "// The grid scrolled");
        Check(gallery.Contains("FlareKit.Click(slot.Back, () => Tapped(slot));") && tapped.Contains("_nav.Tap(slot.Index, _shown.Count)") && tapped.Contains("WriteViewer();")
              && !tapped.Contains("Refresh(") && !tapped.Contains(".Open("),
            "gallery root cause: a tap on a photo shows it whole at once, without writing the app again or opening it");
        Check(Regex.Matches(gallery, @"_app\.Open\(\)").Count == 1 && Between(gallery, "public void Open(bool latest)", "public void Changed()").Contains("_app.Open();"),
            "gallery root cause: the app is opened from one place only (the Camera's buttons and the test), never from a click inside it");
        string mode = Read(@"Photos\CameraMode.cs");
        Check(Between(mode, "private void Gallery()", "// ----").Contains("OpenLatestPhoto()"), "gallery: the viewfinder's small photo opens the newest photo whole, not the grid");
    }

    // Every copy the mod makes of the game's ListBackground, anywhere in the mod, is switched on: the game keeps it switched
    // off, so a copy left as it is draws nothing (the Gallery's grid in 0.27.0; the Stocks and Car Showroom pages' sheet).
    // Switched on where it is made, or kept in a field that is switched on when the page shows (Newsstand, in a part
    // file of its type: Type.cs and Type.Part.cs are read together).
    private static void ListBackgroundsOn()
    {
        var made = new Regex(@"GameObject (\w+) = [^;]*Instantiate\(background\.gameObject");
        var off = new List<string>();
        int copies = 0;
        foreach (string file in Directory.GetFiles(PhotoScripts, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            string stem = System.IO.Path.GetFileName(file).Split('.')[0];
            string type = text;
            foreach (string part in Directory.GetFiles(System.IO.Path.GetDirectoryName(file), stem + ".*.cs"))
            {
                type += File.ReadAllText(part);
            }
            int at = text.IndexOf("Find(\"ListBackground\")", StringComparison.Ordinal);
            while (at >= 0)
            {
                string window = text.Substring(at, Math.Min(4000, text.Length - at));
                Match copy = made.Match(window);
                if (copy.Success)
                {
                    copies++;
                    string name = copy.Groups[1].Value;
                    Match kept = Regex.Match(window, @"(_\w+) = \(RectTransform\)" + name + @"\.transform;");
                    bool on = window.Contains(name + ".SetActive(true);") || (kept.Success && type.Contains(kept.Groups[1].Value + ".gameObject.SetActive("));
                    if (!on)
                    {
                        off.Add(System.IO.Path.GetFileName(file));
                    }
                }
                at = text.IndexOf("Find(\"ListBackground\")", at + 1, StringComparison.Ordinal);
            }
        }
        Check(copies >= 5 && off.Count == 0, "gallery root cause, everywhere: every copy of the game's switched-off list background in the mod is switched on (" + copies + " copies"
                                           + (off.Count > 0 ? "; left off in " + string.Join(", ", off) : string.Empty) + ")");
    }

    // ---------------------------------------------------------------- one tap

    private static void GalleryTaps()
    {
        var nav = new GalleryNav();
        Check(!nav.Viewing && nav.Index == -1, "gallery taps: the Gallery opens on its grid");
        Check(nav.Tap(5, 12) && nav.Viewing && nav.Index == 5, "gallery taps: the first tap on a photo shows it whole at once");
        nav.Back();
        Check(!nav.Viewing && nav.Tap(5, 12) && nav.Tap(5, 12) && nav.Viewing && nav.Index == 5, "gallery taps: a second tap on the same photo shows it (it never takes the Gallery back to its grid)");
        var off = new GalleryNav();
        Check(!off.Tap(12, 12) && !off.Tap(-1, 12) && !off.Tap(0, 0) && !off.Viewing, "gallery taps: a tap where there is no photo does nothing");
        nav.Scroll = 940f;
        nav.Back();
        Check(!nav.Viewing && nav.Scroll == 940f, "gallery taps: back from the photo leaves the grid where it was");
        Check(nav.Switch(GalleryAlbum.Selfies) && nav.Scroll == 0f && !nav.Viewing && nav.Index == -1 && !nav.Switch(GalleryAlbum.Selfies),
            "gallery taps: another album opens on its grid from the top; the same album changes nothing");
    }

    // ---------------------------------------------------------------- the albums

    private static void GalleryAlbums()
    {
        List<PhotoEntry> photos = GalleryPhotos(new[] { 40, 41, 42 }, 5);
        var favourites = new HashSet<string>(StringComparer.Ordinal) { photos[0].Name, photos[7].Name, photos[13].Name, "Photo_20990101_000000_D1_00.jpg" };
        var into = new List<PhotoEntry>();
        GalleryRules.Album(photos, GalleryAlbum.All, favourites, into);
        Check(into.Count == 15 && into.Select(p => p.Name).OrderBy(n => n).SequenceEqual(photos.Select(p => p.Name).OrderBy(n => n)), "gallery albums: All photos has every photo once");
        GalleryRules.Album(photos, GalleryAlbum.Selfies, favourites, into);
        Check(into.Count == photos.Count(p => p.Selfie) && into.All(p => p.Selfie) && into.Count == 5, "gallery albums: Selfies has the selfies alone (" + into.Count + ")");
        GalleryRules.Album(photos, GalleryAlbum.Favourites, favourites, into);
        Check(into.Count == 3 && into.All(p => favourites.Contains(p.Name)), "gallery albums: Favourites has the photos marked, and a mark of a photo not there shows nothing");
        GalleryRules.Album(photos, GalleryAlbum.Favourites, null, into);
        Check(into.Count == 0, "gallery albums: with no favourites read, Favourites is empty");
        GalleryRules.Album(new List<PhotoEntry>(), GalleryAlbum.All, favourites, into);
        Check(into.Count == 0, "gallery albums: an empty save has an empty album");
        GalleryRules.Album(photos, GalleryAlbum.All, favourites, into);
        Check(GalleryRules.IndexOf(into, photos[7].Name) >= 0 && GalleryRules.IndexOf(into, "nothing.jpg") == -1 && GalleryRules.IndexOf(into, null) == -1,
            "gallery albums: a photo is found in its album by name, and a name not there is not");
    }

    // ---------------------------------------------------------------- the days

    private static void GalleryDays()
    {
        // Days out of order on purpose: an older save loaded later takes photos of an earlier game day at a later real time.
        List<PhotoEntry> photos = GalleryPhotos(new[] { 12, 30, 12, 0, 29, 30 }, 3);
        var shuffled = photos.OrderBy(p => p.Name.GetHashCode() ^ 0x5bd1e995).ToList();
        var into = new List<PhotoEntry>();
        GalleryRules.Album(shuffled, GalleryAlbum.All, null, into);
        int[] order = into.Select(p => p.Day).ToArray();
        bool daysDown = true, newestFirst = true;
        for (int i = 1; i < into.Count; i++)
        {
            int a = into[i - 1].Day > 0 ? into[i - 1].Day : -1, b = into[i].Day > 0 ? into[i].Day : -1;
            daysDown &= a >= b;
            newestFirst &= a != b || PhotoStoreRules.Newest(into[i - 1], into[i]) < 0;
        }
        Check(daysDown && order[0] == 30 && order[order.Length - 1] == 0, "gallery days: the latest game day first, a photo of no known day last (" + string.Join(",", order.Distinct()) + ")");
        Check(newestFirst, "gallery days: within a day the newest photo first");
        var again = new List<PhotoEntry>();
        GalleryRules.Album(photos, GalleryAlbum.All, null, again);
        Check(again.Select(p => p.Name).SequenceEqual(into.Select(p => p.Name)), "gallery days: one order whatever order the folder gave");

        var rows = new List<GalleryRow>();
        float total = GalleryRules.Layout(into, 4, 100f, rows);
        var heads = rows.Where(r => r.Kind == GalleryRowKind.Day).ToList();
        Check(heads.Select(r => r.Day).SequenceEqual(new[] { 30, 29, 12, 0 }) && heads.All(r => r.Count == into.Count(p => p.Day == r.Day)),
            "gallery days: one heading a day, each day once even when its photos were taken apart, with its count");
        bool stripsRight = true;
        foreach (GalleryRow head in heads)
        {
            var strips = rows.Where(r => r.Kind == GalleryRowKind.Strip && r.Day == head.Day).ToList();
            stripsRight &= strips.Count == (head.Count + 3) / 4 && strips.Sum(r => r.Count) == head.Count && strips.All(r => r.Count >= 1 && r.Count <= 4)
                           && strips[0].First == head.First && strips.All(r => into.Skip(r.First).Take(r.Count).All(p => (p.Day > 0 ? p.Day : 0) == head.Day));
        }
        Check(stripsRight, "gallery days: a day's photos in strips of four, the last holding what is left, never another day's");
        bool tops = rows[0].Top == 0f;
        for (int i = 1; i < rows.Count; i++)
        {
            float gap = rows[i - 1].Kind == GalleryRowKind.Strip ? GalleryRules.CellGap : 0f;
            tops &= Math.Abs(rows[i].Top - (rows[i - 1].Top + rows[i - 1].Height + gap)) < 0.01f;
        }
        int stripCount = rows.Count(r => r.Kind == GalleryRowKind.Strip);
        float expect = heads.Count * GalleryRules.DayHeight + stripCount * (100f + GalleryRules.CellGap) + GalleryRules.FootHeight;
        Check(tops && rows[rows.Count - 1].Kind == GalleryRowKind.Foot && Math.Abs(total - expect) < 0.01f,
            "gallery days: each row under the one before, a hairline between strips, the count at the foot, and the height of the whole (" + total + ")");
        Check(GalleryRules.Layout(new List<PhotoEntry>(), 4, 100f, rows) == 0f && rows.Count == 0, "gallery days: an empty album has no rows at all");
        Check(GalleryRules.WordOf(41, 41) == GalleryDayWord.Today && GalleryRules.WordOf(40, 41) == GalleryDayWord.Yesterday && GalleryRules.WordOf(39, 41) == GalleryDayWord.Day
              && GalleryRules.WordOf(0, 41) == GalleryDayWord.Unknown && GalleryRules.WordOf(1, 1) == GalleryDayWord.Today && GalleryRules.WordOf(45, 41) == GalleryDayWord.Day,
            "gallery days: Today, Yesterday, a day by its number, and a day not known");
        Check(GalleryRules.Columns(1399f) == 3 && GalleryRules.Columns(1400f) == 4 && GalleryRules.Columns(2000f) == 4 && Math.Abs(GalleryRules.Cell(2000f, 4) * 4 + GalleryRules.CellGap * 3 - 2000f) < 0.01f
              && GalleryRules.Cell(100f, 0) == 0f, "gallery days: three across in a narrow grid, four in a wide one, the squares and their hairlines filling its width");
    }

    // ---------------------------------------------------------------- the rows built

    private static void GalleryRowsInView()
    {
        List<PhotoEntry> photos = GalleryPhotos(Enumerable.Range(1, 30).Reverse().ToArray(), 10);   // 300: a full Gallery
        var into = new List<PhotoEntry>();
        GalleryRules.Album(photos, GalleryAlbum.All, null, into);
        var rows = new List<GalleryRow>();
        float cell = GalleryRules.Cell(2000f, 4);
        float total = GalleryRules.Layout(into, 4, cell, rows);
        const float view = 1700f;
        bool same = true, few = true, covered = true;
        int most = 0;
        for (float y = 0f; y <= total; y += 37f)
        {
            GalleryRules.InView(rows, y, view, out int first, out int last);
            int lf = rows.FindIndex(r => r.Top + r.Height >= y - GalleryRules.Over);
            int ll = rows.FindIndex(r => r.Top + r.Height >= y + view + GalleryRules.Over);
            same &= first == lf && last == (ll < 0 ? rows.Count - 1 : ll);
            int built = last - first + 1;
            most = Math.Max(most, built);
            few &= built <= (int)Math.Ceiling((view + GalleryRules.Over * 2f) / GalleryRules.DayHeight) + 2;
            for (int i = 0; i < rows.Count; i++)
            {
                bool seen = rows[i].Top < y + view && rows[i].Top + rows[i].Height > y;
                covered &= !seen || (i >= first && i <= last);
            }
        }
        Check(same, "gallery rows: the rows in view are found by the binary search as by a walk over every row");
        Check(covered, "gallery rows: every row on the screen is built, wherever the grid is scrolled");
        Check(few && most < rows.Count / 3, "gallery rows: a full Gallery of 300 builds " + most + " of its " + rows.Count + " rows at most, never all");
        Check(GalleryRules.InView(rows, -400f, view, out int f0, out _) && f0 == 0 && GalleryRules.InView(rows, total + 900f, view, out _, out int l1) && l1 == rows.Count - 1,
            "gallery rows: scrolled past either end (the grid's give) the first or the last rows are built");
        Check(!GalleryRules.InView(new List<GalleryRow>(), 0f, view, out int fe, out int le) && fe == -1 && le == -1, "gallery rows: an empty album builds nothing");
        int r = GalleryRules.RowOf(rows, 157);
        Check(r >= 0 && rows[r].Kind == GalleryRowKind.Strip && 157 >= rows[r].First && 157 < rows[r].First + rows[r].Count && GalleryRules.RowOf(rows, 300) == -1,
            "gallery rows: a photo's strip is found by its place");
        GalleryRow far = rows[r];
        float to = GalleryRules.ScrollFor(far, 0f, view, total);
        Check(far.Top + far.Height <= to + view + 0.01f && far.Top >= to - 0.01f && Math.Abs(to - (far.Top + far.Height + GalleryRules.CellGap - view)) < 0.01f
              && GalleryRules.ScrollFor(rows[1], 0f, view, total) == 0f
              && GalleryRules.ScrollFor(rows[1], total, view, total) <= rows[1].Top,
            "gallery rows: back from the viewer the grid scrolls just enough to show the photo last seen, and not at all when it is in view");
    }

    // Layout of a full Gallery and the rows in view looked up 20,000 times: quick, and the look-ups allocate nothing.
    private static void GalleryRowsTimed()
    {
        List<PhotoEntry> photos = GalleryPhotos(Enumerable.Range(1, 30).Reverse().ToArray(), 10);
        var into = new List<PhotoEntry>(320);
        GalleryRules.Album(photos, GalleryAlbum.All, null, into);
        var rows = new List<GalleryRow>(400);
        GalleryRules.Layout(into, 4, 495f, rows);
        LookUps(rows, 200);   // (warmed)
        var watch = new Stopwatch();
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        watch.Start();
        int sum = LookUps(rows, 20000);
        double ms = watch.Elapsed.TotalMilliseconds;
        bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
        Check(bytes == 0 && sum > 0, "gallery rows: looking up the rows in view allocates nothing (" + bytes + " bytes for 20,000)");
        Check(ms < 40.0, "gallery rows: 20,000 look-ups of the rows in view take " + ms.ToString("0.0") + " ms (wanted under 40)");
        LayOut(into, rows, 2);   // (warmed)
        long laid = GC.GetAllocatedBytesForCurrentThread();
        watch.Restart();
        float total = LayOut(into, rows, 200);
        double layMs = watch.Elapsed.TotalMilliseconds / 200.0;
        laid = GC.GetAllocatedBytesForCurrentThread() - laid;
        Check(laid == 0 && total > 0f && layMs < 0.5, "gallery rows: a full Gallery is laid out in " + layMs.ToString("0.000") + " ms (wanted under 0.5) with nothing allocated once its list has room (" + laid + " bytes)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int LookUps(List<GalleryRow> rows, int times)
    {
        int sum = 0;
        float total = rows[rows.Count - 1].Top + rows[rows.Count - 1].Height;
        for (int i = 0; i < times; i++)
        {
            GalleryRules.InView(rows, (i * 97) % total, 1700f, out int first, out int last);
            sum += last - first + 1;
        }
        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static float LayOut(List<PhotoEntry> shown, List<GalleryRow> rows, int times)
    {
        float total = 0f;
        for (int i = 0; i < times; i++)
        {
            total = GalleryRules.Layout(shown, 4, 495f, rows);
        }
        return total;
    }

    // ---------------------------------------------------------------- the photo before and after

    private static void GalleryStepping()
    {
        Check(GalleryRules.Step(0, -1, 10) == 0 && GalleryRules.Step(9, 1, 10) == 9 && GalleryRules.Step(4, 1, 10) == 5 && GalleryRules.Step(4, -1, 10) == 3
              && GalleryRules.Step(0, 1, 1) == 0 && GalleryRules.Step(3, 1, 0) == -1 && GalleryRules.Step(14, -1, 10) == 8,
            "gallery stepping: the photo before and after, staying put at either end; none in an empty album");
        var nav = new GalleryNav();
        nav.Tap(0, 3);
        bool atStart = !nav.Step(-1, 3) && nav.Index == 0;
        bool on = nav.Step(1, 3) && nav.Step(1, 3) && nav.Index == 2;
        bool atEnd = !nav.Step(1, 3) && nav.Index == 2;
        Check(atStart && on && atEnd, "gallery stepping: the viewer goes on to the last photo and stops there, and stops at the first going back");
        var grid = new GalleryNav();
        Check(!grid.Step(1, 5) && grid.Index == -1, "gallery stepping: on the grid the arrow keys move nothing");
        Check(GalleryRules.AfterRemoval(3, 9) == 3 && GalleryRules.AfterRemoval(9, 9) == 8 && GalleryRules.AfterRemoval(0, 0) == -1,
            "gallery stepping: after a deletion the next photo takes its place, or the last when it was the last, or none");
        nav.Reread(-1, 2);
        Check(nav.Viewing && nav.Index == 1, "gallery stepping: the photo shown deleted, the viewer shows the one now in its place");
        nav.Reread(-1, 0);
        Check(!nav.Viewing, "gallery stepping: the last photo of the album deleted, back to the grid");
        var found = new GalleryNav();
        found.Tap(4, 10);
        found.Reread(2, 9);
        Check(found.Viewing && found.Index == 2, "gallery stepping: the album read again keeps showing the same photo, by its name");
        int f = GalleryRules.Film(0, 20, out int l);
        int g = GalleryRules.Film(10, 20, out int m);
        int h = GalleryRules.Film(19, 20, out int n);
        int k = GalleryRules.Film(1, 3, out int o);
        Check(f == 0 && l == 8 && g == 6 && m == 14 && h == 11 && n == 19 && k == 0 && o == 2 && GalleryRules.Film(0, 0, out int z) == 0 && z == -1,
            "gallery stepping: the strip under the photo shows nine neighbours, more on one side at an end, all of a short album");
    }

    // ---------------------------------------------------------------- favourites

    private static void GalleryFavourites(string folder)
    {
        Directory.CreateDirectory(folder);
        List<PhotoEntry> photos = GalleryPhotos(new[] { 3, 4 }, 4);
        var set = new HashSet<string>(StringComparer.Ordinal);
        Check(GalleryRules.Toggle(set, photos[1].Name) && GalleryRules.Toggle(set, photos[5].Name) && !GalleryRules.Toggle(set, photos[1].Name) && GalleryRules.Toggle(set, photos[2].Name)
              && set.Count == 2 && set.Contains(photos[2].Name) && !set.Contains(photos[1].Name), "gallery favourites: a heart marks a photo, a second unmarks it");
        Check(PhotoStore.SetFavourites(folder, set), "gallery favourites: written beside the photos");
        string file = System.IO.Path.Combine(folder, GalleryRules.FavouritesFile);
        var read = new HashSet<string>();
        PhotoStore.Favourites(folder, read);
        Check(File.Exists(file) && read.SetEquals(set), "gallery favourites: read back as they were written (" + read.Count + ")");
        string first = File.ReadAllText(file);
        PhotoStore.SetFavourites(folder, new HashSet<string>(set.Reverse()));
        Check(File.ReadAllText(file) == first && first == GalleryRules.WriteFavourites(set), "gallery favourites: the same favourites always write the same file");
        File.WriteAllText(file, photos[2].Name + "\r\n\r\nnotes.txt\n" + photos[2].Name + "\n  " + photos[6].Name + "  \nSelfie_broken.jpg\n../../evil.jpg\n");
        PhotoStore.Favourites(folder, read);
        Check(read.Count == 2 && read.Contains(photos[2].Name) && read.Contains(photos[6].Name), "gallery favourites: a line that is no photo's name, a blank or a repeat is left out");
        PhotoStore.Favourites(System.IO.Path.Combine(folder, "none"), read);
        Check(read.Count == 0, "gallery favourites: a save with no file has none");
        set.Add(photos[6].Name);
        set.Add("Photo_20990101_000000_D9_00.jpg");
        int gone = GalleryRules.Prune(set, photos);
        Check(gone == 1 && set.Count == 3 && !set.Contains("Photo_20990101_000000_D9_00.jpg"), "gallery favourites: a photo deleted takes its mark with it");
        Check(GalleryRules.Prune(set, photos) == 0 && GalleryRules.Prune(new HashSet<string>(), photos) == 0, "gallery favourites: nothing to take out, nothing taken");
        Check(PhotoStore.SetFavourites(folder, new HashSet<string>()) && !File.Exists(file), "gallery favourites: with none left the file goes");
        Check(PhotoStore.SetFavourites(System.IO.Path.Combine(folder, "new"), set) && File.Exists(System.IO.Path.Combine(folder, "new", GalleryRules.FavouritesFile)),
            "gallery favourites: written into a folder made for them if need be");
        var listed = new List<PhotoEntry>();
        File.WriteAllBytes(System.IO.Path.Combine(folder, photos[0].Name), new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });
        PhotoStore.SetFavourites(folder, set);
        Check(PhotoStore.List(folder, listed) && listed.Count == 1, "gallery favourites: the file is never listed as a photo");
    }

    // ---------------------------------------------------------------- the look and the sources

    private static void GalleryLookAndSources()
    {
        Check(GalleryRules.Dark(19) && GalleryRules.Dark(23) && GalleryRules.Dark(0) && GalleryRules.Dark(6) && !GalleryRules.Dark(7) && !GalleryRules.Dark(12) && !GalleryRules.Dark(18),
            "gallery look: dark from 19:00 to 07:00 of the game's clock, light by day");
        string gallery = GalleryText(), parts = Read(@"Photos\GalleryParts.cs"), director = Read(@"Photos\PhotosDirector.cs");
        Check(!Regex.IsMatch(gallery + parts, @"\bInfoBox\b|\.Box\.|ButtonRow\(") && gallery.Contains("FlareKit.Text(") && Regex.IsMatch(parts, @"class GalleryLook\b[\s\S]*Light = [\s\S]*Night = "),
            "gallery look: the Gallery draws its own look (no part of the game's info box), with the game's font and its own light and dark colours");
        string keys = Between(parts, "internal sealed class GalleryKeys", "internal sealed class GalleryWheel");
        Check(parts.Contains("[DefaultExecutionOrder(-31000)]") && keys.Contains("HotSpots.Time(HotUpdate)") && keys.Contains("enabled = false;")
              && Regex.IsMatch(gallery, @"_keys\.enabled = on;") && Between(gallery, "internal bool ReadKeys()", "private void Step(int delta)").Contains("PhotosGame.TakeCancel()"),
            "gallery look: the viewer's keys are read only while a photo is shown whole, the cancel key taken before the game's own handling");
        string read = Between(gallery, "private IEnumerator ReadSmall(int opening)", "// A small copy made:");
        Check(read.Contains("Task.Run(") && read.Contains("watch.Elapsed.TotalMilliseconds >= GalleryRules.UploadBudgetMs") && read.Contains("yield return null;"),
            "gallery look: small copies are read on a worker and made textures within a budget of a frame");
        Check(Regex.IsMatch(director, @"public bool ToggleFavourite\(string name\)") && Regex.IsMatch(Between(director, "private void WriteFavourites()", "// ----"), @"Task\.Run\(\(\) => writes\.Run\(ticket, \(\) => PhotoStore\.SetFavourites\(folder, copy\)\)\)")
              && Regex.Matches(director, @"GalleryRules\.PruneListed\(_favourites, _photos, _listedWhole\)").Count == 2,
            "gallery look: favourites are written on a worker, the last change winning, and a deleted photo's mark goes on deletion and on reading the folder");
        string forget = Between(gallery, "private void ForgetThumbs()", "// The parts made for a panel");
        Check(forget.Contains("PhotosGame.Destroy(kept.Value)") && Between(gallery, "private void Trim()", "// Every small copy let go").Contains("PhotosGame.Destroy(_thumbs[_drop[i]])")
              && Between(gallery, "private void Forget()", "\n        }").Contains("DropWhole()"),
            "gallery look: every small copy and the whole photo are destroyed when let go or when the app closes");
    }

    // ---------------------------------------------------------------- the review's four

    private static void GalleryReviewFixes()
    {
        // 1. A listing that failed (the disk, the rights) shows no photo: every favourite would be taken out and the file
        // deleted. Marks are taken out only after a whole listing.
        List<PhotoEntry> photos = GalleryPhotos(new[] { 5 }, 4);
        var set = new HashSet<string>(StringComparer.Ordinal) { photos[0].Name, photos[2].Name };
        int kept = GalleryRules.PruneListed(set, new List<PhotoEntry>(), false);
        Check(kept == 0 && set.Count == 2, "gallery review: a failed list keeps the favourites");
        set.Add("Photo_20990101_000000_D9_00.jpg");
        Check(GalleryRules.PruneListed(set, photos, true) == 1 && set.Count == 2, "gallery review: a whole list still takes out the mark of a photo gone");
        string director = Read(@"Photos\PhotosDirector.cs");
        Check(director.Contains("_listedWhole = work.Status == TaskStatus.RanToCompletion && work.Result;") && !Regex.IsMatch(director, @"GalleryRules\.Prune\("),
            "gallery review: the director takes marks out only when the listing ran to completion and read the folder");

        // 2. A whole photo no longer wanted (another photo shown, the app closed) is read but never decoded.
        string game = Read(@"Photos\PhotosGame.cs"), gallery = GalleryText();
        string load = Between(game, "public static IEnumerator Load(", "// THE ONE DECODE");
        int asked = load.IndexOf("if (wanted != null && !wanted())", StringComparison.Ordinal), decoded = load.IndexOf("texture = Decode(bytes);", StringComparison.Ordinal);
        Check(load.Contains("Func<bool> wanted = null") && asked > 0 && decoded > asked && load.IndexOf("work.IsCompleted", StringComparison.Ordinal) < asked
              && gallery.Contains("() => load == _wholeLoad && Showing)"),
            "gallery review: a stale load of a whole photo is let go after its read and before the decode");

        // 3. The viewfinder's ask for the newest photo lasts only while the app opens.
        Check(GalleryRules.LatestWanted(10f, 10f) && GalleryRules.LatestWanted(10f, 11.9f) && !GalleryRules.LatestWanted(10f, 12.1f) && !GalleryRules.LatestWanted(-1f, 5f)
              && !GalleryRules.LatestWanted(10f, 9f), "gallery review: the ask for the newest photo is kept two seconds, then forgotten");
        Check(Between(gallery, "private void Closed()", "// ----").Contains("_latestAskedAt = -1f;") && gallery.Contains("GalleryRules.LatestWanted(_latestAskedAt, Time.unscaledTime)")
              && !gallery.Contains("_wantLatest"), "gallery review: closing the app forgets the ask, and it is honoured only while fresh");

        // 4. A day heading scrolled into view sets words made with the rows; nothing is made while scrolling.
        string bind = Between(gallery, "private void BindDay(DayCell cell, int r)", "// Each day heading's words");
        Check(bind.Contains("_headings[r]") && bind.Contains("_headCounts[r]") && !bind.Contains("PhotosText.") && Between(gallery, "private void WriteGrid(bool top)", "private void Scrolled(").Contains("WriteHeadings();"),
            "gallery review: a day heading's words are made when the rows are worked out, never while scrolling");
    }

    // ---------------------------------------------------------------- the words

    private static readonly string[] GalleryNewKeys =
    {
        "reallife_gallery_album_favourites", "reallife_gallery_count_none", "reallife_gallery_count_one", "reallife_gallery_count_many", "reallife_gallery_day_today",
        "reallife_gallery_day_yesterday", "reallife_gallery_day", "reallife_gallery_day_unknown", "reallife_gallery_when_today", "reallife_gallery_when_yesterday",
        "reallife_gallery_when_day", "reallife_gallery_viewer_sub", "reallife_gallery_viewer_keys", "reallife_gallery_storage", "reallife_gallery_empty_title_all",
        "reallife_gallery_empty_body_all", "reallife_gallery_empty_title_selfies", "reallife_gallery_empty_body_selfies", "reallife_gallery_empty_title_favourites",
        "reallife_gallery_empty_body_favourites", "reallife_gallery_act_share", "reallife_gallery_act_wallpaper", "reallife_gallery_act_favourite"
    };

    private static void GalleryWords()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(PhotoLocales, "en.json"));
        Dictionary<string, string> pt = LoadWords(System.IO.Path.Combine(PhotoLocales, "pt.json"));
        Dictionary<string, string> zh = LoadWords(System.IO.Path.Combine(PhotoLocales, "zh-cn.json"));
        if (en == null || pt == null || zh == null)
        {
            Check(false, "gallery words: the three locales are where the checks look for them");
            return;
        }
        var holes = new Regex(@"\{[a-z_]+\}");
        var bad = new List<string>();
        foreach (string key in GalleryNewKeys)
        {
            if (!en.TryGetValue(key, out string e) || !pt.TryGetValue(key, out string p) || !zh.TryGetValue(key, out string z) || e.Length == 0 || p.Length == 0 || z.Length == 0)
            {
                bad.Add(key);
            }
            else if (Holes(holes, p) != Holes(holes, e) || Holes(holes, z) != Holes(holes, e) || !Regex.IsMatch(z, @"\p{IsCJKUnifiedIdeographs}"))
            {
                bad.Add(key + " (its {names} differ, or the Chinese is not Chinese)");
            }
        }
        Check(bad.Count == 0, "gallery words: each of the Gallery's " + GalleryNewKeys.Length + " new texts is in en, pt and zh-cn with the same {names}" + (bad.Count > 0 ? " (" + string.Join(", ", bad) + ")" : string.Empty));
        string gallery = GalleryText() + Read(@"Photos\PhotosText.cs");
        string unused = GalleryNewKeys.FirstOrDefault(k => !gallery.Contains("\"" + k + "\""));
        Check(unused == null, "gallery words: every new text is read by the Gallery" + (unused != null ? " (" + unused + ")" : string.Empty));
        string[] gone = { "reallife_gallery_pick", "reallife_gallery_page", "reallife_gallery_button_view", "reallife_gallery_note_photo" };
        Check(gone.All(k => !en.ContainsKey(k) && !pt.ContainsKey(k) && !zh.ContainsKey(k)), "gallery words: the old page's texts no one reads are gone (choose, then click again; the pages)");
        Check(en["reallife_gallery_day"] == "Day {day}" && en["reallife_gallery_day_today"] == "Today" && en["reallife_gallery_day_yesterday"] == "Yesterday" && en["reallife_gallery_storage"].Contains("{count} of {cap}"),
            "gallery words: the days read Today, Yesterday, Day 41, and the foot gives the photos kept against the most");
        string[] guides = { en["reallife_guide_photos"], pt["reallife_guide_photos"], zh["reallife_guide_photos"] };
        Check(guides.All(g => !g.Contains("{photos_page}") && g.Contains("{photos_cap}") && g.Contains("favourites.txt")) && guides[0].Contains("Favourites") && guides[1].Contains("Favoritas") && guides[2].Contains("收藏")
              && guides[0].Contains("Click a photo to see it whole") && !guides[0].Contains("Click a photo to choose it"),
            "gallery words: the Guide's page says one click shows a photo, the albums and where the favourites are kept, in each language");
        Check(WhatsNewHas(en, "The Gallery has a look of its own") && WhatsNewHas(pt, "A Galeria tem um visual próprio") && WhatsNewHas(zh, "图库有了自己的外观")
              && WhatsNewHas(en, "Fixed: the Gallery never showed its photos") && WhatsNewHas(pt, "Corrigido: a Galeria nunca mostrava as fotos") && WhatsNewHas(zh, "已修复：图库从不显示照片"),
            "gallery words: What's new has the Gallery's line and its fix in each of the three languages (any of its rows)");
        var dash = new Regex(@"[–—]| - |!");
        string dashed = GalleryNewKeys.FirstOrDefault(k => dash.IsMatch(en[k]) || dash.IsMatch(pt[k]));
        Check(dashed == null, "gallery words: no dash and no exclamation mark" + (dashed != null ? " (" + dashed + ")" : string.Empty));
    }
}
