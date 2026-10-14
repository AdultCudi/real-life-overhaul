using System;

namespace RealLifeOverhaul.Careers
{
    /// <summary>What stands between the player and an offer in a profession. The saved number is never kept.</summary>
    public enum PathStep
    {
        Ready = 0,        // an employer could make an offer now: apply to a vacancy of it
        Studying = 1,     // the course it needs, or one that teaches its skills, is under way
        Course = 2,       // one of the rung's courses is needed
        Certificate = 3,  // a professional certificate is needed
        Skills = 4,       // more points of the rung's two skills
        Closed = 5        // what it needs is taught only at the university, and the university is switched off
    }

    /// <summary>A profession's next step for the player, as the Career app shows it.</summary>
    public struct PathView
    {
        public PathStep Step;
        public int Course;   // the course to take, or the one under way; -1 for none
        public int Short;    // points of the rung's skills still wanted before an offer can come
        public int Chance;   // per cent: the chance that an application now brings an offer; 0 for none
    }

    /// <summary>
    /// The way to an offer, in plain numbers (Careers/Work/ProRules): the chance an application brings one, how many
    /// points short the player is, and what to do first in each profession (a course and where it is taught, more
    /// skill, or a vacancy to wait for). An employer turns down anyone it cannot score high enough with the best luck of
    /// both steps, so a post is open to the player only where an offer can come. Game-free: EconomyCheck runs every one.
    /// </summary>
    public static class ProPath
    {
        private const int Never = 101;

        // ---------------------------------------------------------------- the chance of an offer

        /// <summary>
        /// The chance, per cent, that an application with this score brings an offer: the screening's luck and the
        /// interview's, each even over its range. At least 1 where an offer can come at all.
        /// </summary>
        public static int OfferChance(int score)
        {
            if (score <= 0)
            {
                return 0;
            }
            int screened = 0, offered = 0;
            for (int luck = -ProRules.ScreenLuck; luck <= ProRules.ScreenLuck; luck++)
            {
                screened += ProRules.Screened(score, luck) ? 1 : 0;
            }
            for (int luck = -ProRules.InterviewLuck; luck <= ProRules.InterviewLuck; luck++)
            {
                offered += ProRules.Offered(score, luck) ? 1 : 0;
            }
            if (screened == 0 || offered == 0)
            {
                return 0;
            }
            long all = (2L * ProRules.ScreenLuck + 1) * (2L * ProRules.InterviewLuck + 1);
            return (int)Math.Max(1L, screened * (long)offered * 100L / all);
        }

        /// <summary>True if an application with this score can bring an offer.</summary>
        public static bool CanBeOffered(int score) => OfferChance(score) > 0;

        /// <summary>The points of the rung's skills the candidate still wants before an offer can come: 0 if it can now; over 100 if no points would do (not qualified).</summary>
        public static int PointsShort(Candidate c)
        {
            if (!c.Qualified)
            {
                return Never;
            }
            int points = c.Points;
            for (int more = 0; more <= 100; more++)
            {
                c.Points = points + more;
                if (CanBeOffered(ProRules.HireScore(c)))
                {
                    return more;
                }
            }
            return Never;
        }

        // ---------------------------------------------------------------- with one course more

        /// <summary>True if a course is taught with the options as they are: the school's always, the university's and the certificates while the university is open.</summary>
        public static bool Taught(CourseDef c, bool university)
        {
            return c != null && (c.Kind == CourseKind.Short || c.Kind == CourseKind.Vocational || university);
        }

        /// <summary>True if the player could enrol on a course now as far as their results go: taught, not passed, and what it needs first passed.</summary>
        public static bool Enrollable(StudyFile f, CourseDef c, bool university)
        {
            return Taught(c, university) && !f.Holds(c.Id) && StudyRules.Qualified(f, c);
        }

        private static int SkillWith(StudyFile f, int skill, CourseDef c)
        {
            int points = StudyRules.Points(f, skill);
            if (c != null)
            {
                points += (c.SkillA == skill ? c.GainA : 0) + (c.SkillB == skill ? c.GainB : 0);
            }
            return Math.Min(100, points);
        }

        /// <summary>A rung's points (the first skill three fifths, the second two) once a course is passed too (null: as they are).</summary>
        public static int PointsWith(StudyFile f, Profession p, int rank, CourseDef c)
        {
            int a, b;
            p.SkillsOf(rank, out a, out b);
            return (SkillWith(f, a, c) * 3 + SkillWith(f, b, c) * 2) / 5;
        }

