using System;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the world's small windows share on the kit (the private driver, a sign's look, the preview terminal, the
    /// preview's bar, the colour picker's frame): one tool panel's model refilled from the game's window when it changes,
    /// its lines in one array kept for the window's life, a choice's options opened under it and picked in the game's own
    /// drop-down, and a look at the game's window four times a second for a change. Each window says how to read itself,
    /// where it stands and what a press does. Cost a frame: one comparison of the time; four times a second a few reads.
    /// </summary>
    public abstract class WorldToolScreen
    {
        private const float Every = 0.25f;   // s between two looks at the game's window

        protected readonly UiToolPanelModel M = new UiToolPanelModel { Lines = new UiToolLine[UiToolPanel.MostLines] };
        private bool _dirty = true;
        private float _next;
        private long _seen;

        /// <summary>The model last read, for the tests.</summary>
        public UiToolPanelModel Model => M;

        public abstract string Id { get; }

        /// <summary>The scene's name, for the gallery's and the log's lines.</summary>
        protected abstract string Scene { get; }

        public bool Dirty
        {
            get
            {
                if (Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + Every;
                    long now = Signature();
                    _dirty |= now != _seen;
                    _seen = now;
                }
                return _dirty;
            }
        }

        /// <summary>Asks for a layout on the next frame (a press changed what the game shows).</summary>
        protected void Touch() => _dirty = true;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            M.LineCount = 0;
            Read();
            if (M.Open >= M.LineCount || (M.Open >= 0 && M.Lines[M.Open].Kind != UiToolKind.Choice))
            {
                M.Open = -1;
            }
            float w = Width;
            Place(frame, w, UiToolPanel.Height(M, w), out float x, out float y);
            Draw(s, frame, dark, keys, UiDraw.SnapDown(x), UiDraw.SnapDown(y), w);
            _seen = Signature();
            _dirty = false;
        }

        /// <summary>The panel's width.</summary>
        protected virtual float Width => UiToolPanel.Width;

        /// <summary>Draws the panel at its place; a frame round a hole draws itself.</summary>
        protected virtual void Draw(UiScene s, UiFrame frame, bool dark, IUiKeys keys, float x, float y, float w)
        {
            UiToolPanel.Lay(s, M, frame, dark, keys, Scene, x, y, w);
        }

        /// <summary>Fills the model from the game's window: its title, its state and its lines (<see cref="Add"/>).</summary>
        protected abstract void Read();

        /// <summary>Where the panel's top left stands, px of the layout.</summary>
        protected abstract void Place(UiFrame frame, float w, float h, out float x, out float y);

        /// <summary>A number that changes when what the game's window shows changes.</summary>
        protected abstract long Signature();

        /// <summary>A line's press: its link, its entry's button, its main button. True when it did something.</summary>
        protected virtual bool Line(int line) => false;

        /// <summary>A Buttons line's second button.</summary>
        protected virtual bool Second(int line) => false;

        /// <summary>A Swatches line's add.</summary>
        protected virtual bool AddColour(int line) => false;

        /// <summary>A Swatches line's colour.</summary>
        protected virtual bool Colour(int line, int index) => false;

        /// <summary>A Choice line's options in the words shown, into the model (Options, OptionCount, OptionChosen).</summary>
        protected virtual void Options(int line)
        {
            M.OptionCount = 0;
            M.OptionChosen = -1;
        }

        /// <summary>A Choice line's option picked.</summary>
        protected virtual bool Pick(int line, int option) => false;

        /// <summary>Adds a line and returns it to fill; past the most lines the last is written again.</summary>
        protected ref UiToolLine Add(UiToolKind kind)
        {
            int i = Math.Min(M.LineCount, M.Lines.Length - 1);
            M.Lines[i] = new UiToolLine { Kind = kind, SwatchChosen = -1 };
            M.LineCount = i + 1;
            return ref M.Lines[i];
        }

        public bool Press(int command, int arg)
        {
            bool done;
            if (UiToolPanel.IsSwatch(command, out int swatchLine))
            {
                done = Colour(swatchLine, arg);
            }
            else
            {
                switch (command)
                {
                    case UiToolPanel.LineCommand:
                        if (arg >= 0 && arg < M.LineCount && M.Lines[arg].Kind == UiToolKind.Choice)
                        {
                            // A choice opens its options under it, or closes them; one open at a time.
                            M.Open = M.Open == arg ? -1 : arg;
                            M.OptionOffset = 0f;
                            if (M.Open >= 0)
                            {
                                Options(arg);
                            }
                            done = true;
                        }
                        else
                        {
                            M.Open = -1;
                            done = Line(arg);
                        }
                        break;
                    case UiToolPanel.SecondCommand:
                        M.Open = -1;
                        done = Second(arg);
                        break;
                    case UiToolPanel.AddCommand:
                        M.Open = -1;
                        done = AddColour(arg);
                        break;
                    case UiToolPanel.OptionCommand:
                        int open = M.Open;
                        M.Open = -1;
                        done = open >= 0 && Pick(open, arg);
                        break;
                    default:
                        done = false;
                        break;
                }
            }
            _dirty = true;
            return done;
        }

        public void Focused(int command, int arg)
        {
        }

        /// <summary>The strip's close: the game's window closed its own way. False where the game's Cancel alone closes it.</summary>
        protected virtual bool Closed() => false;

        /// <summary>An open choice closes; else the strip's close closes the game's window its own way (the game's Cancel does too).</summary>
        public bool Back()
        {
            if (M.Open < 0)
            {
                bool closed = Closed();
                _dirty |= closed;
                return closed;
            }
            M.Open = -1;
            _dirty = true;
            return true;
        }

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
            if (M.Open >= 0 && index >= 0)
            {
                float row = UiTokens.ControlSmall, view = UiDraw.ChoiceListHeight(M.OptionCount) - UiTokens.S2;
                float top = index * row;
                M.OptionOffset = top < M.OptionOffset ? top : top + row > M.OptionOffset + view ? top + row - view : M.OptionOffset;
                _dirty = true;
            }
        }

        public void Wheel(int clip, float notches)
        {
            if (M.Open >= 0)
            {
                M.OptionOffset = Math.Max(0f, M.OptionOffset + notches * UiTokens.ControlSmall * 3f);
                _dirty = true;
            }
        }

        // ------------------------------------------------------------------------------------------------- shared reads

        /// <summary>A drop-down's options as it shows them (its own words, or its keys' text), into the model.</summary>
        protected void DropdownOptions(UI.Elements.Dropdown d, GameMember options, GameMember localized)
        {
            var list = d != null && options.Field != null ? options.Field.GetValue(d) as System.Collections.Generic.List<string> : null;
            bool words = d != null && localized.Field != null && localized.Field.GetValue(d) is bool b && b;
            int n = list != null ? list.Count : 0;
            if (M.Options.Length < n)
            {
                M.Options = new string[Math.Max(n, M.Options.Length * 2)];
            }
            for (int i = 0; i < n; i++)
            {
                M.Options[i] = words ? UiText.T(list[i]) : WorldHost.Plain(list[i]) ?? string.Empty;
            }
            M.OptionCount = n;
            M.OptionChosen = d != null ? d.SelectedOptionIndex : -1;
        }

        /// <summary>A drop-down's value as it shows it, or its placeholder's words while nothing is chosen.</summary>
        protected static string DropdownValue(UI.Elements.Dropdown d, GameMember localized, string placeholderKey)
        {
            if (d == null)
            {
                return string.Empty;
            }
            string chosen = d.SelectedOption;
            if (string.IsNullOrEmpty(chosen))
            {
                return UiText.T(placeholderKey);
            }
            bool words = localized.Field != null && localized.Field.GetValue(d) is bool b && b;
            return words ? UiText.T(chosen) : WorldHost.Plain(chosen) ?? string.Empty;
        }

        /// <summary>A rect of the game's as px of the layout: its top left and its size.</summary>
        protected static bool Rect(RectTransform r, UiFrame frame, out float x, out float y, out float w, out float h)
        {
            x = y = w = h = 0f;
            if (r == null)
            {
                return false;
            }
            Canvas canvas = r.GetComponentInParent<Canvas>(true);
            Camera eye = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            r.GetWorldCorners(Corners);
            Vector2 low = RectTransformUtility.WorldToScreenPoint(eye, Corners[0]), high = RectTransformUtility.WorldToScreenPoint(eye, Corners[2]);
            Vector2 a = WorldHost.ToLayout(new Vector2(low.x, high.y), frame), b = WorldHost.ToLayout(new Vector2(high.x, low.y), frame);
            x = Math.Min(a.x, b.x);
            y = Math.Min(a.y, b.y);
            w = Math.Abs(b.x - a.x);
            h = Math.Abs(b.y - a.y);
            return w > 1f && h > 1f;
        }

        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>A hash step for a signature.</summary>
        protected static long Mix(long seen, long value) => unchecked(seen * 31 + value);
    }
}
