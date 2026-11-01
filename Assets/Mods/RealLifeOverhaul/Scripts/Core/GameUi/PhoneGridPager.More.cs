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
        // A tile of the grid while there are pages; Order puts it after the page's items and gaps, the page's last tile.
        private void More()
        {
            if (!PagerRules.ButtonShown(Pages) || _aside)
            {
                if (_more != null && _more.gameObject.activeSelf)
                {
                    _more.gameObject.SetActive(false);
                }
                return;
            }
            if (_more == null && !MakeMore())
            {
                return;
            }
            if (!_more.gameObject.activeSelf)
            {
                _more.gameObject.SetActive(true);
            }
            Label();
        }

        /// <summary>
        /// Where the More apps button stands now, for the checks: a tile of the grid and the page's last, or not there with
        /// one page. Ok is false when it stands anywhere else.
        /// </summary>
        internal string MoreSays(out bool ok)
        {
            int shown = 0, at = -1;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == _template || !child.gameObject.activeSelf || !(child is RectTransform placed) || Ignored(placed))
                {
                    continue;
                }
                if (child == _more)
                {
                    at = shown;
                }
                shown++;
            }
            if (!PagerRules.ButtonShown(Pages))
            {
                ok = at < 0;
                return "one page, " + (ok ? "no More apps button" : "a More apps button that should not be there");
            }
            int want = PhoneLayoutRules.MoreTile(_strip, Tiles);
            ok = at == want && shown == want + 1;
            return "the More apps button on tile " + (at + 1) + " of " + shown + " (wanted " + (want + 1) + " of " + (want + 1) + ")";
        }

        // The empty tiles of a short page: made as first needed, the first count on, the rest off. A gap is as wide as a
        // button where the row sizes its children; the phone's grid gives every child a cell.
        private void ShowGaps(int count)
        {
            while (_gaps.Count < count)
            {
                var gap = new GameObject(GapName, typeof(RectTransform));   // no picture, nothing to click: a tile the layout keeps empty
                gap.AddComponent<PhoneTile>();
                if (_template is RectTransform cell)
                {
                    var room = gap.AddComponent<LayoutElement>();
                    room.minWidth = room.preferredWidth = cell.rect.width;
                    room.minHeight = room.preferredHeight = cell.rect.height;
                }
                gap.SetActive(false);
                gap.transform.SetParent(transform, false);
                _gaps.Add(gap.transform);
            }
            for (int g = 0; g < _gaps.Count; g++)
            {
                bool on = g < count;
                if (_gaps[g] != null && _gaps[g].gameObject.activeSelf != on)
                {
                    _gaps[g].gameObject.SetActive(on);
                }
            }
        }

        private Transform Gap(int index)
        {
            return index >= 0 && index < _gaps.Count ? _gaps[index] : null;
        }

        // The button and the empty tiles taken away: stepping aside, or the pager gone.
        private void DropTiles()
        {
            if (_more != null)
            {
                Retire(_more);
                _more = null;
            }
            for (int g = 0; g < _gaps.Count; g++)
            {
                Retire(_gaps[g]);
            }
            _gaps.Clear();
        }

        // Out of the grid at once, then destroyed, so a pager counts what is left at once.
        private static void Retire(Transform tile)
        {
            if (tile == null)
            {
                return;
            }
            if (GameManager.isCitySceneBeingUnloaded)
            {
                Destroy(tile.gameObject);
                return;
            }
            tile.gameObject.SetActive(false);
            tile.SetParent(null, false);
            Destroy(tile.gameObject);
        }

        // Made once from the apps' template, asleep until dressed, without the game's scripts that would make it an app, then
        // put in the grid as a tile of its own (PhoneTile: no pager counts it as an app). Only one object of its name: one a
        // pager left before, in the grid or beside it where earlier versions put it, is taken away.
        private bool MakeMore()
        {
            Transform parent = transform.parent;
            if (parent == null || _template == null)
            {
                return false;
            }
            Forget(parent);
            Forget(transform);
            Transform dormant = UiClone.Dormant(parent, MoreName + " (making)");
            GameObject copy = UiClone.Asleep(_template.gameObject, dormant, MoreName);
            if (copy == null)
            {
                Destroy(dormant.gameObject);
                return false;
            }
            var rect = (RectTransform)copy.transform;
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
            copy.AddComponent<PhoneTile>();
            copy.SetActive(false);
            rect.SetParent(transform, false);
            Destroy(dormant.gameObject);
            _more = rect;
            _labelPage = _labelPages = _badgeShown = -1;
            return true;
        }

        // A More apps button another pager left under this transform: taken away at once.
        private static void Forget(Transform under)
        {
            for (int i = under.childCount - 1; i >= 0; i--)
            {
                Transform old = under.GetChild(i);
                if (old.name == MoreName)
                {
                    old.gameObject.SetActive(false);
                    old.name = MoreName + " (gone)";
                    old.SetParent(null, false);
                    Destroy(old.gameObject);
                }
            }
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
        internal static int BadgeCount(Transform button)
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
