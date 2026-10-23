using UnityEngine;

namespace RealLifeOverhaul.Floors
{
    /// <summary>A structure of the game's for a floor: a size and version the game has an interior for (Buildings/BuildingSizeResolver).</summary>
    public sealed class FloorStructure
    {
        public readonly string Size;      // the game's size id, "ba:buildingsize_d"
        public readonly int Version;

        public FloorStructure(string size, int version)
        {
            Size = size;
            Version = version;
        }
    }

    /// <summary>The game's own furniture for a floor: a layout of a business type for the floor's structure, by its name (BusinessLayoutSets).</summary>
    public sealed class FloorLayout
    {
        public readonly string BusinessType;   // "ba:businesstype_school"
        public readonly string Name;           // "GarmentDistrictSchool"

        public FloorLayout(string businessType, string name)
        {
            BusinessType = businessType;
            Name = name;
        }
    }

    /// <summary>What laying a floor's layout came to, for its place's log line.</summary>
    public struct FloorLaid
    {
        public int Made;      // pieces of the game's that stand
        public int Skipped;   // pieces left out (out of season, or no prefab)
        public int Frames;    // frames the laying took
        public long Ms;       // milliseconds of it in all
    }

    /// <summary>Where a place's floor is, for the map, the route guider and the Guide.</summary>
    public struct FloorWhere
    {
        public FloorHost Host;
        public CityBuildingController Building;
        public string Street;
        public int Number;
        public int Floor;     // its first floor, from 1
        public Vector3 Door;  // where the player stands at the building's door to choose it
    }

    /// <summary>One floor of a building: the place on it, and which of the place's floors it is.</summary>
    internal struct FloorSlot
    {
        public FloorPlace Place;
        public int Part;      // from 1, of Place.Floors
    }

    /// <summary>
    /// The player on a floor of a building the mod holds: which, whose place, where they came in (the lift is there), and what
    /// stands on it. One at a time; made when the player arrives and dropped when they leave. Nothing of it is saved.
    /// </summary>
    public sealed class FloorVisit
    {
        public FloorHost Host;
        public FloorPlace Place;
        public int Floor;            // from 1
        public int Part;             // which of the place's floors, from 1
        public Vector3 Spawn;        // where the player came in: the lift stands there
        public Quaternion Turn;      // ...and which way they faced
        public int Door;             // the building's door they came in by (the game's door id), -1 not known
        public bool Borrowed;        // the floor stands on a structure that is not the building's own
        public string Structure;     // the structure on show, as the game writes it ("D1"), for the log
        public string OwnStructure;  // the building's own ("C2")
        public string Address;       // the building's address as the game writes it
        public bool Stays;           // as it ends: the player stays inside (its floors taken away), not leaving by the game's door
        public string How;           // how the player came to the floor, in words for the log ("the floor button at the door", "the lift")
        internal string Key;         // the building's key (FloorsRules.Key) when the visit began
        internal string ShownSize;   // the structure on show when borrowed: its size and version
        internal int ShownVersion;
        internal string OwnSize;     // the building's own size and version, to see them put back on the way out
        internal int OwnVersion;
        internal BuildingRegistration Shadow;   // the manager's registration of the place's own while on the floor, never saved
        internal int Built;          // the furniture's generation: a laying or fit-out of an older one stops
        internal bool Laying;        // the place's layout is being laid in
    }
}
