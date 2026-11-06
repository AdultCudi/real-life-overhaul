using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One message of a person's thread as the call reads it (Social/SocialPhone.Thread): the written line's key it shows (for
    /// RLO Texts' words, the key they stand for), its values, the game minute it came, and whether the player wrote it.
    /// </summary>
    public struct SocialThreadLine
    {
        public string Key;
        public Dictionary<string, string> Data;
        public int Minute;
        public bool Player;

        public SocialThreadLine(string key, Dictionary<string, string> data, int minute, bool player)
        {
            Key = key;
            Data = data;
            Minute = minute;
            Player = player;
        }
    }

    /// <summary>What a call back opens on: the subject, the text's key, values, source, game day and minute.</summary>
    public struct SocialFollowPick
    {
        public SocialFollowSubject Subject;
        public string Key;
        public Dictionary<string, string> Data;
        public string Source;
        public int Day;
        public int Minute;
    }

    /// <summary>
    /// When a call picks up what the person just texted about, and when they text the day after an outing. Game-free
    /// (EconomyCheck, CallBackChecks.cs); every answer depends only on what it is given.
    ///
    ///   the record   every text a person of the social life sends lands in their own thread in the game's phone, whoever
    ///                sent it (the social life, the family, life's little moments, travel, RLO Texts' news), with its written
    ///                line's key, its values and its time; RLO Texts' words keep the key they stand for under
    ///                <see cref="KeyField"/>. The thread is read once, when the player calls (<see cref="Pick"/>)
    ///   the subject  of their newest text that has one (SocialFollowCatalogue.Of: a welcome, an invitation, a favour or a
    ///                plan has none), on the day it came or the next (<see cref="WindowDays"/>)
    ///   once         the call opens on it and it is spent: the person keeps the minute of that text
    ///                (SocialPerson.FollowedAt, in the save), and a text of that minute or older is never taken up again,
    ///                so neither hanging up nor loading the save plays it twice
    ///   the day after an outing with them (SocialPerson.AfterDay: kept when it ends, in the save) they text about how
    ///                it went, from <see cref="NextDayFrom"/>, once, through the social life's own texts: their switch, the
    ///                day's cap, the quiet hours and one a person a day. A good one's thanks sent the same day says it
    ///                already; a day later it is let go (<see cref="NextDay"/>)
    ///
    /// COST: on a call, a pass over the newest messages of one thread (the game keeps twenty); once a game hour, a few
    /// comparisons a person.
    /// </summary>
    public static class SocialFollowRules
    {
        public const int WindowDays = 1;          // a text is taken up on a call the day it came and the next game day
        public const string KeyField = "t_key";   // the values of RLO Texts' words keep the written line's key they stand for under this
        public const string WordsKey = "reallife_texts_line";   // RLO Texts' line for a model's words, "{text}" (SocialLinks.WordsKey is this)
        public const int NextDayFrom = 9;         // game hour from which a text about yesterday's outing may come
        // Each feature's keys begin so (written in parts: the catalogue's check reads whole keys only).
        private const string SocialStem = "reallife" + "_social_", TextsStem = "reallife" + "_texts_", FamilyStem = "reallife" + "_family_",
            LifeStem = "reallife" + "_ev_", TravelStem = "reallife" + "_travel_";

        /// <summary>The written line's key a message stands for: for RLO Texts' words, the key kept in its values; else its own.</summary>
        public static string KeyOf(string messageKey, Dictionary<string, string> data)
        {
            if (messageKey == WordsKey && data != null && data.TryGetValue(KeyField, out string written) && !string.IsNullOrEmpty(written))
            {
                return written;
            }
            return messageKey;
        }

        /// <summary>Which feature sent a text, by its key: "the social life", "the family", "life's little moments", "travel"; null for another.</summary>
        public static string Source(string key)
        {
            if (key == null)
            {
                return null;
            }
            if (key.StartsWith(SocialStem, StringComparison.Ordinal) || key.StartsWith(TextsStem, StringComparison.Ordinal))
            {
                return "the social life";   // (RLO Texts' news and gossip are sent by the social life's own texts)
            }
            if (key.StartsWith(FamilyStem, StringComparison.Ordinal))
            {
                return "the family";
            }
            if (key.StartsWith(LifeStem, StringComparison.Ordinal))
            {
                return "life's little moments";
            }
            return key.StartsWith(TravelStem, StringComparison.Ordinal) ? "travel" : null;
        }

        /// <summary>
        /// What a call to somebody opens on: of their thread (oldest first), their newest text that has a subject, if it came
        /// today or the day before and after the last one a call opened on (<paramref name="followedAt"/>, a game minute).
        /// False, and why not in <paramref name="why"/>, when there is none.
        /// </summary>
        public static bool Pick(IReadOnlyList<SocialThreadLine> thread, int followedAt, int today, out SocialFollowPick pick, out string why)
        {
            pick = default;
            if (thread == null || thread.Count == 0)
            {
                why = "nothing in their messages";
                return false;
            }
            string newest = null;
            for (int i = thread.Count - 1; i >= 0; i--)
            {
                SocialThreadLine line = thread[i];
                if (line.Player || line.Key == null)
                {
                    continue;
                }
                newest = newest ?? line.Key;
                SocialFollowSubject subject = SocialFollowCatalogue.Of(line.Key);
                if (subject == null)
                {
                    continue;   // (a welcome, an invitation, a favour, a plan's: not a subject of their own)
                }
                int day = DayOf(line.Minute);
                if (line.Minute <= followedAt)
                {
                    why = "their newest text with a subject (" + line.Key + ", day " + day + ") was taken up on a call already";
                    return false;
                }
                if (today - day > WindowDays || day > today)
                {
                    why = "their newest text with a subject (" + line.Key + ") came on day " + day + ", more than " + WindowDays + " day before today (" + today + ")";
                    return false;
                }
                pick = new SocialFollowPick { Subject = subject, Key = line.Key, Data = line.Data, Source = Source(line.Key), Day = day, Minute = line.Minute };
                why = null;
                return true;
            }
            why = newest == null ? "no text of theirs in their messages" : "none of their texts has a subject (the newest: " + newest + ")";
            return false;
        }

        /// <summary>The game day of a game minute, as the game's Timestamp reads it.</summary>
        public static int DayOf(int minute) => minute < 0 ? -1 : minute / (60 * 24);

        /// <summary>The values a call back's line is filled with: the text's own, then the speaker's where it has none (never a bare placeholder).</summary>
        public static Dictionary<string, string> Filled(Dictionary<string, string> text, Dictionary<string, string> speaker)
        {
            var data = new Dictionary<string, string>(speaker != null ? speaker.Count + 4 : 8, StringComparer.Ordinal);
            if (text != null)
            {
                foreach (KeyValuePair<string, string> pair in text)
                {
                    if (pair.Key != KeyField && pair.Key != "text")
                    {
                        data[pair.Key] = pair.Value;
                    }
                }
            }
            if (speaker != null)
            {
                foreach (KeyValuePair<string, string> pair in speaker)
                {
                    if (!data.ContainsKey(pair.Key))
                    {
                        data[pair.Key] = pair.Value;
                    }
                }
            }
            return data;
        }

        // ---------------------------------------------------------------- the day after an outing

        /// <summary>What happens today with an outing still to be texted about the day after.</summary>
        public enum NextDayStep { None, Text, LetGo }

        /// <summary>
        /// Whether a person texts now about yesterday's outing (<see cref="NextDayStep.Text"/>), waits (None) or lets it go: on
        /// the day after it, from <see cref="NextDayFrom"/>, while they are awake and have neither texted today nor been in
        /// touch today. From the second day after it, let go. (A good outing's thanks the same day lets it go when it is sent:
        /// SocialDirector.)
        /// </summary>
        public static NextDayStep NextDay(int afterDay, int today, int hour, bool asleep, int lastTextDay, int lastContactDay)
        {
            if (afterDay <= 0)
            {
                return NextDayStep.None;
            }
            if (today > afterDay + 1 || today < afterDay)
            {
                return NextDayStep.LetGo;
            }
            if (today == afterDay || hour < NextDayFrom || asleep || lastTextDay == today || lastContactDay == today)
            {
                return NextDayStep.None;
            }
            return NextDayStep.Text;
        }

        /// <summary>The text's lines about yesterday's outing: Mom's, the partner's (a date) or a friend's, as it went.</summary>
        public static string[] NextDayLines(bool family, bool together, bool well)
        {
            if (family)
            {
                return well ? SocialCatalogue.TextNextDayMomGood : SocialCatalogue.TextNextDayMomBad;
            }
            if (together)
            {
                return well ? SocialCatalogue.TextNextDayDateGood : SocialCatalogue.TextNextDayDateBad;
            }
            return well ? SocialCatalogue.TextNextDayGood : SocialCatalogue.TextNextDayBad;
        }

        /// <summary>One of a pool by a hash of the day and the person's seed: the same on every machine, and another another day.</summary>
        public static string Line(string[] pool, int day, int seed)
        {
            if (pool == null || pool.Length == 0)
            {
                return null;
            }
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)day) * 16777619u;
                h = (h ^ (uint)seed) * 16777619u;
                h ^= h >> 13;
                return pool[(int)(h % (uint)pool.Length)];
            }
        }
    }
}
