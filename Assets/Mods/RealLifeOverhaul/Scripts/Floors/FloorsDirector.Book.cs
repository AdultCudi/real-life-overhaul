using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Floors
{
    /// <summary>
    /// The floors' book: the buildings held and the places that asked, worked out again whenever one changes. Each building's
    /// own places stand from floor 1; spare floors go to the places that asked by FloorsRules.Assign; places with none get one
    /// building taken for them all (Core/SpecialBuildings, its existing API: refused for rent in the interface only), and a
    /// building taken that no place needs any more is given back once the player is not in it. COST: on a change only.
    /// </summary>
    public sealed partial class FloorsDirector
    {
        private const string ClaimPrefix = "floors_host_";   // the ids of buildings taken for places in Core/SpecialBuildings
        private const float LookAgain = 60f;   // real seconds between looks at the city for an empty office while places wait for one
        private static readonly Color MapColour = new Color(0.36f, 0.42f, 0.52f);   // their map row's and markers' colour: the slate of a city office

        private static readonly List<FloorHost> Hosts = new List<FloorHost>(8);
        private static readonly List<FloorPlace> Guests = new List<FloorPlace>(8);
        private static readonly List<FloorHost> Parked = new List<FloorHost>(4);           // buildings whose feature does not run now (Follow)
        private static readonly List<FloorPlace> ParkedGuests = new List<FloorPlace>(4);   // ...and places
        private static readonly List<ClaimedHost> Claimed = new List<ClaimedHost>(FloorsRules.MostClaims);
        private static readonly List<FloorHostRow> HostRows = new List<FloorHostRow>(8);
        private static readonly List<FloorGuestRow> GuestRows = new List<FloorGuestRow>(8);
        private static int[] _hostOf = new int[8];
        private static int[] _first = new int[8];
        private static bool _dirty = true;
        private static int _claims;   // buildings taken this city, for their ids
        private static float _lookAgainAt;   // when the city is looked at again for places waiting for a building; 0 while none waits
        private static int _waitTold;        // places last said to be waiting, so the line is written once a change
        private static MapPlaces.Category _map;   // the city map's row of the buildings taken for places, made with the first

        /// <summary>The buildings the floors hold now (the holders' and those taken for places).</summary>
        public static int HostCount => Hosts.Count;

        // The book worked out again after a building or a place changed.
        private static void Book()
        {
            if (!_dirty)
            {
                return;
            }
            _dirty = false;
            for (int i = 0; i < Hosts.Count; i++)
            {
                Own(Hosts[i]);
            }
            for (int i = 0; i < Guests.Count; i++)
            {
                Guests[i].Host = null;
                Guests[i].First = 0;
            }
            Assigned();
            for (int round = 0; round < FloorsRules.MostClaims && FloorsRules.ClaimsFor(GuestRows, _hostOf) > 0 && Claimed.Count < FloorsRules.MostClaims; round++)
            {
                if (!Claim())
                {
                    break;
                }
                Assigned();
            }
            if (FloorsRules.ClaimsFor(GuestRows, _hostOf) == 0)
            {
                _lookAgainAt = 0f;
                _waitTold = 0;
            }
            LetIdleClaims();
            for (int i = 0; i < Hosts.Count; i++)
            {
                Table(Hosts[i]);
            }
            Stamp++;
            Kept();
        }

        // A building's own places from floor 1, and the floors it holds in all.
        private static void Own(FloorHost host)
        {
            host.Key = FloorsGame.KeyOf(host.Building);
            int floor = 1;
            for (int p = 0; p < host.Own.Count; p++)
            {
                FloorPlace place = host.Own[p];
                place.Host = host;
                place.First = floor;
                place.Guest = false;
                floor += Mathf.Max(1, place.Floors);
            }
            host.Used = floor - 1;
            string size = OwnSize(host, out _);
            host.Capacity = FloorsRules.Capacity(FloorsGame.Sqm(host.Building), FloorsGame.StoreySqm(size), host.Used);
        }

        // The spare floors given out by the rule, written on the places.
        private static void Assigned()
        {
            HostRows.Clear();
            for (int i = 0; i < Hosts.Count; i++)
            {
                HostRows.Add(new FloorHostRow { Key = Hosts[i].Key, Rank = Hosts[i].Rank, Used = Hosts[i].Used, Capacity = Hosts[i].Capacity });
            }
            GuestRows.Clear();
            for (int i = 0; i < Guests.Count; i++)
            {
                GuestRows.Add(new FloorGuestRow { Id = Guests[i].Id, Floors = Mathf.Max(1, Guests[i].Floors) });
            }
            if (_hostOf.Length < Guests.Count)
            {
                _hostOf = new int[Guests.Count + 4];
                _first = new int[Guests.Count + 4];
            }
            FloorsRules.Assign(HostRows, GuestRows, _hostOf, _first);
            for (int g = 0; g < Guests.Count; g++)
            {
                Guests[g].Host = _hostOf[g] >= 0 ? Hosts[_hostOf[g]] : null;
                Guests[g].First = _hostOf[g] >= 0 ? _first[g] : 0;
            }
        }

        // Each building's floors in order: its own places', then the places given spare floors there.
        private static void Table(FloorHost host)
        {
            int floors = host.Used;
            for (int g = 0; g < Guests.Count; g++)
            {
                if (ReferenceEquals(Guests[g].Host, host))
                {
                    floors = Mathf.Max(floors, Guests[g].First + Mathf.Max(1, Guests[g].Floors) - 1);
                }
            }
            var table = new FloorSlot[floors];
            for (int p = 0; p < host.Own.Count; p++)
            {
                Fill(table, host.Own[p]);
            }
            for (int g = 0; g < Guests.Count; g++)
            {
                if (ReferenceEquals(Guests[g].Host, host))
                {
                    Fill(table, Guests[g]);
                }
            }
            host.Table = table;
            if (host.DoorPage >= FloorsRules.Pages(floors, 0, FloorsRules.Slots))
            {
                host.DoorPage = 0;
            }
        }

        private static void Fill(FloorSlot[] table, FloorPlace place)
        {
            for (int part = 1; part <= Mathf.Max(1, place.Floors); part++)
            {
                int floor = place.First + part - 1;
                if (floor >= 1 && floor <= table.Length)
                {
                    table[floor - 1] = new FloorSlot { Place = place, Part = part };
                }
            }
        }

        // The floor the player is on after the book changed: the same place, or another on the same floor laid in its place, or out.
        private static void Kept()
        {
            FloorVisit v = _visit;
            if (v == null || _lifting)
            {
                return;
            }
            FloorHost host = v.Host;
            if (!Hosts.Contains(host))
            {
                return;   // let go: Drop has ended the visit
            }
            FloorPlace now = host.PlaceOn(v.Floor);
            if (ReferenceEquals(now, v.Place))
            {
                v.Part = host.PartOf(v.Floor);
                return;
            }
            // Another place is on the player's floor now (a holder's places chosen again, or this one released): laid on the
            // same structure where it can be, as the game's door would have; else the player is off it.
            int floor = now != null ? v.Floor : FloorsRules.DoorFloor(host.Floors, host.DefaultFloor(false));
            FloorPlace to = host.PlaceOn(floor);
            if (to != null && Shown(v, host, to))
            {
                Instance?.Swap(v, to, floor);
                return;
            }
            Off("the floor the player is on is no longer its place's");
        }

        // True if a place's floors stand on the structure on show now.
        private static bool Shown(FloorVisit v, FloorHost host, FloorPlace place)
        {
            string size = Structure(host, place, out int version);
            string shownSize = v.Borrowed ? v.ShownSize : v.OwnSize;
            int shownVersion = v.Borrowed ? v.ShownVersion : v.OwnVersion;
            return string.Equals(size, shownSize, StringComparison.Ordinal) && version == shownVersion;
        }

        // The structure a place's floors stand on in a building: its own wish, or the building's.
        private static string Structure(FloorHost host, FloorPlace place, out int version)
        {
            string own = OwnSize(host, out int ownVersion);
            if (place != null && place.Structure != null && !string.IsNullOrEmpty(place.Structure.Size))
            {
                version = place.Structure.Version;
                return place.Structure.Size;
            }
            version = ownVersion;
            return own;
        }

        // The building's own size and version, also while it reads as another structure.
        private static string OwnSize(FloorHost host, out int version)
        {
            if (_asset != null && host.Building != null && ReferenceEquals(host.Building.building, _asset))
            {
                version = _ownVersion;
                return _ownSize;
            }
            version = FloorsGame.VersionOf(host.Building);
            return FloorsGame.SizeOf(host.Building);
        }

        // True if a place's floors in this building stand on another structure than the building's own.
        private static bool NeedsBorrow(FloorHost host, FloorPlace place)
        {
            string own = OwnSize(host, out int ownVersion);
            return place != null && place.Structure != null && !FloorsRules.Own(place.Structure.Size, place.Structure.Version, own, ownVersion);
        }

        private static FloorHost HostByKey(string key)
        {
            for (int i = 0; key != null && i < Hosts.Count; i++)
            {
                if (string.Equals(Hosts[i].Key, key, StringComparison.Ordinal))
                {
                    return Hosts[i];
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- a building taken for places

        // One more building taken for the places with no floor: the largest empty office that nothing holds. False with none.
        private static bool Claim()
        {
            CityBuildingController b = FloorsGame.PickClaim(SpecialBuildings.Holds);
            if (b == null)
            {
                int waiting = Unplaced();
                if (waiting != _waitTold)
                {
                    _waitTold = waiting;
                    Logger?.Info("Floors: " + waiting + " place(s) found no spare floor, and no empty office building is free to take for them; the city is looked at again each minute.");
                }
                _lookAgainAt = Time.realtimeSinceStartup + LookAgain;
                return false;
            }
            _waitTold = 0;
            int index = _claims++;
            var host = new ClaimedHost(ClaimPrefix + index, b);
            if (!SpecialBuildings.Set(ClaimPrefix + index, b, SpecialKind.Other, "reallife_floors_host", "reallife_floors_host_card_type"))
            {
                Logger?.Info("Floors: " + FloorsGame.Address(b) + " could not be taken for places (the book refused it).");
                return false;
            }
            Claimed.Add(host);
            Hosts.Add(host);
            Own(host);
            OnMap(host);
            Logger?.Info("Floors: " + FloorsGame.Address(b) + " (" + FloorsGame.Sqm(b) + " sqm, " + host.Capacity + " floors) is taken for the places that found no spare floor: " + Unplaced()
                         + " of them; one building for them all.");
            return true;
        }

        private static int Unplaced()
        {
            int n = 0;
            for (int g = 0; g < Guests.Count; g++)
            {
                n += _hostOf[g] < 0 ? 1 : 0;
            }
            return n;
        }

        // A building taken for places that none has a floor in now is given back, unless the player is in it. Every one is
        // looked at before any goes, while the rule's building numbers still stand.
        private static void LetIdleClaims()
        {
            IdleClaims.Clear();
            string inside = FloorsGame.InsideKey();
            for (int i = 0; i < Claimed.Count; i++)
            {
                ClaimedHost host = Claimed[i];
                int index = Hosts.IndexOf(host);
                if (index >= 0 && FloorsRules.ClaimIdle(index, _hostOf, Guests.Count) && !OnFloorOf(host) && !string.Equals(inside, host.Key, StringComparison.Ordinal))
                {
                    IdleClaims.Add(host);
                }
            }
            for (int i = 0; i < IdleClaims.Count; i++)
            {
                LetClaim(IdleClaims[i], "no place has a floor there now");
            }
            IdleClaims.Clear();
        }

        private static readonly List<ClaimedHost> IdleClaims = new List<ClaimedHost>(FloorsRules.MostClaims);

        private static void LetClaim(ClaimedHost host, string why)
        {
            if (host.Marker != null)
            {
                _map?.Remove(host.Marker);
                host.Marker = null;
            }
            Claimed.Remove(host);
            if (Hosts.Remove(host))
            {
                Drop(host, why);
            }
            SpecialBuildings.Remove(host.Id);
            Logger?.Info("Floors: " + FloorsGame.Address(host.Building) + " is given back (" + why + ").");
            _dirty = true;
        }

        // Every building taken for places given back (the city going, the floors standing down) or, not all, those let in the meantime.
        private static void LetClaims(string why, bool all)
        {
            for (int i = Claimed.Count - 1; i >= 0; i--)
            {
                ClaimedHost host = Claimed[i];
                if (all || !host.Open())
                {
                    LetClaim(host, all ? why : "it is let now, or no longer empty");
                }
            }
        }

        // A building taken for places on the city map, under a row of its own (as every building the mod takes has), shown in the street too.
        private static void OnMap(ClaimedHost host)
        {
            try
            {
                if (_map == null)
                {
                    _map = MapPlaces.Register("floors", "reallife_floors_host", MapColour, MapIcon.Briefcase, onByDefault: true, inStreet: true);
                    _map.Available = () => Claimed.Count > 0;
                }
                host.Marker = new MapPlace(host.Door, host.Name + ", " + FloorsGame.Address(host.Building), host.Building) { CardTypeKey = "reallife_floors_host_card_type" };
                _map.Add(host.Marker);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Logger?.Warn("Floors: " + FloorsGame.Address(host.Building) + " could not be put on the city map; its floors are there all the same.");
            }
        }

        // A building that left the floors: what was borrowed of it back, the player off its floors, its doors and panels as found.
        private static void Drop(FloorHost host, string why)
        {
            if (host.Key != null && (Loan.BorrowedOf == host.Key || Loan.PendingOf == host.Key || OnFloorOf(host)))
            {
                Act(Loan.Gone(host.Key), why);
            }
            FloorsGame.PutDoorsBack(host);
            host.HidePanels(why);
            if (ReferenceEquals(_watchHost, host))
            {
                _watching = false;
                _watchHost = null;
            }
            for (int p = 0; p < host.Own.Count; p++)
            {
                host.Own[p].Host = null;
                host.Own[p].First = 0;
            }
        }

        /// <summary>
        /// A building taken by the floors for places that found no spare floor: one for them all. Open while it stands empty
        /// (it is given back once let); its door panel names its floors.
        /// </summary>
        private sealed class ClaimedHost : FloorHost
        {
            public MapPlace Marker;   // its place on the city map while taken

            public ClaimedHost(string id, CityBuildingController building) : base(id, null, "Floors check (door)", "Floors check (lift)")
            {
                Building = building;
                Door = FloorsGame.Front(building);
                Name = "reallife_floors_host".GetLocalization();
                Rank = FloorsRules.RankClaimed;
            }

            public override bool Open() => FloorsGame.Vacant(FloorsGame.RegistrationOf(Building)) && !SpecialBuildings.Players(Building);

            public override void DoorLine(bool open, out string left, out string right, out Color32 colour)
            {
                left = FloorsText.T("reallife_floors_host_door", "count", Floors.ToString());
                right = (open ? "common_open" : "common_closed").GetLocalization();
                colour = open ? WorldPanel.Green() : WorldPanel.Red();
            }
        }
    }
}
