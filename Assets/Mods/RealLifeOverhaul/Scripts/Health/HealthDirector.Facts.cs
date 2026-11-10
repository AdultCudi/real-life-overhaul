using System;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Health
{
    /// <summary>What the pages read, filled in one call from the state and the game.</summary>
    public sealed partial class HealthDirector
    {
        /// <summary>Fills the facts of the pages. False while health is not running (the page then says so).</summary>
        public bool Facts(HealthFacts f)
        {
            f.Ready = Running;
            if (!f.Ready)
            {
                return false;
            }
            int day = HealthGame.Day, yearDays = HealthGame.DaysPerYear, level = HealthGame.Level;
            PopulationState people = HealthGame.People();
            f.Session = GoingPublic.InSession;
            f.People = people != null;
            f.Level = level;
            f.Day = day;
            f.YearDays = yearDays;
            f.MonthDays = HealthGame.MonthDays;
            f.Cash = HealthGame.Cash;
            f.Spare = HealthGame.Spare;

            HealthCase c = _state.Case;
            f.Kind = c.Kind;
            f.Serious = c.Serious;
            f.Treated = c.Treated;
            f.AtWork = c.Work;
            f.DaysLeft = c.Open && c.Until > 0 ? Math.Max(1, c.Until - day) : 0;
            f.Neglect = c.Neglect;
            f.InHospital = _state.Stay.Active;
            f.StayUntilDay = _state.Stay.Until / 1440;
            f.Wave = _wave;
            f.IllAtHome = _illAtHome;
            f.Lifestyle = HealthRules.Lifestyle(HealthGame.Factors());

            HealthAppointment a = _state.Appointment;
            f.Booked = a.Booked;
            f.BookedDay = a.Day;
            f.BookedHour = a.Hour;
            f.Clinic = a.Booked ? a.Number + " " + a.Street : string.Empty;
            f.CanBook = !a.Booked && HealthBridges.Clinic != null;

            HealthPolicy p = _state.Policy;
            f.Age = HealthGame.Age;
            f.Claims = HealthCover.ClaimsLastYear(p, day, yearDays);
            f.Plan = p.Plan;
            f.PolicySince = p.Since;
            f.NextDue = p.NextDue;
            f.Missed = p.Missed;
            f.Premium = HealthCover.Premium(p.Plan, f.Age, f.Claims, level);
            f.WaitUntil = p.Plan != Plan.None ? p.Since + HealthCatalogue.WaitDays : 0;
            f.PlanPaidYear = p.PaidYear;
            f.Employer = EmployerPlan(out int _);
            for (int i = 0; i < f.Plans.Length; i++)
            {
                f.Plans[i].Premium = HealthCover.Premium(f.Plans[i].Plan, f.Age, f.Claims, level);
            }

            f.BillsYear = _state.BillsYear;
            f.ClaimsYear = _state.ClaimsYear;
            f.LastBill = _state.LastBill;
            f.LastClaim = _state.LastClaim;
            f.LastBillDay = _state.LastBillDay;
            f.Owed = _state.Debt.Owed;
            f.DebtPaid = _state.Debt.Paid;
            f.DebtPerDay = _state.Debt.PerDay;
            f.DebtLate = _state.Debt.Late;
            LeaveBook l = _state.Leave;
            f.ShiftOpen = HealthBridges.ShiftOpen != null && HealthBridges.ShiftOpen();
            f.CalledIn = l.ClaimDay == day;
            f.PaidUsed = l.PaidUsed;
            f.PaidDays = HealthLeave.PaidDays(yearDays);
            f.SpellDays = HealthLeave.SpellBefore(l, day + 1);
            f.NoteUntil = HealthLeave.Noted(l, day) ? l.NoteUntil : 0;
            f.CalledWell = l.Claims;
            f.Noticed = l.Noticed;
            f.SickPay = l.PaidMoney;

            PopHealth o = _state.Pop;
            f.IllNow = people != null ? o.IllNow : 0;
            f.CityPeople = people != null ? people.People.Live : 0;
            f.Cases = o.Cases;
            f.SeriousCases = o.Serious;
            f.LostDays = o.LostDays;
            f.CityBills = o.Bills;
            f.CityDebt = o.Debt;
            f.StaffOff = StaffOff(day);
            f.SickPolicy = HealthBridges.SickPolicy != null ? HealthBridges.SickPolicy() : 1;
            f.RivalLine = string.Empty;
            for (int i = 0; i < _state.RivalCases.Length && f.RivalLine.Length == 0; i++)
            {
                f.RivalLine = RivalLine(i);
            }
            return true;
        }
    }
}
