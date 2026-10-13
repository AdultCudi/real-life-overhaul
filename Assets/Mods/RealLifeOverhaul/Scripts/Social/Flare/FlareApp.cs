using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// What the Flare app shows, in the game's frame (GameUi.AppShell, copied from MyEmployees) as Brands, the Company
    /// app and the Web shop are, with the kit's table (GameUi.DataTable, the game's own BaTable). Five tabs over the
    /// table on the right, and on the left the white box with the one thing picked:
    ///
    ///   Feed       the player's posts and the friends' own, newest first, twenty at most; pick one for its words,
    ///              who liked it and the replies
    ///   Post       what the player can post about now (FlareDraft); pick one for its words, the tone, whom to tag,
    ///              and the Post button
    ///   Me         the account: followers, tier, reputation, the last seven days; the player's own posts
    ///   Business   the player's businesses: stars, reviews, the talk, posts, hiring influencers and the deals made
    ///   City       the city's influencers, brands and candidates, each with a profile and a Follow button (FlareApp.City.cs)
    ///
    /// A list goes in the table, which scrolls; the white box shows only the one thing picked. Everything on it is the
    /// game's. PER FRAME: NOTHING, open or closed. The rows and the page are made when the app opens, when a tab or a
    /// row is picked and when the director says something has changed: a few dozen strings, on a click.
    /// </summary>
    public sealed partial class FlareApp
    {
        public static IModLogger Logger;

        private readonly FlareDirector _director;
        private readonly List<TableRow> _rows = new List<TableRow>(32);
        private readonly List<TableRow> _pool = new List<TableRow>(32);

        private AppShell _shell;
        private DataTable _table;
        private int _tab, _wanted = -1;
        private string _pick, _wantPick;   // what is picked: a row's id ("post:12", "draft:7", "shop:...")
        private bool _ready;               // the director has read the people for this opening
        private bool _writing;             // a page is being written: a change it causes does not write it again

        private readonly FlarePictures _pictures;

        public FlareApp(FlareDirector director)
        {
            _director = director;
            _pictures = new FlarePictures(director, Changed);
        }

        /// <summary>The city is unloading or Flare stops: every picture's texture goes.</summary>
        public void Dispose()
        {
            _pictures.Clear();
        }

        // ---------------------------------------------------------------- building it from the game's parts

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each has said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            SessionWatch.Mark("flare: the app being built from the game's MyEmployees");   // once per city: a few hundred objects copied in one frame
            _shell = AppShell.Make(menu, dormant, FlareDirector.AppId);
            _table = DataTable.On(_shell, "Flare");
            if (_table == null || !_shell.SetTabCount(FlareText.TabKeys.Length) || !_shell.SetColumns(FlareTables.Off))
            {
                return null;
            }
            _table.SelectionLost = ClosePick;
            _table.onRowSelected.AddListener(row => UiClone.Safe(() => RowClicked(row)));
            for (int i = 0; i < FlareText.TabKeys.Length; i++)
            {
                int tab = i;
                UiClone.Key(_shell.TabLabels[i], FlareText.TabKeys[i]);
                UiClone.Rewire(_shell.Tabs[i], () => ShowTab(tab));
            }
            return _shell.Root;
        }

        // ---------------------------------------------------------------- opening and the tabs

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        /// <summary>The tab to show the next time the app opens, and what is picked on it.</summary>
        public void Want(int tab, string pick)
        {
            _wanted = Mathf.Clamp(tab, 0, FlareText.TabKeys.Length - 1);
            _wantPick = pick;
            if (Showing)
            {
                TakeWanted();
                Refresh();
            }
        }

        private void TakeWanted()
        {
            if (_wanted >= 0)
            {
                _tab = _wanted;
                _pick = _wantPick;
                _wanted = -1;
                _wantPick = null;
                ResetPages();
            }
        }

        public void Opened()
        {
            if (_shell == null || _table == null)
            {
                return;
            }
            TakeWanted();
            _ready = _director.AppOpened();
            Refresh();
        }

        public void Closed()
        {
            _ready = false;
            _pictures.Clear();
        }

        /// <summary>The director says something has changed: the page is written again if it is showing.</summary>
        public void Changed()
        {
            if (Showing && !_writing)
            {
                _ready = _director.Running;
                TakeWanted();
                Refresh();
            }
        }

        private void ShowTab(int tab)
        {
            if (tab != _tab || _pick != null)
            {
                _tab = tab;
                _pick = null;
                ResetPages();
                Refresh();
            }
        }

        private void RowClicked(TableRow row)
        {
            if (row == null || !row.Pick)
            {
                _table.ClearSelection();
                return;
            }
            if (row.Id != _pick)
            {
                ResetPages();
            }
            _pick = row.Id;
            Refresh();
        }

        private void Back()
        {
            ClosePick();
        }

        private void ClosePick()
        {
            _pick = null;
            ResetPages();
            _table.ClearSelection();
            Refresh();
        }

        // Everything the open tab shows, made again: asked for when the app opens, a tab or a row is picked, or something has changed.
        private void Refresh()
        {
            if (_shell == null || _table == null || _writing)
            {
                return;
            }
            _writing = true;
            try
            {
                float started = Time.realtimeSinceStartup;
                for (int i = 0; i < _rows.Count; i++)
                {
                    _pool.Add(_rows[i]);
                }
                _rows.Clear();
                _shell.ShowTab(_tab);
                InfoBox box = _shell.Box;
                box.Clear();
                string selected = null;
                TableSpec table = FlareTables.Off;
                FlareFile file = _ready ? _director.File : null;
                if (file == null || SaveGameManager.Current == null)
                {
                    UiClone.Words(_shell.Header, FlareText.T("reallife_flare_app"));
                    box.Title(FlareText.T("reallife_flare_off_title"));
                    box.Line(FlareText.T("reallife_flare_off_body"));
                }
                else
                {
                    int day = SaveGameManager.Current.Day;
                    UiClone.Words(_shell.Header, FlareText.Fill("reallife_flare_headline", "followers", FlareText.N(file.Followers), "tier", FlareText.Tier(file.Tier)));
                    switch (_tab)
                    {
                        case FlareDirector.TabPost:
                            table = WriteDrafts(box, file, day, ref selected);
                            break;
                        case FlareDirector.TabMe:
                            table = WriteMe(box, file, day, ref selected);
                            break;
                        case FlareDirector.TabBusiness:
                            table = WriteBusiness(box, file, day, ref selected);
                            break;
                        case FlareDirector.TabCity:
                            table = WriteCity(box, file, day, ref selected);
                            break;
                        default:
                            table = WriteFeed(box, file, day, ref selected);
                            break;
                    }
                }
                box.Done();
                _table.Show(table, _rows, selected);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Flare: tab " + _tab + " written, " + _rows.Count + " row(s)" + (_pick != null ? ", picked " + _pick : string.Empty) + "; "
                                 + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms.");
                }
            }
            finally
            {
                _writing = false;
            }
        }

        // ---------------------------------------------------------------- rows

        private TableRow Row(string id)
        {
            TableRow row;
            if (_pool.Count > 0)
            {
                row = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                row = new TableRow();
            }
            row.Reset(id);
            row.Pick = true;
            _rows.Add(row);
            return row;
        }

        // True if a row is the one picked now; it is then the table's selected row.
        private bool Is(TableRow row, ref string selected)
        {
            if (row.Id != _pick)
            {
                return false;
            }
            selected = row.Id;
            return true;
        }

        // What follows a prefix in what is picked ("post:" and the post's number); null if something else is picked.
        private string Picked(string prefix)
        {
            return _pick != null && _pick.StartsWith(prefix, StringComparison.Ordinal) ? _pick.Substring(prefix.Length) : null;
        }

        private int PickedNumber(string prefix)
        {
            int number;
            string text = Picked(prefix);
            return text != null && int.TryParse(text, out number) ? number : -1;
        }

        private void BackButton(InfoBox box)
        {
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_flare_back", Back);
        }

        // A choice among a few: the game's previous/next selector, or the choice as a line where the game's selector could not be had.
        private void Choose(InfoBox box, int count, int index, Func<int, string> words, Action<int> changed)
        {
            if (count < 1)
            {
                return;
            }
            int at = FlareRules.Clamp(index, 0, count - 1);
            if (!box.HasSelector || !box.Selector(count, at, words, i =>
                {
                    changed(i);
                    Refresh();
                }))
            {
                box.Line(words(at));
            }
        }
    }
}
