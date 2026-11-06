using System.Collections.Generic;
using BAModAPI;
using Dialogs;
using Entities;
using Helpers;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Floors;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.BuildTrades
{
    /// <summary>The building trades' test entries (Options > Tests): each works when pressed or says in a notice why not, and writes its check line.</summary>
    public sealed partial class TradesDirector
    {
        private const float TestSiteReach = 80f;   // metres a site may be from the player for the site test
        private static readonly TradeSite TestSite = new TradeSite();
        private int _testCommissions;

        /// <summary>A trade's shift here and now, whatever the player holds and whatever site is near (none needed): its game at once.</summary>
        public void TestTrade(string id)
        {
            if (!Ready("reallife_trades_test_refused"))
            {
                return;
            }
            Logger?.Info("Trades check (test): the " + id + "'s shift asked for here, as a test (nothing it pays counts towards a level or a record).");
            JobDirector.Instance.TestStart(id);
        }

        /// <summary>The nearest works site's notice conversation opened where the player stands, its Work here page first.</summary>
        public void TestSiteWork()
        {
            if (!Ready("reallife_trades_test_refused"))
            {
                return;
            }
            if (!TradesBridges.NearestSite(PlayerHelper.GetPosition(), TestSiteReach, TestSite))
            {
                Notifications.Show(NotificationType.Info, "reallife_trades_test_no_site", null, 9f);
                return;
            }
            SiteWorkDialog.Open(TestSite.Id);
            Logger?.Info("Trades check (site work test): the work of site " + TestSite.Id + " (" + TestSite.Address + ") opened face to face.");
        }

        /// <summary>The player taken to the practice's floor by the building's own door.</summary>
        public void TestOffice()
        {
            if (!Ready("reallife_trades_test_refused"))
            {
                return;
            }
            if (!Settings.TradesOffice || !_asked || !FloorsDirector.Where(_office, out FloorWhere where))
            {
                Notifications.Show(NotificationType.Info, "reallife_trades_test_no_floor", null, 9f);
                Logger?.Info("Trades check (practice floor): the practice has no floor yet (no spare floor, or its option is off): FAIL.");
                return;
            }
            string why = FloorsDirector.Enter(where.Host, where.Floor, "the trades' test");
            Logger?.Info("Trades check (practice floor): floor " + where.Floor + " of " + TradesGame.Address(where.Street, where.Number) + ": "
                         + (why == null ? "going in by the game's door: PASS" : "not now (" + why + ")") + ".");
            if (why != null)
            {
                Notifications.Show(NotificationType.Info, "reallife_trades_test_not_now", null, 8f);
            }
        }

        /// <summary>A desk trade's shift at the practice's desk, as a test; the player must be on its floor (the test above takes them).</summary>
        public void TestDesk(string id)
        {
            if (!Ready("reallife_trades_test_refused"))
            {
                return;
            }
            if (!TradeOffice.Here)
            {
                Notifications.Show(NotificationType.Info, "reallife_trades_test_go_office", null, 9f);
                return;
            }
            TestTrade(id);
        }

        /// <summary>A builder's brief at the practice now, as the construction company's projects will put one there.</summary>
        public void TestCommission()
        {
            if (!DesignBook.Loaded)
            {
                Notifications.Show(NotificationType.Info, "reallife_trades_test_refused", null, 8f);
                return;
            }
            _testCommissions++;
            TradesBridges.Commission("test:" + _testCommissions, 5, 18, 4000000);
        }

        /// <summary>A bribe of three days ago that comes out today: the fine, and the city's building control letting its inspector go.</summary>
        public void TestBribe()
        {
            if (!DesignBook.Loaded)
            {
                Notifications.Show(NotificationType.Info, "reallife_trades_test_refused", null, 8f);
                return;
            }
            int taken = TimeHelper.CurrentDay - DesignBook.BribeComesOutDays;
            int site = 1;
            while (!TradeRules.BribeCaught(site, taken) && site < 1000)
            {
                site++;   // a site whose bribe comes out on that day: the same rule a real one goes by
            }
            DesignBook.Bribed(taken, site, 500);
            _day = -1;   // looked at on the next second
            Logger?.Info("Trades check (bribe test): a bribe of 500 taken on day " + taken + " at site " + site + ", to come out at the next second.");
        }

        /// <summary>Twenty points of masonry, as a labourer's shifts teach them: the mason's rank at the builder's is open to the player.</summary>
        public void TestApprentice()
        {
            int before = TradesBridges.PointsOf(Skills.Masonry);
            TradesBridges.Learnt(Skills.Masonry, TradeCatalogue.MasonRank.Points * 1000);
            int after = TradesBridges.PointsOf(Skills.Masonry);
            bool holds = TradesBridges.Holds(TradeCatalogue.MasonRank);
            Logger?.Info("Trades check (apprentice): masonry " + before + " -> " + after + " points; the mason's rank asks " + TradeCatalogue.MasonRank.Points + ": "
                         + (holds ? "PASS" : "FAIL (the careers' skills are not there: studying is off)") + ".");
            var data = new Dictionary<string, string>(1) { { "points", after.ToString() } };
            Notifications.Show(holds ? NotificationType.Success : NotificationType.Warning, holds ? "reallife_trades_test_apprentice_done" : "reallife_trades_test_refused", data, 9f);
        }

        // The jobs are there and the player free for a test.
        private bool Ready(string refusal)
        {
            if (JobDirector.Instance == null || !Settings.Trades || _broken)
            {
                Notifications.Show(NotificationType.Info, refusal, null, 8f);
                return false;
            }
            return true;
        }
    }

    /// <summary>A site's work face to face with nobody's notice board in between (the site test): the same pages as "Work here".</summary>
    public sealed class SiteWorkDialog : Dialog
    {
        private const string Name = "reallife_tradessite_calldialogtype";
        private static int _site;

        public static void Open(int siteId)
        {
            _site = siteId;
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(Name);
            CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new SiteWorkDialog()));
            InstanceBehavior<UI.UIs>.Instance.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog);
        }

        public SiteWorkDialog()
        {
            npcNameKey = "reallife_trades_site_foreman";
            DialogController.current.ShowEntry(Guard.Entry(SiteWork.Page(_site)));
        }
    }
}
