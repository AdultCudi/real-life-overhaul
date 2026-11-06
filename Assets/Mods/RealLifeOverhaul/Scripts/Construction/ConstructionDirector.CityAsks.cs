using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The director's questions about the city around the company (ConstructionBridges): the one council's planning
    /// sitting and its vote, the courts for a debt either way and a ring found out, and the player's following on Flare. Each
    /// through the guard of the other questions: a fault there is said once and the bridge's default answered.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        internal static int CouncilDay(int day)
        {
            try
            {
                return !Down("CouncilDay") && ConstructionBridges.CouncilDay != null ? Math.Max(day + 1, ConstructionBridges.CouncilDay(day)) : LandRules.CouncilDay(day);
            }
            catch (Exception e)
            {
                Faulty("CouncilDay", e);
                return LandRules.CouncilDay(day);
            }
        }

        /// <summary>The council's vote on an application: 1, 0, or -1 while it is before a meeting of the player's; -2 with no council to ask.</summary>
        internal static int CouncilVote(string lot, int lotClass, BuildUse use, int suit, int reputation, bool now, out int ayes, out int noes)
        {
            ayes = noes = 0;
            try
            {
                return !Down("Council") && ConstructionBridges.Council != null ? ConstructionBridges.Council(lot, lotClass, (int)use, suit, reputation, now, out ayes, out noes) : -2;
            }
            catch (Exception e)
            {
                Faulty("Council", e);
                return -2;
            }
        }

        internal static int Sue(long amount, string defendant, int strength)
        {
            try
            {
                return !Down("Sue") && ConstructionBridges.Sue != null ? ConstructionBridges.Sue(amount, defendant, strength) : 0;
            }
            catch (Exception e)
            {
                Faulty("Sue", e);
                return 0;
            }
        }

        internal static int SuitResult(int number)
        {
            try
            {
                return !Down("SuitResult") && ConstructionBridges.SuitResult != null ? ConstructionBridges.SuitResult(number) : -2;
            }
            catch (Exception e)
            {
                Faulty("SuitResult", e);
                return -2;
            }
        }

        internal static int Unpaid(long amount, string claimant, string what)
        {
            try
            {
                return !Down("Unpaid") && ConstructionBridges.Unpaid != null ? ConstructionBridges.Unpaid(amount, claimant, what) : 0;
            }
            catch (Exception e)
            {
                Faulty("Unpaid", e);
                return 0;
            }
        }

        internal static int BidRigging(long cut)
        {
            try
            {
                return !Down("BidRigging") && ConstructionBridges.BidRigging != null ? ConstructionBridges.BidRigging(cut) : 0;
            }
            catch (Exception e)
            {
                Faulty("BidRigging", e);
                return 0;
            }
        }

        internal static bool Withdraw(int kind, int number)
        {
            try
            {
                return number > 0 && !Down("Withdraw") && ConstructionBridges.Withdraw != null && ConstructionBridges.Withdraw(kind, number);
            }
            catch (Exception e)
            {
                Faulty("Withdraw", e);
                return false;
            }
        }

        internal static int Followers()
        {
            try
            {
                return !Down("Followers") && ConstructionBridges.Followers != null ? Math.Max(0, ConstructionBridges.Followers()) : 0;
            }
            catch (Exception e)
            {
                Faulty("Followers", e);
                return 0;
            }
        }
    }
}
