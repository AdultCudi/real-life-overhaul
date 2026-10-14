using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using Helpers;
using Streets.Pedestrians;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;

namespace RealLifeOverhaul.Leisure
{
    public enum LeisureCue { Tick, Good, Bad, Bite, Shutter }

    /// <summary>
    /// The sound pass. What the game already has is left to the game: footsteps are its own and
    /// already differ by surface (StepTrigger reads the ground's StepSound), rain has its own
    /// loops indoors and out (RainAudio), the car has a radio with four stations, and the mod's
    /// sirens are already positional with distance roll-off (Traffic/TrafficEffects). What the
    /// game does not have is a bed under the city: birds by day, crickets at night, a low hum of
    /// far traffic, a murmur where many people are on foot. The game ships no clips for those
    /// (none are in its code), so the four loops are made here, once, from arithmetic; the
    /// README lists them as sounds an asset pass can replace by name.
    ///
    /// Four looping 2D sources and one for cues, all made once. Their volumes are moved towards
    /// what the place and hour ask for on a quarter-second timer (never per frame); a layer at
    /// nothing is stopped, so a silent layer costs nothing, and with the option off there are no
    /// sources at all. Everything is routed to the game's own city mixer group and so follows
    /// its master and effects volume; the game's pause stops the lot.
    /// </summary>
    public sealed class LeisureSound : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("leisure", "LeisureSound", typeof(LeisureSound));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const int Rate = 22050;
        private const int Layers = 4;
        private const int Birds = 0, Crickets = 1, Hum = 2, Crowd = 3;

        public static IModLogger Logger;

        private static LeisureSound _instance;

        private readonly AudioSource[] _layer = new AudioSource[Layers];
        private readonly float[] _volume = new float[Layers];
        private readonly float[] _target = new float[Layers];
        private readonly AudioClip[] _cues = new AudioClip[5];
        private AudioSource _cueSource;
        private AudioMixerGroup _group;
        private float _nextTick;
        private float _nextCount;
        private int _crowdNear;
        private int _built;            // how many of the loops exist yet: one is made per tick, never four in a frame
        private readonly AudioClip[] _made = new AudioClip[Layers];   // loops made behind the loading screen, until their source takes them
        private bool _paused;
        private bool _failed;
        private bool _hooked;

        private void Awake()
        {
            _instance = this;
            ScenePrewarm.Add("leisure: the ambient sound's four loops being made (arithmetic, once a city)", PrewarmLoops);
        }

        // The four loops' arithmetic (some 650,000 samples, about 5 ms a loop, which were four frames of the first second
        // on the street after a load) done behind the loading screen while the ambient sound is on. The same numbers
        // whenever they are made: every loop is seeded. Build then only gives each loop its source.
        private static bool PrewarmLoops()
        {
            LeisureSound sound = _instance;
            if (sound == null || sound._failed || !Settings.Leisure || !Settings.AmbientSound)
            {
                return true;   // made on the timer if the sound is switched on later, as before
            }
            using var piece = SessionWatch.Piece("leisure: the ambient sound's four loops being made");
            for (int i = sound._built; i < Layers; i++)
            {
                if (sound._made[i] == null)
                {
                    sound._made[i] = Loop(i);
                }
            }
            return true;
        }

        private void OnDestroy()
        {
            Unhook();
            if (_instance == this)
            {
                _instance = null;
            }
            for (int i = 0; i < Layers; i++)
            {
                if (_layer[i] != null && _layer[i].clip != null)
                {
                    Destroy(_layer[i].clip);
                }
                if (_made[i] != null)
                {
                    Destroy(_made[i]);   // made behind the loading screen, never given a source
                }
            }
            for (int i = 0; i < _cues.Length; i++)
            {
                if (_cues[i] != null)
                {
                    Destroy(_cues[i]);
                }
            }
        }

        // ---------------------------------------------------------------- cues

