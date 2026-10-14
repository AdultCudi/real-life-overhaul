using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BigAmbitions.Characters;
using BigAmbitions.Characters.Appearance;
using Character;
using Character.Customization;
using RealLifeOverhaul.GameUi;
using UI.Load;
using Unity.Collections;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Paints the contacts' pictures (<see cref="ContactPictures"/>) from the game's own assets, one at a time.
    ///
    ///   a logo    the game's logo shape (StreamingAssets/LogoShapes, read with the game's own LogoHelper.GetLogoShapeTexture)
    ///             in white on the catalogue's colour, the way a business logo is a shape on a colour; or, where the game
    ///             has a picture of its own for the same kind of contact (its tow service), that picture
    ///   a face    from the game's portrait camera (PortraitGenerator.Create, as the Rivals app draws a rival), with the
    ///             look built exactly as the mod builds the person in the city: AppearanceSetter.RandomizeElements with
    ///             the person's sex, the skin of their seed, casual clothes and their seed, their age, and the build capped
    ///             as the mod's own people are (Social/SocialCast.Dress). The face is kept as a JPEG under the game's
    ///             data folder (RealLifeOverhaul/Portraits) and read from there on every later load
    ///
    /// A logo is kept there too, once painted, as its picture's own bytes (256 KB, nothing to decode), named by the shape,
    /// the colour, and the size and date of the game's shape file (or the game's own picture and the game's build), so a
    /// shape the game changes is painted again. Every picture is written into the texture's own memory
    /// (Texture2D.GetPixelData) or through one buffer kept for the session: no array per picture, where each picture made
    /// some 0.5 to 1.3 MB of garbage (the owner's log of 14 Oct 2026: 561 KB a second under this in the first minute).
    ///
    /// COST. Nothing while there is nothing to paint: the component switches itself off. While there is: pictures one after
    /// another for about a millisecond of a frame (<see cref="WorkSlice"/>; eight behind a black screen), the first of a frame
    /// always; a picture not in the cache (a logo's 65,536 samples of the shape, once ever; a face drawn by the game's
    /// portrait camera with its own read-back, once per look, ever, with the session watch told first) only as the first of
    /// a frame, alone. A logo from the cache is one read of a file and one upload; a face from the cache one JPEG decode.
    /// </summary>
    public sealed class ContactPainter : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("phone", "ContactPainter", typeof(ContactPainter));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const double SliceMs = 1.0;         // milliseconds of a frame in play the painting may take (the first picture of a frame always)
        private const double HiddenSliceMs = 8.0;   // ...and of a frame behind the game's black screen
        private const float FacePaceSeconds = 2f;   // after a face drawn by the portrait camera
        private const int LogoBytes = ContactPictures.Size * ContactPictures.Size * 4;   // a logo's picture as it is kept: RGBA, a byte each
        private const string LogoVersion = "logo_v1_";   // the painting's own version in a kept logo's name: a new way of painting paints them all again
        private const float FaceTimeout = 15f;      // seconds to wait for the camera's read-back before trying again
        private const int TriesMost = 3;
        private const float GlyphShare = 0.62f;     // of the square's height the shape takes, as the game's own logos leave a margin
        private const float WideShare = 0.8f;       // of its width, for a shape wider than it is high
        private const float BuildMost = 0.55f;      // the build cap the mod's people are made with (Social/SocialCast.MaxBuild)
        private static readonly AppearanceTag[] Casual = { AppearanceTag.Casual };

        private static ContactPainter _instance;
        private static readonly Queue<ContactPictures.Picture> Wanted = new Queue<ContactPictures.Picture>(32);
        private static FieldInfo _generator, _setter;
        private static bool _reflected, _toldFallback;

        private static byte[] _logoBuffer;          // one for the session, made the first time a logo is read or kept: what a kept logo passes through

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

        /// <summary>A face's file in the portrait cache is deleted, so that it is drawn again.</summary>
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
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
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
                }
                return;
            }
            if (Wanted.Count == 0)
            {
                enabled = false;   // nothing to paint: no cost at all until a picture is wanted
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

        // A kept logo painted from one of the game's shapes: by the shape, the colour, and the shape file's size and date. Null if the game has no such file.
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

        // A kept logo into the picture: one read through the session's buffer, one upload. False if there is none (or it is not whole).
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

        // The picture just painted, kept for every later load. A logo that cannot be kept is painted again next time.
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

        // ---------------------------------------------------------------- a logo

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
            NativeArray<Color32> pixels = p.Texture.GetPixelData<Color32>(0);   // the picture's own memory: no array of our own
            // The shape is fitted by what is drawn of it, not by its canvas: as high as GlyphShare of the square, and a wide
            // one (a car) as wide as WideShare, centred. One pass over the shape's pixels to find its bounds, and the same
            // pixels then sampled here (Ink), not through the engine one call a pixel: some 30,000 calls, most of a frame
            // of 11 ms for each logo in the first minute after a load.
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
                KeepLogo(p, kept);   // (the game's 'not found' shape stands in for a missing one: that is never kept)
            }
            Done(p, "the game's logo shape '" + p.Look.Shape + "'" + (shape == null ? " (not found: the game's own 'not found' shape)" : string.Empty));
        }

        // The shape's alpha at texel coordinates (tx, ty), filtered between the four nearest texels as the engine's
        // Texture2D.GetPixelBilinear does it for the game's shapes (texel centres at half-texel offsets; edges clamped, the
        // wrap mode LogoHelper.GetLogoShapeTexture gives them). Pure arithmetic over the pixels already read.
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

        // The box of a shape's pixels that are drawn (alpha over a twentieth), and the pixels it was read from.
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
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            if (maxX < 0)
            {
                minX = minY = 0;
            }
            return px;
        }

        // One of the game's own contact pictures, copied through the graphics card (it is packed and not readable as it is).
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

        // ---------------------------------------------------------------- a face

        private void PaintFace(ContactPictures.Picture p)
        {
            string file = Path.Combine(Folder, ContactCatalogue.CacheName(p.Look.Female, p.Look.Age, p.Seed));
            if (File.Exists(file) && FromJpeg(p, File.ReadAllBytes(file)))
            {
                Done(p, "the portrait cache");
                return;
            }
            PortraitGenerator generator = Generator();
            if (generator == null && _generator != null)
            {
                Requeue(p);   // the city's portrait camera is not there yet (the main menu, a load): asked again in a moment
                p.Tries--;    // (not a failure)
                return;
            }
            _drawing = p;
            _drawingSince = Time.realtimeSinceStartup;
            using var piece = SessionWatch.Piece("contacts: a face drawn by the game's portrait camera");   // once per look, ever: a render and a read-back
            Action<Sprite> back = sprite => UiClone.Safe(() => Drawn(p, sprite, file));
            int days = p.Look.Age * GameCalendar.DaysPerYear;
            BigAmbitions.Characters.Gender gender = p.Look.Female ? BigAmbitions.Characters.Gender.Female : BigAmbitions.Characters.Gender.Male;
            AppearanceSetter setter = generator != null ? Setter(generator) : null;
            if (setter == null)
            {
                // Without the camera's own appearance the game's public way is used: the same person each time, though not
                // dressed as the mod dresses them in the city.
                if (!_toldFallback)
                {
                    _toldFallback = true;
                    ContactPictures.Logger?.Warn("Contacts: the portrait camera is built another way in this version of the game: faces are drawn with the game's own random look for each person.");
                }
                PortraitGenerator.Create(gender, days, p.Seed.ToString(), p.Seed, null, null, back);
                return;
            }
            // The game colours a beard to match the hair by a roll of the engine's own dice: rolled from the same seed, as the
            // mod's own people are (Social/SocialCast, Family/FamilyCast, Home/Roommates/RoommateCast), and the dice put back.
            UnityEngine.Random.State dice = UnityEngine.Random.state;
            UnityEngine.Random.InitState(p.Seed);
            try
            {
                setter.data.ageInDays = days;
                setter.RandomizeElements(gender, SkinColorHelper.GetRandom(p.Seed), Casual, p.Seed);
                setter.data.fatness = Mathf.Min(setter.data.fatness, BuildMost);
                PortraitGenerator.Create(setter.data, null, null, back);
            }
            finally
            {
                UnityEngine.Random.state = dice;
            }
        }

        private void Drawn(ContactPictures.Picture p, Sprite sprite, string file)
        {
            if (_drawing == p)
            {
                _drawing = null;
            }
            _next = Time.realtimeSinceStartup + FacePaceSeconds;
            Texture2D source = sprite != null ? sprite.texture : null;
            if (source == null || !source.isReadable)
            {
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

        // A readable texture into a picture's own, scaled to its size if it is not already: written into the picture's own memory,
        // the source read in its own (a JPEG of the cache is RGB24, a picture of the camera RGBA32 or ARGB32), with no array made.
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
                into.SetPixels32(source.GetPixels32());   // (a format the camera has never given: the engine converts it, as before)
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
                ContactPictures.Logger?.Info("Contacts check (picture): '" + p.Id + "' is a " + (p.Look.Kind == PictureKind.Person ? (p.Look.Female ? "woman" : "man") + " of " + p.Look.Age + ", seed " + p.Seed : "logo")
                                             + ", from " + from + "; " + ContactPictures.Report() + ": PASS");
            }
        }

        // The game's portrait camera of this scene, and the appearance it draws (both private: read once).
        private static PortraitGenerator Generator()
        {
            Reflect();
            return _generator != null ? _generator.GetValue(null) as PortraitGenerator : null;   // (not known: the game's public call is used, and the time-out covers a scene without the camera)
        }

        private static AppearanceSetter Setter(PortraitGenerator generator)
        {
            Reflect();
            return _setter != null ? _setter.GetValue(generator) as AppearanceSetter : null;
        }

        private static void Reflect()
        {
            if (_reflected)
            {
                return;
            }
            _reflected = true;
            _generator = GameContract.PortraitInstance.Field;   // (resolved once: Core/GameContract)
            _setter = GameContract.PortraitCharacter.Field;
            if (_setter != null && _setter.FieldType != typeof(AppearanceSetter))
            {
                _setter = null;
            }
        }
    }
}
