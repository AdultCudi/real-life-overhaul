using System;
using System.Collections.Generic;
using RealLifeOverhaul.Insurance;
using RealLifeOverhaul.Ui;

// The Insurance app's samples: three cars (one insured, one not, one with a premium missed), the health plans, two homes, a business with its three kinds of cover and a
// history of claims. Every premium is worked out by the feature's own rules from made-up cars and homes. Names here are placeholders and never ship.
internal static partial class UiSamples
{
    public static readonly string[] InsuranceStates =
    {
        "cars", "cars_none", "cars_change", "cars_unsound", "cars_buy", "health", "health_off", "homes", "homes_none", "homes_buy", "business", "business_buy", "business_refused",
        "claims", "claims_open", "claims_none", "off"
    };

    public const string InsuranceCar = "car-1", InsuranceCarBare = "car-2", InsuranceHome = "Elm%20Street~12", InsuranceHomeBare = "Harbor%20Road~88", InsuranceShop = "Main%20Street~4";

    /// <summary>A recording stand-in for the director: each press says which action it was.</summary>
    public sealed class InsuranceStub : IInsureActions
    {
        public string Last, Id;
        public int Count, A, B;

        private void Did(string what, string id, int a, int b)
        {
            Last = what;
            Count++;
            Id = id;
            A = a;
            B = b;
        }

        public void BuyCar(string id, int tier) => Did("buy_car", id, tier, 0);
        public void ChangeCar(string id, int tier) => Did("change_car", id, tier, 0);
        public void CancelCar(string id) => Did("cancel_car", id, 0, 0);
        public void TakeHealth(int plan) => Did("take_health", null, plan, 0);
        public void CancelHealth() => Did("cancel_health", null, 0, 0);
        public void BuyHome(string place, int tier) => Did("buy_home", place, tier, 0);
        public void ChangeHome(string place, int tier) => Did("change_home", place, tier, 0);
        public void CancelHome(string place) => Did("cancel_home", place, 0, 0);
        public void BuyBusiness(string place, int kind, int insurer) => Did("buy_business", place, kind, insurer);
        public void CancelBusiness(string place, int kind) => Did("cancel_business", place, kind, 0);
        public void FileClaim(int id) => Did("file_claim", null, id, 0);
    }

    private static CarCoverFact InsuranceCarOf(string id, string name, bool insured, int tier, float garage, int grade, int points, int crashes, int months, int daysLeft, int monthDays)
    {
        var c = new CarCoverFact { Id = id, Name = name, Insured = insured, Tier = tier, DaysLeft = insured ? daysLeft : 0, NoClaim = InsuranceRules.NoClaim(months), Sound = true };
        for (int t = 0; t < InsuranceCatalogue.Tiers; t++)
        {
            c.Premium[t] = (int)InsuranceRules.CarPremium(garage, InsuranceRules.CarFactor(t, grade, points, crashes, months));
        }
        for (int t = 0; t < InsuranceCatalogue.Tiers; t++)
        {
            c.Change[t] = insured && t > tier ? InsuranceRules.UpgradeCharge(c.Premium[tier], c.Premium[t], daysLeft, monthDays) : 0;
        }
        return c;
    }

    private static HomeCoverFact InsuranceHomeOf(string place, string name, bool active, int tier, int sqm, int income, int claims, int months, int day, int nextDue, int monthDays)
    {
        var h = new HomeCoverFact { Place = place, Name = name, Active = active, Tier = tier, Sqm = sqm, NextDue = active ? nextDue : 0, NoClaim = InsuranceRules.NoClaim(months),
            District = InsuranceRules.HomeDistrict(income), Loading = Math.Min(InsuranceCatalogue.HomeLoadingMost, claims * InsuranceCatalogue.HomeClaimLoading) };
        for (int t = 0; t < InsuranceCatalogue.Tiers; t++)
        {
            h.Premium[t] = InsuranceRules.HomePremium(t, sqm, income, claims, months, 1000, monthDays);
        }
        for (int t = 0; t < InsuranceCatalogue.Tiers; t++)
        {
            h.Change[t] = active && t > tier ? InsuranceRules.UpgradeCharge(h.Premium[tier], h.Premium[t], nextDue - day, monthDays) : 0;
        }
        return h;
    }

