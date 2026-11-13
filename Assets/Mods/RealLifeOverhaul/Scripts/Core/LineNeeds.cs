using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What a line's words take for granted about the player: a thing of their own, or where they are. Each is read from the
    /// game's own data by <see cref="LineNeedsGame"/>, never from the mod's guess. A home's things (<see cref="AtHome"/>) are
    /// asked of one home together: the one the player stands in, else any home of theirs.
    /// </summary>
    [Flags]
    public enum LineNeed
    {
        None = 0,
        Home = 1 << 0,       // a residential building rented or owned: a flat in the city or a Hamptons house (the game's residential type)
        Flat = 1 << 1,       // ...a flat in a block, with neighbours above and a lobby: not a Hamptons house (Building.IsHamptonsHouse)
        Rented = 1 << 2,     // ...rented, so there is a landlord and a super: not one the player owns (BuildingRegistration.BuildingOwnedByPlayer)
        Fridge = 1 << 3,     // ...with a fridge or freezer among its furniture (the registration's itemInstances, by item name)
        Sink = 1 << 4,       // ...with a sink, so a kitchen tap
        Bath = 1 << 5,       // ...with a bathtub
        Bed = 1 << 6,        // ...with a bed
        Computer = 1 << 7,   // ...with a computer or laptop
        Car = 1 << 8,        // a car, van or lorry of the player's own (VehicleType.enclosed): not a bicycle, motorbike or scooter
        InCar = 1 << 9,      // driving one of those now: a seat, a radio, wipers and a spare
        Business = 1 << 10,  // a business of the player's own, by the test the game counts them with
        Away = 1 << 11       // not inside a home of the player's own: a till, a cashier, a cat in somebody's window
    }

    /// <summary>
    /// The one rule for lines that speak of what the player may not have. A random event, notice, bill or text whose words
    /// assume a thing (a fridge, a landlord, a car) is listed in <see cref="Table"/> with what it needs, and every place that
    /// draws such lines asks here: a line whose needs do not hold is never drawn, on any path (the event's roll, the pick
    /// from a pool, a single line before it is said). A player once got a bill for repairing a fridge they did not own.
    /// A key not listed needs nothing. New lines declare their needs here; EconomyCheck's LineNeedChecks reads every random
    /// line's English for the things it names and fails on one that is not listed with them. Game-free, with the reads
    /// handed in by the wiring (<see cref="Read"/>), so the checkers compile it. COST: a dictionary lookup a line; a read of
    /// the player's homes or vehicles only for a listed line, when a line is drawn (an event's hourly roll, a text).
    /// </summary>
    public static class LineNeeds
    {
        /// <summary>The needs answered by one home together.</summary>
        public const LineNeed AtHome = LineNeed.Home | LineNeed.Flat | LineNeed.Rented | LineNeed.Fridge | LineNeed.Sink | LineNeed.Bath
                                       | LineNeed.Bed | LineNeed.Computer;

        /// <summary>Whether all of these hold now, set by the wiring (Wiring/LifeLinks); null: nothing does, so no listed line is drawn.</summary>
        public static Func<LineNeed, bool> Read;

        /// <summary>Every line that needs something, with what. A line's key in full; a life event's is its own key (reallife_ev_ and its id, by Event).</summary>
        public static readonly KeyValuePair<string, LineNeed>[] Table =
        {
            // Life events (Scripts/Life): the home and what is in it.
            Event("hm12", LineNeed.Fridge),                              // the fridge at home died: the repair's bill
            Event("fd10", LineNeed.Fridge),                              // the fridge at home holds one lemon
            Event("hm02", LineNeed.Sink),                                // the kitchen tap at home: the plumber's bill
            Event("tc10", LineNeed.Computer),                            // coffee met laptop: the keyboard's bill
            Event("tc07", LineNeed.Rented),                              // an email to your landlord
            Event("hm13", LineNeed.Flat | LineNeed.Rented),              // the super and the lobby light
            Event("hm15", LineNeed.Flat | LineNeed.Rented),              // the tenant association and the building's courtyard
            Event("hm25", LineNeed.Flat | LineNeed.Rented),              // a deal with the super
            Event("hm01", LineNeed.Flat),                                // the upstairs neighbour
            Event("hm04", LineNeed.Flat),                                // the neighbour across the hall
            Event("hm06", LineNeed.Flat),                                // the building's hot water
            Event("hm07", LineNeed.Flat),                                // a parcel for two floors up, the lobby
            Event("hm11", LineNeed.Flat),                                // a message to the whole floor
            Event("tc14", LineNeed.Flat),                                // the building's mailing list
            Event("fm21", LineNeed.Flat),                                // Mom rearranging your apartment

            // Life events: a car, and being in one.
            Event("tr10", LineNeed.Car),                                 // a ticket under your wiper
            Event("tr11", LineNeed.Car),                                 // your car door dinged
            Event("tr12", LineNeed.Car),                                 // a note on your windscreen
            Event("tr20", LineNeed.Car),                                 // wash your car
            Event("tr23", LineNeed.Car),                                 // your registration sticker
            Event("tr24", LineNeed.Car),                                 // near your parked car
            Event("tr05", LineNeed.InCar),                               // coins down the side of the seat
            Event("tr08", LineNeed.InCar),                               // a warning light
            Event("tr09", LineNeed.InCar),                               // the radio, windows up
            Event("tr17", LineNeed.InCar),                               // a flat tyre and a flat spare
            Event("wx05", LineNeed.InCar),                               // the wipers

            // Life events: indoors, but somewhere with a till or somebody else's cat.
            Event("mn05", LineNeed.Away),                                // scratchcards by the till
            Event("mn24", LineNeed.Away),                                // the cashier's change
            Event("lk14", LineNeed.Away),                                // the vending machine
            Event("pt03", LineNeed.Away),                                // a cat in the window: at home it would be a pet

            // Family (Scripts/Family): a child's drawings, Mom's visits, the partner's words.
            Row("reallife_family_drawing_1", LineNeed.Fridge),
            Row("reallife_family_drawing_8", LineNeed.Fridge),
            Row("reallife_family_drawing_4", LineNeed.Fridge | LineNeed.Business),   // on the fridge: your shop
            Row("reallife_family_drawing_2", LineNeed.Flat),
            Row("reallife_family_mom_visit_1", LineNeed.Fridge),         // a casserole left in the fridge
            Row("reallife_family_mom_visit_2", LineNeed.Home),
            Row("reallife_family_mom_visit_3", LineNeed.Home),
            Row("reallife_family_encourage_5", LineNeed.Bath),           // runs you a bath
            Row("reallife_family_greet_day_3", LineNeed.Rented),         // I thought it was the landlord
            Row("reallife_family_glad_work_2", LineNeed.Flat),
            Row("reallife_family_glad_stop_2", LineNeed.Flat),
            Row("reallife_family_argue_6", LineNeed.Bed),                // a wet towel on the bed
            Row("reallife_family_inlaws_ask_lift", LineNeed.Car),        // the in-laws ask for a lift
            Row("reallife_family_grown_call_2", LineNeed.Business),      // a grown child saw one of your shops

            // People (Scripts/Social), the street (Scripts/Crime), the roommate and household staff (Scripts/Home).
            Row("reallife_social_invite_business", LineNeed.Business),   // when do I get the tour of your business (SocialAskRules)
            Row("reallife_social_follow_fr09", LineNeed.Business),       // a call taken up on the friend's text about your business
            Row("reallife_social_follow_fm04", LineNeed.Business),       // ...and on Mom's
            Row("reallife_street_open_famous_3", LineNeed.Business),     // a stranger whose friend talks about your businesses
            Row("reallife_season_turn", LineNeed.Business),              // what sells more at your businesses this season
            Row("reallife_social_text_home_2", LineNeed.Flat),
            Row("reallife_social_text_lift_1", LineNeed.Car),
            Row("reallife_social_text_lift_2", LineNeed.Car),
            Row("reallife_crime_parking_near", LineNeed.Car),            // you check it is not your car
            Row("reallife_text_roommate_cooked", LineNeed.Fridge),       // a portion in the fridge
            Row("reallife_roommate_talk_cooked", LineNeed.Fridge),
            Row("reallife_household_note_cooked", LineNeed.Fridge),      // portions in the fridge, at the home the staff work at
            Row("reallife_household_note_butler", LineNeed.Fridge),
            Row("reallife_household_note_full", LineNeed.Fridge)
        };

        private static readonly Dictionary<string, LineNeed> ByKey = Index(Table);

        /// <summary>What the line needs; None for a line that needs nothing or no line.</summary>
        public static LineNeed Of(string key)
        {
            return key != null && ByKey.TryGetValue(key, out LineNeed needs) ? needs : LineNeed.None;
        }

        /// <summary>True if every one of these holds now; nothing needed always holds, a read that fails never does.</summary>
        public static bool Holds(LineNeed needs)
        {
            if (needs == LineNeed.None)
            {
                return true;
            }
            Func<LineNeed, bool> read = Read;
            try
            {
                return read != null && read(needs);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>True if the line may be drawn now: it needs nothing, or all it needs holds. No line never fits.</summary>
        public static bool Fits(string key) => key != null && Holds(Of(key));

        /// <summary>True if at least one line of the pool may be drawn now.</summary>
        public static bool AnyFits(string[] keys)
        {
            for (int i = 0; keys != null && i < keys.Length; i++)
            {
                if (Fits(keys[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The place in the pool of a line that may be drawn now, at random among those that may and never the one at
        /// <paramref name="last"/> while another may; -1 when none may (a null key never does). <paramref name="random01"/>
        /// gives 0 to 1.
        /// </summary>
        public static int Pick(string[] keys, int last, Func<float> random01)
        {
            if (keys == null)
            {
                return -1;
            }
            int fitting = 0;
            bool lastFits = false;
            for (int i = 0; i < keys.Length; i++)
            {
                if (Fits(keys[i]))
                {
                    fitting++;
                    lastFits |= i == last;
                }
            }
            if (fitting == 0)
            {
                return -1;
            }
            bool skipLast = lastFits && fitting > 1;
            int choices = skipLast ? fitting - 1 : fitting;
            int nth = Math.Min((int)(random01() * choices), choices - 1);
            for (int i = 0; i < keys.Length; i++)
            {
                if ((skipLast && i == last) || !Fits(keys[i]))
                {
                    continue;
                }
                if (nth-- == 0)
                {
                    return i;
                }
            }
            return -1;   // a read that changed between the two passes
        }

        /// <summary>The thing an item of the game's furniture is, by its name (ba:itemname_...); None for anything else.</summary>
        public static LineNeed ItemIs(string itemName)
        {
            if (string.IsNullOrEmpty(itemName))
            {
                return LineNeed.None;
            }
            if (Has(itemName, "fridge") || Has(itemName, "freezer"))
            {
                return LineNeed.Fridge;
            }
            if (Has(itemName, "bathtub") || Has(itemName, "bath_tub"))
            {
                return LineNeed.Bath;
            }
            if (Has(itemName, "sink") && !Has(itemName, "sinksoap"))
            {
                return LineNeed.Sink;
            }
            if (Has(itemName, "computer") || Has(itemName, "laptop"))
            {
                return LineNeed.Computer;
            }
            // A bedside table and a lorry's flatbed are not beds.
            if (Has(itemName, "bed") && !Has(itemName, "bedside") && !Has(itemName, "flatbed"))
            {
                return LineNeed.Bed;
            }
            return LineNeed.None;
        }

        private static bool Has(string name, string part) => name.IndexOf(part, StringComparison.Ordinal) >= 0;

        private static KeyValuePair<string, LineNeed> Row(string key, LineNeed needs) => new KeyValuePair<string, LineNeed>(key, needs);

        // A life event's row by its id: the event's key is reallife_ev_ and the id (LifeEvent.Key), a name no text has by itself.
        private static KeyValuePair<string, LineNeed> Event(string id, LineNeed needs) => Row(EventKey + id, needs);

        private const string EventKey = "reallife_ev_";

        private static Dictionary<string, LineNeed> Index(KeyValuePair<string, LineNeed>[] table)
        {
            var index = new Dictionary<string, LineNeed>(table.Length, StringComparer.Ordinal);
            foreach (KeyValuePair<string, LineNeed> row in table)
            {
                index[row.Key] = row.Value;
            }
            return index;
        }
    }
}
