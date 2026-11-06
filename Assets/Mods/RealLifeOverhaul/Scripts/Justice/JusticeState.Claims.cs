using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>A civil claim the player brings (their company's debt to collect): to be heard, or decided and kept a while for the result.</summary>
    public sealed class JusticeClaim
    {
        public int Number;
        public int Amount;
        public int Fee;               // the court's fee paid at filing, back with a win
        public int Strength;          // how strong the claim is on the claimant's books, thousandths
        public int FiledDay;
        public int HearDay;
        public int Seed;
        public int Result = -1;       // -1 to be heard, 0 lost, 1 won
        public int DecidedDay;
        public string Defendant = string.Empty;
    }

    // The claims the player brings, in the save's line as its own section ("|O:"): an older version passes it over.
    public static partial class JusticeState
    {
        public static readonly List<JusticeClaim> Claims = new List<JusticeClaim>(2);

        /// <summary>The claim with a number, or null.</summary>
        public static JusticeClaim Claim(int number)
        {
            for (int i = 0; number > 0 && i < Claims.Count; i++)
            {
                if (Claims[i].Number == number)
                {
                    return Claims[i];
                }
            }
            return null;
        }

        /// <summary>Claims still to be heard.</summary>
        public static int ClaimsOpen
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Claims.Count; i++)
                {
                    n += Claims[i].Result < 0 ? 1 : 0;
                }
                return n;
            }
        }

        private static void WriteClaims(StringBuilder text)
        {
            text.Append("|O:");
            for (int i = 0; i < Claims.Count; i++)
            {
                JusticeClaim c = Claims[i];
                if (i > 0)
                {
                    text.Append(';');
                }
                Join(text, c.Number, c.Amount, c.Fee, c.Strength, c.FiledDay, c.HearDay, c.Seed, c.Result, c.DecidedDay);
                text.Append(',').Append(Escape(c.Defendant));
            }
        }

        private static void ParseClaims(string body)
        {
            foreach (string one in body.Split(Entry, StringSplitOptions.RemoveEmptyEntries))
            {
                var f = new Fields(one);
                var c = new JusticeClaim
                {
                    Number = f.Int(), Amount = f.Int(), Fee = f.Int(), Strength = f.Int(), FiledDay = f.Int(), HearDay = f.Int(), Seed = f.Signed(), Result = f.Signed(), DecidedDay = f.Int(),
                    Defendant = f.Text()
                };
                if (c.Number > 0 && c.Amount > 0 && Claims.Count < JusticeLaw.ClaimsKept * 2)
                {
                    c.Result = c.Result < -1 || c.Result > 1 ? -1 : c.Result;
                    Claims.Add(c);
                }
            }
        }

        /// <summary>The claims decided long ago go, the newest kept for their results.</summary>
        private static void TrimClaims()
        {
            int decided = Claims.Count - ClaimsOpen;
            for (int i = 0; i < Claims.Count && decided > JusticeLaw.ClaimsKept; i++)
            {
                if (Claims[i].Result >= 0)
                {
                    Claims.RemoveAt(i);
                    decided--;
                    i--;
                }
            }
        }
    }
}