    private static BizCoverFact InsuranceBizOf(int kind, string kindName, int insurer, int premium, bool refused)
    {
        var b = new BizCoverFact { Place = InsuranceShop, Name = "Corner Coffee", Kind = kind, KindName = kindName, Active = insurer >= 0, Insurer = insurer, Premium = premium, Until = 260,
            Claims = insurer >= 0 ? 1 : 0, Most = kind == 0 ? 20000 : kind == 1 ? 8000 : 5000 };
        int[] quotes = { 9, 11, 14 }, shares = { 300, 150, 50 }, least = { 200, 100, 50 }, days = { 6, 4, 2 };
        for (int n = 0; n < InsuranceCatalogue.Tiers; n++)
        {
            b.Quote[n] = quotes[n] + kind;
            b.Writes[n] = !refused;
            b.ExcessShare[n] = shares[n];
            b.ExcessLeast[n] = least[n];
            b.PayDays[n] = days[n];
        }
        return b;
    }

    public static InsuranceFacts InsuranceFactsOf(string state)
    {
        var f = new InsuranceFacts { Ready = state != "off", Day = 212, Level = 1000, MonthDays = 5, YearDays = 60, Cash = 42000, Grade = 2, Points = 3, Crashes = 1, CarsOn = true, BusinessOn = true };
        if (!f.Ready)
        {
            return f;
        }
        if (state != "cars_none")
        {
            CarCoverFact first = InsuranceCarOf(InsuranceCar, "Family hatchback", state != "cars_buy", 1, 64f, f.Grade, f.Points, f.Crashes, 2, 3, f.MonthDays);
            first.PaidYear = 640;
            first.ClaimsYear = 1;
            first.Sound = state != "cars_unsound";
            f.Cars.Add(first);
            f.Cars.Add(InsuranceCarOf(InsuranceCarBare, "Pickup truck", false, 1, 40f, f.Grade, f.Points, f.Crashes, 0, 0, f.MonthDays));
            CarCoverFact late = InsuranceCarOf("car-3", "Sports coupe", true, 0, 210f, f.Grade, f.Points, f.Crashes, 0, 1, f.MonthDays);
            late.GraceUntil = 214;
            f.Cars.Add(late);
        }
        HealthCoverFact h = f.Health;
        h.On = state != "health_off";
        h.Plan = h.On ? 2 : 0;
        string[] plans = { "Basic", "Standard", "Full" };
        int[] premium = { 60, 140, 290 }, hospital = { 700, 850, 1000 }, doctor = { 500, 700, 900 }, excess = { 500, 250, 100 };
        long[] cap = { 20000, 60000, 200000 };
        for (int i = 0; i < InsuranceCatalogue.Tiers; i++)
        {
            h.Names[i] = plans[i];
            h.Premium[i] = premium[i];
            h.HospitalShare[i] = hospital[i];
            h.DoctorShare[i] = doctor[i];
            h.Excess[i] = excess[i];
            h.YearCap[i] = cap[i];
        }
        h.NextDue = 215;
        h.PaidYear = 1800;
        if (state != "homes_none")
        {
            HomeCoverFact home = InsuranceHomeOf(InsuranceHome, "12 Elm Street", state != "homes_buy", 1, 90, 100, 1, 2, f.Day, 214, f.MonthDays);
            home.PaidYear = 102;
            home.ClaimsYear = 1;
            f.Homes.Add(home);
            f.Homes.Add(InsuranceHomeOf(InsuranceHomeBare, "88 Harbor Road", false, 1, 240, 80, 0, 0, f.Day, 0, f.MonthDays));
        }
        f.Insurers[0] = "Dockside Mutual";
        f.Insurers[1] = "Empire General";
        f.Insurers[2] = "Crown Assurance";
        f.Business.Add(InsuranceBizOf(0, "Fire", 1, 14, false));
        f.Business.Add(InsuranceBizOf(1, "Theft", -1, 0, false));
        f.Business.Add(InsuranceBizOf(2, "Liability", -1, 0, state == "business_refused"));
        if (state != "claims_none")
        {
            f.Claims.Add(new ClaimFact { Day = 208, Line = (int)CoverLine.Business, Kind = 1, What = "Corner Coffee", Loss = 2400, Excess = 360, Payout = 2040, Id = 7, Stage = 1, StageDay = 213, KindName = "Theft" });
            f.Claims.Add(new ClaimFact { Day = 205, Line = (int)CoverLine.Car, Kind = (int)ClaimKind.Repair, What = "Family hatchback", Loss = 900, Excess = 100, Payout = 640, Id = -1 });
            f.Claims.Add(new ClaimFact { Day = 198, Line = (int)CoverLine.Home, Kind = (int)ClaimKind.HomeRepair, What = "12 Elm Street", Loss = 140, Excess = 20, Payout = 102, Id = -1 });
            f.Claims.Add(new ClaimFact { Day = 190, Line = (int)CoverLine.Car, Kind = (int)ClaimKind.Liability, What = "Family hatchback", Loss = 450, Excess = 100, Payout = 350, Id = -1 });
        }
        long premiums = h.On ? h.Premium[h.Plan - 1] : 0;
        int inForce = h.On ? 1 : 0;
        for (int i = 0; i < f.Cars.Count; i++)
        {
            premiums += f.Cars[i].Insured ? f.Cars[i].Premium[f.Cars[i].Tier] : 0;
            inForce += f.Cars[i].Insured ? 1 : 0;
        }
        for (int i = 0; i < f.Homes.Count; i++)
        {
            premiums += f.Homes[i].Active ? f.Homes[i].Premium[f.Homes[i].Tier] : 0;
            inForce += f.Homes[i].Active ? 1 : 0;
        }
        for (int i = 0; i < f.Business.Count; i++)
        {
            premiums += f.Business[i].Active ? (long)f.Business[i].Premium * f.MonthDays : 0;
            inForce += f.Business[i].Active ? 1 : 0;
        }
        f.PremiumsMonth = premiums;
        f.InForce = inForce;
        f.PaidYear = 640 + 102 + 350 + 1800;
        return f;
    }

