using System.Collections.Generic;
using NWH.Common.Input;
using NWH.VehiclePhysics2.GroundDetection;
using NWH.VehiclePhysics2.Input;
using NWH.VehiclePhysics2.Powertrain;
using NWH.VehiclePhysics2.Sound;
using NWH.VehiclePhysics2.Sound.SoundComponents;
using UnityEngine;
using UnityEngine.Audio;
using NwhVehicle = NWH.VehiclePhysics2.VehicleController;

namespace RealLifeOverhaul
{
    /// <summary>What the engine is doing, read once a frame from the car's powertrain.</summary>
    public struct EngineReading
    {
        public bool Running;
        public float Rpm;
        public float IdleRpm;
        public float LimitRpm;
        public float Throttle;   // 0 to 1, as the engine sees it
    }

    /// <summary>The game's own sources a recorded engine stands in for, as they were before: given back by <see cref="CarSoundGame.Unmute"/>.</summary>
    public sealed class GameSounds
    {
        public const int Count = 5;   // the engine's note, its pops, the turbo's whistle and flutter, the tyres' skid

        public readonly AudioSource[] Sources = new AudioSource[Count];
        public readonly bool[] WasMuted = new bool[Count];
        public EngineRunningComponent Note;
        public float Distortion;      // the note's distortion at full load: the engine group's distortion the note sets every frame
        public bool Held;

