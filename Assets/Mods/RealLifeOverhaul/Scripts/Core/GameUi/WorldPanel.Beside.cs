using System;
using System.Reflection;
using Localizor;
using UI;
using UI.InGameUI;
using UI.Overlays;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// A panel of the mod's and the game's building box never cover each other. The rule is
    /// <see cref="PanelPlacement"/>; this half of the class is the part that touches the game: it reads where the
    /// two boxes are on the screen and moves the mod's.
    ///
    /// What the game does, and what is done here because of it:
    ///  - the building box (UI.InGameUI.BuildingResume) is put over the door of its building in LateUpdate and
    ///    switched on or off there, every frame; nothing announces it;
    ///  - the panel's box (UI.Overlays.OverlayUI) is put at the screen point of IOverlay.GetTargetPosition in Update,
    ///    every frame, by the game.
    /// So the step aside is given to the game as a step in the world: GetTargetPosition answers the panel's place
    /// plus <c>_shift</c>, a vector across the camera's view that is exactly the wanted pixels at the place's depth.
    /// The game then puts the box beside the building box by itself each frame, and nothing of the mod's lays
    /// anything out per frame. The step is worked out only when the building box comes or goes, when either box has
    /// moved more than <see cref="PanelPlacement.Moved"/> pixels on the screen since the last time (a camera that has
    /// moved; a passer-by the panel follows), or when the panel's own words or buttons changed its size.
    ///
    /// When it is looked at: on the engine's Canvas.willRenderCanvases, which runs after every LateUpdate and after
    /// the interface's own layout, and before the frame is drawn. So the building box is already where it will be
    /// drawn and has its final size, and the two are never drawn on top of each other, not for one frame.
    ///
    /// The cost: nothing while no panel of the mod's is up (the watch is taken off the event). While one is up, each
    /// frame: one call from the engine, a few loads, the type of what is in the game's box, two positions read from
    /// the game's objects and two subtractions.
    /// A measurement (an event, as above): two rectangles read into an array made once, one WorldToScreenPoint, the
    /// rule's arithmetic. Nothing is allocated either way. If a member of the game's this needs is missing, the panels
    /// stand as they did before, with one line in the log.
    /// </summary>
    public sealed partial class WorldPanel
    {
        private const string TestLine = "reallife_panel_test_line";
        private const string TestClose = "reallife_panel_test_close";
        private const float TestReach = 80f;   // metres: how far away the building for the test may be

        // The building box's own rectangle: private in the game, read once per city.
        private static readonly FieldInfo ResumePanel = GameContract.ResumePanel.Field;   // (resolved once: Core/GameContract)

        private static readonly Vector3[] Corners = new Vector3[4];
        private static readonly Canvas.WillRenderCanvases WatchCall = Beside;

        private static bool _watching;
        private static bool _besideOff;         // a member of the game's is missing, or a fault: panels stand as before for the session
        private static bool _measureAgain;      // the panel's words or buttons changed: its size may have
        private static OverlayUI _overlay;      // the game's box for a panel over a place...
        private static RectTransform _box;      // ...and its rectangle
        private static BuildingResume _resume;
        private static RectTransform _resumeBox;
        private static Canvas _resumeCanvas;
        private static PanelSide _side;
        private static bool _boxWasUp;
        private static Vector3 _panelAt;        // where the panel's box was on the screen when the two were last measured
        private static Vector3 _resumeAt;       // ...and the building box
        private static Vector3 _shift;          // the step aside, in the world
        private static EntityController _heldFor;   // the building whose box left no side free
        private static Vector3 _heldAt;             // ...and where that box stood

        private static WorldPanel _test;
        private static CityBuildingController _testBuilding;
        private static Action _testEnd;

        /// <summary>What the panel is over, for the log ("stop"): one word, set once by its owner.</summary>
        public string Name = "panel";

        /// <summary>True when the last <see cref="Show"/> was refused because the game's building box left the panel no room. The owner's timer tries again.</summary>
        public bool Waiting { get; private set; }

        /// <summary>Where the panel of the mod's that is up stands in relation to the game's building box.</summary>
        public static PanelSide Side => _current != null ? _side : PanelSide.Home;

        // The watch on the engine's event: on while a panel of the mod's is in the game's box, off otherwise.
        private static void Watch(bool on)
        {
            _side = PanelSide.Home;
            _shift = Vector3.zero;
            _boxWasUp = false;
            _measureAgain = on;   // a panel just put up is measured in its first frame
            if (on == _watching || (on && (_besideOff || !Find())))
            {
                return;
            }
            _watching = on;
            if (on)
            {
                Canvas.willRenderCanvases += WatchCall;
            }
            else
            {
                Canvas.willRenderCanvases -= WatchCall;
            }
        }

        // The game's two objects, found again when the city (and its interface) is another one.
        private static bool Find()
        {
            if (_box != null && _resumeBox != null)
            {
                return true;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            _overlay = uis != null ? uis.overlayUI : null;
            _box = _overlay != null ? _overlay.transform as RectTransform : null;
            _resume = uis != null ? uis.buildingResume : null;
            _resumeBox = null;
            if (_resume != null)
            {
                _resumeBox = ResumePanel != null ? ResumePanel.GetValue(_resume) as RectTransform : null;
                if (_resumeBox == null && _resume.data != null)
                {
                    _resumeBox = _resume.data.parent as RectTransform;   // the box is the parent of its two halves
                }
                _resumeCanvas = _resume.GetComponentInParent<Canvas>(true);
            }
            if (_box == null || _resumeBox == null)
            {
                if (uis != null)
                {
                    _besideOff = true;
                    UiClone.Fallback("the game's building box was not found where it was, so a panel over a place is not moved aside for it");
                }
                return false;
            }
            return true;
        }

        // Once a frame while a panel of the mod's is up, after the game has placed its building box and before the frame is drawn.
        private static void Beside()
        {
            try
            {
                WorldPanel panel = _current;
                if (panel == null || _box == null || _resumeBox == null || !OverlayUI.IsVisible || _overlay.GetCurrentOverlayType != typeof(WorldPanel))
                {
                    Watch(false);   // the game took its box back (or put a panel of its own in it), or its interface went with the city
                    return;
                }
                bool up = _resumeBox.gameObject.activeInHierarchy;
                Vector3 at = _box.position;
                Vector3 resumeAt = up ? _resumeBox.position : _resumeAt;
                if (!_measureAgain && !PanelPlacement.Due(up, _boxWasUp, (at - _panelAt).sqrMagnitude, (resumeAt - _resumeAt).sqrMagnitude))
                {
                    return;
                }
                _measureAgain = false;
                Measure(panel, up, at, resumeAt);
            }
            catch (Exception e)
            {
                _besideOff = true;
                Watch(false);
                Logger?.Error(e);
                UiClone.Fallback("a panel over a place is no longer moved aside for the game's building box, after the error above");
            }
        }

        // The two rectangles on the screen, the rule, and the step. An event, never a frame's routine.
        private static void Measure(WorldPanel panel, bool up, Vector3 at, Vector3 resumeAt)
        {
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                return;
            }
            _boxWasUp = up;
            _resumeAt = resumeAt;
            _panelAt = at;

            // The panel's rectangle as it would stand at home: where it is now, less the step it has been given.
            Vector3 home = camera.WorldToScreenPoint(panel.Home());
            _box.GetWorldCorners(Corners);   // an overlay canvas: its corners are pixels of the screen
            float backX = home.x - at.x;
            float backY = home.y - at.y;
            float panelLeft = Corners[0].x + backX;
            float panelBottom = Corners[0].y + backY;
            float panelRight = Corners[2].x + backX;
            float panelTop = Corners[2].y + backY;

            float boxLeft = 0f, boxBottom = 0f, boxRight = 0f, boxTop = 0f;
            if (up)
            {
                _resumeBox.GetWorldCorners(Corners);
                Camera through = _resumeCanvas != null && _resumeCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? _resumeCanvas.worldCamera : null;
                Vector2 low = RectTransformUtility.WorldToScreenPoint(through, Corners[0]);
                Vector2 high = RectTransformUtility.WorldToScreenPoint(through, Corners[2]);
                boxLeft = low.x;
                boxBottom = low.y;
                boxRight = high.x;
                boxTop = high.y;
            }

            PanelSide side = PanelPlacement.Place(up && home.z > 0f, boxLeft, boxBottom, boxRight, boxTop, panelLeft, panelBottom, panelRight, panelTop,
                                                  Screen.width, Screen.height, _side, out float dx, out float dy);
            if (side == PanelSide.Wait)
            {
                // No side has room: the panel leaves the game's box until the building box has gone or moved (see Held).
                _heldFor = _resume != null ? _resume.TargetController : null;
                _heldAt = resumeAt;
                _current = null;
                Watch(false);
                panel.Waiting = true;
                OverlayUI.Hide(panel);
                Say(panel, side, 0f, 0f, true);
                return;
            }

            Vector3 shift = Vector3.zero;
            if (side != PanelSide.Home)
            {
                // Metres across the view for one pixel, at the depth the place is at: exact for the game's camera, which looks through a plain lens.
                float perPixel = camera.orthographic
                    ? 2f * camera.orthographicSize / camera.pixelHeight
                    : 2f * home.z * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) / camera.pixelHeight;
                Transform view = camera.transform;
                shift = view.right * (dx * perPixel) + view.up * (dy * perPixel);
            }
            _shift = shift;
            // The game placed its box for this frame already (OverlayUI.Update): it is put beside the building box now,
            // and by the game itself from the next frame on.
            var to = new Vector3(home.x + dx, home.y + dy, home.z);
            if ((to - at).sqrMagnitude > 0.01f)
            {
                _box.position = to;
            }
            _panelAt = to;
            if (side != _side)
            {
                _side = side;
                bool clear = side == PanelSide.Home
                             || !PanelPlacement.Overlap(boxLeft, boxBottom, boxRight, boxTop, panelLeft + dx, panelBottom + dy, panelRight + dx, panelTop + dy, 0f);
                Say(panel, side, dx, dy, clear);
            }
        }

        private static void Say(WorldPanel panel, PanelSide side, float dx, float dy, bool clear)
        {
            if (!Meta.Dev.Verbose)
            {
                return;
            }
            float step = Mathf.Abs(dx) + Mathf.Abs(dy);
            Logger?.Info("Panel check (" + panel.Name + ", beside a building box): " + PanelPlacement.Word(side)
                         + (side != PanelSide.Home && side != PanelSide.Wait ? " by " + step.ToString("F0") + " pixels" : string.Empty)
                         + ": " + (clear ? "PASS" : "FAIL (the two boxes still share part of the screen)") + ".");
        }

        // Whether the building box that left a panel no room is still up and still where it was. Called from Show: on the owner's timer, not per frame.
        private static bool Held()
        {
            if (_heldFor == null)
            {
                return false;
            }
            if (_besideOff || _resume == null || _resumeBox == null || _resume.TargetController != _heldFor || !_resumeBox.gameObject.activeInHierarchy
                || (_resumeBox.position - _heldAt).sqrMagnitude > PanelPlacement.MovedSqr)
            {
                _heldFor = null;   // gone, another building's, or moved: the panel is tried again
                return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- the test tool

        /// <summary>
        /// "Test: a stop's panel and a shop's box together": the game's building box is put up for the nearest building
        /// (as the pointer over it would), and, unless a panel of the mod's is up already, a panel is stood over that
        /// building's door, where the two must meet. The panel has to step aside; the log says to which side. Pressed
        /// again, or with the panel's own button, both are taken down.
        /// </summary>
        public static void TestBeside()
        {
            try
            {
                if ((_test != null && (_test.Showing || _test.Waiting)) || _testBuilding != null)
                {
                    EndTest();
                    return;
                }
                _test = null;   // (one left from a city that has gone)
                UIs uis = InstanceBehavior<UIs>.Instance;
                PlayerController player = Helpers.PlayerHelper.PlayerController;
                if (uis == null || uis.buildingResume == null || player == null || SaveGameManager.Current == null)
                {
                    TestRefusals.Tell(Logger, "Panel beside a building box", TestRefusal.NotLoaded);
                    return;
                }
                if (BuildingManager.IsInsideBuilding)
                {
                    TestRefusals.Tell(Logger, "Panel beside a building box", TestRefusal.Indoors);
                    return;
                }
                if (CityMap.IsOpen)
                {
                    TestRefusals.Tell(Logger, "Panel beside a building box", TestRefusal.ScreenBusy);
                    return;
                }
                // One look through the city's buildings, on a button press.
                Vector3 from = player.transform.position;
                CityBuildingController nearest = null;
                float best = TestReach * TestReach;
                foreach (CityBuildingController building in UnityEngine.Object.FindObjectsOfType<CityBuildingController>())
                {
                    if (building.building == null || building.entranceDoors == null || building.entranceDoors.Length == 0
                        || building.entranceDoors[0] == null || building.entranceDoors[0].doorTransform == null)
                    {
                        continue;
                    }
                    float sqr = (building.entranceDoors[0].doorTransform.position - from).sqrMagnitude;
                    if (sqr < best)
                    {
                        best = sqr;
                        nearest = building;
                    }
                }
                if (nearest == null)
                {
                    TestRefusals.Tell(Logger, "Panel beside a building box", TestRefusal.NoVenue, (int)TestReach);
                    return;
                }
                _testBuilding = nearest;
                uis.buildingResume.SelectTarget(nearest);   // what the pointer over the building does (BuildingResume.HoverBuilding)
                bool own = false;
                if (_current == null && !OverlayUI.IsVisible)
                {
                    if (_testEnd == null)
                    {
                        _testEnd = EndTest;
                    }
                    _test = new WorldPanel { Name = "stop" };
                    _test.Lines(TestLine.GetLocalization(), null, null, White());
                    _test.Button(0, TestClose, _testEnd, true, true);
                    _test.Position = nearest.entranceDoors[0].doorTransform.position + Vector3.up * 0.5f;
                    own = _test.Show();
                    if (!own && !_test.Waiting)
                    {
                        _test = null;
                    }
                }
                Logger?.Info("Panel test: the game's building box is up for the building " + Mathf.Sqrt(best).ToString("F0") + " m away"
                             + (own ? ", and a panel stands over its door" : ", beside the panel that was up already") + ". The next 'Panel check' line says where the panel went.");
                UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, "reallife_panel_test_set", null, 10f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static void EndTest()
        {
            try
            {
                if (_testBuilding != null)
                {
                    UIs uis = InstanceBehavior<UIs>.Instance;
                    if (uis != null && uis.buildingResume != null)
                    {
                        uis.buildingResume.UnselectBuilding(_testBuilding);   // only if the box is still this building's
                    }
                }
                _testBuilding = null;
                if (_test != null)
                {
                    _test.Hide();
                    _test = null;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
