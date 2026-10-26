using System.Collections.Generic;
using RealLifeOverhaul.Floors;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.Uptown;
using UnityEngine;

namespace RealLifeOverhaul.Schools
{
    /// <summary>
    /// The director's part for the campus as it stands: the hall and the junior block asked of the copy engine while the city
    /// loads, the main school's classrooms held and its pieces stood behind the loading screen, the copies cut out of the
    /// walking meshes once they stand, the families at the gates in the school run's hours, its row on the city map and the
    /// district's traffic round it. On a load, a second's look or a day's pass; never a frame.
    /// </summary>
    public sealed partial class SchoolsDirector
    {
        private const float CrowdNear = 300f;   // m from the campus within which the families gather at its gates: the game's pedestrians are wanted elsewhere further off
        private static readonly Color MapColour = new Color(0.30f, 0.52f, 0.30f);   // the playing field's green

        private readonly List<CopyHandle> _copies = new List<CopyHandle>(2);   // one slot a plot, in Blocks' order; null while the copy engine could not be asked
        private readonly List<bool> _told = new List<bool>(2);                 // the slot's outcome is in Player.log
        private readonly List<CampusBlockPlot> _plots = new List<CampusBlockPlot>(2);
        private readonly List<CampusPiece> _pieces = new List<CampusPiece>(480);
        private readonly List<CampusBox> _paths = new List<CampusBox>(12);
        private readonly List<CampusFence> _runs = new List<CampusFence>(8);
        private readonly List<CampusCrowd> _crowdPlaces = new List<CampusCrowd>(9);
        private readonly StationaryAiSpawner[] _crowds = new StationaryAiSpawner[9];   // one slot a square of SchoolGrounds.Crowds, in its order
        private readonly List<MapPlace> _places = new List<MapPlace>(1);
        private GameObject _pieceRoot;
        private SchoolHost _host;
        private SchoolFloors _floors;
        private bool _held;
        private bool _campusStood;          // the main school stood at this load and its pieces are out
        private int _cutCopies;
        private CrowdWhen _crowdMoments;   // who is out now, as the squares were last made
        private bool _crowded;             // the squares were made at least once since the campus stood
        private MapPlaces.Category _map;
        private int _liftLevel = -1;
        private const float ReachAfter = 4f;   // s after the campus stands the walking meshes are asked about its routes, once (the ground is grown by then)
        private float _reachAt;                // when (unscaled time); 0 once done
        private readonly List<SchoolGrounds.CampusRoute> _routes = new List<SchoolGrounds.CampusRoute>(9);

        /// <summary>Pieces stood at this load, and the game's kinds that were not found (null for none).</summary>
        public static int PiecesStood { get; private set; }
        public static string PiecesMissing { get; private set; }

        /// <summary>True when the main school stood at this load and its campus is out.</summary>
        public static bool CampusStands => Instance != null && Instance._campusStood;

        // Behind the loading screen, once the main school stands: the campus's two other buildings asked of the copy engine. Not in
        // Awake: this director is made before the copy engine's (the main school must be declared first), so there the engine does
        // not take requests yet and answers with none (Player.log, 26 Oct 2026: "asked of the copy engine (0)"). The engine stands what
        // is asked of it in the late call that follows this one, the loading screen still up.
        private void Ask()
        {
            _copies.Clear();
            _told.Clear();
            if (!Wanted())
            {
                return;
            }
            SchoolGrounds.Blocks(_plots);
            for (int i = 0; i < _plots.Count; i++)
            {
                bool own = LookStands(i + 1);     // the model stands: the copy engine is not asked (it is only the fallback)
                _copies.Add(own ? null : SchoolsGame.Plot(_plots[i]));
                _told.Add(own);
            }
        }

        private int Asked()
        {
            int asked = 0;
            for (int i = 0; i < _copies.Count; i++)
            {
                asked += _copies[i] != null ? 1 : 0;
            }
            return asked;
        }

