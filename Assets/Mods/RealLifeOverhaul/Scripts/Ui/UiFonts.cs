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
    /// A face's material never keeps the shader the bundle brought, which is built for Windows alone and draws pink blocks
    /// on a Mac: as the faces are read each is given a text shader of the running game's (<see cref="Mend"/>,
    /// <see cref="UiShaderRules"/>), on every system alike, and a face whose shader does not draw here is not written with.
    /// Cost: nothing a frame; a look in a six-slot table a text.
    /// </summary>
    public static class UiFonts
    {
        public const string BundlePath = "Bundles/reallife_ui";   // under the mod's folder, beside its other bundles
        // The faces as UiAssets/build.ps1 names them in the bundle, with the family and weight of each.
        private static readonly string[] Names = { "Overpass-Regular SDF", "Overpass-SemiBold SDF", "Overpass-Bold SDF" };
        private static readonly UiFamily[] Families = { UiFamily.Overpass, UiFamily.Overpass, UiFamily.Overpass };
        private static readonly int[] Weights = { 400, 600, 700 };

        private static readonly TMP_FontAsset[] Assets = new TMP_FontAsset[3];   // the faces written with: read, and their shader draws here
        private static readonly TMP_FontAsset[] Read = new TMP_FontAsset[3];     // the faces as the bundle gave them, written with or not
        private static readonly Shader[] Built = new Shader[3];                  // the shader each face's material came with, for the check's line
        private static readonly bool[] BuiltDraws = new bool[3];                 // ...and whether it draws on this graphics device
        private static Shader _with;          // the game's shader the faces draw with now, held to see it go (Watch)
        private static string _withSaid;      // ...as the check's line names it
        private static int _outcome, _outcomeSaid = -1;   // what the check found, as one number, and what its line last said
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

        /// <summary>How many of the bundle's faces are written with: read, and their shader draws here.</summary>
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
            for (int i = 0; i < Read.Length; i++)
            {
                AddFallback(Read[i]);
            }
            Mend();   // its shader is one of the places a text shader is taken from
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
                Read[at] = f;
                Material came = f.material;
                Built[at] = came != null ? came.shader : null;
                BuiltDraws[at] = Draws(came);
                got++;
                Texture2D atlas = f.atlasTexture;
                if (atlas != null && atlas.isReadable)
                {
                    atlas.Apply(false, true);   // the texture alone stays: the bundle's readable copy is let go
                }
                AddFallback(f);
            }
            Mend();
            if (got < Read.Length)
            {
                Said?.Invoke("UI: the interface's font bundle holds " + got + " of " + Read.Length + " faces; the game's font stands in for the rest");
            }
        }

        /// <summary>
        /// Gives every face read a text shader the running game draws with, and writes with a face only while its shader
        /// draws on this graphics device. The shader a face came with is built for the system the bundle was made on, so it
        /// is never kept, on any system: Windows goes the same way as a Mac. Run as the faces are read, when the game's font
        /// is found, and when the shader in use has gone (<see cref="Watch"/>): never a frame.
        /// </summary>
        private static void Mend()
        {
            Shader game = TextShader(out string from);
            int drawing = 0;
            for (int i = 0; i < Read.Length; i++)
            {
                TMP_FontAsset f = Read[i];
                Material m = f != null ? f.material : null;
                Point(m, game);
                bool draws = Draws(m);
                Assets[i] = draws ? f : null;   // a face whose shader does not draw is never written with: the game's font stands in
                drawing += draws ? 1 : 0;
            }
            foreach (System.Collections.Generic.KeyValuePair<TMP_FontAsset, Material> heavy in Heavier)
            {
                Point(heavy.Value, game);   // the thickened copies draw with the same shader as their faces
            }
            if (!ReferenceEquals(_with, game) || Faces != drawing || _outcome == 0)
            {
                _outcome++;   // the check's line is said again: what the text is drawn with has changed
            }
            _with = game;
            _withSaid = game != null ? "the game's '" + game.name + "', " + from : null;
            Faces = drawing;
            Own = drawing == Assets.Length;
        }

        // The material drawn with another shader, everything else of it kept: its properties stay by name, its keywords are
        // set again and a render queue of its own is put back (one it only had from its old shader is the new shader's to give).
        private static void Point(Material m, Shader shader)
        {
            Shader was = m != null ? m.shader : null;
            if (m == null || shader == null || ReferenceEquals(was, shader))
            {
                return;
            }
            int queue = m.renderQueue;
            bool own = was != null && queue != was.renderQueue;
            string[] keywords = m.shaderKeywords;
            m.shader = shader;
            m.shaderKeywords = keywords;
            if (own && m.renderQueue != queue)
            {
                m.renderQueue = queue;
            }
        }

        // True when a text written with this material shows: one of TextMeshPro's interface shaders, and one this graphics
        // device runs. A shader that went with its scene reads as the engine's error shader, which is neither.
        private static bool Draws(Material m)
        {
            Shader s = m != null ? m.shader : null;
            return s != null && s.isSupported && UiShaderRules.ForText(s.name);
        }

        // The text shader of the running game the faces are given, by UiShaderRules.Pick, and where it came from; null for none.
        private static Shader TextShader(out string from)
        {
            var found = new Shader[UiShaderRules.Places];
            var names = new string[UiShaderRules.Places];
            var draws = new bool[UiShaderRules.Places];
            TMP_FontAsset standard = DefaultFont();
            found[UiShaderRules.FromDefaultFont] = Of(standard);
            found[UiShaderRules.FromGameFont] = Of(_fallback);
            found[UiShaderRules.ByName] = Shader.Find(UiShaderRules.Text);
            found[UiShaderRules.ByNameSmall] = Shader.Find(UiShaderRules.TextSmall);
            for (int i = 0; i < found.Length; i++)
            {
                names[i] = found[i] != null ? found[i].name : null;
                draws[i] = found[i] != null && found[i].isSupported;
            }
            int place = UiShaderRules.Pick(names, draws);
            from = UiShaderRules.From(place, place == UiShaderRules.FromDefaultFont ? standard.name : place == UiShaderRules.FromGameFont ? _fallback.name : null);
            return place >= 0 ? found[place] : null;
        }

        private static Shader Of(TMP_FontAsset font)
        {
            return font != null && font.material != null ? font.material.shader : null;
        }

        // TextMeshPro's default font in the running game, or null where its settings are not there (they always are in the game).
        private static TMP_FontAsset DefaultFont()
        {
            try
            {
                return TMP_Settings.defaultFontAsset;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Once a second from the host: true when the shader the faces draw with has gone (the game unloading what held it)
        /// and they were given another. Cost: two null tests.
        /// </summary>
        public static bool Watch()
        {
            if (!_done || (object)_with == null || _with != null)
            {
                return false;
            }
            Mend();
            return true;
        }

        /// <summary>True when the shaders' check has something its line has not said: once the fonts are read, and again only if what it found changes.</summary>
        public static bool ShadersNews => _done && _outcome != _outcomeSaid;

        /// <summary>True when no face is left on a shader that does not draw here.</summary>
        public static bool ShadersPass => Left(out _) == 0;

        /// <summary>The shaders' check of the font bundle as its one line for Player.log (<see cref="UiShaderRules.Line"/>); saying it ends <see cref="ShadersNews"/>.</summary>
        public static string ShadersLine(int gameBuild)
        {
            _outcomeSaid = _outcome;
            int materials = 0, shaders = 0, unsupported = 0, repointed = 0;
            for (int i = 0; i < Read.Length; i++)
            {
                if (!Came(i))
                {
                    continue;
                }
                materials++;
                bool first = true, replaced = true;   // the faces share one shader: it is counted with the first, and replaced when none of them keeps it
                for (int j = 0; j < Read.Length; j++)
                {
                    if (!Came(j) || !ReferenceEquals(Built[j], Built[i]))
                    {
                        continue;
                    }
                    first &= j >= i;
                    replaced &= Draws(Read[j].material) && !ReferenceEquals(Read[j].material.shader, Built[j]);
                }
                shaders += first ? 1 : 0;
                unsupported += first && !BuiltDraws[i] ? 1 : 0;
                repointed += first && replaced ? 1 : 0;
            }
            int left = Left(out string names);
            string with = materials == 0 ? "the game's own, with the game's font: the bundle's faces were not read" : _withSaid ?? "none of the game's that draws here";
            return UiShaderRules.Line(UiShaderRules.Bundle, materials, shaders, unsupported, repointed, left, names, with,
                SystemInfo.graphicsDeviceType.ToString(), SystemInfo.graphicsDeviceName, SystemInfo.operatingSystem, gameBuild);
        }

        // True for a face the bundle gave with a material.
        private static bool Came(int i)
        {
            return Read[i] != null && Read[i].material != null;
        }

        // The faces read whose material does not draw here, and their materials' names.
        private static int Left(out string names)
        {
            int left = 0;
            names = null;
            for (int i = 0; i < Read.Length; i++)
            {
                if (Read[i] == null || Assets[i] != null)
                {
                    continue;
                }
                left++;
                Material m = Read[i].material;
                names = (names != null ? names + ", " : string.Empty) + (m != null ? m.name : Read[i].name + " (no material)");
            }
            return left;
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
