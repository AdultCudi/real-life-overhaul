using System;
using RealLifeOverhaul.Map25;
using RealLifeOverhaul.Nav;

// The 2.5D city map's logic, round 2 of the job (the reviewer's eleven must-fix): what the picture reads from the game and when (a still, open map
// does nothing), the black picture kept for the session, where the picture and the route stand among the game's marker layer, what it gives back
// when the map shuts or it stands down however it does (the camera's mask and tilt, its texture, every tint), the game's zones, the map's own event
// whatever the minimap and the GPS are, and the cheap things (no step behind the loading screen or in a long frame, no flat picture over the 2.5D
// one). The rules are game-free and run here; the Unity side is held to them by reading its source for each rule, as the rest of the mod's checks do
// where only the game can run the code (what only the game can confirm is in docs/features/navigation.md, "Not verified").
internal static partial class Program
{
    private static void Map25LogicChecks()
    {
        Map25LightsChecks();
        Map25RedrawChecks();
        Map25LatchChecks();
        Map25LayerChecks();
        Map25SourceChecks();
    }

    // The decision to draw the picture again, and the game's zones in it: a zone the district view tints or a neighbourhood line the filters toggle moves no camera, so a
    // still camera is drawn again for it, and for nothing else.
    private static void Map25RedrawChecks()
    {
        var view = new float[16];
        var projection = new float[16];
        for (int i = 0; i < 16; i++)
        {
            view[i] = 1f + i * 0.25f;
            projection[i] = 2f - i * 0.125f;
        }
        float[] lastView = (float[])view.Clone(), lastProjection = (float[])projection.Clone();
        bool still = !NavMapMoves.Needed(false, 0, view, lastView, projection, lastProjection);
        bool changed = NavMapMoves.Needed(true, 0, view, lastView, projection, lastProjection), sending = NavMapMoves.Needed(false, 3, view, lastView, projection, lastProjection);
        view[5] += 0.001f;
        bool turned = NavMapMoves.Needed(false, 0, view, lastView, projection, lastProjection);
        view[5] = lastView[5];
        projection[0] += 0.001f;
        bool zoomed = NavMapMoves.Needed(false, 0, view, lastView, projection, lastProjection);
        projection[0] = lastProjection[0];
        Check(still && changed && sending && turned && zoomed,
            "2.5D city map: a still camera with nothing changed and no mesh to send is not drawn again; a changed picture, a mesh to send or one number of either matrix is");

        var book = new Map25ZoneBook();
        var corners = new float[Map25Zones.MostZones * 8];
        var colours = new Map25Colour[Map25Zones.MostZones];
        var alphas = new float[Map25Zones.MostZones];
        for (int z = 0; z < 2; z++)
        {
            Map25Zones.Corners(z * 100f, 0f, z * 100f + 80f, 60f, corners, z);
            colours[z] = new Map25Colour(0.3f + z * 0.2f, 0.4f, 0.5f);
            alphas[z] = 0.47f;
        }
        // each step is a change of the game's zones and what a still camera does about it: (take the zones, then decide with nothing else changed)
        int drawn = 0, wrong = 0;
        void Step(int count, bool change)
        {
            bool took = book.Take(corners, colours, alphas, count);
            bool draw = NavMapMoves.Needed(took, 0, view, lastView, projection, lastProjection);
            drawn += draw ? 1 : 0;
            wrong += took == change && draw == change ? 0 : 1;
        }
        Step(0, true);                                    // the first look of a book is a change whatever it finds (the scene may hold an earlier picture's zones)
        Step(0, false);                                   // no zone, none held
        Step(2, true);                                    // two show: drawn
        Step(2, false);                                   // the same two: not
        colours[1] = new Map25Colour(0.9f, 0.2f, 0.2f);
        Step(2, true);                                    // the district view tints one: drawn
        Step(2, false);
        alphas[0] = 0.30f;
        Step(2, true);                                    // an opacity
        Step(2, false);
        corners[8] += 5f;
        Step(2, true);                                    // a rectangle
        Step(2, false);
        Step(1, true);                                    // a neighbourhood line is toggled off in the filters: one goes
        Step(1, false);
        Step(0, true);                                    // all gone
        Step(0, false);
        book.Forget();                                    // the map opens again
        Step(0, true);
        Step(0, false);
        Check(wrong == 0 && drawn == 8 && book.Count == 0,
            "2.5D city map: a zone that shows, changes its colour or opacity, moves or goes is a reason to draw on a still camera, and the same zones are none (" + drawn + " draws in 15 looks)");
    }

