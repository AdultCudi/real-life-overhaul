using System;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The viewfinder's kit chrome over the camera. No full-screen blocker, so a drag on the picture still reaches the
    /// camera. Hidden while the shot is read, so the bars are not in the photo. Cost: nothing while the camera is shut.
    /// </summary>
    public static class ViewfinderHost
    {
        private static GameObject _root;
        private static UiHost _host;
        private static ViewfinderScreen _screen;
        private static bool _up;

        public static bool Wanted => UiDirector.On && UiFonts.Ready && UiScreenSwitches.On("viewfinder");

        public static bool Show(Action close, Action shoot, Action grid, Action gallery, Action<int> filter, Action<int> frame, Action<int> mode)
        {
            if (!Wanted)
            {
                Hide();
                return false;
            }
            if (_root == null)
            {
                _screen = new ViewfinderScreen();
                _root = new GameObject("RealLife Viewfinder", typeof(RectTransform));
                var canvas = _root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 21;
                var scaler = _root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(3840f, 2160f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1f;
                _root.AddComponent<GraphicRaycaster>();
                _host = _root.AddComponent<UiHost>();
                if (UiGame.Instance == null)
                {
                    UnityEngine.Object.Destroy(_root);
                    _root = null;
                    _host = null;
                    _screen = null;
                    return false;
                }
                _host.Bind(_screen, UiGame.Instance, UiGame.Instance, UiDirector.Dark, UiDirector.Motion, 0f, UiFrame.BandHeight, "", UiIcon.Camera);
                _host.View.Scale = UiTokens.CanvasPerPx;
                _host.Exclusive = false;
            }
            _screen.OnClose = close;
            _screen.OnShutter = shoot;
            _screen.OnGrid = grid;
            _screen.OnOpenGallery = gallery;
            _screen.OnFilter = filter;
            _screen.OnFrame = frame;
            _screen.OnMode = mode;
            _root.SetActive(true);
            _host.Show();
            _up = true;
            return true;
        }

        public static void Hide()
        {
            _up = false;
            if (_host != null)
            {
                _host.Hide();
            }
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        public static void ForShot(bool shot)
        {
            if (_root != null && _up)
            {
                _root.SetActive(!shot);
            }
        }

        public static void Apply(UiFinderModel model)
        {
            if (_screen == null || model == null)
            {
                return;
            }
            _screen.Model.Modes = model.Modes;
            _screen.Model.Mode = model.Mode;
            _screen.Model.Place = model.Place;
            _screen.Model.When = model.When;
            _screen.Model.Count = model.Count;
            _screen.Model.Filters = model.Filters;
            _screen.Model.Filter = model.Filter;
            _screen.Model.Frames = model.Frames;
            _screen.Model.Frame = model.Frame;
            _screen.Model.Grid = model.Grid;
            _screen.Model.Toast = model.Toast;
            _screen.Model.Open = model.Open;
            _screen.Model.Move = model.Move;
            _screen.Model.Select = model.Select;
            _screen.Model.Back = model.Back;
            _screen.Touch();
        }
    }
}
