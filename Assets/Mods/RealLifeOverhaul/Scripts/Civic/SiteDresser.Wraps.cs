using System;
using System.Collections.Generic;
using UI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The big kinds' share of the street: what is in view for a site that fills a whole building or a plot (the box it fills
    /// against the camera's view), the wraps set out and taken down a few pieces a frame while out of view, a wrap handed on
    /// to be taken down after its site has gone, the plots hiding their frame from the camera, and the test wrap and crane.
    /// Cost: <see cref="Frame"/> runs only while <see cref="Framed"/>: a few pieces a frame, a look at the plots five times a second.
    /// </summary>
    public sealed partial class SiteDresser
    {
        private const float FadeEvery = 0.2f;     // seconds between looks at whether a plot stands between the camera and the player

        private readonly List<SiteWrap> _leaving = new List<SiteWrap>(4);
        private SiteWrap _testWrap;
        private Transform _testCrane;
        private float _nextFade;
        private Action<SiteWrap> _settled;        // made once: every wrap is handed the same

        /// <summary>True while a wrap is changing or coming down, or a plot is out: then <see cref="Frame"/> is wanted each frame.</summary>
        public bool Framed { get; private set; }

        private static bool BigKind(SiteKind kind) => kind == SiteKind.Rebuild || kind == SiteKind.Plot;

        // In view: a big site by the whole box it fills, the others by their middle (CitySpots' cone).
        private bool Seen(Dressed dressed) => dressed.Big ? BoxInView(dressed.Area) : CitySpots.InView(dressed.Origin);

        // What the dressing shows is no longer what the site is at: its stage, its safety, an inspection, a plot's storeys.
        private static bool Changed(Dressed dressed, int day)
        {
            SiteState site = dressed.Site;
            return dressed.Phase != site.Phase || dressed.Unsafe != site.Unsafe || dressed.Halted != site.HaltUntil > day
                   || (site.Kind == SiteKind.Plot && dressed.Step != PlotRules.Step(site.Phase, site.Progress, dressed.PlotStoreys));
        }

        /// <summary>Whether any of a box is in the camera's picture: within its far plane and inside its view. False with no camera.</summary>
        private bool BoxInView(Bounds box)
        {
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                return false;
            }
            if (box.SqrDistance(camera.transform.position) > FarPlane * FarPlane)
            {
                return false;
            }
            if (_planesFrame != Time.frameCount)
            {
                GeometryUtility.CalculateFrustumPlanes(camera, _planes);
                _planesFrame = Time.frameCount;
            }
            return GeometryUtility.TestPlanesAABB(_planes, box);
        }

        /// <summary>
        /// Once a frame while <see cref="Framed"/>: each changing wrap sets out or takes down its pieces for the frame (none
        /// while it is in view, none put up while the city map shows), and the plots' frames are hidden from the camera.
        /// </summary>
        public void Frame(float now)
        {
            bool black = SceneFinds.Black, map = CityMap.IsOpen, any = false;
            for (int i = 0; i < _dressed.Count; i++)
            {
                Dressed dressed = _dressed[i];
                if (dressed.Wrap != null && dressed.Wrap.Busy)
                {
                    StepWrap(dressed.Wrap, dressed.Now, black, map);
                    any |= dressed.Wrap.Busy;
                }
                any |= dressed.Site != null && dressed.Site.Kind == SiteKind.Plot;
            }
            for (int i = _leaving.Count - 1; i >= 0; i--)
            {
                SiteWrap wrap = _leaving[i];
                StepWrap(wrap, false, black, map);
                if (wrap.Placed == 0 || !wrap.Busy)
                {
                    wrap.Strip();
                    _leaving.RemoveAt(i);
                    continue;
                }
                any = true;
            }
            if (_testWrap != null && _testWrap.Busy)
            {
                StepWrap(_testWrap, true, black, map);
                any |= _testWrap.Busy;
            }
            if (now >= _nextFade)
            {
                _nextFade = now + FadeEvery;
                FadePlots();
            }
            Framed = any;
        }

        private void StepWrap(SiteWrap wrap, bool now, bool black, bool map)
        {
            bool taking = wrap.Taking;
            if (map && !taking)
            {
                return;   // a piece set out now would stand on the map over the building's box until the map closes
            }
            int budget = WrapRules.Budget(!taking, !black && BoxInView(wrap.Bounds), black, now);
            if (budget > 0)
            {
                wrap.Step(budget);
            }
        }

        // A wrap whose site has gone: taken down from the top, a few pieces a frame, while nobody is looking.
        private void Leave(SiteWrap wrap)
        {
            wrap.Want(0, false);
            _leaving.Add(wrap);
            Framed = true;
        }

        private SiteWrap NewWrap()
        {
            return new SiteWrap(_root) { Settled = _settled ?? (_settled = Settle) };
        }

        // A wrap has every piece it should: the self-check line, once a change.
        private static void Settle(SiteWrap wrap)
        {
            if (!Meta.Dev.Verbose)
            {
                return;
            }
            if (wrap.Placed == 0)
            {
                string left = wrap.Building != null ? StrayRenderers(wrap.Building) : null;
                CivicDirector.Logger?.Info("Construction check (wrap down): every piece taken down" + (wrap.Building == null ? ": PASS."
                    : left == null ? ", and the building's fade lists hold none of them: PASS." : ": FAIL (" + left + ")."));
                return;
            }
            string why = wrap.Building != null ? wrap.Unlinked() : null;
            CivicDirector.Logger?.Info("Construction check (wrap): " + wrap.ModulesShown + " modules, " + wrap.LiftsShown + " lifts, "
                                       + (wrap.Building == null ? "round a plot's frame, hidden with it from the camera: PASS."
                                           : why == null ? "linked to the building's fade" + (wrap.MapLinked ? " and its city map look" : " (the city map's set could not be read)") + ": PASS."
                                           : "linked to the building's fade: FAIL (" + why + ")."));
        }

        // A destroyed renderer left in the game's lists would stop its fade in the middle of a frame: there must be none.
        private static string StrayRenderers(CityBuildingController building)
        {
            int stray = 0;
            for (int i = 0; i < building.renderersToFade.Count; i++)
            {
                stray += building.renderersToFade[i] == null ? 1 : 0;
            }
            for (int i = 0; i < building.renderersToHide.Count; i++)
            {
                stray += building.renderersToHide[i] == null ? 1 : 0;
            }
            return stray == 0 ? null : stray + " destroyed renderer(s) left in the building's lists";
        }

        /// <summary>
        /// Test: the building wrapped to its roof in the game's own scaffolding, fabric on, a few pieces a frame from now (in
        /// view too). Null when it is under way, else why it cannot be.
        /// </summary>
        public string WrapNow(CityBuildingController building, string name)
        {
            if (!SiteKit.Ready)
            {
                return "the game's scaffold pieces were not found in this city (" + SiteKit.Source + ")";
            }
            if (_testWrap != null && _testWrap.Placed > 0)
            {
                Leave(_testWrap);
            }
            _testWrap = NewWrap();
            string why = _testWrap.Prepare(building, name);
            if (why != null)
            {
                return why;
            }
            _testWrap.Want(_testWrap.Plan.Lifts, true);
            Framed = true;
            return null;
        }

        /// <summary>The test wrap's plan, for the test's notice; null while there is none.</summary>
        public WrapPlan TestPlan => _testWrap?.Plan;

        // The test pieces: taken away with everything else, and once the player has gone far from them.
        private void LooseTick(Vector3 player)
        {
            if (_testWrap != null && _testWrap.Placed > 0 && CivicCity.Flat(_testWrap.Bounds.center - player) > UndressFar * UndressFar)
            {
                Leave(_testWrap);
                _testWrap = null;
            }
            if (_testCrane != null && CivicCity.Flat(_testCrane.position - player) > UndressFar * UndressFar && !CitySpots.InView(_testCrane.position))
            {
                ReturnCrane(_testCrane);
                _testCrane = null;
            }
        }

        private void ClearLoose()
        {
            _testWrap?.Strip();
            _testWrap = null;
            for (int i = 0; i < _leaving.Count; i++)
            {
                _leaving[i].Strip();
            }
            _leaving.Clear();
            if (_testCrane != null)
            {
                ReturnCrane(_testCrane);
                _testCrane = null;
            }
            Framed = false;
        }
    }
}
