using System;
using System.Collections.Generic;
using System.Globalization;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Election day: the polls, every district's count and the winner taking office.</summary>
    public sealed partial class CivicGovernment
    {
        public bool PollsOpen(int day, int hour)
        {
            return State.Phase == ElectionPhase.Campaign && day == State.ElectionDay && hour >= CivicCatalogue.PollsOpen && hour < CivicCatalogue.PollsClose;
        }

        /// <summary>The votes a candidate took in a district. The same every time it is asked, from the shares the count fixed.</summary>
        public int DistrictVotes(int district, int candidate)
        {
            if (candidate < 0 || candidate >= State.CandidateCount || district < 0 || district >= _city.Districts.Count)
            {
                return 0;
            }
            if (district < State.DistrictsCounted)
            {
                // The count's own shares for the district: the voters' preferences there (Count).
                var roll = new CivicRandom(CivicRandom.Mix(State.Seed, State.Number, district + 701));
                float turnedOut = OfficeRules.Turnout(roll.Value, Doors(district));
                return OfficeRules.Votes(_city.Districts[district].Electorate, turnedOut, State.DistrictShares[district * CivicCatalogue.MaxCandidates + candidate]);
            }
            float total = 0f, own = 0f;
            for (int c = 0; c < State.CandidateCount; c++)
            {
                var random = new CivicRandom(CivicRandom.Mix(State.Seed, State.Number * 19 + c, district + 601));
                float share = State.Final[c] * random.Range(0.72f, 1.28f);
                total += share;
                if (c == candidate)
                {
                    own = share;
                }
            }
            var turnout = new CivicRandom(CivicRandom.Mix(State.Seed, State.Number, district + 701));
            float cast = _city.Districts[district].Electorate * turnout.Range(0.34f, 0.56f);
            return total > 0f ? Mathf.RoundToInt(cast * own / total) : 0;
        }

        /// <summary>A candidate's votes across the city, the player's own included (and in a Going Public session every other player's).</summary>
        public int CityVotes(int candidate)
        {
            int votes = (State.Voted == candidate ? 1 : 0) + (candidate >= 0 && candidate < State.OthersVotes.Length ? State.OthersVotes[candidate] : 0);
            if (_city.Districts.Count == 0)
            {
                // The city could not be read: one district of 50,000 with a turnout of 45 %.
                return votes + (candidate >= 0 && candidate < State.CandidateCount ? Mathf.RoundToInt(22500f * State.Final[candidate]) : 0);
            }
            for (int d = 0; d < _city.Districts.Count; d++)
            {
                votes += DistrictVotes(d, candidate);
            }
            return votes;
        }

        private int Doors(int district)
        {
            try
            {
                return DistrictDoors != null ? DistrictDoors(district) : 0;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return 0;
            }
        }

        private readonly float[] _cityFit = new float[CivicCatalogue.MaxCandidates], _districtFit = new float[CivicCatalogue.MaxCandidates];
        private readonly float[] _local = new float[CivicCatalogue.MaxCandidates], _share = new float[CivicCatalogue.MaxCandidates];
        private readonly float[] _lean = new float[CivicCatalogue.Issues];

        // Each district's shares, from what its voters prefer: a candidate's city share leaned by how their platform fits the
        // district against the city, by their local appeal there, and for the player by their home, businesses, doors and posters.
        private void CountDistricts()
        {
            int districts = Math.Min(_city.Districts.Count, CivicCatalogue.MaxDistricts);
            for (int c = 0; c < State.CandidateCount; c++)
            {
                _cityFit[c] = OfficeRules.Fit(State.Candidates[c], null);
            }
            for (int d = 0; d < districts; d++)
            {
                for (int i = 0; i < _lean.Length; i++)
                {
                    _lean[i] = DistrictLean(d, i);
                }
                for (int c = 0; c < State.CandidateCount; c++)
                {
                    Candidate candidate = State.Candidates[c];
                    _districtFit[c] = OfficeRules.Fit(candidate, _lean);
                    float local = 0f;
                    if (candidate.Player)
                    {
                        try
                        {
                            local = PlayerLocal != null ? PlayerLocal(d) : 0f;
                        }
                        catch (Exception e)
                        {
                            CivicDirector.Logger?.Error(e);
                        }
                    }
                    else
                    {
                        local = Appeal(c, d) - Appeal(c, -1);
                    }
                    _local[c] = local;
                }
                OfficeRules.DistrictShares(State.Final, _cityFit, _districtFit, _local, State.CandidateCount, _share);
                for (int c = 0; c < CivicCatalogue.MaxCandidates; c++)
                {
                    State.DistrictShares[d * CivicCatalogue.MaxCandidates + c] = _share[c];
                }
            }
            State.DistrictsCounted = districts;
        }

        // The count: the last poll with election-day movement is fixed, every district is added up, the winner is named and takes office tomorrow.
        private void Count(int day)
        {
            MovePolls(day, 0.2f);
            for (int c = 0; c < CivicCatalogue.MaxCandidates; c++)
            {
                State.Final[c] = c < State.CandidateCount ? State.Polls[c] : 0f;
            }
            CountDistricts();
            int winner = 0, total = 0;
            for (int c = 0; c < State.CandidateCount; c++)
            {
                _votes[c] = CityVotes(c);
                total += _votes[c];
                if (_votes[c] > _votes[winner] || (_votes[c] == _votes[winner] && c == State.Voted))
                {
                    winner = c;
                }
            }
            State.Winner = winner;
            State.Turnout = total;
            State.Phase = ElectionPhase.Counted;
            Candidate elected = State.Candidates[winner];
            bool held = elected.Incumbent;

            // What the outgoing mayor did that the winner does not stand for goes first; then the winner's platform, one change a day.
            int when = day + 1;
            for (int lever = 0; lever < CivicCatalogue.Levers; lever++)
            {
                if (State.Levers[lever] != 0 && !Covers(elected, lever))
                {
                    Schedule(lever, 0, when++);
                }
            }
            for (int i = 0; i < elected.Planks; i++)
            {
                PolicyDef policy = CivicCatalogue.At(elected.Platform[i]);
                if (policy != null && State.Levers[(int)policy.Lever] != policy.Dir)
                {
                    Schedule((int)policy.Lever, policy.Dir, when++);
                }
            }

            // The council follows the vote: seats in proportion, the winner's party never fewer than two.
            int seat = 0;
            for (int c = 0; c < State.CandidateCount && seat < State.Council.Length; c++)
            {
                int seats = Mathf.RoundToInt(State.Final[c] * State.Council.Length);
                if (c == winner)
                {
                    seats = Mathf.Max(2, seats);
                }
                for (int s = 0; s < seats && seat < State.Council.Length; s++)
                {
                    State.Council[seat++] = State.Candidates[c].Party;
                }
            }
            while (seat < State.Council.Length)
            {
                State.Council[seat++] = elected.Party;
            }

            // Donations to the winner are remembered: a faster permit for every 1,000 given (at the start's prices), two at most.
            int per = Mathf.Max(1, CivicCatalogue.FavourPerNow);
            if (State.Donated[winner] >= per)
            {
                State.Favours = Mathf.Min(2, State.Favours + State.Donated[winner] / per);
            }
            State.Mayor.CopyFrom(elected);
            State.Mayor.Incumbent = true;
            State.MayorIsPlayer = elected.Player;
            State.TermStart = day + 1;
            State.Approval = Mathf.Clamp(46f + State.Final[winner] * 30f, 40f, 72f);
            State.LowDays = 0;
            State.PetitionRest = day + 7;
            State.NextCouncilVote = day + CivicCatalogue.CouncilVoteEvery;

            string share = Percent(total > 0 ? (float)_votes[winner] / total : State.Final[winner]);
            CivicText.Toast(NotificationType.Success, held ? "reallife_civic_result_held" : "reallife_civic_result_won",
                "name", CivicText.Name(elected), "party", CivicText.Party(elected), "share", share, 16f);
            if (State.Voted == winner)
            {
                ModState.AddModifier(ModState.MoodUp);
            }
            CivicBridges.Report(held ? "reallife_civic_news_held" : "reallife_civic_news_won", "reallife_civic_news_result_body");
            CivicDirector.Logger?.Info("Civic: election " + State.Number + " counted: winner slot " + winner + " with " + _votes[winner] + " of " + total + " votes; "
                                       + State.Pending.Count + " policy change(s) scheduled.");
            try
            {
                Counted?.Invoke(day);
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
            }
            CampaignChanged?.Invoke();
        }

        private static bool Covers(Candidate candidate, int lever)
        {
            for (int i = 0; i < candidate.Planks; i++)
            {
                PolicyDef policy = CivicCatalogue.At(candidate.Platform[i]);
                if (policy != null && (int)policy.Lever == lever)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
