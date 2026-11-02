using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RealLifeOverhaul;

// Checks for the code of moments of play compiled behind the loading screen (Core/CodeWarm.cs's RootAll, compiled as it
// ships; Core/MomentWarm.cs drives it) and for the frames those moments were cut into. The owner's log of 2 Nov 2026: the
// first car of a session had its handling put on in 4.20 ms where the next car's took under 0.74, a car's handling set up
// again was a frame of 2.19 ms with the last car's numbers taken off, and the street chess opponent was seated in 4.84 ms
// and the onlooker in 2.36, each against 2 ms. Here, on a made-up stall of this checker's own: a type named whole is taken
// in with its constructors and accessors, never its static constructor, its base type's or its nested types' methods unless
// its own code calls them, and what it calls to the walk's depth; a second naming takes nothing again; and done a slice at a
// time it takes in what one done whole takes in. Over the sources: the moment's piece runs behind the loading screen only,
// named from a feature's Awake; the chess set-up and the car's handling a part a frame, every part of their lines named;
// and a person of the city's pool woken where they are to stand, so their agent is made on the navigation mesh.
//
// What is not here: what the compile costs in the game (the "Moment warm check" line names it; offline, the walk from the
// named types reaches some 1,600 methods), and the frames themselves, which the "Chess check (set up a frame a piece)" and
// "Car tuning check (sums spread over frames)" lines judge in play.
internal static partial class Program
{
    private static void MomentWarms()
    {
        MomentRoots();
        MomentDepth();
        MomentAgain();
        MomentSliced();
        MomentSources();
    }

    private static Module MomentModule => typeof(MomentStall).Module;

    // The walk from the made-up stall named whole, compiling nothing: the order methods were taken in.
    private static List<MethodBase> MomentWalk(int deepest, double sliceMs, out CodeWarm warm)
    {
        var order = new List<MethodBase>();
        warm = new CodeWarm(MomentModule, 10000, deepest, m =>
        {
            order.Add(m);
            return true;
        });
        warm.RootAll(typeof(MomentStall));
        for (int slices = 0; slices < 100000; slices++)
        {
            WorkSlice s = sliceMs < 0 ? WorkSlice.Whole : WorkSlice.Of(sliceMs, 1);
            if (warm.Run(ref s))
            {
                break;
            }
        }
        return order;
    }

    private static bool MomentHas(List<MethodBase> order, Type type, string name)
    {
        for (int i = 0; i < order.Count; i++)
        {
            if (order[i].DeclaringType == type && order[i].Name == name)
            {
                return true;
            }
        }
        return false;
    }

    private static int MomentCount(List<MethodBase> order, Type type, string name)
    {
        int n = 0;
        for (int i = 0; i < order.Count; i++)
        {
            n += order[i].DeclaringType == type && order[i].Name == name ? 1 : 0;
        }
        return n;
    }

    private static void MomentRoots()
    {
        List<MethodBase> order = MomentWalk(2, -1.0, out CodeWarm warm);
        Type stall = typeof(MomentStall);
        Check(MomentCount(order, stall, ".ctor") == 2 && MomentHas(order, stall, "get_Sold") && MomentHas(order, stall, "set_Sold")
              && MomentHas(order, stall, "add_Rang") && MomentHas(order, stall, "remove_Rang"),
            "moment warm: a type named whole has both its constructors and its property's and event's accessors taken in");
        Check(MomentHas(order, stall, "Price") && MomentHas(order, stall, "Sell") && MomentHas(order, stall, "Start"),
            "moment warm: every method the type declares is taken in, public or private, static or not, an override too");
        Check(!MomentHas(order, stall, ".cctor"), "moment warm: never the static constructor, which the runtime runs once, when it must");
        Check(!MomentHas(order, typeof(MomentBase), "Shared") && !MomentHas(order, typeof(MomentStall.Shelf), "Inside"),
            "moment warm: neither the base type's methods nor a nested type's that nothing of the type calls");
        Check(!MomentHas(order, stall, "Open") && warm.Skipped >= 1, "moment warm: an open generic method has nothing to compile and is counted as such");
        Check(MomentHas(order, typeof(MomentHelp), "Wrap") && MomentHas(order, typeof(MomentHelp), "Deeper") && !MomentHas(order, typeof(MomentHelp), "Unasked"),
            "moment warm: what the type's code calls is walked, and only that of the type it calls into");
        var once = new HashSet<MethodBase>(order);
        Check(once.Count == order.Count, "moment warm: each method is taken in once");
    }

