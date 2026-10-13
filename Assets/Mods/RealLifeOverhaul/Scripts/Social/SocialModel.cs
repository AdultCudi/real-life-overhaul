using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>How close somebody is. Family (Mom) is a friend who can never be less.</summary>
    public enum SocialStage { Stranger, Acquaintance, Friend, CloseFriend, Dating, Partner }

    /// <summary>Where somebody is met. One person of the cast per origin.</summary>
    public enum SocialOrigin { Family, OldFriend, Gym, Work, CoffeeCart, Neighbour, Helped, Customer, Online, Event }   // saved as a number: new ones go at the end (Online: answered a post on Flare; Event: met at an evening of Standing's)

    [Flags]
    public enum SocialTrait
    {
        None = 0,
        Punctual = 1,      // early themselves; minds lateness twice as much
        Late = 2,          // often late; does not mind yours
        Generous = 4,      // offers to pay; lends more
        Careful = 8,       // splits everything; lends little
        Sporty = 16,       // workouts and walks
        Foodie = 32,       // meals and coffee
        NightOwl = 64,     // drinks and films; free late
        Homebody = 128,    // coffee, walks, your shop; cancels more
        Reliable = 256,    // never cancels; always repays
        Flaky = 512,       // cancels; repays when it suits
        Supportive = 1024, // kind about arrests and money
        Envious = 2048     // not kind about money
    }

    /// <summary>What a plan is for: the eight outings, and the two favours that are played on the street.</summary>
    public enum PlanKind { Coffee, Meal, Drinks, Cinema, Walk, Drive, Workout, Business, Lift, Moving }

    /// <summary>Something done together or done to them: the short list each person keeps.</summary>
    public enum MemoryCode
    {
        None, Met, Coffee, Meal, Drinks, Cinema, Walk, Drive, Workout, Business, Lift, Moving, Loan, Borrowed, Birthday,
        StoodUp, Late, Rude, Arrest, Crash, AskedOut, Official, MovedIn, BreakUp, Introduced, DroppedBy, NotRepaid,
        Posted     // tagged in a post of the player's on Flare (saved as a number: new ones go at the end)
    }

    public struct SocialMemory
    {
        public MemoryCode Code;
        public int Day;
    }

    /// <summary>
    /// One person of the cast: who they are (rolled once per save and then stored, so a change to
    /// the name lists never renames anybody) and how things stand with the player. Free of game
    /// types: nothing here needs the game to be running.
    /// </summary>
    public sealed class SocialPerson
    {
        public const int MaxMemories = 6;

        // ---- who they are
        public int Index;
        public string Name;
        public bool Female;
        public int Age;              // years
        public int Seed;             // of their look, for the game's appearance system
        public SocialOrigin Origin;
        public SocialTrait Traits;
        public int Job;              // index into SocialCatalogue.Jobs
        public int District;         // index into SocialCatalogue.Districts
        public int FreeFrom = 17;    // hours of a weekday they can be met between; weekends are 10 to 23 for everyone
        public int FreeTo = 23;
        public int Birthday = 1;     // day of the year, 1 to the game's days per year
        public bool Compatible;      // a romance is possible, if the player wants one

        // ---- how things stand
        public bool Met;
        public int MetDay;
        public float Familiarity, Affection, Trust;
        public SocialStage Stage;
        public int StageDay;
        public bool MovedIn;
        public bool Drifted;
        public int Mood;             // -2 a rough day ... +2 a great one; rolled each morning
        public int LastContactDay;
        public int LastCallDay;
        public int LastTextDay;
        public int LastOutingDay;
        public int Shared;           // things done together
        public int Dates;            // outings since they started dating
        public PlanKind Wants = PlanKind.Coffee;
        public int WantsDay;
        public string LastSaid = string.Empty;   // the last thing they said, as it was shown
        public int LastTopic = -1;
        public int AsksInWindow;     // favours the player asked of them lately
        public int AskDay;
        public int Debt;             // what the player owes them
        public int DebtDay;
        public int Loan;             // what they owe the player
        public int LoanDue;
        public int BirthdayYear = -1;   // the year (day / days per year) in which the player last remembered it
        public readonly List<SocialMemory> Memories = new List<SocialMemory>(MaxMemories);

        public bool Has(SocialTrait trait) => (Traits & trait) != 0;
        public bool IsFamily => Origin == SocialOrigin.Family;
        public bool Together => Stage == SocialStage.Dating || Stage == SocialStage.Partner;

        /// <summary>One figure for the whole of it, 0 to 100.</summary>
        public int Closeness => (int)Math.Round((Familiarity + Affection + Trust) / 3f);

        public void Remember(MemoryCode code, int day)
        {
            if (code == MemoryCode.None)
            {
                return;
            }
            if (Memories.Count >= MaxMemories)
            {
                Memories.RemoveAt(0);
            }
            Memories.Add(new SocialMemory { Code = code, Day = day });
        }

        public bool Remembers(MemoryCode code)
        {
            for (int i = 0; i < Memories.Count; i++)
            {
                if (Memories[i].Code == code)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True if they can be met at this hour of this kind of day.</summary>
        public bool FreeAt(int hour, bool weekend)
        {
            int from = weekend ? 10 : FreeFrom;
            int to = weekend ? 23 : FreeTo;
            return from <= to ? hour >= from && hour < to : hour >= from || hour < to;
        }

        /// <summary>Nobody answers the phone or sends a text in the small hours; night owls keep later ones.</summary>
        public bool Asleep(int hour)
        {
            return Has(SocialTrait.NightOwl) ? hour >= 2 && hour < 10 : hour >= 23 || hour < 8;
        }
    }

    /// <summary>The plan that is on: who, what, where and when. One at a time.</summary>
    public sealed class SocialPlan
    {
        public int Person = -1;
        public PlanKind Kind;
        public float Time;           // game minutes at which it is agreed for
        public string Street;        // the venue's address (the game's own street name), or null
        public int Number;
        public string Place;         // what the place is called, as it was agreed
        public float X, Y, Z;        // where they wait
        public bool TheirIdea;       // they asked: they are readier to pay
        public bool Reminded;        // the hour-before reminder went out
        public bool Checked;         // whether they cancel was rolled
        public float Arrives;        // game minutes at which they get there
        public string ToStreet;      // a lift's destination, a move's new address
        public int ToNumber;
        public bool Active => Person >= 0;

        public void Clear()
        {
            Person = -1;
            Street = null;
            Place = null;
            ToStreet = null;
            Reminded = false;
            Checked = false;
            TheirIdea = false;
        }
    }

    /// <summary>Something they asked that waits for an answer when the player next speaks to them. One at a time.</summary>
    public sealed class SocialOffer
    {
        public const int Loan = 100;   // Kind for a loan; anything else is a PlanKind

        public int Person = -1;
        public int Kind;
        public int Amount;
        public float Expires;        // game minutes
        public bool Active => Person >= 0;

        public void Clear()
        {
            Person = -1;
            Amount = 0;
        }
    }

    /// <summary>
    /// The rules of the relationship model: what each thing is worth, when a stage is reached and
    /// lost, how neglect wears things down. Every figure in one place.
    /// </summary>
    public static class SocialRules
    {
        public const int CastSize = 8;

        // ---- gains and losses
        public const float CallFamiliarity = 3f, CallAffection = 2f, CallTrust = 0.5f;
        public const float WarmReply = 1.5f, BluntReply = -2f;
        public const float TextReply = 1f;
        public const float SelfieFamiliarity = 1f, SelfieAffection = 2f;   // a selfie together on an outing, once an outing
        public const float SelfieWithin = 3f;                               // metres from the player the friend stands in
        public const float OnTimeTrust = 2f;
        public const float LateTrust = -3f, LateAffection = -2f;
        public const float StoodUpTrust = -12f, StoodUpAffection = -10f;
        public const float CancelledTrust = -4f, CancelledAffection = -3f;   // the player called it off
        public const float RudeAffection = -5f;
        public const float FavourTrust = 8f, FavourAffection = 4f;
        public const float FavourRefusedAffection = -2f;
        public const float BirthdayAffection = 8f, BirthdayTrust = 3f, BirthdayMissed = -4f;
        public const float ArrestTrust = -4f;
        public const float AskTrust = -2f;            // each favour asked of them
        public const float TooManyAsksTrust = -5f;    // ...and a third within a week
        public const int AskWindowDays = 7;
        public const int AsksAllowed = 2;
        public const float NotRepaidTrust = -10f;     // the player kept their money
        public const int RepayDays = 10;
        public const int LateMinutes = 20;            // game minutes after the agreed time that count as late
        public const int StoodUpMinutes = 75;         // ...and as not coming at all
        public const int EarlyMinutes = 60;           // the plan is shown from this long before

        // ---- stages
        public const float FriendFamiliarity = 30f, FriendAffection = 30f, FriendTrust = 20f;
        public const int FriendShared = 2;
        public const float CloseFamiliarity = 60f, CloseAffection = 60f, CloseTrust = 55f;
        public const int CloseShared = 5, CloseDays = 10;
        public const float DateAffection = 60f, DateTrust = 45f;
        public const float PartnerAffection = 80f, PartnerTrust = 70f;
        public const int PartnerDays = 14, PartnerDates = 4;
        public const float MoveInTrust = 85f;
        public const int MoveInDays = 21;
        public const float BreakUpAffection = 35f, BreakUpTrust = 25f;
        public const float Slack = 10f;               // a stage is kept until this far below what earned it
        public const float DriftFamiliarity = 8f;

        public static float Clamp(float value) => value < 0f ? 0f : value > 100f ? 100f : value;

        public static void Change(SocialPerson person, float familiarity, float affection, float trust)
        {
            person.Familiarity = Clamp(person.Familiarity + familiarity);
            person.Affection = Clamp(person.Affection + affection);
            person.Trust = Clamp(person.Trust + trust);
            if (person.IsFamily)
            {
                // Family is family: it never falls below being friends.
                person.Familiarity = Math.Max(person.Familiarity, 55f);
                person.Affection = Math.Max(person.Affection, 45f);
                person.Trust = Math.Max(person.Trust, 40f);
            }
        }

        /// <summary>Days without a word before things start to slip.</summary>
        public static int Grace(SocialPerson person)
        {
            if (person.IsFamily) return 6;
            switch (person.Stage)
            {
                case SocialStage.Partner: return 3;
                case SocialStage.Dating: return 3;
                case SocialStage.CloseFriend: return 10;
                case SocialStage.Friend: return 7;
                default: return 4;
            }
        }

        /// <summary>One day of neglect. True if anything changed.</summary>
        public static bool Decay(SocialPerson person, int day)
        {
            if (!person.Met || day - person.LastContactDay <= Grace(person))
            {
                return false;
            }
            Change(person, -1f, person.Together ? -1.5f : -1f, -0.5f);
            return true;
        }

        /// <summary>
        /// The stage their figures now earn, never Dating or Partner by itself (those are asked
        /// for) and never jumping two at once. Returns the stage to move to, which may be the
        /// one they are in.
        /// </summary>
        public static SocialStage Earned(SocialPerson person, int day)
        {
            if (!person.Met)
            {
                return SocialStage.Stranger;
            }
            SocialStage stage = person.Stage < SocialStage.Acquaintance ? SocialStage.Acquaintance : person.Stage;
            if (person.Together)
            {
                // A couple stays one until it breaks: that is decided in BrokenUp.
                return stage;
            }
            if (stage == SocialStage.Acquaintance && person.Familiarity >= FriendFamiliarity && person.Affection >= FriendAffection
                && person.Trust >= FriendTrust && (person.Shared >= FriendShared || person.IsFamily || person.Origin == SocialOrigin.OldFriend))
            {
                return SocialStage.Friend;
            }
            if (stage == SocialStage.Friend && person.Familiarity >= CloseFamiliarity && person.Affection >= CloseAffection
                && person.Trust >= CloseTrust && person.Shared >= CloseShared && day - person.MetDay >= CloseDays)
            {
                return SocialStage.CloseFriend;
            }
            if (stage == SocialStage.CloseFriend && (person.Familiarity < CloseFamiliarity - Slack || person.Affection < CloseAffection - Slack
                                                     || person.Trust < CloseTrust - Slack))
            {
                return SocialStage.Friend;
            }
            if (stage == SocialStage.Friend && !person.IsFamily && (person.Familiarity < FriendFamiliarity - Slack
                                                                    || person.Affection < FriendAffection - Slack || person.Trust < FriendTrust - Slack))
            {
                return SocialStage.Acquaintance;
            }
            return stage;
        }

        public static bool BrokenUp(SocialPerson person)
        {
            return person.Together && (person.Affection < BreakUpAffection || person.Trust < BreakUpTrust);
        }

        public static bool WouldDate(SocialPerson person)
        {
            return person.Compatible && !person.Together && person.Stage >= SocialStage.Friend
                   && person.Affection >= DateAffection && person.Trust >= DateTrust;
        }

        public static bool WouldCommit(SocialPerson person, int day)
        {
            return person.Stage == SocialStage.Dating && person.Affection >= PartnerAffection && person.Trust >= PartnerTrust
                   && day - person.StageDay >= PartnerDays && person.Dates >= PartnerDates;
        }

        public static bool WouldMoveIn(SocialPerson person, int day)
        {
            return person.Stage == SocialStage.Partner && !person.MovedIn && person.Trust >= MoveInTrust && day - person.StageDay >= MoveInDays;
        }

        /// <summary>How much they like a kind of outing: 0.7 (not their thing) to 1.4 (exactly their thing).</summary>
        public static float Liking(SocialPerson person, PlanKind kind)
        {
            float liking = 1f;
            switch (kind)
            {
                case PlanKind.Coffee: if (person.Has(SocialTrait.Foodie) || person.Has(SocialTrait.Homebody)) liking += 0.3f; break;
                case PlanKind.Meal: if (person.Has(SocialTrait.Foodie)) liking += 0.4f; if (person.Has(SocialTrait.Careful)) liking -= 0.15f; break;
                case PlanKind.Drinks: if (person.Has(SocialTrait.NightOwl)) liking += 0.4f; if (person.Has(SocialTrait.Homebody)) liking -= 0.3f; break;
                case PlanKind.Cinema: if (person.Has(SocialTrait.NightOwl)) liking += 0.25f; if (person.Has(SocialTrait.Sporty)) liking -= 0.1f; break;
                case PlanKind.Walk: if (person.Has(SocialTrait.Sporty) || person.Has(SocialTrait.Homebody)) liking += 0.3f; break;
                case PlanKind.Drive: if (person.Has(SocialTrait.Homebody)) liking -= 0.2f; break;
                case PlanKind.Workout: liking += person.Has(SocialTrait.Sporty) ? 0.4f : -0.3f; break;
                case PlanKind.Business: if (person.Has(SocialTrait.Supportive)) liking += 0.3f; if (person.Has(SocialTrait.Envious)) liking -= 0.3f; break;
            }
            return liking < 0.7f ? 0.7f : liking > 1.4f ? 1.4f : liking;
        }

        /// <summary>The odds that they call it off beforehand.</summary>
        public static float CancelOdds(SocialPerson person, PlanKind kind, bool raining)
        {
            if (person.Has(SocialTrait.Reliable) || kind == PlanKind.Lift || kind == PlanKind.Moving)
            {
                return 0f;
            }
            float odds = 0.05f;
            if (person.Has(SocialTrait.Flaky)) odds += 0.13f;
            if (person.Has(SocialTrait.Homebody)) odds += 0.05f;
            if (person.Mood <= -2) odds += 0.10f;
            if (raining && kind == PlanKind.Walk) odds += 0.25f;
            if (person.Together) odds *= 0.5f;
            return odds;
        }

        /// <summary>Game minutes from the agreed time at which they turn up; negative is early.</summary>
        public static float ArrivalOffset(SocialPerson person, double roll)
        {
            if (person.Has(SocialTrait.Punctual)) return -10f + (float)roll * 6f;
            if (person.Has(SocialTrait.Late)) return roll < 0.55 ? 8f + (float)roll * 40f : -2f;
            return roll < 0.12 ? 10f + (float)roll * 60f : -6f + (float)roll * 8f;
        }
    }

    /// <summary>
    /// The social part of a save: the cast, the plan, what waits for an answer, the day's
    /// counters. Kept in the save's modData as plain strings (see Core/ModState): a save made
    /// with it loads without the mod, and one from before it simply starts a new cast.
    /// </summary>
    public static class SocialState
    {
        private const string KeyMain = "reallife:social";
        private const string KeyPeople = "reallife:social_people";
        private const string KeyPlan = "reallife:social_plan";
        private const string KeyOffer = "reallife:social_offer";
        private const int Version = 1;
        private const char Field = '|';
        private const char Record = '\n';

        public static readonly SocialPerson[] People = new SocialPerson[SocialRules.CastSize];
        public static readonly SocialPlan Plan = new SocialPlan();
        public static readonly SocialOffer Offer = new SocialOffer();

        public static bool Loaded { get; private set; }
        public static int CastSeed;
        public static int Day;               // the day the daily round was last done for
        public static int TextsDay, TextsToday;
        public static int LastMeetDay;       // a new face at most every other day
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
            Plan.Clear();
            Offer.Clear();
            CastSeed = 0;
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

        public static SocialPerson Get(int index)
        {
            return Loaded && index >= 0 && index < People.Length ? People[index] : null;
        }

        public static SocialPerson ByOrigin(SocialOrigin origin)
        {
            for (int i = 0; Loaded && i < People.Length; i++)
            {
                if (People[i] != null && People[i].Origin == origin)
                {
                    return People[i];
                }
            }
            return null;
        }

        public static int MetCount
        {
            get
            {
                int count = 0;
                for (int i = 0; Loaded && i < People.Length; i++)
                {
                    if (People[i] != null && People[i].Met)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>Whoever the player is with, or null.</summary>
        public static SocialPerson Partner
        {
            get
            {
                for (int i = 0; Loaded && i < People.Length; i++)
                {
                    if (People[i] != null && People[i].Together)
                    {
                        return People[i];
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Reads the save, or makes the cast if this save has none. <paramref name="daysPerYear"/>
        /// and <paramref name="districts"/> are the game's; <paramref name="seed"/> is used only
        /// for a new cast.
        /// </summary>
        public static void Load(Dictionary<string, string> data, int day, int daysPerYear, int districts, int seed)
        {
            Reset();
            bool read = false;
            try
            {
                read = Read(data);
            }
            catch (Exception)
            {
                read = false;   // a record this version cannot read: a new cast rather than a broken one
            }
            if (!read)
            {
                Reset();
                CastSeed = seed;
                SocialCatalogue.MakeCast(People, seed, day, daysPerYear, districts);
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
            for (int i = 0; i < People.Length; i++)
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
            }
            data[KeyPeople] = text.ToString();

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

        private static bool Read(Dictionary<string, string> data)
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

            int found = 0;
            foreach (string record in people.Split(Record))
            {
                string[] f = record.Split(Field);
                if (f.Length < 41)
                {
                    continue;
                }
                int index = Int(f[0]);
                if (index < 0 || index >= People.Length || People[index] != null || string.IsNullOrEmpty(f[1]))
                {
                    continue;
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
                if (p.Stage < SocialStage.Stranger || p.Stage > SocialStage.Partner || p.Wants < PlanKind.Coffee || p.Wants > PlanKind.Business)
                {
                    continue;
                }
                if (p.Origin < SocialOrigin.Family || p.Origin > SocialOrigin.Event)
                {
                    p.Origin = SocialOrigin.OldFriend;   // a place of meeting of a later version's: read as an old friend, never as a number past the tables
                }
                foreach (string memory in f[40].Split(','))
                {
                    string[] parts = memory.Split(':');
                    if (parts.Length == 2 && p.Memories.Count < SocialPerson.MaxMemories)
                    {
                        p.Memories.Add(new SocialMemory { Code = (MemoryCode)Int(parts[0]), Day = Int(parts[1]) });
                    }
                }
                People[index] = p;
                found++;
            }
            if (found != People.Length)
            {
                return false;
            }

            if (data.TryGetValue(KeyPlan, out string planText) && !string.IsNullOrEmpty(planText))
            {
                string[] f = planText.Split(Field);
                if (f.Length >= 15 && Int(f[0]) >= 0 && Int(f[0]) < People.Length)
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
                if (f.Length >= 4 && Int(f[0]) >= 0 && Int(f[0]) < People.Length)
                {
                    Offer.Person = Int(f[0]);
                    Offer.Kind = Int(f[1]);
                    Offer.Amount = Int(f[2]);
                    Offer.Expires = Float(f[3]);
                }
            }
            return true;
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
