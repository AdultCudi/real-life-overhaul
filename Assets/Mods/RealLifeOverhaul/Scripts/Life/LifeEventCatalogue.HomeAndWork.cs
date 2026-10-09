using static RealLifeOverhaul.Need;

namespace RealLifeOverhaul
{
    // The apartment building, the neighbours and running a business. Texts are in Locales\en.json.
    internal static partial class LifeEventCatalogue
    {
        // The mod can't tell whose building the player is standing in, so home events are worded
        // as news that reaches the player wherever they are.
        private static LifeEvent[] Home() => new[]
        {
            E("hm01", None, Warn().Tire(6).Down()).At(6, 10),
            E("hm02", None, Bad().Money(-140, -60, "repair")),
            C("hm03", None,
                Yes(Good().Tire(3).Feel(Mood.Helped).Chance(6), Good().Money(30, 30, "gift").Feel(Mood.Helped).Chance(4)),
                No(Info())),
            E("hm04", None, Good().Feed(12).Up()),
            E("hm05", None, Bad().Money(-110, -50, "repair")),
            E("hm06", None, Warn().Down()),
            C("hm07", None, Yes(Good().Tire(4).Feel(Mood.Helped)), No()),
            E("hm08", None, Warn().Money(-60, -25, "purchase")),
            E("hm09", None, Good().Money(10, 40, "found_money")),
            E("hm10", None, Warn().Tire(3)).At(21, 6),
            C("hm11", None,
                Yes(Good().Tire(6).Feed(8).Up()), No(Info().Chance(1), Good().Rest(4).Chance(1))).At(20, 2),
            E("hm12", None, Bad().Money(-220, -120, "repair").DownBig()),
            E("hm13", None, Info()),
            E("hm14", None, Warn().Money(-70, -30, "purchase")),
            C("hm15", None, 15, "donation", Yes(Good().Feel(Mood.Generous)), No(Info().Down())),
            E("hm16", None, Good().Up()),
            E("hm17", None, Warn().Money(-50, -20, "purchase")),
            E("hm18", None, Good().Feel(Mood.Helped)),
            E("hm19", None, Warn().Tire(5)).At(5, 9),
            C("hm20", OnFoot,
                Yes(Good().Tire(8).Up().Chance(60), Good().Tire(8).Money(15, 45, "found_money").Chance(25),
                    Bad().Tire(8).Money(-60, -30, "repair").Down().Chance(15)),
                No()),
            E("hm21", None, Bad().Money(-95, -40, "repair")),
            E("hm22", None, Info().Up()),
            E("hm23", Indoors, Warn()),
            E("hm24", Indoors, Good().Rest(4)),
            C("hm25", None, 40, "repair", Yes(Good().Up()), No(Info().Chance(1), Warn().Down().Chance(1))),
            C("hm26", None, Yes(Good().Feed(6).Feel(Mood.Helped).Chance(7), Warn().Down().Chance(3)), No())
        };

        private static LifeEvent[] Business() => new[]
        {
            E("bz01", HasBusiness, Good().Up()),
            E("bz02", HasBusiness, Warn().Down()),
            E("bz03", HasBusiness, Bad().Money(-180, -80, "business_expense")),
            E("bz04", HasBusiness, Good().Money(40, 160, "business_income")),
            C("bz05", HasBusiness, 80, "business_expense",
                Yes(Good().Money(100, 260, "business_income").Up().Chance(55), Info().Chance(45)), No()),
            E("bz06", HasBusiness, Bad().Money(-150, -70, "business_expense")),
            E("bz07", HasBusiness, Good().Feel(Mood.Lucky)),
            C("bz08", HasBusiness, 100, "donation", Yes(Good().Feel(Mood.Generous)), No()),
            E("bz09", HasBusiness, Warn().Money(-120, -50, "business_expense")),
            E("bz10", HasBusiness, Info()),
            E("bz11", None, Info().Up()),
            C("bz12", None, 150, "investment",
                Yes(Good().Money(220, 400, "investment").Feel(Mood.Lucky).Chance(35), Bad().Down().Chance(65)), No()).Rich(300),
            E("bz13", HasBusiness, Good().Money(30, 90, "refund")),
            E("bz14", HasBusiness, Warn().Money(-90, -40, "business_expense")),
            E("bz15", HasBusiness | Weekend, Good().Up()).At(12, 22),
            C("bz16", HasBusiness, Yes(Good().Feel(Mood.Helped).Chance(7), Info().Chance(3)), No()),
            E("bz17", HasBusiness, Bad().Money(-110, -40, "business_expense")),
            E("bz18", OnFoot, Info()).At(8, 20),
            E("bz19", HasBusiness | Monday, Warn().Down()),
            C("bz20", HasBusiness, 40, "refund_paid",
                Yes(Info()), No(Good().Chance(1), Warn().Down().Chance(1))),
            E("bz21", Veteran | HasBusiness, Good().Up()),
            E("bz22", Newcomer | OnFoot, Info().Up()),
            E("bz23", HasBusiness, Good().Money(50, 150, "business_income")),
            E("bz24", HasBusiness, Warn().Money(-60, -25, "business_expense")),
            C("bz25", HasBusiness, 60, "business_expense",
                Yes(Good().Money(90, 240, "business_income").Up().Chance(1), Info().Down().Chance(1)), No()),
            C("bz26", OnFoot | Rested,
                Yes(Good().Money(40, 40, "odd_job").Tire(10).Chance(75), Good().Money(50, 60, "odd_job").Tire(10).Up().Chance(25)),
                No()).At(7, 19)
        };
    }
}
