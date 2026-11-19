using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The officer's file: what they have done on duty, kept apart from the job so that it follows the officer, in its own line of the
    /// save (<see cref="DutyStore"/>). A save from before it, or one loaded by an older build, has none and reads as an officer with a
    /// clean slate. Game-free.
    /// </summary>
    public sealed class DutyFile
    {
        public int Arrests;          // suspects arrested; each is recorded here only, as a city person has no court of the player's
        public int Tickets;          // tickets written
        public int Commendations;    // earned for a settled risky or deadly incident and for a shift ended at a high rating
        public int Aid;              // people given first aid
        public int Talked;           // incidents talked down
        public int Backups;          // calls for backup
        public int BackedOff;        // incidents walked away from
        public int Hurt;             // times injured on duty
        public int Shifts;           // shifts worked on patrol
        public int Answered;         // incidents answered, whatever came of them (it numbers the next roll)
        public int Day = -1;         // the day the shift rating below is for
        public int Rating = PoliceRules.RatingStart;   // today's shift rating
        public int Today;            // incidents answered today, so that a quiet shift is known for one
        public int Count;            // shift ratings in the ring
        public int NextCase = 1;     // the number of the next case in the book
        public int ActionsToday;     // a detective's actions this shift's day, over all cases
        public int NoticeDay = -1;   // the last day a case notice was sent: one a day
        public int PendOpened, PendReleased, PendConvicted, PendAcquitted, PendCommended;   // the day's case news, summed into the one notice
        public readonly System.Collections.Generic.List<OfficerCase> Cases = new System.Collections.Generic.List<OfficerCase>(8);   // the case book
        public int RingDay = -1;     // the day of the rating the ring ends with: a shift begun again the same day is the same day's
        public readonly int[] Ring = new int[PoliceRules.RingSize];   // the ratings of the last shifts, oldest first

        /// <summary>A new shift's rating begins on a day: the quiet shift's, with nothing answered.</summary>
        public void Begin(int day)
        {
            if (Day == day)
            {
                return;
            }
            Day = day;
            Rating = PoliceRules.RatingStart;
            Today = 0;
            ActionsToday = 0;
        }

        /// <summary>The rating of a finished shift goes into the ring; the oldest goes when it is full.</summary>
        public void Close()
        {
            if (Count > 0 && RingDay == Day)
            {
                Ring[Count - 1] = Rating;
                return;
            }
            RingDay = Day;
            StandingFile.PushTo(Ring, ref Count, Rating);
        }
    }
}
