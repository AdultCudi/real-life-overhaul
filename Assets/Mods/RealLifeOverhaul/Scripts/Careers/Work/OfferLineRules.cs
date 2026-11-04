using System.Collections.Generic;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The one line the job offer card says of how the player got here, as rules with no game types, checked offline (EconomyCheck
    /// FixW2Checks): when the player sat the mod's interview for a post at the very building the offer is made in, the score the
    /// employer gave them (ProRules.HireScore) sets how warmly it is put; with no such interview the line is the game's own record of
    /// a firing there, else none. Never made up: a card with nothing true to say says nothing.
    /// </summary>
    public static class OfferLineRules
    {
        public const int None = 0, Narrow = 1, Fine = 2, Liked = 3;
        public const int RecentDays = 30;   // an interview older than this does not colour a later offer

        /// <summary>How warm the interview was: 0 for an application that was not interviewed to an offer, else by the score it was rated at.</summary>
        public static int Band(int stage, int score)
        {
            if (stage != (int)ApplyStep.Offered && stage != (int)ApplyStep.Accepted)
            {
                return None;
            }
            return score >= ProRules.OfferPass + 15 ? Liked : score >= ProRules.OfferPass ? Fine : Narrow;
        }

        /// <summary>The player's newest interviewed application for a post at an address, sat within <see cref="RecentDays"/> of <paramref name="today"/>, or null.</summary>
        public static Application Find(IList<Application> applications, string street, int number, int today)
        {
            for (int i = applications != null ? applications.Count - 1 : -1; i >= 0; i--)
            {
                Application a = applications[i];
                if (a != null && a.Post.Street == street && a.Post.Number == number && Band(a.Stage, a.Score) != None && today - a.InterviewDay <= RecentDays)
                {
                    return a;
                }
            }
            return null;
        }

        /// <summary>The text key of the interview's line for a band; null for none.</summary>
        public static string Key(int band) => band == Liked ? "reallife_ui_world_offer_liked" : band == Fine ? "reallife_ui_world_offer_fine" :
            band == Narrow ? "reallife_ui_world_offer_narrow" : null;
    }
}
