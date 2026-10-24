using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>
    /// The Marketplace's line of the save, under <see cref="Key"/>; game-free. Parts are divided by '^' and start with a letter
    /// and a colon: h the header, r the ratings, l the day's listings, o the player's things, b the buyers' offers, s the
    /// sales kept and g the visits. Records are divided by ';' and fields by '|', every figure a whole number. A later
    /// version adds fields at the end of a record or parts with new letters, which this reader skips.
    /// </summary>
    public static class HustleStore
    {
        public const string Key = "reallife:hustles";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly char[] Marks = { '|', ';', '^', '\n', '\r' };

        public static HustleState Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, Key, Read(text, out version), version, Version);   // a line that cannot be read is kept as it is
        }

        public static string Write(HustleState s)
        {
            var t = new StringBuilder(512 + (s.Listings.Count + s.Items.Count + s.Gigs.Count) * 96);
            t.Append("v=").Append(Version).Append("^h:");
            Join(t, s.Seed, s.Day, s.LastHour, s.NextId, s.RepairXp, s.Earned, s.Profit, s.JobsDone);
            t.Append("^r:");
            for (int i = 0; i < s.Ratings.Count; i++)
            {
                t.Append(i > 0 ? ";" : string.Empty).Append(s.Ratings[i].ToString(Inv));
            }
            t.Append("^l:");
            for (int i = 0; i < s.Listings.Count && i < 32; i++)
            {
                Listing l = s.Listings[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, l.Id, l.Car ? 1 : 0);
                Text(t, l.Thing, l.Colour);
                Join(t, l.Km, l.Price, l.Condition, l.Shown, l.Asking, l.Floor, l.Haggles, l.Countered, l.Number, l.Gone ? 1 : 0);
                Text(t, l.Seller, l.Street);
            }
            t.Append("^o:");
            for (int i = 0; i < s.Items.Count && i < HustleCatalogue.OwnedMost; i++)
            {
                Owned o = s.Items[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, o.Id, o.Car ? 1 : 0);
                Text(t, o.Thing, o.Colour);
                Join(t, o.Km, o.Price, o.Condition, o.Paid, o.Spent, (int)o.State, (int)o.Plan, o.Number, o.Due, o.Asking, o.ListedDay, o.Cleaned ? 1 : 0, o.Bought, o.Test ? 1 : 0);
                Text(t, o.Street, o.Seller);
                t.Append(Clean(o.VehicleId));
            }
            t.Append("^b:");
            for (int i = 0; i < s.Offers.Count && i < HustleCatalogue.OffersMost; i++)
            {
                BuyerOffer d = s.Offers[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, d.Id, d.Item, d.Offer, d.Ceiling, d.Counters, d.Expires);
                t.Append('|').Append(Clean(d.Buyer));
            }
            t.Append("^s:");
            for (int i = 0; i < s.Sales.Count && i < HustleCatalogue.SalesKept; i++)
            {
                Sale r = s.Sales[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, r.Car ? 1 : 0, r.Price, r.Cost, r.Day);
                Text(t, r.Thing, r.Buyer);
            }
            t.Append("^g:");
            for (int i = 0; i < s.Gigs.Count && i < HustleCatalogue.GigsMost; i++)
            {
                Gig g = s.Gigs[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, g.Id, (int)g.Kind, g.Number, g.Number2, g.Pay, g.From, g.Until, (int)g.Stage, g.HelperFee, g.HelperDone, g.HelperRating, g.Rating, g.Day,
                    g.Late ? 1 : 0, g.Test ? 1 : 0);
                Text(t, g.Client, g.Street);
                t.Append(Clean(g.Street2));
            }
            return t.ToString();
        }

        /// <summary>Reads a line. Null for one that is not this feature's at all.</summary>
        public static HustleState Read(string text, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(text) || !text.StartsWith("v=", StringComparison.Ordinal))
            {
                return null;
            }
            try
            {
                string[] parts = text.Split('^');
                if (!int.TryParse(parts[0].Substring(2), NumberStyles.Integer, Inv, out version))
                {
                    return null;
                }
                var s = new HustleState();
                bool header = false;
                for (int i = 1; i < parts.Length; i++)
                {
                    string part = parts[i];
                    if (part.Length < 2 || part[1] != ':')
                    {
                        continue;
                    }
                    string body = part.Substring(2);
                    string[] records = body.Length == 0 ? Array.Empty<string>() : body.Split(';');
                    switch (part[0])
                    {
                        case 'h':
                        {
                            string[] c = records.Length > 0 ? records[0].Split('|') : Array.Empty<string>();
                            s.Seed = Math.Max(1, I(c, 0, 1));
                            s.Day = I(c, 1, -1);
                            s.LastHour = I(c, 2, -1);
                            s.NextId = Math.Max(1, I(c, 3, 1));
                            s.RepairXp = HustleCatalogue.Clamp(I(c, 4), 0, HustleCatalogue.XpMost);
                            s.Earned = I(c, 5);
                            s.Profit = I(c, 6);
                            s.JobsDone = Math.Max(0, I(c, 7));
                            header = true;
                            break;
                        }
                        case 'r':
                            for (int r = 0; r < records.Length && s.Ratings.Count < HustleCatalogue.RatingsKept; r++)
                            {
                                if (int.TryParse(records[r], NumberStyles.Integer, Inv, out int stars) && stars >= 1 && stars <= 5)
                                {
                                    s.Ratings.Add(stars);
                                }
                            }
                            break;
                        case 'l':
                            for (int r = 0; r < records.Length && s.Listings.Count < 32; r++)
                            {
                                string[] c = records[r].Split('|');
                                if (c.Length < 16 || string.IsNullOrEmpty(c[2]))
                                {
                                    continue;
                                }
                                s.Listings.Add(new Listing
                                {
                                    Id = I(c, 0), Car = I(c, 1) != 0, Thing = c[2], Colour = NullIfEmpty(c[3]), Km = Math.Max(0, I(c, 4)), Price = Math.Max(0, I(c, 5)),
                                    Condition = HustleCatalogue.Clamp(I(c, 6, 50), 1, 100), Shown = HustleCatalogue.Clamp(I(c, 7, 50), 1, 100), Asking = Math.Max(1, I(c, 8, 1)),
                                    Floor = Math.Max(1, I(c, 9, 1)), Haggles = Math.Max(0, I(c, 10)), Countered = Math.Max(0, I(c, 11)), Number = I(c, 12), Gone = I(c, 13) != 0,
                                    Seller = c[14], Street = c[15]
                                });
                            }
                            break;
                        case 'o':
                            for (int r = 0; r < records.Length && s.Items.Count < HustleCatalogue.OwnedMost; r++)
                            {
                                string[] c = records[r].Split('|');
                                if (c.Length < 21 || string.IsNullOrEmpty(c[2]))
                                {
                                    continue;
                                }
                                s.Items.Add(new Owned
                                {
                                    Id = I(c, 0), Car = I(c, 1) != 0, Thing = c[2], Colour = NullIfEmpty(c[3]), Km = Math.Max(0, I(c, 4)), Price = Math.Max(0, I(c, 5)),
                                    Condition = HustleCatalogue.Clamp(I(c, 6, 50), 1, 100), Paid = Math.Max(0, I(c, 7)), Spent = Math.Max(0, I(c, 8)),
                                    State = (OwnedState)HustleCatalogue.Clamp(I(c, 9), 0, 3), Plan = (OwnedPlan)HustleCatalogue.Clamp(I(c, 10), 0, 3), Number = I(c, 11), Due = I(c, 12),
                                    Asking = Math.Max(0, I(c, 13)), ListedDay = I(c, 14), Cleaned = I(c, 15) != 0, Bought = I(c, 16), Test = I(c, 17) != 0,
                                    Street = c[18], Seller = c[19], VehicleId = NullIfEmpty(c[20])
                                });
                            }
                            break;
                        case 'b':
                            for (int r = 0; r < records.Length && s.Offers.Count < HustleCatalogue.OffersMost; r++)
                            {
                                string[] c = records[r].Split('|');
                                if (c.Length < 7)
                                {
                                    continue;
                                }
                                s.Offers.Add(new BuyerOffer
                                {
                                    Id = I(c, 0), Item = I(c, 1), Offer = Math.Max(1, I(c, 2, 1)), Ceiling = Math.Max(1, I(c, 3, 1)), Counters = Math.Max(0, I(c, 4)), Expires = I(c, 5),
                                    Buyer = c[6]
                                });
                            }
                            break;
                        case 's':
                            for (int r = 0; r < records.Length && s.Sales.Count < HustleCatalogue.SalesKept; r++)
                            {
                                string[] c = records[r].Split('|');
                                if (c.Length < 6 || string.IsNullOrEmpty(c[4]))
                                {
                                    continue;
                                }
                                s.Sales.Add(new Sale { Car = I(c, 0) != 0, Price = I(c, 1), Cost = I(c, 2), Day = I(c, 3), Thing = c[4], Buyer = c[5] });
                            }
                            break;
                        case 'g':
                            for (int r = 0; r < records.Length && s.Gigs.Count < HustleCatalogue.GigsMost; r++)
                            {
                                string[] c = records[r].Split('|');
                                if (c.Length < 18 || string.IsNullOrEmpty(c[16]))
                                {
                                    continue;
                                }
                                s.Gigs.Add(new Gig
                                {
                                    Id = I(c, 0), Kind = (GigKind)HustleCatalogue.Clamp(I(c, 1), 0, HustleCatalogue.Gigs.Length - 1), Number = I(c, 2), Number2 = I(c, 3),
                                    Pay = Math.Max(0, I(c, 4)), From = I(c, 5), Until = I(c, 6), Stage = (GigStage)HustleCatalogue.Clamp(I(c, 7), 0, 6), HelperFee = Math.Max(0, I(c, 8)),
                                    HelperDone = I(c, 9), HelperRating = HustleCatalogue.Clamp(I(c, 10), 0, 5), Rating = HustleCatalogue.Clamp(I(c, 11), 0, 5), Day = I(c, 12),
                                    Late = I(c, 13) != 0, Test = I(c, 14) != 0, Client = c[15], Street = c[16], Street2 = NullIfEmpty(c[17])
                                });
                            }
                            break;
                    }
                }
                return header ? s : null;
            }
            catch (Exception)
            {
                return null;   // not a line this feature wrote
            }
        }

        private static int I(string[] c, int at, int fallback = 0)
        {
            return at < c.Length && int.TryParse(c[at], NumberStyles.Integer, Inv, out int v) ? v : fallback;
        }

        private static string NullIfEmpty(string s)
        {
            return string.IsNullOrEmpty(s) ? null : s;
        }

        private static void Join(StringBuilder t, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                t.Append(i > 0 ? "|" : string.Empty).Append(values[i].ToString(Inv));
            }
        }

        // Two texts after the figures already written: "|a|b|", so the next figures follow on.
        private static void Text(StringBuilder t, string a, string b)
        {
            t.Append('|').Append(Clean(a)).Append('|').Append(Clean(b)).Append('|');
        }

        /// <summary>A text as the line keeps it: none of the line's own marks in it, and no longer than a name may be.</summary>
        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            if (s.IndexOfAny(Marks) < 0 && s.Length <= 64)
            {
                return s;
            }
            var t = new StringBuilder(Math.Min(s.Length, 64));
            for (int i = 0; i < s.Length && t.Length < 64; i++)
            {
                t.Append(Array.IndexOf(Marks, s[i]) >= 0 ? ' ' : s[i]);
            }
            return t.ToString();
        }
    }
}
