using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>What a guest's government state learnt from the host's, for what the guest is to be told and given.</summary>
    public struct GovChange
    {
        public bool NewElection;     // another election than the guest knew is under way (its own part in the old one is over)
        public bool Counted;         // the election the guest took part in has been counted since the last look
        public int LeversMoved;      // a bit for each lever that now stands elsewhere
        public bool Phase;           // the election's phase (or which election it is) changed
    }

    /// <summary>
    /// The city's government and building sites in a Going Public session (Core/SharedCity, parts G and W): what of
    /// <see cref="GovState"/> and <see cref="WorksState"/> is the city's, and how a guest's state takes the host's.
    /// Game-free (RealLifeOverhaul.Build/EconomyCheck).
    ///
    /// The city's government: the mayor and the council, every lever and what is on its way, the election (its
    /// calendar, phase, candidates, polls, the momentum every player's doings have given each candidate, the count),
    /// approval and how long it has been low, the heat of crime and accidents, the petition (what it asks, how many have
    /// signed, by when) and the council's calendar. The player's own: what they gave each candidate, whom they endorsed,
    /// canvassed for and saw at a rally, their ballot, the scandal of their own record, the favours they are owed, whether
    /// the petition is theirs and whether they have signed it, and the weekly account of taxes and fines. The votes of
    /// the other players are the city's too (<see cref="GovState.OthersVotes"/>): a guest's ballot is counted in the
    /// host's election.
    ///
    /// The city's sites: every site of the programme, with its progress, complaints and stops. The player's own: the refit
    /// of their own shopfront (a guest's runs on its own machine, with an id from <see cref="OwnSites"/> up so that it
    /// never meets one of the host's), when their shopfront was last refitted, and which sites are next door to them.
    /// </summary>
    public static class CivicShare
    {
        public const int Version = 1;
        public const int OwnSites = 1000000;   // a guest's own sites while it follows the host's programme have ids from here

        // ---------------------------------------------------------------- the government

        /// <summary>The city's part of the government, as a line. The host's own ballot is written among the other players' votes.</summary>
        public static string WriteGov(GovState s)
        {
            var w = new CivicWriter();
            w.Int(Version).Int(s.Seed).Int(s.FirstDay).Long(s.LastHour);
            Candidate(w, s.Mayor);
            w.Int(s.TermStart).Float(s.Approval).Int(s.LowDays);
            for (int i = 0; i < s.Council.Length; i++) w.Int(s.Council[i]);
            for (int i = 0; i < s.Levers.Length; i++) w.Int(s.Levers[i]);
            w.Int(s.Pending.Count);
            for (int i = 0; i < s.Pending.Count; i++) w.Int(s.Pending[i].Lever).Int(s.Pending[i].Position).Int(s.Pending[i].Day);
            w.Int(s.Number).Int(s.ElectionDay).Int((int)s.Phase).Bool(s.Recall).Int(s.CandidateCount);
            for (int i = 0; i < s.Candidates.Length; i++)
            {
                Candidate(w, s.Candidates[i]);
                w.Float(s.Polls[i]).Float(s.Momentum[i]).Float(s.Final[i]).Int(s.OthersVotes[i] + (s.Voted == i ? 1 : 0));
            }
            w.Bool(s.DebateHeld).Int(s.Winner).Int(s.Turnout).Float(s.CrimeHeat).Float(s.CrashHeat);
            w.Int(s.PetitionPolicy).Int(s.PetitionSigned).Int(s.PetitionNeeded).Int(s.PetitionEnds).Int(s.PetitionRest).Int(s.NextCouncilVote);
            return w.ToString();
        }

        /// <summary>
        /// The host's line applied to a guest's state: the city's part is the host's, the player's own is kept (and what
        /// the player did in an election that is over is put away). False, and nothing changed, for a line that cannot be read.
        /// </summary>
        public static bool ApplyGov(GovState mine, string text, out GovChange change)
        {
            change = default(GovChange);
            var host = new GovState();
            var votes = new int[host.Candidates.Length];
            if (mine == null || !ReadGov(host, votes, text))
            {
                return false;
            }
            change.NewElection = host.Number != mine.Number;
            change.Counted = host.Phase == ElectionPhase.Counted && host.Number == mine.Number && mine.Phase == ElectionPhase.Campaign;
            change.Phase = host.Phase != mine.Phase || host.Number != mine.Number;
            for (int i = 0; i < mine.Levers.Length && i < 31; i++)
            {
                if (mine.Levers[i] != host.Levers[i])
                {
                    change.LeversMoved |= 1 << i;
                }
            }
            if (change.NewElection)
            {
                // Another election: the player's part in the last one is over, as when a campaign begins alone.
                mine.Endorsed = -1;
                mine.Volunteered = -1;
                mine.Rallies = 0;
                mine.Voted = -1;
                mine.Scandal = false;
                Array.Clear(mine.Donated, 0, mine.Donated.Length);
            }
            if (host.PetitionPolicy != mine.PetitionPolicy || host.PetitionEnds != mine.PetitionEnds)
            {
                mine.PetitionMine = false;     // another petition than the one the player started or signed
                mine.PetitionISigned = false;
            }

            mine.Seed = host.Seed;
            mine.FirstDay = host.FirstDay;
            mine.LastHour = host.LastHour;
            mine.Mayor.CopyFrom(host.Mayor);
            mine.TermStart = host.TermStart;
            mine.Approval = host.Approval;
            mine.LowDays = host.LowDays;
            Array.Copy(host.Council, mine.Council, mine.Council.Length);
            Array.Copy(host.Levers, mine.Levers, mine.Levers.Length);
            mine.Pending.Clear();
            mine.Pending.AddRange(host.Pending);
            mine.Number = host.Number;
            mine.ElectionDay = host.ElectionDay;
            mine.Phase = host.Phase;
            mine.Recall = host.Recall;
            mine.CandidateCount = host.CandidateCount;
            for (int i = 0; i < mine.Candidates.Length; i++)
            {
                mine.Candidates[i].CopyFrom(host.Candidates[i]);
                mine.Polls[i] = host.Polls[i];
                mine.Momentum[i] = host.Momentum[i];
                mine.Final[i] = host.Final[i];
                mine.OthersVotes[i] = Math.Max(0, votes[i] - (mine.Voted == i ? 1 : 0));   // (every player's ballot but this one's)
            }
            mine.DebateHeld = host.DebateHeld;
            mine.Winner = host.Winner;
            mine.Turnout = host.Turnout;
            mine.CrimeHeat = host.CrimeHeat;
            mine.CrashHeat = host.CrashHeat;
            mine.PetitionPolicy = host.PetitionPolicy;
            mine.PetitionSigned = host.PetitionSigned;
            mine.PetitionNeeded = host.PetitionNeeded;
            mine.PetitionEnds = host.PetitionEnds;
            mine.PetitionRest = host.PetitionRest;
            mine.NextCouncilVote = host.NextCouncilVote;
            return true;
        }

        private static bool ReadGov(GovState s, int[] votes, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            try
            {
                var r = new CivicReader(text);
                if (r.Int() != Version)
                {
                    return false;
                }
                s.Seed = r.Int();
                s.FirstDay = r.Int();
                s.LastHour = r.Long();
                GovState.ReadCandidate(r, s.Mayor);
                s.TermStart = r.Int();
                s.Approval = Math.Max(0f, Math.Min(100f, r.Float()));
                s.LowDays = r.Int();
                for (int i = 0; i < s.Council.Length; i++) s.Council[i] = r.Int();
                for (int i = 0; i < s.Levers.Length; i++) s.Levers[i] = Math.Sign(r.Int());
                int pending = r.Int();
                if (pending < 0 || pending > 32)
                {
                    return false;
                }
                for (int i = 0; i < pending; i++)
                {
                    var change = new PendingChange { Lever = r.Int(), Position = Math.Sign(r.Int()), Day = r.Int() };
                    if (change.Lever >= 0 && change.Lever < CivicCatalogue.Levers)
                    {
                        s.Pending.Add(change);
                    }
                }
                s.Number = r.Int();
                s.ElectionDay = r.Int();
                s.Phase = (ElectionPhase)Math.Max(0, Math.Min(2, r.Int()));
                s.Recall = r.Bool();
                s.CandidateCount = Math.Max(0, Math.Min(CivicCatalogue.MaxCandidates, r.Int()));
                for (int i = 0; i < s.Candidates.Length; i++)
                {
                    GovState.ReadCandidate(r, s.Candidates[i]);
                    s.Polls[i] = r.Float();
                    s.Momentum[i] = r.Float();
                    s.Final[i] = r.Float();
                    votes[i] = Math.Max(0, r.Int());
                }
                s.DebateHeld = r.Bool();
                s.Winner = r.Int();
                s.Turnout = r.Int();
                s.CrimeHeat = r.Float();
                s.CrashHeat = r.Float();
                s.PetitionPolicy = r.Int();
                s.PetitionSigned = r.Int();
                s.PetitionNeeded = r.Int();
                s.PetitionEnds = r.Int();
                s.PetitionRest = r.Int();
                s.NextCouncilVote = r.Int();
                if (s.PetitionPolicy >= CivicCatalogue.All.Length)
                {
                    s.PetitionPolicy = -1;
                }
                if (s.Winner >= s.CandidateCount)
                {
                    s.Winner = -1;
                }
                return !r.Failed && s.Seed != 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>True when two states have the same city government (the players' own parts not looked at).</summary>
        public static bool SameGov(GovState a, GovState b)
        {
            return WriteCityOnly(a) == WriteCityOnly(b);
        }

        // The city's line with every player's ballot left out (the host's line counts its own among them).
        private static string WriteCityOnly(GovState s)
        {
            int voted = s.Voted;
            var others = (int[])s.OthersVotes.Clone();
            s.Voted = -1;
            Array.Clear(s.OthersVotes, 0, s.OthersVotes.Length);
            try
            {
                return WriteGov(s);
            }
            finally
            {
                s.Voted = voted;
                Array.Copy(others, s.OthersVotes, others.Length);
            }
        }

        private static void Candidate(CivicWriter w, Candidate c)
        {
            w.Int(c.First).Int(c.Last).Int(c.Party).Int(c.Slogan).Int(c.Look).Int(c.Lead).Bool(c.Incumbent).Int(c.Planks);
            for (int i = 0; i < c.Platform.Length; i++) w.Int(c.Platform[i]);
        }

        // ---------------------------------------------------------------- the building sites

        /// <summary>The city's programme, as a line: every site, none of them anybody's own.</summary>
        public static string WriteWorks(WorksState s)
        {
            var w = new CivicWriter();
            w.Int(Version).Int(s.NextId).Int(s.Completed).Int(s.Sites.Count);
            for (int i = 0; i < s.Sites.Count; i++)
            {
                SiteState site = s.Sites[i];
                bool mine = site.Mine;
                site.Mine = false;   // (the host's own refit is a site of the city's to everybody else)
                site.Write(w);
                site.Mine = mine;
            }
            return w.ToString();
        }

        /// <summary>The host's programme applied to a guest's: the host's sites, and the guest's own refit kept. False, and nothing changed, for a line that cannot be read.</summary>
        public static bool ApplyWorks(WorksState mine, string text)
        {
            if (mine == null || string.IsNullOrEmpty(text))
            {
                return false;
            }
            var sites = new List<SiteState>(8);
            int nextId, completed;
            try
            {
                var r = new CivicReader(text);
                if (r.Int() != Version)
                {
                    return false;
                }
                nextId = Math.Max(1, r.Int());
                completed = r.Int();
                int count = r.Int();
                if (count < 0 || count > 16)
                {
                    return false;
                }
                for (int i = 0; i < count; i++)
                {
                    var site = new SiteState();
                    site.Read(r);
                    site.Mine = false;
                    sites.Add(site);
                }
                if (r.Failed)
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
            int own = OwnSites;
            for (int i = 0; i < mine.Sites.Count; i++)
            {
                own = Math.Max(own, mine.Sites[i].Id + 1);
            }
            // The guest's own refit keeps an id the host's programme never gives; a site of the host's that the guest already has
            // is the same object, changed in place (what stands in the street for it stays up); the rest go, or come.
            for (int i = mine.Sites.Count - 1; i >= 0; i--)
            {
                SiteState site = mine.Sites[i];
                if (site.Mine)
                {
                    if (site.Id < OwnSites)
                    {
                        site.Id = own++;
                    }
                    continue;
                }
                SiteState host = null;
                for (int j = 0; j < sites.Count && host == null; j++)
                {
                    host = sites[j].Id == site.Id && sites[j].Street == site.Street && sites[j].Number == site.Number ? sites[j] : null;
                }
                if (host == null)
                {
                    mine.Sites.RemoveAt(i);
                    continue;
                }
                Copy(host, site);
                sites.Remove(host);
            }
            for (int i = 0; i < sites.Count; i++)
            {
                SiteState host = sites[i];
                // Two refits at one shopfront cannot be: the guest's own stays, the host's of it is left out.
                bool clash = false;
                for (int j = 0; j < mine.Sites.Count && !clash; j++)
                {
                    clash = mine.Sites[j].Mine && mine.Sites[j].Street == host.Street && mine.Sites[j].Number == host.Number;
                }
                if (!clash && host.Id < OwnSites)
                {
                    mine.Sites.Add(host);
                }
            }
            mine.NextId = nextId;
            mine.Completed = Math.Max(mine.Completed, completed);
            return true;
        }

        // Every figure of a site that a line carries, from one to the other (whose it is, and what is near it, stay).
        private static void Copy(SiteState from, SiteState to)
        {
            to.Kind = from.Kind;
            to.X = from.X;
            to.Y = from.Y;
            to.Z = from.Z;
            to.Project = from.Project;
            to.Contractor = from.Contractor;
            to.StartDay = from.StartDay;
            to.Needed = from.Needed;
            to.Done = from.Done;
            to.RainHours = from.RainHours;
            to.QuietUntil = from.QuietUntil;
            to.HaltUntil = from.HaltUntil;
            to.Complaints = from.Complaints;
            to.Reported = from.Reported;
            to.UnsafeFrom = from.UnsafeFrom;
            to.Local = from.Local;
            to.Favoured = from.Favoured;
        }

        /// <summary>The id of a site that is the guest's own while it follows the host's programme.</summary>
        public static int NextOwnId(WorksState s)
        {
            int own = OwnSites;
            for (int i = 0; i < s.Sites.Count; i++)
            {
                own = Math.Max(own, s.Sites[i].Id + 1);
            }
            return own;
        }
    }
}
