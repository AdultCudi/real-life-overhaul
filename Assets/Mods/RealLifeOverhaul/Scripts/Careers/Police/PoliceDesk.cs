using System;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.MiniMenu;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>One way to answer an incident as the list shows it.</summary>
    internal struct DutyEntry
    {
        public DutyMove Move;
        public string Name, Detail;
        public bool Enabled;
    }

    /// <summary>
    /// The list an officer chooses from at an incident, in the game's own list window (<see cref="TravelList"/>, as the station's front desk is): the
    /// kind of incident and its danger over the moves, each with its odds, and backing off last. The player stands still while it is up, as at the
    /// desk, and it closes if one of the game's own screens comes up. Nothing here runs unless the director opens it; the cost is one list.
    /// </summary>
    internal sealed class PoliceDesk
    {
        private const NavigationBlocker Blocker = NavigationBlocker.DoctorOperation;   // as the station desk's: nothing else in the street sets it
        private const string ButtonDo = "reallife_police_list_do";

        private readonly DeskList _window = new DeskList("RealLifeOverhaul Duty", 1100f, 150f);
        private readonly Action<int> _onPick;
        private readonly Action _onClosed;
        private PlayerController _player;
        private bool _held;
        private Action<DutyMove> _answer;
        private Action _closed;

        public PoliceDesk()
        {
            _onPick = Picked;
            _onClosed = Closed;
        }

        public bool IsOpen => _window.IsOpen;

        /// <summary>Opens the list; false if the game's window could not be copied (the officer is then told so and the incident is left).</summary>
        public bool Open(string title, string info, List<DutyEntry> entries, Action<DutyMove> answer, Action closed)
        {
            if (IsOpen)
            {
                return true;
            }
            TravelList list = _window.Take(this);
            if (list == null)
            {
                return false;
            }
            _answer = answer;
            _closed = closed;
            _player = PlayerHelper.PlayerController;
            if (_player != null && !_held)
            {
                _held = true;
                _player.ResetNavigation();
                _player.SetNavigationBlocker(Blocker);
            }
            list.Begin(title, info);
            for (int i = 0; i < entries.Count; i++)
            {
                DutyEntry e = entries[i];
                list.Add((int)e.Move, e.Name, e.Detail, ButtonDo, e.Enabled);
            }
            list.Open(_onPick, _onClosed);
            return true;
        }

        /// <summary>Takes the list off the screen and lets the player walk. Safe at any time.</summary>
        public void Close()
        {
            if (_window.IsOpen)
            {
                _window.Close();   // its closing lets the player walk
            }
            else
            {
                Release();
            }
        }

        public void Dispose()
        {
            Close();
            _window.Destroy(e => CareersDirector.Logger?.Error(e));
        }

        /// <summary>Once a second while the list is up: it goes if one of the game's own screens comes up. True if it closed it.</summary>
        public bool Watch()
        {
            if (IsOpen && (FullMenu.IsOpen || CityMap.IsOpen || MiniMenu.IsOpen || HudConfirm.isOpen))
            {
                Close();
                return true;
            }
            return false;
        }

        private void Picked(int id)
        {
            Action<DutyMove> answer = _answer;
            _closed = null;   // a pick is no dismissal
            Close();
            answer?.Invoke((DutyMove)id);
        }

        private void Closed()
        {
            Release();
            Action closed = _closed;
            _closed = null;
            _answer = null;
            closed?.Invoke();
        }

        private void Release()
        {
            if (!_held)
            {
                return;
            }
            _held = false;
            try
            {
                if (_player != null)
                {
                    _player.UnsetNavigationBlocker(Blocker);
                }
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
            }
            _player = null;
        }
    }
}
