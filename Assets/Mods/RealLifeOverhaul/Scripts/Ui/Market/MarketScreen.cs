using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BigAmbitions.Items;
using BigAmbitions.Neighborhoods;
using EnhancedUI.EnhancedScroller;
using Entities;
using Extensions;
using Helpers;
using Localizor;
using Localizor.LanguageChangeEvent;
using Streets;
using TMPro;
using UI.Smartphone.Apps.MarketInsider;
using UI.Smartphone.Apps.Shared;
using UnityEngine;
using UnityEngine.UI;
using GameMarketEvent = Entities.MarketEvent;
using GameMarketInsider = UI.Smartphone.Apps.MarketInsider.MarketInsider;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Market Insider as a screen of the kit, mirrored from the game's own app (a demand view for a business owner): the
    /// districts as the game's own tabs list them (the mod's among them), the chosen one's people by income from the game's
    /// district data, its buildings' figures in the game's own words, its demand or real estate as the game's tables hold,
    /// filter and sort them, and its events as the game listed them. Every press goes to the game: a district is the game's
    /// tab pressed, a table its own tab button, a sort its header, a filter its toggles set and its table refreshed by its
    /// own method, the for sale switch its toggle, View on map its button, a building its row's own button in the game's
    /// (unseen) table, so the game's events, map and BizMan run as a click runs them. Cost: a look at a few counts four
    /// times a second while it shows (nothing allocated); a layout only when they changed.
    /// </summary>
    public sealed class MarketScreen : IUiScreen, IUiTyping
    {
        private const float Every = 0.25f;   // seconds between looks at the game's app
        private const string Row3 = "Layout 25-25-50/Row 3/";   // only for the search field (typing), no press
        private const string DemandTab = "MarketDemands", RealEstateTab = "RealEstate";   // the names the game's MarketInsider.ChangeTab takes
        private static readonly string[] DemandHeads = { "ProductName", "Demand", "LowestMarketPrice", "ImportPriceIndex", "Providers" };   // the game's headers, in the kit's columns' order
        private static readonly string[] EstateHeads = { "Address", "BuildingType", "TotalSize", "EstimatedValue", "Price" };
        private static readonly string[] EstateFigures =
        {
            "TotalBuildingsLabel/TotalBuildings", "AverageRetailPriceLabel/AverageRetailPrice", "AverageOfficePriceLabel/AverageOfficePrice",
            "AverageWarehousePriceLabel/AverageWarehousePrice", "AverageResidentialPriceLabel/AverageResidentialPrice"
        };

        private readonly GameMarketInsider _app;
        private readonly MarketView _v = new MarketView();
        private readonly UiMarketModel _m = new UiMarketModel();
        private readonly List<Button> _districtButtons = new List<Button>(12);
        private readonly List<string> _districtKeys = new List<string>(12);
        private readonly List<Component> _toggles = new List<Component>(48);   // the shown table's filter toggles, the game's own
        private readonly List<string> _toggleKeys = new List<string>(48);
        private int _togglesOf = -1;
        private bool _dirty = true, _events;   // _events: the kit's own tab of the district's events (the game lists them beside its tables)
        private float _next;
        private long _seen;
        private int _open = -1, _selected, _reveal = -1, _event = -1, _eventReveal = -1;
        private float _offset, _optionOffset, _eventOffset;

        public MarketScreen(GameMarketInsider app)
        {
            _app = app;
        }

        public string Id => MarketKit.Id;

        public bool Dirty
        {
            get
            {
                if (Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + Every;
                    long now = Signature();
                    _dirty |= now != _seen;
                    _seen = now;
                }
                return _dirty;
            }
        }

        public int TypingCommand => UiDataTable.First + UiDataTable.Search;

        public string TypingText => Search() != null ? Search().text : string.Empty;

        public void Typed(string text)
        {
            TMP_InputField field = Search();
            if (field == null)
            {
                return;
            }
            field.SetTextWithoutNotify(text ?? string.Empty);
            Refresh();   // the game's filter reads its own field: its table filters itself again, as typing there does
            _offset = 0f;
            _dirty = true;
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            Build();
            MarketViews.Market(_m, UiText.Words, _v);
            UiMarket.Lay(s, _m, frame, dark, keys);
            _offset = _m.Table.Offset;
            _eventOffset = _m.Events.Offset;
            _reveal = -1;
            _eventReveal = -1;
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            _dirty = true;
            switch (command)
            {
                case UiAppWindow.NavCommand:
                    return District(arg);
                case UiMarket.MapCommand:
                    return ViewOnMap();
                case UiMarket.TableTabCommand:
                    return ShowTable(arg);
                case UiMarket.SwitchCommand:
                {
                    var toggle = GameContract.MarketForSale.Field?.GetValue(_app.realEstateScrollerController) as Toggle;
                    _offset = 0f;
                    return toggle != null && Done(UiWidgets.Set(toggle, !toggle.isOn), "show only the buildings for sale: handed to the game");
                }
                case UiMarket.EventCommand:
                    return false;   // the events are read only, as the game's list is
                case UiMarket.OptionCommand:
                    return Option(arg);
                case UiDataTable.First + UiDataTable.Row:
                    return Row(arg);
                case UiDataTable.First + UiDataTable.Head:
                    return Sort(arg);
                case UiDataTable.First + UiDataTable.Filter:
                    _open = _open == arg ? -1 : arg;
                    _optionOffset = 0f;
                    return true;
                case UiDataTable.First + UiDataTable.Clear:
                    return Clear();
                default:
                    return false;
            }
        }

        public void Focused(int command, int arg)
        {
            if (command == UiDataTable.First + UiDataTable.Row && arg != _selected)
            {
                _selected = arg;   // the cursor chooses its row, as a management game's tables follow it
                _dirty = true;
            }
            else if (command == UiMarket.EventCommand && arg != _event)
            {
                _event = arg;
                _dirty = true;
            }
        }

        public bool Back()
        {
            if (_open >= 0)
            {
                _open = -1;   // an open list closes first, as the game's panels do
                _dirty = true;
                return true;
            }
            return false;
        }

        public void Tab(int step)
        {
            if (_open >= 0 || _districtKeys.Count == 0)
            {
                return;
            }
            _dirty = true;
            int at = Math.Max(0, _districtKeys.IndexOf(Chosen()));
            District((at + step + _districtKeys.Count) % _districtKeys.Count);
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
            if (_open >= 0)
            {
                _optionOffset = Math.Max(0f, (index - 7) * UiTokens.ControlSmall);
            }
            else if (_m.Events.RowCount > 0 && list == _m.Events.List)
            {
                _eventReveal = index;
            }
            else
            {
                _reveal = index;
            }
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            if (_open >= 0)
            {
                _optionOffset = Math.Max(0f, _optionOffset - notches * UiTokens.ControlSmall);
            }
            else if (_m.Events.RowCount > 0 && clip == _m.Events.Clip)
            {
                _eventOffset = Math.Max(0f, _eventOffset - notches * UiTokens.S16);
            }
            else
            {
                _offset = Math.Max(0f, _offset - notches * UiTokens.S16);
            }
            _dirty = true;
        }

        // ------------------------------------------------------------------------------------------------- presses

        // A district through the game's own tab: the game loads its figures, tables and events a frame later.
        private bool District(int index)
        {
            if (index < 0 || index >= _districtButtons.Count)
            {
                return false;
            }
            _open = -1;
            _selected = 0;
            _offset = 0f;
            _event = -1;
            _eventOffset = 0f;
            return Done(UiWidgets.Press(_districtButtons[index]), "show a district: handed to the game");
        }

        // A table through the game's own tab button (the one shown is held by the game, so it is no press); the events are
        // the kit's own tab over the game's list.
        private bool ShowTable(int tab)
        {
            _open = -1;
            if (tab == UiMarket.Events)
            {
                _events = true;
                return true;
            }
            bool estate = tab == UiMarket.RealEstate;
            _events = false;
            if (estate == Estate())
            {
                return true;
            }
            _selected = 0;
            _offset = 0f;
            _app.ChangeTab(estate ? RealEstateTab : DemandTab);   // the game's own method, the one its two tab buttons call
            return Done(true, "show the district's " + (estate ? "real estate" : "demand") + ": handed to the game");
        }

        // View on map through the game's own button, read from the district's data by its field (no hierarchy path); if the
        // game has no such button the game's own Market Insider shows until the app closes, and Player.log says so.
        private bool ViewOnMap()
        {
            Button b = GameContract.MarketViewOnMap.Field?.GetValue(_app.neighborhoodData) as Button;
            if (b == null)
            {
                UiDirector.Logger?.Info("Market Insider (kit): the View on map button is MISSING (MarketInsiderNeighborhoodData.viewOnMapButton), "
                                        + "so the game's own Market Insider view shows until the app closes");
                MarketKit.Classic();
                return true;
            }
            return Done(UiWidgets.Press(b), "view the district on the map: handed to the game");
        }

        // A building opens through its row's own button in the game's (unseen) table; a demand's row is chosen.
        private bool Row(int row)
        {
            _selected = row;
            if (!Estate())
            {
                return false;   // a demand is read only, as the game's row is
            }
            RealEstateScrollerController table = _app.realEstateScrollerController;
            EnhancedScroller scroller = table != null ? table.scroller : null;
            if (scroller == null || row < 0 || row >= table.data.Count)
            {
                return false;
            }
            scroller.JumpToDataIndex(row);
            var cell = scroller.GetCellViewAtDataIndex(row) as RealEstateCellView;
            var b = cell != null ? GameContract.MarketOpenBuilding.Field?.GetValue(cell) as Button : null;
            return Done(UiWidgets.Press(b), "open a building in BizMan: handed to the game");
        }

        // A sort through the table's own header: it orders its rows and shows its arrow, which the kit reads back.
        private bool Sort(int column)
        {
            string[] heads = Estate() ? EstateHeads : DemandHeads;
            if (column < 0 || column >= heads.Length || heads[column] == null)
            {
                return false;
            }
            if (Estate())
            {
                _app.realEstateScrollerController.ChangeSortOrder(heads[column]);
            }
            else
            {
                _app.marketDemandScrollerController.ChangeSortOrder(heads[column]);
            }
            return true;
        }

        // An option of the open filter: every type (all toggles off) or one (its toggle on, the rest off), then the game's
        // table filters itself again.
        private bool Option(int option)
        {
            int list = _open;
            _open = -1;
            if (list != 0 || option < 0 || option > _toggles.Count)
            {
                return false;
            }
            for (int k = 0; k < _toggles.Count; k++)
            {
                SetOn(_toggles[k], k == option - 1);
            }
            Refresh();
            _offset = 0f;
            return true;
        }

        // Filters and search cleared through the game's own Clear, which filters the table again.
        private bool Clear()
        {
            if (Estate())
            {
                (GameContract.MarketEstateFilters.Field?.GetValue(_app.realEstateScrollerController) as RealEstateFilterController)?.OnClearFilters();
            }
            else
            {
                (GameContract.MarketDemandFilters.Field?.GetValue(_app.marketDemandScrollerController) as MarketDemandFilterController)?.OnClearFilters();
            }
            _offset = 0f;
            return true;
        }

        // The shown table filtered and sorted again by its own method (its panel stays shut, so its own listeners are off).
        private void Refresh()
        {
            if (Estate())
            {
                GameContract.MarketEstateRefresh.Method?.Invoke(_app.realEstateScrollerController, null);
            }
            else
            {
                GameContract.MarketDemandRefresh.Method?.Invoke(_app.marketDemandScrollerController, null);
            }
        }

        // ------------------------------------------------------------------------------------------------- the game's state

        private bool Estate() => _app.realEstateScrollerController != null && _app.realEstateScrollerController.gameObject.activeSelf;

        private string Chosen() => GameContract.MarketDistrict.Field?.GetValue(_app.neighborhoodData) as string;

        private TMP_InputField Search()
        {
            Transform panel = _app.transform.Find(Row3 + (Estate() ? "RealEstateFilterPanel" : "MarketDemandFilterPanel"));
            return panel != null ? panel.GetComponentInChildren<TMP_InputField>(true) : null;
        }

        // What would change the screen: the district shown, the table shown with its rows, filter, search and sort, the for
        // sale switch, the events, and the kit's own row, list and scrolls.
        private long Signature()
        {
            long sig = _selected * 31L + _open + (_events ? 7 : 0);
            sig = sig * 31 + _event;
            string chosen = Chosen();
            sig = sig * 31 + (chosen != null ? chosen.GetHashCode() : 0);
            bool estate = Estate();
            sig = sig * 31 + (estate ? 1 : 2);
            sig = sig * 31 + (estate ? (_app.realEstateScrollerController.data?.Count ?? -1) : (_app.marketDemandScrollerController.data?.Count ?? -1));
            Transform table = estate ? _app.realEstateScrollerController.transform : _app.marketDemandScrollerController.transform;
            sig = sig * 31 + RivalsScreen.SortOf(table, estate ? EstateHeads : DemandHeads, out bool down) * 2 + (down ? 1 : 0);
            var toggle = GameContract.MarketForSale.Field?.GetValue(_app.realEstateScrollerController) as Toggle;
            sig = sig * 31 + (toggle != null && toggle.isOn ? 1 : 0);
            var events = GameContract.MarketEvents.Field?.GetValue(_app.marketEventScrollerController) as IList;
            sig = sig * 31 + (events != null ? events.Count : -1);
            TMP_InputField search = Search();
            sig = sig * 31 + (search != null ? search.text.Length : -1);
            return sig;
        }

        private void Build()
        {
            Districts();
            NeighborhoodData data = !string.IsNullOrEmpty(Chosen()) ? NeighborhoodHelper.GetData(Chosen()) : null;
            _v.Working = data != null ? data.workingClassPercentage : 0;
            _v.Middle = data != null ? data.middleClassPercentage : 0;
            _v.Upper = data != null ? data.upperClassClassPercentage : 0;
            Figures();
            bool estate = Estate();
            _v.Table = _events ? UiMarket.Events : estate ? UiMarket.RealEstate : UiMarket.Demands;
            if (estate)
            {
                Places();
            }
            else
            {
                Demands();
            }
            var toggle = GameContract.MarketForSale.Field?.GetValue(_app.realEstateScrollerController) as Toggle;
            _v.ForSaleOnly = toggle != null && toggle.isOn;
            _v.SortColumn = RivalsScreen.SortOf(estate ? _app.realEstateScrollerController.transform : _app.marketDemandScrollerController.transform,
                estate ? EstateHeads : DemandHeads, out bool down) - 1;
            _v.SortDown = down;
            Filters(estate);
            Events();
            _v.Selected = _selected;
            _v.Offset = _offset;
            _v.Reveal = _reveal;
            _v.EventSelected = _event;
            _v.EventOffset = _eventOffset;
            _v.EventReveal = _eventReveal;
            _v.Money = f => f.ToShortCurrencyFormat();
        }

        // The districts as the game's own tabs list them (built from its districts as the app woke: the mod's among them),
        // each by the key the game named its tab with, the one the game shows chosen.
        private void Districts()
        {
            _districtButtons.Clear();
            _districtKeys.Clear();
            var list = new List<MarketDistrict>(12);
            Transform template = _app.tabTemplate;
            Transform tabs = template != null ? template.parent : null;
            for (int i = 0; tabs != null && i < tabs.childCount; i++)
            {
                Transform tab = tabs.GetChild(i);
                Button b = tab != template && tab.gameObject.activeSelf ? tab.GetComponent<Button>() : null;
                if (b == null)
                {
                    continue;
                }
                TMP_Text label = tab.GetComponent<TMP_Text>();
                string name = label != null ? UiWidgets.Text(label) : null;
                list.Add(new MarketDistrict { Key = tab.name, Name = string.IsNullOrEmpty(name) ? UiText.T(tab.name) : name });
                _districtButtons.Add(b);
                _districtKeys.Add(tab.name);
            }
            _v.Districts = list.ToArray();
            _v.District = Math.Max(0, _districtKeys.IndexOf(Chosen()));
        }

        // The buildings' figures as the game's district box wrote them: each label in its words (its lower-case part as the
        // game draws it) and its value.
        private void Figures()
        {
            Transform box = _app.transform.Find("Layout 25-25-50/Row 1/WhiteBox/RealEstateData");
            var labels = new List<string>(EstateFigures.Length);
            var values = new List<string>(EstateFigures.Length);
            for (int i = 0; box != null && i < EstateFigures.Length; i++)
            {
                Transform value = box.Find(EstateFigures[i]);
                TMP_Text label = value != null && value.parent != null ? value.parent.GetComponent<TMP_Text>() : null;
                TMP_Text figure = value != null ? value.GetComponent<TMP_Text>() : null;
                if (label == null || figure == null || !value.parent.gameObject.activeSelf)
                {
                    continue;
                }
                labels.Add(MarketViews.Plain(label.text));
                string words = UiWidgets.Text(figure);
                values.Add(words == "-" ? UiText.T("reallife_ui_market_no_seller") : words);
            }
            _v.EstateLabels = labels.ToArray();
            _v.EstateValues = values.ToArray();
        }

        // The district's demand as the game's table holds, filters and sorts it, each product as its row writes it.
        private void Demands()
        {
            List<MarketDemandCellView.DemandModel> data = _app.marketDemandScrollerController.data;
            int n = data != null ? data.Count : 0;
            _v.Demands = new MarketDemand[n];
            for (int i = 0; i < n; i++)
            {
                MarketDemandCellView.DemandModel d = data[i];
                if (d == null)
                {
                    continue;
                }
                Item item = ItemsGetter.GetByName(d.ItemName);
                _v.Demands[i] = new MarketDemand
                {
                    Product = d.ProductName, Demand = d.Demand, Lowest = d.LowestMarketPrice < 0f ? null : d.LowestMarketPrice.ToCurrencyFormat(),
                    Import = d.ImportPriceIndex, Service = item != null && (item.type & ItemType.ServiceProduct) != 0,
                    Providers = d.Providers
                };
            }
            var all = GameContract.MarketDemandAll.Field?.GetValue(_app.marketDemandScrollerController) as IList;
            _v.All = all != null ? all.Count : n;
            _v.Places = new RivalBuilding[0];
        }

        // The district's real estate as the game's table holds, filters and sorts it.
        private void Places()
        {
            List<RealEstateCellView.RealEstateModel> data = _app.realEstateScrollerController.data;
            int n = data != null ? data.Count : 0;
            _v.Places = new RivalBuilding[n];
            for (int i = 0; i < n; i++)
            {
                _v.Places[i] = Building(data[i]);
            }
            var all = GameContract.MarketEstateAll.Field?.GetValue(_app.realEstateScrollerController) as IList;
            _v.All = all != null ? all.Count : n;
            _v.Demands = new MarketDemand[0];
        }

        /// <summary>
        /// A building as the game's real estate tables hold it, in the words its rows write; its district through the mod's
        /// one safe reading of a registration (Core/GameRegistrations), as a building the city lacks would throw.
        /// </summary>
        internal static RivalBuilding Building(RealEstateCellView.RealEstateModel b)
        {
            if (b == null)
            {
                return default;
            }
            string district = GameRegistrations.District(BuildingHelper.GetBuildingRegistration(b.Address));
            return new RivalBuilding
            {
                Address = b.FormattedAddress, District = string.IsNullOrEmpty(district) ? null : UiText.T(district), TypeKey = b.BuildingType,
                Type = UiText.T(b.BuildingType), Size = b.TotalSize.ToFormattedArea(), Value = b.EstimatedValue, Price = b.Price
            };
        }

        // The shown table's filter: its value in words (every type, the one chosen, or how many) and the open list's options,
        // from the game's own toggles (read once a table, switched off or on); the search's words.
        private void Filters(bool estate)
        {
            Toggles(estate);
            int on = 0, last = -1;
            for (int k = 0; k < _toggles.Count; k++)
            {
                if (IsOn(_toggles[k]))
                {
                    on++;
                    last = k;
                }
            }
            string all = UiText.T(estate ? "reallife_ui_market_all_buildings" : "reallife_ui_market_all_types");
            _v.Filter = on == 0 ? all : on == 1 ? UiText.T(_toggleKeys[last] ?? string.Empty)
                : StaffViews.Fill(UiText.T("reallife_ui_staff_chosen"), "count", on.ToString(CultureInfo.InvariantCulture));
            TMP_InputField search = Search();
            _v.Query = search != null ? search.text : null;
            _v.Filtered = on > 0 || !string.IsNullOrEmpty(_v.Query);
            _v.OpenList = _open;
            _v.OptionOffset = _optionOffset;
            if (_open < 0)
            {
                _v.Options = new string[0];
                _v.OptionChosen = -1;
                return;
            }
            var options = new string[_toggles.Count + 1];
            options[0] = all;
            for (int k = 0; k < _toggles.Count; k++)
            {
                options[k + 1] = UiText.T(_toggleKeys[k] ?? string.Empty);
            }
            _v.Options = options;
            _v.OptionChosen = on == 0 ? 0 : on == 1 ? last + 1 : -1;
        }

        private void Toggles(bool estate)
        {
            int of = estate ? 1 : 0;
            if (_togglesOf == of && _toggles.Count > 0 && _toggles[0] != null)
            {
                return;
            }
            _toggles.Clear();
            _toggleKeys.Clear();
            _togglesOf = of;
            Transform panel = _app.transform.Find(Row3 + (estate ? "RealEstateFilterPanel" : "MarketDemandFilterPanel"));
            if (panel == null)
            {
                return;
            }
            // The panel stays shut until the player opens it, so its toggles are read switched off or on; the real estate's
            // own for sale toggle is left to the switch, so a filter is by building type alone.
            if (estate)
            {
                foreach (RealEstateFilterToggle t in panel.GetComponentsInChildren<RealEstateFilterToggle>(true))
                {
                    if (t.Group == FilterToggleGroup.BuildingType)
                    {
                        Add(t);
                    }
                }
            }
            else
            {
                foreach (MarketDemandFilterToggle t in panel.GetComponentsInChildren<MarketDemandFilterToggle>(true))
                {
                    Add(t);
                }
            }
        }

        private void Add(Component t)
        {
            var label = t.GetComponentInChildren<TextLocalizationComponent>(true);
            _toggles.Add(t);
            _toggleKeys.Add(label != null ? label.Key : null);
        }

        private static bool IsOn(Component t) => t is MarketDemandFilterToggle d ? d.IsOn : t is RealEstateFilterToggle r && r.IsOn;

        private static void SetOn(Component t, bool on)
        {
            if (t is MarketDemandFilterToggle d)
            {
                d.SetToggleWithoutNotify(on);
            }
            else if (t is RealEstateFilterToggle r)
            {
                r.SetToggleWithoutNotify(on);
            }
        }

        // The district's events as the game's list holds them (newest first), each worded as the game's own row words it.
        private void Events()
        {
            var events = GameContract.MarketEvents.Field?.GetValue(_app.marketEventScrollerController) as List<MarketEventCellView.EventModel>;
            int n = events != null ? events.Count : 0;
            var rows = new List<MarketEvent>(n);
            for (int i = 0; i < n; i++)
            {
                MarketEventCellView.EventModel e = events[i];
                if (e == null || e.MarketEvent == null)
                {
                    continue;
                }
                string key = e.EventType.GetLocalizeKey();
                rows.Add(new MarketEvent { Kind = e.EventType.ToString(), Title = UiText.T(key), Day = e.Day, Body = MarketViews.Plain(Sentence(e, key)) });
            }
            _v.Events = rows.ToArray();
        }

        // An event's sentence with the arguments the game's own row gives it.
        private static string Sentence(MarketEventCellView.EventModel data, string key)
        {
            GameMarketEvent e = data.MarketEvent;
            string neighbourhood = string.IsNullOrEmpty(e.neighbourhood) ? string.Empty : e.neighbourhood.GetLocalization();
            return (key + "_description").Localize(new
            {
                rivalName = e.rivalName,
                businessName = e.businessName,
                address = e.address?.ToFormattedString(),
                neighbourhood,
                itemName = LocalizationHelper.GetItemLabel(e.itemName),
                source = e.GetBuildingRegistration()?.BusinessName ?? string.Empty,
                product = LocalizationHelper.GetItemLabel(e.itemName),
                durationInDays = e.durationInDays
            }).ToString();
        }

        // One line a press in Player.log, so a report shows each button reaching the game.
        private static bool Done(bool done, string what)
        {
            UiDirector.Logger?.Info("Market Insider (kit): " + what + (done ? string.Empty : " was not shown by the game, so nothing was pressed"));
            return done;
        }
    }
}
