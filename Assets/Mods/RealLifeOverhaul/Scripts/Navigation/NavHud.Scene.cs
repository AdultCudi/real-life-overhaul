using System;
using System.Collections.Generic;
using RealLifeOverhaul.Map25;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The minimap in 2.5D: the same blocks, ground, roads and route as the full map, seen from a camera of its own that follows the
    /// player (heading up when the minimap turns, 40 degrees down, or straight down for the flat view), drawn into a render texture
    /// the size of the map's view and shown in it in place of the flat pictures. The picture is drawn again only when the view moved a
    /// pixel's worth, turned half a degree or changed its scale, and at most thirty times a second; nothing is drawn standing still,
    /// and between two draws it is not moved at all (a draw is one pass of at most about fifty chunk draws). The pins are the
    /// panel's own, put at the places the camera sees them. Cost a frame, standing still: a few comparisons.
    /// </summary>
    public sealed partial class NavHud
    {
        private const float DrawEvery = 1f / 30f;     // seconds: the most a moving picture is drawn
        private const float TiltPitch = 40f;          // degrees from the horizon, as the mockup's tilted minimap
        private const float LeadShare = 0.16f;        // of the view's width the camera looks ahead of the player when tilted, so the player stands low in it
        private const float PinLift = 14f;            // HUD units a pin's tip stands above the place it marks

        private RawImage _scenePicture;
        private RenderTexture _sceneTarget;
        private NavScene _sceneFrom;
        private readonly Map25View _sceneView = new Map25View();
        private bool _sceneOn, _sceneTilted;
        private int _sceneWidth, _sceneHeight;
        private float _sceneX, _sceneZ, _sceneTurn = float.MaxValue, _sceneAcross, _sceneDrawn;
        private bool _sceneDirty = true, _sceneMarksDirty;
        private float _scenePlayerX, _scenePlayerY, _scenePlayerZ, _scenePlayerHeading;

        /// <summary>True once a draw of the picture came out black, kept for the session (<see cref="NavMapLatch"/>): the director then leaves the minimap flat.</summary>
        public bool SceneBroken => NavMapLatch.Drew(NavMapLatch.Minimap);

        private bool _sceneProbed;

        /// <summary>True while the minimap shows the 2.5D picture and the director feeds it by <see cref="SceneFrame"/>.</summary>
        public bool SceneMode => _sceneOn;

        /// <summary>Switches the 2.5D picture on (<paramref name="tilted"/> or flat) or off, back to the flat pictures and the panel's own route and markers.</summary>
        public void Scene(NavScene source, bool on, bool tilted)
        {
            if (_view == null || _spin == null)
            {
                return;
            }
            if (on && _scenePicture == null)
            {
                RectTransform rect = Child("Scene", _view);
                Stretch(rect);
                rect.SetSiblingIndex(_spin.GetSiblingIndex());
                _scenePicture = rect.gameObject.AddComponent<RawImage>();
                _scenePicture.raycastTarget = false;
                _scenePicture.enabled = false;
                StripMake();
            }
            if (on == _sceneOn && tilted == _sceneTilted && source == _sceneFrom)
            {
                return;
            }
            _sceneOn = on;
            _sceneTilted = tilted;
            _sceneFrom = source;
            StripShow(on);
            _sceneDirty = true;
            _sceneTurn = float.MaxValue;
            if (_scenePicture != null)
            {
                _scenePicture.enabled = on && _sceneTarget != null;
            }
            _roadsImage.enabled = !on && _roadsImage.texture != null && !_alone;
            _baseImage.enabled = !on && _baseImage.texture != null && _baseWidth > 0f && _baseHeight > 0f;
            _route.gameObject.SetActive(!on);   // the route is drawn in the picture
            if (on)
            {
                _spin.localEulerAngles = Vector3.zero;
                _spin.localScale = Vector3.one;
                _world.anchoredPosition = Vector2.zero;
            }
            else
            {
                ReleaseScene();
                _px = float.MaxValue;
                _scale = 0f;
                _turn = float.MaxValue;
                for (int i = 0; i < _markersOn; i++)
                {
                    Upright(_marker[i]);
                }
                MarksChanged();
            }
        }

        /// <summary>The places were read again: the pins are put where the picture shows them at the next frame, without a draw.</summary>
        public void SceneMarksChanged()
        {
            _sceneMarksDirty = true;
        }

        /// <summary>The route changed (the director has written it into the scene's strips): the picture is drawn again.</summary>
        public void SceneRouteChanged()
        {
            _sceneDirty = true;
        }

        /// <summary>
        /// Once a frame while the minimap shows in 2.5D: where the player is, which way the picture is turned, how many metres it spans. Draws
        /// when something changed enough, and puts the player, the destination and the pins where the picture shows them.
        /// </summary>
        public void SceneFrame(float px, float py, float pz, float heading, float view, float metresAcross, bool turnWithView, List<NavMark> marks)
        {
            if (!_sceneOn || _sceneFrom == null || !_sceneFrom.Ready || !_mapOn || _mapHeight <= 0f)
            {
                return;
            }
            int width = Mathf.Clamp(Mathf.RoundToInt(ViewWidth * Screen.height / 2160f), 96, 1024);
            int height = Mathf.Clamp(Mathf.RoundToInt(_mapHeight * Screen.height / 2160f), 96, 1024);
            if (_sceneTarget == null || width != _sceneWidth || height != _sceneHeight)
            {
                MakeSceneTarget(width, height);
            }
            float turn = turnWithView ? view : 0f;
            float pixel = metresAcross / width;
            bool changed = _sceneDirty || Mathf.Abs(metresAcross - _sceneAcross) > metresAcross * 0.004f
                           || Mathf.Abs(Mathf.DeltaAngle(turn, _sceneTurn == float.MaxValue ? turn + 180f : _sceneTurn)) > 0.5f
                           || (px - _sceneX) * (px - _sceneX) + (pz - _sceneZ) * (pz - _sceneZ) >= pixel * pixel;
            float now = Time.unscaledTime;
            if (changed && (_sceneDirty || now - _sceneDrawn >= DrawEvery))
            {
                float lead = _sceneTilted ? metresAcross * LeadShare : 0f;
                float radians = turn * Mathf.Deg2Rad;
                float tx = px + Mathf.Sin(radians) * lead, tz = pz + Mathf.Cos(radians) * lead;
                float y0 = _sceneFrom.Y0;
                _sceneView.Look(tx, py - y0, tz, turn, _sceneTilted ? TiltPitch : 89.5f, metresAcross, width / (float)height);
                _sceneFrom.Scene.Refresh();
                // the route, the cone and the halo are a few pixels of the picture, as the mockup has them: their metres are what a pixel is at the player
                float mpp = _sceneView.MetresPerPixel(tx, 0f, tz, width);
                _sceneFrom.Scene.SetRouteHalf(Map25Marks.Step(Map25Marks.RouteHalf(true, width, mpp)));
                var frame = new Map25Frame
                {
                    Player = true, PlayerX = px, PlayerY = py - y0, PlayerZ = pz, Heading = heading, Fog = _sceneTilted,
                    ConeRadius = Map25Marks.ConeMetres(true, width, mpp), HaloRadius = Map25Marks.HaloMetres(true, width, mpp)
                };
                _sceneFrom.Scene.Draw(_sceneTarget, _sceneView, frame, 1);
                if (!_sceneProbed)
                {
                    _sceneProbed = true;
                    if (Map25Scene.Blank(_sceneTarget))
                    {
                        if (NavMapLatch.Mark(NavMapLatch.Minimap))
                        {
                            Logger?.Warn("Navigation: the minimap's 2.5D picture drew nothing (it is black), so the minimap stays flat for this session.");
                        }
                        Scene(_sceneFrom, false, _sceneTilted);   // the flat pictures at once: the director's next slow tick would leave the black one up for a quarter of a second
                        return;
                    }
                }
                _sceneX = px;
                _sceneZ = pz;
                _sceneTurn = turn;
                _sceneAcross = metresAcross;
                _sceneDrawn = now;
                _sceneDirty = false;
                _scenePicture.enabled = true;
                StripFrame(turn, metresAcross);
                _scenePlayerX = px;
                _scenePlayerY = py - y0;
                _scenePlayerZ = pz;
                _scenePlayerHeading = heading;
                PlaceOnScene(px, py - y0, pz, heading, turn, marks);
                _sceneMarksDirty = false;
            }
            else if (_sceneMarksDirty)
            {
                _sceneMarksDirty = false;
                PlaceOnScene(_scenePlayerX, _scenePlayerY, _scenePlayerZ, _scenePlayerHeading, _sceneTurn, marks);
            }
        }

        // The player, the destination and the pins at the places the picture shows them (the picture's own fractions, in the view's units).
        private void PlaceOnScene(float px, float py, float pz, float heading, float turn, List<NavMark> marks)
        {
            float halfWide = ViewWidth * 0.5f, halfHigh = _mapHeight * 0.5f;
            if (_sceneView.Project(px, py, pz, out float pu, out float pv))
            {
                ((RectTransform)_playerIcon.parent).anchoredPosition = new Vector2((pu - 0.5f) * ViewWidth, (pv - 0.5f) * _mapHeight);
            }
            float arrow = -Mathf.DeltaAngle(turn, heading);
            _arrow = arrow;
            _playerIcon.localEulerAngles = new Vector3(0f, 0f, arrow + 45f);
            _scale = 1f;
            _turn = 0f;
            if (_goalOn)
            {
                float over = 1f;
                if (_sceneView.Project(_goalX, py, _goalZ, out float gu, out float gv))
                {
                    float gx = (gu - 0.5f) * ViewWidth, gy = (gv - 0.5f) * _mapHeight + PinLift;
                    over = Mathf.Max(Mathf.Abs(gx) / (halfWide - GoalSize * 0.45f), Mathf.Abs(gy) / (halfHigh - GoalSize * 0.45f));
                    if (over > 1f)
                    {
                        gx /= over;
                        gy /= over;
                    }
                    _goal.anchoredPosition = new Vector2(gx, gy);
                }
            }
            int used = 0;
            for (int i = 0; marks != null && i < marks.Count && used < Pool; i++)
            {
                NavMark mark = marks[i];
                if (!_sceneView.Project(mark.X, py, mark.Z, out float u, out float v) || u < -0.04f || u > 1.04f || v < -0.04f || v > 1.04f)
                {
                    continue;
                }
                RectTransform marker = _marker[used];
                if (_markerShows[used] != i)
                {
                    _markerShows[used] = i;
                    _markerBlob[used].color = mark.Colour;
                    Image icon = _markerIcon[used];
                    icon.sprite = mark.Icon;
                    icon.enabled = mark.Icon != null;
                    icon.color = _markerBlob[used].enabled ? new Color32(255, 255, 255, 255) : mark.Colour;
                }
                marker.anchoredPosition = new Vector2((u - 0.5f) * ViewWidth, (v - 0.5f) * _mapHeight + PinLift);
                if (used >= _markersOn)
                {
                    marker.gameObject.SetActive(true);
                    Upright(marker);
                }
                used++;
            }
            for (int i = used; i < _markersOn; i++)
            {
                _marker[i].gameObject.SetActive(false);
                _markerShows[i] = -1;
            }
            _markersOn = used;
        }

        private void MakeSceneTarget(int width, int height)
        {
            ReleaseScene();
            _sceneWidth = width;
            _sceneHeight = height;
            _sceneTarget = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "RealLife Minimap 2.5D",
                antiAliasing = 4,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                hideFlags = HideFlags.HideAndDontSave
            };
            _sceneTarget.Create();
            _scenePicture.texture = _sceneTarget;
            _sceneDirty = true;
        }

        private void ReleaseScene()
        {
            if (_scenePicture != null)
            {
                _scenePicture.texture = null;
                _scenePicture.enabled = false;
            }
            if (_sceneTarget != null)
            {
                _sceneTarget.Release();
                UnityEngine.Object.Destroy(_sceneTarget);
                _sceneTarget = null;
            }
        }
    }
}