    private static void MomentDepth()
    {
        List<MethodBase> shallow = MomentWalk(1, -1.0, out _);
        Check(MomentHas(shallow, typeof(MomentHelp), "Wrap") && !MomentHas(shallow, typeof(MomentHelp), "Deeper"),
            "moment warm: the walk stops at its depth, a call from a root compiled but not read further");
    }

    private static void MomentAgain()
    {
        MomentWalk(2, -1.0, out CodeWarm warm);
        int again = warm.RootAll(typeof(MomentStall));
        int none = warm.RootAll(null);
        int helper = warm.RootAll(typeof(MomentHelp));
        Check(again == 0 && none == 0 && helper == 1,
            "moment warm: a type named again takes nothing new, and a type half met takes only what was not met (" + again + ", " + none + ", " + helper + ")");
    }

    private static void MomentSliced()
    {
        List<MethodBase> whole = MomentWalk(2, -1.0, out _);
        List<MethodBase> sliced = MomentWalk(2, 0.0, out _);   // a method a slice
        bool same = whole.Count == sliced.Count;
        for (int i = 0; same && i < whole.Count; i++)
        {
            same = whole[i] == sliced[i];
        }
        Check(same, "moment warm: done a method a slice it takes in what the walk done whole takes in, in the same order");
    }

    // The members of an enum, or the texts of a string array, in the source: for each item of a line, its name.
    private static int MomentItems(string code, string start, bool texts)
    {
        string body = Body(code, start);
        if (body.Length == 0)
        {
            return -1;
        }
        if (texts)
        {
            return Count(body, "\"") / 2;
        }
        int n = 0;
        foreach (string part in body.Trim('{', '}').Split(','))
        {
            n += part.Trim().Length > 0 ? 1 : 0;
        }
        return n;
    }

    // The text of one case of a switch, up to the next case or default.
    private static string MomentCase(string body, string label)
    {
        int at = body.IndexOf(label, StringComparison.Ordinal);
        if (at < 0)
        {
            return string.Empty;
        }
        int next = body.IndexOf("case ", at + label.Length, StringComparison.Ordinal);
        int fallback = body.IndexOf("default:", at + label.Length, StringComparison.Ordinal);
        int end = next < 0 ? fallback : fallback < 0 ? next : Math.Min(next, fallback);
        return end < 0 ? body.Substring(at) : body.Substring(at, end - at);
    }

    private static void MomentSources()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string moment = Source(scripts, @"Core\MomentWarm.cs");
        string run = Body(moment, "private static bool Run()");
        Check(moment.Contains("ScenePrewarm.Add(What, Run);") && run.Contains("WorkSlice.Whole") && run.Contains("_warm.RootAll(Named[i]);")
              && !moment.Contains("WorkSlice.Of(") && !moment.Contains("ScenePrewarm.Again("),
            "moment warm source: the named types' code is compiled whole in one piece behind the loading screen, never a slice in play");
        Check(run.Contains("if (Meta.Dev.Verbose)") && run.Contains("Moment warm check (behind the loading screen)") && run.Contains("UI.Load.LoadScene.isLoading"),
            "moment warm source: its line, with Developer diagnostics on, says whether it ran behind the loading screen");

        // Named only from a feature's Awake: the loading screen's pieces run after every feature's Awake.
        int callers = 0, outside = 0;
        foreach (string path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            if (path.EndsWith("MomentWarm.cs", StringComparison.Ordinal))
            {
                continue;
            }
            string code = Code(File.ReadAllText(path));
            for (int at = code.IndexOf("MomentWarm.Add(", StringComparison.Ordinal); at >= 0; at = code.IndexOf("MomentWarm.Add(", at + 1, StringComparison.Ordinal))
            {
                callers++;
                int awake = code.LastIndexOf("void Awake()", at, StringComparison.Ordinal);
                int open = awake >= 0 ? code.IndexOf('{', awake) : -1;
                int close = open >= 0 ? BraceEnd(code, open) : -1;
                outside += open >= 0 && open < at && at < close ? 0 : 1;
            }
        }
        Check(callers >= 2 && outside == 0, "moment warm source: every moment is named from a feature's Awake (" + callers + " named, " + outside + " elsewhere)");

