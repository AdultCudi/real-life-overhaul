using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using RealLifeOverhaul.GameUi;
using UI.Load;
using Unity.Collections;
using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Draws the faces not on disk yet through the game's portrait camera (Phone/Contacts/PortraitTurn), only when the
    /// player feels nothing (<see cref="Now"/>). Cost: nothing while there is nothing to draw (it switches itself off); two
    /// comparisons a frame while faces wait; a face is the game's render and read-back, then a worker writes it.
    /// </summary>
    internal sealed class FlareFacePainter : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("flare", "FlareFacePainter", typeof(FlareFacePainter));   // its Update (Core/Guard)

        private const int WantedMost = 64;           // faces asked for by cards in view waiting at most; one past it is asked again when its card is bound again
        private const int SourceMost = 256 * 256 * 4; // bytes: the largest face the game gives, 256 square, ARGB32 at most

        private struct Job
        {
            public FlareAvatarPlan Plan;
            public string Picture;   // FlareAvatarPlan.Picture
            public string File;      // its round picture's file
            public bool Wanted;      // asked for by a card in view, else drawn ahead behind the loading screen or a black screen only
            public bool Load;        // one of the faces asked for behind the loading screen
        }

        /// <summary>A face made (main thread): its picture key and its round picture's RGBA rows (valid only during the call).</summary>
        public static Action<string, byte[]> Painted;

        /// <summary>A face that cannot be had (one of the staff no longer there, the camera refusing): its key.</summary>
        public static Action<string> Missed;

        /// <summary>Whether the Flare app is showing (FlareApp sets it): faces in view are drawn only then.</summary>
        public static bool AppShowing;

        private static FlareFacePainter _instance;
        private static readonly List<Job> Waiting = new List<Job>(WantedMost + FlareFaceAheadRules.AheadMost);
        private static readonly HashSet<string> Queued = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> OnDisk = new HashSet<string>(StringComparer.OrdinalIgnoreCase);   // faces' file names known to be there: the load's listing, and each written since
        private static readonly List<FlareAvatarPlan> Plans = new List<FlareAvatarPlan>(FlareFaceAheadRules.AheadMost);
        private static readonly List<FlareAvatarPlan> ToDraw = new List<FlareAvatarPlan>(FlareFaceAheadRules.AheadMost);
        private static int _wanted, _ahead;
        private static byte[] _source;               // a face as the game gives it, for the worker: one for the session
        private static readonly byte[] Disc = new byte[FlareAvatarRules.Size * FlareAvatarRules.Size * 4];
        private static readonly byte[] Colour = new byte[FlareAvatarRules.Size * FlareAvatarRules.Size * 3];

        // The faces behind the loading screen: where it stands, what came of it.
        private static int _loadStage;               // 0 not begun, 1 the folder being listed, 2 drawing, 3 over
        private static bool _loadOn, _loadStop;
        private static int _loadAsked, _loadDrawn, _loadDisk, _loadMissed;
        private static double _loadMs, _loadMainMs;  // the faces' cost from asking to their file, and the main thread's share
        private static Task<List<FlareFaceFile>> _listing;
        private static string _listed;               // the folder listed

        private Job _job;
        private bool _busy, _rendering;
        private float _since, _rest;
        private long _askedAt;
        private double _mainMs;
        private Action<Sprite> _drawn;
        private Texture2D _decode;                   // a contacts' JPEG decoded into it: one for the session

        /// <summary>A face's round picture's file: by its look, or for one of the staff by the save and the name.</summary>
        public static string FileOf(FlareAvatarPlan plan)
        {
            return Path.Combine(FlarePictureGame.FaceFolder, FlareFaceAheadRules.FileName(plan, FlarePictureGame.SaveId));
        }

        /// <summary>A face to draw, one in view first; one ahead only behind the loading screen or a black screen. Never added twice.</summary>
        public static void Want(FlareAvatarPlan plan, string picture, string file, bool wanted)
        {
            Want(plan, picture, file, wanted, false);
        }

        private static void Want(FlareAvatarPlan plan, string picture, string file, bool wanted, bool load)
        {
            if (picture == null || file == null)
            {
                return;
            }
            if (Queued.Contains(picture))
            {
                if (wanted)
                {
                    for (int i = 0; i < Waiting.Count; i++)
                    {
                        if (Waiting[i].Picture == picture && !Waiting[i].Wanted)
                        {
                            Job j = Waiting[i];
                            j.Wanted = true;
                            Waiting[i] = j;
                            _wanted++;
                            _ahead--;
                        }
                    }
                }
                Wake();
                return;
            }
            if (wanted ? _wanted >= WantedMost : _ahead >= FlareFaceAheadRules.AheadMost)
            {
                return;   // in view: asked again when its card is bound again; ahead: the list is made again each game day
            }
            Waiting.Add(new Job { Plan = plan, Picture = picture, File = file, Wanted = wanted, Load = load });
            Queued.Add(picture);
            _wanted += wanted ? 1 : 0;
            _ahead += wanted ? 0 : 1;
            Wake();
        }

        /// <summary>The likely faces not known to be on disk, drawn behind the black screens; when Flare starts and each game day.</summary>
        public static void Ahead(FlareAvatarFacts facts, int day)
        {
            FlareFaceAheadRules.Likely(facts, day, FlareFaceAheadRules.AheadMost, Plans);
            FlareFaceAheadRules.Split(Plans, OnDisk, FlarePictureGame.SaveId, ToDraw);
            for (int i = 0; i < ToDraw.Count; i++)
            {
                Want(ToDraw[i], ToDraw[i].Picture, FileOf(ToDraw[i]), false, false);
            }
        }

        /// <summary>A city is loading: the faces' piece handed to Core/LoadHold.</summary>
        public static void HoldAtLoad()
        {
            _loadStage = 0;
            _loadOn = false;
            LoadHold.Add("flare: the faces likely to be seen first", LoadStep, LoadEnd);
        }

        /// <summary>The faces behind the loading screen, a step a frame while Core/LoadHold holds it; true when there is nothing more to do now.</summary>
        public static bool LoadStep()
        {
            switch (_loadStage)
            {
                case 0:
                    LoadReset();   // once a load: HoldAtLoad set the stage
                    GameInstance save = SaveGameManager.Current;
                    if (!Settings.SocialMedia || !Settings.FlarePictures || save == null)
                    {
                        _loadStage = 3;
                        return true;
                    }
                    string folder = FlarePictureGame.FaceFolder;
                    _listed = folder;
                    _listing = Task.Run(() => List(folder));
                    _loadStage = 1;
                    return false;
                case 1:
                    if (!_listing.IsCompleted)
                    {
                        return false;
                    }
                    return LoadAsk();
                case 2:
                    return _loadStop || (!_loadOn) || LoadLeft() == 0;
                default:
                    return true;
            }
        }

        // The folder listed: the save read as it is, the likely faces asked for, the clean-up started.
        private static bool LoadAsk()
        {
            List<FlareFaceFile> files = _listing.Status == TaskStatus.RanToCompletion ? _listing.Result : null;
            OnDisk.Clear();
            for (int i = 0; files != null && i < files.Count; i++)
            {
                OnDisk.Add(files[i].Name);
            }
            GameInstance save = SaveGameManager.Current;
            FlareFile file = save != null ? FlareStore.Peek(save.modData) : null;
            var people = new List<FlarePerson>(12);
            if (save != null)
            {
                FlareBridges.SavedPeople?.Invoke(save.modData, people);
            }
            var facts = new FlareAvatarFacts { File = file, People = people, CitySeed = file != null ? FlareLiveRules.CitySeed(file) : 0, MyBusinesses = new List<string>(0) };
            FlareFaceAheadRules.Likely(facts, save != null ? save.Day : 0, FlareFaceAheadRules.LoadMost, Plans);
            string saveId = FlarePictureGame.SaveId;
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Plans.Count; i++)
            {
                keep.Add(FlareFaceAheadRules.FileName(Plans[i], saveId));
            }
            _loadDisk = FlareFaceAheadRules.Split(Plans, OnDisk, saveId, ToDraw);   // those drawn before, by any save: read when their card is shown
            for (int i = 0; i < ToDraw.Count; i++)
            {
                Want(ToDraw[i], ToDraw[i].Picture, FileOf(ToDraw[i]), false, true);
                _loadAsked++;
            }
            if (files != null && files.Count > FlareFaceAheadRules.DiskMost)
            {
                var gone = new List<string>(files.Count - FlareFaceAheadRules.DiskKeep);
                FlareFaceAheadRules.Evict(files, keep, gone);   // a sort of some 2,000 names: a millisecond, behind the loading screen
                for (int i = 0; i < gone.Count; i++)
                {
                    OnDisk.Remove(gone[i]);
                }
                string folder = _listed;
                int had = files.Count;
                Task.Run(() => Clean(folder, had, gone));   // the files deleted on a worker
            }
            _loadOn = _loadAsked > 0;
            _loadStage = 2;
            return !_loadOn;
        }

        /// <summary>The loading screen goes (Core/LoadHold): what was drawn behind it is said, the faces left wait for the black screens.</summary>
        public static void LoadEnd(bool done)
        {
            int left = LoadLeft();
            bool was = _loadStage >= 2;
            _loadOn = false;
            _loadStage = 3;
            for (int i = 0; i < Waiting.Count; i++)
            {
                if (Waiting[i].Load)
                {
                    Job j = Waiting[i];
                    j.Load = false;   // an ordinary face ahead now, for the next black screen
                    Waiting[i] = j;
                }
            }
            if (was && Meta.Dev.Verbose)
            {
                double each = FlareFaceAheadRules.Each(_loadMs, _loadDrawn);
                FlareDirector.Logger?.Info("Flare check (faces at load): " + _loadDrawn + " drawn in " + _loadMs.ToString("0") + " ms (" + each.ToString("0.0", CultureInfo.InvariantCulture)
                                           + " ms a face, "
                                           + FlareFaceAheadRules.Each(_loadMainMs, _loadDrawn).ToString("0.0", CultureInfo.InvariantCulture) + " ms of it the main thread's), " + _loadDisk
                                           + " from disk, "
                                           + left + " left for later (the black screens, or Flare open)" + (_loadMissed > 0 ? ", " + _loadMissed + " not to be had" : string.Empty)
                                           + "; the loading screen held " + LoadHold.Spent.ToString("0") + " ms of at most " + FlareFaceAheadRules.LoadBudgetMs.ToString("0")
                                           + (done ? "" : " (the time was up)") + ": PASS");
            }
        }

        private static int LoadLeft()
        {
            int left = 0;
            for (int i = 0; i < Waiting.Count; i++)
            {
                left += Waiting[i].Load ? 1 : 0;
            }
            return left + (_instance != null && _instance._busy && _instance._job.Load ? 1 : 0);
        }

        private static void LoadReset()
        {
            _loadOn = _loadStop = false;
            _loadAsked = _loadDrawn = _loadDisk = _loadMissed = 0;
            _loadMs = _loadMainMs = 0.0;
            _listing = null;
        }

        // On a worker: the faces' files in the folder and when each was last used.
        private static List<FlareFaceFile> List(string folder)
        {
            var found = new List<FlareFaceFile>(256);
            if (!Directory.Exists(folder))
            {
                return found;
            }
            string[] files = Directory.GetFiles(folder, FlareAvatarRules.FilePrefix + "*" + FlareAvatarRules.FileExtension);
            for (int i = 0; i < files.Length; i++)
            {
                found.Add(new FlareFaceFile { Name = Path.GetFileName(files[i]), Touched = File.GetLastWriteTimeUtc(files[i]).Ticks });
            }
            return found;
        }

        // On a worker: the faces used longest ago deleted, down to the folder's bound (never one this load wants).
        private static void Clean(string folder, int had, List<string> gone)
        {
            int deleted = 0;
            for (int i = 0; i < gone.Count; i++)
            {
                try
                {
                    File.Delete(Path.Combine(folder, gone[i]));
                    deleted++;
                }
                catch (IOException)
                {
                    // in use, or gone meanwhile: looked at again at the next load
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            if (Meta.Dev.Verbose)
            {
                FlareDirector.Logger?.Info("Flare check (faces on disk): " + had + " kept, past the most of " + FlareFaceAheadRules.DiskMost + "; " + deleted
                                           + " used longest ago deleted, down to " + (had - deleted) + " (" + FlareFaceAheadRules.DiskKeep + " wanted): "
                                           + (had - deleted <= FlareFaceAheadRules.DiskKeep ? "PASS" : "FAIL (some could not be deleted)"));
            }
        }

        /// <summary>Flare stops: nothing more is drawn; a face on its way is still kept on disk.</summary>
        public static void Forget()
        {
            Waiting.Clear();
            Queued.Clear();
            _wanted = 0;
            _ahead = 0;
        }

        /// <summary>For the diagnostics and the test: the faces waiting, in view and known on disk.</summary>
        public static string Report()
        {
            return Waiting.Count + " face(s) waiting, " + _wanted + " in view, " + OnDisk.Count + " known on disk";
        }

        private static void Wake()
        {
            if (_instance == null)
            {
                var host = new GameObject("RealLife FlareFacePainter");
                DontDestroyOnLoad(host);
                host.hideFlags = HideFlags.HideAndDontSave;
                _instance = host.AddComponent<FlareFacePainter>();
            }
            _instance.enabled = true;
        }

        /// <summary>
        /// When a face may be drawn now: behind the held loading screen, the black screen at full strength (never its fades,
        /// the picture showing through), or with Flare open for the faces in view (FlareFaceMoment).
        /// </summary>
        private static FaceMoment Now()
        {
            bool fading = UI.UiFader.isFading;
            return FlareFaceMoment.Of(LoadHold.Holding, _loadOn && !_loadStop, LoadScene.isLoading, fading, fading && SceneFinds.Covered, AppShowing, _wanted > 0);
        }

        private void Awake()
        {
            _drawn = sprite => UiClone.Safe(() => Drawn(sprite));
        }

        private static readonly int HotUpdate = HotSpots.Slot("Social: FlareFacePainter.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // its feature was switched off after its faults (Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float now = Time.realtimeSinceStartup;
            if (_busy)
            {
                if (_rendering && now - _since > PortraitTurn.Timeout)
                {
                    TimedOut(now);
                }
                return;
            }
            if (Waiting.Count == 0)
            {
                enabled = false;   // nothing to draw: no cost at all until a face is wanted
                return;
            }
            FaceMoment moment = Now();
            if (moment == FaceMoment.None || !PortraitTurn.Free(now))
            {
                return;   // two comparisons a frame while faces wait for their moment
            }
            if (moment == FaceMoment.Load && !FlareFaceAheadRules.Another(LoadHold.Spent, _loadMs, _loadDrawn, FlareFaceAheadRules.LoadBudgetMs))
            {
                _loadStop = true;   // the time left would not hold another face: the rest wait for the black screens
                return;
            }
            int at = -1;
            for (int i = 0; i < Waiting.Count && at < 0; i++)
            {
                at = Waiting[i].Wanted ? i : at;
            }
            if (at < 0 && moment == FaceMoment.App)
            {
                return;   // with the app open only the faces in view
            }
            if (at < 0)
            {
                at = 0;   // behind the loading screen or a black screen: the faces drawn ahead, most likely first
            }
            _job = Waiting[at];
            Waiting.RemoveAt(at);
            Queued.Remove(_job.Picture);
            _wanted -= _job.Wanted ? 1 : 0;
            _ahead -= _job.Wanted ? 0 : 1;
            _rest = moment == FaceMoment.Load ? PortraitTurn.LoadPaceSeconds : moment == FaceMoment.Black ? PortraitTurn.HiddenPaceSeconds : PortraitTurn.PaceSeconds;
            Draw(now, moment);
        }

        // Its log line in a method of its own: the frame's path builds no text (RLO-PERF-014).
        private void TimedOut(float now)
        {
            FlareDirector.Logger?.Warn("Flare: the game's portrait camera did not answer for a face in " + PortraitTurn.Timeout + " s; its initials stay until the app asks again.");
            PortraitTurn.Give(this, now, _rest);
            _busy = _rendering = false;
            Missed?.Invoke(_job.Picture);
        }

        private void Draw(float now, FaceMoment moment)
        {
            if (File.Exists(_job.File))
            {
                OnDisk.Add(Path.GetFileName(_job.File));
                _loadDisk += _job.Load ? 1 : 0;
                Painted?.Invoke(_job.Picture, null);   // made meanwhile, by another save or the contacts: read from disk
                return;
            }
            _askedAt = Stopwatch.GetTimestamp();
            _mainMs = 0.0;
            if (_job.Plan.Kind == FlareAvatarKind.Face)
            {
                string jpeg = Path.Combine(FlarePictureGame.FaceFolder, ContactCatalogue.CacheName(_job.Plan.Female, _job.Plan.Age, _job.Plan.Seed));
                if (File.Exists(jpeg) && FromJpeg(jpeg))
                {
                    return;
                }
            }
            if (!PortraitTurn.Ready || !PortraitTurn.Take(this, now))
            {
                Want(_job.Plan, _job.Picture, _job.File, _job.Wanted, _job.Load);   // the camera is not there yet: asked again later
                if (_job.Load)
                {
                    _loadStop = true;   // behind the loading screen the camera is not there: the rest wait for the black screens
                }
                return;
            }
            CharacterData staff = _job.Plan.Kind == FlareAvatarKind.Staff ? FlarePictureGame.Staff(_job.Plan.Name) : null;
            if (_job.Plan.Kind == FlareAvatarKind.Staff && staff == null)
            {
                PortraitTurn.Give(this, now, _rest);
                _loadMissed += _job.Load ? 1 : 0;
                Missed?.Invoke(_job.Picture);   // no longer one of the staff: the initials stay
                return;
            }
            _busy = _rendering = true;
            _since = now;
            var watch = Stopwatch.StartNew();
            bool asked;
            using (SessionWatch.Piece("flare: a face drawn by the game's portrait camera"))   // once a look: a render and a read-back
            {
                asked = staff != null ? PortraitTurn.Character(staff, _drawn) : PortraitTurn.Face(_job.Plan.Female, _job.Plan.Age, _job.Plan.Seed, _drawn);
            }
            watch.Stop();
            _mainMs = watch.Elapsed.TotalMilliseconds;
            if (!asked)
            {
                _busy = _rendering = false;
                PortraitTurn.Give(this, now, _rest);
                Want(_job.Plan, _job.Picture, _job.File, _job.Wanted, _job.Load);
                _loadStop |= _job.Load;
                return;
            }
            if (Meta.Dev.Verbose && moment != FaceMoment.Load)
            {
                // Read again after the render: a face behind the black screen is a pass only if nothing showed through.
                bool unseen = moment != FaceMoment.Black || SceneFinds.Black;
                FlareDirector.Logger?.Info("Flare check (faces): a face asked of the game's portrait camera in " + _mainMs.ToString("0.0", CultureInfo.InvariantCulture)
                                           + " ms of the main thread (" + (moment == FaceMoment.Black ? "behind the black screen at full strength" : "the app open") + ", wanted under "
                                           + PortraitTurn.Timeout + " s a face); " + Report() + (unseen ? ": PASS" : ": FAIL (the picture showed through the fade)"));
            }
        }

        // The game's face came back: its pixels copied for the worker, the game's sprite and texture let go, the turn given back.
        private void Drawn(Sprite sprite)
        {
            Texture2D texture = sprite != null ? sprite.texture : null;
            PortraitTurn.Give(this, Time.realtimeSinceStartup, _rest);
            if (!_busy || !_rendering)
            {
                Let(sprite, texture);   // late: the time-out let it go
                return;
            }
            _rendering = false;
            var watch = Stopwatch.StartNew();
            int bytes;
            bool argb;
            bool copied = Copy(texture, out bytes, out argb);
            int w = copied ? texture.width : 0, h = copied ? texture.height : 0;
            Let(sprite, texture);
            _mainMs += watch.Elapsed.TotalMilliseconds;
            if (!copied)
            {
                _busy = false;
                _loadMissed += _job.Load ? 1 : 0;
                Missed?.Invoke(_job.Picture);
                return;
            }
            StartCoroutine(Guard.Routine(Finish(_job, w, h, bytes, argb)));
        }

        // A JPEG of the contacts' of the same look: decoded here (about a millisecond), made round on the worker.
        private bool FromJpeg(string file)
        {
            var watch = Stopwatch.StartNew();
            byte[] data = File.ReadAllBytes(file);
            if (_decode == null)
            {
                _decode = new Texture2D(2, 2, TextureFormat.RGB24, false) { name = "RealLife Flare face", hideFlags = HideFlags.HideAndDontSave };
            }
            int bytes;
            bool argb;
            if (!_decode.LoadImage(data, false) || !Copy(_decode, out bytes, out argb))
            {
                return false;
            }
            _mainMs = watch.Elapsed.TotalMilliseconds;
            _busy = true;
            StartCoroutine(Guard.Routine(Finish(_job, _decode.width, _decode.height, bytes, argb)));
            return true;
        }

        // A readable texture's pixels into the session's buffer, as the engine keeps them (RGB24, RGBA32 or ARGB32).
        private static bool Copy(Texture2D texture, out int bytes, out bool argb)
        {
            bytes = 0;
            argb = false;
            if (texture == null || !texture.isReadable || texture.width <= 0 || texture.height <= 0)
            {
                return false;
            }
            switch (texture.format)
            {
                case TextureFormat.RGB24: bytes = 3; break;
                case TextureFormat.RGBA32: bytes = 4; break;
                case TextureFormat.ARGB32: bytes = 4; argb = true; break;
                default: return false;
            }
            int size = texture.width * texture.height * bytes;
            if (size > SourceMost)
            {
                return false;
            }
            _source ??= new byte[SourceMost];
            NativeArray<byte> pixels = texture.GetPixelData<byte>(0);
            if (pixels.Length < size)
            {
                return false;
            }
            NativeArray<byte>.Copy(pixels, _source, size);
            return true;
        }

        private static void Let(Sprite sprite, Texture2D texture)
        {
            if (sprite != null)
            {
                Destroy(sprite);
            }
            if (texture != null)
            {
                Destroy(texture);
            }
        }

        // On the worker: the round picture made and written; back here, handed to the app and counted for the load's line.
        private IEnumerator Finish(Job job, int w, int h, int bytes, bool argb)
        {
            Task<bool> work = Task.Run(() =>
            {
                if (!FlareAvatarRules.Disc(_source, w, h, bytes, argb, Disc))
                {
                    return false;
                }
                FlareAvatarRules.ColourOf(Disc, Colour);
                PhotoThumbs.Write(job.File, Colour, FlareAvatarRules.Size, FlareAvatarRules.Size);
                return true;
            });
            while (!work.IsCompleted)
            {
                yield return null;
            }
            _busy = false;
            bool made = work.Status == TaskStatus.RanToCompletion && work.Result;
            if (job.Load && made)
            {
                _loadDrawn++;
                _loadMs += (Stopwatch.GetTimestamp() - _askedAt) * 1000.0 / Stopwatch.Frequency;
                _loadMainMs += _mainMs;
            }
            if (made)
            {
                OnDisk.Add(Path.GetFileName(job.File));
                Painted?.Invoke(job.Picture, Disc);
            }
            else
            {
                _loadMissed += job.Load ? 1 : 0;
                Missed?.Invoke(job.Picture);
            }
        }

        private void OnDestroy()
        {
            if (_decode != null)
            {
                Destroy(_decode);
            }
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
