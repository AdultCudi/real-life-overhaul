using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ledger
{
    public sealed partial class LedgerDirector
    {
        // ---------------------------------------------------------------- the owner's test buttons

        /// <summary>Test button: today's paper opened whatever copy the player has (if there is no edition yet, one is put together now).</summary>
        public void TestOpen()
        {
            const string Test = "Open the City Ledger";
            if (!Ready(Test))
            {
                return;
            }
            GameInstance save = Save;
            if (_state.Edition == null || _state.Edition.Day != save.Day)
            {
                Print(save.Day, Mathf.Max(save.Hour, LedgerCatalogue.DeliveryHour + 1), false);
            }
            if (!Read(LedgerPage.FromTest))
            {
                TestRefusals.TellKey(Logger, Test, "reallife_ledger_test_no_page", "the page is not in the menu yet");
                return;
            }
            Logger?.Info(Test + " test: the edition of day " + _state.Edition.Day + " is open (" + _state.Edition.Pieces.Count + " pieces, " + _state.Edition.Quotes.Count + " companies).");
        }

        /// <summary>
        /// Test button: today's paper written again (the city's paper has just been written again by the button's other half,
        /// Social) and a copy left inside the door of the home, as a subscriber's is: nothing is charged.
        /// </summary>
        public void TestDeliver()
        {
            const string Test = "Deliver the City Ledger";
            if (!Ready(Test))
            {
                return;
            }
            GameInstance save = Save;
            Print(save.Day, Mathf.Max(save.Hour, LedgerCatalogue.DeliveryHour + 1), true);
            if (Home() == null)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_ledger_test_no_home");
                return;
            }
            LedgerRules.Deliver(_state, _state.Edition.Day);
            Notifications.Show(NotificationType.Info, AtHome() ? "reallife_ledger_note_delivered_here" : "reallife_ledger_note_delivered", null, 8f);
            Logger?.Info(Test + " test: the edition of day " + _state.Edition.Day + " is at the door of " + HomeName + " (" + _state.Edition.Pieces.Count + " pieces).");
        }

        /// <summary>
        /// Test button: today's edition of every Newsstand title comes to the phone now, whatever the subscriptions and the
        /// connection (nothing is charged), and the app opens on the City Ledger. If there is no edition yet, one is put
        /// together now.
        /// </summary>
        public void TestOnline()
        {
            const string Test = "Newsstand's editions";
            if (!Ready(Test))
            {
                return;
            }
            if (!Settings.LedgerOnline || _online == null)
            {
                TestRefusals.Tell(Logger, Test, TestRefusal.FeatureOff);
                return;
            }
            GameInstance save = Save;
            if (_state.Edition == null || _state.Edition.Day != save.Day)
            {
                Print(save.Day, Mathf.Max(save.Hour, LedgerCatalogue.DeliveryHour + 1), false);
            }
            for (int t = 0; t < NewsstandCatalogue.Titles; t++)
            {
                if (!NewsstandRules.Has(_online, t, save.Day))
                {
                    _online.Loaded[t].Add(save.Day);
                }
            }
            _stand?.Install();
            if (_stand == null)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_ledger_test_no_page", "the app is not on the phone yet");
                return;
            }
            _stand.Open(NewsstandCatalogue.Ledger);
            Logger?.Info(Test + " test: today's editions of the " + NewsstandCatalogue.Titles + " titles are on the phone (day " + save.Day + ", " + _state.Edition.Pieces.Count
                         + " pieces); Newsstand is open on the City Ledger.");
        }

        /// <summary>Test button: a newspaper kiosk on the nearest stretch of shopfront round the player (not kept in the save).</summary>
        public void TestKiosk()
        {
            const string Test = "Place a newspaper kiosk";
            if (!Ready(Test))
            {
                return;
            }
            TestRefusal street = TestRefusals.Street();
            if (street != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, Test, street);
                return;
            }
            if (PlayerHelper.IsUsingVehicle)
            {
                TestRefusals.Tell(Logger, Test, TestRefusal.InVehicle);
                return;
            }
            if (!_kiosks.TestHere(PlayerHelper.PlayerController.transform.position, out float metres))
            {
                TestRefusals.Tell(Logger, Test, TestRefusal.NoShopfront, Mathf.RoundToInt(metres));
                return;
            }
            Logger?.Info(Test + " test: a kiosk stands " + metres.ToString("F0") + " m away; its counter's panel sells today's paper from 06:00 to 20:00.");
        }

        /// <summary>Test button: the kiosks near the home are forgotten and looked for again.</summary>
        public void TestFindKiosks()
        {
            const string Test = "Find the kiosks again";
            if (!Ready(Test))
            {
                return;
            }
            _state.KiosksFor = null;
            _state.Kiosks.Clear();
            _kiosks.Again();
            Logger?.Info(Test + " test: the kiosks near the home are looked for again over the next seconds.");
        }

        /// <summary>Test button: what the tests put in the street goes (the test kiosk).</summary>
        public void TestBack()
        {
            if (_kiosks != null && _kiosks.TestBack())
            {
                Logger?.Info("City Ledger test: the test kiosk is gone.");
            }
        }

        private bool Ready(string test)
        {
            if (!Settings.SocialNewspaper)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            if (!Running || Save == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            return true;
        }
    }
}