        string tuning = Source(scripts, @"Driving\CarTuning.cs");
        string awakeCar = Body(tuning, "private void Awake()");
        Check(awakeCar.Contains("_handling.Prepare();") && awakeCar.Contains("MomentWarm.Add(") && awakeCar.Contains("typeof(CarTuning)") && awakeCar.Contains("typeof(CarHandling)")
              && awakeCar.Contains("typeof(HandlingDesign)") && awakeCar.Contains("typeof(CarSolve)"),
            "moment warm source: a car's sums and handling are named, and the handling's own presets made, as the city loads");
        string leisure = Source(scripts, @"Leisure\LeisureDirector.cs");
        string awakeLeisure = Body(leisure, "private void Awake()");
        Check(awakeLeisure.Contains("MomentWarm.Add(") && awakeLeisure.Contains("typeof(ChessCorner)") && awakeLeisure.Contains("typeof(CityPerson)")
              && awakeLeisure.Contains("typeof(CityPeople)"),
            "moment warm source: the street chess set-up, and the pool and people it takes, are named as the city loads");
        string ledger = Body(Source(scripts, @"Ledger\LedgerDirector.cs"), "private void Awake()");
        Check(ledger.Contains("MomentWarm.Add(") && ledger.Contains("typeof(LedgerDirector)") && ledger.Contains("typeof(LedgerStore)"),
            "moment warm source: the City Ledger's first load after the save is named as the city loads");

        // The car: the design made and the mod's presets filled in a frame of their own, nothing of the car's touched there.
        string handling = Source(scripts, @"Driving\CarHandling.cs");
        string make = Body(handling, "public bool Make()");
        string finish = Body(handling, "public bool Finish()");
        string prepare = Body(handling, "public void Prepare()");
        Check(make.Contains("Build(vehicle);") && !make.Contains("LiftParts(") && !make.Contains("PutOn(") && !make.Contains("Record(")
              && !make.Contains("AddAndOnboardNewComponent("),
            "car handling source: the frame that makes the design fills the mod's own tyres and curves and touches nothing of the car");
        Check(finish.Contains("if (!_made && !Make())") && !finish.Contains("Build("),
            "car handling source: the frame that puts it on finds it made (or makes it first, as before), never builds it again");
        Check(prepare.Contains("CreateInstance<FrictionPreset>()") && prepare.Contains("CreateInstance<GroundDetectionPreset>()"),
            "car handling source: the mod's own tyres and ground preset are made ahead");
        string carry = Body(tuning, "private void Carry(ref WorkSlice slice, long began)");
        Check(MomentCase(carry, "case Sums.HandlingMade:").Contains("_handling.Make()") && !MomentCase(carry, "case Sums.HandlingMade:").Contains("_handling.Finish()")
              && MomentCase(carry, "case Sums.HandlingOn:").Contains("_handling.Finish()") && Body(tuning, "private static bool Heavy(Sums item)").Contains("Sums.HandlingMade"),
            "car tuning source: the handling made and the handling put on are a frame each, both heavy");
        string update = Body(tuning, "private void OnUpdate()");
        int putBack = update.IndexOf("_spread.Item((int)Sums.PutBack, began);", StringComparison.Ordinal);
        int carried = update.IndexOf("Carry(ref slice", StringComparison.Ordinal);
        Check(putBack > 0 && carried > putBack && update.Substring(putBack, carried - putBack).Contains("return;")
              && update.IndexOf("Restore();", StringComparison.Ordinal) < putBack,
            "car tuning source: the last car's numbers taken off are a frame of their own, the next car's sums from the next frame");
        int sums = MomentItems(tuning, "private enum Sums", false);
        int names = MomentItems(tuning, "private static readonly string[] ItemNames", true);
        Check(sums > 0 && sums == names, "car tuning source: every item of the sums' line has its name (" + sums + " items, " + names + " names)");

