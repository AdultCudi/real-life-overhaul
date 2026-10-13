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
    /// The "Interior" tab of BizMan for a business of the player's: the interior score against the district's
    /// standard, the decor score and each category's share, wear and cleanliness, what each figure is doing, and what
    /// to add next, with a button to refresh the finishes.
    ///
    /// How it sits in the game's BizMan (BizManBusiness, decompiled): the tab buttons are the children of its private
    /// "menu", each a text with a button, named by its tab; the pages are the children of "containers" of the same
    /// names; the game shows a tab only while its name is in the private list "_tabs", which it makes again whenever a
    /// business is opened (SetUpTabs, which hides every other button), and switches page with its private SetTab.
    /// So the tab is a copy of the game's own "Marketing" button and a page of the same name, its name is put back at
    /// the end of "_tabs" each time the game has made the list again, and a click asks the game's own SetTab (after the
    /// same question the game asks about unsaved settings). The page is two white boxes of the game's (MyEmployees'
    /// own, Core/GameUi/InfoBox), written when the page is shown and after a refresh.
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
            const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
            Type type = typeof(BizManBusiness);
            _menu = type.GetField("menu", Private)?.GetValue(business) as Transform;
            _containers = type.GetField("containers", Private)?.GetValue(business) as Transform;
            _tabsField = type.GetField("_tabs", Private);
            _setTab = type.GetMethod("SetTab", Private, null, new[] { typeof(string) }, null);
            Transform template = _menu != null ? (_menu.Find("Marketing") ?? _menu.Find("Insight") ?? _menu.Find("Settings")) : null;
            Transform shape = _containers != null ? _containers.Find("Settings") : null;
            Transform source = menu.myEmployees != null ? menu.myEmployees.transform.Find("Layout 30-70/Left/WhiteBox") : null;
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
            _left = Box(source, 0);
            _right = Box(source, 1);
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

        // A copy of MyEmployees' white box as one column of the page.
        private static InfoBox Box(Transform source, int column)
        {
            Transform dormant = UiClone.Dormant(_page.transform, "Templates" + column);
            GameObject copy = UiClone.Asleep(source.gameObject, dormant, "Box" + column);
            if (copy == null)
            {
                return null;
            }
            UiClone.StripGameScripts(copy, script => Array.IndexOf(AppShell.Harmless, script.GetType().Name) >= 0);
            InfoBox box = InfoBox.From(copy.transform, dormant);
            if (box == null)
            {
                return null;
            }
            copy.transform.SetParent(_page.transform, false);
            var rect = (RectTransform)copy.transform;
            rect.anchorMin = new Vector2(0f, 0f);   // the page's whole height, a column wide
            rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(column * (BoxWidth + BoxGap), 0f);
            rect.sizeDelta = new Vector2(BoxWidth, 0f);
            copy.SetActive(true);
            return box;
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
                _left.Done();
                _right.Done();
                return;
            }
            WriteScores(s);
            WriteEffects(s, reg);
            _left.Done();
            _right.Done();
            if (!_fitLogged)
            {
                _fitLogged = true;   // once a session: how the two columns fit the page (the boxes do not scroll)
                InteriorDirector.Logger?.Info("Interiors: the Interior tab's columns: " + _left.Fit() + "; " + _right.Fit() + ".");
            }
        }

        private static void WriteScores(InteriorShop s)
        {
            InfoBox b = _left;
            b.Title(s.Name);
            b.Headline("reallife_interior_head_place");
            b.Line(InteriorText.T(s.InteriorNow != s.Interior ? "reallife_interior_line_interior_worn" : "reallife_interior_line_interior",
                "score", InteriorText.N(s.Interior), "now", InteriorText.N(s.InteriorNow)));
            if (OtherMods.MeaningfulInteriors.Loaded)
            {
                b.Line(InteriorText.T("reallife_interior_line_minimum_other", "minimum", InteriorText.N(s.GameMinimum), "mod", OtherMods.MeaningfulInteriors.Name));
            }
            else
            {
                b.Line(InteriorText.T("reallife_interior_line_standard", "standard", InteriorText.N(s.Standard), "district", Districts.DistrictGame.Name(s.District)));
                if (s.Fitting && s.Kind == InteriorKind.Shop && InteriorDirector.StandardOn)
                {
                    b.Line(InteriorText.T("reallife_interior_line_fitting", "day", InteriorText.N(s.StandardFrom)));
                }
            }
            b.Line(InteriorText.T(s.Decor != s.DecorRaw ? "reallife_interior_line_decor_worn" : "reallife_interior_line_decor",
                "score", InteriorText.N(s.DecorRaw), "now", InteriorText.N(s.Decor)));
            b.Line(InteriorText.T("reallife_interior_line_overall", "overall", InteriorText.N(s.Overall)));
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
        }

        private static void WriteEffects(InteriorShop s, BuildingRegistration reg)
        {
            InfoBox b = _right;
            b.Title("reallife_interior_effects_title".GetLocalization());
            if (s.Stop != SaleStop.None)
            {
                b.Line(InteriorText.T("reallife_interior_line_paused", "reason", InteriorText.Stop(s.Stop)));
            }
            if (s.Kind == InteriorKind.Shop)
            {
                b.Line(InteriorText.T("reallife_interior_line_customers", "footfall", InteriorText.Signed(s.Footfall), "standard", InteriorText.Signed(s.StandardFootfall),
                    "decor", InteriorText.Signed(s.DecorFootfall)));
                b.Line(InteriorDirector.Parts.PriceScale > 0 ? InteriorText.T("reallife_interior_line_prices", "bears", InteriorText.SignedMille(s.Bears)) : "reallife_interior_line_prices_off".GetLocalization());
                b.Line(InteriorDirector.BasketOn ? InteriorText.T("reallife_interior_line_basket", "chance", InteriorText.Mille(s.Basket))
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
                        Write();
                    }
                });
                b.Line(InteriorText.T("reallife_interior_refresh_cost", "amount", Economy.FinanceText.Money(InteriorGame.RefreshCost(s.Sqm))));
            }
        }
    }

    /// <summary>On the game's BizManBusiness: after the game has made its list of tabs again, the Interior tab is put back. One flag test a frame while BizMan shows a business.</summary>
    public sealed class InteriorTabKeeper : MonoBehaviour
    {
        private void OnEnable()
        {
            InteriorTab.Dirty = true;
        }

        private void LateUpdate()
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
