using System;
using System.Reflection;
using BAModAPI;
using Cinemachine;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// V in a car is this mod's camera key, with Empire Motors switched on as without it. That mod's view from the driver's
    /// seat (its EmpireMotorsInteriorCameraController) acts on V by itself in any car the player drives, with any modifier
    /// held, and what it does breaks the chase camera: it hands the game's camera list to the game's
    /// indoorVehicleCamera (CameraHelper.SetCamera, which puts every other camera, the game's vehicleCamera too, at
    /// priority 0) and switches the vehicleCamera off, so the chase camera, which follows the vehicleCamera's
    /// priority (<see cref="ChaseCamera"/>), lets go. On a car with no seat of its named kind (every car of the game's
    /// own read so far) it never moves the camera at all: the player is left with the game's camera for driving inside a
    /// building, with its post-processing switched off, and nothing of this mod's notices (<see cref="CameraYield"/>
    /// looks for a camera moved by another hand, or one above the game's priorities).
    ///
    /// So while this mod's camera key is V, that controller is switched off (Behaviour.enabled): the one thing of
    /// another author's mod this mod changes, on the owner's word, because the two views cannot share a key and that
    /// mod has no setting for its own. Its own OnDisable puts the game's vehicle camera back and forgets its view; the
    /// camera the game wants now is then asked for again (<see cref="GameCamera"/>), so nothing is seen of it. It is
    /// switched on again as it was the moment the key is moved off V (<see cref="KeysChanged"/>) or this mod is unloaded,
    /// and then V in a car is that view's again, as its author made it.
    ///
    /// When: the first time the chase camera comes on, or the camera key goes down in a car, in a run of the game. The
    /// cost: nothing per frame. The controller is looked for once (one walk over the loaded assemblies and one search of
    /// the scene's objects); after that a field read.
    /// </summary>
    public static class EmpireView
    {
        private const string ControllerType = "EmpireMotors.Core.EmpireMotorsInteriorCameraController";

        public static IModLogger Logger;

        private static Behaviour _controller;   // its view's controller, once found
        private static bool _looked;            // it has been looked for (and is not there if _controller is null)
        private static bool _claimed;           // this mod switched it off
        private static bool _told;              // the notice has been shown in this run of the game

        /// <summary>True while this mod's camera key is V: then Empire Motors' view on V is kept off.</summary>
        public static bool Claims => EmpireMotors.On && Keys.Key(KeyAction.CameraToggle) == KeyCode.V;

        /// <summary>True while that view is switched off by this mod.</summary>
        public static bool Claimed => _claimed;

        /// <summary>
        /// The player is driving and the camera is this mod's: Empire Motors' view on V is switched off, if the key is V
        /// and it is not off already. Not per frame: on the chase camera's coming on and on the camera key.
        /// </summary>
        public static void Claim(GameManager gm)
        {
            if (_claimed || !Claims)
            {
                return;
            }
            try
            {
                Behaviour controller = Find();
                if (controller == null || !controller.enabled)
                {
                    return;
                }
                CinemachineVirtualCameraBase current = CameraHelper.GetCurrentCamera();
                controller.enabled = false;   // its OnDisable: the game's vehicle camera back, its view forgotten
                _claimed = true;
                GameCamera(gm, current);
                Logger?.Info("Camera: Empire Motors' view from the driver's seat is kept off while this mod's camera key is V (Camera/EmpireView).");
                if (!_told)
                {
                    _told = true;
                    UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, "reallife_empire_camera_key", null, 10f);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>After any change of a key: if the camera key is no longer V, Empire Motors' view is switched on again.</summary>
        public static void KeysChanged()
        {
            if (_claimed && !Claims)
            {
                Release();
            }
        }

        /// <summary>Empire Motors' view switched on again, as it was. Safe at any time.</summary>
        public static void Release()
        {
            if (!_claimed)
            {
                return;
            }
            _claimed = false;
            try
            {
                if (_controller != null)
                {
                    _controller.enabled = true;   // its OnEnable only listens for the player getting in and out again
                    Logger?.Info("Camera: Empire Motors' view from the driver's seat is on V again.");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // Its controller: one of its components on its own object, which lasts the whole run (DontDestroyOnLoad).
        private static Behaviour Find()
        {
            if (_controller != null || _looked)
            {
                return _controller;
            }
            _looked = true;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!OtherModName.Is(assembly.GetName().Name, OtherMods.EmpireMotors.AssemblyPrefix))
                {
                    continue;
                }
                Type type = assembly.GetType(ControllerType, false);
                if (type != null && typeof(Behaviour).IsAssignableFrom(type))
                {
                    _controller = UnityEngine.Object.FindObjectOfType(type) as Behaviour;
                    if (_controller == null)
                    {
                        _looked = false;   // not made yet: looked for again next time
                    }
                    break;
                }
            }
            if (_controller == null && _looked)
            {
                Logger?.Warn("Camera: Empire Motors is switched on but its view from the driver's seat was not found; V in a car may still reach it.");
            }
            return _controller;
        }

        // The camera the game wants now, asked for again after Empire Motors put its vehicle camera back: for a car, the
        // game's own choice (VehicleController.UpdateCamera: in a building or out, forwards or reversing); otherwise the
        // camera that was live before, unless that was one of the vehicle cameras, which on foot is the on-foot camera.
        private static void GameCamera(GameManager gm, CinemachineVirtualCameraBase before)
        {
            if (gm == null)
            {
                return;
            }
            VehicleController vehicle = gm.selectedVehicle;
            if (vehicle != null && vehicle.controlledByPlayer && vehicle.ShouldUseVehicleCam())
            {
                vehicle.UpdateCamera();
                return;
            }
            CinemachineVirtualCameraBase want = before;
            if (want == null || want == gm.vehicleCamera || want == gm.indoorVehicleCamera)
            {
                want = BuildingManager.IsInsideBuilding || Parking.UndergroundParking.UndergroundParkingManager.IsInsideParking ? gm.indoorCamera : gm.pedestrianCamera;
            }
            if (want != null && want.isActiveAndEnabled)
            {
                CameraHelper.SetCamera(want);
            }
        }
    }
}
