using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What the game studio says on Flare's bridge: the kinds of news a studio's residents can talk about.</summary>
    public enum StudioNews
    {
        Announced = 0, Trailer = 1, Expo = 2, Previews = 3, Launched = 4, Reviewed = 5, Pirated = 6, Update = 7, AddOn = 8, Award = 9, Count = 10
    }

    /// <summary>
    /// What the game studio asks of the living economy's other workstreams, filled in by Wiring/Wiring.StudioLinks. Each is
    /// optional with a default, and a fault in the other feature reads as the default. Game-free: EconomyCheck compiles it.
    ///
    /// The population (<see cref="Population"/>): for a genre and a platform on a day, the people who own the platform and
    /// like the genre, the city's residents (by district and class in its own view) times the world factor that stands for
    /// the world beyond the city, and how many of them are residents. Until it is wired, <see cref="World"/> gives the
    /// documented world: each platform's installed base growing by the save's years towards its ceiling, times the share of its
    /// players who like the genre, sized so that ScaleRules' bands hold for a typical game (StudioCatalogue.ReferenceMarket). The population
    /// must keep that scale: its residents with a taste for a genre and a platform, times its world factor, should sum to the
    /// same order as <see cref="World"/> on the first day and grow with the city and the world after it.
    ///
    /// Flare (<see cref="Talk"/>, <see cref="Devlog"/>, <see cref="Crowdfund"/>): news a studio's residents talk about, a
    /// devlog post that brings hype from the residents who see it, and crowdfunding from those who follow the studio. With
    /// none wired, nothing is posted and the two actions are not offered.
    /// </summary>
    public static class StudioBridges
    {
        /// <summary>The people who own a platform and like a genre on a day of a calendar of so many days a year (residents times the world factor), and of them the city's residents.</summary>
        public delegate long AudienceAsk(int genre, int platform, int day, int year, out long residents);

        /// <summary>The population's audience. Default: none, so <see cref="World"/>.</summary>
        public static AudienceAsk Population;

        /// <summary>A studio's news for Flare: the kind, the studio's place, the game's name and a figure (a score, copies, a percent). Default: nothing is posted.</summary>
        public static Action<int, string, string, long> Talk;

        /// <summary>A devlog posted on Flare for a game (the studio's place, the game's name): the hype it brought, thousandths of the audience. Default: not offered.</summary>
        public static Func<string, string, int> Devlog;

        /// <summary>A crowdfunding campaign on Flare for a concept (the studio's place, the goal in cents): the cents pledged. Default: not offered.</summary>
        public static Func<string, long, long> Crowdfund;

        public static bool CanDevlog => Devlog != null;

        public static bool CanCrowdfund => Crowdfund != null;

        /// <summary>The audience of a genre on a platform on a day of a calendar of <paramref name="year"/> days a year: the population's, or the world's with none, or if it faults.</summary>
        public static long Audience(int genre, int platform, int day, int year, out long residents)
        {
            residents = 0;
            try
            {
                if (Population != null)
                {
                    long people = Population(genre, platform, day, year, out residents);
                    if (people >= 0)
                    {
                        residents = Math.Max(0L, Math.Min(residents, people));
                        return people;
                    }
                }
            }
            catch (Exception)
            {
                // the population's fault: the documented world stands in, as with none
            }
            residents = 0;
            return World(genre, platform, day, year);
        }

        /// <summary>
        /// The documented world: the platform's installed base grown by its rate each of the save's years of <paramref name="year"/>
        /// days, filling towards its ceiling (ScaleRules.Grown), times the thousandths of its players who like the genre. Whole
        /// numbers, the same everywhere.
        /// </summary>
        public static long World(int genre, int platform, int day, int year)
        {
            if (genre < 0 || genre >= (int)Genre.Count || platform < 0 || platform >= StudioCatalogue.GamePlatforms)
            {
                return 0;
            }
            long people = ScaleRules.Grown(StudioCatalogue.InstalledBase[platform], StudioCatalogue.BaseGrowth[platform], StudioCatalogue.BaseCeiling[platform], day, year);
            return people * StudioCatalogue.GenreShare[platform][genre] / 1000;
        }

        /// <summary><see cref="Talk"/> told, a fault in Flare ignored.</summary>
        public static void Say(StudioNews news, string place, string game, long figure)
        {
            try
            {
                Talk?.Invoke((int)news, place, game, figure);
            }
            catch (Exception)
            {
                // Flare's fault: the studio goes on unheard
            }
        }

        /// <summary><see cref="Devlog"/> asked: the hype brought, 0 with none or a fault, at most the most hype there is.</summary>
        public static int DevlogNow(string place, string game)
        {
            try
            {
                return Devlog != null ? Math.Max(0, Math.Min(StudioCatalogue.HypeMost, Devlog(place, game))) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary><see cref="Crowdfund"/> asked: the cents pledged, 0 with none or a fault, never more than the goal.</summary>
        public static long CrowdfundNow(string place, long goal)
        {
            try
            {
                return Crowdfund != null ? Math.Max(0L, Math.Min(goal, Crowdfund(place, goal))) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static void Reset()
        {
            Population = null;
            Talk = null;
            Devlog = null;
            Crowdfund = null;
        }
    }
}
