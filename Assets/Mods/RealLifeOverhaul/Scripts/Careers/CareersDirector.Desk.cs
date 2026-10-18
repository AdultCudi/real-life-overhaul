using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Helpers;
using RealLifeOverhaul.FitOut;
using RealLifeOverhaul.Tech;
using UI.Elements;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// A professional job is worked at the player's own desk, as the game's own job is worked at its till (go to the
    /// job, click the desk, press work, and the game's own panel to skip or fast-forward):
    ///
    ///   the desk       on coming into the employer's building, the player's desk there: one of the game's own stations
    ///                  of the kind the profession works at (DeskRules.Kinds), the seed's pick, kept free of the game's
    ///                  people; on a floor the mod fits out (a tech company's headquarters) one of its desks
    ///                  (FitOut/FitDesks); and where the building has no station that suits the job, a preset fitted out
    ///                  round what the game furnished it with (DeskRules.Preset: a row of office desks, a nurses' station, a
    ///                  teacher's desk, the council's service desks, a back office desk). The same desk every visit.
    ///   the click      the game's own "Click to work" over it (Careers/DeskGame); the player walks there and the game's
    ///                  work panel opens with the mod's job (Careers/DeskWork), its labels as the game's job has them
    ///   a home day     the computer at home, with the internet as before: its own panel has "Start the working day" (or
    ///                  "Work late") beside the game's Pass the time (Careers/ComputerButtons), and the day is sat at it as
    ///                  an online class is (Careers/ComputerSitting)
    ///   away           an hour into a working day not begun, the boss texts as the game's own employer does; a day missed
    ///                  is the mod's warning as before (three are the sack), now said in the boss's texts
    ///
    /// Never another player's business, never the player's own (Going Public: Mine and Foreign): nothing of either is
    /// touched. Nothing is saved: the desk is found again from the building, the seed and the job every visit.
    ///
    /// The cost: on the game's black screen at a door, one look at the building's list of items (and, where a preset is
    /// wanted, FitOut's measure and plan, as every fitted floor has); once a second a few comparisons; nothing per frame.
    /// </summary>
    public sealed partial class CareersDirector
    {
        private const float DeskWaitSeconds = 20f;   // real seconds a floor of the mod's is waited for to stand, with the player's desk on it
        private const int DeskSalt = 9221;           // the dice of a fitted floor's desk

        private EmployeeStationController _deskStation;
        private FitDesk _deskFit;
        private DeskPointer _deskPointer;
        private string _deskAt;                      // the employer's building the desk is being set up in, this visit (its place key)
        private bool _deskDone, _deskFailed, _deskSoon, _deskOff;
        private float _deskWaitUntil;
        private FitView _deskView;
        private readonly FitView.Request _deskRequest = new FitView.Request();
        private DeskWork _deskWork;
        private string _deskWhat, _deskWhatOf;       // what the desk is (a key) and the building it is in, once seen
        private int _awayTold = -1;
        private Action<Address> _onDeskEnter, _deskEnterSeen;
        private string _placeStreet, _placeKey;
        private int _placeNumber;
        private long _placeStamp;                    // the headquarters' stamp the job's place key was made for

        // ---------------------------------------------------------------- once a second

        // From the timer, while the working life runs.
        private void WorkDesks()
        {
            if (_deskOff)
            {
                return;
            }
            try
            {
                HeadquartersMoved();
                DeskWatch();
            }
            catch (Exception e)
            {
                if (!Guard.StandsDown(e, "the desks of professional jobs"))
                {
                    Logger?.Error(e);
                    Logger?.Warn("Careers: the desks of professional jobs are off for this session after the error above; the Career app starts a working day instead.");
                }
                _deskOff = true;
                DeskRelease("the desks stood down");
            }
        }

        private void DeskWatch()
        {
            if (_onDeskEnter == null)
            {
                _onDeskEnter = Guard.Wrap<Address>(_ => DeskEntered());   // (a fault here never reaches the game's own handlers: Core/Guard)
            }
            EventHold.Keep(ref GlobalEvents.onEnterBuilding, _onDeskEnter, ref _deskEnterSeen);
            DeskGame.Install();
            if (_deskWork != null)
            {
                if (_deskWork.Open && !DeskGame.Shows(_deskWork))
                {
                    _deskWork.Finish();   // the game's panel let it go without a word: the minutes so far are counted, and the player stands up
                }
                if (!_deskWork.Open)
                {
                    _deskWork = null;
                }
            }
            Job j = Work != null ? Work.Job : null;
            string here = j != null && DeskGame.Inside(out _, out BuildingRegistration reg) && CareersGame.InsideAt(j.Post.Street, j.Post.Number) ? DeskPlace(reg) : null;
            if (here != null && here != JobPlace(j))
            {
                here = null;   // (another company's floor of a tower: the desk is on the employer's)
            }
            if (_deskAt != null && here != _deskAt)
            {
                DeskRelease(here == null ? "the player left the building" : "another building");
            }
            if (here != null && !_deskSoon)
            {
                DeskSetUp(false);
            }
            if (_deskView != null && _deskView.Active)
            {
                _deskView.Tick(Time.time);
            }
        }

        // The player has just gone in: the desk is looked for as soon as the game has put the building together, under its
        // black screen (where a preset can be measured), not at the next second.
        private void DeskEntered()
        {
            if (!_deskSoon && !_deskOff && _workOn && isActiveAndEnabled)
            {
                _deskSoon = true;
                StartCoroutine(Guard.Routine(DeskSoon()));
            }
        }

        private IEnumerator DeskSoon()
        {
            yield return null;
            for (int frames = 0; frames < 180 && DeskGame.Entering(); frames++)
            {
                yield return null;
            }
            yield return null;   // (the game seats its people at the stations as it finishes going in)
            _deskSoon = false;
            try
            {
                DeskSetUp(true);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- the desk in the employer's building

        /// <summary>
        /// The job's building, as a place key; the same for the same address. A tech company's in one of the towers it
        /// shares with others (Tech/Sites) is its own floor's: the desk is there, never on another company's floor.
        /// </summary>
        private string JobPlace(Job j)
        {
            if (j == null)
            {
                return null;
            }
            long stamp = CareersBridges.HeadquartersStampNow();
            if (_placeKey == null || _placeStreet != j.Post.Street || _placeNumber != j.Post.Number || _placeStamp != stamp)
            {
                _placeStreet = j.Post.Street;
                _placeNumber = j.Post.Number;
                _placeStamp = stamp;
                _placeKey = HiringGame.Place(new Address(j.Post.Street, j.Post.Number));   // (a headquarters can move: the post follows it)
                if (OnTowerFloor(j.Post))
                {
                    _placeKey = HqTowers.FloorPlace(_placeKey, j.Post.Employer);
                }
            }
            return _placeKey;
        }

        // True for a post at a tech company's headquarters as it stands now: its floor of a tower.
        private static bool OnTowerFloor(Opening o)
        {
            if (o == null || o.Kind != (int)EmployerKind.TechFirm || CareersBridges.Headquarters == null)
            {
                return false;
            }
            try
            {
                return CareersBridges.Headquarters(o.Employer, out string street, out int number) && street == o.Street && number == o.Number;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        /// <summary>
        /// The place key of where the player is: the building's, and on a tower's floor that company's floor's. Asked for
        /// every second inside the job's building: made again only when the building or the floor changed (the building's
        /// key is TalentGame.Place's own, kept per building).
        /// </summary>
        private string DeskPlace(BuildingRegistration reg)
        {
            string place = TalentGame.Place(reg);
            string floor = CareersBridges.FloorHereNow();
            if (_deskPlace == null || !ReferenceEquals(place, _deskPlaceOf) || !string.Equals(floor, _deskPlaceFloor, StringComparison.Ordinal))
            {
                _deskPlaceOf = place;
                _deskPlaceFloor = floor;
                _deskPlace = HqTowers.FloorPlace(place, floor);
            }
            return _deskPlace;
        }

        private string _deskPlace, _deskPlaceOf, _deskPlaceFloor;   // the last place key of where the player is, the building's key and the floor's company it was made from

        private int DeskSeed(Job j) => (int)TechRandom.Hash(StudySeed, DeskSalt, DeskRules.JobKey(j));

        // Once a visit: the player's desk in the employer's building they are in. A measure for a preset only behind the black screen.
        private void DeskSetUp(bool atTheDoor)
        {
            Job j = Work != null ? Work.Job : null;
            if (j == null || _deskOff || !DeskGame.Clickable || !DeskGame.Inside(out BuildingManager manager, out BuildingRegistration reg)
                || !CareersGame.InsideAt(j.Post.Street, j.Post.Number))
            {
                return;
            }
            string place = DeskPlace(reg);
            if (place != JobPlace(j))
            {
                return;   // (another company's floor of the employer's tower: no desk of the player's here)
            }
            if (_deskAt == place && (_deskDone || _deskFailed))
            {
                return;
            }
            _deskAt = place;
            if (GoingPublic.Mine(reg) || GoingPublic.Foreign(reg))
            {
                DeskFail(place, "the building is a player's own business, and nothing of it is touched");
                return;
            }
            if (FitDesks.Current != null && FitDesks.Current.Place == place)
            {
                DeskMade(FitDesks.Current);
                return;
            }
            if (SpecialBuildings.Filed(j.Post.Street, j.Post.Number) || FitDesks.Building == place)   // (a building a feature is in: a reservation alone leaves the game's business and its desk as they are)
            {
                // A floor the mod fits out itself (a headquarters): its desk comes when it stands (DeskMade).
                if (_deskWaitUntil <= 0f)
                {
                    _deskWaitUntil = Time.realtimeSinceStartup + DeskWaitSeconds;
                }
                else if (Time.realtimeSinceStartup > _deskWaitUntil)
                {
                    DeskFail(place, "its floor stood no desk for the player within " + DeskWaitSeconds.ToString("0") + " seconds");
                }
                return;
            }
            Profession p = j.Def;
            EmployeeStationController station = DeskGame.Choose(manager, DeskRules.Kinds(p), StudySeed, DeskRules.JobKey(j), out StationKind kind, out int count);
            if (station != null)
            {
                DeskGame.Free(station);
                _deskStation = station;
                DeskGame.Target = station;
                _deskDone = true;
                _deskWhat = DeskRules.What(kind, DeskPreset.None);
                _deskWhatOf = place;
                Logger?.Info("Careers: the desk of " + p.Key + " at " + j.Post.Employer + " (" + reg.StreetName + " " + reg.StreetNumber + "): a station of the game's, " + kind
                             + ", " + count + " of that kind here, the " + StationName(station) + ".");
                if (Verbose)
                {
                    Logger?.Info("Careers check (desk): the same station from the same building, seed and job: " + StationName(DeskGame.Choose(manager, DeskRules.Kinds(p), StudySeed, DeskRules.JobKey(j), out _, out _))
                                 + " and " + StationName(station) + ": " + (ReferenceEquals(station, DeskGame.Choose(manager, DeskRules.Kinds(p), StudySeed, DeskRules.JobKey(j), out _, out _)) ? "PASS" : "FAIL"));
                }
                return;
            }
            DeskPreset preset = DeskRules.Preset(p);
            if (!atTheDoor || !SceneFinds.Hidden)
            {
                DeskFail(place, "no station of the game's for " + p.Key + " here, and a " + preset + " is fitted out only behind the game's black screen at the door (the next visit)");
                return;
            }
            if (!DeskFitOut(manager, reg, preset, j))
            {
                DeskFail(place, "no station of the game's for " + p.Key + " here, and the room could not be measured for a " + preset);
                return;
            }
            _deskWaitUntil = Time.realtimeSinceStartup + DeskWaitSeconds;
            Logger?.Info("Careers: the desk of " + p.Key + " at " + j.Post.Employer + " (" + reg.StreetName + " " + reg.StreetNumber + "): no station of the game's of "
                         + string.Join(", ", DeskRules.Kinds(p)) + " here; a " + preset + " is fitted out.");
        }

        private static string StationName(EmployeeStationController s) => s != null ? s.name + " at " + s.transform.position.ToString("F1", CultureInfo.InvariantCulture) : "none";

        private void DeskFail(string place, string why)
        {
            _deskAt = place;
            _deskFailed = true;
            Logger?.Info("Careers: no desk for the player this visit: " + why + ". The Career app starts the working day here instead.");
        }

        // A preset fitted out on the floor the building leaves free (FitOut's planner, round the game's furniture). False if the room could not be measured.
        private bool DeskFitOut(BuildingManager manager, BuildingRegistration reg, DeskPreset preset, Job j)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || !RoomMeasure.Door(manager, player.transform.position, player.transform.forward, out Vector3 door, out Vector3 inward))
            {
                return false;
            }
            RoomGrid grid = RoomMeasure.Measure(manager, door, true, out float floor);
            if (grid == null)
            {
                return false;
            }
            if (_deskView == null)
            {
                _deskView = new FitView(Logger);
                _deskView.Built += v => Logger?.Info("Careers: the desk's fit-out: " + v.Said + ".");
            }
            FitView.Request r = _deskRequest;
            r.Reset();
            r.Name = "the desk at " + reg.StreetName + " " + reg.StreetNumber;
            r.Kind = KindOf(preset);
            r.Seed = TechRandom.Of(TalentGame.Place(reg)) ^ DeskSeed(j);
            r.Building = manager;
            r.Grid = grid;
            r.Floor = floor;
            r.Door = door;
            r.Inward = inward;
            r.Look = false;                              // (the game's own walls and floors stay)
            r.People = preset == DeskPreset.OfficeRow;   // (colleagues at the other desks of an office's row)
            r.Site.ZoneLamps = false;
            return _deskView.Begin(r, this);
        }

        private static FitKind KindOf(DeskPreset preset)
        {
            switch (preset)
            {
                case DeskPreset.OfficeRow: return FitKind.OfficeRow;
                case DeskPreset.Ward: return FitKind.Ward;
                case DeskPreset.Classroom: return FitKind.Classroom;
                case DeskPreset.ServiceDesk: return FitKind.ServiceDesk;
                default: return FitKind.BackOffice;
            }
        }

        private static DeskPreset PresetOf(FitKind kind)
        {
            switch (kind)
            {
                case FitKind.Ward: return DeskPreset.Ward;
                case FitKind.Classroom: return DeskPreset.Classroom;
                case FitKind.ServiceDesk:
                case FitKind.CivicOffice: return DeskPreset.ServiceDesk;
                case FitKind.BackOffice:
                case FitKind.DataCentre: return DeskPreset.BackOffice;
                default: return DeskPreset.OfficeRow;   // (a headquarters' open-plan desks)
            }
        }

        /// <summary>The player's desk let go: the game's person back at its station if the player is still there, a preset taken down.</summary>
        private void DeskRelease(string why)
        {
            if (_deskWork != null && _deskWork.Open)
            {
                _deskWork.Close();
            }
            _deskWork = null;
            if (_deskStation != null)
            {
                try
                {
                    if (DeskGame.Inside(out _, out BuildingRegistration reg) && DeskPlace(reg) == _deskAt)
                    {
                        DeskGame.GiveBack(_deskStation);
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            if (_deskAt != null && Verbose)
            {
                Logger?.Info("Careers: the desk let go (" + why + ").");
            }
            _deskStation = null;
            _deskView?.End();
            _deskFit = null;
            if (_deskPointer != null)
            {
                UnityEngine.Object.Destroy(_deskPointer.gameObject);   // a desk of the game's own outlives the job: the next set-up must not find a pointer on it already
            }
            _deskPointer = null;
            DeskGame.Target = null;
            _deskAt = null;
            _deskDone = _deskFailed = false;
            _deskWaitUntil = 0f;
        }

        // ---------------------------------------------------------------- a fitted floor's desk (FitOut/FitDesks, wired in Scripts/Wiring)

        /// <summary>The seed of the player's desk on a fitted floor of a building (its place key), or -1 if no job of theirs is worked there.</summary>
        public static int DeskWanted(string place)
        {
            CareersDirector d = Instance;
            Job j = d != null && !d._deskOff && d._workOn && d.Work != null ? d.Work.Job : null;
            return j != null && place == d.JobPlace(j) ? d.DeskSeed(j) : -1;
        }

        /// <summary>The player's desk on a fitted floor stands: it is made a thing to click.</summary>
        public static void DeskMade(FitDesk desk)
        {
            CareersDirector d = Instance;
            Job j = d != null && d.Work != null ? d.Work.Job : null;
            if (j == null || desk == null || desk.Place != d.JobPlace(j))
            {
                return;
            }
            d._deskFit = desk;
            d._deskPointer = DeskPointer.Make(desk);
            DeskGame.Target = d._deskPointer;
            d._deskAt = desk.Place;
            d._deskDone = d._deskPointer != null;
            d._deskFailed = d._deskPointer == null;
            d._deskWhat = DeskRules.What(StationKind.None, PresetOf(desk.Kind));
            d._deskWhatOf = desk.Place;
            Logger?.Info("Careers: the desk of " + j.Def.Key + " at " + j.Post.Employer + ": a desk of the fitted floor (" + desk.Kind + ") at "
                         + desk.Desk.position.ToString("F1", CultureInfo.InvariantCulture)
                         + (d._deskPointer != null ? "." : ", but the game has no layer for a pointer: the Career app starts the working day."));
        }

        /// <summary>...and is taken down with its floor.</summary>
        public static void DeskGone(FitDesk desk)
        {
            CareersDirector d = Instance;
            if (d == null || desk == null || !ReferenceEquals(d._deskFit, desk))
            {
                return;
            }
            if (d._deskWork != null && d._deskWork.Open && ReferenceEquals(d._deskWork.Desk, desk))
            {
                d._deskWork.Finish();
            }
            d._deskFit = null;
            d._deskPointer = null;
            DeskGame.Target = null;
            d._deskDone = false;
        }

        // ---------------------------------------------------------------- the click and the panel

        /// <summary>The player clicked their desk (Careers/DeskGame): they walk to it, and the game's work panel opens there.</summary>
        internal void DeskClicked(EntityController e)
        {
            if (!_workOn || (_deskWork != null && _deskWork.Open) || Work == null || Work.Job == null)
            {
                return;
            }
            if (e is DeskPointer && _deskFit != null)
            {
                FitDesk desk = _deskFit;
                DeskGame.WalkTo(desk.Stand, () => DeskPanel(null, desk));
            }
            else if (_deskStation != null && ReferenceEquals(e, _deskStation))
            {
                EmployeeStationController station = _deskStation;
                DeskGame.WalkTo(station, () => DeskPanel(station, null));
            }
        }

        private readonly List<LabelInfo> _deskLabels = new List<LabelInfo>(6);

        // The game's work panel for the job at a desk: its labels, Cancel, Start working or Work late.
        private void DeskPanel(EmployeeStationController station, FitDesk desk)
        {
            Job j = Work != null ? Work.Job : null;
            if (j == null || Save == null || (_deskWork != null && _deskWork.Open))
            {
                return;
            }
            Profession p = j.Def;
            int day = Save.Day, minute = CareersGame.MinuteOfDay;
            DeskState state = DeskRules.State(j, day, minute, false, out int minutes, out bool late);
            bool canStart = DeskRules.Startable(state);
            _deskLabels.Clear();
            LabelInfo closed = DeskGame.Closed();
            if (closed != null)
            {
                _deskLabels.Add(closed);
            }
            string key = DeskRules.Label(state);
            if (key != null)
            {
                int colour = state == DeskState.NotYet || state == DeskState.StartsSoon ? 1 : 2;
                _deskLabels.Add(key.StartsWith("reallife_", StringComparison.Ordinal)
                    ? DeskGame.Label(state == DeskState.NotHired ? CareersText.T(key, "day", CareersText.Day(j.Hired)) : CareersText.T(key), colour)
                    : DeskGame.GameLabel(key, colour));
            }
            if (station != null && DeskGame.Taken(station))
            {
                _deskLabels.Add(DeskGame.Label(CareersText.T("reallife_desk_label_taken"), 1));
                canStart = false;
            }
            _deskLabels.Add(DeskGame.Label(CareersText.T("reallife_desk_label_hours", "from", CareersText.Hour(p.Hour), "to", CareersText.Hour((p.Hour + p.Hours) % 24)), 0));
            if (canStart)
            {
                _deskLabels.Add(DeskGame.Label(CareersText.T("reallife_work_skip_pay", "pay", CareersText.Rate(Prices.At(j.Pay))), 0));
                if (late && !(j.TodayDay == day && j.TodayMinutes > 0))
                {
                    _deskLabels.Add(DeskGame.Label(CareersText.T("reallife_desk_label_late"), 1));
                }
            }
            bool canLate = state == DeskState.Done && CanWorkLate() == null && !(station != null && DeskGame.Taken(station));
            string title = CareersText.Fill("reallife_work_skip_title", "title", CareersText.Title(j.Post.Prof, j.Post.Rank), "employer", CareersText.Employer(j.Post.Employer));
            var work = new DeskWork(this, station, desk, title, _deskLabels.ToArray(), canStart, canLate);
            if (DeskGame.Show(work))
            {
                _deskWork = work;
            }
            if (Verbose)
            {
                Logger?.Info("Careers check (desk panel): day " + day + " at " + CareersText.Hour(minute / 60) + ", " + state + " (" + minutes + " minutes left"
                             + (late ? ", late" : string.Empty) + "), Start " + (canStart ? "offered" : "not offered") + ", Work late " + (canLate ? "offered" : "not offered")
                             + ": " + (DeskRules.Startable(state) == (minutes > 0) ? "PASS" : "FAIL: the minutes do not agree with the state"));
            }
        }

        /// <summary>
        /// Start working (or Work late) pressed at the desk: the same checks as the day had in the Career app, but for the
        /// place (the player is at their desk), and the day's clock-in. Null and the sitting, or the reason key.
        /// </summary>
        internal string DeskBegin(bool overtime, out Sitting sitting)
        {
            sitting = null;
            Job j = Work != null ? Work.Job : null;
            if (j == null || Save == null)
            {
                return "reallife_work_why_no_job";
            }
            if (_sitting != null || (_skip != null && _skip.Open))
            {
                return "reallife_study_why_busy";
            }
            if (CareersGame.Energy < StudyCatalogue.TiredEnergy)
            {
                return "reallife_study_why_tired";
            }
            int day = Save.Day;
            if (overtime)
            {
                string why = CanWorkLate();
                if (why != null)
                {
                    return why;
                }
                sitting = LateSitting(j);
            }
            else
            {
                bool home = CareersGame.AtHome();
                DeskState state = DeskRules.State(j, day, CareersGame.MinuteOfDay, home, out int minutes, out bool late);
                if (!DeskRules.Startable(state))
                {
                    return DeskRules.Label(state) ?? "reallife_work_why_not_hours";
                }
                if (home && !CareersBridges.HomeOnline())
                {
                    return "reallife_study_why_no_internet";
                }
                ClockIn(j, day, late);
                sitting = DaySitting(j, minutes);
            }
            _sitting = sitting;
            Logger?.Info("Careers: " + (overtime ? "overtime" : "a working day") + " begun at the desk for " + j.Post.Employer + " on day " + day + " at " + CareersText.Hour(CareersGame.MinuteOfDay / 60)
                         + ", " + sitting.SkipMinutes + " minutes.");
            return null;
        }

        /// <summary>The first stint of a working day: its clock-in, and lateness counted once a day.</summary>
        private static void ClockIn(Job j, int day, bool late)
        {
            if (j.TodayDay == day)
            {
                return;
            }
            j.TodayDay = day;
            j.TodayMinutes = 0;
            j.ClockIn = CareersGame.MinuteOfDay;
            if (late)
            {
                j.Late++;
            }
        }

        /// <summary>The rest of a working day as one of the game's activities.</summary>
        private Sitting DaySitting(Job j, int minutes)
        {
            Profession p = j.Def;
            return new Sitting(this, SittingKind.Work, 0, minutes, CareersText.Fill("reallife_work_skip_title", "title", CareersText.Title(j.Post.Prof, j.Post.Rank),
                    "employer", CareersText.Employer(j.Post.Employer)), true,
                CareersText.T("reallife_work_skip_until", "time", CareersText.Hour((p.Hour + p.Hours) % 24)),
                CareersText.T("reallife_work_skip_pay", "pay", CareersText.Rate(Prices.At(j.Pay))));
        }

        /// <summary>A stint of overtime as one of the game's activities.</summary>
        private Sitting LateSitting(Job j)
        {
            return new Sitting(this, SittingKind.Overtime, 0, LifeRules.OvertimeMinutes, CareersText.Fill("reallife_work_skip_title", "title", CareersText.Title(j.Post.Prof, j.Post.Rank),
                "employer", CareersText.Employer(j.Post.Employer)), true, CareersText.T("reallife_life_skip_overtime"), CareersText.T("reallife_work_skip_pay", "pay", CareersText.Rate(Prices.At(j.Pay))));
        }

        // ---------------------------------------------------------------- a day from home, at the computer

        /// <summary>
        /// The words of the home computer's button for the job, beside the game's Pass the time (Careers/ComputerButtons),
        /// or null for none: Start the working day on a day worked from home, Work late once it is worked and overtime can
        /// be, or, greyed, the working hours before it can be begun and the desk's words after (too late, worked already).
        /// Whether it can be pressed, and whether for overtime.
        /// </summary>
        internal string ComputerWork(out bool can, out bool overtime)
        {
            can = false;
            overtime = false;
            Job j = Work != null ? Work.Job : null;
            if (j == null || Save == null || DeskFallback(j))
            {
                return null;   // (no job, or the Career app starts the day)
            }
            DeskState state = DeskRules.State(j, Save.Day, CareersGame.MinuteOfDay, true, out _, out _);
            switch (DeskRules.AtComputer(state, CanWorkLate() == null))
            {
                case HomeWork.Start:
                    can = true;
                    return CareersText.T("reallife_career_button_work");
                case HomeWork.Late:
                    can = true;
                    overtime = true;
                    return CareersText.T("reallife_life_button_late");
                case HomeWork.NotYet:
                    Profession p = j.Def;
                    return CareersText.T("reallife_desk_label_hours", "from", CareersText.Hour(p.Hour), "to", CareersText.Hour((p.Hour + p.Hours) % 24));
                case HomeWork.Why:
                    return CareersText.T(DeskRules.Label(state));
                default:
                    return null;
            }
        }

        /// <summary>
        /// The working day, or overtime, at a computer of the player's home, from its own button (Careers/ComputerButtons):
        /// the desk's checks and the day's clock-in (<see cref="DeskBegin"/>), then the player walks there and sits at it
        /// as for an online class (Careers/ComputerSitting). Null if under way; else the reason key.
        /// </summary>
        internal string WorkAtComputer(ComputerController computer, bool overtime)
        {
            Job j = Work != null ? Work.Job : null;
            if (j == null)
            {
                return "reallife_work_why_no_job";
            }
            if (!CareersGame.AtHome())
            {
                return "reallife_work_why_home_day";
            }
            if (computer == null || !DeskGame.Usable(computer) || computer.FindChair() == null)
            {
                return "reallife_career_desk_no_computer";
            }
            string busy = SeatBusy();
            if (busy != null)
            {
                return busy;
            }
            if (PlayerHelper.ItemInstanceInHands != null && !PlayerHelper.IsHoldingBag)
            {
                return "notification_need_empty_hands_to_interact";   // (the game's own words, as its computer says them)
            }
            int lateBefore = j.Late;
            string why = DeskBegin(overtime, out Sitting sitting);
            if (why != null)
            {
                return why;
            }
            why = StartAtComputer(computer, sitting, j.Post.Employer);
            if (why != null)
            {
                _sitting = null;   // (the game's panel was busy: nothing begun; the day's clock-in stands, as at a desk)
                return why;
            }
            if (j.Late > lateBefore)
            {
                Tell2(NotificationType.Warning, "reallife_desk_label_late", null, TabJob, null);   // (what the desk's panel says before Start)
            }
            return null;
        }

        // ---------------------------------------------------------------- away from work

        /// <summary>
        /// An hour into a working day not begun and the player not there: the boss texts, as the game's own employer does
        /// from its boss contact. The warning itself is the mod's, when the day is counted as missed (SettleDay).
        /// </summary>
        private void DeskHour(Job j, int day, int hour)
        {
            if (!DeskRules.Away(j, day, hour) || _awayTold == day || Working || Excused(day))
            {
                return;
            }
            Profession p = j.Def;
            bool there = ProRules.FromHome((WorkMode)j.Work, day) ? CareersGame.AtHome() : WorkGame.AtWork(p, j.Post);
            if (there)
            {
                return;
            }
            _awayTold = day;
            var data = Data("from", CareersText.Hour(p.Hour), "to", CareersText.Hour((p.Hour + p.Hours) % 24), "until", CareersText.Hour(Math.Min(23, (p.Hour * 60 + ProRules.ClockLateMinutes) / 60)));
            bool sent = DeskGame.BossText(CareersText.Employer(j.Post.Employer), j.Post.Street, j.Post.Number, "reallife_work_text_away", data);
            Logger?.Info("Careers: an hour into the working day at " + j.Post.Employer + " and the player is not there: the boss " + (sent ? "texted." : "could not text; nothing more is said."));
        }

        /// <summary>The boss's text for a working day missed, or the sack; a notification as before where the text cannot be sent.</summary>
        private void BossSays(Job j, string key, Dictionary<string, string> data, NotificationType type, string noticeKey, int tab)
        {
            if (!DeskGame.BossText(CareersText.Employer(j.Post.Employer), j.Post.Street, j.Post.Number, key, data))
            {
                Tell2(type, noticeKey, data, tab, null);
            }
        }

        // ---------------------------------------------------------------- for the Career app

        /// <summary>What and where the player's desk is: seen this visit or before, else what the job is to have.</summary>
        internal string DeskLine(Job j)
        {
            string what = _deskWhat != null && _deskWhatOf == JobPlace(j) ? _deskWhat : DeskRules.Expected(j.Def);
            return CareersText.Fill("reallife_career_desk_at", "what", CareersText.T(what), "employer", CareersText.Employer(j.Post.Employer), "address", WorkGame.Address(j.Post.Street, j.Post.Number));
        }

        /// <summary>What to do today, in a line; null on a day with nothing to do.</summary>
        internal string DeskToday(Job j)
        {
            int day = Save != null ? Save.Day : 0;
            if (!ProRules.WorkDay(j.Def, day) || day < j.Hired || j.SettledDay >= day)
            {
                return null;
            }
            return CareersText.T(ProRules.FromHome((WorkMode)j.Work, day) ? "reallife_career_desk_home" : "reallife_career_desk_how");
        }

        /// <summary>True where the Career app starts the working day itself (<see cref="DeskFallbackWhy"/>).</summary>
        internal bool DeskFallback(Job j) => DeskFallbackWhy(j) != null;

        /// <summary>
        /// Why the Career app starts the working day itself, as a key; null where it is started at the desk or the computer
        /// at home. A day from home in a home with no computer with its chair, or with the computer's own buttons not there
        /// (Careers/ComputerButtons); the game's list of clicks could not be reached; no desk could be had in the employer's
        /// building this visit; the employer's building is closed to the public now (the game lets nobody in, so the day
        /// is started by its door, as before).
        /// </summary>
        internal string DeskFallbackWhy(Job j)
        {
            if (j == null)
            {
                return "reallife_career_desk_none";
            }
            int day = Save != null ? Save.Day : 0;
            if (ProRules.FromHome((WorkMode)j.Work, day))
            {
                if (CareersGame.AtHome() && DeskGame.HomeComputer() == null)
                {
                    return "reallife_career_desk_no_computer";
                }
                return ComputerButtons.Failed ? "reallife_career_desk_none" : null;
            }
            if (_deskOff || !DeskGame.Clickable)
            {
                return "reallife_career_desk_none";
            }
            if (_deskFailed && _deskAt != null && _deskAt == JobPlace(j))
            {
                return "reallife_career_desk_none";
            }
            // (a building the mod holds, a headquarters, is walked into as it stands empty: its desk is inside)
            bool inside = CareersGame.InsideAt(j.Post.Street, j.Post.Number);
            return !inside && WorkGame.AtWork(j.Def, j.Post) && !SpecialBuildings.Filed(j.Post.Street, j.Post.Number) && !DeskGame.CanEnter(j.Post.Street, j.Post.Number)
                ? "reallife_career_desk_closed" : null;
        }

        /// <summary>The game's destination marker on the employer's building. Null if done, else a reason key.</summary>
        internal string DeskRoute()
        {
            Job j = Work != null ? Work.Job : null;
            if (j == null)
            {
                return "reallife_work_why_no_job";
            }
            return DeskGame.Route(j.Post.Street, j.Post.Number) ? null : "reallife_career_why_route";
        }

        // ---------------------------------------------------------------- the test

        /// <summary>The test button: where the desk is; inside the employer's building, its work panel opened at once (its labels as the hour has them).</summary>
        public void TestDesk()
        {
            if (!WorkReady())
            {
                return;
            }
            try
            {
                Job j = _study.Work.Job;
                if (j == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_work_test_desk_none", null, 8f);
                    return;
                }
                if (_deskAt == JobPlace(j))
                {
                    _deskFailed = false;   // (looked for again)
                }
                DeskSetUp(SceneFinds.Hidden);
                Logger?.Info("Careers test: the desk of " + j.Def.Key + " at " + j.Post.Employer + ", " + j.Post.Street + " " + j.Post.Number + ": "
                             + (_deskStation != null ? "a station of the game's, " + StationName(_deskStation) : _deskFit != null ? "a fitted desk (" + _deskFit.Kind + ")" : _deskFailed ? "none this visit" : "not here")
                             + "; expected " + DeskRules.Expected(j.Def) + ".");
                var data = Data("what", CareersText.T(_deskWhat != null && _deskWhatOf == JobPlace(j) ? _deskWhat : DeskRules.Expected(j.Def)), "employer", CareersText.Employer(j.Post.Employer),
                    "address", WorkGame.Address(j.Post.Street, j.Post.Number));
                if (_deskStation != null || _deskFit != null)
                {
                    DeskPanel(_deskStation, _deskFit);
                    Notifications.Show(NotificationType.Success, "reallife_work_test_desk_here", data, 8f);
                    return;
                }
                DeskRoute();
                Notifications.Show(NotificationType.Info, "reallife_work_test_desk_route", data, 10f);
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }
    }
}
