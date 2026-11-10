using RealLifeOverhaul.Dynasty;

namespace RealLifeOverhaul.Careers
{
    /// <summary>CareersDirector's part for a new head of the family (Scripts/Dynasty): the education and the working life as the heir has them.</summary>
    public sealed partial class CareersDirector
    {
        /// <summary>
        /// The founder hands over. Their job ends as a retirement (it goes on the record with its reference, as any job that ends), and the working life starts afresh: the
        /// record of posts, the applications and the benefit were the founder's own. The pension pot is money, so it stays in the family and nothing of it is lost. The study file becomes the heir's (<see cref="DynastyStudy.HandOver"/>):
        /// their share of the skills, their own degree, none of the founder's qualifications. Returns how many qualifications did not pass on.
        /// </summary>
        internal int HandOver(string heir, int share, int day)
        {
            if (_study == null)
            {
                return 0;
            }
            EndJob(LeaveReason.Retired, day, false);
            WorkFile w = _study.Work;
            w.History.Clear();
            w.Applications.Clear();
            w.BenefitUntil = 0;
            w.Retired = 0;
            w.RetiredEarly = false;
            int gone = DynastyStudy.HandOver(_study, heir, share, day);
            Changed();
            Logger?.Info("Careers: a new head of the family; " + gone + " qualification(s) of the founder's did not pass on, the skills are at " + share / 10f + "% of theirs.");
            return gone;
        }
    }
}
