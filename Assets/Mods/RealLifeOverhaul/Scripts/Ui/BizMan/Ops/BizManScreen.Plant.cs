using System;
using System.Collections.Generic;
using Entities;
using Extensions;
using Helpers;
using Localizor;
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
    /// is the game's own control. Over them, by the game's own figures: the machines' states with their filter, the output an
    /// hour of those running, what a machine holds against its limit and how long its ingredients last; each slot's vehicle
    /// and its load; each product's pieces in and out a day, how long it lasts, what it cost and is worth, and the shelves'
    /// room. Cost: a look at a few counts four times a second; a layout only when they change.
    /// </summary>
    public sealed partial class BizManScreen
    {
        private readonly List<BizManFactoryWorkstationGroupTemplate> _groups = new List<BizManFactoryWorkstationGroupTemplate>(8);
        private readonly List<BizManFactoryWorkstationTemplate> _machines = new List<BizManFactoryWorkstationTemplate>(16);
        private readonly List<DriverStation> _slots = new List<DriverStation>(8);
        private readonly List<Transform> _ingredients = new List<Transform>(8);
        private readonly List<int> _machineRows = new List<int>(16);     // the machines' table as filtered: each row's machine
        private readonly Dictionary<string, int> _cargoPieces = new Dictionary<string, int>(32);
        private readonly Dictionary<string, float> _cargoPaid = new Dictionary<string, float>(32);
        private readonly Dictionary<string, string> _productOf = new Dictionary<string, string>(32);   // a product's name as the game shows it: its item
        private int _machineFilter;                                      // the filter's choice shown: all, then each state some machine is in

        private const int DoSellAll = 80, DoManageWorker = 81;
        private const int MachineAll = 0, MachineRunning = 1, MachineNoWorker = 2, MachineNoInput = 3, MachineStopped = 4;
        private const string NoWorkerReason = "bizman_factory_inactive_reason_no_employee", NoInputReason = "bizman_factory_inactive_reason_not_enough_ingredients";

        // The game's reasons a machine stands (FactoryWorkstationInstance.GetInactiveReasonKeys), each as a short chip and its tone.
        private static readonly string[] StopReasons =
        {
            "bizman_factory_inactive_reason_invalid_workstation", "bizman_factory_inactive_reason_closed", NoWorkerReason, "bizman_factory_inactive_reason_no_space",
            NoInputReason, "bizman_factory_inactive_reason_production_limit"
        };
        private static readonly string[] StopChips =
        {
            "reallife_ui_bizops_chip_missing", "reallife_ui_bizops_chip_closed", "reallife_ui_bizops_chip_no_worker", "reallife_ui_bizops_chip_full",
            "reallife_ui_bizops_chip_no_input", "reallife_ui_bizops_chip_limit"
        };
        private static readonly UiRole[] StopTones = { UiRole.Danger, UiRole.Muted, UiRole.Danger, UiRole.Warn, UiRole.Warn, UiRole.Muted };

        // ------------------------------------------------------------------------------------------------- the factory

        private void Factory(BizManBusiness b)
        {
            if (Lacks(FactoryCore))
            {
                Handover(b);
                return;
            }
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
                Columns = BizOpsRules.MachineColumns(UiText.T("reallife_ui_bizops_col_machine"), UiText.T("reallife_ui_bizops_col_makes"), UiText.T("reallife_ui_bizops_worker"),
                    UiText.T("reallife_ui_bizops_col_per_hour"), UiText.T("reallife_ui_bizops_col_state")),
                EmptyIcon = UiIcon.Factory, EmptyTitle = UiText.T("reallife_ui_bizops_machines_none"), Offset = o.Table != null ? o.Table.Offset : 0f
            };
            BuildingRegistration reg = b.buildingRegistration;
            var rows = new UiDataRow[_machines.Count];
            var kinds = new int[_machines.Count];
            int[] counts = new int[MachineStopped + 1];
            int output = 0;
            for (int i = 0; i < _machines.Count; i++)
            {
                rows[i] = MachineRow(_machines[i], reg, out kinds[i], out int perHour);
                counts[kinds[i]]++;
                output += kinds[i] == MachineRunning ? perHour : 0;
            }
            // The filter: all of them, then each state some machine is in, with its count.
            var filters = new List<string>(4) { UiText.Fill("reallife_ui_bizops_filter_all", "count", UiText.Whole(_machines.Count)) };
            var filterKinds = new List<int>(4) { MachineAll };
            AddFilter(filters, filterKinds, counts, MachineRunning, "reallife_ui_bizops_filter_running");
            AddFilter(filters, filterKinds, counts, MachineNoWorker, "reallife_ui_bizops_filter_no_worker");
            AddFilter(filters, filterKinds, counts, MachineNoInput, "reallife_ui_bizops_filter_no_input");
            _machineFilter = Mathf.Clamp(_machineFilter, 0, filters.Count - 1);
            int kind = filterKinds[_machineFilter];
            o.Filters = filters.Count > 1 ? filters.ToArray() : new string[0];
            o.Filter = _machineFilter;
            _machineRows.Clear();
            for (int i = 0; i < _machines.Count; i++)
            {
                if (kind == MachineAll || kinds[i] == kind)
                {
                    t.Selected = list.activeWorkstationTemplate == _machines[i] ? _machineRows.Count : t.Selected;
                    _machineRows.Add(i);
                }
            }
            t.Rows = new UiDataRow[_machineRows.Count];
            for (int r = 0; r < _machineRows.Count; r++)
            {
                t.Rows[r] = rows[_machineRows[r]];
            }
            t.RowCount = _machineRows.Count;
            BizOpsRules.FitChips(t);
            o.Table = t;
            o.TableLead = output <= 0 ? UiText.T("reallife_ui_bizops_output_none")
                : output == 1 ? UiText.T("reallife_ui_bizops_output_one") : UiText.Fill("reallife_ui_bizops_output_total", "count", UiText.Whole(output));
            if (factory.validWorkstationPanel != null && factory.validWorkstationPanel.gameObject.activeInHierarchy)
            {
                Recipe(factory.validWorkstationPanel, o, list.activeWorkstationTemplate != null
                    ? Get<FactoryWorkstationInstance>(GameContract.FactoryMachineItem, list.activeWorkstationTemplate) : null, reg);
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

        private static void AddFilter(List<string> filters, List<int> kinds, int[] counts, int kind, string key)
        {
            if (counts[kind] > 0)
            {
                filters.Add(UiText.Fill(key, "count", UiText.Whole(counts[kind])));
                kinds.Add(kind);
            }
        }

        // A machine as the game's own row shows it (its name, what it makes, running or why not) with who works it this hour
        // and what they make of it an hour, both by the game's own sums (the same its machine panel shows for the one chosen);
        // its state by the game's own reasons: running, nobody on it this hour, short of an ingredient, or stopped otherwise.
        private static UiDataRow MachineRow(BizManFactoryWorkstationTemplate m, BuildingRegistration reg, out int kind, out int perHour)
        {
            string name = Words(Get<TMP_Text>(GameContract.FactoryMachineName, m)) ?? UiText.T("bizman_factory_production_machines");
            string makes = Loc(Get<TextLocalizationComponent>(GameContract.FactoryMakes, m));
            object on = GameContract.FactoryMachineOn.Field != null ? GameContract.FactoryMachineOn.Field.GetValue(Get<UI.Components.IconSwapper>(GameContract.FactoryMachineState, m)) : null;
            bool running = !(on is bool b) || b;
            Tooltip.ListTooltip why = Get<Tooltip.ListTooltip>(GameContract.FactoryMachineWhy, m);
            var item = Get<FactoryWorkstationInstance>(GameContract.FactoryMachineItem, m);
            EmployeeInstance worker = item != null && reg != null ? EmployeeHelper.GetEmployeeAtStationAndHour(reg, item.id) : null;
            string who = worker != null && worker.characterData != null ? worker.characterData.name : UiText.T("bizman_factory_no_employee_assigned");
            float skill = worker != null ? worker.GetSkillValue("ba:skill_factoryworker") : 0f;
            perHour = 0;
            if (worker != null && item != null && item.SelectedRecipe != null && !string.IsNullOrEmpty(item.SelectedRecipe.output.item))
            {
                perHour = item.SelectedRecipe.GetScaledOutputAmount(skill);
            }
            kind = running ? MachineRunning : MachineStopped;
            List<string> reasons = !running && item != null && reg != null ? item.GetInactiveReasonKeys(reg) : null;
            string chip = running ? UiText.T("bizman_factory_running") : UiText.T("bizman_factory_not_running");
            UiRole tone = running ? UiRole.Ok : UiRole.Warn;
            if (reasons != null)
            {
                kind = reasons.Contains(NoWorkerReason) ? MachineNoWorker : reasons.Contains(NoInputReason) ? MachineNoInput : MachineStopped;
                // The game's first reason as a short chip: what stops the machine, in the colour of how much it needs the player.
                string first = reasons.Count > 0 ? reasons[0] : null;
                int at = Array.IndexOf(StopReasons, first);
                if (at >= 0)
                {
                    chip = UiText.T(StopChips[at]);
                    tone = StopTones[at];
                }
            }
            else if (!running && why != null && why.list != null && why.list.Count > 0)
            {
                chip = why.list[0];
            }
            string kindName = item != null && !string.IsNullOrEmpty(item.itemName) ? UiText.T(item.itemName) : null;
            return new UiDataRow
            {
                Cells = new[] { name, makes ?? "-", who, perHour > 0 ? UiText.Whole(perHour) : "-", chip },
                Values = new[] { (int)UiIcon.Factory, 0, 0, 0, 0 },
                Tones = new[] { running ? UiRole.None : UiRole.Warn, UiRole.None, UiRole.None, UiRole.None, tone },
                Subs = new[]
                {
                    kindName != null && kindName != name ? kindName : null, null,
                    worker != null ? UiText.Fill("reallife_ui_bizops_skill_line", "skill", UiText.T("ba:skill_factoryworker"), "level", UiText.Whole(Mathf.FloorToInt(skill)))
                        : null,
                    null, null
                },
                Pictures = new[] { item != null ? BuyPictures.Item(item.itemName) : null, null, null, null, null },
                Hue = worker != null ? UiColour.HueOf(who) : 0f, Ring = worker != null ? StaffViews.Mood((int)worker.satisfaction) : UiRole.None
            };
        }

        // The chosen machine's own panel: its name, its recipe, how much to make (up to an amount, or without end), who
        // works it with the way to their page, what it makes an hour and its ingredients with the stock of each.
        private void Recipe(BizManFactoryValidWorkstationPanel panel, BizOpsModel o, FactoryWorkstationInstance item, BuildingRegistration reg)
        {
            TMP_InputField name = Get<TMP_InputField>(GameContract.FactoryName, panel);
            o.FormTitle = name != null ? name.text : null;
            ChoiceLine(Get<UI.Elements.Dropdown>(GameContract.FactoryRecipe, panel), UiText.T("reallife_ui_bizops_recipe"));
            SwitchLine(Get<Toggle>(GameContract.FactoryUpTo, panel), UiText.T("bizman_factory_produce_up_to"));
            AmountLine(Get<AmountSelector>(GameContract.FactoryAmount, panel), UiText.T("reallife_ui_bizops_amount"));
            var made = item != null ? item.SelectedRecipe : null;
            if (made != null && reg != null && item.produceUpTo && !string.IsNullOrEmpty(made.output.item))
            {
                // The game stops the machine once the factory's shelves hold this many of what it makes (TryApplyProduceUpToCap).
                int have = BuildingHelper.CountResourcesInPallets(reg.Address, made.output.item);
                InfoLine(UiText.T("reallife_ui_bizops_held_now"), UiText.Fill("reallife_ui_bizops_of", "count", UiText.Whole(have), "most", UiText.Whole(item.produceUpToValue)),
                    have >= item.produceUpToValue ? UiRole.WarnText : UiRole.None);
            }
            SwitchLine(Get<Toggle>(GameContract.FactoryAlways, panel), UiText.T("bizman_factory_produce_continuously"));
            SpoiledShare(reg);   // food spoilage: the factory's goods that spoiled over the week
            TextLine(name, UiText.T("bizman_schedule_rename_workstation"), UiText.T("bizman_factory_workstation_name_placeholder"), false);
            _right = true;
            Heading(UiText.T("bizman_factory_production_flow_title"));
            InfoLine(UiText.T("reallife_ui_bizops_worker"), Words(Get<TMP_Text>(GameContract.FactoryWorker, panel)));
            ButtonLine(Get<Button>(GameContract.FactoryManage, panel), null, UiText.T("bizman_schedule_manage_employee_tooltip"), UiIcon.Person, false);
            InfoLine(UiText.T("reallife_ui_bizops_output"), Words(Get<TMP_Text>(GameContract.FactoryOutput, panel)));
            EmployeeInstance worker = item != null && reg != null ? EmployeeHelper.GetEmployeeAtStationAndHour(reg, item.id) : null;
            int perHour = worker != null && made != null ? made.GetScaledOutputAmount(worker.GetSkillValue("ba:skill_factoryworker")) : 0;
            if (perHour > 0)
            {
                InfoLine(UiText.T("reallife_ui_bizops_pace"), Pace(perHour));
            }
            if (item != null && made != null && reg != null && perHour > 0)
            {
                Today(item, made, reg, perHour);
            }
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
            int hoursLeft = int.MaxValue;
            for (int i = 0; i < _ingredients.Count; i++)
            {
                TMP_Text stock = _ingredients[i].Find("StockValue") != null ? _ingredients[i].Find("StockValue").GetComponent<TMP_Text>() : null;
                string have = stock != null ? UiWidgets.Text(stock) : null;
                // The game lists the recipe's ingredients in the recipe's own order, each what an hour of this machine's work
                // takes at its full pace (FactoryBusinessSimulator.PlanIngredientsForRatio).
                bool listed = made != null && made.ingredients != null && i < made.ingredients.Count;
                int amount = listed ? made.ingredients[i].amount : 0;
                int count = listed && reg != null ? BuildingHelper.CountResourcesInPallets(reg.Address, made.ingredients[i].item) : -1;
                bool empty = stock != null && Tone(stock.color) == UiRole.Danger;
                bool shortHour = amount > 0 && count >= 0 && count < amount;
                if (amount > 0 && count >= 0)
                {
                    hoursLeft = Math.Min(hoursLeft, count / amount);
                }
                InfoLine(WordsAt(_ingredients[i], "IngredientValue"), have != null ? UiText.Fill("reallife_ui_bizops_in_stock", "count", have) : null,
                    empty ? UiRole.DangerText : shortHour ? UiRole.WarnText : UiRole.None);
            }
            if (hoursLeft > 0 && hoursLeft != int.MaxValue && _ingredients.Count > 0)   // none left: the ingredient's own line says so in the danger's colour
            {
                NoteLine(UiText.Fill(hoursLeft == 1 ? "reallife_ui_bizops_input_hour" : "reallife_ui_bizops_input_hours", "count", UiText.Whole(hoursLeft)));
            }
            _right = false;
        }

        // What this table made today: the game keeps no count, so the hours somebody stood at it since 06:00 (and this hour, if it is
        // running) at its pace an hour, what that is worth at the game's wholesale prices and what its ingredients cost at theirs.
        // Said as "about", since a past hour is counted by its worker, not by what was on the shelves then.
        private static readonly bool[] Worked = new bool[BizOpsRules.Hours];

        private void Today(FactoryWorkstationInstance item, BigAmbitions.Factories.Recipes.Recipe made, BuildingRegistration reg, int perHour)
        {
            int now = SaveGameManager.Current != null ? SaveGameManager.Current.Hour : 0;
            if (now < BizOpsRules.TodayFrom)
            {
                InfoLine(UiText.T("reallife_ui_bizops_today"), UiText.T("reallife_ui_bizops_today_not_yet"));
                return;
            }
            for (int h = 0; h < Worked.Length; h++)
            {
                Worked[h] = h >= BizOpsRules.TodayFrom && h < now && EmployeeHelper.GetEmployeeAtStationAndHour(reg, item.id, h) != null;
            }
            int hours = BizOpsRules.TodayHours(Worked, now, item.IsWorkstationActive(reg));
            int pieces = BizOpsRules.TodayMade(hours, perHour);
            BigAmbitions.Items.Item output = BigAmbitions.Items.ItemsGetter.GetByName(made.output.item);
            long worth = BizOpsRules.TodayWorth(pieces, output != null ? output.wholesalePrice : 0f);
            InfoLine(UiText.T("reallife_ui_bizops_today"), UiText.Fill("reallife_ui_bizops_today_line", "count", UiText.Whole(pieces), "amount", ((float)worth).ToCurrencyFormat()));
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
            if (Lacks(FleetCore))
            {
                Handover(b);
                return;
            }
            _m.Part = BizManPart.Drivers;
            RowsBegin();
            BizOpsModel o = _m.Ops;
            Drivers page = PageOf<Drivers>(b, BizManRules.Drivers);
            o.FormTitle = UiText.T("bizman_vehicle_slots");
            o.FormLead = UiText.T("bizman_drivers_vehicle_info");
            o.PlansEmpty = UiText.T("reallife_ui_bizops_slots_none");
            o.PlansEmptyBody = UiText.T("bizman_drivers_vehicle_info");
            _slots.Clear();
            var warehouse = b.buildingRegistration as Entities.Warehouse;
            Transform template = page != null && page.driverStationTemplate != null ? page.driverStationTemplate.transform : null;
            for (int i = 0; template != null && template.parent != null && i < template.parent.childCount; i++)
            {
                Transform c = template.parent.GetChild(i);
                if (c != template && c.gameObject.activeSelf && c.TryGetComponent(out DriverStation slot))
                {
                    _slots.Add(slot);
                }
            }
            // The game lays a driver station for each of the warehouse's vehicle slots, in their order (Drivers.Start).
            List<VehicleSlot> slots = warehouse != null && warehouse.vehicleSlots != null && warehouse.vehicleSlots.Count == _slots.Count ? warehouse.vehicleSlots : null;
            int loaded = 0;
            var cards = new List<BizOpsSlot>(_slots.Count);
            for (int i = 0; i < _slots.Count; i++)
            {
                DriverStation s = _slots[i];
                GameObject none = Get<GameObject>(GameContract.DriverNoVehicle, s);
                string vehicle = On(none) ? UiText.T("bizman_warehouse_driverstation_no_vehicle_assigned") : Words(Get<TMP_Text>(GameContract.DriverVehicle, s));
                string slotWords = Loc(Get<TextLocalizationComponent>(GameContract.DriverSlot, s)) ?? UiText.Fill("bizman_drivers_slot_number", "number", UiText.Whole(i + 1));
                _right = _slots.Count > 4 && i >= (_slots.Count + 1) / 2;
                Heading(slotWords);
                InfoLine(UiText.T("bizman_drivers_vehicle"), vehicle);
                VehicleSlot slot = slots != null ? slots[i] : null;
                VehicleInstance v = slot != null && !On(none) && !string.IsNullOrEmpty(slot.vehicleInstanceId) ? Vehicle(slot.vehicleInstanceId) : null;
                // A slot's vehicle on a delivery trip (Economy/LogisticsDirector) is on route until it is back; otherwise it is parked at
                // the warehouse, or away where the player last left it (its own street data); a slot without one is empty.
                bool home = v != null && warehouse != null && warehouse.Address != null && v.IsAtAddress(warehouse.Address);
                string trip = v != null && warehouse != null ? SlotTrip(warehouse, i) : null;
                string where = trip ?? UiText.T(v == null ? "reallife_ui_bizops_empty" : home ? "reallife_ui_bizops_parked" : "reallife_ui_bizops_away");
                InfoLine(UiText.T("reallife_ui_bizops_bay"), where, trip != null || home ? UiRole.OkText : UiRole.None);
                string load = null;
                if (v != null && v.VehicleType != null && v.VehicleType.maxCargoCapacity > 0)
                {
                    int boxes = v.cargoInstances != null ? v.cargoInstances.Count : 0;
                    loaded += boxes > 0 ? 1 : 0;
                    load = UiText.Fill("reallife_ui_bizops_of_boxes", "count", UiText.Whole(boxes), "most", UiText.Whole(v.VehicleType.maxCargoCapacity));
                    InfoLine(UiText.T("reallife_ui_bizops_load"), load);
                }
                int choice = _rows.Count;
                ChoiceLine(Get<UI.Elements.Dropdown>(GameContract.DriverChoice, s), UiText.T("bizman_drivers_driver"));
                // The same slot as a card: its vehicle and load, its driver (the choice row just laid, the same target), and
                // where it is as a chip; on a trip in the go colour, parked in the calm one, away or empty quiet.
                bool driverRow = _rows.Count > choice && _rows[choice].Kind == BizOpsRowKind.Choice;
                cards.Add(new BizOpsSlot
                {
                    Title = v != null ? slotWords + ", " + vehicle : slotWords, Line = load ?? (v != null ? vehicle : UiText.T("bizman_warehouse_driverstation_no_vehicle_assigned")),
                    Status = where, Tone = trip != null ? UiRole.Ok : home ? UiRole.Notice : UiRole.Muted, Vehicle = v != null,
                    Driver = driverRow ? _rows[choice].Value : null, DriverArg = driverRow ? _rows[choice].Arg : -1, DriverDisabled = driverRow && _rows[choice].Disabled
                });
            }
            o.Slots = cards.ToArray();
            _right = false;
            if (_slots.Count > 0)
            {
                o.FormLead = UiText.Fill(_slots.Count == 1 ? "reallife_ui_bizops_slots_one" : "reallife_ui_bizops_slots_line", "count", UiText.Whole(_slots.Count),
                    "loaded", UiText.Whole(loaded)) + " " + UiText.T("bizman_drivers_vehicle_info");
            }
            RowsEnd();
        }

        // The stock as the game's own table lists it, how full the warehouse is over it, and its Sell all (its question first).
        private void Stock(BizManBusiness b)
        {
            if (Lacks(StockCore))
            {
                Handover(b);
                return;
            }
            _m.Part = BizManPart.Stock;
            RowsBegin();
            BizOpsModel o = _m.Ops;
            Inventory page = PageOf<Inventory>(b, BizManRules.Inventory);
            // The game's own boxes label says what the shelf space line under the table says, so it is not repeated over it.
            var actions = new List<UiActionModel>(1);
            Button sell = Get<Button>(GameContract.StockSell, page);
            if (sell != null && sell.gameObject.activeInHierarchy)
            {
                actions.Add(new UiActionModel { Label = UiText.T("bizman_inventory_sell_all"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Coin, Key = UiAction.Count,
                    Arg = DoSellAll, Disabled = !sell.interactable });
            }
            o.Actions = actions.ToArray();
            WarehouseProductsScrollerController goods = page != null ? page.productsScrollerController : null;
            var warehouse = b.buildingRegistration as Entities.Warehouse;
            int used = 0, room = 0;
            float worth = 0f;
            Cargo(warehouse, ref used, ref room);
            var t = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = UiText.T("common_product"), Kind = UiCellKind.Tile },
                    new UiDataColumn { Title = UiText.T("bizman_inventory_in_stock"), Kind = UiCellKind.Figure, Width = 104f },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_boxes"), Kind = UiCellKind.Figure, Width = 80f },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_in_day"), Kind = UiCellKind.Figure, Width = 104f },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_out_day"), Kind = UiCellKind.Figure, Width = 104f },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_lasts"), Kind = UiCellKind.Chip, Width = 136f },
                    new UiDataColumn { Title = UiText.T("reallife_spoil_col"), Kind = UiCellKind.Text, Width = 112f },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_cost_each"), Kind = UiCellKind.Money, Width = 112f },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_value"), Kind = UiCellKind.Money, Width = 120f },
                },
                EmptyIcon = UiIcon.Package, EmptyTitle = UiText.T("reallife_ui_bizops_stock_none"), EmptyBody = UiText.T("reallife_ui_bizops_stock_none_body"),
                Offset = o.Table != null ? o.Table.Offset : 0f, SearchPlaceholder = UiText.T("reallife_ui_bizops_find_good"), Query = _tableQuery
            };
            int n = goods != null && goods.data != null ? goods.data.Count : 0;
            // The game's rows hold a product's name as it shows it (WarehouseProductModel: the item's own localisation): the
            // warehouse's products by that name give each row its item.
            _productOf.Clear();
            if (warehouse != null)
            {
                foreach (string product in warehouse.GetProducts())
                {
                    _productOf[product.GetLocalization()] = product;
                }
            }
            var rows = new List<UiDataRow>(n);
            for (int i = 0; i < n; i++)
            {
                WarehouseProductModel p = goods.data[i];
                // In and out a day are the game's own seven days of deliveries in and shipments out (Warehouse.GetProductDeliveries, GetProductConsumption).
                string days = p.DaysUntilEmpty < 0 ? UiText.T("bizman_inventory_run_out") : p.DaysUntilEmpty == int.MaxValue ? UiText.T("bizman_inventory_never_runs_out")
                    : p.DaysUntilEmpty == 1 ? UiText.T("reallife_ui_bizops_one_day") : UiText.Fill("bizman_inventory_product_days_until_empty", "days", UiText.Whole(p.DaysUntilEmpty));
                string item = p.ProductName != null && _productOf.TryGetValue(p.ProductName, out string name) ? name : null;
                int pieces = item != null && _cargoPieces.TryGetValue(item, out int c) ? c : 0;
                float paid = item != null && _cargoPaid.TryGetValue(item, out float v) ? v : 0f;
                worth += paid;
                string spoils = SpoilsWords(warehouse, item, out UiRole spoilTone);
                if (!Found(p.ProductName))
                {
                    continue;   // the stock's worth counts every good; the search only hides rows
                }
                UiRole lasts = BizOpsRules.LastsTone(p.DaysUntilEmpty);
                rows.Add(new UiDataRow
                {
                    Cells = new[] { p.ProductName, UiText.Whole(p.Stock), UiText.Whole(p.CargoAmount), BizOpsRules.PerDay(p.Deliveries), BizOpsRules.PerDay(p.Consumption), days,
                        spoils, pieces > 0 && paid > 0f ? (paid / pieces).ToCurrencyFormat() : "-", paid > 0f ? paid.ToCurrencyFormat() : "-" },
                    Values = new[] { (int)UiIcon.Package, 0, 0, 0, 0, 0, 0, 0, 0 },
                    Pictures = new[] { item != null ? BuyPictures.Item(item) : null },   // the good's own picture, as its shelves show it
                    Tones = new[] { UiRole.None, p.Stock == 0 ? UiRole.DangerText : UiRole.None, UiRole.None, UiRole.None, UiRole.None,
                        p.DaysUntilEmpty < 0 || lasts == UiRole.DangerText ? UiRole.Danger : lasts == UiRole.WarnText ? UiRole.Warn : p.DaysUntilEmpty == int.MaxValue ? UiRole.Muted : UiRole.Ok,
                        spoilTone, UiRole.None, UiRole.None }
                });
            }
            t.Rows = rows.ToArray();
            t.RowCount = rows.Count;
            t.EmptyAction = rows.Count == 0 && !string.IsNullOrEmpty(_tableQuery) ? UiText.T("bizman_schedule_clear_filter") : null;
            if (t.EmptyAction != null)
            {
                t.EmptyTitle = UiText.T("reallife_ui_bizops_no_match");
                t.EmptyBody = null;
            }
            if (!SpoilShown)
            {
                DropColumn(t, SpoilColumn);   // spoilage off: the table as before it
            }
            o.Table = t;
            o.FootLabel = room > 0 ? UiText.T("reallife_ui_bizops_shelf_space") : null;
            o.FootShare = room > 0 ? Math.Min(100, used * 100 / room) : -1;
            o.FootWords = room > 0 ? UiText.Fill("reallife_ui_bizops_of_boxes", "count", UiText.Whole(used), "most", UiText.Whole(room)) : null;
            o.FootRight = worth > 0f ? UiText.Fill("reallife_ui_bizops_stock_worth", "amount", worth.ToCurrencyFormat()) : null;
            o.Status = SpoiledToday(warehouse);   // food spoilage: what was thrown out today
            RowsEnd();
        }

        // How often a machine makes one at its rate an hour: "One every 1.8 s", "One every 24 s", "One every 2 min".
        private static string Pace(int perHour)
        {
            int tenths = BizOpsRules.TenthsEach(perHour);
            if (tenths < 600)
            {
                return UiText.Fill("reallife_ui_bizops_pace_seconds", "seconds", BizOpsRules.Tenths(tenths));
            }
            return UiText.Fill("reallife_ui_bizops_pace_minutes", "minutes", UiText.Whole((tenths + 300) / 600));
        }

        // The warehouse's shelves: each product's pieces and what was paid for them (each box's price a piece), the boxes on
        // the shelves and the boxes they hold, from the game's own storage items and their cargo, read once a change.
        private void Cargo(Entities.Warehouse warehouse, ref int used, ref int room)
        {
            _cargoPieces.Clear();
            _cargoPaid.Clear();
            Dictionary<string, BigAmbitions.Items.ItemInstance> items = warehouse != null ? warehouse.itemInstances : null;   // as the game's own boxes label counts them (Inventory.RefreshData)
            if (items == null)
            {
                return;
            }
            foreach (BigAmbitions.Items.ItemInstance item in items.Values)
            {
                if (item == null || item.ItemCached == null || !item.ItemCached.HasTag(BigAmbitions.Tags.TagRef.Itemtag.iswarehousestorage))
                {
                    continue;
                }
                room += item.ItemCached.cargoCapacity;
                List<BigAmbitions.Items.CargoInstance> boxes = item.cargoInstances;
                used += boxes != null ? boxes.Count : 0;
                for (int i = 0; boxes != null && i < boxes.Count; i++)
                {
                    BigAmbitions.Items.CargoInstance box = boxes[i];
                    if (box == null || string.IsNullOrEmpty(box.itemName))
                    {
                        continue;
                    }
                    _cargoPieces[box.itemName] = (_cargoPieces.TryGetValue(box.itemName, out int n) ? n : 0) + box.amount;
                    _cargoPaid[box.itemName] = (_cargoPaid.TryGetValue(box.itemName, out float p) ? p : 0f) + box.amount * box.pricePerUnit;
                }
            }
        }

        private static VehicleInstance Vehicle(string id)
        {
            List<VehicleInstance> all = SaveGameManager.Current != null ? SaveGameManager.Current.VehicleInstances : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].id == id)
                {
                    return all[i];
                }
            }
            return null;
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
                    if (_m.Part != BizManPart.Factory || arg < 0 || arg >= _machineRows.Count || _machineRows[arg] >= _machines.Count)
                    {
                        return false;
                    }
                    _machines[_machineRows[arg]].ShowWorkstationPanel();   // what a click on the game's own machine row does
                    return true;
                case BizManPages.OpsFilter:
                    if (_m.Part != BizManPart.Factory)
                    {
                        return false;
                    }
                    _machineFilter = Math.Max(0, arg);
                    if (_m.Ops.Table != null)
                    {
                        _m.Ops.Table.Offset = 0f;
                    }
                    return true;
                case BizManPages.Table + UiDataTable.Search: return _m.Part == BizManPart.Stock;   // typing starts: the host opens the field
                case BizManPages.Table + UiDataTable.Clear:
                    _tableQuery = null;
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
                    sig = sig * 31 + _groups.Count * 7 + _machines.Count + _ingredients.Count * 131 + TimeHelper.CurrentHour * 7919 + _machineFilter * 17;   // who works each machine, by the hour
                    for (int i = 0; i < _ingredients.Count; i++)
                    {
                        sig = sig * 31 + Hash(WordsAt(_ingredients[i], "StockValue"));
                    }
                    return sig;
                }
                case BizManRules.Drivers:
                    return (sig * 31 + _slots.Count + TimeHelper.CurrentHour * 7919) * 31 + TripsSignature();   // a vehicle's load by the hour; a trip leaving or back
                default:
                {
                    Inventory page = PageOf<Inventory>(b, BizManRules.Inventory);
                    var goods = page != null ? page.productsScrollerController : null;
                    sig = sig * 31 + Hash(_tableQuery);   // the goods' search typed
                    for (int i = 0; goods != null && goods.data != null && i < goods.data.Count; i++)
                    {
                        sig = sig * 31 + goods.data[i].Stock * 7 + goods.data[i].Balance;
                    }
                    return (sig * 31 + Hash(Loc(Get<TextLocalizationComponent>(GameContract.StockBoxes, page)))) * 31 + TripsSignature();
                }
            }
        }
    }
}
