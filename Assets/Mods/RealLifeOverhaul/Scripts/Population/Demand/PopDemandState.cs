using System;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The population's demand for the mod's own businesses as it stands: each source's figure on the last day measured and on the anchor day, each demand's
    /// factor worked out from the two, and each district's figures (who lives there, how they earn, save and borrow, how many look for a flat) with the leans
    /// the businesses there read (a district's figure against the city's). The anchor belongs to one population (its seed and the day it was generated): a
    /// city generated again is anchored again. A Going Public guest holds the host's factors and leans only. Game-free. COST: nothing per frame; worked out
    /// once a game day by PopDemandJob, read by a field.
    /// </summary>
    public sealed class PopDemandState
    {
        public const int Version = 1;

        public int Seed, Generated;          // the population it measures; its anchor is of that one
        public int Day = -1;                 // the day last measured; -1 for none yet
        public int AnchorDay = -1;           // the day it was anchored; -1 while not yet (a new city is anchored once it has lived through a month's turn)
        public bool Guest;                   // the host's figures, applied: nothing here is measured

        public readonly long[] Now = new long[PopDemandCatalogue.Sources];
        public readonly long[] Anchor = new long[PopDemandCatalogue.Sources];
        public readonly long[] OnlineNow = new long[PopDemandCatalogue.Kinds];       // cents a day spent online, by kind of spending
        public readonly long[] OnlineAnchor = new long[PopDemandCatalogue.Kinds];
        public readonly int[] Factors = new int[PopDemandCatalogue.Demands];          // thousandths of the anchor day's
        public readonly int[] OnlineFactors = new int[PopDemandCatalogue.Kinds];

        // By district, in the population's own order of its districts (Ids).
        public string[] Ids = new string[0];
        public int[] Households = new int[0], Businesses = new int[0], Adults = new int[0], People = new int[0];
        public int[] Workers = new int[0], Unemployed = new int[0], Youth = new int[0], Housing = new int[0];
        public long[] Income = new long[0];          // cents a day of gross income, its households'
        public long[] Disposable = new long[0];      // cents a day after tax
        public long[] Saved = new long[0];           // cents a day put by (less what was drawn from savings)
        public long[] Debt = new long[0];            // whole money: its households' debt, each household's to the poor band's line (what a lender would count)
        public long[] Borrowing = new long[0];       // cents a day: the income after tax of its households in each credit band, Bands a district
        public long[] Connections = new long[0];     // its homes and businesses on the anchor day (broadband's market moves from it); 0 for none
        public int[] IncomeLean = new int[0], SavingLean = new int[0], BorrowLean = new int[0], HousingLean = new int[0], Crime = new int[0];
        public int[] Ask = new int[0];               // thousandths of a district's borrowing in each band, Bands a district
        public int[] ConnectionFactor = new int[0];  // thousandths of the anchor day's homes and businesses; 1000 with no anchor

        public PopDemandState()
        {
            for (int i = 0; i < Factors.Length; i++)
            {
                Factors[i] = PopDemandCatalogue.Neutral;
            }
            for (int i = 0; i < OnlineFactors.Length; i++)
            {
                OnlineFactors[i] = PopDemandCatalogue.Neutral;
            }
        }

        public int Districts => Ids.Length;

        /// <summary>The district table sized for these districts, keeping what each district already had by its id (a district the city adds starts with none).</summary>
        public void Size(string[] ids)
        {
            if (ids == null)
            {
                return;
            }
            bool same = ids.Length == Ids.Length;
            for (int i = 0; same && i < ids.Length; i++)
            {
                same = string.Equals(ids[i], Ids[i], StringComparison.Ordinal);
            }
            if (same)
            {
                return;
            }
            long[] connections = new long[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                int was = DistrictOf(ids[i]);
                connections[i] = was >= 0 && was < Connections.Length ? Connections[was] : 0;
            }
            int n = ids.Length;
            Ids = (string[])ids.Clone();
            Households = new int[n];
            Businesses = new int[n];
            Adults = new int[n];
            People = new int[n];
            Workers = new int[n];
            Unemployed = new int[n];
            Youth = new int[n];
            Housing = new int[n];
            Income = new long[n];
            Disposable = new long[n];
            Saved = new long[n];
            Debt = new long[n];
            Borrowing = new long[n * PopDemandCatalogue.Bands];
            Connections = connections;
            IncomeLean = Filled(n, PopDemandCatalogue.Neutral);
            SavingLean = Filled(n, PopDemandCatalogue.Neutral);
            BorrowLean = Filled(n, PopDemandCatalogue.Neutral);
            HousingLean = Filled(n, PopDemandCatalogue.Neutral);
            Crime = Filled(n, PopDemandCatalogue.Neutral);
            Ask = new int[n * PopDemandCatalogue.Bands];
            ConnectionFactor = Filled(n, PopDemandCatalogue.Neutral);
        }

        /// <summary>A district's place by its id, or -1.</summary>
        public int DistrictOf(string id)
        {
            for (int i = 0; id != null && i < Ids.Length; i++)
            {
                if (string.Equals(Ids[i], id, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Every district's day figures set to nothing, before a day's measure.</summary>
        public void ClearDay()
        {
            Array.Clear(Households, 0, Households.Length);
            Array.Clear(Businesses, 0, Businesses.Length);
            Array.Clear(Adults, 0, Adults.Length);
            Array.Clear(People, 0, People.Length);
            Array.Clear(Workers, 0, Workers.Length);
            Array.Clear(Unemployed, 0, Unemployed.Length);
            Array.Clear(Youth, 0, Youth.Length);
            Array.Clear(Housing, 0, Housing.Length);
            Array.Clear(Income, 0, Income.Length);
            Array.Clear(Disposable, 0, Disposable.Length);
            Array.Clear(Saved, 0, Saved.Length);
            Array.Clear(Debt, 0, Debt.Length);
            Array.Clear(Borrowing, 0, Borrowing.Length);
        }

        /// <summary>The anchor forgotten: the next settled day is the first again (a city generated again, or a save from another population).</summary>
        public void Forget()
        {
            AnchorDay = -1;
            Array.Clear(Anchor, 0, Anchor.Length);
            Array.Clear(OnlineAnchor, 0, OnlineAnchor.Length);
            Array.Clear(Connections, 0, Connections.Length);
            for (int i = 0; i < Factors.Length; i++)
            {
                Factors[i] = PopDemandCatalogue.Neutral;
            }
            for (int i = 0; i < OnlineFactors.Length; i++)
            {
                OnlineFactors[i] = PopDemandCatalogue.Neutral;
            }
            for (int i = 0; i < ConnectionFactor.Length; i++)
            {
                ConnectionFactor[i] = PopDemandCatalogue.Neutral;
            }
        }

        private static int[] Filled(int n, int value)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++)
            {
                a[i] = value;
            }
            return a;
        }
    }
}
