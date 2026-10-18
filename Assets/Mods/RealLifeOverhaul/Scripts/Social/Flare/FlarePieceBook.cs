using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The host's pictures on their way in, piece by piece, each its own player's. When there are many, a player's own
    /// oldest gives way to its new one, or one with no piece for two minutes; another player's picture still coming is never
    /// cleared for it. Game-free: the caller gives the time.
    /// </summary>
    public sealed class FlarePieceBook
    {
        public const int Most = 8;               // pictures on their way at once
        public const float Stale = 120f;         // real seconds without a piece: its player has gone

        private sealed class Coming
        {
            public string Who;
            public FlareSnapParts Parts;
            public float Last;
        }

        private readonly List<Coming> _coming = new List<Coming>(Most);

        public int Count => _coming.Count;

        /// <summary>A piece of a player's picture. The whole picture's Base64 once its last piece is in, else null (also for a piece refused).</summary>
        public string Add(string who, FlareAsk ask, float now)
        {
            if (string.IsNullOrEmpty(who) || ask.PicId == null || !ask.PicId.StartsWith(who + "/", StringComparison.Ordinal))
            {
                return null;   // a picture is only ever its own player's
            }
            Coming c = null;
            for (int i = 0; i < _coming.Count; i++)
            {
                if (_coming[i].Parts.Id == ask.PicId)
                {
                    c = _coming[i];
                    if (c.Parts.Count != ask.Count)
                    {
                        _coming.RemoveAt(i);   // the same picture sent again in another number of pieces: from the start
                        c = null;
                    }
                    break;
                }
            }
            if (c == null)
            {
                if (_coming.Count >= Most && !MakeRoom(who, now))
                {
                    return null;
                }
                c = new Coming { Who = who, Parts = new FlareSnapParts(ask.PicId, ask.Count) };
                _coming.Add(c);
            }
            c.Last = now;
            if (!c.Parts.Add(ask.Index, ask.Text))
            {
                return null;
            }
            _coming.Remove(c);
            return c.Parts.Bytes() != null ? c.Parts.Text() : string.Empty;   // empty: it came damaged
        }

        private bool MakeRoom(string who, float now)
        {
            int stale = -1;
            for (int i = 0; i < _coming.Count; i++)
            {
                if (_coming[i].Who == who)
                {
                    _coming.RemoveAt(i);   // the oldest of its own: the list is in the order they began
                    return true;
                }
                if (stale < 0 && now - _coming[i].Last > Stale)
                {
                    stale = i;
                }
            }
            if (stale >= 0)
            {
                _coming.RemoveAt(stale);
                return true;
            }
            return false;
        }

        public void Clear()
        {
            _coming.Clear();
        }
    }
}
