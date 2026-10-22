namespace RealLifeOverhaul.Family
{
    /// <summary>What a chore is about: each is a real state of the home, the player or their car.</summary>
    public enum ChoreKind { None = 0, Groceries = 1, Clean = 2, Shower = 3, CarWash = 4 }

    /// <summary>
    /// The rules of the household chores (Chores): a chore is asked only when something is really so (the fridge nearly empty,
    /// the home a tip, the player unwashed, their car filthy) and it is done only when that has really changed (food in the
    /// fridge, the home tidied, a shower, a car wash), within its days. A reading that cannot be had is -1 and never asks or
    /// settles anything. Game-free (EconomyCheck, FamilyChoreChecks.cs). COST: a few comparisons an hour.
    /// </summary>
    public static class ChoreRules
    {
        public const int FoodLow = 4;              // units of food in the fridges (and portions put by) under which they ask for shopping
        public const int FoodDone = 8;             // ...and at which it is done
        public const float TidyLow = 40f;          // the home's tidiness (0..100) under which they ask for a clean
        public const float TidyDone = 70f;
        public const float HygieneLow = 25f;       // the player's hygiene (0..100) under which they ask for a shower, face to face only
        public const float HygieneDone = 70f;
        public const float CarDirty = 0.6f;        // the dirtiest of the player's cars (0..1, the game's own dirt) over which they ask for a wash
        public const float CarDone = 0.15f;
        public const int GapDays = 2;              // days between one chore settled and the next asked
        public const int AskHours = 3;             // hours a chore raised face to face waits to be talked over before it is texted instead
        public const float HarmonyDone = 4f;
        public const float HarmonyMissed = -2f;
        public const float HarmonyRefused = -1f;

        /// <summary>The most pressing chore there is, or None: a shower only when both are at home, then shopping, a clean, the car.</summary>
        public static ChoreKind Wanted(int food, float tidy, float hygiene, float carDirt, bool together)
        {
            if (together && hygiene >= 0f && hygiene < HygieneLow)
            {
                return ChoreKind.Shower;
            }
            if (food >= 0 && food < FoodLow)
            {
                return ChoreKind.Groceries;
            }
            if (tidy >= 0f && tidy < TidyLow)
            {
                return ChoreKind.Clean;
            }
            return carDirt > CarDirty ? ChoreKind.CarWash : ChoreKind.None;
        }

        /// <summary>True once what was asked has really changed; an unread state (-1) never settles it.</summary>
        public static bool Done(ChoreKind kind, int food, float tidy, float hygiene, float carDirt)
        {
            switch (kind)
            {
                case ChoreKind.Groceries: return food >= FoodDone;
                case ChoreKind.Clean: return tidy >= TidyDone;
                case ChoreKind.Shower: return hygiene >= HygieneDone;
                case ChoreKind.CarWash: return carDirt >= 0f && carDirt <= CarDone;
                default: return false;
            }
        }

        /// <summary>The last day it may be done on: a shower the day it is asked, the rest by the day after next.</summary>
        public static int Due(ChoreKind kind, int day)
        {
            return kind == ChoreKind.Shower ? day : day + 2;
        }

        /// <summary>Whether a chore may be texted (asked while apart): a shower is said face to face or not at all.</summary>
        public static bool Textable(ChoreKind kind) => kind != ChoreKind.None && kind != ChoreKind.Shower;

        /// <summary>The ask's text, a complete key.</summary>
        public static string AskKey(ChoreKind kind)
        {
            switch (kind)
            {
                case ChoreKind.Groceries: return "reallife_family_chore_groceries";
                case ChoreKind.Clean: return "reallife_family_chore_clean";
                case ChoreKind.Shower: return "reallife_family_chore_shower";
                case ChoreKind.CarWash: return "reallife_family_chore_carwash";
                default: return null;
            }
        }
    }
}
