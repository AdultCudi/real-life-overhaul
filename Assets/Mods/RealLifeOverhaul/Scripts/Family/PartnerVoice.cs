using System;
using System.Collections.Generic;
using Dialogs;
using Entities;
using UI.Notification;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// How the partner raises things about the player's fortune (half days, their wishes, how they take the player's money).
    /// A first word comes some days after moving in, then one every few days at the most (PartnerVoiceRules), so that they
    /// reach the player one at a time. An ask (something to talk over) stays open for a week: a reminder on the fifth and
    /// sixth day, and after the seventh it lapses, as a no; while one is open nothing else is raised. Each is a text in the
    /// partner's thread and a notice with their name that opens their messages; the talk itself is face to face at home,
    /// where speaking to the partner opens the open ask first (<see cref="OpenAsk"/>). Cost: nothing per frame; a few
    /// comparisons once a game hour in the day.
    /// </summary>
    internal static class PartnerVoice
    {
        public const string Work = "work";   // the kinds of ask, saved by name
        public const string Wish = "wish";
        public const string Car = "car";
        public const string Allowance = "allowance";
        public const string Date = "date";

        /// <summary>For a test button: the next word may come now, whatever the days since the last (one word, then it is spent).</summary>
        internal static bool ForceOne;

        private static readonly string[] Reminders = { "reallife_family_ask_remind_1", "reallife_family_ask_remind_2" };

        private static FamilySave S => FamilyState.S;
        private static int Day => TimeHelper.CurrentDay;

        /// <summary>True while an ask of this kind is open.</summary>
        public static bool Asking(string kind) => S.PAsk == kind;

        /// <summary>True while any ask is open; one whose switch has gone off is none (<see cref="Hourly"/> drops it).</summary>
        public static bool AnyAsk => !string.IsNullOrEmpty(S.PAsk) && On(S.PAsk);

        /// <summary>
        /// True when something may be raised now: living together long enough since moving in, no ask open, and the days
        /// since the last word said over (PartnerVoiceRules).
        /// </summary>
        public static bool Free => FamilyState.Together && S.LivesIn && (ForceOne || (!AnyAsk && PartnerVoiceRules.Free(Day, S.MovedInDay, S.PVoiceDay, S.PSeed)));

        /// <summary>Something said that asks nothing (a reaction, a wish to come): it counts as the latest word.</summary>
        public static void Say(string key, in Args a)
        {
            ForceOne = false;
            S.PVoiceDay = Day;
            Text(key, a);
        }

        /// <summary>Something to talk over, of a kind: open for a week, talked over face to face at home.</summary>
        public static void Ask(string kind, string key, in Args a)
        {
            S.PAsk = kind;
            S.PAskDay = Day;
            S.PAskReminded = 0;
            Say(key, a);
        }

        /// <summary>The open ask of this kind was talked over (yes or no): it is closed.</summary>
        public static void Answered(string kind)
        {
            if (S.PAsk == kind)
            {
                S.PAsk = null;
            }
        }

        /// <summary>A warm word after a yes: it follows its own days and does not hold anything else back.</summary>
        public static void Warm(string key, in Args a)
        {
            Text(key, a);
        }

        /// <summary>Once a game hour in the day: the open ask's reminders, and its lapse after a week.</summary>
        public static void Hourly(int day)
        {
            if (string.IsNullOrEmpty(S.PAsk))
            {
                return;
            }
            if (!FamilyState.Together || !S.LivesIn)
            {
                S.PAsk = null;
                return;
            }
            if (!On(S.PAsk))
            {
                FamilyDirector.Logger?.Info("Family: " + Partner.Name + "'s ask (" + S.PAsk + ") was dropped: its switch is off.");
                S.PAsk = null;   // switched off while it was open: no reminder, no lapse, nothing held against the player
                return;
            }
            int since = day - S.PAskDay;
            if (since >= PartnerVoiceRules.AskDays)
            {
                FamilyDirector.Logger?.Info("Family: " + Partner.Name + "'s ask (" + S.PAsk + ") lapsed after " + since + " days.");
                S.PAsk = null;   // (each feature reads its own flag still set with no ask open as a no)
                Text("reallife_family_ask_lapsed", Args.Us);
                return;
            }
            int remind = PartnerVoiceRules.Reminder(since, S.PAskReminded);
            if (remind >= 0)
            {
                S.PAskReminded = remind + 1;
                Text(Reminders[remind], Args.Us);
            }
        }

        /// <summary>The talk of the open ask, for speaking to the partner face to face: null with none open.</summary>
        public static DialogEntry OpenAsk()
        {
            Func<DialogEntry> talk = AnyAsk ? TalkOf(S.PAsk) : null;
            return talk != null ? talk() : null;
        }

        private static Func<DialogEntry> TalkOf(string kind)
        {
            switch (kind)
            {
                case Work: return PartnerWork.Talk;
                case Wish: return PartnerWish.Talk;
                case Car: return PartnerCar.Offer;
                case Allowance: return PartnerExpect.AllowanceTalk;
                case Date: return PartnerExpect.DateTalk;
                default: return null;
            }
        }

        // A text in the partner's thread without the game's own notice, and a notice with their name and the text that opens their
        // messages; with no contact to text from (the phone's contacts off) the notice alone, so the words never show twice.
        private static void Text(string key, in Args a)
        {
            Contact contact = FamilyPhone.PartnerContact;
            if (contact != null)
            {
                FamilyText.Message(contact, key, a, notify: false);
            }
            var data = new Dictionary<string, string>(2) { { "name", Partner.Name }, { "text", FamilyText.T(key, a) } };
            Action open = contact != null ? contact.OnClickNotification : (Action)null;
            Notifications.Show(NotificationType.Info, "reallife_family_voice_notice", data, 12f, key, open != null ? Guard.Wrap(open) : null, true, false);
        }

        // Whether the switch of an ask's kind is on: an ask whose switch went off while it was open is dropped (Hourly).
        private static bool On(string kind)
        {
            switch (kind)
            {
                case Work: return Settings.FamilyWork;
                case Wish: return Settings.FamilyWork;   // the bigger wish has the cut back's switch
                default: return true;
            }
        }
    }
}
