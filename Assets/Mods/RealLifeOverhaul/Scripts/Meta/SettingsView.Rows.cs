using System;
using System.Collections.Generic;
using System.Text;
using Localizor;
using RealLifeOverhaul.GameUi;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// The Settings app's rows: a page written from its lines, each line a row of the game's settings menu kept for the next
    /// page, and a row written again when it changes. Runs on a click; nothing a frame.
    /// </summary>
    public sealed partial class SettingsView
    {
        private const string GuideSection = "guide";      // the first page of Start here: what the app is
        private const string PresetsSection = "presets";
        private const string KeysSection = "keys";

        /// <summary>A row on the page: the game's row it is drawn with and the line it stands for.</summary>
        private sealed class Slot
        {
            public OptionRow Row;
            public OptionRowKind Made;     // the kind of row it is, for its pool
            public SettingsLine Line;      // null for a row of the app's own (Apply, List the differences, About this section)
            public ControlKind Kind;
            public Action Run;             // what a row of the app's own does
            public int Low, High;          // a slider's ends
            public string Extra;           // a line the page adds to the description (what a preset sets)
            public Action<int> Picked;     // a drop-down's choice: made once with the row
        }

        private static readonly string WarnHex = DarkLook.Hex(DarkLook.Warn);
        private static readonly string MutedHex = DarkLook.Hex(DarkLook.Muted);
        private static readonly string GoodHex = DarkLook.Hex(DarkLook.Good);

        private readonly List<Slot> _shown = new List<Slot>(48);
        private readonly List<Slot>[] _free = { new List<Slot>(16), new List<Slot>(16), new List<Slot>(8), new List<Slot>(16) };   // by OptionRowKind
        private readonly List<TMP_Text> _texts = new List<TMP_Text>(8);
        private readonly List<TMP_Text> _freeParagraphs = new List<TMP_Text>(4);
        private readonly List<TMP_Text> _freeSmalls = new List<TMP_Text>(8);
        private readonly List<TMP_Text> _smalls = new List<TMP_Text>(8);   // which of _texts are small capitals
        private readonly List<string> _choiceWords = new List<string>(8);
        private readonly StringBuilder _note = new StringBuilder(256);
        private bool _filling;     // a row is being written: its control's own events are not the player's

        // ---------------------------------------------------------------- a page

        private void WritePage(SettingsPage page)
        {
            SettingsLine heading = page.Heading;
            TreeSection section = heading.Section;
            Topic(page.Category, CategoryName(page.Category), OptionsLayout.AppLabel(heading), CountOf(page));
            string id = section != null && !page.IsTests ? section.Id : null;
            if (page.IsTests)
            {
                Paragraph(T("reallife_set_box_tests"));
            }
            else if (id == GuideSection)
            {
                Paragraph(T("reallife_set_box_same_page"));
            }
            else if (id == PresetsSection && _closest >= 0)
            {
                string name = PresetName(_closest);
                Paragraph(_exact ? T("reallife_set_box_same", "preset", name) : T("reallife_set_box_differ", "preset", name, "count", _counts[_closest].ToString()));
            }
            else if (id == KeysSection)
            {
                Paragraph(T("reallife_set_keys_note"));
            }
            foreach (SettingsLine line in page.Lines)
            {
                if (OptionsLayout.AppShown(line))
                {
                    Put(line);
                }
            }
            if (id == PresetsSection && _closest >= 0 && !_exact)
            {
                Own(T("reallife_set_list_differ"), null, "reallife_opt_caption_open", () =>
                {
                    _mode = ModeDiffer;
                    Refresh();
                });
            }
            string topic = !page.IsTests ? OptionsLayout.AppGuideTopic(section) : null;
            if (topic != null)
            {
                Own("reallife_opt_about".GetLocalization(), null, "reallife_opt_caption_open", () => Guide.Open(topic));
            }
        }

        private static string CategoryName(int category)
        {
            SettingsTab[] tabs = OptionsLayout.AppTabs;
            return (category >= 0 && category < tabs.Length ? tabs[category].Group.TitleKey : "reallife_meta_title_tests").GetLocalization();
        }

        // The note at the right of a page's header: "4 of 6 on", or the number of test tools.
        private static string CountOf(SettingsPage page)
        {
            int on = 0, all = 0;
            foreach (SettingsLine line in page.Lines)
            {
                if (!OptionsLayout.AppShown(line))
                {
                    continue;
                }
                if (page.IsTests)
                {
                    all++;
                }
                else if (line.Kind == LineKind.Option && OptionsLayout.AppType(line) == OptionType.Toggle)
                {
                    all++;
                    on += OptionsLayout.AppValue(line) != 0 ? 1 : 0;
                }
            }
            if (all == 0)
            {
                return string.Empty;
            }
            return page.IsTests ? T("reallife_set_tools", "count", all.ToString()) : T("reallife_set_count", "on", on.ToString(), "total", all.ToString());
        }

        // ---------------------------------------------------------------- the rows

        private static OptionRowKind RowKindOf(ControlKind kind)
        {
            switch (kind)
            {
                case ControlKind.Switch: return OptionRowKind.Switch;
                case ControlKind.Slider: return OptionRowKind.Slider;
                case ControlKind.Choice: return OptionRowKind.Choice;
                default: return OptionRowKind.Button;
            }
        }

        // A line of the options page as a row: the game's control of its kind, written with the line's words and value.
        private Slot Put(SettingsLine line)
        {
            ControlKind kind = OptionsLayout.AppControl(line);
            Slot slot = Take(RowKindOf(kind));
            slot.Line = line;
            slot.Kind = kind;
            Fill(slot);
            return slot;
        }

        // A row of the app's own: a button that does something here (Apply a preset, List the differences, About this section).
        private void Own(string name, string note, string captionKey, Action run)
        {
            Slot slot = Take(OptionRowKind.Button);
            slot.Kind = ControlKind.Action;
            slot.Run = run;
            SetText(slot.Row.Name, name);
            SetNote(slot.Row, note);
            SetText(slot.Row.Caption, captionKey.GetLocalization());
            slot.Row.SetUsable(true);
        }

        private Slot Take(OptionRowKind kind)
        {
            List<Slot> free = _free[(int)kind];
            Slot slot;
            if (free.Count > 0)
            {
                slot = free[free.Count - 1];
                free.RemoveAt(free.Count - 1);
            }
            else
            {
                slot = Make(kind);
            }
            slot.Row.Show(true);
            slot.Row.Root.transform.SetAsLastSibling();
            _shown.Add(slot);
            return slot;
        }

        // A new row of the game's, made under the page while it shows; its control's events are wired once, for every line it will stand for.
        private Slot Make(OptionRowKind kind)
        {
            var slot = new Slot { Row = _kit.Make(kind, _frame.Page), Made = kind };
            switch (kind)
            {
                case OptionRowKind.Switch:
                    slot.Row.Switch.onValueChanged.AddListener(on => UiClone.Safe(() => Switched(slot, on)));
                    break;
                case OptionRowKind.Slider:
                    slot.Row.Slider.onValueChanged.AddListener(value => UiClone.Safe(() => Slid(slot, value)));
                    break;
                case OptionRowKind.Choice:
                    slot.Picked = index => Picked(slot, index);   // (the drop-down calls it inside UiClone.Safe: GameUi.FilterDrop)
                    break;
                default:
                    UiClone.Rewire(slot.Row.Button, () => Pressed(slot));
                    break;
            }
            return slot;
        }

        private void ClearPage()
        {
            for (int i = 0; i < _shown.Count; i++)
            {
                Slot slot = _shown[i];
                if (slot.Row.Choice != null)
                {
                    slot.Row.Choice.Close();
                }
                slot.Row.Show(false);
                slot.Line = null;
                slot.Run = null;
                slot.Extra = null;
                _free[(int)slot.Made].Add(slot);
            }
            _shown.Clear();
            for (int i = 0; i < _texts.Count; i++)
            {
                TMP_Text text = _texts[i];
                text.gameObject.SetActive(false);
                (_smalls.Contains(text) ? _freeSmalls : _freeParagraphs).Add(text);
            }
            _texts.Clear();
            _smalls.Clear();
        }

        // A paragraph of the page's own, in the help's text.
        private void Paragraph(string words)
        {
            TMP_Text text = Reuse(_freeParagraphs) ?? _frame.NewParagraph();
            Place(text, words);
        }

        // Small capitals over the rows that follow (a search's group and section).
        private void Small(string words)
        {
            TMP_Text text = Reuse(_freeSmalls) ?? _frame.NewSmall();
            if (text == null)
            {
                Paragraph(words);
                return;
            }
            _smalls.Add(text);
            Place(text, words);
        }

        private static TMP_Text Reuse(List<TMP_Text> free)
        {
            if (free.Count == 0)
            {
                return null;
            }
            TMP_Text text = free[free.Count - 1];
            free.RemoveAt(free.Count - 1);
            return text;
        }

        private void Place(TMP_Text text, string words)
        {
            text.gameObject.SetActive(true);
            text.transform.SetAsLastSibling();
            SetText(text, words);
            _texts.Add(text);
        }

        // A row's words, control and state, as the line is now.
        private void Fill(Slot slot)
        {
            SettingsLine line = slot.Line;
            _filling = true;
            try
            {
                SetText(slot.Row.Name, OptionsLayout.AppLabel(line));
                string aside = line.Kind == LineKind.Option ? OptionsLayout.AppAside(line) : null;
                string blocker = line.Kind == LineKind.Option && aside == null ? OptionsLayout.AppBlocker(line) : null;
                FillControl(slot);
                SetNote(slot.Row, Describe(slot, aside, blocker));
                slot.Row.SetUsable(aside == null && blocker == null);
            }
            finally
            {
                _filling = false;
            }
        }

        private void FillControl(Slot slot)
        {
            SettingsLine line = slot.Line;
            OptionRow row = slot.Row;
            switch (slot.Kind)
            {
                case ControlKind.Switch:
                    row.Switch.SetIsOnWithoutNotify(OptionsLayout.AppValue(line) != 0);
                    break;
                case ControlKind.Slider:
                    OptionsLayout.AppRange(line, out int min, out int max);
                    int value = SettingsRules.SliderStart(min, max, OptionsLayout.AppValue(line), out slot.Low, out slot.High);
                    row.Slider.wholeNumbers = true;   // the option's steps: whole numbers, as on the Mods tab
                    row.Slider.minValue = slot.Low;
                    row.Slider.maxValue = slot.High;
                    row.Slider.SetValueWithoutNotify(value);
                    SetText(row.Value, OptionsLayout.AppValueText(line, value));
                    break;
                case ControlKind.Choice:
                    OptionsLayout.AppRange(line, out int none, out int last);
                    _choiceWords.Clear();
                    for (int i = none; i <= last; i++)
                    {
                        _choiceWords.Add(OptionsLayout.AppValueText(line, i));
                    }
                    row.Choice.Set(_choiceWords, OptionsLayout.AppValue(line), slot.Picked);
                    break;
                case ControlKind.Key:
                    int action = OptionsLayout.AppKeyAction(line);
                    SetText(row.Caption, action >= 0 ? Keys.Label((KeyAction)action) : "-");
                    break;
                default:
                    SetText(row.Caption, (line.Kind == LineKind.Test ? "reallife_opt_caption_run" : OptionsLayout.AppCaptionKey(line)).GetLocalization());
                    break;
            }
        }

        // The short description under a row's name: the option's own sentence, then what keeps it from working, when it takes
        // effect and its default when it is not at it; a preset's sentence and how far the choices are from it; who else uses a key.
        private string Describe(Slot slot, string aside, string blocker)
        {
            SettingsLine line = slot.Line;
            _note.Length = 0;
            switch (line.Kind)
            {
                case LineKind.Option:
                    Add(OptionsLayout.AppTip(line), null);
                    if (aside != null)
                    {
                        Add(aside, WarnHex);
                    }
                    else if (blocker != null)
                    {
                        Add(T("reallife_set_box_needs_line", "option", OptionsLayout.AppLabelOf(blocker)), WarnHex);
                    }
                    if (OptionsLayout.AppNeedsReload(line))
                    {
                        Add(T("reallife_set_reload_note"), MutedHex);
                    }
                    int standard = OptionsLayout.AppDefault(line);
                    if (OptionsLayout.AppValue(line) != standard)
                    {
                        Add(T("reallife_set_default_is", "value", OptionsLayout.AppValueText(line, standard)), MutedHex);
                    }
                    break;
                case LineKind.Button:
                    int preset = PresetOf(line);
                    if (preset >= 0)
                    {
                        Add(PresetAbout[preset].GetLocalization(), null);
                        if (_covers[preset] > 0)
                        {
                            Add(_counts[preset] == 0 ? T("reallife_set_in_use") : T("reallife_set_differ_count", "count", _counts[preset].ToString()), _counts[preset] == 0 ? GoodHex : MutedHex);
                        }
                    }
                    break;
                case LineKind.Key:
                    int action = OptionsLayout.AppKeyAction(line);
                    Add(action >= 0 ? Keys.Clash((KeyAction)action, string.Empty, " ") : null, WarnHex);
                    break;
            }
            Add(slot.Extra, MutedHex);
            return _note.ToString();
        }

        private void Add(string words, string colour)
        {
            if (string.IsNullOrEmpty(words))
            {
                return;
            }
            if (_note.Length > 0)
            {
                _note.Append('\n');
            }
            if (colour != null)
            {
                _note.Append("<color=").Append(colour).Append('>').Append(words.TrimEnd()).Append("</color>");
            }
            else
            {
                _note.Append(words);
            }
        }

        private static void SetNote(OptionRow row, string note)
        {
            if (row.Note == null)
            {
                return;
            }
            bool shown = !string.IsNullOrEmpty(note);
            if (row.Note.gameObject.activeSelf != shown)
            {
                row.Note.gameObject.SetActive(shown);
            }
            if (shown)
            {
                SetText(row.Note, note);
            }
        }

        // ---------------------------------------------------------------- the player's changes

        private void Switched(Slot slot, bool on)
        {
            if (_filling || slot.Line == null)
            {
                return;
            }
            Set(slot.Line, on ? 1 : 0);
            Moved(slot);
        }

        private void Slid(Slot slot, float dragged)
        {
            if (_filling || slot.Line == null)
            {
                return;
            }
            int value = SettingsRules.Snap(dragged, slot.Low, slot.High);
            if (value == OptionsLayout.AppValue(slot.Line))
            {
                return;
            }
            Set(slot.Line, value);
            _filling = true;
            try
            {
                SetText(slot.Row.Value, OptionsLayout.AppValueText(slot.Line, OptionsLayout.AppValue(slot.Line)));
                SetNote(slot.Row, Describe(slot, null, null));
            }
            finally
            {
                _filling = false;
            }
            Compare();
            WriteStatus();
        }

        private void Picked(Slot slot, int index)
        {
            if (_filling || slot.Line == null)
            {
                return;
            }
            Set(slot.Line, index);
            Moved(slot);
        }

        private void Pressed(Slot slot)
        {
            if (slot.Run != null)
            {
                slot.Run();
                return;
            }
            if (slot.Line == null)
            {
                return;
            }
            if (slot.Kind == ControlKind.Key)
            {
                int action = OptionsLayout.AppKeyAction(slot.Line);
                if (action >= 0)
                {
                    Keys.Rebind((KeyAction)action);   // its row says "press a key" until the key comes (KeyShown)
                }
                return;
            }
            OptionsLayout.AppRun(slot.Line);   // a preset, an import or a reset reads every option again: Changed() writes the rows
        }

        // A switch or a choice changed: the rows on the page that hang on it say so at once, and the header's count and the preset in use follow.
        private void Moved(Slot changed)
        {
            Compare();
            WriteStatus();
            for (int i = 0; i < _shown.Count; i++)
            {
                Slot slot = _shown[i];
                if (slot.Line != null && slot.Line.Kind != LineKind.Key)
                {
                    Fill(slot);
                }
            }
            if (_mode == ModePage)
            {
                SetText(_frame.TopicNote, CountOf(_pages[_page]));
            }
            if (OptionsLayout.AppId(changed.Line) == Settings.ShowTestToolsId)
            {
                ShowList();   // the test tools' category comes and goes with its switch
            }
        }

        // Every value was read again: each row shown written as it now is, and the list (a reset may show or hide the test tools).
        private void RefillAll()
        {
            Compare();
            WriteStatus();
            for (int i = 0; i < _shown.Count; i++)
            {
                if (_shown[i].Line != null)
                {
                    Fill(_shown[i]);
                }
            }
            if (_mode == ModePage && _page >= 0)
            {
                SetText(_frame.TopicNote, CountOf(_pages[_page]));
            }
            ShowList();
        }

        // A key's row while a new key is waited for, and when it has come (Keys.RowShown).
        private void KeyShown(KeyAction action, bool waiting)
        {
            if (!_ready)
            {
                return;
            }
            for (int i = 0; i < _shown.Count; i++)
            {
                Slot slot = _shown[i];
                if (slot.Kind == ControlKind.Key && slot.Line != null && OptionsLayout.AppKeyAction(slot.Line) == (int)action)
                {
                    SetText(slot.Row.Caption, waiting ? "reallife_keys_press".GetLocalization() : Keys.Label(action));
                    SetNote(slot.Row, waiting ? null : Describe(slot, null, null));
                }
            }
        }

        private void CloseChoices()
        {
            for (int i = 0; i < _shown.Count; i++)
            {
                if (_shown[i].Row.Choice != null)
                {
                    _shown[i].Row.Choice.Close();
                }
            }
        }

        private Slot SlotOf(SettingsLine line)
        {
            for (int i = 0; i < _shown.Count; i++)
            {
                if (_shown[i].Line == line)
                {
                    return _shown[i];
                }
            }
            return null;
        }

        // For the check line: how many rows of each kind the page has.
        private string CountShown()
        {
            int switches = 0, sliders = 0, choices = 0, actions = 0, keys = 0;
            for (int i = 0; i < _shown.Count; i++)
            {
                switch (_shown[i].Kind)
                {
                    case ControlKind.Switch: switches++; break;
                    case ControlKind.Slider: sliders++; break;
                    case ControlKind.Choice: choices++; break;
                    case ControlKind.Key: keys++; break;
                    default: actions++; break;
                }
            }
            return _shown.Count + " rows (" + switches + " switches, " + sliders + " sliders, " + choices + " choices, " + actions + " buttons, " + keys + " keys)";
        }

        private void Set(SettingsLine line, int value)
        {
            if (line != null && OptionsLayout.AppSet(line, value))
            {
                _wrote = true;
                if (Dev.Verbose && OptionsLayout.AppType(line) != OptionType.Slider)
                {
                    MetaLog.Info("Meta check (settings app): " + OptionsLayout.AppId(line) + " set to " + OptionsLayout.AppValue(line) + " (asked " + value + "), the same key the Mods tab saves: "
                                 + (OptionsLayout.AppValue(line) == value ? "PASS" : "CLAMPED") + ".");
                }
            }
        }

        // Where a line stands on the Mods tab: "Your character and home, Needs and wellbeing".
        private static string Where(SettingsLine line)
        {
            string group = OptionsTree.Groups[line.Tab].TitleKey.GetLocalization();
            if (line.Kind == LineKind.Test || line.Kind == LineKind.TestHeading)
            {
                return group + ", " + "reallife_meta_title_tests".GetLocalization();
            }
            return line.Section != null ? group + ", " + line.Section.TitleKey.GetLocalization() : group;
        }
    }
}
