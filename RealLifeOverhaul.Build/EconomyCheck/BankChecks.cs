using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Economy;

// Checks for the rule that sets the city's banks apart (Scripts/Economy/Banking/Terms/BankRules.cs): that the three banks'
// terms differ by it in rate, limit and fee; that the largest keeps the terms as they were before the bank apps; that
// under the lending threshold every bank refuses on every product whatever its share; that limits never fall and
// rates never rise as the rating rises, at every bank; that no rate goes under the floor; how branches and banks are
// ranked; and what ranking costs.
internal static partial class Program
{
    private const float BankCapTotal = 2040000f;   // the two banks of the game's New York, on their own terms
    private const float BankOwnCap = 2000000f;
    private const int BankOwnRate = 12;

    private static void Banks()
    {
        BankProfilesDiffer();
        BankHouseUnchanged();
        BankRefusesUnderThreshold();
        BankMonotonic();
        BankRateFloor();
        BankFees();
        BankRanks();
    }

    // Each of the three has a profile of its own, and every one of the three figures differs between every two of them.
    private static void BankProfilesDiffer()
    {
        for (int a = 0; a < BankRules.AppsMost; a++)
        {
            for (int b = a + 1; b < BankRules.AppsMost; b++)
            {
                BankProfile pa = BankRules.Of(a), pb = BankRules.Of(b);
                Check(pa.RatePoints != pb.RatePoints, "banks " + a + " and " + b + " differ in rate");
                Check(pa.LimitPercent != pb.LimitPercent, "banks " + a + " and " + b + " differ in limits");
                Check(pa.FeePercent != pb.FeePercent, "banks " + a + " and " + b + " differ in fee");
            }
        }
        // A bank past the third, or a rank not known, has the neutral profile.
        Check(BankRules.Of(3).Equals(BankRules.Of(0)) && BankRules.Of(-1).Equals(BankRules.Of(0)) && BankRules.Of(99).Equals(BankRules.Of(0)), "bank: other ranks are neutral");
        // The differences are small: within two points, half and double.
        for (int r = 0; r < BankRules.AppsMost; r++)
        {
            BankProfile p = BankRules.Of(r);
            Check(Math.Abs(p.RatePoints) <= 2 && p.LimitPercent >= 50 && p.LimitPercent <= 200 && p.FeePercent >= 25 && p.FeePercent <= 100, "bank " + r + ": its profile is a small change");
        }
    }

    // The house bank (rank 0) gives every figure exactly as the desk gave it before the bank apps.
    private static void BankHouseUnchanged()
    {
        for (int score = 300; score <= 850; score += 10)
        {
            int overdraft = ProductTerms.OverdraftLimit(score, BankCapTotal), card = ProductTerms.CardLimit(score, BankCapTotal);
            int car = ProductTerms.FinanceMost(ProductKind.CarFinance, score, BankOwnCap), home = ProductTerms.FinanceMost(ProductKind.Mortgage, score, BankOwnCap);
            bool same = BankRules.Limit(overdraft, 0, ProductCatalogue.OverdraftRound, ProductCatalogue.OverdraftLeast) == overdraft
                        && BankRules.Limit(card, 0, ProductCatalogue.CardRound, ProductCatalogue.CardLeast) == card
                        && BankRules.Most(car, 0) == car && BankRules.Most(home, 0) == home
                        && BankRules.Points(ProductTerms.Points(ProductKind.CreditCard), 0) == ProductTerms.Points(ProductKind.CreditCard);
            Check(same, "house bank: the figures at " + score + " are as before");
        }
        for (float amount = 0f; amount <= 200000f; amount += 777f)
        {
            Check(BankRules.Fee(amount, 0) == LendingTerms.PhoneFee(amount), "house bank: the fee of " + amount + " is as before");
        }
    }

