using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// The Camera and the Gallery in the mod's feature list, and the figures their Guide page prints: every {name} in its
    /// text is filled from the rules here (PhotoStoreRules, CameraRules), so the page can never say anything the apps do not
    /// do. Called once, when the feature list is built (Meta/FeatureList); each figure is worked out when the page is opened.
    /// </summary>
    public static class PhotosGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("photos", "reallife_meta_title_photos")
                .Group("reallife_phonecam_header")
                .About("reallife_guide_photos_about").Help("reallife_guide_photos")
                .Owns(Settings.PhoneCameraId, Settings.PhoneCameraGridId)
                .Objects("RealLifeOverhaul Camera View", "RealLifeOverhaul Camera Grade")
                .Test("reallife_phonecam_test_open", "reallife_phonecam_test_selfie", "reallife_gallery_test_open", "reallife_gallery_test_full", "reallife_gallery_test_sweep")
                .Fact("photos_cap", () => PhotoStoreRules.Cap.ToString(Inv))
                .Fact("photos_offer", () => PhotoStoreRules.OfferAtOnce.ToString(Inv))
                .Fact("photos_grace", () => PhotoStoreRules.GraceDays.ToString(Inv))
                .Fact("photos_lines", () => Photos.PhotoPixels.PhotoHeight.ToString(Inv))
                .Fact("photos_small", () => PhotoThumbs.Width.ToString(Inv))
                .Fact("photos_zoom", () => CameraRules.ZoomMost.ToString("0", Inv))
                .Cleanup(() => PhotosDirector.Instance?.StopAll());
        }
    }

    /// <summary>The Camera and the Gallery's place on the options page, under the mod's own namespace so that it is one line there.</summary>
    public static class PhotosOptions
    {
        /// <summary>The "Camera and Gallery" block of the options page: its two switches and the test buttons.</summary>
        public static ModOptions AddPhotos(this ModOptions options)
        {
            return options
                .AddHeader("reallife_phonecam_header")
                .AddToggle(Settings.PhoneCameraId, "reallife_phonecam_enabled", Settings.PhoneCamera,
                    v => Settings.PhoneCamera = v)
                .AddToggle(Settings.PhoneCameraGridId, "reallife_phonecam_opt_grid", Settings.PhoneCameraGrid,
                    v => Settings.PhoneCameraGrid = v)
                .AddButton("reallife_phonecam_test_open", () => PhotosDirector.Instance?.TestOpen(false))
                .AddButton("reallife_phonecam_test_selfie", () => PhotosDirector.Instance?.TestOpen(true))
                .AddButton("reallife_gallery_test_open", () => PhotosDirector.Instance?.TestGallery())
                .AddButton("reallife_gallery_test_full", () => PhotosDirector.Instance?.TestFull())
                .AddButton("reallife_gallery_test_sweep", () => PhotosDirector.Instance?.TestSweep())
                .AddSplitter();
        }
    }
}
