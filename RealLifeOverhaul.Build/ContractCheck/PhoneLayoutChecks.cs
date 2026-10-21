using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul.GameUi;

// The phone's home screen as a real phone's (Core/GameUi/PhoneLayoutRules.cs, PhoneLayout.cs, PhoneFolders.cs and
// PhoneGridPager.cs's folders): the mod's apps in five folders, a dock of two on the HUD phone, the player's moves kept
// as one line, and the three bank apps one Banks app. The rules: every app of the mod's has a folder or the dock; every
// button is in exactly one place (the home screen, the dock or one folder's page) for every layout, order and mix of the
// game's, the mod's and other mods' buttons, and a folder's tile is on the home screen exactly while it has an app; the
// game's own apps first and in order, other mods' last in theirs; the tiles of a page with the dock and on a folder's
// page, the More apps button never on an item's tile, the empty tiles that keep the dock on the last row; the line read
// and written back, with what this version cannot read kept; the option off; the folders' numbers; the cost. And the
// sources: the pager lays out its folders on its events only, the option off is the plain pages, the cancel key is taken
// only while a folder is open, the Banks app is one app, and the texts are in the three languages with What's new.
internal static partial class Program
{
    private static readonly string[] Folders = { "money", "work", "life", "business", "setup" };

    private static void PhoneLayoutChecks(string scripts, string locales)
    {
        Section("the phone's folders and dock: where each app stands (Core/GameUi/PhoneLayoutRules)");
        PhoneDefaults(scripts);
        PhoneViews();
        PhoneTiles();
        PhoneMoves();
        PhoneLines();
        PhoneBadges();
        PhoneTiming();
        Section("the phone's folders and dock: the pager, the tiles and the Banks app follow the rules, in " + scripts);
        PhoneSources(scripts);
        PhoneTexts(scripts, locales);
    }

