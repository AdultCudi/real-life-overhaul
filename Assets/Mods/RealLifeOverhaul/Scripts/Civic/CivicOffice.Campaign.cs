using System;
using System.Collections.Generic;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The race: its candidates, the campaign's days, attacks, endorsements and the district race's polls.</summary>
    public sealed partial class CivicOffice
    {
        public int RaceCount => State.Race == OfficeRace.Council ? State.RivalCount + 1 : Gov.CandidateCount;

        /// <summary>A candidate of the player's race by its number in it (the council's: 0 is the player).</summary>
        public Candidate RaceCandidate(int i)
        {
            if (State.Race == OfficeRace.Council)
            {
                return i == 0 ? State.Me : i - 1 < State.RivalCount && i >= 1 ? State.Rival[i - 1] : null;
            }
            return i >= 0 && i < Gov.CandidateCount ? Gov.Candidates[i] : null;
        }

        public int MyIndex => State.Race == OfficeRace.Council ? 0 : Gov.PlayerSlot;

        public float RacePoll(int i)
        {
            if (State.Race == OfficeRace.Council)
            {
                return i >= 0 && i <= State.RivalCount ? State.RacePolls[i] : 0f;
            }
            return i >= 0 && i < Gov.CandidateCount ? Gov.Polls[i] : 0f;
        }

        public float MyPoll => MyIndex >= 0 ? RacePoll(MyIndex) : 0f;

        /// <summary>The best placed of the others in the player's race; -1 for none.</summary>
        public int Leader()
        {
            int best = -1;
            for (int i = 0; i < RaceCount; i++)
            {
                if (i != MyIndex && (best < 0 || RacePoll(i) > RacePoll(best)))
                {
                    best = i;
                }
            }
            return best;
        }

        // Support for another in the race (an endorsement, an answer that cost them).
        private void Nudge(int i, float push)
        {
            if (State.Race == OfficeRace.Council)
            {
                if (i >= 1 && i <= State.RivalCount)
                {
                    State.RivalPush[i - 1] += push;
                }
            }
            else if (i >= 0 && i < Gov.CandidateCount)
            {
                Gov.Momentum[i] += push;
            }
        }

        // What the others in the race have spent so far, on average: a sum a day since the campaign began, at today's prices.
        private int RivalSpending()
        {
            if (State.Race == OfficeRace.Council)
            {
                int most = 0;
                for (int i = 0; i < State.RivalCount; i++)
                {
                    most = Math.Max(most, State.RivalSpend[i]);
                }
                return most;
            }
            int days = Mathf.Clamp(CivicCatalogue.CampaignDays - (Gov.ElectionDay - TimeHelper.CurrentDay), 0, CivicCatalogue.CampaignDays);
            return days * Prices.At(700);
        }

        private void CampaignDay(int day)
        {
            int left = Gov.ElectionDay - day;
            int seed = Gov.Seed;
            // A donor's offer, if there is none waiting (one waits a day).
            if (State.OfferKind >= 0 && State.OfferDay < day - 1)
            {
                State.OfferKind = -1;
            }
            if (State.OfferKind < 0 && OfficeRules.Offer(OfficeRules.Roll(seed, day, 1201), OfficeRules.Roll(seed, day, 1203), Recognition, MyPoll, Limit, out int kind, out int amount))
            {
                State.OfferKind = kind;
                State.OfferAmount = amount;
                State.OfferDay = day;
                var names = new CivicRandom(CivicRandom.Mix(seed, day, 1207));
                State.OfferFirst = names.Range(0, CivicCatalogue.FirstNames.Length);
                State.OfferLast = names.Range(0, CivicCatalogue.LastNames.Length);
                CivicText.Toast(NotificationType.Info, "reallife_civic_office_offer_new", "amount", CivicText.Money(amount), 12f);
            }
            // An attack waiting too long stands as it is.
            if (State.AttackKind >= 0 && day > State.AttackDay + OfficeRules.AttackAnswerDays)
            {
                State.AttackKind = -1;
            }
            if (State.AttackKind < 0 && left >= 1 && OfficeRules.Roll(seed, day, 1211) < OfficeRules.AttackAttempt(left))
            {
                Attack(day);
            }
            if (left == OfficeRules.EndorseDecide)
            {
                Endorsers(day);
            }
            if (State.Debate == 0 && left < CivicCatalogue.DebateBefore)
            {
                Debated(OfficeRules.EmptyChair, false, day);   // the debate went by without the player
            }
            if (State.Race == OfficeRace.Council)
            {
                for (int i = 0; i < State.RivalCount; i++)
                {
                    State.RivalSpend[i] += Prices.At(150 + OfficeRules.Roll(seed, day, 1221 + i) / 4);
                    State.RivalPush[i] += 0.004f;
                }
                RaceMove(day, 0.34f);
            }
            if (left == CivicCatalogue.CampaignDays - 1)
            {
                State.Story(day + 1, State.Race == OfficeRace.Mayor ? "reallife_civic_news_office_race_mayor" : "reallife_civic_news_office_race_council",
                    CivicText.PlayerName(), Where());
            }
        }

        // An opponent's attack: on what is true of the player if there is something, else on something thin.
        private void Attack(int day)
        {
            int by = Leader();
            if (by < 0)
            {
                return;
            }
            var kinds = new List<AttackKind>(6);
            bool record = CivicBridges.HasRecord() || CivicBridges.Ask(CivicBridges.CaseOpen);
            if (record)
            {
                kinds.Add(AttackKind.Record);
            }
            if (State.Dirty > 0)
            {
                kinds.Add(AttackKind.Money);
            }
            if (State.Missed > 0 || State.Debate == 2)
            {
                kinds.Add(AttackKind.Absent);
            }
            if (Integrity < 45)
            {
                kinds.Add(AttackKind.Business);
            }
            if (State.InOffice && Unkept(State.Holds == OfficeRace.Mayor ? Gov.Mayor : State.Held) > 0)
            {
                kinds.Add(AttackKind.Promises);
            }
            if (State.Wins == 0 && !State.InOffice)
            {
                kinds.Add(AttackKind.Inexperience);
            }
            int roll = OfficeRules.Roll(Gov.Seed, day, 1213);
            AttackKind kind;
            bool real;
            if (kinds.Count > 0 && roll < 700)
            {
                kind = kinds[roll % kinds.Count];
                real = true;
            }
            else
            {
                kind = roll % 2 == 0 ? AttackKind.Business : AttackKind.Money;
                real = (kind == AttackKind.Business && Integrity < 45) || (kind == AttackKind.Money && State.Dirty > 0);
            }
            State.AttackKind = (int)kind;
            State.AttackBy = by;
            State.AttackDay = day;
            State.AttackTrue = real;
            State.Attacks++;
            State.Push += OfficeRules.AttackHit(real);
            Candidate who = RaceCandidate(by);
            string name = who != null ? CivicText.Name(who) : string.Empty;
            CivicText.Toast(NotificationType.Warning, "reallife_civic_office_attacked", "name", name, "what", CivicText.T(AttackKey(kind)), 14f);
            State.Story(day + 1, "reallife_civic_news_office_attack", name, CivicText.T(AttackKey(kind)));
            Logger("attacked by " + name + " on " + kind + (real ? " (there is something in it)" : " (thin)") + ".");
        }

        public static string AttackKey(AttackKind kind)
        {
            switch (kind)
            {
                case AttackKind.Record: return "reallife_civic_office_attack_record";
                case AttackKind.Business: return "reallife_civic_office_attack_business";
                case AttackKind.Money: return "reallife_civic_office_attack_money";
                case AttackKind.Inexperience: return "reallife_civic_office_attack_inexperience";
                case AttackKind.Absent: return "reallife_civic_office_attack_absent";
                default: return "reallife_civic_office_attack_promises";
            }
        }

        /// <summary>The player answers the attack waiting. Returns the locale key.</summary>
        public string AnswerAttack(OfficeAnswer answer, int day)
        {
            if (State.AttackKind < 0)
            {
                return "reallife_civic_office_no_attack";
            }
            OfficeRules.Answer(answer, State.AttackTrue, out float mine, out float theirs);
            State.Push += mine;
            Nudge(State.AttackBy, theirs);
            bool real = State.AttackTrue;
            State.AttackKind = -1;
            if (answer == OfficeAnswer.Straight && real)
            {
                CivicBridges.StandingMoved?.Invoke(-1, 0);   // a denial of something true
            }
            return answer == OfficeAnswer.Straight ? (real ? "reallife_civic_office_answer_denied" : "reallife_civic_office_answer_rebutted")
                : answer == OfficeAnswer.TurnBack ? "reallife_civic_office_answer_hit_back" : "reallife_civic_office_answer_ignored";
        }

        // The bodies that have not been asked decide, four days out, for whoever fits them best.
        private void Endorsers(int day)
        {
            for (int e = 0; e < OfficeRules.Endorsers; e++)
            {
                if (State.Backs[e] >= 0)
                {
                    continue;
                }
                int best = BestFor(e, true);
                Endorse(e, best, day);
            }
        }

        // The candidate a body likes best (the player among them if asked for), by fit and then by the polls.
        private int BestFor(int endorser, bool withMe)
        {
            int best = -1;
            float bestFit = float.MinValue;
            for (int i = 0; i < RaceCount; i++)
            {
                Candidate c = RaceCandidate(i);
                if (c == null || (!withMe && i == MyIndex))
                {
                    continue;
                }
                float fit = OfficeRules.EndorserFit(endorser, c, c.Player ? Integrity : 50) + RacePoll(i) * 0.5f;
                if (fit > bestFit)
                {
                    bestFit = fit;
                    best = i;
                }
            }
            return best;
        }

        private void Endorse(int endorser, int candidate, int day)
        {
            if (candidate < 0)
            {
                return;
            }
            State.Backs[endorser] = candidate;
            float push = endorser == OfficeRules.Endorsers - 1 ? OfficeRules.EditorialPush : OfficeRules.EndorsementPush;
            if (candidate == MyIndex)
            {
                State.Endorsed |= 1 << endorser;
                State.Push += push;
                CivicText.Toast(NotificationType.Success, "reallife_civic_office_endorsed", "who", CivicText.T(EndorserKey(endorser)), 12f);
                State.Story(day + 1, "reallife_civic_news_office_endorsed", CivicText.PlayerName(), CivicText.T(EndorserKey(endorser)));
            }
            else
            {
                Nudge(candidate, push);
            }
        }

        public static string EndorserKey(int endorser)
        {
            return BodyKeys[Mathf.Clamp(endorser, 0, BodyKeys.Length - 1)];
        }

        private static readonly string[] BodyKeys =
        {
            "reallife_civic_office_body_0", "reallife_civic_office_body_1", "reallife_civic_office_body_2", "reallife_civic_office_body_3",
            "reallife_civic_office_body_4", "reallife_civic_office_body_5", "reallife_civic_office_body_6"
        };

        /// <summary>The player asks a body for its endorsement (once an election). Returns the locale key; the body's choice is in State.Backs.</summary>
        public string Ask(int endorser, int day)
        {
            if (!Campaigning || endorser < 0 || endorser >= OfficeRules.Endorsers)
            {
                return "reallife_civic_office_no_campaign";
            }
            if ((State.Asked & (1 << endorser)) != 0 || State.Backs[endorser] >= 0)
            {
                return "reallife_civic_office_asked_already";
            }
            State.Asked |= 1 << endorser;
            int rival = BestFor(endorser, false);
            float mine = OfficeRules.EndorserFit(endorser, State.Me, Integrity), theirs = rival >= 0 ? OfficeRules.EndorserFit(endorser, RaceCandidate(rival), 50) : 0f;
            bool yes = OfficeRules.Roll(Gov.Seed, day, 1301 + endorser) < OfficeRules.AskOdds(mine, theirs, Influence);
            Endorse(endorser, yes ? MyIndex : rival, day);
            return yes ? "reallife_civic_office_ask_yes" : "reallife_civic_office_ask_no";
        }

        private void RaceTargets()
        {
            int d = RaceDistrict();
            for (int i = 0; i < CivicCatalogue.Issues; i++)
            {
                _weights[i] = _gov.Weight(i) * (d >= 0 ? _gov.DistrictLean(d, i) : 1f);
            }
            int cycle = CivicBridges.Cycle();
            int scale = Prices.At(1000);
            _raceScores[0] = OfficeRules.Fit(State.Me, _weights) * 1.1f + Term(d) + OfficeRules.LocalTerm(true, Businesses(d), 0f);
            if (State.Incumbent)
            {
                _raceScores[0] += (State.Approval - 50f) / 40f + OfficeRules.EconomyTerm(cycle, true);
            }
            for (int i = 0; i < State.RivalCount; i++)
            {
                Candidate rival = State.Rival[i];
                var appeal = new CivicRandom(CivicRandom.Mix(Gov.Seed, State.ForDay * 7 + i, 4421));
                float score = OfficeRules.Fit(rival, _weights) * 1.1f + appeal.Range(-0.2f, 0.2f) + State.RivalPush[i]
                              + OfficeRules.SpendingTerm(State.RivalSpend[i], State.Spent, scale) * 0.5f;
                if (rival.Party == Gov.Mayor.Party)
                {
                    score += (Gov.Approval - 50f) / 80f;
                }
                if (rival.Incumbent)
                {
                    score += OfficeRules.EconomyTerm(cycle, true);
                }
                _raceScores[i + 1] = score;
            }
            OfficeRules.Shares(_raceScores, State.RivalCount + 1, _raceShares);
        }

        private void RaceMove(int day, float pull)
        {
            RaceTargets();
            var noise = new CivicRandom(CivicRandom.Mix(Gov.Seed, day, 4431));
            for (int i = 0; i <= State.RivalCount; i++)
            {
                State.RacePolls[i] = OfficeRules.PollStep(State.RacePolls[i], _raceShares[i], pull, noise.Range(-0.012f, 0.012f));
            }
            OfficeRules.Normalise(State.RacePolls, State.RivalCount + 1);
        }
    }
}
