using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The managers in the save, game-free: one line under <see cref="Key"/>, "v=1^h:header^m:managers^d:decisions".
    /// Parts are divided by '^', records by ';' and fields by '|'; a manager's products by '/' and a product's figures by
    /// '*'. A newer version adds fields at the end of a record, or a part with a new letter: this reader skips what it
    /// does not know, bounds every figure and caps every list. A line it cannot read is kept as it was (Core/SaveLines).
    /// </summary>
    public static class ManagerStore
    {
        public const string Key = "reallife:economy_managers";
        public const int Version = 1;

        private const int ManagerFields = 24, DecisionFields = 12;   // whole numbers before a record's words
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The state of this save; null if the save has none or it cannot be read.</summary>
        public static ManagerState Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            string text;
            if (data == null || !data.TryGetValue(Key, out text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, Key, Read(text, out version), version, Version);
        }

        public static string Write(ManagerState s)
        {
            var t = new StringBuilder(64 + s.Managers.Count * 160 + s.Decisions.Count * 80);
            t.Append("v=").Append(Version).Append("^h:");
            Join(t, s.MonthFrom, s.NextId, s.Today);
            t.Append("^m:");
            for (int i = 0, n = 0; i < s.Managers.Count && n < ManagerCatalogue.ManagersMost; i++, n++)
            {
                ManagerFile m = s.Managers[i];
                t.Append(n > 0 ? ";" : string.Empty);
                Join(t, m.Role, m.From, m.Tasks, m.WageBand, m.StockDays, m.PriceLow, m.PriceHigh, m.Budget, m.Keep, m.Spent, m.Decided, m.Mistakes, m.Undone, m.Paid,
                    m.LastDecided, m.LastMistakes, m.LastUndone, m.LastPaid, m.LastSpent, m.SnapDay, m.SnapHour, 0, 0, 0);
                t.Append('|').Append(Clean(m.Place)).Append('|').Append(Clean(m.Id)).Append('|').Append(Clean(m.Base)).Append('|');
                for (int k = 0; k < m.Snaps.Count && k < ManagerCatalogue.SnapsMost; k++)
                {
                    StockSnap snap = m.Snaps[k];
                    t.Append(k > 0 ? "/" : string.Empty).Append(Word(snap.Item)).Append('*').Append(snap.Stock.ToString(Inv)).Append('*').Append(snap.Week.ToString(Inv));
                }
                t.Append('|');
                for (int k = 0; k < m.Held.Count && k < ManagerCatalogue.HeldMost; k++)
                {
                    t.Append(k > 0 ? "/" : string.Empty).Append(Word(m.Held[k]));
                }
                t.Append('|').Append(Clean(m.Name));   // as last read: said if they are gone before the first read after a load
            }
            t.Append("^d:");
            for (int i = Math.Max(0, s.Decisions.Count - ManagerCatalogue.DecisionsKept), n = 0; i < s.Decisions.Count; i++, n++)
            {
                Decision d = s.Decisions[i];
                t.Append(n > 0 ? ";" : string.Empty);
                Join(t, d.Id, d.Day, d.Hour, d.Task, (int)d.State, d.Mistake ? 1 : 0, d.A, d.B, d.C, d.D, d.E, d.F);
                t.Append('|').Append(Clean(d.Place)).Append('|').Append(Clean(d.Subject)).Append('|').Append(Clean(d.Who)).Append('|').Append(Clean(d.Detail));
            }
            return t.ToString();
        }

        /// <summary>Reads a line; null for one that is not this feature's.</summary>
        public static ManagerState Read(string text, out int version)
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
                var s = new ManagerState();
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
                            s.MonthFrom = Math.Max(0, I(c, 0));
                            s.NextId = Math.Max(1, I(c, 1, 1));
                            s.Today = Math.Max(0, I(c, 2));
                            header = true;
                            break;
                        }
                        case 'm':
                            foreach (string record in Records(body))
                            {
                                ManagerFile m = Manager(record.Split('|'));
                                if (m != null && s.Managers.Count < ManagerCatalogue.ManagersMost && s.At(m.Place) == null)
                                {
                                    s.Managers.Add(m);
                                }
                            }
                            break;
                        case 'd':
                            foreach (string record in Records(body))
                            {
                                Decision d = DecisionOf(record.Split('|'));
                                if (d != null && s.Decisions.Count < ManagerCatalogue.DecisionsKept)
                                {
                                    s.Decisions.Add(d);
                                    s.NextId = Math.Max(s.NextId, d.Id + 1);
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

        private static ManagerFile Manager(string[] c)
        {
            if (c.Length < ManagerFields + 2 || c[ManagerFields].Length == 0 || c[ManagerFields + 1].Length == 0)
            {
                return null;
            }
            var m = new ManagerFile
            {
                Role = CompanyCatalogue.Clamp(I(c, 0), ManagerCatalogue.RolePlace, ManagerCatalogue.RoleRegion),
                From = Math.Max(0, I(c, 1)),
                Tasks = I(c, 2) & ((1 << (int)ManagerTask.Count) - 1),
                WageBand = Bound(I(c, 3, ManagerCatalogue.WageBandDefault), ManagerCatalogue.WageBands),
                StockDays = Bound(I(c, 4, ManagerCatalogue.StockDaysDefault), ManagerCatalogue.StockDays),
                PriceLow = Bound(I(c, 5, ManagerCatalogue.PriceLowDefault), ManagerCatalogue.PriceLows),
                PriceHigh = Bound(I(c, 6, ManagerCatalogue.PriceHighDefault), ManagerCatalogue.PriceHighs),
                Budget = Bound(I(c, 7, ManagerCatalogue.BudgetDefault), ManagerCatalogue.Budgets),
                Keep = CompanyCatalogue.Clamp(I(c, 8, -1), -1, ManagerCatalogue.KeepMost),
                Spent = Math.Max(0, I(c, 9)),
                Decided = Math.Max(0, I(c, 10)),
                Mistakes = Math.Max(0, I(c, 11)),
                Undone = Math.Max(0, I(c, 12)),
                Paid = Math.Max(0, I(c, 13)),
                LastDecided = Math.Max(0, I(c, 14)),
                LastMistakes = Math.Max(0, I(c, 15)),
                LastUndone = Math.Max(0, I(c, 16)),
                LastPaid = Math.Max(0, I(c, 17)),
                LastSpent = Math.Max(0, I(c, 18)),
                SnapDay = Math.Max(0, I(c, 19)),
                SnapHour = CompanyCatalogue.Clamp(I(c, 20), 0, 23),
                Place = c[ManagerFields],
                Id = c[ManagerFields + 1],
                Base = c.Length > ManagerFields + 2 ? c[ManagerFields + 2] : string.Empty
            };
            string snaps = c.Length > ManagerFields + 3 ? c[ManagerFields + 3] : string.Empty;
            foreach (string one in Records(snaps, '/'))
            {
                string[] f = one.Split('*');
                if (f.Length >= 3 && f[0].Length > 0 && m.Snaps.Count < ManagerCatalogue.SnapsMost && m.Snap(f[0]) == null)
                {
                    m.Snaps.Add(new StockSnap { Item = f[0], Stock = Math.Max(0, I(f, 1)), Week = Math.Max(0, I(f, 2)) });
                }
            }
            string held = c.Length > ManagerFields + 4 ? c[ManagerFields + 4] : string.Empty;
            foreach (string item in Records(held, '/'))
            {
                if (item.Length > 0 && m.Held.Count < ManagerCatalogue.HeldMost && !m.Holds(item))
                {
                    m.Held.Add(item);
                }
            }
            m.Name = c.Length > ManagerFields + 5 ? c[ManagerFields + 5] : string.Empty;
            return m;
        }

        private static Decision DecisionOf(string[] c)
        {
            int task = I(c, 3, -1), state = I(c, 4, -1);
            if (c.Length < DecisionFields + 1 || task < 0 || task >= (int)ManagerTask.Count || state < 0 || state > (int)DecisionState.Closed)
            {
                return null;
            }
            return new Decision
            {
                Id = Math.Max(1, I(c, 0, 1)), Day = Math.Max(0, I(c, 1)), Hour = CompanyCatalogue.Clamp(I(c, 2), 0, 23), Task = task, State = (DecisionState)state,
                Mistake = I(c, 5) != 0, A = I(c, 6), B = I(c, 7), C = I(c, 8), D = I(c, 9), E = I(c, 10), F = I(c, 11),
                Place = c[DecisionFields],
                Subject = c.Length > DecisionFields + 1 ? c[DecisionFields + 1] : string.Empty,
                Who = c.Length > DecisionFields + 2 ? c[DecisionFields + 2] : string.Empty,
                Detail = c.Length > DecisionFields + 3 ? c[DecisionFields + 3] : string.Empty
            };
        }

        private static int Bound(int step, int[] steps)
        {
            return CompanyCatalogue.Clamp(step, 0, steps.Length - 1);
        }

        private static string[] Records(string body, char divider = ';')
        {
            return string.IsNullOrEmpty(body) ? Array.Empty<string>() : body.Split(divider);
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

        /// <summary>A decision's products as its detail keeps them: "item*before*after/item*before*after".</summary>
        public static string Items(List<ItemChange> items)
        {
            var t = new StringBuilder(items.Count * 32);
            for (int i = 0; i < items.Count && i < ManagerCatalogue.ItemsInDecision; i++)
            {
                t.Append(i > 0 ? "/" : string.Empty).Append(Word(items[i].Item)).Append('*').Append(items[i].Before.ToString(Inv)).Append('*')
                    .Append(items[i].After.ToString(Inv));
            }
            return t.ToString();
        }

        /// <summary>A decision's products read back; a piece that cannot be read is left out.</summary>
        public static void Items(string detail, List<ItemChange> into)
        {
            into.Clear();
            foreach (string one in Records(detail, '/'))
            {
                string[] f = one.Split('*');
                int before, after;
                if (f.Length >= 3 && f[0].Length > 0 && int.TryParse(f[1], NumberStyles.Integer, Inv, out before) && int.TryParse(f[2], NumberStyles.Integer, Inv, out after)
                    && into.Count < ManagerCatalogue.ItemsInDecision)
                {
                    into.Add(new ItemChange { Item = f[0], Before = before, After = after });
                }
            }
        }

        /// <summary>A text with the line's three dividers taken out, so it can never break the line apart.</summary>
        public static string Clean(string text)
        {
            return CompanyStore.Clean(text);
        }

        /// <summary>A product's name with the dividers of a manager's lists taken out as well.</summary>
        public static string Word(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : CompanyStore.Clean(text).Replace('/', ' ').Replace('*', ' ');
        }
    }
}
