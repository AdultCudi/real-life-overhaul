using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;

// Checks for the one rule of when this mod's camera stands aside for the game's own camera moments, and what it gives
// back (Camera/CameraYieldRules.cs, compiled here as it ships), and, over the sources, that every camera path of the mod
// goes through it (Camera/CameraYield.cs, ThirdPersonCamera.cs, ChaseCamera.cs, BridgeHold.cs).
//
// In the third-person camera the highway to the other city could not be driven: the camera went wrong. The game hides a
// bridge, and the car on it, from a camera that comes within a few metres of its deck (ViewBlockingObjectManager,
// BridgeController.SetCameraBlockMode, CarFeatures.UpdateBridgeHiddenState); the chase camera always did on the
// highway. The highway case is checked here as a rule (the bridges are held while the mod's close camera is looked
// through, and only then); that the bridge and the car stay in sight in the game is for the "Camera check (bridges)"
// lines of Player.log.
//
// What is not here: anything of the game. Whether a moment's camera really is one the game asks for at priority 1, and
// whether the figures given back are the camera's again, are for the "Camera: the game's own camera is now ..." and
// "Camera check (given back, ...)" lines of Player.log.
internal static partial class Program
{
    private static void CameraYields()
    {
        MomentsYield();
        ComesBack();
        Highway();
        EveryCombination();
        Handover();
        DistanceKept();
        Uncopy();
        YieldSources();
        YieldTiming();
    }

    // Ordinary play on foot, out of doors, on a bridge's road (the highway's), every option on.
    private static CameraSignals Walking()
    {
        return new CameraSignals
        {
            View = GameView.OnFoot, CameraOn = true, Indoors = true, CarCamera = true, MouseLook = true, Focused = true, OnBridge = true,
        };
    }

    private static CameraSignals Driving()
    {
        CameraSignals s = Walking();
        s.View = GameView.Driving;
        s.InCar = true;
        return s;
    }

    private static bool Nothing(in CameraOrders o)
    {
        return !o.OutdoorFigures && !o.IndoorFigures && !o.Chase && !o.Look && !o.ChaseLook && !o.HoldBridges;
    }

    private static bool SameOrders(in CameraOrders a, in CameraOrders b)
    {
        return a.OutdoorFigures == b.OutdoorFigures && a.IndoorFigures == b.IndoorFigures && a.Chase == b.Chase && a.Look == b.Look
               && a.ChaseLook == b.ChaseLook && a.HoldBridges == b.HoldBridges && a.Moment == b.Moment && a.OutdoorLinger == b.OutdoorLinger
               && a.IndoorLinger == b.IndoorLinger;
    }

