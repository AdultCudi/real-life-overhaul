using System;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The interface's fonts from the bundle beside the DLL (Bundles/reallife_ui, built by UiAssets/build.ps1): Overpass in
    /// three weights, as static distance-field atlases. Read once a session in the background, behind the loading screen
    /// (<see cref="Begin"/>, <see cref="Step"/>); each atlas's copy in main memory is freed as it arrives, so it costs its
    /// texture alone. Every face falls back to the game's own font, given by the host, which carries the game's fallbacks
    /// for Chinese, Japanese and Korean (and Greek, which Overpass does not draw).
    /// Without the bundle every style uses the game's font, so a missing file costs the look, never a screen.
    /// Cost: nothing a frame; a look in a six-slot table a text.
    /// </summary>
    public static class UiFonts
    {
        public const string BundlePath = "Bundles/reallife_ui";   // under the mod's folder, beside its other bundles
        // The faces as UiAssets/build.ps1 names them in the bundle, with the family and weight of each.
        private static readonly string[] Names = { "Overpass-Regular SDF", "Overpass-SemiBold SDF", "Overpass-Bold SDF" };
        private static readonly UiFamily[] Families = { UiFamily.Overpass, UiFamily.Overpass, UiFamily.Overpass };
        private static readonly int[] Weights = { 400, 600, 700 };

        private static readonly TMP_FontAsset[] Assets = new TMP_FontAsset[3];
        private static AssetBundleCreateRequest _opening;
        private static AssetBundleRequest _reading;
        private static AssetBundle _bundle;
        private static TMP_FontAsset _fallback;
        private static bool _done;

        /// <summary>Said when the bundle is missing or a face is not in it, once.</summary>
        public static Action<string> Said;

        /// <summary>True once every face is in, or the bundle is known to be missing (the game's font then stands in).</summary>
        public static bool Ready => _done;

        /// <summary>Whether the bundle's own faces are drawing (false: the game's font stands in).</summary>
        public static bool Own { get; private set; }

        /// <summary>The bundle's file as it was looked for, for the interface's line at load.</summary>
        public static string BundleFile { get; private set; }

        /// <summary>How many of the faces came in from the bundle.</summary>
        public static int Faces { get; private set; }

        /// <summary>Of the faces the bundle should hold.</summary>
        public static int FacesWanted => Names.Length;

        /// <summary>Starts reading the bundle in the background; <see cref="Step"/> finishes it.</summary>
        public static void Begin(string bundlePath)
        {
            if (_done || _opening != null || _reading != null)
            {
                return;
            }
            BundleFile = bundlePath;
            if (string.IsNullOrEmpty(bundlePath) || !System.IO.File.Exists(bundlePath))
            {
                Said?.Invoke("UI: the interface's fonts are not installed (" + bundlePath + "); the game's font stands in");
                _done = true;
                return;
            }
            _opening = AssetBundle.LoadFromFileAsync(bundlePath);
        }

        /// <summary>Moves the read on; true when it is done. Called behind the loading screen until true.</summary>
        public static bool Step()
        {
            if (_done)
            {
                return true;
            }
            if (_opening != null)
            {
                if (!_opening.isDone)
                {
                    return false;
                }
                _bundle = _opening.assetBundle;
                _opening = null;
                if (_bundle == null)
                {
                    Said?.Invoke("UI: the interface's font bundle could not be read; the game's font stands in");
                    _done = true;
                    return true;
                }
                _reading = _bundle.LoadAllAssetsAsync<TMP_FontAsset>();
                return false;
            }
            if (_reading != null && !_reading.isDone)
            {
                return false;
            }
            Take(_reading != null ? _reading.allAssets : null);
            _reading = null;
            _done = true;
            return true;
        }

        /// <summary>Reads the bundle at once (an offline render or a test, never in play).</summary>
        public static void LoadNow(string bundlePath)
        {
            if (_done)
            {
                return;
            }
            AssetBundle b = System.IO.File.Exists(bundlePath) ? AssetBundle.LoadFromFile(bundlePath) : null;
            _bundle = b;
            Take(b != null ? b.LoadAllAssets<TMP_FontAsset>() : null);
            _done = true;
        }

        /// <summary>The game's own font, the last fallback of every face (its own fallbacks carry CJK and Hangul); set by the host.</summary>
        public static void SetFallback(TMP_FontAsset gameFont)
        {
            if (gameFont == null || gameFont == _fallback)
            {
                return;
            }
            _fallback = gameFont;
            for (int i = 0; i < Assets.Length; i++)
            {
                AddFallback(Assets[i]);
            }
        }

        private static readonly System.Collections.Generic.Dictionary<TMP_FontAsset, Material> Heavier = new System.Collections.Generic.Dictionary<TMP_FontAsset, Material>();

        /// <summary>
        /// A face's material with its strokes thickened a little, for dark words on the cyan: dark on light draws thinner
        /// than light on dark, so a primary button's label would read weaker than the secondary one beside it. One a face,
        /// kept for the session. Null for no face.
        /// </summary>
        public static Material OnLight(TMP_FontAsset font)
        {
            if (font == null || font.material == null)
            {
                return null;
            }
            if (!Heavier.TryGetValue(font, out Material heavy) || heavy == null)
            {
                heavy = new Material(font.material) { name = font.material.name + " on light" };
                heavy.SetFloat(ShaderUtilities.ID_FaceDilate, 0.16f);
                Heavier[font] = heavy;
            }
            return heavy;
        }

        /// <summary>The face for a style: the bundle's cut of its family and weight, or the game's font while the bundle is not in.</summary>
        /// <summary>
        /// True when the kit has a face to write with: one of the bundle's, or the game's own standing in. A screen that hides
        /// the game's piece to draw its own asks this first, so it never hides the game's and writes nothing (3 Nov: the
        /// fonts missed the loading screen's hold and stayed out all session).
        /// </summary>
        public static bool CanDraw => For(UiStyle.Body) != null;

        public static TMP_FontAsset For(UiStyle style)
        {
            UiType t = UiTokens.Type(style);
            int best = -1, gap = int.MaxValue;
            for (int i = 0; i < Names.Length; i++)
            {
                int d = Families[i] != t.Family ? int.MaxValue : Math.Abs(Weights[i] - t.Weight);
                if (d < gap)
                {
                    best = i;
                    gap = d;
                }
            }
            return best >= 0 && Assets[best] != null ? Assets[best] : _fallback;
        }

        private static void Take(UnityEngine.Object[] found)
        {
            int got = 0;
            for (int i = 0; found != null && i < found.Length; i++)
            {
                var f = found[i] as TMP_FontAsset;
                int at = f != null ? Array.IndexOf(Names, f.name) : -1;
                if (at < 0)
                {
                    continue;
                }
                Assets[at] = f;
                got++;
                Texture2D atlas = f.atlasTexture;
                if (atlas != null && atlas.isReadable)
                {
                    atlas.Apply(false, true);   // the texture alone stays: the bundle's readable copy is let go
                }
                AddFallback(f);
            }
            Faces = got;
            Own = got == Assets.Length;
            if (!Own)
            {
                Said?.Invoke("UI: the interface's font bundle holds " + got + " of " + Assets.Length + " faces; the game's font stands in for the rest");
            }
        }

        private static void AddFallback(TMP_FontAsset f)
        {
            if (f == null || _fallback == null || f.fallbackFontAssetTable == null || f.fallbackFontAssetTable.Contains(_fallback))
            {
                return;
            }
            f.fallbackFontAssetTable.Add(_fallback);
        }
    }
}
