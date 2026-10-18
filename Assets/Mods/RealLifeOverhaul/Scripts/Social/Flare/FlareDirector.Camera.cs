using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare's director, pictures into Flare: the camera button's shot of the screen without the phone, the selfie button,
    /// and <see cref="SharePhoto"/> for another feature's photo. Cost: one read of the screen on the press (a few
    /// milliseconds); the shrinking runs on a worker thread.
    /// </summary>
    public sealed partial class FlareDirector
    {
        private const float ShotWait = 0.35f;   // real seconds after the phone is put away (its fade is a tenth of a second)
        private bool _shooting;

        // The camera button's shot. Not while the game's question box is up: the box would be in the picture.
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

        // Whatever happens after the read of the screen, its copy is let go and the camera is free again.
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
                    FlareLiveGame.Drop(left);   // Shrink lets it go itself; a second drop does nothing
                }
            }
        }

        public bool Shooting => _shooting;

        /// <summary>
        /// The door for another feature's photo (the Gallery's "Share to Flare"): the compose sheet opens with it as a moment;
        /// <paramref name="smallCopyPath"/>, if any, a Core/PhotoThumbs small copy copied off the main thread. False while not running or with no path.
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
                            // made from the photo when it is first shown, as any other
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