    // Each signal of a game moment makes the camera stand aside.
    private static void MomentsYield()
    {
        CameraOrders walk = CameraYieldRules.Decide(Walking());
        Check(walk.OutdoorFigures && walk.IndoorFigures && walk.Look && walk.HoldBridges && !walk.Chase && !walk.Moment,
            "yield: walking out of doors, the figures are on, mouse look on and the bridges held");

        // A camera of the game's that is not one of ordinary play: placing an item, the interior designer, a building's
        // preview, the subway, a minigame, the free camera, the casino boat, a car's preview, a camera off its list.
        CameraSignals s = Walking();
        s.View = GameView.Moment;
        CameraOrders o = CameraYieldRules.Decide(s);
        Check(!o.OutdoorFigures && !o.IndoorFigures && !o.Look && !o.Chase && !o.ChaseLook && !o.HoldBridges && o.Moment && o.OutdoorLinger && o.IndoorLinger,
            "yield: a moment's camera: the figures to be given back once out of the picture, no mouse look, no chase, the bridges given back");
        s.InCar = true;
        Check(!CameraYieldRules.Decide(s).Chase, "yield: a moment's camera in a car (a car's preview, the free camera): no chase camera");
        s = Walking();
        s.View = GameView.Moment;
        s.Indoors = false;
        Check(!CameraYieldRules.Decide(s).IndoorLinger && CameraYieldRules.Decide(s).OutdoorLinger, "yield: with the indoor option off, nothing lingers on the indoor camera");

        // A screen that needs the pointer: the figures stay (the camera is still the game's on-foot one), the pointer is freed.
        s = Walking();
        s.Screen = true;
        o = CameraYieldRules.Decide(s);
        Check(o.OutdoorFigures && !o.Look && o.HoldBridges && !o.Moment, "yield: a screen up on foot: the pointer freed, the figures and the bridges kept");
        s = Driving();
        s.Screen = true;
        o = CameraYieldRules.Decide(s);
        Check(o.Chase && !o.ChaseLook, "yield: a screen up in the car: the chase camera stays, the pointer is freed");

        // The game holds the player in a conversation (a person, the bank, the casino's tables): no mouse look; in the car the chase camera keeps it.
        s = Walking();
        s.Talk = true;
        o = CameraYieldRules.Decide(s);
        Check(o.OutdoorFigures && !o.Look, "yield: a conversation of the game's on foot: no mouse look, its answers can be clicked");
        s = Driving();
        s.Talk = true;
        Check(CameraYieldRules.Decide(s).ChaseLook, "yield: a phone call of the game's while driving does not take the chase camera's mouse look");

        // The player dead: the pointer is the game's, nothing else changes by itself (the funeral's camera is a moment, held through to its cut).
        s = Walking();
        s.Dead = true;
        o = CameraYieldRules.Decide(s);
        Check(o.OutdoorFigures && !o.Look && !o.Moment && o.HoldBridges, "yield: the player dead, the game's on-foot camera still live: the figures and the bridge held through, no mouse look");
        s.View = GameView.Moment;
        o = CameraYieldRules.Decide(s);
        Check(o.Moment && o.OutdoorLinger && !o.OutdoorFigures, "yield: the funeral's own camera: a moment, the figures given back only once the on-foot camera has left the picture");
        s = Driving();
        s.Dead = true;
        o = CameraYieldRules.Decide(s);
        Check(o.Chase && !o.ChaseLook, "yield: dead at the wheel: the chase camera stays until the game's camera cuts away, the pointer freed");

        // Another mod's camera, the option off: nothing at all.
        s = Driving();
        s.OtherMod = true;
        Check(Nothing(CameraYieldRules.Decide(s)), "yield: another mod drives the camera: nothing of this mod's camera, in a car too");
        s = Driving();
        s.CameraOn = false;
        Check(Nothing(CameraYieldRules.Decide(s)), "yield: the third-person option off: nothing (the game's own camera, bridges as the game hides them)");

        // The map: the figures stay on the cameras it goes back to, no mouse look, no chase, the bridges as the game has them.
        s = Driving();
        s.View = GameView.Map;
        o = CameraYieldRules.Decide(s);
        Check(o.OutdoorFigures && !o.Look && !o.Chase && !o.HoldBridges && !o.Moment, "yield: the city map: no chase, no mouse look, the bridges given back, the figures kept for the way back");

        // The game's other driving cameras (looking back, inside a building or a car park): the game's.
        s = Driving();
        s.View = GameView.VehicleOther;
        o = CameraYieldRules.Decide(s);
        Check(!o.Chase && !o.HoldBridges && o.OutdoorFigures && !o.Moment, "yield: the game's look-back and indoor driving cameras: no chase camera, the bridges the game's");

        // Indoors.
        s = Walking();
        s.View = GameView.Indoor;
        o = CameraYieldRules.Decide(s);
        Check(o.IndoorFigures && o.Look && !o.HoldBridges, "yield: indoors with the indoor option on: the figures and mouse look, no bridges held");
        s.Indoors = false;
        o = CameraYieldRules.Decide(s);
        Check(!o.IndoorFigures && !o.Look && o.OutdoorFigures, "yield: indoors with the indoor option off: the game's own indoor camera, no mouse look");

        // The car's own options.
        s = Driving();
        s.CarCamera = false;
        o = CameraYieldRules.Decide(s);
        Check(!o.Chase && !o.HoldBridges, "yield: the chase camera's option off: the game's own driving camera, the bridges as the game hides them");
        s = Driving();
        s.CarMouse = true;
        Check(CameraYieldRules.Decide(s).Chase && !CameraYieldRules.Decide(s).ChaseLook, "yield: the car steered with the mouse: no mouse look in the car");
        s = Driving();
        s.InCar = false;
        Check(!CameraYieldRules.Decide(s).Chase, "yield: a scooter or a hand truck: no chase camera");
        s = Walking();
        s.CursorFreed = true;
        Check(!CameraYieldRules.Decide(s).Look, "yield: the pointer freed by the player: no mouse look");
        s.CursorFreed = false;
        s.Focused = false;
        Check(!CameraYieldRules.Decide(s).Look, "yield: the game's window without the focus: no mouse look");
    }

