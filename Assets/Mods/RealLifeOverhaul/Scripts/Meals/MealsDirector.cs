using System;
using System.Globalization;
using BAModAPI;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Meals
{
    /// <summary>
    /// Meals and music. When the player eats (anything the game feeds them, or a meal cooked at home) and a free seat is
    /// near in the same room, the player walks there, sits down with the food in front of them (the table's own tray, as
    /// the game's customers have it, or a plate), eats for a short while and gets up; the food's effect is the game's own
    /// and happens once, before any of this. Food carried to a seat the player sat on themselves is eaten there through
    /// Eat in the game's rest panel (<c>MealsDirector.Takeout.cs</c>), by the same steps without the walk. Dancing to the
    /// speakers' music is in <c>MealsDirector.Dance.cs</c>. Nothing is saved or sent: everything is what this machine shows. Cost: idle, one time comparison a frame and a look once a
    /// second; during a meal (a few seconds), a handful of field reads a frame.
    /// </summary>
    public sealed partial class MealsDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("meals", "MealsDirector", typeof(MealsDirector));   // its Update (Core/Guard)
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private const string FoodEaten = "ba:gameevent_foodeaten";   // the game's event for anything that fills (GameManager.ChangeHunger)
        private const float NearEnough = 1.5f;                       // m: arrived, when the walk ends this close to the seat's side

        public static IModLogger Logger;
        public static MealsDirector Instance { get; private set; }

        private enum Step { None, Waiting, Walking, Seated }

        private JobProps _props;
        private MealBody _body;
        private Step _step;
        private MealSeat _seat;
        private bool _cooked;            // a plate from the home's cooker, carried over
        private bool _here;              // at the seat the game's own rest has the player on: no walk, and no sitting down or getting up of ours
        private bool _own;               // a dish of the player's own (packed at home): a plate, never the place's tray
        private float _fill;             // what the meal filled, of the game's 100: how long it is eaten
        private float _until;            // game seconds (Time.time): the step's last moment
        private float _began;            // real seconds: when the meal began, for its line
        private string _food;            // what is in front of the player, for the line
        private Transform _shown;        // the dish of the table's own display that is showing
        private bool _benchBefore;       // the street bench's own setting, given back
        private bool _inside;
        private BuildingRegistration _in;
        private PlayerController _player;
        private bool _listening;
        private MealSeat _armed;         // a seat found for a home-cooked plate, waiting for the game's food event
        private float _armedFill;
        private float _armedUntil;
        private float _lastHunger;
        private bool _hungerKnown;
        private float _nextLook;         // real seconds: idle, the next look; 0 while a meal needs every frame
        private float _nextSlow;
        private Action<string> _onGameEvent;
        private Action<string> _gameEventSeen;
        private Action _onSave;

        private void Awake()
        {
            Instance = this;
            _props = new JobProps(transform);
            _body = new MealBody(_props, Logger);
            MealsGame.Answers(Arrived, Moved, Blocked);
            _onGameEvent = Guard.Wrap<string>(OnGameEvent);
            _onSave = OnSave;
            _onSitting = Guard.Wrap<bool>(OnSitting);
            DanceButton.Logger = Logger;
            TakeoutButton.Logger = Logger;
        }

        private void OnDisable()
        {
            End(false, "its updates stopped", true);
            StopDancing("its updates stopped", false);
        }

        private void OnDestroy()
        {
            End(false, "the city is unloading", true);
            StopDancing("the city is unloading", false);
            DanceButton.Remove();
            TakeoutButton.Remove();
            Unbind();
            SaveHook.Drop(_onSave);
            GameEvent.onGameEventTriggered -= _onGameEvent;
            _props.Dispose();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Meals: MealsDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                End(false, "a fault", true);
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float now = Time.unscaledTime;
            if (now < _nextLook)
            {
                return;
            }
            if (_step != Step.None)
            {
                Meal();
            }
            if (now >= _nextSlow)
            {
                _nextSlow = now + 1f;
                Slow(now);
            }
            _nextLook = _step != Step.None ? 0f : _nextSlow;
        }

        // Once a second: the game's events kept hold of, the hunger bar's level, the dance and its button.
        private void Slow(float now)
        {
            if (MealsGame.Loading())
            {
                End(false, "a load", false);
                StopDancing("a load", false);
                _hungerKnown = false;
                _armed = null;
                _hereArmed = null;
                return;
            }
            SaveHook.Keep(_onSave);
            EventHold.Keep(ref GameEvent.onGameEventTriggered, _onGameEvent, ref _gameEventSeen);
            PlayerController player = MealsGame.Player();
            _body.Bind(player);
            Bind(player);
            _lastHunger = MealsGame.Hunger();
            _hungerKnown = true;
            if (_armed != null && now > _armedUntil)
            {
                _armed = null;
            }
            DanceSlow(player);
            if (Settings.MealsDance && MealsGame.OwnPlace())
            {
                DanceButton.Install();   // once per city; a field read once it is in
            }
            if (Settings.MealsTakeout)
            {
                TakeoutButton.Install();   // once per city, the same
            }
            TestLapse(now);
        }

        // ---------------------------------------------------------------- a meal, step by step

        private void OnGameEvent(string gameEvent)
        {
            if (gameEvent != FoodEaten)
            {
                return;
            }
            float hunger = MealsGame.Hunger();
            float rise = _hungerKnown ? hunger - _lastHunger : 0f;
            _lastHunger = hunger;
            if (_armed != null)
            {
                MealSeat seat = _armed;
                _armed = null;
                Begin(seat, true, _armedFill, "a meal cooked at home");
                return;
            }
            if (_hereArmed != null)
            {
                MealSeat seat = _hereArmed;
                _hereArmed = null;
                Begin(seat, false, rise, _hereArmedWhat, true, _hereArmedOwn);
                return;
            }
            if (!Settings.MealsSeated || _step != Step.None || !MealsRules.Wanted(rise))
            {
                return;
            }
            PlayerController player = MealsGame.Player();
            if (!MealsGame.Free(player) || !MealsGame.HandsFit())
            {
                Say("the player ate (" + rise.ToString("0", Inv) + ") while held, seated, in a vehicle or carrying something: eaten as the game has it", true);
                return;
            }
            MealSeat found = MealsGame.Find(player, out int looked, out int elsewhere);
            if (found == null)
            {
                Say("the player ate (" + rise.ToString("0", Inv) + "); no free seat of " + looked + " looked at is within " + MealsRules.Reach.ToString("0", Inv)
                    + " m in the same room" + Elsewhere(elsewhere) + ": eaten where they stood", true);
                return;
            }
            Begin(found, false, rise, "food of the game's");
        }

        /// <summary>
        /// Home cooking has a plate ready at the cooker (Home/CookActivity through Wiring/MealsLinks): true when a seat is
        /// found, so the plate is carried there and eaten at it rather than at the stove. The food is the cooking's own.
        /// </summary>
        public bool ServeCooked(int fill)
        {
            try
            {
                if (!Settings.MealsCooked || _step != Step.None || MealsGame.Loading())
                {
                    return false;
                }
                PlayerController player = MealsGame.Player();
                if (!MealsGame.Free(player) || !MealsGame.HandsFit())
                {
                    Say("a home-cooked plate is ready but the player is held or carrying something: eaten at the cooker", true);
                    return false;
                }
                MealSeat seat = MealsGame.Find(player, out int looked, out int elsewhere);
                if (seat == null)
                {
                    Say("a home-cooked plate is ready; no free seat of " + looked + " looked at is in the same room" + Elsewhere(elsewhere) + ": eaten at the cooker", true);
                    return false;
                }
                _armed = seat;
                _armedFill = fill;
                _armedUntil = Time.unscaledTime + 2f;
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _armed = null;
                return false;
            }
        }

        private void Begin(MealSeat seat, bool cooked, float fill, string what, bool here = false, bool own = false)
        {
            PlayerController player = MealsGame.Player();
            if (player == null || seat == null)
            {
                return;
            }
            _seat = seat;
            _cooked = cooked;
            _here = here;
            _own = own;
            _fill = fill;
            _player = player;
            _inside = MealsGame.Inside;
            _in = MealsGame.Building();
            _began = Time.unscaledTime;
            _food = null;
            if (!here)
            {
                MealsGame.Reserve(seat, true);   // at a seat of the game's rest, the game has kept it for the player already
            }
            _step = Step.Waiting;
            _until = Time.time + MealsRules.WaitMost;
            _nextLook = 0f;
            Logger?.Info(here ? "Meals: " + what + " (" + fill.ToString("0", Inv) + ") is put in front of the player at " + seat.Kind + ", where they rest."
                : "Meals: " + what + " (" + fill.ToString("0", Inv) + ") is taken to " + seat.Kind + ", " + seat.Option.Flat.ToString("0.0", Inv) + " m away ("
                  + seat.Option.Path.ToString("0.0", Inv) + " m on foot).");
        }

        private bool Wanted => _here ? Settings.MealsTakeout : _cooked ? Settings.MealsCooked : Settings.MealsSeated;

        // Every frame while a meal is on: what ends it, then its step.
        private void Meal()
        {
            PlayerController player = MealsGame.Player();
            if (player == null || !ReferenceEquals(player, _player) || player.Character == null)
            {
                End(false, "the player is gone", false);
                return;
            }
            if (!Wanted)
            {
                End(false, "its option was switched off", true);
                return;
            }
            if (MealsGame.Inside != _inside || !ReferenceEquals(MealsGame.Building(), _in))
            {
                End(false, "the player went through a door or travelled", true);
                return;
            }
            if (_here)
            {
                HereStep(player);
                return;
            }
            switch (_step)
            {
                case Step.Waiting:
                    if ((MealsGame.ScreenUp() || !MealsGame.BiteOver(player)) && Time.time < _until)
                    {
                        return;
                    }
                    if (MealsGame.ScreenUp() || !MealsGame.Free(player))
                    {
                        End(false, "a screen or something of the game's still held the player", true);
                        return;
                    }
                    SetOff(player);
                    return;
                case Step.Walking:
                    if (MealsGame.ScreenUp())
                    {
                        End(false, "a screen came up on the way", true);
                    }
                    else if (Time.time > _until)
                    {
                        End(false, "the walk never arrived", true);
                    }
                    return;
                case Step.Seated:
                    if (MealsGame.MovePressed())
                    {
                        End(false, "the player moved off", true);
                    }
                    else if (MealsGame.ScreenUp())
                    {
                        End(false, "a screen came up", true);
                    }
                    else if (Time.time >= _until)
                    {
                        End(true, "the meal was eaten", true);
                    }
                    return;
            }
        }

        private void SetOff(PlayerController player)
        {
            if (_cooked)
            {
                bool carried = _body.Carry();
                _food = carried ? "the plate carried in both hands" : "nothing carried (no clip or prop for it)";
            }
            Vector3 gap = _seat.Stand - player.transform.position;
            gap.y = 0f;
            _step = Step.Walking;
            _until = Time.time + MealsRules.WalkMost;
            if (gap.sqrMagnitude < 0.25f * 0.25f)
            {
                Arrived();
                return;
            }
            if (!MealsGame.WalkTo(player, _seat.Stand))
            {
                End(false, "the game would not take a walk order", false);
                return;
            }
            MealsGame.Listen(player);   // after the order: giving it raises the same event
            _listening = true;
        }

        // The game's walk ended at the seat's side.
        private void Arrived()
        {
            if (_step != Step.Walking || _seat == null || _player == null)
            {
                return;
            }
            Vector3 off = _seat.Stand - _player.transform.position;
            off.y = 0f;
            if (off.sqrMagnitude > NearEnough * NearEnough)
            {
                End(false, "the walk ended short of the seat", true);
                return;
            }
            if (!MealsGame.StillFree(_seat))
            {
                End(false, "the seat was taken meanwhile", true);
                return;
            }
            if (!_body.Sit(_seat.Place))
            {
                End(false, "the player could not be seated (in a car, hands full, or off the walking mesh)", true);
                return;
            }
            _benchBefore = MealsGame.Bench(_seat, true, false);
            Serve();
        }

        /// <summary>
        /// The food put in front of the seated player and the eating begun: the table's own tray, a plate on the table, a
        /// plate in the lap, or the hand (<see cref="MealsRules.Serve"/>). The one path for a meal walked to a seat and for
        /// food eaten at the seat the player rests on.
        /// </summary>
        private void Serve()
        {
            bool lap = MealsGame.Lap(_seat, out Vector3 lapAt, out Quaternion lapTurn);
            Plate plate = MealsRules.Serve(_seat.Display != null, _cooked || _own, _seat.Top, lap);
            if (plate == Plate.Tray && MealsGame.ShowDisplay(_seat, true, ref _shown))
            {
                _body.PlateAway();
                _food = "the table's own tray";
            }
            else if (_seat.Top && _body.PlateOn(_seat.PlateAt, _seat.PlateTurn))
            {
                _food = "a plate on the table";
            }
            else if (lap && _body.PlateOn(lapAt, lapTurn))
            {
                _food = "no table in front of the seat: a plate in the lap";
            }
            else
            {
                _body.PlateAway();
                _food = "no table and no plate to hand: the meal in hand";
            }
            bool eats = _body.Eat();
            _step = Step.Seated;
            _until = Time.time + MealsRules.EatSeconds(_fill);
            Say("seated at " + _seat.Kind + " with " + _food + "; eating by " + _body.EatingBy + " for " + MealsRules.EatSeconds(_fill).ToString("0", Inv) + " s", eats && (_here || _body.Seated));
        }

        // The game's own event for a new walk order or a movement key: on the way, the player has gone elsewhere.
        private void Moved()
        {
            if (_step == Step.Walking)
            {
                End(false, "the player walked off elsewhere", false);
            }
        }

        // A click to walk while held on the seat (the game's own event for it): up at once.
        private void Blocked()
        {
            if (_step == Step.Seated)
            {
                End(false, "the player clicked to walk away", true);
            }
        }

        private void OnSave()
        {
            End(false, "a save is being written: the player stands where they stood", true);
        }

        /// <summary>
        /// Ends the meal from any step: the listeners off, the walk stopped (unless the player gave a new one), the tray put
        /// away, the plate back on the shelf, the player on their feet, the seat and the bench given back. Safe at any time.
        /// </summary>
        public void End(bool eaten, string why, bool stopWalk)
        {
            if (_step == Step.None)
            {
                return;
            }
            Step was = _step;
            _step = Step.None;
            bool here = _here;
            _here = false;
            PlayerController player = _player;
            MealSeat seat = _seat;
            _seat = null;
            _player = null;
            try
            {
                if (_listening)
                {
                    _listening = false;
                    MealsGame.Unlisten(player);
                }
                if (was == Step.Walking && stopWalk)
                {
                    MealsGame.StopWalk(player);
                }
                if (seat != null)
                {
                    MealsGame.ShowDisplay(seat, false, ref _shown);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _body.Release();
            try
            {
                if (seat != null && !here)
                {
                    MealsGame.Bench(seat, false, _benchBefore);
                    MealsGame.Reserve(seat, false);   // a seat of the game's rest stays the game's: the player still rests there
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _shown = null;
            Say((_cooked ? "a home-cooked plate" : here ? "a meal at the seat the player rests on" : "a meal") + " ended after " + (Time.unscaledTime - _began).ToString("0.0", Inv)
                + " s at the " + was.ToString().ToLowerInvariant() + " step (" + why + ")"
                + (here ? "; the plate and the tray put away, the player still resting there" : "; the player is on their feet, the seat and the table given back"), !_body.Seated && !_body.PlateOut);
            _nextLook = 0f;
            if (here)
            {
                HereEnded(eaten, why);
            }
        }

        /// <summary>A meal is on: walking to a seat, or seated at it.</summary>
        public bool Busy => _step != Step.None;

        // The seats a search left out for being upstairs or down, for its line: none said when there were none.
        private static string Elsewhere(int seats)
        {
            return seats > 0 ? " (" + seats + " on another floor left out)" : string.Empty;
        }

        // One self-check line, with Developer diagnostics on.
        private static void Say(string what, bool pass)
        {
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Meals check (meal): " + what + ": " + (pass ? "PASS" : "FAIL") + ".");
            }
        }

        /// <summary>Everything of the feature given back: for the options page's clean-up. Safe when idle.</summary>
        public void StopAll(string why)
        {
            End(false, why, true);
            StopDancing(why, true);
            _armed = null;
            _hereArmed = null;
            _testArmed = false;
        }
    }
}
