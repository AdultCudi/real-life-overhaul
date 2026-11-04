using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The runs of a page: what each button, link and step does, collected as the page is written and given to the model as an array, so
    /// a press is an index. A step takes two runs in a row (less, then more). Game-free; the source decides what a run does.
    /// </summary>
    public sealed class EstateRuns
    {
        private readonly List<Func<bool>> _list = new List<Func<bool>>(16);

        public int Count => _list.Count;

        /// <summary>A run; returns its index.</summary>
        public int Add(Func<bool> run)
        {
            _list.Add(run ?? (() => false));
            return _list.Count - 1;
        }

        /// <summary>A run that does something and always counts as done (an action of the director: it tells its own refusal).</summary>
        public int Do(Action run)
        {
            return Add(() =>
            {
                run();
                return true;
            });
        }

        /// <summary>A button carrying a run; the kind and the key decide how it looks and which key runs it.</summary>
        public UiActionModel Button(string label, UiButtonKind kind, UiIcon icon, UiAction key, Action run, bool disabled = false)
        {
            return new UiActionModel
            {
                Label = label, Kind = kind, Icon = icon, Key = key, Command = EstateFace.ActionCommand, Arg = disabled ? -1 : Do(run), Disabled = disabled
            };
        }

        public UiActionModel Button(string label, UiButtonKind kind, UiIcon icon, UiAction key, Func<bool> run, bool disabled = false)
        {
            return new UiActionModel
            {
                Label = label, Kind = kind, Icon = icon, Key = key, Command = EstateFace.ActionCommand, Arg = disabled ? -1 : Add(run), Disabled = disabled
            };
        }

        /// <summary>A link line.</summary>
        public UiFactLine Link(string words, Action run)
        {
            return UiFactLine.Go(words, EstateFace.ActionCommand, Do(run));
        }

        /// <summary>A switch line.</summary>
        public UiFactLine Switch(string label, bool on, Action<bool> set, bool disabled = false)
        {
            return UiFactLine.Toggle(label, on, EstateFace.ActionCommand, Do(() => set(!on)), disabled);
        }

        /// <summary>A step line: a figure to move down or up one place.</summary>
        public UiFactLine Step(string label, string value, string hint, bool canLess, bool canMore, Action less, Action more)
        {
            int at = Do(less);
            Do(more);
            return UiFactLine.Amount(label, value, hint, canLess, canMore, EstateFace.StepLess, EstateFace.StepMore, at);
        }

        public Func<bool>[] ToArray() => _list.ToArray();
    }
}
