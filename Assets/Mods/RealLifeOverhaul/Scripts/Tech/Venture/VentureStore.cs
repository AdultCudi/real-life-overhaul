using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// Startups, the player's fund and their firms' cap tables in the save: one line under <see cref="Key"/> in
    /// <see cref="TechWriter"/>'s format, a header (h), the fund (f), the venture firms' doors (v), startups (s) and their holders
    /// (a), the player's firms (c), their holders (k) and term sheets (t), fields in the order <see cref="Write"/> writes them.
    /// </summary>
    public static class VentureStore
    {
        public const string Key = "reallife:tech_ventures";
        public const int Version = 1;

        public static VentureFile Load(Dictionary<string, string> data, out int version) => TechLines.Load(data, Key, Version, Read, out version);

        private static void Line(TechWriter w, Holder h)
        {
            w.Add(h.Kind).Add(h.Who).Add(h.Shares).Add(h.Invested).Add(h.Preference).Add(h.Round).Add(h.ProRata).Add(h.Board);
        }

        private static Holder Line(Fields x)
        {
            return new Holder { Kind = x.Int(), Who = x.Int(), Shares = x.Long(), Invested = x.Long(), Preference = x.Long(), Round = x.Int(), ProRata = x.Bool(), Board = x.Bool() };
        }

        public static string Write(VentureFile f)
        {
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(f.Seed).Add(f.FirstDay).Add(f.LastDay).Add(f.NextId);
            w.Part('f').Record().Add(f.Fund.Open).Add(f.Fund.Committed).Add(f.Fund.Cash).Add(f.Fund.Invested).Add(f.Fund.Returned);
            w.Part('v').Record();
            for (int i = 0; i < f.ClosedUntil.Length; i++)
            {
                w.Add(f.ClosedUntil[i]);
            }
            w.Part('s');
            foreach (Startup s in f.Startups)
            {
                TermSheet a = s.Ask;
                w.Record().Add(s.Id).Add(s.Name).Add(s.Founder).Add(s.Sector).Add(s.Stage).Add(s.State).Add(s.Founded).Add(s.Quality).Add(s.Mrr).Add(s.ClaimBp).Add(s.Cash).Add(s.Burn).Add(s.Post)
                    .Add(s.LastRound).Add(s.FailedRaises).Add(s.Carry).Add(s.Diligence).Add(s.Revealed).Add(a != null ? a.Amount : 0).Add(a != null ? a.Pre : 0).Add(a != null ? a.Lapse : 0)
                    .Add(a != null && a.Board).Add(a != null && a.ProRata);
            }
            w.Part('a');
            foreach (Startup s in f.Startups)
            {
                foreach (Holder h in s.Holders)
                {
                    w.Record().Add(s.Id);
                    Line(w, h);
                }
            }
            w.Part('c');
            foreach (Cap c in f.Caps)
            {
                w.Record().Place(c.Place).Add(c.Stage).Add(c.Post).Add(c.BoardSeats).Add(c.VcSeats).Add(c.TargetMrr).Add(c.TargetDay).Add(c.Misses).Add(c.Pressure).Add(c.Ceo).Add(c.RaiseUntil)
                    .Add(c.Meetings).Add(c.Bridge).Add(c.BridgeDay).Add(c.OptionsGranted).Add(c.LastMrr).Add(c.Unassigned).Add(c.OptionsChecked);
            }
            w.Part('k');
            foreach (Cap c in f.Caps)
            {
                foreach (Holder h in c.Holders)
                {
                    w.Record().Place(c.Place);
                    Line(w, h);
                }
            }
            w.Part('t');
            foreach (Cap c in f.Caps)
            {
                foreach (TermSheet t in c.Sheets)
                {
                    w.Record().Place(c.Place).Add(t.Vc).Add(t.Amount).Add(t.Pre).Add(t.Board).Add(t.ProRata).Add(t.Lapse).Add(t.Score);
                }
            }
            return w.ToString();
        }

        public static VentureFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var f = new VentureFile();
            Fields h = r.One('h');
            f.Seed = h.Int(1);
            f.FirstDay = h.Int();
            f.LastDay = h.Int(-1);
            f.NextId = Math.Max(1, h.Int(1));
            Fields fund = r.One('f');
            f.Fund.Open = fund.Bool();
            f.Fund.Committed = fund.Long();
            f.Fund.Cash = fund.Long();
            f.Fund.Invested = fund.Long();
            f.Fund.Returned = fund.Long();
            Fields closed = r.One('v');
            for (int i = 0; i < f.ClosedUntil.Length; i++)
            {
                f.ClosedUntil[i] = closed.Int();
            }
            foreach (Fields x in r.Records('s'))
            {
                var s = new Startup
                {
                    Id = x.Int(), Name = x.Text(), Founder = x.Text(), Sector = TalentCatalogue.Clamp(x.Int(), 0, (int)Venture.Count - 1),
                        Stage = TalentCatalogue.Clamp(x.Int(), 0, (int)RoundStage.Count - 1),
                    State = x.Int(), Founded = x.Int(), Quality = x.Int(500), Mrr = x.Long(), ClaimBp = x.Int(), Cash = x.Long(), Burn = x.Long(), Post = x.Long(), LastRound = x.Int(),
                    FailedRaises = x.Int(), Carry = x.Long(), Diligence = x.Bool(), Revealed = x.Int()
                };
                long amount = x.Long(), pre = x.Long();
                int lapse = x.Int();
                bool board = x.Bool(), proRata = x.Bool();
                if (amount > 0)
                {
                    s.Ask = new TermSheet { Amount = amount, Pre = pre, Lapse = lapse, Board = board, ProRata = proRata };
                }
                if (f.Startups.Count < VentureCatalogue.StartupsMost * 3)
                {
                    f.Startups.Add(s);
                }
            }
            foreach (Fields x in r.Records('a'))
            {
                Startup s = f.StartupOf(x.Int());
                Holder line = Line(x);
                if (s != null && s.Holders.Count < 24)
                {
                    s.Holders.Add(line);
                }
            }
            foreach (Fields x in r.Records('c'))
            {
                var c = new Cap
                {
                    Place = x.Place(), Stage = x.Int(-1), Post = x.Long(), BoardSeats = Math.Max(1, x.Int(1)), VcSeats = x.Int(), TargetMrr = x.Long(), TargetDay = x.Int(), Misses = x.Int(),
                    Pressure = x.Int(), Ceo = x.Text(), RaiseUntil = x.Int(), Meetings = x.Int(), Bridge = x.Long(), BridgeDay = x.Int(), OptionsGranted = x.Int(), LastMrr = x.Long(),
                    Unassigned = Math.Max(0L, x.Long()), OptionsChecked = x.Int()   // (a line from before 0.12.4 reads 0 and 0)
                };
                if (f.Caps.Count < 32)
                {
                    f.Caps.Add(c);
                }
            }
            foreach (Fields x in r.Records('k'))
            {
                Cap c = f.CapOf(x.Place());
                Holder line = Line(x);
                if (c != null && c.Holders.Count < 24)
                {
                    c.Holders.Add(line);
                }
            }
            foreach (Fields x in r.Records('t'))
            {
                Cap c = f.CapOf(x.Place());
                var t = new TermSheet { Vc = x.Int(), Amount = x.Long(), Pre = x.Long(), Board = x.Bool(), ProRata = x.Bool(), Lapse = x.Int(), Score = x.Int() };
                if (c != null && c.Sheets.Count < VentureCatalogue.VcMeetingsMost)
                {
                    c.Sheets.Add(t);
                }
            }
            return f;
        }
    }
}
