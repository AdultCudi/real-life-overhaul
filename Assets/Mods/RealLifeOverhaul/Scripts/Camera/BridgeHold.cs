using System;
using System.Collections.Generic;
using BAModAPI;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The highway out of the city, and every other bridge, kept in sight under this mod's close cameras.
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
    /// So while this mod's close camera is the one looked through out of doors (<see cref="CameraYieldRules"/>:
    /// HoldBridges), every bridge and every view-blocking piece under it is shown as the game shows it when nothing blocks
    /// (SetCameraBlockMode(false), the fade at 1: what the game itself does for its screenshots, CityBuildingController)
    /// and its component switched off, which is the switch the game's own block asks first (ViewBlockingEntity.CameraBlock:
    /// "if (enabled)"); nothing else of a bridge reads it (its road test and its cars do not). When the game's camera is
    /// back (a moment of the game's, the map, the option off, another mod's camera, the city unloading), each piece this
    /// switched off is switched on again, and none other. The game's skip of the bridge, its trigger, its traffic and its
    /// saved state are not touched.
    ///
    /// The bridges are found once a city, behind the loading screen (a search of the whole scene, ScenePrewarm). Per frame:
    /// one comparison of a flag (<see cref="Hold"/>); the pieces are walked only in the frame the hold changes. Nothing is
    /// sent or saved: in a Going Public session it is this player's view only.
    /// </summary>
    public static class BridgeHold
    {
        private const string Search = "camera: the city's bridges being found (a search of the whole scene, once a city)";

        private static IModLogger Logger => ThirdPersonCamera.Logger;   // (the camera's own log: set where the mod starts)

        private static readonly List<ViewBlockingEntity> Pieces = new List<ViewBlockingEntity>(64);
        private static readonly List<bool> Mine = new List<bool>(64);   // switched off by this, to be switched on again
        private static readonly List<ViewBlockingEntity> Under = new List<ViewBlockingEntity>(16);
        private static bool _held;
        private static int _bridges;
        private static int _taken;   // pieces switched off by the last take

        /// <summary>True while the bridges are kept in sight.</summary>
        public static bool Held => _held;

        /// <summary>
        /// For ScenePrewarm, behind the loading screen: every bridge of the city and the view-blocking pieces under each,
        /// found and kept. Always true (done; a city with no bridge has nothing to keep).
        /// </summary>
        public static bool Prewarm()
        {
            Release();
            Pieces.Clear();
            Mine.Clear();
            BridgeController[] bridges = SceneFinds.All<BridgeController>(Search, FindObjectsInactive.Include);
            _bridges = bridges.Length;
            for (int i = 0; i < bridges.Length; i++)
            {
                if (bridges[i] == null)
                {
                    continue;
                }
                Under.Clear();
                bridges[i].GetComponentsInChildren(true, Under);   // the bridge itself first
                for (int j = 0; j < Under.Count; j++)
                {
                    if (Under[j] != null && !Pieces.Contains(Under[j]))
                    {
                        Pieces.Add(Under[j]);
                        Mine.Add(false);
                    }
                }
            }
            Under.Clear();
            return true;
        }

        /// <summary>
        /// Once a frame with the frame's order (<see cref="CameraOrders.HoldBridges"/>): the bridges kept in sight, or
        /// given back as they were. Does anything only in the frame the order changes.
        /// </summary>
        public static void Hold(bool want)
        {
            if (want == _held)
            {
                return;
            }
            if (want)
            {
                Take();
            }
            else
            {
                Release();
            }
        }

        // Every piece shown as the game shows it unblocked, and switched off so that the game's block passes it by.
        private static void Take()
        {
            _held = true;
            int taken = 0;
            int shown = 0;
            for (int i = 0; i < Pieces.Count; i++)
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
                Logger?.Info("Camera check (bridges): this mod's close camera is looked through out of doors: " + taken + " piece(s) of " + _bridges
                             + " bridge(s) kept out of the game's camera-block hiding, " + shown + " of them hidden at that moment and shown again: "
                             + (Blocked() == 0 ? "PASS" : "FAIL (" + Blocked() + " still hidden)") + ".");
            }
        }

        /// <summary>Every piece this switched off switched on again, as the game had it. Safe at any time (unloading, the option off, a fault).</summary>
        public static void Release()
        {
            if (!_held)
            {
                return;
            }
            _held = false;
            int given = 0;
            int gone = 0;
            for (int i = 0; i < Pieces.Count; i++)
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
                Logger?.Info("Camera check (bridges): the game's camera is back: " + given + " piece(s) switched on again for the game's camera-block hiding, "
                             + gone + " gone with their scene, of " + _taken + " switched off: " + (given + gone == _taken ? "PASS" : "FAIL") + ".");
            }
            _taken = 0;
        }

        // Pieces held that the game still shows hidden (for the check line).
        private static int Blocked()
        {
            int n = 0;
            for (int i = 0; i < Pieces.Count; i++)
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
