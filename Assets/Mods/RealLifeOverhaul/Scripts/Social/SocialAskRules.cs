using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What somebody of the player's circle may ask of their own accord, and what they may say twice. A player with no
    /// business was asked by Mom, again and again, when she would get the tour of it: the outing she wanted was picked with
    /// no look at what the player had, its one line was sent each time she got in touch, and ringing her found no business
    /// to go to, so nothing ever answered it. Now the tour is asked only of somebody with a business that has traded long
    /// enough for word of it to get round, once, and not again until a long time has passed; the call that answers it and
    /// the tour itself both settle it; and nobody sends the line they sent last. Game-free, checked offline
    /// (EconomyCheck --premises). COST: a few comparisons when somebody gets in touch, never per frame.
    /// </summary>
    public static class SocialAskRules
    {
        public const int TourHeardDays = 2;    // days a business has been the player's before anybody can "keep hearing about it"
        public const int TourAgainDays = 30;   // days before the same person asks to be shown round again, answered with a no or not at all
        public const int BusinessKind = 7;     // PlanKind.Business: the tour
        public const int FallbackKind = 0;     // PlanKind.Coffee: what somebody asks for whose own wish cannot be had

        /// <summary>
        /// Whether somebody may ask for the tour today: the player has a business, theirs for <see cref="TourHeardDays"/>
        /// days or more; this person has not been shown round (<paramref name="shownDay"/> 0); and they have not asked in
        /// the last <see cref="TourAgainDays"/> days (<paramref name="askedDay"/> 0: never).
        /// </summary>
        public static bool MayAskTour(bool business, int heldDays, int askedDay, int shownDay, int day)
        {
            if (!business || heldDays < TourHeardDays || shownDay > 0)
            {
                return false;
            }
            return askedDay <= 0 || day - askedDay >= TourAgainDays;
        }

        /// <summary>Whether somebody may wish for the tour at all, when their wish for the days ahead is drawn: only of a player with a business.</summary>
        public static bool MayWish(int kind, bool business) => kind != BusinessKind || business;

        /// <summary>The outing they ask for: their wish, or a coffee when the wish is a tour that may not be asked now.</summary>
        public static int InviteKind(int wants, bool tour) => wants == BusinessKind && !tour ? FallbackKind : wants;

        /// <summary>
        /// The line somebody sends, given the one they sent last: the line itself when it is another; else another line of
        /// its pool that fits the player (<paramref name="fits"/>, Core/LineNeeds), by the roll; else, for a line that
        /// reports something that happened (<paramref name="must"/>: a loan repaid, a plan called off), the line itself,
        /// since the news is new each time; else none, and nothing is sent. So nobody says the same thing twice running
        /// of their own accord.
        /// </summary>
        public static string Next(string last, string key, string[] pool, bool must, int roll, Func<string, bool> fits)
        {
            if (key == null || !string.Equals(last, key, StringComparison.Ordinal))
            {
                return key;
            }
            int others = 0;
            for (int i = 0; pool != null && i < pool.Length; i++)
            {
                others += Other(pool[i], key, fits) ? 1 : 0;
            }
            if (others > 0)
            {
                int nth = (roll & int.MaxValue) % others;
                for (int i = 0; i < pool.Length; i++)
                {
                    if (Other(pool[i], key, fits) && nth-- == 0)
                    {
                        return pool[i];
                    }
                }
            }
            return must ? key : null;
        }

        private static bool Other(string line, string key, Func<string, bool> fits)
        {
            return line != null && !string.Equals(line, key, StringComparison.Ordinal) && (fits == null || fits(line));
        }
    }
}
