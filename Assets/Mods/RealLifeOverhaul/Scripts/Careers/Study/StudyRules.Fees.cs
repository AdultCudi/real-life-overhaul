using System;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>Starting a course and what is held, fees and grants, the student loan, and what a child's degree, an academy and tutoring come to.</summary>
    public static partial class StudyRules
    {
        /// <summary>Whether the player may start a course now, and why not.</summary>
        public static EnrolRefusal CanEnrol(StudyFile f, CourseDef c, StudyMode mode)
        {
            if (c == null)
            {
                return EnrolRefusal.Closed;
            }
            if (f.Live(c.Id) != null)
            {
                return EnrolRefusal.Already;
            }
            if (f.Holds(c.Id))
            {
                return EnrolRefusal.Passed;
            }
            if (!Qualified(f, c))
            {
                return EnrolRefusal.Needs;
            }
            if (f.Courses.Count >= LiveMost)
            {
                return EnrolRefusal.Full;
            }
            if (mode == StudyMode.Distance && !c.Distance)
            {
                return EnrolRefusal.NoDistance;
            }
            return EnrolRefusal.None;
        }

        /// <summary>True if the player has passed what the course needs first.</summary>
        public static bool Qualified(StudyFile f, CourseDef c)
        {
            if (c.Needs.Length == 0 && !c.NeedsDegree)
            {
                return true;
            }
            for (int i = 0; i < c.Needs.Length; i++)
            {
                if (f.Holds(c.Needs[i]))
                {
                    return true;
                }
            }
            return c.NeedsDegree && HoldsAny(f, CourseKind.Degree);
        }

        /// <summary>True if any course of a kind has been passed.</summary>
        public static bool HoldsAny(StudyFile f, CourseKind kind)
        {
            for (int i = 0; i < f.Results.Count; i++)
            {
                CourseDef c = f.Results[i].Def;
                if (c != null && c.Kind == kind && f.Results[i].Stage == (int)EnrolStage.Passed)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The best schooling the player's results count as (TalentCatalogue's schools): self-taught with none.</summary>
        public static int School(StudyFile f)
        {
            int best = TalentCatalogue.SelfTaught;
            for (int i = 0; i < f.Results.Count; i++)
            {
                CourseDef c = f.Results[i].Def;
                if (c != null && f.Results[i].Stage == (int)EnrolStage.Passed && c.School > best)
                {
                    best = c.School;
                }
            }
            return best;
        }

        /// <summary>The professional certificates held (TalentCatalogue.Certs), one bit each, as a professional's file keeps them.</summary>
        public static int Certs(StudyFile f)
        {
            int bits = 0;
            for (int i = 0; i < f.Results.Count; i++)
            {
                CourseDef c = f.Results[i].Def;
                if (c != null && c.Cert >= 0 && c.Cert < 31 && f.Results[i].Stage == (int)EnrolStage.Passed)
                {
                    bits |= 1 << c.Cert;
                }
            }
            return bits;
        }

        /// <summary>The courses passed, one bit each by id.</summary>
        public static long Passed(StudyFile f)
        {
            long bits = 0;
            for (int i = 0; i < f.Results.Count; i++)
            {
                int id = f.Results[i].Course;
                if (f.Results[i].Stage == (int)EnrolStage.Passed && id >= 0 && id < StudyCatalogue.IdsMost)
                {
                    bits |= 1L << id;
                }
            }
            return bits;
        }

        /// <summary>
        /// The share of the fee taken off, thousandths: a merit grant for a student whose last result was the top grade, a
        /// bursary for one with little money, and the council's policy on education (+1 a subsidy, -1 a surcharge, which
        /// adds). Never more than <see cref="StudyCatalogue.GrantMost"/> off; a short course has no grants but the council's.
        /// </summary>
        public static int Grant(StudyFile f, CourseDef c, int cashWhole, int priceLevel, int council)
        {
            int off = 0;
            if (c.Kind != CourseKind.Short)
            {
                Enrolment last = LastPass(f);
                if (last != null && last.Grade >= TopGrade(last.Def))
                {
                    off += StudyCatalogue.MeritGrantShare;
                }
                if (cashWhole < Prices(StudyCatalogue.NeedGrantCash, priceLevel))
                {
                    off += StudyCatalogue.NeedGrantShare;
                }
            }
            off += council > 0 ? StudyCatalogue.CouncilShare : council < 0 ? -StudyCatalogue.CouncilShare : 0;
            return TalentCatalogue.Clamp(off, -StudyCatalogue.CouncilShare, StudyCatalogue.GrantMost);
        }

        private static Enrolment LastPass(StudyFile f)
        {
            for (int i = f.Results.Count - 1; i >= 0; i--)
            {
                if (f.Results[i].Stage == (int)EnrolStage.Passed)
                {
                    return f.Results[i];
                }
            }
            return null;
        }

        /// <summary>A course's whole fee after its grant, whole money at the start's prices.</summary>
        public static int Fee(CourseDef c, int grant)
        {
            return c == null ? 0 : Math.Max(0, (int)((long)c.Fee * (1000 - grant) / 1000));
        }

        /// <summary>How many instalments a course's fee is paid in: a degree's thirds, every other course's at once.</summary>
        public static int Instalments(CourseDef c) => c != null && c.Higher ? StudyCatalogue.InstalmentsHigher : 1;

        /// <summary>
        /// The instalment of the fee that falls due now, whole money at the start's prices, or 0: the first at enrolment,
        /// a degree's others once a third and two thirds of its classes have gone by.
        /// </summary>
        public static int InstalmentDue(Enrolment e)
        {
            CourseDef c = e.Def;
            int parts = Instalments(c);
            if (c == null || e.Instalments >= parts)
            {
                return 0;
            }
            int due = e.Instalments == 0 ? 0 : c.Sessions * e.Instalments / parts;
            if (e.Seen < due)
            {
                return 0;
            }
            int whole = Fee(c, e.Grant);
            return e.Instalments == parts - 1 ? whole - whole / parts * (parts - 1) : whole / parts;
        }

        private static int Prices(int amount, int level) => PriceRules.At(amount, level);

        /// <summary>A day's interest on the student loan, cents.</summary>
        public static long LoanInterest(long owed, int daysPerYear)
        {
            return owed <= 0 ? 0 : owed * StudyCatalogue.LoanRateBp / 10000 / Math.Max(60, daysPerYear);
        }

        /// <summary>
        /// What the student loan takes from a day's pay: <see cref="StudyCatalogue.LoanShareBp"/> of what an hour pays over
        /// the threshold, for the hours paid; never more than is owed.
        /// </summary>
        public static long LoanRepayment(long owed, int centsAnHour, int hours, int priceLevel)
        {
            int threshold = PriceRules.At(StudyCatalogue.LoanThreshold, priceLevel);
            if (owed <= 0 || centsAnHour <= threshold || hours <= 0)
            {
                return 0;
            }
            long over = (long)(centsAnHour - threshold) * hours;
            return Math.Min(owed, over * StudyCatalogue.LoanShareBp / 10000);
        }

        /// <summary>True if the loan can pay so much more (whole money at the start's prices).</summary>
        public static bool LoanRoom(StudyFile f, int whole, int priceLevel)
        {
            return f.LoanCents + whole * 100L <= PriceRules.At(StudyCatalogue.LoanMost, priceLevel) * 100L;
        }

        /// <summary>What a child's degree costs the player, whole money at the start's prices: its fee less what the college fund covers.</summary>
        public static int ChildCost(CourseDef c, int fundWhole)
        {
            return c == null ? 0 : Math.Max(0, c.Fee - Math.Max(0, fundWhole));
        }

        /// <summary>
        /// The class a child graduates with (a degree's 1 to 4): by how close they are to the player (0 to 100), a private
        /// school, and their own luck (-15 to 15).
        /// </summary>
        public static int ChildGrade(float closeness, bool privateSchool, int luck)
        {
            int mark = 40 + (int)(Math.Max(0f, Math.Min(100f, closeness)) * 0.3f) + (privateSchool ? 8 : 0) + TalentCatalogue.Clamp(luck, -15, 15);
            return mark >= StudyCatalogue.First ? 4 : mark >= StudyCatalogue.UpperSecond ? 3 : mark >= StudyCatalogue.LowerSecond ? 2 : 1;
        }

        /// <summary>The students an academy teaches a day: as many as its staff can take, of those who come (more with the council's grants up).</summary>
        public static int AcademyStudents(int staff, int council)
        {
            int demand = StudyCatalogue.AcademyDemand * (council > 0 ? 12 : council < 0 ? 8 : 10) / 10;
            return Math.Max(0, Math.Min(staff * StudyCatalogue.AcademyPerStaff, demand + staff * 4));
        }

        /// <summary>What an hour of tutoring pays, cents at the start's prices: a base and more for the subject's points; nothing under the least.</summary>
        public static int TutorRate(int points)
        {
            return points < StudyCatalogue.TutorLeast ? 0 : StudyCatalogue.TutorBase + points * StudyCatalogue.TutorPerPoint;
        }

        /// <summary>The best subject the player could tutor: the skill with the most points among the professions' and teaching.</summary>
        public static int TutorSkill(StudyFile f)
        {
            int best = Skills.Teaching, points = -1;
            for (int s = 0; s < Skills.Count; s++)
            {
                bool subject = Skills.Professional(s) || s == Skills.Teaching;
                if (subject && f.Skill[s] > points)
                {
                    best = s;
                    points = f.Skill[s];
                }
            }
            return best;
        }
    }
}
