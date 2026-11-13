using System;
using System.Collections.Generic;
using Extensions;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A world prompt the HUD keeps beside its point of the world: its model, where its point is, and who answers its actions.</summary>
    public sealed class HudFollower
    {
        public UiWorldPromptModel Model;
        public Func<Vector3> Point;        // the place in the world it stands over, read once a frame
        public Action<int> Pressed;        // an action's index, pressed with the mouse; the game reads its keys itself
        public bool Dirty = true;          // its model changed: laid out again at the next frame

        // A follower of a look of its own (a district's name and figure on the map): drawn by this instead of the world prompt's layout, in a box of this size, and kept off
        // the rectangle given in px of the layout (a docked panel) when there is one.
        public Action<UiScene> Draw;
        public float Width, Height;
        public float AvoidX, AvoidY, AvoidW, AvoidH;

        internal UiView View;
        internal readonly UiScene Scene = new UiScene();
        internal Vector2 At = new Vector2(float.NaN, float.NaN);
    }

    /// <summary>
    /// The HUD host: the kit's pieces over the running game, on a canvas of its own nested in the game's HUD
    /// canvas (UiOverCanvas), so the game's scaler (3840 x 2160, its UI zoom) scales it as it scales the game's own HUD, and
    /// sorted just above the HUD and below the game's question boxes, notices and other mods' canvases. Three kinds of piece:
    /// - the HUD itself (HudScreen: the status bar and the pieces docked at the edges), one view laid out only when its
    ///   model or what the pills show changes (HudStatusGame.Signature), taking the mouse and never a key;
    /// - world prompts (HudFollower), each a small view moved, never laid out again, to beside its point: one
    ///   WorldToScreenPoint a frame each (as the game's OverlayUI and BuildingResume do), clamped inside the screen above
    ///   the bar and off the left column (UiHudRules.Beside);
    /// - one modal piece at a time (a picker, an activity's card, the time skip veil) on a UiHost, which takes the keys
    ///   while it shows, as the game's own centred panels do.
    /// The money's change rises from the money pill and fades (UiMoneyFlow, UiHudRules.ChangeLift). The UI zoom holds the
    /// layout at least UiHudRules.LeastWidth wide. Cost a frame while it shows: one comparison of the pills' signature, one
    /// size comparison, one projection per world prompt, two writes while the money's change moves; nothing allocated.
    /// While hidden it sleeps.
    /// </summary>
    public sealed class HudHost : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "HudHost", typeof(HudHost));   // its Update (Core/Guard)
        private static readonly int HotUpdate = HotSpots.Slot("Ui: HudHost.Update");
        private const float AppsEvery = 5f;   // seconds between reads of the row's apps while walking: their news change slowly, the walk costs a few dozen buttons

        private readonly UiScene _scene = new UiScene();
        private readonly HudScreen _screen = new HudScreen();
        private readonly List<HudFollower> _followers = new List<HudFollower>(2);
        private readonly UiMoneyFlow _flow = new UiMoneyFlow();
        private readonly UiScene _changeScene = new UiScene();
        private UiView _view, _change;
        private CanvasGroup _changeGroup;
        private float _changeAt = -1f;
        private RectTransform _rect;
        private Vector2 _laidSize;
        private float _scale = UiTokens.CanvasPerPx;
        private int _signature;
        private GameObject _modalRoot;
        private Canvas _modalCanvas;
        private UiHost _modal;
        private float _leftX, _leftY, _leftW, _leftH;   // the left column, which a world prompt keeps off: worked out at each layout
        private bool _hasColumn;
        private float _columnRight, _columnBottom;         // the left column alone, in canvas units from the screen's foot: what the minimap is placed under
        private float _chipsLeft, _chipsTop = -1f;         // the key chips over the bar's right end, in the same units: what the minimap is placed over
        private float _keysX, _keysY, _keysW, _keysH;      // the key chips again, as shares of the screen from its top left: what the item panel keeps clear of; 0 wide for none
        private AllAppsScreen _allApps;
        private float _appsDue;                         // when the app row's list is read again from the game while walking

        /// <summary>The one HUD host, while it stands.</summary>
        public static HudHost Instance { get; private set; }

        /// <summary>The HUD's own screen: set its model's pieces, then <see cref="HudScreen.Dirty"/>.</summary>
        public HudScreen Screen => _screen;

        /// <summary>The scene last laid out, for the checks and the test that reports it.</summary>
        public UiScene Scene => _scene;

        /// <summary>The layout's px scale now, for a test's report: the kit's 2 unless the UI zoom made it smaller.</summary>
        public float PxScale => _scale;

        /// <summary>
        /// The host over the game's HUD, made the first time it is asked for in a city; null while the game's HUD is not
        /// built yet. The canvases above the HUD are walked as the engine nests them, switched off ones too.
        /// </summary>
        public static HudHost Ensure()
        {
            if (Instance != null)
            {
                return Instance;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            RectTransform rect = UiOverCanvas.Make(uis != null ? uis.playerHUD : null, "RealLife Interface HUD");
            if (rect == null)
            {
                return null;   // the HUD is built a little after the city: asked again at the next press
            }
            HudHost host = rect.gameObject.AddComponent<HudHost>();
            host.Build(rect);
            return host;
        }

        /// <summary>The host gone and the game's HUD as it was (the kit only drew over it), for a city unloading or a fault.</summary>
        public static void Remove(string why)
        {
            if (Instance == null)
            {
                return;
            }
            HudHost host = Instance;
            Instance = null;
            if (host._modal != null)
            {
                host._modal.Hide();
            }
            HudStatusGame.Shows = false;
            HudStatusGame.AllAppsOpen = false;
            Destroy(host.gameObject);
            if (Meta.Dev.Verbose && why != null)
            {
                UiDirector.Logger?.Info("UI: the HUD host is gone (" + why + ").");
            }
        }

        private void Build(RectTransform rect)
        {
            Instance = this;
            _rect = rect;
            _canvas = rect.GetComponent<Canvas>();
            _order = _canvas != null ? _canvas.sortingOrder : 0;
            _view = new UiView(rect, "Hud") { Scale = _scale };
            _view.PointerEntered = Guard.Wrap<int>(PointerEntered);
            _view.PointerExited = Guard.Wrap<int>(PointerExited);
            _view.PointerPressed = Guard.Wrap<int>(PointerPressed);
            _view.Ring = false;   // the HUD takes no key, so the keys' ring never shows on it
            _change = new UiView(rect, "MoneyChange") { Scale = _scale };
            _changeGroup = _change.Root.gameObject.AddComponent<CanvasGroup>();
            _changeGroup.alpha = 0f;
            _screen.AllApps = OpenAllApps;   // pressed from the guarded pointer handler
        }

        private Canvas _canvas;
        private int _order;   // the order it was made at, over the game's HUD (UiOverCanvas)

        /// <summary>The HUD kept drawn over the kit phone, whose canvas is at <paramref name="phone"/> (UiHostRules.OverPhone).</summary>
        public void KeepOver(int phone)
        {
            int want = UiHostRules.OverPhone(_order, phone);
            if (_canvas != null && _canvas.sortingOrder != want)
            {
                _canvas.sortingOrder = want;
            }
            SortModal();
        }

        // The modal piece is a window: its canvas stands over the minimap's card, which is over the rest of the HUD (UiHostRules.ModalOrder).
        private void SortModal()
        {
            int want = UiHostRules.ModalOrder(CanvasOrder);
            if (_modalCanvas != null && _modalCanvas.sortingOrder != want)
            {
                _modalCanvas.sortingOrder = want;
            }
        }

        /// <summary>The HUD shows: laid out fresh, then kept current while it shows.</summary>
        public void Show()
        {
            gameObject.SetActive(true);
            enabled = true;
            HudStatusGame.Shows = true;
            _appsDue = 0f;
            _flow.Reset();
            _laidSize = Vector2.zero;
            _view.Open();
        }

        /// <summary>The HUD hidden: its followers and a modal piece too; it sleeps.</summary>
        public void Hide()
        {
            EndModal();
            TooltipKit.MoneyOut();
            _view.Still();
            HudStatusGame.Shows = false;
            gameObject.SetActive(false);
        }

        /// <summary>A world prompt kept beside its point while it stands.</summary>
        public HudFollower Follow(UiWorldPromptModel model, Func<Vector3> point, Action<int> pressed)
        {
            var f = new HudFollower { Model = model, Point = point, Pressed = pressed };
            f.View = new UiView(_rect, "Prompt") { Scale = _scale, Ring = false };
            f.View.PointerPressed = Guard.Wrap<int>(node => FollowerPressed(f, node));
            f.View.PointerEntered = Guard.Wrap<int>(node => f.View.SetHover(node));
            f.View.PointerExited = Guard.Wrap<int>(node => f.View.SetHover(-1));
            _followers.Add(f);
            return f;
        }

        /// <summary>
        /// A small piece of a look of its own kept beside its point (a name and a figure over a district of the map): <paramref name="draw"/> lays it out once into a box of
        /// <paramref name="width"/> by <paramref name="height"/> px, and it stays off the rectangle given in px of the layout. It takes no press.
        /// </summary>
        public HudFollower Follow(float width, float height, Action<UiScene> draw, Func<Vector3> point, float avoidX, float avoidY, float avoidW, float avoidH)
        {
            var f = new HudFollower { Width = width, Height = height, Draw = draw, Point = point, AvoidX = avoidX, AvoidY = avoidY, AvoidW = avoidW, AvoidH = avoidH };
            f.View = new UiView(_rect, "Tag") { Scale = _scale, Ring = false };
            _followers.Add(f);
            return f;
        }

        /// <summary>A world prompt taken away.</summary>
        public void Unfollow(HudFollower f)
        {
            if (f != null && _followers.Remove(f) && f.View != null)
            {
                Destroy(f.View.Root.gameObject);
            }
        }

        /// <summary>A piece that takes the keys while it shows (a picker, an activity's card, the veil), over the HUD; one at a time.</summary>
        public UiHost Modal(IUiScreen screen, string title, UiIcon icon, Action lost = null)
        {
            _modalLost = lost;
            if (_modalRoot == null)
            {
                _modalRoot = new GameObject("Modal", typeof(RectTransform));
                var r = (RectTransform)_modalRoot.transform;
                r.SetParent(_rect, false);
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = r.offsetMax = Vector2.zero;
                _modal = _modalRoot.AddComponent<UiHost>();
                _modalCanvas = _modalRoot.AddComponent<Canvas>();
                _modalCanvas.overrideSorting = true;
                _modalRoot.AddComponent<GraphicRaycaster>();   // a canvas of its own takes the pointer through a raycaster of its own
                UiRoots.Take(_modalRoot);
                _modal.Failed = ModalFailed;
            }
            SortModal();
            _modal.Bind(screen, UiGame.Instance, UiGame.Instance, UiDirector.Dark, UiDirector.Motion, UiFrame.MastheadHeight, UiFrame.BandHeight, title, icon);
            _modal.View.Scale = _scale;
            _modalRoot.SetActive(true);
            _modal.Show();
            _screen.Dirty = true;   // the modal piece draws the bar with its own prompts: the HUD's view leaves its own out meanwhile
            return _modal;
        }

        // The modal piece's host failed, or the hosts stopped: the piece ends as it closes, and whoever opened it gives the game's own back.
        private void ModalFailed(Exception e)
        {
            Action lost = _modalLost;
            _modalLost = null;
            EndModal();
            try
            {
                lost?.Invoke();
            }
            catch (Exception again)
            {
                UiDirector.Logger?.Error(again);
            }
        }

        private Action _modalLost;   // what the piece on show wants done if its host fails under it; null for a piece that only closes

        /// <summary>The modal piece closed: the HUD's own bar back.</summary>
        public void EndModal()
        {
            if (_modal == null || !_modalRoot.activeSelf)
            {
                return;
            }
            _modal.Hide();
            _modalRoot.SetActive(false);
            HudStatusGame.AllAppsOpen = false;
            _screen.Dirty = true;
        }

        // The All apps tile on the walking bar: the frame's own All apps (the shell's screen, its folders and its presses)
        // over the city on the HUD's modal host; an app pressed there opens as the full menu's row opens it.
        private bool OpenAllApps()
        {
            if (!UiScreenSwitches.On("all_apps"))
            {
                return false;
            }
            if (_allApps == null)
            {
                _allApps = new AllAppsScreen { Close = EndModal };
                _allApps.Opening = id =>
                {
                    EndModal();
                    return ShellApps.Press(id);
                };
            }
            UiShellApps.Refresh();
            _allApps.Show(-1, null);
            Modal(_allApps, UiText.T("reallife_ui_shell_all_apps"), UiIcon.Grid);
            HudStatusGame.AllAppsOpen = true;
            return true;
        }

        private bool ModalUp => _modalRoot != null && _modalRoot.activeSelf;

        /// <summary>The host of the modal piece while one is up, else null: whoever opened a piece asks whether it is still the one on show before it ends it.</summary>
        public UiHost ModalHost => ModalUp ? _modal : null;

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
                UiDirector.PieceFailed(UiDirector.HudPiece, e);   // off until the next city: the HUD's watch made a new host each second, which met the same fault
            }
        }

        private void OnUpdate()
        {
            if (!UiDirector.On || !UiScreenSwitches.On("hud"))
            {
                Hide();   // the interface or the HUD's own switch turned off: the game's own HUD alone, at once
                return;
            }
            Vector2 size = _rect.rect.size;
            if (size != _laidSize)
            {
                Rescale(size);
            }
            if (!UiShell.Taken && Time.unscaledTime >= _appsDue)
            {
                // The row's apps and their news, read from the game's own buttons a few times a minute while walking.
                _appsDue = Time.unscaledTime + AppsEvery;
                _screen.Dirty |= UiShellApps.Refresh();
            }
            HudFps.Tick(Time.unscaledTime);
            int signature = HudStatusGame.Signature();
            if (signature != _signature || _screen.Dirty)
            {
                _signature = signature;
                Layout();
            }
            Money();
            for (int i = 0; i < _followers.Count; i++)
            {
                Place(_followers[i]);
            }
        }

        // The canvas changed size (a resolution, the UI zoom): the px scale again, and every view laid out at it.
        private void Rescale(Vector2 size)
        {
            _laidSize = size;
            _scale = UiHudRules.Scale(size.x);
            _view.Scale = _scale;
            _change.Scale = _scale;
            for (int i = 0; i < _followers.Count; i++)
            {
                _followers[i].View.Scale = _scale;
                _followers[i].Dirty = true;
            }
            if (_modal != null)
            {
                _modal.View.Scale = _scale;
            }
            _screen.Dirty = true;
        }

        // The mouse leads, but while the game has control hints to show the keys lead the frame, so they stand as key chips (HudHints).
        private UiFrame Frame() => new UiFrame { Width = _laidSize.x / _scale, Height = _laidSize.y / _scale, Band = UiFrame.BandHeight, Pointer = !HudHints.Chips };

        private void Layout()
        {
            UiFrame frame = Frame();
            if (ModalUp)
            {
                // The modal piece draws the bar with its prompts; the HUD's own pieces stay, its bar steps aside for the time.
                _scene.Begin(_screen.Id, frame.Width, frame.Height, true);
                _scene.Floats = true;
                UiHud.Draw(_scene, _screen.Model, frame, HudHints.Wrap(UiGame.Instance.Keys), false);
                UiHud.OnBar(_scene, _screen.Model, frame);
                _screen.Dirty = false;
            }
            else
            {
                _screen.Lay(_scene, frame, true, HudHints.Wrap(UiGame.Instance.Keys));
            }
            _view.Write(_scene, false);
            UiHud.LeftColumn(_screen.Model, out _leftX, out _leftY, out _leftW, out _leftH);
            UiHud.LeftColumn(_screen.Model, false, out float cx, out float cy, out float cw, out float ch);
            _columnRight = cw > 0f ? (cx + cw) * _scale : 0f;
            _hasColumn = cw > 0f;
            _columnBottom = _laidSize.y - (cy + ch) * _scale;
            bool chips = UiHud.Chips(_scene, _screen.Model, Frame(), HudHints.Wrap(UiGame.Instance.Keys), out float kx, out float ky, out float kw, out float kh);
            _chipsLeft = chips ? kx * _scale : 0f;
            _chipsTop = chips ? _laidSize.y - ky * _scale : -1f;
            bool shares = chips && frame.Width > 0f && frame.Height > 0f;
            _keysX = shares ? kx / frame.Width : 0f;
            _keysY = shares ? ky / frame.Height : 0f;
            _keysW = shares ? kw / frame.Width : 0f;
            _keysH = shares ? kh / frame.Height : 0f;
        }

        /// <summary>
        /// The key chips over the bar (the game's control hints while the keys lead) in px of another kit layout of the given
        /// size, from its top left, for a piece on a canvas of its own (the item panel) to keep clear of; all zero while the
        /// HUD is not on screen or shows no chips. Whole px, rounded outwards. Read where a host looks at the game a few
        /// times a second; nothing is allocated.
        /// </summary>
        public static void KeysBox(float layoutW, float layoutH, out float x, out float y, out float w, out float h)
        {
            x = y = w = h = 0f;
            HudHost host = Instance;
            if (host == null || layoutW <= 0f || layoutH <= 0f || host._keysW <= 0f || host._keysH <= 0f || !host.isActiveAndEnabled || !HudStatusGame.Shows
                || host._laidSize.x <= 0f)
            {
                return;
            }
            float left = Mathf.Floor(host._keysX * layoutW), top = Mathf.Floor(host._keysY * layoutH);
            x = left;
            y = top;
            w = Mathf.Ceil((host._keysX + host._keysW) * layoutW) - left;
            h = Mathf.Ceil((host._keysY + host._keysH) * layoutH) - top;
        }

        /// <summary>The HUD's order, which the minimap's canvas is set over (HudHost.KeepOver may lift it over the phone).</summary>
        public int CanvasOrder => _canvas != null ? _canvas.sortingOrder : _order;

        /// <summary>
        /// Where the HUD stands for the minimap, in canvas units from the screen's foot (the game's HUD is laid out for 3840 x
        /// 2160 and so is this canvas): the bottom bar's top as the layout puts it, and the left column's right and lower
        /// edges (which may lie under the bar when the column is long), and the key chips' left and upper edges (the upper below 0 for none). False
        /// while the HUD is not on screen. Read on the minimap's slow tick.
        /// </summary>
        public bool Space(out float barTop, out bool hasColumn, out float columnRight, out float columnBottom, out float chipsLeft, out float chipsTop)
        {
            barTop = Frame().Band * _scale;
            hasColumn = _hasColumn;
            columnRight = _columnRight;
            columnBottom = _columnBottom;
            chipsLeft = _chipsLeft;
            chipsTop = _chipsTop;
            return isActiveAndEnabled && HudStatusGame.Shows && _laidSize.x > 0f;
        }

        // The money's change: fed the money each frame, one change at a time rising from its pill and fading.
        private void Money()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            _flow.Feed(save.Money, save.Day * 1440 + save.Hour * 60 + (int)save.Minute);
            float now = Time.unscaledTime;
            if (_changeAt >= 0f)
            {
                float age = now - _changeAt;
                if (!UiHudRules.ChangeShows(age))
                {
                    _changeAt = -1f;
                    _changeGroup.alpha = 0f;
                    return;
                }
                _changeGroup.alpha = UiHudRules.ChangeAlpha(age);
                _change.Root.anchoredPosition = new Vector2(0f, UiHudRules.ChangeLift(age) * _scale);
                return;
            }
            if (_flow.Waiting > 0 && _screen.LastBar != null && _flow.Next(out double amount))
            {
                ShowChange((float)amount);
            }
        }

        private void ShowChange(float amount)
        {
            UiFrame frame = Frame();
            string words = amount >= 0f ? "+" + amount.ToShortCurrencyFormat() : "-" + Math.Abs(amount).ToShortCurrencyFormat();
            UiHud.MoneyChangePlace(_screen.LastBar, frame, out float right, out float bottom);
            _changeScene.Begin("money change", frame.Width, frame.Height, true);
            UiDraw.MoneyChange(_changeScene, UiAt.Screen, right, bottom, words, amount >= 0f);
            _change.Write(_changeScene, false);
            _change.Root.anchoredPosition = Vector2.zero;
            _changeGroup.alpha = 1f;
            _changeAt = Time.unscaledTime;
        }

        // A world prompt beside its point: laid out when its model changed, then only moved; one projection a frame.
        private void Place(HudFollower f)
        {
            if ((f.Model == null && f.Draw == null) || f.Point == null)
            {
                return;
            }
            float width = f.Draw != null ? f.Width : UiWorldPrompt.Width;
            float h = f.Draw != null ? f.Height : UiWorldPrompt.Height(f.Model);
            if (f.Dirty)
            {
                f.Scene.Begin("world prompt", width, h, true);
                f.Scene.Floats = true;
                if (f.Draw != null)
                {
                    f.Draw(f.Scene);
                }
                else
                {
                    UiWorldPrompt.Lay(f.Scene, UiAt.Screen, f.Model, 0f, 0f);
                }
                f.View.Write(f.Scene, false);
                f.Dirty = false;
            }
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                return;
            }
            Vector3 screen = camera.WorldToScreenPoint(f.Point());
            UiFrame frame = Frame();
            UiHudRules.ToLayout(screen.x, screen.y, UnityEngine.Screen.width, UnityEngine.Screen.height, frame.Width, frame.Height, out float px, out float py);
            bool own = f.AvoidW > 0f;   // a docked panel of its own to keep off, else the HUD's left column
            UiHudModel hud = _screen.Model;
            float ax = own ? f.AvoidX : _leftX, ay = own ? f.AvoidY : _leftY, aw = own ? f.AvoidW : _leftW, ah = own ? f.AvoidH : _leftH;
            if (own && hud.MapDock)
            {
                // The map's dock where it stands now (dragged or not) and the objectives beside it, which the dock has pushed over: one box to keep off.
                ax = hud.AvoidX;
                ay = hud.AvoidY;
                aw = hud.AvoidW;
                ah = hud.AvoidH;
                if (_leftW > 0f && _leftX > UiHud.Left)
                {
                    aw = Math.Max(aw, _leftX + _leftW + UiTokens.S2 - ax);
                    ah = Math.Max(ah, _leftY + _leftH + UiTokens.S2 - ay);
                }
            }
            UiHudRules.Beside(px, py, screen.z > 0f, width, h, frame.Width, frame.BandY, ax, ay, aw, ah, out float x, out float y);
            UiHudRules.Clear(ref x, ref y, width, h, frame.Width, hud.AvoidX, hud.AvoidY, hud.AvoidW, hud.AvoidH);   // and off the minimap's card
            var at = new Vector2(x * _scale, -y * _scale);
            if (at != f.At)
            {
                f.At = at;
                f.View.Root.anchoredPosition = at;
            }
        }

        private void FollowerPressed(HudFollower f, int node)
        {
            if (node < 0 || node >= f.Scene.NodeCount)
            {
                return;
            }
            ref UiNode n = ref f.Scene.Nodes[node];
            if (n.Disabled || f.Pressed == null)
            {
                UiGame.Instance.Play(UiCue.Error);
                UiRefusalLog.Say("the HUD (a card over the world)", f.Scene, node, true);
                return;
            }
            f.View.Press(Time.unscaledTime);
            f.Pressed(n.Arg);
            UiGame.Instance.Play(UiCue.Confirm);
        }

        // A hover only lifts the target's face: no ring, no sound, as everywhere the mouse leads.
        private void PointerEntered(int node)
        {
            if (node >= 0 && node < _scene.NodeCount)
            {
                _view.SetHover(node);
                ref UiNode over = ref _scene.Nodes[node];
                if (over.Command == UiDraw.MoneyPillCommand && _laidSize.x > 0f && _laidSize.y > 0f)
                {
                    TooltipKit.Money(over.X * _scale / _laidSize.x, over.Y * _scale / _laidSize.y, over.W * _scale / _laidSize.x);   // the net worth card over the pill
                }
                else if (over.Command == UiDraw.ToolPillCommand && _laidSize.x > 0f && _laidSize.y > 0f)
                {
                    string why = HudTopGame.ToolNote(over.Arg);
                    if (why != null)
                    {
                        TooltipKit.Note(over.X * _scale / _laidSize.x, over.Y * _scale / _laidSize.y, over.W * _scale / _laidSize.x, why);   // why the game holds the button
                    }
                }
                else if (over.Command == UiDraw.TimePillCommand && over.Arg == UiDraw.ClockArg && _laidSize.x > 0f && _laidSize.y > 0f)
                {
                    TooltipKit.Years(over.X * _scale / _laidSize.x, over.Y * _scale / _laidSize.y, over.W * _scale / _laidSize.x);   // the years and days, as the game's date says them on hover
                }
            }
        }

        private void PointerExited(int node)
        {
            _view.SetHover(-1);
            TooltipKit.MoneyOut();
        }

        private void PointerPressed(int node)
        {
            if (node < 0 || node >= _scene.NodeCount)
            {
                return;
            }
            TooltipKit.MoneyOut();
            ref UiNode n = ref _scene.Nodes[node];
            if (n.Disabled)
            {
                UiGame.Instance.Play(UiCue.Error);
                UiRefusalLog.Say("the HUD", _scene, node, true);
                return;
            }
            _view.Press(Time.unscaledTime);
            UiRefusals.Begin();
            bool done = _screen.Press(n.Command, n.Arg);
            if (!done)
            {
                UiRefusalLog.Say("the HUD", _scene, node, false);
            }
            UiGame.Instance.Play(done ? UiCue.Confirm : UiCue.Error);
            if (_screen.Dirty)
            {
                Layout();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
