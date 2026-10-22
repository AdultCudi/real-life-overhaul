using System.Collections.Generic;
using System.Globalization;
using Entities;
using UI.Notification;

namespace RealLifeOverhaul.Designs
{
    /// <summary>The designs' test buttons: what is in place, and the firms' orders for the mod's businesses done now.</summary>
    public static partial class DesignsDirector
    {
        private const string TestName = "Ready-made designs";

        public static void TestSay()
        {
            if (!Settings.Designs)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.FeatureOff);
                return;
            }
            DesignsReport last = _last;
            if (Working || last == null)
            {
                DesignsGame.Notice(NotificationType.Info, DesignsText.SayWorking, null);
                return;
            }
            if (last.Failed || !last.On || last.Designs == 0)
            {
                Logger?.Info(TestName + " test: none in place (" + (last.Why ?? (last.On ? "nothing was made" : "taken away")) + ").");
                DesignsGame.Notice(NotificationType.Warning, DesignsText.SayFailed, null);
                return;
            }
            Logger?.Info(TestName + " test: " + last.Designs + " designs for " + last.Types + " business types in place.");
            DesignsGame.Notice(NotificationType.Success, DesignsText.SaySome, new Dictionary<string, string>(2)
            {
                { "count", last.Designs.ToString(CultureInfo.InvariantCulture) }, { "types", last.Types.ToString(CultureInfo.InvariantCulture) }
            });
        }

        /// <summary>Every firm order for the mod's businesses done now; not in a Going Public session, where one may be another player's.</summary>
        public static void TestInstall()
        {
            if (!Settings.Designs || !DesignsGame.Loaded())
            {
                TestRefusals.Tell(Logger, TestName, Settings.Designs ? TestRefusal.NotLoaded : TestRefusal.FeatureOff);
                return;
            }
            if (GoingPublic.InSession)
            {
                Logger?.Info(TestName + " test: not started (a Going Public session).");
                DesignsGame.Notice(NotificationType.Warning, DesignsText.InstallSession, null);
                return;
            }
            var types = new HashSet<string>(System.StringComparer.Ordinal);
            for (int k = 0; k < DesignCatalogue.Kinds.Length; k++)
            {
                types.Add(DesignCatalogue.Kinds[k].Type);
            }
            var orders = new List<InteriorInstallationFirmContract>(4);
            DesignsGame.Orders(types, orders);
            if (orders.Count == 0)
            {
                Logger?.Info(TestName + " test: no order of an installation firm waits for a business of the mod's.");
                DesignsGame.Notice(NotificationType.Info, DesignsText.InstallNone, null);
                return;
            }
            for (int i = 0; i < orders.Count; i++)
            {
                DesignsGame.Install(orders[i]);
            }
            Logger?.Info(TestName + " test: " + orders.Count + " installation firm orders for the mod's businesses done now.");
            DesignsGame.Notice(NotificationType.Success, DesignsText.InstallDone, new Dictionary<string, string>(1)
            {
                { "count", orders.Count.ToString(CultureInfo.InvariantCulture) }
            });
        }
    }
}
