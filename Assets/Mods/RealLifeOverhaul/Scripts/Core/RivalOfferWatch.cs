using System;
using Entities;
using UI;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// On the game's Contacts app: a chat the app picks has its rival offers judged by <see cref="RivalOfferGuard"/>
    /// before the game draws its messages, a frame later (ShowContactConversation draws in a coroutine). Cost: one
    /// reference comparison a frame while the app shows, nothing while closed.
    /// </summary>
    public sealed class RivalOfferWatch : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "RivalOfferWatch", typeof(RivalOfferWatch));

        private ContactsApp _app;
        private Contact _seen;   // the chat last judged; null once the app has closed

        /// <summary>
        /// Puts the watch on this city's Contacts app; null while the app is not there yet. A chat already shown is
        /// judged at once.
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

        // Only while the Contacts app shows.
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
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
                RivalOfferGuard.Picked(chat, false);
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
