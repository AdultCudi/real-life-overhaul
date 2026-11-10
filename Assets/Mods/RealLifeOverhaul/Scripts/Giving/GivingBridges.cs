using System;

namespace RealLifeOverhaul.Giving
{
    /// <summary>
    /// What giving asks of the other features, filled in by Wiring/Wiring.GivingLinks and left empty (the default answer) while a feature is off. Every answer is read on a
    /// day's pass or a press, never per frame.
    /// </summary>
    public static class GivingBridges
    {
        /// <summary>Whole money the wallet must keep for the investors of the player's firms (Tech's firm account): only the cash over it can be given. Null: none.</summary>
        public static Func<long> Reserved;

        /// <summary>The game's business sales between two days, both counted, whole money (what its tax counts as income). Null: none.</summary>
        public static Func<int, int, long> Income;

        /// <summary>How many of the wealth steps the player has passed, 0 to 5 (Standing's wealth). Null: 0.</summary>
        public static Func<int> WealthTier;

        /// <summary>How well the city knows the player, 0 to 100 (Standing's recognition). Null: 0.</summary>
        public static Func<int> Recognition;

        /// <summary>Points to Standing's integrity for a good deed; returns what was applied. Null: nothing.</summary>
        public static Func<int, int> Integrity;

        /// <summary>Tenths of a point of recognition from the press; returns what was applied. Null: nothing.</summary>
        public static Func<int, int> Press;

        /// <summary>A rival tycoon's worth in whole money, or -1 while they are not forward or are out. Null: -1.</summary>
        public static Func<int, long> RivalWorth;

        /// <summary>A rival tycoon gives so much: their worth falls by it, but never under the floor their worth grows back to (a gift from there would be made up the next day).
        /// Returns what really left them. Null: nothing.</summary>
        public static Func<int, long, long> RivalPay;

        /// <summary>A rival tycoon's name. Null: "".</summary>
        public static Func<int, string> RivalName;

        /// <summary>Money given to a person for good, whole money: a gift, or a loan written off (the dynasty counts it in the estate for a time). Null: nobody is told.</summary>
        public static Action<long> GaveToPerson;

        public static void Reset()
        {
            Reserved = null;
            Income = null;
            WealthTier = null;
            Recognition = null;
            Integrity = null;
            Press = null;
            RivalWorth = null;
            RivalPay = null;
            RivalName = null;
            GaveToPerson = null;
        }
    }
}
