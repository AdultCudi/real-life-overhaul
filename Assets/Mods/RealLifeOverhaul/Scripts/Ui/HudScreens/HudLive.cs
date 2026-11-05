using System;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Seasons;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The HUD over the running city, live: the game's top bar, its pause pulse, its job and building panels, the mod's shift
    /// cards and bars, its objectives window, its short notices and its control hints hidden by leaf and mirrored on the HUD
    /// host (the status bar's pills, the side panels at the top right, the objectives at the left, notice cards at the top
    /// centre, key chips over the bar), every press going to the game's own button or method. The HUD shows while the game
    /// shows its own top bar (it hides it for the full menu, the time machine, a building's preview, the mini menu and the
    /// interior designer, and the HUD follows). Each piece has its own switch under the HUD's (hud_side, hud_objectives,
    /// hud_notices); a switch turned off, the interface's switch, a fault or a city unloading give every piece back exactly
    /// as found. While the HUD shows, the kit's phone stands over its bar (and folds down to its head with the game's), the
    /// key chips keep to the phone's left, and the game's notifications start under its side panels. Cost a frame: one
    /// hierarchy test and a clock compare; four times a second a read of what the pieces show (and where the phone stands),
    /// laid out only when that changed.
    /// </summary>
    internal static class HudLive
    {
        private const float LookEvery = 0.25f;    // seconds between reads of the game's pieces while the HUD shows
        private const float BeginEvery = 1f;      // seconds between tries to stand the HUD up while it waits for the city
        public const float LateAfter = 10f;       // seconds a loaded city stands without the HUD before Player.log is told why

        private static HudLiveWatch _watch;
        private static bool _live, _shown, _gameOwn, _lateSaid;
        private static float _cityAt = -1f;       // when the HUD first waited in this loaded city; -1: not waiting
        private static bool _sides, _goals, _notes;
        private static float _beginDue, _lookDue;
        private static int _signature;
        private static float _foot = float.NaN;   // the side panels' foot as the game's notifications were last told it
        private static string _state = "waiting for the city";
        private static readonly UiHudPopupModel[] Popups = new UiHudPopupModel[2];
        private static readonly Vector3[] PhoneCorners = new Vector3[4];

        /// <summary>For the director's line: what the HUD does now.</summary>
        public static string State => _live
            ? "over the game's top bar" + (_sides ? ", the side panels" : string.Empty) + (_goals ? ", the objectives" : string.Empty) + (_notes ? ", the notices" : string.Empty)
            : _state;

        /// <summary>True while the game's pieces are hidden and mirrored.</summary>
        public static bool Live => _live;

        /// <summary>The bar's model as the HUD last laid it out, for a popup to stand over its button.</summary>
        public static UiStatusModel LastBar => HudHost.Instance != null ? HudHost.Instance.Screen.LastBar : null;

        /// <summary>px of the layout: the HUD's frame now (1920 x 1080 unless the UI zoom changed the scale).</summary>
        public static float FrameWidth { get; private set; } = UiFrame.ScreenHeight * 16f / 9f;

        public static float FrameHeight { get; private set; } = UiFrame.ScreenHeight;

        /// <summary>Set by a test: the game's own top bar and panels until it is pressed again (or the city unloads).</summary>
        public static bool GameOwn
        {
            get => _gameOwn;
            set
            {
                _gameOwn = value;
                if (value)
                {
                    StandDown("a test asked for the game's own");
                }
                _beginDue = 0f;
            }
        }

        /// <summary>
        /// The activity's strip while the activity panel draws it on the kit (its own view, ActivityKit.Strip where that
        /// stands), so the HUD's key chips keep clear of it; the HUD never draws it, or it would show twice. Null: none.
        /// </summary>
        public static Func<UiActivityModel> ActivityStrip { get; set; }

        /// <summary>A test's sample in place of what the game shows: the side panels, the objectives or a notice card; null for the game's.</summary>
        public static UiSidePanelModel[] SampleSides;

        public static UiObjectivesModel SampleGoals;

        public static UiHudNoticeModel SampleNote;

        /// <summary>From the director's slow tick while the interface is on: the watch that stands the HUD up.</summary>
        public static void Install()
        {
            if (_watch == null && UiDirector.Instance != null)
            {
                _watch = UiDirector.Instance.gameObject.GetComponent<HudLiveWatch>() ?? UiDirector.Instance.gameObject.AddComponent<HudLiveWatch>();
            }
            if (_watch != null && !_watch.enabled && !HudLiveWatch.Guarded.Off)
            {
                _watch.enabled = true;
            }
        }

        /// <summary>Every piece given back (the interface off, a fault, the mod unloading).</summary>
        public static void Remove(string why)
        {
            StandDown(why);
            if (_watch != null)
            {
                _watch.enabled = false;
            }
        }

        /// <summary>No city: everything given back and every reference forgotten, so the next city is looked at afresh.</summary>
        public static void Rearm()
        {
            StandDown(null);
            HudTopGame.Rearm();
            HudSides.Rearm();
            HudGoals.Rearm();
            HudNotes.Rearm();
            HudHints.Rearm();
            _gameOwn = false;
            SampleSides = null;
            SampleGoals = null;
            SampleNote = null;
            _cityAt = -1f;
            _lateSaid = false;
            _state = "waiting for the city";
        }

        // ------------------------------------------------------------------------------------------------- each frame

        internal static void Tick()
        {
            float now = Time.unscaledTime;
            if (!_live)
            {
                if (now >= _beginDue)
                {
                    _beginDue = now + BeginEvery;
                    Begin();
                }
                return;
            }
            HudHost host = HudHost.Instance;
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (host == null || uis == null || uis.topBar == null)
            {
                StandDown("the game's HUD is gone");
                return;
            }
            bool show = uis.topBar.gameObject.activeInHierarchy;
            if (show != _shown || (show && !host.gameObject.activeSelf))
            {
                // The game hid or showed its top bar (or a test, or the host's own switch, hid the host): the HUD follows it.
                if (!UiDirector.On || !UiScreenSwitches.On("hud"))
                {
                    StandDown("its switch is off");
                    return;
                }
                _shown = show;
                if (show)
                {
                    host.Show();
                }
                else
                {
                    host.Hide();
                }
                HudHints.Shown(show);
                _lookDue = 0f;
                Place(show);
            }
            if (show && now >= _lookDue)
            {
                _lookDue = now + LookEvery;
                Look(host, uis);
            }
        }

        // ------------------------------------------------------------------------------------------------- standing up

        private static void Begin()
        {
            if (!UiDirector.On || !UiScreenSwitches.On("hud") || _gameOwn)
            {
                _state = _gameOwn ? "the game's own, for a test" : "its switch is off";
                return;
            }
            if (SaveGameManager.Current == null || UI.Load.LoadScene.isLoading)
            {
                _cityAt = -1f;
                return;
            }
            if (!UiFonts.Ready)
            {
                Waits("the interface's fonts are not in");
                return;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.topBar == null || uis.playerHUD == null)
            {
                Waits(uis == null ? "the game's interface is not there" : uis.topBar == null ? "the game's top bar is not there" : "the game's HUD is not there");
                return;
            }
            HudHost host = HudHost.Ensure();
            if (host == null)
            {
                Waits("the HUD's own canvas could not be made");
                return;
            }
            if (!HudTopGame.Begin(uis))
            {
                Waits("the game's top bar has no left bar or buttons yet");
                return;   // the game's HUD is built a little after the city: tried again in a second
            }
            HomeNeedsBar.Held = true;    // the needs and the season are pills of the bar now
            SeasonBar.Held = true;
            host.Screen.PiecePressed = Press;
            _live = true;
            _shown = false;
            _signature = 0;
            _lookDue = 0f;
            Switches(uis);
            _state = "over the game's top bar";
            UiDirector.Logger?.Info("UI: the HUD stands over the game's top bar (" + State + "); the game's own pieces are hidden by leaf and kept running"
                                    + (_lateSaid ? ", " + (Time.unscaledTime - _cityAt).ToString("F0") + " s after the city loaded." : "."));
            _cityAt = -1f;
            _lateSaid = false;
        }

        // The HUD could not stand up yet: what it waits for is its state, and said once in Player.log when the city has stood
        // LateAfter seconds without it, so a HUD that never comes is never silent.
        private static void Waits(string what)
        {
            _state = "waiting: " + what;
            float now = Time.unscaledTime;
            if (_cityAt < 0f)
            {
                _cityAt = now;
            }
            if (!_lateSaid && now - _cityAt >= LateAfter)
            {
                _lateSaid = true;
                UiDirector.Logger?.Warn("UI: the HUD has not stood up " + (now - _cityAt).ToString("F0") + " s after the city loaded: " + what
                                        + "; the game's own top bar shows meanwhile, and it is tried again each second.");
            }
        }

        // A piece's switch turned on or off: it hides and mirrors the game's, or gives it back.
        private static void Switches(UIs uis)
        {
            bool sides = UiScreenSwitches.On("hud_side"), goals = UiScreenSwitches.On("hud_objectives"), notes = UiScreenSwitches.On("hud_notices");
            if (sides != _sides)
            {
                _sides = sides && HudSides.Begin(uis);
                if (!sides)
                {
                    HudSides.End();
                }
                NoticeRoom.Room = _sides ? (Func<float>)SideFoot : null;
            }
            if (goals != _goals)
            {
                _goals = goals && HudGoals.Begin(uis);
                if (!goals)
                {
                    HudGoals.End();
                }
            }
            if (notes != _notes)
            {
                _notes = notes && HudNotes.Begin(uis);
                if (!notes)
                {
                    HudNotes.End();
                }
            }
        }

        /// <summary>Everything given back exactly as found; the HUD hidden. Null for a quiet stand down (no city).</summary>
        private static void StandDown(string why)
        {
            if (!_live)
            {
                if (why != null && !_gameOwn)
                {
                    _state = "the game's own (" + why + ")";
                }
                return;   // nothing is held: asked each second while the city or the switch is away
            }
            _live = false;
            _shown = false;
            HudTopGame.End();
            HudSides.End();
            HudGoals.End();
            HudNotes.End();
            HudHints.End();
            _sides = _goals = _notes = false;
            HomeNeedsBar.Held = false;
            SeasonBar.Held = false;
            if (NoticeRoom.Room != null)
            {
                NoticeRoom.Room = null;
            }
            _foot = float.NaN;
            PhoneKit.Lift = 0f;
            HudHost host = HudHost.Instance;
            if (host != null)
            {
                host.Screen.PiecePressed = null;
                Clear(host.Screen.Model);
                host.Hide();
            }
            if (why != null)
            {
                _state = "the game's own (" + why + ")";
                UiDirector.Logger?.Info("UI: the HUD gave the game's top bar and pieces back (" + why + ").");
            }
        }

        private static void Clear(UiHudModel m)
        {
            m.Side = new UiSidePanelModel[0];
            m.Objectives = null;
            m.NoticeCount = 0;
            m.PopupCount = 0;
            m.PromptCount = 0;
            m.ActivityClear = null;
            m.PhoneW = m.PhoneH = 0f;
        }

        // The kit's phone where it stands on the screen (its part above the screen's foot, open or folded) as px of the HUD's
        // layout, so the key chips keep clear of it; nothing while it does not show. Read with the HUD's other pieces, four
        // times a second, into a kept array: nothing allocated. The signature moves by whole px.
        private static int PhoneBox(UiHudModel m)
        {
            RectTransform phone = PhoneKit.Rect;
            m.PhoneW = m.PhoneH = 0f;
            if (phone == null)
            {
                return 0;
            }
            phone.GetWorldCorners(PhoneCorners);   // the phone's canvas is an overlay: world is screen pixels
            float foot = Math.Max(0f, PhoneCorners[0].y), top = Math.Min(Screen.height, PhoneCorners[2].y);
            if (top <= foot)
            {
                return 0;
            }
            UiHudRules.ToLayout(PhoneCorners[0].x, top, Screen.width, Screen.height, FrameWidth, FrameHeight, out float x, out float y);
            UiHudRules.ToLayout(PhoneCorners[2].x, foot, Screen.width, Screen.height, FrameWidth, FrameHeight, out float right, out float bottom);
            m.PhoneX = x;
            m.PhoneY = y;
            m.PhoneW = right - x;
            m.PhoneH = bottom - y;
            return unchecked(((int)x * 31 + (int)y) * 31 + (int)m.PhoneW) | 1;
        }

        // ------------------------------------------------------------------------------------------------- reading

        private static void Look(HudHost host, UIs uis)
        {
            if (!UiDirector.On || !UiScreenSwitches.On("hud"))
            {
                StandDown("its switch is off");
                return;
            }
            Switches(uis);
            if (!_live)
            {
                return;
            }
            host.KeepOver(PhoneKit.Order);   // the bar, its chips and its money's change over the phone's foot, whatever was switched on last
            var size = ((RectTransform)host.transform).rect.size;
            if (host.PxScale > 0f && size.x > 1f)
            {
                FrameWidth = size.x / host.PxScale;
                FrameHeight = size.y / host.PxScale;
            }
            UiHudModel m = host.Screen.Model;
            int box = MinimapBox(m);   // first: the map's dock moves the objectives down under the route strip's row, which they are fitted to
            int popups = 0;
            int sig;
            unchecked
            {
                sig = HudTopGame.Look(m, Popups, ref popups);
                m.Popups = Popups;
                m.PopupCount = popups;
                sig = sig * 31 + (SampleSides != null ? SidesSample(m) : _sides ? HudSides.Look(m) : NoSides(m));
                sig = sig * 31 + (SampleGoals != null ? GoalsSample(m) : _goals ? HudGoals.Look(m, FrameHeight - (m.MapDock ? UiMapChrome.RouteTop - UiTokens.S4 : 0f)) : NoGoals(m));
                sig = sig * 31 + (_notes || SampleNote != null ? HudNotes.Look(m, _notes, SampleNote) : 0);
                sig = sig * 31 + HudHints.Look(m);
                m.ActivityClear = ActivityStrip != null ? ActivityStrip() : null;
                sig = sig * 31 + (m.ActivityClear != null ? 1 + (int)m.ActivityClear.State : 0);
                sig = sig * 31 + PhoneBox(m);
                sig = sig * 31 + box;
            }
            if (sig != _signature)
            {
                _signature = sig;
                host.Screen.Dirty = true;
                Place(true);
            }
        }

        // The minimap's card in the layout's px, for the pieces that keep clear of it; its place is part of what the HUD is laid out from.
        private static int MinimapBox(UiHudModel m)
        {
            UiMinimapBox.Read(FrameWidth, FrameHeight, out m.AvoidX, out m.AvoidY, out m.AvoidW, out m.AvoidH);
            m.MapDock = m.AvoidW > 0f && UiMinimapBox.Dock;
            return unchecked(UiMinimapBox.Stamp(m.AvoidX, m.AvoidY, m.AvoidW, m.AvoidH) * 2 + (m.MapDock ? 1 : 0));
        }

        private static int SidesSample(UiHudModel m)
        {
            m.Side = SampleSides;
            return SampleSides.Length * 7919;
        }

        private static int NoSides(UiHudModel m)
        {
            if (m.Side.Length > 0)
            {
                m.Side = new UiSidePanelModel[0];
            }
            return 0;
        }

        private static int GoalsSample(UiHudModel m)
        {
            m.Objectives = SampleGoals;
            return 104729 + (SampleGoals.Collapsed ? 1 : 0);
        }

        private static int NoGoals(UiHudModel m)
        {
            m.Objectives = null;
            return 0;
        }

        // The phone over the bar and the game's notifications under the side panels, in screen pixels from the foot.
        private static void Place(bool shown)
        {
            float perPx = FrameHeight > 1f ? Screen.height / FrameHeight : 1f;
            PhoneKit.Lift = shown ? (UiFrame.BandHeight + UiTokens.S2) * perPx : 0f;
            float foot = _sides && shown ? SideFoot() : float.NaN;
            bool moved = float.IsNaN(foot) ? !float.IsNaN(_foot) : float.IsNaN(_foot) || Math.Abs(foot - _foot) > 0.5f;
            if (moved)
            {
                _foot = foot;
                NoticeRoom.Moved();
            }
        }

        // The side panels' foot in screen pixels from the screen's foot; NaN while none shows.
        private static float SideFoot()
        {
            HudHost host = HudHost.Instance;
            if (host == null || !_shown)
            {
                return float.NaN;
            }
            UiSidePanelModel[] side = host.Screen.Model.Side;
            if (side == null || side.Length == 0)
            {
                return float.NaN;
            }
            float h = UiSidePanel.StackHeight(side, side.Length);
            float perPx = FrameHeight > 1f ? Screen.height / FrameHeight : 1f;
            return Screen.height - (UiTokens.S4 + h) * perPx;
        }

        // ------------------------------------------------------------------------------------------------- presses

        // A piece's own command, from the HUD's guarded pointer handler: whoever mirrors that piece presses the game's own.
        private static bool Press(int command, int arg)
        {
            bool done;
            if (command == UiDraw.ToolPillCommand)
            {
                done = HudTopGame.PressTool(arg);
            }
            else if (command >= UiHudPopup.RowCommand && command <= UiHudPopup.ActionCommand)
            {
                int popup = command == UiHudPopup.ActionCommand ? arg : arg / 32;
                int row = command == UiHudPopup.ActionCommand ? -1 : arg % 32;
                done = popup >= 0 && popup < Popups.Length && Popups[popup] != null && HudTopGame.PressPopup(Popups[popup], command, row);
            }
            else if (command >= UiSidePanel.FoldCommand && command <= UiSidePanel.SwitchCommand)
            {
                done = SampleSides != null ? SampleFold(command, arg) : HudSides.Press(command, arg);
            }
            else if (command >= UiObjectives.FoldCommand && command <= UiObjectives.CloseCommand)
            {
                done = SampleGoals != null ? SampleFoldGoals(command, arg) : HudGoals.Press(command, arg);
            }
            else if (command == UiHudNotice.LinkCommand || command == UiHudNotice.ActionCommand)
            {
                done = HudNotes.Press(command, arg);
            }
            else
            {
                return false;
            }
            _lookDue = 0f;   // what the press changed is read at once
            return done;
        }

        private static bool SampleFold(int command, int arg)
        {
            if (command != UiSidePanel.FoldCommand || arg < 0 || arg >= SampleSides.Length)
            {
                return command == UiSidePanel.ActionCommand || command == UiSidePanel.ToolCommand || command == UiSidePanel.SwitchCommand;
            }
            SampleSides[arg].Collapsed = !SampleSides[arg].Collapsed;
            HudHost.Instance.Screen.Dirty = true;
            return true;
        }

        private static bool SampleFoldGoals(int command, int arg)
        {
            if (command == UiObjectives.FoldCommand)
            {
                SampleGoals.Collapsed = !SampleGoals.Collapsed;
            }
            else if (command == UiObjectives.GroupCommand && arg >= 0 && arg < SampleGoals.Groups.Length)
            {
                SampleGoals.Groups[arg].Collapsed = !SampleGoals.Groups[arg].Collapsed;
            }
            HudHost.Instance.Screen.Dirty = true;
            return true;
        }
    }

    /// <summary>The HUD's watch on the director's object: <see cref="HudLive.Tick"/> each frame, guarded.</summary>
    internal sealed class HudLiveWatch : MonoBehaviour
    {
        internal static readonly FeatureGuard Guarded = Guard.For("ui", "HudLive", typeof(HudLiveWatch));
        private static readonly int Hot = HotSpots.Slot("Ui: HudLive.Tick");

        private void Update()
        {
            using var hotSpot = HotSpots.Time(Hot);
            if (Guarded.Off)
            {
                HudLive.Remove("its faults");
                return;
            }
            try
            {
                HudLive.Tick();
            }
            catch (Exception e)
            {
                Guard.Fail(Guarded, e, this);
                HudLive.Remove("a fault");
            }
        }
    }
}
