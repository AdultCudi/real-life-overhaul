using System.Globalization;
using System.Text;
using Localizor;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The figures the Guide's page "Running a business" prints: every {name} in its text is filled from
    /// <see cref="CompanyCatalogue"/> here, so the page can never say anything the rules do not do. Called once, when
    /// the feature list is built (Meta/FeatureList); each figure is worked out when the page is opened.
    /// </summary>
    public static class CompanyGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] LevelKeys = { "reallife_meta_preset_relaxed", "reallife_meta_preset_standard", "reallife_meta_preset_realistic", "reallife_meta_preset_hardcore" };

        public static void AddFacts(Feature f)
        {
            int standard = CompanyCatalogue.Standard;
            f.Fact("co_market_low", () => Tenths(CompanyCatalogue.WageMarketLeast))
                .Fact("co_market_high", () => Tenths(CompanyCatalogue.WageMarketMost))
                .Fact("co_raise_gap", () => Tenths(CompanyCatalogue.RaiseGap[standard]))
                .Fact("co_raise_days", () => Whole(CompanyCatalogue.RaiseDays[standard]))
                .Fact("co_answer_days", () => Whole(CompanyCatalogue.RaiseAnswerDays))
                .Fact("co_raise_rest", () => Whole(CompanyCatalogue.RaiseRestDays))
                .Fact("co_stand_start", () => Whole(CompanyCatalogue.StandingStart))
                .Fact("co_stand_calm", () => Whole(CompanyCatalogue.StandingCalm))
                .Fact("co_stand_agree", () => Whole(CompanyCatalogue.StandAgree))
                .Fact("co_stand_less", () => Whole(-CompanyCatalogue.StandLess))
                .Fact("co_stand_refuse", () => Whole(-CompanyCatalogue.StandRefuse))
                .Fact("co_poach_skill", () => Whole(CompanyCatalogue.PoachSkill))
                .Fact("co_poach_gap", () => Tenths(CompanyCatalogue.PoachGap))
                .Fact("co_poach_premium", () => Tenths(CompanyCatalogue.PoachPremium))
                .Fact("co_poach_answer", () => Whole(CompanyCatalogue.PoachAnswerDays))
                .Fact("co_poach_notice", () => Whole(CompanyCatalogue.PoachNoticeDays))
                .Fact("co_strike_share", () => Tenths(CompanyCatalogue.StrikeShare))
                .Fact("co_strike_standing", () => Whole(CompanyCatalogue.StrikeStanding))
                .Fact("co_strike_footfall", () => Whole(-CompanyCatalogue.StrikeFootfall))
                .Fact("co_strike_hours", () => Whole(CompanyCatalogue.StrikeClosedHours))
                .Fact("co_strike_pay_hours", () => Whole(CompanyCatalogue.StrikePayHours))
                .Fact("co_strike_rest", () => Whole(CompanyCatalogue.StrikeRestDays))
                .Fact("co_ot_hours", () => Whole(CompanyCatalogue.OvertimeHours))
                .Fact("co_ot_days", () => Whole(CompanyCatalogue.OvertimeDays))
                .Fact("co_ot_footfall", () => Whole(CompanyCatalogue.OvertimeFootfall))
                .Fact("co_sick_none", () => Signed(CompanyCatalogue.SickStanding[0]))
                .Fact("co_sick_statutory", () => Signed(CompanyCatalogue.SickStanding[1]))
                .Fact("co_sick_generous", () => Signed(CompanyCatalogue.SickStanding[2]))
                .Fact("co_levels", Levels)
                .Fact("co_claim_window", () => Whole(CompanyCatalogue.ClaimWindowDays))
                .Fact("co_claim_loading", () => Tenths(CompanyCatalogue.ClaimLoading))
                .Fact("co_premium_missed", () => Whole(CompanyCatalogue.PremiumMissedMost))
                .Fact("co_insurers", Insurers)
                .Fact("co_covers", Covers)
                .Fact("co_campaigns", Campaigns)
                .Fact("co_campaign_most", () => Whole(CompanyCatalogue.CampaignMost))
                .Fact("co_repeat_days", () => Whole(CompanyCatalogue.RepeatDays))
                .Fact("co_reports", () => Whole(CompanyCatalogue.ReportsKept))
                .Fact("co_return_days", () => Whole(CompanyCatalogue.ReturnDays))
                .Fact("co_penalty_share", () => Tenths(CompanyCatalogue.PenaltyShare))
                .Fact("co_penalty_least", () => Guide.Money(CompanyCatalogue.PenaltyLeast))
                .Fact("co_penalty_most", () => Guide.Money(CompanyCatalogue.PenaltyMost))
                .Fact("co_penalty_repeat", () => Whole(CompanyCatalogue.PenaltyRepeatDays))
                .Fact("co_penalties_most", () => Whole(CompanyCatalogue.PenaltiesMost))
                .Fact("co_late_audit", () => Whole(CompanyCatalogue.LateForAudit))
                .Fact("co_accountants", Accountants)
                .Fact("co_error_chance", () => Tenths(CompanyCatalogue.AccountantErrorChance))
                .Fact("co_hide_fee", () => Tenths(CompanyCatalogue.AccountantHidingFee - 1000))
                .Fact("co_hide_steps", HideSteps)
                .Fact("co_hide_relaxed", () => Whole(CompanyCatalogue.HideMostRelaxed))
                .Fact("co_risk_per", () => (CompanyCatalogue.RiskPerPercent / 100f).ToString("0.##", Inv))
                .Fact("co_risk_decay", () => (CompanyCatalogue.RiskDecay / 100f).ToString("0.##", Inv))
                .Fact("co_risk_most", () => (CompanyCatalogue.RiskMost / 100f).ToString("0.#", Inv))
                .Fact("co_fines", Fines)
                .Fact("co_court", () => Guide.Money(CompanyCatalogue.CourtSum))
                .Fact("co_licences", Licences)
                .Fact("co_lic_grace", () => Whole(CompanyCatalogue.LicenceGraceDays))
                .Fact("co_lic_remind", () => Whole(CompanyCatalogue.LicenceRemindDays))
                .Fact("co_lic_fine", () => Whole(CompanyCatalogue.LicenceFineMultiple))
                .Fact("co_lic_hours", () => Whole(CompanyCatalogue.LicenceClosedHours))
                .Fact("co_late_hour", () => Whole(CompanyCatalogue.LateHour))
                .Fact("co_early_hour", () => Whole(CompanyCatalogue.EarlyHour))
                .Fact("co_loss_days", () => Whole(CompanyCatalogue.LossRunDays));
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }

        private static string Signed(int value)
        {
            return (value > 0 ? "+" : string.Empty) + value.ToString(Inv);
        }

        // Thousandths as a per cent without its sign: "15", "2.5".
        private static string Tenths(int thousandths)
        {
            return (thousandths / 10f).ToString("0.#", Inv);
        }

        // One line per strictness: when staff ask, how long a strike lasts, how often a rival approaches.
        private static string Levels()
        {
            var text = new StringBuilder();
            for (int l = 0; l < LevelKeys.Length; l++)
            {
                bool quiet = CompanyCatalogue.StrikeDays[l] == 0 && CompanyCatalogue.PoachChance[l] == 0;
                text.Append("- ").Append((quiet ? "reallife_guide_economy_company_level_quiet" : "reallife_guide_economy_company_level").GetLocalization()
                    .Replace("{level}", LevelKeys[l].GetLocalization())
                    .Replace("{gap}", Tenths(CompanyCatalogue.RaiseGap[l]))
                    .Replace("{days}", Whole(CompanyCatalogue.RaiseDays[l]))
                    .Replace("{strike}", Whole(CompanyCatalogue.StrikeDays[l]))
                    .Replace("{poach}", Whole(CompanyCatalogue.PoachChance[l]))).Append('\n');
            }
            return text.ToString();
        }

        private static string Insurers()
        {
            var text = new StringBuilder();
            for (int i = 0; i < CompanyCatalogue.Insurers; i++)
            {
                text.Append("- ").Append("reallife_guide_economy_company_insurer".GetLocalization()
                    .Replace("{name}", CompanyText.Insurer(i))
                    .Replace("{price}", Tenths(CompanyCatalogue.InsurerPrice[i]))
                    .Replace("{share}", Tenths(CompanyCatalogue.InsurerExcessShare[i]))
                    .Replace("{least}", Guide.Money(CompanyCatalogue.InsurerExcessLeast[i]))
                    .Replace("{days}", Whole(CompanyCatalogue.InsurerPayDays[i]))
                    .Replace("{claims}", Whole(CompanyCatalogue.InsurerClaimsMost[i] - 1))).Append('\n');
            }
            return text.ToString();
        }

        private static string Covers()
        {
            var text = new StringBuilder();
            for (int k = 0; k < CompanyCatalogue.CoverKinds; k++)
            {
                text.Append("- ").Append("reallife_guide_economy_company_cover".GetLocalization()
                    .Replace("{name}", CompanyText.Cover(k))
                    .Replace("{base}", Guide.Money(CompanyCatalogue.CoverBase[k]))
                    .Replace("{share}", Tenths(CompanyCatalogue.CoverTakingsShare[k]))
                    .Replace("{most}", Guide.Money(CompanyCatalogue.CoverMost[k]))).Append('\n');
            }
            return text.ToString();
        }

        private static string Campaigns()
        {
            var text = new StringBuilder();
            for (int k = 0; k < CompanyCatalogue.CampaignKinds; k++)
            {
                bool free = CompanyCatalogue.CampaignCost[k] == 0;
                text.Append("- ").Append((free ? "reallife_guide_economy_company_campaign_free" : "reallife_guide_economy_company_campaign").GetLocalization()
                    .Replace("{name}", CompanyText.Campaign(k))
                    .Replace("{cost}", Guide.Money(CompanyCatalogue.CampaignCost[k]))
                    .Replace("{share}", Tenths(CompanyCatalogue.DiscountShare))
                    .Replace("{days}", Whole(CompanyCatalogue.CampaignDays[k]))
                    .Replace("{percent}", Whole(CompanyCatalogue.CampaignFootfall[k]))).Append('\n');
            }
            return text.ToString();
        }

        private static string Accountants()
        {
            var text = new StringBuilder();
            for (int i = 0; i < CompanyCatalogue.Accountants; i++)
            {
                text.Append("- ").Append("reallife_guide_economy_company_accountant".GetLocalization()
                    .Replace("{name}", CompanyText.Accountant(i))
                    .Replace("{fee}", Guide.Money(CompanyCatalogue.AccountantFee[i]))
                    .Replace("{saving}", Tenths(CompanyCatalogue.AccountantSaving[i]))
                    .Replace("{risk}", Tenths(CompanyCatalogue.AccountantRisk[i]))).Append('\n');
            }
            return text.ToString();
        }

        private static string Licences()
        {
            var text = new StringBuilder();
            for (int k = 0; k < CompanyCatalogue.LicenceKinds; k++)
            {
                text.Append("- ").Append("reallife_guide_economy_company_licence".GetLocalization()
                    .Replace("{name}", CompanyText.Capital(CompanyText.Licence(k)))
                    .Replace("{fee}", Guide.Money(CompanyCatalogue.LicenceFee[k]))
                    .Replace("{days}", Whole(CompanyCatalogue.LicenceTerm[k]))).Append('\n');
            }
            return text.ToString();
        }

        // "0, 5, 10, 20 or 30"
        private static string HideSteps()
        {
            var text = new StringBuilder();
            int[] steps = CompanyCatalogue.HideSteps;
            for (int i = 0; i < steps.Length; i++)
            {
                text.Append(i == 0 ? string.Empty : i == steps.Length - 1 ? " " + "reallife_guide_economy_company_or".GetLocalization() + " " : ", ").Append(Whole(steps[i]));
            }
            return text.ToString();
        }

        // "25% on Relaxed, 50% on Standard, 75% on Realistic, 100% on Hardcore"
        private static string Fines()
        {
            var text = new StringBuilder();
            for (int l = 0; l < LevelKeys.Length; l++)
            {
                text.Append(l > 0 ? ", " : string.Empty).Append("reallife_guide_economy_company_fine".GetLocalization()
                    .Replace("{share}", Tenths(CompanyCatalogue.FineShare[l])).Replace("{level}", LevelKeys[l].GetLocalization()));
            }
            return text.ToString();
        }
    }
}
