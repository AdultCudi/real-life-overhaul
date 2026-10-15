using System;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The living network in a save: one entry of the save's data, short lines, written with Flare's others (FlareStore).
    ///
    ///   reallife:flare_net
    ///     NT1|next case|next message|notifications read to|city day|picture day|pictures today|next picture|replies day|
    ///         replies today|reports day|reports today|message day|city seed|moved day
    ///     C|id|kind|district|place|name|x|z|filed|answer due|state|order|ref|until|post|answer|reporter|repeats|acted day   a case
    ///     W|district|kind|until                                a watch on a district
    ///     K|kind|district|day|acted                            a complaint of the week
    ///     M|id|with|name|at|mine|text|key|data|read            a message
    ///     T|day|key|name|value|name|value...                   a piece waiting for the paper
    ///     U|player   H|player   G|post   A|net id   E|tally that reached the paper   S|host key (a guest's last host and session)
    ///     B|key                                                what moved a business or the City Office on the moved day
    ///
    /// Texts are escaped as the shared city's lines are (FlareNetShare.Esc). A line this version does not read is left
    /// out; a head of another version leaves the network empty (the posts and the account are kept: they are FlareStore's).
    /// Game-free.
    /// </summary>
    public static class FlareLiveStore
    {
        public const string Key = "reallife:flare_net";
        private const string Version = "NT1";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Write(FlareNet n)
        {
            var b = new StringBuilder(1024 + n.Dms.Count * 80 + n.Cases.Count * 90);
            b.Append(Version);
            Ints(b, n.NextCase, n.NextDm);
            b.Append('|').Append(n.NoticeSeen.ToString(Inv));
            Ints(b, n.CityDay, n.PicDay, n.PicsToday, n.PicNext, n.RepliesDay, n.RepliesToday, n.ReportsDay, n.ReportsToday, n.DmDay, n.CitySeed, n.MovedDay);
            for (int i = 0; i < n.Cases.Count; i++)
            {
                FlareCase c = n.Cases[i];
                b.Append("\nC");
                Ints(b, c.Id, (int)c.Kind);
                Texts(b, c.District, c.Place, c.Name);
                b.Append('|').Append(c.X.ToString("R", Inv)).Append('|').Append(c.Z.ToString("R", Inv)).Append('|').Append(c.Filed.ToString(Inv)).Append('|').Append(c.ReplyAt.ToString(Inv));
                Ints(b, (int)c.State, (int)c.Order, c.Ref, c.Until, c.Post, c.Answer);
                Texts(b, c.Reporter);
                Ints(b, c.Repeats, c.ActedDay);
            }
            for (int i = 0; i < n.Watches.Count; i++)
            {
                b.Append("\nW");
                Texts(b, n.Watches[i].District);
                Ints(b, (int)n.Watches[i].Kind, n.Watches[i].Until);
            }
            for (int i = 0; i < n.Complaints.Count; i++)
            {
                FlareComplaint k = n.Complaints[i];
                b.Append("\nK");
                Ints(b, (int)k.Kind);
                Texts(b, k.District);
                Ints(b, k.Day, k.Acted ? 1 : 0);
            }
            for (int i = 0; i < n.Dms.Count; i++)
            {
                FlareDm m = n.Dms[i];
                b.Append("\nM");
                Ints(b, m.Id);
                Texts(b, m.With, m.Name);
                b.Append('|').Append(m.At.ToString(Inv));
                Ints(b, m.Mine ? 1 : 0);
                Texts(b, m.Text, m.Key, m.Data);
                Ints(b, m.Read ? 1 : 0);
            }
            for (int i = 0; i < n.Stories.Count; i++)
            {
                FlareStory s = n.Stories[i];
                b.Append("\nT");
                Ints(b, s.Day);
                Texts(b, s.Key);
                Texts(b, s.Data);
            }
            Each(b, "U", n.Muted);
            Each(b, "H", n.Hidden);
            Each(b, "E", n.Escalated);
            Each(b, "B", n.Moved);
            for (int i = 0; i < n.Going.Count; i++)
            {
                b.Append("\nG|").Append(n.Going[i].ToString(Inv));
            }
            if (n.HostKey != null)
            {
                b.Append("\nS");
                Texts(b, n.HostKey);
            }
            for (int i = 0; i < n.Applied.Count; i++)
            {
                b.Append("\nA|").Append(n.Applied[i].ToString(Inv));
            }
            return b.ToString();
        }

        /// <summary>The save's network into the one given (emptied first). What is not understood is left out a line at a time.</summary>
        public static void Read(FlareNet n, string text)
        {
            Clear(n);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            string[] lines = text.Split('\n');
            string[] h = lines[0].TrimEnd('\r').Split('|');
            if (h.Length < 13 || h[0] != Version)
            {
                return;
            }
            n.NextCase = Math.Max(1, I(h, 1, 1));
            n.NextDm = Math.Max(1, I(h, 2, 1));
            n.NoticeSeen = L(h, 3);
            n.CityDay = I(h, 4, -1);
            n.PicDay = I(h, 5, -1);
            n.PicsToday = Math.Max(0, I(h, 6));
            n.PicNext = Math.Max(1, I(h, 7, 1));
            n.RepliesDay = I(h, 8, -1);
            n.RepliesToday = Math.Max(0, I(h, 9));
            n.ReportsDay = I(h, 10, -1);
            n.ReportsToday = Math.Max(0, I(h, 11));
            n.DmDay = I(h, 12, -1);
            n.CitySeed = I(h, 13);
            n.MovedDay = I(h, 14, -1);   // (a save from before has none)
            for (int i = 1; i < lines.Length; i++)
            {
                string[] c = lines[i].TrimEnd('\r').Split('|');
                try
                {
                    Line(n, c);
                }
                catch (Exception)
                {
                    // one line this version cannot read: left out
                }
            }
        }

        private static void Line(FlareNet n, string[] c)
        {
            switch (c[0])
            {
                case "C":
                    if (c.Length >= 18 && n.Cases.Count < FlareNet.CasesMost)
                    {
                        int kind = I(c, 2, -1), state = I(c, 10, -1), order = I(c, 11, -1);
                        if (kind < 0 || kind > (int)FlareReport.Utilities || state < 0 || state > (int)FlareCaseState.Queued || order < 0 || order > (int)FlareOrder.SiteCheck)
                        {
                            return;
                        }
                        n.Cases.Add(new FlareCase
                        {
                            Id = I(c, 1), Kind = (FlareReport)kind, District = U(c, 3), Place = U(c, 4), Name = U(c, 5), X = F(c, 6), Z = F(c, 7), Filed = L(c, 8), ReplyAt = L(c, 9),
                            State = (FlareCaseState)state, Order = (FlareOrder)order, Ref = I(c, 12, -1), Until = I(c, 13, -1), Post = I(c, 14, -1), Answer = I(c, 15, -1),
                            Reporter = U(c, 16), Repeats = Math.Max(0, I(c, 17)), ActedDay = I(c, 18, -1)
                        });
                    }
                    break;
                case "W":
                    if (c.Length >= 4 && U(c, 1) != null)
                    {
                        int kind = I(c, 2, -1);
                        if (kind == (int)FlareOrder.Patrol || kind == (int)FlareOrder.Cleanup)
                        {
                            n.Watches.Add(new FlareWatch { District = U(c, 1), Kind = (FlareOrder)kind, Until = I(c, 3) });
                        }
                    }
                    break;
                case "K":
                    if (c.Length >= 5 && n.Complaints.Count < FlareNet.ComplaintsMost && U(c, 2) != null)
                    {
                        int kind = I(c, 1, -1);
                        if (kind >= 0 && kind <= (int)FlareReport.Utilities)
                        {
                            n.Complaints.Add(new FlareComplaint { Kind = (FlareReport)kind, District = U(c, 2), Day = I(c, 3), Acted = I(c, 4) != 0 });
                        }
                    }
                    break;
                case "M":
                    if (c.Length >= 10 && n.Dms.Count < FlareNet.DmsMost && U(c, 2) != null)
                    {
                        n.Dms.Add(new FlareDm
                        {
                            Id = I(c, 1), With = U(c, 2), Name = U(c, 3), At = L(c, 4), Mine = I(c, 5) != 0, Text = U(c, 6), Key = U(c, 7), Data = U(c, 8), Read = I(c, 9) != 0
                        });
                    }
                    break;
                case "T":
                    if (c.Length >= 3 && n.Stories.Count < FlareNet.StoriesMost && U(c, 2) != null)
                    {
                        int pairs = (c.Length - 3) / 2;
                        var data = new string[pairs * 2];
                        for (int k = 0; k < data.Length; k++)
                        {
                            data[k] = U(c, 3 + k) ?? string.Empty;
                        }
                        n.Stories.Add(new FlareStory { Day = I(c, 1), Key = U(c, 2), Data = data });
                    }
                    break;
                case "U":
                    Add(n.Muted, U(c, 1), FlareNet.MutedMost);
                    break;
                case "H":
                    Add(n.Hidden, U(c, 1), FlareNet.MutedMost);
                    break;
                case "E":
                    Add(n.Escalated, U(c, 1), 32);
                    break;
                case "B":
                    Add(n.Moved, U(c, 1), FlareLiveRules.MovedMost);
                    break;
                case "G":
                    if (c.Length >= 2 && n.Going.Count < FlareNet.GoingMost)
                    {
                        n.Going.Add(I(c, 1));
                    }
                    break;
                case "S":
                    n.HostKey = U(c, 1);
                    break;
                case "A":
                    if (c.Length >= 2 && n.Applied.Count < FlareNet.AppliedMost)
                    {
                        n.Applied.Add(I(c, 1));
                    }
                    break;
            }
        }

        public static void Clear(FlareNet n)
        {
            n.Cases.Clear();
            n.Watches.Clear();
            n.Complaints.Clear();
            n.Dms.Clear();
            n.Stories.Clear();
            n.Muted.Clear();
            n.Hidden.Clear();
            n.Going.Clear();
            n.Applied.Clear();
            n.Escalated.Clear();
            n.Moved.Clear();
            n.NextCase = n.NextDm = n.PicNext = 1;
            n.NoticeSeen = 0;
            n.CityDay = n.PicDay = n.RepliesDay = n.ReportsDay = n.DmDay = n.MovedDay = -1;
            n.PicsToday = n.RepliesToday = n.ReportsToday = 0;
            n.CitySeed = 0;
            n.HostKey = null;
        }

        private static void Add(System.Collections.Generic.List<string> list, string value, int most)
        {
            if (value != null && list.Count < most && !list.Contains(value))
            {
                list.Add(value);
            }
        }

        private static void Each(StringBuilder b, string tag, System.Collections.Generic.List<string> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                b.Append('\n').Append(tag).Append('|').Append(FlareNetShare.Esc(list[i]));
            }
        }

        private static void Ints(StringBuilder b, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                b.Append('|').Append(values[i].ToString(Inv));
            }
        }

        private static void Texts(StringBuilder b, params string[] values)
        {
            for (int i = 0; values != null && i < values.Length; i++)
            {
                b.Append('|').Append(FlareNetShare.Esc(values[i]));
            }
        }

        private static string U(string[] c, int at)
        {
            return at < c.Length ? FlareNetShare.Unesc(c[at]) : null;
        }

        private static int I(string[] c, int at, int otherwise = 0)
        {
            int value;
            return at < c.Length && int.TryParse(c[at], NumberStyles.Integer, Inv, out value) ? value : otherwise;
        }

        private static long L(string[] c, int at)
        {
            long value;
            return at < c.Length && long.TryParse(c[at], NumberStyles.Integer, Inv, out value) ? value : 0L;
        }

        private static float F(string[] c, int at)
        {
            float value;
            return at < c.Length && float.TryParse(c[at], NumberStyles.Float, Inv, out value) && !float.IsNaN(value) && !float.IsInfinity(value) ? value : 0f;
        }
    }
}
