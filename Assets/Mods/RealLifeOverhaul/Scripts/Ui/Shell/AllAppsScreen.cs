using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// All apps as a screen of the kit: the frame's apps (UiShellApps) in the phone's folders and Other mods, the chosen
    /// folder's apps as rows with the line each app gives, a press opening that app's own page through its own button
    /// (UiShell), Back or the tile closing the panel. Laid out again only when the apps or their news change. Cost: a
    /// model and a layout on a change; nothing a frame.
    /// </summary>
    public sealed class AllAppsScreen : IUiScreen
    {
        private readonly UiAllAppsModel _m = new UiAllAppsModel();
        private readonly List<UiShellApp> _rows = new List<UiShellApp>(24);
        private readonly int[] _badges = new int[UiShellApps.Folders];
        private int _tab = -1, _version = int.MinValue;
        private string _open;
        private bool _dirty = true;

        /// <summary>The panel closes: Back, the tile again, or an app opened from it.</summary>
        public Action Close;

        /// <summary>An app pressed: the frame opens it through its own button; false when it could not.</summary>
        public Func<string, bool> Opening;

        public string Id => "all_apps";

        public bool Dirty => _dirty || UiShellApps.Version != _version;

        /// <summary>Opens on a folder (a folder's entry in the app row), or on the open app's folder when -1.</summary>
        public void Show(int folder, string open)
        {
            _open = open;
            _tab = folder;
            if (_tab < 0)
            {
                for (int i = 0; i < UiShellApps.Items.Count; i++)
                {
                    if (UiShellApps.Items[i].Id == open)
                    {
                        _tab = UiShellApps.Items[i].Folder;
                    }
                }
            }
            _tab = Math.Max(0, Math.Min(UiShellApps.Folders - 1, _tab));
            _m.First = 0;
            _dirty = true;
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            Build();
            UiAllApps.Lay(s, _m, frame, dark, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            if (command == UiAllApps.TabCommand && arg >= 0 && arg < UiShellApps.Folders)
            {
                _tab = arg;
                _m.First = 0;
                _dirty = true;
                return true;
            }
            if (command == UiAllApps.RowCommand && arg >= 0 && arg < _rows.Count)
            {
                bool opened = Opening != null && Opening(_rows[arg].Id);
                if (opened)
                {
                    Close?.Invoke();
                }
                return opened;
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            Close?.Invoke();
            return true;
        }

        public void Tab(int step)
        {
            _tab = (_tab + step + UiShellApps.Folders) % UiShellApps.Folders;
            _m.First = 0;
            _dirty = true;
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
            int fit = Math.Max(1, _m.RowCount - _m.First);
            if (index < _m.First)
            {
                _m.First = Math.Max(0, index);
                _dirty = true;
            }
            else if (index >= 0 && index < _m.RowCount && index >= _m.First + fit)
            {
                _m.First = index;
                _dirty = true;
            }
            else if (index >= 0 && index < _m.RowCount)
            {
                _m.First = Math.Min(index, Math.Max(0, _m.RowCount - 1));
                _dirty = true;
            }
        }

        public void Wheel(int clip, float notches)
        {
            int next = Math.Max(0, Math.Min(Math.Max(0, _m.RowCount - 1), _m.First - (int)Math.Round(notches)));
            if (next != _m.First)
            {
                _m.First = next;
                _dirty = true;
            }
        }

        private void Build()
        {
            _version = UiShellApps.Version;
            Array.Clear(_badges, 0, _badges.Length);
            _rows.Clear();
            for (int i = 0; i < UiShellApps.Items.Count; i++)
            {
                UiShellApp a = UiShellApps.Items[i];
                if (a.Folder >= 0 && a.Folder < _badges.Length)
                {
                    _badges[a.Folder] += a.Badge;
                }
                if (a.Folder == _tab)
                {
                    _rows.Add(a);
                }
            }
            if (_m.Tabs.Length != UiShellApps.Folders)
            {
                _m.Tabs = new UiAllAppsTab[UiShellApps.Folders];
            }
            for (int f = 0; f < UiShellApps.Folders; f++)
            {
                UiIcon icon = UiShellApps.FolderIcon(f);
                _m.Tabs[f] = new UiAllAppsTab { Icon = icon, Tint = UiIconTints.Of(icon), Label = UiText.T(UiShellApps.FolderKey(f)), Badge = _badges[f] };
            }
            _m.Tab = _tab;
            _m.Title = UiText.T("reallife_ui_shell_all_apps");
            if (_m.Rows.Length < _rows.Count)
            {
                _m.Rows = new UiAllAppsRow[_rows.Count + 8];
            }
            for (int i = 0; i < _rows.Count; i++)
            {
                UiShellApp a = _rows[i];
                _m.Rows[i] = new UiAllAppsRow
                {
                    Icon = a.Icon, Tint = a.Tint, Title = a.Title, Badge = a.Badge, Open = a.Id == _open, Status = UiShellApps.Status != null ? UiShellApps.Status(a.Id, a.Badge) : null
                };
            }
            _m.RowCount = _rows.Count;
            _m.First = Math.Min(_m.First, Math.Max(0, _m.RowCount - 1));
            _m.Empty = UiText.T(_tab == UiShellApps.Other ? "reallife_ui_shell_none_other" : "reallife_ui_shell_none");
            if (_m.Prompts.Length != 4)
            {
                _m.Prompts = new UiPrompt[4];
            }
            _m.Prompts[0] = new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") };
            _m.Prompts[1] = new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_select") };
            _m.Prompts[2] = new UiPrompt { Action = UiAction.TabPrev, Label = UiText.T("reallife_ui_shell_prompt_folder") };
            _m.Prompts[3] = new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") };
            _m.PromptCount = 4;
        }
    }
}
