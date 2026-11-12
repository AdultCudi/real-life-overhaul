using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Insurance
{
    /// <summary>
    /// The homes: a policy for each, its month's premium taken on its day through the mod's one booking call, the days of grace, and what it pays of a repair. A
    /// repair is asked about by the life event that is about to charge it (Wiring/InsuranceLinks). Run on the day's pass, a claim or a press: never per frame.
    /// </summary>
    public sealed partial class InsuranceDirector
    {
        private readonly List<HomeLook> _homeLooks = new List<HomeLook>(4);

        private int HomeLookOf(string place)
        {
            for (int i = 0; i < _homeLooks.Count; i++)
            {
                if (_homeLooks[i].Place == place)
                {
                    return i;
                }
            }
            return -1;
        }

        private static int IncomeOf(string district)
        {
            Func<string, int> income = InsuranceBridges.DistrictIncome;
            return income != null && !string.IsNullOrEmpty(district) ? income(district) : InsuranceCatalogue.HomeIncomeMiddle;
        }

        // A month's premium of a home at a tier today: by its floor area, its district, the claims at it that still count and its months without a claim.
        private static int HomePremiumOf(HomeLook look, HomePolicy p, int tier, int day)
        {
            return InsuranceRules.HomePremium(tier, look.Sqm, IncomeOf(look.District), p != null ? InsuranceRules.HomeClaims(p.ClaimDays, day) : 0, p != null ? p.NoClaimMonths : 0,
                InsuranceGame.Level, InsuranceGame.MonthDays);
        }

        // The day's look at the player's homes: the policy of a home that is no longer theirs ends, and its record goes once its year is over.
        private void SyncHomes(int day)
        {
            InsuranceGame.Homes(_homeLooks);
            int year = InsuranceRules.YearOf(day, InsuranceGame.DaysPerYear);
            for (int i = _state.Homes.Count - 1; i >= 0; i--)
            {
                HomePolicy p = _state.Homes[i];
                int at = HomeLookOf(p.Place);
                if (at >= 0)
                {
                    p.Name = _homeLooks[at].Name;
                    continue;
                }
                if (p.Active)
                {
                    InsuranceRules.End(p);
                    Logger?.Info("Insurance: the home at " + p.Name + " is no longer the player's, so its policy has ended.");
                }
                if (p.Year < year)
                {
                    _state.Homes.RemoveAt(i);
                }
            }
        }

        // The homes' premiums: one due is taken; one that cannot be is asked for again each morning of its days of grace, the cover holding, and then the policy
        // lapses with a notice.
        private void HomeDay(int day)
        {
            int month = InsuranceGame.MonthDays;
            for (int i = 0; i < _state.Homes.Count; i++)
            {
                HomePolicy p = _state.Homes[i];
                int at = HomeLookOf(p.Place);
                if (at < 0 || !InsuranceRules.Due(p, day))
                {
                    continue;
                }
                int premium = HomePremiumOf(_homeLooks[at], p, p.Tier, day);
                if (InsuranceGame.Pay(premium, InsuranceGame.HomeKey))
                {
                    int due = p.NextDue;
                    InsuranceRules.Paid(p, premium, day, month);
                    if (Verbose)
                    {
                        Logger?.Info("Insurance check (a home's premium, " + p.Name + ", tier " + p.Tier + "): " + premium + " taken on day " + day + " for the one due on day " + due
                                     + "; the next is due on day " + p.NextDue + ", " + p.NoClaimMonths + " month(s) without a claim: " + (p.NextDue > day ? "PASS" : "FAIL, the next is not ahead"));
                    }
                    continue;
                }
                GraceStep step = InsuranceRules.Missed(ref p.GraceFrom, day);
                if (step == GraceStep.Began)
                {
                    InsuranceGame.Tell(NotificationType.Warning, "reallife_ins_note_home_grace", InsuranceGame.Data("home", p.Name, "amount", InsuranceGame.Money(premium),
                        "day", InsuranceRules.GraceEnds(p.GraceFrom).ToString()), OpenHomes);
                }
                else if (step == GraceStep.Lapsed)
                {
                    InsuranceRules.End(p);
                    InsuranceGame.Tell(NotificationType.Warning, "reallife_ins_note_home_lapsed", InsuranceGame.Data("home", p.Name, "amount", InsuranceGame.Money(premium)), OpenHomes);
                }
                if (Verbose)
                {
                    Logger?.Info("Insurance check (a home's premium missed, " + p.Name + "): " + premium + " could not be taken on day " + day + ": " + step
                                 + (step == GraceStep.Lapsed ? ", the policy has ended" : ", the cover holds to day " + InsuranceRules.GraceEnds(p.GraceFrom)) + ": PASS");
                }
            }
        }

        // ---- asked by a life event

        /// <summary>
        /// A life event is about to charge for a repair: at home (<paramref name="atHome"/>), which happened at one of the player's homes and is that home's policy's
        /// to pay, or to the player's own things away from home, which a policy that covers belongings pays. Returns what the policy pays, for the bill to be taken
        /// less it; the insurer's notice says so beside the event's own words.
        /// </summary>
        public int HomeRepair(int bill, bool atHome)
        {
            if (!Running || bill <= 0)
            {
                return 0;
            }
            int day = InsuranceGame.Day;
            SyncHomes(day);
            if (_homeLooks.Count == 0)
            {
                return 0;
            }
            HomePolicy p = null;
            if (atHome)
            {
                p = _state.Home(_homeLooks[InsuranceRules.HomeAt(day, _homeLooks.Count)].Place);
            }
            else
            {
                for (int i = 0; i < _state.Homes.Count; i++)
                {
                    HomePolicy h = _state.Homes[i];
                    if (h.Active && HomeLookOf(h.Place) >= 0 && InsuranceCatalogue.BelongingsShare[InsuranceCatalogue.Tier(h.Tier)] > 0 && (p == null || h.Since < p.Since))
                    {
                        p = h;   // the one held longest: its first days are over soonest
                    }
                }
            }
            if (p == null || !p.Active)
            {
                return 0;
            }
            long pays = InsuranceRules.HomeClaim(p, !atHome, bill, day, InsuranceGame.DaysPerYear, InsuranceGame.Level, out long borne, out ClaimWhy why);
            Claimed(CoverLine.Home, atHome ? ClaimKind.HomeRepair : ClaimKind.Belongings, p.Name, bill, borne, pays, why, p.Since + InsuranceCatalogue.HomeWaitDays, true);
            if (Verbose)
            {
                Logger?.Info("Insurance check (a repair " + (atHome ? "at home" : "to the player's things") + ", " + p.Name + ", tier " + p.Tier + "): a bill of " + bill + ", excess " + borne
                             + ", pays " + pays + " (" + why + "), " + p.ClaimsYear + " claim(s) and " + p.PaidYear + " paid this year: " + (pays <= bill ? "PASS" : "FAIL, more than the bill"));
            }
            return (int)pays;
        }

        // ---- pressed in the app

        /// <summary>Takes a policy for a home at a tier: the first month is paid now. False if it has one or the money is not there.</summary>
        public bool BuyHome(string place, int tier, out int charged)
        {
            charged = 0;
            if (!Running)
            {
                return false;
            }
            int day = InsuranceGame.Day;
            SyncHomes(day);
            int at = HomeLookOf(place);
            HomePolicy p = _state.Home(place);
            if (at < 0 || (p != null && p.Active))
            {
                return false;
            }
            tier = InsuranceCatalogue.Tier(tier);
            int premium = HomePremiumOf(_homeLooks[at], p, tier, day);
            if (!InsuranceGame.Pay(premium, InsuranceGame.HomeKey))
            {
                return false;
            }
            if (p == null)
            {
                p = new HomePolicy { Place = place };
                _state.Homes.Add(p);
            }
            p.Name = _homeLooks[at].Name;
            InsuranceRules.Take(p, tier, premium, day, InsuranceGame.MonthDays);
            charged = premium;
            _revision++;
            if (Verbose)
            {
                Logger?.Info("Insurance check (a home's policy bought, " + p.Name + ", tier " + tier + "): " + premium + " for the first month; nothing is paid before day "
                             + (p.Since + InsuranceCatalogue.HomeWaitDays) + ", the next premium is due on day " + p.NextDue + ": PASS");
            }
            return true;
        }

        /// <summary>Moves a home's policy to another tier from now. Up: the difference for the days left of the paid month is taken. Down: nothing is taken or given back.</summary>
        public bool ChangeHome(string place, int tier, out int charged)
        {
            charged = 0;
            if (!Running)
            {
                return false;
            }
            int day = InsuranceGame.Day;
            SyncHomes(day);
            int at = HomeLookOf(place);
            HomePolicy p = _state.Home(place);
            tier = InsuranceCatalogue.Tier(tier);
            if (at < 0 || p == null || !p.Active || tier == p.Tier)
            {
                return false;
            }
            if (tier > p.Tier)
            {
                int before = HomePremiumOf(_homeLooks[at], p, p.Tier, day), after = HomePremiumOf(_homeLooks[at], p, tier, day);
                int due = InsuranceRules.UpgradeCharge(before, after, p.NextDue - day, InsuranceGame.MonthDays);
                if (!InsuranceGame.Pay(due, InsuranceGame.HomeKey))
                {
                    return false;
                }
                charged = due;
                p.Premium = after;
            }
            p.Tier = tier;
            _revision++;
            return true;
        }

        /// <summary>Ends a home's policy. Nothing is given back; its record stays, so taking one again gives no new year and begins with new first days.</summary>
        public bool CancelHome(string place)
        {
            HomePolicy p = Running ? _state.Home(place) : null;
            if (p == null || !p.Active)
            {
                return false;
            }
            InsuranceRules.End(p);
            _revision++;
            return true;
        }
    }
}
