using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>What was found round a showroom's special employee desks, and whether a person seated at one can walk away (<see cref="FloorMeasure.Clear"/>).</summary>
    public sealed class ClearVerdict
    {
        public int Desks;        // special employee desks the layout has (the dealer's desks: a car mod turns the layout's two plain desks into them)
        public int Loose;        // seats still within the desk's radius that are no part of a desk (not the dealer's own, not the one the player is seated at)
        public int Unseated;     // desks with no seat for the player attached (the game seats the player there; nothing here can add one)
        public int Sides;        // the open sides of the seats the player is seated at, of the two each has
        public int Behind;       // of them, seats with a metre and a fifth of open floor behind
        public int Removed;      // loose seats taken out of the copy of the layout (counted by the caller, which takes them out)
        public string First;     // the first thing that is wrong, for the log; null for none

        public bool Pass => Loose == 0 && Sides == Desks * 2 - Unseated * 2 && Behind == Desks - Unseated;
    }

    public sealed partial class FloorMeasure
    {
        public const string DeskName = "specialemployeedesk";   // the game's special employee's desk (the dealer's), as the layout names it without the item start
        public const float DeskRadius = 2.5f;     // m: the seats this near a dealer's desk are taken out; none is the player's to sit at but the desk's own
        public const int CustomerSlot = 3;        // the desk's attachment point for the player's seat (its employee's is 2); the game's BusinessEmployeeController reads the second child of it
        public const float SideStep = 0.5f;       // m: where a person stands up from a seat to its side (the seat's own two side NavMeshTargets)
        public const float BehindClear = 1.2f;    // m: the open floor kept behind the seat the player is seated at, from its middle

        /// <summary>True for the dealer's desk.</summary>
        public static bool IsDesk(string name)
        {
            return string.Equals(name, DeskName, StringComparison.Ordinal);
        }

        /// <summary>
        /// True for a loose seat a desk's surroundings are cleared of: every item of the game's that is a seat (SeatController) the layout
        /// stands on the floor, the staff's chair included. The ones set on the desk itself are not loose.
        /// </summary>
        public static bool IsLoose(string name)
        {
            return IsSeat(name) || string.Equals(name, "officechair2", StringComparison.Ordinal);
        }

        /// <summary>
        /// The pieces that are loose seats within <see cref="DeskRadius"/> of a dealer's desk, by index, in the layout's order: not set on any
        /// piece (<see cref="FloorPiece.Parent"/>, -1), so neither the desk's own seat for its employee nor the player's seat at it.
        /// </summary>
        public static List<int> LooseAt(IList<FloorPiece> pieces)
        {
            var found = new List<int>(8);
            for (int i = 0; pieces != null && i < pieces.Count; i++)
            {
                if (!IsLoose(pieces[i].Name) || pieces[i].Parent != -1)
                {
                    continue;
                }
                for (int d = 0; d < pieces.Count; d++)
                {
                    if (!IsDesk(pieces[d].Name))
                    {
                        continue;
                    }
                    float dx = pieces[i].X - pieces[d].X, dz = pieces[i].Z - pieces[d].Z;
                    if (dx * dx + dz * dz <= DeskRadius * DeskRadius)
                    {
                        found.Add(i);
                        break;
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// Whether a person seated at each dealer's desk can walk away: no loose seat is left within the desk's radius, and the seat the
        /// player is seated at (the one set on the desk's seat point) has both its sides open, where the game stands a person up from it,
        /// and a metre and a fifth of open floor behind it, joined to a door by a way a person can walk. The pieces are measured as they stand.
        /// </summary>
        public ClearVerdict Clear(IList<FloorPiece> pieces)
        {
            var verdict = new ClearVerdict();
            if (pieces == null)
            {
                return verdict;
            }
            verdict.Loose = LooseAt(pieces).Count;
            if (verdict.Loose > 0)
            {
                verdict.First = verdict.Loose + " loose seat(s) stand within " + DeskRadius.ToString("F1", CultureInfo.InvariantCulture) + " m of a desk";
            }
            bool laid = false;
            for (int d = 0; d < pieces.Count; d++)
            {
                if (!IsDesk(pieces[d].Name))
                {
                    continue;
                }
                verdict.Desks++;
                int chair = -1;
                for (int k = 0; k < pieces.Count && chair < 0; k++)
                {
                    chair = pieces[k].Parent == d && pieces[k].Slot == CustomerSlot ? k : -1;
                }
                if (chair < 0)
                {
                    verdict.Unseated++;
                    verdict.First = verdict.First ?? "the desk at " + Place(pieces[d]) + " has no seat set for the player";
                    continue;
                }
                if (!laid)
                {
                    laid = Lay(pieces);
                }
                FloorPiece seat = pieces[chair];
                float yaw = seat.Yaw * (float)Math.PI / 180f, c = (float)Math.Cos(yaw), s = (float)Math.Sin(yaw);
                int open = 0;
                for (int side = -1; side <= 1; side += 2)
                {
                    open += laid && ReachesFrom(seat.X + side * SideStep * c, seat.Z - side * SideStep * s) ? 1 : 0;   // a seat's local x is (c, -s) in the room
                }
                verdict.Sides += open;
                float bx = seat.X - BehindClear * (float)Math.Sin(yaw), bz = seat.Z - BehindClear * (float)Math.Cos(yaw);
                int at = laid ? CellOf(bx, bz) : -1;
                bool behind = at >= 0 && _cover[at] == 0 && _main[at];
                verdict.Behind += behind ? 1 : 0;
                if (open < 2 || !behind)
                {
                    verdict.First = verdict.First ?? "the seat at " + Place(seat) + " has " + open + " of its 2 sides open" + (behind ? string.Empty : " and no clear floor behind it");
                }
            }
            return verdict;
        }

        private static string Place(FloorPiece p)
        {
            return p.X.ToString("F2", CultureInfo.InvariantCulture) + ", " + p.Z.ToString("F2", CultureInfo.InvariantCulture);
        }

        /// <summary>The words of a desk verdict for Player.log and the census, English: what was found at the desks, without what was done.</summary>
        public static string Words(ClearVerdict v)
        {
            if (v == null)
            {
                return "not measured";
            }
            if (v.Desks == 0)
            {
                return "the layout has no dealer's desk (no car mod has made one), so there is nothing to keep clear";
            }
            string range = DeskRadius.ToString("F1", CultureInfo.InvariantCulture), behind = BehindClear.ToString("F1", CultureInfo.InvariantCulture);
            int seated = v.Desks - v.Unseated;
            return (v.Desks == 1 ? "1 dealer's desk" : v.Desks + " dealer's desks") + ", "
                   + (v.Loose > 0 ? v.Loose + " loose seat(s) still within " + range + " m" : "no loose seat within " + range + " m")
                   + ", " + v.Sides + " of " + seated * 2 + " sides of the player's seat open, "
                   + v.Behind + " of " + seated + " with " + behind + " m of floor behind" + (v.First != null ? " (first: " + v.First + ")" : string.Empty);
        }
    }
}
