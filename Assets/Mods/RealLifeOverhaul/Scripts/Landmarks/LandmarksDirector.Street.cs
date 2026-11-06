using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.Uptown;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Landmarks
{
    /// <summary>
    /// The director's part in the street: the stadium's pieces and the plaza and forecourt round the tower and the mall,
    /// stood behind the loading screen at a load or out of view in play; the crowds of the game's own pedestrians at an
    /// opening and on a match day; the traffic index of the district's buildings near them; the game's own panel at a
    /// landmark's front (join an opening, the stadium's fixtures); the landmarks' row on the city map; and the opening
    /// itself. Once a real second, a few comparisons; anything heavier only when something changes.
    /// </summary>
    public sealed partial class LandmarksDirector
    {
        private const float PanelReach = 4f;     // m from a landmark's front the game's panel is offered
        private const int JoinPoints = 2;        // Standing for joining an opening's crowd: the press has the player's face in the crowd
        private static readonly Color MapColour = new Color(0.10f, 0.42f, 0.46f);   // Uptown's own green blue

        private readonly GameObject[] _pieces = new GameObject[(int)LandmarkKind.Count];
        private readonly List<LandmarkPiece> _layout = new List<LandmarkPiece>(64);
        private readonly List<CrowdSquare> _squares = new List<CrowdSquare>(4);
        private readonly List<StationaryAiSpawner> _crowds = new List<StationaryAiSpawner>(6);
        private readonly List<MadeBuilding> _standing = new List<MadeBuilding>(64);
        private readonly List<MapPlace> _places = new List<MapPlace>(3);
        private long _crowdKey = -1;               // what the crowds standing are for: which windows are open now
        private static int _trafficFor = -1;       // what the traffic indices were last set for
        private static bool _streetStale = true;   // something changed that the street shows
        private DoorPanel _panel;
        private Action _join;
        private int _panelFor = -1;
        private int _panelLines = -1;
        private MapPlaces.Category _map;
        private int _mapFor = int.MinValue;

        // ---------------------------------------------------------------- at a load

        // The game's late call, the loading screen still up: what is built stands now, and the district's buildings get their traffic.
        private bool AtLoad()
        {
            try
            {
                if (!Settings.Landmarks || !DistrictBuildings.Lives)
                {
                    return true;
                }
                if (!PropLibrary.Settled)
                {
                    return false;   // the props bundles are still opening: tried again in the late call's next round
                }
                if (!_loaded || !ReferenceEquals(_save, SaveGameManager.Current))
                {
                    Load();
                }
                if (_unread)
                {
                    return true;
                }
                RefreshSites();
                Looks(true);
                Traffic(true);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            return true;
        }

        // ---------------------------------------------------------------- once a second

        private void Street()
        {
            if (!DistrictBuildings.Lives || !UptownLots.Ready)
            {
                if (_crowds.Count > 0 || _panelFor >= 0)
                {
                    Crowds(-1L);
                    HidePanel("the district is not standing");
                }
                return;
            }
            SitesToday();
            if (_streetStale)
            {
                _streetStale = false;
                Looks(false);
            }
            int day = LandmarksGame.Day, hour = LandmarksGame.Hour;
            Taking();
            Pending();
            Crowds(CrowdKey(day, hour));
            Traffic(false);
            Panels(day, hour);
            Map();
        }

        private float _pendingNext;
        private const float PendingEvery = 10f;   // s of real time between two asks that a landmark built be handed to the district

        // A landmark built but not the district's yet (its design was being measured, the district not standing): asked again now and then, not only at the next day.
        private void Pending()
        {
            float now = Time.unscaledTime;
            if (now < _pendingNext)
            {
                return;
            }
            _pendingNext = now + PendingEvery;
            for (int k = 0; k < _state.Files.Length; k++)
            {
                LandmarkFile f = _state.Files[k];
                if (f.Built && !f.InDistrict && SiteKnown[k])
                {
                    ToDistrict(f, Sites[k]);
                    if (f.InDistrict)
                    {
                        _streetStale = true;
                        Written();
                    }
                }
            }
        }

        // ---------------------------------------------------------------- the pieces

        // Each built landmark's pieces stand once a city: at a load at once, in play only while its ground is out of view.
        private void Looks(bool now)
        {
            if (!PropLibrary.Settled)
            {
                _streetStale = true;
                return;
            }
            for (int k = 0; k < _pieces.Length; k++)
            {
                LandmarkFile f = _state.Files[k];
                bool want = SiteKnown[k] && f.Built && (f.Kind == LandmarkKind.Stadium || f.Width > 0f);
                if (!want)
                {
                    if (_pieces[k] != null)
                    {
                        LandmarksGame.Take(ref _pieces[k]);
                    }
                    continue;
                }
                if (_pieces[k] != null)
                {
                    continue;
                }
                LandmarkSite site = Sites[k];
                if (!now && LandmarksGame.InView(site.Ground, f.Kind == LandmarkKind.Stadium ? LandmarkRules.FloodlightHeight : 8f))
                {
                    _streetStale = true;   // tried again at the next second it is out of view
                    continue;
                }
                if (f.Kind == LandmarkKind.Stadium)
                {
                    LandmarkRules.Stadium(site.Ground, _layout);
                }
                else
                {
                    LandmarkRules.Plaza(site.AnchorGround, f.Width, f.Depth, true, _layout);
                }
                _pieces[k] = LandmarksGame.Stand(LandmarkRules.Name(f.Kind), _layout, UptownLots.GroundY, out int modelled, out int plain);
                int solid = 0;
                for (int i = 0; i < _layout.Count; i++)
                {
                    solid += _layout[i].Solid ? 1 : 0;
                }
                bool pass = _pieces[k] != null && modelled + plain > 0;
                string line = "Landmarks check (pieces, " + LandmarkRules.Name(f.Kind) + "): " + modelled + " of " + _layout.Count + " piece(s) the bundles' models, " + plain
                              + " plain shapes, " + solid + " in the way and cut from the walking meshes, on " + Mathf.RoundToInt(site.Ground.Width) + " by "
                              + Mathf.RoundToInt(site.Ground.Depth) + " m of block " + site.Block + (now ? ", behind the loading screen" : ", out of view") + ": " + (pass ? "PASS" : "FAIL");
                if (!pass)
                {
                    Logger?.Warn(line);
                }
                else if (Verbose)
                {
                    Logger?.Info(line);
                }
            }
        }

        // ---------------------------------------------------------------- the crowds

        // Which crowds stand now, as one number: an opening's (each landmark's bit), a match day's (the stadium's), none (0).
        private long CrowdKey(int day, int hour)
        {
            long key = 0;
            for (int k = 0; k < _state.Files.Length; k++)
            {
                LandmarkFile f = _state.Files[k];
                if (!SiteKnown[k])
                {
                    continue;
                }
                if (f.Stage == LandmarkStage.Finished && day == f.Opening
                    && (_testOpening == k || hour >= LandmarkRules.OpeningFrom && hour < LandmarkRules.OpeningTo))
                {
                    key |= 1L << k;
                }
                if (f.Kind == LandmarkKind.Stadium && f.Stage == LandmarkStage.Opened
                    && (_testMatch == day || MatchToday(f, day) && hour >= LandmarkRules.CrowdFrom && hour < LandmarkRules.CrowdTo))
                {
                    key |= 1L << 8;
                }
            }
            return key == 0 ? 0 : key + ((long)day << 16);
        }

        private bool MatchToday(LandmarkFile stadium, int day) => LandmarkRules.MatchDay(day, stadium.Opening);

        // The crowds stood afresh when what they are for changes: the game's own pedestrians come to each square and stand there.
        private void Crowds(long key)
        {
            if (key == _crowdKey)
            {
                return;
            }
            _crowdKey = key;
            for (int i = 0; i < _crowds.Count; i++)
            {
                StationaryAiSpawner zone = _crowds[i];
                LandmarksGame.NoCrowd(ref zone);
            }
            _crowds.Clear();
            if (key <= 0)
            {
                return;
            }
            int made = 0, asked = 0;
            string why = null;
            for (int k = 0; k < _state.Files.Length; k++)
            {
                bool opening = (key & (1L << k)) != 0, match = _state.Files[k].Kind == LandmarkKind.Stadium && (key & (1L << 8)) != 0;
                if (!opening && !match)
                {
                    continue;
                }
                LandmarkRules.Crowds(Sites[k].Ground, match, _squares);
                for (int i = 0; i < _squares.Count; i++)
                {
                    asked++;
                    StationaryAiSpawner zone = LandmarksGame.Crowd(_squares[i].X, _squares[i].Z, 0f, _squares[i].Width, _squares[i].Depth, out string not);
                    if (zone != null)
                    {
                        _crowds.Add(zone);
                        made++;
                    }
                    else
                    {
                        why = not;
                    }
                }
            }
            if (Verbose || made < asked)
            {
                Logger?.Info("Landmarks check (crowds): " + made + " of " + asked + " square(s) of pavement the game's own pedestrians come to" + (why != null ? " (" + why + ")" : string.Empty)
                             + ": " + (made == asked ? "PASS" : "FAIL") + ".");
            }
        }

        // ---------------------------------------------------------------- the traffic

        // The district's buildings' traffic index as the game reads it: each lot's own with the landmarks' lift, and on a match day the crowds' near the stadium.
        private void Traffic(bool force)
        {
            int day = LandmarksGame.Day;
            bool match = false;
            LandmarkFile stadium = _state.Of(LandmarkKind.Stadium);
            if (SiteKnown[(int)LandmarkKind.Stadium] && stadium.Stage == LandmarkStage.Opened)
            {
                match = MatchToday(stadium, day) || _testMatch == day;
            }
            int key = _builtStamp * 2 + (match ? 1 : 0) + (Settings.Landmarks ? 0 : 1 << 30);
            if (!force && key == _trafficFor)
            {
                return;
            }
            _trafficFor = key;
            _standing.Clear();
            DistrictBuildings.Standing(_standing);
            SiteRect ground = SiteKnown[(int)LandmarkKind.Stadium] ? Sites[(int)LandmarkKind.Stadium].Ground : default(SiteRect);
            int lifted = 0;
            for (int i = 0; i < _standing.Count; i++)
            {
                MadeBuilding made = _standing[i];
                if (made == null || made.Spec == null)
                {
                    continue;
                }
                int own = DistrictBuildings.TrafficOf(made.Spec.Id);
                if (own < 0)
                {
                    continue;
                }
                int extra = match ? LandmarkRules.MatchDayLift(made.X, made.Z, ground) : 0;
                if (LandmarksGame.Traffic(made, Math.Min(100, own + extra)) && (extra > 0 || LiftOf(made.Spec.Id) > 0))
                {
                    lifted++;
                }
            }
            _standing.Clear();
            if (Verbose)
            {
                Logger?.Info("Landmarks check (traffic): " + lifted + " of the district's building(s) lifted by the landmarks" + (match ? " and today's match" : string.Empty) + ": PASS.");
            }
        }

        // ---------------------------------------------------------------- the panel at a landmark's front

        // The game's own panel at the front of the landmark the player stands at: join an opening; the stadium's fixtures.
        private void Panels(int day, int hour)
        {
            if (!LandmarksGame.Outdoors || LandmarksGame.InVehicle)
            {
                HidePanel("the player is not on foot outside");
                return;
            }
            Vector3 player = LandmarksGame.Player;
            int at = -1;
            for (int k = 0; k < _state.Files.Length && at < 0; k++)
            {
                if (!SiteKnown[k] || _state.Files[k].Block <= 0)
                {
                    continue;
                }
                LandmarkRules.Front(Sites[k].Ground, out float x, out float z);
                float dx = player.x - x, dz = player.z - z;
                if (dx * dx + dz * dz <= PanelReach * PanelReach && Offered(_state.Files[k], day, hour))
                {
                    at = k;
                }
            }
            if (at < 0)
            {
                HidePanel("no landmark's front here");
                return;
            }
            LandmarkFile f = _state.Files[at];
            bool opening = Opening(f, day, hour);
            int lines = at * 10 + (opening ? 1 : 0) + (f.Joined ? 2 : 0);
            if (_panel == null)
            {
                _panel = new DoorPanel(Logger, "Landmarks check (panel)");
                _join = _panel.Press(() => Join(_panelFor));
            }
            if (_panelFor != at || _panelLines != lines || !_panel.Up)
            {
                _panelFor = at;
                _panelLines = lines;
                WorldPanel panel = _panel.Panel;
                if (opening)
                {
                    panel.Lines(LandmarksText.Fill(LandmarksText.PanelOpeningTitle, "name", NameOf(f.Kind)), LandmarksText.T(LandmarksText.PanelOpeningLine), null, WorldPanel.White());
                    panel.Button(0, f.Joined ? LandmarksText.PanelJoined : LandmarksText.PanelJoin, _join, !f.Joined, !f.Joined);
                }
                else
                {
                    panel.Lines(LandmarksText.Fill(LandmarksText.PanelStadiumTitle, "name", NameOf(f.Kind), "team", LandmarksText.T(LandmarksText.Club)), Fixture(f, day), Result(f, day),
                        WorldPanel.White());
                    panel.Button(0, null, null, false, false);
                }
                panel.Button(1, null, null, false, false);
                panel.Button(2, null, null, false, false);
                panel.Button(3, null, null, false, false);
                panel.Follow = null;
            }
            LandmarkRules.Front(Sites[at].Ground, out float fx, out float fz);
            _panel.Show(new Vector3(fx, UptownLots.GroundY, fz), player, 2.4f, () => "the front of " + LandmarkRules.Name(f.Kind));
        }

        // A panel is offered at an opening the player's company did not build (its own ribbon is the construction game's), and at an open stadium.
        private bool Offered(LandmarkFile f, int day, int hour)
        {
            return Opening(f, day, hour) || (f.Kind == LandmarkKind.Stadium && f.Stage == LandmarkStage.Opened);
        }

        private bool Opening(LandmarkFile f, int day, int hour)
        {
            return f.Stage == LandmarkStage.Finished && f.Builder != LandmarkBuilder.Player && day == f.Opening
                   && (_testOpening == (int)f.Kind || hour >= LandmarkRules.OpeningFrom && hour < LandmarkRules.OpeningTo);
        }

        private void HidePanel(string why)
        {
            _panel?.Hide(why);
            _panelFor = -1;
        }

        // The player joins an opening's crowd: the press has their face among the city's; Standing, once.
        private void Join(int k)
        {
            if (k < 0 || k >= _state.Files.Length)
            {
                return;
            }
            LandmarkFile f = _state.Files[k];
            if (f.Joined || f.Stage != LandmarkStage.Finished)
            {
                return;
            }
            f.Joined = true;
            _panelLines = -1;
            LandmarkBridges.Standing?.Invoke(JoinPoints);
            Notifications.Show(NotificationType.Success, LandmarksText.NoticeJoined, new Dictionary<string, string>(1) { { "name", NameOf(f.Kind) } }, 10f);
            Written();
            Logger?.Info("Landmarks check (joined): the player joined the opening of " + LandmarkRules.Name(f.Kind) + " on day " + f.Opening + ", Standing " + JoinPoints + ": PASS.");
        }

        // The opening held: the landmark open to the city from now on.
        private void Open(LandmarkFile f, bool atOnce)
        {
            if (f.Stage != LandmarkStage.Finished)
            {
                return;
            }
            f.Stage = LandmarkStage.Opened;
            _builtStamp++;
            _crowdKey = -2;
            _mapFor = int.MinValue;
            Logger?.Info("Landmarks: " + LandmarkRules.Name(f.Kind) + " opened on day " + f.Opening + (f.Joined ? ", the player in the crowd" : string.Empty)
                         + (atOnce ? " (a test)" : string.Empty) + ".");
        }

        // The hour's look: an opening held once its ceremony's hours are over.
        private void HourWork(int day, int hour)
        {
            for (int k = 0; k < _state.Files.Length; k++)
            {
                LandmarkFile f = _state.Files[k];
                if (f.Stage == LandmarkStage.Finished && (day > f.Opening || (day == f.Opening && hour >= LandmarkRules.OpeningTo && _testOpening != k)))
                {
                    Open(f, false);
                    Written();
                }
            }
        }

        // ---------------------------------------------------------------- the city map

        // The landmarks' row in the map's filters, a marker at each one's front with its name; read again when a stage moves.
        private void Map()
        {
            int stamp = _builtStamp * 8;
            for (int k = 0; k < _state.Files.Length; k++)
            {
                stamp += SiteKnown[k] && _state.Files[k].Block > 0 ? 1 << k : 0;
            }
            if (stamp == _mapFor)
            {
                return;
            }
            _mapFor = stamp;
            _places.Clear();
            for (int k = 0; k < _state.Files.Length; k++)
            {
                if (!SiteKnown[k] || _state.Files[k].Block <= 0)
                {
                    continue;
                }
                LandmarkRules.Front(Sites[k].Ground, out float x, out float z);
                _places.Add(new MapPlace(new Vector3(x, UptownLots.GroundY, z), NameOf(_state.Files[k].Kind)));
            }
            if (_map == null && _places.Count > 0)
            {
                _map = MapPlaces.Register("landmarks", LandmarksText.MapRow, MapColour, MapIcon.Star, true);
                _map.Available = () => Settings.Landmarks && DistrictBuildings.Lives;
            }
            _map?.Set(_places);
        }

        // ---------------------------------------------------------------- leaving

        /// <summary>Everything the landmarks stand in the street put away: their pieces, their crowds, the panel, the map's markers; the district's traffic as its lots' own.</summary>
        private void LeaveStreet(string why)
        {
            for (int k = 0; k < _pieces.Length; k++)
            {
                LandmarksGame.Take(ref _pieces[k]);
            }
            Crowds(-1L);
            _crowdKey = -1;
            HidePanel(why);
            _places.Clear();
            _map?.Set(_places);
            _mapFor = int.MinValue;
            _streetStale = true;
            if (!LandmarksGame.Loading && DistrictBuildings.Lives && !GameManager.isCitySceneBeingUnloaded)
            {
                _builtStamp++;
                Traffic(true);   // the option off: each lot's own traffic again (the lift reads the option)
            }
        }
    }
}
