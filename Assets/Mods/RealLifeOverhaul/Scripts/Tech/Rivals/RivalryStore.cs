using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// Competition in niches in the save: one line under <see cref="RivalryCatalogue.Key"/> in <see cref="TechWriter"/>'s format, a header (h), the day each niche last held a bake
    /// off (n), what is kept of each rival product (r), what the player's firms looked like when it last looked (s), the reactions for their pages (l), the bake offs (b) and who
    /// entered them (e). A save without the line loads with nothing in it, and an older RLO never reads it. <c>reallife:tech_ventures</c> and the sector's line are untouched.
    /// </summary>
    public static class RivalryStore
    {
        public const string Key = RivalryCatalogue.Key;
        public const int Version = 1;

        public static RivalryFile Load(Dictionary<string, string> data, out int version) => TechLines.Load(data, Key, Version, Read, out version);

        public static string Write(RivalryFile file)
        {
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(file.Since).Add(file.NextBake);
            w.Part('n').Record();
            for (int i = 0; i < file.BakeLast.Length; i++)
            {
                w.Add(file.BakeLast[i]);
            }
            w.Part('r');
            foreach (RivalryRec r in file.Recs)
            {
                w.Record().Add(r.Id).Add(r.Entrant).Add(r.Niche).Add(r.Backer).Add(r.BackerDay).Add(r.PeakShare).Add(r.PeakDay).Add(r.ChestStage).Add(r.ChestDay).Add(r.ChestUntil)
                    .Add(r.ChestMoney).Add(r.CopyTarget).Add(r.CopyAt).Place(r.CopyPlace).Add(r.CopyDone).Add(r.PoachNote);
            }
            w.Part('s');
            foreach (RivalrySeen s in file.Seen)
            {
                w.Record().Place(s.Place).Add(s.Invested).Add(s.Features).Add(s.Day);
            }
            w.Part('l');
            foreach (RivalryReaction x in file.Log)
            {
                w.Record().Add(x.Day).Add(x.Kind).Add(x.Rival).Add(x.Name).Place(x.Place).Add(x.Niche).Add(x.Amount).Add(x.Other);
            }
            w.Part('b');
            foreach (BakeOff b in file.Bakes)
            {
                w.Record().Add(b.Id).Add(b.Niche).Add(b.Customer).Add(b.Opened).Add(b.Closes).Add(b.Seats).Add(b.Value).Add(b.State).Add(b.Winner).Place(b.Place).Add(b.Booked);
            }
            w.Part('e');
            foreach (BakeOff b in file.Bakes)
            {
                foreach (BakeEntry e in b.Entries)
                {
                    w.Record().Add(b.Id).Place(e.Place).Add(e.Tactic);
                }
            }
            return w.ToString();
        }

        public static RivalryFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var file = new RivalryFile();
            Fields h = r.One('h');
            file.Since = h.Int(-1);
            file.NextBake = Math.Max(1, h.Int(1));
            Fields n = r.One('n');
            for (int i = 0; i < file.BakeLast.Length; i++)
            {
                file.BakeLast[i] = n.Int();
            }
            foreach (Fields x in r.Records('r'))
            {
                var rec = new RivalryRec
                {
                    Id = x.Int(), Entrant = x.Bool(), Niche = Math.Max(-1, Math.Min(NicheCatalogue.Count - 1, x.Int(-1))),
                    Backer = Math.Max(-1, Math.Min(HiringCatalogue.RivalFirms.Length - 1, x.Int(-1))),
                    BackerDay = x.Int(), PeakShare = Math.Max(0, x.Int()), PeakDay = x.Int(), ChestStage = x.Int(-1), ChestDay = x.Int(), ChestUntil = x.Int(), ChestMoney = Math.Max(0L, x.Long()),
                    CopyTarget = Math.Max(0, x.Int()), CopyAt = x.Int(), CopyPlace = x.Place(), CopyDone = x.Int(), PoachNote = x.Int()
                };
                if (rec.Id > 0 && file.RecOf(rec.Id) == null && file.Recs.Count < SectorCatalogue.RivalProductsMost)
                {
                    file.Recs.Add(rec);
                }
            }
            foreach (Fields x in r.Records('s'))
            {
                var s = new RivalrySeen { Place = x.Place(), Invested = Math.Max(0L, x.Long()), Features = Math.Max(0, x.Int()), Day = x.Int() };
                if (file.Seen.Count < 64 && file.SeenOf(s.Place) == null)
                {
                    file.Seen.Add(s);
                }
            }
            foreach (Fields x in r.Records('l'))
            {
                var l = new RivalryReaction
                {
                    Day = x.Int(), Kind = Math.Max(0, Math.Min(ReactKind.Count - 1, x.Int())), Rival = x.Int(), Name = x.Text(), Place = x.Place(),
                    Niche = Math.Max(-1, Math.Min(NicheCatalogue.Count - 1, x.Int(-1))), Amount = x.Long(), Other = x.Text()
                };
                if (file.Log.Count < RivalryCatalogue.LogMost)
                {
                    file.Log.Add(l);
                }
            }
            foreach (Fields x in r.Records('b'))
            {
                var b = new BakeOff
                {
                    Id = x.Int(), Niche = Math.Max(-1, Math.Min(NicheCatalogue.Count - 1, x.Int(-1))), Customer = x.Text(), Opened = x.Int(), Closes = x.Int(), Seats = Math.Max(0, x.Int()),
                    Value = Math.Max(0L, x.Long()), State = Math.Max(0, Math.Min(BakeState.Missed, x.Int())), Winner = x.Text(), Place = x.Place(), Booked = Math.Max(0L, x.Long())
                };
                if (b.Id > 0 && file.BakeOf(b.Id) == null && file.Bakes.Count < RivalryCatalogue.BakesMost)
                {
                    file.Bakes.Add(b);
                    file.NextBake = Math.Max(file.NextBake, b.Id + 1);
                }
            }
            foreach (Fields x in r.Records('e'))
            {
                BakeOff b = file.BakeOf(x.Int());
                var e = new BakeEntry { Place = x.Place(), Tactic = Math.Max(0, Math.Min(2, x.Int())) };
                if (b != null && b.Entries.Count < 4 && b.EntryOf(e.Place) == null)
                {
                    b.Entries.Add(e);
                }
            }
            return file;
        }
    }
}
