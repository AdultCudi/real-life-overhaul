using System.Collections.Generic;
using System.IO;
using Localizor;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Notification;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// The photo shown whole, over the whole of the app: dark, the photo fitted with its shape kept, a thin top bar (back,
    /// the game's day and hour it was taken, the kind and the real time, its place in the album), round arrows either side,
    /// a strip of up to nine neighbours (the one shown framed), and a bar of icon buttons: Flare (while Flare runs),
    /// Wallpaper, Favourite, Delete. Shown at once on a tap: the photo's small copy stands in until the whole photo is read
    /// (on a worker) and decoded (once, on the main thread: PhotosGame.Load).
    ///
    /// COST: nothing per frame but the three keys GalleryKeys reads while it shows. A step writes a few texts and asks for one
    /// photo; the texture shown before is destroyed when the next comes or the viewer closes.
    /// </summary>
    internal sealed partial class GalleryApp
    {
        private const float TopBar = 128f;      // canvas units of the viewer's top bar
        private const float BottomBar = 156f;   // ...of its bar of buttons
        private const float FilmCell = 92f;     // ...of a neighbour in the strip
        private const float FilmGap = 10f;
        private const float ActWidth = 210f;    // ...of a button of the bar (room for "Papel de parede")
        private const float ActGap = 16f;
        private const int Acts = 4;             // Flare, Wallpaper, Favourite, Delete
        private const int ActShare = 0, ActWallpaper = 1, ActFavourite = 2, ActDelete = 3;

        private static readonly string[] ActKeys = { "reallife_gallery_act_share", "reallife_gallery_act_wallpaper", "reallife_gallery_act_favourite", "reallife_gallery_button_delete" };

        private RectTransform _viewer;
        private Image _area, _backHit, _prevHit, _nextHit, _topLine, _bottomLine;
        private RawImage _photo;
        private TextMeshProUGUI _when, _sub, _place, _loadingText, _keysHint;
        private readonly Film[] _film = new Film[GalleryRules.FilmSide * 2 + 1];
        private readonly Act[] _acts = new Act[Acts];
        private float _areaX, _areaY, _areaW, _areaH, _viewW, _viewH;
        private Texture2D _wholeTexture;
        private string _wholeName;
        private int _wholeLoad;                // counts the whole photo's loads: only the latest one's texture is kept, any other destroyed

        // A neighbour in the strip under the photo: its frame (lit for the one shown) and its small copy.
        private sealed class Film
        {
            public Image Frame;
            public RawImage Picture;
            public string Name;
            public int Index = -1;
        }

        // A button of the bar: an icon over a word.
        private sealed class Act
        {
            public Image Hit, Icon;
            public TextMeshProUGUI Label;
        }

        // ---------------------------------------------------------------- building

        private void BuildViewer()
        {
            _viewer = FlareKit.Rect(_root, "Viewer");
            FlareKit.Stretch(_viewer, 0f, 0f, 0f, 0f);
            Image back = FlareKit.Box(_viewer, "Back", GalleryLook.ViewerBack, FlareKit.Radius, false, true);
            FlareKit.Stretch(back.rectTransform, 0f, 0f, 0f, 0f);
            _area = FlareKit.Box(_viewer, "Area", GalleryLook.Clear, 0f, false, true);   // takes the wheel over the photo
            _area.gameObject.AddComponent<GalleryWheel>().Step = Step;
            _photo = FlareKit.Picture(_viewer, "Photo");
            _loadingText = FlareKit.Text(_viewer, "Loading", 30f, GalleryLook.ViewerMuted, FontStyles.Normal, TextAlignmentOptions.Center);

            _backHit = FlareKit.Box(_viewer, "BackButton", GalleryLook.ViewerButton, 0f, true, true);
            FlareKit.Click(_backHit, Back);
            Glyph(_backHit.transform, FlareGlyph.Back, 20f, 20f, 40f);
            _when = FlareKit.Text(_viewer, "When", 38f, GalleryLook.ViewerText, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            _when.textWrappingMode = TextWrappingModes.NoWrap;
            _sub = FlareKit.Text(_viewer, "Kind", 26f, GalleryLook.ViewerMuted, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            _sub.textWrappingMode = TextWrappingModes.NoWrap;
            _place = FlareKit.Text(_viewer, "Place", 28f, GalleryLook.ViewerMuted, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
            _place.textWrappingMode = TextWrappingModes.NoWrap;
            _topLine = FlareKit.Box(_viewer, "TopLine", GalleryLook.ViewerLine);

            _prevHit = FlareKit.Box(_viewer, "Before", GalleryLook.ViewerButton, 0f, true, true);
            FlareKit.Click(_prevHit, () => Step(-1));
            Glyph(_prevHit.transform, FlareGlyph.Prev, 24f, 24f, 48f);
            _nextHit = FlareKit.Box(_viewer, "After", GalleryLook.ViewerButton, 0f, true, true);
            FlareKit.Click(_nextHit, () => Step(1));
            Glyph(_nextHit.transform, FlareGlyph.Next, 24f, 24f, 48f);

            for (int i = 0; i < _film.Length; i++)
            {
                var film = new Film();
                film.Frame = FlareKit.Box(_viewer, "Neighbour" + i, GalleryLook.Clear, 10f, false, true);
                FlareKit.Click(film.Frame, () => Jump(film));
                film.Picture = FlareKit.Picture(film.Frame.transform, "Small");
                FlareKit.Stretch(film.Picture.rectTransform, 4f, 4f, 4f, 4f);
                _film[i] = film;
            }

            _bottomLine = FlareKit.Box(_viewer, "BottomLine", GalleryLook.ViewerLine);
            for (int i = 0; i < _acts.Length; i++)
            {
                int act = i;
                var a = new Act();
                a.Hit = FlareKit.Box(_viewer, "Act" + i, GalleryLook.Clear, 24f, false, true);
                FlareKit.Click(a.Hit, () => Pressed(act));
                a.Icon = Glyph(a.Hit.transform, FlareGlyph.Share, (ActWidth - 56f) / 2f, 16f, 56f);
                a.Label = FlareKit.Text(a.Hit.transform, "Label", 24f, GalleryLook.ViewerText, FontStyles.Normal, TextAlignmentOptions.Center);
                a.Label.textWrappingMode = TextWrappingModes.NoWrap;
                FlareKit.Place(a.Label.rectTransform, 0f, 84f, ActWidth, 36f);
                a.Label.text = PhotosText.T(ActKeys[i]);
                _acts[i] = a;
            }
            _acts[ActShare].Icon.sprite = FlareIcons.Get(FlareGlyph.Share);
            _acts[ActWallpaper].Icon.sprite = FlareIcons.Get(FlareGlyph.Phone);
            _acts[ActFavourite].Icon.sprite = FlareIcons.Get(FlareGlyph.Heart);
            _acts[ActDelete].Icon.sprite = FlareIcons.Get(FlareGlyph.Trash);
            _keysHint = FlareKit.Text(_viewer, "Keys", 22f, GalleryLook.ViewerMuted, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            FlareKit.Show(_viewer, false);
        }

        // A glyph, white, square, at a place inside its button.
        private static Image Glyph(Transform parent, FlareGlyph glyph, float x, float y, float size)
        {
            Image icon = FlareKit.Box(parent, "Icon", GalleryLook.ViewerText);
            icon.sprite = FlareIcons.Get(glyph);
            FlareKit.Place(icon.rectTransform, x, y, size, size);
            return icon;
        }

        private void LayViewer(float w, float h)
        {
            _viewW = w;
            _viewH = h;
            FlareKit.Place(_backHit.rectTransform, Margin, 24f, 80f, 80f);
            float textX = Margin + 108f;
            FlareKit.Place(_when.rectTransform, textX, 18f, w - textX * 2f - 260f, 54f);
            FlareKit.Place(_sub.rectTransform, textX, 72f, w - textX * 2f - 260f, 38f);
            FlareKit.Place(_place.rectTransform, w - Margin - 360f, 40f, 360f, 48f);
            FlareKit.Place(_topLine.rectTransform, Margin, TopBar, w - Margin * 2f, 2f);
            float barTop = h - BottomBar;
            FlareKit.Place(_bottomLine.rectTransform, Margin, barTop, w - Margin * 2f, 2f);
            float filmTop = barTop - 18f - FilmCell - 8f;
            _areaX = 160f;
            _areaY = TopBar + 24f;
            _areaW = Mathf.Max(0f, w - _areaX * 2f);
            _areaH = Mathf.Max(0f, filmTop - 24f - _areaY);
            FlareKit.Place(_area.rectTransform, 0f, TopBar + 2f, w, filmTop - TopBar - 2f);
            FlareKit.Place(_loadingText.rectTransform, _areaX, _areaY + _areaH / 2f - 30f, _areaW, 60f);
            float mid = _areaY + _areaH / 2f - 48f;
            FlareKit.Place(_prevHit.rectTransform, Margin + 8f, mid, 96f, 96f);
            FlareKit.Place(_nextHit.rectTransform, w - Margin - 8f - 96f, mid, 96f, 96f);
            for (int i = 0; i < _film.Length; i++)
            {
                FlareKit.Place(_film[i].Frame.rectTransform, 0f, filmTop, FilmCell + 8f, FilmCell + 8f);
            }
            float hintRoom = (w - (ActWidth * Acts + ActGap * (Acts - 1))) / 2f - Margin - 40f;
            FlareKit.Place(_keysHint.rectTransform, Margin + 8f, barTop + 50f, Mathf.Max(0f, hintRoom), 56f);
            FlareKit.Show(_keysHint, hintRoom >= 420f);
        }

        // ---------------------------------------------------------------- writing it

        private void WriteViewer()
        {
            bool on = _nav.Viewing && _nav.Index >= 0 && _nav.Index < _shown.Count;
            FlareKit.Show(_viewer, on);
            FlareKit.Show(_library, !on);
            if (_keys != null)
            {
                _keys.enabled = on;
            }
            if (!on)
            {
                DropWhole();
                return;
            }
            int index = _nav.Index, count = _shown.Count;
            PhotoEntry photo = _shown[index];
            FlareKit.Set(_when, PhotosText.When(photo, PhotosGame.Day));
            FlareKit.Set(_sub, PhotosText.Fill("reallife_gallery_viewer_sub", "kind", PhotosText.T(photo.Selfie ? "reallife_gallery_selfie" : "reallife_gallery_photo"), "real", PhotosText.Real(photo)));
            FlareKit.Set(_place, PhotosText.Fill("reallife_gallery_place", "place", PhotosText.N(index + 1), "count", PhotosText.N(count)));
            FlareKit.Set(_keysHint, PhotosText.Fill("reallife_gallery_viewer_keys", "cancel", PhotosGame.CancelKey));
            FlareKit.Show(_prevHit, GalleryRules.Step(index, -1, count) != index);
            FlareKit.Show(_nextHit, GalleryRules.Step(index, 1, count) != index);
            WriteActs(photo);
            WriteFilm();
            if (_wholeName != photo.Name)
            {
                DropWhole();
                _wholeName = photo.Name;
                _thumbs.TryGetValue(photo.Name, out Texture2D small);
                StandIn(small);
                string name = photo.Name;
                int opening = _opening, load = ++_wholeLoad;
                _director.StartCoroutine(Guard.Routine(PhotosGame.Load(Path.Combine(_director.Folder, name), t => Came(t, name, opening, load), () => load == _wholeLoad && Showing)));
            }
            else
            {
                PlaceWhole();
            }
        }

        // The small copy shown in the photo's place until the whole photo comes (nothing but "Opening the photo" without one).
        private void StandIn(Texture small)
        {
            _photo.texture = small;
            FlareKit.Show(_photo, small != null);
            FlareKit.Set(_loadingText, PhotosText.T("reallife_gallery_loading"));
            FlareKit.Show(_loadingText, small == null);
            PlaceWhole();
        }

        private void WriteActs(PhotoEntry photo)
        {
            bool flare = PhotosBridges.FlareRunning != null && PhotosBridges.FlareRunning();
            bool wallpaper = photo.Name == _director.WallpaperName;
            bool favourite = _director.IsFavourite(photo.Name);
            FlareKit.Show(_acts[ActShare].Hit, flare);
            int shown = flare ? Acts : Acts - 1;
            float x = (_viewW - (ActWidth * shown + ActGap * (shown - 1))) / 2f;
            for (int i = 0; i < _acts.Length; i++)
            {
                if (i == ActShare && !flare)
                {
                    continue;
                }
                FlareKit.Place(_acts[i].Hit.rectTransform, x, _viewH - BottomBar + 12f, ActWidth, BottomBar - 24f);
                x += ActWidth + ActGap;
            }
            Color accent = GalleryLook.Night.Accent, heart = GalleryLook.Night.Heart;
            _acts[ActWallpaper].Icon.color = wallpaper ? accent : GalleryLook.ViewerText;
            _acts[ActWallpaper].Label.color = wallpaper ? accent : GalleryLook.ViewerText;
            _acts[ActFavourite].Icon.sprite = FlareIcons.Get(favourite ? FlareGlyph.HeartFull : FlareGlyph.Heart);
            _acts[ActFavourite].Icon.color = favourite ? heart : GalleryLook.ViewerText;
            _acts[ActFavourite].Label.color = favourite ? heart : GalleryLook.ViewerText;
            _acts[ActDelete].Icon.color = GalleryLook.Night.Danger;
            _acts[ActDelete].Label.color = GalleryLook.ViewerText;
            _acts[ActShare].Icon.color = GalleryLook.ViewerText;
            _acts[ActShare].Label.color = GalleryLook.ViewerText;
        }

        // The strip of neighbours: up to four either side, the one shown framed in the accent; their small copies asked for.
        private void WriteFilm()
        {
            int first = GalleryRules.Film(_nav.Index, _shown.Count, out int last);
            int shown = last - first + 1;
            float total = shown * (FilmCell + 8f) + (shown - 1) * FilmGap;
            float x = (_viewW - total) / 2f;
            for (int i = 0; i < _film.Length; i++)
            {
                Film film = _film[i];
                int index = first + i;
                bool on = index <= last && index >= 0;
                FlareKit.Show(film.Frame, on);
                film.Index = on ? index : -1;
                film.Name = on ? _shown[index].Name : null;
                if (!on)
                {
                    film.Picture.texture = null;
                    continue;
                }
                RectTransform r = film.Frame.rectTransform;
                FlareKit.Place(r, x + i * (FilmCell + 8f + FilmGap), -r.anchoredPosition.y, FilmCell + 8f, FilmCell + 8f);
                film.Frame.color = index == _nav.Index ? GalleryLook.Night.Accent : GalleryLook.ViewerButton;
                _thumbs.TryGetValue(film.Name, out Texture2D small);
                Picture(film.Picture, small);
            }
        }

        // The neighbours whose small copy is not made yet, asked for (with the grid's).
        private void WantFilm()
        {
            if (_viewer == null || !_nav.Viewing)
            {
                return;
            }
            for (int i = 0; i < _film.Length; i++)
            {
                Want(_film[i].Name);
            }
        }

        private void KeepFilm(HashSet<string> keep)
        {
            for (int i = 0; i < _film.Length; i++)
            {
                if (_film[i] != null && _film[i].Name != null)
                {
                    keep.Add(_film[i].Name);
                }
            }
        }

        // A small copy made: in the strip, and in the photo's place if the whole photo has not come yet.
        private void FilmCame(string name, Texture2D made)
        {
            if (_viewer == null || !_nav.Viewing)
            {
                return;
            }
            for (int i = 0; i < _film.Length; i++)
            {
                if (_film[i].Name == name)
                {
                    Picture(_film[i].Picture, made);
                }
            }
            if (name == _wholeName && _wholeTexture == null)
            {
                StandIn(made);
            }
        }

        private void ForgetFilm()
        {
            for (int i = 0; i < _film.Length; i++)
            {
                if (_film[i] != null && _film[i].Picture != null)
                {
                    _film[i].Picture.texture = null;
                }
            }
        }

        private void ForgetViewerParts()
        {
            _viewer = null;
            for (int i = 0; i < _film.Length; i++)
            {
                _film[i] = null;
            }
            for (int i = 0; i < _acts.Length; i++)
            {
                _acts[i] = null;
            }
        }

        // ---------------------------------------------------------------- moving

        /// <summary>The viewer's keys, a frame's look while a photo is shown whole (GalleryKeys): false once it no longer is.</summary>
        internal bool ReadKeys()
        {
            if (!_built || !_nav.Viewing || !Showing)
            {
                return false;
            }
            if (GameConfirm.IsOpen)
            {
                return true;   // (the question box has the keys)
            }
            if (PhotosGame.TakeCancel())
            {
                Back();
                return false;
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Step(-1);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                Step(1);
            }
            return true;
        }

        private void Step(int delta)
        {
            if (_nav.Step(delta, _shown.Count))
            {
                WriteViewer();
            }
        }

        private void Jump(Film film)
        {
            if (film.Index >= 0 && film.Index != _nav.Index && _nav.Tap(film.Index, _shown.Count))
            {
                WriteViewer();
            }
        }

        // Back to the grid where it was, the album read again (a favourite may have changed), the photo last shown in view.
        private void Back()
        {
            string name = _nav.Viewing && _nav.Index >= 0 && _nav.Index < _shown.Count ? _shown[_nav.Index].Name : null;
            _nav.Back();
            Refresh(false);
            Reveal(GalleryRules.IndexOf(_shown, name));
        }

        // ---------------------------------------------------------------- the whole photo

        // A load's answer: kept only if it is the latest load asked for (the same photo viewed again before an earlier load
        // ended gives two answers; the earlier is destroyed, never left unowned), and the texture shown before it let go.
        private void Came(Texture2D texture, string name, int opening, int load)
        {
            if (load != _wholeLoad || opening != _opening || name != _wholeName || !_nav.Viewing || !Showing)
            {
                PhotosGame.Destroy(texture);
                return;
            }
            if (_wholeTexture != null && _wholeTexture != texture)
            {
                PhotosGame.Destroy(_wholeTexture);
            }
            _wholeTexture = texture;
            if (texture != null)
            {
                _photo.texture = texture;
                FlareKit.Show(_photo, true);
                FlareKit.Show(_loadingText, false);
            }
            else
            {
                FlareKit.Set(_loadingText, PhotosText.T("reallife_gallery_unreadable"));
                FlareKit.Show(_loadingText, true);
                FlareKit.Show(_photo, _photo.texture != null);
            }
            PlaceWhole();
        }

        // The photo (or its stand-in) as large as its place allows, its shape kept, in the middle.
        private void PlaceWhole()
        {
            Texture t = _photo.texture;
            if (t == null || _areaW < 100f || _areaH < 100f)
            {
                return;
            }
            float shape = (float)t.width / Mathf.Max(1, t.height);
            float pw = Mathf.Min(_areaW, _areaH * shape), ph = pw / shape;
            FlareKit.Place(_photo.rectTransform, _areaX + (_areaW - pw) / 2f, _areaY + (_areaH - ph) / 2f, pw, ph);
        }

        private void DropWhole()
        {
            _wholeLoad++;   // (a load under way for the photo let go is destroyed when it comes)
            PhotosGame.Destroy(_wholeTexture);
            _wholeTexture = null;
            _wholeName = null;
            if (_photo != null)
            {
                _photo.texture = null;
            }
        }

        // ---------------------------------------------------------------- what can be done with a photo

        private void Pressed(int act)
        {
            if (!_nav.Viewing || _nav.Index < 0 || _nav.Index >= _shown.Count)
            {
                return;
            }
            PhotoEntry photo = _shown[_nav.Index];
            switch (act)
            {
                case ActShare:
                    Share(photo.Name);
                    break;
                case ActWallpaper:
                    Wallpaper(photo.Name == _director.WallpaperName ? null : photo.Name);
                    break;
                case ActFavourite:
                    _director.ToggleFavourite(photo.Name);
                    WriteActs(photo);
                    break;
                default:
                    AskDelete(photo);
                    break;
            }
        }

        private void AskDelete(PhotoEntry photo)
        {
            string name = photo.Name;
            GameConfirm.Show("reallife_gallery_delete_title".Localize(), "reallife_gallery_delete_body".Localize(new { taken = PhotosText.Taken(photo) }),
                () => UiClone.Safe(() => _director.StartCoroutine(Guard.Routine(_director.Delete(new List<string>(1) { name }, _director.Generation)))),
                null, "reallife_gallery_delete_yes", null, true, true);
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
            if (_nav.Viewing && _nav.Index >= 0 && _nav.Index < _shown.Count)
            {
                WriteActs(_shown[_nav.Index]);
            }
        }
    }
}
