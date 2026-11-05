using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>An order that is over, kept for the Delivered and Returned lists: a bounded ring of the last few.</summary>
    public sealed class FinishedOrder
    {
        public int Day, Person, Items, Total, District;   // the person is a seed for the shared people pool's name; Total in cents
        public bool Late, Returned;
    }

    /// <summary>What the owner set for one product on the site: the price it was before it was lowered, its own title and its description.</summary>
    public sealed class LineExtra
    {
        public string Item = string.Empty, Title = string.Empty, Desc = string.Empty;
        public int WasCents;
    }

    /// <summary>A customer review of a product: who (a person of the shared pool), how many stars, where, when, and the answer of the owner if there is one.</summary>
    public sealed class ReviewNote
    {
        public string Item = string.Empty, Reply = string.Empty;
        public int Person, Stars, Day, District;
    }

    /// <summary>What a web shop is set to, beyond the old settings, and what it has kept: the customer-paid delivery fee, the area, the packing time,
    /// night orders and prices that follow rivals.</summary>
    public sealed class ShopExtra
    {
        public const int SalesDays = 30, DoneMost = 40;
        public const int Packing30 = 0, Packing60 = 1, PackingDay = 2;

        public string Place = string.Empty;
        public int FeeCents;                 // charged to the customer for delivery
        public int FreeOverCents;            // an order of this much or more is delivered free; 0: never free
        public int Packing = PackingDay;     // how long customers are told packing takes
        public bool Night;                   // orders are taken between 22:00 and 06:00 and held until the morning
        public bool Match;                   // a line with no price of its own follows the lowest rival price
        public bool ShowStock = true;        // sold out lines are shown as such
        public int DistrictMask;             // bit i set: the district i of the state's list is served; 0: all of them
        public int Reviews;                  // orders that were rated
        public int LastDay;
        public readonly int[] Sales = new int[SalesDays];   // takings in cents by the day's number mod 30
        public readonly List<FinishedOrder> Done = new List<FinishedOrder>(DoneMost);
        public const int NotesMost = 60;
        public readonly List<LineExtra> Lines = new List<LineExtra>(8);
        public readonly List<ReviewNote> Notes = new List<ReviewNote>(NotesMost);

        public LineExtra LineOf(string item, bool make)
        {
            for (int i = 0; i < Lines.Count; i++)
            {
                if (string.Equals(Lines[i].Item, item, StringComparison.Ordinal))
                {
                    return Lines[i];
                }
            }
            if (!make)
            {
                return null;
            }
            var line = new LineExtra { Item = item ?? string.Empty };
            Lines.Add(line);
            return line;
        }

        public bool Serves(int district) => DistrictMask == 0 || (district >= 0 && district < 31 && (DistrictMask & (1 << district)) != 0);
    }

    /// <summary>Every web shop's extras in the save (one line, its own key) so an old save loads as it was.</summary>
    public sealed class OnlineExtra
    {
        public readonly List<ShopExtra> Shops = new List<ShopExtra>(4);

        public ShopExtra Of(string place, bool make)
        {
            for (int i = 0; i < Shops.Count; i++)
            {
                if (string.Equals(Shops[i].Place, place, StringComparison.Ordinal))
                {
                    return Shops[i];
                }
            }
            if (!make)
            {
                return null;
            }
            var extra = new ShopExtra { Place = place ?? string.Empty };
            Shops.Add(extra);
            return extra;
        }
    }

    /// <summary>The extras in the save, game-free: shops by '~', fields by '|', the sales and orders as lists.</summary>
    public static class OnlineExtraStore
    {
        public const string Key = "reallife:online_extra";
        public const int Version = 1;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static OnlineExtra Load(Dictionary<string, string> data)
        {
            string text;
            if (data == null || !data.TryGetValue(Key, out text) || string.IsNullOrEmpty(text))
            {
                return new OnlineExtra();
            }
            try
            {
                return Read(text) ?? new OnlineExtra();
            }
            catch (Exception)
            {
                return new OnlineExtra();
            }
        }

        public static void Save(Dictionary<string, string> data, OnlineExtra x)
        {
            if (data != null && x != null)
            {
                data[Key] = Write(x);
            }
        }

        public static string Write(OnlineExtra x)
        {
            var t = new StringBuilder(256);
            t.Append("v=").Append(Version);
            for (int i = 0; i < x.Shops.Count; i++)
            {
                ShopExtra s = x.Shops[i];
                t.Append('~').Append(Clean(s.Place)).Append('|').Append(s.FeeCents).Append('|').Append(s.FreeOverCents).Append('|').Append(s.Packing).Append('|').Append(s.Night ? 1 : 0).Append('|')
                    .Append(s.Match ? 1 : 0).Append('|').Append(s.ShowStock ? 1 : 0).Append('|').Append(s.DistrictMask).Append('|').Append(s.Reviews).Append('|').Append(s.LastDay).Append('|');
                for (int d = 0; d < ShopExtra.SalesDays; d++)
                {
                    t.Append(d > 0 ? "," : string.Empty).Append(s.Sales[d].ToString(Inv));
                }
                t.Append('|');
                for (int d = 0; d < s.Done.Count && d < ShopExtra.DoneMost; d++)
                {
                    FinishedOrder o = s.Done[d];
                    t.Append(d > 0 ? ";" : string.Empty).Append(o.Day).Append(',').Append(o.Person).Append(',').Append(o.Items).Append(',').Append(o.Total).Append(',').Append(o.District).Append(',')
                        .Append(o.Late ? 1 : 0).Append(',').Append(o.Returned ? 1 : 0);
                }
                t.Append('|');
                for (int d = 0; d < s.Lines.Count; d++)
                {
                    LineExtra l = s.Lines[d];
                    t.Append(d > 0 ? ";" : string.Empty).Append(Clean(l.Item)).Append('^').Append(l.WasCents).Append('^').Append(Clean(l.Title)).Append('^').Append(Clean(l.Desc));
                }
                t.Append('|');
                for (int d = 0; d < s.Notes.Count && d < ShopExtra.NotesMost; d++)
                {
                    ReviewNote n = s.Notes[d];
                    t.Append(d > 0 ? ";" : string.Empty).Append(Clean(n.Item)).Append('^').Append(n.Person).Append('^').Append(n.Stars).Append('^').Append(n.Day)
                        .Append('^').Append(n.District).Append('^').Append(Clean(n.Reply));
                }
            }
            return t.ToString();
        }

        public static OnlineExtra Read(string text)
        {
            string[] parts = text.Split('~');
            int version;
            if (parts.Length == 0 || !parts[0].StartsWith("v=", StringComparison.Ordinal) || !int.TryParse(parts[0].Substring(2), NumberStyles.Integer, Inv, out version) || version < 1)
            {
                return null;
            }
            var x = new OnlineExtra();
            for (int p = 1; p < parts.Length; p++)
            {
                string[] c = parts[p].Split('|');
                if (c.Length < 12 || c[0].Length == 0)
                {
                    continue;
                }
                var s = new ShopExtra
                {
                    Place = c[0], FeeCents = Math.Max(0, I(c, 1)), FreeOverCents = Math.Max(0, I(c, 2)), Packing = Math.Max(0, Math.Min(2, I(c, 3))), Night = I(c, 4) != 0,
                        Match = I(c, 5) != 0,
                    ShowStock = I(c, 6) != 0, DistrictMask = Math.Max(0, I(c, 7)), Reviews = Math.Max(0, I(c, 8)), LastDay = I(c, 9)
                };
                string[] sales = c[10].Split(',');
                for (int d = 0; d < sales.Length && d < ShopExtra.SalesDays; d++)
                {
                    s.Sales[d] = Math.Max(0, I(new[] { sales[d] }, 0));
                }
                foreach (string rec in c[11].Length > 0 ? c[11].Split(';') : new string[0])
                {
                    string[] o = rec.Split(',');
                    if (o.Length >= 7 && s.Done.Count < ShopExtra.DoneMost)
                    {
                        s.Done.Add(new FinishedOrder { Day = I(o, 0), Person = I(o, 1), Items = I(o, 2), Total = I(o, 3), District = I(o, 4), Late = I(o, 5) != 0, Returned = I(o, 6) != 0 });
                    }
                }
                foreach (string rec in c.Length > 12 && c[12].Length > 0 ? c[12].Split(';') : new string[0])
                {
                    string[] l = rec.Split('^');
                    if (l.Length >= 4)
                    {
                        s.Lines.Add(new LineExtra { Item = l[0], WasCents = Math.Max(0, I(l, 1)), Title = l[2], Desc = l[3] });
                    }
                }
                foreach (string rec in c.Length > 13 && c[13].Length > 0 ? c[13].Split(';') : new string[0])
                {
                    string[] n = rec.Split('^');
                    if (n.Length >= 6 && s.Notes.Count < ShopExtra.NotesMost)
                    {
                        s.Notes.Add(new ReviewNote { Item = n[0], Person = I(n, 1), Stars = Math.Max(1, Math.Min(5, I(n, 2))), Day = I(n, 3), District = I(n, 4), Reply = n[5] });
                    }
                }
                x.Shops.Add(s);
            }
            return x;
        }

        private static int I(string[] c, int at)
        {
            int value;
            return at < c.Length && int.TryParse(c[at], NumberStyles.Integer, Inv, out value) ? value : 0;
        }

        private static string Clean(string text) => string.IsNullOrEmpty(text) ? string.Empty : text.Replace('|', ' ').Replace(';', ' ').Replace('~', ' ').Replace('^', ' ');
    }

    /// <summary>The rules the extras follow, game-free and seeded by the save, so rivals and customers behave the same on every machine.</summary>
    public static class OnlineExtraRules
    {
        public const int NightFrom = 22, NightTo = 6;
        public const int ReviewChancePct = 35;     // of delivered orders, the share whose customer rates it
        public const int RivalsMost = 3;

        /// <summary>What the customer pays for delivery on an order of this much: nothing at or over the free-over amount.</summary>
        public static int FeeFor(ShopExtra s, int totalCents)
        {
            return s.FeeCents <= 0 || (s.FreeOverCents > 0 && totalCents >= s.FreeOverCents) ? 0 : s.FeeCents;
        }

        /// <summary>True when an order placed at this hour in this district is taken: the area is served and, at night, the shop takes night orders.</summary>
        public static bool Takes(ShopExtra s, int district, int hour)
        {
            bool night = hour % 24 >= NightFrom || hour % 24 < NightTo;
            return s.Serves(district) && (!night || s.Night);
        }

        /// <summary>The hour an order placed at night is picked from: the morning, held until 06:00.</summary>
        public static int PickFrom(ShopExtra s, int placedHour)
        {
            int h = placedHour % 24;
            return s.Night && (h >= NightFrom || h < NightTo) ? placedHour - h + (h >= NightFrom ? 24 : 0) + NightTo : placedHour;
        }

        /// <summary>The minutes customers are told packing takes: 30, 60 or a day.</summary>
        public static int PackingMinutes(int packing) => packing == ExtraPacking30 ? 30 : packing == ExtraPacking60 ? 60 : 24 * 60;

        private const int ExtraPacking30 = ShopExtra.Packing30, ExtraPacking60 = ShopExtra.Packing60;

        /// <summary>The badge a shop earns: fast delivery, when packing is told as under an hour and the area served is small enough to arrive tomorrow.</summary>
        public static bool Express(ShopExtra s, int districts)
        {
            int served = 0;
            for (int i = 0; i < Math.Max(0, districts); i++)
            {
                served += s.Serves(i) ? 1 : 0;
            }
            return s.Packing != ShopExtra.PackingDay && served > 0;
        }

        /// <summary>A rival web shop's price for a product: its own tilt on the going price, the same for the same save and product.</summary>
        public static int RivalPrice(int seed, int rival, string item, int goingCents)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u ^ (uint)(rival + 1) * 0x85EBCA6Bu;
                for (int i = 0; item != null && i < item.Length; i++)
                {
                    h = (h ^ item[i]) * 16777619u;
                }
                h ^= h >> 15;
                int tilt = (int)(h % 301u) - 150;   // thousandths: a rival is up to 15 per cent either side
                return Math.Max(1, (int)((long)goingCents * (1000 + tilt) / 1000));
            }
        }

        /// <summary>The lowest rival price for a product among the rivals a shop is up against.</summary>
        public static int LowestRival(int seed, string item, int goingCents)
        {
            int lowest = int.MaxValue;
            for (int r = 0; r < RivalsMost; r++)
            {
                lowest = Math.Min(lowest, RivalPrice(seed, r, item, goingCents));
            }
            return lowest;
        }

        /// <summary>What a line sells at: its own price, or with match prices on and no price of its own, the lowest rival price (never under what the shop paid).</summary>
        public static int Price(ShopExtra s, int seed, string item, int ownCents, int shopCents, int costCents)
        {
            if (ownCents > 0)
            {
                return ownCents;
            }
            if (!s.Match)
            {
                return shopCents;
            }
            return Math.Max(Math.Max(1, costCents), LowestRival(seed, item, shopCents));
        }

        /// <summary>An order has reached its door: it is kept in the ring, may be rated, and the customer pays the delivery fee.</summary>
        public static int Delivered(ShopExtra s, int seed, int orderId, int day, int district, int items, int totalCents, bool late, string item = null,
            bool shortOrder = false, int person = 0)
        {
            if (s.Done.Count >= ShopExtra.DoneMost)
            {
                s.Done.RemoveAt(0);
            }
            s.Done.Add(new FinishedOrder { Day = day, Person = orderId * 31 + seed, Items = items, Total = totalCents, District = district, Late = late });
            unchecked
            {
                uint h = (uint)(seed ^ orderId * 2654435761u);
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                if ((int)((h >> 8) % 100u) < ReviewChancePct)
                {
                    s.Reviews++;
                    if (item != null)
                    {
                        // A customer rates what came: late or short takes stars off, a good order gives four or five.
                        int roll = (int)((h >> 20) % 100u);
                        int stars = late ? 1 + roll % 3 : shortOrder ? 2 + roll % 3 : 4 + roll % 2;
                        if (s.Notes.Count >= ShopExtra.NotesMost)
                        {
                            s.Notes.RemoveAt(0);
                        }
                        s.Notes.Add(new ReviewNote { Item = item, Person = person, Stars = stars, Day = day, District = district });
                    }
                }
            }
            return FeeFor(s, totalCents);
        }

        /// <summary>A product reviews by star, one star first; returns how many.</summary>
        public static int Breakdown(ShopExtra s, string item, int[] into)
        {
            int n = 0;
            for (int i = 0; i < into.Length; i++)
            {
                into[i] = 0;
            }
            for (int i = 0; i < s.Notes.Count; i++)
            {
                if (string.Equals(s.Notes[i].Item, item, StringComparison.Ordinal))
                {
                    into[s.Notes[i].Stars - 1]++;
                    n++;
                }
            }
            return n;
        }

        /// <summary>The stars of a product in halves (0 to 10) from its reviews, or -1 with none.</summary>
        public static int Halves(int[] breakdown)
        {
            int n = 0, sum = 0;
            for (int i = 0; i < breakdown.Length; i++)
            {
                n += breakdown[i];
                sum += breakdown[i] * (i + 1);
            }
            return n == 0 ? -1 : (sum * 2 + n / 2) / n;
        }

        /// <summary>A rival shop reviews of a product by the same rule: a count and an average of its own, seeded, and the stars spread round the average.</summary>
        public static int RivalReviews(int seed, int rival, string item, int[] into, out int halves)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u ^ (uint)(rival + 7) * 0xC2B2AE35u;
                for (int i = 0; item != null && i < item.Length; i++)
                {
                    h = (h ^ item[i]) * 16777619u;
                }
                h ^= h >> 13;
                int count = 20 + (int)(h % 880u);
                halves = 7 + (int)((h >> 11) % 4u);   // 3.5 to 5 stars
                int[] weight = { 1, 2, 5, 25, 40 };
                int total = 0;
                for (int i = 0; i < 5; i++)
                {
                    into[i] = Math.Max(1, weight[i] + (i - 2) * (halves - 8) * 6);
                    total += into[i];
                }
                int left = count;
                for (int i = 0; i < 5; i++)
                {
                    into[i] = i == 4 ? left : (int)((long)count * into[i] / total);
                    left -= into[i];
                }
                return count;
            }
        }

        /// <summary>When an order placed now is due, as the page says it: days from today (1 is tomorrow) and the hour.</summary>
        public static void ArrivesBy(int dueHour, int nowHour, out int days, out int hour)
        {
            days = dueHour / 24 - nowHour / 24;
            hour = dueHour % 24;
        }

        /// <summary>The price a product had before it was lowered: kept while the new price stays under it, so the page shows what was saved.</summary>
        public static void PriceChanged(ShopExtra s, string item, int oldCents, int newCents)
        {
            LineExtra l = s.LineOf(item, true);
            if (newCents < oldCents && newCents > 0)
            {
                l.WasCents = Math.Max(l.WasCents, oldCents);
            }
            else if (l.WasCents > 0 && newCents >= l.WasCents)
            {
                l.WasCents = 0;
            }
        }

        /// <summary>Only a few left: at five or fewer in stock.</summary>
        public static bool OnlyLeft(int stock) => stock > 0 && stock <= 5;

        /// <summary>The hours an order placed at this hour waits for the morning, when the shop takes night orders.</summary>
        public static int HoldHours(ShopExtra s, int hour) => s.Night ? PickFrom(s, hour) - hour : 0;

        /// <summary>The day takings go into the thirty day ring.</summary>
        public static void Sold(ShopExtra s, int day, int takingsCents)
        {
            if (s.LastDay != day)
            {
                s.Sales[day % ShopExtra.SalesDays] = 0;
                s.LastDay = day;
            }
            s.Sales[day % ShopExtra.SalesDays] += Math.Max(0, takingsCents);
        }

        /// <summary>The thirty days oldest first, as takings in cents; days before the shop opened are 0 and the page leaves them out.</summary>
        public static int Ring(ShopExtra s, int today, int[] into)
        {
            for (int i = 0; i < ShopExtra.SalesDays; i++)
            {
                int day = today - (ShopExtra.SalesDays - 1 - i);
                into[i] = day > 0 && day <= s.LastDay ? s.Sales[day % ShopExtra.SalesDays] : 0;
            }
            return ShopExtra.SalesDays;
        }
    }
}
