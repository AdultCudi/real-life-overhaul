using System;
using System.Collections.Generic;
using System.Globalization;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The campaign: candidates drawn, the player's seat in the race, the issues' weights and the daily polls.</summary>
    public sealed partial class CivicGovernment
    {
        /// <summary>Candidates are drawn, the first poll is taken, and the posters may go up.</summary>
        public void StartCampaign(int day, bool recall)
        {
            State.Number++;
            State.Phase = ElectionPhase.Campaign;
            State.Recall = recall;
            if (State.ElectionDay < day + 1)
            {
                State.ElectionDay = day + 1;
            }
            State.Endorsed = -1;
            State.Volunteered = -1;
            State.Rallies = 0;
            State.Voted = -1;
            State.DebateHeld = false;
            State.Scandal = false;
            State.Winner = -1;
            State.Turnout = 0;
            Array.Clear(State.OthersVotes, 0, State.OthersVotes.Length);
            State.PlayerSlot = -1;
            State.DistrictsCounted = 0;
            for (int i = 0; i < State.Candidates.Length; i++)
            {
                State.Candidates[i].Player = false;
            }

            var random = new CivicRandom(CivicRandom.Mix(State.Seed, State.Number, 7));
            State.CandidateCount = random.Value < 0.5f ? 3 : 4;
            int usedParties = 0, usedLeads = 0;
            for (int i = 0; i < CivicCatalogue.MaxCandidates; i++)
            {
                State.Momentum[i] = 0f;
                State.Donated[i] = 0;
                State.Final[i] = 0f;
                State.Polls[i] = 0f;
            }
            // The mayor stands again three times in four (always against a recall), in slot 0. A mayor who is the player stands
            // only if they have filed their papers.
            bool mayorRolls = random.Value < 0.75f;
            bool mayorStands = !State.Mayor.Player && (recall || mayorRolls);
            for (int i = 0; i < State.CandidateCount; i++)
            {
                Candidate candidate = State.Candidates[i];
                if (i == 0 && mayorStands)
                {
                    int first = State.Mayor.First, last = State.Mayor.Last, party = State.Mayor.Party, look = State.Mayor.Look;
                    Generate(candidate, ref random, usedParties, usedLeads);
                    candidate.First = first;
                    candidate.Last = last;
                    candidate.Party = party;
                    candidate.Look = look;
                    candidate.Incumbent = true;
                    KeepRecord(candidate);
                }
                else
                {
                    Generate(candidate, ref random, usedParties, usedLeads);
                    candidate.Incumbent = false;
                    if (candidate.First == State.Mayor.First && candidate.Last == State.Mayor.Last)
                    {
                        candidate.Last = (candidate.Last + 1) % CivicCatalogue.LastNames.Length;
                    }
                }
                usedParties |= 1 << candidate.Party;
                usedLeads |= 1 << candidate.Lead;
            }
            Candidate me = null;
            try
            {
                me = PlayerStands != null ? PlayerStands(recall) : null;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
            }
            if (me != null)
            {
                Seat(me);
            }
            // The first poll is where the city stands today, straight.
            Targets(day);
            for (int i = 0; i < State.CandidateCount; i++)
            {
                State.Polls[i] = _scores[i];
            }
            CivicText.Toast(NotificationType.Info, recall ? "reallife_civic_campaign_recall" : "reallife_civic_campaign_begins",
                "count", State.CandidateCount.ToString(), "days", Mathf.Max(1, State.ElectionDay - day).ToString(), 14f);
            CampaignChanged?.Invoke();
        }

        /// <summary>The player's candidate joins a campaign under way: in their party's place, a free one, or the last challenger's. Returns the slot, or -1 with no campaign on.</summary>
        public int JoinRace(Candidate me)
        {
            if (!Campaigning || me == null)
            {
                return -1;
            }
            int slot = Seat(me);
            Targets(TimeHelper.CurrentDay);
            State.Polls[slot] = _scores[slot];
            float total = 0f;
            for (int c = 0; c < State.CandidateCount; c++)
            {
                total += State.Polls[c];
            }
            for (int c = 0; c < State.CandidateCount && total > 0f; c++)
            {
                State.Polls[c] /= total;
            }
            CampaignChanged?.Invoke();
            return slot;
        }

        // The sitting mayor's place if the player is mayor, else their party's, a free one, or the last challenger's.
        private int Seat(Candidate me)
        {
            int slot = -1;
            if (State.Mayor.Player)
            {
                slot = 0;
                if (State.CandidateCount > 0 && !State.Candidates[0].Incumbent && State.CandidateCount < CivicCatalogue.MaxCandidates)
                {
                    // Nobody is in the mayor's place: the field moves up one.
                    for (int i = State.CandidateCount; i > 0; i--)
                    {
                        State.Candidates[i].CopyFrom(State.Candidates[i - 1]);
                        State.Polls[i] = State.Polls[i - 1];
                        State.Momentum[i] = State.Momentum[i - 1];
                        State.Donated[i] = State.Donated[i - 1];
                    }
                    State.CandidateCount++;
                }
            }
            for (int i = 0; slot < 0 && i < State.CandidateCount; i++)
            {
                if (me.Party < CivicCatalogue.Parties.Length && State.Candidates[i].Party == me.Party && !State.Candidates[i].Incumbent)
                {
                    slot = i;   // the party's nomination is the player's: its own candidate stands down
                }
            }
            if (slot < 0)
            {
                slot = State.CandidateCount < CivicCatalogue.MaxCandidates ? State.CandidateCount++ : State.CandidateCount - 1;
                if (State.Candidates[slot].Incumbent && slot > 0)
                {
                    slot--;
                }
            }
            if (State.CandidateCount == 0)
            {
                State.CandidateCount = 1;
            }
            State.Candidates[slot].CopyFrom(me);
            State.Candidates[slot].Player = true;
            State.Candidates[slot].Incumbent = State.Mayor.Player;
            State.Momentum[slot] = 0f;
            State.Donated[slot] = 0;
            State.PlayerSlot = slot;
            return slot;
        }

        // A mayor standing again keeps what they have delivered at the head of their platform.
        private void KeepRecord(Candidate candidate)
        {
            int kept = 0;
            for (int i = 0; i < State.Mayor.Planks && kept < 2; i++)
            {
                PolicyDef policy = CivicCatalogue.At(State.Mayor.Platform[i]);
                if (policy == null || State.Levers[(int)policy.Lever] != policy.Dir)
                {
                    continue;
                }
                // Put it first, dropping whatever plank of the new draw is on the same lever.
                for (int p = 0; p < candidate.Planks; p++)
                {
                    PolicyDef other = CivicCatalogue.At(candidate.Platform[p]);
                    if (other != null && other.Lever == policy.Lever)
                    {
                        candidate.Platform[p] = candidate.Platform[kept];
                        break;
                    }
                }
                if (kept < candidate.Planks)
                {
                    candidate.Platform[kept] = State.Mayor.Platform[i];
                    kept++;
                }
            }
        }

        // Somebody from the pools: a name, a party and a lead issue nobody else in this race has, and three or four planks on
        // different levers, at least two of them about that issue.
        internal static void Generate(Candidate candidate, ref CivicRandom random, int usedParties, int usedLeads)
        {
            candidate.First = random.Range(0, CivicCatalogue.FirstNames.Length);
            candidate.Last = random.Range(0, CivicCatalogue.LastNames.Length);
            candidate.Look = (int)(random.Next() & 0x7FFFFFFF);
            candidate.Party = random.Range(0, CivicCatalogue.Parties.Length);
            for (int tries = 0; tries < 8 && usedParties > 0 && (usedParties & (1 << candidate.Party)) != 0; tries++)
            {
                candidate.Party = (candidate.Party + 1) % CivicCatalogue.Parties.Length;
            }
            candidate.Lead = random.Range(0, CivicCatalogue.Issues);
            for (int tries = 0; tries < CivicCatalogue.Issues && usedLeads > 0 && (usedLeads & (1 << candidate.Lead)) != 0; tries++)
            {
                candidate.Lead = (candidate.Lead + 1) % CivicCatalogue.Issues;
            }
            candidate.Slogan = candidate.Lead * 3 + random.Range(0, 3);
            candidate.Planks = random.Value < 0.5f ? 3 : 4;
            int leverMask = 0;
            for (int plank = 0; plank < candidate.Platform.Length; plank++)
            {
                candidate.Platform[plank] = -1;
            }
            for (int plank = 0; plank < candidate.Planks; plank++)
            {
                bool onIssue = plank < 2;
                int start = random.Range(0, CivicCatalogue.All.Length);
                int found = -1;
                for (int pass = 0; pass < 2 && found < 0; pass++)
                {
                    for (int step = 0; step < CivicCatalogue.All.Length && found < 0; step++)
                    {
                        int index = (start + step) % CivicCatalogue.All.Length;
                        PolicyDef policy = CivicCatalogue.All[index];
                        if ((leverMask & (1 << (int)policy.Lever)) != 0 || (pass == 0 && onIssue && (int)policy.Issue != candidate.Lead))
                        {
                            continue;
                        }
                        found = index;
                    }
                }
                if (found < 0)
                {
                    candidate.Planks = plank;
                    break;
                }
                candidate.Platform[plank] = found;
                leverMask |= 1 << (int)CivicCatalogue.All[found].Lever;
            }
        }

        // How much each issue matters in the city today (1 is ordinary).
        private void Salience()
        {
            for (int i = 0; i < _salience.Length; i++)
            {
                _salience[i] = 1f;
            }
            _salience[(int)Issue.Safety] += Mathf.Min(1.6f, State.CrimeHeat * 0.3f);
            _salience[(int)Issue.Roads] += Mathf.Min(1.6f, State.CrashHeat * 0.35f) + LateSites * 0.1f;
            _salience[(int)Issue.Services] += State.Levers[(int)Lever.Cleaning] < 0 ? 0.35f : 0f;
            _salience[(int)Issue.Living] += (State.Levers[(int)Lever.Rent] > 0 ? 0.35f : 0f) + (State.Levers[(int)Lever.BusinessTax] > 0 ? 0.1f : 0f);
            _salience[(int)Issue.Business] += State.Levers[(int)Lever.BusinessTax] > 0 ? 0.3f : 0f;
            _salience[(int)Issue.Growth] += State.Levers[(int)Lever.Permits] < 0 ? 0.25f : 0f;
            for (int i = 0; i < _salience.Length; i++)
            {
                _salience[i] += _raised[i];
            }
        }

        /// <summary>A social media post raised an issue: up to <see cref="RaisedMost"/> more weight, fading a fifth a day.</summary>
        public void Raised(int issue, float weight)
        {
            if (issue >= 0 && issue < _raised.Length && weight > 0f)
            {
                _raised[issue] = Mathf.Min(RaisedMost, _raised[issue] + weight);
                Salience();
            }
        }

        private float MaxSalience()
        {
            float most = 1f;
            for (int i = 0; i < _salience.Length; i++)
            {
                most = Mathf.Max(most, _salience[i]);
            }
            return most;
        }

        /// <summary>The issue that matters most in the city today.</summary>
        public int TopIssue()
        {
            Salience();
            int top = 0;
            for (int i = 1; i < _salience.Length; i++)
            {
                if (_salience[i] > _salience[top])
                {
                    top = i;
                }
            }
            return top;
        }

        // Where the city stands today: shares into _scores. District -1 is the city as a whole.
        private void Targets(int day, int district = -1)
        {
            Salience();
            int cycle = CivicBridges.Cycle();
            for (int c = 0; c < State.CandidateCount; c++)
            {
                Candidate candidate = State.Candidates[c];
                float fit = 0f, planks = 0f;
                for (int i = 0; i < CivicCatalogue.Issues; i++)
                {
                    float stance = candidate.Stance(i);
                    float weight = _salience[i] * (district < 0 ? 1f : DistrictLean(district, i));
                    fit += stance * weight;
                    planks += stance;
                }
                float score = (planks > 0f ? fit / planks : 1f) * 1.1f;
                score += candidate.Player ? Player(district) : Appeal(c, district);
                score += State.Momentum[c];
                if (candidate.Incumbent)
                {
                    score += (State.Approval - 50f) / 40f + OfficeRules.EconomyTerm(cycle, true);
                }
                _raw[c] = score;
            }
            OfficeRules.Shares(_raw, State.CandidateCount, _scores);
        }

        private readonly float[] _raw = new float[CivicCatalogue.MaxCandidates];

        // The player's own part of a score (CivicOffice).
        private float Player(int district)
        {
            try
            {
                return PlayerTerm != null ? PlayerTerm(district) : 0f;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return 0f;
            }
        }

        // What a candidate has beyond their platform: fixed for the election, a little different in each district.
        private float Appeal(int candidate, int district)
        {
            var random = new CivicRandom(CivicRandom.Mix(State.Seed, State.Number * 13 + candidate, 307));
            float appeal = random.Range(-0.2f, 0.2f);
            if (district >= 0)
            {
                var local = new CivicRandom(CivicRandom.Mix(State.Seed, State.Number * 17 + candidate, district + 401));
                appeal += local.Range(-0.18f, 0.18f);
            }
            return appeal;
        }

        private void MovePolls(int day, float pull)
        {
            Targets(day);
            var random = new CivicRandom(CivicRandom.Mix(State.Seed, day, State.Number + 503));
            float total = 0f;
            for (int c = 0; c < State.CandidateCount; c++)
            {
                State.Polls[c] = Mathf.Max(0.02f, State.Polls[c] + (_scores[c] - State.Polls[c]) * pull + random.Range(-0.012f, 0.012f));
                total += State.Polls[c];
            }
            for (int c = 0; c < State.CandidateCount; c++)
            {
                State.Polls[c] /= total;
            }
        }

        public int Leader()
        {
            int lead = 0;
            for (int c = 1; c < State.CandidateCount; c++)
            {
                if (State.Polls[c] > State.Polls[lead])
                {
                    lead = c;
                }
            }
            return lead;
        }
    }
}
