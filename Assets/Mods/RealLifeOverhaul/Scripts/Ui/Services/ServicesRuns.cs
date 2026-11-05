using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The runs of a page: what each button, link and step does, collected as the page is written and given to the model as an array, so a press is
    /// an index. A step takes two runs in a row (less, then more). Game-free; the source decides what a run does.
    /// </summary>
    public sealed class ServicesRuns
    {
        private readonly List<Func<bool>> _list = new List<Func<bool>>(16);

        public int Count => _list.Count;

        /// <summary>A run that does something and always counts as done (an action of the director: it tells its own refusal).</summary>
        public int Do(Action run)
        {
            _list.Add(() =>
            {
                run();
                return true;
            });
            return _list.Count - 1;
        }

        /// <summary>A button carrying a run; the kind and the key decide how it looks and which key runs it. Disabled, it carries none.</summary>
        public UiActionModel Button(string label, UiButtonKind kind, UiIcon icon, UiAction key, Action run, bool disabled = false)
        {
            return new UiActionModel
            {
                Label = label, Kind = kind, Icon = icon, Key = key, Command = ServicesFace.ActionCommand, Arg = disabled ? -1 : Do(run), Disabled = disabled
            };
        }

        /// <summary>A link line.</summary>
        public UiFactLine Link(string words, Action run) => UiFactLine.Go(words, ServicesFace.ActionCommand, Do(run));

        /// <summary>A switch line.</summary>
        public UiFactLine Switch(string label, bool on, Action<bool> set, bool disabled = false)
        {
            return UiFactLine.Toggle(label, on, ServicesFace.ActionCommand, Do(() => set(!on)), disabled);
        }

        /// <summary>A step line: a figure to move down or up one place.</summary>
        public UiFactLine Step(string label, string value, string hint, bool canLess, bool canMore, Action less, Action more)
        {
            int at = Do(less);
            Do(more);
            return UiFactLine.Amount(label, value, hint, canLess, canMore, ServicesFace.StepLess, ServicesFace.StepMore, at);
        }

        /// <summary>A chip over a table: it chooses the view, the argument the chip's place.</summary>
        public static UiActionModel Chip(string label, int command, int arg) => new UiActionModel { Label = label, Kind = UiButtonKind.Secondary, Command = command, Arg = arg, Key = UiAction.Count };

        public Func<bool>[] ToArray() => _list.ToArray();
    }
}
