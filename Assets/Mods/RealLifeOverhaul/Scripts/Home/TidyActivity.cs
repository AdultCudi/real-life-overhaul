using BigAmbitions.Characters;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
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
}
