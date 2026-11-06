using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using BigAmbitions.Items;
using BusinessLayoutSets;
using Entities;
using RealLifeOverhaul.FitOut;
using RealLifeOverhaul.Tech;
using UnityEngine;
using LayoutItem = BusinessLayoutSets.Item;

namespace RealLifeOverhaul.Floors
{
    /// <summary>
    /// Going in to a floor, and every way out. The building reads as the floor's structure from the moment a going in is asked
    /// (or the game's own click is seen) until the game has loaded it; then its own size and version go back, and with them its
    /// registration's dirt spots, which the game rewrote for the structure it loaded. On the floor the manager's registration
    /// may be the place's own (kept in no list of the save). Each step goes through FloorLoan, which says what to give back.
    /// </summary>
    public sealed partial class FloorsDirector
    {
        private const int LayChunks = 4;            // frames a layout is laid over: the game keeps the screen dark that long after it has entered
        private const float FitWaitMost = 10f;      // real seconds a fit-out waits for the game's going in to end

        private static Buildings.Building _asset;   // the building whose size and version are borrowed
        private static string _ownSize;             // ...its own, to be put back
        private static int _ownVersion;
        private static BuildingRegistration _dirtOf;   // its registration, and the dirt spots it had before the borrowed structure loaded
        private static List<DirtSpot> _keptDirt;
        private static FloorPlace _borrowFor;       // the place the structure is borrowed for (its self-check lines)
        private static int _askedDoor = -1;         // the door the going in asked for, for the visit
        private static string _askedHow;            // ...and how it was asked, in words for the log
        private static int _built;                  // furniture generations
        private static bool _toldSession;           // the Going Public line said this session
        private static readonly List<ItemController> Laid = new List<ItemController>(256);   // pieces the floors laid, while the player is in
        private static FitView _fit;
        private static readonly FitView.Request FitRequest = new FitView.Request();
        private static FloorVisit _fitFor;          // the visit the fit-out stands for

        /// <summary>
        /// The player goes in to a floor of a held building, from outside its door or from anywhere a test sends them; already
        /// inside it, the lift takes them. Null when under way, else why not, in words for the log.
        /// </summary>
        public static string Enter(FloorHost host, int floor, string how)
        {
            return Instance != null ? Instance.EnterNow(host, floor, how, false) : "the floors are not running";
        }

        private string EnterNow(FloorHost host, int floor, string how, bool reenter)
        {
            Book();
            if (_broken)
            {
                return "the floors stood down";
            }
            if (FloorsGame.Manager == null || host == null || !Hosts.Contains(host) || host.Building == null)
            {
                return "the building is not one of the floors'";
            }
            FloorPlace place = host.PlaceOn(floor);
            if (place == null)
            {
                return "it has no floor " + floor;
            }
            if (FloorsGame.Entering || Loan.Pending || Loan.Borrowed || (_lifting && !reenter))
            {
                return "the game is already going into a building";
            }
            if (FloorsGame.InVehicle)
            {
                return "the player is in or holding a vehicle";
            }
            if (!reenter && _visit != null && ReferenceEquals(_visit.Host, host))
            {
                return LiftNow(floor, how);   // inside it already: the lift
            }
            bool borrow = NeedsBorrow(host, place);
            int door = reenter && _visit != null && _visit.Door >= 0 ? _visit.Door : FloorsGame.NearestDoor(host.Building);
            FloorDo d = Loan.Ask(host.Key, floor, borrow, reenter, Time.realtimeSinceStartup);
            if ((d & FloorDo.Refused) != 0)
            {
                return "the game is already going into a building";
            }
            _askedDoor = door;
            _askedHow = how;
            if ((d & FloorDo.Borrow) != 0)
            {
                Borrow(host, place, "asked by " + how);
            }
            Logger?.Info("Floors: going into " + host.Name + " (" + FloorsGame.Address(host.Building) + "), floor " + floor + ", " + place.Said + " (" + how + ", door " + door
                         + (borrow ? ", on structure " + FloorsGame.Words(place.Structure.Size, place.Structure.Version) : string.Empty) + ").");
            try
            {
                FloorsGame.Enter(host.Building, door);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                if ((Loan.EnteringOver() & FloorDo.GiveBack) != 0)
                {
                    GiveBackNow("the game's going in failed");
                }
                return "the game's going in failed";
            }
            StartCoroutine(Guard.Routine(Settle()));
            return null;
        }

