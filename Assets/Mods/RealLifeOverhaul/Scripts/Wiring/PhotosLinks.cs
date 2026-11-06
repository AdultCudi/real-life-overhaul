using System;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Photos;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The Camera and the Gallery (PhotosBridges) with Flare, photo mode's shutter and the phone's wallpaper. Flare's
        /// camera and selfie buttons open the Camera; with it off the selfie falls back to photo mode's.
        /// Nothing per frame: each is asked on a click or a shot.
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
