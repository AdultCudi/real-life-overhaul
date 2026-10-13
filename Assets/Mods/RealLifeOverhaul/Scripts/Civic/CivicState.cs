using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>Somebody standing for mayor (or the mayor). Made from the pools in the catalogue; all of it is indices.</summary>
    public sealed class Candidate
    {
        public int First, Last, Party, Slogan;
        public int Look;                 // a seed for how they look, should a character ever be dressed as them
        public int Lead;                 // the issue they lead on (an Issue)
        public bool Incumbent;
        public bool Player;              // the player themselves (Civic/CivicOffice): named by the game, never saved in the candidate's own fields
        public readonly int[] Platform = new int[4];   // indices into CivicCatalogue.All; -1 for none
        public int Planks;

        public Candidate()
        {
            for (int i = 0; i < Platform.Length; i++)
            {
                Platform[i] = -1;
            }
        }

        public void CopyFrom(Candidate other)
        {
            First = other.First;
            Last = other.Last;
            Party = other.Party;
            Slogan = other.Slogan;
            Look = other.Look;
            Lead = other.Lead;
            Incumbent = other.Incumbent;
            Player = other.Player;
            Planks = other.Planks;
            for (int i = 0; i < Platform.Length; i++)
            {
                Platform[i] = other.Platform[i];
            }
        }

        /// <summary>How much of their platform speaks to an issue: the number of planks that do.</summary>
        public float Stance(int issue)
        {
            float total = 0f;
            for (int i = 0; i < Planks; i++)
            {
                PolicyDef policy = CivicCatalogue.At(Platform[i]);
                if (policy != null && (int)policy.Issue == issue)
                {
                    total += 1f;
                }
            }
            return total + (Lead == issue ? 0.5f : 0f);
        }
    }

    public enum ElectionPhase { None, Campaign, Counted }

    /// <summary>A change to one lever that has been decided and comes in on a day.</summary>
    public struct PendingChange
    {
        public int Lever, Position, Day;
    }

    /// <summary>
    /// The city's government in one save: mayor, council, where every lever stands, the election
    /// in progress, what the player has done in it, approval, the petition and the weekly account.
    /// All of it is data; nothing here touches the game. Kept as one line of text in modData.
    /// </summary>
    public sealed class GovState
    {
        public const int Version = 1;

        public int Seed;
        public int FirstDay;                      // the first day the mod saw this save
        public long LastHour;                     // the last absolute hour that was processed

        public readonly Candidate Mayor = new Candidate();
        public int TermStart;
        public float Approval = 55f;
        public int LowDays;                       // days running with approval under the recall line
        public readonly int[] Council = new int[CivicCatalogue.CouncilSeats];   // each seat's party
        public readonly int[] Levers = new int[CivicCatalogue.Levers];          // -1, 0, 1: as applied now
        public readonly List<PendingChange> Pending = new List<PendingChange>(8);

        // ---- the election
        public int Number;                        // elections held so far
        public int ElectionDay;                   // the next (or current) election day
        public ElectionPhase Phase;
        public bool Recall;                       // this election was forced by a recall
        public int CandidateCount;
        public readonly Candidate[] Candidates = { new Candidate(), new Candidate(), new Candidate(), new Candidate() };
        public readonly float[] Polls = new float[CivicCatalogue.MaxCandidates];      // shares, summing to 1
        public readonly float[] Momentum = new float[CivicCatalogue.MaxCandidates];   // what the player's own actions added
        public readonly int[] Donated = new int[CivicCatalogue.MaxCandidates];
        public int Endorsed = -1;
        public int Volunteered = -1;
        public int Rallies;                       // bit per candidate whose rally the player attended
        public int Voted = -1;
        public bool DebateHeld;
        public bool Scandal;                      // the player's record was used against the candidate they endorsed
        public int Winner = -1;                   // of the election last counted
        public readonly float[] Final = new float[CivicCatalogue.MaxCandidates];      // its city-wide shares
        public int Turnout;                       // votes cast in it
        public readonly int[] OthersVotes = new int[CivicCatalogue.MaxCandidates];   // the other players' ballots in a Going Public session (Civic/CivicShare)
        public int VotesCast;                     // elections the player has voted in, ever (for Permadeath's obituary)
        public int PlayerSlot = -1;               // the candidate who is the player in this election (Civic/CivicOffice); -1 for none
        public bool MayorIsPlayer;                // the mayor in office is the player
        public readonly float[] DistrictShares = new float[CivicCatalogue.MaxDistricts * CivicCatalogue.MaxCandidates];   // the count's shares in each district, by district then candidate
        public int DistrictsCounted;              // districts the count above has

        // ---- between elections
        public int Favours;                       // faster permits owed to the player for donating to the winner
        public float CrimeHeat;                   // recent crime near the player, decaying a day at a time
        public float CrashHeat;                   // recent traffic accidents, likewise
        public int PetitionPolicy = -1;           // an index into CivicCatalogue.All, or -1 for none
        public int PetitionSigned;
        public int PetitionNeeded;
        public int PetitionEnds;
        public bool PetitionMine;                 // the player started it
        public bool PetitionISigned;
        public int PetitionRest;                  // no new petition before this day
        public int NextCouncilVote;

        // ---- the weekly account (what has run up since the last settlement)
        public int SettleDay;
        public float OwedTax, OwedTrade;          // from each day's sales
        public int SeenDay;                       // the day the three sums below are of
        public float SeenFines, SeenRides;        // what today's fines and subway rides came to when last looked at
        public int SeenTickets = -1;              // the game's count of parking tickets when last looked at

        /// <summary>Everything as in a save the mod has never seen (a text that would not read may have left some of it half set).</summary>
        public void Reset()
        {
            Seed = 0;
            FirstDay = 0;
            LastHour = 0;
            TermStart = 0;
            Approval = 55f;
            LowDays = 0;
            Array.Clear(Council, 0, Council.Length);
            Array.Clear(Levers, 0, Levers.Length);
            Pending.Clear();
            Number = 0;
            ElectionDay = 0;
            Phase = ElectionPhase.None;
            Recall = false;
            CandidateCount = 0;
            Array.Clear(Polls, 0, Polls.Length);
            Array.Clear(Momentum, 0, Momentum.Length);
            Array.Clear(Donated, 0, Donated.Length);
            Array.Clear(Final, 0, Final.Length);
            Array.Clear(OthersVotes, 0, OthersVotes.Length);
            VotesCast = 0;
            PlayerSlot = -1;
            MayorIsPlayer = false;
            Array.Clear(DistrictShares, 0, DistrictShares.Length);
            DistrictsCounted = 0;
            Endorsed = -1;
            Volunteered = -1;
            Rallies = 0;
            Voted = -1;
            DebateHeld = false;
            Scandal = false;
            Winner = -1;
            Turnout = 0;
            Favours = 0;
            CrimeHeat = 0f;
            CrashHeat = 0f;
            PetitionPolicy = -1;
            PetitionSigned = 0;
            PetitionNeeded = 0;
            PetitionEnds = 0;
            PetitionMine = false;
            PetitionISigned = false;
            PetitionRest = 0;
            NextCouncilVote = 0;
            SettleDay = 0;
            OwedTax = 0f;
            OwedTrade = 0f;
            SeenDay = 0;
            SeenFines = 0f;
            SeenRides = 0f;
            SeenTickets = -1;
        }

        public string Write()
        {
            var w = new CivicWriter();
            w.Int(Version).Int(Seed).Int(FirstDay).Long(LastHour);
            WriteCandidate(w, Mayor);
            w.Int(TermStart).Float(Approval).Int(LowDays);
            for (int i = 0; i < Council.Length; i++) w.Int(Council[i]);
            for (int i = 0; i < Levers.Length; i++) w.Int(Levers[i]);
            w.Int(Pending.Count);
            for (int i = 0; i < Pending.Count; i++) w.Int(Pending[i].Lever).Int(Pending[i].Position).Int(Pending[i].Day);
            w.Int(Number).Int(ElectionDay).Int((int)Phase).Bool(Recall).Int(CandidateCount);
            for (int i = 0; i < Candidates.Length; i++)
            {
                WriteCandidate(w, Candidates[i]);
                w.Float(Polls[i]).Float(Momentum[i]).Int(Donated[i]).Float(Final[i]);
            }
            w.Int(Endorsed).Int(Volunteered).Int(Rallies).Int(Voted).Bool(DebateHeld).Bool(Scandal).Int(Winner).Int(Turnout);
            w.Int(Favours).Float(CrimeHeat).Float(CrashHeat);
            w.Int(PetitionPolicy).Int(PetitionSigned).Int(PetitionNeeded).Int(PetitionEnds).Bool(PetitionMine).Bool(PetitionISigned).Int(PetitionRest).Int(NextCouncilVote);
            w.Int(SettleDay).Float(OwedTax).Float(OwedTrade).Int(SeenDay).Float(SeenFines).Float(SeenRides).Int(SeenTickets);
            for (int i = 0; i < OthersVotes.Length; i++) w.Int(OthersVotes[i]);   // (added with the shared city, at the end: an older line has none)
            w.Int(VotesCast);   // (added with Permadeath, after them: an older line has none, and counts from nought)
            w.Int(PlayerSlot).Bool(MayorIsPlayer).Int(DistrictsCounted);   // (added with running for office, after it: an older line has none)
            for (int i = 0; i < DistrictsCounted * CivicCatalogue.MaxCandidates; i++) w.Float(DistrictShares[i]);
            return w.ToString();
        }

        /// <summary>False (and the state untouched in any way that matters: the caller starts afresh) if the text is not a state of this version.</summary>
        public bool Read(string text)
        {
            try
            {
                var r = new CivicReader(text);
                if (r.Int() != Version)
                {
                    return false;
                }
                Seed = r.Int();
                FirstDay = r.Int();
                LastHour = r.Long();
                ReadCandidate(r, Mayor);
                TermStart = r.Int();
                Approval = Clamp(r.Float(), 0f, 100f);
                LowDays = r.Int();
                for (int i = 0; i < Council.Length; i++) Council[i] = r.Int();
                for (int i = 0; i < Levers.Length; i++) Levers[i] = Math.Sign(r.Int());
                Pending.Clear();
                int pending = Math.Min(r.Int(), 32);
                for (int i = 0; i < pending; i++)
                {
                    var change = new PendingChange { Lever = r.Int(), Position = Math.Sign(r.Int()), Day = r.Int() };
                    if (change.Lever >= 0 && change.Lever < CivicCatalogue.Levers)
                    {
                        Pending.Add(change);
                    }
                }
                Number = r.Int();
                ElectionDay = r.Int();
                Phase = (ElectionPhase)Math.Max(0, Math.Min(2, r.Int()));
                Recall = r.Bool();
                CandidateCount = Math.Max(0, Math.Min(CivicCatalogue.MaxCandidates, r.Int()));
                for (int i = 0; i < Candidates.Length; i++)
                {
                    ReadCandidate(r, Candidates[i]);
                    Polls[i] = r.Float();
                    Momentum[i] = r.Float();
                    Donated[i] = r.Int();
                    Final[i] = r.Float();
                }
                Endorsed = r.Int();
                Volunteered = r.Int();
                Rallies = r.Int();
                Voted = r.Int();
                DebateHeld = r.Bool();
                Scandal = r.Bool();
                Winner = r.Int();
                Turnout = r.Int();
                Favours = r.Int();
                CrimeHeat = r.Float();
                CrashHeat = r.Float();
                PetitionPolicy = r.Int();
                PetitionSigned = r.Int();
                PetitionNeeded = r.Int();
                PetitionEnds = r.Int();
                PetitionMine = r.Bool();
                PetitionISigned = r.Bool();
                PetitionRest = r.Int();
                NextCouncilVote = r.Int();
                SettleDay = r.Int();
                OwedTax = r.Float();
                OwedTrade = r.Float();
                SeenDay = r.Int();
                SeenFines = r.Float();
                SeenRides = r.Float();
                SeenTickets = r.Int();
                Array.Clear(OthersVotes, 0, OthersVotes.Length);
                for (int i = 0; i < OthersVotes.Length && r.More; i++)
                {
                    OthersVotes[i] = Math.Max(0, r.Int());
                }
                VotesCast = r.More ? Math.Max(0, r.Int()) : 0;
                PlayerSlot = -1;
                MayorIsPlayer = false;
                DistrictsCounted = 0;
                Array.Clear(DistrictShares, 0, DistrictShares.Length);
                if (r.More)
                {
                    int slot = r.Int();
                    PlayerSlot = slot >= 0 && slot < CandidateCount ? slot : -1;
                    MayorIsPlayer = r.Bool();
                    DistrictsCounted = Math.Max(0, Math.Min(CivicCatalogue.MaxDistricts, r.Int()));
                    for (int i = 0; i < DistrictsCounted * CivicCatalogue.MaxCandidates; i++) DistrictShares[i] = Clamp(r.Float(), 0f, 1f);
                }
                for (int i = 0; i < Candidates.Length; i++) Candidates[i].Player = i == PlayerSlot;
                Mayor.Player = MayorIsPlayer;
                if (PetitionPolicy >= CivicCatalogue.All.Length)
                {
                    PetitionPolicy = -1;
                }
                return !r.Failed;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void WriteCandidate(CivicWriter w, Candidate c)
        {
            w.Int(c.First).Int(c.Last).Int(c.Party).Int(c.Slogan).Int(c.Look).Int(c.Lead).Bool(c.Incumbent).Int(c.Planks);
            for (int i = 0; i < c.Platform.Length; i++) w.Int(c.Platform[i]);
        }

        internal static void ReadCandidate(CivicReader r, Candidate c)
        {
            c.First = Wrap(r.Int(), CivicCatalogue.FirstNames.Length);
            c.Last = Wrap(r.Int(), CivicCatalogue.LastNames.Length);
            c.Party = Wrap(r.Int(), CivicCatalogue.Parties.Length + 1);   // (the last is the independent: Civic/CivicCatalogue.Independent)
            c.Slogan = Wrap(r.Int(), CivicCatalogue.Slogans.Length);
            c.Look = r.Int();
            c.Lead = Wrap(r.Int(), CivicCatalogue.Issues);
            c.Incumbent = r.Bool();
            c.Planks = Math.Max(0, Math.Min(c.Platform.Length, r.Int()));
            for (int i = 0; i < c.Platform.Length; i++)
            {
                int plank = r.Int();
                c.Platform[i] = plank >= 0 && plank < CivicCatalogue.All.Length ? plank : -1;
            }
        }

        private static int Wrap(int value, int count)
        {
            return count <= 0 ? 0 : (value % count + count) % count;
        }

        private static float Clamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }

    /// <summary>
    /// One construction site, as data. Where it is is the building it was started at (its
    /// address, for the notice) and a point in front of that building's door; what is stood in
    /// the street is worked out from that point when the player is near (SiteDresser), never saved.
    /// </summary>
    public sealed class SiteState
    {
        public int Id;
        public SiteKind Kind;
        public string Street;            // the game's own street id of the building, e.g. ba:street_thirdstreet
        public int Number;
        public float X, Y, Z;            // in front of the building's door
        public int Project, Contractor;  // indices into the catalogue's name pools (within the kind's three, and the eight)
        public int StartDay;
        public float Needed;             // working hours
        public float Done;
        public int RainHours;            // lost to the weather so far
        public int QuietUntil;           // short hours imposed after a complaint: until this day
        public int HaltUntil;            // stopped for an inspection: until this day
        public int Complaints;
        public bool Reported;            // the player has reported it as unsafe
        public float UnsafeFrom = -1f;   // the stretch of its progress (0 to 1) during which it is left unsafe; negative for never
        public bool Mine;                // the refit the player ordered for their own shop
        public bool Local;
        public bool Favoured;            // a permit favour was spent on it
        public bool NearMine;            // within earshot of something the player lives or works in (looked at daily)

        public float Progress => Needed <= 0f ? 1f : Done >= Needed ? 1f : Done / Needed;

        /// <summary>0 to 4.</summary>
        public int Phase
        {
            get
            {
                int phase = (int)(Progress * CivicCatalogue.PhasesPerKind);
                return phase >= CivicCatalogue.PhasesPerKind ? CivicCatalogue.PhasesPerKind - 1 : phase;
            }
        }

        public bool Finished => Done >= Needed;

        /// <summary>True while it is in the stretch in which the contractor has left it unsafe (and nobody has reported it).</summary>
        public bool Unsafe => UnsafeFrom >= 0f && !Reported && Progress >= UnsafeFrom && Progress < UnsafeFrom + 0.3f;

        /// <summary>The phases that make a noise the neighbours hear.</summary>
        public bool Noisy
        {
            get
            {
                int phase = Phase;
                switch (Kind)
                {
                    case SiteKind.Roadworks: return phase == 1 || phase == 2;
                    case SiteKind.Scaffold: return phase == 0 || phase == 2 || phase == 4;
                    case SiteKind.Refit: return phase == 1 || phase == 2;
                    default: return phase == 1;
                }
            }
        }

        public void Write(CivicWriter w)
        {
            w.Int(Id).Int((int)Kind).Text(Street).Int(Number).Float(X).Float(Y).Float(Z).Int(Project).Int(Contractor).Int(StartDay)
                .Float(Needed).Float(Done).Int(RainHours).Int(QuietUntil).Int(HaltUntil).Int(Complaints).Bool(Reported).Float(UnsafeFrom)
                .Bool(Mine).Bool(Local).Bool(Favoured);
        }

        public void Read(CivicReader r)
        {
            Id = r.Int();
            Kind = (SiteKind)Math.Max(0, Math.Min(3, r.Int()));
            Street = r.Text();
            Number = r.Int();
            X = r.Float();
            Y = r.Float();
            Z = r.Float();
            Project = Math.Abs(r.Int()) % 3;
            Contractor = Math.Abs(r.Int()) % CivicCatalogue.Contractors.Length;
            StartDay = r.Int();
            Needed = Math.Max(1f, r.Float());
            Done = Math.Max(0f, r.Float());
            RainHours = r.Int();
            QuietUntil = r.Int();
            HaltUntil = r.Int();
            Complaints = r.Int();
            Reported = r.Bool();
            UnsafeFrom = r.Float();
            Mine = r.Bool();
            Local = r.Bool();
            Favoured = r.Bool();
        }
    }

    /// <summary>The construction programme in one save: the sites in progress and what has been finished for the player.</summary>
    public sealed class WorksState
    {
        public const int Version = 1;

        public int NextId = 1;
        public long LastHour;
        public readonly List<SiteState> Sites = new List<SiteState>(CivicCatalogue.ActiveSites + 2);
        public string RefreshedStreet;   // the player's shopfront that was last refitted...
        public int RefreshedNumber;
        public int RefreshedDay;         // ...and when; 0 for never
        public int Completed;            // sites handed over so far

        public string Write()
        {
            var w = new CivicWriter();
            w.Int(Version).Int(NextId).Long(LastHour).Text(RefreshedStreet).Int(RefreshedNumber).Int(RefreshedDay).Int(Completed).Int(Sites.Count);
            for (int i = 0; i < Sites.Count; i++)
            {
                Sites[i].Write(w);
            }
            return w.ToString();
        }

        public bool Read(string text)
        {
            try
            {
                var r = new CivicReader(text);
                if (r.Int() != Version)
                {
                    return false;
                }
                NextId = Math.Max(1, r.Int());
                LastHour = r.Long();
                RefreshedStreet = r.Text();
                RefreshedNumber = r.Int();
                RefreshedDay = r.Int();
                Completed = r.Int();
                int count = Math.Min(r.Int(), 16);
                Sites.Clear();
                for (int i = 0; i < count; i++)
                {
                    var site = new SiteState();
                    site.Read(r);
                    if (!r.Failed)
                    {
                        Sites.Add(site);
                    }
                }
                return !r.Failed;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>Fields in a row, separated by '|'. Numbers are written the same in every language of the machine.</summary>
    public sealed class CivicWriter
    {
        private readonly StringBuilder _text = new StringBuilder(512);

        public CivicWriter Int(int value)
        {
            _text.Append(value.ToString(CultureInfo.InvariantCulture)).Append('|');
            return this;
        }

        public CivicWriter Long(long value)
        {
            _text.Append(value.ToString(CultureInfo.InvariantCulture)).Append('|');
            return this;
        }

        public CivicWriter Float(float value)
        {
            _text.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            return this;
        }

        public CivicWriter Bool(bool value)
        {
            _text.Append(value ? '1' : '0').Append('|');
            return this;
        }

        public CivicWriter Text(string value)
        {
            _text.Append(string.IsNullOrEmpty(value) ? string.Empty : value.Replace("|", "/")).Append('|');
            return this;
        }

        public override string ToString()
        {
            return _text.ToString();
        }
    }

    public sealed class CivicReader
    {
        private readonly string[] _fields;
        private int _next;

        public CivicReader(string text)
        {
            _fields = (text ?? string.Empty).Split('|');
        }

        /// <summary>True once something was asked for that was not there or was not a number.</summary>
        public bool Failed { get; private set; }

        /// <summary>True while there are fields left to read (a line ends with a divider, so its last field is empty).</summary>
        public bool More => _next < _fields.Length - 1;

        private string Field()
        {
            if (_next >= _fields.Length)
            {
                Failed = true;
                return string.Empty;
            }
            return _fields[_next++];
        }

        public int Int()
        {
            if (int.TryParse(Field(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return value;
            }
            Failed = true;
            return 0;
        }

        public long Long()
        {
            if (long.TryParse(Field(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
            {
                return value;
            }
            Failed = true;
            return 0;
        }

        public float Float()
        {
            if (float.TryParse(Field(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && !float.IsNaN(value) && !float.IsInfinity(value))
            {
                return value;
            }
            Failed = true;
            return 0f;
        }

        public bool Bool()
        {
            return Field() == "1";
        }

        public string Text()
        {
            string field = Field();
            return field.Length == 0 ? null : field;
        }
    }
}