        /// <summary>A short tone for something that happened in a game being played. Safe from anywhere; silent if sound is not up.</summary>
        public static void Cue(LeisureCue cue)
        {
            LeisureSound sound = _instance;
            if (sound == null || sound._failed)
            {
                return;
            }
            try
            {
                sound.PlayCue(cue);
            }
            catch (Exception e)
            {
                sound.Fail(e);
            }
        }

        private void PlayCue(LeisureCue cue)
        {
            int index = (int)cue;
            if (_cues[index] == null)
            {
                switch (cue)
                {
                    case LeisureCue.Good: _cues[index] = Tone("RealLifeOverhaul Cue Good", 0.22f, 660f, 990f, 0.5f); break;
                    case LeisureCue.Bad: _cues[index] = Tone("RealLifeOverhaul Cue Bad", 0.24f, 220f, 150f, 0.5f); break;
                    case LeisureCue.Bite: _cues[index] = Tone("RealLifeOverhaul Cue Bite", 0.16f, 1250f, 1250f, 0.6f); break;
                    case LeisureCue.Shutter: _cues[index] = Click("RealLifeOverhaul Cue Shutter"); break;
                    default: _cues[index] = Tone("RealLifeOverhaul Cue Tick", 0.07f, 880f, 880f, 0.4f); break;
                }
            }
            if (_cueSource == null)
            {
                _cueSource = Source("Cues", false);
            }
            _cueSource.PlayOneShot(_cues[index], 0.5f);
        }

        // ---------------------------------------------------------------- the bed

        private static readonly int HotUpdate = HotSpots.Slot("Leisure: LeisureSound.Update");
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
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (_failed)
            {
                return;
            }
            float now = Time.unscaledTime;
            if (now < _nextTick)
            {
                return;
            }
            _nextTick = now + 0.25f;
            try
            {
                Tick(now);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void Tick(float now)
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            bool live = gm != null && gm.playerController != null && SaveGameManager.Current != null && !UI.Load.LoadScene.isLoading;
            Radio(gm, live, now);

            bool on = live && Settings.Leisure && Settings.AmbientSound;
            bool paused = Time.timeScale == 0f;
            if (!on || paused)
            {
                if (!_paused)
                {
                    _paused = true;
                    for (int i = 0; i < Layers; i++)
                    {
                        if (_layer[i] != null && _layer[i].isPlaying)
                        {
                            if (on)
                            {
                                _layer[i].Pause();
                            }
                            else
                            {
                                _layer[i].Stop();
                                _volume[i] = 0f;
                            }
                        }
                    }
                }
                return;
            }
            if (_paused)
            {
                _paused = false;
                for (int i = 0; i < Layers; i++)
                {
                    if (_layer[i] != null && _volume[i] > 0f)
                    {
                        _layer[i].UnPause();
                    }
                }
            }

            if (_built < Layers)
            {
                Build(_built++);   // one loop a tick: a few milliseconds each, once a session
                return;
            }

            bool outdoors = !BuildingManager.IsInsideBuilding && !Parking.UndergroundParking.UndergroundParkingManager.IsInsideParking;
            bool raining = RainHelper.isRaining;
            int hour = TimeHelper.CurrentHour;
            float cabin = PlayerHelper.IsUsingVehicle ? 0.4f : 1f;   // windows up
            bool day = hour >= 6 && hour < 19;
            bool night = hour >= 21 || hour < 5;

            if (outdoors && now >= _nextCount)
            {
                _nextCount = now + 2f;
                _crowdNear = PeopleNear(gm.playerController.transform.position, 18f);
            }

            _target[Birds] = outdoors && day && !raining ? (hour < 9 ? 0.26f : 0.17f) * Settings.AmbientBirds / 100f * cabin : 0f;
            _target[Crickets] = outdoors && night && !raining ? 0.15f * Settings.AmbientNight / 100f * cabin : 0f;
            _target[Hum] = outdoors ? (day ? 0.13f : 0.08f) * Settings.AmbientCity / 100f * cabin : 0f;
            _target[Crowd] = outdoors ? Mathf.Clamp01((_crowdNear - 3) / 9f) * 0.16f * Settings.AmbientCrowd / 100f * cabin : 0f;

            for (int i = 0; i < Layers; i++)
            {
                AudioSource source = _layer[i];
                if (source == null)
                {
                    continue;
                }
                float next = Mathf.MoveTowards(_volume[i], _target[i], 0.02f);   // nothing to full in three seconds
                if (next == _volume[i])
                {
                    continue;
                }
                _volume[i] = next;
                source.volume = next;
                if (next > 0f && !source.isPlaying)
                {
                    source.Play();
                }
                else if (next <= 0f && source.isPlaying)
                {
                    source.Stop();
                }
            }
        }

