using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The pictures in the player's posts: the five newest are shown in their posts as thumbnails 256 pixels wide. Each
    /// file is read once, off the main thread, then made into a small texture on it (the full picture is never kept),
    /// and every texture is destroyed when the app closes or the city unloads. A file that is not there any more (moved,
    /// deleted) shows the post without it, and is not looked for again this opening.
    ///
    /// COST: nothing per frame and nothing while the app is closed. Opening a post with a picture not yet loaded starts
    /// one read; a frame or two later one decode and one copy at 256 pixels.
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
        private int _opening;   // counts the app's closings: a picture finished after one is let go

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
            if (_made.TryGetValue(p.Id, out made))
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

        private IEnumerator Make(int post, string path, int opening)
        {
            Task<byte[]> read = Task.Run(() => File.Exists(path) ? File.ReadAllBytes(path) : null);
            while (!read.IsCompleted)
            {
                yield return null;
            }
            byte[] bytes = read.Status == TaskStatus.RanToCompletion ? read.Result : null;
            if (bytes == null || bytes.Length == 0)
            {
                yield break;   // no picture: the post shows without it
            }
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
            if (small == null)
            {
                yield break;
            }
            if (opening != _opening)
            {
                UnityEngine.Object.Destroy(small);   // the app closed meanwhile
                yield break;
            }
            _made[post] = small;
            Keep();
            _loaded?.Invoke();
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

        /// <summary>Every texture destroyed: the app has closed, or the city is unloading.</summary>
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
