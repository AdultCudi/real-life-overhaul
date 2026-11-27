using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The social director's part for passions and the memory of a moment: the daily round's reading of the player's money for a gold
    /// digger, a partner showing what they live for, the text that lets a hint of a passion slip, and the text the day after a moment.
    /// </summary>
    public sealed partial class SocialDirector
    {
        // A day of the round for one person: money warms or cools a gold digger, and a partner shows their passion of themselves.
        private static void DailyPassion(SocialPerson p, int day, int tierToday)
        {
            if (!SocialPassionRules.Active(p))
            {
                return;
            }
            if (SocialPassionRules.ShowsOfThemselves(p, day))
            {
                SocialPassionGame.Clue(p, SocialPassionRules.Bar(p), "time together");
            }
            SocialPassionRules.Daily(p, tierToday, SocialState.WealthTier, out float affection, out float trust);
            if (affection == 0f && trust == 0f)
            {
                return;
            }
            SocialLife.Change(p, 0f, affection, trust);
            if (affection <= -3f)
            {
                SocialLife.SendText(p, SocialLines.Pick(SocialPassionCatalogue.Cooled), SocialLife.Data(p));   // the money fell a step: they heard
            }
        }

        // The key of a text that lets a hint of their passion slip, when one is due (SocialPassionRules.HintDue); null for none.
        private static string PassionHint(SocialPerson p, int day)
        {
            if (!SocialPassionRules.HintDue(p, day))
            {
                return null;
            }
            int at = SocialPassionRules.IndexOf(p.Passion);
            if (at < 0)
            {
                return null;
            }
            string[] lines = SocialPassionCatalogue.TextHints[at];
            return lines[SocialPassionRules.Variant(p, day, lines.Length, 29)];
        }

        // Somebody's text of what they live for: sent through the social life's own texts, and a clue once it is.
        private static bool SendHint(SocialPerson p, string key, int day)
        {
            if (!SocialLife.SendText(p, key, SocialLife.Data(p)))
            {
                return false;
            }
            p.HintDay = day;
            SocialPassionGame.Clue(p, 1, "a hint in a text");
            return true;
        }

        // A day or more after a moment on an outing they text about it, once (their line for that moment); let go after three days.
        private static bool MomentEchoText(int day, int hour)
        {
            for (int i = 0; i < SocialState.Count; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p == null || !p.Met || !p.MomentEcho || day <= p.MomentDay)
                {
                    continue;
                }
                MomentDef def = SocialMomentRules.ById(p.MomentLast);
                if (def == null || day > p.MomentDay + 3 || (p.IsFamily && !MomContact.On))
                {
                    p.MomentEcho = false;
                    continue;
                }
                if (p.Asleep(hour))
                {
                    continue;
                }
                var data = new Dictionary<string, string>(SocialLife.Data(p)) { { "place", p.AfterPlace ?? SocialLife.T(SocialMomentCatalogue.PlaceFallback) } };
                if (SocialLife.SendText(p, def.Echo, data))
                {
                    p.MomentEcho = false;
                    return true;
                }
            }
            return false;
        }
    }
}
