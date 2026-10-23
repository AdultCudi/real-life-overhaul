using System;
using System.Collections.Generic;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>The kind of building a lot of the district takes, as its plan has it (the ground's own LotClass, read by LifeGame).</summary>
    public enum BuildingsClass
    {
        Small,
        Office,
        Big,
        Warehouse
    }

    /// <summary>A lot of the district as the buildings' rules read it, made from the ground's plan by the game side. Game-free.</summary>
    public struct BuildingsLot
    {
        public string Id;              // the plan's stable id, the one builders and the store know it by
        public string Street;          // a street of the mod's own (reallife:street_...)
        public int Number;             // its house number on that street, never changed between versions: saves keep records by it
        public bool First;             // in the district's first block
        public bool Avenue;            // on one of its avenues (they run west to east, its main roads), not one of its streets
        public float X;                // m: the lot's middle
        public float Z;
        public float Facing;           // degrees, MadeLot's: the way the street lies from the lot
        public int Width;              // m along the street
        public int Depth;              // m back from it
        public BuildingsClass Class;
        public IReadOnlyList<string> Sizes;   // the game's sizes its class takes (the plan's)
    }

    /// <summary>A structure the game has for a building: its size, version and type.</summary>
    public struct BuildingsStructure
    {
        public string Size;
        public int Version;
        public string Type;
    }

    /// <summary>Who a finished building is given to at its first load.</summary>
    public enum FinishOwner
    {
        City,    // let to the city's businesses: for rent, a rival's landlord, the game's daily pass after
        Player   // the player's own: a deed of the game's, as BizMan's purchase writes it
    }

    /// <summary>
    /// The rules of the district's buildings, game-free (EconomyCheck's UptownLifeChecks): which of the first block's lots
    /// stand from the first day, which of the game's structures a lot's building is made with, its traffic index, and
    /// what refuses a building finished by the construction game. Every answer comes from the arguments alone, picks by
    /// FNV-1a hashes of the lot's id: the same on every machine with the same mod, in any order the lots come.
    /// COST: nothing per frame; a sort of the first block's lots and a pass over each lot at a load.
    /// </summary>
    public static class BuildingsRules
    {
        public const float DayOneShare = 0.25f;     // of the first block's lots built from the first day: the rest wait for the construction game
        public const float AvenuePull = 30f;        // m: an avenue lot counts this much nearer Manhattan when the first buildings are placed
        public const int AvenueTraffic = 38;        // traffic index of a lot on an avenue: Midtown's buildings' median, 38.5
        public const int StreetTraffic = 22;        // on a side street: between Hell's Kitchen's first quarter (14) and its median (27.5)
        public const float TrafficStepMetres = 25f; // m further from Manhattan than the plan's nearest lot for each step down
        public const int TrafficStep = 2;           // traffic index less for each step
        public const int TrafficStepsMost = 5;      // steps at most: 10 less
        public const int TrafficJitter = 3;         // at most this much more or less, from the lot id's hash: lots side by side are never quite alike
        public const int TrafficLeast = 10;         // the game's own least
        public const int TrafficMost = 60;          // well under the game's busiest (84): a new district is quieter than Midtown
        public const int StoreysMost = NewBuildingsRules.MostFloors;   // storeys a finished building's record may have

        public const string SpecialType = NewBuildingsRules.SpecialType;
        public const string HomeType = NewBuildingsRules.HomeType;

        /// <summary>How many of the first block's lots stand from the first day: a quarter, halves up, at least one when there are any.</summary>
        public static int DayOneCount(int firstBlockLots)
        {
            return firstBlockLots <= 0 ? 0 : Math.Max(1, (int)Math.Round(firstBlockLots * DayOneShare, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// How far a lot is from Manhattan along the way the district runs from it (<paramref name="awayX"/>,
        /// <paramref name="awayZ"/>: a unit direction; north of Manhattan it is (0, 1)), an avenue lot pulled
        /// <see cref="AvenuePull"/> nearer.
        /// </summary>
        public static float Reach(BuildingsLot lot, float awayX, float awayZ)
        {
            return lot.X * awayX + lot.Z * awayZ - (lot.Avenue ? AvenuePull : 0f);
        }

        /// <summary>
        /// The lots that stand from the first day, as indices into <paramref name="lots"/> (into the caller's list, cleared):
        /// of the first block's, a quarter, those nearest Manhattan and the avenues first, so the first streets out of
        /// Manhattan are built up as one place; equals by their ids, ordinal. In the lots' order of their ids.
        /// </summary>
        public static List<int> DayOne(IList<BuildingsLot> lots, float awayX, float awayZ, List<int> into)
        {
            into.Clear();
            if (lots == null)
            {
                return into;
            }
            var first = new List<int>(lots.Count);
            for (int i = 0; i < lots.Count; i++)
            {
                if (lots[i].First && !string.IsNullOrEmpty(lots[i].Id))
                {
                    first.Add(i);
                }
            }
            first.Sort((a, b) =>
            {
                int byReach = Reach(lots[a], awayX, awayZ).CompareTo(Reach(lots[b], awayX, awayZ));
                return byReach != 0 ? byReach : string.CompareOrdinal(lots[a].Id, lots[b].Id);
            });
            int wanted = DayOneCount(first.Count);
            for (int i = 0; i < wanted; i++)
            {
                into.Add(first[i]);
            }
            into.Sort((a, b) => string.CompareOrdinal(lots[a].Id, lots[b].Id));
            return into;
        }

        /// <summary>The least reach of any lot of the plan: where the traffic steps are counted from.</summary>
        public static float Nearest(IList<BuildingsLot> lots, float awayX, float awayZ)
        {
            float least = float.MaxValue;
            for (int i = 0; lots != null && i < lots.Count; i++)
            {
                least = Math.Min(least, lots[i].X * awayX + lots[i].Z * awayZ);
            }
            return least == float.MaxValue ? 0f : least;
        }

        /// <summary>
        /// A lot's traffic index, the Building.trafficIndex its building gets: <see cref="AvenueTraffic"/> on an avenue,
        /// <see cref="StreetTraffic"/> on a side street, <see cref="TrafficStep"/> less for each <see cref="TrafficStepMetres"/>
        /// further from Manhattan than <paramref name="nearest"/> (at most <see cref="TrafficStepsMost"/> steps), then up to
        /// <see cref="TrafficJitter"/> more or less from its id's hash, kept from <see cref="TrafficLeast"/> to <see cref="TrafficMost"/>.
        /// </summary>
        public static int Traffic(BuildingsLot lot, float nearest, float awayX, float awayZ)
        {
            float along = lot.X * awayX + lot.Z * awayZ - nearest;
            int steps = Math.Max(0, Math.Min(TrafficStepsMost, (int)Math.Floor(along / TrafficStepMetres)));
            int jitter = (int)(NewBuildingsRules.Hash(lot.Id) % (uint)(2 * TrafficJitter + 1)) - TrafficJitter;
            int index = (lot.Avenue ? AvenueTraffic : StreetTraffic) - TrafficStep * steps + jitter;
            return Math.Max(TrafficLeast, Math.Min(TrafficMost, index));
        }

        /// <summary>The order the game's structures are offered in: size, type, then version, ordinal; the same game gives the same list everywhere.</summary>
        public static int CompareStructures(BuildingsStructure a, BuildingsStructure b)
        {
            int bySize = string.CompareOrdinal(a.Size, b.Size);
            if (bySize != 0)
            {
                return bySize;
            }
            int byType = string.CompareOrdinal(a.Type, b.Type);
            return byType != 0 ? byType : a.Version.CompareTo(b.Version);
        }

        /// <summary>
        /// The structure a day-one building on a lot is made with: of <paramref name="options"/> (every size, version and type the
        /// game has for the lot's class's sizes, never its special one, in <see cref="CompareStructures"/> order), the one its
        /// id's hash picks; -1 when there is none.
        /// </summary>
        public static int PickStructure(string lotId, IList<BuildingsStructure> options)
        {
            if (options == null || options.Count == 0 || string.IsNullOrEmpty(lotId))
            {
                return -1;
            }
            return (int)(NewBuildingsRules.Hash(lotId + "|structure") % (uint)options.Count);
        }

        /// <summary>
        /// True when a lot's street runs west to east, an avenue: the street lies north or south of it (its facing, MadeLot's,
        /// 0 or 180 degrees), as every road of the district's grid lies along one of the world's axes.
        /// </summary>
        public static bool FrontsAvenue(float facing)
        {
            float f = ((facing % 360f) + 360f) % 360f;
            return f < 45f || f >= 315f || (f >= 135f && f < 225f);
        }

        /// <summary>True when a size is one the lot's class takes (the plan's sizes for it).</summary>
        public static bool TakesSize(BuildingsLot lot, string size)
        {
            if (lot.Sizes == null || string.IsNullOrEmpty(size))
            {
                return false;
            }
            for (int i = 0; i < lot.Sizes.Count; i++)
            {
                if (string.Equals(lot.Sizes[i], size, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Why a building finished by the construction game cannot be raised on a lot (English, for the log and the caller's
        /// why), or null. <paramref name="known"/>: the lot is in the plan; <paramref name="taken"/>: a building stands there or
        /// is finished and waits; <paramref name="structure"/>: the game has that size, version and type; <paramref name="lives"/>:
        /// Uptown is a district of the game this load; <paramref name="inSession"/>: a Going Public session is on.
        /// </summary>
        public static string FinishRefusal(BuildingsLot lot, bool known, bool taken, string size, int version, string type, int storeys, FinishOwner owner,
                                           double price, bool structure, bool lives, bool inSession)
        {
            if (!lives)
            {
                return "Uptown is not a district of the game at this load";
            }
            if (!known)
            {
                return "no lot of Uptown's plan has that id";
            }
            if (taken)
            {
                return "a building stands on the lot, or one finished there waits for the next load";
            }
            if (string.IsNullOrEmpty(type) || type == SpecialType)
            {
                return "the game's special building type needs a special service of the game's";
            }
            if (!TakesSize(lot, size))
            {
                return "the size " + (size ?? "(none)") + " is not one the lot's class takes";
            }
            if (!structure)
            {
                return "the game has no structure of size " + size + " version " + version + " for the type " + type;
            }
            if (storeys < 0 || storeys > StoreysMost)
            {
                return "its storeys " + storeys + " are not between 0 (the design's own) and " + StoreysMost;
            }
            if (owner == FinishOwner.Player && inSession)
            {
                return "a deed made on one machine only: in a Going Public session a finished building goes to the city";
            }
            if (owner == FinishOwner.Player && (price < 0 || double.IsNaN(price) || double.IsInfinity(price)))
            {
                return "its price is not a sum of money";
            }
            return null;
        }

        public const float NewDeedOccupancy = 0f;   // % of a new deed's floor let at its first load: a building just finished has nobody in it yet
    }
}
