using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BAModAPI;
using RealLifeOverhaul.Uptown;
using UnityEngine;

namespace RealLifeOverhaul.Subway
{
    /// <summary>
    /// The subway in Uptown and Cedar Heights (docs/features/subway-uptown.md): three stations of the districts' own, each a
    /// copy of one of the game's entrances with the game's own station on it, joined to the game's network, so the game's
    /// travel, its fare, its map and its window of stations treat them as its own twenty. They are stood once the game has
    /// loaded its city (its late call, the loading screen still up) while their option is on and the districts stand, and at
    /// once when the option is switched on in play; they go at once when it is switched off or the districts close, never
    /// under a rider (<see cref="StationsRules.Removal"/>). Nothing is saved and nothing is sent: fixed places laid again at
    /// every load, so an old save gets them at its first load and a save loads without them. The city's people come up and
    /// go down at the entrance the player is near, by the trains' clock (StationsDirector.Riders). COST: one time comparison
    /// a frame; four looks a second while its people are out or something is pending, else one a second.
    /// </summary>
    public sealed partial class StationsDirector : MonoBehaviour
    {
        public const string FeatureId = "uptown_subway";
        public const string RootName = "RLO subway stations";   // its own object outside the mod's root: the entrances stand under it

        private static readonly FeatureGuard FrameGuard = Guard.For("uptown_subway", "Subway StationsDirector", typeof(StationsDirector));
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private const float Look = 0.25f;          // s of real time between two looks while its people are out or a removal waits
        private const float Idle = 1f;             // s between two looks otherwise: the option, the districts, the player's place
        private const float CheckAfter = 2f;       // s after standing the self-check is written: the cut in the walking meshes takes a frame or two

        public static IModLogger Logger;
        public static StationsDirector Instance { get; private set; }

        private Action<string> _onClosing;   // made once, hung on the districts' closing and taken off by this field
        private bool _late;                  // the game's own start of the city is done: its stations and its window are there
        private bool _closed;                // the feature's clean-up ran: nothing stands again before the next load
        private bool _leaving;               // a removal waits for a ride to end
        private string _leaveWhy;
        private string _stoodWhy;            // why the stations do not stand, for the tests
        private float _nextLook;
        private float _checkAt = -1f;

        /// <summary>True while the districts' stations stand.</summary>
        public static bool Standing => StationsGame.Count > 0;

        private void Awake()
        {
            Instance = this;
            StationsGame.Forget();   // nothing of an earlier city is held, whichever way it went
            _onClosing = Guard.Wrap<string>(FrameGuard, OnDistrictClosing);
            UptownLots.Closing += _onClosing;
            ScenePrewarm.AddLate("subway: the districts' own stations stood and joined to the game's list of stations", AtLoad);
        }

