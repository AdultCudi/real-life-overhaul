using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// A guest's lines for the host, waiting for the next second, in the order asked: each second takes what a request
    /// may carry, one piece of a picture and one post (many posts at once go a second apart, inside
    /// <see cref="FlareRateBook.PostsWindow"/>). A post is never dropped: past <see cref="Most"/> the oldest other line
    /// goes. Game-free.
    /// </summary>
    public sealed class FlareOutbox
    {
        public const int Most = 400;             // other lines kept waiting at most (a guest offline for long)
        public const int PostsEach = 1;          // the player's posts sent a second: many at once go a second apart
        private readonly List<string> _lines = new List<string>(16);
        private int _others;                     // the lines waiting that are not posts

        public int Count => _lines.Count;

        public void Add(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }
            _lines.Add(line);
            if (IsPost(line))
            {
                return;   // a post is never dropped
            }
            _others++;
            for (int i = 0; _others > Most && i < _lines.Count; i++)
            {
                if (!IsPost(_lines[i]))
                {
                    _lines.RemoveAt(i);   // the oldest other ask
                    _others--;
                    break;
                }
            }
        }

        private static bool IsPost(string line)
        {
            return line.Length > 0 && line[0] == 'P';
        }

        /// <summary>
        /// Hands this second's lines to <paramref name="ask"/>: at most <paramref name="room"/>, one piece of a picture and one
        /// post. A line it refuses stays, with all after it, for the next second. How many went.
        /// </summary>
        public int Send(Func<string, bool> ask, int room)
        {
            int sent = 0, posts = 0;
            bool piece = false;
            for (int i = 0; i < _lines.Count && sent < room;)
            {
                string line = _lines[i];
                if (line.Length > 0 && line[0] == 'X')
                {
                    if (piece)
                    {
                        i++;
                        continue;   // one piece a second: the rest wait, in order
                    }
                    piece = true;
                }
                else if (IsPost(line))
                {
                    if (posts >= PostsEach)
                    {
                        i++;
                        continue;   // a post a second: the rest wait, in order
                    }
                    posts++;
                }
                if (!ask(line))
                {
                    break;
                }
                _lines.RemoveAt(i);
                _others -= IsPost(line) ? 0 : 1;
                sent++;
            }
            return sent;
        }

        public void Clear()
        {
            _lines.Clear();
            _others = 0;
        }
    }
}
