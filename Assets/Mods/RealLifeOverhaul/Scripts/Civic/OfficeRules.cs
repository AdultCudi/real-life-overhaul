using System;

namespace RealLifeOverhaul
{
    /// <summary>The office the player stands for, or holds.</summary>
    public enum OfficeRace { None = 0, Council = 1, Mayor = 2 }

    /// <summary>Why the player cannot stand (or file) now; None if they can.</summary>
    public enum OfficeRefusal
    {
        None = 0,
        NoHome = 1,          // no home in the city: nobody can stand who does not live here
        Conviction = 2,      // a conviction that still counts
        CaseOpen = 3,        // a case before the court
        Closed = 4,          // nominations are not open
        Standing = 5,        // already standing in this election
        Session = 6,         // a Going Public session is on
        NoPlatform = 7,      // no plank yet
        PartyLine = 8,       // the party's nomination wants a plank on its issue
        Requirements = 9,    // neither the signatures nor the deposit
        Broke = 10           // the deposit cannot be paid
    }

    /// <summary>One way of answering at the debate, a door, a rally or an attack.</summary>
    public enum OfficeAnswer
    {
        Plan = 0,       // the plank on what was asked
        Pivot = 1,      // to the issue one leads on
        Attack = 2,     // the rival's record on what was asked
        Straight = 3,   // an attack answered plainly
        TurnBack = 4,   // ...turned back on whoever made it
        Dodge = 5,      // ...or the subject changed
        Ask = 6         // at a door: straight to asking for a name on the papers, or a vote
    }

    /// <summary>What an opponent attacks the player on. Saved as a number: new ones go at the end.</summary>
    public enum AttackKind
    {
        Record = 0,        // a criminal record (only when there is one, or a case open)
        Business = 1,      // how the player's businesses treat their workers or their neighbourhoods
        Money = 2,         // money from where it should not come, or simply buying the race
        Inexperience = 3,  // never held office
        Absent = 4,        // council meetings missed, or the debate
        Promises = 5       // promises of the last campaign not kept
    }

    /// <summary>The council's four departments the city budget pays for, each set from two steps down to two up.</summary>
    public enum Department { Police = 0, Streets = 1, Transit = 2, Parks = 3 }

    /// <summary>What a door, a rally or the debate did: support in the district (or the city), names on the papers, money in the fund.</summary>
    public struct OfficeGain
    {
        public float Push;
        public int Signatures;
        public int Donation;
        public int Tone;   // -1 it went badly, 0 so so, 1 well
    }

    /// <summary>The city's budget for a week, at the start's prices.</summary>
    public struct BudgetWeek
    {
        public int Revenue, Spending;
        public int Balance => Revenue - Spending;
    }

    /// <summary>
    /// Running for office, by rule: standing, money and the law, the vote and its count by district, doors, rallies, the
    /// debate, attacks, endorsements, the council, the city's budget, approval and recall. Game-free (EconomyCheck runs it);
    /// amounts are at the start's prices. The locales quote several of these numbers: change both together.
    /// </summary>
    public static class OfficeRules
    {
        public const int OwnCampaign = -2;          // a street pick for the player's own council campaign: no slot of the mayoral race

        /// <summary>A mayoral candidate for a poster, a board or a canvasser in the street, by their share of the polls; <paramref name="roll"/> is 0 to 1.</summary>
        public static int StreetPick(float roll, float[] polls, int count)
        {
            float sum = 0f;
            for (int c = 0; c < count; c++)
            {
                sum += polls[c];
                if (roll < sum)
                {
                    return c;
                }
            }
            return count > 1 ? count - 1 : 0;
        }

        public const int NominationsOpen = 14;      // days before election day
        public const int LateFiling = 2;            // ...and still in the first so many days of the campaign
        public const int MeetingEvery = 7;   // days
        public const int MeetingHour = 18, MeetingLast = 21;   // a meeting may begin from 18:00 until 21:00
        public const int BudgetEvery = 28;   // days
        public const int RegisterDays = 7;          // to file the register of interests after taking office
        public const int AttackAnswerDays = 1;      // an attack is answered by the end of the next day, or it stands
        public const int RecallVoteAfter = 7;       // a councillor's recall vote, days after it is called
        public const int OrderEvery = 14;           // days between a mayor's executive orders
        public const int RallyFrom = 10, RallyTo = 20, DoorsFrom = 10, DoorsTo = 20;   // hours of the day

