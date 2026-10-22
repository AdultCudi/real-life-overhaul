using System;
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
    /// The Camera app on the phone: opening it opens the camera at once, so its page (why the camera cannot open, the photo
    /// count, the newest photo) is seen only when the camera is refused. Nothing per frame: the page is written when the
    /// app opens and when a photo is added.
    /// </summary>
    internal sealed class CameraApp
    {
        public const string AppId = "RealLifeCamera";
        private const string GuideTitle = "reallife_meta_title_photos";

        public static IModLogger Logger;

        private readonly PhotosDirector _director;
        private PhoneApp _app;
        private AppShell _shell;
        private RawImage _picture;
        private TextMeshProUGUI _caption;
        private RectTransform _sheet;
        private bool _gaveUp;
        private CameraRefusal _refused;

        public CameraApp(PhotosDirector director)
        {
            _director = director;
        }

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        /// <summary>Called once a second until it takes, since the phone is built a little after the city.</summary>
        public void Install()
        {
            if (_app != null || _gaveUp)
            {
                return;
            }
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_phonecam_app",
                Icon = like => AppIconPainter.Paint(like, FlareIcons.Get(FlareGlyph.Camera), new Color32(92, 98, 112, 255), new Color32(28, 30, 36, 255), 0.46f, "AppIcon-Camera"),
                Build = Build,
                Opened = () => UiClone.Safe(Opened),
                Closed = Closed
            }, out bool tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Camera: the game's phone is not built as this version expects, so the Camera app is left off. Flare's camera button still opens the camera.");
                }
            }
        }

        public void Remove()
        {
            _app?.Remove();
            _app = null;
            _shell = null;
            _picture = null;
            _caption = null;
            _sheet = null;
            _gaveUp = false;
        }

        private GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("photos: the Camera app being built from the game's MyEmployees");   // once per city
            _shell = AppShell.Make(menu, dormant, AppId);
            if (_shell == null)
            {
                return null;
            }
            UiClone.Key(_shell.TabLabels[0], "reallife_phonecam_button_rear");
            UiClone.Key(_shell.TabLabels[1], "reallife_phonecam_button_selfie");
            UiClone.Rewire(_shell.Tabs[0], () => UiClone.Safe(() => Ask(false)));
            UiClone.Rewire(_shell.Tabs[1], () => UiClone.Safe(() => Ask(true)));
            var list = _shell.List as RectTransform;
            if (list == null)
            {
                UiClone.Fallback("Camera: the table is not laid out as expected");
                return null;
            }
            _sheet = GallerySheet.Make(list, "Latest");
            _picture = FlareKit.Picture(_sheet, "Picture");
            _caption = FlareKit.Text(_sheet, "Caption", 30f, new Color(0.35f, 0.39f, 0.43f, 1f), FontStyles.Normal, TextAlignmentOptions.Center);
            return _shell.Root;
        }

        private void Opened()
        {
            if (_shell == null)
            {
                return;
            }
            _refused = CameraRules.Refuse(PhotosGame.Situation(false));
            Write();
            if (_refused == CameraRefusal.None)
            {
                Ask(false);
            }
        }

        private void Closed()
        {
            if (_picture != null)
            {
                _picture.texture = null;
            }
        }

        private void Ask(bool selfie)
        {
            _director.OpenCamera(selfie, false);
            _refused = CameraRules.Refuse(PhotosGame.Situation(false));
            if (Showing)
            {
                Write();
            }
        }

        /// <summary>A photo was added or deleted.</summary>
        public void Changed()
        {
            if (Showing)
            {
                Write();
            }
        }

        private void Write()
        {
            _shell.ShowTab(-1);
            UiClone.Words(_shell.Header, PhotosText.T("reallife_phonecam_app"));
            InfoBox box = _shell.Box;
            box.Clear();
            box.Title(PhotosText.T("reallife_phonecam_app"));
            string why = CameraRules.RefusalKey(_refused);
            if (why != null)
            {
                box.Line(PhotosText.T(why), CellTone.Yellow);
            }
            box.Row("reallife_gallery_row_count", PhotosText.Fill("reallife_gallery_count_of", "count", PhotosText.N(_director.Count), "cap", PhotosText.N(PhotoStoreRules.Cap)));
            box.Note(PhotosText.T("reallife_phonecam_page_note"));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_phonecam_button_rear", () => Ask(false));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_phonecam_button_gallery", () => UiClone.Safe(_director.OpenGallery));
            Transform second = box.ButtonRow();
            InfoBox.Button(second, InfoBox.Positive, "reallife_phonecam_button_selfie", () => Ask(true));
            Meta.Guide.Point(box, GuideTitle);
            box.Done();
            WriteLatest();
        }

        private void WriteLatest()
        {
            Texture latest = _director.LatestTexture;
            float w = _sheet.rect.width, h = _sheet.rect.height;
            _picture.texture = latest;
            FlareKit.Show(_picture, latest != null);
            if (latest != null && w > 100f && h > 100f)
            {
                float shape = (float)latest.width / Mathf.Max(1, latest.height);
                float pw = Mathf.Min(w - 120f, (h - 200f) * shape), ph = pw / shape;
                FlareKit.Place(_picture.rectTransform, (w - pw) / 2f, 60f, pw, ph);
                FlareKit.Place(_caption.rectTransform, 40f, 80f + ph, w - 80f, 60f);
            }
            else
            {
                FlareKit.Place(_caption.rectTransform, 40f, h / 2f - 30f, Mathf.Max(0f, w - 80f), 60f);
            }
            PhotoEntry newest = _director.Count > 0 ? _director.Photos[0] : null;
            FlareKit.Set(_caption, newest != null ? PhotosText.Fill("reallife_phonecam_latest", "taken", PhotosText.Taken(newest)) : PhotosText.T("reallife_gallery_empty"));
        }
    }

    /// <summary>
    /// The right side of a photos page: a copy of the game's list background stretched over the table. MyEmployees' prefab
    /// keeps that background switched off, so the copy must be switched on or nothing on it draws or takes a click.
    /// </summary>
    internal static class GallerySheet
    {
        public static RectTransform Make(RectTransform list, string name)
        {
            Transform background = list.Find("ListBackground");
            GameObject sheet = background != null ? UnityEngine.Object.Instantiate(background.gameObject, list, false) : new GameObject(name, typeof(RectTransform), typeof(Image));
            sheet.name = name;
            for (int i = sheet.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(sheet.transform.GetChild(i).gameObject);
            }
            var paper = sheet.GetComponent<Image>() ?? sheet.AddComponent<Image>();
            paper.raycastTarget = true;   // takes the clicks meant for the table under it
            var unlaid = sheet.GetComponent<LayoutElement>() ?? sheet.AddComponent<LayoutElement>();
            unlaid.ignoreLayout = true;
            var rect = (RectTransform)sheet.transform;
            FlareKit.Stretch(rect, 0f, 0f, 0f, 0f);
            rect.SetAsLastSibling();
            sheet.SetActive(true);
            return rect;
        }
    }
}
