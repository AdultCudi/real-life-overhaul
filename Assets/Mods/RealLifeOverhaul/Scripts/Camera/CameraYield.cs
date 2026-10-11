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
    /// Standing down for another author's camera mod, and coming back when it stops. Two scripts that both steer the
    /// camera give a picture that shakes, and two that both lock the pointer give a pointer that cannot be used; so
    /// while anything that is neither the game nor this mod drives the camera, this mod's camera does nothing at all:
    /// the game's on-foot cameras are given back their own figures, the chase camera is let go, the pointer is freed
    /// (<see cref="ThirdPersonCamera"/> and <see cref="ChaseCamera"/> ask <see cref="StoodDown"/>).
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
    /// themselves, and that is not them stopping. Nor during the player's funeral, which the game films with a
    /// timeline's cameras that are not on its list.
    ///
    /// The cost, per frame: the screen test, the main camera's reference, two properties of the engine's camera brain
    /// (the live camera, whether it is blending), one reference comparison, and, while nothing foreign is live, the
    /// camera's position and rotation against the brain's (two subtractions and a dot product). Nothing is allocated;
    /// a virtual camera is judged once, when it becomes the live one. The notice, the log lines and the options page's
    /// sentence are made on the change only.
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
        }

        /// <summary>Once a frame, from <see cref="ThirdPersonCamera"/> before it does anything with the camera.</summary>
        public static void Observe()
        {
            if (_confirm)
            {
                _confirm = false;
                Confirm();
            }
            if (ThirdPersonCamera.IsUiOpen() || Helpers.FuneralHelper.PlayerDead)
            {
                return;   // (the funeral is the one scene of the game's that is filmed by cameras outside its own list: CemeteryFuneralSetup)
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

        // The frame in which the camera is given up, or taken back.
        private static void Changed()
        {
            try
            {
                if (_watch.StoodDown)
                {
                    OtherMod known = OtherMods.Of(OtherModKind.Camera);
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
