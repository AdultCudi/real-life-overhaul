using UnityEngine;
using NwhVehicle = NWH.VehiclePhysics2.VehicleController;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One car playing its recorded engine: the loops of its <see cref="EngineVoice"/> on audio sources of the mod's own,
    /// put on the car's own sound holders (the NWH SoundManager's engine, exhaust and other source objects, on its own mixer
    /// groups, so the game's volume, its in-car muffling and its distance falloff apply as to the game's own), and the
    /// game's own note, pops, turbo and skid sources on that car muted meanwhile (<see cref="GameSounds"/>). The NWH engine
    /// sound plays one clip pitched by the revs and cannot cross between recordings; this does, by
    /// <see cref="CarSoundRules"/>, from the powertrain's revs and throttle (or, on the shop's turntable, from revs of its
    /// own on the throttle key). <see cref="Release"/> takes every source of the mod's off and gives the game's back.
    ///
    /// The cost while a car is held: once a frame, the revs and throttle read, a handful of multiplications, the volume and
    /// pitch of the sounding sources set, the wheels' slip read (four wheels); a source with nothing to play is stopped.
    /// Nothing is allocated after the car is taken.
    /// </summary>
    public sealed class CarSoundCar
    {
        private const float Audible = 0.0005f;   // below this a loop is stopped rather than played silent

        private readonly CarController _car;
        private readonly NwhVehicle _nwh;
        private readonly EngineVoice _voice;
        private readonly EngineLoop[] _loops;
        private readonly int _count;
        private readonly float[] _at = new float[CarSoundRules.MostLoops];
        private readonly float[] _gains = new float[CarSoundRules.MostLoops];
        private readonly AudioSource[] _on = new AudioSource[CarSoundRules.MostLoops];
        private readonly AudioSource[] _off = new AudioSource[CarSoundRules.MostLoops];
        private readonly AudioClip[] _pops;
        private readonly GameSounds _game = new GameSounds();
        private readonly System.Random _random = new System.Random(20261018);   // the pops' timing and pitch: one machine's sound alone
        private AudioSource _pop, _whistle, _flutter, _squeal;
        private AudioClip _flutterClip;
        private EngineReading _reading;
        private ExhaustSound _exhaust = CarSoundRules.Exhaust(0);
        private TurboSound _turbo = CarSoundRules.Turbo(0);
        private float _placedIdle, _placedLimit;
        private float _shopRpm;
        private float _lastHigh = -10f;     // when the throttle was last at least LiftFrom
        private float _liftAt = -10f;       // when it was last lifted
        private bool _lifting;
        private float _lastPop = -10f;
        private int _nextPop;
        private float _boost;
        private float _heard;
        private int _sources;

        private CarSoundCar(CarController car, NwhVehicle nwh, EngineVoice voice)
        {
            _car = car;
            _nwh = nwh;
            _voice = voice;
            _loops = CarSoundRules.Loops(voice);
            _count = _loops.Length;
            _pops = new AudioClip[CarSoundRules.Pops.Length];
        }

        public CarController Car => _car;

        public EngineVoice Voice => _voice;

        /// <summary>How many of the car's own sources were quieted, and how many of the mod's were put on.</summary>
        public int GameFound => _game.Found;

        public int Sources => _sources;

        /// <summary>The revs the turntable shows, for the self-check line.</summary>
        public float Rpm => _reading.Rpm;

        /// <summary>
        /// Puts the recorded engine on a car: null (and the car untouched) if it is not a car with the game's sound system
        /// or this engine's clips are not all in memory. <paramref name="why"/> says which.
        /// </summary>
        public static CarSoundCar Take(CarController car, EngineVoice voice, out string why)
        {
            NwhVehicle nwh = CarSoundGame.Physics(car);
            if (nwh == null)
            {
                why = "the car has no sound system of the game's to put it on";
                return null;
            }
            if (!CarSoundBank.Has(voice))
            {
                why = "the " + voice + " engine's clips are not all in memory (" + CarSoundBank.Count + " of " + CarSoundBank.Wanted + " read)";
                return null;
            }
            var held = new CarSoundCar(car, nwh, voice);
            held.Build();
            why = null;
            return held;
        }

        private void Build()
        {
            for (int i = 0; i < _count; i++)
            {
                _on[i] = Loop(_loops[i].Clip, 10 + i);
                _off[i] = _loops[i].HasOff ? Loop(_loops[i].Clip + CarSoundRules.OffSuffix, 10 + i) : null;
            }
            for (int i = 0; i < _pops.Length; i++)
            {
                _pops[i] = CarSoundBank.Get(CarSoundRules.Pops[i]);
            }
            _pop = CarSoundGame.Source(_nwh, 1, false, 150);
            _sources++;
            AudioClip whistle = CarSoundBank.Get(CarSoundRules.WhistleClip);
            if (whistle != null)
            {
                _whistle = CarSoundGame.Source(_nwh, 0, true, 40);
                _whistle.clip = whistle;
                _sources++;
            }
            _flutterClip = CarSoundBank.Get(CarSoundRules.FlutterClip);
            if (_flutterClip != null)
            {
                _flutter = CarSoundGame.Source(_nwh, 0, false, 120);
                _sources++;
            }
            AudioClip squeal = CarSoundBank.Get(CarSoundRules.SquealClip);
            if (squeal != null)
            {
                _squeal = CarSoundGame.Source(_nwh, 2, true, 60);
                _squeal.clip = squeal;
                _sources++;
            }
            CarSoundGame.Mute(_nwh, _game);
        }

        private AudioSource Loop(string clip, int priority)
        {
            AudioSource source = CarSoundGame.Source(_nwh, 0, true, priority);
            source.clip = CarSoundBank.Get(clip);
            _sources++;
            return source;
        }

        /// <summary>What the car's build fits: the exhaust's loudness (0 to 2) and the turbo's size (0 to 2).</summary>
        public void Parts(int exhaust, int turbo)
        {
            _exhaust = CarSoundRules.Exhaust(exhaust);
            _turbo = CarSoundRules.Turbo(turbo);
        }

        /// <summary>
        /// One frame of sound. <paramref name="turntable"/>: the car is held in the shop, and its revs are the sound's own on
        /// the throttle key; otherwise they are the engine's.
        /// </summary>
        public void Frame(float now, float dt, bool turntable)
        {
            CarSoundGame.Read(_nwh, ref _reading);
            float throttle;
            if (turntable)
            {
                bool held = CarSoundGame.ThrottleKey() > 0.5f;
                _shopRpm = CarSoundRules.ShopRevs(_shopRpm, held, _reading.IdleRpm, _reading.LimitRpm, dt);
                _reading.Rpm = _shopRpm;
                _reading.Running = true;
                throttle = held ? 1f : 0f;
            }
            else
            {
                _shopRpm = _reading.Rpm;
                throttle = _reading.Throttle;
            }
            if (_reading.IdleRpm != _placedIdle || _reading.LimitRpm != _placedLimit)
            {
                _placedIdle = _reading.IdleRpm;
                _placedLimit = _reading.LimitRpm;
                CarSoundRules.Place(_voice, _placedIdle, _placedLimit, _at);
            }
            float master = CarSoundGame.Master(_nwh);
            if (!_reading.Running)
            {
                Silence();
                return;
            }
            float limit = _reading.LimitRpm > 1f ? _reading.LimitRpm : 1f;
            float revs = _reading.Rpm / limit;
            float on = CarSoundRules.OnShare(throttle);
            float level = CarSoundRules.Level(revs, on, _exhaust);
            CarSoundRules.Mix(_reading.Rpm, _at, _count, _gains);
            float onLayer = Mathf.Sqrt(on);
            float offLayer = Mathf.Sqrt(1f - on);
            for (int i = 0; i < _count; i++)
            {
                float pitch = CarSoundRules.PitchOf(_reading.Rpm, _at[i]) * _exhaust.Pitch;
                if (_off[i] == null)
                {
                    Set(_on[i], CarSoundRules.Volume(_gains[i], 1f, level, _exhaust) * master, pitch);
                    continue;
                }
                Set(_on[i], CarSoundRules.Volume(_gains[i], onLayer, level, _exhaust) * master, pitch);
                Set(_off[i], CarSoundRules.Volume(_gains[i], offLayer, level, _exhaust) * master, pitch);
            }

            // Lifting off: pops from a louder exhaust, the turbo's flutter on boost.
            if (throttle >= CarSoundRules.LiftFrom)
            {
                _lastHigh = now;
                _lifting = false;
            }
            bool lifted = !_lifting && CarSoundRules.Lifted(throttle, now - _lastHigh);
            if (lifted)
            {
                _lifting = true;
                _liftAt = now;
            }
            float chance = CarSoundRules.PopChance(_exhaust, revs, throttle, now - _liftAt, now - _lastPop, dt);
            if (chance > 0f && _random.NextDouble() < chance)
            {
                PopOnce(master);
                _lastPop = now;
            }
            _boost = CarSoundRules.Boost(_boost, throttle, revs, dt, _turbo);
            if (CarSoundRules.Flutters(_turbo, lifted, _boost) && _flutter != null)
            {
                _flutter.pitch = 0.92f + 0.16f * (float)_random.NextDouble();
                _flutter.PlayOneShot(_flutterClip, Mathf.Clamp01(_turbo.FlutterVolume * _boost) * master);
            }
            if (_whistle != null)
            {
                Set(_whistle, Mathf.Clamp01(CarSoundRules.WhistleVolume(_boost, _turbo)) * master, CarSoundRules.WhistlePitch(_boost, _turbo));
            }

            // The tyres, on the road only.
            if (_squeal != null)
            {
                float loudest = turntable ? 0f : CarSoundGame.Squeal(_nwh);
                _heard = CarSoundRules.SquealEased(_heard, loudest > 1f ? 1f : loudest, dt);
                Set(_squeal, _heard < CarSoundRules.SkidFrom ? 0f : CarSoundRules.SkidVolume * _heard * master, 0.92f + 0.16f * _heard);
            }
        }

        private void PopOnce(float master)
        {
            AudioClip clip = null;
            for (int tries = 0; tries < _pops.Length && clip == null; tries++)
            {
                clip = _pops[_nextPop];
                _nextPop = (_nextPop + 1) % _pops.Length;
            }
            if (clip == null || _pop == null)
            {
                return;
            }
            _pop.pitch = 0.85f + 0.3f * (float)_random.NextDouble();
            _pop.PlayOneShot(clip, Mathf.Clamp01(_exhaust.PopVolume * (0.7f + 0.3f * (float)_random.NextDouble())) * master);
        }

        // A source at this volume and pitch: started when it has something to play, stopped when it has not.
        private static void Set(AudioSource source, float volume, float pitch)
        {
            if (source == null)
            {
                return;
            }
            if (volume < Audible)
            {
                if (source.isPlaying)
                {
                    source.Stop();
                }
                return;
            }
            source.volume = volume;
            source.pitch = pitch;
            if (!source.isPlaying && source.clip != null)
            {
                source.Play();
            }
        }

        private void Silence()
        {
            for (int i = 0; i < _count; i++)
            {
                Set(_on[i], 0f, 1f);
                Set(_off[i], 0f, 1f);
            }
            Set(_whistle, 0f, 1f);
            Set(_squeal, 0f, 1f);
            _boost = 0f;
            _heard = 0f;
        }

        /// <summary>
        /// The car as the game made it: the game's own sources unmuted and its note's distortion back, every source of the
        /// mod's taken off. Safe on a car already gone. Returns how many of the game's sources were given back.
        /// </summary>
        public int Release(out int removed)
        {
            removed = 0;
            int back = CarSoundGame.Unmute(_game);
            for (int i = 0; i < _count; i++)
            {
                removed += Drop(ref _on[i]);
                removed += Drop(ref _off[i]);
            }
            removed += Drop(ref _pop);
            removed += Drop(ref _whistle);
            removed += Drop(ref _flutter);
            removed += Drop(ref _squeal);
            _sources = 0;
            return back;
        }

        private static int Drop(ref AudioSource source)
        {
            if (source == null)
            {
                source = null;   // (gone with the car: nothing to take off)
                return 0;
            }
            source.Stop();
            Object.Destroy(source);
            source = null;
            return 1;
        }
    }
}
