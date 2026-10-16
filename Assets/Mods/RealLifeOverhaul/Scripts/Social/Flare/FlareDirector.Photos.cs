using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The city's photos (FlareCityPhotoRules says which posts, when and where; FlareCityCamera takes them) and the faces
    /// drawn ahead (FlareFacePainter):
    ///
    ///   the folder     the save's photos' folder (FlareBridges.PictureFolder: Photos/(the save)), its "Flare" in it, marked
    ///                  as the save's with the first photo so a deleted save's go with its photos
    ///   which are there  listed on a worker when Flare starts and each game day, the photos of posts that are gone deleted
    ///                  there too (FlareCityPhotoRules.Stale); nothing is in the save
    ///   the next photo once a real second (the director's timer) after the pace: the newest post due, wanted and not tried,
    ///                  if its place can be seen from where the player is; one at a time, at most a dozen a game day
    ///   a post's picture  the app asks <see cref="CityPhotoKey"/>: the photo's small copy, read as any picture of the player's
    ///
    /// In Going Public each machine takes its own photos of the shared posts (the same posts carry one everywhere: the roll
    /// is the city's seed and the post's session number), from where it stands; nothing is sent.
    ///
    /// COST: a comparison a second while it waits for the pace; then a look along the posts (two hundred at most) every
    /// pace (twenty real seconds at least); a photo as FlareCityCamera says. A list of the folder on a worker once a day.
    /// </summary>
    public sealed partial class FlareDirector
    {
        private readonly HashSet<int> _photoHave = new HashSet<int>();     // posts with a photo on disk
        private readonly HashSet<int> _photoTried = new HashSet<int>();    // posts tried this session (taken or not), never tried again
        private string _photoFolder;
        private float _photoNext, _photoPace = FlareCityPhotoRules.PaceLeast;
        private int _photoDay = -1, _photoToday, _photoGeneration;
        private bool _photoBusy, _photoMarked;

        /// <summary>The key of a post's photo of the city for the app (as the player's: "f:" and its path), or null for none.</summary>
        public string CityPhotoKey(FlarePost p)
        {
            if (p == null || _photoFolder == null || Settings.FlareCityPhotos <= 0 || !_photoHave.Contains(p.Id))
            {
                return null;
            }
            return "f:" + Path.Combine(_photoFolder, FlareCityPhotoRules.FileName(p.Id));
        }

        // Flare starts (or the save changes): the folder, which photos are there, the stale ones deleted; the faces drawn ahead.
        private void PhotosStart()
        {
            _photoHave.Clear();
            _photoTried.Clear();
            _photoToday = 0;
            _photoDay = -1;
            _photoMarked = false;
            _photoGeneration++;
            string root = FlareBridges.Ask(FlareBridges.PictureFolder, null);
            _photoFolder = root != null ? Path.Combine(root, FlareCityPhotoRules.Folder) : null;
            PhotosLook();
            if (Settings.FlarePictures)
            {
                FlareFacePainter.Prewarm(FlareLiveRules.CitySeed(_file), _people);
            }
        }

        private void PhotosStop()
        {
            _photoGeneration++;
            _photoHave.Clear();
            _photoTried.Clear();
            _photoFolder = null;
            _photoBusy = false;
            FlareCityCamera.Clear();
            FlareFacePainter.Forget();
        }

        // On a worker: the photos there are, and those of posts that are gone (or beyond the most kept) deleted.
        private void PhotosLook()
        {
            if (_photoFolder == null || _file == null)
            {
                return;
            }
            var ids = new List<int>(_file.Posts.Count);
            for (int i = 0; i < _file.Posts.Count; i++)
            {
                ids.Add(_file.Posts[i].Id);
            }
            StartCoroutine(Guard.Routine(PhotosList(_photoFolder, ids, _photoGeneration)));
        }

        private IEnumerator PhotosList(string folder, List<int> posts, int generation)
        {
            var have = new List<int>(64);
            var gone = new List<int>(16);
            Task<bool> work = Task.Run(() =>
            {
                string thumbs = Path.Combine(folder, "Thumbs");
                if (!Directory.Exists(thumbs))
                {
                    return true;
                }
                string[] files = Directory.GetFiles(thumbs, FlareCityPhotoRules.Prefix + "*");
                var found = new List<int>(files.Length);
                for (int i = 0; i < files.Length; i++)
                {
                    int id = FlareCityPhotoRules.IdOf(Path.GetFileName(files[i]));
                    if (id >= 0)
                    {
                        found.Add(id);
                    }
                }
                var kept = new FlareFile();
                for (int i = 0; i < posts.Count; i++)
                {
                    kept.Posts.Add(new FlarePost { Id = posts[i] });
                }
                FlareCityPhotoRules.Stale(found, kept, gone);
                for (int i = 0; i < gone.Count; i++)
                {
                    try
                    {
                        System.IO.File.Delete(PhotoThumbs.FileOf(Path.Combine(folder, FlareCityPhotoRules.FileName(gone[i]))));
                    }
                    catch (IOException)
                    {
                        // (deleted at the next look)
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
                for (int i = 0; i < found.Count; i++)
                {
                    if (!gone.Contains(found[i]))
                    {
                        have.Add(found[i]);
                    }
                }
                return true;
            });
            while (!work.IsCompleted)
            {
                yield return null;
            }
            if (generation != _photoGeneration || work.Status != TaskStatus.RanToCompletion)
            {
                yield break;
            }
            for (int i = 0; i < have.Count; i++)
            {
                _photoHave.Add(have[i]);
            }
            for (int i = 0; i < gone.Count; i++)
            {
                _photoHave.Remove(gone[i]);
            }
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Flare check (city photos): " + have.Count + " photo(s) of the city's posts kept for this save, " + gone.Count + " of posts that are gone deleted (at most "
                             + FlareCityPhotoRules.FilesMost + "): PASS");
            }
            if (have.Count > 0)
            {
                _view?.Changed();
            }
        }

        // Once a real second (the director's timer): the next photo, if its time has come.
        private void PhotosSecond()
        {
            float now = Time.realtimeSinceStartup;
            if (now < _photoNext || _photoBusy || Settings.FlareCityPhotos <= 0 || _photoFolder == null || Save == null || LoadScene.isLoading)
            {
                return;
            }
            _photoNext = now + _photoPace;
            if (_photoDay != Save.Day)
            {
                _photoDay = Save.Day;
                _photoToday = 0;
            }
            if (_photoToday >= FlareCityPhotoRules.DayMost)
            {
                return;
            }
            FlarePost p = FlareCityPhotoRules.Next(_file, FlareLiveRules.CitySeed(_file), Settings.FlareCityPhotos, Now, _photoTried);
            if (p == null)
            {
                return;
            }
            FlarePhotoSubject subject = FlareCityPhotoRules.Subject(p);
            if (subject != FlarePhotoSubject.Front && !FlareCityPhotoRules.SameDistrict(p, FlareBridges.Ask(FlareBridges.PlayerDistrict, null)))
            {
                _photoTried.Add(p.Id);   // (a street of another district is not there to be seen from here)
                return;
            }
            _photoTried.Add(p.Id);
            _photoToday++;
            StartCoroutine(Guard.Routine(PhotoTake(p.Id, subject, p.Place, FlareCommentRules.Seed(p, FlareLiveRules.CitySeed(_file)), _photoGeneration, false)));
        }

        private IEnumerator PhotoTake(int postId, FlarePhotoSubject subject, string place, int seed, int generation, bool test)
        {
            _photoBusy = true;
            string folder = _photoFolder;
            var result = new FlareCityCamera.Result();
            try
            {
                if (!_photoMarked)
                {
                    _photoMarked = true;
                    Action marking = FlareBridges.MarkPictureFolder;
                    Task mark = Task.Run(() => marking?.Invoke());   // (the save's id beside its photos: a deleted save's photos go with it)
                    while (!mark.IsCompleted)
                    {
                        yield return null;
                    }
                }
                string thumb = PhotoThumbs.FileOf(Path.Combine(folder, FlareCityPhotoRules.FileName(postId)));
                IEnumerator take = FlareCityCamera.Take(subject, place, Math.Abs(seed), thumb, result);
                while (take.MoveNext())
                {
                    yield return take.Current;
                }
            }
            finally
            {
                _photoBusy = false;   // (on every path: a fault in the camera leaves the next photo free to come)
            }
            if (generation != _photoGeneration)
            {
                yield break;
            }
            _photoPace = FlareCityPhotoRules.NextPace(_photoPace, result.RenderMs);
            if (result.Written)
            {
                _photoHave.Add(postId);
                _view?.Changed();
            }
            if (Meta.Dev.Verbose || test)
            {
                Logger?.Info("Flare check (city photo): post " + postId + ", " + subject.ToString().ToLowerInvariant() + (result.Written ? ", taken" : ", not taken: " + result.Why)
                             + "; the renders took " + result.RenderMs.ToString("0.0") + " ms of the main thread (wanted under " + FlareCityPhotoRules.SlowMs + " ms), next in "
                             + _photoPace.ToString("0") + " s; " + _photoToday + " of " + FlareCityPhotoRules.DayMost + " today: " + (result.Written || result.Why != null ? "PASS" : "FAIL"));
            }
            if (test)
            {
                Tell(result.Written ? NotificationType.Info : NotificationType.Warning, result.Written ? "reallife_flare_test_photo_taken" : "reallife_flare_test_photo_not", _openFeedTab,
                    "ms", result.RenderMs.ToString("0"));   // (why not, in the log line above: a reason in the mod's own words)
            }
        }

        /// <summary>
        /// Test: a photo of the city now, of the newest post of the city's people about the street (its photo taken again,
        /// whatever the option's share and the pace), and every face of the city's people drawn ahead at the next black
        /// screen. A notice says what came of it; the log has the measured time.
        /// </summary>
        public void TestPictures()
        {
            Guarded(() =>
            {
                if (!TestReady("reallife_flare_test_pictures"))
                {
                    return;
                }
                FlareFacePainter.Prewarm(FlareLiveRules.CitySeed(_file), _people);
                Logger?.Info("Flare test (pictures): " + FlareFacePainter.Report() + "; " + _photoHave.Count + " city photo(s) on disk; the folder " + (_photoFolder ?? "(none)") + ".");
                if (_photoBusy || _photoFolder == null)
                {
                    Tell(NotificationType.Warning, "reallife_flare_test_photo_busy", null);
                    return;
                }
                FlarePost pick = null;
                for (int i = _file.Posts.Count - 1; i >= 0 && pick == null; i--)
                {
                    FlarePost p = _file.Posts[i];
                    FlarePhotoSubject s = FlareCityPhotoRules.Subject(p);
                    pick = (s == FlarePhotoSubject.Street || s == FlarePhotoSubject.Sky) && p.At <= Now ? p : null;
                }
                if (pick == null)
                {
                    Tell(NotificationType.Warning, "reallife_flare_test_photo_none", null);
                    return;
                }
                _photoTried.Add(pick.Id);
                StartCoroutine(Guard.Routine(PhotoTake(pick.Id, FlareCityPhotoRules.Subject(pick), pick.Place, FlareCommentRules.Seed(pick, FlareLiveRules.CitySeed(_file)), _photoGeneration, true)));
            });
        }
    }
}
