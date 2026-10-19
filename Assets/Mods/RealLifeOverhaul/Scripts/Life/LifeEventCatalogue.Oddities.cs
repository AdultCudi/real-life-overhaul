using static RealLifeOverhaul.Need;

namespace RealLifeOverhaul
{
    // Luck, gadgets, animals and the rhythm of the week. Texts are in Locales\en.json.
    internal static partial class LifeEventCatalogue
    {
        private static LifeEvent[] Luck() => new[]
        {
            E("lk01", None, Good().Feel(Mood.Lucky)),
            E("lk02", None, Good().Money(100, 200, "winnings").Feel(Mood.Lucky)).W(0.5f),
            E("lk03", None, Warn().Down()),
            E("lk04", None, Info()),
            E("lk05", OnFoot, Good().Feel(Mood.Lucky)).At(6, 20),
            E("lk06", None, Warn().Down()),
            C("lk07", OnFoot, 10, "entertainment", Yes(Good().Feel(Mood.Lucky).Chance(1), Info().Chance(1)), No()).At(10, 23),
            E("lk08", None, Good().Up()),
            E("lk09", None, Info()),
            E("lk10", OnFoot, Good().Feed(10).Up()).At(11, 21),
            E("lk11", OnFoot, Warn()).At(7, 22),
            E("lk12", None, Bad().Money(-25, -10, "food")),
            C("lk13", OnFoot, 5, "bet", Yes(Good().Money(10, 10, "winnings").Up().Chance(4), Info().Chance(6)), No()).At(10, 21),
            E("lk14", Indoors, Good().Feed(6)),
            E("lk15", None, Info()).At(0, 5),
            E("lk16", OnFoot, Good().Feel(Mood.Lucky)),
            E("lk17", OnFoot, Bad().Money(-30, -10, "food").Down()),
            E("lk18", None, Info().Up()),
            C("lk19", OnFoot, 25, "purchase",
                Yes(Info().Chance(5), Good().Money(50, 140, "sale").Feel(Mood.Lucky).Chance(3), Good().Up().Chance(2)), No()).At(9, 19)
        };

        private static LifeEvent[] Tech() => new[]
        {
            E("tc01", None, Bad().Money(-180, -70, "repair")),
            E("tc02", None, Warn().Down()),
            E("tc03", OnFoot, Warn().Tire(3)),
            C("tc04", None, 10, "subscription", Yes(Info()), No(Good().Tire(3).Up())),
            E("tc05", None, Info()),
            E("tc06", None, Bad().Money(-70, -25, "repair")),
            E("tc07", HasHome, Warn().Down()),
            C("tc08", None,
                Yes(Bad().Money(-90, -40, "scam").Feel(Mood.Robbed).Chance(8), Good().Money(100, 100, "winnings").Feel(Mood.Lucky).Chance(2)),
                No(Good())).Rich(150),
            E("tc09", None, Good().Up()),
            E("tc10", None, Bad().Money(-120, -50, "repair")),
            E("tc11", None, Info()),
            E("tc12", None, Warn().Tire(4)),
            C("tc13", OnFoot,
                Yes(Good().Rest(4).Feel(Mood.Helped).Chance(6), Good().Money(20, 40, "reward").Feel(Mood.Helped).Chance(4)), No()).At(7, 21),
            E("tc14", None, Warn().Down()),
            E("tc15", Indoors, Info()),
            E("tc16", None, Warn().Money(-40, -15, "purchase")),
            C("tc17", None, Yes(Info().Tire(2).Chance(6), Good().Up().Chance(4)), No(Info()))
        };

        private static LifeEvent[] Animals() => new[]
        {
            C("pt01", OnFoot,
                Yes(Good().Tire(4).Money(30, 80, "reward").Feel(Mood.Helped).Chance(1), Good().Tire(4).Feel(Mood.Helped).Chance(1)),
                No(Info().Down())),
            E("pt02", OnFoot, Good().Up()).At(6, 22),
            E("pt03", Indoors, Good().Up()),
            E("pt04", OnFoot | Dry, Warn()).At(6, 20),
            C("pt05", HasHome,
                Yes(Good().Money(25, 25, "odd_job").Up().Chance(65), Warn().Money(25, 25, "odd_job").Tire(4).Chance(35)), No()),
            E("pt06", OnFoot, Warn().Tire(3)).At(6, 22),
            E("pt07", OnFoot, Good().Up()).At(5, 9),
            E("pt08", OnFoot, Info()).At(6, 20),
            C("pt09", OnFoot, 15, "donation", Yes(Good().Feel(Mood.Generous)), No(Good().Up())).At(9, 18),
            E("pt10", None, Warn().Tire(4)).At(4, 7),
            E("pt11", OnFoot | Rain, Info().Up()),
            E("pt12", Driving, Info()),
            C("pt13", OnFoot,
                Yes(Good().Money(40, 100, "reward").Feel(Mood.Helped).Chance(1), Good().Feel(Mood.Helped).Chance(1)),
                No(Info())).At(7, 20)
        };

        private static LifeEvent[] WeekMoments() => new[]
        {
            E("wk01", Monday, Warn().Tire(3)).At(6, 11),
            E("wk02", Friday, Good().Up()).At(16, 22),
            E("wk03", Sunday, Good().Rest(5)).At(7, 12),
            C("wk04", OnFoot | Weekend, 12, "entertainment", Yes(Good().Feed(25).UpBig()), No(Info().Tire(3))).At(10, 18),
            E("wk05", OnFoot | Weekend, Warn().Tire(3)).At(22, 4),
            E("wk06", OnFoot | Weekday, Info()).At(12, 14),
            E("wk07", Sunday, Warn().Down()).At(17, 23),
            C("wk08", OnFoot | Weekend, Yes(Good().Tire(3).UpBig().Chance(7), Info().Tire(3).Chance(3)), No()).At(11, 18),
            E("wk09", OnFoot | Weekday, Info().Tire(3)).At(17, 20),
            E("wk10", OnFoot | Saturday, Info().Up()).At(9, 13),
            E("wk11", OnFoot, Info()),
            C("wk12", OnFoot | Weekend, 15, "purchase",
                Yes(Good().Up().Chance(7), Good().Money(40, 90, "sale").Feel(Mood.Lucky).Chance(3)), No()).At(9, 17),
            E("wk13", Weekday, Info().Up()).At(5, 7),
            E("wk14", Newcomer, Info()),
            E("wk15", Veteran, Good().Up()),
            E("wk16", Veteran | OnFoot, Info()),
            C("wk17", OnFoot | Weekend, Yes(Good().Feed(20).Tire(3).UpBig()), No()).At(17, 23)
        };
    }
}
