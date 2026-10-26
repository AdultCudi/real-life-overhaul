using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Schools
{
    /// <summary>
    /// The schools' line in the save, game-free: one line under <see cref="Key"/>,
    /// "v=1^a:opened|last day|term|year|offer|offer by|offer end|owned since^s:schools^c:children^n:news", records divided by
    /// ';' and fields by '|' in the order <see cref="Write"/> joins them. A newer version adds fields at the end of a record or
    /// a part with a new letter: this reader skips what it does not know and bounds every figure. It names the schools by
    /// their place in the catalogue and the player's children by name, never an address: the district save keeps what names
    /// the campus in the game's part of the save. COST: a parse at a load, a write at a save.
    /// </summary>
    public static class SchoolStore
    {
        public const string Key = "reallife:schools";
        public const string TestKey = "reallife:schools_before_tests";
        public const int Version = 1;

        private const int Fields = 19;   // a school's record in version 1
        private const int NewsFields = 5;
        private const int NewsMost = 24;
        private const int ChildrenMost = 12;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The state of this save; a new one when it has none; null when its line cannot be read (it is then kept as it is).</summary>
        public static SchoolState Load(Dictionary<string, string> data, string key, out int version)
        {
            version = 0;
            if (data == null || !data.TryGetValue(key, out string text) || string.IsNullOrEmpty(text))
            {
                return new SchoolState();
            }
            return SaveLines.Checked(data, key, Read(text, out version), version, Version);
        }

        /// <summary>The line read; null when it is not a line of this store.</summary>
        public static SchoolState Read(string text, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(text) || !text.StartsWith("v=", StringComparison.Ordinal))
            {
                return null;
            }
            string[] parts = text.Split('^');
            if (!int.TryParse(parts[0].Substring(2), NumberStyles.Integer, Inv, out version) || version < 1)
            {
                return null;
            }
            var state = new SchoolState();
            for (int p = 1; p < parts.Length; p++)
            {
                string part = parts[p];
                if (part.StartsWith("a:", StringComparison.Ordinal))
                {
                    string[] a = part.Substring(2).Split('|');
                    state.Opened = Whole(a, 0, 0, 0, int.MaxValue);
                    state.LastDay = Whole(a, 1, 0, 0, int.MaxValue);
                    state.Term = Whole(a, 2, -1, -1, int.MaxValue);
                    state.Year = Whole(a, 3, 0, 0, int.MaxValue);
                    state.OfferPrice = Whole(a, 4, 0, 0, int.MaxValue);
                    state.OfferBy = Whole(a, 5, -1, -1, SchoolRules.Trusts - 1);
                    state.OfferEnd = Whole(a, 6, 0, 0, int.MaxValue);
                    state.OwnedSince = Whole(a, 7, 0, 0, int.MaxValue);
                }
                else if (part.StartsWith("s:", StringComparison.Ordinal) && part.Length > 2)
                {
                    foreach (string record in part.Substring(2).Split(';'))
                    {
                        ReadSchool(record, state);
                    }
                }
                else if (part.StartsWith("c:", StringComparison.Ordinal) && part.Length > 2)
                {
                    foreach (string name in part.Substring(2).Split(';'))
                    {
                        if (name.Length > 0 && state.Children.Count < ChildrenMost && !state.Children.Contains(name))
                        {
                            state.Children.Add(name);
                        }
                    }
                }
                else if (part.StartsWith("n:", StringComparison.Ordinal) && part.Length > 2)
                {
                    foreach (string record in part.Substring(2).Split(';'))
                    {
                        string[] f = record.Split('|');
                        if (f.Length >= NewsFields && state.News.Count < NewsMost)
                        {
                            state.News.Add(new SchoolNews
                            {
                                Day = Whole(f, 0, 0, 0, int.MaxValue), Kind = Whole(f, 1, 0, 0, 99), School = Whole(f, 2, 0, 0, SchoolRules.Count - 1),
                                Other = Whole(f, 3, 0, -2, int.MaxValue), Value = Long(f, 4, 0, long.MinValue / 4, long.MaxValue / 4)
                            });
                        }
                    }
                }
            }
            return state;
        }

        // A school's record over the catalogue's start: a record of a school this version does not know is passed over.
        private static void ReadSchool(string record, SchoolState state)
        {
            string[] f = record.Split('|');
            if (f.Length < Fields || !int.TryParse(f[0], NumberStyles.Integer, Inv, out int id) || id < 0 || id >= SchoolRules.Count)
            {
                return;
            }
            SchoolDef d = SchoolRules.Def(id);
            SchoolFile s = state.Files[id];
            s.Owner = Whole(f, 1, d.Owner, SchoolOwner.Council, SchoolRules.Trusts - 1);
            s.Places = Whole(f, 2, d.Places, 0, d.Capacity);
            s.Pupils = Whole(f, 3, 0, 0, d.Capacity);
            s.Waiting = Whole(f, 4, 0, 0, SchoolRules.PrivatePool);
            s.Fee = Whole(f, 5, d.Fee, 0, SchoolRules.FeeMost);
            s.Teachers = Whole(f, 6, d.Teachers, 0, d.Capacity);
            s.Skill = Whole(f, 7, d.Skill, 0, 100);
            s.PayBp = Whole(f, 8, d.PayBp, SchoolRules.PayLeast, SchoolRules.PayMost);
            s.Head = Whole(f, 9, s.Head, 0, int.MaxValue);
            s.HeadSkill = Whole(f, 10, d.HeadSkill, 0, 100);
            s.Scholar = Whole(f, 11, d.Scholar, 0, SchoolRules.ScholarMost);
            s.Reputation = Whole(f, 12, d.Reputation, 0, 1000);
            s.Results = Whole(f, 13, -1, -1, 100);
            s.Grade = Whole(f, 14, 0, 0, (int)SchoolGrade.Inadequate);
            s.Inspected = Whole(f, 15, 0, 0, int.MaxValue);
            s.Cash = Long(f, 16, d.Cash, long.MinValue / 4, long.MaxValue / 4);
            s.Week = Long(f, 17, 0, long.MinValue / 4, long.MaxValue / 4);
            s.LossWeeks = Whole(f, 18, 0, 0, 10000);
            s.Pupils = Math.Min(s.Pupils, s.Places);
        }

        /// <summary>The line for the state.</summary>
        public static string Write(SchoolState state)
        {
            var t = new StringBuilder(1400);
            t.Append("v=").Append(Version.ToString(Inv));
            t.Append("^a:").Append(state.Opened.ToString(Inv)).Append('|').Append(state.LastDay.ToString(Inv)).Append('|').Append(state.Term.ToString(Inv)).Append('|')
             .Append(state.Year.ToString(Inv)).Append('|').Append(state.OfferPrice.ToString(Inv)).Append('|').Append(state.OfferBy.ToString(Inv)).Append('|')
             .Append(state.OfferEnd.ToString(Inv)).Append('|').Append(state.OwnedSince.ToString(Inv));
            t.Append("^s:");
            for (int i = 0; i < state.Files.Length; i++)
            {
                SchoolFile s = state.Files[i];
                t.Append(i > 0 ? ";" : string.Empty)
                 .Append(s.Id.ToString(Inv)).Append('|').Append(s.Owner.ToString(Inv)).Append('|').Append(s.Places.ToString(Inv)).Append('|').Append(s.Pupils.ToString(Inv)).Append('|')
                 .Append(s.Waiting.ToString(Inv)).Append('|').Append(s.Fee.ToString(Inv)).Append('|').Append(s.Teachers.ToString(Inv)).Append('|').Append(s.Skill.ToString(Inv)).Append('|')
                 .Append(s.PayBp.ToString(Inv)).Append('|').Append(s.Head.ToString(Inv)).Append('|').Append(s.HeadSkill.ToString(Inv)).Append('|').Append(s.Scholar.ToString(Inv)).Append('|')
                 .Append(s.Reputation.ToString(Inv)).Append('|').Append(s.Results.ToString(Inv)).Append('|').Append(s.Grade.ToString(Inv)).Append('|').Append(s.Inspected.ToString(Inv)).Append('|')
                 .Append(s.Cash.ToString(Inv)).Append('|').Append(s.Week.ToString(Inv)).Append('|').Append(s.LossWeeks.ToString(Inv));
            }
            if (state.Children.Count > 0)
            {
                t.Append("^c:");
                for (int i = 0; i < state.Children.Count && i < ChildrenMost; i++)
                {
                    t.Append(i > 0 ? ";" : string.Empty).Append(Clean(state.Children[i]));
                }
            }
            if (state.News.Count > 0)
            {
                t.Append("^n:");
                for (int i = Math.Max(0, state.News.Count - NewsMost); i < state.News.Count; i++)
                {
                    SchoolNews n = state.News[i];
                    t.Append(i > Math.Max(0, state.News.Count - NewsMost) ? ";" : string.Empty).Append(n.Day.ToString(Inv)).Append('|').Append(n.Kind.ToString(Inv)).Append('|')
                     .Append(n.School.ToString(Inv)).Append('|').Append(n.Other.ToString(Inv)).Append('|').Append(n.Value.ToString(Inv));
                }
            }
            return t.ToString();
        }

        /// <summary>A child's name as the line keeps it: the line's own separators left out.</summary>
        public static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }
            var t = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c != ';' && c != '|' && c != '^')
                {
                    t.Append(c);
                }
            }
            return t.ToString();
        }

        private static int Whole(string[] f, int at, int otherwise, int least, int most)
        {
            if (at >= f.Length || !int.TryParse(f[at], NumberStyles.Integer, Inv, out int v))
            {
                return otherwise;
            }
            return v < least ? least : v > most ? most : v;
        }

        private static long Long(string[] f, int at, long otherwise, long least, long most)
        {
            if (at >= f.Length || !long.TryParse(f[at], NumberStyles.Integer, Inv, out long v))
            {
                return otherwise;
            }
            return v < least ? least : v > most ? most : v;
        }
    }
}
