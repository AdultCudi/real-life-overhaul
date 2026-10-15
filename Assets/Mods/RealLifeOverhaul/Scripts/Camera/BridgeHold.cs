using System;
using System.Collections.Generic;
using BAModAPI;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The highway out of the city, and any bridge the player is on, kept in sight under this mod's close cameras.
    ///
    /// The game hides whatever stands in the way of its camera (ViewBlockingObjectManager, on the main camera): every
    /// other frame it casts a sphere from the camera towards the player and looks for anything within 3.75 metres of the
    /// camera in a car (2.5 on foot), and whatever of its view-blocking kind it meets goes into "camera block mode"
    /// (ViewBlockingEntity.CameraBlock). For a bridge (Streets.BridgeController.SetCameraBlockMode) that means: its
    /// renderers hidden, its objects to disable switched off, its layer made Roads, and every car on its road hidden with
    /// its lights off (CarFeatures.UpdateBridgeHiddenState: a car on a hidden bridge is hidden too). It comes back 0.4 s
    /// after the last block (ViewBlockingEntity.DoUpdate, timeToCameraBlock). The game's own camera rides 15 to 35 metres
    /// above the player, so the sphere at the camera never touches the deck the player drives on. The chase camera rides a
    /// metre or two above it, so on the highway the deck was always within the sphere: the bridge and the player's own car
    /// vanished and came back over and over, the view "bugged out", and the drive to the other city could not be made.
    ///
    /// So while this mod's close camera is the one looked through out of doors, and through the blend out of it
    /// (<see cref="CameraYieldRules"/>: HoldBridges), THE BRIDGE THE PLAYER IS ON, and every view-blocking piece under it,
    /// is shown as the game shows it when nothing blocks (SetCameraBlockMode(false), the fade at 1: what the game itself
    /// does for its screenshots, CityBuildingController) and its component switched off, which is the switch the game's own
    /// block asks first (ViewBlockingEntity.CameraBlock: "if (enabled)"); nothing else of a bridge reads it (its road test
    /// and its cars do not). Every other bridge stays the game's: an elevated road or a viaduct over the player is hidden
    /// by the game as ever, so it never covers the car or holds the camera inside it. Off that bridge, or when the game's
    /// camera is back (a moment of the game's, the map, the option off, another mod's camera, a fault, the city
    /// unloading), each piece this switched off is switched on again, and none other. The game's skip of the bridge, its
    /// trigger, its traffic and its saved state are not touched.
    ///
    /// Which bridge the player is on is asked as the game asks it, four times a second (BridgeTriggerController.
    /// UpdateStatusPeriodically, 0.25 s): in a car, the car's own bridge under it (CarFeatures.GetBridgeBelow, a ray
    /// against the few road colliders of the bridges it is in); on foot, each bridge's road under the player
    /// (BridgeController.IsRoadBelow over the city's bridges, the same ray). The bridges are found once a city, behind the
    /// loading screen (a search of the whole scene, ScenePrewarm). Per frame: one time comparison and one of a number
    /// (<see cref="OnBridge"/>, <see cref="Hold"/>); the pieces are walked only in the frame the bridge held changes.
    /// Nothing is sent or saved: in a Going Public session it is this player's view only.
    /// </summary>
    public static class BridgeHold
    {
        private const string Search = "camera: the city's bridges being found (a search of the whole scene, once a city)";
        private const float Every = 0.25f;   // seconds between asks of the bridge under the player: the game's own (BridgeTriggerController.StatusUpdateInterval)

        private static IModLogger Logger => ThirdPersonCamera.Logger;   // (the camera's own log: set where the mod starts)

        private static readonly List<BridgeController> Bridges = new List<BridgeController>(32);
        private static readonly List<int> Start = new List<int>(33);   // the first of each bridge's pieces, and one past the last
        private static readonly List<ViewBlockingEntity> Pieces = new List<ViewBlockingEntity>(128);
        private static readonly List<bool> Mine = new List<bool>(128);   // switched off by this, to be switched on again
        private static readonly List<ViewBlockingEntity> Under = new List<ViewBlockingEntity>(16);
        private static int _held = -1;    // the bridge held, or -1
        private static int _under = -1;   // the bridge under the player at the last ask, or -1
        private static float _next;       // when to ask again (unscaled seconds)
        private static int _taken;        // pieces switched off by the last take

        /// <summary>The bridge held (its place in the city's list), or -1.</summary>
        public static int Held => _held;

        /// <summary>
        /// For ScenePrewarm, behind the loading screen: every bridge of the city and the view-blocking pieces under each,
        /// found and kept. Always true (done; a city with no bridge has nothing to keep).
        /// </summary>
        public static bool Prewarm()
        {
            Release();
            Bridges.Clear();
            Start.Clear();
            Pieces.Clear();
            Mine.Clear();
            _under = -1;
            _next = 0f;
            BridgeController[] bridges = SceneFinds.All<BridgeController>(Search, FindObjectsInactive.Include);
            for (int i = 0; i < bridges.Length; i++)
            {
                if (bridges[i] == null)
                {
                    continue;
                }
                Bridges.Add(bridges[i]);
                Start.Add(Pieces.Count);
                Under.Clear();
                bridges[i].GetComponentsInChildren(true, Under);   // the bridge itself first
                for (int j = 0; j < Under.Count; j++)
                {
                    if (Under[j] != null)
                    {
                        Pieces.Add(Under[j]);
                        Mine.Add(false);
                    }
                }
            }
            Start.Add(Pieces.Count);
            Under.Clear();
            return true;
        }

        /// <summary>
        /// Whether the player, or the car the player drives, is on a bridge's road: asked again four times a second, as
        /// the game asks it; the answer in between is the last one.
        /// </summary>
        public static bool OnBridge(GameManager gm)
        {
            float now = Time.unscaledTime;
            if (now >= _next)
            {
                _next = now + Every;
                _under = Below(gm);
            }
            return _under >= 0;
        }

        // The bridge under the player or the car, as the game finds it.
        private static int Below(GameManager gm)
        {
            if (Bridges.Count == 0 || gm == null || gm.playerController == null)
            {
                return -1;
            }
            VehicleController vehicle = gm.selectedVehicle;
            if (vehicle != null && vehicle.CarFeatures != null)
            {
                BridgeController below = vehicle.CarFeatures.GetBridgeBelow();
                return below != null ? Bridges.IndexOf(below) : -1;
            }
            Vector3 at = gm.playerController.transform.position;
            for (int i = 0; i < Bridges.Count; i++)
            {
                if (Bridges[i] != null && Bridges[i].IsRoadBelow(at))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Once a frame with the frame's order (<see cref="CameraOrders.HoldBridges"/>): the bridge under the player kept in
        /// sight, or given back as it was. Does anything only in the frame the bridge held changes.
        /// </summary>
        public static void Hold(bool want)
        {
            int bridge = want ? _under : -1;
            if (bridge == _held)
            {
                return;
            }
            Release();
            if (bridge >= 0)
            {
                Take(bridge);
            }
        }

        // The bridge's pieces shown as the game shows them unblocked, and switched off so that the game's block passes them by.
        private static void Take(int bridge)
        {
            _held = bridge;
            int taken = 0;
            int shown = 0;
            for (int i = Start[bridge]; i < Start[bridge + 1]; i++)
            {
                ViewBlockingEntity piece = Pieces[i];
                if (piece == null || !piece.enabled)
                {
                    continue;   // gone with its scene, or switched off by the game itself: left as it is
                }
                try
                {
                    if (piece.IsInCameraBlockMode)
                    {
                        piece.SetCameraBlockMode(false);   // its renderers, its objects, its layer and the cars on it shown again
                        shown++;
                    }
                    piece.SetFadeSate(1f);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);   // that piece left to the game's hiding; the others are held
                    continue;
                }
                piece.enabled = false;
                Mine[i] = true;
                taken++;
            }
            _taken = taken;
            if (Meta.Dev.Verbose)
            {
                int blocked = Blocked(bridge);
                Logger?.Info("Camera check (bridges): this mod's close camera is looked through on '" + (Bridges[bridge] != null ? Bridges[bridge].name : "?") + "': "
                             + taken + " piece(s) of that bridge kept out of the game's camera-block hiding, " + shown + " of them hidden at that moment and shown again; the "
                             + (Bridges.Count - 1) + " other bridge(s) left to the game: " + (blocked == 0 ? "PASS" : "FAIL (" + blocked + " still hidden)") + ".");
            }
        }

        /// <summary>Every piece this switched off switched on again, as the game had it. Safe at any time (unloading, the option off, a fault).</summary>
        public static void Release()
        {
            if (_held < 0)
            {
                return;
            }
            int bridge = _held;
            _held = -1;
            int given = 0;
            int gone = 0;
            int end = bridge + 1 < Start.Count ? Start[bridge + 1] : 0;
            for (int i = Start[bridge]; i < end; i++)
            {
                if (!Mine[i])
                {
                    continue;
                }
                Mine[i] = false;
                ViewBlockingEntity piece = Pieces[i];
                if (piece == null)
                {
                    gone++;   // gone with its scene: nothing to give back
                    continue;
                }
                piece.enabled = true;
                piece.timeSinceLastCameraBlock = -1f;   // as the game leaves it after its screenshots: no block pending
                given++;
            }
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Camera check (bridges): off the bridge, or the game's camera is back: " + given + " piece(s) switched on again for the game's camera-block hiding, "
                             + gone + " gone with their scene, of " + _taken + " switched off: " + (given + gone == _taken ? "PASS" : "FAIL") + ".");
            }
            _taken = 0;
        }

        // The held bridge's pieces that the game still shows hidden (for the check line).
        private static int Blocked(int bridge)
        {
            int n = 0;
            for (int i = Start[bridge]; i < Start[bridge + 1]; i++)
            {
                if (Mine[i] && Pieces[i] != null && Pieces[i].IsInCameraBlockMode)
                {
                    n++;
                }
            }
            return n;
        }
    }
}
