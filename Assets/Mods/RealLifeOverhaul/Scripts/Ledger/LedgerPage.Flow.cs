using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>The paper's page: its rules and its columns.</summary>
    public sealed partial class LedgerPage
    {
        // Marks in a text, read back from TextMeshPro's layout (links of no width, never seen):
        //   r  a rule across the column above this line        c  a short rule above this line (between two stories)
        //   u  a rule under this line (a label sitting on it)   k  keep from here...   e  ...to here in one column
        private const string MarkRule = "<link=\"r\"></link>", MarkCut = "<link=\"c\"></link>", MarkUnder = "<link=\"u\"></link>";
        private const string MarkKeep = "<link=\"k\"></link>", MarkEnd = "<link=\"e\"></link>";

        /// <summary>A page's thin rules from one pool, taken in order as it is written; the ones left over are hidden.</summary>
        private sealed class Rules
        {
            private readonly RectTransform _page;
            private readonly List<Image> _made = new List<Image>(48);
            private int _used;

            public Rules(RectTransform page)
            {
                _page = page;
            }

            public void Begin()
            {
                _used = 0;
            }

            public void End()
            {
                for (int i = _used; i < _made.Count; i++)
                {
                    if (_made[i].gameObject.activeSelf)
                    {
                        _made[i].gameObject.SetActive(false);
                    }
                }
            }

            public void Across(float x, float y, float w, float thick)
            {
                Put(Next().rectTransform, x, y - thick * 0.5f, w, thick);
            }

            public void Down(float x, float y, float h, float thick)
            {
                Put(Next().rectTransform, x - thick * 0.5f, y, thick, h);
            }

            public void Frame(float x, float y, float w, float h, float thick)
            {
                Across(x, y + thick * 0.5f, w, thick);
                Across(x, y + h - thick * 0.5f, w, thick);
                Down(x + thick * 0.5f, y, h, thick);
                Down(x + w - thick * 0.5f, y, h, thick);
            }

            private Image Next()
            {
                if (_used < _made.Count)
                {
                    Image kept = _made[_used++];
                    if (!kept.gameObject.activeSelf)
                    {
                        kept.gameObject.SetActive(true);
                    }
                    return kept;
                }
                var go = new GameObject("Rule", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_page, false);
                var image = go.GetComponent<Image>();
                image.color = RuleInk;
                image.raycastTarget = false;
                _made.Add(image);
                _used++;
                return image;
            }
        }

        /// <summary>
        /// A story's columns, linked one into the next (TextMeshPro's linked overflow) and balanced, with no label or
        /// headline left at a column's foot without its first line; a drop cap has a narrower text beside it.
        /// </summary>
        private sealed class Columns
        {
            private readonly LedgerPage _page;
            public readonly TMP_Text Narrow;      // the first column's lines beside the drop cap
            public readonly TMP_Text Cap;         // the drop cap
            public readonly TMP_Text[] Cols;      // the columns, each linked into the next; the last ends with an ellipsis
            private readonly float[] _x, _depth;
            private float _w, _top, _restTop, _size, _lineSpacing;
            private int _capLines;
            private int _chained = -1;            // how the parts are linked: 1 the narrow text first, 0 without it, -1 not yet

            public float Depth { get; private set; }
            public bool Fitted { get; private set; }

            public Columns(LedgerPage page, RectTransform parent, int count, string name, float lineSpacing, float paragraphSpacing)
            {
                _page = page;
                _lineSpacing = lineSpacing;
                _x = new float[count];
                _depth = new float[count];
                Cols = new TMP_Text[count];
                Narrow = page.Text(parent, name + "Narrow", TextAlignmentOptions.TopJustified);
                for (int i = 0; i < count; i++)
                {
                    Cols[i] = page.Text(parent, name + i, TextAlignmentOptions.TopJustified);
                }
                Cap = page.Text(parent, name + "Cap", TextAlignmentOptions.TopLeft, FontStyles.Bold);
                Cap.textWrappingMode = TextWrappingModes.NoWrap;
                Narrow.lineSpacing = lineSpacing;
                Narrow.paragraphSpacing = paragraphSpacing;
                for (int i = 0; i < count; i++)
                {
                    Cols[i].lineSpacing = lineSpacing;
                    Cols[i].paragraphSpacing = paragraphSpacing;
                }
                Chain(false);
            }

            // The parts linked in reading order, with or without the narrow text beside a drop cap. TextMeshPro's link
            // setter lets go of the whole chain after the text it unlinks, so every link is made again, in order.
            private void Chain(bool cap)
            {
                int want = cap ? 1 : 0;
                if (_chained == want)
                {
                    return;
                }
                _chained = want;
                Narrow.linkedTextComponent = null;
                for (int i = 0; i < Cols.Length; i++)
                {
                    Cols[i].linkedTextComponent = null;
                }
                for (int i = 0; i + 1 < Cols.Length; i++)
                {
                    Cols[i].overflowMode = TextOverflowModes.Linked;
                    Cols[i].linkedTextComponent = Cols[i + 1];
                }
                Cols[Cols.Length - 1].overflowMode = TextOverflowModes.Ellipsis;
                if (cap)
                {
                    Narrow.overflowMode = TextOverflowModes.Linked;
                    Narrow.linkedTextComponent = Cols[0];
                }
                else
                {
                    Narrow.overflowMode = TextOverflowModes.Truncate;
                }
            }

            /// <summary>How many lines a drop cap on this text can span: three, else two; none if too little text beside it, or no letter.</summary>
            public int CapLines(string text, float w, out bool endsBeside)
            {
                endsBeside = false;
                if (string.IsNullOrEmpty(text) || !char.IsLetter(text[0]) || text.Length < 12)
                {
                    return 0;
                }
                float adv = _page.Advance(_size, _lineSpacing);
                string rest = AfterCap(text);
                for (int n = 3; n >= 2; n--)
                {
                    Cap.fontSize = ((n - 1) * adv + _page._cap * _size) / Mathf.Max(0.1f, _page._cap);
                    float narrow = w - (Cap.GetPreferredValues(Plain(text.Substring(0, 1))).x + 0.28f * _size);
                    int lines = _page.Lines(Measure, rest, narrow, _size, _lineSpacing);
                    if (lines >= n)
                    {
                        endsBeside = lines == n;
                        return n;
                    }
                }
                return 0;
            }

            /// <summary>The text that measures for the columns: the last, in their size and spacing.</summary>
            public TMP_Text Measure => Cols[Cols.Length - 1];

            public void Size(float size)
            {
                _size = size;
                Narrow.fontSize = size;
                for (int i = 0; i < Cols.Length; i++)
                {
                    Cols[i].fontSize = size;
                }
            }

            /// <summary>Lays the text out in columns from 'top', never deeper than 'most'; with a cap letter, the text follows it.</summary>
            public void Lay(string text, float x0, float w, float gutter, float top, float most, string capLetter, int capLines, bool capThenGap)
            {
                int k = Cols.Length;
                _w = w;
                _top = top;
                for (int i = 0; i < k; i++)
                {
                    _x[i] = x0 + i * (w + gutter);
                }
                float adv = _page.Advance(_size, _lineSpacing);
                bool cap = !string.IsNullOrEmpty(capLetter) && capLines >= 2;
                _capLines = cap ? capLines : 0;
                TMP_Text head = cap ? Narrow : Cols[0];
                if (cap)
                {
                    Show(Narrow, true);
                    Chain(true);
                    float capSize = ((capLines - 1) * adv + _page._cap * _size) / Mathf.Max(0.1f, _page._cap);
                    Cap.fontSize = capSize;
                    Set(Cap, Plain(capLetter));
                    float capWide = Cap.GetPreferredValues(Cap.text).x + 0.28f * _size;
                    Put(Cap.rectTransform, x0 - 0.03f * capSize, top + _page._asc * _size + (capLines - 1) * adv - _page._asc * capSize, capWide + capSize, capSize * 1.5f);
                    Show(Cap, true);
                    Put(Narrow.rectTransform, x0 + capWide, top, w - capWide, _page._asc * _size + (capLines - 1) * adv + _page._desc * _size + 0.3f * _size);
                    _restTop = top + capLines * adv + (capThenGap ? Cols[0].paragraphSpacing * 0.01f * _size : 0f);
                }
                else
                {
                    Show(Cap, false);
                    Chain(false);
                    Set(Narrow, string.Empty);
                    Show(Narrow, false);
                    _restTop = top;
                }
                float need = Measure.GetPreferredValues(text, w, 0f).y;
                float depth = Mathf.Clamp(Mathf.Floor(need / k / Mathf.Max(1f, adv)) * adv + _page._desc * _size, 3f * adv, Mathf.Max(3f * adv, most));
                for (int i = 0; i < k; i++)
                {
                    _depth[i] = depth;
                }
                Set(head, text);
                if (head != Narrow && Narrow.text.Length > 0)
                {
                    Set(Narrow, string.Empty);
                }
                bool fitted = false;
                for (int pass = 0; pass < 40; pass++)
                {
                    Place();
                    head.ForceMeshUpdate();
                    TMP_Text last = Cols[k - 1];
                    fitted = !(last.text.Length > 0 && last.isTextTruncated);
                    if (!fitted)
                    {
                        if (depth + adv <= most + 0.5f)
                        {
                            depth += adv;
                            for (int i = 0; i < k; i++)
                            {
                                _depth[i] = depth;
                            }
                            continue;
                        }
                        break;
                    }
                    if (!Keep())
                    {
                        break;
                    }
                }
                Depth = depth;
                Fitted = fitted;
            }

            private void Place()
            {
                for (int i = 0; i < Cols.Length; i++)
                {
                    float y = i == 0 ? _restTop : _top;
                    Put(Cols[i].rectTransform, _x[i], y, _w, Mathf.Max(1f, _depth[i] - (y - _top)));
                }
            }

            // The parts in the order the text runs through them, with the column each is in.
            private int PartCount => Cols.Length + (_capLines > 0 ? 1 : 0);

            private TMP_Text Part(int i)
            {
                return _capLines > 0 ? (i == 0 ? Narrow : Cols[i - 1]) : Cols[i];
            }

            private int ColumnOf(int i)
            {
                return _capLines > 0 ? Mathf.Max(0, i - 1) : i;
            }

            private float TopOf(int i)
            {
                return _capLines > 0 && i == 1 ? _restTop - _top : 0f;
            }

            // A label or headline ('k') whose first line under it ('e') went on into the next column: its own column
            // ends above it, so that it starts the next one. True if a column was cut (laid out again).
            private bool Keep()
            {
                int keepPart = -1;
                float keepTop = 0f;
                bool keepFirst = false;
                for (int p = 0; p < PartCount; p++)
                {
                    TMP_Text t = Part(p);
                    TMP_TextInfo info = t.textInfo;
                    if (info == null || t.text.Length == 0)
                    {
                        continue;
                    }
                    int first = p == 0 ? 0 : t.firstVisibleCharacter;
                    for (int l = 0; l < info.linkCount; l++)
                    {
                        TMP_LinkInfo link = info.linkInfo[l];
                        int c = link.linkTextfirstCharacterIndex;
                        if (c < first || c >= info.characterCount)
                        {
                            continue;
                        }
                        char id = Id(link);
                        int line = info.characterInfo[c].lineNumber;
                        if (id == 'k')
                        {
                            keepPart = p;
                            keepTop = TopOf(p) - info.lineInfo[line].ascender;
                            // at a column's top, or beside the cap (whose lines stay): nothing to keep it with
                            keepFirst = (line == 0 && (p == 0 || ColumnOf(p - 1) != ColumnOf(p))) || (_capLines > 0 && p == 0);
                        }
                        else if (id == 'e' && keepPart >= 0)
                        {
                            int a = ColumnOf(keepPart);
                            if (a != ColumnOf(p) && !keepFirst && a + 1 < Cols.Length && keepTop - 0.5f < _depth[a])
                            {
                                _depth[a] = keepTop - 0.5f;
                                return true;
                            }
                            keepPart = -1;
                        }
                    }
                }
                return false;
            }

            /// <summary>The rules the text asks for ('r' across, 'c' short), each in the white above its line; none at a column's top.</summary>
            public void Marks(Rules rules, float thick)
            {
                float prevBaseline = float.NaN;   // the last line's baseline on the page, while still in the same column
                int prevColumn = -1;
                for (int p = 0; p < PartCount; p++)
                {
                    TMP_Text t = Part(p);
                    TMP_TextInfo info = t.textInfo;
                    int column = ColumnOf(p);
                    if (column != prevColumn)
                    {
                        prevBaseline = float.NaN;
                        prevColumn = column;
                    }
                    if (info == null || t.text.Length == 0 || info.lineCount == 0)
                    {
                        continue;
                    }
                    float x = t.rectTransform.anchoredPosition.x, y = -t.rectTransform.anchoredPosition.y;
                    float w = t.rectTransform.sizeDelta.x;
                    if (p == 0 && _capLines > 0)
                    {
                        x = _x[0];   // the rules of the first column span it, the cap's side too
                        w = _w;
                    }
                    int first = p == 0 ? 0 : t.firstVisibleCharacter;
                    for (int l = 0; l < info.linkCount; l++)
                    {
                        TMP_LinkInfo link = info.linkInfo[l];
                        int c = link.linkTextfirstCharacterIndex;
                        if (c < first || c >= info.characterCount)
                        {
                            continue;
                        }
                        char id = Id(link);
                        if (id != 'r' && id != 'c')
                        {
                            continue;
                        }
                        int line = info.characterInfo[c].lineNumber;
                        float above = line > 0 ? y - info.lineInfo[line - 1].baseline : prevBaseline;
                        if (float.IsNaN(above))
                        {
                            continue;   // the top of a column: no rule
                        }
                        TMP_LineInfo li = info.lineInfo[line];
                        float capTop = y - (li.baseline + (li.ascender - li.baseline) * _page._capRatio);
                        float at = (above + capTop) * 0.5f;
                        float colX = _x[column];
                        if (id == 'r')
                        {
                            rules.Across(colX, at, _w, thick);
                        }
                        else
                        {
                            rules.Across(colX + _w * 0.4f, at, _w * 0.2f, thick);
                        }
                    }
                    prevBaseline = y - info.lineInfo[info.lineCount - 1].baseline;
                }
            }

            public void Gutters(Rules rules, float gutter, float thick)
            {
                for (int i = 1; i < Cols.Length; i++)
                {
                    rules.Down(_x[i] - gutter * 0.5f, _top + thick, Depth - 2f * thick, thick);
                }
            }
        }

        /// <summary>A single text's rules under its labels ('u'), each a little under the label's baseline, across the text's width.</summary>
        private void Under(TMP_Text t, Rules rules)
        {
            TMP_TextInfo info = t.textInfo;
            if (info == null || t.text.Length == 0)
            {
                return;
            }
            float x = t.rectTransform.anchoredPosition.x, y = -t.rectTransform.anchoredPosition.y, w = t.rectTransform.sizeDelta.x;
            for (int l = 0; l < info.linkCount; l++)
            {
                TMP_LinkInfo link = info.linkInfo[l];
                int c = link.linkTextfirstCharacterIndex;
                if (c < 0 || c >= info.characterCount || Id(link) != 'u')
                {
                    continue;
                }
                TMP_LineInfo li = info.lineInfo[info.characterInfo[c].lineNumber];
                float size = (li.ascender - li.baseline) / Mathf.Max(0.1f, _asc);
                rules.Across(x, y - li.baseline + 0.3f * size, w, _px);
            }
        }

        // The first letter of a mark's name (read once a link is laid out; a page is written a few times a day).
        private static char Id(TMP_LinkInfo link)
        {
            string id = link.GetLinkID();
            return string.IsNullOrEmpty(id) ? ' ' : id[0];
        }
    }
}
