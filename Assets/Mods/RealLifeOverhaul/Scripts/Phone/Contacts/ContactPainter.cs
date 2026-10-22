using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.GameUi;
using UI.Load;
using Unity.Collections;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Paints the contacts' pictures one at a time: a logo is the game's logo shape in white on the catalogue's colour (or
    /// the game's own picture for that kind of contact), a face comes from <see cref="PortraitTurn"/>. Both are kept under
    /// RealLifeOverhaul/Portraits, named by what they are drawn from. Cost: idle, nothing (it switches itself off); busy,
    /// about a millisecond of a frame, and a picture not yet kept only as the first of a frame, alone.
    /// </summary>
    public sealed class ContactPainter : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("phone", "ContactPainter", typeof(ContactPainter));

        private const double SliceMs = 1.0;         // milliseconds of a frame in play (the first picture of a frame always runs)
        private const double HiddenSliceMs = 8.0;   // milliseconds of a frame behind the game's black screen
        private const float FacePaceSeconds = 2f;   // after a face drawn by the portrait camera
        private const int LogoBytes = ContactPictures.Size * ContactPictures.Size * 4;   // RGBA, a byte each
        private const string LogoVersion = "logo_v1_";   // a new way of painting changes this, and every kept logo is painted again
        private const float FaceTimeout = 15f;      // seconds to wait for the camera's read-back before trying again
        private const int TriesMost = 3;
        private const float GlyphShare = 0.62f;     // of the square's height the shape takes, as the game's own logos leave a margin
        private const float WideShare = 0.8f;       // of its width, for a shape wider than it is high

        private static ContactPainter _instance;
        private static readonly Queue<ContactPictures.Picture> Wanted = new Queue<ContactPictures.Picture>(32);

        private static byte[] _logoBuffer;          // one for the session: every kept logo passes through it

        private ContactPictures.Picture _drawing;   // a face waiting for the portrait camera's read-back
        private float _drawingSince, _next;

        internal static void Want(ContactPictures.Picture p)
        {
            Wanted.Enqueue(p);
            if (_instance == null)
            {
                var host = new GameObject("RealLife ContactPainter");
                DontDestroyOnLoad(host);
                host.hideFlags = HideFlags.HideAndDontSave;
                _instance = host.AddComponent<ContactPainter>();
            }
            _instance.enabled = true;
        }

        /// <summary>Deletes a face's file in the portrait cache, so it is drawn again.</summary>
        internal static void Forget(ContactPictures.Picture p)
        {
            string file = Path.Combine(Folder, ContactCatalogue.CacheName(p.Look.Female, p.Look.Age, p.Seed));
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        private static string Folder => Path.Combine(Application.persistentDataPath, "RealLifeOverhaul", "Portraits");

        private static readonly int HotUpdate = HotSpots.Slot("Phone: ContactPainter.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (_drawing != null)
            {
                if (Time.realtimeSinceStartup - _drawingSince > FaceTimeout)
                {
                    ContactPictures.Logger?.Warn("Contacts: the game's portrait camera did not answer for '" + _drawing.Id + "' in " + FaceTimeout + " s; it is asked again later.");
                    Requeue(_drawing);
                    _drawing = null;
                    PortraitTurn.Give(this, Time.realtimeSinceStartup, false);
                }
                return;
            }
            if (Wanted.Count == 0)
            {
                enabled = false;   // nothing to paint: no cost until a picture is wanted
                return;
            }
            if (Time.realtimeSinceStartup < _next || LoadScene.isLoading)
            {
                return;
            }
            WorkSlice slice = WorkSlice.Of(SceneFinds.Hidden ? HiddenSliceMs : SliceMs, 1);
            while (Wanted.Count > 0 && _drawing == null && Time.realtimeSinceStartup >= _next)
            {
                ContactPictures.Picture p = Wanted.Peek();
                bool heavy = !p.Drawn && !Kept(p);
                if (!slice.More(heavy))
                {
                    return;
                }
                Wanted.Dequeue();
                p.Queued = false;
                if (p.Drawn)
                {
                    continue;
                }
                try
                {
                    if (p.Look.Kind == PictureKind.Logo)
                    {
                        PaintLogo(p);
                    }
                    else
                    {
                        PaintFace(p);
                    }
                }
                catch (Exception e)
                {
                    ContactPictures.Logger?.Error(e);
                    Requeue(p);
                }
                if (heavy)
                {
                    slice.End();   // a picture made from nothing has the frame to itself
                }
            }
        }

        // True if the picture is in the cache: a face's JPEG, a logo's own bytes.
        private static bool Kept(ContactPictures.Picture p)
        {
            if (p.Look.Kind != PictureKind.Logo)
            {
                return File.Exists(Path.Combine(Folder, ContactCatalogue.CacheName(p.Look.Female, p.Look.Age, p.Seed)));
            }
            string game = p.Look.LikeGame != null ? GameLogoFile(p.Look.LikeGame) : null;
            string shape = ShapeLogoFile(p.Look);
            return (game != null && File.Exists(game)) || (shape != null && File.Exists(shape));
        }

        // A kept logo copied from one of the game's own pictures: by its name and the game's build.
        private static string GameLogoFile(string name)
        {
            return Path.Combine(Folder, LogoVersion + "game_" + name + "_" + Nav.NavBaseGame.Build() + ".raw");
        }

        // Named by the shape, the colour and the shape file's size and date; null if the game has no such file.
        private static string ShapeLogoFile(ContactLook look)
        {
            var shape = new FileInfo(Path.Combine(LogoHelper.GetBuildInIconsFolder(), look.Shape + ".png"));
            if (!shape.Exists)
            {
                return null;
            }
            return Path.Combine(Folder, LogoVersion + look.Shape + "_" + look.R.ToString("X2") + look.G.ToString("X2") + look.B.ToString("X2")
                                        + "_" + shape.Length + "_" + shape.LastWriteTimeUtc.Ticks + ".raw");
        }

        // A kept logo into the picture. False if there is none, or it is not whole.
        private static bool ReadLogo(ContactPictures.Picture p, string file)
        {
            if (file == null || !File.Exists(file))
            {
                return false;
            }
            _logoBuffer ??= new byte[LogoBytes];
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1))
            {
                if (stream.Length != LogoBytes)
                {
                    return false;
                }
                int read = 0;
                while (read < LogoBytes)
                {
                    int got = stream.Read(_logoBuffer, read, LogoBytes - read);
                    if (got <= 0)
                    {
                        return false;
                    }
                    read += got;
                }
            }
            p.Texture.SetPixelData(_logoBuffer, 0);
            p.Texture.Apply(false, false);
            return true;
        }

        // A logo that cannot be kept is painted again next time.
        private static void KeepLogo(ContactPictures.Picture p, string file)
        {
            if (file == null)
            {
                return;
            }
            try
            {
                _logoBuffer ??= new byte[LogoBytes];
                NativeArray<byte> bytes = p.Texture.GetPixelData<byte>(0);
                if (bytes.Length != LogoBytes)
                {
                    return;
                }
                bytes.CopyTo(_logoBuffer);
                Directory.CreateDirectory(Folder);
                File.WriteAllBytes(file, _logoBuffer);
            }
            catch (Exception e)
            {
                ContactPictures.Logger?.Warn("Contacts: a logo could not be kept in the portrait cache (" + e.Message + "); it is painted again on the next load.");
            }
        }

        private void Requeue(ContactPictures.Picture p)
        {
            if (++p.Tries < TriesMost && !p.Queued)
            {
                p.Queued = true;
                Wanted.Enqueue(p);
                _next = Time.realtimeSinceStartup + FacePaceSeconds;
            }
            else if (p.Tries >= TriesMost)
            {
                ContactPictures.Logger?.Warn("Contacts: the picture of '" + p.Id + "' could not be made after " + TriesMost + " tries; its square keeps its colour until the next load.");
            }
        }

        private static void PaintLogo(ContactPictures.Picture p)
        {
            if (p.Look.LikeGame != null)
            {
                string game = GameLogoFile(p.Look.LikeGame);
                if (ReadLogo(p, game))
                {
                    Done(p, "the game's own picture '" + p.Look.LikeGame + "', kept");
                    return;
                }
                if (CopyGamePicture(p, p.Look.LikeGame))
                {
                    KeepLogo(p, game);
                    Done(p, "the game's own picture '" + p.Look.LikeGame + "'");
                    return;
                }
            }
            string kept = ShapeLogoFile(p.Look);
            if (ReadLogo(p, kept))
            {
                Done(p, "the game's logo shape '" + p.Look.Shape + "', kept");
                return;
            }
            string path = Path.Combine(LogoHelper.GetBuildInIconsFolder(), p.Look.Shape + ".png");
            Texture2D shape = LogoHelper.GetLogoShapeTexture(path);
            if (shape == null)
            {
                shape = LogoHelper.GetLogoShapeTexture(Path.Combine(LogoHelper.GetBuildInIconsFolder(), "notfound.png"));
            }
            var back = new Color32(p.Look.R, p.Look.G, p.Look.B, 255);
            var white = new Color32(255, 255, 255, 255);
            int size = ContactPictures.Size;
            NativeArray<Color32> pixels = p.Texture.GetPixelData<Color32>(0);
            // Fitted by what is drawn of the shape, not its canvas, and sampled here (Ink) rather than one engine call a pixel.
            int minX = 0, minY = 0, maxX = -1, maxY = -1;
            Color32[] texels = null;
            int sw = 1, sh = 1;
            if (shape != null)
            {
                texels = Bounds(shape, out minX, out minY, out maxX, out maxY);
                sw = shape.width;
                sh = shape.height;
            }
            float bw = maxX - minX + 1, bh = maxY - minY + 1;
            float k = bw > 0f && bh > 0f ? Mathf.Max(bw / (WideShare * size), bh / (GlyphShare * size)) : 1f;   // shape texels to a picture pixel
            float ox = size * 0.5f - bw / (2f * k), oy = size * 0.5f - bh / (2f * k);
            for (int y = 0; y < size; y++)
            {
                float ty = minY + (y + 0.5f - oy) * k;
                for (int x = 0; x < size; x++)
                {
                    float tx = minX + (x + 0.5f - ox) * k;
                    float ink = maxX >= 0 && tx >= minX && tx <= maxX + 1 && ty >= minY && ty <= maxY + 1 ? Ink(texels, sw, sh, tx, ty) : 0f;
                    pixels[y * size + x] = Color32.Lerp(back, white, ink);
                }
            }
            if (shape != null)
            {
                Destroy(shape);
            }
            p.Texture.Apply(false, false);
            if (shape != null)
            {
                KeepLogo(p, kept);   // the game's stand-in for a missing shape is never kept
            }
            Done(p, "the game's logo shape '" + p.Look.Shape + "'" + (shape == null ? " (not found: the game's own 'not found' shape)" : string.Empty));
        }

        // The shape's alpha at texel coordinates, filtered as Texture2D.GetPixelBilinear does for the game's shapes.
        private static float Ink(Color32[] texels, int w, int h, float tx, float ty)
        {
            float u = tx - 0.5f, v = ty - 0.5f;
            int x0 = Mathf.FloorToInt(u), y0 = Mathf.FloorToInt(v);
            float fx = u - x0, fy = v - y0;
            int xa = Mathf.Clamp(x0, 0, w - 1), xb = Mathf.Clamp(x0 + 1, 0, w - 1);
            int ya = Mathf.Clamp(y0, 0, h - 1), yb = Mathf.Clamp(y0 + 1, 0, h - 1);
            float low = Mathf.Lerp(texels[ya * w + xa].a, texels[ya * w + xb].a, fx);
            float high = Mathf.Lerp(texels[yb * w + xa].a, texels[yb * w + xb].a, fx);
            return Mathf.Lerp(low, high, fy) / 255f;
        }

        // The box of a shape's drawn pixels (alpha over a twentieth), and the pixels it was read from.
        private static Color32[] Bounds(Texture2D shape, out int minX, out int minY, out int maxX, out int maxY)
        {
            Color32[] px = shape.GetPixels32();
            int w = shape.width, h = shape.height;
            minX = w;
            minY = h;
            maxX = maxY = -1;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a > 12)
                    {
                        if (x < minX)
                        {
                            minX = x;
                        }
                        if (x > maxX)
                        {
                            maxX = x;
                        }
                        if (y < minY)
                        {
                            minY = y;
                        }
                        if (y > maxY)
                        {
                            maxY = y;
                        }
                    }
                }
            }
            if (maxX < 0)
            {
                minX = minY = 0;
            }
            return px;
        }

        // One of the game's own contact pictures, copied through the graphics card: it is packed and not readable as it is.
        private static bool CopyGamePicture(ContactPictures.Picture p, string name)
        {
            var references = InstanceBehavior<GlobalReferences>.Instance;
            Sprite found = null;
            for (int i = 0; references != null && references.contactIcons != null && i < references.contactIcons.Length; i++)
            {
                Sprite s = references.contactIcons[i];
                if (s != null && s.name == name && s.texture != null)
                {
                    found = s;
                    break;
                }
            }
            if (found == null)
            {
                return false;
            }
            Rect r = found.textureRect;
            var scale = new Vector2(r.width / found.texture.width, r.height / found.texture.height);
            var offset = new Vector2(r.x / found.texture.width, r.y / found.texture.height);
            RenderTexture rt = RenderTexture.GetTemporary(ContactPictures.Size, ContactPictures.Size, 0, RenderTextureFormat.ARGB32);
            RenderTexture was = RenderTexture.active;
            try
            {
                Graphics.Blit(found.texture, rt, scale, offset);
                RenderTexture.active = rt;
                p.Texture.ReadPixels(new Rect(0f, 0f, ContactPictures.Size, ContactPictures.Size), 0, 0, false);
                p.Texture.Apply(false, false);
            }
            finally
            {
                RenderTexture.active = was;
                RenderTexture.ReleaseTemporary(rt);
            }
            return true;
        }

        private void PaintFace(ContactPictures.Picture p)
        {
            string file = Path.Combine(Folder, ContactCatalogue.CacheName(p.Look.Female, p.Look.Age, p.Seed));
            if (File.Exists(file) && FromJpeg(p, File.ReadAllBytes(file)))
            {
                Done(p, "the portrait cache");
                return;
            }
            float now = Time.realtimeSinceStartup;
            if (!PortraitTurn.Ready || !PortraitTurn.Take(this, now))
            {
                Requeue(p);   // the portrait camera is not there yet (the main menu, a load), or Flare's painter has it
                p.Tries--;    // not a failure
                return;
            }
            _drawing = p;
            _drawingSince = now;
            using var piece = SessionWatch.Piece("contacts: a face drawn by the game's portrait camera");   // once per look, ever
            Action<Sprite> back = sprite => UiClone.Safe(() => Drawn(p, sprite, file));
            if (!PortraitTurn.Face(p.Look.Female, p.Look.Age, p.Seed, back))
            {
                _drawing = null;
                PortraitTurn.Give(this, now, false);
                Requeue(p);
                p.Tries--;
            }
        }

        private void Drawn(ContactPictures.Picture p, Sprite sprite, string file)
        {
            if (_drawing == p)
            {
                _drawing = null;
            }
            _next = Time.realtimeSinceStartup + FacePaceSeconds;
            PortraitTurn.Give(this, Time.realtimeSinceStartup, SceneFinds.Hidden);
            Texture2D source = sprite != null ? sprite.texture : null;
            if (source == null || !source.isReadable)
            {
                if (sprite != null)
                {
                    Destroy(sprite);   // the game's portrait is made for this call alone
                }
                if (source != null)
                {
                    Destroy(source);
                }
                Requeue(p);
                return;
            }
            Copy(source, p.Texture);
            Destroy(sprite);
            Destroy(source);
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllBytes(file, p.Texture.EncodeToJPG(90));
            }
            catch (Exception e)
            {
                ContactPictures.Logger?.Warn("Contacts: a face could not be kept in the portrait cache (" + e.Message + "); it is drawn again on the next load.");
            }
            Done(p, "the game's portrait camera");
        }

        private static bool FromJpeg(ContactPictures.Picture p, byte[] bytes)
        {
            var loaded = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try
            {
                if (!loaded.LoadImage(bytes, false))
                {
                    return false;
                }
                Copy(loaded, p.Texture);
                return true;
            }
            finally
            {
                Destroy(loaded);
            }
        }

        // A readable texture into a picture's own memory, scaled if need be; no array is made.
        private static void Copy(Texture2D source, Texture2D into)
        {
            int size = ContactPictures.Size;
            NativeArray<Color32> pixels = into.GetPixelData<Color32>(0);
            bool same = source.width == size && source.height == size;
            if (same && source.format == TextureFormat.RGBA32)
            {
                pixels.CopyFrom(source.GetPixelData<Color32>(0));
            }
            else if (same && (source.format == TextureFormat.RGB24 || source.format == TextureFormat.ARGB32))
            {
                NativeArray<byte> from = source.GetPixelData<byte>(0);
                bool rgb = source.format == TextureFormat.RGB24;
                for (int i = 0, b = 0; i < pixels.Length; i++)
                {
                    if (rgb)
                    {
                        pixels[i] = new Color32(from[b], from[b + 1], from[b + 2], 255);
                        b += 3;
                    }
                    else
                    {
                        pixels[i] = new Color32(from[b + 1], from[b + 2], from[b + 3], from[b]);
                        b += 4;
                    }
                }
            }
            else if (same)
            {
                into.SetPixels32(source.GetPixels32());   // a format the camera has never given: the engine converts it
            }
            else
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        pixels[y * size + x] = source.GetPixelBilinear((x + 0.5f) / size, (y + 0.5f) / size);
                    }
                }
            }
            into.Apply(false, false);
        }

        private static void Done(ContactPictures.Picture p, string from)
        {
            p.Drawn = true;
            p.Tries = 0;
            if (Meta.Dev.Verbose)
            {
                ContactPictures.Logger?.Info("Contacts check (picture): '" + p.Id + "' is a "
                                             + (p.Look.Kind == PictureKind.Person ? (p.Look.Female ? "woman" : "man") + " of " + p.Look.Age + ", seed " + p.Seed : "logo")
                                             + ", from " + from + "; " + ContactPictures.Report() + ": PASS");
            }
        }
    }
}
