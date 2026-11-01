using System;
using UI;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's Contacts app drawn on the RLO interface: the game's app stays alive under the kit with its own layout
    /// unseen and untouchable, the kit's screen (ContactsScreen) reads the game's contacts and messages and presses the
    /// game's own buttons, so every rule of the game stays the game's. The kit covers the whole full menu, masthead and
    /// prompt band included, just above the menu's own canvas, and steps aside for the game's question box, for a call
    /// and for a message only the game's view can answer (the classic view, until the app closes). Anything it cannot
    /// do gives the game's app back as it was, with a line in Player.log. The game keeps the full menu and the app
    /// switched off until they open, so the kit binds over them switched off (every search takes switched off objects);
    /// an app not built yet is looked at again later (UiBindRules), and only a fault of the kit's own screen gives the
    /// app back for the session. It only hides the app's layout (another mod's changes to it stay, Big Hax's among them)
    /// and gives the layout back exactly as it found it. Cost: nothing while the app is closed; while it shows, the
    /// host's input reads and one question-box test a frame.
    /// </summary>
    public static class ContactsKit
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "ContactsKit", typeof(ContactsKit));

        private static ContactsApp _app;
        private static GameObject _root;
        private static UiHost _host;
        private static Canvas _canvas;
        private static int _order;
        private static CanvasGroup _layout;
        private static float _layoutAlpha = 1f;
        private static bool _layoutInteractable = true, _layoutBlocks = true, _hiding;
        private static bool _classic;
        private static bool _gaveUp;      // a fault of the kit's own screen: the game's app for the rest of the session
        private static bool _stopped;     // the app never looked as expected in this city: the game's own until the next city
        private static int _misses;
        private static float _nextLook;
        private static string _state = NotLooked;

        private const string NotLooked = "not looked at yet";
        private const string NoPhone = "waiting for the phone";

        /// <summary>Where the kit stands, for the interface's line at load: drawn, waiting for the app, or the game's own and why.</summary>
        public static string State => _root != null ? "drawn over the game's app" : _state;

        /// <summary>True once the kit has an answer for this city: over the app, waiting to look again, or the game's own.</summary>
        public static bool Settled => _root != null || (_state != NotLooked && _state != NoPhone);

        /// <summary>True while the kit draws the app.</summary>
        public static bool Showing => _root != null && _root.activeSelf && _app != null && _app.isActiveAndEnabled && !_classic;

        /// <summary>Puts the kit over the game's Contacts app once per city; asked again once a second while the switch is on.</summary>
        public static void Install()
        {
            if (_gaveUp || _stopped || FrameGuard.Off)
            {
                return;
            }
            if (_root != null)
            {
                if (_app == null)
                {
                    Remove("the city was unloaded");
                }
                return;
            }
            if (Time.unscaledTime < _nextLook)
            {
                return;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            ContactsApp app = uis != null && uis.fullMenu != null ? uis.fullMenu.contactsApp : null;
            if (app == null)
            {
                _state = NoPhone;
                return;   // the phone is built a little after the city: asked again next second
            }
            // The full menu and the app stay switched off until opened, so the search for the canvases takes switched off ones.
            Transform layout = app.transform.Find("Layout 30-70");
            Canvas[] above = app.GetComponentsInParent<Canvas>(true);
            if (layout == null || above.Length == 0)
            {
                Miss(layout == null ? "its layout is not there" : "no canvas above it");
                return;
            }
            var overrides = new bool[above.Length];
            for (int i = 0; i < above.Length; i++)
            {
                overrides[i] = above[i].overrideSorting;
            }
            Canvas root = above[UiBindRules.Root(above.Length)];
            _order = above[UiBindRules.Sorter(overrides, above.Length)].sortingOrder + 1;   // just over the full menu: the game's own question boxes and notices stay above
            _app = app;
            _layout = layout.GetComponent<CanvasGroup>();
            if (_layout == null)
            {
                _layout = layout.gameObject.AddComponent<CanvasGroup>();
            }
            _root = new GameObject("RealLife Interface Contacts", typeof(RectTransform));
            var rect = (RectTransform)_root.transform;
            rect.SetParent(root.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _canvas = _root.AddComponent<Canvas>();
            Sort();
            _root.AddComponent<GraphicRaycaster>();
            _host = _root.AddComponent<UiHost>();
            _host.Bind(new ContactsScreen(app), UiGame.Instance, UiGame.Instance, UiDirector.Dark, UiDirector.Motion, UiFrame.MastheadHeight, UiFrame.BandHeight,
                UiText.T("reallife_ui_contacts_app"));
            _host.View.Scale = UiTokens.CanvasPerPx;
            _host.Failed = Fallback;
            _root.SetActive(false);
            if (UiDirector.Logger != null && _misses > 0)
            {
                UiDirector.Logger.Info("UI: the Contacts app is drawn on the RLO interface now, after " + _misses + " look(s) that found it not ready.");
            }
            _misses = 0;
            _state = "drawn over the game's app";
            app.gameObject.AddComponent<ContactsWatch>();   // on an open app its OnEnable shows the kit at once
        }

        /// <summary>A new city: an app that never looked as expected in the last one is looked at again.</summary>
        public static void Rearm()
        {
            _stopped = false;
            _misses = 0;
            _nextLook = 0f;
            if (!_gaveUp)
            {
                _state = NotLooked;
            }
        }

        /// <summary>The game's app as it was: the kit gone, the layout seen and touchable again.</summary>
        public static void Remove(string why)
        {
            if (_root == null && _app == null)
            {
                return;
            }
            try
            {
                Restore();
                if (_app != null)
                {
                    ContactsWatch watch = _app.GetComponent<ContactsWatch>();
                    if (watch != null)
                    {
                        UnityEngine.Object.Destroy(watch);
                    }
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
            _app = null;
            _canvas = null;
            _layout = null;
            _hiding = false;
            _classic = false;
            if (UiDirector.Logger != null && why != null && Meta.Dev.Verbose)
            {
                UiDirector.Logger.Info("UI: the Contacts app is the game's own again (" + why + ").");
            }
        }

        /// <summary>The game's own view until the app closes: a call, an offer with its buttons, a table only it can show.</summary>
        public static void Classic()
        {
            _classic = true;
            Restore();
            if (_root != null)
            {
                _host.Hide();
                _root.SetActive(false);
            }
        }

        /// <summary>A fault in the kit's screen (its host has counted it): the game's app back for the rest of the session.</summary>
        public static void Fallback(Exception e)
        {
            UiDirector.Logger?.Warn("UI: " + UiText.T("reallife_ui_note_fallback") + " (" + (e != null ? e.GetType().Name : "?") + ")");
            Remove("a fault");
            _gaveUp = true;
            _state = "the game's own after a fault of the kit's screen";
        }

        internal static void Shown()
        {
            if (_root == null || _classic)
            {
                return;
            }
            try
            {
                Conceal();
                Sort();
                _root.SetActive(true);
                _root.transform.SetAsLastSibling();
                _host.Show();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, null);
                Fallback(e);
            }
        }

        internal static void Hidden()
        {
            _classic = false;   // the classic view lasts until the app closes; the next opening is the kit's again
            if (_root == null)
            {
                return;
            }
            _host.Hide();
            _root.SetActive(false);
        }

        // The game's question box over the app: the kit steps aside while it is up, so the box is seen and answered.
        internal static void Watch()
        {
            if (_root == null || _classic)
            {
                return;
            }
            bool asked = GameConfirm.IsOpen;
            if (_root.activeSelf == asked)
            {
                _root.SetActive(!asked);
                if (!asked)
                {
                    _host.Show();
                }
            }
        }

        // Kept as found, so it goes back so: another mod may have set the layout's group too.
        private static void Conceal()
        {
            if (_layout == null || _hiding)
            {
                return;
            }
            _layoutAlpha = _layout.alpha;
            _layoutInteractable = _layout.interactable;
            _layoutBlocks = _layout.blocksRaycasts;
            _hiding = true;
            _layout.alpha = 0f;
            _layout.interactable = false;
            _layout.blocksRaycasts = false;
        }

        private static void Restore()
        {
            if (_layout != null && _hiding)
            {
                _layout.alpha = _layoutAlpha;
                _layout.interactable = _layoutInteractable;
                _layout.blocksRaycasts = _layoutBlocks;
            }
            _hiding = false;
        }

        // Set again each time the kit shows: a canvas made under a switched off parent is only nested once that parent comes on.
        private static void Sort()
        {
            if (_canvas != null)
            {
                _canvas.overrideSorting = true;
                _canvas.sortingOrder = _order;
            }
        }

        // Often the app is only not built yet, so it is looked at again a little later, a few times a city.
        private static void Miss(string why)
        {
            _misses++;
            if (UiBindRules.Stops(_misses))
            {
                _stopped = true;
                _state = "the game's own for this city (" + why + ")";
                GameUi.UiClone.Fallback("the Contacts app is not built as this version expects (" + why + "), so Contacts stays the game's own until the next city");
                return;
            }
            float wait = UiBindRules.Wait(_misses);
            _nextLook = Time.unscaledTime + wait;
            _state = "waiting: " + why + ", looked at " + _misses + " time(s)";
            if (_misses == 1)
            {
                UiDirector.Logger?.Info("UI: the Contacts app is not ready for the RLO interface yet (" + why + "); looked at again in "
                    + wait.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " s.");
            }
        }
    }

    /// <summary>
    /// On the game's Contacts app: tells the kit the moment the app opens or closes, and watches for the game's question
    /// box while it shows. Cost a frame: one test of the question box while the app shows; nothing while it is closed.
    /// </summary>
    public sealed class ContactsWatch : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "ContactsWatch", typeof(ContactsWatch));
        private static readonly int HotUpdate = HotSpots.Slot("Ui: ContactsWatch.Update");

        private void OnEnable()
        {
            if (!FrameGuard.Off)
            {
                ContactsKit.Shown();
            }
        }

        private void OnDisable()
        {
            if (!FrameGuard.Off)
            {
                ContactsKit.Hidden();
            }
        }

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
                ContactsKit.Watch();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
                ContactsKit.Fallback(e);
            }
        }
    }
}
