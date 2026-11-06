using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Criminal cases other features open on a summons, ordinary Justice cases from then on. Each File returns the case
    /// number, or 0 with no court to open it. Nothing here runs on a timer.
    /// </summary>
    public static class TaxCases
    {
        /// <summary>How strong such a case is: the books, or the record, are in front of the court.</summary>
        public const float Evidence = 0.8f;

        /// <summary>Tax kept back and found by an audit: the sum is the case's, for the charge the court reads out.</summary>
        public static int File(int amount)
        {
            return amount <= 0 ? 0 : Told(Open(JusticeLaw.TaxEvasion, " for tax kept back (" + amount + " found by an audit),"), amount);
        }

        // A case opened with the sum it is about, which the court reads out with the charge; its number, 0 for none.
        private static int Told(JusticeCase opened, int amount)
        {
            if (opened == null)
            {
                return 0;
            }
            opened.Amount = System.Math.Max(0, amount);
            return opened.Number;
        }

        /// <summary>Shares sold soon after a viral post talking the company up.</summary>
        public static int FileMarketAbuse(string company)
        {
            return Summons(JusticeLaw.MarketAbuse, " for market abuse (shares of " + company + " sold after a viral post about them),");
        }

        public static int FileBribery(int amount)
        {
            return amount <= 0 ? 0 : Told(Open(JusticeLaw.Bribery, " for bribery (" + amount + " paid to an inspector came out),"), amount);
        }

        public static int FileCampaignFinance(int amount)
        {
            return Told(Open(JusticeLaw.CampaignFinance, ": money taken for a campaign against the law (" + amount + ") came out;"), amount);
        }

        /// <summary>An undeclared interest in a vote it decided came out.</summary>
        public static int FileMisconduct()
        {
            return Summons(JusticeLaw.Misconduct, ": an interest not declared on a vote that it decided came out;");
        }

        /// <summary>A ring of the city's builders on a tender the player's company covered for came out: the cut taken.</summary>
        public static int FileBidRigging(long cut)
        {
            return Told(Open(JusticeLaw.BidRigging, " for bid rigging (a cover bid on a tender, " + cut + " taken from the ring, came out),"),
                (int)System.Math.Min(int.MaxValue, System.Math.Max(0L, cut)));
        }

        /// <summary>
        /// A firm's investors' money taken for other uses and not paid back, found by an audit: the sum is the case's, and the audit's
        /// evidence (0 to 1, from how much of it the books show) is the case's strength.
        /// </summary>
        public static int FileInvestorFraud(int amount, float evidence)
        {
            if (amount <= 0)
            {
                return 0;
            }
            JusticeCase opened = Justice.Summon(new JusticeReport(JusticeLaw.InvestorFraud) { Complied = true, Evidence = new[] { System.Math.Max(0f, System.Math.Min(1f, evidence)) } });
            if (opened == null)
            {
                return 0;
            }
            opened.Amount = amount;
            JusticeDirector.Logger?.Info("Justice: case " + opened.Number + " opened on a summons for investor fraud (" + amount + " taken from a firm's investors), to be heard on day "
                                         + opened.CourtDay + ".");
            return opened.Number;
        }

        /// <summary>
        /// A case opened on a summons and not yet heard is taken back (a test put back): it was never before the court. True if
        /// there was one.
        /// </summary>
        public static bool Withdraw(int number)
        {
            List<JusticeCase> cases = JusticeState.Cases;
            for (int i = cases.Count - 1; number > 0 && i >= 0; i--)
            {
                JusticeCase c = cases[i];
                if (c.Number == number && c.Open && c.Stage == CaseStage.Pending)
                {
                    cases.RemoveAt(i);
                    if (JusticeState.HearNow == number)
                    {
                        JusticeState.HearNow = 0;
                    }
                    JusticeDirector.Logger?.Info("Justice: case " + number + " was taken back unheard (a test put back).");
                    return true;
                }
            }
            return false;
        }

        /// <summary>A breach at a firm off the data protection register, or breached before.</summary>
        public static int FileDataProtection(string firm)
        {
            return Summons(JusticeLaw.DataProtection, ": a data protection offence (a breach at " + firm + ");");
        }

        private static int Summons(string charge, string what)
        {
            JusticeCase opened = Open(charge, what);
            return opened != null ? opened.Number : 0;
        }

        private static JusticeCase Open(string charge, string what)
        {
            JusticeCase opened = Justice.Summon(new JusticeReport(charge) { Complied = true, Evidence = new[] { Evidence } });
            if (opened == null)
            {
                return null;
            }
            JusticeDirector.Logger?.Info("Justice: case " + opened.Number + " opened on a summons" + what + " to be heard on day " + opened.CourtDay + ".");
            return opened;
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
