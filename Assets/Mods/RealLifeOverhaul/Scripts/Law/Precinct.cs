using UnityEngine;

namespace RealLifeOverhaul.Law
{
    /// <summary>One police station: a building of the city's own, designated and dressed by the mod.</summary>
    public sealed class Precinct
    {
        public int Index;
        public CityBuildingController Building;
        public string District;         // the game's neighbourhood id
        public string NameKey;
        public string Name;             // as shown
        public Vector3 Door;
        public string Address;          // "street|number", as kept in the save
        /// <summary>True if the player can walk in: an empty unit nobody rents, entered through its own door.</summary>
        public bool WalkIn;
        /// <summary>How well its frontage takes the station's dressing, measured when it was chosen (higher is better).</summary>
        public int Score;
        internal MapPlace Place;
        internal bool[] DoorWas;        // the doors' own "always open" flags, to be put back
        internal bool DoorsOpen;
        internal bool Stays;            // on the player's building with nowhere else to go: not looked at again this session
    }
}
