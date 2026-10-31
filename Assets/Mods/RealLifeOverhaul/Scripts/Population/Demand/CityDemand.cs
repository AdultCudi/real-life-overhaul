using System;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// What the mod's own businesses read of the city's people (the interface the business features' bridges are wired to in Scripts/Wiring): each demand's
    /// factor on the figure the business had (thousandths, a thousand on the anchor day and before it), each kind of online spending's, and each district's
    /// leans and counts. Game-free and safe at any time: while the population does not run, or has not been measured yet, every factor and lean is a
    /// thousand and every count is none, so each business keeps exactly its own figures. Main thread only. COST: a field read, or a walk of the districts
    /// (about a dozen) for a district's figure; nothing per frame.
    /// </summary>
    public static class CityDemand
    {
        /// <summary>The demand in use, or null. Set by the population's director only.</summary>
        internal static PopDemandState Live { get; set; }

        /// <summary>True once a day of the people has been measured (on a guest, once the host's figures have come).</summary>
        public static bool Running => Live != null && Live.Day >= 0;

        /// <summary>True once the anchor is taken: from that day the factors move with the people.</summary>
        public static bool Anchored => Live != null && Live.AnchorDay >= 0;

        /// <summary>The day the people were last measured; -1 for none.</summary>
        public static int Day => Live != null ? Live.Day : -1;

        /// <summary>A demand's factor, thousandths of its anchor day's (a thousand while none).</summary>
        public static int Factor(PopDemand d)
        {
            PopDemandState m = Live;
            int i = (int)d;
            return m != null && i >= 0 && i < m.Factors.Length ? m.Factors[i] : PopDemandCatalogue.Neutral;
        }

        /// <summary>
        /// The online factor of a business type's kind of spending (a clothing store's, its customers' clothing bought online), thousandths; the whole online
        /// market's for a type of no kind or a kind nobody buys online.
        /// </summary>
        public static int Online(string businessType)
        {
            PopDemandState m = Live;
            if (m == null)
            {
                return PopDemandCatalogue.Neutral;
            }
            int k = PopulationCatalogue.CategoryOfType(businessType);
            return k >= 0 && k < m.OnlineFactors.Length && PopDemandCatalogue.OnlineShare[k] > 0 ? m.OnlineFactors[k] : Factor(PopDemand.Online);
        }

        /// <summary>A district's income per household against the city's, thousandths (a thousand for none).</summary>
        public static int IncomeLean(string district) => Of(district, m => m.IncomeLean);

        /// <summary>A district's share of income put by against the city's, thousandths.</summary>
        public static int SavingLean(string district) => Of(district, m => m.SavingLean);

        /// <summary>A district's household debt per household against the city's, thousandths.</summary>
        public static int BorrowLean(string district) => Of(district, m => m.BorrowLean);

        /// <summary>A district's households taking or looking for a flat, per household, against the city's, thousandths.</summary>
        public static int HousingLean(string district) => Of(district, m => m.HousingLean);

        /// <summary>A district's crime against the city's, thousandths: its income, unemployment and young people against the city's.</summary>
        public static int Crime(string district) => Of(district, m => m.Crime);

        /// <summary>A district's homes and businesses against the anchor day's, thousandths: broadband's market there.</summary>
        public static int Connections(string district) => Of(district, m => m.ConnectionFactor);

        /// <summary>Thousandths of a district's household debt in a band (PopDemandRules.Poor, Fair, Good); -1 for none.</summary>
        public static int Ask(string district, int band)
        {
            PopDemandState m = Live;
            int d = m != null ? m.DistrictOf(district) : -1;
            return d >= 0 && band >= 0 && band < PopDemandCatalogue.Bands && d * PopDemandCatalogue.Bands + band < m.Ask.Length ? m.Ask[d * PopDemandCatalogue.Bands + band] : -1;
        }

        /// <summary>A district's share of borrowing in a band against the city's, thousandths (a thousand for none).</summary>
        public static int AskLean(string district, int band)
        {
            PopDemandState m = Live;
            int d = m != null ? m.DistrictOf(district) : -1;
            return d >= 0 && band >= 0 && band < PopDemandCatalogue.Bands && d * PopDemandCatalogue.Bands + band < m.AskLean.Length
                ? m.AskLean[d * PopDemandCatalogue.Bands + band] : PopDemandCatalogue.Neutral;
        }

        /// <summary>A district's adult residents (its electorate); -1 for none counted.</summary>
        public static int Adults(string district) => Count(district, m => m.Adults);

        /// <summary>A district's households; -1 for none counted.</summary>
        public static int Households(string district) => Count(district, m => m.Households);

        /// <summary>A district's businesses that sell to people; -1 for none counted.</summary>
        public static int Businesses(string district) => Count(district, m => m.Businesses);

        private static int Of(string district, Func<PopDemandState, int[]> table)
        {
            PopDemandState m = Live;
            int d = m != null ? m.DistrictOf(district) : -1;
            int[] values = d >= 0 ? table(m) : null;
            return values != null && d < values.Length ? values[d] : PopDemandCatalogue.Neutral;
        }

        private static int Count(string district, Func<PopDemandState, int[]> table)
        {
            PopDemandState m = Live;
            int d = m != null && m.Day >= 0 ? m.DistrictOf(district) : -1;
            int[] values = d >= 0 ? table(m) : null;
            return values != null && d < values.Length && values[d] > 0 ? values[d] : -1;
        }
    }
}
