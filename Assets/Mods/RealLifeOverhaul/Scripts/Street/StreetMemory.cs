using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Street
{
    /// <summary>
    /// The day's memory of who the player talked to: at most <see cref="StreetRules.MemoryMost"/> people, the one talked to
    /// longest ago going first, all forgotten the next day. Not saved. Game-free. COST: a look-up per conversation.
    /// </summary>
    public sealed class StreetMemory
    {
        private readonly Dictionary<int, StreetMemo> _memos = new Dictionary<int, StreetMemo>(StreetRules.MemoryMost);
        private readonly List<int> _order = new List<int>(StreetRules.MemoryMost);
        private int _day = -1;

        public int Count => _memos.Count;

        /// <summary>The memory of somebody today; null if they have not met the player today.</summary>
        public StreetMemo Find(int key, int day)
        {
            NewDay(day);
            return _memos.TryGetValue(key, out StreetMemo m) ? m : null;
        }

        /// <summary>The memory of somebody today, made when they first talk to the player.</summary>
        public StreetMemo Get(int key, int day)
        {
            NewDay(day);
            if (_memos.TryGetValue(key, out StreetMemo m))
            {
                _order.Remove(key);
                _order.Add(key);
                return m;
            }
            if (_memos.Count >= StreetRules.MemoryMost && _order.Count > 0)
            {
                _memos.Remove(_order[0]);
                _order.RemoveAt(0);
            }
            m = new StreetMemo { Key = key, Day = day };
            _memos[key] = m;
            _order.Add(key);
            return m;
        }

        public void Clear()
        {
            _memos.Clear();
            _order.Clear();
            _day = -1;
        }

        private void NewDay(int day)
        {
            if (day != _day)
            {
                _memos.Clear();
                _order.Clear();
                _day = day;
            }
        }
    }

    /// <summary>One business told about: by how much, and from which game day.</summary>
    public struct StreetTold
    {
        public string Address;     // the game's own "street~number"
        public int Day;            // the day it was told
        public int Percent;
    }

    /// <summary>
    /// Word of mouth for the player's businesses: who said they would go, capped per business and per day, each lasting
    /// <see cref="StreetRules.MouthDays"/> days. Saved by StreetStore. Game-free.
    /// </summary>
    public sealed class StreetMouth
    {
        public readonly List<StreetTold> Told = new List<StreetTold>(8);
        public int CountedDay = -1;
        public int Counted;

        /// <summary>Somebody will go to the business at this address: true if it counted, under the day's and the business's caps.</summary>
        public bool Tell(string address, int day)
        {
            if (string.IsNullOrEmpty(address))
            {
                return false;
            }
            Forget(day);
            if (CountedDay != day)
            {
                CountedDay = day;
                Counted = 0;
            }
            if (Counted >= StreetRules.MouthPerDay || Percent(address, day) + StreetRules.MouthPercent > StreetRules.MouthMost)
            {
                return false;
            }
            Counted++;
            Told.Add(new StreetTold { Address = address, Day = day, Percent = StreetRules.MouthPercent });
            return true;
        }

        /// <summary>Per cent more customers at this address today: what was told within the last days, capped.</summary>
        public int Percent(string address, int day)
        {
            int sum = 0;
            for (int i = 0; i < Told.Count; i++)
            {
                StreetTold t = Told[i];
                if (t.Address == address && day >= t.Day && day - t.Day < StreetRules.MouthDays)
                {
                    sum += t.Percent;
                }
            }
            return Math.Min(StreetRules.MouthMost, sum);
        }

        /// <summary>Drops what has run its days.</summary>
        public void Forget(int day)
        {
            for (int i = Told.Count - 1; i >= 0; i--)
            {
                if (day - Told[i].Day >= StreetRules.MouthDays || Told[i].Day > day)
                {
                    Told.RemoveAt(i);
                }
            }
        }
    }
}
