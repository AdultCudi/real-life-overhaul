using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The work of the game's midnight that does not have to be done in the frame the game turns the day in, queued by the
    /// day handler and played afterwards, a slice of a frame at a time (Core/DayPump), in the order it was queued. A step is
    /// a function that does a piece of the work and says whether the work is finished: one that says no is called again
    /// in the next slice, so a job cut into items (a business looked at, a vacancy drawn) goes on from where it stopped
    /// and the whole job is the same as the steps run one after the other. Anything that must see the work finished
    /// (the save, the next day, the hour, the feature standing down) runs what is left at once, <see cref="Flush()"/> for
    /// everyone's or <see cref="Flush(object)"/> for one owner's; a feature that goes with its city drops its steps
    /// (<see cref="Drop"/>). A step queued in a frame is first played in a later one. Game-free: checked offline
    /// (EconomyCheck, DaySpreadChecks.cs). COST: nothing while the queue is empty; a queued step is one list entry a day.
    /// </summary>
    public sealed class DaySpread
    {
        /// <summary>The steps most plays a flush may make before it gives up on a step that never says it is finished.</summary>
        public const int FlushMost = 100000;

        private struct Item
        {
            public object Owner;
            public string Name;
            public Func<bool> Do;
            public int Frame;       // the frame it was queued in: played from the next
            public int Slot;        // the caller's number for its scope in the session record
            public bool Gone;
        }

        /// <summary>The queue the mod plays: one for the whole mod, so the steps of all features keep one order.</summary>
        public static readonly DaySpread Shared = new DaySpread();

        private readonly List<Item> _items = new List<Item>(16);
        private int _head;   // items before it are played
        private int _live;   // steps waiting

        /// <summary>Called when a step is queued on an empty queue: whatever plays the queue starts (Core/DayPump).</summary>
        public Action Wake;

        /// <summary>Called with a step's name and the exception when a step throws; that step is dropped and the rest go on.</summary>
        public Action<string, Exception> Faulted;

        /// <summary>Steps played and queued over the run, for the session record and the checks.</summary>
        public int Played, Queued, Faults;

        /// <summary>True while a step waits.</summary>
        public bool Busy => Count > 0;

        /// <summary>Steps waiting, a step that is being played again counted once.</summary>
        public int Count => _live;

        /// <summary>The name of the step to be played next; empty for none.</summary>
        public string NextName
        {
            get
            {
                Trim();
                return _head < _items.Count ? _items[_head].Name : string.Empty;
            }
        }

        /// <summary>The caller's scope number (HotSpots) of the step to be played next; -1 for none.</summary>
        public int NextSlot
        {
            get
            {
                Trim();
                return _head < _items.Count ? _items[_head].Slot : -1;
            }
        }

        /// <summary>
        /// Queues a step of an owner's, <paramref name="frame"/> being the frame it is queued in. The step is called until it
        /// returns true. Queued on an empty queue it wakes the player.
        /// </summary>
        public void Add(object owner, string name, Func<bool> step, int frame, int slot = -1)
        {
            if (step == null)
            {
                return;
            }
            bool wake = _live == 0;
            _items.Add(new Item { Owner = owner, Name = name ?? string.Empty, Do = step, Frame = frame, Slot = slot });
            _live++;
            Queued++;
            if (wake)
            {
                Wake?.Invoke();
            }
        }

        /// <summary>True if a step of the owner's waits (the owner null: any step).</summary>
        public bool Has(object owner)
        {
            for (int i = _head; i < _items.Count; i++)
            {
                if (!_items[i].Gone && (owner == null || ReferenceEquals(_items[i].Owner, owner)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True if the next step was queued before <paramref name="frame"/>: it may be played in this one.</summary>
        public bool Due(int frame)
        {
            Trim();
            return _head < _items.Count && _items[_head].Frame < frame;
        }

        /// <summary>
        /// Plays the next step, if it was queued before <paramref name="frame"/>: once, whether or not it finishes. False if
        /// none was played (the queue is empty, or its next step was queued in this very frame).
        /// </summary>
        public bool PlayOne(int frame)
        {
            Trim();
            if (_head >= _items.Count || _items[_head].Frame >= frame)
            {
                return false;
            }
            Play(_head);
            return true;
        }

        /// <summary>
        /// Plays steps from the next on while the slice has room (always one, if one is due): the player's frame. A step that is
        /// not finished stays at the head for the next slice. Returns the steps played.
        /// </summary>
        public int Run(ref WorkSlice slice, int frame)
        {
            int played = 0;
            while (true)
            {
                Trim();
                if (_head >= _items.Count || _items[_head].Frame >= frame || !slice.More())
                {
                    break;
                }
                Play(_head);
                played++;
            }
            return played;
        }

        /// <summary>Everything that waits, played now, in order, each step until it is finished. Returns the steps played.</summary>
        public int Flush()
        {
            int played = 0;
            while (played < FlushMost)
            {
                Trim();
                if (_head >= _items.Count)
                {
                    break;
                }
                Play(_head);
                played++;
            }
            return played;
        }

        /// <summary>
        /// The owner's steps played now, in the order they were queued, each until it is finished; other owners' stay.
        /// Returns the steps played.
        /// </summary>
        public int Flush(object owner)
        {
            int played = 0;
            for (int at = _head; at < _items.Count && played < FlushMost; at++)
            {
                if (_items[at].Gone || !ReferenceEquals(_items[at].Owner, owner))
                {
                    continue;
                }
                Play(at);
                played++;
                if (!_items[at].Gone)
                {
                    at--;   // not finished: the same step again
                }
            }
            Trim();
            return played;
        }

        /// <summary>The owner's steps given up without being played (its city is going; nothing is written).</summary>
        public int Drop(object owner)
        {
            int dropped = 0;
            for (int i = _head; i < _items.Count; i++)
            {
                if (!_items[i].Gone && ReferenceEquals(_items[i].Owner, owner))
                {
                    Item item = _items[i];
                    item.Gone = true;
                    _items[i] = item;
                    _live--;
                    dropped++;
                }
            }
            Trim();
            return dropped;
        }

        /// <summary>Nothing waits any more.</summary>
        public void Clear()
        {
            _items.Clear();
            _head = 0;
            _live = 0;
        }

        // One call of the step at an index: finished, or thrown, and it is gone.
        private void Play(int at)
        {
            Item item = _items[at];
            bool finished;
            try
            {
                finished = item.Do();
                Played++;
            }
            catch (Exception e)
            {
                finished = true;
                Faults++;
                Played++;
                try
                {
                    Faulted?.Invoke(item.Name, e);
                }
                catch (Exception)
                {
                    // a fault in the report of a fault is not reported
                }
            }
            if (finished && !_items[at].Gone)
            {
                item = _items[at];
                item.Gone = true;
                _items[at] = item;
                _live--;
            }
        }

        // The head moves past the steps played or dropped; the list is emptied when all are.
        private void Trim()
        {
            while (_head < _items.Count && _items[_head].Gone)
            {
                _head++;
            }
            if (_head >= _items.Count && _items.Count > 0)
            {
                _items.Clear();
                _head = 0;
            }
        }
    }
}
