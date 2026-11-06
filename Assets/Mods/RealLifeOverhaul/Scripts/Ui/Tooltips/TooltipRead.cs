using System;
using System.Reflection;
using TMPro;
using Tooltip;
using UI.Elements;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the game's tooltip says now, read into the card's model. The game's TooltipSystem builds each tooltip by cloning a template
    /// for every header, label, bar, list and picture it is given (named by the template's own name and " Clone", in the order given), so
    /// the card reads those clones after the game and other mods filled them, never through a patch. Read once when the clones change (a
    /// count and the last clone's id are looked at each frame, a few reads), never per frame. A picture, or a clone this version does not
    /// know, cannot be drawn: <see cref="Read"/> then says so and the game's own tooltip stays. Cost: a read a tooltip shown.
    /// </summary>
    internal sealed class TooltipRead
    {
        private const string CloneSuffix = " Clone";
        private const string EntryName = "Entry";

        private Transform _content, _imageParent;
        private string _splitter, _header, _category, _label, _bar, _pairs, _list, _checks, _demands, _image;
        private bool _bound;

        /// <summary>The system's content parent (what the game hides under the card), or null while it is not bound.</summary>
        internal Transform Content => _content;

        /// <summary>True once the system's templates were found as this version expects.</summary>
        internal bool Bound => _bound;

        /// <summary>Finds the templates and their clones' names; a reason when the system is not built as expected, else null.</summary>
        internal string Bind(object system)
        {
            _bound = false;
            var header = Template(GameContract.TipHeader, system);
            var splitter = Template(GameContract.TipSplitter, system);
            var category = Template(GameContract.TipCategory, system);
            var label = Template(GameContract.TipLabel, system);
            var bar = Template(GameContract.TipBar, system);
            var pairs = Template(GameContract.TipPairs, system);
            var list = Template(GameContract.TipList, system);
            var checks = Template(GameContract.TipChecks, system);
            var demands = Template(GameContract.TipDemands, system);
            var image = GameContract.TipImage.Field != null ? GameContract.TipImage.Field.GetValue(system) as Image : null;
            if (header == null || splitter == null || category == null || label == null || bar == null || pairs == null || list == null || checks == null || image == null)
            {
                return "its templates are not all there";
            }
            _content = header.parent;
            if (_content == null || splitter.parent != _content || category.parent != _content || label.parent != _content || bar.parent != _content || pairs.parent != _content
                || list.parent != _content || checks.parent != _content || (demands != null && demands.parent != _content))
            {
                return "its templates stand in different places";
            }
            _imageParent = image.transform.parent;
            _splitter = splitter.name + CloneSuffix;
            _header = header.name + CloneSuffix;
            _category = category.name + CloneSuffix;
            _label = label.name + CloneSuffix;
            _bar = bar.name + CloneSuffix;
            _pairs = pairs.name + CloneSuffix;
            _list = list.name + CloneSuffix;
            _checks = checks.name + CloneSuffix;
            _demands = demands != null ? demands.name + CloneSuffix : null;
            _image = image.name + CloneSuffix;
            _bound = true;
            return null;
        }

        private static Transform Template(GameMember member, object system) => member.Field != null ? member.Field.GetValue(system) as Transform : null;

        /// <summary>
        /// A reading of how the content stands: the clones' count and the last one's id. It changes when the game builds another tooltip (its
        /// old clones are destroyed and new ones made), so the card is read again; two reads of a parent, nothing allocated.
        /// </summary>
        internal int Signature()
        {
            int count = _content.childCount;
            int last = count > 0 ? _content.GetChild(count - 1).GetInstanceID() : 0;
            int image = _imageParent != null && _imageParent != _content ? _imageParent.childCount : 0;
            return (count * 397) ^ last ^ (image << 20);
        }

        /// <summary>
        /// The tooltip's words into a model. False when something in it cannot be drawn (a picture, a clone of a kind this version does not know):
        /// the game's own tooltip then stays on show.
        /// </summary>
        internal bool Read(UiTooltipModel into)
        {
            into.Clear();
            if (_imageParent != null && HasClone(_imageParent, _image))
            {
                return false;
            }
            for (int i = 0; i < _content.childCount; i++)
            {
                Transform c = _content.GetChild(i);
                if (!c.gameObject.activeSelf)
                {
                    continue;   // a template, or a clone the game is destroying
                }
                string name = c.name;
                if (name == _splitter)
                {
                    into.Rule();
                }
                else if (name == _header)
                {
                    Header(into, c);
                }
                else if (name == _category)
                {
                    Category(into, c);
                }
                else if (name == _label)
                {
                    Label(into, c);
                }
                else if (name == _bar)
                {
                    Bar(into, c);
                }
                else if (name == _pairs)
                {
                    Pairs(into, c);
                }
                else if (name == _list)
                {
                    List(into, c);
                }
                else if (name == _checks || (_demands != null && name == _demands))
                {
                    Checks(into, c, name == _demands);
                }
                else if (name.EndsWith(CloneSuffix, StringComparison.Ordinal))
                {
                    return false;   // a clone of a template this version does not list
                }
            }
            Tidy(into);
            return true;
        }

        private static bool HasClone(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == name)
                {
                    return true;
                }
            }
            return false;
        }

        private static void Header(UiTooltipModel m, Transform c)
        {
            string first = Words(c.Find("FirstLabel")), second = Words(c.Find("SecondLabel"));
            m.Header(string.IsNullOrEmpty(first) ? second : first, string.IsNullOrEmpty(first) ? null : second);
        }

        private static void Category(UiTooltipModel m, Transform c)
        {
            Transform title = c.Find("Title");
            string words = title != null ? Words(title) : Words(c);
            if (!string.IsNullOrEmpty(words))
            {
                m.Category(words);
            }
        }

        private static void Label(UiTooltipModel m, Transform c)
        {
            TMP_Text text = c.GetComponent<TMP_Text>();
            string words = WorldHost.Plain(text != null ? text.text : null);
            if (string.IsNullOrEmpty(words))
            {
                return;
            }
            bool upper = (text.fontStyle & FontStyles.UpperCase) != 0;
            // the game's labels carry their own line breaks: one block a line
            foreach (string line in words.Split('\n'))
            {
                string one = line.Trim();
                if (one.Length > 0)
                {
                    m.Text(upper ? one.ToUpperInvariant() : one, Ink(text.color), (text.fontStyle & FontStyles.Bold) != 0, ((int)text.alignment & 0x2) != 0);
                }
            }
        }

        private static void Bar(UiTooltipModel m, Transform c)
        {
            ProgressBar bar = c.GetComponent<ProgressBar>();
            if (bar == null || bar.slider == null)
            {
                return;
            }
            int value = Mathf.Clamp(Mathf.RoundToInt(bar.slider.value * 100f), 0, 100);
            Color color = bar.sliderFillImage != null ? bar.sliderFillImage.color : Color.white;
            m.Meter(null, bar.label != null ? bar.label.text : value + "%", value, Fill(color));
        }

        private static void Pairs(UiTooltipModel m, Transform c)
        {
            UiTipBlock block = m.Rows();
            bool any = false;
            for (int i = 0; i < c.childCount; i++)
            {
                Transform e = c.GetChild(i);
                if (!e.gameObject.activeSelf || e.name == EntryName)
                {
                    continue;
                }
                Transform right = e.Find("Right");
                TMP_Text rt = right != null ? right.GetComponent<TMP_Text>() : null;
                string value = WorldHost.Plain(rt != null ? rt.text : null);
                block.Lines.Add(new UiTipLine { Left = Words(e.Find("Left")), Right = value, Ink = rt != null ? Ink(rt.color) : UiRole.None });
                any |= !string.IsNullOrEmpty(value);
            }
            if (!any)
            {
                block.Kind = UiTipKind.List;   // the game's list of words with no value beside them
            }
            if (block.Lines.Count == 0)
            {
                m.Blocks.Remove(block);
            }
        }

        private static void List(UiTooltipModel m, Transform c)
        {
            UiTipBlock block = m.Rows(UiTipKind.List);
            for (int i = 0; i < c.childCount; i++)
            {
                Transform e = c.GetChild(i);
                if (!e.gameObject.activeSelf || e.name == EntryName)
                {
                    continue;
                }
                string words = Words(e);
                if (!string.IsNullOrEmpty(words))
                {
                    block.Lines.Add(new UiTipLine { Left = words.StartsWith("- ", StringComparison.Ordinal) ? words.Substring(2) : words });
                }
            }
            if (block.Lines.Count == 0)
            {
                m.Blocks.Remove(block);
            }
        }

        private static void Checks(UiTooltipModel m, Transform c, bool demands)
        {
            UiTipBlock block = m.Rows(UiTipKind.Checks);
            for (int i = 0; i < c.childCount; i++)
            {
                Transform e = c.GetChild(i);
                if (!e.gameObject.activeSelf || e.name == EntryName)
                {
                    continue;
                }
                Transform toggle = e.Find("Toggle");
                Toggle t = toggle != null ? toggle.GetComponent<Toggle>() : null;
                var line = new UiTipLine { On = t != null && t.isOn };
                if (demands)
                {
                    line.Left = Words(e.Find("DemandName"));
                    Transform priority = e.Find("DemandPriority");
                    TMP_Text pt = priority != null ? priority.GetComponent<TMP_Text>() : null;
                    line.Right = WorldHost.Plain(pt != null ? pt.text : null);
                    line.Ink = pt != null ? Ink(pt.color) : UiRole.None;
                }
                else
                {
                    line.Left = Words(e);
                }
                if (!string.IsNullOrEmpty(line.Left))
                {
                    block.Lines.Add(line);
                }
            }
            if (block.Lines.Count == 0)
            {
                m.Blocks.Remove(block);
            }
        }

        // A rule is never first, last or doubled: the game's splitters stand between lines that may have been left out.
        private static void Tidy(UiTooltipModel m)
        {
            for (int i = m.Blocks.Count - 1; i >= 0; i--)
            {
                bool rule = m.Blocks[i].Kind == UiTipKind.Rule;
                if (rule && (i == 0 || i == m.Blocks.Count - 1 || m.Blocks[i - 1].Kind == UiTipKind.Rule))
                {
                    m.Blocks.RemoveAt(i);
                }
            }
        }

        // The words of a clone (a text on it or on its first text below it), with the game's rich text taken off.
        private static string Words(Transform t)
        {
            if (t == null || !t.gameObject.activeSelf)
            {
                return null;
            }
            TMP_Text text = t.GetComponent<TMP_Text>();
            if (text == null)
            {
                text = t.GetComponentInChildren<TMP_Text>(false);
            }
            return WorldHost.Plain(text != null ? text.text : null);
        }

        // The ink for a colour the game gave a word: its own white and greys are the card's plain ink.
        private static UiRole Ink(Color c)
        {
            float hi = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), lo = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            return hi - lo < 0.14f ? UiRole.None : WorldHost.Tone(c);
        }

        // The meter's fill for the game's colour for it: its green, yellow and red, else the cyan.
        private static UiRole Fill(Color c)
        {
            switch (WorldHost.Tone(c))
            {
                case UiRole.OkText: return UiRole.Ok;
                case UiRole.WarnText: return UiRole.Warn;
                case UiRole.DangerText: return UiRole.Danger;
            }
            return UiRole.Accent;
        }
    }
}
