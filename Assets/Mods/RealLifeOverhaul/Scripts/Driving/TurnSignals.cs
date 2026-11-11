using System;
using System.Globalization;
using BAModAPI;
using UI.Load;
using UnityEngine;
using NwhVehicle = NWH.VehiclePhysics2.VehicleController;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Automatic turn signals for the car the player drives: once the car has been turning a moment, the signal of the side it
    /// turns to comes on through the game's own (the one its keys toggle, with its click), and it goes off when the turn is made
    /// (<see cref="TurnSignalRules"/>). The game's keys work as ever, and a signal the player switched on is never touched.
    /// Only the player's own car; traffic signals its turns through the game's own driving. A signal the rules put on is put off
    /// again whenever the car is let go: the option switched off, another car, getting out, a load, a fault, the city going.
    /// Cost a frame: idle, one comparison with the clock; with a car followed, a look ten times a second (a few field reads, the
    /// nose's heading and one read of the body's property block), nothing allocated.
    /// </summary>
    public sealed class TurnSignals : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("driving", "TurnSignals", typeof(TurnSignals));

        private const float IdleEvery = 0.5f;   // seconds between two looks for a car to follow, while none is

        public static IModLogger Logger;

        public static TurnSignals Instance { get; private set; }

        private readonly TurnSignalState _state = new TurnSignalState();
        private MaterialPropertyBlock _block;   // the body's values read into it, kept for the purpose
        private CarController _car;             // the car followed, while one is
        private NwhVehicle _physics;
        private MonoBehaviour _signals;         // the car's own signal object
        private VehicleController _selected;    // the vehicle last seen in the item panel, and whether it is the player's own
        private bool _mine;
        private CarController _refused;         // a car the signals cannot work on: left to the game's own, not tried again while it lives
        private bool _following;
        private bool _failed;
        private float _nextLook;
        private float _lastLook;
        private Vector3 _heading;               // the nose's heading at the last look, level
        private bool _headed;

        private void Awake()
        {
            Instance = this;
            _block = new MaterialPropertyBlock();
        }

        private static readonly int HotUpdate = HotSpots.Slot("Driving: TurnSignals.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                Release("the feature was switched off after its faults");
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (_failed)
            {
                return;
            }
            float now = Time.time;
            if (now < _nextLook)
            {
                return;
            }
            try
            {
                Look(now);
            }
            catch (Exception e)
            {
                Fault(e);
            }
            _nextLook = now + (_following ? TurnSignalRules.LookEvery : IdleEvery);
        }

        // A fault: the signals the rules put on go off, and the rules are off for the session. The game's own signals are not touched.
        private void Fault(Exception e)
        {
            _failed = true;
            Release("something unexpected");
            Logger?.Error(e);
            Logger?.Warn("Turn signals: switched off for the session after the error above; the car's signals are the game's own.");
        }

        private void Look(float now)
        {
            CarController want = Want();
            if (_following && (_car == null || want != _car))
            {
                Release(_car == null ? "the car is gone" : want == null ? WhyNot() : "another car was driven");
            }
            if (!_following && want != null && want != _refused)
            {
                Take(want, now);
            }
            if (_following)
            {
                Follow(now);
            }
        }

        // The car the signals should follow now, or null: the player's own, driven by them, with the option on.
        private CarController Want()
        {
            if (!Settings.AutoSignals || LoadScene.isLoading)
            {
                return null;
            }
            var game = InstanceBehavior<GameManager>.Instance;
            VehicleController selected = game != null ? game.selectedVehicle : null;
            if (selected != _selected)
            {
                _selected = selected;
                _mine = selected is CarController own && GoingPublic.MineVehicle(own);   // asked once a vehicle, not a look
            }
            return _mine && selected is CarController car && car.controlledByPlayer ? car : null;
        }

        private static string WhyNot()
        {
            return !Settings.AutoSignals ? "the option was switched off" : LoadScene.isLoading ? "a load began" : "the player left the car";
        }

        private void Take(CarController car, float now)
        {
            NwhVehicle physics = TurnSignalGame.Physics(car);
            MonoBehaviour signals = TurnSignalGame.SignalsOf(car);
            string why = physics == null ? "it has no vehicle physics of the game's kind"
                : signals == null ? "it has no signal of the game's own"
                : !TurnSignalGame.CanRead(car) ? "its body has no mesh to read the signals from"
                : null;
            if (why != null)
            {
                _refused = car;
                Logger?.Info("Turn signals: " + car.name + " keeps the game's own signals: " + why + ".");
                return;
            }
            _car = car;
            _physics = physics;
            _signals = signals;
            _state.Reset();
            _headed = false;
            _lastLook = now;
            _following = true;
        }

        // One look at the car: how it is steered and moving, which signals show, and what the rules ask for.
        private void Follow(float now)
        {
            float dt = now - _lastLook;
            _lastLook = now;
            Vector3 forward = _car.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                _headed = false;   // on its nose or its side: no heading to go by
                return;
            }
            forward.Normalize();
            if (!_headed)
            {
                _heading = forward;
                _headed = true;
                return;
            }
            float swung = Vector3.SignedAngle(_heading, forward, Vector3.up);   // degrees, positive to the right
            _heading = forward;
            if (dt <= 0f)
            {
                return;
            }
            TurnSignalLook look = default(TurnSignalLook);
            look.Speed = TurnSignalGame.Speed(_physics);
            look.Steer = TurnSignalGame.Steer(_physics);
            look.Turning = swung / dt;
            TurnSignalGame.Showing(_car, _block, out look.Left, out look.Right);
            float onFor = _state.OnFor;
            float swing = _state.Swing;
            TurnSignalCommand command = TurnSignalRules.Step(_state, look, dt);
            if (command != TurnSignalCommand.None)
            {
                Apply(command, look, onFor, swing);
            }
        }

        // The rules' answer done on the car with the game's own toggle, once, and only if the signal is not already as asked.
        // The state is read from the body's meshes, where the game's own toggle writes it: a signal put on that does not show
        // there is put back and the car is left to the game's own signals, so that a signal the rules cannot see is never left on.
        private void Apply(TurnSignalCommand command, TurnSignalLook look, float onFor, float swing)
        {
            bool left = command == TurnSignalCommand.LeftOn || command == TurnSignalCommand.LeftOff;
            bool on = command == TurnSignalCommand.LeftOn || command == TurnSignalCommand.RightOn;
            bool shows = left ? look.Left : look.Right;
            if (shows != on)
            {
                TurnSignalGame.Toggle(_signals, left);
                if (on && !Shows(left))
                {
                    TurnSignalGame.Toggle(_signals, left);
                    _refused = _car;
                    Logger?.Warn("Turn signals: " + _car.name + " keeps the game's own signals: its toggle did not show in the body's values.");
                    Release("the signal did not show where the game keeps it");
                    return;
                }
            }
            if (Meta.Dev.Verbose)
            {
                Say(look, left, on, onFor, swing);
            }
        }

        private bool Shows(bool left)
        {
            TurnSignalGame.Showing(_car, _block, out bool nowLeft, out bool nowRight);
            return left ? nowLeft : nowRight;
        }

        // The self-check line of a signal put on or off: what the car did against what the rules wanted, and whether the game's signal took it.
        private void Say(TurnSignalLook look, bool left, bool on, float onFor, float swing)
        {
            bool shows = Shows(left);
            string side = left ? "left" : "right";
            string end = shows == on ? "PASS" : "FAIL, the game's signal did not " + (on ? "come on" : "go off");
            if (on)
            {
                Logger?.Info("Turn signal check (a turn to the " + side + "): the nose swinging " + Num(Math.Abs(look.Turning), "F0") + " degrees a second at "
                             + Num(look.Speed * 3.6f, "F0") + " km/h with the wheel at " + Num(Math.Abs(look.Steer) * 100f, "F0") + "%, held " + Num(_state.HeldFor, "F1")
                             + " s against " + Num(TurnSignalRules.TurnHold, "F1") + " s, the " + side + " signal " + (shows ? "shows" : "does not show") + ": " + end + ".");
            }
            else
            {
                Logger?.Info("Turn signal check (the turn made): the " + side + " signal was up " + Num(onFor, "F1") + " s, the nose swung " + Num(swing, "F0")
                             + " degrees against " + Num(TurnSignalRules.TurnDone, "F0") + " and the car ran straight or backed, the " + side + " signal "
                             + (shows ? "still shows" : "is off") + ": " + end + ".");
            }
        }

        private static string Num(float value, string format)
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        // The car let go, whatever the reason: the signal the rules put on goes off with them, unless it has gone off already.
        private void Release(string why)
        {
            if (!_following)
            {
                return;
            }
            _following = false;
            CarController car = _car;
            MonoBehaviour signals = _signals;
            int own = _state.Own;
            _car = null;
            _physics = null;
            _signals = null;
            _headed = false;
            _state.Reset();
            if (own == 0 || car == null || signals == null)
            {
                return;
            }
            try
            {
                TurnSignalGame.Showing(car, _block, out bool left, out bool right);
                bool was = own < 0 ? left : right;
                if (was)
                {
                    TurnSignalGame.Toggle(signals, own < 0);
                }
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Turn signal check (given back): " + car.name + ", the " + (own < 0 ? "left" : "right") + " signal the rules had on is "
                                 + (was ? "put off" : "off already") + " (" + why + "): PASS.");
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Turn signals: a signal could not be put off (" + why + "). " + e.Message);
            }
        }

        private void OnDestroy()
        {
            Release("the city is going");
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
