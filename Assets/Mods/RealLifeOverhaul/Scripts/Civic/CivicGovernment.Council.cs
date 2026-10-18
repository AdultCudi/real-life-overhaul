using System;
using System.Collections.Generic;
using System.Globalization;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The government's days: approval, petitions, the council's own votes, and the levers moving.</summary>
    public sealed partial class CivicGovernment
    {
        private void Approve(int day)
        {
            Salience();
            float target = 52f - State.CrimeHeat * 3f - State.CrashHeat * 2f - LateSites * 1.5f;
            int inForce = 0;
            for (int i = 0; i < State.Mayor.Planks; i++)
            {
                PolicyDef policy = CivicCatalogue.At(State.Mayor.Platform[i]);
                if (policy != null && State.Levers[(int)policy.Lever] == policy.Dir)
                {
                    inForce++;
                }
            }
            target += inForce * 2.5f;
            target -= (State.Mayor.Planks - inForce) * 1.5f;   // promises not yet kept
            target += State.Levers[(int)Lever.Cleaning] * 3f + State.Levers[(int)Lever.Events] * 2f - State.Levers[(int)Lever.BusinessTax] * 2f;
            target += OfficeRules.EconomyTerm(CivicBridges.Cycle(), true) * 20f;   // the economy: four points in a boom, six off in a recession
            try
            {
                target += ApprovalExtra != null ? ApprovalExtra() : 0f;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
            }
            int inOffice = day - State.TermStart;
            if (inOffice < 14)
            {
                target += (14 - inOffice) * 0.6f;   // the honeymoon
            }
            var random = new CivicRandom(CivicRandom.Mix(State.Seed, day, 31));
            float mood = 0f;
            try
            {
                mood = CivicBridges.Mood != null ? Mathf.Clamp(CivicBridges.Mood(), -MoodPullMost, MoodPullMost) : 0f;   // social media's mood: a point a day at most
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
            }
            for (int i = 0; i < _raised.Length; i++)
            {
                _raised[i] *= 0.8f;
            }
            State.Approval = Mathf.Clamp(State.Approval + (Mathf.Clamp(target, 5f, 90f) - State.Approval) * 0.15f + random.Range(-1f, 1f) + mood, 3f, 95f);

            State.LowDays = State.Approval < CivicCatalogue.RecallBelow ? State.LowDays + 1 : 0;
            if (State.LowDays >= CivicCatalogue.RecallDays && State.Phase == ElectionPhase.None && inOffice > 14 && State.ElectionDay - day > CivicCatalogue.CampaignDays + 7)
            {
                State.LowDays = 0;
                State.ElectionDay = day + CivicCatalogue.CampaignDays;
                CivicText.Toast(NotificationType.Warning, "reallife_civic_recall_called", "name", CivicText.Name(State.Mayor), "approval", Mathf.RoundToInt(State.Approval).ToString(), 14f);
                CivicBridges.Report("reallife_civic_news_recall", "reallife_civic_news_recall_body");
                StartCampaign(day, true);
            }
        }

        private void Petition(int day)
        {
            if (State.PetitionPolicy < 0)
            {
                // Now and then the city gets up a petition of its own, about whatever matters most that is not in force.
                var random = new CivicRandom(CivicRandom.Mix(State.Seed, day, 53));
                if (day >= State.PetitionRest && State.Phase == ElectionPhase.None && random.Value < 0.12f)
                {
                    int policy = PickPolicy(ref random);
                    if (policy >= 0)
                    {
                        OpenPetition(policy, day, false);
                        CivicText.Toast(NotificationType.Info, "reallife_civic_petition_new", "policy", CivicText.T(CivicCatalogue.All[policy].Name), "needed", State.PetitionNeeded.ToString("N0"), 12f);
                    }
                }
                return;
            }
            PolicyDef asked = CivicCatalogue.All[State.PetitionPolicy];
            var roll = new CivicRandom(CivicRandom.Mix(State.Seed, day, 59));
            float support = _salience[(int)asked.Issue] / MaxSalience() + (State.Approval < 40f ? 0.2f : 0f) + (State.PetitionMine ? 0.1f : 0f);
            State.PetitionSigned += Mathf.RoundToInt(State.PetitionNeeded * (0.035f + 0.07f * support) * roll.Range(0.6f, 1.4f));
            if (State.PetitionSigned >= State.PetitionNeeded)
            {
                CivicText.Toast(NotificationType.Success, "reallife_civic_petition_met", "policy", CivicText.T(asked.Name), "signed", State.PetitionSigned.ToString("N0"), 12f);
                int policy = State.PetitionPolicy;
                ClosePetition(day);
                if (Sits())
                {
                    ToAgenda?.Invoke(policy, true);   // to the next meeting, where the player has a vote
                }
                else
                {
                    CouncilVote(policy, day, true);
                }
            }
            else if (day >= State.PetitionEnds)
            {
                CivicText.Toast(NotificationType.Warning, "reallife_civic_petition_lapsed", "policy", CivicText.T(asked.Name), "signed", State.PetitionSigned.ToString("N0"), "needed", State.PetitionNeeded.ToString("N0"), 12f);
                ClosePetition(day);
            }
        }

        private void OpenPetition(int policy, int day, bool mine)
        {
            State.PetitionPolicy = policy;
            State.PetitionSigned = 0;
            State.PetitionNeeded = Mathf.Max(200, Mathf.RoundToInt(Electorate() * CivicCatalogue.PetitionShare));
            State.PetitionEnds = day + CivicCatalogue.PetitionDays;
            State.PetitionMine = mine;
            State.PetitionISigned = false;
        }

        private void ClosePetition(int day)
        {
            State.PetitionPolicy = -1;
            State.PetitionMine = false;
            State.PetitionISigned = false;
            State.PetitionRest = day + 10;
        }

        private void Council(int day)
        {
            if (State.NextCouncilVote <= 0)
            {
                State.NextCouncilVote = day + CivicCatalogue.CouncilVoteEvery;
            }
            if (day < State.NextCouncilVote || State.Phase != ElectionPhase.None || Sits())
            {
                return;   // while the player sits, the council's business goes to its meetings
            }
            var random = new CivicRandom(CivicRandom.Mix(State.Seed, day, 67));
            State.NextCouncilVote = day + CivicCatalogue.CouncilVoteEvery + random.Range(-3, 4);
            int policy = PickPolicy(ref random);
            if (policy >= 0)
            {
                CouncilVote(policy, day, false);
            }
        }

        // A motion before the five: each member is for it the more the issue matters now; a petition that met its number adds weight.
        private void CouncilVote(int policyIndex, int day, bool petitioned)
        {
            PolicyDef policy = CivicCatalogue.All[policyIndex];
            Salience();
            float support = _salience[(int)policy.Issue] / MaxSalience();
            var random = new CivicRandom(CivicRandom.Mix(State.Seed, day, policyIndex + 101));
            int ayes = 0;
            for (int seat = 0; seat < State.Council.Length; seat++)
            {
                float odds = 0.25f + 0.35f * support + (petitioned ? 0.2f : 0f) + (State.Council[seat] == State.Mayor.Party && Promised(State.Mayor, policyIndex) ? 0.25f : 0f);
                if (random.Value < odds)
                {
                    ayes++;
                }
            }
            int noes = State.Council.Length - ayes;
            if (ayes > noes)
            {
                Schedule((int)policy.Lever, policy.Dir, day + 1);
                CivicText.Toast(NotificationType.Info, "reallife_civic_council_passed", "policy", CivicText.T(policy.Name), "ayes", ayes.ToString(), "noes", noes.ToString(), 12f);
            }
            else
            {
                CivicText.Toast(NotificationType.Info, "reallife_civic_council_failed", "policy", CivicText.T(policy.Name), "ayes", ayes.ToString(), "noes", noes.ToString(), 12f);
            }
        }

        private bool Sits()
        {
            try
            {
                return OfficeSits != null && OfficeSits();
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return false;
            }
        }

        /// <summary>What the council would take up itself today, from what matters most; -1 for none.</summary>
        public int CityMotion(int day)
        {
            var random = new CivicRandom(CivicRandom.Mix(State.Seed, day, 67));
            return PickPolicy(ref random);
        }

        /// <summary>A motion the council passed at a meeting the player sat in: in tomorrow, announced on its day.</summary>
        public void Passed(int policyIndex, int day)
        {
            PolicyDef policy = CivicCatalogue.At(policyIndex);
            if (policy != null)
            {
                Schedule((int)policy.Lever, policy.Dir, day + 1);
            }
        }

        /// <summary>A mayor's order on a lever that is theirs to set (OfficeRules.ByOrder): in tomorrow. False if it stands there or is on its way.</summary>
        public bool Ordered(Lever lever, int position, int day) => Lobbied(lever, position, day + 1);

        /// <summary>A policy's weight in the city today, 0 to 1 against what matters most.</summary>
        public float Support(int policyIndex)
        {
            PolicyDef policy = CivicCatalogue.At(policyIndex);
            if (policy == null)
            {
                return 0f;
            }
            Salience();
            return _salience[(int)policy.Issue] / MaxSalience();
        }

        /// <summary>How much an issue matters in the city today (1 is ordinary).</summary>
        public float Weight(int issue)
        {
            Salience();
            return issue >= 0 && issue < _salience.Length ? _salience[issue] : 1f;
        }

        public void Weights(float[] into)
        {
            Salience();
            Array.Copy(_salience, into, Math.Min(into.Length, _salience.Length));
        }

        /// <summary>How much a district cares about an issue against the city: 0.7 to 1.3, fixed for the save.</summary>
        public float DistrictLean(int district, int issue)
        {
            var random = new CivicRandom(CivicRandom.Mix(State.Seed, district * 31 + 5, issue + 211));
            return random.Range(0.7f, 1.3f);
        }

        public static bool Promised(Candidate candidate, int policyIndex)
        {
            for (int i = 0; i < candidate.Planks; i++)
            {
                if (candidate.Platform[i] == policyIndex)
                {
                    return true;
                }
            }
            return false;
        }

        // A policy that is not in force (nor on its way), from the issues that matter most now.
        private int PickPolicy(ref CivicRandom random)
        {
            Salience();
            int best = -1;
            float bestScore = float.MinValue;
            for (int i = 0; i < CivicCatalogue.All.Length; i++)
            {
                PolicyDef policy = CivicCatalogue.All[i];
                if (State.Levers[(int)policy.Lever] == policy.Dir || IsPending((int)policy.Lever))
                {
                    continue;
                }
                float score = _salience[(int)policy.Issue] + random.Range(0f, 0.9f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        private bool IsPending(int lever)
        {
            for (int i = 0; i < State.Pending.Count; i++)
            {
                if (State.Pending[i].Lever == lever)
                {
                    return true;
                }
            }
            return false;
        }

        private void Schedule(int lever, int position, int day)
        {
            for (int i = State.Pending.Count - 1; i >= 0; i--)
            {
                if (State.Pending[i].Lever == lever)
                {
                    State.Pending.RemoveAt(i);
                }
            }
            if (State.Levers[lever] != position)
            {
                State.Pending.Add(new PendingChange { Lever = lever, Position = position, Day = day });
            }
        }

        // Changes whose day has come take effect (at nine in the morning), each with a notice that says what it is.
        private void ApplyDue(int day)
        {
            bool any = false;
            for (int i = State.Pending.Count - 1; i >= 0; i--)
            {
                PendingChange change = State.Pending[i];
                if (change.Day > day)
                {
                    continue;
                }
                State.Pending.RemoveAt(i);
                any |= Set((Lever)change.Lever, change.Position, true);
            }
            if (any)
            {
                LeversChanged?.Invoke();
            }
        }

        /// <summary>Moves one lever now. Returns false if it already stood there.</summary>
        public bool Set(Lever lever, int position, bool announce)
        {
            position = Math.Sign(position);
            int old = State.Levers[(int)lever];
            if (old == position)
            {
                return false;
            }
            State.Levers[(int)lever] = position;
            if (announce)
            {
                PolicyDef now = CivicCatalogue.Find(lever, position);
                if (now != null)
                {
                    CivicText.Toast(NotificationType.Info, "reallife_civic_policy_in_force", "policy", CivicText.T(now.Name), "effect", CivicText.Effect(now), 16f);
                }
                else
                {
                    PolicyDef was = CivicCatalogue.Find(lever, old);
                    CivicText.Toast(NotificationType.Info, "reallife_civic_policy_ended", "policy", was != null ? CivicText.T(was.Name) : string.Empty, 12f);
                }
            }
            return true;
        }

        /// <summary>A won push on the council (the player's lobbying): the lever moves on the day given. False if it stands there or a change is on its way.</summary>
        public bool Lobbied(Lever lever, int position, int day)
        {
            position = Math.Sign(position);
            if (State.Levers[(int)lever] == position || IsPending((int)lever))
            {
                return false;
            }
            Schedule((int)lever, position, day);
            return true;
        }

        /// <summary>True if a lever stands at a position now or a change of it is on its way (a push on it would change nothing).</summary>
        public bool Settled(Lever lever, int position)
        {
            return State.Levers[(int)lever] == Math.Sign(position) || IsPending((int)lever);
        }

        /// <summary>Every lever back where the city came, at once, and nothing left on its way.</summary>
        public void RevertAll()
        {
            State.Pending.Clear();
            for (int i = 0; i < State.Levers.Length; i++)
            {
                State.Levers[i] = 0;
            }
            LeversChanged?.Invoke();
        }
    }
}
