using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What exits keep in a save: one line under <see cref="Key"/> in <see cref="TechWriter"/>'s format, a header (h), the firms' exits (f), their
    /// offers (o) and the city startups' founders (p). A save from before it has no line, and a firm then starts with nothing standing. The
    /// venture line (<see cref="VentureStore"/>) is untouched; an older RLO never reads this one.
    /// </summary>
    public static class ExitStore
    {
        public const string Key = "reallife:tech_exits";
        public const int Version = 1;

        public static ExitFile Load(Dictionary<string, string> data, out int version) => TechLines.Load(data, Key, Version, Read, out version);

        public static string Write(ExitFile f)
        {
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(f.Seed).Add(f.LastDay).Add(f.NextOffer);
            w.Part('f');
            foreach (FirmExit e in f.Firms)
            {
                if (e.Empty)
                {
                    continue;
                }
                w.Record().Place(e.Place).Add(e.Stage).Add(e.Bank).Add(e.BankName).Add(e.Reach).Add(e.Spread).Add(e.Fee).Add(e.Fair).Add(e.Demand).Add(e.Choice).Add(e.PriceDay)
                    .Add(e.Low).Add(e.High).Add(e.Price).Add(e.FloatBp).Add(e.Gross).Add(e.Net).Add(e.ListDay).Add(e.LockTo).Add(e.Called).Add(e.StageSeen).Add(e.RoundDay).Add(e.Sold)
                    .Add(e.SoldDay).Add(e.SoldFor).Add(e.SoldTo);
            }
            w.Part('o');
            foreach (FirmExit e in f.Firms)
            {
                foreach (ExitOffer o in e.Offers)
                {
                    w.Record().Place(e.Place).Add(o.Id).Add(o.Kind).Add(o.Buyer).Add(o.BuyerId).Add(o.Name).Add(o.Price).Add(o.Made).Add(o.Lapse).Add(o.Countered);
                }
            }
            w.Part('p');
            foreach (FounderRec r in f.Founders)
            {
                w.Record().Add(r.Startup).Add(r.Age).Add(r.Female).Add(r.Skill).Add(r.Field).Add(r.Linked).Add(r.Tower).Add(r.Slot).Add(r.Hired).Add(r.Seen);
            }
            return w.ToString();
        }

        public static ExitFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var f = new ExitFile();
            Fields h = r.One('h');
            f.Seed = h.Int();
            f.LastDay = h.Int(-1);
            f.NextOffer = Math.Max(1, h.Int(1));
            foreach (Fields x in r.Records('f'))
            {
                if (f.Firms.Count >= 32)
                {
                    break;
                }
                var e = new FirmExit
                {
                    Place = x.Place(), Stage = TalentCatalogue.Clamp(x.Int(), 0, ListStage.CalledOff), Bank = Math.Max(0, x.Int()), BankName = x.Text(), Reach = x.Int(), Spread = x.Int(),
                    Fee = Math.Max(0L, x.Long()), Fair = Math.Max(0L, x.Long()), Demand = x.Int(), Choice = TalentCatalogue.Clamp(x.Int(), 0, 2), PriceDay = x.Int(), Low = x.Long(), High = x.Long(),
                    Price = x.Long(), FloatBp = x.Int(), Gross = Math.Max(0L, x.Long()), Net = Math.Max(0L, x.Long()), ListDay = x.Int(), LockTo = x.Int(), Called = x.Int(), StageSeen = x.Int(-2),
                    RoundDay = x.Int(-1), Sold = Math.Max(0L, x.Long()), SoldDay = x.Int(), SoldFor = Math.Max(0L, x.Long()), SoldTo = x.Text()
                };
                f.Firms.Add(e);
            }
            foreach (Fields x in r.Records('o'))
            {
                FirmExit e = f.Exit(x.Place(), true);
                var o = new ExitOffer
                {
                    Id = x.Int(), Kind = TalentCatalogue.Clamp(x.Int(), 0, 1), Buyer = TalentCatalogue.Clamp(x.Int(), 0, 1), BuyerId = x.Int(), Name = x.Text(), Price = Math.Max(0L, x.Long()),
                    Made = x.Int(), Lapse = x.Int(), Countered = x.Bool()
                };
                if (e.Offers.Count < 8 && o.Id > 0)
                {
                    e.Offers.Add(o);
                    f.NextOffer = Math.Max(f.NextOffer, o.Id + 1);
                }
            }
            foreach (Fields x in r.Records('p'))
            {
                if (f.Founders.Count >= 200)
                {
                    break;
                }
                f.Founders.Add(new FounderRec
                {
                    Startup = x.Int(), Age = x.Int(), Female = x.Bool(), Skill = TalentCatalogue.Clamp(x.Int(), 0, 100), Field = x.Int(), Linked = x.Bool(), Tower = x.Int(-1), Slot = x.Int(),
                    Hired = Math.Max(0, x.Int()), Seen = x.Int()
                });
            }
            return f;
        }
    }
}
