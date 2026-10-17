using System;
using System.Collections;
using BAModAPI;
using Helpers;
using UI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What the justice system changes on the game while it holds the player, remembered for <see cref="ReleaseAll"/> to put back.</summary>
    public sealed class JusticeHold
    {
        private const NavigationBlocker Blocker = NavigationBlocker.JobOfferPanel;   // one of the game's own that nothing of the mod uses

        private readonly IModLogger _logger;
        private PlayerController _blocked;
        private bool _paused, _faded, _resting;

        public JusticeHold(IModLogger logger)
        {
            _logger = logger;
        }

        /// <summary>The holder's signs of life. Counted, not timed: a game left in the background must not look like a dead routine.</summary>
        public int Beats;

        public bool Any => _paused || _faded || _resting || _blocked != null;

        public bool Faded => _faded;

        public void Beat()
        {
            Beats++;
        }

        /// <summary>Game time stops and the player's speed controls are off, as during the game's time skip. Safe to repeat.</summary>
        public void Pause()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.gameSpeed == null || GoingPublic.ClockShared)   // a Going Public clock is never stopped for one player
            {
                return;
            }
            _paused = true;
            uis.gameSpeed.Set(new GameSpeed(true, TimeSpeed.Pause, false));
            uis.gameSpeed.DisableTimeControl(true);
        }

        public void Unpause()
        {
            if (!_paused)
            {
                return;
            }
            _paused = false;
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.gameSpeed != null && (uis.timeMachine == null || !uis.timeMachine.isRunning))
            {
                uis.gameSpeed.DisableTimeControl(false);
                uis.gameSpeed.Reset();
            }
        }

        /// <summary>The game's own time skip has taken over the pause: it sets and resets it itself.</summary>
        public void PauseHandedOver()
        {
            _paused = false;
        }

        public void Block()
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || ReferenceEquals(player, _blocked))
            {
                return;
            }
            Unblock();
            player.ResetNavigation();
            player.SetNavigationBlocker(Blocker);
            _blocked = player;
        }

        public void Unblock()
        {
            PlayerController player = _blocked;
            _blocked = null;
            if (player != null)
            {
                try
                {
                    player.UnsetNavigationBlocker(Blocker);
                }
                catch (Exception e)
                {
                    _logger?.Error(e);
                }
            }
        }

        public IEnumerator Fade(string infoKey)
        {
            _faded = true;
            yield return UiFader.Fade(0.7f, infoKey);
        }

        public IEnumerator Unfade()
        {
            if (_faded)
            {
                _faded = false;
                yield return UiFader.UnFade();
            }
        }

        /// <summary>A bunk: the game's own slow rest, as in its waiting rooms.</summary>
        public void Rest(bool on)
        {
            if (on != _resting)
            {
                _resting = on;
                EnergyHelper.SetCurrentEnergyRegen(on ? EnergyRegen.Hospital : EnergyRegen.None);
            }
        }

        /// <summary>Everything put back, whatever state it was in. <paramref name="host"/> runs the fade back in if it still can.</summary>
        public void ReleaseAll(MonoBehaviour host)
        {
            // Each step on its own: one that fails must not keep the others from being put back.
            try
            {
                Rest(false);
            }
            catch (Exception e)
            {
                _resting = false;
                _logger?.Error(e);
            }
            Unblock();
            try
            {
                Unpause();
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            if (!_faded)
            {
                return;
            }
            _faded = false;
            try
            {
                if (host != null && host.isActiveAndEnabled)
                {
                    host.StartCoroutine(UiFader.UnFade());
                    return;
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            UiFader.isFading = false;   // the scene is going, or the fade back could not be run: the flag at least is put right
        }
    }
}