    public static VentModel Insurance(UiWords t, string state, VentView view = null, IInsureActions act = null)
    {
        VentWords w = VentWordsOf(t);
        InsuranceFacts f = InsuranceFactsOf(state);
        view = view ?? new VentView();
        switch (state)
        {
            case "cars_change":
            case "cars_unsound":
                view.Tab = InsurePages.TabCars;
                view.Pick = view.S1 = InsurePages.CarKey + InsuranceCar;
                view.A = 2;
                break;
            case "cars_buy":
                view.Tab = InsurePages.TabCars;
                view.Pick = view.S1 = InsurePages.CarKey + InsuranceCar;
                view.A = 0;
                break;
            case "health":
            case "health_off": view.Tab = InsurePages.TabHealth; break;
            case "homes":
            case "homes_none": view.Tab = InsurePages.TabHomes; break;
            case "homes_buy":
                view.Tab = InsurePages.TabHomes;
                view.Pick = view.S1 = InsurePages.HomeKey + InsuranceHome;
                view.A = 2;
                break;
            case "business": view.Tab = InsurePages.TabBusiness; break;
            case "business_buy":
            case "business_refused":
                view.Tab = InsurePages.TabBusiness;
                view.Pick = view.S2 = InsurePages.BizKey + InsuranceShop + ":2";
                view.B = 2;
                break;
            case "claims":
            case "claims_none": view.Tab = InsurePages.TabClaims; break;
            case "claims_open":
                view.Tab = InsurePages.TabClaims;
                view.Pick = InsurePages.ClaimKey + 0;
                break;
        }
        var m = new VentModel();
        InsurePages.Fill(m, view, f, act ?? new InsuranceStub(), w);
        return m;
    }
}
