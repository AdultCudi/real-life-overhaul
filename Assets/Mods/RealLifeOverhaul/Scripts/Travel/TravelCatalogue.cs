using System;

namespace RealLifeOverhaul
{
    // Everything about travel that is a number or a name: hotel tiers and rooms, where the
    // hotels are looked for, the trips, what happens on them, and the prices. Nothing in this
    // file uses Unity or the game, like the life event and crime catalogues.
    //
    // PRICES are set against what the game itself charges and pays (read from its data files):
    //   rent of the smallest apartment (54 m2), a day:  Industry City 31, Lower Manhattan 34-39,
    //     Garment District 40-48, Murray Hill 55-63, Hell's Kitchen 79-95, Midtown 134-138;
    //   96 m2: 54-261; 204 m2: 115-529; 589 m2: 364-1455; a Hamptons house: 4768 a day;
    //   wages: the cashier job pays 25 an hour, staff start at 12 to 18 an hour.
    // A hostel bed (45) costs as much as the cheapest apartment's rent and sleeps worse, so it
    // is a stopgap and never the better deal; a week of it (36 a night) is still dearer than
    // renting in Industry City. A mid-range room is a day's rent of a mid-sized apartment in
    // the same district; a luxury room a day's rent of a large one. A beach week is about
    // twelve days of the cashier job.

    public enum HotelTier { Budget, MidRange, Luxury, Cottage }

    public enum TravelSeason { Spring, Summer, Autumn, Winter }

    /// <summary>One kind of room: what a night costs and how well one sleeps in it.</summary>
    public sealed class RoomType
    {
        public string Id;
        public string NameKey;
        public int Rate;             // dollars a night before the hotel's own factor, the day and the season
        public int Quality;          // 0 dormitory, 1 plain, 2 comfortable, 3 luxurious: what the bridges are told
        public int Bed;              // 0 = as a bench, 1 = as a car seat, 2 = as a bed: which of the game's own energy rates sleep uses
        public float BedBonus;       // added to the game's bed multiplier, as a bed's quality is
        public float RoughNight;     // chance that a night's sleep comes with an event from the rough pool
        public float Refund;         // share of prepaid nights not begun that an early departure gets back
    }

    /// <summary>Where one hotel is looked for. The building is picked from static data only, so it is the same in every session.</summary>
    public sealed class HotelSlot
    {
        public string Id;            // what a booking remembers
        public string NameKey;
        public HotelTier Tier;
        public string Neighbourhood; // the game's id
        public string Sizes;         // building sizes that will do, by their last letter, best first
        public float RateFactor;     // the district's own level
        public bool Doorman;
        public string[] Anchors;     // for a place with no building of its own (the cottages): "street|number" of buildings to stand its board by, best first
    }

    public enum TripKind { Real, Abstract, Business }

    public sealed class Trip
    {
        public string Id;
        public string NameKey;
        public string PitchKey;      // the agent's description: {price}, {first}, {days}
        public TripKind Kind;
        public int Days;             // length; an abstract trip has one card a day
        public int Price;            // economy, before the season
        public float FirstClass;     // multiplier for the better class
        public TravelSeason Peak;    // the dear season
        public TravelSeason Low;     // the cheap one
        public int Splurge;          // what a day's splurge costs
        public bool JetLag;
        public string DepartKey;
        public string ReturnKey;
        public string[] DayKeys;     // one opening line per day, in order; the last repeats if the trip is longer
        public string[] Events;      // this destination's own events (ids into TravelCatalogue.Events)
    }

    public enum TripChoice { Relax, Explore, Splurge }

    /// <summary>A small thing that happens on a trip or in a room: a line and what it does.</summary>
    public sealed class TravelEvent
    {
        public string Key;
        public int Mood;             // -2 to 2: which of the mod's shared mood modifiers, 0 for none
        public int Money;            // signed dollars, never more than the player has
        public int Energy;           // signed
        public bool Companion;       // only with somebody along; its text has {name}
        public bool Souvenir;        // comes home with one more thing to give away
    }

