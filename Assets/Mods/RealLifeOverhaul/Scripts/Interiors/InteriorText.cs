using System.Collections.Generic;
using System.Globalization;
using Localizor;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Interiors
{
    /// <summary>The feature's words: its keys filled from the figures. Built when a page is written or a toast shown, never per frame.</summary>
    public static class InteriorText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string T(string key, params string[] pairs)
        {
            return FinanceText.Fill(key, pairs);
        }

        /// <summary>A whole number of per cent with its sign: "+3%", "-2%", "0%".</summary>
        public static string Signed(int percent)
        {
            return (percent > 0 ? "+" : string.Empty) + percent.ToString(Inv) + "%";
        }

        /// <summary>Thousandths as per cent with one decimal and its sign: 25 is "+2.5%".</summary>
        public static string SignedMille(int mille)
        {
            return (mille > 0 ? "+" : string.Empty) + (mille / 10f).ToString("0.#", Inv) + "%";
        }

        /// <summary>Thousandths as per cent with one decimal: 25 is "2.5%".</summary>
        public static string Mille(int mille)
        {
            return (mille / 10f).ToString("0.#", Inv) + "%";
        }

        public static string N(int value)
        {
            return value.ToString(Inv);
        }

        /// <summary>A whole number with its sign: "+2", "-1", "0".</summary>
        public static string SignedN(int value)
        {
            return (value > 0 ? "+" : string.Empty) + value.ToString(Inv);
        }

        private static readonly string[] CategoryKeys =
        {
            "reallife_interior_cat_art", "reallife_interior_cat_greenery", "reallife_interior_cat_lighting", "reallife_interior_cat_audio",
            "reallife_interior_cat_floor", "reallife_interior_cat_furniture", "reallife_interior_cat_equipment"
        };

        // What to add next, by the decor category furthest under its share (furniture and equipment are never advised).
        private static readonly string[] NextKeys =
        {
            "reallife_interior_next_art", "reallife_interior_next_greenery", "reallife_interior_next_lighting", "reallife_interior_next_audio", "reallife_interior_next_floor"
        };

        /// <summary>A category's name.</summary>
        public static string Category(int category)
        {
            return CategoryKeys[InteriorRules.Clamp(category, 0, CategoryKeys.Length - 1)].GetLocalization();
        }

        /// <summary>The advice for the category to add to next; -1 for none (every one is at its share).</summary>
        public static string Next(int category)
        {
            return (category >= 0 && category < NextKeys.Length ? NextKeys[category] : "reallife_interior_next_full").GetLocalization();
        }

        /// <summary>How full a category is against its share, per cent.</summary>
        public static int Filled(InteriorShop s, int category)
        {
            int cap = (int)((long)s.Target * InteriorRules.CapShare[category] / 1000);
            return cap > 0 ? InteriorRules.Clamp((int)((long)s.Capped[category] * 100 / cap), 0, 100) : 0;
        }

        /// <summary>The figures of the test report's toast.</summary>
        public static Dictionary<string, string> Report(InteriorShop s)
        {
            return new Dictionary<string, string>(8)
            {
                { "business", s.Name }, { "interior", N(s.Interior) }, { "decor", N(s.DecorRaw) }, { "overall", N(s.Overall) }, { "standard", N(s.Standard) },
                { "wear", N(s.Wear / 10) }, { "footfall", Signed(s.Footfall) }, { "bears", SignedMille(s.Bears) }
            };
        }
    }
}
