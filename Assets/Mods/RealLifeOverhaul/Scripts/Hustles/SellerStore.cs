using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>
    /// The Marketplace's sellers in the save: one line under <see cref="Key"/>, apart from the Marketplace's own so a save from before it
    /// keeps both as they were. Fields a later version adds at the end of a record read as nothing here. Game-free.
    ///
    ///   h  next id
    ///   s  each seller: id|name|rating|tag|rival|sold
    /// </summary>
    public static class SellerStore
    {
        public const string Key = "reallife:hustle_sellers";
        public const int Version = 1;

        public static SellerFile Load(Dictionary<string, string> data, out int version) => TechLines.Load(data, Key, Version, Read, out version);

        public static string Write(SellerFile f)
        {
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(f.NextId);
            w.Part('s');
            for (int i = 0; i < f.Sellers.Count && i < SellerRules.PoolSize + SellerRules.RivalsMost; i++)
            {
                Seller s = f.Sellers[i];
                w.Record().Add(s.Id).Add(s.Name).Add(s.Rating).Add(s.Tag).Add(s.Rival).Add(s.Sold).Add(s.PersonId);
            }
            return w.ToString();
        }

        public static SellerFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var f = new SellerFile();
            f.NextId = System.Math.Max(1, r.One('h').Int(1));
            foreach (Fields x in r.Records('s'))
            {
                var s = new Seller
                {
                    Id = x.Int(), Name = x.Text(), Rating = TalentCatalogue.Clamp(x.Int(30), 10, 50),
                    Tag = TalentCatalogue.Clamp(x.Int(), 0, (int)SellerTag.Trade), Rival = x.Text(), Sold = System.Math.Max(0, x.Int())
                };
                s.PersonId = System.Math.Max(0, x.Int());
                if (s.Id > 0 && s.Name.Length > 0 && f.Sellers.Count < SellerRules.PoolSize + SellerRules.RivalsMost)
                {
                    f.Sellers.Add(s);
                    f.NextId = System.Math.Max(f.NextId, s.Id + 1);
                }
            }
            return f;
        }
    }
}
