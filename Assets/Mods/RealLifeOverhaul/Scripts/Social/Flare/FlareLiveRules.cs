using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Flare
{
    /// <summary>One reaction of somebody with a name to a post: a like, a reply or a share, so many minutes after it went up.</summary>
    public struct FlareReaction
    {
        public char Kind;            // 'L' a like, 'R' a reply, 'S' a share
        public string Who;           // "f3" a friend, "p" the partner (a friend's number follows: "p4"), "s:Name" one of the staff, "b" the boss, "c12" one of the city's people
        public int Delay;            // minutes after the post
    }

    /// <summary>One line of the Notifications screen, worked out when it is shown.</summary>
    public struct FlareNotice
    {
        public FlareNoticeKind Kind;
        public long At;
        public int Post;             // the post it is about (local number), or -1
        public string Who;           // as a reaction names them, or an account key
        public int Dm;               // a message's number, or -1
    }

    /// <summary>
    /// Flare as a living network, by rule: the city's people (a cast made from the city's seed), the posts the city
    /// makes each day from what really happened (the weather, the news, prices, the street's calendar, the election, the
    /// works, the market) with official accounts and businesses among them, the reactions to a post that come in over
    /// the next hours from people with names, a pile-on, the City Office's cases (when it answers, what it does, how many
    /// crews it has, the tally of ignored complaints that reaches the paper), what a review, a complaint or an
    /// announcement does to a business's customers, Trending, the tags and the typed text's limits, the messages of the
    /// city's people, the notifications, and what is kept. Game-free: whoever calls says what day it is and what the city
    /// is like; EconomyCheck runs these rules (FlareLiveChecks.cs) against a city made up for it.
    ///
    /// Everything that is rolled is rolled with <see cref="FlareRules.Roll"/> from the city's seed and the day, so the same
    /// city on the same day makes the same posts, and in a Going Public session the host's are everybody's.
    ///
    /// A DAY OF THE FEED (the option at Normal): about 14 posts of the city's people (a fifth of them reports or reviews),
    /// 2 to 5 from official accounts, 4 from businesses, the paper's headlines, the market's results, the influencers' and
    /// the friends' own; each with its hour and minute, shown from then on. Quiet has about half, Busy nearly twice.
    /// How much the city says is set by the day and by each post, never by how many people it has: the four hundred are
    /// who says it (a few rolled for each post, reaction, reply and message), so nothing here walks all of them.
    /// </summary>
    public static class FlareLiveRules
    {
        public const int TextMost = 200;               // characters the player may type in a post or a reply
        public const int DmTextMost = 300;             // ...in a message
        public const int TagsMost = 5;                 // tags kept of one post
        public const int TagLength = 24;
        public const int ReportsWeighed = 3;           // the player's reports a day that add to an issue's weight in the council (any number may be made)
        public const int MovedMost = 200;              // what moved a business or the City Office today, kept (FlareNet.Moved): a day's worth
        public const int Citizens = 400;               // the city's people on Flare (derived from the city's seed, never saved)
        public const int FirstCitizens = 40;           // ...the first of them, made as they always were (every save's "c0" to "c39" stays who it was)
        public const int CityKeepDays = 3;             // the city's posts are kept this many days (the player's, the friends' longer)
        public const int NetworkKeepDays = 4;          // ...the players' of a session

        public static readonly int[] CityTalkPerDay = { 7, 14, 24 };   // the city's people's posts, by the option: quiet, normal, busy
        public static readonly int[] OfficialMost = { 2, 4, 6 };
        public static readonly int[] BusinessPerDay = { 2, 4, 7 };

        // ---- the City Office
        public const int ReplyLeast = 45;              // minutes before an answer, at the least...
        public const int ReplyPerOpen = 25;            // ...and so many more for each case still open
        public const int ReplyMost = 300;              // ...within five hours at the most
        public const int WeekDays = 7;
        public static readonly int[] CrewsPerWeek = { 0, 3, 2, 2, 3, 0, 0, 2 };   // by FlareOrder: works, patrols, cleanups, inspections, site checks
        public const int PatrolDays = 3;               // the police in a district after a crime report
        public const int CleanupDays = 5;              // a cleaning crew's round in a district
        public const int InspectionDays = 3;           // the talk against an inspected business
        public const int TallyToPaper = 3;             // complaints of one kind in one district in a week, none acted on: the paper has it
        public const int MoodActed = 3;                // the feed's mood about the city's running: a report acted on
        public const int MoodQueued = -4;              // ...one left
        public const int MoodEscalated = -20;          // ...the tally in the paper
        public const int ReportsWeigh = 400;           // thousandths of a point of an issue's weight in the council a report adds (Wiring/FlareLinks: Issue)

        // ---- what a post does to a business, per cent of its customers (inside FlareRules.BuzzMost, either way)
        public const int ReviewDays = 2;
        public const int ComplaintBuzz = -2;
        public const int AnnounceDays = 2;
        public const int EventCrowd = 2;               // an event at a business: customers on its day
        public const int CleanFootfall = 1;            // per cent more customers in a district while the City Office's cleaners go round it

        // ---- the reactions over the hours
        public const int CurveMinutes = 150;           // likes come in on a curve: two thirds in the first two and a half hours
        public const int CitizensReactMost = 6;        // the city's people with names who react to one post
        public const int PileOnFrom = 150;             // a post that went badly and was seen by this many: a pile-on
        public const int PileOnLeast = 4, PileOnMost = 10;

        // ---- the city's people's messages
        public const int DmGapDays = 2;
        public const int DmChance = 450;               // per mille a day after the gap

        private static readonly string[] FirstNames =
        {
            "Ada", "Bea", "Cal", "Dina", "Eli", "Fay", "Gus", "Hana", "Ivo", "Juno", "Kai", "Lena", "Milo", "Nell", "Otto", "Pia",
            "Quin", "Rae", "Seb", "Tess", "Uma", "Vic", "Wes", "Xena", "Yara", "Zeke", "Arlo", "Bree", "Cyd", "Dex", "Elsa", "Finn"
        };

        private static readonly string[] LastNames =
        {
            "Abbott", "Barlow", "Calder", "Dunmore", "Ellery", "Fenwick", "Garrow", "Hollis", "Imber", "Jarvis", "Kestrel", "Lowry",
            "Maddox", "Norland", "Oakes", "Pemberly", "Quarry", "Ramsey", "Stroud", "Tilney", "Upton", "Varley", "Whitcombe", "Yardley",
            "Ashby", "Brook", "Corwin", "Darby", "Easton", "Frost", "Greer", "Hale"
        };

        // Whether each of the first names above is a woman's.
        private static readonly bool[] FemaleFirst =
        {
            true, true, false, true, false, true, false, true, false, true, false, true, false, true, false, true,
            false, true, false, true, true, false, false, true, true, false, false, true, false, false, true, false
        };

        // The names of the city's people after the first forty: the names above and more, a woman's or a man's.
        private static readonly string[] Women =
        {
            "Ada", "Bea", "Dina", "Fay", "Hana", "Juno", "Lena", "Nell", "Pia", "Rae", "Tess", "Uma", "Xena", "Yara", "Bree", "Elsa",
            "Alba", "Amara", "Asha", "Bella", "Clara", "Cleo", "Daisy", "Edie", "Esme", "Freya", "Greta", "Gwen", "Hazel", "Ines", "Iris", "Jade",
            "Joy", "Kara", "Leah", "Liv", "Lucia", "Mae", "Maya", "Mei", "Mina", "Nina", "Nora", "Olive", "Orla", "Priya", "Rosa", "Ruby",
            "Sadie", "Sian", "Sofia", "Tara", "Thea", "Vera", "Viola", "Willa", "Wren", "Yasmin", "Zara", "Zoe"
        };

        private static readonly string[] Men =
        {
            "Cal", "Eli", "Gus", "Ivo", "Kai", "Milo", "Otto", "Quin", "Seb", "Vic", "Wes", "Zeke", "Arlo", "Cyd", "Dex", "Finn",
            "Abe", "Adam", "Ben", "Carl", "Cole", "Dan", "Diego", "Emil", "Ezra", "Felix", "Frank", "Gabe", "Hank", "Hugo", "Ian", "Isaac",
            "Jack", "Jude", "Kenji", "Kofi", "Leo", "Luis", "Marco", "Max", "Ned", "Noah", "Omar", "Oscar", "Paul", "Pete", "Raj", "Ravi",
            "Rex", "Simon", "Theo", "Tom", "Victor", "Vince", "Wade", "Will", "Yusuf", "Zane"
        };

        private static readonly string[] Surnames =
        {
            "Abbott", "Barlow", "Calder", "Dunmore", "Ellery", "Fenwick", "Garrow", "Hollis", "Imber", "Jarvis", "Kestrel", "Lowry",
            "Maddox", "Norland", "Oakes", "Pemberly", "Quarry", "Ramsey", "Stroud", "Tilney", "Upton", "Varley", "Whitcombe", "Yardley",
            "Ashby", "Brook", "Corwin", "Darby", "Easton", "Frost", "Greer", "Hale",
            "Adler", "Archer", "Baines", "Bishop", "Blake", "Brennan", "Carver", "Castillo", "Chen", "Cross", "Dalton", "Doyle",
            "Duarte", "Dunn", "Ellis", "Ennis", "Evans", "Fairfax", "Fischer", "Flynn", "Fox", "Garcia", "Gilmore", "Goldberg",
            "Harlow", "Hart", "Hayes", "Hughes", "Iqbal", "Irving", "Ito", "Jensen", "Kaur", "Keane", "Kim", "Kowalski",
            "Lambert", "Lane", "Lin", "Lyle", "Mendes", "Mercer", "Moreno", "Murphy", "Nash", "Nguyen", "Nolan", "Novak",
            "Okafor", "Ortiz", "Osei", "Porter", "Price", "Quigley", "Reyes", "Romero", "Rowe", "Shah", "Silva", "Sutton",
            "Tan", "Thorne", "Torres", "Underwood", "Vance", "Vega", "Walsh", "Ward", "Webb", "Xu", "Yates", "Young", "Zimmer"
        };

        private const int SaltFirst = 601, SaltLast = 602, SaltNumber = 603, SaltDistrict = 604, SaltPersona = 605;   // the first forty's rolls, as they always were
        private const int SaltSex = 606, SaltAge = 607, SaltName = 608, SaltNameLow = 609;                            // the rest's, and everybody's age
        private const int NameTries = 8;               // rolls for a name nobody in the city has yet, before the surnames are walked in order

        // ---------------------------------------------------------------- the clock

        /// <summary>Game minutes since day 0.</summary>
        public static long Now(int day, int hour, int minute)
        {
            return ((long)day * 24 + hour) * 60 + minute;
        }

        /// <summary>A post is shown from its minute on.</summary>
        public static bool Visible(FlarePost p, long now)
        {
            return p != null && p.At <= now;
        }

        // ---------------------------------------------------------------- the city's people

        /// <summary>The seed the city's people and posts are made from: the city's (a host's, for a guest that followed one), else the account's cast's.</summary>
        public static int CitySeed(FlareFile f)
        {
            return f.Net.CitySeed != 0 ? f.Net.CitySeed : f.CastSeed != 0 ? f.CastSeed : f.Seed;
        }

        /// <summary>
        /// The city's people on Flare, made from the city's seed: the same names, handles, districts and natures for the same
        /// seed, on every machine. The first <see cref="FirstCitizens"/> are made exactly as they always were, so that every
        /// key a save keeps for one of them ("c12" in a message, a post, a reaction) is still the same person. Each after
        /// them has a name nobody before them in the city has and goes to the district with the fewest people so far, so
        /// every district has its share (44 or 45 of the 400 in nine). Allocates the list's people and a set of names, once for
        /// a seed (Flare starting, or a guest taking the host's city): some 0.2 ms for the four hundred.
        /// </summary>
        public static void CitizensOf(int seed, IReadOnlyList<string> districts, List<FlareCitizen> into)
        {
            into.Clear();
            var names = new HashSet<string>(StringComparer.Ordinal);
            int n = districts != null ? districts.Count : 0;
            var living = new int[n];   // the city's people in each district so far
            for (int i = 0; i < Citizens; i++)
            {
                FlareCitizen c = i < FirstCitizens ? First(seed, i, districts) : After(seed, i, districts, living, names);
                names.Add(c.First + " " + c.Last);
                for (int d = 0; d < n && c.District != null; d++)
                {
                    if (string.Equals(districts[d], c.District, StringComparison.Ordinal))
                    {
                        living[d]++;
                        break;
                    }
                }
                into.Add(c);
            }
        }

        // One of the first forty, as the city has always had them.
        private static FlareCitizen First(int seed, int i, IReadOnlyList<string> districts)
        {
            int first = FlareRules.Roll(seed, i, SaltFirst) % FirstNames.Length;
            int last = (FlareRules.Roll(seed, i, SaltLast) + i * 7) % LastNames.Length;
            string f = FirstNames[first], l = LastNames[last];
            var persona = (FlarePersona)(FlareRules.Roll(seed, i, SaltPersona) % 8);
            return new FlareCitizen
            {
                Index = i, First = f, Last = l,
                Handle = "@" + f.ToLowerInvariant() + "_" + l.ToLowerInvariant() + (10 + FlareRules.Roll(seed, i, SaltNumber) % 90),
                District = districts != null && districts.Count > 0 ? districts[FlareRules.Roll(seed, i, SaltDistrict) % districts.Count] : null,
                Persona = persona, Female = FemaleFirst[first], Age = AgeOf(persona, FlareRules.Roll(seed, i, SaltAge))
            };
        }

        // One of the people after the first forty: a woman or a man by a roll, a name nobody before them has, a district in turn.
        private static FlareCitizen After(int seed, int i, IReadOnlyList<string> districts, int[] living, HashSet<string> taken)
        {
            bool female = FemaleAfter(seed, i);
            string[] firsts = female ? Women : Men;
            string f = null, l = null;
            for (int t = 0; t < NameTries && f == null; t++)
            {
                int roll = Wide(seed, i * NameTries + t, SaltName);
                string a = firsts[roll % firsts.Length], b = Surnames[roll / firsts.Length % Surnames.Length];
                if (!taken.Contains(a + " " + b))
                {
                    f = a;
                    l = b;
                }
            }
            if (f == null)
            {
                // Unlucky every time: the surnames in order from a rolled one, then the next first name (some 7,000 names for 400 people: found at once).
                int start = Wide(seed, i, SaltNameLow);
                for (int k = 0; k < firsts.Length * Surnames.Length && f == null; k++)
                {
                    string a = firsts[(start + k / Surnames.Length) % firsts.Length], b = Surnames[(start + k) % Surnames.Length];
                    if (!taken.Contains(a + " " + b))
                    {
                        f = a;
                        l = b;
                    }
                }
                f = f ?? firsts[0];
                l = l ?? Surnames[0];
            }
            var persona = (FlarePersona)(FlareRules.Roll(seed, i, SaltPersona) % 8);
            // The district with the fewest people so far (the first forty's included), from a rolled one on: the city fills evenly.
            int n = living.Length, home = -1;
            for (int k = 0, from = n > 0 ? FlareRules.Roll(seed, i, SaltDistrict) % n : 0; k < n; k++)
            {
                int d = (from + k) % n;
                home = home < 0 || living[d] < living[home] ? d : home;
            }
            return new FlareCitizen
            {
                Index = i, First = f, Last = l,
                Handle = "@" + f.ToLowerInvariant() + "_" + l.ToLowerInvariant() + (10 + FlareRules.Roll(seed, i, SaltNumber) % 90),
                District = home >= 0 ? districts[home] : null,
                Persona = persona, Female = female, Age = AgeOf(persona, FlareRules.Roll(seed, i, SaltAge))
            };
        }

        /// <summary>Whether one of the city's people is a woman (as CitizensOf makes them; each worked out on its own, without the list).</summary>
        public static bool CitizenFemale(int seed, int index)
        {
            return index < FirstCitizens ? FemaleFirst[FlareRules.Roll(seed, index, SaltFirst) % FirstNames.Length] : FemaleAfter(seed, index);
        }

        /// <summary>One of the city's people's age in years (as CitizensOf makes them; worked out on its own, without the list).</summary>
        public static int CitizenAge(int seed, int index)
        {
            return AgeOf((FlarePersona)(FlareRules.Roll(seed, index, SaltPersona) % 8), FlareRules.Roll(seed, index, SaltAge));
        }

        private static bool FemaleAfter(int seed, int index)
        {
            return FlareRules.Roll(seed, index, SaltSex) < 500;
        }

        /// <summary>An age that suits what somebody is like: a student 18 to 25, a parent 27 to 50, a retiree 65 to 82, anybody else 20 to 64.</summary>
        public static int AgeOf(FlarePersona persona, int roll)
        {
            roll = FlareRules.Clamp(roll, 0, 999);
            switch (persona)
            {
                case FlarePersona.Student: return 18 + roll % 8;
                case FlarePersona.Parent: return 27 + roll % 24;
                case FlarePersona.Retiree: return 65 + roll % 18;
                default: return 20 + roll % 45;
            }
        }

        // A roll of 0 to 999,999: picks among more than a thousand (Roll alone would favour the lower part of four hundred).
        private static int Wide(int seed, int a, int salt)
        {
            return FlareRules.Roll(seed, a, salt) * 1000 + FlareRules.Roll(seed, a, salt + 50);
        }

        /// <summary>Which of the city's people writes a day's k-th post of theirs (CityDay's own pick): their number, so that their face can be drawn ahead of the day.</summary>
        public static int TalkCitizen(int seed, int day, int k)
        {
            return Wide(seed, day * 31 + k, 501) % Citizens;
        }

        /// <summary>One of the city's people by a roll, evenly among all of them: their number, 0 to Citizens - 1.</summary>
        public static int PickCitizen(int seed, int a, int salt)
        {
            return Wide(seed, a, salt) % Citizens;
        }

        // ---------------------------------------------------------------- the day's posts

        /// <summary>
        /// The city's posts of a day, once a day: its people's, the official accounts', the businesses' and the
        /// invitations, each at its own minute of the day (shown from then on). Nothing for a guest that follows the host's
        /// feed, and nothing if the day was made already. The reports and reviews of the city's people among them are
        /// returned in <paramref name="made"/> as well, for the director to file and to apply. Returns the posts made.
        /// </summary>
        public static int CityDay(FlareFile f, FlareCityFacts facts, IReadOnlyList<FlareCitizen> citizens, List<FlarePost> made)
        {
            made?.Clear();
            if (facts == null || facts.Following || f.Net.CityDay == facts.Day)
            {
                return 0;
            }
            f.Net.CityDay = facts.Day;
            int seed = CitySeed(f);
            int busy = FlareRules.Clamp(facts.Busy, 0, 2);
            int day = facts.Day, count = 0;
            int talk = CityTalkPerDay[busy];
            for (int k = 0; citizens != null && citizens.Count > 0 && k < talk; k++)
            {
                FlarePost p = Talk(f, facts, citizens, seed, k);
                if (p != null)
                {
                    f.Posts.Add(p);
                    made?.Add(p);
                    count++;
                }
            }
            count += Officials(f, facts, seed, OfficialMost[busy], made);
            int shops = BusinessPerDay[busy];
            for (int k = 0; k < shops && facts.Shops.Count > 0; k++)
            {
                FlareBiz b = facts.Shops[FlareRules.Roll(seed, day * 13 + k, 520) % facts.Shops.Count];
                if (b.Mine)
                {
                    continue;
                }
                FlarePost p = New(f, day, seed, 700 + k, 9, 21, FlareAuthor.Business, -1, FlareSource.BusinessPost);
                p.Arg = FlareRules.Roll(seed, day * 17 + k, 521) % 5;
                p.Place = b.Place;
                p.Name = b.Name;
                p.District = b.District;
                p.Topic = NicheTag(b.Niche);
                f.Posts.Add(p);
                count++;
            }
            FlareRules.Prune(f);
            return count;
        }

        // A post and its time: the hour between 'from' and 'to', a minute, its figures by roll.
        private static FlarePost New(FlareFile f, int day, int seed, int slot, int from, int to, FlareAuthor author, int who, FlareSource source)
        {
            int id = f.NextId++;
            int span = Math.Max(1, to - from + 1);
            int reach = 60 + FlareRules.Roll(seed, day * 977 + slot, 531) * 3;
            return new FlarePost
            {
                Id = id, Day = day, Hour = from + FlareRules.Roll(seed, day * 977 + slot, 530) % span, Minute = FlareRules.Roll(seed, day * 977 + slot, 532) % 60,
                Author = author, Who = who, Source = source, Seed = FlareRules.Roll(seed, id, 17) * 1000 + FlareRules.Roll(seed, id, 29),
                Reach = reach, Likes = reach / 8 + FlareRules.Roll(seed, id, 533) % 12, Replies = FlareRules.Roll(seed, id, 534) % 6,
                Reposts = FlareRules.Roll(seed, id, 535) % 4
            };
        }

        // One of the city's people's posts: who, by a roll; what about, by what they care about and what the city had today.
        private static FlarePost Talk(FlareFile f, FlareCityFacts facts, IReadOnlyList<FlareCitizen> citizens, int seed, int k)
        {
            int day = facts.Day;
            FlareCitizen c = citizens[Wide(seed, day * 31 + k, 501) % citizens.Count];
            int roll = FlareRules.Roll(seed, day * 37 + k, 502);
            // A fifth are reports or reviews: what the city's people tell the City Office, and what they think of a business.
            if (roll < 120)
            {
                FlareReport kind;
                string district;
                if (CitizenReport(facts, seed, day, k, c, out kind, out district))
                {
                    FlarePost r = New(f, day, seed, k, 7, 22, FlareAuthor.Citizen, c.Index, FlareSource.Report);
                    r.Arg = (int)kind;
                    r.District = district;
                    r.Topic = ReportTag(kind);
                    return r;
                }
            }
            else if (roll < 200 && (facts.Shops.Count > 0 || facts.Mine.Count > 0))
            {
                bool mine = facts.Mine.Count > 0 && FlareRules.Roll(seed, day * 41 + k, 503) < 450;
                List<FlareBiz> from = mine ? facts.Mine : facts.Shops.Count > 0 ? facts.Shops : facts.Mine;
                FlareBiz b = from[FlareRules.Roll(seed, day * 43 + k, 504) % from.Count];
                FlarePost r = New(f, day, seed, k, 9, 22, FlareAuthor.Citizen, c.Index, FlareSource.Review);
                r.Arg = Stars(b.Stars, FlareRules.Roll(seed, day * 47 + k, 505));
                r.Place = b.Place;
                r.Name = b.Name;
                r.District = b.District;
                r.Topic = NicheTag(b.Niche);
                return r;
            }
            FlareTalk topic = TopicOf(c.Persona, facts, FlareRules.Roll(seed, day * 53 + k, 506));
            FlarePost p = New(f, day, seed, k, 7, 23, FlareAuthor.Citizen, c.Index, FlareSource.CityTalk);
            p.Arg = (int)topic * 10 + FlareRules.Roll(seed, day * 59 + k, 507) % 3;
            p.District = c.District;
            switch (topic)
            {
                case FlareTalk.Weather: p.Topic = facts.Rain ? "rain" : "sunshine"; break;
                case FlareTalk.Prices: p.Topic = "prices"; break;
                case FlareTalk.Crime: p.District = facts.NewsDistrict ?? c.District; p.Topic = "safety"; break;
                case FlareTalk.Accident: p.District = facts.NewsDistrict ?? c.District; p.Topic = "traffic"; break;
                case FlareTalk.Event: p.Name = facts.EventToday; p.Topic = "events"; break;
                case FlareTalk.EventTomorrow: p.Name = facts.EventTomorrow; p.Topic = "events"; break;
                case FlareTalk.Election: p.Name = facts.Candidates.Count > 0 ? facts.Candidates[FlareRules.Roll(seed, day * 61 + k, 508) % facts.Candidates.Count] : null; p.Topic = "election"; break;
                case FlareTalk.Roadworks: p.District = facts.SiteDistrict ?? c.District; p.Topic = "roadworks"; break;
                case FlareTalk.Market: p.Name = facts.Mover; p.Arg = (int)topic * 10 + (facts.MoverBp >= 0 ? 0 : 1); p.Topic = "stocks"; break;
                case FlareTalk.Commute: p.Topic = "commute"; break;
                case FlareTalk.Policy: p.Name = facts.Policy; p.Topic = "council"; break;
                case FlareTalk.Fire: p.District = facts.NewsDistrict ?? c.District; p.Topic = "fire"; break;
                case FlareTalk.Night: p.Topic = "nightlife"; break;
                case FlareTalk.Food: p.Topic = "food"; break;
                case FlareTalk.Sport: p.Topic = "fitness"; break;
                default: p.Topic = "city"; break;
            }
            return p;
        }

        /// <summary>A topic for one of the city's people: what they care about, when the city has news of it today; something everyday otherwise.</summary>
        public static FlareTalk TopicOf(FlarePersona persona, FlareCityFacts facts, int roll)
        {
            // What the day has: each persona's first choice, then the city's own news for anybody.
            bool crime = (facts.News & (FlareCast.NewsCrime | FlareCast.NewsBreakIn | FlareCast.NewsArrest)) != 0;
            bool accident = (facts.News & FlareCast.NewsAccident) != 0;
            bool fire = (facts.News & FlareCast.NewsFire) != 0;
            if (roll < 160 && (facts.Rain || facts.Rained))
            {
                return FlareTalk.Weather;
            }
            if (roll < 260 && fire)
            {
                return FlareTalk.Fire;
            }
            if (roll < 340 && crime && persona != FlarePersona.Student)
            {
                return FlareTalk.Crime;
            }
            if (roll < 400 && accident)
            {
                return FlareTalk.Accident;
            }
            if (roll < 470 && facts.EventToday != null)
            {
                return FlareTalk.Event;
            }
            if (roll < 520 && facts.EventTomorrow != null)
            {
                return FlareTalk.EventTomorrow;
            }
            if (roll < 590 && facts.Election && facts.Candidates.Count > 0)
            {
                return FlareTalk.Election;
            }
            if (roll < 640 && facts.Policy != null)
            {
                return FlareTalk.Policy;
            }
            if (roll < 700 && facts.PriceTrend != 0 && (persona == FlarePersona.Parent || persona == FlarePersona.Retiree || persona == FlarePersona.Grumpy))
            {
                return FlareTalk.Prices;
            }
            if (roll < 740 && facts.Mover != null && persona != FlarePersona.Retiree)
            {
                return FlareTalk.Market;
            }
            if (roll < 790 && facts.LateSites > 0)
            {
                return FlareTalk.Roadworks;
            }
            switch (persona)
            {
                case FlarePersona.Commuter: return FlareTalk.Commute;
                case FlarePersona.Foodie: return FlareTalk.Food;
                case FlarePersona.Sporty: return FlareTalk.Sport;
                case FlarePersona.NightOwl: return FlareTalk.Night;
                case FlarePersona.Grumpy: return facts.PriceTrend > 0 ? FlareTalk.Prices : FlareTalk.Commute;
                default: return roll % 2 == 0 ? FlareTalk.Everyday : FlareTalk.Weather;
            }
        }

        // What one of the city's people reports: what the day had, in its district; a lamp or a road otherwise. False for nothing to report.
        private static bool CitizenReport(FlareCityFacts facts, int seed, int day, int k, FlareCitizen c, out FlareReport kind, out string district)
        {
            int roll = FlareRules.Roll(seed, day * 67 + k, 509);
            district = c.District;
            if (facts.RealFaults)
            {
                // The city's faults are real: its people report those as they see them (Scripts/Faults); here only the buses in the rain.
                kind = FlareReport.Transit;
                return facts.Rain && roll < 500 && district != null;
            }
            if ((facts.News & (FlareCast.NewsCrime | FlareCast.NewsBreakIn)) != 0 && roll < 350)
            {
                kind = FlareReport.Crime;
                district = facts.NewsDistrict ?? c.District;
            }
            else if ((facts.Rain || facts.Rained || facts.LateSites > 0) && roll < 600)
            {
                kind = FlareReport.Road;
                district = facts.SiteDistrict ?? c.District;
            }
            else if (facts.LitterPolicy && roll < 750)
            {
                kind = FlareReport.Litter;
            }
            else if (facts.Rain && roll < 850)
            {
                kind = FlareReport.Transit;
            }
            else
            {
                kind = roll % 3 == 0 ? FlareReport.Lamp : roll % 3 == 1 ? FlareReport.Litter : FlareReport.Road;
            }
            return district != null;
        }

        /// <summary>A review's stars from the game's satisfaction (0 to 100) and a roll: one star either side of it now and then; three for one not known.</summary>
        public static int Stars(int satisfaction, int roll)
        {
            int stars = satisfaction < 0 ? 3 : FlareRules.Clamp((satisfaction + 10) / 20, 1, 5);
            stars += roll < 150 ? -1 : roll > 850 ? 1 : 0;
            return FlareRules.Clamp(stars, 1, 5);
        }

        // The official accounts' posts of the day: each when the city has a reason for it, at most so many.
        private static int Officials(FlareFile f, FlareCityFacts facts, int seed, int most, List<FlarePost> made)
        {
            int day = facts.Day, count = 0;
            if (facts.EventTomorrow != null && facts.EventTomorrowFrom >= 0 && count < most)
            {
                FlarePost p = New(f, day, seed, 900, 17, 18, FlareAuthor.Official, (int)FlareOfficial.CityOffice, FlareSource.Invite);
                p.Name = facts.EventTomorrow;
                p.When = (day + 1) * 24 + facts.EventTomorrowFrom;
                p.Topic = "events";
                p.Reach *= 4;
                f.Posts.Add(p);
                count++;
            }
            if (facts.EventToday != null && facts.EventTodayFrom >= 0 && count < most)
            {
                FlarePost p = New(f, day, seed, 901, 7, 8, FlareAuthor.Official, (int)FlareOfficial.CityOffice, FlareSource.Invite);
                p.Name = facts.EventToday;
                p.When = day * 24 + facts.EventTodayFrom;
                p.Topic = "events";
                p.Reach *= 4;
                f.Posts.Add(p);
                count++;
            }
            count += Official(f, day, seed, 902, most - count, (facts.News & FlareCast.NewsCrime) != 0, FlareOfficial.Police, FlareOfficialPost.Patrols, facts.NewsDistrict, "safety");
            count += Official(f, day, seed, 903, most - count, (facts.News & FlareCast.NewsBreakIn) != 0, FlareOfficial.Police, FlareOfficialPost.Advice, facts.NewsDistrict, "safety");
            count += Official(f, day, seed, 904, most - count, facts.Rain, FlareOfficial.Transit, FlareOfficialPost.Service, null, "commute");
            count += Official(f, day, seed, 905, most - count, facts.RateMove != 0, FlareOfficial.Bank, FlareOfficialPost.Rate, null, "rates", facts.RateMove > 0 ? 1 : 0);
            count += Official(f, day, seed, 906, most - count, facts.Policy != null, FlareOfficial.CityOffice, FlareOfficialPost.Policy, null, "council", 0, facts.Policy);
            count += Official(f, day, seed, 907, most - count, facts.LateSites > 0 || FlareRules.Roll(seed, day, 910) < 300, FlareOfficial.CityOffice, FlareOfficialPost.Notice, facts.SiteDistrict, "roadworks");
            count += Official(f, day, seed, 908, most - count, facts.Election && FlareRules.Roll(seed, day, 911) < 350, FlareOfficial.CityOffice, FlareOfficialPost.Election, null, "election");
            count += Official(f, day, seed, 909, most - count, FlareRules.Roll(seed, day, 912) < 150, FlareOfficial.Utilities, FlareOfficialPost.Outage, facts.Districts.Count > 0 ? facts.Districts[FlareRules.Roll(seed, day, 913) % facts.Districts.Count] : null, "utilities");
            for (int i = 0; i < f.Net.Watches.Count && count < most; i++)
            {
                FlareWatch w = f.Net.Watches[i];
                if (w.Kind == FlareOrder.Cleanup && day < w.Until)
                {
                    count += Official(f, day, seed, 920 + i, most - count, true, FlareOfficial.CityOffice, FlareOfficialPost.Clean, w.District, "clean");
                }
            }
            return count;
        }

        private static int Official(FlareFile f, int day, int seed, int slot, int room, bool reason, FlareOfficial who, FlareOfficialPost what, string district, string tag, int variant = 0, string name = null)
        {
            if (!reason || room <= 0)
            {
                return 0;
            }
            FlarePost p = New(f, day, seed, slot, 8, 19, FlareAuthor.Official, (int)who, FlareSource.Official);
            p.Arg = (int)what * 10 + variant;
            p.District = district;
            p.Name = name;
            p.Topic = tag;
            p.Reach *= 5;
            p.Likes *= 2;
            f.Posts.Add(p);
            return 1;
        }

        public static string NicheTag(FlareNiche niche)
        {
            switch (niche)
            {
                case FlareNiche.Food: return "food";
                case FlareNiche.Fashion: return "style";
                case FlareNiche.Fitness: return "fitness";
                case FlareNiche.Nightlife: return "nightlife";
                case FlareNiche.Tech: return "tech";
                default: return "shoplocal";
            }
        }

        public static string ReportTag(FlareReport kind)
        {
            switch (kind)
            {
                case FlareReport.Road: return "potholes";
                case FlareReport.Lamp: return "streetlights";
                case FlareReport.Crime: return "safety";
                case FlareReport.Litter: return "cleanstreets";
                case FlareReport.Unsafe: return "safety";
                case FlareReport.Business: return "complaint";
                case FlareReport.Transit: return "buses";
                default: return "utilities";
            }
        }

        // ---------------------------------------------------------------- the reactions over the hours

        /// <summary>
        /// Who with a name reacts to a post of the player's, and when: the friends who liked or replied (the post's own
        /// rules, FlareRules.Reaction), the partner first, some of the staff, the boss if they follow, and a few of the city's
        /// people by how far it went. Written as one short line kept on the post ("L:f3:12;R:c7:95;..."): the
        /// notifications come in as the minutes pass, from that line alone.
        /// </summary>
        public static string Reactors(FlarePost p, IReadOnlyList<FlarePerson> people, IReadOnlyList<string> staff, bool boss, StringBuilder b)
        {
            b.Length = 0;
            for (int i = 0; people != null && i < people.Count; i++)
            {
                FlarePerson person = people[i];
                if (person.Index < 0 || person.Index > 30)
                {
                    continue;
                }
                int bit = 1 << person.Index;
                bool liked = (p.Likers & bit) != 0, replied = (p.Repliers & bit) != 0;
                if (!liked && !replied)
                {
                    continue;
                }
                string who = (person.Partner ? "p" : "f") + person.Index;
                int delay = person.Partner ? 3 + FlareRules.Roll(p.Seed, person.Index, 801) % 30 : 5 + FlareRules.Roll(p.Seed, person.Index, 802) % 240;
                Add(b, replied ? 'R' : 'L', who, delay);
            }
            for (int i = 0; staff != null && i < staff.Count && i < 8; i++)
            {
                if (FlareRules.Roll(p.Seed, i, 803) < 350 && !p.Bad)
                {
                    Add(b, 'L', "s:" + Plain(staff[i]), 20 + FlareRules.Roll(p.Seed, i, 804) % 600);
                }
            }
            if (boss)
            {
                Add(b, 'L', "b", 60 + FlareRules.Roll(p.Seed, 1, 805) % 240);
            }
            int citizens = FlareRules.Clamp(1 + p.Reach / 300, 1, CitizensReactMost);
            for (int i = 0; i < citizens; i++)
            {
                int c = PickCitizen(p.Seed, i, 806);
                char kind = p.Reach > 1000 && FlareRules.Roll(p.Seed, i, 807) < 250 ? 'S' : 'L';
                Add(b, kind, "c" + c, 2 + FlareRules.Roll(p.Seed, i, 808) % 420);
            }
            return b.ToString();
        }

        private static void Add(StringBuilder b, char kind, string who, int delay)
        {
            if (b.Length > 0)
            {
                b.Append(';');
            }
            b.Append(kind).Append(':').Append(who).Append(':').Append(delay);
        }

        // A name as the reactions' line can carry it: none of its separators in it.
        private static string Plain(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "?";
            }
            var b = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length && b.Length < 32; i++)
            {
                char c = name[i];
                b.Append(c == ';' || c == ':' || c == '|' || c == '\n' || c == '\r' || c == '~' ? ' ' : c);
            }
            return b.ToString();
        }

        /// <summary>The reactions of a post's line, into the list given (filled again). Nothing for none, or for a line that does not read.</summary>
        public static void ReadReactors(string line, List<FlareReaction> into)
        {
            into.Clear();
            if (string.IsNullOrEmpty(line))
            {
                return;
            }
            int start = 0;
            while (start < line.Length)
            {
                int end = line.IndexOf(';', start);
                if (end < 0)
                {
                    end = line.Length;
                }
                int a = line.IndexOf(':', start);
                int z = line.LastIndexOf(':', end - 1);
                if (a > start && z > a && z < end)
                {
                    int delay;
                    if (int.TryParse(line.Substring(z + 1, end - z - 1), out delay))
                    {
                        into.Add(new FlareReaction { Kind = line[start], Who = line.Substring(a + 1, z - a - 1), Delay = Math.Max(0, delay) });
                    }
                }
                start = end + 1;
            }
        }

        /// <summary>How much of a post's figures have come in by now, thousandths: on a curve, all of it a day after.</summary>
        public static int Arrived(FlarePost p, long now) => ArrivedAfter(now - p.At);

        /// <summary>How much of a post's figures have come in so many game minutes after it went up, thousandths.</summary>
        public static int ArrivedAfter(long since)
        {
            if (since <= 0)
            {
                return 0;
            }
            if (since >= 1440)
            {
                return 1000;
            }
            return (int)Math.Round(1000.0 * (1.0 - Math.Exp(-since / (double)CurveMinutes)) / (1.0 - Math.Exp(-1440.0 / CurveMinutes)));
        }

        public static int Shown(int total, FlarePost p, long now)
        {
            return (int)((long)Math.Max(0, total) * Arrived(p, now) / 1000);
        }

        /// <summary>
        /// The city's people's replies to a post of the player's, each a post of its own at its minute: how many by how far
        /// it went, and how they read by what it was and how it went (tone matters: the post's own rules decided Bad and
        /// Viral). A post that went badly and was seen by enough draws a pile-on. Returns how many were made.
        /// </summary>
        public static int CityReplies(FlareFile f, FlarePost p, List<FlarePost> made)
        {
            int count = p.Bad && p.Reach >= PileOnFrom ? FlareRules.Clamp(PileOnLeast + p.Reach / 2000, PileOnLeast, PileOnMost)
                : FlareRules.Clamp(p.Reach / 500 + (p.Viral ? 3 : 0), 0, 5);
            for (int i = 0; i < count; i++)
            {
                int id = f.NextId++;
                long at = p.At + 4 + FlareRules.Roll(p.Seed, i, 820) % (p.Bad ? 180 : 360);
                var r = new FlarePost
                {
                    Id = id, Author = FlareAuthor.Citizen, Who = PickCitizen(p.Seed, i, 821), Source = FlareSource.Reply, ReplyTo = p.Id,
                    Arg = (int)ReplyKindOf(p, FlareRules.Roll(p.Seed, i, 822)), Seed = FlareRules.Roll(p.Seed, id, 17) * 1000 + FlareRules.Roll(p.Seed, id, 29),
                    Likes = FlareRules.Roll(p.Seed, i, 823) % (p.Bad ? 40 : 12), Reach = 30
                };
                SetAt(r, at);
                f.Posts.Add(r);
                made?.Add(r);
            }
            return count;
        }

        /// <summary>How one of the city's people answers a post: angry at one that went badly, kind about an honest arrest, laughing at a cheeky one, doubting a boast about money.</summary>
        public static FlareReplyKind ReplyKindOf(FlarePost p, int roll)
        {
            if (p.Bad)
            {
                return roll < 800 ? FlareReplyKind.Angry : FlareReplyKind.Doubt;
            }
            if (p.Source == FlareSource.Arrested || p.Source == FlareSource.Scandal)
            {
                return p.Tone == FlareTone.Honest ? FlareReplyKind.Kind : FlareReplyKind.Doubt;
            }
            if (p.Source == FlareSource.Report)
            {
                return roll < 600 ? FlareReplyKind.Agree : FlareReplyKind.Question;
            }
            if (p.Tone == FlareTone.Cheeky)
            {
                return roll < 700 ? FlareReplyKind.Funny : FlareReplyKind.Love;
            }
            bool money = p.Source == FlareSource.Promotion || p.Source == FlareSource.Opened || p.Source == FlareSource.RecordDay || p.Source == FlareSource.Shares;
            if (money && p.Tone == FlareTone.Proud && roll < 300)
            {
                return FlareReplyKind.Doubt;
            }
            return p.Viral || roll < 350 ? FlareReplyKind.Love : roll < 700 ? FlareReplyKind.Agree : FlareReplyKind.Question;
        }

        /// <summary>A post that drew a pile-on: it went badly, it was seen widely, and the angry replies came.</summary>
        public static bool PileOn(FlarePost p)
        {
            return p != null && p.Bad && p.Reach >= PileOnFrom && p.Author == FlareAuthor.Player;
        }

        /// <summary>Puts a post at a minute of the game's clock.</summary>
        public static void SetAt(FlarePost p, long at)
        {
            at = Math.Max(0, at);
            p.Minute = (int)(at % 60);
            p.Hour = (int)(at / 60 % 24);
            p.Day = (int)(at / 1440);
        }

        // ---------------------------------------------------------------- the City Office

        /// <summary>
        /// A report reaches the City Office. The same kind in the same district (for a business, the same business) while
        /// an earlier case is open or being acted on is that case again ("already on our list"): its tally of repeats grows
        /// and it is returned with <paramref name="again"/>. Otherwise a new case, its answer due within hours: the more
        /// cases open, the longer.
        /// </summary>
        public static FlareCase File(FlareNet net, FlareReport kind, string district, string place, string name, float x, float z, long now, string reporter, int post, int seed, out bool again,
            bool counted = true)
        {
            again = false;
            FlareCase same = OpenCase(net, kind, district, place);
            if (same != null)
            {
                same.Repeats++;
                same.Counted |= counted;
                again = true;
                return same;
            }
            int open = 0;
            for (int i = 0; i < net.Cases.Count; i++)
            {
                open += net.Cases[i].State == FlareCaseState.Open ? 1 : 0;
            }
            var made = new FlareCase
            {
                Id = net.NextCase++, Kind = kind, District = district, Place = place, Name = name, X = x, Z = z, Filed = now, Reporter = reporter, Post = post, Counted = counted,
                ReplyAt = now + Math.Min(ReplyMost, ReplyLeast + ReplyPerOpen * open + FlareRules.Roll(seed, net.NextCase, 830) % 60)
            };
            net.Cases.Add(made);
            while (net.Cases.Count > FlareNet.CasesMost)
            {
                net.Cases.RemoveAt(Evicted(net, reporter));
            }
            return made;
        }

        /// <summary>
        /// The case that gives way when there are too many: the oldest closed one, else the oldest open one that is not
        /// another player's (the city's people's, this machine's player's, or the new report's own reporter's). Another
        /// player's open case is never let go for somebody else's; the newest (the one just filed) always qualifies.
        /// </summary>
        public static int Evicted(FlareNet net, string reporter)
        {
            int closed = OldestClosed(net);
            if (closed >= 0)
            {
                return closed;
            }
            for (int i = 0; i < net.Cases.Count; i++)
            {
                string r = net.Cases[i].Reporter;
                if (r == null || !r.StartsWith("p:", StringComparison.Ordinal) || string.Equals(r, reporter, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return net.Cases.Count - 1;
        }

        /// <summary>
        /// Whether a player's report today is one of their first three of the day (counted when it is): those move the feed's
        /// mood, the week's tally and the paper; more are filed and answered and move none of them. 'who' as MoveKey has it;
        /// null (the city's people) is always counted.
        /// </summary>
        public static bool Counted(FlareNet net, int day, string who)
        {
            if (who == null)
            {
                return true;
            }
            for (int n = 0; n < ReportsWeighed; n++)
            {
                string key = who + "|W|" + n;
                if (!MovedToday(net, day, key))
                {
                    return FirstToday(net, day, key);
                }
            }
            return false;
        }

        /// <summary>A key for what a post does to a friend (tagged) or to the partner (seeing one tagged): once a day each (FirstToday).</summary>
        public static string FriendKey(char what, int person)
        {
            return "me|" + what + "|" + person;
        }

        /// <summary>A key for the talk a post of the player's brings a business: once a day (FirstToday).</summary>
        public static string TalkKey(string place)
        {
            return place != null ? "me|B|" + place : null;
        }

        /// <summary>The case still open or being acted on for a thing (a kind in a district; a business by its place), or null.</summary>
        public static FlareCase OpenCase(FlareNet net, FlareReport kind, string district, string place)
        {
            for (int i = 0; i < net.Cases.Count; i++)
            {
                FlareCase c = net.Cases[i];
                if (c.Kind == kind && (c.State == FlareCaseState.Open || c.State == FlareCaseState.Acting) && string.Equals(c.District, district, StringComparison.Ordinal)
                    && (kind != FlareReport.Business || string.Equals(c.Place, place, StringComparison.Ordinal)))
                {
                    return c;
                }
            }
            return null;
        }

        private static int OldestClosed(FlareNet net)
        {
            for (int i = 0; i < net.Cases.Count; i++)
            {
                if (net.Cases[i].State == FlareCaseState.Done || net.Cases[i].State == FlareCaseState.Queued)
                {
                    return i;
                }
            }
            return -1;
        }

        public const int SoonerMinutes = 40;       // the city's people report the same real fault again: its answer comes so much sooner...
        public const int SoonestMinutes = 20;      // ...but never sooner than this after now

        /// <summary>An open case reported again by the city's people while the faults are real: its answer comes sooner, never sooner than a little after now.</summary>
        public static long Sooner(long replyAt, long now)
        {
            return Math.Max(now + SoonestMinutes, replyAt - SoonerMinutes);
        }

        /// <summary>True for a verdict of the city's faults that found nothing (nothing, a warning, no crews for now).</summary>
        public static bool FoundNothing(int verdict)
        {
            return verdict >= (int)FaultAnswer.Nothing && verdict <= (int)FaultAnswer.Barred;
        }

        public const int FalseReportCost = 2;      // points of the player's reputation on Flare a report of nothing costs (the city's faults: Scripts/Faults)

        /// <summary>The player's reputation after a report of nothing: <see cref="FalseReportCost"/> less, never below 0.</summary>
        public static int FalseReport(int reputation)
        {
            return FlareRules.Clamp(reputation - FalseReportCost, 0, 100);
        }

        /// <summary>The cases whose answer is due by now, into the list given (oldest first).</summary>
        public static void Due(FlareNet net, long now, List<FlareCase> into)
        {
            into.Clear();
            for (int i = 0; i < net.Cases.Count; i++)
            {
                if (net.Cases[i].State == FlareCaseState.Open && net.Cases[i].ReplyAt <= now)
                {
                    into.Add(net.Cases[i]);
                }
            }
        }

        /// <summary>The orders of a kind given in the week up to now: the crews it took.</summary>
        public static int CrewsUsed(FlareNet net, FlareOrder order, long now)
        {
            int n = 0;
            long from = now - WeekDays * 1440L;
            for (int i = 0; i < net.Cases.Count; i++)
            {
                FlareCase c = net.Cases[i];
                long sent = c.ActedDay >= 0 ? c.ActedDay * 1440L : c.Filed;   // (a case of an older save: when it was filed)
                n += c.Order == order && sent >= from && c.State != FlareCaseState.Open && c.State != FlareCaseState.Queued ? 1 : 0;
            }
            return n;
        }

        /// <summary>
        /// What the City Office does about a case that is due: the order for its kind if a crew is free this week (works for
        /// a road or a lamp, patrols for crime, a cleaning round for litter, an inspector for a building site or a business),
        /// an answer and nothing more for the buses and the power, "not now" when the week's crews are taken. With the
        /// option to act off, an answer only. The answer's kind comes back with it.
        /// </summary>
        public static FlareOrder Decide(FlareNet net, FlareCase c, long now, bool acts, bool siteNear, out FlareAnswer answer)
        {
            return Decide(net, c, now, acts, siteNear, false, out answer);
        }

        /// <summary>
        /// The same, for a report the city's faults found true (<paramref name="realFault"/>): an unsafe place that is real
        /// gets a works crew to make it safe, as a road or a lamp does.
        /// </summary>
        public static FlareOrder Decide(FlareNet net, FlareCase c, long now, bool acts, bool siteNear, bool realFault, out FlareAnswer answer)
        {
            answer = FlareAnswer.Received;
            if (!acts)
            {
                return FlareOrder.Answered;
            }
            FlareOrder order;
            switch (c.Kind)
            {
                case FlareReport.Transit: answer = FlareAnswer.Transit; return FlareOrder.Answered;
                case FlareReport.Utilities: answer = FlareAnswer.Utilities; return FlareOrder.Answered;
                case FlareReport.Road:
                case FlareReport.Lamp: order = FlareOrder.Works; answer = FlareAnswer.Works; break;
                case FlareReport.Crime: order = FlareOrder.Patrol; answer = FlareAnswer.Patrol; break;
                case FlareReport.Litter: order = FlareOrder.Cleanup; answer = FlareAnswer.Cleanup; break;
                case FlareReport.Business: order = FlareOrder.Inspection; answer = FlareAnswer.Inspect; break;
                case FlareReport.Unsafe when realFault: order = FlareOrder.Works; answer = FlareAnswer.Works; break;
                default:
                    order = siteNear ? FlareOrder.SiteCheck : FlareOrder.Patrol;
                    answer = siteNear ? FlareAnswer.Site : FlareAnswer.Patrol;
                    break;
            }
            if (CrewsUsed(net, order, now) >= CrewsPerWeek[(int)order])
            {
                answer = FlareAnswer.Busy;
                return FlareOrder.Queued;
            }
            return order;
        }

        /// <summary>A case acted on (or not): its state, its order and until when, and the watch it puts on its district.</summary>
        public static void Act(FlareNet net, FlareCase c, FlareOrder order, int day, int siteRef)
        {
            c.Order = order;
            c.Ref = siteRef;
            if (order != FlareOrder.Queued && order != FlareOrder.Answered && order != FlareOrder.None)
            {
                c.ActedDay = day;   // a crew sent today: one of the week's
            }
            switch (order)
            {
                case FlareOrder.Patrol:
                    c.Until = day + PatrolDays;
                    Watch(net, c.District, FlareOrder.Patrol, c.Until);
                    c.State = FlareCaseState.Acting;
                    break;
                case FlareOrder.Cleanup:
                    c.Until = day + CleanupDays;
                    Watch(net, c.District, FlareOrder.Cleanup, c.Until);
                    c.State = FlareCaseState.Acting;
                    break;
                case FlareOrder.Inspection:
                    c.Until = day + InspectionDays;
                    c.State = FlareCaseState.Acting;
                    break;
                case FlareOrder.Works:
                    c.State = FlareCaseState.Acting;
                    break;
                case FlareOrder.Queued:
                    c.State = FlareCaseState.Queued;
                    break;
                default:
                    c.State = FlareCaseState.Done;
                    break;
            }
        }

        /// <summary>A watch on a district until a day: the one there already is made longer, never shorter.</summary>
        public static void Watch(FlareNet net, string district, FlareOrder kind, int until)
        {
            if (district == null)
            {
                return;
            }
            for (int i = 0; i < net.Watches.Count; i++)
            {
                FlareWatch w = net.Watches[i];
                if (w.Kind == kind && string.Equals(w.District, district, StringComparison.Ordinal))
                {
                    w.Until = Math.Max(w.Until, until);
                    return;
                }
            }
            net.Watches.Add(new FlareWatch { District = district, Kind = kind, Until = until });
        }

        /// <summary>The day's turn: watches that ran out go, and cases whose days are over are done. True if anything changed.</summary>
        public static bool WatchDay(FlareNet net, int day)
        {
            bool changed = false;
            for (int i = net.Watches.Count - 1; i >= 0; i--)
            {
                if (day >= net.Watches[i].Until)
                {
                    net.Watches.RemoveAt(i);
                    changed = true;
                }
            }
            for (int i = 0; i < net.Cases.Count; i++)
            {
                FlareCase c = net.Cases[i];
                if (c.State == FlareCaseState.Acting && c.Order != FlareOrder.Works && c.Until >= 0 && day >= c.Until)
                {
                    c.State = FlareCaseState.Done;
                    changed = true;
                }
            }
            for (int i = net.Complaints.Count - 1; i >= 0; i--)
            {
                if (day - net.Complaints[i].Day >= WeekDays)
                {
                    net.Complaints.RemoveAt(i);
                }
            }
            for (int i = net.Escalated.Count - 1; i >= 0; i--)
            {
                string[] f = net.Escalated[i].Split('|');
                int when;
                if (f.Length < 3 || !int.TryParse(f[2], out when) || day - when >= WeekDays)
                {
                    net.Escalated.RemoveAt(i);
                }
            }
            return changed;
        }

        /// <summary>A complaint noted for the week's tally: whose does not matter, the player's, the city's people's or another player's.</summary>
        public static void Complained(FlareNet net, FlareReport kind, string district, int day, bool acted)
        {
            if (district == null)
            {
                return;
            }
            net.Complaints.Add(new FlareComplaint { Kind = kind, District = district, Day = day, Acted = acted });
            while (net.Complaints.Count > FlareNet.ComplaintsMost)
            {
                net.Complaints.RemoveAt(0);
            }
        }

        /// <summary>A complaint acted on after all: the week's complaints of its kind there are acted on too.</summary>
        public static void Answered(FlareNet net, FlareReport kind, string district)
        {
            for (int i = 0; i < net.Complaints.Count; i++)
            {
                FlareComplaint c = net.Complaints[i];
                if (c.Kind == kind && string.Equals(c.District, district, StringComparison.Ordinal))
                {
                    c.Acted = true;
                }
            }
        }

        /// <summary>Complaints of a kind in a district this week that nothing was done about.</summary>
        public static int Tally(FlareNet net, FlareReport kind, string district, int day)
        {
            int n = 0;
            for (int i = 0; i < net.Complaints.Count; i++)
            {
                FlareComplaint c = net.Complaints[i];
                n += c.Kind == kind && !c.Acted && day - c.Day < WeekDays && string.Equals(c.District, district, StringComparison.Ordinal) ? 1 : 0;
            }
            return n;
        }

        /// <summary>
        /// Whether the week's ignored complaints of a kind in a district are a story for the paper now: three or more, and
        /// not in the paper already this week. True takes it (it is noted).
        /// </summary>
        public static bool Escalate(FlareNet net, FlareReport kind, string district, int day, bool acts)
        {
            if (!Escalates(kind, acts) || district == null || Tally(net, kind, district, day) < TallyToPaper)
            {
                return false;
            }
            string prefix = (int)kind + "|" + district + "|";
            for (int i = 0; i < net.Escalated.Count; i++)
            {
                if (net.Escalated[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            net.Escalated.Add(prefix + day);
            return true;
        }

        public const int TycoonVoices = 3, TycoonLines = 3, TycoonMoves = 8;   // the tycoons with a voice of their own, their everyday lines, the kinds of move they post about
        public const int TycoonMoveArg = 10;                                     // a post's Arg from here on: about a move of that kind (Arg - 10)

        /// <summary>
        /// A rival tycoon's post of the day, now and then (about a third of days), in their own voice: the move they have just
        /// announced if there is one (it is news, and they brag), else one of their everyday lines. Who is the tycoon's place
        /// in the catalogue (their voice), Name their name. Null on a day without one. The same city and day make the same.
        /// </summary>
        public static FlarePost TycoonPost(FlareFile f, IList<FlareTycoon> tycoons, int day)
        {
            int seed = CitySeed(f);
            if (tycoons == null || tycoons.Count == 0 || FlareRules.Roll(seed, day, 950) >= 350)
            {
                return null;
            }
            FlareTycoon t = null;
            for (int i = 0; i < tycoons.Count && t == null; i++)
            {
                t = tycoons[i] != null && tycoons[i].Move >= 0 && tycoons[i].Move < TycoonMoves ? tycoons[i] : null;   // the one with news first
            }
            t = t ?? tycoons[FlareRules.Roll(seed, day, 952) % tycoons.Count];
            if (t == null || string.IsNullOrEmpty(t.Name))
            {
                return null;
            }
            int id = f.NextId++;
            var p = new FlarePost
            {
                Id = id, Author = FlareAuthor.Tycoon, Source = FlareSource.BusinessPost, Who = FlareRules.Clamp(t.Index, 0, TycoonVoices - 1),
                Arg = t.Move >= 0 && t.Move < TycoonMoves ? TycoonMoveArg + t.Move : FlareRules.Roll(seed, day, 951) % TycoonLines,
                Name = t.Name, Seed = FlareRules.Roll(seed, id, 17) * 1000, Reach = 20000, Likes = 400 + FlareRules.Roll(seed, id, 3), Topic = "business"
            };
            SetAt(p, Now(day, 9 + FlareRules.Roll(seed, day, 953) % 10, FlareRules.Roll(seed, day, 954) % 60));
            return p;
        }

        /// <summary>
        /// Whether ignored complaints of a kind can reach the paper and pull on the mayor's approval: only while the City
        /// Office acts on reports (with the option off it only answers, so nothing is "ignored"), and never for the buses or
        /// the power, which the office answers and passes on (FlareOrder.Answered: not the city's to act on).
        /// </summary>
        public static bool Escalates(FlareReport kind, bool acts)
        {
            return acts && kind != FlareReport.Transit && kind != FlareReport.Utilities;
        }

        /// <summary>
        /// What the City Office does once ignored complaints are in the paper: the crew for the case's kind, if the case is
        /// not being acted on already and a crew of that kind is free this week (escalated or not, a crew is one of the
        /// week's); None otherwise.
        /// </summary>
        public static FlareOrder EscalationOrder(FlareNet net, FlareCase c, long now, bool acts)
        {
            if (c == null || !Escalates(c.Kind, acts) || c.State == FlareCaseState.Acting || FoundNothing(c.Verdict))
            {
                return FlareOrder.None;   // (a case the city's faults found nothing in is never acted on, whatever the paper says)
            }
            if (c.Kind == FlareReport.Business && c.Verdict == (int)FaultAnswer.Clean)
            {
                return FlareOrder.None;   // a business found clean is not inspected
            }
            FlareOrder order = c.Kind == FlareReport.Crime ? FlareOrder.Patrol : c.Kind == FlareReport.Litter ? FlareOrder.Cleanup : c.Kind == FlareReport.Business ? FlareOrder.Inspection
                : c.Kind == FlareReport.Unsafe ? (c.Verdict == (int)FaultAnswer.True ? FlareOrder.Works : FlareOrder.Patrol) : FlareOrder.Works;   // (a real unsafe place: a works crew, as when answered)
            return CrewsUsed(net, order, now) >= CrewsPerWeek[(int)order] ? FlareOrder.None : order;
        }

        /// <summary>Whether the city's posts of a day are still to be made when Flare starts (an update, a new account): not on a guest that follows.</summary>
        public static bool StartDay(FlareNet net, int day, bool following)
        {
            return !following && net.CityDay != day;
        }

        /// <summary>Whether a post of the player's goes to the session: every post while they share their posts, and a report always (it is the City Office's to act on).</summary>
        public static bool ToSession(bool sharePosts, FlareSource source)
        {
            return sharePosts || source == FlareSource.Report;
        }

        /// <summary>
        /// Whether the host answers a post of this player's (the city's replies, an account's answer): true on a guest that
        /// follows and sends it; it then does not answer it itself, or the replies would come twice.
        /// </summary>
        public static bool HostAnswers(bool following, bool sharePosts, FlareSource source)
        {
            return following && ToSession(sharePosts, source);
        }

        /// <summary>A story for a morning's paper, kept until it is printed (a few at most, the oldest go).</summary>
        public static void Story(FlareNet net, int day, string key, params string[] data)
        {
            net.Stories.Add(new FlareStory { Day = day, Key = key, Data = data ?? new string[0] });
            while (net.Stories.Count > FlareNet.StoriesMost)
            {
                net.Stories.RemoveAt(0);
            }
        }

        /// <summary>
        /// A story for a morning's paper, once for its day and key: false (nothing kept) when that morning has it already.
        /// A pile-on is in the paper once, however many posts went badly the day before.
        /// </summary>
        public static bool StoryOnce(FlareNet net, int day, string key, params string[] data)
        {
            for (int i = 0; i < net.Stories.Count; i++)
            {
                if (net.Stories[i].Day == day && string.Equals(net.Stories[i].Key, key, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            Story(net, day, key, data);
            return true;
        }

        // ---------------------------------------------------------------- once a day

        /// <summary>
        /// The key of what a post moves, for <see cref="FirstToday"/>: who posted it ("me", or "p:" and a player of the
        /// session), what it is (a review, an announcement of a kind, a report of a kind in a district) and the business.
        /// Null for anything else, and for nobody (the city's own accounts, whose number a day the city's day sets).
        /// </summary>
        public static string MoveKey(string who, FlareSource source, int arg, string district, string place)
        {
            if (string.IsNullOrEmpty(who))
            {
                return null;
            }
            switch (source)
            {
                case FlareSource.Review: return who + "|R|" + place;
                case FlareSource.Announce: return who + "|A|" + arg + "|" + place;
                case FlareSource.Report: return who + "|K|" + arg + "|" + district + "|" + place;
                default: return null;
            }
        }

        /// <summary>Who posted, as <see cref="MoveKey"/> names them: "me" for the player's own, "p:" and the player for a session's player's, null for the city's.</summary>
        public static string Mover(FlarePost p)
        {
            if (p == null)
            {
                return null;
            }
            return p.Author == FlareAuthor.Player ? "me" : p.Author == FlareAuthor.Network && !string.IsNullOrEmpty(p.AuthorId) ? "p:" + p.AuthorId : null;
        }

        /// <summary>Whether a key has moved something today; read only (the compose sheet's note).</summary>
        public static bool MovedToday(FlareNet net, int day, string key)
        {
            return key != null && net.MovedDay == day && net.Moved.Contains(key);
        }

        /// <summary>
        /// The first time today a player's review of a business, announcement for it or report of a thing moves it: true,
        /// and noted. False for the same again the same day: that post is made, seen and answered, and moves the business's
        /// customers, the City Office and the council no more. A null key (the city's own) is always first. A new day starts
        /// afresh; at most <see cref="MovedMost"/> are kept (the oldest go), so the save stays small.
        /// </summary>
        public static bool FirstToday(FlareNet net, int day, string key)
        {
            if (key == null)
            {
                return true;
            }
            if (net.MovedDay != day)
            {
                net.MovedDay = day;
                net.Moved.Clear();
            }
            if (net.Moved.Contains(key))
            {
                return false;
            }
            net.Moved.Add(key);
            while (net.Moved.Count > MovedMost)
            {
                net.Moved.RemoveAt(0);
            }
            return true;
        }

        /// <summary>Whether a report of the player's adds to an issue's weight in the council: the first three of a day do. Counted when it does.</summary>
        public static bool Weighs(FlareNet net, int day)
        {
            if (net.ReportsDay != day)
            {
                net.ReportsDay = day;
                net.ReportsToday = 0;
            }
            if (net.ReportsToday >= ReportsWeighed)
            {
                return false;
            }
            net.ReportsToday++;
            return true;
        }

        // ---------------------------------------------------------------- what a post does to a business

        /// <summary>
        /// Whether this machine applies what a post does to a business: never to another player's in a session (that player's
        /// own game applies it when the post comes to it: "never change another player's things"); an announcement only to
        /// the poster's own business, by the poster; a review, or an inspection after a complaint, to the player's own or to
        /// one of the city's (every machine has its own copy of those).
        /// </summary>
        public static bool EffectHere(FlareSource source, bool foreign, bool mine, bool ownPost)
        {
            if (foreign)
            {
                return false;
            }
            if (source == FlareSource.Announce)
            {
                return ownPost && mine;
            }
            return source == FlareSource.Review || source == FlareSource.Report;
        }

        /// <summary>What a review does to a business's customers, per cent for two days: a good one more, a poor one less, more for a review many saw.</summary>
        public static int ReviewBuzz(int stars, int reach)
        {
            int big = reach >= 2000 ? 1 : 0;
            return stars >= 4 ? 1 + big : stars <= 2 ? -1 - big : 0;
        }

        /// <summary>What an announcement for a business does: a little more custom for two days, more for an account many follow.</summary>
        public static int AnnounceBuzz(int reach)
        {
            return reach >= 5000 ? 2 : 1;
        }

        // ---------------------------------------------------------------- Trending and the tags

        /// <summary>
        /// The day's Trending: the tags of the posts shown in the last day, counted by how far each post went, the most
        /// first, at most so many. Into the two lists given (filled again).
        /// </summary>
        public static void Trending(List<FlarePost> posts, long now, int most, List<string> tags, List<int> counts)
        {
            tags.Clear();
            counts.Clear();
            for (int i = 0; i < posts.Count; i++)
            {
                FlarePost p = posts[i];
                if (string.IsNullOrEmpty(p.Topic) || p.At > now || now - p.At > 1440)
                {
                    continue;
                }
                int weight = 1 + Math.Min(40, p.Reach / 120);
                int start = 0;
                string t = p.Topic;
                while (start < t.Length)
                {
                    int end = t.IndexOf(' ', start);
                    if (end < 0)
                    {
                        end = t.Length;
                    }
                    if (end > start)
                    {
                        string tag = t.Substring(start, end - start);
                        int at = tags.IndexOf(tag);
                        if (at < 0)
                        {
                            tags.Add(tag);
                            counts.Add(weight);
                        }
                        else
                        {
                            counts[at] += weight;
                        }
                    }
                    start = end + 1;
                }
            }
            for (int i = 1; i < tags.Count; i++)
            {
                for (int k = i; k > 0 && (counts[k] > counts[k - 1] || (counts[k] == counts[k - 1] && string.CompareOrdinal(tags[k], tags[k - 1]) < 0)); k--)
                {
                    int c = counts[k];
                    counts[k] = counts[k - 1];
                    counts[k - 1] = c;
                    string s = tags[k];
                    tags[k] = tags[k - 1];
                    tags[k - 1] = s;
                }
            }
            if (tags.Count > most)
            {
                tags.RemoveRange(most, tags.Count - most);
                counts.RemoveRange(most, counts.Count - most);
            }
        }

        /// <summary>The tags typed in a text (#word: letters, digits and _, lower case), joined by a space, at most five, each once.</summary>
        public static string TagsOf(string text, string built)
        {
            var b = new StringBuilder(64);
            int n = 0;
            AddTags(built, b, ref n, false);
            AddTags(text, b, ref n, true);
            return b.Length > 0 ? b.ToString() : null;
        }

        private static void AddTags(string text, StringBuilder b, ref int n, bool hashed)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            for (int i = 0; i < text.Length && n < TagsMost; i++)
            {
                if (hashed && text[i] != '#')
                {
                    continue;
                }
                int start = hashed ? i + 1 : i;
                int end = start;
                while (end < text.Length && end - start < TagLength && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
                {
                    end++;
                }
                if (end > start)
                {
                    string tag = text.Substring(start, end - start).ToLowerInvariant();
                    if (!HasTag(b, tag))
                    {
                        b.Append(b.Length > 0 ? " " : string.Empty).Append(tag);
                        n++;
                    }
                }
                i = end;
            }
        }

        private static bool HasTag(StringBuilder b, string tag)
        {
            string all = " " + b + " ";
            return all.IndexOf(" " + tag + " ", StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// Typed text as Flare keeps it: on one line (a line break is a space), nothing that is not printable, spaces run
        /// together, trimmed, and no longer than <paramref name="most"/> characters. Null for nothing.
        /// </summary>
        public static string Clip(string text, int most = TextMost)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            var b = new StringBuilder(Math.Min(text.Length, most));
            bool space = false;
            for (int i = 0; i < text.Length && b.Length < most; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c) || char.IsControl(c))
                {
                    space = b.Length > 0;
                    continue;
                }
                if (c == '<' || c == '>')
                {
                    c = c == '<' ? '‹' : '›';   // the text's own markup is never markup: TextMeshPro would read <b> as bold
                }
                if (space && b.Length < most - 1)
                {
                    b.Append(' ');
                }
                space = false;
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 < text.Length && b.Length < most - 1)
                    {
                        b.Append(c).Append(text[i + 1]);
                    }
                    i++;   // (its second half goes with it, or neither does)
                    continue;
                }
                b.Append(c);
            }
            return b.Length > 0 ? b.ToString() : null;
        }

        // ---------------------------------------------------------------- the city's people's messages

        /// <summary>
        /// Whether one of the city's people writes to the player today, and who and about what (a key's end): a fan after a
        /// viral post, somebody asking for work when a business of the player's is hiring, an influencer offering to work
        /// together from Local on, the City Office thanking the player for a report acted on. Once every two days at most.
        /// </summary>
        public static bool CityDm(FlareFile f, int day, bool hiring, out string with, out string what)
        {
            with = null;
            what = null;
            if (day - f.Net.DmDay < DmGapDays || FlareRules.Roll(f.Seed, day, 840) >= DmChance)
            {
                return false;
            }
            int roll = FlareRules.Roll(f.Seed, day, 841);
            int who = PickCitizen(f.Seed, day, 845);   // (one of all of them, evenly)
            if (day - f.LastViralDay <= 2)
            {
                with = "c" + who;
                what = "fan";
            }
            else if (hiring && roll < 500)
            {
                with = "c" + who;
                what = "job";
            }
            else if (f.Tier >= FlareTier.Local && f.ActiveInfluencers > 0 && roll < 750)
            {
                int k = roll % f.Influencers.Count;
                for (int i = 0; i < f.Influencers.Count; i++)
                {
                    FlareInfluencer inf = f.Influencers[(k + i) % f.Influencers.Count];
                    if (inf.Active)
                    {
                        with = "i:" + inf.Id;
                        break;
                    }
                }
                what = "collab";
            }
            else
            {
                with = "c" + who;
                what = f.LastPostDay >= day - 3 ? "chat" : null;
            }
            if (with == null || what == null)
            {
                return false;
            }
            f.Net.DmDay = day;
            return true;
        }

        /// <summary>A message added to the player's, the oldest going when there are too many.</summary>
        public static FlareDm Dm(FlareNet net, string with, string name, long at, bool mine, string text, string key, string data)
        {
            var dm = new FlareDm { Id = net.NextDm++, With = with, Name = name, At = at, Mine = mine, Text = text, Key = key, Data = data, Read = mine };
            net.Dms.Add(dm);
            while (net.Dms.Count > FlareNet.DmsMost)
            {
                net.Dms.RemoveAt(0);
            }
            return dm;
        }

        // ---------------------------------------------------------------- the notifications

        /// <summary>
        /// The Notifications screen, newest first, at most so many: the reactions with names to the player's posts as they
        /// come in, the replies, the answers to the player's reports, the reviews of the player's businesses, the mentions,
        /// the messages and who goes to the player's invitations. Worked out from the posts and the messages each time it
        /// is asked (when the app opens, or on the hour for the badge): nothing is kept but how far it has been read.
        /// </summary>
        public static void Notices(FlareFile f, long now, string me, List<string> myPlaces, List<FlareReaction> scratch, List<FlareNotice> into, int most)
        {
            into.Clear();
            for (int i = 0; i < f.Posts.Count; i++)
            {
                FlarePost p = f.Posts[i];
                if (p.At > now)
                {
                    continue;
                }
                if (p.Author == FlareAuthor.Player)
                {
                    ReadReactors(p.Reactors, scratch);
                    for (int k = 0; k < scratch.Count; k++)
                    {
                        long at = p.At + scratch[k].Delay;
                        if (at <= now)
                        {
                            FlareNoticeKind kind = scratch[k].Kind == 'R' ? FlareNoticeKind.Reply : scratch[k].Kind == 'S' ? FlareNoticeKind.Repost : FlareNoticeKind.Like;
                            into.Add(new FlareNotice { Kind = kind, At = at, Post = p.Id, Who = scratch[k].Who, Dm = -1 });
                        }
                    }
                    continue;
                }
                FlarePost parent = p.ReplyTo >= 0 ? f.Post(p.ReplyTo) : null;
                if (parent != null && parent.Author == FlareAuthor.Player && (p.Source == FlareSource.Reply || p.Source == FlareSource.Answer))
                {
                    into.Add(new FlareNotice { Kind = p.Source == FlareSource.Answer && parent.Source == FlareSource.Report ? FlareNoticeKind.Case : FlareNoticeKind.Reply, At = p.At, Post = p.Id, Who = KeyOf(p), Dm = -1 });
                    continue;
                }
                if (p.Source == FlareSource.Review && p.Place != null && myPlaces != null && myPlaces.Contains(p.Place))
                {
                    into.Add(new FlareNotice { Kind = FlareNoticeKind.Review, At = p.At, Post = p.Id, Who = KeyOf(p), Dm = -1 });
                    continue;
                }
                if (me != null && p.Mention == me)
                {
                    into.Add(new FlareNotice { Kind = FlareNoticeKind.Mention, At = p.At, Post = p.Id, Who = KeyOf(p), Dm = -1 });
                }
            }
            for (int i = 0; i < f.Net.Dms.Count; i++)
            {
                FlareDm dm = f.Net.Dms[i];
                if (!dm.Mine && dm.At <= now)
                {
                    into.Add(new FlareNotice { Kind = FlareNoticeKind.Dm, At = dm.At, Post = -1, Who = dm.With, Dm = dm.Id });
                }
            }
            for (int i = 1; i < into.Count; i++)
            {
                for (int k = i; k > 0 && into[k].At > into[k - 1].At; k--)
                {
                    FlareNotice t = into[k];
                    into[k] = into[k - 1];
                    into[k - 1] = t;
                }
            }
            if (into.Count > most)
            {
                into.RemoveRange(most, into.Count - most);
            }
        }

        /// <summary>Notifications newer than the ones read, and messages not read: the app's badge.</summary>
        public static int Unread(List<FlareNotice> notices, FlareNet net)
        {
            int n = 0;
            for (int i = 0; i < notices.Count; i++)
            {
                n += notices[i].At > net.NoticeSeen ? 1 : 0;
            }
            return n;
        }

        /// <summary>The account a post is from, as a key: "me", "f3", "c12", "o:0", "b:name", "i:3", "p:player", "l:ledger", "m:company", "k:name", "t:name".</summary>
        public static string KeyOf(FlarePost p)
        {
            switch (p.Author)
            {
                case FlareAuthor.Player: return "me";
                case FlareAuthor.Friend: return "f" + p.Who;
                case FlareAuthor.Citizen: return "c" + p.Who;
                case FlareAuthor.Official: return "o:" + p.Who;
                case FlareAuthor.Business: return "k:" + p.Name;
                case FlareAuthor.Network: return "p:" + p.AuthorId;
                case FlareAuthor.Tycoon: return "t:" + p.Name;
                default: return FlareCast.KeyOf(p);
            }
        }

        // ---------------------------------------------------------------- what is kept

        /// <summary>
        /// The city's posts older than a few days go (the players' of a session a day later), and so does a reply whose
        /// post has gone. The player's own, the friends' and the influencers' are kept by FlareRules.Prune.
        /// </summary>
        public static void PruneCity(FlareFile f)
        {
            if (f.Posts.Count == 0)
            {
                return;
            }
            int today = f.Posts[f.Posts.Count - 1].Day;
            for (int i = 0; i < f.Posts.Count; i++)
            {
                today = Math.Max(today, f.Posts[i].Day);
            }
            bool removed = false;
            for (int i = f.Posts.Count - 1; i >= 0; i--)
            {
                FlarePost p = f.Posts[i];
                int keep = p.Author == FlareAuthor.Network ? NetworkKeepDays : CityKeepDays;
                bool city = p.Author == FlareAuthor.Citizen || p.Author == FlareAuthor.Official || p.Author == FlareAuthor.Business || p.Author == FlareAuthor.Network
                            || p.Author == FlareAuthor.Company || p.Author == FlareAuthor.Ledger || p.Author == FlareAuthor.Tycoon;
                if (city && today - p.Day >= keep && !(p.ReplyTo >= 0))
                {
                    f.Posts.RemoveAt(i);
                    removed = true;
                }
            }
            if (!removed)
            {
                return;
            }
            for (int i = f.Posts.Count - 1; i >= 0; i--)
            {
                FlarePost p = f.Posts[i];
                if (p.ReplyTo >= 0 && f.Post(p.ReplyTo) == null)
                {
                    f.Posts.RemoveAt(i);
                }
            }
        }
    }
}