    private static void Map25LightsChecks()
    {
        var lights = new NavMapLights();
        lights.Reset(883);
        lights.Poke();   // the map opens
        int frames = 0, covered = 0;
        var seen = new int[883];
        while (lights.Step(false, 11, 22, out int from, out int to) && frames < 100)
        {
            frames++;
            for (int i = from; i < to; i++)
            {
                seen[i]++;
            }
        }
        foreach (int s in seen)
        {
            covered += s == 1 ? 1 : 0;
        }
        Check(frames == 7 && covered == 883 && lights.Passes == 1 && !lights.Sweeping, "2.5D city map: the map opening is one pass over the 883 buildings, 128 a frame (" + frames + " frames), each read once");
        // a still, open map: nothing to read, nothing allocated, however long (after a warm-up: the first thousands of calls of a cold process are the runtime's own
        // work on this thread, a few kilobytes that the rule does not make)
        int warm = 0, reads = 0;
        for (int f = 0; f < 20000; f++)
        {
            warm += lights.Step(false, 11, 22, out _, out _) ? 1 : 0;
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int f = 0; f < 20000; f++)
        {
            reads += lights.Step(false, 11, 22, out _, out _) ? 1 : 0;
        }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(warm == 0 && reads == 0 && lights.Passes == 1 && bytes == 0, "2.5D city map: a still, open map reads no building in 20,000 frames and allocates nothing (" + reads + " reads, " + bytes + " bytes)");
        // the pointer comes to another building, and a card opens: one pass each
        int hover = 0;
        while (lights.Step(false, 12, 22, out _, out _) && hover < 100)
        {
            hover++;
        }
        int card = 0;
        while (lights.Step(false, 12, 33, out _, out _) && card < 100)
        {
            card++;
        }
        Check(hover == 7 && card == 7 && lights.Passes == 3, "2.5D city map: the pointer coming to another building is one pass and another's card opening is one (" + hover + " and " + card + " frames)");
        // the filters have work for forty frames: pass after pass, then one more for the last of it, then nothing
        int working = 0;
        for (int f = 0; f < 40; f++)
        {
            working += lights.Step(true, 12, 33, out _, out _) ? 1 : 0;
        }
        int after = 0;
        while (lights.Step(false, 12, 33, out _, out _) && after < 100)
        {
            after++;
        }
        bool quiet = !lights.Step(false, 12, 33, out _, out _);
        Check(working == 40 && after >= 1 && after <= 14 && quiet, "2.5D city map: while the game's filters have work a pass is under way every frame, once they have none one more finishes it and the map is still again (" + after + " frames after)");
        // a toggle whose whole work fits in the frame it began in (the game works its queue inside its own update, up to 2 ms a frame): the queue is empty again when the
        // picture looks, so only the count of buildings the game worked on says it was there
        int worked = 0, workSeen = NavMapWork.Items;
        Action<int> game = n => worked += n;
        Action<int> wrapped = NavMapWork.Wrap(game);
        for (int n = 0; n < 883; n++)
        {
            wrapped(1);
        }
        var byQueue = new NavMapLights();
        byQueue.Reset(883);
        bool queueSees = byQueue.Step(false, 12, 33, out _, out _);   // what the picture saw before the count: an empty queue, so nothing
        bool took = NavMapWork.Took(ref workSeen);
        bool tookOnce = !NavMapWork.Took(ref workSeen);
        int passesBefore = lights.Passes;
        bool begins = lights.Step(took, 12, 33, out int firstFrom, out _) && firstFrom == 0 && lights.Passes == passesBefore + 1;
        int single = 1;
        while (lights.Step(NavMapWork.Took(ref workSeen), 12, 33, out _, out _) && single < 100)
        {
            single++;
        }
        Check(worked == 883 && took && tookOnce && !queueSees && begins && single >= 7 && single <= 15 && !lights.Sweeping && !lights.Step(false, 12, 33, out _, out _),
            "2.5D city map: a toggle whose work is done in the frame it began in (the game's queue empty again when the picture looks) still starts a pass, by the count of buildings the game worked on, and the game's own step runs every time ("
            + worked + " buildings worked, " + single + " frames of passes)");
        // a mod that changes a look without telling the game starts a pass; one asked for while a pass is under way follows it
        lights.Poke();
        lights.Step(false, 12, 33, out _, out _);
        lights.Poke();
        int both = 1;
        while (lights.Step(false, 12, 33, out _, out _) && both < 100)
        {
            both++;
        }
        Check(both == 14, "2.5D city map: a pass asked for while one is under way follows it (" + both + " frames for the two)");
        // the book of what is lit: set once, taken away once each, in order, and nothing left
        lights.Reset(883);
        bool first = lights.Set(3, true), again = lights.Set(3, true), second = lights.Set(500, true), never = lights.Set(9, false);
        int cursor = 0, a = lights.TakeNext(ref cursor), b = lights.TakeNext(ref cursor), c = lights.TakeNext(ref cursor);
        Check(first && !again && second && !never && a == 3 && b == 500 && c == -1 && lights.Lit == 0 && !lights.IsLit(3),
            "2.5D city map: every tint the picture took is in a book, and taking them all out as the map shuts finds each once and leaves none (" + a + ", " + b + ", " + c + ")");
        lights.Set(7, true);
        lights.Reset(883);
        Check(lights.Lit == 0 && !lights.IsLit(7) && !lights.Sweeping, "2.5D city map: a new city or a new opening starts the book empty");
    }

