using System;
using RealLifeOverhaul.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The bug report's card, on a canvas above the menus. With the interface or its switch off, or the fonts not in,
    /// the game's own box is used. Cost: nothing until a report is asked for.
    /// </summary>
    public static class BugReportBox
    {
        private const int Order = 32100;

        private static GameObject _root;
        private static UiHost _host;
        private static BugReportScreen _screen;

        /// <summary>Shows the card. False when the panel should ask instead.</summary>
        public static bool TryShow(string where, long bytes)
        {
            if (!UiDirector.On || !UiScreenSwitches.On(BugReportScreen.ScreenId) || !UiFonts.Ready)
            {
                return false;
            }
            try
            {
                Bind();
                _screen.Prepare(where, bytes);
                _root.SetActive(true);
                _host.Show();
                return true;
            }
            catch (Exception e)
            {
                UiDirector.Logger?.Error(e);
                return false;
            }
        }

        private static void Close()
        {
            if (_host != null)
            {
                _host.Hide();
                _root.SetActive(false);
            }
        }

        private static void Bind()
        {
            if (_root != null)
            {
                return;
            }
            _root = new GameObject("RealLife Interface Report", typeof(RectTransform));
            UnityEngine.Object.DontDestroyOnLoad(_root);
            Canvas canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Order;
            CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(3840f, 2160f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            _root.AddComponent<GraphicRaycaster>();
            Image blocker = _root.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0f);
            _screen = (BugReportScreen)BugReportScreen.Make();
            _screen.Closing = Close;
            _host = _root.AddComponent<UiHost>();
            _host.Bind(_screen, UiGame.OverBoxes, UiGame.OverBoxes, UiDirector.Dark, UiDirector.Motion, UiFrame.MastheadHeight, UiFrame.BandHeight, null);
            _host.View.Scale = UiTokens.CanvasPerPx;
            _host.Failed = e => Close();
            _root.SetActive(false);
        }
    }
}
