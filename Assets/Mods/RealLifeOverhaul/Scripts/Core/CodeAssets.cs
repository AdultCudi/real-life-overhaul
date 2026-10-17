using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Engine assets the mod makes by code let go whole. Destroying the object that shows one does not free it, and some
    /// carry others made with them: each setting a volume profile is given (VolumeProfile.Add) is a ScriptableObject of
    /// its own that destroying the profile leaves behind, and a sprite made with Sprite.Create leaves its texture. These
    /// destroy both. Nothing per frame; a call costs a Destroy per piece.
    /// </summary>
    internal static class CodeAssets
    {
        /// <summary>A profile made with ScriptableObject.CreateInstance destroyed with every setting added to it. Safe with null.</summary>
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

        /// <summary>A sprite made with Sprite.Create destroyed with the texture made for it. Safe with null.</summary>
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
