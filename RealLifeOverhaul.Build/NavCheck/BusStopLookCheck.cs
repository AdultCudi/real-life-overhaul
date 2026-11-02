using System;
using System.IO;
using RealLifeOverhaul;

// Checks for a bus stop whose modelled pieces are missing (Transport/BusStopLookRules.cs and Core/PropFitRules.cs, and over the sources): a stop made
// with a missing piece is drawn in plain shapes, or bare when the game has no lit shader, and its pointers are the same either way; a material that
// would be drawn white (a placeholder atlas, a shader the game's build or graphics setting does not give) is refused and said in one line with its
// cause; a stop built before the props came in is made again as modelled; and the sources keep the pointer apart from the drawing and the welcome
// saying a failed bundle.
internal static partial class Program
{
    private static void BusStopLooks()
    {
        StopAlwaysClickable(BusStopLookRules.Look.Model, BusStopLookRules.Look.Plain, BusStopLookRules.Look.Bare);
        BusStopLooksMade();
        BusStopFit();
        BusStopSources();
    }

    // Every way a stop's two pieces can come out, the pointers still whole: the click is a box of its own, whatever is drawn.
    private static void StopAlwaysClickable(params BusStopLookRules.Look[] looks)
    {
        foreach (BusStopLookRules.Look sign in looks)
        {
            foreach (BusStopLookRules.Look shelter in looks)
            {
                bool pole = BusStopLookRules.Clickable(sign, true);
                bool bench = BusStopLookRules.Clickable(shelter, true);
                Check(pole && bench, "bus stop looks: a stop with its sign " + sign + " and its shelter " + shelter + " still has both pointers");
            }
            Check(!BusStopLookRules.Clickable(sign, false), "bus stop looks: without the game's interactive layer no pointer is made (and it is tried again), " + sign);
            Check(!BusStopLookRules.Outlined(sign, 0), "bus stop looks: a sign with nothing drawn has nothing to outline, " + sign);
            Check(BusStopLookRules.Outlined(sign, 3) == (sign != BusStopLookRules.Look.Bare), "bus stop looks: a drawn " + sign + " sign is outlined when pointed at, a bare one is not");
        }
    }

    private static void BusStopLooksMade()
    {
        Check(BusStopLookRules.Of(true, true) == BusStopLookRules.Look.Model && BusStopLookRules.Of(true, false) == BusStopLookRules.Look.Model, "bus stop looks: the bundle's model is used when it is there");
        Check(BusStopLookRules.Of(false, true) == BusStopLookRules.Look.Plain, "bus stop looks: a missing piece is plain shapes");
        Check(BusStopLookRules.Of(false, false) == BusStopLookRules.Look.Bare, "bus stop looks: a missing piece with no lit shader is bare, and the stop is still clickable");
        Check(BusStopLookRules.Remodel(BusStopLookRules.Look.Plain, true, true) && BusStopLookRules.Remodel(BusStopLookRules.Look.Bare, true, true),
              "bus stop looks: a stop built before the props came in is made again once the bundle has the piece");
        Check(!BusStopLookRules.Remodel(BusStopLookRules.Look.Plain, false, true) && !BusStopLookRules.Remodel(BusStopLookRules.Look.Plain, true, false)
              && !BusStopLookRules.Remodel(BusStopLookRules.Look.Model, true, true),
              "bus stop looks: not made again while the props load, when the bundle lacks the piece, or when it is a model already");
    }

    private static void BusStopFit()
    {
        Check(PropFitRules.Problem(4096, 2048, true, true, true) == null && PropFitRules.Problem(2048, 2048, true, true, true) == null, "prop fit: the real atlases and a shader that is there are fit");
        Check(PropFitRules.Problem(1, 1, true, true, true) != null && PropFitRules.Problem(0, 0, true, true, true) != null
              && PropFitRules.Problem(PropFitRules.SmallestAtlas - 1, 4096, true, true, true) != null, "prop fit: a placeholder atlas, which would draw white, is refused");
        Check(PropFitRules.Problem(4096, 2048, false, false, false).Contains("not found") && PropFitRules.Problem(4096, 2048, true, false, true).Contains("not supported")
              && PropFitRules.Problem(4096, 2048, true, true, false).Contains("base colour map"), "prop fit: a missing, unsupported or colourless shader each says so");
        string line = PropFitRules.Line("bus stop piece 'bus_stop_sign'", "RealLifeOverhaul Props", "the texture atlas is 1 x 1", true);
        Check(line.Contains("bus_stop_sign") && line.Contains("RealLifeOverhaul Props") && line.Contains("1 x 1") && line.Contains("ModsLocal") && line.Contains("Workshop") && line.Contains("build"),
              "prop fit: the line names the piece, the material, the reason and what to check (ModsLocal, the Workshop download, the game's build)");
        Check(!line.Contains("!") && !line.Contains("–") && !line.Contains("—"), "prop fit: the line has no exclamation mark or dash");
    }

    // Over the sources: the pointer does not read how the stop is drawn, the welcome says a failed bundle, and the library keeps why.
    private static void BusStopSources()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string dressing = Code(File.ReadAllText(Path.Combine(scripts, "Transport", "BusStopDressing.cs")));
        string pointing = Code(File.ReadAllText(Path.Combine(scripts, "Transport", "TransitDirector.Pointing.cs")));
        string welcome = Code(File.ReadAllText(Path.Combine(scripts, "Meta", "Welcome.cs")));
        string library = Code(File.ReadAllText(Path.Combine(scripts, "Core", "PropLibrary.cs")));
        Check(dressing.Contains("BusStopLookRules.Of(") && dressing.Contains("Say(\"bus_stop_sign\"") && dressing.Contains("Say(\"bus_shelter\""),
              "bus stop looks: the sign and the shelter each say a piece drawn plain");
        Check(dressing.Contains("private void Remodel()") && dressing.Contains("            Remodel();"), "bus stop looks: a stop is made again as modelled when it is put out");
        Check(pointing.Contains("_pointed[i] = BusStopLookRules.Clickable("), "bus stop looks: the pointers are made whatever the stop is drawn with, and tried again if the game would not take them");
        Check(welcome.Contains("reallife_props_note_failed") && welcome.Contains("PropLibrary.Trouble"), "bus stop looks: the welcome says when the props failed");
        Check(library.Contains("PropFitRules.Problem(") && library.Contains("public static string Why(string id)") && library.Contains("Trouble = "),
              "bus stop looks: the props library refuses a material that would draw white, and keeps why");
    }
}
