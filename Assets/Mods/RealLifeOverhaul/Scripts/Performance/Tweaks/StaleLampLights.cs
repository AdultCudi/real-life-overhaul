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
    ///
    /// The lamps are read from the game's own list of what it culls by distance, which every lamp joins in its
    /// Start, never by a search of the scene (that was a frame of 83 ms the moment the tweak came on after a load);
    /// that list is read behind the loading screen (Prewarm), and after a scene has arrived behind the next black
    /// screen or pause (it was read in play, in a frame of 93 ms of which this director had 19.7 ms: the owner's log).
    ///
    /// Whether the game has lit a lamp since is told by its lights themselves: the game switches every light of a lamp
    /// on whenever it lights it (StreetLamp.SetLightVisibility, from InstantlyTurnOn and ChangeLightSetting), and only
    /// then, and these are lights this switched off; so a held lamp with a light on again is the game's, with no
    /// reflection (the five-second look used to read the lamp's private flag of every held lamp, some 3,000 boxed reads
    /// and 60 KB every five seconds). A sweep reads that flag only of a lamp whose lights are on.
    ///
    /// Per frame: nothing; on its timer, a slice of half a millisecond while a sweep is under way, otherwise one pass
    /// over the lamps it holds every five seconds (one enabled test each).
    /// </summary>
    public sealed class StaleLampLights : PerfTweak
    {
        private sealed class Entry
        {
            public StreetLamp Lamp;
            public readonly List<Behaviour> Disabled = new List<Behaviour>(4);
            public GameObject Glow;
        }

        private const double SliceMs = 0.5;     // milliseconds of a tick a sweep under way may take (some hundreds of lamps; three thousand in the city)
        private const int LookEvery = 16;       // lamps between two looks at the clock
        private const float SliceEvery = 0.05f;
        private const float SweepAfter = 4f;    // seconds after the last scene arrived before the lamps are looked through again
        private const float HiddenEvery = 0.25f; // seconds between two looks at whether the screen is black, while a sweep waits for it
        private const string Listing = "performance: the street lamps being listed (from the game's own list of the lamps it culls, after scenes arrive)";

        private readonly List<Entry> _entries = new List<Entry>(256);
        private readonly List<StreetLamp> _lamps = new List<StreetLamp>(4096);   // the lamps of the sweep under way (three thousand in the city)
        private List<StreetLamp> _sweeping;     // the lamps being gone through; null when no sweep is under way
        private int _cursor;
        private bool _stale;
        private float _staleAt;
        private int _parts;

        // The lamps listed behind the loading screen (Prewarm), for the sweep when the tweak comes on: good for the city they
        // were listed in (its PerfDirector) while no scene has arrived since.
        private readonly List<StreetLamp> _kept = new List<StreetLamp>(4096);
        private PerfDirector _keptFor;
        private int _keptArrivals;

        public override string Id => "stale_lights";
        public override string LabelKey => "reallife_perf_stale_lights";
        public override bool DefaultOn => true;   // third benchmark: +0.10 ms, all three pairs the same way; the game's own off state, nothing to see
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
                _stale = true;   // switched on in play with no list made behind the loading screen: made behind the next black screen
                _staleAt = Time.unscaledTime;
            }
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

        // True if the game has lit the lamp since this switched its lights off: it switches all of them on when it does (see the summary).
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

        // The list made behind the loading screen is this city's, and no scene has arrived since.
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

        // Every lamp in the loaded scenes, when the tweak comes on (the list made behind the loading screen, where there is one)
        // and after a scene has arrived (behind the next black screen), read from the game's own list of the lamps it culls
        // (PerfGame.Lamps: one pass over that list, where a search of the scene was a frame of 83 ms in play); the lamps are
        // then gone through a slice per tick.
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
                    continue;   // nothing of it lit (nothing to switch off, and no flag read), or lit by the game
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
                if (entry.Lamp == null || LitSince(entry))
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
            _lamps.Clear();
            Faults.FaultDirector.KeepDark();   // (a street lamp of the city's faults stays dark: what was put back here of it is switched off again)
        }

        public override string Describe() => "lamps the game has off whose lights were still enabled: " + _entries.Count + " lamps, " + _parts + " light components disabled";
    }
}
