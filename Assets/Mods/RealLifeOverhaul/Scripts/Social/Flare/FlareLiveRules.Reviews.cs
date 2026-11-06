using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The city's reviews come of visits: one of the city's people writes a review as they leave a business, at the review's
    /// hour, so only a business the game has open at that hour is reviewed. The day's posts pick among the businesses open
    /// then (FlareBiz.Open: the game's opening hours of the day, nothing while temporarily closed), and the hour itself asks
    /// the game again, so a business closed since the morning is not reviewed for a visit that never was. A review that went
    /// up stays, whatever its business does later in the day. Game-free. Cost: a pass over the day's few dozen businesses
    /// for each review the day makes, and a pass over the posts once a game hour.
    /// </summary>
    public static partial class FlareLiveRules
    {
        public const int ReviewFrom = 9;               // the first hour a city review is written in
        public const int ReviewTo = 22;                // ...the last
        public const int AllHours = 0xFFFFFF;          // a business open every hour of the day: bit h for hour h, 0 to 23

        /// <summary>Whether a day's open hours (bit h for hour h) have the business open at <paramref name="hour"/>.</summary>
        public static bool OpenAt(int open, int hour)
        {
            return hour >= 0 && hour < 24 && (open & (1 << hour)) != 0;
        }

        /// <summary>The hour of the k-th post of a day when it is a review: the visit's, as the post is dated (New).</summary>
        public static int ReviewHour(int seed, int day, int k)
        {
            return ReviewFrom + FlareRules.Roll(seed, day * 977 + k, 530) % (ReviewTo - ReviewFrom + 1);
        }

        /// <summary>
        /// Which of <paramref name="from"/> a review at <paramref name="hour"/> is of: by the roll among those the game has open
        /// then, or -1 when none is. With every one open it is the one the roll always picked.
        /// </summary>
        public static int OpenPick(List<FlareBiz> from, int hour, int roll)
        {
            int open = 0;
            for (int i = 0; from != null && i < from.Count; i++)
            {
                open += OpenAt(from[i].Open, hour) ? 1 : 0;
            }
            if (open == 0)
            {
                return -1;
            }
            int n = roll % open;
            for (int i = 0; i < from.Count; i++)
            {
                if (OpenAt(from[i].Open, hour) && n-- == 0)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// The city's reviews made on this machine (never a host's copy) whose visit falls after the hour
        /// <paramref name="after"/> and by the hour <paramref name="upTo"/> (each counted as day * 24 + hour): the ones the
        /// game is asked about now.
        /// </summary>
        public static void ReviewsDue(FlareFile f, long after, long upTo, List<FlarePost> into)
        {
            into.Clear();
            for (int i = 0; f != null && i < f.Posts.Count; i++)
            {
                FlarePost p = f.Posts[i];
                long hour = (long)p.Day * 24 + p.Hour;
                if (p.Source == FlareSource.Review && p.Author == FlareAuthor.Citizen && !p.Shared && hour > after && hour <= upTo)
                {
                    into.Add(p);
                }
            }
        }

        /// <summary>A review taken back before it went up, with anything under it: its business was closed at the visit. True when it was there.</summary>
        public static bool TakeBack(FlareFile f, int id)
        {
            bool gone = false;
            for (int i = f.Posts.Count - 1; i >= 0; i--)
            {
                if (f.Posts[i].Id == id || f.Posts[i].ReplyTo == id)
                {
                    gone |= f.Posts[i].Id == id;
                    f.Posts.RemoveAt(i);
                }
            }
            if (gone)
            {
                FlareRules.DropLikesOfGone(f);
            }
            return gone;
        }
    }
}
