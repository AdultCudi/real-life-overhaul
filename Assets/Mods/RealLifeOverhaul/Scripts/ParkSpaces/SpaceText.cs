using System.Globalization;
using Localizor;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.ParkSpaces
{
    /// <summary>
    /// The words of the parking spaces in the game's language: <see cref="Ui.SpaceWords"/>, which the kit's page and the checks use
    /// too, through the game's localisation. Every other text is a locale key written whole where it is used.
    /// </summary>
    public static class SpaceText
    {
        /// <summary>The game's own text of a key; the English is only what the key stands for.</summary>
        public static readonly Ui.UiWords Words = (key, english) => key.GetLocalization();

        public static string T(string key) => key.GetLocalization();

        /// <summary>A text with its names filled in: pairs of a name and what goes in its place.</summary>
        public static string Fill(string key, params string[] pairs) => Ui.SpaceWords.Fill(key.GetLocalization(), pairs);

        public static string Space(int slot) => Ui.SpaceWords.Space(Words, slot);

        public static string Owner(SpacePark p) => Ui.SpaceWords.Owner(Words, p);

        public static string Tenure(SpaceHolding h) => Ui.SpaceWords.Tenure(Words, h, Economy.FinanceText.Day);

        public static string RowState(SpacePark p, int player, int free) => Ui.SpaceWords.RowState(Words, p, player, free);
    }

    /// <summary>
    /// The parking spaces' entry in the feature list, its keys written out whole as the options checks read them. Its Guide page's
    /// numbers are facts filled from <see cref="SpaceCatalogue"/> when the page opens.
    /// </summary>
    public static class SpaceGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("parking_spaces", "reallife_meta_title_parking_spaces")
                .Group("reallife_estate_header")
                .About("reallife_guide_parking_spaces_about").Help("reallife_guide_parking_spaces")
                .Owns(Settings.ParkingSpacesId)
                .Data(SpaceStore.Key)
                .Test("reallife_space_test_btn_buy", "reallife_space_test_btn_day", "reallife_space_test_btn_rival")
                .Fact("space_fee", () => Guide.Money(SpaceCatalogue.HourFee))
                .Fact("space_rent_days", () => SpaceCatalogue.RentDays.ToString(Inv))
                .Fact("space_rent", () => Guide.Money(SpaceRules.RentMonth(1000)))
                .Fact("space_back_fee", () => (SpaceCatalogue.BackFee / 10f).ToString("0.#", Inv) + "%")
                .Fact("space_held_most", () => SpaceCatalogue.HeldMost.ToString(Inv))
                .Fact("space_park_most", () => SpaceCatalogue.HeldPerPark.ToString(Inv))
                .Fact("space_gone_days", () => SpaceCatalogue.GoneDays.ToString(Inv))
                .ResetData(() => SpaceDirector.Instance?.Forget());
        }
    }
}
