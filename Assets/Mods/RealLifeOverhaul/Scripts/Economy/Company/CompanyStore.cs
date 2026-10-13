using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// "Running a business" in the save: one line of the save's modData, under <see cref="Key"/>. Game-free.
    ///
    ///     v=1^h:...^t:...^s:...^e:...^c:...^r:...
    ///
    /// Parts are divided by '^' and begin with a letter and a colon; records inside a part by ';', fields by '|'.
    ///   h  the header: seed|first day|last day|days counted|sick pay policy|no approach before|next claim number
    ///   t  tax: quarter from|quarter|five costs by kind|return due|its quarter|its takings|its costs|its penalties|
    ///      late run|accountant|next fee day|risk|saved|owed|last audit|court case
    ///   s  the businesses: place|first day|hidden per cent|campaign kind|from|end|cost|per cent|four last ends|
    ///      strike from|end|rest|overtime asked|from|end|rest|cost|loss run|then three policies of
    ///      insurer|premium|from|until|claims|missed|declined|and four licences of until|renews itself|reminded
    ///   e  the staff: id|place|standing|raise|days underpaid|asked on|asked to|ask rest|offer day|offer|offer rest|leaves on|rival
    ///   c  the claims: id|place|kind|insurer|day|loss|excess|payout|stage|pay day|name
    ///   r  the reports: kind|from|days|days before|days during|customers before|during|takings before|during|profit before|during|cost|name
    ///
    /// Every figure is a whole number, so what is read back is exactly what was written. A NEWER VERSION adds fields at
    /// the end of a record or a part with a new letter; this reader takes the fields it knows and skips parts it does
    /// not, so a save written by a later version still loads. A figure out of its bounds is put inside them; a line
    /// that cannot be read is left alone and the feature starts afresh. The lists are capped when written
    /// (<see cref="CompanyCatalogue.ShopsMost"/> and its neighbours). Nothing here is the game's: a save loads in the
    /// plain game with this line ignored.
    ///
    /// <see cref="TestKey"/> holds the same line as it was before the first test tool changed it, for as long as test
    /// changes are in place.
    /// </summary>
    public static class CompanyStore
    {
        public const string Key = "reallife:economy_company";
        public const string TestKey = "reallife:economy_company_before_tests";
        public const int Version = 1;

        private const int ShopFields = 21, PolicyFields = 7, LicenceFields = 3;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The state of this save; null if the save has none or it cannot be read.</summary>
        public static CompanyState Load(Dictionary<string, string> data, string key, out int version)
        {
            version = 0;
            string text;
            if (data == null || !data.TryGetValue(key, out text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return Read(text, out version);
        }

        public static string Write(CompanyState s)
        {
            var t = new StringBuilder(512 + s.Shops.Count * 160 + s.Staff.Count * 60);
            t.Append("v=").Append(Version).Append("^h:");
            Join(t, s.Seed, s.FirstDay, s.LastDay, s.Age, s.SickPolicy, s.PoachRest, s.NextClaim);
            TaxFile x = s.Tax;
            t.Append("^t:");
            Join(t, x.QuarterFrom, x.Quarter, x.Costs[0], x.Costs[1], x.Costs[2], x.Costs[3], x.Costs[4], x.ReturnDue, x.ReturnQuarter, x.ReturnTakings, x.ReturnCosts, x.ReturnPenalties,
                x.LateRun, x.Accountant, x.AccountantNext, x.Risk, x.Saved, x.Owed, x.LastAudit, x.CaseNumber);
            t.Append("^s:");
            for (int i = 0; i < s.Shops.Count && i < CompanyCatalogue.ShopsMost; i++)
            {
                ShopFile shop = s.Shops[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(Clean(shop.Place)).Append('|');
                Join(t, shop.FirstDay, shop.Hide, shop.Push, shop.PushFrom, shop.PushEnd, shop.PushCost, shop.PushPercent, shop.PushEnded[0], shop.PushEnded[1], shop.PushEnded[2], shop.PushEnded[3],
                    shop.StrikeFrom, shop.StrikeEnd, shop.StrikeRest, shop.OvertimeAsk, shop.OvertimeFrom, shop.OvertimeEnd, shop.OvertimeRest, shop.OvertimeCost, shop.LossRun);
                for (int k = 0; k < shop.Cover.Length; k++)
                {
                    Policy p = shop.Cover[k];
                    t.Append('|');
                    Join(t, p.Insurer, p.Premium, p.From, p.Until, p.Claims, p.Missed, p.Declined);
                }
                for (int k = 0; k < shop.Permits.Length; k++)
                {
                    Licence l = shop.Permits[k];
                    t.Append('|');
                    Join(t, l.Until, l.Auto ? 1 : 0, l.Reminded ? 1 : 0);
                }
                for (int k = CompanyCatalogue.CampaignOwnKinds; k < shop.PushEnded.Length; k++)
                {
                    t.Append('|').Append(shop.PushEnded[k].ToString(Inv));   // kinds added later: after everything an older version reads, which it leaves alone
                }
            }
            t.Append("^e:");
            for (int i = 0; i < s.Staff.Count && i < CompanyCatalogue.StaffMost; i++)
            {
                StaffFile f = s.Staff[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(Clean(f.Id)).Append('|').Append(Clean(f.Place)).Append('|');
                Join(t, f.Standing, f.Raise, f.UnderDays, f.AskDay, f.AskTo, f.AskRest, f.PoachDay, f.PoachOffer, f.PoachRest, f.LeaveDay);
                t.Append('|').Append(Clean(f.Rival));
            }
            t.Append("^c:");
            for (int i = Math.Max(0, s.Claims.Count - CompanyCatalogue.ClaimsMost), n = 0; i < s.Claims.Count; i++, n++)
            {
                Claim c = s.Claims[i];
                t.Append(n > 0 ? ";" : string.Empty);
                Join(t, c.Id);
                t.Append('|').Append(Clean(c.Place)).Append('|');
                Join(t, c.Kind, c.Insurer, c.Day, c.Loss, c.Excess, c.Payout, (int)c.Stage, c.PayDay);
                t.Append('|').Append(Clean(c.Name));
            }
            t.Append("^r:");
            for (int i = Math.Max(0, s.Reports.Count - CompanyCatalogue.ReportsKept), n = 0; i < s.Reports.Count; i++, n++)
            {
                CampaignReport r = s.Reports[i];
                t.Append(n > 0 ? ";" : string.Empty);
                Join(t, r.Kind, r.From, r.Days, r.DaysBefore, r.DaysDuring, r.CustomersBefore, r.CustomersDuring, r.TakingsBefore, r.TakingsDuring, r.ProfitBefore, r.ProfitDuring, r.Cost);
                t.Append('|').Append(Clean(r.Name));
            }
            return t.ToString();
        }

        /// <summary>Reads a line. Null for one that is not this feature's at all.</summary>
        public static CompanyState Read(string text, out int version)
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
                var s = new CompanyState();
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
                            s.Age = Math.Max(0, I(c, 3));
                            s.SickPolicy = CompanyCatalogue.Clamp(I(c, 4, CompanyCatalogue.SickPolicyDefault), 0, CompanyCatalogue.SickShare.Length - 1);
                            s.PoachRest = Math.Max(0, I(c, 5));
                            s.NextClaim = Math.Max(1, I(c, 6, 1));
                            header = true;
                            break;
                        }
                        case 't':
                            Tax(s.Tax, body.Split('|'));
                            break;
                        case 's':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                if (c[0].Length > 0 && s.Shops.Count < CompanyCatalogue.ShopsMost && s.Shop(c[0]) == null)
                                {
                                    s.Shops.Add(Shop(c));
                                }
                            }
                            break;
                        case 'e':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                if (c.Length >= 2 && c[0].Length > 0 && s.Staff.Count < CompanyCatalogue.StaffMost && s.Employee(c[0]) == null)
                                {
                                    s.Staff.Add(new StaffFile
                                    {
                                        Id = c[0], Place = c[1], Standing = CompanyCatalogue.Clamp(I(c, 2, CompanyCatalogue.StandingStart), 0, CompanyCatalogue.StandingMost),
                                        Raise = Math.Max(0, I(c, 3)), UnderDays = Math.Max(0, I(c, 4)), AskDay = Math.Max(0, I(c, 5)), AskTo = Math.Max(0, I(c, 6)), AskRest = Math.Max(0, I(c, 7)),
                                        PoachDay = Math.Max(0, I(c, 8)), PoachOffer = Math.Max(0, I(c, 9)), PoachRest = Math.Max(0, I(c, 10)), LeaveDay = Math.Max(0, I(c, 11)),
                                        Rival = c.Length > 12 ? c[12] : string.Empty
                                    });
                                }
                            }
                            break;
                        case 'c':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                int kind = I(c, 2, -1), insurer = I(c, 3, -1), stage = I(c, 8);
                                if (c.Length >= 10 && kind >= 0 && kind < CompanyCatalogue.CoverKinds && insurer >= 0 && insurer < CompanyCatalogue.Insurers && s.Claims.Count < CompanyCatalogue.ClaimsMost)
                                {
                                    s.Claims.Add(new Claim
                                    {
                                        Id = Math.Max(1, I(c, 0, 1)), Place = c[1], Kind = kind, Insurer = insurer, Day = Math.Max(0, I(c, 4)), Loss = Math.Max(0, I(c, 5)), Excess = Math.Max(0, I(c, 6)),
                                        Payout = Math.Max(0, I(c, 7)), Stage = stage >= 0 && stage <= (int)ClaimStage.Lapsed ? (ClaimStage)stage : ClaimStage.Lapsed, PayDay = Math.Max(0, I(c, 9)),
                                        Name = c.Length > 10 ? c[10] : string.Empty
                                    });
                                }
                            }
                            break;
                        case 'r':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                int kind = I(c, 0, -1);
                                if (c.Length >= 12 && kind >= 0 && kind < CompanyCatalogue.CampaignKinds && s.Reports.Count < CompanyCatalogue.ReportsKept)
                                {
                                    s.Reports.Add(new CampaignReport
                                    {
                                        Kind = kind, From = I(c, 1), Days = Math.Max(0, I(c, 2)), DaysBefore = Math.Max(0, I(c, 3)), DaysDuring = Math.Max(0, I(c, 4)),
                                        CustomersBefore = Math.Max(0, I(c, 5)), CustomersDuring = Math.Max(0, I(c, 6)), TakingsBefore = I(c, 7), TakingsDuring = I(c, 8),
                                        ProfitBefore = I(c, 9), ProfitDuring = I(c, 10), Cost = Math.Max(0, I(c, 11)), Name = c.Length > 12 ? c[12] : string.Empty
                                    });
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

        private static void Tax(TaxFile x, string[] c)
        {
            x.QuarterFrom = Math.Max(0, I(c, 0));
            x.Quarter = Math.Max(1, I(c, 1, 1));
            for (int k = 0; k < x.Costs.Length; k++)
            {
                x.Costs[k] = Math.Max(0, I(c, 2 + k));
            }
            x.ReturnDue = Math.Max(0, I(c, 7));
            x.ReturnQuarter = Math.Max(0, I(c, 8));
            x.ReturnTakings = Math.Max(0, I(c, 9));
            x.ReturnCosts = Math.Max(0, I(c, 10));
            x.ReturnPenalties = CompanyCatalogue.Clamp(I(c, 11), 0, CompanyCatalogue.PenaltiesMost);
            x.LateRun = Math.Max(0, I(c, 12));
            x.Accountant = CompanyCatalogue.Clamp(I(c, 13, -1), -1, CompanyCatalogue.Accountants - 1);
            x.AccountantNext = Math.Max(0, I(c, 14));
            x.Risk = CompanyCatalogue.Clamp(I(c, 15), 0, CompanyCatalogue.RiskMost);
            x.Saved = Math.Max(0, I(c, 16));
            x.Owed = Math.Max(0, I(c, 17));
            x.LastAudit = Math.Max(0, I(c, 18));
            x.CaseNumber = Math.Max(0, I(c, 19));
        }

        private static ShopFile Shop(string[] c)
        {
            var shop = new ShopFile { Place = c[0], FirstDay = Math.Max(0, I(c, 1)) };
            int hide = I(c, 2);
            shop.Hide = Array.IndexOf(CompanyCatalogue.HideSteps, hide) >= 0 ? hide : 0;
            int push = I(c, 3, -1);
            if (push >= 0 && push < CompanyCatalogue.CampaignKinds)
            {
                shop.Push = push;
                shop.PushFrom = Math.Max(0, I(c, 4));
                shop.PushEnd = Math.Max(0, I(c, 5));
                shop.PushCost = Math.Max(0, I(c, 6));
                shop.PushPercent = CompanyCatalogue.Clamp(I(c, 7), 0, CompanyCatalogue.CampaignMost);
            }
            for (int k = 0; k < shop.PushEnded.Length; k++)
            {
                shop.PushEnded[k] = Math.Max(0, I(c, k < CompanyCatalogue.CampaignOwnKinds ? 8 + k : ShopFields + shop.Cover.Length * PolicyFields + shop.Permits.Length * LicenceFields + k - CompanyCatalogue.CampaignOwnKinds));
            }
            shop.StrikeFrom = Math.Max(0, I(c, 12));
            shop.StrikeEnd = Math.Max(0, I(c, 13));
            shop.StrikeRest = Math.Max(0, I(c, 14));
            shop.OvertimeAsk = Math.Max(0, I(c, 15));
            shop.OvertimeFrom = Math.Max(0, I(c, 16));
            shop.OvertimeEnd = Math.Max(0, I(c, 17));
            shop.OvertimeRest = Math.Max(0, I(c, 18));
            shop.OvertimeCost = Math.Max(0, I(c, 19));
            shop.LossRun = Math.Max(0, I(c, 20));
            for (int k = 0; k < shop.Cover.Length; k++)
            {
                int at = ShopFields + k * PolicyFields;
                Policy p = shop.Cover[k];
                int insurer = I(c, at, -1);
                p.Declined = I(c, at + 6) & ((1 << CompanyCatalogue.Insurers) - 1);
                if (insurer >= 0 && insurer < CompanyCatalogue.Insurers)
                {
                    p.Insurer = insurer;
                    p.Premium = Math.Max(1, I(c, at + 1, 1));
                    p.From = Math.Max(0, I(c, at + 2));
                    p.Until = Math.Max(0, I(c, at + 3));
                    p.Claims = Math.Max(0, I(c, at + 4));
                    p.Missed = CompanyCatalogue.Clamp(I(c, at + 5), 0, CompanyCatalogue.PremiumMissedMost);
                }
            }
            for (int k = 0; k < shop.Permits.Length; k++)
            {
                int at = ShopFields + shop.Cover.Length * PolicyFields + k * LicenceFields;
                shop.Permits[k].Until = Math.Max(0, I(c, at));
                shop.Permits[k].Auto = I(c, at + 1) != 0;
                shop.Permits[k].Reminded = I(c, at + 2) != 0;
            }
            return shop;
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
