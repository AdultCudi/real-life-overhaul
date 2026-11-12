using System;
using RealLifeOverhaul.Map25;
using RealLifeOverhaul.Nav;

// The city map's logic since the owner's decision of 5 Nov 2026: the full city map is the game's own city, drawn by the game's own camera at all times, panned or still, and the
// mod puts nothing of its own under the game's pins. What stays is the map button's tilt of the game's camera, the route line, the minimap's black-picture latch and the map
// check after the map shut. The rules are game-free and run here; the Unity side is held to them by reading its source for each rule, as the rest of the mod's checks do where
// only the game can run the code.
internal static partial class Program
{
    private static void Map25LogicChecks()
    {
        Map25LatchChecks();
        Map25SourceChecks();
    }

    private static void Map25LatchChecks()
    {
        NavMapLatch.Forget();
        bool clear = !NavMapLatch.Drew;
        bool firstWord = NavMapLatch.Mark(), secondWord = NavMapLatch.Mark();
        bool kept = NavMapLatch.Drew;
        NavMapLatch.Forget();
        Check(clear && firstWord && !secondWord && kept && !NavMapLatch.Drew, "minimap: a picture that came out black is latched for the session and said once");
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
        string camera = ModSource(@"Navigation\NavMapCamera.cs"), city = ModSource(@"Navigation\NavCityMap.cs"), director = ModSource(@"Navigation\NavDirector.cs"), scene = ModSource(@"Navigation\NavDirector.Scene.cs");
        string mapEvents = ModSource(@"Navigation\NavDirector.Map.cs"), hud = ModSource(@"Navigation\NavHud.cs"), hudScene = ModSource(@"Navigation\NavHud.Scene.cs"), drawn = ModSource(@"Ui\Map\Map25Scene.cs");
        bool read = camera.Length > 1000 && city.Length > 1000 && director.Length > 1000 && scene.Length > 500 && mapEvents.Length > 500 && hud.Length > 1000 && hudScene.Length > 1000 && drawn.Length > 1000;
        Check(read, "city map: the sources the logic is held to are read");

        // 1. the full map never swaps the camera's culling mask and never makes a render target: the game's own city is what the player sees, panned or still
        string[] cityMapFiles = { camera, city, mapEvents, scene };
        bool noMask = true, noTarget = true;
        foreach (string source in cityMapFiles)
        {
            noMask &= !source.Contains("cullingMask") && !source.Contains("GiveCameraBack");
            noTarget &= !source.Contains("RenderTexture") && !source.Contains("RawImage") && !source.Contains("CommandBuffer") && !source.Contains("ExecuteCommandBuffer");
        }
        string[] gone = { @"Navigation\NavMap25.cs", @"Navigation\NavMapNames.cs", @"Navigation\NavMapLights.cs", @"Navigation\NavMapFollow.cs", @"Ui\Map\Map25Names.cs", @"Ui\Map\Map25Zones.cs" };
        bool filesGone = true;
        foreach (string file in gone)
        {
            filesGone &= ModSource(file).Length == 0;
        }
        Check(noMask && noTarget && filesGone && !ModSource(@"Core\Settings.cs").Contains("Map25Id") && !director.Contains("NavMap25"),
            "city map: the full map never swaps the camera's culling mask and never makes a render target, picture or command buffer (the game's own city under its own camera at all times), and the 2.5D picture, its marker, its street names, its tint pass and its option are gone");

        // 2. the route line of the mod's GPS is drawn on the game's map whenever there is a route, and sits under the game's pins
        Check(!city.Contains("PictureStands") && city.Contains("SetAsFirstSibling();") && !scene.Contains("ArrangeMap") && !mapEvents.Contains("ArrangeMap"),
            "city map: the mod's route line is always drawn on the game's map and stands under every one of the game's pins, set when the object is made");

        // 3. the button's tilt moves the camera only when the button asks, and the game's tilt is given back as the map shuts
        Check(Map25Body(camera, "private void Tilt(").Contains("!_pitched && want == Map25View.TiltPitch") && Map25Body(camera, "public void Open(bool open)").Contains("RestorePitch();")
              && Map25Body(camera, "private void OnDisable()").Contains("RestorePitch();") && Map25Body(camera, "private void RestorePitch()").Contains("Tilt(map, Map25View.TiltPitch, false)")
              && Map25Body(camera, "private void LateUpdate()").Contains("Guard.Fail(") && !camera.Contains("Camera.main") && !camera.Contains("GetMainCamera"),
            "city map: the tilt button's camera work does nothing at the game's own tilt, never touches the main camera, gives the game's 47 degree tilt back as the map shuts or the object goes, and stands down on a fault");

        // 4. the map check after the map shut
        string look = Map25Body(scene, "private int CameraLook(StringBuilder line)");
        Check(director.Contains("MapParts.Add(\"route on the map\", MapLook)") && Map25Body(mapEvents, "private int MapLook(StringBuilder line)").Contains("CameraLook(line)") && look.Contains("_mapCamera.Pitched") && !look.Contains("new "),
            "city map: the map check after the map shut (RLO-PERF-022) asks whether the camera is still held at a tilt of the mod's, and allocates nothing to say so");

        // 5. the map's own event whatever the minimap and the GPS are (the tilt button needs it)
        string slow = Map25Body(director, "private void Slow(float now)");
        int bothOff = slow.IndexOf("if (!wantMap && !wantGps)", StringComparison.Ordinal);
        int bothOffEnd = bothOff < 0 ? -1 : slow.IndexOf("return;", bothOff, StringComparison.Ordinal);
        string keep = Map25Body(director, "private void KeepMapEvent()");
        Check(bothOff >= 0 && bothOffEnd > bothOff && slow.Substring(bothOff, bothOffEnd - bothOff).Contains("KeepMapEvent();") && keep.Contains("EventHold.Keep(ref GlobalEvents.onCityMapToggle")
              && keep.Contains("OnMap(CityMap.IsOpen)") && Map25Body(scene, "private void OpenCamera(bool open)").Contains("_mapCamera.Open(open);"),
            "city map: with the minimap and the GPS both off the director still holds the map's own event (and looks at the map if it was missed), and the map opening and shutting reaches the camera's tilt");
        Check(Map25Body(ModSource(@"Navigation\NavDirector.Base.cs"), "private void BaseRedraw()").Contains("_base.Redraw()") && slow.Contains("BaseRedraw();"),
            "minimap: a base made stale by the mod's own buildings is drawn again on the slow tick whatever is on");

        // 6. the minimap's black picture is latched, and not left up until the director's next slow tick
        string frameBody = Map25Body(hudScene, "public void SceneFrame(");
        int blankAt = frameBody.IndexOf("Map25Scene.Blank(", StringComparison.Ordinal), offAt = frameBody.IndexOf("Scene(_sceneFrom, false, _sceneTilted);", StringComparison.Ordinal);
        int shownAt = frameBody.IndexOf("_scenePicture.enabled = true;", StringComparison.Ordinal);
        Check(hudScene.Contains("SceneBroken => NavMapLatch.Drew") && hudScene.Contains("NavMapLatch.Mark()") && blankAt >= 0 && offAt > blankAt && shownAt > offAt,
            "minimap: a picture that comes out black is latched, and switched off in the frame it is found so the flat pictures come back at once");

        // 7. the cheap things
        Check(Map25Body(scene, "private void SceneStep()").Contains("LoadScene.isLoading") && Map25Body(scene, "private void SceneStep()").Contains("LongFrame") && Map25Count(hud, "!_sceneOn") >= 3
              && !drawn.Contains("Fixture"),
            "minimap: the scene's meshes are made a slice a frame but not behind the loading screen and not in a frame over 50 ms, and no flat picture is switched on over the 2.5D one");
    }
}
