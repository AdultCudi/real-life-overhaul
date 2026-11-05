using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The runs of a page: what each press does, by the index the buttons carry. A page is written with a new list, so an index never outlives its page.</summary>
    public sealed class TechRuns
    {
        private TechRun[] _items = new TechRun[16];
        private int _count;

        public int Count => _count;

        public int Add(TechRun run)
        {
            if (_count == _items.Length)
            {
                Array.Resize(ref _items, _count * 2);
            }
            _items[_count] = run;
            return _count++;
        }

        public TechRun[] ToArray()
        {
            var copy = new TechRun[_count];
            Array.Copy(_items, copy, _count);
            return copy;
        }
    }
}
