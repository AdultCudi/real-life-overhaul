using System;
using System.Collections;
using System.Collections.Generic;
using Buildings;
using Dialogs;
using Entities;
using Helpers;
using Tutorial;
using UI;
using UI.Dialog;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The RLO interface's tests of the conversations (Options, Tests): a talk at one of the city's banks, its first answer
    /// (a new loan) pressed on the kit once the menu has closed, so its loan form stands in the conversation; a wholesale
    /// store's manager and an importer's the same way, their first answer opening the delivery contract's form and the
    /// partnership's (or their own words for why not: no business, no warehouse, no agent on a shift); the mod's
    /// Business desk face to face; a court hearing heard at once (a case with no evidence opened for it when none is
    /// open); a tax case heard at once, as an audit brings one, so the card shows what each plea costs and the city's
    /// settlement (a real case: the notice says so); two of Uncle Fred's lines, queued as the game queues them, in his own
    /// voice; and the conversations' switches flipped to the game's own and back. Each goes through the game's own methods or the features' own tests
    /// and says in a notice why it cannot run when it cannot. The bank's, the store's and the importer's desks are their
    /// own: a loan, a contract or a partnership taken there is real, and the notice says so. The switch test changes the
    /// running values only.
    /// </summary>
    public sealed partial class UiDirector
    {
        private const float TalkShows = 5f;     // seconds of real time the kit has to draw the bank's talk once the menu is closed
        private const float AnswerSeen = 0.6f;  // seconds the bank's first line stands before its answer is pressed
        private const int TaxKeptBack = 1800;   // the tax case's sum at the start's prices: an audit's find of a small business

        private static readonly string[] DialogIds = { DialogKits.DialogId, DialogKits.MonologueId, DialogKits.ScenesId };

        /// <summary>Options, Tests: the bank's own talk at one of the city's banks, its loan form asked for on the kit.</summary>
        public void TestDialogBank()
        {
            try
            {
                DialogUI ui = TalkReady(DialogKits.DialogId);
                if (ui == null)
                {
                    return;
                }
                Contact bank = Service(CallDialogType.BankDialog);
                if (bank == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_dialog_no_bank", null, 8f);
                    return;
                }
                ui.ShowDialog(CallDialogType.BankDialog, NavigationBlocker.SpecialEmployeeDialog, bank);
                Notifications.Show(NotificationType.Info, "reallife_ui_test_dialog_bank_real", null, 10f);
                StartCoroutine(Guard.Routine(FirstAnswer(ui)));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: a wholesale store's manager, the delivery contract's form asked for on the kit.</summary>
        public void TestDialogWholesale() => TalkAt(CallDialogType.WholesaleStoreManagerDialog, "reallife_ui_test_dialog_no_wholesale", "reallife_ui_test_dialog_wholesale_real");

        /// <summary>Options, Tests: an importer's manager, the partnership's form asked for on the kit.</summary>
        public void TestDialogImport() => TalkAt(CallDialogType.ImportManagerDialog, "reallife_ui_test_dialog_no_import", "reallife_ui_test_dialog_import_real");

        // The first of the city's places of a kind, its own talk face to face, its first answer pressed on the kit once the menu has closed.
        private void TalkAt(CallDialogType type, string none, string real)
        {
            try
            {
                DialogUI ui = TalkReady(DialogKits.DialogId);
                if (ui == null)
                {
                    return;
                }
                Contact contact = Service(type);
                if (contact == null)
                {
                    Notifications.Show(NotificationType.Warning, none, null, 8f);
                    return;
                }
                ui.ShowDialog(type, NavigationBlocker.SpecialEmployeeDialog, contact);
                Notifications.Show(NotificationType.Info, real, null, 10f);
                StartCoroutine(Guard.Routine(FirstAnswer(ui)));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the mod's Business desk face to face, through its own test.</summary>
        public void TestDialogDesk()
        {
            try
            {
                if (TalkReady(DialogKits.DialogId) != null)
                {
                    BusinessDirector.Instance?.TestDesk();
                }
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the next open case heard as soon as the menu is closed; a case with no evidence opened first when none is.</summary>
        public void TestDialogCourt()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                if (!UiScreenSwitches.On(DialogKits.ScenesId))
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_dialog_off", null, 8f);
                    return;
                }
                JusticeDirector justice = JusticeDirector.Instance;
                if (justice == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_not_ready", null, 8f);
                    return;
                }
                if (JusticeState.NextOpen == null)
                {
                    justice.TestCase(3);   // the justice tests' case with no evidence behind it: the least a hearing can cost
                }
                if (JusticeState.NextOpen != null)
                {
                    justice.TestHearing();
                }
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>
        /// Options, Tests: a tax case filed as an audit files one and heard as soon as the menu is closed, its plea with what
        /// each answer costs and the city's settlement. Only with no case of the player's open, so this one is heard. Real: what
        /// is pleaded counts, as the notice says.
        /// </summary>
        public void TestDialogTaxCourt()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                if (!UiScreenSwitches.On(DialogKits.ScenesId))
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_dialog_off", null, 8f);
                    return;
                }
                JusticeDirector justice = JusticeDirector.Instance;
                if (justice == null || !Justice.Active)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_not_ready", null, 8f);
                    return;
                }
                if (JusticeState.NextOpen != null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_dialog_case_open", null, 8f);
                    return;
                }
                if (TaxCases.File(Prices.At(TaxKeptBack)) <= 0 || JusticeState.NextOpen == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_not_ready", null, 8f);
                    return;
                }
                Notifications.Show(NotificationType.Info, "reallife_ui_test_dialog_tax_real", null, 10f);
                justice.TestHearing();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: two of Uncle Fred's lines in the game's own queue, with his face and his voice where the game has them.</summary>
        public void TestDialogFred()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                if (!UiScreenSwitches.On(DialogKits.MonologueId))
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_dialog_off", null, 8f);
                    return;
                }
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (uis == null || uis.monologueUI == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_not_ready", null, 8f);
                    return;
                }
                Sprite face = uis.tutorialUI != null && GameContract.FredFace.Field != null ? GameContract.FredFace.Field.GetValue(uis.tutorialUI) as Sprite : null;
                int said = 0;
                Quest[] quests = TutorialHelper.allQuests;
                for (int i = 0; quests != null && i < quests.Length && said < 2; i++)
                {
                    Quest q = quests[i];
                    if (q != null && !string.IsNullOrEmpty(q.uncleFredMessageType) && q.uncleFredAudioClip != null)
                    {
                        uis.monologueUI.EnqueueMonologue(q.uncleFredMessageType, q.uncleFredAudioClip, face);
                        said++;
                    }
                }
                if (said == 0)
                {
                    // The tutorial's lines are not loaded in this city: two of them without his voice, as the game says a line it has no sound for.
                    uis.monologueUI.EnqueueMonologue("ba:messagetype_tutorial_quest_find_a_job_uncle_fred", null, face);
                    uis.monologueUI.EnqueueMonologue("ba:messagetype_tutorial_quest_get_some_food_uncle_fred", null, face);
                }
                Logger?.Info("UI test: two of Uncle Fred's lines queued" + (said > 0 ? " with his voice" : " without his voice (the tutorial's lines are not loaded)")
                             + (face != null ? "" : "; his face was not found, so the card shows his initial"));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the conversations, the spoken lines and the scenes switched to the game's own, and back.</summary>
        public void TestDialogSwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                bool on = !UiScreenSwitches.On(DialogKits.DialogId);
                for (int i = 0; i < DialogIds.Length; i++)
                {
                    UiScreenSwitches.Set(UiScreenSwitches.OptionOf(DialogIds[i]), on);
                }
                Notifications.Show(NotificationType.Info, on ? "reallife_ui_test_dialog_kit" : "reallife_ui_test_dialog_game", null, 8f);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // The interface and the conversation's own switch on, the panel there and no conversation open; or a notice that says why not.
        private static DialogUI TalkReady(string screen)
        {
            if (!Ready())
            {
                return null;
            }
            if (!UiScreenSwitches.On(screen))
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_dialog_off", null, 8f);
                return null;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            DialogUI ui = uis != null && uis.playerHUD != null ? uis.playerHUD.dialogUI : null;
            if (ui == null || SaveGameManager.Current == null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_not_ready", null, 8f);
                return null;
            }
            if (ui.isPanelOpen || DialogController.current != null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_dialog_busy", null, 8f);
                return null;
            }
            return ui;
        }

        // The first of the city's places of a kind the game lists (a bank, a wholesale store, an importer; not one on the mod's
        // streets), as its clerk names its contact.
        private static Contact Service(CallDialogType type)
        {
            foreach (KeyValuePair<Address, Building> pair in BuildingHelper.SpecialServiceBuildings)
            {
                Building building = pair.Value;
                if (pair.Key == null || building == null || building.SpecialService == null || building.SpecialService.dialogType != type
                    || NewBuildings.MadeStreets.IsMine(pair.Key.streetName))
                {
                    continue;
                }
                BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(pair.Key);
                Contact contact = reg != null ? reg.GetOrAddBusinessContact(true) : null;
                if (contact != null)
                {
                    return contact;
                }
            }
            return null;
        }

        // Once the menu is closed and the kit draws the talk, its first answer (a new loan, a contract, a partnership) pressed on
        // the kit, as the player presses it.
        private static IEnumerator FirstAnswer(DialogUI ui)
        {
            object talk = ui.dialog;
            while (ui != null && ui.isPanelOpen && ReferenceEquals(ui.dialog, talk) && MenuUp())
            {
                yield return new WaitForSecondsRealtime(0.25f);
            }
            float until = Time.unscaledTime + TalkShows;
            while (ui != null && ui.isPanelOpen && ReferenceEquals(ui.dialog, talk) && Time.unscaledTime < until)
            {
                DialogScreen screen = DialogKits.Showing ? DialogKits.Screen : null;
                if (screen != null)
                {
                    yield return new WaitForSecondsRealtime(AnswerSeen);
                    if (ui.isPanelOpen && ReferenceEquals(ui.dialog, talk) && DialogKits.Showing && screen.HasAnswer)
                    {
                        screen.Press(UiConversation.ChoiceCommand, 0);   // a line with Leave alone (no business to supply) stays to be read
                    }
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }
        }

        // The options the test was pressed in, or the game paused under them: the answer waits until the player is back in the city.
        private static bool MenuUp()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return Scenes.MainMenu.Options.IsVisible || uis == null || (uis.gameSpeed != null && uis.gameSpeed.Paused);
        }
    }
}
