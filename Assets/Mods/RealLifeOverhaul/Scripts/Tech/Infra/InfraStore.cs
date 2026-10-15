using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The player's data centres and cloud in the save: one line of the save's modData under <see cref="Key"/>, written by
    /// <see cref="TechWriter"/>. Game-free.
    ///
    ///     v=1~h:seed|first day|last day|scale from~c:cloud~d:...
    ///
    ///   c  the cloud: open|name|launched|price|rivals' price|last cut|reputation|licence until|hyperscaler|sold|AI sold|own|operations people
    ///   d  each data centre: place|district|tier|square metres|racks|AI racks|book value|fitted|let|outages|down until|last 30 days|the 30 days' takings by commas
    ///      |racks of the new kind|AI racks of the new kind|operators|engineers|security
    ///
    /// The fields after "own" and after the takings are the end game's scale (Core/EndScale), appended: a line written
    /// before them reads with no scale day (it is phased in), no racks of the new kind and no crews.
    /// </summary>
    public static class InfraStore
    {
        public const string Key = "reallife:tech_infra";
        public const int Version = 1;

        public static InfraFile Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            string text;
            if (data == null || !data.TryGetValue(Key, out text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            try
            {
                return SaveLines.Checked(data, Key, Read(text, out version), version, Version);   // (a line that cannot be read is kept as it is: Core/SaveLines)
            }
            catch (Exception)
            {
                SaveLines.Unreadable(data, Key, "it could not be read");
                return null;
            }
        }

        public static string Write(InfraFile f)
        {
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(f.Seed).Add(f.FirstDay).Add(f.LastDay).Add(f.ScaleFrom);
            Cloud c = f.Cloud;
            w.Part('c').Record().Add(c.Open).Add(c.Name).Add(c.Launched).Add(c.Price).Add(c.RivalPrice).Add(c.LastCut).Add(c.Reputation).Add(c.LicenceUntil).Add(c.Hyper).Add(c.Sold).Add(c.GpuSold).Add(c.Own)
                .Add(c.Ops);
            w.Part('d');
            var days = new StringBuilder(256);
            foreach (DataCentre d in f.Centres)
            {
                days.Length = 0;
                for (int i = 0; i < d.Days.Length; i++)
                {
                    days.Append(i > 0 ? "," : string.Empty).Append(d.Days[i].ToString(CultureInfo.InvariantCulture));
                }
                w.Record().Place(d.Place).Add(d.District).Add(d.Tier).Add(d.Sqm).Add(d.Racks).Add(d.GpuRacks).Add(d.Book).Add(d.Fitted).Add(d.Colo).Add(d.Outages).Add(d.DownUntil).Add(d.Takings30).Add(days.ToString())
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
                    long v;
                    d.Days[i] = long.TryParse(days[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0;
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
