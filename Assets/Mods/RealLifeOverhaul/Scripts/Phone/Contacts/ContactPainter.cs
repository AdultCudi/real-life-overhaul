using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using RealLifeOverhaul.GameUi;
using UI.Load;
using Unity.Collections;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Paints the contacts' pictures one at a time: a logo is the game's logo shape in white on the catalogue's colour (or
    /// the game's own picture for that kind of contact), a face comes from <see cref="PortraitTurn"/>. Both are kept under
    /// RealLifeOverhaul/Portraits, named by what they are drawn from. Every file is looked for, read, written and deleted on
    /// a worker, one after another in the order asked (a write is on disk before a later read of it); the main thread
    /// only puts the bytes into the texture. Cost: idle, nothing (it switches itself off); busy, one picture a frame at
    /// most, a kept one about a millisecond of it, and a picture not yet kept painted in its frame alone.
    /// </summary>
    public sealed class ContactPainter : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("phone", "ContactPainter", typeof(ContactPainter));

        private const double KeptMs = 3.0;          // milliseconds of the main thread a kept picture may take: its bytes into the texture, a face's JPEG decoded
        private const float FacePaceSeconds = 2f;   // after a face drawn by the portrait camera
        private const int LogoBytes = ContactPictures.Size * ContactPictures.Size * 4;   // RGBA, a byte each
        private const string LogoVersion = "logo_v1_";   // a new way of painting changes this, and every kept logo is painted again
        private const float FaceTimeout = 15f;      // seconds to wait for the camera's read-back before trying again
        private const int TriesMost = 3;
        private const float GlyphShare = 0.62f;     // of the square's height the shape takes, as the game's own logos leave a margin
        private const float WideShare = 0.8f;       // of its width, for a shape wider than it is high

        private static ContactPainter _instance;
        private static readonly Queue<ContactPictures.Picture> Wanted = new Queue<ContactPictures.Picture>(32);

        private static byte[] _logoBuffer;          // one for the session: every kept logo read passes through it, on the worker, while no other read is out
        private static byte[] _rgb, _rgba;          // the main thread's own: a kept face's decoded rows, and as the texture's
        private static Task _io = Task.CompletedTask;   // the files' work, one after another on a worker
        private static string _folder, _icons;      // the cache's folder and the game's logo shapes', read on the main thread once
        private static int _build = -1;             // the game's build, which names a kept copy of one of its pictures

        private ContactPictures.Picture _drawing;   // a face waiting for the portrait camera's read-back
        private float _drawingSince, _next;
        private Task<Fetch> _fetch;                 // the next picture's kept file, being looked for and read on the worker

        private enum Found
        {
            Nothing,     // not kept: painted, or drawn by the portrait camera
            GameLogo,    // the kept copy of the game's own picture, in the logo buffer
            ShapeLogo,   // the kept logo painted from the game's shape, in the logo buffer
            Face         // the kept face's JPEG
        }

        // One picture's kept file, looked for and read on the worker; the look and seed it was asked with, so a picture
        // given another look meanwhile is never painted with the old one.
        private sealed class Fetch
        {
            public ContactPictures.Picture P;
            public ContactLook Look;
            public int Seed;
            public string Folder, Icons, GameFile, FaceFile;   // given by the main thread
            public string ShapeFile;   // the kept logo's name: from the shape file's size and date, so found on the worker; null if the game has no such shape
            public Found What;
            public byte[] Jpeg;        // a kept face
            public byte[] ShapePng;    // a logo painted from nothing: the game's shape file (or its 'not found' one), null if neither is there
            public string ShapePath;   // ...and the path it was read from, the texture's name as the game's own helper gives it
            public Exception Fault;    // a file that could not be read: the picture is tried again, as a fault in painting it is
        }

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

        /// <summary>Deletes a face's file in the portrait cache, on the worker before any later read, so it is drawn again.</summary>
        internal static void Forget(ContactPictures.Picture p)
        {
            string file = Path.Combine(Folder, ContactCatalogue.CacheName(p.Look.Female, p.Look.Age, p.Seed));
            OnWorker(() =>
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }, "a face could not be taken out of the portrait cache");
        }

        private static string Folder => _folder ??= Path.Combine(Application.persistentDataPath, "RealLifeOverhaul", "Portraits");

        private static string Icons => _icons ??= LogoHelper.GetBuildInIconsFolder();

        private static int Build => _build >= 0 ? _build : (_build = Nav.NavBaseGame.Build());

        // A piece of file work queued behind the others on the worker; a fault is written to the log with what failed.
        private static void OnWorker(Action work, string failed)
        {
            _io = _io.ContinueWith(_ =>
            {
                try
                {
                    work();
                }
                catch (Exception e)
                {
                    ContactPictures.Logger?.Warn("Contacts: " + failed + " (" + e.Message + "); it is made again on the next load.");
                }
            }, TaskScheduler.Default);
        }

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
            if (_fetch != null)
            {
                if (!_fetch.IsCompleted || LoadScene.isLoading)
                {
                    return;   // the file is still being read: the main thread does nothing for it
                }
                Fetch f = _fetch.Result;   // Read never throws: a fault comes back in the fetch
                _fetch = null;
                if (f.Fault != null)
                {
                    ContactPictures.Logger?.Error(f.Fault);
                    Requeue(f.P);
                    return;
                }
                try
                {
                    Use(f);
                }
                catch (Exception e)
                {
                    ContactPictures.Logger?.Error(e);
                    Requeue(f.P);
                }
                return;   // one picture a frame: a kept one is a millisecond, one made from nothing has the frame to itself
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
            ContactPictures.Picture p = null;
            while (p == null && Wanted.Count > 0)
            {
                p = Wanted.Dequeue();
                p.Queued = false;
                p = p.Drawn ? null : p;   // painted meanwhile: nothing to read
            }
            if (p != null)
            {
                _fetch = Ask(p);
            }
        }

        // The picture's kept file asked of the worker, behind any file work already given it: once a picture, not a frame.
        private static Task<Fetch> Ask(ContactPictures.Picture p)
        {
            var ask = new Fetch
            {
                P = p, Look = p.Look, Seed = p.Seed, Folder = Folder, Icons = Icons,
                GameFile = p.Look.Kind == PictureKind.Logo && p.Look.LikeGame != null ? GameLogoFile(p.Look.LikeGame) : null,
                FaceFile = p.Look.Kind == PictureKind.Logo ? null : Path.Combine(Folder, ContactCatalogue.CacheName(p.Look.Female, p.Look.Age, p.Seed))
            };
            Task<Fetch> read = _io.ContinueWith(_ => Read(ask), TaskScheduler.Default);
            _io = read;
            return read;
        }

        // On the worker: the picture's kept file looked for and read, in the order the painting tries them.
        private static Fetch Read(Fetch f)
        {
            try
            {
                return Look(f);
            }
            catch (Exception e)
            {
                f.Fault = e;
                return f;
            }
        }

        private static Fetch Look(Fetch f)
        {
            if (f.Look.Kind != PictureKind.Logo)
            {
                f.Jpeg = File.Exists(f.FaceFile) ? File.ReadAllBytes(f.FaceFile) : null;
                f.What = f.Jpeg != null ? Found.Face : Found.Nothing;
                return f;
            }
            if (ReadLogo(f.GameFile))
            {
                f.What = Found.GameLogo;
                return f;
            }
            f.ShapeFile = ShapeLogoFile(f.Look, f.Icons, f.Folder);
            if (ReadLogo(f.ShapeFile))
            {
                f.What = Found.ShapeLogo;
                return f;
            }
            f.What = Found.Nothing;
            string path = Path.Combine(f.Icons, f.Look.Shape + ".png");
            if (!File.Exists(path))
            {
                path = Path.Combine(f.Icons, "notfound.png");   // the game's own stand-in, as PaintLogo has always fallen back to
            }
            if (File.Exists(path))
            {
                f.ShapePng = File.ReadAllBytes(path);
                f.ShapePath = path;
            }
            return f;
        }

        // On the main thread: what the worker found put into the picture, or the picture made from nothing.
        private void Use(Fetch f)
        {
            ContactPictures.Picture p = f.P;
            if (p.Drawn)
            {
                return;
            }
            if (!Same(p.Look, f.Look) || p.Seed != f.Seed)
            {
                if (!p.Queued)
                {
                    p.Queued = true;   // given another look while its file was read: read again for that one
                    Wanted.Enqueue(p);
                }
                return;
            }
            long began = Stopwatch.GetTimestamp();
            if (p.Look.Kind == PictureKind.Logo)
            {
                PaintLogo(p, f, began);
            }
            else
            {
                PaintFace(p, f, began);
            }
        }

        private static bool Same(ContactLook a, ContactLook b)
        {
            return a.Kind == b.Kind && a.Female == b.Female && a.Age == b.Age && a.Shape == b.Shape && a.LikeGame == b.LikeGame && a.R == b.R && a.G == b.G && a.B == b.B;
        }

        // A kept logo copied from one of the game's own pictures: by its name and the game's build.
        private static string GameLogoFile(string name)
        {
            return Path.Combine(Folder, LogoVersion + "game_" + name + "_" + Build + ".raw");
        }

        // On the worker: named by the shape, the colour and the shape file's size and date; null if the game has no such file.
        private static string ShapeLogoFile(ContactLook look, string icons, string folder)
        {
            var shape = new FileInfo(Path.Combine(icons, look.Shape + ".png"));
            if (!shape.Exists)
            {
                return null;
            }
            return Path.Combine(folder, LogoVersion + look.Shape + "_" + look.R.ToString("X2") + look.G.ToString("X2") + look.B.ToString("X2")
                                        + "_" + shape.Length + "_" + shape.LastWriteTimeUtc.Ticks + ".raw");
        }

        // On the worker: a kept logo into the logo buffer. False if there is none, or it is not whole.
        private static bool ReadLogo(string file)
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
            return true;
        }

        // On the main thread: the logo buffer the worker filled into the picture.
        private static void UseLogo(ContactPictures.Picture p)
        {
            p.Texture.SetPixelData(_logoBuffer, 0);
            p.Texture.Apply(false, false);
        }

        // A logo that cannot be kept is painted again next time. Its bytes are copied here, once ever a logo, and written on the worker.
        private static void KeepLogo(ContactPictures.Picture p, string file)
        {
            if (file == null)
            {
                return;
            }
            NativeArray<byte> bytes = p.Texture.GetPixelData<byte>(0);
            if (bytes.Length != LogoBytes)
            {
                return;
            }
            byte[] kept = bytes.ToArray();
            string folder = Folder;
            OnWorker(() =>
            {
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(file, kept);
            }, "a logo could not be kept in the portrait cache");
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

        private static void PaintLogo(ContactPictures.Picture p, Fetch f, long began)
        {
            if (p.Look.LikeGame != null)
            {
                if (f.What == Found.GameLogo)
                {
                    UseLogo(p);
                    Done(p, "the game's own picture '" + p.Look.LikeGame + "', kept", began);
                    return;
                }
                if (CopyGamePicture(p, p.Look.LikeGame))
                {
                    KeepLogo(p, f.GameFile);
                    Done(p, "the game's own picture '" + p.Look.LikeGame + "'", -1);
                    return;
                }
            }
            string kept = f.ShapeFile;
            if (f.What == Found.ShapeLogo)
            {
                UseLogo(p);
                Done(p, "the game's logo shape '" + p.Look.Shape + "', kept", began);
                return;
            }
            Texture2D shape = ShapeTexture(f);
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
            Done(p, "the game's logo shape '" + p.Look.Shape + "'" + (shape == null ? " (not found: the game's own 'not found' shape)" : string.Empty), -1);
        }

        // The shape's texture from the bytes the worker read, made as the game's LogoHelper.GetLogoShapeTexture makes it from its file.
        private static Texture2D ShapeTexture(Fetch f)
        {
            if (f.ShapePng == null)
            {
                return null;
            }
            var shape = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave, name = f.ShapePath };   // destroyed by PaintLogo once sampled
            shape.LoadImage(f.ShapePng);
            shape.wrapMode = TextureWrapMode.Clamp;
            return shape;
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

        private void PaintFace(ContactPictures.Picture p, Fetch f, long began)
        {
            string file = f.FaceFile;
            if (f.What == Found.Face && FromJpeg(p, f.Jpeg))
            {
                Done(p, "the portrait cache", began);
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
            byte[] jpeg = p.Texture.EncodeToJPG(90);   // the engine's encoder on the texture: the very bytes it always kept
            string folder = Folder;
            OnWorker(() =>
            {
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(file, jpeg);
            }, "a face could not be kept in the portrait cache");
            Done(p, "the game's portrait camera", -1);
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
            else if (same && source.format == TextureFormat.RGB24)
            {
                // A kept face: its rows copied out at once and widened in a managed array, not three engine calls a pixel.
                int n = size * size;
                _rgb ??= new byte[n * 3];
                _rgba ??= new byte[n * 4];
                NativeArray<byte>.Copy(source.GetPixelData<byte>(0), _rgb, n * 3);
                for (int i = 0, b = 0, o = 0; i < n; i++, b += 3, o += 4)
                {
                    _rgba[o] = _rgb[b];
                    _rgba[o + 1] = _rgb[b + 1];
                    _rgba[o + 2] = _rgb[b + 2];
                    _rgba[o + 3] = 255;
                }
                into.SetPixelData(_rgba, 0);
            }
            else if (same && source.format == TextureFormat.ARGB32)
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

        // A kept picture (began: when its bytes started into the texture; -1 for one made from nothing) says the main
        // thread's time of it against KeptMs: its file was read on the worker.
        private static void Done(ContactPictures.Picture p, string from, long began)
        {
            p.Drawn = true;
            p.Tries = 0;
            if (Meta.Dev.Verbose)
            {
                double ms = began >= 0 ? (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency : -1.0;
                ContactPictures.Logger?.Info("Contacts check (picture): '" + p.Id + "' is a "
                                             + (p.Look.Kind == PictureKind.Person ? (p.Look.Female ? "woman" : "man") + " of " + p.Look.Age + ", seed " + p.Seed : "logo")
                                             + ", from " + from
                                             + (ms >= 0.0 ? " (its file read on a worker, " + ms.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                                                            + " ms of the main thread, wanted under " + KeptMs + " ms)" : string.Empty)
                                             + "; " + ContactPictures.Report() + ": " + (ms <= KeptMs ? "PASS" : "SLOW"));
            }
        }
    }
}