        public static bool NominationsOpenOn(int day, int electionDay, bool campaigning, int campaignDays)
        {
            if (electionDay <= 0)
            {
                return false;
            }
            int left = electionDay - day;
            if (campaigning)
            {
                return left >= campaignDays - LateFiling && left >= 1;
            }
            return left <= NominationsOpen && left >= 1;
        }

        public static int NominationsClose(int electionDay, int campaignDays)
        {
            return electionDay - campaignDays + LateFiling;
        }

        public const int SignaturesCouncil = 60, SignaturesMayor = 300;
        public const int DepositCouncil = 1000, DepositMayor = 5000;
        public const float DepositBack = 0.10f;   // share of the vote that returns the deposit
        public const int GrantCouncil = 1500, GrantMayor = 4000;
        public const int SheetsPerBusiness = 8, SheetsBusinesses = 10;   // names a business's staff and customers put on the papers, ten businesses at most

        public static int SignaturesNeeded(OfficeRace race) => race == OfficeRace.Mayor ? SignaturesMayor : SignaturesCouncil;

        public static int Deposit(OfficeRace race) => race == OfficeRace.Mayor ? DepositMayor : DepositCouncil;

        public static int PartyGrant(OfficeRace race, int party) => party >= 0 && party < CivicCatalogue.Parties.Length ? (race == OfficeRace.Mayor ? GrantMayor : GrantCouncil) : 0;

        /// <summary>The issue a party stands for: its nomination wants a plank on it.</summary>
        public static int PartyIssue(int party) => party >= 0 && party < CivicCatalogue.Parties.Length ? party % CivicCatalogue.Issues : -1;

        public static OfficeRefusal MayStand(bool home, bool conviction, bool caseOpen, bool session, bool open, bool standing)
        {
            if (session)
            {
                return OfficeRefusal.Session;
            }
            if (standing)
            {
                return OfficeRefusal.Standing;
            }
            if (!open)
            {
                return OfficeRefusal.Closed;
            }
            if (!home)
            {
                return OfficeRefusal.NoHome;
            }
            if (conviction)
            {
                return OfficeRefusal.Conviction;
            }
            return caseOpen ? OfficeRefusal.CaseOpen : OfficeRefusal.None;
        }

        /// <summary>Whether papers can be filed: everything to stand, a platform the party will have, and the signatures or the deposit.</summary>
        public static OfficeRefusal MayFile(Candidate me, OfficeRace race, int signatures, bool incumbent, bool payDeposit, bool canPay)
        {
            if (me.Planks <= 0)
            {
                return OfficeRefusal.NoPlatform;
            }
            int issue = PartyIssue(me.Party);
            if (issue >= 0 && me.Stance(issue) < 1f)
            {
                return OfficeRefusal.PartyLine;
            }
            if (incumbent || signatures >= SignaturesNeeded(race))
            {
                return OfficeRefusal.None;
            }
            if (!payDeposit)
            {
                return OfficeRefusal.Requirements;
            }
            return canPay ? OfficeRefusal.None : OfficeRefusal.Broke;
        }

        /// <summary>Names put on the papers by the player's businesses (once a run) and by those who know of them.</summary>
        public static int SheetSignatures(int businesses, int recognition)
        {
            return SheetsPerBusiness * Clamp(businesses, 0, SheetsBusinesses) + Clamp(recognition, 0, 100) / 2;
        }

        public const int DonorLimitCouncil = 500, DonorLimitMayor = 1000;
        public const int PosterRun = 300, PostersPerRun = 12;
        public const int LedgerAd = 800, FlareAd = 500, RallyHire = 600, Canvassers = 400;
        public const int CanvasserDoors = 30;       // doors a day of paid canvassers knocks, in the race's district
        public const int OwnStep = 500;             // the player's own money, put into the fund a step at a time
        public const int SalaryCouncil = 600, SalaryMayor = 1800;

        public static int DonorLimit(OfficeRace race) => race == OfficeRace.Mayor ? DonorLimitMayor : DonorLimitCouncil;

        public static int Salary(OfficeRace holds) => holds == OfficeRace.Mayor ? SalaryMayor : holds == OfficeRace.Council ? SalaryCouncil : 0;

