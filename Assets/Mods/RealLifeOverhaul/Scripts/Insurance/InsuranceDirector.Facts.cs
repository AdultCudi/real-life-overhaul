using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Insurance
{
    /// <summary>
    /// What the pages read: the state, the Garage's cars, the player's homes, Health's plan and the Company's cover gathered into <see cref="InsuranceFacts"/> when a
    /// page is written (a press or a changed revision), never per frame.
    /// </summary>
    public sealed partial class InsuranceDirector
    {
        private static readonly Comparison<ClaimFact> NewestFirst = (a, b) => b.Day != a.Day ? b.Day.CompareTo(a.Day) : b.Line.CompareTo(a.Line);

        /// <summary>Fills the facts for a page; false, with Ready false, while insurance does not run.</summary>
        public bool Facts(InsuranceFacts f)
        {
            f.Cars.Clear();
            f.Homes.Clear();
            f.Business.Clear();
            f.Claims.Clear();
            if (!Running)
            {
                f.Ready = false;
                return false;
            }
            f.Ready = true;
            int day = f.Day = InsuranceGame.Day, month = f.MonthDays = InsuranceGame.MonthDays, yearDays = f.YearDays = InsuranceGame.DaysPerYear;
            int year = InsuranceRules.YearOf(day, yearDays);
            f.Level = InsuranceGame.Level;
            f.Cash = InsuranceGame.Cash;
            f.Grade = Grade;
            f.Points = Points;
            f.Crashes = InsuranceRules.Crashes(_state.CrashDays, day);
            long premiums = 0, paid = InsuranceRules.PaidInYear(_state, day, yearDays);
            int inForce = 0;

            f.CarsOn = ReadCars();
            for (int i = 0; f.CarsOn && i < _carLooks.Count; i++)
            {
                CarLook look = _carLooks[i];
                CarPolicy p = _state.Car(look.Id);
                var c = new CarCoverFact { Id = look.Id, Name = look.Name, Insured = look.InsuredUntil >= day, Tier = p != null ? InsuranceCatalogue.Tier(p.Tier) : InsuranceCatalogue.StandardTier };
                c.DaysLeft = c.Insured ? look.InsuredUntil - day + 1 : 0;
                for (int t = 0; t < InsuranceCatalogue.Tiers; t++)
                {
                    c.Premium[t] = WholeMoney.Round(InsuranceRules.CarPremium(look.Premium, FactorOf(p, t, day)));
                }
                for (int t = 0; t < InsuranceCatalogue.Tiers; t++)
                {
                    c.Change[t] = c.Insured && t > c.Tier ? InsuranceRules.UpgradeCharge(c.Premium[c.Tier], c.Premium[t], c.DaysLeft, month) : 0;
                }
                c.Sound = InsuranceRules.Sound(InsuranceRules.Damage(look.Damage), look.Broken);
                c.WaitUntil = c.Insured && p != null && InsuranceRules.Waiting(p.Since, day, InsuranceCatalogue.CarWaitDays) ? p.Since + InsuranceCatalogue.CarWaitDays - 1 : 0;
                c.GraceUntil = c.Insured && p != null && p.GraceFrom > 0 ? InsuranceRules.GraceEnds(p.GraceFrom) : 0;
                c.NoClaim = InsuranceRules.NoClaim(p != null ? p.NoClaimMonths : 0);
                c.PaidYear = p != null && p.Year == year ? p.PaidYear : 0;
                c.ClaimsYear = p != null && p.Year == year ? p.ClaimsYear : 0;
                f.Cars.Add(c);
                if (c.Insured)
                {
                    premiums += c.Premium[c.Tier];
                    inForce++;
                }
            }

            HealthCoverFact h = f.Health;
            Func<HealthCoverFact, bool> health = InsuranceBridges.Health;
            h.On = health != null && health(h);
            if (!h.On)
            {
                h.Plan = 0;
            }
            else if (h.Plan > 0 && h.Plan <= InsuranceCatalogue.Tiers)
            {
                premiums += h.Premium[h.Plan - 1];
                paid += h.PaidYear;
                inForce++;
            }

            SyncHomes(day);
            for (int i = 0; i < _homeLooks.Count; i++)
            {
                HomeLook look = _homeLooks[i];
                HomePolicy p = _state.Home(look.Place);
                var m = new HomeCoverFact { Place = look.Place, Name = look.Name, Active = p != null && p.Active,
                    Tier = p != null ? InsuranceCatalogue.Tier(p.Tier) : InsuranceCatalogue.StandardTier, Sqm = look.Sqm };
                for (int t = 0; t < InsuranceCatalogue.Tiers; t++)
                {
                    m.Premium[t] = HomePremiumOf(look, p, t, day);
                }
                for (int t = 0; t < InsuranceCatalogue.Tiers; t++)
                {
                    m.Change[t] = m.Active && t > m.Tier ? InsuranceRules.UpgradeCharge(m.Premium[m.Tier], m.Premium[t], p.NextDue - day, month) : 0;
                }
                m.NextDue = m.Active ? p.NextDue : 0;
                m.WaitUntil = m.Active && InsuranceRules.Waiting(p.Since, day, InsuranceCatalogue.HomeWaitDays) ? p.Since + InsuranceCatalogue.HomeWaitDays - 1 : 0;
                m.GraceUntil = m.Active && p.GraceFrom > 0 ? InsuranceRules.GraceEnds(p.GraceFrom) : 0;
                m.NoClaim = InsuranceRules.NoClaim(p != null ? p.NoClaimMonths : 0);
                m.PaidYear = p != null && p.Year == year ? p.PaidYear : 0;
                m.ClaimsYear = p != null && p.Year == year ? p.ClaimsYear : 0;
                m.District = InsuranceRules.HomeDistrict(IncomeOf(look.District));
                m.Loading = p != null ? Math.Min(InsuranceCatalogue.HomeLoadingMost, InsuranceRules.HomeClaims(p.ClaimDays, day) * InsuranceCatalogue.HomeClaimLoading) : 0;
                f.Homes.Add(m);
                if (m.Active)
                {
                    premiums += m.Premium[m.Tier];
                    inForce++;
                }
            }

            Func<List<BizCoverFact>, string[], bool> business = InsuranceBridges.Business;
            f.BusinessOn = business != null && business(f.Business, f.Insurers);
            for (int i = 0; i < f.Business.Count; i++)
            {
                if (f.Business[i].Active)
                {
                    premiums += (long)f.Business[i].Premium * month;   // the Company takes a business's premium by the day
                    inForce++;
                }
            }

            for (int i = _state.Claims.Count - 1; i >= 0; i--)
            {
                ClaimNote n = _state.Claims[i];
                f.Claims.Add(new ClaimFact { Day = n.Day, Line = n.Line, Kind = n.Kind, What = n.What, Loss = n.Loss, Excess = n.Excess, Payout = n.Payout, Id = -1 });
            }
            int own = f.Claims.Count;
            if (f.BusinessOn)
            {
                InsuranceBridges.BusinessClaims?.Invoke(f.Claims);
            }
            for (int i = own; i < f.Claims.Count; i++)
            {
                paid += f.Claims[i].Stage == 0 && InsuranceRules.YearOf(f.Claims[i].Day, yearDays) == year ? f.Claims[i].Payout : 0;
            }
            f.Claims.Sort(NewestFirst);
            f.PremiumsMonth = premiums;
            f.PaidYear = paid;
            f.InForce = inForce;
            return true;
        }
    }
}