        // Once a second: a building the copy engine could not be asked for is asked again as soon as it takes requests (its option
        // switched on), and each one's outcome, the design it wears or why it was not made, is written once.
        private void Blocks()
        {
            for (int i = 0; i < _copies.Count && i < _plots.Count; i++)
            {
                CopyHandle handle = _copies[i];
                if (LookStands(i + 1))
                {
                    continue;
                }
                if (handle == null && SchoolsGame.CopiesAvailable)
                {
                    handle = _copies[i] = SchoolsGame.Plot(_plots[i]);
                    _told[i] = false;
                }
                if (_told[i] || (handle != null && !handle.Ready && !handle.Gone))
                {
                    continue;
                }
                _told[i] = true;
                string line = SchoolGrounds.BlockLine(_plots[i].Name, handle != null, handle != null && handle.Ready, handle?.Wears,
                    handle != null ? handle.Why : "its option, New buildings, is off or its director is not made");
                Logger?.Info("Schools: " + line + ".");
            }
        }

        // Behind the loading screen, once the made buildings are made: the main school's classrooms held and the campus's pieces
        // stood, or everything given back with one line when the main school stood down.
        private bool AtLoad()
        {
            _campusStood = false;
            if (!Wanted())
            {
                DropCopies();
                LeaveLook();
                return true;
            }
            MadeBuilding main = SchoolsGame.Main;
            if (main == null || main.Controller == null)
            {
                DropCopies();
                LeaveLook();
                Logger?.Info("Schools: Birchwood School does not stand at this load: its main school stood down (the made buildings' line says why); the market runs on.");
                return true;
            }
            CampusLook.FindMaterials();      // the game's brick, roof, glass and chain-link, by name, once and only here: behind the loading screen
            // The campus's own look from the second props bundle where it is read (else the copy engine's designs, the bundle waited for a moment
            // at most); the main school's walking-mesh cut follows the shape that stands.
            bool cut = false;
            if (BundlesSettled)
            {
                _lookWait = false;
                cut = StandLooks(main) || SchoolsGame.Cut(main);
                Ask();
            }
            else
            {
                _lookWait = true;
                _lookWaitEnd = Time.unscaledTime + LookWaitSeconds;
            }
            PiecesMissing = SchoolsGame.FindTemplates();
            SchoolGrounds.Pieces(_pieces, _runs, main.DoorZ);
            SchoolGrounds.Paths(_paths, main.DoorZ);
            _pieceRoot = SchoolsGame.Stand(_pieces, _runs, _paths, out int stood, out int missed, out int paved, out int nets);
            PiecesStood = stood;
            _campusStood = true;
            _reachAt = Time.unscaledTime + ReachAfter;
            Hold(main);
            FileCard(main);
            Logger?.Info("Schools: Birchwood School's campus stood at 1 Birchwood Green (the main school wears " + main.Design + ", " + (cut ? "cut out of the walking meshes" : "not cut") + "); "
                         + stood + " of " + _pieces.Count + " pieces of the game's own" + (missed > 0 ? " (" + missed + " not stood; not found: " + (PiecesMissing ?? "the pitch") + ")" : string.Empty)
                         + ", " + _runs.Count + " runs of fence, " + paved + " square metres of paths, " + nets + " hoop net(s), " + SchoolGrounds.CrowdCount + " squares for people; "
                         + SchoolGrounds.Words(_pieces) + "; the hall and the junior block "
                         + (_lookWait ? "wait for the props bundles"
                             : LookStands(1) || LookStands(2) ? "from the models" + (Asked() > 0 ? " and asked of the copy engine (" + Asked() + ")" : string.Empty)
                             : "asked of the copy engine (" + Asked() + " of " + _plots.Count + ")") + ".");
            return true;
        }

        private void DropCopies()
        {
            for (int i = 0; i < _copies.Count; i++)
            {
                SchoolsGame.Remove(_copies[i]);
            }
            _copies.Clear();
            _told.Clear();
            _cutCopies = 0;
        }

