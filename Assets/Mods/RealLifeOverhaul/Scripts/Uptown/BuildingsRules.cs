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
        Warehouse,
        Theatre,
        Cinema
    }

    /// <summary>A lot of the district as the buildings' rules read it, made from the ground's plan by the game side. Game-free.</summary>
    public struct BuildingsLot
    {
        public string Id;              // the plan's stable id, the one builders and the store know it by
        public string Street;          // a street of the mod's own (reallife:street_...)
        public int Number;             // its house number on that street, never changed between versions: saves keep records by it
        public int Block;              // Row * 100 + Column, the plan's: the lots round one block between its streets
        public bool First;             // in the district's first block
        public bool Avenue;            // on one of its avenues (they run west to east, its main roads), not one of its streets
        public float X;                // m: the lot's middle
        public float Z;
        public float Facing;           // degrees, MadeLot's: the way the street lies from the lot
        public int Width;              // m along the street
        public int Depth;              // m back from it
        public BuildingsClass Class;
        public IReadOnlyList<string> Sizes;   // the game's sizes it takes: the plan's size on its spaces, or its class's for an open run's space
        public int Version;            // the plan's version of that size, or 0
        public int Spaces;             // the spaces it stands on (SpacesRules)
        public int Strip;              // its strip of spaces and its first space along it (Lot.Strip, Lot.Index)
        public int Index;
        public bool Open;              // a space of an open run: the construction game's land, never built from the first day
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

    /// <summary>What the registration on a leased office's own lot is to its lease (<see cref="BuildingsRules.LeaseOf"/>).</summary>
    public enum LeaseVerdict
    {
        Refused,   // someone else's, or no office: the why says whose
        Theirs,    // the lease's business, its name and description the lease's
        Renamed,   // the lease's business, its name or description to be put back to the lease's (a text changed since it moved in)
        MoveIn     // new at this load, or empty to let and nobody's: the lease's business moves in
    }

    /// <summary>
    /// The rules of the district's buildings, game-free (EconomyCheck's UptownLifeChecks): which of the band's lots stand
    /// from the first day and which stay open for the construction game, the office lots leased to other features'
    /// businesses, which of the game's structures a lot's building is made with, its traffic index, and what refuses a
    /// building finished by the construction game. Every answer comes from the arguments alone, picks by FNV-1a hashes of
    /// the lot's id: the same on every machine with the same mod, in any order the lots come.
    /// COST: nothing per frame; a few sorts of the band's lots and a pass over each lot at a load.
    /// </summary>
    public static class BuildingsRules
    {
        public const float DayOneShare = 0.25f;     // of the first block's lots built before the band was filled: those addresses stay built, as saves hold their records
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

        // Lots something of the game's own stands into, from the ground's scan of the game's scene (navlab/facts/band_clear.py):
        // a building there would meet it, as the copy engine stands a design on the lot's front line. uptown_1_14_0004: the crown
        // of a tree on 1st Street's east pavement leans some 2 m over the lot's front from 3.5 m up.
        private static readonly string[] NotClear = { "uptown_1_14_0004" };

        /// <summary>
        /// False for a lot something of the game's stands into, or one of the district's big special places stands on, spans
        /// or keeps clear (BigPlacesRules.Holds): never built from the first day as one of the plan's ordinary buildings,
        /// leased, counted among a block's open lots, sold or developed (it is taken for the construction game and the
        /// landmarks), so it stays empty or the place's.
        /// </summary>
        public static bool Clear(string lotId)
        {
            return !string.IsNullOrEmpty(lotId) && Array.IndexOf(NotClear, lotId) < 0 && !BigPlacesRules.Holds(lotId);
        }

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
            return DayOne(lots, awayX, awayZ, into, null);
        }

        /// <summary>
        /// As above, its quarter never on a block <paramref name="keptOpen"/> keeps open (a landmark's), so a landmark's site is
        /// its block whole: the quarter's count is the first block's, its lots taken from the others.
        /// </summary>
        public static List<int> DayOne(IList<BuildingsLot> lots, float awayX, float awayZ, List<int> into, Func<int, bool> keptOpen)
        {
            into.Clear();
            if (lots == null)
            {
                return into;
            }
            var first = new List<int>(lots.Count);
            int firstLots = 0;
            for (int i = 0; i < lots.Count; i++)
            {
                bool own = lots[i].First && !lots[i].Open && !string.IsNullOrEmpty(lots[i].Id) && Clear(lots[i].Id);   // never a big place's ground, an open run's or a lot something stands into
                firstLots += own ? 1 : 0;
                if (own && (keptOpen == null || !keptOpen(lots[i].Block)))
                {
                    first.Add(i);
                }
            }
            first.Sort((a, b) =>
            {
                int byReach = Reach(lots[a], awayX, awayZ).CompareTo(Reach(lots[b], awayX, awayZ));
                return byReach != 0 ? byReach : string.CompareOrdinal(lots[a].Id, lots[b].Id);
            });
            int wanted = Math.Min(first.Count, DayOneCount(firstLots));
            for (int i = 0; i < wanted; i++)
            {
                into.Add(first[i]);
            }
            into.Sort((a, b) => string.CompareOrdinal(lots[a].Id, lots[b].Id));
            return into;
        }

        // ---- the band built from the first day, the open runs and the landmarks' blocks open for the construction game (the
        // owner, 23 Oct 2026: "Mostly built"; 25 Oct: "keep 30" of the open lots, as the open runs' spaces now are)

        public const int LeasedOffices = 8;               // office lots built for the businesses other features lease there: the construction game's eight rival builders
        public const string OfficeType = "ba:buildingtype_office";   // the game's building type a leased office is built with
        public const string EmptyBusinessType = "ba:businesstype_empty";   // the game's business type of a unit with no business in it

        /// <summary>
        /// The office lots built for the businesses other features lease in the district, as indices into
        /// <paramref name="lots"/> (into the caller's list, cleared), in the order of the leases (the first is the first
        /// lessee's): of the office lots outside the first block, whose blocks the landmarks take, one a block at most, in
        /// the order of their ids' hashes, the first <see cref="LeasedOffices"/>. Fixed data: never the save's or the options'.
        /// </summary>
        public static List<int> Leased(IList<BuildingsLot> lots, List<int> into)
        {
            into.Clear();
            if (lots == null)
            {
                return into;
            }
            var offices = new List<int>(64);
            for (int i = 0; i < lots.Count; i++)
            {
                if (lots[i].Class == BuildingsClass.Office && !lots[i].First && !lots[i].Open && Clear(lots[i].Id))
                {
                    offices.Add(i);
                }
            }
            offices.Sort((a, b) =>
            {
                int byHash = NewBuildingsRules.Hash(lots[a].Id + "|lease").CompareTo(NewBuildingsRules.Hash(lots[b].Id + "|lease"));
                return byHash != 0 ? byHash : string.CompareOrdinal(lots[a].Id, lots[b].Id);
            });
            for (int k = 0; k < offices.Count && into.Count < LeasedOffices; k++)
            {
                bool blockHasOne = false;
                for (int j = 0; j < into.Count; j++)
                {
                    blockHasOne |= lots[into[j]].Block == lots[offices[k]].Block;
                }
                if (!blockHasOne)
                {
                    into.Add(offices[k]);
                }
            }
            return into;
        }

        /// <summary>
        /// The lots built from the first day, as indices into <paramref name="lots"/> (into the caller's list, cleared), in
        /// the order of their ids: every lot but those left open. Left open: the open runs' spaces (the plan's), and every
        /// lot of a block kept open (<paramref name="keptOpen"/>: a landmark's block) but the first block's quarter built
        /// before the band was filled (<see cref="DayOne"/>, whose records saves hold) and the leased offices
        /// (<see cref="Leased"/>). A lot not <see cref="Clear"/> is never built. The same on every machine with the same mod.
        /// </summary>
        public static List<int> Built(IList<BuildingsLot> lots, Func<int, bool> keptOpen, float awayX, float awayZ, List<int> into)
        {
            into.Clear();
            if (lots == null || lots.Count == 0)
            {
                return into;
            }
            var stays = new bool[lots.Count];   // built whatever the share: the first quarter and the leases
            List<int> fixedOnes = DayOne(lots, awayX, awayZ, new List<int>(32), keptOpen);
            for (int i = 0; i < fixedOnes.Count; i++)
            {
                stays[fixedOnes[i]] = true;
            }
            Leased(lots, fixedOnes);
            for (int i = 0; i < fixedOnes.Count; i++)
            {
                stays[fixedOnes[i]] = true;
            }
            for (int i = 0; i < lots.Count; i++)
            {
                stays[i] |= Venue(lots[i].Class);   // a theatre's or a cinema's lot is built from the first day, for the game to let
            }
            for (int i = 0; i < lots.Count; i++)
            {
                if (string.IsNullOrEmpty(lots[i].Id) || lots[i].Open || !Clear(lots[i].Id))
                {
                    continue;
                }
                if (stays[i] || keptOpen == null || !keptOpen(lots[i].Block))
                {
                    into.Add(i);
                }
            }
            into.Sort((a, b) => string.CompareOrdinal(lots[a].Id, lots[b].Id));
            return into;
        }

        /// <summary>True for a theatre's or a cinema's lot (the owner, 25 Oct 2026: "cinemas etc to be rented").</summary>
        public static bool Venue(BuildingsClass kind)
        {
            return kind == BuildingsClass.Theatre || kind == BuildingsClass.Cinema;
        }

        /// <summary>
        /// The look a lot's building is declared with by name, or null for one of its size's: a cinema wears the game's theatre
        /// front (SM_BT_001), as the game's one cinema look the copy engine could copy has its door 4.1 m back under its marquee,
        /// out of the pedestrians' reach on the district's ground, and its others are drawn in static batches.
        /// </summary>
        public static string LookOf(BuildingsClass kind)
        {
            return kind == BuildingsClass.Cinema ? CinemaLook : null;
        }

        public const string CinemaLook = "SM_BT_001";   // the game's theatre on Broadway (R1, 32.7 by 25.5 m): a cinema's front where the game's own cannot stand

        /// <summary>
        /// What the registration on a leased office's own lot is to its lease (LifeGame.Lease). Refused, with why, where it is
        /// another player's in a Going Public session, the player's (rented or owned), on a building not built as an office, or
        /// another business's. Of the lease's business type it is the lease's own whatever its name and description say: the
        /// lot is the lease's (fixed data), the game starts no business of that type itself, and a name is text in the player's
        /// language, never who runs it; Renamed when either differs from the lease's. Else new at this load (<paramref name="fresh"/>)
        /// or empty to let and nobody's: MoveIn.
        /// </summary>
        public static LeaseVerdict LeaseOf(bool foreign, bool players, string buildingType, string type, string name, string description, bool fresh, bool toLet,
                                           string leaseType, string leaseName, string leaseDescription, out string why)
        {
            why = null;
            if (foreign)
            {
                why = "it is another player's in this session";
                return LeaseVerdict.Refused;
            }
            if (players)
            {
                why = "the player rents or owns it";
                return LeaseVerdict.Refused;
            }
            if (buildingType != OfficeType)
            {
                why = "it was built as " + buildingType + ", not an office";
                return LeaseVerdict.Refused;
            }
            if (!string.IsNullOrEmpty(leaseType) && type == leaseType)
            {
                return name == leaseName && description == leaseDescription ? LeaseVerdict.Theirs : LeaseVerdict.Renamed;
            }
            if (fresh || (toLet && (string.IsNullOrEmpty(type) || type == EmptyBusinessType)))
            {
                return LeaseVerdict.MoveIn;
            }
            why = "a business of " + type + " is in it";
            return LeaseVerdict.Refused;
        }

        /// <summary>True when a registration is a lease's business standing: of the lease's type, not to let and not the player's to rent; its name is never asked.</summary>
        public static bool LeasedBy(string type, bool toLet, bool rentedByPlayer, string leaseType)
        {
            return !string.IsNullOrEmpty(leaseType) && type == leaseType && !toLet && !rentedByPlayer;
        }

        /// <summary>
        /// True when a lease's business may receive the player as a visitor (the construction game's builders at their front
        /// desks): its lessee's business standing there, and never a unit the player rents or owns, nor another player's.
        /// </summary>
        public static bool Visitable(bool leased, bool rentedByPlayer, bool ownedByPlayer, bool anotherPlayers)
        {
            return leased && !rentedByPlayer && !ownedByPlayer && !anotherPlayers;
        }

        /// <summary>The structure a leased office is built with: of the options, those of the game's office type, the one its id's hash picks; -1 when the game has none.</summary>
        public static int PickOffice(string lotId, IList<BuildingsStructure> options)
        {
            int offices = 0;
            for (int i = 0; options != null && i < options.Count; i++)
            {
                offices += options[i].Type == OfficeType ? 1 : 0;
            }
            if (offices == 0 || string.IsNullOrEmpty(lotId))
            {
                return -1;
            }
            int wanted = (int)(NewBuildingsRules.Hash(lotId + "|structure") % (uint)offices);
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Type == OfficeType && wanted-- == 0)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// The structures a lot's building may take instead of <paramref name="options"/>[<paramref name="pick"/>], in the order
        /// the made buildings' engine tries them when no design of its size fits the lot: one of each other size of the options
        /// (a design is picked by size alone, so a second of a size would fail as the first did), the sizes in the options'
        /// order from the pick on and round again, each size's first of the pick's type where it takes that type (the
        /// district keeps its mix of homes, shops, offices and warehouses), else its first; of the office type alone for a
        /// leased office. Into the caller's list (cleared); empty when the pick is not one of the options.
        /// </summary>
        public static List<BuildingsStructure> Others(IList<BuildingsStructure> options, int pick, bool officesOnly, List<BuildingsStructure> into)
        {
            into.Clear();
            if (options == null || pick < 0 || pick >= options.Count)
            {
                return into;
            }
            BuildingsStructure own = options[pick];
            int count = options.Count;
            for (int k = 1; k < count; k++)
            {
                BuildingsStructure each = options[(pick + k) % count];
                if (each.Size == own.Size || (officesOnly && each.Type != OfficeType) || HasSize(into, each.Size))
                {
                    continue;
                }
                BuildingsStructure taken = each;
                for (int j = k; j < count; j++)
                {
                    BuildingsStructure same = options[(pick + j) % count];
                    if (same.Size == each.Size && same.Type == own.Type && (!officesOnly || same.Type == OfficeType))
                    {
                        taken = same;
                        break;
                    }
                }
                into.Add(taken);
            }
            return into;
        }

        private static bool HasSize(List<BuildingsStructure> list, string size)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Size == size)
                {
                    return true;
                }
            }
            return false;
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
            return FinishRefusal(lot, known, taken, size, version, type, storeys, owner, price, structure, lives, inSession, false);
        }

        /// <summary>
        /// As above; <paramref name="spanned"/>: the building stands over other lots too (<see cref="SpanRefusal"/> has passed
        /// them), so its size need not be one its own lot's class takes: the made buildings' engine stands its design only on
        /// ground it fits, at its load.
        /// </summary>
        public static string FinishRefusal(BuildingsLot lot, bool known, bool taken, string size, int version, string type, int storeys, FinishOwner owner,
                                           double price, bool structure, bool lives, bool inSession, bool spanned)
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
            // An open run's space takes any size of the grid that stands on one space; a site of several spans its others.
            if (!spanned && !(lot.Open ? SpacesRules.SpacesOf(size, version) == 1 : TakesSize(lot, size)))
            {
                return "the size " + (size ?? "(none)") + " is not one the lot takes";
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

        // ---- a building over several lots (a landmark of Scripts/Landmarks), and a lot's traffic lifted by what is near it

        public const int SpanMost = 16;             // lots one building may stand over, its own among them: more than any block of the band holds
        public const int LiftedMost = 80;           // the most a lot's traffic index is lifted to: Midtown's busiest quarter, under the game's busiest (84)

        /// <summary>
        /// The ground a building over several lots stands on: the rectangle round all their footprints, its middle, and its
        /// width along the street and depth back from it as the first lot (the building's own, whose address it takes) faces.
        /// False for no lots. Every lot of the district's grid lies along the world's axes, so the rectangle does too.
        /// </summary>
        public static bool SpanGround(IList<BuildingsLot> lots, out float x, out float z, out float width, out float depth)
        {
            x = z = width = depth = 0f;
            if (lots == null || lots.Count == 0)
            {
                return false;
            }
            float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
            for (int i = 0; i < lots.Count; i++)
            {
                BuildingsLot l = lots[i];
                bool avenue = FrontsAvenue(l.Facing);
                float halfX = (avenue ? l.Width : l.Depth) * 0.5f, halfZ = (avenue ? l.Depth : l.Width) * 0.5f;
                x0 = Math.Min(x0, l.X - halfX);
                x1 = Math.Max(x1, l.X + halfX);
                z0 = Math.Min(z0, l.Z - halfZ);
                z1 = Math.Max(z1, l.Z + halfZ);
            }
            x = (x0 + x1) * 0.5f;
            z = (z0 + z1) * 0.5f;
            bool along = FrontsAvenue(lots[0].Facing);
            width = along ? x1 - x0 : z1 - z0;
            depth = along ? z1 - z0 : x1 - x0;
            return true;
        }

        /// <summary>
        /// Why a building cannot stand over other lots as well as its own (English, for the log), or null. Of the
        /// <paramref name="count"/> lots named: <paramref name="known"/> are in the plan, <paramref name="free"/> hold no
        /// building and wait for none, <paramref name="sameBlock"/> are in the building's own block; <paramref name="distinct"/>
        /// when none is named twice or is the building's own lot.
        /// </summary>
        public static string SpanRefusal(int count, int known, int free, int sameBlock, bool distinct)
        {
            if (count <= 0)
            {
                return null;
            }
            if (count + 1 > SpanMost)
            {
                return "a building stands over " + SpanMost + " lots at most";
            }
            if (!distinct)
            {
                return "a lot it stands over is named twice, or is its own";
            }
            if (known < count)
            {
                return "a lot it stands over is not in Uptown's plan";
            }
            if (sameBlock < count)
            {
                return "a lot it stands over is in another block: a street runs between";
            }
            if (free < count)
            {
                return "a building stands on a lot it stands over, or one finished there waits for the next load";
            }
            return null;
        }

        /// <summary>A lot's traffic index lifted by what stands near it (a landmark's crowds): never under the game's least, never over <see cref="LiftedMost"/>.</summary>
        public static int Lifted(int traffic, int lift)
        {
            return Math.Max(TrafficLeast, Math.Min(LiftedMost, traffic + Math.Max(0, lift)));
        }
    }
}
