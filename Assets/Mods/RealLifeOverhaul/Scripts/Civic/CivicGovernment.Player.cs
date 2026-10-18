using System;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What the player can do in the city's politics: donate, endorse, canvass, attend a rally, vote, petition, call in a favour; and the test buttons.</summary>
    public sealed partial class CivicGovernment
    {
        /// <summary>One donation of 500 (at the start's prices) to a campaign. Returns the locale key of what the campaign says.</summary>
        public string Donate(int candidate)
        {
            if (!Campaigning || candidate < 0 || candidate >= State.CandidateCount)
            {
                return "reallife_civic_no_campaign";
            }
            if (State.Candidates[candidate].Player)
            {
                return "reallife_civic_office_own_campaign";
            }
            int step = CivicCatalogue.DonationStepNow;
            if (State.Donated[candidate] >= CivicCatalogue.DonationCapNow)
            {
                return "reallife_civic_donate_cap";
            }
            if (!GameManager.ChangeMoneySafe(-step, new TransactionInfo("reallife_transaction_civic_donation"), null, null, force: false, showNotification: true))
            {
                return "reallife_civic_donate_broke";
            }
            State.Donated[candidate] += step;
            State.Momentum[candidate] += 0.04f;
            Share("m|" + candidate + "|" + F(0.04f));
            return "reallife_civic_donate_thanks";
        }

        /// <summary>How much the player's word is worth: more with every business they run (five at most), and with a following on social media (two businesses' worth at most).</summary>
        public static float EndorsementWeight()
        {
            float following = 0f;
            try
            {
                following = CivicBridges.Following != null ? Mathf.Clamp(CivicBridges.Following(), 0f, FollowingMost) : 0f;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
            }
            return 0.03f + BusinessWeight * Mathf.Min(5, CivicCity.PlayerBusinesses()) + Mathf.Clamp(CivicBridges.Standing(), -50, 50) * 0.001f + following;
        }

        public const float BusinessWeight = 0.035f;            // an endorsement's weight for each business the player runs
        public const float FollowingMost = 2f * BusinessWeight;   // what a following can add: two businesses' worth

        /// <summary>The player's public endorsement: once an election. With a criminal record it is a scandal that costs the candidate instead.</summary>
        public string Endorse(int candidate)
        {
            if (!Campaigning || candidate < 0 || candidate >= State.CandidateCount)
            {
                return "reallife_civic_no_campaign";
            }
            if (State.Endorsed >= 0)
            {
                return "reallife_civic_endorse_already";
            }
            if (State.Candidates[candidate].Player)
            {
                return "reallife_civic_office_own_campaign";
            }
            State.Endorsed = candidate;
            if (CivicBridges.HasRecord())
            {
                State.Scandal = true;
                State.Momentum[candidate] -= 0.15f;
                Share("m|" + candidate + "|" + F(-0.15f));
                ModState.AddModifier(ModState.MoodDown);
                CivicBridges.Report("reallife_civic_news_scandal", "reallife_civic_news_scandal_body");
                return "reallife_civic_endorse_scandal";
            }
            float weight = EndorsementWeight();
            State.Momentum[candidate] += weight;
            Share("m|" + candidate + "|" + F(weight));
            return "reallife_civic_endorse_thanks";
        }

        /// <summary>An afternoon's canvassing is over: every leaflet taken is worth a little.</summary>
        public void Volunteered(int candidate, int leaflets)
        {
            if (!Campaigning || candidate < 0 || candidate >= State.CandidateCount)
            {
                return;
            }
            State.Volunteered = candidate;
            State.Momentum[candidate] += Mathf.Min(30, leaflets) * 0.002f;
            Share("m|" + candidate + "|" + F(Mathf.Min(30, leaflets) * 0.002f));
        }

        /// <summary>The player stood through a rally. False if they had already been to one of this candidate's.</summary>
        public bool AttendRally(int candidate)
        {
            if (!Campaigning || candidate < 0 || candidate >= State.CandidateCount || (State.Rallies & (1 << candidate)) != 0)
            {
                return false;
            }
            State.Rallies |= 1 << candidate;
            State.Momentum[candidate] += 0.02f;
            Share("m|" + candidate + "|" + F(0.02f));
            ModState.AddModifier(ModState.MoodUp);
            return true;
        }

        /// <summary>A ballot is cast. Returns the locale key of what the clerk says.</summary>
        public string Vote(int candidate, int day, int hour)
        {
            if (!PollsOpen(day, hour))
            {
                return "reallife_civic_vote_closed";
            }
            if (State.Voted >= 0)
            {
                return "reallife_civic_vote_already";
            }
            if (candidate < 0 || candidate >= State.CandidateCount)
            {
                return "reallife_civic_vote_closed";
            }
            State.Voted = candidate;
            State.VotesCast++;
            Share("v|" + candidate);
            ModState.AddModifier(ModState.CivicVoted);
            return "reallife_civic_vote_cast";
        }

        /// <summary>The player starts a petition for a policy. Returns the locale key of the answer.</summary>
        public string StartPetition(int policyIndex, int day)
        {
            PolicyDef policy = CivicCatalogue.At(policyIndex);
            if (policy == null)
            {
                return "reallife_civic_petition_none";
            }
            if (State.PetitionPolicy >= 0)
            {
                return "reallife_civic_petition_busy";
            }
            if (day < State.PetitionRest || State.Phase != ElectionPhase.None)
            {
                return "reallife_civic_petition_rest";
            }
            if (State.Levers[(int)policy.Lever] == policy.Dir)
            {
                return "reallife_civic_petition_in_force";
            }
            OpenPetition(policyIndex, day, true);
            Share("p|" + policyIndex);
            SignPetition();
            return "reallife_civic_petition_started";
        }

        /// <summary>The player signs: their own name, and five more for every business they run (signed at the counter).</summary>
        public bool SignPetition()
        {
            if (State.PetitionPolicy < 0 || State.PetitionISigned)
            {
                return false;
            }
            State.PetitionISigned = true;
            int names = 1 + 5 * Mathf.Min(10, CivicCity.PlayerBusinesses());
            State.PetitionSigned += names;
            Share("s|" + names);
            return true;
        }

        /// <summary>A permit favour is called in. Returns true if a story came of it (one time in four): approval and the player's mood pay.</summary>
        public bool SpendFavour(int day)
        {
            if (State.Favours <= 0)
            {
                return false;
            }
            State.Favours--;
            var random = new CivicRandom(CivicRandom.Mix(State.Seed, day, State.Favours + 809));
            if (random.Value >= CivicCatalogue.PayToPlayOdds)
            {
                return false;
            }
            State.Approval = Mathf.Max(3f, State.Approval - 5f);
            Share("a|-5");
            ModState.AddModifier(ModState.MoodDown);
            CivicBridges.Report("reallife_civic_news_paytoplay", "reallife_civic_news_paytoplay_body");
            return true;
        }

        /// <summary>Test button: a campaign starts now, with election day a week off.</summary>
        public void TestCampaign(int day)
        {
            State.Pending.Clear();
            State.ElectionDay = day + CivicCatalogue.CampaignDays;
            StartCampaign(day, false);
        }

        /// <summary>Test button: today is election day (a campaign is started first if there is none); the polls are open until the count is asked for.</summary>
        public void TestElectionDay(int day, int hour)
        {
            bool already = Campaigning && State.ElectionDay == day;
            if (!Campaigning)
            {
                State.ElectionDay = day;
                StartCampaign(day, false);
            }
            State.ElectionDay = day;
            if (already || hour >= CivicCatalogue.PollsClose || hour < CivicCatalogue.PollsOpen || State.Voted >= 0)
            {
                Count(day);   // the second press, too late (or early) to vote today, or the ballot is in: straight to the count
            }
            else
            {
                CivicText.Toast(NotificationType.Info, "reallife_civic_polls_open_plain", 12f);
            }
        }
    }
}
