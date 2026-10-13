using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Fame
{
    // Game-free, like FameRules: RealLifeOverhaul.Build/EconomyCheck compiles it as it is.

    /// <summary>Where the player answers questions: what the papers make of it is so many times a street interview's.</summary>
    public enum FameVenue
    {
        Street = 0,
        Magazine = 1,
        Show = 2
    }

    /// <summary>What a reporter or a host asks.</summary>
    public enum FameQuestion
    {
        Rise = 0,            // how the player got where they are
        Plans = 1,           // what comes next
        Hard = 2,            // the weakest part of their reputation
        Promise = 3          // will they promise the city something
    }

    /// <summary>An invitation's kind. Saved as a number: new ones go at the end.</summary>
    public enum FameInviteKind
    {
        Show = 0,            // the night show, Late Lights
        Magazine = 1,        // an interview for Avenue magazine
        Investors = 2,       // an evening with investors and other owners
        CityEvent = 3        // a reception at City Hall
    }

    /// <summary>Where an invitation stands. Saved as a number.</summary>
    public enum FameInviteState
    {
        Offered = 0,
        Accepted = 1,
        Done = 2,
        Missed = 3,
        Declined = 4,
        Lapsed = 5
    }

    /// <summary>What the player promised the city. Saved as a number: new ones go at the end.</summary>
    public enum FamePromiseKind
    {
        Jobs = 0,            // three more jobs
        Wages = 1,           // a better name as an employer
        Prices = 2,          // a better name with the customers
        Charity = 3          // a gift to charity
    }

    public enum FamePromiseState
    {
        Open = 0,
        Kept = 1,
        Broken = 2
    }

    /// <summary>A piece the City Ledger prints about the player. Each is one locale key (FameText.StoryKeys). New ones go at the end.</summary>
    public enum FameStory
    {
        None = 0,
        Shops2 = 1, Shops5 = 2, Shops10 = 3, Shops20 = 4, Property1 = 5, Property5 = 6,
        Conditions = 7,      // business empire faces criticism over employee conditions
        Trust = 8,           // shoppers lose patience
        Jobs = 9,            // residents praise new jobs
        Rents = 10,          // rents under fire
        InterviewGood = 11, InterviewHard = 12, Show = 13, ShowBad = 14, Magazine = 15,
        PromiseKept = 16, PromiseBroken = 17,
        Refused = 18,        // walked past somebody who needed help
        Sponsor = 19, Charity = 20, Rescue = 21, Renovation = 22, LobbyWon = 23, LobbyLost = 24, Investigation = 25, Partnership = 26, Event = 27
    }

    /// <summary>An issue of business pressure's whose share of the deal Standing moves.</summary>
    public enum FameIssue
    {
        Complaint = 0,
        Praise = 1,
        Inspection = 2,
        Audit = 3
    }

    /// <summary>An invitation: to the night show, a magazine, an evening with investors, a reception.</summary>
    public sealed class FameInvite
    {
        public int Id;
        public FameInviteKind Kind;
        public int Offered;                  // the day it came
        public int AnswerBy;                 // the last day to accept it
        public int Day, Hour;                // when it is
        public FameInviteState State;
    }

    /// <summary>A promise made in an interview, judged on its day.</summary>
    public sealed class FamePromise
    {
        public int Id;
        public FamePromiseKind Kind;
        public int Made, Due;
        public int Goal;                     // the figure to reach: staff, or a reputation
        public FamePromiseState State;
        public FameCause Source;             // where it was made: an interview, the show, a magazine
    }

    /// <summary>One question of an interview: what it is, about which part (a hard one), and the promise it would be.</summary>
    public struct FameAsk
    {
        public FameQuestion Question;
        public FamePart Topic;
        public FamePromiseKind Promise;
    }

    /// <summary>What an answer does: tenths of a point of recognition, up to two parts moved, and whether it makes the promise asked for.</summary>
    public struct FameAnswer
    {
        public int Press;
        public FamePart PartA, PartB;
        public int MoveA, MoveB;
        public bool Promises;
        public bool Good;                    // the papers take it well
    }

    /// <summary>
    /// The press by rule: reporters and a photographer in the street, invitations to the night show and to a magazine,
    /// the questions asked and what each answer does, promises and their day, the papers' pieces about the player, and
    /// what their standing does to business: customers, complaints and inspections, hiring. Game-free.
    ///
    /// WHAT AN INTERVIEW BRINGS (tenths of a point of recognition, before the venue: a street interview once, a magazine
    /// one and a half times, the night show two and a half times):
    ///
    ///   question     first answer                         second answer
    ///   the rise     promote the business: 20, customers  modest, credit the staff: 12, workers and integrity
    ///   plans        grow: 18, the neighbourhoods         keep it good first: 10, the customers
    ///   a hard one   honest: 8, the part and integrity +2 deflect: 14, the part -1 and integrity -2
    ///   a promise    make it: 14, integrity +1            none: 4
    ///
    /// A promise is judged on its day, four weeks on (or kept early): kept, integrity +4 and the papers say so; broken,
    /// integrity -6 and the papers say that too.
    /// </summary>
    public static class FamePress
    {
        public const int InvitesMost = 8, PromisesMost = 6, PromisesOpenMost = 3;

        // ---- the street
        public const int ReporterFrom = 35;        // recognition: reporters come from Known on...
        public const int ReporterStep = 2;         // ...per mille an hour for each point over 30
        public const int ReporterGapDays = 5;
        public const int PhotoFrom = 45;
        public const int PhotoStep = 2;
        public const int PhotoGapDays = 3;
        public const int PhotoPress = 5;           // tenths
        public static readonly int[] VenueTimes = { 2, 3, 5 };   // halves: street, magazine, show

        // ---- invitations
        public const int ShowFrom = 55, MagazineFrom = 40;     // recognition
        public const int ShowChance = 250, MagazineChance = 200;   // per mille a day once one may come
        public const int InviteGapDays = 12;
        public const int InviteAhead = 3;          // days between the invitation and the day itself
        public const int AnswerDays = 2;           // days to accept it
        public const int ShowHour = 21, MagazineHour = 15;
        public const int LateHours = 1;            // the player may be this late; then it is missed
        public const int MissedPress = -10;
        public const int MissedIntegrity = -1;

        // ---- promises
        public const int PromiseDays = 28;
        public const int JobsPromised = 3;
        public const int RepPromised = 5;
        public const int KeptIntegrity = 4, BrokenIntegrity = -6;
        public const int KeptPress = 10, BrokenPress = -10;

        // ---- the papers' pieces
        public const int StoryFrom = 35;           // recognition: the papers write about somebody Known
        public const int StoryGapDays = 30;        // the same kind of piece again
        public const int StoryPress = 5;
        public const int BadFrom = 30;             // a part under this is criticised
        public const int PraiseFrom = 70, PraiseStaff = 10;   // the neighbourhoods at this, with this many jobs, are praised
        public static readonly int[] ShopMilestones = { 2, 5, 10, 20 };
        public static readonly int[] PropertyMilestones = { 1, 5 };

        // ---- what standing does to business
        public const int FootfallMost = 5;         // per cent of a business's customers either way, inside business pressure's own bounds
        public const int IssueLeast = 600, IssueMost = 1800;   // thousandths of an issue's share of the deal
        public const int ScrutinyStep = 3;         // thousandths more inspections for each point of recognition
        public const int EsteemStep = 5;           // a point of esteem as an employer for every 5 of the workers' reputation over or under 50

        // ---- help refused
        public const int RefusedIntegrity = -3, ReportedIntegrity = -2;
        public const int ReportedBase = 350, ReportedStep = 3, ReportedMost = 750;   // per mille, more for somebody better known
        public const int ReportedPress = 5;

        // ---------------------------------------------------------------- the street

        /// <summary>The chance, per mille, that a reporter comes up to the player out of doors in an hour.</summary>
        public static int ReporterChance(int recognition)
        {
            return recognition < ReporterFrom ? 0 : (recognition - 30) * ReporterStep;
        }

        public static int PhotoChance(int recognition)
        {
            return recognition < PhotoFrom ? 0 : (recognition - 40) * PhotoStep;
        }

        public static bool ReporterMay(FameFile f, int day)
        {
            return day - f.ReporterDay >= ReporterGapDays;
        }

        public static bool PhotoMay(FameFile f, int day)
        {
            return day - f.PhotoDay >= PhotoGapDays;
        }

        // ---------------------------------------------------------------- an interview

        /// <summary>The weakest part of the reputation if it is under 50 (a hard question is about it); -1 if none is.</summary>
        public static int Weakest(FameFile f)
        {
            int weakest = -1;
            for (int i = 0; i < 4; i++)
            {
                if (f.Rep[i] < FameRules.Start && (weakest < 0 || f.Rep[i] < f.Rep[weakest]))
                {
                    weakest = i;
                }
            }
            return weakest;
        }

        /// <summary>The promise that would answer the player's weakest point as an owner: the workers, the customers, or more jobs.</summary>
        public static FamePromiseKind PromiseFor(FameFile f)
        {
            int workers = f.Rep[(int)FamePart.Workers], customers = f.Rep[(int)FamePart.Customers];
            if (workers < FameRules.Start && workers <= customers)
            {
                return FamePromiseKind.Wages;
            }
            return customers < FameRules.Start ? FamePromiseKind.Prices : FamePromiseKind.Jobs;
        }

        /// <summary>
        /// The questions of an interview, into the list given: in the street the rise or the plans, then a hard one if a part
        /// is weak, else a promise; a magazine and the night show the rise, a hard one (or the plans), and a promise.
        /// </summary>
        public static void Questions(FameFile f, FameVenue venue, int seed, List<FameAsk> into)
        {
            into.Clear();
            int weakest = Weakest(f);
            FamePromiseKind promise = PromiseFor(f);
            bool plans = (seed & 1) == 0;
            if (venue == FameVenue.Street)
            {
                into.Add(new FameAsk { Question = plans ? FameQuestion.Plans : FameQuestion.Rise });
                into.Add(weakest >= 0 ? new FameAsk { Question = FameQuestion.Hard, Topic = (FamePart)weakest } : new FameAsk { Question = FameQuestion.Promise, Promise = promise });
                return;
            }
            into.Add(new FameAsk { Question = FameQuestion.Rise });
            into.Add(weakest >= 0 ? new FameAsk { Question = FameQuestion.Hard, Topic = (FamePart)weakest } : new FameAsk { Question = FameQuestion.Plans });
            into.Add(new FameAsk { Question = FameQuestion.Promise, Promise = promise });
        }

        /// <summary>What an answer (0 or 1) to a question does, at a venue. See the table at the top.</summary>
        public static FameAnswer Answer(FameAsk ask, int option, FameVenue venue)
        {
            var a = new FameAnswer();
            bool first = option == 0;
            switch (ask.Question)
            {
                case FameQuestion.Rise:
                    a.Press = first ? 20 : 12;
                    a.PartA = first ? FamePart.Customers : FamePart.Workers;
                    a.MoveA = 1;
                    if (!first)
                    {
                        a.PartB = FamePart.Integrity;
                        a.MoveB = 1;
                    }
                    a.Good = true;
                    break;
                case FameQuestion.Plans:
                    a.Press = first ? 18 : 10;
                    a.PartA = first ? FamePart.Neighbourhood : FamePart.Customers;
                    a.MoveA = 1;
                    a.Good = true;
                    break;
                case FameQuestion.Hard:
                    a.Press = first ? 8 : 14;
                    a.Good = first;
                    if (ask.Topic == FamePart.Integrity)
                    {
                        a.PartA = FamePart.Integrity;
                        a.MoveA = first ? 3 : -3;
                    }
                    else
                    {
                        a.PartA = ask.Topic;
                        a.MoveA = first ? 1 : -1;
                        a.PartB = FamePart.Integrity;
                        a.MoveB = first ? 2 : -2;
                    }
                    break;
                default:
                    a.Press = first ? 14 : 4;
                    a.PartA = FamePart.Integrity;
                    a.MoveA = first ? 1 : 0;
                    a.Promises = first;
                    a.Good = true;
                    break;
            }
            a.Press = a.Press * VenueTimes[FameRules.Clamp((int)venue, 0, VenueTimes.Length - 1)] / 2;
            return a;
        }

        /// <summary>An answer given: the papers and the parts, each inside the day's most. Returns the tenths of recognition it brought.</summary>
        public static int Apply(FameFile f, FameAnswer a, FameCause cause, int day)
        {
            int press = FameRules.Publicity(f, a.Press, day, cause);
            if (a.MoveA != 0)
            {
                FameRules.Move(f, a.PartA, cause, a.MoveA, day);
            }
            if (a.MoveB != 0)
            {
                FameRules.Move(f, a.PartB, cause, a.MoveB, day);
            }
            return press;
        }

        // ---------------------------------------------------------------- promises

        /// <summary>A promise made: its goal from where the player stands today, judged in four weeks. Null if three are open already or one of the kind is.</summary>
        public static FamePromise Promise(FameFile f, FamePromiseKind kind, FameCause source, int day)
        {
            int open = 0;
            for (int i = 0; i < f.Promises.Count; i++)
            {
                if (f.Promises[i].State == FamePromiseState.Open)
                {
                    open++;
                    if (f.Promises[i].Kind == kind)
                    {
                        return null;
                    }
                }
            }
            if (open >= PromisesOpenMost)
            {
                return null;
            }
            int goal;
            switch (kind)
            {
                case FamePromiseKind.Jobs: goal = f.Staff + JobsPromised; break;
                case FamePromiseKind.Wages: goal = Math.Min(100, f.Rep[(int)FamePart.Workers] + RepPromised); break;
                case FamePromiseKind.Prices: goal = Math.Min(100, f.Rep[(int)FamePart.Customers] + RepPromised); break;
                default: goal = day; break;   // a gift to charity on this day or after
            }
            var p = new FamePromise { Id = f.NextId++, Kind = kind, Made = day, Due = day + PromiseDays, Goal = goal, Source = source };
            Keep(f.Promises, PromisesMost, x => x.State != FamePromiseState.Open);
            f.Promises.Add(p);
            return p;
        }

        /// <summary>True once a promise's goal is reached. A gift to charity counts from the day it was made (charityDay: the last gift).</summary>
        public static bool Reached(FameFile f, FamePromise p, int charityDay)
        {
            switch (p.Kind)
            {
                case FamePromiseKind.Jobs: return f.Staff >= p.Goal;
                case FamePromiseKind.Wages: return f.Rep[(int)FamePart.Workers] >= p.Goal;
                case FamePromiseKind.Prices: return f.Rep[(int)FamePart.Customers] >= p.Goal;
                default: return charityDay >= p.Goal;
            }
        }

        /// <summary>An open promise looked at today: kept as soon as it is reached, broken on its day if it is not. What it is now.</summary>
        public static FamePromiseState Judge(FameFile f, FamePromise p, int day, int charityDay)
        {
            if (p.State != FamePromiseState.Open)
            {
                return p.State;
            }
            if (Reached(f, p, charityDay))
            {
                p.State = FamePromiseState.Kept;
                FameRules.Move(f, FamePart.Integrity, FameCause.PromiseKept, KeptIntegrity, day);
                FameRules.Publicity(f, KeptPress, day, FameCause.PromiseKept);
            }
            else if (day >= p.Due)
            {
                p.State = FamePromiseState.Broken;
                FameRules.Move(f, FamePart.Integrity, FameCause.PromiseBroken, BrokenIntegrity, day);
                FameRules.Publicity(f, BrokenPress, day, FameCause.PromiseBroken);
            }
            return p.State;
        }

        // ---------------------------------------------------------------- invitations

        /// <summary>
        /// Whether an invitation comes today, from a roll of 0 to 999: the night show from Prominent on, a magazine from 40,
        /// none while one is open, twelve days apart. Made and listed if so; null if not.
        /// </summary>
        public static FameInvite InviteRoll(FameFile f, int day, int roll)
        {
            if (Open(f) != null || day - f.InviteDay < InviteGapDays)
            {
                return null;
            }
            FameInviteKind kind;
            if (f.Recognition >= ShowFrom && roll < ShowChance)
            {
                kind = FameInviteKind.Show;
            }
            else if (f.Recognition >= MagazineFrom && roll < MagazineChance)
            {
                kind = FameInviteKind.Magazine;
            }
            else
            {
                return null;
            }
            return Offer(f, kind, day, kind == FameInviteKind.Show ? ShowHour : MagazineHour);
        }

        /// <summary>An invitation of a kind for a day three days on at an hour, listed.</summary>
        public static FameInvite Offer(FameFile f, FameInviteKind kind, int day, int hour)
        {
            var invite = new FameInvite { Id = f.NextId++, Kind = kind, Offered = day, AnswerBy = day + AnswerDays, Day = day + InviteAhead, Hour = hour, State = FameInviteState.Offered };
            Keep(f.Invites, InvitesMost, x => x.State != FameInviteState.Offered && x.State != FameInviteState.Accepted);
            f.Invites.Add(invite);
            f.InviteDay = day;
            return invite;
        }

        /// <summary>The invitation waiting for an answer or for its day, or null.</summary>
        public static FameInvite Open(FameFile f)
        {
            for (int i = 0; i < f.Invites.Count; i++)
            {
                if (f.Invites[i].State == FameInviteState.Offered || f.Invites[i].State == FameInviteState.Accepted)
                {
                    return f.Invites[i];
                }
            }
            return null;
        }

        /// <summary>An invitation's hour: true if it starts now (accepted, its hour, the player free). Missed or lapsed if its time has gone.</summary>
        public static bool Due(FameFile f, FameInvite invite, int day, int hour, bool free)
        {
            if (invite.State == FameInviteState.Offered && day > invite.AnswerBy)
            {
                invite.State = FameInviteState.Lapsed;
                return false;
            }
            if (invite.State != FameInviteState.Accepted)
            {
                return false;
            }
            int now = day * 24 + hour, from = invite.Day * 24 + invite.Hour;
            if (now >= from && now <= from + LateHours && free)
            {
                return true;
            }
            if (now > from + LateHours)
            {
                invite.State = FameInviteState.Missed;
                FameRules.Publicity(f, MissedPress, day, invite.Kind == FameInviteKind.Show ? FameCause.Show : invite.Kind == FameInviteKind.Magazine ? FameCause.Magazine : FameCause.Event);
                FameRules.Move(f, FamePart.Integrity, FameCause.Event, MissedIntegrity, day);
            }
            return false;
        }

        private static void Keep<T>(List<T> list, int most, Predicate<T> old)
        {
            while (list.Count >= most)
            {
                int at = list.FindIndex(old);
                list.RemoveAt(at >= 0 ? at : 0);
            }
        }

        // ---------------------------------------------------------------- the papers' pieces

        /// <summary>True if the papers may print a piece about the player today (one a day). Takes it if so.</summary>
        public static bool StoryMay(FameFile f, int day)
        {
            if (f.StoryDay == day)
            {
                return false;
            }
            f.StoryDay = day;
            return true;
        }

        /// <summary>
        /// A milestone of the player's rise the papers have not told of yet: a second, fifth, tenth, twentieth business, a
        /// first and a fifth building. What there was when the file was first looked at is no news. None if nothing new.
        /// </summary>
        public static FameStory Milestone(FameFile f)
        {
            bool seen = f.MilestonesSeen;
            f.MilestonesSeen = true;
            for (int i = 0; i < ShopMilestones.Length + PropertyMilestones.Length; i++)
            {
                bool reached = i < ShopMilestones.Length ? f.Businesses >= ShopMilestones[i] : f.Properties >= PropertyMilestones[i - ShopMilestones.Length];
                if (!reached || (f.Milestones & (1 << i)) != 0)
                {
                    continue;
                }
                f.Milestones |= 1 << i;
                if (seen)
                {
                    return i < ShopMilestones.Length ? (FameStory)((int)FameStory.Shops2 + i) : (FameStory)((int)FameStory.Property1 + i - ShopMilestones.Length);   // one a day: the next one tomorrow
                }
            }
            return FameStory.None;
        }

        /// <summary>
        /// A piece about how the player is thought of, once they are Known: criticism over the staff's conditions or the
        /// customers' patience, the rents in a district, praise for the jobs. Thirty days apart each; what it says moves
        /// its part a little. None if nothing is due.
        /// </summary>
        public static FameStory Criticism(FameFile f, int day, out string district)
        {
            district = null;
            if (f.Recognition < StoryFrom)
            {
                return FameStory.None;
            }
            if (f.Rep[(int)FamePart.Workers] < BadFrom && f.Staff > 0 && day - f.ConditionsDay >= StoryGapDays)
            {
                f.ConditionsDay = day;
                FameRules.Move(f, FamePart.Workers, FameCause.Reported, -2, day);
                return FameStory.Conditions;
            }
            if (f.Rep[(int)FamePart.Customers] < BadFrom && f.Businesses > 0 && day - f.TrustDay >= StoryGapDays)
            {
                f.TrustDay = day;
                FameRules.Move(f, FamePart.Customers, FameCause.Reported, -2, day);
                return FameStory.Trust;
            }
            for (int i = 0; i < f.Districts.Count; i++)
            {
                FameDistrict d = f.Districts[i];
                if (d.Raised > 0 && d.Opinion < BadFrom && day - f.RentsDay >= StoryGapDays)
                {
                    f.RentsDay = day;
                    district = d.Name;
                    FameRules.Move(f, FamePart.Neighbourhood, FameCause.Reported, -2, day, d.Name);
                    return FameStory.Rents;
                }
            }
            if (f.Rep[(int)FamePart.Neighbourhood] >= PraiseFrom && f.Staff >= PraiseStaff && day - f.JobsDay >= StoryGapDays)
            {
                f.JobsDay = day;
                FameRules.Move(f, FamePart.Neighbourhood, FameCause.Reported, 2, day);
                return FameStory.Jobs;
            }
            return FameStory.None;
        }

        // ---------------------------------------------------------------- what standing does to business

        /// <summary>
        /// The customers a business gains or loses by the player's standing, per cent: the customers' reputation (a point
        /// for every ten over or under 50) and the district's opinion (one for every twenty), as far as the district knows
        /// the player (40% of it for an owner nobody knows, all of it for one everybody does); 5% at most either way.
        /// </summary>
        public static int Footfall(FameFile f, FameDistrict d)
        {
            int customers = f.Rep[(int)FamePart.Customers], opinion = d != null ? d.Opinion : FameRules.Start, known = d != null ? d.Known : FameRules.DistrictKnown(f.Recognition, 0, 0);
            double percent = ((customers - FameRules.Start) / 10.0 + (opinion - FameRules.Start) / 20.0) * (40 + 60 * FameRules.Clamp(known, 0, 100) / 100.0) / 100.0;
            return FameRules.Clamp((int)Math.Floor(percent + 0.5), -FootfallMost, FootfallMost);
        }

        /// <summary>
        /// An issue's share of business pressure's deal, thousandths: complaints by the customers and the neighbourhoods,
        /// praise by the customers, inspections and audits by integrity, and more of them for somebody well known.
        /// </summary>
        public static int IssueScale(FameFile f, FameIssue issue)
        {
            int customers = f.Rep[(int)FamePart.Customers], neighbourhood = f.Rep[(int)FamePart.Neighbourhood], integrity = f.Rep[(int)FamePart.Integrity];
            int scale;
            switch (issue)
            {
                case FameIssue.Complaint: scale = 1000 + (FameRules.Start - (2 * customers + neighbourhood) / 3) * 10; break;
                case FameIssue.Praise: scale = 1000 + (customers - FameRules.Start) * 10; break;
                default: scale = 1000 + (FameRules.Start - integrity) * 10 + f.Recognition * ScrutinyStep; break;
            }
            return FameRules.Clamp(scale, IssueLeast, IssueMost);
        }

        /// <summary>What it is like to work for the player as the city tells it, -10 to 10, for the company's hiring and its rivals.</summary>
        public static int Esteem(FameFile f)
        {
            return FameRules.Clamp((f.Rep[(int)FamePart.Workers] - FameRules.Start) / EsteemStep, -10, 10);
        }

        // ---------------------------------------------------------------- help refused

        /// <summary>True the first time today: walking past somebody who needs help is counted once a day at most. Takes it if so.</summary>
        public static bool RefusalMay(FameFile f, int day)
        {
            if (f.RefusalDay == day)
            {
                return false;
            }
            f.RefusalDay = day;
            return true;
        }

        /// <summary>
        /// The player walked past somebody who needed help, and somebody saw: integrity -3, and from a roll of 0 to 999 it is
        /// in the paper (35% and 0.3% more a point of recognition, 75% at most): integrity -2 more. True if it is reported.
        /// </summary>
        public static bool Refused(FameFile f, int day, int roll)
        {
            FameRules.Move(f, FamePart.Integrity, FameCause.Refused, RefusedIntegrity, day);
            if (roll >= Math.Min(ReportedMost, ReportedBase + ReportedStep * f.Recognition))
            {
                return false;
            }
            FameRules.Move(f, FamePart.Integrity, FameCause.Reported, ReportedIntegrity, day);
            FameRules.Publicity(f, ReportedPress, day, FameCause.Reported);
            return true;
        }
    }
}
