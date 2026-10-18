using System;
using RealLifeOverhaul.Tech;
using UI.Notification;

namespace RealLifeOverhaul.Careers
{
    /// <summary>Working for oneself (Careers/Work/OwnRules): founding or a practice, former colleagues, a restraint clause, freelance contracts and side work.</summary>
    public sealed partial class CareersDirector
    {
        public const int TabOwn = 5;
        private const string ContractKey = "reallife_transaction_own_contract";
        private const string RegisterKey = "reallife_transaction_own_register";

        public static bool OwnOn => Settings.CareersOwn;

        /// <summary>Why the player may not leave to found a company now, or null.</summary>
        public string CanFoundLeave(Job j)
        {
            if (!OwnOn || j == null)
            {
                return "reallife_work_why_no_job";
            }
            if (!OwnRules.CanFound(j.Def) || j.Post.Rank < OwnRules.FoundLeastRank)
            {
                return "reallife_own_why_cannot_found";
            }
            return j.NoticeEnd > 0 ? "reallife_work_why_leaving" : null;
        }

        /// <summary>Why the player may not leave to open a practice now, or null.</summary>
        public string CanPracticeLeave(Job j)
        {
            if (!OwnOn || j == null)
            {
                return "reallife_work_why_no_job";
            }
            if (!OwnRules.CanPractise(j.Def, _study))
            {
                return "reallife_own_why_cannot_practise";
            }
            if (_study.Work.Practice.Open)
            {
                return "reallife_own_why_practice_open";
            }
            return j.NoticeEnd > 0 ? "reallife_work_why_leaving" : null;
        }

        /// <summary>The player hands in their notice to found a company, taking colleagues along or not. Null if done, else a reason key.</summary>
        public string FoundLeave(bool colleagues)
        {
            Job j = Work != null ? Work.Job : null;
            string why = CanFoundLeave(j);
            if (why != null)
            {
                return why;
            }
            WorkFile w = _study.Work;
            j.NoticeEnd = Save.Day + ProRules.NoticeDays(j.Rank, j.Post.Rank);
            j.Leaving = (int)LeaveReason.SpunOut;
            w.ColleaguesCount = colleagues ? Math.Min(OwnRules.ColleaguesMost, j.Post.Rank + 1) : 0;
            w.ColleaguesProf = j.Post.Prof;
            w.ColleaguesLevel = Math.Max(0, j.Rank.Level - 1);
            w.PracticeAfter = -1;
            Tell2(NotificationType.Info, "reallife_own_note_leaving_found", Data("employer", CareersText.Employer(j.Post.Employer), "day", CareersText.Day(j.NoticeEnd)), TabOwn, null);
            Changed();
            return null;
        }

        /// <summary>The player hands in their notice to open a practice in their profession. Null if done, else a reason key.</summary>
        public string PracticeLeave()
        {
            Job j = Work != null ? Work.Job : null;
            string why = CanPracticeLeave(j);
            if (why != null)
            {
                return why;
            }
            j.NoticeEnd = Save.Day + ProRules.NoticeDays(j.Rank, j.Post.Rank);
            j.Leaving = (int)LeaveReason.SpunOut;
            _study.Work.ColleaguesCount = 0;
            _study.Work.PracticeAfter = j.Post.Prof;
            Tell2(NotificationType.Info, "reallife_own_note_leaving_practice", Data("employer", CareersText.Employer(j.Post.Employer), "day", CareersText.Day(j.NoticeEnd)), TabOwn, null);
            Changed();
            return null;
        }

