using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The player's data centres and cloud in the save: one line under <see cref="Key"/> in <see cref="TechWriter"/>'s format, a
    /// header (h), the cloud (c) and the data centres (d), fields in the order <see cref="Write"/> writes them. The end game's
    /// scale fields come last, so an older line reads with no scale day, no racks of the new kind and no crews.
    /// </summary>
    public static class InfraStore
    {
        public const string Key = "reallife:tech_infra";
        public const int Version = 1;

        public static InfraFile Load(Dictionary<string, string> data, out int version) => TechLines.Load(data, Key, Version, Read, out version);

        public static string Write(InfraFile f)
        {
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(f.Seed).Add(f.FirstDay).Add(f.LastDay).Add(f.ScaleFrom).Add(f.Owed);
            Cloud c = f.Cloud;
            w.Part('c').Record().Add(c.Open).Add(c.Name).Add(c.Launched).Add(c.Price).Add(c.RivalPrice).Add(c.LastCut).Add(c.Reputation).Add(c.LicenceUntil)
                .Add(c.Hyper).Add(c.Sold).Add(c.GpuSold).Add(c.Own).Add(c.Ops);
            w.Part('d');
            var days = new StringBuilder(256);
            foreach (DataCentre d in f.Centres)
            {
                days.Length = 0;
                for (int i = 0; i < d.Days.Length; i++)
                {
                    days.Append(i > 0 ? "," : string.Empty).Append(d.Days[i].ToString(CultureInfo.InvariantCulture));
                }
                w.Record().Place(d.Place).Add(d.District).Add(d.Tier).Add(d.Sqm).Add(d.Racks).Add(d.GpuRacks).Add(d.Book).Add(d.Fitted).Add(d.Colo).Add(d.Outages)
                    .Add(d.DownUntil).Add(d.Takings30).Add(days.ToString())
                    .Add(d.Dense).Add(d.DenseGpu).Add(d.Crew[InfraCatalogue.Operators]).Add(d.Crew[InfraCatalogue.Engineers]).Add(d.Crew[InfraCatalogue.Security]);
            }
            return w.ToString();
        }

        public static InfraFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var f = new InfraFile();
            Fields h = r.One('h');
            f.Seed = h.Int(1);
            f.FirstDay = h.Int();
            f.LastDay = h.Int(-1);
            f.ScaleFrom = h.Int(EndScaleRules.Unset);
            f.Owed = Math.Max(0, h.Long());
            Fields c = r.One('c');
            Cloud cloud = f.Cloud;
            cloud.Open = c.Bool();
            cloud.Name = c.Text();
            cloud.Launched = c.Int();
            cloud.Price = TalentCatalogue.Clamp(c.Int(1000), 300, 3000);
            cloud.RivalPrice = TalentCatalogue.Clamp(c.Int(1000), InfraCatalogue.PriceFloor, 1000);
            cloud.LastCut = c.Int();
            cloud.Reputation = c.Int(600);
            cloud.LicenceUntil = c.Int();
            cloud.Hyper = c.Bool();
            cloud.Sold = c.Int();
            cloud.GpuSold = c.Int();
            cloud.Own = c.Int();
            cloud.Ops = Math.Max(0, c.Int());
            foreach (Fields x in r.Records('d'))
            {
                var d = new DataCentre
                {
                    Place = x.Place(), District = x.Text(), Tier = TalentCatalogue.Clamp(x.Int(), 0, InfraCatalogue.Tiers - 1), Sqm = x.Int(), Racks = x.Int(), GpuRacks = x.Int(), Book = x.Long(),
                    Fitted = x.Int(), Colo = x.Int(), Outages = x.Int(), DownUntil = x.Int(), Takings30 = x.Long()
                };
                string[] days = x.Text().Split(',');
                for (int i = 0; i < d.Days.Length && i < days.Length; i++)
                {
                    d.Days[i] = long.TryParse(days[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : 0;
                }
                d.Dense = TalentCatalogue.Clamp(x.Int(), 0, d.Racks);
                d.DenseGpu = TalentCatalogue.Clamp(x.Int(), 0, d.GpuRacks);
                for (int k = 0; k < InfraCatalogue.CrewKinds; k++)
                {
                    d.Crew[k] = Math.Max(0, x.Int());
                }
                if (f.Centres.Count < InfraCatalogue.CentresMost)
                {
                    f.Centres.Add(d);
                }
            }
            return f;
        }
    }
}
