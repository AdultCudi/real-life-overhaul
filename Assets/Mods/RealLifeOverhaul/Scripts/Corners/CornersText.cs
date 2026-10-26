using Extensions;
using Localizor;

namespace RealLifeOverhaul.Corners
{
    /// <summary>The locale keys of the corner buildings, written out whole so the text checks find them.</summary>
    public static class CornersText
    {
        public const string Lease = "reallife_corner_sign_lease";             // an empty unit's boards while it is to let

        public const string TestStand = "reallife_corner_test_stand";         // the test buttons (Options > Tests, construction)
        public const string TestCheck = "reallife_corner_test_check";
        public const string NoCorner = "reallife_corner_test_no_corner";      // the notices the tests show
        public const string NoneStand = "reallife_corner_test_none";
        public const string ProjectRow = "reallife_corner_project_row";       // the Construction app's project page: the corner building it is

        // The seven corner buildings' names in build mode's list of designs (CornerRules.Models' NameKey).
        public const string NameBodega = "reallife_corner_name_bodega";
        public const string NameDiner = "reallife_corner_name_diner";
        public const string NameBank = "reallife_corner_name_bank";
        public const string NameDeco = "reallife_corner_name_deco";
        public const string NameGlass = "reallife_corner_name_glass";
        public const string NameLoft = "reallife_corner_name_loft";
        public const string NamePrewar = "reallife_corner_name_prewar";

        /// <summary>A text in the player's language.</summary>
        public static string T(string key) => key.GetLocalization();
    }
}
