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

        /// <summary>
        /// Opens a case for bribery (Wiring/StandingLinks: a bribe paid to an inspector came out), on a summons like a tax case:
        /// heard and fined by Justice's own table. Its number, or 0 with no court to open it.
        /// </summary>
        public static int FileBribery(int amount)
        {
            if (!Justice.Active || amount <= 0)
            {
                return 0;
            }
            JusticeCase opened = Justice.Summon(new JusticeReport(JusticeLaw.Bribery) { Complied = true, Evidence = new[] { Evidence } });
            if (opened == null)
            {
                return 0;
            }
            JusticeDirector.Logger?.Info("Justice: case " + opened.Number + " opened on a summons for bribery (" + amount + " paid to an inspector came out), to be heard on day " + opened.CourtDay + ".");
            return opened.Number;
        }

        /// <summary>
        /// Opens a case for money taken for a campaign against the law (Civic/CivicOffice, through Wiring/CivicLinks: over a
        /// donor's limit, cash from nobody named, or passed through other people's names), on a summons like a tax case.
        /// Its number, or 0 with no court to open it.
        /// </summary>
        public static int FileCampaignFinance(int amount)
        {
            return Summons(JusticeLaw.CampaignFinance, "money taken for a campaign against the law (" + amount + ") came out");
        }

        /// <summary>
        /// Opens a case for misconduct in public office (Civic/CivicOffice, through Wiring/CivicLinks: a councillor's vote that
        /// decided a motion they had an interest in and did not declare, or a mayor's own motion of the kind), on a summons.
        /// Its number, or 0 with no court to open it.
        /// </summary>
        public static int FileMisconduct()
        {
            return Summons(JusticeLaw.Misconduct, "an interest not declared on a vote that it decided came out");
        }

        /// <summary>
        /// Opens a case for a data protection offence (the tech sector, through Wiring/TechLinks: a breach at a firm that was
        /// off the register, or breached before), on a summons like a tax case: heard and fined by Justice's own table, on
        /// top of the regulator's own fine. Its number, or 0 with no court to open it.
        /// </summary>
        public static int FileDataProtection(string firm)
        {
            return Summons(JusticeLaw.DataProtection, "a data protection offence (a breach at " + firm + ")");
        }

        private static int Summons(string charge, string what)
        {
            if (!Justice.Active)
            {
                return 0;
            }
            JusticeCase opened = Justice.Summon(new JusticeReport(charge) { Complied = true, Evidence = new[] { Evidence } });
            if (opened == null)
            {
                return 0;
            }
            JusticeDirector.Logger?.Info("Justice: case " + opened.Number + " opened on a summons: " + what + "; to be heard on day " + opened.CourtDay + ".");
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
