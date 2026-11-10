using System;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Parties
{
    /// <summary>
    /// What parties and company events ask of the rest of the mod, filled in by Wiring/Wiring.PartyLinks. Each is optional
    /// with a default, and a fault in the other feature reads as the default (PartyGame catches it).
    /// </summary>
    public static class PartyBridges
    {
        /// <summary>Startup money: what the wallet must keep for the investors of the player's firms, whole money. Default: nothing.</summary>
        public static Func<long> Floor;

        /// <summary>The company: its file of one of the player's employees, by the game's id (their standing, the going rate). Default: none kept.</summary>
        public static Func<string, StaffFile> StaffFile;

        /// <summary>The company: the level its staff rules run at. Default: Standard.</summary>
        public static Func<int> StaffLevel;

        /// <summary>The company: a business has a manager at work who can be told to hold an event. Default: nobody.</summary>
        public static Func<string, bool> Manager;

        /// <summary>Business pressure's closures: a business of the player's closed for so many hours from now, and opened again by itself. Default: cannot be.</summary>
        public static Func<BuildingRegistration, int, bool> Close;
    }
}
