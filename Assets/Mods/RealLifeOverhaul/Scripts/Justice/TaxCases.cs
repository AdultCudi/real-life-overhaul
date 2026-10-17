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

        /// <summary>Tax kept back and found by an audit.</summary>
        public static int File(int amount)
        {
            return amount <= 0 ? 0 : Summons(JusticeLaw.TaxEvasion, " for tax kept back (" + amount + " found by an audit),");
        }

        /// <summary>Shares sold soon after a viral post talking the company up.</summary>
        public static int FileMarketAbuse(string company)
        {
            return Summons(JusticeLaw.MarketAbuse, " for market abuse (shares of " + company + " sold after a viral post about them),");
        }

        public static int FileBribery(int amount)
        {
            return amount <= 0 ? 0 : Summons(JusticeLaw.Bribery, " for bribery (" + amount + " paid to an inspector came out),");
        }

        public static int FileCampaignFinance(int amount)
        {
            return Summons(JusticeLaw.CampaignFinance, ": money taken for a campaign against the law (" + amount + ") came out;");
        }

        /// <summary>An undeclared interest in a vote it decided came out.</summary>
        public static int FileMisconduct()
        {
            return Summons(JusticeLaw.Misconduct, ": an interest not declared on a vote that it decided came out;");
        }

        /// <summary>A breach at a firm off the data protection register, or breached before.</summary>
        public static int FileDataProtection(string firm)
        {
            return Summons(JusticeLaw.DataProtection, ": a data protection offence (a breach at " + firm + ");");
        }

        private static int Summons(string charge, string what)
        {
            JusticeCase opened = Justice.Summon(new JusticeReport(charge) { Complied = true, Evidence = new[] { Evidence } });
            if (opened == null)
            {
                return 0;
            }
            JusticeDirector.Logger?.Info("Justice: case " + opened.Number + " opened on a summons" + what + " to be heard on day " + opened.CourtDay + ".");
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
