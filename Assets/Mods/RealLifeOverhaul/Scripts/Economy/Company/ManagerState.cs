using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    // What the managers keep of a save, and no rules. Game-free: EconomyCheck compiles this file.

    /// <summary>A product's stock at a place as last looked at, so the next look can tell how much was used in between.</summary>
    public sealed class StockSnap
    {
        public string Item = string.Empty;
        public int Stock;               // units on site
        public int Week;                // the game's count of units its contracts brought this week, then
    }

    /// <summary>
    /// One manager: of a place (a business, a warehouse or a factory, by its place), with the player's limits as steps
    /// of <see cref="ManagerCatalogue"/>'s arrays, or of a district (a regional manager, working from a headquarters),
    /// who looks over the place managers there and stands in for one off sick with the place's own limits.
    /// </summary>
    public sealed class ManagerFile
    {
        public string Place = string.Empty;     // the place, as business pressure writes one; a district for a regional manager
        public string Id = string.Empty;        // the game's id for the employee
        public int Role;                        // ManagerCatalogue.RolePlace or RoleRegion
        public string Base = string.Empty;      // a regional manager's headquarters; empty for a place's manager
        public int From;                        // the day appointed
        public int Tasks;                       // the kinds of decision given, a bit each (ManagerTask)
        public int WageBand = ManagerCatalogue.WageBandDefault;
        public int StockDays = ManagerCatalogue.StockDaysDefault;
        public int PriceLow = ManagerCatalogue.PriceLowDefault;
        public int PriceHigh = ManagerCatalogue.PriceHighDefault;
        public int Budget = ManagerCatalogue.BudgetDefault;
        public int Keep = -1;                   // staff hiring keeps the place at; -1 until hiring is first given
        public int Spent;                       // of this month's budget
        public int Decided, Mistakes, Undone, Paid;     // this month: decisions, mistakes among them, undone by the player, allowance
        public int LastDecided, LastMistakes, LastUndone, LastPaid, LastSpent;   // the month before
        public int SnapDay, SnapHour;           // when the stock was last looked at; 0 for never
        public readonly List<StockSnap> Snaps = new List<StockSnap>(8);
        public readonly List<string> Held = new List<string>(2);   // products the player has taken back from the manager

        public string Name = string.Empty;      // as last read from the game

        // Read from the game; never saved.
        public int Skill;
        public bool Seen;                       // the employee was found where they should be on the last read

        public bool Region => Role == ManagerCatalogue.RoleRegion;

        /// <summary>The month is over: its figures become last month's and the new one starts from nothing, its budget whole.</summary>
        public void EndMonth()
        {
            LastDecided = Decided;
            LastMistakes = Mistakes;
            LastUndone = Undone;
            LastPaid = Paid;
            LastSpent = Spent;
            Decided = Mistakes = Undone = Paid = Spent = 0;
        }

        public bool Holds(string item)
        {
            for (int i = 0; i < Held.Count; i++)
            {
                if (string.Equals(Held[i], item, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        public StockSnap Snap(string item)
        {
            for (int i = 0; i < Snaps.Count; i++)
            {
                if (string.Equals(Snaps[i].Item, item, StringComparison.Ordinal))
                {
                    return Snaps[i];
                }
            }
            return null;
        }
    }

    /// <summary>
    /// One decision a manager made, with what it changed and what was there before, so that it can be undone. The six
    /// figures mean what the kind says they mean (CompanyDirector.Managers.cs, the Undo of each kind).
    /// </summary>
    public sealed class Decision
    {
        public int Id;
        public int Day, Hour;
        public string Place = string.Empty;     // where, as the manager that made it keeps it
        public int Task;                        // ManagerTask
        public string Subject = string.Empty;   // the employee's id, a claim's number, a contract's wholesaler; empty for the place
        public string Who = string.Empty;       // the manager's name when they made it
        public int A, B, C, D, E, F;
        public bool Mistake;
        public DecisionState State;
        public string Detail = string.Empty;    // products: "item*before*after/item*before*after"; or a name

        public bool Open => State == DecisionState.Done;
    }

    /// <summary>One product a decision changed: its figure before and after (units ordered, or cents of a price).</summary>
    public struct ItemChange
    {
        public string Item;
        public int Before, After;
    }

    /// <summary>What the managers keep for a save.</summary>
    public sealed class ManagerState
    {
        public int MonthFrom;                   // the first day of the month the reports count
        public int NextId = 1;
        public int Today;                       // decisions made since the last notice of them
        public readonly List<ManagerFile> Managers = new List<ManagerFile>(8);
        public readonly List<Decision> Decisions = new List<Decision>(32);

        public ManagerFile At(string place)
        {
            for (int i = 0; i < Managers.Count; i++)
            {
                if (string.Equals(Managers[i].Place, place, StringComparison.Ordinal))
                {
                    return Managers[i];
                }
            }
            return null;
        }

        /// <summary>The manager who is this employee, if any.</summary>
        public ManagerFile Of(string id)
        {
            for (int i = 0; i < Managers.Count; i++)
            {
                if (string.Equals(Managers[i].Id, id, StringComparison.Ordinal))
                {
                    return Managers[i];
                }
            }
            return null;
        }

        public Decision Find(int id)
        {
            for (int i = Decisions.Count - 1; i >= 0; i--)
            {
                if (Decisions[i].Id == id)
                {
                    return Decisions[i];
                }
            }
            return null;
        }

        /// <summary>
        /// The latest decision of a kind at a place on one subject that was not undone, with what the decisions on it last
        /// set of each product in <paramref name="into"/> (newer over older); null with none.
        /// </summary>
        public Decision LastSet(ManagerTask task, string place, string subject, Dictionary<string, int> into, List<ItemChange> scratch)
        {
            into.Clear();
            Decision last = null;
            for (int i = 0; i < Decisions.Count; i++)
            {
                Decision d = Decisions[i];
                if (d.Task != (int)task || d.Place != place || d.Subject != subject || d.State == DecisionState.Undone)
                {
                    continue;
                }
                last = d;
                ManagerStore.Items(d.Detail, scratch);
                for (int k = 0; k < scratch.Count; k++)
                {
                    into[scratch[k].Item] = scratch[k].After;
                }
            }
            return last;
        }

        /// <summary>The latest decision of a kind at a place on one subject, whatever became of it; null with none.</summary>
        public Decision LastOf(ManagerTask task, string place, string subject)
        {
            for (int i = Decisions.Count - 1; i >= 0; i--)
            {
                Decision d = Decisions[i];
                if (d.Task == (int)task && d.Place == place && d.Subject == subject)
                {
                    return d;
                }
            }
            return null;
        }

        /// <summary>A new decision at the end of the list, the oldest let go past the number kept.</summary>
        public Decision Add(int day, int hour, string place, ManagerTask task, string subject, string who)
        {
            if (Decisions.Count >= ManagerCatalogue.DecisionsKept)
            {
                Decisions.RemoveAt(0);
            }
            var d = new Decision { Id = NextId++, Day = day, Hour = hour, Place = place ?? string.Empty, Task = (int)task, Subject = subject ?? string.Empty, Who = who ?? string.Empty };
            Decisions.Add(d);
            Today++;
            return d;
        }
    }
}
