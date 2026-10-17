using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    // What running a business keeps of a save, and no rules. Game-free: EconomyCheck compiles this file.

    /// <summary>One employee as the mod knows them, by the game's own id for the employee.</summary>
    public sealed class StaffFile
    {
        public string Id;
        public string Place;            // the business they work at, as business pressure writes a place
        public int Standing = CompanyCatalogue.StandingStart;
        public int Raise;               // agreed through the mod, hundredths an hour on top of the game's wage
        public int UnderDays;           // days running underpaid
        public int AskDay;              // the day a raise was asked for; 0 with none waiting
        public int AskTo;               // the hourly pay asked for, hundredths
        public int AskRest;             // no new request before this day
        public int PoachDay;            // the day a rival's offer came; 0 with none waiting
        public int PoachOffer;          // what the rival pays an hour, hundredths
        public int PoachRest;           // no new offer before this day
        public int LeaveDay;            // the day they go; 0 while they stay
        public string Rival = string.Empty;

        // Read from the game; never saved.
        public string Name = string.Empty;
        public int Wage;                // the game's hourly wage at today's prices, hundredths
        public int Going;               // the going rate for them, hundredths an hour
        public int Hours;               // hundredths of an hour worked on the day that has ended
        public int DayHours;            // hundredths of an hour in one of their working days
        public int Skill;               // the game's value of their main skill
        public int Mood;                // the game's satisfaction, 0 to 100
        public bool Sick;               // off sick: on the day's pass, the day that has ended
        public bool Seen;               // found in the game on the last read

        /// <summary>What they are paid an hour, hundredths: the game's wage and the agreed raise.</summary>
        public int Paid => Wage + Raise;
    }

    /// <summary>One policy on one business. An insurer of -1 is no policy.</summary>
    public sealed class Policy
    {
        public int Insurer = -1;
        public int Premium;             // a day, fixed for the term
        public int From;                // the day it began
        public int Until;               // the last day of the term
        public int Claims;              // claims put in during this term
        public int Missed;              // days running the premium could not be taken
        public int Declined;            // insurers that will not write this cover here again, a bit each

        public bool Active => Insurer >= 0;
    }

    /// <summary>One licence of one business.</summary>
    public sealed class Licence
    {
        public int Until;               // the last day it is good for; 0 with none
        public bool Auto;               // renewed by itself on the day after it runs out
        public bool Reminded;           // this term's reminder has gone out
    }

    /// <summary>One of the player's businesses as the mod knows it, by its place.</summary>
    public sealed class ShopFile
    {
        public string Place;
        public int FirstDay;            // the day the mod first saw it
        public int Hide;                // per cent of takings kept off the books
        public readonly Policy[] Cover = { new Policy(), new Policy(), new Policy() };
        public readonly Licence[] Permits = { new Licence(), new Licence(), new Licence(), new Licence() };

        public int Push = -1;           // the campaign running or booked: its kind; -1 with none
        public int PushFrom;            // its first day
        public int PushEnd;             // the first day without it
        public int PushCost;            // what was paid for it
        public int PushPercent;         // per cent of customers it adds
        public readonly int[] PushEnded = new int[CompanyCatalogue.CampaignKinds];   // the first day without the last of each kind; 0 for never

        public int StrikeFrom;          // its first day; 0 with none called or running
        public int StrikeEnd;           // the first day back at work
        public int StrikeRest;          // no new strike before this day
        public int OvertimeAsk;         // the day staff offered overtime; 0 with no offer waiting
        public int OvertimeFrom;        // the first day of agreed overtime
        public int OvertimeEnd;         // the first day without it
        public int OvertimeRest;        // no new offer before this day
        public int OvertimeCost;        // a day
        public int LossRun;             // days running the game's statement showed a loss

        public bool Seen;               // found in the game on the last read; never saved

        public bool Striking(int day) => StrikeFrom > 0 && day >= StrikeFrom && day < StrikeEnd;
        public bool StrikeCalled(int day) => StrikeFrom > 0 && day < StrikeFrom;
        public bool Pushing(int day) => Push >= 0 && day >= PushFrom && day < PushEnd;
        public bool Overtime(int day) => OvertimeFrom > 0 && day >= OvertimeFrom && day < OvertimeEnd;
    }

    /// <summary>Where a claim stands. Saved as a number.</summary>
    public enum ClaimStage
    {
        Open = 0,       // the loss is covered and the claim can be put in
        Filed = 1,      // put in: paid on PayDay
        Paid = 2,
        Lapsed = 3      // not put in within the days allowed
    }

    /// <summary>One loss a policy covers.</summary>
    public sealed class Claim
    {
        public int Id;
        public string Place;
        public string Name = string.Empty;  // the business's name when it happened
        public int Kind;
        public int Insurer;
        public int Day;                     // the day of the loss
        public int Loss;
        public int Excess;
        public int Payout;
        public ClaimStage Stage;
        public int PayDay;
    }

    /// <summary>What a campaign did, by the game's own figures; the game may keep fewer days than it ran.</summary>
    public sealed class CampaignReport
    {
        public string Name = string.Empty;
        public int Kind;
        public int From;
        public int Days;
        public int DaysBefore, DaysDuring;  // days the game had figures for
        public int CustomersBefore, CustomersDuring;
        public int TakingsBefore, TakingsDuring;
        public int ProfitBefore, ProfitDuring;
        public int Cost;

        /// <summary>The extra profit: the campaign's days against as many days before, less its cost.</summary>
        public int Extra => ProfitDuring - ProfitBefore - Cost;

        /// <summary>The return on what was spent, per cent; 0 for a campaign that cost nothing.</summary>
        public int Return => Cost > 0 ? (int)Math.Max(-9999L, Math.Min(9999L, (long)Extra * 100 / Cost)) : 0;

        /// <summary>The change in customers, per cent; 0 with none to compare with.</summary>
        public int Footfall => CustomersBefore > 0 ? (int)Math.Max(-999L, Math.Min(9999L, ((long)CustomersDuring - CustomersBefore) * 100 / CustomersBefore)) : 0;
    }

    /// <summary>Tax: the quarter, the return that waits, the accountant, and the cash kept off the books.</summary>
    public sealed class TaxFile
    {
        public int QuarterFrom;         // the first day of the running quarter
        public int Quarter = 1;         // its number, from 1
        public readonly int[] Costs = new int[(int)CostKind.Count];   // the mod's business costs this quarter, by kind
        public int ReturnDue;           // the last day to file the waiting return; 0 with none
        public int ReturnQuarter;
        public int ReturnTakings;
        public int ReturnCosts;
        public int ReturnPenalties;     // late penalties charged on it so far
        public int LateRun;             // returns filed late or not at all, running
        public int Accountant = -1;     // which is hired; -1 for none
        public int AccountantNext;      // the day the next month's fee is taken
        public int Risk;                // the chance of an audit a day, ten-thousandths
        public int Saved;               // tax not paid on hidden takings since the last audit
        public int Owed;                // back-tax and fines found and not yet paid
        public int LastAudit;           // the day of the last audit; 0 for never
        public int CaseNumber;          // the court's number for an audit's case; 0 for none

        public bool ReturnWaiting => ReturnDue > 0;
    }

    /// <summary>What running a business keeps for a save.</summary>
    public sealed class CompanyState
    {
        public int Seed;
        public int FirstDay;            // the day the mod first ran in the save
        public int LastDay;             // the game's day last counted
        public int Age;                 // days counted
        public int SickPolicy = CompanyCatalogue.SickPolicyDefault;
        public int PoachRest;           // no rival approaches anybody before this day
        public int NextClaim = 1;
        public readonly TaxFile Tax = new TaxFile();
        public readonly List<ShopFile> Shops = new List<ShopFile>(8);
        public readonly List<StaffFile> Staff = new List<StaffFile>(32);
        public readonly List<Claim> Claims = new List<Claim>(4);
        public readonly List<CampaignReport> Reports = new List<CampaignReport>(CompanyCatalogue.ReportsKept);

        public ShopFile Shop(string place)
        {
            for (int i = 0; i < Shops.Count; i++)
            {
                if (string.Equals(Shops[i].Place, place, StringComparison.Ordinal))
                {
                    return Shops[i];
                }
            }
            return null;
        }

        public StaffFile Employee(string id)
        {
            for (int i = 0; i < Staff.Count; i++)
            {
                if (string.Equals(Staff[i].Id, id, StringComparison.Ordinal))
                {
                    return Staff[i];
                }
            }
            return null;
        }

        public Claim ClaimOf(int id)
        {
            for (int i = 0; i < Claims.Count; i++)
            {
                if (Claims[i].Id == id)
                {
                    return Claims[i];
                }
            }
            return null;
        }
    }
}
