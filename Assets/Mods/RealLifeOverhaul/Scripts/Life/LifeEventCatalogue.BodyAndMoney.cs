using static RealLifeOverhaul.Need;

namespace RealLifeOverhaul
{
    // Health, food, and the steady drip of bills and paperwork. Texts are in Locales\en.json.
    internal static partial class LifeEventCatalogue
    {
        // Sick() always reads as a cold: LifeEvents.MakeSick follows up with its own cold message.
        private static LifeEvent[] Health() => new[]
        {
            E("hl01", Tired, Warn().Sick(24, 48)),
            E("hl02", OnFoot, Warn().Sick(24, 40)).At(7, 21),
            E("hl03", None, Bad().Money(-130, -60, "medical")),
            E("hl04", None, Good().Rest(8).Up()).At(5, 10),
            E("hl05", None, Warn().Tire(5)),
            C("hl06", OnFoot, Yes(Good().Up().Chance(7), Info().Chance(3)), No()).At(9, 18),
            E("hl07", Hungry, Warn().Tire(4)),
            E("hl08", Sick | OnFoot, Good().Up()),
            C("hl09", Sick | OnFoot, 20, "medical", Yes(Good().Cure().Chance(1), Info().Rest(5).Chance(1)), No()).At(8, 22),
            E("hl10", OnFoot, Bad().Hurt(8, 6)),
            E("hl11", None, Warn().Money(-55, -20, "medical")),
            E("hl12", Rested | OnFoot, Good().Up()),
            E("hl13", Indoors, Bad().Hurt(6, 4)),
            C("hl14", OnFoot | Dry, Yes(Good().Rest(6).Up().Chance(75), Warn().Tire(5).Chance(25)), No()).At(6, 10),
            E("hl15", Sick | Indoors, Good().Rest(5)),
            E("hl16", Unhappy | OnFoot, Good().Up()),
            E("hl17", Happy, Good().Rest(4)),
            E("hl18", Tired | NotDriving, Info()).At(13, 17),
            E("hl19", None, Bad().Money(-120, -50, "medical")),
            E("hl20", Fed, Good().Rest(4)),
            C("hl21", OnFoot | Healthy | Rested, Yes(Good().Tire(10).Feed(8).Feel(Mood.Helped)), No()).At(9, 18),
            C("hl22", OnFoot | Tired, Yes(Good().Rest(12).Chance(6), Warn().Rest(6).Down().Chance(4)), No())
        };

        private static LifeEvent[] MoneyMatters() => new[]
        {
            E("mn01", None, Warn().Money(-90, -30, "subscription")),
            E("mn02", None, Good().Money(20, 90, "refund")),
            E("mn03", None, Bad().Money(-120, -50, "fee")),
            E("mn04", None, Good().Money(30, 120, "refund")),
            C("mn05", Indoors, 10, "bet",
                Yes(Info().Chance(6), Good().Money(20, 50, "winnings").Chance(3),
                    Good().Money(200, 400, "winnings").Feel(Mood.Lucky).Chance(1)),
                No()),
            E("mn06", None, Warn().Money(-75, -25, "fee")),
            E("mn07", None, Good().Money(10, 50, "refund")),
            E("mn08", None, Bad().Pct(-0.02f, 60, 350, "unexpected_bill")).Rich(1000).Only(LifeAccounts.HasAccountant),
            C("mn09", None, 75, "scam", Yes(Bad().Feel(Mood.Robbed)), No(Good().Up())).Rich(100),
            E("mn10", None, Good().Money(15, 60, "refund")),
            E("mn11", None, Warn().Money(-110, -40, "fee")),
            E("mn12", None, Good().Pct(0.01f, 20, 250, "interest")).Rich(2000),
            E("mn13", None, Bad().Money(-60, -20, "fee")),
            C("mn14", None, Yes(Good().Tire(4).Money(15, 85, "refund").Chance(6), Info().Tire(4).Chance(4)), No()),
            E("mn15", None, Good().Money(20, 60, "found_money").Up()).Poor(300),
            E("mn16", None, Warn().Down()).Poor(200),
            E("mn17", None, Good().Up()).Rich(5000),
            E("mn18", None, Bad().Money(-160, -70, "unexpected_bill")),
            C("mn19", HasHome, 20, "donation",
                Yes(Good().Feel(Mood.Generous).Chance(75), Good().Feed(20).Feel(Mood.Lucky).Chance(25)), No()),
            E("mn20", None, Warn().Money(-85, -35, "fee")).Only(LifeAccounts.HasCard),
            E("mn21", None, Good().Money(25, 75, "sale")),
            E("mn22", None, Bad().Money(-240, -110, "unexpected_bill").DownBig()),
            E("mn23", NotDriving | Weekday, Info().Tire(5).Down()).At(9, 17),
            C("mn24", Indoors, Yes(Good().Feel(Mood.Helped)), No(Info().Money(30, 30, "found_money").Down())),
            E("mn25", None, Warn().Money(-45, -15, "fee")),
            E("mn26", None, Good().Money(10, 40, "refund")).Only(LifeAccounts.HasPower),
            C("mn27", None, Yes(Good().Money(40, 80, "found_money").Tire(4)), No(Good().Money(30, 65, "found_money")))
        };

        private static LifeEvent[] Food() => new[]
        {
            E("fd01", Hungry | OnFoot, Good().Feed(15)),
            C("fd02", OnFoot, 8, "food", Yes(Good().Feed(30).Up().Chance(8), Warn().Feed(20).Tire(6).Down().Chance(2)), No()).At(11, 23),
            E("fd03", None, Warn().Tire(8).Down()),
            E("fd04", None, Good().Rest(5)).At(7, 11),
            E("fd05", None, Warn().Down()).At(7, 11),
            C("fd06", OnFoot, 15, "food", Yes(Good().Feed(35).UpBig().Chance(6), Info().Feed(25).Chance(4)), No()).At(11, 15),
            E("fd07", Hungry, Warn().Down()),
            E("fd08", OnFoot | Saturday, Good().Feed(10).Up()).At(8, 14),
            E("fd09", None, Good().Feed(8)),
            C("fd10", HasHome, 20, "food",
                Yes(Good().Feed(35).Rest(3).Chance(8), Warn().Feed(20).Down().Chance(2)), No(Info().Feed(5))).At(18, 23),
            E("fd11", OnFoot, Info().Feed(-5)).At(5, 12),
            E("fd12", None, Warn().Money(-45, -20, "food")),
            E("fd13", OnFoot, Good().Feed(12).Up()).At(22, 4),
            C("fd14", OnFoot, Yes(Good().Money(20, 20, "winnings").Up().Chance(1), Bad().Tire(6).Feed(-5).Chance(1)), No()).At(10, 20),
            E("fd15", None, Info().Tire(3)).At(14, 17),
            E("fd16", Weekend, Info()).At(10, 14),
            E("fd17", OnFoot | Dry, Good().Feed(6).Up()).At(12, 20),
            E("fd18", None, Warn().Money(-20, -8, "food").Down()),
            C("fd19", OnFoot, Yes(Good().Feed(6).Up().Chance(7), Warn().Feed(-5).Down().Chance(3)), No()).At(9, 21)
        };
    }
}
