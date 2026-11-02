using System;
using System.Collections.Generic;
using Extensions;
using Helpers;
using Localizor.LanguageChangeEvent;
using TMPro;
using UI;
using UI.Smartphone.Apps.BizMan;
using UI.Smartphone.Apps.BizMan.Factory;
using UI.Smartphone.Apps.BizMan.Factory.Table;
using UI.Smartphone.Apps.BizMan.Warehouse;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A factory's machines and a warehouse's drivers and stock, read from the game's own pages after it filled them: the
    /// machine groups as the game lists them, the group's machines and the chosen machine's recipe from the game's own
    /// panel (its recipe, how much to make, its worker, its ingredients), or the group's production from its own table;
    /// each vehicle slot's driver as its own drop-down; the stock from the game's own table with its Sell all. Every press
    /// is the game's own control. Cost: a look at a few counts four times a second; a layout only when they change.
    /// </summary>
    public sealed partial class BizManScreen
    {
        private readonly List<BizManFactoryWorkstationGroupTemplate> _groups = new List<BizManFactoryWorkstationGroupTemplate>(8);
        private readonly List<BizManFactoryWorkstationTemplate> _machines = new List<BizManFactoryWorkstationTemplate>(16);
        private readonly List<DriverStation> _slots = new List<DriverStation>(8);
        private readonly List<Transform> _ingredients = new List<Transform>(8);

        private const int DoSellAll = 80, DoManageWorker = 81;

        // ------------------------------------------------------------------------------------------------- the factory

        private void Factory(BizManBusiness b)
        {
            _m.Part = BizManPart.Factory;
            RowsBegin();
            BizOpsModel o = _m.Ops;
            BizManFactory factory = PageOf<BizManFactory>(b, BizManRules.Factory);
            BizManFactoryMachineList list = factory != null ? factory.machineList : null;
            o.PlansEmpty = UiText.T("reallife_ui_bizops_machines_none");
            o.PlansEmptyBody = UiText.T("reallife_ui_bizops_machines_none_body");
            _groups.Clear();
            _machines.Clear();
            if (list != null)
            {
                list.GetComponentsInChildren(false, _groups);
            }
            BizManFactoryWorkstationGroupTemplate shown = list != null ? list.activeWorkstationGroupTemplate : null;
            if (shown == null && list != null && list.activeWorkstationTemplate != null)
            {
                shown = list.activeWorkstationTemplate.groupTemplate;
            }
            o.Tabs = new string[_groups.Count];
            for (int i = 0; i < _groups.Count; i++)
            {
                o.Tabs[i] = Loc(Get<TextLocalizationComponent>(GameContract.FactoryKind, _groups[i])) ?? UiText.T("bizman_factory_production_machines");
                o.Tab = _groups[i] == shown ? i : o.Tab;
            }
            if (_groups.Count == 0)
            {
                RowsEnd();
                o.Table = new UiDataTableModel();
                return;
            }
            BizManFactoryWorkstationGroupTemplate group = _groups[Mathf.Clamp(o.Tab, 0, _groups.Count - 1)];
            group.GetComponentsInChildren(true, _machines);
            for (int i = _machines.Count - 1; i >= 0; i--)
            {
                if (!_machines[i].gameObject.activeSelf || _machines[i].groupTemplate != group)
                {
                    _machines.RemoveAt(i);   // the group's template row, and another group's
                }
            }
            o.TableTitle = Words(Get<TMP_Text>(GameContract.FactoryActive, group)) ?? UiText.T("bizman_factory_production_machines");
            var t = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_machine"), Kind = UiCellKind.Tile },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_makes"), Kind = UiCellKind.Text, Width = 200f },
                },
                EmptyIcon = UiIcon.Factory, EmptyTitle = UiText.T("reallife_ui_bizops_machines_none"), Offset = o.Table != null ? o.Table.Offset : 0f
            };
            t.Rows = new UiDataRow[_machines.Count];
            for (int i = 0; i < _machines.Count; i++)
            {
                BizManFactoryWorkstationTemplate m = _machines[i];
                string name = Words(Get<TMP_Text>(GameContract.FactoryMachineName, m)) ?? UiText.T("bizman_factory_production_machines");
                t.Rows[i] = new UiDataRow
                {
                    Cells = new[] { name, Loc(Get<TextLocalizationComponent>(GameContract.FactoryMakes, m)) ?? "-" },
                    Values = new[] { (int)UiIcon.Factory, 0 }, Tones = new[] { UiRole.None, UiRole.None }, Sub = o.Tabs[o.Tab]
                };
                t.Selected = list.activeWorkstationTemplate == m ? i : t.Selected;
            }
            t.RowCount = _machines.Count;
            o.Table = t;
            if (factory.validWorkstationPanel != null && factory.validWorkstationPanel.gameObject.activeInHierarchy)
            {
                Recipe(factory.validWorkstationPanel, o);
            }
            else if (factory.invalidWorkstationPanel != null && factory.invalidWorkstationPanel.gameObject.activeInHierarchy)
            {
                TMP_InputField named = Get<TMP_InputField>(GameContract.FactoryName, factory.invalidWorkstationPanel);
                o.FormTitle = named != null && named.text.Length > 0 ? named.text : UiText.T("bizman_factory_production_machines");
                Heading(UiText.T("bizman_factory_inactive_reason_invalid_workstation"));
                NoteLine(UiText.T("reallife_ui_bizops_machine_missing"));
            }
            else if (factory.workstationGroupPanel != null && factory.workstationGroupPanel.gameObject.activeInHierarchy)
            {
                Production(factory.workstationGroupPanel, o);
            }
            else
            {
                o.FormTitle = UiText.T("reallife_ui_bizops_machine_choose");
                NoteLine(UiText.T("reallife_ui_bizops_machine_choose_body"));
            }
            RowsEnd();
        }

        // The chosen machine's own panel: its name, its recipe, how much to make (up to an amount, or without end), who
        // works it with the way to their page, what it makes an hour and its ingredients with the stock of each.
        private void Recipe(BizManFactoryValidWorkstationPanel panel, BizOpsModel o)
        {
            TMP_InputField name = Get<TMP_InputField>(GameContract.FactoryName, panel);
            o.FormTitle = name != null ? name.text : null;
            ChoiceLine(Get<UI.Elements.Dropdown>(GameContract.FactoryRecipe, panel), UiText.T("reallife_ui_bizops_recipe"));
            SwitchLine(Get<Toggle>(GameContract.FactoryUpTo, panel), UiText.T("bizman_factory_produce_up_to"));
            AmountLine(Get<AmountSelector>(GameContract.FactoryAmount, panel), UiText.T("reallife_ui_bizops_amount"));
            SwitchLine(Get<Toggle>(GameContract.FactoryAlways, panel), UiText.T("bizman_factory_produce_continuously"));
            TextLine(name, UiText.T("bizman_schedule_rename_workstation"), UiText.T("bizman_factory_workstation_name_placeholder"), false);
            _right = true;
            Heading(UiText.T("bizman_factory_production_flow_title"));
            InfoLine(UiText.T("reallife_ui_bizops_worker"), Words(Get<TMP_Text>(GameContract.FactoryWorker, panel)));
            ButtonLine(Get<Button>(GameContract.FactoryManage, panel), null, UiText.T("bizman_schedule_manage_employee_tooltip"), UiIcon.Person, false);
            InfoLine(UiText.T("reallife_ui_bizops_output"), Words(Get<TMP_Text>(GameContract.FactoryOutput, panel)));
            Transform template = Get<Transform>(GameContract.FactoryStock, panel);
            _ingredients.Clear();
            for (int i = 0; template != null && template.parent != null && i < template.parent.childCount; i++)
            {
                Transform c = template.parent.GetChild(i);
                if (c != template && c.gameObject.activeSelf && c.Find("IngredientValue") != null)
                {
                    _ingredients.Add(c);
                }
            }
            if (_ingredients.Count > 0)
            {
                Heading(UiText.T("bizman_factory_material_consumption_title"));
            }
            for (int i = 0; i < _ingredients.Count; i++)
            {
                TMP_Text stock = _ingredients[i].Find("StockValue") != null ? _ingredients[i].Find("StockValue").GetComponent<TMP_Text>() : null;
                string have = stock != null ? UiWidgets.Text(stock) : null;
                InfoLine(WordsAt(_ingredients[i], "IngredientValue"), have != null ? UiText.Fill("reallife_ui_bizops_in_stock", "count", have) : null,
                    stock != null && Tone(stock.color) == UiRole.Danger ? UiRole.DangerText : UiRole.None);
            }
            _right = false;
        }

        // A group's production, from the game's own table: each thing made, how many an hour, the stock and how long it lasts.
        private void Production(BizManFactoryWorkstationGroupPanel panel, BizOpsModel o)
        {
            o.FormTitle = UiText.T("bizman_factory_total_production_per_hour_title");
            BizManFactoryWorkstationGroupScrollerController table = panel.scroller;
            int n = table != null && table.data != null ? table.data.Count : 0;
            if (n == 0)
            {
                NoteLine(UiText.T("reallife_ui_bizops_production_none"));
            }
            for (int i = 0; i < n; i++)
            {
                BizManFactoryWorkstationGroupModel p = table.data[i];
                _right = i % 2 == 1;
                Heading(UiText.T(p.itemName));
                InfoLine(UiText.T("bizman_factory_group_table_produced_per_hour"), UiText.Whole(p.producedPerHour));
                InfoLine(UiText.T("bizman_factory_material_in_stock"), UiText.Whole(p.inStock));
                InfoLine(UiText.T("bizman_factory_material_runs_out_in"), p.runsOutInDays < 0 ? UiText.T("bizman_inventory_never_runs_out")
                    : UiText.Fill("bizman_inventory_product_days_until_empty", "days", UiText.Whole(p.runsOutInDays)));
            }
            _right = false;
        }

        // ------------------------------------------------------------------------------------------------- the warehouse

        // Each vehicle slot as the game's own driver station shows it: its number and vehicle, and who drives it as its own drop-down.
        private void Fleet(BizManBusiness b)
        {
            _m.Part = BizManPart.Drivers;
            RowsBegin();
            BizOpsModel o = _m.Ops;
            Drivers page = PageOf<Drivers>(b, BizManRules.Drivers);
            o.FormTitle = UiText.T("bizman_vehicle_slots");
            o.FormLead = UiText.T("bizman_drivers_vehicle_info");
            o.PlansEmpty = UiText.T("reallife_ui_bizops_slots_none");
            o.PlansEmptyBody = UiText.T("bizman_drivers_vehicle_info");
            _slots.Clear();
            Transform template = page != null && page.driverStationTemplate != null ? page.driverStationTemplate.transform : null;
            for (int i = 0; template != null && template.parent != null && i < template.parent.childCount; i++)
            {
                Transform c = template.parent.GetChild(i);
                if (c != template && c.gameObject.activeSelf && c.TryGetComponent(out DriverStation slot))
                {
                    _slots.Add(slot);
                }
            }
            for (int i = 0; i < _slots.Count; i++)
            {
                DriverStation s = _slots[i];
                GameObject none = Get<GameObject>(GameContract.DriverNoVehicle, s);
                string vehicle = On(none) ? UiText.T("bizman_warehouse_driverstation_no_vehicle_assigned") : Words(Get<TMP_Text>(GameContract.DriverVehicle, s));
                string slotWords = Loc(Get<TextLocalizationComponent>(GameContract.DriverSlot, s)) ?? UiText.Fill("bizman_drivers_slot_number", "number", UiText.Whole(i + 1));
                _right = _slots.Count > 4 && i >= (_slots.Count + 1) / 2;
                Heading(slotWords);
                InfoLine(UiText.T("bizman_drivers_vehicle"), vehicle);
                ChoiceLine(Get<UI.Elements.Dropdown>(GameContract.DriverChoice, s), UiText.T("bizman_drivers_driver"));
            }
            _right = false;
            RowsEnd();
        }

        // The stock as the game's own table lists it, how full the warehouse is over it, and its Sell all (its question first).
        private void Stock(BizManBusiness b)
        {
            _m.Part = BizManPart.Stock;
            RowsBegin();
            BizOpsModel o = _m.Ops;
            Inventory page = PageOf<Inventory>(b, BizManRules.Inventory);
            o.FormLead = Loc(Get<TextLocalizationComponent>(GameContract.StockBoxes, page));
            var actions = new List<UiActionModel>(1);
            Button sell = Get<Button>(GameContract.StockSell, page);
            if (sell != null && sell.gameObject.activeInHierarchy)
            {
                actions.Add(new UiActionModel { Label = UiText.T("bizman_inventory_sell_all"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Coin, Key = UiAction.Count, Arg = DoSellAll, Disabled = !sell.interactable });
            }
            o.Actions = actions.ToArray();
            WarehouseProductsScrollerController goods = page != null ? page.productsScrollerController : null;
            var t = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = UiText.T("common_product"), Kind = UiCellKind.Text },
                    new UiDataColumn { Title = UiText.T("bizman_inventory_in_stock"), Kind = UiCellKind.Figure, Width = 112f },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_boxes"), Kind = UiCellKind.Figure, Width = 88f },
                    new UiDataColumn { Title = UiText.T("bizman_inventory_import"), Kind = UiCellKind.Figure, Width = 112f },
                    new UiDataColumn { Title = UiText.T("bizman_inventory_shipped"), Kind = UiCellKind.Figure, Width = 112f },
                    new UiDataColumn { Title = UiText.T("bizman_inventory_balance"), Kind = UiCellKind.Figure, Width = 112f },
                    new UiDataColumn { Title = UiText.T("bizman_inventory_days_until_empty"), Kind = UiCellKind.Text, Width = 176f },
                },
                EmptyIcon = UiIcon.Package, EmptyTitle = UiText.T("reallife_ui_bizops_stock_none"), EmptyBody = UiText.T("reallife_ui_bizops_stock_none_body"),
                Offset = o.Table != null ? o.Table.Offset : 0f
            };
            int n = goods != null && goods.data != null ? goods.data.Count : 0;
            t.Rows = new UiDataRow[n];
            for (int i = 0; i < n; i++)
            {
                WarehouseProductModel p = goods.data[i];
                string days = p.DaysUntilEmpty < 0 ? UiText.T("bizman_inventory_run_out") : p.DaysUntilEmpty == int.MaxValue ? UiText.T("bizman_inventory_never_runs_out")
                    : UiText.Fill("bizman_inventory_product_days_until_empty", "days", UiText.Whole(p.DaysUntilEmpty));
                t.Rows[i] = new UiDataRow
                {
                    Cells = new[] { p.ProductName, UiText.Whole(p.Stock), UiText.Whole(p.CargoAmount), UiText.Whole(p.Deliveries), UiText.Whole(p.Consumption),
                        (p.Balance > 0 ? "+" : string.Empty) + UiText.Whole(p.Balance), days },
                    Values = new[] { 0, 0, 0, 0, 0, 0, 0 },
                    Tones = new[] { UiRole.None, p.Stock == 0 ? UiRole.DangerText : UiRole.None, UiRole.None, UiRole.None, UiRole.None,
                        p.Balance < 0 ? UiRole.DangerText : p.Balance > 0 ? UiRole.OkText : UiRole.None, p.DaysUntilEmpty >= 0 && p.DaysUntilEmpty < 3 ? UiRole.WarnText : UiRole.None }
                };
            }
            t.RowCount = n;
            o.Table = t;
            RowsEnd();
        }

        // ------------------------------------------------------------------------------------------------- presses

        private bool PressPlant(BizManBusiness b, int command, int arg)
        {
            switch (command)
            {
                case BizManPages.OpsTab:
                    // A machine group shown as the game's own group row does it, its panel of production with it.
                    if (arg < 0 || arg >= _groups.Count)
                    {
                        return false;
                    }
                    _groups[arg].ShowWorkstationGroupPanel();
                    return true;
                case BizManPages.Table + UiDataTable.Row:
                    if (_m.Part != BizManPart.Factory || arg < 0 || arg >= _machines.Count)
                    {
                        return false;
                    }
                    _machines[arg].ShowWorkstationPanel();   // what a click on the game's own machine row does
                    return true;
                case BizManPages.OpsAction:
                    if (arg == DoSellAll)
                    {
                        return Click(Get<Button>(GameContract.StockSell, PageOf<Inventory>(b, BizManRules.Inventory)), "sell all the stock (the game asks first)");
                    }
                    return false;
                default:
                    return PressRow(command, arg);
            }
        }

        // ------------------------------------------------------------------------------------------------- what changed

        private long PlantSignature(BizManBusiness b)
        {
            long sig = WidgetsSignature();
            switch (_shown)
            {
                case BizManRules.Factory:
                {
                    BizManFactory factory = PageOf<BizManFactory>(b, BizManRules.Factory);
                    BizManFactoryMachineList list = factory != null ? factory.machineList : null;
                    sig = sig * 31 + (list != null && list.activeWorkstationTemplate != null ? list.activeWorkstationTemplate.GetInstanceID() : 0);
                    sig = sig * 31 + (list != null && list.activeWorkstationGroupTemplate != null ? list.activeWorkstationGroupTemplate.GetInstanceID() : 0);
                    sig = sig * 31 + _groups.Count * 7 + _machines.Count + _ingredients.Count * 131;
                    for (int i = 0; i < _ingredients.Count; i++)
                    {
                        sig = sig * 31 + Hash(WordsAt(_ingredients[i], "StockValue"));
                    }
                    return sig;
                }
                case BizManRules.Drivers:
                    return sig * 31 + _slots.Count;
                default:
                {
                    Inventory page = PageOf<Inventory>(b, BizManRules.Inventory);
                    var goods = page != null ? page.productsScrollerController : null;
                    for (int i = 0; goods != null && goods.data != null && i < goods.data.Count; i++)
                    {
                        sig = sig * 31 + goods.data[i].Stock * 7 + goods.data[i].Balance;
                    }
                    return sig * 31 + Hash(Loc(Get<TextLocalizationComponent>(GameContract.StockBoxes, page)));
                }
            }
        }
    }
}
