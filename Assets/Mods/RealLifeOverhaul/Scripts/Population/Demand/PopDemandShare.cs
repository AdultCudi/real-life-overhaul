using System;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The population's demand in a Going Public session: the host measures its own people and the guests read the host's factors and district leans, so every
    /// player's own bank, hotels, agency and the rest draw on the one city's people. Two sections of the population's part P (Core/SharedCity), each starting
    /// with its own version: <c>m:</c> (the day, the anchor day, every demand's factor and each kind of online spending's) and <c>n:</c> (each district's homes,
    /// businesses, adults and leans). A reader of the part before them skips sections it does not know; a reader of a later version reads the fields it knows
    /// (new fields only ever go at the end). Game-free. COST: nothing per frame; a few hundred characters once a game day.
    /// </summary>
    public static class PopDemandShare
    {
        public const string Factors = "m:", Districts = "n:";
        public const int Version = 1;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The host's two sections, each led by its '~', for the end of the part.</summary>
        public static void Write(StringBuilder t, PopDemandState m)
        {
            if (m == null || m.Day < 0)
            {
                return;
            }
            t.Append('~').Append(Factors).Append(Version.ToString(Inv)).Append('|').Append(m.Day.ToString(Inv)).Append('|').Append(m.AnchorDay.ToString(Inv));
            for (int i = 0; i < m.Factors.Length; i++)
            {
                t.Append('|').Append(m.Factors[i].ToString(Inv));
            }
            for (int k = 0; k < m.OnlineFactors.Length; k++)
            {
                t.Append('|').Append(m.OnlineFactors[k].ToString(Inv));
            }
            t.Append('~').Append(Districts).Append(Version.ToString(Inv));
            for (int d = 0; d < m.Districts; d++)
            {
                t.Append(';').Append(Clean(m.Ids[d]));
                Field(t, m.Households[d]);
                Field(t, m.Businesses[d]);
                Field(t, m.Adults[d]);
                Field(t, m.IncomeLean[d]);
                Field(t, m.SavingLean[d]);
                Field(t, m.BorrowLean[d]);
                Field(t, m.HousingLean[d]);
                Field(t, m.Crime[d]);
                for (int b = 0; b < PopDemandCatalogue.Bands; b++)
                {
                    Field(t, m.Ask[d * PopDemandCatalogue.Bands + b]);
                }
                Field(t, m.ConnectionFactor[d]);
                for (int b = 0; b < PopDemandCatalogue.Bands; b++)
                {
                    Field(t, m.AskLean[d * PopDemandCatalogue.Bands + b]);
                }
            }
        }

        /// <summary>The host's sections applied to a guest's demand (null for none, the guest's businesses keep their own figures); false with no section to read.</summary>
        public static bool Apply(PopDemandState mine, string factors, string districts)
        {
            if (mine == null || string.IsNullOrEmpty(factors))
            {
                return false;
            }
            string[] f = factors.Split('|');
            if (f.Length < 3 || !int.TryParse(f[0], NumberStyles.Integer, Inv, out int version) || version < 1)
            {
                return false;
            }
            mine.Guest = true;
            mine.Day = Int(f[1], -1);
            mine.AnchorDay = Int(f[2], -1);
            int at = 3;
            for (int i = 0; i < mine.Factors.Length; i++)
            {
                mine.Factors[i] = Bound(at < f.Length ? Int(f[at++], PopDemandCatalogue.Neutral) : PopDemandCatalogue.Neutral);
            }
            for (int k = 0; k < mine.OnlineFactors.Length; k++)
            {
                mine.OnlineFactors[k] = Bound(at < f.Length ? Int(f[at++], PopDemandCatalogue.Neutral) : PopDemandCatalogue.Neutral);
            }
            if (string.IsNullOrEmpty(districts))
            {
                return true;
            }
            string[] records = districts.Split(';');
            int count = 0;
            var ids = new string[Math.Max(0, records.Length - 1)];
            for (int r = 1; r < records.Length; r++)
            {
                string[] x = records[r].Split(',');
                if (x.Length > 0 && x[0].Length > 0 && count < PopulationCatalogue.DistrictsMost)
                {
                    ids[count++] = x[0];
                }
            }
            Array.Resize(ref ids, count);
            mine.Size(ids);
            for (int r = 1, d = 0; r < records.Length && d < count; r++)
            {
                string[] x = records[r].Split(',');
                if (x.Length == 0 || x[0].Length == 0)
                {
                    continue;
                }
                int i = 1;
                mine.Households[d] = Next(x, ref i, 0);
                mine.Businesses[d] = Next(x, ref i, 0);
                mine.Adults[d] = Next(x, ref i, 0);
                mine.IncomeLean[d] = Next(x, ref i, PopDemandCatalogue.Neutral);
                mine.SavingLean[d] = Next(x, ref i, PopDemandCatalogue.Neutral);
                mine.BorrowLean[d] = Next(x, ref i, PopDemandCatalogue.Neutral);
                mine.HousingLean[d] = Next(x, ref i, PopDemandCatalogue.Neutral);
                mine.Crime[d] = Next(x, ref i, PopDemandCatalogue.Neutral);
                for (int b = 0; b < PopDemandCatalogue.Bands; b++)
                {
                    mine.Ask[d * PopDemandCatalogue.Bands + b] = Next(x, ref i, b == PopDemandRules.Fair ? 1000 : 0);
                }
                mine.ConnectionFactor[d] = Next(x, ref i, PopDemandCatalogue.Neutral);
                for (int b = 0; b < PopDemandCatalogue.Bands; b++)
                {
                    mine.AskLean[d * PopDemandCatalogue.Bands + b] = Next(x, ref i, PopDemandCatalogue.Neutral);
                }
                d++;
            }
            return true;
        }

        private static void Field(StringBuilder t, long value)
        {
            t.Append(',').Append(value.ToString(Inv));
        }

        private static int Next(string[] x, ref int i, int otherwise)
        {
            return i < x.Length ? Int(x[i++], otherwise) : otherwise;
        }

        private static int Int(string s, int otherwise)
        {
            return int.TryParse(s, NumberStyles.Integer, Inv, out int v) ? v : otherwise;
        }

        private static int Bound(int factor)
        {
            return Math.Max(0, Math.Min(PopDemandCatalogue.FactorMost, factor));
        }

        // Ids never hold the part's separators; anything that would is taken out, so the part always reads.
        private static string Clean(string s)
        {
            return (s ?? string.Empty).Replace(";", string.Empty).Replace(",", string.Empty).Replace("~", string.Empty).Replace("|", string.Empty);
        }
    }
}
