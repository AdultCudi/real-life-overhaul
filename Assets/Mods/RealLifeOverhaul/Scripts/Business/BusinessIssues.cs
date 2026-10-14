using System.Collections.Generic;
using BigAmbitions.Rivals;
using Entities;
using Extensions;
using Helpers;
using Localizor;
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
        public bool Blocked;      // on offer but not possible right now (cannot be paid, a condition not met): said, not chosen
    }

    /// <summary>
    /// What each kind of business issue is: when it can start, how it starts, what it says, what
    /// can be done about it, what each choice does and what happens if the deadline passes. One
    /// switch per question, one case per kind (BusinessCatalogue has every number). Everything it
    /// does to the game goes through <see cref="BusinessGame"/>; everything it remembers is in
    /// <see cref="BusinessState"/>. Called from the Business desk and from the director's hourly
    /// look, never per frame.
    /// </summary>
    internal static class BusinessIssues
    {
        public const int MostOptions = 4;

        private static readonly string[] Weakest =
        {
            "reallife_biz_figure_service", "reallife_biz_figure_pricing", "reallife_biz_figure_cleanliness", "reallife_biz_figure_facility",
            "reallife_biz_figure_interior"   // 4: the look of the place, short of its district's standard (Scripts/Interiors)
        };

        private static BusinessDirector Director => BusinessDirector.Instance;

        // ---------------------------------------------------------------- can it start

        /// <summary>
        /// Why this kind cannot start at this business, as a sentence for the test button; null if it can.
        /// A test waives what depends on how the business is doing (a satisfaction figure, a wage gap), never
        /// what the story needs to exist at all (staff, a delivery, a food business).
        /// </summary>
        public static string Why(IssueDef def, BuildingRegistration reg, bool test)
        {
            switch (def.Id)
            {
                case BusinessCatalogue.Undercut:
                case BusinessCatalogue.Smear:
                    return !BusinessGame.FootfallRoom(reg, BusinessCatalogue.UndercutDip) ? "reallife_biz_why_no_footfall"
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
                    return BusinessGame.Fee(reg, BusinessCatalogue.LicenceShare, 60, 900) <= 0 ? "reallife_biz_why_no_cash" : null;

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
                    return !BusinessGame.FootfallRoom(reg, BusinessCatalogue.ComplaintDip) ? "reallife_biz_why_no_footfall"
                        : !test && BusinessGame.Satisfaction(reg) >= BusinessCatalogue.ComplaintBelow && BusinessGame.Cleanliness(reg) >= 55 && !Shabby(reg)
                            ? "reallife_biz_why_doing_well" : null;

                case BusinessCatalogue.Praise:
                    return !BusinessGame.FootfallRoom(reg, BusinessCatalogue.PraiseBoost) ? "reallife_biz_why_no_footfall"
                        : !test && BusinessGame.Satisfaction(reg) < BusinessCatalogue.PraiseAbove ? "reallife_biz_why_not_good_enough" : null;

                case BusinessCatalogue.Audit:
                    return !test && (SaveGameManager.Current.financialSummaries == null || SaveGameManager.Current.financialSummaries.Count < 7
                                     || BusinessGame.DailyRevenue(reg) <= 0f) ? "reallife_biz_why_no_books" : null;

                case BusinessCatalogue.BulkOrder:
                    return !test && BusinessGame.DailyRevenue(reg) < 100f ? "reallife_biz_why_small" : null;

                case BusinessCatalogue.Feature:
                    return BusinessGame.FootfallRoom(reg, BusinessCatalogue.FeatureBoost) ? null : "reallife_biz_why_no_footfall";

                case BusinessCatalogue.Award:
                    return !test && (BusinessGame.Satisfaction(reg) < BusinessCatalogue.AwardSatisfaction
                                     || BusinessGame.Cleanliness(reg) < BusinessCatalogue.AwardCleanliness) ? "reallife_biz_why_not_good_enough" : null;
            }
            return "reallife_biz_why_no_footfall";
        }

        // Short of the district's standard (Scripts/Interiors, through Wiring/InteriorLinks); never while that is not set.
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

        // The cleanliness the inspector judges: the game's own figure unless a worn place's look is set (Scripts/Interiors).
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

        private static EmployeeInstance Poachable(BuildingRegistration reg)
        {
            List<EmployeeInstance> staff = BusinessGame.Staff(reg);
            EmployeeInstance best = null;
            for (int i = 0; i < staff.Count; i++)
            {
                EmployeeInstance e = staff[i];
                if (e.IsPoachable && string.IsNullOrEmpty(e.poachedByRivalId) && !e.complaintData.isComplaining
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

        // ---------------------------------------------------------------- how it starts

        public static void Start(BusinessIssue issue, BuildingRegistration reg)
        {
            int day = TimeHelper.CurrentDay;
            Dictionary<string, string> data = Data(issue, reg);
            switch (issue.Kind)
            {
                case BusinessCatalogue.Undercut:
                {
                    RivalData rival = BusinessGame.AnyRival(reg);
                    issue.Ref = rival != null ? rival.rivalName : null;
                    issue.A = BusinessCatalogue.UndercutDip;
                    Director.Effect(reg, issue.A, BusinessCatalogue.UndercutDays);
                    break;
                }
                case BusinessCatalogue.Poach:
                {
                    EmployeeInstance e = Poachable(reg);
                    RivalData rival = BusinessGame.AnyRival(reg);
                    issue.Ref = e.id + "|" + BusinessGame.NameOf(e);   // the name too: once they have gone the game no longer knows it
                    e.PoachByRival(rival.id, BusinessCatalogue.PoachDays, BusinessCatalogue.PoachRaise);   // the game's own offer: answered in that employee's messages
                    issue.A = Mathf.RoundToInt(e.poachProposedHourlyWage * 100f);
                    issue.DueDay = day + BusinessCatalogue.PoachDays + 1;
                    BusinessGame.Logger?.Info("Business: " + BusinessGame.NameOf(e) + " at " + BusinessGame.Name(reg) + " has an offer from " + rival.rivalName + " (the game's own poaching).");
                    break;
                }
                case BusinessCatalogue.Smear:
                    issue.A = BusinessCatalogue.SmearDip;
                    Director.Effect(reg, issue.A, BusinessCatalogue.SmearDays);
                    break;

                case BusinessCatalogue.Racket:
                    issue.A = Mathf.Clamp(Mathf.RoundToInt(BusinessGame.DailyRevenue(reg) * BusinessCatalogue.RacketWeeklyShare), 60, 600);
                    BusinessState.Racket = RacketStage.Demand;
                    BusinessState.RacketPlace = issue.Place;
                    BusinessState.RacketWeekly = BusinessState.RacketFirst = issue.A;
                    break;

                case BusinessCatalogue.Health:
                case BusinessCatalogue.Fire:
                    issue.DueDay = day + 1;
                    issue.DueHour = BusinessCatalogue.InspectionHour;
                    break;

                case BusinessCatalogue.Licence:
                    issue.A = Mathf.Max(1, BusinessGame.Fee(reg, BusinessCatalogue.LicenceShare, 60, 900));
                    issue.DueHour = 17;
                    break;

                case BusinessCatalogue.Strike:
                {
                    List<EmployeeInstance> staff = BusinessGame.Staff(reg);
                    float raise = 0f;
                    float bonus = 0f;
                    for (int i = 0; i < staff.Count; i++)
                    {
                        raise += Mathf.Max(0f, RaisedWage(staff[i]) - staff[i].hourlyWage);
                        bonus += staff[i].hourlyWage * BusinessCatalogue.StrikeBonusHours;
                    }
                    issue.A = Mathf.RoundToInt(raise * 100f);    // cents an hour, all of them together
                    issue.B = Mathf.RoundToInt(bonus);
                    issue.C = staff.Count;
                    break;
                }
                case BusinessCatalogue.StaffTheft:
                {
                    List<EmployeeInstance> staff = BusinessGame.Staff(reg);
                    EmployeeInstance e = staff[Random.Range(0, staff.Count)];
                    issue.Ref = e.id + "|" + BusinessGame.NameOf(e);   // the name too: once they have gone the game no longer knows it
                    issue.A = Mathf.Clamp(Mathf.RoundToInt(BusinessGame.DailyRevenue(reg) * BusinessCatalogue.StaffTheftShare), 20, 300);
                    BusinessGame.BookTheft(reg, issue.A);
                    break;
                }
                case BusinessCatalogue.Argument:
                {
                    List<EmployeeInstance> staff = BusinessGame.Staff(reg);
                    int first = Random.Range(0, staff.Count);
                    int second = (first + Random.Range(1, staff.Count)) % staff.Count;
                    issue.Ref = staff[first].id + "|" + staff[second].id;
                    issue.A = Mathf.Clamp(Mathf.RoundToInt(BusinessGame.DailyRevenue(reg) * BusinessCatalogue.ArgumentLunchShare), 20, 120);
                    break;
                }
                case BusinessCatalogue.SupplierLate:
                case BusinessCatalogue.SupplierPrice:
                {
                    float price = BusinessGame.DeliveryPrice(BusinessGame.NextDelivery(reg));
                    bool late = issue.Kind == BusinessCatalogue.SupplierLate;
                    // (The delivery is at today's wholesale prices: the bounds are too.)
                    float fee = late ? Mathf.Clamp(price * BusinessCatalogue.ExpressShare, Prices.At(20f), Prices.At(400f)) : Mathf.Clamp(price * BusinessCatalogue.SurchargeShare, Prices.At(15f), Prices.At(500f));
                    issue.A = Mathf.RoundToInt(Mathf.Min(fee, Mathf.Max(1f, BusinessGame.Cash * BusinessCatalogue.CashShare)));
                    if (late)
                    {
                        issue.DueDay = day;
                        issue.DueHour = 22;
                    }
                    break;
                }
                case BusinessCatalogue.Complaint:
                    issue.B = Shabby(reg) ? 4 : BusinessGame.Weakest(reg);
                    issue.A = BusinessCatalogue.ComplaintDip;
                    Director.Effect(reg, issue.A, BusinessCatalogue.ComplaintDays);
                    break;

                case BusinessCatalogue.Praise:
                    issue.A = BusinessCatalogue.PraiseBoost;
                    Director.Effect(reg, issue.A, BusinessCatalogue.PraiseDays);
                    break;

                case BusinessCatalogue.Audit:
                    issue.A = Prices.At(Mathf.Clamp(Mathf.RoundToInt(BusinessGame.DailyRevenue(reg) * BusinessCatalogue.AuditAccountantShare), 100, 800));   // chosen, so not cash-capped: it only has to be affordable
                    issue.B = BusinessGame.Fee(reg, BusinessCatalogue.AuditAdjustShare, 100, 1500);
                    issue.C = Mathf.Clamp(Mathf.RoundToInt(BusinessGame.DailyRevenue(reg) * BusinessCatalogue.AuditRefundShare), 20, 500);
                    break;

                case BusinessCatalogue.BulkOrder:
                    issue.A = Mathf.Clamp(Mathf.RoundToInt(BusinessGame.DailyRevenue(reg) * BusinessCatalogue.BulkShare), 120, 2000);
                    issue.DueDay = day + 1;
                    issue.DueHour = BusinessCatalogue.BulkHour;
                    break;

                case BusinessCatalogue.Award:
                    issue.A = Mathf.Clamp(Mathf.RoundToInt(BusinessGame.DailyRevenue(reg) * BusinessCatalogue.AwardPrizeShare), 100, 1000);
                    break;
            }
            data = Data(issue, reg);
            Director.Tell(issue.Def.Positive ? NotificationType.Success : NotificationType.Warning, StartKey(issue.Kind), data);
        }

        private static string StartKey(string kind)
        {
            switch (kind)
            {
                case BusinessCatalogue.Undercut: return "reallife_biz_undercut_start";
                case BusinessCatalogue.Poach: return "reallife_biz_poach_start";
                case BusinessCatalogue.Smear: return "reallife_biz_smear_start";
                case BusinessCatalogue.Racket: return "reallife_biz_racket_start";
                case BusinessCatalogue.Health: return "reallife_biz_health_start";
                case BusinessCatalogue.Fire: return "reallife_biz_fire_start";
                case BusinessCatalogue.Licence: return "reallife_biz_licence_start";
                case BusinessCatalogue.Strike: return "reallife_biz_strike_start";
                case BusinessCatalogue.StaffTheft: return "reallife_biz_stafftheft_start";
                case BusinessCatalogue.Argument: return "reallife_biz_argument_start";
                case BusinessCatalogue.SupplierLate: return "reallife_biz_supplierlate_start";
                case BusinessCatalogue.SupplierPrice: return "reallife_biz_supplierprice_start";
                case BusinessCatalogue.Complaint: return "reallife_biz_complaint_start";
                case BusinessCatalogue.Praise: return "reallife_biz_praise_start";
                case BusinessCatalogue.Audit: return "reallife_biz_audit_start";
                case BusinessCatalogue.BulkOrder: return "reallife_biz_bulkorder_start";
                case BusinessCatalogue.Feature: return "reallife_biz_feature_start";
                default: return "reallife_biz_award_start";
            }
        }

        // ---------------------------------------------------------------- what it says

        /// <summary>The issue as the desk reads it out: the same line that announced it, with today's figures.</summary>
        public static string TextKey(BusinessIssue issue)
        {
            if (issue.Kind == BusinessCatalogue.Health && issue.Stage == 1)
            {
                return "reallife_biz_health_again";
            }
            if (issue.Kind == BusinessCatalogue.BulkOrder && issue.Stage == 1)
            {
                return "reallife_biz_bulkorder_accepted";
            }
            return StartKey(issue.Kind);
        }

        /// <summary>Every value an issue's lines may name. A new dictionary each time: the game keeps hold of a toast's data.</summary>
        public static Dictionary<string, string> Data(BusinessIssue issue, BuildingRegistration reg)
        {
            var data = new Dictionary<string, string>(12)
            {
                { "business", BusinessGame.Name(reg) },
                { "amount", Money(issue.A) },
                { "day", issue.DueDay.ToString() },
                { "time", issue.DueHour.GetFormattedTime(0f, true) },
                { "days", Mathf.Max(0, issue.DueDay - TimeHelper.CurrentDay).ToString() }
            };
            if (reg == null)
            {
                return data;
            }
            data["clean"] = BusinessGame.Cleanliness(reg).ToString();
            data["security"] = BusinessGame.Security(reg).ToString();
            data["satisfaction"] = BusinessGame.Satisfaction(reg).ToString();
            switch (issue.Kind)
            {
                case BusinessCatalogue.Undercut:
                    data["rival"] = issue.Ref ?? "reallife_biz_rival_newcomer".GetLocalization();
                    data["percent"] = (-issue.A).ToString();
                    data["length"] = BusinessCatalogue.UndercutDays.ToString();
                    break;
                case BusinessCatalogue.Smear:
                    data["percent"] = (-issue.A).ToString();
                    data["length"] = BusinessCatalogue.SmearDays.ToString();
                    break;
                case BusinessCatalogue.Poach:
                case BusinessCatalogue.StaffTheft:
                    data["name"] = RefName(issue);
                    if (issue.Kind == BusinessCatalogue.Poach)
                    {
                        data["amount"] = (issue.A / 100f).ToCurrencyFormat();
                        data["percent"] = BusinessCatalogue.PoachRaise.ToString();
                    }
                    break;
                case BusinessCatalogue.Argument:
                    Pair(issue, out EmployeeInstance a, out EmployeeInstance b);
                    data["name"] = BusinessGame.NameOf(a);
                    data["other"] = BusinessGame.NameOf(b);
                    break;
                case BusinessCatalogue.Health:
                    data["pass"] = BusinessCatalogue.HealthPass.ToString();
                    break;
                case BusinessCatalogue.Fire:
                    data["pass"] = BusinessCatalogue.FirePass.ToString();
                    data["facility"] = BusinessGame.Facility(reg).ToString();
                    data["need"] = BusinessCatalogue.FireFacility.ToString();
                    break;
                case BusinessCatalogue.Strike:
                    data["amount"] = (issue.A / 100f).ToCurrencyFormat();
                    data["raise"] = (issue.A / 100f).ToCurrencyFormat();
                    data["hours"] = BusinessCatalogue.StrikeClosedHours.ToString();
                    data["count"] = issue.C.ToString();
                    break;
                case BusinessCatalogue.Complaint:
                    data["figure"] = Weakest[Mathf.Clamp(issue.B, 0, Weakest.Length - 1)].GetLocalization();
                    data["percent"] = (-issue.A).ToString();
                    data["length"] = BusinessCatalogue.ComplaintDays.ToString();
                    break;
                case BusinessCatalogue.Praise:
                    data["percent"] = issue.A.ToString();
                    data["length"] = BusinessCatalogue.PraiseDays.ToString();
                    break;
                case BusinessCatalogue.Audit:
                    data["adjust"] = Money(issue.B);
                    break;
            }
            return data;
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

        // ---------------------------------------------------------------- what can be done

        /// <summary>The choices an open issue offers right now. Returns how many; none means it only has to be waited out.</summary>
        public static int Options(BusinessIssue issue, BuildingRegistration reg, IssueOption[] into)
        {
            int n = 0;
            switch (issue.Kind)
            {
                case BusinessCatalogue.Undercut:
                    n = Paid(into, n, reg, "reallife_biz_undercut_do_marketing", "reallife_biz_undercut_marketing", BusinessCatalogue.UndercutMarketingShare, 80, 1200);
                    n = Paid(into, n, reg, "reallife_biz_undercut_do_price", "reallife_biz_undercut_price", BusinessCatalogue.UndercutPriceShare, 40, 600);
                    into[n++] = new IssueOption
                    {
                        Label = "reallife_biz_undercut_do_quality", Text = "reallife_biz_undercut_quality",
                        Blocked = BusinessGame.Cleanliness(reg) < BusinessCatalogue.UndercutQualityCleanliness
                                  || BusinessGame.Satisfaction(reg) < BusinessCatalogue.UndercutQualitySatisfaction
                    };
                    into[n++] = new IssueOption { Label = "reallife_biz_do_ride", Text = "reallife_biz_undercut_ride" };
                    break;

                case BusinessCatalogue.Smear:
                    n = Paid(into, n, reg, "reallife_biz_smear_do_reply", "reallife_biz_smear_reply", BusinessCatalogue.SmearReplyShare, 60, 800);
                    n = Paid(into, n, reg, "reallife_biz_smear_do_lawyer", "reallife_biz_smear_lawyer", BusinessCatalogue.SmearLawyerShare, 50, 500);
                    into[n++] = new IssueOption { Label = "reallife_biz_do_ignore", Text = "reallife_biz_smear_ignore" };
                    break;

                case BusinessCatalogue.Racket:
                    into[n++] = new IssueOption { Label = "reallife_biz_racket_do_pay", Text = "reallife_biz_racket_pay", Amount = issue.A, Blocked = BusinessGame.Cash < issue.A };
                    into[n++] = new IssueOption { Label = "reallife_biz_racket_do_refuse", Text = "reallife_biz_racket_refuse" };
                    into[n++] = new IssueOption { Label = "reallife_biz_racket_do_police", Text = "reallife_biz_racket_police" };
                    into[n++] = new IssueOption { Label = "reallife_biz_racket_do_security", Text = "reallife_biz_racket_security" };
                    break;

                case BusinessCatalogue.Health:
                    if (issue.B == 0 && issue.C == 0)   // not once the inspector is in the shop
                    {
                        n = Paid(into, n, reg, "reallife_biz_health_do_clean", "reallife_biz_health_clean", BusinessCatalogue.HealthCleanShare, 40, 400);
                        n = Bribe(into, n, issue, reg);
                    }
                    break;

                case BusinessCatalogue.Fire:
                    if (issue.B == 0 && issue.C == 0)   // not once the inspector is in the shop
                    {
                        n = Paid(into, n, reg, "reallife_biz_fire_do_check", "reallife_biz_fire_check", BusinessCatalogue.FireCheckShare, 50, 500);
                        n = Bribe(into, n, issue, reg);
                    }
                    break;

                case BusinessCatalogue.Licence:
                    into[n++] = new IssueOption { Label = "reallife_biz_licence_do_pay", Text = "reallife_biz_licence_pay", Amount = issue.A, Blocked = BusinessGame.Cash < issue.A };
                    n = Bribe(into, n, issue, reg);
                    break;

                case BusinessCatalogue.Strike:
                    into[n++] = new IssueOption { Label = "reallife_biz_strike_do_raise", Text = "reallife_biz_strike_raise" };
                    into[n++] = new IssueOption { Label = "reallife_biz_strike_do_bonus", Text = "reallife_biz_strike_bonus", Amount = issue.B, Blocked = BusinessGame.Cash < issue.B };
                    into[n++] = new IssueOption { Label = "reallife_biz_strike_do_refuse", Text = "reallife_biz_strike_refuse" };
                    break;

                case BusinessCatalogue.StaffTheft:
                    into[n++] = new IssueOption { Label = "reallife_biz_stafftheft_do_dismiss", Text = "reallife_biz_stafftheft_dismiss" };
                    into[n++] = new IssueOption { Label = "reallife_biz_stafftheft_do_warn", Text = "reallife_biz_stafftheft_warn" };
                    into[n++] = new IssueOption { Label = "reallife_biz_stafftheft_do_forgive", Text = "reallife_biz_stafftheft_forgive" };
                    break;

                case BusinessCatalogue.Argument:
                    into[n++] = new IssueOption { Label = "reallife_biz_argument_do_first", Text = "reallife_biz_argument_first" };
                    into[n++] = new IssueOption { Label = "reallife_biz_argument_do_second", Text = "reallife_biz_argument_second" };
                    into[n++] = new IssueOption { Label = "reallife_biz_argument_do_lunch", Text = "reallife_biz_argument_lunch", Amount = issue.A, Blocked = BusinessGame.Cash < issue.A };
                    break;

                case BusinessCatalogue.SupplierLate:
                    into[n++] = new IssueOption { Label = "reallife_biz_supplierlate_do_express", Text = "reallife_biz_supplierlate_express", Amount = issue.A, Blocked = BusinessGame.Cash < issue.A };
                    into[n++] = new IssueOption { Label = "reallife_biz_supplierlate_do_wait", Text = "reallife_biz_supplierlate_wait" };
                    break;

                case BusinessCatalogue.SupplierPrice:
                    into[n++] = new IssueOption { Label = "reallife_biz_supplierprice_do_pay", Text = "reallife_biz_supplierprice_pay", Amount = issue.A, Blocked = BusinessGame.Cash < issue.A };
                    into[n++] = new IssueOption { Label = "reallife_biz_supplierprice_do_haggle", Text = "reallife_biz_supplierprice_haggle", Amount = issue.A };
                    into[n++] = new IssueOption { Label = "reallife_biz_supplierprice_do_switch", Text = "reallife_biz_supplierprice_switch", Amount = Mathf.Max(1, issue.A / 3), Blocked = BusinessGame.Cash < issue.A / 3 };
                    break;

                case BusinessCatalogue.Complaint:
                    n = Paid(into, n, reg, "reallife_biz_complaint_do_voucher", "reallife_biz_complaint_voucher", BusinessCatalogue.ComplaintVoucherShare, 20, 300);
                    into[n++] = new IssueOption { Label = "reallife_biz_do_ignore", Text = "reallife_biz_complaint_ignore" };
                    break;

                case BusinessCatalogue.Praise:
                    into[n++] = new IssueOption { Label = "reallife_biz_praise_do_share", Text = "reallife_biz_praise_share" };
                    break;

                case BusinessCatalogue.Audit:
                    into[n++] = new IssueOption { Label = "reallife_biz_audit_do_accountant", Text = "reallife_biz_audit_accountant", Amount = issue.A, Blocked = BusinessGame.Cash < issue.A };
                    into[n++] = new IssueOption { Label = "reallife_biz_audit_do_alone", Text = "reallife_biz_audit_alone", Amount = issue.B };
                    break;

                case BusinessCatalogue.BulkOrder:
                    if (issue.Stage == 0)
                    {
                        into[n++] = new IssueOption { Label = "reallife_biz_bulkorder_do_accept", Text = "reallife_biz_bulkorder_accept", Amount = issue.A };
                        into[n++] = new IssueOption { Label = "reallife_biz_do_decline", Text = "reallife_biz_bulkorder_decline" };
                    }
                    break;

                case BusinessCatalogue.Feature:
                    into[n++] = new IssueOption { Label = "reallife_biz_feature_do_accept", Text = "reallife_biz_feature_accept" };
                    into[n++] = new IssueOption { Label = "reallife_biz_do_decline", Text = "reallife_biz_feature_decline" };
                    break;

                case BusinessCatalogue.Award:
                    into[n++] = new IssueOption { Label = "reallife_biz_award_do_accept", Text = "reallife_biz_award_accept", Amount = issue.A };
                    break;
            }
            return n;
        }

        // An official who would make an inspection or a licence a formality for cash (Standing's corruption, through
        // BusinessDirector.BribeAsk): one more way, read out with what it costs, like any other. Nobody asks while it is not set.
        private static int Bribe(IssueOption[] into, int n, BusinessIssue issue, BuildingRegistration reg)
        {
            int ask = 0;
            try
            {
                if (BusinessDirector.BribeAsk != null && n < MostOptions)
                {
                    int catalogue = issue.Kind == BusinessCatalogue.Licence ? Mathf.Max(1, issue.A * 6 / 10) : BusinessGame.Fee(reg, BribeShare, 60, 900);
                    ask = BusinessDirector.BribeAsk(issue.Kind, issue.Place, catalogue, issue.StartDay * 31 + issue.Stage * 7 + issue.Kind.Length);
                }
            }
            catch (System.Exception e)
            {
                BusinessGame.Logger?.Error(e);
                ask = 0;
            }
            if (ask > 0)
            {
                into[n++] = new IssueOption
                {
                    Label = "reallife_biz_bribe_do_pay", Text = issue.Kind == BusinessCatalogue.Licence ? "reallife_biz_bribe_licence" : "reallife_biz_bribe_inspection", Amount = ask,
                    Blocked = BusinessGame.Cash < ask
                };
            }
            return n;
        }

        private const float BribeShare = 0.3f;   // of a day's income, $60 to $900: what an inspector asks

        // The bribe is paid: a licence goes through, an inspection is to be a formality. Whoever listens hears of it.
        private static string PayBribe(BusinessIssue issue, BuildingRegistration reg, int amount)
        {
            if (!BusinessGame.Charge(reg, amount, "reallife_transaction_biz_bribe"))
            {
                return "reallife_biz_cannot";
            }
            try
            {
                BusinessDirector.Bribed?.Invoke(issue.Kind, issue.Place, amount);
            }
            catch (System.Exception e)
            {
                BusinessGame.Logger?.Error(e);
            }
            if (issue.Kind == BusinessCatalogue.Licence)
            {
                return Done(issue, "reallife_biz_bribe_licence_done");
            }
            issue.B = 2;   // (2: paid off; 1 is the clean or the check booked)
            return "reallife_biz_bribe_inspection_done";   // stays open: the inspector still comes, as a formality
        }

        private static int Paid(IssueOption[] into, int n, BuildingRegistration reg, string label, string text, float share, int floor, int ceiling)
        {
            int amount = Mathf.Clamp(Mathf.RoundToInt(BusinessGame.DailyRevenue(reg) * share), floor, ceiling);
            // Something the player chooses to pay for: its price is the catalogue's, and it only has to be affordable.
            into[n] = new IssueOption { Label = label, Text = text, Amount = amount, Blocked = BusinessGame.Cash < amount };
            return n + 1;
        }

        // ---------------------------------------------------------------- what a choice does

        /// <summary>
        /// The player picked an option. Does it, and returns the line that says what came of it (its values are
        /// in <paramref name="data"/>). The issue is closed unless the line says otherwise.
        /// </summary>
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
            string place = issue.Place;
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
                        BusinessGame.Leave(e);
                        return Done(issue, "reallife_biz_stafftheft_dismissed");
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
                        Director.Effect(reg, BusinessCatalogue.FeatureBoost, BusinessCatalogue.FeatureDays);
                        data["percent"] = BusinessCatalogue.FeatureBoost.ToString();
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
            BusinessEvents.RaiseClosed(issue.Kind, issue.Place, key);   // (for whoever wants to know: Business/BusinessEvents.cs)
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

        // ---------------------------------------------------------------- the deadline

        /// <summary>The deadline has come with nothing (more) chosen: what happens by default. Always ends or advances the issue.</summary>
        public static void Due(BusinessIssue issue, BuildingRegistration reg)
        {
            Dictionary<string, string> data = Data(issue, reg);
            string key;
            NotificationType type = NotificationType.Info;
            int day = TimeHelper.CurrentDay;
            switch (issue.Kind)
            {
                case BusinessCatalogue.Undercut: key = Done(issue, "reallife_biz_undercut_rode"); break;
                case BusinessCatalogue.Smear: key = Done(issue, "reallife_biz_smear_ignored"); break;
                case BusinessCatalogue.Poach: Done(issue, "poach deadline"); return;   // the game has dealt with the offer itself
                case BusinessCatalogue.Racket: key = ChooseRacket(issue, reg, 1, day); type = NotificationType.Warning; break;

                case BusinessCatalogue.Health:
                    key = Inspect(issue, reg, data, out type);
                    if (key == null)
                    {
                        return;   // the inspector is walking round the shop: the verdict comes when they have
                    }
                    break;

                case BusinessCatalogue.Fire:
                {
                    if (issue.B == 2)
                    {
                        type = NotificationType.Success;
                        key = Done(issue, "reallife_biz_bribe_passed");   // paid off: a formality
                        break;
                    }
                    int clean = BusinessGame.Cleanliness(reg);
                    int facility = BusinessGame.Facility(reg);
                    bool cleanOk = clean >= BusinessCatalogue.FirePass;
                    bool facilityOk = facility >= BusinessCatalogue.FireFacility;
                    if (InspectorCalls(issue, reg))
                    {
                        return;
                    }
                    if (issue.B == 1 || (cleanOk && facilityOk))
                    {
                        Boost(reg);
                        type = NotificationType.Success;
                        key = Done(issue, "reallife_biz_fire_pass");
                        break;
                    }
                    data["list"] = (!cleanOk && !facilityOk ? "reallife_biz_fire_list_both" : !cleanOk ? "reallife_biz_fire_list_clutter" : "reallife_biz_fire_list_facility")
                        .GetLocalization().Replace("{clean}", clean.ToString()).Replace("{pass}", BusinessCatalogue.FirePass.ToString())
                        .Replace("{facility}", facility.ToString()).Replace("{need}", BusinessCatalogue.FireFacility.ToString());
                    key = Fine(issue, reg, data, BusinessCatalogue.FireFineShare, 80, 1000, "reallife_transaction_biz_firefine",
                        !cleanOk && !facilityOk, BusinessCatalogue.FireClosedHours, "reallife_biz_fire_fail", "reallife_biz_fire_fail_closed");
                    type = NotificationType.Error;
                    break;
                }

                case BusinessCatalogue.Licence:
                {
                    int late = Mathf.RoundToInt(issue.A * BusinessCatalogue.LicenceLate);
                    data["amount"] = Money(late);
                    type = NotificationType.Error;
                    if (BusinessGame.Charge(reg, late, "reallife_transaction_biz_licence", BusinessGame.Licensing))
                    {
                        key = Done(issue, "reallife_biz_licence_late");
                    }
                    else
                    {
                        Director.CloseFor(reg, BusinessCatalogue.LicenceClosedHours);
                        data["hours"] = BusinessCatalogue.LicenceClosedHours.ToString();
                        key = Done(issue, "reallife_biz_licence_closed");
                    }
                    break;
                }

                case BusinessCatalogue.Strike:
                    key = Done(issue, StrikeRefused(reg));
                    data["hours"] = BusinessCatalogue.StrikeClosedHours.ToString();
                    type = NotificationType.Warning;
                    break;

                case BusinessCatalogue.StaffTheft: key = Done(issue, "reallife_biz_stafftheft_forgiven"); break;

                case BusinessCatalogue.Argument:
                {
                    Pair(issue, out EmployeeInstance a, out EmployeeInstance b);
                    if (a != null)
                    {
                        BusinessGame.Mood(a, -6f);
                    }
                    if (b != null)
                    {
                        BusinessGame.Mood(b, -6f);
                    }
                    key = Done(issue, "reallife_biz_argument_festered");
                    type = NotificationType.Warning;
                    break;
                }

                case BusinessCatalogue.SupplierLate:
                    BusinessGame.Delay(reg, BusinessGame.NextDelivery(reg));
                    key = Done(issue, "reallife_biz_supplierlate_waited");
                    break;

                case BusinessCatalogue.SupplierPrice:
                    if (BusinessGame.Charge(reg, issue.A, "reallife_transaction_biz_surcharge"))
                    {
                        key = Done(issue, "reallife_biz_supplierprice_paid");
                    }
                    else
                    {
                        BusinessGame.Delay(reg, BusinessGame.NextDelivery(reg));
                        key = Done(issue, "reallife_biz_supplierprice_switched");
                    }
                    break;

                case BusinessCatalogue.Complaint: key = Done(issue, "reallife_biz_complaint_ignored"); break;
                case BusinessCatalogue.Praise: Done(issue, "praise ran its course"); return;
                case BusinessCatalogue.Audit: key = Done(issue, AuditOutcome(issue, reg, false, data)); break;

                case BusinessCatalogue.BulkOrder:
                    if (issue.Stage == 0)
                    {
                        key = Done(issue, "reallife_biz_bulkorder_lapsed");
                    }
                    else if (BusinessGame.IsOpenNow(reg))
                    {
                        BusinessGame.Pay(reg, issue.A, "reallife_transaction_biz_bulkorder");
                        type = NotificationType.Success;
                        key = Done(issue, "reallife_biz_bulkorder_done");
                    }
                    else
                    {
                        type = NotificationType.Warning;
                        key = Done(issue, "reallife_biz_bulkorder_missed");
                    }
                    break;

                case BusinessCatalogue.Feature: key = Done(issue, "reallife_biz_feature_lapsed"); break;
                case BusinessCatalogue.Award: key = Done(issue, AwardOutcome(issue, reg)); type = NotificationType.Success; break;
                default: Done(issue, "unknown kind"); return;
            }
            Director.Tell(type, key, data);
        }

        // The health inspector calls: the game's own cleanliness figure decides.
        private static string Inspect(BusinessIssue issue, BuildingRegistration reg, Dictionary<string, string> data, out NotificationType type)
        {
            if (issue.B == 2)
            {
                type = NotificationType.Success;
                return Done(issue, "reallife_biz_bribe_passed");   // paid off: a formality
            }
            int clean = Inspected(reg, BusinessGame.Cleanliness(reg));
            data["clean"] = clean.ToString();
            if (InspectorCalls(issue, reg))
            {
                type = NotificationType.Info;
                return null;
            }
            if (clean >= BusinessCatalogue.HealthPass)
            {
                type = NotificationType.Success;
                if (issue.Stage == 0)
                {
                    Boost(reg);
                    return Done(issue, "reallife_biz_health_pass");
                }
                return Done(issue, "reallife_biz_health_pass_second");
            }
            if (clean >= BusinessCatalogue.HealthFail && issue.Stage == 0)
            {
                type = NotificationType.Warning;
                issue.Stage = 1;
                issue.B = 0;   // a contract clean may be booked again before they come back
                issue.C = 0;
                issue.DueDay = TimeHelper.CurrentDay + 2;
                data["day"] = issue.DueDay.ToString();
                return "reallife_biz_health_warning";
            }
            type = NotificationType.Error;
            return Fine(issue, reg, data, BusinessCatalogue.HealthFineShare, 100, 1500, "reallife_transaction_biz_healthfine",
                clean < BusinessCatalogue.HealthShut, BusinessCatalogue.HealthClosedHours, "reallife_biz_health_fail", "reallife_biz_health_fail_closed");
        }

        // With the player standing in the shop the inspector comes in and looks the place over first: the verdict is
        // put off by one game hour, once. True if that is what has just been arranged.
        private static bool InspectorCalls(BusinessIssue issue, BuildingRegistration reg)
        {
            if (issue.C != 0 || !Director.Visit(BusinessVisit.Kind.Inspector, reg, null))
            {
                return false;
            }
            issue.C = 1;
            issue.DueDay = TimeHelper.CurrentDay;
            issue.DueHour = TimeHelper.CurrentHour + 1;
            if (issue.DueHour > 23)
            {
                issue.DueHour = 0;
                issue.DueDay++;
            }
            return true;
        }

        // A fine, and a short closure if the failure was a bad one or the fine could not be paid.
        private static string Fine(BusinessIssue issue, BuildingRegistration reg, Dictionary<string, string> data, float share, int floor, int ceiling,
            string transaction, bool shut, int hours, string key, string closedKey)
        {
            int fine = BusinessGame.Fee(reg, share, floor, ceiling);
            bool paid = fine > 0 && BusinessGame.Charge(reg, fine, transaction, BusinessGame.Licensing);
            BusinessEvents.RaiseFined(issue.Kind, issue.Place, paid ? fine : 0);
            data["amount"] = Money(paid ? fine : 0);
            data["hours"] = hours.ToString();
            bool closed = (shut || !paid) && Director.CloseFor(reg, hours);
            return Done(issue, closed ? closedKey : key);
        }

        private static void Boost(BuildingRegistration reg)
        {
            if (BusinessGame.HasFootfall(reg))
            {
                Director.Effect(reg, BusinessCatalogue.PassBoost, BusinessCatalogue.PassBoostDays);
            }
        }

        // ---------------------------------------------------------------- watching

        /// <summary>Once a game hour for each open issue: the ones that end by something the game does.</summary>
        public static void Watch(BusinessIssue issue, BuildingRegistration reg)
        {
            if (issue.Kind != BusinessCatalogue.Poach)
            {
                return;
            }
            EmployeeInstance e = BusinessGame.Employee(RefId(issue));
            Dictionary<string, string> data = Data(issue, reg);
            if (e == null || e.assignedAddress == null || e.satisfaction <= 0f)
            {
                Director.Tell(NotificationType.Warning, Done(issue, "reallife_biz_poach_lost"), data);
            }
            else if (string.IsNullOrEmpty(e.poachedByRivalId))
            {
                Director.Tell(NotificationType.Success, Done(issue, "reallife_biz_poach_kept"), data);
            }
        }

        // ---------------------------------------------------------------- the racket between demands

        /// <summary>Once a game hour while the racket is anywhere but at a demand: payments, retaliation, the police.</summary>
        public static void RacketTick(int day, int hour)
        {
            RacketStage stage = BusinessState.Racket;
            if (stage == RacketStage.None || stage == RacketStage.Ended || stage == RacketStage.Demand)
            {
                return;
            }
            BuildingRegistration reg = BusinessGame.Find(BusinessState.RacketPlace);
            if (reg == null)
            {
                BusinessState.Racket = RacketStage.None;   // the business is gone, and they with it
                BusinessState.RacketRestUntil = day + BusinessCatalogue.RacketRestDays;
                return;
            }
            if (day < BusinessState.RacketDay || hour < BusinessCatalogue.RollHour)
            {
                return;
            }
            var data = new Dictionary<string, string>(6)
            {
                { "business", BusinessGame.Name(reg) }, { "amount", Money(BusinessState.RacketWeekly) }, { "security", BusinessGame.Security(reg).ToString() },
                { "time", BusinessCatalogue.RacketStingHour.GetFormattedTime(0f, true) }, { "hours", BusinessCatalogue.RacketClosedHours.ToString() }
            };
            switch (stage)
            {
                case RacketStage.Paying:
                    if (BusinessGame.Charge(reg, BusinessState.RacketWeekly, "reallife_transaction_biz_protection"))
                    {
                        BusinessState.RacketDay = day + 7;
                        int most = Mathf.RoundToInt(BusinessState.RacketFirst * BusinessCatalogue.RacketCreepMost);
                        BusinessState.RacketWeekly = Mathf.Min(most, Mathf.RoundToInt(BusinessState.RacketWeekly * BusinessCatalogue.RacketCreep));
                        data["next"] = Money(BusinessState.RacketWeekly);
                        Director.Tell(NotificationType.Info, "reallife_biz_racket_collected", data);
                    }
                    else
                    {
                        BusinessState.Racket = RacketStage.Refused;
                        BusinessState.RacketDay = day + 2;
                        Director.Tell(NotificationType.Warning, "reallife_biz_racket_short", data);
                    }
                    break;

                case RacketStage.Refused:
                {
                    float odds = BusinessCatalogue.RacketRetaliationOdds * (1f - BusinessGame.Security(reg) / 150f);
                    BusinessState.Racket = RacketStage.None;
                    BusinessState.RacketRestUntil = day + BusinessCatalogue.RacketRestDays;
                    if (Random.value < odds)
                    {
                        int repair = BusinessGame.Fee(reg, BusinessCatalogue.RacketRepairShare, 80, 900);
                        bool paid = BusinessGame.Charge(reg, repair, "reallife_transaction_biz_repair");
                        data["amount"] = Money(paid ? repair : 0);
                        bool closed = Director.CloseFor(reg, BusinessCatalogue.RacketClosedHours);
                        ModState.AddModifier(ModState.Robbed);
                        Director.Tell(NotificationType.Error, closed ? "reallife_biz_racket_vandal_closed" : "reallife_biz_racket_vandal", data);
                    }
                    else
                    {
                        Director.Tell(NotificationType.Info, "reallife_biz_racket_quiet", data);
                    }
                    break;
                }

                case RacketStage.Reported:
                {
                    float odds = Mathf.Min(BusinessCatalogue.RacketPoliceMost, BusinessCatalogue.RacketPoliceOdds + BusinessGame.Security(reg) / 200f);
                    if (Random.value < odds)
                    {
                        BusinessState.Racket = RacketStage.Sting;
                        BusinessState.RacketDay = day + 1;
                        BusinessState.RacketHour = BusinessCatalogue.RacketStingHour;
                        Director.Tell(NotificationType.Success, "reallife_biz_racket_sting_set", data);
                    }
                    else
                    {
                        BusinessState.Racket = RacketStage.Refused;
                        BusinessState.RacketDay = day + 2;
                        Director.Tell(NotificationType.Warning, "reallife_biz_racket_no_case", data);
                    }
                    break;
                }

                case RacketStage.Sting:
                    if (day == BusinessState.RacketDay && hour < BusinessState.RacketHour)
                    {
                        return;
                    }
                    BusinessState.Racket = RacketStage.Ended;
                    ModState.AddModifier(ModState.MoodUpBig);
                    BusinessDesk.Text("reallife_biz_racket_ended", data, false);   // for the record, however it is seen to happen
                    if (!Director.Visit(BusinessVisit.Kind.Sting, reg, null))
                    {
                        Director.Tell(NotificationType.Success, "reallife_biz_racket_ended_away", data);   // the arrest was made without them
                    }
                    BusinessGame.Logger?.Info("Business: the racket at " + BusinessGame.Name(reg) + " is over for good.");
                    break;

                case RacketStage.Securing:
                    if (BusinessGame.Security(reg) >= BusinessCatalogue.RacketSecurityLevel)
                    {
                        BusinessState.Racket = RacketStage.None;
                        BusinessState.RacketRestUntil = day + BusinessCatalogue.RacketRestDays * 2;
                        Director.Tell(NotificationType.Success, "reallife_biz_racket_backed_off", data);
                    }
                    else
                    {
                        BusinessState.Racket = RacketStage.Refused;
                        BusinessState.RacketDay = day + 1;
                        data["need"] = BusinessCatalogue.RacketSecurityLevel.ToString();
                        Director.Tell(NotificationType.Warning, "reallife_biz_racket_not_secure", data);
                    }
                    break;
            }
        }

        /// <summary>From the desk while paying: stop, or go to the police.</summary>
        public static string RacketChange(bool police)
        {
            if (BusinessState.Racket != RacketStage.Paying)
            {
                return "reallife_biz_desk_gone";
            }
            int day = TimeHelper.CurrentDay;
            if (police)
            {
                BusinessState.Racket = RacketStage.Reported;
                BusinessState.RacketDay = day + BusinessCatalogue.RacketInvestigationDays;
                return "reallife_biz_racket_reported";
            }
            BusinessState.Racket = RacketStage.Refused;
            BusinessState.RacketDay = day + Random.Range(2, 5);
            return "reallife_biz_racket_refused";
        }
    }
}
