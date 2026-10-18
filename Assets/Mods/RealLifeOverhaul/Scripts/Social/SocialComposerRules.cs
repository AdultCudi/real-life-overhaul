using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>One line of a call as RLO Texts' card carries it: the player's or the other person's, and the words shown.</summary>
    public struct SocialCallLine
    {
        public bool Player;
        public string Words;

        public SocialCallLine(bool player, string words)
        {
            Player = player;
            Words = words;
        }
    }

    /// <summary>
    /// The rules of a call's composer (PersonDialog.Composer): when a topic is answered by the player's own typed words
    /// rather than one of its two answers, what those words count as, and which lines of the call go on RLO Texts' card.
    /// The typed words always count as the topic's warm answer, the first of its two (<see cref="TypedWarm"/>): the
    /// best a choice could give and no more, settled when they are sent, with the same limits (one chat a day worth the
    /// talk, <see cref="SocialRules.CallTopics"/> topics a call), whatever was typed and whatever comes back. Nothing here
    /// reads the words. Game-free (EconomyCheck, ComposerChecks.cs). COST: a few comparisons on a click.
    /// </summary>
    public static class SocialComposerRules
    {
        public const bool TypedWarm = true;      // a typed answer counts as the topic's first answer, the warm one, whatever it says
        public const int TypedMost = 160;        // characters the player may type on a call: as long as the answer may be
        public const int HistoryMost = 3;        // lines of the call on the card, oldest first, the typed words last (the cards were trained so)
        public const int LinesKept = 8;          // lines of the call remembered for it
        public const double HoldSeconds = 6.0;   // real seconds typed words wait for their answer before the written one is shown

        /// <summary>
        /// Null when a topic's answer is typed (the composer); otherwise why the two answers are shown, as without the
        /// composer. <paramref name="textsWhyNot"/> is RLO Texts' own reason, null when it can answer this person now.
        /// </summary>
        public static string Why(bool socialOn, bool optionOn, bool canType, string textsWhyNot)
        {
            if (!socialOn)
            {
                return "the social life is off";
            }
            if (!optionOn)
            {
                return "typing in calls is switched off";
            }
            if (!canType)
            {
                return "the game has no text box";
            }
            return textsWhyNot;
        }

        /// <summary>Remembers a line of the call, the newest last, at most <see cref="LinesKept"/>; an empty one is not a line.</summary>
        public static void Keep(List<SocialCallLine> lines, bool player, string words)
        {
            if (lines == null || string.IsNullOrEmpty(words))
            {
                return;
            }
            lines.Add(new SocialCallLine(player, words));
            if (lines.Count > LinesKept)
            {
                lines.RemoveAt(0);
            }
        }

        /// <summary>The first of the call's lines on the card: the last <see cref="HistoryMost"/>, the typed words among them last.</summary>
        public static int CardFrom(int count) => Math.Max(0, count - HistoryMost);
    }
}
