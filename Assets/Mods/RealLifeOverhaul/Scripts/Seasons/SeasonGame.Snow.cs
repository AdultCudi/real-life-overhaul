using System.Collections.Generic;
using System.Reflection;
using BigAmbitions.DayNightCycle;
using UnityEngine;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>
    /// What the falling snow borrows of the game's. Its flakes are drawn with the game's own soft round particle (the
    /// spray its wheels throw), found once a city behind the loading screen and used as it is, never changed. And while
    /// snow falls in the place of rain, the game's rain is kept out of sight and hearing on this machine: its particle
    /// renderers off and its sound muted (the wet look is kept off by <see cref="SnowHush"/>). The game's weather itself
    /// (raining or not, how hard, its hours) is never touched, so its people, traffic and Going Public's weather go on as
    /// the game has them, and everything is put back exactly when the snow stops.
    /// </summary>
    public static partial class SeasonGame
    {
        private const string FlakeMapProperty = "Texture2D_23DD87FD";   // the one picture of the game's particle shader graph
        private const string FlakeMap = "SmoothCircle";                  // its soft round dot
        private const string FlakePaintName = "WheelParticlesDrops";     // the game's wheel spray, drawn with that dot
        private const string OwnPrefix = "RealLifeOverhaul";             // the mod's own pieces hung where the rain falls are left alone

        private static readonly int FlakeMapId = Shader.PropertyToID(FlakeMapProperty);

        private static readonly List<Renderer> RainParts = new List<Renderer>(8);
        private static readonly List<bool> RainPartsOn = new List<bool>(8);
        private static readonly List<AudioSource> RainSounds = new List<AudioSource>(2);
        private static readonly List<bool> RainSoundsMuted = new List<bool>(2);
        private static readonly List<Renderer> RainFound = new List<Renderer>(8);
        private static Material _flakePaint;
        private static Transform _hushed;

        /// <summary>The game's soft round particle material, or null until it is found behind a loading screen.</summary>
        public static Material FlakePaint => _flakePaint;

        /// <summary>The name of the game's material the flakes are drawn with, for the log and the test; null while none.</summary>
        public static string FlakePaintFrom { get; private set; }

        /// <summary>The game's rain is kept out of sight and hearing now.</summary>
        public static bool RainHushed => _hushed != null;

        /// <summary>The game's rain particle renderers switched off, and its sounds muted, by the snow.</summary>
        public static int RainPartsHidden => RainParts.Count;
        public static int RainSoundsHushed => RainSounds.Count;

        /// <summary>The game's own rain is falling: its weather, which the snow leaves as it is.</summary>
        public static bool GameRaining => RainHelper.isRaining;

        /// <summary>The game's rain drops would be seen falling now, were they not hidden.</summary>
        public static bool GameDropsFalling => RainHelper.AreRainDropsFalling;

        /// <summary>The game's loading screen is up (else a door's black screen, for work done behind one).</summary>
        public static bool LoadingScreen => UI.Load.LoadScene.isLoading;

        /// <summary>
        /// Behind the loading screen: the game's soft round particle found among everything in memory, by its shader
        /// graph's picture (a name is read of the handful that have one). True when done or never to be, as a prewarm piece.
        /// </summary>
        public static bool FindFlakePaint()
        {
            if (_flakePaint != null)
            {
                return true;
            }
            if (!SceneFinds.Hidden)
            {
                return false;   // tried again behind the next black screen
            }
            Material any = null;
            foreach (Material material in SceneFinds.InMemory<Material>("seasons: the game's soft round particle looked for (everything in memory gone through)"))
            {
                if (material == null || !material.HasProperty(FlakeMapId))
                {
                    continue;
                }
                Texture map = material.GetTexture(FlakeMapId);
                if (map == null || map.name != FlakeMap)
                {
                    continue;
                }
                if (material.name == FlakePaintName)
                {
                    _flakePaint = material;
                    break;
                }
                any = any ?? material;
            }
            _flakePaint = _flakePaint != null ? _flakePaint : any;
            FlakePaintFrom = _flakePaint != null ? _flakePaint.name : null;
            SeasonDirector.Logger?.Info(_flakePaint != null
                ? "Seasons: snowflakes are drawn with the game's own soft round particle ('" + FlakePaintFrom + "', its '" + FlakeMap + "')."
                : "Seasons: the game's soft round particle was not found; snowflakes are drawn as small lit pieces instead.");
            return true;
        }

        /// <summary>
        /// While <paramref name="wanted"/>, the game's rain is out of sight and hearing: the renderers of its rain effect
        /// (drops, sheets, splashes) off and its sound muted, each as it was kept to be put back. Otherwise put back. Asked
        /// once a second; a new city's rain effect is taken over at the next ask.
        /// </summary>
        public static void HushRain(bool wanted)
        {
            if (!wanted)
            {
                UnhushRain();
            }
            else if (_hushed == null)
            {
                Hush();
            }
        }

        // The game's rain effect, read once and hidden: at the snow's start, and again for a new city's.
        private static void Hush()
        {
            Transform place = RainPlace();
            if (place == null)
            {
                return;
            }
            UnhushRain();
            _hushed = place;
            place.GetComponentsInChildren(true, RainFound);
            for (int i = 0; i < RainFound.Count; i++)
            {
                Renderer part = RainFound[i];
                if (part == null || part.name.StartsWith(OwnPrefix, System.StringComparison.Ordinal))
                {
                    continue;
                }
                RainParts.Add(part);
                RainPartsOn.Add(part.enabled);
                part.enabled = false;
            }
            RainFound.Clear();
            FieldInfo field = GameContract.RainSound.Field;
            var sound = field != null ? field.GetValue(null) as Component : null;
            if (sound != null)
            {
                sound.GetComponentsInChildren(true, RainSounds);
                for (int i = 0; i < RainSounds.Count; i++)
                {
                    RainSoundsMuted.Add(RainSounds[i] != null && RainSounds[i].mute);
                    if (RainSounds[i] != null)
                    {
                        RainSounds[i].mute = true;   // the game sets the volume and switches it on and off, never its mute
                    }
                }
            }
        }

        /// <summary>The game's rain seen and heard again, every renderer and sound as it was. Safe at any time.</summary>
        public static void UnhushRain()
        {
            for (int i = 0; i < RainParts.Count; i++)
            {
                if (RainParts[i] != null)
                {
                    RainParts[i].enabled = RainPartsOn[i];
                }
            }
            for (int i = 0; i < RainSounds.Count; i++)
            {
                if (RainSounds[i] != null)
                {
                    RainSounds[i].mute = RainSoundsMuted[i];
                }
            }
            RainParts.Clear();
            RainPartsOn.Clear();
            RainSounds.Clear();
            RainSoundsMuted.Clear();
            _hushed = null;
        }

        /// <summary>The city is unloading: the particle forgotten, as the next city finds its own.</summary>
        private static void ForgetSnow()
        {
            _flakePaint = null;
            FlakePaintFrom = null;
        }
    }
}
