using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// Checks for the distance culling of the mod's own things (Performance/CullRules.cs, Performance/CullBook.cs): the
// game's two bands; what may be culled and what never is, kind by kind, with every kind of thing the mod puts in the
// world in the inventory and its files on disk; registrations only from the files the inventory names for a kind
// culled here, and each taken out again; and the book of the culling group's slots, which a thing that leaves must not
// leave holding another's state.
internal static partial class Program
{
    private static void Culling()
    {
        CullBands();
        CullInventory();
        CullSources();
        CullBookSlots();
        CullBookQueue();
        CullBookCost();
    }

    // The bands are the game's own (Culling.CullingManager: FirstBand 60, SecondBand 200), and only beyond the far one is
    // a registered thing not drawn; the city map draws everything.
    private static void CullBands()
    {
        Check(CullRules.NearBand == 60f && CullRules.FarBand == 200f, "culling: the bands are the game's own, 60 m and 200 m");
        Check(CullRules.Near == 0 && CullRules.Middle == 1 && CullRules.Far == 2, "culling: the bands are numbered as the engine reports them");
        for (int k = 0; k < (int)CullRules.Kind.Count; k++)
        {
            var kind = (CullRules.Kind)k;
            Check(CullRules.MayCull(kind), "culling: " + kind + " may be culled");
            Check(CullRules.Drawn(kind, CullRules.Near, false) && CullRules.Drawn(kind, CullRules.Middle, false) && !CullRules.Drawn(kind, CullRules.Far, false),
                "culling: " + kind + " is drawn within the far band and not beyond it");
            Check(CullRules.Drawn(kind, CullRules.Far, true), "culling: " + kind + " is drawn beyond the far band while the city map is open");
            Check(CullRules.Name(kind) != "other", "culling: " + kind + " has a name for the census");
        }
        Check(!CullRules.MayCull(CullRules.Kind.Count) && !CullRules.MayCull((CullRules.Kind)(-1)), "culling: nothing outside the kinds may be culled");
        Check(CullRules.Drawn(CullRules.Kind.Count, CullRules.Far, false), "culling: a thing of no kind is always drawn");
    }