    // "Bad rating must not give loan at all": under the threshold every bank's every limit is nothing, and no bank lends.
    private static void BankRefusesUnderThreshold()
    {
        for (int score = CreditCatalogue.ScoreMin; score < CreditCatalogue.LendingThreshold; score++)
        {
            bool none = !BankRules.Lends(score) && !LendingTerms.For(score, BankOwnCap, BankOwnRate).Lends && LendingTerms.For(score, BankOwnCap, BankOwnRate).Cap == 0f;
            for (int r = 0; r < BankRules.AppsMost + 1; r++)
            {
                none &= BankRules.Limit(ProductTerms.OverdraftLimit(score, BankCapTotal), r, ProductCatalogue.OverdraftRound, ProductCatalogue.OverdraftLeast) == 0
                        && BankRules.Limit(ProductTerms.CardLimit(score, BankCapTotal), r, ProductCatalogue.CardRound, ProductCatalogue.CardLeast) == 0
                        && BankRules.Most(ProductTerms.FinanceMost(ProductKind.CarFinance, score, BankOwnCap), r) == 0
                        && BankRules.Most(ProductTerms.FinanceMost(ProductKind.Mortgage, score, BankOwnCap), r) == 0
                        && BankRules.Most(ProductTerms.BusinessMost(score, 5000f), r) == 0;
            }
            Check(none, "banks: nothing is lent at a rating of " + score);
        }
        for (int score = CreditCatalogue.LendingThreshold; score <= CreditCatalogue.ScoreMax; score++)
        {
            bool some = BankRules.Lends(score);
            for (int r = 0; r < BankRules.AppsMost; r++)
            {
                some &= BankRules.Limit(ProductTerms.CardLimit(score, BankCapTotal), r, ProductCatalogue.CardRound, ProductCatalogue.CardLeast) >= ProductCatalogue.CardLeast
                        && BankRules.Limit(ProductTerms.OverdraftLimit(score, BankCapTotal), r, ProductCatalogue.OverdraftRound, ProductCatalogue.OverdraftLeast) >= ProductCatalogue.OverdraftLeast;
            }
            Check(some, "banks: every bank lends at a rating of " + score);
        }
    }

    // As the rating rises a limit never falls and a rate never rises, at every bank.
    private static void BankMonotonic()
    {
        for (int r = 0; r < BankRules.AppsMost; r++)
        {
            int lastCard = 0, lastOverdraft = 0, lastCar = 0, lastRate = int.MaxValue;
            bool ok = true;
            for (int score = CreditCatalogue.LendingThreshold; score <= CreditCatalogue.ScoreMax; score++)
            {
                int card = BankRules.Limit(ProductTerms.CardLimit(score, BankCapTotal), r, ProductCatalogue.CardRound, ProductCatalogue.CardLeast);
                int overdraft = BankRules.Limit(ProductTerms.OverdraftLimit(score, BankCapTotal), r, ProductCatalogue.OverdraftRound, ProductCatalogue.OverdraftLeast);
                int car = BankRules.Most(ProductTerms.FinanceMost(ProductKind.CarFinance, score, BankOwnCap), r);
                int rate = ProductTerms.RateBp(score, BankOwnRate, BankRules.Points(ProductTerms.Points(ProductKind.CreditCard), r), 1f);
                ok &= card >= lastCard && overdraft >= lastOverdraft && car >= lastCar && rate <= lastRate;
                lastCard = card;
                lastOverdraft = overdraft;
                lastCar = car;
                lastRate = rate;
            }
            Check(ok, "bank " + r + ": limits never fall and rates never rise as the rating rises");
        }
        // At one rating the second bank gives more than the house bank and the third less; the third is cheaper, the second dearer.
        int at = 700;
        int usual = ProductTerms.CardLimit(at, BankCapTotal);
        Check(BankRules.Limit(usual, 1, ProductCatalogue.CardRound, ProductCatalogue.CardLeast) > usual && BankRules.Limit(usual, 2, ProductCatalogue.CardRound, ProductCatalogue.CardLeast) < usual, "banks: the card limits differ by the rule");
        int rate0 = ProductTerms.RateBp(at, BankOwnRate, BankRules.Points(ProductTerms.Points(ProductKind.CreditCard), 0), 1f);
        int rate1 = ProductTerms.RateBp(at, BankOwnRate, BankRules.Points(ProductTerms.Points(ProductKind.CreditCard), 1), 1f);
        int rate2 = ProductTerms.RateBp(at, BankOwnRate, BankRules.Points(ProductTerms.Points(ProductKind.CreditCard), 2), 1f);
        Check(rate1 == rate0 + 100 && rate2 == rate0 - 100, "banks: the card rates differ by a point each way");
    }

