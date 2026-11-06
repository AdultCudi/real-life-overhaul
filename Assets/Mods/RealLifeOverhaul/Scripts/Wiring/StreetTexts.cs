using System;
using System.Collections.Generic;
using RealLifeOverhaul.Street;
using RealLifeOverhaul.Texts;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// What strangers say in the street, worded by RLO Texts. The game has decided the line and its outcome; the card is
        /// built from the stranger, the hour, what the conversation knows and the player's last words, and asked at the
        /// queue's first priority for <see cref="Street.StreetRules.Hold"/> seconds: street talk's own cards of data 4 and
        /// 5 first, else data 3's answer and invitation cards. Otherwise, or with words that fail the checks (chat, a name
        /// not told, a line said lately), the written line is used. The opening line is guessed while the player looks at
        /// somebody; on a slow machine (<see cref="AheadShare"/>) the blue button's answer is asked as it is shown. With
        /// the player's own AI service, a guess still being written is waited for, never asked again. Nothing is kept or
        /// shared. Nothing per frame: a request per line, on a press or a click.
        /// </summary>
        public static class StreetTexts
        {
            public const double AheadShare = 0.5;   // the blue button's answer is asked ahead when a reply is expected to take more than this share of the hold
            private const int HistoryMost = 3;      // lines of the conversation on a v4 card: the player's, theirs, the player's last

            private static readonly StreetCards.Echoes Lately = new StreetCards.Echoes();   // the model's last lines across strangers, none said twice

            internal static void Connect()
            {
                StreetDirector.Words = Ask;
                StreetDirector.Guess = Guess;
                StreetDirector.TakeGuess = Take;
                StreetDirector.Ahead = Ahead;
                StreetDirector.TakeAhead = TakeAhead;
                StreetDirector.Spoken = Remembered;
            }

            internal static void Disconnect()
            {
                StreetDirector.Words = null;
                StreetDirector.Guess = null;
                StreetDirector.TakeGuess = null;
                StreetDirector.Ahead = null;
                StreetDirector.TakeAhead = null;
                StreetDirector.Spoken = null;
                Lately.Clear();
            }

            // A line the stranger said, however its words came: what they will remember of it, after the line's own card.
            private static void Remembered(StreetChat c, StreetLine line, Dictionary<string, string> data)
            {
                try
                {
                    TextsMemory.Street(c, line, data);
                }
                catch (Exception e)
                {
                    Fault("RLO Texts, what a stranger remembers", e);
                }
            }

            private static bool Ask(StreetChat c, StreetLine line, Dictionary<string, string> data, string player, Action<string> done)
            {
                try
                {
                    if (c == null)
                    {
                        return false;
                    }
                    if (!TextsDirector.Words.Wants(TextUse.People, false))
                    {
                        c.Heard = "not asked: RLO Texts is off, has no pack or is not running";
                        string intent = StreetCards.ByData(line, TextsDirector.Words.Data, true, out string _, out int _);
                        if (intent != null)
                        {
                            TextsDirector.Words.NotAsked(TextUse.People, false, intent);   // counted: the written line, and why
                        }
                        return false;
                    }
                    TextRequest asked = null;
                    bool waited = false;
                    Action<string> back = words => Came(c, asked, waited, words, done);
                    string guessed = c.WaitFor;   // a guess of this line the AI service is still writing: waited for, never asked again
                    c.WaitFor = null;
                    TextRequest r = RequestV4(c, line, data, back, false);
                    asked = r;
                    if (r != null && ((waited = Adopted(ref guessed, r)) || TextsDirector.Words.Ask(r, TextClock.Now)))
                    {
                        c.Waits = r.Waits;
                        return true;
                    }
                    string why = r != null ? r.Why : null;
                    r = RequestV3(c, line, data, back, false);
                    asked = r;
                    if (r != null && ((waited = Adopted(ref guessed, r)) || TextsDirector.Words.Ask(r, TextClock.Now)))
                    {
                        c.Waits = r.Waits;
                        return true;
                    }
                    c.Heard = r != null ? (why != null ? why + "; data v3's card " : string.Empty) + r.Why
                        : why ?? "not asked: no card for this line (" + line + "): written lines only";
                    return false;
                }
                catch (Exception e)
                {
                    Fault("RLO Texts, street talk", e);
                    return false;
                }
            }

            // A guess of this line still being written by the AI service, waited for as the line itself; tried once.
            private static bool Adopted(ref string guessed, TextRequest r)
            {
                string key = guessed;
                guessed = null;
                return key != null && TextsDirector.Words.Adopt(key, r, TextClock.Now);
            }

            // The words back, made fit to be said; whose they are, the card and the time (or why not) go on the conversation
            // for its log line.
            private static void Came(StreetChat c, TextRequest r, bool waited, string words, Action<string> done)
            {
                string why = null;
                string said = words != null ? Spoken(c, words, out why) : null;
                c.Heard = said != null ? TextsDirector.Words.From(r.Source) + ", " + Card(r) + ", " + Ms(r.Took) + (waited ? ", guessed ahead and waited for" : string.Empty)
                                         + (r.ServiceMiss != null ? "; your own AI's words were not used: " + r.ServiceMiss : string.Empty)
                    : (why != null ? TextsDirector.Words.From(r.Source) + ": " + why : null) ?? (r != null && r.Why != null ? r.Why : "no words came");
                done(said);
            }

            // The model's words as a stranger would say them, or null (the written line) and why.
            private static string Spoken(StreetChat c, string words, out string why)
            {
                why = null;
                if (StreetCards.Chatty(words))
                {
                    why = "the words speak of chat: \"" + words + "\"";
                    return null;
                }
                string said = StreetCards.Spoken(words, KnowsName(c) ? null : TextsGame.PlayerName);
                if (said == null)
                {
                    why = "the words call the player by a name they were not told: \"" + words + "\"";
                    return null;
                }
                if (Lately.Heard(said))
                {
                    why = "another stranger said the same lately: \"" + said + "\"";
                    return null;
                }
                Lately.Add(said);
                return said;
            }

            // Told in this talk or earlier today, or a face they know as famous or infamous.
            private static bool KnowsName(StreetChat c) => StreetCards.KnowsName(c.Approach.Famed, c.Done[(int)StreetAsk.Name], c.Memo != null && c.Memo.Told);

            private static string Card(TextRequest r) => r == null ? "the model" : r.Intent + (r.Branch != null ? " " + r.Branch : string.Empty);

            private static string Ms(double seconds) => ((int)(seconds * 1000.0)).ToString(System.Globalization.CultureInfo.InvariantCulture) + " ms";

            // The answer to the blue button, asked when it is shown, on a machine where a reply takes more than half the hold.
            private static void Ahead(StreetChat c, StreetLine line, Dictionary<string, string> data, string player)
            {
                try
                {
                    if (c == null || !TextsDirector.Words.Wants(TextUse.People, false) || TextsDirector.Words.Expect < Street.StreetRules.Hold * AheadShare)
                    {
                        return;   // fast enough to ask at the click, within the hold
                    }
                    TextRequest r = RequestV4(c, line, data, words => { }, true, player);
                    if (r != null)
                    {
                        r.Key = AheadKey(c);
                        TextsDirector.Words.Ask(r, TextClock.Now);
                    }
                }
                catch (Exception e)
                {
                    Fault("RLO Texts, street talk", e);
                }
            }

            private static string TakeAhead(StreetChat c)
            {
                try
                {
                    return c != null ? Taken(c, AheadKey(c), "asked ahead when the button was shown") : null;
                }
                catch (Exception e)
                {
                    Fault("RLO Texts, street talk", e);
                    return null;
                }
            }

            private static string AheadKey(StreetChat c) => "sta:" + c.Who.Key;

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
                    return c != null ? Taken(c, GuessKey(c), "guessed while the player looked at them") : null;
                }
                catch (Exception e)
                {
                    Fault("RLO Texts, street talk", e);
                    return null;
                }
            }

            private static string GuessKey(StreetChat c) => "stg:" + c.Who.Key;

            // A guess at the click: its words if written and fit to be said. One the AI service is still writing is left for
            // the line's own ask to wait for.
            private static string Taken(StreetChat c, string key, string how)
            {
                TextsHub hub = TextsDirector.Words;
                if (hub.Writing(key))
                {
                    c.WaitFor = key;   // Ask waits for it as the line: never asked again
                    return null;
                }
                string words = hub.TakeGuess(key, out TextSource from);
                if (words == null)
                {
                    return null;
                }
                string said = Spoken(c, words, out string why);
                c.Heard = hub.From(from) + (said != null ? ", " + how : ": " + why);
                return said;
            }

            private static TextRequest Request(StreetChat c, string intent, string branch, Action<string> done, bool guess)
            {
                return new TextRequest
                {
                    Use = TextUse.People, Intent = intent, Branch = branch, Key = guess ? GuessKey(c) : "st:" + c.Who.Key + ":" + c.Turns, Speaker = "st:" + c.Who.Key,
                    Priority = TextPriority.Now, Seconds = guess ? Street.StreetRules.GuessSeconds : Street.StreetRules.Hold, Guess = guess, Day = TextsGame.Day, Done = done,
                    Spoken = !guess, Started = guess ? null : c.Speaking   // said to the player's face: the line may wait for their AI service
                };
            }

            // The v4 card (v5's for a data 5 pack): a stranger's role, the line's values, the moment and whether they know the
            // player's name (v5's optional facts), the player's last words.
            private static TextRequest RequestV4(StreetChat c, StreetLine line, Dictionary<string, string> data, Action<string> done, bool guess, string next = null)
            {
                bool warm = data == null || !data.TryGetValue("t_thanks", out string thanks) || thanks != "cool";
                string intent = StreetCards.ByData(line, TextsDirector.Words.Data, warm, out string branch, out int lean);
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
                    Copy(data, r.Slots, "district", "district");   // the day's trouble: where it was
                }
                Copy(data, r.Slots, "t_weather", "weather");
                if (TextsGame.Clock(out int weekday, out int hour))
                {
                    string moment = StreetCards.Moment(weekday, hour);
                    if (moment != null)
                    {
                        r.Slots.Set("moment", moment);
                    }
                }
                bool six = TextsDirector.Words.Data >= TextIntents.DataV6;
                if (KnowsName(c))
                {
                    r.Slots.Set("known", "yes");
                }
                else if (six)
                {
                    r.Slots.Set("known", "no");   // data v6 always says it, so the model knows not to use the name
                }
                if (six)
                {
                    Copy(data, r.Slots, "t_what", "what");     // the card's English words, whatever the game's language
                    Copy(data, r.Slots, "t_way", "way");
                    Copy(data, r.Slots, "t_walk", "walk");
                    Copy(data, r.Slots, "t_event", "event");
                    r.Person = TextsMemory.StrangerKey(c);
                    TextsMemory.Fill(r, null);
                }
                History(c, r, HistoryMost, next);
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
                History(c, r, 3, null);
                return r;
            }

            // The last few lines of the conversation, oldest first, with the player's next words last for a line asked ahead.
            // Three lines on v4's cards: tried offline, the answers keep to the conversation and repeat less than with the
            // player's last words alone.
            private static void History(StreetChat c, TextRequest r, int most, string next)
            {
                int extra = next != null ? 1 : 0;
                int from = Math.Max(0, c.History.Count - (most - extra));
                for (int i = from; i < c.History.Count; i++)
                {
                    string h = c.History[i];
                    bool player = h.StartsWith("P:", StringComparison.Ordinal);
                    r.History.Add(new TextSaid(player ? r.Who.Player : c.Who.First, h.Substring(2)));
                }
                if (next != null)
                {
                    r.History.Add(new TextSaid(r.Who.Player, next));
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