        /// <summary>A job ended: a founder's record, a practice to open, a restraint clause that now binds, colleagues waiting.</summary>
        private void EndedOwn(Job j, LeaveReason why, int day)
        {
            if (!OwnOn)
            {
                return;
            }
            WorkFile w = _study.Work;
            if (j.NonCompete && why != LeaveReason.Retired && why != LeaveReason.Redundant)
            {
                w.RestraintUntil = day + TalentCatalogue.Scaled(OwnRules.RestraintDays, Year);
                w.RestraintProf = j.Post.Prof;
                w.RestraintEmployer = j.Post.Employer;
                w.RestraintWeek = LifeRules.WeekPay(j);
                w.RestraintColleagues = 0;
                w.RestraintTested = false;
                w.ClaimNumber = 0;
            }
            if (why != LeaveReason.SpunOut)
            {
                w.ColleaguesCount = 0;
                w.PracticeAfter = -1;
                return;
            }
            if (w.PracticeAfter >= 0)
            {
                int prof = w.PracticeAfter;
                w.PracticeAfter = -1;
                OpenPractice(prof, day, true);
                return;
            }
            w.FounderDay = day;
            w.FounderProf = j.Post.Prof;
            w.FounderTop = Math.Max(w.FounderTop, j.Post.Rank);
            w.FounderFrom = j.Post.Employer;
            w.ColleaguesUntil = w.ColleaguesCount > 0 ? day + OwnRules.ColleaguesDays : 0;
            Tell2(NotificationType.Success, w.ColleaguesCount > 0 ? "reallife_own_note_founder_colleagues" : "reallife_own_note_founder",
                Data("count", w.ColleaguesCount.ToString(), "days", OwnRules.ColleaguesDays.ToString()), TabOwn, null);
            Story("reallife_own_news_spin_out", "name", PlayerName(), "company", CareersText.Employer(j.Post.Employer));
        }

        /// <summary>What the player's record as a founder adds to a pitch to the venture firms, thousandths.</summary>
        public int FounderBonus()
        {
            WorkFile w = Work;
            if (w == null || !OwnOn || w.FounderDay <= 0 || w.FounderProf < 0)
            {
                return 0;
            }
            return OwnRules.FounderScore(w.FounderTop, ProRules.Experience(w, w.FounderProf, Save.Day, Year), CareersBridges.ReputationNow());
        }

        /// <summary>The firm of the player's that colleagues can join now, or null.</summary>
        public string ColleaguesFirm()
        {
            WorkFile w = Work;
            if (w == null || w.ColleaguesCount <= 0 || Save.Day > w.ColleaguesUntil)
            {
                return null;
            }
            try
            {
                return CareersBridges.TechFirm != null ? CareersBridges.TechFirm() : null;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return null;
            }
        }

