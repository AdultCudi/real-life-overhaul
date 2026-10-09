using System.Collections.Generic;
using BigAmbitions.Characters;
using Helpers;
using PlayerActivity;
using UI.Elements;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One of the mod's own routines, run as one of the game's activities: the game's own panel
    /// (a start and a cancel button, the slider, the progress bar, its fast-forward and its time
    /// skip) and the game's own way there (the player walks to the furniture or the piece of
    /// floor; a click elsewhere on the way calls it off). What a routine adds is in four places:
    /// where to go, what the body does on arrival (<see cref="Begin"/>), what happens as the
    /// minutes go by (<see cref="Minute"/>) and what it all came to (<see cref="End"/>).
    ///
    /// The body is the director's <see cref="JobBody"/>: held on the spot, then one of the mod's
    /// clips by id if the bundle has it, else the game's own animation. Whatever was started is
    /// undone in <see cref="Finish"/>, which every way out goes through: the stop button, the
    /// end of the time, the time skip ending, the player running out of energy.
    /// </summary>
    public abstract class HomeActivity : IPlayerActivity
    {
        private const float SkipAfter = 6f;      // seconds of a routine that are watched before the game's time skip takes the rest
        private const int WatchedWhole = 14;     // a routine no longer than this many game minutes is watched to the end

        protected readonly HomeDirector Host;
        protected PlayerActivityState State = PlayerActivityState.NotStarted;
        private PlayerActivityState _before = PlayerActivityState.NotStarted;
        protected int Total;
        protected int Done;
        private bool _begun;

        /// <summary>When (unscaled time) the director starts the game's time skip for this routine; negative for never.</summary>
        public float SkipAt = -1f;

        protected HomeActivity(HomeDirector host)
        {
            Host = host;
        }

        public abstract Doing Kind { get; }
        protected abstract string Title { get; }
        protected abstract string StartKey { get; }

        /// <summary>The furniture to walk to, or null for a piece of floor (<see cref="Floor"/>).</summary>
        protected virtual EntityController Furniture => null;

        /// <summary>The clear floor wanted round a routine without furniture.</summary>
        protected virtual float Floor => 0.7f;

        /// <summary>Last look before setting off: false (having said why) keeps the panel open.</summary>
        protected virtual bool Ready(PlayerController player) => true;

        protected abstract void Begin(PlayerController player);
        protected virtual void Minute(PlayerController player, int minutes) { }
        protected abstract void End(PlayerController player, bool completed, int minutes);

        protected virtual int SliderLow => 0;
        protected virtual int SliderHigh => 0;
        protected virtual int SliderNow => Total;
        protected virtual void Slide(int value) { Total = value; }
        protected virtual (string, object) SliderText() => (Title, null);
        protected virtual LabelInfo[] Labels() => null;

        public bool Running => State == PlayerActivityState.Running;
        public bool Over => State == PlayerActivityState.Finished;

        // ---------------------------------------------------------------- the game's contract

        public virtual bool RequiresEnergy() => false;
        public PlayerActivityState GetState() => State;
        public PlayerActivityState GetStateBeforeFinishing() => _before;
        public void ChangeState(PlayerActivityState state) { State = state; }

        public void Perform(int minutes)
        {
            if (State == PlayerActivityState.Finished)
            {
                return;
            }
            Done += minutes;
            if (Done >= Total)
            {
                Finish();
                return;
            }
            Minute(PlayerHelper.PlayerController, minutes);
        }

        public void Finish()
        {
            if (State == PlayerActivityState.Finished)
            {
                return;
            }
            PlayerController player = PlayerHelper.PlayerController;
            if (State == PlayerActivityState.MovingTowardsActivity && player != null)
            {
                player.ResetNavigation();
            }
            _before = State;
            State = PlayerActivityState.Finished;
            SkipAt = -1f;
            if (_begun)
            {
                _begun = false;
                Host.Body.Release();
                try
                {
                    End(player, Done >= Total, Mathf.Min(Done, Total));
                }
                catch (System.Exception e)
                {
                    HomeDirector.Logger?.Error(e);
                }
            }
            Done = 0;
        }

        private void Cancel()
        {
            _before = State;
            State = PlayerActivityState.Finished;
        }

        private void Start()
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || !Ready(player))
            {
                return;
            }
            State = PlayerActivityState.MovingTowardsActivity;
            // Two frames on, as the game's own activities wait one: its panel has let the clock run again by then.
            Host.Later(SetOff, 2);
        }

        private void SetOff()
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (State != PlayerActivityState.MovingTowardsActivity || player == null)
            {
                return;
            }
            EntityController furniture = Furniture;
            Vector3 target;
            if (furniture != null)
            {
                if (!player.ExistsRoute(furniture, showErrorNotification: true))
                {
                    Cancel();
                    return;
                }
                target = furniture.GetClosestNavMeshTargetPosition(player.transform.position);
            }
            else if (!HomePlace.ClearFloor(player, Floor, out target))
            {
                HomeDirector.Refuse("reallife_home_refused_no_room", Title);
                Cancel();
                return;
            }
            Vector3 gap = target - player.transform.position;
            gap.y = 0f;
            if (target == Vector3.zero || gap.sqrMagnitude < 0.2f * 0.2f)
            {
                player.ResetWalkingAnimation();
                Arrived();
            }
            else
            {
                player.SetGoal(target, Arrived);
            }
        }

        private void Arrived()
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (State != PlayerActivityState.MovingTowardsActivity || player == null || player.Character == null)
            {
                return;
            }
            EntityController furniture = Furniture;
            if (furniture != null)
            {
                if (furniture.Occupied)
                {
                    HomeDirector.Refuse("reallife_home_refused_in_use", Title);
                    Cancel();
                    return;
                }
                Vector3 look = furniture.transform.position - player.transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f)
                {
                    player.Character.ForceToRotation(Quaternion.LookRotation(look.normalized));
                }
            }
            State = PlayerActivityState.Started;   // the game's panel makes it Running on its next frame
            Done = 0;
            _begun = true;
            Host.Body.Bind(player);
            Host.Body.Hold();
            Begin(player);
            // A short routine is watched through; a long one is watched for a few seconds and then the game's own
            // time skip takes the rest. Never out of doors: the time skip clears the street of traffic.
            SkipAt = Total > WatchedWhole && BuildingManager.IsInsideBuilding ? Time.unscaledTime + SkipAfter : -1f;
        }

        public LabelInfo GetHeadlineLabel()
        {
            return new LabelInfo(Title, InstanceBehavior<GlobalReferences>.Instance.colors.white);
        }

        public LabelInfo[] GetLabels()
        {
            return State == PlayerActivityState.NotStarted ? Labels() : null;
        }

        public ButtonInfo[] GetButtons()
        {
            switch (State)
            {
                case PlayerActivityState.NotStarted:
                    return new[]
                    {
                        new ButtonInfo("CancelHomeRoutine", "common_cancel", "gray", Cancel, PlayerAction.Cancel),
                        new ButtonInfo("StartHomeRoutine", StartKey, "blue", Start, PlayerAction.Confirm)
                    };
                case PlayerActivityState.Running:
                    return new[] { new ButtonInfo("StopHomeRoutine", "reallife_home_stop", "gray", Finish, PlayerAction.Cancel) };
                default:
                    return null;
            }
        }

        public bool HasFastForward() => State == PlayerActivityState.Running;
        public bool HasTimeMachine() => State == PlayerActivityState.Running;
        public int GetRemainingMinutesForTimeMachine() => Mathf.Max(0, Total - Done);
        public bool HasProgressBar() => State == PlayerActivityState.Running;
        public float GetProgressBarPercentageValue() => 100f * Done / Mathf.Max(1, Total);

        public (string key, object arguments) GetProgressBarLabel()
        {
            int left = Mathf.Max(0, Total - Done);
            return ("playeractivityui_time_remaining", new { hours = left / 60, minutes = left % 60 });
        }

        public bool HasSlider() => State == PlayerActivityState.NotStarted && SliderHigh > SliderLow;
        public int GetMinSliderValue() => SliderLow;
        public int GetMaxSliderValue() => SliderHigh;
        public float GetCurrentSliderValue() => SliderNow;
        public void OnSliderValueChanged(int value)
        {
            if (SliderHigh > SliderLow)   // the panel sets its slider up for every activity, with or without one
            {
                Slide(Mathf.Clamp(value, SliderLow, SliderHigh));
            }
        }
        public (string key, object arguments) GetSliderInfo() => SliderText();

        // ---------------------------------------------------------------- what routines share

        /// <summary>One of the mod's clips by id, if the bundle has it (asked quietly: most of these are still on the wish-list).</summary>
        protected bool Clip(string id)
        {
            return ClipLibrary.Has(id) && Host.Body.Clip(id);
        }

        protected static LabelInfo Line(string key, object arguments = null)
        {
            return new LabelInfo(key, arguments, InstanceBehavior<GlobalReferences>.Instance.colors.white);
        }

        /// <summary>One of the game's one-off gestures on the player (a trigger): harmless if their animator has none such.</summary>
        protected static void Gesture(PlayerController player, AnimationType gesture)
        {
            if (player != null && player.Character != null && player.Character.animator != null)
            {
                player.Character.animator.SetTrigger(gesture);
            }
        }

        protected static void Toast(NotificationType type, string key, string name = null, string value = null, string name2 = null, string value2 = null)
        {
            Dictionary<string, string> data = null;
            if (name != null)
            {
                data = new Dictionary<string, string>(2) { { name, value } };
                if (name2 != null)
                {
                    data[name2] = value2;
                }
            }
            Notifications.Show(type, key, data, 8f);
        }
    }

    // ==================================================================== cooking

    /// <summary>
    /// A meal cooked at a cooker in the player's home from what is in their fridge, and eaten.
    /// The slider picks the recipe. The food leaves the fridge when the cooking starts; taking
    /// the pan off early wastes it. More filling than its ingredients eaten cold, counted as a
    /// home-cooked meal for the diet, and it can burn.
    /// </summary>
    public sealed class CookActivity : HomeActivity
    {
        private readonly ItemController _cooker;
        private int _recipe;
        private int _saturation;
        private float _nextStir;
        private bool _clip;

        public CookActivity(HomeDirector host, ItemController cooker) : base(host)
        {
            _cooker = cooker;
            int portions = HomePlace.Portions();
            _recipe = portions >= 2 ? 2 : portions >= 1 ? 1 : 0;
            Total = HomeBalance.Recipes[_recipe].Minutes;
        }

        public override Doing Kind => Doing.HomeRoutine;
        protected override string Title => "reallife_home_cook_title";
        protected override string StartKey => "reallife_home_cook_start";
        protected override EntityController Furniture => _cooker;
        protected override int SliderLow => HomeState.Leftovers > 0 ? 0 : 1;
        protected override int SliderHigh => HomeBalance.Recipes.Length - 1;
        protected override int SliderNow => _recipe;

        protected override void Slide(int value)
        {
            _recipe = value;
            Total = HomeBalance.Recipes[_recipe].Minutes;
        }

        protected override (string, object) SliderText()
        {
            Recipe recipe = HomeBalance.Recipes[_recipe];
            return ("reallife_home_cook_slider", new
            {
                recipe = JobText.T(recipe.Name),
                minutes = recipe.Minutes,
                portions = recipe.Portions,
                have = HomePlace.Portions(),
                leftovers = HomeState.Leftovers
            });
        }

        protected override LabelInfo[] Labels()
        {
            return new[] { Line("reallife_home_cook_info") };
        }

        protected override bool Ready(PlayerController player)
        {
            Recipe recipe = HomeBalance.Recipes[_recipe];
            if (recipe.Portions == 0 ? HomeState.Leftovers <= 0 : HomePlace.Portions() < recipe.Portions)
            {
                HomeDirector.Refuse("reallife_home_refused_no_food", Title);
                return false;
            }
            return true;
        }

        protected override void Begin(PlayerController player)
        {
            Recipe recipe = HomeBalance.Recipes[_recipe];
            if (recipe.Portions == 0)
            {
                HomeState.Leftovers = Mathf.Max(0, HomeState.Leftovers - 1);
                _saturation = HomeBalance.LeftoverSaturation;
            }
            else
            {
                _saturation = Mathf.RoundToInt(HomePlace.Take(recipe.Portions) * recipe.Yield);
            }
            _clip = Clip("cook_stir");
            if (!_clip)
            {
                Gesture(player, AnimationType.UsingProducer);
                _nextStir = Time.time + 4f;
            }
        }

        protected override void Minute(PlayerController player, int minutes)
        {
            if (!_clip && Time.time >= _nextStir)
            {
                _nextStir = Time.time + 4f;
                Gesture(player, AnimationType.UsingProducer);
            }
        }

        protected override void End(PlayerController player, bool completed, int minutes)
        {
            Recipe recipe = HomeBalance.Recipes[_recipe];
            string meal = JobText.T(recipe.Name);
            HomeTidy.Change(-recipe.Mess * (completed ? 1f : 0.5f));
            if (!completed || _saturation <= 0)
            {
                Toast(NotificationType.Warning, "reallife_home_cook_wasted", "meal", meal);
                return;
            }
            GameInstance save = SaveGameManager.Current;
            float burn = recipe.Burn + (HomeState.Stress > HomeBalance.StressHigh || save.Energy < 20f ? 0.08f : 0f);
            bool burnt = Random.value < burn * (Settings.HomeEasy ? HomeBalance.Easy : 1f);
            int saturation = burnt ? Mathf.RoundToInt(_saturation * HomeBalance.BurntYield) : _saturation;
            Host.OwnMeal = burnt ? Meal.Plain : Meal.Home;   // the game's "food eaten" event comes straight back to the director
            Gesture(player, AnimationType.ConsumeFood);
            InstanceBehavior<GameManager>.Instance.ChangeHunger(saturation);
            if (burnt)
            {
                ModState.AddModifier(ModState.MoodDown);
                Toast(NotificationType.Warning, "reallife_home_cook_burnt", "meal", meal);
            }
            else
            {
                HomeState.Stress = HomeState.Clamp(HomeState.Stress - recipe.Calm, 0f, 100f);
                HomeState.Leftovers = Mathf.Min(HomeBalance.LeftoversMost, HomeState.Leftovers + Mathf.Max(0, recipe.Leftovers));
                if (recipe.Portions > 0)
                {
                    ModState.AddModifier(ModState.HomeCooked);
                }
                Toast(NotificationType.Success, recipe.Leftovers > 0 ? "reallife_home_cook_done_leftovers" : "reallife_home_cook_done", "meal", meal);
            }
            float poison = (burnt ? HomeBalance.PoisonFromBurnt : 0f) + (HomeState.Tidy < HomeBalance.MessLow ? HomeBalance.PoisonFromFilth : 0f);
            if (poison > 0f && Random.value < poison * (Settings.HomeEasy ? HomeBalance.Easy : 1f))
            {
                HomeHealth.Catch(Illness.FoodPoisoning, burnt ? "reallife_health_cause_burnt" : "reallife_health_cause_dirty_kitchen", false);
            }
        }
    }

    // ==================================================================== coffee

    /// <summary>A cup from a coffee machine in the player's home: a little energy (inside the game's own daily limit for food and drink), a little calm, three a day.</summary>
    public sealed class CoffeeActivity : HomeActivity
    {
        private readonly ItemController _machine;

        public CoffeeActivity(HomeDirector host, ItemController machine) : base(host)
        {
            _machine = machine;
            Total = 10;
        }

        public override Doing Kind => Doing.HomeRoutine;
        protected override string Title => "reallife_home_coffee_title";
        protected override string StartKey => "reallife_home_coffee_start";
        protected override EntityController Furniture => _machine;

        protected override LabelInfo[] Labels()
        {
            return new[] { Line("reallife_home_coffee_info", new { cups = HomeState.CoffeesToday, most = HomeBalance.CoffeesADay }) };
        }

        protected override void Begin(PlayerController player)
        {
            Gesture(player, AnimationType.UsingProducer);
        }

        protected override void End(PlayerController player, bool completed, int minutes)
        {
            if (!completed)
            {
                return;
            }
            HomeState.CoffeesToday++;
            Gesture(player, AnimationType.ConsumeDrink);
            if (HomeState.CoffeesToday > HomeBalance.CoffeesADay)
            {
                HomeState.Stress = HomeState.Clamp(HomeState.Stress + 4f, 0f, 100f);
                Toast(NotificationType.Warning, "reallife_home_coffee_too_many");
                return;
            }
            GameInstance save = SaveGameManager.Current;
            float room = EnergyHelper.MaxDailyEnergyGeneratedFromConsumables - save.EnergyGeneratedFromConsumables;
            float energy = Mathf.Min(HomeBalance.CoffeeEnergy, room);
            if (energy > 0f)
            {
                save.EnergyGeneratedFromConsumables += energy;
                EnergyHelper.GenerateEnergy(energy);
            }
            HomeState.Stress = HomeState.Clamp(HomeState.Stress - 3f, 0f, 100f);
            Toast(NotificationType.Success, energy > 0f ? "reallife_home_coffee_done" : "reallife_home_coffee_no_kick");
        }
    }

    // ==================================================================== tidying up

    /// <summary>Tidying the flat: the mop over the floor for as long as the slider says. Takes the game's own dirt off every cell of the home.</summary>
    public sealed class TidyActivity : HomeActivity
    {
        public TidyActivity(HomeDirector host) : base(host)
        {
            Total = Mathf.Clamp(Mathf.CeilToInt((100f - HomeTidy.Read()) / HomeBalance.TidyPerMinute / 5f) * 5, 15, 90);
        }

        public override Doing Kind => Doing.HomeRoutine;
        protected override string Title => "reallife_home_tidy_title";
        protected override string StartKey => "reallife_home_tidy_start";
        protected override float Floor => 0.8f;
        protected override int SliderLow => 10;
        protected override int SliderHigh => 90;

        protected override (string, object) SliderText()
        {
            int now = Mathf.RoundToInt(HomeTidy.Read());
            return ("reallife_home_tidy_slider", new { minutes = Total, now, then = Mathf.Min(100, now + Mathf.RoundToInt(Total * HomeBalance.TidyPerMinute)) });
        }

        protected override void Begin(PlayerController player)
        {
            if (!Clip("mop_floor") && Host.Body.Pose(PermanentAnimationType.Cleaning, PermanentAnimationType.CleaningIdle))
            {
                Host.Body.GameProp("Mop", player.Character.rightHand);
            }
        }

        protected override void End(PlayerController player, bool completed, int minutes)
        {
            if (minutes <= 0)
            {
                return;
            }
            HomeTidy.Change(minutes * HomeBalance.TidyPerMinute);
            Toast(NotificationType.Success, "reallife_home_tidy_done", "tidy", Mathf.RoundToInt(HomeTidy.Read()).ToString());
        }
    }

    // ==================================================================== floor exercises

    public enum FloorExercise
    {
        Stretch,
        PushUps,
        SitUps
    }

    /// <summary>
    /// Exercise that needs no machine, anywhere there is room: stretching, push-ups, sit-ups, with the
    /// animations the game's own gym uses. Fitness, strength, a calmer head and a sweaty shirt come from the
    /// minutes actually done (the director counts them); stretching also eases a bad back.
    /// </summary>
    public sealed class ExerciseActivity : HomeActivity
    {
        private readonly FloorExercise _what;
        private float _nextRep;
        private bool _clip;

        public ExerciseActivity(HomeDirector host, FloorExercise what) : base(host)
        {
            _what = what;
            Total = what == FloorExercise.Stretch ? 15 : 20;
        }

        public override Doing Kind => Doing.Exercise;
        public override bool RequiresEnergy() => true;
        // The mod's floor clips are CENTRED on the character's origin and lie along its forward axis: `pushup` from the
        // toes 0.79 m behind to the crown 0.90 m ahead (hands 0.25 m either side), `situp` from the head 0.70 m behind to
        // the feet 0.60 m ahead; `stretch` needs 0.8 m either side. The player is walked to the middle of a circle of
        // clear floor this wide and exercises where they stop, whichever way they face: 0.9 m covers all three, and
        // push-ups get 0.1 m more because the walk there may end a little short and the head is right at the rim.
        protected override float Floor => _what == FloorExercise.PushUps ? 1.0f : 0.9f;
        protected override int SliderLow => 10;
        protected override int SliderHigh => BuildingManager.IsInsideBuilding ? 60 : 30;
        protected override string StartKey => "reallife_home_exercise_start";

        protected override string Title
        {
            get
            {
                switch (_what)
                {
                    case FloorExercise.PushUps: return "reallife_home_exercise_pushups";
                    case FloorExercise.SitUps: return "reallife_home_exercise_situps";
                    default: return "reallife_home_exercise_stretch";
                }
            }
        }

        protected override (string, object) SliderText() => ("reallife_home_exercise_slider", new { minutes = Total });

        protected override void Begin(PlayerController player)
        {
            switch (_what)
            {
                case FloorExercise.PushUps:
                    _clip = Clip("pushup") || Host.Body.Pose(PermanentAnimationType.PushUpIdle);
                    break;
                case FloorExercise.SitUps:
                    _clip = Clip("situp") || Host.Body.Pose(PermanentAnimationType.Crunches);
                    break;
                default:
                    _clip = Clip("stretch") || Clip("tai_chi");
                    if (!_clip)
                    {
                        Gesture(player, AnimationType.StretchArms);
                    }
                    break;
            }
            _nextRep = Time.time + 5f;
        }

        protected override void Minute(PlayerController player, int minutes)
        {
            if (Time.time < _nextRep)
            {
                return;
            }
            _nextRep = Time.time + 5f;
            if (_what == FloorExercise.Stretch && !_clip)
            {
                Gesture(player, AnimationType.StretchArms);
            }
            else if (_what == FloorExercise.PushUps && !Host.Body.ClipAlive)
            {
                Gesture(player, AnimationType.PushUpJump);
            }
        }

        protected override void End(PlayerController player, bool completed, int minutes)
        {
            if (minutes <= 0)
            {
                return;
            }
            if (_what == FloorExercise.Stretch)
            {
                if (HomeState.Ill == Illness.BackPain)
                {
                    HomeHealth.Shorten(minutes * 20f);   // a quarter of an hour's stretching takes five hours off a bad back
                }
            }
            else
            {
                Gesture(player, AnimationType.SweatRemove);
            }
            Toast(NotificationType.Success, "reallife_home_exercise_done", "minutes", minutes.ToString(), "fitness", Mathf.RoundToInt(HomeState.Fitness).ToString());
        }
    }

    // ==================================================================== the doctor's waiting room

    /// <summary>The wait before a consultation at a hospital or clinic. Seen through, the doctor's conversation opens; walked out of, nothing is charged.</summary>
    public sealed class WaitingRoomActivity : HomeActivity
    {
        public WaitingRoomActivity(HomeDirector host) : base(host)
        {
            Total = 20 + Random.Range(0, 4) * 5;
        }

        public override Doing Kind => Doing.Resting;
        protected override string Title => "reallife_doctor_wait_title";
        protected override string StartKey => "reallife_doctor_wait_start";
        protected override float Floor => 0.6f;

        protected override LabelInfo[] Labels()
        {
            return new[] { Line("reallife_doctor_wait_info", new { minutes = Total, amount = JobText.Money(HomeBalance.ConsultationFee) }) };
        }

        protected override void Begin(PlayerController player)
        {
            Clip("queue_wait");
        }

        protected override void End(PlayerController player, bool completed, int minutes)
        {
            if (completed)
            {
                Host.Later(DoctorVisit.Open, 3);   // once the game's panel has closed
            }
        }
    }
}