    // After a moment, everything comes back as it was before it.
    private static void ComesBack()
    {
        GameView[] views = { GameView.OnFoot, GameView.Indoor, GameView.Driving, GameView.VehicleOther, GameView.Map };
        foreach (GameView view in views)
        {
            CameraSignals s = view == GameView.Driving ? Driving() : Walking();
            s.View = view;
            CameraOrders before = CameraYieldRules.Decide(s);
            CameraSignals m = s;
            m.View = GameView.Moment;
            CameraOrders during = CameraYieldRules.Decide(m);
            CameraOrders after = CameraYieldRules.Decide(s);
            Check(during.Moment && !during.OutdoorFigures && SameOrders(before, after), "yield: back from a moment to " + view + ", the orders are those before it");
        }
        CameraSignals d = Walking();
        CameraOrders walk = CameraYieldRules.Decide(d);
        d.Dead = true;
        CameraYieldRules.Decide(d);
        d.Dead = false;
        Check(SameOrders(walk, CameraYieldRules.Decide(d)), "yield: back from the player's death to walking, everything as before");
        // A moment that ends while the camera never left the picture (the interior designer shut within its 0.3 s blend):
        // the game wrote its own distance back meanwhile; the first frame after is fresh, so it is not taken for a zoom.
        bool fresh = CameraYieldRules.Fresh(false, true);
        Check(fresh && Near(CameraYieldRules.Wanted(fresh, true, false, 6f, 4f, 13f, 4f, 1f, 20f), 4f),
            "yield: the designer shut within its blend: the game's 13 m written back is not a zoom, the camera stays at the player's 4 m");
        Check(!CameraYieldRules.Fresh(false, false) && CameraYieldRules.Fresh(true, false), "yield: an ordinary frame is not fresh, a new take is");
        d.Talk = true;
        CameraYieldRules.Decide(d);
        d.Talk = false;
        Check(SameOrders(walk, CameraYieldRules.Decide(d)), "yield: the conversation over: mouse look back");
    }

