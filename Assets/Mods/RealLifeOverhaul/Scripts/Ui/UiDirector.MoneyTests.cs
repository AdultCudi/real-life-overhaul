using RealLifeOverhaul.Economy;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Utilities;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Options, Tests: each money app on the kit, and each switch flipped back to the page as it was. A notice says why
    /// one cannot open. The switch test changes only the running value.
    /// </summary>
    public sealed partial class UiDirector
    {
        private static readonly string[] MoneySwitchIds = { "finance", "banks", "own_bank", "utilities" };

        /// <summary>Options, Tests: Finance on the kit.</summary>
        public void TestFinance() => OpenMoney(EconomyDirector.AppId);

        /// <summary>Options, Tests: Banks on the kit.</summary>
        public void TestBanks() => OpenMoney(BanksDirector.AppId);

        /// <summary>Options, Tests: Your bank on the kit.</summary>
        public void TestOwnBank() => OpenMoney(OwnBankDirector.AppId);

        /// <summary>Options, Tests: Utilities on the kit.</summary>
        public void TestUtilities() => OpenMoney(UtilitiesDirector.AppId);

        /// <summary>Options, Tests: each money app's switch flipped, then Finance's put back on.</summary>
        public void TestMoneySwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                for (int i = 0; i < MoneySwitchIds.Length; i++)
                {
                    string id = MoneySwitchIds[i];
                    UiScreenSwitches.Set(UiScreenSwitches.OptionOf(id), !UiScreenSwitches.On(id));
                }
                UiScreenSwitches.Set(UiScreenSwitches.OptionOf("finance"), true);
                Notifications.Show(NotificationType.Info, "reallife_ui_test_money_switch", null, 8f);
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OpenMoney(string id)
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                if (!PhoneApp.OpenById(id))
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_phone", null, 8f);
                }
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }
    }
}