        private void OnDestroy()
        {
            UptownLots.Closing -= _onClosing;
            try
            {
                ReleaseRiders();
                _cast?.Dispose();
                _cast = null;
                if (!Meta.SaveTools.Dormant && !GameManager.isCitySceneBeingUnloaded)
                {
                    StationsGame.RemoveAll();   // a city that stays: out of the game's lists as they came
                }
                StationsGame.Forget();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Subway: StationsDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float now = Time.unscaledTime;
            if (now < _nextLook)
            {
                return;
            }
            bool busy = false;
            if (_late && !_closed)
            {
                Decide("its option");
                if (_checkAt >= 0f && now >= _checkAt)
                {
                    _checkAt = -1f;
                    SelfCheck();
                }
                busy = _leaving || _checkAt >= 0f || RunRiders(now);
            }
            _nextLook = now + (busy ? Look : Idle);
        }

        // ---------------------------------------------------------------- standing and going

        // The game's late call after its own start of the city, the loading screen still up: its stations are found and its window of them is made.
        private bool AtLoad()
        {
            _late = true;
            PrewarmRiders();
            Decide("the load");
            return true;
        }

        // The stations stand when wanted and go when not: asked at the load, then once a second (a few comparisons), so the option acts at once.
        private void Decide(string from)
        {
            bool wanted = StationsRules.Wanted(Settings.UptownSubway, UptownLots.Ready && !UptownLots.Closed, StationsGame.GameStations());
            if (wanted && !_leaving && !Standing && _stoodWhy == null)
            {
                StandAll(from);
            }
            else if (!wanted && (Standing || _leaving))
            {
                Leave(!Settings.UptownSubway ? "its option was switched off" : "the districts do not stand");
            }
            else if (wanted && _leaving)
            {
                _leaving = false;   // switched back on while a ride was let finish: they stay
            }
            if (!wanted && !Standing)
            {
                _stoodWhy = null;   // asked afresh the next time they are wanted
            }
        }

        private readonly List<float> _px = new List<float>(64);
        private readonly List<float> _pz = new List<float>(64);

        // Each station of the plan: the rules' own look at its ground first (every lot, the parks' and pavements' pieces, the stops the routes
        // want, the campus's gates), then the game's (StationsGame.Stand). One line for all.
        private void StandAll(string from)
        {
            IReadOnlyList<StationSite> sites = StationsRules.Sites;
            var words = new StringBuilder(256);
            int stood = 0;
            for (int i = 0; i < sites.Count; i++)
            {
                StationSite site = sites[i];
                string why;
                try
                {
                    why = Plan(site) ?? StationsGame.Stand(site, out _);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    why = "a fault while it was stood (" + e.GetType().Name + ")";
                }
                stood += why == null ? 1 : 0;
                StationsRules.Arrival(site, out float ax, out float az);
                words.Append(site.Id).Append(why == null ? " stands at (" : " NOT stood at (").Append(site.X.ToString("F1", Inv)).Append(", ").Append(site.Z.ToString("F1", Inv))
                    .Append("), its riders up at (").Append(ax.ToString("F1", Inv)).Append(", ").Append(az.ToString("F1", Inv)).Append(')')
                    .Append(why != null ? ": " + why : string.Empty).Append("; ");
            }
            _stoodWhy = stood == sites.Count ? null : words.ToString();
            if (stood == 0)
            {
                _stoodWhy = _stoodWhy ?? "none could be stood";
            }
            _checkAt = stood > 0 ? Time.unscaledTime + CheckAfter : -1f;
            _nextLook = 0f;
            Logger?.Info("Subway: " + stood + " of " + sites.Count + " stations of the districts' own stood at " + from + " and joined to the game's " + StationsGame.GameStations()
                         + ": " + words + "fare and travel the game's own.");
        }

        /// <summary>
        /// The rules' look at a station's ground as the district is laid at this load: null when it may stand, else why not. Off every lot (the
        /// plan's buildings, the open runs the player builds on, the big special places), off the carriageways, clear of the parks' and the
        /// pavements' pieces, of the avenue's lamps, of the campus's gates and of the bus stops the routes want.
        /// </summary>
        public string Plan(StationSite site)
        {
            int block = StationsRules.Holds(site, out bool pavement);
            if (block == 0 || pavement != (site.Ground == StationGround.Pavement))
            {
                return "its ground is not the " + (site.Ground == StationGround.Pavement ? "pavement" : "park's lawn") + " the plan stands it on";
            }
            string lot = StationsRules.MeetsLot(site, UptownLots.All) ?? StationsRules.MeetsLot(site, UptownLots.Places);
            if (lot != null)
            {
                return "it would stand on lot " + lot;
            }
            if (site.Ground == StationGround.Pavement)
            {
                StationsRules.PavementRoom(site, out float behind, out float toKerb);
                if (behind < StationsRules.WayPast || toKerb < 0f)
                {
                    return "it leaves " + behind.ToString("F2", Inv) + " m of pavement behind it (" + StationsRules.WayPast.ToString("F1", Inv) + " wanted)";
                }
                StationsRules.Lamps(site, _px, _pz);
                string lamp = StationsRules.MeetsPoint(site, _px, _pz, StationsRules.LampClear, "a lamp post of the avenue");
                if (lamp != null)
                {
                    return lamp + " stands on its ground";
                }
            }
            string piece = Pieces(site);
            if (piece != null)
            {
                return piece + " stands on its ground";
            }
            StreetsRules.StopPoles(_px, _pz);
            string stop = StationsRules.MeetsPoint(site, _px, _pz, StationsRules.StopClear, "a bus stop the routes want");
            return stop != null ? stop + " is nearer than " + StationsRules.StopClear.ToString("F0", Inv) + " m" : null;
        }

        private readonly List<TransitPlan.Route> _routes = new List<TransitPlan.Route>(4);
        private readonly List<StreetPlace> _furniture = new List<StreetPlace>(768);
        private readonly List<ParkPiece> _parkPieces = new List<ParkPiece>(160);
        private readonly List<Schools.CampusCrowd> _crowds = new List<Schools.CampusCrowd>(4);

        // The pieces the district stands by rule round a station: the pavements' furniture, its park's trees, benches, bins and lamps, the campus's gates' crowds.
        private string Pieces(StationSite site)
        {
            StreetsRules.Routes(_routes);
            StreetsRules.Furniture(_routes, _furniture);
            _px.Clear();
            _pz.Clear();
            for (int i = 0; i < _furniture.Count; i++)
            {
                _px.Add(_furniture[i].X);
                _pz.Add(_furniture[i].Z);
            }
            string met = StationsRules.MeetsPoint(site, _px, _pz, StationsRules.PieceClear, "a piece of the pavement's furniture");
            if (met != null)
            {
                return met;
            }
            if (ParksRules.IsPark(site.Block))
            {
                ParksRules.Pieces(site.Block, _parkPieces);
                _px.Clear();
                _pz.Clear();
                for (int i = 0; i < _parkPieces.Count; i++)
                {
                    _px.Add(_parkPieces[i].X);
                    _pz.Add(_parkPieces[i].Z);
                }
                met = StationsRules.MeetsPoint(site, _px, _pz, StationsRules.PieceClear, "a piece of the park");
                if (met != null)
                {
                    return met;
                }
            }
            if (site.Block == LayoutRules.CampusBlock)
            {
                _crowds.Clear();
                Schools.SchoolGrounds.Crowds(_crowds);
                for (int i = 0; i < _crowds.Count; i++)
                {
                    Schools.CampusCrowd crowd = _crowds[i];
                    bool across = Mathf.Abs(Mathf.DeltaAngle(crowd.Yaw, 90f)) < 1f || Mathf.Abs(Mathf.DeltaAngle(crowd.Yaw, 270f)) < 1f;
                    float halfX = (across ? crowd.Depth : crowd.Width) * 0.5f, halfZ = (across ? crowd.Width : crowd.Depth) * 0.5f;
                    met = StationsRules.MeetsBox(site, crowd.X - halfX, crowd.Z - halfZ, crowd.X + halfX, crowd.Z + halfZ, StationsRules.LotClear, "the crowd's ground at " + crowd.Name);
                    if (met != null)
                    {
                        return met;
                    }
                }
            }
            return null;
        }

        // The stations go: at once, but never under a rider, and the game's map is shut first when it lists stations from one of them.
        private void Leave(string why)
        {
            switch (StationsRules.Removal(SubwaySystem.IsRiding, StationsGame.RideTouches(), StationsGame.PickingFromOwn()))
            {
                case StationRemoval.AfterRide:
                    if (!_leaving)
                    {
                        Logger?.Info("Subway: the districts' stations go once the ride under way has brought its rider up (" + why + ").");
                    }
                    _leaving = true;
                    _leaveWhy = why;
                    return;
                case StationRemoval.MapFirst:
                    StationsGame.ShutMap();
                    break;
            }
            _leaving = false;
            _leaveWhy = null;
            _checkAt = -1f;
            ReleaseRiders();
            int gone = StationsGame.RemoveAll();
            _stoodWhy = null;
            if (gone > 0)
            {
                Logger?.Info("Subway: " + gone + " station(s) of the districts' own taken out of the game's list, its window and its map, and away (" + why + "); "
                             + StationsGame.GameStations() + " of the game's own stand as they did.");
            }
        }

        private void OnDistrictClosing(string why)
        {
            Leave("the districts close: " + why);
        }

        /// <summary>
        /// The feature's clean-up (the options page, five faults in a row, the save tools, another city): the stations go and stay away until
        /// the next load. Safe when none stands.
        /// </summary>
        public void Close(string why)
        {
            _closed = true;
            _leaving = false;
            ReleaseRiders();
            if (SubwaySystem.IsRiding && StationsGame.RideTouches())
            {
                Logger?.Info("Subway: closed until the next load (" + why + "); a ride is under way at one of the districts' stations, which stay until the city goes.");
                return;
            }
            if (StationsGame.PickingFromOwn())
            {
                StationsGame.ShutMap();
            }
            int gone = StationsGame.RemoveAll();
            Logger?.Info("Subway: closed until the next load (" + why + "); " + gone + " station(s) taken away.");
        }

        // ---------------------------------------------------------------- the self-check

        // Once, two seconds after they are stood (the cut is in by then): what stands against what was wanted.
        private void SelfCheck()
        {
            if (!Meta.Dev.Verbose && StationsGame.Count == StationsRules.Sites.Count)
            {
                if (StationsGame.Check(out string quiet))
                {
                    return;   // all as wanted: said only with Developer diagnostics on
                }
                Logger?.Warn("Subway check (stations): " + quiet);
                return;
            }
            StationsGame.Check(out string line);
            Logger?.Info("Subway check (stations): " + line);
        }
    }
}
