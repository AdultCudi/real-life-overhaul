using System;
using System.Collections.Generic;
using RealLifeOverhaul.Street;
using RealLifeOverhaul.Texts;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// What strangers say in the street, worded by RLO Texts. The game has decided the line (StreetRules: its kind, and with
        /// it the outcome); here the card is built from the person (StreetStranger: name, age, job, home, traits, today's mood
        /// leaned the way it went, how they talk), the hour, what the conversation knows (the place and its street, the way,
        /// the plan booked, the candidate...) and what the player just said, and asked at the queue's first priority with a
        /// deadline of <see cref="Street.StreetRules.Hold"/> seconds. Two cards are tried in turn:
        ///
        ///   data v4   street talk's own intents (TextIntents.Street.cs: street.greet, .brush, .directions and the rest, a branch
        ///             per outcome), asked only of a pack trained on them (TextsHub.Data)
        ///   data v3   what fits until then: a city person answering what was said (citizen.dm.answer, the mood steering the
        ///             tone) for a greeting, small talk, a compliment, a joke, a flirt, the player's own words and a goodbye; the
        ///             invitation's answer (citizen.dm.invite_yes, _maybe, _no, with v3's reasons)
        ///
        /// Anything else, or no pack, a v2 pack, words that fail the checker or speak of chat (Flare, messages, following, a
        /// handle), or a game in another language: the written line, at once or when the hold is over. The opening line is
        /// guessed while the player looks at somebody and taken at the press. Nothing is kept, nothing is shared.
        /// COST: nothing per frame; a card and a request per line, on a press or a click.
        /// </summary>
        public static class StreetTexts
        {
            internal static void Connect()
            {
                StreetDirector.Words = Ask;
                StreetDirector.Guess = Guess;
                StreetDirector.TakeGuess = Take;
            }

            internal static void Disconnect()
            {
                StreetDirector.Words = null;
                StreetDirector.Guess = null;
                StreetDirector.TakeGuess = null;
            }

            // ---------------------------------------------------------------- asking

            private static bool Ask(StreetChat c, StreetLine line, Dictionary<string, string> data, string player, Action<string> done)
            {
                try
                {
                    if (c == null || !TextsDirector.Words.Wants(TextUse.People, false))
                    {
                        return false;
                    }
                    Action<string> back = words => done(StreetCards.Chatty(words) ? null : words);
                    TextRequest r = RequestV4(c, line, data, back, false);
                    if (r != null && TextsDirector.Words.Ask(r, TextClock.Now))
                    {
                        return true;
                    }
                    r = RequestV3(c, line, data, back, false);
                    return r != null && TextsDirector.Words.Ask(r, TextClock.Now);
                }
                catch (Exception e)
                {
                    Fault("RLO Texts, street talk", e);
                    return false;
                }
            }

            private static void Guess(StreetChat c, StreetLine line, Dictionary<string, string> data)
            {
                try
                {
                    if (c == null || !TextsDirector.Words.Wants(TextUse.People, false))
                    {
                        return;
                    }
                    TextRequest r = RequestV4(c, line, data, words => { }, true);
                    if (r == null || !TextsDirector.Words.Ask(r, TextClock.Now))
                    {
                        r = RequestV3(c, line, data, words => { }, true);
                        if (r != null)
                        {
                            TextsDirector.Words.Ask(r, TextClock.Now);
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("RLO Texts, street talk", e);
                }
            }

            private static string Take(StreetChat c)
            {
                try
                {
                    string words = c != null ? TextsDirector.Words.TakeGuess(GuessKey(c)) : null;
                    return StreetCards.Chatty(words) ? null : words;
                }
                catch (Exception e)
                {
                    Fault("RLO Texts, street talk", e);
                    return null;
                }
            }

            private static string GuessKey(StreetChat c) => "stg:" + c.Who.Key;

            private static TextRequest Request(StreetChat c, string intent, string branch, Action<string> done, bool guess)
            {
                return new TextRequest
                {
                    Use = TextUse.People, Intent = intent, Branch = branch, Key = guess ? GuessKey(c) : "st:" + c.Who.Key + ":" + c.Turns, Speaker = "st:" + c.Who.Key,
                    Priority = TextPriority.Now, Seconds = guess ? Street.StreetRules.GuessSeconds : Street.StreetRules.Hold, Guess = guess, Day = TextsGame.Day, Done = done
                };
            }

            // The v4 card: the friend face with a stranger's role, the values of the line, the player's last words.
            private static TextRequest RequestV4(StreetChat c, StreetLine line, Dictionary<string, string> data, Action<string> done, bool guess)
            {
                string intent = StreetCards.V4(line, out string branch, out int lean);
                if (intent == null || TextIntents.Find(intent, branch) == null)
                {
                    return null;
                }
                TextRequest r = Request(c, intent, branch, done, guess);
                StreetStranger s = c.Who;
                TextWho w = r.Who;
                w.Clear();
                w.Name = s.Name;
                w.First = s.First;
                w.Age = s.Age;
                w.Job = TextWords.Jobs[Math.Max(0, Math.Min(s.Job, TextWords.Jobs.Length - 1))];
                w.District = SocialPlaces.District(s.District);
                w.Role = StreetCards.Role(intent, branch);
                TextWords.TraitsOf((int)s.Traits, w.Traits);
                w.Mood = TextWords.Mood(StreetCards.Leaned(s.Mood, lean));
                w.Style = Street.StreetRules.Styles[Math.Max(0, Math.Min(s.Style, Street.StreetRules.Styles.Length - 1))];
                w.Player = TextsGame.PlayerName;
                w.When = TextsGame.When();
                r.Slots.Set("player", w.Player).Set("job", w.Job).Set("district", w.District);
                Copy(data, r.Slots, "place", "place");
                Copy(data, r.Slots, "place_two", "place_two");
                Copy(data, r.Slots, "street", "street");
                Copy(data, r.Slots, "way", "way");
                Copy(data, r.Slots, "walk", "walk");
                Copy(data, r.Slots, "what", "what");
                Copy(data, r.Slots, "event", "event");
                Copy(data, r.Slots, "candidate", "candidate");
                Copy(data, r.Slots, "shop", "shop");
                Copy(data, r.Slots, "t_outing", "outing");
                Copy(data, r.Slots, "t_time", "time");
                if (line == StreetLine.AreaNews)
                {
                    Copy(data, r.Slots, "district", "district");   // (the day's trouble: where it was)
                }
                History(c, r, 1);
                return r;
            }

            // The v3 card: a city person answering what was said, the mood steering it; an invitation's answer with v3's reasons.
            private static TextRequest RequestV3(StreetChat c, StreetLine line, Dictionary<string, string> data, Action<string> done, bool guess)
            {
                string intent = StreetCards.V3(line, out string reason, out int lean);
                if (intent == null || TextIntents.Find(intent) == null)
                {
                    return null;
                }
                TextRequest r = Request(c, intent, null, done, guess);
                StreetStranger s = c.Who;
                TextWho w = r.Who;
                w.Clear();
                w.Name = s.Name;
                w.First = s.First;
                w.Age = s.Age;
                w.Handle = s.Handle;
                w.District = SocialPlaces.District(s.District);
                w.Mood = TextWords.Mood(StreetCards.Leaned(s.Mood, lean));
                w.Style = Street.StreetRules.Styles[Math.Max(0, Math.Min(s.Style, Street.StreetRules.Styles.Length - 1))];
                w.Player = TextsGame.PlayerName;
                w.When = TextsGame.When();
                Copy(data, r.Slots, "t_outing", "outing");
                Copy(data, r.Slots, "place", "place");
                Copy(data, r.Slots, "t_time", "time");
                if (reason != null)
                {
                    r.Slots.Set("reason", reason);
                }
                History(c, r, 3);
                return r;
            }

            // The thread: the last few lines of this conversation, oldest first, the player's last (v4's cards: the player's last words only, as trained).
            private static void History(StreetChat c, TextRequest r, int most)
            {
                int from = Math.Max(0, c.History.Count - most);
                for (int i = from; i < c.History.Count; i++)
                {
                    string h = c.History[i];
                    bool player = h.StartsWith("P:", StringComparison.Ordinal);
                    r.History.Add(new TextSaid(player ? r.Who.Player : c.Who.First, h.Substring(2)));
                }
            }

            private static void Copy(Dictionary<string, string> from, TextSlots to, string name, string slot)
            {
                if (from != null && from.TryGetValue(name, out string v) && !string.IsNullOrEmpty(v))
                {
                    to.Set(slot, v);
                }
            }
        }
    }
}
