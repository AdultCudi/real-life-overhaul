using System;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The self-test's way into a host (Diagnostics/SelfTest): a press, typed words and the wheel go in by the host's own
    /// private paths, the ones a click, the keyboard and the mouse's wheel use, so a press in a test is a real press. Read
    /// only by a running plan. COST: nothing unless a plan asks; a search of the scene's targets on each press.
    /// </summary>
    public sealed partial class UiHost
    {
        private bool _lastPressDone;   // what the last press came to: the screen's own answer, true for a press the host answers itself

        /// <summary>Presses the target of the screen with that command and argument (the topmost when several); false, with why, when there is none or it is disabled.</summary>
        internal bool SelfTestPress(int command, int arg, out string why)
        {
            why = null;
            int found = Find(command, arg);
            if (found < 0)
            {
                why = "no target with command " + command + " and argument " + arg + " is on the screen now (it has " + (_scene != null ? _scene.NodeCount : 0) + " targets)";
                return false;
            }
            if (_scene.Nodes[found].Disabled)
            {
                why = "the target " + command + "/" + arg + " is there but disabled";
                return false;
            }
            PressNode(found, Time.unscaledTime);
            if (!_lastPressDone)
            {
                why = "the screen answered false to the press of " + command + "/" + _scene.Nodes[found].Arg + " (a refusal: Player.log names the control and why)";
            }
            return _lastPressDone;
        }

        /// <summary>Types words into the screen's field as the keyboard does (its field opened by a press of its own target, then the text set, which the field hears as typing).</summary>
        internal bool SelfTestType(string text, bool submit, out string why)
        {
            why = null;
            if (!(_screen is IUiTyping typing))
            {
                why = "the screen '" + (_screen != null ? _screen.Id : "none") + "' takes no typed words";
                return false;
            }
            if (!_typing)
            {
                int at = Find(typing.TypingCommand, -1);
                if (at < 0)
                {
                    why = "the screen's field has no target on the page now (command " + typing.TypingCommand + ")";
                    return false;
                }
                PressNode(at, Time.unscaledTime);
            }
            if (_field == null || !_typing)
            {
                why = "the field did not open";
                return false;
            }
            _field.text = text ?? string.Empty;
            if (submit)
            {
                Submitted(_field.text);
            }
            EndTyping();
            return true;
        }

        /// <summary>The wheel over a scroll region, in notches (positive up).</summary>
        internal bool SelfTestWheel(int clip, float notches, out string why)
        {
            why = null;
            if (_scene == null || clip < 0 || clip >= _scene.ClipCount)
            {
                why = "the screen has no scroll region " + clip + " (it has " + (_scene != null ? _scene.ClipCount : 0) + ")";
                return false;
            }
            Wheeled(clip, notches);
            return true;
        }

        /// <summary>The screen's Back, as the key's press is handled: true when the screen took it.</summary>
        internal bool SelfTestBack()
        {
            return _screen != null && _screen.Back();
        }

        /// <summary>True when any item of the laid scene carries these words (compared without regard to case).</summary>
        internal bool SelfTestHasWords(string words)
        {
            if (_scene == null || string.IsNullOrEmpty(words))
            {
                return false;
            }
            for (int i = 0; i < _scene.ItemCount; i++)
            {
                string text = _scene.Items[i].Text;
                if (text != null && text.IndexOf(words, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>How many targets and scroll regions the screen has now, for a plan's author.</summary>
        internal string SelfTestShape()
        {
            return _scene == null ? "no scene" : _scene.NodeCount + " targets, " + _scene.ClipCount + " scroll regions, " + _scene.ItemCount + " items";
        }

        /// <summary>The words of a target: the first text item that belongs to it, else the text of its face; empty when it has none.</summary>
        internal string SelfTestLabelOf(int node)
        {
            if (_scene == null || node < 0 || node >= _scene.NodeCount)
            {
                return string.Empty;
            }
            int face = _scene.Nodes[node].Face;
            if (face >= 0 && face < _scene.ItemCount && !string.IsNullOrEmpty(_scene.Items[face].Text))
            {
                return _scene.Items[face].Text;
            }
            for (int i = 0; i < _scene.ItemCount; i++)
            {
                if (_scene.Items[i].Node == node && !string.IsNullOrEmpty(_scene.Items[i].Text))
                {
                    return _scene.Items[i].Text;
                }
            }
            return string.Empty;
        }

        /// <summary>All the words of a target: its own label, and the text of every item laid inside its rectangle (a table row's other cells).</summary>
        internal string SelfTestWordsOf(int node)
        {
            if (_scene == null || node < 0 || node >= _scene.NodeCount)
            {
                return string.Empty;
            }
            UiNode n = _scene.Nodes[node];
            var sb = new System.Text.StringBuilder(SelfTestLabelOf(node));
            for (int i = 0; i < _scene.ItemCount; i++)
            {
                UiItem it = _scene.Items[i];
                if (string.IsNullOrEmpty(it.Text) || it.Clip != n.Clip || it.Layer != n.Layer)
                {
                    continue;
                }
                float cx = it.X + it.W * 0.5f, cy = it.Y + it.H * 0.5f;
                if (cx >= n.X && cx <= n.X + n.W && cy >= n.Y && cy <= n.Y + n.H)
                {
                    sb.Append(" | ").Append(it.Text);
                }
            }
            return sb.ToString();
        }

        /// <summary>Presses the nth (from 1) target whose words, or a cell inside it, hold the text (any case); false, with why, when there is none or it is disabled.</summary>
        internal bool SelfTestPressText(string text, int nth, out string label, out string why)
        {
            label = null;
            why = null;
            int seen = 0, found = -1;
            for (int i = 0; _scene != null && i < _scene.NodeCount; i++)
            {
                if (_scene.Nodes[i].Handle)
                {
                    continue;
                }
                string words = SelfTestWordsOf(i);
                if (words.Length > 0 && words.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 && ++seen == Math.Max(1, nth))
                {
                    found = i;
                    label = SelfTestLabelOf(i).Length > 0 ? SelfTestLabelOf(i) : words;
                    break;
                }
            }
            if (found < 0)
            {
                why = "no target whose words hold '" + text + "' (" + seen + " found, wanted number " + Math.Max(1, nth) + "); kit_commands lists the targets";
                return false;
            }
            return PressFound(found, label, out why);
        }

        /// <summary>Presses the row (from 0) of the screen's list: the target that is that row.</summary>
        internal bool SelfTestPressRow(int row, out string label, out string why)
        {
            label = null;
            why = null;
            for (int i = 0; _scene != null && i < _scene.NodeCount; i++)
            {
                UiNode n = _scene.Nodes[i];
                if (!n.Handle && n.List >= 0 && n.Index == row)
                {
                    label = SelfTestWordsOf(i);
                    return PressFound(i, label, out why);
                }
            }
            why = "the screen has no list row " + row + " (kit_commands lists the targets)";
            return false;
        }

        private bool PressFound(int found, string label, out string why)
        {
            why = null;
            if (_scene.Nodes[found].Disabled)
            {
                why = "the target '" + label + "' is there but disabled";
                return false;
            }
            PressNode(found, Time.unscaledTime);
            if (!_lastPressDone)
            {
                why = "the screen answered false to the press of '" + label + "' (a refusal: Player.log names the control and why)";
            }
            return _lastPressDone;
        }

        /// <summary>One line a target of the laid screen: its number, command (by the name the caller gives it), argument, words and whether it is enabled.</summary>
        internal void SelfTestTargets(System.Text.StringBuilder into, Func<int, string> nameOf, int most)
        {
            for (int i = 0; _scene != null && i < _scene.NodeCount && i < most; i++)
            {
                UiNode n = _scene.Nodes[i];
                if (n.Handle)
                {
                    continue;
                }
                string words = SelfTestLabelOf(i);
                into.Append("  [").Append(i).Append("] ").Append(nameOf(n.Command)).Append('/').Append(n.Arg).Append(n.Disabled ? " DISABLED" : string.Empty)
                    .Append(words.Length > 0 ? " '" + (words.Length > 60 ? words.Substring(0, 60) : words).Replace('\n', ' ') + "'" : string.Empty).Append('\n');
            }
        }

        private int Find(int command, int arg)
        {
            if (_scene == null)
            {
                return -1;
            }
            for (int i = _scene.NodeCount - 1; i >= 0; i--)
            {
                UiNode n = _scene.Nodes[i];
                if (!n.Handle && n.Command == command && (arg < 0 || n.Arg == arg))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
