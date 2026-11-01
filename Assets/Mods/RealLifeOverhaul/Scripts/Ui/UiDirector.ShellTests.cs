using System;
using System.Collections;
using System.Collections.Generic;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The RLO interface's tests of its frame, boxes and notices (Options, Tests): All apps over Contacts, a question box
    /// plain and negative, the acknowledgement, the text box, the mod's own message, each kind of notice, and Contacts
    /// switched back to the game's own and on again. Each says in a notice what it did, or exactly why it cannot run.
    /// Nothing is saved: the switch test changes the running value only, and the next start reads the page's.
    /// </summary>
    public sealed partial class UiDirector
    {
        private const float MenuSettles = 0.4f;   // seconds of real time for the full menu to open before All apps is asked for

        /// <summary>Options, Tests: Contacts on the kit, then All apps over it.</summary>
        public void TestAllApps()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (uis == null || uis.smartphoneUI == null || !ContactsKitReady())
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_phone", null, 8f);
                    return;
                }
                Slow();
                uis.smartphoneUI.OpenApp("Contacts");
                StartCoroutine(Guard.Routine(AllAppsSoon()));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private IEnumerator AllAppsSoon()
        {
            yield return new WaitForSecondsRealtime(MenuSettles);
            if (!UiShell.ToggleApps(-1))
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_all_apps_off", null, 8f);
            }
        }

        /// <summary>Options, Tests: the game's question box, through GameConfirm as the mod asks every question.</summary>
        public void TestQuestion(bool negative)
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                GameConfirm.Show(null, negative ? "reallife_ui_test_question_negative_body" : "reallife_ui_test_question_body",
                    () => Notifications.Show(NotificationType.Success, "reallife_ui_test_question_yes", null, 6f),
                    () => Notifications.Show(NotificationType.Info, "reallife_ui_test_question_no", null, 6f), null, null, false, negative);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the game's acknowledgement, never remembered (it is asked without a name to remember it by).</summary>
        public void TestAcknowledge()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                AcknowledgeWarning.Show(null, Guard.Wrap(() => Notifications.Show(NotificationType.Success, "reallife_ui_test_question_yes", null, 6f)),
                    "reallife_ui_test_ack_title", "reallife_ui_test_ack_body");
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the game's text box, through the mod's TextBox as every box of the mod's asks.</summary>
        public void TestTextBox()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                bool asked = GameUi.TextBox.Ask("reallife_ui_test_text_box_ask", "reallife_ui_test_text_box_ok",
                    text => Notifications.Show(NotificationType.Success, "reallife_ui_test_text_box_typed", new Dictionary<string, string> { { "text", text ?? string.Empty } }, 6f),
                    null, string.Empty, false, 40);
                if (!asked)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_phone", null, 8f);
                }
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the mod's own message, on the kit's card.</summary>
        public void TestModBox()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                Meta.MetaUi.Confirm(UiText.T("reallife_ui_test_mod_box_title"), UiText.T("reallife_ui_test_mod_box_body"), "reallife_meta_ok",
                    () => Notifications.Show(NotificationType.Success, "reallife_ui_test_question_yes", null, 6f));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: one notice of each kind, as the notices' own test sends them.</summary>
        public void TestNotices()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                GameUi.NoticeLook.Test();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: Contacts switched to the game's own app, or back onto the kit, for this run only.</summary>
        public void TestContactsSwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                bool on = !UiScreenSwitches.On("contacts");
                UiScreenSwitches.Set(UiScreenSwitches.OptionOf("contacts"), on);
                Notifications.Show(NotificationType.Info, on ? "reallife_ui_test_contacts_kit" : "reallife_ui_test_contacts_game", null, 8f);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // The interface on and its fonts in, or a notice that says which is not.
        private static bool Ready()
        {
            if (!On)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_off", null, 8f);
                return false;
            }
            if (!UiFonts.Ready)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_fonts", null, 8f);
                return false;
            }
            return true;
        }
    }
}
