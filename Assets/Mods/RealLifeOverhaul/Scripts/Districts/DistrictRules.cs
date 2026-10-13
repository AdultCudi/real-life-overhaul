using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Districts
{
    /// <summary>The layers of the district view, in the order of their rows in the map's filter list.</summary>
    public enum DistrictLayer
    {
        Wealth = 0,       // the district's income level (the city's economy), from the game's class shares when that is off
        Rent = 1,         // what shops, offices and warehouses are let for, a square metre a day
        Property = 2,     // what a building is worth, a square metre
        Footfall = 3,     // the street traffic past its doors and what its customers want
        Crime = 4,        // crimes reported in the district over the last four weeks
        Standing = 5,     // what the district thinks of the player as an owner
        Supply = 6,       // power and water at the player's places there
        Internet = 7,     // the providers that reach it, today's outages, the player's own fibre
        Competition = 8,  // the businesses open there
        Council = 9       // street parking and roadworks, and how it voted
    }

    /// <summary>Which end of a layer's scale is the good one: it sets the colours.</summary>
    public enum LayerSense
    {
        Neutral = 0,    // more is neither better nor worse (a rent is a cost to a tenant and an income to a landlord): one hue, light to deep
        HighGood = 1,   // red at the bottom, green at the top
        LowGood = 2     // green at the bottom, red at the top
    }

    /// <summary>A colour of the view, in bytes (game-free: the view makes the engine's colour from it).</summary>
    public struct Tint
    {
        public byte R, G, B, A;

        public Tint(byte r, byte g, byte b, byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public string Hex => R.ToString("X2", CultureInfo.InvariantCulture) + G.ToString("X2", CultureInfo.InvariantCulture) + B.ToString("X2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The district view's scales: which of five bands a district's figure falls in, the band's colour and the range
    /// the legend gives for it. Plain arithmetic, no game types; checked offline (EconomyCheck, DistrictChecks).
    ///
    /// Two kinds of scale:
    ///   fixed      the figure means the same in every city: an income level (100 is the city's middle), crimes in
    ///              four weeks, an opinion (50 is where every district starts), the share of the player's places
    ///              supplied, the number of providers. Its four edges are below.
    ///   the city's own  money and counts, which grow with prices and differ from one city to the next: rents, values,
    ///              street traffic, businesses, parking. The five bands split the range from the district lowest on
    ///              it to the highest into five equal steps, so the legend reads "the lowest in the city" to "the
    ///              highest". With every district alike, all are in the middle band.
    /// A district with no figure (none of the player's places there, nothing reported) is <see cref="None"/>: grey.
    /// </summary>
    public static class DistrictRules
    {
        public const int Layers = 10;
        public const int Bands = 5;
        public const int None = -1;                // a district with no figure on a layer
        public const int CrimeDays = 28;           // the crime layer counts this many days back, today included
        public const int LogMost = 400;            // reports kept in the save at most (the oldest go first)
        public const int ZoneAlpha = 120;          // the game's own zones are drawn at 0.47 (NeighborhoodZone's colour): the same here

        /// <summary>The layers' ids: the end of their locale keys and of their rows' names.</summary>
        public static readonly string[] Ids = { "wealth", "rent", "property", "footfall", "crime", "standing", "supply", "internet", "competition", "council" };

        // The fixed scales' edges: a figure at or over an edge is in the band above it.
        public static readonly int[] WealthEdges = { 88, 96, 104, 112 };     // the income level, 60 to 140; 100 the middle
        public static readonly int[] CrimeEdges = { 1, 3, 6, 10 };           // reports in CrimeDays
        public static readonly int[] StandingEdges = { 35, 45, 56, 66 };     // opinion, 0 to 100; 50 where a district starts
        public static readonly int[] SupplyEdges = { 25, 50, 75, 100 };      // per cent of the player's power and water accounts there that are supplied
        public static readonly int[] InternetEdges = { 1, 2, 3, 4 };         // providers that reach it, the player's own company included

        public static LayerSense Sense(DistrictLayer layer)
        {
            switch (layer)
            {
                case DistrictLayer.Crime:
                    return LayerSense.LowGood;
                case DistrictLayer.Standing:
                case DistrictLayer.Supply:
                case DistrictLayer.Internet:
                case DistrictLayer.Footfall:
                    return LayerSense.HighGood;
                default:
                    return LayerSense.Neutral;
            }
        }

        /// <summary>True for a layer whose bands are the city's own range (money and counts); false for a fixed scale.</summary>
        public static bool Relative(DistrictLayer layer)
        {
            return FixedEdges(layer) == null;
        }

        /// <summary>A fixed layer's four edges; null for one on the city's own range.</summary>
        public static int[] FixedEdges(DistrictLayer layer)
        {
            switch (layer)
            {
                case DistrictLayer.Wealth: return WealthEdges;
                case DistrictLayer.Crime: return CrimeEdges;
                case DistrictLayer.Standing: return StandingEdges;
                case DistrictLayer.Supply: return SupplyEdges;
                case DistrictLayer.Internet: return InternetEdges;
                default: return null;
            }
        }

        /// <summary>The band a figure is in on four rising edges: 0 under the first, 4 at or over the last.</summary>
        public static int BandOf(long value, int[] edges)
        {
            int band = 0;
            for (int i = 0; edges != null && i < edges.Length && i < Bands - 1; i++)
            {
                if (value >= edges[i])
                {
                    band = i + 1;
                }
            }
            return band;
        }

        /// <summary>
        /// The four edges of the city's own range from its lowest and highest figure: five equal steps, each edge rounded
        /// up, so the lowest figure is always in the bottom band and the highest in the top one. A range narrower than five
        /// has edges that repeat: the bands between them are empty (<see cref="HasRange"/>). With least equal to most there
        /// is no range: null.
        /// </summary>
        public static int[] RelativeEdges(long least, long most)
        {
            if (most <= least)
            {
                return null;
            }
            var edges = new int[Bands - 1];
            long span = most - least;
            for (int i = 0; i < edges.Length; i++)
            {
                long step = (span * (i + 1) + Bands - 1) / Bands;
                long edge = least + Math.Max(1L, step);
                edges[i] = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, edge));
            }
            return edges;
        }

        /// <summary>False for a band no figure can be in (between two edges that are the same); the key leaves it out.</summary>
        public static bool HasRange(int[] edges, int band)
        {
            int? from, to;
            Range(edges, band, out from, out to);
            return !from.HasValue || !to.HasValue || to.Value >= from.Value;
        }

        /// <summary>
        /// The band of each district's figure (<see cref="None"/> where it has none), on the layer's own scale. For a
        /// layer on the city's range the edges used are given back (null when every figure is the same: all middle).
        /// </summary>
        public static int[] BandAll(DistrictLayer layer, long[] values, bool[] known, out int[] edges)
        {
            int count = values != null ? values.Length : 0;
            var bands = new int[count];
            edges = FixedEdges(layer);
            if (edges == null)
            {
                long least = long.MaxValue, most = long.MinValue;
                for (int i = 0; i < count; i++)
                {
                    if (known[i])
                    {
                        least = Math.Min(least, values[i]);
                        most = Math.Max(most, values[i]);
                    }
                }
                edges = least <= most ? RelativeEdges(least, most) : null;
            }
            for (int i = 0; i < count; i++)
            {
                bands[i] = !known[i] ? None : edges == null ? Bands / 2 : BandOf(values[i], edges);
            }
            return bands;
        }

        /// <summary>The range a band covers on four edges: from (null for "under the first edge") and to (null for "and over"), inclusive.</summary>
        public static void Range(int[] edges, int band, out int? from, out int? to)
        {
            from = band <= 0 || edges == null ? (int?)null : edges[Math.Min(band, edges.Length) - 1];
            to = edges == null || band >= edges.Length ? (int?)null : edges[Math.Max(band, 0)] - 1;
        }

        // ---------------------------------------------------------------- colours

        // Low to high. The good-bad ramp runs red, orange, yellow, light green, green; the neutral one light blue to deep blue.
        private static readonly Tint[] GoodRamp =
        {
            new Tint(214, 64, 58, ZoneAlpha), new Tint(232, 140, 48, ZoneAlpha), new Tint(236, 204, 64, ZoneAlpha),
            new Tint(150, 204, 82, ZoneAlpha), new Tint(52, 168, 83, ZoneAlpha)
        };

        private static readonly Tint[] NeutralRamp =
        {
            new Tint(198, 226, 246, ZoneAlpha), new Tint(142, 196, 236, ZoneAlpha), new Tint(84, 156, 220, ZoneAlpha),
            new Tint(40, 110, 196, ZoneAlpha), new Tint(18, 64, 150, ZoneAlpha)
        };

        public static readonly Tint NoFigure = new Tint(150, 156, 162, ZoneAlpha);

        /// <summary>A band's colour on a layer (grey for <see cref="None"/>).</summary>
        public static Tint Colour(LayerSense sense, int band)
        {
            if (band < 0 || band >= Bands)
            {
                return NoFigure;
            }
            switch (sense)
            {
                case LayerSense.HighGood: return GoodRamp[band];
                case LayerSense.LowGood: return GoodRamp[Bands - 1 - band];
                default: return NeutralRamp[band];
            }
        }
    }

    /// <summary>
    /// The crimes reported in each district, newest last: a day and a district each, at most
    /// <see cref="DistrictRules.LogMost"/>, older than the view looks back thrown away. It is the view's only line in the
    /// save ("reallife:district_log"): "1;day:district;day:district...". A line that cannot be read gives an empty log.
    /// </summary>
    public sealed class DistrictLog
    {
        public const string Key = "reallife:district_log";
        private const string Version = "1";

        private readonly List<int> _days = new List<int>(64);
        private readonly List<string> _districts = new List<string>(64);

        public int Reports => _days.Count;

        /// <summary>A crime reported in a district on a day. Nothing for an empty district.</summary>
        public void Add(int day, string district)
        {
            if (string.IsNullOrEmpty(district) || district.IndexOf(';') >= 0)
            {
                return;
            }
            _days.Add(day);
            _districts.Add(district);
            if (_days.Count > DistrictRules.LogMost)
            {
                _days.RemoveAt(0);
                _districts.RemoveAt(0);
            }
        }

        /// <summary>A district's reports from the days back from today (today is one of them). A report dated after today does not count.</summary>
        public int Count(string district, int today, int days)
        {
            int count = 0;
            for (int i = 0; i < _days.Count; i++)
            {
                int age = today - _days[i];
                if (age >= 0 && age < days && string.Equals(_districts[i], district, StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>Throws away what is older than the view looks back.</summary>
        public void Prune(int today)
        {
            for (int i = _days.Count - 1; i >= 0; i--)
            {
                if (today - _days[i] >= DistrictRules.CrimeDays)
                {
                    _days.RemoveAt(i);
                    _districts.RemoveAt(i);
                }
            }
        }

        public void Clear()
        {
            _days.Clear();
            _districts.Clear();
        }

        public string Write()
        {
            var w = new StringBuilder(16 + _days.Count * 32);
            w.Append(Version);
            for (int i = 0; i < _days.Count; i++)
            {
                w.Append(';').Append(_days[i].ToString(CultureInfo.InvariantCulture)).Append(':').Append(_districts[i]);
            }
            return w.ToString();
        }

        public static DistrictLog Read(string line)
        {
            var log = new DistrictLog();
            if (string.IsNullOrEmpty(line))
            {
                return log;
            }
            string[] parts = line.Split(';');
            if (parts[0] != Version)
            {
                return log;
            }
            for (int i = 1; i < parts.Length; i++)
            {
                int colon = parts[i].IndexOf(':');
                int day;
                if (colon <= 0 || !int.TryParse(parts[i].Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out day))
                {
                    continue;
                }
                log.Add(day, parts[i].Substring(colon + 1));
            }
            return log;
        }
    }
}
