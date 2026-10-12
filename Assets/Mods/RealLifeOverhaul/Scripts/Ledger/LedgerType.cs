using System;
using System.Text;
using Enums;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// The City Ledger's typeface: a serif, as a newspaper is set. First the serif the game ships itself (Noto Serif, one of
    /// the faces it offers for shop signs, loaded with the city), else any serif among the game's loaded fonts, else one of
    /// the operating system's serifs made into a font asset at runtime (Georgia, Times New Roman or Cambria on Windows;
    /// Liberation Serif or DejaVu Serif under Linux and Proton), with its bold and italic faces where the system has them;
    /// else none, and the paper keeps the game's own font. No font file ships with the mod.
    ///
    /// Looked up when the page is built (once a city). One line in Player.log, once a session, says which face the paper is
    /// set in and why, with the names of the game's loaded font assets. A face made from the system is made once a
    /// session and kept.
    /// </summary>
    public static class LedgerType
    {
        private static readonly string[] SystemSerifs = { "Georgia", "Times New Roman", "Cambria", "Liberation Serif", "DejaVu Serif" };
        private static readonly string[] SerifNames = { "Serif", "Georgia", "Times", "Cambria", "Garamond", "Baskerville", "Caslon" };

        private static TMP_FontAsset _made;   // made from the system's fonts this session
        private static string _madeFrom;
        private static bool _triedSystem;
        private static bool _told;

        /// <summary>The paper's face, or null for the game's own font.</summary>
        public static TMP_FontAsset Find()
        {
            TMP_FontAsset face = null;
            string why = null;
            try
            {
                var refs = InstanceBehavior<GlobalReferences>.Instance;
                face = refs != null ? refs.GetFontByName(FontFace.NotoSerif) : null;
            }
            catch (Exception)
            {
                face = null;   // the game's list of sign faces is not as this version expects: looked for by name below
            }
            if (face != null)
            {
                why = "the serif the game ships for shop signs";
            }
            TMP_FontAsset[] loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (face == null)
            {
                face = Serif(loaded);
                if (face != null)
                {
                    why = "a serif among the game's fonts";
                }
            }
            if (face == null)
            {
                face = FromSystem();
                if (face != null)
                {
                    why = "the game has no serif loaded, so it is made from the system's " + _madeFrom;
                }
            }
            if (!_told)
            {
                _told = true;
                LedgerDirector.Logger?.Info("City Ledger: the paper is set in " + (face != null ? Family(face) + ", " + why : "the game's own font: no serif in the game or on the system")
                                            + ". The game's font assets: " + Names(loaded) + ".");
            }
            return face;
        }

        // The first serif among the loaded font assets (not one the game keeps as a fallback for other scripts).
        private static TMP_FontAsset Serif(TMP_FontAsset[] all)
        {
            for (int i = 0; all != null && i < all.Length; i++)
            {
                TMP_FontAsset f = all[i];
                if (f == null || f.name.IndexOf("Fallback", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }
                string family = Family(f);
                if (family.IndexOf("Sans", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }
                for (int n = 0; n < SerifNames.Length; n++)
                {
                    if (family.IndexOf(SerifNames[n], StringComparison.OrdinalIgnoreCase) >= 0 || f.name.IndexOf(SerifNames[n], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return f;
                    }
                }
            }
            return null;
        }

        // A serif of the operating system's as a dynamic font asset, its bold and italic faces in its weight table so that
        // bold and italic are the real faces, not drawn heavier or slanted. Tried once a session.
        private static TMP_FontAsset FromSystem()
        {
            if (_made != null || _triedSystem)
            {
                return _made;
            }
            _triedSystem = true;
            for (int i = 0; i < SystemSerifs.Length; i++)
            {
                TMP_FontAsset regular = Make(SystemSerifs[i], "Regular");
                if (regular == null)
                {
                    continue;
                }
                TMP_FontWeightPair[] weights = regular.fontWeightTable;
                if (weights != null && weights.Length > 7)
                {
                    weights[7].regularTypeface = Make(SystemSerifs[i], "Bold");
                    weights[4].italicTypeface = Make(SystemSerifs[i], "Italic");
                    weights[7].italicTypeface = Make(SystemSerifs[i], "Bold Italic");
                }
                _made = regular;
                _madeFrom = SystemSerifs[i];
                return regular;
            }
            return null;
        }

        private static TMP_FontAsset Make(string family, string style)
        {
            TMP_FontAsset f;
            try
            {
                f = TMP_FontAsset.CreateFontAsset(family, style);
            }
            catch (Exception)
            {
                f = null;   // the system has no such face, or its shader is missing from this build
            }
            if (f == null)
            {
                return null;
            }
            // made at runtime and kept for the session: never unloaded with a scene's unused assets
            f.name = family + " " + style + " (City Ledger)";
            f.hideFlags = HideFlags.DontUnloadUnusedAsset;
            if (f.material != null)
            {
                f.material.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            Texture2D[] atlases = f.atlasTextures;
            for (int i = 0; atlases != null && i < atlases.Length; i++)
            {
                if (atlases[i] != null)
                {
                    atlases[i].hideFlags = HideFlags.DontUnloadUnusedAsset;
                }
            }
            return f;
        }

        private static string Family(TMP_FontAsset f)
        {
            string family = f.faceInfo.familyName;
            return string.IsNullOrEmpty(family) ? f.name : family;
        }

        private static string Names(TMP_FontAsset[] all)
        {
            var b = new StringBuilder(256);
            for (int i = 0; all != null && i < all.Length; i++)
            {
                if (all[i] != null)
                {
                    b.Append(b.Length > 0 ? ", " : string.Empty).Append(all[i].name);
                }
            }
            return b.Length > 0 ? b.ToString() : "none";
        }
    }
}