        // Once a second while running: the copies cut once they stand, the classrooms held while the main school is shown, the
        // families at the gates in the school run's hours, the map's row.
        private void Campus()
        {
            if (!_campusStood)
            {
                return;
            }
            LookTick();
            ReachTick();
            if (_cutCopies < _copies.Count)
            {
                int cut = 0;
                for (int i = 0; i < _copies.Count; i++)
                {
                    cut += LookStands(i + 1) || SchoolsGame.CutCopy(_copies[i]) ? 1 : 0;    // a model is carved when it stands
                }
                _cutCopies = cut;
            }
            Blocks();
            MadeBuilding main = SchoolsGame.Main;
            if (main != null && main.Shown != _held)
            {
                if (main.Shown)
                {
                    Hold(main);
                }
                else
                {
                    Let("the main school is hidden");
                }
            }
            Crowds();
            Map();
        }

        // The main school on the game's building card as a school: its name and kind in the mod's one card path (SpecialBuildings, as the university's and
        // the precincts'), open or closed by the school's own hours and who runs it in the card's own lines. Once a load; the card asks once a second.
        private void FileCard(MadeBuilding main)
        {
            if (main == null || main.Controller == null
                || !SpecialBuildings.Set(SchoolsGame.MainId, main.Controller, SpecialKind.Campus, SchoolsText.Campus, SchoolsText.CardType, () => Settings.Schools))
            {
                Logger?.Info("Schools: the main school is not on the game's card as a school (the special buildings' book refused it).");
                return;
            }
            SpecialBuildings.Facts(SchoolsGame.MainId, SayCard);
            if (Verbose)
            {
                Logger?.Info("Schools check (card): the game's building card at 1 Birchwood Green reads " + CardWords(SchoolsGame.Day, SchoolsGame.Hour, _state.Campus.Owner) + ": PASS.");
            }
        }

        // What the card says: filled each time the card is written.
        private static void SayCard(SpecialFacts facts)
        {
            int day = SchoolsGame.Day, hour = SchoolsGame.Hour;
            bool open = SchoolRules.Open(day, hour);
            facts.State = open ? CardState.Open : CardState.Closed;
            facts.Hours = SchoolRules.HoursToChange(day, hour, out int at);
            facts.AtHour = at;
            facts.CanEnter = open;
            facts.RunLine = SchoolsText.CardRun(_state.Campus.Owner);
        }

        /// <summary>The card as words for the log: the name, the kind, the state by the hours and who runs it.</summary>
        internal static string CardWords(int day, int hour, int owner)
        {
            return "'" + SchoolsText.T(SchoolsText.Campus) + "', '" + SchoolsText.T(SchoolsText.CardType) + "', " + (SchoolRules.Open(day, hour) ? "open" : "closed")
                   + " (" + SchoolRules.HoursToChange(day, hour, out int at) + " h to " + at.ToString("00") + ":00 at day " + day + ", " + hour.ToString("00") + ":00), " + SchoolsText.CardRun(owner);
        }

        // Once a load, a few seconds after the campus stands: can people walk each route of SchoolGrounds.Routes on the pedestrians' walking mesh, and the
        // player on theirs (the gates join the campus to the pavements; the fence, the buildings and the pieces are cut out of both). One line in Player.log.
        private void ReachTick()
        {
            if (_reachAt <= 0f || Time.unscaledTime < _reachAt)
            {
                return;
            }
            _reachAt = 0f;
            MadeBuilding main = SchoolsGame.Main;
            SchoolGrounds.Routes(_routes, main != null ? main.DoorX : float.NaN, main != null ? main.DoorZ : float.NaN);
            int people = 0, player = 0;
            string failed = null;
            for (int i = 0; i < _routes.Count; i++)
            {
                SchoolGrounds.CampusRoute r = _routes[i];
                var from = new Vector3(r.FromX, UptownLots.GroundY, r.FromZ);
                var to = new Vector3(r.ToX, UptownLots.GroundY, r.ToZ);
                bool a = SchoolsGame.Reaches(from, to, SchoolsGame.NpcAgent, out string whyPeople), b = SchoolsGame.Reaches(from, to, SchoolsGame.PlayerAgent, out string whyPlayer);
                people += a ? 1 : 0;
                player += b ? 1 : 0;
                if (!a || !b)
                {
                    failed = (failed == null ? string.Empty : failed + "; ") + r.Name + " (" + (!a ? "people: " + whyPeople : string.Empty)
                             + (!a && !b ? ", " : string.Empty) + (!b ? "the player: " + whyPlayer : string.Empty) + ")";
                }
            }
            Logger?.Info("Schools check (campus reach): people can walk " + people + " of " + _routes.Count
                         + " routes from the gates to the main door, the yards, the court, the plaza, the hall's and the junior block's ends and the field, the player "
                         + player + (failed == null ? ": PASS." : "; not: " + failed + "."));
        }

