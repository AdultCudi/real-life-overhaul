using System;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The city's government: a mayor and a council of five, an election every eight weeks, the
    /// campaign before it, the count, the winner's policies coming in a day at a time, approval,
    /// petitions, council votes and recalls. All of it is arithmetic on <see cref="GovState"/>,
    /// run once per in-game hour (and caught up hour by hour after a time skip); nothing here
    /// runs per frame and nothing here puts anything in the street.
    ///
    /// THE CALENDAR
    ///   first election   14 days after the mod first sees a save (never before the end of week 2)
    ///   term             56 days (8 weeks); a recall election 7 days after it is called
    ///   campaign         the 7 days before election day: posters, canvassers, rallies, polls
    ///   debate           3 days before, 19:00
    ///   election day     polls open 07:00 to 20:00; the count at 21:00
    ///   handover         the day after, 09:00; then one policy change a day
    ///
    /// THE POLLS move a third of the way each day towards what the city now thinks. What the
    /// city thinks of a candidate is how much of their platform speaks to each issue, weighted
    /// by how much that issue matters now: safety matters more after crime near the player
    /// (the crime scenes of this save, and whatever Law reports), roads after traffic accidents,
    /// services when the streets are dirty, living costs under landlord-friendly rent rules.
    /// On top: a sitting mayor carries their approval, and the player's own part (donations,
    /// an endorsement that weighs more the more businesses they run, an afternoon's leaflets,
    /// going to a rally). An endorsement from somebody with a criminal record is a scandal.
    /// </summary>
    public sealed class CivicGovernment
    {
        private const int CatchUpHours = 24 * 21;   // after a longer gap than this the calendar simply moves on
        private const float PollPull = 0.34f;
        private const float Sharpness = 1.5f;

        public readonly GovState State = new GovState();
        private readonly CivicCity _city;
        private readonly float[] _salience = new float[CivicCatalogue.Issues];
        private readonly float[] _raised = new float[CivicCatalogue.Issues];   // what posts on social media have added to each issue lately (fades by a fifth a day; not saved)
        public const float RaisedMost = 0.5f;          // at most, on top of the city's own weight of an issue
        public const float MoodPullMost = 1f;          // points of approval a day social media's mood moves, at most
        private readonly float[] _scores = new float[CivicCatalogue.MaxCandidates];
        private readonly int[] _votes = new int[CivicCatalogue.MaxCandidates];
        private bool _crimeWasOn, _crashWasOn;
        private int _crimesToday, _crashesToday;

        /// <summary>Set by the construction programme each day: how many sites are running late.</summary>
        public int LateSites;

        /// <summary>Raised when a lever has moved (the effects are applied afresh) and when campaign dressing must go up or come down.</summary>
        public Action LeversChanged;
        public Action CampaignChanged;

        public CivicGovernment(CivicCity city)
        {
            _city = city;
        }

        public bool Campaigning => State.Phase == ElectionPhase.Campaign;

        /// <summary>A save the mod has not seen before: a mayor in office with the city as it came, and the first election two weeks off.</summary>
        public void NewSave(int day, long hour)
        {
            State.Reset();
            int seed = CivicRandom.Mix(day * 7919 + 17, (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF), Environment.TickCount);
            State.Seed = seed == 0 ? 1 : seed;
            State.FirstDay = day;
            State.LastHour = hour;
            var random = new CivicRandom(State.Seed);
            Generate(State.Mayor, ref random, -1, -1);
            State.Mayor.Incumbent = true;
            State.Mayor.Planks = 0;   // in office since before the player came: the city as it is IS their record
            State.TermStart = day - random.Range(10, 40);
            State.Approval = random.Range(46f, 60f);
            for (int i = 0; i < State.Council.Length; i++)
            {
                State.Council[i] = i < 3 ? State.Mayor.Party : random.Range(0, CivicCatalogue.Parties.Length);
            }
            State.ElectionDay = day + CivicCatalogue.FirstElectionAfter;
            State.NextCouncilVote = day + random.Range(6, 12);
            State.PetitionRest = day + 3;
            State.SettleDay = day + 7;
        }

        // ---------------------------------------------------------------- the clock

        /// <summary>
        /// Called about once a second. Notes crime and accident scenes as they begin, and when
        /// the hour has changed runs each hour that has gone by since the last one processed.
        /// Returns how many hours it ran.
        /// </summary>
        public int Tick(int day, int hour)
        {
            bool crime = CrimeDirector.IsActive;
            if (crime && !_crimeWasOn)
            {
                _crimesToday++;
            }
            _crimeWasOn = crime;
            bool crash = TrafficAccidents.IsActive;
            if (crash && !_crashWasOn)
            {
                _crashesToday++;
            }
            _crashWasOn = crash;

            long now = (long)day * 24 + hour;
            if (State.LastHour <= 0 || now < State.LastHour || now - State.LastHour > CatchUpHours)
            {
                State.LastHour = now;   // a save from another time: nothing to catch up
                return 0;
            }
            int ran = 0;
            while (State.LastHour < now)
            {
                State.LastHour++;
                int d = (int)(State.LastHour / 24);
                int h = (int)(State.LastHour % 24);
                if (h == 0)
                {
                    NewDay(d);
                }
                Hour(d, h);
                ran++;
            }
            return ran;
        }

        private void Hour(int day, int hour)
        {
            if (hour == 9)
            {
                ApplyDue(day);
            }
            if (State.Phase != ElectionPhase.Campaign)
            {
                return;
            }
            if (!State.DebateHeld && ((day == State.ElectionDay - CivicCatalogue.DebateBefore && hour >= 19) || day > State.ElectionDay - CivicCatalogue.DebateBefore))
            {
                State.DebateHeld = true;
                // The debate moves the polls a little towards whoever speaks to what matters most tonight.
                MovePolls(day, 0.25f);
                CivicText.Toast(NotificationType.Info, "reallife_civic_debate_tonight", 12f);
            }
            if (day == State.ElectionDay && hour == CivicCatalogue.PollsOpen)
            {
                District near = PlayerDistrict();
                if (near != null && near.PollingAddress != null)
                {
                    CivicText.Toast(NotificationType.Info, "reallife_civic_polls_open", "address", near.PollingAddress, "district", CivicCity.Name(near), 14f);
                }
                else
                {
                    CivicText.Toast(NotificationType.Info, "reallife_civic_polls_open_plain", 12f);
                }
            }
            if ((day == State.ElectionDay && hour >= CivicCatalogue.ResultHour) || day > State.ElectionDay)
            {
                Count(day);
            }
        }

        private void NewDay(int day)
        {
            // Yesterday's crime and accidents, with what other features reported, fading a fifth a day.
            State.CrimeHeat = State.CrimeHeat * 0.8f + _crimesToday + CivicBridges.Crimes;
            State.CrashHeat = State.CrashHeat * 0.8f + _crashesToday + CivicBridges.Accidents;
            _crimesToday = 0;
            _crashesToday = 0;
            CivicBridges.Crimes = 0;
            CivicBridges.Accidents = 0;

            Approve(day);
            Petition(day);
            Council(day);

            if (State.Phase == ElectionPhase.Counted && day > State.ElectionDay + 1)
            {
                State.Phase = ElectionPhase.None;
                State.ElectionDay = State.ElectionDay + CivicCatalogue.TermDays;
                State.Recall = false;
            }
            if (State.Phase == ElectionPhase.None)
            {
                if (State.ElectionDay < day)
                {
                    State.ElectionDay = day + CivicCatalogue.CampaignDays;   // the calendar was skipped past: the next one is a week off
                }
                if (day >= State.ElectionDay - CivicCatalogue.CampaignDays)
                {
                    StartCampaign(day, false);
                }
            }
            else if (State.Phase == ElectionPhase.Campaign)
            {
                MovePolls(day, PollPull);
                int left = State.ElectionDay - day;
                if (left == 3 || left == 1)
                {
                    int lead = Leader();
                    CivicText.Toast(NotificationType.Info, "reallife_civic_poll_news", "name", CivicText.Name(State.Candidates[lead]),
                        "share", Percent(State.Polls[lead]), "days", left.ToString(), 11f);
                }
            }
        }

        // ---------------------------------------------------------------- approval, petitions, the council

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
                CouncilVote(policy, day, true);
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
            if (day < State.NextCouncilVote || State.Phase != ElectionPhase.None)
            {
                return;
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
                float odds = 0.25f + 0.35f * support + (petitioned ? 0.2f : 0f) + (State.Council[seat] == State.Mayor.Party && InPlatform(State.Mayor, policyIndex) ? 0.25f : 0f);
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

        // ---------------------------------------------------------------- levers

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

        /// <summary>
        /// A push on the council that won (Wiring/StandingLinks: the player's lobbying): one lever is to move to a position in
        /// so many days, announced on the day as any change is. False, and nothing changed, if it stands there already or a
        /// change of it is on its way.
        /// </summary>
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

        // ---------------------------------------------------------------- the campaign

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
            // The mayor stands again three times in four (always, to answer a recall), in slot 0 with promises for the next term.
            bool mayorStands = recall || random.Value < 0.75f;
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

        // Somebody from the pools: a name, a party and an issue nobody else in this race has, and three or four planks on different levers,
        // at least two of them about that issue.
        private static void Generate(Candidate candidate, ref CivicRandom random, int usedParties, int usedLeads)
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

        private static bool InPlatform(Candidate candidate, int policyIndex)
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

        /// <summary>A post on social media raised an issue (Wiring/FlareLinks): its weight in the council a little more, up to half a point, fading by a fifth a day.</summary>
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
            float total = 0f;
            for (int c = 0; c < State.CandidateCount; c++)
            {
                Candidate candidate = State.Candidates[c];
                float fit = 0f, planks = 0f;
                for (int i = 0; i < CivicCatalogue.Issues; i++)
                {
                    float stance = candidate.Stance(i);
                    float weight = _salience[i] * (district < 0 ? 1f : Lean(district, i));
                    fit += stance * weight;
                    planks += stance;
                }
                float score = (planks > 0f ? fit / planks : 1f) * 1.1f;
                score += Appeal(c, district);
                score += State.Momentum[c];
                if (candidate.Incumbent)
                {
                    score += (State.Approval - 50f) / 40f;
                }
                _scores[c] = Mathf.Exp(score * Sharpness);
                total += _scores[c];
            }
            for (int c = 0; c < CivicCatalogue.MaxCandidates; c++)
            {
                _scores[c] = c < State.CandidateCount && total > 0f ? _scores[c] / total : 0f;
            }
        }

        // How much a district cares about an issue compared with the city: 0.7 to 1.3, fixed for the save.
        private float Lean(int district, int issue)
        {
            var random = new CivicRandom(CivicRandom.Mix(State.Seed, district * 31 + 5, issue + 211));
            return random.Range(0.7f, 1.3f);
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

        // ---------------------------------------------------------------- election day

        /// <summary>True while a ballot can be cast.</summary>
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

        /// <summary>A candidate's votes across the city, the player's own included.</summary>
        public int CityVotes(int candidate)
        {
            int votes = State.Voted == candidate ? 1 : 0;
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

        // The count: the last poll with election-day movement is fixed, every district is added up, the winner is named and takes office tomorrow.
        private void Count(int day)
        {
            MovePolls(day, 0.2f);
            for (int c = 0; c < CivicCatalogue.MaxCandidates; c++)
            {
                State.Final[c] = c < State.CandidateCount ? State.Polls[c] : 0f;
            }
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

        // ---------------------------------------------------------------- what the player can do

        /// <summary>One donation of 500 (at the start's prices) to a campaign. Returns the locale key of what the campaign says.</summary>
        public string Donate(int candidate)
        {
            if (!Campaigning || candidate < 0 || candidate >= State.CandidateCount)
            {
                return "reallife_civic_no_campaign";
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
            State.Endorsed = candidate;
            if (CivicBridges.HasRecord())
            {
                State.Scandal = true;
                State.Momentum[candidate] -= 0.15f;
                ModState.AddModifier(ModState.MoodDown);
                CivicBridges.Report("reallife_civic_news_scandal", "reallife_civic_news_scandal_body");
                return "reallife_civic_endorse_scandal";
            }
            State.Momentum[candidate] += EndorsementWeight();
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
            State.PetitionSigned += 1 + 5 * Mathf.Min(10, CivicCity.PlayerBusinesses());
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
            ModState.AddModifier(ModState.MoodDown);
            CivicBridges.Report("reallife_civic_news_paytoplay", "reallife_civic_news_paytoplay_body");
            return true;
        }

        // ---------------------------------------------------------------- tests

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

        // ---------------------------------------------------------------- small things

        public int Electorate()
        {
            return _city.Electorate > 0 ? _city.Electorate : 50000;
        }

        /// <summary>The district the player is in (the game's own note of it), else the one whose polling place is nearest.</summary>
        public District PlayerDistrict()
        {
            string current = NeighborhoodHelper.CurrentNeighborhood;
            for (int i = 0; i < _city.Districts.Count; i++)
            {
                if (_city.Districts[i].Id == current)
                {
                    return _city.Districts[i];
                }
            }
            return PlayerHelper.PlayerController != null ? _city.NearestPolling(PlayerHelper.GetPosition(), out _) : null;
        }

        public static string Percent(float share)
        {
            return Mathf.RoundToInt(share * 100f).ToString();
        }
    }
}
