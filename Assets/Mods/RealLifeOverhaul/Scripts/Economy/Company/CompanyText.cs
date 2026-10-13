using System.Globalization;
using Extensions;
using Localizor;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The company's words: every figure it shows is written here, the same way in the Company app, the Planner, the
    /// paper and a notification. Every text is a key of en.json; money is written as the Finance app writes it.
    /// Called when something is shown or said, never otherwise.
    /// </summary>
    public static class CompanyText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static readonly string[] CoverKeys = { "reallife_co_cover_fire", "reallife_co_cover_theft", "reallife_co_cover_liability" };
        public static readonly string[] InsurerKeys = { "reallife_co_insurer_0", "reallife_co_insurer_1", "reallife_co_insurer_2" };
        public static readonly string[] CampaignKeys =
        {
            "reallife_co_campaign_flyers", "reallife_co_campaign_radio", "reallife_co_campaign_discount", "reallife_co_campaign_refit", "reallife_co_campaign_sponsored",
            "reallife_co_campaign_inf_post", "reallife_co_campaign_inf_series", "reallife_co_campaign_inf_ambassador", "reallife_co_campaign_inf_plan",
            "reallife_co_campaign_inf_launch", "reallife_co_campaign_inf_giveaway"
        };
        public static readonly string[] LicenceKeys = { "reallife_co_licence_hygiene", "reallife_co_licence_alcohol", "reallife_co_licence_late", "reallife_co_licence_signage" };
        public static readonly string[] AccountantKeys = { "reallife_co_accountant_0", "reallife_co_accountant_1", "reallife_co_accountant_2" };
        public static readonly string[] SickKeys = { "reallife_co_sick_none", "reallife_co_sick_statutory", "reallife_co_sick_generous" };
        public static readonly string[] CostKeys = { "reallife_co_deduct_wages", "reallife_co_deduct_premiums", "reallife_co_deduct_marketing", "reallife_co_deduct_licences", "reallife_co_deduct_accountant" };

        public static string T(string key)
        {
            return FinanceText.T(key);
        }

        public static string T(string key, string name, string value)
        {
            return FinanceText.T(key, name, value);
        }

        public static string T(string key, string name, string value, string name2, string value2)
        {
            return FinanceText.T(key, name, value, name2, value2);
        }

        public static string T(string key, string name, string value, string name2, string value2, string name3, string value3)
        {
            return FinanceText.T(key, name, value, name2, value2, name3, value3);
        }

        /// <summary>A text with any number of its names filled in: name, value, name, value.</summary>
        public static string Fill(string key, params string[] pairs)
        {
            return FinanceText.Fill(key, pairs);
        }

        public static string Money(float amount)
        {
            return FinanceText.Money(amount);
        }

        /// <summary>An hourly wage kept in hundredths, as the game writes a wage: with its cents.</summary>
        public static string Wage(int hundredths)
        {
            return (hundredths / 100f).ToCurrencyFormat();
        }

        public static string Day(int day)
        {
            return FinanceText.Day(day);
        }

        public static string Percent(int percent)
        {
            return (percent > 0 ? "+" : string.Empty) + percent.ToString(Inv) + "%";
        }

        /// <summary>Thousandths as a per cent: "15%", "2.5%".</summary>
        public static string Share(int thousandths)
        {
            return (thousandths / 10f).ToString("0.#", Inv) + "%";
        }

        public static string Cover(int kind)
        {
            return T(CoverKeys[CompanyCatalogue.Clamp(kind, 0, CoverKeys.Length - 1)]);
        }

        public static string Insurer(int insurer)
        {
            return T(InsurerKeys[CompanyCatalogue.Clamp(insurer, 0, InsurerKeys.Length - 1)]);
        }

        public static string Campaign(int kind)
        {
            return T(CampaignKeys[CompanyCatalogue.Clamp(kind, 0, CampaignKeys.Length - 1)]);
        }

        public static string Licence(int kind)
        {
            return T(LicenceKeys[CompanyCatalogue.Clamp(kind, 0, LicenceKeys.Length - 1)]);
        }

        public static string Accountant(int tier)
        {
            return tier < 0 ? T("reallife_co_accountant_none") : T(AccountantKeys[CompanyCatalogue.Clamp(tier, 0, AccountantKeys.Length - 1)]);
        }

        /// <summary>A name at the head of a line or a cell: with a capital.</summary>
        public static string Capital(string name)
        {
            return string.IsNullOrEmpty(name) ? name : char.ToUpper(name[0], Inv) + name.Substring(1);
        }

        /// <summary>A district's name as the game writes it.</summary>
        public static string District(string district)
        {
            return string.IsNullOrEmpty(district) ? T("reallife_co_district_none") : LocalizorManager.IsLocalizedKey(district) ? district.GetLocalization() : district;
        }

        /// <summary>The licences of a set, by name: "food hygiene, signage".</summary>
        public static string Licences(int mask)
        {
            string list = string.Empty;
            for (int k = 0; k < LicenceKeys.Length; k++)
            {
                if ((mask & (1 << k)) != 0)
                {
                    list += (list.Length > 0 ? ", " : string.Empty) + T(LicenceKeys[k]);
                }
            }
            return list;
        }
    }
}
