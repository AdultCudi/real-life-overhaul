using BigAmbitions.Characters;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The floor exercises, which need no machine.</summary>
    public enum FloorExercise
    {
        Stretch,
        PushUps,
        SitUps
    }

    /// <summary>Stretching, push-ups or sit-ups on clear floor, with the game's gym animations. Fitness and calm come from the minutes done; stretching eases a bad back.</summary>
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
        // The mod's floor clips are centred on the character along its forward axis (push-ups 0.79 m behind to 0.90 m ahead,
        // sit-ups 0.70 m behind to 0.60 m ahead, stretching 0.8 m either side), and the player may face any way: 0.9 m of
        // clear floor covers all three, and push-ups get 0.1 m more, as the walk there may end a little short.
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
}
