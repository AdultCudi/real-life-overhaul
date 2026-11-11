using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Items;
using Entities;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The one place the seats of the tech firms touch the game's items. The game seats an employee at a workstation whose item lists
    /// one of their skills, and its desk computers list customer service for nobody (SeatRules), so a sales, support or customer
    /// service hire at an office had nowhere to sit. While the people part runs, the skills of SeatRules.ModDesk are added to each desk
    /// computer's list (the game's items with the Computer and EmployeeWorkstation types that programmers sit at), and taken off again
    /// when it stops, so with the option off the items are the game's own. Cost: one pass over the game's items when the part starts or
    /// stops, and a pass over a business's items when its page asks; nothing per frame.
    /// </summary>
    internal static class SeatGame
    {
        public static IModLogger Logger;

        private static readonly List<Item> Changed = new List<Item>(8);
        private static readonly List<string[]> Before = new List<string[]>(8);
        private static readonly List<string[]> After = new List<string[]>(8);

        /// <summary>Adds the mod's desk skills to the game's desk computers that lack them, once; returns how many items it changed. A line in the log says what was found.</summary>
        public static int Apply()
        {
            Remove();   // a second start lists the items afresh: they may be new copies
            int desks = 0, changed = 0, missing = 0;
            try
            {
                IEnumerable<Item> items = Items();
                if (items == null)
                {
                    Logger?.Warn("Tech check (desks): the game's items are not loaded yet, so the desks could not be given the customer service skill: FAIL");
                    return 0;
                }
                foreach (Item item in items)
                {
                    if (!IsDesk(item))
                    {
                        continue;
                    }
                    desks++;
                    string[] now = item.suitableSkills;
                    string[] want = SeatRules.WithModDesk(now);
                    if (want != null)
                    {
                        Changed.Add(item);
                        Before.Add(now);
                        After.Add(want);
                        item.suitableSkills = want;
                        changed++;
                    }
                    missing += SeatRules.WithModDesk(item.suitableSkills) != null ? 1 : 0;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            bool every = desks > 0 && missing == 0;
            Logger?.Info("Tech check (desks): " + desks + " desk computer item(s) in the game's items, " + changed + " given the customer service skill: " + (every ? "PASS" : "FAIL"));
            return changed;
        }

        /// <summary>Gives the desk computers their own skills back, the ones this changed that still hold the list it set. Returns how many.</summary>
        public static int Remove()
        {
            int back = 0;
            for (int i = 0; i < Changed.Count; i++)
            {
                try
                {
                    if (Changed[i] != null && ReferenceEquals(Changed[i].suitableSkills, After[i]))
                    {
                        Changed[i].suitableSkills = Before[i];
                        back++;
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            Changed.Clear();
            Before.Clear();
            After.Clear();
            return back;
        }

        /// <summary>
        /// A business's placed workstations by the skills they list, read once in a pass over its items: how many desks each list of skills stands for (the
        /// game's items share one list among all of a kind). Asked of each of thousands of professionals on a page, it is a few comparisons for each, with
        /// nothing made; a pass over the items with a delegate made for each would be millions of both.
        /// </summary>
        public sealed class Desks
        {
            private readonly List<string[]> _lists = new List<string[]>(8);
            private readonly List<int> _counts = new List<int>(8);

            /// <summary>Counts the business's placed workstations. Nothing is left if they cannot be read.</summary>
            public void Read(BuildingRegistration business)
            {
                _lists.Clear();
                _counts.Clear();
                try
                {
                    Dictionary<string, ItemInstance> placed = business != null ? business.itemInstances : null;
                    if (placed == null)
                    {
                        return;
                    }
                    foreach (ItemInstance placedItem in placed.Values)
                    {
                        Item item = placedItem != null ? placedItem.ItemCached : null;
                        if (item == null || !item.assignable || item.suitableSkills == null)
                        {
                            continue;
                        }
                        int at = _lists.IndexOf(item.suitableSkills);
                        if (at < 0)
                        {
                            _lists.Add(item.suitableSkills);
                            _counts.Add(1);
                        }
                        else
                        {
                            _counts[at]++;
                        }
                    }
                }
                catch (Exception e)
                {
                    _lists.Clear();
                    _counts.Clear();
                    Logger?.Error(e);
                }
            }

            /// <summary>How many of the workstations list a skill the employee has.</summary>
            public int For(EmployeeInstance employee)
            {
                int n = 0;
                for (int i = 0; employee != null && i < _lists.Count; i++)
                {
                    string[] skills = _lists[i];
                    for (int k = 0; k < skills.Length; k++)
                    {
                        if (employee.HasSkill(skills[k]))
                        {
                            n += _counts[i];
                            break;
                        }
                    }
                }
                return n;
            }
        }

        // The game's items, or null before they are loaded: a small method of its own, since it names members a game update could move.
        private static IEnumerable<Item> Items() => ItemsGetter.AllItems;

        // A desk computer: an assignable item that is both a computer and a workstation, which programmers sit at.
        private static bool IsDesk(Item item)
        {
            const ItemType Desk = ItemType.Computer | ItemType.EmployeeWorkstation;
            return item != null && item.assignable && (item.type & Desk) == Desk && item.suitableSkills != null && Array.IndexOf(item.suitableSkills, TalentCatalogue.Programmer) >= 0;
        }
    }
}
