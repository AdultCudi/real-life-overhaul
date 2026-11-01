using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The population's demand in the save: one small line of the save's modData under <see cref="Key"/>, written the way the tech sector writes its lines
    /// (Tech/TechCodec), beside the population's own. It keeps the anchor (so a business's figures go on from the same first day across saves) and the last
    /// day's figures (so every factor and lean is there the moment a save loads, before the next day is measured). Game-free.
    ///
    ///     v=1~a:seed|generated|day|anchor day~n:each source now~c:each source's anchor~o:online now by kind~p:online anchor by kind
    ///     ~d:district|households|businesses|adults|people|workers|unemployed|youth|housing|income|disposable|saved|debt|borrowing poor|fair|good|homes anchored;...
    ///
    /// A save without the line measures the next day afresh and anchors once settled; a line of another population (its seed or generation day) is forgotten
    /// at the first measure; a later version's extra fields at the end of a record read as nothing.
    /// </summary>
    public static class PopDemandStore
    {
        public const string Key = "reallife:population_demand";

        public static PopDemandState Load(Dictionary<string, string> data, out int version) => TechLines.Load(data, Key, PopDemandState.Version, Read, out version);

        public static string Write(PopDemandState m)
        {
            var w = new TechWriter(PopDemandState.Version);
            w.Part('a').Record().Add(m.Seed).Add(m.Generated).Add(m.Day).Add(m.AnchorDay);
            Numbers(w, 'n', m.Now);
            Numbers(w, 'c', m.Anchor);
            Numbers(w, 'o', m.OnlineNow);
            Numbers(w, 'p', m.OnlineAnchor);
            w.Part('d');
            for (int d = 0; d < m.Districts; d++)
            {
                w.Record().Add(m.Ids[d] ?? string.Empty).Add(m.Households[d]).Add(m.Businesses[d]).Add(m.Adults[d]).Add(m.People[d]).Add(m.Workers[d]).Add(m.Unemployed[d])
                 .Add(m.Youth[d]).Add(m.Housing[d]).Add(m.Income[d]).Add(m.Disposable[d]).Add(m.Saved[d]).Add(m.Debt[d]);
                for (int b = 0; b < PopDemandCatalogue.Bands; b++)
                {
                    w.Add(m.Borrowing[d * PopDemandCatalogue.Bands + b]);
                }
                w.Add(m.Connections[d]);
            }
            return w.ToString();
        }

        public static PopDemandState Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var m = new PopDemandState();
            Fields a = r.One('a');
            m.Seed = a.Int();
            m.Generated = a.Int();
            m.Day = a.Int(-1);
            m.AnchorDay = a.Int(-1);
            Read(r.One('n'), m.Now);
            Read(r.One('c'), m.Anchor);
            Read(r.One('o'), m.OnlineNow);
            Read(r.One('p'), m.OnlineAnchor);
            var ids = new List<string>(12);
            var rows = new List<Fields>(12);
            foreach (Fields x in r.Records('d'))
            {
                string id = x.Text();
                if (id.Length > 0 && ids.Count < PopulationCatalogue.DistrictsMost && !ids.Contains(id))
                {
                    ids.Add(id);
                    rows.Add(x);
                }
            }
            m.Size(ids.ToArray());
            for (int d = 0; d < rows.Count; d++)
            {
                Fields x = rows[d];
                m.Households[d] = x.Int();
                m.Businesses[d] = x.Int();
                m.Adults[d] = x.Int();
                m.People[d] = x.Int();
                m.Workers[d] = x.Int();
                m.Unemployed[d] = x.Int();
                m.Youth[d] = x.Int();
                m.Housing[d] = x.Int();
                m.Income[d] = x.Long();
                m.Disposable[d] = x.Long();
                m.Saved[d] = x.Long();
                m.Debt[d] = x.Long();
                for (int b = 0; b < PopDemandCatalogue.Bands; b++)
                {
                    m.Borrowing[d * PopDemandCatalogue.Bands + b] = x.Long();
                }
                m.Connections[d] = x.Long();
            }
            PopDemandRules.Leans(m);
            PopDemandRules.Refactor(m);
            return m;
        }

        private static void Numbers(TechWriter w, char part, long[] values)
        {
            w.Part(part).Record();
            for (int i = 0; i < values.Length; i++)
            {
                w.Add(values[i]);
            }
        }

        private static void Read(Fields f, long[] into)
        {
            for (int i = 0; i < into.Length; i++)
            {
                into[i] = f.Long();
            }
        }
    }
}
