using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>A brand's recognition week by week, and the recognition of the rival brand built from a rival's outlets of the same kind, twelve weeks each.</summary>
    public sealed class BrandRing
    {
        public const int Weeks = 12;
        public int BrandId;
        public int LastWeek = -1;
        public int Count;
        public readonly int[] Mine = new int[Weeks];      // the brand's best district, hundredths, oldest first
        public readonly int[] Rival = new int[Weeks];     // the rival brand's, the same weeks

        public void Remember(int week, int mine, int rival)
        {
            LastWeek = week;
            if (Count < Weeks)
            {
                Mine[Count] = mine;
                Rival[Count] = rival;
                Count++;
                return;
            }
            for (int i = 1; i < Weeks; i++)
            {
                Mine[i - 1] = Mine[i];
                Rival[i - 1] = Rival[i];
            }
            Mine[Weeks - 1] = mine;
            Rival[Weeks - 1] = rival;
        }
    }

    /// <summary>A franchise's royalties month by month: a bit for each of the last twelve, set when they were paid on time.</summary>
    public sealed class RoyaltyRing
    {
        public const int Months = 12;
        public int FranchiseId;
        public int LastMonth = -1;
        public int Count;
        public int Bits;                                   // bit i is month i of the ring, oldest first

        public void Remember(int month, bool paid)
        {
            LastMonth = month;
            if (Count < Months)
            {
                Bits |= (paid ? 1 : 0) << Count;
                Count++;
                return;
            }
            Bits = (Bits >> 1) | ((paid ? 1 : 0) << (Months - 1));
        }

        /// <summary>Months of the ring paid on time.</summary>
        public int OnTime
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Count; i++)
                {
                    n += (Bits >> i) & 1;
                }
                return n;
            }
        }
    }

    /// <summary>A franchisee's claim for a rebate: a rival opened a business of the brand's kind inside the exclusive area.</summary>
    public sealed class Rebate
    {
        public int FranchiseId, Move, Tycoon, Day;
        public string District = string.Empty, Type = string.Empty;
    }

    /// <summary>A rival's outlet as brands see it: which move opened it, whose it is, where and of what kind.</summary>
    public struct RivalOutletFact
    {
        public int Move, Tycoon;
        public string District, Type;
    }

    /// <summary>What Brands keeps besides the brands themselves (<see cref="BrandsState"/>): its own save key, so an old save loads as it was.</summary>
    public sealed class BrandsExtra
    {
        public const int RebatesMost = 24;
        public readonly List<BrandRing> Rings = new List<BrandRing>(4);
        public readonly List<RoyaltyRing> Royalties = new List<RoyaltyRing>(16);
        public readonly List<Rebate> Rebates = new List<Rebate>(8);

        public BrandRing RingOf(int brandId, bool make)
        {
            for (int i = 0; i < Rings.Count; i++)
            {
                if (Rings[i].BrandId == brandId)
                {
                    return Rings[i];
                }
            }
            if (!make)
            {
                return null;
            }
            var ring = new BrandRing { BrandId = brandId };
            Rings.Add(ring);
            return ring;
        }

        public RoyaltyRing RoyaltyOf(int franchiseId, bool make)
        {
            for (int i = 0; i < Royalties.Count; i++)
            {
                if (Royalties[i].FranchiseId == franchiseId)
                {
                    return Royalties[i];
                }
            }
            if (!make)
            {
                return null;
            }
            var ring = new RoyaltyRing { FranchiseId = franchiseId };
            Royalties.Add(ring);
            return ring;
        }

        public bool Has(int franchiseId, int move)
        {
            for (int i = 0; i < Rebates.Count; i++)
            {
                if (Rebates[i].FranchiseId == franchiseId && Rebates[i].Move == move)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The rebate claims of a franchise that is still on a claim now (the franchise has an open dispute).</summary>
        public Rebate Latest(int franchiseId)
        {
            Rebate found = null;
            for (int i = 0; i < Rebates.Count; i++)
            {
                found = Rebates[i].FranchiseId == franchiseId ? Rebates[i] : found;
            }
            return found;
        }

        /// <summary>What points at a brand or franchise that is gone is dropped.</summary>
        public void Prune(BrandsState state)
        {
            Rings.RemoveAll(r => state.Brand(r.BrandId) == null);
            Royalties.RemoveAll(r => state.Franchise(r.FranchiseId) == null);
            Rebates.RemoveAll(r => state.Franchise(r.FranchiseId) == null);
        }
    }

    /// <summary>The brands' extras in the save, game-free: one line of modData, parts by '~' (r, y, c), records by ';', fields by '|'.</summary>
    public static class BrandExtraStore
    {
        public const string Key = "reallife:brands_extra";
        public const int Version = 1;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static BrandsExtra Load(Dictionary<string, string> data)
        {
            string text;
            if (data == null || !data.TryGetValue(Key, out text) || string.IsNullOrEmpty(text))
            {
                return new BrandsExtra();
            }
            try
            {
                return Read(text) ?? new BrandsExtra();
            }
            catch (Exception)
            {
                return new BrandsExtra();
            }
        }

        public static void Save(Dictionary<string, string> data, BrandsExtra x)
        {
            if (data != null && x != null)
            {
                data[Key] = Write(x);
            }
        }

        public static string Write(BrandsExtra x)
        {
            var t = new StringBuilder(256);
            t.Append("v=").Append(Version).Append("~r:");
            for (int i = 0; i < x.Rings.Count; i++)
            {
                BrandRing r = x.Rings[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(r.BrandId).Append('|').Append(r.LastWeek).Append('|').Append(r.Count).Append('|');
                for (int k = 0; k < r.Count; k++)
                {
                    t.Append(k > 0 ? "," : string.Empty).Append(r.Mine[k].ToString(Inv)).Append('/').Append(r.Rival[k].ToString(Inv));
                }
            }
            t.Append("~y:");
            for (int i = 0; i < x.Royalties.Count; i++)
            {
                RoyaltyRing r = x.Royalties[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(r.FranchiseId).Append('|').Append(r.LastMonth).Append('|').Append(r.Count).Append('|').Append(r.Bits);
            }
            t.Append("~c:");
            for (int i = 0; i < x.Rebates.Count && i < BrandsExtra.RebatesMost; i++)
            {
                Rebate c = x.Rebates[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(c.FranchiseId).Append('|').Append(c.Move).Append('|').Append(c.Tycoon).Append('|').Append(c.Day).Append('|')
                    .Append(Clean(c.District)).Append('|').Append(Clean(c.Type));
            }
            return t.ToString();
        }

        public static BrandsExtra Read(string text)
        {
            string[] parts = text.Split('~');
            int version;
            if (parts.Length == 0 || !parts[0].StartsWith("v=", StringComparison.Ordinal) || !int.TryParse(parts[0].Substring(2), NumberStyles.Integer, Inv, out version) || version < 1)
            {
                return null;
            }
            var x = new BrandsExtra();
            for (int p = 1; p < parts.Length; p++)
            {
                if (parts[p].Length < 2 || parts[p][1] != ':')
                {
                    continue;
                }
                string body = parts[p].Substring(2);
                string[] records = body.Length == 0 ? new string[0] : body.Split(';');
                switch (parts[p][0])
                {
                    case 'r':
                        foreach (string rec in records)
                        {
                            string[] c = rec.Split('|');
                            if (c.Length < 4 || I(c, 0) <= 0)
                            {
                                continue;
                            }
                            var ring = new BrandRing { BrandId = I(c, 0), LastWeek = I(c, 1, -1) };
                            foreach (string pair in c[3].Length > 0 ? c[3].Split(',') : new string[0])
                            {
                                string[] ab = pair.Split('/');
                                if (ab.Length == 2 && ring.Count < BrandRing.Weeks)
                                {
                                    ring.Mine[ring.Count] = I(ab, 0);
                                    ring.Rival[ring.Count] = I(ab, 1);
                                    ring.Count++;
                                }
                            }
                            x.Rings.Add(ring);
                        }
                        break;
                    case 'y':
                        foreach (string rec in records)
                        {
                            string[] c = rec.Split('|');
                            if (c.Length >= 4 && I(c, 0) > 0)
                            {
                                x.Royalties.Add(new RoyaltyRing { FranchiseId = I(c, 0), LastMonth = I(c, 1, -1), Count = Math.Max(0, Math.Min(RoyaltyRing.Months, I(c, 2))),
                                    Bits = I(c, 3) });
                            }
                        }
                        break;
                    case 'c':
                        foreach (string rec in records)
                        {
                            string[] c = rec.Split('|');
                            if (c.Length >= 6 && I(c, 0) > 0 && x.Rebates.Count < BrandsExtra.RebatesMost)
                            {
                                x.Rebates.Add(new Rebate { FranchiseId = I(c, 0), Move = I(c, 1), Tycoon = I(c, 2), Day = I(c, 3), District = c[4], Type = c[5] });
                            }
                        }
                        break;
                }
            }
            return x;
        }

        private static int I(string[] c, int at, int fallback = 0)
        {
            int value;
            return at < c.Length && int.TryParse(c[at], NumberStyles.Integer, Inv, out value) ? value : fallback;
        }

        private static string Clean(string text) => string.IsNullOrEmpty(text) ? string.Empty : text.Replace('|', ' ').Replace(';', ' ').Replace('~', ' ');
    }

    /// <summary>
    /// Brand value, the rival brand and the franchise grievances beyond the old ones, game-free. A brand is worth its shops at what a shop of that standing is worth, plus years of
    /// the royalty and levy it takes; a rival's brand is worth the same by the same rule, from the rival's outlets of the same kind.
    /// </summary>
    public static class BrandExtraRules
    {
        public const int ExclusiveRadius = 600;      // metres round a franchised shop that is exclusive
        public const int ShopWorth = 40000;          // at the starting prices, what a shop of an unknown brand adds
        public const int FeeYears = 2;               // months of fees: a brand is worth this many years of what its franchises pay
        public const int RebateDays = 45;            // a rebate claim is this many days of royalty and levy

        /// <summary>What a brand is worth: each shop at the recognition it has, and two years of the fees a month.</summary>
        public static long Value(int shops, int bestRec, int monthlyFees, int index)
        {
            long perShop = (long)CompanyCatalogue.AtPrices(ShopWorth, index) * (50 + Math.Min(10000, Math.Max(0, bestRec)) / 100) / 100;
            return perShop * Math.Max(0, shops) + (long)Math.Max(0, monthlyFees) * 12 * FeeYears;
        }

        /// <summary>What a rival brand's recognition is: its outlets of the same kind, a step each, and a seeded standing of its own. Hundredths, 0 to 9000.</summary>
        public static int RivalRec(int seed, int tycoon, int outlets, int week)
        {
            int own = (int)(VenturesStep(seed, tycoon, 41) % 1500u);
            int drift = (int)(VenturesStep(seed, tycoon * 977 + week, 42) % 201u) - 100;
            return Math.Max(0, Math.Min(9000, 500 + own + Math.Max(0, outlets) * 700 + drift));
        }

        /// <summary>What a rival's brand is worth by the same rule, its fees taken from its outlets' takings.</summary>
        public static long RivalValue(int outlets, int rec, int index) => Value(outlets, rec, Math.Max(0, outlets) * CompanyCatalogue.AtPrices(600, index), index);

        private static uint VenturesStep(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u;
                h ^= (uint)b * 0x85EBCA6Bu + 0x7F4A7C15u + (h << 6) + (h >> 2);
                h ^= (uint)c * 0xC2B2AE35u + 0x165667B1u + (h << 6) + (h >> 2);
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                return h;
            }
        }

        /// <summary>Weekly: the brand's best district and the rival's recognition go into the ring, once for each new week.</summary>
        public static bool Week(BrandsExtra x, Brand b, int seed, int tycoon, int rivalOutlets, int day)
        {
            int week = day / 7;
            BrandRing ring = x.RingOf(b.Id, true);
            if (ring.LastWeek == week)
            {
                return false;
            }
            ring.Remember(week, BrandRules.Best(b), RivalRec(seed, tycoon, rivalOutlets, week));
            return true;
        }

        /// <summary>Monthly: whether each franchise paid its royalties on time. A franchisee short of cash pays late.</summary>
        public static bool Month(BrandsExtra x, Franchise f, int day, int monthDays)
        {
            int month = day / Math.Max(1, monthDays);
            RoyaltyRing ring = x.RoyaltyOf(f.Id, true);
            if (ring.LastMonth == month)
            {
                return false;
            }
            ring.Remember(month, !f.Trouble);
            return true;
        }

        /// <summary>
        /// A rival opened a business of the brand's kind where an exclusive franchise stands: the franchisee files a claim for a rebate, once for each outlet. The district stands for
        /// the area, because a rival's outlet is placed by its district. Returns the claim, or null.
        /// </summary>
        public static Rebate Inside(BrandsExtra x, BrandsState state, Franchise f, IList<RivalOutletFact> outlets, int day)
        {
            Brand b = state.Brand(f.BrandId);
            if (b == null || !f.Exclusive || f.Ask != Ask.None || day < f.AskRest)
            {
                return null;
            }
            for (int i = 0; i < outlets.Count; i++)
            {
                RivalOutletFact o = outlets[i];
                if (string.Equals(o.District, f.District, StringComparison.Ordinal) && string.Equals(o.Type, b.Type, StringComparison.Ordinal) && !x.Has(f.Id, o.Move))
                {
                    if (x.Rebates.Count >= BrandsExtra.RebatesMost)
                    {
                        x.Rebates.RemoveAt(0);
                    }
                    var claim = new Rebate { FranchiseId = f.Id, Move = o.Move, Tycoon = o.Tycoon, Day = day, District = o.District, Type = o.Type };
                    x.Rebates.Add(claim);
                    return claim;
                }
            }
            return null;
        }

        /// <summary>What the rebate claims: days of royalty and levy, and the claim stands as the franchise's dispute (answered as any other).</summary>
        public static int RebateClaim(Brand b, Franchise f)
        {
            int ordinary = FranchiseRules.Ordinary(f.Base, b.Ticket);
            return (FranchiseRules.Royalty(ordinary, f.RoyaltyBp) + FranchiseRules.Royalty(ordinary, f.LevyBp)) * RebateDays;
        }

        /// <summary>The franchisee files it: the franchise stands in dispute for the claim.</summary>
        public static void File(Brand b, Franchise f, int day)
        {
            f.Ask = Ask.Dispute;
            f.AskUntil = day + BrandCatalogue.AskDays;
            f.AskAmount = Math.Max(0, RebateClaim(b, f));
        }

        /// <summary>The shops of the brand's own inside the exclusive radius of a franchise, by their doors.</summary>
        public static int OwnInside(BrandsState state, Franchise f)
        {
            int n = 0;
            if (!f.HasSpot)
            {
                return 0;
            }
            for (int i = 0; i < state.Members.Count; i++)
            {
                Member m = state.Members[i];
                n += m.BrandId == f.BrandId && m.HasSpot && BrandRules.Distance(f.X, f.Z, m.X, m.Z) <= ExclusiveRadius ? 1 : 0;
            }
            for (int i = 0; i < state.Franchises.Count; i++)
            {
                Franchise o = state.Franchises[i];
                n += o.Id != f.Id && o.BrandId == f.BrandId && o.HasSpot && BrandRules.Distance(f.X, f.Z, o.X, o.Z) <= ExclusiveRadius ? 1 : 0;
            }
            return n;
        }
    }
}
