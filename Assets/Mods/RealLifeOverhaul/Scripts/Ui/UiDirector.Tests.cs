using System;
using UI;
using UI.Notification;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The RLO interface's tests (Options, Tests): the gallery of every component in both looks, the phone's Contacts on
    /// the kit, the city's people and a firm's page, each drawn over whatever shows (Back closes it).
    /// Each says in its notice exactly why it cannot run when it cannot (the switch off, the fonts still loading, the
    /// city's people still settling, the phone not built yet), never a bare "not ready". The firm's
    /// test opens a sample studio, named as one, on a save that owns no tech firm, so the page can be seen on day one.
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
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_fonts", null, 8f);
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

        /// <summary>Options, Tests: the population's views (the city, then where its rows lead) over the screen.</summary>
        public void TestPopulation()
        {
            try
            {
                if (!On)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_off", null, 8f);
                    return;
                }
                if (!UiFonts.Ready || !Population.CityPopulation.Running)
                {
                    Notifications.Show(NotificationType.Warning, !UiFonts.Ready ? "reallife_ui_test_fonts" : "reallife_ui_test_city", null, 8f);
                    return;
                }
                var screen = new PopulationScreen(PopulationLive.Source());
                Overlay(screen, UiText.T("reallife_ui_page_population"), host => screen.Close = () =>
                {
                    host.Hide();
                    host.gameObject.SetActive(false);
                    Destroy(host.gameObject);
                }, UiIcon.People);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // A canvas of its own over every other, scaled as the game's, with a host for one screen on it.
        internal UiHost Overlay(IUiScreen screen, string title, Action<UiHost> closing, UiIcon icon = UiIcon.None)
        {
            var go = new GameObject("RealLife Interface " + screen.Id, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(3840f, 2160f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            UiRoots.Take(go);
            var blocker = go.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0f);   // takes the clicks the game's own screen under it would otherwise get
            var host = go.AddComponent<UiHost>();
            host.Failed = e => Destroy(go);   // a page that fails goes, with the clear picture that took the clicks; whoever opened it may give its own way out
            host.Bind(screen, UiGame.Instance, UiGame.Instance, Dark, Motion, UiFrame.MastheadHeight, UiFrame.BandHeight, title, icon);
            host.View.Scale = UiTokens.CanvasPerPx;
            host.StepAside = true;   // a box of the game's over this canvas (the desk card's question) would sit under order 32000 and take no click
            closing(host);
            host.Show();
            return host;
        }

        /// <summary>
        /// Options, Tests: the first of the player's tech firms (the studio's page, or another firm's) over the screen, or a
        /// sample studio named as one when the player owns none yet.
        /// </summary>
        public void TestFirm()
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
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_fonts", null, 8f);
                    return;
                }
                Tech.TechDirector d = Tech.TechDirector.Instance;
                string place = d != null && d.FirmPlaces.Count > 0 ? d.FirmPlaces[0].File.Place : null;
                var screen = new StudioPageScreen(place);
                Overlay(screen, UiText.T("reallife_ui_page_studio"), host => screen.Close = () => Destroy(host.gameObject), UiIcon.GameController);
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
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_phone", null, 8f);
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
            UiRoots.Take(_gallery);
            var screen = new UiGalleryScreen(UiText.Words, UiText.Money);
            screen.Close = () =>
            {
                _galleryHost.Hide();
                _gallery.SetActive(false);
            };
            _galleryHost = _gallery.AddComponent<UiHost>();
            _galleryHost.Bind(screen, UiGame.Instance, UiGame.Instance, Dark, Motion, UiFrame.MastheadHeight, UiFrame.BandHeight, UiText.T("reallife_ui_gallery_title"),
                UiIcon.Grid);
            _galleryHost.View.Scale = UiTokens.CanvasPerPx;
            _galleryHost.Failed = e => screen.Close();
        }
    }
}
