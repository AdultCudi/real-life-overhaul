using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The money's changes as the status bar shows them, merged as the game's top bar merges its floating texts
    /// (Topbar.AddMoneyTransaction): every change of one sign within one minute of the game's clock is one change. Fed the
    /// money once a frame (one comparison while it stands still), it queues each change and hands the next one out when
    /// the last has finished rising (UiHudRules.ChangeTime). Game-free: the HUD feeds it the save's money and the clock.
    /// Cost: a comparison a frame; nothing allocated after it is made.
    /// </summary>
    public sealed class UiMoneyFlow
    {
        public const int Most = 8;                // changes waiting at once; an older one is merged into the last rather than lost
        public const double Least = 0.5;          // money: a change smaller than half a cent is the float's noise, not a payment

        private readonly double[] _amount = new double[Most];
        private readonly int[] _minute = new int[Most];
        private int _first, _count;
        private double _last;
        private bool _seen;

        /// <summary>Changes waiting to be shown.</summary>
        public int Waiting => _count;

        /// <summary>Forgets everything: a new save, so its money is not read as a change.</summary>
        public void Reset()
        {
            _first = _count = 0;
            _seen = false;
        }

        /// <summary>The money now and the game's clock in minutes: queues the change since the last feed, merged with the last waiting one of the same sign and minute.</summary>
        public void Feed(double money, int minute)
        {
            if (!_seen)
            {
                _seen = true;
                _last = money;
                return;
            }
            double change = money - _last;
            if (Math.Abs(change) < Least)
            {
                return;
            }
            _last = money;
            if (_count > 0)
            {
                int tail = (_first + _count - 1) % Most;
                if (_minute[tail] == minute && Math.Sign(_amount[tail]) == Math.Sign(change))
                {
                    _amount[tail] += change;
                    return;
                }
            }
            if (_count == Most)
            {
                int tail = (_first + _count - 1) % Most;
                _amount[tail] += change;   // a flood of changes: the last one carries the rest, so the sum stays true
                return;
            }
            int at = (_first + _count) % Most;
            _amount[at] = change;
            _minute[at] = minute;
            _count++;
        }

        /// <summary>Takes the next change to show; false when none waits.</summary>
        public bool Next(out double amount)
        {
            if (_count == 0)
            {
                amount = 0;
                return false;
            }
            amount = _amount[_first];
            _first = (_first + 1) % Most;
            _count--;
            return true;
        }
    }
}
