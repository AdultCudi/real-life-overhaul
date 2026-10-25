using RealLifeOverhaul.Floors;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Schools
{
    /// <summary>
    /// The main school as the floors core holds it: three floors of the game's own school (structure D1, layout
    /// GarmentDistrictSchool, as Hudson City University's), the juniors on the first, the middle school on the second and the
    /// seniors on the third, open in the school's hours. The game's door and lift panels, its doors opened by the core. Asked
    /// by the core on its one-second timer or the game's events, never per frame.
    /// </summary>
    internal sealed class SchoolHost : FloorHost
    {
        public const string HostId = "schools_birchwood";
        public const string FeatureId = "schools";
        public const int OpenFrom = 7, OpenTo = 18;   // game hours the doors are open: the school run's first to the last club's end

        public SchoolHost() : base(HostId, FeatureId, "Schools check (main school door)", "Schools check (main school lift)")
        {
            Rank = FloorsRules.RankClaimed;
        }

        public override bool Open()
        {
            int hour = SchoolsGame.Hour;
            return hour >= OpenFrom && hour < OpenTo && SchoolRules.SchoolDay(SchoolsGame.Day);
        }

        public override bool ShowsDoor(bool open) => true;   // closed, its floors are offered greyed with its hours

        public override void DoorLine(bool open, out string left, out string right, out Color32 colour)
        {
            left = SchoolsText.Fill(SchoolsText.DoorLine, "from", OpenFrom.ToString("00") + ":00", "to", OpenTo.ToString("00") + ":00");
            right = SchoolsText.T(open ? "common_open" : "common_closed");
            colour = open ? WorldPanel.Green() : WorldPanel.Red();
        }

        public override string DoorWhat(bool open) => "at the door of Birchwood School (" + (open ? "open" : "closed") + ")";
    }

    /// <summary>
    /// The main school's classrooms: the game's school laid on each of its three floors, five classrooms a floor with their
    /// desks, chairs and the teacher's desk. The game's classroom doors belong to its business school's diplomas, so they are
    /// kept from the click once laid (SchoolsGame.QuietDoors).
    /// </summary>
    internal sealed class SchoolFloors : FloorPlace
    {
        private const string SchoolType = "ba:businesstype_school";
        private const string SchoolLayout = "GarmentDistrictSchool";
        private const string SchoolDoor = "ba:itemname_schooldoor";
        private static readonly string[] FloorKeys = { SchoolsText.FloorJuniors, SchoolsText.FloorMiddle, SchoolsText.FloorSeniors };
        private readonly object[] _args = new object[SchoolGrounds.MainStoreys];

        public SchoolFloors() : base("schools_classrooms", SchoolHost.FeatureId, SchoolsText.Campus)
        {
            Floors = SchoolGrounds.MainStoreys;
            Structure = new FloorStructure("ba:buildingsize_d", 1);
            Layout = new FloorLayout(SchoolType, SchoolLayout);
            LayLast = SchoolDoor;   // the classroom doors after the chairs they find, as the game lays its own school
            Check = "Schools check (main school way in)";
            LayCheck = "Schools check (main school floor)";
            LogName = "Birchwood School";
            StructureWords = "the school's structure";
        }

        /// <summary>Doors quietened on the floor last laid, for the census.</summary>
        public static int Quietened { get; private set; }

        public override bool Open() => true;

        public override void Button(int floor, int part, out string key, out object args, out bool marked)
        {
            int at = Mathf.Clamp(part - 1, 0, FloorKeys.Length - 1);
            key = FloorKeys[at];
            if (_args[at] == null)
            {
                _args[at] = new { floor = floor.ToString() };
            }
            args = _args[at];
            marked = false;
        }

        public override string LiftLine(int floor, int part) => SchoolsText.Fill(FloorKeys[Mathf.Clamp(part - 1, 0, FloorKeys.Length - 1)], "floor", floor.ToString());

        public override void Laid(FloorVisit visit, FloorLaid laid)
        {
            Quietened = SchoolsGame.QuietDoors();
            SchoolsDirector.Logger?.Info("Schools: floor " + visit.Floor + " of Birchwood School laid: " + laid.Made + " pieces of the game's school over " + laid.Frames
                                         + " frames, " + laid.Ms + " ms; " + Quietened + " classroom door(s) kept from the game's diplomas.");
        }
    }
}
