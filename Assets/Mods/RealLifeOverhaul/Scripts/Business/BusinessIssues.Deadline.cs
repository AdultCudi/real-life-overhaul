using System.Collections.Generic;
using Entities;
using Helpers;
using Localizor;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Business issues: what a deadline brings, the inspections' verdicts, and the racket between demands.</summary>
    internal static partial class BusinessIssues
    {
        /// <summary>The deadline has come with nothing more chosen: the default happens, and the issue ends or moves on.</summary>
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
                        return;   // the inspector is walking round the shop: the verdict comes after
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

        // With the player in the shop the inspector comes in and looks round first: the verdict waits one game hour, once.
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

        // A fine, and a short closure if the failure was bad or the fine could not be paid.
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
                    BusinessDesk.Text("reallife_biz_racket_ended", data, false);   // for the record, however it is seen
                    if (!Director.Visit(BusinessVisit.Kind.Sting, reg, null))
                    {
                        Director.Tell(NotificationType.Success, "reallife_biz_racket_ended_away", data);
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
