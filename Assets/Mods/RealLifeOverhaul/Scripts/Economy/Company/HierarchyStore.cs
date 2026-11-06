using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RealLifeOverhaul.Careers;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The company's hierarchy in the save, game-free: one line under <see cref="Key"/>,
    /// "v=1^h:header^c:company^r:regions^s:stores^t:templates^w:watches^e:escalations^l:log^x:executives^a:regions picked".
    /// Parts are divided by '^', records by ';', fields by '|', and a policy's steps or a watch's days by '*'. A newer
    /// version adds fields at the end of a record, or a part with a new letter: this reader skips what it does not know,
    /// bounds every figure and caps every list. A line it cannot read is kept as it was (Core/SaveLines). A save without it
    /// is a company at the starting policy, which decides exactly as the managers did before.
    /// </summary>
    public static class HierarchyStore
    {
        public const string Key = "reallife:company_hierarchy";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The state of this save; null if the save has none or it cannot be read.</summary>
        public static HierarchyState Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            string text;
            if (data == null || !data.TryGetValue(Key, out text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, Key, Read(text, out version), version, Version);
        }

        public static string Write(HierarchyState s)
        {
            var t = new StringBuilder(256 + s.Escalations.Count * 64 + s.Entries.Count * 56 + (s.Regions.Count + s.Stores.Count) * 80);
            t.Append("v=").Append(Version).Append("^h:");
            Join(t, s.NextId, s.NextTemplate, s.WeekFrom, s.Settled, s.Deep ? 1 : 0, s.MonthFrom);
            t.Append("^c:");
            Steps(t, s.Company);
            t.Append("^r:");
            Sets(t, s.Regions, PolicyCatalogue.RegionsMost);
            t.Append("^s:");
            Sets(t, s.Stores, PolicyCatalogue.StoresMost);
            t.Append("^t:");
            for (int i = 0; i < s.Templates.Count && i < PolicyCatalogue.TemplatesMost; i++)
            {
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, s.Templates[i].Number, s.Templates[i].Day);
                t.Append('|');
                Steps(t, s.Templates[i].Set);
            }
            t.Append("^w:");
            for (int i = 0, n = 0; i < s.Watches.Count && n < HierarchyState.WatchesMost; i++)
            {
                PlaceWatch w = s.Watches[i];
                t.Append(n++ > 0 ? ";" : string.Empty);
                Join(t, w.GapFrom, w.LowFrom, w.KpiRun, w.KpiDay, w.Staff, w.TopUp, w.TopUpFrom);
                t.Append('|').Append(Clean(w.Place)).Append('|');
                for (int k = 0; k < w.Left.Count && k < 8; k++)
                {
                    t.Append(k > 0 ? "*" : string.Empty).Append(w.Left[k].ToString(Inv));
                }
            }
            t.Append("^e:");
            for (int i = 0; i < s.Escalations.Count && i < HierarchyState.EscalationsKept; i++)
            {
                Escalation e = s.Escalations[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, e.Id, e.Kind, e.Level, e.Day, e.Moved, e.State, e.A, e.B, e.C);
                t.Append('|').Append(Clean(e.Place)).Append('|').Append(Clean(e.Subject)).Append('|').Append(Clean(e.Detail)).Append('|').Append(Clean(e.By));
            }
            t.Append("^l:");
            for (int i = Math.Max(0, s.Entries.Count - HierarchyState.EntriesKept), n = 0; i < s.Entries.Count; i++)
            {
                HierarchyEntry e = s.Entries[i];
                t.Append(n++ > 0 ? ";" : string.Empty);
                Join(t, e.Id, e.Day, e.Hour, e.Kind, e.Level, e.A, e.B, e.C);
                t.Append('|').Append(Clean(e.Who)).Append('|').Append(Clean(e.Place)).Append('|').Append(Clean(e.Detail));
            }
            t.Append("^x:");
            for (int i = 0; i < s.Executives.Count && i < ExecutiveHiring.Seats.Length; i++)
            {
                ExecutiveFile x = s.Executives[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, x.Area, x.Role, x.Level, x.From, x.Decided, x.Paid, x.LastDecided, x.LastPaid, x.ProposedDay);
                t.Append('|').Append(Clean(x.Id)).Append('|').Append(Clean(x.Name)).Append('|').Append(Clean(x.Place));
            }
            t.Append("^a:");
            for (int i = 0; i < s.AreaPicks.Count && i < HierarchyState.AreaPicksMost; i++)
            {
                t.Append(i > 0 ? ";" : string.Empty).Append(Clean(s.AreaPicks[i].Place)).Append('|').Append(Clean(s.AreaPicks[i].Area));
            }
            return t.ToString();
        }

        private static void Sets(StringBuilder t, List<PolicySet> sets, int most)
        {
            for (int i = 0, n = 0; i < sets.Count && n < most; i++)
            {
                if (sets[i].Count == 0)
                {
                    continue;   // a scope that sets nothing is the one above: nothing to keep
                }
                t.Append(n++ > 0 ? ";" : string.Empty).Append(Clean(sets[i].Scope)).Append('|');
                Steps(t, sets[i]);
            }
        }

        private static void Steps(StringBuilder t, PolicySet set)
        {
            for (int f = 0; f < set.Steps.Length; f++)
            {
                t.Append(f > 0 ? "*" : string.Empty).Append(set.Steps[f].ToString(Inv));
            }
        }

        /// <summary>Reads a line; null for one that is not this feature's.</summary>
        public static HierarchyState Read(string text, out int version)
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
                var s = new HierarchyState();
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
                            s.NextId = Math.Max(1, I(c, 0, 1));
                            s.NextTemplate = Math.Max(1, I(c, 1, 1));
                            s.WeekFrom = Math.Max(0, I(c, 2));
                            s.Settled = Math.Max(0, I(c, 3));
                            s.Deep = I(c, 4) != 0;
                            s.MonthFrom = Math.Max(0, I(c, 5));
                            header = true;
                            break;
                        }
                        case 'c':
                            ReadSteps(body, s.Company, true);
                            break;
                        case 'r':
                        case 's':
                        {
                            bool region = part[0] == 'r';
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                if (c.Length >= 2 && c[0].Length > 0 && (region ? s.Region(c[0]) : s.Store(c[0])) == null)
                                {
                                    PolicySet set = s.Want(region, c[0]);
                                    if (set != null)
                                    {
                                        ReadSteps(c[1], set, false);
                                    }
                                }
                            }
                            break;
                        }
                        case 't':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                if (c.Length >= 3 && s.Templates.Count < PolicyCatalogue.TemplatesMost)
                                {
                                    var tpl = new PolicyTemplate { Number = Math.Max(1, I(c, 0, 1)), Day = Math.Max(0, I(c, 1)) };
                                    ReadSteps(c[2], tpl.Set, true);
                                    s.Templates.Add(tpl);
                                    s.NextTemplate = Math.Max(s.NextTemplate, tpl.Number + 1);
                                }
                            }
                            break;
                        case 'w':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                if (c.Length < 8 || c[7].Length == 0 || s.Watch(c[7], false) != null)
                                {
                                    continue;
                                }
                                PlaceWatch w = s.Watch(c[7], true);
                                if (w == null)
                                {
                                    continue;
                                }
                                w.GapFrom = Math.Max(0, I(c, 0));
                                w.LowFrom = Math.Max(0, I(c, 1));
                                w.KpiRun = CompanyCatalogue.Clamp(I(c, 2), 0, 999);
                                w.KpiDay = Math.Max(0, I(c, 3));
                                w.Staff = CompanyCatalogue.Clamp(I(c, 4, -1), -1, 9999);
                                w.TopUp = Math.Max(0, I(c, 5));
                                w.TopUpFrom = Math.Max(0, I(c, 6));
                                string[] left = c.Length > 8 && c[8].Length > 0 ? c[8].Split('*') : Array.Empty<string>();
                                for (int k = 0; k < left.Length && w.Left.Count < 8; k++)
                                {
                                    int day;
                                    if (int.TryParse(left[k], NumberStyles.Integer, Inv, out day) && day > 0)
                                    {
                                        w.Left.Add(day);
                                    }
                                }
                            }
                            break;
                        case 'e':
                            foreach (string record in Records(body))
                            {
                                Escalation e = EscalationOf(record.Split('|'));
                                if (e != null && s.Escalations.Count < HierarchyState.EscalationsKept)
                                {
                                    s.Escalations.Add(e);
                                    s.NextId = Math.Max(s.NextId, e.Id + 1);
                                }
                            }
                            break;
                        case 'l':
                            foreach (string record in Records(body))
                            {
                                HierarchyEntry e = EntryOf(record.Split('|'));
                                if (e != null && s.Entries.Count < HierarchyState.EntriesKept)
                                {
                                    s.Entries.Add(e);
                                    s.NextId = Math.Max(s.NextId, e.Id + 1);
                                }
                            }
                            break;
                        case 'x':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                int area = I(c, 0, -1);
                                if (c.Length < 10 || !ExecutiveHiring.Real(area) || c[9].Length == 0 || s.Executive((ExecSeat)area) != null)
                                {
                                    continue;
                                }
                                s.Executives.Add(new ExecutiveFile
                                {
                                    Area = area, Role = CompanyCatalogue.Clamp(I(c, 1), 0, 999), Level = CompanyCatalogue.Clamp(I(c, 2), 0, 9), From = Math.Max(0, I(c, 3)),
                                    Decided = Math.Max(0, I(c, 4)), Paid = Math.Max(0, I(c, 5)), LastDecided = Math.Max(0, I(c, 6)), LastPaid = Math.Max(0, I(c, 7)),
                                    ProposedDay = Math.Max(0, I(c, 8)), Id = c[9], Name = c.Length > 10 ? c[10] : string.Empty, Place = c.Length > 11 ? c[11] : string.Empty
                                });
                            }
                            break;
                        case 'a':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                if (c.Length >= 2 && c[0].Length > 0 && c[1].Length > 0 && s.AreaPicked(c[0]) == null)
                                {
                                    s.PickArea(c[0], c[1]);
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

        // A set's steps, each bounded by its rule; a rule the line lacks is the company's start (whole) or unset (a scope's).
        private static void ReadSteps(string body, PolicySet into, bool whole)
        {
            string[] c = string.IsNullOrEmpty(body) ? Array.Empty<string>() : body.Split('*');
            for (int f = 0; f < into.Steps.Length; f++)
            {
                PolicyDef d = PolicyCatalogue.Defs[f];
                int step = I(c, f, whole ? d.Default : -1);
                into.Steps[f] = step < 0 ? (whole ? d.Default : -1) : CompanyCatalogue.Clamp(step, 0, d.Steps.Length - 1);
                if (!whole && d.Way == PolicyWay.CompanyOnly)
                {
                    into.Steps[f] = -1;   // only the company sets it, whatever a line says
                }
            }
        }

        private static Escalation EscalationOf(string[] c)
        {
            int kind = I(c, 1, -1), state = I(c, 5, -1);
            if (c.Length < 10 || kind < 0 || kind >= (int)EscalationKind.Count || state < 0 || state > (int)EscalationState.Lapsed)
            {
                return null;
            }
            return new Escalation
            {
                Id = Math.Max(1, I(c, 0, 1)), Kind = kind, Level = CompanyCatalogue.Clamp(I(c, 2, EscalationRules.LevelOwner), EscalationRules.LevelRegion, EscalationRules.LevelOwner),
                Day = Math.Max(0, I(c, 3)), Moved = Math.Max(0, I(c, 4)), State = state, A = I(c, 6), B = I(c, 7), C = I(c, 8),
                Place = c[9], Subject = c.Length > 10 ? c[10] : string.Empty, Detail = c.Length > 11 ? c[11] : string.Empty, By = c.Length > 12 ? c[12] : string.Empty
            };
        }

        private static HierarchyEntry EntryOf(string[] c)
        {
            int kind = I(c, 3, -1);
            if (c.Length < 9 || kind < 0 || kind > (int)HierarchyLog.Policy)
            {
                return null;
            }
            return new HierarchyEntry
            {
                Id = Math.Max(1, I(c, 0, 1)), Day = Math.Max(0, I(c, 1)), Hour = CompanyCatalogue.Clamp(I(c, 2), 0, 23), Kind = kind,
                Level = CompanyCatalogue.Clamp(I(c, 4), 0, (int)Rung.Owner), A = I(c, 5), B = I(c, 6), C = I(c, 7),
                Who = c[8], Place = c.Length > 9 ? c[9] : string.Empty, Detail = c.Length > 10 ? c[10] : string.Empty
            };
        }

        private static string[] Records(string body)
        {
            return string.IsNullOrEmpty(body) ? Array.Empty<string>() : body.Split(';');
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

        /// <summary>A text with the line's dividers taken out, the steps' and days' one as well, so it can never break the line apart.</summary>
        public static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : CompanyStore.Clean(text).Replace('*', ' ');
        }
    }
}