    public static class TravelCatalogue
    {
        public const int CheckoutHour = 11;
        public const int LateCheckoutHour = 16;
        public const float LateCheckoutShare = 0.3f;     // of a night's rate; free in a luxury room
        public const float DepositSurcharge = 0.05f;     // paying at checkout instead of up front
        public const float WeekendFactor = 1.2f;         // Friday and Saturday nights
        public const float WeeklyFactor = 0.8f;          // seven nights or more, every night
        public const float FortnightFactor = 0.72f;      // fourteen or more
        public const int StampsForFreeNight = 8;
        public const int FreeNightCap = 250;             // the most a loyalty night is worth
        public static int SuitcasePrice => Prices.At(120);
        public const float InsuranceShare = 0.08f;       // of the trip's price
        public const float CompanionShare = 0.7f;        // a second traveller costs this much more
        public const float CancelChance = 0.06f;         // a trip falls through at the gate
        public const float UninsuredRefund = 0.4f;
        public const int TicketDays = 7;                 // a booked trip must be begun within this many days
        public const float ExpiredRefund = 0.5f;
        public const int BurnoutWarnDays = 18;           // days without a break before a friend says something
        public const int BurnoutDays = 24;               // ...and before work starts to suffer
        public const int TradeFairEveryDays = 21;

        public static readonly int[] NightChoices = { 1, 2, 3, 5, 7, 14 };

        private static readonly RoomType Dorm = new RoomType { Id = "dorm", NameKey = "reallife_travel_room_dorm", Rate = 45, Quality = 0, Bed = 0, BedBonus = -0.1f, RoughNight = 0.45f, Refund = 0f };
        private static readonly RoomType Single = new RoomType { Id = "single", NameKey = "reallife_travel_room_single", Rate = 75, Quality = 1, Bed = 1, BedBonus = 0f, RoughNight = 0.25f, Refund = 0f };
        private static readonly RoomType Standard = new RoomType { Id = "standard", NameKey = "reallife_travel_room_standard", Rate = 150, Quality = 2, Bed = 2, BedBonus = 0f, RoughNight = 0.05f, Refund = 0.5f };
        private static readonly RoomType Deluxe = new RoomType { Id = "deluxe", NameKey = "reallife_travel_room_deluxe", Rate = 230, Quality = 2, Bed = 2, BedBonus = 0.2f, RoughNight = 0f, Refund = 0.5f };
        private static readonly RoomType King = new RoomType { Id = "king", NameKey = "reallife_travel_room_king", Rate = 480, Quality = 3, Bed = 2, BedBonus = 0.4f, RoughNight = 0f, Refund = 0.7f };
        private static readonly RoomType Suite = new RoomType { Id = "suite", NameKey = "reallife_travel_room_suite", Rate = 900, Quality = 3, Bed = 2, BedBonus = 0.6f, RoughNight = 0f, Refund = 0.7f };
        private static readonly RoomType CottageRoom = new RoomType { Id = "cottage_room", NameKey = "reallife_travel_room_cottage_room", Rate = 210, Quality = 2, Bed = 2, BedBonus = 0.15f, RoughNight = 0f, Refund = 0.5f };
        private static readonly RoomType CottageWhole = new RoomType { Id = "cottage_whole", NameKey = "reallife_travel_room_cottage_whole", Rate = 380, Quality = 3, Bed = 2, BedBonus = 0.35f, RoughNight = 0f, Refund = 0.5f };

        private static readonly RoomType[][] RoomsByTier =
        {
            new[] { Dorm, Single }, new[] { Standard, Deluxe }, new[] { King, Suite }, new[] { CottageRoom, CottageWhole }
        };

        public const string HamptonsSlot = "hamptons_cottages";
        public const string Hamptons = "ba:neighborhood_thehamptons";

