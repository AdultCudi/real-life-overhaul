using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Meals
{
    /// <summary>
    /// The director's part for dancing to the music: "Dance" in the game's radio panel over a loudspeaker starts one of the
    /// game's own dances (the ones its dance list and the nightclub's floor use), and it ends when the player moves (the
    /// game's own rule), presses the button again, the music stops or they leave. Danced long enough, the mood lifts by
    /// the mod's own modifier, at most once in <see cref="MealsRules.DanceGapHours"/> game hours. Looked at once a second.
    /// </summary>
    public sealed partial class MealsDirector
    {
        private const float FirstLook = 1.5f;   // s: the animator is given this long to show the dance before it is read

        private bool _dancing;
        private int _lastDance = -1;
        private float _danceFrom;            // game minutes: when the dance began
        private float _danceSeen;            // real seconds: when it was started
        private float _lastLift = -1f;       // game minutes: the last lift this session; -1 none
        private bool _lifted;                // this dance has lifted the mood already
        private BuildingRegistration _danceIn;

        /// <summary>The player is dancing to the music, started from the radio panel.</summary>
        public bool DancingNow => _dancing;

        /// <summary>The radio panel's button: a dance to the music, or the dance stopped.</summary>
        public void DanceClicked()
        {
            try
            {
                if (_dancing)
                {
                    StopDancing("the button was pressed again", true);
                    return;
                }
                if (!Settings.MealsDance || MealsGame.Loading())
                {
                    return;
                }
                PlayerController player = MealsGame.Player();
                if (player == null || player.Character == null)
                {
                    return;
                }
                if (!MealsGame.OwnPlace() || !MealsGame.MusicPlaying())
                {
                    MealsGame.Notice(NotificationType.Warning, MealsText.NoMusic);
                    return;
                }
                if (_step != Step.None || !MealsGame.CanDance())
                {
                    MealsGame.Notice(NotificationType.Warning, MealsText.CannotDance);
                    DanceSay("the game's dances are not open now (hands full, a vehicle, held or at a meal): not started", true);
                    return;
                }
                if (MealsGame.TooTired(player))
                {
                    MealsGame.InsufficientEnergy();   // the game's own refusal, as its dance list gives it
                    return;
                }
                int dance = MealsRules.NextDance(_lastDance, Random.value);   // what this machine shows: nothing shared depends on it
                MealsGame.StartDance(player, dance);
                _lastDance = dance;
                _dancing = true;
                _lifted = false;
                _danceFrom = MealsGame.NowMinutes();
                _danceSeen = Time.unscaledTime;
                _danceIn = MealsGame.Building();
                Logger?.Info("Meals: dancing to the music (the game's dance " + (dance + 1) + ").");
            }
            catch (System.Exception e)
            {
                Logger?.Error(e);
                StopDancing("a fault", true);
            }
        }

        // Once a second while dancing: what ends it, and the lift.
        private void DanceSlow(PlayerController player)
        {
            if (!_dancing || Time.unscaledTime - _danceSeen < FirstLook)
            {
                return;
            }
            if (player == null || !MealsGame.Dancing(player))
            {
                StopDancing("the game ended it: the player moved, ran out of energy or was taken up by something", false);
                return;
            }
            if (!Settings.MealsDance)
            {
                StopDancing("its option was switched off", true);
                return;
            }
            if (!ReferenceEquals(MealsGame.Building(), _danceIn))
            {
                StopDancing("the player left", true);
                return;
            }
            if (!MealsGame.MusicPlaying())
            {
                StopDancing("the music stopped", true);
                return;
            }
            float now = MealsGame.NowMinutes();
            if (!_lifted && MealsRules.Lifts(now - _danceFrom, now, _lastLift))
            {
                _lifted = true;
                _lastLift = now;
                ModState.AddModifier(ModState.MoodUp);
                MealsGame.Notice(NotificationType.Success, MealsText.Lifted);
                DanceSay("danced " + (now - _danceFrom).ToString("0", Inv) + " game minutes to the music, wanted at least " + MealsRules.DanceAtLeast
                         + ": the mood lifts, and not again for " + MealsRules.DanceGapHours + " game hours", true);
            }
        }

        /// <summary>Ends dancing to the music; with <paramref name="stopGame"/> the game's own dance is stopped too. Safe when not dancing.</summary>
        public void StopDancing(string why, bool stopGame)
        {
            if (!_dancing)
            {
                return;
            }
            _dancing = false;
            _danceIn = null;
            if (stopGame)
            {
                try
                {
                    MealsGame.StopDance();
                }
                catch (System.Exception e)
                {
                    Logger?.Error(e);
                }
            }
            DanceSay("the dance ended (" + why + ") after " + (MealsGame.Loading() ? "a load" : (MealsGame.NowMinutes() - _danceFrom).ToString("0", Inv) + " game minutes")
                     + "; the mood lifted " + (_lifted ? "once" : "not this time"), true);
        }

        private static void DanceSay(string what, bool pass)
        {
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Meals check (dance): " + what + ": " + (pass ? "PASS" : "FAIL") + ".");
            }
        }
    }
}
