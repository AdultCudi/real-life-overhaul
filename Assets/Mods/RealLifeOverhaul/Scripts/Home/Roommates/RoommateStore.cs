using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Roommates
{
    /// <summary>
    /// The roommate in the save: one line of modData under <see cref="Key"/>, game-free.
    ///
    ///     v=1^h:...^m:...^a:...^c:...
    ///
    /// Parts are divided by '^' and begin with a letter and a colon; records inside a part by ';', fields by '|'.
    ///   h  seed|last day|last hour stamp|notice up|its stamp|notices so far
    ///   m  the roommate, as a person; absent when nobody lives in
    ///   a  the arrangement: street|number|lodger|sofa|moved in|next rent day|last rent day|weekly|owed|owed due|slips|
    ///      loan|loan due|asked to borrow|topics|what was taken|raid week|raids|asked to stop|asked for quiet|
    ///      weeks covered|meals left|last talk|last meal|last company|last greeting|made a friend|introduced|
    ///      partner seen|notice|leave day
    ///   c  the people who answered the notice
    ///   a person: name|female|age|look seed|job|social traits|familiarity|affection|trust (tenths)|traits|known|
    ///             share|answers at|told|place in the social cast
    ///
    /// Names and addresses are escaped. A newer version adds fields at the end or a part with a new letter: this reader
    /// takes what it knows, puts every figure inside its bounds, and gives null for a line it cannot read at all.
    /// </summary>
    public static class RoommateStore
    {
        public const string Key = "reallife:home_roommate";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static RoommateState Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            string text;
            if (data == null || !data.TryGetValue(Key, out text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, Key, Read(text, out version), version, Version);   // (a line that cannot be read is kept as it is: Core/SaveLines)
        }

        public static string Write(RoommateState s)
        {
            var t = new StringBuilder(256);
            t.Append("v=").Append(Version).Append("^h:");
            Join(t, s.Seed, s.LastDay, s.LastStamp, s.AdUp ? 1 : 0, s.AdStamp, s.Ads);
            if (s.Mate != null)
            {
                t.Append("^m:");
                Person(t, s.Mate);
                t.Append("^a:").Append(Esc(s.HomeStreet)).Append('|');
                Join(t, s.HomeNumber, s.Lodger ? 1 : 0, s.OnSofa ? 1 : 0, s.MovedIn, s.RentDay, s.LastRentDay, s.Weekly, s.Owed, s.OwedDue, s.OwedSlips, s.Loan, s.LoanDue, s.BorrowAsk, (int)s.Topics);
                t.Append('|').Append(Esc(s.RaidItem)).Append('|');
                Join(t, s.RaidWeek, s.Raids, s.RaidStopped ? 1 : 0, s.QuietAsked ? 1 : 0, s.Covered, s.Meals, s.LastTalkDay, s.LastMealDay, s.LastCompanyDay, s.LastGreetStamp,
                    s.MadeFriend ? 1 : 0, s.Introduced ? 1 : 0, s.PartnerSeen ? 1 : 0, (int)s.Notice, s.LeaveDay);
            }
            if (s.Candidates.Count > 0)
            {
                t.Append("^c:");
                for (int i = 0; i < s.Candidates.Count && i < RoommateRules.CandidatesMost; i++)
                {
                    t.Append(i > 0 ? ";" : string.Empty);
                    Person(t, s.Candidates[i]);
                }
            }
            return t.ToString();
        }

        /// <summary>Reads a line. Null for one that is not this feature's at all.</summary>
        public static RoommateState Read(string text, out int version)
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
            var s = new RoommateState();
            bool header = false;
            string[] arrangement = null;
            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length < 2 || part[1] != ':')
                {
                    continue;
                }
                string body = part.Substring(2);
                switch (part[0])
                {
                    case 'h':
                    {
                        string[] f = body.Split('|');
                        s.Seed = Int(f, 0);
                        s.LastDay = Math.Max(0, Int(f, 1));
                        s.LastStamp = Math.Max(0, Int(f, 2));
                        s.AdUp = Int(f, 3) != 0;
                        s.AdStamp = Math.Max(0, Int(f, 4));
                        s.Ads = Math.Max(0, Int(f, 5));
                        header = true;
                        break;
                    }
                    case 'm':
                        s.Mate = Person(body.Split('|'));
                        break;
                    case 'a':
                        arrangement = body.Split('|');
                        break;
                    case 'c':
                    {
                        string[] records = body.Split(';');
                        for (int r = 0; r < records.Length && s.Candidates.Count < RoommateRules.CandidatesMost; r++)
                        {
                            Flatmate c = Person(records[r].Split('|'));
                            if (c != null)
                            {
                                s.Candidates.Add(c);
                            }
                        }
                        break;
                    }
                }
            }
            if (!header)
            {
                return null;
            }
            if (s.Mate != null && arrangement != null)
            {
                Arrangement(s, arrangement);
            }
            else
            {
                s.Mate = null;   // a person without an arrangement, or the other way round: nobody lives in
            }
            if (!s.AdUp)
            {
                s.Candidates.Clear();
            }
            return s;
        }

        private static void Arrangement(RoommateState s, string[] f)
        {
            s.HomeStreet = Unesc(Str(f, 0));
            s.HomeNumber = Int(f, 1);
            s.Lodger = Int(f, 2) != 0;
            s.OnSofa = Int(f, 3) != 0;
            s.MovedIn = Math.Max(0, Int(f, 4));
            s.RentDay = Math.Max(s.MovedIn, Int(f, 5));
            s.LastRentDay = Math.Max(s.MovedIn, Int(f, 6));
            s.Weekly = Math.Max(0, Int(f, 7));
            s.Owed = Math.Max(0, Int(f, 8));
            s.OwedDue = Math.Max(0, Int(f, 9));
            s.OwedSlips = Bound(Int(f, 10), 0, RoommateRules.SlipsMost);
            s.Loan = Math.Max(0, Int(f, 11));
            s.LoanDue = Math.Max(0, Int(f, 12));
            s.BorrowAsk = Bound(Int(f, 13), 0, 200);
            s.Topics = (Topic)(Int(f, 14) & 127);
            s.RaidItem = Unesc(Str(f, 15));
            s.RaidWeek = Math.Max(0, Int(f, 16));
            s.Raids = Bound(Int(f, 17), 0, RoommateRules.RaidsPerWeek);
            s.RaidStopped = Int(f, 18) != 0;
            s.QuietAsked = Int(f, 19) != 0;
            s.Covered = Math.Max(0, Int(f, 20));
            s.Meals = Math.Max(0, Int(f, 21));
            s.LastTalkDay = Int(f, 22);
            s.LastMealDay = Int(f, 23);
            s.LastCompanyDay = Int(f, 24);
            s.LastGreetStamp = Int(f, 25);
            s.MadeFriend = Int(f, 26) != 0;
            s.Introduced = Int(f, 27) != 0;
            s.PartnerSeen = Int(f, 28) != 0;
            int notice = Int(f, 29);
            s.Notice = notice > 0 && notice <= (int)EndWhy.BecamePartner ? (EndWhy)notice : EndWhy.None;
            s.LeaveDay = s.Notice != EndWhy.None ? Math.Max(s.MovedIn, Int(f, 30)) : 0;
            if (s.Owed == 0)
            {
                s.OwedDue = 0;
                s.OwedSlips = 0;
                s.Topics &= ~Topic.Rent;
            }
            if (s.Loan == 0)
            {
                s.LoanDue = 0;
            }
        }

        private static void Person(StringBuilder t, Flatmate c)
        {
            SocialPerson p = c.Person;
            t.Append(Esc(p.Name)).Append('|');
            Join(t, p.Female ? 1 : 0, p.Age, p.Seed, p.Job, (int)p.Traits, Tenths(p.Familiarity), Tenths(p.Affection), Tenths(p.Trust), (int)c.Traits, (int)c.Known,
                c.SharePct, c.Answers, c.Told ? 1 : 0, c.Friend);
        }

        private static Flatmate Person(string[] f)
        {
            string name = Unesc(Str(f, 0));
            if (string.IsNullOrEmpty(name) || f.Length < 12)
            {
                return null;
            }
            var p = new SocialPerson
            {
                Index = -1, Origin = SocialOrigin.Neighbour, Name = name, Female = Int(f, 1) != 0, Age = Bound(Int(f, 2), 18, 99), Seed = Int(f, 3),
                Job = Bound(Int(f, 4), 0, RoommateRules.JobCount - 1), Traits = (SocialTrait)(Int(f, 5) & 4095), Met = true, Stage = SocialStage.Acquaintance,
                Familiarity = Bound(Int(f, 6), 0, 1000) / 10f, Affection = Bound(Int(f, 7), 0, 1000) / 10f, Trust = Bound(Int(f, 8), 0, 1000) / 10f
            };
            if (p.Seed == 0)
            {
                p.Seed = 1;
            }
            var traits = (RoomTrait)(Int(f, 9) & 16383);
            if (!RoommateRules.Consistent(traits))
            {
                traits = RoomTrait.None;   // a pair with both sides set cannot have been written by this code
            }
            var c = new Flatmate
            {
                Person = p, Traits = traits, Known = (RoomTrait)(Int(f, 10) & 16383) & traits, SharePct = Bound(Int(f, 11), 5, 70), Answers = Math.Max(0, Int(f, 12)),
                Told = Int(f, 13) != 0, Friend = f.Length > 14 ? Bound(Int(f, 14), -1, SocialRules.CastSize - 1) : -1
            };
            p.Index = c.Friend;
            return c;
        }

        private static int Tenths(float value) => (int)Math.Round(value * 10f);

        private static int Bound(int value, int low, int high) => value < low ? low : value > high ? high : value;

        private static void Join(StringBuilder t, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                {
                    t.Append('|');
                }
                t.Append(values[i].ToString(Inv));
            }
        }

        private static string Str(string[] f, int index) => index < f.Length ? f[index] : string.Empty;

        private static int Int(string[] f, int index)
        {
            int value;
            return index < f.Length && int.TryParse(f[index], NumberStyles.Integer, Inv, out value) ? value : 0;
        }

        private static string Esc(string text) => string.IsNullOrEmpty(text) ? string.Empty : Uri.EscapeDataString(text);

        private static string Unesc(string text)
        {
            try
            {
                return string.IsNullOrEmpty(text) ? null : Uri.UnescapeDataString(text);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
