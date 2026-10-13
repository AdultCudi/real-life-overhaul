using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The online shops in the save: one line of the save's modData, under <see cref="Key"/>. Game-free.
    ///
    ///     v=1^h:...^d:...^v:...^s:...^l:...^o:...^r:...^b:...^s:...
    ///
    /// Parts are divided by '^' and begin with a letter and a colon; records inside a part by ';', fields by '|'.
    ///   h  the header: seed|first day|last hour worked through|next order number
    ///   d  the districts by the game's names, in the order the orders count them
    ///   v  the vans: driver|free at (minute)|minutes driven|on day
    ///   s  one site: place|name|quality|way|promise|built in-house|opened|hosting due|offline|stars|seven closing
    ///      ratings|advertising from|end|per cent|last ended|idle hours|refunds not yet in the game's statement
    ///   l  its catalogue: product|price|offered|units|takings|cost|missed|seven days' units|seven days' missed|
    ///      takings not yet in the statement|cost not yet in it
    ///   o  its open orders: number|district|placed|due|arrives|stage|products|total|tilt|short, then for each product
    ///      its place in the catalogue|asked|picked|price of one|cost
    ///   r  its returns on their way: day|product|units|refund|cost of one
    ///   b  its books, today's first and then the week's seven: day|orders|picked|units|takings|goods|delivery|
    ///      refunds|hosting|on time|late|short|cancelled|missing|returned|turned away
    /// The l, o, r and b parts after an s part are that site's.
    ///
    /// Every figure is a whole number, so what is read back is exactly what was written. A NEWER VERSION adds fields at
    /// the end of a record or a part with a new letter; this reader takes the fields it knows and skips parts it does
    /// not, so a save written by a later version still loads. A figure out of its bounds is put inside them; a line
    /// that cannot be read is left alone and the feature starts afresh. Nothing here is the game's: a save loads in the
    /// plain game with this line ignored, and a save made without the feature simply has no line.
    ///
    /// <see cref="TestKey"/> holds the same line as it was before the first test tool changed it, for as long as test
    /// changes are in place.
    /// </summary>
    public static class OnlineStore
    {
        public const string Key = "reallife:economy_online";
        public const string TestKey = "reallife:economy_online_before_tests";
        public const int Version = 1;

        private const int Most = 100000000;     // cents in a day's figure, kept inside what an int adds up safely
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The state of this save; null if the save has none or it cannot be read.</summary>
        public static OnlineState Load(Dictionary<string, string> data, string key, out int version)
        {
            version = 0;
            string text;
            if (data == null || !data.TryGetValue(key, out text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, key, Read(text, out version), version, Version);   // (a line that cannot be read is kept as it is: Core/SaveLines)
        }

        public static string Write(OnlineState s)
        {
            var t = new StringBuilder(256 + s.Shops.Count * 1024);
            t.Append("v=").Append(Version).Append("^h:");
            Join(t, s.Seed, s.FirstDay, s.LastHour, s.NextOrder);
            t.Append("^d:");
            for (int i = 0; i < s.Districts.Count; i++)
            {
                t.Append(i > 0 ? ";" : string.Empty).Append(Clean(s.Districts[i]));
            }
            t.Append("^v:");
            for (int i = 0; i < s.Vans.Count && i < OnlineCatalogue.VansMost; i++)
            {
                Van v = s.Vans[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(Clean(v.Driver)).Append('|');
                Join(t, v.FreeAt, v.Used, v.Day);
            }
            for (int i = 0; i < s.Shops.Count && i < OnlineCatalogue.ShopsMost; i++)
            {
                WebShop shop = s.Shops[i];
                t.Append("^s:").Append(Clean(shop.Place)).Append('|').Append(Clean(shop.Name)).Append('|');
                Join(t, shop.Quality, shop.Way, shop.Promise, shop.InHouse ? 1 : 0, shop.OpenDay, shop.HostDue, shop.Offline ? 1 : 0, shop.Stars);
                t.Append('|');
                Join(t, shop.StarLog);
                t.Append('|');
                Join(t, shop.AdFrom, shop.AdEnd, shop.AdPercent, shop.AdEnded, shop.Idle, shop.BookRefunds);
                t.Append("^l:");
                for (int l = 0; l < shop.Lines.Count && l < OnlineCatalogue.LinesMost; l++)
                {
                    WebLine line = shop.Lines[l];
                    t.Append(l > 0 ? ";" : string.Empty).Append(Clean(line.Item)).Append('|');
                    Join(t, line.Price, line.On ? 1 : 0, line.Units, line.Takings, line.Cost, line.Missed);
                    t.Append('|');
                    Join(t, line.WeekUnits);
                    t.Append('|');
                    Join(t, line.WeekMissed);
                    t.Append('|');
                    Join(t, line.BookTakings, line.BookCost);
                }
                t.Append("^o:");
                for (int o = 0; o < shop.Orders.Count && o < OnlineCatalogue.OpenMost; o++)
                {
                    WebOrder order = shop.Orders[o];
                    t.Append(o > 0 ? ";" : string.Empty);
                    Join(t, order.Id, order.District, order.Placed, order.Due, order.Arrive, (int)order.Stage, order.Count, order.Total, order.Tilt, order.Short ? 1 : 0);
                    for (int j = 0; j < order.Count; j++)
                    {
                        t.Append('|');
                        Join(t, order.Line[j], order.Qty[j], order.Got[j], order.Unit[j], order.Paid[j]);
                    }
                }
                t.Append("^r:");
                for (int r = 0; r < shop.Returns.Count && r < OnlineCatalogue.ReturnsMost; r++)
                {
                    WebReturn back = shop.Returns[r];
                    t.Append(r > 0 ? ";" : string.Empty);
                    Join(t, back.Day);
                    t.Append('|').Append(Clean(back.Item)).Append('|');
                    Join(t, back.Qty, back.Refund, back.Cost);
                }
                t.Append("^b:");
                Book(t, shop.Today);
                for (int b = 0; b < shop.Week.Length; b++)
                {
                    t.Append(';');
                    Book(t, shop.Week[b]);
                }
            }
            return t.ToString();
        }

        private static void Book(StringBuilder t, DayBook b)
        {
            Join(t, b.Day, b.Orders, b.Picked, b.Units, b.Takings, b.Goods, b.Delivery, b.Refunds, b.Hosting, b.OnTime, b.Late, b.Short, b.Cancelled, b.Missing, b.Returned, b.Turned);
        }

        /// <summary>Reads a line. Null for one that is not this feature's at all.</summary>
        public static OnlineState Read(string text, out int version)
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
                var s = new OnlineState();
                bool header = false;
                WebShop shop = null;
                for (int p = 1; p < parts.Length; p++)
                {
                    string part = parts[p];
                    if (part.Length < 2 || part[1] != ':')
                    {
                        continue;
                    }
                    string body = part.Substring(2);
                    switch (part[0])
                    {
                        case 'h':
                        {
                            string[] c = body.Split('|');
                            s.Seed = I(c, 0);
                            s.FirstDay = Math.Max(0, I(c, 1));
                            s.LastHour = Math.Max(-1, I(c, 2, -1));
                            s.NextOrder = OnlineCatalogue.Clamp(I(c, 3, 1), 1, 99999999);
                            header = true;
                            break;
                        }
                        case 'd':
                            foreach (string name in Records(body))
                            {
                                if (name.Length > 0 && s.Districts.Count < 64 && s.District(name) < 0)
                                {
                                    s.Districts.Add(name);
                                }
                            }
                            break;
                        case 'v':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                if (c[0].Length > 0 && s.Vans.Count < OnlineCatalogue.VansMost && s.VanOf(c[0]) == null)
                                {
                                    s.Vans.Add(new Van { Driver = c[0], FreeAt = Math.Max(0, I(c, 1)), Used = OnlineCatalogue.Clamp(I(c, 2), 0, 24 * 60), Day = Math.Max(0, I(c, 3)) });
                                }
                            }
                            break;
                        case 's':
                        {
                            string[] c = body.Split('|');
                            shop = null;
                            if (c[0].Length > 0 && s.Shops.Count < OnlineCatalogue.ShopsMost && s.Shop(c[0]) == null)
                            {
                                shop = Shop(c);
                                s.Shops.Add(shop);
                            }
                            break;
                        }
                        case 'l':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                if (shop != null && c[0].Length > 0 && shop.Lines.Count < OnlineCatalogue.LinesMost && shop.Line(c[0]) == null)
                                {
                                    shop.Lines.Add(Line(c));
                                }
                            }
                            break;
                        case 'o':
                            foreach (string record in Records(body))
                            {
                                WebOrder order = shop != null && shop.Orders.Count < OnlineCatalogue.OpenMost ? Order(record.Split('|'), s.Districts.Count, shop.Lines.Count) : null;
                                if (order != null)
                                {
                                    shop.Orders.Add(order);
                                }
                            }
                            break;
                        case 'r':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                if (shop != null && c.Length >= 5 && c[1].Length > 0 && shop.Returns.Count < OnlineCatalogue.ReturnsMost && I(c, 2) > 0)
                                {
                                    shop.Returns.Add(new WebReturn { Day = Math.Max(0, I(c, 0)), Item = c[1], Qty = OnlineCatalogue.Clamp(I(c, 2), 1, 9), Refund = OnlineCatalogue.Clamp(I(c, 3), 0, Most), Cost = OnlineCatalogue.Clamp(I(c, 4), 0, Most) });
                                }
                            }
                            break;
                        case 'b':
                        {
                            string[] records = Records(body);
                            for (int b = 0; shop != null && b < records.Length && b <= shop.Week.Length; b++)
                            {
                                Book(b == 0 ? shop.Today : shop.Week[b - 1], records[b].Split('|'));
                            }
                            break;
                        }
                    }
                }
                return header ? s : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static WebShop Shop(string[] c)
        {
            var shop = new WebShop
            {
                Place = c[0], Name = c.Length > 1 ? c[1] : string.Empty, Quality = OnlineCatalogue.Quality(I(c, 2)), Way = OnlineCatalogue.Clamp(I(c, 3, OnlineCatalogue.Courier), 0, OnlineCatalogue.Ways - 1),
                Promise = OnlineCatalogue.Clamp(I(c, 4, 1), 0, 1), InHouse = I(c, 5) != 0, OpenDay = Math.Max(0, I(c, 6)), HostDue = Math.Max(0, I(c, 7)), Offline = I(c, 8) != 0,
                Stars = OnlineCatalogue.Clamp(I(c, 9, OnlineCatalogue.StarsStart), OnlineCatalogue.StarsLeast, OnlineCatalogue.StarsMost)
            };
            for (int k = 0; k < shop.StarLog.Length; k++)
            {
                shop.StarLog[k] = OnlineCatalogue.Clamp(I(c, 10 + k), 0, OnlineCatalogue.StarsMost);
            }
            shop.AdFrom = Math.Max(0, I(c, 17));
            shop.AdEnd = Math.Max(0, I(c, 18));
            shop.AdPercent = OnlineCatalogue.Clamp(I(c, 19), 0, OnlineCatalogue.AdMost);
            shop.AdEnded = Math.Max(0, I(c, 20));
            shop.Idle = Math.Max(0, I(c, 21));
            shop.BookRefunds = OnlineCatalogue.Clamp(I(c, 22), 0, Most);
            return shop;
        }

        private static WebLine Line(string[] c)
        {
            var line = new WebLine
            {
                Item = c[0], Price = OnlineCatalogue.Clamp(I(c, 1), 0, Most), On = I(c, 2, 1) != 0, Units = Math.Max(0, I(c, 3)), Takings = OnlineCatalogue.Clamp(I(c, 4), 0, Most),
                Cost = OnlineCatalogue.Clamp(I(c, 5), -Most, Most), Missed = Math.Max(0, I(c, 6))
            };
            for (int k = 0; k < 7; k++)
            {
                line.WeekUnits[k] = Math.Max(0, I(c, 7 + k));
                line.WeekMissed[k] = Math.Max(0, I(c, 14 + k));
            }
            line.BookTakings = OnlineCatalogue.Clamp(I(c, 21), 0, Most);
            line.BookCost = OnlineCatalogue.Clamp(I(c, 22), -Most, Most);
            return line;
        }

        // An order whose district or stage the save does not know is not kept; a product of it that is no longer in the catalogue is.
        private static WebOrder Order(string[] c, int districts, int lines)
        {
            int stage = I(c, 5, -1), count = I(c, 6), district = I(c, 1, -1);
            if (c.Length < 10 || stage < 0 || stage > (int)OrderStage.Out || count < 1 || count > OnlineCatalogue.OrderLines || district < 0 || district >= Math.Max(1, districts))
            {
                return null;
            }
            WebOrder o = OnlineRules.NewOrder();
            o.Id = Math.Max(1, I(c, 0, 1));
            o.District = district;
            o.Placed = Math.Max(0, I(c, 2));
            o.Due = Math.Max(0, I(c, 3));
            o.Arrive = Math.Max(0, I(c, 4));
            o.Stage = (OrderStage)stage;
            o.Count = count;
            o.Total = OnlineCatalogue.Clamp(I(c, 7), 0, Most);
            o.Tilt = Math.Max(0, I(c, 8));
            o.Short = I(c, 9) != 0;
            for (int j = 0; j < count; j++)
            {
                int at = 10 + j * 5, line = I(c, at, -1);
                o.Line[j] = line >= 0 && line < lines ? line : -1;
                o.Qty[j] = OnlineCatalogue.Clamp(I(c, at + 1, 1), 1, 9);
                o.Got[j] = OnlineCatalogue.Clamp(I(c, at + 2), 0, o.Qty[j]);
                o.Unit[j] = OnlineCatalogue.Clamp(I(c, at + 3), 0, Most);
                o.Paid[j] = OnlineCatalogue.Clamp(I(c, at + 4), 0, Most);
            }
            return o;
        }

        private static void Book(DayBook b, string[] c)
        {
            b.Day = Math.Max(0, I(c, 0));
            b.Orders = Math.Max(0, I(c, 1));
            b.Picked = Math.Max(0, I(c, 2));
            b.Units = Math.Max(0, I(c, 3));
            b.Takings = OnlineCatalogue.Clamp(I(c, 4), 0, Most);
            b.Goods = OnlineCatalogue.Clamp(I(c, 5), -Most, Most);
            b.Delivery = OnlineCatalogue.Clamp(I(c, 6), 0, Most);
            b.Refunds = OnlineCatalogue.Clamp(I(c, 7), 0, Most);
            b.Hosting = OnlineCatalogue.Clamp(I(c, 8), 0, Most);
            b.OnTime = Math.Max(0, I(c, 9));
            b.Late = Math.Max(0, I(c, 10));
            b.Short = Math.Max(0, I(c, 11));
            b.Cancelled = Math.Max(0, I(c, 12));
            b.Missing = Math.Max(0, I(c, 13));
            b.Returned = Math.Max(0, I(c, 14));
            b.Turned = Math.Max(0, I(c, 15));
        }

        private static string[] Records(string body)
        {
            return body.Length == 0 ? Array.Empty<string>() : body.Split(';');
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
