using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Options, Tests: the Tech app on the kit, a studio's game page, a data centre's page, and the switch flipped back to the old pages. A notice says why one cannot open.
    /// The switch test changes only the running value.
    /// </summary>
    public sealed partial class UiDirector
    {
        /// <summary>Options, Tests: the Tech app on the kit, on its firms.</summary>
        public void TestTech() => OpenTech(TechDirector.TabFirms, null);

        /// <summary>Options, Tests: a studio's game page (the first game of a studio of yours, else the first product).</summary>
        public void TestTechGame()
        {
            TechDirector d = TechDirector.Instance;
            FirmsFile f = d != null ? d.Firms : null;
            Product first = null;
            for (int i = 0; f != null && i < f.Products.Count; i++)
            {
                if (f.Products[i].K == ProductKind.Game)
                {
                    first = f.Products[i];
                    break;
                }
            }
            if (first == null && f != null && f.Products.Count > 0)
            {
                first = f.Products[0];
            }
            if (first == null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_tech_none", null, 8f);
                return;
            }
            OpenTech(TechDirector.TabProducts, TechApp.ProductRow + first.Id);
        }

        /// <summary>Options, Tests: a data centre's page (the first one of yours).</summary>
        public void TestTechCentre()
        {
            TechDirector d = TechDirector.Instance;
            InfraFile f = d != null ? d.Infra : null;
            if (f == null || f.Centres.Count == 0)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_tech_none", null, 8f);
                return;
            }
            OpenTech(TechDirector.TabInfra, TechApp.CentreRow + f.Centres[0].Place);
        }

        /// <summary>Options, Tests: the Tech screen's switch flipped, then put back on.</summary>
        public void TestTechSwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                UiScreenSwitches.Set(UiScreenSwitches.OptionOf(TechScreen.ScreenId), !UiScreenSwitches.On(TechScreen.ScreenId));
                UiScreenSwitches.Set(UiScreenSwitches.OptionOf(TechScreen.ScreenId), true);
                Notifications.Show(NotificationType.Info, "reallife_ui_test_tech_switch", null, 8f);
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OpenTech(int tab, string pick)
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                TechLive.Want(tab, pick);
                if (!PhoneApp.OpenById(TechDirector.TechAppId))
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
