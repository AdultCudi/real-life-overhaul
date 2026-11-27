using System;
using System.Collections.Generic;
using System.Text;
using Entities;
using Localizor;
using UI.Notification;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game side of what people let slip about themselves: whether it is on, the line itself (one at a time, in an outing's chat, in a call or as a
    /// text of its own), what the player has heard so far (the People page's "Things you know about them") and the one notice the first time. The rules,
    /// the things and the texts' keys are game-free (<see cref="SocialFriendsRules"/>, <see cref="SocialInterests"/>). COST: nothing per frame; a few
    /// strings when a chat, a text or a page is made.
    /// </summary>
    public static class SocialHintGame
    {
        /// <summary>Whether people let things slip and quiz the player: the option, and the social life it belongs to.</summary>
        public static bool On => Settings.Social && Settings.FriendsQuiz;

        /// <summary>A thing as it sits inside a sentence: the answer's text with its first letter small.</summary>
        public static string Thing(int kind, int value)
        {
            return Lower(SocialLife.T(SocialInterests.ThingKey(kind, value)));
        }

        /// <summary>The thing a person is into for one kind, as it sits in a sentence.</summary>
        public static string ThingOf(SocialPerson person, int kind)
        {
            return Thing(kind, SocialInterests.ValueOf(SocialInterests.SeedOf(person), kind));
        }

        private static string Lower(string text)
        {
            return string.IsNullOrEmpty(text) || !char.IsUpper(text[0]) ? text : char.ToLowerInvariant(text[0]) + text.Substring(1);
        }

        /// <summary>
        /// Whether somebody lets something slip now, by what they are to the player and the roll of where it is said, and if so which kind and the key and
        /// values of the line. Nothing is marked: <see cref="Learned"/> does that once the line is really said.
        /// </summary>
        public static bool Pick(SocialPerson person, HintWhere where, out int kind, out string key, out Dictionary<string, string> data)
        {
            kind = -1;
            key = null;
            data = null;
            if (!On || person == null || !person.Met || !SocialState.Loaded)
            {
                return false;
            }
            int day = SocialLife.Day;
            if (!SocialFriendsRules.MayHint(person.Stage, person.Heard, person.InterestHintDay, day) || Random.value >= SocialFriendsRules.Chance(where))
            {
                return false;
            }
            int seed = SocialInterests.SeedOf(person);
            kind = SocialFriendsRules.NextKind(seed, person.Heard, day);
            if (kind < 0)
            {
                return false;
            }
            string[] pool = SocialInterests.Hints[kind];
            key = pool[SocialFriendsRules.Index(pool.Length, day, seed, 11 + kind)];
            data = SocialLife.Data(person, "thing", ThingOf(person, kind));
            return true;
        }

        /// <summary>The player heard it: the kind is theirs to answer for, they will not let another slip today, and the first time ever a notice says where it is kept.</summary>
        public static void Learned(SocialPerson person, int kind)
        {
            if (person == null || kind < 0 || kind >= SocialInterests.Kinds)
            {
                return;
            }
            if (person.InterestSeed == 0)
            {
                person.InterestSeed = person.Seed;   // from now on what they like is kept, whatever look they are given
            }
            person.Heard |= 1 << kind;
            person.InterestHintDay = SocialLife.Day;
            if ((SocialFriendsStore.Told & SocialFriendsStore.ToldHint) == 0)
            {
                SocialFriendsStore.Told |= SocialFriendsStore.ToldHint;
                SocialLife.Toast(NotificationType.Info, "reallife_friends_hint_first", SocialLife.Data(person), 12f);
            }
            SocialDirector.Logger?.Info("Social check (a hint): " + person.Name + " let slip a " + (InterestKind)kind + " (" + SocialInterests.Count(person.Heard) + " of "
                                        + SocialInterests.Kinds + " heard, they tell at most " + SocialFriendsRules.HintCap(person.Stage) + " at this stage): PASS.");
        }

        /// <summary>
        /// A chat's opening line with a hint after it, when they let one slip: the text to show, or null for the line alone. The hint is as they would
        /// say it, after a few words that bring it in.
        /// </summary>
        public static string Hinted(SocialPerson person, string openKey, Dictionary<string, string> openData, HintWhere where)
        {
            if (!Pick(person, where, out int kind, out string key, out Dictionary<string, string> data))
            {
                return null;
            }
            int seed = SocialInterests.SeedOf(person);
            string lead = SocialInterests.Leads[SocialFriendsRules.Index(SocialInterests.Leads.Length, SocialLife.Day, seed, 29)];
            string said = SocialLife.T(lead) + " " + SocialLife.T(key, data);
            Learned(person, kind);
            return SocialLife.T(openKey, openData) + "\n\n" + said;
        }

        /// <summary>Puts a hint after the opening line of a page of a chat, if they let one slip: a face to face chat or a call's. The page's text becomes the line and the hint.</summary>
        public static void Weave(DialogEntry entry, SocialPerson person, string openKey, Dictionary<string, string> openData, HintWhere where)
        {
            try
            {
                string text = Hinted(person, openKey, openData, where);
                if (text != null)
                {
                    entry.messageData = SocialLinks.WordsKey.Localize(new Dictionary<string, string> { { "text", text } });
                    person.LastSaid = text.Replace('\n', ' ');
                }
            }
            catch (Exception e)
            {
                SocialDirector.Logger?.Error(e);   // the chat stands without it
            }
        }

        /// <summary>
        /// The People page's section: "Things you know about them" and a line for each thing heard, or a line that says nothing yet. Null while the quiz
        /// option is off or for somebody not met, so the page is as it was.
        /// </summary>
        public static string KnownLines(SocialPerson person)
        {
            if (!On || person == null || !person.Met)
            {
                return null;
            }
            int count = SocialInterests.Count(person.Heard);
            if (count == 0)
            {
                return SocialLife.T("reallife_friends_know_none");
            }
            var text = new StringBuilder(256);
            text.Append(SocialLife.T("reallife_friends_know_head", new Dictionary<string, string>
            {
                { "count", count.ToString() }, { "total", SocialInterests.Kinds.ToString() }
            }));
            for (int k = 0; k < SocialInterests.Kinds; k++)
            {
                if (SocialInterests.Has(person.Heard, k))
                {
                    text.Append('\n').Append(SocialLife.T("reallife_friends_know_line", new Dictionary<string, string>
                    {
                        { "label", SocialLife.T(SocialInterests.Labels[k]) }, { "thing", ThingOf(person, k) }
                    }));
                }
            }
            return text.ToString();
        }
    }
}
