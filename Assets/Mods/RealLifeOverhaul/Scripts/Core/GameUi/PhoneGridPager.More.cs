using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Localizor.LanguageChangeEvent;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>The pager's More apps button: its tile, its page line, its icon and the badges.</summary>
    public sealed partial class PhoneGridPager
    {
        // On the page's last tile, while there are pages.
        private void More()
        {
            if (!PagerRules.ButtonShown(Pages) || _aside || !isActiveAndEnabled)
            {
                if (_more != null && _more.gameObject.activeSelf)   // out of sight it is placed, or made, when the grid shows again (OnEnable)
                {
                    _more.gameObject.SetActive(false);
                }
                return;
            }
            if (_more == null && !MakeMore())
            {
                return;
            }
            if (!Tile(MoreTile, out Vector3 centre, out Vector2 size))
            {
                _more.gameObject.SetActive(false);
                return;
            }
            _more.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            _more.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            _more.localScale = transform.localScale;
            _more.position = transform.TransformPoint(centre);
            int grid = transform.GetSiblingIndex(), now = _more.GetSiblingIndex();
            if (now != grid + 1)
            {
                _more.SetSiblingIndex(now < grid ? grid : grid + 1);   // drawn just after the grid, as one of its tiles
            }
            if (!_more.gameObject.activeSelf)
            {
                _more.gameObject.SetActive(true);
            }
            Label();
        }

        // The middle of a page's tile (from 0) in the grid's space, and its size.
        private bool Tile(int tile, out Vector3 centre, out Vector2 size)
        {
            centre = Vector3.zero;
            size = Vector2.zero;
            var box = ((RectTransform)transform).rect;
            if (_strip)
            {
                var layout = GetComponent<HorizontalLayoutGroup>();
                var cell = _template as RectTransform;
                if (layout == null || cell == null)
                {
                    return false;
                }
                float pitch = Pitch(layout, cell);
                float height = cell.rect.height;
                float top = layout.padding.top + (box.height - layout.padding.vertical - height) * ((int)layout.childAlignment / 3 * 0.5f);
                size = new Vector2(pitch - layout.spacing, height);
                centre = new Vector3(_pagesLeft + tile * pitch + size.x * 0.5f, box.yMax - top - height * 0.5f, 0f);
                return size.x >= 1f;
            }
            var grid = GetComponent<GridLayoutGroup>();
            if (grid == null)
            {
                return false;
            }
            RowAsBuilt built = AsBuilt(grid);
            int columns = Columns(grid, built);
            int rows = (Tiles + columns - 1) / columns;
            float column = tile % columns, line = tile / columns;
            float needed = rows * grid.cellSize.y + (rows - 1) * grid.spacing.y;
            float from = grid.padding.top + (box.height - grid.padding.vertical - needed) * ((int)built.Alignment / 3 * 0.5f);
            size = grid.cellSize;
            centre = new Vector3(box.xMin + GridLeft(grid, built) + column * (size.x + grid.spacing.x) + size.x * 0.5f,
                                 box.yMax - from - line * (size.y + grid.spacing.y) - size.y * 0.5f, 0f);
            return size.x >= 1f;
        }

        // Made once from the apps' template, asleep until dressed, without the game's scripts that would make it an app. Only
        // one object of its name under the grid's parent: one a pager left before is taken away.
        private bool MakeMore()
        {
            Transform parent = transform.parent;
            if (parent == null || _template == null)
            {
                return false;
            }
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform old = parent.GetChild(i);
                if (old.name == MoreName)
                {
                    old.gameObject.SetActive(false);
                    old.name = MoreName + " (gone)";
                    Destroy(old.gameObject);
                }
            }
            Transform dormant = UiClone.Dormant(parent, MoreName + " (making)");
            GameObject copy = UiClone.Asleep(_template.gameObject, dormant, MoreName);
            if (copy == null)
            {
                Destroy(dormant.gameObject);
                return false;
            }
            var rect = (RectTransform)copy.transform;
            var row = (RectTransform)transform;
            rect.anchorMin = row.anchorMin;   // anchored as the row or grid is: it moves with them
            rect.anchorMax = row.anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localRotation = Quaternion.identity;
            if (Tile(0, out Vector3 _, out Vector2 size))
            {
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);   // the tile's size, before the page's line is placed
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            }
            Image picture = Picture(copy.transform);   // through the game's own field, before its script is taken off
            var title = copy.GetComponentInChildren<TextLocalizationComponent>(true);
            UiClone.StripGameScripts(copy, KeptOnMore);
            Transform selected = copy.transform.Find("SelectedIcon");
            if (selected != null)
            {
                DestroyImmediate(selected.gameObject);
            }
            if (UiClone.Rewire(copy.transform, NextPage) == null)
            {
                UiClone.Fallback("the phone's app button has no Button, so the pages have no More apps button (the wheel still turns them)");
                Destroy(dormant.gameObject);
                return false;
            }
            if (picture != null)
            {
                Sprite icon = MoreIcon(picture.sprite);
                if (icon != null)
                {
                    picture.sprite = icon;
                }
            }
            if (title != null)
            {
                UiClone.Key(title, MoreTitleKey);
                _morePage = PageLine(rect, title, picture);
            }
            _moreBadge = copy.GetComponentInChildren<global::UI.Elements.Badge>(true);
            copy.AddComponent<MoreAppsWheel>().Pager = this;
            copy.SetActive(false);
            rect.SetParent(parent, false);
            Destroy(dormant.gameObject);
            _more = rect;
            _labelPage = _labelPages = _badgeShown = -1;
            return true;
        }

        // What stays of the game's scripts: its hover and sound, and the badge.
        internal static bool KeptOnMore(MonoBehaviour script)
        {
            return script is ButtonEffects || script is global::UI.Elements.Badge;
        }

        // The page, in a copy of the name's text: under the picture where the name stands over it (the full menu), one line
        // under the name where it stands under the picture (the HUD phone).
        internal static TextLocalizationComponent PageLine(RectTransform button, TextLocalizationComponent title, Image picture)
        {
            var titleRect = (RectTransform)title.transform;
            GameObject copy = Instantiate(title.gameObject, titleRect.parent, false);
            copy.name = "Page";
            var rect = (RectTransform)copy.transform;
            var words = copy.GetComponent<TMP_Text>();
            float line = words != null && words.fontSize > 1f ? words.fontSize * LineStep : 34f;
            RectTransform pictureRect = picture != null ? picture.rectTransform : null;
            if (pictureRect != null && pictureRect.parent == titleRect.parent && titleRect.localPosition.y > pictureRect.localPosition.y)
            {
                float bottom = pictureRect.localPosition.y + pictureRect.rect.yMin;
                float anchor = Mathf.Lerp(button.rect.yMin, button.rect.yMax, rect.anchorMax.y);
                rect.pivot = new Vector2(rect.pivot.x, 1f);
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, line);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, bottom - anchor);
            }
            else
            {
                rect.anchoredPosition = titleRect.anchoredPosition - new Vector2(0f, line);
            }
            if (words != null)
            {
                words.verticalAlignment = VerticalAlignmentOptions.Top;
            }
            var page = copy.GetComponent<TextLocalizationComponent>();
            UiClone.Key(page, MorePageKey);
            return page;
        }

        // "2 / 3", written only when it changes; a new dictionary, because the game's text writes itself again only when given
        // another object.
        private void Label()
        {
            if (_morePage == null)
            {
                return;
            }
            PagerRules.Shown(_page, Pages, out int shown, out int of);
            if (shown == _labelPage && of == _labelPages)
            {
                return;
            }
            _labelPage = shown;
            _labelPages = of;
            _morePage.Arguments = new Dictionary<string, string>(2)
            {
                { "page", shown.ToString(CultureInfo.InvariantCulture) },
                { "pages", of.ToString(CultureInfo.InvariantCulture) },
            };
        }

        // The game's red badges: each folder's apps' numbers added up, on the Back tile the buttons outside the folder with
        // theirs up, on the More apps button the items on other pages with theirs up. Each written only when it changes.
        private void Paint()
        {
            if (_aside)
            {
                return;
            }
            if (_foldered && _folders != null)
            {
                PaintFolders();
            }
            if (_moreBadge == null)
            {
                return;
            }
            int waiting = 0;
            if (Pages > 1)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i] != null && PageOf(i) != _page && BadgeUp(_items[i]))
                    {
                        waiting++;
                    }
                }
            }
            if (waiting == _badgeShown)
            {
                return;
            }
            _badgeShown = waiting;
            try
            {
                ShowBadge(_moreBadge, waiting);
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the badge on the More apps button");
                _moreBadge = null;   // a game without the badge's call: the button goes without it
            }
        }

        // The game's Badge switches its object on for a number over 0.
        private static bool BadgeUp(Transform button)
        {
            var badge = button.GetComponentInChildren<global::UI.Elements.Badge>(true);
            return badge != null && badge.gameObject.activeSelf;
        }

        private void PaintFolders()
        {
            int count = _apps.Count;
            int outside = 0;
            for (int i = 0; i < count; i++)
            {
                _badge[i] = _kind[i] == PhoneLayoutRules.ModItem ? BadgeCount(_apps[i]) : 0;
                if (_open >= 0 && PhoneLayoutRules.FolderOf(_kind[i], _place[i]) != _open && _apps[i] != null && BadgeUp(_apps[i]))
                {
                    outside++;
                }
            }
            PhoneLayoutRules.FolderBadges(_kind, _place, _badge, count, _sums);
            for (int f = 0; f < PhoneLayoutRules.FolderCount; f++)
            {
                _folders.Badge(f, _sums[f]);
            }
            _folders.BackBadge(_open >= 0 ? outside : 0);
        }

        // Read from the badge's words ("99+" is 99), whoever set it; 0 while the badge is down.
        private static int BadgeCount(Transform button)
        {
            var badge = button != null ? button.GetComponentInChildren<global::UI.Elements.Badge>(true) : null;
            if (badge == null || !badge.gameObject.activeSelf)
            {
                return 0;
            }
            var label = badge.GetComponentInChildren<TMP_Text>(true);
            string words = label != null ? label.text : null;
            int number = 0;
            for (int c = 0; words != null && c < words.Length && number < 10000; c++)
            {
                char d = words[c];
                if (d >= '0' && d <= '9')
                {
                    number = number * 10 + (d - '0');
                }
            }
            return number > 0 ? number : 1;   // up, with no number read: one
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ShowBadge(global::UI.Elements.Badge badge, int count)
        {
            badge.UpdateBadge(count);
        }

        // An app icon of the game's manner on a light grey tile with the game's chevrons in dark ink, or drawn ones.
        private static Sprite MoreIcon(Sprite like)
        {
            Sprite glyph = GameSprites.Get(MoreGlyph) ?? DrawnGlyph();
            return AppIconPainter.Paint(like, glyph, MoreTop, MoreBottom, GlyphShare, MoreIconName, MoreInk);
        }

        // Two white chevrons, 64 by 64 as the game's glyph: made once a session, kept.
        internal static Sprite DrawnGlyph()
        {
            if (_drawnGlyph != null)
            {
                return _drawnGlyph;
            }
            const int size = 64;
            const float stroke = 9f;   // pixels: each chevron's line, as heavy as the game's
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Mathf.Min(Chevron(x + 0.5f, y + 0.5f, 30f), Chevron(x + 0.5f, y + 0.5f, 48f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(stroke * 0.5f - distance + 0.5f) * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;   // kept for the session
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "RealLife-MoreAppsGlyph";
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            _drawnGlyph = sprite;
            return sprite;
        }

        // Distance from a chevron pointing right with its tip at (tip, 32).
        private static float Chevron(float x, float y, float tip)
        {
            return Mathf.Min(Segment(x, y, tip - 14f, 14f, tip, 32f), Segment(x, y, tip - 14f, 50f, tip, 32f));
        }

        private static float Segment(float x, float y, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy));
            float px = ax + t * dx - x, py = ay + t * dy - y;
            return Mathf.Sqrt(px * px + py * py);
        }

        // The game's own field (AppButton's "icon": "Logo" on the full menu, "Icon" on the HUD phone), else a child of either name.
        internal static Image Picture(Transform button)
        {
            if (!_pictureLooked)
            {
                _pictureLooked = true;
                _pictureField = GameContract.AppButtonIcon.Field;
            }
            var script = button.GetComponent<AppButton>();
            if (_pictureField != null && script != null && _pictureField.GetValue(script) is Image own && own != null)
            {
                return own;
            }
            Transform named = FindDeep(button, "Logo") ?? FindDeep(button, "Icon");
            return named != null ? named.GetComponent<Image>() : null;
        }

        private static Transform FindDeep(Transform under, string name)
        {
            for (int i = 0; i < under.childCount; i++)
            {
                Transform child = under.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }
                Transform deeper = FindDeep(child, name);
                if (deeper != null)
                {
                    return deeper;
                }
            }
            return null;
        }
    }
}
