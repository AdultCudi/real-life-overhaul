using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What the tech sector remembers of its firms on the market: one line under <see cref="Key"/> in <see cref="TechWriter"/>'s format, a header (h) and the
    /// firms (f), each the listing its stock was made for and how the stock ended. A save from before it has no line, and a firm that listed then is
    /// given its stock on the first day it is seen. The lines of <see cref="ExitStore"/> and <see cref="VentureStore"/> are untouched; an older RLO never
    /// reads this one.
    /// </summary>
    public static class ExitStockStore
    {
        public const string Key = "reallife:tech_exit_stock";
        public const int Version = 1;
        public const int FirmsMost = 64;

        public static StockFile Load(Dictionary<string, string> data, out int version) => TechLines.Load(data, Key, Version, Read, out version);

        public static string Write(StockFile f)
        {
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(f.LastDay);
            w.Part('f');
            foreach (StockFirm s in f.Firms)
            {
                w.Record().Place(s.Place).Add(s.ListDay).Add(s.OpenDay).Add(s.EndDay).Add(s.Ended).Add(s.Old).Add(s.Ticker);
            }
            return w.ToString();
        }

        public static StockFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var f = new StockFile();
            Fields h = r.One('h');
            f.LastDay = h.Int(-1);
            foreach (Fields x in r.Records('f'))
            {
                if (f.Firms.Count >= FirmsMost)
                {
                    break;
                }
                var s = new StockFirm { Place = x.Place(), ListDay = x.Int(), OpenDay = Math.Max(0, x.Int()), EndDay = Math.Max(0, x.Int()), Ended = TalentCatalogue.Clamp(x.Int(), 0, StockEnd.Gone),
                    Old = x.Bool(), Ticker = x.Text() };
                if (s.Place.Length > 0)
                {
                    f.Firms.Add(s);
                }
            }
            return f;
        }
    }
}
