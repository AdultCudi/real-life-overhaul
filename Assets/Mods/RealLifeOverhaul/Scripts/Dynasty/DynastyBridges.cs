using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>
    /// What the dynasty asks of the other features, filled in by Wiring/Wiring.DynastyLinks and left empty (the default answer) while a feature is off. Every answer is read on a
    /// day's pass, a handover's step or a press, never per frame.
    /// </summary>
    public static class DynastyBridges
    {
        /// <summary>Whole money the wallet must keep for the investors of the founder's firms (Tech's firm account): it is not the estate's. Null: none.</summary>
        public static Func<long> Reserved;

        /// <summary>What the estate is made of: the Finance app's net worth (shares on the market counted, every debt it knows taken off)
        /// and what it leaves out, line by line. Null: the cash alone.</summary>
        public static Func<Holdings> Holdings;

        /// <summary>The day the present marriage began as family life kept it: -1 not married, 0 married with no day kept (a save from before it was). Null: -1.</summary>
        public static Func<int> Wedding;

        /// <summary>The people who may carry on or be left something, into the list (cleared): the partner, the children, the people the founder knows. The founder's
        /// age in years and the day are given. Null: nobody.</summary>
        public static Action<int, int, List<KinPerson>> People;

        /// <summary>The children seen grown today, by name, so their years go on after eighteen. Null: none.</summary>
        public static Action<List<string>> GrownChildren;

        /// <summary>Who draws a will up. Null: a notary with no name.</summary>
        public static Func<Notary> Notary;

        /// <summary>What the law has open against the founder and whether they are held (DynastyGate composes the answers). Null: nothing read, which counts as open.</summary>
        public static Func<LawFacts> Law;

        /// <summary>The Permadeath option is on, and it is armed for the save: a death then ends the save (DynastyGate.Final). Null: no.</summary>
        public static Func<bool> Permadeath, Armed;

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
            Holdings = null;
            Wedding = null;
            People = null;
            GrownChildren = null;
            Notary = null;
            Law = null;
            Permadeath = null;
            Armed = null;
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
