using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>A matter before the council at its next meeting.</summary>
    public sealed class OfficeMotion
    {
        public const int KindPolicy = 0, KindBudget = 1;
        public const int ByCity = 0, ByPlayer = 1, ByMayor = 2, ByPetition = 3;

        public int Kind;
        public int Policy = -1;                          // an index into CivicCatalogue.All (a policy motion)
        public int By;
        public int Day;                                  // the day it was put on the agenda
        public bool Undeclared;                          // the player tabled it as mayor with an interest in it they did not declare
        public readonly int[] Budget = new int[4];       // the departments' levels (a budget motion)
    }

    /// <summary>A vote the player cast on something they had an interest in, without declaring it.</summary>
    public sealed class OfficeSecret
    {
        public int Day;
        public int Policy;
        public bool Decisive;                            // the motion went the way the player's vote took it
    }

    /// <summary>A piece for the City Ledger about the player's run or office: a key and the two names that fill it.</summary>
    public sealed class OfficeStory
    {
        public int Day;                                  // the morning it is printed
        public string Key;
        public string Name, Value;
    }

    /// <summary>The player's own part in the city's politics, game-free: a run, its result, an office held and the city's budget, saved as one line in modData.</summary>
    public sealed class OfficeState
    {
        public const int Version = 1;
        public const int Rivals = 2;                     // the player's opponents in the district's council race
        public const int AgendaMost = 4, SecretsMost = 8, StoriesMost = 8;

        public OfficeRace Race;
        public bool Filed;                               // the papers are in: the player is a candidate
        public int ForDay;                               // the election day the run is for
        public string District;                          // the district of the player's home, for a council seat (the game's neighbourhood id)
        public readonly Candidate Me = new Candidate { Player = true };
        public int Signatures;
        public bool Sheets;                              // the businesses' signature sheets have been used for this run
        public bool Deposit;                             // the deposit was paid
        public bool Incumbent;                           // standing again for the office the player holds

        public int Fund, Raised, Spent, Own;
        public int Dirty;                                // money taken against the law, not yet come out
        public int DirtyDay;                             // the day it was last taken (it can come out for a while after the election)
        public int Case;                                 // the court case it brought, if it came out
        public int OfferKind = -1, OfferAmount, OfferFirst, OfferLast, OfferDay;

        public int PostersHeld, PostersUp, Doors, Rallies, Ads, CanvassDay, LastRally, LastDoorDay;
        public float Push;                               // support of the player's own beyond any district (adverts, rallies, the debate)
        public readonly float[] DistrictPush = new float[CivicCatalogue.MaxDistricts];
        public readonly int[] DistrictDoors = new int[CivicCatalogue.MaxDistricts];
        public int Endorsed, Asked;                      // a bit for each body that has endorsed the player, and each asked
        public readonly int[] Backs = new int[OfficeRules.Endorsers];   // whom each body backs: -1 nobody yet; in the race's own numbering
        public int AttackKind = -1, AttackBy, AttackDay, Attacks;
        public bool AttackTrue;
        public int Debate;                               // 0 still to come, 1 the player took part, 2 they were not there
        public float DebateGain;

        // The district's council race; the mayor's race is the city's own (GovState).
        public int RivalCount;
        public readonly Candidate[] Rival = { new Candidate(), new Candidate() };
        public readonly float[] RacePolls = new float[Rivals + 1];   // the player first
        public readonly float[] RivalPush = new float[Rivals];
        public readonly int[] RivalSpend = new int[Rivals];
        public readonly int[] RaceVotes = new int[Rivals + 1];
        public int RaceTurnout;

        public int Result;                               // 0 none, 1 won, 2 lost
        public OfficeRace ResultRace;
        public int ResultShare;                          // per mille of the vote
        public int ResultDay;
        public bool ResultShown;                         // result night has been seen

        public OfficeRace Holds;
        public int Seat = -1;                            // the council seat
        public int TermStart;
        public float Approval = 50f;                     // a councillor's (a mayor's is the city's own)
        public int LowDays;
        public int PayDay;
        public bool Registered;
        public int RegisterBy;
        public int NextMeeting;
        public readonly List<OfficeMotion> Agenda = new List<OfficeMotion>(AgendaMost);
        public int Tabled;                               // the day the player last tabled a motion
        public int Missed, Attended;
        public float VotesRecord;                        // how the player's votes sit with the district
        public int Scandals;
        public int OrderDay;
        public readonly List<OfficeSecret> Secrets = new List<OfficeSecret>(SecretsMost);
        public int RecallDay;                            // a councillor's recall vote; 0 for none
        public float RecallPush;

        public readonly int[] Budget = new int[4];
        public long Reserve;
        public bool ReserveSet;
        public int BudgetDay;                            // when the next budget is due
        public int WeekDay;                              // when the budget's next week is reckoned

        public readonly List<OfficeStory> Stories = new List<OfficeStory>(StoriesMost);
        public int Runs, Wins, Terms;
        public readonly Candidate Held = new Candidate { Player = true };   // the platform and party the office was won on

        public bool Running => Race != OfficeRace.None;

        public bool InOffice => Holds != OfficeRace.None;

        public OfficeState()
        {
            Reset();
        }

        public void Reset()
        {
            ClearRun();
            Result = 0;
            ResultRace = OfficeRace.None;
            ResultShare = 0;
            ResultDay = 0;
            ResultShown = true;
            Holds = OfficeRace.None;
            Seat = -1;
            TermStart = 0;
            Approval = 50f;
            LowDays = 0;
            PayDay = 0;
            Registered = false;
            RegisterBy = 0;
            NextMeeting = 0;
            Agenda.Clear();
            Tabled = 0;
            Missed = Attended = 0;
            VotesRecord = 0f;
            Scandals = 0;
            OrderDay = 0;
            Secrets.Clear();
            RecallDay = 0;
            RecallPush = 0f;
            Array.Clear(Budget, 0, Budget.Length);
            Reserve = 0;
            ReserveSet = false;
            BudgetDay = 0;
            WeekDay = 0;
            Stories.Clear();
            Runs = Wins = Terms = 0;
            Held.CopyFrom(new Candidate());
            Held.Player = true;
            Dirty = 0;
            DirtyDay = 0;
            Case = 0;
            Fund = 0;
        }

        /// <summary>A run is over (or never began): everything of it but the fund, money still to come out and the record.</summary>
        public void ClearRun()
        {
            Race = OfficeRace.None;
            Filed = false;
            ForDay = 0;
            District = null;
            Me.CopyFrom(new Candidate());
            Me.Player = true;
            Signatures = 0;
            Sheets = false;
            Deposit = false;
            Incumbent = false;
            Raised = Spent = Own = 0;
            OfferKind = -1;
            OfferAmount = OfferFirst = OfferLast = OfferDay = 0;
            PostersHeld = PostersUp = Doors = Rallies = Ads = CanvassDay = LastRally = LastDoorDay = 0;
            Push = 0f;
            Array.Clear(DistrictPush, 0, DistrictPush.Length);
            Array.Clear(DistrictDoors, 0, DistrictDoors.Length);
            Endorsed = Asked = 0;
            for (int i = 0; i < Backs.Length; i++)
            {
                Backs[i] = -1;
            }
            AttackKind = -1;
            AttackBy = AttackDay = Attacks = 0;
            AttackTrue = false;
            Debate = 0;
            DebateGain = 0f;
            RivalCount = 0;
            for (int i = 0; i < Rivals; i++)
            {
                Rival[i].CopyFrom(new Candidate());
                RivalPush[i] = 0f;
                RivalSpend[i] = 0;
            }
            Array.Clear(RacePolls, 0, RacePolls.Length);
            Array.Clear(RaceVotes, 0, RaceVotes.Length);
            RaceTurnout = 0;
        }

        /// <summary>A story for a morning's paper; the oldest goes when there are too many.</summary>
        public void Story(int day, string key, string name = null, string value = null)
        {
            while (Stories.Count >= StoriesMost)
            {
                Stories.RemoveAt(0);
            }
            Stories.Add(new OfficeStory { Day = day, Key = key, Name = name, Value = value });
        }

        public string Write()
        {
            var w = new CivicWriter();
            w.Int(Version).Int((int)Race).Bool(Filed).Int(ForDay).Text(District);
            GovState.WriteCandidate(w, Me);
            w.Int(Signatures).Bool(Sheets).Bool(Deposit).Bool(Incumbent);
            w.Int(Fund).Int(Raised).Int(Spent).Int(Own).Int(Dirty).Int(DirtyDay).Int(Case);
            w.Int(OfferKind).Int(OfferAmount).Int(OfferFirst).Int(OfferLast).Int(OfferDay);
            w.Int(PostersHeld).Int(PostersUp).Int(Doors).Int(Rallies).Int(Ads).Int(CanvassDay).Int(LastRally).Int(LastDoorDay).Float(Push);
            for (int i = 0; i < DistrictPush.Length; i++)
            {
                w.Float(DistrictPush[i]).Int(DistrictDoors[i]);
            }
            w.Int(Endorsed).Int(Asked);
            for (int i = 0; i < Backs.Length; i++)
            {
                w.Int(Backs[i]);
            }
            w.Int(AttackKind).Int(AttackBy).Int(AttackDay).Int(Attacks).Bool(AttackTrue).Int(Debate).Float(DebateGain);
            w.Int(RivalCount);
            for (int i = 0; i < Rivals; i++)
            {
                GovState.WriteCandidate(w, Rival[i]);
                w.Float(RivalPush[i]).Int(RivalSpend[i]);
            }
            for (int i = 0; i <= Rivals; i++)
            {
                w.Float(RacePolls[i]).Int(RaceVotes[i]);
            }
            w.Int(RaceTurnout);
            w.Int(Result).Int((int)ResultRace).Int(ResultShare).Int(ResultDay).Bool(ResultShown);
            w.Int((int)Holds).Int(Seat).Int(TermStart).Float(Approval).Int(LowDays).Int(PayDay).Bool(Registered).Int(RegisterBy).Int(NextMeeting);
            w.Int(Agenda.Count);
            for (int i = 0; i < Agenda.Count; i++)
            {
                OfficeMotion m = Agenda[i];
                w.Int(m.Kind).Int(m.Policy).Int(m.By).Int(m.Day).Bool(m.Undeclared);
                for (int d = 0; d < m.Budget.Length; d++)
                {
                    w.Int(m.Budget[d]);
                }
            }
            w.Int(Tabled).Int(Missed).Int(Attended).Float(VotesRecord).Int(Scandals).Int(OrderDay);
            w.Int(Secrets.Count);
            for (int i = 0; i < Secrets.Count; i++)
            {
                w.Int(Secrets[i].Day).Int(Secrets[i].Policy).Bool(Secrets[i].Decisive);
            }
            w.Int(RecallDay).Float(RecallPush);
            for (int d = 0; d < Budget.Length; d++)
            {
                w.Int(Budget[d]);
            }
            w.Long(Reserve).Bool(ReserveSet).Int(BudgetDay).Int(WeekDay);
            w.Int(Stories.Count);
            for (int i = 0; i < Stories.Count; i++)
            {
                w.Int(Stories[i].Day).Text(Stories[i].Key).Text(Stories[i].Name).Text(Stories[i].Value);
            }
            w.Int(Runs).Int(Wins).Int(Terms);
            GovState.WriteCandidate(w, Held);
            return w.ToString();
        }

        /// <summary>False (and the caller starts afresh) for a line that is not a state of this version.</summary>
        public bool Read(string text)
        {
            try
            {
                var r = new CivicReader(text);
                if (r.Int() != Version)
                {
                    return false;
                }
                Reset();
                Race = (OfficeRace)OfficeRules.Clamp(r.Int(), 0, 2);
                Filed = r.Bool();
                ForDay = r.Int();
                District = r.Text();
                GovState.ReadCandidate(r, Me);
                Me.Player = true;
                Signatures = Math.Max(0, r.Int());
                Sheets = r.Bool();
                Deposit = r.Bool();
                Incumbent = r.Bool();
                Fund = Math.Max(0, r.Int());
                Raised = Math.Max(0, r.Int());
                Spent = Math.Max(0, r.Int());
                Own = Math.Max(0, r.Int());
                Dirty = Math.Max(0, r.Int());
                DirtyDay = r.Int();
                Case = r.Int();
                OfferKind = OfficeRules.Clamp(r.Int(), -1, OfficeRules.OfferStraw);
                OfferAmount = Math.Max(0, r.Int());
                OfferFirst = Wrap(r.Int(), CivicCatalogue.FirstNames.Length);
                OfferLast = Wrap(r.Int(), CivicCatalogue.LastNames.Length);
                OfferDay = r.Int();
                PostersHeld = Math.Max(0, r.Int());
                PostersUp = Math.Max(0, r.Int());
                Doors = Math.Max(0, r.Int());
                Rallies = Math.Max(0, r.Int());
                Ads = Math.Max(0, r.Int());
                CanvassDay = r.Int();
                LastRally = r.Int();
                LastDoorDay = r.Int();
                Push = OfficeRules.Clamp(r.Float(), -1f, 1f);
                for (int i = 0; i < DistrictPush.Length; i++)
                {
                    DistrictPush[i] = OfficeRules.Clamp(r.Float(), -1f, 1f);
                    DistrictDoors[i] = Math.Max(0, r.Int());
                }
                Endorsed = r.Int();
                Asked = r.Int();
                for (int i = 0; i < Backs.Length; i++)
                {
                    Backs[i] = OfficeRules.Clamp(r.Int(), -1, CivicCatalogue.MaxCandidates - 1);
                }
                AttackKind = OfficeRules.Clamp(r.Int(), -1, AttackKindLast);
                AttackBy = r.Int();
                AttackDay = r.Int();
                Attacks = Math.Max(0, r.Int());
                AttackTrue = r.Bool();
                Debate = OfficeRules.Clamp(r.Int(), 0, 2);
                DebateGain = OfficeRules.Clamp(r.Float(), -1f, 1f);
                RivalCount = OfficeRules.Clamp(r.Int(), 0, Rivals);
                for (int i = 0; i < Rivals; i++)
                {
                    GovState.ReadCandidate(r, Rival[i]);
                    Rival[i].Player = false;
                    RivalPush[i] = OfficeRules.Clamp(r.Float(), -1f, 1f);
                    RivalSpend[i] = Math.Max(0, r.Int());
                }
                for (int i = 0; i <= Rivals; i++)
                {
                    RacePolls[i] = OfficeRules.Clamp01(r.Float());
                    RaceVotes[i] = Math.Max(0, r.Int());
                }
                RaceTurnout = Math.Max(0, r.Int());
                Result = OfficeRules.Clamp(r.Int(), 0, 2);
                ResultRace = (OfficeRace)OfficeRules.Clamp(r.Int(), 0, 2);
                ResultShare = OfficeRules.Clamp(r.Int(), 0, 1000);
                ResultDay = r.Int();
                ResultShown = r.Bool();
                Holds = (OfficeRace)OfficeRules.Clamp(r.Int(), 0, 2);
                Seat = OfficeRules.Clamp(r.Int(), -1, CivicCatalogue.CouncilSeats - 1);
                TermStart = r.Int();
                Approval = OfficeRules.Clamp(r.Float(), 0f, 100f);
                LowDays = Math.Max(0, r.Int());
                PayDay = r.Int();
                Registered = r.Bool();
                RegisterBy = r.Int();
                NextMeeting = r.Int();
                int motions = Math.Min(r.Int(), AgendaMost);
                for (int i = 0; i < motions; i++)
                {
                    var m = new OfficeMotion { Kind = OfficeRules.Clamp(r.Int(), 0, 1), Policy = r.Int(), By = OfficeRules.Clamp(r.Int(), 0, 3), Day = r.Int(), Undeclared = r.Bool() };
                    for (int d = 0; d < m.Budget.Length; d++)
                    {
                        m.Budget[d] = OfficeRules.Clamp(r.Int(), -OfficeRules.DepartmentMost, OfficeRules.DepartmentMost);
                    }
                    if (m.Kind == OfficeMotion.KindBudget || CivicCatalogue.At(m.Policy) != null)
                    {
                        Agenda.Add(m);
                    }
                }
                Tabled = r.Int();
                Missed = Math.Max(0, r.Int());
                Attended = Math.Max(0, r.Int());
                VotesRecord = OfficeRules.Clamp(r.Float(), -20f, 20f);
                Scandals = Math.Max(0, r.Int());
                OrderDay = r.Int();
                int secrets = Math.Min(r.Int(), SecretsMost);
                for (int i = 0; i < secrets; i++)
                {
                    var s = new OfficeSecret { Day = r.Int(), Policy = r.Int(), Decisive = r.Bool() };
                    if (CivicCatalogue.At(s.Policy) != null)
                    {
                        Secrets.Add(s);
                    }
                }
                RecallDay = r.Int();
                RecallPush = OfficeRules.Clamp(r.Float(), 0f, 1f);
                for (int d = 0; d < Budget.Length; d++)
                {
                    Budget[d] = OfficeRules.Clamp(r.Int(), -OfficeRules.DepartmentMost, OfficeRules.DepartmentMost);
                }
                Reserve = r.Long();
                ReserveSet = r.Bool();
                BudgetDay = r.Int();
                WeekDay = r.Int();
                int stories = Math.Min(r.Int(), StoriesMost);
                for (int i = 0; i < stories; i++)
                {
                    var s = new OfficeStory { Day = r.Int(), Key = r.Text(), Name = r.Text(), Value = r.Text() };
                    if (s.Key != null)
                    {
                        Stories.Add(s);
                    }
                }
                Runs = Math.Max(0, r.Int());
                Wins = Math.Max(0, r.Int());
                Terms = Math.Max(0, r.Int());
                GovState.ReadCandidate(r, Held);
                Held.Player = true;
                if (Holds != OfficeRace.Council)
                {
                    Seat = -1;
                }
                if (Race == OfficeRace.None)
                {
                    Filed = false;
                }
                return !r.Failed;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private const int AttackKindLast = (int)RealLifeOverhaul.AttackKind.Promises;

        private static int Wrap(int value, int count)
        {
            return count <= 0 ? 0 : (value % count + count) % count;
        }
    }
}
