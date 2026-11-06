namespace RealLifeOverhaul.Careers
{
    /// <summary>What a seat at a recruitment office's desk offers.</summary>
    public enum AgencyChoice
    {
        JobsBoard = 0,    // register to look for work (the mod's registration)
        Recruitment = 1   // hire staff through the agency (the game's own dialogue)
    }

    /// <summary>What a choice at the desk leads to.</summary>
    public enum AgencyStep
    {
        Register = 0,     // the registration page
        Recruit = 1,      // the game's recruitment dialogue, going on to the campaign
        NeedBasicHr = 2   // the game's own page that says the Basic HR course is first
    }

    /// <summary>The choice at a recruitment office's desk and what each way needs. Game-free; costs nothing, asked once a seat.</summary>
    public static class AgencyRules
    {
        public const int Choices = 2;   // the desk always offers both, in this order

        /// <summary>True if the way needs the Basic HR course: hiring staff does, looking for work never does.</summary>
        public static bool NeedsBasicHr(AgencyChoice choice) => choice == AgencyChoice.Recruitment;

        /// <summary>Where a choice leads, given whether the Basic HR course is complete.</summary>
        public static AgencyStep Step(AgencyChoice choice, bool basicHrDone)
        {
            if (choice == AgencyChoice.JobsBoard)
            {
                return AgencyStep.Register;
            }
            return basicHrDone ? AgencyStep.Recruit : AgencyStep.NeedBasicHr;
        }

        /// <summary>True if a seat in person offers the choice at all: on the phone the game's own dialogue answers, as it always did.</summary>
        public static bool Offered(bool inPerson, bool jobsOn) => inPerson && jobsOn;
    }
}
