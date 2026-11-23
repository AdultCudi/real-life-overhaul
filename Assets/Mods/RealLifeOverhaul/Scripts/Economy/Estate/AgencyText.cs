using System.Globalization;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The agency book's words: an owner, a rival agency, a block and why one was lost, in the game's language. Written on a click or a morning's notice, never per frame.</summary>
    public static class AgencyText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly string[] CityKeys =
        {
            "reallife_estate_owner_0", "reallife_estate_owner_1", "reallife_estate_owner_2", "reallife_estate_owner_3", "reallife_estate_owner_4", "reallife_estate_owner_5"
        };

        private static readonly string[] StandardKeys = { "reallife_estate_standard_fair", "reallife_estate_standard_good", "reallife_estate_standard_strict" };

        /// <summary>An owner of a block: a rival of the game by the game's own name, or a landlord of the city by a name of the mod's.</summary>
        public static string Owner(string owner)
        {
            int city = AgencyBookRules.CityNumber(owner);
            if (city >= 0)
            {
                return EstateText.T(CityKeys[city]);
            }
            string name = EstateGame.RivalName(owner);
            return string.IsNullOrEmpty(name) ? EstateText.T("reallife_estate_owner_unknown") : name;
        }

        /// <summary>A rival's agency, named after the rival.</summary>
        public static string Agency(string rival)
        {
            return string.IsNullOrEmpty(rival) ? EstateText.T("reallife_estate_agency_unknown") : EstateText.T("reallife_estate_rival_agency", "name", Owner(rival));
        }

        /// <summary>A block as a row names it: its flats and its district.</summary>
        public static string Block(ManagedBlock b)
        {
            return EstateText.T("reallife_estate_row_block", "units", b.Units.ToString(Inv), "district", EstateText.District(b.District));
        }

        /// <summary>The satisfaction an owner expects, in a word.</summary>
        public static string Standard(int level)
        {
            int at = 0;
            for (int i = 0; i < AgencyBookCatalogue.Standards.Length; i++)
            {
                at = level >= AgencyBookCatalogue.Standards[i] ? i : at;
            }
            return EstateText.T(StandardKeys[at]);
        }

        /// <summary>Why a block was lost, in a sentence that stands after a colon or alone.</summary>
        public static string Why(ManagedBlock b)
        {
            switch ((BlockWhy)b.Why)
            {
                case BlockWhy.Rival: return EstateText.T("reallife_estate_block_why_rival", "agency", Agency(b.Rival));
                case BlockWhy.Nobody: return EstateText.T("reallife_estate_block_why_nobody");
                case BlockWhy.Standard: return EstateText.T("reallife_estate_block_why_standard");
                case BlockWhy.Inspection: return EstateText.T("reallife_estate_block_why_inspection");
                case BlockWhy.Resigned: return EstateText.T("reallife_estate_block_why_resigned");
                default: return string.Empty;
            }
        }

        /// <summary>A block's stage for the table's stage column (EstateText.StageKeys 0 to 2 are the instructions').</summary>
        public static int StageCode(ManagedBlock b)
        {
            return b.Stage == (int)BlockStage.Offered ? (int)InstructionStage.Offered : b.Stage == (int)BlockStage.Held ? StageManaged : StageLost;
        }

        public const int StageManaged = 3, StageLost = 4;
    }
}
