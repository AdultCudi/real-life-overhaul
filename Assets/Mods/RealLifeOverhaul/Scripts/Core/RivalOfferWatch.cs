using System;
using Entities;
using UI;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// In the game's Contacts app, on the app's own object (so it runs only while the app shows): when the app picks a
    /// chat, that chat's rival offers are judged by Core/RivalOfferGuard before the game draws its messages. The game
    /// draws a chat a frame after picking it: ContactsApp.ShowContactConversation sets the chat at once and draws its
    /// messages in a coroutine that first waits a frame, and such a coroutine goes on after every Update of its frame.
    /// So a chat never shows buttons the guard would take, from an older save or from a way of leaving none of its
    /// other looks hears of.
    ///
    /// Cost: one reference comparison a frame while the Contacts app shows, nothing while it is closed; a chat's
    /// messages (twenty at most) when one is picked.
    /// </summary>
    public sealed class RivalOfferWatch : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "RivalOfferWatch", typeof(RivalOfferWatch));

        private ContactsApp _app;
        private Contact _seen;   // the chat last judged; null once the app has closed

        /// <summary>
        /// Puts the watch on this city's Contacts app, once; null while the app is not there yet. A chat the app
        /// already shows is judged at once, and drawn again if it loses buttons.
        /// </summary>
        internal static RivalOfferWatch Install()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            ContactsApp app = uis != null && uis.fullMenu != null ? uis.fullMenu.contactsApp : null;
            if (app == null)
            {
                return null;
            }
            RivalOfferWatch watch = app.GetComponent<RivalOfferWatch>();
            if (watch == null)
            {
                watch = app.gameObject.AddComponent<RivalOfferWatch>();
            }
            watch._app = app;
            if (app.isActiveAndEnabled && app.selectedContact != null)
            {
                watch._seen = app.selectedContact;
                RivalOfferGuard.Picked(watch._seen, true);
            }
            if (Meta.Dev.Verbose)
            {
                RivalOfferGuard.Logger?.Info("Rival offers check (the Contacts app): the watch is on the game's app,"
                                             + " so a chat it picks is judged before it is drawn: PASS");
            }
            return watch;
        }

        private static readonly int HotUpdate = HotSpots.Slot("Core: RivalOfferWatch.Update");

        // Only while the Contacts app shows: whether it has picked another chat.
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            Contact chat = _app != null ? _app.selectedContact : null;
            if (ReferenceEquals(chat, _seen))
            {
                return;
            }
            _seen = chat;
            if (chat != null)
            {
                RivalOfferGuard.Picked(chat, false);   // before the game draws it, a frame on
            }
        }

        // The app opens or closes: the chat it shows next is judged afresh.
        private void OnEnable()
        {
            _seen = null;
        }

        private void OnDisable()
        {
            _seen = null;
        }
    }
}
