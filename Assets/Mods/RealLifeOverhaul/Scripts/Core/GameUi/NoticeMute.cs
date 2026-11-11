using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using BAModAPI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The kinds of the mod's notifications the player switched off (Options, "Notifications that pop up"): such a
    /// notification is never made. While a kind is off, a gate stands in the game's toast maker's place on
    /// Notifications.onShow (NotificationsUI.ShowNotification, at the maker's own place in the list, so every other listener
    /// keeps its order: BikeNames first, NoticeLook and the RLO interface after). The gate hands every notification on to the
    /// maker unchanged but one kept back (<see cref="NoticeMuteRules.Muted"/>), which it writes into the game's notification
    /// list as the maker would: the save's notifications, with its type, key, words and time, and not while one with its
    /// duplicate id is up. It goes there whatever its sender asked, as the list is where one that did not pop up is found (a
    /// sender keeps a toast out of the list because it was seen; the list gets a copy of such a one's words, <see
    /// cref="NoticeMuteRules.ListData"/>); only a click it carried is not kept, as the list has none. No copy of the template,
    /// no text laid out, no sound, fade or coroutine.
    /// With every kind on, the gate is not there at all and the game's event is the game's own. A notification held for the
    /// morning line (MorningNews) is not held while its kind is off: it goes straight into the list. No Harmony; nothing sent
    /// and nothing saved of its own, so each Going Public player's switches are their own and the game's list is as it was.
    /// COST: nothing a frame. A notification costs one comparison more with every kind on, the kind's table walk with one off;
    /// the gate is put on or taken off only when a switch changes, the options are read again, or a city loads or unloads.
    /// </summary>
    public static class NoticeMute
    {
        public static IModLogger Logger;

        private const int SameSlots = 8;          // kept-back notifications with a duplicate id remembered at once (the game makes one toast per id)
        private const float FadeIn = 0.5f;        // seconds of real time the game's toast takes to come (NotificationsUI.NotificationCoroutine)
        private const float FadeOut = 0.5f;       // seconds of real time it takes to go (NotificationsUI.DestroyNotificationCoroutine)
        private const float TestGap = 1.2f;       // seconds of real time between the test's samples, as in the look's test
        private const float TestSeconds = 6f;     // seconds a sample that pops up stays
        private const float DoneSeconds = 12f;    // seconds the test's answer stays
        private const string DoneKey = "reallife_notice_mute_test_done";   // the test's answer: a test's answer always pops up (NoticeMuteRules.TestAnswer)

        private static readonly FeatureGuard FrameGuard = Guard.For(null, "NoticeMute", typeof(NoticeMute));   // the gate, the switches and the test
        private static readonly Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool> Gate = Pass;   // guarded itself: its catch is the guard's
        private static readonly Action Reread = Place;   // the options read again: a preset, an import or a reset may switch a kind
        private static readonly string[] SameIds = new string[SameSlots];
        private static readonly float[] SameUntil = new float[SameSlots];

        private static NotificationsUI _maker;    // the game's toast maker of this city, found by NoticeLook behind the loading screen
        private static Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool> _game;   // its own entry, while the gate has its place
        private static int _mask;                 // the kinds switched off, a bit each (NoticeMuteRules.Bit)
        private static bool _standing;            // the gate is on the game's event
        private static bool _waiting;             // a kind is off and the maker was not on its event yet: tried again at the next notification
        private static bool _passing;             // that try, with a notification on its way: the maker is on its event by now if it makes toasts at all
        private static bool _heard;               // listening for the options being read again
        private static bool _missingSaid;
        private static bool _keptSaid;
        private static int _kept;                 // notifications kept back this session: the test counts them
        private static int _sameNext;
        private static string _keptKey;           // the notification on its way, when the gate kept it back: its key and words as sent
        private static Dictionary<string, string> _keptData;
        private static Coroutine _test;

        /// <summary>Behind the loading screen (NoticeLook.Attach): the game's toast maker of this city, or null when it was not found.</summary>
        internal static void Attached(NotificationsUI maker)
        {
            try
            {
                if (_standing && !ReferenceEquals(_game != null ? _game.Target : null, maker))
                {
                    StepAside();   // an earlier maker's place given back before this one's is taken
                }
                _maker = maker;
                _waiting = false;
                if (!_heard)
                {
                    _heard = true;
                    Settings.Changed += Reread;   // once a session
                }
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e);
            }
            Place();
        }

        /// <summary>
        /// A switch changed, or the options were read again: with a kind off, the gate takes the maker's place (once); with every
        /// kind on, or the gate stood down by the guard, the maker has its own place back. Never per frame.
        /// </summary>
        public static void Place()
        {
            try
            {
                _mask = OptionsMask();
                if (_mask != 0 && !FrameGuard.Off)
                {
                    Stand();
                }
                else
                {
                    _waiting = false;
                    StepAside();
                }
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e);
            }
        }

        /// <summary>The city is unloading (NoticeLook.OnDestroy): the maker has its own place back, or the gate simply goes with it.</summary>
        internal static void Leave()
        {
            try
            {
                StepAside();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e);
            }
            _maker = null;
            _test = null;
            _waiting = false;
            Array.Clear(SameIds, 0, SameSlots);
        }

        /// <summary>
        /// A notification is being shown (NoticeLook, on the game's event, after the maker): when a kind was off before the game's
        /// maker had joined its event (its Start), the gate takes its place now. Otherwise one read of a flag.
        /// </summary>
        internal static void Shown()
        {
            if (_waiting)
            {
                _waiting = false;
                _passing = true;
                Place();
                _passing = false;
            }
        }

        /// <summary>
        /// Before a notification is sent: true while the gate stands and would keep it back, so MorningNews lets it go at once
        /// instead of holding it for the morning line. One flag read with every kind on.
        /// </summary>
        public static bool Muting(NotificationType type, string key)
        {
            return _standing && NoticeMuteRules.Muted(key, Urgent(type), _mask);
        }

        /// <summary>
        /// For the listeners after the game's maker on its event (NoticeLook, the kit's cards): true when the gate kept this very
        /// notification back, so no toast was made for it and the newest one up is an older notification's. False for one that
        /// came before the gate stood (it was made), whatever its kind's switch says now. Two reference comparisons.
        /// </summary>
        public static bool KeptBack(string key, Dictionary<string, string> data)
        {
            return key != null && ReferenceEquals(key, _keptKey) && ReferenceEquals(data, _keptData);
        }

        /// <summary>
        /// Options > Tests: the samples of every kind, a little apart; then how many popped up and how many went straight into the
        /// notification list, and a log line that sets what the gate kept back against what the switches say, and the toasts the
        /// game's maker made against what the gate kept back (none for a kept one, one for each other), PASS or FAIL.
        /// </summary>
        public static void Test()
        {
            NoticeLook look = NoticeLook.Instance;
            if (look == null || !ModState.Loaded || SaveGameManager.Current == null || Notifications.onShow == null)
            {
                TestRefusals.Tell(Logger, "Notice mute", TestRefusal.NotLoaded);
                return;
            }
            if (_test != null)
            {
                look.StopCoroutine(_test);
            }
            _test = look.StartCoroutine(Guard.Routine(ShowEach()));
        }

        private static IEnumerator ShowEach()
        {
            string[] samples = NoticeLook.Samples;
            int mask = OptionsMask();
            int expected = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                expected += NoticeMuteRules.Muted(samples[i], false, mask) ? 1 : 0;
            }
            int kept = 0, wrong = 0, counted = 0;
            var gap = new WaitForSecondsRealtime(TestGap);
            for (int i = 0; i < samples.Length; i++)
            {
                Transform stack = _maker != null ? _maker.container : null;   // the game's toasts: the maker puts each it makes in here at once
                int toasts = stack != null ? stack.childCount : 0;
                int before = _kept;   // the event is called at once: what the gate keeps back in this call is this sample, whatever else comes meanwhile
                Notifications.Show(NotificationType.Info, samples[i], null, TestSeconds);
                bool held = _kept > before;
                kept += held ? 1 : 0;
                if (stack != null)
                {
                    counted++;
                    wrong += stack.childCount - toasts == (held ? 0 : 1) ? 0 : 1;   // none made for one kept back, one for one passed on
                }
                yield return gap;
            }
            _test = null;
            var data = new Dictionary<string, string>(3)
            {
                { "shown", (samples.Length - kept).ToString(CultureInfo.InvariantCulture) },
                { "all", samples.Length.ToString(CultureInfo.InvariantCulture) },
                { "kept", kept.ToString(CultureInfo.InvariantCulture) }
            };
            Notifications.Show(NotificationType.Info, DoneKey, data, DoneSeconds);
            bool pass = kept == expected && wrong == 0 && counted == samples.Length;
            Logger?.Info("Notice mute test: " + samples.Length + " samples sent, " + kept + " kept from popping up, " + (samples.Length - kept) + " popped up; the switches keep back "
                         + expected + " (" + Kinds(mask) + "); toasts made as the gate said for " + (counted - wrong) + " of " + samples.Length
                         + (pass ? ": PASS."
                             : kept != expected ? ": FAIL (the gate did not stand in the maker's place; see the lines above)."
                             : counted < samples.Length ? ": FAIL (the game's toast maker was not found, so its toasts were not counted)."
                             : ": FAIL (a toast was made for a notification kept back, or none for one passed on)."));
        }

        // The gate, in the maker's place: every notification goes on to the maker but one kept back. The maker is called after the
        // guard's try, as the event itself would call it, so a fault of the game's stays the game's, as it would be without the gate.
        private static void Pass(NotificationType type, string key, Dictionary<string, string> data, float seconds, string same, Action onClick, bool sound, bool track)
        {
            Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool> game = _game;
            bool kept = false;
            _keptKey = null;   // this notification's fate for the listeners after the maker: passed on, unless kept back below
            _keptData = null;
            try
            {
                var maker = game != null ? game.Target as NotificationsUI : null;
                if (maker == null)
                {
                    game = null;   // the maker is gone with its city, its own entry with it: the gate goes too
                    StepAside();
                }
                else if (FrameGuard.Off)
                {
                    StepAside();   // stood down by the guard: the maker has its place back, and this one goes on to it
                }
                else if (NoticeMuteRules.Muted(key, Urgent(type), _mask))
                {
                    Keep(maker, type, key, data, seconds, same, track);
                    kept = true;
                    _keptKey = key;
                    _keptData = data;
                }
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
            if (!kept && game != null)
            {
                game(type, key, data, seconds, same, onClick, sound, track);
            }
        }

        // As the game's PlayNotification records one (one with the duplicate id of a toast that is up is dropped), with nothing made
        // and no sound; into the save's list whatever its sender asked, since it did not pop up (a copy of the words of one its
        // sender kept out of the list, NoticeMuteRules.ListData).
        private static void Keep(NotificationsUI maker, NotificationType type, string key, Dictionary<string, string> data, float seconds, string same, bool track)
        {
            _kept++;
            if (same != null)
            {
                float now = Time.realtimeSinceStartup;
                if ((maker.container != null && maker.container.Find(same) != null) || SameUp(same, now))
                {
                    return;   // one with this id is up, or would be: the game drops this one
                }
                SameIds[_sameNext] = same;
                SameUntil[_sameNext] = now + FadeIn + seconds + FadeOut;
                _sameNext = (_sameNext + 1) % SameSlots;
            }
            GameInstance save = SaveGameManager.Current;
            if (save != null && save.notifications != null)
            {
                save.notifications.Enqueue(new Notification { type = type, key = key, notificationData = NoticeMuteRules.ListData(data, track), date = TimeHelper.Now() });
            }
            if (Meta.Dev.Verbose && !_keptSaid)
            {
                _keptSaid = true;
                bool listed = save != null && save.notifications != null;
                Logger?.Info("Notice check (mute): " + key + " (" + NoticeKinds.Of(key) + ") kept from popping up"
                             + (listed ? ", written into the game's notification list (" + save.notifications.Count + " there"
                                         + (track ? ")" : "; its sender kept it out of the list, which holds a copy of its words)") + ": PASS."
                                       : ": FAIL (no save's list to write it in)."));
            }
        }

        // A kept-back notification with this duplicate id would still be up.
        private static bool SameUp(string same, float now)
        {
            for (int i = 0; i < SameSlots; i++)
            {
                if (SameUntil[i] > now && string.Equals(SameIds[i], same, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        // The gate in the maker's own place on the event, once; nothing while the maker or its entry is not there.
        private static void Stand()
        {
            NotificationsUI maker = _maker;
            MethodInfo method = GameContract.NoticeMutePart.Off ? null : GameContract.NoticeMaker.Method;
            if (maker == null || method == null)
            {
                return;   // no city, the maker not found (NoticeLook said so), or a game update changed it (said at start)
            }
            Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool> shown = Notifications.onShow;
            Delegate[] list = shown != null ? shown.GetInvocationList() : Array.Empty<Delegate>();
            if (Array.IndexOf(list, Gate) >= 0)
            {
                _standing = true;
                return;
            }
            // The entry the game puts on in its Start and takes off in its OnDestroy, made again to be found by equality.
            Delegate own = Delegate.CreateDelegate(typeof(Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>), maker, method, false);
            int at = own != null ? Array.IndexOf(list, own) : -1;
            if (at < 0)
            {
                if (!_passing)
                {
                    _waiting = true;   // the maker joins its event in its Start, which may come after the loading screen: again at the next notification
                }
                else if (!_missingSaid)
                {
                    _missingSaid = true;
                    Logger?.Warn("Game UI: the game's notification maker is not on its event as expected; the mod's notifications pop up whatever their kinds' switches say.");
                }
                return;
            }
            _game = (Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>)list[at];
            list[at] = Gate;
            SetEvent(list);
            _standing = true;
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Notice check (mute): " + Kinds(_mask) + " kept from popping up; the gate stands in the game's maker's place, " + (at + 1) + " of "
                             + list.Length + " on its event: PASS.");
            }
        }

        // The maker's own entry back in the gate's place; or, the maker gone with its city, the gate simply taken off.
        private static void StepAside()
        {
            Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool> game = _game;
            bool was = _standing;
            _game = null;
            _standing = false;
            _keptKey = null;
            _keptData = null;
            if (game == null && !was)
            {
                return;   // the gate was never on: nothing to give back
            }
            Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool> shown = Notifications.onShow;
            Delegate[] list = shown != null ? shown.GetInvocationList() : null;
            int at = list != null ? Array.IndexOf(list, Gate) : -1;
            var maker = game != null ? game.Target as NotificationsUI : null;
            if (at >= 0 && maker != null)
            {
                list[at] = game;
                SetEvent(list);
            }
            else if (at >= 0)
            {
                Notifications.onShow = (Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>)Delegate.Remove(shown, Gate);
            }
            else if (maker != null && (list == null || Array.IndexOf(list, game) < 0))
            {
                // The gate was taken off by something else: the maker is put back on, so the game's notifications still pop up.
                Notifications.onShow = (Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>)Delegate.Combine(shown, game);
            }
            if (was && Meta.Dev.Verbose)
            {
                Logger?.Info("Notice check (mute): " + (maker != null ? "the game's maker has its own place back on its event" : "the maker went with its city; the gate is off its event")
                             + (FrameGuard.Off ? " (the guard stood the gate down)" : string.Empty) + ": PASS.");
            }
        }

        private static void SetEvent(Delegate[] list)
        {
            Notifications.onShow = (Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>)Delegate.Combine(list);
        }

        // The kinds switched off, from the options, in NoticeMuteRules.Switchable's order.
        private static int OptionsMask()
        {
            int mask = NoticeMuteRules.With(0, NoticeKind.Road, Settings.NoticeShowRoad);
            mask = NoticeMuteRules.With(mask, NoticeKind.Money, Settings.NoticeShowMoney);
            mask = NoticeMuteRules.With(mask, NoticeKind.Property, Settings.NoticeShowProperty);
            mask = NoticeMuteRules.With(mask, NoticeKind.Work, Settings.NoticeShowWork);
            mask = NoticeMuteRules.With(mask, NoticeKind.Business, Settings.NoticeShowBusiness);
            mask = NoticeMuteRules.With(mask, NoticeKind.Tech, Settings.NoticeShowTech);
            mask = NoticeMuteRules.With(mask, NoticeKind.News, Settings.NoticeShowNews);
            mask = NoticeMuteRules.With(mask, NoticeKind.People, Settings.NoticeShowPeople);
            return NoticeMuteRules.With(mask, NoticeKind.Other, Settings.NoticeShowOther);
        }

        private static bool Urgent(NotificationType type)
        {
            return type == NotificationType.Warning || type == NotificationType.Error;
        }

        // The kinds switched off, for the log: "Road, News", or "no kind".
        private static string Kinds(int mask)
        {
            var names = new StringBuilder(64);
            for (int i = 0; i < NoticeMuteRules.Switchable.Length; i++)
            {
                NoticeKind kind = NoticeMuteRules.Switchable[i];
                if ((mask & NoticeMuteRules.Bit(kind)) != 0)
                {
                    names.Append(names.Length > 0 ? ", " : string.Empty).Append(kind);
                }
            }
            return names.Length > 0 ? names.ToString() : "no kind";
        }
    }
}
