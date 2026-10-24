using System;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The working life's parts of the careers line (Careers/CareersStore). Game-free.
    ///
    ///   w  next id|last day|vacancies day|recruitment office until|earned|days worked|pension|benefit until|benefit a week|retired|search firm day|benefit paid|retired early|first step told|recruitment office end told
    ///   o  each vacancy, a each application, j the job now (one), y each job before
    ///      a vacancy: id|profession|rank|kind|employer|street|number|rival|pay|mode|from|to|via|family|level|lead (who passed it on; only here, not in a or j)|its week
    ///      an application: the vacancy, then day|stage|answer|interview day|interview hour|score|offer|asks|lapse|start|turned down on|its week
    ///      the job: the vacancy, then hired|rank day|pay|mode|boss|settled|today|today's minutes|clocked in|due|worked|late|absent|week|week's
    ///      minutes|overtime|burnout|rating|review day|low runs|warnings|raises|politics|politics day|favour|options|vest day|restraint|exclusive|
    ///      notice end|leaving|declared|conflict seen|earned|bonus|rise asked|signed off until|its week|shut day
    ///      a week: the post's working week fitted to its workplace's opening hours (OpenHoursRules.Pack), at the end of each record so a
    ///      save from before it loads (0: fitted again at load) and an older build reads the rest
    ///   y  profession|top rank|kind|employer|from|to|reference|leaving|earned
    ///   f  founder day|profession|top rank|from|restraint until|profession|colleagues|employer|week's pay|claim|tested|colleagues until|count|profession|level|
    ///      practice profession|opened|name|served|late|practice to open
    ///   k  each contract: id|profession|rank|client|street|number|hours|minutes|fee|due|lapse|stage|practice
    /// </summary>
    public static class WorkStore
    {
        public static void Write(TechWriter w, WorkFile f)
        {
            w.Part('w').Record().Add(f.NextId).Add(f.LastDay).Add(f.OpeningsDay).Add(f.AgencyUntil).Add(f.Earned).Add(f.DaysWorked).Add(f.Pension).Add(f.BenefitUntil)
                .Add(f.BenefitWeek).Add(f.Retired).Add(f.HeadhuntDay).Add(f.BenefitPaid).Add(f.RetiredEarly).Add(f.StartTold).Add(f.AgencyTold);
            w.Part('o');
            for (int i = 0; i < f.Openings.Count && i < WorkFile.OpeningsMost; i++)
            {
                Opening o = f.Openings[i];
                Post(w.Record(), o).Add(o.Lead).Add(o.Shift);   // the lead first, then the week: each read in that order
            }
            w.Part('a');
            for (int i = 0; i < f.Applications.Count && i < WorkFile.ApplicationsMost; i++)
            {
                Application a = f.Applications[i];
                Post(w.Record(), a.Post).Add(a.Day).Add(a.Stage).Add(a.Answer).Add(a.InterviewDay).Add(a.InterviewHour).Add(a.Score).Add(a.Offer).Add(a.Asks).Add(a.Lapse).Add(a.StartDay).Add(a.Refused)
                    .Add(a.Post.Shift);
            }
            w.Part('j');
            Job j = f.Job;
            if (j != null)
            {
                Post(w.Record(), j.Post).Add(j.Hired).Add(j.RankDay).Add(j.Pay).Add(j.Work).Add(j.Boss).Add(j.SettledDay).Add(j.TodayDay).Add(j.TodayMinutes).Add(j.ClockIn)
                    .Add(j.Due).Add(j.Worked).Add(j.Late).Add(j.Absent).Add(j.WeekOf).Add(j.WeekMinutes).Add(j.Overtime).Add(j.Burnout).Add(j.Rating).Add(j.ReviewDay).Add(j.LowRuns)
                    .Add(j.Warnings).Add(j.Raises).Add(j.Politics).Add(j.PoliticsDay).Add(j.Favour).Add(j.Options).Add(j.VestDay).Add(j.NonCompete).Add(j.Exclusive).Add(j.NoticeEnd)
                    .Add(j.Leaving).Add(j.Declared).Add(j.ConflictSeen).Add(j.Earned).Add(j.Bonus).Add(j.AskDay).Add(j.OffUntil).Add(j.Post.Shift).Add(j.ShutDay);
            }
            w.Part('y');
            for (int i = 0; i < f.History.Count; i++)
            {
                Spell s = f.History[i];
                w.Record().Add(s.Prof).Add(s.Top).Add(s.Kind).Add(s.Employer).Add(s.From).Add(s.To).Add(s.Reference).Add(s.Leaving).Add(s.Earned);
            }
            w.Part('f').Record().Add(f.FounderDay).Add(f.FounderProf).Add(f.FounderTop).Add(f.FounderFrom).Add(f.RestraintUntil).Add(f.RestraintProf).Add(f.RestraintColleagues)
                .Add(f.RestraintEmployer).Add(f.RestraintWeek).Add(f.ClaimNumber).Add(f.RestraintTested).Add(f.ColleaguesUntil).Add(f.ColleaguesCount).Add(f.ColleaguesProf).Add(f.ColleaguesLevel)
                .Add(f.Practice.Prof).Add(f.Practice.Opened).Add(f.Practice.Reputation).Add(f.Practice.Served).Add(f.Practice.Late).Add(f.PracticeAfter);
            w.Part('k');
            for (int i = 0; i < f.Contracts.Count && i < OwnRules.ContractsMost * 2; i++)
            {
                Contract c = f.Contracts[i];
                w.Record().Add(c.Id).Add(c.Prof).Add(c.Rank).Add(c.Client).Add(c.Street).Add(c.Number).Add(c.Hours).Add(c.Minutes).Add(c.Fee).Add(c.Due).Add(c.Lapse).Add(c.Stage).Add(c.Practice);
            }
        }

        private static TechWriter Post(TechWriter w, Opening o)
        {
            return w.Add(o.Id).Add(o.Prof).Add(o.Rank).Add(o.Kind).Add(o.Employer).Add(o.Street).Add(o.Number).Add(o.Rival).Add(o.Pay).Add(o.Work).Add(o.From).Add(o.To).Add(o.Via)
                .Add(o.Family).Add(o.Level);
        }

        private static bool Post(Fields x, Opening o)
        {
            o.Id = x.Int();
            o.Prof = x.Int(-1);
            o.Rank = Math.Max(0, x.Int());
            o.Kind = TalentCatalogue.Clamp(x.Int(), 0, (int)EmployerKind.Public);
            o.Employer = x.Text();
            o.Street = x.Text();
            o.Number = x.Int();
            o.Rival = x.Text();
            o.Pay = Math.Max(0, x.Int());
            o.Work = TalentCatalogue.Clamp(x.Int(), 0, 2);
            o.From = x.Int();
            o.To = x.Int();
            o.Via = TalentCatalogue.Clamp(x.Int(), 0, (int)Careers.Via.Headhunter);
            o.Family = x.Int(-1);
            o.Level = x.Int(-1);
            Profession p = o.Def;
            if (p == null)
            {
                return false;
            }
            o.Rank = Math.Min(o.Rank, p.Ranks.Length - 1);
            return true;
        }

        // A post's week as saved; 0 for a save from before it, or one this build cannot work (fitted again at load).
        private static int Week(Fields x)
        {
            int shift = x.Int();
            int hours = OpenHoursRules.HoursOf(shift);
            return OpenHoursRules.DaysOf(shift) != 0 && hours > 0 && OpenHoursRules.HourOf(shift) + hours <= 24 ? shift : 0;
        }

        public static void Read(TechReader r, WorkFile f)
        {
            Fields h = r.One('w');
            f.NextId = Math.Max(1, h.Int(1));
            f.LastDay = h.Int(-1);
            f.OpeningsDay = h.Int(-1);
            f.AgencyUntil = h.Int();
            f.Earned = Math.Max(0, h.Long());
            f.DaysWorked = Math.Max(0, h.Int());
            f.Pension = Math.Max(0, h.Long());
            f.BenefitUntil = h.Int();
            f.BenefitWeek = Math.Max(0, h.Int());
            f.Retired = h.Int();
            f.HeadhuntDay = h.Int();
            f.BenefitPaid = h.Int();
            f.RetiredEarly = h.Bool();
            f.StartTold = h.Bool();
            f.AgencyTold = h.Int();   // (a save from before it: 0, nothing told)
            foreach (Fields x in r.Records('o'))
            {
                var o = new Opening();
                if (Post(x, o) && f.Openings.Count < WorkFile.OpeningsMost)
                {
                    o.Lead = x.Text();   // (a save from before leads: empty)
                    o.Shift = Week(x);   // (a save from before the weeks: 0, fitted again at load)
                    f.Openings.Add(o);
                }
            }
            foreach (Fields x in r.Records('a'))
            {
                var a = new Application();
                if (!Post(x, a.Post))
                {
                    continue;
                }
                a.Day = x.Int();
                a.Stage = TalentCatalogue.Clamp(x.Int(), 0, (int)ApplyStep.Withdrawn);
                a.Answer = x.Int();
                a.InterviewDay = x.Int();
                a.InterviewHour = x.Int();
                a.Score = x.Int();
                a.Offer = Math.Max(0, x.Int());
                a.Asks = Math.Max(0, x.Int());
                a.Lapse = x.Int();
                a.StartDay = x.Int();
                int refused = x.Int(int.MinValue);
                a.Refused = refused != int.MinValue ? Math.Max(-1, refused) : ProRules.RefusedDay(a);   // (a save from before the day was kept: told from what it holds, else no bar)
                a.Post.Shift = Week(x);
                if (f.Applications.Count < WorkFile.ApplicationsMost)
                {
                    f.Applications.Add(a);
                }
            }
            foreach (Fields x in r.Records('j'))
            {
                var j = new Job();
                if (!Post(x, j.Post))
                {
                    break;
                }
                j.Hired = x.Int();
                j.RankDay = x.Int();
                j.Pay = Math.Max(0, x.Int());
                j.Work = TalentCatalogue.Clamp(x.Int(), 0, 2);
                j.Boss = Math.Max(0, x.Int());
                j.SettledDay = x.Int(-1);
                j.TodayDay = x.Int(-1);
                j.TodayMinutes = Math.Max(0, x.Int());
                j.ClockIn = x.Int(-1);
                j.Due = Math.Max(0, x.Int());
                j.Worked = Math.Max(0, x.Int());
                j.Late = Math.Max(0, x.Int());
                j.Absent = Math.Max(0, x.Int());
                j.WeekOf = x.Int(-1);
                j.WeekMinutes = Math.Max(0, x.Int());
                j.Overtime = Math.Max(0, x.Int());
                j.Burnout = TalentCatalogue.Clamp(x.Int(), 0, 1000);
                j.Rating = TalentCatalogue.Clamp(x.Int(), 0, 5);
                j.ReviewDay = x.Int();
                j.LowRuns = Math.Max(0, x.Int());
                j.Warnings = Math.Max(0, x.Int());
                j.Raises = Math.Max(0, x.Int());
                j.Politics = x.Int(-1);
                j.PoliticsDay = x.Int();
                j.Favour = x.Int();
                j.Options = Math.Max(0, x.Int());
                j.VestDay = x.Int();
                j.NonCompete = x.Bool();
                j.Exclusive = x.Bool();
                j.NoticeEnd = x.Int();
                j.Leaving = x.Int();
                j.Declared = x.Bool();
                j.ConflictSeen = x.Int();
                j.Earned = Math.Max(0, x.Long());
                j.Bonus = Math.Max(0, x.Int());
                j.AskDay = x.Int(-1000);
                j.OffUntil = x.Int();
                j.Post.Shift = Week(x);
                j.ShutDay = x.Int(-1);
                f.Job = j;
                break;
            }
            foreach (Fields x in r.Records('y'))
            {
                var s = new Spell
                {
                    Prof = x.Int(-1), Top = Math.Max(0, x.Int()), Kind = x.Int(), Employer = x.Text(), From = x.Int(), To = x.Int(), Reference = TalentCatalogue.Clamp(x.Int(), 0, 3),
                    Leaving = x.Int(), Earned = Math.Max(0, x.Long())
                };
                if (ProCatalogue.Find(s.Prof) != null && f.History.Count < WorkFile.HistoryMost)
                {
                    f.History.Add(s);
                }
            }
            Fields own = r.One('f');
            f.FounderDay = own.Int();
            f.FounderProf = own.Int(-1);
            f.FounderTop = Math.Max(0, own.Int());
            f.FounderFrom = own.Text();
            f.RestraintUntil = own.Int();
            f.RestraintProf = own.Int(-1);
            f.RestraintColleagues = Math.Max(0, own.Int());
            f.RestraintEmployer = own.Text();
            f.RestraintWeek = Math.Max(0, own.Long());
            f.ClaimNumber = own.Int();
            f.RestraintTested = own.Bool();
            f.ColleaguesUntil = own.Int();
            f.ColleaguesCount = TalentCatalogue.Clamp(own.Int(), 0, OwnRules.ColleaguesMost);
            f.ColleaguesProf = own.Int(-1);
            f.ColleaguesLevel = Math.Max(0, own.Int());
            int practice = own.Int(-1);
            f.Practice.Prof = ProCatalogue.Find(practice) != null ? practice : -1;
            f.Practice.Opened = own.Int();
            f.Practice.Reputation = TalentCatalogue.Clamp(own.Int(OwnRules.PracticeStart), 0, 100);
            f.Practice.Served = Math.Max(0, own.Int());
            f.Practice.Late = Math.Max(0, own.Int());
            f.PracticeAfter = own.Int(-1);
            foreach (Fields x in r.Records('k'))
            {
                var c = new Contract
                {
                    Id = x.Int(), Prof = x.Int(-1), Rank = Math.Max(0, x.Int()), Client = x.Text(), Street = x.Text(), Number = x.Int(), Hours = Math.Max(1, x.Int(1)), Minutes = Math.Max(0, x.Int()),
                    Fee = Math.Max(0, x.Long()), Due = x.Int(), Lapse = x.Int(), Stage = TalentCatalogue.Clamp(x.Int(), 0, (int)ContractStep.Dropped), Practice = x.Bool()
                };
                if (c.Def != null && f.Contracts.Count < OwnRules.ContractsMost * 2)
                {
                    f.Contracts.Add(c);
                }
            }
        }
    }
}
