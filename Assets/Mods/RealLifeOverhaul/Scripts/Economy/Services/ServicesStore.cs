using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The state as one line of the save's modData, game-free: parts divided by '^', each a letter and a colon, records by
    /// ';' and fields by '|', as <see cref="Write"/> writes them. A later version adds fields at the end or new parts,
    /// which this reader skips; a figure out of bounds is clamped.
    /// </summary>
    public static class ServicesStore
    {
        public const string Key = "reallife:economy_services";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The state of this save; null if it has none or it cannot be read (the line is then kept as it is).</summary>
        public static ServicesState Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, Key, Read(text, out version), version, Version);
        }

        public static string Write(ServicesState s)
        {
            var t = new StringBuilder(256 + s.Clients.Count * 120 + s.SaleOffers.Count * 60);
            t.Append("v=").Append(Version).Append("^h:");
            Join(t, s.Seed, s.FirstDay, s.LastDay, s.NextId, s.Van ? 1 : 0, s.LastOfferDay, s.VanMetres);
            for (int i = 0; i < 7; i++)
            {
                t.Append('|').Append(s.VanCost[i].ToString(Inv));
            }
            for (int i = 0; i < 7; i++)
            {
                t.Append('|').Append(s.FeeIncome[i].ToString(Inv));
            }
            t.Append('|').Append(s.RivalSaleDay.ToString(Inv));
            t.Append("^k:");
            for (int i = 0; i < s.Crew.Count && i < ServicesCatalogue.CrewMost; i++)
            {
                t.Append(i > 0 ? ";" : string.Empty).Append(Clean(s.Crew[i]));
            }
            t.Append("^c:");
            for (int i = 0; i < s.Clients.Count && i < ServicesCatalogue.ClientsMost; i++)
            {
                CleaningClient c = s.Clients[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(Clean(c.Place)).Append('|').Append(Clean(c.Name)).Append('|').Append(Clean(c.District)).Append('|');
                Join(t, c.Outside ? 1 : 0, c.X, c.Z, c.Mode, c.Hours[0], c.Hours[1], c.Hours[2], c.Threshold, c.NeedDay, c.WorstStart, c.CleanedToday, c.MinutesToday, c.MinutesBefore, c.CleanBefore,
                    c.LastDay, c.LastMinute);
                t.Append('|').Append(Clean(c.LastCleaner)).Append('|');
                Join(t, c.Dirt, c.DirtDay, c.Sqm, c.Capacity, c.Fee, c.Standard, c.WeekFrom, c.DaysUnder, c.Missed, c.Earned);
            }
            t.Append("^o:");
            for (int i = 0; i < s.Offers.Count && i < ServicesCatalogue.OffersMost; i++)
            {
                CleaningOffer o = s.Offers[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(Clean(o.Place)).Append('|').Append(Clean(o.Name)).Append('|').Append(Clean(o.District)).Append('|');
                Join(t, o.Sqm, o.Capacity, o.X, o.Z, o.Fee, o.Standard, o.Until);
            }
            t.Append("^l:");
            for (int i = 0; i < s.Listings.Count && i < ServicesCatalogue.ListingsMost; i++)
            {
                SaleListing l = s.Listings[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(Clean(l.Place)).Append('|').Append(Clean(l.Name)).Append('|');
                Join(t, l.ListedDay, l.Step, l.Asking, l.Value, l.NextOfferDay, l.OffersMade, l.Lift);
            }
            t.Append("^f:");
            for (int i = Math.Max(0, s.SaleOffers.Count - ServicesCatalogue.OffersKept), n = 0; i < s.SaleOffers.Count; i++, n++)
            {
                SaleOffer o = s.SaleOffers[i];
                t.Append(n > 0 ? ";" : string.Empty);
                Join(t, o.Id);
                t.Append('|').Append(Clean(o.Place)).Append('|');
                Join(t, o.Buyer, o.Price, o.Ceiling, o.KeepsStaff ? 1 : 0, o.Parts, o.ClauseDays, o.Deadline, o.Countered ? 1 : 0, o.Face);
                t.Append('|').Append(Clean(o.Who)).Append('|').Append(Clean(o.WhoName)).Append('|');
                Join(t, o.WhoCount, o.Lift);
            }
            t.Append("^d:");
            for (int i = Math.Max(0, s.Dues.Count - ServicesCatalogue.DuesMost), n = 0; i < s.Dues.Count; i++, n++)
            {
                SaleDue d = s.Dues[i];
                t.Append(n > 0 ? ";" : string.Empty).Append(Clean(d.Name)).Append('|');
                Join(t, d.Buyer, d.Part, d.Left, d.NextDay);
            }
            t.Append("^n:");
            for (int i = Math.Max(0, s.Clauses.Count - ServicesCatalogue.ClausesMost), n = 0; i < s.Clauses.Count; i++, n++)
            {
                SaleClause c = s.Clauses[i];
                t.Append(n > 0 ? ";" : string.Empty).Append(Clean(c.Type)).Append('|').Append(Clean(c.District)).Append('|').Append(Clean(c.Name)).Append('|');
                Join(t, c.FromDay, c.Until, c.Price);
            }
            t.Append("^r:");
            for (int i = Math.Max(0, s.Records.Count - ServicesCatalogue.RecordsKept), n = 0; i < s.Records.Count; i++, n++)
            {
                SaleRecord r = s.Records[i];
                t.Append(n > 0 ? ";" : string.Empty);
                Join(t, r.Day);
                t.Append('|').Append(Clean(r.Name)).Append('|');
                Join(t, r.Price, r.Gain);
            }
            t.Append("^v:");
            for (int i = Math.Max(0, s.RivalSales.Count - ServicesCatalogue.RivalSalesMost), n = 0; i < s.RivalSales.Count; i++, n++)
            {
                RivalSale v = s.RivalSales[i];
                t.Append(n > 0 ? ";" : string.Empty);
                Join(t, v.Id);
                t.Append('|').Append(Clean(v.Place)).Append('|').Append(Clean(v.Business)).Append('|').Append(Clean(v.Seller)).Append('|').Append(Clean(v.SellerName))
                    .Append('|').Append(Clean(v.Buyer)).Append('|').Append(Clean(v.BuyerName)).Append('|');
                Join(t, v.Price, v.Weekly, v.Day, v.Deadline);
            }
            WriteMore(t, s);
            return t.ToString();
        }

        /// <summary>Reads a line; null for one that is not this feature's or cannot be read.</summary>
        public static ServicesState Read(string text, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(text) || !text.StartsWith("v=", StringComparison.Ordinal))
            {
                return null;
            }
            try
            {
                string[] parts = text.Split('^');
                if (!int.TryParse(parts[0].Substring(2), NumberStyles.Integer, Inv, out version) || version < 1)
                {
                    return null;
                }
                var s = new ServicesState();
                bool header = false;
                for (int p = 1; p < parts.Length; p++)
                {
                    string part = parts[p];
                    if (part.Length < 2 || part[1] != ':')
                    {
                        continue;
                    }
                    string[] records = part.Length == 2 ? Array.Empty<string>() : part.Substring(2).Split(';');
                    switch (part[0])
                    {
                        case 'h':
                        {
                            string[] c = part.Substring(2).Split('|');
                            s.Seed = I(c, 0);
                            s.FirstDay = Math.Max(0, I(c, 1));
                            s.LastDay = Math.Max(0, I(c, 2));
                            s.NextId = Math.Max(1, I(c, 3, 1));
                            s.Van = I(c, 4) != 0;
                            s.LastOfferDay = Math.Max(0, I(c, 5));
                            s.VanMetres = Math.Max(0, I(c, 6));
                            for (int i = 0; i < 7; i++)
                            {
                                s.VanCost[i] = Math.Max(0, I(c, 7 + i));
                                s.FeeIncome[i] = I(c, 14 + i);
                            }
                            s.RivalSaleDay = Math.Max(0, I(c, 21));
                            header = true;
                            break;
                        }
                        case 'k':
                            foreach (string record in records)
                            {
                                if (record.Length > 0 && s.Crew.Count < ServicesCatalogue.CrewMost && !s.Crew.Contains(record))
                                {
                                    s.Crew.Add(record);
                                }
                            }
                            break;
                        case 'c':
                            foreach (string record in records)
                            {
                                string[] c = record.Split('|');
                                if (c.Length >= 7 && c[0].Length > 0 && s.Clients.Count < ServicesCatalogue.ClientsMost && s.Client(c[0]) == null)
                                {
                                    s.Clients.Add(Client(c));
                                }
                            }
                            break;
                        case 'o':
                            foreach (string record in records)
                            {
                                string[] c = record.Split('|');
                                if (c.Length >= 10 && c[0].Length > 0 && s.Offers.Count < ServicesCatalogue.OffersMost && s.Offer(c[0]) == null)
                                {
                                    s.Offers.Add(new CleaningOffer
                                    {
                                        Place = c[0], Name = c[1], District = c[2], Sqm = Math.Max(0, I(c, 3)), Capacity = Math.Max(0, I(c, 4)), X = I(c, 5), Z = I(c, 6),
                                        Fee = Math.Max(0, I(c, 7)), Standard = ServicesCatalogue.Clamp(I(c, 8, 80), 0, 100), Until = Math.Max(0, I(c, 9))
                                    });
                                }
                            }
                            break;
                        case 'l':
                            foreach (string record in records)
                            {
                                string[] c = record.Split('|');
                                if (c.Length >= 8 && c[0].Length > 0 && s.Listings.Count < ServicesCatalogue.ListingsMost && s.Listing(c[0]) == null)
                                {
                                    s.Listings.Add(new SaleListing
                                    {
                                        Place = c[0], Name = c[1], ListedDay = Math.Max(0, I(c, 2)),
                                        Step = ServicesCatalogue.Clamp(I(c, 3, ServicesCatalogue.AskingDefault), 0, ServicesCatalogue.AskingPct.Length - 1),
                                        Asking = Math.Max(0, I(c, 4)), Value = Math.Max(0, I(c, 5)), NextOfferDay = Math.Max(0, I(c, 6)), OffersMade = Math.Max(0, I(c, 7)),
                                        Lift = Math.Max(0, I(c, 8))
                                    });
                                }
                            }
                            break;
                        case 'f':
                            foreach (string record in records)
                            {
                                string[] c = record.Split('|');
                                int buyer = I(c, 2, -1);
                                if (c.Length >= 11 && c[1].Length > 0 && buyer >= 0 && buyer < (int)BuyerKind.Count && s.SaleOffers.Count < ServicesCatalogue.OffersKept
                                    && s.SaleOffer(I(c, 0)) == null)
                                {
                                    int price = Math.Max(0, I(c, 3));
                                    s.SaleOffers.Add(new SaleOffer
                                    {
                                        Id = Math.Max(1, I(c, 0, 1)), Place = c[1], Buyer = buyer, Price = price, Ceiling = Math.Max(price, I(c, 4)), KeepsStaff = I(c, 5) != 0,
                                        Parts = ServicesCatalogue.Clamp(I(c, 6), 0, 12), ClauseDays = Math.Max(0, I(c, 7)), Deadline = Math.Max(0, I(c, 8)), Countered = I(c, 9) != 0,
                                        Face = Math.Max(0, I(c, 10)), Who = S(c, 11), WhoName = S(c, 12), WhoCount = Math.Max(0, I(c, 13)), Lift = Math.Max(0, I(c, 14))
                                    });
                                }
                            }
                            break;
                        case 'd':
                            foreach (string record in records)
                            {
                                string[] c = record.Split('|');
                                int buyer = I(c, 1, -1);
                                if (c.Length >= 5 && buyer >= 0 && buyer < (int)BuyerKind.Count && s.Dues.Count < ServicesCatalogue.DuesMost)
                                {
                                    s.Dues.Add(new SaleDue
                                    {
                                        Name = c[0], Buyer = buyer, Part = Math.Max(0, I(c, 2)), Left = ServicesCatalogue.Clamp(I(c, 3), 0, 12), NextDay = Math.Max(0, I(c, 4))
                                    });
                                }
                            }
                            break;
                        case 'n':
                            foreach (string record in records)
                            {
                                string[] c = record.Split('|');
                                if (c.Length >= 6 && c[0].Length > 0 && s.Clauses.Count < ServicesCatalogue.ClausesMost)
                                {
                                    s.Clauses.Add(new SaleClause
                                    {
                                        Type = c[0], District = c[1], Name = c[2], FromDay = Math.Max(0, I(c, 3)), Until = Math.Max(0, I(c, 4)), Price = Math.Max(0, I(c, 5))
                                    });
                                }
                            }
                            break;
                        case 'g':
                        case 'm':
                        case 'b':
                        case 'q':
                        case 'w':
                            ReadMore(s, part[0], records, part);
                            break;
                        case 'v':
                            foreach (string record in records)
                            {
                                string[] c = record.Split('|');
                                if (c.Length >= 11 && c[1].Length > 0 && c[3].Length > 0 && s.RivalSales.Count < ServicesCatalogue.RivalSalesMost && s.RivalSale(I(c, 0)) == null)
                                {
                                    s.RivalSales.Add(new RivalSale
                                    {
                                        Id = Math.Max(1, I(c, 0, 1)), Place = c[1], Business = c[2], Seller = c[3], SellerName = c[4], Buyer = c[5], BuyerName = c[6],
                                        Price = Math.Max(0, I(c, 7)), Weekly = Math.Max(0, I(c, 8)), Day = Math.Max(0, I(c, 9)), Deadline = Math.Max(0, I(c, 10))
                                    });
                                }
                            }
                            break;
                        case 'r':
                            foreach (string record in records)
                            {
                                string[] c = record.Split('|');
                                if (c.Length >= 4 && s.Records.Count < ServicesCatalogue.RecordsKept)
                                {
                                    s.Records.Add(new SaleRecord { Day = Math.Max(0, I(c, 0)), Name = c[1], Price = Math.Max(0, I(c, 2)), Gain = Math.Max(0, I(c, 3)) });
                                }
                            }
                            break;
                    }
                }
                return header ? s : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static CleaningClient Client(string[] c)
        {
            var client = new CleaningClient
            {
                Place = c[0], Name = c[1], District = c[2], Outside = I(c, 3) != 0, X = I(c, 4), Z = I(c, 5),
                Mode = ServicesCatalogue.Clamp(I(c, 6), 0, ServicesCatalogue.VisitsMost),
                Threshold = ServicesCatalogue.Clamp(I(c, 10, ServicesCatalogue.ThresholdDefault), 0, 100),
                NeedDay = Math.Max(-1, I(c, 11, -1)), WorstStart = ServicesCatalogue.Clamp(I(c, 12), 0, ServicesCatalogue.DirtMost), CleanedToday = Math.Max(0, I(c, 13)),
                MinutesToday = Math.Max(0, I(c, 14)), MinutesBefore = Math.Max(0, I(c, 15)), CleanBefore = ServicesCatalogue.Clamp(I(c, 16, -1), -1, 100),
                LastDay = Math.Max(0, I(c, 17)), LastMinute = ServicesCatalogue.Clamp(I(c, 18), 0, 24 * 60), LastCleaner = c.Length > 19 ? c[19] : string.Empty,
                Dirt = ServicesCatalogue.Clamp(I(c, 20), 0, ServicesCatalogue.DirtMost), DirtDay = ServicesCatalogue.Clamp(I(c, 21), 0, ServicesCatalogue.DirtMost),
                Sqm = Math.Max(0, I(c, 22)), Capacity = Math.Max(0, I(c, 23)), Fee = Math.Max(0, I(c, 24)), Standard = ServicesCatalogue.Clamp(I(c, 25), 0, 100),
                WeekFrom = Math.Max(0, I(c, 26)), DaysUnder = Math.Max(0, I(c, 27)), Missed = Math.Max(0, I(c, 28)), Earned = Math.Max(0, I(c, 29))
            };
            for (int k = 0; k < client.Hours.Length; k++)
            {
                client.Hours[k] = ServicesCatalogue.Clamp(I(c, 7 + k, ServicesCatalogue.HoursDefault[k]), 0, 23);
            }
            if (client.Outside)
            {
                client.Force = CleaningRules.Force(client.Sqm);
            }
            return client;
        }

        // The parts a later version added, each its own letter so an older reader skips them and an older save has none: the named crews, the fees contracted by month and the
        // contracts rivals took, the rivals' bids on the open offers, the crew each contract has, and the worth of each business by day.
        private static void WriteMore(StringBuilder t, ServicesState s)
        {
            t.Append("^g:");
            for (int i = 0; i < s.Places.Count && i < ServicesCatalogue.CrewMost; i++)
            {
                t.Append(i > 0 ? ";" : string.Empty).Append(Clean(s.Places[i].Id)).Append('|');
                Join(t, s.Places[i].Group);
            }
            t.Append("^m:");
            Join(t, s.FeeMonth, s.FeeNoted, s.RivalWon);
            for (int i = 0; i < s.FeeMonths.Length; i++)
            {
                t.Append('|').Append(s.FeeMonths[i].ToString(Inv));
            }
            t.Append("^b:");
            int n = 0;
            for (int i = 0; i < s.Offers.Count && i < ServicesCatalogue.OffersMost; i++)
            {
                CleaningOffer o = s.Offers[i];
                if (o.Rivals.Count == 0)
                {
                    continue;
                }
                t.Append(n++ > 0 ? ";" : string.Empty).Append(Clean(o.Place));
                for (int r = 0; r < o.Rivals.Count && r < ServicesCatalogue.RivalBidsMost; r++)
                {
                    t.Append('|');
                    Triple(t, o.Rivals[r].Firm, o.Rivals[r].Pct, o.Rivals[r].Skill);
                }
            }
            t.Append("^q:");
            n = 0;
            for (int i = 0; i < s.Clients.Count && i < ServicesCatalogue.ClientsMost; i++)
            {
                if (s.Clients[i].Crew >= 0)
                {
                    t.Append(n++ > 0 ? ";" : string.Empty).Append(Clean(s.Clients[i].Place)).Append('|');
                    Join(t, s.Clients[i].Crew);
                }
            }
            t.Append("^w:");
            for (int i = 0; i < s.Worths.Count && i < ServicesCatalogue.ListingsMost; i++)
            {
                WorthRing w = s.Worths[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(Clean(w.Place));
                for (int d = 0; d < w.Day.Length; d++)
                {
                    if (w.Day[d] > 0)
                    {
                        t.Append('|');
                        Triple(t, d, w.Day[d], w.Value[d]);
                    }
                }
            }
        }

        private static void ReadMore(ServicesState s, char kind, string[] records, string part)
        {
            switch (kind)
            {
                case 'g':
                    foreach (string record in records)
                    {
                        string[] c = record.Split('|');
                        int group = I(c, 1, -1);
                        if (c.Length >= 2 && c[0].Length > 0 && group >= 0 && group < ServicesCatalogue.CrewGroups && s.Places.Count < ServicesCatalogue.CrewMost && s.GroupOf(c[0]) < 0)
                        {
                            s.Places.Add(new CrewPlace { Id = c[0], Group = group });
                        }
                    }
                    break;
                case 'm':
                {
                    string[] c = part.Substring(2).Split('|');
                    s.FeeMonth = Math.Max(-1, I(c, 0, -1));
                    s.FeeNoted = ServicesCatalogue.Clamp(I(c, 1), 0, ServicesCatalogue.FeeMonths);
                    s.RivalWon = Math.Max(0, I(c, 2));
                    for (int i = 0; i < s.FeeMonths.Length; i++)
                    {
                        s.FeeMonths[i] = Math.Max(0, I(c, 3 + i));
                    }
                    break;
                }
                case 'b':
                    foreach (string record in records)
                    {
                        string[] c = record.Split('|');
                        CleaningOffer o = c.Length > 1 ? s.Offer(c[0]) : null;
                        for (int r = 1; o != null && r < c.Length && o.Rivals.Count < ServicesCatalogue.RivalBidsMost; r++)
                        {
                            string[] b = c[r].Split(',');
                            o.Rivals.Add(new RivalBid
                            {
                                Firm = ServicesCatalogue.Clamp(I(b, 0), 0, ServicesCatalogue.RivalFirms.Length - 1), Pct = ServicesCatalogue.Clamp(I(b, 1, 100), 50, 150),
                                Skill = ServicesCatalogue.Clamp(I(b, 2, 50), 0, 100)
                            });
                        }
                    }
                    break;
                case 'q':
                    foreach (string record in records)
                    {
                        string[] c = record.Split('|');
                        CleaningClient client = c.Length >= 2 ? s.Client(c[0]) : null;
                        if (client != null)
                        {
                            client.Crew = ServicesCatalogue.Clamp(I(c, 1, -1), -1, ServicesCatalogue.CrewGroups - 1);
                        }
                    }
                    break;
                case 'w':
                    foreach (string record in records)
                    {
                        string[] c = record.Split('|');
                        if (c[0].Length == 0 || s.Worths.Count >= ServicesCatalogue.ListingsMost)
                        {
                            continue;
                        }
                        var ring = new WorthRing { Place = c[0] };
                        for (int i = 1; i < c.Length; i++)
                        {
                            string[] x = c[i].Split(',');
                            int slot = I(x, 0, -1);
                            if (slot >= 0 && slot < ring.Day.Length)
                            {
                                ring.Day[slot] = Math.Max(0, I(x, 1));
                                ring.Value[slot] = Math.Max(0, I(x, 2));
                            }
                        }
                        s.Worths.Add(ring);
                    }
                    break;
            }
        }

        private static void Triple(StringBuilder t, int a, int b, int c) => t.Append(a.ToString(Inv)).Append(',').Append(b.ToString(Inv)).Append(',').Append(c.ToString(Inv));

        private static void Join(StringBuilder t, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                t.Append(i > 0 ? "|" : string.Empty).Append(values[i].ToString(Inv));
            }
        }

        private static string S(string[] c, int at) => at < c.Length ? c[at] : string.Empty;

        private static int I(string[] c, int at, int fallback = 0)
        {
            return at < c.Length && int.TryParse(c[at], NumberStyles.Integer, Inv, out int value) ? value : fallback;
        }

        /// <summary>A text with the line's three dividers taken out, so that it can never break the line apart.</summary>
        public static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace('|', ' ').Replace(';', ' ').Replace('^', ' ');
        }
    }
}
