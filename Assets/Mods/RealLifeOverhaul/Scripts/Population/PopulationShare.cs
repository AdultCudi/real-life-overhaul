using System;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The population in a Going Public session (Core/SharedCity, part P; POPULATION.md section 11): the host owns the people and works out every
    /// business's day, its own and the guests'; a guest follows the host's figures and lives none of its own. The part is a line of its own version
    /// (<c>v=1~</c>) that the shared city carries only between players of the same version of this mod (another version's message is never applied):
    /// the day, the council's books and the region, each district's figures, each business's takings, class mix and customers by its address, and the
    /// residents of each genre on each platform (the game studio's audience).
    /// A guest anchors each business on the first takings it hears (so the game's own count on that machine stands that day, as the host's does for
    /// its own), and reads the drill-down to people as the host's alone. Game-free. COST: nothing per frame; the host writes a few tens of
    /// kilobytes once a game day, a guest reads them.
    /// </summary>
    public static class PopulationShare
    {
        public const char Part = 'P';
        public const int Version = 1;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The host's part as it stands.</summary>
        public static string Write(PopulationState s)
        {
            var t = new StringBuilder(4096 + s.Businesses.Count * 48);
            t.Append("v=").Append(Version).Append("~h:");
            Join(t, s.Day, s.YearDays, s.MonthNumber, s.Births, s.Deaths, s.BirthsLast, s.DeathsLast, s.Region.MovedIn, s.Region.MovedOut, s.Region.MovedInLast,
                s.Region.MovedOutLast, s.Region.Commuting, s.People.Live, s.Households.Live);
            CityBooks k = s.Books;
            t.Append("~k:");
            JoinLong(t, k.IncomeTax, k.SalesTax, k.PropertyTax, k.IncomeTaxLast, k.SalesTaxLast, k.PropertyTaxLast, k.Benefits, k.Pensions, k.BenefitsLast, k.PensionsLast,
                k.Utilities, k.UtilitiesLast, k.Outside, k.OutsideLast, k.CommuterSpend, k.VisitorSpend, k.CommuterSpendLast, k.VisitorSpendLast, k.Wages, k.WagesLast, k.Rents, k.RentsLast);
            t.Append("~d:");
            for (int d = 0; d < s.Districts.Count && d < s.Figures.Length; d++)
            {
                DistrictFigures f = s.Figures[d];
                t.Append(d > 0 ? ";" : string.Empty).Append(Clean(s.Districts[d].Id)).Append(',');
                Join(t, ',', f.People, f.Households, f.Children, f.Pupils, f.Students, f.Workers, f.Unemployed, f.Retired, f.Home, f.Flats, f.Vacant, f.Jobs, f.Vacancies, f.Commuters,
                    f.RentIndex, f.MeanRent, f.MeanIncome, f.Births, f.Deaths, f.MovedIn, f.MovedOut);
                t.Append(',').Append(f.Spending.ToString(Inv)).Append(',').Append(f.Takings.ToString(Inv)).Append(',').Append(f.Taxes.ToString(Inv));
            }
            t.Append("~b:");
            BusinessTable bt = s.Businesses;
            bool first = true;
            for (int b = 0; b < bt.Count; b++)
            {
                if (!bt.Seen[b] || bt.Key[b] == null)
                {
                    continue;
                }
                t.Append(first ? string.Empty : ";").Append(Clean(bt.Key[b]).Replace('|', ',')).Append(',').Append(Clean(bt.Type[b] ?? string.Empty)).Append(',');
                JoinLongSep(t, ',', bt.Takings[b], bt.ByClass[b * 3], bt.ByClass[b * 3 + 1], bt.ByClass[b * 3 + 2], bt.Visits[b]);
                first = false;
            }
            t.Append("~g:");   // the residents of each genre on each platform, then at the population's start: the studio's audience on a guest is the host's
            Join(t, s.GameCounts);
            t.Append('|');
            Join(t, s.GameStart);
            return t.ToString();
        }

        /// <summary>The host's part applied to a guest's state; false, the state untouched, for a line this version cannot read.</summary>
        public static bool Apply(PopulationState mine, string text)
        {
            if (mine == null || string.IsNullOrEmpty(text) || !text.StartsWith("v=", StringComparison.Ordinal))
            {
                return false;
            }
            string[] parts = text.Split('~');
            if (!int.TryParse(parts[0].Substring(2), NumberStyles.Integer, Inv, out int version) || version < 1)
            {
                return false;
            }
            try
            {
                string head = Find(parts, "h:"), books = Find(parts, "k:"), districts = Find(parts, "d:"), businesses = Find(parts, "b:");
                if (head == null || books == null)
                {
                    return false;
                }
                long[] h = Numbers(head, '|', 14);
                long[] k = Numbers(books, '|', 22);
                if (h == null || k == null)
                {
                    return false;
                }
                mine.Day = (int)h[0];
                mine.YearDays = Math.Max(8, (int)h[1]);
                mine.MonthNumber = (int)h[2];
                mine.Births = (int)h[3];
                mine.Deaths = (int)h[4];
                mine.BirthsLast = (int)h[5];
                mine.DeathsLast = (int)h[6];
                mine.Region.MovedIn = (int)h[7];
                mine.Region.MovedOut = (int)h[8];
                mine.Region.MovedInLast = (int)h[9];
                mine.Region.MovedOutLast = (int)h[10];
                mine.Region.Commuting = (int)h[11];
                mine.HostPeople = (int)h[12];
                mine.HostHouseholds = (int)h[13];
                CityBooks b = mine.Books;
                b.IncomeTax = k[0]; b.SalesTax = k[1]; b.PropertyTax = k[2]; b.IncomeTaxLast = k[3]; b.SalesTaxLast = k[4]; b.PropertyTaxLast = k[5];
                b.Benefits = k[6]; b.Pensions = k[7]; b.BenefitsLast = k[8]; b.PensionsLast = k[9]; b.Utilities = k[10]; b.UtilitiesLast = k[11];
                b.Outside = k[12]; b.OutsideLast = k[13]; b.CommuterSpend = k[14]; b.VisitorSpend = k[15]; b.CommuterSpendLast = k[16]; b.VisitorSpendLast = k[17];
                b.Wages = k[18]; b.WagesLast = k[19]; b.Rents = k[20]; b.RentsLast = k[21];
                ApplyDistricts(mine, districts);
                ApplyBusinesses(mine, businesses);
                long[] g = Find(parts, "g:") is string games ? Numbers(games, '|', GameRules.Cells * 2) : null;
                for (int i = 0; g != null && i < GameRules.Cells; i++)
                {
                    mine.GameCounts[i] = (int)g[i];
                    mine.GameStart[i] = (int)g[GameRules.Cells + i];
                }
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static void ApplyDistricts(PopulationState mine, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            foreach (string record in text.Split(';'))
            {
                string[] f = record.Split(',');
                if (f.Length < 25)
                {
                    continue;
                }
                int d = mine.DistrictOf(f[0]);
                if (d < 0)
                {
                    mine.Districts.Add(new PopDistrict { Id = f[0] });
                    d = mine.Districts.Count - 1;
                }
                if (mine.Figures.Length <= d)
                {
                    var grown = new DistrictFigures[d + 1];
                    Array.Copy(mine.Figures, grown, mine.Figures.Length);
                    for (int i = mine.Figures.Length; i < grown.Length; i++)
                    {
                        grown[i] = new DistrictFigures();
                    }
                    mine.Figures = grown;
                }
                DistrictFigures x = mine.Figures[d];
                int at = 1;
                x.People = I(f[at++]); x.Households = I(f[at++]); x.Children = I(f[at++]); x.Pupils = I(f[at++]); x.Students = I(f[at++]); x.Workers = I(f[at++]);
                x.Unemployed = I(f[at++]); x.Retired = I(f[at++]); x.Home = I(f[at++]); x.Flats = I(f[at++]); x.Vacant = I(f[at++]); x.Jobs = I(f[at++]);
                x.Vacancies = I(f[at++]); x.Commuters = I(f[at++]); x.RentIndex = I(f[at++]); x.MeanRent = I(f[at++]); x.MeanIncome = I(f[at++]);
                x.Births = I(f[at++]); x.Deaths = I(f[at++]); x.MovedIn = I(f[at++]); x.MovedOut = I(f[at++]);
                x.Spending = L(f[at++]); x.Takings = L(f[at++]); x.Taxes = L(f[at]);
            }
        }

        private static void ApplyBusinesses(PopulationState mine, string text)
        {
            BusinessTable bt = mine.Businesses;
            for (int b = 0; b < bt.Count; b++)
            {
                bt.Seen[b] = false;
            }
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            foreach (string record in text.Split(';'))
            {
                string[] f = record.Split(',');
                if (f.Length < 8)
                {
                    continue;
                }
                string key = f[0] + "|" + f[1];
                int b = mine.BusinessOfKey(key);
                if (b < 0)
                {
                    b = bt.Add(key);
                    mine.KnowBusiness(key, b);
                }
                bt.Type[b] = f[2].Length > 0 ? f[2] : null;
                bt.Kind[b] = (byte)Math.Max(0, PopulationCatalogue.CategoryOfType(bt.Type[b]));
                bt.Takings[b] = L(f[3]);
                bt.ByClass[b * 3] = L(f[4]);
                bt.ByClass[b * 3 + 1] = L(f[5]);
                bt.ByClass[b * 3 + 2] = L(f[6]);
                bt.Visits[b] = I(f[7]);
                bt.Seen[b] = true;
                if (bt.AnchorDay[b] < 0 && bt.Takings[b] > 0)
                {
                    bt.Anchor[b] = bt.Takings[b];   // this machine's own anchor: the game's count here stands on the day it first hears
                    bt.AnchorDay[b] = mine.Day;
                }
            }
        }

        private static string Find(string[] parts, string tag)
        {
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].StartsWith(tag, StringComparison.Ordinal))
                {
                    return parts[i].Substring(tag.Length);
                }
            }
            return null;
        }

        private static long[] Numbers(string text, char sep, int least)
        {
            string[] f = text.Split(sep);
            if (f.Length < least)
            {
                return null;
            }
            var n = new long[f.Length];
            for (int i = 0; i < f.Length; i++)
            {
                n[i] = L(f[i]);
            }
            return n;
        }

        private static int I(string s) => int.Parse(s, NumberStyles.Integer, Inv);

        private static long L(string s) => long.Parse(s, NumberStyles.Integer, Inv);

        // Ids and types never hold the part's separators; anything that would is taken out, so a line always reads.
        private static string Clean(string s)
        {
            return (s ?? string.Empty).Replace(";", string.Empty).Replace(",", string.Empty).Replace("~", string.Empty);
        }

        private static void Join(StringBuilder t, params int[] values)
        {
            Join(t, '|', values);
        }

        private static void Join(StringBuilder t, char sep, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                t.Append(i > 0 ? sep.ToString() : string.Empty).Append(values[i].ToString(Inv));
            }
        }

        private static void JoinLong(StringBuilder t, params long[] values)
        {
            JoinLongSep(t, '|', values);
        }

        private static void JoinLongSep(StringBuilder t, char sep, params long[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                t.Append(i > 0 ? sep.ToString() : string.Empty).Append(values[i].ToString(Inv));
            }
        }
    }
}
