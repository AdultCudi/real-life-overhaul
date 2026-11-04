using System.Collections;
using System.Globalization;
using TMPro;
using UI;
using UI.Monologues;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's spoken line (Uncle Fred's lesson, a rival's word) as the kit's notice card at the top centre: the face
    /// the game shows on its tile (Uncle Fred's named beside it), the line, where it stands among the lines the game holds
    /// ("1 of 2"), how far its sound has played, and Next and Skip, which press the game's own close: Next on this line,
    /// Skip on this one and on each line waiting as it comes, so every line's own ending runs as the game's close runs it.
    /// The game keeps its queue, its sound and its timing: the card comes as the game's card comes, goes the moment the
    /// line is over (the game's card slides away for two seconds more, unseen), and the next line follows as the game
    /// sends it. It takes no key. Cost: a look at a signature four times a second while a line shows.
    /// </summary>
    public sealed class MonologueScreen : IUiScreen
    {
        private const float Every = 0.25f;
        private const int ProgressStep = 5;        // the bar moves in steps of a twentieth: a layout a step, not a frame
        private const float SkipFor = 8f;          // seconds a skip waits for the next line: the game's slide out and in, with room
        private const float RunGap = 10f;          // seconds without a line said after which the next starts a new run

        private readonly MonologueUI _ui;
        private readonly Transform _card;
        private readonly AudioSource _sound;       // the game's own, which plays the line
        private readonly UiMonologueModel _m = new UiMonologueModel();
        private bool _dirty = true, _spoke, _heard;
        private float _next;
        private long _seen;
        private string _line;
        private Sprite _fred;
        private int _run;                          // lines shown in this run of the game's queue, this one counted
        private int _skipLeft;                     // lines still to close after a skip, as they come
        private float _skipBy;                     // until when a skip waits for them
        private float _lastSaid;                   // when a line was last being said

        public MonologueScreen(MonologueUI ui, Transform card)
        {
            _ui = ui;
            _card = card;
            _sound = ui != null ? ui.GetComponent<AudioSource>() : null;
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
            UiMonologue.Lay(s, _m, frame, dark, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            if (command == UiMonologue.SkipCommand)
            {
                _skipLeft = Waiting();
                _skipBy = Time.unscaledTime + SkipFor;
                bool skipped = CloseLine();
                UiDirector.Logger?.Info("Monologue (kit): skipped" + (skipped ? ", by the game's own close, with " + _skipLeft + " more to close as they come"
                    : ", but the game's close was not shown, so nothing was pressed"));
                return skipped;
            }
            if (command != UiMonologue.CloseCommand)
            {
                return false;
            }
            bool done = CloseLine();
            UiDirector.Logger?.Info("Monologue (kit): the line closed" + (done ? " by the game's own button" : ", but the game's close was not shown, so nothing was pressed"));
            return done;
        }

        public void Focused(int command, int arg)
        {
        }

        // Cancel is Skip while lines wait after this one (it ends this line and each as it comes), else it closes this line.
        public bool Back() => Press(string.IsNullOrEmpty(_m.Skip) ? UiMonologue.CloseCommand : UiMonologue.SkipCommand, 0);

        public void Tab(int step)
        {
        }

        // Interact is Next: this line closed as the game's own button closes it.
        public bool Act(UiAction action) => action == UiAction.Alt && Press(UiMonologue.CloseCommand, 0);

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }

        // The game's own close on the line now: the line ends as it ends when the player closes the game's card.
        private bool CloseLine()
        {
            Transform close = _card != null ? _card.Find("Close") : null;
            bool done = UiWidgets.Press(close != null ? close.GetComponent<Button>() : null);
            _dirty = true;
            return done;
        }

        // The line, its face, the lines waiting after it, whether it is still being said and how far its sound has played.
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
            sig = sig * 31L + Progress() / ProgressStep;
            return sig * 31L + (Speaking() ? 1 : 0);
        }

        private void Build()
        {
            TMP_Text words = Words();
            string line = words != null ? ContactsRules.Plain(words.text) : null;
            int waiting = Waiting();
            bool speaking = Speaking();
            if (line != _line)
            {
                _line = line;
                _spoke = false;   // a new line: shown until it has been said
                _heard = false;
                if (line != null)
                {
                    _run = Time.unscaledTime - _lastSaid > RunGap ? 1 : _run + 1;   // after a quiet while, a new run
                }
            }
            if (line != null && speaking && _skipLeft > 0 && Time.unscaledTime <= _skipBy && !_spoke)
            {
                // A line that came after a skip: closed by the game's own close as it shows, so its own ending runs.
                _skipLeft--;
                _skipBy = Time.unscaledTime + SkipFor;
                CloseLine();
                _spoke = true;
                speaking = false;
            }
            _spoke |= speaking;
            if (speaking)
            {
                _lastSaid = Time.unscaledTime;
            }
            Image face = Face();
            // Uncle Fred named by his face, which the tutorial holds; a rival's face alone, as the game's card shows it.
            _m.Name = face != null && face.sprite != null && face.sprite == Fred() ? UiText.T("uncle_fred") : null;
            _m.Picture = face != null ? DialogKits.PictureOf(face.sprite) : null;
            _m.Hue = UiColour.HueOf(line ?? string.Empty);
            _m.Line = !_spoke || speaking ? line : null;   // said and sliding away: the card goes now, not two seconds later
            int total = _run + waiting;
            _m.More = total > 1 && _run > 0
                ? UiText.Fill("reallife_ui_monologue_count", "index", _run.ToString(CultureInfo.InvariantCulture), "total", total.ToString(CultureInfo.InvariantCulture))
                : null;
            _m.Progress = Progress();
            _m.Next = UiText.T(waiting > 0 ? "reallife_ui_monologue_next" : "common_close");
            _m.Skip = waiting > 0 ? UiText.T("reallife_ui_monologue_skip") : null;
            if (!speaking && waiting == 0)
            {
                _run = 0;   // the run is over: the next line starts a new one
            }
        }

        // How far the line's sound has played, 0 to 100: all of it in the second the game waits after it; -1 for a line
        // without a sound of its own (the game shows it for a fixed time), so the card shows no bar.
        private int Progress()
        {
            if (_sound == null || _sound.clip == null || _sound.clip.length <= 0f)
            {
                return -1;
            }
            if (_sound.isPlaying)
            {
                _heard = true;
                return Mathf.Clamp((int)(100f * _sound.time / _sound.clip.length), 0, 100);
            }
            return _heard ? 100 : 0;
        }

        // Uncle Fred's face as the tutorial holds it, found once.
        private Sprite Fred()
        {
            if (_fred == null && GameContract.FredFace.Field != null)
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                _fred = uis != null && uis.tutorialUI != null ? GameContract.FredFace.Field.GetValue(uis.tutorialUI) as Sprite : null;
            }
            return _fred;
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
