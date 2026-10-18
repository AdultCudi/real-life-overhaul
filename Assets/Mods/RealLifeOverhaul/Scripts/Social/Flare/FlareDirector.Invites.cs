using System;
using RealLifeOverhaul.Texts;
using UI.Notification;

namespace RealLifeOverhaul.Flare
{
    /// <summary>What Flare asks of the social life for an invitation in a message (Wiring/FlareInviteLinks). Without it every invitation is a maybe.</summary>
    public static partial class FlareBridges
    {
        /// <summary>Fills what the social life knows for the decision and, for a yes, the place and the time.</summary>
        public static Action<FlareInviteAsk> InviteLook;

        /// <summary>Books a yes's plan as a call does (somebody new is met online first): their place in the cast, or -1.</summary>
        public static Func<FlareInviteBook, int> InviteBook;
    }

    /// <summary>
    /// Flare's director, an invitation in the player's message to a city person or an influencer (FlareInviteRules): read
    /// when sent, decided at once by the social life's rules, answered at its minute, a yes booked a moment before it shows
    /// and marked in the save. Nothing is sent in a session. Cost: a pass over each message sent; one comparison a second.
    /// </summary>
    public sealed partial class FlareDirector
    {
        private long _inviteDue = -1;              // the minute the next yes is booked; -1 still to be worked out, long.MaxValue none
        private FlareFile _inviteFile;             // the account it was worked out for: a load or a new account works it out again
        private int _testInvitee = -1;             // the test's city person: counts as somebody the player has talked with, this session

        // An invitation in the player's message, decided and scheduled; null when it is none or to somebody the game does not answer for.
        private FlareDm Invite(string with, string text, long now)
        {
            if (!FlareInviteRules.Read(text, out PlanKind kind) || !FlareTextsMap.DmSpeaker(with, out bool influencer, out int number))
            {
                return null;
            }
            var a = new FlareInviteAsk
            {
                Kind = kind, Influencer = influencer, Hour = Save.Hour, Standing = FlareBridges.Standing(), Reputation = _file.Reputation,
                Roll = FlareInviteRules.Roll(_file.Seed, now), Chats = FlareInviteRules.Chats(_file.Net.Dms, with, now)
            };
            if (!influencer)
            {
                if (number < 0 || number >= _citizens.Count)
                {
                    return null;
                }
                FlareCitizen c = _citizens[number];
                int citySeed = FlareLiveRules.CitySeed(_file);
                a.First = c.First;
                a.Last = c.Last;
                a.Sex = FlareInviteRules.SexOf(c);
                a.Age = FlareTextsMap.Age(c, citySeed);
                a.Face = FlareAvatarRules.Citizen(citySeed, number).Seed;   // the face they have on Flare, kept when they are met
                a.NightOwl = c.Persona == FlarePersona.NightOwl;
                a.Grumpy = c.Persona == FlarePersona.Grumpy;
                a.Taken = FlareInviteRules.Taken(citySeed, number) && number != _testInvitee;
                a.Chats = number == _testInvitee ? Math.Max(a.Chats, FlareInviteRules.ChatsForYes) : a.Chats;
            }
            long yesAt = FlareInviteRules.AnswerAt(now, _file.Seed, _file.Net.NextDm, true);
            a.From = FlareInviteRules.PlanFrom(yesAt);
            a.Off = FlareBridges.InviteLook == null;
            FlareBridges.InviteLook?.Invoke(a);
            FlareDm waiting = FlareInviteRules.WaitingWith(_file.Net.Dms, null);   // a yes on its way is the player's plan already: one at a time
            if (waiting != null && waiting.With == with)
            {
                a.PlanWithThem = true;
                a.PlanKind = FlareInviteRules.BookOf(waiting)?.Kind ?? a.Kind;
                a.Outing = FlareInviteRules.Value(waiting.Data, "outing");
                a.Place = FlareInviteRules.Value(waiting.Data, "place");
                a.When = FlareInviteRules.Value(waiting.Data, "time");
                a.CardWhen = FlareInviteRules.Value(waiting.Data, "t_time");
            }
            else if (waiting != null)
            {
                a.PlanOther = true;
            }
            FlareInviteAnswer answer = FlareInviteRules.Decide(a, out FlareInviteWhy why);
            bool yes = answer == FlareInviteAnswer.Yes;
            long at = yes ? yesAt : FlareInviteRules.AnswerAt(now, _file.Seed, _file.Net.NextDm, false);
            FlareDm dm = FlareLiveRules.Dm(_file.Net, with, AccountName(with), at, false, null, FlareInviteRules.Key(answer, why, kind), FlareInviteRules.Data(a, answer, why));
            _inviteDue = yes ? -1 : _inviteDue;   // worked out again on the next second
            Logger?.Info("Flare: an invitation to " + kind + " read in a message to " + AccountName(with) + ": " + answer + " (" + why + ")"
                         + (yes ? ", " + a.Place + " at " + a.CardWhen + ", booked when the answer comes in " + (at - now) + " min" : ", answered in " + (at - now) + " min")
                         + "; talked " + a.Chats + " times, standing " + a.Standing + ".");
            if (yes && Meta.Dev.Verbose)
            {
                bool before = FlareInviteRules.BeforePanel(at, a.PlanAt);
                Logger?.Info("Flare check (invitation): the yes comes at minute " + at + ", the plan's reminder and panel line at " + (a.PlanAt - FlareInviteRules.PanelBefore)
                             + (before ? ": PASS" : ": the answer would come after the panel line: FAIL") + ".");
            }
            return dm;
        }

