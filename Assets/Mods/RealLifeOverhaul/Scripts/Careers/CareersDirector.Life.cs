using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;
using UI.Notification;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// Working life (Careers/Work/LifeRules): reviews twice a year with a rating, rises, bonuses and promotions; asking for a
    /// rise; overtime and burnout; office matters with a boss of a certain manner; search firms that come for good people;
    /// redundancy in a downturn with severance and unemployment benefit; share options at a tech company; the pension and
    /// retirement; a seat at the top of a rival's company and its share of the takings; and a conflict of interest with
    /// the player's own businesses, declared or found out. All of it on the game's day and at the end of a working stint.
    /// </summary>
    public sealed partial class CareersDirector
    {
        private const string BonusKey = "reallife_transaction_work_bonus";
        private const string SeveranceKey = "reallife_transaction_work_severance";
        private const string BenefitKey = "reallife_transaction_work_benefit";
        private const string OptionsKey = "reallife_transaction_work_options";
        private const string PensionKey = "reallife_transaction_work_pension";
        private const string PensionPaidKey = "reallife_transaction_work_pension_paid";
        private const string ShareKey = "reallife_transaction_work_profit_share";

        private readonly List<BuildingRegistration> _mine = new List<BuildingRegistration>(16);
        private int _mineDay = -1;

        private static int Year => GameCalendar.DaysPerYear;

        // ---------------------------------------------------------------- the hooks of the working life (CareersDirector.Work.cs)

        partial void Hired(Job j, int day)
        {
            if (!Settings.CareersLife)
            {
                return;
            }
            EmployerKind kind = (EmployerKind)j.Post.Kind;
            j.Options = LifeRules.OptionsFor(kind, j.Post.Rank);
            j.VestDay = day + TalentCatalogue.Scaled(LifeRules.VestQuarterDays, Year);
            j.NonCompete = kind != EmployerKind.Public && j.Post.Rank >= 2;
            j.Exclusive = kind == EmployerKind.TechFirm || j.Post.Rank >= 3;
            j.PoliticsDay = day + TechRandom.Range(StudySeed, 8601, day, 0, LifeRules.MatterGapLeast, LifeRules.MatterGapMost);
            j.Politics = -1;
            if (j.Rank.Exec && kind == EmployerKind.Rival)
            {
                SeatAtTheTop(j);
            }
        }

        partial void Ended(Job j, LeaveReason why, int day)
        {
            if (!Settings.CareersLife)
            {
                return;
            }
            WorkFile w = _study.Work;
            if (why == LeaveReason.Redundant)
            {
                long severance = LifeRules.Severance(j, day, Year);
                int whole = Prices.At((int)(severance / 100));
                CareersGame.Pay(whole, SeveranceKey, null, CareersText.Employer(j.Post.Employer));
                w.BenefitWeek = LifeRules.BenefitWeek(LifeRules.WeekPay(j));
                w.BenefitUntil = day + 7 * LifeRules.BenefitWeeks;
                w.BenefitPaid = day;
                Tell2(NotificationType.Info, "reallife_life_note_severance", Data("amount", CareersText.Money(whole), "benefit", CareersText.Money(Prices.At(w.BenefitWeek)),
                    "weeks", LifeRules.BenefitWeeks.ToString()), TabJob, null);
            }
            if (j.Rank.Exec && j.Post.Kind == (int)EmployerKind.Rival)
            {
                Story("reallife_life_news_left_top", "name", PlayerName(), "company", CareersText.Employer(j.Post.Employer));
            }
        }

        partial void WorkedMore(Job j, Sitting s, int minutes, bool whole, int day)
        {
            if (!Settings.CareersLife)
            {
                return;
            }
            if (s.Kind == SittingKind.Overtime)
            {
                j.Overtime += minutes;
            }
            int before = j.Burnout;
            j.Burnout = Math.Min(1000, j.Burnout + LifeRules.BurnoutFor(j.WeekMinutes - minutes, minutes));
            if (before <= LifeRules.BurnoutMood && j.Burnout > LifeRules.BurnoutMood)
            {
                CareersGame.Mood(ModState.Burnout);
                Tell2(NotificationType.Warning, "reallife_life_note_burnout", Data("hours", LifeRules.LongWeekHours.ToString()), TabJob, null);
            }
            if ((BossKind)j.Boss == BossKind.Mentor)
            {
                int a, b;
                j.Def.SkillsOf(j.Post.Rank, out a, out b);
                int hours = Math.Max(1, minutes / 60);
                StudyRules.Learn(_study, a, StudyCatalogue.WorkLearn * hours * (100 - StudyRules.Points(_study, a)) / 100 * LifeRules.MentorLearn / 1000, day);
            }
        }

        partial void PaidMore(Job j, long cents, bool overtime)
        {
            if (!Settings.CareersLife)
            {
                return;
            }
            long own, employer;
            LifeRules.Pension(cents, (EmployerKind)j.Post.Kind, out own, out employer);
            if (own > 0)
            {
                CareersGame.Take(own / 100f, PensionKey, CareersText.Employer(j.Post.Employer));
            }
            _study.Work.Pension += own + employer;
        }

        partial void WorkDayMore(int day)
        {
            if (!Settings.CareersLife)
            {
                return;
            }
            WorkFile w = _study.Work;
            w.Pension += LifeRules.PensionGrowth(w.Pension, Year);
            if (w.Retired > 0 && w.Pension > 0)
            {
                long paid = Math.Min(w.Pension, LifeRules.PensionDay(w.Pension, Year, w.RetiredEarly));   // (the pot is kept in the money it was paid in)
                w.Pension -= paid;
                CareersGame.Pay(paid / 100f, PensionPaidKey, null, CareersText.T("reallife_life_pension_name"));
            }
            Job j = w.Job;
            if (j == null)
            {
                if (w.BenefitUntil >= day && day - w.BenefitPaid >= 7 && w.BenefitWeek > 0)
                {
                    w.BenefitPaid = day;
                    int whole = Prices.At(w.BenefitWeek);
                    CareersGame.Pay(whole, BenefitKey, null, CareersText.T("reallife_work_employer_council"));
                    Tell2(NotificationType.Info, "reallife_life_note_benefit", Data("amount", CareersText.Money(whole), "day", CareersText.Day(w.BenefitUntil)), TabJob, null);
                }
                return;
            }
            // Rest: a day not worked eases burnout; too much of it and the doctor signs the player off.
            if (j.TodayDay != day - 1)
            {
                j.Burnout = Math.Max(0, j.Burnout - LifeRules.BurnoutRest);
            }
            if (j.Burnout >= LifeRules.BurnoutSignedOff && j.OffUntil < day)
            {
                j.OffUntil = day + 1;
                j.Burnout = LifeRules.BurnoutAfterSignedOff;
                Tell2(NotificationType.Warning, "reallife_life_note_signed_off", Data("day", CareersText.Day(j.OffUntil)), TabJob, null);
            }
            if (day >= j.ReviewDay + LifeRules.ReviewEvery(Year) && day > j.Hired)
            {
                Review(j, day);
                if (w.Job != j)
                {
                    return;
                }
            }
            Matters(j, day);
            Vesting(j, day);
            Approach(j, day);
            Downturn(j, day);
            Conflict(j, day);
            if (w.Job == j && (day - j.Hired) % 7 == 6)
            {
                ProfitShare(j);
            }
        }

        // ---------------------------------------------------------------- reviews

        private void Review(Job j, int day)
        {
            Profession p = j.Def;
            int score = LifeRules.ReviewScore(j, ProRules.Points(_study, p, j.Post.Rank), ProRules.Need(_study, p, j.Post.Rank), (BossKind)j.Boss);
            int rating = LifeRules.Rating(score);
            j.Rating = rating;
            j.ReviewDay = day;
            string employer = CareersText.Employer(j.Post.Employer);
            int rise = LifeRules.RiseFor(rating, j.Raises);
            if (rise > 0)
            {
                j.Pay = (int)((long)j.Pay * (10000 + rise) / 10000);
                j.Raises++;
            }
            int weeks = LifeRules.BonusWeeks(rating);
            int bonus = weeks > 0 ? Prices.At((int)(LifeRules.WeekPay(j) * weeks / 100)) : 0;
            if (bonus > 0)
            {
                CareersGame.Pay(bonus, BonusKey, SalaryCategoryName, employer);
            }
            bool promoted = LifeRules.CanPromote(_study, j, rating, day, Year);
            j.Due = j.Worked = j.Late = j.Absent = 0;
            j.Overtime = 0;
            j.Favour /= 2;
            if (rating >= 3)
            {
                j.Warnings = 0;
                j.LowRuns = 0;
            }
            else if (rating == 1)
            {
                j.LowRuns++;
            }
            Tell2(rating >= 3 ? NotificationType.Success : NotificationType.Warning, bonus > 0 ? "reallife_life_note_review_bonus" : "reallife_life_note_review",
                Data("rating", rating.ToString(), "rise", rise > 0 ? CareersText.T("reallife_life_rise", "percent", Bp(rise)) : CareersText.T("reallife_life_no_rise"),
                    "bonus", CareersText.Money(bonus)), TabJob, null);
            Logger?.Info("Careers: review at " + j.Post.Employer + " on day " + day + ": score " + score + ", rating " + rating + ", rise " + rise + " bp, bonus " + bonus
                         + (promoted ? ", promoted." : "."));
            if (j.LowRuns >= LifeRules.LowRunsToGo)
            {
                Tell2(NotificationType.Error, "reallife_life_note_dismissed_reviews", Data("employer", employer), TabCv, null);
                EndJob(LeaveReason.Dismissed, day, false);
                return;
            }
            if (promoted)
            {
                Promote(j, day);
            }
        }

        private const string SalaryCategoryName = "ba:transactioncategory_salaryincome";

        private static string Bp(int bp) => (bp / 100f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";

        private void Promote(Job j, int day)
        {
            int next = j.Post.Rank + 1;
            j.Pay = LifeRules.PromotedPay(j, next);
            j.Post.Rank = next;
            j.RankDay = day;
            j.Raises = 0;
            j.NonCompete |= next >= 2 && j.Post.Kind != (int)EmployerKind.Public;
            j.Exclusive |= next >= 3;
            if (j.Post.Kind == (int)EmployerKind.TechFirm)
            {
                j.Options += LifeRules.OptionsPerRank;
            }
            string title = CareersText.Title(j.Post.Prof, next), employer = CareersText.Employer(j.Post.Employer);
            Tell2(NotificationType.Success, "reallife_life_note_promoted", Data("title", title, "employer", employer, "pay", CareersText.Rate(Prices.At(j.Pay))), TabJob, null);
            CareersGame.Mood(ModState.MoodUp);
            if (j.Rank.Exec)
            {
                Story("reallife_life_news_top", "name", PlayerName(), "title", title + ", " + employer);
                if (j.Post.Kind == (int)EmployerKind.Rival)
                {
                    SeatAtTheTop(j);
                }
            }
        }

        /// <summary>A seat at the top of a rival's company: the rival says so, and a share of the company's week of takings comes with it.</summary>
        private void SeatAtTheTop(Job j)
        {
            string rival = WorkGame.RivalName(j.Post.Rival) ?? CareersText.Employer(j.Post.Employer);
            bool chief = j.Post.Rank == j.Def.Ranks.Length - 1;
            Tell2(NotificationType.Success, chief ? "reallife_life_note_rival_chief" : "reallife_life_note_rival_seat",
                Data("rival", rival, "share", Bp(LifeRules.ProfitShareBp(j))), TabJob, null);
        }

        /// <summary>The player asks for a rise between reviews. Null if answered, else a reason key.</summary>
        public string AskRise()
        {
            Job j = Work != null ? Work.Job : null;
            if (j == null)
            {
                return "reallife_work_why_no_job";
            }
            int day = Save.Day;
            if (day - j.AskDay < LifeRules.AskGapDays)
            {
                return "reallife_life_why_asked";
            }
            j.AskDay = day;
            string employer = CareersText.Employer(j.Post.Employer);
            int rating = j.Rating > 0 ? j.Rating : 3;
            if (TechRandom.Mille(StudySeed, 8602, day, j.Hired) < LifeRules.AskRiseChance(rating) * 10)
            {
                j.Pay = (int)((long)j.Pay * (10000 + LifeRules.AskRiseBp) / 10000);
                Tell2(NotificationType.Success, "reallife_life_note_rise_yes", Data("employer", employer, "pay", CareersText.Rate(Prices.At(j.Pay))), TabJob, null);
            }
            else
            {
                j.Favour = Math.Max(-LifeRules.FavourMost, j.Favour - 1);
                Tell2(NotificationType.Info, "reallife_life_note_rise_no", Data("employer", employer), TabJob, null);
            }
            Changed();
            return null;
        }

        // ---------------------------------------------------------------- overtime

        public string CanWorkLate()
        {
            Job j = Work != null ? Work.Job : null;
            int day = Save != null ? Save.Day : 0;
            if (j == null)
            {
                return "reallife_work_why_no_job";
            }
            Profession p = j.Def;
            if (j.TodayDay != day || j.TodayMinutes * 1000 < p.Hours * 60 * ProRules.CountedShare)
            {
                return "reallife_life_why_day_first";
            }
            if (CareersGame.MinuteOfDay + LifeRules.OvertimeMinutes > 23 * 60)
            {
                return "reallife_life_why_too_late";
            }
            if (ProRules.FromHome((WorkMode)j.Work, day))
            {
                return CareersGame.AtHome() ? null : "reallife_work_why_home_day";
            }
            return WorkGame.AtWork(p, j.Post) ? null : "reallife_work_why_not_at_work";
        }

        public string WorkLate()
        {
            string why = CanWorkLate();
            if (why != null)
            {
                return why;
            }
            Job j = Work.Job;
            return Offer(new Sitting(this, SittingKind.Overtime, 0, LifeRules.OvertimeMinutes, CareersText.Fill("reallife_work_skip_title", "title", CareersText.Title(j.Post.Prof, j.Post.Rank),
                "employer", CareersText.Employer(j.Post.Employer)), true, CareersText.T("reallife_life_skip_overtime"), CareersText.T("reallife_work_skip_pay", "pay", CareersText.Rate(Prices.At(j.Pay)))));
        }

        // ---------------------------------------------------------------- office matters

        private void Matters(Job j, int day)
        {
            if (j.Politics >= 0)
            {
                if (day > j.PoliticsDay)
                {
                    Answer(j, false, day);   // left unanswered: as if the second answer was given
                }
                return;
            }
            if (day < j.PoliticsDay || j.NoticeEnd > 0)
            {
                return;
            }
            j.Politics = TechRandom.Range(StudySeed, 8603, day, j.Hired, 0, LifeRules.Matters.Length - 1);
            j.PoliticsDay = day + LifeRules.MatterDays;
            Matter m = LifeRules.Matters[j.Politics];
            Tell2(NotificationType.Info, CareersText.MatterKey(m), Data("employer", CareersText.Employer(j.Post.Employer)), TabJob, null);
        }

        /// <summary>The player answers the office matter waiting: the first answer or the second. Null if done, else a reason key.</summary>
        public string AnswerMatter(bool first)
        {
            Job j = Work != null ? Work.Job : null;
            if (j == null || j.Politics < 0)
            {
                return "reallife_life_why_no_matter";
            }
            Answer(j, first, Save.Day);
            Changed();
            return null;
        }

        private void Answer(Job j, bool first, int day)
        {
            Matter m = LifeRules.Matters[TalentCatalogue.Clamp(j.Politics, 0, LifeRules.Matters.Length - 1)];
            j.Favour = TalentCatalogue.Clamp(j.Favour + LifeRules.FavourOf(m, first, (BossKind)j.Boss), -LifeRules.FavourMost, LifeRules.FavourMost);
            if (first)
            {
                j.Burnout = Math.Min(1000, j.Burnout + m.BurnoutA);
            }
            StudyRules.Use(_study, TalentCatalogue.Relationships, day);
            j.Politics = -1;
            j.PoliticsDay = day + TechRandom.Range(StudySeed, 8604, day, j.Hired, LifeRules.MatterGapLeast, LifeRules.MatterGapMost);
        }

        // ---------------------------------------------------------------- options, search firms, downturns, conflicts, the top seats

        private void Vesting(Job j, int day)
        {
            if (j.Options <= 0 || day < j.VestDay)
            {
                return;
            }
            int vested;
            long cents = LifeRules.Vest(j.Options, CareersBridges.CycleNow(), out vested);
            j.Options -= vested;
            j.VestDay = day + TalentCatalogue.Scaled(LifeRules.VestQuarterDays, Year);
            if (cents > 0)
            {
                int whole = Prices.At((int)(cents / 100));
                CareersGame.Pay(whole, OptionsKey, null, CareersText.Employer(j.Post.Employer));
                Tell2(NotificationType.Success, "reallife_life_note_vested", Data("count", vested.ToString(), "amount", CareersText.Money(whole), "left", j.Options.ToString()), TabJob, null);
            }
        }

        /// <summary>A search firm comes for a good senior: a direct offer, a rung up where there is one, at another employer of the profession.</summary>
        private void Approach(Job j, int day, bool force = false)
        {
            WorkFile w = _study.Work;
            if (!force && (j.NoticeEnd > 0 || day - w.HeadhuntDay < LifeRules.PoachGapDays))
            {
                return;
            }
            int chance = LifeRules.PoachChanceOf(j.Post.Rank, j.Rating, CareersBridges.ReputationNow());
            if (!force && (chance <= 0 || TechRandom.Mille(StudySeed, 8605, day, j.Hired) >= chance))
            {
                return;
            }
            Profession p = j.Def;
            ReadEmployers(day);
            Opening o = null;
            for (int n = 0; n < 12 && o == null; n++)
            {
                Opening c = NewVacancy(day, 900 + n, false, false);
                if (c != null && c.Prof == j.Post.Prof && c.Employer != j.Post.Employer)
                {
                    o = c;
                }
            }
            if (o == null)
            {
                if (force)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_work_test_none", null, 8f);
                }
                return;
            }
            int rank = Math.Min(j.Post.Rank + 1, p.Ranks.Length - 1);
            o.Rank = p.Ranks[rank].Exec && !force ? j.Post.Rank : rank;
            o.Pay = Math.Max(o.Pay, (int)((long)j.Pay * (10000 + LifeRules.PoachPremiumBp) / 10000));
            o.Via = (int)Via.Headhunter;
            o.To = day + ProRules.OfferDays;
            w.HeadhuntDay = day;
            var a = new Application { Day = day, Score = 80 };
            o.CopyTo(a.Post);
            Offer(a, day);
            w.Applications.Add(a);
        }

        private void Downturn(Job j, int day)
        {
            if (j.NoticeEnd > 0)
            {
                return;
            }
            int chance = LifeRules.RedundancyChance(CareersBridges.CycleNow(), (EmployerKind)j.Post.Kind, j.Rating);
            if (chance <= 0 || TechRandom.Mille(StudySeed, 8606, day, j.Hired) >= chance)
            {
                return;
            }
            j.Leaving = (int)LeaveReason.Redundant;
            j.NoticeEnd = day + ProRules.NoticeDays(j.Rank, j.Post.Rank);
            Tell2(NotificationType.Warning, "reallife_life_note_redundant", Data("employer", CareersText.Employer(j.Post.Employer), "day", CareersText.Day(j.NoticeEnd)), TabJob, null);
        }

        /// <summary>True if a business of the player's own is in the same trade as the employer: a conflict of interest.</summary>
        public bool Conflicted(Job j)
        {
            if (j == null || j.Post.Kind == (int)EmployerKind.Public)
            {
                return false;
            }
            int day = Save != null ? Save.Day : 0;
            if (_mineDay != day)
            {
                _mineDay = day;
                TalentGame.Businesses(_mine);
            }
            BuildingRegistration at = null;
            try
            {
                at = j.Post.Kind == (int)EmployerKind.TechFirm ? null : Helpers.BuildingHelper.GetBuildingRegistration(new Address(j.Post.Street, j.Post.Number));
            }
            catch (Exception)
            {
            }
            string type = at != null ? at.businessTypeName : null;
            for (int i = 0; i < _mine.Count; i++)
            {
                string mine = _mine[i].businessTypeName;
                if (type != null ? mine == type : TechTypes.Is(mine))
                {
                    return true;
                }
            }
            return false;
        }

        private void Conflict(Job j, int day)
        {
            if (j.Declared || !Conflicted(j))
            {
                return;
            }
            if (TechRandom.Mille(StudySeed, 8607, day, j.Hired) >= LifeRules.ConflictChanceOf(j.Post.Rank))
            {
                return;
            }
            string employer = CareersText.Employer(j.Post.Employer);
            Tell2(NotificationType.Error, "reallife_life_note_conflict_found", Data("employer", employer), TabCv, null);
            CareersBridges.Standing?.Invoke(3, LifeRules.ConflictIntegrity);
            Story("reallife_life_news_conflict", "name", PlayerName(), "company", employer);
            EndJob(LeaveReason.Misconduct, day, false);
        }

        /// <summary>The player declares a conflict of interest to the employer: nothing is held against them, but the top seats there are closed.</summary>
        public string Declare()
        {
            Job j = Work != null ? Work.Job : null;
            if (j == null)
            {
                return "reallife_work_why_no_job";
            }
            j.Declared = true;
            Tell2(NotificationType.Info, "reallife_life_note_declared", Data("employer", CareersText.Employer(j.Post.Employer)), TabJob, null);
            Changed();
            return null;
        }

        private void ProfitShare(Job j)
        {
            int bp = LifeRules.ProfitShareBp(j);
            if (bp <= 0)
            {
                return;
            }
            float week = WorkGame.RivalWeekly(j.Post.Rival);
            int whole = (int)(week * bp / 10000f);
            if (whole > 0)
            {
                CareersGame.Pay(whole, ShareKey, null, CareersText.Employer(j.Post.Employer));
                Tell2(NotificationType.Success, "reallife_life_note_profit_share", Data("amount", CareersText.Money(whole), "share", Bp(bp)), TabJob, null);
            }
        }

        // ---------------------------------------------------------------- retiring

        public string CanRetire()
        {
            WorkFile w = Work;
            if (w == null || w.Retired > 0)
            {
                return "reallife_life_why_retired";
            }
            return CareersGame.AgeYears() < LifeRules.RetireEarlyAge ? "reallife_life_why_young" : null;
        }

        /// <summary>The player retires: the job (if any) ends, and the pension pays a day for the rest of the life.</summary>
        public string Retire()
        {
            string why = CanRetire();
            if (why != null)
            {
                return why;
            }
            WorkFile w = Work;
            int day = Save.Day;
            if (w.Job != null)
            {
                EndJob(LeaveReason.Retired, day, false);
            }
            w.Retired = day;
            w.RetiredEarly = CareersGame.AgeYears() < LifeRules.RetireAge;
            long a = LifeRules.PensionDay(w.Pension, Year, w.RetiredEarly);
            Tell2(NotificationType.Success, "reallife_life_note_retired", Data("pot", CareersText.Cents(w.Pension), "day", CareersText.Cents(a)), TabJob, null);
            Changed();
            return null;
        }

        private static string PlayerName()
        {
            try
            {
                return Helpers.PlayerHelper.CharacterData != null ? Helpers.PlayerHelper.CharacterData.name : "?";
            }
            catch (Exception)
            {
                return "?";
            }
        }
    }
}
