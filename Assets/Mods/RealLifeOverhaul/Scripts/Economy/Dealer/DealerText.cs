using System.Globalization;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>The showroom's words and figures, written the same in the app and in notifications.</summary>
    public static class DealerText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly string[] KindKeys = { "reallife_dealer_kind_wholesale", "reallife_dealer_kind_used", "reallife_dealer_kind_auction" };
        private static readonly string[] SourceKeys =
        {
            "reallife_dealer_kind_wholesale", "reallife_dealer_kind_used", "reallife_dealer_kind_auction", "reallife_dealer_kind_trade"
        };
        private static readonly string[] ProfileKeys = { "reallife_dealer_profile_bargain", "reallife_dealer_profile_fair", "reallife_dealer_profile_keen" };
        private static readonly string[] TierKeys = { "reallife_dealer_tier_basic", "reallife_dealer_tier_extended", "reallife_dealer_tier_complete" };
        private static readonly string[] StepKeys =
        {
            "reallife_dealer_step_0", "reallife_dealer_step_1", "reallife_dealer_step_2", "reallife_dealer_step_3", "reallife_dealer_step_4"
        };
        private static readonly string[] PerkKeys =
        {
            "reallife_dealer_perk_none", "reallife_dealer_perk_1", "reallife_dealer_perk_2", "reallife_dealer_perk_3", "reallife_dealer_perk_4"
        };
        private static readonly string[] MissionKeys =
        {
            "reallife_dealer_mission_0", "reallife_dealer_mission_1", "reallife_dealer_mission_2", "reallife_dealer_mission_3", "reallife_dealer_mission_4",
            "reallife_dealer_mission_5", "reallife_dealer_mission_6", "reallife_dealer_mission_7", "reallife_dealer_mission_8", "reallife_dealer_mission_9",
            "reallife_dealer_mission_10", "reallife_dealer_mission_11"
        };
        private static readonly string[] MissionAboutKeys =
        {
            "reallife_dealer_mission_0_about", "reallife_dealer_mission_1_about", "reallife_dealer_mission_2_about", "reallife_dealer_mission_3_about",
            "reallife_dealer_mission_4_about", "reallife_dealer_mission_5_about", "reallife_dealer_mission_6_about", "reallife_dealer_mission_7_about",
            "reallife_dealer_mission_8_about", "reallife_dealer_mission_9_about", "reallife_dealer_mission_10_about", "reallife_dealer_mission_11_about"
        };
        private static readonly string[] CarKindKeys =   // the car catalogue's kinds 1 to 6
        {
            "reallife_dealer_carkind_1", "reallife_dealer_carkind_2", "reallife_dealer_carkind_3", "reallife_dealer_carkind_4", "reallife_dealer_carkind_5",
            "reallife_dealer_carkind_6"
        };
        private static readonly string[] EventKeys = { "reallife_dealer_event_0", "reallife_dealer_event_1", "reallife_dealer_event_2", "reallife_dealer_event_3" };
        private static readonly string[] EventShortKeys =
        {
            "reallife_dealer_event_0_short", "reallife_dealer_event_1_short", "reallife_dealer_event_2_short", "reallife_dealer_event_3_short"
        };
        private static readonly string[] EventAboutKeys =
        {
            "reallife_dealer_event_0_about", "reallife_dealer_event_1_about", "reallife_dealer_event_2_about", "reallife_dealer_event_3_about"
        };
        private static readonly string[] AddonKeys = { "reallife_dealer_addon_0", "reallife_dealer_addon_1", "reallife_dealer_addon_2", "reallife_dealer_addon_3" };
        private static readonly string[] FaultKeys =
        {
            "reallife_dealer_fault_tyres", "reallife_dealer_fault_brakes", "reallife_dealer_fault_suspension", "reallife_dealer_fault_body", "reallife_dealer_fault_engine",
            "reallife_dealer_fault_interior"
        };

        public static string T(string key) => FinanceText.T(key);

        public static string T(string key, string name, string value) => FinanceText.T(key, name, value);

        public static string T(string key, string name, string value, string name2, string value2) => FinanceText.T(key, name, value, name2, value2);

        public static string Fill(string key, params string[] pairs) => FinanceText.Fill(key, pairs);

        public static string Money(int amount) => FinanceText.Money(amount);

        public static string Km(int km) => T("reallife_dealer_km", "km", km.ToString("N0", Inv));

        /// <summary>Thousandths of condition, as a per cent.</summary>
        public static string Condition(int thousandths) => T("reallife_dealer_condition_value", "percent", ((thousandths + 5) / 10).ToString(Inv));

        public static string Share(int thousandths) => T("reallife_dealer_share", "percent", ((thousandths + 5) / 10).ToString(Inv));

        public static string Faults(int faults)
        {
            if (faults == 0)
            {
                return T("reallife_dealer_faults_none");
            }
            var t = new System.Text.StringBuilder(64);
            for (int f = 0; f < DealerCatalogue.Faults; f++)
            {
                if ((faults & (1 << f)) != 0)
                {
                    t.Append(t.Length > 0 ? ", " : string.Empty).Append(T(FaultKeys[f]));
                }
            }
            return t.ToString();
        }

        public static string Kind(ListingKind kind) => T(KindKeys[(int)kind]);

        public static string Source(CarSource source) => T(SourceKeys[(int)source]);

        public static string Profile(int profile) => T(ProfileKeys[DealerCatalogue.Clamp(profile, 0, ProfileKeys.Length - 1)]);

        public static string Tier(int tier) => tier < 0 ? T("reallife_dealer_tier_none") : T(TierKeys[DealerCatalogue.Clamp(tier, 0, TierKeys.Length - 1)]);

        /// <summary>An hour of the game (day x 24 + hour) as the day and the clock.</summary>
        public static string When(int hourOfGame)
        {
            int day = hourOfGame / 24, hour = hourOfGame % 24;
            return T("reallife_dealer_when", "day", FinanceText.Day(day), "hour", hour.ToString("00", Inv) + ":00");
        }

        /// <summary>A number of hours left; one or none reads "under an hour".</summary>
        public static string Hours(int hours) => hours <= 1 ? T("reallife_dealer_hours_one") : T("reallife_dealer_hours", "hours", hours.ToString(Inv));

        public static string Step(int step) => T(StepKeys[DealerCatalogue.Clamp(step, 0, StepKeys.Length - 1)]);

        /// <summary>The perk a step of the reputation opens; step 0 has none.</summary>
        public static string Perk(int step) => T(PerkKeys[DealerCatalogue.Clamp(step, 0, PerkKeys.Length - 1)]);

        public static string Mission(int mission) => T(MissionKeys[DealerCatalogue.Clamp(mission, 0, MissionKeys.Length - 1)]);

        /// <summary>What a mission asks, with its target.</summary>
        public static string MissionAbout(int mission) => Fill(MissionAboutKeys[DealerCatalogue.Clamp(mission, 0, MissionAboutKeys.Length - 1)],
            "target", DealerCatalogue.MissionTarget[mission].ToString(Inv), "days", DealerCatalogue.QuickDays.ToString(Inv), "share", Share(DealerCatalogue.GoodMargin));

        /// <summary>A kind of car of the mod's car catalogue (1 economy to 6 vans), as a featured event names it.</summary>
        public static string CarKind(int kind) => T(CarKindKeys[DealerCatalogue.Clamp(kind, 1, CarKindKeys.Length) - 1]);

        /// <summary>An event's name; a featured event's with its kind of car.</summary>
        public static string Event(int kind, int feature)
        {
            if (kind == DealerCatalogue.FeaturedKind && feature > 0)
            {
                return T("reallife_dealer_event_featured_of", "kind", CarKind(feature));
            }
            return T(EventKeys[DealerCatalogue.Clamp(kind, 0, EventKeys.Length - 1)]);
        }

        public static string EventShort(int kind) => T(EventShortKeys[DealerCatalogue.Clamp(kind, 0, EventShortKeys.Length - 1)]);

        public static string EventAbout(int kind) => T(EventAboutKeys[DealerCatalogue.Clamp(kind, 0, EventAboutKeys.Length - 1)]);

        public static string Extra(int extra) => T(AddonKeys[DealerCatalogue.Clamp(extra, 0, AddonKeys.Length - 1)]);

        /// <summary>The extras of a bit mask, one after another; empty for none.</summary>
        public static string Extras(int extras)
        {
            if (extras == 0)
            {
                return string.Empty;
            }
            var t = new System.Text.StringBuilder(64);
            for (int e = 0; e < DealerCatalogue.ExtraKinds; e++)
            {
                if ((extras & (1 << e)) != 0)
                {
                    t.Append(t.Length > 0 ? ", " : string.Empty).Append(Extra(e));
                }
            }
            return t.ToString();
        }
    }
}
