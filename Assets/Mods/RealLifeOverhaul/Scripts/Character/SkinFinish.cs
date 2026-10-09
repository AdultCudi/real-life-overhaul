using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How the game's skin is finished. Game-free on purpose: the HDRP preview (CharacterAssets/PreviewProject)
    /// compiles this very file, so what is judged there is what runs here.
    ///
    /// What the game's skin shaders offer (read from the compiled shaders in the game's files): a base colour map
    /// multiplied by the skin tone, a normal map at fixed strength, and a mask map whose green is ambient occlusion
    /// (remapped by _AOMin/_AOMax) and whose alpha is the smoothness, used as it is. There is no smoothness or
    /// normal-strength number and no subsurface scattering, so the only way to a different surface is other maps.
    /// The game's maps are smooth: one even gloss over the body (0.20) and the face (0.30) and no relief finer than
    /// a wrinkle. On dark skin, where the sheen is most of what one sees, that reads as wax.
    ///
    /// A finished skin material gets copies of its maps, made once:
    ///  - the normal map with a fine relief of skin creases blended in;
    ///  - the mask map with the smoothness lowered a little, more on dark skin, and on the face lowered further
    ///    in the creases, which breaks the sheen up.
    /// The copies are shared by every material that uses the same game texture at the same skin tone step.
    /// Nothing per frame, no new shader, no new keyword.
    ///
    /// Tried in the preview and left out: a deeper occlusion (the map only reaches ambient light: no visible
    /// change), a broader unevenness in the normal map (reads as a skin disease at any strength that shows),
    /// creases at the size the textures can draw as creases (five texels: reptile scales) and more gloss (wet plastic).
    /// </summary>
    internal static class SkinFinish
    {
        /// <summary>What a finish does; <see cref="Default"/> holds the values chosen in the preview.</summary>
        public struct Settings
        {
            /// <summary>Smoothness multiplier on light skin (1 = the game's).</summary>
            public float Smooth;
            /// <summary>Smoothness multiplier on dark skin; tones in between are in between.</summary>
            public float SmoothDark;
            /// <summary>Strength of the crease relief in the normal map of the face (0 = the game's normal map).</summary>
            public float Pores;
            /// <summary>The same for the body, whose texture covers four times the skin per texel.</summary>
            public float PoresBody;
            /// <summary>How much the creases lower the smoothness of the face (0 to 1): breaks the sheen up.</summary>
            public float Grain;
            /// <summary>Pattern texels per normal-map texel (1 = creases about five texels apart; 2 = a grain too fine to read as lines).</summary>
            public float Scale;
            /// <summary>Compress the copies (BC5 normals, DXT5 masks); off only to compare in the preview.</summary>
            public bool Compress;

            public static Settings Default => new Settings
            {
                Smooth = 0.9f, SmoothDark = 0.75f, Pores = 0.12f, PoresBody = 0.06f, Grain = 0.3f, Scale = 2f, Compress = true
            };
        }

        public const string Marker = "(RealLifeOverhaul skin)";
        private const int SmallTexture = 64;      // the game's flat placeholder maps: nothing to finish
        private const int ToneSteps = 4;          // skin tone is taken in this many steps, so copies can be shared

        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int[] MaskIds = { Shader.PropertyToID("_MaskMap"), Shader.PropertyToID("_MaskMap_1") };
        private static readonly int[] NormalIds = { Shader.PropertyToID("_NormalMap"), Shader.PropertyToID("_NormalMap_1") };

        /// <summary>The copies made so far and the game texture each stands in for. The caller owns it.</summary>
        public sealed class Maps
        {
            private readonly Dictionary<string, Texture2D> _made = new Dictionary<string, Texture2D>(8);
            private readonly Dictionary<int, Texture> _sources = new Dictionary<int, Texture>(8);
            private readonly Dictionary<int, long> _bytes = new Dictionary<int, long>(8);
            private readonly List<string> _scratch = new List<string>(8);

            public int Count => _made.Count;
            public long Bytes { get; private set; }
            public double Milliseconds { get; internal set; }
            public int MadeEver { get; private set; }

            /// <summary>The game's texture behind a copy of ours; the texture itself if it is not one of ours.</summary>
            public Texture Source(Texture texture)
            {
                return texture != null && _sources.TryGetValue(texture.GetInstanceID(), out Texture source) ? source : texture;
            }

            public bool IsCopy(Texture texture)
            {
                return texture != null && _sources.ContainsKey(texture.GetInstanceID());
            }

            internal bool TryGet(string key, out Texture2D copy)
            {
                return _made.TryGetValue(key, out copy) && copy != null;
            }

            internal void Add(string key, Texture2D copy, Texture source, long bytes)
            {
                _made[key] = copy;
                _sources[copy.GetInstanceID()] = source;
                _bytes[copy.GetInstanceID()] = bytes;
                Bytes += bytes;
                MadeEver++;
            }

            /// <summary>Destroys the copies no material uses any more (<paramref name="used"/>: instance ids still in use).</summary>
            public void Sweep(HashSet<int> used)
            {
                _scratch.Clear();
                foreach (KeyValuePair<string, Texture2D> pair in _made)
                {
                    if (pair.Value == null || !used.Contains(pair.Value.GetInstanceID()))
                    {
                        _scratch.Add(pair.Key);
                    }
                }
                for (int i = 0; i < _scratch.Count; i++)
                {
                    Drop(_made[_scratch[i]]);
                    _made.Remove(_scratch[i]);
                }
                _scratch.Clear();
            }

            public void Clear()
            {
                foreach (Texture2D copy in _made.Values)
                {
                    Drop(copy);
                }
                _made.Clear();
                _sources.Clear();
                _bytes.Clear();
                Bytes = 0;
            }

            private void Drop(Texture2D copy)
            {
                if (copy == null)
                {
                    return;
                }
                int id = copy.GetInstanceID();
                if (_bytes.TryGetValue(id, out long bytes))
                {
                    Bytes -= bytes;
                    _bytes.Remove(id);
                }
                _sources.Remove(id);
                Release(copy);
            }
        }

        /// <summary>0 for light skin, 1 for dark, in a few steps (so that copies are shared between close tones).</summary>
        public static float Darkness(Color tone)
        {
            float luminance = 0.2126f * tone.r + 0.7152f * tone.g + 0.0722f * tone.b;   // of the tone as picked (sRGB values)
            float dark = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.22f, 0.62f, luminance));
            return Mathf.Round(dark * ToneSteps) / ToneSteps;
        }

        public static bool IsSkinMap(Texture texture)
        {
            return texture != null && texture.width > SmallTexture && texture.height > SmallTexture;
        }

        /// <summary>
        /// Gives a skin material finished copies of its maps (the aged set of a face too, once it shows).
        /// Works on the material it is handed: the caller passes one that belongs to one character.
        /// Safe to call again: a map that is already a copy is redone from the game's texture behind it.
        /// Returns how many maps the material now takes from <paramref name="maps"/>.
        /// </summary>
        public static int Treat(Material material, bool face, Color tone, Settings settings, Maps maps)
        {
            if (material == null)
            {
                return 0;
            }
            float smooth = Mathf.Lerp(settings.Smooth, settings.SmoothDark, Darkness(tone));
            float pores = face ? settings.Pores : settings.PoresBody;
            bool aged = material.HasProperty(FadeId) && material.GetFloat(FadeId) > 0.01f;
            int count = 0;
            for (int set = 0; set < 2; set++)
            {
                if (set == 1 && !aged)
                {
                    // Not showing: left as it is (and put back, should it have been finished earlier).
                    Put(material, MaskIds[1], maps);
                    Put(material, NormalIds[1], maps);
                    break;
                }
                if (!material.HasProperty(MaskIds[set]) || !material.HasProperty(NormalIds[set]))
                {
                    continue;
                }
                Texture mask = maps.Source(material.GetTexture(MaskIds[set]));
                Texture normal = maps.Source(material.GetTexture(NormalIds[set]));
                if (IsSkinMap(normal) && pores > 0f)
                {
                    string key = "n" + normal.GetInstanceID() + "/" + pores.ToString("0.###") + "/" + settings.Scale.ToString("0.###") + (settings.Compress ? "c" : "u");
                    if (!maps.TryGet(key, out Texture2D copy))
                    {
                        var watch = System.Diagnostics.Stopwatch.StartNew();
                        copy = Normal(normal, pores, settings.Scale, settings.Compress, out long bytes);
                        maps.Add(key, copy, normal, bytes);
                        maps.Milliseconds += watch.Elapsed.TotalMilliseconds;
                    }
                    material.SetTexture(NormalIds[set], copy);
                    count++;
                }
                else
                {
                    Put(material, NormalIds[set], maps);
                }
                // The grain is laid out in the normal map's texels; only the face is seen close enough to be worth them.
                float grain = face && pores > 0f && IsSkinMap(normal) ? settings.Grain : 0f;
                if (IsSkinMap(mask) && (Mathf.Abs(smooth - 1f) > 0.001f || grain > 0f))
                {
                    int size = grain > 0f ? Mathf.Max(mask.width, normal.width) : mask.width;
                    string key = "m" + mask.GetInstanceID() + "/" + smooth.ToString("0.###") + "/" + grain.ToString("0.###") + "/" + size + "/" + settings.Scale.ToString("0.###")
                                 + (settings.Compress ? "c" : "u");
                    if (!maps.TryGet(key, out Texture2D copy))
                    {
                        var watch = System.Diagnostics.Stopwatch.StartNew();
                        copy = Mask(mask, smooth, grain, size, settings.Scale, settings.Compress, out long bytes);
                        maps.Add(key, copy, mask, bytes);
                        maps.Milliseconds += watch.Elapsed.TotalMilliseconds;
                    }
                    material.SetTexture(MaskIds[set], copy);
                    count++;
                }
                else
                {
                    Put(material, MaskIds[set], maps);
                }
            }
            return count;
        }

        /// <summary>Puts the game's own maps back on a material that was finished. Leaves anything else alone.</summary>
        public static void Restore(Material material, Maps maps)
        {
            if (material == null)
            {
                return;
            }
            for (int set = 0; set < 2; set++)
            {
                Put(material, MaskIds[set], maps);
                Put(material, NormalIds[set], maps);
            }
        }

        /// <summary>
        /// Adds the copies this material uses now to <paramref name="used"/> (for <see cref="Maps.Sweep"/>);
        /// returns how many of its maps are copies.
        /// </summary>
        public static int Collect(Material material, Maps maps, HashSet<int> used)
        {
            int count = 0;
            if (material != null)
            {
                for (int set = 0; set < 2; set++)
                {
                    count += Note(material, MaskIds[set], maps, used) + Note(material, NormalIds[set], maps, used);
                }
            }
            return count;
        }

        private static int Note(Material material, int property, Maps maps, HashSet<int> used)
        {
            if (material.HasProperty(property))
            {
                Texture texture = material.GetTexture(property);
                if (maps.IsCopy(texture))
                {
                    used?.Add(texture.GetInstanceID());
                    return 1;
                }
            }
            return 0;
        }

        // Only ever replaces a copy of ours by the texture it was made from: whatever the game has put there since stays.
        private static void Put(Material material, int property, Maps maps)
        {
            if (material.HasProperty(property))
            {
                Texture texture = material.GetTexture(property);
                if (maps.IsCopy(texture))
                {
                    material.SetTexture(property, maps.Source(texture));
                }
            }
        }

        // ---------------------------------------------------------------- the copies

        /// <summary>
        /// A copy of a mask map with the smoothness (alpha) scaled and lowered in the creases; everything else as it
        /// is. <paramref name="size"/> may be larger than the source: the grain needs the normal map's texels.
        /// </summary>
        public static Texture2D Mask(Texture source, float smooth, float grain, int size, float scale, bool compress, out long bytes)
        {
            Color32[] pixels = Read(source, out bool srgb);
            int width = size, height = Mathf.Max(1, size * source.height / source.width);
            if (width != source.width)
            {
                pixels = Enlarge(pixels, source.width, source.height, width, height);
            }
            Relief relief = grain > 0f ? Relief.Get() : null;
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                int ry = relief != null ? Wrap(Mathf.FloorToInt(y * scale)) * Relief.Size : 0;
                for (int x = 0; x < width; x++)
                {
                    float a = pixels[row + x].a * smooth;
                    if (relief != null)
                    {
                        a *= 1f - grain * relief.Crease[ry + Wrap(Mathf.FloorToInt(x * scale))];
                    }
                    pixels[row + x].a = (byte)(a > 255f ? 255f : a + 0.5f);
                }
            }
            var copy = new Texture2D(width, height, TextureFormat.RGBA32, true, !srgb) { name = source.name + " " + Marker };
            copy.SetPixels32(pixels);
            Finish(copy, source);
            copy.Apply(true, false);
            if (compress)
            {
                copy.Compress(false);   // DXT5: the smoothness has the alpha block to itself
            }
            copy.Apply(false, true);    // no longer readable: the CPU copy is dropped
            bytes = Size(copy);
            return copy;
        }

        /// <summary>
        /// A copy of a normal map with the crease relief added. The game's maps keep x in alpha and y in green
        /// (the shader multiplies red into alpha, so either packing reads the same); the copy is stored the same
        /// way or, compressed, as BC5 (x in red, y in green), which the same shader code reads correctly.
        /// </summary>
        public static Texture2D Normal(Texture source, float strength, float scale, bool compress, out long bytes)
        {
            int width = source.width, height = source.height;
            Color32[] pixels = Read(source, out bool srgb);
            Relief relief = Relief.Get();
            float gain = strength * 127.5f;
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                int ry = Wrap(Mathf.FloorToInt(y * scale)) * Relief.Size;
                for (int x = 0; x < width; x++)
                {
                    Color32 p = pixels[row + x];
                    int at = ry + Wrap(Mathf.FloorToInt(x * scale));
                    // x = red * alpha, as the shader unpacks it.
                    float nx = p.a * (p.r / 255f) + relief.SlopeX[at] * gain;
                    float ny = p.g + relief.SlopeY[at] * gain;
                    p.r = 255;
                    p.a = (byte)(nx < 0f ? 0f : nx > 255f ? 255f : nx + 0.5f);
                    p.g = (byte)(ny < 0f ? 0f : ny > 255f ? 255f : ny + 0.5f);
                    p.b = 255;
                    pixels[row + x] = p;
                }
            }
            Texture2D copy;
            bool bc5 = compress && SystemInfo.SupportsTextureFormat(TextureFormat.BC5) && (width & 3) == 0 && (height & 3) == 0;
            if (bc5)
            {
                copy = Bc5(pixels, width, height, source.name + " " + Marker);
                Finish(copy, source);
                copy.Apply(false, true);
            }
            else
            {
                copy = new Texture2D(width, height, TextureFormat.RGBA32, true, !srgb) { name = source.name + " " + Marker };
                copy.SetPixels32(pixels);
                Finish(copy, source);
                copy.Apply(true, true);
            }
            bytes = Size(copy);
            return copy;
        }

        private static void Finish(Texture2D copy, Texture source)
        {
            copy.wrapMode = source.wrapMode;
            copy.filterMode = FilterMode.Trilinear;
            copy.anisoLevel = 8;
        }

        // In memory, with its mip chain.
        private static long Size(Texture2D texture)
        {
            long blocks = (long)Mathf.Max(1, texture.width / (int)GraphicsFormatUtility.GetBlockWidth(texture.graphicsFormat))
                          * Mathf.Max(1, texture.height / (int)GraphicsFormatUtility.GetBlockHeight(texture.graphicsFormat));
            return blocks * GraphicsFormatUtility.GetBlockSize(texture.graphicsFormat) * 4L / 3L;
        }

        // The game's textures are not readable: drawn once into a render texture and read back. A texture stored
        // as sRGB comes out of the sampler as linear light, whatever the target; so those go through a half-float
        // target and are put back into sRGB here, which returns the stored bytes (alpha is never converted).
        // (Checked in the preview: a copy with nothing changed draws exactly as the game's map.)
        private static Color32[] Read(Texture source, out bool srgb)
        {
            int width = source.width, height = source.height;
            srgb = GraphicsFormatUtility.IsSRGBFormat(source.graphicsFormat);
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, srgb ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            var read = new Texture2D(width, height, srgb ? TextureFormat.RGBAHalf : TextureFormat.RGBA32, false, true);
            read.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Color32[] pixels;
            if (srgb)
            {
                Color[] linear = read.GetPixels();
                pixels = new Color32[linear.Length];
                for (int i = 0; i < linear.Length; i++)
                {
                    Color c = linear[i];
                    pixels[i] = new Color32(Stored(c.r), Stored(c.g), Stored(c.b), (byte)(Mathf.Clamp01(c.a) * 255f + 0.5f));
                }
            }
            else
            {
                pixels = read.GetPixels32();
            }
            Release(read);
            return pixels;
        }

        private static byte Stored(float linear)
        {
            return (byte)(Mathf.LinearToGammaSpace(Mathf.Clamp01(linear)) * 255f + 0.5f);
        }

        // Bilinear, as the sampler would have enlarged it.
        private static Color32[] Enlarge(Color32[] source, int width, int height, int toWidth, int toHeight)
        {
            var result = new Color32[toWidth * toHeight];
            float sx = (float)width / toWidth, sy = (float)height / toHeight;
            for (int y = 0; y < toHeight; y++)
            {
                float fy = Mathf.Clamp((y + 0.5f) * sy - 0.5f, 0f, height - 1);
                int y0 = (int)fy, y1 = Mathf.Min(y0 + 1, height - 1);
                float ty = fy - y0;
                for (int x = 0; x < toWidth; x++)
                {
                    float fx = Mathf.Clamp((x + 0.5f) * sx - 0.5f, 0f, width - 1);
                    int x0 = (int)fx, x1 = Mathf.Min(x0 + 1, width - 1);
                    float tx = fx - x0;
                    Color32 a = source[y0 * width + x0], b = source[y0 * width + x1], c = source[y1 * width + x0], d = source[y1 * width + x1];
                    float w00 = (1f - tx) * (1f - ty), w10 = tx * (1f - ty), w01 = (1f - tx) * ty, w11 = tx * ty;
                    result[y * toWidth + x] = new Color32(
                        (byte)(a.r * w00 + b.r * w10 + c.r * w01 + d.r * w11 + 0.5f), (byte)(a.g * w00 + b.g * w10 + c.g * w01 + d.g * w11 + 0.5f),
                        (byte)(a.b * w00 + b.b * w10 + c.b * w01 + d.b * w11 + 0.5f), (byte)(a.a * w00 + b.a * w10 + c.a * w01 + d.a * w11 + 0.5f));
                }
            }
            return result;
        }

        private static void Release(Object made)
        {
            if (made == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Object.Destroy(made);
            }
            else
            {
                Object.DestroyImmediate(made);   // the preview runs in the editor, outside play mode
            }
        }

        private static int Wrap(int value)
        {
            return value & (Relief.Size - 1);
        }

        // ---------------------------------------------------------------- BC5

        // Two-channel block compression written out by hand: Unity's own runtime compressor only makes DXT, whose
        // colour block is too coarse for a relief this fine. Each 4x4 block stores, per channel, two end values and
        // sixteen 3-bit steps between them.
        private static Texture2D Bc5(Color32[] pixels, int width, int height, string name)
        {
            int levels = 1;
            for (int w = width, h = height; w > 4 && h > 4 && (w & 7) == 0 && (h & 7) == 0; w >>= 1, h >>= 1)
            {
                levels++;
            }
            long total = 0;
            for (int level = 0, w = width, h = height; level < levels; level++, w >>= 1, h >>= 1)
            {
                total += (w / 4) * (h / 4) * 16;
            }
            var data = new byte[total];
            var xs = new byte[width * height];
            var ys = new byte[width * height];
            for (int i = 0; i < xs.Length; i++)
            {
                xs[i] = pixels[i].a;
                ys[i] = pixels[i].g;
            }
            int offset = 0;
            var block = new byte[16];
            for (int level = 0, w = width, h = height; level < levels; level++)
            {
                for (int by = 0; by < h; by += 4)
                {
                    for (int bx = 0; bx < w; bx += 4)
                    {
                        Gather(xs, w, bx, by, block);
                        Bc4(block, data, offset);
                        Gather(ys, w, bx, by, block);
                        Bc4(block, data, offset + 8);
                        offset += 16;
                    }
                }
                if (level + 1 < levels)
                {
                    Halve(xs, w, h);
                    Halve(ys, w, h);
                    w >>= 1;
                    h >>= 1;
                }
            }
            var texture = new Texture2D(width, height, TextureFormat.BC5, levels, true) { name = name };
            texture.LoadRawTextureData(data);
            return texture;
        }

        private static void Gather(byte[] channel, int width, int bx, int by, byte[] block)
        {
            for (int y = 0; y < 4; y++)
            {
                int row = (by + y) * width + bx;
                block[y * 4] = channel[row];
                block[y * 4 + 1] = channel[row + 1];
                block[y * 4 + 2] = channel[row + 2];
                block[y * 4 + 3] = channel[row + 3];
            }
        }

        // In place: the start of the array becomes the next level down (rows of half the width).
        private static void Halve(byte[] channel, int width, int height)
        {
            int w = width >> 1, h = height >> 1;
            for (int y = 0; y < h; y++)
            {
                int a = y * 2 * width, b = a + width, to = y * w;
                for (int x = 0; x < w; x++)
                {
                    channel[to + x] = (byte)((channel[a + x * 2] + channel[a + x * 2 + 1] + channel[b + x * 2] + channel[b + x * 2 + 1] + 2) >> 2);
                }
            }
        }

        private static void Bc4(byte[] block, byte[] data, int offset)
        {
            int min = 255, max = 0;
            for (int i = 0; i < 16; i++)
            {
                int v = block[i];
                if (v < min)
                {
                    min = v;
                }
                if (v > max)
                {
                    max = v;
                }
            }
            data[offset] = (byte)max;
            data[offset + 1] = (byte)min;
            if (max == min)
            {
                for (int i = 2; i < 8; i++)
                {
                    data[offset + i] = 0;
                }
                return;
            }
            // With the first end above the second: codes 0 = max, 1 = min, 2..7 = six steps from max down to min.
            ulong bits = 0;
            float range = max - min;
            for (int i = 0; i < 16; i++)
            {
                int step = (int)((max - block[i]) * 7f / range + 0.5f);   // 0 (max) .. 7 (min)
                ulong code = step == 0 ? 0UL : step == 7 ? 1UL : (ulong)(step + 1);
                bits |= code << (3 * i);
            }
            for (int i = 0; i < 6; i++)
            {
                data[offset + 2 + i] = (byte)(bits >> (8 * i));
            }
        }

        // ---------------------------------------------------------------- the relief

        /// <summary>
        /// The fine structure of skin, as one tile that repeats: a net of shallow creases round small, uneven
        /// plateaus (what skin looks like under a lens), a little deeper in some places than others. Generated once.
        /// </summary>
        private sealed class Relief
        {
            public const int Size = 256;           // a power of two: wrapped with a mask
            private const int Cells = 52;          // plateaus across the tile: creases about five pattern texels apart

            public readonly float[] SlopeX = new float[Size * Size];
            public readonly float[] SlopeY = new float[Size * Size];
            public readonly float[] Crease = new float[Size * Size];   // towards 1 in a crease, 0 on a plateau

            private static Relief _made;

            public static Relief Get()
            {
                return _made ?? (_made = new Relief());
            }

            private Relief()
            {
                var px = new float[Cells * Cells];
                var py = new float[Cells * Cells];
                float cell = (float)Size / Cells;
                for (int j = 0; j < Cells; j++)
                {
                    for (int i = 0; i < Cells; i++)
                    {
                        px[j * Cells + i] = (i + 0.15f + 0.7f * Hash(i, j, 1)) * cell;
                        py[j * Cells + i] = (j + 0.15f + 0.7f * Hash(i, j, 2)) * cell;
                    }
                }
                var height = new float[Size * Size];
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        int ci = (int)(x / cell), cj = (int)(y / cell);
                        float first = float.MaxValue, second = float.MaxValue;
                        for (int dj = -1; dj <= 1; dj++)
                        {
                            for (int di = -1; di <= 1; di++)
                            {
                                int i = ci + di, j = cj + dj;
                                int wi = (i + Cells) % Cells, wj = (j + Cells) % Cells;
                                float dx = px[wj * Cells + wi] + (i - wi) * cell - (x + 0.5f);
                                float dy = py[wj * Cells + wi] + (j - wj) * cell - (y + 0.5f);
                                float d = Mathf.Sqrt(dx * dx + dy * dy);
                                if (d < first)
                                {
                                    second = first;
                                    first = d;
                                }
                                else if (d < second)
                                {
                                    second = d;
                                }
                            }
                        }
                        // Half the difference is the distance to the border between two plateaus.
                        float crease = 1f - Mathf.SmoothStep(0f, 1f, (second - first) * 0.5f / 1.1f);
                        float depth = 0.55f + 0.45f * Soft(x, y, 11);
                        Crease[y * Size + x] = crease * depth;
                        height[y * Size + x] = -crease * depth + 0.12f * (Hash(x, y, 3) - 0.5f);
                    }
                }
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        int l = (x + Size - 1) & (Size - 1), r = (x + 1) & (Size - 1), u = (y + Size - 1) & (Size - 1), d = (y + 1) & (Size - 1);
                        SlopeX[y * Size + x] = (height[y * Size + l] - height[y * Size + r]) * 0.5f;
                        SlopeY[y * Size + x] = (height[u * Size + x] - height[d * Size + x]) * 0.5f;
                    }
                }
            }

            private static float Hash(int x, int y, int salt)
            {
                unchecked
                {
                    uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)salt * 2246822519u;
                    h = (h ^ (h >> 13)) * 1274126177u;
                    h ^= h >> 16;
                    return (h & 0xFFFFFF) / 16777216f;
                }
            }

            // Smooth noise that repeats with the tile: <period> lattice cells across it.
            private static float Soft(float x, float y, int period)
            {
                x *= (float)period / Size;
                y *= (float)period / Size;
                int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
                float fx = x - xi, fy = y - yi;
                fx = fx * fx * (3f - 2f * fx);
                fy = fy * fy * (3f - 2f * fy);
                int x0 = ((xi % period) + period) % period, x1 = (x0 + 1) % period, y0 = ((yi % period) + period) % period, y1 = (y0 + 1) % period;
                return Mathf.Lerp(Mathf.Lerp(Hash(x0, y0, 5), Hash(x1, y0, 5), fx), Mathf.Lerp(Hash(x0, y1, 5), Hash(x1, y1, 5), fx), fy);
            }
        }
    }
}
