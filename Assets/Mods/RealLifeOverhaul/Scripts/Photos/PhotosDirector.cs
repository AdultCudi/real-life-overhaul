using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BAModAPI;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// The Camera and the Gallery, kept going: the two apps on the phone (CameraApp, GalleryApp), the save's photos listed
    /// (newest first, from its folder: PhotoStore), the latest photo's small copy for the viewfinder's Gallery button, the
    /// phone's wallpaper from a photo, the look over deleted saves' photos once a game, the question when the Gallery is
    /// full, and the tests. The camera itself is CameraMode, added beside this.
    ///
    /// It has no Update. A one-second timer on the real clock waits for the save, puts the apps on the phone, follows the
    /// save being played (another save, another folder) and the option. Every file is read, written and deleted on a
    /// worker thread (Task.Run), the textures made from what the worker read on the main thread (a small copy is one upload
    /// of 110 KB). PER FRAME: NOTHING, open or shut. Nothing is written into the save.
    ///
    /// Going Public: nothing is sent; the photos and the wallpaper are this computer's own. Sharing goes through Flare,
    /// which sends a photo's small copy alone.
    /// </summary>
    public sealed class PhotosDirector : MonoBehaviour
    {
        public static IModLogger Logger;
        public static PhotosDirector Instance { get; private set; }

        private const float ArmedSeconds = 30f;   // real seconds a test waits for the options page to close

        private readonly List<PhotoEntry> _photos = new List<PhotoEntry>(PhotoStoreRules.Cap + 16);
        private readonly List<PhotoEntry> _offer = new List<PhotoEntry>(PhotoStoreRules.OfferAtOnce);
        private CameraMode _mode;
        private CameraApp _cameraApp;
        private GalleryApp _galleryApp;
        private bool _running, _listed, _swept, _pretendFull, _wallpaperWanted, _wallpaperLoading;
        private string _saveId, _folder, _root;
        private int _generation;               // counts the saves followed: a worker's answer for an earlier one is let go
        private Texture2D _latest;
        private string _latestName;
        private LastWins _wallpaperWrites = new LastWins();   // the wallpaper's line written by workers, the last choice winning (one for each save)
        private string _wallpaper;             // the photo set as the wallpaper, by name, or null
        private Texture _wallpaperTexture;
        private int _armed;                    // a test waiting for the options page to close: 1 the camera, 2 the selfie, 3 the Gallery
        private float _armedUntil;

        /// <summary>True once the save's photos have been listed: the camera can take one.</summary>
        public bool Ready => _running && _listed && _folder != null;

        /// <summary>The save's photos in the Gallery's order (newest first).</summary>
        public List<PhotoEntry> Photos => _photos;

        public int Count => _photos.Count;

        /// <summary>The save's folder of photos.</summary>
        public string Folder => _folder;

        /// <summary>The newest photo's small copy, for the viewfinder's Gallery button, or null.</summary>
        public Texture LatestTexture => _latest;

        /// <summary>The photo set as the phone's wallpaper, by name, or null.</summary>
        public string WallpaperName => _wallpaper;

        private void Awake()
        {
            Instance = this;
            CameraMode.Logger = Logger;
            CameraApp.Logger = Logger;
            GalleryApp.Logger = Logger;
            PhotosGame.Logger = Logger;
            _mode = gameObject.AddComponent<CameraMode>();
            _cameraApp = new CameraApp(this);
            _galleryApp = new GalleryApp(this);
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            try
            {
                StandDown();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private IEnumerator Tick()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return wait;
                Slow();
            }
        }

        private static readonly int HotTick = HotSpots.Slot("Photos: PhotosDirector.Tick");
        // Once a second: start when the save is ready, stand down when the option goes off, follow the save, the apps on the phone.
        private void Slow()
        {
            using var hotSpot = HotSpots.Time(HotTick);
            if (SaveGameManager.Current == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            if (!Settings.PhoneCamera)
            {
                if (_running)
                {
                    StandDown();
                    Logger?.Info("Camera and Gallery: switched off; the apps are off the phone and the phone's wallpaper is its own again. The photos stay on disk.");
                }
                return;
            }
            if (!_running)
            {
                _running = true;
                _root = Path.GetFullPath(PhotosGame.Root);   // one form of the root from here on: the engine's '/' as the game's Mono gives paths back
            }
            string id = PhotosGame.SaveId;
            if (_folder == null || !string.Equals(id, _saveId, StringComparison.Ordinal))
            {
                Follow(id);
            }
            _cameraApp.Install();
            _galleryApp.Install();
            if (!_swept)
            {
                _swept = true;
                Sweep(false);
            }
            if (_wallpaperWanted && !_wallpaperLoading && _wallpaperTexture == null && PhotosBridges.HasWallpaper != null && PhotosBridges.HasWallpaper())
            {
                StartCoroutine(Guard.Routine(LoadWallpaper(_wallpaper, _generation)));
            }
            Armed();
        }

        // Another save (or the first): its folder, its photos listed on a worker, its wallpaper.
        private void Follow(string id)
        {
            _saveId = id;
            _generation++;
            _listed = false;
            _photos.Clear();
            ClearWallpaper();
            DropLatest();
            _wallpaperWrites = new LastWins();
            _folder = PhotoStore.Folder(_root, id);
            StartCoroutine(Guard.Routine(List(_root, id, _folder, _generation)));
        }

        private IEnumerator List(string root, string id, string folder, int generation)
        {
            var found = new List<PhotoEntry>(PhotoStoreRules.Cap + 16);
            string wallpaper = null;
            bool adopted = false;
            Task<bool> work = Task.Run(() =>
            {
                adopted = PhotoStore.Adopt(root, id);   // (an earlier build's folder of this save, moved to its name now)
                bool read = PhotoStore.List(folder, found);
                wallpaper = PhotoStore.Wallpaper(folder);
                return read;
            });
            while (!work.IsCompleted)
            {
                yield return null;
            }
            if (generation != _generation)
            {
                yield break;   // another save meanwhile
            }
            if (work.Status != TaskStatus.RanToCompletion || !work.Result)
            {
                Logger?.Warn("Gallery: the folder of this save's photos could not be read (" + folder + "); the Gallery shows none for now.");
            }
            if (adopted)
            {
                Logger?.Info("Gallery: this save's photos were moved from the folder an earlier build named to " + Path.GetFileName(folder) + ".");
            }
            _photos.Clear();
            _photos.AddRange(found);
            _listed = true;
            _wallpaper = wallpaper;
            _wallpaperWanted = wallpaper != null;
            Logger?.Info("Gallery: " + _photos.Count + " photo(s) of this save" + (wallpaper != null ? ", one of them the phone's wallpaper" : string.Empty) + ".");
            StartCoroutine(Guard.Routine(LoadLatest(generation)));
            _galleryApp.Changed();
        }

        // ---------------------------------------------------------------- standing down

        private void StandDown()
        {
            _running = false;
            _mode?.Close("the Camera and Gallery switched off");
            _cameraApp?.Remove();
            _galleryApp?.Remove();
            ClearWallpaper();
            DropLatest();
            _photos.Clear();
            _listed = false;
            _folder = null;
            _saveId = null;
            _generation++;
            _armed = 0;
        }

        /// <summary>For the options page's clean-up: the camera shut and the wallpaper the phone's own (the photos stay on disk).</summary>
        public void StopAll()
        {
            _mode?.Close("the save tools");
            ClearWallpaper();
        }

        // ---------------------------------------------------------------- the camera

        /// <summary>
        /// The camera asked for (the Camera app, Flare's camera button, a test): the phone put away and the camera opened as the
        /// rear camera or the selfie. Says why not in the game's notice. False when the Camera is off, or when it is asked for
        /// from Flare where Flare's own picture still works and the Camera's does not (CameraRules.FlareTakesOver: a vehicle,
        /// the bus, the game holding the player...): Flare then takes its own, with no word from the Camera. True when it
        /// opened, will open, or was refused with a word.
        /// </summary>
        public bool OpenCamera(bool selfie, bool fromFlare)
        {
            if (!Settings.PhoneCamera || !_running)
            {
                return false;
            }
            if (CameraMode.Active)
            {
                return true;
            }
            CameraRefusal refusal = CameraRules.Refuse(PhotosGame.Situation(false));
            if (refusal != CameraRefusal.None)
            {
                if (fromFlare && CameraRules.FlareTakesOver(refusal, selfie))
                {
                    Logger?.Info("Camera: not now (" + refusal + "); Flare takes its own " + (selfie ? "selfie" : "picture") + ".");
                    return false;
                }
                Refused(refusal);
                return true;
            }
            if (!Ready)
            {
                PhotosGame.Tell(NotificationType.Info, "reallife_phonecam_wait");
                return true;
            }
            int offer = _pretendFull ? Math.Min(PhotoStoreRules.OfferAtOnce, _photos.Count) : PhotoStoreRules.ToOffer(_photos.Count);
            _pretendFull = false;
            if (offer > 0)
            {
                AskRoom(offer, selfie, fromFlare);
            }
            else if (PhotoStoreRules.Full(_photos.Count))
            {
                PhotosGame.Tell(NotificationType.Warning, "reallife_phonecam_full_none", "cap", PhotosText.N(PhotoStoreRules.Cap));
            }
            else
            {
                StartCoroutine(Guard.Routine(OpenWhenPutAway(selfie, fromFlare)));
            }
            return true;
        }

        private void Refused(CameraRefusal refusal)
        {
            string key = CameraRules.RefusalKey(refusal);
            if (key != null)
            {
                PhotosGame.Tell(NotificationType.Warning, key);
            }
            Logger?.Info("Camera: not now (" + refusal + ").");
        }

        // The phone put away with the game's own close, and the camera opened once it is (its fade is a tenth of a second).
        private IEnumerator OpenWhenPutAway(bool selfie, bool fromFlare)
        {
            PhotosGame.CloseMenu();
            float until = Time.unscaledTime + 0.25f;
            while (Time.unscaledTime < until || PhotosGame.MenuOpen)
            {
                if (Time.unscaledTime > until + 2f)
                {
                    yield break;   // the phone was opened again meanwhile: the player is doing something else
                }
                yield return null;
            }
            CameraRefusal refusal = _mode != null ? _mode.Open(selfie, fromFlare) : CameraRefusal.Gone;
            if (refusal != CameraRefusal.None)
            {
                Refused(refusal);
            }
        }

        // The Gallery is full: the oldest offered for deletion with the game's question box. Never deleted without a yes.
        private void AskRoom(int offer, bool selfie, bool fromFlare)
        {
            PhotoStoreRules.Oldest(_photos, offer, _wallpaper, _offer);
            if (_offer.Count == 0)
            {
                PhotosGame.Tell(NotificationType.Warning, "reallife_phonecam_full_none", "cap", PhotosText.N(PhotoStoreRules.Cap));
                return;
            }
            var names = new List<string>(_offer.Count);
            for (int i = 0; i < _offer.Count; i++)
            {
                names.Add(_offer[i].Name);
            }
            string oldest = PhotosText.Taken(_offer[0]);
            GameConfirm.Show("reallife_gallery_full_title".Localize(new { count = PhotosText.N(_photos.Count), cap = PhotosText.N(PhotoStoreRules.Cap) }),
                "reallife_gallery_full_body".Localize(new { count = PhotosText.N(names.Count), oldest }),
                () => UiClone.Safe(() => StartCoroutine(Guard.Routine(DeleteThenOpen(names, selfie, fromFlare)))),
                () => UiClone.Safe(() => PhotosGame.Tell(NotificationType.Info, "reallife_gallery_full_kept")),
                "reallife_gallery_full_yes", "reallife_gallery_full_no", true, true);
        }

        private IEnumerator DeleteThenOpen(List<string> names, bool selfie, bool fromFlare)
        {
            int generation = _generation;
            yield return StartCoroutine(Guard.Routine(Delete(names, generation)));
            if (generation == _generation && !PhotoStoreRules.Full(_photos.Count))
            {
                yield return StartCoroutine(Guard.Routine(OpenWhenPutAway(selfie, fromFlare)));
            }
        }

        /// <summary>The Gallery app opened (the viewfinder's Gallery button, a test).</summary>
        public void OpenGallery()
        {
            _galleryApp.Open();
        }

        // ---------------------------------------------------------------- the photos

        /// <summary>
        /// A photo the camera has just written into <paramref name="folder"/> (the folder the shot took from <see cref="Folder"/>):
        /// first in the list, its small copy the latest. Returns that texture. The folder is told by the one the shot took, never
        /// by taking the photo's path apart (the game's Mono gives '' where the engine's root has '/').
        /// </summary>
        internal Texture Added(string folder, string path, PhotosGame.Developed made)
        {
            var entry = new PhotoEntry();
            if (path == null || !PhotoStoreRules.Parse(Path.GetFileName(path), entry) || !PhotoStoreRules.SameFolder(folder, _folder))
            {
                return _latest;   // (a photo of a save no longer followed)
            }
            _photos.Insert(0, entry);
            PhotoStoreRules.Order(_photos);
            DropLatest();
            _latest = PhotosGame.SmallTexture(made.Small, made.SmallWidth, made.SmallHeight);
            _latestName = entry.Name;
            _galleryApp.Changed();
            _cameraApp.Changed();
            return _latest;
        }

        /// <summary>Photos deleted with their small copies, on a worker; the list, the latest and the wallpaper follow. The Gallery is told.</summary>
        public IEnumerator Delete(List<string> names, int generation)
        {
            string folder = _folder;
            var gone = new List<string>(names.Count);
            Task work = Task.Run(() =>
            {
                for (int i = 0; i < names.Count; i++)
                {
                    if (PhotoStore.Delete(folder, names[i]))
                    {
                        gone.Add(names[i]);
                    }
                }
            });
            while (!work.IsCompleted)
            {
                yield return null;
            }
            if (generation != _generation)
            {
                yield break;
            }
            bool latest = false, wallpaper = false;
            for (int i = _photos.Count - 1; i >= 0; i--)
            {
                if (gone.Contains(_photos[i].Name))
                {
                    latest |= _photos[i].Name == _latestName;
                    wallpaper |= _photos[i].Name == _wallpaper;
                    _photos.RemoveAt(i);
                }
            }
            if (wallpaper)
            {
                SetWallpaper(null);
            }
            if (latest)
            {
                DropLatest();
                StartCoroutine(Guard.Routine(LoadLatest(generation)));
            }
            _cameraApp.Changed();
            Logger?.Info("Gallery: " + gone.Count + " of " + names.Count + " photo(s) deleted, with their small copies; " + _photos.Count + " left.");
            if (gone.Count < names.Count)
            {
                PhotosGame.Tell(NotificationType.Warning, "reallife_gallery_note_delete_failed");
            }
            _galleryApp.Changed();
        }

        /// <summary>A generation token for a worker's answer: an answer for an earlier save is let go.</summary>
        public int Generation => _generation;

        // The newest photo's small copy, read on a worker and made a texture.
        private IEnumerator LoadLatest(int generation)
        {
            if (_photos.Count == 0)
            {
                yield break;
            }
            string name = _photos[0].Name;
            string path = Path.Combine(_folder, name);
            int w = 0, h = 0;
            Task<byte[]> work = Task.Run(() => PhotoThumbs.Get(path, out w, out h, out bool _));
            while (!work.IsCompleted)
            {
                yield return null;
            }
            if (generation != _generation || _latest != null || work.Status != TaskStatus.RanToCompletion || work.Result == null)
            {
                yield break;
            }
            _latest = PhotosGame.SmallTexture(work.Result, w, h);
            _latestName = name;
            _cameraApp.Changed();
        }

        private void DropLatest()
        {
            PhotosGame.Destroy(_latest);
            _latest = null;
            _latestName = null;
        }

        // ---------------------------------------------------------------- the wallpaper

        /// <summary>Whether the phone has a wallpaper the Gallery can set now (the modern phone look on).</summary>
        public static bool CanSetWallpaper => PhotosBridges.HasWallpaper != null && PhotosBridges.HasWallpaper();

        /// <summary>A photo set as the phone's wallpaper (null: the phone's own again), written down for this save and shown.</summary>
        public void SetWallpaper(string name)
        {
            string folder = _folder;
            _wallpaper = name;
            _wallpaperWanted = name != null;
            LastWins writes = _wallpaperWrites;
            int ticket = writes.Next();
            Task.Run(() => writes.Run(ticket, () => PhotoStore.SetWallpaper(folder, name)));   // (two quick changes: the later is the one written)
            ClearShownWallpaper();
            if (name != null)
            {
                StartCoroutine(Guard.Routine(LoadWallpaper(name, _generation)));
            }
        }

        private IEnumerator LoadWallpaper(string name, int generation)
        {
            if (name == null || _folder == null)
            {
                yield break;
            }
            _wallpaperLoading = true;
            try
            {
                Texture2D loaded = null;
                yield return StartCoroutine(Guard.Routine(PhotosGame.Load(Path.Combine(_folder, name), t => loaded = t)));
                if (generation != _generation || name != _wallpaper || !_running)
                {
                    PhotosGame.Destroy(loaded);
                    yield break;
                }
                if (loaded == null)
                {
                    _wallpaperWanted = false;
                    yield break;
                }
                ClearShownWallpaper();
                if (PhotosBridges.Wallpaper != null && PhotosBridges.Wallpaper(loaded))
                {
                    _wallpaperTexture = loaded;
                    Logger?.Info("Gallery: the phone's wallpaper is " + name + ".");
                }
                else
                {
                    PhotosGame.Destroy(loaded);   // (the phone's look is off: tried again when it is on)
                }
            }
            finally
            {
                _wallpaperLoading = false;
            }
        }

        // The phone's own wallpaper back, and the photo's texture let go (what is written for the save stays).
        private void ClearShownWallpaper()
        {
            if (_wallpaperTexture != null)
            {
                PhotosBridges.Wallpaper?.Invoke(null);
                PhotosGame.Destroy(_wallpaperTexture);
                _wallpaperTexture = null;
            }
        }

        private void ClearWallpaper()
        {
            ClearShownWallpaper();
            _wallpaper = null;
            _wallpaperWanted = false;
        }

        // ---------------------------------------------------------------- deleted saves

        // Every save's photos looked over on a worker (PhotoStore.Sweep); 'say' tells what was found in a notice (the test).
        private void Sweep(bool say)
        {
            string root = _root ?? PhotosGame.Root;
            string saves = PhotosGame.SaveGames;
            string playing = _saveId;
            var deleted = new List<string>(4);
            var marked = new List<string>(4);
            DateTime today = DateTime.Today;
            StartCoroutine(Guard.Routine(Swept(Task.Run(() => PhotoStore.Sweep(root, playing, id => PhotoStore.SavePresent(saves, id), today, deleted, marked)), deleted, marked, say)));
        }

        private IEnumerator Swept(Task<int> work, List<string> deleted, List<string> marked, bool say)
        {
            while (!work.IsCompleted)
            {
                yield return null;
            }
            int looked = work.Status == TaskStatus.RanToCompletion ? work.Result : 0;
            for (int i = 0; i < deleted.Count; i++)
            {
                Logger?.Info("Gallery: the photos of a save deleted " + PhotoStoreRules.GraceDays + " days or more ago are deleted (" + deleted[i] + ").");
            }
            for (int i = 0; i < marked.Count; i++)
            {
                Logger?.Info("Gallery: the save of the photos in " + marked[i] + " is gone; they are deleted in " + PhotoStoreRules.GraceDays + " days unless it comes back.");
            }
            if (say)
            {
                PhotosGame.Tell(NotificationType.Info, "reallife_gallery_test_sweep_done", "looked", looked.ToString(), "marked", marked.Count.ToString(), "deleted", deleted.Count.ToString(),
                    "days", PhotoStoreRules.GraceDays.ToString());
            }
        }

        // ---------------------------------------------------------------- tests

        /// <summary>Options > Tests: the camera opens (the rear camera, or the selfie) as soon as the options page is closed.</summary>
        public void TestOpen(bool selfie)
        {
            if (!Settings.PhoneCamera)
            {
                TestRefusals.Tell(Logger, "Camera", TestRefusal.FeatureOff);
                return;
            }
            if (!_running || !PhotosGame.Loaded)
            {
                TestRefusals.Tell(Logger, "Camera", TestRefusal.NotLoaded);
                return;
            }
            Arm(selfie ? 2 : 1);
        }

        /// <summary>Options > Tests: the Gallery opens as soon as the options page is closed.</summary>
        public void TestGallery()
        {
            if (!Settings.PhoneCamera)
            {
                TestRefusals.Tell(Logger, "Gallery", TestRefusal.FeatureOff);
                return;
            }
            if (!_running || !PhotosGame.Loaded)
            {
                TestRefusals.Tell(Logger, "Gallery", TestRefusal.NotLoaded);
                return;
            }
            Arm(3);
        }

        /// <summary>Options > Tests: the next opening of the camera is asked as if the Gallery were full (the oldest offered, up to ten of those there are).</summary>
        public void TestFull()
        {
            if (!Settings.PhoneCamera || !_running)
            {
                TestRefusals.Tell(Logger, "Camera", Settings.PhoneCamera ? TestRefusal.NotLoaded : TestRefusal.FeatureOff);
                return;
            }
            if (_photos.Count == 0)
            {
                PhotosGame.Tell(NotificationType.Warning, "reallife_gallery_test_full_none");
                return;
            }
            _pretendFull = true;
            PhotosGame.Tell(NotificationType.Info, "reallife_gallery_test_full_armed", "count", PhotosText.N(Math.Min(PhotoStoreRules.OfferAtOnce, _photos.Count)));
        }

        /// <summary>Options > Tests: the look over deleted saves' photos now, and what it found in a notice.</summary>
        public void TestSweep()
        {
            if (!Settings.PhoneCamera || !_running)
            {
                TestRefusals.Tell(Logger, "Gallery", Settings.PhoneCamera ? TestRefusal.NotLoaded : TestRefusal.FeatureOff);
                return;
            }
            Sweep(true);
        }

        private void Arm(int what)
        {
            _armed = what;
            _armedUntil = Time.unscaledTime + ArmedSeconds;
            PhotosGame.Tell(NotificationType.Info, "reallife_phonecam_test_armed");
        }

        // A test's opening, once the options page is closed (within half a minute).
        private void Armed()
        {
            if (_armed == 0)
            {
                return;
            }
            if (Time.unscaledTime > _armedUntil)
            {
                _armed = 0;
                return;
            }
            if (ThirdPersonCamera.IsUiOpen())
            {
                return;
            }
            int what = _armed;
            _armed = 0;
            if (what == 3)
            {
                OpenGallery();
            }
            else
            {
                OpenCamera(what == 2, false);
            }
        }
    }
}
