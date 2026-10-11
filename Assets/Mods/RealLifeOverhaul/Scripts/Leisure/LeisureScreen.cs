using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// Everything leisure puts on screen, as the game's own interface (nothing is drawn by the mod):
    ///  - the objective and the figures: a copy of the game's job panel at the top right (<see cref="ShiftCard"/>): the activity's
    ///    name, what to do, the status, and while a timing meter is up two more lines (what it asks and the gauge);
    ///  - the prompt and the choices: the game's panel over the player (<see cref="DoorPanel"/>), the game writing each key on its
    ///    button and running it on that key or a click;
    ///  - a place worth a visit, before anything is started: the same panel over the place;
    ///  - the marker: the game's destination marker (<see cref="GuideMark"/>);
    ///  - the list of the clothes browser: the game's list window (<see cref="DeskList"/>);
    ///  - what a game or a visit adds up to, and the radio's station and song: the game's notifications.
    ///
    /// The timing meter. The game has no such widget. Its activity panel (PlayerActivityUI) was looked at: it pauses the game
    /// while it is shown, and it writes its progress bar only when a whole minute of game time has passed (OnActivityRunning), or
    /// in UpdateActivityDisplay, which also makes its labels and buttons again. A cursor that crosses in about a second cannot be a
    /// progress bar there. So the meter is a line of the job panel: what it asks in words with the zone as percentages
    /// ("Cast: good from 59% to 85%, best from 68% to 76%"), and under it a gauge of twenty cells in plain letters, the zone shown
    /// as = and #, the cursor as |, written only when the cursor moves to another cell. The action is a button of the game's panel
    /// on the game's key.
    ///
    /// The cost: nothing per frame except <see cref="Gauge"/>, which compares one cell number and writes a line only when the cursor
    /// has moved to another cell (about twenty times a pass). Everything else is told on the tick and only when it changed.
    /// </summary>
    public sealed class LeisureScreen
    {
        private const string ButtonText = "reallife_leisure_button";          // "{text}": the words come from the activity
        private const string ButtonGo = "reallife_leisure_button_go";
        private const int Cells = 20;

        private readonly IModLogger _logger;
        private readonly DoorPanel _door;
        private readonly GuideMark _mark = new GuideMark();
        private readonly DeskList _list = new DeskList("RealLifeOverhaul Clothes", 1000f, 110f);
        private readonly Action _interact;
        private readonly Action[] _choose = new Action[3];
        private readonly object[] _choiceArgs = new object[3];
        private readonly StringBuilder _gauge = new StringBuilder(160);
        private ShiftCard _card;
        private bool _cardTried;
        private bool _suppressed;
        private string _press, _pressText;
        private object _pressArgs;
        private string[] _choices;
        private string _objectiveToldFor;
        private string _meterLabel;
        private float _zoneCentre = -1f, _zoneWidth = -1f, _zoneSweet = -1f;
        private string _meterWords, _gaugeWords;
        private int _cell = -2;
        private bool _meterOn;
        private string _offerText, _offerPlain;
        private bool _offerUp;
        private Action _onOffer;

        public LeisureScreen(IModLogger logger, Action interact, Action<int> choose, Action accept)
        {
            _logger = logger;
            _door = new DoorPanel(logger, "Leisure check (prompt panel)");
            _interact = _door.Press(interact);
            _onOffer = _door.Press(accept);
            for (int i = 0; i < _choose.Length; i++)
            {
                int index = i;
                _choose[i] = _door.Press(() => choose(index));
            }
            TravelList.Logger = logger;
        }

        /// <summary>True while one of the panel's buttons is the one the game is reading the key for (the mod's own interact key stands aside).</summary>
        public bool TakesKey => _door.TakesKey;

        public bool ListOpen => _list.IsOpen;

        /// <summary>Hidden for a photograph: the panel, the card and the marker are put away until it is lifted.</summary>
        public void Suppress(bool hidden)
        {
            _suppressed = hidden;
            if (hidden)
            {
                HideActivity();
                HideOffer();
            }
        }

        // ---------------------------------------------------------------- the activity

        /// <summary>The card, the panel and the marker for the activity that is on. Five times a second; each is told only what changed.</summary>
        public void Draw(LeisureActivity activity, string title, Vector3 player)
        {
            if (_suppressed)
            {
                return;
            }
            if (ThirdPersonCamera.IsUiOpen() && !_list.IsOpen)
            {
                HideActivity();
                return;
            }
            ShiftCard card = GetCard();
            if (card != null)
            {
                Color32 white = WorldPanel.White();
                card.Title(title);
                card.Line(0, activity.Objective, white);
                card.Line(1, activity.Status, white);
                Meter(activity, card);
                card.Show(true);
            }
            else if (activity.Objective != null && !ReferenceEquals(activity.Objective, _objectiveToldFor))
            {
                // Without the copy of the game's job panel what is asked of the player is sent as a notification when it changes.
                _objectiveToldFor = activity.Objective;
                Say(activity.Objective, 6f);
            }
            if (activity.Marked)
            {
                _mark.Show(activity.MarkAt, activity.MarkLabel);
            }
            else
            {
                _mark.Hide();
            }
            Panel(activity, title, player);
        }

        private ShiftCard GetCard()
        {
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
                    _logger?.Info("Leisure check (activity card): " + (_card != null ? "a copy of the game's job panel is ready: PASS"
                        : "the game's job panel could not be copied; what is asked is sent as notifications and the meter has no gauge: FAIL") + ".");
                }
            }
            return _card;
        }

        private void Panel(LeisureActivity activity, string title, Vector3 player)
        {
            string press = activity.Prompt ?? (activity.OwnsInteractKey ? activity.QuietPrompt : null);
            string[] choices = activity.Choices;
            if (press == null && choices == null)
            {
                _door.Hide("nothing to press");
                return;
            }
            if (!ReferenceEquals(press, _press))
            {
                _press = press;
                _pressText = press != null ? Plain(press) : null;
                _pressArgs = press != null ? new { text = _pressText } : null;
            }
            if (!ReferenceEquals(choices, _choices))
            {
                _choices = choices;
                for (int i = 0; i < _choiceArgs.Length; i++)
                {
                    _choiceArgs[i] = choices != null && i < choices.Length && choices[i] != null ? new { text = choices[i] } : null;
                }
            }
            WorldPanel panel = _door.Panel;
            panel.Lines(title, null, null, WorldPanel.White());
            panel.Button(0, press != null ? ButtonText : null, _pressArgs, press != null ? _interact : null, true, true);
            for (int i = 0; i < _choose.Length; i++)
            {
                bool has = _choiceArgs[i] != null;
                panel.Button(i + 1, has ? ButtonText : null, _choiceArgs[i], has ? _choose[i] : null, true, false);
            }
            panel.Follow = null;
            _door.Show(player, player, 2.1f, () => "activity");
        }

        // ---------------------------------------------------------------- the meter

        private void Meter(LeisureActivity activity, ShiftCard card)
        {
            if (!activity.Meter)
            {
                if (_meterOn)
                {
                    _meterOn = false;
                    card.Line(2, null, WorldPanel.White());
                    card.Line(3, null, WorldPanel.White());
                }
                return;
            }
            if (!_meterOn || !ReferenceEquals(activity.MeterLabel, _meterLabel) || activity.ZoneCentre != _zoneCentre
                || activity.ZoneWidth != _zoneWidth || activity.SweetWidth != _zoneSweet)
            {
                _meterOn = true;
                _meterLabel = activity.MeterLabel;
                _zoneCentre = activity.ZoneCentre;
                _zoneWidth = activity.ZoneWidth;
                _zoneSweet = activity.SweetWidth;
                _meterWords = MeterWords(activity);
                _cell = -2;
                card.Line(2, _meterWords, WorldPanel.White());
            }
            Cursor(activity, card);
        }

        /// <summary>Per frame while a meter is up: the gauge is written only when the cursor is in another cell.</summary>
        public void Gauge(LeisureActivity activity)
        {
            if (_card != null && _meterOn && activity.Meter && !_suppressed)
            {
                Cursor(activity, _card);
            }
        }

        private void Cursor(LeisureActivity activity, ShiftCard card)
        {
            int cell = Mathf.Clamp(Mathf.RoundToInt(activity.MeterCursor * (Cells - 1)), 0, Cells - 1);
            if (cell == _cell)
            {
                return;
            }
            _cell = cell;
            _gauge.Length = 0;
            _gauge.Append('[');
            bool zone = activity.ZoneWidth > 0f;
            for (int i = 0; i < Cells; i++)
            {
                float at = i / (float)(Cells - 1);
                if (i == cell)
                {
                    _gauge.Append(zone ? "<color=#FFC83C>|</color>" : "#");
                }
                else if (!zone)
                {
                    _gauge.Append(i < cell ? '#' : '-');   // a clock draining: what is left is filled
                }
                else
                {
                    float off = Mathf.Abs(at - activity.ZoneCentre);
                    _gauge.Append(off <= activity.SweetWidth * 0.5f ? '#' : off <= activity.ZoneWidth * 0.5f ? '=' : '-');
                }
            }
            _gauge.Append(']');
            _gaugeWords = _gauge.ToString();
            card.Line(3, _gaugeWords, WorldPanel.White());
        }

        private static string MeterWords(LeisureActivity activity)
        {
            string label = activity.MeterLabel ?? string.Empty;
            if (activity.ZoneWidth <= 0f)
            {
                return label;
            }
            string text = JobText.T(activity.SweetWidth > 0f ? "reallife_leisure_meter_zone_best" : "reallife_leisure_meter_zone");
            return text.Replace("{label}", label)
                .Replace("{from}", Percent(activity.ZoneCentre - activity.ZoneWidth * 0.5f)).Replace("{to}", Percent(activity.ZoneCentre + activity.ZoneWidth * 0.5f))
                .Replace("{bestfrom}", Percent(activity.ZoneCentre - activity.SweetWidth * 0.5f)).Replace("{bestto}", Percent(activity.ZoneCentre + activity.SweetWidth * 0.5f));
        }

        private static string Percent(float fraction)
        {
            return Mathf.Clamp(Mathf.RoundToInt(fraction * 100f), 0, 100).ToString();
        }

        // ---------------------------------------------------------------- a place worth a visit

        /// <summary>The game's panel over a place where something can be started: "Fish here (bait $5, five casts)" and a button.</summary>
        public void Offer(string text, Vector3 place, Vector3 player)
        {
            if (_suppressed)
            {
                return;
            }
            if (!ReferenceEquals(text, _offerText))
            {
                _offerText = text;
                _offerPlain = Plain(text);
            }
            WorldPanel panel = _door.Panel;
            panel.Lines(_offerPlain, null, null, WorldPanel.White());
            panel.Button(0, ButtonGo, _onOffer, true, true);
            panel.Button(1, null, null, false, false);
            panel.Button(2, null, null, false, false);
            panel.Button(3, null, null, false, false);
            panel.Follow = null;
            _offerUp = _door.Show(place, player, 2.2f, () => "an activity on offer");
        }

        public void HideOffer()
        {
            if (_offerUp || _door.Up)
            {
                _offerUp = false;
                _door.Hide("nothing on offer here now");
            }
        }

        /// <summary>The prompt, the card and the marker put away (the activity is over or the screen is needed).</summary>
        public void HideActivity()
        {
            _card?.Show(false);
            _door.Hide("no activity is on");
            _mark.Hide();
            _press = null;
            _choices = null;
        }

        // ---------------------------------------------------------------- the list

        /// <summary>The game's list window for the clothes browser; null (one line in the log) if it could not be copied.</summary>
        public TravelList ListFor(object owner)
        {
            TravelList list = _list.Take(owner);
            if (list == null)
            {
                _logger?.Info("Leisure check (clothes list): the game's list could not be made; the rack and the wardrobe are not offered: FAIL.");
            }
            return list;
        }

        public void CloseList()
        {
            _list.Close();
        }

        // ---------------------------------------------------------------- notifications

        /// <summary>A game or a visit adds up: one of the game's notifications with the title over the lines.</summary>
        public void Card(string title, string body, float seconds)
        {
            Notifications.Show(NotificationType.Info, "reallife_leisure_card_toast",
                new Dictionary<string, string>(2) { { "title", title ?? string.Empty }, { "body", body ?? string.Empty } }, seconds);
        }

        /// <summary>One line, as one of the game's notifications.</summary>
        public void Say(string text, float seconds)
        {
            Notifications.Show(NotificationType.Info, "reallife_leisure_text", new Dictionary<string, string>(1) { { "text", text ?? string.Empty } }, seconds);
        }

        public void Dispose()
        {
            _door.Hide("leisure went with the city");
            _mark.Hide();
            _card?.Destroy();
            _card = null;
            _list.Destroy(e => _logger?.Error(e));
        }

        // A prompt as the activities write it ("E: cast") without its key: the game writes the key on the button itself.
        private static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            int colon = text.IndexOf(": ", StringComparison.Ordinal);
            if (colon > 0 && colon <= 14)
            {
                text = text.Substring(colon + 2);
            }
            return char.IsLower(text[0]) ? char.ToUpperInvariant(text[0]) + text.Substring(1) : text;
        }
    }
}