        // The main school's classrooms handed to the floors core: the game's school on its three floors.
        private void Hold(MadeBuilding main)
        {
            if (_held || main == null || main.Controller == null || !main.Shown)
            {
                return;
            }
            _host = _host ?? new SchoolHost();
            _floors = _floors ?? new SchoolFloors();
            _host.Building = main.Controller;
            float turn = main.DoorFacing * Mathf.Deg2Rad;
            _host.Door = new Vector3(main.DoorX + Mathf.Sin(turn) * 1.2f, main.DoorY, main.DoorZ + Mathf.Cos(turn) * 1.2f);
            _host.Name = SchoolsText.T(SchoolsText.Campus);
            _host.Own.Clear();
            _host.Own.Add(_floors);
            FloorsDirector.Hold(_host);
            _held = true;
        }

        private void Let(string why)
        {
            if (!_held)
            {
                return;
            }
            _held = false;
            if (_host != null)
            {
                FloorsDirector.Let(_host, why);
            }
        }

        // The people of the school day, the game's own pedestrians in squares of ground: the families before the gates and the pupils at the door in the
        // school run's hours and on an open day, the courtyard, court, green and yards at break and lunch, the court and the field after classes; none at night
        // or at the weekend, and only while the player is near. The squares are made and taken away as the moments change (once a second, a few comparisons).
        private void Crowds()
        {
            CrowdWhen want = SchoolRules.CrowdMoments(SchoolsGame.Day, SchoolsGame.Minute, SchoolsGame.Year);
            want = _testRun ? CrowdWhen.Run | CrowdWhen.Break | CrowdWhen.Lunch | CrowdWhen.After | CrowdWhen.OpenDay : want;
            want = Near(CrowdNear) ? want : CrowdWhen.None;
            if (_crowded && want == _crowdMoments)
            {
                return;
            }
            _crowded = true;
            _crowdMoments = want;
            SchoolGrounds.Crowds(_crowdPlaces);
            int stood = 0, asked = 0;
            string why = null;
            for (int i = 0; i < _crowdPlaces.Count && i < _crowds.Length; i++)
            {
                bool wanted = (_crowdPlaces[i].When & want) != CrowdWhen.None;
                if (!wanted)
                {
                    SchoolsGame.NoCrowd(ref _crowds[i]);
                    continue;
                }
                asked++;
                if (_crowds[i] == null)
                {
                    _crowds[i] = SchoolsGame.Crowd(_crowdPlaces[i], out string whyNot);
                    why = why ?? whyNot;
                }
                stood += _crowds[i] != null ? 1 : 0;
            }
            if (Verbose && asked > 0)
            {
                Logger?.Info("Schools check (school run): " + stood + " of " + asked + " squares of people out (" + CrowdWords(want) + ")" + (stood == asked ? ": PASS" : ": " + why) + ".");
            }
        }