        // The game's own click on a building (its door, or the building) is going in: to the door's floor, on its structure.
        private void GameWayIn(FloorHost host)
        {
            int floor = FloorsRules.DoorFloor(host.Floors, host.DefaultFloor(false));
            FloorPlace place = host.PlaceOn(floor);
            if (place == null)
            {
                return;
            }
            bool borrow = NeedsBorrow(host, place);
            FloorDo d = Loan.Ask(host.Key, floor, borrow, false, Time.realtimeSinceStartup);
            if ((d & FloorDo.Refused) != 0)
            {
                return;
            }
            _askedDoor = FloorsGame.NearestDoor(host.Building);   // the game goes in by the door nearest the player (CityBuildingController.Interact)
            _askedHow = "the game's own door";
            if ((d & FloorDo.Borrow) != 0)
            {
                Borrow(host, place, "the game's own way in (a click on the building, or its door)");
            }
            host.GameWayIn(floor);
            Logger?.Info("Floors: the player is going into " + host.Name + " by the game's own door, to floor " + floor + ", " + place.Said + ".");
            StartCoroutine(Guard.Routine(Settle()));
        }

        // The building reads as the floor's structure until the game has loaded it; its own size, version and dirt spots kept.
        private static void Borrow(FloorHost host, FloorPlace place, string why)
        {
            Buildings.Building asset = host.Building.building;
            _asset = asset;
            _ownSize = asset.BuildingSize;
            _ownVersion = asset.BuildingVersion;
            _borrowFor = place;
            _dirtOf = FloorsGame.RegistrationOf(host.Building);
            _keptDirt = FloorsGame.DirtOf(_dirtOf);
            FloorsGame.ReadAs(asset, place.Structure.Size, place.Structure.Version);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info(place.Check + ": " + why + ": the building read as " + FloorsGame.Words(place.Structure.Size, place.Structure.Version)
                             + " until the game has loaded it (was " + _ownSize + " " + _ownVersion + "): PASS");
            }
        }

        // The building's own size and version back, and its registration's dirt spots as they were.
        private static void GiveBackNow(string why)
        {
            if (_asset != null)
            {
                FloorsGame.ReadAs(_asset, _ownSize, _ownVersion);
            }
            if (_dirtOf != null)
            {
                FloorsGame.PutDirt(_dirtOf, _keptDirt);
            }
            if (Meta.Dev.Verbose && _borrowFor != null)
            {
                bool back = _asset == null || (_asset.BuildingSize == _ownSize && _asset.BuildingVersion == _ownVersion);
                Logger?.Info(_borrowFor.Check + ": the building's own size put back (" + why + "): " + (back ? "PASS" : "FAIL (it reads " + _asset.BuildingSize + " " + _asset.BuildingVersion + ")"));
            }
            _asset = null;
            _dirtOf = null;
            _keptDirt = null;
            _borrowFor = null;
        }

