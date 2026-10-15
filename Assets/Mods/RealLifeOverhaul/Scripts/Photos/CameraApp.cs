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
    /// The Camera app on the phone. Opening it puts the phone away and opens the camera (CameraMode) at once, as a phone's
    /// camera opens; the app's own page is seen only for the moment the phone takes to close, or when the camera cannot
    /// open now (the game's notice says why, and the page says it too). In the game's frame (GameUi.AppShell):
    ///
    ///   the white box   the title "Camera", why it cannot open now (if it cannot), the save's photos against the most it
    ///                   keeps, a note on where it works, and three buttons: Rear camera, Selfie, Gallery; the Guide's page
    ///   the tabs        Rear camera and Selfie: either opens the camera that way
    ///   the right side  the newest photo, large, over the game's own list background, with when it was taken
    ///
    /// PER FRAME: NOTHING. The page is written when the app opens and when a photo is added.
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

        /// <summary>Once a second while the Camera runs: the app among the game's (the phone is built a little after the city).</summary>
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

        // ---------------------------------------------------------------- building it

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

        // ---------------------------------------------------------------- opening

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
                Ask(false);   // the camera opens at once, as a phone's does
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

        /// <summary>A photo was added or deleted: the page is written again if it is showing.</summary>
        public void Changed()
        {
            if (Showing)
            {
                Write();
            }
        }

        // ---------------------------------------------------------------- the page

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

        // The newest photo, as large as the right side has room for, its shape kept.
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
    /// The right side of the Camera's page: the game's own list background over the table, copied and stretched (as
    /// Newsstand's page is), and switched on. The game keeps MyEmployees' ListBackground switched off (its prefab has it so
    /// and nothing of the game's switches it on), and a copy of an object that is off is off: in 0.27.0 the copy was
    /// never switched on, so nothing on it was drawn or could be clicked (the Gallery's photos among it).
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
            paper.raycastTarget = true;   // the sheet takes the clicks meant for the table under it
            var unlaid = sheet.GetComponent<LayoutElement>() ?? sheet.AddComponent<LayoutElement>();
            unlaid.ignoreLayout = true;
            var rect = (RectTransform)sheet.transform;
            FlareKit.Stretch(rect, 0f, 0f, 0f, 0f);
            rect.SetAsLastSibling();
            sheet.SetActive(true);   // (a copy of the game's switched-off background is switched off: see above)
            return rect;
        }
    }
}
