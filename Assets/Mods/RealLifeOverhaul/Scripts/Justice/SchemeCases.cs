using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The courts' door for an inquiry's file (Scripts/Schemes, through the wiring): a case opened on a summons with the
    /// evidence the inquiry gathered and the sum it is about, ordinary from then on; how such a case stands; the case
    /// that comes up next; and a judge who weighs its evidence lightly. Nothing here runs on a timer.
    /// </summary>
    public static class SchemeCases
    {
        /// <summary>
        /// Files the charge with its evidence (0 to 1) and the sum the court reads out. A case opened for it alone takes
        /// its doubt from <paramref name="seed"/>, so the verdict is the save's own and never a fresh roll; charges that
        /// join a case opened the same day stand under that case's. The case's number, or 0 with no court to open it.
        /// </summary>
        public static int File(string charge, float evidence, long amount, int seed, bool complied)
        {
            if (JusticeLaw.Find(charge) == null)
            {
                return 0;
            }
            int next = JusticeState.NextNumber;
            JusticeCase opened = Justice.Summon(new JusticeReport(charge) { Complied = complied, Evidence = new[] { Math.Max(0f, Math.Min(1f, evidence)) } });
            if (opened == null)
            {
                return 0;
            }
            if (opened.Number == next)
            {
                opened.Seed = JusticeLaw.Hash("scheme") ^ seed;
            }
            opened.Amount = (int)Math.Min(int.MaxValue, Math.Max(opened.Amount, amount));
            JusticeDirector.Logger?.Info("Justice: case " + opened.Number + " opened on a summons for " + charge + " after an inquiry (" + amount + " found), to be heard on day "
                                         + opened.CourtDay + ".");
            return opened.Number;
        }

        /// <summary>How a case stands: -1 when the courts no longer hold it, 0 while it is open, else how it ended (a <see cref="CaseOutcome"/> as a number).</summary>
        public static int Outcome(int number)
        {
            JusticeCase c = JusticeState.Loaded ? JusticeState.Find(number) : null;
            return c == null ? -1 : c.Open ? 0 : (int)c.Outcome;
        }

        /// <summary>The number of the open case with a court date that comes up first; 0 for none.</summary>
        public static int Next()
        {
            JusticeCase c = JusticeState.Loaded ? JusticeState.NextOpen : null;
            return c != null && c.Stage == CaseStage.Pending ? c.Number : 0;
        }

        /// <summary>The judge of a case weighs its evidence lightly: so much off every charge still standing, never under the least a charge is tried on. False for a case not waiting to be heard.</summary>
        public static bool Lean(int number, float less)
        {
            JusticeCase c = JusticeState.Loaded ? JusticeState.Find(number) : null;
            if (c == null || !c.Open || c.Stage != CaseStage.Pending || less <= 0f)
            {
                return false;
            }
            List<JusticeCharge> charges = c.Charges;
            for (int i = 0; i < charges.Count; i++)
            {
                if (!charges[i].Dropped)
                {
                    charges[i].Evidence = Math.Max(0.05f, charges[i].Evidence - less);
                }
            }
            JusticeDirector.Logger?.Info("Justice: the evidence in case " + number + " is weighed " + less.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                                         + " lighter on every charge.");
            return true;
        }
    }
}
