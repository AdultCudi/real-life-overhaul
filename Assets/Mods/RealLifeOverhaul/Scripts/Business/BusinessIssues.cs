using System.Collections.Generic;
using Entities;
using Extensions;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>One thing the player can do about an issue, as the Business desk reads it out.</summary>
    internal struct IssueOption
    {
        public string Label;      // the button
        public string Text;       // what it means, with {amount} and the issue's other values
        public int Amount;        // what it costs (or pays), for {amount}
        public bool Blocked;      // on offer but not possible now (cannot be paid, a condition not met): said, not chosen
    }

    /// <summary>
    /// What each kind of business issue is: when it can start, how it starts, what it says, its choices and what each does,
    /// and what its deadline brings; one switch per question, one case per kind. The game is touched only through
    /// <see cref="BusinessGame"/>, and state kept in <see cref="BusinessState"/>. Called from the desk and the director's hour.
    /// </summary>
    internal static partial class BusinessIssues
    {
        public const int MostOptions = 4;

        private static readonly string[] Weakest =
        {
            "reallife_biz_figure_service", "reallife_biz_figure_pricing", "reallife_biz_figure_cleanliness", "reallife_biz_figure_facility",
            "reallife_biz_figure_interior"   // 4: short of its district's standard
        };

        private static BusinessDirector Director => BusinessDirector.Instance;

        /// <summary>Why the kind cannot start here, as a locale key, or null; a test waives how the business does, not what the story needs.</summary>
        public static string Why(IssueDef def, BuildingRegistration reg, bool test)
        {
            switch (def.Id)
            {
                case BusinessCatalogue.Undercut:
                case BusinessCatalogue.Smear:
                    return !BusinessGame.FootfallRoom(reg, BusinessCatalogue.IssueFootfall(def.Id)) ? "reallife_biz_why_no_footfall"
                        : !test && BusinessGame.GameRivalActive(reg) ? "reallife_biz_why_game_rival" : null;

                case BusinessCatalogue.Poach:
                    return BusinessGame.AnyRival(reg) == null ? "reallife_biz_why_no_rival"
                        : Poachable(reg) == null ? "reallife_biz_why_no_staff" : null;

                case BusinessCatalogue.Racket:
                    return BusinessState.Racket == RacketStage.Ended ? "reallife_biz_why_racket_over"
                        : BusinessState.Racket != RacketStage.None ? "reallife_biz_why_racket_on"
                        : !test && TimeHelper.CurrentDay < BusinessState.RacketRestUntil ? "reallife_biz_why_resting"
                        : !test && BusinessGame.DailyRevenue(reg) < BusinessCatalogue.RacketMinDaily ? "reallife_biz_why_small" : null;

                case BusinessCatalogue.Health:
                    return BusinessGame.IsFood(reg) ? null : "reallife_biz_why_not_food";

                case BusinessCatalogue.Fire:
                    return null;

                case BusinessCatalogue.Licence:
                    return BusinessCatalogue.CompanyKeepsLicences(Settings.Economy, Settings.EconomyCompany, Settings.EconomyCompanyLicences) ? "reallife_biz_why_company_licences"
                        : BusinessGame.Fee(reg, BusinessCatalogue.LicenceShare, 60, 900) <= 0 ? "reallife_biz_why_no_cash" : null;

                case BusinessCatalogue.Strike:
                    return BusinessGame.Staff(reg).Count < 2 ? "reallife_biz_why_two_staff"
                        : !test && AverageGap(reg) < BusinessCatalogue.StrikeGap ? "reallife_biz_why_paid_well" : null;

                case BusinessCatalogue.StaffTheft:
                    return !BusinessGame.AllowsTheft(reg) ? "reallife_biz_why_no_theft"
                        : BusinessGame.Staff(reg).Count < 1 ? "reallife_biz_why_no_staff"
                        : !test && BusinessGame.Security(reg) < BusinessCatalogue.StaffTheftMinSecurity ? "reallife_biz_why_no_camera" : null;

                case BusinessCatalogue.Argument:
                    return BusinessGame.Staff(reg).Count < 2 ? "reallife_biz_why_two_staff" : null;

                case BusinessCatalogue.SupplierLate:
                case BusinessCatalogue.SupplierPrice:
                {
                    DeliveryContract next = BusinessGame.NextDelivery(reg);
                    return next == null || BusinessGame.DeliveryPrice(next) < 1f ? "reallife_biz_why_no_delivery" : null;
                }

                case BusinessCatalogue.Complaint:
                    return !BusinessGame.FootfallRoom(reg, BusinessCatalogue.IssueFootfall(def.Id)) ? "reallife_biz_why_no_footfall"
                        : !test && BusinessGame.Satisfaction(reg) >= BusinessCatalogue.ComplaintBelow && BusinessGame.Cleanliness(reg) >= 55 && !Shabby(reg)
                            ? "reallife_biz_why_doing_well" : null;

                case BusinessCatalogue.Praise:
                    return !BusinessGame.FootfallRoom(reg, BusinessCatalogue.IssueFootfall(def.Id)) ? "reallife_biz_why_no_footfall"
                        : !test && BusinessGame.Satisfaction(reg) < BusinessCatalogue.PraiseAbove ? "reallife_biz_why_not_good_enough" : null;

                case BusinessCatalogue.Audit:
                    return !test && (SaveGameManager.Current.financialSummaries == null || SaveGameManager.Current.financialSummaries.Count < 7
                                     || BusinessGame.DailyRevenue(reg) <= 0f) ? "reallife_biz_why_no_books" : null;

                case BusinessCatalogue.BulkOrder:
                    return !test && BusinessGame.DailyRevenue(reg) < 100f ? "reallife_biz_why_small" : null;

                case BusinessCatalogue.Feature:
                    return BusinessGame.FootfallRoom(reg, BusinessCatalogue.IssueFootfall(def.Id)) ? null : "reallife_biz_why_no_footfall";

                case BusinessCatalogue.Award:
                    return !test && (BusinessGame.Satisfaction(reg) < BusinessCatalogue.AwardSatisfaction
                                     || BusinessGame.Cleanliness(reg) < BusinessCatalogue.AwardCleanliness) ? "reallife_biz_why_not_good_enough" : null;
            }
            return "reallife_biz_why_no_footfall";
        }

        private static bool Shabby(BuildingRegistration reg)
        {
            try
            {
                return BusinessDirector.Shabby != null && BusinessDirector.Shabby(reg);
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static int Inspected(BuildingRegistration reg, int clean)
        {
            try
            {
                return BusinessDirector.Inspected != null ? Mathf.Clamp(BusinessDirector.Inspected(reg, clean), 0, clean) : clean;
            }
            catch (System.Exception)
            {
                return clean;
            }
        }

        public static bool GroupOn(PressureGroup group)
        {
            switch (group)
            {
                case PressureGroup.Rivals: return Settings.BusinessRivals;
                case PressureGroup.Racket: return Settings.BusinessRacket;
                case PressureGroup.Inspections: return Settings.BusinessInspections;
                case PressureGroup.Staff: return Settings.BusinessStaff;
                case PressureGroup.Suppliers: return Settings.BusinessSuppliers;
                case PressureGroup.Customers: return Settings.BusinessCustomers;
                case PressureGroup.Audit: return Settings.BusinessAudit;
                default: return Settings.BusinessOpportunities;
            }
        }

        // The most skilled employee the game would let a rival approach, with no offer or complaint open and nothing in hand
        // for them elsewhere (BusinessDirector.Held): one rival's offer to an employee at a time.
        private static EmployeeInstance Poachable(BuildingRegistration reg)
        {
            List<EmployeeInstance> staff = BusinessGame.Staff(reg);
            EmployeeInstance best = null;
            for (int i = 0; i < staff.Count; i++)
            {
                EmployeeInstance e = staff[i];
                if (e.IsPoachable && string.IsNullOrEmpty(e.poachedByRivalId) && !e.complaintData.isComplaining && !BusinessDirector.HeldElsewhere(e.id)
                    && (best == null || e.characterData.skills[0].value > best.characterData.skills[0].value))
                {
                    best = e;
                }
            }
            return best;
        }

        private static float AverageGap(BuildingRegistration reg)
        {
            List<EmployeeInstance> staff = BusinessGame.Staff(reg);
            float total = 0f;
            for (int i = 0; i < staff.Count; i++)
            {
                total += Mathf.Max(0f, BusinessGame.WageGap(staff[i]));
            }
            return staff.Count > 0 ? total / staff.Count : 0f;
        }

        // Somebody an issue is about is remembered as "id|name".
        private static string RefId(BusinessIssue issue)
        {
            int bar = issue.Ref != null ? issue.Ref.IndexOf('|') : -1;
            return bar > 0 ? issue.Ref.Substring(0, bar) : issue.Ref;
        }

        private static string RefName(BusinessIssue issue)
        {
            int bar = issue.Ref != null ? issue.Ref.IndexOf('|') : -1;
            return bar > 0 ? issue.Ref.Substring(bar + 1) : BusinessGame.NameOf(BusinessGame.Employee(issue.Ref));
        }

        private static string Money(int amount)
        {
            return ((float)amount).ToCurrencyFormat();
        }

        private static void Pair(BusinessIssue issue, out EmployeeInstance a, out EmployeeInstance b)
        {
            a = null;
            b = null;
            int bar = issue.Ref != null ? issue.Ref.IndexOf('|') : -1;
            if (bar > 0)
            {
                a = BusinessGame.Employee(issue.Ref.Substring(0, bar));
                b = BusinessGame.Employee(issue.Ref.Substring(bar + 1));
            }
        }

        private static float RaisedWage(EmployeeInstance e)
        {
            float wanted = BusinessGame.ExpectedWage(e) * BusinessCatalogue.StrikeRaiseTo;
            return Mathf.Max(e.hourlyWage, Mathf.Min(wanted, e.hourlyWage * BusinessCatalogue.StrikeRaiseMost));
        }

        /// <summary>Does the option picked and returns the line saying what came of it, its values in <paramref name="data"/>. The issue closes unless the line says otherwise.</summary>
        public static string Choose(BusinessIssue issue, BuildingRegistration reg, int option, out Dictionary<string, string> data)
        {
            var options = new IssueOption[MostOptions];
            int count = Options(issue, reg, options);
            data = Data(issue, reg);
            if (option < 0 || option >= count)
            {
                return "reallife_biz_desk_gone";
            }
            IssueOption picked = options[option];
            data["amount"] = Money(picked.Amount);
            if (picked.Blocked)
            {
                return "reallife_biz_cannot";
            }
            int day = TimeHelper.CurrentDay;
            BusinessGame.Logger?.Info("Business: '" + issue.Kind + "' at " + BusinessGame.Name(reg) + ": option " + option + " (" + picked.Label + ").");
            if (picked.Label == "reallife_biz_bribe_do_pay")
            {
                return PayBribe(issue, reg, picked.Amount);
            }
            switch (issue.Kind)
            {
                case BusinessCatalogue.Undercut:
                    if (option == 0)
                    {
                        if (!BusinessGame.Charge(reg, picked.Amount, "reallife_transaction_biz_marketing", BusinessGame.Marketing))
                        {
                            return "reallife_biz_cannot";
                        }
                        Director.Effect(reg, 4, 3);
                        return Done(issue, "reallife_biz_undercut_marketing_done");
                    }
                    if (option == 1)
                    {
                        if (!BusinessGame.Charge(reg, picked.Amount, "reallife_transaction_biz_pricematch", BusinessGame.Marketing))
                        {
                            return "reallife_biz_cannot";
                        }
                        Director.Effect(reg, issue.A / 2, Mathf.Max(1, issue.StartDay + BusinessCatalogue.UndercutDays - day));
                        return Done(issue, "reallife_biz_undercut_price_done");
                    }
                    if (option == 2)
                    {
                        Director.Effect(reg, issue.A, 2);
                        return Done(issue, "reallife_biz_undercut_quality_done");
                    }
                    return Done(issue, "reallife_biz_undercut_rode");

                case BusinessCatalogue.Smear:
                    if (option == 0)
                    {
                        if (!BusinessGame.Charge(reg, picked.Amount, "reallife_transaction_biz_pr", BusinessGame.Marketing))
                        {
                            return "reallife_biz_cannot";
                        }
                        Director.Effect(reg, 0, 0);
                        return Done(issue, "reallife_biz_smear_reply_done");
                    }
                    if (option == 1)
                    {
                        if (!BusinessGame.Charge(reg, picked.Amount, "reallife_transaction_biz_lawyer"))
                        {
                            return "reallife_biz_cannot";
                        }
                        if (Random.value < BusinessCatalogue.SmearLawyerOdds)
                        {
                            Director.Effect(reg, 3, 2);
                            return Done(issue, "reallife_biz_smear_lawyer_won");
                        }
                        return Done(issue, "reallife_biz_smear_lawyer_lost");
                    }
                    return Done(issue, "reallife_biz_smear_ignored");

                case BusinessCatalogue.Racket:
                    return ChooseRacket(issue, reg, option, day);

                case BusinessCatalogue.Health:
                    if (!BusinessGame.Charge(reg, picked.Amount, "reallife_transaction_biz_cleaning"))
                    {
                        return "reallife_biz_cannot";
                    }
                    BusinessGame.DeepClean(reg);
                    issue.B = 1;
                    data["clean"] = BusinessGame.Cleanliness(reg).ToString();
                    return "reallife_biz_health_clean_done";   // stays open: the inspector still comes

                case BusinessCatalogue.Fire:
                    if (!BusinessGame.Charge(reg, picked.Amount, "reallife_transaction_biz_firecheck"))
                    {
                        return "reallife_biz_cannot";
                    }
                    issue.B = 1;
                    return "reallife_biz_fire_check_done";     // stays open: the inspector still comes

                case BusinessCatalogue.Licence:
                    if (!BusinessGame.Charge(reg, issue.A, "reallife_transaction_biz_licence", BusinessGame.Licensing))
                    {
                        return "reallife_biz_cannot";
                    }
                    return Done(issue, "reallife_biz_licence_paid");

                case BusinessCatalogue.Strike:
                    return ChooseStrike(issue, reg, option);

                case BusinessCatalogue.StaffTheft:
                {
                    EmployeeInstance e = BusinessGame.Employee(RefId(issue));
                    if (e == null)
                    {
                        return Done(issue, "reallife_biz_staff_gone");
                    }
                    if (option == 0)
                    {
                        return Done(issue, BusinessGame.Leave(e) ? "reallife_biz_stafftheft_dismissed" : "reallife_biz_staff_gone");
                    }
                    if (option == 1)
                    {
                        BusinessGame.Mood(e, -10f);
                        if (Random.value < BusinessCatalogue.StaffTheftRepayOdds)
                        {
                            BusinessGame.UnbookTheft(reg, issue.A);
                            return Done(issue, "reallife_biz_stafftheft_repaid");
                        }
                        return Done(issue, "reallife_biz_stafftheft_warned");
                    }
                    return Done(issue, "reallife_biz_stafftheft_forgiven");
                }

                case BusinessCatalogue.Argument:
                {
                    Pair(issue, out EmployeeInstance a, out EmployeeInstance b);
                    if (a == null || b == null)
                    {
                        return Done(issue, "reallife_biz_staff_gone");
                    }
                    if (option == 2)
                    {
                        if (!BusinessGame.Charge(reg, issue.A, "reallife_transaction_biz_lunch", BusinessGame.Salaries))
                        {
                            return "reallife_biz_cannot";
                        }
                        BusinessGame.Mood(a, 6f);
                        BusinessGame.Mood(b, 6f);
                        return Done(issue, "reallife_biz_argument_lunch_done");
                    }
                    BusinessGame.Mood(option == 0 ? a : b, 5f);
                    BusinessGame.Mood(option == 0 ? b : a, -12f);
                    data["name"] = BusinessGame.NameOf(option == 0 ? a : b);
                    data["other"] = BusinessGame.NameOf(option == 0 ? b : a);
                    return Done(issue, "reallife_biz_argument_sided");
                }

                case BusinessCatalogue.SupplierLate:
                    if (option == 0)
                    {
                        if (!BusinessGame.Charge(reg, issue.A, "reallife_transaction_biz_express"))
                        {
                            return "reallife_biz_cannot";
                        }
                        return Done(issue, "reallife_biz_supplierlate_express_done");
                    }
                    BusinessGame.Delay(reg, BusinessGame.NextDelivery(reg));
                    return Done(issue, "reallife_biz_supplierlate_waited");

                case BusinessCatalogue.SupplierPrice:
                    if (option == 1)
                    {
                        float odds = Mathf.Min(0.8f, BusinessCatalogue.NegotiateOdds + BusinessGame.NegotiationSkillValue() / 200f);
                        BusinessGame.TrainNegotiation(0.3f);
                        if (Random.value < odds)
                        {
                            return Done(issue, "reallife_biz_supplierprice_haggled");
                        }
                        BusinessGame.Charge(reg, Mathf.Min(issue.A, (int)BusinessGame.Cash), "reallife_transaction_biz_surcharge");
                        return Done(issue, "reallife_biz_supplierprice_haggle_failed");
                    }
                    if (option == 2)
                    {
                        if (!BusinessGame.Charge(reg, picked.Amount, "reallife_transaction_biz_switch"))
                        {
                            return "reallife_biz_cannot";
                        }
                        BusinessGame.Delay(reg, BusinessGame.NextDelivery(reg));
                        return Done(issue, "reallife_biz_supplierprice_switched");
                    }
                    if (!BusinessGame.Charge(reg, issue.A, "reallife_transaction_biz_surcharge"))
                    {
                        return "reallife_biz_cannot";
                    }
                    return Done(issue, "reallife_biz_supplierprice_paid");

                case BusinessCatalogue.Complaint:
                    if (option == 0)
                    {
                        if (!BusinessGame.Charge(reg, picked.Amount, "reallife_transaction_biz_voucher", BusinessGame.Marketing))
                        {
                            return "reallife_biz_cannot";
                        }
                        Director.Effect(reg, 0, 0);
                        return Done(issue, "reallife_biz_complaint_voucher_done");
                    }
                    return Done(issue, "reallife_biz_complaint_ignored");

                case BusinessCatalogue.Praise:
                    Director.Effect(reg, issue.A, Mathf.Max(1, issue.StartDay + BusinessCatalogue.PraiseSharedDays - day));
                    return Done(issue, "reallife_biz_praise_shared");

                case BusinessCatalogue.Audit:
                    return Done(issue, AuditOutcome(issue, reg, option == 0, data));

                case BusinessCatalogue.BulkOrder:
                    if (option == 0)
                    {
                        issue.Stage = 1;
                        return "reallife_biz_bulkorder_taken";   // stays open until the order is collected
                    }
                    return Done(issue, "reallife_biz_bulkorder_declined");

                case BusinessCatalogue.Feature:
                    if (option == 0)
                    {
                        int boost = BusinessCatalogue.IssueFootfall(issue.Kind);   // the figure Why tested for room
                        Director.Effect(reg, boost, BusinessCatalogue.FeatureDays);
                        data["percent"] = boost.ToString();
                        data["length"] = BusinessCatalogue.FeatureDays.ToString();
                        return Done(issue, "reallife_biz_feature_done");
                    }
                    return Done(issue, "reallife_biz_feature_declined");

                case BusinessCatalogue.Award:
                    return Done(issue, AwardOutcome(issue, reg));
            }
            return Done(issue, "reallife_biz_desk_gone");
        }

        private static string Done(BusinessIssue issue, string key)
        {
            BusinessState.Close(issue, TimeHelper.CurrentDay);
            BusinessGame.Logger?.Info("Business: '" + issue.Kind + "' closed (" + key + ").");
            BusinessEvents.RaiseClosed(issue.Kind, issue.Place, key);
            return key;
        }

        private static string AuditOutcome(BusinessIssue issue, BuildingRegistration reg, bool accountant, Dictionary<string, string> data)
        {
            if (accountant && !BusinessGame.Charge(reg, issue.A, "reallife_transaction_biz_accountant"))
            {
                accountant = false;   // could not be paid after all: they go in alone
            }
            float roll = Random.value;
            float clean = accountant ? 0.7f : 0.4f;
            if (roll < clean)
            {
                return accountant ? "reallife_biz_audit_clean_accountant" : "reallife_biz_audit_clean";
            }
            if (!accountant && roll > 0.85f)
            {
                BusinessGame.Pay(reg, issue.C, "reallife_transaction_biz_taxrefund");
                data["amount"] = Money(issue.C);
                return "reallife_biz_audit_refund";
            }
            int adjust = Mathf.Min(accountant ? issue.B / 3 : issue.B, (int)(BusinessGame.Cash * BusinessCatalogue.CashShare));
            data["amount"] = Money(adjust);
            BusinessGame.Charge(reg, adjust, "reallife_transaction_biz_taxadjust");
            return "reallife_biz_audit_adjusted";
        }

        private static string AwardOutcome(BusinessIssue issue, BuildingRegistration reg)
        {
            BusinessGame.Pay(reg, issue.A, "reallife_transaction_biz_prize");
            if (BusinessGame.HasFootfall(reg))
            {
                Director.Effect(reg, BusinessCatalogue.AwardBoost, BusinessCatalogue.AwardDays);
            }
            ModState.AddModifier(ModState.MoodUpBig);
            return "reallife_biz_award_done";
        }

        private static string ChooseStrike(BusinessIssue issue, BuildingRegistration reg, int option)
        {
            List<EmployeeInstance> staff = BusinessGame.Staff(reg);
            if (option == 0)
            {
                for (int i = 0; i < staff.Count; i++)
                {
                    BusinessGame.SetWage(staff[i], RaisedWage(staff[i]));
                    BusinessGame.Mood(staff[i], 5f);
                }
                return Done(issue, "reallife_biz_strike_raised");
            }
            if (option == 1)
            {
                if (!BusinessGame.Charge(reg, issue.B, "reallife_transaction_biz_bonus", BusinessGame.Salaries))
                {
                    return "reallife_biz_cannot";
                }
                for (int i = 0; i < staff.Count; i++)
                {
                    BusinessGame.Mood(staff[i], 8f);
                }
                return Done(issue, "reallife_biz_strike_bonus_done");
            }
            return Done(issue, StrikeRefused(reg));
        }

        private static string StrikeRefused(BuildingRegistration reg)
        {
            if (Random.value < BusinessCatalogue.StrikeWalkoutOdds && Director.CloseFor(reg, BusinessCatalogue.StrikeClosedHours))
            {
                return "reallife_biz_strike_walkout";
            }
            List<EmployeeInstance> staff = BusinessGame.Staff(reg);
            for (int i = 0; i < staff.Count; i++)
            {
                BusinessGame.Mood(staff[i], -8f);
            }
            return "reallife_biz_strike_backed_down";
        }

        private static string ChooseRacket(BusinessIssue issue, BuildingRegistration reg, int option, int day)
        {
            switch (option)
            {
                case 0:
                    if (!BusinessGame.Charge(reg, issue.A, "reallife_transaction_biz_protection"))
                    {
                        return "reallife_biz_cannot";
                    }
                    BusinessState.Racket = RacketStage.Paying;
                    BusinessState.RacketDay = day + 7;
                    return Done(issue, "reallife_biz_racket_paying");
                case 2:
                    BusinessState.Racket = RacketStage.Reported;
                    BusinessState.RacketDay = day + BusinessCatalogue.RacketInvestigationDays;
                    return Done(issue, "reallife_biz_racket_reported");
                case 3:
                    BusinessState.Racket = RacketStage.Securing;
                    BusinessState.RacketDay = day + BusinessCatalogue.RacketSecurityDays;
                    return Done(issue, "reallife_biz_racket_securing");
                default:
                    BusinessState.Racket = RacketStage.Refused;
                    BusinessState.RacketDay = day + Random.Range(2, 5);
                    return Done(issue, "reallife_biz_racket_refused");
            }
        }

        /// <summary>Hourly: the poaching ends lost by the rule that takes the offer's buttons, so the two agree, or kept once matched.</summary>
        public static void Watch(BusinessIssue issue, BuildingRegistration reg)
        {
            if (issue.Kind != BusinessCatalogue.Poach)
            {
                return;
            }
            EmployeeInstance e = BusinessGame.Employee(RefId(issue));
            RivalOfferRules.State standing = BusinessGame.Standing(e, RefName(issue));
            Dictionary<string, string> data = Data(issue, reg);
            if (RivalOfferRules.Lost(standing))
            {
                Director.Tell(NotificationType.Warning, Done(issue, "reallife_biz_poach_lost"), data);
            }
            else if (e != null && RivalOfferRules.Kept(standing, e.poachedByRivalId))
            {
                Director.Tell(NotificationType.Success, Done(issue, "reallife_biz_poach_kept"), data);
            }
        }
    }
}
