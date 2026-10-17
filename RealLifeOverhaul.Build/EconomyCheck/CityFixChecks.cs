using System.Collections.Generic;
using RealLifeOverhaul.Economy;

// The city's economy fixed, each reproduced here:
//   1. a line's shares were booked in the order they came, so a refund share was paid while the charge shares beside it
//      were refused for want of cash;
//   2. a rent or wage refund left the game's tax deducting the whole of the game's figure;
//   3. the goods a business stocks were not noted when its prices could not be judged;
//   4. a guest's hourly work waited on the host's day;
//   5. the verbose month line named next month's lagged rate;
//   6. putting the city's economy back forgot a rival that had opened since the first test;
//   and for the Finance app, what leaves tomorrow had the game's rent without the city's change to it.
internal static partial class Program
{
    private static void CityFixes()
    {
        CityFixRefundsFirst();
        CityFixDeduction();
        CityFixSources();
        CityFixKeepReal();
        CityFixRentTomorrow();
    }

    // The game's ChangeMoneySafe: a charge over the cash is refused, a payment always goes through.
    private static bool CityFixCash(ref int cash, int amount, List<int> booked)
    {
        if (amount < 0 && cash < -amount)
        {
            return false;
        }
        cash += amount;
        booked.Add(amount);
        return true;
    }

    private static void CityFixRefundsFirst()
    {
        int[] parts = CityAdjust.Split(-100, new[] { 200f, -100f });
        int cash = 150;
        var booked = new List<int>();
        int sum = CityAdjust.BookParts(parts, (i, part) => CityFixCash(ref cash, part, booked));
        Check(parts[0] == -200 && parts[1] == 100 && sum == -100 && cash == 50 && booked.Count == 2 && booked[0] == 100,
            "city fix: a line's refund share is booked first, so the charge beside it is paid out of it and the day nets to what it owes (" + sum + ", cash " + cash + ")");
        int poor = 20;
        var none = new List<int>();
        int partly = CityAdjust.BookParts(new[] { -200, 100 }, (i, part) => CityFixCash(ref poor, part, none));
        Check(partly == 100 && poor == 120, "city fix: a charge the cash cannot cover even with the refund is refused alone, and carried");
        Check(MemberText(EconomySource(@"City\CityEconomyDirector.Money.cs"), "private static int BookShared(").Contains("CityAdjust.BookParts(parts,"),
            "city fix: the director books a line's shares through the rule");
    }

    private static void CityFixDeduction()
    {
        float left = 300f;
        float lowered = CityAdjust.Lowered(1000f, ref left);
        float rest = 300f;
        float emptied = CityAdjust.Lowered(200f, ref rest);
        Check(lowered == 700f && left == 0f && emptied == 0f && rest == 100f, "city fix: a refund lowers the deduction by itself, never under nothing");
        string game = EconomySource(@"City\CityGame.cs");
        string book = MemberText(game, "public static bool Book("), figures = MemberText(game, "public static DayBooks DayFigures(");
        Check(book.Contains("LowerDeduction(deducted, amount)") && book.Contains("amount > 0 && !string.IsNullOrEmpty(deducted)")
              && figures.Contains("books.Rent.Add(t.address, RentCategory, -t.amount, Deducted(t))") && figures.Contains("books.Wages.Add(t.address, SalaryCategory, -t.amount, Deducted(t))"),
            "city fix: a rent or wage refund lowers the deduction the game kept under the name of the charges it adjusts");
    }

    private static void CityFixSources()
    {
        string game = EconomySource(@"City\CityGame.cs");
        string ratio = MemberText(game, "private static int PriceRatio(");
        int noted = ratio.IndexOf("buys |= 1 << good.Kind", System.StringComparison.Ordinal), judged = ratio.IndexOf("!judged ||", System.StringComparison.Ordinal);
        Check(noted > 0 && judged > noted && ratio.IndexOf("TryGetValue(district, out index)", System.StringComparison.Ordinal) < noted
              && Occurs(ratio, "return 0;") == 1,
            "city fix: a business's goods are noted as stocked before, and apart from, the judgement of its prices");
        string director = EconomySource(@"City\CityEconomyDirector.cs");
        Check(MemberText(director, "private void OnHour(").Contains("SharedCity.Follows('E') ? Math.Max(_followedDay, _state.LastDay) : _state.LastDay"),
            "city fix: a guest's hour goes on once its own part of the day is done, not when the host's day arrives");
        string day = MemberText(director, "private void OnDay(");
        Check(day.Contains("felt = _state.BaseBefore") && day.Contains("Quarters(felt)") && !day.Contains("Quarters(_state.BaseBefore)"),
            "city fix: the month's line names the rate the month's inflation felt");
    }

    private static void CityFixKeepReal()
    {
        var back = new CityState { LastCopycatDay = 10, CarryRent = 1 };
        back.News.Add(new CityNews { Day = 10, Kind = CityNewsKind.Rival, Name = "Old" });
        var now = new CityState { LastCopycatDay = 30, CarryRent = 40, CarryWages = 41, CarryTakings = 42, CarryLiving = 43 };
        now.News.Add(new CityNews { Day = 30, Kind = CityNewsKind.Rival, Name = "Corner Shop" });
        now.News.Add(new CityNews { Day = 30, Kind = CityNewsKind.Prices, A = 1 });
        CityModel.KeepReal(back, now);
        int rivals = 0;
        foreach (CityNews n in back.News)
        {
            rivals += n.Kind == CityNewsKind.Rival && n.Day == 30 && n.Name == "Corner Shop" ? 1 : 0;
        }
        Check(back.LastCopycatDay == 30 && rivals == 1 && back.News.Count == 2 && back.CarryRent == 40 && back.CarryWages == 41 && back.CarryTakings == 42 && back.CarryLiving == 43,
            "city fix: putting the city back keeps a rival that opened since, its rest and its piece, and what is carried");
        var later = new CityState { LastCopycatDay = 50 };
        CityModel.KeepReal(later, now);
        Check(later.LastCopycatDay == 50 && later.News.Count == 0, "city fix: a later rival in the state put back stays as it was");
        Check(MemberText(EconomySource(@"City\CityEconomyDirector.Tests.cs"), "public void TestBack(").Contains("CityModel.KeepReal(back, _state)"),
            "city fix: the test tool keeps them through the rule");
    }

    private static void CityFixRentTomorrow()
    {
        Check(CityAdjust.With(1000f, 1100, 50) == 1150f && CityAdjust.With(1000f, 1005, 0) == 1000f && CityAdjust.With(1000f, 900, -5) == 900f,
            "city fix: tomorrow's rent is the game's with the city's change and what was carried");
        Check(MemberText(EconomySource(@"Finance\Books.cs"), "public static Obligations Tomorrow(CreditFile file, int today)").Contains("CityEconomyDirector.RentWithCity("),
            "city fix: what leaves tomorrow, and the overdraft's cover, count the city's change to the rent");
    }
}
