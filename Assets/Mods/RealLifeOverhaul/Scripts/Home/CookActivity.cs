using BigAmbitions.Characters;
using UI.Elements;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>One dish from the cooking list, made at a cooker or the fridge and eaten: the food leaves the fridges on arrival, the rest is put by, and it can burn.</summary>
    public sealed class CookActivity : HomeActivity
    {
        private readonly ItemController _at;
        private readonly CookPlan _plan;
        private readonly string _name;
        private float _nextStir;
        private bool _clip;
        private readonly bool _together;   // cooked with the partner (CookingTogether): a portion is theirs

        public CookActivity(HomeDirector host, ItemController at, CookPlan plan) : base(host)
        {
            _at = at;
            _plan = plan;
            _name = JobText.T(plan.NameKey);
            Total = plan.Minutes;
            _together = CookingTogether.Waiting;
        }

        public override Doing Kind => Doing.HomeRoutine;
        protected override string Title => "reallife_home_cook_title";
        protected override string StartKey => "reallife_home_cook_start";
        protected override EntityController Furniture => _at;

        protected override LabelInfo Headline()
        {
            return new LabelInfo(_name, InstanceBehavior<GlobalReferences>.Instance.colors.white, false);
        }

        protected override LabelInfo[] Labels()
        {
            return new[] { Line("reallife_home_cook_info") };
        }

        private bool StillThere => _plan.Reheat ? HomeState.Leftovers > 0 : HomePlace.Holds(_plan);

        protected override bool Ready(PlayerController player)
        {
            if (!StillThere)
            {
                HomeDirector.Refuse("reallife_home_refused_food_gone", Title, _plan.NameKey);
                return false;
            }
            return true;
        }

        protected override bool Prepare(PlayerController player)
        {
            if (_plan.Reheat)
            {
                if (HomeState.Leftovers <= 0)
                {
                    HomeDirector.Refuse("reallife_home_refused_food_gone", Title, _plan.NameKey);
                    return false;
                }
                HomeState.Leftovers--;
                return true;
            }
            if (!HomePlace.Take(_plan))
            {
                HomeDirector.Refuse("reallife_home_refused_food_gone", Title, _plan.NameKey);
                return false;
            }
            return true;
        }

        protected override void Begin(PlayerController player)
        {
            _clip = _plan.Cooker && Clip("cook_stir");
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
            Dish dish = _plan.Dish;
            float mess = dish != null ? dish.Mess * HomeBalance.BatchMess[_plan.Size] : 0f;
            HomeTidy.Change(-mess * (completed ? 1f : 0.5f));
            if (!completed)
            {
                Toast(NotificationType.Warning, "reallife_home_cook_stopped", "meal", _name);
                if (_together)
                {
                    CookingTogether.Finish(false);
                }
                return;
            }
            GameInstance save = SaveGameManager.Current;
            float easy = Settings.HomeEasy ? HomeBalance.Easy : 1f;
            bool worn = HomeState.Stress > HomeBalance.StressHigh || save.Energy < HomeBalance.WornEnergy;
            bool burnt = Random.value < Cooking.BurnChance(_plan, worn) * easy;
            bool full = Mathf.RoundToInt(save.Hunger) >= 100;   // where the game's own eating says "too stuffed"
            int fill = burnt ? Mathf.RoundToInt(_plan.Fill * HomeBalance.BurntYield) : _plan.Fill;
            if (!full)
            {
                Host.OwnMeal = burnt ? Meal.Plain : Meal.Home;   // the game's "food eaten" event comes straight back to the director
                Gesture(player, AnimationType.ConsumeFood);
                InstanceBehavior<GameManager>.Instance.ChangeHunger(fill);
            }
            // What was not eaten is put by: nothing of a burnt meal, everything when the player could not eat.
            int extra = burnt ? 0 : full ? _plan.Portions : _plan.Portions - 1;
            if (_together && extra > 0)
            {
                extra--;   // the partner's plate
            }
            int kept = extra > 0 ? HomeState.PutBy(extra, dish != null ? dish.Number : HomeState.LeftoverDish, _plan.Fill) : 0;
            HomeDirector.Logger?.Info("Home: " + _plan.NameKey + " done: " + (burnt ? "burnt, " : string.Empty) + (full ? "not eaten (full), " : "filled " + fill + ", ")
                                      + kept + " of " + extra + " portion(s) put by, leftovers now " + HomeState.Leftovers + ".");
            if (burnt)
            {
                ModState.AddModifier(ModState.MoodDown);
                Toast(NotificationType.Warning, full ? "reallife_home_cook_burnt_full" : "reallife_home_cook_burnt", "meal", _name);
            }
            else
            {
                if (dish != null)
                {
                    HomeState.Stress = HomeState.Clamp(HomeState.Stress - dish.Calm, 0f, 100f);
                    if (!full)
                    {
                        ModState.AddModifier(ModState.HomeCooked);
                    }
                }
                string key = full ? "reallife_home_cook_served_full" : kept > 0 ? "reallife_home_cook_served_leftovers" : "reallife_home_cook_served";
                Toast(NotificationType.Success, key, "meal", _name, "count", kept.ToString());
            }
            if (_together)
            {
                CookingTogether.Finish(true);   // burnt or not, it was cooked and eaten together
            }
            if (full)
            {
                return;
            }
            float poison = (burnt ? HomeBalance.PoisonFromBurnt : 0f) + (HomeState.Tidy < HomeBalance.MessLow ? HomeBalance.PoisonFromFilth : 0f);
            if (poison > 0f && Random.value < poison * easy)
            {
                HomeHealth.Catch(Illness.FoodPoisoning, burnt ? "reallife_health_cause_burnt" : "reallife_health_cause_dirty_kitchen", false);
            }
        }
    }
}
