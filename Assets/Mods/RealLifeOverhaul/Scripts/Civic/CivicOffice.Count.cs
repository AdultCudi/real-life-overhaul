using System;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The count and what follows it: the result, taking and leaving office, and what can come out.</summary>
    public sealed partial class CivicOffice
    {
        private void Counted(int day)
        {
            bool mine = State.Filed && State.ForDay == Gov.ElectionDay;
            OfficeRace held = State.Holds;
            if (!mine)
            {
                if (State.Running)
                {
                    State.ClearRun();   // papers never filed
                }
                if (held == OfficeRace.Council || (held == OfficeRace.Mayor && !Gov.MayorIsPlayer))
                {
                    LeaveOffice(day, "reallife_civic_office_term_over");
                }
                return;
            }
            bool won;
            float share;
            if (State.Race == OfficeRace.Mayor)
            {
                int slot = Gov.PlayerSlot;
                won = slot >= 0 && Gov.Winner == slot;
                share = slot >= 0 && Gov.Turnout > 0 ? _gov.CityVotes(slot) / (float)Gov.Turnout : 0f;
            }
            else
            {
                won = CountRace(day, out share);
            }
            State.Runs++;
            State.Result = won ? 1 : 2;
            State.ResultRace = State.Race;
            State.ResultShare = Mathf.RoundToInt(share * 1000f);
            State.ResultDay = day;
            State.ResultShown = false;
            if (State.Deposit && share >= OfficeRules.DepositBack)
            {
                CivicEffects.Pay(Prices.At(OfficeRules.Deposit(State.Race)), "reallife_transaction_civic_office_deposit_back");
            }
            string place = Where();
            if (won)
            {
                State.Wins++;
                if (held != OfficeRace.None && held != State.Race)
                {
                    State.Terms++;   // a councillor elected mayor gives up the seat
                }
                TakeOffice(State.Race, share, day);
                State.Story(day + 1, State.Race == OfficeRace.Mayor ? "reallife_civic_news_office_won_mayor" : "reallife_civic_news_office_won_council", CivicText.PlayerName(), place);
                Post(PostWon, place);
                ModState.AddModifier(ModState.MoodUpBig);
            }
            else
            {
                if (held != OfficeRace.None)
                {
                    LeaveOffice(day, null);
                }
                State.Story(day + 1, "reallife_civic_news_office_lost", CivicText.PlayerName(), place);
                ModState.AddModifier(ModState.MoodDown);
            }
            CivicBridges.Moved(0, won ? 25 : 8);
            if (State.Dirty > 0)
            {
                State.DirtyDay = Math.Max(State.DirtyDay, day);   // money taken against the law can still come out for a while
            }
            Logger("the count: " + (won ? "won" : "lost") + " the race for " + State.Race + " with " + State.ResultShare / 10f + " % of the vote.");
            // The run is over; the race's names and votes stay for result night and City Hall until the next run begins.
            State.Race = OfficeRace.None;
            State.Filed = false;
            State.OfferKind = -1;
            State.AttackKind = -1;
        }

        // The district's race: its voters, its turnout, every candidate's votes. True if the player won; their share out.
        private bool CountRace(int day, out float share)
        {
            RaceMove(day, 0.2f);
            int d = RaceDistrict();
            int electorate = d >= 0 && d < _city.Districts.Count ? _city.Districts[d].Electorate : 9000;
            var roll = new CivicRandom(CivicRandom.Mix(Gov.Seed, Gov.Number, 4441));
            float turnout = OfficeRules.Turnout(roll.Value, d >= 0 && d < State.DistrictDoors.Length ? State.DistrictDoors[d] : 0);
            int total = 0, best = 0;
            for (int i = 0; i <= State.RivalCount; i++)
            {
                State.RaceVotes[i] = OfficeRules.Votes(electorate, turnout, State.RacePolls[i]) + (i == 0 && Gov.Voted >= 0 ? 1 : 0);
                total += State.RaceVotes[i];
                if (State.RaceVotes[i] > State.RaceVotes[best])
                {
                    best = i;
                }
            }
            State.RaceTurnout = total;
            share = total > 0 ? State.RaceVotes[0] / (float)total : 0f;
            return best == 0;
        }

        private void TakeOffice(OfficeRace race, float share, int day)
        {
            State.Holds = race;
            State.Held.CopyFrom(State.Me);
            State.Held.Player = true;
            State.TermStart = day + 1;
            State.PayDay = day + 1 + 7;
            State.Registered = false;
            State.RegisterBy = day + 1 + OfficeRules.RegisterDays;
            State.NextMeeting = day + 1 + 3;
            State.Agenda.Clear();
            State.Missed = 0;
            State.Attended = 0;
            State.VotesRecord = 0f;
            State.LowDays = 0;
            State.RecallDay = 0;
            State.Approval = OfficeRules.StartApproval(share);
            if (race == OfficeRace.Council)
            {
                State.Seat = CivicCatalogue.CouncilSeats - 1;
                Gov.Council[State.Seat] = State.Me.Party;
            }
            else
            {
                State.Seat = -1;
            }
        }

        private void LeaveOffice(int day, string toastKey)
        {
            if (State.Holds == OfficeRace.None)
            {
                return;
            }
            State.Terms++;
            State.Holds = OfficeRace.None;
            State.Seat = -1;
            State.Agenda.Clear();
            State.RecallDay = 0;
            if (toastKey != null)
            {
                CivicText.Toast(NotificationType.Info, toastKey, 12f);
            }
        }

        /// <summary>A post about the player's run or office went out on social media (Wiring/FlareLinks): thousandths of a point of its worth.</summary>
        public void Posted(int weight)
        {
            float worth = Mathf.Clamp(weight, 0, 1500) / 1000f;
            if (Campaigning)
            {
                State.Push += Mathf.Min(0.04f, worth * 0.1f);
            }
            else if (State.Holds == OfficeRace.Council)
            {
                State.Approval = Mathf.Min(95f, State.Approval + Mathf.Min(1.5f, worth * 3f));
            }
        }

        public void ResultSeen() => State.ResultShown = true;

        private void Discoveries(int day)
        {
            if (State.Dirty > 0)
            {
                if (day - State.DirtyDay > OfficeRules.DirtyWindow && !Campaigning)
                {
                    Logger("the money taken against the law never came out (" + State.Dirty + ").");
                    State.Dirty = 0;
                }
                else if (OfficeRules.Roll(Gov.Seed, day, 1401) < OfficeRules.DirtyOdds(State.Dirty, Limit, Recognition))
                {
                    int dirty = State.Dirty;
                    State.Dirty = 0;
                    State.Fund = Math.Max(0, State.Fund - dirty);
                    State.Case = CivicBridges.Summon(0, dirty);
                    Scandal(day, "reallife_civic_news_office_money", 10f, -8, 0.2f);
                    CivicText.Toast(NotificationType.Warning, "reallife_civic_office_money_out", "amount", CivicText.Money(dirty), 16f);
                }
            }
            for (int i = State.Secrets.Count - 1; i >= 0; i--)
            {
                OfficeSecret s = State.Secrets[i];
                if (day - s.Day > OfficeRules.UndeclaredWindow)
                {
                    State.Secrets.RemoveAt(i);
                    continue;
                }
                if (OfficeRules.Roll(Gov.Seed, day, 1411 + s.Policy * 7 + s.Day) < OfficeRules.UndeclaredOdds(s.Decisive, Recognition, State.Registered))
                {
                    State.Secrets.RemoveAt(i);
                    PolicyDef policy = CivicCatalogue.At(s.Policy);
                    if (s.Decisive)
                    {
                        CivicBridges.Summon(1, 0);
                    }
                    Scandal(day, "reallife_civic_news_office_interest", 8f, -6, 0.1f);
                    CivicText.Toast(NotificationType.Warning, s.Decisive ? "reallife_civic_office_interest_out_case" : "reallife_civic_office_interest_out",
                        "policy", policy != null ? CivicText.T(policy.Name) : string.Empty, 16f);
                }
            }
        }

        // A scandal: approval in office (or support in a campaign), integrity and the papers.
        private void Scandal(int day, string storyKey, float approval, int integrity, float support)
        {
            State.Scandals++;
            if (Campaigning)
            {
                State.Push -= support;
            }
            if (State.Holds == OfficeRace.Mayor && Gov.MayorIsPlayer)
            {
                Gov.Approval = Mathf.Max(3f, Gov.Approval - approval);
            }
            else if (State.Holds == OfficeRace.Council)
            {
                State.Approval = Mathf.Max(3f, State.Approval - approval);
            }
            CivicBridges.Moved(integrity, 15);
            ModState.AddModifier(ModState.MoodDownBig);
            State.Story(day + 1, storyKey, CivicText.PlayerName(), Where());
        }
    }
}
