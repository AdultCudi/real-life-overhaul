namespace RealLifeOverhaul
{
    /// <summary>
    /// How an employee this mod lets go leaves (Core/StaffLeave does it): once, the game's own way, with one wage and one
    /// notice.
    ///
    /// THE GAME'S OWN WAY. A complaint left unanswered ends in EmployeeInstance.ComplaintResign: no satisfaction left, the
    /// headhunter's replacement where their plan takes it, otherwise Resign (their shifts let go, their tasks done, the
    /// hours on their count paid, the business's security looked at again, their name put on the list of the hour's
    /// notices). Resign leaves them on the save's list of employees: the game calls it inside its employees' hour
    /// (EmployeeHelper.RunHourly), which takes them off the list right after and then shows the hour's notices. Called from
    /// anywhere else, as this mod used to, they stay on the list at no satisfaction, and at their next hour the game's own
    /// check (LowSatisfactionComplaint: under 10) has them resign again: a second message in their chat, a second wage, a
    /// second notice, and a complaint counted against the week's two. Should a satisfaction update the game still has
    /// queued for them (EmployeeInstance.UpdateSatisfactionWork) land first and lift them off nothing, they are never taken
    /// off at all. So the mod takes ComplaintResign's steps and then takes them off the list as that hour does.
    ///
    /// THE WAGE. Resign pays the hours on the game's count (workedHoursToday). The game pays every employee's count in its
    /// new day just before it raises the day event (GameManager.NewDay: PayDailyWages, then onNewDay) and clears it in its
    /// employees' day just after (EmployeeHelper.RunDaily). The company's and the tech sector's departures are made in that
    /// day event, where the count still holds the day that has ended, paid already: it is cleared first, as the employees'
    /// day clears it (Core/DayRollGuard clears it the same way after a night that stopped short). A count of today is
    /// owed, and paid. Which day a count is of is the game's own lastWorkedDay (EmployeeInstance.AddWorkedDay sets it as it
    /// counts an hour); without it (a newer game) a count at the first hour of a day is taken for the day before. No part
    /// of this mod lets anybody go in the hour event that opens a day, which comes before the wages.
    ///
    /// Game-free (no Unity or game types): checked offline (EconomyCheck, StaffLeaveChecks.cs). Cost: a few comparisons.
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
        /// True when the hours on an employee's count are of a day the game has paid already (the turn of a day, between its
        /// wages and its employees' day): they are cleared before the resignation, which would pay them again. A count of
        /// nothing has nothing to pay.
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

        /// <summary>Why an employee was not let go, for Player.log.</summary>
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
