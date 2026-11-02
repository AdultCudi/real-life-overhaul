using System;
using System.Collections.Generic;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The one way the kit draws over a screen of the game's (ContactsKit was its first form). The game's object stays
    /// alive and active underneath with only its own layout leaves unseen and untouchable (UiConceal, given back exactly
    /// as found); the kit screen reads the game's state and presses the game's own buttons, so every rule of the game stays
    /// the game's. The game keeps its full menu and apps switched off until they open, so the bind takes switched off
    /// objects and walks the canvases as the engine nests them (UiBindRules); a screen not built yet is looked at again
    /// later, and one that never looks as expected stays the game's own until the next city, with one Player.log line. A
    /// small watcher on the game's object (UiGameWatch) shows the kit the moment the game opens it. The kit steps aside for
    /// a call or anything only the game's view can show (the classic view, until the screen closes), holds its keys while
    /// a box stands over it, and a fault of its own screen gives the game's screen back for the session. Its canvas stands
    /// just above the screen it covers, below the game's boxes, notices and other mods' canvases. Cost: nothing while the
    /// screen is closed; while it shows, the host's input reads and one box test a frame.
    /// </summary>
    public sealed class UiGameHost
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "UiGameHost", typeof(UiGameHost));
        private static readonly List<UiGameHost> Hosts = new List<UiGameHost>(8);

        private const string NotLooked = "not looked at yet";
        private const string NotBuilt = "waiting for the game to build it";
        private const string Drawn = "drawn over the game's screen";

        /// <summary>Told when a host over a full menu app starts or stops showing: the frame takes the menu's chrome or gives it back.</summary>
        public static Action FacesChanged;

        /// <summary>The frame's answer to the status bar's commands and to a window closed at its top level; null outside the full menu.</summary>
        public static Func<int, int, bool> Chrome;

        private readonly IUiGameScreen _screen;
        private readonly UiConceal _conceal = new UiConceal();
        private Component _found;
        private GameObject _root;
        private UiHost _host;
        private Canvas _canvas;
        private CanvasGroup _group;   // the kit's own fade, held to the full menu's
        private UiGameWatch _watch;
        private int _order;
        private bool _classic, _held, _shown;
        private Func<bool> _back;     // while the game runs something in its own view: true once it is over and the kit may come back
        private bool _gaveUp, _stopped;
        private int _misses;
        private float _nextLook;
        private string _state = NotLooked;

        public UiGameHost(IUiGameScreen screen)
        {
            _screen = screen;
            Hosts.Add(this);
        }

        /// <summary>Where it stands, for the interface's line at load: drawn, waiting, or the game's own and why.</summary>
        public string State => _root != null ? Drawn : _state;

        /// <summary>True once it has an answer for this city: over the screen, waiting to look again, or the game's own.</summary>
        public bool Settled => _root != null || (_state != NotLooked && _state != NotBuilt);

        /// <summary>True while the kit draws the screen.</summary>
        public bool Showing => _root != null && _root.activeSelf && _found != null && _found.gameObject.activeInHierarchy && !_classic;

        /// <summary>The kit screen it runs, or null before the bind.</summary>
        public IUiScreen Screen => _host != null ? _host.Screen : null;

        /// <summary>
        /// The kit's own root over the game's canvas, or null before the bind: its rect is the kit's px times
        /// UiTokens.CanvasPerPx, for a screen that places itself around a piece the game draws (the uniforms' preview).
        /// </summary>
        public RectTransform Root => _root != null ? (RectTransform)_root.transform : null;

        /// <summary>The screen's id in UiScreens.</summary>
        public string Id => _screen.Id;

        /// <summary>True while any host draws an app of the full menu: the frame owns the menu's chrome then.</summary>
        public static bool AnyInFullMenu
        {
            get
            {
                for (int i = 0; i < Hosts.Count; i++)
                {
                    if (Hosts[i]._screen.Kind == UiGameKind.App && Hosts[i].Showing)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>The frame round the full menu's apps changed (the app row's news): each app the kit shows is laid out again.</summary>
        public static void AgainAll()
        {
            for (int i = 0; i < Hosts.Count; i++)
            {
                if (Hosts[i]._screen.Kind == UiGameKind.App && Hosts[i]._host != null)
                {
                    Hosts[i]._host.Again();
                }
            }
        }

        /// <summary>The sort order of the canvas the kit draws an app of the full menu on, so what stands over it (All apps) goes one above.</summary>
        public static int AppOrder()
        {
            int order = 0;
            for (int i = 0; i < Hosts.Count; i++)
            {
                if (Hosts[i]._screen.Kind == UiGameKind.App && Hosts[i]._root != null)
                {
                    order = Math.Max(order, Hosts[i]._order);
                }
            }
            return order;
        }

        /// <summary>Every host's line for Player.log at load.</summary>
        public static string States()
        {
            var sb = new System.Text.StringBuilder(64);
            for (int i = 0; i < Hosts.Count; i++)
            {
                sb.Append(i > 0 ? ", " : string.Empty).Append(Hosts[i]._screen.Name).Append(" (").Append(Hosts[i].State).Append(')');
            }
            return sb.ToString();
        }

        /// <summary>Puts the kit over the game's screen once a city; asked again once a second while its switches are on.</summary>
        public void Install()
        {
            if (_gaveUp || _stopped || FrameGuard.Off)
            {
                return;
            }
            if (_root != null)
            {
                if (_found == null)
                {
                    Remove("the city was unloaded");
                }
                return;
            }
            if (Time.unscaledTime < _nextLook)
            {
                return;
            }
            Component found = _screen.Find();
            if (found == null)
            {
                _state = NotBuilt;
                return;   // the game builds it a little after the city: asked again next second
            }
            string wrong = _screen.Check(found);
            // The game keeps its screens switched off until opened, so the search for the canvases takes switched off ones.
            Canvas[] above = found.GetComponentsInParent<Canvas>(true);
            if (wrong != null || above.Length == 0)
            {
                Miss(wrong ?? "no canvas above it");
                return;
            }
            var overrides = new bool[above.Length];
            for (int i = 0; i < above.Length; i++)
            {
                overrides[i] = above[i].overrideSorting;
            }
            Canvas root = above[UiBindRules.Root(above.Length)];
            // A box stands one more above, so it covers the frame the kit draws just over the full menu too.
            _order = UiHostRules.Above(above[UiBindRules.Sorter(overrides, above.Length)].sortingOrder) + (_screen.Kind == UiGameKind.Box ? 1 : 0);
            _found = found;
            _screen.Conceal(found, _conceal);
            _root = new GameObject("RealLife Interface " + _screen.Id, typeof(RectTransform));
            var rect = (RectTransform)_root.transform;
            rect.SetParent(root.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _canvas = _root.AddComponent<Canvas>();
            _group = _root.AddComponent<CanvasGroup>();
            Sort();
            _root.AddComponent<GraphicRaycaster>();
            if (_screen.Kind != UiGameKind.Overlay)
            {
                // Takes the clicks no target of the kit's takes: the game's pieces it hides under it stay pressable by alpha alone.
                var blocker = _root.AddComponent<Image>();
                blocker.color = new Color(0f, 0f, 0f, 0f);
            }
            _host = _root.AddComponent<UiHost>();
            UiGame input = _screen.Kind == UiGameKind.Box ? UiGame.OverBoxes : UiGame.Instance;
            _host.Bind(_screen.Make(found), input, input, UiDirector.Dark, UiDirector.Motion, UiFrame.MastheadHeight, UiFrame.BandHeight, _screen.Title, _screen.Icon);
            _host.PointerOnly = _screen.Kind == UiGameKind.Overlay;
            _host.View.Scale = UiTokens.CanvasPerPx;
            _host.Failed = Fallback;
            if (_screen.Kind == UiGameKind.App)
            {
                _host.Chrome = Chrome;
            }
            _root.SetActive(false);
            if (UiDirector.Logger != null && _misses > 0)
            {
                UiDirector.Logger.Info("UI: " + _screen.Name + " is drawn on the RLO interface now, after " + _misses + " look(s) that found it not ready.");
            }
            _misses = 0;
            _state = Drawn;
            _watch = found.gameObject.AddComponent<UiGameWatch>();   // on an open screen its OnEnable shows the kit at once
            _watch.Host = this;
        }

        /// <summary>A new city: a screen that never looked as expected in the last one is looked at again.</summary>
        public void Rearm()
        {
            _stopped = false;
            _misses = 0;
            _nextLook = 0f;
            if (!_gaveUp)
            {
                _state = NotLooked;
            }
        }

        /// <summary>The game's screen as it was: the kit gone, its leaves seen and touchable again.</summary>
        public void Remove(string why)
        {
            if (_root == null && _found == null)
            {
                return;
            }
            try
            {
                _conceal.Clear();
                if (_watch != null)
                {
                    UnityEngine.Object.Destroy(_watch);
                }
                if (_root != null)
                {
                    UnityEngine.Object.Destroy(_root);
                }
            }
            catch (Exception e)
            {
                UiDirector.Logger?.Error(e);
            }
            _root = null;
            _host = null;
            _found = null;
            _canvas = null;
            _group = null;
            _watch = null;
            _classic = false;
            _held = false;
            _back = null;
            Shown(false);
            if (UiDirector.Logger != null && why != null && Meta.Dev.Verbose)
            {
                UiDirector.Logger.Info("UI: " + _screen.Name + " is the game's own again (" + why + ").");
            }
        }

        /// <summary>The game's own view until the screen closes: an offer with its buttons, a table only it can show, another mod's page.</summary>
        public void Classic()
        {
            _classic = true;
            _back = null;
            _conceal.Show();
            if (_root != null)
            {
                _host.Hide();
                _root.SetActive(false);
            }
            Shown(false);
        }

        /// <summary>
        /// Something the game runs in its own view (a call): the kit steps aside so it is seen and answered there, and comes
        /// back the moment <paramref name="over"/> says it is over (the game freed its call button again).
        /// </summary>
        public void StepAside(Func<bool> over)
        {
            Classic();
            _back = over;
        }

        /// <summary>The keys go to something of the kit's standing over this screen (a box, All apps); the screen stays drawn as it is.</summary>
        public void Hold(bool hold)
        {
            if (_held == hold)
            {
                return;
            }
            _held = hold;
            if (_host != null && _root != null && _root.activeSelf)
            {
                _host.Hold(hold);
            }
        }

        /// <summary>A fault in the kit's screen (its host has counted it): the game's screen back for the rest of the session.</summary>
        public void Fallback(Exception e)
        {
            UiDirector.Logger?.Warn("UI: " + UiText.T("reallife_ui_note_fallback") + " (" + _screen.Name + ", " + (e != null ? e.GetType().Name : "?") + ")");
            Remove("a fault");
            _gaveUp = true;
            _state = "the game's own after a fault of the kit's screen";
        }

        internal void Opened()
        {
            if (_root == null || _classic)
            {
                return;
            }
            try
            {
                Bring();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, null);
                Fallback(e);
            }
        }

        internal void Closed()
        {
            _classic = false;   // the classic view lasts until the screen closes, and the next opening is the kit's again
            _back = null;
            if (_root == null)
            {
                return;
            }
            _host.Hide();
            _root.SetActive(false);
            _conceal.Show();
            Shown(false);
        }

        // A frame while the game's screen shows: a call handed back, the menu's fade followed, a box over it.
        internal void Watch()
        {
            if (_back != null && _back())
            {
                _back = null;
                _classic = false;
                if (_root != null)
                {
                    Bring();
                    UiDirector.Logger?.Info(_screen.Name + " (kit): the game's own view is done, the kit is back");
                }
            }
            if (_root == null || _classic)
            {
                return;
            }
            if (_screen.Kind != UiGameKind.App)
            {
                return;
            }
            if (Fade() && _host.enabled)
            {
                _host.Hide();   // the menu is closing (View on map does that on its way to the map): no key acts on a screen leaving
            }
            // A box of the game's own over the screen steps the kit aside so it is seen and answered; one the kit draws only holds the keys.
            bool own = UiBoxes.Up && !UiBoxes.Drawn;
            if (_root.activeSelf == own)
            {
                _root.SetActive(!own);
                if (!own)
                {
                    _host.Show();
                }
            }
            Hold(UiBoxes.Up || UiBoxes.Over);
        }

        // Shows the kit over the game's screen, its leaves hidden, arriving.
        private void Bring()
        {
            _conceal.Hide();
            Sort();
            if (_screen.Kind == UiGameKind.App)
            {
                Fade();
            }
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            _host.Show();
            if (_held)
            {
                _host.Hold(true);
            }
            Shown(true);
        }

        // A box the kit draws holds the keys of the screens under it (UiBoxes.Drawn); an app tells the frame to take the menu's chrome.
        private void Shown(bool shown)
        {
            if (_shown == shown)
            {
                return;
            }
            _shown = shown;
            if (_screen.Kind == UiGameKind.Box)
            {
                bool any = false;
                for (int i = 0; i < Hosts.Count; i++)
                {
                    any |= Hosts[i]._screen.Kind == UiGameKind.Box && Hosts[i]._shown;
                }
                UiBoxes.Drawn = any;
            }
            else if (_screen.Kind == UiGameKind.App)
            {
                FacesChanged?.Invoke();
            }
        }

        // The game fades its full menu in and out, and View on map closes it on the way to the map. The kit stands on a
        // canvas of its own, so it takes the menu's fade to come and go with it rather than hang on over the map. True
        // while the menu is closing.
        private bool Fade()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            CanvasGroup menu = uis != null && uis.fullMenu != null ? uis.fullMenu.canvasGroup : null;
            if (_group != null && menu != null && !Mathf.Approximately(_group.alpha, menu.alpha))
            {
                _group.alpha = menu.alpha;
            }
            return uis != null && uis.fullMenu != null && !global::UI.Smartphone.FullMenu.IsOpen;
        }

        // Set again each time the kit shows: a canvas made under a switched off parent is only nested once that parent comes on.
        private void Sort()
        {
            if (_canvas != null)
            {
                _canvas.overrideSorting = true;
                _canvas.sortingOrder = _order;
            }
        }

        // Often the screen is only not built yet, so it is looked at again a little later, a few times a city.
        private void Miss(string why)
        {
            _misses++;
            if (UiBindRules.Stops(_misses))
            {
                _stopped = true;
                _state = "the game's own for this city (" + why + ")";
                GameUi.UiClone.Fallback(_screen.Name + " is not built as this version expects (" + why + "), so it stays the game's own until the next city");
                return;
            }
            float wait = UiBindRules.Wait(_misses);
            _nextLook = Time.unscaledTime + wait;
            _state = "waiting: " + why + ", looked at " + _misses + " time(s)";
            if (_misses == 1)
            {
                UiDirector.Logger?.Info("UI: " + _screen.Name + " is not ready for the RLO interface yet (" + why + "); looked at again in "
                    + wait.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " s.");
            }
        }
    }
}
