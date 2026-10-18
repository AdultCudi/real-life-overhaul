using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The pictures in the posts by key: "f:" the player's, its small copy read off the main thread; "n:" the session's,
    /// decoded on a worker. At most <see cref="Most"/> textures. Cost: nothing per frame; a new picture one read and one upload.
    /// </summary>
    internal sealed class FlarePictures
    {
        public const int Most = 16;   // textures

        private readonly Dictionary<string, Texture2D> _made = new Dictionary<string, Texture2D>(Most);
        private readonly List<string> _used = new List<string>(Most);   // the keys, used last at the end
        private readonly HashSet<string> _asked = new HashSet<string>();
        private readonly FlareDirector _director;
        private readonly Action _loaded;
        private int _opening;   // counts the city's unloads (Clear): a picture finished after one is let go

        public FlarePictures(FlareDirector director, Action loaded)
        {
            _director = director;
            _loaded = loaded;
        }

        public static string Folder => Path.Combine(Application.persistentDataPath, "RealLifeOverhaul", "Shots");

        /// <summary>The file a picture of the player's is, or would be for a shot that is only its small copy; null for another key.</summary>
        public static string PathOf(string pic)
        {
            if (string.IsNullOrEmpty(pic) || pic.Length < 3 || !pic.StartsWith("f:", StringComparison.Ordinal))
            {
                return null;
            }
            string name = pic.Substring(2);
            return Path.IsPathRooted(name) ? name : Path.Combine(Folder, Path.GetFileName(name));
        }

        /// <summary>A post's picture key, an older post's own photo too, whose picture was its file name.</summary>
        public static string KeyOf(FlarePost p)
        {
            if (!string.IsNullOrEmpty(p.Pic))
            {
                return p.Pic;
            }
            return p.Author == FlareAuthor.Player && (p.Source == FlareSource.Photo || p.Source == FlareSource.Selfie) && !string.IsNullOrEmpty(p.Name) ? "f:" + p.Name : null;
        }

        /// <summary>A picture's texture if it has been made; starts making it if not. Null meanwhile, or for none.</summary>
        public Texture2D Of(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }
            Texture2D made;
            if (_made.TryGetValue(key, out made) && made != null)
            {
                if (_used.Count == 0 || _used[_used.Count - 1] != key)
                {
                    _used.Remove(key);
                    _used.Add(key);
                }
                return made;
            }
            if (_director != null && _asked.Add(key))
            {
                if (key.StartsWith("n:", StringComparison.Ordinal))
                {
                    string data = _director.NetPicture(key.Substring(2));
                    if (data == null)
                    {
                        _asked.Remove(key);   // not come yet: asked again when its card is shown again
                        return null;
                    }
                    _director.StartCoroutine(Guard.Routine(Session(key, data, _opening)));
                }
                else
                {
                    string path = PathOf(key);
                    if (path != null)
                    {
                        _director.StartCoroutine(Guard.Routine(Make(key, path, _opening)));
                    }
                }
            }
            return null;
        }

        // What the worker thread found: the small copy's rows, or that the engine has to draw this one small.
        private sealed class Found
        {
            public byte[] Rows;
            public int Width, Height;
            public bool Undecoded;
        }

        private IEnumerator Make(string key, string path, int opening)
        {
            Task<Found> read = Task.Run(() =>
            {
                var found = new Found();
                found.Rows = PhotoThumbs.Read(PhotoThumbs.FileOf(path), out found.Width, out found.Height);   // a shot's small copy is all there is of it
                if (found.Rows == null)
                {
                    found.Rows = PhotoThumbs.Get(path, out found.Width, out found.Height, out found.Undecoded);
                }
                return found;
            });
            while (!read.IsCompleted)
            {
                yield return null;
            }
            Found copy = read.Status == TaskStatus.RanToCompletion ? read.Result : null;
            Texture2D small = null;
            if (copy != null && copy.Rows != null)
            {
                small = Upload(copy.Rows, copy.Width, copy.Height, path);
            }
            else if (copy != null && copy.Undecoded)
            {
                // A picture the small copy is not made from (a JPG, an unusual PNG): drawn small by the engine.
                Task<byte[]> whole = Task.Run(() => File.Exists(path) ? File.ReadAllBytes(path) : null);
                while (!whole.IsCompleted)
                {
                    yield return null;
                }
                byte[] bytes = whole.Status == TaskStatus.RanToCompletion ? whole.Result : null;
                if (bytes != null && bytes.Length > 0)
                {
                    small = Drawn(bytes, path);
                }
            }
            Done(key, small, opening);
        }

        private IEnumerator Session(string key, string base64, int opening)
        {
            Task<Found> read = Task.Run(() =>
            {
                var found = new Found();
                try
                {
                    found.Rows = FlareSnap.Decode(Convert.FromBase64String(base64), out found.Width, out found.Height);
                }
                catch (FormatException)
                {
                    found.Rows = null;
                }
                return found;
            });
            while (!read.IsCompleted)
            {
                yield return null;
            }
            Found copy = read.Status == TaskStatus.RanToCompletion ? read.Result : null;
            Done(key, copy != null && copy.Rows != null ? Upload(copy.Rows, copy.Width, copy.Height, key) : null, opening);
        }

        private void Done(string key, Texture2D small, int opening)
        {
            if (small == null)
            {
                return;   // no picture: the card shows without it
            }
            if (opening != _opening)
            {
                UnityEngine.Object.Destroy(small);   // the city unloaded meanwhile
                return;
            }
            Texture2D before;
            if (_made.TryGetValue(key, out before) && before != null && before != small)
            {
                UnityEngine.Object.Destroy(before);
            }
            _made[key] = small;
            _used.Remove(key);
            _used.Add(key);
            while (_used.Count > Most)
            {
                string oldest = _used[0];
                _used.RemoveAt(0);
                if (_made.TryGetValue(oldest, out before) && before != null)
                {
                    UnityEngine.Object.Destroy(before);
                }
                _made.Remove(oldest);
                _asked.Remove(oldest);
            }
            _loaded?.Invoke();
        }

        // The rows, as they are, into an RGB24 texture without mipmaps, its copy in main memory let go.
        private static Texture2D Upload(byte[] rows, int width, int height, string name)
        {
            Texture2D small = null;
            try
            {
                small = new Texture2D(width, height, TextureFormat.RGB24, false);
                small.LoadRawTextureData(rows);
                small.Apply(false, true);
                return small;
            }
            catch (Exception e)
            {
                FlareDirector.Logger?.Warn("Flare: a picture could not be shown in its post (" + Path.GetFileName(name) + "): " + e.Message);
                if (small != null)
                {
                    UnityEngine.Object.Destroy(small);
                }
                return null;
            }
        }

        // The engine's own way: the picture decoded, drawn at 256 pixels wide and read back.
        private static Texture2D Drawn(byte[] bytes, string path)
        {
            Texture2D full = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D small = null;
            try
            {
                if (full.LoadImage(bytes, false) && full.width > 0)
                {
                    int width = FlareRules.ThumbWidth, height = Mathf.Max(1, full.height * width / full.width);
                    RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                    RenderTexture before = RenderTexture.active;
                    try
                    {
                        Graphics.Blit(full, rt);
                        RenderTexture.active = rt;
                        small = new Texture2D(width, height, TextureFormat.RGB24, false);
                        small.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                        small.Apply(false, true);   // no longer readable: the copy in main memory is let go
                    }
                    finally
                    {
                        RenderTexture.active = before;   // on a fault too
                        RenderTexture.ReleaseTemporary(rt);
                    }
                }
            }
            catch (Exception e)
            {
                FlareDirector.Logger?.Warn("Flare: a picture could not be shown in its post (" + Path.GetFileName(path) + "): " + e.Message);
                if (small != null)
                {
                    UnityEngine.Object.Destroy(small);
                    small = null;
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(full);
            }
            return small;
        }

        /// <summary>The app has closed: the pictures are kept; one that could not be had is looked for again next time.</summary>
        public void Closed()
        {
            _asked.Clear();
            foreach (KeyValuePair<string, Texture2D> pair in _made)
            {
                if (pair.Value != null)
                {
                    _asked.Add(pair.Key);
                }
            }
        }

        /// <summary>Every texture destroyed: the city unloads or Flare stops.</summary>
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
            _opening++;
        }
    }
}
