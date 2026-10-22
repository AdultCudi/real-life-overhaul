using System;
using System.Collections.Generic;
using System.IO;
using BAModAPI;
using UnityEngine;
using UnityEngine.Networking;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The recorded car sounds, read once a session in the background as the mod starts and kept decoded (about 6 MB); until
    /// an engine's clips are in, that car keeps the game's sound. Nothing per frame; a failed file is said once.
    /// </summary>
    public static class CarSoundBank
    {
        private enum State { NotStarted, Loading, Done }

        public static IModLogger Logger;

        private static readonly Dictionary<string, AudioClip> Loaded = new Dictionary<string, AudioClip>(40);
        private static State _state = State.NotStarted;
        private static int _pending;
        private static int _failed;
        private static float _began;

        /// <summary>How many clips are in memory, of <see cref="Wanted"/>.</summary>
        public static int Count => Loaded.Count;

        public static int Wanted => CarSoundRules.AllClips.Length;

        /// <summary>True once every file has been read or has failed to.</summary>
        public static bool Settled => _state == State.Done;

        /// <summary>Starts the one-off reading of every clip. Safe to call again.</summary>
        public static void Load(ModContext context)
        {
            if (_state != State.NotStarted || context == null)
            {
                return;
            }
            _state = State.Loading;
            _began = Time.realtimeSinceStartup;
            string folder = Path.Combine(context.ModRootPath, CarSoundRules.Folder);
            string[] names = CarSoundRules.AllClips;
            _pending = names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                Read(folder, names[i]);
            }
        }

        /// <summary>A clip by name, or null while it is loading, if it failed, or if there is no such clip.</summary>
        public static AudioClip Get(string name)
        {
            return name != null && Loaded.TryGetValue(name, out AudioClip clip) ? clip : null;
        }

        /// <summary>Whether every clip of an engine is in memory (its loops and their off-throttle layers).</summary>
        public static bool Has(EngineVoice voice)
        {
            EngineLoop[] loops = CarSoundRules.Loops(voice);
            if (loops.Length == 0)
            {
                return false;
            }
            for (int i = 0; i < loops.Length; i++)
            {
                if (!Loaded.ContainsKey(loops[i].Clip) || (loops[i].HasOff && !Loaded.ContainsKey(loops[i].Clip + CarSoundRules.OffSuffix)))
                {
                    return false;
                }
            }
            return true;
        }

        private static void Read(string folder, string name)
        {
            string path = Path.Combine(folder, name + CarSoundRules.Extension);
            try
            {
                if (!File.Exists(path))
                {
                    Settle(name, "the file is not installed (" + path + ")");
                    return;
                }
                UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.OGGVORBIS);
                var handler = request.downloadHandler as DownloadHandlerAudioClip;
                if (handler != null)
                {
                    handler.streamAudio = false;   // decoded once, so a loop plays from memory and loops on the sample
                    handler.compressed = false;
                }
                UnityWebRequestAsyncOperation reading = request.SendWebRequest();
                reading.completed += Guard.Way<AsyncOperation>(_ => Done(name, request));
            }
            catch (Exception e)
            {
                Settle(name, "it could not be opened: " + e.Message);
            }
        }

        private static void Done(string name, UnityWebRequest request)
        {
            try
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Settle(name, "it did not read: " + request.error);
                    return;
                }
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null || clip.samples <= 0)
                {
                    Settle(name, "it is not a clip the engine can play");
                    return;
                }
                clip.name = "RealLifeOverhaul " + name;
                Loaded[name] = clip;
                Settle(name, null);
            }
            catch (Exception e)
            {
                Settle(name, "it could not be decoded: " + e.Message);
            }
            finally
            {
                request.Dispose();
            }
        }

        // One file is done, well or not; the last says how it went.
        private static void Settle(string name, string problem)
        {
            if (problem != null)
            {
                _failed++;
                Logger?.Warn("Car sounds: '" + name + "' is left out: " + problem + ". What needs it keeps the game's own sound.");
            }
            _pending--;
            if (_pending > 0)
            {
                return;
            }
            _state = State.Done;
            Logger?.Info("Car sounds: " + Loaded.Count + " of " + Wanted + " recorded clips read from the mod's folder in "
                         + ((Time.realtimeSinceStartup - _began) * 1000f).ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " ms"
                         + (_failed > 0 ? ", " + _failed + " left out." : "."));
        }
    }
}
