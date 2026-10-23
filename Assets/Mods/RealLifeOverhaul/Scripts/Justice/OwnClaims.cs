using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A civil claim the player brings: their construction company sues a client or buyers who will not pay. The court's
    /// fee is paid at filing; the claim is heard some days on and decided by its own seed, its strength on the claimant's
    /// books and counsel's quality; a win pays what was claimed and the fee back. Judged on Justice's day; nothing on a timer.
    /// </summary>
    public static class OwnClaims
    {
        /// <summary>
        /// Files a claim for an amount against a defendant (in words), as strong as its books make it (thousandths). Returns its
        /// number, or 0 with no court to file with or the fee not paid.
        /// </summary>
        public static int File(long amount, string defendant, int strength)
        {
            if (!DebtClaims.Available || amount <= 0)
            {
                return 0;
            }
            int claim = amount > int.MaxValue / 2 ? int.MaxValue / 2 : (int)amount;
            int fee = JusticeLaw.ClaimFee(claim);
            if (!Crime.Pay(fee, "reallife_transaction_court_claim_fee"))
            {
                return 0;
            }
            int today = TimeHelper.CurrentDay;
            var c = new JusticeClaim
            {
                Number = JusticeState.NextNumber++, Amount = claim, Fee = fee, Strength = strength, FiledDay = today, Defendant = defendant ?? string.Empty
            };
            c.Seed = JusticeLaw.Hash("claim" + c.Number) ^ (today * 7919);
            c.HearDay = today + JusticeLaw.ClaimHearLeast + JusticeLaw.Roll(c.Seed, 3) % (JusticeLaw.ClaimHearMost - JusticeLaw.ClaimHearLeast + 1);
            JusticeState.Claims.Add(c);
            LegalPhone.Send("reallife_text_justice_claim_filed", new Dictionary<string, string>
            {
                { "defendant", c.Defendant }, { "amount", JusticeText.Money(claim) }, { "fee", JusticeText.Money(fee) }, { "day", JusticeText.N(c.HearDay) }
            });
            JusticeDirector.Logger?.Info("Justice: claim " + c.Number + " for " + claim + " filed by the player against " + c.Defendant + " (strength " + strength + " in 1000, fee " + fee
                                         + "), heard on day " + c.HearDay + ".");
            return c.Number;
        }

        /// <summary>Where a claim stands: -1 to be heard, 1 won, 0 lost, -2 for none with this number.</summary>
        public static int Result(int number)
        {
            JusticeClaim c = JusticeState.Claim(number);
            return c == null ? -2 : c.Result;
        }

        /// <summary>A claim not yet heard is taken back (a test put back); its fee is not refunded. True if there was one.</summary>
        public static bool Withdraw(int number)
        {
            JusticeClaim c = JusticeState.Claim(number);
            if (c == null || c.Result >= 0)
            {
                return false;
            }
            JusticeState.Claims.Remove(c);
            JusticeDirector.Logger?.Info("Justice: the player's claim " + number + " was taken back unheard (a test put back).");
            return true;
        }

        /// <summary>The day's hearings of the player's claims: each decided by its own seed on or after its day.</summary>
        internal static void Day(int today)
        {
            List<JusticeClaim> claims = JusticeState.Claims;
            for (int i = 0; i < claims.Count; i++)
            {
                JusticeClaim c = claims[i];
                if (c.Result < 0 && today >= c.HearDay)
                {
                    Hear(c, today);
                }
            }
        }

        /// <summary>Test: one claim of the player's to be heard is heard now. False for none with this number to hear.</summary>
        public static bool HearNow(int number, int today)
        {
            JusticeClaim c = JusticeState.Claim(number);
            if (c == null || c.Result >= 0)
            {
                return false;
            }
            Hear(c, today);
            return true;
        }

        private static void Hear(JusticeClaim c, int today)
        {
            int chance = JusticeLaw.ClaimChance(c.Strength, LegalCounsel.Quality(null));
            bool won = JusticeLaw.Roll(c.Seed, 11) < chance;
            c.Result = won ? 1 : 0;
            c.DecidedDay = today;
            if (won)
            {
                Crime.Give(c.Amount + c.Fee, "reallife_transaction_court_award");
            }
            LegalPhone.Send(won ? "reallife_text_justice_claim_won" : "reallife_text_justice_claim_lost", new Dictionary<string, string>
            {
                { "defendant", c.Defendant }, { "amount", JusticeText.Money(c.Amount) }, { "fee", JusticeText.Money(c.Fee) }
            });
            JusticeDirector.Logger?.Info("Justice: the player's claim " + c.Number + " against " + c.Defendant + " was heard (chance " + chance + " in 1000): " + (won ? "won, " + c.Amount
                                         + " and the fee paid to the player." : "lost."));
        }
    }
}
