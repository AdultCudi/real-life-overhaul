using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Careers
{
    /// <summary>One employer of the city as the vacancies are made from it: a business, an office, a public building.</summary>
    public sealed class EmployerRef
    {
        public string Name = string.Empty, Street = string.Empty, Type = string.Empty, Rival = string.Empty;
        public int Number;
        public EmployerKind Kind;
        public long Professions;   // the professions its type employs (ProCatalogue.EmployedBy), read once a day
    }

    /// <summary>What the city says now of the premises at a post's address (WorkGame.PremisesAt). Game-free.</summary>
    public struct Premises
    {
        public bool Stands;     // the building and its registration are in the city
        public bool Trades;     // a business of the city's (a rival's or nobody's, never the player's or another player's) trades there
        public string Name;     // the business's name as the vacancies write it (TalentGame.NameOf)
        public string Rival;    // the game's rival that owns it, by id; empty for none
    }

    /// <summary>
    /// When the employer of a post stops existing or stops trading where the post is: the business closed (the game's own shut-down of
    /// a failing business, a redevelopment's eviction, a franchise wound up), was bought by the player, or its building came down.
    /// Whoever holds the job is laid off, not made to quit or sacked: a redundancy with its pay, the CV keeping the title and a good
    /// reference, no mark, no notice and no wait before applying again, and a short priority in the next applications. A business
    /// that stands somewhere else under the same name takes the job with it. Game-free, checked offline (EconomyCheck LayoffChecks).
    /// COST: a few string compares for each post, asked once a game hour for the job, the applications and the vacancies.
    /// </summary>
    public static class ClosureRules
    {
        public const int PayLeastDays = 3;       // working days' pay a redundancy pays at the least (a week's pay a year of service above that: LifeRules.SeveranceWeeksPerYear)
        public const int PriorityDays = 21;      // days after a lay-off in which the next applications are answered at once and interviewed the next day
        public const int ShownDays = 30;         // days the Career app says who the player was laid off by
        public const int InterviewAfterDays = 1; // an interview is held this many days after the invitation, for someone laid off (ProRules.InterviewAfterDays otherwise)

        /// <summary>True while a post is still worked at where it is: its premises trade as the post says, or it never had any (the public sector, an office).</summary>
        public static bool Employs(Opening post, Premises at)
        {
            if (post == null || post.Kind == (int)EmployerKind.Public || string.IsNullOrEmpty(post.Street))
            {
                return true;   // the council, the hospitals and the school never close; a post with no address has no premises to lose
            }
            if (!at.Stands)
            {
                return false;
            }
            if (!string.IsNullOrEmpty(post.Rival) && post.Kind != (int)EmployerKind.City)
            {
                // a rival's business: still that rival's, and under its own name unless the post is the company's (a jobs-board ad, a tech firm)
                return at.Trades && at.Rival == post.Rival && (post.Kind == (int)EmployerKind.TechFirm || post.Via == (int)Via.JobsBoard || at.Name == post.Employer);
            }
            if (post.Kind == (int)EmployerKind.City)
            {
                return at.Trades && at.Name == post.Employer;   // a different business in the unit is not the employer
            }
            return true;   // a tech firm's headquarters or an office: the building is all there is (a headquarters that moves is followed by the post, Tech/Sites)
        }

        /// <summary>
        /// The business a post's employer has moved to: the first, by street and number, of the city's employers that is the same
        /// one (a business of the same name, or of the same rival) at another address and employs the post's profession; -1 if none.
        /// </summary>
        public static int Successor(Opening post, IReadOnlyList<EmployerRef> all)
        {
            Profession p = post != null ? post.Def : null;
            if (p == null || all == null || string.IsNullOrEmpty(post.Street) || post.Kind == (int)EmployerKind.Public)
            {
                return -1;
            }
            bool rival = !string.IsNullOrEmpty(post.Rival) && post.Kind != (int)EmployerKind.City;
            if (!rival && post.Kind != (int)EmployerKind.City)
            {
                return -1;   // an office or a headquarters does not move by itself
            }
            int best = -1;
            for (int i = 0; i < all.Count; i++)
            {
                EmployerRef e = all[i];
                if ((e.Street == post.Street && e.Number == post.Number) || !ProCatalogue.Holds(e.Professions, p, e.Type))
                {
                    continue;
                }
                bool same = rival ? e.Rival == post.Rival && (post.Kind == (int)EmployerKind.TechFirm || post.Via == (int)Via.JobsBoard || e.Name == post.Employer)
                    : e.Kind == EmployerKind.City && e.Name == post.Employer;
                if (same && (best < 0 || string.CompareOrdinal(e.Street, all[best].Street) < 0 || (e.Street == all[best].Street && e.Number < all[best].Number)))
                {
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// The redundancy pay when the employer closes, cents at the start's prices (the director scales it by the price level): a week's
        /// pay for each year of service, as the careers' own redundancy has it, but at least <see cref="PayLeastDays"/> working days'
        /// pay, so that someone who had worked a day is not sent away with nothing.
        /// </summary>
        public static long Payment(Job j, int day, int year)
        {
            int years = Math.Max(0, day - j.Hired) / Math.Max(60, year);
            return Math.Max(LifeRules.DayPay(j) * PayLeastDays, LifeRules.WeekPay(j) * years * LifeRules.SeveranceWeeksPerYear);
        }

        /// <summary>
        /// The job ends because its employer is gone: on the CV as <see cref="LeaveReason.EmployerClosed"/> with the rank earned and a
        /// good reference, the day and employer kept for the Career app and the priority, and the council's benefit as for any redundancy.
        /// Nothing is marked: no warning, no ban, no notice runs (the job, with its notice, is gone). Returns the CV line and the pay.
        /// </summary>
        public static Spell Close(WorkFile w, Job j, int day, int year, out long cents)
        {
            cents = Payment(j, day, year);
            Spell s = ProRules.Spell(j, day, LeaveReason.EmployerClosed);
            w.Remember(s);
            w.Job = null;
            w.LaidOffDay = day;
            w.LaidOffFrom = j.Post.Employer;
            w.BenefitWeek = LifeRules.BenefitWeek(LifeRules.WeekPay(j));
            w.BenefitUntil = day + 7 * LifeRules.BenefitWeeks;
            w.BenefitPaid = day;
            return s;
        }

        public const int RepairDays = 60;        // a job left this long ago or less, whose employer is gone and no closing is recorded for, is taken to have ended with it

        /// <summary>
        /// True for a job of the CV that could have ended because its employer closed: left by a resignation, a sacking or a walk out
        /// (the three a player takes for their own failing), at one of the city's businesses.
        /// </summary>
        public static bool Mendable(Spell s)
        {
            return s != null && (s.Leaving == (int)LeaveReason.Resigned || s.Leaving == (int)LeaveReason.Dismissed || s.Leaving == (int)LeaveReason.WalkedOut)
                   && (s.Kind == (int)EmployerKind.City || s.Kind == (int)EmployerKind.Rival);
        }

        /// <summary>
        /// True if a job of the CV ended while or after its employer had stopped trading: the game recorded the business closing
        /// (<paramref name="closedDay"/>, -1 for none) on a day inside the job, or none is recorded (the record is kept some two months)
        /// and the employer is gone now and the job ended within <see cref="RepairDays"/>.
        /// </summary>
        public static bool Mends(Spell s, int closedDay, bool goneNow, int today)
        {
            if (!Mendable(s))
            {
                return false;
            }
            return closedDay >= 0 ? closedDay >= s.From && closedDay <= s.To : goneNow && today - s.To <= RepairDays && s.To <= today;
        }

        /// <summary>
        /// Puts such a job right as the redundancy it was: it reads as the employer closed with a good reference (so it stops costing the
        /// hire score of every application, ten points a poor reference), a restraint clause that came of it is lifted (unless a claim was
        /// filed), and a job left within three weeks gives the priority a lay off gives. Returns true once; a mended job is not mendable again.
        /// </summary>
        public static bool Mend(WorkFile w, Spell s, int today)
        {
            if (!Mendable(s))
            {
                return false;
            }
            s.Leaving = (int)LeaveReason.EmployerClosed;
            s.Reference = 3;
            if (w.RestraintUntil > 0 && w.RestraintEmployer == s.Employer && w.RestraintProf == s.Prof && w.ClaimNumber == 0)
            {
                w.RestraintUntil = 0;
                w.RestraintProf = -1;
                w.RestraintColleagues = 0;
                w.RestraintEmployer = string.Empty;
                w.RestraintWeek = 0;
                w.RestraintTested = false;
            }
            if (w.Job == null && s.To >= w.LaidOffDay && today - s.To <= PriorityDays)
            {
                w.LaidOffDay = s.To;
                w.LaidOffFrom = s.Employer;
            }
            return true;
        }

        /// <summary>True if a restraint clause binds the player after leaving for this reason: not on retiring, a redundancy or a closed employer.</summary>
        public static bool Restrains(LeaveReason why) => why != LeaveReason.Retired && why != LeaveReason.Redundant && why != LeaveReason.EmployerClosed;

        /// <summary>An application to an employer that is gone: over, with no answer owed and no bar on the same post. False if it was over already.</summary>
        public static bool Shut(Application a)
        {
            if (!a.Live)
            {
                return false;
            }
            a.Stage = (int)ApplyStep.Closed;
            return true;
        }

        /// <summary>A job accepted to start when the old one's notice ran out starts soon after a lay-off, not after a notice that no longer runs.</summary>
        public static bool StartSooner(Application a, int day)
        {
            if (a.Stage != (int)ApplyStep.Accepted || a.StartDay <= day + ProRules.StartDays)
            {
                return false;
            }
            a.StartDay = day + ProRules.StartDays;
            return true;
        }

        /// <summary>True while the player, laid off by a closed employer, is applying inside the short priority.</summary>
        public static bool Priority(WorkFile w, int day) => w != null && w.Job == null && w.LaidOffDay > 0 && day >= w.LaidOffDay && day <= w.LaidOffDay + PriorityDays;

        /// <summary>True while the Career app says it was a closed employer that let the player go.</summary>
        public static bool Shown(WorkFile w, int day) => w != null && w.Job == null && w.LaidOffDay > 0 && day >= w.LaidOffDay && day <= w.LaidOffDay + ShownDays;

        /// <summary>The day an employer answers an application sent today: the next morning for someone laid off, else the roll.</summary>
        public static int AnswerDay(WorkFile w, int day, int roll) => Priority(w, day) ? day + ProRules.AnswerDaysLeast : day + roll;

        /// <summary>The days from an invitation to its interview.</summary>
        public static int InterviewAfter(WorkFile w, int day) => Priority(w, day) ? InterviewAfterDays : ProRules.InterviewAfterDays;
    }
}
