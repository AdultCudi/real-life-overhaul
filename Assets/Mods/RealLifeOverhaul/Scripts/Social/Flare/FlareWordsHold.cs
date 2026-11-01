using System;
using System.Collections.Generic;
using RealLifeOverhaul.Texts;

namespace RealLifeOverhaul.Flare
{
    /// <summary>What a held Flare item is, for how long it may wait and how soon it is asked.</summary>
    public enum FlareHoldKind
    {
        Message = 0,   // the city's answer to a message of the player's: read in an open thread, so it waits least
        Reply = 1,     // an answer under the player's own post: a friend's reply, a city person's or an account's
        Post = 2       // a post of the city's feed: nobody waits for it
    }

    /// <summary>What the hold does with an item at a look.</summary>
    public enum FlareHoldStep
    {
        Wait,      // its words are being written: it stays hidden
        Ask,       // no request waits for it: ask (again)
        Show,      // its words came: it appears with them
        Written    // the model has truly failed it, or is gone: it appears with its written line, counted with the reason
    }

    /// <summary>One Flare item held back until its words come. Runtime only; never saved or sent.</summary>
    public sealed class FlareHeld
    {
        public string Key;              // the words' key ("fp:12", "fd:c:4:9000", "fr:12:3")
        public FlareHoldKind Kind;
        public string Intent;           // what its written line is counted under
        public long Planned;            // game minutes: when it was to appear
        public double Since = double.NaN;   // real seconds (TextClock) it was first held
        public bool Asking;             // a request for it waits
        public string AskKey;           // the key the waiting request keeps its words under (a message's moves with its minute)
        public int Failures;            // requests that ended refused by the checks or failed by the model
        public TextShownWhy Last = TextShownWhy.Slow;   // the reason of the last request that ended without words
        public bool Final;              // the model cannot word it at all (no card, the language, switched off): its written line now
        public bool Chatter;            // a city person's everyday post: the one kind the hold may drop when it holds too many
        public TextUse Use;             // the kind of message it is, for whether a model is there for it
        public object Item;             // the director's own: the post, the message or the post a friend replies under
        public int Slot = -1;           // ...and for a friend's reply, the friend's number
    }

    /// <summary>
    /// Flare with a model present (1 Nov 2026: Flare is written only by a model when one is present, else by its written
    /// lines): an item whose words are asked appears only with them, held back until they come. The
    /// written line only when the model has truly failed it: refused or failed again after one retry, or kept waiting past
    /// its kind's cap, which is real seconds the model had (game time runs on in fast forward, when the background is not
    /// written at all). Game-free. COST: a pass over the held items, a few dozen, once a real second.
    /// </summary>
    public sealed class FlareWordsHold
    {
        public const double MessageCap = 90.0;   // real seconds an answer to the player's message may wait: they may be reading the thread, and a model on a
                                                 // processor answers a message due soon within half a minute even behind other work
        public const double ReplyCap = 180.0;    // ...an answer under the player's post: they come back to it, and three minutes is past any busy spell
        public const double PostCap = 900.0;     // ...a post of the city's: nobody waits for it, as for a comment (FlareDirector.CommentSeconds)
        public const int RetryMost = 1;          // retries of an item the checks refused or the model failed, then its written line
        public const int Most = 96;              // items held at once: past it the oldest everyday post is dropped, never one of the player's own
        public const int AskingMost = 12;        // held items asked at once: a quarter of the queue (TextQueue.Most 48), with comments' 8 beside them
        public const long AheadMinutes = 30;     // game minutes ahead an item due is held, and pushed past while held: more than fast forward
                                                 // moves the clock in the second between two looks

        private readonly List<FlareHeld> _held = new List<FlareHeld>(Most);
        private readonly Dictionary<string, FlareHeld> _byKey = new Dictionary<string, FlareHeld>(Most, StringComparer.Ordinal);

        public IReadOnlyList<FlareHeld> Items => _held;

        public int Count => _held.Count;

        /// <summary>This session: items that appeared with the model's words after being held, written after all, and everyday posts dropped.</summary>
        public int Worded { get; private set; }
        public int Written { get; private set; }
        public int Dropped { get; private set; }

        public static double Cap(FlareHoldKind kind) => kind == FlareHoldKind.Message ? MessageCap : kind == FlareHoldKind.Reply ? ReplyCap : PostCap;

        /// <summary>The held item of a key; null when it is not held.</summary>
        public FlareHeld Of(string key) => key != null && _byKey.TryGetValue(key, out FlareHeld h) ? h : null;

        public bool Holds(string key) => key != null && _byKey.ContainsKey(key);

