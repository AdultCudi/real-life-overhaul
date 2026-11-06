using System;
using System.IO;

// Checks, over the mod's sources as they ship, that the one-off work of the first seconds after a load stays where it
// costs nothing to see: behind the loading screen (Core/SceneFinds.cs, ScenePrewarm), or read from the game's own lists.
// A session's log had a frame of 1381 ms just after a save loaded, in which the landmarks (six
// searches of the whole scene), the scooter stands, the skies (two) and the street lamps were each looked for, a frame
// of 196 ms after it in which the far districts gathered every renderer of the city, and a frame of some 135 ms on the
// first drive of each session, in which Empire Motors' view was looked for by a search of the scene. Each is now handed
// to ScenePrewarm by the feature that needs it, or read from a list the game keeps; these checks fail if one comes back.
//
// What is not here: how long anything takes in the game, which the "Session: a frame of ..." and "Scene search check
// (...)" lines of Player.log show ("behind the loading screen ...: PASS").
internal static partial class Program
{
    private static void Prewarms()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        if (!Directory.Exists(scripts))
        {
            Check(false, "prewarm: the mod's sources are at " + scripts);
            return;
        }

        Handed(scripts, @"Leisure\LeisureDirector.cs", "LeisureVenues.PrewarmLandmarks", "the city's landmarks");
        Handed(scripts, @"Leisure\Sound\LeisureSound.cs", "PrewarmLoops", "the ambient sound's loops");
        Handed(scripts, @"Garage\GarageDirector.cs", "_bikes.Places.PrewarmStands", "the scooter stands of the bike-share docks");
        Handed(scripts, @"Performance\PerfDirector.cs", "PerfTweaks.Prewarm", "the performance tweaks' looks at the scene");
        Handed(scripts, @"Camera\ChaseCamera.cs", "EmpireView.Prewarm", "Empire Motors' view from the driver's seat");
        Handed(scripts, @"Core\RealLifeOverhaulMod.cs", "TrafficEffects.PrewarmSirens", "the sirens' sounds (made in the frame the first ambulance or patrol car was sent)");
        string effects = Source(scripts, @"Traffic\TrafficEffects.cs");
        string sirens = Body(effects, "public static bool PrewarmSirens()");
        Check(sirens.Contains("GetSirenClip(SirenTone.Wail)") && sirens.Contains("GetSirenClip(SirenTone.Yelp)") && Body(effects, "private static AudioClip GetSirenClip(").Contains("_wailClip == null"),
            "prewarm: both sirens are made behind the loading screen, and a siren made already is used as it is");
        // The game's smoke is looked for only by the prewarm, behind a screen: an accident in play takes what was found, even nothing.
        string wreck = Source(scripts, @"Traffic\TrafficEffects.Wreck.cs");
        string smoke = Body(wreck, "public static GameObject CreateSmoke(");
        string findSmoke = Body(wreck, "private static void FindSmoke()");
        Check(smoke.Length > 0 && !smoke.Contains("SceneFinds.") && !smoke.Contains("FindSmoke(") && findSmoke.Contains("!SceneFinds.Hidden")
              && Count(effects + wreck, "FindSmoke(") == 2 && Body(effects, "public static bool Prewarm()").Contains("FindSmoke();"),
            "prewarm: the game's smoke is searched for only by the prewarm behind a screen, never by an accident in play");

        // Searches that come back after every door wait for a dark screen, never a plain pause (its menu shows the search as a
        // hitch): the hair finish's search of the whole scene, and the loading screen's pieces tried again out of doors.
        string finds = Source(scripts, @"Core\SceneFinds.cs");
        Check(finds.Contains("public static bool Dark => UI.Load.LoadScene.isLoading || UI.UiFader.isFading;"), "prewarm: a dark screen is the loading screen or the game's black screen, not a plain pause");
        Check(Body(finds, "private void OnUpdate()").Contains("!SceneFinds.Dark") && !Body(finds, "private void OnUpdate()").Contains("SceneFinds.Hidden"),
            "prewarm: a piece of the loading screen's work is tried again only behind a dark screen out of doors, never on a pause");
        string quality = Source(scripts, @"Character\CharacterQuality.cs");
        Check(quality.Contains("_othersPace.Due(SceneFinds.Dark, ") && !quality.Contains("_othersPace.Due(SceneFinds.Hidden"),
            "prewarm: the hair finish searches the whole scene only behind a dark screen, never on a pause");
        string metaUi = Source(scripts, @"Meta\MetaUi.cs");
        string gameFont = Source(scripts, @"Core\GameUi\GameFont.cs");
        Check(metaUi.Length > 0 && !metaUi.Contains("FindObjectsOfType") && Body(metaUi, "private void FindFont()").Contains("GameUi.GameFont.Asset")
              && gameFont.Contains("SceneFinds.Any(") && !gameFont.Contains("FindObjectOfType"),
            "prewarm: the mod's panels take the game's font found behind the loading screen, and a look for it in play goes through SceneFinds");

