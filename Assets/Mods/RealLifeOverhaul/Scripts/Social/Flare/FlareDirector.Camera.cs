using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Pictures into Flare: the camera button (a shot of the screen as it is, without the phone), the selfie button (photo
    /// mode as a selfie, its picture back in the compose sheet), and <see cref="SharePhoto"/>, the one door for a photo of
    /// another feature's (the Gallery's "Share to Flare"): a photo's full path and, if it has one, its small copy.
    ///
    /// A shot: the phone is put away with the game's own close, a third of a second later at the end of a frame the
    /// screen is read (the engine's copy, outside the managed heap), the phone opens again on the compose sheet at once,
    /// and on a worker thread the screen is made small (256 pixels wide, Core/PhotoThumbs' format) and written beside the
    /// photos ("Shots/Thumbs"). Only the small copy is kept: nothing full-size is ever written. COST: one read of the
    /// screen on the main thread (a few milliseconds, on the press); the rest off it.
    /// </summary>
    public sealed partial class FlareDirector
    {
        private const float ShotWait = 0.35f;   // real seconds after the phone is put away (its fade is a tenth of a second)
        private bool _shooting;

        // The camera button's shot (CameraPressed is the one line that chooses what the button does). Not while the game's
        // question box is up: putting the phone away would leave the box in the picture, so the shot is refused with a word.
        private void TakeShot()
        {
            if (_shooting || !Running || _app == null)
            {
                return;
            }
            if (GameConfirm.IsOpen)
            {
                Tell(NotificationType.Warning, "reallife_flare_note_shot_busy", null);
                return;
            }
            _shooting = true;
            StartCoroutine(Guard.Routine(Shoot()));
        }

        // Whatever happens from the read of the screen on (a fault, Flare stopping), the screen's copy is let go and the
        // camera is free again: a finally around it all (on the worker when the worker still has it).
        private IEnumerator Shoot()
        {
            FlareLiveGame.Shot shot = null;
            Task<bool> work = null;
            try
            {
                FlareLiveGame.CloseMenu();
                float until = Time.unscaledTime + ShotWait;
                while (Time.unscaledTime < until)
                {
                    yield return null;
                }
                yield return new WaitForEndOfFrame();
                shot = FlareLiveGame.Capture();
                if (_app != null && Running)
                {
                    _app.Open();   // back to the compose sheet, which says the picture is on its way
                }
                if (shot == null)
                {
                    Tell(NotificationType.Warning, "reallife_flare_note_shot_failed", null);
                    yield break;
                }
                string path = Path.Combine(FlarePictures.Folder, "RealLifeOverhaul_Flare_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                FlareLiveGame.Shot taken = shot;
                work = Task.Run(() =>
                {
                    int w, h;
                    byte[] rows = FlareLiveGame.Shrink(taken, PhotoThumbs.Width, 1024, out w, out h);
                    if (rows == null)
                    {
                        return false;
                    }
                    PhotoThumbs.Write(PhotoThumbs.FileOf(path), rows, w, h);
                    return System.IO.File.Exists(PhotoThumbs.FileOf(path));
                });
                while (!work.IsCompleted)
                {
                    yield return null;
                }
                bool made = work.Status == TaskStatus.RanToCompletion && work.Result;
                if (!made || !Running)
                {
                    Tell(NotificationType.Warning, "reallife_flare_note_shot_failed", null);
                    yield break;
                }
                Logger?.Info("Flare: a shot of the screen for a post (" + Path.GetFileName(path) + ", its small copy only).");
                _view?.Attach("f:" + path);
            }
            finally
            {
                _shooting = false;
                FlareLiveGame.Shot left = shot;
                if (work != null && !work.IsCompleted)
                {
                    work.ContinueWith(_ => FlareLiveGame.Drop(left));
                }
                else
                {
                    FlareLiveGame.Drop(left);   // (Shrink lets it go itself; a second drop does nothing)
                }
            }
        }

        /// <summary>True while the camera is taking a shot (the compose sheet says so).</summary>
        public bool Shooting => _shooting;

        /// <summary>
        /// THE DOOR FOR A PHOTO OF ANOTHER FEATURE'S (the Gallery's "Share to Flare"). Flare opens on its compose sheet with
        /// the photo attached, as a moment ("a picture") the player can write about and post.
        ///
        ///   photoPath      the photo's full path on disk: a PNG (any size; the engine's own capture) or a JPG
        ///   smallCopyPath  its small copy, or null: Core/PhotoThumbs' format ("RLOT", the width and the height as
        ///                  little-endian 32-bit integers, then RGB24 rows from the bottom up), 256 pixels wide
        ///                  (PhotoThumbs.Width), any height. Flare reads it where PhotoThumbs.FileOf(photoPath) says
        ///                  (the photo's folder, "Thumbs", the photo's file name and ".thumb"); one given elsewhere is
        ///                  copied there once, off the main thread. With none, Flare makes it from a PNG off the main
        ///                  thread (PhotoThumbs.Get) the first time it is shown, or the engine draws a JPG small.
        ///
        /// False, with nothing done, while Flare is not running or the path is empty. Called on a click, never per frame.
        /// </summary>
        public bool SharePhoto(string photoPath, string smallCopyPath = null)
        {
            if (!Running || Save == null || string.IsNullOrEmpty(photoPath))
            {
                return false;
            }
            try
            {
                string kept = PhotoThumbs.FileOf(photoPath);
                if (!string.IsNullOrEmpty(smallCopyPath) && !string.Equals(Path.GetFullPath(smallCopyPath), Path.GetFullPath(kept), StringComparison.OrdinalIgnoreCase))
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            if (System.IO.File.Exists(smallCopyPath) && !System.IO.File.Exists(kept))
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(kept) ?? ".");
                                System.IO.File.Copy(smallCopyPath, kept, false);
                            }
                        }
                        catch (Exception)
                        {
                            // (made from the photo when it is first shown, as for any other)
                        }
                    });
                }
                BuildingRegistration inside = FlareGame.Inside();
                FlareDraft d = FlareRules.Draft(_file, FlareSource.Photo, Save.Day, Save.Hour, -1, inside != null ? FlareGame.Place(inside) : null, photoPath, 0);
                if (d == null)
                {
                    for (int i = 0; i < _file.Drafts.Count && d == null; i++)
                    {
                        d = _file.Drafts[i].Name == photoPath ? _file.Drafts[i] : null;
                    }
                }
                Made(d);
                if (d != null && _view != null && _app != null)
                {
                    _view.WantCompose(d.Id);
                    _app.Open();
                }
                Logger?.Info("Flare: a photo is shared to Flare from another feature (" + Path.GetFileName(photoPath) + ").");
                return d != null;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        // A picture saved in photo mode after the selfie button: back in the compose sheet.
        private void SelfieBack(FlareDraft d)
        {
            if (d == null || _awaitSelfie < 0f || Time.unscaledTime > _awaitSelfie || (d.Source != FlareSource.Selfie && d.Source != FlareSource.Photo))
            {
                return;
            }
            _awaitSelfie = -1f;
            if (_view != null)
            {
                _view.WantCompose(d.Id);
            }
            Tell(NotificationType.Info, "reallife_flare_note_selfie_ready", _openCompose);
        }
    }
}
