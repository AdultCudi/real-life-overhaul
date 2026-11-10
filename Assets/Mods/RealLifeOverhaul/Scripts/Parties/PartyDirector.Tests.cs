using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Parties
{
    /// <summary>
    /// Parties' entries in Options, Tests, for what the owner would otherwise wait days for: a friend asks the player to a
    /// party now; whatever is planned next starts this hour; and the day's round runs now. Each says in a notice what it
    /// did, or why it could not.
    /// </summary>
    public sealed partial class PartyDirector
    {
        // Why no test can run, as a notice's key; null when one can.
        private string TestReady()
        {
            return !On ? "reallife_party_no_off" : _file == null || Save == null ? "reallife_party_no_save" : _world.Guest ? "reallife_party_no_guest" : null;
        }

        private static void Say(NotificationType type, string key, params string[] pairs)
        {
            var data = new Dictionary<string, string>(pairs.Length / 2);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                data[pairs[i]] = pairs[i + 1];
            }
            Notifications.Show(type, key, data, 10f, null, null, true, false);
        }

        /// <summary>A friend who can host asks the player to a party that starts this hour, at a place the city has open then.</summary>
        public void TestInvite()
        {
            try
            {
                string why = TestReady();
                if (why != null)
                {
                    Say(NotificationType.Warning, why);
                    return;
                }
                SocialPerson host = null;
                for (int i = 0; i < _world.People && host == null; i++)
                {
                    SocialPerson p = _world.Person(i);
                    host = p != null && p.Met && !p.Drifted && !p.IsFamily && p.Stage >= SocialStage.Friend ? p : null;
                }
                int now = _world.Now, day = now / 1440, hour = now % 1440 / 60;
                PartyPlace kind = PartyPlace.Restaurant;
                PlaceFacts place = host != null ? _world.FindVenue(kind, PartyRules.Stamp(day, hour), PartyCatalogue.InviteMinutes) : default(PlaceFacts);
                if (host != null && (!place.Found || !place.Open))
                {
                    kind = PartyPlace.Bar;
                    place = _world.FindVenue(kind, PartyRules.Stamp(day, hour), PartyCatalogue.InviteMinutes);
                }
                if (host == null || !place.Found || !place.Open)
                {
                    Say(NotificationType.Warning, host == null ? "reallife_party_test_no_friend" : "reallife_party_test_no_place");
                    return;
                }
                var invite = new Invite
                {
                    Id = _file.NextId++, Person = host.Index, State = InviteState.Asked, Asked = day, Day = day, Hour = hour, Street = place.Street ?? string.Empty,
                    Number = place.Number, Name = place.Name ?? string.Empty, Place = kind
                };
                _file.Invites.Add(invite);
                Touch();
                Say(NotificationType.Success, "reallife_party_test_invite_done", "name", host.Name, "place", invite.Name);
                Check("test invitation", host.Name + " asks to " + invite.Name + " now, invitation " + invite.Id, _file.Invitation(invite.Id) != null);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Whatever is planned next (a party of the player's, an invitation they said yes to, a company event) is moved to start this hour.</summary>
        public void TestNow()
        {
            try
            {
                string why = TestReady();
                if (why != null)
                {
                    Say(NotificationType.Warning, why);
                    return;
                }
                int now = _world.Now, day = now / 1440, hour = now % 1440 / 60, moved = 0;
                for (int i = 0; i < _file.Parties.Count && moved == 0; i++)
                {
                    if (_file.Parties[i].State == PartyState.Planned)
                    {
                        _file.Parties[i].Day = day;
                        _file.Parties[i].Hour = hour;
                        moved++;
                    }
                }
                for (int i = 0; i < _file.Invites.Count && moved == 0; i++)
                {
                    if (_file.Invites[i].State == InviteState.Accepted && _file.Invites[i].Began < 0)
                    {
                        _file.Invites[i].Day = day;
                        _file.Invites[i].Hour = hour;
                        moved++;
                    }
                }
                for (int i = 0; i < _file.Events.Count && moved == 0; i++)
                {
                    if (_file.Events[i].State == StaffEventState.Planned)
                    {
                        _file.Events[i].Day = day;
                        _file.Events[i].Hour = hour;
                        moved++;
                    }
                }
                _minute = -1;   // looked at with the next second
                Touch();
                Say(moved > 0 ? NotificationType.Success : NotificationType.Warning, moved > 0 ? "reallife_party_test_now_done" : "reallife_party_test_now_none");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>The day's round now: the lifts fade, the managers book what is due, and an invitation is drawn as if the gap were over.</summary>
        public void TestRound()
        {
            try
            {
                string why = TestReady();
                if (why != null)
                {
                    Say(NotificationType.Warning, why);
                    return;
                }
                int events = _file.Events.Count, invites = _file.Invites.Count, lifts = _file.Lifts.Count;
                _file.Settled = -1;
                _file.LastInvite = -1000;
                PartyDesk.Day(_file, _world, PartyCatalogue.RoundHour);
                Touch();
                Say(NotificationType.Info, "reallife_party_test_round_done", "events", PartyText.Whole(_file.Events.Count - events),
                    "invites", PartyText.Whole(_file.Invites.Count - invites), "lifts", PartyText.Whole(_file.Lifts.Count));
                Check("test round", "the round ran on day " + _world.Day + ": " + (_file.Events.Count - events) + " event(s) booked by managers, "
                                    + (_file.Invites.Count - invites) + " invitation(s), " + lifts + " -> " + _file.Lifts.Count + " lifts", _file.Settled == _world.Day);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