        /// <summary>Kinds of donor offer. Saved as a number.</summary>
        public const int OfferPerson = 0, OfferBusiness = 1, OfferOverLimit = 2, OfferCash = 3, OfferStraw = 4;

        public static bool Legal(int kind, int amount, int limit)
        {
            return (kind == OfferPerson || kind == OfferBusiness) && amount > 0 && amount <= limit;
        }

        /// <summary>A donor's offer for a day, from two rolls of 0 to 999: likelier the better known and placed the player is. Kind and amount (start's prices) out.</summary>
        public static bool Offer(int roll, int roll2, int recognition, float poll, int limit, out int kind, out int amount)
        {
            kind = OfferPerson;
            amount = 0;
            int chance = 250 + Clamp(recognition, 0, 100) * 4 + (int)(Clamp01(poll) * 400f);
            if (roll >= Math.Min(900, chance))
            {
                return false;
            }
            int pick = roll2 % 100;
            if (pick < 50)
            {
                kind = OfferPerson;
                amount = Round50(limit * (20 + roll2 % 81) / 100);
            }
            else if (pick < 72)
            {
                kind = OfferBusiness;
                amount = Round50(limit * (50 + roll2 % 51) / 100);
            }
            else if (pick < 84)
            {
                kind = OfferOverLimit;
                amount = Round50(limit * (2 + roll2 % 3));
            }
            else if (pick < 93)
            {
                kind = OfferCash;
                amount = Round50(limit * (3 + roll2 % 4) / 2);
            }
            else
            {
                kind = OfferStraw;
                amount = Round50(limit * (3 + roll2 % 5));
            }
            amount = Math.Max(50, amount);
            return true;
        }

        public const int DirtyWindow = 28;          // days after the money was taken (or after the election) a donation can still come out

        /// <summary>The chance, per mille, that money taken against the law comes out today: more the more of it, more for somebody well known.</summary>
        public static int DirtyOdds(int dirty, int limit, int recognition)
        {
            if (dirty <= 0)
            {
                return 0;
            }
            int share = limit <= 0 ? 1000 : Math.Min(1000, dirty * 250 / limit);   // four times the limit is all of it
            return Clamp(20 + share / 10 + Clamp(recognition, 0, 100) / 5, 0, 120);
        }

        public const float Sharpness = 1.5f;        // how sharply a lead in score becomes a lead in share

        /// <summary>How well a platform speaks to what matters: stance by weight over stance; 1 for a platform of nothing.</summary>
        public static float Fit(Candidate c, float[] weight)
        {
            float fit = 0f, planks = 0f;
            for (int i = 0; i < CivicCatalogue.Issues; i++)
            {
                float stance = c.Stance(i);
                fit += stance * (weight != null && i < weight.Length ? weight[i] : 1f);
                planks += stance;
            }
            return planks > 0f ? fit / planks : 1f;
        }

