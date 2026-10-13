using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The cleaning service and selling a business in the save: one line of the save's modData, under <see cref="Key"/>.
    /// Game-free.
    ///
    ///     v=1^h:...^k:...^c:...^o:...^l:...^f:...^d:...^n:...^r:...
    ///
    /// Parts are divided by '^' and begin with a letter and a colon; records inside a part by ';', fields by '|'.
    ///   h  the header: seed|first day|last day|next id|van|last contract offer day|metres driven today|
    ///      seven days of van cost|seven days of contract income
    ///   k  the crew: an employee's id each
    ///   c  the clients: place|name|district|outside|x|z|mode|three hours|threshold|need a day|worst tile at the
    ///      last morning|cleaned today|minutes today|minutes the day before|cleanliness at the last morning|last
    ///      visit day|its minute|its cleaner|then, for a contract: dirt|dirt a day|m2|capacity|fee|standard|week
    ///      from|days under|weeks missed|earned
    ///   o  contract offers: place|name|district|m2|capacity|x|z|fee|standard|until
    ///   l  businesses on the market: place|name|listed day|asking step|asking|value|next offer day|offers made
    ///   f  offers for them: id|place|buyer|price|ceiling|keeps staff|parts|clause days|deadline|countered|face
    ///   d  parts of a price still to come: name|buyer|part|left|next day
    ///   n  clauses: type|district|name|from|until|price
    ///   r  sales completed: day|name|price|gain
    ///
    /// Every figure is a whole number, so what is read back is exactly what was written. A NEWER VERSION adds fields at
    /// the end of a record or a part with a new letter; this reader takes the fields it knows and skips parts it does
    /// not, so a save written by a later version still loads. A figure out of its bounds is put inside them; a line
    /// that cannot be read is left alone and the feature starts afresh. The lists are capped when written. Nothing
    /// here is the game's: a save loads in the plain game with this line ignored.
    /// </summary>
    public static class ServicesStore
    {
        public const string Key = "reallife:economy_services";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The state of this save; null if the save has none or it cannot be read.</summary>
        public static ServicesState Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            string text;
            if (data == null || !data.TryGetValue(Key, out text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, Key, Read(text, out version), version, Version);   // (a line that cannot be read is kept as it is: Core/SaveLines)
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
                Join(t, l.ListedDay, l.Step, l.Asking, l.Value, l.NextOfferDay, l.OffersMade);
            }
            t.Append("^f:");
            for (int i = Math.Max(0, s.SaleOffers.Count - ServicesCatalogue.OffersKept), n = 0; i < s.SaleOffers.Count; i++, n++)
            {
                SaleOffer o = s.SaleOffers[i];
                t.Append(n > 0 ? ";" : string.Empty);
                Join(t, o.Id);
                t.Append('|').Append(Clean(o.Place)).Append('|');
                Join(t, o.Buyer, o.Price, o.Ceiling, o.KeepsStaff ? 1 : 0, o.Parts, o.ClauseDays, o.Deadline, o.Countered ? 1 : 0, o.Face);
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
            return t.ToString();
        }

        /// <summary>Reads a line. Null for one that is not this feature's at all.</summary>
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
                                        Place = c[0], Name = c[1], ListedDay = Math.Max(0, I(c, 2)), Step = ServicesCatalogue.Clamp(I(c, 3, ServicesCatalogue.AskingDefault), 0, ServicesCatalogue.AskingPct.Length - 1),
                                        Asking = Math.Max(0, I(c, 4)), Value = Math.Max(0, I(c, 5)), NextOfferDay = Math.Max(0, I(c, 6)), OffersMade = Math.Max(0, I(c, 7))
                                    });
                                }
                            }
                            break;
                        case 'f':
                            foreach (string record in records)
                            {
                                string[] c = record.Split('|');
                                int buyer = I(c, 2, -1);
                                if (c.Length >= 11 && c[1].Length > 0 && buyer >= 0 && buyer < (int)BuyerKind.Count && s.SaleOffers.Count < ServicesCatalogue.OffersKept && s.SaleOffer(I(c, 0)) == null)
                                {
                                    int price = Math.Max(0, I(c, 3));
                                    s.SaleOffers.Add(new SaleOffer
                                    {
                                        Id = Math.Max(1, I(c, 0, 1)), Place = c[1], Buyer = buyer, Price = price, Ceiling = Math.Max(price, I(c, 4)), KeepsStaff = I(c, 5) != 0,
                                        Parts = ServicesCatalogue.Clamp(I(c, 6), 0, 12), ClauseDays = Math.Max(0, I(c, 7)), Deadline = Math.Max(0, I(c, 8)), Countered = I(c, 9) != 0, Face = Math.Max(0, I(c, 10))
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
                                    s.Dues.Add(new SaleDue { Name = c[0], Buyer = buyer, Part = Math.Max(0, I(c, 2)), Left = ServicesCatalogue.Clamp(I(c, 3), 0, 12), NextDay = Math.Max(0, I(c, 4)) });
                                }
                            }
                            break;
                        case 'n':
                            foreach (string record in records)
                            {
                                string[] c = record.Split('|');
                                if (c.Length >= 6 && c[0].Length > 0 && s.Clauses.Count < ServicesCatalogue.ClausesMost)
                                {
                                    s.Clauses.Add(new SaleClause { Type = c[0], District = c[1], Name = c[2], FromDay = Math.Max(0, I(c, 3)), Until = Math.Max(0, I(c, 4)), Price = Math.Max(0, I(c, 5)) });
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

        private static void Join(StringBuilder t, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                t.Append(i > 0 ? "|" : string.Empty).Append(values[i].ToString(Inv));
            }
        }

        private static int I(string[] c, int at, int fallback = 0)
        {
            int value;
            return at < c.Length && int.TryParse(c[at], NumberStyles.Integer, Inv, out value) ? value : fallback;
        }

        /// <summary>A text with the three dividers taken out, so that it can never break the line apart.</summary>
        public static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace('|', ' ').Replace(';', ' ').Replace('^', ' ');
        }
    }
}
