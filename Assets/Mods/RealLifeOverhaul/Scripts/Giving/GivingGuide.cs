using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Giving
{
    /// <summary>
    /// Giving's entry in the feature list, its keys written out whole as the options checks read them. Its Guide page's numbers are facts filled from <see cref="GivingCatalogue"/>
    /// when the page opens.
    /// </summary>
    public static class GivingGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("giving", "reallife_meta_title_giving")
                .Group("reallife_giving_header")
                .About("reallife_guide_giving_about").Help("reallife_guide_giving")
                .Owns(Settings.GivingId)
                .Data(GivingStore.Key, GivingDirector.TestKey)
                .Test("reallife_giving_test_btn_gift", "reallife_giving_test_btn_month", "reallife_giving_test_btn_person", "reallife_giving_test_btn_lend", "reallife_giving_test_btn_ask",
                    "reallife_giving_test_btn_back")
                .Fact("giving_deduct", () => (GivingCatalogue.DeductionShare / 10f).ToString("0.#", Inv) + "%")
                .Fact("giving_loan_parts", () => GivingCatalogue.LoanParts.ToString(Inv))
                .Fact("giving_loan_most", () => Guide.Money(GivingCatalogue.LoanMost))
                .Fact("giving_gift_most", () => Guide.Money(GivingCatalogue.GiftsMost))
                .Fact("giving_loans_most", () => GivingCatalogue.LoansMost.ToString(Inv))
                .Fact("giving_ask_gap", () => GivingCatalogue.AskGapDays.ToString(Inv))
                .Fact("giving_standing_most", () => GivingCatalogue.StandingMost.ToString(Inv))
                .Fact("giving_misses", () => GivingCatalogue.LoanMisses.ToString(Inv))
                .Fact("giving_big", () => Guide.Money(GivingCatalogue.BigGift))
                .Fact("giving_window", () => GivingCatalogue.WindowDays.ToString(Inv))
                .ResetData(() => GivingDirector.Instance?.Forget());
        }
    }

    /// <summary>Giving's place on the options page.</summary>
    public static class GivingOptions
    {
        public static ModOptions AddGiving(this ModOptions options)
        {
            return options
                .AddHeader("reallife_giving_header")
                .AddToggle(Settings.GivingId, "reallife_giving_enabled", Settings.Giving, v => { Settings.Giving = v; })
                .AddButton("reallife_giving_test_btn_gift", () => GivingDirector.Instance?.TestGift())
                .AddButton("reallife_giving_test_btn_month", () => GivingDirector.Instance?.TestMonth())
                .AddButton("reallife_giving_test_btn_person", () => GivingDirector.Instance?.TestPerson(false))
                .AddButton("reallife_giving_test_btn_lend", () => GivingDirector.Instance?.TestPerson(true))
                .AddButton("reallife_giving_test_btn_ask", () => GivingDirector.Instance?.TestAsk())
                .AddButton("reallife_giving_test_btn_back", () => GivingDirector.Instance?.TestBack())
                .AddSplitter();
        }
    }
}
