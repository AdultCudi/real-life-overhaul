using System.Collections.Generic;

namespace RealLifeOverhaul.Careers
{
    public sealed partial class CareersDirector
    {
        /// <summary>Current quotes use the enrollment owner's rules on detached candidate records, never enrollment.</summary>
        public CompanionModule CompanionEducation()
        {
            if (_broken)
            {
                return new CompanionModule("failed");
            }
            if (!_studyOn || _study == null)
            {
                return new CompanionModule("unavailable");
            }
            return EducationCompanion.Capture(_study, Save.Day, Prices.Level, CareersGame.Cash, Council, LoanOn, Offered);
        }
    }
}
