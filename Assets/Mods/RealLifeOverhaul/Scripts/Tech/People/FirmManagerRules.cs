using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The figures of a tech firm's manager: what naming one costs and what the post does. Thousandths and basis points unless named; days are the game's.</summary>
    public static class FirmManagerCatalogue
    {
        public const int RiseBp = 1000;             // the pay rise that comes with the post, basis points (one rise: it rests the next for WageRules' period)
        public const int OwnWorkShare = 700;        // thousandths of their own output they keep while they manage: the rest of their hours is the team's
        public const int ReportDays = 7;            // days between the summary notices
        public const int TargetsMost = 6;           // role targets a post keeps
        public const int VacanciesMost = 8;         // people who left whose place the manager means to fill
        public const int VacancyDays = 45;          // days a vacancy is kept before the manager lets it go
        public const int AdsMost = 2;               // ads the manager keeps open at once
        public const int RetryDays = 5;             // days before the manager tries again after an ad could not be paid for
        // the routine raise a manager agrees alone: what a review of rating 4 or 5 asks for. Above it, or the player's own limit when higher, it waits for the player
        public const int RaiseCeilingBp = 600;
        public const int HaggleMost = 150;          // thousandths a top manager takes off the raise a person asked for
        public const int WornFrom = 500;            // burnout, thousandths, from which a skilled manager flags somebody as wearing thin
        public const int WornSlack = 5;             // thousandths of burnout a manager is slower to see for each point of skill under SeesThroughAt
        public const int IdleDays = 4;              // daily runs in a row with no hours worked that flag somebody as idle (a weekend is two)
        public const int SeesThroughAt = 60;        // skill from which a manager judges a CV by the true skill, not the one it claims
        public const int ChiefBonus = 8;            // skill points a chief technology officer or engineering manager has for the work
        public const int ChoiceCeilingBp = 11000;   // a manager offers an applicant their ask but not past this share of the going rate (basis points)
        public const int SkillPerSpan = 40;         // skill points a move a day more: 1 move at 0, 2 at 40, 3 at 80
        public const int SpanMost = 4;

        public const int DutyBalance = 1, DutyHire = 2, DutyRaises = 4, DutyWatch = 8;
        public const int DutiesAll = DutyBalance | DutyHire | DutyRaises | DutyWatch;
    }

    /// <summary>A role the manager keeps this many of at the firm.</summary>
    public sealed class ManagerTarget
    {
        public int Role, Count;
    }

    /// <summary>Somebody who left and whose place the manager means to fill: their role and level, and the day they went.</summary>
    public struct Vacancy
    {
        public int Role, Level, Day;
    }

    /// <summary>
    /// The player's named manager of one firm (a business of theirs): who, since when, which of the four duties are on, the role targets they keep, the places they mean to
    /// fill, the ads they have open and the week's tally. Saved in the people part's line (part g).
    /// </summary>
    public sealed class ManagerPost
    {
        public string Place = string.Empty, Id = string.Empty;   // the business (a place key) and the manager's file
        public int Since, LastDay = -1, ReportDay;               // the day they were named, the last day they ran the firm, the last day they reported
        public int Duties = FirmManagerCatalogue.DutiesAll;
        public int WMoves, WRaises, WAds, WOffers, WWorn, WIdle; // this week's tally: role moves, raises agreed, ads posted, offers made, those flagged wearing thin, those flagged idle
        public readonly List<ManagerTarget> Targets = new List<ManagerTarget>(FirmManagerCatalogue.TargetsMost);
        public readonly List<Vacancy> Vacancies = new List<Vacancy>(4);
        public readonly List<int> Ads = new List<int>(FirmManagerCatalogue.AdsMost);
        // read or kept for the session, never saved
        public int RetryDay;
        public string Name = string.Empty, Last = string.Empty;   // the manager's name as read, and what they did last (the Team page's line)
        public readonly List<string> Worn = new List<string>(3), Idle = new List<string>(3);   // who they flagged on their last run (names), the first few

        public bool Has(int duty) => (Duties & duty) != 0;

        public int TargetOf(int role)
        {
            for (int i = 0; i < Targets.Count; i++)
            {
                if (Targets[i].Role == role)
                {
                    return Targets[i].Count;
                }
            }
            return -1;
        }

        /// <summary>A target set (or changed); a count of 0 takes it away. False when the post keeps as many as it can.</summary>
        public bool SetTarget(int role, int count)
        {
            role = TalentCatalogue.Clamp(role, 0, TalentCatalogue.RoleCount - 1);
            count = TalentCatalogue.Clamp(count, 0, 999);
            for (int i = 0; i < Targets.Count; i++)
            {
                if (Targets[i].Role == role)
                {
                    if (count == 0)
                    {
                        Targets.RemoveAt(i);
                    }
                    else
                    {
                        Targets[i].Count = count;
                    }
                    return true;
                }
            }
            if (count == 0)
            {
                return true;
            }
            if (Targets.Count >= FirmManagerCatalogue.TargetsMost)
            {
                return false;
            }
            Targets.Add(new ManagerTarget { Role = role, Count = count });
            return true;
        }

        public void ClearWeek()
        {
            WMoves = WRaises = WAds = WOffers = WWorn = WIdle = 0;
        }

        public bool QuietWeek => WMoves + WRaises + WAds + WOffers + WWorn + WIdle == 0;
    }

    /// <summary>One applicant the manager may offer: what their CV claims, what they truly can do, what they ask and the going rate for the post.</summary>
    public struct ManagerApplicant
    {
        public int Id, Claimed, True, Ask, Going;
    }

    /// <summary>
    /// What a tech firm's named manager does and how well. Skill is theirs: the main skill (the game's own, for the work) and, where the role has the Leadership
    /// specialism, that score too; a chief technology officer or an engineering manager has the training for it. The skill sets how many role moves they make a day (the span), how
    /// much they take off a raise they agree, and whether they judge applicants by the true skill or the one a CV claims. Every decision is arithmetic on the people and
    /// the post, with no dice, so a reload and every machine decide alike. Game-free: EconomyCheck runs it. Cost: a few comparisons a person, once a day.
    /// </summary>
    public static class FirmManagerRules
    {
        /// <summary>The manager's skill for the work, 0 to 100: the main skill, with the Leadership score blended in where the role has it, and the training of a chief.</summary>
        public static int Skill(Profile p, int mainPoints)
        {
            int main = TalentCatalogue.Clamp(mainPoints, 0, 100);
            int lead = p.ScoreOf(TalentCatalogue.Leadership);
            int skill = lead >= 0 ? (main + TalentCatalogue.Clamp(lead, 0, 100)) / 2 : main;
            if (Chief(p.Role))
            {
                skill += FirmManagerCatalogue.ChiefBonus;
            }
            return TalentCatalogue.Clamp(skill, 0, 100);
        }

        /// <summary>True for the roles whose job is running a team: a chief technology officer and an engineering manager.</summary>
        public static bool Chief(int role) => role == TalentCatalogue.Cto || role == TalentCatalogue.EngineeringManager;

        /// <summary>Role moves a day: one more for each forty points of skill, and one more for a chief.</summary>
        public static int Span(int skill, int role)
        {
            return TalentCatalogue.Clamp(1 + skill / FirmManagerCatalogue.SkillPerSpan + (Chief(role) ? 1 : 0), 1, FirmManagerCatalogue.SpanMost);
        }

        /// <summary>What a manager of this skill takes off a raise they agree, thousandths of it.</summary>
        public static int Haggle(int skill) => FirmManagerCatalogue.HaggleMost * TalentCatalogue.Clamp(skill, 0, 100) / 100;

        /// <summary>The most a manager agrees alone, basis points: a routine raise, or the player's own limit for agreeing a request by itself when that is higher.</summary>
        public static int Ceiling(int playerLimitBp) => Math.Max(FirmManagerCatalogue.RaiseCeilingBp, Math.Max(0, playerLimitBp));

        /// <summary>
        /// The raise a manager gives a person who asked for one, basis points, or 0 where it waits for the player: the person was raised lately (the period of one raise is the rule's),
        /// or asks for more than the ceiling. A skilled manager takes a little off; never under a hundred (a raise of nothing would settle a request with an insult).
        /// </summary>
        public static int Raise(int skill, int askBp, int ceilingBp, bool rested)
        {
            if (askBp <= 0 || rested || askBp > ceilingBp)
            {
                return 0;
            }
            return Math.Max(Math.Min(askBp, 100), (int)((long)askBp * (1000 - Haggle(skill)) / 1000));
        }

        /// <summary>True if a rival's offer, as basis points over the pay, is within what the manager matches alone (the ceiling): a bigger one is the player's to answer.</summary>
        public static bool Match(int offerBp, int ceilingBp) => offerBp > 0 && offerBp <= ceilingBp;

        /// <summary>
        /// The applicant a manager offers: of those whose ask is within the going rate's ceiling, the best by what they judge them by (the true skill from
        /// <see cref="FirmManagerCatalogue.SeesThroughAt"/> points of skill, else the skill a CV claims), the lower ask next, the id last. -1 for none.
        /// </summary>
        public static int Pick(int skill, IList<ManagerApplicant> applicants)
        {
            int best = -1;
            bool sees = skill >= FirmManagerCatalogue.SeesThroughAt;
            for (int i = 0; i < applicants.Count; i++)
            {
                ManagerApplicant a = applicants[i];
                if (a.Going > 0 && (long)a.Ask * 10000 > (long)a.Going * FirmManagerCatalogue.ChoiceCeilingBp)
                {
                    continue;
                }
                if (best < 0)
                {
                    best = i;
                    continue;
                }
                ManagerApplicant b = applicants[best];
                int sa = sees ? a.True : a.Claimed, sb = sees ? b.True : b.Claimed;
                if (sa > sb || sa == sb && (a.Ask < b.Ask || a.Ask == b.Ask && a.Id < b.Id))
                {
                    best = i;
                }
            }
            return best;
        }

        /// <summary>True if a counter-offer is within the going rate's ceiling, so the manager takes it.</summary>
        public static bool TakeCounter(int counter, int going) => going > 0 && (long)counter * 10000 <= (long)going * FirmManagerCatalogue.ChoiceCeilingBp;

        /// <summary>The burnout, in thousandths, from which a manager of this skill sees somebody wearing thin: from 500 for a skilled one, later for a weaker.</summary>
        public static int WornLine(int skill) => FirmManagerCatalogue.WornFrom + Math.Max(0, FirmManagerCatalogue.SeesThroughAt - TalentCatalogue.Clamp(skill, 0,
            100)) * FirmManagerCatalogue.WornSlack;

        /// <summary>True if somebody is wearing thin by a manager's eye: burnout, in thousandths, at their line.</summary>
        public static bool WearingThin(int burnout, int skill) => burnout >= WornLine(skill);

        /// <summary>True if somebody has had no hours for as many daily runs in a row as the flag wants.</summary>
        public static bool Idle(int runsWithoutHours) => runsWithoutHours >= FirmManagerCatalogue.IdleDays;

        /// <summary>A vacancy still worth filling on a day: not older than the days they are kept for.</summary>
        public static bool Fresh(Vacancy v, int day) => day - v.Day <= FirmManagerCatalogue.VacancyDays;

        /// <summary>The vacancies a manager remembers, the newest kept when there are more than the post holds; one more is added for somebody who left.</summary>
        public static void Left(ManagerPost post, int role, int level, int day)
        {
            while (post.Vacancies.Count >= FirmManagerCatalogue.VacanciesMost)
            {
                post.Vacancies.RemoveAt(0);
            }
            post.Vacancies.Add(new Vacancy { Role = role, Level = level, Day = day });
        }

        /// <summary>
        /// The roles the manager is short of against the post's targets, as (role, how many short) pairs in the order the targets were set, given how many people hold each role now.
        /// <paramref name="have"/> is by role id.
        /// </summary>
        public static int Short(ManagerPost post, int[] have, List<ManagerTarget> into)
        {
            into.Clear();
            for (int i = 0; i < post.Targets.Count; i++)
            {
                ManagerTarget t = post.Targets[i];
                int now = t.Role >= 0 && t.Role < have.Length ? have[t.Role] : 0;
                if (now < t.Count)
                {
                    into.Add(new ManagerTarget { Role = t.Role, Count = t.Count - now });
                }
            }
            return into.Count;
        }

        /// <summary>
        /// Whether a person of a role is spare for the balancing: their role has no target, or more people hold it than its target. Nobody is taken from a role that is at or under its
        /// target, so the balancing cannot move people back and forth.
        /// </summary>
        public static bool Spare(ManagerPost post, int role, int[] have)
        {
            int t = post.TargetOf(role);
            return t < 0 || (role >= 0 && role < have.Length && have[role] > t);
        }
    }
}
