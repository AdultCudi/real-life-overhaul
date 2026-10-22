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
    /// <summary>Business issues: how each kind starts, and the values its lines name.</summary>
    internal static partial class BusinessIssues
    {
        public static void Start(BusinessIssue issue, BuildingRegistration reg)
        {
            int day = TimeHelper.CurrentDay;
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
                    issue.Ref = e.id + "|" + BusinessGame.NameOf(e);   // the name too: once they have gone the game forgets it
                    e.PoachByRival(rival.id, BusinessCatalogue.PoachDays, BusinessCatalogue.PoachRaise);   // the game's own offer, in the employee's messages
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
                    issue.Ref = e.id + "|" + BusinessGame.NameOf(e);
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
                    // the delivery is at today's prices, so the bounds are too
                    float fee = late
                        ? Mathf.Clamp(price * BusinessCatalogue.ExpressShare, Prices.At(20f), Prices.At(400f))
                        : Mathf.Clamp(price * BusinessCatalogue.SurchargeShare, Prices.At(15f), Prices.At(500f));
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
                    issue.A = Prices.At(Mathf.Clamp(Mathf.RoundToInt(BusinessGame.DailyRevenue(reg) * BusinessCatalogue.AuditAccountantShare), 100, 800));   // chosen, so not cash-capped
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
            Dictionary<string, string> data = Data(issue, reg);
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

        /// <summary>The line the desk reads the issue out with: the one that announced it, with today's figures.</summary>
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

        /// <summary>Every value an issue's lines may name, in a new dictionary each time: the game keeps a notice's data.</summary>
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
    }
}
