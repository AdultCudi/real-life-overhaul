using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A lamp never lit since the load is off to the game but keeps its lights enabled, each weighed every frame; this gives
    /// those the game's own off state, and the game lights them again itself. From the game's culling list, never a search.
    /// No cost a frame: a half-millisecond slice on the timer while sweeping, else a pass every five seconds.
    /// </summary>
    public sealed class StaleLampLights : PerfTweak
    {
        private sealed class Entry
        {
            public StreetLamp Lamp;
            public readonly List<Behaviour> Disabled = new List<Behaviour>(4);
            public GameObject Glow;
        }

        private const double SliceMs = 0.5;   // milliseconds of a tick a sweep may take
        private const int LookEvery = 16;   // lamps between two looks at the clock
        private const float SliceEvery = 0.05f;   // seconds between slices
        private const float SweepAfter = 4f;   // seconds after the last scene arrived before the lamps are looked through again
        private const float HiddenEvery = 0.25f;   // seconds between looks at whether the screen is black, while a sweep waits
        private const string Listing = "performance: the street lamps being listed (from the game's own list of the lamps it culls, after scenes arrive)";

        private readonly List<Entry> _entries = new List<Entry>(256);
        private readonly List<StreetLamp> _lamps = new List<StreetLamp>(4096);   // the lamps of the sweep under way (three thousand in the city)
        private List<StreetLamp> _sweeping;   // null when no sweep is under way
        private int _cursor;
        private bool _stale;
        private float _staleAt;
        private int _parts;

        // the lamps listed behind the loading screen, good for its city while no scene has arrived since
        private readonly List<StreetLamp> _kept = new List<StreetLamp>(4096);
        private PerfDirector _keptFor;
        private int _keptArrivals;

        public override string Id => "stale_lights";
        public override string LabelKey => "reallife_perf_stale_lights";
        public override bool DefaultOn => true;   // third benchmark: +0.10 ms, all three pairs the same way
        public override float Interval => _sweeping != null ? SliceEvery : _stale ? HiddenEvery : 5f;
        public override bool Busy => _sweeping != null;

        protected override void Apply()
        {
            if (!PerfGame.KnowsLamps)
            {
                throw new InvalidOperationException("this version of the game keeps a street lamp's lights, or its list of lamps, somewhere else");
            }
            if (Kept || SceneFinds.Hidden)
            {
                Begin();
            }
            else
            {
                _stale = true;   // on in play with no list made behind the loading screen: made behind the next black screen
                _staleAt = Time.unscaledTime;
            }
        }

        // a scene that has gone took its lamps with it (the timer drops those); only one that arrives brings new ones
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
            // a lamp the game has lit since is the game's again, both ways
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                Entry entry = _entries[i];
                if (entry.Lamp == null || LitSince(entry))
                {
                    _parts -= entry.Disabled.Count;
                    _entries.RemoveAt(i);
                }
            }
            if (_stale && now >= _staleAt && SceneFinds.Hidden)
            {
                Begin();
            }
        }

        // the game switches all of a lamp's lights on when it lights it
        private static bool LitSince(Entry entry)
        {
            for (int i = 0; i < entry.Disabled.Count; i++)
            {
                Behaviour part = entry.Disabled[i];
                if (part != null && part.enabled)
                {
                    return true;
                }
            }
            return false;
        }

        private bool Kept => _keptFor != null && ReferenceEquals(_keptFor, PerfDirector.Instance) && _keptArrivals == PerfTweaks.Arrivals;

        protected override void Prewarm()
        {
            using (SessionWatch.Piece(Listing))
            {
                PerfGame.Lamps(_kept);
            }
            _keptFor = PerfDirector.Instance;
            _keptArrivals = PerfTweaks.Arrivals;
        }

        // every lamp of the loaded scenes, from the list made behind the loading screen or the game's culling list, then
        // gone through a slice a tick
        private void Begin()
        {
            _stale = false;
            if (Kept)
            {
                _lamps.Clear();
                _lamps.AddRange(_kept);
            }
            else
            {
                using (SessionWatch.Piece(Listing))
                {
                    PerfGame.Lamps(_lamps);
                }
            }
            _kept.Clear();   // once: a scene that arrives later is listed again behind a black screen
            _keptFor = null;
            _sweeping = _lamps;
            _cursor = 0;
        }

        private void Sweep()
        {
            WorkSlice slice = WorkSlice.Of(SliceMs, LookEvery);
            for (; _cursor < _sweeping.Count && slice.More(); _cursor++)
            {
                StreetLamp lamp = _sweeping[_cursor];
                if (lamp == null)
                {
                    continue;
                }
                Light[] lights = PerfGame.LightsOf(lamp);
                HDAdditionalLightData[] data = PerfGame.LightDataOf(lamp);
                if ((!AnyOn(lights) && !AnyOn(data)) || PerfGame.IsOn(lamp))
                {
                    continue;   // nothing lit (no flag read), or lit by the game
                }
                Entry entry = null;
                Disable(lamp, lights, ref entry);
                Disable(lamp, data, ref entry);
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
            if (_cursor >= _sweeping.Count)
            {
                _sweeping = null;
                _lamps.Clear();   // nothing held of a city that may go
            }
        }

        private static bool AnyOn(Behaviour[] parts)
        {
            for (int i = 0; parts != null && i < parts.Length; i++)
            {
                if (parts[i] != null && parts[i].enabled)
                {
                    return true;
                }
            }
            return false;
        }

        // a lamp already held has no enabled light left, so a later sweep makes no second entry for it
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
                if (entry.Lamp == null || LitSince(entry))
                {
                    continue;   // gone, or lit by the game since
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
            _lamps.Clear();
            Faults.FaultDirector.KeepDark();   // a street lamp of the city's faults stays dark
        }

        public override string Describe() => "lamps the game has off whose lights were still enabled: " + _entries.Count + " lamps, " + _parts
                                             + " light components disabled";
    }
}
