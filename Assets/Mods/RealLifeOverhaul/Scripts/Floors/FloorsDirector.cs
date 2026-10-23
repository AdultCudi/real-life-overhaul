using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.Floors
{
    /// <summary>
    /// The mod's places on floors of the buildings it holds, so a new place takes a floor and not a building of the city's.
    /// Holders hand over their buildings with their own places on them (the university's floors of classrooms, the tech
    /// companies' towers with a company a floor: <see cref="Hold"/>); a feature asks a spare floor for its place
    /// (<see cref="Ask"/>): the towers' first, then the university's building, then one building taken for many places, never
    /// one a place (FloorsRules.Assign). At a building's door and at its lift the game's own panel offers the floors, named after
    /// their places, three to a page with "more floors" beyond four. A floor may stand on another of the game's structures than
    /// the building's own: the building reads as that size and version while the game loads it, and all that is borrowed goes
    /// back on every way out (FloorLoan). A floor's furniture is the game's own layout, FitOut's fit-out, or the place's own.
    /// Which place is on which floor is worked out from the buildings and places alone, the same on every machine; nothing is saved.
    /// The floors follow their hosts: a building or place is put away while the feature that holds it stands down in another city
    /// or is switched off (Core/CityCheck; Follow), and no building a DLC keeps is ever held or taken (Core/GameDlc).
    /// COST: no Update. A one-second timer; while the player is outdoors within twelve metres of an open door whose floor stands
    /// on another structure, one flag a frame (the game's own click on the building is seen before it loads); the lift and the
    /// panels' buttons on their presses only.
    /// </summary>
    public sealed partial class FloorsDirector : MonoBehaviour
    {
        public static IModLogger Logger;

        public static FloorsDirector Instance { get; private set; }

        private const float DoorReach = 3.2f;      // metres from the place outside a door that its panel shows within
        private const float DoorRise = 2.5f;       // metres up or down from that place it still shows within
        private const float LiftReach = 2.6f;      // metres from where the player came in that the lift's panel shows within
        private const float LiftRise = 2f;         // metres up or down
        private const float WatchReach = 12f;      // metres from a door that the game's own click on its building is watched for within
        private const float LiftFade = 0.25f;      // seconds of the lift's fade each way, as the university's and the towers' were
        private const float LiftWaitMost = 6f;     // real seconds a lift keeps the screen dark for a floor's furniture at most
        private const float SettleMost = 15f;      // real seconds a borrowed structure is waited on for (FloorLoan.MostSeconds)

        private static readonly FloorLoan Loan = new FloorLoan();
        private static FloorVisit _visit;          // the player on a floor, or null
        private static bool _lifting;              // a lift is under way behind the dark screen
        private static bool _wrongTold;            // found inside on no floor: acted on once a going in
        private static bool _broken;               // stood down after a fault: everything given back, nothing more done

        private Action<Address> _onEnter, _onExit, _enterSeen, _exitSeen;
        private Action _onSave;

        /// <summary>The player's floor now, or null.</summary>
        public static FloorVisit Visit => _visit;

        /// <summary>True while a lift between floors is under way (the screen dark).</summary>
        public static bool Lifting => _lifting;

        /// <summary>True from a floor asked for until the player is in, and during a lift: no other going in now.</summary>
        public static bool GoingIn => Loan.Pending || Loan.Borrowed || _lifting;

        /// <summary>The door of a building nearest the player (the game's door id), as the floors go in by.</summary>
        public static int NearestDoor(CityBuildingController building) => FloorsGame.NearestDoor(building);

        /// <summary>True while a panel of the floors' is up, and in the frame its button ran (World/NearbyInteraction leaves the key alone).</summary>
        public static bool TakesInteractKey
        {
            get
            {
                for (int i = 0; i < Hosts.Count; i++)
                {
                    if (Hosts[i].DoorPanel.TakesKey || Hosts[i].LiftPanel.TakesKey)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private void Awake()
        {
            Instance = this;
            _broken = false;
            _onEnter = Guard.Wrap<Address>(Entered);
            _onExit = Guard.Wrap<Address>(Left);
            _onSave = OnSave;
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            GlobalEvents.onEnterBuilding -= _onEnter;
            GlobalEvents.onExitBuilding -= _onExit;
            SaveHook.Drop(_onSave);
            try
            {
                Everything("the city went", true);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            Hosts.Clear();
            Guests.Clear();
            Parked.Clear();
            ParkedGuests.Clear();
            Claimed.Clear();
            _lookAgainAt = 0f;
            _waitTold = 0;
            _dirty = true;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private IEnumerator Tick()
        {
            var second = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return second;
                if (_broken)
                {
                    continue;
                }
                try
                {
                    Slow();
                }
                catch (Exception e)
                {
                    StandDown(e);
                }
            }
        }

        private static readonly int HotTick = HotSpots.Slot("Floors: FloorsDirector.Slow");

        private void Slow()
        {
            using var hotSpot = HotSpots.Time(HotTick);
            if (!FloorsGame.Loaded || Meta.SaveTools.Dormant)
            {
                return;
            }
            EventHold.Keep(ref GlobalEvents.onEnterBuilding, _onEnter, ref _enterSeen);
            EventHold.Keep(ref GlobalEvents.onExitBuilding, _onExit, ref _exitSeen);
            SaveHook.Keep(_onSave);
            Follow();
            if (Claimed.Count > 0)
            {
                LetClaims(null, false);   // a building taken for places that is let meanwhile goes back; the book takes another
            }
            if (_lookAgainAt > 0f && Time.realtimeSinceStartup >= _lookAgainAt)
            {
                _lookAgainAt = 0f;
                _dirty = true;   // places waiting for a building: the city is looked at again for an empty office
            }
            Book();
            for (int i = 0; i < Hosts.Count; i++)
            {
                FloorsGame.Doors(Hosts[i], Hosts[i].Open());
            }
            if (Loan.Borrowed && (Loan.Timeout(Time.realtimeSinceStartup) & FloorDo.GiveBack) != 0)
            {
                GiveBackNow("the game took longer than 15 s to go in");   // the net under the net, should the settle's look not have run
            }
            Floors();
        }

        // A fault in the timer: everything borrowed given back once, the floors stand down for the session with one line.
        private void StandDown(Exception e)
        {
            Logger?.Error(e);
            if (_broken)
            {
                return;
            }
            _broken = true;
            try
            {
                Everything("the floors stood down", false);
            }
            catch (Exception again)
            {
                Logger?.Error(again);
            }
            Logger?.Warn("Floors: the floors of the mod's buildings stand down for this session after the error above; what they borrowed of the game's is put back.");
        }

        // ---------------------------------------------------------------- what the holders and features call

        /// <summary>
        /// A holder's building with its own places (its Own list), held by the floors from now; again whenever its places change.
        /// Never a building a DLC keeps (Core/GameDlc), which is refused with a line.
        /// </summary>
        public static void Hold(FloorHost host)
        {
            if (host == null || host.Building == null || Parked.Contains(host))
            {
                return;   // put away with its feature: taken up again with it, its places as they are then
            }
            if (!Hosts.Contains(host))
            {
                if (GameDlc.Gated(host.Building))
                {
                    Logger?.Warn("Floors: " + FloorsGame.Address(host.Building) + " is kept behind a DLC and is never held for floors (" + host.Id + ").");
                    return;
                }
                if (Asleep(host.Feature))
                {
                    Parked.Add(host);
                    return;
                }
                Hosts.Add(host);
            }
            _dirty = true;
        }

        /// <summary>The holder lets its building go: its panels down, its doors as they were, the player off its floors.</summary>
        public static void Let(FloorHost host, string why)
        {
            if (host == null || Parked.Remove(host) || !Hosts.Remove(host))
            {
                return;   // one put away with its feature has nothing out
            }
            Drop(host, why);
            _dirty = true;
            Book();
        }

        /// <summary>A feature asks a floor for its place: a spare one now, or as soon as a building has one.</summary>
        public static void Ask(FloorPlace place)
        {
            if (place == null || Guests.Contains(place) || ParkedGuests.Contains(place))
            {
                return;
            }
            place.Guest = true;
            if (Asleep(place.Feature))
            {
                ParkedGuests.Add(place);
                return;
            }
            Guests.Add(place);
            _dirty = true;
        }

        /// <summary>The feature lets its place's floor go (its option off, the city going): the player off it first.</summary>
        public static void Release(FloorPlace place, string why)
        {
            if (place == null || ParkedGuests.Remove(place) || !Guests.Remove(place))
            {
                return;
            }
            PutAway(place, why);
            Book();
        }

        // A place's floor taken from it: the player off it first, or a going in to it under way given the building's own structure.
        private static void PutAway(FloorPlace place, string why)
        {
            if (_visit != null && ReferenceEquals(_visit.Place, place))
            {
                Off(why);
            }
            else if (place.Host != null && string.Equals(Loan.PendingOf, place.Host.Key, StringComparison.Ordinal) && ReferenceEquals(place.Host.PlaceOn(Loan.PendingFloor), place))
            {
                Act(Loan.Gone(place.Host.Key), why);   // going in to it: the game loads the building's own structure, and no floor comes of it
            }
            place.Host = null;
            place.First = 0;
            _dirty = true;
        }

        // The floors follow their features. One whose feature stands down in this city (Core/CityCheck: another city, or the test
        // as in one) or is switched off after its faults no longer runs the code that would let its building or place go, so the
        // core puts them away as a Let or a Release would, the player off their floors first, and takes them up again once the
        // feature is back. Two set lookups a building and a place, once a second; nothing allocated unless one goes or comes.
        private static void Follow()
        {
            for (int i = Hosts.Count - 1; i >= 0; i--)
            {
                FloorHost host = Hosts[i];
                if (Asleep(host.Feature))
                {
                    string why = AsleepWhy(host.Feature);
                    Hosts.RemoveAt(i);
                    Parked.Add(host);
                    Drop(host, why);
                    _dirty = true;
                    Logger?.Info("Floors: " + host.Name + " (" + host.Id + ") is put away while " + why + "; it comes back with its feature.");
                }
            }
            for (int i = Guests.Count - 1; i >= 0; i--)
            {
                FloorPlace place = Guests[i];
                if (Asleep(place.Feature))
                {
                    string why = AsleepWhy(place.Feature);
                    Guests.RemoveAt(i);
                    ParkedGuests.Add(place);
                    PutAway(place, why);
                    Logger?.Info("Floors: the floor of " + place.Said + " is put away while " + why + "; it comes back with its feature.");
                }
            }
            for (int i = Parked.Count - 1; i >= 0; i--)
            {
                FloorHost host = Parked[i];
                if (!Asleep(host.Feature))
                {
                    Parked.RemoveAt(i);
                    Hosts.Add(host);
                    _dirty = true;
                    Logger?.Info("Floors: " + host.Name + " (" + host.Id + ") is held again, its feature '" + host.Feature + "' back.");
                }
            }
            for (int i = ParkedGuests.Count - 1; i >= 0; i--)
            {
                FloorPlace place = ParkedGuests[i];
                if (!Asleep(place.Feature))
                {
                    ParkedGuests.RemoveAt(i);
                    Guests.Add(place);
                    _dirty = true;
                    Logger?.Info("Floors: " + place.Said + " asks for a floor again, its feature '" + place.Feature + "' back.");
                }
            }
        }

        // True while a feature's code does not run: it stands down in this city, or is switched off after its faults.
        private static bool Asleep(string feature) => FeatureGuards.FeatureAway(feature) || FeatureGuards.FeatureOff(feature);

        private static string AsleepWhy(string feature)
        {
            return FeatureGuards.FeatureAway(feature) ? "its feature '" + feature + "' stands down in this city" : "its feature '" + feature + "' is switched off after its faults";
        }

        /// <summary>Where a place's floor is now, for the map, the route guider and the Guide; false while it has none.</summary>
        public static bool Where(FloorPlace place, out FloorWhere where)
        {
            Book();
            where = default;
            FloorHost host = place != null ? place.Host : null;
            if (host == null || host.Building == null || place.First < 1)
            {
                return false;
            }
            where.Host = host;
            where.Building = host.Building;
            where.Street = host.Building.building != null ? host.Building.building.StreetName : null;
            where.Number = host.Building.building != null ? host.Building.building.StreetNumber : 0;
            where.Floor = place.First;
            where.Door = host.Door;
            return where.Street != null;
        }

        /// <summary>Changes whenever a place's floor or a building's floors change (for map rows and the like).</summary>
        public static int Stamp { get; private set; }

        /// <summary>The building held at an address, or null.</summary>
        public static FloorHost HostAt(string street, int number)
        {
            string key = FloorsRules.Key(street, number);
            for (int i = 0; i < Hosts.Count; i++)
            {
                if (string.Equals(Hosts[i].Key, key, StringComparison.Ordinal))
                {
                    return Hosts[i];
                }
            }
            return null;
        }

        /// <summary>True while the player is on a floor of this building.</summary>
        public static bool OnFloorOf(FloorHost host) => host != null && _visit != null && ReferenceEquals(_visit.Host, host);

        // Everything borrowed given back and every visit ended: the city going, or the floors standing down (then the player on a
        // floor of another structure is taken out, and the manager's registration is the building's own again, as on a release).
        private static void Everything(string why, bool cityGoing)
        {
            if (_visit != null && !cityGoing && !FloorsGame.CityGoing)
            {
                Act(Loan.Gone(_visit.Key), why);
            }
            FloorDo d = Loan.CityGone();
            if ((d & FloorDo.GiveBack) != 0)
            {
                GiveBackNow(why);
            }
            if (_visit != null)
            {
                EndVisit(why, false);
            }
            for (int i = 0; i < Hosts.Count; i++)
            {
                FloorsGame.PutDoorsBack(Hosts[i]);
                Hosts[i].HidePanels(why);
            }
            LetClaims(why, true);
            _lifting = false;
            _watching = false;
        }
    }
}
