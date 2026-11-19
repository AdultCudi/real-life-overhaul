using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>How an officer's case ended. Saved as a number: new ends go at the end.</summary>
    public enum CaseEnd
    {
        Open = 0,
        Released = 1,    // never charged: the arrest was not enough
        Acquitted = 2,
        Convicted = 3,   // after a hearing
        Pleaded = 4      // pleaded guilty before it
    }

    /// <summary>What a detective does for a case at a shift. Not saved.</summary>
    public enum CaseAction
    {
        Witness = 1,     // interview a witness
        Scene = 2,       // check the scene
        Write = 3        // write the file up
    }

    /// <summary>How a detective gives evidence at a hearing. Not saved.</summary>
    public enum WitnessChoice
    {
        Plain = 1,       // what was seen, as it was
        Prepared = 2,    // from the file: better if the file was written up
        Hold = 3         // say little
    }

    /// <summary>One arrest's case in the officer's case book. Game-free.</summary>
    public sealed class OfficerCase
    {
        public int Id;
        public string Name = string.Empty;      // the suspect, as the city's people are named (a person of the pool)
        public int PersonId;                    // their id in the pool; 0 where the name is the catalogue's
        public string Offence = string.Empty;   // a charge of Justice's table (JusticeLaw)
        public string District = string.Empty;  // the district's name key, as the places are named; empty for none
        public int Day;                         // the day of the arrest
        public int Hearing;                     // the day it is heard; 0 for a case never charged
        public int Quality;                     // the arrest's quality, 0 to 100, fixed at the arrest
        public int Strength;                    // the case's strength, 0 to 100: the arrest's quality and what a detective adds
        public int WorkedDay = -1;              // the day a detective last worked it (one action a shift)
        public int Done;                        // bits of CaseAction done on it
        public int Witness;                     // what the officer's evidence added to the odds, in points of strength
        public bool Summoned;                   // called to give evidence, and answered
        public CaseEnd End;
        public int EndDay;
        public int Sentence;                    // days away after a conviction, on the record only (see the docs)

        public bool Open => End == CaseEnd.Open;
    }

    /// <summary>
    /// The rules of an arrest's aftermath in plain numbers: whether it is charged, when it is heard, how it ends, what a detective adds and how the officer's evidence
    /// weighs, and what a good or a poor arrest does. Game-free; every outcome is decided from the offence, the arrest's quality and the save's seed, never from luck of
    /// the machine. The words, the timings and the sentences are Justice's own (JusticeLaw), as the player's own charges have them.
    /// </summary>
    public static class CaseRules
    {
        public const int DetectiveRank = 2;       // the rung from which cases are worked and evidence given
        public const int ActionsMost = 3;         // a detective's actions in one shift, over all cases
        public const int Most = 40;               // cases kept: the oldest closed go

        public const int QualityBase = 40;        // an arrest's quality before the margin, the rank and the skill
        public const int QualityPerRank = 4;
        public const int QualityMarginDiv = 20;   // thousandths of margin to a point
        public const int QualityPointsDiv = 5;    // points of the arrest skill to a point
        public const int GoodAt = 65;             // a quality from here is a good arrest...
        public const int PoorAt = 40;             // ...and under here a poor one, which a court throws out

        public const int ChargeBase = 500;        // thousandths that a case is charged at quality 0...
        public const int ChargePerQuality = 4;    // ...and each point of quality adds this
        public const int AcquitBase = 600;        // thousandths acquitted at strength 0...
        public const int AcquitPerStrength = 6;   // ...each point of strength (and of evidence) takes this off
        public const int AcquitLeast = 50, AcquitMost = 700;
        public const int PleaShare = 300;         // thousandths of the rest that plead guilty

        public const int GainBase = 5;            // strength a detective's action adds...
        public const int GainPointsDiv = 8;       // ...and one per this many points of its skill
        public const int GainPerRank = 2;         // ...and per rung above a detective's
        public const int GainMost = 20;
        public const int WitnessPrepared = 8;     // evidence from a written-up file
        public const int WitnessUnready = 2;      // ...from one not written up
        public const int WitnessPlain = 0;
        public const int WitnessHold = -6;
        public const int WitnessHourMinutes = 60; // an hour of the shift in court

        public const int ThrownDays = 90;         // a case thrown out for a poor arrest counts against reviews this long (a 365-day year, scaled)
        public const int ThrownMost = 3;          // ...at most this many points

        private const int SaltCharge = 9311, SaltHearing = 9313;

        /// <summary>The charge of Justice's table an arrest for a street crime of the catalogue is for.</summary>
        public static string Offence(string crimeId)
        {
            switch (crimeId)
            {
                case "mugging": case "holdup": return JusticeLaw.Robbery;
                case "assault": case "disturbance": return JusticeLaw.Assault;
                case "graffiti": return JusticeLaw.Vandalism;
                case "foot_chase": return JusticeLaw.Resisting;
                default: return JusticeLaw.Theft;
            }
        }

        /// <summary>The arrest's quality: the move's margin (thousandths of chance it passed by), the rank and the skill the move rides on, 0 to 100.</summary>
        public static int Quality(int margin, int rank, int points)
        {
            return TalentCatalogue.Clamp(QualityBase + Math.Max(0, margin) / QualityMarginDiv + Math.Max(0, rank) * QualityPerRank + TalentCatalogue.Clamp(points, 0, 100) / QualityPointsDiv, 0, 100);
        }

        public static bool Good(int quality) => quality >= GoodAt;

        public static bool Poor(int quality) => quality < PoorAt;

        /// <summary>The chance, in thousandths, that an arrest of this quality is charged.</summary>
        public static int ChargeChance(int quality) => TalentCatalogue.Clamp(ChargeBase + TalentCatalogue.Clamp(quality, 0, 100) * ChargePerQuality, 0, 1000);

        public static bool Charged(int seed, int day, int id, int quality) => TechRandom.Mille(seed, SaltCharge, day, id) < ChargeChance(quality);

        /// <summary>The day a charged case is heard: Justice's own days to court for the offence's class.</summary>
        public static int HearingDay(int day, string offence)
        {
            ChargeDef def = JusticeLaw.Find(offence);
            return day + JusticeLaw.DaysToCourt(def != null ? def.Class : ChargeClass.Misdemeanour);
        }

        /// <summary>The chances of each end of a hearing, in thousandths, adding to one thousand.</summary>
        public static void Odds(int strength, int witness, out int acquitted, out int pleaded, out int convicted)
        {
            int effective = TalentCatalogue.Clamp(strength, 0, 100) + witness;
            acquitted = TalentCatalogue.Clamp(AcquitBase - effective * AcquitPerStrength, AcquitLeast, AcquitMost);
            pleaded = (1000 - acquitted) * PleaShare / 1000;
            convicted = 1000 - acquitted - pleaded;
        }

        /// <summary>How a case ends at its hearing: the roll is the save's seed, the case and its hearing day, the same on every machine and every load.</summary>
        public static CaseEnd Decide(int seed, OfficerCase c)
        {
            Odds(c.Strength, c.Witness, out int acquitted, out int pleaded, out _);
            int roll = TechRandom.Mille(seed, SaltHearing, c.Id, c.Hearing);
            return roll < acquitted ? CaseEnd.Acquitted : roll < acquitted + pleaded ? CaseEnd.Pleaded : CaseEnd.Convicted;
        }

        /// <summary>Days of the sentence on the record: Justice's prison days for the offence, three quarters of them for a plea (its own guilty-plea factor).</summary>
        public static int Sentence(string offence, CaseEnd end)
        {
            ChargeDef def = JusticeLaw.Find(offence);
            int days = def != null ? def.PrisonDays : 0;
            return end == CaseEnd.Convicted ? days : end == CaseEnd.Pleaded ? (int)Math.Round(days * JusticeLaw.GuiltyPleaFactor) : 0;
        }

        /// <summary>The skill an action rides on: interviewing on relationships, the scene on guarding, the file on writing.</summary>
        public static int SkillOf(CaseAction action)
        {
            return action == CaseAction.Witness ? TalentCatalogue.Relationships : action == CaseAction.Scene ? Skills.Guarding : TalentCatalogue.Writing;
        }

        /// <summary>The strength an action adds to a case, by the detective's skill and rank.</summary>
        public static int Gain(int points, int rank)
        {
            return TalentCatalogue.Clamp(GainBase + TalentCatalogue.Clamp(points, 0, 100) / GainPointsDiv + Math.Max(0, rank - DetectiveRank) * GainPerRank, 0, GainMost);
        }

        /// <summary>What the officer's evidence adds to the odds for a choice, by whether the file was written up.</summary>
        public static int WitnessValue(WitnessChoice choice, bool written)
        {
            return choice == WitnessChoice.Prepared ? (written ? WitnessPrepared : WitnessUnready) : choice == WitnessChoice.Hold ? WitnessHold : WitnessPlain;
        }

        /// <summary>True if a case was thrown out for a poor arrest: released or acquitted with a quality under <see cref="PoorAt"/>.</summary>
        public static bool ThrownOut(OfficerCase c) => (c.End == CaseEnd.Released || c.End == CaseEnd.Acquitted) && Poor(c.Quality);

        /// <summary>What cases thrown out lately take off a review's score: one point each, within <see cref="ThrownMost"/>.</summary>
        public static int ThrownPenalty(List<OfficerCase> cases, int today, int year)
        {
            int within = TalentCatalogue.Scaled(ThrownDays, year), n = 0;
            for (int i = 0; cases != null && i < cases.Count; i++)
            {
                n += ThrownOut(cases[i]) && today - cases[i].EndDay <= within ? 1 : 0;
            }
            return Math.Min(n, ThrownMost);
        }

        /// <summary>Whether an action can be done on a case now: open and charged, not worked this shift, the shift's actions not used up, and not done on it before.</summary>
        public static string ActionWhy(OfficerCase c, CaseAction action, int day, int actionsToday, int rank)
        {
            if (rank < DetectiveRank)
            {
                return "reallife_police_why_case_rank";
            }
            if (c == null || !c.Open || c.Hearing <= 0 || c.Hearing < day)
            {
                return "reallife_police_why_case_closed";
            }
            if (c.WorkedDay == day)
            {
                return "reallife_police_why_case_worked";
            }
            if (actionsToday >= ActionsMost)
            {
                return "reallife_police_why_case_actions";
            }
            return (c.Done & (1 << (int)action)) != 0 ? "reallife_police_why_case_done" : null;
        }

        /// <summary>One action done: the case's strength and the shift's count.</summary>
        public static void Work(OfficerCase c, CaseAction action, int day, int points, int rank, DutyFile f)
        {
            c.Strength = TalentCatalogue.Clamp(c.Strength + Gain(points, rank), 0, 100);
            c.Done |= 1 << (int)action;
            c.WorkedDay = day;
            f.ActionsToday++;
        }

        /// <summary>
        /// The day's turn: every charged case whose hearing day is behind is decided, once. Returns how many ended; the counts go to the file's pending notice (one a day).
        /// </summary>
        public static int Advance(DutyFile f, int day, int seed)
        {
            int ended = 0;
            for (int i = 0; i < f.Cases.Count; i++)
            {
                OfficerCase c = f.Cases[i];
                if (!c.Open || c.Hearing <= 0 || c.Hearing >= day)
                {
                    continue;
                }
                c.End = Decide(seed, c);
                c.EndDay = day;
                c.Sentence = Sentence(c.Offence, c.End);
                ended++;
                if (c.End == CaseEnd.Acquitted)
                {
                    f.PendAcquitted++;
                }
                else
                {
                    f.PendConvicted++;
                    if (Good(c.Quality))
                    {
                        f.Commendations++;
                        f.PendCommended++;
                    }
                }
            }
            return ended;
        }

        /// <summary>An arrest opens a case: charged and given its hearing, or released at once. The case goes in the book; the oldest closed go past <see cref="Most"/>.</summary>
        public static OfficerCase Open(DutyFile f, string name, int personId, string offence, string district, int day, int quality, int seed)
        {
            var c = new OfficerCase
            {
                Id = f.NextCase++, Name = name ?? string.Empty, PersonId = personId, Offence = offence, District = district ?? string.Empty, Day = day, Quality = quality, Strength = quality
            };
            f.PendOpened++;
            if (Charged(seed, day, c.Id, quality))
            {
                c.Hearing = HearingDay(day, offence);
            }
            else
            {
                c.End = CaseEnd.Released;
                c.EndDay = day;
                f.PendReleased++;
            }
            f.Cases.Add(c);
            for (int i = 0; f.Cases.Count > Most && i < f.Cases.Count; )
            {
                if (f.Cases[i].Open)
                {
                    i++;
                }
                else
                {
                    f.Cases.RemoveAt(i);
                }
            }
            return c;
        }

        /// <summary>The open cases that wait for a hearing.</summary>
        public static int OpenCount(DutyFile f)
        {
            int n = 0;
            for (int i = 0; i < f.Cases.Count; i++)
            {
                n += f.Cases[i].Open ? 1 : 0;
            }
            return n;
        }

        /// <summary>True if one notice for the day's case news is due and has not been sent; the file's counters are then taken and cleared by <see cref="TakeNotice"/>.</summary>
        public static bool NoticeDue(DutyFile f, int day) => f.NoticeDay != day && (f.PendOpened | f.PendReleased | f.PendConvicted | f.PendAcquitted) != 0;

        /// <summary>Takes the pending counts into one notice for the day and clears them: never a second notice in a day.</summary>
        public static void TakeNotice(DutyFile f, int day, out int opened, out int released, out int convicted, out int acquitted, out int commended)
        {
            opened = f.PendOpened;
            released = f.PendReleased;
            convicted = f.PendConvicted;
            acquitted = f.PendAcquitted;
            commended = f.PendCommended;
            f.PendOpened = f.PendReleased = f.PendConvicted = f.PendAcquitted = f.PendCommended = 0;
            f.NoticeDay = day;
        }
    }
}
