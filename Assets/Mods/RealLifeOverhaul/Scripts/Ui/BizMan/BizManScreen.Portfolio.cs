using System;
using System.Collections.Generic;
using Extensions;
using Helpers;
using Streets;
using TMPro;
using UI.Smartphone.Apps.BizMan;
using UI.Smartphone.Apps.Shared;
using static UI.Smartphone.Apps.Shared.FilterToggleGroup;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The portfolio: the game's own list mirrored. The businesses come from its business table's rows in the order and
    /// filter the game applied (Going Public's shared shops among them, marked so), the homes from its residences' table,
    /// the warehouses and headquarters from the entries the game made for them. The filters are the game's own toggles,
    /// set as a press on them would set them; a column's head presses the game's head; a row opens the place as the game's
    /// row does. Cost: a layout a change; the toggles found once a city.
    /// </summary>
    public sealed partial class BizManScreen
    {
        private const int Shops = 0, Homes = 1, Stores = 2, Bosses = 3;   // the portfolio's kinds of place, in the sidebar's order
        // The game's filter groups in the filter row's order: district, kind of business, kind of building, status.
        private static readonly FilterToggleGroup[] FilterGroups = { Neighborhood, FilterToggleGroup.BusinessType, FilterToggleGroup.BuildingType, FilterToggleGroup.Other };
        private static readonly string[] AnyKeys = { "reallife_ui_bizman_any_district", "reallife_ui_bizman_any_kind", "reallife_ui_bizman_any_building", "reallife_ui_bizman_any_status" };

        private readonly List<int> _kinds = new List<int>(4);
        private readonly List<BizManFilterToggle> _toggles = new List<BizManFilterToggle>(64);
        private readonly List<BizManFilterToggle> _options = new List<BizManFilterToggle>(32);
        private readonly List<Transform> _entries = new List<Transform>(16);
        private int _kind;
        private int _filterOpen = -1;

        private BusinessScrollerController ShopsTable => _app.list != null ? Get<BusinessScrollerController>(GameContract.BizShops, _app.list) : null;

        private long PortfolioSignature()
        {
            BusinessScrollerController shops = ShopsTable;
            long sig = _kind * 7 + _filterOpen + 1;
            if (shops != null)
            {
                sig = sig * 31 + shops.data.Count;
                sig = sig * 31 + shops.ActiveFilterCount;
                sig = sig * 31 + (shops.data.Count > 0 && shops.data[0] != null ? Hash(shops.data[0].Id ?? shops.data[0].Name) : 0);
            }
            return sig * 31 + EntryCount(Get<Transform>(GameContract.BizStoreEntry, Get<WarehouseList>(GameContract.BizStores, _app.list)))
                   + EntryCount(Get<Transform>(GameContract.BizHeadEntry, Get<HeadquartersList>(GameContract.BizHeads, _app.list))) * 17;
        }

        private void BuildPortfolio()
        {
            _m.Page = BizManPage.Portfolio;
            _m.Head = null;
            BusinessScrollerController shops = ShopsTable;
            PrivateResidenceScrollerController homes = Get<PrivateResidenceScrollerController>(GameContract.BizHomes, _app.list);
            Transform storeEntry = Get<Transform>(GameContract.BizStoreEntry, Get<WarehouseList>(GameContract.BizStores, _app.list));
            Transform headEntry = Get<Transform>(GameContract.BizHeadEntry, Get<HeadquartersList>(GameContract.BizHeads, _app.list));
            int shopCount = shops != null ? shops.data.Count : 0, homeCount = homes != null ? homes.data.Count : 0;
            int storeCount = EntryCount(storeEntry), headCount = EntryCount(headEntry);
            _kinds.Clear();
            _kinds.Add(Shops);
            if (homeCount > 0)
            {
                _kinds.Add(Homes);
            }
            if (storeCount > 0)
            {
                _kinds.Add(Stores);
            }
            if (headCount > 0)
            {
                _kinds.Add(Bosses);
            }
            _kind = Math.Max(0, Math.Min(_kind, _kinds.Count - 1));
            var nav = new UiNavEntry[_kinds.Count];
            for (int i = 0; i < _kinds.Count; i++)
            {
                int kind = _kinds[i];
                UiIcon icon = kind == Homes ? UiIcon.Home : kind == Stores ? UiIcon.Package : kind == Bosses ? UiIcon.Building : UiIcon.Store;
                string key = kind == Homes ? "reallife_ui_bizman_homes" : kind == Stores ? "bizman_warehouses" : kind == Bosses ? "bizman_headquarters" : "common_businesses";
                int count = kind == Homes ? homeCount : kind == Stores ? storeCount : kind == Bosses ? headCount : shopCount;
                nav[i] = new UiNavEntry { Icon = icon, Tint = UiIconTints.Of(icon), Label = UiText.T(key), Count = UiText.Whole(count) };
            }
            _m.Nav = nav;
            _m.NavSelected = _kind;
            Summary(_kinds[_kind] == Shops ? shops : null);   // the line says what the businesses come to, over the businesses alone
            UiDataTableModel old = _m.Table;
            switch (_kinds[_kind])
            {
                case Homes: _m.Table = HomesTable(homes); break;
                case Stores: _m.Table = EntriesTable(storeEntry, false); break;
                case Bosses: _m.Table = EntriesTable(headEntry, true); break;
                default: _m.Table = ShopTable(shops); break;
            }
            if (old != null && old.Columns.Length == _m.Table.Columns.Length)
            {
                _m.Table.Offset = old.Offset;   // a refresh keeps the place the rows were scrolled to
                _m.Table.Reveal = old.Reveal;
            }
            _m.FilterOpen = _kinds[_kind] == Shops ? _filterOpen : -1;
            _m.FilterOptions = _m.FilterOpen >= 0 ? FilterChoices(_filterOpen, out _m.FilterChosen) : null;
        }

        // What the portfolio comes to, from the rows the game's list holds: its businesses, their takings a day and their alerts.
        // What the table's lines do not say on their own: how many are open now, how satisfied their customers are on
        // average, and which earns the most. Its income and its alerts in all are the table's own totals line.
        private void Summary(BusinessScrollerController shops)
        {
            int places = 0, open = 0, rated = 0, satisfaction = 0;
            float best = float.MinValue;
            string top = null;
            for (int i = 0; shops != null && i < shops.data.Count; i++)
            {
                BusinessCellView.BusinessModel b = shops.data[i];
                if (b == null || b.IsRealEstate || string.IsNullOrEmpty(b.BusinessName))
                {
                    continue;
                }
                places++;
                open += Tone(b.OpenStatus.color) == UiRole.Ok ? 1 : 0;
                if (b.Satisfaction >= 0)
                {
                    rated++;
                    satisfaction += b.Satisfaction;
                }
                if (b.AvgDailyIncome > best)
                {
                    best = b.AvgDailyIncome;
                    top = b.BusinessName;
                }
            }
            if (places == 0)
            {
                _m.Summary = new string[0];
                _m.SummaryIcons = new UiIcon[0];
                return;
            }
            bool told = rated > 0, leads = places > 1 && top != null;
            _m.SummaryIcons = new UiIcon[1 + (told ? 1 : 0) + (leads ? 1 : 0)];
            _m.Summary = new string[_m.SummaryIcons.Length];
            _m.SummaryIcons[0] = UiIcon.Store;
            _m.Summary[0] = UiText.Fill("reallife_ui_bizman_sum_open", "open", UiText.Whole(open), "count", UiText.Whole(places));
            int k = 1;
            if (told)
            {
                _m.SummaryIcons[k] = UiIcon.SmileyHappy;
                _m.Summary[k++] = UiText.Fill("reallife_ui_bizman_sum_mood", "percent", UiText.Whole(satisfaction / rated));
            }
            if (leads)
            {
                _m.SummaryIcons[k] = UiIcon.Trophy;
                _m.Summary[k] = UiText.Fill("reallife_ui_bizman_sum_best", "name", top);
            }
        }

        private UiDataTableModel ShopTable(BusinessScrollerController shops)
        {
            var t = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = UiText.T("common_business_name"), Kind = UiCellKind.Tile },
                    new UiDataColumn { Title = UiText.T("common_neighborhood"), Kind = UiCellKind.Text, Width = DistrictWidth },
                    new UiDataColumn { Title = UiText.T("bizman_avg_daily_income"), Kind = UiCellKind.Money, Width = 152f },
                    new UiDataColumn { Title = UiText.T("bizman_insight_satisfaction"), Kind = UiCellKind.Meter, Width = 168f },
                    new UiDataColumn { Title = UiText.T("bizman_alerts"), Kind = UiCellKind.Figure, Width = 88f },
                    new UiDataColumn { Title = UiText.T("bizman_status"), Kind = UiCellKind.Chip, Width = StatusWidth },
                    Destination(),
                },
                SearchPlaceholder = UiText.T("reallife_ui_bizman_search"), EmptyIcon = UiIcon.Store,
                EmptyTitle = UiText.T("reallife_ui_bizman_none_title"), EmptyBody = UiText.T("reallife_ui_bizman_none_body")
            };
            // The game's heads by column: the set destination column at the end is the kit's own and has none.
            Heads(t, shops != null ? shops.transform.Find("Headers") : null, BizManRules.ShopHeads);
            Filters(t);
            t.Query = SearchField() is TMP_InputField f && f.text.Length > 0 ? f.text : null;
            int n = shops != null ? shops.data.Count : 0;
            t.Rows = new UiDataRow[n];
            float total = 0f;
            int alerts = 0;
            for (int i = 0; i < n; i++)
            {
                BusinessCellView.BusinessModel b = shops.data[i];
                t.Rows[i] = ShopRow(b);
                if (b != null)
                {
                    total += b.AvgDailyIncome;
                    alerts += b.IsRealEstate ? 0 : b.Alerts;
                }
            }
            t.RowCount = n;
            t.Count = UiText.Whole(n);
            if (n > 0)
            {
                t.TotalsLabel = UiText.T("common_total");
                t.Totals = new[] { null, null, total.ToShortCurrencyFormat(), null, UiText.Whole(alerts), null, null };
            }
            if (n == 0 && shops != null && shops.ActiveFilterCount > 0)
            {
                t.EmptyTitle = UiText.T("reallife_ui_bizman_filtered_title");
                t.EmptyBody = UiText.T("reallife_ui_bizman_filtered_body");
                t.EmptyAction = UiText.T("reallife_ui_bizman_clear");
            }
            return t;
        }

        // A business as the game's own row shows it: its name or address, its kind (or an empty building, or real estate), its
        // district, its takings a day, its satisfaction, its alerts and its state in the game's own words and colour.
        private static UiDataRow ShopRow(BusinessCellView.BusinessModel b)
        {
            if (b == null)
            {
                return new UiDataRow { Cells = new string[7] };
            }
            bool running = !string.IsNullOrEmpty(b.BusinessName);
            UiIcon icon = BizManRules.KindIcon(b.BusinessType, b.BuildingType);
            string sub = running ? UiText.T(b.BusinessType)
                : b.IsRealEstate ? UiText.Fill("bizman_list_real_estate_type", "buildingType", UiText.T(b.BuildingType))
                : UiText.Fill("bizman_empty_building", "buildingType", UiText.T(b.BuildingType));
            string status = b.OpenStatus.text.ToString();
            UiRole tone = Tone(b.OpenStatus.color);
            BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(b.Address);
            if (reg != null && !reg.RentedByPlayer && !reg.BuildingOwnedByPlayer)
            {
                // Another player's shop shared with this one (Going Public lists it): said in a chip, never a colour of its own.
                status = UiText.T("reallife_ui_bizman_shared");
                tone = UiRole.Accent;
            }
            return new UiDataRow
            {
                Cells = new[]
                {
                    b.Name, District(b), b.IsRealEstate || running ? b.AvgDailyIncome.ToShortCurrencyFormat() : "-", running ? UiText.Whole(b.Satisfaction) : "-",
                    running ? UiText.Whole(b.Alerts) : "-", status, UiText.T("common_setdestination")
                },
                Values = new[] { (int)icon, 0, 0, running ? b.Satisfaction : -1, -1, 0, (int)UiIcon.Locate },
                Tones = new[] { UiIconTints.Of(icon), UiRole.None, UiRole.None, UiRole.None, UiRole.None, tone, UiRole.None },
                Sub = sub
            };
        }

        private static string District(BusinessCellView.BusinessModel b)
        {
            Buildings.Building building = b.Address != null ? BuildingHelper.GetBuilding(b.Address) : null;
            return building != null && !string.IsNullOrEmpty(building.Neighbourhood) ? UiText.T(building.Neighbourhood) : null;
        }

        private UiDataTableModel HomesTable(PrivateResidenceScrollerController homes)
        {
            var t = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = UiText.T("common_address"), Kind = UiCellKind.Tile },
                    new UiDataColumn { Title = UiText.T("bizman_building_total_size"), Kind = UiCellKind.Figure, Width = 160f },
                    Destination(),
                },
                EmptyIcon = UiIcon.Home, EmptyTitle = UiText.T("reallife_ui_bizman_none_title")
            };
            int n = homes != null ? homes.data.Count : 0;
            t.Rows = new UiDataRow[n];
            for (int i = 0; i < n; i++)
            {
                PrivateResidenceCellView.PrivateResidenceModel h = homes.data[i];
                t.Rows[i] = new UiDataRow
                {
                    Cells = new[] { h != null && h.Address != null ? h.Address.ToFormattedString() : null, h != null ? h.Size.ToFormattedArea() : null, null },
                    Values = new[] { (int)UiIcon.Home, -1, (int)UiIcon.Locate }, Tones = new[] { UiIconTints.Of(UiIcon.Home), UiRole.None, UiRole.None }
                };
            }
            t.RowCount = n;
            return t;
        }

        // The warehouses' or headquarters' entries the game made in its list: each its name and address, and what the game
        // counts on it (vehicle slots and drivers; managers of each kind).
        private UiDataTableModel EntriesTable(Transform template, bool heads)
        {
            Entries(template);
            var t = new UiDataTableModel { EmptyIcon = heads ? UiIcon.Building : UiIcon.Package, EmptyTitle = UiText.T("reallife_ui_bizman_none_title") };
            if (heads)
            {
                t.Columns = new[]
                {
                    new UiDataColumn { Title = UiText.T("bizman_headquarters"), Kind = UiCellKind.Tile },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizman_tab_pricing"), Kind = UiCellKind.Figure, Width = 120f },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizman_tab_logistics"), Kind = UiCellKind.Figure, Width = 120f },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizman_tab_purchasing"), Kind = UiCellKind.Figure, Width = 120f },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizman_tab_hr"), Kind = UiCellKind.Figure, Width = 120f },
                    new UiDataColumn { Title = UiText.T("bizman_menu_headhunters"), Kind = UiCellKind.Figure, Width = 120f },
                    Destination(),
                };
            }
            else
            {
                t.Columns = new[]
                {
                    new UiDataColumn { Title = UiText.T("bizman_warehouses"), Kind = UiCellKind.Tile },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizman_runs_out"), Kind = UiCellKind.Text },
                    new UiDataColumn { Title = UiText.T("bizman_inventory_days_until_empty"), Kind = UiCellKind.Figure, Width = 176f },
                    new UiDataColumn { Title = UiText.T("bizman_vehicle_slots"), Kind = UiCellKind.Figure, Width = 136f },
                    new UiDataColumn { Title = UiText.T("bizman_drivers"), Kind = UiCellKind.Figure, Width = 112f },
                    Destination(),
                };
            }
            t.Rows = new UiDataRow[_entries.Count];
            for (int i = 0; i < _entries.Count; i++)
            {
                Transform e = _entries[i];
                UiIcon icon = heads ? UiIcon.Building : UiIcon.Package;
                if (heads)
                {
                    t.Rows[i] = new UiDataRow
                    {
                        Cells = new[]
                        {
                            WordsAt(e, "TopPanel/Name"), Count(e, "EmployeeCounter/PricingManagers/Count"), Count(e, "EmployeeCounter/LogisticsManagers/Count"),
                            Count(e, "EmployeeCounter/PurchasingAgents/Count"), Count(e, "EmployeeCounter/HRManagers/Count"), Count(e, "EmployeeCounter/Headhunters/Count"), null
                        },
                        Values = new[] { (int)icon, -1, -1, -1, -1, -1, (int)UiIcon.Locate },
                        Tones = new[] { UiIconTints.Of(icon), UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
                        Sub = WordsAt(e, "TopPanel/Address")
                    };
                    continue;
                }
                Slots(e, out int slots, out int drivers);
                FirstRunOut(e, out string good, out string days);
                t.Rows[i] = new UiDataRow
                {
                    Cells = new[] { WordsAt(e, "WarehouseName"), good, days, UiText.Whole(slots), UiText.Whole(drivers), null },
                    Values = new[] { (int)icon, 0, -1, -1, -1, (int)UiIcon.Locate },
                    Tones = new[] { UiIconTints.Of(icon), UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
                    Sub = WordsAt(e, "Address")
                };
            }
            t.RowCount = _entries.Count;
            return t;
        }

        // The entries the game made from a template: its shown copies beside it.
        private void Entries(Transform template)
        {
            _entries.Clear();
            Transform parent = template != null ? template.parent : null;
            for (int i = 0; parent != null && i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c != template && c.gameObject.activeSelf)
                {
                    _entries.Add(c);
                }
            }
        }

        private static int EntryCount(Transform template)
        {
            Transform parent = template != null ? template.parent : null;
            int n = 0;
            for (int i = 0; parent != null && i < parent.childCount; i++)
            {
                n += parent.GetChild(i) != template && parent.GetChild(i).gameObject.activeSelf ? 1 : 0;
            }
            return n;
        }

        private static string Count(Transform entry, string path) => WordsAt(entry, path) ?? "0";

        // A warehouse's vehicle slots and the drivers on them, as its entry lists them: a slot's driver words in the game's red is none.
        private static void Slots(Transform entry, out int slots, out int drivers)
        {
            slots = drivers = 0;
            Transform list = entry.Find("VehicleSlotsList");
            for (int i = 0; list != null && i < list.childCount; i++)
            {
                Transform slot = list.GetChild(i);
                if (!slot.gameObject.activeSelf || slot.name == "VehicleSlotEntry")
                {
                    continue;
                }
                slots++;
                drivers += Painted(slot, "DriverStatus") != UiRole.Danger && Painted(slot, "VehicleName") != UiRole.Danger ? 1 : 0;
            }
        }

        // The tone the game paints the words at a path in; a slot's vehicle or driver in its red is missing.
        private static UiRole Painted(Transform root, string path)
        {
            Transform t = root.Find(path);
            TMP_Text words = t != null ? t.GetComponentInChildren<TMP_Text>(true) : null;
            return words != null ? Tone(words.color) : UiRole.Danger;
        }

        // The good that runs out first and when, as the warehouse's entry lists its stock.
        private static void FirstRunOut(Transform entry, out string good, out string days)
        {
            good = days = null;
            Transform list = entry.Find("InventoryList");
            for (int i = 0; list != null && i < list.childCount; i++)
            {
                Transform item = list.GetChild(i);
                if (item.gameObject.activeSelf && item.name != "InventoryEntry")
                {
                    good = WordsAt(item, "ItemName");
                    days = WordsAt(item, "DaysLeft");
                    return;
                }
            }
        }

        private const float DistrictWidth = 200f;   // px: a district's name, so a business's own name has the room left
        private const float StatusWidth = 216f;     // px: the longest status chip whole in the longest language ("Vorübergehend geschlossen")

        // A row's Set destination, as the game's own row offers it: a locate button at its right.
        private static UiDataColumn Destination() => new UiDataColumn { Title = string.Empty, Kind = UiCellKind.Action, Width = UiTokens.ControlSmall };

        private static bool Shown(Transform t) => t != null && t.gameObject.activeSelf;

        // ------------------------------------------------------------------------------------------------- filters

        private void Toggles()
        {
            if (_toggles.Count > 0 && _toggles[0] != null)
            {
                return;
            }
            _toggles.Clear();
            BizManFilterController filter = Get<BizManFilterController>(GameContract.BizFilter, ShopsTable);
            if (filter != null)
            {
                filter.GetComponentsInChildren(true, _toggles);   // the game's own toggles, made once by its filter; asked for once
            }
        }

        private void Filters(UiDataTableModel t)
        {
            Toggles();
            t.Filters = new string[FilterGroups.Length];
            for (int g = 0; g < FilterGroups.Length; g++)
            {
                int on = 0;
                string one = null;
                for (int i = 0; i < _toggles.Count; i++)
                {
                    if (_toggles[i] != null && _toggles[i].Group == FilterGroups[g] && _toggles[i].IsOn)
                    {
                        on++;
                        one = Label(_toggles[i]);
                    }
                }
                t.Filters[g] = on == 0 ? UiText.T(AnyKeys[g]) : on == 1 ? one : UiText.Fill("reallife_ui_bizman_chosen", "count", UiText.Whole(on));
            }
        }

        // A toggle's words, read with its panel closed too.
        private static string Label(Component c)
        {
            TMP_Text text = c != null ? c.GetComponentInChildren<TMP_Text>(true) : null;
            return text != null ? text.text : null;
        }

        private string[] FilterChoices(int group, out int chosen)
        {
            Toggles();
            _options.Clear();
            chosen = 0;
            for (int i = 0; i < _toggles.Count; i++)
            {
                if (_toggles[i] != null && _toggles[i].Group == FilterGroups[group])
                {
                    _options.Add(_toggles[i]);
                }
            }
            var words = new string[_options.Count + 1];
            words[0] = UiText.T(AnyKeys[group]);
            for (int i = 0; i < _options.Count; i++)
            {
                words[i + 1] = Label(_options[i]);
                chosen = _options[i].IsOn ? i + 1 : chosen;
            }
            return words;
        }

        // A filter's choice set as the game's toggles: the one chosen alone on in its group, or none for "all"; then the
        // game's list filtered again, as its toggles' own change does.
        private bool Choose(int option)
        {
            if (_filterOpen < 0)
            {
                return false;
            }
            FilterChoices(_filterOpen, out _);
            for (int i = 0; i < _options.Count; i++)
            {
                _options[i].SetToggleWithoutNotify(i + 1 == option);
            }
            _filterOpen = -1;
            BusinessScrollerController shops = ShopsTable;
            shops?.RefreshFilters();
            UiDirector.Logger?.Info("BizMan (kit): a filter of the business list set through the game's own toggles");
            return shops != null;
        }

        private TMP_InputField SearchField()
        {
            BizManFilterController filter = Get<BizManFilterController>(GameContract.BizFilter, ShopsTable);
            return filter != null ? filter.GetComponentInChildren<TMP_InputField>(true) : null;
        }

        // ------------------------------------------------------------------------------------------------- presses

        private bool PressPortfolio(int command, int arg)
        {
            BusinessScrollerController shops = ShopsTable;
            switch (command)
            {
                case BizManPages.Section:
                    _kind = Math.Max(0, Math.Min(arg, _kinds.Count - 1));
                    _filterOpen = -1;
                    return true;
                case UiDataTable.First + UiDataTable.Filter:
                    _filterOpen = _filterOpen == arg ? -1 : arg;
                    return true;
                case BizManPages.FilterOption:
                    return Choose(arg);
                case UiDataTable.First + UiDataTable.Head:
                {
                    Transform heads = shops != null ? shops.transform.Find("Headers") : null;
                    string name = BizManRules.HeadOf(BizManRules.ShopHeads, arg);
                    Transform head = heads != null && name != null ? heads.Find(name) : null;
                    return head != null && Click(head.GetComponent<Button>(), "sort the business list by " + name);
                }
                case UiDataTable.First + UiDataTable.Clear:
                    shops?.ClearFilters();
                    return shops != null;
                case UiDataTable.First + UiDataTable.Row:
                    return OpenRow(arg);
                case UiDataTable.First + UiDataTable.Act:
                    return DestinationOf(arg);
                default:
                    return false;
            }
        }

        // A row's Set destination, as the game's own row's button sets it: the game's notice, its guide, the menu closed.
        private bool DestinationOf(int row)
        {
            int kind = _kinds.Count > 0 ? _kinds[_kind] : Shops;
            switch (kind)
            {
                case Homes:
                {
                    PrivateResidenceScrollerController homes = Get<PrivateResidenceScrollerController>(GameContract.BizHomes, _app.list);
                    if (homes == null || row < 0 || row >= homes.data.Count || homes.data[row] == null || _app.business == null)
                    {
                        return false;
                    }
                    _app.business.SetDestination(homes.data[row].Address);
                    return true;
                }
                case Stores:
                case Bosses:
                {
                    Transform button = row >= 0 && row < _entries.Count ? _entries[row].Find(kind == Bosses ? "TopPanel/Address/SetDestinationButton" : "Address/SetDestinationButton") : null;
                    return button != null && Click(button.GetComponent<Button>(), "set the destination");
                }
                default:
                {
                    BusinessScrollerController shops = ShopsTable;
                    if (shops == null || row < 0 || row >= shops.data.Count || shops.data[row] == null || _app.business == null)
                    {
                        return false;
                    }
                    _app.business.SetDestination(shops.data[row].Address);   // what the game's row's own button calls
                    return true;
                }
            }
        }

        // A row opened as the game's own row opens it: a business or real estate on its page, a home on its presentation, a
        // warehouse or headquarters by its entry's own button. The game's view of a page the kit does not draw is not opened
        // by a row: the kit shows the place's building instead, with its other pages in the sidebar.
        private bool OpenRow(int row)
        {
            int kind = _kinds.Count > 0 ? _kinds[_kind] : Shops;
            _openedByKit = true;
            switch (kind)
            {
                case Homes:
                {
                    PrivateResidenceScrollerController homes = Get<PrivateResidenceScrollerController>(GameContract.BizHomes, _app.list);
                    if (homes == null || row < 0 || row >= homes.data.Count)
                    {
                        return false;
                    }
                    _app.list.OpenResidential(homes.data[row]);
                    return true;
                }
                case Stores:
                case Bosses:
                    return row >= 0 && row < _entries.Count && Click(_entries[row].GetComponent<Button>(), "open " + WordsAt(_entries[row], kind == Bosses ? "TopPanel/Name" : "WarehouseName"));
                default:
                {
                    BusinessScrollerController shops = ShopsTable;
                    if (shops == null || row < 0 || row >= shops.data.Count || shops.data[row] == null)
                    {
                        return false;
                    }
                    _app.list.OpenBusiness(shops.data[row]);
                    UiDirector.Logger?.Info("BizMan (kit): open " + shops.data[row].Name + ": handed to the game");
                    return true;
                }
            }
        }
    }
}
