using System;
using System.Collections.Generic;
using System.Globalization;
using Extensions;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>Why a site could not be set out where it is. A test button says exactly this.</summary>
    public enum SiteRefusal { None, NoPavement, NoLane, NoKerb, Vehicles, NoWall, NoRoom, CannotClose, NoSlot }

    /// <summary>
    /// Puts the construction sites near the player in the street as props and takes them away. Every place is found, never
    /// assumed: works in the road by the kerb, with the lane closed or not set out at all; scaffold and hoarding in bays along
    /// the wall; a rebuild wrapped in the game's own scaffolding to its roof; a new plot's fence, frame and crane on the ground
    /// given. A new phase is drawn only out of view. Cost: nothing per frame while no wrap is changing and no plot is out (one
    /// comparison); at most one site set out a tick; a wrap's pieces a few a frame, out of view.
    /// </summary>
    public sealed partial class SiteDresser
    {
        private const int MaxDressed = 3;
        private const float DressNear = 125f;   // metres
        private const float UndressFar = 175f;   // metres
        private const float HalfTrench = 4f;        // the trench is eight metres long
        private const float StripCentre = 0.55f;    // the middle of the works strip, from the kerb
        private const float NeedsClosure = 3f;      // metres: a lane's middle nearer the kerb than this must be closed for works in the strip
        private const float BayWidth = 2.4f;   // metres
        private const float BayDepth = 1.86f;       // wall to the outer standards
        private const float WallGap = 0.12f;   // metres, off the wall
        private const float DeckHeight = 2.4f;   // metres: the first deck, open underneath so the pavement stays walkable
        private const float LiftHeight = 2.0f;   // metres
        private const int MaxBays = 5;
        private const int MaxLifts = 3;
        private const float NoticeReach = 1.7f;   // metres
        private const float FarPlane = 300f;      // metres: the game's cameras draw nothing beyond, so a big site there is out of view

        private sealed class CrewSpot
        {
            public Vector3 At, Face;
            public string Work, Rest;
            public CityLook Look;
            public float Lift;
        }

        private sealed class Dressed
        {
            public SiteState Site;
            public int Phase = -1;
            public bool Unsafe, Halted;
            public bool Now;                   // set out by a test button: being in view does not hold it back
            public bool Gone;                  // the site has left the programme: taken away as soon as nobody is looking
            public readonly List<Transform> Props = new List<Transform>(48);
            public readonly RoadClosure Road = new RoadClosure();
            public float NextSweep;
            public bool LaneShut;

            // Where it is, found once.
            public Vector3 Origin;             // the middle of it, on the ground
            public Vector3 Out;                // from the pavement towards the road (works in the road), or out of the wall
            public Vector3 Along;              // the lane's direction, or along the wall
            public Vector3 Kerb;               // works in the road: the kerb, level with the origin. Wall kinds: the foot of the wall at bay 0
            public Vector3 Edge;               // works in the road: the edge of the pavement people can stand on, level with the origin
            public float LaneFromKerb;
            public int BayMin, BayMax, Lifts;  // wall kinds: which bays stand (indices either side of the first)
            public bool HasSkip;
            public Vector3 SkipAt, SkipAlong;
            public bool HasNotice;
            public Vector3 NoticeAt, NoticeFace, Reader;

            public readonly CrewSpot[] Crew = { new CrewSpot(), new CrewSpot(), new CrewSpot() };
            public int CrewCount;

            // The big kinds: whether they are in view is asked of the whole box they fill, not of their middle.
            public bool Big;
            public Bounds Area;
            public SiteWrap Wrap;              // a rebuild's or a plot's game scaffolding; made once and kept with this
            public int Step = -1;              // a plot: PlotRules.Step when last drawn (a storey more is a change)

            // A plot's ground, from the site's record.
            public Vector3 PlotAt;
            public Quaternion PlotTurn;
            public int PlotWidth, PlotDepth, PlotYaw, PlotStoreys;
            public readonly List<GameObject> Made = new List<GameObject>(4);   // the plot's meshes in the game's materials
            public readonly List<Renderer> Hides = new List<Renderer>(8);      // what the plot hides when it stands between camera and player
            public bool Hidden;
        }

        private readonly CivicProps _props;
        private readonly CivicWorks _works;
        private readonly CivicCrew _crew;
        private readonly CivicCast _cast;
        private readonly List<Dressed> _dressed = new List<Dressed>(MaxDressed);
        private readonly Stack<Dressed> _spare = new Stack<Dressed>(MaxDressed);
        private readonly Dictionary<int, float> _retry = new Dictionary<int, float>(8);   // site id -> when to try setting it out again
        private readonly RoadClosure _probe = new RoadClosure();   // only ever asked where a lane is; closes nothing
        private Dressed _crewed;
        private NavMeshQueryFilter _pavement;
        private readonly WrapShell _shell = new WrapShell();
        private readonly Transform _root;
        private readonly Plane[] _planes = new Plane[6];
        private int _planesFrame = -1;

        public SiteRefusal LastRefusal { get; private set; }

        public SiteDresser(CivicProps props, CivicWorks works, CivicCast cast, Transform parent)
        {
            _root = new GameObject("RealLifeOverhaul Civic Sites").transform;
            _root.SetParent(parent, false);
            _props = props;
            _works = works;
            _cast = cast;
            _crew = new CivicCrew(cast);
            _pavement = new NavMeshQueryFilter { agentTypeID = NavMeshHelper.NpcAgentTypeId, areaMask = NavMesh.AllAreas & ~NavMeshHelper.SwimmingAreaMask };
        }

        public int DressedCount => _dressed.Count;

        public int CrewOut => _crew.Count;

        /// <summary>True if a site set out stands within so many metres of a place.</summary>
        public bool Near(Vector3 at, float metres)
        {
            for (int i = 0; i < _dressed.Count; i++)
            {
                if (!_dressed[i].Gone && CivicCity.Flat(_dressed[i].Origin - at) < metres * metres)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The site whose notice the player is standing at; null if none.</summary>
        public SiteState NoticeInReach(Vector3 player)
        {
            for (int i = 0; i < _dressed.Count; i++)
            {
                Dressed dressed = _dressed[i];
                if (!dressed.HasNotice || dressed.Gone)
                {
                    continue;
                }
                Vector3 offset = dressed.Reader - player;
                if (Mathf.Abs(offset.y) < 2f && CivicCity.Flat(offset) < NoticeReach * NoticeReach)
                {
                    return dressed.Site;
                }
            }
            return null;
        }

        private Dressed Find(SiteState site)
        {
            for (int i = 0; i < _dressed.Count; i++)
            {
                if (_dressed[i].Site == site)
                {
                    return _dressed[i];
                }
            }
            return null;
        }

        /// <param name="crewAllowed">False while something else of civic life's has the people (a rally).</param>
        public void Tick(float now, Vector3 player, int day, int hour, bool crewAllowed)
        {
            List<SiteState> sites = _works.State.Sites;

            // Sites that are set out: gone, far, or changed.
            for (int i = _dressed.Count - 1; i >= 0; i--)
            {
                Dressed dressed = _dressed[i];
                float sqr = CivicCity.Flat(dressed.Origin - player);
                if (!dressed.Gone && (!sites.Contains(dressed.Site) || !SiteBuilders.IsShown(dressed.Site)))   // a builder's site put away goes as one finished
                {
                    dressed.Gone = true;
                    dressed.Road.Open();   // the road first, whoever is looking
                    dressed.LaneShut = false;
                    if (_crewed == dressed)
                    {
                        _crew.Dismiss();
                    }
                }
                bool seen = Seen(dressed);
                // A big site far off waits for nobody to be looking, as a wrapped building can be seen from further than this.
                bool far = sqr > UndressFar * UndressFar && (!dressed.Big || !seen || sqr > FarPlane * FarPlane);
                if (far || (dressed.Gone && (!seen || dressed.Now)))
                {
                    Undress(i, false);
                    continue;
                }
                if (dressed.Gone)
                {
                    continue;
                }
                if (Changed(dressed, day) && (!seen || dressed.Now))
                {
                    Rebuild(dressed, day);
                }
                if (dressed.LaneShut && now >= dressed.NextSweep)
                {
                    dressed.NextSweep = now + 20f;
                    dressed.Road.Sweep();
                }
            }

            // Sites near enough that are not set out yet.
            for (int i = 0; i < sites.Count && _dressed.Count < MaxDressed; i++)
            {
                SiteState site = sites[i];
                var at = new Vector3(site.X, site.Y, site.Z);
                if (CivicCity.Flat(at - player) > DressNear * DressNear || Find(site) != null || !SiteBuilders.IsShown(site) || CitySpots.InView(at)
                    || (site.Kind == SiteKind.Plot && PlotInView(site)))
                {
                    continue;   // a plot's fence and slab stand at once: never while any of its ground is in the picture
                }
                if (_retry.TryGetValue(site.Id, out float again) && now < again)
                {
                    continue;
                }
                if (!Dress(site, false, day))
                {
                    _retry[site.Id] = now + 45f;
                }
                break;   // one a tick, and never in view
            }

            LooseTick(player);
            Crew(now, player, day, hour, crewAllowed);
        }

        /// <summary>Sets a site out now, in view or not: for the test buttons. False with <see cref="LastRefusal"/> if it has no room here.</summary>
        public bool DressNow(SiteState site, int day)
        {
            Dressed already = Find(site);
            if (already != null)
            {
                already.Now = true;
                return true;
            }
            if (_dressed.Count >= MaxDressed)
            {
                Undress(0, false);   // one of the others makes way
            }
            return Dress(site, true, day);
        }

        /// <summary>Redraws every site that is set out, now (a test button advanced them).</summary>
        public void RefreshNow(int day)
        {
            for (int i = 0; i < _dressed.Count; i++)
            {
                if (!_dressed[i].Gone && _works.State.Sites.Contains(_dressed[i].Site))
                {
                    Rebuild(_dressed[i], day);
                }
            }
        }

        /// <summary>Everything away at once: props, the crew, the wraps; every closed lane opened. Safe at any time, any number of times.</summary>
        public void Clear()
        {
            for (int i = _dressed.Count - 1; i >= 0; i--)
            {
                Undress(i, true);
            }
            _crew.Release();
            _crewed = null;
            _retry.Clear();
            ClearLoose();
        }

        private bool Dress(SiteState site, bool now, int day)
        {
            Dressed dressed = _spare.Count > 0 ? _spare.Pop() : new Dressed();
            dressed.Site = site;
            dressed.Phase = -1;
            dressed.Now = now;
            dressed.Gone = false;
            dressed.HasNotice = false;
            dressed.HasSkip = false;
            dressed.LaneShut = false;
            dressed.Big = BigKind(site.Kind);
            dressed.Step = -1;
            dressed.Hidden = false;
            SiteRefusal why;
            try
            {
                why = Resolve(dressed);
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                why = SiteRefusal.NoRoom;
            }
            if (why == SiteRefusal.None)
            {
                _dressed.Add(dressed);
                Rebuild(dressed, day);
                if (dressed.Gone)
                {
                    why = SiteRefusal.NoRoom;   // a fault while it was being set out: Rebuild has stripped it
                    _dressed.Remove(dressed);
                }
                else if (InRoad(site.Kind) && dressed.LaneFromKerb < NeedsClosure && !dressed.LaneShut && site.Phase < 4)
                {
                    // The works would stand beside a lane that cannot be shut: not here.
                    _dressed.Remove(dressed);
                    Strip(dressed);
                    why = SiteRefusal.CannotClose;
                }
            }
            LastRefusal = why;
            if (why != SiteRefusal.None)
            {
                dressed.Site = null;
                _spare.Push(dressed);
                CivicDirector.Logger?.Info("Civic: site " + site.Id + " (" + site.Kind + ") could not be set out at " + CivicWorks.PlaceText(site) + ": " + why + ".");
                return false;
            }
            CivicDirector.Logger?.Info("Civic: site " + site.Id + " (" + site.Kind + ") set out at " + CivicWorks.PlaceText(site) + ", stage " + (site.Phase + 1) + ": "
                                       + dressed.Props.Count + " prop(s)" + (dressed.LaneShut ? ", one lane closed" : string.Empty)
                                       + (InRoad(site.Kind) ? ", lane " + dressed.LaneFromKerb.ToString("F1", CultureInfo.InvariantCulture) + " m from the kerb"
                                           : dressed.Wrap != null && dressed.Wrap.Plan.Pieces.Count > 0
                                               ? ", the game's scaffold planned: " + dressed.Wrap.Plan.Modules + " module(s) a lift, " + dressed.Wrap.Plan.Lifts + " lift(s), "
                                                 + dressed.Wrap.Plan.Runs + " run(s)" + (dressed.Wrap.Plan.Why != null ? " (" + dressed.Wrap.Plan.Why + ")" : string.Empty)
                                               : site.Kind == SiteKind.Plot ? ", " + dressed.PlotWidth + " by " + dressed.PlotDepth + " m of ground, " + dressed.PlotStoreys + " storey(s)"
                                               : ", " + (dressed.BayMax - dressed.BayMin + 1) + " bay(s), " + dressed.Lifts + " lift(s)") + ".");
            return true;
        }

        private static bool InRoad(SiteKind kind)
        {
            return kind == SiteKind.Roadworks || kind == SiteKind.Utility;
        }

        // At once (<paramref name="now"/>: indoors, behind the black screen, the feature off) or, for a wrap, handed to be
        // taken down a few pieces a frame while nobody is looking.
        private void Undress(int index, bool now)
        {
            Dressed dressed = _dressed[index];
            _dressed.RemoveAt(index);
            if (_crewed == dressed)
            {
                _crew.Release();
                _crewed = null;
            }
            Strip(dressed);
            if (dressed.Wrap != null && dressed.Wrap.Placed > 0)
            {
                if (now || SceneFinds.Black)
                {
                    dressed.Wrap.Strip();
                }
                else
                {
                    Leave(dressed.Wrap);
                    dressed.Wrap = null;   // a new one is made the next time this is used
                }
            }
            dressed.Site = null;
            _spare.Push(dressed);
        }

        private void Strip(Dressed dressed)
        {
            dressed.Road.Open();
            dressed.LaneShut = false;
            _props.ReturnAll(dressed.Props);
            DropMade(dressed);
        }

        private void Rebuild(Dressed dressed, int day)
        {
            try
            {
                if (_crewed == dressed)
                {
                    _crew.Release();   // their places change with the phase
                    _crewed = null;
                }
                _props.ReturnAll(dressed.Props);
                DropMade(dressed);
                dressed.Phase = dressed.Site.Phase;
                dressed.Unsafe = dressed.Site.Unsafe;
                dressed.Halted = dressed.Site.HaltUntil > day;
                dressed.CrewCount = 0;
                switch (dressed.Site.Kind)
                {
                    case SiteKind.Roadworks: BuildRoad(dressed); break;
                    case SiteKind.Scaffold: BuildScaffold(dressed); break;
                    case SiteKind.Refit: BuildRefit(dressed); break;
                    case SiteKind.Rebuild: BuildRebuild(dressed); break;
                    case SiteKind.Plot: BuildPlot(dressed); break;
                    case SiteKind.Demolition: BuildDemolition(dressed); break;
                    default: BuildUtility(dressed); break;
                }
                if (dressed.HasNotice)
                {
                    Put(dressed, "site_notice_board", dressed.NoticeAt, dressed.NoticeFace);
                }
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                Strip(dressed);
                dressed.Wrap?.Strip();
                dressed.Gone = true;   // the site goes on as data; tried again in ten minutes
                _retry[dressed.Site.Id] = Time.time + 600f;
            }
        }

        private void Crew(float now, Vector3 player, int day, int hour, bool allowed)
        {
            _crew.Tick(now);
            // Who should have it: the nearest site that is set out and at work.
            Dressed want = null;
            if (allowed && !CivicBridges.WorkStopped() && !CivicBridges.SceneOn())
            {
                float best = float.MaxValue;
                for (int i = 0; i < _dressed.Count; i++)
                {
                    Dressed dressed = _dressed[i];
                    if (dressed.Gone || dressed.CrewCount == 0 || !_works.Working(dressed.Site, day, hour))
                    {
                        continue;
                    }
                    float sqr = CivicCity.Flat(dressed.Origin - player);
                    if (sqr < best)
                    {
                        best = sqr;
                        want = dressed;
                    }
                }
            }
            if (_crewed != null && (_crewed != want || _crew.Disturbed))
            {
                // Hours are up, the rain has come, another site is nearer, or one of them was knocked down: they walk off.
                _crew.Dismiss();
                if (_crew.Count == 0)
                {
                    _crew.Release();
                    _crewed = null;
                }
                return;
            }
            if (_crewed == null && _crew.Count > 0)
            {
                _crew.Dismiss();   // people of a site that has gone: still walking off
                return;
            }
            if (_crewed == null && want != null && _cast.Ready(want.CrewCount, player, now))
            {
                int added = 0;
                for (int i = 0; i < want.CrewCount; i++)
                {
                    CrewSpot spot = want.Crew[i];
                    if (spot.Lift > 0.5f && CitySpots.InView(spot.At))
                    {
                        continue;   // nobody is seen arriving on a scaffold: the deck stays empty this time
                    }
                    if (_crew.Add(spot.Look, spot.At, spot.Face, spot.Work, spot.Rest, spot.Lift))
                    {
                        added++;
                    }
                }
                if (added > 0)
                {
                    _crewed = want;
                    CivicDirector.Logger?.Info("Civic: a crew of " + added + " at site " + want.Site.Id + " (" + want.Site.Kind + ", stage " + (want.Phase + 1) + ").");
                }
            }
        }

        private static void Plan(Dressed dressed, Vector3 at, Vector3 face, CityLook look, string work, string rest, float lift = 0f)
        {
            if (dressed.CrewCount >= dressed.Crew.Length || dressed.Halted)
            {
                return;   // stopped by the inspector: nobody on site
            }
            if (!CitySpots.Stand(at, 0.38f, out Vector3 spot))
            {
                return;   // no room for them here: the crew is one fewer
            }
            CrewSpot planned = dressed.Crew[dressed.CrewCount++];
            planned.At = spot;
            planned.Face = face;
            planned.Look = look;
            planned.Work = work;
            planned.Rest = rest;
            planned.Lift = lift;
        }
    }
}
