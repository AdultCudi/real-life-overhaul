using System;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The one list window a feature's screens share: a <see cref="TravelList"/> made the first time one of them is
    /// wanted, widened for entries that say what a thing costs, and used again by every screen of the feature (a
    /// desk's menu, its pages, the room, a test). Only one screen has it at a time: taking it closes whichever had
    /// it, and that screen's own closing runs.
    ///
    /// Made for every feature that stands a desk in the world (hotels, the police station, the driving school), so
    /// that each is the same few lines: <c>Take(owner)</c>, fill, <c>Open</c>. If the game's window cannot be copied
    /// <see cref="Take"/> returns null (one "Game UI:" line says why) and stays null for the session, so the caller
    /// uses its plain fallback.
    /// </summary>
    public sealed class DeskList
    {
        private readonly string _name;
        private readonly float _width;
        private readonly float _entry;
        private TravelList _list;
        private object _owner;
        private bool _failed;

        /// <param name="name">The window's object name.</param>
        /// <param name="width">How wide, in the HUD's units (3,840 across); the game's own list is 775.</param>
        /// <param name="entryHeight">How high an entry is; its lines under the name may run on.</param>
        public DeskList(string name, float width, float entryHeight)
        {
            _name = name;
            _width = width;
            _entry = entryHeight;
        }

        public bool IsOpen => _list != null && _list.IsOpen;

        /// <summary>True while the window is open and it is this owner's screen that is in it.</summary>
        public bool OpenFor(object owner)
        {
            return IsOpen && ReferenceEquals(_owner, owner);
        }

        /// <summary>True when the game's window could not be copied: the feature is to use its plain way.</summary>
        public bool Failed => _failed;

        /// <summary>The window for this owner to fill and open, or null if it cannot be made.</summary>
        public TravelList Take(object owner)
        {
            if (_failed)
            {
                return null;
            }
            if (_list == null || !_list.Alive)
            {
                _list = TravelList.Make(_name);
                if (_list == null)
                {
                    _failed = true;
                    return null;
                }
                RectTransform window = _list.Window;
                window.sizeDelta = new Vector2(_width, window.sizeDelta.y);
                _list.EntryShape(_entry, true);
            }
            if (_list.IsOpen && !ReferenceEquals(_owner, owner))
            {
                _list.Close();
            }
            _owner = owner;
            return _list;
        }

        /// <summary>Closes the window if it is open (its owner's closing runs).</summary>
        public void Close()
        {
            if (IsOpen)
            {
                _list.Close();
            }
        }

        /// <summary>The window gone with its feature (the game's interface is rebuilt with the city); a later <see cref="Take"/> tries again.</summary>
        public void Destroy(Action<Exception> onError)
        {
            try
            {
                _list?.Destroy();
            }
            catch (Exception e)
            {
                onError?.Invoke(e);
            }
            _list = null;
            _owner = null;
            _failed = false;
        }
    }
}
