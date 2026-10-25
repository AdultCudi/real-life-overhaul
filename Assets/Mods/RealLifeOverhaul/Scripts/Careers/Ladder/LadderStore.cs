using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The management ladder in the save: one line of the save's modData under <see cref="Key"/>, apart from the careers' own so
    /// that a save from before it, or one loaded by an older build, keeps both as they were. Written the way the careers' line is
    /// (Tech/TechCodec); a later version's extra fields at the end of a record are read by this one as nothing. Game-free.
    ///
    ///   h  hired|street|number|employer|rung|seat|aim|rung day|score|recommended|aiming|ask|ask day|ask way|prefer|equipped|insured|handled|lapsed|next task|last day|week
    ///   c  key|name|rival|next person|floor|top wage|band|budget|staffing|escalate
    ///   d  each district: its name
    ///   s  each store: street|number|name|district|needed|schedule|gap days|low days|spent|results|covered|pushed|present
    ///   p  each person: id|game id|name|store|region|skill|wage|morale|want|asked on|role|seat|raised on
    ///   t  each task: id|kind|store|person|about|day|lapse|amount
    /// </summary>
    public static class LadderStore
    {
        public const string Key = "reallife:career_ladder";
        public const int Version = 1;

        public static LadderFile Load(Dictionary<string, string> data, out int version)
        {
            return TechLines.Load(data, Key, Version, Read, out version);
        }

        public static string Write(LadderFile f)
        {
            var w = new TechWriter(Version);
            w.Part('h').Record().Add(f.Hired).Add(f.Street).Add(f.Number).Add(f.Employer).Add(f.Rung).Add(f.Seat).Add(f.Aim).Add(f.RungDay).Add(f.Score).Add(f.Recommended)
                .Add(f.Aiming).Add(f.Ask).Add(f.AskDay).Add(f.AskWay).Add(f.Prefer).Add(f.Equipped).Add(f.Insured).Add(f.Handled).Add(f.Lapsed).Add(f.NextTask).Add(f.LastDay).Add(f.Week);
            CityCompany c = f.Company;
            CompanyPolicy p = c.Policy;
            w.Part('c').Record().Add(c.Key).Add(c.Name).Add(c.Rival).Add(c.NextPerson).Add(p.Floor).Add(p.WageTop).Add(p.Band).Add(p.Budget).Add(p.Staffing).Add(p.Escalate);
            w.Part('d');
            for (int i = 0; i < c.Districts.Count; i++)
            {
                w.Record().Add(c.Districts[i]);
            }
            w.Part('s');
            for (int i = 0; i < c.Stores.Count && i < CareerLadderRules.StoresMost; i++)
            {
                Outlet s = c.Stores[i];
                w.Record().Add(s.Street).Add(s.Number).Add(s.Name).Add(s.Region).Add(s.Needed).Add(s.Rota).Add(s.GapDays).Add(s.LowDays).Add(s.Spent).Add(s.Results).Add(s.Covered)
                    .Add(s.Pushed).Add(s.Present);
            }
            w.Part('p');
            for (int i = 0; i < c.People.Count; i++)
            {
                Colleague x = c.People[i];
                w.Record().Add(x.Id).Add(x.GameId).Add(x.Name).Add(x.Store).Add(x.Region).Add(x.Skill).Add(x.Wage).Add(x.Morale).Add(x.Want).Add(x.WantDay).Add(x.Role).Add(x.Seat)
                    .Add(x.RaisedDay);
            }
            w.Part('t');
            for (int i = 0; i < f.Tasks.Count && i < CareerLadderRules.TasksMost; i++)
            {
                LadderTask t = f.Tasks[i];
                w.Record().Add(t.Id).Add(t.Kind).Add(t.Store).Add(t.Person).Add(t.About).Add(t.Day).Add(t.Lapse).Add(t.Amount);
            }
            return w.ToString();
        }

        public static LadderFile Read(string text, out int version)
        {
            var r = new TechReader(text);
            version = r.Version;
            var f = new LadderFile();
            Fields h = r.One('h');
            f.Hired = h.Int();
            f.Street = h.Text();
            f.Number = h.Int();
            f.Employer = h.Text();
            f.Rung = TalentCatalogue.Clamp(h.Int(), 0, LadderRanks.CareerTop);
            f.Seat = TalentCatalogue.Clamp(h.Int(), 0, (int)ExecSeat.Cfo);
            f.Aim = TalentCatalogue.Clamp(h.Int(), 0, (int)ExecSeat.Cfo);
            f.RungDay = h.Int();
            f.Score = TalentCatalogue.Clamp(h.Int(CareerLadderRules.ScoreStart * 10), 0, 1000);
            f.Recommended = h.Bool();
            f.Aiming = h.Bool(true);
            f.Ask = TalentCatalogue.Clamp(h.Int(), 0, (int)StaffWant.Insurance);
            f.AskDay = h.Int();
            f.AskWay = TalentCatalogue.Clamp(h.Int(), -1, 1);
            f.Prefer = TalentCatalogue.Clamp(h.Int(), -2, 2);
            f.Equipped = h.Bool();
            f.Insured = h.Bool();
            f.Handled = Math.Max(0, h.Int());
            f.Lapsed = Math.Max(0, h.Int());
            f.NextTask = Math.Max(1, h.Int(1));
            f.LastDay = h.Int(-1);
            f.Week = h.Int(-1);
            CityCompany c = f.Company;
            Fields ch = r.One('c');
            c.Key = ch.Text();
            c.Name = ch.Text();
            c.Rival = ch.Text();
            c.NextPerson = Math.Max(1, ch.Int(1));
            CompanyPolicy p = c.Policy;
            p.Floor = TalentCatalogue.Clamp(ch.Int(p.Floor), 0, 100);
            p.WageTop = Math.Max(CareerLadderRules.WageLeast, ch.Int(p.WageTop));
            p.Band = TalentCatalogue.Clamp(ch.Int(p.Band), 0, 5000);
            p.Budget = Math.Max(0, ch.Int(p.Budget));
            p.Staffing = TalentCatalogue.Clamp(ch.Int(p.Staffing), 0, 2);
            p.Escalate = TalentCatalogue.Clamp(ch.Int(p.Escalate), 1, 30);
            foreach (Fields x in r.Records('d'))
            {
                c.Districts.Add(x.Text());
            }
            foreach (Fields x in r.Records('s'))
            {
                if (c.Stores.Count >= CareerLadderRules.StoresMost)
                {
                    break;
                }
                var s = new Outlet
                {
                    Street = x.Text(), Number = x.Int(), Name = x.Text(), Region = Math.Max(0, x.Int()), Needed = Math.Max(1, x.Int(1)),
                        Rota = TalentCatalogue.Clamp(x.Int(1), 0, 2),
                    GapDays = Math.Max(0, x.Int()), LowDays = Math.Max(0, x.Int()), Spent = Math.Max(0, x.Int()), Results = TalentCatalogue.Clamp(x.Int(), 0, 100),
                        Covered = x.Int(-1),
                    Pushed = x.Int(-1), Present = x.Int(-1)
                };
                c.Stores.Add(s);
            }
            while (c.Districts.Count <= MostRegion(c))
            {
                c.Districts.Add(string.Empty);   // (a district lost from the line: the stores keep theirs)
            }
            foreach (Fields x in r.Records('p'))
            {
                var o = new Colleague
                {
                    Id = x.Int(), GameId = x.Text(), Name = x.Text(), Store = x.Int(-1), Region = x.Int(-1), Skill = TalentCatalogue.Clamp(x.Int(), 0, 100),
                        Wage = Math.Max(0, x.Int()),
                    Morale = TalentCatalogue.Clamp(x.Int(CareerLadderRules.MoraleBase), 0, 100), Want = TalentCatalogue.Clamp(x.Int(), 0, (int)StaffWant.Insurance),
                        WantDay = x.Int(),
                    Role = TalentCatalogue.Clamp(x.Int(), 0, LadderRanks.CareerTop), Seat = TalentCatalogue.Clamp(x.Int(), 0, (int)ExecSeat.Cfo), RaisedDay = x.Int(-1000)
                };
                if (o.Store < c.Stores.Count && o.Id > 0 && c.PersonOf(o.Id) == null)
                {
                    c.People.Add(o);
                    c.NextPerson = Math.Max(c.NextPerson, o.Id + 1);
                }
            }
            foreach (Fields x in r.Records('t'))
            {
                var t = new LadderTask
                {
                    Id = x.Int(), Kind = TalentCatalogue.Clamp(x.Int(), 0, (int)TaskKind.Appoint), Store = x.Int(-1), Person = x.Int(-1), About = x.Int(), Day = x.Int(),
                        Lapse = x.Int(),
                    Amount = Math.Max(0, x.Int())
                };
                if (t.Store >= 0 && t.Store < c.Stores.Count && f.Tasks.Count < CareerLadderRules.TasksMost && f.TaskOf(t.Id) == null)
                {
                    f.Tasks.Add(t);
                    f.NextTask = Math.Max(f.NextTask, t.Id + 1);
                }
            }
            return f;
        }

        private static int MostRegion(CityCompany c)
        {
            int most = -1;
            for (int i = 0; i < c.Stores.Count; i++)
            {
                most = Math.Max(most, c.Stores[i].Region);
            }
            return most;
        }
    }
}
