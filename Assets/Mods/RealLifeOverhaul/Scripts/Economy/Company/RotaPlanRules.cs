using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The crew a business wants on shift in a block of hours of every day, whatever the customers say: a floor under the planner's own need.</summary>
    public struct RotaBlock
    {
        public int From, To;    // hours of the day: from the first to the end of the last (24 at most)
        public int Crew;        // people on shift at least
    }

    /// <summary>
    /// One named plan of a business's rota: how the planner weighs the week while it is in use. The rota planner's own mode (Auto, Staff first, Lean) stays the
    /// business's; a plan adds what the player can ask of any week: the most hours each person may be placed, whether overtime may go past it, the least crew
    /// in a block of hours, the weeks it repeats on and the days it holds for. Saved under <see cref="RotaPlanStore.Key"/>.
    /// </summary>
    public sealed class RotaPlanFile
    {
        public int Id;
        public string Name = string.Empty;
        public int From, To;                    // the first and the last day it holds for; 0 and 0 for always
        public int Parity;                      // RotaPlanRules.Every, Odd or Even: the weeks it applies to
        public int Hours = RotaPlanRules.HoursStart;   // most hours a week for each person
        public bool Overtime;                   // people may be placed up to RotaPlanRules.OvertimeExtra hours past it
        public readonly List<RotaBlock> Blocks = new List<RotaBlock>(RotaPlanRules.BlocksMost);

        /// <summary>Raised on every change, so that what was worked out for the plan before is not reused. Never saved.</summary>
        public int Version;

        public bool Dated => From > 0 || To > 0;
    }

    /// <summary>A business's plans and which one it falls back on.</summary>
    public sealed class RotaPlanSet
    {
        public string Place = string.Empty;
        public int Active;                      // the plan used when no dated plan holds for the week; 0: the first undated one
        public int NextId = 1;
        public readonly List<RotaPlanFile> Plans = new List<RotaPlanFile>(RotaPlanRules.PlansMost);

        public RotaPlanFile Find(int id)
        {
            for (int i = 0; i < Plans.Count; i++)
            {
                if (Plans[i].Id == id)
                {
                    return Plans[i];
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Rota plans, game-free (EconomyCheck compiles them): which plan is in use for a week (a dated one holding for it, else the chosen one, by the weeks it
    /// repeats on), the floor of crew it puts under the planner's need, the cap it puts on each person's hours, a plan copied, and the save line.
    /// </summary>
    public static class RotaPlanRules
    {
        public const int PlansMost = 8, BlocksMost = 4, NameMost = 24;
        public const int Every = 0, Odd = 1, Even = 2;                    // the weeks a plan repeats on, counted from the week of day 1
        public const int HoursStart = 0, HoursLeast = 8, HoursTop = 80;   // hours a week a person may be placed; 0 is their own hours, as the planner has always used them
        public const int OvertimeExtra = 8;                               // hours past the cap that overtime allows
        public const int CrewMost = 12;                                   // people a block may ask for

        /// <summary>The week a day is in, counted from 0 at day 1 (a Monday).</summary>
        public static int WeekOf(int day)
        {
            return Math.Max(0, day - 1) / RotaCatalogue.Days;
        }

        /// <summary>Whether a plan applies to the week a day is in: its weeks, and the days it holds for (a week it touches at all).</summary>
        public static bool Applies(RotaPlanFile p, int day)
        {
            int week = WeekOf(day);
            bool weeks = p.Parity == Every || (p.Parity == Odd ? week % 2 == 0 : week % 2 == 1);   // the first week is week 1: odd
            if (!weeks)
            {
                return false;
            }
            if (!p.Dated)
            {
                return true;
            }
            int monday = week * RotaCatalogue.Days + 1, sunday = monday + RotaCatalogue.Days - 1;
            return (p.From <= 0 || p.From <= sunday) && (p.To <= 0 || p.To >= monday);
        }

        /// <summary>
        /// The plan in use for the week a day is in: a dated plan that holds for it (the latest to begin, then the lowest number), else the chosen plan if it
        /// applies to that week, else the first undated plan that does. Null when the business has none that does, and the planner's own rules stand.
        /// </summary>
        public static RotaPlanFile InUse(RotaPlanSet set, int day)
        {
            if (set == null || set.Plans.Count == 0)
            {
                return null;
            }
            RotaPlanFile dated = null;
            for (int i = 0; i < set.Plans.Count; i++)
            {
                RotaPlanFile p = set.Plans[i];
                if (p.Dated && Applies(p, day) && (dated == null || p.From > dated.From || (p.From == dated.From && p.Id < dated.Id)))
                {
                    dated = p;
                }
            }
            if (dated != null)
            {
                return dated;
            }
            RotaPlanFile chosen = set.Find(set.Active);
            if (chosen != null && !chosen.Dated && Applies(chosen, day))
            {
                return chosen;
            }
            for (int i = 0; i < set.Plans.Count; i++)
            {
                if (!set.Plans[i].Dated && Applies(set.Plans[i], day))
                {
                    return set.Plans[i];
                }
            }
            return null;
        }

        /// <summary>The most hours a week each person may be placed under a plan, overtime included.</summary>
        public static int Cap(RotaPlanFile p)
        {
            return p == null || p.Hours <= 0 ? 0 : Math.Min(HoursTop + OvertimeExtra, p.Hours + (p.Overtime ? OvertimeExtra : 0));
        }

        /// <summary>A person's hours demands with the plan's cap put on: the upper end of an hours demand (or one made, high priority) is the cap if it is looser.</summary>
        public static RotaDemand[] Capped(RotaDemand[] demands, RotaPlanFile p)
        {
            int cap = Cap(p);
            if (cap <= 0 || demands == null)
            {
                return demands;
            }
            for (int i = 0; i < demands.Length; i++)
            {
                if (demands[i].Kind != RotaDemandKind.Hours)
                {
                    continue;
                }
                if (demands[i].B == 0 || demands[i].B > cap)
                {
                    demands[i].B = cap;
                    demands[i].A = Math.Min(demands[i].A, cap);
                }
                return demands;
            }
            if (demands.Length >= RotaCatalogue.DemandsMost)
            {
                return demands;
            }
            var more = new RotaDemand[demands.Length + 1];
            Array.Copy(demands, more, demands.Length);
            more[demands.Length] = new RotaDemand { Kind = RotaDemandKind.Hours, Priority = RotaCatalogue.PriorityHigh, A = 0, B = cap };
            return more;
        }

        /// <summary>The floor of a plan by hour of the week: the most any of its blocks asks for each cell (day * 24 + hour); all cleared first.</summary>
        public static void FloorCells(RotaPlanFile p, int[] cells)
        {
            Array.Clear(cells, 0, cells.Length);
            for (int b = 0; p != null && b < p.Blocks.Count; b++)
            {
                RotaBlock block = p.Blocks[b];
                for (int d = 0; d < RotaCatalogue.Days; d++)
                {
                    for (int h = Math.Max(0, block.From); h < Math.Min(RotaCatalogue.Hours, block.To); h++)
                    {
                        int cell = d * RotaCatalogue.Hours + h;
                        cells[cell] = Math.Max(cells[cell], Math.Min(CrewMost, Math.Max(0, block.Crew)));
                    }
                }
            }
        }

        /// <summary>
        /// The floor put under a week's worth: in every open cell where fewer stations are wanted than the plan asks for, stations are wanted too, at the
        /// least worth, in the station order the planner reads them (those left alone, and cleaning ones, apart). Returns how many were added; a closed
        /// hour never asks for anybody.
        /// </summary>
        public static int Lift(int[] floor, bool[] open, int stations, int[] value, bool[] taken, byte[] groupKindOfStation, int[] groupOf, int least)
        {
            int added = 0;
            for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
            {
                if (floor[cell] <= 0 || !open[cell])
                {
                    continue;
                }
                int wanted = 0;
                for (int s = 0; s < stations; s++)
                {
                    wanted += value[cell * stations + s] > 0 || taken[cell * stations + s] ? 1 : 0;
                }
                for (int s = 0; s < stations && wanted < floor[cell]; s++)
                {
                    int i = cell * stations + s;
                    if (value[i] > 0 || taken[i] || groupKindOfStation[groupOf[s]] == RotaGroupKind.Cleaning)
                    {
                        continue;
                    }
                    value[i] = Math.Max(1, least);
                    wanted++;
                    added++;
                }
            }
            return added;
        }

        // ---------------------------------------------------------------- the plans themselves

        /// <summary>The plan every business starts with: every week, the people's own hours, no overtime, no floor.</summary>
        public static RotaPlanFile Starting(int id, string name)
        {
            return new RotaPlanFile { Id = id, Name = Name(name) };
        }

        /// <summary>A plan's name as it may be kept: no dividers, a short one, never empty.</summary>
        public static string Name(string text)
        {
            string clean = CompanyStore.Clean(text ?? string.Empty).Trim();
            if (clean.Length > NameMost)
            {
                clean = clean.Substring(0, NameMost).TrimEnd();
            }
            return clean.Length == 0 ? "-" : clean;
        }

        /// <summary>
        /// A copy of a plan under a new number, the name given; null when the business already keeps <see cref="PlansMost"/>. The copy holds for no days and
        /// repeats every week until it is changed, so a copy never silently takes over from the plan it was made of.
        /// </summary>
        public static RotaPlanFile Copy(RotaPlanSet set, int id, string name)
        {
            RotaPlanFile from = set.Find(id);
            if (from == null || set.Plans.Count >= PlansMost)
            {
                return null;
            }
            var copy = new RotaPlanFile { Id = set.NextId++, Name = Name(name), Hours = from.Hours, Overtime = from.Overtime };
            for (int i = 0; i < from.Blocks.Count; i++)
            {
                copy.Blocks.Add(from.Blocks[i]);
            }
            set.Plans.Add(copy);
            return copy;
        }

        /// <summary>A new plan, made as the starting one; null when the business already keeps <see cref="PlansMost"/>.</summary>
        public static RotaPlanFile Add(RotaPlanSet set, string name)
        {
            if (set.Plans.Count >= PlansMost)
            {
                return null;
            }
            RotaPlanFile p = Starting(set.NextId++, name);
            set.Plans.Add(p);
            return p;
        }

        /// <summary>A plan put right: its numbers inside what the planner can use, its blocks sorted, inside the day, none overlapping, at most <see cref="BlocksMost"/>.</summary>
        public static void Tidy(RotaPlanFile p)
        {
            p.Hours = p.Hours <= 0 ? 0 : CompanyCatalogue.Clamp(p.Hours, HoursLeast, HoursTop);
            p.Parity = CompanyCatalogue.Clamp(p.Parity, Every, Even);
            p.From = Math.Max(0, p.From);
            p.To = Math.Max(0, p.To);
            if (p.To > 0 && p.From > p.To)
            {
                p.To = p.From;
            }
            p.Blocks.Sort((a, b) => a.From.CompareTo(b.From));
            int end = 0;
            for (int i = 0; i < p.Blocks.Count; i++)
            {
                RotaBlock b = p.Blocks[i];
                b.From = CompanyCatalogue.Clamp(Math.Max(b.From, end), 0, RotaCatalogue.Hours - 1);
                b.To = CompanyCatalogue.Clamp(b.To, b.From + 1, RotaCatalogue.Hours);
                b.Crew = CompanyCatalogue.Clamp(b.Crew, 1, CrewMost);
                end = b.To;
                if (b.To <= b.From || i >= BlocksMost)
                {
                    p.Blocks.RemoveRange(i, p.Blocks.Count - i);
                    break;
                }
                p.Blocks[i] = b;
            }
            p.Version++;
        }

        /// <summary>The shifts a plan in use takes off the plan that would be in use otherwise, for "4 fewer shifts" on the page: the difference in the hours asked for by the floors in a
        /// week.</summary>
        public static int FloorHours(RotaPlanFile p)
        {
            int hours = 0;
            for (int b = 0; p != null && b < p.Blocks.Count; b++)
            {
                hours += (Math.Min(RotaCatalogue.Hours, p.Blocks[b].To) - Math.Max(0, p.Blocks[b].From)) * p.Blocks[b].Crew * RotaCatalogue.Days;
            }
            return hours;
        }
    }

    /// <summary>The plans in the save, game-free: "v=1^p:place|active|nextId|plans", plans divided by ';' and a plan's fields by ',' with its name last.</summary>
    public static class RotaPlanStore
    {
        public const string Key = "reallife:co_rota_plans";
        public const int Version = 1;
        public const int PlacesMost = CompanyCatalogue.ShopsMost;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Write(Dictionary<string, RotaPlanSet> sets)
        {
            var t = new StringBuilder(16 + sets.Count * 96);
            t.Append("v=").Append(Version);
            var places = new List<string>(sets.Keys);
            places.Sort(string.CompareOrdinal);   // the same line for the same plans
            for (int i = 0; i < places.Count && i < PlacesMost; i++)
            {
                RotaPlanSet s = sets[places[i]];
                if (s.Plans.Count == 0)
                {
                    continue;
                }
                t.Append("^p:").Append(CompanyStore.Clean(places[i])).Append('|').Append(s.Active.ToString(Inv)).Append('|').Append(s.NextId.ToString(Inv)).Append('|');
                for (int k = 0; k < s.Plans.Count && k < RotaPlanRules.PlansMost; k++)
                {
                    RotaPlanFile p = s.Plans[k];
                    t.Append(k > 0 ? ";" : string.Empty);
                    t.Append(p.Id.ToString(Inv)).Append(',').Append(p.From.ToString(Inv)).Append(',').Append(p.To.ToString(Inv)).Append(',')
                        .Append(p.Parity.ToString(Inv)).Append(',').Append(p.Hours.ToString(Inv)).Append(',').Append(p.Overtime ? '1' : '0').Append(',');
                    for (int b = 0; b < p.Blocks.Count && b < RotaPlanRules.BlocksMost; b++)
                    {
                        t.Append(b > 0 ? "/" : string.Empty).Append(p.Blocks[b].From.ToString(Inv)).Append('-').Append(p.Blocks[b].To.ToString(Inv)).Append('-').Append(p.Blocks[b].Crew.ToString(Inv));
                    }
                    t.Append(',').Append(CompanyStore.Clean(p.Name).Replace(',', ' '));
                }
            }
            return t.ToString();
        }

        /// <summary>Reads a line into the plans; false, with none, for another version or a line that is not the plans'.</summary>
        public static bool Read(string text, Dictionary<string, RotaPlanSet> into)
        {
            into.Clear();
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }
            string[] parts = text.Split('^');
            if (parts[0] != "v=" + Version)
            {
                return false;
            }
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].Length < 3 || parts[i][0] != 'p' || parts[i][1] != ':')
                {
                    continue;
                }
                string[] c = parts[i].Substring(2).Split('|');
                if (c.Length < 4 || c[0].Length == 0 || into.Count >= PlacesMost || into.ContainsKey(c[0]))
                {
                    continue;
                }
                var set = new RotaPlanSet { Place = c[0], Active = Math.Max(0, I(c, 1)), NextId = Math.Max(1, I(c, 2, 1)) };
                foreach (string record in c[3].Length == 0 ? new string[0] : c[3].Split(';'))
                {
                    string[] f = record.Split(',');
                    if (f.Length < 8 || set.Plans.Count >= RotaPlanRules.PlansMost || set.Find(I(f, 0)) != null)
                    {
                        continue;
                    }
                    var p = new RotaPlanFile
                    {
                        Id = Math.Max(1, I(f, 0, 1)), From = I(f, 1), To = I(f, 2), Parity = I(f, 3), Hours = I(f, 4, RotaPlanRules.HoursStart),
                        Overtime = I(f, 5) != 0, Name = RotaPlanRules.Name(f[7])
                    };
                    foreach (string block in f[6].Length == 0 ? new string[0] : f[6].Split('/'))
                    {
                        string[] b = block.Split('-');
                        if (b.Length == 3 && p.Blocks.Count < RotaPlanRules.BlocksMost)
                        {
                            p.Blocks.Add(new RotaBlock { From = I(b, 0), To = I(b, 1), Crew = I(b, 2) });
                        }
                    }
                    RotaPlanRules.Tidy(p);
                    set.Plans.Add(p);
                    set.NextId = Math.Max(set.NextId, p.Id + 1);
                }
                into[set.Place] = set;
            }
            return true;
        }

        private static int I(string[] c, int at, int fallback = 0)
        {
            int value;
            return at < c.Length && int.TryParse(c[at], NumberStyles.Integer, Inv, out value) ? value : fallback;
        }
    }
}
