using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare's director, the city's photos and the faces drawn ahead; each machine of a session keeps its own bank and
    /// picks alike, nothing sent. Cost: a comparison or two a second while nothing is due, a look along the posts a game
    /// minute, a millisecond of fronts a second once a day; file work on a worker.
    /// </summary>
    public sealed partial class FlareDirector
    {
        private const float EventNear = 90f;              // metres from a street event's middle it can be photographed from
        private const double FrontsSliceMs = 1.0;         // milliseconds of the main thread a second the fronts' list may take while it is read
        private const int FrontsMost = 2000;              // fronts held at most; the city has about a thousand businesses
        private const int FrontsNearMost = 24;            // fronts within range handed to the plan at most
        private const int PicksALook = 8;                 // posts picked in one look at most

        private FlareBank _bank;                                          // null until its file is read
        private readonly HashSet<int> _bankFiles = new HashSet<int>();    // the bank's photos with a small copy on disk
        private readonly FlarePhotoScene _scene = new FlarePhotoScene();
        private readonly List<FlareFrontSpot> _fronts = new List<FlareFrontSpot>(1024);
        private readonly List<int> _bankGone = new List<int>(16);
        private readonly HashSet<int> _pickSkip = new HashSet<int>();
        private Func<FlarePost, int> _nicheOf;
        private string _photoFolder;
        private float _photoNext, _photoPace = FlareCityPhotoRules.PaceLeast;
        private int _photoDay = -1, _photoToday, _photoGeneration, _bankDay = -1, _frontsCursor, _frontsDay = -1;
        private long _picksLooked = -1;
        private bool _photoBusy, _photoMarked, _bankDirty, _frontsDone;
        private volatile bool _bankWriting;
        private readonly FlareAvatarFacts _aheadFacts = new FlareAvatarFacts { MyBusinesses = new List<string>(0) };

        /// <summary>The key of a post's photo of the city for the app (as the player's: "f:" and its path), or null for none.</summary>
        public string CityPhotoKey(FlarePost p)
        {
            if (p == null || _bank == null || _photoFolder == null || Settings.FlareCityPhotos <= 0)
            {
                return null;
            }
            int id = _bank.PickOf(p.Id);
            return id >= 0 && _bankFiles.Contains(id) ? "f:" + Path.Combine(_photoFolder, FlareCityPhotoRules.FileName(id)) : null;
        }

        // Flare starts or the save changes: the folder, the bank read on a worker, the faces drawn ahead.
        private void PhotosStart()
        {
            _bank = null;
            _bankFiles.Clear();
            _fronts.Clear();
            _photoToday = 0;
            _photoDay = _bankDay = _frontsDay = -1;
            _picksLooked = -1;
            _photoMarked = _bankDirty = false;
            _photoGeneration++;
            _nicheOf ??= NicheOfPost;
            string root = FlareBridges.Ask(FlareBridges.PictureFolder, null);
            _photoFolder = root != null ? Path.Combine(root, FlareCityPhotoRules.Folder) : null;
            if (_photoFolder != null)
            {
                StartCoroutine(Guard.Routine(BankRead(_photoFolder, _photoGeneration)));
            }
            FacesAhead(Save != null ? Save.Day : 0);
        }

        // The faces likely to be seen soon handed to the painter, drawn behind the black screens; none with the option off.
        private void FacesAhead(int day)
        {
            if (!Settings.FlarePictures || _file == null)
            {
                return;
            }
            _aheadFacts.File = _file;
            _aheadFacts.People = _people;
            _aheadFacts.CitySeed = FlareLiveRules.CitySeed(_file);
            FlareFacePainter.Ahead(_aheadFacts, day);
        }

        private void PhotosStop()
        {
            if (_bank != null && _bankDirty && _photoFolder != null)
            {
                BankWrite();   // what changed since the last second goes to its file
            }
            _photoGeneration++;
            _bank = null;
            _bankFiles.Clear();
            _fronts.Clear();
            _photoFolder = null;
            _photoBusy = false;
            FlareCityCamera.Clear();
            FlareFacePainter.Forget();
        }

        // An influencer's niche (FlareNiche) for a post of theirs; -1 for anybody else's.
        private int NicheOfPost(FlarePost p)
        {
            FlareInfluencer inf = p.Author == FlareAuthor.Influencer && _file != null ? _file.Influencer(p.Who) : null;
            return inf != null ? (int)inf.Niche : -1;
        }

        // On a worker: the bank's file read and the photos listed, files and records without the other let go; then the bank is Flare's.
        private IEnumerator BankRead(string folder, int generation)
        {
            FlareBank bank = null;
            var have = new List<int>(64);
            int legacy = 0, orphans = 0, lost = 0;
            Task<bool> work = Task.Run(() =>
            {
                string index = Path.Combine(folder, FlareCityPhotoRules.IndexFile);
                bank = FlareCityPhotoRules.Read(System.IO.File.Exists(index) ? System.IO.File.ReadAllText(index) : null);
                string thumbs = Path.Combine(folder, "Thumbs");
                var found = new HashSet<int>();
                if (Directory.Exists(thumbs))
                {
                    string[] files = Directory.GetFiles(thumbs);
                    for (int i = 0; i < files.Length; i++)
                    {
                        string name = Path.GetFileName(files[i]);
                        int id = FlareCityPhotoRules.IdOf(name);
                        if (id >= 0 && bank.Photo(id) != null)
                        {
                            found.Add(id);
                            continue;
                        }
                        if (id >= 0 || FlareCityPhotoRules.Legacy(name))
                        {
                            legacy += id >= 0 ? 0 : 1;
                            orphans += id >= 0 ? 1 : 0;
                            Delete(files[i]);
                        }
                    }
                }
                var missing = new List<int>(4);
                for (int i = 0; i < bank.Photos.Count; i++)
                {
                    if (!found.Contains(bank.Photos[i].Id))
                    {
                        missing.Add(bank.Photos[i].Id);
                    }
                }
                lost = FlareCityPhotoRules.Drop(bank, missing);
                have.AddRange(found);
                return true;
            });
            while (!work.IsCompleted)
            {
                yield return null;
            }
            if (generation != _photoGeneration || work.Status != TaskStatus.RanToCompletion || bank == null)
            {
                yield break;
            }
            _bank = bank;
            for (int i = 0; i < have.Count; i++)
            {
                _bankFiles.Add(have[i]);
            }
            _bankDirty = lost > 0;
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Flare check (city photos): the bank for this save has " + bank.Photos.Count + " photo(s) and " + bank.Picks.Count + " post(s) picked; " + legacy
                             + " photo(s) of the old kind, " + orphans + " without a record and " + lost + " record(s) without a photo let go (at most " + FlareCityPhotoRules.BankMost
                             + "): PASS");
            }
            if (bank.Picks.Count > 0)
            {
                _view?.Changed();
            }
        }

        private static void Delete(string path)
        {
            try
            {
                System.IO.File.Delete(path);
            }
            catch (IOException)
            {
                // deleted at the next start
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        // The bank's file written on a worker: a temporary file, then copied over the old.
        private void BankWrite()
        {
            _bankDirty = false;
            _bankWriting = true;
            string text = FlareCityPhotoRules.Write(_bank), folder = _photoFolder;
            Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(folder);
                    string path = Path.Combine(folder, FlareCityPhotoRules.IndexFile), temp = path + ".new";
                    System.IO.File.WriteAllText(temp, text);
                    System.IO.File.Copy(temp, path, true);
                    System.IO.File.Delete(temp);
                }
                catch (IOException)
                {
                    // written again with the next change
                }
                catch (UnauthorizedAccessException)
                {
                }
                finally
                {
                    _bankWriting = false;
                }
            });
        }

        // Once a game day, and when the bank grows past its most: stale photos and the picks of posts that are gone let go.
        private void BankClean(int day)
        {
            FlareFile file = _file;
            int picks = FlareCityPhotoRules.PrunePicks(_bank, id => file != null && file.Post(id) != null);
            int gone = FlareCityPhotoRules.Stale(_bank, day, _bankGone);
            if (gone > 0)
            {
                FlareCityPhotoRules.Drop(_bank, _bankGone);
                var paths = new List<string>(gone);
                for (int i = 0; i < _bankGone.Count; i++)
                {
                    _bankFiles.Remove(_bankGone[i]);
                    paths.Add(PhotoThumbs.FileOf(Path.Combine(_photoFolder, FlareCityPhotoRules.FileName(_bankGone[i]))));
                }
                Task.Run(() =>
                {
                    for (int i = 0; i < paths.Count; i++)
                    {
                        Delete(paths[i]);
                    }
                });
            }
            _bankDirty |= picks > 0 || gone > 0;
        }

        // Once a real second: the picks, the fronts' list, the bank's file, and the next photo if its time has come.
        private void PhotosSecond()
        {
            if (_bank == null || _photoFolder == null || Save == null || LoadScene.isLoading)
            {
                return;
            }
            long now = Now;
            int day = (int)(now / 1440);
            if (day != _bankDay)
            {
                _bankDay = day;
                BankClean(day);
            }
            if (Settings.FlareCityPhotos > 0 && now != _picksLooked)
            {
                _picksLooked = now;
                PicksLook(now);
            }
            FrontsRead(day);
            if (_bankDirty && !_bankWriting)
            {
                BankWrite();
            }
            float t = Time.realtimeSinceStartup;
            if (t < _photoNext || _photoBusy || Settings.FlareCityPhotos <= 0 || FlarePictureGame.Driving())
            {
                return;   // in a vehicle the photo waits, its pace not spent
            }
            _photoNext = t + _photoPace;
            if (_photoDay != day)
            {
                _photoDay = day;
                _photoToday = 0;
            }
            if (_photoToday >= FlareCityPhotoRules.DayMost || !Scene(now, out Vector3? focus) || !FlareCityPhotoRules.Plan(_bank, _scene, out FlarePhotoShot shot))
            {
                return;   // indoors, or the bank has all it wants of here
            }
            _photoToday++;
            StartCoroutine(Guard.Routine(PhotoTake(shot, FocusFor(shot, focus), _photoGeneration, false)));
        }

        // The posts going up soon or just up, picked: a fitting photo, or none once a post is up; at most PicksALook a look.
        private void PicksLook(long now)
        {
            int citySeed = FlareLiveRules.CitySeed(_file);
            bool wet = FlarePictureGame.Raining(), changed = false;
            _pickSkip.Clear();
            for (int k = 0; k < PicksALook; k++)
            {
                FlarePost p = FlareCityPhotoRules.NextPick(_file, _bank, citySeed, Settings.FlareCityPhotos, now, _nicheOf, _pickSkip);
                if (p == null)
                {
                    break;
                }
                FlareInfluencer inf = p.Author == FlareAuthor.Influencer ? _file.Influencer(p.Who) : null;
                FlareBankPhoto photo = FlareCityPhotoRules.Wish(p, inf != null ? (int)inf.Niche : -1, inf?.District, wet, out FlarePhotoWish w)
                    ? FlareCityPhotoRules.Pick(_bank, w, FlareCommentRules.Seed(p, citySeed)) : null;
                if (photo != null || now >= p.At)
                {
                    FlareCityPhotoRules.Picked(_bank, p.Id, photo);
                    _bankDirty = true;
                    changed |= photo != null && p.At <= now;
                }
                _pickSkip.Add(p.Id);
            }
            if (changed)
            {
                _view?.Changed();
            }
        }

        // The fronts of the city's businesses read a slice at a time, a millisecond a second, once a game day.
        private void FrontsRead(int day)
        {
            if (_frontsDay != day)
            {
                _frontsDay = day;
                _fronts.Clear();
                _frontsCursor = 0;
                _frontsDone = false;
            }
            if (_frontsDone)
            {
                return;
            }
            _frontsDone = FlarePictureGame.FrontSlice(ref _frontsCursor, _fronts, (long)(Stopwatch.Frequency * FrontsSliceMs / 1000.0)) || _fronts.Count >= FrontsMost;
        }

        // What can be photographed here now, into the scene; false indoors. 'focus' is the event's or the sight's middle.
        private bool Scene(long now, out Vector3? focus)
        {
            focus = null;
            string district = FlareBridges.Ask(FlareBridges.PlayerDistrict, null);
            if (district == null || !FlarePictureGame.Outdoors(out Vector3 player, out _))
            {
                return false;
            }
            int minute = (int)(now % 1440);
            _scene.District = district;
            _scene.Day = (int)(now / 1440);
            _scene.Minute = minute;
            _scene.Light = FlareCityPhotoRules.LightAt(minute / 60);
            _scene.Wet = FlarePictureGame.Raining();
            _scene.Event = null;
            _scene.Sight = null;
            _scene.Fronts.Clear();
            if (FlareBridges.StreetEventAt != null && FlareBridges.StreetEventAt(out float ex, out float ey, out float ez, out string label) && label != null
                && (ex - player.x) * (ex - player.x) + (ez - player.z) * (ez - player.z) <= EventNear * EventNear)
            {
                _scene.Event = label;
                focus = new Vector3(ex, ey, ez);
            }
            else if (FlareBridges.SightAt != null && FlareBridges.SightAt(player.x, player.z, out string sight, out float sx, out float sz)
                     && FlareCityPhotoRules.SightKind(sight) != FlarePhotoKind.None)
            {
                _scene.Sight = sight;
                focus = new Vector3(sx, player.y, sz);
            }
            for (int i = 0; i < _fronts.Count && _scene.Fronts.Count < FrontsNearMost; i++)
            {
                FlareFrontSpot f = _fronts[i];
                float dx = f.X - player.x, dz = f.Z - player.z;
                if (FlareCityPhotoRules.InRange(dx, dz))
                {
                    _scene.Fronts.Add(new FlareFrontNear { Place = f.Place, District = f.District, Niche = f.Niche, Metres = Mathf.Sqrt(dx * dx + dz * dz), Posts = Posting(f.Place) });
                }
            }
            return true;
        }

        // A business posting in the city's feed today, or one of the player's: its front first.
        private bool Posting(string place)
        {
            for (int i = 0; i < _facts.Shops.Count; i++)
            {
                if (_facts.Shops[i].Place == place)
                {
                    return true;
                }
            }
            for (int i = 0; i < _facts.Mine.Count; i++)
            {
                if (_facts.Mine[i].Place == place)
                {
                    return true;
                }
            }
            return false;
        }

        private static Vector3? FocusFor(FlarePhotoShot shot, Vector3? focus)
        {
            return shot.Kind == FlarePhotoKind.Event || shot.Kind == FlarePhotoKind.Park || shot.Kind == FlarePhotoKind.Landmark ? focus : null;
        }

        private IEnumerator PhotoTake(FlarePhotoShot shot, Vector3? focus, int generation, bool test)
        {
            _photoBusy = true;
            string folder = _photoFolder, district = _scene.District;
            FlarePhotoLight light = _scene.Light;
            bool wet = _scene.Wet;
            int day = _scene.Day, minute = _scene.Minute, id = _bank.NextId++;   // the number is the bank's from now: a photo that fails leaves a gap, never a reuse
            var result = new FlareCityCamera.Result();
            try
            {
                if (!_photoMarked)
                {
                    _photoMarked = true;
                    Action marking = FlareBridges.MarkPictureFolder;
                    Task mark = Task.Run(() => marking?.Invoke());   // the save's id beside its photos: a deleted save's photos go with it
                    while (!mark.IsCompleted)
                    {
                        yield return null;
                    }
                }
                string thumb = PhotoThumbs.FileOf(Path.Combine(folder, FlareCityPhotoRules.FileName(id)));
                IEnumerator take = FlareCityCamera.Take(shot.Kind, shot.Place, focus, id * 7919 + day, thumb, result);
                while (take.MoveNext())
                {
                    yield return take.Current;
                }
            }
            finally
            {
                _photoBusy = false;   // on every path: a fault in the camera leaves the next photo free to come
            }
            if (generation != _photoGeneration || _bank == null)
            {
                yield break;
            }
            _photoPace = FlareCityPhotoRules.NextPace(_photoPace, result.RenderMs);
            if (result.Written)
            {
                FlareCityPhotoRules.Taken(_bank, id, shot, district, light, wet, day, minute);
                _bankFiles.Add(id);
                _bankDirty = true;
                if (_bank.Photos.Count > FlareCityPhotoRules.BankMost)
                {
                    BankClean(day);   // past the most: the oldest goes now
                }
            }
            if (Meta.Dev.Verbose || test)
            {
                Logger?.Info("Flare check (city photo): " + shot.Kind.ToString().ToLowerInvariant() + (shot.Place != null ? " (" + shot.Place + ")" : string.Empty) + " in " + district + ", "
                             + light.ToString().ToLowerInvariant() + (wet ? ", in the rain" : ", dry") + (result.Written ? ", taken as photo " + id : ", not taken: " + result.Why)
                             + "; the renders took " + result.RenderMs.ToString("0.0", CultureInfo.InvariantCulture) + " ms of the main thread over " + result.Frames + " frame(s), "
                             + result.WorstFrameMs.ToString("0.0", CultureInfo.InvariantCulture) + " ms in the slowest (wanted under " + FlareCityPhotoRules.SlowMs + " ms in all and "
                             + FlareCityPhotoRules.FrameMs + " ms a frame, one render a frame: " + (result.WorstFrameMs < FlareCityPhotoRules.FrameMs ? "PASS" : "SLOW") + "), next in "
                             + _photoPace.ToString("0") + " s; " + _photoToday + " of " + FlareCityPhotoRules.DayMost + " today, " + _bank.Photos.Count + " in the bank: "
                             + (result.Written || result.Why != null ? "PASS" : "FAIL"));
            }
            if (test)
            {
                Tell(result.Written ? NotificationType.Info : NotificationType.Warning, result.Written ? "reallife_flare_test_photo_taken" : "reallife_flare_test_photo_not", _openFeedTab,
                    "ms", result.RenderMs.ToString("0"));   // why not is in the log line above
            }
        }

        /// <summary>Test: a photo of the city for the bank now whatever the pace, and the likely faces drawn ahead.</summary>
        public void TestPictures()
        {
            Guarded(() =>
            {
                if (!TestReady("reallife_flare_test_pictures"))
                {
                    return;
                }
                FacesAhead(Save.Day);
                Logger?.Info("Flare test (pictures): " + FlareFacePainter.Report() + "; "
                             + (_bank != null ? _bank.Photos.Count + " city photo(s) in the bank, " + _bank.Picks.Count + " post(s) picked" : "the bank not read yet")
                             + "; the folder " + (_photoFolder ?? "(none)") + ".");
                if (_photoBusy || _photoFolder == null || _bank == null)
                {
                    Tell(NotificationType.Warning, "reallife_flare_test_photo_busy", null);
                    return;
                }
                if (!Scene(Now, out Vector3? focus))
                {
                    Logger?.Info("Flare test (pictures): no photo: the player is indoors, or in no district the game knows.");
                    Tell(NotificationType.Warning, "reallife_flare_test_photo_not", null, "ms", "0");
                    return;
                }
                if (!FlareCityPhotoRules.Plan(_bank, _scene, out FlarePhotoShot shot))
                {
                    shot = new FlarePhotoShot { Kind = FlarePhotoKind.Street, Niche = -1 };
                }
                StartCoroutine(Guard.Routine(PhotoTake(shot, FocusFor(shot, focus), _photoGeneration, true)));
            });
        }
    }
}