        /// <summary>
        /// The eight places to stay. A city hotel is the residential building of its district, of
        /// the sizes named, with the most passing trade (ties by street name, then number) that the
        /// player neither rents nor owns. With the game's data as shipped these are: Fourth Avenue 1
        /// (Garment District), Twenty-first Street 23 (Industry City), Second Avenue 8 (Hell's
        /// Kitchen), Fourth Avenue 45 (Lower Manhattan), Sixth Avenue 18 (Murray Hill), Broadway 13
        /// (Midtown), Second Avenue 14 (Hell's Kitchen).
        /// </summary>
        public static readonly HotelSlot[] Hotels =
        {
            new HotelSlot { Id = "garment_hostel", NameKey = "reallife_travel_hotel_garment_hostel", Tier = HotelTier.Budget, Neighbourhood = "ba:neighborhood_garmentdistrict", Sizes = "f", RateFactor = 1f },
            new HotelSlot { Id = "industry_hostel", NameKey = "reallife_travel_hotel_industry_hostel", Tier = HotelTier.Budget, Neighbourhood = "ba:neighborhood_industrycity", Sizes = "fl", RateFactor = 0.85f },
            new HotelSlot { Id = "kitchen_hotel", NameKey = "reallife_travel_hotel_kitchen_hotel", Tier = HotelTier.MidRange, Neighbourhood = "ba:neighborhood_hellskitchen", Sizes = "l", RateFactor = 1f, Doorman = true },
            new HotelSlot { Id = "downtown_hotel", NameKey = "reallife_travel_hotel_downtown_hotel", Tier = HotelTier.MidRange, Neighbourhood = "ba:neighborhood_lowermanhattan", Sizes = "nl", RateFactor = 0.85f, Doorman = true },
            new HotelSlot { Id = "murray_hotel", NameKey = "reallife_travel_hotel_murray_hotel", Tier = HotelTier.MidRange, Neighbourhood = "ba:neighborhood_murrayhill", Sizes = "l", RateFactor = 0.95f, Doorman = true },
            new HotelSlot { Id = "midtown_grand", NameKey = "reallife_travel_hotel_midtown_grand", Tier = HotelTier.Luxury, Neighbourhood = "ba:neighborhood_midtown", Sizes = "n", RateFactor = 1.2f, Doorman = true },
            new HotelSlot { Id = "kitchen_tower", NameKey = "reallife_travel_hotel_kitchen_tower", Tier = HotelTier.Luxury, Neighbourhood = "ba:neighborhood_hellskitchen", Sizes = "n", RateFactor = 1f, Doorman = true },
            new HotelSlot
            {
                Id = HamptonsSlot, NameKey = "reallife_travel_hotel_hamptons_cottages", Tier = HotelTier.Cottage, Neighbourhood = Hamptons, RateFactor = 1f,
                Anchors = new[] { "ba:street_oceancrestroad|1", "ba:street_harborstreet|1", "ba:street_seventhavenue|5" }
            }
        };