    // A point off never takes a rate under the floor, even on the cheapest product at the best rating.
    private static void BankRateFloor()
    {
        bool ok = true;
        for (int own = 0; own <= 3; own++)
        {
            for (int score = CreditCatalogue.LendingThreshold; score <= CreditCatalogue.ScoreMax; score += 5)
            {
                int bp = ProductTerms.RateBp(score, own, BankRules.Points(ProductTerms.Points(ProductKind.Mortgage), 2), 1f);
                ok &= bp >= CreditCatalogue.RateFloor * 100;
            }
        }
        Check(ok, "banks: no rate under the floor");
    }

    private static void BankFees()
    {
        bool ok = true;
        for (float amount = CreditCatalogue.LoanLeast; amount <= 500000f; amount += 913f)
        {
            int f0 = BankRules.Fee(amount, 0), f1 = BankRules.Fee(amount, 1), f2 = BankRules.Fee(amount, 2);
            ok &= f1 >= CreditCatalogue.PhoneFeeLeast && f2 >= CreditCatalogue.PhoneFeeLeast && f1 <= f2 && f2 <= f0;
        }
        Check(ok, "banks: every fee is at least the least, and the second bank's is the lowest");
        Check(BankRules.Fee(0f, 1) == 0 && BankRules.Fee(-5f, 2) == 0, "banks: no fee on nothing");
        Check(BankRules.Fee(100000f, 1) == 750 && BankRules.Fee(100000f, 2) == 1125 && BankRules.Fee(100000f, 0) == 1500, "banks: the fee on 100,000 is 1,500, 750 and 1,125");
        Check(Math.Abs(BankRules.FeeShare(1) - 0.0075f) < 1e-6f, "banks: the second bank's share is half the usual");
    }

    private static void BankRanks()
    {
        var order = new List<int>(8);
        var ranks = new int[8];
        // The game's New York: the larger lender first, whatever the order they are found in.
        int count = BankRules.Rank(new[] { "Jensen Capital", "Vantander Bank" }, new[] { 40000f, 2000000f }, ranks, order);
        Check(count == 2 && ranks[0] == 1 && ranks[1] == 0, "ranks: Vantander Bank first, Jensen Capital second");
        // Two branches of one bank are one bank, ranked by its larger cap.
        count = BankRules.Rank(new[] { "A", "B", "A", "C" }, new[] { 100f, 500f, 900f, 50f }, ranks, order);
        Check(count == 3 && ranks[0] == 0 && ranks[2] == 0 && ranks[1] == 1 && ranks[3] == 2, "ranks: branches share their bank's rank");
        // Equal caps: by name.
        count = BankRules.Rank(new[] { "Zeta", "Alpha", "Mid" }, new[] { 10f, 10f, 10f }, ranks, order);
        Check(count == 3 && ranks[1] == 0 && ranks[2] == 1 && ranks[0] == 2, "ranks: equal caps go by name");
        // No bank at all.
        Check(BankRules.Rank(new string[0], new float[0], ranks, order) == 0, "ranks: no bank, no rank");
        // A negative cap counts as nothing, not as a cap below the others.
        count = BankRules.Rank(new[] { "Odd", "Even" }, new[] { -5f, 0f }, ranks, order);
        Check(count == 2 && ranks[1] == 0 && ranks[0] == 1, "ranks: a negative cap is nothing");
        // Ranking a city of eight branches a thousand times takes well under a millisecond each.
        string[] names = { "A", "B", "C", "D", "A", "B", "E", "F" };
        float[] caps = { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f };
        Stopwatch watch = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            BankRules.Rank(names, caps, ranks, order);
        }
        watch.Stop();
        Check(watch.Elapsed.TotalMilliseconds / 1000.0 < 0.1, "ranks: ranking eight branches takes under 0.1 ms (" + (watch.Elapsed.TotalMilliseconds / 1000.0).ToString("0.0000") + " ms)");
    }
}
