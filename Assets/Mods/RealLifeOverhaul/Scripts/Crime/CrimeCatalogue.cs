using System.Collections.Generic;

namespace RealLifeOverhaul
{
    // Game-free, like CrimeDeck.cs: RealLifeOverhaul.Build\CatalogueCheck compiles this file on
    // its own. Every locale key is written out in full here so the check can find it.

    /// <summary>
    /// What the police charge, and how likely it is that anybody saw. Kept beside the crimes so
    /// that everything a player might want to tune is in one file.
    /// </summary>
    internal static class CrimeLaw
    {
        // Traffic offences by the player. A fine is only ever charged by an officer who has stopped them.
        public static int RedLightFine => Prices.At(250);
        public static int SpeedingFine => Prices.At(180);
        public static int RecklessFine => Prices.At(320);           // driving on the pavement or against the traffic
        public static int JaywalkFine => Prices.At(25);
        public static int HornFine => Prices.At(40);                // sounding the horn again and again at people who have the right of way, with an officer in earshot (People/HornReactions)
        public static int DrunkDrivingFine => Prices.At(900);       // at the wheel after two drinks or more (Leisure), found at any stop
        public static int FleeingFine => Prices.At(900);            // on top of the offence, for making them chase
        public static int FleeingPerQuarterMinute => Prices.At(120); // the longer the chase, the more it costs
        public static int FleeingMost => Prices.At(2000);
        public static int WastedCallFine => Prices.At(150);         // a third 911 call about nothing in one day

        // The chance that a patrol car happens to be near enough to see an offence nobody on foot saw.
        public const float RedLightSeen = 0.22f;
        public const float SpeedingSeen = 0.06f;       // (was 0.10 when few cars could speed at all; see below)
        public const float RecklessSeen = 0.16f;
        public const float JaywalkFineChance = 0.2f;   // of the times an officer says anything at all

        // Speeding. Since Driving/CarTuning every car passes 130 km/h and brisk driving is over the limit on every
        // straight, so it is policed as "well over, for a while, now and then": 95 in a 50 held for four seconds
        // is looked at once and then not again for a minute and a half. A patrol car happens to see it 6 times in
        // 100 (3.6 to 9.6 by the crime frequency option; 4.3 as that option comes), twice that from 150 km/h; an
        // officer on foot with a clear view at most 19 in 100. Driven at without pause that is a stop about every
        // 35 minutes, every 8 under an officer's nose, every 4 in a car that got away earlier in the day.
        public const float SpeedLimitKmh = 50f;        // where the road itself does not say
        public const float SpeedingMarginKmh = 45f;    // "well over": this much above the limit
        public const float SpeedingSeconds = 4f;       // ...for this long
        public const float SpeedingCooldown = 90f;     // seconds after speeding was looked at before it is looked at again
        public const float SpeedingOnFoot = 0.35f;     // of what an officer on foot notices of other offences (0.55 on watch, 0.30 at work)
        public const float SpeedingGrossKmh = 150f;    // from this speed, whatever the limit...
        public const float SpeedingGrossTimes = 2f;    // ...a patrol car is this much more likely to notice
        public const float SpeedingSeenKnown = 0.35f;  // the chance for a car that got away earlier today (any other offence: certain)

        public const int CustodyHoursMin = 2;
        public const int CustodyHoursMax = 5;
    }

    /// <summary>
    /// Every crime the mod can stage. Adding one is: an entry here (with its lines in
    /// Locales\en.json), and a scene for it in CrimeScenes (a new class, or one more instance of
    /// an existing one). See docs/contributing/adding-things.md.
    /// </summary>
    internal static class CrimeCatalogue
    {
        public static readonly CrimeEntry[] All = Build();

        private static readonly Dictionary<string, CrimeEntry> ById = Index(All);

        public static CrimeEntry Find(string id)
        {
            return id != null && ById.TryGetValue(id, out CrimeEntry entry) ? entry : null;
        }

        private static Dictionary<string, CrimeEntry> Index(CrimeEntry[] all)
        {
            var index = new Dictionary<string, CrimeEntry>(all.Length);
            foreach (CrimeEntry entry in all)
            {
                index[entry.Id] = entry;
            }
            return index;
        }

