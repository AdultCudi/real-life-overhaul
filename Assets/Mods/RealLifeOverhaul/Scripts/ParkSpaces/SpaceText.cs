using System.Globalization;
using Localizor;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.ParkSpaces
{
    /// <summary>The words of the parking spaces that are built from the state: a space's number, a building's owner, how a space is held. Every other text is a locale key written whole where it is used.</summary>
    public static class SpaceText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string T(string key) => key.GetLocalization();

        /// <summary>A text with its names filled in: pairs of a name and what goes in its place.</summary>
        public static string Fill(string key, params string[] pairs)
        {
            string text = key.GetLocalization();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        public static string Space(int slot) => Fill("reallife_space_number", "number", slot.ToString(Inv));

        /// <summary>Who sells a car park's spaces: the rival that owns the building by name, the player, or the building's landlord.</summary>
        public static string Owner(SpacePark p)
        {
            return p.Owner == SpaceOwner.Player ? T("reallife_space_owner_you") : p.Owner == SpaceOwner.Rival && !string.IsNullOrEmpty(p.OwnerName) ? p.OwnerName
                : T("reallife_space_owner_landlord");
        }

        /// <summary>How a space is held: bought, rented until a day, or rented and given up.</summary>
        public static string Tenure(SpaceHolding h)
        {
            return !h.Rented ? T("reallife_space_tenure_owned")
                : Fill(h.Ending ? "reallife_space_tenure_ending" : "reallife_space_tenure_rented", "day", Economy.FinanceText.Day(h.PaidTo + 1));
        }
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