        /// <summary>The colleagues who left with the player join their firm. Null if done, else a reason key.</summary>
        public string BringColleagues()
        {
            WorkFile w = Work;
            string place = ColleaguesFirm();
            if (w == null || place == null)
            {
                return "reallife_own_why_no_firm";
            }
            Profession p = ProCatalogue.Find(w.ColleaguesProf);
            int role = p != null ? p.Rank(0).Role : -1;
            if (role < 0)
            {
                role = TalentCatalogue.SoftwareEngineer;
            }
            int joined = 0;
            try
            {
                joined = CareersBridges.JoinFirm != null ? CareersBridges.JoinFirm(place, role, w.ColleaguesLevel, w.ColleaguesCount) : 0;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (joined <= 0)
            {
                return "reallife_own_why_no_firm";
            }
            w.ColleaguesCount = 0;
            w.ColleaguesUntil = 0;
            Tell2(NotificationType.Success, "reallife_own_note_colleagues_joined", Data("count", joined.ToString(), "employer", CareersText.Employer(w.FounderFrom)), TabOwn, null);
            if (w.RestraintUntil >= Save.Day && w.RestraintEmployer == w.FounderFrom)
            {
                w.RestraintColleagues = joined;
                TestRestraint(true);
            }
            Changed();
            return null;
        }

        /// <summary>The former employer looks at what the player did under the clause: a claim, or nothing (once).</summary>
        private void TestRestraint(bool colleagues)
        {
            WorkFile w = _study.Work;
            if (w.RestraintTested || w.RestraintUntil < Save.Day)
            {
                return;
            }
            w.RestraintTested = true;
            if (TechRandom.Mille(StudySeed, 8701, Save.Day, w.RestraintUntil) >= OwnRules.ClaimChance(colleagues))
            {
                return;
            }
            int amount = Prices.At((int)Math.Min(int.MaxValue, OwnRules.ClaimFor(w.RestraintWeek, w.RestraintColleagues)));
            string employer = CareersText.Employer(w.RestraintEmployer);
            int number = 0;
            try
            {
                number = CareersBridges.Claim != null ? CareersBridges.Claim(amount, employer) : 0;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            w.ClaimNumber = number;
            Tell2(NotificationType.Warning, number > 0 ? "reallife_own_note_claim" : "reallife_own_note_claim_letter", Data("employer", employer, "amount", CareersText.Money(amount)), TabOwn, null);
        }

        /// <summary>Opens a practice in a profession the player may practise: its registration paid. Null if done, else a reason key.</summary>
        public string OpenPracticeNow(int prof)
        {
            WorkFile w = Work;
            Profession p = ProCatalogue.Find(prof);
            if (w == null || !OwnOn || p == null)
            {
                return "reallife_work_why_closed";
            }
            if (w.Practice.Open)
            {
                return "reallife_own_why_practice_open";
            }
            if (!OwnRules.CanPractise(p, _study))
            {
                return "reallife_own_why_cannot_practise";
            }
            return OpenPractice(prof, Save.Day, false) ? null : "reallife_study_why_money";
        }

        private bool OpenPractice(int prof, int day, bool fromJob)
        {
            WorkFile w = _study.Work;
            Profession p = ProCatalogue.Find(prof);
            int fee = Prices.At(OwnRules.RegisterFee(p));
            if (!CareersGame.Charge(fee, RegisterKey, CareersText.ProName(prof)))
            {
                if (fromJob)
                {
                    Tell2(NotificationType.Warning, "reallife_own_note_practice_unpaid", Data("fee", CareersText.Money(fee)), TabOwn, null);
                }
                return false;
            }
            w.Practice.Prof = prof;
            w.Practice.Opened = day;
            w.Practice.Reputation = OwnRules.PracticeStart + Math.Min(20, ProRules.Experience(w, prof, day, Year) / 120);
            w.Practice.Served = w.Practice.Late = 0;
            Tell2(NotificationType.Success, "reallife_own_note_practice_open", Data("profession", CareersText.ProName(prof), "fee", CareersText.Money(fee)), TabOwn, null);
            Story("reallife_own_news_practice", "name", PlayerName(), "profession", CareersText.ProName(prof));
            if (w.RestraintUntil >= day && w.RestraintProf == prof)
            {
                TestRestraint(false);
            }
            Changed();
            return true;
        }

        public string ClosePractice()
        {
            WorkFile w = Work;
            if (w == null || !w.Practice.Open)
            {
                return "reallife_own_why_no_practice";
            }
            w.Practice.Prof = -1;
            Changed();
            return null;
        }

        /// <summary>The profession contracts come in: the practice's, else the job's, else the best the player is qualified for.</summary>
        private int FreelanceProf()
        {
            WorkFile w = _study.Work;
            if (w.Practice.Open)
            {
                return w.Practice.Prof;
            }
            if (w.Job != null)
            {
                return w.Job.Post.Prof;
            }
            int best = -1, bestPay = 0;
            for (int i = 0; i < ProCatalogue.All.Length; i++)
            {
                Profession p = ProCatalogue.All[i];
                int r = ProRules.BestRank(_study, p, ProRules.Experience(w, p.Id, Save.Day, Year));
                if (r >= 0 && p.Ranks[r].Pay > bestPay && p.Remote | !p.Public)
                {
                    best = p.Id;
                    bestPay = p.Ranks[r].Pay;
                }
            }
            return best;
        }

        private void OwnDay(int day)
        {
            WorkFile w = _study.Work;
            if (w.ColleaguesCount > 0 && w.ColleaguesUntil > 0 && day > w.ColleaguesUntil)
            {
                w.ColleaguesCount = 0;
                Tell2(NotificationType.Info, "reallife_own_note_colleagues_gone", null, TabOwn, null);
            }
            for (int i = w.Contracts.Count - 1; i >= 0; i--)
            {
                Contract c = w.Contracts[i];
                if (c.Stage == (int)ContractStep.Offered && day > c.Lapse)
                {
                    c.Stage = (int)ContractStep.Lapsed;
                }
                else if (c.Stage == (int)ContractStep.Taken && day > c.Due + 7)
                {
                    c.Stage = (int)ContractStep.Dropped;
                    OwnRules.Delivered(c.Practice ? w.Practice : null, true);
                    Tell2(NotificationType.Warning, "reallife_own_note_dropped", Data("client", c.Client), TabOwn, null);
                }
                if (c.Stage > (int)ContractStep.Taken && day - c.Due > 30)
                {
                    w.Contracts.RemoveAt(i);
                }
            }
            OfferContract(day, 0, false);
        }

        /// <summary>A contract offered by a client of the city, on the day's roll (or at once, for a test).</summary>
        private void OfferContract(int day, int salt, bool force)
        {
            WorkFile w = _study.Work;
            if (w.Retired > 0 || OwnRules.Live(w.Contracts) >= OwnRules.ContractsMost)
            {
                return;
            }
            if (!force && TechRandom.Mille(StudySeed, 8702, day, salt) >= OwnRules.OfferChanceOf(w.Practice))
            {
                return;
            }
            int prof = FreelanceProf();
            Profession p = ProCatalogue.Find(prof);
            if (p == null)
            {
                return;
            }
            int rank = Math.Max(0, ProRules.BestRank(_study, p, ProRules.Experience(w, prof, day, Year)));
            ReadEmployers(day);
            if (_employers.Count == 0)
            {
                return;
            }
            EmployerRef client = _employers[TechRandom.Range(StudySeed, 8703, day, salt, 0, _employers.Count - 1)];
            int hours = OwnRules.HoursOf[TechRandom.Range(StudySeed, 8704, day, salt, 0, OwnRules.HoursOf.Length - 1)];
            bool office = OwnOffice(p) != null;
            var c2 = new Contract
            {
                Id = w.Next(), Prof = prof, Rank = rank, Client = client.Name, Street = client.Street, Number = client.Number, Hours = hours,
                Fee = OwnRules.Fee(ProRules.PostPay(p, rank, EmployerKind.City, CareersBridges.ShortageOf(p.Family)), hours, w.Practice.Open, office),
                Due = day + TechRandom.Range(StudySeed, 8705, day, salt, OwnRules.ContractDaysLeast, OwnRules.ContractDaysMost), Lapse = day + OwnRules.OfferDays,
                Stage = (int)ContractStep.Offered, Practice = w.Practice.Open
            };
            w.Contracts.Add(c2);
            Tell2(NotificationType.Info, "reallife_own_note_offer", Data("client", c2.Client, "fee", CareersText.Money(Prices.At((int)c2.Fee)), "hours", hours.ToString()),
                TabOwn, CareerApp.ContractRow + c2.Id);
        }

        /// <summary>A business of the player's own where a profession's desk work can be done (an office, or the practice's own kind), or null.</summary>
        internal BuildingRegistration OwnOffice(Profession p)
        {
            ReadMine();
            for (int i = 0; i < _mine.Count; i++)
            {
                BuildingRegistration reg = _mine[i];
                string type = reg.businessTypeName ?? string.Empty;
                if (ProCatalogue.EmploysKept(p, type) || type == "ba:businesstype_headquarters")
                {
                    return reg;
                }
            }
            return null;
        }

        public string TakeContract(int id)
        {
            Contract c = ContractOf(id);
            if (c == null || c.Stage != (int)ContractStep.Offered)
            {
                return "reallife_own_why_no_contract";
            }
            c.Stage = (int)ContractStep.Taken;
            Changed();
            return null;
        }

        public string DropContract(int id)
        {
            Contract c = ContractOf(id);
            if (c == null || c.Stage > (int)ContractStep.Taken)
            {
                return "reallife_own_why_no_contract";
            }
            bool taken = c.Stage == (int)ContractStep.Taken;
            c.Stage = taken ? (int)ContractStep.Dropped : (int)ContractStep.Lapsed;
            if (taken)
            {
                OwnRules.Delivered(c.Practice ? _study.Work.Practice : null, true);
            }
            Changed();
            return null;
        }

        internal Contract ContractOf(int id)
        {
            WorkFile w = Work;
            for (int i = 0; w != null && i < w.Contracts.Count; i++)
            {
                if (w.Contracts[i].Id == id)
                {
                    return w.Contracts[i];
                }
            }
            return null;
        }

        /// <summary>Where a contract's work can be done now, or the reason key: a trade at the client's, desk work at home or at an office of the player's.</summary>
        public string CanWorkContract(Contract c)
        {
            if (c == null || c.Stage != (int)ContractStep.Taken || OwnRules.Left(c) <= 0)
            {
                return "reallife_own_why_no_contract";
            }
            Profession p = c.Def;
            bool trade = p.Place == Workplace.Business && (p.Id == ProCatalogue.Electrician || p.Id == ProCatalogue.Plumber || p.Id == ProCatalogue.Carpenter);
            if (trade)
            {
                var post = new Opening { Prof = p.Id, Street = c.Street, Number = c.Number };
                return WorkGame.AtWork(p, post) ? null : "reallife_own_why_at_client";
            }
            if (CareersGame.AtHome())
            {
                return null;
            }
            BuildingRegistration office = OwnOffice(p);
            return office != null && CareersGame.InsideAt(office.StreetName, office.StreetNumber) ? null : "reallife_own_why_desk";
        }

        public string WorkContract(int id)
        {
            Contract c = ContractOf(id);
            string why = CanWorkContract(c);
            if (why != null)
            {
                return why;
            }
            int minutes = Math.Min(OwnRules.SittingMinutes, OwnRules.Left(c));
            return Offer(new Sitting(this, SittingKind.Contract, id, minutes, CareersText.T("reallife_own_skip_title", "client", c.Client), true,
                CareersText.T("reallife_own_skip_left", "time", CareersText.Minutes(OwnRules.Left(c))), CareersText.T("reallife_own_skip_fee", "fee", CareersText.Money(Prices.At((int)c.Fee)))));
        }

        private void ContractEnded(Sitting s, int minutes)
        {
            Contract c = ContractOf(s.Of);
            if (c == null || minutes <= 0)
            {
                return;
            }
            WorkFile w = _study.Work;
            int day = Save.Day;
            c.Minutes += minutes;
            Profession p = c.Def;
            int a, b;
            p.SkillsOf(c.Rank, out a, out b);
            int hours = Math.Max(1, minutes / 60);
            LearnAtWork(a, hours, day);
            LearnAtWork(b, hours, day);
            // Side work under a contract that does not allow it may be noticed.
            Job j = w.Job;
            if (j != null && j.Exclusive && TechRandom.Mille(StudySeed, 8706, day, c.Id * 7 + c.Minutes) < OwnRules.SideWorkFound)
            {
                j.Warnings++;
                if (j.Warnings >= WarningsToGo)
                {
                    Tell2(NotificationType.Error, "reallife_own_note_side_sacked", Data("employer", CareersText.Employer(j.Post.Employer)), TabCv, null);
                    EndJob(LeaveReason.Misconduct, day, false);
                }
                else
                {
                    Tell2(NotificationType.Warning, "reallife_own_note_side_warned", Data("employer", CareersText.Employer(j.Post.Employer), "count", j.Warnings.ToString(),
                        "most", WarningsToGo.ToString()), TabJob, null);
                }
            }
            if (w.RestraintUntil >= day && w.RestraintProf == c.Prof)
            {
                TestRestraint(false);
            }
            if (OwnRules.Left(c) > 0)
            {
                return;
            }
            bool late = day > c.Due;
            long paid = OwnRules.Paid(c, day);
            int fee = Prices.At((int)Math.Min(int.MaxValue, paid));
            CareersGame.Pay(fee, ContractKey, null, c.Client);
            c.Stage = (int)(late ? ContractStep.Late : ContractStep.Done);
            OwnRules.Delivered(c.Practice ? w.Practice : null, late);
            w.Earned += fee * 100L;
            Tell2(NotificationType.Success, late ? "reallife_own_note_delivered_late" : "reallife_own_note_delivered", Data("client", c.Client, "fee", CareersText.Money(fee)), TabOwn, null);
        }

        /// <summary>A sitting of a later part (contract work, tutoring, a ceremony): settled by that part.</summary>
        private void OtherEnded(Sitting sitting, int minutes, bool whole)
        {
            if (sitting.Kind == SittingKind.Contract && _workOn && OwnOn)
            {
                ContractEnded(sitting, minutes);
            }
            OtherEndedMore(sitting, minutes, whole);
        }
    }
}
