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
        private static bool _picturesOn;   // the newest photo's key answered among the kit's pictures, added once a session

        private static Sprite _latest;
        private static Texture _latestOf;

        /// <summary>The newest photo's small copy, asked for by the chrome's thumbnail; set by the camera.</summary>
        public static Func<Texture> Newest;

        public static bool Wanted => UiDirector.On && UiFonts.Ready && UiScreenSwitches.On("viewfinder");

        /// <summary>Shows the chrome; false, nothing shown, when the kit's switch is off or the kit is not up.</summary>
        public static bool Show(ViewfinderActions actions)
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
                UiRoots.Take(_root);
                _host = _root.AddComponent<UiHost>();
                _host.Failed = e => Hide();   // the camera goes on with its own frame lines and keys: the chrome only shows them
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
            _screen.Actions = actions;
            if (!_picturesOn)
            {
                // Once a session: it once took the answer before it again at each opening and read it from a field as it
                // answered, so a screen's answer added between two openings closed a ring.
                _picturesOn = true;
                UiPictures.Answer(Find);
            }
            _root.SetActive(true);
            _host.Show();
            _up = true;
            return true;
        }

        public static void Hide()
        {
            _up = false;
            DropLatest();
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
            _screen.Model.Take(model);
            _screen.Touch();
        }

        /// <summary>The picture key of the newest photo on the chrome: answered here, from the camera's small copy of it.</summary>
        public const string LatestKey = "viewfinder:latest";

        // The thumbnail's sprite, cut from the small copy once and made again when the newest photo changes; let go when the chrome hides.
        // Any other key is another part's.
        private static Sprite Find(string key)
        {
            if (key != LatestKey)
            {
                return null;
            }
            Texture texture = Newest != null ? Newest() : null;
            if (texture == null)
            {
                DropLatest();
                return null;
            }
            if (_latest == null || _latestOf != texture)
            {
                DropLatest();
                var t2 = texture as Texture2D;
                if (t2 == null)
                {
                    return null;
                }
                _latest = Sprite.Create(t2, new Rect(0f, 0f, t2.width, t2.height), new Vector2(0.5f, 0.5f), 100f);
                _latest.name = "RealLifeOverhaul Newest Photo";
                _latestOf = texture;
            }
            return _latest;
        }

        private static void DropLatest()
        {
            if (_latest != null)
            {
                UnityEngine.Object.Destroy(_latest);
            }
            _latest = null;
            _latestOf = null;
        }
    }
}
