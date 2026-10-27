using System;
using System.Collections.Generic;
using System.Reflection;
using RealLifeOverhaul.BizPace;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Interiors
{
    /// <summary>
    /// The Interior tab of BizMan: the scores on the left; on the right four pages by the game's own selector (what each
    /// figure does and what to add next, the items that count, today's customers by where they came from, the game's own
    /// rules for the type). The game shows a tab only while its name is in BizMan's private "_tabs", made again whenever a
    /// business opens, so the tab, a copy of the "Marketing" button, is put back after "Marketing" each time; Meaningful
    /// Interiors and Ambience keep theirs before "Settings", so they never take turns. The columns are BizMan's own scroll
    /// views holding MyEmployees' parts. Cost: nothing while BizMan is closed, one flag test a frame while it shows a business.
    /// </summary>
    public static partial class InteriorTab
    {
        public const string Id = "RealLifeInteriors";
        private const float BoxWidth = 1150f, BoxGap = 90f;   // pixels: two columns over the 2,390 of the game's Settings page

        private static BizManBusiness _business;
        private static Transform _menu, _containers;
        private static GameObject _button, _page;
        private static InfoBox _left, _right;
        private static ScrollRect _rightScroll;
        private static int _shown;                   // the right column's page, kept for the session
        public const int Pages = 4;                  // what it does, the items, today's customers, the game's rules
        private const string GuideTitle = "reallife_meta_title_interiors";   // the Guide's page each column points to at its foot
        private static readonly List<InteriorItem> Sorted = new List<InteriorItem>(16);
        private static readonly List<GameRule> GameRules = new List<GameRule>(16);
        private static FieldInfo _tabsField;
        private static MethodInfo _setTab;
        private static bool _failed, _fitLogged;
        internal static bool Dirty;
        internal static int Session;                 // the times BizMan's business view was switched on: a page written in one showing stays while its menu holds the game paused
        private static BuildingRegistration _wroteReg;
        private static InteriorShop _wroteShop;      // the business the boxes hold the page of; null for none
        private static string _wroteLocale;
        private static int _wroteSession = -1, _wrotePage = -1;

        /// <summary>A new session (a city loaded): the tab may be put in again, and its fit is logged again.</summary>
        public static void Begin()
        {
            _wroteShop = null;
            _failed = false;
            _fitLogged = false;
            Dirty = false;
        }

        /// <summary>Once a second from the director: the tab is put in BizMan once the game's BizMan is there.</summary>
        public static void Keep()
        {
            if (_failed || (_button != null && _business != null))
            {
                return;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            BizManBusiness business = uis != null && uis.fullMenu != null && uis.fullMenu.bizMan != null ? uis.fullMenu.bizMan.business : null;
            if (business == null)
            {
                return;
            }
            try
            {
                Install(business, uis.fullMenu);
            }
            catch (Exception e)
            {
                _failed = true;
                InteriorDirector.Logger?.Error(e);
                Remove();
            }
        }

        private static void Install(BizManBusiness business, FullMenu menu)
        {
            // BizMan's private pieces, through the game contract
            _menu = GameContract.BizManMenu.Field?.GetValue(business) as Transform;
            _containers = GameContract.BizManContainers.Field?.GetValue(business) as Transform;
            _tabsField = GameContract.BizManTabs.Field;
            _setTab = GameContract.BizManSetTab.Method;
            Transform template = _menu != null ? (_menu.Find("Marketing") ?? _menu.Find("Insight") ?? _menu.Find("Settings")) : null;
            Transform shape = _containers != null ? _containers.Find("Settings") : null;
            Transform source = menu.myEmployees != null ? menu.myEmployees.transform.Find("Layout 30-70/Left/WhiteBox") : null;
            Transform details = GameContract.BizManDetails.Field?.GetValue(business) as Transform;
            Transform scroll = details != null ? details.Find("WhiteBox") : null;   // BizMan's scroll view of a business's details
            if (_menu == null || _containers == null || _tabsField == null || _setTab == null || template == null || shape == null || source == null
                || template.GetComponent<TextMeshProUGUI>() == null)
            {
                UiClone.Fallback("BizMan is not built as expected, so the Interior tab is not added");
                _failed = true;
                return;
            }
            _business = business;

            // the game's own button, its click ours
            _button = UnityEngine.Object.Instantiate(template.gameObject, _menu, false);
            _button.name = Id;
            _button.SetActive(false);
            UiClone.Key(_button.transform, "reallife_interior_tab");
            UiClone.Rewire(_button.transform, Clicked);
            _button.AddComponent<InteriorTabWatch>();

            // the page: the shape of the game's Settings page, with two white boxes
            _page = new GameObject(Id, typeof(RectTransform));
            _page.SetActive(false);
            _page.transform.SetParent(_containers, false);
            var from = (RectTransform)shape;
            var to = (RectTransform)_page.transform;
            to.anchorMin = from.anchorMin;
            to.anchorMax = from.anchorMax;
            to.pivot = from.pivot;
            to.anchoredPosition = from.anchoredPosition;
            to.sizeDelta = from.sizeDelta;
            _left = Column(source, scroll, 0, out ScrollRect unused);
            _right = Column(source, scroll, 1, out _rightScroll);
            if (_left == null || _right == null)
            {
                _failed = true;
                Remove();
                return;
            }
            _page.AddComponent<InteriorPageWatch>();
            business.gameObject.AddComponent<InteriorTabKeeper>();
            _wroteShop = null;
            Dirty = true;
            InteriorDirector.Logger?.Info("Interiors: the Interior tab is in BizMan.");
        }

        // A copy of BizMan's details scroll view, emptied and written with MyEmployees' white box's parts; without that
        // scroll view, the white box itself, which does not scroll.
        private static InfoBox Column(Transform source, Transform scrollSource, int column, out ScrollRect scrolls)
        {
            scrolls = null;
            Transform dormant = UiClone.Dormant(_page.transform, "Templates" + column);
            GameObject parts = UiClone.Asleep(source.gameObject, dormant, "Parts" + column);
            if (parts == null)
            {
                return null;
            }
            UiClone.StripGameScripts(parts, script => Array.IndexOf(AppShell.Harmless, script.GetType().Name) >= 0);
            GameObject scroll = scrollSource != null ? UiClone.Asleep(scrollSource.gameObject, dormant, "Box" + column) : null;
            Transform content = scroll != null ? scroll.transform.Find("Viewport/Content") : null;
            ScrollRect rect = scroll != null ? scroll.GetComponent<ScrollRect>() : null;
            if (content != null && rect != null)
            {
                UiClone.StripGameScripts(scroll, script => Array.IndexOf(AppShell.Harmless, script.GetType().Name) >= 0);
                for (int i = content.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(content.GetChild(i).gameObject);
                }
                var layout = content.GetComponent<VerticalLayoutGroup>();
                if (layout != null)
                {
                    layout.spacing = 30f;   // as MyEmployees' white box spaces its lines
                    layout.childControlWidth = true;
                    layout.childForceExpandWidth = true;
                    layout.childControlHeight = true;
                    layout.childForceExpandHeight = false;
                }
                InfoBox box = InfoBox.Into(parts.transform, dormant, content);
                if (box != null)
                {
                    box.SetAccent(LookRules.Accent("Interior"));
                    UnityEngine.Object.DestroyImmediate(parts);
                    Place(scroll.transform, column);
                    scrolls = rect;
                    return box;
                }
            }
            if (scroll != null)
            {
                UiClone.Fallback("BizMan's scroll view of a business is not built as expected, so the Interior tab's columns do not scroll");
                UnityEngine.Object.DestroyImmediate(scroll);
            }
            InfoBox plain = InfoBox.From(parts.transform, dormant);
            if (plain != null)
            {
                plain.SetAccent(LookRules.Accent("Interior"));
                Place(parts.transform, column);
            }
            return plain;
        }

        // The page's whole height, a column wide.
        private static void Place(Transform column, int index)
        {
            column.SetParent(_page.transform, false);
            var rect = (RectTransform)column;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(index * (BoxWidth + BoxGap), 0f);
            rect.sizeDelta = new Vector2(BoxWidth, 0f);
            column.gameObject.SetActive(true);
        }

        /// <summary>Everything of the tab's taken out of BizMan.</summary>
        public static void Remove()
        {
            try
            {
                List<string> tabs = Tabs();
                tabs?.Remove(Id);
                if (_business != null)
                {
                    var keeper = _business.GetComponent<InteriorTabKeeper>();
                    if (keeper != null)
                    {
                        UnityEngine.Object.Destroy(keeper);
                    }
                }
                if (_page != null && _page.activeSelf)
                {
                    Show(tabs != null && tabs.Count > 1 ? tabs[1] : "Presentation");
                }
                if (_button != null)
                {
                    UnityEngine.Object.Destroy(_button);
                }
                if (_page != null)
                {
                    UnityEngine.Object.Destroy(_page);
                }
            }
            catch (Exception e)
            {
                InteriorDirector.Logger?.Error(e);
            }
            _button = _page = null;
            _left = _right = null;
            _rightScroll = null;
            _business = null;
            _wroteShop = null;
        }

        private static List<string> Tabs()
        {
            return _business != null && _tabsField != null ? _tabsField.GetValue(_business) as List<string> : null;
        }

        private static void Show(string tab)
        {
            if (_business != null && _setTab != null)
            {
                _setTab.Invoke(_business, new object[] { tab });
            }
        }

        private static readonly int BizFix = HotSpots.Slot("Interiors: InteriorTab.Fix");

        /// <summary>After the game made its list of tabs again: the tab added at its end and its button placed, or taken out.</summary>
        internal static void Fix()
        {
            using var timed = BizPaceWatch.Time(BizFix);   // a step of BizMan's open or switch (BizPace)
            Dirty = false;
            if (_business == null || _button == null)
            {
                return;
            }
            List<string> tabs = Tabs();
            if (tabs == null)
            {
                return;
            }
            InteriorDirector d = InteriorDirector.Instance;
            bool wanted = d != null && d.Running && InteriorGame.KindOf(_business.buildingRegistration) != InteriorKind.None;
            if (!wanted)
            {
                tabs.Remove(Id);
                if (_button.activeSelf)
                {
                    _button.SetActive(false);
                }
                if (_page.activeSelf)
                {
                    Show(tabs.Count > 1 ? tabs[1] : "Presentation");
                }
                Dirty = false;
                return;
            }
            if (!tabs.Contains(Id))
            {
                tabs.Add(Id);
            }
            Transform marketing = _menu.Find("Marketing");
            Transform settings = _menu.Find("Settings");
            int at = marketing != null && marketing.gameObject.activeSelf ? marketing.GetSiblingIndex() + 1
                : settings != null ? settings.GetSiblingIndex() : _menu.childCount - 1;
            if (_button.transform.GetSiblingIndex() != at)
            {
                _button.transform.SetSiblingIndex(at > _button.transform.GetSiblingIndex() ? at - 1 : at);
            }
            if (!_button.activeSelf)
            {
                _button.SetActive(true);
            }
            Dirty = false;   // the button's own switching on is not a change of the game's list
        }

        // As the game's own buttons do, a question first if the settings page has changes not saved.
        private static void Clicked()
        {
            if (_business == null)
            {
                return;
            }
            if (_business.bizManSettings != null && _business.bizManSettings.HasUnsavedChanges)
            {
                GameConfirm.Show(null, "change_character_clothes_unsaved_changes_warning", () => UiClone.Safe(() => Show(Id)), null, null, null, true, true);
                return;
            }
            Show(Id);
        }
    }

    /// <summary>On the game's BizManBusiness: puts the Interior tab back after the game made its list again. One flag test a frame.</summary>
    public sealed class InteriorTabKeeper : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("interiors", "InteriorTabKeeper", typeof(InteriorTabKeeper));

        private void OnEnable()
        {
            InteriorTab.Dirty = true;
            InteriorTab.Session++;   // a new showing of the business view: nothing written in the last is taken for this one's
        }

        private static readonly int HotLateUpdate = HotSpots.Slot("Interiors: InteriorTabKeeper.LateUpdate");
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnLateUpdate()
        {
            if (InteriorTab.Dirty)
            {
                UiClone.Safe(InteriorTab.Fix);
            }
        }
    }

    /// <summary>On the tab's button: the game switching it off is the game making its list of tabs again. No cost a frame.</summary>
    public sealed class InteriorTabWatch : MonoBehaviour
    {
        private void OnDisable()
        {
            InteriorTab.Dirty = true;
        }
    }

    /// <summary>On the tab's page: written each time it is shown. No cost a frame.</summary>
    public sealed class InteriorPageWatch : MonoBehaviour
    {
        private void OnEnable()
        {
            UiClone.Safe(InteriorTab.Write);
        }
    }
}
