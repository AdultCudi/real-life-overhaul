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
    /// Takes the buttons off a rival's offer to one of the player's employees once it is no longer theirs to answer, as
    /// a press does: the message's button list is emptied, so the chat and the save keep none (<see cref="RivalOfferRules"/>).
    /// The chats are looked through at the end of every game hour, after a rival is beaten, after the game's own firing
    /// (a GameEvent with no name), once a save has loaded, after this mod lets an employee go (<see cref="Look"/>), and
    /// for one chat as the Contacts app picks it (<see cref="RivalOfferWatch"/>). Going Public: only the player's own
    /// messages about their own employees; nothing is sent. Cost: no Update; a one-second real-time timer, and a look
    /// through the chats once a game hour (one field test a message).
    /// </summary>
    public sealed class RivalOfferGuard : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "RivalOfferGuard", typeof(RivalOfferGuard));

        public static IModLogger Logger;

        private const string NewHour = "ba:gameevent_newhour";               // the end of an hour's work (and a day's)
        private const string RivalDefeated = "ba:gameevent_rivaldefeated";   // after the rival's giving-up messages

        // The game's private fields; null on a game without them.
        private static readonly FieldInfo DeadlineField = GameContract.OfferDeadline.Field;
        private static readonly FieldInfo OfferDayField = GameContract.OfferDay.Field;

        private Action<string> _onGameEvent, _gameEventSeen;
        private GameInstance _of;              // the save looked through as it loaded
        private RivalOfferWatch _watch;

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        // What a look found: rival messages with buttons about the player's employees, those kept, those taken.
        private struct Tally
        {
            public int Looked, Kept, Taken;
        }

        private void Awake()
        {
            _onGameEvent = Guard.Wrap<string>(FrameGuard, OnGameEvent);
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            GameEvent.onGameEventTriggered -= _onGameEvent;
        }

        // Once a second of real time: the handler kept on the game's event (emptied on every load), the watch put on a
        // new city's Contacts app, and a save just loaded looked through once.
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

        /// <summary>The mod has just let an employee go: a rival's offer to them loses its buttons now. Never throws.</summary>
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
                Guard.Fail(FrameGuard, e);   // the caller's own leaving goes on
            }
        }

        /// <summary>The Contacts app picked a chat; <paramref name="drawn"/>: on screen already, so drawn again if needed.</summary>
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
        /// Whether an employee is still the player's, by the same rule as the buttons; <paramref name="name"/>: the name
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
                return RivalOfferRules.State.Live;   // no save to tell by
            }
            bool sameName = name == null || SameName(e, name);
            return RivalOfferRules.Standing(Employed(e, staff), sameName, e.isBeingReplaced, e.satisfaction);
        }

        /// <summary>True while the game's own offer to an employee is open; the mod's own offers give way to it.</summary>
        public static bool OfferOpen(EmployeeInstance e)
        {
            if (e == null || string.IsNullOrEmpty(e.poachedByRivalId))
            {
                return false;   // nearly everybody: no field read
            }
            bool known = DeadlineField != null;
            return RivalOfferRules.Open(e.poachedByRivalId, known, known ? (int)DeadlineField.GetValue(e) : 0);
        }

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

        // One chat: each rival message about an employee of the player's is judged; one taken has its buttons emptied,
        // as a press does (ContextButton.SetUp).
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
                    continue;   // nearly every message: one field test
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

        // The employee the buttons' calls act on (their target). Null for anything else: Going Public's copy of a
        // partner's message calls its own relay.
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

        // That very object is in the save's list (a copy Going Public moves to another player is another object).
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

        private static Contact OpenChat()
        {
            ContactsApp app = App();
            return app != null && app.isActiveAndEnabled ? app.selectedContact : null;
        }

        // A chat on screen that lost buttons is drawn again the game's own way.
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
