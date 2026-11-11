using System;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>How the making of a car on a lot ended (see <see cref="CarStandRules"/>).</summary>
    public enum CarMaking : byte
    {
        Made = 0,           // the car stands, and it is the model that was paid for
        NoPlace = 1,        // the lot has no place for it: the lot's fault, not the model's
        UnknownModel = 2,   // the game has no such model (a car mod taken out, a changed game)
        WrongModel = 3,     // the game stood another model than the one paid for
        Failed = 4          // the game would not make it
    }

    /// <summary>
    /// The promise behind every car the Car Showroom pays for (docs/features/car-suppliers.md, "The car paid for is the car that stands"):
    /// the car on the lot is the model that was paid for, or the whole price goes back and a notice says why. A listing carries its model
    /// by name from the market to the game's vehicle, so what can differ is what the game makes of it; this is the one judgement of that, for
    /// a car bought at once, a car that comes later and a car the player walks in to find. Game-free, so EconomyCheck holds every verdict.
    /// COST: a string comparison when a car is made or a showroom is walked into.
    /// </summary>
    public static class CarStandRules
    {
        public const string RefuseKey = "reallife_dealer_refuse_model";            // a model the game no longer has, refused before any money moves
        public const string UnknownKey = "reallife_dealer_note_stand_unknown";     // ...paid back: the game has no such model
        public const string WrongKey = "reallife_dealer_note_stand_wrong";         // ...paid back: the game stood another model
        public const string FailedKey = "reallife_dealer_note_stand_failed";       // ...paid back: the game would not make it

        /// <summary>
        /// What the game stood against what was paid for: <see cref="CarMaking.Made"/> when the model is the one paid for, or when the game
        /// does not say which it stood; <see cref="CarMaking.WrongModel"/> when it names another; <see cref="CarMaking.UnknownModel"/> when
        /// there is no model to pay for. A model is told by its name exactly.
        /// </summary>
        public static CarMaking Verdict(string paid, string stood)
        {
            if (string.IsNullOrEmpty(paid))
            {
                return CarMaking.UnknownModel;
            }
            return string.IsNullOrEmpty(stood) || string.Equals(paid, stood, StringComparison.Ordinal) ? CarMaking.Made : CarMaking.WrongModel;
        }

        /// <summary>True when the model is at fault, not the lot: the car is paid back and the player told, where a lot with no place only makes a car wait.</summary>
        public static bool ModelFault(CarMaking making)
        {
            return making == CarMaking.UnknownModel || making == CarMaking.WrongModel || making == CarMaking.Failed;
        }

        /// <summary>What goes back for a car that cannot be stood: all of what it cost, never less than nothing.</summary>
        public static int Refund(int cost)
        {
            return Math.Max(0, cost);
        }

        /// <summary>The key of the notice that says why a paid-for car was paid back; none for a car that stands or only waits for a place.</summary>
        public static string NoticeKey(CarMaking making)
        {
            switch (making)
            {
                case CarMaking.UnknownModel: return UnknownKey;
                case CarMaking.WrongModel: return WrongKey;
                case CarMaking.Failed: return FailedKey;
                default: return null;
            }
        }
    }
}
