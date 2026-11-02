using System.Collections;
using TMPro;
using UI.Monologues;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's spoken line (Uncle Fred's lesson, a rival's word) as the kit's notice card at the top centre: the face
    /// the game shows on its tile, the line, how many more the game holds after it, and the close, which presses the
    /// game's own. The game keeps its queue, its sound and its timing: the card comes as the game's card comes, goes the
    /// moment the line is over (the game's card slides away for two seconds more, unseen), and the next line follows as
    /// the game sends it. It takes no key. Cost: a look at a signature four times a second while a line shows.
    /// </summary>
    public sealed class MonologueScreen : IUiScreen
    {
        private const float Every = 0.25f;

        private readonly MonologueUI _ui;
        private readonly Transform _card;
        private readonly UiMonologueModel _m = new UiMonologueModel();
        private bool _dirty = true, _spoke;
        private float _next;
        private long _seen;
        private string _line;

        public MonologueScreen(MonologueUI ui, Transform card)
        {
            _ui = ui;
            _card = card;
        }

        public string Id => DialogKits.MonologueId;

        public bool Dirty
        {
            get
            {
                float now = Time.unscaledTime;
                if (now >= _next)
                {
                    _next = now + Every;
                    long sig = Signature();
                    _dirty |= sig != _seen;
                    _seen = sig;
                }
                return _dirty;
            }
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            Build();
            UiMonologue.Lay(s, _m, frame, dark);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            if (command != UiMonologue.CloseCommand)
            {
                return false;
            }
            Transform close = _card != null ? _card.Find("Close") : null;
            bool done = UiWidgets.Press(close != null ? close.GetComponent<Button>() : null);
            UiDirector.Logger?.Info("Monologue (kit): the line closed" + (done ? " by the game's own button" : ", but the game's close was not shown, so nothing was pressed"));
            _dirty = true;
            return done;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back() => false;

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }

        // The line, its face, the lines waiting after it and whether it is still being said.
        private long Signature()
        {
            if (_card == null)
            {
                return 0L;
            }
            TMP_Text words = Words();
            Image face = Face();
            long sig = words != null && words.text != null ? words.text.GetHashCode() : 0;
            sig = sig * 31L + (face != null && face.sprite != null ? face.sprite.GetInstanceID() : 0);
            sig = sig * 31L + Waiting();
            return sig * 31L + (Speaking() ? 1 : 0);
        }

        private void Build()
        {
            TMP_Text words = Words();
            string line = words != null ? ContactsRules.Plain(words.text) : null;
            if (line != _line)
            {
                _line = line;
                _spoke = false;   // a new line: shown until it has been said
            }
            bool speaking = Speaking();
            _spoke |= speaking;
            Image face = Face();
            _m.Name = null;   // the game names nobody on its card: the face says who it is
            _m.Picture = face != null ? DialogKits.PictureOf(face.sprite) : null;
            _m.Hue = UiColour.HueOf(line ?? string.Empty);
            _m.Line = !_spoke || speaking ? line : null;   // said and sliding away: the card goes now, not two seconds later
            int waiting = Waiting();
            _m.More = waiting > 0 ? UiText.Fill("reallife_ui_monologue_more", "count", waiting.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null;
        }

        private TMP_Text Words()
        {
            Transform t = _card != null ? _card.Find("Message") : null;
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }

        private Image Face()
        {
            Transform t = _card != null ? _card.Find("Image") : null;
            return t != null ? t.GetComponent<Image>() : null;
        }

        // The lines the game holds after this one; none where this version keeps its queue otherwise.
        private int Waiting()
        {
            var queue = GameContract.MonologueQueue.Field != null && _ui != null ? GameContract.MonologueQueue.Field.GetValue(_ui) as ICollection : null;
            return queue != null ? queue.Count : 0;
        }

        // The line still being said (its sound and the wait after it); true where this version keeps that otherwise, so the
        // card then stays until the game's own card has gone.
        private bool Speaking()
        {
            if (GameContract.MonologueSpeaking.Field == null || _ui == null)
            {
                return true;
            }
            return GameContract.MonologueSpeaking.Field.GetValue(_ui) != null;
        }
    }
}