        // The safety net: whatever happens to the going in, what was borrowed goes back when it is over, or after 15 s.
        private static IEnumerator Settle()
        {
            float until = Time.realtimeSinceStartup + SettleMost;
            yield return null;
            while ((Loan.Pending || Loan.Borrowed) && FloorsGame.Entering && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
            yield return null;
            if (!Loan.Pending && !Loan.Borrowed)
            {
                yield break;
            }
            bool late = FloorsGame.Entering;
            string place = _borrowFor != null ? _borrowFor.Said : "the floor";
            if ((Loan.EnteringOver() & FloorDo.GiveBack) != 0)
            {
                GiveBackNow(late ? "the game took longer than 15 s to go in" : "the game's going in ended without " + place);
            }
        }

        private static readonly int HotEntered = HotSpots.Slot("Floors: FloorsDirector.Entered");
        private static readonly int HotLeft = HotSpots.Slot("Floors: FloorsDirector.Left");

        // The game has entered a building (its onEnterBuilding, after it loaded the structure and put the player at the door).
        private void Entered(Address address)
        {
            using var hotSpot = HotSpots.Time(HotEntered);
            if (_broken)
            {
                return;
            }
            try
            {
                string key = FloorsGame.KeyOf(address);
                FloorHost host = HostByKey(key);
                _wrongTold = false;   // a new going in: found on no floor is acted on once again
                bool asked = host != null && string.Equals(Loan.PendingOf, key, StringComparison.Ordinal);
                FloorPlace place = asked ? host.PlaceOn(Loan.PendingFloor) : null;
                if (asked && place == null)
                {
                    // the floor asked for is gone while the game went in (its place released): as a way in that was not asked
                    if ((Loan.EnteringOver() & FloorDo.GiveBack) != 0)
                    {
                        GiveBackNow("the floor asked for is gone");
                    }
                    asked = false;
                }
                bool borrowed = asked && Loan.PendingBorrowed;
                // The place's own registration is made now, from the building's as the game has just loaded it (its dirt spots
                // the structure's), before that registration gets its own dirt spots back.
                BuildingRegistration shadow = place != null ? place.MakeShadow(FloorsGame.ManagerRegistration, borrowed) : null;
                string borrowedWords = _borrowFor != null ? _borrowFor.StructureWords : "the floor's structure";
                int floor = Loan.PendingFloor;
                FloorDo d = Loan.Entered(key, shadow != null);
                if ((d & FloorDo.Leave) != 0 && _visit != null)
                {
                    EndVisit("another building was entered", false);
                }
                if ((d & FloorDo.GiveBack) != 0)
                {
                    GiveBackNow(asked ? "the game has loaded " + borrowedWords : "another building was entered");
                }
                if ((d & FloorDo.Arrive) != 0 && place != null)
                {
                    Arrive(host, place, floor, borrowed, shadow, _askedDoor, false, _askedHow ?? "asked");
                }
                else if (host != null && _visit == null && !Loan.Pending && !Loan.Borrowed)
                {
                    Found(host, true);   // in by the game's own door: its floor at once where it stands on the building's own structure
                }
                _askedDoor = -1;
                _askedHow = null;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The game has left a building (its onExitBuilding; also its first step of going in again from a floor to another structure).
        private void Left(Address address)
        {
            using var hotSpot = HotSpots.Time(HotLeft);
            if (_broken)
            {
                return;
            }
            try
            {
                string key = FloorsGame.KeyOf(address);
                bool through = Loan.Reentering;
                FloorDo d = Loan.Exited(key);
                if ((d & FloorDo.Leave) != 0 && _visit != null)
                {
                    EndVisit(through ? "the lift, through the game's door" : "left the building", false);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The player on a floor: its place's registration, its furniture, its own parts.
        private void Arrive(FloorHost host, FloorPlace place, int floor, bool borrowed, BuildingRegistration shadow, int door, bool found, string how)
        {
            FloorsGame.PlayerAt(out Vector3 at, out Quaternion turn);
            if (found && FloorsGame.Spawn(door, out Vector3 spawn, out Quaternion spawnTurn))
            {
                at = spawn;   // found inside after a load: the lift stands where the game's door brings the player in
                turn = spawnTurn;
            }
            string own = OwnSize(host, out int ownVersion);
            var v = new FloorVisit
            {
                Host = host,
                Place = place,
                Floor = floor,
                Part = host.PartOf(floor),
                Spawn = at,
                Turn = turn,
                Door = door,
                Borrowed = borrowed,
                Key = host.Key,
                Address = FloorsGame.Address(host.Building),
                OwnSize = own,
                OwnVersion = ownVersion,
                OwnStructure = FloorsGame.Words(own, ownVersion),
                ShownSize = borrowed ? place.Structure.Size : own,
                ShownVersion = borrowed ? place.Structure.Version : ownVersion,
                Shadow = shadow,
                How = how,
                Built = ++_built
            };
            v.Structure = FloorsGame.Words(v.ShownSize, v.ShownVersion);
            _visit = v;
            _wrongTold = false;
            host.LiftPage = 0;
            if (shadow != null)
            {
                FloorsGame.ManagerRegistration = shadow;
            }
            Logger?.Info("Floors: the player is on floor " + floor + " of " + host.Name + " (" + v.Address + "), " + place.Said + ", on "
                         + (borrowed ? "structure " + v.Structure + " (the building's own is " + v.OwnStructure + ")" : "the building's own structure " + v.Structure)
                         + (shadow != null ? ", with a registration of the place's own" : string.Empty) + ".");
            if (GoingPublic.InSession && !_toldSession)
            {
                _toldSession = true;
                Logger?.Info("Floors: in this Going Public session another player is shown indoors by the building's address alone (\"" + FloorsGame.TagOf(FloorsGame.ManagerRegistration)
                             + "\" on every floor of it), so a player on another floor of " + v.Address + " is shown on this one.");
            }
            try
            {
                place.Arrive(v);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            Furnish(v);
        }

        // The player was found inside a held building on no floor: on its door's floor at once where that stands on the building's
        // own structure, else through the game's door again while it is open, else out to the street.
        private void Found(FloorHost host, bool justEntered)
        {
            if (_wrongTold)
            {
                return;
            }
            _wrongTold = true;
            int floor = FloorsRules.DoorFloor(host.Floors, host.DefaultFloor(!justEntered));
            FloorPlace place = host.PlaceOn(floor);
            if (place == null)
            {
                return;
            }
            bool open = host.Open() && place.Open();
            if (!NeedsBorrow(host, place))
            {
                if (!host.Open())
                {
                    return;   // standing empty no longer: no floor of the mod's there
                }
                BuildingRegistration shadow = place.MakeShadow(FloorsGame.ManagerRegistration, false);
                FloorDo d = Loan.Found(host.Key, floor, shadow != null);
                if ((d & FloorDo.Arrive) != 0)
                {
                    host.FoundInside(floor, false, true);
                    Arrive(host, place, floor, false, shadow, -1, !justEntered, justEntered ? "the game's own door" : "found inside, after a load or a way in not seen");
                }
                return;
            }
            host.FoundInside(floor, true, open);
            if (open)
            {
                StartCoroutine(Guard.Routine(Again(host, floor)));
            }
            else
            {
                FloorsGame.Exit(FloorsGame.NearestDoor(host.Building));
            }
        }

        private IEnumerator Again(FloorHost host, int floor)
        {
            yield return null;
            string why = EnterNow(host, floor, "a way back to the floors", false);
            if (why != null)
            {
                Logger?.Info("Floors: the way back to floor " + floor + " of " + host.Name + " was refused (" + why + "); the player is taken out to the street.");
                FloorsGame.Exit(0);
            }
        }

        // What the loan answered for a building gone or a place released, done.
        private static void Act(FloorDo d, string why)
        {
            FloorVisit v = _visit;
            bool going = FloorsGame.CityGoing;   // the city going: the manager goes with it, and nobody is walked out
            if ((d & FloorDo.GiveBack) != 0)
            {
                GiveBackNow(why);
            }
            if ((d & FloorDo.PutBackRegistration) != 0 && !going && v != null && v.Shadow != null && ReferenceEquals(FloorsGame.ManagerRegistration, v.Shadow))
            {
                FloorsGame.ManagerRegistration = FloorsGame.RegistrationOf(v.Host.Building);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info(v.Place.Check + ": the manager's registration is the building's own again (" + why + "): PASS");
                }
            }
            if ((d & FloorDo.Leave) != 0 && v != null)
            {
                EndVisit(why, (d & FloorDo.TakeOut) == 0);
            }
            if ((d & FloorDo.TakeOut) != 0 && v != null && !going)
            {
                Logger?.Info("Floors: the player is taken out of " + v.Host.Name + " to the street (" + why + "): floor " + v.Floor + " stood on structure " + v.Structure
                             + ", not the building's own.");
                FloorsGame.Exit(v.Door >= 0 ? v.Door : FloorsGame.NearestDoor(v.Host.Building));
            }
        }

        // The floor the player is on is no longer its place's (released, or its building let go).
        private static void Off(string why)
        {
            if (_visit != null)
            {
                Act(Loan.Gone(_visit.Key), why);
            }
        }

        // The visit over: its place's own parts and the floors' furniture go, and with Developer diagnostics on, the way out's check.
        private static void EndVisit(string why, bool stays)
        {
            FloorVisit v = _visit;
            _visit = null;
            if (v == null)
            {
                return;
            }
            v.Built = ++_built;   // a laying or a fit-out of it under way stops
            v.Stays = stays;
            try
            {
                v.Place.Leave(v);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            EndFurniture(stays);
            v.Host.LiftPanel.Hide(why);
            if (v.Borrowed && Meta.Dev.Verbose)
            {
                // Through the game's door to another floor, the building is already read as the next floor's structure for the
                // game's load: then its own is what is kept to be put back.
                bool lent = _asset != null && v.Host.Building != null && ReferenceEquals(_asset, v.Host.Building.building);
                string size = lent ? _ownSize : FloorsGame.SizeOf(v.Host.Building);
                int version = lent ? _ownVersion : FloorsGame.VersionOf(v.Host.Building);
                bool back = string.Equals(size, v.OwnSize, StringComparison.Ordinal) && version == v.OwnVersion;
                Logger?.Info("Floors check: floor " + v.Floor + " of " + v.Address + " on structure " + v.Structure + ", the building's own size and version put back on the way out"
                             + (lent ? " (kept, the building read as the next floor's structure until the game has loaded it)" : string.Empty) + ": "
                             + (back ? "PASS" : "FAIL (it reads " + size + " " + version + ", its own is " + v.OwnSize + " " + v.OwnVersion + ")"));
            }
            Logger?.Info("Floors: the player is off floor " + v.Floor + " of " + v.Host.Name + ", " + v.Place.Said + " (" + why + ").");
        }

        // Another place laid on the floor the player stands on, on the same structure (a holder's places chosen again): no warp.
        private void Swap(FloorVisit v, FloorPlace to, int floor)
        {
            v.Stays = true;
            try
            {
                v.Place.Leave(v);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            EndFurniture(true);
            Loan.Moved(floor);
            Logger?.Info("Floors: floor " + v.Floor + " of " + v.Host.Name + " is " + to.Said + "'s now; it is laid again where the player stands.");
            v.Place = to;
            v.Floor = floor;
            v.Part = v.Host.PartOf(floor);
            v.How = "its place changed under the player";
            v.Built = ++_built;
            try
            {
                to.Arrive(v);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            Furnish(v);
        }

        // ---------------------------------------------------------------- furniture

        // The floors' own furniture of a place: the game's layout, laid behind the dark screen, or FitOut's fit-out once in.
        private void Furnish(FloorVisit v)
        {
            if (v.Place.Layout != null)
            {
                v.Laying = true;
                StartCoroutine(Guard.Routine(Lay(v)));
            }
            else if (v.Place.Fit != null)
            {
                StartCoroutine(Guard.Routine(FitWhenIn(v)));
            }
        }

        // The place's layout laid in over the frames the game keeps the screen dark; the doors (LayLast) last.
        private static IEnumerator Lay(FloorVisit v)
        {
            int built = v.Built;
            var watch = Stopwatch.StartNew();
            FloorPlace place = v.Place;
            BusinessLayoutSet set = FloorsGame.LayoutSet(place.Layout, v.ShownSize, v.ShownVersion);
            if (set == null || set.Items == null)
            {
                v.Laying = false;
                string of = place.Layout.BusinessType ?? place.Layout.DesignBuilding;   // a business's layout, or a firm's design for a building type
                Logger?.Warn("Floors: " + place.Said + " has no layout of the game's for structure " + v.Structure + " (" + of + ", " + place.Layout.Name
                             + "); its floor stands bare.");
                yield break;
            }
            List<LayoutItem> order = FloorsGame.LayOrder(set, place.LayLast, place.Layout.Keeps);
            var made = new Dictionary<string, KeyValuePair<ItemController, List<AttachableChild>>>(order.Count);
            try
            {
                FloorsGame.ApplyLook(set);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            int per = (order.Count + LayChunks - 1) / LayChunks, at = 0, frames = 0, skipped = 0;
            while (at < order.Count)
            {
                yield return null;
                frames++;
                if (built != v.Built || !ReferenceEquals(_visit, v) || !FloorsGame.InsideBuilding)
                {
                    v.Laying = false;
                    yield break;
                }
                long before = watch.ElapsedMilliseconds;
                Transform container = FloorsGame.Container;
                for (int n = 0; n < per && at < order.Count; n++, at++)
                {
                    try
                    {
                        ItemController piece = FloorsGame.Place(order[at], container);
                        if (piece != null)
                        {
                            made[order[at].id] = new KeyValuePair<ItemController, List<AttachableChild>>(piece, order[at].stackedItems);
                            Laid.Add(piece);
                        }
                        else
                        {
                            skipped++;
                        }
                    }
                    catch (Exception e)
                    {
                        skipped++;
                        Logger?.Error(e);
                    }
                }
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info(place.LayCheck + ": frame " + frames + ", " + at + " of " + order.Count + " pieces, " + (watch.ElapsedMilliseconds - before) + " ms: PASS");
                }
            }
            try
            {
                FloorsGame.Attach(made);
                FloorsGame.Producers();   // the game reads the pieces again: doors find their chairs, stations their places
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            v.Laying = false;
            try
            {
                place.Laid(v, new FloorLaid { Made = made.Count, Skipped = skipped, Frames = frames, Ms = watch.ElapsedMilliseconds });
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // FitOut's fit-out of the place round the door, once the game's going in is over (the room is measured on its walkable floor).
        private static IEnumerator FitWhenIn(FloorVisit v)
        {
            int built = v.Built;
            float until = Time.realtimeSinceStartup + FitWaitMost;
            while (FloorsGame.Entering && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
            yield return null;
            if (built != v.Built || !ReferenceEquals(_visit, v))
            {
                yield break;
            }
            Fit(v);
        }

        private static void Fit(FloorVisit v)
        {
            FitOutDirector fitter = FitOutDirector.Instance;
            BuildingManager manager = FloorsGame.Manager;
            if (fitter == null || manager == null)
            {
                Logger?.Info("Floors: " + v.Place.Said + "'s floor is not fitted out (the fit-out is not running); it stands bare.");
                return;
            }
            if (_fit == null)
            {
                _fit = new FitView(Logger);
                _fit.Built += FitBuilt;
            }
            RoomGrid grid = null;
            float floor = 0f;
            Vector3 forward = v.Turn * Vector3.forward;
            if (RoomMeasure.Door(manager, v.Spawn, forward, out Vector3 door, out Vector3 inward))
            {
                grid = RoomMeasure.Measure(manager, door, true, out floor);
            }
            FitView.Request r = FitRequest;
            r.Reset();
            r.Name = v.Place.Said + " on floor " + v.Floor + " of " + v.Address;
            r.Kind = v.Place.Fit.Value;
            r.Seed = TechRandom.Of(v.Place.Id);
            r.Building = manager;
            r.Place = FloorsGame.DeskPlace(v.Place.Id);   // a desk there is the place's own
            r.Grid = grid;
            r.Floor = floor;
            r.Door = door;
            r.Inward = inward;
            r.Size = v.Borrowed ? v.ShownSize : null;   // the designs of the structure on show, not the building's own
            r.Version = v.ShownVersion;
            r.DesignKinds.AddRange(v.Place.Designs);
            _fitFor = grid != null && _fit.Begin(r, fitter) ? v : null;
            if (_fitFor == null)
            {
                Logger?.Info("Floors: " + v.Place.Said + "'s floor could not be measured from the door; it stands bare.");
            }
        }

        private static void FitBuilt(FitView view)
        {
            Logger?.Info("Floors: fitted out: " + view.Said + ".");
        }

        // Once a second on the floor: the fit-out's people, hidden for the game's placement mode.
        private static void FitTick(FloorVisit v)
        {
            if (_fit != null && _fit.Active && ReferenceEquals(_fitFor, v))
            {
                _fit.Hide(FloorsGame.Placing);
                _fit.Tick(Time.time);
            }
        }

        // The floors' furniture of the visit gone: the fit-out ended; the laid pieces destroyed while the player stays inside
        // (the game clears them itself when the player leaves).
        private static void EndFurniture(bool stays)
        {
            if (_fit != null && _fit.Active)
            {
                _fit.End();
            }
            _fitFor = null;
            if (stays && Laid.Count > 0)
            {
                FloorsGame.Unlay(Laid);
            }
            Laid.Clear();
        }

        // True once the visit's furniture stands (a lift keeps the screen dark until then).
        private static bool ReadyNow(FloorVisit v)
        {
            bool fitted = v.Place.Fit == null || _fit == null || !_fit.Active || !ReferenceEquals(_fitFor, v) || _fit.Ready;
            return !v.Laying && fitted && v.Place.Ready(v);
        }

        // With Developer diagnostics on, at the game's save: nothing of the floors' is in what it writes.
        private void OnSave()
        {
            if (!Meta.Dev.Verbose || _broken || (_visit == null && !Loan.Borrowed))
            {
                return;
            }
            FloorVisit v = _visit;
            bool inList = v != null && v.Shadow != null && FloorsGame.InSaveList(v.Shadow);
            bool own = v == null || (string.Equals(FloorsGame.SizeOf(v.Host.Building), v.OwnSize, StringComparison.Ordinal) && FloorsGame.VersionOf(v.Host.Building) == v.OwnVersion);
            Logger?.Info("Floors check (save): "
                         + (v != null ? "on floor " + v.Floor + " of " + v.Address + ": the building reads as its own " + v.OwnStructure + (own ? string.Empty : " NOT") : "going in")
                         + (Loan.Borrowed ? "; a building reads as another structure while the game loads it (the game saves no building's size)" : string.Empty)
                         + (v != null && v.Shadow != null ? "; the manager's registration is the place's own, " + (inList ? "IN" : "in no") + " list of the save" : string.Empty)
                         + ": " + (own && !inList ? "PASS" : "FAIL"));
        }
    }
}
