using System;
using System.Collections.Generic;
using RealLifeOverhaul.Health;
using RealLifeOverhaul.Ui;

// The Health app's samples: somebody well, ill, seriously ill and in hospital; the plans with one held; a year's bills with a debt and the sick leave; the city in the
// flu's weeks. The rich list name is one of the game's own. Names here are placeholders and never ship.
internal static partial class UiSamples
{
    public static readonly string[] HealthStates =
    {
        "you", "you_ill", "you_serious", "you_hospital", "you_booked", "cover", "cover_held", "cover_broke", "bills", "bills_debt", "bills_shift", "city", "city_session", "off"
    };

    /// <summary>A recording stand-in for the director: each press says which action it was.</summary>
    public sealed class HealthStub : IHealthActions
    {
        public string Last;
        public int Count;
        public int Plan;

        private void Did(string what, int plan = 0)
        {
            Last = what;
            Count++;
            Plan = plan;
        }

        public void Book() => Did("book");
        public void CancelBooking() => Did("cancel_booking");
        public void CallInSick() => Did("call_in");
        public void TakePlan(int plan) => Did("take", plan);
        public void CancelPlan() => Did("cancel_plan");
        public void PayDebt() => Did("pay_debt");
    }

    public static HealthFacts HealthFactsOf(string state)
    {
        var f = new HealthFacts { Level = 1000, Day = 64, YearDays = 60, MonthDays = 5, Cash = 42000, Spare = 38000 };
        if (state == "off")
        {
            f.Ready = false;
            return f;
        }
        f.Ready = true;
        f.People = true;
        f.Age = 34;
        f.Wave = 700;
        f.IllAtHome = 1;
        f.Lifestyle = 750;
        f.CanBook = true;
        f.PaidDays = HealthLeave.PaidDays(60);
        for (int i = 0; i < f.Plans.Length; i++)
        {
            f.Plans[i].Premium = HealthCover.Premium(f.Plans[i].Plan, f.Age, 0, 1000);
        }
        f.IllNow = 1480;
        f.CityPeople = 56300;
        f.Cases = 2210;
        f.SeriousCases = 61;
        f.LostDays = 3120;
        f.CityBills = 248000;
        f.CityDebt = 31400;
        f.StaffOff = 2;
        f.SickPolicy = 1;
        f.RivalLine = "Space Karen is off sick with the flu and back on day 67.";
        switch (state)
        {
            case "you_ill":
                f.Kind = Condition.Flu;
                f.Neglect = 1;
                f.Lifestyle = 2000;
                break;
            case "you_serious":
                f.Kind = Condition.Flu;
                f.Serious = true;
                f.Neglect = 4;
                break;
            case "you_hospital":
                f.Kind = Condition.BrokenArm;
                f.Serious = true;
                f.InHospital = true;
                f.StayUntilDay = 65;
                break;
            case "you_booked":
                f.Kind = Condition.Sprain;
                f.DaysLeft = 3;
                f.Booked = true;
                f.BookedDay = 65;
                f.BookedHour = 9;
                f.Clinic = "12 Park Avenue";
                f.CanBook = false;
                break;
            case "cover_held":
                f.Plan = Plan.Standard;
                f.PolicySince = 40;
                f.NextDue = 65;
                f.Premium = f.Plans[1].Premium;
                f.WaitUntil = 43;
                f.Employer = Plan.Standard;
                break;
            case "cover_broke":
                f.Cash = 30;
                f.Spare = 30;
                break;
            case "bills_debt":
                f.BillsYear = 4400;
                f.ClaimsYear = 1900;
                f.LastBill = 2600;
                f.LastClaim = 1100;
                f.LastBillDay = 61;
                f.Owed = 1500;
                f.DebtPerDay = 150;
                f.DebtLate = 1;
                f.PaidUsed = 2;
                f.SickPay = 320;
                f.SpellDays = 2;
                f.NoteUntil = 66;
                f.CalledWell = 1;
                f.Noticed = 1;
                break;
            case "bills_shift":
                f.ShiftOpen = true;
                f.BillsYear = 125;
                f.LastBill = 80;
                f.LastBillDay = 60;
                break;
            case "city_session":
                f.Session = true;
                f.People = false;
                f.RivalLine = string.Empty;
                break;
        }
        return f;
    }

    public static VentModel Health(UiWords t, string state, VentView view = null, IHealthActions act = null)
    {
        VentWords w = VentWordsOf(t);
        HealthFacts f = HealthFactsOf(state);
        view = view ?? new VentView();
        if (state.StartsWith("cover", StringComparison.Ordinal))
        {
            view.Tab = HealthPages.TabCover;
        }
        else if (state.StartsWith("bills", StringComparison.Ordinal))
        {
            view.Tab = HealthPages.TabBills;
        }
        else if (state.StartsWith("city", StringComparison.Ordinal))
        {
            view.Tab = HealthPages.TabCity;
        }
        var m = new VentModel();
        HealthPages.Fill(m, view, f, act ?? new HealthStub(), w);
        return m;
    }
}
