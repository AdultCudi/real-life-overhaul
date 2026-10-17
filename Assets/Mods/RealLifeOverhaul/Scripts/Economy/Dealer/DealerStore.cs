using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The car dealership's line of the save, under <see cref="Key"/>; game-free. Parts are divided by '^' and start with a
    /// letter and a colon: h the header; y a showroom, followed by its t tallies, p places, c cars, d offers and b books
    /// (today first, then the days kept); m the market, f finance plans, w warranties, s sales and q cars on their way.
    /// Records are divided by ';' and fields by '|', every figure a whole number. A later version adds fields at the end
    /// of a record or parts with new letters, which this reader skips; a version 1 line, without t and q, still reads.
    /// </summary>
    public static class DealerStore
    {
        public const string Key = "reallife:economy_dealer";
        public const string TestKey = "reallife:economy_dealer_before_tests";
        public const int Version = 2;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static DealerState Load(Dictionary<string, string> data, string key, out int version)
        {
            version = 0;
            string text;
            if (data == null || !data.TryGetValue(key, out text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, key, Read(text, out version), version, Version);   // a line that cannot be read is kept as it is
        }

        public static string Write(DealerState s)
        {
            var t = new StringBuilder(512 + s.Yards.Count * 6144 + s.Market.Count * 96 + (s.Plans.Count + s.Covers.Count + s.Deliveries.Count) * 96);
            t.Append("v=").Append(Version).Append("^h:");
            Join(t, s.Seed, s.FirstDay, s.LastHour, s.LastDay, s.NextId, s.MarketDay, s.NoBidsUntil);
            for (int i = 0; i < s.Yards.Count && i < DealerCatalogue.YardsMost; i++)
            {
                DealerYard y = s.Yards[i];
                t.Append("^y:").Append(Clean(y.Place)).Append('|').Append(Clean(y.Name)).Append('|');
                Join(t, y.StaffFloor, y.Finance ? 1 : 0, y.Warranty ? 1 : 0, y.VisitorCarry, y.Extras ? 1 : 0, y.Rep, y.Done, y.Event, y.Feature, y.EventFrom, y.EventUntil);
                for (int k = 0; k < y.EventLast.Length; k++)
                {
                    t.Append('|').Append(y.EventLast[k].ToString(Inv));
                }
                t.Append("^t:");
                Join(t, y.Tally);
                t.Append("^p:");
                for (int p = 0; p < y.Spots.Count && p < DealerCatalogue.SpotsMost; p++)
                {
                    t.Append(p > 0 ? ";" : string.Empty);
                    Join(t, y.Spots[p].X, y.Spots[p].Y, y.Spots[p].Z, y.Spots[p].Yaw);
                }
                t.Append("^c:");
                for (int c = 0; c < y.Cars.Count && c < DealerCatalogue.CarsMost; c++)
                {
                    StockCar car = y.Cars[c];
                    t.Append(c > 0 ? ";" : string.Empty);
                    Join(t, car.Id);
                    t.Append('|').Append(Clean(car.VehicleId)).Append('|').Append(Clean(car.Type)).Append('|').Append(Clean(car.Colour)).Append('|');
                    Join(t, car.Km, car.Condition, car.Faults, car.Known, car.Cost, car.Asking, car.Bought, (int)car.Source, car.Spot, car.OutUntil, car.ShopUntil);
                }
                t.Append("^d:");
                for (int d = 0; d < y.Deals.Count && d < DealerCatalogue.DealsMost; d++)
                {
                    DealerOffer x = y.Deals[d];
                    t.Append(d > 0 ? ";" : string.Empty);
                    Join(t, x.Id, x.Car);
                    t.Append('|').Append(Clean(x.Customer)).Append('|');
                    Join(t, x.Profile, x.Offer, x.Ceiling, x.Counters, x.Made, x.Expires, (int)x.Stage, x.Drove ? 1 : 0, x.WantsDrive ? 1 : 0, x.WantsFinance ? 1 : 0,
                        x.Score, x.Deposit, x.Term, x.Tier);
                    t.Append('|').Append(Clean(x.TradeType)).Append('|').Append(Clean(x.TradeColour)).Append('|');
                    Join(t, x.TradeKm, x.TradeCondition, x.TradeAllowance, x.Extras, x.Lift);
                }
                t.Append("^b:");
                Book(t, y.Today);
                for (int b = 0; b < y.Past.Length && y.Past[b].Day >= 0; b++)
                {
                    t.Append(';');   // the days kept fill the first slots, so the first unused one ends them
                    Book(t, y.Past[b]);
                }
            }
            t.Append("^m:");
            for (int i = 0; i < s.Market.Count && i < 32; i++)
            {
                DealerListing l = s.Market[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, l.Id, (int)l.Kind);
                t.Append('|').Append(Clean(l.Type)).Append('|').Append(Clean(l.Colour)).Append('|');
                Join(t, l.Km, l.Condition, l.Shown, l.Faults, l.Inspected ? 1 : 0, l.Asking, l.Floor, l.Haggles, l.Countered, l.Ends, l.Leader, l.Ceiling, l.Bids, l.Gone ? 1 : 0);
            }
            t.Append("^f:");
            for (int i = 0; i < s.Plans.Count && i < DealerCatalogue.PlansMost; i++)
            {
                DealerPlan p = s.Plans[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, p.Id);
                t.Append('|').Append(Clean(p.Place)).Append('|').Append(Clean(p.Customer)).Append('|').Append(Clean(p.Type)).Append('|');
                Join(t, p.Worth, p.Owed, p.Weekly, p.Due, p.Missed, p.Paid, p.Score);
            }
            t.Append("^w:");
            for (int i = 0; i < s.Covers.Count && i < DealerCatalogue.CoversMost; i++)
            {
                DealerCover c = s.Covers[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, c.Id);
                t.Append('|').Append(Clean(c.Place)).Append('|').Append(Clean(c.Customer)).Append('|').Append(Clean(c.Type)).Append('|');
                Join(t, c.Tier, c.Worth, c.Condition, c.Until, c.Claims, c.Paid, c.RestUntil);
            }
            t.Append("^s:");
            for (int i = 0; i < s.Sales.Count && i < DealerCatalogue.SalesKept; i++)
            {
                DealerSale r = s.Sales[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, r.Day);
                t.Append('|').Append(Clean(r.Place)).Append('|').Append(Clean(r.Customer)).Append('|').Append(Clean(r.Type)).Append('|');
                Join(t, r.Price, r.Cost, r.Financed ? 1 : 0, r.Tier, r.Trade ? 1 : 0, r.Extras);
            }
            t.Append("^q:");
            for (int i = 0; i < s.Deliveries.Count && i < DealerCatalogue.DeliveriesMost; i++)
            {
                DealerDelivery q = s.Deliveries[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, q.Id);
                t.Append('|').Append(Clean(q.Place)).Append('|').Append(Clean(q.Type)).Append('|').Append(Clean(q.Colour)).Append('|');
                Join(t, q.Km, q.Condition, q.Faults, q.Known, q.Cost, q.Due, q.Told ? 1 : 0);
            }
            return t.ToString();
        }

        private static void Book(StringBuilder t, DealerBook b)
        {
            Join(t, b.Day, b.Visitors, b.Served, b.Lost, b.Offers, b.Sold, b.Takings, b.Bought, b.Claims, b.Margin, b.Revenue, b.Extras, b.Spent, b.Cars, b.Age, b.Arrears, b.Late, b.Rep);
        }

        /// <summary>Reads a line. Null for one that is not this feature's at all.</summary>
        public static DealerState Read(string text, out int version)
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
                var s = new DealerState();
                bool header = false;
                DealerYard yard = null;
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
                            string[] c = FirstRecord(records);
                            s.Seed = I(c, 0, 1);
                            s.FirstDay = I(c, 1);
                            s.LastHour = I(c, 2, -1);
                            s.LastDay = I(c, 3, -1);
                            s.NextId = Math.Max(1, I(c, 4, 1));
                            s.MarketDay = I(c, 5, -1);
                            s.NoBidsUntil = I(c, 6, -1);
                            header = true;
                            break;
                        }
                        case 'y':
                        {
                            string[] c = FirstRecord(records);
                            yard = null;
                            if (c.Length < 2 || s.Yards.Count >= DealerCatalogue.YardsMost || string.IsNullOrEmpty(c[0]) || s.Yard(c[0]) != null)
                            {
                                break;
                            }
                            yard = new DealerYard
                            {
                                Place = c[0], Name = c[1], StaffFloor = DealerCatalogue.Clamp(I(c, 2, DealerCatalogue.StaffFloor), DealerCatalogue.StaffFloorLeast, DealerCatalogue.StaffFloorMost),
                                Finance = I(c, 3, 1) != 0, Warranty = I(c, 4, 1) != 0, VisitorCarry = DealerCatalogue.Clamp(I(c, 5), 0, 999), Extras = I(c, 6, 1) != 0,
                                Rep = DealerCatalogue.Clamp(I(c, 7), 0, DealerCatalogue.RepMost), Done = I(c, 8) & ((1 << DealerCatalogue.Missions) - 1),
                                Event = DealerCatalogue.Clamp(I(c, 9, -1), -1, DealerCatalogue.EventKinds - 1), Feature = Math.Max(0, I(c, 10)), EventFrom = I(c, 11), EventUntil = I(c, 12)
                            };
                            for (int k = 0; k < yard.EventLast.Length; k++)
                            {
                                yard.EventLast[k] = I(c, 13 + k, -1);
                            }
                            s.Yards.Add(yard);
                            break;
                        }
                        case 't':
                        {
                            string[] c = FirstRecord(records);
                            for (int k = 0; yard != null && k < yard.Tally.Length; k++)
                            {
                                yard.Tally[k] = Math.Max(0, I(c, k));
                            }
                            break;
                        }
                        case 'p':
                            for (int r = 0; yard != null && r < records.Length && yard.Spots.Count < DealerCatalogue.SpotsMost; r++)
                            {
                                string[] c = records[r].Split('|');
                                if (c.Length >= 4)
                                {
                                    yard.Spots.Add(new LotSpot { X = I(c, 0), Y = I(c, 1), Z = I(c, 2), Yaw = I(c, 3) });
                                }
                            }
                            break;
                        case 'c':
                            for (int r = 0; yard != null && r < records.Length && yard.Cars.Count < DealerCatalogue.CarsMost; r++)
                            {
                                string[] c = records[r].Split('|');
                                if (c.Length < 14 || string.IsNullOrEmpty(c[1]) || string.IsNullOrEmpty(c[2]))
                                {
                                    continue;
                                }
                                yard.Cars.Add(new StockCar
                                {
                                    Id = I(c, 0), VehicleId = c[1], Type = c[2], Colour = NullIfEmpty(c[3]), Km = Math.Max(0, I(c, 4)), Condition = DealerCatalogue.Clamp(I(c, 5, 1000), 0, 1000),
                                    Faults = I(c, 6), Known = I(c, 7), Cost = Math.Max(0, I(c, 8)), Asking = Math.Max(0, I(c, 9)), Bought = I(c, 10),
                                    Source = (CarSource)DealerCatalogue.Clamp(I(c, 11), 0, 3), Spot = I(c, 12, -1), OutUntil = I(c, 13, -1), ShopUntil = I(c, 14, -1)
                                });
                            }
                            break;
                        case 'd':
                            for (int r = 0; yard != null && r < records.Length && yard.Deals.Count < DealerCatalogue.DealsMost; r++)
                            {
                                string[] c = records[r].Split('|');
                                if (c.Length < 22)
                                {
                                    continue;
                                }
                                yard.Deals.Add(new DealerOffer
                                {
                                    Id = I(c, 0), Car = I(c, 1), Customer = c[2], Profile = DealerCatalogue.Clamp(I(c, 3), 0, DealerCatalogue.Profiles - 1), Offer = Math.Max(0, I(c, 4)),
                                    Ceiling = Math.Max(0, I(c, 5)), Counters = I(c, 6), Made = I(c, 7), Expires = I(c, 8), Stage = (OfferStage)DealerCatalogue.Clamp(I(c, 9), 0, 1),
                                    Drove = I(c, 10) != 0, WantsDrive = I(c, 11) != 0, WantsFinance = I(c, 12) != 0, Score = I(c, 13), Deposit = I(c, 14), Term = I(c, 15),
                                    Tier = DealerCatalogue.Clamp(I(c, 16, -1), -1, DealerCatalogue.Tiers - 1), TradeType = NullIfEmpty(c[17]), TradeColour = NullIfEmpty(c[18]),
                                    TradeKm = I(c, 19), TradeCondition = I(c, 20), TradeAllowance = Math.Max(0, I(c, 21)),
                                    Extras = I(c, 22) & ((1 << DealerCatalogue.ExtraKinds) - 1), Lift = DealerCatalogue.Clamp(I(c, 23), 0, 1000)
                                });
                            }
                            break;
                        case 'b':
                            for (int r = 0; yard != null && r < records.Length && r <= yard.Past.Length; r++)
                            {
                                ReadBook(records[r].Split('|'), r == 0 ? yard.Today : yard.Past[r - 1]);
                            }
                            break;
                        case 'm':
                            for (int r = 0; r < records.Length && s.Market.Count < 32; r++)
                            {
                                string[] c = records[r].Split('|');
                                if (c.Length < 18 || string.IsNullOrEmpty(c[2]))
                                {
                                    continue;
                                }
                                s.Market.Add(new DealerListing
                                {
                                    Id = I(c, 0), Kind = (ListingKind)DealerCatalogue.Clamp(I(c, 1), 0, 2), Type = c[2], Colour = NullIfEmpty(c[3]), Km = Math.Max(0, I(c, 4)),
                                    Condition = DealerCatalogue.Clamp(I(c, 5, 1000), 0, 1000), Shown = DealerCatalogue.Clamp(I(c, 6, 1000), 0, 1000), Faults = I(c, 7), Inspected = I(c, 8) != 0,
                                    Asking = Math.Max(0, I(c, 9)), Floor = Math.Max(0, I(c, 10)), Haggles = I(c, 11), Countered = Math.Max(0, I(c, 12)), Ends = I(c, 13, -1),
                                    Leader = I(c, 14, -1), Ceiling = Math.Max(0, I(c, 15)), Bids = I(c, 16), Gone = I(c, 17) != 0
                                });
                            }
                            break;
                        case 'f':
                            for (int r = 0; r < records.Length && s.Plans.Count < DealerCatalogue.PlansMost; r++)
                            {
                                string[] c = records[r].Split('|');
                                if (c.Length < 11)
                                {
                                    continue;
                                }
                                s.Plans.Add(new DealerPlan
                                {
                                    Id = I(c, 0), Place = c[1], Customer = c[2], Type = c[3], Worth = Math.Max(0, I(c, 4)), Owed = Math.Max(0, I(c, 5)), Weekly = Math.Max(0, I(c, 6)),
                                    Due = I(c, 7), Missed = Math.Max(0, I(c, 8)), Paid = Math.Max(0, I(c, 9)), Score = I(c, 10)
                                });
                            }
                            break;
                        case 'w':
                            for (int r = 0; r < records.Length && s.Covers.Count < DealerCatalogue.CoversMost; r++)
                            {
                                string[] c = records[r].Split('|');
                                if (c.Length < 11)
                                {
                                    continue;
                                }
                                s.Covers.Add(new DealerCover
                                {
                                    Id = I(c, 0), Place = c[1], Customer = c[2], Type = c[3], Tier = DealerCatalogue.Clamp(I(c, 4), 0, DealerCatalogue.Tiers - 1), Worth = Math.Max(0, I(c, 5)),
                                    Condition = DealerCatalogue.Clamp(I(c, 6, 1000), 0, 1000), Until = I(c, 7), Claims = Math.Max(0, I(c, 8)), Paid = Math.Max(0, I(c, 9)), RestUntil = I(c, 10, -1)
                                });
                            }
                            break;
                        case 's':
                            for (int r = 0; r < records.Length && s.Sales.Count < DealerCatalogue.SalesKept; r++)
                            {
                                string[] c = records[r].Split('|');
                                if (c.Length < 9)
                                {
                                    continue;
                                }
                                s.Sales.Add(new DealerSale
                                {
                                    Day = I(c, 0), Place = c[1], Customer = c[2], Type = c[3], Price = I(c, 4), Cost = I(c, 5), Financed = I(c, 6) != 0,
                                    Tier = DealerCatalogue.Clamp(I(c, 7, -1), -1, DealerCatalogue.Tiers - 1), Trade = I(c, 8) != 0, Extras = I(c, 9) & ((1 << DealerCatalogue.ExtraKinds) - 1)
                                });
                            }
                            break;
                        case 'q':
                            for (int r = 0; r < records.Length && s.Deliveries.Count < DealerCatalogue.DeliveriesMost; r++)
                            {
                                string[] c = records[r].Split('|');
                                if (c.Length < 11 || string.IsNullOrEmpty(c[1]) || string.IsNullOrEmpty(c[2]))
                                {
                                    continue;
                                }
                                s.Deliveries.Add(new DealerDelivery
                                {
                                    Id = I(c, 0), Place = c[1], Type = c[2], Colour = NullIfEmpty(c[3]), Km = Math.Max(0, I(c, 4)), Condition = DealerCatalogue.Clamp(I(c, 5, 1000), 0, 1000),
                                    Faults = I(c, 6), Known = I(c, 7), Cost = Math.Max(0, I(c, 8)), Due = I(c, 9), Told = I(c, 10) != 0
                                });
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

        private static string[] FirstRecord(string[] records)
        {
            return records.Length > 0 ? records[0].Split('|') : Array.Empty<string>();
        }

        private static void ReadBook(string[] c, DealerBook b)
        {
            b.Day = I(c, 0, -1);
            b.Visitors = I(c, 1);
            b.Served = I(c, 2);
            b.Lost = I(c, 3);
            b.Offers = I(c, 4);
            b.Sold = I(c, 5);
            b.Takings = I(c, 6);
            b.Bought = I(c, 7);
            b.Claims = I(c, 8);
            b.Margin = I(c, 9);
            b.Revenue = I(c, 10);
            b.Extras = I(c, 11);
            b.Spent = I(c, 12);
            b.Cars = Math.Max(0, I(c, 13));
            b.Age = Math.Max(0, I(c, 14));
            b.Arrears = Math.Max(0, I(c, 15));
            b.Late = Math.Max(0, I(c, 16));
            b.Rep = DealerCatalogue.Clamp(I(c, 17), 0, DealerCatalogue.RepMost);
        }

        private static string NullIfEmpty(string s)
        {
            return string.IsNullOrEmpty(s) ? null : s;
        }

        private static int I(string[] c, int at, int fallback = 0)
        {
            int v;
            return at < c.Length && int.TryParse(c[at], NumberStyles.Integer, Inv, out v) ? v : fallback;
        }

        private static void Join(StringBuilder t, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                t.Append(i > 0 ? "|" : string.Empty).Append(values[i].ToString(Inv));
            }
        }

        /// <summary>A text as the line keeps it: none of the line's own marks in it.</summary>
        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            if (s.IndexOfAny(Marks) < 0)
            {
                return s;
            }
            var t = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                t.Append(Array.IndexOf(Marks, s[i]) >= 0 ? ' ' : s[i]);
            }
            return t.ToString();
        }

        private static readonly char[] Marks = { '|', ';', '^', '\n', '\r' };
    }
}
