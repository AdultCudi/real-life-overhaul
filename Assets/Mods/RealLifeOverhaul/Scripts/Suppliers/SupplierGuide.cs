using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Suppliers
{
    /// <summary>
    /// Suppliers and contracts' entry in the feature list, its keys written out whole as the options checks read them. Its Guide page's numbers are facts filled from
    /// <see cref="SupplierCatalogue"/> when the page opens.
    /// </summary>
    public static class SupplierGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("suppliers", "reallife_meta_title_suppliers")
                .About("reallife_guide_suppliers_about").Help("reallife_guide_suppliers")
                .Owns(Settings.LivingSuppliersId)
                .Data(SupplierStore.Key, SupplierDirector.TestKey)
                .Test("reallife_supply_test_btn_show", "reallife_supply_test_btn_sign", "reallife_supply_test_btn_deliver", "reallife_supply_test_btn_late",
                    "reallife_supply_test_btn_short", "reallife_supply_test_btn_under", "reallife_supply_test_btn_rivals", "reallife_supply_test_btn_back")
                .Fact("supply_firms", () => SupplierCatalogue.Firms.Length.ToString(Inv))
                .Fact("supply_terms", () => SupplierCatalogue.TermWeeks[0].ToString(Inv) + ", " + SupplierCatalogue.TermWeeks[1].ToString(Inv) + " or "
                    + SupplierCatalogue.TermWeeks[2].ToString(Inv))
                .Fact("supply_discount_least", () => Share(SupplierCatalogue.TermDiscount[0]))
                .Fact("supply_discount_most", () => Share(SupplierCatalogue.TermDiscount[SupplierCatalogue.TermDiscount.Length - 1]))
                .Fact("supply_break", () => Share(SupplierCatalogue.BreakShare))
                .Fact("supply_failures", () => SupplierCatalogue.FailuresToLeave.ToString(Inv))
                .Fact("supply_short_credit", () => Share(SupplierCatalogue.ShortCredit))
                .Fact("supply_late_credit", () => Share(SupplierCatalogue.LateCreditADay))
                .Fact("supply_grace_most", () => SupplierCatalogue.GraceDays[0].ToString(Inv))
                .Fact("supply_grace_least", () => SupplierCatalogue.GraceDays[2].ToString(Inv))
                .Fact("supply_sour", () => SupplierCatalogue.SourDays.ToString(Inv))
                .Fact("supply_reopen", () => SupplierCatalogue.ReopenDays.ToString(Inv))
                .Fact("supply_contracts_most", () => SupplierCatalogue.ContractsMost.ToString(Inv))
                .Fact("supply_renew_days", () => SupplierCatalogue.RenewAheadDays.ToString(Inv))
                .ResetData(() => SupplierDirector.Instance?.Forget());
        }

        private static string Share(int thousandths) => (thousandths / 10f).ToString("0.#", Inv) + "%";
    }

    /// <summary>Suppliers and contracts' place on the options page: under the living economy's master switch.</summary>
    public static class SupplierOptions
    {
        public static ModOptions AddSuppliers(this ModOptions options)
        {
            return options
                .AddToggle(Settings.LivingSuppliersId, "reallife_supply_enabled", Settings.LivingSuppliers, v => { Settings.LivingSuppliers = v; })
                .AddButton("reallife_supply_test_btn_show", () => SupplierDirector.Instance?.TestShow())
                .AddButton("reallife_supply_test_btn_sign", () => SupplierDirector.Instance?.TestSign())
                .AddButton("reallife_supply_test_btn_deliver", () => SupplierDirector.Instance?.TestDeliver())
                .AddButton("reallife_supply_test_btn_late", () => SupplierDirector.Instance?.TestFail(true))
                .AddButton("reallife_supply_test_btn_short", () => SupplierDirector.Instance?.TestFail(false))
                .AddButton("reallife_supply_test_btn_under", () => SupplierDirector.Instance?.TestUnder())
                .AddButton("reallife_supply_test_btn_rivals", () => SupplierDirector.Instance?.TestRivals())
                .AddButton("reallife_supply_test_btn_back", () => SupplierDirector.Instance?.TestBack());
        }
    }
}
