using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The slots of <see cref="ModCulling"/>'s culling group, kept packed: a thing that leaves gives its slot to the last,
    /// which is looked at again a frame later, once the engine has culled it. Checked offline (NavCheck, CullCheck.cs).
    /// </summary>
    public sealed class CullBook
    {
        private int[] _idAt;        // slot -> id
        private int[] _slotOf;      // id -> slot, -1 while the id is free
        private int[] _queuedAt;    // id -> the frame it was queued in, -1 while it is not queued
        private int[] _ring;        // ids waiting for a look, first in first out
        private int[] _free;        // ids given back, to be given out again
        private int _freeCount, _head, _waiting, _ids;

        public CullBook(int capacity)
        {
            capacity = Math.Max(4, capacity);
            _idAt = new int[capacity];
            _slotOf = new int[capacity];
            _queuedAt = new int[capacity];
            _ring = new int[capacity];
            _free = new int[capacity];
            for (int i = 0; i < capacity; i++)
            {
                _slotOf[i] = -1;
                _queuedAt[i] = -1;
            }
        }

        /// <summary>Things registered: they hold slots 0 to Count - 1.</summary>
        public int Count { get; private set; }

        /// <summary>Slots there is room for before the book grows, and the sphere array with it.</summary>
        public int Capacity => _idAt.Length;

        /// <summary>Things queued for a look.</summary>
        public int Waiting => _waiting;

        /// <summary>A new thing at the last slot, queued for a look; its id.</summary>
        public int Add(int frame)
        {
            if (Count == _idAt.Length)
            {
                Grow();
            }
            int id = _freeCount > 0 ? _free[--_freeCount] : _ids++;
            _idAt[Count] = id;
            _slotOf[id] = Count;
            Count++;
            Queue(id, frame);
            return id;
        }

        /// <summary>Takes a thing out; the id of the last slot's thing, moved into its slot, or -1.</summary>
        public int Remove(int id, int frame)
        {
            int slot = SlotOf(id);
            if (slot < 0)
            {
                return -1;
            }
            int last = Count - 1;
            int moved = -1;
            if (slot != last)
            {
                moved = _idAt[last];
                _idAt[slot] = moved;
                _slotOf[moved] = slot;
                Queue(moved, frame);
            }
            Count--;
            _slotOf[id] = -1;
            _free[_freeCount++] = id;   // its place in the ring, if any, is skipped when reached
            return moved;
        }

        /// <summary>The slot of a thing, or -1.</summary>
        public int SlotOf(int id) => id >= 0 && id < _slotOf.Length ? _slotOf[id] : -1;

        /// <summary>The thing in a slot, or -1.</summary>
        public int IdAt(int slot) => slot >= 0 && slot < Count ? _idAt[slot] : -1;

        /// <summary>The engine says a slot's band changed: its thing is queued for a look.</summary>
        public void Changed(int slot, int frame)
        {
            int id = IdAt(slot);
            if (id >= 0)
            {
                Queue(id, frame);
            }
        }

        /// <summary>Every thing queued for a look: the city map opened or closed, the culling switched on.</summary>
        public void QueueAll(int frame)
        {
            for (int slot = 0; slot < Count; slot++)
            {
                Queue(_idAt[slot], frame);
            }
        }

        /// <summary>The next thing queued before <paramref name="frame"/>, passing over things taken out since; false if none.</summary>
        public bool Next(int frame, out int id)
        {
            while (_waiting > 0)
            {
                id = _ring[_head];
                if (_slotOf[id] >= 0 && _queuedAt[id] >= frame)
                {
                    break;   // queued this frame: the engine has not culled it yet
                }
                _head = (_head + 1) % _ring.Length;
                _waiting--;
                bool live = _slotOf[id] >= 0 && _queuedAt[id] >= 0;
                _queuedAt[id] = -1;
                if (live)
                {
                    return true;
                }
            }
            id = -1;
            return false;
        }

        /// <summary>Forgets every thing: the city is going.</summary>
        public void Clear()
        {
            for (int i = 0; i < _slotOf.Length; i++)
            {
                _slotOf[i] = -1;
                _queuedAt[i] = -1;
            }
            Count = 0;
            _freeCount = 0;
            _ids = 0;
            _head = 0;
            _waiting = 0;
        }

        // an id is in the ring at most once: queued again while it waits, it takes the later frame
        private void Queue(int id, int frame)
        {
            if (_queuedAt[id] >= 0)
            {
                _queuedAt[id] = Math.Max(_queuedAt[id], frame);
                return;
            }
            _queuedAt[id] = frame;
            _ring[(_head + _waiting) % _ring.Length] = id;
            _waiting++;
        }

        private void Grow()
        {
            int size = _idAt.Length * 2;
            int[] ring = new int[size];
            for (int i = 0; i < _waiting; i++)
            {
                ring[i] = _ring[(_head + i) % _ring.Length];
            }
            _ring = ring;
            _head = 0;
            Array.Resize(ref _idAt, size);
            Array.Resize(ref _free, size);
            int old = _slotOf.Length;
            Array.Resize(ref _slotOf, size);
            Array.Resize(ref _queuedAt, size);
            for (int i = old; i < size; i++)
            {
                _slotOf[i] = -1;
                _queuedAt[i] = -1;
            }
        }
    }
}
