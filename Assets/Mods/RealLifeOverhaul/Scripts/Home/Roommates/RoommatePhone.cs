using System;
using System.Collections.Generic;
using BAModAPI;
using Dialogs;
using Entities;
using Localizor;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul.Roommates
{
    /// <summary>
    /// The "Roommate" contact in the game's own Contacts app: where a notice is put up or a friend is asked, where the
    /// people who answered are read through and one of them is offered the room, and, once somebody lives in, how
    /// things stand, a call to them, and notice. The game's own call panel throughout; text messages from the contact
    /// say what happened while the player was out.
    ///
    /// Everything here is built when the player rings the contact or presses a button of the call. Nothing runs otherwise.
    /// </summary>
    public static class RoommatePhone
    {
        public const string ContactId = "reallife-roommate";
        private const string DialogTypeName = "reallife_roommate_calldialogtype";

        private static readonly List<SocialPerson> Friends = new List<SocialPerson>(SocialRules.CastSize);
        /// <summary>The contact, once it has been added to this save's phone; null before, and with the phone's contacts off.</summary>
        public static Contact Contact { get; private set; }

        public static void Reset()
        {
            Contact = null;
        }

        /// <summary>
        /// The contact's picture from the game's own assets (Phone/Contacts): the roommate's own face, from the look they are
        /// dressed in at home (RoommateCast.Dress), while somebody lives in; the catalogue's logo of a room to let while nobody does.
        /// Asked when the contact is set up and when somebody moves in or out.
        /// </summary>
        public static void Picture()
        {
            RoommateDirector d = RoommateDirector.Instance;
            Flatmate mate = d != null && d.State != null ? d.State.Mate : null;
            if (mate != null && mate.Person != null)
            {
                ContactPictures.Person(ContactId, mate.Person.Female, Math.Max(18, Math.Min(80, mate.Person.Age)), mate.Person.Seed != 0 ? mate.Person.Seed : 1);
                return;
            }
            ContactPictures.Add(ContactId);
        }

        /// <summary>Adds the contact (or finds it again in a loaded save) and gives it its call. Safe to call again.</summary>
        public static void Setup()
        {
            Contact = null;
            if (!RoommateSettings.Enabled || !Settings.PhoneContacts || SaveGameManager.Current == null)
            {
                return;
            }
            try
            {
                Picture();
                Contact contact = Contact.GetContact(ContactId, ContactCategoryName.General, "reallife_contact_roommate_desc", null, false, true);
                var type = (CallDialogType)ModEnumHash.GetSafeHash(DialogTypeName);
                contact.callDialogTypeOverride = type;   // not saved by the game, so set on every load
                CallDialogFactory.RegisterDialog(type, Guard.Maker(() => new RoommateCallDialog()));
                if (contact.messagesQueue == null || contact.messagesQueue.Count == 0)
                {
                    contact.SendMessage(new TextMessage("reallife_text_roommate_welcome"), notify: false);
                }
                Contact = contact;
            }
            catch (Exception e)
            {
                RoommateDirector.Logger?.Error(e);
                RoommateDirector.Logger?.Warn("Roommates: the Roommate contact could not be added; what it would have said is shown as notices instead.");
            }
        }

        // ---------------------------------------------------------------- the call

        /// <summary>The first page of a call, by how things stand.</summary>
        internal static DialogEntry Menu()
        {
            RoommateDirector d = RoommateDirector.Instance;
            if (d == null || !d.Running)
            {
                return RoommateTalk.Close("reallife_roommate_phone_off", default);
            }
            RoommateState s = d.State;
            return s.Living ? Living(d, s) : s.AdUp ? Notice(d, s) : Empty(d);
        }

        private static DialogEntry Entry(Localizor.LanguageChangeEvent.LanguageChangeEventDataHolder text, string firstKey, Func<DialogEntry> first, string secondKey, Func<DialogEntry> second)
        {
            return new DialogEntry
            {
                messageData = text,
                ConfirmTextOverride = firstKey.Localize(),
                OnConfirm = RoommateTalk.Safe(first),
                SecondOptionTextOverride = second != null ? secondKey : null,
                OnSecondOption = second != null ? RoommateTalk.Safe(second) : null,
                OnCancel = RoommateTalk.Leave
            };
        }

        // Nobody lives in and no notice is up: what the home offers, a notice, a friend.
        private static DialogEntry Empty(RoommateDirector d)
        {
            RoomKind room = d.RoomNow(out int weekly, out bool lodger);
            string key = room == RoomKind.Bed ? (lodger ? "reallife_roommate_page_empty_lodger" : "reallife_roommate_page_empty_bed")
                : room == RoomKind.Sofa ? "reallife_roommate_page_empty_sofa" : "reallife_roommate_page_empty_none";
            Fill f = new Fill().Cost(weekly).Count(RoommateRules.SofaPct);
            if (room == RoomKind.None)
            {
                return RoommateTalk.Close(d.HasHome ? key : "reallife_roommate_page_no_home", f);
            }
            return Entry(RoommateText.Say(key, f),
                "reallife_roommate_do_advertise", () =>
                {
                    string refusal = d.Advertise();
                    return RoommateTalk.Close(refusal ?? "reallife_roommate_ad_up", new Fill().Cost(RoommateRules.AdFee).Count(RoommateRules.AdDays));
                },
                "reallife_roommate_do_ask_friend", () => Friend(d, 0, true));
        }

        // The friends who could be asked, one at a time.
        private static DialogEntry Friend(RoommateDirector d, int index, bool first)
        {
            if (first)
            {
                Friends.Clear();
                if (RoommateBridges.Friends != null)
                {
                    RoommateBridges.Friends(Friends);
                }
            }
            if (Friends.Count == 0)
            {
                return RoommateTalk.Close("reallife_roommate_friend_none", default);
            }
            SocialPerson friend = Friends[index % Friends.Count];
            RoomKind room = d.RoomNow(out _, out _);
            Flatmate as_ = RoommateRules.Friend(friend, room);
            int weekly = d.WeeklyFor(as_.SharePct);
            Fill f = Fill.For(friend.Name).Cost(weekly).With(RoommateText.Traits(as_.Traits)).And(RoommateText.Job(as_.Person));
            return Entry(RoommateText.Say("reallife_roommate_friend_page", f),
                "reallife_roommate_do_ask", () =>
                {
                    string refusal = d.AskFriend(friend);
                    return RoommateTalk.Close(refusal ?? "reallife_roommate_friend_yes", f);
                },
                "reallife_roommate_do_someone_else", Friends.Count > 1 ? () => Friend(d, index + 1, false) : (Func<DialogEntry>)null);
        }

        // A notice is up: how long for, who has answered.
        private static DialogEntry Notice(RoommateDirector d, RoommateState s)
        {
            int answered = 0;
            for (int i = 0; i < s.Candidates.Count; i++)
            {
                answered += s.Candidates[i].Told ? 1 : 0;
            }
            int until = s.AdStamp / 24 + RoommateRules.AdDays;
            Fill f = new Fill().Count(answered).On(RoommateText.When(until));
            return Entry(RoommateText.Say(answered > 0 ? "reallife_roommate_page_ad" : "reallife_roommate_page_ad_quiet", f),
                answered > 0 ? "reallife_roommate_do_see" : "reallife_roommate_do_take_down",
                answered > 0 ? () => Applicant(d, First(s, 0)) : (Func<DialogEntry>)(() => TakeDown(d)),
                "reallife_roommate_do_take_down", answered > 0 ? () => TakeDown(d) : (Func<DialogEntry>)null);
        }

        private static DialogEntry TakeDown(RoommateDirector d)
        {
            d.WithdrawAd();
            return RoommateTalk.Close("reallife_roommate_ad_down", default);
        }

        // The first applicant from a place on whose answer has come; -1 if nobody's has.
        private static int First(RoommateState s, int from)
        {
            for (int i = 0; i < s.Candidates.Count; i++)
            {
                int k = (from + i) % s.Candidates.Count;
                if (s.Candidates[k].Told)
                {
                    return k;
                }
            }
            return -1;
        }

        // One applicant: who they are, what they offer, what shows of them.
        private static DialogEntry Applicant(RoommateDirector d, int index)
        {
            RoommateState s = d.State;
            if (index < 0 || index >= s.Candidates.Count || !s.AdUp)
            {
                return RoommateTalk.Close("reallife_roommate_page_ad_quiet", new Fill().On(RoommateText.When(s.AdStamp / 24 + RoommateRules.AdDays)));
            }
            Flatmate c = s.Candidates[index];
            int weekly = d.WeeklyFor(c.SharePct);
            var text = "reallife_roommate_applicant".Localize(new
            {
                name = c.Person.Name,
                age = c.Person.Age.ToString(),
                job = RoommateText.Job(c.Person),
                hours = RoommateText.Hours(c.Person),
                amount = RoommateText.Money(weekly),
                share = c.SharePct.ToString(),
                traits = RoommateText.Traits(c.Known)
            });
            int next = First(s, index + 1);
            return Entry(text,
                "reallife_roommate_do_offer", () =>
                {
                    string name = c.Person.Name;
                    string refusal = d.Offer(c);
                    return RoommateTalk.Close(refusal ?? "reallife_roommate_moved_in", Fill.For(name).Cost(weekly).On(RoommateText.When(d.State.RentDay)));
                },
                "reallife_roommate_do_next", next >= 0 && next != index ? () => Applicant(d, next) : (Func<DialogEntry>)null);
        }

        // Somebody lives in: how things stand, a call to them, notice.
        private static DialogEntry Living(RoommateDirector d, RoommateState s)
        {
            Flatmate m = s.Mate;
            string status = s.OnNotice
                ? RoommateText.T("reallife_roommate_status_notice", Fill.For(m.Person.Name).On(RoommateText.When(s.LeaveDay)).With(RoommateText.T(RoommateText.WhyKey(s.Notice))))
                : s.Owed > 0 ? RoommateText.T("reallife_roommate_status_owes", Fill.For(m.Person.Name).Cost(s.Owed).On(RoommateText.When(s.OwedDue)))
                : RoommateText.T("reallife_roommate_status_fine", Fill.For(m.Person.Name));
            var text = (s.Lodger ? "reallife_roommate_page_lodger" : "reallife_roommate_page_living").Localize(new
            {
                name = m.Person.Name,
                job = RoommateText.Job(m.Person),
                hours = RoommateText.Hours(m.Person),
                days = Math.Max(0, RoommateGame.Day - s.MovedIn).ToString(),
                amount = RoommateText.Money(d.WeeklyFor(m.SharePct)),
                day = RoommateText.When(s.RentDay),
                level = RoommateText.T(RoommateText.LevelWord(d.Level)),
                traits = RoommateText.Traits(m.Known),
                where = RoommateText.T(WhereKey(d.WhereNow)),
                status
            });
            bool mine = s.Notice == EndWhy.PlayerNotice;
            return Entry(text,
                "reallife_roommate_do_talk", () => d.WhereNow == RoomWhere.Asleep ? RoommateTalk.Close("reallife_roommate_phone_asleep") : RoommateTalk.Next(),
                mine ? "reallife_roommate_do_take_back" : "reallife_roommate_do_notice",
                mine ? () => RoommateTalk.Close(d.TakeBackNotice() ? "reallife_roommate_notice_back" : "reallife_roommate_notice_back_no")
                : !s.OnNotice ? () => ConfirmNotice(d) : (Func<DialogEntry>)null);
        }

        private static DialogEntry ConfirmNotice(RoommateDirector d)
        {
            Fill f = Fill.For(d.MateName).Count(RoommateRules.NoticeDays);
            return Entry(RoommateText.Say("reallife_roommate_notice_ask", f),
                "reallife_roommate_do_notice_yes", () =>
                {
                    d.GiveNoticeByPlayer();
                    return RoommateTalk.Close("reallife_roommate_notice_given", Fill.For(d.MateName).On(RoommateText.When(d.State.LeaveDay)));
                },
                "reallife_roommate_do_notice_no", () => RoommateTalk.Close("reallife_roommate_notice_not"));
        }

        private static string WhereKey(RoomWhere where)
        {
            switch (where)
            {
                case RoomWhere.Asleep: return "reallife_roommate_where_asleep";
                case RoomWhere.Work: return "reallife_roommate_where_work";
                case RoomWhere.Out: return "reallife_roommate_where_out";
                case RoomWhere.InRoom: return "reallife_roommate_where_room";
                default: return "reallife_roommate_where_in";
            }
        }
    }

    /// <summary>A call to the Roommate contact.</summary>
    public class RoommateCallDialog : Dialog
    {
        public RoommateCallDialog()
        {
            npcNameKey = RoommatePhone.ContactId;
            DialogController.current.ShowEntry(Guard.Entry(RoommateTalk.Safe(RoommatePhone.Menu)()));
        }
    }
}
