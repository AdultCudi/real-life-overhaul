using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A street lamp's lights are switched by StreetLamp.SetLightVisibility, which the game only calls when the
    /// lamp changes between on and off. A lamp that has never been on since the city loaded is "off" to the game,
    /// but its Light components are still enabled as they were saved in the scene (114 garden lamps in the
    /// Hamptons in the first measurement, all 2.7 km from the player): each is a light the engine considers every
    /// frame and a script call per frame (HDAdditionalLightData.LateUpdate). This gives exactly those lamps the
    /// state the game gives a lamp it switches off: light, light data and glow object disabled. Brightness and
    /// the lamp's material are not touched, and the game switches everything on again by itself the moment it
    /// lights the lamp. Only lamps the game holds OFF are touched, so nothing the game means to be lit goes dark.
    /// </summary>
    public sealed class StaleLampLights : PerfTweak
    {
        private sealed class Entry
        {
            public StreetLamp Lamp;
            public readonly List<Behaviour> Disabled = new List<Behaviour>(4);
            public GameObject Glow;
        }

        private const int Slice = 250;          // lamps looked at per tick while a sweep is under way (three thousand in the city: a quarter of a second in all)
        private const float SliceEvery = 0.05f;
        private const float SweepAfter = 4f;    // seconds after the last scene arrived before the lamps are looked through again

        private readonly List<Entry> _entries = new List<Entry>(256);
        private StreetLamp[] _sweeping;         // the lamps being gone through; null when no sweep is under way
        private int _cursor;
        private bool _stale;
        private float _staleAt;
        private int _parts;

        public override string Id => "stale_lights";
        public override string LabelKey => "reallife_perf_stale_lights";
        public override bool DefaultOn => true;   // third benchmark: +0.10 ms, all three pairs the same way; the game's own off state, nothing to see
        public override float Interval => _sweeping != null ? SliceEvery : 5f;
        public override bool Busy => _sweeping != null;

        protected override void Apply()
        {
            if (!PerfGame.KnowsLamps)
            {
                throw new InvalidOperationException("this version of the game keeps a street lamp's lights somewhere else");
            }
            Begin();
        }

        // A scene that has gone took its lamps with it (the timer drops those); only one that has arrived can bring new ones.
        internal override void WorldChanged(bool loaded)
        {
            if (loaded)
            {
                _stale = true;
                _staleAt = Time.unscaledTime + SweepAfter;
            }
        }

        protected override void Tick(float now)
        {
            if (_sweeping != null)
            {
                Sweep();
                return;
            }
            // A lamp the game has lit since is the game's again: from then on it switches the lights itself, both ways.
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                Entry entry = _entries[i];
                if (entry.Lamp == null || PerfGame.IsOn(entry.Lamp))
                {
                    _parts -= entry.Disabled.Count;
                    _entries.RemoveAt(i);
                }
            }
            if (_stale && now >= _staleAt)
            {
                Begin();
            }
        }

        // Every lamp in the loaded scenes, when the tweak comes on and after a scene has arrived: the list is one engine
        // call, the lamps are then gone through a slice per tick (three private fields read of each).
        private void Begin()
        {
            _stale = false;
            _sweeping = UnityEngine.Object.FindObjectsByType<StreetLamp>(FindObjectsSortMode.None);
            _cursor = 0;
        }

        private void Sweep()
        {
            int end = Mathf.Min(_cursor + Slice, _sweeping.Length);
            for (; _cursor < end; _cursor++)
            {
                StreetLamp lamp = _sweeping[_cursor];
                if (lamp == null || PerfGame.IsOn(lamp))
                {
                    continue;
                }
                Entry entry = null;
                Disable(lamp, PerfGame.LightsOf(lamp), ref entry);
                Disable(lamp, PerfGame.LightDataOf(lamp), ref entry);
                if (entry == null)
                {
                    continue;
                }
                GameObject glow = PerfGame.GlowOf(lamp);
                if (glow != null && glow.activeSelf)
                {
                    glow.SetActive(false);
                    entry.Glow = glow;
                }
                _entries.Add(entry);
                _parts += entry.Disabled.Count;
            }
            if (_cursor >= _sweeping.Length)
            {
                _sweeping = null;
            }
        }

        // (A lamp this already holds has no enabled light left, so a later sweep makes no second entry for it.)

        private static void Disable(StreetLamp lamp, Behaviour[] parts, ref Entry entry)
        {
            if (parts == null)
            {
                return;
            }
            foreach (Behaviour part in parts)
            {
                if (part != null && part.enabled)
                {
                    entry ??= new Entry { Lamp = lamp };
                    part.enabled = false;
                    entry.Disabled.Add(part);
                }
            }
        }

        protected override void Restore()
        {
            foreach (Entry entry in _entries)
            {
                if (entry.Lamp == null || PerfGame.IsOn(entry.Lamp))
                {
                    continue;   // gone, or lit by the game since: its lights are as the game wants them
                }
                foreach (Behaviour part in entry.Disabled)
                {
                    if (part != null)
                    {
                        part.enabled = true;
                    }
                }
                if (entry.Glow != null)
                {
                    entry.Glow.SetActive(true);
                }
            }
            _entries.Clear();
            _parts = 0;
            _stale = false;
            _sweeping = null;
        }

        public override string Describe() => "lamps the game has off whose lights were still enabled: " + _entries.Count + " lamps, " + _parts + " light components disabled";
    }
}
