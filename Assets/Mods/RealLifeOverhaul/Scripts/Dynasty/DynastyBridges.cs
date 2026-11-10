using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>Who draws a will up: a law firm of the city, or the founder's own (no fee).</summary>
    public struct Notary
    {
        public string Name;
        public bool Own;
    }

    /// <summary>
    /// What the dynasty asks of the other features, filled in by Wiring/Wiring.DynastyLinks and left empty (the default answer) while a feature is off. Every answer is read on a
    /// day's pass, a handover's step or a press, never per frame.
    /// </summary>
    public static class DynastyBridges
    {
        /// <summary>Whole money the wallet must keep for the investors of the founder's firms (Tech's firm account): it is not the estate's. Null: none.</summary>
        public static Func<long> Reserved;

        /// <summary>The founder's net worth as the Finance app shows it, shares on the market counted, every debt taken off. Null: the cash alone.</summary>
        public static Func<long> NetWorth;

        /// <summary>The people who may carry on or be left something, into the list (cleared): the partner, the children, the people the founder knows. The founder's
        /// age in years and the day are given. Null: nobody.</summary>
        public static Action<int, int, List<KinPerson>> People;

        /// <summary>The children seen grown today, by name, so their years go on after eighteen. Null: none.</summary>
        public static Action<List<string>> GrownChildren;

        /// <summary>Who draws a will up. Null: a notary with no name.</summary>
        public static Func<Notary> Notary;

        /// <summary>True while the law has anything open against the founder: a case, a warrant, prison, probation, hours owed, a suspended licence, wanted. Null: no.</summary>
        public static Func<bool> LawOpen;

        /// <summary>True while the founder is in a cell, in prison or before the court. Null: no.</summary>
        public static Func<bool> Custody;

        /// <summary>True while a death ends the save (Permadeath is on for it). Null: no.</summary>
        public static Func<bool> Final;

        /// <summary>True while Giving's charities can take a bequest. Null: no.</summary>
        public static Func<bool> Charities;

        /// <summary>A bequest into a charity's pot (its number, whole money); what really went in. The dynasty books the money. Null: nothing.</summary>
        public static Func<int, long, long> ToCharity;

        /// <summary>A bequest into a person's household as the population holds it (the social cast's index, whole money); what really landed. Null: nothing.</summary>
        public static Func<int, long, long> ToHousehold;

        /// <summary>The heir becomes the head in every other feature: the family, the people, the education, the standing, the record. Null: nothing.</summary>
        public static Action<Statement> Succeed;

        /// <summary>A rival tycoon's worth in whole money, or -1 while they are not forward, are out or rest (a session). Null: -1.</summary>
        public static Func<int, long> RivalWorth;

        /// <summary>A rival tycoon pays estate tax: their worth falls by it, never under their floor. Returns what really left them. Null: nothing.</summary>
        public static Func<int, long, long> RivalTax;

        /// <summary>A rival's new head finds their feet: their fortune grows at half the pace until the day. Null: nothing.</summary>
        public static Action<int, int> RivalSlow;

        /// <summary>A rival tycoon's name. Null: "".</summary>
        public static Func<int, string> RivalName;

        public static void Reset()
        {
            Reserved = null;
            NetWorth = null;
            People = null;
            GrownChildren = null;
            Notary = null;
            LawOpen = null;
            Custody = null;
            Final = null;
            Charities = null;
            ToCharity = null;
            ToHousehold = null;
            Succeed = null;
            RivalWorth = null;
            RivalTax = null;
            RivalSlow = null;
            RivalName = null;
        }
    }
}
