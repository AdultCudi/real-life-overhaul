using System;
using System.Collections.Generic;
using Extensions;
using Streets;
using TMPro;
using UI.Smartphone.Apps.BizMan;
using UI.Smartphone.Apps.BizMan.StartBusiness;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A place's page: the game's tab bar as the sidebar (its own tabs by their names, the mod's Interior tab, another mod's
    /// tab by its own words), the head from the business view's own header, and the shown tab's page read from that tab's
    /// own widgets after the game filled them. The kit follows the tab the game shows; a tab only the game's own view
    /// shows is a handover page until it is opened there. Cost: a layout a change.
    /// </summary>
    public sealed partial class BizManScreen
    {
        private readonly List<Transform> _tabs = new List<Transform>(20);
        private readonly List<Transform> _extras = new List<Transform>(4);   // another mod's own pieces on the business view, outside its tab bar
        private readonly List<string> _words = new List<string>(16);
        private string _shown, _gameTab;
        private global::Address _shownFor;
        private bool _openedByKit, _listOpen, _todoOpen, _kindOpen, _fontOpen, _sliderHeld, _pagesOpen;

        private Transform Menu(BizManBusiness b) => Get<Transform>(GameContract.BizMenu, b);

        private Transform Page(BizManBusiness b, string tab)
        {
            Transform containers = Get<Transform>(GameContract.BizContainers, b);
            return containers != null ? containers.Find(tab) : null;
        }

        private T PageOf<T>(BizManBusiness b, string tab) where T : Component
        {
            Transform page = Page(b, tab);
            return page != null ? page.GetComponentInChildren<T>(true) : null;
        }

        // ------------------------------------------------------------------------------------------------- the page

        private void BuildPage(BizManBusiness b)
        {
            _m.Page = BizManPage.Business;
            string gameTab = GameContract.BizTab.Field?.GetValue(b) as string;
            if (!Equals(b.address, _shownFor) || gameTab != _gameTab)
            {
                // A new place, or the game moved its own tab (a business started, the settings saved): the kit follows it.
                bool fresh = !Equals(b.address, _shownFor);
                _shownFor = b.address;
                _gameTab = gameTab;
                _shown = gameTab;
                if (fresh)
                {
                    CloseLists();
                    if (_openedByKit && !BizManRules.Drawn(gameTab, Ops) && PressTab(b, BizManRules.Presentation))
                    {
                        _shown = _gameTab = BizManRules.Presentation;   // a row of the portfolio opens the place's building, not a page the kit hands over
                    }
                }
                _openedByKit = false;
            }
            if (BizManRules.Drawn(_shown, Ops) && _shown != _gameTab)
            {
                _shown = _gameTab;   // a drawn page is always the one the game shows: the kit never shows one page over another
            }
            Nav(b);
            Head(b);
            ClearPart();
            if (!BizManRules.Drawn(_shown, Ops))
            {
                Handover(b);
                return;
            }
            switch (BizManRules.PartOf(_shown, Ops))
            {
                case BizManPart.Schedule: Schedule(b); break;
                case BizManPart.Managers: Managers(b); break;
                case BizManPart.Factory: Factory(b); break;
                case BizManPart.Drivers: Fleet(b); break;
                case BizManPart.Stock: Stock(b); break;
                case BizManPart.Insight: Insight(b); break;
                case BizManPart.Pricing: Pricing(b); break;
                case BizManPart.Deliveries: Deliveries(b); break;
                case BizManPart.Marketing: Marketing(b); break;
                case BizManPart.Settings: Settings(b); break;
                case BizManPart.Estate: Estate(b); break;
                case BizManPart.Interior: Interior(b); break;
                default: Presentation(b); break;
            }
        }

        // The game's tab bar as the sidebar, in its order, only the tabs it shows (Going Public's pruning stays the truth);
        // then another mod's pieces on the business view, offered as one more entry.
        private void Nav(BizManBusiness b)
        {
            _tabs.Clear();
            Transform menu = Menu(b);
            for (int i = 0; menu != null && i < menu.childCount; i++)
            {
                Transform tab = menu.GetChild(i);
                if (tab.gameObject.activeSelf)
                {
                    _tabs.Add(tab);
                }
            }
            Extras(b);
            NoteForeign();
            int pages = KitForOtherMods.PagesOfBizMan.Count;   // pages other mods added through the kit's interface (docs/reference/kit-for-other-mods.md)
            var nav = new UiNavEntry[_tabs.Count + (_extras.Count > 0 ? 1 : 0) + pages];
            int shown = 0;
            for (int i = 0; i < _tabs.Count; i++)
            {
                string name = _tabs[i].name;
                UiIcon icon = BizManRules.TabIcon(name);
                nav[i] = new UiNavEntry { Icon = icon, Tint = UiIconTints.Of(icon), Label = TabWords(_tabs[i]) };
                shown = name == _shown ? i : shown;
            }
            if (_extras.Count > 0)
            {
                nav[_tabs.Count] = new UiNavEntry { Icon = UiIcon.Code, Tint = UiIconTints.Of(UiIcon.Code), Label = UiText.T("reallife_ui_bizman_other_mods"), Count = UiText.Whole(_extras.Count) };
                shown = _shown == ExtrasName ? _tabs.Count : shown;
            }
            int first = _tabs.Count + (_extras.Count > 0 ? 1 : 0);
            for (int p = 0; p < pages; p++)
            {
                nav[first + p] = new UiNavEntry { Icon = UiIcon.Code, Tint = UiIconTints.Of(UiIcon.Code), Label = KitForOtherMods.PagesOfBizMan[p].Title };
            }
            _m.Nav = nav;
            _m.NavSelected = shown;
        }

        private const string ExtrasName = "RealLife other mods";   // the kit's own entry for other mods' pieces outside the tab bar

        private readonly HashSet<int> _noted = new HashSet<int>();   // the foreign pieces said already: said once, so no text is built for one seen again

        // Which mod's tabs and pieces the sidebar holds, said once each in Player.log (the self-check names them too).
        private void NoteForeign()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                Transform tab = _tabs[i];
                if (!UiHostRules.Own(tab.name, BizManRules.GameTabs) && _noted.Add(tab.GetInstanceID()))
                {
                    UiForeign.Note("bizman", UiPieceKind.Tab, UiForeignGame.OwnerOf(tab) ?? tab.name, "the business page's tab bar: " + tab.name,
                        "its own entry in the sidebar; its page opens in the game's own view");
                }
            }
            for (int i = 0; i < _extras.Count; i++)
            {
                Transform extra = _extras[i];
                if (_noted.Add(extra.GetInstanceID()))
                {
                    UiForeign.Note("bizman", UiForeignGame.KindOf(extra), UiForeignGame.OwnerOf(extra) ?? extra.name, "the business page: " + extra.name,
                        "listed under Other mods in the sidebar; opens in the game's own view");
                }
            }
        }

        // A tab's name in the sidebar: the kit's short words for the game's own (the game's longer ones do not fit every
        // language beside a glyph), the mod's own, or another mod's own words.
        private static string TabWords(Transform tab)
        {
            switch (tab.name)
            {
                case BizManRules.Presentation: return UiText.T("reallife_ui_bizman_tab_building");
                case BizManRules.Insight: return UiText.T("bizman_menu_insight");
                case BizManRules.Pricing: return UiText.T("reallife_ui_bizman_tab_prices");
                case BizManRules.Schedule: return UiText.T("bizman_menu_schedule");
                case BizManRules.Deliveries: return UiText.T("bizman_menu_deliveries");
                case BizManRules.Marketing: return UiText.T("bizman_menu_marketing");
                case BizManRules.Settings: return UiText.T("bizman_menu_settings");
                case BizManRules.RealEstate: return UiText.T("reallife_ui_bizman_tab_estate");
                case BizManRules.Drivers: return UiText.T("bizman_menu_drivers");
                case BizManRules.Inventory: return UiText.T("bizman_menu_inventory");
                case BizManRules.Factory: return UiText.T("bizman_menu_factory");
                case BizManRules.PricingManagers: return UiText.T("reallife_ui_bizman_tab_pricing");
                case BizManRules.LogisticsManagers: return UiText.T("reallife_ui_bizman_tab_logistics");
                case BizManRules.PurchasingAgents: return UiText.T("reallife_ui_bizman_tab_purchasing");
                case BizManRules.HrManagers: return UiText.T("reallife_ui_bizman_tab_hr");
                case BizManRules.Headhunters: return UiText.T("bizman_menu_headhunters");
                case BizManRules.Interiors: return UiText.T("reallife_interior_tab");
                case BizManRules.Loyalty: return UiText.T("reallife_loyalty_tab");
                default: return Label(tab) ?? UiText.T("reallife_ui_bizman_foreign_tab");
            }
        }

        // Another mod's own pieces on the business view outside its tab bar: a piece beside the page, told by the code that
        // runs it (neither the game's, Unity's nor the mod's) or by a button of its mod's making under it, and a button a mod
        // put inside the page the game shows (the way into its own window, beside the game's buttons), which the kit's page
        // does not draw. Only those the kit's window would hide count; one with a canvas of its own stands over the kit as it
        // stood over the game.
        private static readonly List<MonoBehaviour> Behaviours = new List<MonoBehaviour>(32);
        private static readonly List<Button> PageButtons = new List<Button>(64);
        private readonly Dictionary<int, bool> _judged = new Dictionary<int, bool>(64);   // each button of the pages told once: another mod's or not

        private void Extras(BizManBusiness b)
        {
            _extras.Clear();
            Transform root = b.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform c = root.GetChild(i);
                Canvas own = c.GetComponent<Canvas>();
                if (c.gameObject.activeSelf && c.name != "Layout 30-70" && (own == null || !own.overrideSorting) && Foreign(c))
                {
                    _extras.Add(c);
                }
            }
            Transform layout = root.Find("Layout 30-70"), menu = Menu(b);
            if (layout == null)
            {
                return;
            }
            PageButtons.Clear();
            layout.GetComponentsInChildren(false, PageButtons);   // the page the game shows now: a button on another tab's page is met on that tab
            for (int i = 0; i < PageButtons.Count; i++)
            {
                Button button = PageButtons[i];
                if (menu != null && button.transform.IsChildOf(menu))
                {
                    continue;   // the tab bar's are sections of the sidebar already
                }
                int id = button.GetInstanceID();
                if (!_judged.TryGetValue(id, out bool others))
                {
                    others = UiForeignGame.ButtonOwner(button) != null;
                    _judged[id] = others;
                }
                if (others)
                {
                    _extras.Add(button.transform);
                }
            }
            PageButtons.Clear();
        }

        private static bool Foreign(Transform t)
        {
            if (!UiHostRules.Own(t.name, BizManRules.GameTabs) && t.name.StartsWith(UiHostRules.ModPrefix, StringComparison.Ordinal))
            {
                return false;
            }
            Behaviours.Clear();
            t.GetComponentsInChildren(true, Behaviours);
            bool foreign = false;
            for (int i = 0; i < Behaviours.Count && !foreign; i++)
            {
                string assembly = Behaviours[i] != null ? Behaviours[i].GetType().Assembly.GetName().Name : null;
                foreign = assembly != null && !assembly.StartsWith("BigAmbitions", StringComparison.Ordinal) && !assembly.StartsWith("Unity", StringComparison.Ordinal)
                          && assembly != "Assembly-CSharp" && assembly != "ExternalPlugins" && assembly != "HGPlugins" && assembly != typeof(BizManScreen).Assembly.GetName().Name;
            }
            Behaviours.Clear();
            if (foreign)
            {
                return true;
            }
            // A plain copy of a game button with the mod's own listener carries no script of its mod's: told by who listens to it.
            PageButtons.Clear();
            t.GetComponentsInChildren(true, PageButtons);
            for (int i = 0; i < PageButtons.Count && !foreign; i++)
            {
                foreign = UiForeignGame.ButtonOwner(PageButtons[i]) != null;
            }
            PageButtons.Clear();
            return foreign;
        }

        // ------------------------------------------------------------------------------------------------- the head

        private void Head(BizManBusiness b)
        {
            BuildingRegistration reg = b.buildingRegistration;
            UiFirmHeadModel h = _m.Head ?? new UiFirmHeadModel();
            _m.Head = h;
            Image logo = Get<Image>(GameContract.BizLogo, b);
            string kindKey = reg.businessTypeName;
            bool running = !string.IsNullOrEmpty(reg.BusinessName);
            h.Name = running ? reg.BusinessName : (b.address != null ? b.address.ToFormattedString() : null);
            h.Picture = running && logo != null && logo.gameObject.activeInHierarchy ? Pic(logo.sprite) : null;
            h.Aspect = Aspect(logo != null ? logo.sprite : null);
            h.Glyph = h.KindIcon = BizManRules.KindIcon(kindKey, b.building != null ? b.building.BuildingType : null);
            h.Kind = running ? (Words(Get<Component>(GameContract.BizKind, b)) ?? UiText.T(kindKey)) : (b.building != null ? UiText.T(b.building.BuildingType) : null);
            h.Place = running ? (b.address != null ? b.address.ToFormattedString() : null)
                : b.building != null && !string.IsNullOrEmpty(b.building.Neighbourhood) ? UiText.T(b.building.Neighbourhood) : null;   // the name is the address already
            UI.Elements.Dropdown names = Get<UI.Elements.Dropdown>(GameContract.BizSwitch, b);
            List<string> options = Get<List<string>>(GameContract.BizOptions, names);
            bool localize = GameContract.BizOptionsLocalized.Field != null && names != null && (bool)GameContract.BizOptionsLocalized.Field.GetValue(names);
            bool own = running && reg.RentedByPlayer;   // the game's header, with its switch, its toggle and its needs, stands only on the player's own places
            h.Others = new string[own && options != null ? options.Count : 0];
            for (int i = 0; i < h.Others.Length; i++)
            {
                h.Others[i] = localize ? UiText.T(options[i]) : options[i];
            }
            h.Chosen = names != null ? names.SelectedOptionIndex : -1;
            h.ListOpen = _listOpen && h.Others.Length > 1;
            Toggle closed = Get<Toggle>(GameContract.BizClosed, b);
            h.HasSwitch = own && closed != null && closed.transform.parent != null && closed.transform.parent.gameObject.activeSelf;
            h.On = closed != null && !closed.isOn;
            h.SwitchWord = UiText.T(h.On ? "common_open" : "bizman_temporarily_closed");
            h.SwitchDisabled = closed != null && !closed.interactable;
            Chips(b, h);
            Needs(b, h, own);
            h.TodoOpen = _todoOpen && h.Todo != null;
            h.Actions = running && reg.RentedByPlayer
                ? new[] { new UiActionModel { Label = UiText.T("common_open_in_econoview"), Kind = UiButtonKind.Secondary, Icon = UiIcon.ChartPie, Key = UiAction.Option,
                    Command = UiFirmHead.First + UiFirmHead.Action, Arg = BizManPages.DoEconoView } }
                : new UiActionModel[0];
        }

        // The business's state as chips: its security as the game words and colours it, and its open state when it is closed
        // now though not closed by its switch (outside its hours).
        private static void Chips(BizManBusiness b, UiFirmHeadModel h)
        {
            var words = new List<string>(2);
            var tones = new List<UiRole>(2);
            GameObject security = Get<GameObject>(GameContract.BizSecurity, b);
            Component label = Get<Component>(GameContract.BizSecurityLabel, b);
            if (security != null && security.activeInHierarchy && Words(label) is string level)
            {
                TMP_Text ink = label.GetComponentInChildren<TMP_Text>(true);
                words.Add(UiText.T("bizman_security") + " " + level);
                tones.Add(ink != null ? Tone(ink.color) : UiRole.Muted);
            }
            BuildingRegistration reg = b.buildingRegistration;
            if (!string.IsNullOrEmpty(reg.BusinessName) && reg.RentedByPlayer)
            {
                var status = reg.GetOpenStatus();
                UiRole tone = Tone(status.Item2);
                string said = status.Item1.ToString();
                if (tone != UiRole.Ok && !string.IsNullOrEmpty(said) && h.On)
                {
                    words.Add(said);
                    tones.Add(tone == UiRole.Muted ? UiRole.Muted : tone);
                }
            }
            if (!reg.RentedByPlayer && !reg.BuildingOwnedByPlayer && !string.IsNullOrEmpty(reg.businessOwnerRivalId) && !string.IsNullOrEmpty(reg.BusinessName))
            {
                words.Add(UiText.T("reallife_ui_bizman_rival"));
                tones.Add(UiRole.Muted);
            }
            if (reg.AvailableForRent && !reg.RentedByPlayer)
            {
                words.Add(UiText.T("reallife_ui_bizman_for_rent"));
                tones.Add(UiRole.Accent);
            }
            else if (reg.RentedByPlayer && string.IsNullOrEmpty(reg.BusinessName))
            {
                words.Add(UiText.T("reallife_ui_bizman_rented"));
                tones.Add(UiRole.Accent);
            }
            if (!reg.BuildingOwnedByPlayer && SaveGameManager.Current != null && SaveGameManager.Current.buildingsForSale.Exists(x => x.address == reg.Address))
            {
                words.Add(UiText.T("reallife_ui_bizman_for_sale"));
                tones.Add(UiRole.Accent);
            }
            h.Chips = words.ToArray();
            h.ChipTones = tones.ToArray();
        }

        // The requirements and the to-do lines the game wrote into its header: a link that says how many are missing, the
        // list under it each as a check line, each alert behind the warning's glyph.
        private void Needs(BizManBusiness b, UiFirmHeadModel h, bool own)
        {
            Transform need = own ? Get<Transform>(GameContract.BizNeed, b) : null, alert = own ? Get<Transform>(GameContract.BizAlert, b) : null;
            var checks = new List<string>(8);
            var met = new List<bool>(8);
            for (int i = 0; need != null && need.parent != null && i < need.parent.childCount; i++)
            {
                Transform c = need.parent.GetChild(i);
                if (c == need || !c.gameObject.activeSelf)
                {
                    continue;
                }
                checks.Add(Label(c));
                Toggle tick = c.GetComponentInChildren<Toggle>(true);
                met.Add(tick != null && tick.isOn);
            }
            var alerts = new List<string>(4);
            var urgent = new List<bool>(4);
            for (int i = 0; alert != null && alert.parent != null && i < alert.parent.childCount; i++)
            {
                Transform c = alert.parent.GetChild(i);
                if (c == alert || !c.gameObject.activeSelf)
                {
                    continue;
                }
                alerts.Add(Label(c));
                Transform icon = c.Find("Icon");
                Image mark = icon != null ? icon.GetComponent<Image>() : null;
                urgent.Add(mark != null && Tone(mark.color) == UiRole.Danger);
            }
            int missing = 0;
            for (int i = 0; i < met.Count; i++)
            {
                missing += met[i] ? 0 : 1;
            }
            h.Checks = checks.ToArray();
            h.Met = met.ToArray();
            h.Alerts = alerts.ToArray();
            h.Urgent = urgent.ToArray();
            h.TodoTitle = UiText.T("common_requirements");
            h.Todo = missing > 0 ? UiText.Fill("reallife_ui_bizman_needs", "missing", UiText.Whole(missing), "count", UiText.Whole(checks.Count))
                : alerts.Count == 1 ? UiText.T("reallife_ui_bizman_sum_alert_one") : alerts.Count > 0 ? UiText.Fill("reallife_ui_bizman_sum_alerts", "count", UiText.Whole(alerts.Count)) : null;
            h.TodoTone = missing > 0 || alerts.Count > 0 ? UiRole.Warn : UiRole.Ok;
        }

        // ------------------------------------------------------------------------------------------------- what changed

        private long PageSignature(BizManBusiness b)
        {
            long sig = (_listOpen ? 1 : 0) + (_todoOpen ? 2 : 0) + (_kindOpen ? 4 : 0) + (_fontOpen ? 8 : 0) + (_sliderHeld ? 16 : 0) + (_pagesOpen ? 32 : 0);
            UI.Elements.Dropdown names = Get<UI.Elements.Dropdown>(GameContract.BizSwitch, b);
            Toggle closed = Get<Toggle>(GameContract.BizClosed, b);
            Transform need = Get<Transform>(GameContract.BizNeed, b), alert = Get<Transform>(GameContract.BizAlert, b);
            sig = sig * 31 + (names != null ? names.SelectedOptionIndex : -1);
            sig = sig * 31 + (closed != null ? (closed.isOn ? 1 : 0) + (closed.interactable ? 2 : 0) : 3);
            sig = sig * 31 + EntryCount(need) * 7 + EntryCount(alert);
            sig = sig * 31 + _m.Ops.ListOpen + _opsHeld * 7 + (Ops ? 1 : 0);
            switch (_shown)
            {
                case BizManRules.Schedule: return sig * 31 + ScheduleSignature(b);
                case BizManRules.PricingManagers: case BizManRules.LogisticsManagers: case BizManRules.PurchasingAgents: case BizManRules.HrManagers: case BizManRules.Headhunters:
                    return sig * 31 + ManagersSignature(b);
                case BizManRules.Factory: case BizManRules.Drivers: case BizManRules.Inventory: return sig * 31 + PlantSignature(b);
                case BizManRules.Presentation: return sig * 31 + PresentationSignature(b);
                case BizManRules.Interiors: return sig * 31 + InteriorSignature();
                case BizManRules.Loyalty: return sig * 31 + LoyaltySignature();
                case BizManRules.Insight:
                {
                    Extensions.ChartController chart = Get<Extensions.ChartController>(GameContract.SeeChart, b.bizManInsight);
                    var data = chart != null && chart.chart != null ? chart.chart.GetChartData() : null;
                    return sig * 31 + (data != null && data.DataSets.Count > 0 ? data.DataSets[0].Entries.Count : 0);
                }
                case BizManRules.Pricing:
                {
                    InventoryProductsScrollerController table = b.bizManInventoryPricing != null ? b.bizManInventoryPricing.inventoryProductsScrollerController : null;
                    for (int i = 0; table != null && i < table.data.Count; i++)
                    {
                        sig = sig * 31 + (table.data[i] != null && table.data[i].RetailPriceReference != null ? table.data[i].RetailPriceReference.price.GetHashCode() : 0);
                    }
                    return sig;
                }
                case BizManRules.Deliveries:
                {
                    BizManDeliveries deals = PageOf<BizManDeliveries>(b, BizManRules.Deliveries);
                    BizManContractSettings set = Get<BizManContractSettings>(GameContract.DealSettings, deals);
                    List<DeliveryContractEntry> entries = Get<List<DeliveryContractEntry>>(GameContract.DealEntries, deals);
                    sig = sig * 31 + (entries != null ? entries.Count : 0) + Hash(Words(Get<Component>(GameContract.DealChosen, deals)));
                    if (set != null && set.gameObject.activeSelf)
                    {
                        sig = sig * 31 + Hash(Words(set.totalCostLabel)) + Hash(Words(set.lockTimeLabel)) + (set.repeatingOrderToggle != null && set.repeatingOrderToggle.isOn ? 1 : 0)
                              + (set.orderButton != null && set.orderButton.gameObject.activeSelf ? 2 : 0);
                    }
                    return sig;
                }
                case BizManRules.Marketing:
                {
                    Transform agency = Get<Transform>(GameContract.AdsAgency, b.bizManMarketing);
                    sig = sig * 31 + EntryCount(agency) + Hash(Words(Get<Component>(GameContract.AdsDaily, b.bizManMarketing)));
                    return sig;
                }
                case BizManRules.Settings:
                {
                    TMP_InputField name = Get<TMP_InputField>(GameContract.SetName, b.bizManSettings);
                    Button save = Get<Button>(GameContract.SetSave, b.bizManSettings);
                    LogoCustomizer logo = Get<LogoCustomizer>(GameContract.SetLogo, b.bizManSettings);
                    Button saveLogo = Get<Button>(GameContract.LogoSave, logo);
                    UI.Elements.Dropdown kind = Get<UI.Elements.Dropdown>(GameContract.SetKind, b.bizManSettings), font = Get<UI.Elements.Dropdown>(GameContract.LogoFont, logo);
                    return sig * 31 + Hash(name != null ? name.text : null) + (save != null && save.interactable ? 1 : 0) + (saveLogo != null && saveLogo.interactable ? 2 : 0)
                           + (kind != null ? kind.SelectedOptionIndex * 7 : 0) + (font != null ? font.SelectedOptionIndex * 131 : 0) + LogoSignature(logo) * 1009;
                }
                case BizManRules.RealEstate:
                {
                    RealEstateSettings est = PageOf<RealEstateSettings>(b, BizManRules.RealEstate);
                    Slider slider = Get<Slider>(GameContract.EstateSlider, est);
                    Button apply = Get<Button>(GameContract.EstateApply, est);
                    GameObject cancel = Get<GameObject>(GameContract.EstateCancelPanel, est);
                    TMP_InputField price = Get<TMP_InputField>(GameContract.EstatePrice, est);
                    return sig * 31 + (slider != null ? slider.value.GetHashCode() : 0) + (apply != null && apply.interactable ? 1 : 0) + (cancel != null && cancel.activeSelf ? 2 : 0)
                           + Hash(price != null ? price.text : null);
                }
                default:
                    return sig;
            }
        }

        private long PresentationSignature(BizManBusiness b)
        {
            BizManPresentation pres = PageOf<BizManPresentation>(b, BizManRules.Presentation);
            if (pres == null)
            {
                return 0;
            }
            StartBusinessUI start = Get<StartBusinessUI>(GameContract.ShowStart, pres);
            RentBuildingUI rent = Get<RentBuildingUI>(GameContract.ShowRent, pres);
            HamptonsPurchaseBoxUI villa = Get<HamptonsPurchaseBoxUI>(GameContract.ShowVilla, pres);
            Image photo = Get<Image>(GameContract.ShowPhoto, pres);
            long sig = (start != null && start.gameObject.activeSelf ? 1 : 0) + (rent != null && rent.gameObject.activeSelf ? 2 : 0) + (villa != null && villa.gameObject.activeSelf ? 4 : 0)
                       + (pres.businessSideAiOwned != null && pres.businessSideAiOwned.gameObject.activeSelf ? 8 : 0)
                       + (photo != null && photo.sprite != null ? photo.sprite.GetInstanceID() * 16L : 0);
            sig = sig * 31 + Hash(pres.offerAmountInputField != null ? pres.offerAmountInputField.text : null)
                  + Hash(pres.buyBuildingAmountInputField != null ? pres.buyBuildingAmountInputField.text : null) * 3;
            if (start != null && start.gameObject.activeSelf)
            {
                Transform kinds = Get<Transform>(GameContract.StartKinds, start);
                TMP_InputField name = Get<TMP_InputField>(GameContract.StartName, start);
                sig = sig * 31 + (kinds != null ? kinds.childCount : 0) + Hash(name != null ? name.text : null) + Hash(_kindQuery) * 7;
                for (int i = 0; kinds != null && i < kinds.childCount; i++)
                {
                    GameObject chosen = Get<GameObject>(GameContract.KindChosen, kinds.GetChild(i).GetComponent<StartBusinessTypeUI>());
                    sig = sig * 3 + (chosen != null && chosen.activeSelf ? 1 : 0);
                }
            }
            if (villa != null && villa.gameObject.activeSelf && villa.offerInputField != null)
            {
                sig = sig * 31 + Hash(villa.offerInputField.text);
            }
            return sig;
        }

        // ------------------------------------------------------------------------------------------------- shared

        // The part's fields emptied, so a part never shows what another left.
        private void ClearPart()
        {
            _m.PartTitle = _m.Lead = _m.FactsTitle = _m.MoreTitle = _m.LogoLabel = _m.GoodsTitle = null;
            _m.Figures = new UiFigureModel[0];
            _m.Lines = _m.SecondLines = new BizManLine[0];
            _m.Pages = new string[0];
            _m.Picture = null;
            _m.Facts = _m.More = _m.BuildingFacts = new BizManFact[0];
            _m.Actions = new UiActionModel[0];
            _m.Amount = _m.Second = null;
            _m.Chart = null;
            _m.Periods = new string[0];
            _m.Grades = _m.SecondGrades = new BizManGrade[0];
            _m.GradesTitle = _m.SecondTitle = _m.SecondEmpty = _m.CapacityTitle = _m.CapacityLine = null;
            _m.Capacity = new BizManCapacity[0];
            _m.Figures = new UiFigureModel[0];
            _m.ShareLabels = _m.ShareValues = new string[0];
            _m.Shares = new float[0];
            _m.Items = _m.Others = new BizManItem[0];
            _m.Toggles = new BizManToggle[0];
            _m.HasRepeat = false;
            _m.Kinds = _m.Fonts = new string[0];
            _m.KindOpen = _m.FontOpen = false;
            _m.Uniforms = _m.LogoTitle = _m.NameLabel = _m.NameText = _m.NamePlaceholder = null;
            _m.LogoEdit = false;
            _m.ShapesTitle = _m.ShapesNone = _m.ShapesOwn = _m.LogoNote = _m.PreviewPic = null;
            _m.ShapeCount = 0;
            _m.ShapeChosen = -1;
            _m.Colours = new BizManColours[0];
            _m.SliderFigure = null;
            ClearOps();
        }

        private static UiActionModel Do(int what, string label, UiButtonKind kind, UiIcon icon, UiAction key, bool disabled = false) =>
            new UiActionModel { Label = label, Kind = kind, Icon = icon, Key = key, Command = BizManPages.Action, Arg = what, Disabled = disabled };

        // An action offered as the game offers its button: only while the button shows, held while the game holds it (Going
        // Public's locks), in the button's own words.
        private static void Offer(List<UiActionModel> into, Button b, int what, UiButtonKind kind, UiIcon icon, UiAction key, string fallbackKey)
        {
            if (b == null || !b.gameObject.activeInHierarchy)
            {
                return;
            }
            into.Add(Do(what, UiWidgets.Label(b) ?? UiText.T(fallbackKey), kind, icon, key, !b.interactable));
        }

        private static BizManFact Fact(string labelKey, string value, bool link = false) => new BizManFact { Label = UiText.T(labelKey), Value = value, Link = link };

        private static string TextOf(TMP_Text t) => t != null && t.gameObject.activeInHierarchy ? UiWidgets.Text(t) : null;

        private static bool On(GameObject go) => go != null && go.activeSelf;

        private static bool On(Component c) => c != null && c.gameObject.activeSelf;
    }
}