        // The moments in words, for the log.
        private static string CrowdWords(CrowdWhen moments)
        {
            string words = string.Empty;
            foreach (CrowdWhen m in new[] { CrowdWhen.Run, CrowdWhen.Break, CrowdWhen.Lunch, CrowdWhen.After, CrowdWhen.OpenDay })
            {
                words += (moments & m) != CrowdWhen.None ? (words.Length > 0 ? ", " : string.Empty) + m.ToString().ToLowerInvariant() : string.Empty;
            }
            return words.Length > 0 ? words : "none";
        }

        private void NoCrowds()
        {
            for (int i = 0; i < _crowds.Length; i++)
            {
                SchoolsGame.NoCrowd(ref _crowds[i]);
            }
            _crowdMoments = CrowdWhen.None;
        }

        private static bool Near(float within)
        {
            if (!SchoolsGame.Outdoors)
            {
                return false;
            }
            Vector3 p = SchoolsGame.Player;
            SchoolGrounds.Ground(out float x0, out float z0, out float x1, out float z1);
            float dx = p.x < x0 ? x0 - p.x : p.x > x1 ? p.x - x1 : 0f, dz = p.z < z0 ? z0 - p.z : p.z > z1 ? p.z - z1 : 0f;
            return dx * dx + dz * dz < within * within;
        }

        // The schools' row in the map's filters, a marker at the main gate with the school's name.
        private void Map()
        {
            if (_map != null && _places.Count > 0)
            {
                return;
            }
            SchoolGrounds.Gate(out float x, out float z, out _);
            _places.Clear();
            _places.Add(new MapPlace(new Vector3(x, UptownLots.GroundY, z), SchoolsText.T(SchoolsText.Campus)));
            if (_map == null)
            {
                _map = MapPlaces.Register("schools", "reallife_schools_map_row", MapColour, MapIcon.Paper, true, inStreet: true);   // Paper: the Mortarboard is the university's own
                _map.Available = () => Settings.Schools && UptownLots.Ready;
            }
            _map.Set(_places);
        }

        // The district's buildings near the campus given their traffic again when its name moves the lift a step.
        private void Traffic(bool force)
        {
            int level = SchoolRules.Lift(0f, 0f, _state.Campus.Reputation, _state.Opened > 0, 0f, 0f, 0f, 0f);
            if (!force && level == _liftLevel)
            {
                return;
            }
            _liftLevel = level;
            if (!DistrictBuildings.Lives || SchoolsGame.InSession)
            {
                return;
            }
            Standing.Clear();
            DistrictBuildings.Standing(Standing);
            SchoolGrounds.Ground(out float x0, out float z0, out float x1, out float z1);
            int moved = 0;
            for (int i = 0; i < Standing.Count; i++)
            {
                MadeBuilding made = Standing[i];
                if (made == null || made.Spec == null)
                {
                    continue;
                }
                float dx = made.X < x0 ? x0 - made.X : made.X > x1 ? made.X - x1 : 0f, dz = made.Z < z0 ? z0 - made.Z : made.Z > z1 ? made.Z - z1 : 0f;
                int own = dx * dx + dz * dz <= SchoolRules.LiftReach * SchoolRules.LiftReach ? DistrictBuildings.TrafficOf(made.Spec.Id) : -1;
                if (own >= 0 && SchoolsGame.Traffic(made, own))
                {
                    moved++;
                }
            }
            Standing.Clear();
            if (Verbose)
            {
                Logger?.Info("Schools check (traffic): the campus's name lifts the lots near it by up to " + level + " point(s); " + moved + " of the district's building(s) within "
                             + SchoolRules.LiftReach + " m read again: PASS.");
            }
        }

        private static readonly List<MadeBuilding> Standing = new List<MadeBuilding>(64);

        // Everything the campus stands in the street put away: its pieces, the families, the classrooms, the map's marker.
        private void LeaveCampus(string why)
        {
            SchoolsGame.Take(ref _pieceRoot);
            SpecialBuildings.Remove(SchoolsGame.MainId);
            NoCrowds();
            _crowded = false;
            Let(why);
            DropCopies();
            LeaveLook();
            _places.Clear();
            _map?.Set(_places);
            _campusStood = false;
        }
    }
}
