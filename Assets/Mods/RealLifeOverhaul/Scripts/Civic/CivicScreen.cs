using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What civic life puts on screen, as the game's own interface (nothing is drawn by the mod): the polling station, the
    /// campaign canvasser, the site notice and the canvassing shift's "offer a leaflet" are the game's panel over a place
    /// (<see cref="DoorPanel"/>) with one button, which the game labels with the player's key and runs on that key or a click;
    /// the canvassing shift's figures (leaflets taken, seconds left) are the headline and first line of a copy of the game's job
    /// panel (<see cref="ShiftCard"/>). Without that copy the figures are sent as a notification each time they change.
    ///
    /// The cost: nothing per frame. The director offers a prompt once a second (five times a second during a shift); the panel and
    /// the card are told only what changed and rebuild their words only when they do.
    /// </summary>
    public sealed class CivicScreen
    {
        private readonly IModLogger _logger;
        private readonly DoorPanel _door;
        private readonly Action _pressed;
        private ShiftCard _card;
        private bool _cardTried;
        private readonly Func<string> _what;
        private string _first, _told;
        private bool _offerUp;

        public CivicScreen(IModLogger logger, Action pressed)
        {
            _logger = logger;
            _door = new DoorPanel(logger, "Civic check (prompt panel)");
            _pressed = _door.Press(pressed);
            _what = () => _first;
        }

        /// <summary>True while the panel's key is the one the game is reading (the mod's own interact key stands aside).</summary>
        public bool TakesKey => _door.TakesKey;

        /// <summary>The prompt: <paramref name="first"/> is what stands there, <paramref name="button"/> the locale key of the verb.</summary>
        public void Offer(string first, string button, Vector3 player)
        {
            _first = first;
            WorldPanel panel = _door.Panel;
            panel.Lines(first, null, null, WorldPanel.White());
            panel.Button(0, button, _pressed, true, true);
            panel.Button(1, null, null, false, false);
            panel.Button(2, null, null, false, false);
            panel.Button(3, null, null, false, false);
            panel.Follow = null;
            _offerUp = _door.Show(player, player, 2.1f, _what);
        }

        public void ClearOffer()
        {
            if (_offerUp || _door.Up)
            {
                _offerUp = false;
                _door.Hide("nothing to offer here now");
            }
        }

        /// <summary>The shift's figures; null hides them.</summary>
        public void Status(string title, string line)
        {
            if (line == null)
            {
                _card?.Show(false);
                return;
            }
            if (_card != null && !_card.Alive)
            {
                _card = null;
                _cardTried = false;
            }
            if (_card == null && !_cardTried)
            {
                _cardTried = true;
                _card = ShiftCard.Make(_logger);
                if (Meta.Dev.Verbose)
                {
                    _logger?.Info("Civic check (shift card): " + (_card != null ? "a copy of the game's job panel is ready: PASS"
                        : "the game's job panel could not be copied; the figures are sent as notifications: FAIL") + ".");
                }
            }
            if (_card == null)
            {
                if (!ReferenceEquals(line, _told))
                {
                    _told = line;
                    Notifications.Show(NotificationType.Info, "reallife_civic_text", new Dictionary<string, string>(1) { { "text", line } }, 3f);
                }
                return;
            }
            _card.Title(title);
            _card.Line(0, line, WorldPanel.White());
            _card.Show(true);
        }

        public void HideAll()
        {
            ClearOffer();
            Status(null, null);
        }

        public void Dispose()
        {
            _door.Hide("civic life went with the city");
            _card?.Destroy();
            _card = null;
        }
    }
}
