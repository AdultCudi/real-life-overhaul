using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The host's bounds on what one player may ask over the last ten minutes of real time and the game's day: posts,
    /// messages, likes and pictures. They guard the host against a flood, never the player's posting: a post a second is
    /// well inside <see cref="PostsWindow"/>. Pictures are four a day; a post past them goes without its picture.
    /// Game-free: the caller gives the time.
    /// </summary>
    public sealed class FlareRateBook
    {
        public const float Window = 600f;        // real seconds
        public const int PostsWindow = 900;      // a player's posts in ten minutes: half as many again as a post a second sends
        public const int DmsWindow = 20, LikesWindow = 80;   // in ten minutes
        public const int PicturesDay = 4;        // a player's pictures a game day: the network's own bound, kept

        /// <summary>What becomes of the picture of a post of the player's in a session today.</summary>
        public enum PictureWay { Send, TellWithout, Without }

        /// <summary>
        /// A picture of the player's for the session: one of the day's four goes (counted); past them the post goes without
        /// it, and the first time today the player is told (TellWithout), never again that day (Without).
        /// </summary>
        public static PictureWay Picture(FlareNet n, int day)
        {
            if (n.PicDay != day)
            {
                n.PicDay = day;
                n.PicsToday = 0;
            }
            if (n.PicsToday < PicturesDay)
            {
                n.PicsToday++;
                return PictureWay.Send;
            }
            if (n.PicsToday == PicturesDay)
            {
                n.PicsToday++;   // (one past the four: told)
                return PictureWay.TellWithout;
            }
            return PictureWay.Without;
        }

        private readonly Dictionary<string, List<float>> _times = new Dictionary<string, List<float>>(16);
        private readonly Dictionary<string, int> _day = new Dictionary<string, int>(16);
        private int _dayOf = -1;

        /// <summary>
        /// True, and counted, if the player may ask this now. A piece of a picture ('X') gives its number: the first piece
        /// counts one of the day's four pictures, and every piece one of the day's pieces.
        /// </summary>
        public bool Allow(string player, char kind, float now, int day, int piece = 0)
        {
            if (string.IsNullOrEmpty(player))
            {
                return false;
            }
            if (day != _dayOf)
            {
                _day.Clear();
                _dayOf = day;
            }
            if (kind == 'X' && piece == 0)
            {
                int pictures;
                string pictureKey = player + "|#";
                _day.TryGetValue(pictureKey, out pictures);
                if (pictures >= PicturesDay || !Allow(player, 'X', now, day, -1))
                {
                    return false;
                }
                _day[pictureKey] = pictures + 1;
                return true;
            }
            int most = kind == 'P' ? PostsWindow : kind == 'D' ? DmsWindow : kind == 'X' ? int.MaxValue : LikesWindow;
            string key = player + "|" + kind;
            List<float> times;
            if (!_times.TryGetValue(key, out times))
            {
                times = new List<float>(8);
                _times[key] = times;
            }
            for (int i = times.Count - 1; i >= 0; i--)
            {
                if (now - times[i] > Window)
                {
                    times.RemoveAt(i);
                }
            }
            int today;
            _day.TryGetValue(key, out today);
            int dayMost = kind == 'X' ? PicturesDay * FlareSnap.PartsMost : int.MaxValue;
            if (times.Count >= most || today >= dayMost)
            {
                return false;
            }
            times.Add(now);
            _day[key] = today + 1;
            return true;
        }

        public void Clear()
        {
            _times.Clear();
            _day.Clear();
        }
    }
}
