using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>
    /// Brands and franchises in the save: one line of the save's modData, under <see cref="Key"/>. Game-free.
    ///
    ///     v=1^h:...^b:...^d:...^m:...^a:...^f:...^t:...
    ///
    /// Parts are divided by '^' and begin with a letter and a colon; records inside a part by ';', fields by '|'.
    ///   h  the header: seed|first day|last day|next number
    ///   b  the brands: id|name|type|flagship|founded|age|city-wide recognition|standards|minimum|on offer|campaign|
    ///      from|end|its district|fund|ticket|net|earned|failed|bid|bid by|bid until|ask rest
    ///   d  recognition by district: brand|district|figure
    ///   m  the member shops: place|brand|joined
    ///   a  the applicants: id|brand|name|temperament|capital|experience|shops run|shops closed|ability|district|until|from
    ///   f  the franchises: id|brand|name|temperament|ability|district|place|the game's name|hold|x|z|has a spot|
    ///      customers|signed|until|term|entry fee|royalty|levy|exclusive|standards|seen|seen day|warnings|warned until|
    ///      retrained until|cash|ask|ask until|ask amount|ask rest|court number|waived until|takings|royalty paid
    ///      yesterday|customers yesterday|paid in all|no renewal
    ///   t  the player's shops under a city chain: place|chain|from|until|days in breach|average royalty|paid
    ///
    /// Every figure is a whole number, so what is read back is exactly what was written. A NEWER VERSION adds fields at
    /// the end of a record or a part with a new letter; this reader takes the fields it knows and skips parts it does
    /// not, so a save written by a later version still loads. A figure out of its bounds is put inside them, a record
    /// that points at a brand that is not there is dropped, and a line that cannot be read is left alone and the
    /// feature starts afresh. Nothing here is the game's: a save loads in the plain game with this line ignored.
    ///
    /// <see cref="TestKey"/> holds the same line as it was before the first test tool changed it, for as long as test
    /// changes are in place.
    /// </summary>
    public static class BrandStore
    {
        public const string Key = "reallife:economy_brands";
        public const string TestKey = "reallife:economy_brands_before_tests";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The state of this save; null if the save has none or it cannot be read.</summary>
        public static BrandsState Load(Dictionary<string, string> data, string key, out int version)
        {
            version = 0;
            string text;
            if (data == null || !data.TryGetValue(key, out text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return Read(text, out version);
        }

        public static string Write(BrandsState s)
        {
            var t = new StringBuilder(256 + s.Brands.Count * 200 + s.Franchises.Count * 220 + s.Members.Count * 40);
            t.Append("v=").Append(Version).Append("^h:");
            Join(t, s.Seed, s.FirstDay, s.LastDay, s.NextId);
            t.Append("^b:");
            for (int i = 0; i < s.Brands.Count && i < BrandCatalogue.BrandsMost; i++)
            {
                Brand b = s.Brands[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, b.Id);
                t.Append('|').Append(Clean(b.Name)).Append('|').Append(Clean(b.Type)).Append('|').Append(Clean(b.Flagship)).Append('|');
                Join(t, b.Founded, b.Age, b.City, b.Standards, b.Minimum, b.Offered ? 1 : 0, b.Push, b.PushFrom, b.PushEnd);
                t.Append('|').Append(Clean(b.PushDistrict)).Append('|');
                Join(t, b.Fund, b.Ticket, b.Net, b.Earned, b.Failed, b.Bid, b.BidBy, b.BidUntil, b.AskRest);
            }
            t.Append("^d:");
            bool first = true;
            for (int i = 0; i < s.Brands.Count && i < BrandCatalogue.BrandsMost; i++)
            {
                Brand b = s.Brands[i];
                for (int k = 0; k < b.Districts.Count && k < BrandCatalogue.DistrictsMost; k++)
                {
                    t.Append(first ? string.Empty : ";");
                    first = false;
                    Join(t, b.Id);
                    t.Append('|').Append(Clean(b.Districts[k].Name)).Append('|');
                    Join(t, b.Districts[k].Rec);
                }
            }
            t.Append("^m:");
            for (int i = 0; i < s.Members.Count && i < BrandCatalogue.MembersMost; i++)
            {
                Member m = s.Members[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(Clean(m.Place)).Append('|');
                Join(t, m.BrandId, m.Joined);
            }
            t.Append("^a:");
            for (int i = 0; i < s.Applicants.Count && i < BrandCatalogue.ApplicantsMost; i++)
            {
                Applicant a = s.Applicants[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, a.Id, a.BrandId);
                t.Append('|').Append(Clean(a.Name)).Append('|');
                Join(t, (int)a.Temper, a.Capital, a.Experience, a.ShopsRun, a.ShopsClosed, a.Ability);
                t.Append('|').Append(Clean(a.District)).Append('|');
                Join(t, a.Until, a.From);
            }
            t.Append("^f:");
            for (int i = 0; i < s.Franchises.Count && i < BrandCatalogue.FranchisesMost; i++)
            {
                Franchise f = s.Franchises[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, f.Id, f.BrandId);
                t.Append('|').Append(Clean(f.Name)).Append('|');
                Join(t, (int)f.Temper, f.Ability);
                t.Append('|').Append(Clean(f.District)).Append('|').Append(Clean(f.Place)).Append('|').Append(Clean(f.GameName)).Append('|');
                Join(t, (int)f.Hold, f.X, f.Z, f.HasSpot ? 1 : 0, f.Base, f.Signed, f.Until, f.Term, f.EntryFee, f.RoyaltyBp, f.LevyBp, f.Exclusive ? 1 : 0, f.True, f.Seen, f.SeenDay, f.Warnings,
                    f.WarnedUntil, f.RetrainedUntil, f.Health, (int)f.Ask, f.AskUntil, f.AskAmount, f.AskRest, f.Suit, f.WaivedUntil, f.Takings, f.Royalty, f.Customers, f.Paid, f.NoRenewal ? 1 : 0);
            }
            t.Append("^t:");
            for (int i = 0; i < s.Taken.Count && i < BrandCatalogue.TakenMost; i++)
            {
                Taken k = s.Taken[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(Clean(k.Place)).Append('|');
                Join(t, k.Chain, k.From, k.Until, k.Breach, k.Average, k.Paid);
            }
            return t.ToString();
        }

        /// <summary>Reads a line. Null for one that is not this feature's at all.</summary>
        public static BrandsState Read(string text, out int version)
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
                var s = new BrandsState();
                bool header = false;
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
                            s.LastDay = Math.Max(0, I(c, 2));
                            s.NextId = Math.Max(1, I(c, 3, 1));
                            header = true;
                            break;
                        }
                        case 'b':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                int id = I(c, 0);
                                if (c.Length >= 4 && id > 0 && c[1].Length > 0 && s.Brands.Count < BrandCatalogue.BrandsMost && s.Brand(id) == null)
                                {
                                    s.Brands.Add(ReadBrand(c, id));
                                }
                            }
                            break;
                        case 'd':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                Brand b = s.Brand(I(c, 0));
                                if (b != null && c.Length >= 3 && c[1].Length > 0 && b.District(c[1]) == null && b.Districts.Count < BrandCatalogue.DistrictsMost)
                                {
                                    b.Districts.Add(new DistrictFile { Name = c[1], Rec = BrandCatalogue.Clamp(I(c, 2), 0, BrandCatalogue.RecMost) });
                                }
                            }
                            break;
                        case 'm':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                if (c[0].Length > 0 && s.Brand(I(c, 1)) != null && s.Member(c[0]) == null && s.Members.Count < BrandCatalogue.MembersMost)
                                {
                                    s.Members.Add(new Member { Place = c[0], BrandId = I(c, 1), Joined = Math.Max(0, I(c, 2)) });
                                }
                            }
                            break;
                        case 'a':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                int id = I(c, 0);
                                if (c.Length >= 11 && id > 0 && s.Brand(I(c, 1)) != null && s.Applicant(id) == null && s.Applicants.Count < BrandCatalogue.ApplicantsMost)
                                {
                                    s.Applicants.Add(new Applicant
                                    {
                                        Id = id, BrandId = I(c, 1), Name = c[2], Temper = (Temper)BrandCatalogue.Clamp(I(c, 3), 0, 2), Capital = Math.Max(0, I(c, 4)),
                                        Experience = BrandCatalogue.Clamp(I(c, 5), 0, BrandCatalogue.ExperienceMost), ShopsRun = Math.Max(0, I(c, 6)), ShopsClosed = Math.Max(0, I(c, 7)),
                                        Ability = BrandCatalogue.Clamp(I(c, 8, 50), 0, 100), District = c[9], Until = Math.Max(0, I(c, 10)), From = Math.Max(0, I(c, 11))
                                    });
                                }
                            }
                            break;
                        case 'f':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                int id = I(c, 0);
                                if (c.Length >= 20 && id > 0 && s.Brand(I(c, 1)) != null && s.Franchise(id) == null && s.Franchises.Count < BrandCatalogue.FranchisesMost)
                                {
                                    s.Franchises.Add(ReadFranchise(c, id));
                                }
                            }
                            break;
                        case 't':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                if (c[0].Length > 0 && c.Length >= 4 && s.TakenAt(c[0]) == null && s.Member(c[0]) == null && s.Taken.Count < BrandCatalogue.TakenMost)
                                {
                                    s.Taken.Add(new Taken
                                    {
                                        Place = c[0], Chain = BrandCatalogue.Clamp(I(c, 1), 0, BrandCatalogue.Chains - 1), From = Math.Max(0, I(c, 2)), Until = Math.Max(0, I(c, 3)),
                                        Breach = BrandCatalogue.Clamp(I(c, 4), 0, BrandCatalogue.ChainEndDays), Average = Math.Max(0, I(c, 5)), Paid = Math.Max(0, I(c, 6))
                                    });
                                }
                            }
                            break;
                    }
                }
                if (!header)
                {
                    return null;
                }
                for (int i = 0; i < s.Brands.Count; i++)
                {
                    Brand b = s.Brands[i];
                    if (b.Bid > 0 && s.Franchise(b.BidBy) == null)
                    {
                        b.Bid = b.BidBy = b.BidUntil = 0;   // (a bid whose bidder is not in the line)
                    }
                    s.NextId = Math.Max(s.NextId, b.Id + 1);
                }
                for (int i = 0; i < s.Franchises.Count; i++)
                {
                    s.NextId = Math.Max(s.NextId, s.Franchises[i].Id + 1);
                }
                for (int i = 0; i < s.Applicants.Count; i++)
                {
                    s.NextId = Math.Max(s.NextId, s.Applicants[i].Id + 1);
                }
                return s;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Brand ReadBrand(string[] c, int id)
        {
            var b = new Brand
            {
                Id = id, Name = c[1], Type = c[2], Flagship = c[3], Founded = Math.Max(0, I(c, 4)), Age = Math.Max(0, I(c, 5)), City = BrandCatalogue.Clamp(I(c, 6), 0, BrandCatalogue.RecMost),
                Standards = BrandCatalogue.Clamp(I(c, 7), 0, 100), Offered = I(c, 9) != 0, Fund = Math.Max(0, I(c, 14)), Ticket = Math.Max(0, I(c, 15)), Net = I(c, 16), Earned = Math.Max(0, I(c, 17)),
                Failed = Math.Max(0, I(c, 18)), Bid = Math.Max(0, I(c, 19)), BidBy = Math.Max(0, I(c, 20)), BidUntil = Math.Max(0, I(c, 21)), AskRest = Math.Max(0, I(c, 22))
            };
            int minimum = I(c, 8, BrandCatalogue.OfferStandards);
            b.Minimum = Array.IndexOf(BrandCatalogue.MinimumSteps, minimum) >= 0 ? minimum : BrandCatalogue.OfferStandards;
            int push = I(c, 10, -1);
            if (push == BrandCatalogue.PushLocal || push == BrandCatalogue.PushCity)
            {
                b.Push = push;
                b.PushFrom = Math.Max(0, I(c, 11));
                b.PushEnd = Math.Max(0, I(c, 12));
                b.PushDistrict = c.Length > 13 ? c[13] : string.Empty;
            }
            return b;
        }

        private static Franchise ReadFranchise(string[] c, int id)
        {
            int ask = I(c, 27);
            int hold = I(c, 8, (int)Hold.Books);
            return new Franchise
            {
                Id = id, BrandId = I(c, 1), Name = c[2], Temper = (Temper)BrandCatalogue.Clamp(I(c, 3), 0, 2), Ability = BrandCatalogue.Clamp(I(c, 4, 50), 0, 100), District = c[5], Place = c[6],
                GameName = c[7], Hold = hold >= 0 && hold <= (int)Hold.Books ? (Hold)hold : Hold.Books, X = I(c, 9), Z = I(c, 10), HasSpot = I(c, 11) != 0, Base = Math.Max(0, I(c, 12)),
                Signed = Math.Max(0, I(c, 13)), Until = Math.Max(0, I(c, 14)), Term = Math.Max(1, I(c, 15, 1)), EntryFee = Math.Max(0, I(c, 16)),
                RoyaltyBp = BrandCatalogue.Clamp(I(c, 17), 0, BrandCatalogue.RoyaltyBp[BrandCatalogue.Tiers - 1]), LevyBp = BrandCatalogue.Clamp(I(c, 18), 0, BrandCatalogue.LevyBp[BrandCatalogue.Tiers - 1]),
                Exclusive = I(c, 19) != 0, True = BrandCatalogue.Clamp(I(c, 20, 6000), 0, 10000), Seen = BrandCatalogue.Clamp(I(c, 21, -1), -1, 100), SeenDay = Math.Max(0, I(c, 22)),
                Warnings = Math.Max(0, I(c, 23)), WarnedUntil = Math.Max(0, I(c, 24)), RetrainedUntil = Math.Max(0, I(c, 25)), Health = I(c, 26),
                Ask = ask >= 0 && ask <= (int)Ask.BuyBrand ? (Ask)ask : Ask.None, AskUntil = Math.Max(0, I(c, 28)), AskAmount = Math.Max(0, I(c, 29)), AskRest = Math.Max(0, I(c, 30)),
                Suit = Math.Max(0, I(c, 31)), WaivedUntil = Math.Max(0, I(c, 32)), Takings = Math.Max(0, I(c, 33)), Royalty = Math.Max(0, I(c, 34)), Customers = Math.Max(0, I(c, 35)),
                Paid = Math.Max(0, I(c, 36)), NoRenewal = I(c, 37) != 0
            };
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
