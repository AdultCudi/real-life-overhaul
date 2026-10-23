using System;
using System.Collections.Generic;
using BAModAPI;
using Localizor;
using RealLifeOverhaul.Floors;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The tech companies' towers on the floors core (Floors/FloorsDirector): a held building a tower, a company a floor, on the
    /// building's own structure; the game's own panel at a tower's door and at its lift, a floor named after its company, the
    /// player's employer's marked; the game's own door goes to the employer's floor. The lobby and the rest of a company's floor
    /// are TechSites' (a lift lays them again under the dark screen). Nothing is saved.
    /// Cost: once a second a comparison of the headquarters' stamp; the towers handed over again only when they change.
    /// </summary>
    internal sealed class TowerFloors
    {
        private const int LiftClearFrames = 10;   // frames between a floor's furniture going and the next floor's: its cuts out of the walkable floor go with it

        private readonly IModLogger _logger;
        private readonly TechSites _sites;
        private readonly List<TowerHost> _hosts = new List<TowerHost>(4);
        private readonly Dictionary<int, CompanyFloor> _places = new Dictionary<int, CompanyFloor>(12);   // a company's floor, by company, kept for the session
        private readonly Dictionary<string, object> _args = new Dictionary<string, object>(32);
        private int _stamp = -1;   // the headquarters' stamp the towers were handed over at

        public TowerFloors(IModLogger logger, TechSites sites)
        {
            _logger = logger;
            _sites = sites;
        }

        /// <summary>The tower the player is in, on one of its floors; null outdoors or elsewhere.</summary>
        public Headquarters.Tower In => FloorsDirector.Visit != null && FloorsDirector.Visit.Host is TowerHost host && _hosts.Contains(host) ? host.Tower : null;

        /// <summary>The floor of <see cref="In"/> the player is on, from 1; 0 when not on one.</summary>
        public int Floor => In != null ? FloorsDirector.Visit.Floor : 0;

        /// <summary>The company of the floor the player is on, or null.</summary>
        public Headquarters.Hq Here => In != null && FloorsDirector.Visit.Place is CompanyFloor floor ? floor.Hq : null;

        /// <summary>True while a panel of the floors' is up (a tower's door or lift among them), and in the frame its button ran.</summary>
        public bool TakesKey => FloorsDirector.TakesInteractKey;

        /// <summary>Once a second: the towers handed to the floors core afresh whenever the headquarters change.</summary>
        public void Tick(Headquarters hq)
        {
            if (hq == null || !Settings.TechSiteHq || hq.Towers.Count == 0)
            {
                Off("no towers");
                return;
            }
            if (hq.Stamp == _stamp && _hosts.Count == hq.Towers.Count)
            {
                return;
            }
            _stamp = hq.Stamp;
            Sync(hq);
        }

        // Each tower a held building with its companies from floor 1; a tower whose building changed is let go and held anew.
        private void Sync(Headquarters hq)
        {
            for (int i = _hosts.Count - 1; i >= 0; i--)
            {
                TowerHost host = _hosts[i];
                Headquarters.Tower now = TowerOf(hq, host.Tower.Index);
                if (now == null || !ReferenceEquals(now, host.Tower) || !ReferenceEquals(now.Building, host.Building))
                {
                    _hosts.RemoveAt(i);
                    FloorsDirector.Let(host, "the towers were chosen again");
                }
            }
            for (int t = 0; t < hq.Towers.Count; t++)
            {
                Headquarters.Tower tower = hq.Towers[t];
                if (tower.Building == null)
                {
                    continue;
                }
                TowerHost host = HostOf(tower);
                if (host == null)
                {
                    host = new TowerHost(this, tower);
                    _hosts.Add(host);
                }
                host.Own.Clear();
                for (int f = 0; f < tower.Floors.Count; f++)
                {
                    host.Own.Add(PlaceOf(tower.Floors[f]));
                }
                FloorsDirector.Hold(host);   // again: its companies may have changed
            }
        }

        private static Headquarters.Tower TowerOf(Headquarters hq, int index)
        {
            for (int i = 0; i < hq.Towers.Count; i++)
            {
                if (hq.Towers[i].Index == index)
                {
                    return hq.Towers[i];
                }
            }
            return null;
        }

        private TowerHost HostOf(Headquarters.Tower tower)
        {
            for (int i = 0; i < _hosts.Count; i++)
            {
                if (ReferenceEquals(_hosts[i].Tower, tower))
                {
                    return _hosts[i];
                }
            }
            return null;
        }

        // A company's floor, the same object for the company all session (the floors core knows a place by it).
        private CompanyFloor PlaceOf(Headquarters.Hq hq)
        {
            if (!_places.TryGetValue(hq.Company, out CompanyFloor place))
            {
                place = new CompanyFloor(this, hq);
                _places[hq.Company] = place;
            }
            place.Hq = hq;
            return place;
        }

        /// <summary>The towers let go of by the floors core: their doors as found, their panels down, the player off their floors.</summary>
        public void Off(string why)
        {
            for (int i = _hosts.Count - 1; i >= 0; i--)
            {
                FloorsDirector.Let(_hosts[i], why);
            }
            _hosts.Clear();
            _stamp = -1;
        }

        private object Args(string key, int floor, string company)
        {
            string id = key + "#" + floor + "#" + company;
            if (!_args.TryGetValue(id, out object args))
            {
                args = new { floor = floor.ToString(), company };
                _args[id] = args;
            }
            return args;
        }

        // The floor of the company the player works for, if it is in this tower; 0 otherwise.
        private static int EmployerFloor(Headquarters.Tower tower)
        {
            string employer = TechBridges.EmployerNow();
            for (int i = 0; employer != null && i < tower.Floors.Count; i++)
            {
                if (string.Equals(tower.Floors[i].Name, employer, StringComparison.Ordinal))
                {
                    return tower.Floors[i].Floor;
                }
            }
            return 0;
        }

        /// <summary>A tower as the floors core holds it: open while it stands empty, its door taking the player to their employer's floor.</summary>
        private sealed class TowerHost : FloorHost
        {
            private readonly TowerFloors _floors;
            public readonly Headquarters.Tower Tower;

            public TowerHost(TowerFloors floors, Headquarters.Tower tower) : base(Headquarters.TowerIdOf(tower.Index), "Tech sites check (tower door)", "Tech sites check (tower lift)")
            {
                _floors = floors;
                Tower = tower;
                Building = tower.Building;
                Door = tower.Front;
                Name = tower.Name;
                Rank = FloorsRules.RankTowers;
            }

            public override bool Open() => Settings.TechSiteHq && Headquarters.Open(Tower);

            public override bool ShowsDoor(bool open) => open && Tower.Floors.Count > 0;

            public override int DefaultFloor(bool afterLoad) => HqTowers.DoorFloor(Tower.Floors.Count, EmployerFloor(Tower));

            public override void DoorLine(bool open, out string left, out string right, out Color32 colour)
            {
                left = TechText.Fill("reallife_hq_tower_door", "count", Tower.Floors.Count.ToString());
                right = "common_open".GetLocalization();
                colour = WorldPanel.Green();
            }

            public override string DoorWhat(bool open) => "at the door of " + Tower.Name;

            public override string LiftWhat(int floor) => "the lift of " + Tower.Name + " on floor " + floor;
        }

        /// <summary>A company's floor of its tower: its name on the buttons, the player's employer's marked, its lobby and floor laid by TechSites.</summary>
        private sealed class CompanyFloor : FloorPlace
        {
            private readonly TowerFloors _floors;
            public Headquarters.Hq Hq;

            public CompanyFloor(TowerFloors floors, Headquarters.Hq hq) : base(Headquarters.IdPrefix + "floor_" + hq.Company, hq.Name, true)
            {
                _floors = floors;
                Hq = hq;
                FacesIn = true;                 // the lobby is measured from where the player faces as they came in
                ClearFrames = LiftClearFrames;
                Check = "Tech sites check (tower way in)";
                LayCheck = "Tech sites check (tower floor)";
            }

            public override void Button(int floor, int part, out string key, out object args, out bool marked)
            {
                Headquarters.Tower tower = Hq.Tower;
                bool mine = tower != null && floor == EmployerFloor(tower);
                key = mine ? "reallife_hq_floor_yours" : "reallife_hq_floor";
                args = _floors.Args(key, floor, Hq.Name);
                marked = mine;
            }

            public override string LiftLine(int floor, int part) => TechText.Fill("reallife_hq_lift_here", "floor", floor.ToString(), "company", Hq.Name);

            public override void Arrive(FloorVisit visit)
            {
                Headquarters.Tower tower = Hq.Tower;
                _floors._logger?.Info("Tech sites: the player is in "
                                      + (tower != null ? tower.Name + " (" + tower.Street + " " + tower.Number + "), on floor " + visit.Floor + " of " + tower.Floors.Count : "a tower")
                                      + ", " + Hq.Name + " (" + visit.How + ").");
                _floors._sites.FloorArrived();
            }

            public override void Leave(FloorVisit visit) => _floors._sites.FloorLeft();

            public override bool Ready(FloorVisit visit) => _floors._sites.FloorReady();
        }
    }
}
