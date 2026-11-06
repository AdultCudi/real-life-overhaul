namespace RealLifeOverhaul
{
    /// <summary>
    /// How an employee the mod lets go leaves: once, the game's own way, with one wage and one notice. Resign leaves them
    /// on the save's list (the game calls it inside RunHourly, which takes them off right after); called from anywhere
    /// else they would resign again at their next hour, so they are taken off the list too. The wage: the game pays the
    /// count (workedHoursToday) just before the day event and clears it just after, so a count of a day already paid
    /// (by lastWorkedDay, or at hour 0 without it) is cleared first. Game-free; a few comparisons.
    /// </summary>
    public static class StaffLeaveRules
    {
        /// <summary>Why an employee is not let go now; <see cref="None"/> when they are.</summary>
        public enum Refusal
        {
            None = 0,
            Missing = 1,         // no employee to let go
            Candidate = 2,       // a candidate, not yet hired
            NotListed = 3,       // not on the save's list of employees: gone already
            BeingReplaced = 4,   // the headhunter is replacing them: the game's own hours pass them by
            Foreign = 5          // at another player's business in a Going Public session
        }

        /// <summary>
        /// True when an employee's count is of a day the game has paid already, so it is cleared before the resignation
        /// would pay it again.
        /// </summary>
        public static bool CountPaid(int counted, int lastWorkedDay, bool dayKnown, int today, int hour)
        {
            if (counted <= 0)
            {
                return false;
            }
            return dayKnown ? lastWorkedDay < today : hour == 0;
        }

        /// <summary>The hours the resignation pays: the count, or none where the game has paid it already.</summary>
        public static int HoursPaid(int counted, int lastWorkedDay, bool dayKnown, int today, int hour)
        {
            return counted <= 0 || CountPaid(counted, lastWorkedDay, dayKnown, today, hour) ? 0 : counted;
        }

        /// <summary>Whether an employee is the player's to let go now, and if not, why.</summary>
        public static Refusal Why(bool exists, bool candidate, bool listed, bool beingReplaced, bool foreign)
        {
            if (!exists)
            {
                return Refusal.Missing;
            }
            if (candidate)
            {
                return Refusal.Candidate;
            }
            if (!listed)
            {
                return Refusal.NotListed;
            }
            if (beingReplaced)
            {
                return Refusal.BeingReplaced;
            }
            return foreign ? Refusal.Foreign : Refusal.None;
        }

        /// <summary>Why an employee was not let go, for the log.</summary>
        public static string Words(Refusal why)
        {
            switch (why)
            {
                case Refusal.Missing: return "there is no such employee";
                case Refusal.Candidate: return "they are a candidate, not yet hired";
                case Refusal.NotListed: return "they are no longer on the game's list of employees";
                case Refusal.BeingReplaced: return "the headhunter is replacing them";
                case Refusal.Foreign: return "they work at another player's business";
                default: return "they are let go";
            }
        }
    }
}
