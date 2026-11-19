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
    /// A gig's own music from the player's files (<see cref="GigMusicRules"/>), played on the gig's own audio source in place of
    /// the game's clip. The folder is read once, as the gig starts (<see cref="Open"/>); each file is opened as a stream
    /// when its turn comes and let go as soon as it ends or the set stops, so no song is held in memory after the gig. The set's
    /// end fades the file out over <see cref="GigMusicRules.FadeSeconds"/>, whatever its length: the gig's own timing never reads
    /// the file. A file that cannot be played is named once in a notice and skipped; when none can be, the gig's own sound plays
    /// instead. The sound is local to this machine: nothing about it is saved or sent. Cost: while a file plays, one look at the
    /// source every half second from a coroutine; nothing per frame, nothing while idle.
    /// </summary>
    public sealed class GigMusic
    {
        private const float PollSeconds = 0.5f;     // seconds between looks at whether a file has ended
        private const float LoadWait = 3f;          // seconds a stream may take to be ready before the file is taken as broken
        private const float TooShort = 1f;          // seconds: a file that ends sooner than this after it started did not really play
        private const int FadeSteps = 8;            // volume steps in the fade at the set's end

        private static readonly HashSet<string> Told = new HashSet<string>();
        private static readonly HashSet<GigSound> ToldEmpty = new HashSet<GigSound>();
        private static readonly WaitForSeconds Poll = new WaitForSeconds(PollSeconds);
        private static readonly WaitForSecondsRealtime FadeStep = new WaitForSecondsRealtime(GigMusicRules.FadeSeconds / FadeSteps);

        private readonly GigMusicSet _set;
        private readonly MonoBehaviour _runner;
        private readonly IModLogger _log;
        private readonly GigSound _kind;

        private int _token;               // changes whenever the run in flight must end: a stop, or a new start
        private int _index;
        private bool _active;
        private bool _gaveUp;             // every file failed: the gig's own sound for the rest of the gig
        private UnityWebRequest _request; // the stream's reader, kept open while its clip plays
        private AudioClip _clip;

        private GigMusic(GigSound kind, GigMusicSet set, MonoBehaviour runner, IModLogger log)
        {
            _kind = kind;
            _set = set;
            _runner = runner;
            _log = log;
        }

        /// <summary>
        /// Reads the gig's folder once and returns the player for it, or null when the gig keeps its own sound: the option is off
        /// (nothing is read) or no file is taken, which is said once in a notice.
        /// </summary>
        public static GigMusic Open(GigSound kind, MonoBehaviour runner, IModLogger log)
        {
            try
            {
                if (!Settings.GigMusic || runner == null)
                {
                    return null;
                }
                string folder = GigMusicRules.FolderOf(GigMusicGame.Root, kind);
                bool shuffle = Settings.GigMusicOrder == 1;
                GigMusicSet set = GigMusicRules.Read(GigMusicGame.Disc, folder, shuffle, GigMusicRules.SeedFor(Street.StreetGame.SaveSeed(), kind));
                log?.Info("Gig music: " + GigMusicRules.Name(kind) + ": " + set.Count + " files taken from " + folder + (shuffle ? ", shuffled by the save" : ", in order")
                          + (set.Left.Count > 0 ? "; left out: " + string.Join("; ", set.Left) : string.Empty) + ".");
                if (GigMusicRules.UsesOwn(true, set.Count))
                {
                    return new GigMusic(kind, set, runner, log);
                }
                if (GigMusicRules.SaysEmpty(true, set.Count, ToldEmpty.Contains(kind)))
                {
                    ToldEmpty.Add(kind);
                    JobText.Toast(UI.Notification.NotificationType.Info, "reallife_gig_music_empty", "folder", GigMusicRules.Name(kind), 9f);
                }
            }
            catch (Exception e)
            {
                log?.Error(e);   // the gig keeps its own sound
            }
            return null;
        }

        /// <summary>
        /// Starts the player's files on <paramref name="source"/> (already placed and set up by the gig); a start in flight is left as it is.
        /// <paramref name="otherwise"/> runs once if no file can be played, to start the gig's own sound.
        /// </summary>
        public void Play(AudioSource source, Action otherwise)
        {
            if (_active)
            {
                return;
            }
            if (_gaveUp)
            {
                otherwise?.Invoke();
                return;
            }
            _active = true;
            _token++;
            _runner.StartCoroutine(Guard.Routine(Run(_token, source, source.volume, otherwise)));
        }

        /// <summary>
        /// Ends the music with a short fade, and lets go of the file. True if it took over stopping the source; false if nothing of
        /// the player's was playing (the gig's own sound, or nothing), so the gig stops its source itself.
        /// </summary>
        public bool Stop(AudioSource source)
        {
            _token++;   // the run in flight ends at its next step and touches nothing more
            _active = false;
            AudioClip clip = _clip;
            UnityWebRequest request = _request;
            _clip = null;
            _request = null;
            if (clip == null)
            {
                request?.Dispose();
                return false;
            }
            if (source != null && _runner != null && _runner.isActiveAndEnabled && source.isPlaying)
            {
                _runner.StartCoroutine(Guard.Routine(Fade(source, _token, clip, request)));
            }
            else
            {
                Release(source, clip, request);
            }
            return true;
        }

        private IEnumerator Run(int token, AudioSource source, float volume, Action otherwise)
        {
            int failures = 0;
            while (token == _token && failures < _set.Count)
            {
                string path = _set.Paths[_index];
                UnityWebRequest request = Ask(path, out string problem);
                AudioClip clip = null;
                if (request != null)
                {
                    yield return request.SendWebRequest();
                    if (token != _token)
                    {
                        request.Dispose();
                        yield break;
                    }
                    clip = Content(request, out problem);
                }
                float waited = 0f;
                while (clip != null && clip.loadState == AudioDataLoadState.Loading && waited < LoadWait)
                {
                    yield return Poll;
                    waited += PollSeconds;
                    if (token != _token)
                    {
                        Release(null, clip, request);
                        yield break;
                    }
                }
                if (clip != null && clip.loadState == AudioDataLoadState.Failed)
                {
                    problem = "the engine could not decode it";
                    Release(null, clip, request);
                    clip = null;
                }
                if (clip == null)
                {
                    request?.Dispose();
                    Fail(path, problem);
                    failures++;
                    _index = GigMusicRules.Following(_index, _set.Count);
                    continue;
                }
                _request = request;
                _clip = clip;
                source.clip = clip;
                source.loop = GigMusicRules.LoopsAlone(_set.Count);
                source.volume = volume;
                source.Play();
                float started = Time.realtimeSinceStartup;
                yield return Poll;
                while (token == _token && source.isPlaying)
                {
                    yield return Poll;
                }
                if (token != _token)
                {
                    yield break;   // the stop has the clip and the reader now
                }
                if (Time.realtimeSinceStartup - started < TooShort)
                {
                    Fail(path, "it ended as soon as it started");
                    failures++;
                }
                else
                {
                    failures = 0;
                }
                Release(source, _clip, _request);
                _clip = null;
                _request = null;
                _index = GigMusicRules.Following(_index, _set.Count);
            }
            if (token == _token)
            {
                _gaveUp = true;
                _active = false;
                _log?.Info("Gig music: " + GigMusicRules.Name(_kind) + ": no file could be played; the gig's own sound plays instead.");
                otherwise?.Invoke();
            }
        }

        // A stream on the file, not decoded whole: a long song costs the disc, not the memory.
        private UnityWebRequest Ask(string path, out string problem)
        {
            problem = null;
            try
            {
                UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, TypeOf(path));
                var handler = request.downloadHandler as DownloadHandlerAudioClip;
                if (handler != null)
                {
                    handler.streamAudio = true;
                }
                return request;
            }
            catch (Exception e)
            {
                problem = "it could not be opened: " + e.Message;
                return null;
            }
        }

        private static AudioType TypeOf(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".wav": return AudioType.WAV;
                case ".ogg": return AudioType.OGGVORBIS;
                case ".aiff":
                case ".aif": return AudioType.AIFF;
                case ".flac": return (AudioType)7;   // as the game's own radio names it
                default: return AudioType.MPEG;      // .mp3 and .mp2
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
                if (clip == null)
                {
                    problem = "it is not a clip the engine can play";
                }
                return clip;
            }
            catch (Exception e)
            {
                problem = e.Message;
                return null;
            }
        }

        // The file is named once in a session, however many gigs or songs meet it.
        private void Fail(string path, string problem)
        {
            string name = Path.GetFileName(path);
            _log?.Warn("Gig music: " + GigMusicRules.Name(_kind) + ": '" + name + "' was left out: " + problem + ".");
            if (Told.Add(path))
            {
                JobText.Toast(UI.Notification.NotificationType.Warning, "reallife_gig_music_failed", "file", name.Length > 60 ? name.Substring(0, 57) + "..." : name, "folder", GigMusicRules.Name(_kind), 9f);
            }
        }

        private IEnumerator Fade(AudioSource source, int token, AudioClip clip, UnityWebRequest request)
        {
            float full = source.volume;
            for (int step = 1; step <= FadeSteps; step++)
            {
                yield return FadeStep;
                if (token != _token || source == null)
                {
                    break;   // a new start took the source over: only the old file is let go
                }
                source.volume = full * GigMusicRules.FadeGain(step * GigMusicRules.FadeSeconds / FadeSteps);
            }
            Release(token == _token ? source : null, clip, request);
            if (token == _token && source != null)
            {
                source.volume = full;
            }
        }

        // The source stops if it still holds this clip, and the clip and its reader are let go.
        private static void Release(AudioSource source, AudioClip clip, UnityWebRequest request)
        {
            try
            {
                if (source != null && source.clip == clip)
                {
                    source.Stop();
                    source.clip = null;
                }
                if (clip != null)
                {
                    UnityEngine.Object.Destroy(clip);
                }
                request?.Dispose();
            }
            catch (Exception)
            {
                // (already gone with the scene)
            }
        }
    }
}
