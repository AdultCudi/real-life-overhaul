using static RealLifeOverhaul.Need;

namespace RealLifeOverhaul
{
    // Alex and Mom. Alex() and Mum() outcomes arrive as text messages from the phone contacts;
    // events that mention them but answer with a toast ask for the contact explicitly.
    internal static partial class LifeEventCatalogue
    {
        private static LifeEvent[] FriendTexts() => new[]
        {
            E("fr01", None, Alex()),
            E("fr02", None, Alex()),
            E("fr03", None, Alex()),
            E("fr04", None, Alex().Money(20, 60, "repaid").Up()),
            C("fr05", Friend, 50, "loan_friend",
                Yes(Alex().Money(50, 70, "repaid").Feel(Mood.Generous).Chance(6), Alex().Feel(Mood.Generous).Chance(4)),
                No(Alex())).Rich(100),
            E("fr06", HasBusiness, Alex().Up()),
            C("fr07", Friend, 30, "food", Yes(Good().Feed(25).Tire(4).Feel(Mood.Social)), No(Alex())).At(17, 23),
            E("fr08", None, Alex()),
            E("fr09", HasBusiness, Alex().Up()),
            C("fr10", Friend | Weekend, Yes(Good().Tire(12).Feed(20).Feel(Mood.Helped)), No(Alex())).At(9, 18),
            E("fr11", None, Alex()),
            E("fr12", None, Alex().Money(15, 40, "gift")),
            E("fr13", None, Alex()),
            C("fr14", Friend, 40, "entertainment",
                Yes(Good().UpBig().Chance(7), Info().Tire(4).Chance(3)), No()).At(15, 22).Rich(100),
            E("fr15", None, Alex()),
            E("fr16", None, Alex().Up()),
            C("fr17", Friend, Yes(Good().Tire(2).Feel(Mood.Social)), No(Alex())),
            E("fr18", None, Alex()),
            E("fr19", Sick, Alex().Up()),
            E("fr20", None, Alex()).At(23, 4),
            E("fr21", Rain, Alex()),
            E("fr22", Monday, Alex()),
            C("fr23", Friend | Saturday,
                Yes(Good().Tire(5).Feel(Mood.Social).Chance(6), Good().Tire(5).Money(30, 90, "sale").Feel(Mood.Social).Chance(4)),
                No(Alex())).At(8, 14),
            E("fr24", None, Alex().Money(10, 30, "repaid")),
            C("fr25", Friend, 20, "bet",
                Yes(Alex().Money(40, 40, "winnings").Up().Chance(1), Alex().Chance(1)), No()).At(12, 23)
        };

        private static LifeEvent[] FamilyTexts() => new[]
        {
            E("fm01", MomGiftDue, Mum().Money(50, 200, "family_gift")),
            E("fm02", None, Mum()),
            E("fm03", None, Mum()),
            E("fm04", HasBusiness, Mum().Up()),
            C("fm05", Mom, 15, "fee",
                Yes(Good().Feed(20).Feel(Mood.Family).Chance(7), Good().Feed(10).Money(20, 50, "family_gift").Feel(Mood.Family).Chance(3)),
                No(Mum())),
            E("fm06", None, Mum()),
            C("fm07", Mom, 60, "gift_given", Yes(Mum().Feel(Mood.Family)), No(Mum())).Rich(150),
            E("fm08", None, Mum()),
            E("fm09", None, Mum().Up()),
            E("fm10", Sick, Mum().Up()),
            E("fm11", None, Mum()).At(6, 9),
            C("fm12", Mom, Yes(Good().Tire(4).Feel(Mood.Family)), No(Mum())),
            E("fm13", Sunday, Mum()).At(16, 22),
            E("fm14", MomGiftDue, Mum().Money(20, 60, "family_gift")),
            E("fm15", None, Mum()),
            E("fm16", Rain, Mum()),
            C("fm17", Mom, 45, "gift_given", Yes(Mum().Feel(Mood.Family)), No(Good().Feel(Mood.Family))).Rich(150),
            E("fm18", None, Mum()),
            E("fm19", HasBusiness, Mum()),
            E("fm20", None, Mum()).At(22, 2),
            C("fm21", Mom | HasHome, Yes(Mum().Feel(Mood.Family)), No(Mum()))
        };
    }
}
