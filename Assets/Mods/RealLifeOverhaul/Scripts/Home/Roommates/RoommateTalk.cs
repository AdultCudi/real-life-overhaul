using System;
using BAModAPI;
using Dialogs;
using Entities;
using Localizor;
using Localizor.LanguageChangeEvent;
using UI.Notification;

namespace RealLifeOverhaul.Roommates
{
    /// <summary>
    /// The conversations with a roommate, in the game's own dialogue panel, face to face in the flat or on the phone.
    /// What is talked about is whatever the day left waiting (<see cref="Topic"/>): their notice, the player's partner
    /// moving in, rent that is late, last night's noise, food that went, a loan, a meal they left; with nothing
    /// waiting, their day, once a day. Each has two or three replies, and each reply does something: the panel's two
    /// buttons and its third (the game's own "cancel" place, relabelled), whose answer comes as a notice because the
    /// panel closes on it.
    ///
    /// A reply acts the moment it is pressed, and the topic is done only then: a save made with the panel open, or a
    /// conversation walked away from, leaves the topic waiting for the next one. Built when the panel opens or a
    /// button is pressed; nothing here runs otherwise.
    /// </summary>
    internal static class RoommateTalk
    {
        private const string InPersonType = "reallife_roommatetalk_calldialogtype";

        // Written out in full for the locale check.
        private static readonly string[] DayLines = { "reallife_roommate_day_1", "reallife_roommate_day_2", "reallife_roommate_day_3", "reallife_roommate_day_4", "reallife_roommate_day_5" };

        private static Func<DialogEntry> _first;

        private static RoommateDirector Director => RoommateDirector.Instance;

        /// <summary>The first entry of the face-to-face conversation being opened; read once by <see cref="RoommateDialog"/>.</summary>
        internal static DialogEntry TakeFirst()
        {
            Func<DialogEntry> first = _first;
            _first = null;
            return Safe(first ?? Nothing)();
        }

        /// <summary>
        /// Opens the conversation face to face (the game's own panel, as its counters and desks open one).
        /// <paramref name="closed"/> is called when it ends, however it ends.
        /// </summary>
        public static bool Open(Action closed, ThirdPersonCharacter with)
        {
            var uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis == null || uis.playerHUD == null || uis.playerHUD.dialogUI == null || DialogController.current != null)
            {
                closed?.Invoke();
                return false;
            }
            _first = Next;
            var type = (CallDialogType)ModEnumHash.GetSafeHash(InPersonType);
            CallDialogFactory.RegisterDialog(type, Guard.Maker(() => new RoommateDialog()));
            uis.playerHUD.dialogUI.ShowDialog(type, NavigationBlocker.SpecialEmployeeDialog, null, Guard.Way(closed), with);
            return true;
        }

        // ---------------------------------------------------------------- the pieces

        private static bool OnPhone => DialogController.current != null && DialogController.current.dialogType == DialogType.PhoneCall;

        /// <summary>A last line: on the phone the call ends by itself, face to face there is a button to leave.</summary>
        public static DialogEntry Close(string key, in Fill f)
        {
            bool phone = OnPhone;
            return new DialogEntry
            {
                messageData = RoommateText.Say(key, f),
                OnVisible = phone ? DialogController.current.FinishDialog : (Action)null,
                OnCancel = phone ? (Action)null : Leave
            };
        }

        public static DialogEntry Close(string key)
        {
            return Close(key, Us);
        }

        public static void Leave()
        {
            if (DialogController.current != null)
            {
                DialogController.current.CancelDialog();
            }
        }

        private static Fill Us => Fill.For(Director != null ? Director.MateName : string.Empty);

        private static DialogEntry Nothing()
        {
            return Close("reallife_roommate_talk_nothing");
        }

        // A reply runs from the game's button handler: a fault must end the conversation politely, not escape.
        internal static Func<DialogEntry> Safe(Func<DialogEntry> action)
        {
            return () =>
            {
                try
                {
                    if (Director == null || !Director.Running || SaveGameManager.Current == null)
                    {
                        return Close("reallife_roommate_phone_off", default);
                    }
                    return action();
                }
                catch (Exception e)
                {
                    RoommateDirector.Logger?.Error(e);
                    return Close("reallife_roommate_talk_nothing", default);
                }
            };
        }

