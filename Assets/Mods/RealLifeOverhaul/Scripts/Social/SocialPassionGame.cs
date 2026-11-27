using System.Collections.Generic;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What finding out somebody's passion looks like in the game: the notice when it is found, the lines the People page and an outing's
    /// card say of it, and the clue the player gets. The rules (what counts, how many clues find it) are SocialPassionRules'. COST: strings
    /// made when a page is shown, an outing ends or something is found, never per frame.
    /// </summary>
    public static class SocialPassionGame
    {
        /// <summary>Their passion's name as the player reads it.</summary>
        public static string NameOf(SocialPerson p)
        {
            int at = SocialPassionRules.IndexOf(p.Passion);
            return at >= 0 ? SocialLife.T(SocialPassionCatalogue.Names[at]) : string.Empty;
        }

        /// <summary>
        /// A clue of somebody's passion noticed: <paramref name="points"/> of it, and <paramref name="why"/> for the log. The notice, a
        /// memory and the log line come the moment it is enough to find it out. Nothing while passions are off or it is found.
        /// </summary>
        public static void Clue(SocialPerson p, int points, string why)
        {
            if (!SocialPassionRules.Active(p) || p.PassionFound || points <= 0)
            {
                return;
            }
            bool found = SocialPassionRules.AddClue(p, points, SocialLife.Day);
            if (Meta.Dev.Verbose)
            {
                SocialDirector.Logger?.Info("Social check (passion): a clue of " + points + " noticed of " + p.Name + " from " + why + ", " + p.PassionClues + " of "
                                            + SocialPassionRules.Bar(p) + " to find it out (" + p.Passion + "): " + (found ? "found: PASS" : "still hidden: PASS"));
            }
            if (found)
            {
                Found(p, why);
            }
        }

        /// <summary>The player found out what they live for: the notice, with an expressive line, and a small lift of the mood.</summary>
        public static void Found(SocialPerson p, string why)
        {
            int at = SocialPassionRules.IndexOf(p.Passion);
            if (at < 0)
            {
                return;
            }
            Dictionary<string, string> data = SocialLife.Data(p, "passion", SocialLife.T(SocialPassionCatalogue.Names[at]));
            string[] lines = SocialPassionCatalogue.Found[at];
            SocialLife.Toast(NotificationType.Success, lines[SocialPassionRules.Variant(p, SocialLife.Day, lines.Length, 17)], data, 12f);
            ModState.AddModifier(ModState.Social);
            SocialDirector.Logger?.Info("Social: found out " + p.Name + "'s passion, " + p.Passion + " (" + why + ").");
        }

        /// <summary>The line the People page's first page adds: that there is more to them, with a hint once something has been noticed, or what they live for.</summary>
        public static string PageLine(SocialPerson p)
        {
            if (!SocialPassionRules.Active(p))
            {
                return null;
            }
            int at = SocialPassionRules.IndexOf(p.Passion);
            if (at < 0)
            {
                return null;
            }
            if (p.PassionFound)
            {
                Dictionary<string, string> known = SocialLife.Data(p, "passion", SocialLife.T(SocialPassionCatalogue.Names[at]));
                known["means"] = SocialLife.T(SocialPassionCatalogue.Means[at], SocialLife.Data(p));
                return SocialLife.T(SocialPassionCatalogue.PageKnown, known);
            }
            string line = SocialLife.T(SocialPassionCatalogue.PageUnknown, SocialLife.Data(p));
            return p.PassionClues > 0 ? line + " " + SocialLife.T(SocialPassionCatalogue.PageHints[at], SocialLife.Data(p)) : line;
        }

        /// <summary>What goes after their two traits on their history page: their passion once found, else that there is more to them. Null while passions are off.</summary>
        public static string HistoryPart(SocialPerson p)
        {
            if (!SocialPassionRules.Active(p))
            {
                return null;
            }
            int at = SocialPassionRules.IndexOf(p.Passion);
            if (at < 0)
            {
                return null;
            }
            return p.PassionFound
                ? SocialLife.T(SocialPassionCatalogue.HistoryKnown, SocialLife.Data(p, "passion", SocialLife.T(SocialPassionCatalogue.Names[at])))
                : SocialLife.T(SocialPassionCatalogue.HistoryUnknown, SocialLife.Data(p));
        }

        /// <summary>
        /// The line an outing's card or a moment gives when their passion moved it by <paramref name="delta"/> affection: by name once found,
        /// else a hint that does not name it. Null for a passion that is not in the tables.
        /// </summary>
        public static string CardLine(SocialPerson p, int at, float delta)
        {
            if (at < 0 || at >= SocialPassionRules.Count)
            {
                return null;
            }
            if (p.PassionFound)
            {
                Dictionary<string, string> data = SocialLife.Data(p, "passion", SocialLife.T(SocialPassionCatalogue.Names[at]));
                data["delta"] = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(delta))).ToString();
                return SocialLife.T(delta >= 0f ? SocialPassionCatalogue.CardFound : SocialPassionCatalogue.CardFoundCool, data);
            }
            string[] seen = SocialPassionCatalogue.Seen[at];
            string key = delta >= 0f ? seen[SocialPassionRules.Variant(p, SocialLife.Day, seen.Length, 71)] : SocialPassionCatalogue.Cold[at];
            return SocialLife.T(key, SocialLife.Data(p));
        }

        /// <summary>
        /// The test button: finds out the chosen person's passion at once (what the notice and the People page say), or names it when it was
        /// found already. Null while passions are off.
        /// </summary>
        public static string Reveal(SocialPerson p)
        {
            if (!SocialPassionRules.Active(p))
            {
                return null;
            }
            if (!p.PassionFound)
            {
                p.PassionClues = SocialPassionRules.Bar(p);
                p.PassionFound = true;
                p.Remember(MemoryCode.PassionFound, SocialLife.Day);
                Found(p, "the test button");
            }
            return NameOf(p);
        }
    }
}
