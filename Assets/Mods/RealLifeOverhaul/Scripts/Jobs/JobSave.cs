using System;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    // Game-free: nothing in this file touches the game or the engine, so the round trip and the sums can be checked
    // on their own (RealLifeOverhaul.Build/CatalogueCheck compiles it and compares it with the live pay rules).

    /// <summary>
    /// A shift that was being worked when the game was saved: which job, what it had been paid, and what it was owed
    /// at that moment for work done and not yet paid (worked out by the job's own pay rule for a shift that ends
    /// early through nobody's fault). For an employer's shift also what the basic wage is counted from.
    /// </summary>
    public sealed class SavedShift
    {
        public string JobId;
        public int Day;                 // the game day of the save
        public float Minutes;           // game time of the save, total minutes
        public PaySlip Paid;            // everything the shift had been paid before the save (it is in the save's money already)
        public PaySlip Owed;            // done by hand or by the clock, with tips and bonuses earned, and not paid yet

        public bool Rota;               // an employer's shift
        public string Employer;         // its id
        public float Hourly;            // the basic wage an hour as it was being paid (raises, the pay option and the owner's factor included)
        public bool Late;               // clocked on late: an hour is docked
        public float StartMinutes;      // game time at clock-on
    }

    /// <summary>What a saved shift comes to when it is settled.</summary>
    public struct ShiftSettlement
    {
        public int Work;        // the owed slip's total: paid now
        public int Hours;       // hours of basic wage (an employer's shift only)
        public int Wage;        // ...and what they pay: paid now
        public int UnitsOwed;   // units paid for now: they count towards the level and the day's amount
        public int Units;       // units of the whole shift, paid before the save or now
        public int Tips;        // tips of the whole shift (what an employer's review counts)
        public int Earned;      // the whole shift's money: paid before the save, and now

        /// <summary>What is handed over on loading.</summary>
        public int Total => Work + Wage;

        /// <summary>True if any work was done: only then is it a shift worked at all.</summary>
        public bool Worked => Units > 0;
    }

    /// <summary>
    /// A job in progress across a save. The game can be saved at any moment of a shift, and a shift cannot be taken up
    /// again where it was: its people, its props and its place are found on the street when it starts. So a save made
    /// during a shift carries one line (<see cref="Key"/>) saying what the shift was owed, and a load settles it at
    /// once as a shift that ended early through nobody's fault:
    ///   - what was done and not paid is paid by the job's own rule for stopping early (no bonus for a whole shift);
    ///   - an employer's basic wage is paid for the hours clocked up to the save, by the rule a shift ends by;
    ///   - the units count towards the level and the day's amount, so the day's work cannot be had twice;
    ///   - no strike, no complaint and no mark; and no strike is taken off either: it is not a good shift, only a paid one.
    /// What had been paid before the save is in the save's money and is never paid again: only <see cref="SavedShift.Owed"/> is.
    /// The line is written on every save (empty when no shift is on) and emptied when it has been settled.
    /// </summary>
    public static class JobSave
    {
        public const string Key = "reallife:job_live";
        private const string Version = "1";
        private const int Fields = 11;

        /// <summary>The save's line for a shift. Never null.</summary>
        public static string Write(SavedShift shift)
        {
            if (shift == null || string.IsNullOrEmpty(shift.JobId))
            {
                return string.Empty;
            }
            var text = new StringBuilder(128);
            text.Append(Version).Append('|').Append(Clean(shift.JobId)).Append('|').Append(Int(shift.Day)).Append('|').Append(Float(shift.Minutes)).Append('|');
            Slip(text, shift.Paid);
            text.Append('|');
            Slip(text, shift.Owed);
            text.Append('|').Append(shift.Rota ? '1' : '0').Append('|').Append(Clean(shift.Employer)).Append('|').Append(Float(shift.Hourly))
                .Append('|').Append(shift.Late ? '1' : '0').Append('|').Append(Float(shift.StartMinutes));
            return text.ToString();
        }

        /// <summary>Reads a line back. False, with null, for an empty line or one that does not add up: nothing is then settled.</summary>
        public static bool Read(string text, out SavedShift shift)
        {
            shift = null;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            string[] parts = text.Split('|');
            if (parts.Length < Fields || parts[0] != Version || parts[1].Length == 0)
            {
                return false;
            }
            var read = new SavedShift { JobId = parts[1] };
            if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out read.Day)
                || !Number(parts[3], out read.Minutes) || !Slip(parts[4], out read.Paid) || !Slip(parts[5], out read.Owed)
                || !Number(parts[8], out read.Hourly) || !Number(parts[10], out read.StartMinutes))
            {
                return false;
            }
            read.Rota = parts[6] == "1";
            read.Employer = parts[7];
            read.Late = parts[9] == "1";
            // Nothing a line says can pay more than a shift could have earned: a figure below zero is a line that does not add up.
            if (read.Owed.Units < 0 || read.Paid.Units < 0 || read.Hourly < 0f || read.Owed.Base < 0 || read.Owed.Distance < 0 || read.Owed.Tips < 0
                || read.Owed.Bonus < 0 || read.Owed.Penalty < 0 || (read.Rota && read.Employer.Length == 0))
            {
                return false;
            }
            shift = read;
            return true;
        }

        /// <summary>
        /// What a saved shift is settled at. The work is the owed slip's own total (never below zero); the wage is the hours
        /// from clock-on to the save by <see cref="CareerCatalogue.PaidHours"/>, the rule a shift ends by, and only for a
        /// shift on which some work was done.
        /// </summary>
        public static ShiftSettlement Settle(SavedShift shift)
        {
            var result = new ShiftSettlement();
            if (shift == null)
            {
                return result;
            }
            result.Work = shift.Owed.Total;
            result.UnitsOwed = shift.Owed.Units;
            result.Units = shift.Paid.Units + shift.Owed.Units;
            result.Tips = shift.Paid.Tips + shift.Owed.Tips;
            if (shift.Rota && result.Units > 0)
            {
                result.Hours = CareerCatalogue.PaidHours(shift.Minutes - shift.StartMinutes, shift.Late);
                result.Wage = result.Hours > 0 ? CareerCatalogue.Wage(result.Hours, shift.Hourly) : 0;
            }
            result.Earned = shift.Paid.Total + result.Work + result.Wage;
            return result;
        }

        private static void Slip(StringBuilder text, in PaySlip slip)
        {
            text.Append(Int(slip.Units)).Append(',').Append(Int(slip.Base)).Append(',').Append(Int(slip.Distance)).Append(',')
                .Append(Int(slip.Tips)).Append(',').Append(Int(slip.Bonus)).Append(',').Append(Int(slip.Penalty));
        }

        private static bool Slip(string text, out PaySlip slip)
        {
            slip = default;
            string[] parts = text.Split(',');
            return parts.Length == 6
                   && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out slip.Units)
                   && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out slip.Base)
                   && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out slip.Distance)
                   && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out slip.Tips)
                   && int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out slip.Bonus)
                   && int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out slip.Penalty);
        }

        private static bool Number(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Float(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string Clean(string text) => string.IsNullOrEmpty(text) ? string.Empty : text.Replace("|", string.Empty).Replace(",", string.Empty);
    }
}
