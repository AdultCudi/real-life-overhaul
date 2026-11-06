using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Destroys engine assets made by code together with what was made with them: destroying a volume profile leaves
    /// each setting added to it, and destroying a sprite leaves its texture. COST: nothing per frame; a Destroy per piece.
    /// </summary>
    internal static class CodeAssets
    {
        /// <summary>Destroys a profile made by code with every setting added to it. Safe with null.</summary>
        public static void DropProfile(VolumeProfile profile)
        {
            if (profile == null)
            {
                return;
            }
            List<VolumeComponent> settings = profile.components;
            for (int i = 0; settings != null && i < settings.Count; i++)
            {
                if (settings[i] != null)
                {
                    Object.Destroy(settings[i]);
                }
            }
            Object.Destroy(profile);
        }

        /// <summary>Destroys a sprite made with Sprite.Create and its texture. Safe with null.</summary>
        public static void DropSprite(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }
            Texture2D texture = sprite.texture;
            Object.Destroy(sprite);
            if (texture != null)
            {
                Object.Destroy(texture);
            }
        }
    }
}
