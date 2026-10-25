using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>
    /// The utilities' save line: "v=2~h:" the days, "~a:" the accounts, "~n:" the broadband company, "~g:" the grace. A
    /// line that cannot be read gives no state; figures out of range are brought within it; later fields read as unset.
    /// </summary>
    public static class UtilitiesStore
    {
        public const string Key = "reallife:utilities";
        public const int Version = 2;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The state of this save; null if the save has none or it cannot be read.</summary>
        public static UtilitiesState Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, Key, Read(text, out version), version, Version);   // a line that cannot be read is kept as it is
        }

        public static string Write(UtilitiesState s)
        {
            var t = new StringBuilder(64 + s.Accounts.Count * 64);
            t.Append("v=").Append(Version).Append("~h:").Append(s.LastDay.ToString(Inv)).Append('|').Append(s.MonthStart.ToString(Inv))
                .Append('|').Append(s.Seed.ToString(Inv)).Append('|').Append(s.Month.ToString(Inv));
            for (int p = 0; p < s.Promo.Length; p++)
            {
                t.Append('|').Append(s.Promo[p].ToString(Inv));
            }
            t.Append('|').Append(s.SpeedMet.ToString(Inv));   // after the promotions: a line from before it reads 0
            t.Append("~a:");
            bool first = true;
            for (int i = 0; i < s.Accounts.Count; i++)
            {
                Account a = s.Accounts[i];
                if (a == null || string.IsNullOrEmpty(a.Place))
                {
                    continue;
                }
                t.Append(first ? string.Empty : ";").Append(Escape(a.Place));
                first = false;
                int[] figures =
                {
                    (int)a.Service, (int)a.Kind, (int)a.State, a.Owned ? 1 : 0, a.AutoDebit ? 1 : 0, a.OpenedDay, a.ConnectDay, a.TemporaryUntil,
                    a.Deposit, a.Accrued, a.Units, a.Owed, a.DueDay, a.LateCharged ? 1 : 0, a.LastBill, a.LastBillDay, a.OnTime, a.CutForDebt ? 1 : 0,
                    a.Provider, a.Plan, a.BusinessPlan ? 1 : 0, a.ContractUntil, a.PriceCents, a.OutageFrom, a.OutageUntil
                };
                for (int f = 0; f < figures.Length; f++)
                {
                    t.Append('|').Append(figures[f].ToString(Inv));
                }
            }
            Network n = s.Network;
            if (n.Exists)
            {
                t.Append("~n:").Append((int)n.State);
                long[] head =
                {
                    n.FoundedDay, n.SoldDay, n.SalePrice, n.PriceShare, n.Maintenance, n.Marketing, n.MonthTakings, n.MonthCosts, n.LastTakings, n.LastCosts,
                    n.LastConnections, n.LastGained, n.HistoryCount
                };
                for (int f = 0; f < head.Length; f++)
                {
                    t.Append('|').Append(head[f].ToString(Inv));
                }
                for (int d = 0; d < Network.Districts; d++)
                {
                    t.Append('|').Append(n.Built[d].ToString(Inv)).Append('|').Append(n.Spent[d].ToString(Inv));
                    for (int p = 0; p < Network.Providers; p++)
                    {
                        t.Append('|').Append(n.Share[d, p].ToString(Inv));
                    }
                }
                for (int h = 0; h < n.History.Length; h++)
                {
                    t.Append('|').Append(n.History[h].ToString(Inv));
                }
                t.Append('|').Append(n.ScaleFrom.ToString(Inv));
                for (int d = 0; d < Network.Districts; d++)
                {
                    t.Append('|').Append(n.Scaled[d] ? 1 : 0);
                }
                for (int k = 0; k < BroadbandCatalogue.CrewKinds; k++)
                {
                    t.Append('|').Append(n.Crew[k].ToString(Inv));
                }
                t.Append('|').Append(n.Owed.ToString(Inv));
            }
            if (s.GraceUntil > 0 && s.Grace.Count > 0)
            {
                t.Append("~g:").Append(s.GraceUntil.ToString(Inv));
                for (int g = 0; g < s.Grace.Count; g++)
                {
                    t.Append('|').Append(Escape(s.Grace[g]));
                }
            }
            return t.ToString();
        }

        // the company: its state, settings and books, each district's build, cost and shares, then its months
        private static void ReadNetwork(string[] c, Network n)
        {
            n.State = (CompanyState)Clamp(I(c, 0), 0, (int)CompanyState.Sold);
            n.FoundedDay = Math.Max(0, I(c, 1));
            n.SoldDay = Math.Max(0, I(c, 2));
            n.SalePrice = Math.Max(0L, L(c, 3));
            n.PriceShare = Clamp(I(c, 4), BroadbandCatalogue.PriceLeast, BroadbandCatalogue.PriceMost);
            n.Maintenance = Clamp(I(c, 5), BroadbandCatalogue.MaintenanceLeast, BroadbandCatalogue.MaintenanceMost);
            n.Marketing = Clamp(I(c, 6), 0, BroadbandCatalogue.Marketing.Length - 1);
            n.MonthTakings = Math.Max(0, I(c, 7));
            n.MonthCosts = Math.Max(0, I(c, 8));
            n.LastTakings = Math.Max(0, I(c, 9));
            n.LastCosts = Math.Max(0, I(c, 10));
            n.LastConnections = Math.Max(0, I(c, 11));
            n.LastGained = I(c, 12);
            n.HistoryCount = Clamp(I(c, 13), 0, n.History.Length);
            int at = 14;
            for (int d = 0; d < Network.Districts; d++)
            {
                n.Built[d] = I(c, at++);
                n.Spent[d] = Math.Max(0, I(c, at++));
                int sum = 0;
                for (int p = 0; p < Network.Providers; p++)
                {
                    n.Share[d, p] = Clamp(I(c, at++), 0, 1000);
                    sum += n.Share[d, p];
                }
                if (sum > 1000)
                {
                    for (int p = 0; p < Network.Providers; p++)
                    {
                        n.Share[d, p] = n.Share[d, p] * 1000 / sum;   // a line edited by hand: brought back within the whole
                    }
                }
            }
            for (int h = 0; h < n.History.Length; h++)
            {
                n.History[h] = I(c, at++);
            }
            n.ScaleFrom = at < c.Length ? I(c, at) : EndScaleRules.Unset;
            at++;
            for (int d = 0; d < Network.Districts; d++)
            {
                n.Scaled[d] = I(c, at++) != 0;
            }
            for (int k = 0; k < BroadbandCatalogue.CrewKinds; k++)
            {
                n.Crew[k] = Math.Max(0, I(c, at++));
            }
            n.Owed = Math.Max(0L, L(c, at++));
        }

        public static UtilitiesState Read(string text, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(text) || !text.StartsWith("v=", StringComparison.Ordinal))
            {
                return null;
            }
            string[] sections = text.Split('~');
            if (!int.TryParse(sections[0].Substring(2), NumberStyles.Integer, Inv, out version) || version < 1)
            {
                return null;
            }
            var s = new UtilitiesState();
            for (int i = 1; i < sections.Length; i++)
            {
                string section = sections[i];
                if (section.StartsWith("h:", StringComparison.Ordinal))
                {
                    string[] c = section.Substring(2).Split('|');
                    s.LastDay = Math.Max(0, I(c, 0));
                    s.MonthStart = Math.Max(0, I(c, 1));
                    s.Seed = I(c, 2);
                    s.Month = Math.Max(0, I(c, 3));
                    for (int p = 0; p < s.Promo.Length; p++)
                    {
                        int promo = I(c, 4 + p);
                        s.Promo[p] = promo <= 0 ? 1000 : Clamp(promo, InternetCatalogue.PromoLeast, InternetCatalogue.PromoMost);   // none in a line from before the internet
                    }
                    s.SpeedMet = Math.Max(0, I(c, 4 + s.Promo.Length));
                }
                else if (section.StartsWith("n:", StringComparison.Ordinal))
                {
                    ReadNetwork(section.Substring(2).Split('|'), s.Network);
                }
                else if (section.StartsWith("g:", StringComparison.Ordinal))
                {
                    string[] c = section.Substring(2).Split('|');
                    for (int g = 1; g < c.Length; g++)
                    {
                        string place = Unescape(c[g]);
                        if (!string.IsNullOrEmpty(place) && !s.Grace.Contains(place))
                        {
                            s.Grace.Add(place);
                        }
                    }
                    s.GraceUntil = s.Grace.Count > 0 ? Math.Max(0, I(c, 0)) : 0;
                }
                else if (section.StartsWith("a:", StringComparison.Ordinal) && section.Length > 2)
                {
                    foreach (string record in section.Substring(2).Split(';'))
                    {
                        Account a = Account(record);
                        if (a != null && s.Find(a.Place, a.Service) == null)
                        {
                            s.Accounts.Add(a);
                        }
                    }
                }
            }
            return s;
        }

        private static Account Account(string record)
        {
            string[] c = record.Split('|');
            if (c.Length < 18)
            {
                return null;
            }
            string place = Unescape(c[0]);
            if (string.IsNullOrEmpty(place))
            {
                return null;
            }
            return new Account
            {
                Place = place,
                Service = (Service)Clamp(I(c, 1), 0, UtilitiesCatalogue.AllServices - 1),
                Kind = (PlaceKind)Clamp(I(c, 2), 0, (int)PlaceKind.Industry),
                State = (SupplyState)Clamp(I(c, 3), 0, (int)SupplyState.Closed),
                Owned = I(c, 4) != 0,
                AutoDebit = I(c, 5) != 0,
                OpenedDay = Math.Max(0, I(c, 6)),
                ConnectDay = Math.Max(0, I(c, 7)),
                TemporaryUntil = Math.Max(0, I(c, 8)),
                Deposit = Math.Max(0, I(c, 9)),
                Accrued = Math.Max(0, I(c, 10)),
                Units = Math.Max(0, I(c, 11)),
                Owed = Math.Max(0, I(c, 12)),
                DueDay = Math.Max(0, I(c, 13)),
                LateCharged = I(c, 14) != 0,
                LastBill = Math.Max(0, I(c, 15)),
                LastBillDay = Math.Max(0, I(c, 16)),
                OnTime = Math.Max(0, I(c, 17)),
                CutForDebt = I(c, 18) != 0,   // none in a line from before it was kept
                Provider = c.Length > 19 ? Clamp(I(c, 19), -1, InternetCatalogue.OwnIndex) : -1,
                Plan = Math.Max(0, I(c, 20)),
                BusinessPlan = I(c, 21) != 0,
                ContractUntil = Math.Max(0, I(c, 22)),
                PriceCents = Math.Max(0, I(c, 23)),
                OutageFrom = Math.Max(0, I(c, 24)),
                OutageUntil = Math.Max(0, I(c, 25))
            };
        }

        /// <summary>A place written so that none of the line's dividers (~ | ;) can be in it; read back exactly.</summary>
        public static string Escape(string place)
        {
            return Uri.EscapeDataString(place ?? string.Empty).Replace("~", "%7E");
        }

        public static string Unescape(string text)
        {
            try
            {
                return Uri.UnescapeDataString(text ?? string.Empty);
            }
            catch (UriFormatException)
            {
                return null;
            }
        }

        private static long L(string[] c, int at)
        {
            return at < c.Length && long.TryParse(c[at], NumberStyles.Integer, Inv, out long value) ? value : 0;
        }

        private static int I(string[] c, int at)
        {
            return at < c.Length && int.TryParse(c[at], NumberStyles.Integer, Inv, out int value) ? value : 0;
        }

        private static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }
    }
}
