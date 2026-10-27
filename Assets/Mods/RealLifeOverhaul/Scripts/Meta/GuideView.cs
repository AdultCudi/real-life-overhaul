using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BaTable;
using EnhancedUI.EnhancedScroller;
using Localizor;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Meta
{
    /// <summary>One row of the Guide's table: a page to open, or a line of the page being read.</summary>
    public sealed class GuideRow
    {
        public string Id;          // the page's id for a page row, null for a line of text
        public int Look;           // GuideView.Topic, Heading, Bullet or Plain
        public string Text;
        public float Height;
    }

    /// <summary>The Guide's table, the game's own BaTable as the Planner's is: its scroller makes only the rows in view. A row is a page (100 high) or a line of one.</summary>
    public sealed class GuideTable : BaTable<GuideCellView, GuideRow>
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "GuideTable", typeof(GuideTable));   // the game's calls of it

        private bool _started;

        public override void Start()
        {
            base.Start();
            _started = true;
            scroller.ReloadData();
        }

        public override float GetCellViewSize(EnhancedScroller scroller, int dataIndex)
        {
            if (FrameGuard.Off)
            {
                return 100f;
            }
            try
            {
                return dataIndex >= 0 && dataIndex < data.Count ? data[dataIndex].Height : 100f;
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, 100f);
            }
        }

        protected override string GetDataId(GuideRow model)
        {
            if (FrameGuard.Off)
            {
                return string.Empty;
            }
            try
            {
                return model.Id ?? model.Text;
            }
            catch (Exception fault)
            {
                return Guard.Failed(FrameGuard, fault, string.Empty);
            }
        }

        /// <summary>Shows these rows. The first time the app opens this runs before the scroller has woken up: the rows are kept and Start shows them.</summary>
        public void Show(List<GuideRow> rows)
        {
            data = rows;
            SelectRow(null, null);
            if (_started && scroller != null)
            {
                scroller.ReloadData();
            }
        }

        public void ClearSelection()
        {
            SelectRow(null, null);
        }
    }

    /// <summary>A row: the game's MyEmployees row with one cell left, wrapped, written from a <see cref="GuideRow"/>.</summary>
    public sealed class GuideCellView : BaTableCellView<GuideRow>
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "GuideCellView", typeof(GuideCellView));   // the game's calls of it

        private TMP_Text _text;
        private GuideRow _row;
        private bool _bound;

        protected override void Awake()
        {
            Bind();
            base.Awake();
        }

        private void Bind()
        {
            if (_bound)
            {
                return;
            }
            _bound = true;
            Transform background = transform.Find("Background");
            backgroundImage = background != null ? background.GetComponent<Image>() : null;
            selectedBackgroundColor = new Color(0.741f, 0.78f, 0.812f, 1f);
            Transform cell = transform.Find("Text");
            _text = cell != null ? cell.GetComponent<TMP_Text>() : null;
        }

        public override void SetData(GuideRow row)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                }
                Bind();
                _row = row;
                if (_text != null)
                {
                    _text.text = row.Text ?? string.Empty;
                    _text.fontStyle = row.Look == GuideView.Heading || row.Look == GuideView.Topic ? FontStyles.Bold : FontStyles.Normal;
                }
                Look(false);
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        public override void ResetVisuals()
        {
            try
            {
                Look(false);
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        public override void VisualizeSelected(bool selected)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                if (_row == null || _row.Look != GuideView.Topic || backgroundImage == null)
                {
                    Look(false);   // a line of text is not something to select
                    return;
                }
                base.VisualizeSelected(selected);
                Look(selected);
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        private void Look(bool selected)
        {
            if (_row == null)
            {
                return;
            }
            if (backgroundImage != null && _row.Look != GuideView.Topic)
            {
                Color clear = defaultBackgroundColor;
                clear.a = 0f;
                backgroundImage.color = clear;
            }
            else if (backgroundImage != null && !selected)
            {
                backgroundImage.color = defaultBackgroundColor;
            }
            var references = InstanceBehavior<GlobalReferences>.Instance;
            if (references != null && _text != null && _row.Look != GuideView.Topic)
            {
                _text.color = _row.Look == GuideView.Heading ? references.colors.yellow : references.colors.white;
            }
        }
    }

    /// <summary>The Guide app's page in the game's frame (<see cref="AppShell"/>): a tab listing the pages, one reading the page picked a line a row. Nothing while closed.</summary>
    public sealed class GuideView
    {
        public const int Topic = 0, Heading = 1, Bullet = 2, Plain = 3;
        private const float TextWidth = 1500f;
        private const float GroupHeight = 80f;      // the heading row that names a group of pages in the list
        private const float CharsPerLine = 86f;     // a conservative guess at a line of the cell: a row too tall is only blank space, one too short cuts the words

        private readonly List<GuideRow> _rows = new List<GuideRow>(64);
        private readonly StringBuilder _line = new StringBuilder(256);
        private List<GuideTopic> _topics;
        private AppShell _shell;
        private GuideTable _table;
        private GuideTopic _topic;
        private string _wanted;           // a page asked for before the app was opened
        private int _tab;

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each has said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("guide: the app being built from the game's MyEmployees");
            _shell = AppShell.Make(menu, dormant, GuideApp.AppId);
            if (_shell == null || !PrepareRow(_shell.RowPrefab))
            {
                return null;
            }
            if (!_shell.SetColumns(new[] { "ColumnText" }, new[] { "reallife_guide_col_pages" }, new[] { TextWidth }, 0))
            {
                return null;
            }
            _table = _shell.List.gameObject.AddComponent<GuideTable>();
            _table.scroller = _shell.Scroller;
            _table.prefab = _shell.RowPrefab.AddComponent<GuideCellView>();
            _table.allowRowSelection = true;
            _table.onRowSelected.AddListener(row => UiClone.Safe(() => RowClicked(row)));
            UiClone.Key(_shell.TabLabels[0], "reallife_guide_tab_pages");
            UiClone.Key(_shell.TabLabels[1], "reallife_guide_tab_reading");
            UiClone.Rewire(_shell.Tabs[0], () => ShowTab(0));
            UiClone.Rewire(_shell.Tabs[1], () => ShowTab(1));
            return _shell.Root;
        }

        // The game's employee row keeps its name cell, renamed and wrapped: the one cell a line of text needs.
        private static bool PrepareRow(GameObject row)
        {
            Transform t = row.transform;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            Transform cell = t.Find("EmployeeName");
            TMP_Text text = cell != null ? cell.GetComponent<TMP_Text>() : null;
            if (layout == null || cell == null || text == null)
            {
                UiClone.Fallback("the game's employee row is not built as expected, so the Guide is not added");
                return false;
            }
            UiClone.KeepOnly(t, "Background", "RaycastTarget", "EmployeeName");
            cell.name = "Text";
            AppShell.RowLayout(layout);
            AppShell.Width(cell, TextWidth, true);
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = TextAlignmentOptions.Left;
            return true;
        }

        /// <summary>Asks for a page (null for the list); shown when the app opens.</summary>
        public void Want(string topicId)
        {
            _wanted = topicId;
            if (_shell != null && _shell.Root != null && _shell.Root.activeInHierarchy)
            {
                Opened();
            }
        }

        public void Opened()
        {
            float started = Time.realtimeSinceStartup;
            _topics = Guide.Topics();
            GuideTopic pick = null;
            if (_wanted != null)
            {
                for (int i = 0; i < _topics.Count && pick == null; i++)
                {
                    pick = _topics[i].Id == _wanted ? _topics[i] : null;
                }
            }
            _wanted = null;
            if (pick != null)
            {
                Read(pick);
            }
            else
            {
                _topic = _topic != null ? Find(_topic.Id) : null;
                if (_topic != null && _tab == 1)
                {
                    Read(_topic);
                }
                else
                {
                    List();
                }
            }
            if (Dev.Verbose)
            {
                MetaLog.Info("Meta check (guide app): opened on " + (_tab == 1 && _topic != null ? "the page '" + _topic.Id + "', " : "the list of ") + _topics.Count + " pages, "
                             + _rows.Count + " rows; "
                             + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms: PASS.");
            }
        }

        public void Closed()
        {
            if (_table != null)
            {
                _table.ClearSelection();
            }
        }

        private GuideTopic Find(string id)
        {
            for (int i = 0; i < _topics.Count; i++)
            {
                if (_topics[i].Id == id)
                {
                    return _topics[i];
                }
            }
            return null;
        }

        private void ShowTab(int tab)
        {
            if (tab == _tab)
            {
                return;
            }
            if (tab == 1 && _topic != null)
            {
                Read(_topic);
            }
            else
            {
                List();
            }
        }

        private void RowClicked(GuideRow row)
        {
            if (row == null || row.Look != Topic || row.Id == null)
            {
                _table.ClearSelection();
                return;
            }
            GuideTopic topic = Find(row.Id);
            if (topic != null)
            {
                Read(topic);
            }
        }

        private void List()
        {
            _tab = 0;
            _rows.Clear();
            string group = null;
            for (int i = 0; i < _topics.Count; i++)
            {
                if (_topics[i].Group != null && _topics[i].Group != group)
                {
                    group = _topics[i].Group;
                    _rows.Add(new GuideRow { Look = Heading, Text = group.GetLocalization(), Height = GroupHeight });
                }
                _rows.Add(new GuideRow { Id = _topics[i].Id, Look = Topic, Text = _topics[i].Title, Height = 100f });
            }
            _shell.ShowTab(0);
            _table.Show(_rows);
            Header();
            Box();
        }

        private void Read(GuideTopic topic)
        {
            _topic = topic;
            _tab = 1;
            _rows.Clear();
            string filled;
            try
            {
                filled = Guide.Fill(topic.Body()) ?? string.Empty;
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                filled = topic.Title;
            }
            foreach (string raw in filled.Replace("\r", string.Empty).Split('\n'))
            {
                if (raw.Length == 0)
                {
                    continue;
                }
                if (raw.StartsWith("# ", StringComparison.Ordinal))
                {
                    string words = raw.Substring(2);
                    _rows.Add(new GuideRow { Look = Heading, Text = words, Height = HeightOf(words, 1.25f) + 24f });
                }
                else if (raw.StartsWith("- ", StringComparison.Ordinal))
                {
                    _line.Length = 0;
                    _line.Append("-  ").Append(raw, 2, raw.Length - 2);
                    string words = _line.ToString();
                    _rows.Add(new GuideRow { Look = Bullet, Text = words, Height = HeightOf(words, 1f) });
                }
                else
                {
                    _rows.Add(new GuideRow { Look = Plain, Text = raw, Height = HeightOf(raw, 1f) });
                }
            }
            _shell.ShowTab(1);
            _table.Show(_rows);
            Header();
            Box();
        }

        // A row's height from the length of its words without their tags: 36 for the margins and 40 a line.
        private static float HeightOf(string words, float size)
        {
            int length = 0;
            bool tag = false;
            for (int i = 0; i < words.Length; i++)
            {
                char c = words[i];
                if (c == '<')
                {
                    tag = true;
                }
                else if (c == '>' && tag)
                {
                    tag = false;
                }
                else if (!tag)
                {
                    length++;
                }
            }
            int lines = Mathf.Max(1, Mathf.CeilToInt(length * size / CharsPerLine));
            return Mathf.Max(64f, 36f + 40f * lines * size);
        }

        private void Header()
        {
            UiClone.Words(_shell.Header, "reallife_guide_app_title".GetLocalization());
            Transform title = _shell.Headers != null ? _shell.Headers.Find("ColumnText/Title") : null;
            if (title != null)
            {
                UiClone.Key(title, _tab == 0 ? "reallife_guide_col_pages" : "reallife_guide_col_reading");
            }
        }

        // The white box: which page, a line about it, and the way back to the list or to what changed.
        private void Box()
        {
            InfoBox box = _shell.Box;
            box.Clear();
            if (_tab == 1 && _topic != null)
            {
                box.Title(_topic.Title);
                box.Note("reallife_guide_reading_hint".GetLocalization());
                Transform row = box.ButtonRow();
                InfoBox.Button(row, InfoBox.Positive, "reallife_guide_back", () => ShowTab(0));
            }
            else
            {
                box.Title("reallife_guide_app_title".GetLocalization());
                box.Subtitle("reallife_guide_version".GetLocalization().Replace("{version}", Changelog.Shown));
                if (Changelog.IsBeta)
                {
                    box.Note("reallife_beta_guide_note".GetLocalization());
                }
                box.Note("reallife_guide_list_hint".GetLocalization());
                Transform row = box.ButtonRow();
                InfoBox.Button(row, InfoBox.Positive, "reallife_guide_whatsnew_title", () => Want("whatsnew"));
            }
            box.Done();
        }
    }
}
