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
    /// rather than one of its two answers, what those words are worth, what they are read as and what that gets back, and
    /// which lines of the call go on RLO Texts' card. What typed words are worth is the topic's warm answer, the first of
    /// its two (<see cref="TypedWarm"/>): the best a choice could give and no more, settled when they are sent, with the
    /// same limits (one chat a day worth the talk, <see cref="SocialRules.CallTopics"/> topics a call), whatever was typed.
    /// What they say is read (<see cref="Read"/>, the Reading part): good news, bad news, a bare yes or no, which each topic
    /// takes as its question means it (<see cref="Heard"/>), or nothing clear; and that picks their written answer
    /// (<see cref="Reaction"/>): the topic's own line for good news or for bad, and a neutral line for anything not clear,
    /// never the line for bad news. RLO Texts' words take its place only when they do not read the other way
    /// (<see cref="Against"/>). Game-free (EconomyCheck, ComposerChecks.cs and TypedReadingChecks.cs).
    /// COST: a few comparisons on a click, and a reading of the words when they are sent and when the model's answer comes.
    /// </summary>
    public static partial class SocialComposerRules
    {
        public const bool TypedWarm = true;      // a typed answer is worth the topic's first answer, the warm one, whatever it says
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

        /// <summary>What a reading means to this topic's question: a bare yes or no as the topic takes it (SocialTopic.YesIs, NoIs), the rest as read.</summary>
        public static SocialRead Heard(SocialTopic topic, SocialRead read)
        {
            switch (read)
            {
                case SocialRead.Yes:
                    return topic != null ? topic.YesIs : SocialRead.Unclear;
                case SocialRead.No:
                    return topic != null ? topic.NoIs : SocialRead.Unclear;
                default:
                    return read;
            }
        }

        /// <summary>
        /// The lines they may answer typed words with, by what the words were heard as: the topic's for good news and for bad,
        /// and the neutral lines (SocialCatalogue.TypedNeutral) for anything not clear or a topic with none.
        /// </summary>
        public static string[] Reaction(SocialTopic topic, SocialRead heard)
        {
            if (topic != null && heard == SocialRead.Good && topic.TypedGood != null && topic.TypedGood.Length > 0)
            {
                return topic.TypedGood;
            }
            if (topic != null && heard == SocialRead.Bad && topic.TypedBad != null && topic.TypedBad.Length > 0)
            {
                return topic.TypedBad;
            }
            return SocialCatalogue.TypedNeutral;
        }

        /// <summary>True when RLO Texts' words read the other way from what the typed words were heard as: good news answered as bad, or bad as good.</summary>
        public static bool Against(SocialRead heard, string words)
        {
            if (heard != SocialRead.Good && heard != SocialRead.Bad)
            {
                return false;
            }
            SocialRead said = Read(words);
            return (heard == SocialRead.Good && said == SocialRead.Bad) || (heard == SocialRead.Bad && said == SocialRead.Good);
        }
    }
}