        public static readonly Trip[] Trips =
        {
            new Trip
            {
                Id = "hamptons", NameKey = "reallife_travel_trip_hamptons", PitchKey = "reallife_travel_pitch_hamptons", Kind = TripKind.Real, Days = 2, Price = 520, FirstClass = 1.75f,
                Peak = TravelSeason.Summer, Low = TravelSeason.Winter, Splurge = 90, DepartKey = "reallife_travel_depart_hamptons", ReturnKey = "reallife_travel_return_hamptons",
                DayKeys = new[] { "reallife_travel_day_hamptons_1", "reallife_travel_day_hamptons_2" },
                Events = new[] { "reallife_travel_ev_hamptons_1", "reallife_travel_ev_hamptons_2", "reallife_travel_ev_hamptons_3", "reallife_travel_ev_hamptons_4" }
            },
            new Trip
            {
                Id = "cruise", NameKey = "reallife_travel_trip_cruise", PitchKey = "reallife_travel_pitch_cruise", Kind = TripKind.Abstract, Days = 3, Price = 1500, FirstClass = 1.9f,
                Peak = TravelSeason.Summer, Low = TravelSeason.Autumn, Splurge = 160, DepartKey = "reallife_travel_depart_cruise", ReturnKey = "reallife_travel_return_cruise",
                DayKeys = new[] { "reallife_travel_day_cruise_1", "reallife_travel_day_cruise_2", "reallife_travel_day_cruise_3" },
                Events = new[] { "reallife_travel_ev_cruise_1", "reallife_travel_ev_cruise_2", "reallife_travel_ev_cruise_3", "reallife_travel_ev_cruise_4" }
            },
            new Trip
            {
                Id = "ski", NameKey = "reallife_travel_trip_ski", PitchKey = "reallife_travel_pitch_ski", Kind = TripKind.Abstract, Days = 4, Price = 2900, FirstClass = 1.8f,
                Peak = TravelSeason.Winter, Low = TravelSeason.Summer, Splurge = 220, DepartKey = "reallife_travel_depart_ski", ReturnKey = "reallife_travel_return_ski",
                DayKeys = new[] { "reallife_travel_day_ski_1", "reallife_travel_day_ski_2", "reallife_travel_day_ski_3", "reallife_travel_day_ski_4" },
                Events = new[] { "reallife_travel_ev_ski_1", "reallife_travel_ev_ski_2", "reallife_travel_ev_ski_3", "reallife_travel_ev_ski_4" }
            },
            new Trip
            {
                Id = "miami", NameKey = "reallife_travel_trip_miami", PitchKey = "reallife_travel_pitch_miami", Kind = TripKind.Abstract, Days = 5, Price = 2400, FirstClass = 1.8f,
                Peak = TravelSeason.Winter, Low = TravelSeason.Summer, Splurge = 180, DepartKey = "reallife_travel_depart_miami", ReturnKey = "reallife_travel_return_miami",
                DayKeys = new[] { "reallife_travel_day_miami_1", "reallife_travel_day_miami_2", "reallife_travel_day_miami_3", "reallife_travel_day_miami_4", "reallife_travel_day_miami_5" },
                Events = new[] { "reallife_travel_ev_miami_1", "reallife_travel_ev_miami_2", "reallife_travel_ev_miami_3", "reallife_travel_ev_miami_4" }
            },
            new Trip
            {
                Id = "europe", NameKey = "reallife_travel_trip_europe", PitchKey = "reallife_travel_pitch_europe", Kind = TripKind.Abstract, Days = 7, Price = 5600, FirstClass = 1.75f,
                Peak = TravelSeason.Summer, Low = TravelSeason.Winter, Splurge = 300, JetLag = true, DepartKey = "reallife_travel_depart_europe", ReturnKey = "reallife_travel_return_europe",
                DayKeys = new[]
                {
                    "reallife_travel_day_europe_1", "reallife_travel_day_europe_2", "reallife_travel_day_europe_3", "reallife_travel_day_europe_4", "reallife_travel_day_europe_5",
                    "reallife_travel_day_europe_6", "reallife_travel_day_europe_7"
                },
                Events = new[] { "reallife_travel_ev_europe_1", "reallife_travel_ev_europe_2", "reallife_travel_ev_europe_3", "reallife_travel_ev_europe_4" }
            },
            new Trip
            {
                Id = "tradefair", NameKey = "reallife_travel_trip_tradefair", PitchKey = "reallife_travel_pitch_tradefair", Kind = TripKind.Business, Days = 2, Price = 900, FirstClass = 1.6f,
                Peak = TravelSeason.Autumn, Low = TravelSeason.Summer, Splurge = 140, DepartKey = "reallife_travel_depart_tradefair", ReturnKey = "reallife_travel_return_tradefair",
                DayKeys = new[] { "reallife_travel_day_tradefair_1", "reallife_travel_day_tradefair_2" },
                Events = new[] { "reallife_travel_ev_tradefair_1", "reallife_travel_ev_tradefair_2", "reallife_travel_ev_tradefair_3", "reallife_travel_ev_tradefair_4" }
            }
        };

        /// <summary>What can happen on a day spent resting.</summary>
        public static readonly TravelEvent[] RelaxEvents =
        {
            E("reallife_travel_ev_relax_1", 1), E("reallife_travel_ev_relax_2", 1, 0, 6), E("reallife_travel_ev_relax_3", 0, 0, 8), E("reallife_travel_ev_relax_4", 1),
            E("reallife_travel_ev_relax_5", -1), E("reallife_travel_ev_relax_6", 2), E("reallife_travel_ev_relax_7", 0, -25), E("reallife_travel_ev_relax_8", 1, 0, 4),
            E("reallife_travel_ev_relax_9", -1, 0, -6), E("reallife_travel_ev_relax_10", 1), E("reallife_travel_ev_relax_11", 0, 0, 5), E("reallife_travel_ev_relax_12", 2)
        };

