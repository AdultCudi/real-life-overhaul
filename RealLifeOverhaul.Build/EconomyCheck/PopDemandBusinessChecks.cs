using System;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Population;

// The mod's own businesses on the city's people (the brief bizpop): each keeps its own mechanics and moves its old figure by the people's factor. For each:
// with no population, or on the anchor day (every factor a thousand), its figures are exactly what they were (nothing jumps); a pool the business shares with
// its rivals is the people's, and what the player's business and the rivals hold of it sums to it (money and people conserve); its figures lean the right
// way with the people; and over the ten years the population lives without the player (PopDemandChecks' run) its figures stay within the money scale.
internal static partial class Program
{
    private static void PopDemandBusinessChecks()
    {
        DemandBank();
        DemandBranches();
    }

    // The worst and best of a demand's factor over the ten years, after the anchor.
    private static void DemandRange(PopDemand d, out int least, out int most)
    {
        least = int.MaxValue;
        most = 0;
        for (int n = 0; _demandYears != null && n < _demandYears.Length; n++)
        {
            int f = _demandYears[n][(int)d];
            least = Math.Min(least, f);
            most = Math.Max(most, f);
        }
        if (least == int.MaxValue)
        {
            least = most = 1000;
        }
    }

    // ---------------------------------------------------------------- the own bank: its savers and borrowers are the people's

    private static void DemandBank()
    {
        BankCity c = BankCity.Quiet(60);
        var neutral = c;
        neutral.People = new int[OwnBankCatalogue.Savers + OwnBankCatalogue.Segments];
        for (int i = 0; i < neutral.People.Length; i++)
        {
            neutral.People[i] = 1000;
        }
        bool same = true;
        for (int k = 0; k < OwnBankCatalogue.Savers; k++)
        {
            same &= OwnBankRules.SavingsPool(neutral, k) == OwnBankRules.SavingsPool(c, k);
        }
        for (int j = 0; j < OwnBankCatalogue.Segments; j++)
        {
            same &= OwnBankRules.DemandPool(neutral, j) == OwnBankRules.DemandPool(c, j);
        }
        Check(same && OwnBankRules.SavingsPool(c, OwnBankCatalogue.People) == OwnBankCatalogue.SavingsCents[OwnBankCatalogue.People],
            "population demand, own bank: with no population, and on the anchor day, the city's savings and borrowing are the catalogue's to the cent");

        var b = new OwnBank();
        OwnBankRules.Found(b, 1, 7, OwnBankRules.CapitalLeast(PriceRules.Start), "Check");
        var richer = neutral;
        richer.People = (int[])neutral.People.Clone();
        richer.People[OwnBankCatalogue.People] = 1300;
        richer.People[OwnBankCatalogue.Savers + OwnBankCatalogue.Poor] = 700;
        long pool = OwnBankRules.SavingsPool(richer, OwnBankCatalogue.People), own = OwnBankRules.SaversWanted(richer, b, OwnBankCatalogue.People, 1000);
        long poolBefore = OwnBankRules.SavingsPool(neutral, OwnBankCatalogue.People), ownBefore = OwnBankRules.SaversWanted(neutral, b, OwnBankCatalogue.People, 1000);
        Check(pool == poolBefore * 1300 / 1000 && own > ownBefore && own < pool && pool - own > poolBefore - ownBefore,
            "population demand, own bank: households saving three tenths more make the city's savings pool three tenths larger; the bank and the rival banks share it, the rivals holding the rest");
        Check(OwnBankRules.DemandPool(richer, OwnBankCatalogue.Poor) == OwnBankRules.DemandPool(neutral, OwnBankCatalogue.Poor) * 700 / 1000
              && OwnBankRules.DemandPool(richer, OwnBankCatalogue.Good) == OwnBankRules.DemandPool(neutral, OwnBankCatalogue.Good),
            "population demand, own bank: fewer poorly rated households borrow less from the city's lenders, each band by its own people");
        var cycle = neutral;
        cycle.Cycle = CycleState.Recession;
        Check(OwnBankRules.DemandPool(cycle, OwnBankCatalogue.Fair) < OwnBankRules.DemandPool(neutral, OwnBankCatalogue.Fair),
            "population demand, own bank: the national cycle still moves what the city borrows, the people's figures on top");

        // Ten years of the people: the city's savings and borrowing within the money scale.
        bool scale = true;
        string worst = string.Empty;
        foreach (PopDemand d in new[] { PopDemand.SaversPeople, PopDemand.SaversFirms, PopDemand.BorrowPoor, PopDemand.BorrowFair, PopDemand.BorrowGood, PopDemand.BorrowShops, PopDemand.BorrowFirms })
        {
            DemandRange(d, out int least, out int most);
            bool ok = least >= 500 && most <= 2000;
            if (!ok && worst.Length == 0)
            {
                worst = d + " " + least + " to " + most;
            }
            scale &= ok;
        }
        DemandRange(PopDemand.SaversPeople, out int sLeast, out int sMost);
        Check(scale, "population demand, own bank: over ten years without the player the city's savings and borrowing stay between half and twice their first day (people's savings "
                     + OwnBankCatalogue.SavingsCents[0] / 100 * sLeast / 1000 / 1000000 + "M to " + OwnBankCatalogue.SavingsCents[0] / 100 * sMost / 1000 / 1000000 + "M)" + (worst.Length > 0 ? ": " + worst : string.Empty));
    }

