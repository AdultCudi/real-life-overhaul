using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The names of the buttons on the game's two phones: read in one search when a pair of phones is first seen, then kept by
    /// the mod's own installs and removals, so an app asks by name and no install searches a whole phone. Game-free; a set
    /// look-up an app.
    /// </summary>
    public sealed class PhoneNames
    {
        private readonly HashSet<string> _names = new HashSet<string>(StringComparer.Ordinal);
        private int _phone, _menu;
        private bool _read;

        /// <summary>Searches of the phones in the session: one for each pair seen.</summary>
        public int Searches { get; private set; }

        /// <summary>Names asked in the session: one for each app that asked to go on.</summary>
        public int Asked { get; private set; }

        /// <summary>True if this pair of phones (their instance numbers) has been read.</summary>
        public bool Read(int phone, int menu)
        {
            return _read && _phone == phone && _menu == menu;
        }

        /// <summary>Forgets every name for a new pair of phones, whose buttons are then learnt from one search.</summary>
        public void Begin(int phone, int menu)
        {
            _names.Clear();
            _phone = phone;
            _menu = menu;
            _read = true;
            Searches++;
        }

        public void Learn(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                _names.Add(name);
            }
        }

        /// <summary>A button taken off: its name is free for an app put on later.</summary>
        public void Forget(string name)
        {
            if (name != null)
            {
                _names.Remove(name);
            }
        }

        /// <summary>True if a button of that name is on either phone or an app of the mod's waits under it.</summary>
        public bool Has(string name)
        {
            Asked++;
            return name != null && _names.Contains(name);
        }
    }
}
