using System;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What delivery trips ask of the rest of the mod, filled in by Wiring/LogisticsLinks. Each is optional.</summary>
    public static class LogisticsBridges
    {
        /// <summary>Metres of road between two places, measured a slice a call: -1 while still looking, 0 with no way. Default: 0, the straight line's guess stays.</summary>
        public static Func<Vector3, Vector3, int> Road;

        /// <summary>Whether a gift of food to charity can be taken now (giving runs and the city's people are there). Default: no, and the food is thrown out as ever.</summary>
        public static Func<bool> FoodGiftReady;

        /// <summary>Food given to charity: whole money it cost, and whole money of it that earns (a deduction and the rest). Returns whether it was taken. Default: no.</summary>
        public static Func<long, long, bool> FoodGift;

        /// <summary>Points of neighbourhood standing in a district for a place's gift; returns what was applied. Default: none.</summary>
        public static Func<string, int, int> Neighbourhood;

        /// <summary>A small lift in satisfaction for the staff of a place that gave: the points each. Default: none.</summary>
        public static Action<BuildingRegistration, int> StaffLift;

        public static void Reset()
        {
            Road = null;
            FoodGiftReady = null;
            FoodGift = null;
            Neighbourhood = null;
            StaffLift = null;
        }
    }
}