    // ---------------------------------------------------------------- the bank's branches: what the residents round each come for

    private static void DemandBranches()
    {
        var none = default(BranchPeople);
        var even = new BranchPeople { Sums = 1000, Saving = 1000, Borrowing = 1000, Poor = 1000, Fair = 1000, Good = 1000 };
        bool same = true;
        for (int draw = 0; draw < 1000; draw++)
        {
            same &= BranchRules.Kind(draw, false, even) == BranchRules.Kind(draw, false) && BranchRules.Kind(draw, true, even) == BranchRules.Kind(draw, true)
                    && BranchRules.Asker(draw, even) == BranchRules.Asker(draw) && BranchRules.Kind(draw, false, none) == BranchRules.Kind(draw, false);
        }
        for (int kind = 0; kind < BranchCatalogue.Kinds; kind++)
        {
            same &= BranchRules.Sum(kind, 500, PriceRules.Start, even) == BranchRules.Sum(kind, 500, PriceRules.Start) && BranchRules.Sum(kind, 500, PriceRules.Start, none) == BranchRules.Sum(kind, 500, PriceRules.Start);
        }
        Check(same, "population demand, branches: residents as the city's average, or no population, bring every branch the catalogue's own mix and sums, visit for visit");

        var savers = new BranchPeople { Sums = 1500, Saving = 1400, Borrowing = 600, Poor = 300, Fair = 900, Good = 1500 };
        int deposits = 0, withdrawals = 0, loans = 0, evenDeposits = 0, evenWithdrawals = 0, evenLoans = 0, poor = 0, evenPoor = 0;
        for (int draw = 0; draw < 1000; draw++)
        {
            int k = BranchRules.Kind(draw, false, savers), e = BranchRules.Kind(draw, false);
            deposits += k == BranchCatalogue.Deposit ? 1 : 0;
            withdrawals += k == BranchCatalogue.Withdraw ? 1 : 0;
            loans += k == BranchCatalogue.Loan ? 1 : 0;
            evenDeposits += e == BranchCatalogue.Deposit ? 1 : 0;
            evenWithdrawals += e == BranchCatalogue.Withdraw ? 1 : 0;
            evenLoans += e == BranchCatalogue.Loan ? 1 : 0;
            poor += BranchRules.Asker(draw, savers) == OwnBankCatalogue.Poor ? 1 : 0;
            evenPoor += BranchRules.Asker(draw) == OwnBankCatalogue.Poor ? 1 : 0;
        }
        Check(deposits > evenDeposits && withdrawals < evenWithdrawals && loans < evenLoans && poor < evenPoor
              && BranchRules.Sum(BranchCatalogue.Deposit, 500, PriceRules.Start, savers) == BranchRules.Sum(BranchCatalogue.Deposit, 500, PriceRules.Start) * 1500 / 1000,
            "population demand, branches: round a branch of richer savers more pay in and fewer take out or borrow, the sums are larger, and fewer askers are poorly rated");
        var extreme = new BranchPeople { Sums = 9000, Saving = 9000, Borrowing = 9000, Poor = 9000, Fair = 0, Good = 0 };
        long top = BranchRules.Sum(BranchCatalogue.Loan, 999, PriceRules.Start, extreme);
        Check(top <= BranchRules.Sum(BranchCatalogue.Loan, 999, PriceRules.Start) * BranchCatalogue.PeopleMost / 1000 + 1 && BranchRules.Asker(500, extreme) == OwnBankCatalogue.Poor,
            "population demand, branches: however far a district leans, a counter's sums stay within twice the usual");

        PopDemandState m = _demandLast;
        if (m != null)
        {
            long hh = 0, income = 0, saving = 0;
            for (int d = 0; d < m.Districts; d++)
            {
                hh += m.Households[d];
                income += (long)m.Households[d] * m.IncomeLean[d];
                saving += (long)m.Disposable[d] * m.SavingLean[d];
            }
            long meanIncome = hh > 0 ? income / hh : 1000;
            Check(Math.Abs(meanIncome - 1000) <= 10, "population demand, branches: the districts' income leans average the city's over its households, so the city's branches together keep the catalogue's sums (" + meanIncome + ")");
        }
    }
}
