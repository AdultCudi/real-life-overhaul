using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Suppliers
{
    /// <summary>
    /// What suppliers and contracts ask of the other features, filled in by Wiring/Wiring.SupplierLinks and left empty (the default answer) while a feature is off. Every
    /// answer is read on an hour's or a day's pass or a press, never per frame.
    /// </summary>
    public static class SupplierBridges
    {
        /// <summary>The kind of goods a product is (the city's six kinds); -1 for a service, a raw material the exchange prices or a product the city does not know. Null: -1.</summary>
        public static Func<string, int> KindOf;

        /// <summary>The season, the weather and the city event of a day (the calendar's). Null: a fair spring day with nothing on.</summary>
        public static Func<int, CityDay> Day;

        /// <summary>The player's credit rating, 300 to 850; 0 where the credit side keeps none. Null: 0.</summary>
        public static Func<int> Credit;

        /// <summary>True where a manager of the mod's with stock orders on decides at a business today: one at work, or the regional manager standing in. Null: false.</summary>
        public static Func<BuildingRegistration, bool> Manager;

        /// <summary>That manager's budget for the month: a sum in whole money is spent from it and true, or false, nothing spent, when it does not fit. Null: no budget.</summary>
        public static Func<BuildingRegistration, long, bool> Budget;

        /// <summary>What the wallet must keep for the investors of the player's firms (Tech's firm account), cents; a payment to a supplier never takes it. Null: nothing.</summary>
        public static Func<long> Reserved;

        /// <summary>A rival tycoon's name while they are forward and the rivals are not resting; null otherwise. Null: none.</summary>
        public static Func<int, string> RivalName;

        /// <summary>A rival tycoon's worth moves by so much whole money (more paid for supply takes it down, never under their floor). Null: nothing.</summary>
        public static Action<int, long> RivalMove;

        /// <summary>The rival tycoons' outlets: whose each is, its key and the game's business type it trades as. Null: none.</summary>
        public static Action<List<RivalOutletRef>, List<string>> Outlets;

        public static void Reset()
        {
            KindOf = null;
            Day = null;
            Credit = null;
            Manager = null;
            Budget = null;
            Reserved = null;
            RivalName = null;
            RivalMove = null;
            Outlets = null;
        }
    }
}
