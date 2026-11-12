using System;
using Buildings;
using UI;
using UI.Notification;
using UI.Smartphone.Apps.BizMan;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Options, Tests: BizMan's operations on the kit: a store's schedule, a headquarters' managers, a factory and a
    /// warehouse, each opened through the game's own BizMan.Open on its tab as the map's and the phone's own buttons open
    /// a place; the uniform presets' editor opened through the game's own; and the operations' and the editor's switches
    /// flipped. Each says in a notice what it did or exactly why it cannot run. Nothing is saved: a switch test changes the
    /// running value only, and the next start reads the page's.
    /// </summary>
    public sealed partial class UiDirector
    {
        private enum OpsPick
        {
            Store,
            Headquarters,
            Factory,
            Warehouse
        }

        /// <summary>Options, Tests: a store of the player's on its schedule, drawn as the board.</summary>
        public void TestBizOpsSchedule() => OpenOps(OpsPick.Store, BizMan.ScheduleTab, "reallife_ui_test_bizops_no_store");

        /// <summary>Options, Tests: the player's headquarters on its managers' plans.</summary>
        public void TestBizOpsManagers() => OpenOps(OpsPick.Headquarters, BizMan.PricingManagersTab, "reallife_ui_test_bizops_no_hq");

        /// <summary>Options, Tests: a factory of the player's on its machines.</summary>
        public void TestBizOpsFactory() => OpenOps(OpsPick.Factory, BizMan.FactoryTab, "reallife_ui_test_bizops_no_factory");

        /// <summary>Options, Tests: a warehouse of the player's on its drivers.</summary>
        public void TestBizOpsWarehouse() => OpenOps(OpsPick.Warehouse, BizMan.DriversTab, "reallife_ui_test_bizops_no_warehouse");

        /// <summary>Options, Tests: the operations switched to the game's own pages for this run, or back onto the kit.</summary>
        public void TestBizOpsSwitch() => Flip(BizManScreen.BizOpsId, "reallife_ui_test_bizops_kit", "reallife_ui_test_bizops_game");

        /// <summary>Options, Tests: the uniform presets' editor, opened as BizMan's own button opens it.</summary>
        public void TestUniforms()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (uis == null || uis.employeePresetCustomizer == null || SaveGameManager.Current == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_phone", null, 8f);
                    return;
                }
                if (!UiScreenSwitches.On(UniformsKit.Id))
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_uniforms_off", null, 8f);
                    return;
                }
                UniformsKit.Install();
                Slow();
                uis.employeePresetCustomizer.Show();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the uniform presets' editor switched to the game's own for this run, or back onto the kit.</summary>
        public void TestUniformsSwitch() => Flip(UniformsKit.Id, "reallife_ui_test_uniforms_kit", "reallife_ui_test_uniforms_game");

        private void OpenOps(OpsPick pick, string tab, string noneKey)
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (uis == null || uis.fullMenu == null || uis.fullMenu.bizMan == null || SaveGameManager.Current == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_phone", null, 8f);
                    return;
                }
                if (!UiScreenSwitches.On("bizman") || !UiScreenSwitches.On(BizManScreen.BizOpsId))
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_bizops_off", null, 8f);
                    return;
                }
                BuildingRegistration reg = PickOps(pick);
                if (reg == null)
                {
                    Notifications.Show(NotificationType.Warning, noneKey, null, 8f);
                    return;
                }
                Slow();
                uis.fullMenu.bizMan.Open(reg.Address, tab);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // The first of the player's own places of the kind asked for, in the save's own order, so the test opens the same one on every machine.
        private static BuildingRegistration PickOps(OpsPick pick)
        {
            foreach (BuildingRegistration reg in SaveGameManager.Current.BuildingRegistrations)
            {
                if (reg == null || reg.Address == null || !reg.RentedByPlayer || !reg.HasEstablishedBusiness || !GoingPublic.Mine(reg))
                {
                    continue;
                }
                Building building = GameRegistrations.BuildingOf(reg);
                bool warehouse = building != null && building.BuildingType == "ba:buildingtype_warehouse";
                bool hq = reg.businessTypeName == "ba:businesstype_headquarters", factory = reg.businessTypeName == "ba:businesstype_factory";
                switch (pick)
                {
                    case OpsPick.Headquarters:
                        if (hq)
                        {
                            return reg;
                        }
                        break;
                    case OpsPick.Factory:
                        if (factory)
                        {
                            return reg;
                        }
                        break;
                    case OpsPick.Warehouse:
                        if (warehouse)
                        {
                            return reg;
                        }
                        break;
                    default:
                        if (!hq && !factory && !warehouse && reg.businessTypeName != "ba:businesstype_empty")
                        {
                            return reg;
                        }
                        break;
                }
            }
            return null;
        }
    }
}
