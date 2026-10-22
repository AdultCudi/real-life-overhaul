using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BAModAPI;
using Localizor.LanguageChangeEvent;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// Pages for the HUD phone's grid (two columns, four rows: the game's own eight apps fill it) and for the full menu's
    /// row of app buttons (OnStrip), whose centred row would otherwise grow under the money and the date and off the screen.
    /// A page is a set number of tiles (PagerRules): eight on the phone; on the row as many as fit between where the game's
    /// row begins and the money's box, worked out again only when the bar's width changes. The game's apps come first in
    /// their order, then the mod's in one fixed order, then other mods'. With pages, every page's last tile is the More apps
    /// button (a copy of the game's app button), which turns to the next page; the wheel turns them too. On the phone the
    /// button's badge counts the badges up on the other pages.
    ///
    /// With the option App folders on, the mod's apps stand in five folders and a dock (PhoneLayoutRules, PhoneFolders,
    /// PhoneMove); every button is in exactly one place (ContractCheck PhoneLayoutChecks).
    ///
    /// What a pager does is written on the grid itself (PageMark, RowAsBuilt, one More apps object under the grid's parent),
    /// so any pager after it carries on. Another mod paging the same grid (a script on it that is not the game's or the
    /// mod's, or a hidden button shown again) makes this one step aside for good and show everything.
    ///
    /// Costs nothing a frame: it acts when the grid is shown, its children change, a button is shown or hidden (one pass
    /// before the canvas is drawn), its box changes size, on the button, the wheel, a folder's tile and a layout change.
    /// </summary>
    public sealed partial class PhoneGridPager : MonoBehaviour, IScrollHandler
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "PhoneGridPager", typeof(PhoneGridPager));   // the game's calls of it

        private const string MoreName = "RealLife MoreApps";
        private const int RanksKept = 128;                               // before the record of names is started again
        private const string MoreTitleKey = "reallife_apps_more";       // "More apps", where the apps show their names
        private const string MorePageKey = "reallife_apps_page";        // "{page} / {pages}", under the name
        internal const string MoreGlyph = "Icon-Arrows";                  // the game's double chevron, white, 64 by 64
        private const string MoreIconName = "AppIcon-MoreApps";
        internal const float GlyphShare = 0.42f;                          // of the tile: the glyph's size on every app icon of the mod's
        private const float LineStep = 1.25f;                            // font sizes, from the name's line to the page's, on the HUD phone's button
        internal static readonly Color32 MoreTop = new Color32(240, 242, 245, 255);     // upper left of the tile: a light grey, unlike any app's colour
        internal static readonly Color32 MoreBottom = new Color32(196, 201, 209, 255);  // lower right of the tile
        internal static readonly Color32 MoreInk = new Color32(54, 62, 76, 255);        // the glyph: dark on the light tile

        public static IModLogger Logger;

        private static FieldInfo _pictureField;
        private static bool _pictureLooked;
        private static Sprite _drawnGlyph;

        private Transform _template;
        private readonly List<Transform> _apps = new List<Transform>(24);
        private readonly List<MonoBehaviour> _scripts = new List<MonoBehaviour>(4);
        private int _page;
        private bool _aside;        // someone else pages this grid
        private bool _working;      // our own changes must not start another pass
        private bool _ready;        // the template is known
        private float _lastTurn;
        private bool _strip;            // the full menu's row of app buttons, not the HUD phone's grid
        private RectTransform _rightData;
        private int _tiles;             // the strip's tiles on a page, for the bar's width it was worked out for (0: not yet)
        private int _gameApps = -1;     // the buttons of the game's own apps, found once (-1: not yet)
        private int _gameShown;         // of those, how many are among the apps now
        private float _measuredFor = float.NaN;   // the bar's width the strip's tiles were worked out for (NaN: not yet)
        private readonly HashSet<Transform> _gameButtons = new HashSet<Transform>();
        private readonly List<Transform> _found = new List<Transform>(24);
        private bool[] _isGame = new bool[32];
        private int[] _order = new int[32];
        private int[] _rank = new int[32];                                           // each button's place among the mod's apps, -1 for the game's and other mods'
        private readonly Dictionary<Transform, int> _ranks = new Dictionary<Transform, int>(32);   // ...read once from its name
        private float _pagesLeft;       // the strip's left edge for a page: a full page centred in the room
        private float _room = float.NaN;   // from where the game's row begins to the money's box (NaN: not measured)
        private bool _own;              // only the game's own apps: laid out as the game lays them out
        private bool _pending;          // a pass is asked for before the frame is drawn
        private bool _checkDue;         // the menu has opened: one diagnostics line on where the row ends
        private RectTransform _more;    // the "More apps" button, beside the grid under its parent
        private TextLocalizationComponent _morePage;
        private global::UI.Elements.Badge _moreBadge;
        private int _badgeShown = -1;   // the number last put on the button's badge
        private UnityEngine.Events.UnityAction<AppName> _onBadge;   // the game's word that a badge changed, through the guard: kept for taking off
        private int _labelPage = -1, _labelPages = -1;
        private Canvas.WillRenderCanvases _late;
        private static bool _toldByHand;
        private static readonly Vector3[] Corners = new Vector3[4];
        private static readonly Dictionary<Type, bool> KnownScripts = new Dictionary<Type, bool>(8);   // true for the game's, Unity's and the mod's scripts, false for another mod's

        private readonly List<Transform> _items = new List<Transform>(24);   // what the pages hold, in order: buttons and folders' tiles
        private readonly List<Transform> _dock = new List<Transform>(PhoneLayoutRules.DockMost);   // the HUD phone's dock row, on the home screen
        private readonly List<Transform> _sorted = new List<Transform>(40);  // every child in the order it is to stand (Order)
        private int[] _kind = new int[32];                                   // each of _apps: the game's, the mod's or another mod's
        private int[] _place = new int[32];                                  // ...where the layout puts it
        private int[] _badge = new int[32];                                  // ...the number on its badge
        private int[] _itemOf = new int[32];                                 // ...its place among _items, -1 if not there
        private bool[] _docked = new bool[32];                               // ...in the dock row
        private int[] _view = new int[48];
        private readonly int[] _dockAt = new int[PhoneLayoutRules.DockMost];
        private readonly int[] _members = new int[PhoneLayoutRules.FolderCount];   // each folder's apps on this phone
        private readonly int[] _sums = new int[PhoneLayoutRules.FolderCount];      // ...and their numbers added up
        private PhoneFolders _folders;   // the folders', Back and empty tiles: made when folders are first shown
        private bool _foldered;          // this pass lays the grid out in folders
        private int _open = -1;          // the folder whose page shows; -1 for the home screen
        private int _homePage;           // the home screen's page, to go back to from a folder
        private int _inDock;             // the mod's apps the layout puts in the dock, at most its room
        private Action _onLayout;        // the layout's word that it changed, kept for taking off
        private PhoneFolderKeys _keys;   // the game's cancel key while a folder of the HUD phone's is open
        private bool _noFolders;         // the folders' tiles could not be made: plain pages for this grid

        /// <summary>The HUD phone's pager and the full menu row's, while they stand.</summary>
        public static PhoneGridPager Hud { get; private set; }
        public static PhoneGridPager Strip { get; private set; }

        public int Pages { get; private set; } = 1;
        public int Count => _apps.Count;
        public bool SteppedAside => _aside;

        // The HUD phone's eight; the full menu's as many as fit (the game's own row until measured).
        private int Tiles => _strip ? (_tiles > 0 ? _tiles : PagerRules.StripTiles(_gameApps)) : PagerRules.PhoneTiles;

        // The tiles of a page that hold the view's items (all but the Back tile and the dock row), and the More apps tile.
        private int ItemTiles => _foldered ? PhoneLayoutRules.ItemTiles(_strip, Tiles, _open >= 0, _dock.Count > 0) : Tiles;
        private int MoreTile => _foldered ? PhoneLayoutRules.MoreTile(_strip, Tiles, _open >= 0, _dock.Count > 0) : PagerRules.ButtonTile(Tiles);

        /// <summary>The folder whose page shows, -1 for the home screen.</summary>
        public int Folder => _open;

        public bool Foldered => _foldered;

        /// <summary>The mod's apps on this phone that the layout puts in the dock.</summary>
        public int InDock => _inDock;

        /// <summary>A folder's apps on this phone, as last laid out.</summary>
        public int Members(int folder)
        {
            return folder >= 0 && folder < _members.Length ? _members[folder] : 0;
        }

        /// <summary>Pages for the HUD phone's grid. <paramref name="dot"/> is not used.</summary>
        public static PhoneGridPager On(Transform grid, Transform template, Sprite dot)
        {
            if (grid == null)
            {
                return null;
            }
            PhoneGridPager pager = grid.GetComponent<PhoneGridPager>();
            if (pager == null)
            {
                pager = grid.gameObject.AddComponent<PhoneGridPager>();   // its OnEnable runs here, before the template is known: it waits
            }
            pager._template = template;
            pager._ready = true;
            Hud = pager;
            pager.HearBadges();
            pager.HearLayout();
            pager.Refresh();
            return pager;
        }

        // The button counts every badge on the pages not showing, the game's own too.
        private void HearBadges()
        {
            if (_onBadge != null)
            {
                return;
            }
            _onBadge = Guard.Listener<AppName>(BadgeChanged);
            try
            {
                ListenBadges(true);
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the game's own badges on the More apps button");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ListenBadges(bool on)
        {
            if (on)
            {
                SmartphoneUI.OnUpdatedBadgeCount.AddListener(_onBadge);
            }
            else
            {
                SmartphoneUI.OnUpdatedBadgeCount.RemoveListener(_onBadge);
            }
        }

        private void BadgeChanged(AppName app)
        {
            if (this != null)
            {
                Paint();
            }
        }

        /// <summary>
        /// Pages for the full menu's row. <paramref name="rightData"/> (the money, date and time) is read only for the
        /// diagnostics line; <paramref name="dot"/> is not used.
        /// </summary>
        public static PhoneGridPager OnStrip(Transform strip, Transform template, Sprite dot, RectTransform rightData)
        {
            if (strip == null || !(strip is RectTransform) || strip.GetComponent<HorizontalLayoutGroup>() == null)
            {
                return null;   // not the row the game builds: left as it is
            }
            PhoneGridPager pager = strip.GetComponent<PhoneGridPager>();
            if (pager == null)
            {
                pager = strip.gameObject.AddComponent<PhoneGridPager>();
            }
            pager._strip = true;
            pager._rightData = rightData;
            if (strip.parent is RectTransform bar)
            {
                RowBarWatch watch = bar.GetComponent<RowBarWatch>() ?? bar.gameObject.AddComponent<RowBarWatch>();
                watch.Pager = pager;   // the bar's width changes with the screen: the tiles are worked out again
            }
            pager._template = template;
            pager._ready = true;
            Strip = pager;
            pager.HearLayout();
            pager.Refresh();
            return pager;
        }

        // A move, the option switched or the options read again: laid out again. Once for the pager.
        private void HearLayout()
        {
            if (_onLayout != null)
            {
                return;
            }
            _onLayout = Guard.Way(LayoutChanged);
            PhoneLayoutPrefs.Changed += _onLayout;
        }

        private void LayoutChanged()
        {
            if (this == null)
            {
                return;
            }
            if (!PhoneLayoutPrefs.On)
            {
                _open = -1;
            }
            Refresh();
        }

        private void Awake()
        {
            _late = new Canvas.WillRenderCanvases(Guard.Way(LatePass));   // one delegate for good: asking for a pass allocates nothing
        }

        private void OnEnable()
        {
            _checkDue = _strip;
            Refresh();
            if (_strip && !_aside)
            {
                ShowSelected();
            }
        }

        // The menu has opened: on the page (and folder) of the app it opens on.
        private void ShowSelected()
        {
            for (int i = 0; i < _apps.Count; i++)
            {
                Transform selected = _apps[i].Find("SelectedIcon");
                if (selected != null && selected.gameObject.activeSelf)
                {
                    ShowPageOf(_apps[i]);
                    return;
                }
            }
        }

        private void OnDisable()
        {
            if (_pending)
            {
                _pending = false;
                Canvas.willRenderCanvases -= _late;
            }
            if (_more != null && _more.gameObject.activeSelf)
            {
                _more.gameObject.SetActive(false);   // it stands beside the grid, not in it
            }
        }

        private void OnTransformChildrenChanged()
        {
            Refresh();
        }

        // The screen or the interface's scale changed: the page and its button may stand elsewhere.
        private void OnRectTransformDimensionsChange()
        {
            Changed();
        }

        /// <summary>
        /// A button was shown or hidden by any hand (its PageMark says so), which is no change of the grid's children: one
        /// pass before the canvas is drawn, however many changed.
        /// </summary>
        internal void Changed()
        {
            if (_working || _aside || _pending || !_ready || !isActiveAndEnabled)
            {
                return;   // our own changes, or the grid is not showing: looked at again when it is
            }
            _pending = true;
            Canvas.willRenderCanvases += _late;
        }

        private void LatePass()
        {
            Canvas.willRenderCanvases -= _late;
            _pending = false;
            if (this != null && isActiveAndEnabled)
            {
                Refresh();
            }
        }

        /// <summary>Where a button is in the grid, counted from 1; 0 if it is not one of the apps.</summary>
        public int PlaceOf(Transform button)
        {
            return _apps.IndexOf(button) + 1;
        }

        /// <summary>Turns to the page a button is on, in its folder if it is in one.</summary>
        public void ShowPageOf(Transform button)
        {
            int app = _apps.IndexOf(button);
            if (app < 0 || _aside)
            {
                return;
            }
            int folder = _foldered ? PhoneLayoutRules.FolderOf(_kind[app], _place[app]) : -1;
            if (folder != _open)
            {
                Go(folder);
            }
            int index = _items.IndexOf(button);
            if (index >= 0)
            {
                Turn(PageOf(index));
            }
        }

        /// <summary>A folder's tile: its first page.</summary>
        internal void OpenFolder(int folder)
        {
            if (!_aside && _foldered && folder >= 0 && folder < PhoneLayoutRules.FolderCount && folder != _open)
            {
                Go(folder);
            }
        }

        /// <summary>The Back tile, or the game's cancel key: the home screen again, on the page it was left on.</summary>
        internal void CloseFolder()
        {
            if (_open >= 0)
            {
                Go(-1);
            }
        }

        // To a folder's first page or to the home screen on the page it was left on.
        private void Go(int folder)
        {
            if (_open < 0 && folder >= 0)
            {
                _homePage = _page;
            }
            _page = folder >= 0 ? 0 : _homePage;
            _open = folder;
            Refresh();
            Keys();
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Game UI: " + (_strip ? "the full menu's row" : "the HUD phone") + " shows " + (_open >= 0 ? "the folder " + PhoneLayoutRules.FolderIds[_open] : "the home screen")
                             + ": " + Check() + ".");
            }
        }

        // Only on the HUD phone, and only while a folder of it is open.
        private void Keys()
        {
            bool want = !_strip && _open >= 0 && !_aside;
            if (want && _keys == null)
            {
                _keys = gameObject.AddComponent<PhoneFolderKeys>();
                _keys.Pager = this;
            }
            if (_keys != null && _keys.enabled != want)
            {
                _keys.enabled = want;
            }
        }

        /// <summary>A badge of the mod's changed: the More apps button counts again. The parameters are not read.</summary>
        public void SetAttention(Transform button, bool on)
        {
            Paint();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                Wheel(eventData);
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        // The wheel over the row or its button: one page on or back, stopping at either end.
        internal void Wheel(PointerEventData eventData)
        {
            if (_aside || Pages < 2 || eventData == null || Time.unscaledTime - _lastTurn < 0.15f || Mathf.Abs(eventData.scrollDelta.y) < 0.01f)
            {
                return;
            }
            _lastTurn = Time.unscaledTime;
            Turn(PagerRules.Wheel(_page, Pages, eventData.scrollDelta.y < 0f));
        }

        // The next page, and from the last back to the first.
        private void NextPage()
        {
            if (!_aside)
            {
                Turn(PagerRules.Next(_page, Pages));
            }
        }

        private void Turn(int page)
        {
            page = Mathf.Clamp(page, 0, Pages - 1);
            if (page != _page)
            {
                _page = page;
                Refresh();
            }
        }

        public void Refresh()
        {
            if (_working || !_ready || this == null)
            {
                return;
            }
            _working = true;
            try
            {
                if (!_aside && SomeoneElse())
                {
                    StepAside();
                }
                Collect();
                if (!_aside)
                {
                    View();
                    if (_strip)
                    {
                        StripPages();
                        if (_foldered != (FoldersWanted && !_noFolders))
                        {
                            View();   // the row was measured just now: it may have room for folders, or no longer
                            StripPages();
                        }
                    }
                    else
                    {
                        Pages = PagerRules.Pages(_items.Count, ItemTiles);
                    }
                    _page = Mathf.Clamp(_page, 0, Pages - 1);
                    Apply();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                StepAside();
            }
            finally
            {
                _working = false;
            }
        }

        private void OnDestroy()
        {
            if (_onLayout != null)
            {
                PhoneLayoutPrefs.Changed -= _onLayout;   // a static event: taken off on every path
                _onLayout = null;
            }
            if (Hud == this)
            {
                Hud = null;
                PhoneMove.Forget();   // the move list stands on the HUD
            }
            if (Strip == this)
            {
                Strip = null;
            }
            if (_onBadge != null)
            {
                try
                {
                    ListenBadges(false);   // the game's event outlives the city
                }
                catch (Exception e) when (Guard.Missing(e))
                {
                    Guard.DoesWithout(e, "the game's own badges on the More apps button");
                }
                _onBadge = null;
            }
            if (GameManager.isCitySceneBeingUnloaded)
            {
                return;   // the interface goes with the city
            }
            ShowAll();
            RestoreRow();
            if (_more != null)
            {
                Destroy(_more.gameObject);
            }
            DropFolders();
        }

        /// <summary>
        /// Every button is in exactly one place (the view, the dock or one folder's page) and each folder's tile stands on the
        /// home screen while it has apps, worked out again from the rules: "PASS" or what is wrong, for the log and the test.
        /// </summary>
        public string Check()
        {
            int count = _apps.Count;
            if (_aside)
            {
                return "another mod pages this grid: PASS (nothing of the folders is on it)";
            }
            if (!_foldered)
            {
                return count + " apps on " + Pages + " page(s), no folders (" + (PhoneLayoutPrefs.On ? "no room for them" : "the option is off") + "): "
                       + (_items.Count == count ? "PASS" : "FAIL, " + _items.Count + " on the pages");
            }
            var seen = new int[count];
            var view = new int[count + PhoneLayoutRules.FolderCount];
            var dock = new int[PhoneLayoutRules.DockMost];
            var members = new int[PhoneLayoutRules.FolderCount];
            int tiles = 0;
            for (int folder = -1; folder < PhoneLayoutRules.FolderCount; folder++)
            {
                int n = PhoneLayoutRules.View(_kind, _place, count, folder, !_strip, view, dock, out int docked, members);
                for (int j = 0; j < n; j++)
                {
                    if (view[j] >= 0)
                    {
                        seen[view[j]]++;
                    }
                    else if (folder < 0)
                    {
                        tiles++;
                    }
                }
                for (int d = 0; d < docked && folder < 0; d++)
                {
                    seen[dock[d]]++;
                }
            }
            int lost = 0, twice = 0, folders = 0, inFolders = 0;
            for (int i = 0; i < count; i++)
            {
                lost += seen[i] == 0 ? 1 : 0;
                twice += seen[i] > 1 ? 1 : 0;
            }
            for (int f = 0; f < PhoneLayoutRules.FolderCount; f++)
            {
                folders += members[f] > 0 ? 1 : 0;
                inFolders += members[f];
            }
            bool ok = lost == 0 && twice == 0 && tiles == folders;
            return count + " buttons: " + inFolders + " in " + folders + " folder(s), " + _inDock + " in the dock, " + (count - inFolders - _inDock) + " on the home screen; "
                   + (_open >= 0 ? "the folder " + PhoneLayoutRules.FolderIds[_open] + " open, " : string.Empty) + _items.Count + " item(s) on " + Pages + " page(s): "
                   + (ok ? "PASS" : "FAIL, " + lost + " lost, " + twice + " twice, " + tiles + " folder tile(s) for " + folders + " folder(s)");
        }
    }

    /// <summary>The wheel over the More apps button turns the pages: the grid does not hear it there. Costs nothing a frame.</summary>
    public sealed class MoreAppsWheel : MonoBehaviour, IScrollHandler
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "MoreAppsWheel", typeof(MoreAppsWheel));   // the game's calls of it

        internal PhoneGridPager Pager;

        public void OnScroll(PointerEventData eventData)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                if (Pager != null)
                {
                    Pager.Wheel(eventData);
                }
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }
    }

    /// <summary>On the full menu's top bar: a change of its width has the row's tiles worked out again. Costs nothing a frame.</summary>
    public sealed class RowBarWatch : MonoBehaviour
    {
        internal PhoneGridPager Pager;

        private void OnRectTransformDimensionsChange()
        {
            if (Pager != null)
            {
                Pager.Changed();
            }
        }
    }

    /// <summary>
    /// On each button of a paged grid: Off while a pager has it hidden for being on another page, so any pager counts it
    /// as an app; tells the pager when any hand shows or hides it. Costs nothing a frame.
    /// </summary>
    public sealed class PageMark : MonoBehaviour
    {
        public bool Off;
        internal PhoneGridPager Pager;   // not saved with the button: a copy of it waits for its own pager

        private void OnEnable()
        {
            if (Pager != null)
            {
                Pager.Changed();
            }
        }

        private void OnDisable()
        {
            if (Pager != null)
            {
                Pager.Changed();
            }
        }
    }

    /// <summary>The grid's alignment and left padding as the game built them, kept from the first paging. Costs nothing a frame.</summary>
    public sealed class RowAsBuilt : MonoBehaviour
    {
        public TextAnchor Alignment;
        public int Left;
    }
}
