using System;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare in the city by rule, the market and the council (the brief's seventh and eighth steps): a viral thread about a business moves its trade on
    /// the stock market by the city's investors it reached (each kick the market's own buzz, MarketInputs.BuzzKickBp; the market-abuse rule of
    /// FlareRules.Abuse stands); a post about an issue moves the feed's mood about the city's running by the residents it reached; the player's word in
    /// an election is worth what the share of the city's adults who follow them makes it. Game-free. COST: a few operations, on a post.
    /// </summary>
    public static partial class FlareCityRules
    {
        /// <summary>
        /// Points of the feed's mood a post about an issue moves: Flare's own move for a post that interests the city as much as
        /// <see cref="FlareCityCatalogue.IssueMoodAt"/> says, more or less by the residents it reached.
        /// </summary>
        public static int IssueMood(int interested, int adults)
        {
            long mood = (long)FlareRules.MoodIssuePost * Math.Max(0, interested) * 1000 / Math.Max(1, adults) / FlareCityCatalogue.IssueMoodAt;
            return (int)Math.Max(FlareCityCatalogue.IssueMoodLeast, Math.Min(FlareCityCatalogue.IssueMoodMost, mood));
        }

        /// <summary>
        /// How much of the most the player's word adds to an endorsement, thousandths: the city's adults who follow them against
        /// <see cref="FlareCityCatalogue.EndorseFull"/>, for a trusted account.
        /// </summary>
        public static int EndorseShare(int cityFollowers, int adults, int reputation)
        {
            long full = Math.Max(1L, (long)Math.Max(0, adults) * FlareCityCatalogue.EndorseFull / 1000);
            long share = Math.Min(1000L, (long)Math.Max(0, cityFollowers) * 1000 / full);
            return (int)(share * Math.Max(0, Math.Min(reputation, 50)) / 50);
        }

        /// <summary>The city's adults, as the day's index counted them.</summary>
        public static int AdultsOf(FlareCityState c)
        {
            int n = 0;
            for (int d = 0; d < c.Adults.Length; d++)
            {
                n += c.Adults[d];
            }
            return n;
        }
    }
}