        /// <summary>Scores into shares that add up to one (the first <paramref name="count"/> of them).</summary>
        public static void Shares(float[] scores, int count, float[] into)
        {
            float top = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                top = Math.Max(top, scores[i]);
            }
            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                into[i] = (float)Math.Exp((scores[i] - top) * Sharpness);
                total += into[i];
            }
            for (int i = 0; i < into.Length; i++)
            {
                into[i] = i < count && total > 0f ? into[i] / total : 0f;
            }
        }

        /// <summary>What the city knows of the player, to the vote: their recognition, what the public thinks of them (-100 to 100) and their integrity (0 to 100).</summary>
        public static float StandingTerm(int recognition, int publicSigned, int integrity)
        {
            float term = (Clamp(recognition, 0, 100) - 25) / 100f * 0.35f + Clamp(publicSigned, -100, 100) / 100f * 0.25f + (Clamp(integrity, 0, 100) - 50) / 100f * 0.2f;
            return Clamp(term, -0.4f, 0.4f);
        }

        /// <summary>What a campaign's money buys against the other side's: diminishing, both ways, a third of a point at most.</summary>
        public static float SpendingTerm(int spent, int rivalSpent, int scale)
        {
            scale = Math.Max(1, scale);
            double mine = Math.Log(1.0 + Math.Max(0, spent) / (double)scale), theirs = Math.Log(1.0 + Math.Max(0, rivalSpent) / (double)scale);
            return Clamp((float)(0.10 * (mine - theirs)), -0.3f, 0.3f);
        }

        /// <summary>The economy, to whoever holds the office (and the other way, by half, to whoever wants it): CycleState 0 boom to 3 recession.</summary>
        public static float EconomyTerm(int cycle, bool incumbent)
        {
            float held = cycle <= 0 ? 0.20f : cycle == 1 ? 0.05f : cycle == 2 ? -0.12f : -0.30f;
            return incumbent ? held : -held * 0.5f;
        }

        /// <summary>What a district gives somebody of its own: a home there, businesses there (jobs) and the doors and posters of the campaign there.</summary>
        public static float LocalTerm(bool home, int businesses, float pushed)
        {
            return (home ? 0.15f : 0f) + Math.Min(0.2f, 0.05f * Math.Max(0, businesses)) + Clamp(pushed, -0.2f, MostPush);
        }

        public const float MostPush = 0.4f;         // a district's support from doors and posters, at most

        /// <summary>The outsider's lift for an independent while the mayor is unpopular (approval under 40).</summary>
        public static float OutsiderTerm(bool independent, float approval)
        {
            return independent && approval < 40f ? (40f - approval) / 100f : 0f;
        }

        /// <summary>A poll a day nearer the target, with a little noise, never under 2 %.</summary>
        public static float PollStep(float poll, float target, float pull, float noise)
        {
            return Math.Max(0.02f, poll + (target - poll) * pull + noise);
        }

        public static void Normalise(float[] shares, int count)
        {
            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                total += Math.Max(0f, shares[i]);
            }
            for (int i = 0; i < count; i++)
            {
                shares[i] = total > 0f ? Math.Max(0f, shares[i]) / total : 1f / count;
            }
        }

        /// <summary>The city's result shared among the districts: each city share leaned by how much better the platform fits there, and by local pull.</summary>
        public static void DistrictShares(float[] final, float[] cityFit, float[] districtFit, float[] local, int count, float[] into)
        {
            float total = 0f;
            for (int c = 0; c < count; c++)
            {
                into[c] = Math.Max(0f, final[c]) * (float)Math.Exp(Sharpness * (districtFit[c] - cityFit[c] + local[c]));
                total += into[c];
            }
            for (int c = 0; c < into.Length; c++)
            {
                into[c] = c < count && total > 0f ? into[c] / total : 0f;
            }
        }

        /// <summary>Of a district's voters, those who vote: 34 to 56 % by a roll of 0 to 1, more where the campaign has been at their doors (6 points at most).</summary>
        public static float Turnout(float roll, int doors)
        {
            return 0.34f + 0.22f * Clamp01(roll) + Math.Min(0.06f, Math.Max(0, doors) * 0.001f);
        }

        public static int Votes(int electorate, float turnout, float share)
        {
            return (int)Math.Round(Math.Max(0, electorate) * Clamp01(turnout) * Clamp01(share));
        }

        public const float PosterPush = 0.004f, PosterPushMost = 0.12f;   // a poster up in a district, and all of them there

        /// <summary>One door, by the resident's worry and mood (0 to 2), what the player says and a roll of 0 to 999: signatures while filing, now and then money, support.</summary>
        public static OfficeGain Door(int mood, OfficeAnswer answer, bool plankOnTheirs, bool plankOnLead, bool filing, int roll)
        {
            var g = new OfficeGain();
            mood = Clamp(mood, 0, 2);
            float good = 0f;
            switch (answer)
            {
                case OfficeAnswer.Plan: good = plankOnTheirs ? 1f : 0.3f; break;     // their own worry, with a plank on it or only a promise to look
                case OfficeAnswer.Pivot: good = plankOnLead ? 0.55f : 0.2f; break;   // the player's own issue
                default: good = mood == 2 ? 0.6f : mood == 1 ? 0.2f : -0.4f; break;  // straight to asking: a friend signs, a stranger minds
            }
            good += (mood - 1) * 0.3f;
            g.Tone = good >= 0.6f ? 1 : good < 0.1f ? -1 : 0;
            g.Push = Clamp(0.006f * good, -0.003f, 0.008f);
            if (filing && good > 0f)
            {
                g.Signatures = 1 + (roll % 3 == 0 ? 1 : 0) + (answer == OfficeAnswer.Ask && mood == 2 ? 1 : 0);
            }
            if (!filing && answer == OfficeAnswer.Ask && mood == 2 && roll < 250)
            {
                g.Donation = 20 + roll % 81;
            }
            return g;
        }

        /// <summary>A rally, by its opening and close, a plank on what was opened, the crowd and how well known the player is.</summary>
        public static OfficeGain Rally(OfficeAnswer open, bool plank, bool promise, int listeners, int recognition, int roll)
        {
            float good = open == OfficeAnswer.Plan ? (plank ? 1f : 0.35f) : open == OfficeAnswer.Pivot ? 0.6f : (roll < 500 ? 0.8f : 0.1f);
            good += promise ? 0.25f : 0.1f;
            float reach = 0.8f + Clamp(recognition, 0, 100) / 250f + Clamp(listeners, 0, 6) * 0.05f;
            var g = new OfficeGain { Push = Clamp(0.03f * good * reach, 0f, 0.07f) };
            g.Tone = good >= 0.9f ? 1 : good < 0.5f ? -1 : 0;
            return g;
        }

        public const float EmptyChair = -0.05f;     // a debate the player did not turn up to

        /// <summary>One debate answer: a plan pays only with a plank on it, an attack only where the rival has none; to an attack, straight, turned back or dodged.</summary>
        public static float DebateAnswer(OfficeAnswer answer, bool plank, bool rivalPlank, bool attackTrue, int roll)
        {
            switch (answer)
            {
                case OfficeAnswer.Plan: return plank ? 0.04f : 0f;
                case OfficeAnswer.Pivot: return 0.015f;
                case OfficeAnswer.Attack: return rivalPlank ? -0.01f : 0.03f;
                case OfficeAnswer.Straight: return attackTrue ? 0.01f : 0.03f;
                case OfficeAnswer.TurnBack: return roll < 500 ? 0.02f : -0.02f;
                default: return -0.01f;
            }
        }

        /// <summary>The debate is on television: how much of what was said reaches the voters, by how well known the player is.</summary>
        public static float DebateReach(int recognition)
        {
            return 0.8f + Clamp(recognition, 0, 100) / 250f;
        }

        public const int AttackOdds = 350;          // per mille a campaign day, of an attack on the player

        /// <summary>The chance, per mille, of an attack on a campaign day so many days before the vote: more as the day comes near.</summary>
        public static int AttackAttempt(int daysLeft)
        {
            return daysLeft < 1 ? 0 : daysLeft <= 3 ? AttackOdds + 150 : AttackOdds;
        }

        public static float AttackHit(bool true_)
        {
            return true_ ? -0.04f : -0.015f;
        }

        /// <summary>An answer to an attack: what the player wins back or loses, and what it costs the attacker.</summary>
        public static void Answer(OfficeAnswer answer, bool true_, out float mine, out float theirs)
        {
            float hit = AttackHit(true_);
            switch (answer)
            {
                case OfficeAnswer.Straight:
                    mine = true_ ? hit * 0.3f : -hit * 1.2f;   // a denial makes it worse; a rebuttal wins it all back and a little more
                    theirs = true_ ? 0f : -0.02f;
                    return;
                case OfficeAnswer.TurnBack:
                    mine = -0.01f;
                    theirs = -0.04f;
                    return;
                default:
                    mine = 0f;
                    theirs = 0f;
                    return;
            }
        }

        /// <summary>The bodies that endorse in an election, each for the issue it cares about (the last is the City Ledger's editorial: all of them).</summary>
        public static readonly int[] EndorserIssue = { (int)Issue.Business, (int)Issue.Safety, (int)Issue.Services, (int)Issue.Living, (int)Issue.Roads, (int)Issue.Growth, -1 };
        public const int Endorsers = 7;
        public const float EndorsementPush = 0.05f, EditorialPush = 0.06f;
        public const int EndorseDecide = 4;          // days before election day an endorser that has not been asked decides

        /// <summary>How a body sees a platform: its stance on the body's issue (the editorial: every plank, and integrity).</summary>
        public static float EndorserFit(int endorser, Candidate c, int integrity)
        {
            if (endorser < 0 || endorser >= Endorsers)
            {
                return 0f;
            }
            int issue = EndorserIssue[endorser];
            return issue >= 0 ? c.Stance(issue) : c.Planks * 0.4f + (Clamp(integrity, 0, 100) - 50) / 50f;
        }

        /// <summary>The chance, per mille, that a body asked by the player endorses them: their fit against the best rival's, and the player's influence.</summary>
        public static int AskOdds(float fit, float bestRival, int influence)
        {
            return Clamp(300 + (int)((fit - bestRival) * 300f) + Clamp(influence, 0, 100) * 4, 50, 900);
        }

        /// <summary>The levers a mayor may set by order, without the council: how the city is kept, not what it charges.</summary>
        public static bool ByOrder(Lever lever)
        {
            return lever == Lever.Cleaning || lever == Lever.Events || lever == Lever.Permits || lever == Lever.Transit;
        }

        /// <summary>One member's chance of voting aye: the issue's weight now, a petition, the mayor's side, and the player's influence if they tabled it.</summary>
        public static float MemberOdds(float support, bool petitioned, bool mayorsSide, bool mayorTabled, bool playerTabled, int influence)
        {
            float odds = 0.25f + 0.35f * Clamp01(support) + (petitioned ? 0.2f : 0f) + (mayorsSide ? 0.25f : 0f) + (mayorTabled && mayorsSide ? 0.1f : 0f)
                         + (playerTabled ? Clamp(influence, 0, 100) / 400f : 0f);
            return Clamp(odds, 0.05f, 0.95f);
        }

        public const float MayorWord = 0.15f;       // what the mayor speaking for (or against) a motion adds to their own party's members' odds

        /// <summary>A motion passes with more ayes than noes; an abstention or an absence is neither.</summary>
        public static bool Passes(int ayes, int noes)
        {
            return ayes > noes;
        }

        /// <summary>True if a lever touches what the player earns from their own businesses or buildings: a vote on it is a conflict of interest.</summary>
        public static bool Conflicted(Lever lever, int businesses, bool landlord)
        {
            switch (lever)
            {
                case Lever.BusinessTax:
                case Lever.TradeLevy:
                case Lever.Grants:
                case Lever.Wages:
                case Lever.Parking:
                    return businesses > 0;
                case Lever.Rent:
                    return businesses > 0 || landlord;
                default:
                    return false;
            }
        }

        public const int UndeclaredWindow = 21;     // days an undeclared interest can still come out

        /// <summary>The chance, per mille a day, that a vote cast without declaring an interest comes out: more if it decided the motion, more for somebody well known.</summary>
        public static int UndeclaredOdds(bool decisive, int recognition, bool registered)
        {
            int odds = 40 + (decisive ? 30 : 0) + Clamp(recognition, 0, 100) / 2;
            return registered ? odds : odds * 2;
        }

        public const int DepartmentMost = 2;   // steps either way
        public const float DepartmentStep = 0.12f;  // a step up or down is this much more or less spent on a department
        public static readonly int[] DepartmentShare = { 30, 25, 25, 20 };   // per cent of the city's revenue each department takes as the city came

        /// <summary>What the city takes in a week from its electorate and its policies, at the start's prices.</summary>
        public static int Revenue(int electorate, int[] levers)
        {
            double r = Math.Max(1, electorate) * 0.6;
            if (levers != null && levers.Length >= CivicCatalogue.Levers)
            {
                r *= 1.0 + 0.10 * levers[(int)Lever.BusinessTax] + 0.05 * Math.Max(0, levers[(int)Lever.TradeLevy]) - 0.04 * Math.Max(0, -levers[(int)Lever.TradeLevy])
                     + 0.02 * levers[(int)Lever.Parking] + 0.02 * levers[(int)Lever.Transit] - 0.03 * Math.Max(0, levers[(int)Lever.Grants]) + 0.01 * Math.Max(0, -levers[(int)Lever.Grants]);
            }
            return (int)Math.Round(r);
        }

        public static BudgetWeek Week(int electorate, int[] levers, int[] levels)
        {
            int baseRevenue = Revenue(electorate, null);
            var w = new BudgetWeek { Revenue = Revenue(electorate, levers) };
            double spend = 0.0;
            for (int d = 0; d < DepartmentShare.Length; d++)
            {
                int level = levels != null && d < levels.Length ? Clamp(levels[d], -DepartmentMost, DepartmentMost) : 0;
                spend += baseRevenue * DepartmentShare[d] / 100.0 * (1.0 + DepartmentStep * level);
            }
            w.Spending = (int)Math.Round(spend);
            return w;
        }

        /// <summary>What a department's level does to the heat it answers for (police: crime; streets: accidents): 8 % less a step up.</summary>
        public static float HeatScale(int level)
        {
            return 1f - 0.08f * Clamp(level, -DepartmentMost, DepartmentMost);
        }

        /// <summary>The budget, to the mayor's approval: a point a step for services people see, weighted by what matters, less a deficit (ten points at most).</summary>
        public static float BudgetApproval(int[] levels, float[] issueWeight, long reserve, int weekRevenue)
        {
            float points = 0f;
            for (int d = 0; levels != null && d < levels.Length && d < 4; d++)
            {
                int issue = d == 0 ? (int)Issue.Safety : d == 1 ? (int)Issue.Roads : d == 2 ? (int)Issue.Services : (int)Issue.Living;
                float weight = issueWeight != null && issue < issueWeight.Length ? issueWeight[issue] : 1f;
                points += Clamp(levels[d], -DepartmentMost, DepartmentMost) * weight;
            }
            if (reserve < 0 && weekRevenue > 0)
            {
                points -= Math.Min(10f, -reserve / (float)weekRevenue * 2f);
            }
            return points;
        }

        /// <summary>The reserve the city starts with: four weeks' surplus of a tenth of its revenue.</summary>
        public static long StartReserve(int electorate)
        {
            return (long)Revenue(electorate, null) * 4 / 10;
        }

        /// <summary>The budget a mayor of the city's choosing would set: a step up on what they lead on, a step down on the department furthest from it.</summary>
        public static void CityBudget(int lead, int[] into)
        {
            for (int d = 0; d < into.Length; d++)
            {
                into[d] = 0;
            }
            int up = lead == (int)Issue.Safety ? 0 : lead == (int)Issue.Roads ? 1 : lead == (int)Issue.Services ? 2 : lead == (int)Issue.Living ? 3 : -1;
            if (up >= 0 && up < into.Length)
            {
                into[up] = 1;
                into[(up + 2) % into.Length] = -1;
            }
        }

        public const float RecallBelow = 25f;   // approval
        public const int RecallDays = 7;   // days in a row below it

        /// <summary>Where a councillor's approval is heading: part of the mayor's, promises kept, district votes, scandals, meetings missed and the economy.</summary>
        public static float CouncillorTarget(float mayorApproval, int kept, int unkept, float votesRecord, int scandals, int missed, int cycle)
        {
            float target = 50f + (mayorApproval - 50f) * 0.3f + kept * 2.5f - unkept * 1.5f + Clamp(votesRecord, -10f, 10f) - scandals * 6f - missed * 2f
                           + EconomyTerm(cycle, true) * 10f;
            return Clamp(target, 5f, 90f);
        }

        /// <summary>Approval moves 15 % of the way to its target a day.</summary>
        public static float Approach(float approval, float target, float noise)
        {
            return Clamp(approval + (target - approval) * 0.15f + noise, 3f, 95f);
        }

        /// <summary>A councillor's recall vote: the share that keeps them, by approval and the campaign against the recall.</summary>
        public static float RecallKeep(float approval, float push)
        {
            return Clamp(0.32f + Clamp(approval, 0f, 100f) / 100f * 0.6f + Clamp(push, 0f, MostPush), 0.05f, 0.95f);
        }

        public static float StartApproval(float share)
        {
            return Clamp(46f + Clamp01(share) * 30f, 40f, 72f);
        }

        public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

        public static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

        public static float Clamp01(float value) => Clamp(value, 0f, 1f);

        private static int Round50(int amount) => Math.Max(50, (amount + 25) / 50 * 50);

        /// <summary>A roll of 0 to 999 from a seed and numbers: the same every time.</summary>
        public static int Roll(int seed, int a, int b)
        {
            var random = new CivicRandom(CivicRandom.Mix(seed, a, b));
            return (int)(random.Next() % 1000u);
        }
    }
}
