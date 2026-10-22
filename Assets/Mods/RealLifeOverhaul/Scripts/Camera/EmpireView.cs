using System;
using System.Reflection;
using BAModAPI;
using Cinemachine;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// V in a car is this mod's camera key, with Empire Motors on too. That mod's view from the driver's seat acts on V in
    /// any car and breaks the chase camera (it puts the vehicleCamera at priority 0), so while this mod's key is V that
    /// controller is switched off: the one thing of another author's mod this mod changes, as the two cannot share a key.
    /// It is switched on again as it was once the key moves off V or this mod unloads. Nothing per frame; the controller
    /// is looked for once a run, from the game's listeners or a search behind the loading screen.
    /// </summary>
    public static class EmpireView
    {
        private const string ControllerType = "EmpireMotors.Core.EmpireMotorsInteriorCameraController";

        public static IModLogger Logger;

        private static Behaviour _controller;   // its view's controller, once found
        private static bool _looked;            // looked for, and not there if _controller is null
        private static bool _claimed;           // this mod switched it off
        private static bool _told;              // the notice has been shown in this run of the game

        /// <summary>True while this mod's camera key is V: then Empire Motors' view on V is kept off.</summary>
        public static bool Claims => EmpireMotors.On && Keys.Key(KeyAction.CameraToggle) == KeyCode.V;

        /// <summary>True while that view is switched off by this mod.</summary>
        public static bool Claimed => _claimed;

        /// <summary>Switches Empire Motors' view off while the key is V: on the chase camera's coming on and on the key, never per frame.</summary>
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

        /// <summary>Switches Empire Motors' view on again, as it was. Safe at any time.</summary>
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

        /// <summary>For ScenePrewarm: true once the controller is found or known absent; false while Empire Motors has not made it.</summary>
        public static bool Prewarm()
        {
            if (!EmpireMotors.On)
            {
                return true;
            }
            try
            {
                Find();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return true;   // looked for on the first drive instead
            }
            return _looked;
        }

        // One of its components on its own object, which lasts the whole run.
        private static Behaviour Find()
        {
            if (_controller != null || _looked)
            {
                return _controller;
            }
            _controller = Listening();
            _looked = true;
            if (_controller != null)
            {
                return _controller;
            }
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!OtherModName.Is(assembly.GetName().Name, OtherMods.EmpireMotors.AssemblyPrefix))
                {
                    continue;
                }
                Type type = assembly.GetType(ControllerType, false);
                if (type != null && typeof(Behaviour).IsAssignableFrom(type))
                {
                    _controller = SceneFinds.Any(type, "camera: Empire Motors' view from the driver's seat being looked for (a search of the whole scene, once a run)") as Behaviour;
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

        // The controller joins the game's vehicle listeners in its OnEnable; absent while it is off, or after a later load.
        private static Behaviour Listening()
        {
            Action<VehicleController> listeners = GlobalEvents.onEnterVehicle;
            if (listeners == null)
            {
                return null;
            }
            foreach (Delegate listener in listeners.GetInvocationList())
            {
                if (listener.Target is Behaviour behaviour && behaviour != null)
                {
                    Type type = behaviour.GetType();
                    if (type.FullName == ControllerType && OtherModName.Is(type.Assembly.GetName().Name, OtherMods.EmpireMotors.AssemblyPrefix))
                    {
                        return behaviour;
                    }
                }
            }
            return null;
        }

        // For a car the game's own choice; otherwise the camera live before, or for a vehicle camera the on-foot or indoor one.
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
