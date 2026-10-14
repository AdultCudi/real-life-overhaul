using System;
using System.Collections.Generic;
using System.Reflection;
using AI.Customers.CustomerEntries;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using Helpers;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The one way the mod has the game draw again the customers still to come today at a business of the player's
    /// (business pressure's footfall, the brands' and the rota planner's hours), and the one place it reads that list.
    ///
    /// The game's own redraw (CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness) throws away every customer
    /// not yet served, the hour under way included, and draws them all again. Meaningful Ambience, another author's
    /// mod, adds its customers for the hour under way once, when the hour turns: a redraw in the middle of the hour
    /// threw them away until the next. So while that mod is switched on, the customers of the hour under way are kept
    /// as they were (theirs and the game's) and only the hours after it are drawn again, unless the business is not
    /// open this hour or the game draws it no customers (then the hour is as the game leaves it: empty). Without that mod
    /// the redraw is the game's own, untouched.
    ///
    /// The list is the game's private table of every business's customers for the day, read by reflection once (the
    /// field is looked up the first time; afterwards it is a dictionary lookup). Nothing of the game's is called that
    /// the game does not itself call for the same thing. Cost: nothing per frame; a redraw is a walk over the day's
    /// customers of one business, a few hundred at most.
    /// </summary>
    public static class CustomerDraw
    {
        public static IModLogger Logger;

        /// <summary>A business's customers still to come today were drawn again (by this mod): whatever it had booked for it is gone. Raised after each redraw; a handler must not throw.</summary>
        public static event Action<BuildingRegistration> Redrawn;

        private static FieldInfo _table;
        private static bool _looked;
        private static readonly List<CustomerEntry> Kept = new List<CustomerEntry>(32);
        private static readonly HashSet<CustomerEntry> KeptSet = new HashSet<CustomerEntry>();

        /// <summary>The customers of the day at a business of the player's, the game's own list; null if the game keeps none for it.</summary>
        public static List<CustomerEntry> Entries(BuildingRegistration reg)
        {
            if (reg == null || reg.Address == null)
            {
                return null;
            }
            var table = Table();
            List<CustomerEntry> list;
            return table != null && table.TryGetValue(reg.Address, out list) ? list : null;
        }

        private static Dictionary<Address, List<CustomerEntry>> Table()
        {
            if (!_looked)
            {
                _looked = true;
                try
                {
                    _table = typeof(CustomerEntriesHelper).GetField("BusinessCustomerEntries", BindingFlags.NonPublic | BindingFlags.Static);
                    if (_table == null || _table.FieldType != typeof(Dictionary<Address, List<CustomerEntry>>))
                    {
                        _table = null;
                        Logger?.Warn("Customers: the game's list of a day's customers is not where it was; the hour under way is not kept on a redraw, and the basket adds nothing.");
                    }
                }
                catch (Exception e)
                {
                    _table = null;
                    Logger?.Error(e);
                }
            }
            return _table != null ? _table.GetValue(null) as Dictionary<Address, List<CustomerEntry>> : null;
        }

        /// <summary>The game draws again the customers still to come today at a business of the player's (see the class comment).</summary>
        public static void Redraw(BuildingRegistration reg)
        {
            if (reg == null)
            {
                return;
            }
            DayOfWeekOrdered day = TimeHelper.GetDayOfWeek();
            try
            {
                Draw(reg, day);
            }
            finally
            {
                Action<BuildingRegistration> told = Redrawn;
                if (told != null)
                {
                    try
                    {
                        told(reg);
                    }
                    catch (Exception e)
                    {
                        Logger?.Error(e);
                    }
                }
            }
        }

        private static void Draw(BuildingRegistration reg, DayOfWeekOrdered day)
        {
            if (!OtherMods.MeaningfulAmbience.Loaded)
            {
                CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness(reg, day);
                return;
            }
            Kept.Clear();
            KeptSet.Clear();
            try
            {
                int today = TimeHelper.CurrentDay, hour = TimeHelper.CurrentHour;
                List<CustomerEntry> before = Entries(reg);
                for (int i = 0; before != null && i < before.Count; i++)
                {
                    CustomerEntry e = before[i];
                    if (e != null && !e.completed && e.spawnTime != null && e.spawnTime.Day == today && e.spawnTime.Hour == hour)
                    {
                        Kept.Add(e);
                        KeptSet.Add(e);
                    }
                }
                CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness(reg, day);
                List<CustomerEntry> after = Entries(reg);
                if (after == null || Kept.Count == 0 || !BusinessHelper.IsBusinessOpen(reg, hour) || !reg.AreAllRequirementsMet())
                {
                    return;   // (a business shut now, or that the game draws no customers for, has none this hour: as the game left it)
                }
                // The hour under way as it was: what the game has just drawn for it goes, what was there comes back.
                after.RemoveAll(e => e != null && !e.completed && e.spawnTime != null && e.spawnTime.Day == today && e.spawnTime.Hour == hour && !KeptSet.Contains(e));
                for (int i = 0; i < Kept.Count; i++)
                {
                    if (!after.Contains(Kept[i]))
                    {
                        after.Add(Kept[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            finally
            {
                Kept.Clear();
                KeptSet.Clear();
            }
        }
    }
}
