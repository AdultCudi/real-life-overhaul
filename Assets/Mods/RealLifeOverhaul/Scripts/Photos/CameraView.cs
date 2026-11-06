using System;
using RealLifeOverhaul.Flare;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// The viewfinder over the whole screen: a phone camera's own look, an exception to the native rule shared with Flare
    /// (the game's font, FlareKit's shapes). Bars, filter and frame chips, the shutter, a toast and the flash, on a canvas
    /// of the game's 3840 by 2160. Built once, about sixty objects; while up, a frame sets only what changed.
    /// </summary>
    internal sealed class CameraView
    {
        private const float Height = 2160f;          // canvas units
        private const float TopBar = 150f;
        private const float BottomBar = 330f;
        private const float ChipHeight = 76f, ChipWidth = 250f, ChipGap = 16f, ChipGroupGap = 60f;
        private const int SortingOrder = 20;         // over the game's HUD and menus, under its text box (21)

        private static readonly Color Bar = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color Pill = new Color(0.12f, 0.12f, 0.14f, 0.85f);
        private static readonly Color Lit = new Color(1f, 0.82f, 0.2f, 1f);
        private static readonly Color Line = new Color(1f, 1f, 1f, 0.45f);
        private static readonly Color Paper = new Color(0.97f, 0.96f, 0.93f, 1f);

        private readonly GameObject _root;
        private readonly RectTransform _canvas;
        private readonly RectTransform _grid, _frame, _controls;
        private readonly Image[] _gridLines = new Image[4];
        private readonly Image[] _border = new Image[4];
        private readonly Image[] _film = new Image[2];
        private readonly Image _top, _bottom, _close, _gridButton, _poseButton, _ring, _disc, _flip, _thumbBack, _toastBack, _flash;
        private readonly TextMeshProUGUI _closeLabel, _mode, _zoom, _gridLabel, _poseLabel, _hint, _flipLabel, _galleryLabel, _toast;
        private readonly RawImage _thumb;
        private readonly Image[] _filterChips = new Image[5];
        private readonly TextMeshProUGUI[] _filterLabels = new TextMeshProUGUI[5];
        private readonly Image[] _frameChips = new Image[3];
        private readonly TextMeshProUGUI[] _frameLabels = new TextMeshProUGUI[3];
        private float _laidWidth = -1f;

        public readonly ViewfinderInput Input;

        public CameraView(Transform parent, Action close, Action shoot, Action flip, Action grid, Action pose, Action gallery, Action<int> filter, Action<int> frame)
        {
            _root = new GameObject("RealLifeOverhaul Camera View", typeof(RectTransform));
            _root.transform.SetParent(parent, false);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(3840f, Height);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            _root.AddComponent<GraphicRaycaster>();
            _canvas = (RectTransform)_root.transform;

            Image surface = FlareKit.Box(_canvas, "Surface", new Color(0f, 0f, 0f, 0f), 0f, false, true);
            FlareKit.Stretch(surface.rectTransform, 0f, 0f, 0f, 0f);
            Input = surface.gameObject.AddComponent<ViewfinderInput>();

            _grid = FlareKit.Rect(_canvas, "Grid");
            FlareKit.Stretch(_grid, 0f, 0f, 0f, 0f);
            for (int i = 0; i < _gridLines.Length; i++)
            {
                _gridLines[i] = FlareKit.Box(_grid, "Line" + i, Line);
            }

            _frame = FlareKit.Rect(_canvas, "Frame");
            FlareKit.Stretch(_frame, 0f, 0f, 0f, 0f);
            for (int i = 0; i < _border.Length; i++)
            {
                _border[i] = FlareKit.Box(_frame, "Border" + i, Paper);
            }
            for (int i = 0; i < _film.Length; i++)
            {
                _film[i] = FlareKit.Box(_frame, "Film" + i, Color.black);
            }

            _controls = FlareKit.Rect(_canvas, "Controls");
            FlareKit.Stretch(_controls, 0f, 0f, 0f, 0f);
            _top = FlareKit.Box(_controls, "Top", Bar);
            _bottom = FlareKit.Box(_controls, "Bottom", Bar);
            _close = Button(_controls, "Close", Pill, out _closeLabel, close);
            _gridButton = Button(_controls, "Grid", Pill, out _gridLabel, grid);
            _poseButton = Button(_controls, "Pose", Pill, out _poseLabel, pose);
            _mode = FlareKit.Text(_controls, "Mode", 44f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            _zoom = FlareKit.Text(_controls, "Zoom", 32f, Lit, FontStyles.Bold, TextAlignmentOptions.Center);
            _hint = FlareKit.Text(_controls, "Keys", 28f, new Color(1f, 1f, 1f, 0.85f), FontStyles.Normal, TextAlignmentOptions.Center);
            _hint.textWrappingMode = TextWrappingModes.NoWrap;

            _ring = FlareKit.Box(_controls, "ShutterRing", Color.white, 0f, true, true);
            _disc = FlareKit.Box(_ring.transform, "Disc", Color.white, 0f, true);
            FlareKit.Click(_ring, shoot);
            var discRing = FlareKit.Box(_ring.transform, "Gap", Color.black, 0f, true);
            discRing.transform.SetSiblingIndex(0);
            FlareKit.Place(discRing.rectTransform, 12f, 12f, 176f, 176f);
            FlareKit.Place(_disc.rectTransform, 20f, 20f, 160f, 160f);

            _thumbBack = FlareKit.Box(_controls, "GalleryButton", Color.white, 24f, false, true);
            FlareKit.Click(_thumbBack, gallery);
            _thumb = FlareKit.Picture(_thumbBack.transform, "Latest");
            FlareKit.Stretch(_thumb.rectTransform, 6f, 6f, 6f, 6f);
            _thumb.color = new Color(0.2f, 0.2f, 0.22f, 1f);
            _galleryLabel = FlareKit.Text(_controls, "GalleryLabel", 28f, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);

            _flip = FlareKit.Box(_controls, "Flip", Pill, 0f, true, true);
            FlareKit.Click(_flip, flip);
            _flipLabel = FlareKit.Text(_flip.transform, "Label", 30f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);   // the camera it turns to
            FlareKit.Stretch(_flipLabel.rectTransform, 10f, 0f, 10f, 0f);

            for (int i = 0; i < _filterChips.Length; i++)
            {
                int n = i;
                _filterChips[i] = Button(_controls, "Filter" + i, Pill, out _filterLabels[i], () => filter(n));
                _filterLabels[i].text = PhotosText.T(CameraRules.Filters[i].NameKey);
            }
            for (int i = 0; i < _frameChips.Length; i++)
            {
                int n = i;
                _frameChips[i] = Button(_controls, "Frame" + i, Pill, out _frameLabels[i], () => frame(n));
                _frameLabels[i].text = PhotosText.T(CameraRules.FrameKeys[i]);
            }

            _toastBack = FlareKit.Box(_controls, "Toast", Pill, 0f, true);
            _toast = FlareKit.Text(_toastBack.transform, "Words", 34f, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            FlareKit.Stretch(_toast.rectTransform, 40f, 0f, 40f, 0f);

            _flash = FlareKit.Box(_canvas, "Flash", new Color(1f, 1f, 1f, 0f));
            FlareKit.Stretch(_flash.rectTransform, 0f, 0f, 0f, 0f);

            _closeLabel.text = PhotosText.T("reallife_phonecam_close");
            _gridLabel.text = PhotosText.T("reallife_phonecam_grid");
            _galleryLabel.text = PhotosText.T("reallife_phonecam_gallery");
            _root.SetActive(false);
        }

        private static Image Button(Transform parent, string name, Color colour, out TextMeshProUGUI label, Action click)
        {
            Image pill = FlareKit.Box(parent, name, colour, 0f, true, true);
            FlareKit.Click(pill, click);
            label = FlareKit.Text(pill.transform, "Label", 30f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            FlareKit.Stretch(label.rectTransform, 12f, 0f, 12f, 0f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return pill;
        }

        public bool Shown => _root != null && _root.activeSelf;

        public void Show(bool on)
        {
            if (_root != null && _root.activeSelf != on)
            {
                _root.SetActive(on);
                if (on)
                {
                    _laidWidth = -1f;
                    Flash(0f);
                    Toast(null, 0f);
                }
            }
        }

        public void Destroy()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }
        }

        /// <summary>Everything placed for the screen's shape; does nothing until the shape changes.</summary>
        public void Lay(CameraFrame frame)
        {
            float w = _canvas.rect.width;
            if (w < 100f || Mathf.Abs(w - _laidWidth) < 1f)
            {
                return;
            }
            _laidWidth = w;
            float h = Height;
            float mid = w / 2f;

            FlareKit.Place(_gridLines[0].rectTransform, w * CameraRules.GridLines[0] - 1.5f, 0f, 3f, h);
            FlareKit.Place(_gridLines[1].rectTransform, w * CameraRules.GridLines[1] - 1.5f, 0f, 3f, h);
            FlareKit.Place(_gridLines[2].rectTransform, 0f, h * CameraRules.GridLines[0] - 1.5f, w, 3f);
            FlareKit.Place(_gridLines[3].rectTransform, 0f, h * CameraRules.GridLines[1] - 1.5f, w, 3f);

            float side = h * CameraRules.BorderSide, foot = h * CameraRules.BorderFoot;
            FlareKit.Place(_border[0].rectTransform, 0f, 0f, w, side);
            FlareKit.Place(_border[1].rectTransform, 0f, 0f, side, h);
            FlareKit.Place(_border[2].rectTransform, w - side, 0f, side, h);
            FlareKit.Place(_border[3].rectTransform, 0f, h - foot, w, foot);
            float bar = h * CameraRules.FilmBar(w / h);
            FlareKit.Place(_film[0].rectTransform, 0f, 0f, w, bar);
            FlareKit.Place(_film[1].rectTransform, 0f, h - bar, w, bar);
            Frame(frame);

            FlareKit.Place(_top.rectTransform, 0f, 0f, w, TopBar);
            FlareKit.Place(_bottom.rectTransform, 0f, h - BottomBar, w, BottomBar);
            FlareKit.Place(_close.rectTransform, 40f, 30f, 230f, 90f);
            FlareKit.Place(_gridButton.rectTransform, w - 270f, 30f, 230f, 90f);
            FlareKit.Place(_poseButton.rectTransform, w - 560f, 30f, 270f, 90f);
            FlareKit.Place(_mode.rectTransform, mid - 500f, 18f, 1000f, 60f);
            FlareKit.Place(_zoom.rectTransform, mid - 300f, 82f, 600f, 50f);
            FlareKit.Place(_hint.rectTransform, 0f, TopBar + 14f, w, 44f);

            float barTop = h - BottomBar;
            FlareKit.Place(_ring.rectTransform, mid - 100f, barTop + 50f, 200f, 200f);
            FlareKit.Place(_thumbBack.rectTransform, mid - 700f, barTop + 60f, 180f, 180f);
            FlareKit.Place(_galleryLabel.rectTransform, mid - 760f, barTop + 250f, 300f, 40f);
            FlareKit.Place(_flip.rectTransform, mid + 520f, barTop + 60f, 180f, 180f);

            float row = _filterChips.Length * ChipWidth + (_filterChips.Length - 1) * ChipGap + ChipGroupGap + _frameChips.Length * ChipWidth + (_frameChips.Length - 1) * ChipGap;
            float x = Mathf.Max(20f, mid - row / 2f), y = barTop - ChipHeight - 24f;
            for (int i = 0; i < _filterChips.Length; i++, x += ChipWidth + ChipGap)
            {
                FlareKit.Place(_filterChips[i].rectTransform, x, y, ChipWidth, ChipHeight);
            }
            x += ChipGroupGap - ChipGap;
            for (int i = 0; i < _frameChips.Length; i++, x += ChipWidth + ChipGap)
            {
                FlareKit.Place(_frameChips[i].rectTransform, x, y, ChipWidth, ChipHeight);
            }
            FlareKit.Place(_toastBack.rectTransform, mid - 700f, h / 2f - 60f, 1400f, 120f);
        }

        public void Write(bool selfie, float zoom, bool grid, string poseKey, int filter, CameraFrame frame)
        {
            FlareKit.Set(_mode, PhotosText.T(selfie ? "reallife_phonecam_mode_selfie" : "reallife_phonecam_mode_rear"));
            FlareKit.Show(_zoom, !selfie);
            FlareKit.Set(_zoom, CameraRules.ZoomWords(zoom));
            FlareKit.Show(_grid, grid);
            _gridButton.color = grid ? Lit : Pill;
            _gridLabel.color = grid ? Color.black : Color.white;
            FlareKit.Show(_poseButton, selfie && poseKey != null);
            if (selfie && poseKey != null)
            {
                FlareKit.Set(_poseLabel, PhotosText.Fill("reallife_phonecam_pose", "pose", PhotosText.T(poseKey)));
            }
            FlareKit.Set(_flipLabel, PhotosText.T(selfie ? "reallife_phonecam_to_rear" : "reallife_phonecam_to_selfie"));
            FlareKit.Set(_hint, PhotosText.Fill(selfie ? "reallife_phonecam_keys_selfie" : "reallife_phonecam_keys", "confirm", PhotosGame.ConfirmKey, "cancel", PhotosGame.CancelKey));
            for (int i = 0; i < _filterChips.Length; i++)
            {
                bool on = i == filter;
                _filterChips[i].color = on ? Lit : Pill;
                _filterLabels[i].color = on ? Color.black : Color.white;
            }
            for (int i = 0; i < _frameChips.Length; i++)
            {
                bool on = i == (int)frame;
                _frameChips[i].color = on ? Lit : Pill;
                _frameLabels[i].color = on ? Color.black : Color.white;
            }
            Frame(frame);
        }

        private void Frame(CameraFrame frame)
        {
            for (int i = 0; i < _border.Length; i++)
            {
                FlareKit.Show(_border[i], frame == CameraFrame.Border);
            }
            for (int i = 0; i < _film.Length; i++)
            {
                FlareKit.Show(_film[i], frame == CameraFrame.Film && _film[i].rectTransform.sizeDelta.y > 0.5f);
            }
        }

        /// <summary>The latest photo's small copy on the Gallery button; null for a plain tile.</summary>
        public void Latest(Texture texture)
        {
            _thumb.texture = texture;
            _thumb.color = texture != null ? Color.white : new Color(0.2f, 0.2f, 0.22f, 1f);
            if (texture != null && texture.height > 0)
            {
                // the middle square of the picture
                float shape = (float)texture.width / texture.height;
                _thumb.uvRect = shape >= 1f ? new Rect((1f - 1f / shape) / 2f, 0f, 1f / shape, 1f) : new Rect(0f, (1f - shape) / 2f, 1f, shape);
            }
        }

        /// <summary>Everything but the photo's frame put away for the frame the screen is read in, or back.</summary>
        public void ForShot(bool shot, bool grid)
        {
            FlareKit.Show(_controls, !shot);
            FlareKit.Show(_grid, !shot && grid);
        }

        public void Flash(float strength)
        {
            Color c = _flash.color;
            float a = Mathf.Clamp01(strength) * 0.85f;
            if (Mathf.Abs(c.a - a) > 0.001f)
            {
                _flash.color = new Color(1f, 1f, 1f, a);
                _flash.raycastTarget = false;
            }
        }

        /// <summary>A line in the middle, its strength 0 to 1; null for none.</summary>
        public void Toast(string words, float strength)
        {
            bool on = words != null && strength > 0.01f;
            FlareKit.Show(_toastBack, on);
            if (!on)
            {
                return;
            }
            FlareKit.Set(_toast, words);
            float a = Mathf.Clamp01(strength);
            _toastBack.color = new Color(Pill.r, Pill.g, Pill.b, Pill.a * a);
            _toast.alpha = a;
        }

        public void Busy(bool busy)
        {
            _disc.color = busy ? new Color(0.7f, 0.7f, 0.7f, 1f) : Color.white;
        }
    }

    /// <summary>
    /// The clear surface under the viewfinder's controls: it adds up the drag and the wheel for the camera to take each
    /// frame, and keeps them from the world behind it. No Update; no cost a frame of its own.
    /// </summary>
    internal sealed class ViewfinderInput : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("photos", "ViewfinderInput", typeof(ViewfinderInput));

        private float _x, _y, _wheel;

        /// <summary>The drag in pixels and the wheel in notches since the last take, right and up positive.</summary>
        public void Take(out float x, out float y, out float wheel)
        {
            x = _x;
            y = _y;
            wheel = _wheel;
            _x = _y = _wheel = 0f;
        }

        public void OnBeginDrag(PointerEventData data)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                _x = _y = 0f;
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        public void OnDrag(PointerEventData data)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                if (data != null)
                {
                    _x += data.delta.x;
                    _y += data.delta.y;
                }
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        public void OnScroll(PointerEventData data)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                float d = data != null ? data.scrollDelta.y : 0f;
                if (d != 0f)
                {
                    _wheel += Mathf.Clamp(d / Mathf.Max(1f, Mathf.Min(120f, Mathf.Abs(d))), -1f, 1f);   // a notch an event: mice give 1 or 120, a touchpad less
                }
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        private void OnDisable()
        {
            _x = _y = _wheel = 0f;
        }
    }
}