        /// <summary>...on a day spent looking about.</summary>
        public static readonly TravelEvent[] ExploreEvents =
        {
            E("reallife_travel_ev_explore_1", 1, 0, -6), E("reallife_travel_ev_explore_2", 2, 0, -8), Gift("reallife_travel_ev_explore_3", 1, -30), E("reallife_travel_ev_explore_4", -1, -60),
            E("reallife_travel_ev_explore_5", 1, -15, -4), E("reallife_travel_ev_explore_6", 0, 0, -10), E("reallife_travel_ev_explore_7", 2), Gift("reallife_travel_ev_explore_8", 1, -45),
            E("reallife_travel_ev_explore_9", -1, 0, -8), E("reallife_travel_ev_explore_10", 1, 40), E("reallife_travel_ev_explore_11", 1, 0, -5), E("reallife_travel_ev_explore_12", -2, -90),
            E("reallife_travel_ev_explore_13", 2, 0, -6), E("reallife_travel_ev_explore_14", 0, -20), E("reallife_travel_ev_explore_15", 1), E("reallife_travel_ev_explore_16", 1, 0, -4)
        };

        /// <summary>...on a day of spending.</summary>
        public static readonly TravelEvent[] SplurgeEvents =
        {
            Gift("reallife_travel_ev_splurge_1", 2), E("reallife_travel_ev_splurge_2", 2), Gift("reallife_travel_ev_splurge_3", 1), E("reallife_travel_ev_splurge_4", 1, -80),
            E("reallife_travel_ev_splurge_5", 2, 0, 5), E("reallife_travel_ev_splurge_6", -1), Gift("reallife_travel_ev_splurge_7", 2), E("reallife_travel_ev_splurge_8", 1, 120),
            E("reallife_travel_ev_splurge_9", 2), E("reallife_travel_ev_splurge_10", 0, -50), Gift("reallife_travel_ev_splurge_11", 1), E("reallife_travel_ev_splurge_12", 2, 0, 6)
        };

        /// <summary>...with somebody along ({name}); one of these takes the place of the day's own event now and then.</summary>
        public static readonly TravelEvent[] CompanionEvents =
        {
            With("reallife_travel_ev_companion_1", 2), With("reallife_travel_ev_companion_2", 1), With("reallife_travel_ev_companion_3", 2), With("reallife_travel_ev_companion_4", -1),
            With("reallife_travel_ev_companion_5", 1), With("reallife_travel_ev_companion_6", 2), With("reallife_travel_ev_companion_7", 1), With("reallife_travel_ev_companion_8", 2)
        };

        /// <summary>Each destination's own.</summary>
        public static readonly TravelEvent[] PlaceEvents =
        {
            E("reallife_travel_ev_hamptons_1", 1), E("reallife_travel_ev_hamptons_2", 2), E("reallife_travel_ev_hamptons_3", 0, -35), E("reallife_travel_ev_hamptons_4", 1, 0, 5),
            E("reallife_travel_ev_cruise_1", 1), E("reallife_travel_ev_cruise_2", -1, 0, -6), E("reallife_travel_ev_cruise_3", 2), E("reallife_travel_ev_cruise_4", 1, 60),
            E("reallife_travel_ev_ski_1", 2, 0, -8), E("reallife_travel_ev_ski_2", -1, 0, -10), E("reallife_travel_ev_ski_3", 1), E("reallife_travel_ev_ski_4", 1, -40),
            E("reallife_travel_ev_miami_1", 2), E("reallife_travel_ev_miami_2", -1), E("reallife_travel_ev_miami_3", 1, -30), E("reallife_travel_ev_miami_4", 1, 0, 6),
            E("reallife_travel_ev_europe_1", 2), E("reallife_travel_ev_europe_2", -1, -70), Gift("reallife_travel_ev_europe_3", 1, -40), E("reallife_travel_ev_europe_4", 2, 0, -6),
            E("reallife_travel_ev_tradefair_1", 1), E("reallife_travel_ev_tradefair_2", 1, 0, -6), E("reallife_travel_ev_tradefair_3", 0, -40), E("reallife_travel_ev_tradefair_4", 2)
        };

