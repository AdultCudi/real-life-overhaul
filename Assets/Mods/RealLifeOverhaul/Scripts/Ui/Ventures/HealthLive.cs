using Localizor;
using RealLifeOverhaul.Health;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Reads the Health pages from the director and carries every button to it: the same calls the app in the game's frame makes, the same notices. A page is written when the
    /// director's revision moves or the player pressed something; nothing a frame.
    /// </summary>
    public sealed class HealthLive : IVentSource, IHealthActions
    {
        public static readonly HealthLive Instance = new HealthLive();
        public const string ScreenId = "health";

        private readonly HealthFacts _facts = new HealthFacts();
        private readonly VentWords _words = new VentWords((key, english) => key.GetLocalization(), x => HealthGame.Money((long)x));
        private int _local;

        private HealthLive()
        {
        }

        public int Generation => (HealthDirector.Instance != null ? HealthDirector.Instance.Revision : 0) + _local;

        public void Opened(VentView view)
        {
        }

        public bool Back(VentView view) => false;

        public void Fill(VentModel m, VentView v)
        {
            HealthDirector d = HealthDirector.Instance;
            if (d == null || !d.Facts(_facts))
            {
                _facts.Ready = false;
            }
            HealthPages.Fill(m, v, _facts, this, _words);
        }

        public void Book()
        {
            HealthDirector d = HealthDirector.Instance;
            if (d != null && d.Book())
            {
                HealthAppointment a = d.State.Appointment;
                HealthGame.Tell(NotificationType.Info, "reallife_hc_note_booked", HealthGame.Data("day", a.Day.ToString(), "hour", a.Hour.ToString(), "place", a.Number + " " + a.Street));
            }
            else
            {
                HealthGame.Tell(NotificationType.Warning, "reallife_hc_note_book_failed", null);
            }
            _local++;
        }

        public void CancelBooking()
        {
            HealthDirector d = HealthDirector.Instance;
            if (d != null && d.CancelBooking())
            {
                HealthGame.Tell(NotificationType.Info, "reallife_hc_note_booking_cancelled", null);
            }
            _local++;
        }

        public void CallInSick()
        {
            HealthDirector d = HealthDirector.Instance;
            bool ok = d != null && d.CallInSick();
            HealthGame.Tell(ok ? NotificationType.Info : NotificationType.Warning, ok ? "reallife_hc_note_called_in" : "reallife_hc_note_call_in_failed", null);
            _local++;
        }

        public void TakePlan(int plan)
        {
            HealthDirector d = HealthDirector.Instance;
            Plan wanted = plan > 0 && plan < (int)Plan.Count ? (Plan)plan : Plan.None;
            int amount = 0;
            bool ok = d != null && d.TakePlan(wanted, out amount);
            HealthGame.Tell(ok ? NotificationType.Info : NotificationType.Warning, ok ? "reallife_hc_note_plan_taken" : "reallife_hc_note_plan_refused",
                HealthGame.Data("plan", HealthText.PlanName(wanted), "amount", HealthGame.Money(amount), "days", HealthCatalogue.WaitDays.ToString()));
            _local++;
        }

        public void CancelPlan()
        {
            HealthDirector d = HealthDirector.Instance;
            if (d != null && d.CancelPlan())
            {
                HealthGame.Tell(NotificationType.Info, "reallife_hc_note_plan_cancelled", null);
            }
            _local++;
        }

        public void PayDebt()
        {
            HealthDirector d = HealthDirector.Instance;
            long amount = 0;
            bool ok = d != null && d.PayOffDebt(out amount);
            HealthGame.Tell(ok ? NotificationType.Success : NotificationType.Warning, ok ? "reallife_hc_note_debt_paid_off" : "reallife_hc_note_debt_no_cash",
                HealthGame.Data("amount", HealthGame.Money(amount)));
            _local++;
        }
    }
}
