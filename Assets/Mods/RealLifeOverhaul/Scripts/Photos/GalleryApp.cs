using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BAModAPI;
using Localizor;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// The Gallery app on the phone: the save's photos, newest first, in the game's frame (GameUi.AppShell):
    ///
    ///   the tabs        All photos, Selfies
    ///   the right side  a grid of five by four over the game's own list background, each photo's small copy (its middle,
    ///                   16 by 9, a pale frame round it, the one chosen framed in blue, "Selfie" and "Wallpaper" marked in a
    ///                   corner); a click chooses a photo, a second click shows it whole. Shown whole, the photo fills the
    ///                   right side on black, its shape kept, with when it was taken under it; a click goes back to the grid
    ///   the white box   nothing chosen: the title, the photos against the most a save keeps, the page (the game's own
    ///                   previous and next arrows, "Page 2 of 4"), a note, Open the camera; a photo chosen: Photo or Selfie,
    ///                   when it was taken (the game's day and hour, and the real date), whether it is the wallpaper, the
    ///                   photo's place among the tab's (previous and next), View (or Back to the photos), Delete, Close;
    ///                   Share to Flare (while Flare runs), Set as wallpaper (or Use the phone's own wallpaper); the
    ///                   Guide's page
    ///
    /// A page's small copies are read on a worker thread (some 100 KB each), made textures on the main thread (one upload
    /// each) and kept while the app is open, two pages' worth at most; the whole photo is read and decoded off the main
    /// thread (PhotosGame.Load) only when it is shown whole, and let go when it is not. Closing the app lets every texture go.
    /// Deleting asks first with the game's question box. Setting the wallpaper needs the phone's modern look (Phone/PhoneSkin);
    /// without it the Gallery says so and sets nothing.
    ///
    /// PER FRAME: NOTHING. The page is written on a click, when a photo is added or deleted, and when its pictures come.
    /// </summary>
    internal sealed class GalleryApp
    {
        public const string AppId = "RealLifeGallery";
        private const string GuideTitle = "reallife_meta_title_photos";
        private const int KeptMost = PhotoStoreRules.PerPage * 2;   // small copies kept as textures while the app is open
        private const float Gap = 22f;                               // canvas units between two photos of the grid

        private static readonly string[] TabKeys = { "reallife_gallery_tab_all", "reallife_gallery_tab_selfies" };
        private static readonly Color Frame = new Color(0.84f, 0.86f, 0.89f, 1f);
        private static readonly Color Chosen = new Color(0.16f, 0.45f, 0.85f, 1f);
        private static readonly Color Mark = new Color(0f, 0f, 0f, 0.6f);

        public static IModLogger Logger;

        private readonly PhotosDirector _director;
        private readonly List<PhotoEntry> _shown = new List<PhotoEntry>(PhotoStoreRules.Cap + 16);
        private readonly Dictionary<string, Texture2D> _thumbs = new Dictionary<string, Texture2D>(KeptMost);
        private readonly HashSet<string> _asked = new HashSet<string>();
        private readonly HashSet<string> _missing = new HashSet<string>();   // photos whose small copy could not be read
        private readonly List<string> _drop = new List<string>(KeptMost);
        private readonly Cell[] _cells = new Cell[PhotoStoreRules.PerPage];
        private PhoneApp _app;
        private AppShell _shell;
        private RectTransform _grid, _viewer;
        private RawImage _whole;
        private TextMeshProUGUI _wholeCaption, _empty;
        private bool _gaveUp, _writing, _viewing;
        private int _tab, _page, _opening;
        private string _picked;                 // the photo chosen, by name
        private Texture2D _wholeTexture;
        private string _wholeName;
        private float _laidWidth = -1f, _laidHeight = -1f;

        // One place of the grid: its frame (the click), the small copy, and its two marks.
        private sealed class Cell
        {
            public Image Back;
            public RawImage Picture;
            public Image MarkBack;
            public TextMeshProUGUI MarkWords, NoPicture;
            public PhotoEntry Entry;
        }

        public GalleryApp(PhotosDirector director)
        {
            _director = director;
        }

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        /// <summary>Once a second while the Gallery runs: the app among the game's.</summary>
        public void Install()
        {
            if (_app != null || _gaveUp)
            {
                return;
            }
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_gallery_app",
                Icon = like => AppIconPainter.Paint(like, FlareIcons.Get(FlareGlyph.Picture), new Color32(255, 183, 77, 255), new Color32(226, 84, 110, 255), 0.46f, "AppIcon-Gallery"),
                Build = Build,
                Opened = () => UiClone.Safe(Opened),
                Closed = Closed
            }, out bool tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Gallery: the game's phone is not built as this version expects, so the Gallery app is left off. Photos are still saved.");
                }
            }
        }

        public void Remove()
        {
            Forget();
            _app?.Remove();
            _app = null;
            _shell = null;
            _grid = _viewer = null;
            _whole = null;
            _gaveUp = false;
        }

        /// <summary>The app opens (the viewfinder's Gallery button, a test).</summary>
        public void Open()
        {
            if (_app == null || !_app.Installed)
            {
                return;
            }
            if (Showing)
            {
                Refresh();
            }
            else
            {
                _app.Open();
            }
        }

        /// <summary>A photo was added or deleted: the page is written again if it is showing.</summary>
        public void Changed()
        {
            if (Showing && !_writing)
            {
                Refresh();
            }
        }

        // ---------------------------------------------------------------- building it

        private GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("photos: the Gallery app being built from the game's MyEmployees");   // once per city
            _shell = AppShell.Make(menu, dormant, AppId);
            var list = _shell != null ? _shell.List as RectTransform : null;
            if (list == null)
            {
                UiClone.Fallback("Gallery: the game's frame is not laid out as expected");
                return null;
            }
            for (int i = 0; i < TabKeys.Length; i++)
            {
                int tab = i;
                UiClone.Key(_shell.TabLabels[i], TabKeys[i]);
                UiClone.Rewire(_shell.Tabs[i], () => UiClone.Safe(() => ShowTab(tab)));
            }
            _grid = GallerySheet.Make(list, "Grid");
            for (int i = 0; i < _cells.Length; i++)
            {
                int n = i;
                var cell = new Cell();
                cell.Back = FlareKit.Box(_grid, "Photo" + i, Frame, 14f, false, true);
                FlareKit.Click(cell.Back, () => CellClicked(n));
                cell.Picture = FlareKit.Picture(cell.Back.transform, "Small");
                FlareKit.Stretch(cell.Picture.rectTransform, 8f, 8f, 8f, 8f);
                cell.NoPicture = FlareKit.Text(cell.Back.transform, "None", 26f, new Color(0.35f, 0.39f, 0.43f, 1f), FontStyles.Normal, TextAlignmentOptions.Center);
                FlareKit.Stretch(cell.NoPicture.rectTransform, 10f, 10f, 10f, 10f);
                cell.NoPicture.text = PhotosText.T("reallife_gallery_no_preview");
                cell.MarkBack = FlareKit.Box(cell.Back.transform, "Mark", Mark, 10f);
                cell.MarkWords = FlareKit.Text(cell.MarkBack.transform, "Words", 24f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
                FlareKit.Stretch(cell.MarkWords.rectTransform, 8f, 0f, 8f, 0f);
                cell.MarkWords.textWrappingMode = TextWrappingModes.NoWrap;
                _cells[i] = cell;
            }
            _empty = FlareKit.Text(_grid, "Empty", 34f, new Color(0.35f, 0.39f, 0.43f, 1f), FontStyles.Normal, TextAlignmentOptions.Center);

            _viewer = GallerySheet.Make(list, "Whole");
            var black = _viewer.GetComponent<Image>();
            if (black != null)
            {
                black.color = Color.black;
            }
            FlareKit.Click(black, () => View(false));
            _whole = FlareKit.Picture(_viewer, "Photo");
            _wholeCaption = FlareKit.Text(_viewer, "Caption", 30f, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            _viewer.gameObject.SetActive(false);
            return _shell.Root;
        }

        // ---------------------------------------------------------------- opening and closing

        private void Opened()
        {
            if (_shell == null)
            {
                return;
            }
            _opening++;
            _viewing = false;
            Refresh();
        }

        private void Closed()
        {
            Forget();
            _viewing = false;
            _picked = null;
        }

        // Every texture let go (the small copies and the whole photo): reopening reads them again.
        private void Forget()
        {
            _opening++;
            foreach (KeyValuePair<string, Texture2D> kept in _thumbs)
            {
                PhotosGame.Destroy(kept.Value);
            }
            _thumbs.Clear();
            _asked.Clear();
            _missing.Clear();
            DropWhole();
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] != null && _cells[i].Picture != null)
                {
                    _cells[i].Picture.texture = null;
                }
            }
        }

        private void ShowTab(int tab)
        {
            if (tab != _tab)
            {
                _tab = tab;
                _page = 0;
                _picked = null;
                _viewing = false;
                Refresh();
            }
        }

        // ---------------------------------------------------------------- the page

        private void Refresh()
        {
            if (_shell == null || _writing)
            {
                return;
            }
            _writing = true;
            try
            {
                _shell.ShowTab(_tab);
                PhotoStoreRules.Of(_director.Photos, _tab, _shown);
                int index = Index(_picked);
                if (_picked != null && index < 0)
                {
                    _picked = null;   // deleted meanwhile
                    _viewing = false;
                }
                if (index >= 0)
                {
                    _page = PhotoStoreRules.PageOf(index);
                }
                _page = PhotoStoreRules.ClampPage(_page, _shown.Count);
                UiClone.Words(_shell.Header, PhotosText.Fill("reallife_gallery_headline", "count", PhotosText.N(_director.Count), "cap", PhotosText.N(PhotoStoreRules.Cap)));
                Lay();
                WriteGrid();
                WriteWhole(index);
                InfoBox box = _shell.Box;
                box.Clear();
                if (index >= 0)
                {
                    WritePhoto(box, _shown[index], index);
                }
                else
                {
                    WriteFront(box);
                }
                Meta.Guide.Point(box, GuideTitle);
                box.Done();
            }
            finally
            {
                _writing = false;
            }
        }

        private int Index(string name)
        {
            if (name == null)
            {
                return -1;
            }
            for (int i = 0; i < _shown.Count; i++)
            {
                if (_shown[i].Name == name)
                {
                    return i;
                }
            }
            return -1;
        }

        // Nothing chosen: the count, the page, a note, the camera.
        private void WriteFront(InfoBox box)
        {
            box.Title(PhotosText.T("reallife_gallery_app"));
            box.Row("reallife_gallery_row_count", PhotosText.Fill("reallife_gallery_count_of", "count", PhotosText.N(_director.Count), "cap", PhotosText.N(PhotoStoreRules.Cap)));
            if (_tab == PhotoStoreRules.TabSelfies)
            {
                box.Row("reallife_gallery_row_selfies", PhotosText.N(_shown.Count));
            }
            Pages(box);
            if (PhotoStoreRules.Full(_director.Count))
            {
                box.Line(PhotosText.Fill("reallife_gallery_full_line", "cap", PhotosText.N(PhotoStoreRules.Cap)), CellTone.Yellow);
            }
            box.Note(PhotosText.T(_shown.Count == 0 ? (_tab == PhotoStoreRules.TabSelfies ? "reallife_gallery_empty_selfies" : "reallife_gallery_empty") : "reallife_gallery_pick"));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_gallery_button_camera", () => UiClone.Safe(() => _director.OpenCamera(false, false)));
        }

        private void Pages(InfoBox box)
        {
            int pages = PhotoStoreRules.Pages(_shown.Count);
            if (pages > 1)
            {
                box.Selector(pages, _page, p => PhotosText.Fill("reallife_gallery_page", "page", PhotosText.N(p + 1), "pages", PhotosText.N(pages)), p => UiClone.Safe(() =>
                {
                    _page = p;
                    _picked = null;
                    _viewing = false;
                    Refresh();
                }));
            }
        }

        // A photo chosen: when, whether it is the wallpaper, its place, and what can be done with it.
        private void WritePhoto(InfoBox box, PhotoEntry photo, int index)
        {
            string name = photo.Name;
            bool wallpaper = name == _director.WallpaperName;
            box.Title(PhotosText.T(photo.Selfie ? "reallife_gallery_selfie" : "reallife_gallery_photo"));
            box.Row("reallife_gallery_row_taken", PhotosText.Taken(photo));
            if (wallpaper)
            {
                box.Line(PhotosText.T("reallife_gallery_is_wallpaper"), CellTone.Green);
            }
            box.Selector(_shown.Count, index, i => PhotosText.Fill("reallife_gallery_place", "place", PhotosText.N(i + 1), "count", PhotosText.N(_shown.Count)), i => UiClone.Safe(() =>
            {
                if (i >= 0 && i < _shown.Count)
                {
                    _picked = _shown[i].Name;
                    Refresh();
                }
            }));
            Transform first = box.ButtonRow();
            InfoBox.Button(first, InfoBox.Positive, _viewing ? "reallife_gallery_button_back" : "reallife_gallery_button_view", () => UiClone.Safe(() => View(!_viewing)));
            InfoBox.Button(first, InfoBox.Negative, "reallife_gallery_button_delete", () => UiClone.Safe(() => AskDelete(photo)));
            InfoBox.Button(first, InfoBox.Additional, "reallife_gallery_button_close", () => UiClone.Safe(() =>
            {
                _picked = null;
                _viewing = false;
                Refresh();
            }));
            Transform second = box.ButtonRow();
            if (PhotosBridges.FlareRunning != null && PhotosBridges.FlareRunning())
            {
                InfoBox.Button(second, InfoBox.Positive, "reallife_gallery_button_flare", () => UiClone.Safe(() => Share(name)));
            }
            InfoBox.Button(second, InfoBox.Additional, wallpaper ? "reallife_gallery_button_own_wallpaper" : "reallife_gallery_button_wallpaper", () => UiClone.Safe(() => Wallpaper(wallpaper ? null : name)));
            box.Note(PhotosText.T(PhotosDirector.CanSetWallpaper ? "reallife_gallery_note_photo" : "reallife_gallery_note_photo_no_wallpaper"));
        }

        // ---------------------------------------------------------------- the grid

        // The grid placed for the right side's size now: five by four, each 16 by 9 inside its frame.
        private void Lay()
        {
            float w = _grid.rect.width, h = _grid.rect.height;
            if (w < 100f)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_shell.Root.transform);
                w = _grid.rect.width;
                h = _grid.rect.height;
            }
            if (w < 100f || (Mathf.Abs(w - _laidWidth) < 1f && Mathf.Abs(h - _laidHeight) < 1f))
            {
                return;
            }
            _laidWidth = w;
            _laidHeight = h;
            int cols = PhotoStoreRules.Columns, rows = PhotoStoreRules.Rows;
            float cw = (w - Gap * (cols + 1)) / cols;
            float ch = cw * 9f / 16f;
            if (ch * rows + Gap * (rows + 1) > h)
            {
                ch = (h - Gap * (rows + 1)) / rows;
                cw = ch * 16f / 9f;
            }
            float x0 = (w - (cw * cols + Gap * (cols - 1))) / 2f, y0 = Gap;
            for (int i = 0; i < _cells.Length; i++)
            {
                int c = i % cols, r = i / cols;
                FlareKit.Place(_cells[i].Back.rectTransform, x0 + c * (cw + Gap), y0 + r * (ch + Gap), cw, ch);
                FlareKit.Place(_cells[i].MarkBack.rectTransform, 14f, ch - 52f, Mathf.Min(cw - 28f, 190f), 40f);
            }
            FlareKit.Place(_empty.rectTransform, 60f, h / 2f - 60f, w - 120f, 120f);
        }

        private void WriteGrid()
        {
            int first = PhotoStoreRules.PageRange(_page, _shown.Count, out int count);
            FlareKit.Show(_empty, _shown.Count == 0);
            if (_shown.Count == 0)
            {
                FlareKit.Set(_empty, PhotosText.T(_tab == PhotoStoreRules.TabSelfies ? "reallife_gallery_empty_selfies" : "reallife_gallery_empty"));
            }
            List<string> wanted = null;
            for (int i = 0; i < _cells.Length; i++)
            {
                Cell cell = _cells[i];
                bool on = i < count;
                FlareKit.Show(cell.Back, on);
                cell.Entry = on ? _shown[first + i] : null;
                if (!on)
                {
                    continue;
                }
                PhotoEntry photo = cell.Entry;
                cell.Back.color = photo.Name == _picked ? Chosen : Frame;
                bool wallpaper = photo.Name == _director.WallpaperName;
                FlareKit.Show(cell.MarkBack, photo.Selfie || wallpaper);
                if (photo.Selfie || wallpaper)
                {
                    FlareKit.Set(cell.MarkWords, PhotosText.T(wallpaper ? "reallife_gallery_mark_wallpaper" : "reallife_gallery_mark_selfie"));
                }
                _thumbs.TryGetValue(photo.Name, out Texture2D small);
                Show(cell, small);
                if (small == null && _asked.Add(photo.Name))
                {
                    (wanted ?? (wanted = new List<string>(PhotoStoreRules.PerPage))).Add(photo.Name);
                }
            }
            if (wanted != null)
            {
                Trim(first, count);
                _director.StartCoroutine(Guard.Routine(ReadSmall(wanted, _director.Folder, _opening)));
            }
        }

        private void Show(Cell cell, Texture2D small)
        {
            cell.Picture.texture = small;
            FlareKit.Show(cell.Picture, small != null);
            FlareKit.Show(cell.NoPicture, small == null && _missing.Contains(cell.Entry.Name));
            if (small != null && small.height > 0)
            {
                // the middle of the picture at 16 by 9, as the frame is
                float shape = (float)small.width / small.height, want = 16f / 9f;
                cell.Picture.uvRect = shape > want ? new Rect((1f - want / shape) / 2f, 0f, want / shape, 1f) : new Rect(0f, (1f - shape / want) / 2f, 1f, shape / want);
            }
        }

        // Small copies kept beyond two pages' worth, other than this page's, let go before more are read.
        private void Trim(int first, int count)
        {
            if (_thumbs.Count + PhotoStoreRules.PerPage <= KeptMost)
            {
                return;
            }
            _drop.Clear();
            foreach (KeyValuePair<string, Texture2D> kept in _thumbs)
            {
                bool onPage = false;
                for (int i = first; i < first + count && !onPage; i++)
                {
                    onPage = _shown[i].Name == kept.Key;
                }
                if (!onPage)
                {
                    _drop.Add(kept.Key);
                }
            }
            for (int i = 0; i < _drop.Count; i++)
            {
                PhotosGame.Destroy(_thumbs[_drop[i]]);
                _thumbs.Remove(_drop[i]);
                _asked.Remove(_drop[i]);
            }
        }

        // A page's small copies read on a worker (each some 100 KB), then made textures here, one upload each.
        private IEnumerator ReadSmall(List<string> names, string folder, int opening)
        {
            var rows = new byte[names.Count][];
            var widths = new int[names.Count];
            var heights = new int[names.Count];
            Task work = Task.Run(() =>
            {
                for (int i = 0; i < names.Count; i++)
                {
                    rows[i] = PhotoThumbs.Read(PhotoThumbs.FileOf(Path.Combine(folder, names[i])), out widths[i], out heights[i]);
                }
            });
            while (!work.IsCompleted)
            {
                yield return null;
            }
            if (opening != _opening || !Showing)
            {
                yield break;   // closed, or reopened, meanwhile
            }
            int missing = 0;
            for (int i = 0; i < names.Count; i++)
            {
                Texture2D made = PhotosGame.SmallTexture(rows[i], widths[i], heights[i]);
                if (made == null)
                {
                    missing++;
                    _missing.Add(names[i]);
                    continue;
                }
                if (_thumbs.TryGetValue(names[i], out Texture2D old))
                {
                    PhotosGame.Destroy(old);
                }
                _thumbs[names[i]] = made;
            }
            for (int i = 0; i < _cells.Length; i++)
            {
                Cell cell = _cells[i];
                if (cell.Entry != null && cell.Back.gameObject.activeSelf)
                {
                    _thumbs.TryGetValue(cell.Entry.Name, out Texture2D small);
                    Show(cell, small);
                }
            }
            if (missing > 0)
            {
                Logger?.Info("Gallery: " + missing + " photo(s) of this page have no small copy (their Thumbs file was taken away); they show without one.");
            }
        }

        private void CellClicked(int n)
        {
            Cell cell = n >= 0 && n < _cells.Length ? _cells[n] : null;
            if (cell == null || cell.Entry == null)
            {
                return;
            }
            if (cell.Entry.Name == _picked)
            {
                View(true);
                return;
            }
            _picked = cell.Entry.Name;
            _viewing = false;
            Refresh();
        }

        // ---------------------------------------------------------------- the whole photo

        private void View(bool on)
        {
            _viewing = on && _picked != null;
            Refresh();
        }

        private void WriteWhole(int index)
        {
            bool on = _viewing && index >= 0;
            FlareKit.Show(_viewer, on);
            if (!on)
            {
                DropWhole();
                return;
            }
            PhotoEntry photo = _shown[index];
            FlareKit.Set(_wholeCaption, PhotosText.Taken(photo));
            if (_wholeName != photo.Name)
            {
                DropWhole();
                _wholeName = photo.Name;
                _whole.texture = null;
                FlareKit.Show(_whole, false);
                FlareKit.Set(_wholeCaption, PhotosText.T("reallife_gallery_loading"));
                string name = photo.Name;
                int opening = _opening;
                _director.StartCoroutine(Guard.Routine(PhotosGame.Load(Path.Combine(_director.Folder, name), t => Came(t, name, opening))));
            }
            else
            {
                PlaceWhole();
            }
        }

        private void Came(Texture2D texture, string name, int opening)
        {
            if (opening != _opening || name != _wholeName || !_viewing || !Showing)
            {
                PhotosGame.Destroy(texture);
                return;
            }
            _wholeTexture = texture;
            _whole.texture = texture;
            FlareKit.Show(_whole, texture != null);
            PhotoEntry photo = Index(name) >= 0 ? _shown[Index(name)] : null;
            FlareKit.Set(_wholeCaption, texture == null ? PhotosText.T("reallife_gallery_unreadable") : photo != null ? PhotosText.Taken(photo) : string.Empty);
            PlaceWhole();
        }

        // The photo as large as the right side has room for, its shape kept, its caption under it.
        private void PlaceWhole()
        {
            float w = _viewer.rect.width, h = _viewer.rect.height;
            Texture t = _whole.texture;
            if (t == null || w < 100f || h < 100f)
            {
                FlareKit.Place(_wholeCaption.rectTransform, 40f, h / 2f - 30f, Mathf.Max(0f, w - 80f), 60f);
                return;
            }
            float shape = (float)t.width / Mathf.Max(1, t.height);
            float pw = Mathf.Min(w - 60f, (h - 110f) * shape), ph = pw / shape;
            FlareKit.Place(_whole.rectTransform, (w - pw) / 2f, 30f, pw, ph);
            FlareKit.Place(_wholeCaption.rectTransform, 40f, 40f + ph, w - 80f, 60f);
        }

        private void DropWhole()
        {
            PhotosGame.Destroy(_wholeTexture);
            _wholeTexture = null;
            _wholeName = null;
            if (_whole != null)
            {
                _whole.texture = null;
            }
        }

        // ---------------------------------------------------------------- what can be done with a photo

        private void AskDelete(PhotoEntry photo)
        {
            string name = photo.Name;
            GameConfirm.Show("reallife_gallery_delete_title".Localize(), "reallife_gallery_delete_body".Localize(new { taken = PhotosText.Taken(photo) }),
                () => UiClone.Safe(() =>
                {
                    if (_picked == name)
                    {
                        _picked = null;
                        _viewing = false;
                    }
                    _director.StartCoroutine(Guard.Routine(_director.Delete(new List<string>(1) { name }, _director.Generation)));
                }), null, "reallife_gallery_delete_yes", null, true, true);
        }

        private void Share(string name)
        {
            string path = Path.Combine(_director.Folder, name);
            if (PhotosBridges.ShareToFlare == null || !PhotosBridges.ShareToFlare(path, PhotoThumbs.FileOf(path)))
            {
                PhotosGame.Tell(NotificationType.Warning, "reallife_gallery_note_flare_off");
            }
        }

        private void Wallpaper(string name)
        {
            if (name != null && !PhotosDirector.CanSetWallpaper)
            {
                PhotosGame.Tell(NotificationType.Warning, "reallife_gallery_note_no_wallpaper");
                return;
            }
            _director.SetWallpaper(name);
            PhotosGame.Tell(NotificationType.Success, name != null ? "reallife_gallery_note_wallpaper" : "reallife_gallery_note_wallpaper_own");
            Refresh();
        }
    }
}
