using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Fame
{
    /// <summary>
    /// Standing in a save: one entry of the save's data, short lines.
    ///
    ///   reallife:standing_file
    ///     ST1|seed|start|settled|wealth|recognition|press|net worth|weekly|followers|businesses|properties|staff|
    ///         customers,workers,neighbourhood,integrity|moved,moved,moved,moved|moved day|street day|street today|
    ///         street minute|shop day|shop today|visit day|place;place|notice day
    ///     D|district|opinion                                    a district's opinion of the player
    ///     N|day|part|cause|delta|district                       a change the Standing page lists
    ///   reallife:standing_press
    ///     SP1|next id|reporter day|photo day|invite day|story day|refusal day|milestones|milestones seen|conditions day|
    ///         trust day|jobs day|rents day
    ///     I|id|kind|offered|answer by|day|hour|state            an invitation
    ///     P|id|kind|made|due|goal|state|source                  a promise
    ///   reallife:standing_city
    ///     SC1|request day|charity day|scandal until|lobby rest|lobby credit
    ///     R|id|kind|offered|until|amount|district|place|seed|state   a request
    ///     B|id|day|amount|place|surfaced|case                   a bribe
    ///     L|id|lever|position|day|decide day|chance|rival|state  a push on the council
    ///     O|place|district|percent|until                        more customers for a while
    ///
    /// Every number is written in the invariant culture. What is not understood is left out a line at a time: a note of
    /// a part or a cause this version does not know (from a later one) is dropped, never read as something else, so an
    /// entry from a later version or a damaged one never stops a load. A first line that is not this version's is not
    /// read at all (a new file: everybody's opinion where a newcomer's is). Game-free.
    /// </summary>
    public static class FameStore
    {
        public const string FileKey = "reallife:standing_file";
        public const string PressKey = "reallife:standing_press";
        private const string PressVersion = "SP1";
        public const string CityKey = "reallife:standing_city";
        private const string CityVersion = "SC1";
        public const string Keys = "reallife:standing_*";
        private const string Version = "ST1";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // The kinds this version knows: anything past them is from a later one.
        private const int Parts = (int)FamePart.Integrity + 1;
        private const int Causes = (int)FameCause.Press + 1;
        private const int InviteKinds = (int)FameInviteKind.CityEvent + 1;
        private const int InviteStates = (int)FameInviteState.Lapsed + 1;
        private const int PromiseKinds = (int)FamePromiseKind.Charity + 1;
        private const int PromiseStates = (int)FamePromiseState.Broken + 1;
        private const int RequestKinds = (int)FameRequestKind.Partnership + 1;
        private const int RequestStates = (int)FameRequestState.Withdrawn + 1;
        private const int LobbyStates = (int)FameLobbyState.Lost + 1;

        public static void Save(Dictionary<string, string> data, FameFile f)
        {
            if (data == null || f == null)
            {
                return;
            }
            data[FileKey] = Write(f);
            data[PressKey] = WritePress(f);
            data[CityKey] = WriteCity(f);
        }

        /// <summary>The save's file, or null if it has none this version can read (a new one is opened then).</summary>
        public static FameFile Load(Dictionary<string, string> data)
        {
            string text, press, city;
            FameFile f = data != null && data.TryGetValue(FileKey, out text) ? Read(text) : null;
            if (f != null && data.TryGetValue(PressKey, out press))
            {
                ReadPress(f, press);
            }
            if (f != null && data.TryGetValue(CityKey, out city))
            {
                ReadCity(f, city);
            }
            return f;
        }

        public static string WriteCity(FameFile f)
        {
            var b = new StringBuilder(96 + f.Requests.Count * 48 + f.Bribes.Count * 32 + f.Lobbies.Count * 32 + f.Boosts.Count * 32);
            b.Append(CityVersion);
            Join(b, f.RequestDay, f.CharityDay, f.ScandalUntil, f.LobbyRest, f.LobbyCredit);
            for (int i = 0; i < f.Requests.Count; i++)
            {
                FameRequest r = f.Requests[i];
                b.Append("\nR");
                Join(b, r.Id, (int)r.Kind, r.Offered, r.Until, r.Amount);
                b.Append('|').Append(Clean(r.District)).Append('|').Append(Clean(r.Place));
                Join(b, r.Seed, (int)r.State);
            }
            for (int i = 0; i < f.Bribes.Count; i++)
            {
                FameBribe x = f.Bribes[i];
                b.Append("\nB");
                Join(b, x.Id, x.Day, x.Amount);
                b.Append('|').Append(Clean(x.Place));
                Join(b, x.Surfaced, x.Case);
            }
            for (int i = 0; i < f.Lobbies.Count; i++)
            {
                FameLobby l = f.Lobbies[i];
                b.Append("\nL");
                Join(b, l.Id, l.Lever, l.Position, l.Day, l.DecideDay, l.Chance, l.Rival, (int)l.State);
            }
            for (int i = 0; i < f.Boosts.Count; i++)
            {
                FameBoost o = f.Boosts[i];
                b.Append("\nO|").Append(Clean(o.Place)).Append('|').Append(Clean(o.District));
                Join(b, o.Percent, o.Until);
            }
            return b.ToString();
        }

        /// <summary>The city's line into a file. What is not understood is left out a line at a time; another version's entry leaves the file without it.</summary>
        public static void ReadCity(FameFile f, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            try
            {
                string[] lines = text.Split('\n');
                string[] h = lines[0].TrimEnd('\r').Split('|');
                if (h.Length < 6 || h[0] != CityVersion)
                {
                    return;
                }
                f.RequestDay = I(h, 1, -100);
                f.CharityDay = I(h, 2, -1);
                f.ScandalUntil = I(h, 3, -1);
                f.LobbyRest = I(h, 4, -100);
                f.LobbyCredit = FameRules.Clamp(I(h, 5), 0, FameCity.CreditFromReception);
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] c = lines[i].TrimEnd('\r').Split('|');
                    if (c[0] == "R" && c.Length >= 10 && f.Requests.Count < FameCity.RequestsMost)
                    {
                        int kind = I(c, 2, -1), state = I(c, 9, -1);
                        if (kind < 0 || kind >= RequestKinds || state < 0 || state >= RequestStates)
                        {
                            continue;   // a kind of a later version's
                        }
                        f.Requests.Add(new FameRequest { Id = I(c, 1), Kind = (FameRequestKind)kind, Offered = I(c, 3), Until = I(c, 4), Amount = Math.Max(0, I(c, 5)), District = Text(c[6]),
                            Place = Text(c[7]), Seed = I(c, 8), State = (FameRequestState)state });
                    }
                    else if (c[0] == "B" && c.Length >= 7 && f.Bribes.Count < FameCity.BribesMost)
                    {
                        f.Bribes.Add(new FameBribe { Id = I(c, 1), Day = I(c, 2), Amount = Math.Max(0, I(c, 3)), Place = Text(c[4]), Surfaced = I(c, 5, -1), Case = I(c, 6) });
                    }
                    else if (c[0] == "L" && c.Length >= 9 && f.Lobbies.Count < FameCity.LobbiesMost)
                    {
                        int state = I(c, 8, -1);
                        if (state < 0 || state >= LobbyStates)
                        {
                            continue;
                        }
                        f.Lobbies.Add(new FameLobby { Id = I(c, 1), Lever = I(c, 2), Position = Math.Sign(I(c, 3)), Day = I(c, 4), DecideDay = I(c, 5), Chance = FameRules.Clamp(I(c, 6), 0, 100),
                            Rival = I(c, 7), State = (FameLobbyState)state });
                    }
                    else if (c[0] == "O" && c.Length >= 5 && f.Boosts.Count < FameCity.BoostsMost)
                    {
                        f.Boosts.Add(new FameBoost { Place = Text(c[1]), District = Text(c[2]), Percent = FameRules.Clamp(I(c, 3), -FameCity.BoostMost, FameCity.BoostMost), Until = I(c, 4) });
                    }
                }
            }
            catch (Exception)
            {
                f.Requests.Clear();
                f.Bribes.Clear();
                f.Lobbies.Clear();
                f.Boosts.Clear();
            }
        }

        public static string WritePress(FameFile f)
        {
            var b = new StringBuilder(128 + f.Invites.Count * 32 + f.Promises.Count * 32);
            b.Append(PressVersion);
            Join(b, f.NextId, f.ReporterDay, f.PhotoDay, f.InviteDay, f.StoryDay, f.RefusalDay, f.Milestones, f.MilestonesSeen ? 1 : 0, f.ConditionsDay, f.TrustDay, f.JobsDay, f.RentsDay);
            for (int i = 0; i < f.Invites.Count; i++)
            {
                FameInvite v = f.Invites[i];
                b.Append("\nI");
                Join(b, v.Id, (int)v.Kind, v.Offered, v.AnswerBy, v.Day, v.Hour, (int)v.State);
            }
            for (int i = 0; i < f.Promises.Count; i++)
            {
                FamePromise p = f.Promises[i];
                b.Append("\nP");
                Join(b, p.Id, (int)p.Kind, p.Made, p.Due, p.Goal, (int)p.State, (int)p.Source);
            }
            return b.ToString();
        }

        /// <summary>The press's line into a file. What is not understood is left out a line at a time; another version's entry leaves the file without it.</summary>
        public static void ReadPress(FameFile f, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            try
            {
                string[] lines = text.Split('\n');
                string[] h = lines[0].TrimEnd('\r').Split('|');
                if (h.Length < 13 || h[0] != PressVersion)
                {
                    return;
                }
                f.NextId = Math.Max(1, I(h, 1, 1));
                f.ReporterDay = I(h, 2, -100);
                f.PhotoDay = I(h, 3, -100);
                f.InviteDay = I(h, 4, -100);
                f.StoryDay = I(h, 5, -1);
                f.RefusalDay = I(h, 6, -100);
                f.Milestones = I(h, 7);
                f.MilestonesSeen = I(h, 8) != 0;
                f.ConditionsDay = I(h, 9, -100);
                f.TrustDay = I(h, 10, -100);
                f.JobsDay = I(h, 11, -100);
                f.RentsDay = I(h, 12, -100);
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] c = lines[i].TrimEnd('\r').Split('|');
                    if (c[0] == "I" && c.Length >= 8 && f.Invites.Count < FamePress.InvitesMost)
                    {
                        int kind = I(c, 2, -1), state = I(c, 7, -1);
                        if (kind < 0 || kind >= InviteKinds || state < 0 || state >= InviteStates)
                        {
                            continue;   // a kind of a later version's
                        }
                        f.Invites.Add(new FameInvite { Id = I(c, 1), Kind = (FameInviteKind)kind, Offered = I(c, 3), AnswerBy = I(c, 4), Day = I(c, 5), Hour = FameRules.Clamp(I(c, 6), 0, 23), State = (FameInviteState)state });
                    }
                    else if (c[0] == "P" && c.Length >= 8 && f.Promises.Count < FamePress.PromisesMost)
                    {
                        int kind = I(c, 2, -1), state = I(c, 6, -1), source = I(c, 7, 0);
                        if (kind < 0 || kind >= PromiseKinds || state < 0 || state >= PromiseStates)
                        {
                            continue;
                        }
                        f.Promises.Add(new FamePromise { Id = I(c, 1), Kind = (FamePromiseKind)kind, Made = I(c, 3), Due = I(c, 4), Goal = I(c, 5), State = (FamePromiseState)state,
                            Source = source >= 0 && source < Causes ? (FameCause)source : FameCause.Interview });
                    }
                }
            }
            catch (Exception)
            {
                f.Invites.Clear();
                f.Promises.Clear();
            }
        }

        public static string Write(FameFile f)
        {
            var b = new StringBuilder(320 + f.Districts.Count * 24 + f.Notes.Count * 28);
            b.Append(Version);
            Join(b, f.Seed, f.StartDay, f.SettledDay, f.Wealth, f.Recognition, f.Press);
            b.Append('|').Append(f.NetWorth.ToString(Inv)).Append('|').Append(f.Weekly.ToString(Inv));
            Join(b, f.Followers, f.Businesses, f.Properties, f.Staff);
            b.Append('|');
            List(b, f.Rep);
            b.Append('|');
            List(b, f.Moved);
            Join(b, f.MovedDay, f.StreetDay, f.StreetToday, f.StreetMinute, f.ShopDay, f.ShopToday, f.VisitDay);
            b.Append('|');
            for (int i = 0; i < f.Visited.Count; i++)
            {
                b.Append(i > 0 ? ";" : string.Empty).Append(Clean(f.Visited[i]).Replace(";", string.Empty));
            }
            Join(b, f.NoticeDay);
            for (int i = 0; i < f.Districts.Count; i++)
            {
                FameDistrict d = f.Districts[i];
                b.Append("\nD|").Append(Clean(d.Name));
                Join(b, d.Opinion);
            }
            for (int i = 0; i < f.Notes.Count; i++)
            {
                FameNote n = f.Notes[i];
                b.Append("\nN");
                Join(b, n.Day, n.Part, (int)n.Cause, n.Delta);
                b.Append('|').Append(Clean(n.District));
            }
            return b.ToString();
        }

        /// <summary>A file from the save's line; null if it is not this version's or cannot be read at all.</summary>
        public static FameFile Read(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            try
            {
                string[] lines = text.Split('\n');
                string[] h = lines[0].TrimEnd('\r').Split('|');
                if (h.Length < 24 || h[0] != Version)
                {
                    return null;
                }
                var f = new FameFile
                {
                    Seed = I(h, 1), StartDay = I(h, 2), SettledDay = I(h, 3, -1), Wealth = FameRules.Clamp(I(h, 4), 0, 100), Recognition = FameRules.Clamp(I(h, 5), 0, 100),
                    Press = FameRules.Clamp(I(h, 6), 0, FameRules.PressMost), NetWorth = L(h, 7), Weekly = L(h, 8), Followers = Math.Max(0, I(h, 9)), Businesses = Math.Max(0, I(h, 10)),
                    Properties = Math.Max(0, I(h, 11)), Staff = Math.Max(0, I(h, 12)), MovedDay = I(h, 15, -1), StreetDay = I(h, 16, -1), StreetToday = Math.Max(0, I(h, 17)),
                    StreetMinute = I(h, 18, -10000), ShopDay = I(h, 19, -1), ShopToday = Math.Max(0, I(h, 20)), VisitDay = I(h, 21, -1), NoticeDay = I(h, 23, -1)
                };
                Fill(h[13], f.Rep, FameRules.Start, 0, 100);
                Fill(h[14], f.Moved, 0, -FameRules.DayMost, FameRules.DayMost);
                string[] visited = h[22].Split(';');
                for (int i = 0; i < visited.Length && f.Visited.Count < FameRules.VisitsMost; i++)
                {
                    if (visited[i].Length > 0)
                    {
                        f.Visited.Add(visited[i]);
                    }
                }
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] c = lines[i].TrimEnd('\r').Split('|');
                    if (c[0] == "D" && c.Length >= 3 && c[1].Length > 0 && f.Districts.Count < FameRules.DistrictsMost && f.District(c[1]) == null)
                    {
                        f.Districts.Add(new FameDistrict { Name = c[1], Opinion = FameRules.Clamp(I(c, 2, FameRules.Start), 0, 100) });
                    }
                    else if (c[0] == "N" && c.Length >= 6 && f.Notes.Count < FameRules.NotesMost)
                    {
                        int part = I(c, 2, -9), cause = I(c, 3, -1);
                        if (part < FameNote.Recognition || part >= Parts || cause < 0 || cause >= Causes)
                        {
                            continue;   // a kind of a later version's
                        }
                        f.Notes.Add(new FameNote { Day = I(c, 1), Part = part, Cause = (FameCause)cause, Delta = I(c, 4), District = Text(c[5]) });
                    }
                }
                return f;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---------------------------------------------------------------- the pieces of a line

        internal static void Join(StringBuilder b, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                b.Append('|').Append(values[i].ToString(Inv));
            }
        }

        private static void List(StringBuilder b, int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                b.Append(i > 0 ? "," : string.Empty).Append(values[i].ToString(Inv));
            }
        }

        private static void Fill(string text, int[] into, int otherwise, int least, int most)
        {
            string[] parts = (text ?? string.Empty).Split(',');
            for (int i = 0; i < into.Length; i++)
            {
                int value;
                into[i] = i < parts.Length && int.TryParse(parts[i], NumberStyles.Integer, Inv, out value) ? FameRules.Clamp(value, least, most) : otherwise;
            }
        }

        /// <summary>Text as it goes into a line whose fields are parted by '|' and whose lines by a line break.</summary>
        internal static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace("|", string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty);
        }

        internal static string Text(string field)
        {
            return string.IsNullOrEmpty(field) ? null : field;
        }

        internal static int I(string[] c, int at, int otherwise = 0)
        {
            int value;
            return at < c.Length && int.TryParse(c[at], NumberStyles.Integer, Inv, out value) ? value : otherwise;
        }

        internal static long L(string[] c, int at)
        {
            long value;
            return at < c.Length && long.TryParse(c[at], NumberStyles.Integer, Inv, out value) ? value : 0L;
        }
    }
}
