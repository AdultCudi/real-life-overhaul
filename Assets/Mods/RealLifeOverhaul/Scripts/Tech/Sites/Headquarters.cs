using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Buildings.Outdoors;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The city's tech companies' headquarters: one of the city's office buildings for each of the tech sector's rival firms
    /// (Tech/Hiring/HiringCatalogue.RivalFirms: the stock market's software, hardware and network companies the tech sector
    /// names, Skyvault Systems' cloud among them). Which building is <see cref="HqPick"/>'s: the same city gives the same
    /// answer on every machine, so in a Going Public session every player's city has the same headquarters (the
    /// candidates are the city's own office buildings, with no special service, not behind a private fence and needing no
    /// DLC; who rents what is the same for everyone in a session). Never a building the player (or another player) rents
    /// or owns, never one with a business in it, never another of the mod's special buildings, never one twice; chosen
    /// after the utilities office, the courthouse, the police stations and the university have chosen theirs
    /// (TechBridges.OthersPicked; a minute and a half at most), and chosen again once a game day and whenever one of them
    /// is let (only the company whose building it was moves).
    ///
    /// Each carries its company's name wherever the game names a building (Core/SpecialBuildings: the card, the map's
    /// search, the GPS, the headline inside) and is on the city map in the "Tech companies" row (World/MapLinks). Its
    /// doors let the player in (the game's own "always open" flag, put back when the option is off), and inside the
    /// lobby is furnished (LobbyView). Careers sends the player there for a job at the company (CareersBridges.Headquarters).
    /// Nothing of it is saved. Nothing here runs by itself: TechSites ticks it once a second.
    /// </summary>
    internal sealed class Headquarters
    {
        public const string IdPrefix = "techhq_";
        private const string Office = "ba:buildingtype_office";
        private const string Empty = "ba:businesstype_empty";
        private const int WaitForOthers = 90;   // seconds at most

        /// <summary>One company's headquarters.</summary>
        public sealed class Hq
        {
            public int Company;
            public string Name;
            public CityBuildingController Building;
            public string Street;
            public int Number;
            public Vector3 Front;
            public bool[] DoorWas;
            public bool DoorsOpen;
        }

        private readonly IModLogger _logger;
        private readonly List<Hq> _all = new List<Hq>(12);
        private readonly List<CityBuildingController> _candidates = new List<CityBuildingController>(64);
        private readonly List<HqCandidate> _rows = new List<HqCandidate>(64);
        private int _waited;
        private int _day = -1;
        private int _look;
        private bool _failed;

        public Headquarters(IModLogger logger)
        {
            _logger = logger;
        }

        public IReadOnlyList<Hq> All => _all;

        /// <summary>Changes whenever the headquarters do (for the map).</summary>
        public int Stamp { get; private set; }

        public bool Picked { get; private set; }

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
                    for (int i = 0; i < _all.Count && !let; i++)
                    {
                        let = !Vacant(_all[i].Building);
                    }
                }
                if (let || day != _day)
                {
                    _day = day;
                    Pick();
                }
                for (int i = 0; i < _all.Count; i++)
                {
                    Doors(_all[i], true);
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

        /// <summary>The headquarters of a company by its name.</summary>
        public bool Of(string company, out string street, out int number)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                if (string.Equals(_all[i].Name, company, StringComparison.Ordinal))
                {
                    street = _all[i].Street;
                    number = _all[i].Number;
                    return true;
                }
            }
            street = null;
            number = 0;
            return false;
        }

        /// <summary>The headquarters at an address, or null.</summary>
        public Hq At(string street, int number)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].Number == number && string.Equals(_all[i].Street, street, StringComparison.Ordinal))
                {
                    return _all[i];
                }
            }
            return null;
        }

        /// <summary>True if the player can walk into a headquarters now: its unit still stands empty.</summary>
        public static bool Open(Hq hq) => hq != null && Vacant(hq.Building);

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
            _candidates.Clear();
            _rows.Clear();
            for (int i = 0; i < buildings.Length; i++)
            {
                CityBuildingController b = buildings[i];
                if (b == null || b.building == null || b.building.BuildingType != Office || b.building.SpecialService != null || b.building.locatedInsidePrivateFenceIndex >= 0
                    || (int)b.building.requiredDLC != 0 || b.building.IsHamptonsHouse() || b.entranceDoors == null || b.entranceDoors.Length == 0 || b.building.totalSqm <= 0)
                {
                    continue;
                }
                _candidates.Add(b);
                _rows.Add(new HqCandidate { Street = b.building.StreetName, Number = b.building.StreetNumber, Sqm = b.building.totalSqm });
            }
            var picks = new int[Companies.Count];
            HqPick.Pick(_rows, (IList<string>)Companies, i => Taken(_candidates[i]), picks);
            // Those that move let go of their buildings first, so that one can take another's (two companies swapping, say).
            bool changed = false;
            var moving = new bool[picks.Length];
            for (int k = 0; k < picks.Length; k++)
            {
                CityBuildingController now = picks[k] >= 0 ? _candidates[picks[k]] : null;
                Hq was = Find(k);
                if ((was == null && now == null) || (was != null && ReferenceEquals(was.Building, now)))
                {
                    continue;
                }
                moving[k] = true;
                changed = true;
                if (was != null)
                {
                    Doors(was, false);
                    _all.Remove(was);
                    SpecialBuildings.Remove(IdPrefix + k);
                }
            }
            for (int k = 0; k < picks.Length; k++)
            {
                if (!moving[k] || picks[k] < 0)
                {
                    continue;
                }
                CityBuildingController now = _candidates[picks[k]];
                var hq = new Hq
                {
                    Company = k, Name = Companies[k], Building = now, Street = now.building.StreetName, Number = now.building.StreetNumber,
                    Front = JobSpots.Front(now, out Vector3 front) ? front : Door(now)
                };
                _all.Add(hq);
                SpecialBuildings.SetNamed(IdPrefix + k, hq.Street, hq.Number, SpecialKind.Other, hq.Name, "reallife_hq_card_type", () => Settings.TechSiteHq);
            }
            if (!changed)
            {
                return;
            }
            _all.Sort((a, b) => a.Company.CompareTo(b.Company));
            Stamp++;
            var said = new StringBuilder(512);
            said.Append("Tech sites: headquarters (").Append(_all.Count).Append(" of ").Append(Companies.Count).Append(", from ").Append(_candidates.Count).Append(" office buildings):");
            for (int i = 0; i < _all.Count; i++)
            {
                said.Append(' ').Append(_all[i].Name).Append(" at ").Append(_all[i].Street).Append(' ').Append(_all[i].Number).Append(i + 1 < _all.Count ? ";" : ".");
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

        // Another feature's building, or one that is not standing empty and to let.
        private static bool Taken(CityBuildingController building)
        {
            string holder = SpecialBuildings.HolderOf(building);
            return (holder != null && !holder.StartsWith(IdPrefix, StringComparison.Ordinal)) || !Vacant(building);
        }

        // Standing empty: nobody rents or owns it (the player, another player in a session, a rival), nothing of the game's is in it.
        private static bool Vacant(CityBuildingController building)
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
        // says. Set on a headquarters' doors only while it stands empty, remembered, and put back.
        private static void Doors(Hq hq, bool open)
        {
            open &= Settings.TechSiteHq && Vacant(hq.Building);
            BuildingEntranceDoor[] doors = hq.Building != null ? hq.Building.entranceDoors : null;
            if (hq.DoorsOpen == open || doors == null)
            {
                return;
            }
            if (open)
            {
                hq.DoorWas = new bool[doors.Length];
                for (int i = 0; i < doors.Length; i++)
                {
                    if (doors[i] != null)
                    {
                        hq.DoorWas[i] = doors[i].alwaysOpen;
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
                        doors[i].alwaysOpen = hq.DoorWas != null && i < hq.DoorWas.Length && hq.DoorWas[i];
                    }
                }
                hq.DoorWas = null;
            }
            hq.DoorsOpen = open;
        }

        /// <summary>The option is off, or the city is going: the doors put back and the names given back; chosen afresh when it is on again.</summary>
        public void Off()
        {
            for (int i = 0; i < _all.Count; i++)
            {
                try
                {
                    Doors(_all[i], false);
                }
                catch (Exception e)
                {
                    _logger?.Error(e);
                }
                SpecialBuildings.Remove(IdPrefix + _all[i].Company);
            }
            if (_all.Count > 0)
            {
                Stamp++;
            }
            _all.Clear();
            Picked = false;
            _waited = 0;
            _day = -1;
        }
    }
}
