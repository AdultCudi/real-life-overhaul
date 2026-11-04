using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Builds a work app's page from its parts: the buttons with what each does, the rows with what each opens. Every button is drawn from a
    /// WorkDo: one that cannot work now is drawn disabled and its reason is said (never a dead button), so the checks can walk every
    /// action of every state and know each one acts or says why not. Game-free.
    /// </summary>
    public sealed class WorkBuilder
    {
        public readonly WorkPage Page = new WorkPage();
        private readonly List<UiActionModel> _buttons = new List<UiActionModel>(8);
        private readonly List<WorkDo> _acts = new List<WorkDo>(8);
        private readonly List<string> _whys = new List<string>(2);
        public readonly UtilitiesWords W;

        public WorkBuilder(UtilitiesWords words)
        {
            W = words;
        }

        public UiAppModel M => Page.Model;

        /// <summary>How many buttons the page has so far, so a page can say which stand in its head.</summary>
        public int Count => _buttons.Count;

        /// <summary>A button: its label, kind, glyph, key and what it does. With a reason in the action it is drawn disabled and the reason goes under the lead.</summary>
        public void Button(string label, UiButtonKind kind, UiIcon icon, UiAction key, WorkDo act)
        {
            bool off = act.Why != null || !act.Any;
            _buttons.Add(new UiActionModel { Label = label, Kind = kind, Icon = icon, Key = key, Command = UiAppWindow.ActionCommand,
                Arg = _buttons.Count, Disabled = off });
            _acts.Add(act);
            if (act.Why != null)
            {
                _whys.Add(act.Why);
            }
        }

        /// <summary>A filter chip: a choice of the page, drawn as the kit's chip, chosen in the accent.</summary>
        public void Chip(string label, bool chosen, WorkDo act)
        {
            _buttons.Add(new UiActionModel { Label = label, Kind = UiButtonKind.Chip, Icon = chosen ? UiIcon.Check : UiIcon.None,
                Key = UiAction.Count, Command = UiAppWindow.ActionCommand, Arg = _buttons.Count });
            _acts.Add(act);
        }

        public void Table(string title, UiColumn[] columns, int graded, UiAppRow[] rows, WorkDo[] opens, int chosen)
        {
            M.TableTitle = title;
            M.Columns = columns;
            M.GradeColumn = graded;
            M.Rows = rows;
            Page.Rows = opens ?? new WorkDo[rows.Length];
            Page.Chosen = chosen;
        }

        public void People(string title, string count, UiPersonRow[] people, WorkDo[] opens)
        {
            M.PeopleTitle = title;
            M.PeopleCount = count;
            M.People = people;
            Page.People = opens ?? new WorkDo[people.Length];
        }

        /// <summary>
        /// The one thing the page is about (the chosen row, or an empty page's sentence) as the hero: its glyph, a kicker, its name and one line,
        /// each cut to a whole word that fits, so no text runs out of its box.
        /// </summary>
        public void About(UiIcon icon, string kicker, string title, string line)
        {
            M.Hero = new UiHeroModel
            {
                Icon = icon, Tint = UiRole.None, Kicker = string.IsNullOrEmpty(kicker) ? null : UiTextRules.Cut(kicker.ToUpperInvariant(),
                    UiStyle.SectionHeader, HeroKicker),
                Title = UiTextRules.Cut(title ?? string.Empty, UiStyle.Display, HeroTitle), Line = UiTextRules.Cut(line ?? string.Empty,
                    UiStyle.Body, HeroLine), Progress = -1
            };
        }

        public const float HeroKicker = 420f, HeroTitle = 520f, HeroLine = 640f, LeadWidth = 360f;

        /// <summary>A sentence for the lead; with reasons of disabled buttons it says them after it.</summary>
        public void Lead(string next)
        {
            M.Next = string.IsNullOrEmpty(next) ? next : UiTextRules.Cut(next, UiStyle.BodyStrong, LeadWidth);
        }

        public WorkPage Done()
        {
            M.Actions = _buttons.ToArray();
            Page.Acts = _acts.ToArray();
            if (_whys.Count > 0)
            {
                string said = string.Join(" ", _whys);
                M.Next = UiTextRules.Cut(string.IsNullOrEmpty(M.Next) ? said : M.Next + " " + said, UiStyle.BodyStrong, LeadWidth);
            }
            return Page;
        }

        // ------------------------------------------------------------------------------------------------- small helpers

        public static UiColumn Col(string title, float width = 0f, bool right = false, bool grow = false) => new UiColumn { Title = title,
            Width = width, Right = right, Grow = grow };

        public static UiAppRow Row(UiIcon icon, UiRole tint, string[] cells, string status = null, UiRole tone = UiRole.Ok, int grade = -1)
        {
            return new UiAppRow { Icon = icon, Tint = tint, Cells = cells, Status = status, StatusTone = tone, Grade = grade };
        }

        public static WorkDo Do(int kind, int arg = 0, string why = null, bool opens = false) => new WorkDo { Kind = kind, Arg = arg, Why = why,
            Opens = opens };

        public static string Percent(int value) => (value >= 0 ? "+" : string.Empty) + value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";

        public static string Number(int n) => n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }
}