        // Once a second: one comparison while no yes is due; a yes due is booked a moment before it shows.
        private void InvitesTick()
        {
            if (!Running || Save == null)
            {
                return;
            }
            if (_inviteFile != _file)
            {
                _inviteFile = _file;
                _inviteDue = -1;
            }
            if (_inviteDue < 0)
            {
                _inviteDue = FlareInviteRules.Due(_file.Net.Dms);
            }
            if (_inviteDue == long.MaxValue || Now < _inviteDue)
            {
                return;
            }
            long now = Now;
            for (int i = 0; i < _file.Net.Dms.Count; i++)
            {
                FlareDm m = _file.Net.Dms[i];
                if (FlareInviteRules.Waiting(m) && m.At - FlareInviteRules.BookAhead <= now)
                {
                    Book(m, now);
                }
            }
            _inviteDue = FlareInviteRules.Due(_file.Net.Dms);
            Recount();
            Changed();
        }

        // A yes booked as a call books a plan; one that can no longer be shows as a maybe. Either way never looked at again.
        private void Book(FlareDm m, long now)
        {
            FlareInviteBook b = FlareInviteRules.BookOf(m);
            bool late = b == null || now > b.At - FlareInviteRules.PanelBefore;   // its reminder's hour passed: slept through the answer
            int cast = !late && FlareBridges.InviteBook != null ? FlareBridges.InviteBook(b) : -1;
            if (cast < 0)
            {
                FlareInviteRules.Unbook(m);
                Logger?.Info("Flare: " + m.Name + "'s yes could not be booked any more (" + (late ? "its hour has passed" : "a plan is on already, or the place is gone")
                             + "): it says maybe instead.");
                return;
            }
            m.Data += FlareInviteRules.BookedMark;
            Logger?.Info("Flare: " + m.Name + " said yes; the plan is booked: " + b.Kind + " at " + b.Place + ", minute " + b.At + ".");
            Tell(NotificationType.Info, "reallife_flare_note_invite_booked", _openMessages, "name", m.Name, "outing", FlareInviteRules.Value(m.Data, "outing"),
                "place", b.Place, "time", FlareInviteRules.Value(m.Data, "time"));
        }

        // RLO Texts: an invitation's answer is asked with its own intent and card values: a v3 pack's decided answer, a v2 pack none.
        private static void InviteCard(FlareDm answer, int data, TextRequest r)
        {
            if (FlareInviteRules.AnswerOf(answer.Key, out _))
            {
                r.Intent = FlareInviteRules.TrainedIntent(answer.Key, data);
                FlareInviteRules.Slots(answer, r.Slots);
            }
        }

        /// <summary>Test: a city person writes now and counts as known a while, so an invitation to them is decided so.</summary>
        public void TestInvitee()
        {
            if (!TestReady("Flare invitation answered"))
            {
                return;
            }
            Guarded(() =>
            {
                if (_citizens.Count == 0)
                {
                    TestRefusals.Tell(Logger, "Flare invitation answered", TestRefusal.FeatureOff);
                    return;
                }
                int number = Save.Day % _citizens.Count;
                string with = "c" + number;
                _testInvitee = number;
                FlareLiveRules.Dm(_file.Net, with, AccountName(with), Now, false, null, "reallife_flare_dm_chat", null);
                Recount();
                Tell(NotificationType.Info, "reallife_flare_test_invitee_done", _openMessages, "name", AccountName(with));
                Logger?.Info("Flare: test: " + AccountName(with) + " wrote to the player and counts as somebody talked with; invite them out in a message.");
                Changed();
            });
        }
    }
}
