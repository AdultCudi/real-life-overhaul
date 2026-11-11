using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What a day of the people part asks of the rest of the mod about a place of work: the comfort of its office, the burnout its team meeting pushed, and
    /// how hard the rivals pull at its people. They are the same for everyone who works at the place, so each is asked once for a place and a day and kept, not
    /// once for each of the thousands of professionals on file (the day was a bridge call, and a pass over the market and the rivals, for every one of them).
    /// The pull is asked only when somebody there can be approached. Game-free; a dictionary look for each professional, the bridges once for each place.
    /// </summary>
    public sealed class PlaceDays
    {
        /// <summary>One place for one day.</summary>
        public sealed class Entry
        {
            public int Day = -1;
            public int Comfort, Pushed;
            public int Pull = -1;   // thousandths, -1 until somebody there can be approached
        }

        /// <summary>The team meeting's week at a place on a day, as the tech bridges give it (extra output and the burnout pushed, thousandths).</summary>
        public delegate void TeamWeek(string place, int day, out int output, out int burnout);

        private readonly Dictionary<string, Entry> _places = new Dictionary<string, Entry>(64, StringComparer.Ordinal);
        private readonly Func<string, int> _comfort;
        private readonly TeamWeek _team;
        private readonly Func<string, int, int> _pull;

        /// <summary>Calls made to the bridges since this was made: a place's comfort and team week count one each, a pull one; held offline to the places, not the professionals.</summary>
        public int Asked;

        public PlaceDays(Func<string, int> comfort, TeamWeek team, Func<string, int, int> pull)
        {
            _comfort = comfort;
            _team = team;
            _pull = pull;
        }

        /// <summary>The place on the day: its comfort and team week asked if this is the first look today.</summary>
        public Entry Of(string place, int day)
        {
            string key = place ?? string.Empty;
            if (!_places.TryGetValue(key, out Entry e))
            {
                e = new Entry();
                _places[key] = e;
            }
            if (e.Day != day)
            {
                e.Day = day;
                e.Comfort = _comfort(key);
                _team(key, day, out _, out e.Pushed);
                e.Pull = -1;
                Asked += 2;
            }
            return e;
        }

        /// <summary>The rivals' pull at the place, thousandths, asked the first time somebody there can be approached today.</summary>
        public int PullAt(Entry e, string place, int day)
        {
            if (e.Pull < 0)
            {
                e.Pull = Math.Max(0, _pull(place ?? string.Empty, day));
                Asked++;
            }
            return e.Pull;
        }

        /// <summary>Everything forgotten (the people part stopped).</summary>
        public void Clear()
        {
            _places.Clear();
        }
    }
}