    // The highway to the other city: the bridge and the car on it stay in sight under the chase camera.
    private static void Highway()
    {
        CameraSignals s = Driving();
        Check(CameraYieldRules.Decide(s).HoldBridges, "highway: driving onto the bridge with the chase camera: the bridges are held out of the game's hiding");
        // The game's skip of the bridge fades the screen and moves the car; its driving camera stays the one asked for.
        Check(CameraYieldRules.Decide(s).Chase && CameraYieldRules.Decide(s).HoldBridges, "highway: through the skip's black screen the chase camera stays and the bridges stay held");
        s.Screen = true;   // the skip's time machine, a confirm box
        Check(CameraYieldRules.Decide(s).HoldBridges, "highway: a screen up on the bridge does not give the bridges back while the chase camera is looked through");
        s = Driving();
        s.View = GameView.Map;
        Check(!CameraYieldRules.Decide(s).HoldBridges, "highway: the map opened on the bridge: the bridges given back (the game shows them itself for the map)");
        s.View = GameView.Driving;
        Check(CameraYieldRules.Decide(s).HoldBridges, "highway: the map closed: held again");
        s.View = GameView.VehicleOther;
        Check(!CameraYieldRules.Decide(s).HoldBridges, "highway: reversing on the bridge (the game's own look-back camera, far above): the bridges the game's");
        s = Walking();
        Check(CameraYieldRules.Decide(s).HoldBridges, "highway: out of the car on the bridge, the on-foot camera close behind: still held");
        s = Driving();
        s.CameraOn = false;
        Check(!CameraYieldRules.Decide(s).HoldBridges, "highway: with this mod's camera off, the game's camera 35 m up hides the bridges as it always did");
        s = Driving();
        s.OtherMod = true;
        Check(!CameraYieldRules.Decide(s).HoldBridges, "highway: another mod's camera: the bridges the game's");

        // Only the bridge under the player: an elevated road or a viaduct overhead stays the game's to hide, so it never
        // covers the car or holds the chase camera inside it (the level14 junction and HighwayAvenue, the Park Ave viaduct).
        s = Driving();
        s.OnBridge = false;
        Check(!CameraYieldRules.Decide(s).HoldBridges, "highway: driving under an elevated road with the chase camera pitched up: nothing held, the game hides the deck overhead");
        s = Walking();
        s.OnBridge = false;
        Check(!CameraYieldRules.Decide(s).HoldBridges, "highway: walking under a viaduct: nothing held");

        // The game's look-back camera blends a second away from the deck: the bridge stays held while the chase camera is in the picture.
        s = Driving();
        s.View = GameView.VehicleOther;
        s.CloseInPicture = true;
        Check(CameraYieldRules.Decide(s).HoldBridges, "highway: looking back on the bridge, the chase camera still in the blend: the bridge stays held (no flicker)");
        s.CloseInPicture = false;
        Check(!CameraYieldRules.Decide(s).HoldBridges, "highway: ...given back once the chase camera has left the picture");
        s.CloseInPicture = true;
        s.OnBridge = false;
        Check(!CameraYieldRules.Decide(s).HoldBridges, "highway: a blend off the bridge holds nothing");
        s.OnBridge = true;
        s.CameraOn = false;
        Check(!CameraYieldRules.Decide(s).HoldBridges, "highway: this mod's camera switched off: nothing lingers");
    }

    // Every combination of the signals keeps the rule's promises.
    private static void EveryCombination()
    {
        int bad = 0, combos = 0;
        GameView[] views = { GameView.Moment, GameView.OnFoot, GameView.Indoor, GameView.Driving, GameView.VehicleOther, GameView.Map };
        foreach (GameView view in views)
        {
            for (int bits = 0; bits < 1 << 14; bits++)
            {
                var s = new CameraSignals
                {
                    View = view,
                    Screen = (bits & 1) != 0, Talk = (bits & 2) != 0, Dead = (bits & 4) != 0, OtherMod = (bits & 8) != 0, InCar = (bits & 16) != 0,
                    CameraOn = (bits & 32) != 0, Indoors = (bits & 64) != 0, CarCamera = (bits & 128) != 0, MouseLook = (bits & 256) != 0,
                    CursorFreed = (bits & 512) != 0, Focused = (bits & 1024) != 0, CarMouse = (bits & 2048) != 0,
                    OnBridge = (bits & 4096) != 0, CloseInPicture = (bits & 8192) != 0,
                };
                CameraOrders o = CameraYieldRules.Decide(s);
                CameraOrders again = CameraYieldRules.Decide(s);
                combos++;
                bool off = !s.CameraOn || s.OtherMod;
                bool ok = SameOrders(o, again)
                          && (!off || (Nothing(o) && !o.Moment && !o.OutdoorLinger && !o.IndoorLinger))
                          && (!o.Chase || (view == GameView.Driving && s.InCar && s.CarCamera))
                          && (!o.ChaseLook || (o.Chase && !s.Screen && !s.CarMouse && !s.CursorFreed && s.Focused && s.MouseLook && !s.Dead))
                          && (!o.Look || ((view == GameView.OnFoot || view == GameView.Indoor) && !s.Screen && !s.Talk && !s.CursorFreed && s.Focused && s.MouseLook && !s.Dead))
                          && (!o.Look || !o.Chase)
                          && (!o.HoldBridges || (s.OnBridge && (o.Chase || (view == GameView.OnFoot && o.OutdoorFigures) || s.CloseInPicture)))
                          && (!(s.OnBridge && (o.Chase || (view == GameView.OnFoot && o.OutdoorFigures))) || o.HoldBridges)
                          && (view != GameView.Moment || (!o.OutdoorFigures && !o.IndoorFigures && !o.Look && !o.Chase && (!o.HoldBridges || s.CloseInPicture) && (off || o.Moment)))
                          && (!o.IndoorFigures || s.Indoors)
                          && (o.Moment == (view == GameView.Moment && !off));
                bad += ok ? 0 : 1;
            }
        }
        Check(bad == 0, "yield: every one of " + combos + " combinations of the signals keeps the rule (nothing while off, the chase only for the game's driving camera, mouse look only on the camera carrying the figures, only the bridge under the player and only under the close camera or its blend out, a moment's camera never steered), the same twice: " + bad + " broke it");
    }

