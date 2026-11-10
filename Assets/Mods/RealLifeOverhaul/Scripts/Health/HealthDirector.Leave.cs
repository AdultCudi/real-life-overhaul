using System;
using UI.Notification;

namespace RealLifeOverhaul.Health
{
    /// <summary>Sick leave at the player's job, the player's plan and the debt paid off: the presses of the pages, and what the working life asks.</summary>
    public sealed partial class HealthDirector
    {
        // What is true of a working day, for the leave rules: really ill by the mod's older rules or with a case of health's, a doctor's note, in hospital.
        private LeaveFacts FactsOf(int day)
        {
            HealthCase c = _state.Case;
            HealthStay st = _state.Stay;
            bool cased = c.Open && c.Began <= day;
            return new LeaveFacts
            {
                Ill = cased || (HealthBridges.WasIll != null && HealthBridges.WasIll(day)),
                Note = HealthBridges.DoctorNote != null && HealthBridges.DoctorNote(day),
                InHospital = st.Active && day >= st.From / 1440 && day <= st.Until / 1440,
                AtWorkInjury = cased && c.Work
            };
        }

        /// <summary>True if a working day not worked is excused as sick, by the leave rules. Asking changes nothing.</summary>
        public bool Covers(int day)
        {
            return Running && HealthLeave.Covers(_state.Leave, FactsOf(day), day, _state.Seed);
        }

        /// <summary>
        /// A working day at the player's job went by unworked (the working life says so once, before it asks whether the day is excused): the leave rules settle it, and
        /// the sick pay due for it is paid.
        /// </summary>
        public void DayMissed(int day, int dayPayCents, int hiredDay)
        {
            if (!Running)
            {
                return;
            }
            LeaveBook l = _state.Leave;
            LeaveFacts f = FactsOf(day);
            int noticed = l.Noticed;
            long cents = HealthLeave.Settle(l, f, day, _state.Seed, dayPayCents, hiredDay, HealthGame.DaysPerYear);
            if (cents > 0)
            {
                HealthGame.ReceiveCents(cents, HealthGame.SickPayKey);
                HealthGame.Tell(NotificationType.Info, "reallife_hc_note_sick_pay", HealthGame.Data("amount", HealthGame.Money(cents / 100), "used", l.PaidUsed.ToString(),
                    "days", HealthLeave.PaidDays(HealthGame.DaysPerYear).ToString()));
            }
            else if (l.Noticed > noticed)
            {
                HealthGame.Tell(NotificationType.Warning, "reallife_hc_note_seen_through", null);
            }
            else if (f.Ill && !l.LastCovered)
            {
                HealthGame.Tell(NotificationType.Warning, "reallife_hc_note_need_note", HealthGame.Data("days", HealthCatalogue.SelfCertDays.ToString()));
            }
            _revision++;
            if (Verbose)
            {
                Logger?.Info("Health check (the working day " + day + " not worked): " + (f.Ill ? "ill" : f.InHospital ? "in hospital" : l.ClaimDay == day ? "called in while well" : "no reason")
                             + ", " + (l.LastCovered ? "excused" : "not excused") + ", sick pay " + cents + " cents, paid days " + l.PaidUsed + " of "
                             + HealthLeave.PaidDays(HealthGame.DaysPerYear) + ": " + (cents <= dayPayCents ? "PASS" : "FAIL, more than the day's pay"));
            }
        }

        /// <summary>The player calls in sick for today. False if they did already or have no shift to miss.</summary>
        public bool CallInSick()
        {
            if (!Running || HealthBridges.ShiftOpen == null || !HealthBridges.ShiftOpen())
            {
                return false;
            }
            int day = HealthGame.Day;
            bool ok = HealthLeave.CallIn(_state.Leave, day, FactsOf(day).Ill, HealthGame.DaysPerYear);
            _revision++;
            return ok;
        }

        // ---- the plan

        /// <summary>Takes or changes the plan: the first month is paid now. False if it is the plan held or the cash is not there.</summary>
        public bool TakePlan(Plan plan, out int amount)
        {
            amount = 0;
            if (!Running)
            {
                return false;
            }
            bool ok = HealthDay.TakePlan(_state, plan, HealthGame.Day, HealthGame.Age, HealthGame.DaysPerYear, HealthGame.MonthDays, HealthGame.Level, _payPremium, out amount);
            _revision++;
            if (Verbose)
            {
                Logger?.Info("Health check (a plan asked for): " + plan + " at " + amount + " a month: " + (ok ? "taken, nothing that begins before day "
                             + (_state.Policy.Since + HealthCatalogue.WaitDays) + " is covered" : "refused") + ": PASS");
            }
            return ok;
        }

        public bool CancelPlan()
        {
            if (!Running || _state.Policy.Plan == Plan.None)
            {
                return false;
            }
            HealthCover.Cancel(_state.Policy);
            _revision++;
            return true;
        }

        /// <summary>Pays the whole medical debt off now. False without the cash.</summary>
        public bool PayOffDebt(out long amount)
        {
            amount = 0;
            if (!_read || !HealthDay.PayOff(_state, HealthGame.Day, _payDebt, out amount))
            {
                return false;
            }
            HealthBridges.Credit?.Invoke(3, (int)Math.Min(int.MaxValue / 2, amount));
            _revision++;
            return true;
        }

        /// <summary>What is owed for care, for the Finance app's list of debts: read with the option on or off, as it is collected either way.</summary>
        public bool DebtLine(out long owed, out int perDay, out int late)
        {
            owed = _read ? _state.Debt.Owed : 0;
            perDay = _read ? _state.Debt.PerDay : 0;
            late = _read ? _state.Debt.Late : 0;
            return owed > 0;
        }
    }
}