        // People on foot within a distance: the game's walking pedestrians, counted on a two-second timer.
        private static int PeopleNear(Vector3 from, float within)
        {
            List<Pedestrian> walkers = PerfGame.Pedestrians;
            if (walkers == null)
            {
                return 0;
            }
            int near = 0;
            float sqr = within * within;
            for (int i = 0; i < walkers.Count; i++)
            {
                Pedestrian walker = walkers[i];
                if (walker != null && (walker.transform.position - from).sqrMagnitude < sqr)
                {
                    near++;
                }
            }
            return near;
        }

        /// <summary>Options-page button: each layer in turn at full for a few seconds, wherever the player is.</summary>
        public static void Test()
        {
            LeisureSound sound = _instance;
            if (sound == null || sound._failed || !Settings.Leisure || !Settings.AmbientSound)
            {
                TestRefusals.Tell(Logger, "Ambient sound", TestRefusal.FeatureOff);
                return;
            }
            if (sound._built < Layers)
            {
                TestRefusals.Tell(Logger, "Ambient sound", TestRefusal.NotLoaded, 0, "the loops are still being made");
                return;
            }
            sound.StartCoroutine(Guard.Routine(sound.Audition()));
        }

        private System.Collections.IEnumerator Audition()
        {
            string[] names = { "reallife_leisure_sound_birds", "reallife_leisure_sound_night", "reallife_leisure_sound_city", "reallife_leisure_sound_crowd" };
            for (int i = 0; i < Layers; i++)
            {
                AudioSource source = _layer[i];
                if (source == null)
                {
                    continue;
                }
                UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, names[i], null, 3f);
                _volume[i] = 0.3f;
                source.volume = 0.3f;
                if (!source.isPlaying)
                {
                    source.Play();
                }
                yield return new WaitForSecondsRealtime(3.5f);
                // Left where it is: the timer takes it back down (or keeps it, if it belongs here now).
            }
        }

        // ---------------------------------------------------------------- the radio's name on the HUD

        // The game's own car radio says nothing on screen when the station or the song changes: a notification names both
        // for a few seconds while driving. Its events are the game's; we only listen.
        private void Radio(GameManager gm, bool live, float now)
        {
            if (!live || !Settings.Leisure || !Settings.RadioHud || gm.radioPlayer == null)
            {
                Unhook();
            }
            else if (!_hooked)
            {
                _hooked = true;
                _onSong = _onSong ?? Guard.Listener<string>(OnSong);   // (made once, through the guard: Core/Guard)
                gm.radioPlayer.onNewSong.AddListener(_onSong);
            }
        }

        private void Unhook()
        {
            if (!_hooked)
            {
                return;
            }
            _hooked = false;
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm != null && gm.radioPlayer != null)
            {
                gm.radioPlayer.onNewSong.RemoveListener(_onSong);
            }
        }

        private UnityAction<string> _onSong;

        private void OnSong(string song)
        {
            try
            {
                var gm = InstanceBehavior<GameManager>.Instance;
                if (gm == null || gm.radioPlayer == null || gm.radioPlayer.IsMuted || !PlayerHelper.IsUsingVehicle || PhotoMode.Active || string.IsNullOrEmpty(song))
                {
                    return;
                }
                // The game's own car radio says nothing on screen when the song changes: one of the game's notifications does.
                LeisureDirector director = LeisureDirector.Instance;
                if (director != null && director.Screen != null)
                {
                    director.Screen.Say(JobText.T("reallife_leisure_radio_strip", "station", gm.radioPlayer.GetCurrentStation().ToString(), "song", song), 5f);
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // ---------------------------------------------------------------- sources

        private AudioSource Source(string label, bool loop)
        {
            var holder = new GameObject("RealLifeOverhaul Sound " + label);
            holder.transform.SetParent(transform, false);
            var source = holder.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = 200;
            source.volume = loop ? 0f : 1f;
            if (_group == null)
            {
                var references = InstanceBehavior<GlobalReferences>.Instance;
                _group = references != null ? (references.cityMixerGroup != null ? references.cityMixerGroup : references.foleyMixerGroup) : null;
                if (_group == null)
                {
                    Logger?.Warn("Sound: the game's mixer groups were not found; the mod's sounds will not follow the game's volume sliders.");
                }
            }
            source.outputAudioMixerGroup = _group;
            return source;
        }

        private void Build(int layer)
        {
            AudioClip clip = _made[layer];
            _made[layer] = null;
            if (clip == null)
            {
                clip = Loop(layer);
            }
            AudioSource source = Source(clip.name, true);
            source.clip = clip;
            _layer[layer] = source;
            _volume[layer] = 0f;
        }

        private void Fail(Exception e)
        {
            _failed = true;
            Logger?.Error(e);
            Logger?.Warn("Sound: the ambient layers are switched off for this session after the error above.");
            for (int i = 0; i < Layers; i++)
            {
                if (_layer[i] != null)
                {
                    _layer[i].Stop();
                }
            }
            Unhook();
        }

        // ---------------------------------------------------------------- arithmetic

        private static AudioClip Loop(int layer)
        {
            switch (layer)
            {
                case Birds: return BirdsLoop();
                case Crickets: return CricketsLoop();
                case Hum: return NoiseLoop("RealLifeOverhaul Ambience City", 8f, 0.035f, 0f, 0.25f, 0.9f, 4101);
                default: return NoiseLoop("RealLifeOverhaul Ambience Crowd", 8f, 0.22f, 0.05f, 4.5f, 0.7f, 977);
            }
        }

        private static AudioClip Make(string clipName, float[] samples)
        {
            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // A tone that slides from one pitch to another under a short envelope.
        private static AudioClip Tone(string clipName, float seconds, float from, float to, float level)
        {
            var samples = new float[(int)(seconds * Rate)];
            double phase = 0.0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)samples.Length;
                phase += 2.0 * Math.PI * Mathf.Lerp(from, to, t) / Rate;
                float envelope = Mathf.Min(1f, t * 30f) * (1f - t) * (1f - t);
                samples[i] = (float)Math.Sin(phase) * envelope * level;
            }
            return Make(clipName, samples);
        }

        // Two short bursts of noise: a shutter.
        private static AudioClip Click(string clipName)
        {
            var samples = new float[(int)(0.16f * Rate)];
            var random = new System.Random(7);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)Rate;
                float burst = Mathf.Exp(-t * 180f) + (t > 0.08f ? Mathf.Exp(-(t - 0.08f) * 140f) : 0f);
                samples[i] = (float)(random.NextDouble() * 2.0 - 1.0) * burst * 0.5f;
            }
            return Make(clipName, samples);
        }

        // Eight seconds of small birds: runs of short rising and falling notes between 2.4 and 5.5 kHz, with gaps.
        private static AudioClip BirdsLoop()
        {
            var samples = new float[8 * Rate];
            var random = new System.Random(2206);
            float at = 0.2f;
            while (at < 7.4f)
            {
                int notes = random.Next(2, 6);
                float pitch = 2400f + (float)random.NextDouble() * 2600f;
                float slide = ((float)random.NextDouble() - 0.4f) * 1400f;
                float noteLength = 0.06f + (float)random.NextDouble() * 0.09f;
                float level = 0.25f + (float)random.NextDouble() * 0.5f;
                for (int n = 0; n < notes && at < 7.7f; n++)
                {
                    int start = (int)(at * Rate);
                    int length = (int)(noteLength * Rate);
                    double phase = 0.0;
                    for (int i = 0; i < length && start + i < samples.Length; i++)
                    {
                        float t = i / (float)length;
                        phase += 2.0 * Math.PI * (pitch + slide * t) / Rate;
                        samples[start + i] += (float)Math.Sin(phase) * Mathf.Sin(t * Mathf.PI) * level * 0.5f;
                    }
                    at += noteLength + 0.03f + (float)random.NextDouble() * 0.05f;
                }
                at += 0.35f + (float)random.NextDouble() * 1.1f;
            }
            return Make("RealLifeOverhaul Ambience Birds", samples);
        }

        // Four seconds of crickets: two carriers, each chirping in threes. Whole numbers of every period, so the loop has no seam.
        private static AudioClip CricketsLoop()
        {
            var samples = new float[4 * Rate];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)Rate;
                float a = Chirp(t, 4200f, 2f, 0f);
                float b = Chirp(t, 3650f, 1.5f, 0.31f) * 0.6f;
                samples[i] = (a + b) * 0.35f;
            }
            return Make("RealLifeOverhaul Ambience Crickets", samples);
        }

        private static float Chirp(float t, float carrier, float chirpsPerSecond, float offset)
        {
            float cycle = (t * chirpsPerSecond + offset) % 1f;
            if (cycle > 0.42f)
            {
                return 0f;   // the rest between chirps
            }
            float pulse = Mathf.Sin(cycle / 0.42f * Mathf.PI * 3f);   // three pulses to a chirp
            return Mathf.Sin(2f * Mathf.PI * carrier * t) * pulse * pulse;
        }

        // Filtered noise with a slow swell: a far rumble (low cut-off) or a murmur (a band, wobbling at the pace of speech).
        // lowPass and highPass are one-pole coefficients (0 = off for the high pass); the ends are cross-faded so it loops.
        private static AudioClip NoiseLoop(string clipName, float seconds, float lowPass, float highPass, float wobbleHz, float level, int seed)
        {
            int length = (int)(seconds * Rate);
            int overlap = Rate / 2;
            var raw = new float[length + overlap];
            var random = new System.Random(seed);
            float low = 0f, slow = 0f, wobble = 0f, wobbleGoal = 0.5f;
            int wobbleEvery = Mathf.Max(1, (int)(Rate / Mathf.Max(0.05f, wobbleHz)));
            for (int i = 0; i < raw.Length; i++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                low += (white - low) * lowPass;
                float value = low;
                if (highPass > 0f)
                {
                    slow += (low - slow) * highPass;
                    value = low - slow;
                }
                if (i % wobbleEvery == 0)
                {
                    wobbleGoal = 0.35f + (float)random.NextDouble() * 0.65f;
                }
                wobble += (wobbleGoal - wobble) * (8f * wobbleHz / Rate + 0.00002f);
                raw[i] = value * wobble;
            }
            var samples = new float[length];
            float peak = 0.0001f;
            for (int i = 0; i < length; i++)
            {
                float value = raw[i];
                if (i < overlap)
                {
                    float t = i / (float)overlap;
                    value = value * t + raw[length + i] * (1f - t);
                }
                samples[i] = value;
                peak = Mathf.Max(peak, Mathf.Abs(value));
            }
            float gain = level / peak;
            for (int i = 0; i < length; i++)
            {
                samples[i] *= gain;
            }
            return Make(clipName, samples);
        }
    }
}
