using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>The size classes of Manhattan's buildings a lot is planned for (the game's sizes in each, places.md's count).</summary>
    public enum LotClass
    {
        Small = 0,       // shops and homes: the game's sizes A, B, C and F
        Office = 1,      // D, J and K
        Big = 2,         // big stores and big homes: M, L and N
        Warehouse = 3    // H, I, P and Q
    }

    /// <summary>
    /// One lot of Uptown's plan: open ground with one side on a street, where a building of the mod's own may stand or a
    /// plot be built. Everything here is fixed data, the same on every machine and in every version: its id, its street
    /// and number are kept in saves (by the builders, by the district save), so they never change once shipped.
    /// </summary>
    public sealed class Lot
    {
        public readonly string Id;          // "uptown_<row>_<column, two digits>_<n, two digits>": letters, digits and underscores only
        public readonly int Row;            // 1, the first block's row north of 1st Avenue, then 2 and 3
        public readonly int Column;         // the block's column west to east, 1 to 18 (9th Street's block east of it is 6)
        public readonly int Block;          // Row * 100 + Column
        public readonly bool First;         // in the first block, where the band meets Manhattan
        public readonly string Street;      // the street of the mod's own it fronts ("reallife:street_uptown_...")
        public readonly int Number;         // its house number on that street, odd on one side and even on the other
        public readonly int Order;          // its place along its side of the street, from the street's first number
        public readonly float X;            // m: its middle on the ground
        public readonly float Z;
        public readonly int Yaw;            // whole degrees, as CivicWorks.StartPlot and PlotRules take a plot: the land's front, at -Z, turned by it
        public readonly float Facing;       // degrees, as NewBuildings' MadeLot has it: 0 = +Z, the way the street lies from the lot (Yaw + 180)
        public readonly int Width;          // whole metres along the street
        public readonly int Depth;          // whole metres back from it
        public readonly LotClass Class;

        public Lot(string id, int row, int column, string street, int number, int order, float x, float z, int yaw, int width, int depth, LotClass kind, bool first)
        {
            Id = id;
            Row = row;
            Column = column;
            Block = row * 100 + column;
            First = first;
            Street = street;
            Number = number;
            Order = order;
            X = x;
            Z = z;
            Yaw = yaw;
            Facing = (yaw + 180) % 360;
            Width = width;
            Depth = depth;
            Class = kind;
        }

        /// <summary>The game's building sizes the lot's class holds ("ba:buildingsize_a" and so on); a design of one must also fit the lot.</summary>
        public IReadOnlyList<string> Sizes => UptownLots.SizesOf(Class);
    }

    /// <summary>A rectangle of the district's own ground, in world metres, corners in order (as the district save takes it).</summary>
    public struct GroundRect
    {
        public float X1;
        public float Z1;
        public float X2;
        public float Z2;

        public GroundRect(float x1, float z1, float x2, float z2)
        {
            X1 = Math.Min(x1, x2);
            Z1 = Math.Min(z1, z2);
            X2 = Math.Max(x1, x2);
            Z2 = Math.Max(z1, z2);
        }

        public bool Holds(float x, float z)
        {
            return x >= X1 && x <= X2 && z >= Z1 && z <= Z2;
        }
    }

    /// <summary>The district's way out: a spot on a game street outside it, its heading and the game's district it is in.</summary>
    public struct UptownExit
    {
        public float X;
        public float Y;
        public float Z;
        public float Heading;     // degrees, 0 = +Z
        public string District;   // the game's district id ("ba:neighborhood_midtown")

        public UptownExit(float x, float y, float z, float heading, string district)
        {
            X = x;
            Y = y;
            Z = z;
            Heading = heading;
            District = district;
        }
    }

    /// <summary>
    /// The lots of Uptown, north of Manhattan, for the features that build on them (docs/features/uptown.md, "The lots"):
    /// the district's buildings stand on them, the builders sell and build the empty ones. The whole band is laid at every
    /// load, so every lot opens with it. The plan is game-free and fixed
    /// (<see cref="All"/>, <see cref="FirstBlock"/>, <see cref="Get"/>, <see cref="Ground"/>, <see cref="Exit"/>), readable
    /// whenever the mod is loaded; what stands at this load is set by the ground's load step in the city's load, before the
    /// city's buildings are made (<see cref="Ready"/>, <see cref="Why"/>, <see cref="Open"/>, <see cref="Stamp"/>), and
    /// <see cref="Closing"/> is raised first when the ground's feature is cleaned up. COST: reads of fixed lists and fields;
    /// <see cref="Get"/> is a dictionary look-up.
    /// </summary>
    public static class UptownLots
    {
        public const float GroundY = 0f;   // m: the height the district's ground is laid at, the city's own ground's

        private static readonly string[] SmallSizes = { "ba:buildingsize_a", "ba:buildingsize_b", "ba:buildingsize_c", "ba:buildingsize_f" };
        private static readonly string[] OfficeSizes = { "ba:buildingsize_d", "ba:buildingsize_j", "ba:buildingsize_k" };
        private static readonly string[] BigSizes = { "ba:buildingsize_m", "ba:buildingsize_l", "ba:buildingsize_n" };
        private static readonly string[] WarehouseSizes = { "ba:buildingsize_h", "ba:buildingsize_i", "ba:buildingsize_p", "ba:buildingsize_q" };

        private static string _why = "the city has not loaded";

        /// <summary>Every lot of the band, in id order (row, column, then each block's own order).</summary>
        public static IReadOnlyList<Lot> All => LayoutRules.Lots;

        /// <summary>The first block's lots, between 1st Avenue's buildings and Uptown Avenue, where the band meets Manhattan.</summary>
        public static IReadOnlyList<Lot> FirstBlock => LayoutRules.FirstLots;

        /// <summary>The lot of that id, or null for an id the plan does not have.</summary>
        public static Lot Get(string id)
        {
            return id != null && LayoutRules.ById.TryGetValue(id, out Lot lot) ? lot : null;
        }

        /// <summary>The district's own ground as it is laid at every load (the whole band's), for the district save's Ground entries.</summary>
        public static IReadOnlyList<GroundRect> Ground => LayoutRules.BandGround;

        /// <summary>The way out for the player and the cars written while on the district's ground: a parking spot on 1st Avenue.</summary>
        public static UptownExit Exit => LayoutRules.Exit;

        /// <summary>True while the district's ground stands at this load: switched on, New York, the session agrees, nothing of the game's there.</summary>
        public static bool Ready { get; private set; }

        /// <summary>Null while <see cref="Ready"/>; else why not, one line in English for the log.</summary>
        public static string Why => Ready ? null : _why;

        /// <summary>True from the feature's clean-up until the next load: the district has closed, its ground stays until the city goes.</summary>
        public static bool Closed { get; private set; }

        /// <summary>Changes whenever lots open or close: a look once a second is one comparison.</summary>
        public static int Stamp { get; private set; }

        /// <summary>Raised with the reason first thing in the ground's clean-up, before anything of it is touched. A handler that throws is logged; the rest still run.</summary>
        public static event Action<string> Closing;

        /// <summary>True when the lot's ground is laid and walkable at this load: every lot of the band while it stands.</summary>
        public static bool Open(Lot lot)
        {
            return Ready && lot != null;
        }

        /// <summary>The game's building sizes a class holds.</summary>
        public static IReadOnlyList<string> SizesOf(LotClass kind)
        {
            switch (kind)
            {
                case LotClass.Office:
                    return OfficeSizes;
                case LotClass.Big:
                    return BigSizes;
                case LotClass.Warehouse:
                    return WarehouseSizes;
                default:
                    return SmallSizes;
            }
        }

        // ---- set by the ground's load step and its clean-up (the same assembly), never by a feature that reads the lots

        /// <summary>A city load begins: nothing stands until the ground's step says so.</summary>
        internal static void Begin(string why)
        {
            Closed = false;
            SetReady(false, why);
        }

        /// <summary>The ground's step is done: the district stands, or why not.</summary>
        internal static void Laid(bool ready, string why)
        {
            SetReady(ready, why);
        }

        /// <summary>
        /// The ground's feature is cleaned up: <see cref="Closing"/> first, then every lot closed until the next load.
        /// Each handler runs in its own try, its fault handed to <paramref name="fault"/>.
        /// </summary>
        internal static void Close(string why, Action<Exception> fault)
        {
            if (Closed)
            {
                return;
            }
            Action<string> closing = Closing;
            if (closing != null)
            {
                Delegate[] handlers = closing.GetInvocationList();
                for (int i = 0; i < handlers.Length; i++)
                {
                    try
                    {
                        ((Action<string>)handlers[i])(why);
                    }
                    catch (Exception e)
                    {
                        fault?.Invoke(e);
                    }
                }
            }
            Closed = true;
            SetReady(false, "the district closed: " + why);
        }

        private static void SetReady(bool ready, string why)
        {
            if (ready != Ready)
            {
                Stamp++;
            }
            Ready = ready;
            _why = ready ? null : why ?? "it stood down";
        }
    }
}
