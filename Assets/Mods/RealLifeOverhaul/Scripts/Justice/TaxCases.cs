using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A criminal case for tax kept back, opened from outside the justice system (running a business, through
    /// Wiring/CompanyLinks, when an audit finds cash sales that were kept off the books). It is one of Justice's own
    /// cases and nothing else: a summons with a court date and no arrest, on the charge
    /// <see cref="JusticeLaw.TaxEvasion"/>, listed in the Legal file with the others, heard by Justice and sentenced
    /// by its own table and bounds; a conviction is one of Justice's own records, which the credit rating reads as it
    /// reads every conviction. This file adds the two things an outside feature needs and Justice had no entry for:
    /// open one, and ask whether it is still before the court.
    ///
    /// Nothing here runs on a timer. A case is only opened while the justice system is on, has read the save and is
    /// working; otherwise <see cref="File"/> returns 0 and nothing happens.
    /// </summary>
    public static class TaxCases
    {
        /// <summary>How strong the case is: the audit has the books in front of it.</summary>
        public const float Evidence = 0.8f;

        /// <summary>Opens a case for a sum of tax kept back. Returns its number in the court's books, or 0 if there is no court to open it.</summary>
        public static int File(int amount)
        {
            if (!Justice.Active || amount <= 0)
            {
                return 0;
            }
            JusticeCase opened = Justice.Summon(new JusticeReport(JusticeLaw.TaxEvasion) { Complied = true, Evidence = new[] { Evidence } });
            if (opened == null)
            {
                return 0;
            }
            JusticeDirector.Logger?.Info("Justice: case " + opened.Number + " opened on a summons for tax kept back (" + amount + " found by an audit), to be heard on day " + opened.CourtDay + ".");
            return opened.Number;
        }

        /// <summary>
        /// Opens a case for market abuse (Wiring/FlareLinks: shares sold soon after a viral post talking the company up), on a
        /// summons like a tax case: heard and fined by Justice's own table. Its number, or 0 with no court to open it.
        /// </summary>
        public static int FileMarketAbuse(string company)
        {
            if (!Justice.Active)
            {
                return 0;
            }
            JusticeCase opened = Justice.Summon(new JusticeReport(JusticeLaw.MarketAbuse) { Complied = true, Evidence = new[] { Evidence } });
            if (opened == null)
            {
                return 0;
            }
            JusticeDirector.Logger?.Info("Justice: case " + opened.Number + " opened on a summons for market abuse (shares of " + company + " sold after a viral post about them), to be heard on day " + opened.CourtDay + ".");
            return opened.Number;
        }

        /// <summary>True while a case with this number is still before the court.</summary>
        public static bool Open(int number)
        {
            List<JusticeCase> cases = JusticeState.Cases;
            for (int i = 0; number > 0 && i < cases.Count; i++)
            {
                if (cases[i].Number == number)
                {
                    return cases[i].Open;
                }
            }
            return false;
        }
    }
}
