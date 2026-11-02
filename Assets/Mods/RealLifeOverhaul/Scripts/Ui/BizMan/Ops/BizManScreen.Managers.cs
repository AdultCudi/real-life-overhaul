using System;
using System.Collections.Generic;
using Buildings.Office.Headquarters;
using Entities;
using Extensions;
using Helpers;
using Localizor.LanguageChangeEvent;
using Streets;
using TMPro;
using UI;
using UI.Components;
using UI.Smartphone.Apps.BizMan;
using UI.Smartphone.Apps.BizMan.HrManagers;
using UI.Smartphone.Apps.BizMan.HRManagers;
using UI.Smartphone.Apps.BizMan.LogisticsManagers;
using UI.Smartphone.Apps.BizMan.PricingManagers;
using UI.Smartphone.Apps.BizMan.PurchasingAgent;
using UnityEngine;
using UnityEngine.UI;
using InputField = UI.Components.InputField;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A headquarters' managers of one kind, read from the game's own plan list and plan page after the game filled them:
    /// the plans as the game's reorderable list holds them (what each covers, who runs it, the chosen one), the chosen plan
    /// as a form of the game's own widgets, and its goods, its route's stops or its staff from the game's own tables. A plan
    /// with no manager shows the game's own question for one. Every press is the game's: the list's own entries and Add
    /// plan, its reorder through the game's ReorderableList, each widget and each row's own cell. Cost: a look at a few counts
    /// four times a second; a layout only when they change.
    /// </summary>
    public sealed partial class BizManScreen
    {
        private readonly List<ReorderableListItem> _plans = new List<ReorderableListItem>(16);   // the game's entries, in its list's order
        private readonly List<LogisticsManagerDestinationUI> _stops = new List<LogisticsManagerDestinationUI>(8);
        private readonly List<Transform> _goods = new List<Transform>(32);       // the chosen stop's goods, the game's own rows
        private readonly List<Toggle> _breakers = new List<Toggle>(16);
        private ReorderableList _planOrder;
        private Button _planAdd;
        private NoManagerAssignedPopUp _noManager;
        private int _stop;                          // the stop whose goods show
        private int _good = -1;                     // the good chosen: a purchasing agent's detail, a managed price or a stop's target being stepped
        private string _goodOf;                     // the kind of manager it was chosen on

        // What each of a managers' page's actions does.
        private const int DoAddPlan = 60, DoPlanUp = 61, DoPlanDown = 62, DoDeletePlan = 63, DoSuggest = 64, DoShowFrom = 65, DoAddStop = 66, DoBuyEnd = 67,
            DoBuyCancel = 68, DoBuyUrgent = 69, DoBuyOrder = 70, DoHrAssign = 71, DoHrFill = 72, DoHrClear = 73, DoHrClose = 74;

        // ------------------------------------------------------------------------------------------------- the model

        private void Managers(BizManBusiness b)
        {
            if (Lacks(CoreOf(_shown)))
            {
                Handover(b);
                return;
            }
            _m.Part = BizManPart.Managers;
            if (!string.Equals(_goodOf, _shown, StringComparison.Ordinal))
            {
                _goodOf = _shown;   // a pricing manager's product is not a route's good: another kind starts with none chosen
                _good = -1;
            }
            RowsBegin();
            _plans.Clear();
            _planOrder = null;
            _planAdd = null;
            _noManager = null;
            BizOpsModel o = _m.Ops;
            o.FormEmpty = UiText.T("reallife_ui_bizops_plan_choose");
            o.FormEmptyBody = UiText.T("reallife_ui_bizops_plan_choose_body");
            switch (_shown)
            {
                case BizManRules.PricingManagers: Pricing(b, o); break;
                case BizManRules.LogisticsManagers: Logistics(b, o); break;
                case BizManRules.PurchasingAgents: Purchasing(b, o); break;
                case BizManRules.HrManagers: Hr(b, o); break;
                default: Headhunters(b, o); break;
            }
            PlanActions(o);
            RowsEnd();
        }

        private static GameMember[] CoreOf(string tab)
        {
            switch (tab)
            {
                case BizManRules.PricingManagers: return PricingCore;
                case BizManRules.LogisticsManagers: return RoutesCore;
                case BizManRules.PurchasingAgents: return BuyingCore;
                case BizManRules.HrManagers: return HrCore;
                default: return HuntCore;
            }
        }

        // The plans: the game's reorderable list's own entries, each read by what the game wrote into it.
        private void Plans(ReorderableList order, Button add, string title, string empty, string emptyBody, Func<ReorderableListItem, BizManItem> read)
        {
            _planOrder = order;
            _planAdd = add;
            BizOpsModel o = _m.Ops;
            o.PlansTitle = title;
            o.PlansEmpty = empty;
            o.PlansEmptyBody = emptyBody;
            for (int i = 0; order != null && i < order.transform.childCount; i++)
            {
                Transform c = order.transform.GetChild(i);
                if (c.gameObject.activeSelf && c.TryGetComponent(out ReorderableListItem item))
                {
                    _plans.Add(item);
                }
            }
            var items = new BizManItem[_plans.Count];
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = read(_plans[i]);
            }
            o.Plans = items;
        }

        // An entry as the game's HR, headhunter and purchasing lists build it: its manager's name (or Unassigned), its other
        // words, and chosen while its manager's drop-down shows in place of the name.
        private static BizManItem Entry(ReorderableListItem e, UiIcon glyph, string titleChild, string lineKey)
        {
            Transform t = e.transform;
            string manager = WordsAt(t, "ManagerName") ?? TextAt(t.Find("ManagerName"));
            Transform none = t.Find("ManagerName/UnassignedManagerIcon");
            Transform drop = t.Find("ManagerDropdown");
            bool chosen = drop != null && drop.gameObject.activeSelf;
            string title = titleChild != null ? TextAt(t.Find(titleChild)) : null;
            string info = TextAt(t.Find("Info"));
            bool unassigned = none != null && none.gameObject.activeSelf;
            if (chosen && drop.GetComponent<UI.Elements.Dropdown>() is UI.Elements.Dropdown d)
            {
                string[] options = Options(d);
                manager = d.SelectedOptionIndex >= 0 && d.SelectedOptionIndex < options.Length ? options[d.SelectedOptionIndex] : manager;
            }
            return new BizManItem
            {
                Glyph = glyph, Name = title ?? manager ?? UiText.T("common_unassigned"),
                Line = title != null ? manager : info != null && lineKey != null ? UiText.Fill(lineKey, "count", info) : null,
                WarnWords = unassigned ? UiText.T("reallife_ui_bizops_no_manager") : null, Chosen = chosen
            };
        }

        private static string TextAt(Transform t)
        {
            if (t == null || !t.gameObject.activeInHierarchy)
            {
                return null;
            }
            TMP_Text text = t.GetComponent<TMP_Text>();
            return text != null ? UiWidgets.Text(text) : null;
        }

        // The chosen entry's own manager drop-down, the form's first row; or, for a plan with no manager, the game's own
        // question: its choice of person, what is missing, and Delete plan. Returns true when the question stands.
        private bool ManagerRows(NoManagerAssignedPopUp popup, string notAssignedKey, string notAssignedBodyKey)
        {
            _noManager = popup;
            int chosen = ChosenPlan();
            if (chosen < 0)
            {
                return false;
            }
            if (popup != null && popup.gameObject.activeInHierarchy)
            {
                Heading(UiText.T(notAssignedKey));
                NoteLine(UiText.T(notAssignedBodyKey));
                ChoiceLine(popup.employeeDropdown, UiText.T("reallife_ui_bizops_manager"));
                return true;
            }
            Transform drop = _plans[chosen].transform.Find("ManagerDropdown");
            UI.Elements.Dropdown d = drop != null ? drop.GetComponent<UI.Elements.Dropdown>() : null;
            if (d == null)
            {
                d = _plans[chosen].GetComponentInChildren<UI.Elements.Dropdown>(false);   // the pricing and logistics entries hold it inside their chosen face
            }
            ChoiceLine(d, UiText.T("reallife_ui_bizops_manager"));
            return false;
        }

        private int ChosenPlan()
        {
            for (int i = 0; i < _m.Ops.Plans.Length; i++)
            {
                if (_m.Ops.Plans[i].Chosen)
                {
                    return i;
                }
            }
            return -1;
        }

        // The plans' actions: Add plan as the game's own button, and the chosen one moved up or down its list as a drag of
        // the game's own list moves it; Delete plan where the game's question offers it. Add plan is the page's main action
        // only while the chosen plan's form has none of its own: a page has one.
        private void PlanActions(BizOpsModel o)
        {
            bool formMain = false;
            for (int i = 0; i < o.Actions.Length; i++)
            {
                formMain |= o.Actions[i].Kind == UiButtonKind.Primary;
            }
            var actions = new List<UiActionModel>(4);
            int chosen = ChosenPlan();
            if (chosen >= 0 && _plans.Count > 1)
            {
                // Square arrows beside Add plan: their glyphs say what they do, so the plans' narrow foot holds one line.
                actions.Add(new UiActionModel { Kind = UiButtonKind.Quiet, Icon = UiIcon.Down, Key = UiAction.Count, Arg = DoPlanDown, Disabled = chosen >= _plans.Count - 1 });
                actions.Add(new UiActionModel { Kind = UiButtonKind.Quiet, Icon = UiIcon.Up, Key = UiAction.Count, Arg = DoPlanUp, Disabled = chosen == 0 });
            }
            if (_planAdd != null && _planAdd.gameObject.activeInHierarchy)
            {
                actions.Add(new UiActionModel { Label = UiText.T("bizman_add_plan"), Kind = formMain ? UiButtonKind.Secondary : UiButtonKind.Primary, Icon = UiIcon.Plus,
                    Key = formMain ? UiAction.Count : UiAction.Alt, Arg = DoAddPlan, Disabled = !_planAdd.interactable });
            }
            o.PlanActions = actions.ToArray();
            if (_noManager != null && _noManager.gameObject.activeInHierarchy)
            {
                var all = new List<UiActionModel>(o.Actions);
                all.Insert(0, new UiActionModel { Label = UiText.T("bizman_delete_plan"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Trash, Key = UiAction.Count, Arg = DoDeletePlan });
                o.Actions = all.ToArray();
            }
        }

        // ------------------------------------------------------------------------------------------------- pricing

        private void Pricing(BizManBusiness b, BizOpsModel o)
        {
            PricingManagersPlanList list = b.pricingManagersPlanList;
            Plans(Get<ReorderableList>(GameContract.PriceOrder, list), ButtonOf(Get<Transform>(GameContract.PriceAdd, list)?.gameObject), UiText.T("bizman_menu_pricing_managers"),
                UiText.T("reallife_ui_bizops_pricing_none"), UiText.T("reallife_ui_bizops_pricing_none_body"), e =>
                {
                    var entry = e.GetComponent<PricingManagersPlanListEntry>();
                    PricingManagerPlan plan = entry != null ? entry.Plan : null;
                    EmployeeInstance who = plan != null ? EmployeeHelper.GetEmployeeById(plan.assignedEmployeeId) : null;
                    return new BizManItem
                    {
                        Glyph = UiIcon.MapTrifold, Name = plan != null && !string.IsNullOrEmpty(plan.supervisedNeighborhood) ? UiText.T(plan.supervisedNeighborhood) : UiText.T("common_unassigned"),
                        Line = who != null ? who.GetEmployeeNameWithInfo() : UiText.T("common_unassigned"), WarnWords = who == null ? UiText.T("reallife_ui_bizops_no_manager") : null,
                        Chosen = entry != null && entry.ManagerDropdown != null && entry.ManagerDropdown.gameObject.activeInHierarchy
                    };
                });
            PricingManagerPlanUI plan2 = Get<PricingManagerPlanUI>(GameContract.PriceList, list);
            if (ChosenPlan() < 0 || plan2 == null || !plan2.gameObject.activeInHierarchy)
            {
                return;
            }
            o.FormTitle = o.Plans[ChosenPlan()].Name;
            if (ManagerRows(plan2.noManagerAssignedPopUp, "bizman_pricingmanagers_not_assigned", "bizman_pricingmanagers_not_assigned_description"))
            {
                return;
            }
            ChoiceLine(Get<UI.Elements.Dropdown>(GameContract.PriceDistrict, plan2), UiText.T("common_neighborhood"));
            o.FormLead = Loc(Get<TextLocalizationComponent>(GameContract.PriceOwned, list));
            PricingManagerProductsScrollerController goods = Get<PricingManagerProductsScrollerController>(GameContract.PriceGoods, plan2);
            var actions = new List<UiActionModel>(1);
            Button suggest = Get<Button>(GameContract.PriceSuggest, goods);
            if (suggest != null && suggest.gameObject.activeInHierarchy)
            {
                actions.Add(new UiActionModel { Label = UiText.T("bizman_pricingmanagers_apply_suggested_prices"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check,
                    Key = UiAction.Alt, Arg = DoSuggest, Disabled = !suggest.interactable });
            }
            o.Actions = actions.ToArray();
            o.Table = PriceTable(goods, o.Table);   // under its head alone: its first column says Product
        }

        private UiDataTableModel PriceTable(PricingManagerProductsScrollerController goods, UiDataTableModel old)
        {
            var t = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = UiText.T("bizman_pricingmanagers_header_product"), Kind = UiCellKind.Text, Sortable = true },
                    new UiDataColumn { Title = UiText.T("bizman_pricingmanagers_header_current_price"), Kind = UiCellKind.Money, Width = 128f, Sortable = true },
                    new UiDataColumn { Title = UiText.T("bizman_pricingmanagers_header_suggested_price"), Kind = UiCellKind.Money, Width = 176f, Sortable = true },
                    new UiDataColumn { Title = UiText.T("bizman_pricingmanagers_header_rivals_prices"), Kind = UiCellKind.Money, Width = 128f, Sortable = true },
                    new UiDataColumn { Title = UiText.T("bizman_pricingmanagers_header_new_price"), Kind = UiCellKind.Stepper, Width = UiDataTable.StepperWidth, OnChosen = true },
                },
                EmptyIcon = UiIcon.Tag, EmptyTitle = UiText.T("bizman_pricingmanagers_no_products"), Offset = old != null ? old.Offset : 0f, Reveal = old != null ? old.Reveal : -1,
                SortColumn = _sortColumn, SortDown = goods != null && !goods.orderByAsc
            };
            int n = goods != null && goods.data != null ? goods.data.Count : 0;
            _good = n == 0 ? -1 : Mathf.Clamp(_good, -1, n - 1);
            t.Selected = _good;   // the product whose price is being set: its stepper on its row, the others' prices as figures
            t.Rows = new UiDataRow[n];
            for (int i = 0; i < n; i++)
            {
                PricingManagerProductModel p = goods.data[i];
                float min = p.Suggestion.suggestedMin, max = p.Suggestion.suggestedMax;
                string suggested = Mathf.Approximately(min, max) ? max.ToCurrencyFormat() : UiText.Fill("reallife_ui_bizops_range", "from", min.ToCurrencyFormat(), "to", max.ToCurrencyFormat());
                t.Rows[i] = new UiDataRow
                {
                    Cells = new[] { p.ProductName, p.CurrentPrice >= 0f ? p.CurrentPrice.ToCurrencyFormat() : "-", suggested, p.RivalsPrice >= 0f ? p.RivalsPrice.ToCurrencyFormat() : "-",
                        p.CurrentPrice >= 0f ? p.CurrentPrice.ToCurrencyFormat() : p.SuggestedPrice.ToCurrencyFormat() },
                    Values = new[] { 0, 0, 0, 0, UiDataTable.CanLess | UiDataTable.CanMore }
                };
            }
            t.RowCount = n;
            return t;
        }

        private int _sortColumn = -1;   // the head last pressed on a table the game sorts by its own headers

        private static readonly string[] ManagedHeads = { "ProductName", "CurrentPrice", "SuggestedPrice", "RivalsPrice" };   // the game's sort keys, the model's properties

        // ------------------------------------------------------------------------------------------------- logistics

        private void Logistics(BizManBusiness b, BizOpsModel o)
        {
            LogisticsManagersPlanList list = b.logisticsLogisticsManagersPlanList;
            bool factories = list != null && string.Equals(list.currentTab, "Factory", StringComparison.OrdinalIgnoreCase);
            Button wTab = Get<Button>(GameContract.RouteWarehouses, list), fTab = Get<Button>(GameContract.RouteFactories, list);
            if (wTab != null && fTab != null && wTab.gameObject.activeInHierarchy && fTab.gameObject.activeInHierarchy)
            {
                o.Tabs = new[] { UiText.T("bizman_warehouses"), UiText.T("reallife_ui_bizops_factories") };
                o.Tab = factories ? 1 : 0;
            }
            Plans(Get<ReorderableList>(GameContract.RouteOrder, list), ButtonOf(Get<Transform>(GameContract.RouteAdd, list)?.gameObject), UiText.T("bizman_menu_logistics_managers"),
                UiText.T("reallife_ui_bizops_routes_none"), UiText.T("reallife_ui_bizops_routes_none_body"), e =>
                {
                    var entry = e.GetComponent<LogisticsManagersPlanListEntry>();
                    LogisticsManagerPlan plan = entry != null ? entry.Plan : null;
                    EmployeeInstance who = plan != null ? EmployeeHelper.GetEmployeeById(plan.assignedEmployeeId) : null;
                    BuildingRegistration from = plan != null && !plan.targetAddress.IsUndefined() ? BuildingHelper.GetBuildingRegistration(plan.targetAddress) : null;
                    return new BizManItem
                    {
                        Glyph = factories ? UiIcon.Factory : UiIcon.Package, Name = from != null ? from.BusinessName : UiText.T("common_unassigned"),
                        Line = who != null ? who.GetEmployeeNameWithInfo() : UiText.T("common_unassigned"), WarnWords = who == null ? UiText.T("reallife_ui_bizops_no_manager") : null,
                        Chosen = entry != null && entry.ManagerDropdown != null && entry.ManagerDropdown.gameObject.activeInHierarchy
                    };
                });
            LogisticsManagerPlanUI plan2 = list != null ? list.logisticsManagerPlanUI : null;
            _stops.Clear();
            if (ChosenPlan() < 0 || plan2 == null || !plan2.gameObject.activeInHierarchy)
            {
                return;
            }
            o.FormTitle = o.Plans[ChosenPlan()].Name;
            if (ManagerRows(plan2.noManagerAssignedPopUp, "bizman_logisticsmanagers_not_assigned", "bizman_logisticsmanagers_not_assigned_description"))
            {
                return;
            }
            _right = true;
            ChoiceLine(Get<UI.Elements.Dropdown>(GameContract.RouteFrom, plan2), UiText.T(factories ? "ba:businesstype_factory" : "ba:businesstype_warehouse"));
            _right = false;
            o.FormLead = Loc(Get<TextLocalizationComponent>(GameContract.RouteMost, plan2));
            var actions = new List<UiActionModel>(2);
            Button show = Get<Button>(GameContract.RouteShow, plan2), add = Get<Button>(GameContract.RouteAddStop, plan2);
            if (show != null && show.gameObject.activeInHierarchy)
            {
                actions.Add(new UiActionModel { Label = Loc(Get<TextLocalizationComponent>(GameContract.RouteShowLabel, plan2)) ?? UiText.T("common_show_warehouse"), Kind = UiButtonKind.Secondary,
                    Icon = UiIcon.Locate, Key = UiAction.Count, Arg = DoShowFrom, Disabled = !show.interactable });
            }
            if (add != null && add.gameObject.activeInHierarchy)
            {
                actions.Add(new UiActionModel { Label = UiText.T("bizman_logisticsmanagers_adddestination"), Kind = UiButtonKind.Primary, Icon = UiIcon.Plus,
                    Key = UiAction.Alt, Arg = DoAddStop, Disabled = !add.interactable });
            }
            o.Actions = actions.ToArray();
            Stops(plan2, o);
        }

        // The route's stops: each the game's own destination row, its place as its own drop-down chose it, past the route's
        // limit where the game fades it; the stop chosen shows its goods as the big table, each amount the game's own row's.
        private void Stops(LogisticsManagerPlanUI plan, BizOpsModel o)
        {
            List<LogisticsManagerDestinationUI> stops = Get<List<LogisticsManagerDestinationUI>>(GameContract.RouteStops, plan);
            for (int i = 0; stops != null && i < stops.Count; i++)
            {
                if (stops[i] != null && stops[i].gameObject.activeSelf)
                {
                    _stops.Add(stops[i]);
                }
            }
            o.StopsTitle = UiText.T("reallife_ui_bizops_stops");
            _stop = Mathf.Clamp(_stop, 0, Math.Max(0, _stops.Count - 1));
            o.Stops = new BizOpsStop[_stops.Count];
            for (int i = 0; i < _stops.Count; i++)
            {
                LogisticsManagerDestinationUI d = _stops[i];
                UI.Elements.Dropdown target = Get<UI.Elements.Dropdown>(GameContract.StopTarget, d);
                string[] options = Options(target);
                int pick = target != null ? target.SelectedOptionIndex : -1;
                CanvasGroup faded = d.GetComponent<CanvasGroup>();
                Button remove = Get<Button>(GameContract.StopRemove, d);
                int goods = GoodsOf(d, null);
                o.Stops[i] = new BizOpsStop
                {
                    Name = pick >= 0 && pick < options.Length ? options[pick] : UiText.T("common_unassigned"),
                    Line = (Loc(Get<TextLocalizationComponent>(GameContract.StopName, d)) ?? string.Empty) + ", " + UiText.Fill("reallife_ui_bizops_goods_count", "count", UiText.Whole(goods)),
                    Open = i == _stop, Warn = faded != null && faded.alpha < 0.99f, Removable = remove != null && remove.gameObject.activeInHierarchy
                };
                if (o.ListOpen == BizManPages.OpsStopList + i)
                {
                    o.Options = options;
                    o.OptionChosen = pick;
                }
            }
            if (_stops.Count == 0)
            {
                o.Stops = new BizOpsStop[0];
                o.FormLead = (o.FormLead != null ? o.FormLead + " " : string.Empty) + UiText.T("reallife_ui_bizops_stops_none");
                return;
            }
            _goods.Clear();
            GoodsOf(_stops[_stop], _goods);
            o.TableLead = UiText.Fill("reallife_ui_bizops_stop_goods", "place", o.Stops[_stop].Name);
            var t = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = UiText.T("common_product"), Kind = UiCellKind.Text },
                    new UiDataColumn { Title = UiText.T("bizman_logisticsmanagers_warehouse_stock"), Kind = UiCellKind.Figure, Width = 152f },
                    new UiDataColumn { Title = UiText.T("bizman_logisticsmanagers_runs_out_in"), Kind = UiCellKind.Text, Width = 136f },
                    new UiDataColumn { Title = UiText.T("bizman_logisticsmanagers_min_stock_amount"), Kind = UiCellKind.Stepper, Width = UiDataTable.StepperWidth, OnChosen = true },
                },
                EmptyIcon = UiIcon.Package, EmptyTitle = UiText.T("bizman_logisticsmanagers_no_products"), Offset = o.Table != null ? o.Table.Offset : 0f
            };
            t.Rows = new UiDataRow[_goods.Count];
            for (int i = 0; i < _goods.Count; i++)
            {
                Transform g = _goods[i];
                TextLocalizationComponent name = g.Find("NameRow/ProductName") != null ? g.Find("NameRow/ProductName").GetComponent<TextLocalizationComponent>() : null;
                InputField amount = g.Find("BusinessTarget/Amount") != null ? g.Find("BusinessTarget/Amount").GetComponent<InputField>() : null;
                t.Rows[i] = new UiDataRow
                {
                    Cells = new[]
                    {
                        Loc(name) ?? string.Empty, WordsAt(g, "WarehouseStock") ?? "-", WordsAt(g, "RunsOutIn") ?? "-",
                        amount != null && amount.tmpInputField != null ? amount.tmpInputField.text : "0"
                    },
                    Values = new[] { 0, 0, 0, UiDataTable.CanLess | UiDataTable.CanMore }
                };
            }
            t.RowCount = _goods.Count;
            _good = _goods.Count == 0 ? -1 : Mathf.Clamp(_good, -1, _goods.Count - 1);
            t.Selected = _good;   // the good whose target is being set: its stepper on its row
            o.Table = t;
        }

        // A stop's goods: the game's own rows under its list, counted, and kept when asked for.
        private static int GoodsOf(LogisticsManagerDestinationUI d, List<Transform> into)
        {
            Transform rows = d.productsElementTemplate != null ? d.productsElementTemplate.parent : null;
            int n = 0;
            for (int i = 0; rows != null && i < rows.childCount; i++)
            {
                Transform r = rows.GetChild(i);
                if (r == d.productsElementTemplate || r.Find("BusinessTarget/Amount") == null)
                {
                    continue;
                }
                n++;
                into?.Add(r);
            }
            return n;
        }

        private UI.Elements.Dropdown StopChoice(int stop) => stop >= 0 && stop < _stops.Count ? Get<UI.Elements.Dropdown>(GameContract.StopTarget, _stops[stop]) : null;

        // ------------------------------------------------------------------------------------------------- purchasing

        private void Purchasing(BizManBusiness b, BizOpsModel o)
        {
            PurchasingAgentsPlanList list = b.purchasingAgentsPlanList;
            GameObject none = Get<GameObject>(GameContract.BuyNone, list);
            Plans(Get<ReorderableList>(GameContract.BuyOrder, list), null, UiText.T("bizman_menu_purchasing_agents"), UiText.T("reallife_ui_bizops_imports_none"),
                UiText.T("reallife_ui_bizops_imports_none_body"), e => Entry(e, UiIcon.Handshake, "ContractName", null));
            if (none != null && none.activeInHierarchy)
            {
                o.Plans = new BizManItem[0];
            }
            PurchasingAgentPlanUI plan = list != null ? list.purchasingAgentPlanUISettings : null;
            if (ChosenPlan() < 0 || plan == null || !plan.gameObject.activeInHierarchy)
            {
                return;
            }
            o.FormTitle = Loc(plan.contractTitleLabel) ?? o.Plans[ChosenPlan()].Name;
            if (ManagerRows(plan.noManagerAssignedPopUp, "bizman_purchasingagents_not_assigned", "bizman_purchasingagents_not_assigned_description"))
            {
                return;
            }
            InfoLine(UiText.T("bizman_purchasingagents_next_delivery"), Loc(plan.nextDeliveryDayLabel));
            InfoLine(UiText.T("bizman_purchasingagents_lock_time_header"), Loc(plan.lockTimeLabel));
            InfoLine(UiText.T("bizman_purchasingagents_total"), TextOf(plan.nextDeliveryTotalLabel));
            _right = true;
            SwitchLine(plan.repeatingOrderToggle, UiText.T("repeating_order"));
            SwitchLine(plan.autoStockToggle, UiText.T("bizman_purchasingagents_smart_delivery"));
            if (plan.massActionsUI != null && plan.massActionsUI.gameObject.activeInHierarchy)
            {
                SwitchLine(plan.massActionsUI.massActionAllToggle, UiText.T("reallife_ui_bizops_pick_all"));
                ChoiceLine(Get<UI.Elements.Dropdown>(GameContract.BuyMassTo, plan.massActionsUI), UiText.T("reallife_ui_bizops_designate"));
            }
            _right = false;
            var actions = new List<UiActionModel>(4);
            Offer(actions, plan.endPartnershipButton, DoBuyEnd, UiButtonKind.Destructive, UiIcon.Close, UiAction.Count, "bizman_purchasingagents_end_partnership");
            Offer(actions, plan.cancelContractButton, DoBuyCancel, UiButtonKind.Secondary, UiIcon.Close, UiAction.Count, "bizman_purchasingagents_cancel_order");
            Offer(actions, plan.urgentOrderButton, DoBuyUrgent, UiButtonKind.Secondary, UiIcon.Lightning, UiAction.Count, "bizman_deliveries_urgent_order");
            Offer(actions, plan.orderButton, DoBuyOrder, UiButtonKind.Primary, UiIcon.ShoppingCart, UiAction.Alt, "bizman_purchasingagents_order");
            for (int i = 0; i < actions.Count; i++)
            {
                UiActionModel a = actions[i];
                a.Command = BizManPages.OpsAction;
                actions[i] = a;
            }
            o.Actions = actions.ToArray();
            BuyTable(plan, o);   // under its head alone: its first column says Product
        }

        // The purchasing plan's goods: each its target, its boxes, its price, the warehouse's stock and the warehouse it
        // goes to, a row picked where the game's own mass toggle is on; the good chosen gets its target and warehouse
        // as the form's rows.
        private void BuyTable(PurchasingAgentPlanUI plan, BizOpsModel o)
        {
            PurchasingAgentProductsScrollerController goods = plan.productsScrollerController;
            var t = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = UiText.T("bizman_purchasingagents_product"), Kind = UiCellKind.Text },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizops_target"), Kind = UiCellKind.Figure, Width = 96f },
                    new UiDataColumn { Title = UiText.T("bizman_purchasingagents_price"), Kind = UiCellKind.Money, Width = 128f },
                    new UiDataColumn { Title = UiText.T("bizman_purchasingagents_warehouse_stock"), Kind = UiCellKind.Figure, Width = 136f },
                    new UiDataColumn { Title = UiText.T("bizman_purchasingagents_designated_warehouse"), Kind = UiCellKind.Text, Width = 200f },
                },
                EmptyIcon = UiIcon.ShoppingCart, EmptyTitle = UiText.T("reallife_ui_bizops_imports_goods_none"), Offset = o.Table != null ? o.Table.Offset : 0f
            };
            int n = goods != null && goods.data != null ? goods.data.Count : 0;
            _good = n == 0 ? -1 : Mathf.Clamp(_good, -1, n - 1);
            t.Rows = new UiDataRow[n];
            for (int i = 0; i < n; i++)
            {
                PurchasingAgentProductModel p = goods.data[i];
                t.Rows[i] = new UiDataRow
                {
                    Cells = new[]
                    {
                        p.ProductName, UiText.Whole(p.Target), p.Price.ToCurrencyFormat(), UiText.Whole(p.WarehouseStock),
                        string.IsNullOrEmpty(p.AssignedWarehouse) ? UiText.T("common_unassigned") : p.AssignedWarehouse
                    },
                    Values = new[] { 0, 0, 0, 0, 0 }, Picked = p.isSelected,
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, string.IsNullOrEmpty(p.AssignedWarehouse) ? UiRole.WarnText : UiRole.None }
                };
            }
            t.RowCount = n;
            t.Selected = _good;
            o.Table = t;
            if (_good >= 0 && Cell(goods, _good) is PurchasingAgentProductCellView cell)
            {
                _right = true;
                Heading(goods.data[_good].ProductName);
                TextLine(cell.targetInput != null ? cell.targetInput.tmpInputField : null, UiText.T("reallife_ui_bizops_target"), null, true);
                ChoiceLine(cell.assignedWarehouseDropdown, UiText.T("bizman_purchasingagents_designated_warehouse"));
                SwitchLine(cell.massActionToggle, UiText.T("reallife_ui_bizops_pick"));
                _right = false;
            }
        }

        // ------------------------------------------------------------------------------------------------- HR managers

        private void Hr(BizManBusiness b, BizOpsModel o)
        {
            HrManagersPlanList list = b.hrManagersPlanList;
            Plans(Get<ReorderableList>(GameContract.HrOrder, list), ButtonOf(Get<Transform>(GameContract.HrAdd, list)?.gameObject), UiText.T("bizman_menu_hr_managers"),
                UiText.T("reallife_ui_bizops_hr_none"), UiText.T("reallife_ui_bizops_hr_none_body"), e => Entry(e, UiIcon.People, null, "reallife_ui_bizops_hr_line"));
            HrManagerPlanUI plan = b.hrHrManagerPlanUI;
            if (ChosenPlan() < 0 || plan == null || !plan.gameObject.activeInHierarchy)
            {
                return;
            }
            o.FormTitle = o.Plans[ChosenPlan()].Name;
            if (ManagerRows(plan.noManagerAssignedPopUp, "bizman_hrmanager_not_assigned", "bizman_hrmanager_not_assigned_description"))
            {
                return;
            }
            o.FormLead = Loc(Get<TextLocalizationComponent>(GameContract.HrCount, plan));
            SwitchLine(Get<Toggle>(GameContract.HrReplace, plan), UiText.T("reallife_ui_bizops_hr_replace"));
            SliderLine(Get<Slider>(GameContract.HrTrain, plan), Get<TMP_Text>(GameContract.HrTrainValue, plan), UiText.T("reallife_ui_bizops_hr_train"));
            _right = true;
            InfoLine(UiText.T("bizman_hrmanager_average_salary"), Loc(Get<TextLocalizationComponent>(GameContract.HrWage, plan)));
            MeterLine(UiText.T("reallife_ui_bizops_hr_mood"), Get<UI.Elements.ProgressBar>(GameContract.HrMood, plan));
            MeterLine(UiText.T("reallife_ui_bizops_hr_skill"), Get<UI.Elements.ProgressBar>(GameContract.HrSkill, plan));
            Heading(UiText.T("reallife_ui_bizops_hr_cover"));
            NoteLine(Loc(Get<TextLocalizationComponent>(GameContract.HrCover, plan)) ?? Loc(Get<TextLocalizationComponent>(GameContract.HrCoverNone, plan)));
            ButtonLine(ButtonOf(Get<GameObject>(GameContract.HrCoverUp, plan)), null, UiText.T("bizman_hrmanager_upgrade_insurance_button"), UiIcon.ShieldCheck, false);
            ButtonLine(ButtonOf(Get<GameObject>(GameContract.HrCoverCancel, plan)), null, UiText.T("bizman_hrmanager_cancel_insurance_button"), UiIcon.Close, true);
            _right = false;
            bool assigning = plan.IsAssignEmployeesListOpen;
            var actions = new List<UiActionModel>(3);
            if (assigning)
            {
                Transform box = Get<Transform>(GameContract.HrAssignBox, plan);
                AddCalling(actions, box, "ClearAssignedEmployees", DoHrClear, UiButtonKind.Secondary, UiIcon.Close, "bizman_hrmanager_clear");
                AddCalling(actions, box, "Fill", DoHrFill, UiButtonKind.Secondary, UiIcon.People, "bizman_hrmanager_fill");
                AddCalling(actions, box, "CloseAssignEmployeesList", DoHrClose, UiButtonKind.Primary, UiIcon.Check, "reallife_ui_bizops_done");
                o.TableTitle = Loc(Get<TextLocalizationComponent>(GameContract.HrAssignTitle, plan));
                o.Table = HrTable(Get<EmployeesScrollerController>(GameContract.HrAssignList, plan), o.Table, true);
            }
            else
            {
                AddCalling(actions, plan, "OpenAssignEmployeesList", DoHrAssign, UiButtonKind.Primary, UiIcon.People, "bizman_hrmanager_assign_employees");
                o.TableTitle = UiText.T("reallife_ui_bizops_hr_staff");
                o.Table = HrTable(Get<EmployeesScrollerController>(GameContract.HrStaff, plan), o.Table, false);
            }
            o.Actions = actions.ToArray();
        }

        // An action whose button the game wired in its Inspector to a method of its page, found by that method, offered while it shows.
        private static void AddCalling(List<UiActionModel> into, Component root, string method, int what, UiButtonKind kind, UiIcon icon, string key)
        {
            Button b = Calling(root, method);
            if (b != null)
            {
                into.Add(new UiActionModel { Label = UiText.T(key), Kind = kind, Icon = icon, Key = kind == UiButtonKind.Primary ? UiAction.Alt : UiAction.Count,
                    Command = BizManPages.OpsAction, Arg = what, Disabled = !b.interactable });
            }
        }

        // The plan's staff (or, while assigning, every one the manager may take with whether they are), as the game's tables list them.
        private static UiDataTableModel HrTable(EmployeesScrollerController staff, UiDataTableModel old, bool assigning)
        {
            var t = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_person"), Kind = UiCellKind.Person },
                    new UiDataColumn { Title = UiText.T("common_business"), Kind = UiCellKind.Text, Width = 200f },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_wage"), Kind = UiCellKind.Money, Width = 112f },
                    new UiDataColumn { Title = UiText.T("bizman_hrmanager_insurance_demand"), Kind = UiCellKind.Text, Width = 152f },
                    assigning ? new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_assigned"), Kind = UiCellKind.Chip, Width = 128f }
                        : new UiDataColumn { Title = UiText.T("reallife_ui_bizops_hr_mood"), Kind = UiCellKind.Meter, Width = 136f },
                },
                EmptyIcon = UiIcon.People, EmptyTitle = UiText.T(assigning ? "reallife_ui_bizops_hr_assign_none" : "reallife_ui_bizops_hr_staff_none"),
                EmptyBody = assigning ? null : UiText.T("reallife_ui_bizops_hr_staff_none_body"), Offset = old != null ? old.Offset : 0f
            };
            int n = staff != null && staff.data != null ? staff.data.Count : 0;
            t.Rows = new UiDataRow[n];
            for (int i = 0; i < n; i++)
            {
                HrManagerEmployeeModel e = staff.data[i];
                EmployeeInstance who = EmployeeHelper.GetEmployeeById(e.employeeId);
                float wage = who != null ? PaidWage.NowMoney(who.hourlyWage, who.id) : e.hourlyWage;
                int mood = Mathf.Clamp((int)e.satisfaction, 0, 100);
                t.Rows[i] = new UiDataRow
                {
                    Cells = new[] { e.employeeName, e.businessName, UiText.Fill("bizman_hrmanager_salary", "wage", wage.ToCurrencyFormat()), e.insuranceDemandText,
                        assigning ? UiText.T(e.assigned ? "reallife_ui_bizops_assigned" : "reallife_ui_bizops_not_assigned") : UiText.Whole(mood) + "%" },
                    Values = new[] { 0, 0, 0, 0, assigning ? 0 : mood }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, e.assigned ? UiRole.Ok : UiRole.Muted },
                    Sub = string.IsNullOrEmpty(e.primarySkillName) ? null : UiText.T(e.primarySkillName), Hue = UiColour.HueOf(e.employeeName ?? string.Empty), Ring = StaffViews.Mood(mood)
                };
            }
            t.RowCount = n;
            return t;
        }

        // ------------------------------------------------------------------------------------------------- headhunters

        private void Headhunters(BizManBusiness b, BizOpsModel o)
        {
            Component list = PageOf<UI.Smartphone.Apps.BizMan.Headhunters.HeadhuntersPlanList>(b, BizManRules.Headhunters);
            Plans(Get<ReorderableList>(GameContract.HuntOrder, list), ButtonOf(Get<Transform>(GameContract.HuntAdd, list)?.gameObject), UiText.T("bizman_menu_headhunters"),
                UiText.T("reallife_ui_bizops_hunt_none"), UiText.T("reallife_ui_bizops_hunt_none_body"), e => Entry(e, UiIcon.IdBadge, null, "reallife_ui_bizops_hunt_line"));
            HeadhunterPlanUI plan = Get<HeadhunterPlanUI>(GameContract.HuntPlan, list);
            if (ChosenPlan() < 0 || plan == null || !plan.gameObject.activeInHierarchy)
            {
                return;
            }
            o.FormTitle = o.Plans[ChosenPlan()].Name;
            bool replacing = On(Get<GameObject>(GameContract.HuntReplacePanel, plan));
            o.Tabs = new[] { UiText.T("bizman_menu_headhunters_recruiting_header"), UiText.T("bizman_menu_headhunters_automatic_replacement_header") };
            o.Tab = replacing ? 1 : 0;
            if (ManagerRows(plan.noManagerAssignedPopUp, "bizman_headhunter_not_assigned", "bizman_headhunter_not_assigned_description"))
            {
                return;
            }
            if (replacing)
            {
                Replacing(plan.GetComponentInChildren<HeadhuntersAutomaticReplacementTab>(true), o);
            }
            else
            {
                Recruiting(plan.GetComponentInChildren<HeadhuntersRecruitingTab>(true), o);
            }
        }

        // Recruiting: the kind of person and Start; then the target skill, the wage it means, how many to find; the deal
        // breakers with the points they use; and what is being recruited now with its candidates and Stop.
        private void Recruiting(HeadhuntersRecruitingTab tab, BizOpsModel o)
        {
            if (tab == null)
            {
                return;
            }
            if (On(Get<GameObject>(GameContract.HuntKindPanel, tab)))
            {
                ChoiceLine(Get<UI.Elements.Dropdown>(GameContract.HuntKind, tab), UiText.T("bizman_headhunters_type_of_employee_title"));
                ButtonLine(ButtonOf(Get<GameObject>(GameContract.HuntStart, tab)), null, UiText.T("bizman_headhunters_start_recruiting_button"), UiIcon.Play, false);
            }
            if (On(Get<GameObject>(GameContract.HuntNow, tab)))
            {
                Heading(UiText.T("bizman_headhunters_currently_recruiting_title"));
                InfoLine(UiText.T("bizman_headhunters_employee_type_title"), Loc(Get<TextLocalizationComponent>(GameContract.HuntNowSkill, tab)));
                InfoLine(UiText.T("bizman_headhunters_skill_level_title"), Loc(Get<TextLocalizationComponent>(GameContract.HuntNowLevel, tab)));
                InfoLine(UiText.T("bizman_headhunters_salary_range_title"), Loc(Get<TextLocalizationComponent>(GameContract.HuntNowWage, tab)));
                ButtonLine(ButtonOf(Get<GameObject>(GameContract.HuntView, tab)), null, UiText.T("bizman_headhunters_view_current_recruits_button"), UiIcon.People, false);
                ButtonLine(ButtonOf(Get<GameObject>(GameContract.HuntStop, tab)), null, UiText.T("bizman_headhunters_stop_recruiting_button"), UiIcon.Pause, true);
            }
            if (On(Get<GameObject>(GameContract.HuntWagePanel, tab)))
            {
                Heading(UiText.T("bizman_headhunters_wage_and_skill_title"));
                SliderLine(Get<Slider>(GameContract.HuntTarget, tab), Get<TMP_Text>(GameContract.HuntTargetLabel, tab), UiText.T("reallife_ui_bizops_hunt_target"));
                InfoLine(UiText.T("bizman_headhunters_salary_range_title"), Loc(Get<TextLocalizationComponent>(GameContract.HuntWage, tab)));
            }
            if (On(Get<GameObject>(GameContract.HuntSettings, tab)))
            {
                SwitchLine(Get<Toggle>(GameContract.HuntAlways, tab), UiText.T("reallife_ui_bizops_hunt_always"));
                SwitchLine(Get<Toggle>(GameContract.HuntCount, tab), UiText.T("reallife_ui_bizops_hunt_count"));
                TextLine(Get<TMP_InputField>(GameContract.HuntAmount, tab), UiText.T("reallife_ui_bizops_hunt_amount"), null, true);
            }
            HeadhuntersDealBreakers breakers = Get<HeadhuntersDealBreakers>(GameContract.HuntBreakers, tab);
            _breakers.Clear();
            if (breakers != null && breakers.gameObject.activeInHierarchy)
            {
                _right = true;
                Heading(UiText.T("bizman_headhunters_deal_breakers_title"));
                NoteLine(Loc(Get<TextLocalizationComponent>(GameContract.HuntPoints, tab)));   // the game's own line says what it counts ("Points used: 3")
                breakers.GetComponentsInChildren(false, _breakers);
                for (int i = 0; i < _breakers.Count; i++)
                {
                    TMP_Text words = _breakers[i].GetComponentInChildren<TMP_Text>(true);
                    SwitchLine(_breakers[i], words != null ? UiWidgets.Text(words) : UiText.T("reallife_ui_bizops_hunt_breaker"));
                }
                ButtonLine(ButtonOf(Get<GameObject>(GameContract.HuntClear, tab)), null, UiText.T("bizman_headhunters_deal_breakers_clear_all_button"), UiIcon.Close, false);
                _right = false;
            }
        }

        // Automatic replacement: replacing on resignation and retirement with their fees, the warning when the headhunter
        // lacks the skill, and the people close to needing it.
        private void Replacing(HeadhuntersAutomaticReplacementTab tab, BizOpsModel o)
        {
            if (tab == null)
            {
                return;
            }
            if (On(Get<GameObject>(GameContract.HuntWeak, tab)))
            {
                Heading(UiText.T("bizman_headhunters_automatic_replacement_not_enough_skill_title"));
                NoteLine(UiText.T("bizman_headhunters_automatic_replacement_not_enough_skill_description"));
            }
            SwitchLine(Get<Toggle>(GameContract.HuntResign, tab), UiText.T("reallife_ui_bizops_hunt_resign"));
            NoteLine(Loc(Get<TextLocalizationComponent>(GameContract.HuntResignFee, tab)));   // "Replacement Fee: $2.5K", the game's own line
            _right = true;
            SwitchLine(Get<Toggle>(GameContract.HuntRetire, tab), UiText.T("reallife_ui_bizops_hunt_retire"));
            NoteLine(Loc(Get<TextLocalizationComponent>(GameContract.HuntRetireFee, tab)));
            _right = false;
            HeadhunterEmployeesScrollerController staff = Get<HeadhunterEmployeesScrollerController>(GameContract.HuntStaff, tab);
            var t = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_person"), Kind = UiCellKind.Person },
                    new UiDataColumn { Title = UiText.T("common_business"), Kind = UiCellKind.Text, Width = 224f },
                    new UiDataColumn { Title = UiText.T("bizman_headhunters_replacement_reason"), Kind = UiCellKind.Text, Width = 240f },
                },
                EmptyIcon = UiIcon.People, EmptyTitle = UiText.T("reallife_ui_bizops_hunt_staff_none"), Offset = o.Table != null ? o.Table.Offset : 0f
            };
            int n = staff != null && staff.data != null ? staff.data.Count : 0;
            t.Rows = new UiDataRow[n];
            for (int i = 0; i < n; i++)
            {
                HeadhunterEmployeeModel e = staff.data[i];
                t.Rows[i] = new UiDataRow
                {
                    Cells = new[] { e.EmployeeName, e.BusinessName, e.ReplacementReason }, Values = new[] { 0, 0, 0 }, Sub = string.IsNullOrEmpty(e.PrimarySkill) ? null : UiText.T(e.PrimarySkill),
                    Hue = UiColour.HueOf(e.EmployeeName ?? string.Empty), Ring = UiRole.None
                };
            }
            t.RowCount = n;
            o.TableTitle = UiText.T("reallife_ui_bizops_hunt_staff");
            o.Table = t;
        }

        // ------------------------------------------------------------------------------------------------- presses

        private bool PressManagers(BizManBusiness b, int command, int arg)
        {
            switch (command)
            {
                case BizManPages.OpsPlan: return SelectPlan(b, arg);
                case BizManPages.OpsTab: return ManagersTab(b, arg);
                case BizManPages.OpsAction: return ManagersAction(b, arg);
                case BizManPages.OpsFirstPlan: return ManagersAction(b, DoAddPlan);
                case BizManPages.OpsStop:
                    _stop = arg;
                    _good = -1;
                    return true;
                case BizManPages.OpsStopChoice:
                    _m.Ops.ListOpen = _m.Ops.ListOpen == BizManPages.OpsStopList + arg ? -1 : BizManPages.OpsStopList + arg;
                    return true;
                case BizManPages.OpsStopRemove:
                    return arg >= 0 && arg < _stops.Count && Click(Get<Button>(GameContract.StopRemove, _stops[arg]), "remove a stop (the game asks first)");
                case BizManPages.Table + UiDataTable.Head: return ManagersHead(b, arg);
                case BizManPages.Table + UiDataTable.Row: return ManagersRow(b, arg);
                case BizManPages.Table + UiDataTable.Less:
                case BizManPages.Table + UiDataTable.More:
                    return ManagersStep(b, arg, command == BizManPages.Table + UiDataTable.More);
                case BizManPages.Table + UiDataTable.Type: return true;
                default: return PressRow(command, arg);
            }
        }

        // A plan chosen: the game's own entry pressed, as a click on it selects it and loads its page.
        private bool SelectPlan(BizManBusiness b, int index)
        {
            if (index < 0 || index >= _plans.Count)
            {
                return false;
            }
            _stop = 0;
            _good = -1;
            ReorderableListItem e = _plans[index];
            if (e.GetComponent<PricingManagersPlanListEntry>() is PricingManagersPlanListEntry price && b.pricingManagersPlanList != null)
            {
                b.pricingManagersPlanList.SelectPlan(price);   // what its own button calls
                return true;
            }
            if (e.GetComponent<LogisticsManagersPlanListEntry>() is LogisticsManagersPlanListEntry route && b.logisticsLogisticsManagersPlanList != null)
            {
                b.logisticsLogisticsManagersPlanList.SelectPlan(route.transform, route.Plan);
                return true;
            }
            return Click(e.GetComponent<Button>(), "choose a plan");
        }

        // The page's own tabs: logistics' warehouses and factories, a headhunter's recruiting and replacement; each the game's own tab button.
        private bool ManagersTab(BizManBusiness b, int tab)
        {
            if (_shown == BizManRules.LogisticsManagers)
            {
                LogisticsManagersPlanList list = b.logisticsLogisticsManagersPlanList;
                return Click(Get<Button>(tab == 1 ? GameContract.RouteFactories : GameContract.RouteWarehouses, list), "the logistics' " + (tab == 1 ? "factories" : "warehouses"));
            }
            HeadhunterPlanUI plan = Get<HeadhunterPlanUI>(GameContract.HuntPlan, PageOf<UI.Smartphone.Apps.BizMan.Headhunters.HeadhuntersPlanList>(b, BizManRules.Headhunters));
            return Click(Get<Button>(tab == 1 ? GameContract.HuntReplaceTab : GameContract.HuntRecruitTab, plan), "a headhunter's tab");
        }

        private bool ManagersAction(BizManBusiness b, int what)
        {
            switch (what)
            {
                case DoAddPlan: return Click(_planAdd, "add a plan");
                case DoPlanUp:
                case DoPlanDown: return Reorder(ChosenPlan(), what == DoPlanUp ? -1 : 1);
                case DoDeletePlan:
                    if (_noManager == null)
                    {
                        return false;
                    }
                    _noManager.DeletePlan();   // the game's own Delete plan: its question first
                    return true;
                case DoSuggest:
                    return Click(Get<Button>(GameContract.PriceSuggest, Get<PricingManagerProductsScrollerController>(GameContract.PriceGoods,
                        Get<PricingManagerPlanUI>(GameContract.PriceList, b.pricingManagersPlanList))), "apply the suggested prices (the game asks first)");
                case DoShowFrom: return Click(Get<Button>(GameContract.RouteShow, b.logisticsLogisticsManagersPlanList?.logisticsManagerPlanUI), "show the route's warehouse");
                case DoAddStop: return Click(Get<Button>(GameContract.RouteAddStop, b.logisticsLogisticsManagersPlanList?.logisticsManagerPlanUI), "add a stop");
                case DoBuyEnd: return Click(b.purchasingAgentsPlanList?.purchasingAgentPlanUISettings?.endPartnershipButton, "end the import partnership (the game asks first)");
                case DoBuyCancel: return Click(b.purchasingAgentsPlanList?.purchasingAgentPlanUISettings?.cancelContractButton, "cancel the order");
                case DoBuyUrgent: return Click(b.purchasingAgentsPlanList?.purchasingAgentPlanUISettings?.urgentOrderButton, "an urgent order (the game asks first)");
                case DoBuyOrder: return Click(b.purchasingAgentsPlanList?.purchasingAgentPlanUISettings?.orderButton, "place the order");
                case DoHrAssign: return Click(Calling(b.hrHrManagerPlanUI, "OpenAssignEmployeesList"), "assign employees");
                case DoHrFill: return Click(Calling(Get<Transform>(GameContract.HrAssignBox, b.hrHrManagerPlanUI), "Fill"), "fill the plan");
                case DoHrClear: return Click(Calling(Get<Transform>(GameContract.HrAssignBox, b.hrHrManagerPlanUI), "ClearAssignedEmployees"), "unassign all");
                case DoHrClose: return Click(Calling(Get<Transform>(GameContract.HrAssignBox, b.hrHrManagerPlanUI), "CloseAssignEmployeesList"), "close the assigning");
                default: return false;
            }
        }

        // A plan moved up or down its list as a drag of the game's own list moves it: the entry taken up, laid where its
        // neighbour stands, let go, so the list's own order and the game's save change as for the player's drag.
        private bool Reorder(int index, int step)
        {
            int to = index + step;
            if (_planOrder == null || index < 0 || to < 0 || index >= _plans.Count || to >= _plans.Count)
            {
                return false;
            }
            ReorderableListItem item = _plans[index];
            float y = _plans[to].transform.position.y;
            _planOrder.BeginDrag(item, null);
            Vector3 at = item.transform.position;
            item.transform.position = new Vector3(at.x, y, at.z);
            _planOrder.EndDrag();
            UiDirector.Logger?.Info("BizMan (kit): a plan moved through the game's own list");
            return true;
        }

        // A head of a table the game sorts by its own headers: the pricing managers' goods.
        private bool ManagersHead(BizManBusiness b, int column)
        {
            if (_shown != BizManRules.PricingManagers || column < 0 || column >= ManagedHeads.Length)
            {
                return false;
            }
            PricingManagerProductsScrollerController goods = Get<PricingManagerProductsScrollerController>(GameContract.PriceGoods,
                Get<PricingManagerPlanUI>(GameContract.PriceList, b.pricingManagersPlanList));
            if (goods == null)
            {
                return false;
            }
            goods.ChangeSortOrder(ManagedHeads[column], _sortColumn == column);
            _sortColumn = column;
            return true;
        }

        // A row of a table: the purchasing agent's good chosen for its form; while assigning, a person's own assign toggle flipped.
        private bool ManagersRow(BizManBusiness b, int row)
        {
            if (_shown == BizManRules.PurchasingAgents)
            {
                _good = _good == row ? -1 : row;
                return true;
            }
            if (_shown == BizManRules.PricingManagers || _shown == BizManRules.LogisticsManagers)
            {
                _good = row;   // its stepper comes to its row; the focus stays on the row, the stepper a step right
                return true;
            }
            HrManagerPlanUI plan = b.hrHrManagerPlanUI;
            if (_shown == BizManRules.HrManagers && plan != null && plan.IsAssignEmployeesListOpen
                && Cell(Get<EmployeesScrollerController>(GameContract.HrAssignList, plan), row) is UI.Smartphone.Apps.BizMan.HRManagers.EmployeeCellView cell)
            {
                Toggle assign = Get<Toggle>(GameContract.HrAssignToggle, cell);
                return assign != null && UiWidgets.Set(assign, !assign.isOn);   // its listener refuses past the manager's limit with its own notice
            }
            return false;
        }

        // A row's amount stepped on the game's own row: a pricing manager's price, a stop's target.
        private bool ManagersStep(BizManBusiness b, int row, bool more)
        {
            if (_shown == BizManRules.PricingManagers)
            {
                PricingManagerProductsScrollerController goods = Get<PricingManagerProductsScrollerController>(GameContract.PriceGoods,
                    Get<PricingManagerPlanUI>(GameContract.PriceList, b.pricingManagersPlanList));
                return Cell(goods, row) is PricingManagerProductCellView cell && Click(Get<Button>(more ? GameContract.PricePlus : GameContract.PriceMinus, cell), "a managed price stepped");
            }
            if (_shown == BizManRules.LogisticsManagers && row >= 0 && row < _goods.Count)
            {
                Transform btn = _goods[row].Find(more ? "BusinessTarget/PlusButton" : "BusinessTarget/MinusButton");
                return btn != null && Click(btn.GetComponent<Button>(), "a stop's target stepped");
            }
            return false;
        }

        // The field a table's typed figure types into: a managed price, a stop's target.
        private TMP_InputField ManagersField(BizManBusiness b, int row)
        {
            if (_shown == BizManRules.PricingManagers)
            {
                PricingManagerProductsScrollerController goods = Get<PricingManagerProductsScrollerController>(GameContract.PriceGoods,
                    Get<PricingManagerPlanUI>(GameContract.PriceList, b.pricingManagersPlanList));
                InputField f = Cell(goods, row) is PricingManagerProductCellView cell ? Get<InputField>(GameContract.PriceInput, cell) : null;
                return f != null ? f.tmpInputField : null;
            }
            if (_shown == BizManRules.LogisticsManagers && row >= 0 && row < _goods.Count)
            {
                Transform amount = _goods[row].Find("BusinessTarget/Amount");
                InputField f = amount != null ? amount.GetComponent<InputField>() : null;
                return f != null ? f.tmpInputField : null;
            }
            return null;
        }

        // The game's own cell for a row of one of its tables, its scroller jumped to it so the cell is laid and live.
        private static Component Cell<TCell, TModel>(BaTable.BaTable<TCell, TModel> table, int row) where TCell : BaTable.BaTableCellView<TModel>
        {
            if (table == null || table.scroller == null || table.data == null || row < 0 || row >= table.data.Count)
            {
                return null;
            }
            table.scroller.JumpToDataIndex(row);
            return table.scroller.GetCellViewAtDataIndex(row);
        }

        // ------------------------------------------------------------------------------------------------- what changed

        private long ManagersSignature(BizManBusiness b)
        {
            long sig = _stop * 7 + _good * 131 + _sortColumn;
            for (int i = 0; i < _plans.Count; i++)
            {
                sig = sig * 31 + (_plans[i] != null && _plans[i].gameObject.activeSelf ? _plans[i].transform.GetSiblingIndex() + 1 : 0);
                Transform drop = _plans[i] != null ? _plans[i].transform.Find("ManagerDropdown") : null;
                sig = sig * 3 + (drop != null && drop.gameObject.activeSelf ? 1 : 0);
            }
            if (_planOrder != null)
            {
                sig = sig * 31 + _planOrder.transform.childCount;
            }
            sig = sig * 31 + (_noManager != null && _noManager.gameObject.activeInHierarchy ? 1 : 0);
            sig = sig * 31 + WidgetsSignature();
            switch (_shown)
            {
                case BizManRules.PricingManagers:
                {
                    var goods = Get<PricingManagerProductsScrollerController>(GameContract.PriceGoods, Get<PricingManagerPlanUI>(GameContract.PriceList, b.pricingManagersPlanList));
                    for (int i = 0; goods != null && goods.data != null && i < goods.data.Count; i++)
                    {
                        sig = sig * 31 + goods.data[i].CurrentPrice.GetHashCode();
                    }
                    return sig;
                }
                case BizManRules.LogisticsManagers:
                    sig = sig * 31 + _stops.Count + _goods.Count * 7;
                    for (int i = 0; i < _goods.Count; i++)
                    {
                        sig = sig * 31 + Hash(WordsAt(_goods[i], "BusinessTarget/Amount/Text Area/Text"));
                        Transform amount = _goods[i] != null ? _goods[i].Find("BusinessTarget/Amount") : null;
                        InputField f = amount != null ? amount.GetComponent<InputField>() : null;
                        sig = sig * 31 + Hash(f != null && f.tmpInputField != null ? f.tmpInputField.text : null);
                    }
                    return sig;
                case BizManRules.PurchasingAgents:
                {
                    var goods = b.purchasingAgentsPlanList?.purchasingAgentPlanUISettings?.productsScrollerController;
                    for (int i = 0; goods != null && goods.data != null && i < goods.data.Count; i++)
                    {
                        sig = sig * 31 + goods.data[i].Target * 7 + (goods.data[i].isSelected ? 1 : 0) + Hash(goods.data[i].AssignedWarehouse);
                    }
                    return sig;
                }
                case BizManRules.HrManagers:
                {
                    HrManagerPlanUI plan = b.hrHrManagerPlanUI;
                    var staff = plan != null ? Get<EmployeesScrollerController>(plan.IsAssignEmployeesListOpen ? GameContract.HrAssignList : GameContract.HrStaff, plan) : null;
                    sig = sig * 31 + (plan != null && plan.IsAssignEmployeesListOpen ? 1 : 0);
                    for (int i = 0; staff != null && staff.data != null && i < staff.data.Count; i++)
                    {
                        sig = sig * 31 + staff.data[i].employeeId.GetHashCode() + (staff.data[i].assigned ? 1 : 0);
                    }
                    return sig;
                }
                default:
                    return sig;
            }
        }

        // The rows' widgets' states: a switch, a choice, a slider, a field or an amount changed by the game or the player.
        private long WidgetsSignature()
        {
            long sig = _widgets.Count;
            for (int i = 0; i < _widgets.Count; i++)
            {
                Component c = _widgets[i].Main;
                if (c == null)
                {
                    continue;
                }
                sig = sig * 31 + (c.gameObject.activeInHierarchy ? 1 : 0);
                switch (c)
                {
                    case Toggle t: sig = sig * 3 + (t.isOn ? 1 : 0) + (t.interactable ? 2 : 0); break;
                    case UI.Elements.Dropdown d: sig = sig * 31 + d.SelectedOptionIndex; break;
                    case Slider s: sig = sig * 31 + s.value.GetHashCode(); break;
                    case TMP_InputField f: sig = sig * 31 + Hash(f.text); break;
                    case AmountSelector a: sig = sig * 31 + a.Amount + (a.Interactable ? 1 : 0); break;
                    case Button bt: sig = sig * 3 + (bt.interactable ? 1 : 0); break;
                }
            }
            return sig;
        }
    }
}
