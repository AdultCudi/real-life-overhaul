using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using EnhancedUI.EnhancedScroller;
using UI;
using UI.Notification;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The mod's notifications told apart from the game's at a glance, in the game's own toast and list: each carries a
    /// picture of what it is about (<see cref="NoticeKinds"/>) on a disc in the mod's colour where the game's carry theirs.
    /// The game's and other mods' notifications are left as the game draws them.
    ///
    /// The game (UI.Notification): NotificationsUI.ShowNotification copies one of four templates by type into its
    /// container, the copy's Text/Image its picture; NotificationListCellView.SetData switches on the one child of a row's
    /// Icon named after the type. The look goes on those objects as the game makes them:
    /// - a toast: on Notifications.onShow, after the game's toast maker (joined again at its end if something joined
    ///   after), the copy just made gets the disc and the kind's picture; copies are destroyed after each notification,
    ///   so nothing needs putting back;
    /// - a list row: on the scroller's cellViewVisibilityChanged, raised right after the game fills a row; rows are reused,
    ///   so the mod's get the disc and the picture as one more child of Icon (which SetData switches off for the game's),
    ///   and any other row gets the game's white disc back.
    /// Pictures are the game's where one fits (<see cref="GameSprites"/>), else the mod's glyphs (<see cref="MapPlaces.Glyph"/>).
    /// Nothing is sent: a Going Public guest sees the look on what its own game shows.
    ///
    /// Costs nothing a frame (no Update). A notification: a prefix test of its key, and for the mod's a walk of the kinds
    /// table and a few writes on the toast; a row in view the same, plus a look through its icon's children.
    /// </summary>
    public sealed class NoticeLook : MonoBehaviour
    {
        public static IModLogger Logger;
        public static NoticeLook Instance { get; private set; }

        /// <summary>The game's stack of notifications (NotificationsUI.container), found behind the loading screen; null before. NoticeRoom moves it.</summary>
        internal Transform Container => _container;

        private const string GlyphName = "RealLifeOverhaul";       // the mod's child on a picture's disc
        private const float ToastDisc = 52f;                       // px of its 3840 x 2160 canvas (the game's is 44)
        private const float ToastGlyph = 32f;                      // the kind's picture on it
        private const float RowGlyph = 30f;                        // on the list's 48 px disc (the game's pictures are 24)
        private const float TestGap = 1.2f;                        // seconds of real time between the test's notifications
        private const float TestSeconds = 6f;                      // seconds each of them stays up

        /// <summary>The mod's colour: a deep indigo, unlike the game's four, that a white picture reads on.</summary>
        public static readonly Color Accent = new Color(0.17f, 0.19f, 0.45f, 1f);

        // The game's picture for a kind where there is one, else the mod's glyph.
        private static readonly string[][] GameNames =
        {
            null,
            new[] { "Icon-Traffic" },                       // Road
            new[] { "Icon-Security" },                      // Police
            new[] { "BusinessIcon-Hospital" },              // Health
            new[] { "Icon-Coin" },                          // Money
            new[] { "Icon-Building" },                      // Property
            new[] { "Icon-Briefcase" },                     // Work
            new[] { "Icon-Shop" },                          // Business
            new[] { "Icon-Laptop" },                        // Tech
            null,                                           // News: the game has no paper of its own
            new[] { "Icon-People" },                        // People
            null                                            // Other
        };

        private static readonly MapIcon[] Glyphs =
        {
            MapIcon.Star, MapIcon.Cone, MapIcon.Shield, MapIcon.Cross, MapIcon.Coin, MapIcon.House, MapIcon.Briefcase,
            MapIcon.Tower, MapIcon.Cloud, MapIcon.Paper, MapIcon.Heart, MapIcon.Star
        };

        // The options page's test: one notification of each kind, by its own key.
        private static readonly string[] TestKeys =
        {
            "reallife_notice_test_road", "reallife_notice_test_police", "reallife_notice_test_health", "reallife_notice_test_money",
            "reallife_notice_test_property", "reallife_notice_test_work", "reallife_notice_test_business", "reallife_notice_test_tech",
            "reallife_notice_test_news", "reallife_notice_test_people", "reallife_notice_test_other"
        };

        private static readonly NotificationType[] TestTypes =
        {
            NotificationType.Warning, NotificationType.Error, NotificationType.Warning, NotificationType.Info, NotificationType.Info,
            NotificationType.Success, NotificationType.Success, NotificationType.Info, NotificationType.Info, NotificationType.Success,
            NotificationType.Info
        };

        private readonly Sprite[] _pictures = new Sprite[NoticeKinds.Count];
        private readonly string[] _pictureNames = new string[NoticeKinds.Count];   // for the self-check line
        private Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool> _onShow;
        private CellViewVisibilityChangedDelegate _onRow;

        private NotificationsUI _toasts;
        private Transform _container;
        private readonly Transform[] _templates = new Transform[4];
        private Transform _lastToast;
        private NotificationListScrollerController _list;
        private EnhancedScroller _scroller;
        private Sprite _disc;
        private Color _rowDisc = Color.white;
        private bool _attached;
        private bool _toastSaid, _rowSaid, _failSaid;
        private Coroutine _test;

        private void Awake()
        {
            Instance = this;
            _onShow = Guard.Wrap<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>(Shown);   // the game's own events: Core/Guard
            _onRow = new CellViewVisibilityChangedDelegate(Guard.Wrap<EnhancedScrollerCellView>(RowShown));
        }

        private void OnDestroy()
        {
            try
            {
                Notifications.onShow = (Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>)Delegate.Remove(Notifications.onShow, _onShow);
                if (_scroller != null)
                {
                    _scroller.cellViewVisibilityChanged = (CellViewVisibilityChangedDelegate)Delegate.Remove(_scroller.cellViewVisibilityChanged, _onRow);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>One-off work behind the loading screen (ScenePrewarm): the game's toast maker, its list and the pictures. True once done.</summary>
        public static bool Prewarm()
        {
            NoticeLook look = Instance;
            if (look == null)
            {
                return true;   // the city is going
            }
            if (look._attached)
            {
                return true;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.notificationsListUI == null)
            {
                return false;   // the interface is not there yet: tried again at the next black screen
            }
            look.Attach(uis);
            return true;
        }

        private void Attach(UIs uis)
        {
            _attached = true;
            // The toast maker, Canvases/NotificationsUI, stands beside the list's Canvases/NotificationsList.
            Transform canvases = uis.notificationsListUI.transform.parent;
            Transform maker = canvases != null ? canvases.Find("NotificationsUI") : null;
            _toasts = maker != null ? maker.GetComponent<NotificationsUI>() : null;
            int pictures = 0;
            if (_toasts != null && _toasts.container != null)
            {
                _container = _toasts.container;
                _templates[0] = _toasts.successTemplate != null ? _toasts.successTemplate.transform : null;
                _templates[1] = _toasts.errorTemplate != null ? _toasts.errorTemplate.transform : null;
                _templates[2] = _toasts.infoTemplate != null ? _toasts.infoTemplate.transform : null;
                _templates[3] = _toasts.warningTemplate != null ? _toasts.warningTemplate.transform : null;
                for (int i = 0; i < _templates.Length; i++)
                {
                    pictures += _templates[i] != null && _templates[i].Find("Text/Image") != null ? 1 : 0;
                }
                _lastToast = _container.childCount > 0 ? _container.GetChild(_container.childCount - 1) : null;
                Join();
            }
            else
            {
                Logger?.Warn("Game UI: the game's notification maker (Canvases/NotificationsUI) was not found; the mod's toasts look like the game's.");
            }

            _list = uis.notificationsListUI.GetComponentInChildren<NotificationListScrollerController>(true);
            Transform rowIcon = _list != null && _list.prefab != null ? _list.prefab.transform.Find("Icon") : null;
            Image rowDisc = rowIcon != null ? rowIcon.GetComponent<Image>() : null;
            if (rowDisc != null && _list.scroller != null)
            {
                _disc = rowDisc.sprite;
                _rowDisc = rowDisc.color;
                _scroller = _list.scroller;
                _scroller.cellViewVisibilityChanged = (CellViewVisibilityChangedDelegate)Delegate.Remove(_scroller.cellViewVisibilityChanged, _onRow);
                _scroller.cellViewVisibilityChanged = (CellViewVisibilityChangedDelegate)Delegate.Combine(_scroller.cellViewVisibilityChanged, _onRow);
            }
            else
            {
                Logger?.Warn("Game UI: the row of the game's notification list (its Icon) was not found; the mod's rows there look like the game's.");
            }
            if (_disc == null)
            {
                _disc = GameSprites.Get("circle");
            }

            int theirs = 0;
            for (int kind = 1; kind < NoticeKinds.Count; kind++)
            {
                Sprite sprite = null;
                string[] names = GameNames[kind];
                for (int n = 0; names != null && n < names.Length && sprite == null; n++)
                {
                    sprite = GameSprites.Get(names[n]);
                    _pictureNames[kind] = sprite != null ? names[n] : null;
                }
                if (sprite != null)
                {
                    theirs++;
                }
                else
                {
                    sprite = MapPlaces.Glyph(Glyphs[kind]);
                    _pictureNames[kind] = "the mod's " + Glyphs[kind];
                }
                _pictures[kind] = sprite;
            }
            if (Meta.Dev.Verbose)
            {
                bool pass = _container != null && pictures == 4 && _scroller != null && _disc != null;
                Logger?.Info("Notice check (attach): the game's toast picture (Text/Image) in " + pictures + " of 4 templates, the list's row Icon "
                             + (_scroller != null ? "found" : "missing") + ", the disc " + (_disc != null ? _disc.name : "missing") + "; "
                             + theirs + " of " + (NoticeKinds.Count - 1) + " kinds drawn with the game's own picture"
                             + (pass ? ": PASS." : ": FAIL (see the warnings above)."));
            }
        }

        // Last on the game's event, so the toast is made by the time this runs.
        private void Join()
        {
            Notifications.onShow = (Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>)Delegate.Remove(Notifications.onShow, _onShow);
            Notifications.onShow = (Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>)Delegate.Combine(Notifications.onShow, _onShow);
        }

        private void Shown(NotificationType type, string key, Dictionary<string, string> data, float seconds, string duplicate, Action onClick, bool sound, bool track)
        {
            try
            {
                Transform made = Newest();
                // Something joined the event after this (a combined delegate reports the last one's target): join again at its end.
                if (!ReferenceEquals(Notifications.onShow?.Target, this))
                {
                    Join();
                }
                if (made == null)
                {
                    return;   // the game's maker had not run yet (it is now ahead of this)
                }
                _lastToast = made;
                NoticeKind kind = Settings.NoticeLook ? NoticeKinds.Of(key) : NoticeKind.None;
                if (kind == NoticeKind.None)
                {
                    return;
                }
                Transform picture = made.Find("Text/Image");
                Image disc = picture != null ? picture.GetComponent<Image>() : null;
                if (disc == null)
                {
                    if (!_failSaid)
                    {
                        _failSaid = true;
                        Logger?.Warn("Game UI: a toast of the game's has no Text/Image; the mod's toasts keep the game's picture.");
                    }
                    return;
                }
                if (_disc != null)
                {
                    disc.sprite = _disc;
                    disc.type = Image.Type.Simple;
                }
                disc.color = Accent;
                disc.preserveAspect = true;
                var rect = (RectTransform)picture;
                rect.sizeDelta = new Vector2(ToastDisc, ToastDisc);
                Image glyph = Glyph(picture, ToastGlyph);
                glyph.sprite = _pictures[(int)kind];
                glyph.gameObject.SetActive(true);
                if (Meta.Dev.Verbose && !_toastSaid)
                {
                    _toastSaid = true;
                    Logger?.Info("Notice check (toast): " + key + " drawn as " + kind + " with " + _pictureNames[(int)kind] + " on the mod's disc: PASS.");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The toast the game's maker just put at the end of its container, or null if none is new.
        private Transform Newest()
        {
            if (_container == null || _container.childCount == 0)
            {
                return null;
            }
            Transform last = _container.GetChild(_container.childCount - 1);
            if (ReferenceEquals(last, _lastToast))
            {
                return null;
            }
            for (int i = 0; i < _templates.Length; i++)
            {
                if (ReferenceEquals(last, _templates[i]))
                {
                    return null;
                }
            }
            return last;
        }

        private void RowShown(EnhancedScrollerCellView view)
        {
            try
            {
                if (view == null || !view.active || _list == null)
                {
                    return;
                }
                var row = view as NotificationListCellView;
                List<NotificationListModel> models = _list.data;
                int index = view.dataIndex;
                if (row == null || models == null || index < 0 || index >= models.Count)
                {
                    return;
                }
                Transform icon = row.transform.Find("Icon");
                Image disc = icon != null ? icon.GetComponent<Image>() : null;
                if (disc == null)
                {
                    return;
                }
                string key = models[index]?.Key;
                NoticeKind kind = Settings.NoticeLook ? NoticeKinds.Of(key) : NoticeKind.None;
                if (kind == NoticeKind.None)
                {
                    disc.color = _rowDisc;   // a reused row of the game's: SetData already put its picture on and the mod's off
                    Transform mine = icon.Find(GlyphName);
                    if (mine != null)
                    {
                        mine.gameObject.SetActive(false);
                    }
                    return;
                }
                disc.color = Accent;
                Image glyph = Glyph(icon, RowGlyph);
                glyph.sprite = _pictures[(int)kind];
                for (int i = 0; i < icon.childCount; i++)
                {
                    Transform child = icon.GetChild(i);
                    child.gameObject.SetActive(ReferenceEquals(child, glyph.transform));
                }
                if (Meta.Dev.Verbose && !_rowSaid)
                {
                    _rowSaid = true;
                    Logger?.Info("Notice check (list): " + key + " drawn as " + kind + " with " + _pictureNames[(int)kind] + " on the mod's disc: PASS.");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // Made once on each toast or reused row: white, centred, never a click target.
        private static Image Glyph(Transform disc, float size)
        {
            Transform found = disc.Find(GlyphName);
            Image image = found != null ? found.GetComponent<Image>() : null;
            if (image == null)
            {
                var go = new GameObject(GlyphName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(disc, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
                image = go.GetComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                image.color = Color.white;
            }
            ((RectTransform)image.transform).sizeDelta = new Vector2(size, size);
            return image;
        }

        /// <summary>Options > Tests: one notification of each kind, a little apart, kept in the list like any other.</summary>
        public static void Test()
        {
            NoticeLook look = Instance;
            if (look == null || !ModState.Loaded || SaveGameManager.Current == null || Notifications.onShow == null)
            {
                TestRefusals.Tell(Logger, "Notice look", TestRefusal.NotLoaded);
                return;
            }
            if (!Settings.NoticeLook)
            {
                TestRefusals.Tell(Logger, "Notice look", TestRefusal.FeatureOff);
                return;
            }
            if (look._test != null)
            {
                look.StopCoroutine(look._test);
            }
            look._test = look.StartCoroutine(Guard.Routine(look.ShowEach()));
        }

        private IEnumerator ShowEach()
        {
            var gap = new WaitForSecondsRealtime(TestGap);
            for (int i = 0; i < TestKeys.Length; i++)
            {
                Notifications.Show(TestTypes[i], TestKeys[i], null, TestSeconds);
                yield return gap;
            }
            _test = null;
            Logger?.Info("Notice look test: " + TestKeys.Length + " notifications shown, one of each kind; open the game's notification list to see them there too.");
        }
    }
}
