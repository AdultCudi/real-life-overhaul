using System;
using System.Collections.Generic;
using BAModAPI;
using Cinemachine;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The one place this mod's camera decides to stand aside, for the game's camera moments and for another author's
    /// camera mod, and to come back; the rule is <see cref="CameraYieldRules"/>'s, and this reads the game's signals for it.
    /// Another camera mod is noticed without its name (<see cref="Observe"/>): a live virtual camera above the game's
    /// priorities that is not this mod's, or a camera not where the engine's brain put it. <see cref="CameraWatch"/>
    /// counts either over frames. Cost per frame: the screen test, two properties of the brain, a reference comparison and
    /// a position and rotation compared; for the orders, at most seven priorities. Nothing allocated.
    /// </summary>
    public static class CameraYield
    {
        private const string Toast = "reallife_camera_other_toast";
        private const string Unnamed = "reallife_camera_other_unnamed";
        private const string TestName = "reallife_camera_test_pretend_name";
        private const string Section = "reallife_camera_other_section";
        private const int GamePriority = 1;           // the highest priority the game gives a virtual camera of its own
        private const float ApartSqr = 0.05f * 0.05f; // metres, squared: further than this from where the engine put it, something else moved it
        private const float Turned = 0.99996f;        // the dot product of two rotations half a degree apart
        private const string OwnPrefix = "RealLifeOverhaul";   // every object of this mod's begins so

        public static IModLogger Logger;

        private static CameraWatch _watch;
        private static Camera _camera;
        private static CinemachineBrain _brain;
        private static ICinemachineCamera _judged;   // the live virtual camera last looked at
        private static bool _judgedForeign;          // and what was made of it
        private static string _holder;
        private static bool _told;                   // the notice has been shown in this session of the game
        private static bool _confirm;                // the stand-down is checked in the frame after
        private static bool _pretend;
        private static int _decided = -1;          // the frame the orders were last made in
        private static CameraOrders _orders;
        private static GameView _view = GameView.OnFoot;   // the last frame's, for the log line on a change

        /// <summary>True while another mod has the camera and this mod's camera does nothing. A field read.</summary>
        public static bool StoodDown => _watch.StoodDown;

        /// <summary>The mod that has the camera, as its author names it, or "another mod"; null while none has.</summary>
        public static string Holder => _watch.StoodDown ? _holder : null;

        /// <summary>For the options page: why the camera's options do nothing, or null while they work.</summary>
        public static string Sentence()
        {
            return _watch.StoodDown ? Section.GetLocalization().Replace("{mod}", _holder ?? Unnamed.GetLocalization()) : null;
        }

        /// <summary>A new city: nothing has been seen in it yet. What the player was told this session stays told.</summary>
        public static void Reset()
        {
            _watch.Reset();
            _camera = null;
            _brain = null;
            _judged = null;
            _judgedForeign = false;
            _holder = null;
            _confirm = false;
            _pretend = false;
            _decided = -1;
            _orders = default;
            _view = GameView.OnFoot;
        }

        /// <summary>The frame's orders, made from the game's signals once a frame by whoever asks first; all off with no player.</summary>
        public static CameraOrders Orders
        {
            get
            {
                int frame = Time.frameCount;
                if (frame != _decided)
                {
                    _decided = frame;
                    _orders = Decide(InstanceBehavior<GameManager>.Instance);
                }
                return _orders;
            }
        }

        /// <summary>The game's own camera mode in this frame.</summary>
        public static GameView View
        {
            get
            {
                _ = Orders;
                return _view;
            }
        }

        private static CameraOrders Decide(GameManager gm)
        {
            if (gm == null || gm.playerController == null)
            {
                return default;
            }
            VehicleController vehicle = gm.selectedVehicle;
            var signals = new CameraSignals
            {
                View = ViewOf(gm),
                Screen = ThirdPersonCamera.IsUiOpen(),
                Talk = DialogController.current != null,
                Dead = GameMembers.PlayerDead,
                OtherMod = _watch.StoodDown,
                InCar = vehicle != null && vehicle.vehicleType != null && !vehicle.vehicleType.spawnInPlayerObject,
                CameraOn = Settings.CameraEnabled,
                Indoors = Settings.CameraIndoors,
                CarCamera = Settings.CarCamera,
                MouseLook = Settings.MouseLook,
                CursorFreed = ThirdPersonCamera.CursorFreed,
                Focused = Application.isFocused,
                CarMouse = PlayerPrefSettings.VehicleMouseInput,
                OnBridge = BridgeHold.OnBridge(gm),
                CloseInPicture = ChaseCamera.InPicture || ThirdPersonCamera.InPicture,
            };
            if (signals.View != _view)
            {
                _view = signals.View;
                Logger?.Info("Camera: the game's own camera is now " + _view + (CameraYieldRules.Play(_view) ? "." : ": this mod's camera stands aside for the game's moment."));
            }
            return CameraYieldRules.Decide(signals);
        }

        // The camera of the game's own list it has asked for (priority 1).
        private static GameView ViewOf(GameManager gm)
        {
            if (Asked(gm.pedestrianCamera))
            {
                return GameView.OnFoot;
            }
            if (Asked(gm.indoorCamera))
            {
                return GameView.Indoor;
            }
            if (Asked(gm.vehicleCamera))
            {
                return GameView.Driving;
            }
            if (Asked(gm.vehicleCameraReverse) || Asked(gm.indoorVehicleCamera) || Asked(gm.indoorVehicleCameraReverse))
            {
                return GameView.VehicleOther;
            }
            return Asked(gm.citymapCamera) ? GameView.Map : GameView.Moment;
        }

        private static bool Asked(CinemachineVirtualCameraBase camera)
        {
            return camera != null && camera.Priority == GamePriority;
        }

        /// <summary>Once a frame, from <see cref="ThirdPersonCamera"/> before it does anything with the camera.</summary>
        public static void Observe()
        {
            if (_confirm)
            {
                _confirm = false;
                Confirm();
            }
            // Read here, not through the orders, which come after this count: another mod is stood aside for in the frame it shows.
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            if (ThirdPersonCamera.IsUiOpen() || GameMembers.PlayerDead || (gm != null && ViewOf(gm) == GameView.Moment))
            {
                return;   // a moment may film with a camera off the game's list (the funeral, a car's preview)
            }
            bool foreign = _pretend;
            if (!foreign)
            {
                Camera camera = GameManager.GetMainCamera();
                if (camera != _camera)
                {
                    _camera = camera;
                    _brain = camera != null ? camera.GetComponent<CinemachineBrain>() : null;
                    _judged = null;
                    _judgedForeign = false;
                }
                CinemachineBrain brain = _brain;
                if (brain != null && brain.isActiveAndEnabled)
                {
                    ICinemachineCamera live = brain.ActiveVirtualCamera;
                    if (!ReferenceEquals(live, _judged))
                    {
                        _judged = live;
                        _judgedForeign = Foreign(live);
                    }
                    foreign = _judgedForeign;
                    if (!foreign && live != null && !brain.IsBlending)
                    {
                        // Where the engine put the camera at the end of the last frame, against where it is now.
                        CameraState put = brain.CurrentCameraState;
                        Transform view = camera.transform;
                        foreign = (view.position - put.FinalPosition).sqrMagnitude > ApartSqr
                                  || Mathf.Abs(Quaternion.Dot(view.rotation, put.FinalOrientation)) < Turned;
                    }
                }
            }
            if (_watch.Feed(foreign, OtherMods.RpgCamera.Loaded))
            {
                Changed();
            }
        }

        // A live virtual camera above every priority the game uses, and not one of this mod's own.
        private static bool Foreign(ICinemachineCamera live)
        {
            if (live == null || live.Priority <= GamePriority)
            {
                return false;
            }
            GameObject made = live.VirtualCameraGameObject;
            return made == null || !made.name.StartsWith(OwnPrefix, StringComparison.Ordinal);
        }

        // A camera taken while driving with no camera mod on is Empire Motors' view from the driver's seat.
        private static bool Driving()
        {
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            return gm != null && gm.selectedVehicle != null && gm.selectedVehicle.controlledByPlayer;
        }

        private static void Changed()
        {
            try
            {
                if (_watch.StoodDown)
                {
                    OtherMod known = OtherMods.Of(OtherModKind.Camera) ?? (EmpireMotors.On && Driving() ? OtherMods.EmpireMotors : null);
                    _holder = _pretend ? TestName.GetLocalization() : known != null ? known.Name : Unnamed.GetLocalization();
                    _confirm = true;
                    Logger?.Warn("Camera: another mod drives the camera (" + _holder
                                 + (_judgedForeign && _judged != null ? ", through its own camera '" + _judged.Name + "'" : _pretend ? string.Empty : ", by moving the game's camera itself")
                                 + "). This mod's camera, mouse look and chase camera stand aside until it stops.");
                    if (!_told)
                    {
                        _told = true;   // once in a session of the game, not on every load
                        Tell();
                    }
                }
                else
                {
                    Logger?.Info("Camera: nothing else has driven the camera for " + CameraWatch.ToReturn + " frames. This mod's camera is back.");
                    _holder = null;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // One frame after the stand-down: is anything of this mod's still on the camera.
        private static void Confirm()
        {
            if (!Meta.Dev.Verbose || !_watch.StoodDown)
            {
                return;
            }
            bool clear = !ThirdPersonCamera.Steering && !ThirdPersonCamera.IsLooking && !ChaseCamera.Live && !ChaseCamera.IsLooking;
            Logger?.Info("Camera check: another mod drives the camera (" + _holder + "): "
                         + (clear ? "stood down: PASS"
                             : "FAIL (still on: " + (ThirdPersonCamera.Steering ? "the on-foot camera's figures " : string.Empty)
                               + (ThirdPersonCamera.IsLooking || ChaseCamera.IsLooking ? "mouse look " : string.Empty)
                               + (ChaseCamera.Live ? "the chase camera" : string.Empty) + ")") + ".");
        }

        /// <summary>The notice of who has the camera: once a session by itself, again when the camera key does nothing.</summary>
        public static void Tell()
        {
            if (_watch.StoodDown)
            {
                Notifications.Show(NotificationType.Info, Toast, new Dictionary<string, string>(1) { { "mod", _holder ?? Unnamed.GetLocalization() } }, 10f);
            }
        }

        /// <summary>Test: every frame counts as foreign until pressed again, so the stand-down runs as it would.</summary>
        public static void TestPretend()
        {
            try
            {
                if (!ModState.Loaded || SaveGameManager.Current == null || GameManager.GetMainCamera() == null)
                {
                    TestRefusals.Tell(Logger, "Pretend another mod has the camera", TestRefusal.NotLoaded);
                    return;
                }
                _pretend = !_pretend;
                _told = _told && !_pretend;   // the test shows the notice too
                Logger?.Info("Camera test: " + (_pretend ? "from the next frame out of a screen, something else is taken to drive the camera." : "the pretence is over; the camera comes back after "
                                                                                                                                            + CameraWatch.ToReturn + " clean frames."));
                Notifications.Show(NotificationType.Info, _pretend ? "reallife_camera_test_pretend_on" : "reallife_camera_test_pretend_off", null, 8f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