        private static bool HoldsWith(StudyFile f, int[] quals, CourseDef c)
        {
            if (quals.Length == 0)
            {
                return true;
            }
            for (int i = 0; i < quals.Length; i++)
            {
                if (f.Holds(quals[i]) || (c != null && c.Id == quals[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>As <see cref="ProRules.Qualified"/>, once a course is passed too.</summary>
        public static bool QualifiedWith(StudyFile f, Profession p, int rank, CourseDef c)
        {
            int certs = StudyRules.Certs(f) | (c != null && c.Cert >= 0 && c.Cert < 31 ? 1 << c.Cert : 0);
            for (int r = 0; r <= rank && r < p.Ranks.Length; r++)
            {
                ProRank pr = p.Ranks[r];
                if (pr.Cert >= 0 && (certs & (1 << pr.Cert)) == 0)
                {
                    return false;
                }
                if (!HoldsWith(f, pr.Quals, c) && !(ProRules.SelfTaught(p) && r == 0))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>What an employer would read of the player for a rung, once a course is passed too (null: as they are). The rest (references, name, economy) is the game's, from the template.</summary>
        public static Candidate With(StudyFile f, Profession p, int rank, CourseDef c, Candidate template)
        {
            Candidate x = template;
            ProRank r = p.Rank(rank);
            x.Points = PointsWith(f, p, rank, c);
            x.Need = r.Need + (HoldsWith(f, r.Quals, c) ? 0 : ProRules.SelfTaughtExtra);
            x.Qualified = QualifiedWith(f, p, rank, c);
            x.Rank = rank;
            return x;
        }

        /// <summary>True if an offer could come for a rung now, or once a course is passed.</summary>
        public static bool Ready(StudyFile f, Profession p, int rank, CourseDef c, Candidate template)
        {
            Candidate x = With(f, p, rank, c, template);
            return x.Qualified && CanBeOffered(ProRules.HireScore(x));
        }

        /// <summary>How long a course takes to finish, roughly, as a number to compare: its classes and homework first, then its fee.</summary>
        public static long Cost(CourseDef c) => (c.Sessions + c.Homework) * 100000L + c.Fee;

        // ---------------------------------------------------------------- a profession's next step

        /// <summary>The next step to an offer at a rung of a profession: ready, a course under way, a course or a certificate to take, more skill, or closed.</summary>
        public static PathView For(StudyFile f, Profession p, int rank, Candidate template, bool university)
        {
            var v = new PathView { Course = -1 };
            Candidate now = With(f, p, rank, null, template);
            int score = ProRules.HireScore(now);
            if (now.Qualified && CanBeOffered(score))
            {
                v.Step = PathStep.Ready;
                v.Chance = OfferChance(score);
                return v;
            }
            if (!now.Qualified)
            {
                int certs = StudyRules.Certs(f);
                for (int r = 0; r <= rank && r < p.Ranks.Length; r++)
                {
                    ProRank pr = p.Ranks[r];
                    if (!HoldsWith(f, pr.Quals, null) && !(ProRules.SelfTaught(p) && r == 0))
                    {
                        return Needing(f, pr.Quals, university, PathStep.Course, v);
                    }
                    if (pr.Cert >= 0 && (certs & (1 << pr.Cert)) == 0)
                    {
                        CourseDef cert = StudyCatalogue.OfCert(pr.Cert);
                        return Needing(f, cert != null ? new[] { cert.Id } : new int[0], university, PathStep.Certificate, v);
                    }
                }
            }
            v.Short = PointsShort(now);
            v.Step = PathStep.Skills;
            bool live;
            int best = Helping(f, p, rank, template, university, out live);
            if (best >= 0)
            {
                v.Course = best;
                ProRank first = p.Rank(rank);
                v.Step = live ? PathStep.Studying : Array.IndexOf(first.Quals, best) >= 0 && !HoldsWith(f, first.Quals, null) ? PathStep.Course : PathStep.Skills;
            }
            return v;
        }

        /// <summary>One of a rung's courses: the one under way, else the quickest the player can enrol on, else the quickest taught, else closed.</summary>
        private static PathView Needing(StudyFile f, int[] quals, bool university, PathStep step, PathView v)
        {
            int open = -1, taught = -1, any = -1;
            for (int i = 0; i < quals.Length; i++)
            {
                CourseDef c = StudyCatalogue.Find(quals[i]);
                if (c == null)
                {
                    continue;
                }
                if (f.Live(c.Id) != null)
                {
                    v.Step = PathStep.Studying;
                    v.Course = c.Id;
                    return v;
                }
                any = any < 0 ? c.Id : any;
                if (Taught(c, university) && (taught < 0 || Cost(c) < Cost(StudyCatalogue.Find(taught))))
                {
                    taught = c.Id;
                }
                if (Enrollable(f, c, university) && (open < 0 || Cost(c) < Cost(StudyCatalogue.Find(open))))
                {
                    open = c.Id;
                }
            }
            v.Course = open >= 0 ? open : taught >= 0 ? taught : any;
            v.Step = open >= 0 || taught >= 0 ? step : PathStep.Closed;
            return v;
        }

        /// <summary>
        /// The course that helps most toward an offer at a rung: one under way that teaches its skills; else, of those the
        /// player can enrol on that make an offer possible, the one whose length over the chance it brings is least; else
        /// the one that adds the most points. -1 for none.
        /// </summary>
        private static int Helping(StudyFile f, Profession p, int rank, Candidate template, bool university, out bool live)
        {
            live = false;
            int a, b;
            p.SkillsOf(rank, out a, out b);
            for (int i = 0; i < f.Courses.Count; i++)
            {
                CourseDef c = f.Courses[i].Def;
                if (c != null && (c.SkillA == a || c.SkillA == b || c.SkillB == a || c.SkillB == b))
                {
                    live = true;
                    return c.Id;
                }
            }
            int ready = -1, most = -1, mostGain = 0, before = PointsWith(f, p, rank, null);
            long readyCost = long.MaxValue;
            for (int i = 0; i < StudyCatalogue.All.Length; i++)
            {
                CourseDef c = StudyCatalogue.All[i];
                if (!Enrollable(f, c, university))
                {
                    continue;
                }
                Candidate x = With(f, p, rank, c, template);
                int chance = x.Qualified ? OfferChance(ProRules.HireScore(x)) : 0;
                if (chance > 0)
                {
                    long cost = Cost(c) * 100L / chance;   // (the course's length over the chance it brings)
                    if (cost < readyCost)
                    {
                        ready = c.Id;
                        readyCost = cost;
                    }
                    continue;
                }
                int gain = PointsWith(f, p, rank, c) - before;
                if (gain > mostGain || (gain == mostGain && gain > 0 && Cost(c) < Cost(StudyCatalogue.Find(most))))
                {
                    most = c.Id;
                    mostGain = gain;
                }
            }
            return ready >= 0 ? ready : most;
        }

        // ---------------------------------------------------------------- the first step of all

        /// <summary>
        /// The quickest way to a first offer from where the player is: a profession they could be offered now (course -1),
        /// else the profession and the course under way or to take that makes an offer likeliest soonest (the course's
        /// length over the chance of an offer it brings). The better paid first rung wins a tie. False if no single
        /// course would do.
        /// </summary>
        public static bool FirstStep(StudyFile f, Candidate template, bool university, out int prof, out int course)
        {
            prof = -1;
            course = -1;
            long best = long.MaxValue;
            int bestPay = -1;
            for (int i = 0; i < ProCatalogue.All.Length; i++)
            {
                Profession p = ProCatalogue.All[i];
                int pay = p.Ranks[0].Pay;
                if (Ready(f, p, 0, null, template))
                {
                    Keep(0L, pay, p.Id, -1, ref best, ref bestPay, ref prof, ref course);
                    continue;
                }
                for (int k = 0; k < StudyCatalogue.All.Length; k++)
                {
                    CourseDef c = StudyCatalogue.All[k];
                    bool under = f.Live(c.Id) != null;
                    if (!under && !Enrollable(f, c, university))
                    {
                        continue;
                    }
                    Candidate x = With(f, p, 0, c, template);
                    int chance = x.Qualified ? OfferChance(ProRules.HireScore(x)) : 0;
                    if (chance > 0)
                    {
                        Keep(under ? 1L : Cost(c) * 100L / chance, pay, p.Id, c.Id, ref best, ref bestPay, ref prof, ref course);
                    }
                }
            }
            return prof >= 0;
        }

        private static void Keep(long cost, int pay, int p, int c, ref long best, ref int bestPay, ref int prof, ref int course)
        {
            if (cost < best || (cost == best && pay > bestPay))
            {
                best = cost;
                bestPay = pay;
                prof = p;
                course = c;
            }
        }
    }
}
