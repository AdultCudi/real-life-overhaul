using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// What leisure shows, through the game's own interface: a copy of the job panel for the objective and the meter
    /// (the game's activity panel would pause the game), the game's panel for prompts and offers, its marker and its
    /// notifications. Per frame only <see cref="Gauge"/> runs; the rest is told on the tick, only on change.
    /// </summary>
    public sealed class LeisureScreen
    {
        private const string ButtonText = "reallife_leisure_button";   // "{text}": the words come from the activity
        private const string ButtonGo = "reallife_leisure_button_go";

        private readonly IModLogger _logger;
        private readonly DoorPanel _door;
        private readonly GuideMark _mark = new GuideMark("outings");
        private readonly Action _interact;
        private readonly Action[] _choose = new Action[3];
        private readonly object[] _choiceArgs = new object[3];
        private readonly Action _onOffer;
        private ShiftCard _card;
        private bool _cardTried;
        private bool _suppressed;
        private string _press;
        private object _pressArgs;
        private string[] _choices;
        private string _objectiveToldFor;
        private string _meterLabel;
        private float _zoneCentre = -1f, _zoneWidth = -1f, _zoneSweet = -1f;
        private GameBar _bar;
        private bool _meterOn;
        private LeisureActivity _gaugeSaidFor;
        private string _offerText, _offerPlain;
        private bool _offerUp;

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
        }

        internal void KeptBy(Behaviour host)
        {
            _mark.Keep(host);
        }

        /// <summary>True while one of the panel's buttons has the game's key.</summary>
        public bool TakesKey => _door.TakesKey;

        /// <summary>Hidden for photo mode: the panel, the card and the marker stay away until lifted.</summary>
        public void Suppress(bool hidden)
        {
            _suppressed = hidden;
            if (hidden)
            {
                HideActivity();
                HideOffer();
            }
        }

        public void Draw(LeisureActivity activity, string title, Vector3 player)
        {
            if (_suppressed)
            {
                return;
            }
            if (ThirdPersonCamera.IsUiOpen())
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
                // Without the job panel the objective goes out as a notification each time it changes.
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
                _bar = null;
                _meterOn = false;
            }
            if (_card == null && !_cardTried)
            {
                _cardTried = true;
                _card = ShiftCard.Make(_logger);
                if (Meta.Dev.Verbose)
                {
                    _logger?.Info("Leisure check (activity card): " + (_card != null ? "a copy of the game's job panel is ready: PASS"
                        : "the game's job panel could not be copied; what is asked is sent as notifications and a timing has no bar: FAIL") + ".");
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
                _pressArgs = press != null ? new { text = Plain(press) } : null;
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

        private void Meter(LeisureActivity activity, ShiftCard card)
        {
            if (!activity.Meter)
            {
                if (_meterOn)
                {
                    _meterOn = false;
                    card.Line(2, null, WorldPanel.White());
                    _bar?.Show(false);
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
                _bar = card.Bar();
                card.Line(2, _bar != null ? activity.MeterLabel ?? string.Empty : MeterWords(activity), WorldPanel.White());
                card.Line(3, null, WorldPanel.White());   // the bar takes the place of the last two lines
                card.Line(4, null, WorldPanel.White());
                if (_bar != null && activity.ZoneWidth > 0f)
                {
                    _bar.Zone(activity.ZoneCentre, activity.ZoneWidth, activity.SweetWidth);
                }
            }
            Cursor(activity);
            _bar?.Show(true);
            if (!ReferenceEquals(_gaugeSaidFor, activity))
            {
                _gaugeSaidFor = activity;
                if (Meta.Dev.Verbose)
                {
                    _logger?.Info("Gauge check (" + LeisureKinds.Id(activity.Kind) + "): "
                                  + (_bar != null && _bar.Value >= 0f
                                      ? "the game's bar, " + _bar.Value.ToString("0.00", CultureInfo.InvariantCulture)
                                        + (_bar.Timing ? " (a marker, the zone about " + activity.ZoneCentre.ToString("0.00", CultureInfo.InvariantCulture) + ")" : " (filled)") + ": PASS"
                                      : "FAIL (the game's bar could not be copied; the zone is given in figures on the line above)") + ".");
                }
            }
        }

        /// <summary>Per frame while a meter is up; the bar writes only when the cursor moves a visible step.</summary>
        public void Gauge(LeisureActivity activity)
        {
            if (_bar != null && _meterOn && activity.Meter && !_suppressed)
            {
                Cursor(activity);
            }
        }

        // With a zone the marker runs along the bar; without one it is a clock, the bar filled with what is left.
        private void Cursor(LeisureActivity activity)
        {
            if (_bar == null)
            {
                return;
            }
            if (activity.ZoneWidth > 0f)
            {
                _bar.Mark(activity.MeterCursor);
            }
            else
            {
                _bar.Fill(activity.MeterCursor);
            }
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

        /// <summary>The game's panel over a place where something can be started.</summary>
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

        public void HideActivity()
        {
            _card?.Show(false);
            _door.Hide("no activity is on");
            _mark.Hide();
            _press = null;
            _choices = null;
        }

        /// <summary>A result as one of the game's notifications, the title over the lines.</summary>
        public static void Card(string title, string body, float seconds)
        {
            Notifications.Show(NotificationType.Info, "reallife_leisure_card_toast",
                new Dictionary<string, string>(2) { { "title", title ?? string.Empty }, { "body", body ?? string.Empty } }, seconds);
        }

        public static void Say(string text, float seconds)
        {
            Notifications.Show(NotificationType.Info, "reallife_leisure_text", new Dictionary<string, string>(1) { { "text", text ?? string.Empty } }, seconds);
        }

        public void Dispose()
        {
            _door.Hide("leisure went with the city");
            _mark.Hide();
            _card?.Destroy();
            _card = null;
        }

        // "E: cast" without its key: the game writes the key on the button itself.
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
