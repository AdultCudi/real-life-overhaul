using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Localizor.LanguageChangeEvent;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The tiles a pager lays its folders out with: a folder's (a copy of the app button with an icon of its first nine
    /// apps), the Back tile, and empty tiles that keep the HUD phone's dock on the last row. Each carries a PhoneTile, so no
    /// pager counts it as an app. Costs nothing a frame.
    /// </summary>
    internal sealed class PhoneFolders
    {
        private const string FolderPrefix = "RealLife Folder ";   // followed by the folder's id
        private const string BackName = "RealLife Folder Back";
        private const string GapName = "RealLife Folder Gap";
        private const string BackKey = "reallife_phone_folder_back";   // "Back", under the folder's name on the Back tile
        private const string FolderIconName = "AppIcon-PhoneFolder";
        private const string BackIconName = "AppIcon-PhoneBack";
        private const float IconPixels = 160f;   // the game's app icons are 160 by 160, the tile 112 of it (AppIconPainter)
        private const float TilePixels = 112f;
        private const float TileLift = 4f;       // pixels the tile sits above the picture's middle, its shadow below
        private const float MiniInset = 12f;     // pixels between the tile's edge and its small pictures
        private const float MiniFill = 0.84f;    // of a small picture's cell: its tile; the rest the gap to the next

        private static readonly Color32 FolderTop = new Color32(138, 150, 170, 255);    // a folder's tile, upper left: a slate under the apps' strong colours
        private static readonly Color32 FolderBottom = new Color32(76, 86, 106, 255);   // ...lower right

        private sealed class Folder
        {
            public Transform Tile;
            public Image Picture;
            public global::UI.Elements.Badge Badge;
            public int BadgeShown = -1;
            public readonly Image[] Minis = new Image[PhoneLayoutRules.MiniMost];
            public readonly Sprite[] MiniShown = new Sprite[PhoneLayoutRules.MiniMost];
        }

        private readonly PhoneGridPager _pager;
        private readonly Transform _template;
        private readonly Folder[] _folders = new Folder[PhoneLayoutRules.FolderCount];
        private readonly List<Transform> _gaps = new List<Transform>(PagerRules.PhoneTiles);
        private TextLocalizationComponent _backTitle;
        private global::UI.Elements.Badge _backBadge;
        private int _backBadgeShown = -1;
        private int _backFolder = -1;

        /// <summary>The Back tile, at the head of each page of a folder.</summary>
        public Transform BackTile { get; private set; }

        private PhoneFolders(PhoneGridPager pager, Transform template)
        {
            _pager = pager;
            _template = template;
        }

        /// <summary>Every tile of a grid's folders, or null (one log line) if the game's app button cannot be copied: the grid keeps plain pages.</summary>
        public static PhoneFolders Make(PhoneGridPager pager, Transform template, bool strip)
        {
            if (pager == null || template == null)
            {
                return null;
            }
            var made = new PhoneFolders(pager, template);
            try
            {
                if (made.MakeAll())
                {
                    return made;
                }
            }
            catch (Exception e)
            {
                PhoneGridPager.Logger?.Error(e);
            }
            made.Destroy();
            UiClone.Fallback("the phone's app button could not be copied for the folders, so " + (strip ? "the full menu's row" : "the HUD phone") + " keeps its plain pages");
            return null;
        }

        private bool MakeAll()
        {
            Sprite folderIcon = null;
            for (int f = 0; f < PhoneLayoutRules.FolderCount; f++)
            {
                int folder = f;
                Transform tile = Copy(FolderPrefix + PhoneLayoutRules.FolderIds[f], () => _pager.OpenFolder(folder), out Image picture, out TextLocalizationComponent title,
                                      out global::UI.Elements.Badge badge);
                if (tile == null)
                {
                    return false;
                }
                var made = new Folder { Tile = tile, Picture = picture, Badge = badge };
                _folders[f] = made;
                UiClone.Key(title, PhoneLayoutRules.FolderKeys[f]);
                if (picture != null)
                {
                    folderIcon = folderIcon ?? AppIconPainter.Paint(Like(picture.sprite), null, FolderTop, FolderBottom, PhoneGridPager.GlyphShare, FolderIconName);
                    if (folderIcon != null)
                    {
                        picture.sprite = folderIcon;
                    }
                    Minis(made);
                }
                ShowBadge(badge, 0);
            }
            BackTile = Copy(BackName, _pager.CloseFolder, out Image back, out TextLocalizationComponent backTitle, out _backBadge);
            if (BackTile == null)
            {
                return false;
            }
            _backTitle = backTitle;
            if (back != null)
            {
                Sprite glyph = GameSprites.Get(PhoneGridPager.MoreGlyph) ?? PhoneGridPager.DrawnGlyph();
                Sprite icon = AppIconPainter.Paint(Like(back.sprite), glyph, PhoneGridPager.MoreTop, PhoneGridPager.MoreBottom, PhoneGridPager.GlyphShare, BackIconName,
                                                   PhoneGridPager.MoreInk, true);
                if (icon != null)
                {
                    back.sprite = icon;
                }
            }
            if (backTitle != null)
            {
                TextLocalizationComponent line = PhoneGridPager.PageLine((RectTransform)BackTile, backTitle, back);
                UiClone.Key(line, BackKey);
            }
            ShowBadge(_backBadge, 0);
            return true;
        }

        // The game's icon to paint a tile on, as the mod's apps are painted (Persona's); else the template's.
        private Sprite Like(Sprite fallback)
        {
            Transform grid = _pager.transform;
            Sprite any = null;
            for (int i = 0; i < grid.childCount; i++)
            {
                Transform child = grid.GetChild(i);
                if (!Enum.TryParse(child.name, out AppName app))
                {
                    continue;
                }
                Image picture = PhoneGridPager.Picture(child);
                Sprite sprite = picture != null ? picture.sprite : null;
                if (sprite != null && app == AppName.Persona)
                {
                    return sprite;
                }
                any = any != null ? any : sprite;
            }
            return any != null ? any : fallback;
        }

        // A copy of the phone's app button, made asleep: without the scripts that make it an app (hover, sound and badge
        // kept) and the open app's dot, with the click and a PhoneTile; then in the grid, off.
        private Transform Copy(string name, Action click, out Image picture, out TextLocalizationComponent title, out global::UI.Elements.Badge badge)
        {
            picture = null;
            title = null;
            badge = null;
            Transform grid = _pager.transform;
            Transform dormant = UiClone.Dormant(grid.parent != null ? grid.parent : grid, name + " (making)");
            GameObject copy = UiClone.Asleep(_template.gameObject, dormant, name);
            if (copy == null)
            {
                UnityEngine.Object.Destroy(dormant.gameObject);
                return null;
            }
            picture = PhoneGridPager.Picture(copy.transform);   // through the game's own field, before its script is taken off
            title = copy.GetComponentInChildren<TextLocalizationComponent>(true);
            UiClone.StripGameScripts(copy, PhoneGridPager.KeptOnMore);
            Unmark(copy);
            Transform selected = copy.transform.Find("SelectedIcon");
            if (selected != null)
            {
                UnityEngine.Object.DestroyImmediate(selected.gameObject);
            }
            if (UiClone.Rewire(copy.transform, click) == null)
            {
                UnityEngine.Object.Destroy(dormant.gameObject);
                return null;
            }
            badge = copy.GetComponentInChildren<global::UI.Elements.Badge>(true);
            copy.AddComponent<PhoneTile>();
            copy.SetActive(false);
            copy.transform.SetParent(grid, false);
            UnityEngine.Object.Destroy(dormant.gameObject);
            return copy.transform;
        }

        // What a pager of the mod's puts on a button, off a copy of a live one: no page mark, right click or wheel.
        private static void Unmark(GameObject copy)
        {
            foreach (MonoBehaviour script in copy.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (script is PageMark || script is PhoneAppPress || script is MoreAppsWheel || script is PhoneTile)
                {
                    UnityEngine.Object.DestroyImmediate(script);
                }
            }
        }

        // Three rows of three over the tile, made once and off until there is an app for them.
        private static void Minis(Folder folder)
        {
            RectTransform under = folder.Picture.rectTransform;
            float size = Mathf.Min(under.rect.width, under.rect.height);
            float scale = (size >= 1f ? size : IconPixels) / IconPixels;
            float tile = TilePixels * scale, inset = MiniInset * scale;
            float cell = (tile - 2f * inset) / PhoneLayoutRules.MiniRow;
            float each = cell * MiniFill * IconPixels / TilePixels;   // a small picture is an app's whole icon: its tile is 112 of its 160
            for (int k = 0; k < PhoneLayoutRules.MiniMost; k++)
            {
                var mini = new GameObject("Mini" + k, typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)mini.transform;
                rect.SetParent(under, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);   // from the picture's middle, whatever its pivot
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(each, each);
                int column = k % PhoneLayoutRules.MiniRow, row = k / PhoneLayoutRules.MiniRow;
                rect.anchoredPosition = new Vector2(-tile * 0.5f + inset + cell * (column + 0.5f), tile * 0.5f - inset - cell * (row + 0.5f) + TileLift * scale);
                var image = mini.GetComponent<Image>();
                image.raycastTarget = false;   // the click is the tile's
                image.preserveAspect = true;
                mini.SetActive(false);
                folder.Minis[k] = image;
            }
        }

        public Transform Tile(int folder)
        {
            return folder >= 0 && folder < _folders.Length && _folders[folder] != null ? _folders[folder].Tile : null;
        }

        public void Show(int folder, bool on)
        {
            Transform tile = Tile(folder);
            if (tile != null && tile.gameObject.activeSelf != on)
            {
                tile.gameObject.SetActive(on);
            }
        }

        /// <summary>Each folder's small pictures: the icons of its first nine apps, written only where one changed.</summary>
        public void Fill(List<Transform> apps, int[] kind, int[] place, int count)
        {
            for (int f = 0; f < PhoneLayoutRules.FolderCount; f++)
            {
                Folder folder = _folders[f];
                if (folder == null || folder.Minis[0] == null)
                {
                    continue;
                }
                int k = 0;
                for (int i = 0; i < count && k < PhoneLayoutRules.MiniMost; i++)
                {
                    if (PhoneLayoutRules.FolderOf(kind[i], place[i]) != f)
                    {
                        continue;
                    }
                    Image icon = PhoneGridPager.Picture(apps[i]);
                    Sprite sprite = icon != null ? icon.sprite : null;
                    if (sprite == null)
                    {
                        continue;
                    }
                    Mini(folder, k++, sprite);
                }
                for (; k < PhoneLayoutRules.MiniMost; k++)
                {
                    Mini(folder, k, null);
                }
            }
        }

        private static void Mini(Folder folder, int k, Sprite sprite)
        {
            Image image = folder.Minis[k];
            if (image == null || folder.MiniShown[k] == sprite && image.gameObject.activeSelf == (sprite != null))
            {
                return;
            }
            folder.MiniShown[k] = sprite;
            image.sprite = sprite;
            image.gameObject.SetActive(sprite != null);
        }

        /// <summary>The Back tile on a folder's page (<paramref name="open"/> at 0 or more), with that folder's name; off on the home screen.</summary>
        public void Back(int open)
        {
            if (BackTile == null)
            {
                return;
            }
            bool on = open >= 0 && open < PhoneLayoutRules.FolderCount;
            if (on && open != _backFolder && _backTitle != null)
            {
                _backFolder = open;
                UiClone.Key(_backTitle, PhoneLayoutRules.FolderKeys[open]);
            }
            if (BackTile.gameObject.activeSelf != on)
            {
                BackTile.gameObject.SetActive(on);
            }
        }

        /// <summary>The first <paramref name="count"/> empty tiles on, the rest off: made as they are first needed.</summary>
        public void ShowGaps(int count)
        {
            while (_gaps.Count < count)
            {
                var gap = new GameObject(GapName, typeof(RectTransform));   // no picture, nothing to click: a tile the layout keeps empty
                gap.AddComponent<PhoneTile>();
                gap.SetActive(false);
                gap.transform.SetParent(_pager.transform, false);
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

        /// <summary>An empty tile, made by <see cref="ShowGaps"/>.</summary>
        public Transform Gap(int index)
        {
            return index >= 0 && index < _gaps.Count ? _gaps[index] : null;
        }

        /// <summary>A folder tile's number, its apps' added up: written only when it changes.</summary>
        public void Badge(int folder, int count)
        {
            Folder made = folder >= 0 && folder < _folders.Length ? _folders[folder] : null;
            if (made == null || made.Badge == null || made.BadgeShown == count)
            {
                return;
            }
            made.BadgeShown = count;
            ShowBadge(made.Badge, count);
        }

        /// <summary>The number on the Back tile: the buttons outside the folder with their badge up.</summary>
        public void BackBadge(int count)
        {
            if (_backBadge == null || _backBadgeShown == count)
            {
                return;
            }
            _backBadgeShown = count;
            ShowBadge(_backBadge, count);
        }

        private static void ShowBadge(global::UI.Elements.Badge badge, int count)
        {
            if (badge == null)
            {
                return;
            }
            try
            {
                UpdateBadge(badge, count);
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the badges on the phone's folders");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UpdateBadge(global::UI.Elements.Badge badge, int count)
        {
            badge.UpdateBadge(count);
        }

        /// <summary>Every tile taken away. The city's own unloading takes them with the interface.</summary>
        public void Destroy()
        {
            for (int f = 0; f < _folders.Length; f++)
            {
                Retire(_folders[f] != null ? _folders[f].Tile : null);
                _folders[f] = null;
            }
            Retire(BackTile);
            BackTile = null;
            for (int g = 0; g < _gaps.Count; g++)
            {
                Retire(_gaps[g]);
            }
            _gaps.Clear();
        }

        // Out of the grid at once, then destroyed: the pager counts what is left at once (a destroyed object stays a child until the frame ends).
        private static void Retire(Transform tile)
        {
            if (tile == null)
            {
                return;
            }
            if (GameManager.isCitySceneBeingUnloaded)
            {
                UnityEngine.Object.Destroy(tile.gameObject);
                return;
            }
            tile.gameObject.SetActive(false);
            tile.SetParent(null, false);
            UnityEngine.Object.Destroy(tile.gameObject);
        }
    }

    /// <summary>On each tile a pager makes for its folders (a folder's, the Back tile, an empty one): no pager counts it as an app. Costs nothing a frame.</summary>
    public sealed class PhoneTile : MonoBehaviour
    {
    }
}
