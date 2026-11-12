using System;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The people's games, the game studio's audience: each person from <see cref="GameAge"/> has a bit for each platform they play on (PC, console,
    /// phone: <see cref="PopulationCatalogue.GamePlatformShare"/> by age, leaned by class and sex) and for each of the studio's twelve genres they like
    /// (the studio's share of each platform's players who like it, by their main platform, leaned by age and sex). A child first drawn takes some of a
    /// parent's likes and plays more on what the home has; at each birthday a few bits are drawn again for the new age, so tastes change slowly. Every
    /// draw is seeded by the person's id and the day, the same on every machine. The day's figures count the residents of each genre on each platform,
    /// by district and class (<see cref="Add"/>), and the studio's audience is those residents times the world's people over the residents at the
    /// population's start (<see cref="Audience"/>): its first day is the documented world's, and after it the audience follows births, deaths, moves
    /// and the world's growth. Game-free. COST: a few draws a person when made and on a birthday; a few additions a person in the day's figures.
    /// </summary>
    public static class GameRules
    {
        public const int Genres = 12;                   // the studio's genres (Tech/Studio/StudioCatalogue's Genre)
        public const int Platforms = 3;                 // PC, console, phone: the studio's game platforms
        public const int Cells = Genres * Platforms;    // a count for each genre on each platform, genre by genre
        public const int PlatformShift = Genres;        // a person's platform bits follow their genre bits
        public const int GameAge = 6;                   // years: a younger child plays no game of their own

        private const int Pc = 0, Console = 1, Phone = 2;
        private const int Steps = 64;                   // draws a person may take on one day: each its own number below this

        /// <summary>The games tables' age band: 6-12, 13-17, 18-34, 35-54, 55-64, 65 and over.</summary>
        public static int Band(int age)
        {
            return age < 13 ? 0 : age < 18 ? 1 : age < 35 ? 2 : age < 55 ? 3 : age < 65 ? 4 : 5;
        }

        /// <summary>The platforms of a person's games, a bit each from PC.</summary>
        public static int PlatformsOf(ushort games)
        {
            return games >> PlatformShift;
        }

        /// <summary>
        /// A person's games drawn on a day for their age, class and sex: the platforms they play on, then the genres they like by their main platform.
        /// A child (under 18) takes some of a parent's likes and plays more on a platform the parent plays on; 0 for no parent. Nothing under <see cref="GameAge"/>.
        /// </summary>
        public static ushort Draw(int seed, int id, int day, int age, int cls, bool female, ushort parent)
        {
            if (age < GameAge)
            {
                return 0;
            }
            int band = Band(age);
            bool child = age < 18 && parent != 0;
            int platforms = 0;
            for (int p = 0; p < Platforms; p++)
            {
                if (PopDraw.Mille(seed, PopDraw.Games, id, day * Steps + p, PlatformChance(band, cls, female, p, child && (PlatformsOf(parent) & (1 << p)) != 0)))
                {
                    platforms |= 1 << p;
                }
            }
            if (platforms == 0)
            {
                return 0;   // plays on nothing: counted on no platform
            }
            int main = Main(platforms, age);
            int genres = 0;
            for (int g = 0; g < Genres; g++)
            {
                bool like = child && PopDraw.Mille(seed, PopDraw.Games, id, day * Steps + 24 + g, PopulationCatalogue.GameInherit)
                    ? (parent & (1 << g)) != 0
                    : PopDraw.Mille(seed, PopDraw.Games, id, day * Steps + 8 + g, GenreChance(main, band, female, g));
                genres |= like ? 1 << g : 0;
            }
            return (ushort)(genres | platforms << PlatformShift);
        }

        /// <summary>
        /// A birthday's change: each platform and each genre drawn again for the new age at <see cref="PopulationCatalogue.GameDrift"/> per mille; a
        /// child reaching <see cref="GameAge"/> drawn whole, taking some of a parent's likes.
        /// </summary>
        public static ushort Birthday(int seed, int id, int day, int age, int cls, bool female, ushort games, ushort parent)
        {
            if (age < GameAge)
            {
                return 0;
            }
            if (age == GameAge)
            {
                return Draw(seed, id, day, age, cls, female, parent);
            }
            int band = Band(age);
            int platforms = PlatformsOf(games);
            for (int p = 0; p < Platforms; p++)
            {
                if (PopDraw.Mille(seed, PopDraw.Games, id, day * Steps + 40 + p, PopulationCatalogue.GameDrift))
                {
                    bool plays = PopDraw.Mille(seed, PopDraw.Games, id, day * Steps + 44 + p, PlatformChance(band, cls, female, p, false));
                    platforms = plays ? platforms | 1 << p : platforms & ~(1 << p);
                }
            }
            if (platforms == 0)
            {
                return 0;
            }
            int main = Main(platforms, age);
            int genres = PlatformsOf(games) == 0 ? 0 : games & ((1 << Genres) - 1);
            bool fresh = PlatformsOf(games) == 0;   // took up games this year: every genre drawn
            for (int g = 0; g < Genres; g++)
            {
                if (fresh || PopDraw.Mille(seed, PopDraw.Games, id, day * Steps + 48 + g, PopulationCatalogue.GameDrift))
                {
                    bool like = PopDraw.Mille(seed, PopDraw.Games, id, day * Steps + 8 + g, GenreChance(main, band, female, g));
                    genres = like ? genres | 1 << g : genres & ~(1 << g);
                }
            }
            return (ushort)(genres | platforms << PlatformShift);
        }

        /// <summary>Per mille who play on a platform at an age band, class and sex, more for a child whose home has it.</summary>
        public static int PlatformChance(int band, int cls, bool female, int platform, bool home)
        {
            int share = PopulationCatalogue.GamePlatformShare[Clamp(band, 0, 5)][platform];
            share = share * PopulationCatalogue.GameClassLean[Clamp(cls, 0, 2)][platform] / 1000;
            int women = PopulationCatalogue.GameWomen[platform];
            share = share * (female ? women : 2000 - women) / 1000;
            share = home ? share * PopulationCatalogue.GameHome / 1000 : share;
            return Clamp(share, 0, PopulationCatalogue.GameLikeMost);
        }

        /// <summary>Per mille who like a genre among the players of a main platform, by age band and sex.</summary>
        public static int GenreChance(int main, int band, bool female, int genre)
        {
            int share = PopulationCatalogue.GameGenreShare[Clamp(main, 0, Platforms - 1)][genre];
            share = share * PopulationCatalogue.GameAgeLean[Clamp(band, 0, 5)][genre] / 1000;
            int women = PopulationCatalogue.GameWomenLean[genre];
            share = share * (female ? women : 2000 - women) / 1000;
            return Clamp(share, 0, PopulationCatalogue.GameLikeMost);
        }

        /// <summary>The platform a person plays most: a console for the young who have one, a PC for the older, a phone for those with nothing else.</summary>
        public static int Main(int platforms, int age)
        {
            bool pc = (platforms & 1 << Pc) != 0, console = (platforms & 1 << Console) != 0;
            if (console && (age < 35 || !pc))
            {
                return Console;
            }
            return pc ? Pc : Phone;
        }

        /// <summary>A person's games added to a table of counts from an offset: one for each genre they like on each platform they play on.</summary>
        public static void Add(ushort games, int[] counts, int offset)
        {
            int platforms = PlatformsOf(games);
            if (platforms == 0)
            {
                return;
            }
            for (int g = 0; g < Genres; g++)
            {
                if ((games & 1 << g) == 0)
                {
                    continue;
                }
                for (int p = 0; p < Platforms; p++)
                {
                    if ((platforms & 1 << p) != 0)
                    {
                        counts[offset + g * Platforms + p]++;
                    }
                }
            }
        }

        /// <summary>
        /// The city's counts made afresh from its people (a load's, a guest's figures aside): the residents of each genre on each platform, by district
        /// and class, households away left out as the day's figures leave them out.
        /// </summary>
        public static void CountAll(PopulationState s)
        {
            int districts = s.Districts.Count;
            Array.Clear(s.GameCounts, 0, s.GameCounts.Length);
            if (s.GameByDistrict.Length != districts * Cells)
            {
                s.GameByDistrict = new int[districts * Cells];
            }
            Array.Clear(s.GameByDistrict, 0, s.GameByDistrict.Length);
            Array.Clear(s.GameByClass, 0, s.GameByClass.Length);
            HouseholdTable hh = s.Households;
            for (int h = 0; h < hh.Count; h++)
            {
                if (!hh.Alive(h) || (hh.Flags[h] & HouseholdFlags.Away) != 0)
                {
                    continue;
                }
                int d = hh.District[h], c = Clamp(hh.Class[h], 0, PopulationCatalogue.Classes - 1);
                for (int p = hh.First[h]; p >= 0; p = s.People.Next[p])
                {
                    ushort games = s.People.Games[p];
                    Add(games, s.GameCounts, 0);
                    if (d < districts)
                    {
                        Add(games, s.GameByDistrict, d * Cells);
                    }
                    Add(games, s.GameByClass, c * Cells);
                }
            }
        }

        /// <summary>Every person's games drawn as the generation draws them (a line from before the games: version 1), a child's leaning on its household's head.</summary>
        public static void DrawAll(PopulationState s)
        {
            HouseholdTable hh = s.Households;
            PersonTable pt = s.People;
            for (int h = 0; h < hh.Count; h++)
            {
                if (!hh.Alive(h))
                {
                    continue;
                }
                int head = hh.First[h];
                for (int p = head; p >= 0; p = pt.Next[p])
                {
                    ushort parent = p != head && head >= 0 && pt.Role[p] == (byte)Role.Child ? pt.Games[head] : (ushort)0;
                    pt.Games[p] = Draw(s.Seed, pt.Id[p], s.Day, s.Age(p, s.Day), hh.Class[h], pt.Female[p] == 1, parent);
                }
            }
        }

        /// <summary>The population's start for the studio's world factor: the counts as they stand, when it has none yet.</summary>
        public static void Start(PopulationState s)
        {
            long had = 0;
            for (int i = 0; i < s.GameStart.Length; i++)
            {
                had += s.GameStart[i];
            }
            if (had == 0)
            {
                Array.Copy(s.GameCounts, s.GameStart, Math.Min(s.GameCounts.Length, s.GameStart.Length));
                s.Growth.PeopleStart = s.People.Live;
            }
            if (s.Growth.PeopleStart <= 0)
            {
                s.Growth.PeopleStart = s.People.Live;   // a save from before the growth: its people at this load stand for the start's
            }
        }

        /// <summary>
        /// The residents who play read as the city's tastes, not its size: their count at the start's people (the studio sells to the world, whose
        /// people the city's do not make; a city that grows on its new land is no bigger a market for a game sold everywhere).
        /// </summary>
        public static long Share(long now, long people, long peopleStart)
        {
            return people > 0 && peopleStart > 0 ? Math.Max(0L, now) * peopleStart / people : Math.Max(0L, now);
        }

        /// <summary>
        /// The studio's audience of a genre on a platform: the residents now times the world's people over the residents at the population's start (the
        /// world factor, fixed then, grown as the world grows); -1 with no start, so the studio takes the documented world.
        /// </summary>
        public static long Audience(long now, long start, long world)
        {
            return start > 0 ? Math.Max(0L, now) * Math.Max(0L, world) / start : -1;
        }

        private static int Clamp(int v, int least, int most)
        {
            return v < least ? least : v > most ? most : v;
        }
    }
}
