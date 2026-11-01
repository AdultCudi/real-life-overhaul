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
    /// do gives the game's app back as it was, once, with a line in Player.log. Cost: nothing while the app is closed;
    /// while it shows, the host's input reads and one question-box test a frame.
    /// </summary>
    public static class ContactsKit
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "ContactsKit", typeof(ContactsKit));

        private static ContactsApp _app;
        private static GameObject _root;
        private static UiHost _host;
        private static CanvasGroup _layout;
        private static bool _classic;
        private static bool _gaveUp;

        /// <summary>True while the kit draws the app.</summary>
        public static bool Showing => _root != null && _root.activeSelf && _app != null && _app.isActiveAndEnabled && !_classic;

        /// <summary>Puts the kit over the game's Contacts app once per city; asked again once a second while the switch is on.</summary>
        public static void Install()
        {
            if (_gaveUp || FrameGuard.Off)
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
            UIs uis = InstanceBehavior<UIs>.Instance;
            ContactsApp app = uis != null && uis.fullMenu != null ? uis.fullMenu.contactsApp : null;
            if (app == null)
            {
                return;   // the phone is built a little after the city: asked again next second
            }
            Transform layout = app.transform.Find("Layout 30-70");
            Canvas menu = app.GetComponentInParent<Canvas>();
            if (layout == null || menu == null || menu.rootCanvas == null)
            {
                GiveUp("the Contacts app is not built as this version expects");
                return;
            }
            _app = app;
            _layout = layout.GetComponent<CanvasGroup>() ?? layout.gameObject.AddComponent<CanvasGroup>();
            _root = new GameObject("RealLife Interface Contacts", typeof(RectTransform));
            var rect = (RectTransform)_root.transform;
            rect.SetParent(menu.rootCanvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var canvas = _root.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = menu.sortingOrder + 1;   // just over the full menu: the game's own question boxes and notices stay above
            _root.AddComponent<GraphicRaycaster>();
            _host = _root.AddComponent<UiHost>();
            _host.Bind(new ContactsScreen(app), UiGame.Instance, UiGame.Instance, UiDirector.Dark, UiDirector.Motion, UiFrame.FullMenuTop, UiFrame.FullMenuBand,
                UiText.T("reallife_ui_contacts_app"));
            _host.View.Scale = UiTokens.CanvasPerPx;
            _host.Failed = Fallback;
            _root.SetActive(false);
            app.gameObject.AddComponent<ContactsWatch>();
            if (app.isActiveAndEnabled)
            {
                Shown();
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
            _layout = null;
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
        }

        internal static void Shown()
        {
            if (_root == null || _classic)
            {
                return;
            }
            try
            {
                _layout.alpha = 0f;
                _layout.interactable = false;
                _layout.blocksRaycasts = false;
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

        private static void Restore()
        {
            if (_layout != null)
            {
                _layout.alpha = 1f;
                _layout.interactable = true;
                _layout.blocksRaycasts = true;
            }
        }

        private static void GiveUp(string why)
        {
            _gaveUp = true;
            GameUi.UiClone.Fallback(why + ", so Contacts stays the game's own");
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
