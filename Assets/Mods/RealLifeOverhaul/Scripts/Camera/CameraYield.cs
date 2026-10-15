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
    /// THE ONE PLACE this mod's camera decides to stand aside, for the game's own camera moments and for another author's
    /// camera mod, and to come back. Every camera path of the mod follows the frame's <see cref="Orders"/>: the
    /// figures on the game's on-foot cameras (<see cref="ThirdPersonCamera"/>), the chase camera (<see cref="ChaseCamera"/>),
    /// mouse look in either, and the bridges kept in sight (<see cref="BridgeHold"/>). What the orders are is a rule of
    /// plain values (<see cref="CameraYieldRules"/>, checked offline); this file only reads the game's signals for it.
    ///
    /// THE GAME'S MOMENTS are told by the game's own signals, never by a list of moments: which camera of its own list it
    /// has asked for (priority 1: CameraHelper.CameraTransition), whether a screen that needs the pointer is up, whether it
    /// holds the player in a conversation (DialogController.current: a person's talk, the bank's and the casino's tables,
    /// every one of its dialogs, whose answers are clicked), and whether the player is dead. Any
    /// camera of the game's other than those of ordinary play (on foot, indoors, a car, the map) is a moment: placing an
    /// item and the interior designer, a building's preview, the subway, the minigames, the game's free camera, the casino
    /// boat, a car's preview, and whatever camera the game adds later.
    ///
    /// ANOTHER AUTHOR'S CAMERA MOD: two scripts that both steer the camera give a picture that shakes, and two that both
    /// lock the pointer give a pointer that cannot be used; so while anything that is neither the game nor this mod drives
    /// the camera, this mod's camera does nothing at all (<see cref="StoodDown"/>).
    ///
    /// How another driver is noticed, without knowing its name (<see cref="Observe"/>, once a frame):
    ///  - the game steers its camera through Cinemachine and gives its own virtual cameras (the fourteen it lists in
    ///    GameManager's start-up, CameraHelper.Init) the priority 1 or 0 and no other (CameraHelper.CameraTransition).
    ///    A live virtual camera with a higher priority that is not one of this mod's is therefore another mod's. That
    ///    is how a camera mod that plays by the engine's rules shows;
    ///  - the engine writes the camera's place at the end of each frame (CinemachineBrain) and nothing of the game's
    ///    writes it afterwards. If at the start of the next frame the camera is not where the engine put it, something
    ///    wrote it in between. That is how a camera mod that moves the camera directly shows.
    /// Neither is believed for one frame: <see cref="CameraWatch"/> counts frames running, in both directions.
    /// What is known by name (<see cref="OtherMods"/>) only gives the name for the sentence and shortens the count.
    ///
    /// Nothing is looked at while a screen is open (the city map, a menu): camera mods stand aside for those
    /// themselves, and that is not them stopping. Nor during a moment of the game's: the player's funeral, which the game
    /// films with a timeline's cameras that are not on its list, and a car's preview, filmed by the showcase's own camera.
    ///
    /// The cost, per frame: the screen test, the main camera's reference, two properties of the engine's camera brain
    /// (the live camera, whether it is blending), one reference comparison, and, while nothing foreign is live, the
    /// camera's position and rotation against the brain's (two subtractions and a dot product); for the orders, the
    /// priorities of at most seven of the game's cameras, two of its flags and the options, once a frame whoever asks
    /// first. Nothing is allocated; a virtual camera is judged once, when it becomes the live one. The notice, the log
    /// lines and the options page's sentence are made on the change only.
    /// </summary>
    public static class CameraYield
    {
        private const string Toast = "reallife_camera_other_toast";
        private const string Unnamed = "reallife_camera_other_unnamed";
        private const string TestName = "reallife_camera_test_pretend_name";
        private const string Section = "reallife_camera_other_section";
        private const int GamePriority = 1;           // the highest priority the game gives a virtual camera of its own (CameraHelper.CameraTransition)
        private const float ApartSqr = 0.05f * 0.05f; // metres, squared: further than this from where the engine put it, the camera was moved by something else
        private const float Turned = 0.99996f;        // the dot product of two rotations half a degree apart: under it, the camera was turned by something else
        private const string OwnPrefix = "RealLifeOverhaul";   // every object of this mod's begins so

        public static IModLogger Logger;

        private static CameraWatch _watch;
        private static Camera _camera;
        private static CinemachineBrain _brain;
        private static ICinemachineCamera _judged;   // the live virtual camera last looked at...
        private static bool _judgedForeign;          // ...and what was made of it
        private static string _holder;
        private static bool _told;                   // the notice has been shown in this session of the game
        private static bool _confirm;                // the stand-down is checked in the frame after it was asked for
        private static bool _pretend;
        private static int _decided = -1;          // the frame the orders were last made in
        private static CameraOrders _orders;
        private static GameView _view = GameView.OnFoot;   // the game's camera mode in the last frame decided, for the log line on a change

        /// <summary>True while another mod has the camera and this mod's camera does nothing. One load: fit for every frame.</summary>
        public static bool StoodDown => _watch.StoodDown;

        /// <summary>The mod that has the camera, as its author names it; "another mod" if it is not one this mod knows. Null while none has.</summary>
        public static string Holder => _watch.StoodDown ? _holder : null;

        /// <summary>For the options page: the sentence that says why the camera's options do nothing, or null while they work.</summary>
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

        /// <summary>
        /// The frame's orders for every camera path of this mod (<see cref="CameraYieldRules.Decide"/>), made from the game's
        /// signals once a frame, by whoever asks first. All off while there is no game manager or player.
        /// </summary>
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

        /// <summary>The game's own camera mode in this frame (made with the <see cref="Orders"/>).</summary>
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
            };
            if (signals.View != _view)
            {
                _view = signals.View;
                Logger?.Info("Camera: the game's own camera is now " + _view + (CameraYieldRules.Play(_view) ? "." : ": this mod's camera stands aside for the game's moment."));
            }
            return CameraYieldRules.Decide(signals);
        }

        // The game's camera mode: the camera of its own list it has asked for (priority 1), as far as this mod's camera cares.
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
            if (ThirdPersonCamera.IsUiOpen() || GameMembers.PlayerDead || View == GameView.Moment)
            {
                return;   // (a moment of the game's may film with a camera outside its own list: the funeral, CemeteryFuneralSetup; a car's preview, ShowcaseVehicleController)
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

        // A live virtual camera above every priority the game uses, and not one of this mod's own (the chase camera, the custom shop's).
        private static bool Foreign(ICinemachineCamera live)
        {
            if (live == null || live.Priority <= GamePriority)
            {
                return false;
            }
            GameObject made = live.VirtualCameraGameObject;
            return made == null || !made.name.StartsWith(OwnPrefix, StringComparison.Ordinal);
        }

        // The player drives: a camera taken now with no camera mod switched on is Empire Motors' view from the driver's seat.
        private static bool Driving()
        {
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            return gm != null && gm.selectedVehicle != null && gm.selectedVehicle.controlledByPlayer;
        }

        // The frame in which the camera is given up, or taken back.
        private static void Changed()
        {
            try
            {
                if (_watch.StoodDown)
                {
                    OtherMod known = OtherMods.Of(OtherModKind.Camera) ?? (EmpireMotors.On && Driving() ? OtherMods.EmpireMotors : null);
                    _holder = _pretend ? TestName.GetLocalization() : known != null ? known.Name : Unnamed.GetLocalization();
                    _confirm = true;
                    Logger?.Warn("Camera: another mod drives the camera (" + _holder + (_judgedForeign && _judged != null ? ", through its own camera '" + _judged.Name + "'" : _pretend ? string.Empty : ", by moving the game's camera itself")
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
                             : "FAIL (still on: " + (ThirdPersonCamera.Steering ? "the on-foot camera's figures " : string.Empty) + (ThirdPersonCamera.IsLooking || ChaseCamera.IsLooking ? "mouse look " : string.Empty)
                               + (ChaseCamera.Live ? "the chase camera" : string.Empty) + ")") + ".");
        }

        /// <summary>The notice that says who has the camera. Shown by itself once a session; again when the player presses the camera key and nothing happens.</summary>
        public static void Tell()
        {
            if (_watch.StoodDown)
            {
                Notifications.Show(NotificationType.Info, Toast, new Dictionary<string, string>(1) { { "mod", _holder ?? Unnamed.GetLocalization() } }, 10f);
            }
        }

        /// <summary>
        /// "Test: pretend another mod has the camera": from the next frame every frame counts as one in which
        /// something else drove the camera, so the stand-down runs exactly as it would, and can be seen without
        /// installing anything. Pressed again, the pretence ends and the camera comes back after the usual wait.
        /// </summary>
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
