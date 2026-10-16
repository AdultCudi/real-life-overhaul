using System;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Photos;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The Camera and the Gallery (Photos/PhotosBridges) with the rest of the mod, and Flare with the Camera:
        ///
        ///   ShareToFlare   a photo of the Gallery's into Flare's compose sheet: its full path and its small copy where Flare
        ///                  reads it (FlareDirector.SharePhoto(photo, PhotoThumbs.FileOf(photo)); Flare sends only the file's
        ///                  name and, in a session, the small copy)
        ///   FlareRunning   whether Flare runs, for the Gallery's "Share to Flare"
        ///   Shutter        photo mode's shutter (Leisure/Sound)
        ///   Wallpaper      the phone's wallpaper from a photo (Phone/PhoneSkin, while its modern look is on)
        ///   OpenCamera     Flare's camera button opens the Camera, its first photo back in the compose sheet; Flare's selfie
        ///                  button the Camera's selfie (photo mode's selfie as before when the Camera is off)
        ///
        /// COST: nothing per frame; each is asked on a click or a shot.
        /// </summary>
        public static class PhotosLinks
        {
            private static Action _selfieBefore;

            internal static void Connect()
            {
                PhotosBridges.ShareToFlare = (photo, small) => FlareDirector.Instance != null && FlareDirector.Instance.SharePhoto(photo, small);
                PhotosBridges.FlareRunning = () => FlareDirector.Instance != null && FlareDirector.Instance.Running;
                PhotosBridges.Shutter = () => Leisure.LeisureSound.Cue(Leisure.LeisureCue.Shutter);
                PhotosBridges.Wallpaper = picture => PhoneSkin.Instance != null && PhoneSkin.Instance.Wallpaper(picture);
                PhotosBridges.HasWallpaper = () => PhoneSkin.Instance != null && PhoneSkin.Instance.Applied;
                FlareBridges.OpenCamera = () => PhotosDirector.Instance != null && PhotosDirector.Instance.OpenCamera(false, true);
                FlareBridges.PictureFolder = () => PhotoStore.Folder(System.IO.Path.GetFullPath(PhotosGame.Root), PhotosGame.SaveId);
                FlareBridges.MarkPictureFolder = () => PhotoStore.MarkSave(PhotoStore.Folder(System.IO.Path.GetFullPath(PhotosGame.Root), PhotosGame.SaveId), PhotosGame.SaveId);
                _selfieBefore = FlareBridges.TakeSelfie;
                Action before = _selfieBefore;
                FlareBridges.TakeSelfie = () =>
                {
                    if (PhotosDirector.Instance == null || !PhotosDirector.Instance.OpenCamera(true, true))
                    {
                        before?.Invoke();
                    }
                };
            }

            internal static void Disconnect()
            {
                PhotosBridges.Reset();
                FlareBridges.OpenCamera = null;
                FlareBridges.PictureFolder = null;
                FlareBridges.MarkPictureFolder = null;
                FlareBridges.TakeSelfie = _selfieBefore;
                _selfieBefore = null;
            }
        }
    }
}
