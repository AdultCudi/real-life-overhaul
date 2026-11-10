using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Schemes
{
    /// <summary>
    /// The schemes in a save: one entry of the save's data, short lines.
    ///
    ///   reallife:schemes
    ///     SC1|seed|start day|day|next inquiry|cash in hand|clubs took|clubs gave back|expense day|acts so far|watched|cautioned
    ///     H|heat, a figure a kind|inquiries opened, a figure a kind
    ///     A|kind|day|gain|back|paid|size|knows|paper|inquiry|ref|place                       an act on a trail
    ///     Q|id|kind|opened|stage|evidence|lost|asked|coop|pressed|case|end|closed|found|surcharge|repaid   an inquiry
    ///     P|who|day|amount|what|talked                                                        somebody paid
    ///     C|who                                                                               somebody who said no
    ///     B|person                                                                            a contact whose next favour is bought
    ///     S|place|lift|wash|window|window from|flagged|club day|claim day                     a business's settings
    ///     R|rival|heat|acts|last act|gained|size|opened|stage|since|evidence|convictions|trial day|found|news day|news|news kind|news amount|owes
    ///     F|rival|state|day|due|amount|sham|checked|reported|number                           the fund on offer
    ///
    /// Numbers are in the invariant culture. A later version's line (SC2 and on) is read as far as this one knows it: a
    /// kind of line it does not know is skipped and fields past the ones it knows are left. A save without the entry
    /// starts with nothing in it. A first line that is not the schemes' leaves the entry unread, kept as it was read
    /// (Core/SaveLines). Game-free.
    /// </summary>
    public static class SchemeStore
    {
        public const string Key = "reallife:schemes";
        public const int Version = 1;
        private const string Prefix = "SC";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Write(SchemeState s)
        {
            var b = new StringBuilder(256 + s.Acts.Count * 48 + s.Cases.Count * 48);
            b.Append(Prefix).Append(Version.ToString(Inv));
            Add(b, s.Seed).Add(b, s.StartDay).Add(b, s.Day).Add(b, s.NextInquiry).Add(b, s.Dirty).Add(b, s.ClubTaken).Add(b, s.ClubBack).Add(b, s.ExpenseDay).Add(b, s.Acted)
                .Add(b, s.Watched ? 1 : 0).Add(b, s.Cautioned);
            b.Append("\nH");
            for (int k = 0; k < s.Heat.Length; k++)
            {
                Add(b, s.Heat[k]);
            }
            for (int k = 0; k < s.Inquiries.Length; k++)
            {
                Add(b, s.Inquiries[k]);
            }
            for (int i = 0; i < s.Acts.Count; i++)
            {
                SchemeAct a = s.Acts[i];
                b.Append("\nA");
                Add(b, (int)a.Kind).Add(b, a.Day).Add(b, a.Gain).Add(b, a.Back).Add(b, a.Paid).Add(b, a.Size).Add(b, a.Knows).Add(b, a.Paper ? 1 : 0).Add(b, a.Inquiry).Add(b, a.Ref);
                b.Append('|').Append(Clean(a.Place));
            }
            for (int i = 0; i < s.Cases.Count; i++)
            {
                SchemeInquiry q = s.Cases[i];
                b.Append("\nQ");
                Add(b, q.Id).Add(b, (int)q.Kind).Add(b, q.Opened).Add(b, (int)q.Stage).Add(b, q.Evidence).Add(b, q.Lost ? 1 : 0).Add(b, q.Asked ? 1 : 0).Add(b, q.Coop ? 1 : 0)
                    .Add(b, q.Pressed).Add(b, q.Case).Add(b, (int)q.End).Add(b, q.Closed).Add(b, q.Found).Add(b, q.Surcharge).Add(b, q.Repaid);
            }
            for (int i = 0; i < s.Paid.Count; i++)
            {
                SchemePaid p = s.Paid[i];
                b.Append("\nP|").Append(Clean(p.Who));
                Add(b, p.Day).Add(b, p.Amount).Add(b, (int)p.What).Add(b, p.Talked ? 1 : 0);
            }
            for (int i = 0; i < s.Cold.Count; i++)
            {
                b.Append("\nC|").Append(Clean(s.Cold[i]));
            }
            for (int i = 0; i < s.Bought.Count; i++)
            {
                b.Append("\nB");
                Add(b, s.Bought[i]);
            }
            for (int i = 0; i < s.Shops.Count; i++)
            {
                SchemeShop shop = s.Shops[i];
                b.Append("\nS|").Append(Clean(shop.Place));
                Add(b, shop.Lift).Add(b, shop.Wash).Add(b, shop.Window).Add(b, shop.WindowFrom).Add(b, shop.Flagged ? 1 : 0).Add(b, shop.ClubDay).Add(b, shop.ClaimDay);
            }
            for (int i = 0; i < s.Rival.Length; i++)
            {
                SchemeRival r = s.Rival[i];
                b.Append("\nR");
                Add(b, i).Add(b, r.Heat).Add(b, r.Acts).Add(b, r.LastAct).Add(b, r.Gained).Add(b, r.Size).Add(b, r.Opened).Add(b, (int)r.Stage).Add(b, r.Since).Add(b, r.Evidence)
                    .Add(b, r.Convictions).Add(b, r.TrialDay).Add(b, r.Found).Add(b, r.NewsDay).Add(b, r.News).Add(b, (int)r.NewsKind).Add(b, r.NewsAmount).Add(b, r.Owes);
            }
            SchemeFund f = s.Fund;
            b.Append("\nF");
            Add(b, f.Tycoon).Add(b, (int)f.State).Add(b, f.Day).Add(b, f.Due).Add(b, f.Amount).Add(b, f.Sham ? 1 : 0).Add(b, f.Checked ? 1 : 0).Add(b, f.Reported ? 1 : 0)
                .Add(b, f.Number);
            return b.ToString();
        }

        /// <summary>The state from the save's line and its version; null if it is not the schemes' line at all.</summary>
        public static SchemeState Read(string text, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            try
            {
                string[] lines = text.Split('\n');
                string[] h = lines[0].TrimEnd('\r').Split('|');
                if (!h[0].StartsWith(Prefix, StringComparison.Ordinal) || !int.TryParse(h[0].Substring(Prefix.Length), NumberStyles.Integer, Inv, out version) || version < 1)
                {
                    version = 0;
                    return null;
                }
                var s = new SchemeState
                {
                    Seed = I(h, 1, 0), StartDay = I(h, 2, 0), Day = I(h, 3, -1), NextInquiry = Math.Max(1, I(h, 4, 1)), Dirty = Math.Max(0L, L(h, 5)),
                    ClubTaken = Math.Max(0L, L(h, 6)), ClubBack = Math.Max(0L, L(h, 7)), ExpenseDay = I(h, 8, -1000), Acted = Math.Max(0, I(h, 9, 0)), Watched = I(h, 10, 0) != 0,
                    Cautioned = Math.Max(0, I(h, 11, 0))
                };
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] c = lines[i].TrimEnd('\r').Split('|');
                    switch (c[0])
                    {
                        case "H":
                            Heat(s, c);
                            break;
                        case "A":
                            Act(s, c);
                            break;
                        case "Q":
                            Inquiry(s, c);
                            break;
                        case "P":
                            if (c.Length >= 6 && s.Paid.Count < SchemeCatalogue.PaidMost && c[1].Length > 0 && I(c, 4, -1) >= 0 && I(c, 4, -1) <= (int)Favour.Judge)
                            {
                                s.Paid.Add(new SchemePaid { Who = c[1], Day = I(c, 2, 0), Amount = Math.Max(0L, L(c, 3)), What = (Favour)I(c, 4, 0), Talked = I(c, 5, 0) != 0 });
                            }
                            break;
                        case "C":
                            if (c.Length >= 2 && c[1].Length > 0 && s.Cold.Count < SchemeCatalogue.ColdMost && !s.Cold.Contains(c[1]))
                            {
                                s.Cold.Add(c[1]);
                            }
                            break;
                        case "B":
                            if (c.Length >= 2 && I(c, 1, -1) >= 0 && s.Bought.Count < SchemeCatalogue.BoughtMost && !s.Bought.Contains(I(c, 1, -1)))
                            {
                                s.Bought.Add(I(c, 1, -1));
                            }
                            break;
                        case "S":
                            Shop(s, c);
                            break;
                        case "R":
                            Rival(s, c);
                            break;
                        case "F":
                            Fund(s, c);
                            break;
                    }
                }
                return s;
            }
            catch (Exception)
            {
                version = 0;
                return null;
            }
        }

        private static void Heat(SchemeState s, string[] c)
        {
            for (int k = 0; k < s.Heat.Length; k++)
            {
                s.Heat[k] = SchemeCatalogue.Clamp(I(c, 1 + k, 0), 0, SchemeCatalogue.HeatMost);
                s.Inquiries[k] = Math.Max(0, I(c, 1 + s.Heat.Length + k, 0));
            }
        }

        private static void Act(SchemeState s, string[] c)
        {
            int kind = I(c, 1, -1);
            if (c.Length < 12 || kind < 0 || kind >= SchemeCatalogue.Kinds || s.Acts.Count >= SchemeCatalogue.ActsMost * SchemeCatalogue.Kinds)
            {
                return;
            }
            long gain = Math.Max(0L, L(c, 3));
            s.Acts.Add(new SchemeAct
            {
                Kind = (SchemeKind)kind, Day = I(c, 2, 0), Gain = gain, Back = Math.Min(gain, Math.Max(0L, L(c, 4))), Paid = Math.Max(0L, L(c, 5)),
                Size = SchemeCatalogue.Clamp(I(c, 6, 0), 0, SchemeCatalogue.Large), Knows = Math.Max(0, I(c, 7, 0)), Paper = I(c, 8, 0) != 0, Inquiry = Math.Max(0, I(c, 9, 0)),
                Ref = I(c, 10, 0), Place = c[11]
            });
        }

        private static void Inquiry(SchemeState s, string[] c)
        {
            int id = I(c, 1, 0), kind = I(c, 2, -1), stage = I(c, 4, -1), end = I(c, 11, 0);
            if (c.Length < 15 || id <= 0 || kind < 0 || kind >= SchemeCatalogue.Kinds || stage < 0 || stage > (int)InquiryStage.Closed || end < 0 || end > (int)InquiryEnd.Settled
                || s.Inquiry(id) != null)
            {
                return;
            }
            var q = new SchemeInquiry
            {
                Id = id, Kind = (SchemeKind)kind, Opened = I(c, 3, 0), Stage = (InquiryStage)stage, Evidence = SchemeCatalogue.Clamp(I(c, 5, 0), 0, SchemeCatalogue.EvidenceMost),
                Lost = I(c, 6, 0) != 0, Asked = I(c, 7, 0) != 0, Coop = I(c, 8, 0) != 0, Pressed = Math.Max(0, I(c, 9, 0)), Case = Math.Max(0, I(c, 10, 0)), End = (InquiryEnd)end,
                Closed = I(c, 12, 0), Found = Math.Max(0L, L(c, 13)), Surcharge = Math.Max(0L, L(c, 14)), Repaid = Math.Max(0L, L(c, 15))
            };
            if (q.Open && s.OpenOf(q.Kind) != null)
            {
                return;   // never two open for a kind
            }
            s.Cases.Add(q);
            s.NextInquiry = Math.Max(s.NextInquiry, id + 1);
        }

        private static void Shop(SchemeState s, string[] c)
        {
            if (c.Length < 9 || c[1].Length == 0 || s.Shop(c[1], false) != null || s.Shops.Count >= 64)
            {
                return;
            }
            s.Shops.Add(new SchemeShop
            {
                Place = c[1], Lift = SchemeRules.LiftStep(I(c, 2, 0)), Wash = SchemeRules.WashStep(I(c, 3, 0)), Window = Math.Max(0L, L(c, 4)), WindowFrom = I(c, 5, 0),
                Flagged = I(c, 6, 0) != 0, ClubDay = I(c, 7, -1000), ClaimDay = I(c, 8, -1000)
            });
        }

        private static void Rival(SchemeState s, string[] c)
        {
            int at = I(c, 1, -1), stage = I(c, 8, (int)InquiryStage.Closed), kind = I(c, 16, 0);
            if (c.Length < 18 || at < 0 || at >= s.Rival.Length || stage < 0 || stage > (int)InquiryStage.Closed)
            {
                return;
            }
            SchemeRival r = s.Rival[at];
            r.Heat = SchemeCatalogue.Clamp(I(c, 2, 0), 0, SchemeCatalogue.HeatMost);
            r.Acts = Math.Max(0, I(c, 3, 0));
            r.LastAct = I(c, 4, -1000);
            r.Gained = Math.Max(0L, L(c, 5));
            r.Size = SchemeCatalogue.Clamp(I(c, 6, 0), 0, SchemeCatalogue.Large);
            r.Opened = Math.Max(0, I(c, 7, 0));
            r.Stage = (InquiryStage)stage;
            r.Since = I(c, 9, 0);
            r.Evidence = SchemeCatalogue.Clamp(I(c, 10, 0), 0, SchemeCatalogue.EvidenceMost);
            r.Convictions = Math.Max(0, I(c, 11, 0));
            r.TrialDay = Math.Max(0, I(c, 12, 0));
            r.Found = Math.Max(0L, L(c, 13));
            r.NewsDay = I(c, 14, -1000);
            r.News = SchemeCatalogue.Clamp(I(c, 15, 0), 0, SchemeRules.RivalNewsCleared);
            r.NewsKind = (SchemeKind)SchemeCatalogue.Clamp(kind, 0, SchemeCatalogue.Kinds - 1);
            r.NewsAmount = Math.Max(0L, L(c, 17));
            r.Owes = Math.Max(0L, L(c, 18));
        }

        private static void Fund(SchemeState s, string[] c)
        {
            int state = I(c, 2, 0);
            if (c.Length < 10 || state < 0 || state > (int)FundState.Declined)
            {
                return;
            }
            SchemeFund f = s.Fund;
            f.Tycoon = SchemeCatalogue.Clamp(I(c, 1, -1), -1, s.Rival.Length - 1);
            f.State = f.Tycoon < 0 ? FundState.None : (FundState)state;
            f.Day = I(c, 3, 0);
            f.Due = I(c, 4, 0);
            f.Amount = Math.Max(0L, L(c, 5));
            f.Sham = I(c, 6, 0) != 0;
            f.Checked = I(c, 7, 0) != 0;
            f.Reported = I(c, 8, 0) != 0;
            f.Number = Math.Max(0, I(c, 9, 0));
        }

        /// <summary>
        /// The save's state: a new one with the seed given for a save without the entry, what the entry holds otherwise, or
        /// null when the entry is not one this version can read at all (it is then kept as it was read).
        /// </summary>
        public static SchemeState Load(Dictionary<string, string> data, int seed, out int version)
        {
            version = Version;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return new SchemeState { Seed = seed };
            }
            SchemeState s = SaveLines.Checked(data, Key, Read(text, out version), version, Version);
            if (s != null && s.Seed == 0)
            {
                s.Seed = seed;
            }
            return s;
        }

        public static void Save(Dictionary<string, string> data, SchemeState s)
        {
            if (data != null && s != null)
            {
                data[Key] = Write(s);
            }
        }

        private static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace("|", " ").Replace("\n", " ").Replace("\r", string.Empty);
        }

        private static StringBuilder Add(this StringBuilder chain, StringBuilder b, long value)
        {
            return b.Append('|').Append(value.ToString(Inv));
        }

        private static StringBuilder Add(StringBuilder b, long value)
        {
            return b.Append('|').Append(value.ToString(Inv));
        }

        private static int I(string[] a, int at, int fallback)
        {
            return at < a.Length && int.TryParse(a[at], NumberStyles.Integer, Inv, out int v) ? v : fallback;
        }

        private static long L(string[] a, int at)
        {
            return at < a.Length && long.TryParse(a[at], NumberStyles.Integer, Inv, out long v) ? v : 0L;
        }
    }
}
