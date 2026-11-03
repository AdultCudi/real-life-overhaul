using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The bug report on the kit: what happened, what was expected, where, and the log. The main action saves a file.
    /// Nothing is sent. The words are taken when the player saves, not per frame.
    /// </summary>
    public sealed class BugReportScreen : IUiScreen, IUiTypingSubmit
    {
        public const string ScreenId = "bug_report";

        private readonly UiReportModel _model = new UiReportModel();
        private readonly CultureInfo _inv = CultureInfo.InvariantCulture;
        private int _field;
        private bool _shot;
        private bool _dirty = true;
        private string _happened = string.Empty;
        private string _expected = string.Empty;
        private string _where = string.Empty;
        private string _detected = string.Empty;
        private long _bytes;

        public Action Closing;

        public static IUiScreen Make()
        {
            if (_one == null)
            {
                _one = new BugReportScreen();
            }
            return _one;
        }

        private static BugReportScreen _one;

        public string Id => ScreenId;

        public bool Dirty => _dirty;

        public int TypingCommand => UiReport.FieldCommand;

        public int TypingLimit => BugReportRules.BoxWords;

        public string TypingText => _field == 1 ? _expected : _happened;

        public void Prepare(string where, long bytes)
        {
            _detected = string.IsNullOrEmpty(where) ? T("reallife_bug_where_city") : where;
            _where = _detected;
            _bytes = bytes;
            _shot = false;
            _happened = string.Empty;
            _expected = string.Empty;
            _field = 0;
            _dirty = true;
        }

        public void Typed(string text)
        {
            string cut = text ?? string.Empty;
            if (cut.Length > BugReportRules.BoxWords)
            {
                cut = cut.Substring(0, BugReportRules.BoxWords);
            }
            if (_field == 1)
            {
                _expected = cut;
            }
            else
            {
                _happened = cut;
            }
            _dirty = true;
        }

        public void Submitted(string text)
        {
            Typed(text);
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            _model.Title = T("reallife_bug_title");
            _model.Body = T("reallife_bug_card_body");
            _model.HappenedLabel = T("reallife_bug_happened");
            _model.Happened = _happened;
            _model.HappenedHint = T("reallife_bug_happened_hint");
            _model.ExpectedLabel = T("reallife_bug_expected");
            _model.Expected = _expected;
            _model.ExpectedHint = T("reallife_bug_expected_hint");
            _model.WhereLabel = T("reallife_bug_where");
            _model.Where = _where;
            _model.WhereOptions = new[] { _detected, T("reallife_bug_where_city") };
            _model.WhereChosen = _where == T("reallife_bug_where_city") && _detected != _where ? 1 : 0;
            _model.Checks = new[]
            {
                new UiReportCheck { Text = T("reallife_bug_log_line").Replace("{size}", Size(_bytes)), Met = true },
                new UiReportCheck { Text = T("reallife_bug_version_line").Replace("{version}", Changelog.Shown), Met = true },
                new UiReportCheck { Text = T("reallife_bug_shot_line"), Met = _shot, Link = _shot ? null : T("reallife_bug_shot_add") }
            };
            _model.Buttons = new[]
            {
                new UiActionModel { Label = T("reallife_ui_prompt_back"), Kind = UiButtonKind.Secondary, Key = UiAction.Back },
                new UiActionModel { Label = T("reallife_bug_save"), Kind = UiButtonKind.Primary, Key = UiAction.Alt }
            };
            _model.Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = T("reallife_ui_prompt_move") },
                new UiPrompt { Action = UiAction.Confirm, Label = T("reallife_ui_prompt_select") },
                new UiPrompt { Action = UiAction.Alt, Label = T("reallife_bug_save") },
                new UiPrompt { Action = UiAction.Back, Label = T("reallife_ui_prompt_back") }
            };
            _model.PromptCount = 4;
            UiReport.Lay(s, _model, frame, dark, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            if (command == UiReport.FieldCommand)
            {
                _field = arg;
                _dirty = true;
                return true;
            }
            if (command == UiReport.WhereCommand)
            {
                _model.WhereOpen = !_model.WhereOpen;
                _dirty = true;
                return true;
            }
            if (command == UiReport.OptionCommand)
            {
                _where = arg == 1 ? T("reallife_bug_where_city") : _detected;
                _model.WhereOpen = false;
                _dirty = true;
                return true;
            }
            if (command == UiReport.LinkCommand)
            {
                _shot = true;
                _dirty = true;
                return true;
            }
            if (command == UiReport.ButtonCommand)
            {
                if (arg == _model.Buttons.Length - 1)
                {
                    Save();
                }
                Close();
                return true;
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
            if (command == UiReport.FieldCommand)
            {
                _field = arg;
            }
        }

        public bool Back()
        {
            if (_model.WhereOpen)
            {
                _model.WhereOpen = false;
                _dirty = true;
                return true;
            }
            Close();
            return true;
        }

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action)
        {
            if (action == UiAction.Alt)
            {
                Save();
                Close();
                return true;
            }
            return false;
        }

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }

        private void Save()
        {
            string words = BugReportRules.Join(_happened, _expected, T("reallife_bug_where_prefix").Replace("{where}", _where));
            BugReport.MakeFromCard(words, _shot);
        }

        private void Close()
        {
            Action closing = Closing;
            if (closing != null)
            {
                closing();
            }
        }

        private string Size(long bytes)
        {
            if (bytes >= 1024L * 1024L)
            {
                return (bytes / (1024d * 1024d)).ToString("0.0", _inv) + " MB";
            }
            if (bytes >= 1024L)
            {
                return (bytes / 1024d).ToString("0", _inv) + " KB";
            }
            return bytes.ToString(_inv) + " B";
        }

        private static string T(string key) => UiText.T(key);
    }
}
