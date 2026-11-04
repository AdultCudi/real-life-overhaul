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
using Company = RealLifeOverhaul.Economy.CompanyDirector;
using InputField = UI.Components.InputField;
using PriceStance = RealLifeOverhaul.Economy.PriceStance;
using PriceStrategy = RealLifeOverhaul.Economy.PriceStrategy;
using PriceStrategyRules = RealLifeOverhaul.Economy.PriceStrategyRules;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A headquarters' managers of one kind, read from the game's own plan list and plan page after the game filled them:
    /// the plans as the game's reorderable list holds them (what each covers, who runs it, the chosen one), the chosen plan
    /// as a form of the game's own widgets, and its goods, its route's stops or its staff from the game's own tables. A plan
    /// with no manager shows the game's own question for one. Every press is the game's: the list's own entries and Add
    /// plan, its reorder through the game's ReorderableList, each widget and each row's own cell. Beside them, by the game's
    /// own figures: a pricing plan's shops, its next reprice, each product's cost, margin and stand against the rivals with
    /// a filter, the plan's margin and its shops' takings and profit yesterday; and its strategy, which the mod keeps and
    /// applies after the game's own reprice (Economy/CompanyDirector.PricePlans), with Reset to yesterday. A route's stops
    /// with their opening, each good in the shop against its target and what the next run takes, the route's last run and
    /// its goods out a day, and Send now, which runs the route's deliveries at once by the game's own plan. Cost: a look at
    /// a few counts four times a second; a layout only when they change.
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
        private readonly List<int> _priceRows = new List<int>(32);      // the goods' table as filtered: each row's place in the game's own list
        private readonly List<BuildingRegistration> _planShops = new List<BuildingRegistration>(8);
        private readonly List<LogisticsManagerPlanDestination> _sending = new List<LogisticsManagerPlanDestination>(8);
        private int _priceFilter;                   // all the plan's goods, those set by hand, or those over the rivals' lowest
        private int _priceView;                     // the chosen pricing plan's half: its prices (0), or its plan and strategy (1)
        private string _sent;                       // what Send now did, said over the stop's goods...
        private LogisticsManagerPlan _sentOn;       // ...of the route it was done on

        private const int PriceAll = 0, PriceChanged = 1, PriceAbove = 2;
        private const int RunHour = 2;              // the hour the game runs every route (GameManager: its hour 2)
        // The strategy's rows, each a row the mod answers itself (BizManScreen.Ops: ModWidget), and what a press of one does.
        private const int ModFollow = 1, ModStance = 2, ModMargin = 3, ModNinety = 4, ModCost = 5, ModStep = 6;
        private const int ModToggle = 1, ModOption = 100;

        // What each of a managers' page's actions does.
        private const int DoAddPlan = 60, DoPlanUp = 61, DoPlanDown = 62, DoDeletePlan = 63, DoSuggest = 64, DoShowFrom = 65, DoAddStop = 66, DoBuyEnd = 67,
            DoBuyCancel = 68, DoBuyUrgent = 69, DoBuyOrder = 70, DoHrAssign = 71, DoHrFill = 72, DoHrClear = 73, DoHrClose = 74, DoResetPrices = 75, DoSendNow = 76,
            DoAssignFirst = 77, DoHuntStart = 78;

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
            UI.Elements.Dropdown who = _noManager != null && _noManager.gameObject.activeInHierarchy ? _noManager.employeeDropdown : null;
            string[] people = who != null ? Options(who) : null;
            if (people != null && people.Length > 1 && !Held(who))
            {
                // A plan with nobody on it and people who could take it: the first of the game's own choices (its option
                // after Unassigned) is the page's one main action, chosen as a click on that option chooses them.
                var with = new List<UiActionModel>(o.Actions);
                with.Add(new UiActionModel { Label = UiText.Fill("reallife_ui_bizops_assign_named", "name", people[1]), Kind = UiButtonKind.Primary, Icon = UiIcon.People,
                    Key = UiAction.Alt, Arg = DoAssignFirst });
                o.Actions = with.ToArray();
            }
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
            int before = _rows.Count;
            if (ManagerRows(plan2.noManagerAssignedPopUp, "bizman_pricingmanagers_not_assigned", "bizman_pricingmanagers_not_assigned_description"))
            {
                return;
            }
            if (_priceView == 0 && _rows.Count > before)
            {
                _rows.RemoveRange(before, _rows.Count - before);   // the prices' half: the plan's manager is on its plan's row, the table takes the room
            }
            // The plan in two halves, as one switch over the form: its prices, the table with the whole height the page has
            // (the thing a player comes to set), or its neighbourhood, its figures and the company's strategy for it.
            o.FormTabs = new[] { UiText.T("reallife_ui_bizops_view_prices"), UiText.T("reallife_ui_bizops_view_plan") };
            _priceView = Mathf.Clamp(_priceView, 0, 1);
            o.FormTab = _priceView;
            o.FormLead = Loc(Get<TextLocalizationComponent>(GameContract.PriceOwned, list));
            PricingManagerProductsScrollerController goods = Get<PricingManagerProductsScrollerController>(GameContract.PriceGoods, plan2);
            PricingManagerPlan plan = ChosenPricePlan();
            if (_priceView == 1)
            {
                ChoiceLine(Get<UI.Elements.Dropdown>(GameContract.PriceDistrict, plan2), UiText.T("common_neighborhood"));
                PlanFacts(plan, goods);
                Strategy(plan);
            }
            var actions = new List<UiActionModel>(2);
            Company director = Company.Instance;
            if (plan != null && director != null && director.PricePlansOn)
            {
                actions.Add(new UiActionModel { Label = UiText.T("reallife_ui_bizops_reset_prices"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Back, Key = UiAction.Count,
                    Arg = DoResetPrices, Disabled = !director.PlanHasYesterday(plan.id) || !Company.PlanMine(plan) });
            }
            Button suggest = Get<Button>(GameContract.PriceSuggest, goods);
            if (suggest != null && suggest.gameObject.activeInHierarchy)
            {
                actions.Add(new UiActionModel { Label = UiText.T("bizman_pricingmanagers_apply_suggested_prices"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check,
                    Key = UiAction.Alt, Arg = DoSuggest, Disabled = !suggest.interactable });
            }
            o.Actions = actions.ToArray();
            o.Table = _priceView == 0 ? PriceTable(goods, o.Table, o) : null;   // under its head alone: its first column says Product
        }

        private PricingManagerPlan ChosenPricePlan()
        {
            int chosen = ChosenPlan();
            var entry = chosen >= 0 && chosen < _plans.Count ? _plans[chosen].GetComponent<PricingManagersPlanListEntry>() : null;
            return entry != null ? entry.Plan : null;
        }

        // The goods with what each costs (the game's wholesale price), the rivals' lowest and the game's suggestion, its price
        // (stepped on the chosen row, the game's current price and its new one in one column, as they are one figure), its
        // margin over the cost and how it stands against the rivals, filtered to all, those set by hand (the game's own
        // manual prices) or those over the rivals. Eight columns would leave a product's name no room beside the plans.
        private UiDataTableModel PriceTable(PricingManagerProductsScrollerController goods, UiDataTableModel old, BizOpsModel o)
        {
            var t = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = UiText.T("bizman_pricingmanagers_header_product"), Kind = UiCellKind.Tile, Sortable = true },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_cost"), Kind = UiCellKind.Money, Width = 88f },
                    new UiDataColumn { Title = UiText.T("bizman_pricingmanagers_header_rivals_prices"), Kind = UiCellKind.Money, Width = 104f, Sortable = true },
                    new UiDataColumn { Title = UiText.T("bizman_pricingmanagers_header_suggested_price"), Kind = UiCellKind.Money, Width = 152f, Sortable = true },
                    new UiDataColumn { Title = UiText.T("bizman_pricingmanagers_header_current_price"), Kind = UiCellKind.Stepper, Width = UiDataTable.StepperWidth, OnChosen = true,
                        Sortable = true },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_margin"), Kind = UiCellKind.Figure, Width = 80f },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_vs_rivals"), Kind = UiCellKind.Figure, Width = 88f },
                },
                EmptyIcon = UiIcon.Tag, EmptyTitle = UiText.T("bizman_pricingmanagers_no_products"), Offset = old != null ? old.Offset : 0f, Reveal = old != null ? old.Reveal : -1,
                SortColumn = _sortColumn, SortDown = goods != null && !goods.orderByAsc, SearchPlaceholder = UiText.T("reallife_ui_bizops_find_product"), Query = _tableQuery
            };
            int n = goods != null && goods.data != null ? goods.data.Count : 0;
            int changed = 0, above = 0;
            for (int i = 0; i < n; i++)
            {
                changed += ByHand(goods.data[i]) ? 1 : 0;
                above += OverRivals(goods.data[i]) ? 1 : 0;
            }
            o.Filters = new[]
            {
                UiText.Fill("reallife_ui_bizops_filter_all", "count", UiText.Whole(n)), UiText.Fill("reallife_ui_bizops_filter_changed", "count", UiText.Whole(changed)),
                UiText.Fill("reallife_ui_bizops_filter_above", "count", UiText.Whole(above))
            };
            _priceFilter = Mathf.Clamp(_priceFilter, PriceAll, PriceAbove);
            o.Filter = _priceFilter;
            _priceRows.Clear();
            for (int i = 0; i < n; i++)
            {
                PricingManagerProductModel p = goods.data[i];
                if ((_priceFilter == PriceAll || (_priceFilter == PriceChanged && ByHand(p)) || (_priceFilter == PriceAbove && OverRivals(p))) && Found(p.ProductName))
                {
                    _priceRows.Add(i);
                }
            }
            _good = _priceRows.Count == 0 ? -1 : Mathf.Clamp(_good, -1, _priceRows.Count - 1);
            t.Selected = _good;   // the product whose price is being set: its stepper on its row, the others' prices as figures
            t.Rows = new UiDataRow[_priceRows.Count];
            for (int r = 0; r < _priceRows.Count; r++)
            {
                PricingManagerProductModel p = goods.data[_priceRows[r]];
                float min = p.Suggestion.suggestedMin, max = p.Suggestion.suggestedMax;
                string suggested = Mathf.Approximately(min, max) ? max.ToCurrencyFormat() : UiText.Fill("reallife_ui_bizops_range", "from", min.ToCurrencyFormat(), "to", max.ToCurrencyFormat());
                int price = p.CurrentPrice >= 0f ? Mathf.RoundToInt(p.CurrentPrice * 100f) : 0;
                int cost = Company.WholesaleCents(p.Suggestion.itemName);
                int margin = PriceStrategyRules.MarginOf(price, cost);
                int against = PriceStrategyRules.AgainstRivals(price, p.RivalsPrice > 0f ? Mathf.RoundToInt(p.RivalsPrice * 100f) : 0);
                t.Rows[r] = new UiDataRow
                {
                    Cells = new[]
                    {
                        p.ProductName, cost > 0 ? (cost / 100f).ToCurrencyFormat() : "-", p.RivalsPrice >= 0f ? p.RivalsPrice.ToCurrencyFormat() : "-", suggested,
                        p.CurrentPrice >= 0f ? p.CurrentPrice.ToCurrencyFormat() : p.SuggestedPrice.ToCurrencyFormat(), margin != int.MinValue ? Percent(margin) : "-",
                        against != int.MinValue ? Signed(against) : "-"
                    },
                    Values = new[] { (int)UiIcon.Tag, 0, 0, 0, UiDataTable.CanLess | UiDataTable.CanMore, 0, 0 },
                    Pictures = new[] { p.Suggestion != null ? BuyPictures.Item(p.Suggestion.itemName) : null },   // the good's own picture, as its shelves show it
                    Tones = new[]
                    {
                        UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, margin != int.MinValue && margin < 0 ? UiRole.DangerText : UiRole.None,
                        against != int.MinValue && against > 0 ? UiRole.WarnText : UiRole.None
                    }
                };
            }
            t.RowCount = _priceRows.Count;
            t.EmptyAction = _priceRows.Count == 0 && !string.IsNullOrEmpty(_tableQuery) ? UiText.T("bizman_schedule_clear_filter") : null;
            return t;
        }

        private static bool ByHand(PricingManagerProductModel p) => p != null && p.Plan != null && p.Suggestion != null && p.Plan.IsManuallyPriced(p.Suggestion.itemName);

        private static bool OverRivals(PricingManagerProductModel p) => p != null && p.CurrentPrice > 0f && p.RivalsPrice > 0f && p.CurrentPrice > p.RivalsPrice + 0.005f;

        private int PriceRow(int row) => row >= 0 && row < _priceRows.Count ? _priceRows[row] : -1;

        // A whole per cent, and tenths of one with their sign, in the words of the language ("34%", "+3.2%").
        private static string Percent(int whole) => UiText.Fill("reallife_ui_bizops_percent", "count", UiText.Whole(whole));

        private static string Signed(int tenths) =>
            UiText.Fill("reallife_ui_bizops_percent", "count", (tenths > 0 ? "+" : tenths < 0 ? "-" : string.Empty) + BizOpsRules.Tenths(Math.Abs(tenths)));

        // The plan's shops (the player's in its neighbourhood that the game's pricing manager prices, as its own plan finds
        // them) and its next reprice; then how its prices stand today against their cost and the rivals, and what its shops
        // took and made yesterday against the day before, by the game's own daily statements.
        private void PlanFacts(PricingManagerPlan plan, PricingManagerProductsScrollerController goods)
        {
            GameInstance game = SaveGameManager.Current;
            if (plan == null || game == null)
            {
                return;
            }
            _planShops.Clear();
            List<BuildingRegistration> all = game.BuildingRegistrations;
            string names = string.Empty;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration r = all[i];
                if (r != null && GameRegistrations.District(r) == plan.supervisedNeighborhood && PricingManagerHelper.IsManageableBusiness(r))
                {
                    names += (_planShops.Count > 0 ? ", " : string.Empty) + r.BusinessName;
                    _planShops.Add(r);
                }
            }
            InfoLine(UiText.T("reallife_ui_bizops_shops"), _planShops.Count == 0 ? UiText.T("reallife_ui_bizops_shops_none")
                : UiText.Fill(_planShops.Count == 1 ? "reallife_ui_bizops_shops_one" : "reallife_ui_bizops_shops_line", "count", UiText.Whole(_planShops.Count), "names", names));
            Company director = Company.Instance;
            bool held = director != null && director.PricePlansOn && director.PlanStrategy(plan.id).Hold;
            InfoLine(UiText.T("reallife_ui_bizops_next_reprice"), held ? UiText.T("reallife_ui_bizops_reprice_held")
                : string.IsNullOrEmpty(plan.assignedEmployeeId) ? UiText.T("reallife_ui_bizops_reprice_none") : When(plan.nextUpdateDay, plan.nextUpdateHour));
            Heading(UiText.T("reallife_ui_bizops_plan_today"));
            long margins = 0, against = 0;
            int marginCount = 0, againstCount = 0;
            for (int i = 0; goods != null && goods.data != null && i < goods.data.Count; i++)
            {
                PricingManagerProductModel p = goods.data[i];
                int price = p.CurrentPrice > 0f ? Mathf.RoundToInt(p.CurrentPrice * 100f) : 0;
                int m = PriceStrategyRules.MarginOf(price, Company.WholesaleCents(p.Suggestion.itemName));
                int a = PriceStrategyRules.AgainstRivals(price, p.RivalsPrice > 0f ? Mathf.RoundToInt(p.RivalsPrice * 100f) : 0);
                margins += m != int.MinValue ? m : 0;
                marginCount += m != int.MinValue ? 1 : 0;
                against += a != int.MinValue ? a : 0;
                againstCount += a != int.MinValue ? 1 : 0;
            }
            InfoLine(UiText.T("reallife_ui_bizops_average_margin"), marginCount > 0 ? Percent((int)Math.Round((double)margins / marginCount)) : "-");
            int mean = againstCount > 0 ? (int)Math.Round((double)against / againstCount) : 0;
            InfoLine(UiText.T("reallife_ui_bizops_against_rivals"), againstCount == 0 ? "-" : mean == 0 ? UiText.T("reallife_ui_bizops_rivals_level")
                : UiText.Fill(mean > 0 ? "reallife_ui_bizops_rivals_over" : "reallife_ui_bizops_rivals_under", "share",
                    UiText.Fill("reallife_ui_bizops_percent", "count", BizOpsRules.Tenths(Math.Abs(mean)))), mean > 0 ? UiRole.WarnText : UiRole.None);
            int today = TimeHelper.CurrentDay;
            bool hasY = PlanBooks(game, today - 1, out float takingsY, out float profitY), hasB = PlanBooks(game, today - 2, out float takingsB, out float profitB);
            InfoLine(UiText.T("reallife_ui_bizops_takings_yesterday"), hasY ? AgainstDay(takingsY, takingsB, hasB) : UiText.T("reallife_ui_bizops_books_none"));
            InfoLine(UiText.T("reallife_ui_bizops_profit_yesterday"), hasY ? AgainstDay(profitY, profitB, hasB) : UiText.T("reallife_ui_bizops_books_none"),
                hasY && profitY < 0f ? UiRole.DangerText : UiRole.None);
        }

        // The plan's shops' takings and profit on a day, by the game's daily statement of each; false where it has none of them.
        private bool PlanBooks(GameInstance game, int day, out float takings, out float profit)
        {
            takings = profit = 0f;
            bool found = false;
            List<FinancialSummary> summaries = game.financialSummaries;
            for (int i = summaries != null ? summaries.Count - 1 : -1; i >= 0; i--)
            {
                FinancialSummary s = summaries[i];
                if (s == null || s.dayNumber != day || s.businessIncomeStatements == null)
                {
                    if (s != null && s.dayNumber < day)
                    {
                        break;   // the game keeps them oldest first
                    }
                    continue;
                }
                for (int b = 0; b < s.businessIncomeStatements.Count; b++)
                {
                    FinancialSummary.BusinessIncomeStatement st = s.businessIncomeStatements[b];
                    for (int k = 0; st != null && k < _planShops.Count; k++)
                    {
                        if (st.Address == _planShops[k].Address)
                        {
                            takings += Mathf.Max(0f, st.TotalSales);
                            profit += st.TotalProfit;
                            found = true;
                            break;
                        }
                    }
                }
            }
            return found;
        }

        // A day's figure and how it stands against the day before: "$2,140, up 4% on the day before".
        private static string AgainstDay(float now, float before, bool hasBefore)
        {
            string amount = now.ToCurrencyFormat();
            if (!hasBefore || Math.Abs(before) < 0.5f)
            {
                return amount;
            }
            int tenths = (int)Math.Round((now - before) * 1000.0 / Math.Abs(before));
            if (tenths == 0)
            {
                return UiText.Fill("reallife_ui_bizops_day_same", "amount", amount);
            }
            return UiText.Fill(tenths > 0 ? "reallife_ui_bizops_day_up" : "reallife_ui_bizops_day_down", "amount", amount, "share",
                UiText.Fill("reallife_ui_bizops_percent", "count", BizOpsRules.Tenths(Math.Abs(tenths))));
        }

        // A day of the game and an hour as words: "Today 06:00", "Tomorrow 06:00", "Day 214, 06:00".
        private static string When(int day, int hour)
        {
            int today = TimeHelper.CurrentDay;
            string at = hour.GetFormattedTime(0f);
            if (day < today || (day == today && hour <= TimeHelper.CurrentHour))
            {
                return UiText.T("reallife_ui_bizops_reprice_due");
            }
            return day == today ? UiText.Fill("reallife_ui_bizops_today_at", "hour", at) : day == today + 1 ? UiText.Fill("reallife_ui_bizops_tomorrow_at", "hour", at)
                : UiText.Fill("reallife_ui_bizops_day_at", "day", UiText.Whole(day), "hour", at);
        }

        // The plan's strategy, in the form's right column: whether the daily reprice runs, how the prices stand against the
        // rivals, the least margin, prices ending in .90, never under cost and the most a price moves in a day. Kept and
        // applied by the mod (Economy/CompanyDirector.PricePlans); another player's plan or shops are never priced.
        private void Strategy(PricingManagerPlan plan)
        {
            Company director = Company.Instance;
            if (plan == null || director == null || !director.PricePlansOn)
            {
                return;   // the company off, or at fault: the game's own reprice, with nothing of the mod's to set
            }
            PriceStrategy s = director.PlanStrategy(plan.id);
            bool off = !Company.PlanMine(plan), rules = off || s.Hold;
            _right = true;
            Heading(UiText.T("reallife_ui_bizops_strategy"));
            ModSwitch(ModFollow, UiText.T("reallife_ui_bizops_strategy_follow"), !s.Hold, off);
            var stances = new string[PriceStrategyRules.StanceKeys.Length];
            for (int i = 0; i < stances.Length; i++)
            {
                stances[i] = UiText.T(PriceStrategyRules.StanceKeys[i]);
            }
            ModChoice(ModStance, UiText.T("reallife_ui_bizops_strategy_stance"), stances, (int)s.Stance, rules);
            ModChoice(ModMargin, UiText.T("reallife_ui_bizops_strategy_margin"), Shares(PriceStrategyRules.Margins, "reallife_ui_bizops_margin_none"),
                Math.Max(0, Array.IndexOf(PriceStrategyRules.Margins, s.Margin)), rules);
            ModSwitch(ModNinety, UiText.T("reallife_ui_bizops_strategy_ninety"), s.Ninety, rules);
            ModSwitch(ModCost, UiText.T("reallife_ui_bizops_strategy_cost"), s.OverCost, rules);
            ModChoice(ModStep, UiText.T("reallife_ui_bizops_strategy_step"), Shares(PriceStrategyRules.Steps, "reallife_ui_bizops_step_none"),
                Math.Max(0, Array.IndexOf(PriceStrategyRules.Steps, s.Step)), rules);
            _right = false;
        }

        // A list of whole per cents as a choice's options, its first (none) in words.
        private static string[] Shares(int[] shares, string noneKey)
        {
            var words = new string[shares.Length];
            for (int i = 0; i < shares.Length; i++)
            {
                words[i] = shares[i] > 0 ? Percent(shares[i]) : UiText.T(noneKey);
            }
            return words;
        }

        // A press of one of the strategy's rows: the plan's strategy changed by it, and kept (and applied) by the company.
        private bool ModPress(int code, int what)
        {
            if (_shown == BizManRules.LogisticsManagers)
            {
                return TripPress(code, what);
            }
            PricingManagerPlan plan = _shown == BizManRules.PricingManagers ? ChosenPricePlan() : null;
            Company director = Company.Instance;
            if (plan == null || director == null || !director.PricePlansOn || !Company.PlanMine(plan))
            {
                return false;
            }
            PriceStrategy s = director.PlanStrategy(plan.id);
            switch (code)
            {
                case ModFollow: s.Hold = !s.Hold; break;
                case ModStance:
                    if (what < ModOption)
                    {
                        return false;
                    }
                    s.Stance = (PriceStance)Mathf.Clamp(what - ModOption, (int)PriceStance.Manager, (int)PriceStance.Premium);
                    break;
                case ModMargin:
                    if (what < ModOption)
                    {
                        return false;
                    }
                    s.Margin = PriceStrategyRules.Margins[Mathf.Clamp(what - ModOption, 0, PriceStrategyRules.Margins.Length - 1)];
                    break;
                case ModNinety: s.Ninety = !s.Ninety; break;
                case ModCost: s.OverCost = !s.OverCost; break;
                case ModStep:
                    if (what < ModOption)
                    {
                        return false;
                    }
                    s.Step = PriceStrategyRules.Steps[Mathf.Clamp(what - ModOption, 0, PriceStrategyRules.Steps.Length - 1)];
                    break;
                default: return false;
            }
            director.SetPlanStrategy(plan, s);
            UiDirector.Logger?.Info("BizMan (kit): a pricing plan's strategy changed from its page");
            return true;
        }

        // Reset to yesterday: the game's question box first, then the plan's prices put back by the company.
        private bool ResetPrices()
        {
            PricingManagerPlan plan = ChosenPricePlan();
            Company director = Company.Instance;
            if (plan == null || director == null || !director.PricePlansOn || !director.PlanHasYesterday(plan.id))
            {
                return false;
            }
            string id = plan.id;
            GameConfirm.Show("reallife_ui_bizops_reset_title", "reallife_ui_bizops_reset_body", () =>
            {
                PricingManagerPlan now = PricingManagerHelper.GetPlanFromId(id);
                if (now != null && Company.Instance != null)
                {
                    Company.Instance.ResetPlan(now);
                }
            }, null, "reallife_ui_bizops_reset_prices", null, true, false);
            return true;
        }

        private int _sortColumn = -1;   // the head last pressed on a table the game sorts by its own headers

        // The game's sort keys, the model's properties, by the table's columns; a column the game does not sort by has none.
        private static readonly string[] ManagedHeads = { "ProductName", null, "RivalsPrice", "SuggestedPrice", "CurrentPrice", null, null };

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
            LogisticsManagerPlan route = ChosenRoute();
            if (TripsShown)
            {
                TripLines(route);   // the route runs as delivery trips: its departures, vehicles and figures in place of the game's one run
            }
            else
            {
                RouteLog(route);
            }
            var actions = new List<UiActionModel>(3);
            if (route != null)
            {
                bool today = SentToday(route);   // once a day for each route: the button greys and says so until tomorrow
                actions.Add(new UiActionModel { Label = UiText.T(today ? "reallife_ui_bizops_sent_today" : "reallife_ui_bizops_send_now"), Kind = UiButtonKind.Secondary,
                    Icon = UiIcon.Truck, Key = UiAction.Count, Arg = DoSendNow, Disabled = today || !CanSend(route) });
            }
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
            o.Status = route != null && ReferenceEquals(route, _sentOn) ? _sent : null;
            Stops(plan2, o, route);
        }

        private LogisticsManagerPlan ChosenRoute()
        {
            int chosen = ChosenPlan();
            var entry = chosen >= 0 && chosen < _plans.Count ? _plans[chosen].GetComponent<LogisticsManagersPlanListEntry>() : null;
            return entry != null ? entry.Plan : null;
        }

        // The route's runs, by the game's own record of its warehouse's goods going out (it keeps the last 60 movements):
        // the day of the last, and the pieces out a day over the game's week, as its stock page sums them. The game runs a
        // route each night at 02:00 (GameManager); the record holds no stop, so a stop's own share is not known.
        private void RouteLog(LogisticsManagerPlan route)
        {
            var from = route != null && route.targetAddress != null && !route.targetAddress.IsUndefined()
                ? BuildingHelper.GetBuildingRegistration(route.targetAddress) as Entities.Warehouse : null;
            List<DeliveryTransaction> log = from != null ? from.deliveryTransactions : null;
            if (log == null)
            {
                return;
            }
            int today = TimeHelper.CurrentDay, last = -1, week = 0;
            for (int i = 0; i < log.Count; i++)
            {
                DeliveryTransaction t = log[i];
                int pieces = t != null ? Out(t) : 0;
                if (pieces <= 0)
                {
                    continue;
                }
                last = Math.Max(last, t.dayOfDelivery);
                week += t.dayOfDelivery >= today - 7 ? pieces : 0;
            }
            _right = true;   // under the warehouse, so the form stays two rows deep and the stop's goods keep their room
            InfoLine(UiText.T("reallife_ui_bizops_last_run"), last < 0 ? UiText.T("reallife_ui_bizops_runs_none") : last >= today ? UiText.T("reallife_ui_bizops_run_today")
                : last == today - 1 ? UiText.T("reallife_ui_bizops_run_yesterday") : UiText.Fill("reallife_ui_bizops_run_days_ago", "count", UiText.Whole(today - last)));
            InfoLine(UiText.T("reallife_ui_bizops_out_day"), BizOpsRules.PerDay(week));
            _right = false;
            NoteLine(UiText.Fill("reallife_ui_bizops_runs_at", "hour", RunHour.GetFormattedTime(0f)));
        }

        // The pieces a movement took out of the warehouse (the game writes those as negative amounts).
        private static int Out(DeliveryTransaction t)
        {
            int n = 0;
            for (int i = 0; t.deliveryItems != null && i < t.deliveryItems.Count; i++)
            {
                DeliveryItem item = t.deliveryItems[i];
                n += item != null && item.amountDelivered < 0 ? -item.amountDelivered : 0;
            }
            return n;
        }

        // Send now was used on this route today: it may be used once a game day for each route (Economy/SendNowLimit).
        private static bool SentToday(LogisticsManagerPlan route) => route != null && !Economy.SendNowLimit.Allowed(route.id, TimeHelper.CurrentDay);

        // Send now may run: a manager on the route, its warehouse the player's own, no stop another player's, at least one
        // stop it can serve, and no run of the game's under way (its nightly run goes through the same two queues).
        private static bool CanSend(LogisticsManagerPlan route)
        {
            if (route == null || string.IsNullOrEmpty(route.assignedEmployeeId) || route.targetAddress == null || route.targetAddress.IsUndefined()
                || route.destinations == null || route.destinations.Count == 0
                || LogisticsManagerHelper.WarehouseDeliveriesWork.HasPendingWork || LogisticsManagerHelper.FactoryDeliveriesWork.HasPendingWork)
            {
                return false;
            }
            BuildingRegistration from = BuildingHelper.GetBuildingRegistration(route.targetAddress);
            if (!(from is Entities.Warehouse) || !GoingPublic.Mine(from))
            {
                return false;
            }
            for (int i = 0; i < route.destinations.Count; i++)
            {
                Address to = route.destinations[i] != null ? route.destinations[i].deliveryTargetAddress : null;
                if (to != null && !to.IsUndefined() && GoingPublic.Foreign(BuildingHelper.GetBuildingRegistration(to)))
                {
                    return false;
                }
            }
            return route.MaxDestinations > 0;
        }

        // Send now: the game's question box first, then the route's deliveries at once as the game runs them each night
        // (LogisticsManagerHelper.ProgressDelivery): the stops its own plan gives, each delivered by the plan, then the game's
        // end of a run (its reports, an exporter's takings, its alerts). What it moved is said over the stop's goods.
        private bool SendNow()
        {
            LogisticsManagerPlan route = ChosenRoute();
            if (!CanSend(route) || SentToday(route))
            {
                return false;
            }
            if (TripsShown)
            {
                return SendTrips(route);
            }
            string id = route.id;
            GameConfirm.Show("reallife_ui_bizops_send_title", "reallife_ui_bizops_send_body", () => Sent(id), null, "reallife_ui_bizops_send_now", null, true, false);
            return true;
        }

        private void Sent(string id)
        {
            LogisticsManagerPlan route = null;
            List<LogisticsManagerPlan> plans = SaveGameManager.Current != null ? SaveGameManager.Current.logisticsManagerPlans : null;
            for (int i = 0; plans != null && i < plans.Count && route == null; i++)
            {
                route = plans[i] != null && plans[i].id == id ? plans[i] : null;
            }
            if (!CanSend(route) || SentToday(route))
            {
                return;   // a run of the game's began, or the route changed or was sent, while the question stood
            }
            var from = (Entities.Warehouse)BuildingHelper.GetBuildingRegistration(route.targetAddress);
            List<DeliveryTransaction> log = from.deliveryTransactions;
            DeliveryTransaction mark = log != null && log.Count > 0 ? log[log.Count - 1] : null;
            _sending.Clear();
            foreach (LogisticsManagerPlanDestination d in route.GetPlannedDeliveries())
            {
                _sending.Add(d);   // the plan's own list first: it starts a run afresh, and its stops are delivered after
            }
            for (int i = 0; i < _sending.Count; i++)
            {
                route.DeliverDestination(_sending[i]);
            }
            LogisticsManagerPlan.OnFinishedDeliveries();
            int sent = 0;
            for (int i = log != null ? (mark != null ? log.IndexOf(mark) + 1 : 0) : 0; log != null && i < log.Count; i++)
            {
                sent += log[i] != null ? Out(log[i]) : 0;
            }
            if (sent > 0)
            {
                Economy.SendNowLimit.Sent(id, TimeHelper.CurrentDay);   // the day is used only when something went out
            }
            _sentOn = route;
            _sent = sent > 0 ? UiText.Fill(_sending.Count == 1 ? "reallife_ui_bizops_sent_one" : "reallife_ui_bizops_sent", "count", UiText.Whole(sent), "stops", UiText.Whole(_sending.Count))
                : UiText.T("reallife_ui_bizops_sent_none");
            UiDirector.Logger?.Info("BizMan (kit): a route's deliveries run at once through the game's own plan: " + sent + " piece(s) to " + _sending.Count + " stop(s)");
            _sending.Clear();
        }

        // A stop's opening today, by its own schedule: open now, opening later at an hour, or closed for the day.
        private static string OpenWords(BuildingRegistration reg)
        {
            if (reg == null)
            {
                return null;
            }
            ScheduleDay today = BuildingHelper.GetTodaySchedule(reg);
            if (reg.temporarilyClosed || today == null || !today.isOpen)
            {
                return UiText.T("reallife_ui_bizops_closed_today");
            }
            if (BusinessHelper.IsBusinessOpen(reg))
            {
                return UiText.T("reallife_ui_bizops_open_now");
            }
            int hour = TimeHelper.CurrentHour, next = -1;
            for (int i = 0; today.openingHourSlots != null && i < today.openingHourSlots.Count; i++)
            {
                int start = today.openingHourSlots[i].startingHour;
                next = start > hour && (next < 0 || start < next) ? start : next;
            }
            return next >= 0 ? UiText.Fill("reallife_ui_bizops_opens_at", "hour", next.GetFormattedTime(0f)) : UiText.T("reallife_ui_bizops_closed_now");
        }

        // The tone of a place's opening on its stop's card: open now in the go colour, opening later in the waiting one,
        // closed quiet.
        private static UiRole OpenTone(BuildingRegistration reg)
        {
            if (reg == null)
            {
                return UiRole.Muted;
            }
            ScheduleDay today = BuildingHelper.GetTodaySchedule(reg);
            return reg.temporarilyClosed || today == null || !today.isOpen ? UiRole.Muted : BusinessHelper.IsBusinessOpen(reg) ? UiRole.Ok : UiRole.Warn;
        }

        // The route's stops: each the game's own destination row, its place as its own drop-down chose it, past the route's
        // limit where the game fades it; the stop chosen shows its goods as the big table, each amount the game's own row's.
        private void Stops(LogisticsManagerPlanUI plan, BizOpsModel o, LogisticsManagerPlan route)
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
            // The game lays a row for each of the route's stops, in their order (LogisticsManagerPlanUI): each row's place.
            List<LogisticsManagerPlanDestination> places = route != null && route.destinations != null && route.destinations.Count == _stops.Count ? route.destinations : null;
            for (int i = 0; i < _stops.Count; i++)
            {
                LogisticsManagerDestinationUI d = _stops[i];
                UI.Elements.Dropdown target = Get<UI.Elements.Dropdown>(GameContract.StopTarget, d);
                string[] options = Options(target);
                int pick = target != null ? target.SelectedOptionIndex : -1;
                CanvasGroup faded = d.GetComponent<CanvasGroup>();
                Button remove = Get<Button>(GameContract.StopRemove, d);
                int goods = GoodsOf(d, null);
                Address to = places != null && places[i] != null ? places[i].deliveryTargetAddress : null;
                string open = to != null && !to.IsUndefined() ? OpenWords(BuildingHelper.GetBuildingRegistration(to)) : null;
                o.Stops[i] = new BizOpsStop
                {
                    Name = pick >= 0 && pick < options.Length ? options[pick] : UiText.T("common_unassigned"),
                    Line = (Loc(Get<TextLocalizationComponent>(GameContract.StopName, d)) ?? string.Empty) + ", "
                        + (goods == 1 ? UiText.T("reallife_ui_bizops_goods_one") : UiText.Fill("reallife_ui_bizops_goods_count", "count", UiText.Whole(goods)))
                        + (open != null ? ", " + open : string.Empty),
                    Open = i == _stop, Warn = faded != null && faded.alpha < 0.99f, Removable = remove != null && remove.gameObject.activeInHierarchy,
                    Open2 = open, OpenTone = OpenTone(to != null && !to.IsUndefined() ? BuildingHelper.GetBuildingRegistration(to) : null)
                };
                StopGoods(ref o.Stops[i], places != null ? places[i] : null);
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
            LogisticsManagerPlanDestination stop = places != null ? places[_stop] : null;
            BuildingRegistration shop = stop != null && stop.deliveryTargetAddress != null && !stop.deliveryTargetAddress.IsUndefined()
                ? BuildingHelper.GetBuildingRegistration(stop.deliveryTargetAddress) : null;
            bool exporter = stop != null && stop.IsTargetExporter;   // an exporter takes its whole target each run (ProcessStockTarget)
            Address from = route != null && route.targetAddress != null && !route.targetAddress.IsUndefined() ? route.targetAddress : null;
            var t = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = UiText.T("common_product"), Kind = UiCellKind.Text },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_in_shop"), Kind = UiCellKind.Figure, Width = 96f },
                    new UiDataColumn { Title = UiText.T("bizman_logisticsmanagers_min_stock_amount"), Kind = UiCellKind.Stepper, Width = UiDataTable.StepperWidth, OnChosen = true },
                    new UiDataColumn { Title = UiText.T("bizman_logisticsmanagers_warehouse_stock"), Kind = UiCellKind.Figure, Width = 120f },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_takes"), Kind = UiCellKind.Figure, Width = 88f },
                    new UiDataColumn { Title = UiText.T("bizman_logisticsmanagers_runs_out_in"), Kind = UiCellKind.Text, Width = 128f },
                },
                EmptyIcon = UiIcon.Package, EmptyTitle = UiText.T("bizman_logisticsmanagers_no_products"), Offset = o.Table != null ? o.Table.Offset : 0f
            };
            t.Rows = new UiDataRow[_goods.Count];
            for (int i = 0; i < _goods.Count; i++)
            {
                Transform g = _goods[i];
                TextLocalizationComponent name = g.Find("NameRow/ProductName") != null ? g.Find("NameRow/ProductName").GetComponent<TextLocalizationComponent>() : null;
                InputField amount = g.Find("BusinessTarget/Amount") != null ? g.Find("BusinessTarget/Amount").GetComponent<InputField>() : null;
                string item = name != null ? name.Key : null;   // the game's row is named by the item's own key
                int target = Target(stop, item);
                int inShop = shop != null && !exporter && !string.IsNullOrEmpty(item) ? BuildingHelper.CountTotalResourcesInStock(shop.Address, item) : -1;
                int stock = from != null && !string.IsNullOrEmpty(item) ? BuildingHelper.CountResourcesInPallets(from, item) : -1;
                // What the next run takes: the target less what the shop holds (all of it for an exporter), at most the warehouse's stock.
                int need = target <= 0 ? 0 : exporter ? target : Math.Max(0, target - Math.Max(0, inShop));
                int takes = stock >= 0 ? Math.Min(need, stock) : need;
                t.Rows[i] = new UiDataRow
                {
                    Cells = new[]
                    {
                        Loc(name) ?? string.Empty, inShop >= 0 ? UiText.Whole(inShop) : "-", amount != null && amount.tmpInputField != null ? amount.tmpInputField.text : "0",
                        WordsAt(g, "WarehouseStock") ?? "-", target >= 0 ? UiText.Whole(takes) : "-", WordsAt(g, "RunsOutIn") ?? "-"
                    },
                    Values = new[] { 0, 0, UiDataTable.CanLess | UiDataTable.CanMore, 0, 0, 0 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, takes < need ? UiRole.WarnText : UiRole.None, UiRole.None }
                };
            }
            t.RowCount = _goods.Count;
            _good = _goods.Count == 0 ? -1 : Mathf.Clamp(_good, -1, _goods.Count - 1);
            t.Selected = _good;   // the good whose target is being set: its stepper on its row
            o.Table = t;
        }

        // A stop's card's goods: the first few of the stop's own targets (LogisticsManagerPlanDestination.stockTargets, what
        // the game's rows show), each its name, its target and its picture, and how many more there are.
        private static void StopGoods(ref BizOpsStop st, LogisticsManagerPlanDestination stop)
        {
            int n = stop != null && stop.stockTargets != null ? stop.stockTargets.Count : 0;
            int shown = Math.Min(n, StopCardGoods);
            st.Goods = new string[shown];
            st.Fills = new string[shown];
            st.Pictures = new string[shown];
            for (int k = 0; k < shown; k++)
            {
                var target = stop.stockTargets[k];
                string item = target != null ? target.itemName : null;
                st.Goods[k] = item != null ? UiText.T(item) : string.Empty;
                st.Fills[k] = target != null ? UiText.Whole(target.targetAmount) : "-";
                st.Pictures[k] = item != null ? BuyPictures.Item(item) : null;
            }
            st.More = n > shown ? UiText.Fill("reallife_ui_bizops_more_goods", "count", UiText.Whole(n - shown)) : null;
        }

        private const int StopCardGoods = 3;   // goods on a stop's card, as the page lays them

        // A good's target at a stop, by the stop's own targets (the game's row shows the same); -1 where it has none.
        private static int Target(LogisticsManagerPlanDestination stop, string item)
        {
            for (int i = 0; stop != null && stop.stockTargets != null && item != null && i < stop.stockTargets.Count; i++)
            {
                if (stop.stockTargets[i] != null && stop.stockTargets[i].itemName == item)
                {
                    return stop.stockTargets[i].targetAmount;
                }
            }
            return -1;
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
            Plans(Get<ReorderableList>(GameContract.BuyPlanOrder, list), null, UiText.T("bizman_menu_purchasing_agents"), UiText.T("reallife_ui_bizops_imports_none"),
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
                // Start recruiting is the page's one main action, at the form's foot, the game's own button pressed.
                Button start = ButtonOf(Get<GameObject>(GameContract.HuntStart, tab));
                if (start != null && start.gameObject.activeInHierarchy)
                {
                    var with = new List<UiActionModel>(o.Actions);
                    with.Add(new UiActionModel { Label = UiText.T("bizman_headhunters_start_recruiting_button"), Kind = UiButtonKind.Primary, Icon = UiIcon.Play, Key = UiAction.Alt,
                        Arg = DoHuntStart, Disabled = !start.interactable });
                    o.Actions = with.ToArray();
                }
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
                case BizManPages.OpsFilter:
                    if (_shown != BizManRules.PricingManagers)
                    {
                        return false;
                    }
                    _priceFilter = Mathf.Clamp(arg, PriceAll, PriceAbove);
                    _good = -1;
                    if (_m.Ops.Table != null)
                    {
                        _m.Ops.Table.Offset = 0f;
                    }
                    return true;
                case BizManPages.Table + UiDataTable.Head: return ManagersHead(b, arg);
                case BizManPages.Table + UiDataTable.Row: return ManagersRow(b, arg);
                case BizManPages.Table + UiDataTable.Less:
                case BizManPages.Table + UiDataTable.More:
                    return ManagersStep(b, arg, command == BizManPages.Table + UiDataTable.More);
                case BizManPages.Table + UiDataTable.Type: return true;
                case BizManPages.Table + UiDataTable.Search: return true;   // typing starts: the host opens the field
                case BizManPages.Table + UiDataTable.Clear:
                    _tableQuery = null;
                    return true;
                case BizManPages.OpsFormTab:
                    _priceView = Mathf.Clamp(arg, 0, 1);
                    _good = -1;
                    return true;
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
            _sent = null;
            _sentOn = null;
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
                case DoResetPrices: return ResetPrices();
                case DoSendNow: return SendNow();
                case DoAssignFirst: return _noManager != null && _noManager.gameObject.activeInHierarchy && Pick(_noManager.employeeDropdown, 1);
                case DoHuntStart:
                {
                    HeadhunterPlanUI hunt = Get<HeadhunterPlanUI>(GameContract.HuntPlan, PageOf<UI.Smartphone.Apps.BizMan.Headhunters.HeadhuntersPlanList>(b, BizManRules.Headhunters));
                    HeadhuntersRecruitingTab tab = hunt != null ? hunt.GetComponentInChildren<HeadhuntersRecruitingTab>(true) : null;
                    return Click(tab != null ? ButtonOf(Get<GameObject>(GameContract.HuntStart, tab)) : null, "start recruiting");
                }
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
            if (_shown != BizManRules.PricingManagers || column < 0 || column >= ManagedHeads.Length || ManagedHeads[column] == null)
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
                return Cell(goods, PriceRow(row)) is PricingManagerProductCellView cell && Click(Get<Button>(more ? GameContract.PricePlus : GameContract.PriceMinus, cell), "a managed price stepped");
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
                InputField f = Cell(goods, PriceRow(row)) is PricingManagerProductCellView cell ? Get<InputField>(GameContract.PriceInput, cell) : null;
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
            long sig = _stop * 7 + _good * 131 + _sortColumn + _priceFilter * 7919 + _priceView * 104729 + Hash(_sent) + Hash(_tableQuery) * 17;
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
                        sig = sig * 31 + goods.data[i].CurrentPrice.GetHashCode() + (ByHand(goods.data[i]) ? 1 : 0);
                    }
                    PricingManagerPlan plan = ChosenPricePlan();
                    Company director = Company.Instance;
                    if (plan != null)
                    {
                        sig = sig * 31 + plan.nextUpdateDay * 24 + plan.nextUpdateHour;
                        sig = sig * 31 + (director != null && director.PricePlansOn ? PriceStrategyRules.Pack(director.PlanStrategy(plan.id)) * 2 + (director.PlanHasYesterday(plan.id) ? 1 : 0) : -1);
                    }
                    return sig;
                }
                case BizManRules.LogisticsManagers:
                    // A shop's stock and its opening move with the clock, looked at a quarter of an hour at a time; a run of the game's.
                    sig = sig * 31 + _stops.Count + _goods.Count * 7 + TimeHelper.CurrentHour * 4 + (int)TimeHelper.CurrentMinute / 15;
                    sig = sig * 3 + (LogisticsManagerHelper.WarehouseDeliveriesWork.HasPendingWork || LogisticsManagerHelper.FactoryDeliveriesWork.HasPendingWork ? 1 : 0);
                    sig = sig * 31 + TripsSignature();
                    sig = sig * 3 + (SentToday(ChosenRoute()) ? 1 : 0);
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
