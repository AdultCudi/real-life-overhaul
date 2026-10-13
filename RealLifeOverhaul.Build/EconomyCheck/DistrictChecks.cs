using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Districts;

// The district view (Scripts/Districts): every layer has its scale and its words, a fixed scale's bands fall where their
// edges say, the city's own range always runs from its lowest district in the bottom band to its highest in the top one
// in five steps that never repeat, a district with no figure is grey and every district alike sits in the middle, the
// key's ranges meet without a gap, the colours run the right way for each kind of layer, and the crime log counts its
// window, forgets what is older, keeps no more than it may and reads back what it wrote.
internal static partial class Program
{
    private static void Districts()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        DistrictLayers();
        DistrictFixedBands();
        DistrictRelativeBands();
        DistrictBandAll();
        DistrictKey();
        DistrictColours();
        DistrictLogs();
        DistrictWords();
        Console.WriteLine("  District view: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static void DistrictLayers()
    {
        Check(DistrictRules.Ids.Length == DistrictRules.Layers && Enum.GetValues(typeof(DistrictLayer)).Length == DistrictRules.Layers,
            "districts: one id per layer, and as many layers as the enum has");
        var seen = new HashSet<string>();
        for (int i = 0; i < DistrictRules.Ids.Length; i++)
        {
            Check(seen.Add(DistrictRules.Ids[i]) && DistrictRules.Ids[i].IndexOf('_') < 0, "districts: layer id '" + DistrictRules.Ids[i] + "' is unique and has no underscore");
        }
        for (int i = 0; i < DistrictRules.Layers; i++)
        {
            var layer = (DistrictLayer)i;
            int[] edges = DistrictRules.FixedEdges(layer);
            Check(DistrictRules.Relative(layer) == (edges == null), "districts: " + layer + " is either a fixed scale or the city's own range");
            if (edges == null)
            {
                continue;
            }
            bool rising = edges.Length == DistrictRules.Bands - 1;
            for (int e = 1; e < edges.Length; e++)
            {
                rising &= edges[e] > edges[e - 1];
            }
            Check(rising, "districts: " + layer + "'s four edges rise");
        }
        Check(DistrictRules.Sense(DistrictLayer.Crime) == LayerSense.LowGood, "districts: on crime fewer is better");
        Check(DistrictRules.Sense(DistrictLayer.Standing) == LayerSense.HighGood && DistrictRules.Sense(DistrictLayer.Supply) == LayerSense.HighGood
              && DistrictRules.Sense(DistrictLayer.Internet) == LayerSense.HighGood && DistrictRules.Sense(DistrictLayer.Footfall) == LayerSense.HighGood,
            "districts: on standing, supply, the internet and footfall more is better");
        Check(DistrictRules.Sense(DistrictLayer.Rent) == LayerSense.Neutral && DistrictRules.Sense(DistrictLayer.Wealth) == LayerSense.Neutral
              && DistrictRules.Sense(DistrictLayer.Property) == LayerSense.Neutral && DistrictRules.Sense(DistrictLayer.Competition) == LayerSense.Neutral,
            "districts: wealth, rents, prices and competition are neither good nor bad");
        Check(Array.IndexOf(DistrictRules.WealthEdges, 100) < 0 && DistrictRules.BandOf(100, DistrictRules.WealthEdges) == 2,
            "districts: the city's middle income (100) is in the middle band");
        Check(DistrictRules.BandOf(50, DistrictRules.StandingEdges) == 2, "districts: the opinion every district starts at (50) is neutral");
        Check(DistrictRules.BandOf(0, DistrictRules.CrimeEdges) == 0 && DistrictRules.BandOf(1, DistrictRules.CrimeEdges) == 1, "districts: no crime is the bottom band, one is the next");
        Check(DistrictRules.BandOf(100, DistrictRules.SupplyEdges) == 4 && DistrictRules.BandOf(99, DistrictRules.SupplyEdges) == 3, "districts: only every account supplied is the top band");
    }

    private static void DistrictFixedBands()
    {
        int[] edges = { 10, 20, 30, 40 };
        Check(DistrictRules.BandOf(long.MinValue, edges) == 0 && DistrictRules.BandOf(9, edges) == 0, "districts: under the first edge is band 0");
        Check(DistrictRules.BandOf(10, edges) == 1 && DistrictRules.BandOf(19, edges) == 1, "districts: at an edge is the band above it");
        Check(DistrictRules.BandOf(40, edges) == 4 && DistrictRules.BandOf(long.MaxValue, edges) == 4, "districts: at or over the last edge is band 4");
        int last = 0;
        bool steady = true;
        for (int v = -5; v < 60; v++)
        {
            int band = DistrictRules.BandOf(v, edges);
            steady &= band >= last && band - last <= 1 && band >= 0 && band < DistrictRules.Bands;
            last = band;
        }
        Check(steady, "districts: a rising figure never falls a band or skips one");
    }

    private static void DistrictRelativeBands()
    {
        Check(DistrictRules.RelativeEdges(5, 5) == null && DistrictRules.RelativeEdges(9, 3) == null, "districts: no range, no edges");
        var random = new Random(7);
        bool ends = true, rising = true, five = true, narrow = true;
        for (int trial = 0; trial < 4000; trial++)
        {
            long least = random.Next(-1000, 100000);
            long most = least + 1 + random.Next(0, trial % 3 == 0 ? 6 : 2000000);
            int[] edges = DistrictRules.RelativeEdges(least, most);
            if (edges == null || edges.Length != DistrictRules.Bands - 1)
            {
                five = false;
                continue;
            }
            for (int e = 1; e < edges.Length; e++)
            {
                if (most - least >= DistrictRules.Bands)
                {
                    rising &= edges[e] > edges[e - 1];
                }
                else
                {
                    narrow &= edges[e] >= edges[e - 1];
                }
            }
            for (long v = least; v <= most && most - least < 50; v++)
            {
                narrow &= DistrictRules.HasRange(edges, DistrictRules.BandOf(v, edges));
            }
            ends &= DistrictRules.BandOf(least, edges) == 0 && DistrictRules.BandOf(most, edges) == DistrictRules.Bands - 1;
        }
        Check(five, "districts: a range always has four edges");
        Check(rising, "districts: a range of five or more never has two edges the same");
        Check(narrow, "districts: in a narrower range the edges never fall, and every figure is in a band the key shows");
        Check(ends, "districts: the lowest district is always in the bottom band and the highest in the top one");
        int[] even = DistrictRules.RelativeEdges(0, 100);
        Check(even != null && even[0] == 20 && even[1] == 40 && even[2] == 60 && even[3] == 80, "districts: 0 to 100 splits at 20, 40, 60 and 80");
    }

    private static void DistrictBandAll()
    {
        int[] edges;
        int[] bands = DistrictRules.BandAll(DistrictLayer.Rent, new long[] { 300, 100, 500, 0 }, new[] { true, true, true, false }, out edges);
        Check(bands[1] == 0 && bands[2] == DistrictRules.Bands - 1 && bands[3] == DistrictRules.None && edges != null,
            "districts: the city's range is set by the districts with a figure, and one with none is grey");
        bands = DistrictRules.BandAll(DistrictLayer.Competition, new long[] { 7, 7, 7 }, new[] { true, true, true }, out edges);
        Check(edges == null && bands[0] == DistrictRules.Bands / 2 && bands[2] == DistrictRules.Bands / 2, "districts: every district alike is the middle band, with no key ranges");
        bands = DistrictRules.BandAll(DistrictLayer.Council, new long[] { 1, 2 }, new[] { false, false }, out edges);
        Check(edges == null && bands[0] == DistrictRules.None && bands[1] == DistrictRules.None, "districts: no figures at all, all grey");
        bands = DistrictRules.BandAll(DistrictLayer.Crime, new long[] { 0, 2, 4, 7, 30 }, new[] { true, true, true, true, true }, out edges);
        Check(ReferenceEquals(edges, DistrictRules.CrimeEdges) && bands[0] == 0 && bands[1] == 1 && bands[2] == 2 && bands[3] == 3 && bands[4] == 4,
            "districts: a fixed layer bands by its own edges, whatever the city's range");
        bands = DistrictRules.BandAll(DistrictLayer.Wealth, new long[0], new bool[0], out edges);
        Check(bands.Length == 0, "districts: no districts, no bands");
    }

    private static void DistrictKey()
    {
        var all = new List<int[]> { DistrictRules.WealthEdges, DistrictRules.CrimeEdges, DistrictRules.StandingEdges, DistrictRules.SupplyEdges, DistrictRules.InternetEdges,
            DistrictRules.RelativeEdges(0, 7), DistrictRules.RelativeEdges(120, 98000), DistrictRules.RelativeEdges(3, 5) };
        bool meet = true, ends = true, holds = true;
        foreach (int[] edges in all)
        {
            int? lastTo = null;
            for (int band = 0; band < DistrictRules.Bands; band++)
            {
                int? from, to;
                DistrictRules.Range(edges, band, out from, out to);
                if (!DistrictRules.HasRange(edges, band))
                {
                    continue;   // (an empty band of a narrow range: the key leaves it out)
                }
                if (band == 0)
                {
                    ends &= !from.HasValue && to.HasValue;
                }
                else
                {
                    meet &= from.HasValue && lastTo.HasValue && from.Value == lastTo.Value + 1;
                }
                if (band == DistrictRules.Bands - 1)
                {
                    ends &= from.HasValue && !to.HasValue;
                }
                // Every figure the range names is in that band.
                if (from.HasValue)
                {
                    holds &= DistrictRules.BandOf(from.Value, edges) == band;
                }
                if (to.HasValue)
                {
                    holds &= DistrictRules.BandOf(to.Value, edges) == band;
                }
                lastTo = to;
            }
        }
        Check(ends, "districts: the key's bottom band is open below and its top band open above");
        Check(meet, "districts: each band of the key starts where the one under it ends");
        Check(holds, "districts: the ends of a band's range in the key are in that band");
    }

    private static void DistrictColours()
    {
        bool alpha = true, distinct = true;
        foreach (LayerSense sense in new[] { LayerSense.Neutral, LayerSense.HighGood, LayerSense.LowGood })
        {
            var seen = new HashSet<string>();
            for (int band = 0; band < DistrictRules.Bands; band++)
            {
                Tint tint = DistrictRules.Colour(sense, band);
                alpha &= tint.A == DistrictRules.ZoneAlpha;
                distinct &= seen.Add(tint.Hex) && tint.Hex != DistrictRules.NoFigure.Hex;
            }
        }
        Check(alpha, "districts: every band is drawn at the game's own zone strength");
        Check(distinct, "districts: five colours a layer, none of them the grey of no figure");
        Tint goodTop = DistrictRules.Colour(LayerSense.HighGood, 4), goodBottom = DistrictRules.Colour(LayerSense.HighGood, 0);
        Check(goodTop.G > goodTop.R && goodBottom.R > goodBottom.G, "districts: where more is better the top band is green and the bottom red");
        Tint crimeNone = DistrictRules.Colour(LayerSense.LowGood, 0), crimeMany = DistrictRules.Colour(LayerSense.LowGood, 4);
        Check(crimeNone.Hex == goodTop.Hex && crimeMany.Hex == goodBottom.Hex, "districts: where less is better the colours run the other way");
        bool deepening = true;
        for (int band = 1; band < DistrictRules.Bands; band++)
        {
            Tint a = DistrictRules.Colour(LayerSense.Neutral, band - 1), b = DistrictRules.Colour(LayerSense.Neutral, band);
            deepening &= b.R + b.G + b.B < a.R + a.G + a.B && b.B >= b.R;
        }
        Check(deepening, "districts: a neutral layer runs from light blue to deep blue");
        Check(DistrictRules.Colour(LayerSense.Neutral, DistrictRules.None).Hex == DistrictRules.NoFigure.Hex && DistrictRules.Colour(LayerSense.HighGood, 9).Hex == DistrictRules.NoFigure.Hex,
            "districts: no figure (or a band out of range) is grey");
    }

    private static void DistrictLogs()
    {
        var log = new DistrictLog();
        log.Add(9, "ba:neighborhood_midtown");    // 28 days before "today": just outside the window
        log.Add(20, "ba:neighborhood_midtown");
        log.Add(37, "ba:neighborhood_midtown");
        log.Add(37, "ba:neighborhood_hellskitchen");
        log.Add(40, "ba:neighborhood_midtown");   // after "today" below: not counted
        log.Add(37, string.Empty);
        log.Add(37, "bad;district");
        Check(log.Reports == 5, "districts: a report needs a district, and one that would break the save's line is not taken");
        int today = 37;
        Check(log.Count("ba:neighborhood_midtown", today, DistrictRules.CrimeDays) == 2, "districts: the crime layer counts today and the days before it inside its window");
        Check(log.Count("ba:neighborhood_midtown", today, 1) == 1 && log.Count("ba:neighborhood_hellskitchen", today, DistrictRules.CrimeDays) == 1,
            "districts: each district counts its own");
        Check(log.Count("ba:neighborhood_thehamptons", today, DistrictRules.CrimeDays) == 0, "districts: a district with no reports has none");
        DistrictLog back = DistrictLog.Read(log.Write());
        Check(back.Reports == log.Reports && back.Write() == log.Write(), "districts: the log reads back exactly what it wrote");
        log.Prune(today);
        Check(log.Reports == 4 && log.Count("ba:neighborhood_midtown", today, DistrictRules.CrimeDays) == 2, "districts: what is older than the window is forgotten, nothing else");
        Check(DistrictLog.Read(null).Reports == 0 && DistrictLog.Read("").Reports == 0 && DistrictLog.Read("9;3:x").Reports == 0,
            "districts: no line, or a line of another version, is an empty log");
        Check(DistrictLog.Read("1;x;4:ba:neighborhood_midtown;q:ba:neighborhood_midtown;5:").Reports == 1, "districts: a damaged report is skipped and the rest are read");
        var full = new DistrictLog();
        for (int i = 0; i < DistrictRules.LogMost + 50; i++)
        {
            full.Add(i, "ba:neighborhood_midtown");
        }
        Check(full.Reports == DistrictRules.LogMost && full.Count("ba:neighborhood_midtown", DistrictRules.LogMost + 49, 1) == 1
              && full.Count("ba:neighborhood_midtown", 49, 50) == 0, "districts: the log keeps its newest reports up to its cap");
        full.Clear();
        Check(full.Reports == 0 && full.Write() == "1", "districts: a cleared log writes nothing but its version");
    }

    private static void DistrictWords()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json"));
        Dictionary<string, string> pt = LoadWords(System.IO.Path.Combine(folder, "pt.json"));
        Dictionary<string, string> zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        Check(en != null && pt != null && zh != null, "districts: en.json, pt.json and zh-cn.json are where the checks look for them (" + folder + ")");
        if (en == null || pt == null || zh == null)
        {
            return;
        }
        var missing = new List<string>();
        var keys = new List<string> { "reallife_dv_category", "reallife_dv_key", "reallife_dv_districts", "reallife_dv_hint", "reallife_dv_all_alike", "reallife_dv_no_figure",
            "reallife_dv_range_under", "reallife_dv_range_over", "reallife_dv_range_between", "reallife_dv_close" };
        for (int i = 0; i < DistrictRules.Layers; i++)
        {
            string id = DistrictRules.Ids[i];
            keys.Add("reallife_dv_row_" + id);
            keys.Add("reallife_dv_about_" + id);
            keys.Add("reallife_dv_none_" + id);
            for (int band = 0; band < DistrictRules.Bands; band++)
            {
                keys.Add("reallife_dv_band_" + id + "_" + band);
            }
        }
        foreach (string key in keys)
        {
            string a, b, c;
            if (!en.TryGetValue(key, out a) || !pt.TryGetValue(key, out b) || !zh.TryGetValue(key, out c) || string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b) || string.IsNullOrWhiteSpace(c))
            {
                missing.Add(key);
            }
        }
        Check(missing.Count == 0, "districts: every layer's row, words, key bands and grey are in en.json, pt.json and zh-cn.json (" + keys.Count + " keys"
            + (missing.Count > 0 ? "; missing " + string.Join(", ", missing) : string.Empty) + ")");
        string under, over, between;
        Check(en.TryGetValue("reallife_dv_range_under", out under) && under.Contains("{to}") && en.TryGetValue("reallife_dv_range_over", out over) && over.Contains("{from}")
              && en.TryGetValue("reallife_dv_range_between", out between) && between.Contains("{from}") && between.Contains("{to}"),
            "districts: the key's ranges have the figures they are written with");
        string guide;
        Check(en.TryGetValue("reallife_guide_districtview", out guide) && guide.Contains("{dv_crime_days}") && guide.Contains("{dv_bands}"),
            "districts: the Guide page prints the crime window and the number of bands from the rules");
    }
}
