using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The pictures in the player's posts: the five newest are shown in their posts as thumbnails 256 pixels wide. A
    /// thumbnail is the photo's small copy (Core/PhotoThumbs): made off the main thread when the photo was taken, or, for a
    /// photo taken before that, made off the main thread the first time it is wanted and kept beside the photo; it is read
    /// off the main thread and uploaded on it as it is, with no decode. The textures are kept while the city is loaded, so
    /// opening the app again shows them at once; they are destroyed when the city unloads or Flare stops, and those no
    /// longer among the newest five as soon as they drop out. A file that is not there any more (moved, deleted) shows the
    /// post without it, and is looked for again the next time the app opens. A photo whose PNG the small copy cannot be
    /// made from is drawn small by the engine, as every picture once was.
    ///
    /// COST: nothing per frame and nothing while the app is closed. A post with a picture not yet loaded starts one read of
    /// some 100 KB off the main thread (the first time for an old photo: its PNG read and shrunk there, a row at a time);
    /// a frame or two later one upload of the 256-pixel copy.
    /// </summary>
    internal sealed class FlarePictures
    {
        private const float Tall = 170f;   // pixels of the box at most

        private readonly Dictionary<int, Texture2D> _made = new Dictionary<int, Texture2D>(FlareRules.Thumbs);
        private readonly HashSet<int> _asked = new HashSet<int>();
        private readonly List<int> _newest = new List<int>(FlareRules.Thumbs);
        private readonly List<int> _drop = new List<int>(FlareRules.Thumbs);
        private readonly FlareDirector _director;
        private readonly Action _loaded;
        private int _opening;   // counts the city's unloads (Clear): a picture finished after one is let go

        public FlarePictures(FlareDirector director, Action loaded)
        {
            _director = director;
            _loaded = loaded;
        }

        public static string Folder => Path.Combine(Application.persistentDataPath, "RealLifeOverhaul", "Shots");

        /// <summary>The thumbnail of a post if it is one of the newest five and has been made; starts making it if not. Null otherwise.</summary>
        public Texture2D Of(FlareFile file, FlarePost p)
        {
            FlareRules.Pictures(file, _newest);
            if (!_newest.Contains(p.Id))
            {
                return null;
            }
            Texture2D made;
            if (_made.TryGetValue(p.Id, out made) && made != null)
            {
                return made;
            }
            if (_asked.Add(p.Id) && _director != null)
            {
                _director.StartCoroutine(Guard.Routine(Make(p.Id, Path.Combine(Folder, Path.GetFileName(p.Name ?? string.Empty)), _opening)));
            }
            return null;
        }

        public float Height => Tall;

        // What the worker thread found: the small copy's rows, or that the engine has to draw this one small.
        private sealed class Found
        {
            public byte[] Rows;
            public int Width, Height;
            public bool Undecoded;
        }

        private IEnumerator Make(int post, string path, int opening)
        {
            Task<Found> read = Task.Run(() =>
            {
                var found = new Found();
                found.Rows = PhotoThumbs.Get(path, out found.Width, out found.Height, out found.Undecoded);
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
                small = Upload(copy, path);
            }
            else if (copy != null && copy.Undecoded)
            {
                // A PNG the small copy is not made from: drawn small by the engine, as every picture once was.
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
            if (small == null)
            {
                yield break;   // no picture: the post shows without it
            }
            if (opening != _opening)
            {
                UnityEngine.Object.Destroy(small);   // the city unloaded meanwhile
                yield break;
            }
            if (_made.TryGetValue(post, out Texture2D before) && before != null && before != small)
            {
                UnityEngine.Object.Destroy(before);
            }
            _made[post] = small;
            Keep();
            _loaded?.Invoke();
        }

        // The small copy's rows, as they are, into a texture of the kind the engine's copy was (RGB24, sRGB, no mipmaps), its
        // copy in main memory let go.
        private static Texture2D Upload(Found copy, string path)
        {
            Texture2D small = null;
            try
            {
                small = new Texture2D(copy.Width, copy.Height, TextureFormat.RGB24, false);
                small.LoadRawTextureData(copy.Rows);
                small.Apply(false, true);
                return small;
            }
            catch (Exception e)
            {
                FlareDirector.Logger?.Warn("Flare: a picture could not be shown in its post (" + Path.GetFileName(path) + "): " + e.Message);
                if (small != null)
                {
                    UnityEngine.Object.Destroy(small);
                }
                return null;
            }
        }

        // The engine's own way: the PNG decoded, drawn at 256 pixels wide and read back.
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
                    Graphics.Blit(full, rt);
                    RenderTexture.active = rt;
                    small = new Texture2D(width, height, TextureFormat.RGB24, false);
                    small.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                    small.Apply(false, true);   // no longer readable: the copy in main memory is let go
                    RenderTexture.active = before;
                    RenderTexture.ReleaseTemporary(rt);
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

        // Only the newest five are kept.
        private void Keep()
        {
            FlareFile file = _director != null ? _director.File : null;
            if (file == null)
            {
                return;
            }
            FlareRules.Pictures(file, _newest);
            _drop.Clear();
            foreach (KeyValuePair<int, Texture2D> pair in _made)
            {
                if (!_newest.Contains(pair.Key))
                {
                    _drop.Add(pair.Key);
                }
            }
            for (int i = 0; i < _drop.Count; i++)
            {
                UnityEngine.Object.Destroy(_made[_drop[i]]);
                _made.Remove(_drop[i]);
            }
        }

        /// <summary>The app has closed: the pictures made are kept for its next opening; one that could not be had is looked for again then.</summary>
        public void Closed()
        {
            _asked.Clear();
            foreach (KeyValuePair<int, Texture2D> pair in _made)
            {
                if (pair.Value != null)
                {
                    _asked.Add(pair.Key);
                }
            }
        }

        /// <summary>Every texture destroyed: the city is unloading, or Flare stops.</summary>
        public void Clear()
        {
            foreach (KeyValuePair<int, Texture2D> pair in _made)
            {
                if (pair.Value != null)
                {
                    UnityEngine.Object.Destroy(pair.Value);
                }
            }
            _made.Clear();
            _asked.Clear();
            _opening++;
        }
    }
}
