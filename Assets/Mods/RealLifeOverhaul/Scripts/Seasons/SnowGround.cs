using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>
    /// Where lying snow may be drawn: the city's ground, roads, pavements and roofs, on the game's own physics layers for
    /// them, are given one decal layer of their own, and the snow's decal is drawn on that layer alone. Cars and people
    /// are never given it, so the snow cannot reach them whatever stands in its box, and the game's own decals are drawn
    /// as before (it keeps its default layer). Marked once a city behind the loading screen (one search of the scene's
    /// renderers) and put back exactly; nothing runs a frame.
    /// </summary>
    internal sealed class SnowGround
    {
        // The game's own physics layers (HGExtensions LayerHelper and the scene's own): the ground, roads and pavements
        // lie flat; buildings are their outsides, whose walls the decal's angle fade keeps white-free.
        private static readonly string[] Flat = { "Ground", "GroundUnplacable", "Roads", "Sidewalk" };
        private const string Roofs = "Buildings";

        private const int DecalBitsFrom = SeasonRules.DecalBitsFrom;
        private const int DecalBitsTo = SeasonRules.DecalBitsTo;

        private readonly List<Renderer> _marked = new List<Renderer>(4096);
        private readonly List<Renderer> _cleared = new List<Renderer>(16);   // others that carried the bit, given it back on unmarking
        private uint _bit;
        private int _already;

        /// <summary>The rendering layer bit the snow is on; 0 while nothing is marked.</summary>
        public uint Bit => Marked > 0 ? _bit : 0u;

        /// <summary>The decal layer for the snow's projector (the same bit, as the projector counts them).</summary>
        public DecalLayerEnum Layer => _bit != 0 ? (DecalLayerEnum)(_bit >> DecalBitsFrom) : DecalLayerEnum.Nothing;

        public int Marked => _marked.Count + _already;

        /// <summary>The game's pipeline can draw a decal on a layer of its own (HDRP with decals and decal layers in its asset).</summary>
        public static bool Drawable
        {
            get
            {
                var asset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
                return asset != null && asset.currentPlatformRenderPipelineSettings.supportDecals && asset.currentPlatformRenderPipelineSettings.supportDecalLayers;
            }
        }

        public int Roofed { get; private set; }
        public int Cleared { get; private set; }

        /// <summary>Why nothing could be marked, for the log and the test; null when the ground is marked.</summary>
        public string Missing { get; private set; } = "the ground is marked behind the next loading screen";

        /// <summary>
        /// Behind the loading screen: every renderer on the ground's and roofs' layers given the snow's decal layer. True
        /// when done (or never to be), as a prewarm piece; marks from an earlier city are let go first.
        /// </summary>
        public bool Mark()
        {
            Unmark();
            int flat = 0, roofs = LayerMask.NameToLayer(Roofs);
            for (int i = 0; i < Flat.Length; i++)
            {
                int layer = LayerMask.NameToLayer(Flat[i]);
                flat |= layer >= 0 ? 1 << layer : 0;
            }
            if (flat == 0)
            {
                Missing = "this build of the game has none of its ground layers by name";
                return true;
            }
            Renderer[] all = SceneFinds.All<Renderer>("seasons: the ground and roofs given the snow's decal layer", FindObjectsInactive.Include);
            // The bit fewest of everything else carry (none, as the game's renderers stand); what still carries it is
            // taken off it while marked, so nothing but the ground is ever on the snow's layer.
            var holders = new int[DecalBitsTo + 1];
            for (int i = 0; i < all.Length; i++)
            {
                if (Kind(all[i], flat, roofs) != 0)
                {
                    continue;
                }
                uint mask = all[i].renderingLayerMask;
                for (int b = DecalBitsFrom + 1; b <= DecalBitsTo; b++)   // never the default decal layer: the game's own decals are on it
                {
                    holders[b] += (mask & (1u << b)) != 0u ? 1 : 0;
                }
            }
            _bit = 1u << SeasonRules.SnowBit(holders);
            int roofed = 0;
            for (int i = 0; i < all.Length; i++)
            {
                Renderer renderer = all[i];
                int kind = Kind(renderer, flat, roofs);
                if (kind == 0)
                {
                    if (renderer != null && (renderer.renderingLayerMask & _bit) != 0u)
                    {
                        renderer.renderingLayerMask &= ~_bit;
                        _cleared.Add(renderer);
                    }
                    continue;
                }
                roofed += kind == 2 ? 1 : 0;
                if ((renderer.renderingLayerMask & _bit) != 0u)
                {
                    _already++;   // the game's own: left on it when unmarking
                    continue;
                }
                renderer.renderingLayerMask |= _bit;
                _marked.Add(renderer);
            }
            Roofed = roofed;
            Missing = Marked > 0 ? null : "the city has no renderer on its ground layers";
            Cleared = _cleared.Count;
            SeasonDirector.Logger?.Info("Seasons: " + Marked + " piece(s) of ground, road, pavement and roof (" + roofed + " roof) given the snow's own decal layer (rendering layer bit "
                                        + Bits(_bit) + (_cleared.Count > 0 ? "; " + _cleared.Count + " other renderer(s) carrying that bit taken off it while marked" : string.Empty)
                                        + "); cars and people are never on it, so the snow never lies on them.");
            return true;
        }

        /// <summary>Every renderer marked has its rendering layers as the game gave them. Safe at any time.</summary>
        public void Unmark()
        {
            for (int i = 0; i < _marked.Count; i++)
            {
                if (_marked[i] != null)
                {
                    _marked[i].renderingLayerMask &= ~_bit;
                }
            }
            for (int i = 0; i < _cleared.Count; i++)
            {
                if (_cleared[i] != null)
                {
                    _cleared[i].renderingLayerMask |= _bit;
                }
            }
            _marked.Clear();
            _cleared.Clear();
            Roofed = Cleared = _already = 0;
            Missing = "the ground is marked behind the next loading screen";
        }

        /// <summary>True if a renderer is on the snow's layer: for the test's sample of the cars and people near the player.</summary>
        public bool On(Renderer renderer)
        {
            return _bit != 0u && renderer != null && (renderer.renderingLayerMask & _bit) != 0u;
        }

        // 1 the ground, roads or pavements, 2 a roof, 0 anything else (cars, people, props, particles, lines).
        private static int Kind(Renderer renderer, int flat, int roofs)
        {
            if (renderer == null || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer || renderer is SkinnedMeshRenderer)
            {
                return 0;
            }
            int layer = renderer.gameObject.layer;
            return (flat & (1 << layer)) != 0 ? 1 : layer == roofs ? 2 : 0;
        }

        private static int Bits(uint bit)
        {
            int at = 0;
            while (bit > 1u)
            {
                bit >>= 1;
                at++;
            }
            return at;
        }
    }
}
