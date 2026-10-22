using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// The Gallery app: a photos app of its own look in the game's phone frame, an exception to the native rule as Flare
    /// is. Two screens, the grid of the album by day and the viewer of one photo. Only the rows in view are built and
    /// their small copies read; every texture is let go on closing. Nothing per frame while it stands still.
    /// </summary>
    internal sealed partial class GalleryApp
    {
        public const string AppId = "RealLifeGallery";
        private const float Margin = 34f;           // canvas units
        private const float HeaderHeight = 200f;    // canvas units

        private static readonly string[] AlbumKeys = { "reallife_gallery_tab_all", "reallife_gallery_tab_selfies", "reallife_gallery_album_favourites" };

        public static IModLogger Logger;

        private readonly PhotosDirector _director;
        private readonly GalleryNav _nav = new GalleryNav();
        private readonly List<PhotoEntry> _shown = new List<PhotoEntry>(PhotoStoreRules.Cap + 16);
        private PhoneApp _app;
        private AppShell _shell;
        private RectTransform _root, _library;
        private Image _backdrop;
        private GalleryKeys _keys;
        private GalleryLook _look;
        private bool _gaveUp, _built, _writing;
        private float _latestAskedAt = -1f;         // real seconds when the newest photo was asked for, or -1
        private int _opening;                       // counts the openings, so a worker's answer for an earlier one is let go
        private float _laidWidth = -1f, _laidHeight = -1f;

        private TextMeshProUGUI _title, _count;
        private Image _segBack, _segPick, _cameraButton, _cameraIcon, _headerLine;
        private readonly Image[] _segHits = new Image[3];
        private readonly TextMeshProUGUI[] _segLabels = new TextMeshProUGUI[3];

        private RectTransform _empty;
        private Image _emptyIcon, _emptyButton;
        private TextMeshProUGUI _emptyTitle, _emptyBody, _emptyLabel;

        public GalleryApp(PhotosDirector director)
        {
            _director = director;
        }

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        /// <summary>Called once a second until it takes.</summary>
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
            _root = _library = null;
            _built = false;
            _laidWidth = _laidHeight = -1f;
            ForgetParts();
            if (_keys != null)
            {
                _keys.Owner = null;
                _keys.enabled = false;
            }
            _gaveUp = false;
        }

        /// <summary>Opens the app, with <paramref name="latest"/> on the newest photo shown whole.</summary>
        public void Open(bool latest)
        {
            if (_app == null || !_app.Installed)
            {
                return;
            }
            _latestAskedAt = latest ? Time.unscaledTime : -1f;
            if (Showing)
            {
                Refresh(false);
            }
            else
            {
                _app.Open();
            }
        }

        /// <summary>A photo was added or deleted, or the list was read.</summary>
        public void Changed()
        {
            if (Showing && !_writing)
            {
                Refresh(false);
            }
        }

        private GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("photos: the Gallery app being built in the game's frame");   // once per city
            _shell = AppShell.Make(menu, dormant, AppId);
            if (_shell == null)
            {
                return null;
            }
            Transform layout = _shell.Root.transform.Find("Layout 30-70");
            if (layout == null)
            {
                UiClone.Fallback("Gallery: the game's frame is not laid out as expected");
                return null;
            }
            for (int i = 0; i < layout.childCount; i++)
            {
                layout.GetChild(i).gameObject.SetActive(false);   // the game's box and table: the Gallery draws its own
            }
            _root = FlareKit.Rect(layout, "Gallery");
            _root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            FlareKit.Stretch(_root, 0f, 0f, 0f, 0f);
            _backdrop = FlareKit.Box(_root, "Back", Color.white, FlareKit.Radius, false, true);   // takes every click that lands on the app
            FlareKit.Stretch(_backdrop.rectTransform, 0f, 0f, 0f, 0f);
            _library = FlareKit.Rect(_root, "Library");
            FlareKit.Stretch(_library, 0f, 0f, 0f, 0f);
            BuildHeader();
            BuildGrid();
            BuildEmpty();
            BuildViewer();
            _keys = _director.gameObject.GetComponent<GalleryKeys>() ?? _director.gameObject.AddComponent<GalleryKeys>();
            _keys.Owner = this;
            _keys.enabled = false;
            _look = null;
            _built = true;
            return _shell.Root;
        }

        private void BuildHeader()
        {
            _title = FlareKit.Text(_library, "Title", 64f, Color.white, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            _count = FlareKit.Text(_library, "Count", 28f, Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _segBack = FlareKit.Box(_library, "Albums", Color.white, 0f, true);
            _segPick = FlareKit.Box(_segBack.transform, "Chosen", Color.white, 0f, true);
            for (int i = 0; i < _segHits.Length; i++)
            {
                int album = i;
                _segHits[i] = FlareKit.Box(_segBack.transform, "Album" + i, GalleryLook.Clear, 0f, true, true);
                FlareKit.Click(_segHits[i], () => SwitchAlbum((GalleryAlbum)album));
                _segLabels[i] = FlareKit.Text(_segHits[i].transform, "Label", 28f, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
                FlareKit.Stretch(_segLabels[i].rectTransform, 8f, 0f, 8f, 0f);
                _segLabels[i].textWrappingMode = TextWrappingModes.NoWrap;
                _segLabels[i].text = PhotosText.T(AlbumKeys[i]);
            }
            _cameraButton = FlareKit.Box(_library, "Camera", Color.white, 0f, true, true);
            FlareKit.Click(_cameraButton, OpenCamera);
            _cameraIcon = FlareKit.Box(_cameraButton.transform, "Icon", Color.white);
            _cameraIcon.sprite = FlareIcons.Get(FlareGlyph.Camera);
            FlareKit.Place(_cameraIcon.rectTransform, 20f, 20f, 48f, 48f);
            _headerLine = FlareKit.Box(_library, "Line", Color.white);
        }

        private void BuildEmpty()
        {
            _empty = FlareKit.Rect(_library, "Empty");
            _emptyIcon = FlareKit.Box(_empty, "Icon", Color.white);
            _emptyIcon.sprite = FlareIcons.Get(FlareGlyph.Picture);
            _emptyTitle = FlareKit.Text(_empty, "Title", 44f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            _emptyBody = FlareKit.Text(_empty, "Body", 30f, Color.white, FontStyles.Normal, TextAlignmentOptions.Top);
            _emptyButton = FlareKit.Box(_empty, "Camera", Color.white, 0f, true, true);
            FlareKit.Click(_emptyButton, OpenCamera);
            _emptyLabel = FlareKit.Text(_emptyButton.transform, "Label", 32f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            FlareKit.Stretch(_emptyLabel.rectTransform, 0f, 0f, 0f, 0f);
            _emptyLabel.text = PhotosText.T("reallife_gallery_button_camera");
            FlareKit.Show(_empty, false);
        }

        private void Opened()
        {
            if (!_built)
            {
                return;
            }
            _opening++;
            _nav.Back();
            _nav.Scroll = 0f;
            Refresh(true);
        }

        private void Closed()
        {
            _latestAskedAt = -1f;   // an opening that never came does not open a later one on the newest photo
            Forget();
            _nav.Back();
            if (_keys != null)
            {
                _keys.enabled = false;
            }
        }

        // Everything shown written again; 'top' takes the grid back to its top.
        private void Refresh(bool top)
        {
            if (!_built || _writing)
            {
                return;
            }
            _writing = true;
            try
            {
                GalleryLook look = GalleryLook.Now(PhotosGame.Hour);
                if (look != _look)
                {
                    _look = look;
                    Paint();
                }
                string viewing = _nav.Viewing && _nav.Index >= 0 && _nav.Index < _shown.Count ? _shown[_nav.Index].Name : null;
                GalleryRules.Album(_director.Photos, _nav.Album, _director.Favourites, _shown);
                _nav.Reread(GalleryRules.IndexOf(_shown, viewing), _shown.Count);
                if (GalleryRules.LatestWanted(_latestAskedAt, Time.unscaledTime))
                {
                    _latestAskedAt = -1f;
                    OpenLatest();
                }
                Lay();
                WriteHeader();
                WriteGrid(top);
                WriteViewer();
            }
            finally
            {
                _writing = false;
            }
        }

        // On All photos, where every photo is.
        private void OpenLatest()
        {
            if (_director.Count == 0)
            {
                return;
            }
            if (_nav.Switch(GalleryAlbum.All))
            {
                GalleryRules.Album(_director.Photos, _nav.Album, _director.Favourites, _shown);
            }
            _nav.Tap(GalleryRules.IndexOf(_shown, _director.Photos[0].Name), _shown.Count);
        }

        private void SwitchAlbum(GalleryAlbum album)
        {
            if (_nav.Switch(album))
            {
                Refresh(true);
            }
        }

        private void OpenCamera()
        {
            _director.OpenCamera(false, false);
        }

        // Laid out again only when the frame's size changes.
        private void Lay()
        {
            float w = _root.rect.width, h = _root.rect.height;
            if (w < 100f)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_shell.Root.transform);
                w = _root.rect.width;
                h = _root.rect.height;
            }
            if (w < 100f || (Mathf.Abs(w - _laidWidth) < 1f && Mathf.Abs(h - _laidHeight) < 1f))
            {
                return;
            }
            _laidWidth = w;
            _laidHeight = h;
            float colW = Mathf.Min(GalleryRules.GridMost, w - Margin * 4f);
            float x = (w - colW) / 2f;
            const float segW = 270f, segH = 76f, camera = 88f;
            float segAll = segW * _segHits.Length + 12f;
            FlareKit.Place(_title.rectTransform, x, Margin, colW - segAll - camera - 60f, 96f);
            FlareKit.Place(_count.rectTransform, x, Margin + 104f, colW - segAll - camera - 60f, 40f);
            FlareKit.Place(_cameraButton.rectTransform, x + colW - camera, Margin + 38f, camera, camera);
            FlareKit.Place(_segBack.rectTransform, x + colW - camera - 24f - segAll, Margin + 44f, segAll, segH);
            for (int i = 0; i < _segHits.Length; i++)
            {
                FlareKit.Place(_segHits[i].rectTransform, 6f + i * segW, 6f, segW, segH - 12f);
            }
            FlareKit.Place(_headerLine.rectTransform, x, HeaderHeight - 2f, colW, 2f);
            LayGrid(x, HeaderHeight, colW, h - HeaderHeight);
            FlareKit.Place(_empty, x, HeaderHeight, colW, h - HeaderHeight);
            float ew = Mathf.Min(1100f, colW), ex = (colW - ew) / 2f, ey = Mathf.Max(60f, (h - HeaderHeight) / 2f - 330f);
            FlareKit.Place(_emptyIcon.rectTransform, (colW - 160f) / 2f, ey, 160f, 160f);
            FlareKit.Place(_emptyTitle.rectTransform, ex, ey + 190f, ew, 60f);
            FlareKit.Place(_emptyBody.rectTransform, ex, ey + 264f, ew, 130f);
            FlareKit.Place(_emptyButton.rectTransform, (colW - 420f) / 2f, ey + 420f, 420f, 92f);
            LayViewer(w, h);
        }

        // The grid's colours for the look now; the viewer keeps its own.
        private void Paint()
        {
            GalleryLook l = _look;
            _backdrop.color = l.Back;
            _title.color = l.Text;
            _count.color = l.Muted;
            _segBack.color = l.Chip;
            _segPick.color = l.Card;
            _cameraButton.color = l.Accent;
            _cameraIcon.color = l.AccentText;
            _headerLine.color = l.Line;
            _emptyIcon.color = l.Muted;
            _emptyTitle.color = l.Text;
            _emptyBody.color = l.Muted;
            _emptyButton.color = l.Accent;
            _emptyLabel.color = l.AccentText;
            PaintGrid();
        }

        private void WriteHeader()
        {
            int album = (int)_nav.Album;
            FlareKit.Set(_title, PhotosText.T(AlbumKeys[album]));
            FlareKit.Set(_count, PhotosText.Count(_shown.Count));
            for (int i = 0; i < _segLabels.Length; i++)
            {
                bool on = i == album;
                _segLabels[i].fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                _segLabels[i].color = on ? _look.Text : _look.Muted;
            }
            RectTransform hit = _segHits[album].rectTransform;
            FlareKit.Place(_segPick.rectTransform, hit.anchoredPosition.x, -hit.anchoredPosition.y, hit.sizeDelta.x, hit.sizeDelta.y);
            bool empty = _shown.Count == 0;
            FlareKit.Show(_empty, empty);
            if (empty)
            {
                switch (_nav.Album)
                {
                    case GalleryAlbum.Selfies:
                        FlareKit.Set(_emptyTitle, PhotosText.T("reallife_gallery_empty_title_selfies"));
                        FlareKit.Set(_emptyBody, PhotosText.T("reallife_gallery_empty_body_selfies"));
                        break;
                    case GalleryAlbum.Favourites:
                        FlareKit.Set(_emptyTitle, PhotosText.T("reallife_gallery_empty_title_favourites"));
                        FlareKit.Set(_emptyBody, PhotosText.T("reallife_gallery_empty_body_favourites"));
                        break;
                    default:
                        FlareKit.Set(_emptyTitle, PhotosText.T("reallife_gallery_empty_title_all"));
                        FlareKit.Set(_emptyBody, PhotosText.T("reallife_gallery_empty_body_all"));
                        break;
                }
                FlareKit.Show(_emptyButton, _nav.Album != GalleryAlbum.Favourites);
            }
        }

        // Every texture let go; reopening reads them again.
        private void Forget()
        {
            _opening++;
            ForgetThumbs();
            DropWhole();
        }
    }
}
