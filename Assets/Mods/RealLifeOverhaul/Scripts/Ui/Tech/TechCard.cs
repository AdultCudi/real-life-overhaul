using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A card of the Tech app's pages built line by line: its facts, links, steps and buttons, the runs they carry added to the page's list. Game-free.</summary>
    public sealed class TechCard
    {
        private readonly TechRuns _runs;
        private readonly List<UiFactLine> _lines = new List<UiFactLine>(12);
        private readonly List<UiActionModel> _actions = new List<UiActionModel>(3);

        public string Title, Sub, Chip;
        public bool Heading = true;
        public UiRole ChipTone = UiRole.Ok;

        public TechCard(TechRuns runs, string title = null)
        {
            _runs = runs;
            Title = title;
        }

        public int LineCount => _lines.Count;

        public TechCard Row(string label, string value, UiRole ink = UiRole.None)
        {
            _lines.Add(UiFactLine.Row(label, value, ink));
            return this;
        }

        public TechCard Say(string words, UiRole ink = UiRole.None)
        {
            if (!string.IsNullOrEmpty(words))
            {
                _lines.Add(UiFactLine.Say(words, ink));
            }
            return this;
        }

        public TechCard Tick(string words, bool met)
        {
            _lines.Add(UiFactLine.Tick(words, met));
            return this;
        }

        public TechCard Bar(string label, int fill, string figure)
        {
            _lines.Add(UiFactLine.Bar(label, fill, figure));
            return this;
        }

        public TechCard Section(string label)
        {
            _lines.Add(UiFactLine.Section(label));
            return this;
        }

        public TechCard Who(string name, string line, string value, UiRole ink = UiRole.None)
        {
            _lines.Add(UiFactLine.Who(name, line, value, ink));
            return this;
        }

        public TechCard Space()
        {
            _lines.Add(UiFactLine.Space());
            return this;
        }

        public TechCard Go(string words, TechRun run)
        {
            _lines.Add(UiFactLine.Go(words, TechFace.ActionCommand, _runs.Add(run)));
            return this;
        }

        /// <summary>A figure to move between a minus and a plus: the two runs stand side by side in the page's list, the minus first.</summary>
        public TechCard Step(string label, string value, string hint, bool canLess, bool canMore, TechRun less, TechRun more)
        {
            int at = _runs.Add(less);
            _runs.Add(more);
            _lines.Add(UiFactLine.Amount(label, value, hint, canLess, canMore, TechFace.StepLess, TechFace.StepMore, at));
            return this;
        }

        public TechCard Button(string label, UiButtonKind kind, UiAction key, TechRun run, bool disabled = false, UiIcon icon = UiIcon.None)
        {
            _actions.Add(new UiActionModel { Label = label, Kind = kind, Key = key, Icon = icon, Command = TechFace.ActionCommand, Arg = _runs.Add(run), Disabled = disabled });
            return this;
        }

        public UiFactCardModel Build() => new UiFactCardModel
        {
            Title = Title, Heading = Heading, Sub = Sub, Chip = Chip, ChipTone = ChipTone, Lines = _lines.ToArray(), Actions = _actions.ToArray()
        };
    }
}
