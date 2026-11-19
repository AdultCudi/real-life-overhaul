using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.Ui;
using UnityEngine;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>
    /// What a plan reads of the mod's own record while it runs: the game's log lines that are errors or exceptions (the log
    /// event is hooked only while a plan runs), and the guards' faults, the features switched off and the kit pieces that
    /// fell back to the game's screen. Two windows are kept: the step's own, for the results, and the one an assert reads,
    /// which begins at the last step that is not an assert. COST: one string kept per error line (at most 60 a window).
    /// </summary>
    internal sealed class SelfTestWatch
    {
        private static readonly FeatureGuard LogGuard = Guard.For("diagnostics", "SelfTestWatch.OnLog", typeof(SelfTestWatch));   // the log event's handler (Core/Guard)

        private const int MostLines = 60;     // error lines kept in a window; the rest are counted
        private const int Longest = 300;      // characters of a line kept

        private readonly List<string> _step = new List<string>(8);
        private readonly List<string> _window = new List<string>(8);
        private readonly List<string> _windowRlo = new List<string>(8);
        private int _stepMore, _windowMore;
        private bool _hooked;
        private Application.LogCallback _onLog;   // made once: the same delegate is put on the game's log and taken off it

        private int[] _guardFaults = new int[0];
        private int _featuresOff, _piecesOff, _breaker;
        private string _piecesNames = string.Empty;

        public void Hook()
        {
            if (!_hooked)
            {
                _hooked = true;
                _onLog = _onLog ?? OnLog;
                Application.logMessageReceived += _onLog;
            }
        }

        public void Unhook()
        {
            if (_hooked)
            {
                _hooked = false;
                Application.logMessageReceived -= _onLog;
            }
        }

        private void OnLog(string condition, string stack, LogType type)
        {
            try
            {
                if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                {
                    return;
                }
                string owner = SelfTestRules.Owner(condition, stack);
                string line = (type == LogType.Exception ? "exception" : "error") + " (" + owner + "): " + Shorten(condition);
                Keep(_step, line, ref _stepMore);
                Keep(_window, line, ref _windowMore);
                if (owner == SelfTestRules.OwnerRlo)
                {
                    _windowRlo.Add(line);
                }
            }
            catch (System.Exception e)
            {
                Guard.Fail(LogGuard, e);
            }
        }

        private static void Keep(List<string> list, string line, ref int more)
        {
            if (list.Count < MostLines)
            {
                list.Add(line);
            }
            else
            {
                more++;
            }
        }

        private static string Shorten(string text)
        {
            if (text == null)
            {
                return string.Empty;
            }
            int cut = text.IndexOf('\n');
            if (cut > 0)
            {
                text = text.Substring(0, cut);
            }
            return text.Length > Longest ? text.Substring(0, Longest) + "..." : text;
        }

        /// <summary>A step begins: its own window is cleared; the assert window too when the step is not an assert, and the fault marks are taken.</summary>
        public void Begin(bool assert)
        {
            _step.Clear();
            _stepMore = 0;
            if (assert)
            {
                return;
            }
            _window.Clear();
            _windowRlo.Clear();
            _windowMore = 0;
            MarkFaults();
        }

        /// <summary>The step's own new error lines, for the results.</summary>
        public void StepLines(List<string> into)
        {
            into.AddRange(_step);
            if (_stepMore > 0)
            {
                into.Add("and " + _stepMore + " more error lines");
            }
        }

        /// <summary>The error lines of the assert window: RLO's only, or all.</summary>
        public int WindowLines(bool rloOnly, List<string> into)
        {
            List<string> from = rloOnly ? _windowRlo : _window;
            into.AddRange(from);
            return from.Count + (rloOnly ? 0 : _windowMore);
        }

        public int OtherWindowLines() => _window.Count + _windowMore - _windowRlo.Count;

        private void MarkFaults()
        {
            IReadOnlyList<FeatureGuard> guards = FeatureGuards.Guards;
            var counts = new int[guards.Count];
            for (int i = 0; i < counts.Length; i++)
            {
                counts[i] = guards[i].Faults;
            }
            _guardFaults = counts;
            _featuresOff = CountOff();
            _piecesOff = UiPieces.OffCount;
            _piecesNames = _piecesOff > 0 ? UiPieces.OffNames() : string.Empty;
            _breaker = UiBreaker.Count;
        }

        private static int CountOff()
        {
            int n = 0;
            foreach (string unused in FeatureGuards.OffList)
            {
                n++;
            }
            return n;
        }

        /// <summary>Words for every fault, feature switched off and kit piece fallen back since the last mark; empty when there is none.</summary>
        public string FaultWords()
        {
            var sb = new StringBuilder(0);
            IReadOnlyList<FeatureGuard> guards = FeatureGuards.Guards;
            for (int i = 0; i < guards.Count; i++)
            {
                int before = i < _guardFaults.Length ? _guardFaults[i] : 0;
                if (guards[i].Faults > before)
                {
                    sb.Append(sb.Length > 0 ? "; " : string.Empty).Append(guards[i].Where).Append(" had ").Append(guards[i].Faults - before).Append(" fault(s)");
                }
            }
            if (CountOff() > _featuresOff)
            {
                sb.Append(sb.Length > 0 ? "; " : string.Empty).Append("a feature was switched off (").Append(string.Join(", ", FeatureGuards.OffList)).Append(')');
            }
            if (UiPieces.OffCount > _piecesOff)
            {
                sb.Append(sb.Length > 0 ? "; " : string.Empty).Append("a kit piece fell back to the game's own screen (").Append(UiPieces.OffNames()).Append(')');
            }
            if (UiBreaker.Count > _breaker)
            {
                sb.Append(sb.Length > 0 ? "; " : string.Empty).Append("a kit screen's breaker opened");
            }
            string hosts = UiGameHost.FellBack();
            if (hosts.Length > 0)
            {
                sb.Append(sb.Length > 0 ? "; " : string.Empty).Append("kit screens that are the game's own after a fault: ").Append(hosts);
            }
            return sb.ToString();
        }
    }
}
