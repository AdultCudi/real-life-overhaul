using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using Entities;
using Helpers;
using UI;
using UI.Load;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;
using UnityEngine.Events;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Takes the buttons off a rival's offer to one of the player's employees once it is no longer theirs to answer
    /// (the employee gone, replaced or resigned, the offer over), the way a press of one takes them: the message's list
    /// of buttons is emptied, so the chat draws it without them and the save keeps none. The why and the rule are in
    /// <see cref="RivalOfferRules"/>; an offer still open to the player is left exactly as the game has it.
    ///
    /// WHEN. The player's chats are looked through:
    ///   - at the end of every game hour, at the game's own event for it (GameEvent "ba:gameevent_newhour", raised
    ///     after the employees' hour and, at midnight, their day: the deadline that sends one to the rival, a
    ///     complaint's resignation, retirement, the headhunter's replacements), and after a rival is beaten
    ///     ("ba:gameevent_rivaldefeated", raised once its giving-up messages are sent);
    ///   - after the game's own firing: MyEmployees and its mass action raise GameEvent with no name once it is done;
    ///   - once a save has loaded, for an offer an older save kept;
    ///   - right after this mod lets an employee go (<see cref="Look"/>: Core/StaffLeave, by which business pressure's
    ///     dismissal, the company's departures and the tech sector's resignations go; the tech sector's dismissal; a
    ///     business sold with its staff);
    ///   - and as the Contacts app picks a chat, that chat alone, before the game draws it
    ///     (<see cref="RivalOfferWatch"/>).
    /// A chat on screen whose offer loses its buttons is drawn again through the game's own call
    /// (ContactsApp.OpenAppWithContact): the phone stops the clock while it is open, so that happens only where time
    /// runs on, as in a Going Public session.
    ///
    /// GOING PUBLIC. Only the player's own messages about their own employees: an employee of another player's business
    /// is left as the game has it, and so is Going Public's copy of a partner's message (its buttons are Going Public's
    /// own calls, not an employee's). Nothing is sent. Going Public's relay of a partner's press takes an emptied list
    /// as answered.
    ///
    /// Cost: no Update. A one-second timer on the real clock keeps the handler on the game's event and the watch in the
    /// Contacts app (one reference comparison each); a look through the chats once a game hour and after a firing (one
    /// field test a message, a few hundred to a few thousand of them), with a search of the employee list for each
    /// rival message that still has buttons (a handful at most).
    /// </summary>
    public sealed class RivalOfferGuard : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "RivalOfferGuard", typeof(RivalOfferGuard));

        public static IModLogger Logger;

        private const string NewHour = "ba:gameevent_newhour";               // the end of an hour's work (and a day's)
        private const string RivalDefeated = "ba:gameevent_rivaldefeated";   // after the rival's giving-up messages

        // The game's private fields, resolved once (Core/GameContract); null on a game without them.
        private static readonly FieldInfo DeadlineField = GameContract.OfferDeadline.Field;
        private static readonly FieldInfo OfferDayField = GameContract.OfferDay.Field;

        private Action<string> _onGameEvent, _gameEventSeen;
        private GameInstance _of;              // the save looked through as it loaded
        private RivalOfferWatch _watch;        // on this city's Contacts app

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        // What a look found: rival messages with buttons about the player's employees, those kept, those taken.
        private struct Tally
        {
            public int Looked, Kept, Taken;
        }

        private void Awake()
        {
            _onGameEvent = Guard.Wrap<string>(FrameGuard, OnGameEvent);   // made once: put on and taken off as one
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            GameEvent.onGameEventTriggered -= _onGameEvent;   // the wrapped handler that was put on
        }

        // Once a second of real time: the handler kept on the game's event (the game empties it on every load), the
        // watch put on a new city's Contacts app, and a save just loaded looked through once.
        private IEnumerator Tick()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return wait;
                if (FrameGuard.Off || Save == null || LoadScene.isLoading)
                {
                    continue;
                }
                EventHold.Keep(ref GameEvent.onGameEventTriggered, _onGameEvent, ref _gameEventSeen);
                if (_watch == null)
                {
                    _watch = RivalOfferWatch.Install();   // null until this city's interface is there
                }
                if (!ReferenceEquals(_of, Save))
                {
                    _of = Save;
                    Sweep("the save just loaded");
                }
            }
        }

        private static readonly int HotEvent = HotSpots.Slot("Core: RivalOfferGuard.OnGameEvent");

        // The game's own events: the end of each hour, a rival beaten, and those with no name (the game's firing).
        private void OnGameEvent(string name)
        {
            if (name == null || (name.Length > 0 && name != NewHour && name != RivalDefeated) || Save == null
                || LoadScene.isLoading)
            {
                return;
            }
            using var hotSpot = HotSpots.Time(HotEvent);
            Sweep(name.Length == 0 ? "a change of the game's own, as a firing"
                  : name == NewHour ? "the game's hour" : "a rival beaten");
        }

        /// <summary>
        /// Called by this mod right after it lets an employee go (Core/StaffLeave, by which business pressure's
        /// dismissal, the company's departures and the tech sector's resignations go; the tech sector's dismissal; a
        /// business sold with its staff): a rival's offer to them loses its buttons now, not at the next hour. Never throws.
        /// </summary>
        public static void Look(string why)
        {
            try
            {
                if (!FrameGuard.Off && Save != null)
                {
                    Sweep(why);
                }
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e);   // (the caller's own leaving goes on)
            }
        }

        /// <summary>
        /// The Contacts app has picked a chat (<see cref="RivalOfferWatch"/>): its rival messages are judged now. The
        /// game draws a picked chat a frame later; <paramref name="drawn"/> is for a chat already on screen, which is
        /// drawn again if a message of it lost its buttons.
        /// </summary>
        internal static void Picked(Contact chat, bool drawn)
        {
            GameInstance save = Save;
            if (FrameGuard.Off || save == null || save.EmployeeInstances == null || chat == null)
            {
                return;
            }
            var tally = new Tally();
            Pass(chat, save.EmployeeInstances, "the chat opened", ref tally);
            if (tally.Taken > 0 && drawn)
            {
                Redraw(chat);
            }
            if (Verbose && tally.Looked > 0)
            {
                Logger?.Info("Rival offers check (the chat with " + chat.id + " opened, "
                             + (drawn ? "on screen" : "before it is drawn") + "): " + tally.Looked
                             + " rival message(s) with buttons, " + tally.Kept + " still the player's to answer"
                             + " kept them, " + tally.Taken + " lost them: PASS");
            }
        }

        /// <summary>
        /// Whether an employee is still the player's to keep, by the employee part of the rule
        /// (<see cref="RivalOfferRules.Standing"/>): business pressure's watch on its poaching asks this, so that it
        /// says an employee has left exactly when their offer loses its buttons. <paramref name="name"/> is the name
        /// the offer was made to (null: the employee's own).
        /// </summary>
        public static RivalOfferRules.State Standing(EmployeeInstance e, string name)
        {
            List<EmployeeInstance> staff = Save != null ? Save.EmployeeInstances : null;
            if (e == null)
            {
                return RivalOfferRules.State.Gone;
            }
            if (staff == null)
            {
                return RivalOfferRules.State.Live;   // (no save to tell by: nothing is judged)
            }
            bool sameName = name == null || SameName(e, name);
            return RivalOfferRules.Standing(Employed(e, staff), sameName, e.isBeingReplaced, e.satisfaction);
        }

        /// <summary>
        /// True while the game's own offer to an employee is open (EmployeeInstance.PoachByRival, made by the game's special
        /// rivals or by business pressure), by <see cref="RivalOfferRules.Open"/>: the company's and the tech sector's own
        /// offers wait for it and give way to it. A few field reads; asked on the hour, the day and a press, never per frame.
        /// </summary>
        public static bool OfferOpen(EmployeeInstance e)
        {
            if (e == null || string.IsNullOrEmpty(e.poachedByRivalId))
            {
                return false;   // (nearly everybody: no rival named, no field read)
            }
            bool known = DeadlineField != null;
            return RivalOfferRules.Open(e.poachedByRivalId, known, known ? (int)DeadlineField.GetValue(e) : 0);
        }

        // Every chat of the player's: the rival messages with buttons are judged, and those the rule takes lose them.
        private static void Sweep(string why)
        {
            GameInstance save = Save;
            List<Contact> chats = save.Contacts;
            List<EmployeeInstance> staff = save.EmployeeInstances;
            if (chats == null || staff == null)
            {
                return;
            }
            Contact open = OpenChat();
            var tally = new Tally();
            bool redraw = false;
            for (int i = 0; i < chats.Count; i++)
            {
                Contact chat = chats[i];
                int before = tally.Taken;
                Pass(chat, staff, why, ref tally);
                redraw |= tally.Taken > before && open != null && ReferenceEquals(chat, open);
            }
            if (redraw)
            {
                Redraw(open);
            }
            if (Verbose && tally.Looked > 0)
            {
                Logger?.Info("Rival offers check (" + why + "): " + tally.Looked + " rival message(s) with buttons"
                             + " in the player's chats, " + tally.Kept + " still the player's to answer kept them, "
                             + tally.Taken + " lost them: PASS");
            }
        }

        // One chat: each rival message whose buttons act on an employee of the player's is judged by the rule; the
        // buttons of one it takes are emptied, as a press empties them (ContextButton.SetUp).
        private static void Pass(Contact chat, List<EmployeeInstance> staff, string why, ref Tally tally)
        {
            Queue<TextMessage> messages = chat != null ? chat.messagesQueue : null;
            if (messages == null || messages.Count == 0)
            {
                return;
            }
            foreach (TextMessage m in messages)
            {
                AdditionalMessageData extra = m != null ? m.additionalData : null;
                List<TextMessage.ContextButtonData> buttons = extra != null ? extra.contextButtonData : null;
                if (buttons == null || buttons.Count == 0)
                {
                    continue;   // (nearly every message: one field test)
                }
                RivalOfferRules.Kind kind = RivalOfferRules.KindOf(m.messageKey);
                if (kind == RivalOfferRules.Kind.None)
                {
                    continue;
                }
                EmployeeInstance e = EmployeeOf(buttons);
                if (e == null || Foreign(e))
                {
                    continue;   // Going Public's copy of a partner's message, or another player's employee: as it is
                }
                tally.Looked++;
                RivalOfferRules.State state = RivalOfferRules.Of(kind, FactsOf(e, chat.id, m, staff));
                if (state == RivalOfferRules.State.Live)
                {
                    tally.Kept++;
                    continue;
                }
                buttons.Clear();   // as a press leaves it (and as Going Public's relay reads one answered)
                tally.Taken++;
                int day = m.timestamp != null ? m.timestamp.Day : -1;
                string what = kind == RivalOfferRules.Kind.Offer ? "the rival's offer" : "the rival's giving up";
                Logger?.Info("Rival offers: " + what + " in " + chat.id + "'s chat (day " + day + ") lost its buttons ("
                             + why + "): " + RivalOfferRules.Why(state) + ".");
            }
        }

        // What the game knows of the employee and the offer, for the rule.
        private static RivalOfferRules.Facts FactsOf(EmployeeInstance e, string sentTo, TextMessage m,
                                                     List<EmployeeInstance> staff)
        {
            var f = new RivalOfferRules.Facts
            {
                Employed = Employed(e, staff),
                SameName = SameName(e, sentTo),
                BeingReplaced = e.isBeingReplaced,
                Satisfaction = e.satisfaction,
                RivalId = e.poachedByRivalId
            };
            if (DeadlineField != null && OfferDayField != null && m.timestamp != null)
            {
                f.Known = true;
                f.DeadlineDays = (int)DeadlineField.GetValue(e);
                f.OfferDay = (int)OfferDayField.GetValue(e);
                f.MessageDay = m.timestamp.Day;
            }
            return f;
        }

        // The employee the buttons act on: the object of the game's own calls (the offer's two are methods of the
        // employee, the giving-up's two its own lambdas; a save keeps them with it). Null for anything else: Going
        // Public's copy of a partner's message calls its own relay.
        private static EmployeeInstance EmployeeOf(List<TextMessage.ContextButtonData> buttons)
        {
            EmployeeInstance found = null;
            for (int i = 0; i < buttons.Count; i++)
            {
                UnityAction call = buttons[i].onClick;
                if (call == null)
                {
                    continue;
                }
                EmployeeInstance e = call.Target as EmployeeInstance;
                if (e == null || (found != null && !ReferenceEquals(found, e)))
                {
                    return null;
                }
                found = e;
            }
            return found;
        }

        // That very employee is in the player's list: the save's own, which an employee let go is taken out of (a copy
        // Going Public moves to another player is another object).
        private static bool Employed(EmployeeInstance e, List<EmployeeInstance> staff)
        {
            for (int i = 0; i < staff.Count; i++)
            {
                if (ReferenceEquals(staff[i], e))
                {
                    return true;
                }
            }
            return false;
        }

        // The name the message was sent to: the headhunter's replacement keeps the employee object, renamed.
        private static bool SameName(EmployeeInstance e, string name)
        {
            return e.characterData != null && string.Equals(e.characterData.name, name, StringComparison.Ordinal);
        }

        // Another player's employee in a Going Public session: left as the game has it.
        private static bool Foreign(EmployeeInstance e)
        {
            if (!GoingPublic.InSession || e.assignedAddress == null)
            {
                return false;
            }
            BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(e.assignedAddress);
            return reg != null && GoingPublic.Foreign(reg);
        }

        private static ContactsApp App()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null && uis.fullMenu != null ? uis.fullMenu.contactsApp : null;
        }

        // The chat the Contacts app shows now, or null.
        private static Contact OpenChat()
        {
            ContactsApp app = App();
            return app != null && app.isActiveAndEnabled ? app.selectedContact : null;
        }

        // A chat on screen that lost buttons is drawn again the game's own way, from its messages as they are now.
        private static void Redraw(Contact chat)
        {
            ContactsApp app = App();
            if (app != null && app.isActiveAndEnabled && ReferenceEquals(app.selectedContact, chat))
            {
                app.OpenAppWithContact(chat);
                Logger?.Info("Rival offers: " + chat.id + "'s chat, on screen, was drawn again without the buttons"
                             + " taken.");
            }
        }
    }
}
