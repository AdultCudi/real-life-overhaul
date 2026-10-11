using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>
    /// Ventures in the save: one line of the save's modData, under <see cref="Key"/>. Game-free.
    ///
    ///     v=1~h:...~i:...~o:...~p:...~d:...~c:...~s:...
    ///
    /// Parts are divided by '~' and begin with a letter and a colon; records inside a part by ';', fields by '|'.
    ///   h  the header: the numbers of <see cref="VenturesFile"/>, in the order of <see cref="Write"/>
    ///   i  the investors: id|kind|temperament|first name|last name|sectors liked|smallest sum|largest sum|diligence|closed until|last approached
    ///   o  the offers open (four at most): thirteen numbers, then the business's key, its name and, for a business not yet open, its type
    ///   p  money promised for a business not yet open (three at most): eleven numbers, then the type
    ///   d  the deals (eight at most): twenty-four numbers, then the business's key and its name
    ///   c  the companies (twelve): id|sector|rival|name index|price|ordinary move|dividend|last move|its cause, what it is
    ///      worth, a rival's name, and its last thirty closing prices divided by commas
    ///   s  the shares held: company|shares|cost|dividends received|test
    ///
    /// The line is capped by those counts: at its fullest it is a few kilobytes. A NEWER VERSION adds fields at the end
    /// of a record or a part with a new letter; this reader takes the fields it knows and skips parts it does not, so
    /// a save written by a later version still loads. A change that cannot be read that way raises
    /// <see cref="Version"/> and gets a step in <see cref="Upgrade"/>. A line that cannot be read at all is left
    /// alone and Ventures starts afresh for the save.
    /// </summary>
    public static class VenturesStore
    {
        public const string Key = "reallife:economy_ventures";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The file of this save; null if the save has none or it cannot be read.</summary>
        public static VenturesFile Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            string text;
            if (data == null || !data.TryGetValue(Key, out text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            try
            {
                VenturesFile file = Read(text, out version);
                if (file != null)
                {
                    Upgrade(file, version);
                }
                return file;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Save(Dictionary<string, string> data, VenturesFile file)
        {
            if (data != null && file != null)
            {
                data[Key] = Write(file);
            }
        }

        /// <summary>What an older version's file needs before this version uses it. There is no older version yet.</summary>
        private static void Upgrade(VenturesFile file, int from)
        {
            file.NextId = Math.Max(1, file.NextId);
        }

        public static string Write(VenturesFile f)
        {
            var t = new StringBuilder(2048);
            t.Append("v=").Append(Version).Append("~h:");
            // (while the test tools have moved the market, the save keeps the market as it stood before them)
            List<Company> companies = f.Backup != null ? f.Backup.Companies : f.Companies;
            Join(t, f.Seed, f.StartDay, f.Reputation, f.NextId, f.Pitches, f.OfferDay, f.SettledDay, f.Backup != null ? f.Backup.Steps : f.MarketSteps,
                f.Backup != null ? f.Backup.Day : f.MarketDay, f.DividendsTotal, f.PaidOutTotal,
                f.InvestedTotal, f.TargetsMet, f.LeversSeen, f.ElectionSeen, f.Unseen);
            t.Append('|').Append(f.RateSeen.ToString("R", Inv));
            t.Append("~i:");
            for (int i = 0; i < f.Investors.Count; i++)
            {
                Investor v = f.Investors[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, v.Id, (int)v.Kind, (int)v.Temper, v.First, v.Last, v.Likes, v.ChequeLeast, v.ChequeMost, v.Diligence, v.ClosedUntil, v.PitchedDay);
            }
            t.Append("~o:");
            for (int i = 0; i < f.Offers.Count && i < VenturesCatalogue.OffersMost; i++)
            {
                Offer o = f.Offers[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, o.Id, o.InvestorId, o.Source, o.Amount, o.ShareBp, o.TargetPct, o.TargetDays, o.MadeDay, o.LapseDay, o.Countered ? 1 : 0, o.Budget, o.Test ? 1 : 0, 0);
                t.Append('|').Append(Clean(o.Place)).Append('|').Append(Clean(o.Business)).Append('|').Append(Clean(o.TypeName));
            }
            t.Append("~p:");
            for (int i = 0; i < f.Pledges.Count && i < VenturesCatalogue.PledgesMost; i++)
            {
                Pledge p = f.Pledges[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, p.Id, p.InvestorId, p.Amount, p.ShareBp, p.TargetPct, p.TargetDays, p.MadeDay, p.OpenBy, p.Budget, p.Test ? 1 : 0, p.Had);
                t.Append('|').Append(Clean(p.TypeName));
            }
            t.Append("~d:");
            for (int i = 0; i < f.Deals.Count && i < VenturesCatalogue.DealsMost; i++)
            {
                Deal d = f.Deals[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, d.Id, d.InvestorId, d.Amount, d.ShareBp, d.StartDay, d.PaidOut, d.Arrears, d.BaseProfit, d.TargetProfit, d.TargetDay, d.Misses, d.TargetsMet, d.Mood,
                    (int)d.Stage, d.DemandDay, d.BoardUntil, d.CostTarget, d.CostDay, d.CallOwed, d.CallPerDay, d.CallMisses, d.Claim, d.FollowOn ? 1 : 0, d.Test ? 1 : 0);
                t.Append('|').Append(Clean(d.Place)).Append('|').Append(Clean(d.Business));
            }
            t.Append("~c:");
            for (int i = 0; i < companies.Count && i < VenturesCatalogue.Companies; i++)
            {
                Company c = companies[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, c.Id, (int)c.Sector, c.Rival ? 1 : 0, c.NameIndex, c.PriceCents, c.VolBp, c.YieldBp, c.LastBp, c.LastCause);
                t.Append('|').Append(c.FairCents.ToString("R", Inv)).Append('|').Append(Clean(c.RivalName)).Append('|');
                for (int h = 0; h < c.HistoryCount; h++)
                {
                    t.Append(h > 0 ? "," : string.Empty).Append(c.History[h].ToString(Inv));
                }
            }
            t.Append("~s:");
            for (int i = 0, written = 0; i < f.Holdings.Count; i++)
            {
                Holding s = f.Holdings[i];
                if (s.Shares <= 0)
                {
                    continue;
                }
                t.Append(written++ > 0 ? ";" : string.Empty);
                Join(t, s.CompanyId, s.Shares, s.Cost, s.Dividends, s.Test ? 1 : 0);
            }
            return t.ToString();
        }

        public static VenturesFile Read(string text, out int version)
        {
            version = 0;
            string[] parts = text.Split('~');
            if (parts.Length == 0 || !parts[0].StartsWith("v=", StringComparison.Ordinal) || !int.TryParse(parts[0].Substring(2), NumberStyles.Integer, Inv, out version) || version < 1)
            {
                return null;
            }
            var f = new VenturesFile();
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
                    case 'h': Header(f, body.Split('|')); break;
                    case 'i':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            int kind = I(c, 1, -1), temper = I(c, 2, -1);
                            if (c.Length < 9 || I(c, 0) <= 0 || kind < 0 || kind >= (int)InvestorKind.Count || temper < 0 || temper > (int)Temperament.Demanding
                                || f.Investors.Count >= VenturesCatalogue.InvestorsMost || f.Investor(I(c, 0)) != null)
                            {
                                continue;
                            }
                            f.Investors.Add(new Investor
                            {
                                Id = I(c, 0), Kind = (InvestorKind)kind, Temper = (Temperament)temper, First = Count(c, 3), Last = Count(c, 4), Likes = Count(c, 5),
                                ChequeLeast = Count(c, 6), ChequeMost = Math.Max(Count(c, 6), Count(c, 7)), Diligence = Math.Min(100, Count(c, 8)),
                                ClosedUntil = Count(c, 9), PitchedDay = Count(c, 10)
                            });
                        }
                        break;
                    case 'o':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            if (c.Length < 9 || I(c, 0) <= 0 || Count(c, 3) <= 0 || Count(c, 4) <= 0 || f.Offers.Count >= VenturesCatalogue.OffersMost)
                            {
                                continue;
                            }
                            f.Offers.Add(new Offer
                            {
                                Id = I(c, 0), InvestorId = I(c, 1), Source = Math.Min(Count(c, 2), OfferSource.FollowOn), Amount = Count(c, 3), ShareBp = Math.Min(10000, Count(c, 4)),
                                TargetPct = Count(c, 5), TargetDays = Math.Max(1, Count(c, 6)), MadeDay = I(c, 7), LapseDay = I(c, 8), Countered = I(c, 9) != 0,
                                Budget = Count(c, 10), Test = I(c, 11) != 0, Place = S(c, 13), Business = S(c, 14), TypeName = S(c, 15)
                            });
                        }
                        break;
                    case 'p':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            if (c.Length < 12 || I(c, 0) <= 0 || Count(c, 2) <= 0 || S(c, 11).Length == 0 || f.Pledges.Count >= VenturesCatalogue.PledgesMost)
                            {
                                continue;
                            }
                            f.Pledges.Add(new Pledge
                            {
                                Id = I(c, 0), InvestorId = I(c, 1), Amount = Count(c, 2), ShareBp = Math.Min(10000, Count(c, 3)), TargetPct = Count(c, 4),
                                TargetDays = Math.Max(1, Count(c, 5)), MadeDay = I(c, 6), OpenBy = I(c, 7), Budget = Count(c, 8), Test = I(c, 9) != 0, Had = Count(c, 10),
                                TypeName = S(c, 11)
                            });
                        }
                        break;
                    case 'd':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            int stage = I(c, 13, -1);
                            if (c.Length < 25 || I(c, 0) <= 0 || stage < 0 || stage > (int)DealStage.Quiet || S(c, 24).Length == 0 || f.Deals.Count >= VenturesCatalogue.DealsMost)
                            {
                                continue;
                            }
                            f.Deals.Add(new Deal
                            {
                                Id = I(c, 0), InvestorId = I(c, 1), Amount = Count(c, 2), ShareBp = Math.Min(10000, Count(c, 3)), StartDay = I(c, 4), PaidOut = Count(c, 5),
                                Arrears = Count(c, 6), BaseProfit = Count(c, 7), TargetProfit = Count(c, 8), TargetDay = Count(c, 9), Misses = Count(c, 10),
                                TargetsMet = Count(c, 11), Mood = Math.Min(100, Count(c, 12)), Stage = (DealStage)stage, DemandDay = Count(c, 14), BoardUntil = Count(c, 15),
                                CostTarget = Count(c, 16), CostDay = Count(c, 17), CallOwed = Count(c, 18), CallPerDay = Count(c, 19), CallMisses = Count(c, 20),
                                Claim = Count(c, 21), FollowOn = I(c, 22) != 0, Test = I(c, 23) != 0, Place = S(c, 24), Business = S(c, 25)
                            });
                        }
                        break;
                    case 'c':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            int sector = I(c, 1, -1);
                            if (c.Length < 10 || I(c, 0) <= 0 || sector < 0 || sector >= (int)MarketSector.Count || Count(c, 4) <= 0 || f.Companies.Count >= VenturesCatalogue.Companies
                                || f.Company(I(c, 0)) != null)
                            {
                                continue;
                            }
                            var company = new Company
                            {
                                Id = I(c, 0), Sector = (MarketSector)sector, Rival = I(c, 2) != 0 && S(c, 10).Length > 0, NameIndex = Count(c, 3) & 1,
                                PriceCents = Math.Max(VenturesCatalogue.PriceFloorCents, Count(c, 4)), VolBp = Count(c, 5), YieldBp = Count(c, 6), LastBp = I(c, 7),
                                LastCause = Count(c, 8), RivalName = S(c, 10)
                            };
                            double fair;
                            company.FairCents = double.TryParse(c[9], NumberStyles.Float, Inv, out fair) && fair >= VenturesCatalogue.PriceFloorCents && !double.IsInfinity(fair)
                                ? fair : company.PriceCents;
                            foreach (string close in S(c, 11).Length > 0 ? c[11].Split(',') : Array.Empty<string>())
                            {
                                int price;
                                if (int.TryParse(close, NumberStyles.Integer, Inv, out price) && price > 0 && company.HistoryCount < company.History.Length)
                                {
                                    company.History[company.HistoryCount++] = price;
                                }
                            }
                            if (company.HistoryCount == 0)
                            {
                                company.Remember(company.PriceCents);
                            }
                            f.Companies.Add(company);
                        }
                        break;
                    case 's':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            if (c.Length >= 3 && I(c, 0) > 0 && Count(c, 1) > 0 && f.Holding(I(c, 0)) == null && f.Holdings.Count < VenturesCatalogue.Companies)
                            {
                                f.Holdings.Add(new Holding
                                {
                                    CompanyId = I(c, 0), Shares = Math.Min(VenturesCatalogue.HoldingMost, Count(c, 1)), Cost = Count(c, 2), Dividends = Count(c, 3), Test = I(c, 4) != 0
                                });
                            }
                        }
                        break;
                }
            }
            // What points at something that is not there is dropped: a share in no company, an offer or a deal with no investor.
            f.Holdings.RemoveAll(h => f.Company(h.CompanyId) == null);
            f.Offers.RemoveAll(o => f.Investor(o.InvestorId) == null);
            f.Pledges.RemoveAll(p => f.Investor(p.InvestorId) == null);
            f.Deals.RemoveAll(d => f.Investor(d.InvestorId) == null);
            return f.Investors.Count > 0 || f.Companies.Count > 0 ? f : null;
        }

        private static void Header(VenturesFile f, string[] c)
        {
            f.Seed = I(c, 0);
            f.StartDay = I(c, 1);
            f.Reputation = Math.Min(100, Count(c, 2));
            f.NextId = Math.Max(1, I(c, 3, 1));
            f.Pitches = Count(c, 4);
            f.OfferDay = I(c, 5);
            f.SettledDay = I(c, 6);
            f.MarketSteps = Count(c, 7);
            f.MarketDay = I(c, 8);
            f.DividendsTotal = Count(c, 9);
            f.PaidOutTotal = Count(c, 10);
            f.InvestedTotal = Count(c, 11);
            f.TargetsMet = Count(c, 12);
            f.LeversSeen = I(c, 13, -1);
            f.ElectionSeen = I(c, 14, -1);
            f.Unseen = Count(c, 15);
            float rate;
            f.RateSeen = c.Length > 16 && float.TryParse(c[16], NumberStyles.Float, Inv, out rate) && !float.IsNaN(rate) && !float.IsInfinity(rate) ? rate : -1f;
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

        private static int Count(string[] c, int at)
        {
            return Math.Max(0, I(c, at));
        }

        private static string S(string[] c, int at)
        {
            return at < c.Length ? c[at] : string.Empty;
        }

        /// <summary>A text with the dividers taken out, so that it can never break the line apart.</summary>
        public static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace('|', ' ').Replace(';', ' ').Replace('~', ' ');
        }
    }
}
