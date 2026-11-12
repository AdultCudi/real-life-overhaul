using System;
using BigAmbitions.InputSystem;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The world's pieces on the RLO interface, on one canvas of the kit's nested over the game's OverlayUI canvas
    /// (UiOverCanvas), so the game's scaler and the UI zoom scale it as they scale the game's own boxes:
    /// - the world prompt (<see cref="WorldPrompt"/>): every box the game's OverlayUI shows (a door, a lift, a fuel stop, a
    ///   garage bay) and every one of the mod's WorldPanel and DoorPanel, which stand in it, drawn as the kit's card where
    ///   the game put its own: the game projects its box each frame and the card reads that place, so a shown prompt costs no
    ///   projection of its own;
    /// - the building's panel (<see cref="WorldBuilding"/>): the game's BuildingResume as a card at its door while the
    ///   pointer is over a building, and as the info panel docked at the top left once the building is picked.
    /// The game's own boxes are hidden by leaf (UiConceal) while the kit draws them and come back as they were when a switch
    /// or the interface goes off. Cost a frame: a few reference comparisons and one move per shown piece; nothing allocated.
    /// </summary>
    public sealed class WorldHost : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "WorldHost", typeof(WorldHost));   // its Update (Core/Guard)
        private static readonly int HotUpdate = HotSpots.Slot("Ui: WorldHost.Update");
        private static readonly int HotLate = HotSpots.Slot("Ui: WorldHost.LateUpdate");

        private RectTransform _rect;
        private Vector2 _laidSize;
        private bool _noFaceSaid;   // the line that the world's boxes wait for a font, said once
        private float _scale = UiTokens.CanvasPerPx;
        private readonly WorldPrompt _prompt = new WorldPrompt();
        private readonly WorldBuilding _building = new WorldBuilding();
        private readonly OverlayCardsLive _overlays = new OverlayCardsLive();
        private const float SwitchEvery = 0.5f;   // s of real time between reads of the item cards' switches: a switch is never read each frame
        private float _switchDue;
        private bool _hoverOn, _clickOn, _personOn, _aimOn;

        /// <summary>The one host while it stands.</summary>
        public static WorldHost Instance { get; private set; }

        /// <summary>The prompt's reader, for the tests.</summary>
        internal WorldPrompt Prompt => _prompt;

        /// <summary>The building's reader, for the tests.</summary>
        internal WorldBuilding Building => _building;

        /// <summary>The cards over the world's items, for the tests.</summary>
        internal OverlayCardsLive Overlays => _overlays;

        /// <summary>What each piece is doing, for the interface's line at load.</summary>
        public static string State => Instance == null ? "the world: not built yet" : "the world: the prompt " + Instance._prompt.State + ", the building's panel "
            + Instance._building.State + ", the items' cards " + Instance._overlays.State;

        /// <summary>The host over the game's OverlayUI, made the first time it is asked for in a city; null while the game has not built it.</summary>
        public static WorldHost Ensure()
        {
            if (Instance != null)
            {
                return Instance;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            RectTransform rect = UiOverCanvas.Make(uis != null ? uis.overlayUI : null, "RealLife Interface World");
            if (rect == null)
            {
                return null;   // asked again in a second
            }
            WorldHost host = rect.gameObject.AddComponent<WorldHost>();
            Instance = host;
            host._rect = rect;
            host._prompt.Build(rect, host._scale);
            host._building.Build(rect, host._scale);
            host._overlays.Build(rect, host._scale);
            rect.gameObject.SetActive(true);
            return host;
        }

        /// <summary>The host gone and the game's boxes as they were, for a city unloading, a switch or a fault.</summary>
        public static void Remove(string why)
        {
            if (Instance == null)
            {
                return;
            }
            WorldHost host = Instance;
            Instance = null;
            host._prompt.Give();
            host._building.Give();
            host._overlays.Give();
            TutorialAimLive.Release();
            Destroy(host.gameObject);
            if (Meta.Dev.Verbose && why != null)
            {
                UiDirector.Logger?.Info("UI: the world's pieces are the game's own again (" + why + ").");
            }
        }

        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // its feature was switched off after its faults (Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
                Remove("a fault");
            }
        }

        private void OnUpdate()
        {
            if (!UiDirector.On)
            {
                Remove("the interface is off");
                return;
            }
            Vector2 size = _rect.rect.size;
            if (size != _laidSize)
            {
                _laidSize = size;
                _scale = UiHudRules.Scale(size.x);
                _prompt.Rescale(_scale);
                _building.Rescale(_scale);
                _overlays.Rescale(_scale);
            }
            UiFrame frame = Frame();
            // No face to write with yet: the game's own boxes stay, so a door, a prompt or a right click is never hidden and
            // left blank; said once a city.
            bool write = UiFonts.CanDraw;
            if (!write && !_noFaceSaid)
            {
                _noFaceSaid = true;
                SayNoFace();
            }
            _prompt.Frame(frame, write && UiScreenSwitches.On(WorldKits.PromptId));
            _building.Frame(frame, write && UiScreenSwitches.On(WorldKits.BuildingId));
            float now = Time.unscaledTime;
            if (now >= _switchDue)
            {
                _switchDue = now + SwitchEvery;
                _hoverOn = UiScreenSwitches.On(UiOverlays.HoverId);
                _clickOn = UiScreenSwitches.On(UiOverlays.ClickId);
                _personOn = UiScreenSwitches.On(UiOverlays.PersonId);
                _aimOn = UiScreenSwitches.On(UiOverlays.PointerId);
            }
            _overlays.Frame(frame, write && _hoverOn, write && _clickOn, _personOn);
            EmployeeFollowLive.Tick();
            if (_aimOn)
            {
                TutorialAimLive.Tick(Time.unscaledTime);
            }
            else
            {
                TutorialAimLive.Release();
            }
        }

        private static void SayNoFace() =>
            UiDirector.Logger?.Warn("UI: the world's boxes stay the game's own for now: the kit has no font to write with yet ("
                                    + (UiFonts.Ready ? "the fonts are read" : "the fonts are still being read") + ").");

        private UiFrame Frame() => new UiFrame { Width = _laidSize.x / _scale, Height = _laidSize.y / _scale, Band = UiFrame.BandHeight, Pointer = true };

        // After the game's own move of the tutorial's arrows, which it makes in its Update.
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLate);
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                TutorialAimLive.Late();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
                Remove("a fault");
            }
        }

        private void OnDestroy()
        {
            TutorialAimLive.Release();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ------------------------------------------------------------------------------------------------- shared helpers

        /// <summary>A screen point (px of the screen) as px of the layout.</summary>
        internal static Vector2 ToLayout(Vector2 screen, UiFrame frame)
        {
            UiHudRules.ToLayout(screen.x, screen.y, UnityEngine.Screen.width, UnityEngine.Screen.height, frame.Width, frame.Height, out float x, out float y);
            return new Vector2(x, y);
        }

        /// <summary>The game's words with its rich text taken off, the lines kept; null for none.</summary>
        internal static string Plain(string text) => string.IsNullOrEmpty(text) ? null : MapText.Plain(text);

        /// <summary>The kit's tone for the colour the game gives its words (its green open, red closed, yellow yours, blue for rent).</summary>
        internal static UiRole Tone(Color c)
        {
            if (c.g > 0.55f && c.r < 0.5f && c.b < 0.6f)
            {
                return UiRole.OkText;
            }
            if (c.r > 0.6f && c.g < 0.45f)
            {
                return UiRole.DangerText;
            }
            if (c.r > 0.6f && c.g > 0.5f && c.b < 0.4f)
            {
                return UiRole.WarnText;
            }
            if (c.b > 0.6f && c.r < 0.5f)
            {
                return UiRole.AccentText;
            }
            return UiRole.Text2;
        }

        /// <summary>The game's key for one of its actions, named as its windows name it, a pad's glyph when one leads.</summary>
        internal static UiGameKey Key(PlayerAction action)
        {
            UiAction kit = action == PlayerAction.Interact ? UiAction.Alt : action == PlayerAction.SecondaryInteract ? UiAction.Option
                : action == PlayerAction.Cancel ? UiAction.Back : action == PlayerAction.Confirm ? UiAction.Confirm : UiAction.Count;
            UiPad pad = UiGame.Instance != null ? UiGame.Instance.Pad : UiPad.None;
            if (pad != UiPad.None && kit != UiAction.Count)
            {
                UiIcon glyph = UiKeyGlyphs.For(kit, pad);
                if (glyph != UiIcon.None)
                {
                    return new UiGameKey { Glyph = glyph };
                }
            }
            try
            {
                return new UiGameKey { Name = action.GetDisplayName() };
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                return default(UiGameKey);
            }
        }
    }
}
