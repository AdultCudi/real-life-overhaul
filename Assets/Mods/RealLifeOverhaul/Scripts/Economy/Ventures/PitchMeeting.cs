using System;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>A meeting with an investor in the game's own list window, locked until it is over. It runs only on clicks: no frame cost.</summary>
    internal sealed class PitchMeeting
    {
        private const NavigationBlocker Blocker = (NavigationBlocker)7303;   // our own value, so nobody else's is ever lifted
        private const float InputDelay = 0.4f;                               // seconds after a page appears before a click counts
        private const int Leave = 3;
        private const int TakeIt = 0, ThinkItOver = 1, Decline = 2;
        private const string ButtonSay = "reallife_ven_meet_say";
        private const string ButtonDo = "reallife_ven_meet_do";

        private readonly VenturesDirector _director;
        private readonly DeskList _window = new DeskList("RealLifeOverhaul Ventures", 1250f, 150f);
        private readonly long[] _values = new long[3];
        private readonly int[] _order = new int[3];
        private readonly Action<int> _onPick;
        private readonly Action _onClosed;

        private PitchState _state;
        private Investor _investor;
        private PlayerController _held;
        private int _count;            // answers on the page
        private int _offerId;          // the offer the decision made; 0: none
        private bool _result;          // the page is the decision (or "caught"), not a question
        private bool _leaving;         // closed by us
        private float _shownAt;

        public PitchMeeting(VenturesDirector director)
        {
            _director = director;
            _onPick = Picked;
            _onClosed = Closed;
        }

        public bool Active { get; private set; }

        /// <summary>Starts the meeting. False, and nothing shown, if the game's window cannot be made or the player cannot be found.</summary>
        public bool Begin(PitchState state, Investor investor)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (Active || player == null || _window.Failed)
            {
                return false;
            }
            _state = state;
            _investor = investor;
            _offerId = 0;
            _result = false;
            Active = true;
            // The game's list stands on the HUD, which the phone's full screen hides.
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.fullMenu != null && FullMenu.IsOpen)
            {
                uis.fullMenu.Toggle(false);
            }
            if (!Show())
            {
                Active = false;
                _state = null;
                return false;
            }
            _held = player;
            player.ResetNavigation();
            player.SetNavigationBlocker(Blocker);
            return true;
        }

        // The question the meeting is at, its answers in this meeting's order. False if the game's window cannot be made.
        private bool Show()
        {
            TravelList list = _window.Take(this);
            VenturesFile file = _director.File;
            if (list == null || file == null)
            {
                return false;
            }
            Question q = Pitch.Bank[_state.Questions[_state.Index]];
            string[] parts = FinanceText.T(q.Key).Split('|');
            string question = parts[0].Replace("{business}", _state.Business);
            string progress = FinanceText.T("reallife_ven_meet_progress", "number", (_state.Index + 1).ToString(), "of", _state.Questions.Count.ToString());
            list.Begin(FinanceText.T("reallife_ven_meet_title", "name", _investor.Name), question + "\n<size=80%>" + progress + "</size>");
            _count = q.Kind == QuestionKind.Fact ? Pitch.Options(q.Fact, _state.Facts, _values) : q.Answers.Length;
            Pitch.Order(file.Seed, _state.Number, _state.Index, _count, _order);
            for (int i = 0; i < _count; i++)
            {
                int option = _order[i];
                string words = q.Kind == QuestionKind.Fact ? Figure(q.Fact, _values[option]) : option + 1 < parts.Length ? parts[option + 1] : string.Empty;
                list.Add(i, FinanceText.T("reallife_ven_meet_answer", "number", (i + 1).ToString()), words, ButtonSay, true, list.HeightFor(words.Length, true));
            }
            string leave = FinanceText.T("reallife_ven_meet_leave_detail", "name", _investor.Name, "day", _investor.OpenDay.ToString());
            list.Add(Leave, FinanceText.T("reallife_ven_meet_leave"), leave, ButtonDo, true, list.HeightFor(leave.Length, true));
            Open(list);
            return true;
        }

        private void Open(TravelList list)
        {
            list.Open(_onPick, _onClosed);
            list.ScrollToTop();
            list.Locked = true;
            _shownAt = Time.unscaledTime;
        }

        // A figure as an answer: money as the game writes it, days and counts as numbers, "none" where less is better.
        private static string Figure(Fact fact, long value)
        {
            if (value == 0 && Pitch.LowerBetter(fact))
            {
                return FinanceText.T("reallife_ven_meet_none");
            }
            switch (Pitch.UnitOf(fact))
            {
                case FactUnit.Money:
                    return value < 0 ? FinanceText.T("reallife_ven_meet_loss", "amount", FinanceText.Money(-value)) : FinanceText.Money(value);
                case FactUnit.Days:
                    return FinanceText.T("reallife_ven_meet_days", "days", value.ToString());
                default:
                    return value.ToString();
            }
        }

        private void Picked(int id)
        {
            try
            {
                if (!Active || Time.unscaledTime - _shownAt < InputDelay)
                {
                    return;
                }
                VenturesFile file = _director.File;
                if (file == null || SaveGameManager.Current == null)
                {
                    End();
                    return;
                }
                if (_result)
                {
                    Decided(id);
                    return;
                }
                if (id == Leave)
                {
                    _director.Detail("pitch " + _state.Number + " with " + _investor.Name + " left at question " + (_state.Index + 1) + " of " + _state.Questions.Count + ": no decision");
                    _director.Tell(NotificationType.Info, "reallife_ven_meet_left", null, "name", _investor.Name, "day", _investor.OpenDay.ToString());
                    End();
                    return;
                }
                if (id < 0 || id >= _count)
                {
                    return;
                }
                int day = SaveGameManager.Current.Day;
                Question q = Pitch.Bank[_state.Questions[_state.Index]];
                int option = _order[id];
                long said = q.Kind == QuestionKind.Fact ? _values[option] : 0;
                AnswerResult result = Pitch.Answer(file, _state, _investor, option, VenturesDirector.Level, day);
                if (Meta.Dev.Verbose)
                {
                    VenturesDirector.Logger?.Info("Ventures check (pitch " + _state.Number + ", question " + _state.Index + " of " + _state.Questions.Count + "): " + q.Key + " (" + q.Kind
                                                  + "), answer " + (option + 1)
                                                  + (q.Kind == QuestionKind.Fact ? " = " + said + " against the true " + Pitch.Truth(q.Fact, _state.Facts) : string.Empty) + ": "
                                                  + (result.Untrue ? "untrue, chance " + result.Chance + "%, roll " + result.Roll + (result.Caught ? ", CAUGHT" : ", not caught")
                                                      : "true or a matter of opinion")
                                                  + ", " + result.Points + " point(s), " + Sum() + " of " + _state.Points.Count * VenturesCatalogue.PointsMost + " so far.");
                }
                if (result.Caught)
                {
                    ShowCaught(q);
                    return;
                }
                _director.Tell(result.Points >= 8 ? NotificationType.Success : result.Points >= 4 ? NotificationType.Info : NotificationType.Warning,
                    result.Points >= 8 ? "reallife_ven_meet_react_good" : result.Points >= 4 ? "reallife_ven_meet_react_fair" : "reallife_ven_meet_react_poor", null, "name", _investor.Name);
                if (Pitch.Over(_state))
                {
                    ShowDecision(file, day);
                }
                else if (!Show())
                {
                    End();
                }
            }
            catch (Exception e)
            {
                End();
                _director.MeetingFault(e);
            }
        }

        private int Sum()
        {
            int sum = 0;
            for (int i = 0; i < _state.Points.Count; i++)
            {
                sum += _state.Points[i];
            }
            return sum;
        }

        private void ShowCaught(Question q)
        {
            TravelList list = _window.Take(this);
            if (list == null)
            {
                End();
                return;
            }
            string[] parts = FinanceText.T(q.Key).Split('|');
            string body = FinanceText.T("reallife_ven_meet_caught_body", "name", _investor.Name, "topic", parts[parts.Length - 1], "days", VenturesCatalogue.CaughtCloseDays.ToString())
                          .Replace("{day}", _investor.ClosedUntil.ToString());
            list.Begin(FinanceText.T("reallife_ven_meet_title", "name", _investor.Name), FinanceText.T("reallife_ven_meet_caught"));
            list.AddNote(_investor.Name, body, list.HeightFor((int)(body.Length * 1.3f), false));
            list.Add(Decline, FinanceText.T("reallife_ven_meet_close"), string.Empty, ButtonDo, true);
            _result = true;
            _offerId = 0;
            _director.Tell(NotificationType.Error, "reallife_ven_note_caught", null, "name", _investor.Name, "day", _investor.ClosedUntil.ToString());
            Open(list);
        }

        private void ShowDecision(VenturesFile file, int day)
        {
            TravelList list = _window.Take(this);
            if (list == null)
            {
                End();
                return;
            }
            PitchDecision d = Pitch.Decide(file, _investor, _state.Facts, _state.Place, _state.Points, VenturesDirector.Level, day);
            bool forNew = !_state.Facts.Running;
            string amount = FinanceText.Money(d.Amount), share = VenturesText.Share(d.ShareBp);
            string verdict;
            switch (d.Verdict)
            {
                case Verdict.Invest:
                    verdict = FinanceText.Fill(forNew ? "reallife_ven_verdict_new" : d.OnFounder ? "reallife_ven_verdict_founder" : "reallife_ven_verdict_invest",
                        "name", _investor.Name, "amount", amount, "share", share, "business", _state.Business);
                    break;
                case Verdict.TooSmall:
                    verdict = FinanceText.T("reallife_ven_verdict_small", "name", _investor.Name, "least", FinanceText.Money(_investor.Least));
                    break;
                case Verdict.NoProfit:
                    verdict = FinanceText.T("reallife_ven_verdict_no_profit", "name", _investor.Name, "business", _state.Business);
                    break;
                case Verdict.NoRoom:
                    verdict = FinanceText.T("reallife_ven_verdict_no_room", "name", _investor.Name);
                    break;
                case Verdict.NotOpen:
                    verdict = FinanceText.T("reallife_ven_verdict_not_open");
                    break;
                default:
                    verdict = FinanceText.T("reallife_ven_verdict_low", "name", _investor.Name);
                    break;
            }
            // The best and the worst answer of the meeting, where they stand out.
            string why = string.Empty;
            if (d.Best >= 0 && _state.Points[d.Best] >= 6)
            {
                why += " " + FinanceText.T("reallife_ven_verdict_helped", "topic", Topic(d.Best));
            }
            if (d.Worst >= 0 && d.Worst != d.Best && _state.Points[d.Worst] <= 6)
            {
                why += " " + FinanceText.T("reallife_ven_verdict_hurt", "topic", Topic(d.Worst));
            }
            string body = verdict + why;
            list.Begin(FinanceText.T("reallife_ven_meet_title", "name", _investor.Name),
                FinanceText.T(d.Verdict == Verdict.Invest ? "reallife_ven_meet_result_yes" : "reallife_ven_meet_result_no"));
            list.AddNote(_investor.Name, body, list.HeightFor((int)(body.Length * 1.3f), false));
            _offerId = 0;
            if (d.Verdict == Verdict.Invest)
            {
                Offer offer = OfferRules.Add(file, _investor, _state.Place, _state.Business, d.Amount, d.ShareBp, day, VenturesDirector.Level, OfferSource.Pitch);
                offer.TypeName = _state.TypeName;
                offer.Budget = _state.Facts.Budget;
                offer.Test = _state.Test;
                file.Unseen = Mathf.Max(0, file.Unseen - 1);   // it is being shown now
                _offerId = offer.Id;
                string take = forNew
                    ? FinanceText.Fill("reallife_ven_meet_take_new", "amount", amount, "share", share, "type", _state.Business, "days", VenturesCatalogue.PledgeDays.ToString())
                    : FinanceText.Fill("reallife_ven_meet_take", "amount", amount, "share", share, "business", _state.Business, "growth", offer.TargetPct.ToString(),
                        "days", offer.TargetDays.ToString());
                string think = FinanceText.T("reallife_ven_meet_think_detail", "day", offer.LapseDay.ToString());
                list.Add(TakeIt, FinanceText.T("reallife_ven_meet_take_title"), take, ButtonDo, true, list.HeightFor(take.Length, true));
                list.Add(ThinkItOver, FinanceText.T("reallife_ven_meet_think"), think, ButtonDo, true, list.HeightFor(think.Length, true));
                list.Add(Decline, FinanceText.T("reallife_ven_meet_decline"), string.Empty, ButtonDo, true);
            }
            else
            {
                string again = FinanceText.T("reallife_ven_meet_again", "name", _investor.Name, "day", _investor.OpenDay.ToString());
                list.Add(ThinkItOver, FinanceText.T("reallife_ven_meet_close"), again, ButtonDo, true, list.HeightFor(again.Length, true));
            }
            _result = true;
            VenturesDirector.Logger?.Info("Ventures: pitch " + _state.Number + " to " + _investor.Name + " about " + _state.Business + ": " + Sum() + " of "
                                          + _state.Points.Count * VenturesCatalogue.PointsMost + " points, score " + d.Score + " (needs " + d.Needed + "): " + d.Verdict
                                          + (d.Verdict == Verdict.Invest ? ", " + d.Amount + " for " + d.ShareBp + " bp" : string.Empty) + ".");
            if (Meta.Dev.Verbose)
            {
                VenturesDirector.Logger?.Info("Ventures check (pitch " + _state.Number + ", decision): points " + string.Join(",", _state.Points) + "; liked sector "
                                              + _investor.LikesSector(_state.Facts.Sector) + " (+" + VenturesCatalogue.LikedScore + "), name with investors " + file.Reputation
                                              + "; score " + d.Score + " against " + d.Needed + "; "
                                              + (forNew ? "budget " + _state.Facts.Budget : "recent profit " + _state.Facts.AvgProfit7.ToString("0") + " a day valued at " + d.ValueDays + " days")
                                              + (d.OnFounder ? ", backed on the founder" : string.Empty) + ", sums " + _investor.Least + " to " + _investor.Most + ": "
                                              + d.Verdict + " " + d.Amount + " for " + d.ShareBp + " bp; best answer " + (d.Best + 1) + ", worst " + (d.Worst + 1) + ".");
            }
            Open(list);
        }

        // What a question of this meeting was about, in a few words: the last part of its line.
        private string Topic(int index)
        {
            string[] parts = FinanceText.T(Pitch.Bank[_state.Questions[index]].Key).Split('|');
            return parts[parts.Length - 1];
        }

        private void Decided(int id)
        {
            int offer = _offerId;
            End();
            if (offer <= 0)
            {
                return;
            }
            if (id == TakeIt)
            {
                _director.Accept(offer);
            }
            else if (id == Decline)
            {
                _director.Refuse(offer);
            }
        }

        // The window was closed from outside (it is locked, so not by the player): the meeting is over as it stands.
        private void Closed()
        {
            if (Active && !_leaving)
            {
                Free();
            }
        }

        /// <summary>Once a second, from the director: a meeting whose window has gone with the game's interface is ended.</summary>
        public void Watch()
        {
            if (Active && !_window.OpenFor(this))
            {
                Free();
            }
        }

        /// <summary>Ends the meeting and puts the window away. An offer already made stays in the app.</summary>
        public void End()
        {
            _leaving = true;
            if (_window.OpenFor(this))
            {
                _window.Close();
            }
            _leaving = false;
            Free();
        }

        private void Free()
        {
            Active = false;
            _state = null;
            _result = false;
            PlayerController player = _held;
            _held = null;
            try
            {
                if (player != null)
                {
                    player.UnsetNavigationBlocker(Blocker);
                }
            }
            catch (Exception e)
            {
                VenturesDirector.Logger?.Error(e);
            }
        }

        public void Dispose()
        {
            End();
            _window.Destroy(e => VenturesDirector.Logger?.Error(e));
        }
    }
}
