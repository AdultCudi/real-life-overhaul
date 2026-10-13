using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>Where a freelance contract stands: the saved number of <see cref="Contract.Stage"/>.</summary>
    public enum ContractStep
    {
        Offered = 0, Taken = 1, Done = 2, Late = 3, Lapsed = 4, Dropped = 5
    }

    /// <summary>A piece of freelance work for a client of the city. Game-free.</summary>
    public sealed class Contract
    {
        public int Id, Prof, Rank;
        public string Client = string.Empty;
        public string Street = string.Empty;   // where it is done, for a trade: the client's building
        public int Number;
        public int Hours, Minutes;             // asked for, and worked so far (minutes)
        public long Fee;                       // whole money at the start's prices
        public int Due;                        // the day it is due
        public int Lapse;                      // an offer not taken lapses on this day
        public int Stage;                      // ContractStep
        public bool Practice;                  // came to the player's own practice

        public ContractStep At => (ContractStep)Stage;

        public Profession Def => ProCatalogue.Find(Prof);
    }

    /// <summary>The player's own practice in a profession. Game-free.</summary>
    public sealed class Practice
    {
        public int Prof = -1;
        public int Opened;
        public int Reputation = OwnRules.PracticeStart;   // 0 to 100
        public int Served, Late;

        public bool Open => Prof >= 0;
    }

    /// <summary>
    /// The rules of working for oneself, in plain numbers: leaving to found a startup or open a practice, what a founder's
    /// record is worth to the venture firms, bringing former colleagues along, a restraint clause and the claim a former
    /// employer files over it, freelance contracts, side work and an exclusive contract, and a practice's name. Game-free:
    /// EconomyCheck runs every one.
    /// </summary>
    public static class OwnRules
    {
        public const int FoundLeastRank = 1;        // the second rung up may leave to found a company
        public const int ColleaguesMost = 3;
        public const int ColleaguesDays = 30;       // days the colleagues wait for the player's firm
        public const int RestraintDays = 120;       // a restraint clause binds this long after leaving, for a year of 365 days
        public const int ClaimWeeks = 8;            // a former employer's claim: weeks of the player's last pay...
        public const int ClaimWeeksPerColleague = 2;   // ...and more for each colleague taken
        public const int ClaimColleagues = 700;     // per mille: a claim when colleagues were taken under a clause
        public const int ClaimSameField = 350;      // ...when the player works in the same field for themselves
        public const int FounderMost = 150;         // thousandths a founder's record adds to a pitch's start (VentureMeeting's 500)

        public const int FreelanceBp = 13000;       // a contract pays this much of the player's rate an hour (basis points)
        public const int PracticeBp = 14000;        // ...more at the player's own practice
        public const int OfficeBp = 1000;           // ...and more again from an office of the player's own
        public const int LateCutBp = 3000;          // a late delivery is paid this much less
        public const int ContractsMost = 6;         // offers and contracts at once
        public const int ContractDaysLeast = 6, ContractDaysMost = 14;
        public const int OfferDays = 3;
        public static readonly int[] HoursOf = { 8, 12, 16, 24, 32 };   // a contract's size
        public const int SittingMinutes = 180;      // a stint of work on a contract
        public const int SideWorkFound = 80;        // per mille a stint of side work under an exclusive contract is noticed
        public const int OfferChance = 250;         // per mille a day a freelancer is offered a contract...
        public const int PracticeChance = 450;      // ...and a practice, by its name

        public const int PracticeStart = 20;
        public const int PracticeOnTime = 3, PracticeLate = -6;
        public static int RegisterFee(Profession p) => p.Id == ProCatalogue.Lawyer ? 1500 : p.Id == ProCatalogue.Accountant ? 900 : p.Id == ProCatalogue.Architect || p.Id == ProCatalogue.CivilEngineer ? 700 : 300;

        // ---------------------------------------------------------------- leaving to found

        /// <summary>True for a profession whose people can leave to found a tech company.</summary>
        public static bool CanFound(Profession p)
        {
            return p.Family == (int)Tech.Family.Engineering || p.Family == (int)Tech.Family.Data || p.Family == (int)Tech.Family.Security || p.Family == (int)Tech.Family.Design
                   || p.Family == (int)Tech.Family.Product || p.Family == (int)Tech.Family.Support;
        }

        /// <summary>
        /// True for a profession that can be practised on one's own (a practice and its clients): law, accountancy,
        /// architecture, engineering, design, marketing, HR, the trades. The regulated ones need their certificate.
        /// </summary>
        public static bool CanPractise(Profession p, StudyFile f)
        {
            switch (p.Id)
            {
                case ProCatalogue.Lawyer: return HasCert(f, 8);
                case ProCatalogue.Accountant: return HasCert(f, 6);
                case ProCatalogue.Architect:
                case ProCatalogue.CivilEngineer: return HasCert(f, 7);
                case ProCatalogue.Designer:
                case ProCatalogue.Marketer:
                case ProCatalogue.HumanResources:
                case ProCatalogue.Developer:
                case ProCatalogue.Electrician:
                case ProCatalogue.Plumber:
                case ProCatalogue.Carpenter:
                    return ProRules.Qualified(f, p, Math.Min(1, p.Ranks.Length - 1));
                default: return false;
            }
        }

        private static bool HasCert(StudyFile f, int cert) => (StudyRules.Certs(f) & (1 << cert)) != 0;

        /// <summary>
        /// What a founder's record adds to a pitch, thousandths (VentureMeeting starts a pitch at 500): twenty for each rung
        /// reached in a tech profession, ten a year of it, three a point of the player's name; at most <see cref="FounderMost"/>.
        /// </summary>
        public static int FounderScore(int topRank, int yearsDays, int reputation)
        {
            int s = Math.Max(0, topRank) * 20 + Math.Max(0, yearsDays) / 365 * 10 + reputation * 3;
            return TalentCatalogue.Clamp(s, 0, FounderMost);
        }

        /// <summary>The claim a former employer files over a broken restraint clause, whole money at the start's prices.</summary>
        public static long ClaimFor(long weekPayCents, int colleagues)
        {
            return weekPayCents * (ClaimWeeks + ClaimWeeksPerColleague * Math.Max(0, colleagues)) / 100;
        }

        /// <summary>The chance (per mille) a former employer files a claim: for colleagues taken, or for work in the same field.</summary>
        public static int ClaimChance(bool colleagues) => colleagues ? ClaimColleagues : ClaimSameField;

        // ---------------------------------------------------------------- freelance contracts

        /// <summary>A contract's fee, whole money at the start's prices: the hours at the rank's rate, by the freelance premium, a practice's and an office's.</summary>
        public static long Fee(int rateCents, int hours, bool practice, bool office)
        {
            long bp = practice ? PracticeBp : FreelanceBp;
            bp += office ? OfficeBp : 0;
            return (long)rateCents * hours * bp / 10000 / 100;
        }

        /// <summary>What a contract delivered is paid: the fee, less for a late one.</summary>
        public static long Paid(Contract c, int day)
        {
            return day > c.Due ? c.Fee * (10000 - LateCutBp) / 10000 : c.Fee;
        }

        /// <summary>A practice's name after a delivery: up for one on time, down for a late one.</summary>
        public static void Delivered(Practice p, bool late)
        {
            if (p == null || !p.Open)
            {
                return;
            }
            p.Reputation = TalentCatalogue.Clamp(p.Reputation + (late ? PracticeLate : PracticeOnTime), 0, 100);
            p.Served++;
            p.Late += late ? 1 : 0;
        }

        /// <summary>The chance (per mille) of a contract offer this day: a practice's by its name.</summary>
        public static int OfferChanceOf(Practice p)
        {
            return p != null && p.Open ? PracticeChance + p.Reputation * 3 : OfferChance;
        }

        /// <summary>The live contracts and offers.</summary>
        public static int Live(List<Contract> all)
        {
            int n = 0;
            for (int i = 0; i < all.Count; i++)
            {
                n += all[i].Stage <= (int)ContractStep.Taken ? 1 : 0;
            }
            return n;
        }

        /// <summary>Minutes of a contract still to work.</summary>
        public static int Left(Contract c) => Math.Max(0, c.Hours * 60 - c.Minutes);
    }
}