        private static CrimeEnding End(string name, float odds, params string[] texts) => new CrimeEnding(name, odds, false, texts);
        private static CrimeEnding Arrest(string name, float odds, params string[] texts) => new CrimeEnding(name, odds, true, texts);

        private static CrimeEntry[] Build()
        {
            return new[]
            {
                // ------------------------------------------------------------ on the pavement
                new CrimeEntry("foot_chase", CrimeFamily.Street)
                {
                    Starts = new[] { "reallife_crime_chase_start_1", "reallife_crime_chase_start_2", "reallife_crime_chase_start_3" },
                    Near = new[] { "reallife_crime_chase_near" },
                    Endings = new[]
                    {
                        Arrest("caught", 0.45f, "reallife_crime_chase_caught_1", "reallife_crime_chase_caught_2", "reallife_crime_chase_caught_3"),
                        End("escaped", 0.40f, "reallife_crime_chase_escaped_1", "reallife_crime_chase_escaped_2", "reallife_crime_chase_escaped_3"),
                        Arrest("tripped", 0.15f, "reallife_crime_chase_tripped_1", "reallife_crime_chase_tripped_2")
                    }
                },
                new CrimeEntry("questioning", CrimeFamily.Street)
                {
                    Weight = 0.8f,
                    Report = ReportKind.None,
                    Starts = new[] { "reallife_crime_question_start_1", "reallife_crime_question_start_2", "reallife_crime_question_start_3" },
                    Near = new[] { "reallife_crime_question_near" },
                    Endings = new[]
                    {
                        End("released", 0.5f, "reallife_crime_question_released_1", "reallife_crime_question_released_2", "reallife_crime_question_released_3"),
                        Arrest("led_off", 0.3f, "reallife_crime_question_led_1", "reallife_crime_question_led_2"),
                        End("bolted", 0.2f, "reallife_crime_question_bolted_1", "reallife_crime_question_bolted_2")
                    }
                },
                new CrimeEntry("disturbance", CrimeFamily.Street)
                {
                    Weight = 0.8f,
                    Starts = new[] { "reallife_crime_disturbance_start_1", "reallife_crime_disturbance_start_2", "reallife_crime_disturbance_start_3" },
                    Near = new[] { "reallife_crime_disturbance_near" },
                    Endings = new[]
                    {
                        End("calmed", 0.55f, "reallife_crime_disturbance_end_1", "reallife_crime_disturbance_end_2", "reallife_crime_disturbance_end_3"),
                        End("fizzled", 0.25f, "reallife_crime_disturbance_fizzled_1", "reallife_crime_disturbance_fizzled_2"),
                        Arrest("arrested", 0.20f, "reallife_crime_disturbance_arrest_1", "reallife_crime_disturbance_arrest_2")
                    }
                },
                new CrimeEntry("snatch", CrimeFamily.Street)
                {
                    Cast = 3,
                    Weight = 1.2f,
                    Report = ReportKind.Theft,
                    Marker = "reallife_crime_marker_theft",
                    Starts = new[] { "reallife_crime_snatch_start_1", "reallife_crime_snatch_start_2", "reallife_crime_snatch_start_3" },
                    Extra = new[] { "reallife_crime_snatch_x_1", "reallife_crime_snatch_x_2", "reallife_crime_snatch_x_3" },
                    Endings = new[]
                    {
                        End("escaped", 0.30f, "reallife_crime_snatch_escaped_1", "reallife_crime_snatch_escaped_2", "reallife_crime_snatch_escaped_3"),
                        End("victim_chase", 0.20f, "reallife_crime_snatch_victim_1", "reallife_crime_snatch_victim_2"),
                        Arrest("bystander", 0.15f, "reallife_crime_snatch_bystander_1", "reallife_crime_snatch_bystander_2"),
                        Arrest("officer", 0.20f, "reallife_crime_snatch_officer_1", "reallife_crime_snatch_officer_2"),
                        End("false_alarm", 0.15f, "reallife_crime_snatch_false_1", "reallife_crime_snatch_false_2")
                    }
                },
                new CrimeEntry("mugging", CrimeFamily.Street)
                {
                    Cast = 3,
                    Weight = 0.8f,
                    Report = ReportKind.Robbery,
                    Marker = "reallife_crime_marker_robbery",
                    Starts = new[] { "reallife_crime_mugging_start_1", "reallife_crime_mugging_start_2", "reallife_crime_mugging_start_3" },
                    Near = new[] { "reallife_crime_mugging_near_1", "reallife_crime_mugging_near_2" },
                    Endings = new[]
                    {
                        End("robbed", 0.40f, "reallife_crime_mugging_robbed_1", "reallife_crime_mugging_robbed_2", "reallife_crime_mugging_robbed_3"),
                        End("fought_back", 0.25f, "reallife_crime_mugging_fought_1", "reallife_crime_mugging_fought_2"),
                        Arrest("interrupted", 0.20f, "reallife_crime_mugging_interrupted_1", "reallife_crime_mugging_interrupted_2"),
                        End("lost", 0.15f, "reallife_crime_mugging_lost_1", "reallife_crime_mugging_lost_2")
                    }
                },
                new CrimeEntry("assault", CrimeFamily.Street)
                {
                    Cast = 3,
                    Needs = CrimeNeed.Ragdolls,   // it is about somebody being knocked down
                    Starts = new[] { "reallife_crime_assault_start_1", "reallife_crime_assault_start_2", "reallife_crime_assault_start_3" },
                    Extra = new[] { "reallife_crime_assault_x_1", "reallife_crime_assault_x_2" },
                    Endings = new[]
                    {
                        Arrest("arrested", 0.30f, "reallife_crime_assault_arrested_1", "reallife_crime_assault_arrested_2"),
                        End("fled", 0.25f, "reallife_crime_assault_fled_1", "reallife_crime_assault_fled_2"),
                        End("pulled_apart", 0.20f, "reallife_crime_assault_apart_1", "reallife_crime_assault_apart_2"),
                        End("walked_off", 0.25f, "reallife_crime_assault_walked_1", "reallife_crime_assault_walked_2")
                    }
                },
                new CrimeEntry("graffiti", CrimeFamily.Street)
                {
                    Starts = new[] { "reallife_crime_graffiti_start_1", "reallife_crime_graffiti_start_2", "reallife_crime_graffiti_start_3" },
                    Near = new[] { "reallife_crime_graffiti_near" },
                    Endings = new[]
                    {
                        End("ran", 0.30f, "reallife_crime_graffiti_ran_1", "reallife_crime_graffiti_ran_2"),
                        Arrest("caught", 0.20f, "reallife_crime_graffiti_caught_1", "reallife_crime_graffiti_caught_2"),
                        End("told_off", 0.20f, "reallife_crime_graffiti_told_1", "reallife_crime_graffiti_told_2"),
                        End("cleared", 0.15f, "reallife_crime_graffiti_cleared_1", "reallife_crime_graffiti_cleared_2"),
                        End("alone", 0.15f, "reallife_crime_graffiti_alone_1", "reallife_crime_graffiti_alone_2")
                    }
                },
                new CrimeEntry("car_breakin", CrimeFamily.Street)
                {
                    Report = ReportKind.Theft,
                    Marker = "reallife_crime_marker_theft",
                    Starts = new[] { "reallife_crime_breakin_start_1", "reallife_crime_breakin_start_2", "reallife_crime_breakin_start_3" },
                    Near = new[] { "reallife_crime_breakin_near" },
                    Endings = new[]
                    {
                        End("ran", 0.30f, "reallife_crime_breakin_ran_1", "reallife_crime_breakin_ran_2"),
                        Arrest("caught", 0.25f, "reallife_crime_breakin_caught_1", "reallife_crime_breakin_caught_2"),
                        End("told_off", 0.20f, "reallife_crime_breakin_owner_1", "reallife_crime_breakin_owner_2"),
                        End("cleared", 0.10f, "reallife_crime_breakin_cleared_1", "reallife_crime_breakin_cleared_2"),
                        End("alone", 0.15f, "reallife_crime_breakin_alone_1", "reallife_crime_breakin_alone_2")
                    }
                },
                new CrimeEntry("drunk", CrimeFamily.Street)
                {
                    Starts = new[] { "reallife_crime_drunk_start_1", "reallife_crime_drunk_start_2", "reallife_crime_drunk_start_3" },
                    Near = new[] { "reallife_crime_drunk_near" },
                    Endings = new[]
                    {
                        End("moved_on", 0.25f, "reallife_crime_drunk_home_1", "reallife_crime_drunk_home_2"),
                        Arrest("arrested", 0.15f, "reallife_crime_drunk_arrested_1", "reallife_crime_drunk_arrested_2"),
                        End("friend", 0.25f, "reallife_crime_drunk_friend_1", "reallife_crime_drunk_friend_2"),
                        End("collapsed", 0.20f, "reallife_crime_drunk_fell_1", "reallife_crime_drunk_fell_2"),
                        End("alone", 0.15f, "reallife_crime_drunk_alone_1", "reallife_crime_drunk_alone_2")
                    }
                },
                new CrimeEntry("street_vendor", CrimeFamily.Street)
                {
                    Weight = 0.7f,
                    Report = ReportKind.None,
                    Starts = new[] { "reallife_crime_vendor_start_1", "reallife_crime_vendor_start_2" },
                    Near = new[] { "reallife_crime_vendor_near" },
                    Endings = new[]
                    {
                        End("moved_on", 0.40f, "reallife_crime_vendor_moved_1", "reallife_crime_vendor_moved_2"),
                        End("ticketed", 0.30f, "reallife_crime_vendor_ticket_1", "reallife_crime_vendor_ticket_2"),
                        End("ran", 0.15f, "reallife_crime_vendor_ran_1", "reallife_crime_vendor_ran_2"),
                        End("alone", 0.15f, "reallife_crime_vendor_alone_1")
                    }
                },
                new CrimeEntry("panhandler", CrimeFamily.Street)
                {
                    Weight = 0.7f,
                    Starts = new[] { "reallife_crime_panhandler_start_1", "reallife_crime_panhandler_start_2" },
                    Near = new[] { "reallife_crime_panhandler_near" },
                    Endings = new[]
                    {
                        End("moved_on", 0.40f, "reallife_crime_panhandler_moved_1", "reallife_crime_panhandler_moved_2"),
                        Arrest("arrested", 0.12f, "reallife_crime_panhandler_arrested_1"),
                        End("told_off", 0.20f, "reallife_crime_panhandler_told_1", "reallife_crime_panhandler_told_2"),
                        End("alone", 0.28f, "reallife_crime_panhandler_alone_1", "reallife_crime_panhandler_alone_2")
                    }
                },
                new CrimeEntry("fare_dodger", CrimeFamily.Street)
                {
                    Weight = 0.6f,
                    Report = ReportKind.None,
                    Starts = new[] { "reallife_crime_fare_start_1", "reallife_crime_fare_start_2" },
                    Near = new[] { "reallife_crime_fare_near" },
                    Endings = new[]
                    {
                        End("ticketed", 0.50f, "reallife_crime_fare_ticket_1", "reallife_crime_fare_ticket_2"),
                        End("cleared", 0.25f, "reallife_crime_fare_cleared_1", "reallife_crime_fare_cleared_2"),
                        Arrest("arrested", 0.25f, "reallife_crime_fare_arrested_1", "reallife_crime_fare_arrested_2")
                    }
                },
                new CrimeEntry("drug_deal", CrimeFamily.Street)
                {
                    Cast = 3,
                    Starts = new[] { "reallife_crime_deal_start_1", "reallife_crime_deal_start_2", "reallife_crime_deal_start_3" },
                    Near = new[] { "reallife_crime_deal_near" },
                    Endings = new[]
                    {
                        End("scattered", 0.30f, "reallife_crime_deal_scattered_1", "reallife_crime_deal_scattered_2"),
                        Arrest("one_caught", 0.25f, "reallife_crime_deal_caught_1", "reallife_crime_deal_caught_2"),
                        End("nothing", 0.20f, "reallife_crime_deal_nothing_1", "reallife_crime_deal_nothing_2"),
                        End("done", 0.25f, "reallife_crime_deal_done_1", "reallife_crime_deal_done_2")
                    }
                },
                new CrimeEntry("car_theft", CrimeFamily.Street)
                {
                    Cast = 3,
                    Weight = 0.7f,
                    Needs = CrimeNeed.PatrolCars | CrimeNeed.Traffic,
                    Report = ReportKind.Robbery,
                    Marker = "reallife_crime_marker_cartheft",
                    Starts = new[] { "reallife_crime_cartheft_start_1", "reallife_crime_cartheft_start_2" },
                    Extra = new[] { "reallife_crime_cartheft_x_1", "reallife_crime_cartheft_x_2" },
                    Endings = new[]
                    {
                        Arrest("caught", 0.5f, "reallife_crime_cartheft_caught_1", "reallife_crime_cartheft_caught_2"),
                        End("escaped", 0.5f, "reallife_crime_cartheft_escaped_1", "reallife_crime_cartheft_escaped_2")
                    }
                },

                // ------------------------------------------------------------ on the road
                new CrimeEntry("traffic_stop", CrimeFamily.Traffic)
                {
                    Cast = 1,
                    Needs = CrimeNeed.PatrolCars | CrimeNeed.Traffic,
                    Report = ReportKind.None,
                    Marker = "reallife_crime_marker_traffic",
                    Starts = new[] { "reallife_crime_stop_start_1", "reallife_crime_stop_start_2", "reallife_crime_stop_start_3", "reallife_crime_stop_start_4" },
                    Endings = new[]
                    {
                        End("ticket", 0.55f, "reallife_crime_stop_ticket_1", "reallife_crime_stop_ticket_2", "reallife_crime_stop_ticket_3"),
                        End("warning", 0.25f, "reallife_crime_stop_warning_1", "reallife_crime_stop_warning_2"),
                        Arrest("arrested", 0.20f, "reallife_crime_stop_arrested_1", "reallife_crime_stop_arrested_2")
                    }
                },
                new CrimeEntry("reckless_driver", CrimeFamily.Traffic)
                {
                    Cast = 2,
                    Weight = 0.8f,
                    Needs = CrimeNeed.PatrolCars | CrimeNeed.Traffic,
                    Report = ReportKind.Traffic,
                    Marker = "reallife_crime_marker_traffic",
                    Starts = new[] { "reallife_crime_reckless_start_1", "reallife_crime_reckless_start_2", "reallife_crime_reckless_start_3" },
                    Endings = new[]
                    {
                        Arrest("caught", 0.5f, "reallife_crime_reckless_caught_1", "reallife_crime_reckless_caught_2"),
                        End("escaped", 0.5f, "reallife_crime_reckless_escaped_1", "reallife_crime_reckless_escaped_2")
                    }
                },
                new CrimeEntry("parking_ticket", CrimeFamily.Traffic)
                {
                    Weight = 0.7f,
                    Report = ReportKind.None,
                    Marker = "reallife_crime_marker_traffic",
                    Starts = new[] { "reallife_crime_parking_start_1", "reallife_crime_parking_start_2" },
                    Near = new[] { "reallife_crime_parking_near" },
                    Endings = new[]
                    {
                        End("ticketed", 0.45f, "reallife_crime_parking_ticketed_1", "reallife_crime_parking_ticketed_2"),
                        End("argued", 0.30f, "reallife_crime_parking_argued_1", "reallife_crime_parking_argued_2"),
                        End("let_off", 0.25f, "reallife_crime_parking_letoff_1", "reallife_crime_parking_letoff_2")
                    }
                },

                // ------------------------------------------------------------ in shops
                new CrimeEntry("shop_theft_own", CrimeFamily.Shop)
                {
                    Place = CrimePlace.OwnShop,
                    Cast = 1,
                    Weight = 0f,   // started when the game books a theft, never dealt
                    Report = ReportKind.Theft,
                    Marker = "reallife_crime_marker_shoplifter",
                    Extra = new[] { "reallife_crime_shoptheft_bolted_1", "reallife_crime_shoptheft_bolted_2" },
                    Endings = new[]
                    {
                        End("escaped", 0.45f, "reallife_crime_shoptheft_escaped_1", "reallife_crime_shoptheft_escaped_2"),
                        End("guard", 0.55f, "reallife_crime_shoptheft_guard_1", "reallife_crime_shoptheft_guard_2"),
                        End("dropped", 0f, "reallife_crime_shoptheft_dropped_1", "reallife_crime_shoptheft_dropped_2"),
                        End("paid", 0f, "reallife_crime_shoptheft_paid_1", "reallife_crime_shoptheft_paid_2"),
                        End("arrested", 0f, "reallife_crime_shoptheft_arrested_1")
                    }
                },
                new CrimeEntry("shoplifter", CrimeFamily.Shop)
                {
                    Place = CrimePlace.OtherShop,
                    Report = ReportKind.Theft,
                    Marker = "reallife_crime_marker_shoplifter",
                    Starts = new[] { "reallife_crime_shoplifter_start_1", "reallife_crime_shoplifter_start_2", "reallife_crime_shoplifter_start_3" },
                    Extra = new[] { "reallife_crime_shoplifter_x_1", "reallife_crime_shoplifter_x_2", "reallife_crime_shoplifter_x_3" },
                    Endings = new[]
                    {
                        End("escaped", 0.40f, "reallife_crime_shoplifter_escaped_1", "reallife_crime_shoplifter_escaped_2"),
                        Arrest("caught_at_door", 0.30f, "reallife_crime_shoplifter_caught_1", "reallife_crime_shoplifter_caught_2"),
                        End("false_alarm", 0.30f, "reallife_crime_shoplifter_false_1", "reallife_crime_shoplifter_false_2"),
                        End("tripped", 0f, "reallife_crime_shoplifter_tripped_1", "reallife_crime_shoplifter_tripped_2")
                    }
                },
                new CrimeEntry("holdup", CrimeFamily.Shop)
                {
                    Place = CrimePlace.OwnShop | CrimePlace.OtherShop,
                    Weight = 0.3f,
                    Report = ReportKind.Robbery,
                    Marker = "reallife_crime_marker_robbery",
                    Starts = new[] { "reallife_crime_holdup_start_1", "reallife_crime_holdup_start_2" },
                    Extra = new[] { "reallife_crime_holdup_x_1", "reallife_crime_holdup_x_2" },
                    Endings = new[]
                    {
                        End("fled", 0.40f, "reallife_crime_holdup_fled_1", "reallife_crime_holdup_fled_2"),
                        Arrest("caught_outside", 0.35f, "reallife_crime_holdup_caught_1", "reallife_crime_holdup_caught_2"),
                        End("bungled", 0.25f, "reallife_crime_holdup_bungled_1", "reallife_crime_holdup_bungled_2"),
                        End("till", 0f, "reallife_crime_holdup_till_1")
                    }
                },

                // ------------------------------------------------------------ against the player
                new CrimeEntry("player_pickpocket", CrimeFamily.Player)
                {
                    Cast = 1,
                    Weight = 0.5f,
                    Needs = CrimeNeed.OnFoot | CrimeNeed.AgainstPlayer | CrimeNeed.Cash,
                    Report = ReportKind.None,
                    Marker = "reallife_crime_marker_thief",
                    Endings = new[]
                    {
                        End("noticed", 0.6f, "reallife_crime_pickpocket_noticed_1", "reallife_crime_pickpocket_noticed_2"),
                        End("unnoticed", 0.4f, "reallife_crime_pickpocket_unnoticed_1", "reallife_crime_pickpocket_unnoticed_2"),
                        End("recovered", 0f, "reallife_crime_pickpocket_recovered_1", "reallife_crime_pickpocket_recovered_2"),
                        End("lost", 0f, "reallife_crime_pickpocket_lost_1", "reallife_crime_pickpocket_lost_2")
                    }
                },
                new CrimeEntry("player_mugging", CrimeFamily.Player)
                {
                    Cast = 1,
                    Weight = 0.5f,
                    Needs = CrimeNeed.Night | CrimeNeed.OnFoot | CrimeNeed.AgainstPlayer | CrimeNeed.Cash,
                    Report = ReportKind.None,
                    Endings = new[]
                    {
                        End("complied", 1f, "reallife_crime_mugged_complied_1", "reallife_crime_mugged_complied_2"),
                        End("scared_off", 0f, "reallife_crime_mugged_scared_1", "reallife_crime_mugged_scared_2"),
                        End("hurt", 0f, "reallife_crime_mugged_hurt_1", "reallife_crime_mugged_hurt_2"),
                        End("avoided", 0f, "reallife_crime_mugged_avoided_1", "reallife_crime_mugged_avoided_2")
                    }
                }
            };
        }
    }
}
