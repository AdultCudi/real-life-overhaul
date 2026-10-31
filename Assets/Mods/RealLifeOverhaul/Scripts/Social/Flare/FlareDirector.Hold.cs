using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Texts;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare's director, the hold (FlareWordsHold): with a model present, a post, an answer to the player's message or a
    /// friend's reply under the player's post whose words are asked is shown only with them. Due without its words, it is
    /// held back past the look ahead and asked again; it appears the minute its words come, so the feed and the threads stay
    /// in the order they are read. The written line only when the model is gone or has truly failed it, counted with the
    /// reason. The session's shared feed is never held: the host asks as before and the guests show what it carries.
    /// COST: once a real second, a pass over the posts and messages and over the held items, a dictionary look each; nothing per frame.
    /// </summary>
    public sealed partial class FlareDirector
    {
        private readonly FlareWordsHold _wordsHold = new FlareWordsHold();
        private readonly List<FlareHeld> _holdNext = new List<FlareHeld>(FlareWordsHold.AskingMost);
        private readonly List<FlareHeld> _holdDropped = new List<FlareHeld>(4);
        private long _holdCovered = -1;   // game minute the last look covered: anything due by then may have been shown, and is never held back
        private long _holdAhead;          // ...and the minute up to which posts and messages were looked at: each is looked at once, as it comes within the look ahead
        private int _holdIds, _holdDms;   // the file's next post and message numbers at the last look: one made since was never shown

        /// <summary>True while a friend's reply under the player's post is held for its words (then it is not shown).</summary>
        public bool FriendHeld(int post, int index) => _wordsHold.Holds(FriendKey(post, index));

        // The second's look.
        private void WordsHoldSecond()
        {
            if (FlareBridges.TextAsk == null || FlareBridges.TextWants == null || _file == null || Save == null)
            {
                return;
            }
            long now = Now;
            double real = TextClock.Now;
            if (_holdCovered < 0)
            {
                _holdCovered = _holdAhead = now;   // the first look of a file: what is up already stays as it is
                _holdIds = _file.NextId;
                _holdDms = _file.Net.NextDm;
            }
            HoldNew(now, real);
            HoldTend(now, real);
            HoldAsk(now, real);
            _holdCovered = now;
            _holdAhead = System.Math.Max(_holdAhead, now + FlareWordsHold.AheadMinutes);
            _holdIds = _file.NextId;
            _holdDms = _file.Net.NextDm;
        }

        // Posts and answers come within the look ahead since the last look (or made since, never shown), their words not kept: held.
        private void HoldNew(long now, double real)
        {
            long ahead = now + FlareWordsHold.AheadMinutes;
            bool hostShares = Settings.FlareSharePosts;
            for (int i = 0; i < _file.Posts.Count; i++)
            {
                FlarePost p = _file.Posts[i];
                if (p.At > ahead || (p.At <= _holdAhead && p.Id < _holdIds) || p.Text != null || p.Shared || p.Author == FlareAuthor.Player)
                {
                    continue;
                }
                string key = PostKey(p);
                if (_wordsHold.Holds(key) || (FlareBridges.TextKept != null && FlareBridges.TextKept(key) != null)
                    || (GoingPublic.InSession && FlareNetShare.IsShared(_file, p, hostShares)))
                {
                    continue;   // worded already, or the session's shared feed: shown as it comes
                }
                TextRequest r = PostRequest(p, false);
                if (r == null || !FlareBridges.TextWants(r.Use, false))
                {
                    continue;   // no card, or no model for it: the written line as ever
                }
                FlarePost parent = p.ReplyTo >= 0 ? _file.Post(p.ReplyTo) : null;
                FlareHoldKind kind = parent != null && parent.Author == FlareAuthor.Player ? FlareHoldKind.Reply : FlareHoldKind.Post;
                bool chatter = p.Author == FlareAuthor.Citizen && p.Source == FlareSource.CityTalk;
                FlareHeld h = _wordsHold.Hold(key, kind, r.Intent, p.At, real, chatter, p, _holdDropped);
                if (h != null)
                {
                    h.Use = r.Use;
                    h.Asking = _textAsked.Waiting(p.Id);   // asked ahead already: its request may still come
                    h.AskKey = key;
                }
                else
                {
                    Drop(p);   // an everyday post past the bound: never shown
                }
            }
            DropHeld();
            for (int i = 0; i < _file.Net.Dms.Count; i++)
            {
                FlareDm m = _file.Net.Dms[i];
                if (m.Mine || m.Text != null || m.At > ahead || m.At <= _holdCovered || (m.At <= _holdAhead && m.Id < _holdDms) || _wordsHold.Holds(DmHoldKey(m)))
                {
                    continue;
                }
                string key = FlareTextsMap.DmKey(m);
                if (FlareBridges.TextKept != null && FlareBridges.TextKept(key) != null)
                {
                    continue;
                }
                FlareDm asked = FlareTextsMap.DmAsked(_file.Net.Dms, m);
                TextRequest r = FlareTextsMap.DmWants(m, _holdCovered, TextData) ? DmRequest(m, asked != null ? asked.Text : null) : null;
                if (r == null || !FlareBridges.TextWants(r.Use, false))
                {
                    continue;
                }
                FlareHeld h = _wordsHold.Hold(DmHoldKey(m), FlareHoldKind.Message, r.Intent, m.At, real, false, m, null);
                h.Use = r.Use;
                h.Asking = _textDms.Waiting(key);
                h.AskKey = key;
            }
        }

        // A friend's reply under the player's post, asked as it is posted: held from then, its wait counted from its time.
        private void HoldFriendReply(FlarePost p, int index, long at, TextRequest r)
        {
            if (FlareBridges.TextWants == null || !FlareBridges.TextWants(r.Use, false) || (FlareBridges.TextKept != null && FlareBridges.TextKept(r.Key) != null))
            {
                return;
            }
            FlareHeld h = _wordsHold.Hold(r.Key, FlareHoldKind.Reply, r.Intent, at, double.NaN, false, p, null);
            h.Use = r.Use;
            h.Slot = index;
        }

        // Each held item: shown with its words, written after all, or kept back a little longer.
        private void HoldTend(long now, double real)
        {
            IReadOnlyList<FlareHeld> items = _wordsHold.Items;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                FlareHeld h = items[i];
                if (!Present(h))
                {
                    _wordsHold.Let(h, true);   // its post or message is gone: nothing to show
                    continue;
                }
                bool due = h.Planned <= now + FlareWordsHold.AheadMinutes;
                if (double.IsNaN(h.Since))
                {
                    if (!due)
                    {
                        continue;   // a friend's reply still to come: asked, not yet waited for
                    }
                    h.Since = real;
                }
                bool worded = FlareBridges.TextKept != null && FlareBridges.TextKept(h.AskKey ?? h.Key) != null;
                TextShownWhy away = FlareBridges.TextWhyNot != null ? FlareBridges.TextWhyNot(h.Use) : TextShownWhy.Model;
                FlareHoldStep step = FlareWordsHold.Step(h, worded, away == TextShownWhy.Model, real);
                if (step == FlareHoldStep.Show || step == FlareHoldStep.Written)
                {
                    if (step == FlareHoldStep.Written)
                    {
                        TextShownWhy why = FlareWordsHold.Why(h, away, real);
                        FlareBridges.TextWritten?.Invoke(h.Intent, why);
                        if (Meta.Dev.Verbose)
                        {
                            Logger?.Info("Flare check (held words): " + h.Intent + " (" + h.Key + ") shows its written line after all: " + TextShown.Name(why) + ", held "
                                         + (real - h.Since).ToString("0", CultureInfo.InvariantCulture) + " s of " + FlareWordsHold.Cap(h.Kind).ToString("0", CultureInfo.InvariantCulture)
                                         + " s, " + h.Failures + " failure(s).");
                        }
                    }
                    Release(h, now, step == FlareHoldStep.Show);
                    _wordsHold.Let(h, step == FlareHoldStep.Show);
                    continue;
                }
                Push(h, now);
            }
        }

        // The held items asked next: a few at a time, messages first.
        private void HoldAsk(long now, double real)
        {
            _wordsHold.Next(_holdNext);
            for (int i = 0; i < _holdNext.Count; i++)
            {
                FlareHeld h = _holdNext[i];
                if (double.IsNaN(h.Since))
                {
                    continue;
                }
                double seconds = System.Math.Max(TextSpeed.AheadSeconds, FlareWordsHold.Cap(h.Kind) - (real - h.Since));   // never so short a slow machine would not take it
                if (h.Item is FlarePost p && h.Slot < 0)
                {
                    bool shared = GoingPublic.InSession && FlareNetShare.IsShared(_file, p, Settings.FlareSharePosts);
                    TextRequest r = shared ? null : PostRequest(p, false);
                    if (r == null)
                    {
                        FlareWordsHold.Asked(h, false, h.Key, TextShownWhy.NoCard);
                        continue;
                    }
                    AskPost(p, r, false, seconds);
                }
                else if (h.Item is FlarePost mine)
                {
                    TextRequest r = FriendReplyAgain(mine, h.Slot);
                    if (r == null)
                    {
                        FlareWordsHold.Asked(h, false, h.Key, TextShownWhy.NoCard);
                        continue;
                    }
                    AskFriendReply(r, h.Planned, seconds);
                }
                else if (h.Item is FlareDm m)
                {
                    FlareDm asked = FlareTextsMap.DmAsked(_file.Net.Dms, m);
                    TextRequest r = DmRequest(m, asked != null ? asked.Text : null);
                    if (r == null)
                    {
                        FlareWordsHold.Asked(h, false, h.Key, TextShownWhy.NoCard);
                        continue;
                    }
                    AskDm(m, r, seconds);
                }
            }
        }

        // A friend's reply's card again, from its post's reactions; null when the friend or the reaction is gone.
        private TextRequest FriendReplyAgain(FlarePost p, int index)
        {
            string kind = FlareTextsMap.PostKind(p.Source);
            if (kind == null || string.IsNullOrEmpty(p.Reactors) || !Person(index, out FlarePerson person))
            {
                return null;
            }
            return FriendReplyRequest(p, index, person, kind);
        }

        // Whether a held item's post or message is still the file's.
        private bool Present(FlareHeld h)
        {
            if (h.Item is FlarePost p)
            {
                return _file.Post(p.Id) == p;
            }
            return h.Item is FlareDm m && _file.Net.Dms.Contains(m);
        }

        // A held post or message kept past the look ahead; a friend's reply needs nothing (the thread leaves it out).
        private static void Push(FlareHeld h, long now)
        {
            if (h.Slot >= 0)
            {
                return;
            }
            if (h.Item is FlarePost p && p.At <= now + FlareWordsHold.AheadMinutes)
            {
                FlareLiveRules.SetAt(p, FlareWordsHold.PushedAt(p.At, now));
            }
            else if (h.Item is FlareDm m && m.At <= now + FlareWordsHold.AheadMinutes)
            {
                m.At = FlareWordsHold.PushedAt(m.At, now);
            }
        }

        // A held item lets go: up at its own time, or the minute its words came; a message's words follow it to its new key.
        private static void Release(FlareHeld h, long now, bool worded)
        {
            long at = FlareWordsHold.ShownAt(h, now);
            if (h.Slot >= 0)
            {
                return;   // a friend's reply keeps its place in the thread: the thread draws it once its words are kept
            }
            if (h.Item is FlarePost p)
            {
                FlareLiveRules.SetAt(p, at);
            }
            else if (h.Item is FlareDm m)
            {
                m.At = at;
                string key = FlareTextsMap.DmKey(m);
                if (worded && h.AskKey != null && h.AskKey != key)
                {
                    FlareBridges.TextMove?.Invoke(h.AskKey, key);
                }
            }
        }

        // A request of the hold's, or of an item it holds, ended: the hold hears how.
        private void HoldEnded(string key, bool words, TextShownWhy why)
        {
            FlareHeld h = _wordsHold.Of(key);
            if (h != null)
            {
                FlareWordsHold.Ended(h, words, why);
            }
        }

        private void HoldAsked(string key, bool asked, string askKey, TextShownWhy why)
        {
            FlareHeld h = _wordsHold.Of(key);
            if (h != null)
            {
                FlareWordsHold.Asked(h, asked, askKey, why);
            }
        }

        // The everyday posts the bound let go: never shown, so taken out of the feed.
        private void DropHeld()
        {
            for (int i = 0; i < _holdDropped.Count; i++)
            {
                if (_holdDropped[i].Item is FlarePost p)
                {
                    Drop(p);
                }
            }
            if (_holdDropped.Count > 0 && Meta.Dev.Verbose)
            {
                Logger?.Info("Flare check (held words): " + _holdDropped.Count + " everyday post(s) never shown were let go, " + FlareWordsHold.Most + " being held: PASS.");
            }
            _holdDropped.Clear();
        }

        private void Drop(FlarePost p)
        {
            int i = _file.Posts.IndexOf(p);
            if (i >= 0 && p.At > Now)
            {
                _file.Posts.RemoveAt(i);
            }
        }

        private void HoldForget()
        {
            _wordsHold.Clear();
            _holdCovered = -1;
        }
    }
}
