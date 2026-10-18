using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>One reaction of somebody with a name to a post: a like, a reply or a share, so many minutes after it went up.</summary>
    public struct FlareReaction
    {
        public char Kind;            // 'L' a like, 'R' a reply, 'S' a share
        public string Who;           // "f3" a friend, "p4" the partner, "s:Name" one of the staff, "b" the boss, "c12" one of the city's people
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
    /// Flare as a living network, by rule: the city's people (made from the city's seed), the posts the city makes each day
    /// from what happened, the reactions to a post over the next hours, pile-ons, the City Office's cases, what a post does
    /// to a business, Trending, the messages and the notifications. Game-free.
    ///
    /// Every roll is <see cref="FlareRules.Roll"/> of the city's seed and the day, so the same city on the same day makes the
    /// same posts, and in a Going Public session the host's are everybody's. At Normal a day has about 14 posts of the
    /// city's people, 2 to 5 of official accounts and 4 of businesses; Quiet about half, Busy nearly twice. That is set by
    /// the day, never by how many people there are, so nothing here walks all four hundred.
    /// </summary>
    public static partial class FlareLiveRules
    {
        public const int TextMost = 200;               // characters the player may type in a post or a reply
        public const int DmTextMost = 300;             // ...in a message
        public const int TagsMost = 5;                 // tags kept of one post
        public const int TagLength = 24;   // characters
        public const int ReportsWeighed = 3;           // the player's reports a day that weigh in the council; any number may be made
        public const int MovedMost = 200;              // what moved a business or the City Office today, kept: a day's worth
        public const int Citizens = 400;               // never saved: made from the city's seed
        public const int FirstCitizens = 40;           // ...made as they always were, so every save's "c0" to "c39" stays who it was
        public const int CityKeepDays = 3;             // the city's posts are kept this many days (the player's, the friends' longer)
        public const int NetworkKeepDays = 4;          // ...the players' of a session

        public static readonly int[] CityTalkPerDay = { 7, 14, 24 };   // the city's people's posts, by the option: quiet, normal, busy
        public static readonly int[] OfficialMost = { 2, 4, 6 };   // by the option
        public static readonly int[] BusinessPerDay = { 2, 4, 7 };   // by the option

        // What a post does to a business, per cent of its customers (inside FlareRules.BuzzMost, either way).
        public const int ReviewDays = 2;
        public const int ComplaintBuzz = -2;   // per cent, while the inspection's talk lasts
        public const int AnnounceDays = 2;
        public const int EventCrowd = 2;               // an event at a business: customers on its day
        public const int CleanFootfall = 1;            // per cent more customers in a district while the City Office's cleaners go round it

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

        /// <summary>The seed of the city's people and posts: the city's (the host's, for a guest that followed one), else the cast's.</summary>
        public static int CitySeed(FlareFile f)
        {
            return f.Net.CitySeed != 0 ? f.Net.CitySeed : f.CastSeed != 0 ? f.CastSeed : f.Seed;
        }

        /// <summary>
        /// The city's people on Flare, the same for the same seed on every machine. The first <see cref="FirstCitizens"/> are
        /// made as they always were, so every key a save keeps ("c12") is still the same person; each after them gets a name
        /// nobody has yet and the district with the fewest people so far. Allocates, once a seed: some 0.2 ms.
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
                // Unlucky every time: walk the surnames from a rolled one (some 7,000 names for 400 people).
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

        /// <summary>Whether one of the city's people is a woman, as CitizensOf makes them, without the list.</summary>
        public static bool CitizenFemale(int seed, int index)
        {
            return index < FirstCitizens ? FemaleFirst[FlareRules.Roll(seed, index, SaltFirst) % FirstNames.Length] : FemaleAfter(seed, index);
        }

        /// <summary>One of the city's people's age in years, as CitizensOf makes them, without the list.</summary>
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

        /// <summary>Which of the city's people writes a day's k-th post, as CityDay picks: for drawing their face ahead of the day.</summary>
        public static int TalkCitizen(int seed, int day, int k)
        {
            return Wide(seed, day * 31 + k, 501) % Citizens;
        }

        /// <summary>One of the city's people by a roll, evenly among all of them: their number, 0 to Citizens - 1.</summary>
        public static int PickCitizen(int seed, int a, int salt)
        {
            return Wide(seed, a, salt) % Citizens;
        }

        /// <summary>
        /// The city's posts of a day, once a day, each at its own minute: its people's, the official accounts' and the
        /// businesses'. Nothing for a guest that follows the host's feed. The city's people's posts also go into
        /// <paramref name="made"/>, for the director to file and apply. The count made.
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
            count += Officials(f, facts, seed, OfficialMost[busy]);
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
                // The city's faults are real: its people report those through Scripts/Faults; here only the buses in the rain.
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

        /// <summary>A review's stars from the game's satisfaction (0 to 100) and a roll, one star off now and then; three for one not known.</summary>
        public static int Stars(int satisfaction, int roll)
        {
            int stars = satisfaction < 0 ? 3 : FlareRules.Clamp((satisfaction + 10) / 20, 1, 5);
            stars += roll < 150 ? -1 : roll > 850 ? 1 : 0;
            return FlareRules.Clamp(stars, 1, 5);
        }

        // The official accounts' posts of the day: each when the city has a reason for it, at most so many.
        private static int Officials(FlareFile f, FlareCityFacts facts, int seed, int most)
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
            count += Official(f, day, seed, 907, most - count, facts.LateSites > 0 || FlareRules.Roll(seed, day, 910) < 300, FlareOfficial.CityOffice, FlareOfficialPost.Notice,
                facts.SiteDistrict, "roadworks");
            count += Official(f, day, seed, 908, most - count, facts.Election && FlareRules.Roll(seed, day, 911) < 350, FlareOfficial.CityOffice, FlareOfficialPost.Election, null, "election");
            string outage = facts.Districts.Count > 0 ? facts.Districts[FlareRules.Roll(seed, day, 913) % facts.Districts.Count] : null;
            count += Official(f, day, seed, 909, most - count, FlareRules.Roll(seed, day, 912) < 150, FlareOfficial.Utilities, FlareOfficialPost.Outage, outage, "utilities");
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

        private static int Official(FlareFile f, int day, int seed, int slot, int room, bool reason, FlareOfficial who, FlareOfficialPost what, string district, string tag,
            int variant = 0, string name = null)
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

        public const int TycoonVoices = 3, TycoonLines = 3, TycoonMoves = 8;   // the tycoons with a voice of their own, their everyday lines, the kinds of move they post about
        public const int TycoonMoveArg = 10;                                     // a post's Arg from here on: about a move of that kind (Arg - 10)

        /// <summary>
        /// A rival tycoon's post of the day on about a third of days, in their own voice: the move they just announced if any,
        /// else an everyday line. Null on a day without one.
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
    }
}
