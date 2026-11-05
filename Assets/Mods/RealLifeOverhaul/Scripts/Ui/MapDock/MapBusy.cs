using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What a place usually draws, kept so the place card can say "busier than usual": a rolling average of the arrivals the simulation counts for it, for the places
    /// the player has looked at (at most <see cref="Most"/>, the longest unlooked at dropped first). A place is first kept at what it draws that day, so it is never
    /// "busier" on the first look; each game day after, the average moves a quarter of the way to the day's arrivals. Rivals' and the city's shops are counted by the same
    /// simulation, so they get the same average. The line in the save, <c>reallife:map_busy</c>: <c>1;place|usual|day;...</c>. A line this version cannot read is
    /// left as it was read (Core/SaveLines). Game-free.
    /// </summary>
    public sealed class MapBusy
    {
        public const string Key = "reallife:map_busy";
        public const int Most = 64;
        private const string Version = "1";

        private struct Entry
        {
            public string Place;   // Flare's writing of a place ("street~number")
            public int Usual;      // arrivals a day, rolling
            public int Day;        // the last game day it moved on
        }

        private readonly List<Entry> _entries = new List<Entry>(8);

        public int Count => _entries.Count;

        private int IndexOf(string place)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (string.Equals(_entries[i].Place, place, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Starts keeping a place at what it draws now, or notes it was looked at today (the one that waited longest goes first when there are too many).</summary>
        public void Watch(string place, int arrivals, int day)
        {
            if (string.IsNullOrEmpty(place) || place.IndexOfAny(new[] { ';', '|' }) >= 0)
            {
                return;
            }
            int at = IndexOf(place);
            if (at >= 0)
            {
                Entry e = _entries[at];
                e.Day = Math.Max(e.Day, day);
                _entries[at] = e;
                return;
            }
            _entries.Add(new Entry { Place = place, Usual = Math.Max(0, arrivals), Day = day });
            while (_entries.Count > Most)
            {
                int oldest = 0;
                for (int i = 1; i < _entries.Count; i++)
                {
                    oldest = _entries[i].Day < _entries[oldest].Day ? i : oldest;
                }
                _entries.RemoveAt(oldest);
            }
        }

        /// <summary>A day's arrivals for a place the average is kept for: it moves a quarter of the way, once a game day. True when it moved.</summary>
        public bool Move(string place, int arrivals, int day)
        {
            int at = IndexOf(place);
            if (at < 0 || _entries[at].Day >= day)
            {
                return false;
            }
            Entry e = _entries[at];
            e.Usual = (e.Usual * 3 + Math.Max(0, arrivals) + 2) / 4;
            e.Day = day;
            _entries[at] = e;
            return true;
        }

        /// <summary>The places kept, for the day's turn to move each: their keys, in the list's order.</summary>
        public string PlaceAt(int index) => _entries[index].Place;

        /// <summary>What a place usually draws, or false when it is not kept.</summary>
        public bool Usual(string place, out int usual)
        {
            int at = IndexOf(place);
            usual = at >= 0 ? _entries[at].Usual : 0;
            return at >= 0;
        }

        public string Write()
        {
            if (_entries.Count == 0)
            {
                return null;
            }
            var w = new StringBuilder(16 + _entries.Count * 28);
            w.Append(Version);
            for (int i = 0; i < _entries.Count; i++)
            {
                w.Append(';').Append(_entries[i].Place).Append('|').Append(_entries[i].Usual.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(_entries[i].Day.ToString(CultureInfo.InvariantCulture));
            }
            return w.ToString();
        }

        /// <summary>True when a line is one this version reads: none, or one of its own version.</summary>
        public static bool Readable(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return true;
            }
            int cut = line.IndexOf(';');
            return (cut < 0 ? line : line.Substring(0, cut)) == Version;
        }

        /// <summary>The places a line holds; one that cannot be read is left out, a line of another version gives none.</summary>
        public static MapBusy Read(string line)
        {
            var busy = new MapBusy();
            if (!Readable(line) || string.IsNullOrEmpty(line))
            {
                return busy;
            }
            string[] parts = line.Split(';');
            for (int i = 1; i < parts.Length && busy._entries.Count < Most; i++)
            {
                // "place|usual|day": the place is Flare's own writing of one ("street~number", the street escaped), so it never carries a separator.
                string[] one = parts[i].Split('|');
                if (one.Length == 3 && one[0].Length > 0 && int.TryParse(one[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int usual)
                    && int.TryParse(one[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int day)
                    && busy.IndexOf(one[0]) < 0)
                {
                    busy._entries.Add(new Entry { Place = one[0], Usual = Math.Max(0, usual), Day = day });
                }
            }
            return busy;
        }
    }

    /// <summary>One review the Flare city holds for a business, as the card reads it.</summary>
    public struct MapFlareReview
    {
        public int Person;
        public bool Female;
        public int Stars;          // 1 to 5
        public int Day;
        public string Name;        // the resident as a review signs them
    }

    /// <summary>What Flare in the city knows of a business, for its place card: its rating in tenths, how many have reviewed it, its price index, its arrivals and the newest reviews.</summary>
    public struct MapFlareFacts
    {
        public bool Found;
        public int Tenths;         // the rating in tenths of a star, 0 for none yet
        public int ReviewCount;
        public int Price;          // the business's prices against the city's, in thousandths
        public int Arrivals;       // what the simulation counts coming to it a day
        public int Why;            // FlareReviewWhy: what a reviewer would weigh most now (0 mixed, 1 price, 2 quality, 3 wait, 4 clean)
        public MapFlareReview[] Newest;
    }

    /// <summary>
    /// What the map's panels ask of other features, wired where the features meet (Scripts/Wiring/MapLinks): Flare in the city's facts about a business by its place, and the
    /// game's day. Left null with the feature off, the card then shows only what the building itself says.
    /// </summary>
    public static class MapBridges
    {
        /// <summary>Flare's facts for a place ("street~number", as Flare writes one).</summary>
        public static Func<string, MapFlareFacts> Flare;
    }
}
