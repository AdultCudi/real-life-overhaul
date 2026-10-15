using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Rivals
{
    /// <summary>
    /// The rival tycoons in a save: one entry of the save's data, short lines. Game-free.
    ///
    ///   reallife:rivals
    ///     RV|version|seed|start|settled|next move|next id|tier|player worth|player a week ago|week day|top day
    ///     T|index|worth|a week ago|came forward|taken over|quiet until|slow until|barred until|last move|stake day|bid day|
    ///        listed|outbid day,poach day,press day,lobby day,takeover day|break-up money|break-up day
    ///     M|id|tycoon|kind|state|announced|comes|ends|amount|second|rented|test|place|name|district|type|who   a move
    ///     C|id|tycoon|kind|day|cost|move                                                                       a counter
    ///
    /// Every number is written in the invariant culture, so what is read back is exactly what was written. A NEWER
    /// VERSION adds fields at the end of a line or lines with a new letter: this reader takes the fields it knows, skips
    /// lines it does not, and drops a move or a counter of a kind it does not know (never reads it as something else).
    /// A line it cannot read at all is kept in the save as it was read (Core/SaveLines) and the rivals start afresh.
    /// <see cref="TestKey"/> holds the same line as it was before the first test button changed it, while test changes
    /// are in place.
    /// </summary>
    public static class RivalStore
    {
        public const string Key = "reallife:rivals";
        public const string TestKey = "reallife:rivals_before_tests";
        public const string Keys = "reallife:rivals*";
        public const int Version = 1;

        private const string Head = "RV";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The save's rivals; null if it has none or none this version can read (they start afresh then).</summary>
        public static RivalFile Load(Dictionary<string, string> data)
        {
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, Key, Read(text, out int version), version, Version);   // (a line that cannot be read is kept as it is: Core/SaveLines)
        }

        public static void Save(Dictionary<string, string> data, RivalFile f)
        {
            if (data != null && f != null)
            {
                data[Key] = Write(f);
            }
        }

        public static string Write(RivalFile f)
        {
            var b = new StringBuilder(256 + f.Moves.Count * 96 + f.Counters.Count * 32);
            b.Append(Head).Append('|').Append(Version.ToString(Inv));
            Join(b, f.Seed, f.StartDay, f.SettledDay, f.NextMoveDay, f.NextId, f.Tier);
            Join(b, f.PlayerWorth, f.PlayerWeekAgo);
            Join(b, f.WeekDay, f.TopDay);
            for (int i = 0; i < f.Tycoons.Length; i++)
            {
                Tycoon t = f.Tycoons[i];
                b.Append("\nT|").Append(t.Index.ToString(Inv));
                Join(b, t.Worth, t.WeekAgo);
                Join(b, t.ActiveDay, t.DefeatedDay, t.QuietUntil, t.SlowUntil, t.BarredUntil, t.LastMoveDay, t.StakeDay, t.BidDay, t.Listed ? 1 : 0);
                b.Append('|');
                for (int k = 0; k < t.CounterDay.Length; k++)
                {
                    b.Append(k > 0 ? "," : string.Empty).Append(t.CounterDay[k].ToString(Inv));
                }
                Join(b, t.BreakUp);
                Join(b, t.BreakUpDay);
            }
            for (int i = 0; i < f.Moves.Count; i++)
            {
                RivalMove m = f.Moves[i];
                b.Append("\nM|").Append(m.Id.ToString(Inv));
                Join(b, m.Tycoon, (int)m.Kind, (int)m.State, m.Announced, m.Land, m.End);
                Join(b, m.Amount, m.Second);
                Join(b, m.Rented ? 1 : 0, m.Test ? 1 : 0);
                b.Append('|').Append(Clean(m.Place)).Append('|').Append(Clean(m.Name)).Append('|').Append(Clean(m.District)).Append('|').Append(Clean(m.Type)).Append('|').Append(Clean(m.Who));
            }
            for (int i = 0; i < f.Counters.Count; i++)
            {
                RivalCounterNote c = f.Counters[i];
                b.Append("\nC|").Append(c.Id.ToString(Inv));
                Join(b, c.Tycoon, (int)c.Kind, c.Day);
                Join(b, c.Cost);
                Join(b, c.Move);
            }
            return b.ToString();
        }

        /// <summary>A line into a file. Null for one that is not the rivals' at all or cannot be read; <paramref name="version"/> is the line's own.</summary>
        public static RivalFile Read(string text, out int version)
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
                if (h.Length < 12 || h[0] != Head || !int.TryParse(h[1], NumberStyles.Integer, Inv, out version) || version < 1)
                {
                    return null;
                }
                var f = new RivalFile
                {
                    Seed = I(h, 2), StartDay = I(h, 3), SettledDay = I(h, 4), NextMoveDay = I(h, 5), NextId = Math.Max(1, I(h, 6)), Tier = RivalCatalogue.Clamp(I(h, 7), 0, RivalCatalogue.Tiers - 1),
                    PlayerWorth = L(h, 8), PlayerWeekAgo = L(h, 9), WeekDay = I(h, 10), TopDay = I(h, 11)
                };
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] c = lines[i].TrimEnd('\r').Split('|');
                    switch (c[0])
                    {
                        case "T": Tycoon(f, c); break;
                        case "M": Move(f, c); break;
                        case "C": Counter(f, c); break;
                        // (a line of a later version's: left out)
                    }
                }
                return f;
            }
            catch (FormatException)
            {
                return null;
            }
            catch (OverflowException)
            {
                return null;
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
        }

        private static void Tycoon(RivalFile f, string[] c)
        {
            if (c.Length < 14)
            {
                return;
            }
            int index = I(c, 1);
            if (index < 0 || index >= f.Tycoons.Length)
            {
                return;   // (a tycoon of a later version's)
            }
            Tycoon t = f.Tycoons[index];
            t.Worth = Math.Max(0L, L(c, 2));
            t.WeekAgo = Math.Max(0L, L(c, 3));
            t.ActiveDay = I(c, 4);
            t.DefeatedDay = I(c, 5);
            t.QuietUntil = I(c, 6);
            t.SlowUntil = I(c, 7);
            t.BarredUntil = I(c, 8);
            t.LastMoveDay = I(c, 9);
            t.StakeDay = I(c, 10);
            t.BidDay = I(c, 11);
            t.Listed = I(c, 12) != 0;
            string[] days = c[13].Split(',');
            for (int k = 0; k < t.CounterDay.Length && k < days.Length; k++)
            {
                t.CounterDay[k] = int.Parse(days[k], NumberStyles.Integer, Inv);
            }
            if (c.Length > 15)
            {
                t.BreakUp = Math.Max(0L, L(c, 14));
                t.BreakUpDay = I(c, 15);
            }
        }

        private static void Move(RivalFile f, string[] c)
        {
            if (c.Length < 16)
            {
                return;
            }
            int kind = I(c, 3), state = I(c, 4), tycoon = I(c, 2);
            if (kind < 0 || kind >= (int)RivalMoveKind.Count || state < 0 || state >= (int)RivalMoveState.Count || tycoon < 0 || tycoon >= f.Tycoons.Length)
            {
                return;   // (a kind, a state or a tycoon this version does not know: dropped, never read as another)
            }
            if (f.Moves.Count >= RivalCatalogue.MovesKept * 2)
            {
                return;
            }
            f.Moves.Add(new RivalMove
            {
                Id = I(c, 1), Tycoon = tycoon, Kind = (RivalMoveKind)kind, State = (RivalMoveState)state, Announced = I(c, 5), Land = I(c, 6), End = I(c, 7), Amount = L(c, 8), Second = L(c, 9),
                Rented = I(c, 10) != 0, Test = I(c, 11) != 0, Place = c[12], Name = c[13], District = c[14], Type = c[15], Who = c.Length > 16 ? c[16] : string.Empty
            });
        }

        private static void Counter(RivalFile f, string[] c)
        {
            if (c.Length < 7)
            {
                return;
            }
            int kind = I(c, 3), tycoon = I(c, 2);
            if (kind < 0 || kind >= (int)RivalCounter.Count || tycoon < 0 || tycoon >= f.Tycoons.Length || f.Counters.Count >= RivalCatalogue.CountersKept)
            {
                return;
            }
            f.Counters.Add(new RivalCounterNote { Id = I(c, 1), Tycoon = tycoon, Kind = (RivalCounter)kind, Day = I(c, 4), Cost = L(c, 5), Move = I(c, 6) });
        }

        // ---------------------------------------------------------------- pieces

        private static void Join(StringBuilder b, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                b.Append('|').Append(values[i].ToString(Inv));
            }
        }

        private static void Join(StringBuilder b, params long[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                b.Append('|').Append(values[i].ToString(Inv));
            }
        }

        private static int I(string[] c, int i) => int.Parse(c[i], NumberStyles.Integer, Inv);

        private static long L(string[] c, int i) => long.Parse(c[i], NumberStyles.Integer, Inv);

        /// <summary>A name or a place as a field: no divider and no line break in it.</summary>
        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            return s.IndexOf('|') < 0 && s.IndexOf('\n') < 0 && s.IndexOf('\r') < 0 ? s : s.Replace('|', '/').Replace('\n', ' ').Replace('\r', ' ');
        }
    }
}
