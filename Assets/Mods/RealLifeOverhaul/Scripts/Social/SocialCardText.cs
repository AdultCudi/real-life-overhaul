using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The lines of a card that says what an evening with somebody changed, shared by the outing's card and the one after meeting somebody's friends:
    /// a figure with its sign and colour, and how far a figure stands from the next step ("(74 of 90 for dating)"). Builds strings: for when a card is
    /// made, never per frame.
    /// </summary>
    public static class SocialCardText
    {
        /// <summary>A figure with its sign, green when it went up and red when it went down.</summary>
        public static string Signed(float value)
        {
            int rounded = Mathf.RoundToInt(value);
            return rounded >= 0 ? "<color=#7CE08A>+" + rounded + "</color>" : "<color=#FF8A70>" + rounded + "</color>";
        }

        /// <summary>
        /// Where the code has a next figure for a meter (SocialRules.Next), how far it now stands from it: "(74 of 90 for dating)". Nothing for a figure
        /// with no step ahead.
        /// </summary>
        public static string NextOf(SocialPerson who, SocialMeter meter)
        {
            if (!SocialRules.Next(who, meter, SocialLife.Romantic(who), SocialState.Partner == null, out float need, out SocialGoal goal))
            {
                return string.Empty;
            }
            return "   " + SocialLife.T("reallife_social_card_next", new Dictionary<string, string>(3)
            {
                { "now", Mathf.FloorToInt(SocialRules.Value(who, meter)).ToString(CultureInfo.InvariantCulture) }, { "need", Mathf.RoundToInt(need).ToString(CultureInfo.InvariantCulture) },
                { "goal", SocialLife.T(GoalKey(goal)) }
            });
        }

        public static string GoalKey(SocialGoal goal)
        {
            switch (goal)
            {
                case SocialGoal.Friend: return "reallife_social_goal_friend";
                case SocialGoal.CloseFriend: return "reallife_social_goal_close";
                case SocialGoal.Date: return "reallife_social_goal_date";
                case SocialGoal.Partner: return "reallife_social_goal_partner";
                default: return "reallife_social_goal_movein";
            }
        }

        /// <summary>One line of a card: a label, then its value.</summary>
        public static void Line(StringBuilder text, string labelKey, string value)
        {
            text.Append(SocialLife.T(labelKey)).Append("   ").Append(value).Append('\n');
        }

        /// <summary>The three figures a worth changed, each with its label, the ones that moved only: "Affection +4, Trust -4".</summary>
        public static string Figures(SocialWorth worth)
        {
            var text = new StringBuilder(64);
            Add(text, "reallife_social_card_familiarity", worth.Familiarity);
            Add(text, "reallife_social_card_affection", worth.Affection);
            Add(text, "reallife_social_card_trust", worth.Trust);
            return text.ToString();
        }

        private static void Add(StringBuilder text, string labelKey, float value)
        {
            if (Mathf.RoundToInt(value) == 0)
            {
                return;
            }
            text.Append(text.Length > 0 ? ", " : string.Empty).Append(SocialLife.T(labelKey)).Append(' ').Append(Signed(value));
        }
    }
}
