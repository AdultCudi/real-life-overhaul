using System;
using System.Collections.Generic;
using BAModAPI;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The bridge the player is on kept in sight under this mod's close cameras. The game hides whatever its camera's sphere
    /// meets (ViewBlockingObjectManager: 3.75 metres around the camera in a car), and for a bridge that hides its renderers
    /// and every car on it. The game's camera rides 15 to 35 metres up, so it never meets the deck; the chase camera rides a
    /// metre or two above it, and on the highway the bridge and the player's car would vanish and come back over and over.
    ///
    /// So while a close camera is looked through out of doors, and through its blend out (CameraOrders.HoldBridges), the
    /// bridge under the player is shown as the game shows it unblocked (SetCameraBlockMode(false), as its screenshots do) and
    /// its view-blocking pieces switched off, which the game's block asks first; nothing else of a bridge reads that switch.
    /// Every other bridge stays the game's, so a viaduct overhead is hidden as ever. Off the bridge, or when the game's
    /// camera is back, each piece this switched off is switched on again, and none other.
    ///
    /// The bridge under the player is asked as the game asks it, four times a second. The bridges are found once a city
    /// behind the loading screen. Per frame: one time comparison and one of a number; the pieces are walked only in the
    /// frame the held bridge changes. Nothing is sent or saved: in Going Public it is this player's view only.
    /// </summary>
    public static class BridgeHold
    {
        private const string Search = "camera: the city's bridges being found (a search of the whole scene, once a city)";
        private const float Every = 0.25f;   // seconds between asks, as the game's BridgeTriggerController.StatusUpdateInterval

        private static IModLogger Logger => ThirdPersonCamera.Logger;

        private static readonly List<BridgeController> Bridges = new List<BridgeController>(32);
        private static readonly List<int> Start = new List<int>(33);   // the first of each bridge's pieces, and one past the last
        private static readonly List<ViewBlockingEntity> Pieces = new List<ViewBlockingEntity>(128);
        private static readonly List<bool> Mine = new List<bool>(128);   // switched off by this, to be switched on again
        private static readonly List<ViewBlockingEntity> Under = new List<ViewBlockingEntity>(16);
        private static int _held = -1;    // the bridge held, or -1
        private static int _under = -1;   // the bridge under the player at the last ask, or -1
        private static float _next;       // unscaled seconds
        private static int _taken;        // pieces switched off by the last take

        /// <summary>The bridge held (its place in the city's list), or -1.</summary>
        public static int Held => _held;

        /// <summary>
        /// For ScenePrewarm, behind the loading screen: every bridge of the city and the view-blocking pieces under each.
        /// Always true.
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

        /// <summary>Whether the player, or the car they drive, is on a bridge's road; asked again four times a second.</summary>
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
        /// Once a frame with the frame's <see cref="CameraOrders.HoldBridges"/>: the bridge under the player kept in sight, or
        /// given back. Does anything only in the frame the held bridge changes.
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

        // Shown as the game shows them unblocked, and switched off so that the game's block passes them by.
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
                        piece.SetCameraBlockMode(false);   // its renderers, objects, layer and the cars on it shown again
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

        /// <summary>Switches every piece this switched off on again. Safe at any time (unloading, the option off, a fault).</summary>
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
                    gone++;
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

        // The held bridge's pieces the game still shows hidden, for the check line.
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