    // The game's figures given back exactly as they were taken.
    private static void Handover()
    {
        var game = new CameraFigures { OffsetX = 0f, OffsetY = 0.74f, OffsetZ = -0.68f, Near = 15f, Far = 35f, Distance = 35f, Speed = 2f, Fov = 49f };
        var mine = new CameraFigures { OffsetX = 0f, OffsetY = 0.2f, OffsetZ = -0.98f, Near = 2.5f, Far = 35f, Distance = 5f, Speed = 2f, Fov = 60f };
        var h = new FigureHandover();
        Check(!h.Taken && !h.Give(out _), "handover: a camera never taken has nothing to give back");
        Check(h.Take(game) && h.Taken, "handover: the first take writes the game's figures down");
        Check(!h.Take(mine) && h.Game.Same(game), "handover: a second take (this mod's own figures on the camera) writes nothing over the game's");
        Check(h.Give(out CameraFigures back) && back.Same(game) && !h.Taken, "handover: given back, the figures are exactly those taken");
        Check(!h.Give(out _), "handover: given back once only");

        // The game changed its own figures while it had the camera (a zoom of its own, the interior designer's copied distance):
        // the next take writes those down, and they are what comes back.
        CameraFigures zoomed = game;
        zoomed.Distance = 22f;
        Check(h.Take(zoomed) && h.Give(out back) && back.Same(zoomed) && !back.Same(game), "handover: what the game changed while it had the camera is what is given back after the next take");

        // A run of moments: each give equals its take.
        bool all = true;
        for (int i = 0; i < 100; i++)
        {
            CameraFigures f = game;
            f.Distance = 10f + i * 0.25f;
            f.Fov = 40f + i % 7;
            all &= h.Take(f) && !h.Take(mine) && h.Give(out back) && back.Same(f);
        }
        Check(all, "handover: a hundred moments, each camera given back exactly as it was taken");
        CameraFigures other = game;
        other.Speed = 2.5f;
        Check(!game.Same(other) && game.Same(game), "handover: figures differing in one number are not the same");
    }