        /// <summary>
        /// Holds an item; the held one when it is held already. With <see cref="Most"/> held, the oldest everyday post is let go
        /// into <paramref name="dropped"/> (the director takes it out of the feed: it was never shown); null when none can make
        /// room and the new item is an everyday post itself, which is then dropped.
        /// </summary>
        public FlareHeld Hold(string key, FlareHoldKind kind, string intent, long planned, double now, bool chatter, object item, List<FlareHeld> dropped)
        {
            if (key == null)
            {
                return null;
            }
            if (_byKey.TryGetValue(key, out FlareHeld held))
            {
                return held;
            }
            if (_held.Count >= Most)
            {
                int oldest = -1;
                for (int i = 0; i < _held.Count; i++)
                {
                    if (_held[i].Chatter && (oldest < 0 || _held[i].Planned < _held[oldest].Planned))
                    {
                        oldest = i;
                    }
                }
                if (oldest >= 0 && (!chatter || _held[oldest].Planned <= planned))
                {
                    dropped?.Add(_held[oldest]);
                    Remove(oldest);
                    Dropped++;
                }
                else if (chatter)
                {
                    Dropped++;
                    return null;
                }
            }
            held = new FlareHeld { Key = key, Kind = kind, Intent = intent, Planned = planned, Since = now, Chatter = chatter, Item = item };
            _held.Add(held);
            _byKey[key] = held;
            return held;
        }

        /// <summary>What to do with an item now: its words kept, whether the model is there for it, the real clock.</summary>
        public static FlareHoldStep Step(FlareHeld h, bool worded, bool modelThere, double now)
        {
            if (worded)
            {
                return FlareHoldStep.Show;
            }
            if (!modelThere || h.Final || h.Failures > RetryMost || now - h.Since >= Cap(h.Kind))
            {
                return FlareHoldStep.Written;
            }
            return h.Asking ? FlareHoldStep.Wait : FlareHoldStep.Ask;
        }

        /// <summary>
        /// The reason its written line is counted under: the model's absence as the hub names it, else what made the model
        /// fail it, else slow (the cap).
        /// </summary>
        public static TextShownWhy Why(FlareHeld h, TextShownWhy notThere, double now)
        {
            if (notThere != TextShownWhy.Model)
            {
                return notThere;
            }
            if (h.Final || h.Failures > RetryMost)
            {
                return h.Last;
            }
            return TextShownWhy.Slow;
        }

        /// <summary>A request for it was made: true when it was, false when the hub did not take it (then its reason).</summary>
        public static void Asked(FlareHeld h, bool asked, string askKey, TextShownWhy why)
        {
            h.Asking = asked;
            h.AskKey = askKey;
            if (!asked)
            {
                Ended(h, false, why);
            }
        }

        /// <summary>
        /// A request for it ended. Refused or failed counts toward its retry; no card, another language, switched off or no pack
        /// are final; a time running out, a full queue or the model still loading only ask again.
        /// </summary>
        public static void Ended(FlareHeld h, bool words, TextShownWhy why)
        {
            h.Asking = false;
            if (words)
            {
                return;
            }
            switch (why)
            {
                case TextShownWhy.Refused:
                case TextShownWhy.Failed:
                    h.Failures++;
                    h.Last = why;
                    break;
                case TextShownWhy.NoCard:
                case TextShownWhy.Language:
                case TextShownWhy.Off:
                case TextShownWhy.NoPack:
                    h.Final = true;
                    h.Last = why;
                    break;
            }
        }

        /// <summary>When an item appears: its own time if its words came before it, else the minute they came.</summary>
        public static long ShownAt(FlareHeld h, long nowGame) => Math.Max(h.Planned, nowGame);

        /// <summary>The time a held item is pushed to while it waits: past the look ahead, so it is never shown before its words.</summary>
        public static long PushedAt(long current, long nowGame) => Math.Max(current, nowGame + AheadMinutes + 1);

        /// <summary>Lets an item go, as worded or written; counted.</summary>
        public void Let(FlareHeld h, bool worded)
        {
            int i = _held.IndexOf(h);
            if (i < 0)
            {
                return;
            }
            Remove(i);
            if (worded)
            {
                Worded++;
            }
            else
            {
                Written++;
            }
        }

        /// <summary>The held items to ask next, at most <see cref="AskingMost"/> asking at once: messages first, then answers under the player's posts, then the feed by its time.</summary>
        public void Next(List<FlareHeld> into)
        {
            into.Clear();
            int asking = 0;
            for (int i = 0; i < _held.Count; i++)
            {
                asking += _held[i].Asking ? 1 : 0;
            }
            for (int k = 0; k < 3 && asking + into.Count < AskingMost; k++)
            {
                int start = into.Count;
                for (int i = 0; i < _held.Count; i++)
                {
                    FlareHeld h = _held[i];
                    if ((int)h.Kind == k && !h.Asking && !h.Final && h.Failures <= RetryMost)
                    {
                        into.Add(h);
                    }
                }
                for (int a = start + 1; a < into.Count; a++)
                {
                    for (int b = a; b > start && into[b].Planned < into[b - 1].Planned; b--)
                    {
                        FlareHeld t = into[b];
                        into[b] = into[b - 1];
                        into[b - 1] = t;
                    }
                }
            }
            if (into.Count > AskingMost - asking)
            {
                into.RemoveRange(Math.Max(0, AskingMost - asking), into.Count - Math.Max(0, AskingMost - asking));
            }
        }

        public void Clear()
        {
            _held.Clear();
            _byKey.Clear();
        }

        private void Remove(int i)
        {
            _byKey.Remove(_held[i].Key);
            _held.RemoveAt(i);
        }
    }
}
