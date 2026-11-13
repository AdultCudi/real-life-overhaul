using System;

namespace RealLifeOverhaul
{
    /// <summary>What the ground at a place is, for something or somebody that stands there.</summary>
    public enum StreetGround
    {
        Pavement,      // pavement or a square: the one answer a standing site is put on
        OffWalk,       // not on the pedestrians' walking ground at street level
        Crossing,      // a marked crossing: walking ground, in the road
        Carriageway,   // a road's own surface under it
        Lane,          // within a lane's width of a traffic lane's line
        BusZone,       // the kerb a bus stop keeps
        Driveway,      // a parking box or a car park's ground: where cars turn in
        Doorway        // in front of something the player can use
    }

    /// <summary>What was read of the ground at a place (CitySpots.Read), handed to <see cref="SiteGroundRules.Verdict"/>.</summary>
    public struct StreetGroundFacts
    {
        public bool Walkable;        // on the pedestrians' walking mesh at street level
        public bool CrossingUnder;   // a crossing's trigger or strip reaches under the footprint
        public bool RoadUnder;       // a road's collider reaches under it (the game's Roads layer; a street tree put there is no road)
        public bool ParkingUnder;    // a parking box or a car park's ground reaches under it
        public bool LaneKnown;       // the traffic system's lanes could be read here
        public float LaneDistance;   // metres on the flat from the place to the nearest lane point
        public bool BusZone;         // the footprint reaches into a bus stop's zone
        public bool DoorNear;        // something the player can use is within the keep-out
        public bool Kerbside;        // what stands there does so at the kerb's edge by its nature (SiteGroundRules.Kerbside): the road and the parking beside it are no fault
        public float Reach;          // metres from the place the footprint reaches
    }

    /// <summary>
    /// The one rule for where a standing site of the living city may be: a chess table, a cart, a stall, a busker's audience,
    /// a crew, a queue, and each person and prop of theirs. The pedestrians' walking ground is not the rule: it runs over
    /// every crossing, so a place could be "pavement" in the middle of a road (a player found a chess table on a crosswalk,
    /// people sitting at it). A place is pavement only when it is walking ground with no crossing, no road surface, no
    /// traffic lane, no bus zone, no parking and no doorway under or beside what stands there. Asked when a place is chosen
    /// and again while the site stands (<see cref="RecheckSeconds"/>), since the street can change under it (a bus zone made,
    /// ground laid). Game-free: CitySpots reads the facts, EconomyCheck --premises runs the samples. COST: a few comparisons.
    /// </summary>
    public static class SiteGroundRules
    {
        public const float LaneHalf = 1.65f;        // metres from a lane's line to its edge (BusBay.LaneHalf: a lane is 3.3 m)
        public const float StandReach = 0.25f;      // metres round a person's or a prop's place that must be pavement: their feet
        public const float SiteReach = 0.6f;        // metres round a site's middle for a kind with no figure of its own
        public const float RecheckSeconds = 20f;    // real seconds between two looks at the ground under a site that is out

        /// <summary>
        /// Every kind of standing site the rule covers, with the metres round its middle its body takes (the people and props
        /// further out are each asked for by themselves): the living city's activities by their catalogue ids, and Leisure's
        /// chess table. A kind added to CityLife/CityCatalogue without a line here fails EconomyCheck.
        /// </summary>
        public static readonly string[] Kinds =
        {
            "sweeper", "window_cleaner", "repairs", "road_crew", "leaflets", "fundraiser", "chat", "smoker", "phone_call", "coffee_break", "street_lunch",
            "mug_break", "tourist", "jogger", "busker_audience", "coffee_cart", "hot_dog_cart", "courier", "mail", "painter", "gardener", "chess", "bench_chat",
            "tai_chi", "newspaper", "map_tourist", "traveller", "pushchair", "car_lean", "doorman", "door_queue", "umbrella", "umbrella_two", LeisureChess
        };

        public const string LeisureChess = "leisure_chess";   // the chess table the player can sit down at (Leisure/Activities/ChessGame)

        /// <summary>Metres round a site's middle that must be pavement, by its kind: a table with its stools, a cart with its vendor, else a person or two.</summary>
        public static float Reach(string kind)
        {
            switch (kind)
            {
                case "chess":
                case LeisureChess:
                    return 1.0f;    // the table (0.4 m) and a stool to either side (0.62 m out)
                case "coffee_cart":
                case "hot_dog_cart":
                    return 1.0f;    // the cart (0.85 m to either end)
                case "road_crew":
                case "busker_audience":
                case "bench_chat":
                    return 0.8f;
                default:
                    return SiteReach;
            }
        }

        /// <summary>
        /// True for a kind that stands at the kerb's very edge by its nature: somebody leaning on a parked car has the parking
        /// lane and the road's edge within their reach, on the pavement all the same. A crossing, a lane and a bus zone refuse
        /// it like any other.
        /// </summary>
        public static bool Kerbside(string kind) => kind == "car_lean";

        /// <summary>True if the kind is one the rule knows.</summary>
        public static bool Covers(string kind) => Array.IndexOf(Kinds, kind) >= 0;

        /// <summary>What the ground is, the first thing wrong with it first: only <see cref="StreetGround.Pavement"/> may be stood on.</summary>
        public static StreetGround Verdict(in StreetGroundFacts f)
        {
            if (!f.Walkable)
            {
                return StreetGround.OffWalk;
            }
            if (f.CrossingUnder)
            {
                return StreetGround.Crossing;
            }
            if (f.RoadUnder && !f.Kerbside)
            {
                return StreetGround.Carriageway;
            }
            if (f.LaneKnown && f.LaneDistance < LaneHalf + Math.Max(0f, f.Reach))
            {
                return StreetGround.Lane;
            }
            if (f.BusZone)
            {
                return StreetGround.BusZone;
            }
            if (f.ParkingUnder && !f.Kerbside)
            {
                return StreetGround.Driveway;
            }
            return f.DoorNear ? StreetGround.Doorway : StreetGround.Pavement;
        }

        /// <summary>True only for pavement or a square.</summary>
        public static bool Stands(in StreetGroundFacts f) => Verdict(in f) == StreetGround.Pavement;

        /// <summary>The reason in words, for the one log line a refused or cleared site gets.</summary>
        public static string Words(StreetGround ground)
        {
            switch (ground)
            {
                case StreetGround.Pavement: return "pavement";
                case StreetGround.OffWalk: return "no walking ground";
                case StreetGround.Crossing: return "a crossing";
                case StreetGround.Carriageway: return "the road";
                case StreetGround.Lane: return "a traffic lane";
                case StreetGround.BusZone: return "a bus zone";
                case StreetGround.Driveway: return "parking or a driveway";
                default: return "a doorway";
            }
        }
    }
}
