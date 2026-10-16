using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using RealLifeOverhaul.GameUi;
using UI.Load;
using Unity.Collections;
using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Draws the faces Flare's accounts show (FlareAvatars) that are not on disk yet, one at a time, through the game's
    /// portrait camera (Phone/Contacts/PortraitTurn, shared with the contacts' faces), and keeps each as its round
    /// picture's file under the game's data folder (FlareAvatarRules: 27 KB, by its look; drawn once per look, ever). A face
    /// the contacts already have (their JPEG of the same look) is made round from that, without the camera.
    ///
    /// WHEN. Only when it costs the player nothing they would feel: while the Flare app is open (the phone is up; a face
    /// asked for by a card in view), or behind the game's black screen (a door, sleep, travel), when the faces of the
    /// city's people and the player's friends are drawn ahead (<see cref="Prewarm"/>). Never during a load, never in plain
    /// play with the phone down. The camera rests two seconds after each face in view, a quarter of a second behind a black
    /// screen.
    ///
    /// COST. Nothing while there is nothing to draw: the component switches itself off. While faces wait but may not be
    /// drawn now: two comparisons a frame. A face: the game's portrait render and read-back (some 10 to 30 ms of one frame,
    /// measured and logged with Developer diagnostics, told to the session watch first), or a JPEG of the contacts'
    /// decoded (about a millisecond); then on a worker thread the round picture is made and written.
    /// </summary>
    internal sealed class FlareFacePainter : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("flare", "FlareFacePainter", typeof(FlareFacePainter));   // its Update (Core/Guard)

        private const int WaitingMost = 96;          // faces waiting at most: the city's 40 people, a dozen friends, the influencers, the staff
        private const int SourceMost = 256 * 256 * 4; // bytes: the largest face the game gives (its portraits are 256 square, ARGB32 at most)

        private struct Job
        {
            public FlareAvatarPlan Plan;
            public string Picture;   // FlareAvatarPlan.Picture
            public string File;      // its round picture's file
            public bool Wanted;      // asked for by a card in view (else drawn ahead, behind a black screen only)
        }

        /// <summary>A face made (main thread): its picture key and its round picture's RGBA rows (valid only during the call).</summary>
        public static Action<string, byte[]> Painted;

        /// <summary>A face that cannot be had (one of the staff no longer there, the camera refusing three times): its key.</summary>
        public static Action<string> Missed;

        /// <summary>Whether the Flare app is showing (FlareApp sets it): faces in view are drawn only then.</summary>
        public static bool AppShowing;

        private static FlareFacePainter _instance;
        private static readonly List<Job> Waiting = new List<Job>(WaitingMost);
        private static readonly HashSet<string> Queued = new HashSet<string>(StringComparer.Ordinal);
        private static int _wanted;
        private static byte[] _source;               // a face as the game gives it, for the worker (one for the session)
        private static readonly byte[] Disc = new byte[FlareAvatarRules.Size * FlareAvatarRules.Size * 4];
        private static readonly byte[] Colour = new byte[FlareAvatarRules.Size * FlareAvatarRules.Size * 3];

        private Job _job;
        private bool _busy, _rendering;
        private float _since;
        private Action<Sprite> _drawn;
        private Texture2D _decode;                   // a JPEG of the contacts' decoded into it (one for the session)

        /// <summary>A face to draw: one in view first; one ahead only behind a black screen. Asked again while it waits, it is not added twice.</summary>
        public static void Want(FlareAvatarPlan plan, string picture, string file, bool wanted)
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
                        }
                    }
                }
                Wake();
                return;
            }
            if (Waiting.Count >= WaitingMost)
            {
                return;   // (asked again when its card is bound again)
            }
            Waiting.Add(new Job { Plan = plan, Picture = picture, File = file, Wanted = wanted });
            Queued.Add(picture);
            _wanted += wanted ? 1 : 0;
            Wake();
        }

        /// <summary>
        /// The faces of the city's people and the player's friends drawn ahead, behind the game's black screens (those not on
        /// disk yet; a look at each file is made when its turn comes, not now).
        /// </summary>
        public static void Prewarm(int citySeed, IReadOnlyList<FlarePerson> people)
        {
            for (int i = 0; i < FlareLiveRules.Citizens; i++)
            {
                FlareAvatarPlan plan = FlareAvatarRules.Citizen(citySeed, i);
                Want(plan, plan.Picture, Path.Combine(FlarePictureGame.FaceFolder, FlareAvatarRules.FaceName(plan.Female, plan.Age, plan.Seed)), false);
            }
            for (int i = 0; people != null && i < people.Count; i++)
            {
                if (people[i].LookSeed == 0)
                {
                    continue;
                }
                var plan = new FlareAvatarPlan { Kind = FlareAvatarKind.Face, Female = people[i].Female, Age = people[i].Age, Seed = people[i].LookSeed };
                Want(plan, plan.Picture, Path.Combine(FlarePictureGame.FaceFolder, FlareAvatarRules.FaceName(plan.Female, plan.Age, plan.Seed)), false);
            }
        }

        /// <summary>Flare stops: nothing more is drawn (a face on its way is still kept on disk).</summary>
        public static void Forget()
        {
            Waiting.Clear();
            Queued.Clear();
            _wanted = 0;
        }

        /// <summary>For the diagnostics and the test: faces waiting, and how many of them are in view.</summary>
        public static string Report()
        {
            return Waiting.Count + " face(s) waiting, " + _wanted + " in view";
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
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
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
                    FlareDirector.Logger?.Warn("Flare: the game's portrait camera did not answer for a face in " + PortraitTurn.Timeout + " s; its initials stay until the app asks again.");
                    PortraitTurn.Give(this, now, false);
                    _busy = _rendering = false;
                    Missed?.Invoke(_job.Picture);
                }
                return;
            }
            if (Waiting.Count == 0)
            {
                enabled = false;   // nothing to draw: no cost at all until a face is wanted
                return;
            }
            bool hidden = SceneFinds.Hidden;
            if ((!hidden && (_wanted == 0 || !AppShowing)) || LoadScene.isLoading || !PortraitTurn.Free(now))
            {
                return;   // (two comparisons a frame while faces wait for their moment)
            }
            int at = -1;
            for (int i = 0; i < Waiting.Count && at < 0; i++)
            {
                at = Waiting[i].Wanted ? i : at;
            }
            if (at < 0)
            {
                at = 0;   // (behind a black screen: the faces drawn ahead)
            }
            _job = Waiting[at];
            Waiting.RemoveAt(at);
            Queued.Remove(_job.Picture);
            _wanted -= _job.Wanted ? 1 : 0;
            Draw(now, hidden);
        }

        private void Draw(float now, bool hidden)
        {
            if (File.Exists(_job.File))
            {
                Painted?.Invoke(_job.Picture, null);   // made meanwhile (another save, the other painter): read from disk
                return;
            }
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
                Want(_job.Plan, _job.Picture, _job.File, _job.Wanted);   // the camera is not there yet (the main menu, a load): asked again later
                return;
            }
            CharacterData staff = _job.Plan.Kind == FlareAvatarKind.Staff ? FlarePictureGame.Staff(_job.Plan.Name) : null;
            if (_job.Plan.Kind == FlareAvatarKind.Staff && staff == null)
            {
                PortraitTurn.Give(this, now, hidden);
                Missed?.Invoke(_job.Picture);   // no longer one of the staff: the initials stay
                return;
            }
            _busy = _rendering = true;
            _since = now;
            var watch = Stopwatch.StartNew();
            bool asked;
            using (SessionWatch.Piece("flare: a face drawn by the game's portrait camera"))   // once per look, ever: a render and a read-back
            {
                asked = staff != null ? PortraitTurn.Character(staff, _drawn) : PortraitTurn.Face(_job.Plan.Female, _job.Plan.Age, _job.Plan.Seed, _drawn);
            }
            watch.Stop();
            if (!asked)
            {
                _busy = _rendering = false;
                PortraitTurn.Give(this, now, hidden);
                Want(_job.Plan, _job.Picture, _job.File, _job.Wanted);
                return;
            }
            if (Meta.Dev.Verbose)
            {
                FlareDirector.Logger?.Info("Flare check (faces): a face asked of the game's portrait camera in " + watch.Elapsed.TotalMilliseconds.ToString("0.0") + " ms of the main thread ("
                                           + (hidden ? "behind a black screen" : "the app open") + ", wanted under " + PortraitTurn.Timeout + " s a face); " + Report() + ": PASS");
            }
        }

        // The game's face came back: its pixels copied for the worker, the game's sprite and texture let go, the turn given back.
        private void Drawn(Sprite sprite)
        {
            Texture2D texture = sprite != null ? sprite.texture : null;
            bool hidden = SceneFinds.Hidden;
            PortraitTurn.Give(this, Time.realtimeSinceStartup, hidden);
            if (!_busy || !_rendering)
            {
                Let(sprite, texture);   // (late: the time-out let it go)
                return;
            }
            _rendering = false;
            int bytes;
            bool argb;
            bool copied = Copy(texture, out bytes, out argb);
            int w = copied ? texture.width : 0, h = copied ? texture.height : 0;
            Let(sprite, texture);
            if (!copied)
            {
                _busy = false;
                Missed?.Invoke(_job.Picture);
                return;
            }
            StartCoroutine(Guard.Routine(Finish(_job, w, h, bytes, argb)));
        }

        // A JPEG of the contacts' of the same look: decoded here (about a millisecond), made round on the worker.
        private bool FromJpeg(string file)
        {
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

        // On the worker: the round picture made and written; back here, handed to the app.
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
            if (work.Status == TaskStatus.RanToCompletion && work.Result)
            {
                Painted?.Invoke(job.Picture, Disc);
            }
            else
            {
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
