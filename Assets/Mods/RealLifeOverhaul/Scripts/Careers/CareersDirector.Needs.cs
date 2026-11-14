using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// What a post, a rank or a licence asks and the player lacks, said with figures: the qualification missing by name, the points
    /// asked against the points held, and what raises them. The pages show the same asks as ticks (CareerApp, CareerLive), so a
    /// refusal names what the page shows short.
    /// </summary>
    public sealed partial class CareersDirector
    {
        /// <summary>
        /// Why the player cannot apply for a post: the qualification they do not hold, by name; the skills too far below what it asks, with
        /// the points asked and held and what raises them; or the score too low for an offer ever to come. Null when they can apply.
        /// <paramref name="template"/> is what an employer reads of the player for any post, when the caller has it already.
        /// </summary>
        internal string ApplyWhy(Opening o, Candidate template) => ApplyWhy(o, true, template);

        /// <summary>The same where the caller has no template: what an employer reads of the player is read for the post alone.</summary>
        internal string ApplyWhy(Opening o) => ApplyWhy(o, false, default);

        private string ApplyWhy(Opening o, bool haveTemplate, Candidate template)
        {
            Profession p = o != null ? o.Def : null;
            if (p == null || _study == null)
            {
                return "reallife_work_why_gone";
            }
            if (!ProRules.Qualified(_study, p, o.Rank))
            {
                var groups = new List<int[]>(2);
                var certs = new List<int>(2);
                StudyNeedRules.PostMissing(_study, p, o.Rank, groups, certs);
                return CareersWhy.With("reallife_work_why_unqualified", "missing", MissingWords(groups, certs));
            }
            if (!ProRules.Eligible(_study, p, o.Rank))
            {
                p.SkillsOf(o.Rank, out int a, out int b);
                int need = ProRules.Need(_study, p, o.Rank);
                return CareersWhy.With("reallife_work_why_skills", "a", CareersText.Skill(a), "b", CareersText.Skill(b), "need", need.ToString(),
                    "have", ProRules.Points(_study, p, o.Rank).ToString(), "least", Math.Max(0, need - 10).ToString(), "raise", RaiseLine(a, -1));
            }
            if (o.Via != (int)Via.Headhunter)
            {
                Candidate c = haveTemplate ? CandidateFor(o, template) : CandidateFor(o);
                if (!ProPath.CanBeOffered(ProRules.HireScore(c)))
                {
                    return CareersWhy.With("reallife_work_why_no_chance", "points", ProPath.PointsShort(c).ToString());   // not even the best luck at both steps would do
                }
            }
            return null;
        }

        // The qualifications a post asks for that the player lacks: any one of a group of courses, and each certificate's course.
        private static string MissingWords(List<int[]> groups, List<int> certs)
        {
            var b = new StringBuilder(96);
            string or = CareersText.T("reallife_campus_or");
            for (int g = 0; g < groups.Count; g++)
            {
                var one = new StringBuilder(64);
                for (int i = 0; i < groups[g].Length; i++)
                {
                    one.Append(i > 0 ? " " + or + " " : string.Empty).Append(CareersText.Course(groups[g][i]));
                }
                b.Append(b.Length > 0 ? "; " : string.Empty).Append(groups[g].Length > 1 ? CareersText.T("reallife_work_missing_one_of", "courses", one.ToString()) : one.ToString());
            }
            for (int i = 0; i < certs.Count; i++)
            {
                b.Append(b.Length > 0 ? "; " : string.Empty).Append(CareersText.Course(certs[i]));
            }
            return b.ToString();
        }

        /// <summary>
        /// The reason the player cannot be put up a rung on the management ladder now, with what is short in figures (days in the rank, the
        /// score, the skills' points, the stores), and what raises the skills; null when nothing stops them.
        /// </summary>
        internal string LadderWhy(LadderFile f)
        {
            string why = CareerLadderRules.WhyNot(f, Save.Day, Year, PointsOf);
            if (why == null || f.Rung >= LadderRanks.CareerTop)
            {
                return why;
            }
            RungDef next = LadderRanks.Of(f.Rung + 1);
            switch (why)
            {
                case "reallife_ladder_why_days":
                    return CareersWhy.With(why, "have", Math.Max(0, Save.Day - f.RungDay).ToString(), "need", CareerLadderRules.DaysNeeded(f, Year).ToString());
                case "reallife_ladder_why_score":
                    return CareersWhy.With(why, "have", CareerLadderRules.ScoreOf(f).ToString(), "need", next.Score.ToString());
                case "reallife_ladder_why_stores":
                    return CareersWhy.With(why, "have", f.Company.Stores.Count.ToString(), "need", next.Stores.ToString());
                case "reallife_ladder_why_recommend":
                    return CareersWhy.With(why, "boss", LadderBoss(f));
                case "reallife_ladder_why_skills":
                    CareerLadderRules.SkillsNeeded(f, PointsOf, out int have, out int need);
                    LadderRanks.SkillsOf(f.Rung + 1, CareerLadderRules.SeatAimed(f, PointsOf), out int a, out int b);
                    return CareersWhy.With(why, "a", CareersText.Skill(a), "b", CareersText.Skill(b), "have", have.ToString(), "need", need.ToString(), "raise", RaiseLine(a, -1));
                default:
                    return why;
            }
        }
    }
}