        // The third reply closes the panel, so what it does is done first and its answer is given as a notice.
        private static Action Third(Func<string> effect, NotificationType type = NotificationType.Info)
        {
            return () =>
            {
                try
                {
                    if (Director != null && Director.Running && Director.State.Living)
                    {
                        Fill us = Us.Cost(RoommateRules.FoodPrice);
                        RoommateText.Toast(type, effect(), us, 8f);
                    }
                }
                catch (Exception e)
                {
                    RoommateDirector.Logger?.Error(e);
                }
                Leave();
            };
        }

        private static DialogEntry Ask(LanguageChangeEventDataHolder text, string firstKey, Func<DialogEntry> first, string secondKey, Func<DialogEntry> second,
            string thirdKey = null, Action third = null)
        {
            return new DialogEntry
            {
                messageData = text,
                ConfirmTextOverride = firstKey.Localize(),
                OnConfirm = Safe(first),
                SecondOptionTextOverride = secondKey,
                OnSecondOption = Safe(second),
                CancelTextOverride = thirdKey,
                OnCancel = third ?? Leave
            };
        }

        // ---------------------------------------------------------------- what is talked about

        /// <summary>The conversation for whatever waits; their day when nothing does; a closing line when that has been had today.</summary>
        public static DialogEntry Next()
        {
            RoommateDirector d = Director;
            RoommateState s = d != null && d.Running ? d.State : null;
            if (s == null || !s.Living)
            {
                return Close("reallife_roommate_talk_nothing", default);
            }
            switch (s.NextTopic())
            {
                case Topic.Leaving: return Leaving(d, s);
                case Topic.PartnerIn: return PartnerIn(d);
                case Topic.Rent: return Rent(d, s);
                case Topic.Noise: return Noise(d);
                case Topic.Raid: return Raid(d, s);
                case Topic.Borrow: return Borrow(d, s);
                case Topic.Cooked: return Cooked(d);
                default: return TheirDay(d, s);
            }
        }

        private static DialogEntry Leaving(RoommateDirector d, RoommateState s)
        {
            Fill f = Us.On(RoommateText.When(s.LeaveDay)).With(RoommateText.T(RoommateText.WhyKey(s.Notice)));
            if (s.Notice == EndWhy.Row)
            {
                return Ask(RoommateText.Say("reallife_roommate_talk_leaving_row", f),
                    "reallife_roommate_reply_fair", () => { d.LeavingHeard(); return Close("reallife_roommate_talk_leaving_row_fair"); },
                    "reallife_roommate_reply_sorry", () => Close(d.LeavingAskStay() ? "reallife_roommate_talk_leaving_stays" : "reallife_roommate_talk_leaving_row_no"));
            }
            return Ask(RoommateText.Say("reallife_roommate_talk_leaving", f),
                "reallife_roommate_reply_luck", () => { d.LeavingLuck(); return Close("reallife_roommate_talk_leaving_luck"); },
                "reallife_roommate_reply_stay", () => Close(d.LeavingAskStay() ? "reallife_roommate_talk_leaving_stays" : "reallife_roommate_talk_leaving_goes"));
        }

        private static DialogEntry PartnerIn(RoommateDirector d)
        {
            Fill f = Us.And(RoommateBridges.PartnerName != null ? RoommateBridges.PartnerName() : string.Empty);
            return Ask(RoommateText.Say("reallife_roommate_talk_partner", f),
                "reallife_roommate_reply_stay_room", () => { d.PartnerStay(); return Close("reallife_roommate_talk_partner_stays"); },
                "reallife_roommate_reply_best_go", () => { d.PartnerGo(); return Close("reallife_roommate_talk_partner_goes", Us.On(RoommateText.When(d.State.LeaveDay))); });
        }

