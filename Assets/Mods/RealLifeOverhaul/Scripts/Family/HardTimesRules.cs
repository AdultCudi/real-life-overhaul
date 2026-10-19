using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// What can lead to a difficult moment at home. Saved by number (HardMemory): never reorder, add at the end.
    /// </summary>
    public enum Strain
    {
        None = 0,          // nothing is wrong: an everyday moment, now and then
        LateHome = 1,      // evenings home after ten while the partner waited
        NightsAway = 2,    // nights not spent at home
        LongHours = 3,     // long working hours
        Unseen = 4,        // days without seeing, calling or texting the partner
        Worn = 5,          // stress, too little sleep
        Money = 6,         // in the red, owing more than is owned, money borrowed
        Loss = 7,          // a big fall in the money in hand
        Spend = 8,         // a big purchase on the statement
        NewChild = 9,      // a child has just arrived
        NewPlace = 10,     // a new place: a building taken, the home moved
        LetDown = 11,      // a date, an outing or an appointment missed, a promise broken
        Forgot = 12,       // the partner's birthday or the anniversary, with no word all day
        Skipping = 13,     // meals skipped, a poor diet: Mom worries
        Mess = 14,         // a messy home
        Sorry = 15,        // the last moments were answered with a quick sorry
        Business = 16,     // evenings and nights at the player's own business
        RunningLate = 17   // late for a family outing now
    }

    /// <summary>
    /// The player's situation as a difficult moment is weighed: plain figures, filled in by HardTimesWatch (the
    /// household and the game's own records) and through HardTimesBridges by the wiring (home life, careers, the social
    /// life, travel, the economy). A figure its feature does not keep (switched off, or no such thing) is -1, and no
    /// moment comes of it.
    /// </summary>
    public struct HardSigns
    {
        public int Day, Hour;
        public bool LivesIn;           // the two live together
        public bool Committed;         // engaged or married: their money is the household's too
        public float Harmony;          // 0..100
        public int TalkDay;            // the last day they talked or spent time together (family life's own)
        public int SharedDay;          // the last day they spent time together: a date, a meal, a routine (family life)
        public int ChildDay;           // the day the newest child at home arrived; 0 none
        public int SinceDay;           // the anniversary's day: the wedding day if married, else the day they began
        public int YearDays;           // the game's year in days
        public bool MomThere;          // Mom is in the phone and relatives are on
        public int OutingLate;         // minutes late for an outing with others: a child's play or party, Mom's dinner
        public int LetDownDay;         // the last day a promise was broken: a date, an appointment, the birth; 0 none
        public int LetDowns;           // how many in the week to that day
        public int PlaceDay;           // the last day a new place was taken; 0 none
        public int Places;             // the player's own places now (homes and businesses)
        public int MovedDay;           // the last day the home moved; 0 none
        public bool AwayTogether;      // travel: on a trip with the partner, so a night away is a night together

        // Home life (Scripts/Home) and careers.
        public float WorkHours;        // hours on a job over the last days, halved each night: twice a steady day's
        public int Burnout;            // the professional job's burnout, thousandths
        public float Stress;           // 0 calm .. 100
        public float SleepDebt;        // hours of sleep owed
        public int Skipped;            // meals skipped over the last three days
        public float Diet;             // 0 junk and missed meals .. 100 cooked at home
        public float Tidy;             // the player's home, 0 a tip .. 100 spotless

        // The social life, about the partner.
        public int ContactDay;         // the last day the player called, texted or met them
        public int OutingDay;          // the last day the two went out together
        public int StoodUpDay;         // the last day the player stood them up or kept them waiting on an outing
        public bool BirthdayToday;     // today is their birthday...
        public bool BirthdayWished;    // ...and the player has wished them a happy one

        // Money: the game's own records, and the economy's.
        public int PriceLevel;         // the city's price level, tenths of a point (1000 at the start's prices)
        public float Cash;             // money in hand now
        public float WeekHigh;         // the highest of the game's last seven midnight balances
        public int RedMidnights;       // of the last three midnights, how many ended below nothing
        public bool NetKnown;
        public long NetWorth;          // as the Finance app shows it
        public bool OwnsBusiness;      // a business of the player's that takes money
        public float BigBuy;           // the biggest purchase of three days, not a running cost or the family's own
        public float BigBuyBefore;     // the money in hand just before it
        public float Borrowed;         // money borrowed over the last three days

        /// <summary>
        /// Signs with every figure of another feature unknown: what the watch starts from before the bridge fills in
        /// what it can.
        /// </summary>
        public static HardSigns Unknown(int day, int hour)
        {
            return new HardSigns
            {
                Day = day, Hour = hour, WorkHours = -1f, Burnout = -1, Stress = -1f, SleepDebt = -1f, Skipped = -1,
                Diet = -1f, Tidy = -1f, ContactDay = -1, OutingDay = -1, StoodUpDay = -1, PriceLevel = PriceRules.Start
            };
        }
    }

    /// <summary>
    /// What the difficult moments remember between saves: the evenings and nights the player was out, when each cause
    /// began and when it last led to a moment, the places, the broken promises and the quick sorries. One optional
    /// field of the family's save (FamilySave.Hard, written by <see cref="Write"/>); a save without it, or with a line
    /// this version cannot read, reads as nothing remembered, which never brings a moment by itself.
    /// </summary>
    public sealed class HardMemory
    {
        public const int Version = 1;

        public int Base;               // the day bit 0 of the marks stands for
        // The marks, bit n for the day Base - n: the evenings out after ten while the partner was home, the nights
        // away from home (the night that began on that day), and of each those at the player's own business.
        public int Late, LateWork;
        public int Away, AwayWork;
        public readonly int[] Since = new int[HardTimesRules.Strains];   // the day each cause began; 0 none
        public readonly int[] Spoke = new int[HardTimesRules.Strains];   // the day each last led to a moment; 0 never
        public int Places = -1;        // the player's own places as last counted; -1 not yet
        public int PlaceDay;           // the last day one more was counted
        public int HomeKey;            // the home last seen (HardTimesRules.Key of its address); 0 none
        public int Promises = -1;      // family life's broken promises as last counted; -1 not yet
        public int PromiseDay;         // the last day one was broken
        public int PromiseRun;         // how many were broken in the week to that day
        public int Sorry;              // quick sorries in a row, the last within HardTimesRules.SorryDays
        public int SorryDay;
        public int MovedDay;           // the last day the home was somewhere else than before

        /// <summary>
        /// One line: the version, then the fields in this order, comma-separated; a later version adds its own at the
        /// end.
        /// </summary>
        public string Write()
        {
            var b = new StringBuilder(160);
            b.Append(Version.ToString(CultureInfo.InvariantCulture));
            Add(b, Base);
            Add(b, Late);
            Add(b, LateWork);
            Add(b, Away);
            Add(b, AwayWork);
            Add(b, Since.Length);
            for (int i = 0; i < Since.Length; i++)
            {
                Add(b, Since[i]);
            }
            Add(b, Spoke.Length);
            for (int i = 0; i < Spoke.Length; i++)
            {
                Add(b, Spoke[i]);
            }
            Add(b, Places);
            Add(b, PlaceDay);
            Add(b, HomeKey);
            Add(b, Promises);
            Add(b, PromiseDay);
            Add(b, PromiseRun);
            Add(b, Sorry);
            Add(b, SorryDay);
            Add(b, MovedDay);
            return b.ToString();
        }

        /// <summary>
        /// The memory a line holds. Null or empty (a save from before this field), a version below 1, or a line that
        /// cannot be read: a fresh memory. Fields a line lacks (an earlier form of it) are as fresh; fields it has
        /// beyond these (a later version's) are passed over.
        /// </summary>
        public static HardMemory Read(string line)
        {
            var m = new HardMemory();
            if (string.IsNullOrEmpty(line))
            {
                return m;
            }
            string[] f = line.Split(',');
            int at = 0;
            if (!Take(f, ref at, out int version) || version < 1)
            {
                return new HardMemory();
            }
            bool read = Field(f, ref at, ref m.Base) && Field(f, ref at, ref m.Late) && Field(f, ref at, ref m.LateWork)
                        && Field(f, ref at, ref m.Away) && Field(f, ref at, ref m.AwayWork)
                        && Days(f, ref at, m.Since) && Days(f, ref at, m.Spoke)
                        && Field(f, ref at, ref m.Places) && Field(f, ref at, ref m.PlaceDay)
                        && Field(f, ref at, ref m.HomeKey) && Field(f, ref at, ref m.Promises)
                        && Field(f, ref at, ref m.PromiseDay) && Field(f, ref at, ref m.PromiseRun)
                        && Field(f, ref at, ref m.Sorry) && Field(f, ref at, ref m.SorryDay)
                        && Field(f, ref at, ref m.MovedDay);
            return read ? m : new HardMemory();
        }

        private static void Add(StringBuilder b, int value)
        {
            b.Append(',');
            b.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        // The next number, or nothing more (false only for one that is there and is not a number).
        private static bool Field(string[] f, ref int at, ref int into)
        {
            if (at >= f.Length)
            {
                return true;   // an earlier form of the line: the rest stays fresh
            }
            if (!Take(f, ref at, out int value))
            {
                return false;
            }
            into = value;
            return true;
        }

        private static bool Days(string[] f, ref int at, int[] into)
        {
            int count = 0;
            if (!Field(f, ref at, ref count) || count < 0)
            {
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                int value = 0;
                if (!Field(f, ref at, ref value))
                {
                    return false;
                }
                if (i < into.Length)
                {
                    into[i] = value;   // a later version's further causes are passed over
                }
            }
            return true;
        }

        private static bool Take(string[] f, ref int at, out int value)
        {
            value = 0;
            if (at >= f.Length)
            {
                return false;
            }
            return int.TryParse(f[at++], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }

    /// <summary>
    /// When a difficult moment comes, and which, from what is going on: game-free, so EconomyCheck runs it
    /// (HardTimesChecks.cs). Each cause has a level of a hundred, from the player's own days; a moment's causes are the
    /// ones its words are about, and a moment comes once its causes come to <see cref="MinFit"/>, a lasting cause has
    /// lasted a day or two (a tense home is quicker to it, a warm one slower), its hour of the day has come, and the
    /// days since the last moment allow. Every cause there when it came then rests a week. A cause that has gone (they
    /// made up, the money came back) leaves its moments nothing to come of. With nothing wrong, only the towel, whose
    /// words fit any home, comes now and then. Which of the fitting moments, its hour and a day more or less are from
    /// <see cref="Mix"/> of the partner's seed and the day: the same state gives the same moment, on every machine.
    /// Cost: a few dozen comparisons, once a game hour.
    /// </summary>
    public static class HardTimesRules
    {
        public const int Strains = 18;          // the causes, Strain.None included
        public const int Moments = 10;          // HardTimes' difficult moments, by their place in its list
        public const int Towel = 5;             // "a wet towel on the bed": its words fit a day with nothing wrong
        public const int MinFit = 30;           // points of a hundred a moment's causes must come to
        public const int StrongFit = 80;        // from this a moment needs only the shortest gap, however warm the home
        public const int MinGapDays = 2;        // days between two moments at the least, as there always were
        public const int RestDays = 7;          // days the causes aired in a moment rest (and up to two more)
        public const int EverydayGapDays = 21;  // days since the last moment before an everyday one
        public const int EverydayPerMille = 25; // per mille a day with nothing wrong: about one day in forty
        public const int MarkDays = 31;         // days the evening and night marks reach back (bits of an int)
        public const int Week = 7;              // days the marks are counted over
        public const int SorryDays = 14;        // days a run of quick sorries is remembered
        public const int EventDays = 5;         // days a let-down or a new place can still lead to a moment
        public const int ChildDays = 21;        // days a child is new
        public const int ForgotFrom = 18;       // the hour a birthday or anniversary with no word counts as forgotten
        public const int LateHour = 22;         // "past ten": an evening is read at this hour, or the first after it
        public const int NightFrom = 2;         // a night is read between these hours
        public const int NightTo = 6;
        public const int DayFrom = 8;           // no moment before this hour: nobody is woken for one
        public const float TenseBelow = 40f;    // harmony: a lasting cause comes to a moment a day sooner
        public const float WarmFrom = 70f;      // harmony: a day later, and longer between moments
        public const float HoursFrom = 18f;     // home life's hours on a job (two days' worth): nine a day is long
        public const int BurnoutFrom = 400;     // thousandths: careers shows a burnout this high in yellow
        public const float StressFrom = 75f;    // home life's "Stressed" (HomeBalance.StressHigh)
        public const float SleepFrom = 8f;      // hours owed: home life's "Short of sleep" (HomeBalance.DebtHigh)
        public const float DietPoor = 32f;      // home life's poor diet (HomeBalance.DietPoor)
        public const float MessFrom = 25f;      // home life's "Messy home" (HomeBalance.MessLow)
        public const float MessBad = 10f;       // a tip
        public const int SpendLeast = 1500;     // money at the start's prices: a smaller purchase is never a big one
        public const int LossFrom = 5000;       // at the start's prices: a week's high below it has little to lose
        public const int LossLeast = 2000;      // money at the start's prices: a smaller fall is never a big loss
        public const int BorrowLeast = 5000;    // money at the start's prices: borrowing less is nobody's worry
        public const int SorryFirst = 10;       // points: "you always say sorry" is said before the cause's own moment

        // The hours of the day each moment can come in: home late at ten or eleven, the outing as soon as it is late,
        // the rest in the late afternoon and the evening, the towel at any hour of the day.
        private static readonly int[] FromHour = { 22, 18, 19, 17, 17, 9, 18, 19, DayFrom, 17 };
        private static readonly int[] ToHour = { 23, 22, 22, 21, 21, 22, 22, 23, DayFrom, 22 };

        /// <summary>
        /// Each cause's level now, into <paramref name="raw"/>; into <paramref name="usable"/> the same for the causes
        /// that have lasted long enough and are not resting since a moment aired them, else 0. Notes the day each cause
        /// began (and forgets it when the cause has gone), so a cause that comes and goes starts again.
        /// </summary>
        public static void Weigh(in HardSigns g, HardMemory m, int seed, int[] raw, int[] usable)
        {
            raw[0] = usable[0] = 0;
            for (int i = 1; i < Strains; i++)
            {
                var s = (Strain)i;
                int level = Level(s, g, m);
                raw[i] = level;
                if (level <= 0)
                {
                    m.Since[i] = 0;
                    usable[i] = 0;
                    continue;
                }
                if (m.Since[i] <= 0 || m.Since[i] > g.Day)
                {
                    m.Since[i] = g.Day;
                }
                usable[i] = Ripe(s, level, g, m, seed) && !Resting(s, g, m, seed) ? level : 0;
            }
        }

        /// <summary>
        /// How strongly a cause is there now, points of a hundred; 0 when it is not, or nothing of it is known.
        /// </summary>
        public static int Level(Strain s, in HardSigns g, HardMemory m)
        {
            switch (s)
            {
                case Strain.LateHome:
                    return g.LivesIn ? Steps(Bits(m.Late, 0, Week), 2, 40, 20) : 0;
                case Strain.NightsAway:
                    return g.LivesIn ? Steps(Bits(m.Away, 0, Week + 1), 2, 45, 20) : 0;
                case Strain.LongHours:
                {
                    int hours = g.WorkHours < HoursFrom ? 0 : Math.Min(100, 30 + (int)((g.WorkHours - HoursFrom) * 7f));
                    int burnout = g.Burnout < BurnoutFrom ? 0
                        : Math.Min(100, 30 + (g.Burnout - BurnoutFrom) * 70 / (1000 - BurnoutFrom));
                    return Math.Max(hours, burnout);
                }
                case Strain.Unseen:
                {
                    int talked = Math.Max(g.TalkDay, g.ContactDay);
                    if (talked <= 0)
                    {
                        return 0;   // never known to have spoken: nothing to measure the quiet from
                    }
                    int quiet = g.Day - talked;
                    return g.LivesIn ? Steps(quiet, 3, 40, 20) : Steps(quiet, 4, 40, 15);
                }
                case Strain.Worn:
                {
                    int stress = g.Stress >= StressFrom ? Math.Min(100, 40 + (int)((g.Stress - StressFrom) * 2.4f)) : 0;
                    int sleep = g.SleepDebt <= SleepFrom ? 0
                        : Math.Min(100, 40 + (int)((g.SleepDebt - SleepFrom) * 7.5f));
                    return Math.Max(stress, sleep);
                }
                case Strain.Money:
                {
                    // In the red now: once back above nothing, it is over.
                    int red = g.Cash < 0f ? Math.Min(100, 60 + 10 * Math.Max(0, g.RedMidnights)) : 0;
                    int owing = g.NetKnown && g.NetWorth < 0L ? 70 : 0;
                    int borrowed = g.Borrowed >= PriceRules.At(BorrowLeast, g.PriceLevel) ? 40 : 0;
                    return Math.Max(red, Math.Max(owing, borrowed));
                }
                case Strain.Loss:
                {
                    if (g.WeekHigh < PriceRules.At(LossFrom, g.PriceLevel))
                    {
                        return 0;
                    }
                    float fell = g.WeekHigh - g.Cash - Math.Max(0f, g.BigBuy);   // a purchase explains its own part
                    if (fell < PriceRules.At(LossLeast, g.PriceLevel))
                    {
                        return 0;
                    }
                    float share = fell / g.WeekHigh;
                    return share >= 0.8f ? 100 : share >= 0.6f ? 70 : share >= 0.4f ? 45 : 0;
                }
                case Strain.Spend:
                {
                    if (g.BigBuy < PriceRules.At(SpendLeast, g.PriceLevel))
                    {
                        return 0;
                    }
                    float part = g.BigBuy / Math.Max(1f, g.BigBuyBefore);
                    return part >= 0.75f ? 100 : part >= 0.5f ? 75 : part >= 0.25f ? 45 : 0;
                }
                case Strain.NewChild:
                {
                    int age = g.ChildDay > 0 ? g.Day - g.ChildDay : -1;
                    return age < 0 ? 0 : age <= 7 ? 80 : age <= 14 ? 60 : age <= ChildDays ? 40 : 0;
                }
                case Strain.NewPlace:
                {
                    // "Another one": a place besides the one they have, or the home moved.
                    int day = Math.Max(g.Places >= 2 ? g.PlaceDay : 0, g.MovedDay);
                    int age = day > 0 ? g.Day - day : -1;
                    return age < 0 ? 0 : age <= 2 ? 80 : age <= EventDays ? 50 : 0;
                }
                case Strain.LetDown:
                {
                    int day = Math.Max(g.LetDownDay, g.StoodUpDay);
                    if (day <= 0 || g.Day - day > EventDays || Math.Max(g.SharedDay, g.OutingDay) > day)
                    {
                        return 0;   // long ago, or made up since: time together after it
                    }
                    int times = Math.Max(0, g.LetDowns) + (g.StoodUpDay > 0 && g.Day - g.StoodUpDay <= Week ? 1 : 0);
                    return times >= 2 ? 80 : 55;
                }
                case Strain.Forgot:
                {
                    bool birthday = g.BirthdayToday && !g.BirthdayWished;
                    bool anniversary = g.SinceDay > 0 && g.YearDays > 0 && g.Day > g.SinceDay
                                       && (g.Day - g.SinceDay) % g.YearDays == 0;
                    if ((!birthday && !anniversary) || g.Hour < ForgotFrom)
                    {
                        return 0;
                    }
                    return Math.Max(g.TalkDay, g.ContactDay) >= g.Day ? 0 : 90;   // a word today: not forgotten
                }
                case Strain.Skipping:
                {
                    int skipped = g.Skipped >= 5 ? 75 : g.Skipped >= 3 ? 50 : 0;
                    int poor = g.Diet >= 0f && g.Diet <= DietPoor ? 40 : 0;
                    return Math.Max(skipped, poor);
                }
                case Strain.Mess:
                    return g.Tidy < 0f ? 0 : g.Tidy < MessBad ? 75 : g.Tidy < MessFrom ? 50 : 0;
                case Strain.Sorry:
                    return m.Sorry >= 2 && g.Day - m.SorryDay <= SorryDays ? (m.Sorry >= 3 ? 80 : 60) : 0;
                case Strain.Business:
                    return g.LivesIn ? Steps(Bits(m.LateWork | m.AwayWork, 0, Week + 1), 2, 50, 20) : 0;
                case Strain.RunningLate:
                    return g.OutingLate >= 60 ? 90 : g.OutingLate >= 30 ? 70 : 0;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// A cause that is an event, not a state: it can lead to a moment the day it happens, and is over in days.
        /// </summary>
        public static bool Sudden(Strain s)
        {
            return s == Strain.Spend || s == Strain.NewPlace || s == Strain.LetDown || s == Strain.Forgot
                   || s == Strain.RunningLate || s == Strain.Sorry;
        }

        /// <summary>
        /// Whether a lasting cause has lasted long enough to come to a moment: none for a strong one, a day for a
        /// middling one, two for a mild one, now and then a day more, a day sooner in a tense home and a day later in a
        /// warm one.
        /// </summary>
        public static bool Ripe(Strain s, int level, in HardSigns g, HardMemory m, int seed)
        {
            if (Sudden(s))
            {
                return true;
            }
            int since = m.Since[(int)s];
            if (since <= 0)
            {
                return false;
            }
            int need = level >= 80 ? 0 : level >= 50 ? 1 : 2;
            need += Mix(seed, since, (int)s) & 1;
            need += g.Harmony < TenseBelow ? -1 : g.Harmony >= WarmFrom ? 1 : 0;
            return g.Day - since >= Math.Max(0, need);
        }

        /// <summary>
        /// Whether a cause is resting: it was there when a moment came, within a week (or up to two days more, by that
        /// moment's day, so the troubles aired together come back together and the worst of them leads).
        /// </summary>
        public static bool Resting(Strain s, in HardSigns g, HardMemory m, int seed)
        {
            int spoke = m.Spoke[(int)s];
            return spoke > 0 && g.Day - spoke < RestDays + Mix(seed, spoke, 40) % 3;
        }

        /// <summary>
        /// How well a moment fits what is going on, points of a hundred, from its causes' usable levels (each weighted
        /// by how squarely the words are about it), and the cause it is most about. 0 for a household its words do not
        /// fit: the evening meal, Mom's call, the towel and the shop are for two who live together; the statement and
        /// the new place for two whose money and plans are shared.
        /// </summary>
        public static int Fit(int moment, in HardSigns g, HardMemory m, int[] raw, int[] usable, out Strain why)
        {
            why = Strain.None;
            int best = 0;
            bool shared = g.LivesIn || g.Committed;
            switch (moment)
            {
                case 0:   // "You said you would be home by seven. It is past ten. I ate by myself, again."
                    if (!g.LivesIn || !LateTonight(g, m))
                    {
                        return 0;
                    }
                    Take(usable, Strain.LateHome, 100, ref best, ref why);
                    Take(usable, Strain.NightsAway, 80, ref best, ref why);
                    Take(usable, Strain.LongHours, 50, ref best, ref why);
                    return Scaled(best, raw[(int)Strain.NewChild] > 0 ? 125 : 100);   // worse with a new child at home
                case 1:   // "...three times this week and you have not heard a word of it. Where do you go?"
                    Take(usable, Strain.Unseen, 100, ref best, ref why);
                    Take(usable, Strain.Worn, 80, ref best, ref why);
                    Take(usable, Strain.LongHours, 60, ref best, ref why);
                    if (g.LivesIn)
                    {
                        Take(usable, Strain.NightsAway, 60, ref best, ref why);
                    }
                    return Scaled(best, raw[(int)Strain.NewChild] > 0 ? 120 : 100);
                case 2:   // "You forgot. You actually forgot what today was."
                    Take(usable, Strain.Forgot, 100, ref best, ref why);
                    Take(usable, Strain.LetDown, 90, ref best, ref why);
                    return best;
                case 3:   // "We agreed to talk about big purchases first. And then I see the bank statement."
                    if (!shared)
                    {
                        return 0;
                    }
                    Take(usable, Strain.Spend, 100, ref best, ref why);
                    Take(usable, Strain.Money, 90, ref best, ref why);
                    Take(usable, Strain.Loss, 80, ref best, ref why);
                    return best;
                case 4:   // "Your mother rang me to ask if I was feeding you. I am not your housekeeper."
                    if (!g.LivesIn || !g.MomThere)
                    {
                        return 0;
                    }
                    Take(usable, Strain.Skipping, 100, ref best, ref why);
                    Take(usable, Strain.Worn, 50, ref best, ref why);
                    return best;
                case 5:   // "There is a wet towel on the bed. There is always a wet towel on the bed."
                    if (!g.LivesIn)
                    {
                        return 0;
                    }
                    Take(usable, Strain.Mess, 100, ref best, ref why);
                    Take(usable, Strain.NewChild, 60, ref best, ref why);
                    Take(usable, Strain.Worn, 50, ref best, ref why);
                    return best;
                case 6:   // "You always say sorry. You say it so quickly. I don't think you even hear..."
                {
                    int sorry = usable[(int)Strain.Sorry];
                    if (sorry <= 0)
                    {
                        return 0;
                    }
                    int other = 0;
                    for (int i = 1; i < Strains; i++)
                    {
                        if (i != (int)Strain.Sorry && usable[i] > other)
                        {
                            other = usable[i];
                        }
                    }
                    if (other <= 0)
                    {
                        return 0;   // a sorry is for something: with nothing wrong now, nothing to be upset about
                    }
                    why = Strain.Sorry;
                    return Math.Min(100, Math.Min(sorry, other) + SorryFirst);
                }
                case 7:   // "I feel like the shop gets the best of you and I get what is left over."
                    if (g.LivesIn)
                    {
                        Take(usable, Strain.Business, 100, ref best, ref why);
                    }
                    else if (g.OwnsBusiness)
                    {
                        Take(usable, Strain.Unseen, 80, ref best, ref why);
                    }
                    return best;
                case 8:   // "We were supposed to leave half an hour ago. They are all waiting. What are you doing?"
                    Take(usable, Strain.RunningLate, 100, ref best, ref why);
                    return best;
                case 9:   // "I found the plan for the new place. Another one. When were you going to tell me?"
                    if (!shared)
                    {
                        return 0;
                    }
                    Take(usable, Strain.NewPlace, 100, ref best, ref why);
                    return best;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// The moment that comes now, or -1: of the moments that fit (at least <see cref="MinFit"/>), those whose hour
        /// today has come and that the days since the last allow, the best fit, a little either way by the day, never
        /// the last one again while another fits as well, and one not had lately before one that has. With no moment
        /// fitting at all, now and then the towel for two who live together. <paramref name="why"/> is the cause it
        /// came of (None for an everyday one).
        /// </summary>
        public static int Choose(in HardSigns g, HardMemory m, int seed, IList<int> seen, int last, int lastDay,
                                 int[] raw, int[] usable, out Strain why)
        {
            why = Strain.None;
            Weigh(g, m, seed, raw, usable);
            if (g.Hour < DayFrom)
            {
                return -1;
            }
            int since = lastDay > 0 && lastDay <= g.Day ? g.Day - lastDay : int.MaxValue;
            int best = -1, bestWeight = 0, fitting = 0;
            for (int k = 0; k < Moments; k++)
            {
                int fit = Fit(k, g, m, raw, usable, out Strain cause);
                if (fit < MinFit)
                {
                    continue;
                }
                fitting++;
                if (since < (fit >= StrongFit ? MinGapDays : Gap(g.Harmony)) || g.Hour < HourOf(k, g.Day, seed))
                {
                    continue;
                }
                int weight = fit + Mix(seed, g.Day, 100 + k) % 16
                             - (k == last ? 20 : seen != null && seen.Contains(k) ? 8 : 0);
                if (weight > bestWeight)
                {
                    best = k;
                    bestWeight = weight;
                    why = cause;
                }
            }
            if (best >= 0 || fitting > 0)
            {
                return best;
            }
            if (Everyday(g, seed, since))
            {
                return Towel;
            }
            return -1;
        }

        /// <summary>
        /// The towel on a day with nothing wrong: two who live together, three weeks since the last moment, about one
        /// day in forty, at its hour.
        /// </summary>
        public static bool Everyday(in HardSigns g, int seed, int daysSinceLast)
        {
            return g.LivesIn && daysSinceLast >= EverydayGapDays && Mix(seed, g.Day, 77) % 1000 < EverydayPerMille
                   && g.Hour >= HourOf(Towel, g.Day, seed);
        }

        /// <summary>
        /// Days between two moments: five in a warm home, three in one that is so so, two in a tense one.
        /// </summary>
        public static int Gap(float harmony)
        {
            return harmony >= WarmFrom ? 5 : harmony >= TenseBelow ? 3 : MinGapDays;
        }

        /// <summary>
        /// The hour a moment can come from today: its own part of the day, at an hour of it by the day and the seed.
        /// </summary>
        public static int HourOf(int moment, int day, int seed)
        {
            int from = FromHour[moment], to = ToHour[moment];
            return from + Mix(seed, day, 200 + moment) % (to - from + 1);
        }

        /// <summary>
        /// A moment has come: it airs everything that was wrong then, so its own cause and every other cause there at
        /// the time (<paramref name="raw"/>, as last weighed) rest, and the same troubles come back in a week if they
        /// are still there, while something new can come up after the usual days between. One that came of quick
        /// sorries ends their run.
        /// </summary>
        public static void Spoke(HardMemory m, Strain why, int[] raw, int day)
        {
            for (int i = 1; i < Strains; i++)
            {
                if (i == (int)why || (raw != null && raw[i] > 0))
                {
                    m.Spoke[i] = day;
                }
            }
            if (why == Strain.Sorry)
            {
                m.Sorry = 0;
            }
        }

        /// <summary>
        /// A moment was answered: a quick "sorry" adds to a run of them (a fortnight apart at most), anything else ends
        /// it.
        /// </summary>
        public static void Said(HardMemory m, bool sorry, int day)
        {
            if (!sorry)
            {
                m.Sorry = 0;
                return;
            }
            m.Sorry = m.Sorry > 0 && day - m.SorryDay <= SorryDays ? m.Sorry + 1 : 1;
            m.SorryDay = day;
        }

        /// <summary>
        /// Family life's count of broken promises, read again: a rise is a let-down today, and the run is how many in
        /// the week to it. The first count of a save (and a count that fell) is taken as it is: what happened before is
        /// not a let-down now.
        /// </summary>
        public static void NotePromises(HardMemory m, int broken, int day)
        {
            if (m.Promises < 0 || broken < m.Promises)
            {
                m.Promises = broken;
                return;
            }
            if (broken == m.Promises)
            {
                return;
            }
            int more = broken - m.Promises;
            m.PromiseRun = m.PromiseDay > 0 && day - m.PromiseDay <= Week ? m.PromiseRun + more : more;
            m.PromiseDay = day;
            m.Promises = broken;
        }

        /// <summary>
        /// The player's own places and the home, read again: one more place is a new place today, and the home
        /// somewhere else a move. The first count of a save is taken as it is; a place given up is no news.
        /// </summary>
        public static void NotePlaces(HardMemory m, int places, int homeKey, int day)
        {
            if (m.Places >= 0 && places > m.Places)
            {
                m.PlaceDay = day;
            }
            m.Places = places;
            if (homeKey != 0)
            {
                if (m.HomeKey != 0 && homeKey != m.HomeKey)
                {
                    m.MovedDay = day;
                }
                m.HomeKey = homeKey;
            }
        }

        /// <summary>
        /// Moves the marks on to <paramref name="day"/>: each day passed shifts them a place; a month or more, or a
        /// clock gone back, clears them.
        /// </summary>
        public static void Roll(HardMemory m, int day)
        {
            if (day <= 0 || day == m.Base)
            {
                return;
            }
            int shift = m.Base > 0 ? day - m.Base : MarkDays;
            if (shift < 0 || shift >= MarkDays)
            {
                m.Late = m.LateWork = m.Away = m.AwayWork = 0;
            }
            else
            {
                m.Late = (m.Late << shift) & int.MaxValue;
                m.LateWork = (m.LateWork << shift) & int.MaxValue;
                m.Away = (m.Away << shift) & int.MaxValue;
                m.AwayWork = (m.AwayWork << shift) & int.MaxValue;
            }
            m.Base = day;
        }

        /// <summary>
        /// Marks a day (an evening, or the night that began on it) in a mark of the memory based on
        /// <paramref name="baseDay"/>.
        /// </summary>
        public static void Mark(ref int mask, int baseDay, int day)
        {
            int n = baseDay - day;
            if (n >= 0 && n < MarkDays)
            {
                mask |= 1 << n;
            }
        }

        /// <summary>How many of the days from <paramref name="from"/> days ago, so many of them, are marked.</summary>
        public static int Bits(int mask, int from, int count)
        {
            int n = 0;
            for (int i = Math.Max(0, from); i < from + count && i < MarkDays; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>Tonight is one of the evenings out after ten.</summary>
        public static bool LateTonight(in HardSigns g, HardMemory m)
        {
            return m.Base == g.Day && (m.Late & 1) != 0;
        }

        /// <summary>
        /// A home's address as a number that is the same on every machine and in every session (FNV-1a); never 0.
        /// </summary>
        public static int Key(string street, int number)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; street != null && i < street.Length; i++)
                {
                    h = (h ^ street[i]) * 16777619u;
                }
                h = (h ^ (uint)number) * 16777619u;
                int key = (int)(h & 0x7fffffff);
                return key == 0 ? 1 : key;
            }
        }

        /// <summary>A number from three, the same for the same three on every machine: 0 to int.MaxValue.</summary>
        public static int Mix(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u ^ (uint)c * 0xC2B2AE3Du;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return (int)(h & 0x7fffffff);
            }
        }

        private static int Steps(int n, int from, int first, int step)
        {
            return n < from ? 0 : Math.Min(100, first + (n - from) * step);
        }

        private static int Scaled(int value, int percent)
        {
            return Math.Min(100, value * percent / 100);
        }

        private static void Take(int[] usable, Strain s, int percent, ref int best, ref Strain why)
        {
            int value = usable[(int)s] * percent / 100;
            if (value > best)
            {
                best = value;
                why = s;
            }
        }
    }
}
