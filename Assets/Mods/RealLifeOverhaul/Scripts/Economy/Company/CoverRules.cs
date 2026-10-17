using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Business insurance: premiums, what a claim pays and when, and when an insurer will not renew. Game-free.
    /// The loss stays the other feature's (a fire, a break-in, a fine): a policy pays some of it back later.
    /// </summary>
    public static class CoverRules
    {
        /// <summary>What the district does to a premium, thousandths.</summary>
        public static int District(int kind, int income)
        {
            return CompanyCatalogue.Clamp(1000 + (income - CompanyCatalogue.IncomeMiddle) * CompanyCatalogue.CoverIncomePerPoint[kind],
                CompanyCatalogue.CoverDistrictLeast, CompanyCatalogue.CoverDistrictMost);
        }

        /// <summary>
        /// A day's premium: a base and a share of an average day's takings, by insurer, district, the last term's
        /// claims and today's prices. One at least.
        /// </summary>
        public static int Premium(int kind, int insurer, int takings, int income, int claims, int index)
        {
            // Thousandths of money. Only the base is put at today's prices: the game's takings already are.
            long list = (long)CompanyCatalogue.CoverBase[kind] * CompanyCatalogue.Clamp(index, 500, 4000)
                        + (long)Math.Max(0, takings) * CompanyCatalogue.CoverTakingsShare[kind];
            list = list * CompanyCatalogue.InsurerPrice[insurer] / 1000;
            list = list * District(kind, income) / 1000;
            list = list * (1000 + (long)Math.Max(0, claims) * CompanyCatalogue.ClaimLoading) / 1000;
            return (int)Math.Max(1, Math.Min(int.MaxValue / 4, (list + 500) / 1000));
        }

        /// <summary>The holder's part of a loss: a share, a stated sum at least, never more than the loss.</summary>
        public static int Excess(int insurer, int loss)
        {
            loss = Math.Max(0, loss);
            return Math.Min(loss, Math.Max(CompanyCatalogue.InsurerExcessLeast[insurer], CompanyCatalogue.Share(loss, CompanyCatalogue.InsurerExcessShare[insurer])));
        }

        /// <summary>What a claim pays: the loss up to the cover's most, less the excess.</summary>
        public static int Payout(int kind, int insurer, int loss)
        {
            int counted = Math.Min(Math.Max(0, loss), CompanyCatalogue.CoverMost[kind]);
            return counted - Excess(insurer, counted);
        }

        public static bool Renews(int insurer, int claims)
        {
            return claims < CompanyCatalogue.InsurerClaimsMost[insurer];
        }

        public static bool WillWrite(Policy p, int insurer)
        {
            return (p.Declined & (1 << insurer)) == 0;
        }

        public static void Buy(Policy p, int insurer, int premium, int day, int termDays)
        {
            p.Insurer = insurer;
            p.Premium = Math.Max(1, premium);
            p.From = day;
            p.Until = day + Math.Max(1, termDays) - 1;
            p.Claims = p.Missed = 0;
        }

        /// <summary>The policy ends; the insurers that declined are remembered.</summary>
        public static void End(Policy p)
        {
            p.Insurer = -1;
            p.Premium = p.From = p.Until = p.Claims = p.Missed = 0;
        }

        public static bool Covers(Policy p, int day)
        {
            return p.Active && day >= p.From && day <= p.Until;
        }

        /// <summary>A day's premium was taken or not. True when one miss too many has ended the policy.</summary>
        public static bool Charged(Policy p, bool taken)
        {
            if (taken)
            {
                p.Missed = 0;
                return false;
            }
            p.Missed++;
            if (p.Missed < CompanyCatalogue.PremiumMissedMost)
            {
                return false;
            }
            End(p);
            return true;
        }

        public static bool TermOver(Policy p, int day)
        {
            return p.Active && day > p.Until;
        }

        /// <summary>Term over: renewed counting its claims (true), or declined for good by the insurer.</summary>
        public static bool Renew(Policy p, int kind, int takings, int income, int index, int day, int termDays)
        {
            int insurer = p.Insurer;
            if (!Renews(insurer, p.Claims))
            {
                p.Declined |= 1 << insurer;
                End(p);
                return false;
            }
            Buy(p, insurer, Premium(kind, insurer, takings, income, p.Claims, index), day, termDays);
            return true;
        }

        /// <summary>A covered loss becomes a claim; null if not covered that day or nothing would be paid.</summary>
        public static Claim Open(CompanyState state, Policy p, string place, string name, int kind, int loss, int day)
        {
            if (!Covers(p, day) || loss <= 0)
            {
                return null;
            }
            int payout = Payout(kind, p.Insurer, loss);
            if (payout <= 0)
            {
                return null;
            }
            Trim(state.Claims);
            var claim = new Claim
            {
                Id = state.NextClaim++, Place = place, Name = name ?? string.Empty, Kind = kind, Insurer = p.Insurer, Day = day, Loss = loss,
                Excess = Excess(p.Insurer, Math.Min(loss, CompanyCatalogue.CoverMost[kind])), Payout = payout, Stage = ClaimStage.Open
            };
            state.Claims.Add(claim);
            return claim;
        }

        // Room for one more: the oldest finished claim goes, else the oldest of all.
        private static void Trim(List<Claim> claims)
        {
            while (claims.Count >= CompanyCatalogue.ClaimsMost)
            {
                int at = 0;
                for (int i = 0; i < claims.Count; i++)
                {
                    if (claims[i].Stage == ClaimStage.Paid || claims[i].Stage == ClaimStage.Lapsed)
                    {
                        at = i;
                        break;
                    }
                }
                claims.RemoveAt(at);
            }
        }

        /// <summary>The claim is put in: paid after the insurer's days, and counted in the policy's term.</summary>
        public static bool File(Claim c, Policy p, int day)
        {
            if (c.Stage != ClaimStage.Open)
            {
                return false;
            }
            c.Stage = ClaimStage.Filed;
            c.PayDay = day + CompanyCatalogue.InsurerPayDays[c.Insurer];
            if (p != null && p.Insurer == c.Insurer)
            {
                p.Claims++;
            }
            return true;
        }

        /// <summary>A day for a claim: true if it is paid now. One nobody put in lapses after its days.</summary>
        public static bool Day(Claim c, int day)
        {
            if (c.Stage == ClaimStage.Filed && day >= c.PayDay)
            {
                c.Stage = ClaimStage.Paid;
                return true;
            }
            if (c.Stage == ClaimStage.Open && day > c.Day + CompanyCatalogue.ClaimWindowDays)
            {
                c.Stage = ClaimStage.Lapsed;
            }
            return false;
        }
    }
}