        // The chess set-up: one part a frame, its line leaving out the frames a pause or a screen hides.
        string chess = Source(scripts, @"Leisure\Activities\ChessGame.cs");
        string step = Body(chess, "public void Step()");
        string[] parts = { "case SetUp.OpponentTaken:", "case SetUp.OpponentSeated:", "case SetUp.OnlookerTaken:", "case SetUp.OnlookerWatching:" };
        string[] heavy = { "People.Take(", ".Work(", "Watch(city" };
        bool onePart = true;
        string tally = string.Empty;
        foreach (string label in parts)
        {
            string section = MomentCase(step, label);
            int n = 0;
            foreach (string h in heavy)
            {
                n += Count(section, h);
            }
            onePart &= section.Length > 0 && n == 1;
            tally += (tally.Length > 0 ? ", " : string.Empty) + n;
        }
        Check(onePart, "chess source: the opponent taken, the opponent seated, the onlooker taken and the onlooker set watching are a frame each, one heavy call each (" + tally + ")");
        string watch = Body(chess, "private CityPerson Watch(CityLifeDirector city, Vector3 table, Vector3 along)");
        Check(watch.Contains("People.Take(") && !watch.Contains(".Work("), "chess source: the onlooker is taken in one frame and set to their clip in the next");
        string put = Body(chess, "public bool PutOut(Vector3 anchor, Vector3 player, out TestRefusal why)");
        Check(put.Contains("_pending = SetUp.OpponentTaken;") && put.Contains("_spread.Begin();") && !put.Contains("People.Take(") && !put.Contains(".Work("),
            "chess source: the table goes out alone, its line begun");
        string count = Body(chess, "private void Count(SetUp part, long began, bool unseen)");
        string say = Body(chess, "private void Say(string ended)");
        Check(step.Contains("bool unseen = Looks.Hidden;") && count.IndexOf("_unseen++;", StringComparison.Ordinal) >= 0
              && count.IndexOf("_unseen++;", StringComparison.Ordinal) < count.IndexOf("_spread.Item(", StringComparison.Ordinal)
              && say.Contains("if (!Meta.Dev.Verbose)") && say.Contains("_spread.Verdict(false)"),
            "chess source: its line, with Developer diagnostics on, judges each frame in play against 2 ms and leaves out what a pause or a screen hides");
        int setUps = MomentItems(chess, "private enum SetUp", false);
        int partNames = MomentItems(chess, "private static readonly string[] Parts", true);
        Check(setUps > 0 && setUps == partNames, "chess source: every part of the set-up's line has its name (" + setUps + " parts, " + partNames + " names)");

        // A person of the pool woken where they are to stand: woken under the city, their agent was made off the mesh.
        string people = Source(scripts, @"CityLife\CityPeople.cs");
        string take = Body(people, "public CityPerson Take(CityLook look, Vector3 spot, Vector3 face, bool fresh)");
        int there = take.IndexOf("person.Character.transform.position = spot;", StringComparison.Ordinal);
        int woken = take.IndexOf("person.Character.gameObject.SetActive(true);", StringComparison.Ordinal);
        Check(there >= 0 && woken > there, "city people source: a person taken from the pool is put at their spot before they are woken");
        string shelf = Source(scripts, @"CityLife\CityPeople.Shelf.cs");
        string redress = Body(shelf, "private void Redress(CityPerson person, CityLook look)");
        int off = redress.IndexOf("agent.enabled = false;", StringComparison.Ordinal);
        int awakeShelf = redress.IndexOf("body.SetActive(true);", StringComparison.Ordinal);
        int asleep = redress.IndexOf("body.SetActive(false);", StringComparison.Ordinal);
        int on = redress.IndexOf("agent.enabled = true;", StringComparison.Ordinal);
        Check(off >= 0 && awakeShelf > off && asleep > awakeShelf && on > asleep,
            "city people source: a look put on at the shelf wakes them with their agent off, and gives it back once they sleep again");
    }
}

// The made-up stall a moment's code is named by, for the walk above.
internal abstract class MomentBase
{
    public abstract int Price();

    public int Shared() => 7;   // the base type's own: taken only if something calls it
}

internal sealed class MomentStall : MomentBase
{
    public static int Opened = Start();   // a static constructor of its own
    private static readonly int[] Prices = { 3, 5, 8 };
    private int _sold;

    public MomentStall()
    {
    }

    public MomentStall(int sold)
    {
        _sold = sold;
    }

    public int Sold
    {
        get => _sold;
        set => _sold = value;
    }

    public event Action Rang;

    public override int Price() => Prices[_sold % Prices.Length];

    public int Sell()
    {
        _sold++;
        Rang?.Invoke();
        return MomentHelp.Wrap(Price());
    }

    public T Open<T>() => default;   // open generic: nothing of its own to compile

    private static int Start() => 1;

    internal sealed class Shelf
    {
        public int Inside() => 2;   // a nested type nothing of the stall calls
    }
}

internal static class MomentHelp
{
    public static int Wrap(int price) => Deeper(price) + 1;

    private static int Deeper(int price) => price * 2;

    public static int Unasked() => 4;   // in a type the stall calls into, but never called
}