        /// <summary>How many of the game's sources there were to quiet.</summary>
        public int Found
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Count; i++)
                {
                    n += Sources[i] != null ? 1 : 0;
                }
                return n;
            }
        }
    }

    /// <summary>
    /// The one place the recorded car sounds meet the game; each member a game update might change is read in a small
    /// method of its own. Nothing here runs unless a car is held.
    /// </summary>
    public static class CarSoundGame
    {
        private const int Note = 0, Pop = 1, Whistle = 2, Flutter = 3, Skid = 4;
        private const float DefaultSkid = 0.3f;   // a surface's skid level as the sound system's presets come: the recording's full level there

        /// <summary>The car's NWH vehicle with its sound system ready, or null (not an NWH car, no sound manager, no source holders).</summary>
        public static NwhVehicle Physics(CarController car)
        {
            NwhVehicle nwh = car != null ? car.vehicleController : null;
            SoundManager sound = nwh != null ? nwh.soundManager : null;
            if (sound == null || nwh.powertrain == null || nwh.powertrain.engine == null
                || sound.engineSourceGO == null || sound.exhaustSourceGO == null || sound.otherSourceGO == null || sound.engineMixerGroup == null)
            {
                return null;
            }
            return nwh;
        }

        /// <summary>What kind of car this is, as the driving feature reckons it (Driving/CarCatalogue).</summary>
        public static CarKind KindOf(CarController car)
        {
            Vehicles.VehicleTypes.VehicleType type = car != null ? car.vehicleType : null;
            if (type == null)
            {
                return CarKind.Untouched;
            }
            Rigidbody body = car.GetComponent<Rigidbody>();
            var traits = new CarTraits(type.IsMotorVehicle, type.spawnInPlayerObject, type.maxSpeed, type.maxCargoCapacity, type.isLuxuryCar, body != null ? body.mass : 0f);
            return CarCatalogue.For(type.vehicleTypeName, traits).Kind;
        }

        public static string TypeName(CarController car)
        {
            return car != null && car.vehicleType != null ? car.vehicleType.vehicleTypeName : null;
        }

        /// <summary>The engine this frame.</summary>
        public static void Read(NwhVehicle nwh, ref EngineReading reading)
        {
            EngineComponent engine = nwh.powertrain.engine;
            reading.Running = engine.ignition && engine.IsRunning && engine.canRun;
            reading.Rpm = engine.OutputRPM;
            reading.IdleRpm = engine.idleRPM;
            reading.LimitRpm = engine.revLimiterRPM;
            reading.Throttle = engine.ThrottlePosition;
        }

        /// <summary>The player's throttle key, added up over the game's input providers as the car's own input adds them.</summary>
        public static float ThrottleKey()
        {
            List<InputProvider> providers = InputProvider.Instances;
            float sum = 0f;
            for (int i = 0; providers != null && i < providers.Count; i++)
            {
                if (providers[i] is VehicleInputProviderBase vehicleInput)
                {
                    sum += vehicleInput.Throttle();
                }
            }
            return sum;
        }

        /// <summary>An audio source of the mod's on one of the car's source holders, set up as the game's sound system sets up its own.</summary>
        public static AudioSource Source(NwhVehicle nwh, int holder, bool loop, int priority)
        {
            SoundManager sound = nwh.soundManager;
            GameObject on = holder == 0 ? sound.engineSourceGO : holder == 1 ? sound.exhaustSourceGO : sound.otherSourceGO;
            AudioMixerGroup group = holder == 2 && sound.surfaceNoiseMixerGroup != null ? sound.surfaceNoiseMixerGroup : sound.engineMixerGroup;
            AudioSource source = on.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = group;
            source.spatialBlend = sound.spatialBlend;
            source.dopplerLevel = sound.dopplerLevel;
            source.playOnAwake = false;
            source.loop = loop;
            source.priority = priority;
            source.volume = 0f;
            return source;
        }

        /// <summary>The car's master volume (the game's sound system scales every source of the car by it).</summary>
        public static float Master(NwhVehicle nwh)
        {
            return nwh.soundManager.masterVolume;
        }

        /// <summary>Mutes the game's engine note, pops, turbo and skid sources on the car and takes the note's distortion off, recording it all first.</summary>
        public static void Mute(NwhVehicle nwh, GameSounds into)
        {
            SoundManager sound = nwh.soundManager;
            into.Note = sound.engineRunningComponent;
            Take(into, Note, sound.engineRunningComponent);
            Take(into, Pop, sound.exhaustPopComponent);
            Take(into, Whistle, sound.turboWhistleComponent);
            Take(into, Flutter, sound.turboFlutterComponent);
            Take(into, Skid, sound.wheelSkidComponent);
            if (into.Note != null)
            {
                into.Distortion = into.Note.maxDistortion;
                into.Note.maxDistortion = 0f;   // the note sets the whole engine group's distortion every frame: none on a recording
            }
            into.Held = true;
        }

        private static void Take(GameSounds into, int slot, SoundComponent component)
        {
            AudioSource source = component != null ? component.source : null;
            into.Sources[slot] = source;
            into.WasMuted[slot] = source != null && source.mute;
            if (source != null)
            {
                source.mute = true;
            }
        }

        /// <summary>Every one of the game's sources back as it was. Returns how many were given back (a car already gone counts none).</summary>
        public static int Unmute(GameSounds from)
        {
            int back = 0;
            if (!from.Held)
            {
                return 0;
            }
            for (int i = 0; i < GameSounds.Count; i++)
            {
                AudioSource source = from.Sources[i];
                if (source != null)
                {
                    source.mute = from.WasMuted[i];
                    back++;
                }
                from.Sources[i] = null;
            }
            if (from.Note != null)
            {
                from.Note.maxDistortion = from.Distortion;
            }
            from.Note = null;
            from.Held = false;
            return back;
        }

        /// <summary>The loudest wheel's squeal, 0 to 1, times the surface's skid level; 0 where the game plays no skid, and in the air.</summary>
        public static float Squeal(NwhVehicle nwh)
        {
            if (nwh.groundDetection == null || !nwh.groundDetection.state.isEnabled || nwh.powertrain.wheels == null)
            {
                return 0f;
            }
            SurfacePreset surface = nwh.groundDetection.DominantSurfacePreset;
            if (surface == null || !surface.playSkidSounds)
            {
                return 0f;
            }
            float loudest = 0f;
            float speed = nwh.Speed;
            for (int i = 0; i < nwh.powertrain.wheels.Count; i++)
            {
                WheelComponent wheel = nwh.powertrain.wheels[i];
                if (wheel == null || wheel.wheelUAPI == null || !wheel.wheelUAPI.IsGrounded
                    || !(wheel.wheelUAPI.IsSkiddingLaterally || wheel.wheelUAPI.IsSkiddingLongitudinally))
                {
                    continue;
                }
                float one = CarSoundRules.Squeal(wheel.wheelUAPI.NormalizedLateralSlip + wheel.wheelUAPI.NormalizedLongitudinalSlip, speed, wheel.wheelUAPI.AngularVelocity);
                loudest = one > loudest ? one : loudest;
            }
            float level = loudest * surface.skidSoundVolume / DefaultSkid;
            return level > 1f ? 1f : level;
        }
    }
}
