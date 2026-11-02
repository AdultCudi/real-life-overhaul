using System;
using BigAmbitions.Tags;
using Buildings;
using Helpers;
using UI;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Options, Tests: BizMan on the kit with one of the player's businesses, with another's shop, with a building to rent,
    /// and BizMan switched back to the game's own and on again. Each opens BizMan through the game's own BizMan.Open, as the
    /// map's and the phone's own buttons do, and says in a notice what it did or exactly why it cannot run. Nothing is
    /// saved: the switch test changes the running value only, and the next start reads the page's.
    /// </summary>
    public sealed partial class UiDirector
    {
        private enum BizManPick
        {
            Mine,
            Rival,
            ForRent
        }

        /// <summary>Options, Tests: BizMan on the kit, on the player's first business.</summary>
        public void TestBizMan() => OpenBizMan(BizManPick.Mine, "reallife_ui_test_bizman_none");

        /// <summary>Options, Tests: BizMan on the kit, on another's shop, with its offers.</summary>
        public void TestBizManRival() => OpenBizMan(BizManPick.Rival, "reallife_ui_test_bizman_no_rival");

        /// <summary>Options, Tests: BizMan on the kit, on a building to rent.</summary>
        public void TestBizManRent() => OpenBizMan(BizManPick.ForRent, "reallife_ui_test_bizman_no_rent");

        /// <summary>Options, Tests: BizMan switched to the game's own, or back onto the kit.</summary>
        public void TestBizManSwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                bool on = !UiScreenSwitches.On("bizman");
                UiScreenSwitches.Set(UiScreenSwitches.OptionOf("bizman"), on);
                Notifications.Show(NotificationType.Info, on ? "reallife_ui_test_bizman_kit" : "reallife_ui_test_bizman_game", null, 8f);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OpenBizMan(BizManPick pick, string noneKey)
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
                BuildingRegistration reg = Pick(pick);
                if (reg == null)
                {
                    Notifications.Show(NotificationType.Warning, noneKey, null, 8f);
                    return;
                }
                Slow();
                uis.fullMenu.bizMan.Open(reg.Address);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // The first place of the kind asked for, in the save's own order, so the test opens the same one on every machine.
        private static BuildingRegistration Pick(BizManPick pick)
        {
            foreach (BuildingRegistration reg in SaveGameManager.Current.BuildingRegistrations)
            {
                if (reg == null || reg.Address == null)
                {
                    continue;
                }
                switch (pick)
                {
                    case BizManPick.Mine:
                        if (reg.RentedByPlayer && reg.HasEstablishedBusiness && GoingPublic.Mine(reg))
                        {
                            return reg;
                        }
                        break;
                    case BizManPick.Rival:
                        Building building = GameRegistrations.BuildingOf(reg);
                        if (building != null && !reg.RentedByPlayer && !reg.BuildingOwnedByPlayer && !string.IsNullOrEmpty(reg.BusinessName)
                            && reg.businessTypeName != "ba:businesstype_empty" && BuildingTypeHelper.GetData(building).HasTag(TagRef.Buildingtypetag.canbetakenover)
                            && building.SpecialService == null)
                        {
                            return reg;
                        }
                        break;
                    default:
                        if (reg.AvailableForRent && !reg.RentedByPlayer && !SpecialBuildings.Holds(reg.Address.streetName, reg.Address.streetNumber))
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
