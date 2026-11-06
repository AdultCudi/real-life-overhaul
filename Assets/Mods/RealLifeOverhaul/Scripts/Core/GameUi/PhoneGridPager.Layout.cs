using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>The pager's layout: where the page of the full menu's row and of the HUD phone's grid stands.</summary>
    public sealed partial class PhoneGridPager
    {
        // How many pages the row needs and where they stand, from where the game's own row begins in the row's space.
        private void StripPages()
        {
            var strip = (RectTransform)transform;
            var layout = GetComponent<HorizontalLayoutGroup>();
            var cell = _template as RectTransform;
            int count = _items.Count;
            Pages = 1;
            _own = false;
            if (layout == null || cell == null || count < 1)
            {
                return;
            }
            float pitch = Pitch(layout, cell);
            if (pitch < 1f)
            {
                return;
            }
            RowAsBuilt built = AsBuilt(layout);
            if (_gameApps > 0 && _gameShown == count && _open < 0)
            {
                _own = true;   // only the game's own apps: its row as the game laid it out
                return;
            }
            float inner = strip.rect.width - built.Left - layout.padding.right;
            float gameRow = PagerRules.RowWidth(PagerRules.StripTiles(_gameApps), pitch, layout.spacing);
            float rowStart = strip.rect.xMin + built.Left + (inner - gameRow) * ((int)built.Alignment % 3) * 0.5f;   // where the game's own row begins
            float barWidth = strip.parent is RectTransform bar ? bar.rect.width : 0f;
            if (PagerRules.Remeasure(barWidth, _measuredFor) && strip.gameObject.activeInHierarchy)
            {
                // Once for each screen: the room to the money's box, never its words.
                _room = DataLeft(strip) - rowStart;
                _tiles = PagerRules.StripTiles(_room, pitch, layout.spacing, _gameApps);
                _measuredFor = barWidth;
            }
            _pagesLeft = PagerRules.PageStart(rowStart, _room, Tiles, pitch, layout.spacing);   // a full page centred in the room: the button stands still
            Pages = PagerRules.Pages(count, ItemTiles);
        }

        // From where a full page begins: a shorter last page keeps the same left edge.
        private void PlaceStripPage()
        {
            var strip = (RectTransform)transform;
            var layout = GetComponent<HorizontalLayoutGroup>();
            var cell = _template as RectTransform;
            if (layout == null || cell == null)
            {
                return;
            }
            if (_own || _apps.Count == 0)
            {
                RestoreRow();
                return;
            }
            RowAsBuilt built = AsBuilt(layout);
            float pitch = Pitch(layout, cell);
            float begin = _pagesLeft;
            var alignment = (TextAnchor)((int)built.Alignment / 3 * 3);   // the same row, from the left
            int padding = Mathf.RoundToInt(begin - strip.rect.xMin);
            if (layout.childAlignment != alignment || layout.padding.left != padding)
            {
                layout.childAlignment = alignment;
                layout.padding.left = padding;
            }
            if (strip.gameObject.activeInHierarchy)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(strip);   // in place before anything is drawn
                if (!layout.isActiveAndEnabled || Stacked(strip, pitch))
                {
                    ByHand(strip, layout, begin, pitch - layout.spacing);
                }
            }
            else
            {
                LayoutRebuilder.MarkLayoutForRebuild(strip);   // and the pass when the menu opens looks again
            }
        }

        // Two buttons that show standing where one does: the layout has not placed them.
        private static bool Stacked(RectTransform strip, float pitch)
        {
            bool first = true;
            float last = 0f;
            for (int i = 0; i < strip.childCount; i++)
            {
                var child = strip.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf || Ignored(child))
                {
                    continue;
                }
                float left = child.localPosition.x + child.rect.xMin * child.localScale.x;
                if (!first && Mathf.Abs(left - last) < pitch * 0.25f)
                {
                    return true;
                }
                first = false;
                last = left;
            }
            return false;
        }

        // The buttons placed as the row's layout would place them, only when it has not.
        private void ByHand(RectTransform strip, HorizontalLayoutGroup layout, float begin, float cellWidth)
        {
            if (!_toldByHand)
            {
                _toldByHand = true;
                UiClone.Fallback("the full menu's row of app buttons was not laid out by its own layout (" + (layout.enabled ? "switched on" : "switched off")
                                 + "), so the pages place its buttons themselves; it showed " + Shown(strip));
            }
            Rect box = strip.rect;
            float x = begin - box.xMin;   // from the row's left edge, as the layout counts
            float row = (int)layout.childAlignment / 3 * 0.5f;   // upper, middle or lower
            for (int i = 0; i < strip.childCount; i++)
            {
                var child = strip.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf || Ignored(child))
                {
                    continue;
                }
                float width = layout.childControlWidth ? Mathf.Max(LayoutUtility.GetPreferredWidth(child), child.rect.width) : child.rect.width;
                if (width < 1f)
                {
                    width = cellWidth;
                }
                float height = child.rect.height;
                float top = layout.padding.top + (box.height - layout.padding.vertical - height) * row;
                child.anchorMin = child.anchorMax = new Vector2(0f, 1f);
                child.anchoredPosition = new Vector2(x + width * child.pivot.x, -(top + height * (1f - child.pivot.y)));
                x += width + layout.spacing;
            }
        }

        // For the diagnostics line: each button's name and left edge.
        private static string Shown(RectTransform strip)
        {
            var said = new System.Text.StringBuilder(256);
            for (int i = 0; i < strip.childCount; i++)
            {
                var child = strip.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf)
                {
                    continue;
                }
                said.Append(said.Length == 0 ? string.Empty : ", ").Append(child.name).Append(" at ")
                    .Append((child.localPosition.x + child.rect.xMin * child.localScale.x).ToString("0"));
            }
            return said.Length == 0 ? "nothing" : said.ToString();
        }

        // A child the layout leaves out (LayoutElement.ignoreLayout): placed by whoever put it there.
        private static bool Ignored(RectTransform child)
        {
            var ignorer = child.GetComponent<ILayoutIgnorer>();
            return ignorer != null && ignorer.ignoreLayout;
        }

        // How the game built the row or the grid, kept on it the first time it is paged, for any pager after this one.
        private RowAsBuilt AsBuilt(LayoutGroup layout)
        {
            var built = GetComponent<RowAsBuilt>();
            if (built == null)
            {
                built = gameObject.AddComponent<RowAsBuilt>();
                built.Alignment = layout.childAlignment;
                built.Left = layout.padding.left;
            }
            return built;
        }

        private static float Pitch(HorizontalLayoutGroup layout, RectTransform cell)
        {
            float width = layout.childControlWidth ? Mathf.Max(LayoutUtility.GetPreferredWidth(cell), cell.rect.width) : cell.rect.width;
            return width + layout.spacing;
        }

        private void RestoreRow()
        {
            var layout = GetComponent<LayoutGroup>();
            var built = GetComponent<RowAsBuilt>();
            if (layout == null || built == null || layout.childAlignment == built.Alignment && layout.padding.left == built.Left)
            {
                return;
            }
            layout.childAlignment = built.Alignment;
            layout.padding.left = built.Left;
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }

        // The left edge of the money's box (RightData, 1350 wide) in the row's space: the box, never its words. No limit
        // without it.
        private float DataLeft(RectTransform strip)
        {
            if (_rightData == null)
            {
                return float.MaxValue;
            }
            _rightData.GetWorldCorners(Corners);
            return strip.InverseTransformPoint(Corners[0]).x;
        }

        // Diagnostics, once each time the menu opens: where a full page ends against where the money begins.
        private void StripCheck()
        {
            var strip = (RectTransform)transform;
            var layout = GetComponent<HorizontalLayoutGroup>();
            var cell = _template as RectTransform;
            if (_own || Pages < 2 || layout == null || cell == null)
            {
                return;
            }
            float end = _pagesLeft + PagerRules.RowWidth(Tiles, Pitch(layout, cell), layout.spacing);
            float money = RightLimit(strip);
            Logger?.Info("Game UI: full menu row check (" + _apps.Count + " apps, " + Pages + " pages of " + (Tiles - 1) + " and the More apps button, "
                         + Tiles + " tiles for a bar " + _measuredFor.ToString("0") + " wide, the money's box at " + DataLeft(strip).ToString("0")
                         + "): the row runs from " + _pagesLeft.ToString("0") + " to " + end.ToString("0") + ", the money and the date begin at "
                         + (money == float.MaxValue ? "nothing found" : money.ToString("0")) + ": "
                         + (end <= money ? "PASS" : Tiles <= PagerRules.LeastTiles ? "the screen leaves room for no more than the least page" : "the money begins under the row's end") + ".");
        }

        // The left edge of what is drawn of the money, the date and the time (a text can be wider than its part); failing
        // them, the bar's right edge. Only for the diagnostics line.
        private float RightLimit(RectTransform strip)
        {
            float right = float.MaxValue;
            if (_rightData != null && _rightData.gameObject.activeInHierarchy)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_rightData);   // their widths as they read now
                for (int i = 0; i < _rightData.childCount; i++)
                {
                    var part = _rightData.GetChild(i) as RectTransform;
                    if (part == null || !part.gameObject.activeSelf)
                    {
                        continue;
                    }
                    right = Mathf.Min(right, LeftEdge(strip, part));
                    Graphic[] drawn = part.GetComponentsInChildren<Graphic>(false);
                    for (int g = 0; g < drawn.Length; g++)
                    {
                        if (drawn[g].enabled)
                        {
                            right = Mathf.Min(right, LeftEdge(strip, drawn[g].rectTransform));
                        }
                    }
                }
            }
            if (right == float.MaxValue && strip.parent is RectTransform bar)
            {
                bar.GetWorldCorners(Corners);
                right = strip.InverseTransformPoint(Corners[2]).x;
            }
            return right;
        }

        private static float LeftEdge(RectTransform strip, RectTransform part)
        {
            if (part.rect.width < 1f)
            {
                return float.MaxValue;
            }
            part.GetWorldCorners(Corners);
            return strip.InverseTransformPoint(Corners[0]).x;
        }

        // A short page keeps the full page's left edge: the game's grid centres the columns it uses.
        private void PlaceGridPage()
        {
            var grid = GetComponent<GridLayoutGroup>();
            if (grid == null)
            {
                return;
            }
            if (Pages < 2 && !_foldered)
            {
                RestoreRow();   // in folders a short page keeps the left edge too
                return;
            }
            if (grid.startCorner != GridLayoutGroup.Corner.UpperLeft || grid.startAxis != GridLayoutGroup.Axis.Horizontal)
            {
                return;   // not the grid the game builds: its own layout
            }
            RowAsBuilt built = AsBuilt(grid);
            var alignment = (TextAnchor)((int)built.Alignment / 3 * 3);   // the same rows, from the left
            int padding = Mathf.RoundToInt(GridLeft(grid, built));
            if (grid.childAlignment != alignment || grid.padding.left != padding)
            {
                grid.childAlignment = alignment;
                grid.padding.left = padding;
                LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
            }
        }

        // Where a full page's first column begins, from the grid's left edge.
        private static float GridLeft(GridLayoutGroup grid, RowAsBuilt built)
        {
            float width = ((RectTransform)grid.transform).rect.width;
            int columns = Columns(grid, built);
            float required = columns * grid.cellSize.x + (columns - 1) * grid.spacing.x;
            return built.Left + (width - built.Left - grid.padding.right - required) * ((int)built.Alignment % 3) * 0.5f;
        }

        private static int Columns(GridLayoutGroup grid, RowAsBuilt built)
        {
            if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            {
                return Mathf.Max(1, grid.constraintCount);
            }
            float width = ((RectTransform)grid.transform).rect.width - built.Left - grid.padding.right;
            return Mathf.Max(1, PagerRules.Fit(width, grid.cellSize.x + grid.spacing.x, grid.spacing.x));
        }
    }
}
