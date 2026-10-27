using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// One garage door of a design (the game's <c>DriveInEntrance</c>) measured round the design's root, in the root's own
    /// turn (metres, degrees), as the copy engine reads it from the building it copies. A warehouse's car or truck drives
    /// through it into the building's own interior: the game's <c>BuildingManager.EnterBuildingWithVehicle</c> takes the
    /// vehicle in by the slot <see cref="Id"/> + 1 of the warehouse (its <c>numberOfVehicleSlots</c>).
    /// </summary>
    public struct Gate
    {
        public int Id;          // the door's id (DriveInEntrance.doorID): the warehouse's vehicle slot it enters by is Id + 1
        public float X, Z;      // m round the root: where its door stands (the object that carries the game's component)
        public float Facing;    // degrees from the root's forward: the way a vehicle comes out of it (the door's forward)
        public float Width;     // m across: the closed door's collider
        public float Depth;     // m along: the tunnel that collider plugs
    }

    /// <summary>
    /// The entrance of a design's underground car park (the game's <c>UndergroundParkingEntrance</c>) measured round the
    /// design's root as <see cref="Gate"/> is: the game finds a car park by its number, so a copy is given a number of its
    /// own (<see cref="GateRules.ParkNumber"/>).
    /// </summary>
    public struct Park
    {
        public int Number;       // the original's number in the game's list of car parks; a copy's is its own
        public int Version;      // the car park's interior (ba:buildingsize_parking, version 1 or 2)
        public float X, Z;       // m round the root: the entrance's object
        public float Facing;     // degrees from the root's forward: the way the object is turned
        public float ExitX, ExitZ, ExitFacing;   // m, degrees: where a vehicle comes out (the game's vehicleExitPoint) and the way it faces
        public float PlayerX, PlayerZ;           // m: where a player on foot comes out
    }

    /// <summary>
    /// What the copy engine knows of the vehicle entrances of a building it copies, game-free (EconomyCheck's GateChecks,
    /// Census/design-gates-3682.json: the game's own, measured from its scenes by the kit's filllab/facts/design_gates.py):
    /// which of a design's garage doors a made building keeps (the game's warehouse is the one that takes a vehicle in, and
    /// only by a slot it has), whether a door looks out on the street the design is stood to (a door on a side wall that a
    /// neighbour's wall stands against takes no car), where it stands in the world, and the number of a copy's car park.
    /// Every answer comes from the arguments alone. COST: nothing per frame; a few loops over a design's doors at a load.
    /// </summary>
    public static class GateRules
    {
        public const string WarehouseType = "ba:buildingtype_warehouse";   // the only type whose registration is a Warehouse, with vehicle slots

        public const float FacingNear = 20f;      // degrees: a door's facing this near its design's front looks out on the street
        public const float FrontBehind = 4f;      // m a door may stand behind its design's front and still be reached from the street (the game's stand 1.7 m before it)
        public const float FrontAhead = 3f;       // m before it
        public const int ParkFrom = 1000000;      // the lowest number a copy's car park is given: the game's own are 1 to 5
        public const int ParkSpan = 1000000000;   // numbers in the range a copy's car park is drawn from, so two never share one in practice

        private const float Deg = (float)(Math.PI / 180.0);

        /// <summary>
        /// True when a copy of a design keeps the door as the game's own: a special building copied whole keeps all its doors;
        /// a warehouse (the only registration with vehicle slots) keeps the door whose slot (<see cref="Gate.Id"/> + 1) its
        /// structure has, <paramref name="slots"/> being the structure's <c>numberOfVehicleSlots</c> (H one, I, P and Q two).
        /// Any other building's look with a garage door has it shut: no car is taken into an interior that has no bay for it.
        /// </summary>
        public static bool Live(bool whole, string type, int slots, int id)
        {
            return whole || (string.Equals(type, WarehouseType, StringComparison.Ordinal) && id >= 0 && id < slots);
        }

        /// <summary>m a point stands behind the front of the design (the side it is stood to its street by); negative before it.</summary>
        public static float Behind(DesignEntry d, float x, float z)
        {
            NewBuildingsRules.DoorFrame(d, out _, out _, out _, out float vMax);
            float front = NewBuildingsRules.FrontOf(d) * Deg;
            return vMax - (x * (float)Math.Sin(front) + z * (float)Math.Cos(front));
        }

        /// <summary>True when a facing (degrees from the root's forward) is the design's front within <see cref="FacingNear"/>.</summary>
        public static bool Fronts(DesignEntry d, float facing)
        {
            float gap = NewBuildingsRules.Turn(facing - NewBuildingsRules.FrontOf(d) + 180f) - 180f;
            return Math.Abs(gap) <= FacingNear;
        }

        /// <summary>True when the door looks out on the side the design is stood to its street by, at that side: a car can come to it from the street.</summary>
        public static bool Serves(DesignEntry d, Gate g)
        {
            float behind = Behind(d, g.X, g.Z);
            return Fronts(d, g.Facing) && behind <= FrontBehind && behind >= -FrontAhead;
        }

        /// <summary>True when the design has garage doors and every one serves the street: the look a warehouse can wear so that a vehicle drives in from the street.</summary>
        public static bool Serves(DesignEntry d)
        {
            if (d == null || d.Gates == null || d.Gates.Length == 0)
            {
                return false;
            }
            for (int i = 0; i < d.Gates.Length; i++)
            {
                if (!Serves(d, d.Gates[i]))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>True when the design has an underground car park's entrance.</summary>
        public static bool HasPark(DesignEntry d)
        {
            return d != null && d.Parks != null && d.Parks.Length > 0;
        }

        /// <summary>True when a vehicle comes out of the car park on the side the design is stood to its street by, at that side.</summary>
        public static bool ParkServes(DesignEntry d, Park p)
        {
            float behind = Behind(d, p.ExitX, p.ExitZ);
            return Fronts(d, p.ExitFacing) && behind <= FrontBehind && behind >= -FrontAhead;
        }

        /// <summary>True when the design has a car park and every one serves the street.</summary>
        public static bool ParkServes(DesignEntry d)
        {
            if (!HasPark(d))
            {
                return false;
            }
            for (int i = 0; i < d.Parks.Length; i++)
            {
                if (!ParkServes(d, d.Parks[i]))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Where a point round a design's root stands in the world when the root is at (x, z) turned by <paramref name="yaw"/> degrees,
        /// as <see cref="NewBuildingsRules.DoorAt"/> gives a door.
        /// </summary>
        public static void World(float x, float z, float yaw, float localX, float localZ, out float worldX, out float worldZ)
        {
            float a = yaw * Deg;
            float s = (float)Math.Sin(a), c = (float)Math.Cos(a);
            worldX = x + localX * c + localZ * s;
            worldZ = z - localX * s + localZ * c;
        }

        /// <summary>The way a facing points in the world, as a whole quarter turn: 0 north (+Z), 1 east, 2 south, 3 west; the district's grid lies along the axes.</summary>
        public static int Quarter(float yaw, float facing)
        {
            return ((int)Math.Round(NewBuildingsRules.Turn(yaw + facing) / 90.0) % 4 + 4) % 4;
        }

        /// <summary>
        /// A copy's car park's number: its own, drawn from the made building's id so that it is the same on every machine and at
        /// every load, whatever else stands, and never one of the game's. The game finds a car park by it and a car left in the
        /// car park is kept by it (<c>ba:street_parking</c>, this number).
        /// </summary>
        public static int ParkNumber(string madeId)
        {
            return ParkFrom + (int)(NewBuildingsRules.Hash((madeId ?? string.Empty) + "|car park") % (uint)ParkSpan);
        }

        /// <summary>True for a number a copy's car park is given (the game's own are below <see cref="ParkFrom"/>).</summary>
        public static bool IsMadePark(int number)
        {
            return number >= ParkFrom;
        }
    }
}
