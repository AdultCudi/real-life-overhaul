using System;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>What an employer reads of the player when they apply. Filled by the game side; game-free.</summary>
    public struct Candidate
    {
        public int Points;        // the rank's two skills, the first counting three fifths
        public int Need;          // what the rank asks for
        public bool Qualified;    // holds what the rank needs (a course, a certificate), or may come to it self-taught
        public int Experience;    // days worked in the profession before
        public int Good, Poor;    // references
        public int Reputation;    // -10 to 10
        public bool Referral, Agency, Record, Honours, Master;
        public int Cycle;         // the city's economy: 0 boom to 3 recession
        public bool Summer;       // the graduates are out: more people after the first rung
        public int Rank;
    }

    /// <summary>
    /// The rules of working in a profession in plain numbers (Careers/Work/ProCatalogue): what a rank asks, an employer's
    /// score, the interview, the offer and haggling, the working day and its pay, notice, references and experience.
    /// Game-free: EconomyCheck runs every one.
    /// </summary>
    public static class ProRules
    {
        public const int SelfTaughtExtra = 20;    // points more a rank asks of someone without its qualification, where a profession takes people self-taught
        public const int ScreenPass = 50;         // the score an application needs to be invited
        public const int OfferPass = 60;          // ...and the interview to bring an offer
        public const int ScreenLuck = 10, InterviewLuck = 15;
        public const int AnswerDaysLeast = 1, AnswerDaysMost = 2;
        public const int ReapplyDays = 14;        // game days after an employer turns the player down before it takes an application for the same post again, so applying daily cannot beat the odds
        public const int InterviewAfterDays = 2;  // an interview is held this many days after the invitation
        public const int InterviewHourLeast = 10, InterviewHourMost = 15;
        public const int InterviewMinutes = 60;
        public const int InterviewEarlyMinutes = 30;  // an interview can be joined from this long before its hour...
        public const int InterviewLateMinutes = 60;   // ...until this long after it
        public const int OfferDays = 3;           // days to answer an offer
        public const int StartDays = 2;           // days after accepting before the first day, out of work
        public const int AskSmallBp = 500, AskLargeBp = 1000;
        public const int AsksMost = 2;
        public const int ClockEarlyMinutes = 60;  // a working day can be started this long before its hour...
        public const int ClockLateMinutes = 120;  // ...and until this long after it
        public const int LateAfterMinutes = 15;   // after this it is late
        public const int CountedShare = 750;      // thousandths of the day worked for it to count
        public const int OpeningsBase = 14;       // vacancies the city has open at once
        public const int OpeningsAgency = 5;      // ...more with the recruitment office
        public const int OpeningsWithinReach = 3; // first posts kept open, while the player has no job, in professions they could be offered one in now
        public static readonly int[] OpeningsByCycle = { 4, 0, -3, -6 };
        public const int OpeningDaysLeast = 7, OpeningDaysMost = 14;
        public static readonly int[] RankWeights = { 450, 300, 150, 70, 30 };   // thousandths: the share of vacancies at each rung (the top rungs are found by search firms)
        public const int AgencyDays = 14;         // a registration with the recruitment office lasts this long
        public const int AgencyNoteHour = 7;      // hour of the game's day: the reminder on a registration's last day comes from then (or at the first hour seen after it)
        public const int BossKinds = 4;           // the manners a boss can have (Careers/Work/LifeRules)
        public const int FriendsForReferral = 3;  // friends it takes for a referral to come now and then
        public const int ReferralShare = 150;     // thousandths of vacancies a friend can put the player forward for

        /// <summary>What employers of a kind pay, thousandths of the rank's rate.</summary>
        public static int KindFactor(EmployerKind kind)
        {
            switch (kind)
            {
                case EmployerKind.TechFirm: return 1100;
                case EmployerKind.Rival: return 1050;
                case EmployerKind.Public: return 900;
                default: return 1000;
            }
        }

        /// <summary>The points of a rank's two skills, the first counting three fifths.</summary>
        public static int Points(StudyFile f, Profession p, int rank)
        {
            int a, b;
            p.SkillsOf(rank, out a, out b);
            return (StudyRules.Points(f, a) * 3 + StudyRules.Points(f, b) * 2) / 5;
        }

        /// <summary>True for a profession that takes people without its qualification if their skills are good enough.</summary>
        public static bool SelfTaught(Profession p)
        {
            return p.Family == (int)Tech.Family.Engineering || p.Family == (int)Tech.Family.Data || p.Family == (int)Tech.Family.Security || p.Family == (int)Tech.Family.Design
                   || p.Family == (int)Tech.Family.Commercial || p.Family == (int)Tech.Family.Support || p.Family == (int)Tech.Family.Product || p.Family == (int)Tech.Family.Infrastructure;
        }

        /// <summary>The points a rank asks of this player: its own, or more for one who comes to it self-taught.</summary>
        public static int Need(StudyFile f, Profession p, int rank)
        {
            ProRank r = p.Rank(rank);
            return r.Need + (HoldsQuals(f, r) ? 0 : SelfTaughtExtra);
        }

        private static bool HoldsQuals(StudyFile f, ProRank r)
        {
            if (r.Quals.Length == 0)
            {
                return true;
            }
            for (int i = 0; i < r.Quals.Length; i++)
            {
                if (f.Holds(r.Quals[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// True if the player holds what a rank needs: one of its courses (or comes to it self-taught where the profession
        /// allows), its certificate, and what each rank below it needs, for one who comes in at the rank from outside.
        /// </summary>
        public static bool Qualified(StudyFile f, Profession p, int rank)
        {
            int certs = StudyRules.Certs(f);
            for (int r = 0; r <= rank && r < p.Ranks.Length; r++)
            {
                ProRank pr = p.Ranks[r];
                if (pr.Cert >= 0 && (certs & (1 << pr.Cert)) == 0)
                {
                    return false;
                }
                if (!HoldsQuals(f, pr) && !(SelfTaught(p) && r == 0))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>True if the player would be considered at all: qualified, and within ten points of what the rank asks.</summary>
        public static bool Eligible(StudyFile f, Profession p, int rank)
        {
            return Qualified(f, p, rank) && Points(f, p, rank) >= Need(f, p, rank) - 10;
        }

        /// <summary>The highest rank of a profession the player could come in at now; -1 for none.</summary>
        public static int BestRank(StudyFile f, Profession p, int experience)
        {
            int best = -1;
            for (int r = 0; r < p.Ranks.Length; r++)
            {
                if (p.Ranks[r].Exec)
                {
                    break;
                }
                if (Eligible(f, p, r) && experience >= YearsDays(p, r))
                {
                    best = r;
                }
            }
            return best;
        }

        /// <summary>Days of the profession an employer wants to see before taking someone on at a rank from outside, written for a 365-day year: the ranks below's time.</summary>
        public static int YearsDays(Profession p, int rank)
        {
            int days = 0;
            for (int r = 1; r <= rank && r < p.Ranks.Length; r++)
            {
                days += p.Ranks[r].Days;
            }
            return days / 2;   // (half the time of rising inside: a move is how people climb faster)
        }

        /// <summary>
        /// How an employer scores an application, 0 to 100: half for turning up qualified, then the skills against the rank
        /// (up to 20 for being over it, three a point off for being under), experience, references, the player's name,
        /// a friend's word or the recruitment office, honours and a master's, a record, and the city's economy.
        /// </summary>
        public static int HireScore(Candidate c)
        {
            if (!c.Qualified)
            {
                return 0;
            }
            int fit = c.Points - c.Need;
            int s = 50 + (fit >= 0 ? Math.Min(20, fit) : fit * 3);
            s += Math.Min(12, c.Experience / 20);
            s += Math.Min(8, c.Good * 4) - c.Poor * 10;
            s += TalentCatalogue.Clamp(c.Reputation, -10, 10);
            s += (c.Referral ? 10 : 0) + (c.Agency ? 5 : 0) + (c.Honours ? 4 : 0) + (c.Master ? 4 : 0);
            s -= c.Record ? 15 : 0;
            s += c.Cycle == 0 ? 6 : c.Cycle == 2 ? -6 : c.Cycle >= 3 ? -12 : 0;
            s -= c.Summer && c.Rank == 0 ? 5 : 0;
            return TalentCatalogue.Clamp(s, 0, 100);
        }

        /// <summary>True if the application gets an invitation: its score and the day's luck.</summary>
        public static bool Screened(int score, int luck) => score > 0 && score + TalentCatalogue.Clamp(luck, -ScreenLuck, ScreenLuck) >= ScreenPass;

        /// <summary>True if the interview brings an offer.</summary>
        public static bool Offered(int score, int luck) => score > 0 && score + TalentCatalogue.Clamp(luck, -InterviewLuck, InterviewLuck) >= OfferPass;

        /// <summary>
        /// The points of score an application was short of what the employer wanted at the step that turned it down
        /// (<see cref="ScreenPass"/> at the screening, <see cref="OfferPass"/> after the interview), before luck: 0 when the
        /// score was enough on average and the day's luck turned it down (it was close).
        /// </summary>
        public static int ScoreShort(int score, bool interview)
        {
            return Math.Max(0, (interview ? OfferPass : ScreenPass) - Math.Max(0, score));
        }

        /// <summary>The luck of a step of an application, the same on a reload.</summary>
        public static int Luck(int seed, int id, int step, int most) => TechRandom.Range(seed, 8101, id, step, -most, most);

        /// <summary>The first day the player may apply again for a post an employer turned them down for on <paramref name="refused"/>.</summary>
        public static int ReapplyFrom(int refused) => refused + ReapplyDays;

        /// <summary>
        /// The day an application was turned down, for a save from before that day was kept: from what it holds, or -1 (it then
        /// bars nothing): the answer's day at the screening, the interview's after it; an offer withdrawn is not kept, so -1.
        /// </summary>
        public static int RefusedDay(Application a)
        {
            if (a.Stage != (int)ApplyStep.Rejected || a.Offer > 0)
            {
                return -1;
            }
            return a.InterviewDay > 0 ? a.InterviewDay : a.Answer > 0 ? a.Answer : -1;
        }

        /// <summary>What an offer pays, cents an hour at the start's prices: the post's rate, a little more the better the candidate.</summary>
        public static int OfferPay(int postPay, int score)
        {
            int factor = TalentCatalogue.Clamp(950 + (score - OfferPass) * 3, 950, 1100);
            return (int)((long)postPay * factor / 1000);
        }

        /// <summary>A post's rate, cents an hour at the start's prices: the rank's, by the kind of employer and the market's shortage of the family (thousandths).</summary>
        public static int PostPay(Profession p, int rank, EmployerKind kind, int shortage)
        {
            long pay = (long)p.Rank(rank).Pay * KindFactor(kind) / 1000;
            return (int)(pay * TalentCatalogue.Clamp(shortage, 850, 1500) / 1000);
        }

        /// <summary>The player asks for more: the chance (per cent) the employer agrees, by the score, the size of the ask and how often they have asked.</summary>
        public static int AskChance(int score, int askBp, int asks)
        {
            return TalentCatalogue.Clamp(60 + (score - 70) * 2 - askBp / 50 - asks * 20, 5, 90);
        }

        /// <summary>An ask turned down: true if the employer takes the offer back (a large ask, or a second one, now and then).</summary>
        public static bool Withdraws(int askBp, int asks, int roll) => (askBp >= AskLargeBp || asks >= 1) && roll < 150;

        /// <summary>True on a working day of a profession's week (day 1 a Monday).</summary>
        public static bool WorkDay(Profession p, int day) => StudyRules.On(p.Days, day);

        /// <summary>True if a working day can be started at this minute, with the minutes it still has; late if after its hour by more than a quarter.</summary>
        public static bool CanStart(Profession p, int day, int minuteOfDay, out int minutes, out bool late)
        {
            minutes = 0;
            late = false;
            if (!WorkDay(p, day))
            {
                return false;
            }
            int start = p.Hour * 60;
            if (minuteOfDay < start - ClockEarlyMinutes || minuteOfDay > start + ClockLateMinutes)
            {
                return false;
            }
            minutes = p.Hours * 60 - Math.Max(0, minuteOfDay - start);
            late = minuteOfDay > start + LateAfterMinutes;
            return minutes > 0;
        }

        /// <summary>The minutes of a working day the player still owes it, having worked some of it already.</summary>
        public static int Remaining(Profession p, Job j, int day, int minuteOfDay)
        {
            int end = (p.Hour + p.Hours) * 60;
            int left = Math.Min(end - minuteOfDay, p.Hours * 60 - (j.TodayDay == day ? j.TodayMinutes : 0));
            return Math.Max(0, left);
        }

        /// <summary>True once a working day is over: it counts as worked or missed.</summary>
        public static bool DayOver(Profession p, int day, int today, int minuteOfDay)
        {
            return WorkDay(p, day) && (day < today || (day == today && minuteOfDay >= (p.Hour + p.Hours) * 60));
        }

        /// <summary>A working day settled: worked if three quarters of it was, missed if not. Returns true for worked. A day before the job began is nothing.</summary>
        public static bool Settle(Job j, int day)
        {
            Profession p = j.Def;
            if (p == null || day <= j.SettledDay || day < j.Hired || !WorkDay(p, day))
            {
                return false;
            }
            j.SettledDay = day;
            j.Due++;
            bool worked = j.TodayDay == day && j.TodayMinutes * 1000 >= p.Hours * 60 * CountedShare;
            if (worked)
            {
                j.Worked++;
            }
            else
            {
                j.Absent++;
            }
            return worked;
        }

        /// <summary>A settled day that was not worked, excused (off sick, on leave, an internet outage on a day from home): taken back out of the days due and missed, so it is neither.</summary>
        public static void Excuse(Job j)
        {
            j.Absent = Math.Max(0, j.Absent - 1);
            j.Due = Math.Max(0, j.Due - 1);
        }

        /// <summary>The minutes of the save (day * 1440 + minute) a working day can be started in: <see cref="ClockEarlyMinutes"/> before its hour to <see cref="ClockLateMinutes"/> after.</summary>
        public static void StartWindow(Profession p, int day, out int from, out int to)
        {
            int start = day * 24 * 60 + (p != null ? p.Hour : 0) * 60;
            from = start - ClockEarlyMinutes;
            to = start + ClockLateMinutes;
        }

        /// <summary>
        /// True if a working day not worked is excused for the home's internet: a day worked from home, with the internet
        /// down over its start window for an outage (StudyRules.Excuses). No pay, no missed day, no warning, like a sick day.
        /// </summary>
        public static bool DayExcused(WorkMode mode, int day, HomeNet net)
        {
            return FromHome(mode, day) && StudyRules.Excuses(net);
        }

        /// <summary>The minutes of the save an invitation's interview can be joined in: <see cref="InterviewEarlyMinutes"/> before its hour to <see cref="InterviewLateMinutes"/> after.</summary>
        public static void InterviewWindow(Application a, out int from, out int to)
        {
            int at = a.InterviewDay * 24 * 60 + a.InterviewHour * 60;
            from = at - InterviewEarlyMinutes;
            to = at + InterviewLateMinutes;
        }

        /// <summary>True once an invitation's interview can no longer be joined (its day gone, or its window shut today) and it is still waiting.</summary>
        public static bool InterviewGone(Application a, int today, int minuteOfDay)
        {
            return a.Stage == (int)ApplyStep.Interview
                   && (today > a.InterviewDay || (today == a.InterviewDay && minuteOfDay > a.InterviewHour * 60 + InterviewLateMinutes));
        }

        /// <summary>True if an interview gone by is moved rather than lost: one by video from home (a remote post), with the home's internet down over its window for an outage.</summary>
        public static bool InterviewExcused(Application a, HomeNet net)
        {
            return a.Post.Work == (int)WorkMode.Remote && StudyRules.Excuses(net);
        }

        /// <summary>The first weekday on or after a day (day 1 a Monday): interviews are held on weekdays.</summary>
        public static int Weekday(int day)
        {
            while ((day - 1) % 7 >= 5)
            {
                day++;
            }
            return day;
        }

        /// <summary>An interview moved, at the same hour, to the first weekday on or after <paramref name="first"/>; the invitation lapses with it then.</summary>
        public static void Reschedule(Application a, int first)
        {
            a.InterviewDay = Weekday(first);
            a.Lapse = a.InterviewDay;
        }

        /// <summary>Minutes worked: counted for the day and the week (a new week each seventh day).</summary>
        public static void Worked(Job j, int day, int minutes)
        {
            if (j.TodayDay != day)
            {
                j.TodayDay = day;
                j.TodayMinutes = 0;
            }
            j.TodayMinutes += Math.Max(0, minutes);
            int week = (day - 1) / 7;
            if (week != j.WeekOf)
            {
                j.WeekOf = week;
                j.WeekMinutes = 0;
            }
            j.WeekMinutes += Math.Max(0, minutes);
        }

        /// <summary>What so many minutes pay, cents at the start's prices.</summary>
        public static long Wage(int payCents, int minutes) => (long)payCents * Math.Max(0, minutes) / 60;

        /// <summary>The share of a job's working days worked since its last review, thousandths (all of them before any was due).</summary>
        public static int Attendance(Job j) => j.Due <= 0 ? 1000 : j.Worked * 1000 / j.Due;

        /// <summary>Days of notice a rank gives: a week, two from the third rung, four at the top.</summary>
        public static int NoticeDays(ProRank r, int rank) => r.Exec ? 28 : rank >= 2 ? 14 : 7;

        /// <summary>What a job's reference says: poor after a dismissal or walking out, fair after short or patchy service, good otherwise.</summary>
        public static int Reference(Job j, int day, LeaveReason why)
        {
            if (why == LeaveReason.Dismissed || why == LeaveReason.WalkedOut || why == LeaveReason.Misconduct)
            {
                return 1;
            }
            if (day - j.Hired < 14 || Attendance(j) < 700 || (j.Rating > 0 && j.Rating <= 2))
            {
                return 2;
            }
            return 3;
        }

        /// <summary>A job ended: the spell for the CV.</summary>
        public static Spell Spell(Job j, int day, LeaveReason why)
        {
            return new Spell
            {
                Prof = j.Post.Prof, Top = j.Post.Rank, Kind = j.Post.Kind, Employer = j.Post.Employer, From = j.Hired, To = day, Reference = Reference(j, day, why),
                Leaving = (int)why, Earned = j.Earned
            };
        }

        /// <summary>Days worked in a profession before, from the CV (and the job now, if it is one), in days of a 365-day year.</summary>
        public static int Experience(WorkFile w, int prof, int today, int year)
        {
            long days = 0;
            for (int i = 0; i < w.History.Count; i++)
            {
                if (w.History[i].Prof == prof)
                {
                    days += Math.Max(0, w.History[i].To - w.History[i].From);
                }
            }
            if (w.Job != null && w.Job.Post.Prof == prof && today > w.Job.Hired)
            {
                days += today - w.Job.Hired;
            }
            return (int)(days * 365 / Math.Max(60, year));
        }

        /// <summary>References on the CV: good ones and poor ones.</summary>
        public static void References(WorkFile w, out int good, out int poor)
        {
            good = poor = 0;
            for (int i = 0; i < w.History.Count; i++)
            {
                good += w.History[i].Reference == 3 ? 1 : 0;
                poor += w.History[i].Reference == 1 ? 1 : 0;
            }
        }

        /// <summary>
        /// True if the reminder that the registration with the recruitment office ends today is due: on its last day
        /// (<paramref name="until"/>), from <see cref="AgencyNoteHour"/>, and not yet sent for that registration
        /// (<paramref name="told"/>: the last day it was sent for, kept in the save, so a reload does not send it again).
        /// </summary>
        public static bool AgencyEnds(int until, int told, int day, int hour)
        {
            return until > 0 && until == day && told != until && hour >= AgencyNoteHour;
        }

        /// <summary>How many vacancies the city has open at once.</summary>
        public static int OpeningsWanted(int cycle, bool agency)
        {
            return Math.Max(4, OpeningsBase + OpeningsByCycle[TalentCatalogue.Clamp(cycle, 0, 3)] + (agency ? OpeningsAgency : 0));
        }

        /// <summary>The rung of a new vacancy from a roll of 0 to 999: mostly the first ones; never the top of a ladder.</summary>
        public static int RankRoll(Profession p, int roll)
        {
            int open = 0;
            while (open < p.Ranks.Length && !p.Ranks[open].Exec)
            {
                open++;
            }
            open = Math.Max(1, Math.Min(open, RankWeights.Length));
            int total = 0;
            for (int r = 0; r < open; r++)
            {
                total += RankWeights[r];
            }
            int at = TalentCatalogue.Clamp(roll, 0, 999) * total / 1000;
            for (int r = 0; r < open; r++)
            {
                if (at < RankWeights[r])
                {
                    return r;
                }
                at -= RankWeights[r];
            }
            return open - 1;
        }

        /// <summary>How a vacancy's days are worked, from a roll: at the place for a profession that cannot be done from home; else now and then hybrid or remote.</summary>
        public static WorkMode ModeRoll(Profession p, int roll)
        {
            if (!p.Remote)
            {
                return WorkMode.OnSite;
            }
            return roll < 200 ? WorkMode.Remote : roll < 550 ? WorkMode.Hybrid : WorkMode.OnSite;
        }

        /// <summary>True if a day of a job in a mode is worked from home: every day remote, Mondays and Fridays hybrid.</summary>
        public static bool FromHome(WorkMode mode, int day)
        {
            int weekday = (day - 1) % 7;
            return mode == WorkMode.Remote || (mode == WorkMode.Hybrid && (weekday == 0 || weekday == 4));
        }
    }
}
