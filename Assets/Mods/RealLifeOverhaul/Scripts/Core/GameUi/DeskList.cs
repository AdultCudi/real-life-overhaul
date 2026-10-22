using System;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The one list window a feature's screens share (a desk's menu, its pages, the room, a test): a <see cref="TravelList"/>
    /// made when first wanted, widened for entries that say what a thing costs. One screen has it at a time: taking it closes
    /// whichever had it, and that screen's closing runs. Each desk in the world is then <c>Take(owner)</c>, fill,
    /// <c>Open</c>. If the game's window cannot be copied, <see cref="Take"/> returns null (one "Game UI:" line says why) for
    /// the session and the caller uses its plain fallback. Costs nothing a frame of its own.
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
        /// <param name="width">In the HUD's units (3,840 across); the game's own list is 775.</param>
        /// <param name="entryHeight">An entry's height; its lines under the name may wrap.</param>
        public DeskList(string name, float width, float entryHeight)
        {
            _name = name;
            _width = width;
            _entry = entryHeight;
        }

        public bool IsOpen => _list != null && _list.IsOpen;

        /// <summary>The window is open with this owner's screen in it.</summary>
        public bool OpenFor(object owner)
        {
            return IsOpen && ReferenceEquals(_owner, owner);
        }

        /// <summary>The game's window could not be copied: the feature uses its plain way.</summary>
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

        /// <summary>Closes the window if open; its owner's closing runs.</summary>
        public void Close()
        {
            if (IsOpen)
            {
                _list.Close();
            }
        }

        /// <summary>The window gone with its feature (the interface is rebuilt with the city); a later <see cref="Take"/> tries again.</summary>
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
