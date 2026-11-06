using System;
using BigAmbitions.Characters;
using BigAmbitions.Items;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Meals
{
    /// <summary>
    /// The director's part for takeout and plates. Food the player carries (a meal of the game's in the bag in their hand,
    /// or a home portion packed from the cooking list) is offered as Eat in the game's own rest panel once they sit down
    /// on a chair or a bench themselves; pressed, the food is eaten exactly as the game's inventory (or home cooking) eats
    /// it, once, and the container becomes the place's tray, a plate on the table or one in the lap, eaten with the
    /// seated meal's own steps, at that seat. The rest goes on after. Cost: nothing per frame; the sit and stand of the
    /// local player's character, an opening of the panel and the press each make one decision.
    /// </summary>
    public sealed partial class MealsDirector
    {
        private const float TestWaitSeconds = 120f;   // s: an armed test waits this long for the player to sit down

        private MealSeat _hereArmed;          // the seat the player rests on, waiting for the game's food event of the Eat just pressed
        private string _hereArmedWhat;
        private bool _hereArmedOwn;
        private Transform _herePlace;         // where the player sat when the meal began: a different place, or none, ends it
        private ThirdPersonCharacter _sitter; // the local player's character, whose sitting down and getting up are heard
        private Action<bool> _onSitting;
        private bool _testArmed;              // the Options test waits for the player to sit down
        private float _testUntil;
        private bool _testMeal;               // the meal on is the test's: nothing was eaten
        private bool _testPlate;              // the test's plate or tray was put out

        /// <summary>Eat in the game's rest panel (Meals/TakeoutButton): the food carried eaten here, at the seat the player rests on.</summary>
        public void EatHere()
        {
            try
            {
                if (!Settings.MealsTakeout || _step != Step.None || MealsGame.Loading())
                {
                    return;
                }
                PlayerController player = MealsGame.Player();
                if (!TakeoutGame.Resting(player, out Transform place))
                {
                    Say("Eat was pressed but the player is not resting on a seat of the game's: nothing eaten", true);
                    return;
                }
                CargoInstance food = TakeoutGame.BagMeal();
                TakeoutFood what = MealsRules.Choose(food != null, Carried());
                MealSeat seat = what != TakeoutFood.None ? MealsGame.Here(player, place) : null;
                if (seat == null)
                {
                    Say("Eat was pressed with nothing to eat in the bag or packed: nothing eaten", true);
                    return;
                }
                _herePlace = place;
                _hereArmed = seat;
                _hereArmedOwn = what == TakeoutFood.Packed;
                _hereArmedWhat = what == TakeoutFood.Bag ? "the game's " + food.itemName + " from the bag" : "a home portion packed to take";
                _hungerKnown = true;
                _lastHunger = MealsGame.Hunger();
                // The food event comes back inside this call (GameManager.ChangeHunger): the seat is armed only for it.
                bool ate = what == TakeoutFood.Bag ? TakeoutGame.EatFromBag(food) : MealsBridges.EatCarried != null && MealsBridges.EatCarried();
                if (_hereArmed != null)
                {
                    _hereArmed = null;
                    Say("Eat was pressed but the game ate nothing (too full, a bite already playing, or the food gone): " + (ate ? "no food event came" : "refused"), !ate);
                }
            }
            catch (Exception e)
            {
                _hereArmed = null;
                Logger?.Error(e);
            }
            TakeoutButton.Refresh();
        }

        private int Carried()
        {
            try
            {
                return MealsBridges.Carried != null ? MealsBridges.Carried() : 0;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return 0;
            }
        }

        /// <summary>The name of what Eat would eat now, for its button; null with nothing to eat.</summary>
        internal string EatWhat(out bool offered)
        {
            offered = false;
            PlayerController player = MealsGame.Player();
            bool resting = TakeoutGame.Resting(player, out _);
            CargoInstance food = resting ? TakeoutGame.BagMeal() : null;
            int packed = resting ? Carried() : 0;
            TakeoutFood what = MealsRules.Choose(food != null, packed);
            offered = MealsRules.Offered(Settings.MealsTakeout, resting, _step != Step.None, what);
            if (!offered)
            {
                return null;
            }
            return what == TakeoutFood.Bag ? TakeoutGame.Name(food) : JobText.T(MealsBridges.CarriedName != null ? MealsBridges.CarriedName() : "reallife_home_dish_leftovers");
        }

        // Every frame while a meal at the rested seat is on (a few seconds): what ends it, then its step.
        private void HereStep(PlayerController player)
        {
            // The rest's own panel stays open throughout, so the walk's "a screen came up" cannot be asked here: whatever
            // takes the player away (Stop, a time skip, being held) ends the game's rest, and that ends the meal.
            if (!TakeoutGame.Resting(player, out Transform place) || !ReferenceEquals(place, _herePlace))
            {
                End(false, "the player got up or the rest ended", false);
                return;
            }
            if (_step == Step.Waiting)
            {
                if (!MealsGame.BiteOver(player) && Time.time < _until)
                {
                    return;
                }
                Serve();
                _testPlate = _testMeal && (_body.PlateOut || _shown != null);
                return;
            }
            if (Time.time >= _until && EatingFinished())
            {
                End(true, "the meal was eaten", false);
            }
        }

        // A meal at the rested seat is over: the Eat button decided again, and the test's verdict when it was the test's.
        private void HereEnded(bool eaten, string why)
        {
            _herePlace = null;
            if (_testMeal)
            {
                _testMeal = false;
                PlayerController player = MealsGame.Player();
                bool stillResting = TakeoutGame.Resting(player, out _);
                bool pass = eaten && _testPlate && stillResting && !_body.PlateOut && TakeoutButton.Installed;
                Logger?.Info("Meals check (takeout test): a test plate at the seat the player rests on: " + (_testPlate ? "put out" : "never put out") + ", " + why
                             + ", given back " + (!_body.PlateOut ? "yes" : "no") + ", the player still resting " + (stillResting ? "yes" : "no") + ", Eat in the game's rest panel "
                             + (TakeoutButton.Installed ? "made" : "not made") + ": " + (pass ? "PASS" : "FAIL") + ".");
                MealsGame.Notice(pass ? NotificationType.Success : NotificationType.Error, pass ? MealsText.TestPass : MealsText.TestFail);
            }
            TakeoutButton.Refresh();
        }

        // The local player's character sat down or got up (BaseHuman.onSittingChanged): the Eat button decided again, an
        // armed test begun, and a meal at the rested seat ended when they get up.
        private void OnSitting(bool sat)
        {
            if (!sat && _here && _step != Step.None)
            {
                End(false, "the player got up", false);
            }
            if (sat && _testArmed && Time.unscaledTime <= _testUntil)
            {
                PlayerController player = MealsGame.Player();
                if (TakeoutGame.Resting(player, out Transform place))
                {
                    _testArmed = false;
                    StartTestHere(player, place);
                }
            }
            TakeoutButton.Refresh();
        }

        // The sitting of the player's own character heard, never another's; a new player object (a load) moves it over.
        private void Bind(PlayerController player)
        {
            ThirdPersonCharacter body = player != null ? player.Character : null;
            if (ReferenceEquals(body, _sitter))
            {
                return;
            }
            Unbind();
            if (body != null)
            {
                body.onSittingChanged += _onSitting;
                _sitter = body;
            }
        }

        private void Unbind()
        {
            if (_sitter != null)
            {
                _sitter.onSittingChanged -= _onSitting;
            }
            _sitter = null;
        }

        /// <summary>The Options test: a test plate at the seat the player rests on, now or as soon as they sit down within two minutes.</summary>
        public void TestTakeout()
        {
            if (!Settings.MealsTakeout)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.FeatureOff);
                return;
            }
            if (MealsGame.Loading())
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.NotLoaded);
                return;
            }
            if (_step != Step.None || _dancing)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.ScreenBusy, 0, "a meal or a dance is on");
                return;
            }
            PlayerController player = MealsGame.Player();
            if (TakeoutGame.Resting(player, out Transform place))
            {
                StartTestHere(player, place);
                return;
            }
            _testArmed = true;
            _testUntil = Time.unscaledTime + TestWaitSeconds;
            Logger?.Info("Meals check (takeout test): armed; it begins when the player sits down on a chair or a bench through the game's own Rest within "
                         + TestWaitSeconds.ToString("0", Inv) + " s.");
            MealsGame.Notice(NotificationType.Info, MealsText.TestArmed);
        }

        private void StartTestHere(PlayerController player, Transform place)
        {
            MealSeat seat = MealsGame.Here(player, place);
            if (seat == null)
            {
                Logger?.Info("Meals check (takeout test): the seat the player rests on could not be read: FAIL.");
                MealsGame.Notice(NotificationType.Error, MealsText.TestFail);
                return;
            }
            _herePlace = place;
            _testMeal = true;
            _testPlate = false;
            _hungerKnown = true;
            Begin(seat, false, TestFill, "a test plate (nothing is eaten)", true, false);
        }

        // An armed test that nobody sat down for is dropped, once a second.
        private void TestLapse(float now)
        {
            if (_testArmed && now > _testUntil)
            {
                _testArmed = false;
                Logger?.Info("Meals check (takeout test): nobody sat down within " + TestWaitSeconds.ToString("0", Inv) + " s: not run.");
                MealsGame.Notice(NotificationType.Warning, MealsText.TestLapsed);
            }
        }
    }
}