        /// <summary>A bad night in a cheap room.</summary>
        public static readonly TravelEvent[] RoughNights =
        {
            E("reallife_travel_ev_rough_1", -1, 0, -10), E("reallife_travel_ev_rough_2", -1, 0, -8), E("reallife_travel_ev_rough_3", -2, 0, -12), E("reallife_travel_ev_rough_4", -1, 0, -6),
            E("reallife_travel_ev_rough_5", -1, -20, -5), E("reallife_travel_ev_rough_6", -1, 0, -9), E("reallife_travel_ev_rough_7", 0, 0, -7), E("reallife_travel_ev_rough_8", -2, 0, -10),
            E("reallife_travel_ev_rough_9", -1, 0, -8), E("reallife_travel_ev_rough_10", 1, 0, -4)
        };

        /// <summary>The small kindnesses of a good hotel, one each morning.</summary>
        public static readonly TravelEvent[] FineMornings =
        {
            E("reallife_travel_ev_fine_1", 1), E("reallife_travel_ev_fine_2", 1, 0, 4), E("reallife_travel_ev_fine_3", 1), E("reallife_travel_ev_fine_4", 2),
            E("reallife_travel_ev_fine_5", 1, 0, 3), E("reallife_travel_ev_fine_6", 1)
        };

        /// <summary>A walk on the beach at the cottages.</summary>
        public static readonly TravelEvent[] BeachWalks =
        {
            E("reallife_travel_ev_beach_1", 1), Gift("reallife_travel_ev_beach_2", 1), E("reallife_travel_ev_beach_3", 2), E("reallife_travel_ev_beach_4", 0, 0, -4),
            E("reallife_travel_ev_beach_5", 1, 20), E("reallife_travel_ev_beach_6", 2), E("reallife_travel_ev_beach_7", 1), E("reallife_travel_ev_beach_8", -1)
        };

        /// <summary>Why a trip fell through at the gate.</summary>
        public static readonly string[] Cancellations =
        {
            "reallife_travel_cancel_1", "reallife_travel_cancel_2", "reallife_travel_cancel_3", "reallife_travel_cancel_4"
        };

        public static readonly string[] DeskGreetings =
        {
            "reallife_travel_desk_greet_1", "reallife_travel_desk_greet_2", "reallife_travel_desk_greet_3", "reallife_travel_desk_greet_4"
        };

        public static readonly string[] HostelGreetings =
        {
            "reallife_travel_hostel_greet_1", "reallife_travel_hostel_greet_2", "reallife_travel_hostel_greet_3"
        };

        public static readonly string[] AgentGreetings =
        {
            "reallife_travel_agent_greet_1", "reallife_travel_agent_greet_2", "reallife_travel_agent_greet_3", "reallife_travel_agent_greet_4"
        };

        public static readonly string[] SleptWell =
        {
            "reallife_travel_slept_1", "reallife_travel_slept_2", "reallife_travel_slept_3", "reallife_travel_slept_4"
        };

        public static readonly string[] BurnoutNudges =
        {
            "reallife_travel_burnout_friend_1", "reallife_travel_burnout_friend_2", "reallife_travel_burnout_friend_3"
        };

        private static TravelEvent E(string key, int mood, int money = 0, int energy = 0)
        {
            return new TravelEvent { Key = key, Mood = mood, Money = money, Energy = energy };
        }

        private static TravelEvent Gift(string key, int mood, int money = 0)
        {
            return new TravelEvent { Key = key, Mood = mood, Money = money, Souvenir = true };
        }

        private static TravelEvent With(string key, int mood)
        {
            return new TravelEvent { Key = key, Mood = mood, Companion = true };
        }

        public static RoomType[] Rooms(HotelTier tier)
        {
            return RoomsByTier[(int)tier];
        }

        public static RoomType Room(string id)
        {
            for (int t = 0; t < RoomsByTier.Length; t++)
            {
                for (int i = 0; i < RoomsByTier[t].Length; i++)
                {
                    if (RoomsByTier[t][i].Id == id)
                    {
                        return RoomsByTier[t][i];
                    }
                }
            }
            return null;
        }

        public static HotelSlot Hotel(string id)
        {
            for (int i = 0; i < Hotels.Length; i++)
            {
                if (Hotels[i].Id == id)
                {
                    return Hotels[i];
                }
            }
            return null;
        }

