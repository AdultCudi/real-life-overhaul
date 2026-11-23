using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BAModAPI;
using UnityEngine;
using UnityEngine.Networking;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A gig's short sound (the cart's customer calling out, its till, a taxi's car door) from the player's own files
    /// (<see cref="GigMusicRules"/>), played in place of the game's own effect. The folder is read and every file loaded once, as the
    /// gig starts (<see cref="Open"/>); the clips are small (<see cref="GigMusicRules.MostEffectKilobytes"/> KB at most) and are
    /// destroyed when the gig ends (<see cref="Close"/>). Each moment plays the next file, round and round, in the order of the read
    /// (by name or by the save's seed). A file is cut after <see cref="GigMusicRules.MostEffectSeconds"/>, so a mistaken long one
    /// never holds anything up. <see cref="Play"/> says false when nothing can play (still loading, or every file failed), and the
    /// gig plays the game's own effect as before. Local to this machine: nothing is saved or sent. Cost: one call at each moment;
    /// nothing per frame.
    /// </summary>
    public sealed class GigEffect
    {
        private static readonly HashSet<string> Told = new HashSet<string>();
        private static readonly HashSet<GigSound> ToldEmpty = new HashSet<GigSound>();

        private readonly GigSound _kind;
        private readonly MonoBehaviour _runner;
        private readonly IModLogger _log;
        private readonly List<AudioClip> _clips = new List<AudioClip>();
        private AudioSource _source;
        private int _played;          // moments played so far: which file is next
        private int _cut;             // changes at every play, so an older cut does not stop a newer sound
        private bool _closed;

        private GigEffect(GigSound kind, MonoBehaviour runner, IModLogger log)
        {
            _kind = kind;
            _runner = runner;
            _log = log;
        }

        /// <summary>
        /// Reads the effect's folder once and starts loading what it holds; null when the gig keeps the game's own effect: the option
        /// is off (nothing is read) or no file is taken (said once in a notice).
        /// </summary>
        public static GigEffect Open(GigSound kind, MonoBehaviour runner, IModLogger log)
        {
            try
            {
                if (!Settings.GigMusic || runner == null)
                {
                    return null;
                }
                bool shuffle = Settings.GigMusicOrder == 1;
                GigMusicSet set = GigMusicRules.Read(GigMusicGame.Disc, kind, GigMusicGame.Root, shuffle, GigMusicRules.SeedFor(Street.StreetGame.SaveSeed(), kind));
                log?.Info("Gig music: " + GigMusicRules.Name(kind) + ": " + set.Count + " files taken" + (shuffle ? ", shuffled by the save" : ", in order")
                          + (set.Left.Count > 0 ? "; left out: " + string.Join("; ", set.Left) : string.Empty) + ".");
                if (GigMusicRules.UsesOwn(true, set.Count))
                {
                    var effect = new GigEffect(kind, runner, log);
                    runner.StartCoroutine(Guard.Routine(effect.Load(set.Paths)));
                    return effect;
                }
                if (GigMusicRules.SaysEmpty(true, set.Count, ToldEmpty.Contains(kind)))
                {
                    ToldEmpty.Add(kind);
                    JobText.Toast(UI.Notification.NotificationType.Info, "reallife_gig_music_empty", "folder", GigMusicRules.Name(kind), 9f);
                }
            }
            catch (Exception e)
            {
                log?.Error(e);   // the gig keeps the game's own effect
            }
            return null;
        }

        /// <summary>
        /// Plays the next file at <paramref name="position"/>. False if there is none ready, so the gig plays its own effect.
        /// </summary>
        public bool Play(Vector3 position)
        {
            try
            {
                if (_closed || _clips.Count == 0)
                {
                    return false;
                }
                if (_source == null && !MakeSource())
                {
                    return false;
                }
                AudioClip clip = _clips[GigMusicRules.EffectIndex(_played++, _clips.Count)];
                _source.transform.position = position;
                _source.clip = clip;
                _source.volume = 1f;
                _source.Play();
                _runner.StartCoroutine(Guard.Routine(CutAfter(++_cut)));
                return true;
            }
            catch (Exception e)
            {
                _log?.Error(e);
                return false;
            }
        }

        /// <summary>The gig is over: the sound stops and every clip is let go.</summary>
        public void Close()
        {
            _closed = true;
            _cut++;
            try
            {
                if (_source != null)
                {
                    _source.Stop();
                    _source.clip = null;
                    UnityEngine.Object.Destroy(_source.gameObject);
                    _source = null;
                }
                for (int i = 0; i < _clips.Count; i++)
                {
                    if (_clips[i] != null)
                    {
                        UnityEngine.Object.Destroy(_clips[i]);
                    }
                }
                _clips.Clear();
            }
            catch (Exception)
            {
                // (already gone with the scene)
            }
        }

        private bool MakeSource()
        {
            if (_runner == null)
            {
                return false;
            }
            var host = new GameObject("RealLifeOverhaul Gig Effect " + GigMusicRules.Name(_kind)).transform;
            host.SetParent(_runner.transform, false);
            _source = host.gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 1f;
            _source.minDistance = 3f;
            _source.maxDistance = 30f;
            var references = InstanceBehavior<GlobalReferences>.Instance;
            if (references != null)
            {
                _source.outputAudioMixerGroup = references.cityMixerGroup;
            }
            return true;
        }

        // Each file read whole, one after another; one that fails is named once and left out.
        private IEnumerator Load(string[] paths)
        {
            for (int i = 0; i < paths.Length && !_closed; i++)
            {
                UnityWebRequest request = Ask(paths[i], out string problem);
                AudioClip clip = null;
                if (request != null)
                {
                    yield return request.SendWebRequest();
                    clip = _closed ? null : Content(request, out problem);
                }
                request?.Dispose();
                if (_closed)
                {
                    if (clip != null)
                    {
                        UnityEngine.Object.Destroy(clip);
                    }
                    yield break;
                }
                if (clip == null)
                {
                    Fail(paths[i], problem);
                    continue;
                }
                _clips.Add(clip);
            }
            _log?.Info("Gig music: " + GigMusicRules.Name(_kind) + ": " + _clips.Count + " of " + paths.Length + " files ready.");
        }

        private UnityWebRequest Ask(string path, out string problem)
        {
            problem = null;
            try
            {
                UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, GigMusic.TypeOf(path));
                var handler = request.downloadHandler as DownloadHandlerAudioClip;
                if (handler != null)
                {
                    handler.streamAudio = false;   // a short effect starts at once and is decoded whole
                }
                return request;
            }
            catch (Exception e)
            {
                problem = "it could not be opened: " + e.Message;
                return null;
            }
        }

        private static AudioClip Content(UnityWebRequest request, out string problem)
        {
            problem = null;
            try
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    problem = request.error;
                    return null;
                }
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null || clip.samples <= 0)
                {
                    problem = "it is not a clip the engine can play";
                    if (clip != null)
                    {
                        UnityEngine.Object.Destroy(clip);
                    }
                    return null;
                }
                return clip;
            }
            catch (Exception e)
            {
                problem = e.Message;
                return null;
            }
        }

        private void Fail(string path, string problem)
        {
            string name = Path.GetFileName(path);
            _log?.Warn("Gig music: " + GigMusicRules.Name(_kind) + ": '" + name + "' was left out: " + problem + ".");
            if (Told.Add(path))
            {
                string shown = name.Length > 60 ? name.Substring(0, 57) + "..." : name;   // a long name must not push the notice past its length
                JobText.Toast(UI.Notification.NotificationType.Warning, "reallife_gig_music_failed", "file", shown, "folder", GigMusicRules.Name(_kind), 9f);
            }
        }

        // A file longer than an effect should be is stopped; a sound played since then is not touched.
        private IEnumerator CutAfter(int token)
        {
            yield return new WaitForSeconds(GigMusicRules.MostEffectSeconds);
            if (token == _cut && _source != null)
            {
                _source.Stop();
            }
        }
    }
}
