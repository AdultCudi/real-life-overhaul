using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The terms of every round and the board's say over big spends, in a line of their own beside the ventures' (<see cref="VentureStore"/> keeps its fields as they
    /// were): holders of the player's firms (c) and of the city's startups (s) by their place in the cap table, the offers on the table (t) and the rights of each
    /// firm (r). A save without the line, or an older RLO that never wrote it, leaves every line at the plain terms: one times, not participating, no anti-dilution,
    /// the vesting it had, a monthly pack and no provision. Game-free.
    /// </summary>
    public static class TermsStore
    {
        public const string Key = "reallife:tech_terms";
        public const int Version = 1;

        /// <summary>The line read into the venture file's holders, offers and firms; a save without one changes nothing.</summary>
        public static void Apply(Dictionary<string, string> data, VentureFile f)
        {
            TermsFile read = f != null ? TechLines.Load(data, Key, Version, Read, out _) : null;
            read?.Apply(f);
        }

        /// <summary>The line, or null when every line is plain (so an old save and a plain one write nothing).</summary>
        public static string Write(VentureFile f)
        {
            var w = new TechWriter(Version);
            int lines = 0;
            w.Part('h').Record().Add(0);
            w.Part('c');
            foreach (Cap c in f.Caps)
            {
                for (int i = 0; i < c.Holders.Count; i++)
                {
                    if (c.Holders[i].T != null)
                    {
                        Fill(w.Record().Place(c.Place).Add(i), c.Holders[i].T);
                        lines++;
                    }
                }
            }
            w.Part('s');
            foreach (Startup s in f.Startups)
            {
                for (int i = 0; i < s.Holders.Count; i++)
                {
                    if (s.Holders[i].T != null)
                    {
                        Fill(w.Record().Add(s.Id).Add(i), s.Holders[i].T);
                        lines++;
                    }
                }
            }
            w.Part('t');
            foreach (Cap c in f.Caps)
            {
                foreach (TermSheet t in c.Sheets)
                {
                    if (t.T != null || t.Pushed)
                    {
                        Fill(w.Record().Place(c.Place).Add(t.Vc), t.T ?? new Terms()).Add(t.Pushed);
                        lines++;
                    }
                }
            }
            w.Part('r');
            foreach (Cap c in f.Caps)
            {
                CapRights r = c.Rt;
                if (r.Breaches > 0 || r.Approved > 0 || r.ApprovedUntil > 0 || r.RefusedUntil > 0 || r.RaiseStage != -2 || r.SlipDay > 0)
                {
                    w.Record().Place(c.Place).Add(r.Breaches).Add(r.BreachDay).Add(r.Approved).Add(r.ApprovedUntil).Add(r.RefusedUntil).Add(r.RaiseStage).Add(r.SlipDay);
                    lines++;
                }
            }
            return lines > 0 ? w.ToString() : null;
        }

        private static TechWriter Fill(TechWriter w, Terms t)
        {
            return w.Add(t.Pref).Add(t.Participating).Add(t.Anti).Add(t.Vest).Add(t.Info).Add(t.Provision);
        }

        private static Terms Line(Fields x)
        {
            return new Terms
            {
                Pref = Nearest(x.Int(1000)), Participating = x.Bool(), Anti = TalentCatalogue.Clamp(x.Int(), 0, TermsCatalogue.AntiRatchet),
                Vest = TalentCatalogue.Clamp(x.Int(), 0, 1), Info = TalentCatalogue.Clamp(x.Int(), 0, TermsCatalogue.InfoDemand), Provision = Math.Max(0L, x.Long())
            };
        }

        private static int Nearest(int pref) => pref >= 1750 ? 2000 : pref >= 1250 ? 1500 : 1000;

        private sealed class TermsFile
        {
            public readonly List<(string Place, int Index, Terms T)> Caps = new List<(string, int, Terms)>(8);
            public readonly List<(int Id, int Index, Terms T)> Startups = new List<(int, int, Terms)>(16);
            public readonly List<(string Place, int Vc, Terms T, bool Pushed)> Sheets = new List<(string, int, Terms, bool)>(3);
            public readonly List<(string Place, CapRights R)> Rights = new List<(string, CapRights)>(4);

            public void Apply(VentureFile f)
            {
                foreach (var l in Caps)
                {
                    Cap c = f.CapOf(l.Place);
                    if (c != null && l.Index >= 0 && l.Index < c.Holders.Count && !l.T.Standard)
                    {
                        c.Holders[l.Index].T = l.T;
                    }
                }
                foreach (var l in Startups)
                {
                    Startup s = f.StartupOf(l.Id);
                    if (s != null && l.Index >= 0 && l.Index < s.Holders.Count && !l.T.Standard)
                    {
                        s.Holders[l.Index].T = l.T;
                    }
                }
                foreach (var l in Sheets)
                {
                    Cap c = f.CapOf(l.Place);
                    for (int i = 0; c != null && i < c.Sheets.Count; i++)
                    {
                        if (c.Sheets[i].Vc == l.Vc)
                        {
                            c.Sheets[i].T = l.T.Standard ? null : l.T;
                            c.Sheets[i].Pushed = l.Pushed;
                        }
                    }
                }
                foreach (var l in Rights)
                {
                    Cap c = f.CapOf(l.Place);
                    if (c != null)
                    {
                        CapRights r = c.Rt;
                        r.Breaches = l.R.Breaches;
                        r.BreachDay = l.R.BreachDay;
                        r.Approved = l.R.Approved;
                        r.ApprovedUntil = l.R.ApprovedUntil;
                        r.RefusedUntil = l.R.RefusedUntil;
                        r.RaiseStage = l.R.RaiseStage;
                        r.SlipDay = l.R.SlipDay;
                    }
                }
            }
        }

        private static TermsFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var f = new TermsFile();
            foreach (Fields x in r.Records('c'))
            {
                string place = x.Place();
                int index = x.Int(-1);
                f.Caps.Add((place, index, Line(x)));
            }
            foreach (Fields x in r.Records('s'))
            {
                int id = x.Int(), index = x.Int(-1);
                f.Startups.Add((id, index, Line(x)));
            }
            foreach (Fields x in r.Records('t'))
            {
                string place = x.Place();
                int vc = x.Int(-1);
                Terms t = Line(x);
                f.Sheets.Add((place, vc, t, x.Bool()));
            }
            foreach (Fields x in r.Records('r'))
            {
                string place = x.Place();
                var rights = new CapRights
                {
                    Breaches = Math.Max(0, x.Int()), BreachDay = x.Int(), Approved = Math.Max(0L, x.Long()), ApprovedUntil = x.Int(), RefusedUntil = x.Int(), RaiseStage = x.Int(-2), SlipDay = x.Int()
                };
                f.Rights.Add((place, rights));
            }
            return f;
        }
    }
}
