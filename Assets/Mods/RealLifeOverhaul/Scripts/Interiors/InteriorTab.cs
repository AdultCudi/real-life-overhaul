using System;
using System.Collections.Generic;
using System.Reflection;
using Helpers;
using Localizor;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Interiors
{
    /// <summary>
    /// The "Interior" tab of BizMan for a business of the player's. On the left: the interior score against the
    /// district's standard, the decor score and each category's share, wear and cleanliness. On the right, four pages
    /// chosen with the game's own previous/next selector: what each figure is doing and what to add next (with a button
    /// to refresh the finishes); every placed item that counts, by category, with its points; today's customers by where
    /// they came from, as the game counts them; and the game's own rules for the business's type, what its customers
    /// want and what it needs to open, each met or not and the items that meet it. Both columns are copies of the scroll
    /// view BizMan shows a business's details in, so a long page scrolls.
    ///
    /// How it sits in the game's BizMan (BizManBusiness, decompiled): the tab buttons are the children of its private
    /// "menu", each a text with a button, named by its tab; the pages are the children of "containers" of the same
    /// names; the game shows a tab only while its name is in the private list "_tabs", which it makes again whenever a
    /// business is opened (SetUpTabs, which hides every other button), and switches page with its private SetTab.
    /// So the tab is a copy of the game's own "Marketing" button and a page of the same name, its name is put back at
    /// the end of "_tabs" each time the game has made the list again, and a click asks the game's own SetTab (after the
    /// same question the game asks about unsaved settings). The page is two of BizMan's own scroll views (its business
    /// details' white box) holding MyEmployees' lines, headlines, selector and buttons (Core/GameUi/InfoBox.Into),
    /// written when the page is shown, when its page is changed and after a refresh.
    ///
    /// Two other authors' mods add a tab to the same row (Meaningful Interiors, Meaningful Ambience): each keeps its
    /// own just before "Settings" every frame. This one goes after "Marketing" (or before "Settings" where a business
    /// has no marketing) and is placed again only when the game has made its list again, so they never take turns.
    ///
    /// Cost: nothing while BizMan is closed. While a business's page in BizMan is open, one flag test a frame (the
    /// keeper's LateUpdate); the work is done only after the game has made its list of tabs again.
    /// </summary>
    public static class InteriorTab
    {
        public const string Id = "RealLifeInteriors";
        private const float BoxWidth = 1150f, BoxGap = 90f;   // two columns over the 2,390 of the game's own Settings page

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

        // ---------------------------------------------------------------- putting it there

        /// <summary>A new session (a city loaded): the tab may be put in again, and its fit is logged again.</summary>
        public static void Begin()
        {
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
            // BizMan's private pieces, resolved once (Core/GameContract).
            _menu = GameContract.BizManMenu.Field?.GetValue(business) as Transform;
            _containers = GameContract.BizManContainers.Field?.GetValue(business) as Transform;
            _tabsField = GameContract.BizManTabs.Field;
            _setTab = GameContract.BizManSetTab.Method;
            Transform template = _menu != null ? (_menu.Find("Marketing") ?? _menu.Find("Insight") ?? _menu.Find("Settings")) : null;
            Transform shape = _containers != null ? _containers.Find("Settings") : null;
            Transform source = menu.myEmployees != null ? menu.myEmployees.transform.Find("Layout 30-70/Left/WhiteBox") : null;
            Transform details = GameContract.BizManDetails.Field?.GetValue(business) as Transform;
            Transform scroll = details != null ? details.Find("WhiteBox") : null;   // BizMan's own scroll view of a business's details
            if (_menu == null || _containers == null || _tabsField == null || _setTab == null || template == null || shape == null || source == null
                || template.GetComponent<TextMeshProUGUI>() == null)
            {
                UiClone.Fallback("BizMan is not built as expected, so the Interior tab is not added");
                _failed = true;
                return;
            }
            _business = business;

            // The button: the game's own, its click ours.
            _button = UnityEngine.Object.Instantiate(template.gameObject, _menu, false);
            _button.name = Id;
            _button.SetActive(false);
            UiClone.Key(_button.transform, "reallife_interior_tab");
            UiClone.Rewire(_button.transform, Clicked);
            _button.AddComponent<InteriorTabWatch>();

            // The page: the shape of the game's own Settings page, with two white boxes.
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
            Dirty = true;
            InteriorDirector.Logger?.Info("Interiors: the Interior tab is in BizMan.");
        }

        // One column of the page: a copy of BizMan's scroll view of a business's details, emptied, its content written with
        // MyEmployees' white box's parts. Where that scroll view is not there, MyEmployees' white box itself (it does not scroll).
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
                Place(parts.transform, column);
            }
            return plain;
        }

        // A column of the page: its whole height, a column wide.
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

        // ---------------------------------------------------------------- keeping it there

        /// <summary>
        /// After the game has made its list of tabs again: the tab is in it (at the end, so that the game's "first tab"
        /// stays its own) and its button shows, where it belongs; anywhere else it is out of it, and its page is left.
        /// </summary>
        internal static void Fix()
        {
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
            Dirty = false;   // (the button's own switching on is not a change of the game's list)
        }

        // The tab clicked: as the game's own buttons do, a question first if the settings page has changes not saved.
        private static void Clicked()
        {
            if (_business == null)
            {
                return;
            }
            if (_business.bizManSettings != null && _business.bizManSettings.HasUnsavedChanges)
            {
                HudConfirm.Show(null, "change_character_clothes_unsaved_changes_warning", () => UiClone.Safe(() => Show(Id)), null, null, null, true, true);
                return;
            }
            Show(Id);
        }

        // ---------------------------------------------------------------- the page

        /// <summary>The page written for the business BizMan shows now.</summary>
        internal static void Write()
        {
            if (_business == null || _left == null || _right == null)
            {
                return;
            }
            BuildingRegistration reg = _business.buildingRegistration;
            InteriorShop s = InteriorDirector.At(reg);
            _left.Clear();
            _right.Clear();
            if (s == null)
            {
                _left.Title(BusinessGame.Name(reg));
                _left.Line("reallife_interior_none".GetLocalization());
                Meta.Guide.Point(_left, GuideTitle);
                _left.Done();
                _right.Done();
                return;
            }
            WriteScores(s);
            _left.Done();
            WriteRight(s, reg);
            if (!_fitLogged)
            {
                _fitLogged = true;   // once a session: how the two columns' pages fit (they scroll when they do not)
                InteriorDirector.Logger?.Info("Interiors: the Interior tab's columns: " + _left.Fit() + "; " + _right.Fit() + ".");
            }
        }

        // The right column: the page selector, then the page chosen.
        private static void WriteRight(InteriorShop s, BuildingRegistration reg)
        {
            _right.Clear();
            _right.Selector(Pages, _shown, InteriorText.Page, page =>
            {
                _shown = page;
                InteriorShop now = InteriorDirector.At(reg);
                if (now != null)
                {
                    WriteRight(now, reg);
                }
            });
            switch (InteriorRules.Clamp(_shown, 0, Pages - 1))
            {
                case 1: WriteItems(s); break;
                case 2: WriteCustomers(s, reg); break;
                case 3: WriteRules(reg); break;
                default: WriteEffects(s, reg); break;
            }
            Meta.Guide.Point(_right, GuideTitle);
            _right.Done();
            if (_rightScroll != null)
            {
                _rightScroll.verticalNormalizedPosition = 1f;   // a new page starts at its top
            }
        }

        // Every placed item that counts, by category: its copies and its points, and each category's total against its share.
        private static void WriteItems(InteriorShop s)
        {
            InfoBox b = _right;
            b.Title(InteriorText.Page(1));
            if (s.Placed.Count == 0)
            {
                b.Line("reallife_interior_items_none".GetLocalization());
            }
            for (int c = 0; c < InteriorRules.Categories; c++)
            {
                Sorted.Clear();
                for (int i = 0; i < s.Placed.Count; i++)
                {
                    if ((int)s.Placed[i].Category == c)
                    {
                        Sorted.Add(s.Placed[i]);
                    }
                }
                if (Sorted.Count == 0)
                {
                    continue;
                }
                Sorted.Sort((x, y) => y.Points != x.Points ? y.Points.CompareTo(x.Points) : string.CompareOrdinal(x.Name, y.Name));
                int cap = (int)((long)s.Target * InteriorRules.CapShare[c] / 1000);
                b.HeadlineWords(InteriorText.T("reallife_interior_items_head", "category", InteriorText.Category(c), "points", InteriorText.Tenths(s.Capped[c]), "cap", InteriorText.Tenths(cap)));
                for (int i = 0; i < Sorted.Count; i++)
                {
                    b.Line(InteriorText.T("reallife_interior_items_line", "item", InteriorGame.ItemName(Sorted[i].Name), "copies", InteriorText.N(Sorted[i].Copies),
                        "points", InteriorText.Tenths(Sorted[i].Points)));
                }
            }
            Sorted.Clear();
            b.Line(InteriorText.T("reallife_interior_items_note", "repeat", InteriorText.N(InteriorRules.RepeatShare / 10), "most", InteriorText.N(InteriorRules.RepeatMost),
                "target", InteriorText.Tenths(s.Target)));
        }

        // Today's customers by where they came from, as the game counts them.
        private static void WriteCustomers(InteriorShop s, BuildingRegistration reg)
        {
            InfoBox b = _right;
            b.Title(InteriorText.Page(2));
            InteriorDirector d = InteriorDirector.Instance;
            CustomerCount c;
            if (d == null || !InteriorGame.Count(reg, d.Ours, out c))
            {
                b.Line("reallife_interior_cust_none".GetLocalization());
                return;
            }
            CustomerSplit split = InteriorRules.Split(c.Drawn, c.Booked, c.OtherMod, c.BaseMille, c.Traffic, c.Own, c.Written, BusinessCatalogue.ClampInterior(s.Footfall), c.OtherPercent);
            b.Line(InteriorText.T("reallife_interior_cust_total", "total", InteriorText.N(split.Total), "served", InteriorText.N(c.Served)));
            b.Line(InteriorText.T("reallife_interior_cust_game", "count", InteriorText.N(split.Game)));
            b.Line(InteriorText.T("reallife_interior_cust_marketing", "count", InteriorText.N(split.Marketing)));
            b.Line(InteriorText.T("reallife_interior_cust_interiors", "count", InteriorText.SignedN(split.Interiors)));
            b.Line(InteriorText.T("reallife_interior_cust_others", "count", InteriorText.SignedN(split.Others)));
            if (OtherMods.MeaningfulAmbience.Loaded)
            {
                b.Line(InteriorText.T("reallife_interior_cust_ambience", "mod", OtherMods.MeaningfulAmbience.Name, "count", InteriorText.N(split.OtherMod)));
            }
            if (c.Booked > 0)
            {
                b.Line(InteriorText.T("reallife_interior_cust_booked", "count", InteriorText.N(c.Booked)));
            }
            if (s.Stop != SaleStop.None)
            {
                b.Line(InteriorText.T("reallife_interior_line_paused", "reason", InteriorText.Stop(s.Stop)));
            }
            b.Line(InteriorText.T("reallife_interior_cust_note", "traffic", InteriorText.N(c.Traffic), "marketing", InteriorText.N(c.Marketing), "figure", InteriorText.N(c.Written)));
        }

        // The game's own rules for the business's type: what its customers want and what it needs to open.
        private static void WriteRules(BuildingRegistration reg)
        {
            InfoBox b = _right;
            b.Title(InteriorText.Page(3));
            InteriorGame.Rules(reg, GameRules);
            int demands = 0, needs = 0;
            b.Headline("reallife_interior_rules_demands_head");
            for (int i = 0; i < GameRules.Count; i++)
            {
                if (GameRules[i].Demand)
                {
                    demands++;
                    Rule(b, GameRules[i]);
                }
            }
            if (demands == 0)
            {
                b.Line("reallife_interior_rules_none".GetLocalization());
            }
            b.Headline("reallife_interior_rules_needs_head");
            for (int i = 0; i < GameRules.Count; i++)
            {
                if (!GameRules[i].Demand)
                {
                    needs++;
                    Rule(b, GameRules[i]);
                }
            }
            if (needs == 0)
            {
                b.Line("reallife_interior_rules_none".GetLocalization());
            }
            b.Line("reallife_interior_rules_note".GetLocalization());
            GameRules.Clear();
        }

        private static void Rule(InfoBox b, GameRule rule)
        {
            string state = (rule.Met ? "reallife_interior_rules_met" : "reallife_interior_rules_missing").GetLocalization();
            if (rule.Demand)
            {
                b.Line(InteriorText.T("reallife_interior_rules_demand", "name", InteriorText.Demand(rule), "state", state, "chance", InteriorText.N(Math.Max(0, rule.Chance))));
                string detail = InteriorText.DemandDetail(rule);
                if (detail != null)
                {
                    b.Line(detail);
                }
            }
            else
            {
                b.Line(InteriorText.T(rule.Need > 1 ? "reallife_interior_rules_need_count" : "reallife_interior_rules_need", "name", InteriorGame.ItemName(rule.Kind), "state", state,
                    "have", InteriorText.N(Math.Max(0, rule.Have)), "need", InteriorText.N(rule.Need)));
            }
            if (rule.Placed.Count > 0)
            {
                b.Line(InteriorText.T("reallife_interior_rules_by", "items", string.Join(", ", rule.Placed)));
            }
            else if (rule.Examples.Count > 0)
            {
                b.Line(InteriorText.T("reallife_interior_rules_add", "items", string.Join(", ", rule.Examples)));
            }
        }

        private static void WriteScores(InteriorShop s)
        {
            InfoBox b = _left;
            b.Title(s.Name);
            if (!OtherMods.MeaningfulInteriors.Loaded && s.Fitting && s.Kind == InteriorKind.Shop && InteriorDirector.StandardOn)
            {
                b.Line(InteriorText.T("reallife_interior_line_fitting", "day", InteriorText.N(s.StandardFrom)));
            }
            b.Headline("reallife_interior_head_place");
            b.Line(InteriorText.T("reallife_interior_line_overall", "overall", InteriorText.N(s.Overall)));
            if (OtherMods.MeaningfulInteriors.Loaded)
            {
                b.Line(InteriorText.T("reallife_interior_line_minimum_other", "minimum", InteriorText.N(s.GameMinimum), "mod", OtherMods.MeaningfulInteriors.Name));
            }
            else
            {
                b.Line(InteriorText.T("reallife_interior_line_standard", "standard", InteriorText.N(s.Standard), "district", Districts.DistrictGame.Name(s.District)));
            }
            b.Line(InteriorText.T(s.InteriorNow != s.Interior ? "reallife_interior_line_interior_worn" : "reallife_interior_line_interior",
                "score", InteriorText.N(s.Interior), "now", InteriorText.N(s.InteriorNow)));
            b.Line(InteriorText.T(s.Decor != s.DecorRaw ? "reallife_interior_line_decor_worn" : "reallife_interior_line_decor",
                "score", InteriorText.N(s.DecorRaw), "now", InteriorText.N(s.Decor)));
            b.Headline("reallife_interior_head_decor");
            for (int i = 0; i < InteriorRules.Categories; i++)
            {
                b.Line(InteriorText.T("reallife_interior_line_category", "category", InteriorText.Category(i), "filled", InteriorText.N(InteriorText.Filled(s, i)),
                    "items", InteriorText.N(s.Items[i])));
            }
            b.Line(InteriorText.T("reallife_interior_line_area", "sqm", InteriorText.N(s.Sqm)));
            b.Headline("reallife_interior_head_condition");
            b.Line(InteriorText.T(InteriorDirector.Parts.WearScale > 0 ? "reallife_interior_line_wear" : "reallife_interior_line_wear_off", "wear", InteriorText.N(s.Wear / 10)));
            b.Line(InteriorText.T("reallife_interior_line_clean", "clean", InteriorText.N(s.Clean)));
            Meta.Guide.Point(b, GuideTitle);
        }

        // What the interior does to a shop's customers and takings today, in plain figures, each with its reason: the
        // district's standard against the shop's figure, a dip of business pressure's the shortfall gives way to, the
        // decor, the days it is still coming in, the prices, and the two together.
        private static void WriteNow(InfoBox b, InteriorShop s)
        {
            InteriorParts parts = InteriorDirector.Parts;
            string district = Districts.DistrictGame.Name(s.District);
            if (InteriorDirector.StandardOn)
            {
                string standard = InteriorText.N(s.Standard), overall = InteriorText.N(s.Overall);
                if (s.Fitting && s.Overall < s.Standard)
                {
                    b.Line(InteriorText.T("reallife_interior_now_fitting", "district", district, "standard", standard, "overall", overall, "day", InteriorText.N(s.StandardFrom)));
                }
                else if (s.StandardFootfall < 0)
                {
                    b.Line(InteriorText.T("reallife_interior_now_short", "district", district, "standard", standard, "overall", overall, "percent", InteriorText.N(-s.StandardFootfall)));
                }
                else if (s.StandardFootfall > 0)
                {
                    b.Line(InteriorText.T("reallife_interior_now_over", "district", district, "standard", standard, "overall", overall, "percent", InteriorText.N(s.StandardFootfall)));
                }
                else
                {
                    b.Line(InteriorText.T("reallife_interior_now_level", "district", district, "standard", standard, "overall", overall));
                }
                if (s.StandardFull < 0 && s.StandardFootfall > InteriorRules.Scale(s.StandardFull, s.Ramp))
                {
                    b.Line(InteriorText.T("reallife_interior_now_giveway", "dip", InteriorText.N(-Math.Min(0, s.IssueDip)), "bound", InteriorText.N(s.Bound)));
                }
            }
            if (InteriorDirector.DecorOn)
            {
                b.Line(InteriorText.T("reallife_interior_now_decor", "decor", InteriorText.N(s.Decor), "percent", InteriorText.N(s.DecorFootfall)));
            }
            if (s.Ramp < 1000 && (s.StandardFull != 0 || s.DecorFull != 0 || s.Bears != 0 || s.Basket != 0))
            {
                b.Line(InteriorText.T("reallife_interior_now_phase", "share", InteriorText.N(s.Ramp / 10), "day", InteriorText.N(InteriorRules.FullFrom(s.PhaseFrom))));
            }
            if (parts.PriceScale <= 0)
            {
                b.Line("reallife_interior_line_prices_off".GetLocalization());
            }
            else
            {
                b.Line(s.Bears > 0 ? InteriorText.T("reallife_interior_now_prices", "bears", InteriorText.Mille(s.Bears)) : "reallife_interior_now_prices_none".GetLocalization());
            }
            b.Line(InteriorText.T("reallife_interior_now_total", "customers", InteriorText.Signed(s.Footfall), "takings", InteriorText.SignedMille(InteriorRules.Takings(s.Footfall, s.Basket))));
        }

        private static void WriteEffects(InteriorShop s, BuildingRegistration reg)
        {
            InfoBox b = _right;
            b.Title(InteriorText.Page(0));
            if (s.Stop != SaleStop.None)
            {
                b.Line(InteriorText.T("reallife_interior_line_paused", "reason", InteriorText.Stop(s.Stop)));
            }
            if (s.Kind == InteriorKind.Shop)
            {
                WriteNow(b, s);
                b.Line(InteriorDirector.BasketOn ? InteriorText.T("reallife_interior_line_basket", "share", InteriorText.Mille(s.Basket),
                        "most", InteriorText.Mille(InteriorRules.BasketShare(InteriorRules.BasketFullAt, s.Standard, InteriorDirector.Parts.PriceScale)), "full", InteriorText.N(InteriorRules.BasketFullAt))
                    : OtherMods.MeaningfulInteriors.Loaded && Settings.InteriorsBasket ? InteriorText.T("reallife_interior_line_basket_other", "mod", OtherMods.MeaningfulInteriors.Name)
                    : "reallife_interior_line_basket_off".GetLocalization());
                if (InteriorDirector.StandardOn)
                {
                    b.Line(s.ComplaintChance > 0 ? InteriorText.T("reallife_interior_line_complaints", "chance", InteriorText.Mille(s.ComplaintChance)) : "reallife_interior_line_complaints_none".GetLocalization());
                }
                if (Settings.InteriorsStanding)
                {
                    b.Line(InteriorText.T("reallife_interior_line_standing", "brand", InteriorText.SignedN(s.BrandPoints), "standing", InteriorText.SignedN(s.StandingPoints)));
                }
            }
            else
            {
                b.Line("reallife_interior_line_office".GetLocalization());
                if (s.OfficeBoost > 0)
                {
                    int standing;
                    int booked = InteriorDirector.Instance != null ? InteriorDirector.Instance.BookedToday(reg, out standing) : 0;
                    b.Line(InteriorText.T("reallife_interior_line_clients", "boost", InteriorText.SignedMille(s.OfficeBoost), "booked", InteriorText.N(booked)));
                }
                else if (OtherMods.MeaningfulAmbience.Loaded)
                {
                    b.Line(InteriorText.T("reallife_interior_line_clients_other", "mod", OtherMods.MeaningfulAmbience.Name));
                }
            }
            if (Settings.InteriorsStaff)
            {
                b.Line(InteriorText.T("reallife_interior_line_comfort", "comfort", InteriorText.N(s.Comfort), "most", InteriorText.N(InteriorRules.ComfortMost)));
                b.Line(InteriorText.T("reallife_interior_line_bids", "bids", InteriorText.Mille(s.OfficeBid)));
                if (s.Kind == InteriorKind.Office)
                {
                    b.Line(InteriorText.T("reallife_interior_line_esteem", "esteem", InteriorText.N(s.OfficeEsteem)));
                }
            }
            if (OtherMods.MeaningfulInteriors.Loaded)
            {
                b.Line(InteriorText.T("reallife_interior_line_other_interiors", "mod", OtherMods.MeaningfulInteriors.Name));
            }
            if (OtherMods.MeaningfulAmbience.Loaded)
            {
                b.Line(InteriorText.T("reallife_interior_line_other_ambience", "mod", OtherMods.MeaningfulAmbience.Name));
            }
            b.Headline("reallife_interior_head_next");
            int next = InteriorRules.Next(s);
            b.Line(InteriorText.Next(next));
            if (s.Kind == InteriorKind.Shop && InteriorDirector.StandardOn && s.Overall < s.Standard && s.InteriorNow < s.Standard)
            {
                b.Line("reallife_interior_next_walls".GetLocalization());
            }
            if (Settings.InteriorsStaff && !s.RestCorner)
            {
                b.Line("reallife_interior_next_rest".GetLocalization());
            }
            if (InteriorDirector.Parts.WearScale > 0)
            {
                if (s.Wear >= InteriorRules.WornFrom)
                {
                    b.Line("reallife_interior_next_worn".GetLocalization());
                }
                Transform row = b.ButtonRow();
                InfoBox.Button(row, InfoBox.Positive, "reallife_interior_refresh_button", () =>
                {
                    InteriorDirector d = InteriorDirector.Instance;
                    if (d != null && d.Refresh(reg))
                    {
                        UiClone.Safe(Write);
                    }
                });
                b.Line(InteriorText.T("reallife_interior_refresh_cost", "amount", Economy.FinanceText.Money(InteriorGame.RefreshCost(s.Sqm))));
            }
        }
    }

    /// <summary>On the game's BizManBusiness: after the game has made its list of tabs again, the Interior tab is put back. One flag test a frame while BizMan shows a business.</summary>
    public sealed class InteriorTabKeeper : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("interiors", "InteriorTabKeeper", typeof(InteriorTabKeeper));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private void OnEnable()
        {
            InteriorTab.Dirty = true;
        }

        private static readonly int HotLateUpdate = HotSpots.Slot("Interiors: InteriorTabKeeper.LateUpdate");
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (System.Exception e)
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

    /// <summary>On the tab's button: the game switching it off is the game making its list of tabs again.</summary>
    public sealed class InteriorTabWatch : MonoBehaviour
    {
        private void OnDisable()
        {
            InteriorTab.Dirty = true;
        }
    }

    /// <summary>On the tab's page: written each time it is shown.</summary>
    public sealed class InteriorPageWatch : MonoBehaviour
    {
        private void OnEnable()
        {
            UiClone.Safe(InteriorTab.Write);
        }
    }
}
