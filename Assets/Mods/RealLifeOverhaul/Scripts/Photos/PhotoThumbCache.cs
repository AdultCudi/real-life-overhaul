using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// The small copies the kit's Gallery and Flare's photo picker show, loaded only for the photos in view and kept to at most
    /// <see cref="Most"/>. A screen says which photos of its list are in view (<see cref="Want"/>); the copies (Core/PhotoThumbs files,
    /// 256 pixels wide) are read on a worker a few at a time and made textures and sprites within a budget of a frame; the ones
    /// out of view that are over the cap are let go, the longest unused first. A sprite is found by the key "photo:" and the
    /// photo's name. Nothing runs while nothing is wanted and nothing changes: the cost is the coroutine of a read. Main thread.
    /// </summary>
    internal sealed class PhotoThumbCache
    {
        public const int Most = 120;          // small copies kept as textures (some 110 KB each: 13 MB)
        public const int AtOnce = 16;         // small copies a worker reads in one go
        public const double BudgetMs = 1.0;   // milliseconds of the main thread a frame for making them textures
        public const string Prefix = "photo:";

        private sealed class Held
        {
            public Texture2D Texture;
            public Sprite Sprite;
            public int Used;
        }

        private readonly Dictionary<string, Held> _held = new Dictionary<string, Held>(Most + AtOnce, StringComparer.Ordinal);
        private readonly HashSet<string> _wanted = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _missing = new HashSet<string>(StringComparer.Ordinal);   // no small copy to read
        private readonly HashSet<string> _loading = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _queue = new List<string>(64);
        private readonly List<string> _drop = new List<string>(16);
        private int _tick, _generation;
        private bool _reading;

        /// <summary>Changes whenever a small copy comes in or goes: a screen laid out for an older version lays out again.</summary>
        public int Version { get; private set; }

        public int Count => _held.Count;

        /// <summary>A photo's sprite if its small copy is loaded, else null.</summary>
        public Sprite Sprite(string name)
        {
            if (name != null && _held.TryGetValue(name, out Held h))
            {
                h.Used = ++_tick;
                return h.Sprite;
            }
            return null;
        }

        /// <summary>
        /// The photos of <paramref name="photos"/> from <paramref name="first"/> to <paramref name="last"/> are in view: asks for the
        /// ones not loaded, and lets go of copies over the cap that are not in view. <paramref name="host"/> runs the reads.
        /// </summary>
        public void Want(MonoBehaviour host, string folder, List<PhotoEntry> photos, int first, int last)
        {
            _wanted.Clear();
            _queue.Clear();
            first = Math.Max(0, first);
            last = Math.Min(photos.Count - 1, last);
            for (int i = first; i <= last; i++)
            {
                string name = photos[i].Name;
                _wanted.Add(name);
                if (!_held.ContainsKey(name) && !_loading.Contains(name) && !_missing.Contains(name))
                {
                    _queue.Add(name);
                }
            }
            Evict();
            if (_queue.Count > 0 && !_reading && host != null && folder != null)
            {
                _reading = true;
                host.StartCoroutine(Guard.Routine(Read(folder, _generation)));
            }
        }

        // The copies over the cap that are not in view go, the longest unused first.
        private void Evict()
        {
            if (_held.Count <= Most)
            {
                return;
            }
            _drop.Clear();
            foreach (KeyValuePair<string, Held> pair in _held)
            {
                if (!_wanted.Contains(pair.Key))
                {
                    _drop.Add(pair.Key);
                }
            }
            _drop.Sort((a, b) => _held[a].Used.CompareTo(_held[b].Used));
            for (int i = 0; i < _drop.Count && _held.Count > Most; i++)
            {
                Let(_drop[i]);
            }
        }

        private void Let(string name)
        {
            if (_held.TryGetValue(name, out Held h))
            {
                Sprite sprite = h.Sprite;
                if (sprite != null)
                {
                    UnityEngine.Object.Destroy(sprite);
                }
                PhotosGame.Destroy(h.Texture);
                _held.Remove(name);
                Version++;
            }
        }

        private IEnumerator Read(string folder, int generation)
        {
            try
            {
                while (_queue.Count > 0 && generation == _generation)
                {
                    int n = Math.Min(AtOnce, _queue.Count);
                    var names = new string[n];
                    for (int i = 0; i < n; i++)
                    {
                        names[i] = _queue[i];
                        _loading.Add(names[i]);
                    }
                    _queue.RemoveRange(0, n);
                    var rows = new byte[n][];
                    var widths = new int[n];
                    var heights = new int[n];
                    Task work = Task.Run(() =>
                    {
                        for (int i = 0; i < names.Length; i++)
                        {
                            string photo = Path.Combine(folder, names[i]);
                            rows[i] = PhotoThumbs.Read(PhotoThumbs.FileOf(photo), out widths[i], out heights[i])
                                      ?? PhotoThumbs.Get(photo, out widths[i], out heights[i], out bool _);
                        }
                    });
                    while (!work.IsCompleted)
                    {
                        yield return null;
                    }
                    var watch = Stopwatch.StartNew();
                    for (int i = 0; i < n; i++)
                    {
                        _loading.Remove(names[i]);
                        if (generation != _generation)
                        {
                            yield break;
                        }
                        Texture2D texture = _wanted.Contains(names[i]) ? PhotosGame.SmallTexture(rows[i], widths[i], heights[i]) : null;
                        rows[i] = null;
                        if (texture == null)
                        {
                            if (_wanted.Contains(names[i]))
                            {
                                _missing.Add(names[i]);   // it has none, or cannot be read: shown without one
                            }
                            continue;
                        }
                        var sprite = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                        sprite.name = "RealLifeOverhaul Thumb";
                        _held[names[i]] = new Held { Texture = texture, Sprite = sprite, Used = ++_tick };
                        Version++;
                        if (watch.Elapsed.TotalMilliseconds >= BudgetMs && i < n - 1)
                        {
                            yield return null;
                            watch.Restart();
                        }
                    }
                    Evict();
                }
            }
            finally
            {
                if (generation == _generation)
                {
                    _reading = false;
                }
            }
        }

        /// <summary>A photo gone (deleted) or changed: its copy is let go and asked for again if wanted.</summary>
        public void Forget(string name)
        {
            Let(name);
            _missing.Remove(name);
        }

        /// <summary>Every copy let go, and any read under way ended: the screen closed, or the city unloads.</summary>
        public void Clear()
        {
            _generation++;
            _reading = false;
            _wanted.Clear();
            _queue.Clear();
            _loading.Clear();
            _missing.Clear();
            var names = new List<string>(_held.Keys);
            for (int i = 0; i < names.Count; i++)
            {
                Let(names[i]);
            }
        }
    }
}
