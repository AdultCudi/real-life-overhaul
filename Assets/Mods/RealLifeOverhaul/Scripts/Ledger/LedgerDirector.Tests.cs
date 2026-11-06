using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>The director's test buttons.</summary>
    public sealed partial class LedgerDirector
    {
        /// <summary>Test button: today's paper opened whatever copy the player has; an edition is put together if there is none yet.</summary>
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

        /// <summary>Test button: today's paper written again (Social has just written the city's again) and left at the door, free.</summary>
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

        /// <summary>Test button: today's edition of every title comes to the phone free, and Newsstand opens on the City Ledger.</summary>
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

        /// <summary>
        /// Test button: today's paper written again with a rival's takeover story and a milestone story (the player's listed
        /// company, else the city's most valuable), and opened. Nothing of the rivals, the market or the record changes; a
        /// notice says why the milestone is not the player's, or why there is none.
        /// </summary>
        public void TestStories()
        {
            const string Test = "City Ledger stories";
            if (!Ready(Test))
            {
                return;
            }
            if (!Settings.LedgerStories)
            {
                TestRefusals.Tell(Logger, Test, TestRefusal.FeatureOff);
                return;
            }
            if (SharedCity.Follows(LedgerShare.Part))
            {
                TestRefusals.TellKey(Logger, Test, "reallife_ledger_test_stories_guest", "a guest reads the host's paper");
                return;
            }
            GameInstance save = Save;
            LedgerPiece rival = TestStoriesReady(save.Day, out LedgerPiece mark, out bool own, out string name);
            Print(save.Day, Mathf.Max(save.Hour, LedgerCatalogue.DeliveryHour + 1), _state.Edition != null && _state.Edition.Day == save.Day);
            if (!Read(LedgerPage.FromTest))
            {
                TestRefusals.TellKey(Logger, Test, "reallife_ledger_test_no_page", "the page is not in the menu yet");
                return;
            }
            if (mark == null)
            {
                Notifications.Show(NotificationType.Info, "reallife_ledger_test_stories_no_market", null, 8f);
            }
            else if (!own)
            {
                string first = LedgerText.Large(LedgerStories.Ladder[0]);
                Notifications.Show(NotificationType.Info, "reallife_ledger_test_stories_city", LedgerText.Data("name", name, "mark", first), 8f);
            }
            Logger?.Info(Test + " test: the edition of day " + _state.Edition.Day + " is open with " + (rival != null ? "a rival's takeover (" + rival.Key + ")" : "no rival's story")
                         + " and " + (mark != null ? "a milestone of " + name + (own ? ", the player's company" : ", a city company") + " (" + mark.Key + ")" : "no milestone (no market here)")
                         + "; the rivals, the market and the record are unchanged.");
        }

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
