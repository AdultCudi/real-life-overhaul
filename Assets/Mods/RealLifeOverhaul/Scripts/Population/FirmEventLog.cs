using System.Collections.Generic;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The firms' doings, kept so a story is told once and about the firm that did it: a business that opened (its name and its day), and a business that moved
    /// its prices. The newest <see cref="Most"/> are kept, oldest first; one firm's one doing on one day is kept once. Saved with the population. Game-free.
    /// </summary>
    public sealed class FirmEventLog
    {
        public const int Most = 24;
        private readonly List<FirmEvent> _events = new List<FirmEvent>(Most);

        public int Count => _events.Count;

        public FirmEvent this[int index] => _events[index];

        /// <summary>Adds a doing. False for no name, a day under 1, or the same firm's same doing on the same day.</summary>
        public bool Add(FirmEventKind kind, Owner owner, int day, int value, string name)
        {
            if (string.IsNullOrEmpty(name) || day < 1)
            {
                return false;
            }
            for (int i = 0; i < _events.Count; i++)
            {
                FirmEvent had = _events[i];
                if (had.Kind == kind && had.Day == day && had.Name == name)
                {
                    return false;
                }
            }
            if (_events.Count >= Most)
            {
                _events.RemoveAt(0);
            }
            _events.Add(new FirmEvent { Kind = kind, Owner = owner, Day = day, Value = value, Name = name });
            return true;
        }

        /// <summary>The doings of a kind on the days from <paramref name="since"/> up to, not including, <paramref name="day"/>, oldest first, into <paramref name="into"/>.</summary>
        public void Between(FirmEventKind kind, int since, int day, List<FirmEvent> into)
        {
            into.Clear();
            for (int i = 0; i < _events.Count; i++)
            {
                FirmEvent e = _events[i];
                if (e.Kind == kind && e.Day >= since && e.Day < day)
                {
                    into.Add(e);
                }
            }
        }
    }
}
