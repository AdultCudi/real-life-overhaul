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
    /// COST. Nothing while there is nothing to paint: the component switches itself off. While there is: one picture
    /// every PaceSeconds of real time; a logo is 65,536 samples of the shape once; a face read from the cache is one
    /// JPEG decode; a face drawn is one render of the game's portrait camera with its own read-back (the game's own
    /// cost, the same as drawing a rival) and is done at most once per look, ever, with the session watch told first.
    /// </summary>
    public sealed class ContactPainter : MonoBehaviour
    {
        private const float PaceSeconds = 0.4f;     // between two pictures: never two in one frame
        private const float FacePaceSeconds = 2f;   // after a face drawn by the portrait camera
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

        private void Update()
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
            _next = Time.realtimeSinceStartup + PaceSeconds;
            ContactPictures.Picture p = Wanted.Dequeue();
            p.Queued = false;
            if (p.Drawn)
            {
                return;
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
            if (p.Look.LikeGame != null && CopyGamePicture(p, p.Look.LikeGame))
            {
                Done(p, "the game's own picture '" + p.Look.LikeGame + "'");
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
            var pixels = new Color32[size * size];
            // The shape is fitted by what is drawn of it, not by its canvas: as high as GlyphShare of the square, and a wide
            // one (a car) as wide as WideShare, centred. One pass over the shape's pixels to find its bounds.
            int minX = 0, minY = 0, maxX = -1, maxY = -1;
            if (shape != null)
            {
                Bounds(shape, out minX, out minY, out maxX, out maxY);
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
                    float ink = maxX >= 0 && tx >= minX && tx <= maxX + 1 && ty >= minY && ty <= maxY + 1 ? shape.GetPixelBilinear(tx / shape.width, ty / shape.height).a : 0f;
                    pixels[y * size + x] = Color32.Lerp(back, white, ink);
                }
            }
            if (shape != null)
            {
                Destroy(shape);
            }
            p.Texture.SetPixels32(pixels);
            p.Texture.Apply(false, false);
            Done(p, "the game's logo shape '" + p.Look.Shape + "'" + (shape == null ? " (not found: the game's own 'not found' shape)" : string.Empty));
        }

        // The box of a shape's pixels that are drawn (alpha over a twentieth).
        private static void Bounds(Texture2D shape, out int minX, out int minY, out int maxX, out int maxY)
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
            SessionWatch.Mark("contacts: a face drawn by the game's portrait camera");   // once per look, ever: a render and a read-back
            Action<Sprite> back = sprite => UiClone.Safe(() => Drawn(p, sprite, file));
            int days = p.Look.Age * Mathf.Max(1, SaveGameManager.Current != null && SaveGameManager.Current.gameVariables != null ? SaveGameManager.Current.gameVariables.daysPerYear : 60);
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

        // A readable texture into a picture's own, scaled to its size if it is not already.
        private static void Copy(Texture2D source, Texture2D into)
        {
            int size = ContactPictures.Size;
            Color32[] pixels;
            if (source.width == size && source.height == size)
            {
                pixels = source.GetPixels32();
            }
            else
            {
                pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        pixels[y * size + x] = source.GetPixelBilinear((x + 0.5f) / size, (y + 0.5f) / size);
                    }
                }
            }
            into.SetPixels32(pixels);
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
            _generator = typeof(PortraitGenerator).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            _setter = typeof(PortraitGenerator).GetField("portraitAppearanceSetter", BindingFlags.NonPublic | BindingFlags.Instance);
            if (_setter != null && _setter.FieldType != typeof(AppearanceSetter))
            {
                _setter = null;
            }
        }
    }
}
