using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>What an avatar shows (FlareAvatars.Of): a round picture, a logo in a disc, an emblem, or the initials.</summary>
    internal struct FlareAvatarShown
    {
        public Texture Picture;      // a round picture (its corners clear), or null
        public Texture Logo;         // the game's logo of a business, shown in a white disc, or null
        public int Emblem;           // FlareAvatarRules.Emblem*, or -1
    }

    /// <summary>
    /// The pictures beside every account's name, as FlareAvatarRules says, at most <see cref="FlareAvatarRules.Most"/>
    /// round textures, the longest unused destroyed first. Cost: nothing per frame or while closed; binding a card is a
    /// plan and a dictionary look, a new picture one read off the main thread and one upload.
    /// </summary>
    internal sealed class FlareAvatars
    {
        private const int Bytes = FlareAvatarRules.Size * FlareAvatarRules.Size * 4;

        private readonly Dictionary<string, Texture2D> _made = new Dictionary<string, Texture2D>(FlareAvatarRules.Most + 1, StringComparer.Ordinal);
        private readonly FlareAvatarLru _used = new FlareAvatarLru(FlareAvatarRules.Most);
        private readonly HashSet<string> _asked = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Texture2D> _logos = new Dictionary<string, Texture2D>(StringComparer.Ordinal);   // the game's textures, by name
        private readonly HashSet<string> _noLogo = new HashSet<string>(StringComparer.Ordinal);
        private readonly FlareAvatarFacts _facts = new FlareAvatarFacts { MyBusinesses = new List<string>(8) };
        private readonly FlareDirector _director;
        private readonly Action _loaded;
        private readonly Action<string, byte[]> _painted;
        private readonly Action<string> _missed;
        private readonly Stack<byte[]> _buffers = new Stack<byte[]>(2);
        private Texture2D _decode;
        private int _opening;   // counts the city's unloads (Clear): a picture finished after one is let go

        public FlareAvatars(FlareDirector director, Action loaded)
        {
            _director = director;
            _loaded = loaded;
            _painted = Painted;
            _missed = Missed;
        }

        /// <summary>The app opens or its screen is written again: what the plans are made from, the player's businesses too with <paramref name="all"/>.</summary>
        public void Refresh(bool all)
        {
            _facts.File = _director.File;
            _facts.People = _director.People;
            _facts.CitySeed = _facts.File != null ? FlareLiveRules.CitySeed(_facts.File) : 0;
            if (all)
            {
                FlarePictureGame.MyBusinesses(_facts.MyBusinesses);
            }
            FlareFacePainter.Painted = _painted;
            FlareFacePainter.Missed = _missed;
        }

        /// <summary>What an account's avatar shows now; starts making its picture if it is not there yet.</summary>
        public FlareAvatarShown Of(string key)
        {
            var shown = new FlareAvatarShown { Emblem = -1 };
            if (!Settings.FlarePictures || _facts.File == null)
            {
                return shown;   // the option off: the initials
            }
            FlareAvatarPlan plan = FlareAvatarRules.Plan(key, _facts);
            switch (plan.Kind)
            {
                case FlareAvatarKind.Emblem:
                    shown.Emblem = plan.Emblem;
                    return shown;
                case FlareAvatarKind.Logo:
                    shown.Logo = Logo(plan);
                    shown.Emblem = shown.Logo == null ? FlareAvatarRules.EmblemBusiness : -1;   // the shop's emblem while its logo comes, and for one the game has none of
                    return shown;
                case FlareAvatarKind.Initials:
                    return shown;
            }
            string picture = plan.Picture;
            Texture2D made;
            if (_made.TryGetValue(picture, out made) && made != null)
            {
                _used.Use(picture);
                shown.Picture = made;
                return shown;
            }
            if (_asked.Add(picture))
            {
                Start(plan, picture);
            }
            return shown;
        }

        private void Start(FlareAvatarPlan plan, string picture)
        {
            switch (plan.Kind)
            {
                case FlareAvatarKind.Face:
                case FlareAvatarKind.Staff:
                    string file = FlareFacePainter.FileOf(plan);
                    _director.StartCoroutine(Guard.Routine(Read(plan, picture, file, _opening)));
                    break;
                case FlareAvatarKind.Player:
                    if (plan.Name != null)
                    {
                        _director.StartCoroutine(Guard.Routine(Selfie(picture, FlarePictures.PathOf(plan.Name), null, _opening)));
                    }
                    else
                    {
                        string portrait = FlarePictureGame.PlayerPortrait();
                        if (portrait == null)
                        {
                            return;   // the game has made none yet: asked again when the app opens again
                        }
                        _director.StartCoroutine(Guard.Routine(Portrait(picture, portrait, _opening)));
                    }
                    break;
                case FlareAvatarKind.Selfie:
                    string data = plan.Name.Length > 2 ? _director.NetPicture(plan.Name.Substring(2)) : null;
                    if (data == null)
                    {
                        _asked.Remove(picture);   // not come yet: asked again when its card is shown again
                        return;
                    }
                    _director.StartCoroutine(Guard.Routine(Selfie(picture, null, data, _opening)));
                    break;
            }
        }

        // A face's round picture from its file, or asked of the painter when there is none yet.
        private IEnumerator Read(FlareAvatarPlan plan, string picture, string file, int opening)
        {
            byte[] into = Buffer();
            Task<bool> read = Task.Run(() =>
            {
                int w, h;
                byte[] rows = PhotoThumbs.Read(file, out w, out h);
                bool whole = rows != null && FlareAvatarRules.FromColour(rows, w, h, into);
                if (whole)
                {
                    Touch(file);
                }
                return whole;
            });
            while (!read.IsCompleted)
            {
                yield return null;
            }
            if (read.Status == TaskStatus.RanToCompletion && read.Result)
            {
                Done(picture, into, opening);
            }
            else if (opening == _opening)
            {
                FlareFacePainter.Want(plan, picture, file, true);   // drawn by the game's portrait camera while the app is open
            }
            _buffers.Push(into);
        }

        // On the worker: a face read a day after its file was last written has the file's time brought forward.
        private static void Touch(string file)
        {
            try
            {
                long now = DateTime.UtcNow.Ticks;
                if (FlareFaceAheadRules.Stale(File.GetLastWriteTimeUtc(file).Ticks, now))
                {
                    File.SetLastWriteTimeUtc(file, new DateTime(now, DateTimeKind.Utc));
                }
            }
            catch (IOException)
            {
                // in use: its time stays, and it is read again another day
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        // The player's latest selfie (its small copy) or another player's (the session's small copy), made round.
        private IEnumerator Selfie(string picture, string path, string base64, int opening)
        {
            byte[] into = Buffer();
            Task<bool> read = Task.Run(() =>
            {
                int w, h;
                byte[] rows;
                if (base64 != null)
                {
                    try
                    {
                        rows = FlareSnap.Decode(Convert.FromBase64String(base64), out w, out h);
                    }
                    catch (FormatException)
                    {
                        return false;
                    }
                }
                else
                {
                    rows = PhotoThumbs.Read(PhotoThumbs.FileOf(path), out w, out h);
                    if (rows == null)
                    {
                        bool undecoded;
                        rows = PhotoThumbs.Get(path, out w, out h, out undecoded);
                    }
                }
                return rows != null && FlareAvatarRules.Disc(rows, w, h, 3, false, into);
            });
            while (!read.IsCompleted)
            {
                yield return null;
            }
            if (read.Status == TaskStatus.RanToCompletion && read.Result)
            {
                Done(picture, into, opening);
            }
            _buffers.Push(into);
        }

        // The game's portrait of the player's character: its JPEG read off the main thread, decoded here, made round off it.
        private IEnumerator Portrait(string picture, string file, int opening)
        {
            Task<byte[]> read = Task.Run(() => File.Exists(file) ? File.ReadAllBytes(file) : null);
            while (!read.IsCompleted)
            {
                yield return null;
            }
            byte[] jpeg = read.Status == TaskStatus.RanToCompletion ? read.Result : null;
            if (jpeg == null || opening != _opening)
            {
                yield break;
            }
            if (_decode == null)
            {
                _decode = new Texture2D(2, 2, TextureFormat.RGB24, false) { name = "RealLife Flare portrait", hideFlags = HideFlags.HideAndDontSave };
            }
            if (!_decode.LoadImage(jpeg, false) || _decode.format != TextureFormat.RGB24)
            {
                yield break;
            }
            int w = _decode.width, h = _decode.height;
            var rows = new byte[w * h * 3];   // once a session: the player's portrait
            NativeArray<byte>.Copy(_decode.GetPixelData<byte>(0), rows, rows.Length);
            byte[] into = Buffer();
            Task<bool> round = Task.Run(() => FlareAvatarRules.Disc(rows, w, h, 3, false, into));
            while (!round.IsCompleted)
            {
                yield return null;
            }
            if (round.Status == TaskStatus.RanToCompletion && round.Result)
            {
                Done(picture, into, opening);
            }
            _buffers.Push(into);
        }

        private byte[] Buffer()
        {
            return _buffers.Count > 0 ? _buffers.Pop() : new byte[Bytes];
        }

        // A face the painter made (its rows valid only now), or one it found on disk (null rows: read it).
        private void Painted(string picture, byte[] rows)
        {
            _asked.Remove(picture);
            if (rows != null)
            {
                Done(picture, rows, _opening);
            }
            else
            {
                _loaded?.Invoke();   // the cards in view ask again, and read it from its file
            }
        }

        private void Missed(string picture)
        {
            // kept as asked: the initials stay until the app opens again
        }

        // A round picture's rows into a texture of its own (RGBA, no mipmaps), its copy in main memory let go.
        private void Done(string picture, byte[] rows, int opening)
        {
            if (opening != _opening)
            {
                return;   // the city unloaded meanwhile
            }
            Texture2D made = null;
            try
            {
                made = new Texture2D(FlareAvatarRules.Size, FlareAvatarRules.Size, TextureFormat.RGBA32, false) { name = "RealLife Flare avatar", wrapMode = TextureWrapMode.Clamp };
                made.LoadRawTextureData(rows);
                made.Apply(false, true);
            }
            catch (Exception e)
            {
                FlareDirector.Logger?.Warn("Flare: an account's picture could not be shown (" + e.Message + "); its initials stay.");
                if (made != null)
                {
                    UnityEngine.Object.Destroy(made);
                }
                return;
            }
            Texture2D before;
            if (_made.TryGetValue(picture, out before) && before != null && before != made)
            {
                UnityEngine.Object.Destroy(before);
            }
            _made[picture] = made;
            string gone = _used.Use(picture);
            if (gone != null)
            {
                if (_made.TryGetValue(gone, out before) && before != null)
                {
                    UnityEngine.Object.Destroy(before);
                }
                _made.Remove(gone);
                _asked.Remove(gone);
            }
            _loaded?.Invoke();
        }

        // The game's logo of a business, asked once; null meanwhile and when the game has none.
        private Texture Logo(FlareAvatarPlan plan)
        {
            Texture2D logo;
            if (_logos.TryGetValue(plan.Name, out logo))
            {
                if (logo != null)
                {
                    return logo;
                }
                _logos.Remove(plan.Name);   // the game let it go: asked again
            }
            if (_noLogo.Contains(plan.Name) || !_asked.Add("logo:" + plan.Name))
            {
                return null;
            }
            string name = plan.Name;
            int opening = _opening;
            bool asking = true;   // the game may answer at once, inside this card's binding: then the card shows it now
            FlarePictureGame.Logo(name, plan.Mine, texture =>
            {
                _asked.Remove("logo:" + name);
                if (opening != _opening)
                {
                    return;
                }
                if (texture == null)
                {
                    _noLogo.Add(name);
                }
                else
                {
                    _logos[name] = texture;
                }
                if (!asking)
                {
                    _loaded?.Invoke();
                }
            });
            asking = false;
            return _logos.TryGetValue(name, out logo) ? logo : null;
        }

        /// <summary>The app has closed: the pictures made are kept; one that could not be had is looked for again at the next opening.</summary>
        public void Closed()
        {
            _asked.Clear();
            _noLogo.Clear();
            foreach (KeyValuePair<string, Texture2D> pair in _made)
            {
                if (pair.Value != null)
                {
                    _asked.Add(pair.Key);
                }
            }
        }

        /// <summary>Every texture of ours destroyed, the game's logos being the game's: the city is unloading, or Flare stops.</summary>
        public void Clear()
        {
            foreach (KeyValuePair<string, Texture2D> pair in _made)
            {
                if (pair.Value != null)
                {
                    UnityEngine.Object.Destroy(pair.Value);
                }
            }
            _made.Clear();
            _used.Clear();
            _asked.Clear();
            _logos.Clear();
            _noLogo.Clear();
            if (_decode != null)
            {
                UnityEngine.Object.Destroy(_decode);
                _decode = null;
            }
            _opening++;
            if (FlareFacePainter.Painted == _painted)
            {
                FlareFacePainter.Painted = null;
                FlareFacePainter.Missed = null;
            }
        }
    }
}
