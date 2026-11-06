using UI.Dialog;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// Puts right what a conversation leaves behind when it ends other than by its own last button (a call closed by
    /// putting the phone away or Esc): four times a second it closes a text box whose asker is gone (<see cref="TextBox.Sweep"/>)
    /// and gives the dialogue panel back its conversation handle (<see cref="TextBoxRules.HandBack"/>). Costs one time
    /// comparison a frame.
    /// </summary>
    public sealed class ConversationWatch : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "ConversationWatch", typeof(ConversationWatch));   // its Update

        private float _next;

        private void Awake()
        {
            Drop();   // a box left from the city before: what it was for is gone
        }

        private static readonly int HotUpdate = HotSpots.Slot("Core: ConversationWatch.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // its feature was switched off after its faults
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float now = Time.unscaledTime;
            if (now < _next)
            {
                return;
            }
            _next = now + TextBoxRules.WatchEvery;
            TextBox.Sweep(false);
            HandBack();
        }

        // From a call that cleared it.
        private static void HandBack()
        {
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            DialogUI panel = uis != null && uis.playerHUD != null ? uis.playerHUD.dialogUI : null;
            if (panel == null || !panel.isPanelOpen)
            {
                return;
            }
            DialogController holder = DialogController.current;
            TalkHolder who = holder == null ? TalkHolder.None : holder is ContactsApp ? TalkHolder.Phone : TalkHolder.Panel;
            if (!TextBoxRules.HandBack(true, who))
            {
                return;
            }
            DialogController.current = panel;
            if (Meta.Dev.Verbose)
            {
                TextBox.Logger?.Info("Conversation check (the panel's handle): the dialogue panel was open with no conversation of the game's (a call on the phone had cleared it); "
                                     + "given back, so its buttons and Esc end it: PASS");
            }
        }

        private void OnDestroy()
        {
            Drop();
        }

        private static void Drop()
        {
            try
            {
                TextBox.Sweep(true);
            }
            catch (System.Exception e)
            {
                TextBox.Logger?.Error(e);
            }
        }
    }
}
