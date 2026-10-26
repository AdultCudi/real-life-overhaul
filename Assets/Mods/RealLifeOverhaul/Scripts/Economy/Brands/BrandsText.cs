using System.Collections.Generic;
using System.Globalization;
using Localizor;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>The words of brands and franchises: every figure they show is written here, the same everywhere, from keys of en.json.</summary>
    public static class BrandsText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static readonly string[] TemperKeys = { "reallife_brand_temper_careful", "reallife_brand_temper_ambitious", "reallife_brand_temper_cutting" };
        public static readonly string[] TemperAboutKeys = { "reallife_brand_app_temper_about_0", "reallife_brand_app_temper_about_1", "reallife_brand_app_temper_about_2" };
        public static readonly string[] HoldKeys = { "reallife_brand_hold_opened", "reallife_brand_hold_converted", "reallife_brand_hold_books" };
        public static readonly string[] AskKeys =
        {
            "reallife_brand_ask_none", "reallife_brand_ask_help", "reallife_brand_ask_second", "reallife_brand_ask_renewal", "reallife_brand_ask_dispute", "reallife_brand_ask_bid"
        };
        public static readonly string[] RefusalKeys =
        {
            "reallife_brand_answer_yes", "reallife_brand_answer_dear", "reallife_brand_answer_capital", "reallife_brand_answer_territory", "reallife_brand_answer_full"
        };
        public static readonly string[] ChainKeys = { "reallife_brand_chain_0", "reallife_brand_chain_1", "reallife_brand_chain_2" };
        public static readonly string[] PushKeys = { "reallife_brand_push_local", "reallife_brand_push_city" };
        public static readonly string[] ScandalKeys = { "reallife_brand_scandal_inspection", "reallife_brand_scandal_strike", "reallife_brand_scandal_fire", "reallife_brand_scandal_influencer" };

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

        public static string Day(int day)
        {
            return FinanceText.Day(day);
        }

        /// <summary>Recognition kept in hundredths, as whole points: "49".</summary>
        public static string Points(int hundredths)
        {
            return (hundredths / 100).ToString(Inv);
        }

        /// <summary>Recognition kept in hundredths, to a tenth, as the Brands table shows it: "8.9".</summary>
        public static string Rec(int hundredths)
        {
            return (BrandRules.Shown(hundredths) / 10f).ToString("0.0", Inv);
        }

        /// <summary>Hundredths of a point with one decimal at most: "4", "1.5".</summary>
        public static string Lost(int hundredths)
        {
            return (hundredths / 100f).ToString("0.#", Inv);
        }

        /// <summary>A change in customers: "+6%".</summary>
        public static string Percent(int percent)
        {
            return (percent > 0 ? "+" : string.Empty) + percent.ToString(Inv) + "%";
        }

        /// <summary>Ten-thousandths as a per cent: "6%".</summary>
        public static string Bp(int bp)
        {
            return (bp / 100f).ToString("0.#", Inv) + "%";
        }

        /// <summary>Thousandths as a per cent: "95%".</summary>
        public static string Share(int thousandths)
        {
            return (thousandths / 10f).ToString("0.#", Inv) + "%";
        }

        public static string Temper(Temper temper)
        {
            return T(TemperKeys[BrandCatalogue.Clamp((int)temper, 0, TemperKeys.Length - 1)]);
        }

        /// <summary>What a temperament means for a franchise, in a sentence.</summary>
        public static string TemperAbout(Temper temper)
        {
            return T(TemperAboutKeys[BrandCatalogue.Clamp((int)temper, 0, TemperAboutKeys.Length - 1)]);
        }

        public static string Hold(Hold hold)
        {
            return T(HoldKeys[BrandCatalogue.Clamp((int)hold, 0, HoldKeys.Length - 1)]);
        }

        public static string Ask(Ask ask)
        {
            return T(AskKeys[BrandCatalogue.Clamp((int)ask, 0, AskKeys.Length - 1)]);
        }

        /// <summary>The names the refusals fill in: the limit that "full" speaks of, counted over every brand's franchised shops together.</summary>
        public static Dictionary<string, string> RefusalData()
        {
            return new Dictionary<string, string> { { "most", BrandCatalogue.FranchisesMost.ToString(Inv) } };
        }

        public static string Answer(Refusal refusal)
        {
            return T(RefusalKeys[BrandCatalogue.Clamp((int)refusal, 0, RefusalKeys.Length - 1)], "most", BrandCatalogue.FranchisesMost.ToString(Inv));
        }

        public static string Push(int kind)
        {
            return T(PushKeys[BrandCatalogue.Clamp(kind, 0, PushKeys.Length - 1)]);
        }

        public static string Scandal(Scandal kind)
        {
            return T(ScandalKeys[BrandCatalogue.Clamp((int)kind, 0, ScandalKeys.Length - 1)]);
        }

        public static string Chain(int chain)
        {
            return T(ChainKeys[BrandCatalogue.Clamp(chain, 0, ChainKeys.Length - 1)]);
        }

        /// <summary>A business type's name as the game writes it.</summary>
        public static string Type(string type)
        {
            return string.IsNullOrEmpty(type) ? string.Empty : LocalizorManager.IsLocalizedKey(type) ? type.GetLocalization() : type;
        }

        /// <summary>A district's name as the game writes it.</summary>
        public static string District(string district)
        {
            return string.IsNullOrEmpty(district) ? T("reallife_brand_district_none") : LocalizorManager.IsLocalizedKey(district) ? district.GetLocalization() : district;
        }

        /// <summary>A franchised shop by its brand and who runs it, as the map and the Planner call it.</summary>
        public static string Shop(Brand b, Franchise f)
        {
            return T("reallife_brand_shop_name", "brand", b != null ? b.Name : "?", "who", f.Name);
        }

        /// <summary>A minimum for standards: its points, or "none".</summary>
        public static string Minimum(int points)
        {
            return points > 0 ? points.ToString(Inv) : T("reallife_brand_none");
        }
    }
}
