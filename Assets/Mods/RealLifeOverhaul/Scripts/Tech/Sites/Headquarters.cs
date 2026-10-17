using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Buildings.Outdoors;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The city's tech companies' headquarters: the tech sector's rival firms (Tech/Hiring/HiringCatalogue.RivalFirms: the
    /// stock market's software, hardware and network companies the tech sector names, Skyvault Systems' cloud among them)
    /// share a few of the city's largest office buildings, the towers, a floor each (<see cref="HqTowers"/>: twelve
    /// companies, three towers of four). Up to 0.10.4 each had a building of its own, and with the City Utilities office
    /// and the university the mod held every empty office the city had, so a player found none to rent; the towers leave
    /// nine of those to let.
    ///
    /// Which building is <see cref="HqTowers"/>'s: the same city gives the same answer on every machine, so in a Going
    /// Public session every player's city has the same towers and floors (the candidates are the city's own office
    /// buildings, with no special service, not behind a private fence and needing no DLC; who rents what is the same for
    /// everyone in a session). Never a building the player (or another player) rents or owns, never one with a business in
    /// it, never another of the mod's special buildings, never one twice; chosen after the utilities office, the
    /// courthouse, the police stations and the university have chosen theirs (TechBridges.OthersPicked; a minute and a
    /// half at most), and chosen again once a game day and whenever one of them is let (only the tower that stood there
    /// moves, with its companies).
    ///
    /// Each tower carries its own name wherever the game names a building, with its companies as its tenants (Core/
    /// SpecialBuildings: the card, the map's search by the tower's name and by each company's, the GPS, the headline
    /// inside) and is on the city map in the "Tech companies" row (World/MapLinks). Its doors let the player in (the game's
    /// own "always open" flag, put back when the option is off); the floor is chosen at the door or the lift (TowerFloors),
    /// and inside, that company's lobby is furnished (LobbyView) and the rest of its floor fitted out round it (FitOut/
    /// FitView, from TechSites). Careers sends the player there for a job at a company (CareersBridges.Headquarters), and
    /// the player's desk is on that company's floor. Nothing of it is saved. Nothing here runs by itself: TechSites ticks
    /// it once a second. A choice (once a game day, and when a tower's building is let) looks at the city's office
    /// buildings that can ever be a tower, listed once a city in the city's order (about 60 of some 2,000 buildings: the
    /// type, the fence, the DLC and the doors never change), so it costs tens of microseconds.
    /// </summary>
    internal sealed class Headquarters
    {
        public const string IdPrefix = "techhq_";
        private const string TowerId = IdPrefix + "tower_";
        private const string Office = "ba:buildingtype_office";
        private const string Empty = "ba:businesstype_empty";
        private const int WaitForOthers = 90;   // seconds at most

        /// <summary>The towers' proper names, never translated (the companies' names are not either). Three are wanted for twelve companies.</summary>
        public static readonly string[] TowerNames = { "Harbourgate Tower", "Meridian Exchange", "Lanternhill House", "Northbank Tower", "Copperfield Court", "Westwater House" };

        /// <summary>One of the towers: a building and the companies on its floors.</summary>
        public sealed class Tower
        {
            public int Index;
            public string Name;
            public CityBuildingController Building;
            public string Street;
            public int Number;
            public Vector3 Front;
            public readonly List<Hq> Floors = new List<Hq>(HqTowers.PerTower);   // floor 1 first
            public string[] Tenants = new string[0];
            public bool[] DoorWas;
            public bool DoorsOpen;

            /// <summary>The company on a floor (from 1), or null.</summary>
            public Hq OnFloor(int floor) => floor >= 1 && floor <= Floors.Count ? Floors[floor - 1] : null;
        }

        /// <summary>One company's headquarters: its floor of a tower.</summary>
        public sealed class Hq
        {
            public int Company;
            public string Name;
            public Tower Tower;
            public int Floor;   // from 1

            public CityBuildingController Building => Tower != null ? Tower.Building : null;
            public string Street => Tower != null ? Tower.Street : null;
            public int Number => Tower != null ? Tower.Number : 0;
            public Vector3 Front => Tower != null ? Tower.Front : Vector3.zero;
        }

        private readonly IModLogger _logger;
        private readonly List<Hq> _all = new List<Hq>(12);
        private readonly List<Tower> _towers = new List<Tower>(4);
        private readonly List<CityBuildingController> _candidates = new List<CityBuildingController>(64);
        private readonly List<HqCandidate> _rows = new List<HqCandidate>(64);
        private readonly List<CityBuildingController> _offices = new List<CityBuildingController>(96);   // the city's buildings that can ever be a tower, in its order
        private CityBuildingController[] _officesOf;                                                       // the city's list they were read from
        private int _waited;
        private int _day = -1;
        private int _look;
        private bool _failed;

        public Headquarters(IModLogger logger)
        {
            _logger = logger;
        }

        /// <summary>Every company that has a floor, by company.</summary>
        public IReadOnlyList<Hq> All => _all;

        /// <summary>The towers that stand, by index.</summary>
        public IReadOnlyList<Tower> Towers => _towers;

        /// <summary>Changes whenever the headquarters do (for the map, the careers, the floors).</summary>
        public int Stamp { get; private set; }

        public bool Picked { get; private set; }

        /// <summary>The office buildings the towers were chosen from at the last choice.</summary>
        public int Candidates => _candidates.Count;

        /// <summary>The companies, in the order they choose.</summary>
        public static IReadOnlyList<string> Companies => HiringCatalogue.RivalFirms;

        /// <summary>Once a second while the option is on.</summary>
        public void Tick(int day)
        {
            if (_failed)
            {
                return;
            }
            try
            {
                if (!Picked)
                {
                    Func<bool> others = TechBridges.OthersPicked;
                    if (others != null && !others() && ++_waited < WaitForOthers)
                    {
                        return;
                    }
                    Pick();
                    _day = day;
                    return;
                }
                bool let = false;
                if (++_look >= 5)
                {
                    _look = 0;
                    for (int i = 0; i < _towers.Count && !let; i++)
                    {
                        let = !Vacant(_towers[i].Building);
                    }
                }
                if (let || day != _day)
                {
                    _day = day;
                    Pick();
                }
                for (int i = 0; i < _towers.Count; i++)
                {
                    Doors(_towers[i], true);
                }
            }
            catch (Exception e)
            {
                _failed = true;
                _logger?.Error(e);
                _logger?.Warn("Tech sites: the tech companies' headquarters could not be kept after the error above; the companies keep to the Tech app.");
                Off();
            }
        }

        /// <summary>The headquarters of a company by its name: its tower's address.</summary>
        public bool Of(string company, out string street, out int number)
        {
            Hq hq = Named(company);
            street = hq != null ? hq.Street : null;
            number = hq != null ? hq.Number : 0;
            return hq != null;
        }

        /// <summary>A company's floor by its name, or null.</summary>
        public Hq Named(string company)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                if (string.Equals(_all[i].Name, company, StringComparison.Ordinal))
                {
                    return _all[i];
                }
            }
            return null;
        }

        /// <summary>The tower at an address, or null.</summary>
        public Tower At(string street, int number)
        {
            for (int i = 0; i < _towers.Count; i++)
            {
                if (_towers[i].Number == number && string.Equals(_towers[i].Street, street, StringComparison.Ordinal))
                {
                    return _towers[i];
                }
            }
            return null;
        }

        /// <summary>True if the player can walk into a tower now: its building still stands empty.</summary>
        public static bool Open(Tower tower) => tower != null && Vacant(tower.Building);

        /// <summary>The same for a company's floor.</summary>
        public static bool Open(Hq hq) => hq != null && Open(hq.Tower);

        // The city's buildings that can ever be a tower, in the city's order: an office of the base game, outside any
        // private fence, with a door and a floor. None of that changes in a city, so they are listed once for it (a
        // building not yet read is kept in the list and looked at again with every choice).
        private void OfficesOf(CityBuildingController[] buildings)
        {
            if (ReferenceEquals(buildings, _officesOf))
            {
                return;
            }
            _officesOf = buildings;
            _offices.Clear();
            for (int i = 0; i < buildings.Length; i++)
            {
                CityBuildingController b = buildings[i];
                if (b != null && (b.building == null || CanEverBe(b)))
                {
                    _offices.Add(b);
                }
            }
        }

        private static bool CanEverBe(CityBuildingController b)
        {
            return b.building.BuildingType == Office && !GameMembers.Fenced(b.building) && (int)b.building.requiredDLC == 0 && !b.building.IsHamptonsHouse() && b.entranceDoors != null
                   && b.entranceDoors.Length > 0 && b.building.totalSqm > 0;
        }

        // The choice, made again from the city as it is now. Only what changed is put in the book again.
        private void Pick()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] buildings = city != null ? city.cityBuildingControllers : null;
            if (buildings == null || buildings.Length == 0)
            {
                return;
            }
            Picked = true;
            OfficesOf(buildings);
            _candidates.Clear();
            _rows.Clear();
            for (int i = 0; i < _offices.Count; i++)
            {
                CityBuildingController b = _offices[i];
                if (b == null || b.building == null || !CanEverBe(b) || Offices.Conversions.Holds(b.building) || b.building.SpecialService != null)
                {
                    continue;
                }
                _candidates.Add(b);
                _rows.Add(new HqCandidate { Street = b.building.StreetName, Number = b.building.StreetNumber, Sqm = b.building.totalSqm });
            }
            int wanted = Math.Min(HqTowers.Towers(Companies.Count), TowerNames.Length);
            var picks = new int[wanted];
            HqTowers.Pick(_rows, wanted, i => Taken(_candidates[i]), picks);
            var towerOf = new int[Companies.Count];
            var floorOf = new int[Companies.Count];
            HqTowers.Floors(Companies.Count, picks, towerOf, floorOf);

            // The towers: those that move let go of their buildings first, so that one can take another's.
            bool changed = false;
            var stood = new Tower[wanted];
            for (int t = 0; t < wanted; t++)
            {
                CityBuildingController now = picks[t] >= 0 ? _candidates[picks[t]] : null;
                Tower was = TowerOf(t);
                if (was != null && ReferenceEquals(was.Building, now))
                {
                    stood[t] = was;
                    continue;
                }
                if (was != null)
                {
                    Doors(was, false);
                    _towers.Remove(was);
                    SpecialBuildings.Remove(TowerId + t);
                    changed = true;
                }
            }
            for (int t = 0; t < wanted; t++)
            {
                if (stood[t] != null || picks[t] < 0)
                {
                    continue;
                }
                CityBuildingController now = _candidates[picks[t]];
                stood[t] = new Tower
                {
                    Index = t, Name = TowerNames[t], Building = now, Street = now.building.StreetName, Number = now.building.StreetNumber,
                    Front = JobSpots.Front(now, out Vector3 front) ? front : Door(now)
                };
                _towers.Add(stood[t]);
                changed = true;
            }

            // The floors: each company on its tower's floor; a tower whose companies changed is named again with them.
            for (int k = 0; k < Companies.Count; k++)
            {
                Tower tower = towerOf[k] >= 0 ? stood[towerOf[k]] : null;
                Hq hq = Find(k);
                if (hq != null && ReferenceEquals(hq.Tower, tower) && hq.Floor == floorOf[k])
                {
                    continue;
                }
                changed = true;
                if (hq != null)
                {
                    _all.Remove(hq);
                }
                if (tower != null)
                {
                    _all.Add(new Hq { Company = k, Name = Companies[k], Tower = tower, Floor = floorOf[k] });
                }
            }
            if (!changed)
            {
                return;
            }
            _all.Sort((a, b) => a.Company.CompareTo(b.Company));
            _towers.Sort((a, b) => a.Index.CompareTo(b.Index));
            for (int t = 0; t < _towers.Count; t++)
            {
                Tower tower = _towers[t];
                tower.Floors.Clear();
                for (int i = 0; i < _all.Count; i++)
                {
                    if (ReferenceEquals(_all[i].Tower, tower))
                    {
                        tower.Floors.Add(_all[i]);
                    }
                }
                tower.Floors.Sort((a, b) => a.Floor.CompareTo(b.Floor));
                var tenants = new string[tower.Floors.Count];
                for (int f = 0; f < tenants.Length; f++)
                {
                    tenants[f] = tower.Floors[f].Name;
                }
                tower.Tenants = tenants;
                SpecialBuildings.SetNamed(TowerId + tower.Index, tower.Street, tower.Number, SpecialKind.Other, tower.Name, "reallife_hq_tower_card_type", () => Settings.TechSiteHq, tenants);
            }
            Stamp++;
            var said = new StringBuilder(640);
            said.Append("Tech sites: headquarters (").Append(_all.Count).Append(" of ").Append(Companies.Count).Append(" companies in ").Append(_towers.Count).Append(" towers, ")
                .Append(HqTowers.PerTower).Append(" floors to a tower, from ").Append(_candidates.Count).Append(" office buildings; ")
                .Append(Math.Max(0, _all.Count - _towers.Count)).Append(" buildings left to let that a building to each company would have taken):");
            for (int t = 0; t < _towers.Count; t++)
            {
                Tower tower = _towers[t];
                said.Append(' ').Append(tower.Name).Append(" at ").Append(tower.Street).Append(' ').Append(tower.Number)
                    .Append(" (").Append(tower.Building.building.totalSqm).Append(" sqm, ").Append(tower.Building.building.Neighbourhood).Append("):");
                for (int f = 0; f < tower.Floors.Count; f++)
                {
                    said.Append(" floor ").Append(tower.Floors[f].Floor).Append(' ').Append(tower.Floors[f].Name).Append(f + 1 < tower.Floors.Count ? "," : string.Empty);
                }
                said.Append(t + 1 < _towers.Count ? ";" : ".");
            }
            if (_all.Count < Companies.Count)
            {
                said.Append(" No office building was free for ").Append(Companies.Count - _all.Count).Append(" of them: they keep to the Tech app until one is.");
            }
            _logger?.Info(said.ToString());
        }

        private Hq Find(int company)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].Company == company)
                {
                    return _all[i];
                }
            }
            return null;
        }

        private Tower TowerOf(int index)
        {
            for (int i = 0; i < _towers.Count; i++)
            {
                if (_towers[i].Index == index)
                {
                    return _towers[i];
                }
            }
            return null;
        }

        // Another feature's building, or one that is not standing empty and to let.
        private static bool Taken(CityBuildingController building)
        {
            string holder = SpecialBuildings.HolderOf(building);
            return (holder != null && !holder.StartsWith(IdPrefix, StringComparison.Ordinal)) || !Vacant(building);
        }

        /// <summary>Standing empty: nobody rents or owns it (the player, another player in a session, a rival), nothing of the game's is in it.</summary>
        public static bool Vacant(CityBuildingController building)
        {
            BuildingRegistration reg = building != null ? building.buildingRegistration : null;
            return reg != null && reg.AvailableForRent && !reg.RentedByPlayer && !reg.BuildingOwnedByPlayer && string.IsNullOrEmpty(reg.businessOwnerRivalId)
                   && (string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == Empty) && reg.Layout == null
                   && (reg.itemInstances == null || reg.itemInstances.Count == 0);
        }

        private static Vector3 Door(CityBuildingController building)
        {
            for (int i = 0; building.entranceDoors != null && i < building.entranceDoors.Length; i++)
            {
                if (building.entranceDoors[i] != null && building.entranceDoors[i].doorTransform != null)
                {
                    return building.entranceDoors[i].doorTransform.position;
                }
            }
            return building.transform.position;
        }

        // The game's own flag on an entrance: with it set, the building's door lets the player in whatever its registration
        // says. Set on a tower's doors only while it stands empty, remembered, and put back.
        private static void Doors(Tower tower, bool open)
        {
            open &= Settings.TechSiteHq && Vacant(tower.Building);
            BuildingEntranceDoor[] doors = tower.Building != null ? tower.Building.entranceDoors : null;
            if (tower.DoorsOpen == open || doors == null)
            {
                return;
            }
            if (open)
            {
                tower.DoorWas = new bool[doors.Length];
                for (int i = 0; i < doors.Length; i++)
                {
                    if (doors[i] != null)
                    {
                        tower.DoorWas[i] = doors[i].alwaysOpen;
                        doors[i].alwaysOpen = true;
                    }
                }
            }
            else
            {
                for (int i = 0; i < doors.Length; i++)
                {
                    if (doors[i] != null)
                    {
                        doors[i].alwaysOpen = tower.DoorWas != null && i < tower.DoorWas.Length && tower.DoorWas[i];
                    }
                }
                tower.DoorWas = null;
            }
            tower.DoorsOpen = open;
        }

        /// <summary>The option is off, or the city is going: the doors put back and the names given back; chosen afresh when it is on again.</summary>
        public void Off()
        {
            for (int i = 0; i < _towers.Count; i++)
            {
                try
                {
                    Doors(_towers[i], false);
                }
                catch (Exception e)
                {
                    _logger?.Error(e);
                }
                SpecialBuildings.Remove(TowerId + _towers[i].Index);
            }
            if (_all.Count > 0 || _towers.Count > 0)
            {
                Stamp++;
            }
            _all.Clear();
            _towers.Clear();
            Picked = false;
            _waited = 0;
            _day = -1;
        }
    }
}
