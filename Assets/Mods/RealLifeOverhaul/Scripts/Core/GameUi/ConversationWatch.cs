using UI.Dialog;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// Puts right what a conversation leaves behind when it ends some other way than by its own last button (issue 22: a
    /// call closed by shutting the phone or with Esc, after which nothing would work). Four times a second of real time:
    ///   the text box   a box of the mod's is closed, with the game's own close, once what it was asked for is over
    ///                  (<see cref="TextBox.Sweep"/>): the call hung up or cancelled by the phone being put away or Esc,
    ///                  the talk left, the phone page or the options page closed, the player through a door; and every
    ///                  box of the mod's goes when a city starts (one left from before) and when it unloads;
    ///   the handle     the dialogue panel, open with nobody holding the game's handle on the conversation (a call on the
    ///                  phone took it from the panel and cleared it as it ended), has it back, so that its last button, Esc
    ///                  and the guard's way out end it again and the game's navigation blocker it set goes with it
    ///                  (<see cref="TextBoxRules.HandBack"/>).
    ///
    /// The cost: one time comparison a frame; four times a second a few field reads.
    /// </summary>
    public sealed class ConversationWatch : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "ConversationWatch", typeof(ConversationWatch));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private float _next;

        private void Awake()
        {
            Drop();   // (a box left from the city before: what it was for is gone)
        }

        private static readonly int HotUpdate = HotSpots.Slot("Core: ConversationWatch.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
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

        // The dialogue panel's handle back from a call that cleared it.
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
