using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Household
{
    /// <summary>
    /// Household staff's entry in the feature list, its keys written out whole as the options checks read them. Its Guide
    /// page's numbers are facts filled from <see cref="HouseholdRules"/> when the page opens.
    /// </summary>
    public static class HouseholdGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("home_household", "reallife_meta_title_household")
                .Group("reallife_household_header")
                .About("reallife_guide_household_about").Help("reallife_guide_household")
                .Owns(Settings.HouseholdStaffId)
                .Data(HouseholdStore.Key, HouseholdDirector.TestKey)
                .Contact(HouseholdPhone.ContactId)
                .Test("reallife_household_test_hire", "reallife_household_test_day", "reallife_household_test_show", "reallife_household_test_mechanic",
                    "reallife_household_test_hamptons", "reallife_household_test_homes", "reallife_household_test_back")
                .Fact("hh_cleaner_from", () => Hour(StaffKind.Cleaner, false))
                .Fact("hh_cleaner_to", () => Hour(StaffKind.Cleaner, true))
                .Fact("hh_cook_from", () => Hour(StaffKind.Cook, false))
                .Fact("hh_cook_to", () => Hour(StaffKind.Cook, true))
                .Fact("hh_butler_from", () => Hour(StaffKind.Butler, false))
                .Fact("hh_butler_to", () => Hour(StaffKind.Butler, true))
                .Fact("hh_mechanic_at", () => Hour(StaffKind.Mechanic, false))
                .Fact("hh_cook_share", () => Share(StaffKind.Cook))
                .Fact("hh_butler_share", () => Share(StaffKind.Butler))
                .Fact("hh_callout", () => Guide.Money(HouseholdRules.CallOut))
                .Fact("hh_wash", () => Guide.Money(HouseholdRules.WashFee))
                .Fact("hh_shopping", () => Guide.Money(HouseholdRules.GroceriesPerPortion))
                .Fact("hh_portions", () => HouseholdRules.LeftoversMost.ToString(Inv))
                .Fact("hh_reach", () => HouseholdRules.HomeReach.ToString("0", Inv))
                .Fact("hh_quit", () => HouseholdRules.QuitAfter.ToString(Inv))
                .Fact("hh_note", () => HouseholdText.Hour(HouseholdRules.NoteHour))
                .Cleanup(() => HouseholdDirector.Instance?.StopAll())
                .ResetData(() => HouseholdDirector.Instance?.Forget())
                .Repair(HouseholdPhone.Setup);
        }

        private static string Hour(StaffKind kind, bool end)
        {
            StaffRole role = HouseholdRules.Role(kind);
            return HouseholdText.Hour(end ? role.From + role.Hours : role.From);
        }

        // A role's wage beside the cleaner's, as "a quarter more".
        private static string Share(StaffKind kind)
        {
            return (HouseholdRules.Role(kind).ShareBp / 100 - 100).ToString(Inv) + "%";
        }
    }

    /// <summary>Household staff's place on the options page.</summary>
    public static class HouseholdOptions
    {
        public static ModOptions AddHousehold(this ModOptions options)
        {
            return options
                .AddHeader("reallife_household_header")
                .AddToggle(Settings.HouseholdStaffId, "reallife_household_enabled", Settings.HouseholdStaff,
                    v => { Settings.HouseholdStaff = v; if (!v) { HouseholdDirector.Instance?.StopAll(); } })
                .AddButton("reallife_household_test_hire", () => HouseholdDirector.Instance?.TestHire())
                .AddButton("reallife_household_test_day", () => HouseholdDirector.Instance?.TestDay())
                .AddButton("reallife_household_test_show", () => HouseholdDirector.Instance?.TestShow())
                .AddButton("reallife_household_test_mechanic", () => HouseholdDirector.Instance?.TestMechanic())
                .AddButton("reallife_household_test_hamptons", () => HouseholdDirector.Instance?.TestHamptons())
                .AddButton("reallife_household_test_homes", () => HouseholdDirector.Instance?.TestHomes())
                .AddButton("reallife_household_test_back", () => HouseholdDirector.Instance?.TestBack())
                .AddSplitter();
        }
    }
}
