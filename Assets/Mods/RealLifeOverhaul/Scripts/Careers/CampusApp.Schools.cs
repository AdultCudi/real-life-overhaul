using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The Campus app's Schools tab: the city's schools and Birchwood School's page, written by the schools' own page
    /// (Scripts/Schools) through <see cref="CareersBridges.SchoolsTab"/>, so the app holds no rule of theirs.
    /// </summary>
    public sealed partial class CampusApp
    {
        public const int TabSchools = 5;

        private TableSpec WriteSchools(InfoBox box)
        {
            SchoolsTabWriter writer = CareersBridges.SchoolsTab;
            TableSpec table = writer != null ? writer(box, Row, _picked, Do) : null;
            if (table != null)
            {
                return table;
            }
            _picked = null;
            box.Title(CareersText.T("reallife_schools_box_title"));
            box.Note(CareersText.T("reallife_schools_box_off"));
            return CampusColumns.Off;
        }
    }
}
