using System.Globalization;
using Localizor;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The property business's words: every text is a key of the locale file, filled in here, with money, days and
    /// shares written as the Finance app writes them. Nothing is kept; a text is made when a page is written or a
    /// notice is sent.
    /// </summary>
    public static class EstateText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static readonly string[] UseKeys = { "reallife_estate_use_homes", "reallife_estate_use_shops", "reallife_estate_use_offices", "reallife_estate_use_storage" };
        public static readonly string[] WorkKeys = { "reallife_estate_work_none", "reallife_estate_work_repair", "reallife_estate_work_refurbish", "reallife_estate_work_planning", "reallife_estate_work_convert" };
        public static readonly string[] CycleKeys = { "reallife_estate_cycle_boom", "reallife_estate_cycle_steady", "reallife_estate_cycle_slowdown", "reallife_estate_cycle_recession" };
        public static readonly string[] StaffKeys = { "reallife_estate_staff_basic", "reallife_estate_staff_standard", "reallife_estate_staff_excellent" };
        public static readonly string[] ReviewKeys =
        {
            "reallife_estate_review_none", "reallife_estate_review_clean", "reallife_estate_review_dirty", "reallife_estate_review_staff", "reallife_estate_review_no_staff", "reallife_estate_review_value",
            "reallife_estate_review_dear"
        };
        public static readonly string[] CarStateKeys = { "reallife_estate_car_idle", "reallife_estate_car_out", "reallife_estate_car_valet", "reallife_estate_car_waiting" };
        public static readonly string[] ClassKeys = { "reallife_estate_class_economy", "reallife_estate_class_standard", "reallife_estate_class_premium", "reallife_estate_class_luxury" };
        public static readonly string[] StageKeys = { "reallife_estate_stage_offered", "reallife_estate_stage_marketing", "reallife_estate_stage_completing" };
        /// <summary>Who a tenant is, by the building's use: eight descriptions each. Never a name.</summary>
        private static readonly string[][] TenantKeys =
        {
            new[] { "reallife_estate_tenant_homes_0", "reallife_estate_tenant_homes_1", "reallife_estate_tenant_homes_2", "reallife_estate_tenant_homes_3",
                    "reallife_estate_tenant_homes_4", "reallife_estate_tenant_homes_5", "reallife_estate_tenant_homes_6", "reallife_estate_tenant_homes_7" },
            new[] { "reallife_estate_tenant_shops_0", "reallife_estate_tenant_shops_1", "reallife_estate_tenant_shops_2", "reallife_estate_tenant_shops_3",
                    "reallife_estate_tenant_shops_4", "reallife_estate_tenant_shops_5", "reallife_estate_tenant_shops_6", "reallife_estate_tenant_shops_7" },
            new[] { "reallife_estate_tenant_offices_0", "reallife_estate_tenant_offices_1", "reallife_estate_tenant_offices_2", "reallife_estate_tenant_offices_3",
                    "reallife_estate_tenant_offices_4", "reallife_estate_tenant_offices_5", "reallife_estate_tenant_offices_6", "reallife_estate_tenant_offices_7" },
            new[] { "reallife_estate_tenant_storage_0", "reallife_estate_tenant_storage_1", "reallife_estate_tenant_storage_2", "reallife_estate_tenant_storage_3",
                    "reallife_estate_tenant_storage_4", "reallife_estate_tenant_storage_5", "reallife_estate_tenant_storage_6", "reallife_estate_tenant_storage_7" }
        };

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

        public static string Pair(string labelKey, string value)
        {
            return FinanceText.Pair(labelKey, value);
        }

        /// <summary>Thousandths as a per cent: "105%", "6.5%".</summary>
        public static string Share(int thousandths)
        {
            return (thousandths / 10f).ToString("0.#", Inv) + "%";
        }

        /// <summary>Hundredths of a per cent as a per cent: "5.2%".</summary>
        public static string Yield(int hundredths)
        {
            return (hundredths / 100f).ToString("0.#", Inv) + "%";
        }

        /// <summary>A condition, a cleanliness or a reputation of 0 to 1000 as a per cent.</summary>
        public static string Level(int thousand)
        {
            return (EstateCatalogue.Clamp(thousand, 0, 1000) / 10).ToString(Inv) + "%";
        }

        public static string Use(int use)
        {
            return T(UseKeys[EstateCatalogue.Clamp(use, 0, UseKeys.Length - 1)]);
        }

        public static string Work(int work)
        {
            return T(WorkKeys[EstateCatalogue.Clamp(work, 0, WorkKeys.Length - 1)]);
        }

        public static string Cycle(int cycle)
        {
            return T(CycleKeys[EstateCatalogue.Clamp(cycle, 0, CycleKeys.Length - 1)]);
        }

        public static string Staff(int level)
        {
            return T(StaffKeys[EstateCatalogue.Clamp(level, 0, StaffKeys.Length - 1)]);
        }

        public static string Review(int review)
        {
            return T(ReviewKeys[EstateCatalogue.Clamp(review, 0, ReviewKeys.Length - 1)]);
        }

        public static string CarState(int state)
        {
            return T(CarStateKeys[EstateCatalogue.Clamp(state, 0, CarStateKeys.Length - 1)]);
        }

        public static string CarClass(int carClass)
        {
            return T(ClassKeys[EstateCatalogue.Clamp(carClass, 0, ClassKeys.Length - 1)]);
        }

        public static string Stage(int stage)
        {
            return T(StageKeys[EstateCatalogue.Clamp(stage, 0, StageKeys.Length - 1)]);
        }

        /// <summary>Who a tenant or an applicant is: one of eight descriptions for the building's use. Never a name.</summary>
        public static string Tenant(int use, int kind)
        {
            string[] kinds = TenantKeys[EstateCatalogue.Clamp(use, 0, TenantKeys.Length - 1)];
            return T(kinds[EstateCatalogue.Clamp(kind, 0, kinds.Length - 1)]);
        }

        /// <summary>A record of 20 to 95 in a word.</summary>
        public static string Record(int record)
        {
            return T(record >= 80 ? "reallife_estate_record_good" : record >= 60 ? "reallife_estate_record_fair" : record >= 40 ? "reallife_estate_record_patchy" : "reallife_estate_record_poor");
        }

        /// <summary>A district's name as the game writes it.</summary>
        public static string District(string district)
        {
            return CompanyText.District(district);
        }

        public static string Stars(int rating)
        {
            return T("reallife_estate_stars", "stars", HotelRules.Stars(rating).ToString(Inv));
        }
    }
}
