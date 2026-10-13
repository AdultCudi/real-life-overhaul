using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The player's tech firms in the save: one line of the save's modData under <see cref="Key"/>, written by
    /// <see cref="TechWriter"/>. Game-free.
    ///
    ///     v=1~h:seed|first day|last day|next id|last tender~f:...~p:...~c:...
    ///
    ///   f  each firm: place|kind|founded|focus|debt|reputation|registered until|breaches|outages|research costs|relief day|last 30 days|the 30 days' takings divided by commas
    ///   p  each product: id|place|name|kind|segment|stage|features|progress|bugs|users|free|tier|marketing|share|launched|hype|scope|revenue|rival features|rival tier|last release|carry|training|training level
    ///   c  each contract: id|place|kind|client|own place|council|points|done|value|paid|start|due|end|satisfaction|stage|bid|rivals|last billed
    /// </summary>
    public static class FirmsStore
    {
        public const string Key = "reallife:tech_firms";
        public const int Version = 1;

        public static FirmsFile Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            string text;
            if (data == null || !data.TryGetValue(Key, out text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            try
            {
                return Read(text, out version);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string Write(FirmsFile f)
        {
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(f.Seed).Add(f.FirstDay).Add(f.LastDay).Add(f.NextId).Add(f.LastTender);
            w.Part('f');
            var days = new StringBuilder(256);
            foreach (Firm m in f.Firms)
            {
                days.Length = 0;
                for (int i = 0; i < m.Days.Length; i++)
                {
                    if (i > 0)
                    {
                        days.Append(',');
                    }
                    days.Append(m.Days[i].ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                w.Record().Place(m.Place).Add(m.Kind).Add(m.Founded).Add(m.Focus).Add(m.Debt).Add(m.Reputation).Add(m.RegisteredUntil).Add(m.Breaches).Add(m.Outages).Add(m.RnD).Add(m.ReliefDay)
                    .Add(m.Revenue30).Add(days.ToString());
            }
            w.Part('p');
            foreach (Product p in f.Products)
            {
                w.Record().Add(p.Id).Place(p.Place).Add(p.Name).Add(p.Kind).Add(p.Segment).Add(p.Stage).Add(p.Features).Add(p.Progress).Add(p.Bugs).Add(p.Users).Add(p.Free).Add(p.Tier)
                    .Add(p.Marketing).Add(p.Share).Add(p.Launched).Add(p.Hype).Add(p.Scope).Add(p.Revenue).Add(p.RivalFeatures).Add(p.RivalTier).Add(p.LastRelease).Add(p.Carry).Add(p.Compute).Add(p.Train);
            }
            w.Part('c');
            foreach (Contract c in f.Contracts)
            {
                w.Record().Add(c.Id).Place(c.Place).Add(c.Kind).Add(c.Client).Place(c.OwnPlace).Add(c.Council).Add(c.Points).Add(c.Done).Add(c.Value).Add(c.Paid).Add(c.Start).Add(c.Due).Add(c.End)
                    .Add(c.Satisfaction).Add(c.Stage).Add(c.Bid).Add(c.Rivals).Add(c.LastBilled);
            }
            return w.ToString();
        }

        public static FirmsFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var f = new FirmsFile();
            Fields h = r.One('h');
            f.Seed = h.Int(1);
            f.FirstDay = h.Int();
            f.LastDay = h.Int(-1);
            f.NextId = Math.Max(1, h.Int(1));
            f.LastTender = h.Int();
            foreach (Fields x in r.Records('f'))
            {
                var m = new Firm
                {
                    Place = x.Place(), Kind = TalentCatalogue.Clamp(x.Int(), 0, (int)FirmKind.Count - 1), Founded = x.Int(), Focus = x.Int(), Debt = x.Int(), Reputation = x.Int(500),
                    RegisteredUntil = x.Int(), Breaches = x.Int(), Outages = x.Int(), RnD = x.Long(), ReliefDay = x.Int(), Revenue30 = x.Long()
                };
                string[] days = x.Text().Split(',');
                for (int i = 0; i < m.Days.Length && i < days.Length; i++)
                {
                    long v;
                    m.Days[i] = long.TryParse(days[i], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 0;
                }
                if (f.Firms.Count < 32)
                {
                    f.Firms.Add(m);
                }
            }
            foreach (Fields x in r.Records('p'))
            {
                var p = new Product
                {
                    Id = x.Int(), Place = x.Place(), Name = x.Text(), Kind = TalentCatalogue.Clamp(x.Int(), 0, (int)ProductKind.Count - 1), Segment = TalentCatalogue.Clamp(x.Int(), 0, 2),
                    Stage = x.Int(), Features = x.Int(), Progress = x.Int(), Bugs = x.Int(), Users = x.Long(), Free = x.Long(), Tier = TalentCatalogue.Clamp(x.Int(1), 0, 2), Marketing = x.Int(),
                    Share = x.Int(100), Launched = x.Int(), Hype = x.Int(), Scope = x.Int(), Revenue = x.Long(), RivalFeatures = x.Int(), RivalTier = TalentCatalogue.Clamp(x.Int(1), 0, 2),
                    LastRelease = x.Int(), Carry = x.Long(), Compute = x.Long(), Train = x.Int(2)
                };
                if (f.Products.Count < FirmsCatalogue.ProductsMost * 2)
                {
                    f.Products.Add(p);
                }
            }
            foreach (Fields x in r.Records('c'))
            {
                var c = new Contract
                {
                    Id = x.Int(), Place = x.Place(), Kind = TalentCatalogue.Clamp(x.Int(), 0, (int)WorkKind.Count - 1), Client = x.Text(), OwnPlace = x.Place(), Council = x.Bool(), Points = x.Int(),
                    Done = x.Int(), Value = x.Long(), Paid = x.Long(), Start = x.Int(), Due = x.Int(), End = x.Int(), Satisfaction = x.Int(700), Stage = x.Int(), Bid = x.Long(), Rivals = x.Int(),
                    LastBilled = x.Int()
                };
                if (f.Contracts.Count < FirmsCatalogue.ContractsMost + FirmsCatalogue.OffersMost)
                {
                    f.Contracts.Add(c);
                }
            }
            return f;
        }
    }
}