    // Every app of the mod's has its usual place, a folder or the dock, as the brief groups them.
    private static void PhoneDefaults(string scripts)
    {
        string[] order = PagerRules.ModOrder;
        Check(PhoneLayoutRules.Defaults.Length == order.Length, "a usual place for each of the " + order.Length + " apps in the mod's order (" + PhoneLayoutRules.Defaults.Length + ")");
        Check(PhoneLayoutRules.FolderIds.Length == PhoneLayoutRules.FolderCount && PhoneLayoutRules.FolderKeys.Length == PhoneLayoutRules.FolderCount
              && PhoneLayoutRules.FolderIds.SequenceEqual(Folders) && PhoneLayoutRules.FolderKeys.Distinct().Count() == PhoneLayoutRules.FolderCount,
            "five folders, each with its own name in the layout (money, work, life, business, setup) and its own text");
        bool placed = true;
        for (int i = 0; i < order.Length && i < PhoneLayoutRules.Defaults.Length; i++)
        {
            int p = PhoneLayoutRules.Defaults[i];
            placed &= p == PhoneLayoutRules.Dock || p >= 0 && p < PhoneLayoutRules.FolderCount;
            placed &= PhoneLayoutRules.DefaultOf(order[i]) == p;
        }
        Check(placed, "every app of the mod's starts in a folder or the dock, none loose on the home screen");
        int docked = PhoneLayoutRules.Defaults.Count(p => p == PhoneLayoutRules.Dock);
        Check(docked == PhoneLayoutRules.DockMost && PhoneLayoutRules.DefaultOf("RealLifePlanner") == PhoneLayoutRules.Dock && PhoneLayoutRules.DefaultOf("RealLifeFlare") == PhoneLayoutRules.Dock,
            "the dock starts full with two: the Planner and Flare (" + docked + ")");
        var want = new Dictionary<string, int>
        {
            { "RealLifeFinance", 0 }, { "RealLifeBanks", 0 }, { "RealLifeOwnBank", 0 }, { "RealLifeStocks", 0 }, { "RealLifeNewsstand", 0 }, { "RealLifeLedger", 0 }, { "RealLifeOverhaulUtilities", 0 },
            { "RealLifeCareer", 1 }, { "RealLifeCampus", 1 }, { "RealLifeOpenRoles", 1 },
            { "RealLifeCamera", 2 }, { "RealLifeGallery", 2 },
            { "RealLifeCompany", 3 }, { "RealLifeBrands", 3 }, { "RealLifeServices", 3 }, { "RealLifeVentures", 3 }, { "RealLifeOnline", 3 }, { "RealLifeEstate", 3 },
            { "RealLifeDealer", 3 }, { "RealLifeTechSector", 3 },
            { "RealLifeGuide", 4 }, { "RealLifeSettings", 4 },
        };
        var wrong = want.Where(w => PhoneLayoutRules.DefaultOf(w.Key) != w.Value).Select(w => w.Key).ToList();
        Check(wrong.Count == 0, "money (the banks, credit, the stocks, the paper, the bills), work and study, social and life, business (BizMan's add-ons, tech, the showroom), "
                                + "the Guide and the options each start in their folder (wrong: " + string.Join(", ", wrong) + ")");
        for (int f = 0; f < PhoneLayoutRules.FolderCount; f++)
        {
            Check(PhoneLayoutRules.Defaults.Contains(f), "the folder " + Folders[f] + " starts with an app in it");
        }
        Check(PhoneLayoutRules.DefaultOf("Persona") == PhoneLayoutRules.Home && PhoneLayoutRules.DefaultOf("GoingPublicHub") == PhoneLayoutRules.Home && PhoneLayoutRules.DefaultOf(null) == PhoneLayoutRules.Home,
            "a name that is not one of the mod's apps has no folder");
        // Every app the mod puts on the phone, by its id in the code, has a folder or the dock.
        var ids = new List<string>();
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(file), "const string \\w*AppId = \"(RealLife\\w+)\""))
            {
                ids.Add(m.Groups[1].Value);
            }
        }
        var loose = ids.Where(id => PhoneLayoutRules.DefaultOf(id) == PhoneLayoutRules.Home).ToList();
        Check(ids.Count >= 20 && loose.Count == 0, "every app of the mod's in the code (" + ids.Count + ") belongs to a folder or the dock (none: " + string.Join(", ", loose) + ")");
        Check(ids.Contains("RealLifeBanks") && !ids.Any(id => Regex.IsMatch(id, "^RealLifeBank[0-9]")), "the banks are one Banks app (RealLifeBanks), no app for each bank");
    }

    // Every button in exactly one place, for many layouts, orders and mixes.
    private static void PhoneViews()
    {
        var random = new Random(21102026);
        string[] mods = PagerRules.ModOrder;
        int cases = 0, faults = 0;
        string first = null;
        for (int round = 0; round < 6000; round++)
        {
            int games = random.Next(0, 9), others = random.Next(0, 4);
            var mine = mods.Where(_ => random.Next(4) > 0).OrderBy(_ => random.Next()).ToList();   // some apps off, put on in any order
            var names = new List<string>();
            for (int g = 0; g < games; g++)
            {
                names.Add("Game" + g);
            }
            names.AddRange(mine);
            for (int o = 0; o < others; o++)
            {
                names.Insert(random.Next(games, names.Count + 1), "OtherMod" + o);   // other mods' buttons anywhere after the game's
            }
            int count = names.Count;
            var isGame = names.Select(n => n.StartsWith("Game", StringComparison.Ordinal)).ToArray();
            var rank = names.Select((n, i) => isGame[i] ? -1 : PagerRules.ModRank(n)).ToArray();
            var orderAt = new int[count];
            PagerRules.Arrange(isGame, rank, count, orderAt);
            string[] arranged = orderAt.Select(i => names[i]).ToArray();
            var layout = new PhoneLayout();
            int moves = random.Next(0, 8);
            for (int m = 0; m < moves && mods.Length > 0; m++)
            {
                layout.Move(mods[random.Next(mods.Length)], random.Next(-2, PhoneLayoutRules.FolderCount));
            }
            var kind = new int[count];
            var place = new int[count];
            for (int i = 0; i < count; i++)
            {
                string n = arranged[i];
                kind[i] = n.StartsWith("Game", StringComparison.Ordinal) ? PhoneLayoutRules.GameItem : n.StartsWith("OtherMod", StringComparison.Ordinal) ? PhoneLayoutRules.OtherItem : PhoneLayoutRules.ModItem;
                place[i] = kind[i] == PhoneLayoutRules.ModItem ? layout.Place(n) : PhoneLayoutRules.Home;
            }
            foreach (bool dockRow in new[] { true, false })
            {
                cases++;
                string fault = ViewFault(kind, place, count, dockRow, arranged);
                if (fault != null)
                {
                    faults++;
                    first ??= fault + " (" + string.Join(",", arranged) + "; " + layout.Write() + ")";
                }
            }
        }
        Check(faults == 0, cases + " grids of up to 8 of the game's, any of the mod's put on in any order, up to 3 of other mods', after up to 7 moves, on both phones: every button in exactly one place, "
                           + "the game's first in order, other mods' last in theirs, a folder's tile exactly while it has an app, the dock at most two: " + faults + " wrong"
                           + (first != null ? " (first: " + first + ")" : string.Empty));
        // An empty grid, arrays too short, a folder with nothing in it.
        var view = new int[4];
        var dock = new int[2];
        var members = new int[PhoneLayoutRules.FolderCount];
        Check(PhoneLayoutRules.View(null, null, 0, -1, true, view, dock, out int none, members) == 0 && none == 0 && members.All(m => m == 0)
              && PhoneLayoutRules.View(new[] { 0, 1 }, new[] { -1, 0 }, 2, 3, true, view, dock, out _, members) == 0 && members[0] == 1,
            "no buttons are an empty view, and a folder with nothing in it an empty page");
        Check(PhoneLayoutRules.View(new[] { 1, 1, 1 }, new[] { PhoneLayoutRules.Dock, PhoneLayoutRules.Dock, PhoneLayoutRules.Dock }, 3, -1, true, view, dock, out int three, members) == 1
              && three == 2 && view[0] == 2 && dock[0] == 0 && dock[1] == 1,
            "a third app in the dock (put there while one was off) stands on the home screen, the first two in the dock");
        Check(PhoneLayoutRules.View(new[] { 1, 1 }, new[] { 7, -5 }, 2, -1, true, view, dock, out _, members) == 2 && view[0] == 0 && view[1] == 1,
            "an app the layout puts in a place this version does not have stands on the home screen");
    }

    private static string ViewFault(int[] kind, int[] place, int count, bool dockRow, string[] names)
    {
        var view = new int[count + PhoneLayoutRules.FolderCount];
        var dock = new int[PhoneLayoutRules.DockMost];
        var members = new int[PhoneLayoutRules.FolderCount];
        var seen = new int[count];
        int n = PhoneLayoutRules.View(kind, place, count, -1, dockRow, view, dock, out int docked, members);
        if (docked > PhoneLayoutRules.DockMost || !dockRow && docked != 0)
        {
            return "the dock row holds " + docked;
        }
        for (int d = 0; d < docked; d++)
        {
            seen[dock[d]]++;
        }
        int tiles = 0, lastGame = -1, firstOther = int.MaxValue, at = 0;
        var tileOf = new bool[PhoneLayoutRules.FolderCount];
        for (int j = 0; j < n; j++)
        {
            int e = view[j];
            if (e < 0)
            {
                int f = -1 - e;
                if (f >= PhoneLayoutRules.FolderCount || tileOf[f] || members[f] == 0)
                {
                    return "folder tile " + f + " wrong";
                }
                tileOf[f] = true;
                tiles++;
                continue;
            }
            seen[e]++;
            if (kind[e] == PhoneLayoutRules.GameItem)
            {
                if (j != at++ || e <= lastGame)
                {
                    return "the game's " + names[e] + " not first in order";
                }
                lastGame = e;
            }
            else if (kind[e] == PhoneLayoutRules.OtherItem)
            {
                firstOther = Math.Min(firstOther, j);
            }
            else if (j > firstOther)
            {
                return names[e] + " after another mod's";
            }
        }
        for (int f = 0; f < PhoneLayoutRules.FolderCount; f++)
        {
            if (tileOf[f] != members[f] > 0)
            {
                return "folder " + Folders[f] + " has " + members[f] + " apps and " + (tileOf[f] ? "a" : "no") + " tile";
            }
            int m = PhoneLayoutRules.View(kind, place, count, f, dockRow, view, dock, out int inFolder, new int[PhoneLayoutRules.FolderCount]);
            if (m != members[f] || inFolder != 0)
            {
                return "folder " + Folders[f] + "'s page holds " + m + " of " + members[f];
            }
            for (int j = 0; j < m; j++)
            {
                if (PhoneLayoutRules.FolderOf(kind[view[j]], place[view[j]]) != f || j > 0 && view[j] <= view[j - 1])
                {
                    return "folder " + Folders[f] + "'s page has " + names[view[j]] + " or is out of order";
                }
                seen[view[j]]++;
            }
        }
        for (int i = 0; i < count; i++)
        {
            if (seen[i] != 1)
            {
                return names[i] + " is in " + seen[i] + " places";
            }
        }
        if (!dockRow)
        {
            // On the row the docked apps stand right after the game's own.
            int games = kind.Take(count).Count(k => k == PhoneLayoutRules.GameItem);
            PhoneLayoutRules.View(kind, place, count, -1, false, view, dock, out _, members);
            int dockedOnRow = 0;
            for (int i = 0; i < count && dockedOnRow < PhoneLayoutRules.DockMost; i++)
            {
                if (kind[i] == PhoneLayoutRules.ModItem && place[i] == PhoneLayoutRules.Dock)
                {
                    if (view[games + dockedOnRow] != i)
                    {
                        return names[i] + " (docked) not right after the game's apps on the row";
                    }
                    dockedOnRow++;
                }
            }
        }
        return null;
    }

    // The tiles of a page: with the dock on the HUD phone's home screen, on a folder's page, on the row.
    private static void PhoneTiles()
    {
        Check(PhoneLayoutRules.ItemTiles(false, 8, false, false) == 8 && PhoneLayoutRules.MoreTile(false, 8, false, false) == 7,
            "the HUD phone's home screen with an empty dock: eight tiles, the More apps button on the eighth");
        Check(PhoneLayoutRules.ItemTiles(false, 8, false, true) == 6 && PhoneLayoutRules.MoreTile(false, 8, false, true) == 5,
            "with the dock: six tiles above it, the More apps button on the sixth, the dock the last row");
        Check(PhoneLayoutRules.ItemTiles(false, 8, true, true) == 7 && PhoneLayoutRules.MoreTile(false, 8, true, true) == 7,
            "a folder's page on the HUD phone: the Back tile, seven tiles after it, the More apps button on the last; no dock row");
        Check(PhoneLayoutRules.ItemTiles(true, 6, false, true) == 6 && PhoneLayoutRules.ItemTiles(true, 6, true, false) == 5 && PhoneLayoutRules.MoreTile(true, 6, true, false) == 5,
            "the full menu's row of six: every tile on the home screen (no dock row there), the Back tile and five on a folder's page");
        Check(!PhoneLayoutRules.FoldersFit(true, 2) && PhoneLayoutRules.FoldersFit(true, 3) && PhoneLayoutRules.FoldersFit(false, 2),
            "a row of two (a 4:3 screen) has no room for a folder's page and keeps the plain pages; three have; the HUD phone always");
        int cases = 0, faults = 0;
        string first = null;
        foreach (bool strip in new[] { false, true })
        {
            foreach (int tiles in strip ? new[] { 3, 4, 5, 6, 7 } : new[] { PagerRules.PhoneTiles })
            {
                foreach (bool folder in new[] { false, true })
                {
                    foreach (bool dock in new[] { false, true })
                    {
                        int held = PhoneLayoutRules.ItemTiles(strip, tiles, folder, dock);
                        int more = PhoneLayoutRules.MoreTile(strip, tiles, folder, dock);
                        bool row = !strip && !folder && dock;
                        for (int count = 0; count <= 60; count++)
                        {
                            cases++;
                            int pages = PagerRules.Pages(count, held);
                            string fault = null;
                            if (more >= tiles || (folder ? 1 : 0) + held > tiles || row && held + PhoneLayoutRules.DockMost != tiles)
                            {
                                fault = "the page's tiles do not fit";
                            }
                            for (int p = 0; p < pages && fault == null; p++)
                            {
                                int size = PagerRules.PageSize(p, count, held);
                                int gaps = PhoneLayoutRules.Gaps(size, held, row);
                                if (row && size + gaps != held)
                                {
                                    fault = "page " + p + ": " + size + " items and " + gaps + " empty tiles before the dock";
                                }
                                if (!row && gaps != 0)
                                {
                                    fault = "empty tiles with no dock row";
                                }
                                for (int i = 0; i < size && fault == null; i++)
                                {
                                    int tile = (folder ? 1 : 0) + PagerRules.TileOf(p * PagerRules.PerPage(count, held) + i, count, held);
                                    if (pages > 1 && tile == more || tile >= tiles || folder && tile == 0)
                                    {
                                        fault = "an item on tile " + tile + " (the More apps button's " + more + ")";
                                    }
                                }
                            }
                            if (fault != null)
                            {
                                faults++;
                                first ??= (strip ? "row of " + tiles : "HUD") + (folder ? ", a folder" : ", home") + (dock ? ", dock" : string.Empty) + ", " + count + " items: " + fault;
                            }
                        }
                    }
                }
            }
        }
        Check(faults == 0, cases + " pages of 0 to 60 items on both phones, home and folder, with and without the dock: the Back tile first, no item on the More apps button's tile, "
                           + "the dock on the last row: " + faults + " wrong" + (first != null ? " (first: " + first + ")" : string.Empty));
    }

    // Who may go where.
    private static void PhoneMoves()
    {
        Check(PhoneLayoutRules.CanMove(PhoneLayoutRules.Money, PhoneLayoutRules.Home, 2) && PhoneLayoutRules.CanMove(PhoneLayoutRules.Home, PhoneLayoutRules.Setup, 0)
              && PhoneLayoutRules.CanMove(PhoneLayoutRules.Work, PhoneLayoutRules.Dock, 1),
            "an app goes to another folder, onto the home screen, or into a dock with room");
        Check(!PhoneLayoutRules.CanMove(PhoneLayoutRules.Work, PhoneLayoutRules.Dock, 2) && !PhoneLayoutRules.CanMove(PhoneLayoutRules.Life, PhoneLayoutRules.Life, 0)
              && !PhoneLayoutRules.CanMove(PhoneLayoutRules.Home, 9, 0) && !PhoneLayoutRules.CanMove(PhoneLayoutRules.Dock, PhoneLayoutRules.Dock, 2),
            "not into a full dock, not where it is already, not to a place this version does not have");
    }

    // The layout's one line: written, read back, and what this version cannot read kept.
    private static void PhoneLines()
    {
        Check(PhoneLayout.Read(null).Moves == 0 && PhoneLayout.Read(string.Empty).Write() == PhoneLayout.Version, "no line is the usual places, written as the version alone");
        var layout = new PhoneLayout();
        bool moved = layout.Move("RealLifeStocks", PhoneLayoutRules.Home) && layout.Move("RealLifePlanner", PhoneLayoutRules.Life) && layout.Move("RealLifeGuide", PhoneLayoutRules.Dock)
                     && layout.Move("RealLifeFuture", PhoneLayoutRules.Business);
        string line = layout.Write();
        PhoneLayout back = PhoneLayout.Read(line);
        Check(moved && line == "1|RealLifeStocks=home|RealLifePlanner=life|RealLifeGuide=dock|RealLifeFuture=business" && back.Write() == line && back.Moves == 4
              && back.Place("RealLifeStocks") == PhoneLayoutRules.Home && back.Place("RealLifePlanner") == PhoneLayoutRules.Life && back.Place("RealLifeGuide") == PhoneLayoutRules.Dock
              && back.Place("RealLifeFuture") == PhoneLayoutRules.Business && back.Place("RealLifeCareer") == PhoneLayoutRules.Work,
            "moves are written in the order made and read back the same, an app this version lacks too (" + line + ")");
        Check(layout.Move("RealLifeStocks", PhoneLayoutRules.Money) && !layout.Moved("RealLifeStocks") && layout.Moves == 3,
            "an app put back in its usual place is no move any more");
        Check(!layout.Move("RealLifeStocks", 9) && !layout.Move(null, PhoneLayoutRules.Home) && !layout.Move("a|b", PhoneLayoutRules.Home) && !layout.Move("a=b", PhoneLayoutRules.Home) && layout.Moves == 3,
            "a place this version does not have, no name, or a name with the line's marks is refused, nothing changed");
        PhoneLayout kept = PhoneLayout.Read("1|RealLifeStocks=home|RealLifeCareer=garden|broken|RealLifeStocks=life|RealLifeCampus=work");
        Check(kept.Moves == 1 && kept.Kept == 3 && kept.Place("RealLifeStocks") == PhoneLayoutRules.Home && kept.Place("RealLifeCareer") == PhoneLayoutRules.Work
              && kept.Write() == "1|RealLifeStocks=home|RealLifeCareer=garden|broken|RealLifeStocks=life",
            "a later version's folder, a part that is no move and a second move of one app are kept as they were read and written back; a move to the usual place is dropped (" + kept.Write() + ")");
        PhoneLayout later = PhoneLayout.Read("2|RealLifeStocks=vault");
        later.Move("RealLifeGuide", PhoneLayoutRules.Home);
        Check(later.Moves == 1 && later.Kept == 1 && later.Place("RealLifeStocks") == PhoneLayoutRules.Money && later.Write() == "1|RealLifeGuide=home|2|RealLifeStocks=vault",
            "a later version's line is kept whole and written back after this version's moves");
        var full = new PhoneLayout();
        for (int i = 0; i < PhoneLayout.MostMoves + 5; i++)
        {
            full.Move("RealLifeApp" + i, PhoneLayoutRules.Business);   // (an app not in the table is at home by default: a folder is a move)
        }
        Check(full.Moves == PhoneLayout.MostMoves && PhoneLayout.Read(full.Write()).Moves == PhoneLayout.MostMoves, "the layout holds at most " + PhoneLayout.MostMoves + " moves: a line never grows without end");
        full.Reset();
        Check(full.Moves == 0 && full.Kept == 0 && full.Write() == PhoneLayout.Version, "putting every app back empties the layout");
        var a = new PhoneLayout();
        var b = new PhoneLayout();
        foreach (PhoneLayout l in new[] { a, b })
        {
            l.Move("RealLifeFlare", PhoneLayoutRules.Home);
            l.Move("RealLifeBanks", PhoneLayoutRules.Dock);
        }
        Check(a.Write() == b.Write(), "the same moves write the same line");
    }

    private static void PhoneBadges()
    {
        var sums = new int[PhoneLayoutRules.FolderCount];
        PhoneLayoutRules.FolderBadges(new[] { 1, 1, 1, 0, 2, 1 }, new[] { 0, 0, 2, 0, 0, PhoneLayoutRules.Dock }, new[] { 3, 4, 1, 9, 9, 5 }, 6, sums);
        Check(sums[0] == 7 && sums[2] == 1 && sums[1] == 0 && sums[3] == 0 && sums[4] == 0,
            "a folder's number is its apps' numbers added up; the game's own, other mods' and the dock's never count in a folder (" + string.Join(", ", sums) + ")");
        PhoneLayoutRules.FolderBadges(new[] { 1, 1 }, new[] { 1, 1 }, new[] { int.MaxValue, 5 }, 2, sums);
        Check(sums[1] == int.MaxValue, "a sum past the largest number stays the largest (the game's badge shows 99+)");
        PhoneLayoutRules.FolderBadges(null, null, null, 3, sums);
        Check(sums.All(s => s == 0), "with no buttons every folder's number is 0");
    }

    private static void PhoneTiming()
    {
        int count = 34;
        var kind = new int[count];
        var place = new int[count];
        for (int i = 0; i < count; i++)
        {
            kind[i] = i < 8 ? PhoneLayoutRules.GameItem : i < 31 ? PhoneLayoutRules.ModItem : PhoneLayoutRules.OtherItem;
            place[i] = kind[i] == PhoneLayoutRules.ModItem ? PhoneLayoutRules.Defaults[(i - 8) % PhoneLayoutRules.Defaults.Length] : PhoneLayoutRules.Home;
        }
        var view = new int[count + PhoneLayoutRules.FolderCount];
        var dock = new int[PhoneLayoutRules.DockMost];
        var members = new int[PhoneLayoutRules.FolderCount];
        ViewPass(kind, place, count, view, dock, members, 1000);   // warmed
        var clock = new Stopwatch();
        long before = GC.GetAllocatedBytesForCurrentThread();
        clock.Start();
        int sum = ViewPass(kind, place, count, view, dock, members, 20000);
        clock.Stop();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        double each = clock.Elapsed.TotalMilliseconds / 20000.0;
        Check(sum > 0 && each < 0.02, "the home screen and a folder of 34 buttons are worked out in " + each.ToString("0.00000") + " ms, wanted under 0.02 ms");
        Check(bytes == 0, "working out the views allocates nothing (" + bytes + " bytes over 20,000 passes)");
        Console.WriteLine("   the home screen and a folder of 34 buttons in " + each.ToString("0.00000") + " ms, " + bytes + " bytes.");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int ViewPass(int[] kind, int[] place, int count, int[] view, int[] dock, int[] members, int times)
    {
        int sum = 0;
        for (int t = 0; t < times; t++)
        {
            sum += PhoneLayoutRules.View(kind, place, count, -1, true, view, dock, out int docked, members) + docked;
            sum += PhoneLayoutRules.View(kind, place, count, t % PhoneLayoutRules.FolderCount, true, view, dock, out _, members);
            sum += PhoneLayoutRules.ItemTiles(false, 8, false, docked > 0) + PhoneLayoutRules.Gaps(3, 6, true);
        }
        return sum;
    }

    private static void PhoneSources(string scripts)
    {
        string pager = Read(scripts, "Core/GameUi/PhoneGridPager.cs");
        string folders = Read(scripts, "Core/GameUi/PhoneFolders.cs");
        string keys = Read(scripts, "Core/GameUi/PhoneFolderKeys.cs");
        string move = Read(scripts, "Core/GameUi/PhoneMove.cs");
        string store = Read(scripts, "Core/GameUi/PhoneLayoutPrefs.cs");
        string banks = Read(scripts, "Economy/Banking/BankApps/BanksDirector.cs");
        string bankApp = Read(scripts, "Economy/Banking/BankApps/BankApp.cs");
        string mod = Read(scripts, "Core/RealLifeOverhaulMod.cs");
        string settings = Read(scripts, "Core/Settings.cs");
        if (pager == null || folders == null || keys == null || move == null || store == null || banks == null || bankApp == null || mod == null || settings == null)
        {
            Check(false, "the phone's folders' sources are not where they were");
            return;
        }
        string view = Body(pager, "private void View()");
        Check(view != null && view.Contains("_foldered = FoldersWanted && !_noFolders") && view.Contains("if (!_foldered)") && view.Contains("_folders.Destroy();")
              && view.Contains("_items.Add(_apps[i]);") && view.Contains("PhoneLayoutRules.View(_kind, _place, count, _open, !_strip, _view, _dockAt, out int docked, _members)"),
            "the pages are the rules' view: with the option off (or no room) every button as before and nothing of the folders left on the grid");
        Check(pager.Contains("private bool FoldersWanted => PhoneLayoutPrefs.On && PhoneLayoutRules.FoldersFit(_strip, Tiles);"), "folders only with the option on and room for them");
        Check(Body(pager, "public void Refresh()")?.Contains("Pages = PagerRules.Pages(_items.Count, ItemTiles)") == true && Body(pager, "private void More()")?.Contains("Tile(MoreTile,") == true,
            "the pages and the More apps button's tile come from the view and its tiles");
        string order = Body(pager, "private void Order(int gaps)");
        Check(order != null && order.Contains("if (inOrder)") && order.Contains("SetAsLastSibling()") && order.IndexOf("_kind[i] == PhoneLayoutRules.GameItem", StringComparison.Ordinal) < order.IndexOf("_folders.BackTile", StringComparison.Ordinal),
            "the grid's children are put in the view's order, the game's own first, and moved only when not in it");
        string collect = Body(pager, "private void Collect()");
        Check(collect != null && collect.Contains("child.GetComponent<PhoneTile>() != null"), "the pager's own tiles are never counted as apps");
        Check(Body(pager, "private void StepAside()")?.Contains("DropFolders();") == true && Body(pager, "private void OnDestroy()")?.Contains("PhoneLayoutPrefs.Changed -= _onLayout;") == true
              && Body(pager, "private void HearLayout()")?.Contains("PhoneLayoutPrefs.Changed += _onLayout;") == true,
            "stepping aside or going takes every tile of the folders away, and the layout's word is heard once and let go");
        Check(Body(pager, "private void Keys()")?.Contains("bool want = !_strip && _open >= 0 && !_aside;") == true, "the cancel key is heard only on the HUD phone, only while a folder is open");
        Check(Body(pager, "private void PaintFolders()")?.Contains("PhoneLayoutRules.FolderBadges(_kind, _place, _badge, count, _sums)") == true
              && Body(pager, "private void Paint()")?.Contains("BadgeUp(_items[i])") == true,
            "each folder's badge is its apps' numbers added up, and the More apps button counts the items on other pages, folders too");
        Check(!Regex.IsMatch(folders + move + store, @"\bvoid (Update|LateUpdate|FixedUpdate)\("), "the folders' tiles, the move list and the layout run nothing per frame");
        Check(folders.Contains("UiClone.StripGameScripts(copy, PhoneGridPager.KeptOnMore)") && folders.Contains("copy.AddComponent<PhoneTile>()") && folders.Contains("AppIconPainter.Paint(")
              && folders.Contains("image.raycastTarget = false;") && folders.Contains("PhoneLayoutRules.MiniMost"),
            "a folder's tile is a copy of the game's app button without what makes it an app, with a painted tile and the small pictures of its apps");
        Check(keys.Contains("[DefaultExecutionOrder(-31000)]") && keys.Contains("PlayerAction.Cancel.Reset();") && keys.Contains("Guard.Fail(FrameGuard, e, this)")
              && keys.Contains("using var hotSpot = HotSpots.Time(HotUpdate);") && keys.Contains("TravelList.AnyOpen") && keys.Contains("HudConfirm.isOpen")
              && keys.Contains("eventData.button != PointerEventData.InputButton.Right"),
            "the cancel key is taken before the game's own handling, only when nothing else would take it, behind the guard; the move list opens on a right click only");
        Check(move.Contains("new DeskList(") && move.Contains("PhoneLayoutRules.CanMove(now, place, pager.InDock)") && move.Contains("PhoneLayoutPrefs.Move(app, place)"),
            "the move list is the game's own list window, and offers only the moves the rules allow");
        Check(store.Contains("\"reallife.phone.layout\"") && store.Contains("PlayerPrefs.GetString(Pref") && store.Contains("PlayerPrefs.SetString(Pref") && !store.Contains("SaveGameManager") && !store.Contains("modData"),
            "the layout is kept on this computer with the game's settings, never in the save and never sent to anyone (Going Public: each player's own)");
        Check(settings.Contains("public const string PhoneFoldersId = \"phone_folders\";") && settings.Contains("public static bool PhoneFolders = true;")
              && settings.Contains("PhoneFolders = Bool(modId, PhoneFoldersId, PhoneFolders);") && mod.Contains("GameUi.PhoneLayoutPrefs.Tell();"),
            "the option App folders on the phone is on by default, read with the others, and lays the phones out again at once when switched");
        Check(banks.Contains("public const string AppId = \"RealLifeBanks\";") && !banks.Contains("AppIdPrefix") && Regex.Matches(banks, @"PhoneApp\.Install\(").Count == 1
              && banks.Contains("_view?.Offer(_banks);") && bankApp.Contains("private void Which(InfoBox box)") && bankApp.Contains("box.Selector(_banks.Count, _at,"),
            "the three banks are one Banks app, the bank picked with the game's own selector on its page");
    }

    private static void PhoneTexts(string scripts, string locales)
    {
        var used = new List<string>();
        foreach (string name in new[] { "Core/GameUi/PhoneLayoutRules.cs", "Core/GameUi/PhoneFolders.cs", "Core/GameUi/PhoneMove.cs", "Economy/Banking/BankApps/BanksDirector.cs" })
        {
            string text = Read(scripts, name) ?? string.Empty;
            used.AddRange(Regex.Matches(text, @"""(reallife_(?:phone|banks)_[a-z_]+)""").Select(m => m.Groups[1].Value));
        }
        used = used.Distinct().ToList();
        Check(used.Count >= 20, "the folders' texts are named in the code (" + used.Count + ")");
        var notices = new[] { "reallife_phone_move_cannot", "reallife_phone_moved", "reallife_phone_layout_reset_done", "reallife_phone_test_pass", "reallife_phone_test_fail",
                              "reallife_phone_test_none", "reallife_phone_test_off", "reallife_phone_test_opened", "reallife_phone_test_empty" };
        foreach (string lang in new[] { "en", "pt", "zh-cn" })
        {
            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(locales, lang + ".json"))))
            {
                var missing = used.Concat(new[] { "reallife_phone_folders", "reallife_opt_tip_phone_folders", "reallife_phone_layout_reset", "reallife_phone_test_check",
                                                  "reallife_phone_test_folder", "reallife_meta_title_phonefolders", "reallife_guide_phonefolders_about", "reallife_guide_phonefolders" })
                    .Where(k => !doc.RootElement.TryGetProperty(k, out JsonElement v) || string.IsNullOrWhiteSpace(v.GetString())).Distinct().ToList();
                Check(missing.Count == 0, "every text of the folders is in " + lang + ".json (missing: " + string.Join(", ", missing) + ")");
                if (lang == "en")
                {
                    var tooLong = notices.Where(k => doc.RootElement.TryGetProperty(k, out JsonElement v) && v.GetString().Length > 240).ToList();
                    Check(tooLong.Count == 0, "every notice of the folders is at most 240 characters (" + string.Join(", ", tooLong) + ")");
                }
                bool guide = doc.RootElement.TryGetProperty("reallife_guide_phonefolders", out JsonElement page) && page.GetString().Contains("{phone_dock}");
                Check(guide, "the Guide's page in " + lang + ".json fills the dock's size from the code ({phone_dock})");
                string folderWord = lang == "en" ? "folders" : lang == "pt" ? "pastas" : "文件夹";
                bool told = false;
                foreach (JsonProperty row in doc.RootElement.EnumerateObject())
                {
                    if (row.Name.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Name.EndsWith("_title", StringComparison.Ordinal) && row.Value.ValueKind == JsonValueKind.String)
                    {
                        string said = row.Value.GetString();
                        told |= said.Contains(folderWord) && (said.Contains("Banks") || said.Contains("Bancos") || said.Contains("银行应用"));
                    }
                }
                Check(told, "What's new in " + lang + ".json tells of the folders and the one Banks app");
            }
        }
        string features = Read(scripts, "Meta/FeatureList.cs") ?? string.Empty;
        Check(features.Contains(".Fact(\"phone_dock\", () => Whole(GameUi.PhoneLayoutRules.DockMost))") && features.Contains(".Owns(Settings.PhoneFoldersId)"),
            "the folders are a feature of the options page with its Guide page, the dock's size filled from the code");
    }
}
