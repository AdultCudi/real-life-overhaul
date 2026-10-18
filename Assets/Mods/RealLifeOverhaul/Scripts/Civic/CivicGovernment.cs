using System;
using System.Collections.Generic;
using System.Globalization;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The city's government: mayor and council, an election every eight weeks after a week's campaign, the count, policies
    /// phased in, approval, petitions, council votes and recalls; arithmetic on <see cref="GovState"/> once per in-game hour,
    /// nothing per frame. In a Going Public session the host runs the hours and a guest follows (<see cref="FollowTick"/>).
    /// </summary>
    public sealed partial class CivicGovernment
    {
        private const int CatchUpHours = 24 * 21;   // after a longer gap than this the calendar simply moves on
        private const float PollPull = 0.34f;   // share of the gap to the city's view the polls close each day

        public readonly GovState State = new GovState();
        private readonly CivicCity _city;
        private readonly float[] _salience = new float[CivicCatalogue.Issues];
        private readonly float[] _raised = new float[CivicCatalogue.Issues];   // added by social media posts; fades a fifth a day, not saved
        public const float RaisedMost = 0.5f;          // at most, on top of the city's own weight of an issue
        public const float MoodPullMost = 1f;          // points of approval a day social media's mood moves, at most
        private readonly float[] _scores = new float[CivicCatalogue.MaxCandidates];
        private readonly int[] _votes = new int[CivicCatalogue.MaxCandidates];
        private bool _crimeWasOn, _crashWasOn;
        private int _crimesToday, _crashesToday;
        private long _followHour;                       // a guest: the last hour it looked at
        private readonly HashSet<string> _ballots = new HashSet<string>();   // the host: the players whose ballot is in, in election _ballotsOf
        private int _ballotsOf = -1;

        /// <summary>Sites running late, set by the construction programme each day.</summary>
        public int LateSites;

        public Action LeversChanged;
        public Action CampaignChanged;   // campaign dressing goes up or comes down

        // The player's own part, set by CivicOffice; each is optional and runs no hour of its own.
        public Func<bool, Candidate> PlayerStands;      // a campaign starts (true: a recall): the player's candidate for mayor, or null
        public Func<int, float> PlayerTerm;             // the player's part beyond their platform in a district (-1 the city)
        public Func<int, float> PlayerLocal;            // what the player has of their own in a district at the count
        public Func<int, int> DistrictDoors;            // doors knocked in a district: a little more of it turns out
        public Action<int> Counted;                     // the count is in: the player's race and office follow
        public Action<int> DayBegins;                   // a day has run (after approval, petitions and the council)
        public Func<bool> OfficeSits;                   // the player sits: the council meets on the player's calendar
        public Action<int, bool> ToAgenda;              // a motion for the next meeting (the policy; whether a petition put it there)
        public Func<float> ApprovalExtra;               // the mayor's approval beyond the city's own (budget, scandals)
        public Func<Department, int> DepartmentLevel;   // a department's budget level, -2 to 2

        private int Level(Department department)
        {
            try
            {
                return DepartmentLevel != null ? DepartmentLevel(department) : 0;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return 0;
            }
        }

        /// <summary>The player stood down as mayor: an election for the office in a week, unless one is on its way.</summary>
        public void Vacate(int day)
        {
            if (State.Phase == ElectionPhase.None)
            {
                State.ElectionDay = day + CivicCatalogue.CampaignDays;
                StartCampaign(day, false);
            }
        }

        public CivicGovernment(CivicCity city)
        {
            _city = city;
        }

        public bool Campaigning => State.Phase == ElectionPhase.Campaign;

        /// <summary>A save the mod has not seen before: a mayor in office with the city as it came, and the first election two weeks off.</summary>
        public void NewSave(int day, long hour)
        {
            State.Reset();
            State.Mayor.Player = false;
            int seed = CivicRandom.Mix(day * 7919 + 17, (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF), Environment.TickCount);
            State.Seed = seed == 0 ? 1 : seed;
            State.FirstDay = day;
            State.LastHour = hour;
            var random = new CivicRandom(State.Seed);
            Generate(State.Mayor, ref random, -1, -1);
            State.Mayor.Incumbent = true;
            State.Mayor.Planks = 0;   // in office before the player came: the city as it is is their record
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

        /// <summary>About once a second: notes crime and accident scenes as they begin and runs every hour gone by since the last. Returns the hours run.</summary>
        public int Tick(int day, int hour)
        {
            Scenes();
            _followHour = 0;

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

        // Crime and accident scenes as they begin: the heat behind approval and the issues.
        private void Scenes()
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
        }

        /// <summary>A guest, in place of <see cref="Tick"/>: tells the host once a day what crime and accidents it saw, and says when its polling place opens.</summary>
        public void FollowTick(int day, int hour)
        {
            Scenes();
            long now = (long)day * 24 + hour;
            if (_followHour <= 0 || now < _followHour || now - _followHour > 48)
            {
                _followHour = now;
                return;
            }
            while (_followHour < now)
            {
                _followHour++;
                int d = (int)(_followHour / 24), h = (int)(_followHour % 24);
                if (h == 0 && (_crimesToday > 0 || _crashesToday > 0 || CivicBridges.Crimes > 0 || CivicBridges.Accidents > 0))
                {
                    SharedCity.Ask('G', "h|" + (_crimesToday + CivicBridges.Crimes) + "|" + (_crashesToday + CivicBridges.Accidents));
                    _crimesToday = _crashesToday = 0;
                    CivicBridges.Crimes = CivicBridges.Accidents = 0;
                }
                if (State.Phase == ElectionPhase.Campaign && d == State.ElectionDay && h == CivicCatalogue.PollsOpen)
                {
                    PollsOpenToast();
                }
            }
        }

        private void PollsOpenToast()
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

        /// <summary>The host: one line of what a guest's player did, applied within the bounds the guest's game keeps. False if it cannot be read or does not apply.</summary>
        public bool HostRequest(string line, string who, int day, int hour)
        {
            string[] f = line.Split('|');
            if (f.Length < 2)
            {
                return false;
            }
            int a = Int(f, 1), b = Int(f, 2);
            float x = f.Length > 2 && float.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float v) && !float.IsNaN(v) && !float.IsInfinity(v) ? v : 0f;
            bool valid = a >= 0 && a < State.CandidateCount;
            switch (f[0])
            {
                case "m":   // momentum: a donation, an endorsement (or the scandal of one), leaflets, a rally
                    if (!Campaigning || !valid)
                    {
                        return false;
                    }
                    State.Momentum[a] += Mathf.Clamp(x, -0.15f, 0.26f);
                    return true;
                case "v":   // a ballot
                    if (_ballotsOf != State.Number)
                    {
                        _ballotsOf = State.Number;
                        _ballots.Clear();
                    }
                    if (!PollsOpen(day, hour) || !valid || !_ballots.Add(who ?? string.Empty))
                    {
                        return false;
                    }
                    State.OthersVotes[a]++;
                    return true;
                case "p":   // a petition started
                {
                    PolicyDef policy = CivicCatalogue.At(a);
                    if (policy == null || State.PetitionPolicy >= 0 || day < State.PetitionRest || State.Phase != ElectionPhase.None || State.Levers[(int)policy.Lever] == policy.Dir)
                    {
                        return false;
                    }
                    OpenPetition(a, day, false);
                    CivicText.Toast(NotificationType.Info, "reallife_civic_petition_new", "policy", CivicText.T(policy.Name), "needed", State.PetitionNeeded.ToString("N0"), 12f);
                    return true;
                }
                case "s":   // signatures
                    if (State.PetitionPolicy < 0)
                    {
                        return false;
                    }
                    State.PetitionSigned += Mathf.Clamp(a, 1, 51);
                    return true;
                case "a":   // approval: a favour that became a story
                    State.Approval = Mathf.Max(3f, State.Approval + Mathf.Clamp(a, -5, 0));
                    return true;
                case "h":   // the crime and accidents a guest saw yesterday
                    _crimesToday += Mathf.Clamp(a, 0, 20);
                    _crashesToday += Mathf.Clamp(b, 0, 20);
                    return true;
            }
            return false;
        }

        private static int Int(string[] f, int at)
        {
            return at < f.Length && int.TryParse(f[at], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : -1;
        }

        private static void Share(string line)
        {
            if (SharedCity.Follows('G'))
            {
                SharedCity.Ask('G', line);
            }
        }

        private static string F(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
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
                PollsOpenToast();
            }
            if ((day == State.ElectionDay && hour >= CivicCatalogue.ResultHour) || day > State.ElectionDay)
            {
                Count(day);
            }
        }

        private void NewDay(int day)
        {
            // Yesterday's crime and accidents fade a fifth a day; the city's police and streets budgets scale them.
            State.CrimeHeat = State.CrimeHeat * 0.8f + (_crimesToday + CivicBridges.Crimes) * OfficeRules.HeatScale(Level(Department.Police));
            State.CrashHeat = State.CrashHeat * 0.8f + (_crashesToday + CivicBridges.Accidents) * OfficeRules.HeatScale(Level(Department.Streets));
            _crimesToday = 0;
            _crashesToday = 0;
            CivicBridges.Crimes = 0;
            CivicBridges.Accidents = 0;

            Approve(day);
            Petition(day);
            Council(day);
            DayBegins?.Invoke(day);

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
