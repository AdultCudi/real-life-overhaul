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
    /// The desk a professional job is worked at: a station of the game's of the profession's kind, a desk of a floor the mod
    /// fits out, or a preset fitted out (DeskRules), the same every visit, never at a player's business; nothing saved. The
    /// cost: one look at the building's items behind the black screen at a door; once a second a few comparisons.
    /// </summary>
    public sealed partial class CareersDirector
    {
        private const float DeskWaitSeconds = 20f;   // real seconds a floor of the mod's is waited for to stand, with the player's desk on it
        private const int DeskSalt = 9221;           // salt of a fitted floor's desk seed

        private EmployeeStationController _deskStation;
        private FitDesk _deskFit;
        private DeskPointer _deskPointer;
        private string _deskAt;                      // the place key of the employer's building being set up this visit
        private bool _deskDone, _deskFailed, _deskSoon, _deskOff;
        private float _deskWaitUntil;
        private FitView _deskView;
        private readonly FitView.Request _deskRequest = new FitView.Request();
        private DeskWork _deskWork;
        private string _deskWhat, _deskWhatOf;       // what the desk is (a key) and the building it is in, once seen
        private Action<Address> _onDeskEnter, _deskEnterSeen;
        private string _placeStreet, _placeKey;
        private int _placeNumber;
        private long _placeStamp;                    // the headquarters' stamp the job's place key was made for

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
                _onDeskEnter = Guard.Wrap<Address>(_ => DeskEntered());
            }
            EventHold.Keep(ref GlobalEvents.onEnterBuilding, _onDeskEnter, ref _deskEnterSeen);
            DeskGame.Install();
            if (_deskWork != null)
            {
                if (_deskWork.Open && !DeskGame.Shows(_deskWork))
                {
                    _deskWork.Finish();   // the game's panel let it go: count the minutes so far and stand up
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
                here = null;   // another company's floor of a tower: the desk is on the employer's
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

        // Just gone in: the desk is looked for under the black screen once the game has built the building, not at the next second.
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
            yield return null;   // the game seats its people as it finishes going in
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

        /// <summary>The job's building as a place key; in a tower shared with other companies (Tech/Sites), the employer's floor's.</summary>
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
                _placeKey = HiringGame.Place(new Address(j.Post.Street, j.Post.Number));   // a headquarters can move; the post follows it
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

        /// <summary>The place key of where the player is (in a tower, that company's floor's); made again only when either changes.</summary>
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

        private string _deskPlace, _deskPlaceOf, _deskPlaceFloor;   // the last key of where the player is, and the building key and floor it was made from

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
                return;   // another company's floor of the employer's tower
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
            if (SpecialBuildings.Filed(j.Post.Street, j.Post.Number) || FitDesks.Building == place)   // a feature is in it; a reservation alone leaves the game's desk as it is
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
                    Logger?.Info("Careers check (desk): the same station from the same building, seed and job: "
                                 + StationName(DeskGame.Choose(manager, DeskRules.Kinds(p), StudySeed, DeskRules.JobKey(j), out _, out _)) + " and " + StationName(station) + ": "
                                 + (ReferenceEquals(station, DeskGame.Choose(manager, DeskRules.Kinds(p), StudySeed, DeskRules.JobKey(j), out _, out _)) ? "PASS" : "FAIL"));
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
            r.Look = false;                              // the game's own walls and floors stay
            r.People = preset == DeskPreset.OfficeRow;   // colleagues at the other desks of an office's row
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
                default: return DeskPreset.OfficeRow;   // a headquarters' open-plan desks
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
                UnityEngine.Object.Destroy(_deskPointer.gameObject);   // the game's own desk outlives the job: the next set-up must not find a pointer on it
            }
            _deskPointer = null;
            DeskGame.Target = null;
            _deskAt = null;
            _deskDone = _deskFailed = false;
            _deskWaitUntil = 0f;
        }

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

        /// <summary>The player's desk on a fitted floor is taken down with its floor.</summary>
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

        /// <summary>True where the Career app starts the working day itself (<see cref="DeskFallbackWhy"/>).</summary>
        internal bool DeskFallback(Job j) => DeskFallbackWhy(j) != null;

        /// <summary>Why the Career app starts the working day itself, as a key; null where the desk or the home computer does.</summary>
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
            // a building the mod holds (a headquarters) is walked into empty: its desk is inside
            bool inside = CareersGame.InsideAt(j.Post.Street, j.Post.Number);
            return !inside && WorkGame.AtWork(j.Def, j.Post) && !SpecialBuildings.Filed(j.Post.Street, j.Post.Number) && !DeskGame.CanEnter(j.Post.Street, j.Post.Number)
                ? "reallife_career_desk_closed" : null;
        }
    }
}
