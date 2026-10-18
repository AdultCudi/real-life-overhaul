using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The recorded car sounds (the Garage's "Recorded car sounds" option): which car plays them, and every way back to
    /// the game's own. Only one car ever does: the car on the custom shop's turntable while the shop is open, else the car
    /// the player is driving, if it is the player's own (Going Public: never another player's) and a car with a recorded
    /// engine (Driving/Sound/CarSoundRules.VoiceFor). Traffic, the bus, the tow truck and the two-wheelers are never
    /// touched: none of them is the driven car or the turntable's.
    ///
    /// The game's own sound comes back on every way out, the frame it happens: getting out or into another car, the
    /// shop closing on a car nobody drives, a load (LoadScene), the city going (this component's end), the option or the
    /// Garage switched off, and a fault (logged once; the sounds then stay off for the session). With Developer
    /// diagnostics on, one "Car sound check" line on taking a car and one on giving it back.
    ///
    /// The cost a frame: idle, two reference reads and a comparison (and a retry twice a second while a car waits for its
    /// clips); with a car held, <see cref="CarSoundCar.Frame"/>, and its build looked at four times a second.
    /// </summary>
    public sealed class CarSoundDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("garage", "CarSoundDirector", typeof(CarSoundDirector));   // its Update (Core/Guard)

        private const float RetryEvery = 0.5f;   // seconds between tries on a car waiting for its clips
        private const float PartsEvery = 0.25f;  // seconds between looks at what the build fits (the shop's cart changes it)

        public static IModLogger Logger;

        public static CarSoundDirector Instance { get; private set; }

        private CarSoundCar _held;
        private CarController _wanted;       // the car last wanted, held or not
        private float _nextTry;
        private float _nextParts;
        private int _parts = -1;
        private bool _failed;
        private readonly HashSet<string> _toldWhy = new HashSet<string>();   // a car's reason for keeping the game's sound, said once

        /// <summary>The car playing recorded sounds now, or null.</summary>
        public CarController Held => _held != null ? _held.Car : null;

        private void Awake()
        {
            Instance = this;
        }

        private static readonly int HotUpdate = HotSpots.Slot("Garage: CarSoundDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                GiveBack("the feature was switched off after its faults");
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Fault(e);
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // A fault: the game's own sound back at once, and the recorded sounds off for the session.
        private void Fault(Exception e)
        {
            _failed = true;
            GiveBack("something unexpected");
            Logger?.Warn("Car sounds: switched off for the session after something unexpected; the car has the game's own sound again. " + e.Message);
        }

        private void OnUpdate()
        {
            if (_failed)
            {
                return;
            }
            VehicleController turntable = CarSoundBridges.Turntable != null ? CarSoundBridges.Turntable() : null;
            CarController want = Want(turntable);
            if (want != _wanted)
            {
                _wanted = want;
                _nextTry = 0f;
            }
            if (_held != null && _held.Car != want)
            {
                GiveBack(want == null ? Why() : "another car took its place");
            }
            float now = Time.unscaledTime;
            if (_held == null)
            {
                if (want != null && now >= _nextTry)
                {
                    _nextTry = now + RetryEvery;
                    TryTake(want);
                }
                return;
            }
            if (now >= _nextParts)
            {
                _nextParts = now + PartsEvery;
                int parts = CarSoundBridges.Parts != null ? CarSoundBridges.Parts(_held.Car) : 0;
                if (parts != _parts)
                {
                    _parts = parts;
                    _held.Parts(CarSoundBridges.ExhaustOf(parts), CarSoundBridges.TurboOf(parts));
                }
            }
            _held.Frame(now, Time.unscaledDeltaTime, turntable != null && turntable == _held.Car);
        }

        // The car that should play recorded sounds now, or null: the turntable's, else the driven car, if everything allows.
        private static CarController Want(VehicleController turntable)
        {
            if (!Settings.Garage || !Settings.GarageSounds || LoadScene.isLoading)
            {
                return null;
            }
            if (turntable is CarController onTable)
            {
                return onTable;
            }
            var gm = InstanceBehavior<GameManager>.Instance;
            return gm != null && gm.selectedVehicle is CarController driven && GoingPublic.MineVehicle(driven) ? driven : null;
        }

        // Why no car is wanted, for the leaving line.
        private static string Why()
        {
            return !Settings.Garage || !Settings.GarageSounds ? "the option was switched off" : LoadScene.isLoading ? "a load began" : "the player left the car";
        }

        private void TryTake(CarController car)
        {
            EngineVoice voice = CarSoundRules.VoiceFor(CarSoundGame.TypeName(car), CarSoundGame.KindOf(car));
            string why;
            CarSoundCar held = null;
            if (voice == EngineVoice.None)
            {
                why = "it is not a car with a recorded engine";
                _nextTry = float.MaxValue;   // (until another car is wanted)
            }
            else
            {
                held = CarSoundCar.Take(car, voice, out why);
            }
            if (held == null)
            {
                if ((CarSoundBank.Settled || voice == EngineVoice.None) && _toldWhy.Add(car.name + why))   // said once a car and reason, when it is final
                {
                    Logger?.Info("Car sounds: " + car.name + " keeps the game's own sound: " + why + ".");
                    if (Meta.Dev.Verbose)
                    {
                        Logger?.Info("Car sound check (entering a car): " + car.name + ", " + voice + " engine, clips " + CarSoundBank.Count + " of " + CarSoundBank.Wanted
                                     + " in memory: " + (voice == EngineVoice.None ? "PASS, not a car with a recorded engine (the game's own sound is right)." : "FAIL, " + why + "."));
                    }
                }
                return;
            }
            _held = held;
            _parts = -1;
            _nextParts = 0f;
            int loops = CarSoundRules.Loops(voice).Length;
            Logger?.Info("Car sounds: " + car.name + " plays the recorded " + voice + " engine (" + loops + " loops).");
            if (Meta.Dev.Verbose)
            {
                bool all = CarSoundBank.Has(voice);
                Logger?.Info("Car sound check (entering a car): " + car.name + ", " + voice + " engine of " + loops + " loops, clips " + CarSoundBank.Count + " of "
                             + CarSoundBank.Wanted + " in memory, " + held.Sources + " sources of the mod's on the car's own sound holders, " + held.GameFound
                             + " of the game's sources quieted: " + (all && held.Sources > 0 ? "PASS." : "FAIL, an engine clip is missing."));
            }
        }

        // The game's own sound back on the held car, whatever the reason.
        private void GiveBack(string reason)
        {
            CarSoundCar held = _held;
            if (held == null)
            {
                return;
            }
            _held = null;
            _parts = -1;
            int removed = 0;
            int back;
            try
            {
                back = held.Release(out removed);
            }
            catch (Exception e)
            {
                Logger?.Warn("Car sounds: the game's own sound could not all be given back to " + (held.Car != null ? held.Car.name : "a car that is gone") + ". " + e);
                return;
            }
            bool gone = held.Car == null;
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Car sound check (leaving a car): " + (gone ? "the car is gone with its sources" : held.Car.name) + " (" + reason + "), " + back + " of "
                             + held.GameFound + " of the game's sources back as they were, " + removed + " of the mod's taken off: "
                             + (gone || back == held.GameFound ? "PASS." : "FAIL, a source of the game's was not there to give back."));
            }
        }

        private void OnDestroy()
        {
            GiveBack("the city is going");
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ---------------------------------------------------------------- the test

        /// <summary>Options, Tests: what the recorded car sounds are doing now, in a notice and the log.</summary>
        public void TestReport()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            if (!Settings.Garage || !Settings.GarageSounds)
            {
                TestRefusals.Tell(Logger, "Car sounds", TestRefusal.FeatureOff);
                return;
            }
            if (_held == null && (gm == null || !(gm.selectedVehicle is CarController)))
            {
                TestRefusals.Tell(Logger, "Car sounds", TestRefusal.NotDriving);
                return;
            }
            var data = new Dictionary<string, string>
            {
                { "loaded", CarSoundBank.Count.ToString(CultureInfo.InvariantCulture) },
                { "wanted", CarSoundBank.Wanted.ToString(CultureInfo.InvariantCulture) }
            };
            Notifications.Show(NotificationType.Info, _held != null ? "reallife_garage_sounds_test_on" : "reallife_garage_sounds_test_wait", data, 6f);
            Logger?.Info("Car sounds: test: " + (_held != null ? _held.Car.name + " plays the recorded " + _held.Voice + " engine" : "the car keeps the game's own sound")
                         + "; " + CarSoundBank.Count + " of " + CarSoundBank.Wanted + " clips in memory.");
        }
    }
}
