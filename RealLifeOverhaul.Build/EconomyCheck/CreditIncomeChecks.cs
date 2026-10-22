using System;
using System.IO;
using RealLifeOverhaul.Economy;

internal static partial class Program
{
    private static void CreditIncomeLimits()
    {
        const int score = 695;
        const float cityCaps = 2040000f;
        foreach (ProductKind kind in new[] { ProductKind.Overdraft, ProductKind.CreditCard })
        {
            bool card = kind == ProductKind.CreditCard;
            int round = card ? ProductCatalogue.CardRound : ProductCatalogue.OverdraftRound;
            int least = card ? ProductCatalogue.CardLeast : ProductCatalogue.OverdraftLeast;
            int usual = card ? ProductTerms.CardLimit(score, cityCaps) : ProductTerms.OverdraftLimit(score, cityCaps);
            int days = card ? ProductCatalogue.CardIncomeDays : ProductCatalogue.OverdraftIncomeDays;
            for (int rank = 0; rank <= 3; rank++)
            {
                int rating = BankRules.Limit(usual, rank, round, least);
                int low = ProductTerms.IncomeLimit(kind, rating, 80f);
                Check(low == (card ? 4800 : 2400) && low < rating,
                    "credit income: " + kind + " at 695 and 80 a day is " + (card ? 4800 : 2400) + " at bank rank " + rank);
                Check(ProductTerms.IncomeLimit(kind, rating, 100000f) == rating,
                    "credit income: a large income reaches the rating's " + rating + " for " + kind + " at rank " + rank);
                Check(ProductTerms.IncomeLimit(kind, rating, 0f) == least
                      && ProductTerms.IncomeLimit(kind, rating, 1f) == least
                      && ProductTerms.IncomeLimit(kind, rating, -80f) == least,
                    "credit income: negligible, zero or negative income keeps the starter " + kind + " at rank " + rank);
                Check(ProductTerms.IncomeLimit(kind, rating, 81f) == (card ? 4850 : 2400)
                      && ProductTerms.IncomeLimit(kind, rating, 81f) <= 81 * days,
                    "credit income: " + kind + " rounds affordability down to 50 at rank " + rank);
                Check(ProductTerms.IncomeLimit(kind, rating, float.MaxValue) == rating
                      && ProductTerms.IncomeLimit(kind, rating, 80f) == low,
                    "credit income: " + kind + " handles the largest income and is deterministic at rank " + rank);
            }
            Check(ProductTerms.IncomeLimit(kind, 0, 80f) == 0
                  && ProductTerms.IncomeLimit(kind, 0, float.MaxValue) == 0,
                "credit income: a refused " + kind + " stays refused regardless of income");
            Check(ProductTerms.IncomeLimit(kind, least, 80f) == least,
                "credit income: a small city's " + kind + " stays within its rating limit");
        }
        Check(ProductTerms.IncomeLimit(ProductKind.CarFinance, 10000, 80f) == 0,
            "credit income: the revolving income cap offers no purchase finance");

        CreditIncomeSources();
    }

    private static void CreditIncomeSources()
    {
        string root = System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Economy", "Banking");
        string Read(string folder, string file) => File.ReadAllText(System.IO.Path.Combine(root, folder, file));
        string limits = MemberText(Read("Desk", "CreditDesk.Banks.cs"), "public static int LimitAt(");
        Check(limits.Contains("PlayerHelper.CalculateDailyIncome()")
              && limits.IndexOf("ProductTerms.IncomeLimit(", StringComparison.Ordinal)
                 > limits.IndexOf("BankRules.Limit(", StringComparison.Ordinal),
            "credit income: live bank limits use the game's average income after the bank's profile");
        string offers = Read("Desk", "CreditDesk.Offers.cs");
        Check(offers.Contains("offer.Most = LimitAt(kind, score, offer.Bank, true);")
              && limits.Contains("Logger?.Info(") && limits.Contains("if (diagnose && Verbose && limit < ratingLimit)"),
            "credit income: offers use the shared limit and diagnose an income cap only when asked");
        string offer = MemberText(offers, "public ProductOffer Offer(ProductKind kind, Lender bank)");
        Check(offer.Contains("offer.Most = open.Agreed;")
              && offer.IndexOf("offer.Most = open.Agreed;", StringComparison.Ordinal)
                 < offer.IndexOf("offer.Most = LimitAt(", StringComparison.Ordinal),
            "credit income: an existing account keeps its agreed limit, so repricing creates no over-limit debt or fee");
        string bankApp = Read("BankApps", "BankApp.cs");
        Check(bankApp.Contains("CreditDesk.LimitAt(ProductKind.CreditCard, at, Bank)")
              && bankApp.Contains("CreditDesk.LimitAt(ProductKind.Overdraft, at, Bank)"),
            "credit income: the Rating table uses the same income-capped terms as offers");
        Check(Read("Loans", "GameLoans.cs").Contains("public float Limit => BankTerms.OwnCapTotal;"),
            "credit income: game loan utilisation still uses the banks' original caps");
    }
}
