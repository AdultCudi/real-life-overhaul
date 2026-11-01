using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The other tech firms' markets in the save: one line under <see cref="Key"/> in <see cref="TechWriter"/>'s format, a
    /// header (h), the city last read (c), the markets' anchors (a), the companies' names and tender wins (k), the rival
    /// products (r), the player's firms' records (f) and products' records (p), each record's fields in the order
    /// <see cref="Write"/> writes them. New fields go at the end of a record; a record with fewer reads the rest as they
    /// start. A save without the line loads: the markets are set up from the firms as they stand, so nothing jumps
    /// (SectorRules.Convert). The city, the companies and the rivals are also what the shared city carries in a Going
    /// Public session (<see cref="SectorShare"/>). Game-free.
    /// </summary>
    public static class SectorStore
    {
        public const string Key = "reallife:tech_sector";
        public const int Version = 1;

        public static SectorFile Load(Dictionary<string, string> data, out int version) => TechLines.Load(data, Key, Version, Read, out version);

        public static string Write(SectorFile f)
        {
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(f.Seed).Add(f.FirstDay).Add(f.LastDay).Add(f.Converted).Add(f.NextRival).Add(f.DigitalAnchor).Add(f.AnchorDay)
                .Add(f.NextSpike).Add(f.SpikeFrom).Add(f.SpikeUntil).Add(f.SpikeFactor).Add(f.Hedged).Add(f.ThrottleUntil).Add(f.FailoverUntil).Add(f.LastCapacity).Add(f.ColoTarget);
            WriteCity(w, f);
            w.Part('a').Record();
            for (int m = 0; m < f.Anchor.Length; m++)
            {
                w.Add(f.Anchor[m]);
            }
            WriteCompanies(w, f);
            WriteRivals(w, f);
            w.Part('f');
            foreach (SectorFirm s in f.Firms)
            {
                w.Record().Place(s.Place).Add(s.LastOutage).Add(s.LostToOutage).Add(s.LastBigAsk).Add(s.OfferCarry).Add(s.PortfolioUntil).Add(s.PitchTender).Add(s.PitchWeight)
                    .Add(s.SprintUntil).Add(s.Sprint).Add(s.Release).Add(s.DivertUntil).Add(s.ReliableUntil).Add(s.IncidentFrom);
            }
            w.Part('p');
            foreach (SectorProduct p in f.Products)
            {
                w.Record().Add(p.Id).Add(p.Benchmark).Add(p.Generation).Add(p.BoostUntil).Add(p.Boost).Add(p.DiscountUntil).Add(p.TrainedDay).Add(p.Data);
            }
            return w.ToString();
        }

        /// <summary>The city the markets are sized from, one record: a part the shared city carries.</summary>
        internal static void WriteCity(TechWriter w, SectorFile f)
        {
            SectorCityFigures c = f.City;
            w.Part('c').Record().Add(c.People).Add(c.Small).Add(c.Mid).Add(c.Large).Add(c.Digital).Add(c.Known);
        }

        internal static void WriteCompanies(TechWriter w, SectorFile f)
        {
            w.Part('k').Record();
            for (int i = 0; i < f.CompanyRep.Length; i++)
            {
                w.Add(f.CompanyRep[i]).Add(f.CompanyWon[i]);
            }
        }

        internal static void WriteRivals(TechWriter w, SectorFile f)
        {
            w.Part('r');
            foreach (RivalProduct r in f.Rivals)
            {
                w.Record().Add(r.Id).Add(r.Company).Add(r.Startup).Add(r.Stage).Add(r.Market).Add(r.Name).Add(r.Features).Add(r.Bugs).Add(r.Progress).Add(r.Tier).Add(r.Customers)
                    .Add(r.Peak).Add(r.PeakDay).Add(r.LastShip).Add(r.LastCut).Add(r.Carry).Add(r.Compute);
            }
        }

        internal static void ReadCity(TechReader r, SectorFile f)
        {
            Fields c = r.One('c');
            f.City.Set(c.Int(SectorCatalogue.ReferencePeople), c.Int(SectorCatalogue.ReferenceSmall), c.Int(SectorCatalogue.ReferenceMid), c.Int(SectorCatalogue.ReferenceLarge),
                SectorRules.Clamp(c.Int(1000), SectorCatalogue.DigitalLeast, SectorCatalogue.DigitalMost), c.Bool());
        }

        internal static void ReadCompanies(TechReader r, SectorFile f)
        {
            Fields k = r.One('k');
            for (int i = 0; i < f.CompanyRep.Length; i++)
            {
                f.CompanyRep[i] = SectorRules.Clamp(k.Int(SectorCatalogue.CompanyReputation[i]), 0, 1000);
                f.CompanyWon[i] = k.Int();
            }
        }

        internal static void ReadRivals(TechReader r, SectorFile f)
        {
            f.Rivals.Clear();
            foreach (Fields x in r.Records('r'))
            {
                var p = new RivalProduct
                {
                    Id = x.Int(), Company = SectorRules.Clamp(x.Int(-1), -1, SectorCatalogue.CompanyMarkets.Length - 1), Startup = x.Int(), Stage = Math.Max(0, x.Int()),
                    Market = SectorRules.Clamp(x.Int(), 0, (int)SectorMarket.Count - 1), Name = x.Text(), Features = Math.Max(0, x.Int(1)), Bugs = Math.Max(0, x.Int()),
                    Progress = Math.Max(0L, x.Long()), Tier = SectorRules.Clamp(x.Int(1), 0, 2), Customers = Math.Max(0L, x.Long()), Peak = Math.Max(0L, x.Long()), PeakDay = x.Int(),
                    LastShip = x.Int(), LastCut = x.Int(), Carry = x.Long(), Compute = Math.Max(0L, x.Long())
                };
                if (f.Rivals.Count < SectorCatalogue.RivalProductsMost)
                {
                    f.Rivals.Add(p);
                }
            }
        }

        public static SectorFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var f = new SectorFile();
            Fields h = r.One('h');
            f.Seed = h.Int(1);
            f.FirstDay = h.Int();
            f.LastDay = h.Int(-1);
            f.Converted = h.Bool();
            f.NextRival = Math.Max(1, h.Int(1));
            f.DigitalAnchor = Math.Max(0L, h.Long());
            f.AnchorDay = h.Int();
            f.NextSpike = h.Int();
            f.SpikeFrom = h.Int();
            f.SpikeUntil = h.Int();
            f.SpikeFactor = SectorRules.Clamp(h.Int(1000), 1000, SectorCatalogue.SpikeMost);
            f.Hedged = h.Bool();
            f.ThrottleUntil = h.Int();
            f.FailoverUntil = h.Int();
            f.LastCapacity = h.Int(-1000);
            f.ColoTarget = h.Int(-1);
            ReadCity(r, f);
            Fields a = r.One('a');
            for (int m = 0; m < f.Anchor.Length; m++)
            {
                f.Anchor[m] = Math.Max(0, a.Int(1000));
            }
            ReadCompanies(r, f);
            ReadRivals(r, f);
            foreach (Fields x in r.Records('f'))
            {
                var s = new SectorFirm
                {
                    Place = x.Place(), LastOutage = x.Int(-1000), LostToOutage = Math.Max(0L, x.Long()), LastBigAsk = x.Int(-1000), OfferCarry = Math.Max(0L, x.Long()),
                    PortfolioUntil = x.Int(), PitchTender = x.Int(), PitchWeight = x.Int(), SprintUntil = x.Int(), Sprint = SectorRules.Clamp(x.Int(1), 0, 2),
                    Release = SectorRules.Clamp(x.Int(1), 0, 1), DivertUntil = x.Int(), ReliableUntil = x.Int(), IncidentFrom = x.Int()
                };
                if (s.Place.Length > 0 && f.Firms.Count < 64)
                {
                    f.Firms.Add(s);
                }
            }
            foreach (Fields x in r.Records('p'))
            {
                var p = new SectorProduct
                {
                    Id = x.Int(), Benchmark = SectorRules.Clamp(x.Int(), 0, 100), Generation = Math.Max(0, x.Int()), BoostUntil = x.Int(), Boost = Math.Max(0, x.Int()),
                    DiscountUntil = x.Int(), TrainedDay = x.Int(-1000), Data = Math.Max(0, x.Int())
                };
                if (f.Products.Count < FirmsCatalogue.ProductsStored)
                {
                    f.Products.Add(p);
                }
            }
            return f;
        }
    }

    /// <summary>
    /// The other tech firms' shared market in a Going Public session: one part of the shared city (Core/SharedCity, letter
    /// <see cref="Part"/>), written by the host and applied on the guests, so every player sells into markets of the same
    /// size against the same rival products, prices and power price. Only the city's figures, the companies and the rivals
    /// travel: each player's own firms, products and desk answers stay on their own machine (never another player's things).
    /// A message of another version of the mod is never applied (SharedCity), so an older or absent RLO never reads it. Game-free.
    /// </summary>
    public static class SectorShare
    {
        public const char Part = 'R';
        public const int Version = 1;

        /// <summary>The host's markets as they stand, whole.</summary>
        public static string Write(SectorFile f)
        {
            if (f == null)
            {
                return null;
            }
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(f.NextRival).Add(f.NextSpike).Add(f.SpikeFrom).Add(f.SpikeUntil).Add(f.SpikeFactor).Add(f.FirstDay).Add(f.AnchorDay);
            SectorStore.WriteCity(w, f);
            w.Part('a').Record();
            for (int m = 0; m < f.Anchor.Length; m++)
            {
                w.Add(f.Anchor[m]);
            }
            SectorStore.WriteCompanies(w, f);
            SectorStore.WriteRivals(w, f);
            return w.ToString();
        }

        /// <summary>A guest takes the host's markets in place of its own; false (nothing changed) for a line it cannot read.</summary>
        public static bool Apply(SectorFile f, string text)
        {
            if (f == null || string.IsNullOrEmpty(text))
            {
                return false;
            }
            try
            {
                var r = new TechReader(text);
                if (r.Version != Version)
                {
                    return false;
                }
                var read = new SectorFile();
                Fields h = r.One('h');
                read.NextRival = Math.Max(1, h.Int(1));
                read.NextSpike = h.Int();
                read.SpikeFrom = h.Int();
                read.SpikeUntil = h.Int();
                read.SpikeFactor = SectorRules.Clamp(h.Int(1000), 1000, SectorCatalogue.SpikeMost);
                read.FirstDay = h.Int(f.FirstDay);
                read.AnchorDay = h.Int();
                SectorStore.ReadCity(r, read);
                Fields a = r.One('a');
                for (int m = 0; m < read.Anchor.Length; m++)
                {
                    read.Anchor[m] = Math.Max(0, a.Int(1000));
                }
                SectorStore.ReadCompanies(r, read);
                SectorStore.ReadRivals(r, read);
                f.NextRival = read.NextRival;
                if (read.SpikeFrom != f.SpikeFrom)
                {
                    f.Hedged = false;   // a new spike: this player has not answered it yet
                }
                f.NextSpike = read.NextSpike;
                f.SpikeFrom = read.SpikeFrom;
                f.SpikeUntil = read.SpikeUntil;
                f.SpikeFactor = read.SpikeFactor;
                f.FirstDay = read.FirstDay;   // the world beyond the city grows from the host's first day
                f.AnchorDay = read.AnchorDay;  // the host's markets are held up for the host's older save, and sized so on every machine
                Array.Copy(read.Anchor, f.Anchor, f.Anchor.Length);
                f.Converted = true;
                f.City.CopyFrom(read.City);
                Array.Copy(read.CompanyRep, f.CompanyRep, f.CompanyRep.Length);
                Array.Copy(read.CompanyWon, f.CompanyWon, f.CompanyWon.Length);
                f.Rivals.Clear();
                f.Rivals.AddRange(read.Rivals);
                return true;
            }
            catch (Exception)
            {
                return false;   // read whole first: a broken line changes nothing
            }
        }
    }
}