    // The distance the player chose survives the game's moments.
    private static void DistanceKept()
    {
        const float min = 1f, max = 20f;
        Check(Near(CameraYieldRules.Wanted(false, false, false, 6f, 0f, 13f, 0f, min, max), 6f), "distance: the first take: the option's distance");
        Check(Near(CameraYieldRules.Wanted(false, true, false, 6f, 6f, 8f, 6f, min, max), 8f), "distance: the game's scroll wheel moved the camera 2 m: the player's own zoom");
        // The interior designer: this mod wrote 4 m; the game copied the placement camera's 30 m back in while it had the camera.
        Check(Near(CameraYieldRules.Wanted(true, true, false, 6f, 4f, 30f, 4f, min, max), 4f), "distance: back from the interior designer, the 30 m the game wrote is not taken for a zoom: the player's 4 m");
        Check(Near(CameraYieldRules.Wanted(true, true, true, 9f, 4f, 30f, 4f, min, max), 9f), "distance: the option changed meanwhile: the option's distance");
        Check(Near(CameraYieldRules.Wanted(false, true, false, 6f, 19f, 40f, 19f, min, max), max) && Near(CameraYieldRules.Wanted(false, true, false, 6f, 2f, -30f, 2f, min, max), min),
            "distance: always within the nearest and furthest");
        Check(Near(CameraYieldRules.Wanted(true, false, false, 50f, 4f, 30f, 4f, min, max), max), "distance: an option past the furthest is held at the furthest");
    }

    // A distance the placement camera copied from this mod's own is given the game's own.
    private static void Uncopy()
    {
        Check(Near(CameraYieldRules.Uncopied(4f, 4f, 13f), 13f), "placement: it copied this mod's 4 m: it has the game's own 13 m");
        Check(Near(CameraYieldRules.Uncopied(4.0005f, 4f, 13f), 13f), "placement: a copy within a millimetre is a copy");
        Check(Near(CameraYieldRules.Uncopied(25f, 4f, 13f), 25f), "placement: a distance that is not this mod's (the player's own zoom there) is left alone");
        Check(Near(CameraYieldRules.Uncopied(13f, 13f, 13f), 13f), "placement: the game's own distance stays the game's own");
    }

