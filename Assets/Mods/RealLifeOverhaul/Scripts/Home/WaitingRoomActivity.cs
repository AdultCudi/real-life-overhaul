using UI.Elements;
using UnityEngine;

namespace RealLifeOverhaul
{
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
