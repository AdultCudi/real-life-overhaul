using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The terms of the player's sales in a save: one line under <see cref="Key"/> in <see cref="TechWriter"/>'s format, a header (h) and the deals (d). A save from before has no
    /// line and no deal stands; the exits' line (<see cref="ExitStore"/>) is untouched, and an older RLO never reads this one. New fields go at the end of a record.
    /// </summary>
    public static class SaleTermStore
    {
        public const string Key = "reallife:tech_sale_terms";
        public const int Version = 1;

        public static TermFile Load(Dictionary<string, string> data, out int version) => TechLines.Load(data, Key, Version, Read, out version);

        /// <summary>The line, or null while there is nothing to keep.</summary>
        public static string Write(TermFile f)
        {
            if (f == null || f.Deals.Count == 0)
            {
                return null;
            }
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(f.Seed).Add(f.Next);
            w.Part('d');
            foreach (TermDeal d in f.Deals)
            {
                w.Record().Add(d.Id).Add(d.Kind).Add(d.State).Add(d.Day).Add(d.EndDay).Place(d.Place).Add(d.Firm).Add(d.Buyer).Add(d.BuyerId).Add(d.BuyerKind).Add(d.Price).Add(d.Now)
                    .Add(d.Deferred).Add(d.Mrr0).Add(d.Cust0).Add(d.Paid).Add(d.Most).Add(d.Seen).Add(d.Index).Add(d.CIndex).Add(d.Stock).Add(d.Shares).Add(d.ShareCents).Add(d.LockTo)
                    .Add(d.Mille).Add(d.Measure).Add(d.Target).Add(d.EarnState).Add(d.Due).Add(d.Further).Add(d.Salary).Add(d.Grant).Add(d.Years).Add(d.Vested).Add(d.Strikes)
                    .Add(d.Review).Add(d.Handed).Add(d.Synced).Add(d.Absent);
            }
            return w.ToString();
        }

        public static TermFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var f = new TermFile();
            Fields h = r.One('h');
            f.Seed = h.Int();
            f.Next = Math.Max(1, h.Int(1));
            foreach (Fields x in r.Records('d'))
            {
                if (f.Deals.Count >= SaleTermCatalogue.DealsMost)
                {
                    break;
                }
                var d = new TermDeal
                {
                    Id = x.Int(), Kind = TalentCatalogue.Clamp(x.Int(), 0, TermKind.Count - 1), State = TalentCatalogue.Clamp(x.Int(), 0, TermState.Gone), Day = x.Int(), EndDay = x.Int(),
                    Place = x.Place(), Firm = x.Text(), Buyer = x.Text(), BuyerId = x.Int(), BuyerKind = TalentCatalogue.Clamp(x.Int(), 0, 1), Price = Math.Max(0L, x.Long()),
                    Now = Math.Max(0L, x.Long()), Deferred = Math.Max(0L, x.Long()), Mrr0 = Math.Max(0L, x.Long()), Cust0 = Math.Max(0L, x.Long()), Paid = Math.Max(0L, x.Long()),
                    Most = Math.Max(0L, x.Long()), Seen = x.Int(), Index = TalentCatalogue.Clamp(x.Int(1000), SaleTermCatalogue.IndexLeast, SaleTermCatalogue.IndexMost),
                    CIndex = TalentCatalogue.Clamp(x.Int(1000), SaleTermCatalogue.IndexLeast, SaleTermCatalogue.IndexMost), Stock = x.Int(), Shares = Math.Max(0L, x.Long()),
                    ShareCents = Math.Max(0, x.Int()), LockTo = x.Int(), Mille = TalentCatalogue.Clamp(x.Int(), 0, 1000), Measure = TalentCatalogue.Clamp(x.Int(), 0, 1), Target = x.Int(),
                    EarnState = TalentCatalogue.Clamp(x.Int(), 0, 2), Due = x.Int(), Further = Math.Max(0L, x.Long()), Salary = Math.Max(0L, x.Long()), Grant = Math.Max(0L, x.Long()),
                    Years = Math.Max(0, x.Int()), Vested = Math.Max(0, x.Int()), Strikes = Math.Max(0, x.Int()), Review = x.Int(), Handed = x.Bool(), Synced = x.Int(), Absent = Math.Max(0, x.Int())
                };
                if (d.Id > 0 && f.Of(d.Id) == null)
                {
                    f.Deals.Add(d);
                    f.Next = Math.Max(f.Next, d.Id + 1);
                }
            }
            return f;
        }
    }
}
