namespace RealLifeOverhaul.Family
{
    /// <summary>What a wish of the partner's does on a day (<see cref="PartnerExpectRules.Wish"/>).</summary>
    public enum WishStep { None, Ask, Short, Met }

    /// <summary>
    /// What a partner who lives with the player comes to expect as the player gets richer, game-free: three levels of the
    /// player's cash, and at each a bigger home, evenings out more often and a bigger weekly allowance, a car of their own
    /// from the first and a second home from the second. A wish is said once; going without cools the home a little each day
    /// after some days' grace and meeting it warms it; evenings out further apart than they now expect cool it each day, and
    /// each week's allowance warms or cools it by whether it is what they expect. Nothing here is read per frame.
    /// </summary>
    public static class PartnerExpectRules
    {
        /// <summary>The player's cash at the start's prices for each level: 250,000, a million, five million.</summary>
        public static readonly int[] Levels = { 250000, 1000000, 5000000 };

        /// <summary>Square metres of home they expect at each level (0 at none).</summary>
        public static readonly int[] HomeSqm = { 0, 60, 100, 160 };

        /// <summary>The most days between evenings out they accept at each level (0 at none: no expectation).</summary>
        public static readonly int[] DateDays = { 0, 14, 10, 7 };

        public const int HomeGraceDays = 10;        // days after a bigger home is asked for before a small one is held against the player
        public const float HomeShort = -0.4f;       // harmony a day while the home is smaller than they expect, past the grace
        public const float DatesShort = -0.3f;      // harmony a day while the last evening out is longer ago than they expect
        public const float HomeFound = 5f;          // harmony once, when a home big enough is found

        /// <summary>The level the cash has reached, 0 to 3, with the levels at today's prices (level 1000 at the start's).</summary>
        public static int LevelOf(float cash, int priceLevel)
        {
            int level = 0;
            for (int i = 0; i < Levels.Length; i++)
            {
                if (cash >= Levels[i] * (priceLevel / 1000f))
                {
                    level = i + 1;
                }
            }
            return level;
        }

        /// <summary>The home they expect at a level, in square metres; 0 for none.</summary>
        public static int HomeWanted(int level) => HomeSqm[Clamp(level)];

        /// <summary>Whether a home of a size falls short of what is wanted: a size the game does not give (0) never does.</summary>
        public static bool HomeShortOf(int sqm, int wanted) => wanted > 0 && sqm > 0 && sqm < wanted;

        /// <summary>Whether the last evening out is longer ago than expected at a level; counted from when they moved in if there has been none.</summary>
        public static bool DatesShortOf(int level, int day, int lastDate, int movedIn)
        {
            int most = DateDays[Clamp(level)];
            return most > 0 && day - (lastDate > movedIn ? lastDate : movedIn) > most;
        }

        // An allowance of their own, a week, chosen from the Family contact (at the start's prices, as pocket money is).
        public const int AllowanceMost = 10000;   // the most a week that can be typed, at the start's prices
        public static readonly int[] AllowanceWanted = { 0, 200, 1000, 2500 };   // the weekly allowance they expect at each level
        public const float AllowanceMet = 1.5f;     // harmony a week when it is at least what they expect
        public const float AllowanceShort = -2f;    // harmony a week when it falls short

        // Things of their own: a car from the first level, a second home from the second.
        public const int CarFrom = 1, SecondHomeFrom = 2;
        public const int WishGraceDays = 14;        // days after asking before going without is held against the player
        public const float WishShort = -0.3f;       // harmony a day while a wish goes without, past the grace
        public const float WishMet = 5f;            // harmony once, when it is met

        /// <summary>The weekly allowance they expect at a level, at the start's prices.</summary>
        public static int AllowanceExpected(int level) => AllowanceWanted[Clamp(level)];

        // How the partner is with money, a per cent of the usual allowance they expect (Social's traits, or the default partner's seed).
        public const int Thrifty = 70, Usual = 100, Giving = 80, Lavish = 150;
        public const int PerChild = 100;            // a week more for each child at home, unless a nursery or a nanny already costs the player

        /// <summary>
        /// The weekly allowance they expect, at the start's prices: the level's, by how they are with money, half again when
        /// they earn nothing (stopped, retired) or a quarter more on half days, a quarter more once married, and more for each
        /// child at home whose care the player does not already pay for. Rounded to tens.
        /// </summary>
        public static int AllowanceExpected(int level, int spend, bool earnsNothing, bool halfDays, bool married, int children, bool carePaid)
        {
            int basis = AllowanceExpected(level);
            if (basis <= 0)
            {
                return 0;
            }
            float x = basis * spend / 100f;
            x *= earnsNothing ? 1.5f : halfDays ? 1.25f : 1f;
            x *= married ? 1.25f : 1f;
            x += carePaid ? 0 : PerChild * children;
            return (int)(System.Math.Round(x / 10f) * 10);
        }

