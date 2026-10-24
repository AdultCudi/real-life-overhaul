using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using BAModAPI;
using RealLifeOverhaul.NewGround;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's ground (docs/features/uptown.md): at every load, in the city's load behind the loading screen and before
    /// the traffic starts, the whole band north of Manhattan, three rows of eighteen blocks, is laid whole or not at all.
    /// Everything it needs of the game's is found first (the backdrop, its walls, the map's camera, the road modules and a
    /// block's ground to copy); then the backdrop is put away where the band stands, its streets go into the traffic through
    /// 1st Avenue's own junctions and its corner at 9th Street, its street art is copied, both walking meshes grow over its
    /// ground from Manhattan's carriageways and pavements (walking round whatever stands on it), the old walls open and new
    /// ones close its edge, its ground and road colliders, the map's bounds, its fog cut away (GroundDirector.Fog) and its
    /// bus termini are laid; once the game is loaded, its parking and its pedestrians' zones. Where the game has put
    /// something of its own there (a free update),
    /// the step that finds it stands down with one line and everything laid before it is given back the same load, so the
    /// district never covers the game's. Its option, the city check and the session (<see cref="GroundSession"/>) decide
    /// first; nothing is saved or sent. The lots open when it stands (<see cref="UptownLots"/>), before the city's
    /// buildings are made. COST: disabled while no test runs; then four looks a second, and one time comparison a frame
    /// between them.
    /// </summary>
    public sealed partial class GroundDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("uptown", "Uptown GroundDirector", typeof(GroundDirector));
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private const float Look = 0.25f;                     // s of real time between two looks while a test runs
        private const string NoticeId = "RealLifeOverhaul uptown ground";   // one notice at a time: a new one replaces it
        private const float NoticeSeconds = 10f;              // s

        public static IModLogger Logger;
        public static GroundDirector Instance { get; private set; }

        private readonly List<GroundRect> _rects = new List<GroundRect>(16);
        private readonly List<PlanBox> _walls = new List<PlanBox>(5);
        private readonly List<PlanBox> _grounds = new List<PlanBox>(3);
        private readonly List<PlanBox> _roads = new List<PlanBox>(64);
        private readonly List<float> _wallX = new List<float>(11);
        private readonly List<float> _wallZ = new List<float>(11);
        private readonly List<PlanTerminus> _termini = new List<PlanTerminus>(3);
        private RoadBuild _build;
        private bool _walkable;                // the player's ground was laid
        private bool _people;                  // the pedestrians' ground was laid
        private string _peopleWhy;             // why not
        private string _playerWalk, _peopleWalk;   // how each mesh was grown, for the census
        private double _layMs, _playerMs, _peopleMs, _streetsMs, _artMs;
        private int _hidden, _strays, _opened, _copied;
        private string _laidWhy;               // why the district does not stand, or null
        private float _nextLook;

        private void Awake()
        {
            Instance = this;
            enabled = false;
            UptownLots.Begin("the city is loading");
            Lay();
        }

        private void OnDestroy()
        {
            GroundGame.UncutFog();   // the fog is the game's object: its own mesh back on every way out, what stands staying or not
            bool stays = Meta.SaveTools.Dormant;   // the save tools stopped the mod in a city that stays: what stands stays until it goes
            if (!stays)
            {
                GroundGame.Forget();
            }
            UptownLots.Laid(false, stays ? "the save tools stopped the mod" : "the city is going");
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Uptown: GroundDirector.Update");
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
            _nextLook = now + Look;
            if (!TickCount())
            {
                enabled = false;
            }
        }

        // ---------------------------------------------------------------- laid at the load

        // In the city's load, before the traffic starts: the whole band, or nothing of it.
        private void Lay()
        {
            long began = Stopwatch.GetTimestamp();
            GroundGame.Forget();   // nothing of an earlier city is held, whichever way it went
            if (!GroundSession.Agrees(out string why))
            {
                StandDown(why, false);
                return;
            }
            why = GroundGame.FindBackdrop() ?? GroundGame.FindTemplates();
            if (why != null)
            {
                StandDown(why, false);
                return;
            }
            // The backdrop and the walls across the old street ends go first: the ground's check counts every collider of
            // the game's left on the district's ground as something a free update built there.
            LayoutPlans.Hides(_rects);
            _hidden = GroundGame.HideBackdrop(_rects);
            _strays = GroundGame.HideStrays(_rects);
            LayoutPlans.OldWalls(_wallX, _wallZ);
            _opened = GroundGame.OpenWalls(_wallX, _wallZ, LayoutPlans.OldWallReach);
            long mark = Stopwatch.GetTimestamp();
            why = GroundWork.Streets(LayoutPlans.Streets(), out RoadBuild build);
            _streetsMs = Ms(mark);
            if (why != null)
            {
                StandDown("its streets: " + why, true);
                return;
            }
            // The art stands before the ground grows: what of it stands on the ground (a pole, a bin) is walked round.
            mark = Stopwatch.GetTimestamp();
            _copied = GroundGame.CopyArt(build);
            _artMs = Ms(mark);
            mark = Stopwatch.GetTimestamp();
            why = GroundWork.Ground(LayoutPlans.PlayerGround());
            _playerMs = Ms(mark);
            if (why != null)
            {
                string back = GroundWork.TakeBack(build);
                if (back == null)
                {
                    StandDown("its ground for the player: " + why, true);
                    return;
                }
                // Its streets stay among the traffic's: the rest is laid for the cars, and no lot opens.
                Logger?.Warn("Uptown: its ground for the player could not be laid (" + why + ") and its streets could not be taken back (" + back
                             + "); the district is laid for the cars alone, closed to the player.");
            }
            _walkable = why == null;
            if (GroundBuilder.ClearMiss != null)
            {
                Logger?.Warn("Uptown: " + GroundBuilder.ClearMiss + "; the district is laid all the same, as it never gives its ground back at this load once it is grown.");
            }
            _playerWalk = _walkable ? GroundWork.LastWalk : null;
            if (GroundWork.LastNote != null)
            {
                Logger?.Warn("Uptown: the player's ground noted (laid all the same): " + GroundWork.LastNote + ".");
            }
            if (_walkable)
            {
                Logger?.Info("Uptown: " + _playerWalk + ".");
            }
            mark = Stopwatch.GetTimestamp();
            _peopleWhy = _walkable ? GroundWork.Ground(LayoutPlans.NpcGround()) : "the player's ground was not laid";
            _peopleMs = Ms(mark);
            _people = _peopleWhy == null;
            _peopleWalk = _people ? GroundWork.LastWalk : null;
            if (GroundWork.LastNote != null)
            {
                Logger?.Warn("Uptown: the pedestrians' ground noted (laid all the same): " + GroundWork.LastNote + ".");
            }
            if (_people)
            {
                Logger?.Info("Uptown: " + _peopleWalk + ".");
            }
            LayoutPlans.NewWalls(_walls);
            LayoutPlans.Colliders(_grounds, _roads);
            GroundGame.Build(_walls, _grounds, _roads, LayoutPlans.GroundCollider, LayoutPlans.RoadCollider);
            LayoutPlans.MapBounds(out float west, out float east, out float north);
            if (!GroundGame.WidenMap(west, east, north))
            {
                Logger?.Warn("Uptown: the city map's camera was not found, so the map does not reach over the district at this load; the district stands all the same.");
            }
            LayFog();
            LayoutPlans.Termini(_termini);
            for (int i = 0; i < _termini.Count; i++)
            {
                TransitMap.AddTerminus(new Vector3(_termini[i].X, UptownLots.GroundY, _termini[i].Z), _termini[i].NameKey, _termini[i].Name);
            }
            _build = build;
            ScenePrewarm.AddLate("uptown: parking and pedestrians on its streets, once the game is loaded", Late);
            _laidWhy = _walkable ? null : "its ground for the player could not be laid";
            UptownLots.Laid(_walkable, _laidWhy);
            _layMs = Ms(began);
            Logger?.Info("Uptown: laid behind the loading screen in " + _layMs.ToString("F0", Inv) + " ms: " + RoadRules.Words(build) + " in " + _streetsMs.ToString("F0", Inv)
                         + " ms; " + _copied + " pieces of the game's copied (" + GroundGame.GroundsLaid + " blocks' grounds, " + GroundGame.SceneryLaid + " past the walls; "
                         + GroundGame.Unpaved + " block grounds' slab edges laid as their middles) in "
                         + _artMs.ToString("F0", Inv) + " ms; the player's walking ground " + (_walkable ? "grown in " + _playerMs.ToString("F0", Inv) + " ms" : "NOT laid")
                         + ", the pedestrians' " + (_people ? "in " + _peopleMs.ToString("F0", Inv) + " ms" : "not (" + _peopleWhy + "), so no one walks there") + "; " + _hidden
                         + " pieces of the backdrop and " + _strays + " strays put away, " + _opened + " of the game's walls opened, " + GroundGame.Built
                         + " walls and colliders built, " + _termini.Count + " bus termini; " + UptownLots.All.Count + " lots " + (_walkable ? "open" : "closed") + ".");
        }

        // The district does not stand at this load: one line, and what was laid of it given back.
        private void StandDown(string why, bool laid)
        {
            if (laid)
            {
                GroundGame.GiveBack();
            }
            else
            {
                GroundGame.Forget();
            }
            _laidWhy = why;
            UptownLots.Laid(false, why);
            Logger?.Info("Uptown: not laid at this load: " + why + ".");
        }

        // The game's late call after its own start of the city, the loading screen still up: the parking along the
        // district's kerbs and, with the pedestrians' ground, their zones; and with Developer diagnostics on, whether the
        // traffic took the district's streets as its own.
        private bool Late()
        {
            if (_build == null)
            {
                return true;
            }
            int refused = GroundGame.Populate(_people, out string why);
            if (Meta.Dev.Verbose && RoadData.Scene != null)
            {
                RoadsGame.Taken(_build, RoadData.Scene, out string line);
                Logger?.Info("Uptown check (streets): " + line);
            }
            Logger?.Info("Uptown: once the game was loaded, " + GroundGame.ParkingLanes + " parking lanes along its kerbs" + (refused > 0 ? " (" + refused + " not laid: " + why + ")" : string.Empty)
                         + " and " + GroundGame.PeopleZones + " pedestrians' zones" + (_people ? string.Empty : " (no pedestrians' ground)") + ".");
            return true;
        }

        /// <summary>
        /// The feature's clean-up (the options page, five faults in a row, the save tools, another city): the district
        /// closes until the next load, the lots' features told first; its parking and its people go back to the game's pools.
        /// Its ground stays until the city goes: the traffic, once started, keeps its streets.
        /// </summary>
        public void Close(string why)
        {
            StopCount();
            UptownLots.Close(why, e => Logger?.Error(e));
            GroundGame.Unpopulate();
            GroundGame.UncutFog();
            Logger?.Info("Uptown: closed until the next load (" + why + "); its ground stays until the city goes.");
        }

        // ---------------------------------------------------------------- shared

        private static double Ms(long since)
        {
            return (Stopwatch.GetTimestamp() - since) * 1000.0 / Stopwatch.Frequency;
        }

        private static void Notice(string key, Dictionary<string, string> data)
        {
            Notifications.Show(NotificationType.Info, key, data, NoticeSeconds, NoticeId, null, notificationSound: false, trackOnSaveGame: false);
        }

        private static string Metres(float value)
        {
            return value.ToString("0", Inv);
        }
    }
}
