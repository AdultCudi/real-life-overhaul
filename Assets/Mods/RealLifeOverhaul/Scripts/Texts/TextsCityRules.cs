using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Texts
{
    /// <summary>What one of the player's staff says now (TextsCityRules.Staff).</summary>
    public enum TextStaffSay { None, RaiseAsk, Sick, RaiseThanks, Hours }

    /// <summary>
    /// When the city's new talk of data v6 speaks, and who; the same answer on every machine, no random number but a hash.
    /// Game-free. COST: a pass over the staff or the people, a few dozen, once a game hour.
    /// </summary>
    public static class TextsCityRules
    {
        public const int StaffFrom = 9, StaffTo = 20;   // game hours a message from the staff may come between
        public const int SickFrom = 6, SickTo = 11;     // ...one saying they are off sick
        public const int BigShare = 25;                 // per cent of the net worth a day's move must be to be a big win or loss
        public const int EventsMost = 32;               // life events kept, the newest, for gossip and the people's memory
        public const int EventDays = 60;                // ...and for so many days
        public const int GossipFrom = 1, GossipTo = 3;  // days after an event that gossip may pass it on
        public const int GossipHourFrom = 10, GossipHours = 9;   // ...in the nine game hours from 10, the hour by the hash

        // The written lines' keys without their kind and number, built so the catalogue's check does not read them as keys.
        public const string LifeStem = "reallife" + "_texts_life_";
        public const string GossipStem = "reallife" + "_texts_gossip_";

        /// <summary>What one of the staff says now, the mark moved so nothing is said twice; a first sight only notes their raise.</summary>
        public static TextStaffSay Staff(TextStaff s, TextStaffMark m, int day, int hour)
        {
            if (s == null || m == null)
            {
                return TextStaffSay.None;
            }
            m.SeenDay = day;
            if (m.RaiseSeen < 0)
            {
                m.RaiseSeen = s.Raise;   // seen for the first time: nothing to say of what came before
                return TextStaffSay.None;
            }
            if (m.LastDay == day)
            {
                return TextStaffSay.None;   // said something today already: a raise seen now is thanked tomorrow
            }
            // The raise is marked seen only when it is thanked for: one first seen on a day they texted something else (off
            // sick, asking for a raise) is thanked on a later look, once.
            int was = m.RaiseSeen;
            TextStaffSay say = TextStaffSay.None;
            if (s.Sick && m.SickTold != day && hour >= SickFrom && hour < SickTo)
            {
                say = TextStaffSay.Sick;
                m.SickTold = day;
            }
            else if (hour < StaffFrom || hour >= StaffTo)
            {
                return TextStaffSay.None;   // seen again in business hours
            }
            else if (s.AskDay == day && s.AskTo > s.Paid && m.AskTold != day)
            {
                say = TextStaffSay.RaiseAsk;
                m.AskTold = day;
            }
            else if (s.Raise > was && s.Raise > 0)
            {
                say = TextStaffSay.RaiseThanks;
                m.RaiseSeen = s.Raise;   // thanked once for it
            }
            else if (s.MostHours > 0 && s.WeekHours > s.MostHours && !s.Complaining && m.HoursWeek != Week(day))
            {
                say = TextStaffSay.Hours;
                m.HoursWeek = Week(day);
            }
            if (s.Raise < was)
            {
                m.RaiseSeen = s.Raise;   // a raise taken back: nothing owed, and a later one is measured from here
            }
            if (say != TextStaffSay.None)
            {
                m.LastDay = day;
            }
            return say;
        }

        /// <summary>The game week a day falls in, seven days from day 1.</summary>
        public static int Week(int day) => day <= 0 ? 0 : (day - 1) / 7;

        /// <summary>Adds the player's life events since the last look, the first look only noting where things stand; how many.</summary>
        public static int Life(TextLife now, TextLifeMark mark, long floor, List<TextLifeEvent> into)
        {
            int before = into.Count;
            if (now == null || mark == null)
            {
                return 0;
            }
            if (mark.Day < 0)
            {
                Note(now, mark);
                return 0;
            }
            if (now.Children > mark.Children)
            {
                into.Add(new TextLifeEvent { Kind = "baby", Day = now.Day, Child = now.ChildName, Adopted = now.Adopted, Value = now.Partner });
            }
            if (now.Married && now.MarriedDay > mark.MarriedDay)
            {
                into.Add(new TextLifeEvent { Kind = "married", Day = now.Day, Value = now.Partner });
            }
            if (now.Degree != null && mark.DegreeDay != now.Day)
            {
                into.Add(new TextLifeEvent { Kind = "graduated", Day = now.Day, Value = now.Degree });
                mark.DegreeDay = now.Day;
            }
            if (now.CourtClosed > mark.CourtClosed)
            {
                into.Add(new TextLifeEvent { Kind = now.CourtGuilty ? "court_guilty" : "court_cleared", Day = now.Day });
            }
            if (now.Listed != null && now.ListedDay > mark.ListedDay)
            {
                into.Add(new TextLifeEvent { Kind = "listed", Day = now.Day, Value = now.Listed });
            }
            if (now.Business != null && now.BusinessDay > mark.BusinessDay)
            {
                into.Add(new TextLifeEvent { Kind = "opened", Day = now.Day, Value = now.Business });
            }
            if (now.HasWorth && mark.WorthDay >= 0 && now.Day > mark.WorthDay)
            {
                long move = now.Worth - mark.Worth;
                long big = Math.Max(floor, Math.Abs(mark.Worth) * BigShare / 100);
                if (big > 0 && Math.Abs(move) >= big)
                {
                    into.Add(new TextLifeEvent { Kind = move > 0 ? "win" : "loss", Day = now.Day, Value = Math.Abs(move).ToString(System.Globalization.CultureInfo.InvariantCulture) });
                }
            }
            Note(now, mark);
            return into.Count - before;
        }

        // Moves the mark to now: every figure, the worth once a day.
        private static void Note(TextLife now, TextLifeMark mark)
        {
            mark.Day = now.Day;
            mark.Children = now.Children;
            mark.MarriedDay = now.Married ? Math.Max(mark.MarriedDay, now.MarriedDay) : mark.MarriedDay;
            mark.CourtClosed = now.CourtClosed;
            mark.ListedDay = now.Listed != null ? Math.Max(mark.ListedDay, now.ListedDay) : mark.ListedDay;
            mark.BusinessDay = now.Business != null ? Math.Max(mark.BusinessDay, now.BusinessDay) : mark.BusinessDay;
            if (now.HasWorth && now.Day > mark.WorthDay)
            {
                mark.Worth = now.Worth;
                mark.WorthDay = now.Day;
            }
        }

        /// <summary>Who texts about a kind of event, in order, with the card's intent; none for what the social life texts itself.</summary>
        public static string[][] SenderOrder(string kind)
        {
            switch (kind)
            {
                case "crash":
                case "knocked": return new[] { new[] { "partner", "partner.crash" }, new[] { "mom", "mom.news.crash" } };
                case "court_guilty":
                case "court_cleared": return new[] { new[] { "partner", "partner.court" }, new[] { "mom", "mom.news.court" } };
                case "graduated": return new[] { new[] { "mom", "mom.news.graduated" }, new[] { "close", "friend.graduated" } };
                case "listed": return new[] { new[] { "friend", "friend.listed" }, new[] { "mom", "mom.news.listed" } };
                case "opened": return new[] { new[] { "close", "friend.congrats_open" }, new[] { "mom", "mom.news.opened" } };
                case "married": return new[] { new[] { "close", "friend.married" } };
                case "baby": return new[] { new[] { "mom", "mom.news.baby" }, new[] { "close", "friend.baby" } };
                case "win": return new[] { new[] { "friend", "friend.win" } };
                case "loss": return new[] { new[] { "partner", "partner.loss" }, new[] { "friend", "friend.loss" } };
                default: return new string[0][];
            }
        }

        /// <summary>The branch of a life event's card: the court's verdict, a child born or adopted; null for the rest.</summary>
        public static string Branch(TextLifeEvent e)
        {
            switch (e.Kind)
            {
                case "court_guilty": return "guilty";
                case "court_cleared": return "cleared";
                case "baby": return e.Adopted ? "adopted" : "born";
                default: return null;
            }
        }

        /// <summary>Who texts about an event now: the first of its order who is free to; false for nobody.</summary>
        public static bool Sender(TextLifeEvent e, List<TextPerson> people, int day, out TextLifeSender sender)
        {
            sender = new TextLifeSender { Index = -1 };
            string[][] order = SenderOrder(e.Kind);
            for (int i = 0; i < order.Length; i++)
            {
                TextPerson p = Who(order[i][0], people, day, e.Kind);
                if (p == null)
                {
                    continue;
                }
                sender.Index = p.Index;
                sender.Intent = order[i][1];
                sender.Branch = Branch(e);
                sender.Key = LifeKey(sender.Intent, sender.Branch, Hash(e.Day, e.Kind, p.Seed));
                return true;
            }
            return false;
        }

        // The person of a role who may text today, or null.
        private static TextPerson Who(string role, List<TextPerson> people, int day, string kind)
        {
            TextPerson best = null;
            for (int i = 0; i < people.Count; i++)
            {
                TextPerson p = people[i];
                if (!Free(p, day))
                {
                    continue;
                }
                switch (role)
                {
                    case "partner":
                        if (p.Together)
                        {
                            return p;
                        }
                        break;
                    case "mom":
                        if (p.Family)
                        {
                            return p;
                        }
                        break;
                    default:
                        if (p.Family || p.Together || p.Stage < 2)
                        {
                            break;   // a friend: not the partner nor Mom, and a friend at least
                        }
                        if (best == null || Closer(p, best, day, kind))
                        {
                            best = p;
                        }
                        break;
                }
            }
            return best;
        }

        // Whether a person may text the player today: met, not drifted apart, awake, and not texted today.
        private static bool Free(TextPerson p, int day) => p != null && p.Met && !p.Drifted && !p.Asleep && p.LastTextDay != day;

        // The closer friend: the higher stage, then the closer, then the lower hash of the day, the event and their seed.
        private static bool Closer(TextPerson a, TextPerson b, int day, string kind)
        {
            if (a.Stage != b.Stage)
            {
                return a.Stage > b.Stage;
            }
            if (a.Closeness != b.Closeness)
            {
                return a.Closeness > b.Closeness;
            }
            return Hash(day, kind, a.Seed) < Hash(day, kind, b.Seed);
        }

        /// <summary>The written line's key of a life event's card: the intent, the branch, and one of two lines by the hash.</summary>
        public static string LifeKey(string intent, string branch, uint hash)
        {
            string stem = LifeStem + intent.Replace('.', '_') + (branch != null ? "_" + branch : string.Empty);
            return stem + "_" + (1 + hash % 2).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>True for an event of the player's that gossip passes on: what could be seen or read of the player in public.</summary>
        public static bool Public(string kind)
        {
            switch (kind)
            {
                case "crash":
                case "arrest":
                case "court_guilty":
                case "court_cleared":
                case "opened":
                case "graduated":
                case "listed":
                case "married":
                case "baby":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The gossip branch of an event's kind: the court's two verdicts are one.</summary>
        public static string GossipKind(string kind) => kind == "court_guilty" || kind == "court_cleared" ? "court" : kind;

        /// <summary>Whether two people of the player's life know each other, by the social life's own data.</summary>
        public static bool Knows(TextPerson a, TextPerson b)
        {
            if (a == null || b == null || a.Index == b.Index)
            {
                return false;
            }
            if (a.District == b.District)
            {
                return true;
            }
            if (a.Origin == b.Origin && SharedPlace(a.Origin))
            {
                return true;
            }
            return (a.Together && (b.Family || b.Stage == 3)) || (b.Together && (a.Family || a.Stage == 3));
        }

        // A place where two people the player met could have met each other too (SocialOrigin: Gym 2, Work 3, CoffeeCart 4, Neighbour 5, Customer 7, Event 9).
        private static bool SharedPlace(int origin) => origin == 2 || origin == 3 || origin == 4 || origin == 5 || origin == 7 || origin == 9;

        /// <summary>Who passes an event on now, at its hour in the days after it; false for one not public or passed on already.</summary>
        public static bool Gossip(TextLifeEvent e, List<TextPerson> people, int day, int hour, out int teller, out int hearer)
        {
            teller = hearer = -1;
            if (e == null || e.Gossiped || !Public(e.Kind) || day - e.Day < GossipFrom || day - e.Day > GossipTo || hour != GossipHour(e))
            {
                return false;
            }
            TextPerson told = null;
            for (int i = 0; i < people.Count; i++)
            {
                if (people[i].Index == e.Texted && people[i].Met && !people[i].Family)
                {
                    told = people[i];
                }
            }
            if (told == null)
            {
                for (int i = 0; i < people.Count; i++)
                {
                    TextPerson p = people[i];
                    if (p.Met && !p.Drifted && !p.Family && !p.Together && p.Stage >= 2 && (told == null || Closer(p, told, e.Day, e.Kind)))
                    {
                        told = p;
                    }
                }
            }
            if (told == null)
            {
                return false;
            }
            TextPerson best = null;
            for (int i = 0; i < people.Count; i++)
            {
                TextPerson p = people[i];
                if (!Free(p, day) || p.Family || p.Together || p.Index == told.Index || p.Index == e.Texted || !Knows(p, told))
                {
                    continue;
                }
                if (best == null || Hash(e.Day, e.Kind, p.Seed) < Hash(e.Day, e.Kind, best.Seed))
                {
                    best = p;
                }
            }
            if (best == null)
            {
                return false;
            }
            teller = told.Index;
            hearer = best.Index;
            return true;
        }

        public const int HoursInDay = 24, HoursMost = 24;   // a pass is made for each game hour that went by between two looks, a day's worth at most

        /// <summary>
        /// How many game hours the city's pass is owed from one look to the next: one for the hour just begun, and one for each it skipped
        /// when time ran fast; a day's worth at most, and one when the clock stands still or runs back (a load).
        /// </summary>
        public static int HourSteps(int lastDay, int lastHour, int day, int hour)
        {
            long gone = ((long)day * HoursInDay + hour) - ((long)lastDay * HoursInDay + lastHour);
            return gone < 1 ? 1 : gone > HoursMost ? HoursMost : (int)gone;
        }

        /// <summary>The day and hour that were <paramref name="back"/> hours before a day and hour.</summary>
        public static void HourBack(int day, int hour, int back, out int dayThen, out int hourThen)
        {
            long at = (long)day * HoursInDay + hour - back;
            dayThen = (int)Math.Floor(at / (double)HoursInDay);
            hourThen = (int)(at - (long)dayThen * HoursInDay);
        }

        /// <summary>The game hour an event's gossip may come at, by the hash of its day and kind.</summary>
        public static int GossipHour(TextLifeEvent e) => GossipHourFrom + (int)(Hash(e.Day, e.Kind, 0) % GossipHours);

        /// <summary>The written line's key of gossip: one of two lines of its kind by the hash.</summary>
        public static string GossipKey(string kind, uint hash) => GossipStem + GossipKind(kind) + "_" + (1 + hash % 2).ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>FNV-1a of a day, a word and a seed: the same on every machine.</summary>
        public static uint Hash(int day, string word, int seed)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)day) * 16777619u;
                for (int i = 0; word != null && i < word.Length; i++)
                {
                    h = (h ^ word[i]) * 16777619u;
                }
                h = (h ^ (uint)seed) * 16777619u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                return h ^ (h >> 15);
            }
        }

        /// <summary>The figure names of a review, in the order of the game's insight page.</summary>
        public static readonly string[] ReviewFigures = { "service", "prices", "cleanliness", "interior" };

        /// <summary>What a review is mostly about: the best figure for four or five stars, else the worst, a tie to the first.</summary>
        public static string ReviewAbout(int stars, int service, int pricing, int cleanliness, int interior)
        {
            int[] f = { service, pricing, cleanliness, interior };
            bool good = stars >= 4;
            int pick = 0;
            for (int i = 1; i < f.Length; i++)
            {
                if (good ? f[i] > f[pick] : f[i] < f[pick])
                {
                    pick = i;
                }
            }
            return ReviewFigures[pick];
        }

        /// <summary>Keeps the newest <see cref="EventsMost"/> events, none older than <see cref="EventDays"/>; how many went.</summary>
        public static int Prune(List<TextLifeEvent> events, int day)
        {
            int gone = 0;
            for (int i = events.Count - 1; i >= 0; i--)
            {
                if (day - events[i].Day > EventDays)
                {
                    events.RemoveAt(i);
                    gone++;
                }
            }
            while (events.Count > EventsMost)
            {
                events.RemoveAt(0);
                gone++;
            }
            return gone;
        }
    }
}
