using static RealLifeOverhaul.Need;

namespace RealLifeOverhaul
{
    // Out and about: the sidewalk, the weather and getting around. Texts are in Locales\en.json.
    internal static partial class LifeEventCatalogue
    {
        private static LifeEvent[] Street() => new[]
        {
            E("st01", OnFoot, Good().Money(10, 60, "found_money")),
            C("st02", OnFoot, 25, "donation",
                Yes(Good().Feel(Mood.Generous).Chance(3), Info().Chance(1)), No()),
            E("st03", OnFoot, Bad().Pct(-0.05f, 20, 400, "pickpocket").Feel(Mood.Robbed)).Rich(200).At(21, 5).W(1.5f),
            E("st04", OnFoot, Bad().Hurt(12, 10)),
            C("st05", OnFoot, 5, "tip", Yes(Good().Up()), No()).At(10, 23),
            C("st06", OnFoot,
                Yes(Good().Money(20, 60, "reward").Feel(Mood.Helped), Good().Tire(4).Feel(Mood.Helped)),
                No(Info().Money(40, 120, "found_money").Down())),
            E("st07", OnFoot | Dry, Good().Up()).At(10, 19),
            C("st08", OnFoot, 20, "bet",
                Yes(Bad().Down().Chance(3), Good().Money(40, 40, "winnings").Feel(Mood.Lucky).Chance(1)), No()).At(10, 20),
            E("st09", OnFoot, Warn().Down()).At(6, 20),
            C("st10", OnFoot, Yes(Good().Tire(5).Feel(Mood.Helped)), No(Info())).At(9, 19),
            E("st11", OnFoot, Info().Tire(4)).At(7, 20),
            E("st12", OnFoot, Good().Feed(8)).At(7, 11),
            C("st13", OnFoot, 20, "donation", Yes(Good().Feel(Mood.Generous)), No()).At(9, 19),
            E("st14", OnFoot, Bad().Money(-60, -20, "repair").Down()).At(7, 19),
            E("st15", OnFoot, Good().Up()).At(17, 21),
            C("st16", OnFoot, 10, "purchase",
                Yes(Good().Up().Chance(7), Good().Money(30, 80, "sale").Feel(Mood.Lucky).Chance(3)), No()).At(9, 19),
            E("st17", OnFoot, Warn().Money(-50, -50, "fine")),
            E("st18", OnFoot, Info()).At(10, 21),
            C("st19", OnFoot, Yes(Good().Feed(5).Feel(Mood.Helped)), No(Info().Down())).At(8, 20),
            E("st20", OnFoot, Warn().Tire(5).Down()).At(22, 5),
            E("st21", OnFoot, Good().Money(5, 25, "found_money")),
            C("st22", OnFoot, 10, "bet",
                Yes(Bad().Tire(3).Chance(65), Good().Money(20, 20, "winnings").Up().Chance(35)), No()).At(8, 21),
            E("st23", OnFoot, Warn().Tire(6)).At(8, 19),
            E("st24", OnFoot | Dry, Good().Rest(5)).At(8, 19),
            C("st25", OnFoot, Yes(Good().Money(15, 15, "odd_job").Feed(3)), No()).At(9, 19),
            E("st26", OnFoot, Bad().Money(-40, -15, "repair")).At(8, 19),
            E("st27", OnFoot | Dry, Info().Up()).At(9, 18),
            C("st28", OnFoot, 30, "purchase",
                Yes(Bad().Down().Chance(6), Good().Money(60, 120, "sale").Chance(4)), No()),
            E("st29", OnFoot, Warn().Tire(3)),
            E("st30", OnFoot | Weekday, Info().Tire(4)).At(7, 10),
            E("st31", OnFoot, Good().Up()),
            C("st32", OnFoot,
                Yes(Good().Feel(Mood.Helped).Chance(8), Bad().Money(-150, -80, "repair").Down().Chance(2)), No()).At(20, 4),
            C("st33", OnFoot, Yes(Good().UpBig().Chance(7), Info().Down().Chance(3)), No()).At(10, 22),
            C("st34", OnFoot | Dry, 20, "purchase", Yes(Good().Up().Chance(6), Info().Chance(4)), No()).At(9, 20)
        };