    // Every line of the inventory says how its things are kept cheap, why, and from which files; the kinds culled here are
    // each one line; the lines that are never culled name what gameplay needs; every file named is there.
    private static void CullInventory()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var here = new int[(int)CullRules.Kind.Count];
        int never = 0, missing = 0;
        string lost = null;
        foreach (CullRules.Thing thing in CullRules.Inventory)
        {
            Check(!string.IsNullOrWhiteSpace(thing.What) && !string.IsNullOrWhiteSpace(thing.Why) && !string.IsNullOrWhiteSpace(thing.Files),
                "culling: the inventory's line '" + thing.What + "' says what, why and where");
            Check((thing.Way == CullRules.Way.Here) == CullRules.MayCull(thing.Kind), "culling: '" + thing.What + "' has a kind exactly when it is culled here");
            if (thing.Way == CullRules.Way.Here)
            {
                here[(int)thing.Kind]++;
            }
            if (thing.Way == CullRules.Way.Never)
            {
                never++;
            }
            foreach (string file in thing.Files.Split(' '))
            {
                if (!File.Exists(Path.Combine(scripts, file.Replace('/', '\\'))))
                {
                    missing++;
                    lost = file;
                }
            }
        }
        for (int k = 0; k < here.Length; k++)
        {
            Check(here[k] == 1, "culling: the kind " + (CullRules.Kind)k + " is one line of the inventory (" + here[k] + ")");
        }
        Check(missing == 0, "culling: every file the inventory names is there" + (lost != null ? " (not: " + lost + ")" : ""));
        Check(never >= 8, "culling: the inventory names what gameplay needs, never culled (" + never + " lines)");
        string[] needs = { "an incident in progress", "a car the player chases", "a person in a conversation", "the player waits for it or rides it" };
        foreach (string need in needs)
        {
            bool named = false;
            foreach (CullRules.Thing thing in CullRules.Inventory)
            {
                named |= thing.Way == CullRules.Way.Never && thing.Why.StartsWith(need, StringComparison.Ordinal);
            }
            Check(named, "culling: never culled: " + need);
        }
    }

    // Over the sources as they ship: a thing is registered (ModCulling.Add) only from a file the inventory names for a kind
    // culled here, with that kind; every file that registers also takes its things out (ModCulling.Remove); and the culling
    // touches nothing but drawing: forceRenderingOff, never SetActive, enabled or a collider.
    private static void CullSources()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var allowed = new Dictionary<string, CullRules.Kind>(StringComparer.OrdinalIgnoreCase);
        foreach (CullRules.Thing thing in CullRules.Inventory)
        {
            if (thing.Way == CullRules.Way.Here)
            {
                foreach (string file in thing.Files.Split(' '))
                {
                    allowed[file.Replace('/', '\\')] = thing.Kind;
                }
            }
        }
        var registered = new HashSet<CullRules.Kind>();
        var adders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);    // owners (by the inventory's file) that register
        var removers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);  // and those that take out, in any of their part files
        int adds = 0;
        string stray = null;
        foreach (string path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string rel = path.Substring(scripts.Length + 1);
            string code = Code(File.ReadAllText(path));
            string owner = Owner(rel, allowed);
            if (owner != null && code.Contains("ModCulling.Remove("))
            {
                removers.Add(owner);
            }
            foreach (Match call in Regex.Matches(code, @"ModCulling\.Add\(\s*CullRules\.Kind\.(\w+)"))
            {
                adds++;
                bool known = Enum.TryParse(call.Groups[1].Value, out CullRules.Kind kind);
                if (owner == null || !known || allowed[owner] != kind)
                {
                    stray = rel + " (" + call.Groups[1].Value + ")";
                    continue;
                }
                registered.Add(kind);
                adders.Add(owner);
            }
        }
        foreach (string owner in adders)
        {
            Check(removers.Contains(owner), "culling: " + owner + " takes out what it registers");
        }
        Check(adds >= 7 && stray == null, "culling: things are registered only from the inventory's files, with their kind (" + adds + " calls" + (stray != null ? "; not: " + stray : "") + ")");
        Check(registered.Count == (int)CullRules.Kind.Count, "culling: every kind culled here is registered somewhere (" + registered.Count + ")");
        string culling = Code(File.ReadAllText(Path.Combine(scripts, @"Performance\ModCulling.cs")));
        Check(culling.Contains("forceRenderingOff = true") && culling.Contains("forceRenderingOff = false"), "culling: things are hidden from drawing only");
        Check(!culling.Contains("SetActive(") && !culling.Contains(".enabled =") && !culling.Contains("Collider") && !culling.Contains("NavMesh"),
            "culling: nothing but drawing changes: no object switched off, no component, no collider, no navigation box");
        Check(culling.Contains("SetBoundingDistances(Bands)") && culling.Contains("SetDistanceReferencePoint(player)"), "culling: the group measures the game's bands from the player");
        Check(Regex.IsMatch(culling, @"if \(Book\.Waiting == 0\)\s*\{\s*return;"), "culling: a frame with no change of band costs one comparison");
        string director = Code(File.ReadAllText(Path.Combine(scripts, @"Performance\PerfDirector.cs")));
        Check(director.Contains("ModCulling.Frame();") && director.Contains("ModCulling.Reset();"), "culling: the performance director applies the changes and forgets the city's things with it");
    }

    private static string Owner(string rel, Dictionary<string, CullRules.Kind> allowed)
    {
        foreach (string file in allowed.Keys)
        {
            // A part file of an owner counts as the owner (Law\PrecinctFrontage.Wall.cs is Law\PrecinctFrontage.cs's).
            string stem = file.Substring(0, file.Length - 3);
            if (string.Equals(rel, file, StringComparison.OrdinalIgnoreCase) || rel.StartsWith(stem + ".", StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }
        return null;
    }

    // Slots stay packed: a thing that leaves gives its slot to the last one, which is looked at again; ids are given out
    // again; a thing not in the book changes nothing.
    private static void CullBookSlots()
    {
        var book = new CullBook(4);
        int a = book.Add(1), b = book.Add(1), c = book.Add(1);
        Check(book.Count == 3 && book.SlotOf(a) == 0 && book.SlotOf(b) == 1 && book.SlotOf(c) == 2, "culling book: things take slots in turn");
        while (book.Next(2, out _))
        {
        }
        int moved = book.Remove(a, 3);
        Check(moved == c && book.SlotOf(c) == 0 && book.IdAt(0) == c && book.Count == 2 && book.SlotOf(a) == -1, "culling book: the last thing moves into the slot of one that leaves");
        Check(book.Next(4, out int again) && again == c && !book.Next(4, out _), "culling book: the moved thing is looked at again, and only it");
        Check(book.Remove(b, 5) == -1 && book.Count == 1, "culling book: the last thing leaving moves nobody");
        Check(book.Remove(a, 5) == -1 && book.Remove(-1, 5) == -1 && book.Remove(99, 5) == -1 && book.Count == 1, "culling book: a thing not in it changes nothing");
        int d = book.Add(6);
        Check(d == a || d == b, "culling book: an id given back is given out again");
        for (int i = 0; i < 40; i++)
        {
            book.Add(7);
        }
        Check(book.Count == 42 && book.Capacity >= 42, "culling book: it grows past its first size");
        bool packed = true;
        for (int slot = 0; slot < book.Count; slot++)
        {
            packed &= book.SlotOf(book.IdAt(slot)) == slot;
        }
        Check(packed, "culling book: every slot's thing knows its slot after growing");
        book.Clear();
        Check(book.Count == 0 && book.Waiting == 0 && !book.Next(100, out _), "culling book: cleared, it holds nothing");
    }

    // A thing is looked at only in a frame after it was queued (the engine has culled it since); once however often it was
    // queued; a thing taken out while it waits is passed over.
    private static void CullBookQueue()
    {
        var book = new CullBook(8);
        int a = book.Add(10);
        Check(!book.Next(10, out _), "culling book: a new thing waits a frame for the engine's first cull");
        Check(book.Next(11, out int first) && first == a && !book.Next(11, out _), "culling book: and is looked at in the next");
        book.Changed(book.SlotOf(a), 12);
        book.Changed(book.SlotOf(a), 12);
        book.QueueAll(12);
        Check(book.Waiting == 1, "culling book: a thing queued three times waits once");
        int b = book.Add(12);
        book.Remove(a, 12);
        Check(book.Next(13, out int next) && next == b && !book.Next(13, out _), "culling book: a thing taken out while it waits is passed over");
        book.Changed(99, 14);
        book.Changed(-1, 14);
        Check(book.Waiting == 0, "culling book: a change for a slot nobody holds queues nothing");
    }

    // A city's worth of things (fifty bus stops, fronts, signs) added, moved and taken out, many times: bounded and quick.
    private static void CullBookCost()
    {
        long ticks = CullBookRun();
        double ms = ticks * 1000.0 / Stopwatch.Frequency;
        Check(ms < 200.0, "culling book: 1,000 rounds of 64 things added, looked at and taken out in under 200 ms (" + ms.ToString("F1") + " ms)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long CullBookRun()
    {
        var book = new CullBook(64);
        var ids = new int[64];
        long start = Stopwatch.GetTimestamp();
        for (int round = 0; round < 1000; round++)
        {
            int frame = round * 3;
            for (int i = 0; i < 64; i++)
            {
                ids[i] = book.Add(frame);
            }
            while (book.Next(frame + 1, out _))
            {
            }
            for (int i = 0; i < 64; i += 2)
            {
                book.Remove(ids[i], frame + 2);
            }
            for (int i = 1; i < 64; i += 2)
            {
                book.Remove(ids[i], frame + 2);
            }
            while (book.Next(frame + 3, out _))
            {
            }
        }
        return Stopwatch.GetTimestamp() - start;
    }
}
