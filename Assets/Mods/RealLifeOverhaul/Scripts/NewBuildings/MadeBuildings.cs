using System;
using System.Collections.Generic;
using Buildings;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>A made building as it stands in this city: its declaration, the design it wears, where it stands and the game's objects for it.</summary>
    public sealed class MadeBuilding
    {
        public MadeSpec Spec;
        public string Design;                        // the design it wears (DesignEntry.Name)
        public int Lot = -1;                         // the lot of Spec.Lots it stands on
        public CityBuildingController Controller;    // the game's own controller of its copy, never destroyed in a session
        public Building Building;                    // the game's record of it, made at the load and never saved
        public BuildingRegistration Registration;    // kept only in the game's private lookup, never in the save's list (a district's: the save's own)
        public float X, Z, Yaw;                      // m and degrees: where its root stands and its turn
        public float DoorX, DoorY, DoorZ;            // m: its first door, where a save made inside it puts the player
        public float DoorFacing;                     // degrees: the way that door faces out
        public bool Shown;                           // standing in view; hidden while the option is off or a session disagrees
        public string Address;                       // as the game writes it, for the log
        public int DesignIndex = -1;                 // the design's place in the copy engine's catalogue at this load
        public bool NewRegistration;                 // a district's building whose registration was made at this load: nothing of it was in the save
    }

    /// <summary>
    /// The mod's own buildings, a service as Core/SpecialBuildings is: a feature declares a building of its own and finds
    /// it standing once the city has loaded.
    ///
    ///     MadeBuildings.Declare(new MadeSpec { Id = "city_hall", Street = MadeStreets.Prefix + "provinglane", Number = 2,
    ///         Size = "ba:buildingsize_k", Version = 1, Type = "ba:buildingtype_office", District = "...", Lots = lots });
    ///     MadeBuilding hall = MadeBuildings.Get("city_hall");   // after the load: null if it stood down (Player.log says why)
    ///
    /// Declare in the feature's director's Awake or earlier: every building is made behind the loading screen once every
    /// director is there, and again at every load; nothing of it is saved. The address is the feature's own, on a street of
    /// the mod's own, and does not move when the building has to take another lot. A made building can host floors of the
    /// floors core (docs/features/new-buildings.md, "A made building as a host of floors"). Costs nothing a frame.
    /// </summary>
    public static class MadeBuildings
    {
        private static readonly List<MadeSpec> Declared = new List<MadeSpec>(8);
        private static readonly List<MadeBuilding> City = new List<MadeBuilding>(8);

        /// <summary>Changes whenever a building is made, hidden or shown: a reader that keeps something made from them compares it.</summary>
        public static int Stamp { get; private set; }

        /// <summary>The buildings standing in this city, shown or hidden.</summary>
        public static int Count => City.Count;

        /// <summary>Declares a building, in place of one with the same id; made at the next load (or this one, if it has not come to them yet).</summary>
        public static void Declare(MadeSpec spec)
        {
            if (spec == null || string.IsNullOrEmpty(spec.Id))
            {
                return;
            }
            Withdraw(spec.Id);
            Declared.Add(spec);
        }

        /// <summary>Not made from the next load; one standing now stays until then. False if nothing had that id.</summary>
        public static bool Withdraw(string id)
        {
            for (int i = 0; i < Declared.Count; i++)
            {
                if (string.Equals(Declared[i].Id, id, StringComparison.Ordinal))
                {
                    Declared.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>The declarations whose feature wants them now, into the caller's list (not cleared), in the order they were declared.</summary>
        public static void Wanted(List<MadeSpec> into)
        {
            for (int i = 0; i < Declared.Count; i++)
            {
                MadeSpec spec = Declared[i];
                if (spec.Wanted == null || spec.Wanted())
                {
                    into.Add(spec);
                }
            }
        }

        /// <summary>The building of that id standing in this city, or null.</summary>
        public static MadeBuilding Get(string id)
        {
            for (int i = 0; i < City.Count; i++)
            {
                if (string.Equals(City[i].Spec.Id, id, StringComparison.Ordinal))
                {
                    return City[i];
                }
            }
            return null;
        }

        /// <summary>The building standing at that address in this city, or null.</summary>
        public static MadeBuilding At(string street, int number)
        {
            if (City.Count == 0 || !MadeStreets.IsMine(street))
            {
                return null;
            }
            for (int i = 0; i < City.Count; i++)
            {
                if (City[i].Spec.Number == number && string.Equals(City[i].Spec.Street, street, StringComparison.Ordinal))
                {
                    return City[i];
                }
            }
            return null;
        }

        /// <summary>The building whose controller this is, or null for any of the game's.</summary>
        public static MadeBuilding Of(CityBuildingController controller)
        {
            if (controller == null || City.Count == 0)
            {
                return null;
            }
            for (int i = 0; i < City.Count; i++)
            {
                if (City[i].Controller == controller)
                {
                    return City[i];
                }
            }
            return null;
        }

        /// <summary>Every building standing in this city, into the caller's list (not cleared).</summary>
        public static void All(List<MadeBuilding> into)
        {
            into.AddRange(City);
        }

        /// <summary>
        /// The address of every building standing in this city, into the caller's list (not cleared), for whatever keeps the
        /// mod's addresses out of the save. Any address on a street of the mod's own is a made one, standing or not:
        /// <see cref="IsMadeStreet"/> tells them by the street alone.
        /// </summary>
        public static void Addresses(List<Address> into)
        {
            for (int i = 0; i < City.Count; i++)
            {
                into.Add(new Address(City[i].Spec.Street, City[i].Spec.Number));
            }
        }

        /// <summary>True for an address of a building standing in this city.</summary>
        public static bool IsMade(string street, int number) => At(street, number) != null;

        /// <summary>True for a street of the mod's own: every address on it is a made building's, and never the game's. A string test.</summary>
        public static bool IsMadeStreet(string street)
        {
            return MadeStreets.IsMine(street);
        }

        internal static void Add(MadeBuilding made)
        {
            City.Add(made);
            Stamp++;
        }

        internal static void Changed()
        {
            Stamp++;
        }

        internal static void Forget()
        {
            City.Clear();
            Stamp++;
        }
    }
}
