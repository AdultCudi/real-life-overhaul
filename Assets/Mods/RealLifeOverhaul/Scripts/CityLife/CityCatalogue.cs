using BigAmbitions.Characters;

namespace RealLifeOverhaul
{
    /// <summary>The three groups the options page can switch on and off, and the test buttons step through.</summary>
    public enum CityFamily { Workers, Everyday, Stalls, Deliveries, Leisure }

    /// <summary>One thing that can happen on the street: who, when, how often, and the activity that plays it.</summary>
    public sealed class CityEntry
    {
        public string Id;
        public string Name;              // locale key: what a test button calls it
        public CityFamily Family;
        public int Cast = 1;             // people it must be able to take from the pool to start
        public int Weight = 10;          // in the deal, among what is possible at this hour
        public int From = 7, To = 20;    // hours of the day it can start in: From <= hour < To (To may be past midnight: 26 is 2 am)
        public float MinMinutes = 4f, MaxMinutes = 9f;   // real minutes it stays, if nobody is watching when the time is up
        public bool Rain;                // it can be out in the rain (people under umbrellas); everything else is taken in
        public bool RainOnly;            // and only then
        public ICityActivity Activity;   // the one instance: an entry is out at most once at a time

        public bool OpenAt(int hour)
        {
            return (hour >= From && hour < To) || (To > 24 && hour < To - 24);
        }
    }

    /// <summary>
    /// THE CATALOGUE of the living city: every activity, one entry each. This is the one place
    /// to look for what exists and the one place to add to: see "Adding an activity" in
    /// docs/contributing/adding-things.md. Somebody working alone is a <see cref="SoloSpec"/> and needs no class of its own.
    ///
    /// Hours follow the day: sweepers and window cleaners early, the road crew in working
    /// hours, leaflets and the coffee cart while shops are busy, joggers at both ends of the
    /// day, smokers and people on the phone into the night. Between two and five in the morning
    /// nothing starts at all. In the rain everybody who is out is taken off the street as soon as
    /// nobody is looking, and the only thing that starts is people walking under umbrellas (the
    /// entries marked Rain); those go in again when it stops.
    /// </summary>
    public static class CityCatalogue
    {
        public const int Families = 5;

