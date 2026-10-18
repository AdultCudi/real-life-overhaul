using System;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The screen of a scene of the player's run or office in the game's own list window (<see cref="TravelList"/> through a
    /// <see cref="DeskList"/>): what is said, and a button for each answer; the player stands still for it. Nothing per frame.
    /// </summary>
    internal sealed class OfficePanel
    {
        public const int MostChoices = 3;

        private const NavigationBlocker Blocker = (NavigationBlocker)7312;   // a value of our own, so nobody else's is ever lifted
        private const float InputDelay = 0.4f;                               // seconds after a page appears before an answer counts
        private const string ButtonChoose = "reallife_civic_office_choose";

        private readonly DeskList _window = new DeskList("RealLifeOverhaul Civic Office", 1250f, 150f);
        private readonly Action<int> _onPick;
        private readonly Action _onClosed;
        private Action<int> _answer;
        private PlayerController _held;
        private float _shownAt;
        private int _count;
        private bool _leaving;

        public OfficePanel()
        {
            _onPick = Picked;
            _onClosed = Closed;
        }

        public bool Active { get; private set; }

        /// <summary>The scene begins: the phone is put away and the player stands still. False if the game's window cannot be made.</summary>
        public bool Begin()
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (Active || player == null || _window.Failed)
            {
                return false;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.fullMenu != null && FullMenu.IsOpen)
            {
                uis.fullMenu.Toggle(false);   // the game's list stands on the HUD, which the phone's full screen hides
            }
            Active = true;
            _held = player;
            player.ResetNavigation();
            player.SetNavigationBlocker(Blocker);
            return true;
        }

        /// <summary>One page: up to three answers, each a name and what it means. False if the game's window cannot be made (the scene is ended).</summary>
        public bool Show(string title, string subtitle, string speaker, string body, string[] names, string[] hints, int count, Action<int> answer)
        {
            TravelList list = Active ? _window.Take(this) : null;
            if (list == null)
            {
                End();
                return false;
            }
            _answer = answer;
            _count = Mathf.Clamp(count, 1, MostChoices);
            list.Begin(title ?? string.Empty, subtitle ?? string.Empty);
            body = body ?? string.Empty;
            int lines = 1;
            for (int i = 0; i < body.Length; i++)
            {
                lines += body[i] == '\n' ? 1 : 0;
            }
            list.AddNote(speaker ?? string.Empty, body, list.HeightFor((int)((body.Length + 70 * lines) * 1.3f), false));
            for (int i = 0; i < _count; i++)
            {
                string hint = hints != null && i < hints.Length ? hints[i] : null;
                list.Add(i, names[i] ?? string.Empty, hint, ButtonChoose, true, string.IsNullOrEmpty(hint) ? 0f : list.HeightFor(hint.Length, true));
            }
            list.Open(_onPick, _onClosed);
            list.ScrollToTop();
            list.Locked = true;
            _shownAt = Time.unscaledTime;
            return true;
        }

        private void Picked(int id)
        {
            try
            {
                if (!Active || Time.unscaledTime - _shownAt < InputDelay || id < 0 || id >= _count)
                {
                    return;
                }
                Action<int> answer = _answer;
                _answer = null;
                answer?.Invoke(id);
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                End();
            }
        }

        private void Closed()
        {
            if (Active && !_leaving)
            {
                Free();
            }
        }

        /// <summary>Once a second while a scene is on: one whose window has gone with the game's interface is ended.</summary>
        public void Watch()
        {
            if (Active && !_window.OpenFor(this))
            {
                Free();
            }
        }

        public void End()
        {
            _leaving = true;
            if (_window.OpenFor(this))
            {
                _window.Close();
            }
            _leaving = false;
            Free();
        }

        public void Destroy()
        {
            End();
            _window.Destroy(e => CivicDirector.Logger?.Error(e));
        }

        private void Free()
        {
            Active = false;
            _answer = null;
            PlayerController player = _held;
            _held = null;
            try
            {
                if (player != null)
                {
                    player.UnsetNavigationBlocker(Blocker);
                }
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
            }
        }
    }
}
