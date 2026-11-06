using System;
using System.Collections.Generic;
using UI.Notification;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Fame
{
    /// <summary>
    /// Connections and power (FameCity has the rules): the city's requests, evenings, lobbying the council, and
    /// bribes asked on the Business desk.
    /// </summary>
    public sealed partial class FameDirector
    {
        private static readonly string[] RequestTransactions =
        {
            "reallife_transaction_fame_sponsor", "reallife_transaction_fame_charity", "reallife_transaction_fame_rescue", "reallife_transaction_fame_complaint",
            "reallife_transaction_fame_partnership"
        };

        private int _eventDone;          // the evening's choices taken, a bit each
        private string _eventSaid;       // what came of the last one

        private void CityDay(int day)
        {
            int lapsed = FameCity.RequestsDay(_file, day);
            if (lapsed > 0)
            {
                Logger?.Info("Standing: " + lapsed + " request(s) lapsed or were called off.");
            }
            List<BuildingRegistration> shops = FameGame.Businesses();
            string place = shops.Count > 0 ? FameGame.Place(shops[Random.Range(0, shops.Count)]) : null;
            FameRequest request = FameCity.RequestRoll(_file, day, Random.Range(0, 1000), Random.Range(0, 1000), FameGame.BusiestDistrict(), place);
            if (request != null)
            {
                Requested(request);
            }
            if (Settings.StandingPress)
            {
                FameInvite invite = FameCity.EventRoll(_file, day, Random.Range(0, 1000), Random.Range(0, 1000));
                if (invite != null)
                {
                    Invited(invite);
                }
            }
            FameLobby pending = FameCity.Pending(_file);
            if (pending != null)
            {
                FameStory story = FameCity.Decide(_file, pending, day, Random.Range(0, 100));
                if (story != FameStory.None)
                {
                    Decided(pending, story, day);
                }
            }
            if (Settings.StandingCorruption)
            {
                FameBribe bribe = FameCity.Surface(_file, day, Random.Range(0, 1000));
                if (bribe != null)
                {
                    Surfaced(bribe, day);
                }
            }
        }

        // A request of the city's: on the Standing page either way, and a notification unless the player has switched those off.
        private void Requested(FameRequest r)
        {
            Logger?.Info("Standing: a request (" + r.Kind + ", " + r.Amount + (r.District != null ? ", " + r.District : string.Empty) + "), to be answered by day " + r.Until + "."
                         + (Settings.StandingRequestNotes ? string.Empty : " No notification: they are switched off."));
            if (Settings.StandingRequestNotes)
            {
                Tell(NotificationType.Info, "reallife_fame_note_request", _openPage, "what", FameText.RequestTitle(r), "day", FameText.N(r.Until));
            }
        }

        /// <summary>The player answers a request on the Standing page; an accepted one is paid for first.</summary>
        public void AnswerRequest(int id, bool accept)
        {
            FameRequest r = Running ? FameCity.Request(_file, id) : null;
            if (r == null || r.State != FameRequestState.Open || Save == null)
            {
                return;
            }
            if (accept && !FameGame.Pay(r.Amount, RequestTransactions[FameRules.Clamp((int)r.Kind, 0, RequestTransactions.Length - 1)]))
            {
                return;   // the game has said there is not enough money
            }
            int day = Save.Day;
            FameStory story = FameCity.Answer(_file, r, accept, day);
            Logger?.Info("Standing: the request (" + r.Kind + ") is " + r.State + (accept ? ", " + r.Amount + " paid" : string.Empty) + "; customers " + _file.Rep[0]
                         + ", neighbourhoods " + _file.Rep[2] + ", integrity " + _file.Rep[3] + ".");
            Print(story, day);
        }

        /// <summary>The player lobbies the council for one change from the Standing page; the lobbyist's fee is paid first.</summary>
        public void Lobby(int lever, int position)
        {
            if (!Running || Save == null || !FameCity.MayLobby(_file, Save.Day))
            {
                return;
            }
            int fee = FameCity.LobbyFee(_file.Weekly);
            if (!FameGame.Pay(fee, "reallife_transaction_fame_lobby"))
            {
                return;
            }
            FameLobby l = FameCity.Lobby(_file, lever, position, Save.Day, Random.Range(0, 1000));
            Logger?.Info("Standing: the player lobbies the council (lever " + lever + " to " + position + ", " + fee + " to a lobbyist): " + l.Chance
                         + "% after the other side's " + l.Rival + " points, decided on day " + l.DecideDay + ".");
        }

        private void Decided(FameLobby l, FameStory story, int day)
        {
            bool won = l.State == FameLobbyState.Won;
            bool scheduled = won && FameBridges.Lobbied != null && FameBridges.Lobbied(l.Lever, l.Position, day + FameCity.LobbyApplyDays);
            Logger?.Info("Standing: the council " + (won ? "agrees" : "says no") + " (lever " + l.Lever + " to " + l.Position + ", " + l.Chance + "%)"
                         + (won ? scheduled ? ": in force in " + FameCity.LobbyApplyDays + " days." : ": but it stands there already." : "."));
            Print(story, day);
            string rival = FameGame.Rival(l.Id) ?? FameText.T("reallife_fame_rival_default");
            Tell(won ? NotificationType.Success : NotificationType.Info, won ? "reallife_fame_note_lobby_won" : "reallife_fame_note_lobby_lost", _openPage,
                "rival", rival, "days", FameText.N(FameCity.LobbyApplyDays));
        }

        /// <summary>The bribe an official asks at this issue, or 0; the same answer for the same issue.</summary>
        public int BribeAsk(string kind, string place, int ask, int seed)
        {
            if (!Running || Save == null || !Settings.StandingCorruption || ask <= 0 || FameCity.InScandal(_file, Save.Day))
            {
                return 0;
            }
            return FameCity.BribeAsked(seed ^ _file.Seed) ? ask : 0;
        }

        public void Bribed(string kind, string place, int amount)
        {
            if (!Running || Save == null)
            {
                return;
            }
            FameBribe b = FameCity.Bribed(_file, place, amount, Save.Day);
            Logger?.Info("Standing: a bribe of " + amount + " paid at " + place + " (" + kind + "); integrity " + _file.Rep[(int)FamePart.Integrity] + ". It may come out from day "
                         + (b.Day + FameCity.SurfaceAfterDays) + ".");
        }

        private void Surfaced(FameBribe b, int day)
        {
            int number = 0;
            try
            {
                number = FameBridges.Investigate != null ? FameBridges.Investigate(b.Amount) : 0;
            }
            catch (Exception e)
            {
                Logger?.Warn("Standing: the court could not be asked to open a case (" + e.Message + ").");
            }
            b.Case = number;
            Logger?.Info("Standing: the bribe of day " + b.Day + " (" + b.Amount + ") came out: a scandal until day " + _file.ScandalUntil
                         + (number > 0 ? ", case " + number + " before the court" : ", no court to hear it") + "; integrity " + _file.Rep[(int)FamePart.Integrity] + ".");
            Print(FameStory.Investigation, day);
            Tell(NotificationType.Error, number > 0 ? "reallife_fame_note_scandal_case" : "reallife_fame_note_scandal", _openPage, "days", FameText.N(FameCity.ScandalDays));
        }

        private void EveningBegin(FameInvite invite)
        {
            _eventDone = 0;
            _eventSaid = null;
            Logger?.Info("Standing: the " + invite.Kind + " begins.");
            EventPage();
        }

        // Three ways to spend the evening, two of them once each; the last page says what came of it.
        private void EventPage()
        {
            bool investors = _scene.Kind == FameInviteKind.Investors;
            string title = FameText.InviteName(_scene.Kind);
            string place = FameText.T(investors ? "reallife_fame_call_place_0" : "reallife_fame_call_place_1");
            string speaker = FameText.T(investors ? "reallife_fame_event_host_0" : "reallife_fame_event_host_1");
            if ((_eventDone & 3) == 3)
            {
                _panel.Show(title, place, speaker, _eventSaid + "\n" + FameText.T("reallife_fame_event_end"), new[] { FameText.T("reallife_fame_scene_leave") }, null, 1,
                    _ => SceneOver(true));
                return;
            }
            string body = _eventSaid != null ? _eventSaid + "\n" + FameText.T("reallife_fame_event_more") : FameText.T(investors ? "reallife_fame_call_open_0" : "reallife_fame_call_open_1");
            var names = new List<string>(3);
            var hints = new List<string>(3);
            var picks = new List<int>(3);
            if ((_eventDone & 1) == 0)
            {
                names.Add(FameText.T(investors ? "reallife_fame_event_talk_0" : "reallife_fame_event_talk_1"));
                hints.Add(FameText.T(investors ? "reallife_fame_event_talk_hint_0" : "reallife_fame_event_talk_hint_1"));
                picks.Add(0);
            }
            if ((_eventDone & 2) == 0)
            {
                names.Add(FameText.T("reallife_fame_event_meet"));
                hints.Add(FameText.T("reallife_fame_event_meet_hint"));
                picks.Add(1);
            }
            names.Add(FameText.T("reallife_fame_event_leave"));
            hints.Add(FameText.T("reallife_fame_event_leave_hint"));
            picks.Add(2);
            _panel.Show(title, place, speaker, body, names.ToArray(), hints.ToArray(), names.Count, id => EventChoice(picks[id]));
        }

        private void EventChoice(int choice)
        {
            if (_scene == null || Save == null)
            {
                return;
            }
            if (choice == 2)
            {
                SceneOver(true);
                return;
            }
            _eventDone |= 1 << choice;
            if (choice == 0)
            {
                EventTalk(Save.Day);
            }
            else
            {
                EventMeet();
            }
            EventPage();
        }

        private void EventTalk(int day)
        {
            FameRules.Publicity(_file, 8, day, FameCause.Event);
            if (_scene.Kind != FameInviteKind.Investors)
            {
                _file.LobbyCredit = FameCity.CreditFromReception;
                _eventSaid = FameText.T("reallife_fame_event_talked_1");
                Logger?.Info("Standing: the reception: a word with the council's people (influence +" + FameCity.CreditFromReception + " for the next push).");
                return;
            }
            List<BuildingRegistration> shops = FameGame.Businesses();
            FameRequest r = shops.Count > 0 && FameCity.Open(_file) == null && !FameCity.InScandal(_file, day)
                ? FameCity.Offer(_file, FameRequestKind.Partnership, day, Random.Range(0, 1000), FameGame.BusiestDistrict(), FameGame.Place(shops[Random.Range(0, shops.Count)]))
                : null;
            _eventSaid = FameText.T(r != null ? "reallife_fame_event_partner" : "reallife_fame_event_talked_0");
            Logger?.Info("Standing: the evening with investors: the player talked business" + (r != null ? ", and another owner offers a partnership." : "."));
        }

        private void EventMeet()
        {
            string name = null;
            try
            {
                name = FameBridges.Meet != null ? FameBridges.Meet() : null;
            }
            catch (Exception e)
            {
                Logger?.Warn("Standing: nobody could be met at the evening (" + e.Message + ").");
            }
            _eventSaid = name != null ? FameText.Fill("reallife_fame_event_met", "name", name) : FameText.T("reallife_fame_event_nobody");
            Logger?.Info("Standing: the evening: " + (name != null ? "the player met " + name + "." : "nobody new."));
        }

        private void EveningOver(FameInvite invite, bool finished)
        {
            if (finished && Save != null)
            {
                Print(FameStory.Event, Save.Day);
            }
            Logger?.Info("Standing: the " + invite.Kind + " is over" + (finished ? "." : " (the screen went with the game's interface)."));
        }
    }
}
