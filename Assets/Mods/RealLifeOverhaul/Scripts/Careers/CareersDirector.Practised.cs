using System;

namespace RealLifeOverhaul.Careers
{
    public sealed partial class CareersDirector
    {
        /// <summary>
        /// A skill learnt by doing something outside a job that the professions do: selling a firm, taking it to the market. The same learning an hour of work gives
        /// (LearnAtWork), so the skills grow by one route only. Nothing while the work part is off.
        /// </summary>
        internal void PractisedSkill(int skill, int hours)
        {
            if (!_workOn || _study == null || _study.Work == null || Save == null || hours <= 0)
            {
                return;
            }
            LearnAtWork(skill, Math.Min(hours, 8), Save.Day);
        }
    }
}