    private static void Map25LatchChecks()
    {
        NavMapLatch.Forget();
        bool clear = !NavMapLatch.Drew(NavMapLatch.Full) && !NavMapLatch.Drew(NavMapLatch.Minimap);
        bool firstWord = NavMapLatch.Mark(NavMapLatch.Full), secondWord = NavMapLatch.Mark(NavMapLatch.Full);
        bool kept = NavMapLatch.Drew(NavMapLatch.Full) && !NavMapLatch.Drew(NavMapLatch.Minimap);
        NavMapLatch.Forget();
        Check(clear && firstWord && !secondWord && kept, "2.5D city map: a picture that came out black is latched for the session and said once, and the other picture is not held to it");
    }

    private static void Map25LayerChecks()
    {
        NavMapLayers.Indices(true, true, out int p1, out int r1);
        NavMapLayers.Indices(false, true, out int p2, out int r2);
        NavMapLayers.Indices(true, false, out int p3, out int r3);
        NavMapLayers.Indices(false, false, out int p4, out int r4);
        Check(p1 == 0 && r1 == 1 && p2 == -1 && r2 == 0 && p3 == 0 && r3 == -1 && p4 == -1 && r4 == -1,
            "2.5D city map: the picture is first among the marker layer's children and the mod's route line next, whichever was made last, and the route is first when there is no picture");
    }

    // The text of a method or property: from its signature to the brace that closes it.
    private static string Map25Body(string source, string signature)
    {
        int at = source.IndexOf(signature, StringComparison.Ordinal);
        if (at < 0)
        {
            return string.Empty;
        }
        int open = source.IndexOf('{', at);
        int semicolon = source.IndexOf(';', at);
        if (open < 0 || (semicolon >= 0 && semicolon < open))
        {
            return semicolon < 0 ? string.Empty : source.Substring(at, semicolon - at);   // a property with an arrow body
        }
        int depth = 0;
        for (int i = open; i < source.Length; i++)
        {
            depth += source[i] == '{' ? 1 : source[i] == '}' ? -1 : 0;
            if (depth == 0)
            {
                return source.Substring(at, i - at + 1);
            }
        }
        return string.Empty;
    }