        public static readonly CityEntry[] All =
        {
            // ---- people at work
            new CityEntry
            {
                Id = "sweeper", Name = "reallife_city_name_sweeper", Family = CityFamily.Workers, Weight = 14, From = 6, To = 18,
                Activity = new SoloActivity(new SoloSpec
                {
                    Look = CityLook.Sweeper, Room = 0.7f, Ahead = 1.3f, Work = "sweep", Rest = "broom_rest", WorkMin = 9f, WorkMax = 16f, RestMin = 4f, RestMax = 8f
                })
            },
            new CityEntry
            {
                Id = "window_cleaner", Name = "reallife_city_name_window_cleaner", Family = CityFamily.Workers, Weight = 12, From = 6, To = 12,
                Activity = new SoloActivity(new SoloSpec
                {
                    Look = CityLook.Cleaner, Place = SoloPlace.Shopfront, Wall = 0.50f, Room = 0.45f, Work = "squeegee_wipe", Rest = "squeegee_rest",
                    WorkMin = 8f, WorkMax = 16f, RestMin = 4f, RestMax = 7f, Props = new[] { new SoloProp("bucket", 0.62f, 0.05f, 0f) }
                })
            },
            new CityEntry
            {
                Id = "repairs", Name = "reallife_city_name_repairs", Family = CityFamily.Workers, Weight = 10, From = 8, To = 17,
                Activity = new SoloActivity(new SoloSpec
                {
                    Look = CityLook.RoadWorker, Place = SoloPlace.Shopfront, Wall = 0.66f, Room = 0.5f, Work = "kneel_work",
                    Props = new[] { new SoloProp("toolbox", -0.62f, 0.1f, 75f) }
                })
            },
            new CityEntry
            {
                Id = "road_crew", Name = "reallife_city_name_road_crew", Family = CityFamily.Workers, Cast = 3, Weight = 12, From = 8, To = 17, MinMinutes = 6f, MaxMinutes = 12f,
                Activity = new RoadCrewActivity()
            },
            new CityEntry
            {
                Id = "leaflets", Name = "reallife_city_name_leaflets", Family = CityFamily.Workers, Weight = 10, From = 10, To = 19,
                Activity = new SoloActivity(new SoloSpec
                {
                    Look = CityLook.Canvasser, Room = 0.7f, Ahead = 1.0f, Work = "leaflet_idle", Carry = "leaflet_offer", Greet = "leaflet_offer"
                })
            },
            new CityEntry
            {
                Id = "fundraiser", Name = "reallife_city_name_fundraiser", Family = CityFamily.Workers, Weight = 8, From = 10, To = 18,
                Activity = new SoloActivity(new SoloSpec { Look = CityLook.Canvasser, Room = 0.7f, Work = "clipboard_write" })
            },

            // ---- everyday people
            new CityEntry
            {
                Id = "chat", Name = "reallife_city_name_chat", Family = CityFamily.Everyday, Cast = 2, Weight = 16, From = 8, To = 23,
                Activity = new ChatActivity()
            },
            new CityEntry
            {
                Id = "smoker", Name = "reallife_city_name_smoker", Family = CityFamily.Everyday, Weight = 12, From = 8, To = 26,
                Activity = new SoloActivity(new SoloSpec { Place = SoloPlace.BackToWall, Wall = 0.55f, Room = 0.4f, Work = "smoke" })
            },
            new CityEntry
            {
                Id = "phone_call", Name = "reallife_city_name_phone_call", Family = CityFamily.Everyday, Weight = 10, From = 8, To = 25,
                Activity = new SoloActivity(new SoloSpec
                {
                    Place = SoloPlace.BackToWall, Wall = 0.6f, Room = 0.4f, UsesGamePose = true, GamePose = PermanentAnimationType.TalkingPhone, GameProp = "PhoneOff",
                    Ring = "phone_answer", WorkMin = 14f, WorkMax = 26f, RestMin = 5f, RestMax = 9f   // a call ends, the phone is pocketed, it rings again and is answered
                })
            },
            new CityEntry
            {
                Id = "coffee_break", Name = "reallife_city_name_coffee_break", Family = CityFamily.Everyday, Weight = 10, From = 7, To = 17,
                Activity = new SoloActivity(new SoloSpec { Place = SoloPlace.BackToWall, Wall = 0.6f, Room = 0.4f, Work = "cup_hold", Now = "sip", NowMin = 8f, NowMax = 15f })
            },
            new CityEntry
            {
                // Lunch out of a carton by a wall: the carton on the left palm, a piece to the mouth every few seconds (eat_standing, with takeaway_box).
                Id = "street_lunch", Name = "reallife_city_name_street_lunch", Family = CityFamily.Everyday, Weight = 9, From = 11, To = 15,
                Activity = new SoloActivity(new SoloSpec { Place = SoloPlace.BackToWall, Wall = 0.6f, Room = 0.4f, Now = "eat_standing", HoldNow = true, NowMin = 4f, NowMax = 8f })
            },
            new CityEntry
            {
                // Somebody from a shop or an office on the doorstep with a mug: held at the hip, a drink now and then (drink_stand, with mug).
                Id = "mug_break", Name = "reallife_city_name_mug_break", Family = CityFamily.Everyday, Weight = 8, From = 7, To = 17,
                Activity = new SoloActivity(new SoloSpec { Place = SoloPlace.BackToWall, Wall = 0.6f, Room = 0.4f, Now = "drink_stand", HoldNow = true, NowMin = 7f, NowMax = 14f })
            },
            new CityEntry
            {
                Id = "tourist", Name = "reallife_city_name_tourist", Family = CityFamily.Everyday, Weight = 8, From = 10, To = 20,
                Activity = new SoloActivity(new SoloSpec { Room = 0.7f, Ahead = 0.8f, Carry = "take_photo", Now = "take_photo", NowMin = 9f, NowMax = 16f })
            },
            new CityEntry
            {
                Id = "jogger", Name = "reallife_city_name_jogger", Family = CityFamily.Everyday, Weight = 8, From = 6, To = 21, MinMinutes = 3f, MaxMinutes = 6f,
                Activity = new JoggerActivity()
            },
            new CityEntry
            {
                Id = "busker_audience", Name = "reallife_city_name_busker_audience", Family = CityFamily.Everyday, Cast = 2, Weight = 14, From = 9, To = 23,
                Activity = new BuskerAudienceActivity()
            },

            // ---- stalls
            new CityEntry
            {
                Id = "coffee_cart", Name = "reallife_city_name_coffee_cart", Family = CityFamily.Stalls, Cast = 2, Weight = 12, From = 7, To = 18, MinMinutes = 8f, MaxMinutes = 15f,
                Activity = new StallActivity(StallKind.Coffee)
            },
            new CityEntry
            {
                Id = "hot_dog_cart", Name = "reallife_city_name_hot_dog_cart", Family = CityFamily.Stalls, Cast = 2, Weight = 10, From = 11, To = 22, MinMinutes = 8f, MaxMinutes = 15f,
                Activity = new StallActivity(StallKind.HotDogs)
            },

            // ---- deliveries and trades: people who come, do something and go
            new CityEntry
            {
                Id = "courier", Name = "reallife_city_name_courier", Family = CityFamily.Deliveries, Weight = 14, From = 8, To = 19, MinMinutes = 3f, MaxMinutes = 6f,
                Activity = new CourierActivity()
            },
            new CityEntry
            {
                Id = "mail", Name = "reallife_city_name_mail", Family = CityFamily.Deliveries, Weight = 12, From = 7, To = 14, MinMinutes = 3f, MaxMinutes = 6f,
                Activity = new MailActivity()
            },
            new CityEntry
            {
                Id = "painter", Name = "reallife_city_name_painter", Family = CityFamily.Deliveries, Weight = 9, From = 8, To = 17,
                Activity = new SoloActivity(new SoloSpec
                {
                    // On the first tread of the ladder, which stands between the painter and the wall (paint_roll: 0.80 m to the wall, the ladder 0.24 m ahead).
                    Look = CityLook.Painter, Place = SoloPlace.Shopfront, Wall = 0.80f, Room = 0.5f, Work = "paint_roll",
                    Props = new[] { new SoloProp("step_ladder", 0f, 0.24f, 0f), new SoloProp("bucket", 0.62f, 0.15f, 0f) }
                })
            },
            new CityEntry
            {
                Id = "gardener", Name = "reallife_city_name_gardener", Family = CityFamily.Deliveries, Weight = 9, From = 7, To = 16,
                Activity = new TreeCareActivity()
            },

            // ---- leisure: the park, the door, the weather
            new CityEntry
            {
                Id = "chess", Name = "reallife_city_name_chess", Family = CityFamily.Leisure, Cast = 2, Weight = 9, From = 10, To = 20, MinMinutes = 8f, MaxMinutes = 15f,
                Activity = new ChessActivity()
            },
            new CityEntry
            {
                Id = "bench_chat", Name = "reallife_city_name_bench_chat", Family = CityFamily.Leisure, Cast = 2, Weight = 9, From = 9, To = 21, MinMinutes = 6f, MaxMinutes = 12f,
                Activity = new BenchChatActivity()
            },
            new CityEntry
            {
                Id = "tai_chi", Name = "reallife_city_name_tai_chi", Family = CityFamily.Leisure, Weight = 9, From = 6, To = 10,
                Activity = new SoloActivity(new SoloSpec { Room = 1.0f, Ahead = 0.9f, Work = "tai_chi" })
            },
            new CityEntry
            {
                Id = "newspaper", Name = "reallife_city_name_newspaper", Family = CityFamily.Leisure, Weight = 10, From = 7, To = 18,
                Activity = new SoloActivity(new SoloSpec { Place = SoloPlace.BackToWall, Wall = 0.6f, Room = 0.4f, Work = "read_paper" })
            },
            new CityEntry
            {
                Id = "map_tourist", Name = "reallife_city_name_map_tourist", Family = CityFamily.Leisure, Weight = 9, From = 9, To = 20,
                Activity = new SoloActivity(new SoloSpec { Room = 0.7f, Work = "map_read" })
            },
            new CityEntry
            {
                // A traveller with a cabin case, walking the pavements on the clip's own full-body walk (carry_suitcase_walk, suitcase), kept in step with the ground.
                Id = "traveller", Name = "reallife_city_name_traveller", Family = CityFamily.Leisure, Weight = 8, From = 6, To = 23, MinMinutes = 3f, MaxMinutes = 6f,
                Activity = new StrollActivity(StrollKind.Suitcase)
            },
            new CityEntry
            {
                Id = "pushchair", Name = "reallife_city_name_pushchair", Family = CityFamily.Leisure, Weight = 9, From = 8, To = 19, MinMinutes = 3f, MaxMinutes = 6f,
                Activity = new StrollActivity(StrollKind.Pushchair)
            },
            new CityEntry
            {
                Id = "car_lean", Name = "reallife_city_name_car_lean", Family = CityFamily.Leisure, Weight = 8, From = 8, To = 25,
                Activity = new CarLeanActivity()
            },
            new CityEntry
            {
                Id = "doorman", Name = "reallife_city_name_doorman", Family = CityFamily.Leisure, Weight = 8, From = 9, To = 26,
                // (Somebody on foot in front of him is shown the way in: an open hand swept round to the door.)
                Activity = new SoloActivity(new SoloSpec { Look = CityLook.Doorman, Place = SoloPlace.BackToWall, Wall = 0.55f, Room = 0.4f, Work = "guard_stand", Greet = "point_show" })
            },
            new CityEntry
            {
                Id = "door_queue", Name = "reallife_city_name_door_queue", Family = CityFamily.Leisure, Cast = 3, Weight = 14, From = 21, To = 26, MinMinutes = 6f, MaxMinutes = 12f,
                Activity = new DoorQueueActivity()
            },
            new CityEntry
            {
                Id = "umbrella", Name = "reallife_city_name_umbrella", Family = CityFamily.Leisure, Weight = 20, From = 6, To = 26, MinMinutes = 3f, MaxMinutes = 6f, Rain = true, RainOnly = true,
                Activity = new StrollActivity(StrollKind.Umbrella)
            },
            new CityEntry
            {
                Id = "umbrella_two", Name = "reallife_city_name_umbrella", Family = CityFamily.Leisure, Weight = 20, From = 6, To = 26, MinMinutes = 3f, MaxMinutes = 6f, Rain = true, RainOnly = true,
                Activity = new StrollActivity(StrollKind.Umbrella)
            }
        };

        /// <summary>True if the options page has this family switched on.</summary>
        public static bool Enabled(CityFamily family)
        {
            return family == CityFamily.Everyday || family == CityFamily.Leisure ? Settings.CityEveryday : Settings.CityWorkers;   // the stalls and the deliveries are people at work too
        }
    }
}