        string venues = Source(scripts, @"Leisure\LeisureVenues.cs");
        string find = Body(venues, "public static void FindLandmarks()");
        Check(find.Length > 0 && !find.Contains("SceneFinds.") && find.Contains("SceneLandmarks()"), "prewarm: the landmarks found on the street make no search of the scene of their own (the searches are SceneLandmarks')");
        Check(Body(venues, "public static bool PrewarmLandmarks()").Contains("SceneLandmarks()"), "prewarm: behind the loading screen the landmarks' six searches are made");

        string lamps = Source(scripts, @"Performance\Tweaks\StaleLampLights.cs");
        Check(lamps.Length > 0 && !lamps.Contains("SceneFinds.All") && !lamps.Contains("SceneFinds.Any") && !lamps.Contains("SceneFinds.InMemory") && lamps.Contains("PerfGame.Lamps("), "prewarm: the street lamps come from the game's own culling list, never a search of the scene");
        Check(Source(scripts, @"Performance\PerfGame.cs").Contains("GameContract.CullList.Field")
              && Source(scripts, @"Core\GameContract.cs").Contains("\"Culling.CullingGroupController\", \"_cullables\""),
            "prewarm: the game's culling list is read through its field, looked up once (a line of the game contract: Core/GameContract)");

        string sky = Source(scripts, @"Performance\Tweaks\SkyThrottle.cs");
        Check(Body(sky, "protected override void Prewarm()").Contains("SceneFinds.All<Volume>") && Body(sky, "private void Sweep()").Contains("kept ?"), "prewarm: the skies are searched for behind the loading screen and the switch-on uses what was found");

        string far = Source(scripts, @"Performance\Tweaks\FarDistricts.cs");
        Check(Body(far, "protected override void Prewarm()").Contains("Refresh()"), "prewarm: the far districts gather the scenery behind the loading screen");

        string tweak = Source(scripts, @"Performance\PerfTweak.cs");
        Check(Body(tweak, "internal void RunPrewarm(").Contains("ShouldBeActive"), "prewarm: only a tweak that is wanted looks at the scene behind the loading screen");

        string bikes = Source(scripts, @"Garage\BikePlaces.cs");
        Check(Body(bikes, "public void Find()").Contains("_stands ??"), "prewarm: the bike-share docks are placed from the stands found behind the loading screen");

        LoadPrewarms(scripts);

