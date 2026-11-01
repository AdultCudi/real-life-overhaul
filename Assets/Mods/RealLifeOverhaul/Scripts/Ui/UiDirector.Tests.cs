using System;
using UI;
using UI.Notification;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The RLO interface's two tests (Options, Tests): the gallery of every component in both looks, drawn over
    /// whatever shows (Back closes it), and the phone's Contacts opened on the kit. Each says in a notice why it cannot
    /// run when it cannot (the switch off, the fonts or the phone not there yet).
    /// </summary>
    public sealed partial class UiDirector
    {
        private GameObject _gallery;
        private UiHost _galleryHost;

        /// <summary>Options, Tests: the kit's gallery over the screen.</summary>
        public void TestGallery()
        {
            try
            {
                if (!On)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_off", null, 8f);
                    return;
                }
                if (!UiFonts.Ready)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_not_ready", null, 8f);
                    return;
                }
                if (_gallery == null)
                {
                    BuildGallery();
                }
                _gallery.SetActive(true);
                _galleryHost.Show();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the phone's Contacts, which the kit draws while the switch is on.</summary>
        public void TestContacts()
        {
            try
            {
                if (!On)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_off", null, 8f);
                    return;
                }
                ContactsKit.Install();
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (uis == null || uis.smartphoneUI == null || !ContactsKitReady())
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_not_ready", null, 8f);
                    return;
                }
                uis.smartphoneUI.OpenApp("Contacts");
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private static bool ContactsKitReady()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null && uis.fullMenu != null && uis.fullMenu.contactsApp != null;
        }

        // A canvas of its own over every other, scaled as the game's (3840 x 2160, expanded), the gallery's host on it.
        private void BuildGallery()
        {
            _gallery = new GameObject("RealLife Interface Gallery", typeof(RectTransform));
            DontDestroyOnLoad(_gallery);
            var canvas = _gallery.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = _gallery.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(3840f, 2160f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            _gallery.AddComponent<GraphicRaycaster>();
            var screen = new UiGalleryScreen(UiText.Words, UiText.Money);
            screen.Close = () =>
            {
                _galleryHost.Hide();
                _gallery.SetActive(false);
            };
            _galleryHost = _gallery.AddComponent<UiHost>();
            _galleryHost.Bind(screen, UiGame.Instance, UiGame.Instance, Dark, Motion, UiFrame.FullMenuTop, UiFrame.FullMenuBand, UiText.T("reallife_ui_gallery_title"));
            _galleryHost.View.Scale = UiTokens.CanvasPerPx;
        }
    }
}
