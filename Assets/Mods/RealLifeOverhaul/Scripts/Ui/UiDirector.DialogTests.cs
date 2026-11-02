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
    /// (a new loan) pressed on the kit once the menu has closed, so its loan form stands in the conversation; the mod's
    /// Business desk face to face; a court hearing heard at once (a case with no evidence opened for it when none is
    /// open); two of Uncle Fred's lines, queued as the game queues them, in his own voice; and the conversations'
    /// switches flipped to the game's own and back. Each goes through the game's own methods or the features' own tests
    /// and says in a notice why it cannot run when it cannot. The bank's desk is the bank's own: a loan taken there is
    /// real, and the notice says so. The switch test changes the running values only.
    /// </summary>
    public sealed partial class UiDirector
    {
        private const float TalkShows = 5f;     // seconds of real time the kit has to draw the bank's talk once the menu is closed
        private const float AnswerSeen = 0.6f;  // seconds the bank's first line stands before its answer is pressed

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
                Contact bank = Bank();
                if (bank == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_dialog_no_bank", null, 8f);
                    return;
                }
                ui.ShowDialog(CallDialogType.BankDialog, NavigationBlocker.SpecialEmployeeDialog, bank);
                Notifications.Show(NotificationType.Info, "reallife_ui_test_dialog_bank_real", null, 10f);
                StartCoroutine(Guard.Routine(AskForLoan(ui)));
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

        // The first of the city's banks the game lists (not a branch on one of the mod's streets), as its clerk names its contact.
        private static Contact Bank()
        {
            foreach (KeyValuePair<Address, Building> pair in BuildingHelper.SpecialServiceBuildings)
            {
                Building building = pair.Value;
                if (pair.Key == null || building == null || building.SpecialService == null || building.SpecialService.dialogType != CallDialogType.BankDialog
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

        // Once the menu is closed and the kit draws the talk, its first answer (a new loan) pressed on the kit, as the player presses it.
        private static IEnumerator AskForLoan(DialogUI ui)
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
                    if (ui.isPanelOpen && ReferenceEquals(ui.dialog, talk) && DialogKits.Showing)
                    {
                        screen.Press(UiConversation.ChoiceCommand, 0);
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
