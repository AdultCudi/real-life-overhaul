using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using RealLifeOverhaul.Flare;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// The Gallery's grid: a scrolling column of rows (GalleryRules.Layout: a day's heading, strips of square photos, the
    /// count at the foot) of which only those in view and half a screen either side are built, from pools, and placed where
    /// the rules put them; and the photos' small copies, read a few at a time on a worker and made textures within
    /// <see cref="GalleryRules.UploadBudgetMs"/> of a frame, kept while the app is open (those far out of view let go past
    /// <see cref="GalleryRules.KeptMost"/>).
    ///
    /// COST: nothing while it stands still. While it scrolls, one look a frame at which rows are in view (a binary search),
    /// and a row bound only as it comes into view. While small copies come in, a millisecond a frame at most.
    /// </summary>
    internal sealed partial class GalleryApp
    {
        private readonly List<GalleryRow> _rows = new List<GalleryRow>(PhotoStoreRules.Cap + 64);
        private readonly List<StripCell> _liveStrips = new List<StripCell>(24);
        private readonly Stack<StripCell> _freeStrips = new Stack<StripCell>(24);
        private readonly List<DayCell> _liveDays = new List<DayCell>(8);
        private readonly Stack<DayCell> _freeDays = new Stack<DayCell>(8);
        private readonly List<StripCell> _allStrips = new List<StripCell>(24);
        private readonly List<DayCell> _allDays = new List<DayCell>(8);
        private readonly Dictionary<string, Texture2D> _thumbs = new Dictionary<string, Texture2D>(GalleryRules.KeptMost + GalleryRules.ReadAtOnce);
        private readonly HashSet<string> _missing = new HashSet<string>();   // photos whose small copy could not be read
        private readonly HashSet<string> _loading = new HashSet<string>();   // ...being read now
        private readonly HashSet<string> _keep = new HashSet<string>();      // ...built now (in view, or in the viewer's strip)
        private readonly List<string> _want = new List<string>(64);           // ...to be read, in the order they were seen
        private readonly List<string> _drop = new List<string>(64);
        private RectTransform _list, _viewport, _content, _foot;
        private ScrollRect _scroll;
        private FlareGlide _glide;
        private TextMeshProUGUI _footCount, _footStorage;
        private int _columns = GalleryRules.ColumnsWide;
        private float _gridWidth, _cell, _total;
        private bool _filling, _footLive;
        private int _readingFor = -1;          // the opening a reader of small copies is at work for, or -1

        // A strip of photos: up to four squares.
        private sealed class StripCell
        {
            public RectTransform Rect;
            public int Row = -1;
            public readonly Slot[] Slots = new Slot[GalleryRules.ColumnsWide];
        }

        // One square of a strip: the photo's small copy over a placeholder, and its two marks (favourite, wallpaper).
        private sealed class Slot
        {
            public Image Back, FavShade, Fav, WallShade, Wall;
            public RawImage Picture;
            public string Name;
            public int Index = -1;
        }

        // A day's heading: its name and its count.
        private sealed class DayCell
        {
            public RectTransform Rect;
            public int Row = -1;
            public TextMeshProUGUI Label, Count;
        }

        // ---------------------------------------------------------------- building

        private void BuildGrid()
        {
            _list = FlareKit.Rect(_library, "Grid");
            var catcher = _list.gameObject.AddComponent<Image>();
            catcher.color = GalleryLook.Clear;   // takes the wheel and the drag everywhere over the grid
            _viewport = FlareKit.Rect(_list, "Viewport");
            FlareKit.Stretch(_viewport, 0f, 0f, 0f, 0f);
            _viewport.gameObject.AddComponent<RectMask2D>();
            _content = FlareKit.Rect(_viewport, "Content");
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = new Vector2(0f, 10f);
            _scroll = _list.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = _viewport;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Elastic;
            _scroll.elasticity = 0.1f;
            _scroll.inertia = true;
            _scroll.decelerationRate = 0.135f;
            _scroll.scrollSensitivity = 0f;   // the wheel is the glide's
            _scroll.onValueChanged.AddListener(Guard.Listener<Vector2>(Scrolled));
            _glide = _list.gameObject.AddComponent<FlareGlide>();
            _glide.Scroll = _scroll;
            _glide.enabled = false;
            _list.gameObject.AddComponent<FlareWheel>().Glide = _glide;

            _foot = FlareKit.Rect(_content, "Foot");
            _footCount = FlareKit.Text(_foot, "Count", 30f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            _footStorage = FlareKit.Text(_foot, "Storage", 26f, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            FlareKit.Show(_foot, false);
        }

        private StripCell MakeStrip()
        {
            var cell = new StripCell { Rect = FlareKit.Rect(_content, "Strip") };
            for (int j = 0; j < cell.Slots.Length; j++)
            {
                var slot = new Slot();
                slot.Back = FlareKit.Box(cell.Rect, "Photo" + j, Color.white, 0f, false, true);
                FlareKit.Click(slot.Back, () => Tapped(slot));
                slot.Picture = FlareKit.Picture(slot.Back.transform, "Small");
                FlareKit.Stretch(slot.Picture.rectTransform, 0f, 0f, 0f, 0f);
                slot.FavShade = FlareKit.Box(slot.Back.transform, "FavouriteShade", Color.white, 0f, true);
                slot.Fav = FlareKit.Box(slot.FavShade.transform, "Favourite", Color.white);
                slot.Fav.sprite = FlareIcons.Get(FlareGlyph.HeartFull);
                FlareKit.Place(slot.Fav.rectTransform, 10f, 11f, 36f, 36f);
                slot.WallShade = FlareKit.Box(slot.Back.transform, "WallpaperShade", Color.white, 0f, true);
                slot.Wall = FlareKit.Box(slot.WallShade.transform, "Wallpaper", Color.white);
                slot.Wall.sprite = FlareIcons.Get(FlareGlyph.Phone);
                FlareKit.Place(slot.Wall.rectTransform, 10f, 10f, 36f, 36f);
                cell.Slots[j] = slot;
            }
            _allStrips.Add(cell);
            PaintStrip(cell);
            return cell;
        }

        private DayCell MakeDay()
        {
            var cell = new DayCell { Rect = FlareKit.Rect(_content, "Day") };
            cell.Label = FlareKit.Text(cell.Rect, "Label", 38f, Color.white, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            cell.Label.textWrappingMode = TextWrappingModes.NoWrap;
            cell.Count = FlareKit.Text(cell.Rect, "Count", 26f, Color.white, FontStyles.Normal, TextAlignmentOptions.BottomRight);
            cell.Count.textWrappingMode = TextWrappingModes.NoWrap;
            _allDays.Add(cell);
            PaintDay(cell);
            return cell;
        }

        // ---------------------------------------------------------------- laying out and painting

        private void LayGrid(float x, float y, float width, float height)
        {
            FlareKit.Place(_list, x, y, width, height - Margin);
            _gridWidth = width;
            _columns = GalleryRules.Columns(width);
            _cell = GalleryRules.Cell(width, _columns);
            FlareKit.Place(_footCount.rectTransform, 0f, 48f, width, 44f);
            FlareKit.Place(_footStorage.rectTransform, 0f, 96f, width, 40f);
        }

        private void PaintGrid()
        {
            for (int i = 0; i < _allStrips.Count; i++)
            {
                PaintStrip(_allStrips[i]);
            }
            for (int i = 0; i < _allDays.Count; i++)
            {
                PaintDay(_allDays[i]);
            }
            _footCount.color = _look.Muted;
            _footStorage.color = _look.Muted;
        }

        private void PaintStrip(StripCell cell)
        {
            if (_look == null)
            {
                return;
            }
            for (int j = 0; j < cell.Slots.Length; j++)
            {
                Slot s = cell.Slots[j];
                s.Back.color = _look.Placeholder;
                s.FavShade.color = _look.Shade;
                s.Fav.color = Color.white;
                s.WallShade.color = _look.Shade;
                s.Wall.color = Color.white;
            }
        }

        private void PaintDay(DayCell cell)
        {
            if (_look == null)
            {
                return;
            }
            cell.Label.color = _look.Text;
            cell.Count.color = _look.Muted;
        }

        // ---------------------------------------------------------------- the rows

        // The album's rows worked out again and the cells in view bound; 'top' takes the grid back to its top, else it stays
        // where it was (as far as the new rows allow).
        private void WriteGrid(bool top)
        {
            FreeAll();
            FlareKit.Show(_list, _shown.Count > 0);
            _total = GalleryRules.Layout(_shown, _columns, _cell, _rows);
            _content.sizeDelta = new Vector2(0f, Mathf.Max(10f, _total));
            _glide.Stop();
            _scroll.StopMovement();
            float most = Mathf.Max(0f, _total - _viewport.rect.height);
            _nav.Scroll = top ? 0f : Mathf.Clamp(_nav.Scroll, 0f, most);
            _content.anchoredPosition = new Vector2(0f, _nav.Scroll);
            FlareKit.Set(_footCount, PhotosText.Count(_shown.Count));
            FlareKit.Set(_footStorage, PhotoStoreRules.Full(_director.Count)
                ? PhotosText.Fill("reallife_gallery_full_line", "cap", PhotosText.N(PhotoStoreRules.Cap))
                : PhotosText.Fill("reallife_gallery_storage", "count", PhotosText.N(_director.Count), "cap", PhotosText.N(PhotoStoreRules.Cap)));
            _footStorage.color = PhotoStoreRules.Full(_director.Count) ? _look.Accent : _look.Muted;
            Fill();
        }

        private void Scrolled(Vector2 _)
        {
            if (!_filling && _built)
            {
                _nav.Scroll = _content.anchoredPosition.y;
                Fill();
            }
        }

        // The rows in view (and half a screen either side) get a cell; the others give theirs back.
        private void Fill()
        {
            if (_filling || _gridWidth <= 0f)
            {
                return;
            }
            _filling = true;
            try
            {
                if (!GalleryRules.InView(_rows, _content.anchoredPosition.y, _viewport.rect.height, out int first, out int last))
                {
                    FreeAll();
                    return;
                }
                for (int i = _liveStrips.Count - 1; i >= 0; i--)
                {
                    if (_liveStrips[i].Row < first || _liveStrips[i].Row > last)
                    {
                        FreeStrip(i);
                    }
                }
                for (int i = _liveDays.Count - 1; i >= 0; i--)
                {
                    if (_liveDays[i].Row < first || _liveDays[i].Row > last)
                    {
                        FreeDay(i);
                    }
                }
                _footLive = false;
                int today = PhotosGame.Day;
                for (int r = first; r <= last; r++)
                {
                    GalleryRow row = _rows[r];
                    switch (row.Kind)
                    {
                        case GalleryRowKind.Strip:
                            if (!StripBound(r))
                            {
                                BindStrip(TakeStrip(), r);
                            }
                            break;
                        case GalleryRowKind.Day:
                            if (!DayBound(r))
                            {
                                BindDay(TakeDay(), r, today);
                            }
                            break;
                        default:
                            _footLive = true;
                            FlareKit.Place(_foot, 0f, row.Top, _gridWidth, row.Height);
                            break;
                    }
                }
                FlareKit.Show(_foot, _footLive);
                WantInView();
            }
            finally
            {
                _filling = false;
            }
        }

        private bool StripBound(int row)
        {
            for (int i = 0; i < _liveStrips.Count; i++)
            {
                if (_liveStrips[i].Row == row)
                {
                    return true;
                }
            }
            return false;
        }

        private bool DayBound(int row)
        {
            for (int i = 0; i < _liveDays.Count; i++)
            {
                if (_liveDays[i].Row == row)
                {
                    return true;
                }
            }
            return false;
        }

        private StripCell TakeStrip()
        {
            StripCell cell = _freeStrips.Count > 0 ? _freeStrips.Pop() : MakeStrip();
            _liveStrips.Add(cell);
            return cell;
        }

        private DayCell TakeDay()
        {
            DayCell cell = _freeDays.Count > 0 ? _freeDays.Pop() : MakeDay();
            _liveDays.Add(cell);
            return cell;
        }

        private void FreeStrip(int at)
        {
            StripCell cell = _liveStrips[at];
            _liveStrips.RemoveAt(at);
            FlareKit.Show(cell.Rect, false);
            cell.Row = -1;
            for (int j = 0; j < cell.Slots.Length; j++)
            {
                cell.Slots[j].Name = null;
                cell.Slots[j].Index = -1;
                if (cell.Slots[j].Picture != null)
                {
                    cell.Slots[j].Picture.texture = null;   // (the texture may be let go next)
                }
            }
            _freeStrips.Push(cell);
        }

        private void FreeDay(int at)
        {
            DayCell cell = _liveDays[at];
            _liveDays.RemoveAt(at);
            FlareKit.Show(cell.Rect, false);
            cell.Row = -1;
            _freeDays.Push(cell);
        }

        private void FreeAll()
        {
            for (int i = _liveStrips.Count - 1; i >= 0; i--)
            {
                FreeStrip(i);
            }
            for (int i = _liveDays.Count - 1; i >= 0; i--)
            {
                FreeDay(i);
            }
            if (_foot != null)
            {
                FlareKit.Show(_foot, false);
            }
        }

        private void BindStrip(StripCell cell, int r)
        {
            GalleryRow row = _rows[r];
            cell.Row = r;
            FlareKit.Show(cell.Rect, true);
            FlareKit.Place(cell.Rect, 0f, row.Top, _gridWidth, row.Height);
            for (int j = 0; j < cell.Slots.Length; j++)
            {
                Slot slot = cell.Slots[j];
                bool on = j < row.Count && j < _columns;
                FlareKit.Show(slot.Back, on);
                if (!on)
                {
                    slot.Name = null;
                    slot.Index = -1;
                    continue;
                }
                PhotoEntry photo = _shown[row.First + j];
                slot.Name = photo.Name;
                slot.Index = row.First + j;
                FlareKit.Place(slot.Back.rectTransform, j * (_cell + GalleryRules.CellGap), 0f, _cell, _cell);
                FlareKit.Place(slot.FavShade.rectTransform, 12f, _cell - 70f, 58f, 58f);
                FlareKit.Place(slot.WallShade.rectTransform, _cell - 70f, _cell - 70f, 56f, 56f);
                MarkSlot(slot);
                _thumbs.TryGetValue(photo.Name, out Texture2D small);
                Picture(slot.Picture, small);
            }
        }

        private void MarkSlot(Slot slot)
        {
            FlareKit.Show(slot.FavShade, _director.IsFavourite(slot.Name));
            FlareKit.Show(slot.WallShade, slot.Name != null && slot.Name == _director.WallpaperName);
        }

        private void BindDay(DayCell cell, int r, int today)
        {
            GalleryRow row = _rows[r];
            cell.Row = r;
            FlareKit.Show(cell.Rect, true);
            FlareKit.Place(cell.Rect, 0f, row.Top, _gridWidth, row.Height);
            FlareKit.Place(cell.Label.rectTransform, 4f, 0f, _gridWidth * 0.6f, row.Height - 22f);
            FlareKit.Place(cell.Count.rectTransform, _gridWidth * 0.6f, 0f, _gridWidth * 0.4f - 4f, row.Height - 24f);
            FlareKit.Set(cell.Label, PhotosText.Day(row.Day, today));
            FlareKit.Set(cell.Count, PhotosText.Count(row.Count));
        }

        // A small copy shown square: its middle, as a phone's grid crops it.
        private static void Picture(RawImage picture, Texture texture)
        {
            picture.texture = texture;
            FlareKit.Show(picture, texture != null);
            if (texture != null && texture.height > 0 && texture.width > 0)
            {
                float shape = (float)texture.width / texture.height;
                picture.uvRect = shape >= 1f ? new Rect((1f - 1f / shape) / 2f, 0f, 1f / shape, 1f) : new Rect(0f, (1f - shape) / 2f, 1f, shape);
            }
        }

        private void Tapped(Slot slot)
        {
            if (slot.Index >= 0 && _nav.Tap(slot.Index, _shown.Count))
            {
                WriteViewer();
            }
        }

        // The grid scrolled, if need be, so that a photo's strip is in view (back from the viewer, which may have gone on).
        private void Reveal(int index)
        {
            int r = GalleryRules.RowOf(_rows, index);
            if (r < 0)
            {
                return;
            }
            float to = GalleryRules.ScrollFor(_rows[r], _content.anchoredPosition.y, _viewport.rect.height, _total);
            if (Mathf.Abs(to - _content.anchoredPosition.y) > 0.5f)
            {
                _glide.Stop();
                _scroll.StopMovement();
                _content.anchoredPosition = new Vector2(0f, to);
                _nav.Scroll = to;
                Fill();
            }
        }

        // ---------------------------------------------------------------- the small copies

        // The photos in view whose small copy is not made yet, asked for (in order, top first), and the reader started.
        private void WantInView()
        {
            _want.Clear();
            for (int i = 0; i < _liveStrips.Count; i++)
            {
                Slot[] slots = _liveStrips[i].Slots;
                for (int j = 0; j < slots.Length; j++)
                {
                    Want(slots[j].Name);
                }
            }
            WantFilm();
            StartReading();
        }

        private void Want(string name)
        {
            if (name != null && !_thumbs.ContainsKey(name) && !_loading.Contains(name) && !_missing.Contains(name) && !_want.Contains(name))
            {
                _want.Add(name);
            }
        }

        private void StartReading()
        {
            if (_want.Count > 0 && _readingFor != _opening && Showing)
            {
                _readingFor = _opening;
                _director.StartCoroutine(Guard.Routine(ReadSmall(_opening)));
            }
        }

        // Small copies read a few at a time on a worker (each some 100 KB), then made textures here within a millisecond a
        // frame, each shown as it is made; until none is wanted, or the app is closed or opened again.
        private IEnumerator ReadSmall(int opening)
        {
            int missing = 0;
            try
            {
                while (_want.Count > 0 && opening == _opening && Showing)
                {
                    int n = Mathf.Min(GalleryRules.ReadAtOnce, _want.Count);
                    var names = new string[n];
                    for (int i = 0; i < n; i++)
                    {
                        names[i] = _want[i];
                        _loading.Add(names[i]);
                    }
                    _want.RemoveRange(0, n);
                    var rows = new byte[n][];
                    var widths = new int[n];
                    var heights = new int[n];
                    string folder = _director.Folder;
                    Task work = Task.Run(() =>
                    {
                        for (int i = 0; i < names.Length; i++)
                        {
                            rows[i] = PhotoThumbs.Read(PhotoThumbs.FileOf(Path.Combine(folder, names[i])), out widths[i], out heights[i]);
                        }
                    });
                    while (!work.IsCompleted)
                    {
                        yield return null;
                    }
                    var watch = Stopwatch.StartNew();
                    for (int i = 0; i < n; i++)
                    {
                        if (opening != _opening || !Showing)
                        {
                            yield break;   // closed, or opened again, meanwhile (Forget let the rest go)
                        }
                        _loading.Remove(names[i]);
                        Texture2D made = PhotosGame.SmallTexture(rows[i], widths[i], heights[i]);
                        rows[i] = null;
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
                        ThumbCame(names[i], made);
                        if (watch.Elapsed.TotalMilliseconds >= GalleryRules.UploadBudgetMs && i < n - 1)
                        {
                            yield return null;
                            watch.Restart();
                        }
                    }
                    Trim();
                }
            }
            finally
            {
                if (_readingFor == opening)
                {
                    _readingFor = -1;
                }
            }
            if (missing > 0)
            {
                Logger?.Info("Gallery: " + missing + " photo(s) have no small copy (their Thumbs file was taken away); they show without one.");
            }
            if (opening == _opening && _want.Count > 0)
            {
                StartReading();   // (wanted while the last of a batch was being made)
            }
        }

        // A small copy made: shown wherever it is wanted now (a square in view, the viewer's strip, the viewer's stand-in).
        private void ThumbCame(string name, Texture2D made)
        {
            for (int i = 0; i < _liveStrips.Count; i++)
            {
                Slot[] slots = _liveStrips[i].Slots;
                for (int j = 0; j < slots.Length; j++)
                {
                    if (slots[j].Name == name)
                    {
                        Picture(slots[j].Picture, made);
                    }
                }
            }
            FilmCame(name, made);
        }

        // Past the most kept, the small copies of photos built nowhere now let go (read again if they come back into view).
        private void Trim()
        {
            if (_thumbs.Count <= GalleryRules.KeptMost)
            {
                return;
            }
            _keep.Clear();
            for (int i = 0; i < _liveStrips.Count; i++)
            {
                Slot[] slots = _liveStrips[i].Slots;
                for (int j = 0; j < slots.Length; j++)
                {
                    if (slots[j].Name != null)
                    {
                        _keep.Add(slots[j].Name);
                    }
                }
            }
            KeepFilm(_keep);
            _drop.Clear();
            foreach (KeyValuePair<string, Texture2D> kept in _thumbs)
            {
                if (!_keep.Contains(kept.Key))
                {
                    _drop.Add(kept.Key);
                }
            }
            for (int i = 0; i < _drop.Count; i++)
            {
                PhotosGame.Destroy(_thumbs[_drop[i]]);
                _thumbs.Remove(_drop[i]);
            }
        }

        // Every small copy let go, and every square emptied.
        private void ForgetThumbs()
        {
            foreach (KeyValuePair<string, Texture2D> kept in _thumbs)
            {
                PhotosGame.Destroy(kept.Value);
            }
            _thumbs.Clear();
            _missing.Clear();
            _loading.Clear();
            _want.Clear();
            _readingFor = -1;
            FreeAll();
            ForgetFilm();
        }

        // The parts made for a panel that is gone (the app taken off the phone): made again with the next panel.
        private void ForgetParts()
        {
            _liveStrips.Clear();
            _freeStrips.Clear();
            _allStrips.Clear();
            _liveDays.Clear();
            _freeDays.Clear();
            _allDays.Clear();
            _rows.Clear();
            _list = _viewport = _content = _foot = null;
            _scroll = null;
            _glide = null;
            ForgetViewerParts();
        }
    }
}