        private static DialogEntry Rent(RoommateDirector d, RoommateState s)
        {
            Fill f = Us.Cost(s.Owed).On(RoommateText.When(s.OwedDue));
            return Ask(RoommateText.Say(s.OwedSlips > 0 ? "reallife_roommate_talk_rent_again" : "reallife_roommate_talk_rent", f),
                "reallife_roommate_reply_wait", () => { d.RentWait(); return Close("reallife_roommate_talk_rent_wait", f); },
                "reallife_roommate_reply_now", () => Close(d.RentChase() ? "reallife_roommate_talk_rent_paid" : "reallife_roommate_talk_rent_cannot", f),
                "reallife_roommate_reply_forget", Third(() => { d.RentCover(); return "reallife_roommate_talk_rent_covered"; }, NotificationType.Success));
        }

        private static DialogEntry Noise(RoommateDirector d)
        {
            return Ask(RoommateText.Say("reallife_roommate_talk_noise", Us),
                "reallife_roommate_reply_quiet", () => { d.NoiseAsk(); return Close("reallife_roommate_talk_noise_agreed"); },
                "reallife_roommate_reply_fine", () => { d.NoiseFine(); return Close("reallife_roommate_talk_noise_fine"); },
                "reallife_roommate_reply_not_on", Third(() => { d.NoiseRow(); return "reallife_roommate_talk_noise_row"; }, NotificationType.Warning));
        }

        private static DialogEntry Raid(RoommateDirector d, RoommateState s)
        {
            Fill f = Us.And(string.IsNullOrEmpty(s.RaidItem) ? RoommateText.T("reallife_roommate_food_something") : RoommateText.T(s.RaidItem)).Cost(RoommateRules.FoodPrice);
            return Ask(RoommateText.Say("reallife_roommate_talk_raid", f),
                "reallife_roommate_reply_help_yourself", () => { d.RaidFine(); return Close("reallife_roommate_talk_raid_fine"); },
                "reallife_roommate_reply_stop", () => { d.RaidStop(); return Close("reallife_roommate_talk_raid_stop"); },
                "reallife_roommate_reply_replace", Third(() => d.RaidReplace() ? "reallife_roommate_talk_raid_replace" : "reallife_roommate_talk_raid_grumble"));
        }

        private static DialogEntry Borrow(RoommateDirector d, RoommateState s)
        {
            Fill f = Us.Cost(s.BorrowAsk).On(RoommateText.When(RoommateGame.Day + RoommateRules.BorrowDays));
            return Ask(RoommateText.Say("reallife_roommate_talk_borrow", f),
                "reallife_roommate_reply_lend", () => Close(d.BorrowYes() ? "reallife_roommate_talk_borrow_yes" : "reallife_roommate_talk_borrow_short", f),
                "reallife_roommate_reply_cannot", () => { d.BorrowNo(); return Close("reallife_roommate_talk_borrow_no"); });
        }

        private static DialogEntry Cooked(RoommateDirector d)
        {
            return Ask(RoommateText.Say("reallife_roommate_talk_cooked", Us),
                "reallife_roommate_reply_eat", () => Close(d.CookedEat() ? "reallife_roommate_talk_cooked_eat" : "reallife_roommate_talk_cooked_gone"),
                "reallife_roommate_reply_later", () => { d.CookedLater(); return Close("reallife_roommate_talk_cooked_later"); });
        }

        private static DialogEntry TheirDay(RoommateDirector d, RoommateState s)
        {
            if (s.LastTalkDay == RoommateGame.Day)
            {
                return Close("reallife_roommate_talk_later");
            }
            string line = DayLines[RoommateRules.Roll(s.Seed, RoommateGame.Day, 21) % DayLines.Length];
            return Ask(RoommateText.Say(line, Us.With(RoommateText.Job(s.Mate.Person))),
                "reallife_roommate_reply_warm", () => { d.DayWarm(); return Close("reallife_roommate_talk_day_warm"); },
                "reallife_roommate_reply_short", () => { d.DayShort(); return Close("reallife_roommate_talk_day_short"); });
        }
    }

    /// <summary>The face-to-face conversation: whatever <see cref="RoommateTalk.Open"/> was given.</summary>
    public class RoommateDialog : Dialog
    {
        public RoommateDialog()
        {
            npcNameKey = RoommatePhone.ContactId;
            DialogController.current.ShowEntry(Guard.Entry(RoommateTalk.TakeFirst()));
        }
    }
}
