using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The social part of a save, kept as plain strings in its modData: a save made with it loads without the mod. The first
    /// <see cref="SocialRules.BaseCast"/> people are in one record, as every version has written them; everybody met after
    /// them is in a record of their own, which a version that knows only the first eight never reads or writes, so going
    /// back a version keeps them for the next load with this one.
    /// </summary>
    public static class SocialState
    {
        private const string KeyMain = "reallife:social";
        private const string KeyPeople = "reallife:social_people";
        private const string KeyMore = "reallife:social_more";
        private const string KeyPlan = "reallife:social_plan";
        private const string KeyOffer = "reallife:social_offer";
        private const int Version = 1;
        private const int ReadFields = 41;   // a person's fields every version has written
        private const char Field = '|';
        private const char Record = '\n';

        public static readonly SocialPerson[] People = new SocialPerson[SocialRules.MaxCast];
        public static readonly SocialPlan Plan = new SocialPlan();
        public static readonly SocialOffer Offer = new SocialOffer();

        // Lines of the later people's record this version could not read, written back as they were read.
        private static readonly List<string> KeptMore = new List<string>(4);

        public static bool Loaded { get; private set; }

        /// <summary>One past the highest slot taken: every loop over the cast stops here. A field read.</summary>
        public static int Count { get; private set; }

        public static int CastSeed;
        public static int Day;               // the day the daily round was last done for
        public static int TextsDay, TextsToday;
        public static int LastMeetDay;       // the day of the last new face
        public static int LastDropByDay;
        public static int LastCallInDay;     // the day one of them last rang
        public static int CoffeeBuys;        // coffees bought at a cart: a regular is noticed by another
        public static int WealthTier;        // the last wealth milestone they reacted to
        public static int LastArrestDay;
        public static int TestPerson;        // whom the test buttons act on

        public static void Reset()
        {
            Loaded = false;
            for (int i = 0; i < People.Length; i++)
            {
                People[i] = null;
            }
            Count = 0;
            KeptMore.Clear();
            Plan.Clear();
            Offer.Clear();
            CastSeed = 0;
            PersonLooks.Forget();
            Day = 0;
            TextsDay = 0;
            TextsToday = 0;
            LastMeetDay = 0;
            LastDropByDay = 0;
            LastCallInDay = 0;
            CoffeeBuys = 0;
            WealthTier = 0;
            LastArrestDay = 0;
            TestPerson = 0;
        }

        /// <summary>Null while nothing is loaded, or outside the cast.</summary>
        public static SocialPerson Get(int index)
        {
            return Loaded && index >= 0 && index < Count ? People[index] : null;
        }

        /// <summary>
        /// Somebody new joins the cast in the next free slot, which becomes their <see cref="SocialPerson.Index"/>. False, and
        /// nothing changed, once <see cref="SocialRules.MaxCast"/> are known.
        /// </summary>
        public static bool Add(SocialPerson person)
        {
            if (!Loaded || person == null || Count >= People.Length)
            {
                return false;
            }
            person.Index = Count;
            People[Count] = person;
            Count++;
            return true;
        }

        /// <summary>
        /// Somebody new in a slot already taken, its <see cref="SocialPerson.Index"/> theirs: whoever had it is let go. A
        /// plan or a question of the one who leaves is let go with them. False, and nothing changed, outside the cast.
        /// </summary>
        public static bool Replace(SocialPerson person)
        {
            if (!Loaded || person == null || person.Index < 0 || person.Index >= Count)
            {
                return false;
            }
            People[person.Index] = person;
            if (Plan.Person == person.Index)
            {
                Plan.Clear();
            }
            if (Offer.Person == person.Index)
            {
                Offer.Clear();
            }
            if (TestPerson == person.Index)
            {
                TestPerson = 0;
            }
            return true;
        }

        /// <summary>The first person of that origin, met or not, or null.</summary>
        public static SocialPerson ByOrigin(SocialOrigin origin)
        {
            for (int i = 0; Loaded && i < Count; i++)
            {
                if (People[i] != null && People[i].Origin == origin)
                {
                    return People[i];
                }
            }
            return null;
        }

        /// <summary>The first person of that origin not met yet, or null.</summary>
        public static SocialPerson Unmet(SocialOrigin origin)
        {
            for (int i = 0; Loaded && i < Count; i++)
            {
                if (People[i] != null && !People[i].Met && People[i].Origin == origin)
                {
                    return People[i];
                }
            }
            return null;
        }

        /// <summary>Whether somebody other than <paramref name="except"/> has that name: each name is one contact on the phone.</summary>
        public static bool NameTaken(string name, SocialPerson except)
        {
            for (int i = 0; i < Count; i++)
            {
                SocialPerson p = People[i];
                if (p != null && p != except && string.Equals(p.Name, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        public static int MetCount
        {
            get
            {
                int count = 0;
                for (int i = 0; Loaded && i < Count; i++)
                {
                    if (People[i] != null && People[i].Met)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>Whoever the player is dating or with, or null.</summary>
        public static SocialPerson Partner
        {
            get
            {
                for (int i = 0; Loaded && i < Count; i++)
                {
                    if (People[i] != null && People[i].Together)
                    {
                        return People[i];
                    }
                }
                return null;
            }
        }

        /// <summary>The cast is made from <paramref name="seed"/> if the save has none this version can read.</summary>
        public static void Load(Dictionary<string, string> data, int day, int daysPerYear, int districts, int seed)
        {
            Reset();
            bool read = false;
            try
            {
                read = Read(data, day);
            }
            catch (Exception)
            {
                read = false;   // a record this version cannot read: a new cast rather than a broken one
            }
            if (!read && data != null)
            {
                // Kept as it is for a version that can read it (Core/SaveLines).
                SaveLines.Unreadable(data, KeyMain, "it could not be read");
                if (SaveLines.IsKept(data, KeyMain))
                {
                    SaveLines.Unreadable(data, KeyPeople, "its record could not be read");
                    SaveLines.Unreadable(data, KeyMore, "its record could not be read");
                    SaveLines.Unreadable(data, KeyPlan, "its record could not be read");
                    SaveLines.Unreadable(data, KeyOffer, "its record could not be read");
                }
            }
            if (!read)
            {
                Reset();
                CastSeed = seed;
                SocialCatalogue.MakeCast(People, seed, day, daysPerYear, districts);
                Count = SocialRules.BaseCast;
                Day = day;
            }
            Loaded = true;
        }

        public static void Save(Dictionary<string, string> data)
        {
            if (!Loaded)
            {
                return;
            }
            CultureInfo inv = CultureInfo.InvariantCulture;
            var text = new StringBuilder(2048);
            text.Append(Version).Append(Field).Append(CastSeed).Append(Field).Append(Day).Append(Field).Append(TextsDay).Append(Field).Append(TextsToday)
                .Append(Field).Append(LastMeetDay).Append(Field).Append(LastDropByDay).Append(Field).Append(LastCallInDay).Append(Field).Append(CoffeeBuys)
                .Append(Field).Append(WealthTier).Append(Field).Append(LastArrestDay).Append(Field).Append(TestPerson);
            data[KeyMain] = text.ToString();

            text.Length = 0;
            AppendPeople(text, 0, Math.Min(Count, SocialRules.BaseCast));
            data[KeyPeople] = text.ToString();

            text.Length = 0;
            AppendPeople(text, SocialRules.BaseCast, Count);
            for (int i = 0; i < KeptMore.Count; i++)
            {
                text.Append(text.Length > 0 ? Record.ToString() : string.Empty).Append(KeptMore[i]);
            }
            if (text.Length > 0)
            {
                data[KeyMore] = text.ToString();
            }
            else
            {
                data.Remove(KeyMore);
            }

            SocialPlan plan = Plan;
            data[KeyPlan] = !plan.Active
                ? string.Empty
                : string.Join(Field.ToString(), plan.Person.ToString(inv), ((int)plan.Kind).ToString(inv), plan.Time.ToString("F0", inv), Clean(plan.Street),
                    plan.Number.ToString(inv), Clean(plan.Place), plan.X.ToString("F2", inv), plan.Y.ToString("F2", inv), plan.Z.ToString("F2", inv),
                    plan.TheirIdea ? "1" : "0", plan.Reminded ? "1" : "0", plan.Checked ? "1" : "0", plan.Arrives.ToString("F0", inv), Clean(plan.ToStreet),
                    plan.ToNumber.ToString(inv));
            SocialOffer offer = Offer;
            data[KeyOffer] = !offer.Active
                ? string.Empty
                : string.Join(Field.ToString(), offer.Person.ToString(inv), offer.Kind.ToString(inv), offer.Amount.ToString(inv), offer.Expires.ToString("F0", inv));
        }

        // One line a person of slots from to to, by slot.
        private static void AppendPeople(StringBuilder text, int from, int to)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            for (int i = from; i < to; i++)
            {
                SocialPerson p = People[i];
                if (p == null)
                {
                    continue;
                }
                if (text.Length > 0)
                {
                    text.Append(Record);
                }
                text.Append(p.Index).Append(Field).Append(Clean(p.Name)).Append(Field).Append(p.Female ? 1 : 0).Append(Field).Append(p.Age).Append(Field).Append(p.Seed)
                    .Append(Field).Append((int)p.Origin).Append(Field).Append((int)p.Traits).Append(Field).Append(p.Job).Append(Field).Append(p.District)
                    .Append(Field).Append(p.FreeFrom).Append(Field).Append(p.FreeTo).Append(Field).Append(p.Birthday).Append(Field).Append(p.Compatible ? 1 : 0)
                    .Append(Field).Append(p.Met ? 1 : 0).Append(Field).Append(p.MetDay)
                    .Append(Field).Append(p.Familiarity.ToString("F1", inv)).Append(Field).Append(p.Affection.ToString("F1", inv)).Append(Field).Append(p.Trust.ToString("F1", inv))
                    .Append(Field).Append((int)p.Stage).Append(Field).Append(p.StageDay).Append(Field).Append(p.MovedIn ? 1 : 0).Append(Field).Append(p.Drifted ? 1 : 0)
                    .Append(Field).Append(p.Mood).Append(Field).Append(p.LastContactDay).Append(Field).Append(p.LastCallDay).Append(Field).Append(p.LastTextDay)
                    .Append(Field).Append(p.LastOutingDay).Append(Field).Append(p.Shared).Append(Field).Append(p.Dates).Append(Field).Append((int)p.Wants)
                    .Append(Field).Append(p.WantsDay).Append(Field).Append(Clean(p.LastSaid)).Append(Field).Append(p.LastTopic).Append(Field).Append(p.AsksInWindow)
                    .Append(Field).Append(p.AskDay).Append(Field).Append(p.Debt).Append(Field).Append(p.DebtDay).Append(Field).Append(p.Loan).Append(Field).Append(p.LoanDue)
                    .Append(Field).Append(p.BirthdayYear).Append(Field);
                for (int m = 0; m < p.Memories.Count; m++)
                {
                    if (m > 0)
                    {
                        text.Append(',');
                    }
                    text.Append((int)p.Memories[m].Code).Append(':').Append(p.Memories[m].Day);
                }
                // The look: a version that reads 41 fields leaves it; its text holds no '|' or line break.
                text.Append(Field).Append(p.Look ?? string.Empty);
                // The day their age is true on: a version that reads 42 fields leaves it.
                text.Append(Field).Append(p.AgeDay);
                // The minute of the text a call back last opened on, then the outing still to be texted about the day after:
                // a version that reads 43 fields leaves them (Social/SocialFollowRules).
                text.Append(Field).Append(p.FollowedAt).Append(Field).Append(p.AfterDay).Append(Field).Append((int)p.AfterKind).Append(Field).Append(p.AfterWell ? 1 : 0)
                    .Append(Field).Append(Clean(p.AfterPlace));
            }
        }

        /// <summary>The people met as the save holds them, the loaded state untouched: for Flare's faces drawn behind the loading screen.</summary>
        public static void PeekMet(Dictionary<string, string> data, List<SocialPerson> into)
        {
            into.Clear();
            if (data == null || !data.TryGetValue(KeyMain, out string main) || string.IsNullOrEmpty(main)
                || !data.TryGetValue(KeyPeople, out string people) || string.IsNullOrEmpty(people))
            {
                return;
            }
            string[] m = main.Split(Field);
            if (m.Length < 12 || Int(m[0]) != Version)
            {
                return;
            }
            var found = new bool[People.Length];
            if (Peek(people, 0, SocialRules.BaseCast, found, into) != SocialRules.BaseCast)
            {
                into.Clear();
                return;
            }
            if (data.TryGetValue(KeyMore, out string more) && !string.IsNullOrEmpty(more))
            {
                Peek(more, SocialRules.BaseCast, People.Length, found, into);
            }
        }

        // The met of one record's slots from to to into the list; how many people of those slots were read.
        private static int Peek(string record, int from, int to, bool[] found, List<SocialPerson> into)
        {
            int count = 0;
            foreach (string line in record.Split(Record))
            {
                string[] f = line.Split(Field);
                int index = f.Length >= ReadFields ? Int(f[0]) : -1;
                if (index < from || index >= to || found[index] || string.IsNullOrEmpty(f[1]))
                {
                    continue;
                }
                var p = new SocialPerson
                {
                    Index = index, Name = f[1], Female = f[2] == "1", Age = Int(f[3]), Seed = Int(f[4]), Origin = (SocialOrigin)Int(f[5]), Met = f[13] == "1",
                    Stage = (SocialStage)Int(f[18]), Wants = (PlanKind)Int(f[29])
                };
                if (!Readable(p))
                {
                    continue;
                }
                ReadLook(p, f);
                SocialCatalogue.FixLook(p);
                found[index] = true;
                count++;
                if (p.Met)
                {
                    into.Add(p);
                }
            }
            return count;
        }

        private static bool Readable(SocialPerson p)
        {
            return p.Stage >= SocialStage.Stranger && p.Stage <= SocialStage.Partner && p.Wants >= PlanKind.Coffee && p.Wants <= PlanKind.Business;
        }

        // Field 42, the day their stored age is true on. A record from before it was kept: the day of this load, so the stored age is
        // their age now and grows from here (the day the cast was made was never saved).
        private static int AgeDayOf(string[] f, int day)
        {
            return f.Length >= 43 && f[42].Length > 0 ? Int(f[42]) : day;
        }

        // Fields 43 to 47, a call back's mark and the outing to be texted about the day after. A record from before them has
        // none: no text taken up on a call yet (the newest fresh one is, once), and no outing waiting.
        private static void ReadFollow(SocialPerson p, string[] f)
        {
            p.FollowedAt = f.Length >= 44 && f[43].Length > 0 ? Int(f[43]) : -1;
            if (f.Length < 48)
            {
                return;
            }
            p.AfterDay = Math.Max(0, Int(f[44]));
            var kind = (PlanKind)Int(f[45]);
            p.AfterKind = kind >= PlanKind.Coffee && kind <= PlanKind.Moving ? kind : PlanKind.Coffee;
            p.AfterWell = f[46] == "1";
            p.AfterPlace = f[47].Length > 0 ? f[47] : null;
            if (kind != p.AfterKind)
            {
                p.AfterDay = 0;   // (a later version's outing: never a number past the table)
            }
        }

        // Field 41, the look kept from the street, gives them its seed; one that cannot be read is let go.
        private static void ReadLook(SocialPerson p, string[] f)
        {
            if (f.Length < 42 || f[41].Length == 0 || p.Origin == SocialOrigin.Family || p.Origin == SocialOrigin.OldFriend)
            {
                return;
            }
            int seed = PersonLooks.Remember(f[41]);
            if (seed != 0)
            {
                p.Look = f[41];
                p.Seed = seed;
            }
        }

        private static bool Read(Dictionary<string, string> data, int day)
        {
            if (data == null || !data.TryGetValue(KeyMain, out string main) || string.IsNullOrEmpty(main)
                || !data.TryGetValue(KeyPeople, out string people) || string.IsNullOrEmpty(people))
            {
                return false;
            }
            string[] m = main.Split(Field);
            if (m.Length < 12 || Int(m[0]) != Version)
            {
                return false;
            }
            CastSeed = Int(m[1]);
            Day = Int(m[2]);
            TextsDay = Int(m[3]);
            TextsToday = Int(m[4]);
            LastMeetDay = Int(m[5]);
            LastDropByDay = Int(m[6]);
            LastCallInDay = Int(m[7]);
            CoffeeBuys = Int(m[8]);
            WealthTier = Int(m[9]);
            LastArrestDay = Int(m[10]);
            TestPerson = Int(m[11]);

            // Every one of the first eight, or the cast is not this save's.
            int found = 0;
            foreach (string record in people.Split(Record))
            {
                SocialPerson p = ReadPerson(record.Split(Field), day);
                if (p != null && p.Index < SocialRules.BaseCast && People[p.Index] == null)
                {
                    People[p.Index] = p;
                    found++;
                }
            }
            if (found != SocialRules.BaseCast)
            {
                return false;
            }
            Count = SocialRules.BaseCast;
            if (data.TryGetValue(KeyMore, out string more) && !string.IsNullOrEmpty(more))
            {
                ReadMore(more, day);
            }

            if (data.TryGetValue(KeyPlan, out string planText) && !string.IsNullOrEmpty(planText))
            {
                string[] f = planText.Split(Field);
                if (f.Length >= 15 && Known(Int(f[0])))
                {
                    Plan.Person = Int(f[0]);
                    Plan.Kind = (PlanKind)Int(f[1]);
                    Plan.Time = Float(f[2]);
                    Plan.Street = f[3].Length > 0 ? f[3] : null;
                    Plan.Number = Int(f[4]);
                    Plan.Place = f[5];
                    Plan.X = Float(f[6]);
                    Plan.Y = Float(f[7]);
                    Plan.Z = Float(f[8]);
                    Plan.TheirIdea = f[9] == "1";
                    Plan.Reminded = f[10] == "1";
                    Plan.Checked = f[11] == "1";
                    Plan.Arrives = Float(f[12]);
                    Plan.ToStreet = f[13].Length > 0 ? f[13] : null;
                    Plan.ToNumber = Int(f[14]);
                }
            }
            if (data.TryGetValue(KeyOffer, out string offerText) && !string.IsNullOrEmpty(offerText))
            {
                string[] f = offerText.Split(Field);
                if (f.Length >= 4 && Known(Int(f[0])))
                {
                    Offer.Person = Int(f[0]);
                    Offer.Kind = Int(f[1]);
                    Offer.Amount = Int(f[2]);
                    Offer.Expires = Float(f[3]);
                }
            }
            return true;
        }

        // The people after the first eight. A line that cannot be read is kept as it was read, and its slot stays taken.
        private static void ReadMore(string more, int day)
        {
            foreach (string line in more.Split(Record))
            {
                if (line.Length == 0)
                {
                    continue;
                }
                string[] f = line.Split(Field);
                SocialPerson p = ReadPerson(f, day);
                if (p != null && p.Index >= SocialRules.BaseCast && People[p.Index] == null)
                {
                    People[p.Index] = p;
                    Count = Math.Max(Count, p.Index + 1);
                    continue;
                }
                KeptMore.Add(line);
                int slot = Int(f[0]);
                if (slot >= SocialRules.BaseCast && slot < People.Length)
                {
                    Count = Math.Max(Count, slot + 1);
                }
            }
        }

        private static bool Known(int index) => index >= 0 && index < Count && People[index] != null;

        // A person's line as every version has written it, and what later fields it has; null if it cannot be read.
        private static SocialPerson ReadPerson(string[] f, int day)
        {
            if (f.Length < ReadFields)
            {
                return null;
            }
            int index = Int(f[0]);
            if (index < 0 || index >= People.Length || string.IsNullOrEmpty(f[1]))
            {
                return null;
            }
            var p = new SocialPerson
            {
                Index = index, Name = f[1], Female = f[2] == "1", Age = Int(f[3]), Seed = Int(f[4]), Origin = (SocialOrigin)Int(f[5]),
                Traits = (SocialTrait)Int(f[6]), Job = Int(f[7]), District = Int(f[8]), FreeFrom = Int(f[9]), FreeTo = Int(f[10]), Birthday = Int(f[11]),
                Compatible = f[12] == "1", Met = f[13] == "1", MetDay = Int(f[14]), Familiarity = Float(f[15]), Affection = Float(f[16]), Trust = Float(f[17]),
                Stage = (SocialStage)Int(f[18]), StageDay = Int(f[19]), MovedIn = f[20] == "1", Drifted = f[21] == "1", Mood = Int(f[22]),
                LastContactDay = Int(f[23]), LastCallDay = Int(f[24]), LastTextDay = Int(f[25]), LastOutingDay = Int(f[26]), Shared = Int(f[27]),
                Dates = Int(f[28]), Wants = (PlanKind)Int(f[29]), WantsDay = Int(f[30]), LastSaid = f[31], LastTopic = Int(f[32]), AsksInWindow = Int(f[33]),
                AskDay = Int(f[34]), Debt = Int(f[35]), DebtDay = Int(f[36]), Loan = Int(f[37]), LoanDue = Int(f[38]), BirthdayYear = Int(f[39])
            };
            if (!Readable(p))
            {
                return null;
            }
            ReadLook(p, f);
            p.AgeDay = AgeDayOf(f, day);
            ReadFollow(p, f);
            SocialCatalogue.FixLook(p);   // before a later version's origin is read as an old friend below
            if (p.Origin < SocialOrigin.Family || p.Origin > SocialOrigin.Street)
            {
                p.Origin = SocialOrigin.OldFriend;   // a later version's place of meeting: never a number past the tables
            }
            foreach (string memory in f[40].Split(','))
            {
                string[] parts = memory.Split(':');
                if (parts.Length == 2 && p.Memories.Count < SocialPerson.MaxMemories)
                {
                    p.Memories.Add(new SocialMemory { Code = (MemoryCode)Int(parts[0]), Day = Int(parts[1]) });
                }
            }
            return p;
        }

        // Nothing a record is split on may be inside a field.
        private static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            if (text.Length > 160)
            {
                text = text.Substring(0, 160);
            }
            return text.IndexOf(Field) < 0 && text.IndexOf(Record) < 0 && text.IndexOf('\r') < 0
                ? text
                : text.Replace(Field, '/').Replace(Record, ' ').Replace('\r', ' ');
        }

        private static int Int(string text)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
        }

        private static float Float(string text)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : 0f;
        }
    }
}