    private static void Map25SourceChecks()
    {
        string map = ModSource(@"Navigation\NavMap25.cs"), city = ModSource(@"Navigation\NavCityMap.cs"), director = ModSource(@"Navigation\NavDirector.cs"), scene = ModSource(@"Navigation\NavDirector.Scene.cs");
        string mapEvents = ModSource(@"Navigation\NavDirector.Map.cs"), hud = ModSource(@"Navigation\NavHud.cs"), hudScene = ModSource(@"Navigation\NavHud.Scene.cs"), drawn = ModSource(@"Ui\Map\Map25Scene.cs");
        string districts = ModSource(@"Districts\DistrictGame.cs");
        bool read = map.Length > 1000 && city.Length > 1000 && director.Length > 1000 && scene.Length > 1000 && mapEvents.Length > 500 && hud.Length > 1000 && hudScene.Length > 1000 && drawn.Length > 1000 && districts.Length > 1000;
        Check(read, "2.5D city map: the sources the logic is held to are read");

        // 1. the game's zones are drawn into the picture, from the colour the zone has, and a change of one is noticed
        Check(map.Contains("ReadZones()") && Map25Body(map, "private void ReadZones()").Contains("CityMapNeighborhoodZones.Zones") && map.Contains("\"_UnlitColor\"") && map.Contains("SetZones(_zoneCorners")
              && drawn.Contains("public void SetZones(") && districts.Split(new[] { "NavBridges.MapLooks" }, StringSplitOptions.None).Length - 1 >= 2 && director.Contains("NavBridges.MapLooks = PokeMap;"),
            "2.5D city map: the game's neighbourhood zones that show are read (their box, matrix and _UnlitColor) and drawn into the picture, and the district view tells the picture when it tints or clears one");

        // 1b. the zones are read before the picture decides whether to draw: a change of them on a still camera is drawn
        string show = Map25Body(map, "private void Show(CityMap map)");
        string readZones = Map25Body(map, "private void ReadZones()");
        int readAt = show.IndexOf("ReadZones();", StringComparison.Ordinal), decideAt = show.IndexOf("NavMapMoves.Needed(", StringComparison.Ordinal);
        Check(readAt >= 0 && decideAt > readAt && show.Contains("NavMapMoves.Needed(_dirty,") && readZones.Contains("_zonesDrawn.Take(") && readZones.Contains("_dirty = true;") && !show.Contains("moved = _dirty"),
            "2.5D city map: the game's zones are read before the picture decides whether to draw again, and a change of them is a reason to draw (so a zone the district view tints, or a neighbourhood line a filter toggles, shows on a still camera)");

        // 2. one owner of the order of the marker layer's children
        string arrange = Map25Body(scene, "private void ArrangeMap()");
        string toggled = Map25Body(mapEvents, "private void MapToggled(bool open)");
        Check(!map.Contains("SetAsFirstSibling") && !city.Contains("SetAsFirstSibling") && arrange.Contains("NavMapLayers.Indices(") && Map25Count(arrange, "SetSiblingIndex(") == 2
              && Map25Body(scene, "private void OpenPicture(bool open)").Contains("ArrangeMap();") && toggled.Contains("ArrangeMap();") && toggled.LastIndexOf("ArrangeMap();", StringComparison.Ordinal) > toggled.LastIndexOf("_map.Open(open);", StringComparison.Ordinal)
              && Map25Body(scene, "private void SceneWanted(bool wantMinimap)").Contains("ArrangeMap();"),
            "2.5D city map: neither object sets its own place among the marker layer's children; the director does, after either is made and after the map opens, so the route is over the picture on the first open of a session");

        // 3. a black picture is kept for the session
        Check(map.Contains("NavMapLatch.Drew(NavMapLatch.Full)") && map.Contains("NavMapLatch.Mark(NavMapLatch.Full)") && !Map25Body(map, "public void Open(bool open)").Contains("_failed = false")
              && Map25Body(scene, "private bool FullWanted").Contains("NavMapLatch.Drew(") && hudScene.Contains("SceneBroken => NavMapLatch.Drew(NavMapLatch.Minimap)") && hudScene.Contains("NavMapLatch.Mark(NavMapLatch.Minimap)"),
            "2.5D city map: a black picture is latched (said once) and neither the next opening nor the director brings the picture back; the minimap's is latched the same");

        // 3b. the minimap's black picture is not left up until the director's next slow tick
        string frameBody = Map25Body(hudScene, "public void SceneFrame(");
        int blankAt = frameBody.IndexOf("Map25Scene.Blank(", StringComparison.Ordinal), offAt = frameBody.IndexOf("Scene(_sceneFrom, false, _sceneTilted);", StringComparison.Ordinal);
        int shownAt = frameBody.IndexOf("_scenePicture.enabled = true;", StringComparison.Ordinal);
        Check(blankAt >= 0 && offAt > blankAt && shownAt > offAt,
            "2.5D city map: a minimap picture that comes out black is switched off in the frame it is found and the flat pictures come back at once, not at the director's next slow tick");

        // 4. the map's own event whatever the minimap and the GPS are
        string slow = Map25Body(director, "private void Slow(float now)");
        int bothOff = slow.IndexOf("if (!wantMap && !wantGps)", StringComparison.Ordinal);
        int bothOffEnd = bothOff < 0 ? -1 : slow.IndexOf("return;", bothOff, StringComparison.Ordinal);
        string keep = Map25Body(director, "private void KeepMapEvent()");
        Check(bothOff >= 0 && bothOffEnd > bothOff && slow.Substring(bothOff, bothOffEnd - bothOff).Contains("KeepMapEvent();") && keep.Contains("EventHold.Keep(ref GlobalEvents.onCityMapToggle")
              && keep.Contains("OnMap(CityMap.IsOpen)") && Map25Body(scene, "private void OpenPicture(bool open)").Contains("Player();"),
            "2.5D city map: with the minimap and the GPS both off the director still holds the map's own event (and looks at the map if it was missed) and the player's place is read as the map opens");
        Check(Map25Body(ModSource(@"Navigation\NavDirector.Base.cs"), "private void BaseRedraw()").Contains("_base.Redraw()") && slow.Contains("BaseRedraw();"),
            "2.5D city map: a base made stale by the mod's own buildings is drawn again on the slow tick whatever is on, so the map alone has them too");

        // 5. every tint the picture took is taken out when it stops standing; a tint writes the full map's set only
        Check(Map25Body(map, "public void Open(bool open)").Contains("ClearTints();") && Map25Body(map, "private void ClearTints()").Contains("TakeNext(") && Map25Body(map, "private void OnDestroy()").Contains("ClearTints();")
              && Map25Body(drawn, "public bool Retint(").Contains("_sets[0].Retint(") && !Map25Body(drawn, "public bool Retint(").Contains("for ("),
            "2.5D city map: the tints the picture took are taken out as the map shuts and when the picture goes, and a tint writes the full map's blocks only, never the minimap's");

        // 6. the highlights are read when something changed them, not every frame
        string lightsBody = Map25Body(map, "private void Lights()");
        Check(Map25Count(map, "gameObject.layer") == 1 && lightsBody.Contains("gameObject.layer") && lightsBody.Contains("_lights.Step(") && lightsBody.Contains("CityMapFilters.Work.HasPendingWork") && lightsBody.Contains("MouseController.currentTargetEntity")
              && lightsBody.Contains("buildingResume") && !map.Contains("Slice") && !map.Contains("Mirror()") && !map.Contains("Harmony"),
            "2.5D city map: the buildings' layers are read only inside the pass the game's own signals start (the filters' work, the pointer, the open card, the map opening), no sweep every frame and no patch of the game's code");

        // 6b. the game's look at its buildings is counted, so work done in the frame it began in is seen too
        string follow = ModSource(@"Navigation\NavMapFollow.cs"), contract = ModSource(@"Core\GameContract.cs");
        Check(lightsBody.Contains("NavMapWork.Took(") && lightsBody.Contains("NavMapWork.Following") && lightsBody.Contains("Input.GetMouseButtonUp(0)") && Map25Body(map, "public static NavMap25 Make(").Contains("NavMapFollow.Install(")
              && follow.Contains("GameContract.MapWork.Off") && follow.Contains("WorkQueueStep.Wrap<") && follow.Contains("NavMapWork.Wrap") && ModSource(@"Core\WorkQueueStep.cs").Contains("\"_updateAction\"")
              && follow.Contains("NavMapWork.Following = true;")
              && contract.Contains("ContractBook.Part(\"map_work\"") && contract.Contains("On(ClaimedRows, MapWork)"),
            "2.5D city map: the game's look at its buildings is counted by a step wrapped round the game's own (the contract names the piece), so work done in the frame it began in starts a pass too, and a game whose queue is not as expected falls back to a pass at every mouse release");

        // 7. every way of standing down gives the game's map camera its tilt back
        string release = Map25Body(map, "private void Release()");
        Check(release.Contains("RestorePitch();") && release.Contains("GiveCameraBack(") && Map25Body(map, "public void Open(bool open)").Contains("Release();") && Map25Body(map, "private void OnDestroy()").Contains("Release();")
              && Map25Body(map, "private void OnDisable()").Contains("Release();") && Map25Body(map, "private void RestorePitch()").Contains("Tilt(map, Map25View.TiltPitch, false)")
              && Map25Body(map, "private void LateUpdate()").Contains("_failed = true;") && Map25Body(map, "private void LateUpdate()").Contains("Release();"),
            "2.5D city map: the picture's every way of standing down (the option off, another mod's route, a fault, a black picture, the map shut, the object gone) lets go of its texture, gives the game's camera its mask and its 47 degree tilt back");

        // 8. the map check after the map shut
        string held = Map25Body(map, "public int Held(StringBuilder line)");
        Check(director.Contains("MapParts.Add(\"route on the map\", MapLook)") && Map25Body(mapEvents, "private int MapLook(StringBuilder line)").Contains("Map25Look(line)") && held.Contains("texture alive") && held.Contains("camera masked") && held.Contains("camera tilted") && held.Contains("tints kept") && held.Contains("object on")
              && !held.Contains("new ") && Map25Body(scene, "private int Map25Look(StringBuilder line)").Contains("_map25.Held(line)"),
            "2.5D city map: the map check after the map shut (RLO-PERF-022) asks the picture whether it still holds its object, texture, the camera's mask or tilt, or a tint, and allocates nothing to say so");

        // the picture lies where the game's own city does, and the route has one owner at a time
        Check(map.Contains("_view.Set(_viewMatrix, _projection, pitch, _scene.Y0)") && city.Contains("NavMap25.PictureStands") && map.Contains("PictureStands = true;") && Map25Count(map, "PictureStands = false;") >= 2,
            "2.5D city map: the game's view is lowered by the scene's ground (exact, not near y = 0), and while the picture stands the mod's route line of the city map is left to it");

        // the cheap things
        Check(Map25Body(scene, "private void SceneStep()").Contains("LoadScene.isLoading") && Map25Body(scene, "private void SceneStep()").Contains("LongFrame") && Map25Count(hud, "!_sceneOn") >= 3
              && !ModSource(@"Ui\Map\Map25Scene.cs").Contains("Fixture"),
            "2.5D city map: the scene's meshes are made a slice a frame but not behind the loading screen and not in a frame over 50 ms, and no flat picture of the minimap is switched on over the 2.5D one");
    }
}
