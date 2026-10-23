using System;
using System.Collections.Generic;

// STAND-IN, to be taken out at the merge: Uptown's ground (feature/uptown-ground, Scripts/Uptown/Lots.cs) gives these names,
// with these members as its branch has them; this file holds the same names so the district's side builds and its checks
// run on its own branch. With it the ground is never laid (Ready is false), so no building of the district stands in a game
// built from this branch.
namespace RealLifeOverhaul.Uptown
{
    /// <summary>The size classes of Manhattan's buildings a lot is planned for (stand-in of the ground's own).</summary>
    public enum LotClass
    {
        Small = 0,       // shops and homes: the game's sizes A, B, C and F
        Office = 1,      // D, J and K
        Big = 2,         // big stores and big homes: M, L and N
        Warehouse = 3    // H, I, P and Q
    }

    /// <summary>A lot of the district's plan (stand-in of the ground's own).</summary>
    public sealed class Lot
    {
        public readonly string Id;
        public readonly int Row;
        public readonly int Column;
        public readonly int Block;
        public readonly bool First;
        public readonly string Street;
        public readonly int Number;
        public readonly int Order;
        public readonly float X;
        public readonly float Z;
        public readonly int Yaw;
        public readonly float Facing;
        public readonly int Width;
        public readonly int Depth;
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

        public IReadOnlyList<string> Sizes => UptownLots.SizesOf(Class);
    }

    /// <summary>A rectangle of the district's own ground (stand-in).</summary>
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

    /// <summary>The way out to Manhattan's streets (stand-in).</summary>
    public struct UptownExit
    {
        public float X;
        public float Y;
        public float Z;
        public float Heading;
        public string District;

        public UptownExit(float x, float y, float z, float heading, string district)
        {
            X = x;
            Y = y;
            Z = z;
            Heading = heading;
            District = district;
        }
    }

    /// <summary>The ground's lots and its state at this load (stand-in: no lot, the ground never laid).</summary>
    public static class UptownLots
    {
        public const float GroundY = 0f;

        private static readonly Lot[] None = new Lot[0];
        private static readonly GroundRect[] NoGround = new GroundRect[0];
        private static readonly string[] SmallSizes = { "ba:buildingsize_a", "ba:buildingsize_b", "ba:buildingsize_c", "ba:buildingsize_f" };
        private static readonly string[] OfficeSizes = { "ba:buildingsize_d", "ba:buildingsize_j", "ba:buildingsize_k" };
        private static readonly string[] BigSizes = { "ba:buildingsize_m", "ba:buildingsize_l", "ba:buildingsize_n" };
        private static readonly string[] WarehouseSizes = { "ba:buildingsize_h", "ba:buildingsize_i", "ba:buildingsize_p", "ba:buildingsize_q" };

        public static IReadOnlyList<Lot> All => None;
        public static IReadOnlyList<Lot> FirstBlock => None;
        public static IReadOnlyList<GroundRect> Ground => NoGround;
        public static UptownExit Exit => new UptownExit(91.18f, 0f, 473.33f, 270f, "ba:neighborhood_midtown");

        public static bool Ready => false;
        public static string Why => "the district's ground is not in this build";
        public static bool Closed => false;
        public static int Stamp => 0;

#pragma warning disable 67   // the ground's own raises it; the stand-in never does
        public static event Action<string> Closing;
#pragma warning restore 67

        public static Lot Get(string id)
        {
            return null;
        }

        public static bool Open(Lot lot)
        {
            return false;
        }

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
    }
}
