using System;
using System.Collections;
using BigAmbitions.DayNightCycle;
using Helpers;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Health
{
    /// <summary>The player's own case: Home's illness followed, injuries, the day of neglect or rest, the doctor, the appointment, the stay in hospital and its bill.</summary>
    public sealed partial class HealthDirector
    {
        private const float SkipMost = 300f;   // real seconds one day of a stay may take before it is stopped

        private bool _stayRunning, _stayFaded, _stayResting;

        // ---- the case

        private static Condition HomeIllness()
        {
            int kind = HealthBridges.Illness != null ? HealthBridges.Illness() : 0;
            return kind > 0 && kind < (int)Condition.Sprain ? (Condition)kind : Condition.None;
        }

        // Home's illness is the case; one that Home says is over closes it, unless it has turned serious (then Home is held, see Holding).
        private void MirrorHome(int day)
        {
            HealthCase c = _state.Case;
            Condition home = HomeIllness();
            bool treated = home != Condition.None && HealthBridges.Treated != null && HealthBridges.Treated();
            if (home != Condition.None)
            {
                // the day it really began, by Home's own record: never the day health first saw it (cover bought before it must not wait on that day)
                HealthRules.Mirror(c, home, treated, day, HealthBridges.IllnessSince != null ? HealthBridges.IllnessSince() : 0);
            }
            else if (c.Open && !c.Serious && !HealthCatalogue.Is(HealthCatalogue.Injury, c.Kind))
            {
                c.Clear();
            }
            HealthGame.Drain(c.Open && c.Serious && !_state.Stay.Active);
            HealthGame.HoldHurt(c.Open && HealthCatalogue.Is(HealthCatalogue.Injury, c.Kind));
        }

        // The case is open through a day: kept in the leave book, so a working day settled later is judged by what was true on it, whichever of the working life's day and
        // health's timer comes first (the working life settles yesterday on the game's own new day event; health's timer, a second at a time, sees the day after that).
        private void ObserveCase(int last)
        {
            HealthLeave.ObserveCase(_state.Leave, _state.Case, _state.Stay.Active, last);
        }

        private void CaseDay(int day)
        {
            MirrorHome(day);
            HealthFactors f = HealthGame.Factors();
            HealthCase c = _state.Case;
            Condition was = c.Kind;
            CaseEvent ev = c.Open && !_state.Stay.Active ? HealthRules.CaseDay(c, f, _state.Seed, day) : CaseEvent.None;
            switch (ev)
            {
                case CaseEvent.TurnedSerious:
                    HealthGame.Tell(NotificationType.Error, "reallife_hc_note_serious", HealthGame.Data("illness", HealthText.Name(was)), OpenAppClick);
                    HealthGame.Drain(true);
                    break;
                case CaseEvent.Worse:
                    HealthGame.Tell(NotificationType.Warning, "reallife_hc_note_worse", HealthGame.Data("illness", HealthText.Name(was)), OpenAppClick);
                    break;
                case CaseEvent.Healed:
                    c.Clear();
                    HealthGame.Tell(NotificationType.Success, "reallife_hc_note_healed", HealthGame.Data("illness", HealthText.Name(was)));
                    break;
            }
            if (ev != CaseEvent.None && Verbose)
            {
                Logger?.Info("Health check (the case on day " + day + "): " + was + ", neglect " + c.Neglect + ": " + ev + ": PASS");
            }
            ObserveCase(day);
            if (c.Open || _state.Stay.Active)
            {
                return;   // one thing at a time
            }
            Condition caught = HealthRules.PlayerCatch(_state.Seed, day, _wave, _illAtHome, f);
            if (caught != Condition.None && HealthBridges.Catch != null
                && HealthBridges.Catch((int)caught, caught == Condition.Flu ? "reallife_hc_cause_wave" : "reallife_hc_cause_home"))
            {
                MirrorHome(day);
                if (Verbose)
                {
                    Logger?.Info("Health check (the day's draw on day " + day + "): the wave at " + _wave + ", " + _illAtHome + " ill at home, living at "
                                 + HealthRules.Lifestyle(f) + ": caught " + caught + ": PASS");
                }
            }
        }

        /// <summary>The player was hurt for so many hours (LifeEvents.Injure: a crash, a hit on foot, a mugging): from a day's worth it is an injury with a course.</summary>
        public void Hurt(int hours)
        {
            if (!Running)
            {
                return;
            }
            Condition injury = HealthRules.InjuryOf(_state.Seed, hours, _state.Injuries);
            if (injury == Condition.None)
            {
                return;
            }
            TakeInjury(injury, false);
        }

        /// <summary>A shift was worked at a trade with so much risk (0 none, 1 some, 2 high): now and then an accident, drawn once a day.</summary>
        public void ShiftWorked(int risk)
        {
            if (!Running || risk <= 0)
            {
                return;
            }
            Condition injury = HealthRules.WorkAccident(_state.Seed, HealthGame.Day, risk, HealthGame.Factors());
            if (injury != Condition.None)
            {
                TakeInjury(injury, true);
            }
        }

        private void TakeInjury(Condition injury, bool atWork)
        {
            int day = HealthGame.Day;
            if (!HealthRules.Injure(_state.Case, injury, _state.Seed, day, _state.Injuries++, atWork))
            {
                return;
            }
            HealthCase c = _state.Case;
            ObserveCase(day);
            HealthGame.HoldHurt(true);
            HealthGame.Drain(c.Serious && !_state.Stay.Active);
            HealthGame.Tell(c.Serious ? NotificationType.Error : NotificationType.Warning,
                c.Serious ? (atWork ? "reallife_hc_note_injury_work_serious" : "reallife_hc_note_injury_serious") : atWork ? "reallife_hc_note_injury_work" : "reallife_hc_note_injury",
                HealthGame.Data("illness", HealthText.Name(injury), "days", Math.Max(1, c.Until - day).ToString()), OpenAppClick);
            _revision++;
            if (Verbose)
            {
                Logger?.Info("Health check (an injury on day " + day + "): " + injury + (atWork ? ", at work" : string.Empty)
                             + (c.Serious ? ", needs the hospital" : ", heals by day " + c.Until) + ": PASS");
            }
        }

        // ---- the doctor (Home's consultation calls these through Wiring)

        /// <summary>The doctor's fee or the prescription was paid at the desk: the plan that covers it pays its part back.</summary>
        public void DoctorPaid(int amount)
        {
            if (!Running || amount <= 0)
            {
                return;
            }
            if (_state.Appointment.Kept)
            {
                _state.Appointment.Clear();
            }
            BillResult r = SettleBill(Care.Doctor, amount, true);
            if (r.Claim > 0)
            {
                HealthGame.Tell(NotificationType.Info, "reallife_hc_note_claim", HealthGame.Data("amount", HealthGame.Money(r.Claim), "bill", HealthGame.Money(r.Bill),
                    "plan", r.Employer ? HealthText.Fill("reallife_hc_plan_employer") : HealthText.PlanName(r.By)));
            }
        }

        /// <summary>The doctor prescribed or dressed it: the case is treated.</summary>
        public void DoctorTreated()
        {
            if (Running)
            {
                HealthRules.Treat(_state.Case, HealthGame.Day);
                ObserveCase(HealthGame.Day);
                _revision++;
            }
        }

        /// <summary>The minutes in the waiting room: with an appointment kept on its hour, a few.</summary>
        public int WaitMinutes(int usual)
        {
            if (!Running || !HealthDay.InWindow(_state.Appointment, HealthGame.Day, HealthGame.Minutes % 1440))
            {
                return usual;
            }
            _state.Appointment.Kept = true;
            _revision++;
            return Math.Min(usual, HealthCatalogue.AppointmentWait);
        }

        /// <summary>True if the case needs the hospital and the player is not in it: the doctor then admits instead of prescribing.</summary>
        public bool NeedsAdmission => Running && _state.Case.Open && _state.Case.Serious && !_state.Stay.Active;

        /// <summary>The stay the doctor would admit for: its days, its bill and what the plan would pay of it, as today's figures say.</summary>
        public void AdmissionQuote(out Condition kind, out int days, out long bill, out long pays)
        {
            HealthCase c = _state.Case;
            kind = c.Kind;
            days = HealthRules.StayDays(c.Kind);
            bill = HealthCover.StayBill(days, false, HealthGame.Level);
            Plan employer = EmployerPlan(out int since);
            HealthPolicy p = _state.Policy;
            long own = HealthCover.Covers(p.Plan, p.Since, c.Began) ? HealthCover.Claim(p.Plan, Care.Hospital, bill, p.PaidYear, HealthGame.Level) : 0L;
            long theirs = c.Work ? bill : HealthCover.EmployerCovers(employer, since, c.Began) ? HealthCover.Claim(employer, Care.Hospital, bill, 0L, HealthGame.Level) : 0L;
            pays = Math.Max(own, theirs);
        }

        // ---- the appointment

        /// <summary>Books the next hour the surgery has at one of the city's hospitals or clinics. False if one is booked already or the city has none.</summary>
        public bool Book()
        {
            if (!Running || _state.Appointment.Booked || HealthBridges.Clinic == null || !HealthBridges.Clinic(out string street, out int number))
            {
                return false;
            }
            bool ok = HealthDay.Book(_state.Appointment, HealthGame.Day, HealthGame.Hour, street, number);
            _revision++;
            return ok;
        }

        public bool CancelBooking()
        {
            if (!Running || !_state.Appointment.Booked)
            {
                return false;
            }
            _state.Appointment.Clear();
            _revision++;
            return true;
        }

        // An appointment not kept by the end of its window costs the missed fee, from the cash that is there, unless the player was in hospital (HealthDay.AppointmentDay).
        private void AppointmentLook(int day)
        {
            AppointmentResult r = HealthDay.AppointmentDay(_state.Appointment, day, HealthGame.Minutes % 1440, _state.Stay.Active || HealthGame.InHospitalNight);
            if (r == AppointmentResult.Fee)
            {
                int fee = HealthRules.At(HealthCatalogue.MissedFee, HealthGame.Level);
                bool paid = HealthGame.Pay(fee, HealthGame.MissedKey);
                HealthGame.Tell(NotificationType.Warning, paid ? "reallife_hc_note_missed" : "reallife_hc_note_missed_unpaid", HealthGame.Data("amount", HealthGame.Money(fee)));
            }
            if (r != AppointmentResult.None)
            {
                _revision++;
            }
        }

        // ---- bills

        private Plan EmployerPlan(out int since)
        {
            int plan = HealthBridges.EmployerPlan != null ? HealthBridges.EmployerPlan() : 0;
            since = HealthBridges.EmployerSince != null ? HealthBridges.EmployerSince() : 0;
            return plan > 0 && plan < (int)Plan.Count ? (Plan)plan : Plan.None;
        }

        // A bill is met (HealthDay.Bill): the plan's part, the cash that is there, and the rest owed. A bill paid at the desk already only brings the plan's part back.
        private BillResult SettleBill(Care care, long bill, bool prepaid)
        {
            Plan employer = EmployerPlan(out int since);
            long owedBefore = _state.Debt.Owed;
            BillResult r = HealthDay.Bill(_state, care, bill, prepaid, employer, since, HealthGame.Day, HealthGame.DaysPerYear, HealthGame.MonthDays, HealthGame.Level,
                HealthGame.Spare, _payBill);
            if (prepaid)
            {
                HealthGame.Refund(r.Claim);
            }
            if (r.Owed > 0)
            {
                HealthBridges.Credit?.Invoke(0, (int)Math.Min(int.MaxValue / 2, r.Owed));
            }
            _revision++;
            if (Verbose)
            {
                Logger?.Info("Health check (a bill of " + r.Bill + ", " + care + "): the plan " + r.By + (r.Employer ? " of the employer" : string.Empty) + " paid " + r.Claim + ", the player "
                             + r.Paid + ", owed " + r.Owed + " (debt " + owedBefore + " to " + _state.Debt.Owed + "): "
                             + (r.Claim <= r.Bill && r.Claim + r.Paid + r.Owed == r.Bill ? "PASS" : "FAIL, the parts do not make the bill"));
            }
            return r;
        }

        // ---- the game's own night

        // The game puts the player in hospital for a night (a collapse, the ambulance, a crash) and takes its own bill at the end. Once it is over the plan pays its part of
        // that bill back, and a serious case stays in for the rest of its days.
        private void GameNights()
        {
            int stays = HealthGame.GameStays;
            if (_state.GameStays < 0)
            {
                _state.GameStays = stays;
            }
            if (stays > _state.GameStays)
            {
                _pendingNights += stays - _state.GameStays;
                _state.GameStays = stays;
            }
            if (_pendingNights <= 0 || EnergyHelper.goingToHospital)
            {
                return;
            }
            _pendingNights = 0;
            if (_state.Stay.Active)
            {
                return;   // a night inside a stay of health's own is in that stay's bill
            }
            MirrorHome(HealthGame.Day);
            BillResult r = HealthDay.GameNight(_state, HealthGame.Minutes, HealthGame.Day, _settleNight, out bool admitted);
            ObserveCase(HealthGame.Day);
            if (r.Claim > 0)
            {
                HealthGame.Tell(NotificationType.Info, "reallife_hc_note_claim", HealthGame.Data("amount", HealthGame.Money(r.Claim), "bill", HealthGame.Money(r.Bill),
                    "plan", r.Employer ? HealthText.Fill("reallife_hc_plan_employer") : HealthText.PlanName(r.By)));
            }
            if (admitted)
            {
                BeginStay();
            }
            _revision++;
        }

        // ---- the stay

        /// <summary>The player is admitted for the serious case. With <paramref name="gameNight"/> the first night was the game's own.</summary>
        public bool Admit(bool gameNight)
        {
            if (!Running || !HealthDay.Admit(_state, HealthGame.Minutes, gameNight))
            {
                return false;
            }
            ObserveCase(HealthGame.Day);
            BeginStay();
            return true;
        }

        // The stay begins: the drain stops, and the days pass, or the stay is over already.
        private void BeginStay()
        {
            HealthGame.Drain(false);
            _revision++;
            if (HealthDay.StayOver(_state.Stay, HealthGame.Minutes))
            {
                Discharge();
                return;
            }
            _stayRunning = true;
            StartCoroutine(Guard.Routine(StayRoutine()));
        }

        // A save loaded in the middle of a stay: it goes on, or ends if its time has passed.
        private void ResumeStay()
        {
            if (HealthDay.StayOver(_state.Stay, HealthGame.Minutes))
            {
                Discharge();
                return;
            }
            if (LifeEvents.IsSafeForEvent())
            {
                _stayRunning = true;
                StartCoroutine(Guard.Routine(StayRoutine()));
            }
        }

        // The screen goes dark and the clock moves on by the game's own time skip, a day at a time, to the morning of the discharge. The days really pass: the businesses
        // run on, bills fall due, the court sits. In a Going Public session the clock is everyone's and is not moved: the stay is served at once. The finally puts the game
        // back whatever happens.
        private IEnumerator StayRoutine()
        {
            try
            {
                float waited = 0f;
                while (!LifeEvents.IsSafeForEvent() && waited < 20f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (!LifeEvents.IsSafeForEvent() || uis == null || uis.timeMachine == null || SaveGameManager.Current == null)
                {
                    yield break;   // tried again by the next look
                }
                if (!GoingPublic.ClockShared)
                {
                    _stayFaded = true;
                    yield return UiFader.Fade(0.7f, "reallife_hc_stay_fade");
                    _stayResting = true;
                    EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.Hospital);
                    for (int round = 0; HealthDay.NextSkip(_state.Stay, HealthGame.Minutes, round, out int target); round++)
                    {
                        int before = HealthGame.Minutes;
                        uis.timeMachine.StartTimeMachine(new Timestamp((float)target), disableCancel: true, "reallife_hc_stay_info");
                        waited = 0f;
                        float nextNeeds = 0f;
                        while (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning && waited < SkipMost)
                        {
                            waited += Time.unscaledDeltaTime;
                            if (waited >= nextNeeds)
                            {
                                nextNeeds = waited + 0.25f;
                                HealthGame.HoldNeeds();
                            }
                            yield return null;
                        }
                        if (HealthGame.Minutes <= before)
                        {
                            break;   // the clock did not move: no second try
                        }
                        yield return null;
                    }
                }
                if (_state.Stay.Active)
                {
                    Discharge();   // (a clock that would not move: discharged with the days billed as far as they went)
                }
            }
            finally
            {
                EndStay();
            }
        }

        // Puts back whatever the stay's routine had changed. Safe more than once.
        private void EndStay()
        {
            try
            {
                _stayRunning = false;
                if (_stayResting)
                {
                    _stayResting = false;
                    EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.None);
                }
                if (_stayFaded)
                {
                    _stayFaded = false;
                    if (isActiveAndEnabled)
                    {
                        StartCoroutine(UiFader.UnFade());
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The discharge: the bill for the days (the plan's part, the cash, the rest owed), the case treated, Home's illness cured, a note for the days and two after.
        private void Discharge()
        {
            HealthStay st = _state.Stay;
            if (!st.Active)
            {
                return;
            }
            int day = HealthGame.Day, days = st.Days;
            Condition kind = st.Kind;
            BillResult? released = HealthDay.Release(_state, HealthGame.Minutes, day, GoingPublic.ClockShared, HealthGame.Level, _settleStay, out bool illness);
            if (released == null)
            {
                return;
            }
            BillResult r = released.Value;
            ObserveCase(day);
            if (illness)
            {
                HealthBridges.Cure?.Invoke();
            }
            HealthGame.Drain(false);
            HealthGame.HoldNeeds();
            string key = r.Owed > 0 ? "reallife_hc_note_discharged_owed" : "reallife_hc_note_discharged";
            HealthGame.Tell(r.Owed > 0 ? NotificationType.Warning : NotificationType.Info, key, HealthGame.Data("days", days.ToString(), "bill", HealthGame.Money(r.Bill),
                "plan", HealthGame.Money(r.Claim), "paid", HealthGame.Money(r.Paid), "owed", HealthGame.Money(r.Owed)), OpenAppClick);
            _revision++;
            if (Verbose)
            {
                Logger?.Info("Health check (discharged on day " + day + " after " + days + " day(s) for " + kind + "): bill " + r.Bill + ", plan " + r.Claim + ", paid " + r.Paid
                             + ", owed " + r.Owed + ", note to day " + _state.Leave.NoteUntil + ": " + (!_state.Stay.Active && !_state.Case.Serious ? "PASS" : "FAIL"));
            }
        }
    }
}
