using System;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ledger;
using RealLifeOverhaul.Outlook;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>Options, Tests: each news screen on the kit, and each switch flipped back. A notice says why one cannot open.</summary>
    public sealed partial class UiDirector
    {
        private static readonly string[] NewsSwitchIds = { "economy", "people", "newsstand", "ledger", "ending", "household", "renters" };

        public void TestEconomy() => OpenNews(OutlookDirector.AppId, false);

        public void TestNewsstand() => OpenNews(NewsstandApp.AppId, false);

        public void TestLedger() => OpenNews(LedgerPage.AppId, false);

        public void TestPeople() => OpenOver(new PeopleScreen(NewsLive.People), "reallife_news_people_title", UiIcon.People);

        public void TestEnding()
        {
            NewsLive.EndingOpen = true;
            OpenOver(new EndingScreen(NewsLive.Ending), "reallife_news_end_kicker", UiIcon.Person);
        }

        public void TestNewsSwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                for (int i = 0; i < NewsSwitchIds.Length; i++)
                {
                    string id = NewsSwitchIds[i];
                    UiScreenSwitches.Set(UiScreenSwitches.OptionOf(id), !UiScreenSwitches.On(id));
                }
                UiScreenSwitches.Set(UiScreenSwitches.OptionOf("economy"), true);
                Notifications.Show(NotificationType.Info, "reallife_ui_test_news_switch", null, 8f);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OpenNews(string id, bool wantedPeople)
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                if (wantedPeople)
                {
                    NewsLive.WantedTab = 7;
                }
                if (!PhoneApp.OpenById(id))
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_phone", null, 8f);
                }
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OpenOver(IUiScreen screen, string titleKey, UiIcon icon)
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                Overlay(screen, UiText.T(titleKey), host =>
                {
                    NewsLive.EndingOpen = false;
                    host.Hide();
                    host.gameObject.SetActive(false);
                    Destroy(host.gameObject);
                }, icon);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }
    }
}
