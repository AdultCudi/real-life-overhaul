using System;
using System.Collections.Generic;
using BAModAPI;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The bridge the player is on kept in sight under this mod's close cameras. The game hides whatever meets a sphere
    /// round its camera, a bridge with every car on it; its own camera rides 15 to 35 metres up, the chase camera a metre or
    /// two above the deck, where the bridge and the car would vanish over and over. So while a close camera is looked
    /// through (CameraOrders.HoldBridges) the bridge under the player is shown as the game shows it unblocked and its
    /// view-blocking pieces switched off; every other bridge stays the game's, and each piece is switched on again after.
    /// The bridges are found behind the loading screen; per frame, two comparisons. Nothing is sent or saved.
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

        /// <summary>For ScenePrewarm: every bridge of the city and the view-blocking pieces under each. Always true.</summary>
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

        /// <summary>Once a frame with the frame's order: the bridge under the player held or given back, work only when it changes.</summary>
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
