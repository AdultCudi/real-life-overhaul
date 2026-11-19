using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.Ui;
using UI.Notification;
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

        private const int MostAll = 40000;    // log lines of every kind kept for assert_log_has; the rest of a very long run are not
        private const int MostLines = 60;     // error lines kept in a window; the rest are counted
        private const int Longest = 300;      // characters of a line kept

        private readonly List<string> _step = new List<string>(8);
        private readonly List<string> _window = new List<string>(8);
        private readonly List<string> _windowRlo = new List<string>(8);
        private int _stepMore, _windowMore;
        private readonly List<string> _all = new List<string>(1024);   // every line of the log since the plan began, for assert_log_has
        private readonly Dictionary<string, int> _starts = new Dictionary<string, int>(StringComparer.Ordinal);   // where in _all each step began
        private readonly Dictionary<string, float> _minutesAt = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _moneyAt = new Dictionary<string, float>(StringComparer.Ordinal);
        private float _windowMinutes, _windowMoney;
        private readonly List<string> _notices = new List<string>(64);   // every notice shown since the plan began: "[Type] key: its words"
        private readonly Dictionary<string, int> _noticeStarts = new Dictionary<string, int>(StringComparer.Ordinal);
        private int _noticeWindow;
        private Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool> _onNotice, _noticeSeen;
        private int _windowStart;   // where in _all the assert window began
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
                _onNotice = Guard.Wrap<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>(OnNotice);
                EventHold.Keep(ref Notifications.onShow, _onNotice, ref _noticeSeen);
            }
        }

        public void Unhook()
        {
            if (_hooked)
            {
                _hooked = false;
                Application.logMessageReceived -= _onLog;
                Notifications.onShow = (Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>)Delegate.Remove(Notifications.onShow, _onNotice);
            }
        }

        private void OnLog(string condition, string stack, LogType type)
        {
            try
            {
                if (_all.Count < MostAll)
                {
                    _all.Add(condition ?? string.Empty);
                }
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
        public void Begin(string label, bool assert)
        {
            _starts[label] = _all.Count;
            if (_onNotice != null)
            {
                EventHold.Keep(ref Notifications.onShow, _onNotice, ref _noticeSeen);   // the game clears its event hubs on every load
            }
            _noticeStarts[label] = _notices.Count;
            float minutes = SelfTestGame.Minutes(), money = SelfTestGame.MoneyNow();
            _minutesAt[label] = minutes;
            _moneyAt[label] = money;
            if (!assert)
            {
                _noticeWindow = _notices.Count;
                _windowMinutes = minutes;
                _windowMoney = money;
            }
            _step.Clear();
            _stepMore = 0;
            if (assert)
            {
                return;
            }
            _windowStart = _all.Count;
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

        /// <summary>True when a log line since a step began (the named step's label, else the last step that is not an assert) holds the text.</summary>
        public bool LogHas(string text, string fromLabel, out string found)
        {
            found = null;
            int from = fromLabel != null && _starts.TryGetValue(fromLabel, out int at) ? at : _windowStart;
            for (int i = from; i < _all.Count; i++)
            {
                if (_all[i].IndexOf(text, StringComparison.Ordinal) >= 0)
                {
                    found = _all[i].Length > 240 ? _all[i].Substring(0, 240) + "..." : _all[i];
                    return true;
                }
            }
            return false;
        }

        private void OnNotice(NotificationType type, string key, Dictionary<string, string> data, float seconds, string duplicate, Action click, bool sound, bool tracked)
        {
            string words = UiText.T(key);
            if (data != null)
            {
                foreach (KeyValuePair<string, string> pair in data)
                {
                    words = words.Replace("{" + pair.Key + "}", pair.Value);
                }
            }
            _notices.Add("[" + type + "] " + key + ": " + (words.Length > 200 ? words.Substring(0, 200) + "..." : words));
        }

        /// <summary>The notices shown since the named step began (else the assert window), each "[Type] key: words".</summary>
        public List<string> NoticeLines(string fromLabel)
        {
            int from = fromLabel != null && _noticeStarts.TryGetValue(fromLabel, out int at) ? at : _noticeWindow;
            return _notices.GetRange(Math.Min(from, _notices.Count), _notices.Count - Math.Min(from, _notices.Count));
        }

        /// <summary>The log lines since the named step began, the mod's own and the game's, at most that many, each shortened.</summary>
        public List<string> LogLinesSince(string fromLabel, int most)
        {
            int from = fromLabel != null && _starts.TryGetValue(fromLabel, out int at) ? at : _windowStart;
            var lines = new List<string>(most);
            for (int i = from; i < _all.Count && lines.Count < most; i++)
            {
                lines.Add(_all[i].Length > 200 ? _all[i].Substring(0, 200) + "..." : _all[i]);
            }
            return lines;
        }

        /// <summary>The game's minutes when the named step began, else when the assert window began.</summary>
        public float MinutesAt(string label) => label != null && _minutesAt.TryGetValue(label, out float m) ? m : _windowMinutes;

        /// <summary>The player's money when the named step began, else when the assert window began.</summary>
        public float MoneyAt(string label) => label != null && _moneyAt.TryGetValue(label, out float m) ? m : _windowMoney;

        /// <summary>True when the step of that label has begun.</summary>
        public bool Knows(string label) => _starts.ContainsKey(label);

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
