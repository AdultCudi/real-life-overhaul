using System;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>What an incident is, which decides the moves an officer has at it. Not saved.</summary>
    public enum DutyKind
    {
        Theft = 0,      // a snatch, a shoplifter, a car broken into or taken, a deal
        Fight = 1,      // a fight, a mugging, an assault, a hold-up
        Nuisance = 2,   // a drunk, graffiti, a stall without a permit, a fare dodged, a parked car
        Accident = 3,   // a crash on the road
        Casualty = 4    // a person lying in the road
    }

    /// <summary>What the officer can do at an incident. Not saved.</summary>
    public enum DutyMove
    {
        TalkDown = 0,
        Arrest = 1,
        Backup = 2,
        FirstAid = 3,
        Ticket = 4,
        BackOff = 5     // always there: it costs the shift's rating, never the officer's health
    }

    /// <summary>How an answered incident ended. Not saved.</summary>
    public enum DutyResult
    {
        Success = 0,
        Failed = 1,     // they got away, refused, or the help came too late: nobody hurt
        Hurt = 2        // the officer was injured, and the incident was not settled
    }

    /// <summary>Why a shift ended. Not saved.</summary>
    public enum DutyEnd
    {
        Handed = 0,     // the officer ended it, or the hours ran out
        Hurt = 1,       // an injury sent the officer home
        Skipped = 2,    // the clock jumped (a sleep, a stay in hospital): what was not seen is not worked
        Dismissed = 3   // the service let the officer go
    }

    /// <summary>
    /// The rules of a police career's shift in plain numbers: who may join, what an incident is and how dangerous, the moves, the odds of
    /// each outcome, the shift's rating, injuries and when the service lets an officer go. Game-free: EconomyCheck runs this file as it ships,
    /// so the Guide's figures are these. Nothing is rolled here: the caller hands in a roll from the save's seed (<see cref="Roll"/>), so
    /// every machine and every reload gives the same answer. Every constant has its unit and the reason it is what it is.
    /// </summary>
    public static class PoliceRules
    {
        // ------------------------------------------------------------------------------------------------- joining

        public const int CleanDays = 180;           // days (of a 365-day year, scaled to the game's) with nothing new on the police record: a ticket counts
        public const int EntryRank = 0;             // the rung everyone starts on, the cadet's

        // ------------------------------------------------------------------------------------------------- the shift

        public const int Hour = 14;                 // the hour a shift starts: the afternoon into the evening, when the city has most to answer
        public const int Hours = 8;                 // its length, so it ends at 22:00 inside the day
        public const int NoticeMetres = 130;        // an incident this near is said once, with its danger: the dispatcher's call
        public const int ArriveMetres = 14;         // ...and this near, on foot, the list of moves opens
        public const int MostJumpMinutes = 12;      // game minutes one look may see pass and count: a longer jump is a sleep or a skip, and ends the shift
        public const float PromptGapSeconds = 20f;  // real seconds before a list the officer closed is offered again
        public const int LookEverySeconds = 5;      // seconds between looks at the officer's health and licence

        // ------------------------------------------------------------------------------------------------- danger

        public const int Routine = 0, Tense = 1, Risky = 2, Deadly = 3;   // an incident's danger; the order is the danger's
        public const int Dangers = 4;
        public static readonly int[] InjuryHours = { 0, 12, 30, 54 };     // hours hurt by danger: under a day is a bruise, from a day a sprain, from two a broken arm or a concussion (Health)
        public const float InjuryEnergy = 10f;                              // energy an injury costs at once
        public const int FatalFrom = Deadly;                                // the only danger whose injury Permadeath may judge as fatal (PermadeathRules.DutyFrom)

        // ------------------------------------------------------------------------------------------------- the rating

        public const int RatingStart = 60;          // a quiet shift's rating: a day worked and nothing asked of it
        public const int RatingMost = 100, RatingLeast = 0;
        public const int GainSuccess = 6;           // plus GainDanger for each step of danger
        public const int GainDanger = 2;
        public const int GainBackup = 2;            // calling for backup is never wrong; at a risky or a deadly one it is sound
        public const int GainBackupRisky = 3;
        public const int GainTicket = 3;
        public const int LossFailed = 5;            // at a routine or a tense incident; two less at a risky or a deadly one
        public const int LossFailedRisky = 3;
        public const int LossHurt = 2;              // an injury is not the officer's fault
        public const int LossBackOff = 8;           // at a routine incident, two less for each step of danger: walking away from a deadly one is nearly free
        public const int LossBackOffStep = 2;
        public const int CommendRating = 90;        // a shift that ends at this rating or more earns a commendation, as does a settled risky or deadly incident
        public const int ReviewSteps = 6;           // rating points that move a review's score by one

        // ------------------------------------------------------------------------------------------------- the odds (thousandths)

        public const int OddsBase = 450;            // the chance at a routine incident for someone with no skill on the entry rung
        public const int OddsPerPoint = 3;          // each point (0 to 100) of the skill a move rides on
        public const int OddsPerRank = 30;          // each rung
        public const int OddsPerDanger = 150;       // each step of danger takes this off
        public const int OddsArrestExtra = 50;      // an arrest is harder than talking
        public const int OddsAidExtra = 50;         // first aid is not about a fight
        public const int OddsLeast = 100, OddsMost = 950;   // nothing is certain either way
        public const int HurtPerDanger = 90;        // the chance of an injury for each step of danger, before skill and rank
        public const int HurtArrestExtra = 40;
        public const int HurtAidPerDanger = 40;     // first aid on a road is risky at a risky or deadly scene only
        public const int HurtMost = 450;
        public const int HurtRankOff = 8;           // each rung takes this off the chance
        public const int BackupHurtPerDanger = 40;  // backup is nearly safe: only a risky or a deadly scene can hurt whoever waits for it
        public const int TicketFailPerDanger = 100; // the driver or the stall's owner who will not take a ticket
        public const int TicketFailRankOff = 10;

        // ------------------------------------------------------------------------------------------------- progress

        public const int ReviewBonusMost = 8;       // points a review's score moves with the shift ratings, either way
        public const int RingSize = 6;              // the last shift ratings the review reads

        public static readonly string[] DangerKeys =
        {
            "reallife_police_danger_routine", "reallife_police_danger_tense", "reallife_police_danger_risky", "reallife_police_danger_deadly"
        };

        public static readonly string[] MoveKeys =
        {
            "reallife_police_move_talk", "reallife_police_move_arrest", "reallife_police_move_backup", "reallife_police_move_aid", "reallife_police_move_ticket",
            "reallife_police_move_back_off"
        };

        public static readonly string[] KindKeys =
        {
            "reallife_police_kind_theft", "reallife_police_kind_fight", "reallife_police_kind_nuisance", "reallife_police_kind_accident", "reallife_police_kind_casualty"
        };

        // ------------------------------------------------------------------------------------------------- what an incident is

        /// <summary>
        /// The kind and danger of a staged street crime by its id in the crime catalogue; false for one that is not the officer's to answer
        /// (a crime against the player, or a theft in the player's own shop, which is the shop's own business).
        /// </summary>
        public static bool Crime(string id, out DutyKind kind, out int danger)
        {
            kind = DutyKind.Nuisance;
            danger = Routine;
            switch (id)
            {
                case "questioning": case "graffiti": case "street_vendor": case "panhandler": case "fare_dodger": case "parking_ticket":
                    return true;
                case "drunk": case "traffic_stop":
                    danger = Tense;
                    return true;
                case "reckless_driver":
                    danger = Risky;
                    return true;
                case "snatch": case "car_breakin": case "shoplifter":
                    kind = DutyKind.Theft;
                    danger = Tense;
                    return true;
                case "foot_chase": case "car_theft": case "drug_deal":
                    kind = DutyKind.Theft;
                    danger = Risky;
                    return true;
                case "disturbance":
                    kind = DutyKind.Fight;
                    danger = Tense;
                    return true;
                case "mugging": case "assault":
                    kind = DutyKind.Fight;
                    danger = Risky;
                    return true;
                case "holdup":
                    kind = DutyKind.Fight;
                    danger = Deadly;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The danger of a road accident by its severity (0 slight, 1 serious, 2 severe).</summary>
        public static int AccidentDanger(int severity) => TalentCatalogue.Clamp(severity, Routine, Risky);

        /// <summary>The danger of a person lying in the road: the traffic is the risk.</summary>
        public static int CasualtyDanger => Tense;

        /// <summary>The moves an incident of a kind offers, in the order they are listed: never <see cref="DutyMove.BackOff"/>, which is always there.</summary>
        public static DutyMove[] Moves(DutyKind kind)
        {
            switch (kind)
            {
                case DutyKind.Theft: return TheftMoves;
                case DutyKind.Fight: return FightMoves;
                case DutyKind.Accident: return AccidentMoves;
                case DutyKind.Casualty: return CasualtyMoves;
                default: return NuisanceMoves;
            }
        }

        private static readonly DutyMove[] TheftMoves = { DutyMove.Arrest, DutyMove.TalkDown, DutyMove.Backup };
        private static readonly DutyMove[] FightMoves = { DutyMove.TalkDown, DutyMove.Arrest, DutyMove.Backup };
        private static readonly DutyMove[] NuisanceMoves = { DutyMove.TalkDown, DutyMove.Ticket, DutyMove.Backup };
        private static readonly DutyMove[] AccidentMoves = { DutyMove.FirstAid, DutyMove.Ticket, DutyMove.Backup };
        private static readonly DutyMove[] CasualtyMoves = { DutyMove.FirstAid, DutyMove.Backup };

        /// <summary>The skill a move rides on (the careers' own numbering), or -1 for a move that rides on none.</summary>
        public static int SkillOf(DutyMove move)
        {
            switch (move)
            {
                case DutyMove.TalkDown: return TalentCatalogue.Relationships;
                case DutyMove.Arrest: return Skills.Guarding;
                case DutyMove.FirstAid: return Skills.FirstAid;
                default: return -1;
            }
        }

        // ------------------------------------------------------------------------------------------------- the odds

        /// <summary>
        /// The chances of each outcome of a move, in thousandths, which always add to 1000: <paramref name="success"/> it is settled,
        /// <paramref name="hurt"/> the officer is injured and it is not, and what is left it fails with nobody hurt. A routine incident
        /// never hurts anyone; backing off is no move and has no odds.
        /// </summary>
        public static void Odds(DutyMove move, int danger, int rank, int points, out int success, out int hurt, out int failed)
        {
            danger = TalentCatalogue.Clamp(danger, Routine, Deadly);
            rank = Math.Max(0, rank);
            points = TalentCatalogue.Clamp(points, 0, 100);
            switch (move)
            {
                case DutyMove.Backup:
                    hurt = danger >= Risky ? (danger - 1) * BackupHurtPerDanger : 0;
                    success = 1000 - hurt;
                    failed = 0;
                    return;
                case DutyMove.Ticket:
                    hurt = danger >= Risky ? (danger - 1) * BackupHurtPerDanger : 0;
                    failed = TalentCatalogue.Clamp(danger * TicketFailPerDanger - rank * TicketFailRankOff, 0, 1000 - hurt);
                    success = 1000 - hurt - failed;
                    return;
                case DutyMove.BackOff:
                    success = hurt = 0;
                    failed = 1000;
                    return;
            }
            bool aid = move == DutyMove.FirstAid;
            int chance = OddsBase + points * OddsPerPoint + rank * OddsPerRank - danger * OddsPerDanger - (move == DutyMove.Arrest ? OddsArrestExtra : 0) + (aid ? OddsAidExtra : 0);
            success = TalentCatalogue.Clamp(chance, OddsLeast, OddsMost);
            if (danger == Routine)
            {
                hurt = 0;
            }
            else if (aid)
            {
                hurt = TalentCatalogue.Clamp(danger * HurtAidPerDanger - points / 2 - rank * HurtRankOff, 0, HurtMost);
            }
            else
            {
                hurt = TalentCatalogue.Clamp(danger * HurtPerDanger + (move == DutyMove.Arrest ? HurtArrestExtra : 0) - points - rank * HurtRankOff, 0, HurtMost);
            }
            if (success + hurt > 1000)
            {
                success = 1000 - hurt;
            }
            failed = 1000 - success - hurt;
        }

        /// <summary>
        /// The roll of one answered incident, 0 to 999: the save's seed, the day, the shift's incident number and the officer's rank, the same
        /// on every machine and every reload (Going Public: nothing here is the machine's own luck).
        /// </summary>
        public static int Roll(int seed, int day, int number, int rank) => TechRandom.Mille(seed, SaltRoll, day, number * 16 + Math.Max(0, rank));

        private const int SaltRoll = 9301;

        /// <summary>What a roll comes to against the odds of a move.</summary>
        public static DutyResult Result(DutyMove move, int danger, int rank, int points, int roll)
        {
            Odds(move, danger, rank, points, out int success, out int hurt, out _);
            return roll < success ? DutyResult.Success : roll < success + hurt ? DutyResult.Hurt : DutyResult.Failed;
        }

        /// <summary>True if a move can hurt the officer at a danger at all: the injuries only ever come from here.</summary>
        public static bool CanHurt(DutyMove move, int danger)
        {
            Odds(move, danger, 0, 0, out _, out int hurt, out _);
            return hurt > 0;
        }

        /// <summary>The hours of injury an outcome leaves: none unless <see cref="DutyResult.Hurt"/>.</summary>
        public static int HurtHours(DutyResult result, int danger) => result == DutyResult.Hurt ? InjuryHours[TalentCatalogue.Clamp(danger, 0, Dangers - 1)] : 0;

        /// <summary>The measure handed to Permadeath's judge for an injury: the incident's danger, so only a deadly one can end a life.</summary>
        public static float FatalMeasure(int danger) => danger;

        // ------------------------------------------------------------------------------------------------- the rating

        /// <summary>The rating a result moves the shift by: a move's own, with the danger it was made at.</summary>
        public static int Change(DutyMove move, DutyResult result, int danger)
        {
            danger = TalentCatalogue.Clamp(danger, Routine, Deadly);
            if (move == DutyMove.BackOff)
            {
                return -Math.Max(0, LossBackOff - LossBackOffStep * danger);
            }
            switch (result)
            {
                case DutyResult.Hurt:
                    return -LossHurt;
                case DutyResult.Failed:
                    return -(danger >= Risky ? LossFailedRisky : LossFailed);
            }
            if (move == DutyMove.Backup)
            {
                return danger >= Risky ? GainBackupRisky : GainBackup;
            }
            if (move == DutyMove.Ticket)
            {
                return GainTicket;
            }
            return GainSuccess + GainDanger * danger;
        }

        public static int Clamp(int rating) => TalentCatalogue.Clamp(rating, RatingLeast, RatingMost);

        /// <summary>True if a settled incident earns a commendation: an arrest, a talk or first aid at a risky or a deadly one (never a call for backup).</summary>
        public static bool Commended(DutyMove move, DutyResult result, int danger)
        {
            return result == DutyResult.Success && danger >= Risky && (move == DutyMove.Arrest || move == DutyMove.TalkDown || move == DutyMove.FirstAid);
        }

        /// <summary>True if a whole shift earns one: its rating at the end, and at least one incident answered.</summary>
        public static bool ShiftCommended(int rating, int answered) => answered > 0 && rating >= CommendRating;

        /// <summary>The words that tell a chance: thousandths to the whole percent.</summary>
        public static int Percent(int mille) => (mille + 5) / 10;

        // ------------------------------------------------------------------------------------------------- the review

        /// <summary>What the last shift ratings add to or take from a review's score: the mean against a quiet shift's, in steps, within <see cref="ReviewBonusMost"/>. 0 for none.</summary>
        public static int ReviewBonus(int[] ring, int count)
        {
            if (ring == null || count <= 0)
            {
                return 0;
            }
            int sum = 0;
            int n = Math.Min(count, ring.Length);
            for (int i = 0; i < n; i++)
            {
                sum += ring[i];
            }
            int mean = sum / n;
            int steps = (mean - RatingStart) / ReviewSteps;
            return TalentCatalogue.Clamp(steps, -ReviewBonusMost, ReviewBonusMost);
        }

        /// <summary>The score a shift is recorded with on the standing's ring: the job's own score and the shift's rating, evenly.</summary>
        public static int Blend(int score, int rating) => TalentCatalogue.Clamp((score + rating) / 2, 0, 100);

        // ------------------------------------------------------------------------------------------------- joining and leaving

        /// <summary>What the service reads of the player when they apply or when they stand for duty.</summary>
        public struct Conduct
        {
            public int SinceOffence;   // days since the latest line of the police record, -1 for a clean record
            public bool Serious;       // held in a cell, or wanted, since the day asked about
            public bool Convicted;     // a conviction since the day asked about
            public bool Licence;       // a driving licence in good standing
            public bool Sound;         // nothing so serious that it needs the hospital: the entry's ask
            public bool Fit;           // no illness or injury at all that sends someone home: a shift's ask
        }

        /// <summary>
        /// The reason a player cannot join the service, as a locale key (with its figures through <paramref name="days"/>), or null. The entry
        /// exam is the cadet rung's own qualification (the careers' ordinary one), not this.
        /// </summary>
        public static string EntryWhy(Conduct c, int year, out int days)
        {
            days = TalentCatalogue.Scaled(CleanDays, year);
            if (c.Convicted)
            {
                return "reallife_police_why_conviction";
            }
            if (c.Serious || (c.SinceOffence >= 0 && c.SinceOffence < days))
            {
                return "reallife_police_why_record";
            }
            if (!c.Licence)
            {
                return "reallife_police_why_licence";
            }
            return c.Sound ? null : "reallife_police_why_unfit";
        }

        /// <summary>The reason a shift cannot be begun now (not a reason not to be an officer), or null: unwell, or a driving licence lost.</summary>
        public static string ShiftWhy(Conduct c)
        {
            if (!c.Fit)
            {
                return "reallife_police_why_shift_unfit";
            }
            return c.Licence ? null : "reallife_police_why_shift_licence";
        }

        /// <summary>
        /// True if the service lets an officer go: a stay in a cell or a warrant, or a conviction, on a day after they joined. A ticket does not
        /// (an officer's minor traffic stops are a courtesy, see <see cref="Courtesy"/>), and what was on the record before is the entry's.
        /// </summary>
        public static bool Dismissed(Conduct sinceHired) => sinceHired.Serious || sinceHired.Convicted;

        /// <summary>
        /// True if a minor traffic stop of an officer on a shift in uniform ends in a word and no ticket: nobody was hurt, no damage is owed
        /// and there was no drink. Anything else is as it always was.
        /// </summary>
        public static bool Courtesy(bool onShift, int hurt, int killed, int owed, bool drunk) => onShift && hurt == 0 && killed == 0 && owed <= 0 && !drunk;

        /// <summary>Counted minutes of a look: the clock's move since the last, or 0 for a jump too long to have been watched (a sleep, a skip).</summary>
        public static int Counted(int minutes) => minutes >= 0 && minutes <= MostJumpMinutes ? minutes : 0;

        /// <summary>True if a look saw a jump of the clock that ends the shift.</summary>
        public static bool Jumped(int minutes) => minutes > MostJumpMinutes || minutes < 0;
    }
}
