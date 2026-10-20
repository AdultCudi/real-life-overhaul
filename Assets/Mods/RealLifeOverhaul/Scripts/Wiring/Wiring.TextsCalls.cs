using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Street;
using RealLifeOverhaul.Texts;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// RLO Texts at the other end of a call's composer (Social/PersonDialog.Composer): a friend or the partner answers the
        /// player's typed words in their own voice. The card is data v3's answer to a message (TextsMapRules.Answering:
        /// friend.dm.answer, partner.dm.answer), the speaker's own data as their texts carry it (TextsPhone), with the call's
        /// last lines (SocialComposerRules.HistoryMost) and the typed words last, as the cards were trained; asked at the
        /// queue's first priority with a hold of SocialComposerRules.HoldSeconds. A pack of data v2 has no such card, and the
        /// call keeps its two answers; so does Mom's, for whom no pack has a card yet. Words that speak of chat (the card is a
        /// Flare message's: StreetCards.Chatty) are not shown. The words are shown once on this machine and change nothing:
        /// nothing is kept, nothing is shared.
        /// COST: nothing per frame; a card and a request a typed message, and a few comparisons when a topic is shown.
        /// </summary>
        public static class TextsCalls
        {
            private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

            internal static void Connect()
            {
                SocialLinks.ComposerWhyNot = WhyNot;
                SocialLinks.CleanTyped = Clean;
                SocialLinks.AnswerTyped = Answer;
            }

            internal static void Disconnect()
            {
                SocialLinks.ComposerWhyNot = null;
                SocialLinks.CleanTyped = null;
                SocialLinks.AnswerTyped = null;
            }

            // Null when the model can answer this person on a call now; otherwise why not.
            private static string WhyNot(SocialPerson person)
            {
                try
                {
                    string intent = Intent(person);
                    if (intent == null)
                    {
                        return "no pack has a card that answers what is said to " + (person != null && person.IsFamily ? "Mom" : "them");
                    }
                    return TextsDirector.Words.WhyNot(TextUse.People, false, intent, null, TextPriority.Now, false, SocialComposerRules.HoldSeconds);
                }
                catch (Exception e)
                {
                    Fault("RLO Texts, a call's composer", e);
                    return "RLO Texts failed (see the log)";
                }
            }

            private static string Intent(SocialPerson person)
            {
                if (person == null || !person.Met)
                {
                    return null;
                }
                return TextsMapRules.Answering(person.IsFamily ? TextsMapRules.From.Mom : person.Together ? TextsMapRules.From.Partner : TextsMapRules.From.Friend);
            }

            private static string Clean(string text) => TextCheck.Typed(text, SocialComposerRules.TypedMost);

            private static bool Answer(SocialPerson person, IReadOnlyList<SocialCallLine> lines, Func<bool> wanted, Action<string, string> done, out string why)
            {
                why = null;
                try
                {
                    string intent = Intent(person);
                    if (intent == null || lines == null || lines.Count == 0 || done == null)
                    {
                        why = "not asked: no card answers them, or nothing was said";
                        return false;
                    }
                    var r = new TextRequest
                    {
                        Use = TextUse.People, Intent = intent, Key = "call:" + person.Index, Speaker = "s:" + person.Index, Priority = TextPriority.Now,
                        Seconds = SocialComposerRules.HoldSeconds, Day = TextsGame.Day, Wanted = wanted,
                        Spoken = true   // (said on the call while the player waits: with their AI service late, the end of the hold is the pack's)
                    };
                    TextsPhone.FriendWho(person, r.Who, person.Together, false);
                    string player = r.Who.Player, first = r.Who.First;
                    for (int i = SocialComposerRules.CardFrom(lines.Count); i < lines.Count; i++)
                    {
                        r.History.Add(new TextSaid(lines[i].Player ? player : first, lines[i].Words));
                    }
                    r.Done = words => Came(r, words, done);
                    TextsMemory.Fill(r, person);
                    TextsMemory.Spoke(r, "call");
                    if (TextsDirector.Words.Ask(r, TextClock.Now))
                    {
                        return true;
                    }
                    why = r.Why;
                    return false;
                }
                catch (Exception e)
                {
                    Fault("RLO Texts, a call's composer", e);
                    why = "RLO Texts failed (see the log)";
                    return false;
                }
            }

            // The words back, or why none: said to the call with how they came (whose: the AI service's, the pack's).
            private static void Came(TextRequest r, string words, Action<string, string> done)
            {
                if (words != null && StreetCards.Chatty(words))
                {
                    done(null, TextsDirector.Words.From(r.Source) + ": the words speak of chat: \"" + words + "\"");
                    return;
                }
                done(words, words != null ? TextsDirector.Words.From(r.Source) + ", " + r.Intent + ", " + r.Took.ToString("0.00", Inv) + " s"
                                            + (r.ServiceMiss != null ? "; your own AI's words were not used: " + r.ServiceMiss : string.Empty)
                    : r.Why ?? "no words came");
            }
        }
    }
}
