using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Notification;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The way back for a folded phone while the game's own phone is the one in play (<see cref="PhoneReachRules"/>). The game
    /// saves the phone's fold and has no key for it; its head stands at the foot of the screen, under the interface's bar,
    /// where it can be neither seen nor pressed. The kit's own phone lifts its head over the bar and answers the Phone key
    /// (PhoneKit). The classic phone, which stays whenever another mod pages the phone's apps (a player with Aluna's Phone
    /// Pages had it every session, folded), another mod's screen is open on it, its switch is off or the kit's phone met a
    /// fault, had nothing: here it has the Phone key, which folds and unfolds it, and a phone button in the bar's pill of
    /// small buttons that presses the game's own fold button. The first time the phone is seen folded the key and the button
    /// are named once in a notice, and a press that does nothing says why in Player.log. Cost: a key test a frame while the
    /// interface is on; four times a second, with the bar, two property reads.
    /// </summary>
    internal static class PhoneReach
    {
        internal static readonly FeatureGuard WatchGuard = Guard.For("ui", "PhoneReach", typeof(PhoneReachWatch));

        private static PhoneReachWatch _watch;
        private static PhoneWhy _said = PhoneWhy.None;   // the cause last written to Player.log: a repeat of it is not
        private static string _heldSaid;
        private static bool _told;

        /// <summary>From the director's second while the interface is on: the watch that reads the Phone key stands.</summary>
        public static void Ensure()
        {
            UiDirector director = UiDirector.Instance;
            if (director == null || WatchGuard.Off)
            {
                return;
            }
            if (_watch == null)
            {
                _watch = director.GetComponent<PhoneReachWatch>() ?? director.gameObject.AddComponent<PhoneReachWatch>();
            }
            if (!_watch.enabled)
            {
                _watch.enabled = true;
            }
        }

        /// <summary>The interface is off or its part is given back: the key is the game's alone again.</summary>
        public static void Stop()
        {
            if (_watch != null)
            {
                _watch.enabled = false;
            }
            _said = PhoneWhy.None;
            _heldSaid = null;
        }

        /// <summary>A new city: the notice may be said again, the log may repeat a cause.</summary>
        public static void Rearm()
        {
            _told = false;
            _said = PhoneWhy.None;
            _heldSaid = null;
        }

        /// <summary>
        /// The bar's phone button: whether it shows (the classic phone is the one in play and the game shows it) and whether the
        /// phone is open now. Read with the bar's other pieces; the first sight of a folded phone is the hint's moment.
        /// </summary>
        public static bool Look(out bool open)
        {
            open = false;
            UIs uis = InstanceBehavior<UIs>.Instance;
            SmartphoneUI phone = uis != null ? uis.smartphoneUI : null;
            CollapsibleWindow window = uis != null ? uis.smartphoneCollapsibleWindow : null;
            bool shows = phone != null && window != null && phone.gameObject.activeInHierarchy;
            if (shows)
            {
                open = !window.IsCollapsed;
                if (PhoneReachRules.Remind(_told, true, window.IsCollapsed))
                {
                    Tell();
                }
            }
            return PhoneReachRules.Offer(shows, PhoneKit.Showing);
        }

        /// <summary>What the phone button says when the pointer is on it: what it does and the key, as bound now.</summary>
        public static string Words() => UiText.Fill("reallife_phone_tool_note", "key", Keys.Label(KeyAction.Phone));

        private static void Tell()
        {
            _told = true;
            Notifications.Show(NotificationType.Info, "reallife_phone_put_away", new Dictionary<string, string> { { "key", Keys.Label(KeyAction.Phone) } }, 10f, null, null, true, false);
        }

        /// <summary>The Phone key, each frame while the interface is on and the classic phone is in play (the kit's own answers its key itself).</summary>
        internal static void Frame()
        {
            if (!UnityEngine.Input.GetKeyDown(Keys.Key(KeyAction.Phone)) || !PhoneReachRules.Mine(PhoneKit.Showing))
            {
                return;
            }
            if (!Keys.Down(KeyAction.Phone))
            {
                Held();
                return;
            }
            if (!Press())
            {
                UiGame.Instance.Play(UiCue.Error);   // the bar's button has the HUD's own cue for a press that did nothing
            }
        }

        /// <summary>
        /// The phone folded if it is open and unfolded if it is folded, by the game's own fold button (its listener keeps the state
        /// in the save and other mods hear it); the window's own change where the button cannot be pressed. False, with the cause
        /// said in Player.log, when it could not.
        /// </summary>
        public static bool Press()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            SmartphoneUI phone = uis != null ? uis.smartphoneUI : null;
            CollapsibleWindow window = uis != null ? uis.smartphoneCollapsibleWindow : null;
            bool city = SaveGameManager.Current != null && !UI.Load.LoadScene.isLoading;
            PhoneWhy why = PhoneReachRules.Why(city, phone != null, window != null, phone != null && phone.gameObject.activeInHierarchy);
            if (why == PhoneWhy.None)
            {
                bool folded = window.IsCollapsed;
                Fold(window);
                if (window.IsCollapsed != folded)
                {
                    _said = PhoneWhy.None;
                    return true;
                }
                why = PhoneWhy.Refused;
            }
            Say(why);
            return false;
        }

        /// <summary>The kit's own phone met a key that moved nothing (it is still in its animation, or another mod turned it back).</summary>
        internal static void Refused() => Say(PhoneWhy.Refused);

        private static void Fold(CollapsibleWindow window)
        {
            Button collapse = !GameContract.PhoneKitPart.Off ? (GameContract.PhoneCollapse.Field?.GetValue(window) as GameObject)?.GetComponent<Button>() : null;
            if (UiWidgets.Shows(collapse))
            {
                UiWidgets.Hand(collapse);
            }
            else
            {
                window.ChangeState(!window.IsCollapsed);
            }
        }

        private static void Say(PhoneWhy why)
        {
            if (why == _said)
            {
                return;
            }
            _said = why;
            UiDirector.Logger?.Info(PhoneReachRules.Words(why, why == PhoneWhy.Hidden ? HiddenBy() : null, Keys.Label(KeyAction.Phone)));
        }

        // The key went down and the keys' own check refused it: a panel of the mod holds the keyboard, a text field has it, a key is being chosen.
        private static void Held()
        {
            string line = PhoneReachRules.Held(Keys.Capturing, Keys.Suspended, TypingNow.Now, Keys.Label(KeyAction.Phone));
            if (line != null && line != _heldSaid)
            {
                _heldSaid = line;
                UiDirector.Logger?.Info(line);
            }
        }

        // What of the game has switched its phone off, where it can be told.
        private static string HiddenBy()
        {
            try
            {
                if (global::UI.Smartphone.FullMenu.IsOpen)
                {
                    return "the full menu is open";
                }
                if (CityMap.IsOpen)
                {
                    return "the city map is open";
                }
                if (GameManager.IsAnyMiniGameActive())
                {
                    return "a mini game is running";
                }
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                // the game's members are not what the mod was made for: the line says it without the cause
            }
            return "a scene, another mod or the game's own sequence has it";
        }
    }

    /// <summary>The Phone key's watch on the director's object: <see cref="PhoneReach.Frame"/> each frame while the interface is on, guarded.</summary>
    internal sealed class PhoneReachWatch : MonoBehaviour
    {
        private static readonly int Hot = HotSpots.Slot("Ui: PhoneReach.Frame");

        private void Update()
        {
            using var hotSpot = HotSpots.Time(Hot);
            if (PhoneReach.WatchGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                PhoneReach.Frame();
            }
            catch (Exception e)
            {
                Guard.Fail(PhoneReach.WatchGuard, e, this);   // the phone's bar button and the mouse still work; only the key goes
            }
        }
    }
}
