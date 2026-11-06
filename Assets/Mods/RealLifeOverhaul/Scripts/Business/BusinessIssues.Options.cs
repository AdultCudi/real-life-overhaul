using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Business issues: the choices each kind offers, bribes included.</summary>
    internal static partial class BusinessIssues
    {
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
                    if (issue.B == 0 && issue.C == 0)   // nothing booked, and the inspector not yet in the shop
                    {
                        n = Paid(into, n, reg, "reallife_biz_health_do_clean", "reallife_biz_health_clean", BusinessCatalogue.HealthCleanShare, 40, 400);
                        n = Bribe(into, n, issue, reg);
                    }
                    break;

                case BusinessCatalogue.Fire:
                    if (issue.B == 0 && issue.C == 0)
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
                    into[n++] = new IssueOption
                    {
                        Label = "reallife_biz_supplierprice_do_switch", Text = "reallife_biz_supplierprice_switch", Amount = Mathf.Max(1, issue.A / 3),
                        Blocked = BusinessGame.Cash < issue.A / 3
                    };
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

        // An official who would wave an inspection or licence through for cash (BusinessDirector.BribeAsk): one more option.
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

        // A licence goes through; an inspection becomes a formality.
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
            issue.B = 2;   // paid off; 1 is the clean or the check booked
            return "reallife_biz_bribe_inspection_done";   // stays open: the inspector still comes
        }

        private static int Paid(IssueOption[] into, int n, BuildingRegistration reg, string label, string text, float share, int floor, int ceiling)
        {
            int amount = Mathf.Clamp(Mathf.RoundToInt(BusinessGame.DailyRevenue(reg) * share), floor, ceiling);
            // chosen, so the catalogue's price with no cash cap: it only has to be affordable
            into[n] = new IssueOption { Label = label, Text = text, Amount = amount, Blocked = BusinessGame.Cash < amount };
            return n + 1;
        }
    }
}