        private static LifeEvent[] Weather() => new[]
        {
            E("wx01", OnFoot | Rain, Warn().Sick(24, 48)),
            C("wx02", OnFoot | Rain, 10, "purchase",
                Yes(Good().Up().Chance(6), Info().Chance(4)), No(Warn().Tire(5).Down())),
            E("wx03", OnFoot | Rain, Bad().Down()),
            E("wx04", Indoors | Rain, Good().Up()),
            E("wx05", Driving | Rain, Warn().Tire(4)),
            E("wx06", OnFoot | Rain, Good().Up()),
            E("wx07", OnFoot | Dry, Warn().Tire(6)).At(11, 16),
            E("wx08", OnFoot | Dry, Good().Rest(6).Up()).At(6, 10),
            E("wx09", Rain | HasHome, Bad().Money(-90, -30, "repair")),
            E("wx10", OnFoot | Rain, Warn().Money(-15, -5, "purchase")),
            E("wx11", OnFoot | Dry, Good().Up()).At(19, 23),
            C("wx12", OnFoot | Rain, Yes(Good().Up()), No(Warn().Tire(4))),
            E("wx13", Indoors | Rain, Good().Rest(6)).At(20, 6),
            E("wx14", OnFoot | Dry, Warn()),
            E("wx15", Driving | Dry, Warn().Tire(3)).At(16, 19),
            E("wx16", Dry, Good().Up()).At(5, 8),
            C("wx17", Indoors | Rain, Yes(Good().Rest(4).Chance(6), Info().Chance(4)), No())
        };

        private static LifeEvent[] Transport() => new[]
        {
            E("tr01", Driving, Bad().Money(-120, -60, "vehicle")),
            E("tr02", Driving, Good().Up()),
            E("tr03", Driving, Warn().Tire(4)),
            E("tr04", Driving, Bad().Money(-65, -65, "fine").Down()),
            E("tr05", Driving, Good().Money(5, 20, "found_money")),
            E("tr06", Driving, Good().Up()).At(22, 5),
            E("tr07", Driving, Warn().Down()),
            E("tr08", Driving, Warn().Money(-45, -20, "vehicle")),
            E("tr09", Driving, Good().Up()),
            E("tr10", HasVehicle | NotDriving, Bad().Money(-115, -55, "fine")),
            E("tr11", HasVehicle | NotDriving, Bad().Money(-80, -30, "vehicle").Down()),
            E("tr12", HasVehicle | NotDriving, Good().Up()),
            C("tr13", OnFoot, 10, "purchase", Yes(Good().Rest(4).Up()), No()).At(8, 20),
            E("tr14", NotDriving | Weekday, Warn().Tire(5).Down()).At(7, 10),
            E("tr15", OnFoot, Good().Money(5, 15, "found_money")),
            C("tr16", OnFoot,
                Yes(Good().Tire(8).Feel(Mood.Helped).Chance(7), Good().Tire(8).Money(20, 20, "tip").Feel(Mood.Helped).Chance(3)),
                No()).At(6, 22),
            E("tr17", Driving, Bad().Money(-180, -90, "vehicle")).Rich(300),
            E("tr18", Driving | Weekend, Info().Tire(3)).At(10, 19),
            E("tr19", OnFoot | NoVehicle, Warn().Tire(4)),
            C("tr20", HasVehicle, 15, "vehicle", Yes(Good().Up().Chance(8), Info().Chance(2)), No()).At(8, 19),
            E("tr21", Driving | Weekday, Warn().Tire(5)).At(16, 19),
            E("tr22", Driving, Good().Feel(Mood.Helped)),
            E("tr23", HasVehicle, Warn().Money(-95, -45, "vehicle")),
            C("tr24", HasVehicle | OnFoot,
                Yes(Good().Tire(3).Feel(Mood.Helped).Chance(7), Good().Money(20, 20, "tip").Feel(Mood.Helped).Chance(3)), No())
        };
    }
}