        /// <summary>How somebody without Social's traits is with money, from their seed: about one in five each thrifty, giving and lavish.</summary>
        public static int SpendOf(int seed)
        {
            int roll = ((seed >> 16) & 0x7fffffff) % 5;
            return roll == 0 ? Thrifty : roll == 1 ? Giving : roll == 2 ? Lavish : Usual;
        }

        /// <summary>How Social's people are with money, by their traits: the envious lavish, the careful thrifty, the generous giving.</summary>
        public static int SpendOfTraits(bool careful, bool envious, bool generous) => envious ? Lavish : careful ? Thrifty : generous ? Giving : Usual;

        /// <summary>The home's harmony for a week's allowance: nothing while nothing is expected.</summary>
        public static float AllowanceWeek(int paid, int expected, bool married) => expected <= 0 ? 0f : paid >= expected ? AllowanceMet : married ? AllowanceShort / 2f : AllowanceShort;

        /// <summary>How much closer a week's allowance brings them: a little when it is enough, more when it is generous (half as much again).</summary>
        public static float AllowanceAffection(int paid, int expected) => expected <= 0 ? 0f : paid * 2 >= expected * 3 ? 1f : paid >= expected ? 0.5f : 0f;

        // Closer, not only a warmer home: affection when a wish is met.
        public const float AffectionHome = 2f, AffectionCar = 2f, AffectionSecondHome = 3f;

        // A car asked back: it hurts, and they will not ask again for a while.
        public const float CarBackHarmony = -5f, CarBackAffection = -3f;
        public const int CarBackDays = 30;

        /// <summary>Whether they want a car of their own: from the level, and not within <see cref="CarBackDays"/> of one being asked back.</summary>
        public static bool WantsCar(int level, int day, int backDay) => level >= CarFrom && (backDay <= 0 || day - backDay > CarBackDays);

        /// <summary>The place in a list of the cars on sale, cheapest first, of the model bought for them at a level: dearer the richer.</summary>
        public static int ModelAt(int level, int count)
        {
            float[] at = { 0.3f, 0.4f, 0.7f, 0.95f };
            return count <= 0 ? 0 : System.Math.Min(count - 1, (int)(at[Clamp(level)] * (count - 1) + 0.5f));
        }

        /// <summary>
        /// A weekly allowance as typed, in dollars: the figures in it, cents after a last point or comma dropped, other marks
        /// ignored ("$1,500", "1500.00"), rounded to tens and held to <paramref name="most"/>; -1 when there is no figure.
        /// </summary>
        public static int ParseAllowance(string typed, int most)
        {
            if (string.IsNullOrEmpty(typed))
            {
                return -1;
            }
            string t = typed.Trim();
            int cut = System.Math.Max(t.LastIndexOf('.'), t.LastIndexOf(','));
            if (cut >= 0 && t.Length - cut - 1 <= 2 && t.Length - cut - 1 > 0)
            {
                t = t.Substring(0, cut);   // (cents)
            }
            long value = 0;
            bool any = false;
            foreach (char c in t)
            {
                if (c >= '0' && c <= '9')
                {
                    any = true;
                    value = System.Math.Min(value * 10 + (c - '0'), 1000000000L);
                }
            }
            if (!any)
            {
                return -1;
            }
            long tens = (value + 5) / 10 * 10;
            return (int)System.Math.Min(tens, most);
        }


        /// <summary>A second home: the player rents or owns at least two homes.</summary>
        public static bool SecondHomeMet(int homes) => homes >= 2;

        /// <summary>
        /// What a wish does on a day: <c>Ask</c> when it is wanted, not met and not yet asked for; <c>Short</c> when it has
        /// gone without past the grace; <c>Met</c> when one asked for is met; else <c>None</c>.
        /// </summary>
        public static WishStep Wish(bool wanted, bool met, int askedDay, int day)
        {
            if (askedDay > 0 && met)
            {
                return WishStep.Met;
            }
            if (!wanted || met)
            {
                return WishStep.None;
            }
            return askedDay <= 0 ? WishStep.Ask : day - askedDay > WishGraceDays ? WishStep.Short : WishStep.None;
        }

        private static int Clamp(int level) => level < 0 ? 0 : level > Levels.Length ? Levels.Length : level;
    }
}