        string empire = Source(scripts, @"Camera\EmpireView.cs");
        string look = Body(empire, "private static Behaviour Find()");
        int listened = look.IndexOf("Listening()", StringComparison.Ordinal);
        int searched = look.IndexOf("SceneFinds.Any(", StringComparison.Ordinal);
        Check(!empire.Contains("FindObjectOfType") && listened >= 0 && searched > listened, "prewarm: Empire Motors' view is found in the game's own list of listeners first, and searched for only through SceneFinds after that");
    }

    // The work of the first minute after a load (as measured: a frame of 394 ms as the loading screen went,
    // with 1284 KB of the GPS's graph and 1188 KB of the bus network's in it, 2 MB of contact pictures, the performance tweaks
    // all switched in one frame of up to 19.7 ms, the materials listed in play and logged FAIL, the road data searched twice
    // per load): each held behind the loading screen, or spread, or kept, here.
    private static void LoadPrewarms(string scripts)
    {
        // The late pieces: run in the game's late "game loaded" call, its traffic system started, the loading screen still up.
        string finds = Source(scripts, @"Core\SceneFinds.cs");
        Check(Body(finds, "public static void Begin()").Contains("GlobalEvents.RegisterOnGameLoadedLateCallback(") && finds.Contains("Guard.Wrap(RunLate)"),
            "prewarm: the late pieces run in the game's own late call after its load, through the guard");
        Check(finds.Contains("ScenePrewarm.InLate") && Body(finds, "private static void RunLate()").Contains("InLate = true"),
            "prewarm: while the late pieces run, the screen counts as hidden (the loading screen is still up)");
        HandedLate(scripts, @"Navigation\NavDirector.cs", "PrewarmRoads", "the GPS's road graph, walking graph and both minimap pictures");
        HandedLate(scripts, @"Transport\TransitDirector.cs", "TransitMap.Prewarm", "the bus routes between the subway stations");
        Handed(scripts, @"Transport\TransitDirector.cs", "RoadPaint.Prewarm", "the game's road paint for the bus bays");

        // One road graph a city, built once the traffic system has marked its junctions, searched by the GPS and the buses.
        string graph = Source(scripts, @"Core\RoadGraph.cs");
        string shared = Body(graph, "public static RouteSearch Shared(");
        Check(shared.Contains("TrafficManager.IsInitialized") && shared.Contains("ReferenceEquals(points, _sharedFor)"), "roads: the city's one graph is built once, and only once the traffic system has started");
        Check(Body(Source(scripts, @"Navigation\NavGraph.cs"), "private bool Begin()").Contains("RoadGraph.Shared(") && !Source(scripts, @"Navigation\NavGraph.cs").Contains("RoadGraph.Build("),
            "roads: the GPS searches the city's one graph and builds none of its own");
        string transit = Source(scripts, @"Transport\TransitMap.cs");
        Check(Body(transit, "private static void BuildGraph()").Contains("RouteSearch.Over(roads, true)"), "roads: the buses search the city's one graph with edge costs of their own (the corridors)");
        Check(!transit.Contains("_graph.Find(") && Body(transit, "private static bool Seek(").Contains("_graph.GiveUp()"),
            "transit: every search of the routes is made a slice at a time, held to its steps as a search made whole");
        Check(Body(transit, "private static void Next(").Contains("WorkSlice.Of(StopsSliceMs"), "transit: the stops are placed a slice of about a millisecond a frame");
        Check(Body(Source(scripts, @"Transport\TransitDirector.cs"), "private bool DressNear(").Contains("WorkSlice.Of(PutsSliceMs"), "transit: the stops are put up as many a tick as fit in about a millisecond");
        Dressings(scripts);

        // The road data: one search a city, never in play.
        string roadData = Source(scripts, @"Core\RoadData.cs");
        Check(Body(roadData, "public static bool Prewarm()").Contains("if (_scene == null)"), "roads: the city's piece searches for the road data only if no piece before it has found it (one search a load, not two)");
        Check(Body(roadData, "public static CurrentSceneData Scene").Contains("SceneFinds.Hidden"), "roads: the road data is never searched for in play");

        // The game's road paint: found behind the loading screen through SceneFinds, never in play.
        string paint = Source(scripts, @"Transport\RoadPaint.cs");
        Check(!paint.Contains("Resources.FindObjectsOfTypeAll") && Body(paint, "private static Material FindTemplate()").Contains("SceneFinds.InMemory<Material>(")
              && Body(paint, "private static Material FindTemplate()").Contains("SceneFinds.Hidden"), "transit: the road paint is looked for through SceneFinds and only behind a screen");

        // The minimap's pictures: kept across loads by a print of what they are drawn from, drawn and sent behind the loading screen.
        string nav = Source(scripts, @"Navigation\NavGraph.cs");
        Check(Body(nav, "private void StartPicture()").Contains("_keptPrint == _job.Print") && Body(nav, "public void Dispose()").Length > 0 && !Body(nav, "public void Dispose()").Contains("Destroy("),
            "minimap: the road picture is kept across loads and used again for the same roads");
        string navBase = Source(scripts, @"Navigation\NavBase.cs");
        Check(Body(navBase, "public void Begin(").Contains("_keptPrint == PrintOf("), "minimap: the buildings and water are kept across loads and used again for the same city");
        string director = Body(Source(scripts, @"Navigation\NavDirector.cs"), "private bool PrewarmRoads()");
        Check(director.Contains("_graph.StepsNow()") && director.Contains("_graph.WaitForPicture(") && director.Contains("_base.WaitForDrawing("),
            "minimap: both pictures are drawn and sent to the graphics card behind the loading screen");

        // The contacts' pictures: no array per picture, logos kept as files, painted by a time budget.
        Check(!Body(Source(scripts, @"Phone\Contacts\ContactPictures.cs"), "internal static void Fill(").Contains("new Color32["), "contacts: a picture's colour is written into its own texture, no 256 KB array each");
        string painter = Source(scripts, @"Phone\Contacts\ContactPainter.cs");
        string painterUpdate = Body(painter, "private void OnUpdate()");
        Check(!painter.Contains("new Color32[") && painter.Contains("KeepLogo(p, ") && painterUpdate.Contains("_fetch = Ask(p);") && Body(painter, "private static Task<Fetch> Ask(").Contains("_io.ContinueWith(_ => Read(ask)") && !painterUpdate.Contains("File.")
              && painterUpdate.IndexOf("Use(f);", StringComparison.Ordinal) is int used && used > 0 && painterUpdate.IndexOf("return;", used, StringComparison.Ordinal) > used
              && painterUpdate.IndexOf("return;", used, StringComparison.Ordinal) < painterUpdate.IndexOf("Wanted.Count == 0", StringComparison.Ordinal),
            "contacts: no picture array, logos kept as files, every file read on the worker, one picture a frame");

        // The performance tweaks: one switched a frame, what they look at taken behind the loading screen, never searched in play.
        string sync = Body(Source(scripts, @"Performance\PerfDirector.cs"), "private void Sync(");
        int switched = sync.IndexOf("tweak.SetActive(", StringComparison.Ordinal);
        Check(switched >= 0 && sync.IndexOf("return;", switched, StringComparison.Ordinal) > switched, "performance: one tweak is switched a frame");
        string refraction = Source(scripts, @"Performance\Tweaks\RefractionOff.cs");
        Check(Body(refraction, "protected override void Prewarm()").Contains("SceneFinds.InMemory<Material>") && Body(refraction, "protected override void Tick(").Contains("SceneFinds.Hidden"),
            "performance: the materials are listed behind the loading screen, and again only behind a black screen");
        string lamps = Source(scripts, @"Performance\Tweaks\StaleLampLights.cs");
        Check(Body(lamps, "protected override void Prewarm()").Contains("PerfGame.Lamps(") && !Body(lamps, "protected override void Tick(").Contains("PerfGame.IsOn(")
              && !Body(lamps, "protected override void Restore()").Contains("PerfGame.IsOn("), "performance: the lamps are listed behind the loading screen, and held lamps are told lit by their lights, with no reflection");
        Check(Body(Source(scripts, @"Performance\Tweaks\SkyThrottle.cs"), "protected override void Tick(").Contains("SceneFinds.Hidden"), "performance: the skies of a scene that arrives in play are searched for only behind a black screen");
        string far = Source(scripts, @"Performance\Tweaks\FarDistricts.cs");
        Check(Body(far, "protected override void Tick(").Contains("_stale && SceneFinds.Hidden") && Body(far, "private void Survey()").Contains("WorkSlice.Of("),
              "performance: the far districts gather only behind a screen and sort by the clock");
        // RLO-PERF-022: for the city map only the map's layers come back, a square in the frame the map's camera first sees it
        // (before it culls), never the whole city as it opens; after it the very renderers lent are taken out again, a slice
        // every frame from the moment its camera is back.
        string farMap = Source(scripts, @"Performance\Tweaks\FarDistricts.Map.cs");
        string lendForMap = Body(farMap, "private void LendForMap(");
        string opened = Body(farMap, "private void OnMapToggled(");
        Check(Body(far, "private void OnCameraRendering(").Contains("LendInView(camera)") && Body(farMap, "private void LendInView(").Contains("GeometryUtility.TestPlanesAABB(")
              && lendForMap.Contains("Show(cell.Taken)") && !lendForMap.Contains("TakenOff") && !opened.Contains("Show(") && !opened.Contains("GiveBack")
              && Body(farMap, "private void HoldAgain(").Contains("slice.More()")
              && far.Contains("_again ? AgainEvery") && far.Contains("GlobalEvents.onCityMapClosed += _onMapClosed"),
            "performance: for the city map the far districts give back only the map's layers, a square as the map's camera first sees it, and take them out again a slice every frame once its camera is back");

        // The police's road map is built behind the loading screen (the streets' piece builds it), never on the first drive.
        Check(Body(Source(scripts, @"Traffic\Solid\SolidStreets.cs"), "public static bool Prewarm()").Contains("RoadMap.Ensure()"), "prewarm: the police's road map is built behind the loading screen");
    }

    // The bus stops' dressings: built behind the loading screen once the routes are laid (a spike of about 19 ms the first
    // time back on the street was one built and put up in the same tick of play), and in play never built and put together.
    private static void Dressings(string scripts)
    {
        string director = Source(scripts, @"Transport\TransitDirector.cs");
        HandedLate(scripts, @"Transport\TransitDirector.cs", "PrewarmDressings", "the bus stops' dressings");
        int routes = director.IndexOf(", TransitMap.Prewarm)", StringComparison.Ordinal), dressings = director.IndexOf(", PrewarmDressings)", StringComparison.Ordinal);
        Check(routes >= 0 && dressings > routes, "transit: the dressings are built after the routes, in the same late call (pieces run in the order handed over)");
        string prewarm = Body(director, "private bool PrewarmDressings()");
        int laid = prewarm.IndexOf("TransitMap.RoutesLaid", StringComparison.Ordinal), built = prewarm.IndexOf("new BusStopDressing(", StringComparison.Ordinal);
        Check(laid >= 0 && prewarm.Contains("PropLibrary.Settled") && built > laid && prewarm.Contains("TransitMap.PlacesLikely()") && prewarm.Contains(".Prepare(glyphs)")
              && prewarm.Contains("Math.Min(Dressings, "), "transit: once the routes are laid and the props loaded, a dressing for about every place is built and prepared behind the loading screen");
        string prepare = Body(Source(scripts, @"Transport\BusStopDressing.cs"), "public void Prepare(");
        Check(prepare.Contains("PaintReady()") && prepare.Contains("BuildStrips()") && prepare.Contains("ForceMeshUpdate(true)"),
            "transit: a dressing built behind the loading screen has its paint or strips made and its lettering drawn there");
        string dress = Body(director, "private bool DressNear(");
        int made = dress.IndexOf("new BusStopDressing(", StringComparison.Ordinal), put = dress.IndexOf("PutDressing(", StringComparison.Ordinal);
        int alone = made >= 0 ? dress.IndexOf("if (!hidden)", made, StringComparison.Ordinal) : -1;
        int back = alone >= 0 ? dress.IndexOf("return true;", alone, StringComparison.Ordinal) : -1;
        Check(made >= 0 && put > made && alone > made && back > alone && back < put && dress.Contains("FreeDressing(at, best, hidden)"),
            "transit: in play a dressing is never built and put up in the same tick; a new one waits for the next");
        string free = Body(director, "private int FreeDressing(");
        Check(free.Contains("empty >= 0 && (hidden || built == 0)") && free.Contains("!= _served"),
            "transit: in play a place is dressed with an idle dressing or the furthest one, and a new one is built only behind a screen or for a city that has none");
    }

    // The feature's file hands the named piece of work to ScenePrewarm as a late piece.
    private static void HandedLate(string scripts, string file, string work, string what)
    {
        bool handed = false;
        foreach (string line in Source(scripts, file).Split('\n'))
        {
            handed |= line.Contains("ScenePrewarm.AddLate(") && line.Contains(", " + work + ")");
        }
        Check(handed, "prewarm: " + what + " handed to ScenePrewarm as a late piece (" + file + ")");
    }

    // The feature's file hands the named piece of work to ScenePrewarm.
    private static void Handed(string scripts, string file, string work, string what)
    {
        bool handed = false;
        foreach (string line in Source(scripts, file).Split('\n'))
        {
            handed |= line.Contains("ScenePrewarm.Add(") && line.Contains(", " + work + ")");
        }
        Check(handed, "prewarm: " + what + " handed to ScenePrewarm (" + file + ")");
    }

    // A file of the mod with its comments taken out (BuildCheck's Code), or nothing if it is not there.
    private static string Source(string scripts, string file)
    {
        string path = Path.Combine(scripts, file);
        return File.Exists(path) ? Code(File.ReadAllText(path)) : string.Empty;
    }

    // The body of the member that starts with this text, braces matched; nothing if it is not there.
    private static string Body(string code, string start)
    {
        int at = code.IndexOf(start, StringComparison.Ordinal);
        if (at < 0)
        {
            return string.Empty;
        }
        int open = code.IndexOf('{', at);
        if (open < 0)
        {
            return string.Empty;
        }
        int depth = 0;
        for (int i = open; i < code.Length; i++)
        {
            if (code[i] == '{')
            {
                depth++;
            }
            else if (code[i] == '}' && --depth == 0)
            {
                return code.Substring(open, i - open + 1);
            }
        }
        return string.Empty;
    }
}
