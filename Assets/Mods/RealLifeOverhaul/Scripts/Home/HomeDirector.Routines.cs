using Extensions;
using Helpers;
using Parking.UndergroundParking;
using PlayerActivity;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The director's part that starts a routine: the refusals, the game's activity panel, cooking.</summary>
    public sealed partial class HomeDirector
    {
        private readonly System.Collections.Generic.List<PantryItem> _pantry = new System.Collections.Generic.List<PantryItem>(16);
        private HomeActivity _dropped;

        /// <summary>A toast and a log line for something that could not be started: one sentence per cause.</summary>
        public static void Refuse(string key, string what, string detail = null)
        {
            Logger?.Info("Home: '" + what + "' not started (" + key + (detail != null ? "; " + detail : string.Empty) + ").");
            Notifications.Show(NotificationType.Warning, key, null, 8f);
        }

        private void Act(HomeWish wish, bool test)
        {
            string what = wish.ToString();
            var gm = InstanceBehavior<GameManager>.Instance;
            PlayerController player = gm != null ? gm.playerController : null;
            if (!HomeState.Loaded || player == null || SaveGameManager.Current == null)
            {
                TestRefusals.Tell(Logger, "Home " + what, TestRefusal.NotLoaded);
                return;
            }
            if (!Settings.HomeLife || _broken)
            {
                TestRefusals.Tell(Logger, "Home " + what, TestRefusal.FeatureOff, 0, _broken ? "switched off after an error" : null);
                return;
            }
            if (wish == HomeWish.StopRun)
            {
                if (_run.Active)
                {
                    _run.End("reallife_home_run_ended_stopped");
                }
                return;
            }
            if (JobDirector.OnShift)
            {
                Refuse("reallife_home_refused_on_job", what);
                return;
            }
            if (PlayerHelper.IsUsingVehicle)
            {
                Refuse("reallife_home_refused_vehicle", what);
                return;
            }
            if (_run.Active)
            {
                Refuse("reallife_home_refused_running", what);
                return;
            }
            if (!Free(player))
            {
                Refuse("reallife_home_refused_busy", what);
                return;
            }
            Vector3 at = player.transform.position;
            float energy = SaveGameManager.Current.Energy;
            switch (wish)
            {
                case HomeWish.Cook:
                case HomeWish.CookFirst:
                {
                    if (!Settings.HomeRoutines)
                    {
                        TestRefusals.Tell(Logger, "Home " + what, TestRefusal.FeatureOff);
                        return;
                    }
                    if (!HomeTidy.AtHome())
                    {
                        Refuse("reallife_home_refused_not_home", what);
                        return;
                    }
                    HomePlace.Scan(true);
                    if (wish == HomeWish.Cook)
                    {
                        // The list says for itself what is missing: no fridge, nothing in it, no cooker.
                        string found = HomeCookList.Open(this);
                        if (found != null)
                        {
                            Logger?.Info("Home: cooking list opened: " + found);
                            return;
                        }
                        // The game's window could not be copied: the first dish the fridges allow is cooked.
                    }
                    HomePlace.ReadPantry(_pantry);
                    bool cooker = HomePlace.Cookers.Count > 0;
                    CookPlan first = cooker ? Cooking.Reheat(HomeState.Leftovers, HomeState.LeftoverDish, HomeState.LeftoverFill) : null;
                    for (int d = 0; first == null && d < HomeBalance.Dishes.Length; d++)
                    {
                        if (cooker || !HomeBalance.Dishes[d].Cooker)
                        {
                            first = Cooking.Plan(HomeBalance.Dishes[d], 0, _pantry);
                        }
                    }
                    if (first == null)
                    {
                        Refuse("reallife_home_refused_no_food", what, HomePlace.Fridges.Count + " fridge(s), " + HomePlace.Cookers.Count + " cooker(s)");
                        return;
                    }
                    Cook(first);
                    return;
                }
                case HomeWish.Coffee:
                {
                    if (!Settings.HomeRoutines)
                    {
                        TestRefusals.Tell(Logger, "Home " + what, TestRefusal.FeatureOff);
                        return;
                    }
                    if (!HomeTidy.AtHome())
                    {
                        Refuse("reallife_home_refused_not_home", what);
                        return;
                    }
                    HomePlace.Scan(true);
                    ItemController item = _clicked != null && HomePlace.CoffeeMachines.Contains(_clicked) ? _clicked : HomePlace.Nearest(HomePlace.CoffeeMachines, at, 200f);
                    if (item == null)
                    {
                        Refuse("reallife_home_refused_no_coffee_machine", what);
                        return;
                    }
                    Show(new CoffeeActivity(this, item));
                    return;
                }
                case HomeWish.Tidy:
                    if (!Settings.HomeRoutines)
                    {
                        TestRefusals.Tell(Logger, "Home " + what, TestRefusal.FeatureOff);
                    }
                    else if (!HomeTidy.AtHome())
                    {
                        Refuse("reallife_home_refused_not_home", what);
                    }
                    else if (HomeTidy.Read() >= 97f)
                    {
                        Refuse("reallife_home_refused_spotless", what);
                    }
                    else
                    {
                        Show(new TidyActivity(this));
                    }
                    return;
                case HomeWish.Stretch:
                case HomeWish.PushUps:
                case HomeWish.SitUps:
                    if (!Settings.HomeRoutines)
                    {
                        TestRefusals.Tell(Logger, "Home " + what, TestRefusal.FeatureOff);
                    }
                    else if (BuildingManager.IsInsideBuilding && !GoingPublic.Mine(InstanceBehavior<BuildingManager>.Instance.buildingRegistration))
                    {
                        Refuse("reallife_home_refused_not_here", what);
                    }
                    else if (wish != HomeWish.Stretch && HomeHealth.TooIllToTrain)
                    {
                        Refuse("reallife_home_refused_ill", what, HomeState.Ill.ToString());
                    }
                    else if (energy < 15f)
                    {
                        Refuse("reallife_home_refused_tired", what, "energy " + (int)energy);
                    }
                    else if (!CanPerform(player, wish))
                    {
                        Refuse("reallife_home_refused_no_animation", what);   // never a routine with nothing to see
                    }
                    else
                    {
                        Show(new ExerciseActivity(this, wish == HomeWish.PushUps ? FloorExercise.PushUps : wish == HomeWish.SitUps ? FloorExercise.SitUps : FloorExercise.Stretch));
                    }
                    return;
                case HomeWish.Run:
                    if (!Settings.HomeJog)
                    {
                        TestRefusals.Tell(Logger, "Home " + what, TestRefusal.FeatureOff);
                    }
                    else if (BuildingManager.IsInsideBuilding || UndergroundParkingManager.IsInsideParking)
                    {
                        TestRefusals.Tell(Logger, "Home " + what, TestRefusal.Indoors);
                    }
                    else if (HomeHealth.TooIllToTrain)
                    {
                        Refuse("reallife_home_refused_ill", what, HomeState.Ill.ToString());
                    }
                    else if (energy < 25f)
                    {
                        Refuse("reallife_home_refused_tired", what, "energy " + (int)energy);
                    }
                    else if (!_run.Begin(player, transform, out string refusal))
                    {
                        Refuse(refusal, what);
                    }
                    else
                    {
                        _nextRunTick = Time.time + 0.25f;
                    }
                    return;
                case HomeWish.Doctor:
                    if (!Settings.HomeIllness)
                    {
                        TestRefusals.Tell(Logger, "Home " + what, TestRefusal.FeatureOff);
                    }
                    else if (test)
                    {
                        DoctorVisit.Free = true;
                        DoctorVisit.Open();
                    }
                    else if (!HomePlace.InHospital())
                    {
                        Refuse("reallife_home_refused_not_hospital", what);
                    }
                    else if (SaveGameManager.Current.Money < HomeBalance.ConsultationFee)
                    {
                        Refuse("reallife_doctor_no_money_toast", what);
                    }
                    else
                    {
                        DoctorVisit.Free = false;
                        Show(new WaitingRoomActivity(this));
                    }
                    return;
            }
        }

        private bool CanPerform(PlayerController player, HomeWish wish)
        {
            Body.Bind(player);
            Animator animator = player.Character != null ? player.Character.animator : null;
            switch (wish)
            {
                case HomeWish.PushUps: return ClipLibrary.Has("pushup") || Body.Knows(BigAmbitions.Characters.PermanentAnimationType.PushUpIdle);
                case HomeWish.SitUps: return ClipLibrary.Has("situp") || Body.Knows(BigAmbitions.Characters.PermanentAnimationType.Crunches);
                default: return ClipLibrary.Has("stretch") || ClipLibrary.Has("tai_chi") || (animator != null && animator.HasParameter("StretchArms"));
            }
        }

        // Puts a routine on the game's own activity panel. The game may still say no (no energy for an exercise): then nothing is on.
        private void Show(HomeActivity activity, bool startForThePlayer = false)
        {
            bool panelBusy = PlayerActivityUI.IsPanelOpen;   // the game's Show then does nothing and says nothing
            PlayerActivityUI.Show(activity);
            var uis = InstanceBehavior<UIs>.Instance;
            _activity = uis != null && ReferenceEquals(uis.playerActivityUI.GetCurrentActivity, activity) ? activity : null;
            Logger?.Info("Home: " + activity.GetType().Name + (_activity == null ? " was turned down by the game."
                : startForThePlayer ? " on the game's panel; it starts by itself." : " offered."));
            if (_activity == null)
            {
                if (panelBusy)
                {
                    Refuse("reallife_home_refused_busy", activity.GetType().Name, "the game's activity panel was open");
                }
                return;   // otherwise the game has said why itself (no energy, in a vehicle)
            }
            if (startForThePlayer)
            {
                // Two frames on: the game's panel must have seen the routine waiting once before it sees it under
                // way, or it neither hides itself nor lets the clock run (it compares with the state it saw last).
                _starting = activity;
                Later(StartChosen, 2);
            }
        }

        private void StartChosen()
        {
            HomeActivity activity = _starting;
            _starting = null;
            var uis = InstanceBehavior<UIs>.Instance;
            if (activity != null && uis != null && ReferenceEquals(uis.playerActivityUI.GetCurrentActivity, activity))
            {
                activity.StartNow();
                activity.CallOff();   // it could not start after all and has said why: its panel is not left standing
            }
        }

        /// <summary>A routine the game's panel dropped as it set off is shown again and started for the player, who has said yes once.</summary>
        public void Again(HomeActivity activity)
        {
            _dropped = activity;
            Later(ShowAgain, 1);
        }

        private void ShowAgain()
        {
            HomeActivity activity = _dropped;
            _dropped = null;
            if (activity != null)
            {
                activity.ChangeState(PlayerActivityState.NotStarted);
                Show(activity, true);
            }
        }

        /// <summary>A dish chosen on the cooking list: it starts once the player is free, within 20 seconds, or they are told why not.</summary>
        public void Cook(CookPlan plan)
        {
            if (plan == null || _broken)
            {
                return;
            }
            _plan = plan;
            _planUntil = Time.unscaledTime + 20f;
        }

        // Per frame only while a dish is waiting to be started.
        private void TryCook(float now)
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            PlayerController player = gm != null ? gm.playerController : null;
            string what = _plan.NameKey;
            if (player == null || !Settings.HomeLife || !Settings.HomeRoutines)
            {
                _plan = null;
                return;
            }
            if (!Free(player) || _run.Active || _later != null)
            {
                if (now > _planUntil)
                {
                    _plan = null;
                    Refuse(_run.Active ? "reallife_home_refused_running" : "reallife_home_refused_busy", what, "waited 20 s");
                }
                return;
            }
            CookPlan plan = _plan;
            _plan = null;
            if (!HomeTidy.AtHome())
            {
                Refuse("reallife_home_refused_not_home", what);
                return;
            }
            HomePlace.Scan(true);
            Vector3 at = player.transform.position;
            ItemController where = HomePlace.Nearest(HomePlace.Cookers, at, 200f);
            if (!plan.Cooker)
            {
                where = HomePlace.NearestFridge(at, 200f) ?? where;   // put together at the fridge
            }
            if (where == null)
            {
                Refuse(plan.Cooker ? "reallife_home_refused_no_cooker" : "reallife_home_refused_no_fridge", what);
                return;
            }
            Show(new CookActivity(this, where, plan), true);
        }
    }
}
