using System;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>What a notification of the mod's is about: the picture it carries in the game's toast and list.</summary>
    public enum NoticeKind
    {
        None,       // not the mod's: the game's own, or another mod's; left exactly as the game draws it
        Road,       // accidents, the car, driving licences, buses, taxis, the GPS
        Police,     // crime, the police, courts and prison
        Health,     // illness, the doctor, the hospital
        Money,      // banks, loans, credit, bills, markets, investors
        Property,   // property, letting, rent, the roommate
        Work,       // jobs, careers, study
        Business,   // running the player's own businesses
        Tech,       // the tech sector: firms, hiring, servers, broadband
        News,       // the paper, the city's government, standing, the economy's news
        People,     // family and friends
        Other       // anything else of the mod's
    }

    /// <summary>
    /// Which kind a notification of the mod's is, from its locale key alone, game-free (checked by CatalogueCheck's
    /// NoticeCheck). The game saves a notification as its type, its key, its names and values and its time
    /// (UI.Notification/Notification.cs), and the list draws every saved one again from those
    /// (NotificationListScrollerController.LoadNotifications), so the kind is read from the key each time it is drawn:
    /// nothing new is saved, and a notification saved by any older version of the mod is drawn with its picture too.
    /// A key that is not the mod's (without the mod's prefix in front) is <see cref="NoticeKind.None"/>.
    ///
    /// Cost of <see cref="Of"/>: a walk down the table below (about 120 short ordinal comparisons at most), no allocation;
    /// called once per toast and once per row the list brings into view.
    /// </summary>
    public static class NoticeKinds
    {
        public const string ModPrefix = "reallife_";

        /// <summary>How many kinds there are, <see cref="NoticeKind.None"/> included (for arrays indexed by kind).</summary>
        public const int Count = (int)NoticeKind.Other + 1;

        // The part of the key after the prefix, up to and with its underscore, and the kind it means. Longer before
        // shorter where one begins another ("biz_racket_" before "biz_"): the first match wins.
        private static readonly string[] Prefixes =
        {
            // the options page's test of the pictures: one notification of each kind
            "notice_test_road", "notice_test_police", "notice_test_health", "notice_test_money", "notice_test_property",
            "notice_test_work", "notice_test_business", "notice_test_tech", "notice_test_news", "notice_test_people", "notice_test_other",
            // exceptions inside a wider prefix
            "biz_racket_", "biz_visit_police", "event_ambulance", "family_checkup_", "travel_burnout_doctor", "home_rent",
            // accidents and the road
            "accident_", "crash_", "car_", "roadside_", "garage_", "customshop_", "bike_", "transit_", "taxi_", "bus_", "subway_",
            "transport_", "nav_", "lic_", "licences_", "tow_", "pedhit_", "pedstruck_", "ped_", "horn_", "solid_", "player_",
            "traffic_", "speedometer_", "siren_", "empire_",
            // police and crime
            "crime_", "law_", "justice_", "police_", "pursuit_", "chase_", "streets_", "patrol_", "fine_", "records_", "911_",
            // health and the hospital
            "health_", "doctor_", "pd_", "ragdolls_",
            // money and bills
            "econ_", "bank_", "lender_", "mkt_", "lst_", "ven_", "vc_", "ob_", "util_", "net_", "advice_", "collect_",
            // property and rent
            "estate_", "roommate_",
            // work and careers
            "job_", "jobs_", "jobline_", "career_", "careers_", "work_", "life_", "own_", "study_", "campus_", "school_",
            "degrees_", "gig_", "pro_",
            // the player's businesses
            "co_", "rota_", "biz_", "brand_", "svc_", "online_", "dealer_", "interior_", "business_", "businesstype_",
            // the tech sector
            "tech_", "infra_", "sites_", "bb_", "hire_", "talent_", "roles_", "hq_",
            // news
            "ledger_", "news_", "civic_", "fame_", "macro_", "weather_",
            // family and friends
            "family_", "social_", "friend_", "mom_", "contact_", "contacts_"
        };

        private static readonly NoticeKind[] Kinds = Build();

        private static NoticeKind[] Build()
        {
            var kinds = new NoticeKind[Prefixes.Length];
            for (int i = 0; i < Prefixes.Length; i++)
            {
                kinds[i] = Section(Prefixes[i]);
            }
            return kinds;
        }

        // The kind each prefix stands for: the test keys by their last word, the exceptions one by one, the rest by
        // where they stand in the table.
        private static NoticeKind Section(string prefix)
        {
            switch (prefix)
            {
                case "notice_test_road": return NoticeKind.Road;
                case "notice_test_police": return NoticeKind.Police;
                case "notice_test_health": return NoticeKind.Health;
                case "notice_test_money": return NoticeKind.Money;
                case "notice_test_property": return NoticeKind.Property;
                case "notice_test_work": return NoticeKind.Work;
                case "notice_test_business": return NoticeKind.Business;
                case "notice_test_tech": return NoticeKind.Tech;
                case "notice_test_news": return NoticeKind.News;
                case "notice_test_people": return NoticeKind.People;
                case "notice_test_other": return NoticeKind.Other;
                case "biz_racket_": return NoticeKind.Police;
                case "biz_visit_police": return NoticeKind.Police;
                case "event_ambulance": return NoticeKind.Health;
                case "family_checkup_": return NoticeKind.Health;
                case "travel_burnout_doctor": return NoticeKind.Health;
                case "home_rent": return NoticeKind.Property;
            }
            int at = Array.IndexOf(Prefixes, prefix);
            if (at < Array.IndexOf(Prefixes, "crime_")) return NoticeKind.Road;
            if (at < Array.IndexOf(Prefixes, "health_")) return NoticeKind.Police;
            if (at < Array.IndexOf(Prefixes, "econ_")) return NoticeKind.Health;
            if (at < Array.IndexOf(Prefixes, "estate_")) return NoticeKind.Money;
            if (at < Array.IndexOf(Prefixes, "job_")) return NoticeKind.Property;
            if (at < Array.IndexOf(Prefixes, "co_")) return NoticeKind.Work;
            if (at < Array.IndexOf(Prefixes, "tech_")) return NoticeKind.Business;
            if (at < Array.IndexOf(Prefixes, "ledger_")) return NoticeKind.Tech;
            if (at < Array.IndexOf(Prefixes, "family_")) return NoticeKind.News;
            return NoticeKind.People;
        }

        /// <summary>The kind of a notification by its key; <see cref="NoticeKind.None"/> for a key that is not the mod's.</summary>
        public static NoticeKind Of(string key)
        {
            if (key == null || key.Length <= ModPrefix.Length || string.CompareOrdinal(key, 0, ModPrefix, 0, ModPrefix.Length) != 0)
            {
                return NoticeKind.None;
            }
            int rest = key.Length - ModPrefix.Length;
            for (int i = 0; i < Prefixes.Length; i++)
            {
                string prefix = Prefixes[i];
                if (prefix.Length <= rest && string.CompareOrdinal(key, ModPrefix.Length, prefix, 0, prefix.Length) == 0)
                {
                    return Kinds[i];
                }
            }
            return NoticeKind.Other;
        }

        /// <summary>The prefixes, for the offline check (a copy).</summary>
        public static string[] Table()
        {
            return (string[])Prefixes.Clone();
        }
    }
}
