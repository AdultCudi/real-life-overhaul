using System;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The test tools of the options page for the firm account: each does one thing now, for real, to the first funded firm.</summary>
    public sealed partial class TechDirector
    {
        // The first firm of the player's with investors, its business standing; null, with a message, when there is none.
        private Cap TestFunded(out FirmPlace firm)
        {
            firm = null;
            if (!TestVentures())
            {
                return null;
            }
            for (int i = 0; i < _venture.Caps.Count; i++)
            {
                FirmPlace there = StartupMoney.Has(_venture.Caps[i]) ? FirmOf(_venture.Caps[i].Place) : null;
                if (there != null)
                {
                    firm = there;
                    return _venture.Caps[i];
                }
            }
            Notifications.ShowError("reallife_vc_test_no_funded");
            return null;
        }

        /// <summary>The board of the first funded firm gets its pack now and looks at the books: suspicion is worked out and the ladder's next step taken if it is due.</summary>
        public void TestBooks()
        {
            Cap c = TestFunded(out FirmPlace firm);
            if (c == null)
            {
                return;
            }
            c.Acc.PackDay = Save.Day;
            c.Acc.Calm = 0;
            MoneyDay(c, firm, Save.Day);
            RefreshFloor(Save.Day);
            ChangedTech();
        }

        /// <summary>The first funded firm is wound down now: its debts, the waterfall to the investors out of the wallet, and the claim for any gap.</summary>
        public void TestWindDown()
        {
            Cap c = TestFunded(out FirmPlace firm);
            if (c == null)
            {
                return;
            }
            WindDown(c, firm.File.Name, Save.Day);
        }

        /// <summary>
        /// A quarter of the investors' money is spent on startups of the city (a lawful purchase the table calls misuse) and the same sum comes back
        /// at once, so only the books remember it: the shortfall is counted, and the firm page shows it.
        /// </summary>
        public void TestSpendElsewhere()
        {
            Cap c = TestFunded(out FirmPlace firm);
            if (c == null)
            {
                return;
            }
            long floor = Math.Max(_floorNow, 1);
            long spend = Math.Max(0L, VentureGame.Cash - floor) + Math.Max(floor / 4, Level(MoneyCatalogue.NoiseCents) * 2);
            if (_floorNow <= 0 || spend > VentureGame.Cash || !VentureGame.Pay(spend, VentureGame.InvestKey, firm.File.Name))
            {
                Notifications.ShowError("reallife_vc_test_no_floor");
                return;
            }
            VentureGame.Give(spend, VentureGame.ReturnKey, firm.File.Name);
            ChangedTech();
        }
    }
}