        /// <summary>The dorm bed's and the corner suite's own rates (before the hotel's district, the day and the season), at today's prices.</summary>
        public static int DormRate => Prices.At(Dorm.Rate);
        public static int SuiteRate => Prices.At(Suite.Rate);

        public static Trip TripById(string id)
        {
            for (int i = 0; i < Trips.Length; i++)
            {
                if (Trips[i].Id == id)
                {
                    return Trips[i];
                }
            }
            return null;
        }

        public static TravelEvent PlaceEvent(string key)
        {
            for (int i = 0; i < PlaceEvents.Length; i++)
            {
                if (PlaceEvents[i].Key == key)
                {
                    return PlaceEvents[i];
                }
            }
            return null;
        }

        /// <summary>The season of a day, the weather's own (GameCalendar): quarters of the year the player chose, counted from spring.</summary>
        public static TravelSeason Season(int day)
        {
            return (TravelSeason)GameCalendar.SeasonFromSpring(day);
        }

        /// <param name="weekday">The game's ordered day of the week: 1 Monday to 7 Sunday.</param>
        public static bool WeekendNight(int weekday)
        {
            return weekday == 5 || weekday == 6;
        }

        public static float SeasonFactor(HotelSlot hotel, TravelSeason season)
        {
            if (hotel.Tier == HotelTier.Cottage)
            {
                return season == TravelSeason.Summer ? 1.4f : season == TravelSeason.Winter ? 0.75f : 1f;
            }
            return season == TravelSeason.Summer ? 1.15f : season == TravelSeason.Winter ? 0.9f : 1f;
        }

        /// <summary>What one night costs, rounded to the dollar.</summary>
        /// <param name="demand">The leisure bridge's figure for the day: 1 on an ordinary one.</param>
        public static int NightRate(HotelSlot hotel, RoomType room, int day, int weekday, float demand)
        {
            float rate = room.Rate * hotel.RateFactor * SeasonFactor(hotel, Season(day));
            if (WeekendNight(weekday))
            {
                rate *= WeekendFactor;
            }
            rate *= Math.Min(Math.Max(demand, 0.8f), 1.6f);
            return Math.Max(1, (int)Math.Round(Prices.At(rate)));   // at today's prices
        }

        public static float LongStayFactor(int nights)
        {
            return nights >= 14 ? FortnightFactor : nights >= 7 ? WeeklyFactor : 1f;
        }

        /// <summary>
        /// How many of a kind of room are free on a day, 0 for none: the same answer all day and in
        /// every session (a hash of the day, the hotel and the room), fewer at weekends, in the
        /// dear season and when the leisure bridge says the town is busy.
        /// </summary>
        public static int RoomsFree(HotelSlot hotel, RoomType room, int day, int weekday, float demand)
        {
            uint hash = Hash(hotel.Id) * 31u + Hash(room.Id) * 17u + (uint)day * 2654435761u;
            hash ^= hash >> 15;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            int roll = (int)(hash % 100u);                      // 0 to 99
            float busy = 0.10f;                                 // an ordinary weekday: one day in ten a kind of room is gone
            if (WeekendNight(weekday)) busy += 0.12f;
            if (SeasonFactor(hotel, Season(day)) > 1.05f) busy += 0.08f;
            busy += Math.Max(0f, demand - 1f) * 0.6f;
            if (room.Quality == 0) busy *= 0.5f;                // there is nearly always a bunk
            int threshold = (int)(Math.Min(busy, 0.85f) * 100f);
            return roll < threshold ? 0 : 1 + (roll - threshold) / 12;
        }

        public static int TripPrice(Trip trip, int day, bool first, bool companion, bool insured, int coach)
        {
            TravelSeason season = Season(day);
            float price = trip.Price * (season == trip.Peak ? 1.25f : season == trip.Low ? 0.85f : 1f);
            if (first)
            {
                price *= trip.FirstClass;
            }
            if (companion)
            {
                price *= 1f + CompanionShare;
            }
            price += coach * (companion ? 2 : 1);
            if (insured)
            {
                price *= 1f + InsuranceShare;
            }
            return (int)Math.Round(Prices.At(price) / 10f) * 10;   // at today's prices
        }

        private static uint Hash(string text)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < text.Length; i++)
            {
                hash = (hash ^ text[i]) * 16777619u;
            }
            return hash;
        }
    }
}