    // Over the sources: every camera path goes through the one rule.
    private static void YieldSources()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string yield = Source(scripts, @"Camera\CameraYield.cs");
        string third = Source(scripts, @"Camera\ThirdPersonCamera.cs");
        string chase = Source(scripts, @"Camera\ChaseCamera.cs");
        string hold = Source(scripts, @"Camera\BridgeHold.cs");
        Check(yield.Length > 0 && third.Length > 0 && chase.Length > 0 && hold.Length > 0, "yield sources: the camera's files are at " + scripts);
        Check(yield.Contains("CameraYieldRules.Decide(signals)") && yield.Contains("DialogController.current != null") && yield.Contains("GameView.Moment"),
            "yield sources: CameraYield makes the orders by the rule, from the game's camera mode and its conversations");
        Check(Body(third, "private void OnUpdate()").Contains("CameraYield.Orders") && Body(chase, "private void OnLateUpdate()").Contains("CameraYield.Orders"),
            "yield sources: the on-foot camera and the chase camera both follow the frame's orders");
        Check(!third.Contains(".Priority") && !Body(chase, "private void OnLateUpdate()").Contains(".Priority"),
            "yield sources: neither camera reads a game camera's priority itself (the game's camera mode is CameraYield's alone)");
        Check(!third.Contains("Settings.CameraEnabled &&") && !Body(chase, "private void OnLateUpdate()").Contains("Settings.CameraEnabled"),
            "yield sources: neither camera decides from the options itself");
        Check(Body(third, "private void OnUpdate()").Contains("BridgeHold.Hold(orders.HoldBridges)") && Body(chase, "private void OnLateUpdate()").Contains("BridgeHold.Hold(orders.HoldBridges)"),
            "yield sources: the bridges are held by the order, from both cameras");
        Check(Body(third, "private void OnDestroy()").Contains("GiveBack()") && Body(third, "private void GiveBack()").Contains("BridgeHold.Release()") && Body(chase, "private void OnDestroy()").Contains("BridgeHold.Release()"),
            "yield sources: the bridges are given back when the city unloads");
        Handed(scripts, @"Camera\ThirdPersonCamera.cs", "BridgeHold.Prewarm", "the city's bridges for the camera");
        string find = Body(hold, "public static bool Prewarm()");
        Check(find.Contains("SceneFinds.All<BridgeController>(") && hold.IndexOf("SceneFinds.", StringComparison.Ordinal) == hold.IndexOf("SceneFinds.All<BridgeController>(", StringComparison.Ordinal),
            "yield sources: the bridges are searched for only behind the loading screen, never in play");
        string take = Body(hold, "private static void Take(int bridge)");
        Check(take.Contains("SetCameraBlockMode(false)") && take.Contains("piece.enabled = false") && Body(hold, "public static void Release()").Contains("piece.enabled = true")
              && Body(hold, "public static void Hold(bool want)").Contains("if (bridge == _held)") && Body(hold, "public static void Hold(bool want)").Contains("int bridge = want ? _under : -1;") && take.Contains("i < Start[bridge + 1]"),
            "yield sources: a bridge is shown and switched off when held, switched on again when given back, and nothing is done while the hold does not change");
        string below = Body(hold, "private static int Below(GameManager gm)");
        Check(Body(hold, "public static bool OnBridge(GameManager gm)").Contains("_next = now + Every") && below.Contains("GetBridgeBelow()") && below.Contains("IsRoadBelow(at)")
              && yield.Contains("OnBridge = BridgeHold.OnBridge(gm)"),
            "yield sources: only the bridge under the player is held, asked as the game asks it, four times a second (the car's bridge below it, or the road under the player)");
        Check(yield.Contains("CloseInPicture = ChaseCamera.InPicture || ThirdPersonCamera.InPicture") && chase.Contains("CinemachineCore.Instance.IsLive(_vcam)"),
            "yield sources: the bridge is held through the blend out of the close camera");
        string observe = Body(yield, "public static void Observe()");
        Check(observe.Contains("ViewOf(gm)") && !observe.Contains("View ==") && !observe.Contains("Orders"),
            "yield sources: the other-mod count is made before the frame's orders, so they see it in the same frame");
        string features = Source(scripts, @"Meta\FeatureList.cs");
        int camera = features.IndexOf("Features.Add(\"camera\"", StringComparison.Ordinal);
        int next = camera >= 0 ? features.IndexOf("Features.Add(", camera + 10, StringComparison.Ordinal) : -1;
        string cleanup = camera >= 0 && next > camera ? features.Substring(camera, next - camera) : string.Empty;
        Check(cleanup.Contains("ThirdPersonCamera.StandAside()") && cleanup.Contains("ChaseCamera.LetGo()")
              && Body(third, "public static void StandAside()").Contains("BridgeHold.Release()") && Body(third, "private void GiveBack()").Contains("Apply(_indoor, false, null)")
              && Body(chase, "public static void LetGo()").Contains("BridgeHold.Release()"),
            "yield sources: the camera feature's clean-up (run by the guard when it switches the feature off) gives the figures, the pointer and the bridge back");
        Check(Body(third, "if (FrameGuard.Off)").Contains("GiveBack();") && Body(chase, "if (FrameGuard.Off)").Contains("LetGo();"),
            "yield sources: a camera found switched off by the guard gives everything back before it stops");
    }

    // The rule a frame: no allocation, a few nanoseconds.
    private static void YieldTiming()
    {
        long allocated = Decisions(out double ms);
        Check(allocated == 0, "yield: a million decisions allocate nothing (" + allocated + " bytes)");
        Check(ms < 200, "yield: a million decisions take under 200 ms (" + ms.ToString("F1") + " ms)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long Decisions(out double ms)
    {
        CameraSignals s = Driving();
        var watch = new Stopwatch();
        int held = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        watch.Start();
        for (int i = 0; i < 1000000; i++)
        {
            s.View = (GameView)(i % 6);
            s.Screen = (i & 8) != 0;
            CameraOrders o = CameraYieldRules.Decide(s);
            held += o.HoldBridges ? 1 : 0;
        }
        watch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        ms = watch.Elapsed.TotalMilliseconds;
        return held >= 0 ? allocated : -1;
    }
}
