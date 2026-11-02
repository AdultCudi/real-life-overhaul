using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Where the game's own picture of a person is read from: a sprite the game holds, one the game's portrait camera draws
    /// from the character's data, or one the mod's portrait cache keeps. The screen that reads the game gives it once a
    /// person (<see cref="UiFaces.Source"/>); the view asks it each time it binds the person's face. It answers null while
    /// the picture is still being made, and must start making it once and never twice. Cost: a lookup a call.
    /// </summary>
    public abstract class UiFaceSource
    {
        /// <summary>The game's sprite of the person, or null while it is not there yet.</summary>
        public abstract Sprite Get();
    }

    /// <summary>
    /// "The picture of a person", the one rule every portrait of the kit is drawn by (the view asks <see cref="Of"/> for each
    /// face): the game's own picture of them, whether a sprite or the game's generator drawing their character, then, only
    /// for a person the game has no picture of, the kit's painted face, which follows the sex and age the mod records for
    /// them (<see cref="UiFaceRules"/>). A game picture is copied once into a round face of its own (a person looks the
    /// same on every screen at every size) and kept for the session, the oldest let go past <see cref="MostRound"/>. While a
    /// picture is being made the person shows a plain disc, never a stranger's face. Cost: a dictionary read a face bound,
    /// and some 0.3 ms once a game picture arrives; nothing a frame.
    /// </summary>
    public static class UiFaces
    {
        private const int Size = 128;           // texels of a round face: shown at 40 to 96 px, and two mips below
        private const int MostRound = 160;      // round faces kept at once, the oldest let go first
        private const float PixelsPerUnit = 100f;

        private static readonly Dictionary<string, UiFaceSource> Sources = new Dictionary<string, UiFaceSource>(64, System.StringComparer.Ordinal);
        private static readonly Dictionary<int, Sprite> Rounds = new Dictionary<int, Sprite>(32);
        private static readonly Queue<int> RoundOrder = new Queue<int>(32);

        /// <summary>Changes whenever a person is told or a game picture arrives: the host lays its screen out again.</summary>
        public static int Version => UiFaceBook.Version;

        /// <summary>True when a game picture is already told for the name.</summary>
        public static bool Has(string name) => !string.IsNullOrEmpty(name) && Sources.ContainsKey(name);

        /// <summary>The name has a game picture, read from this source; told again with the same source it does nothing.</summary>
        public static void Source(string name, UiFaceSource source)
        {
            if (string.IsNullOrEmpty(name) || source == null)
            {
                return;
            }
            if (Sources.TryGetValue(name, out UiFaceSource now) && now == source)
            {
                return;
            }
            Sources[name] = source;
            UiFaceBook.KnowGame(name);
            UiFaceBook.Changed();
        }

        /// <summary>The mod records this person's sex and age: it is what their painted face follows if the game has no picture of them.</summary>
        public static void Traits(string name, bool female, int age) => UiFaceBook.KnowTraits(name, female, age);

        /// <summary>A game picture arrived: screens showing it are laid out again.</summary>
        public static void Arrived() => UiFaceBook.Changed();

        /// <summary>A picture that was drawn again under the same sprite: its round copy is made afresh the next time it is shown.</summary>
        public static void Drop(Sprite game)
        {
            if (game != null && Rounds.TryGetValue(game.GetInstanceID(), out Sprite made))
            {
                Let(made);
                Rounds.Remove(game.GetInstanceID());
            }
        }

        /// <summary>A new city: nobody told stays, the round faces made go.</summary>
        public static void Clear()
        {
            Sources.Clear();
            UiFaceBook.Clear();
            foreach (KeyValuePair<int, Sprite> r in Rounds)
            {
                Let(r.Value);
            }
            Rounds.Clear();
            RoundOrder.Clear();
        }

        /// <summary>
        /// The face of a person by the name a screen shows them under, on a disc of a hue for a painted one. <paramref name="plain"/>
        /// says the game's picture of them is still coming: the face is a disc to be tinted, not a picture.
        /// </summary>
        public static Sprite Of(string name, float hue, out bool plain)
        {
            plain = false;
            UiFaceBook.TryGet(name, out UiFaceFacts facts);
            if (facts.HasGame)
            {
                Sprite game = Sources.TryGetValue(name, out UiFaceSource source) ? source.Get() : null;
                facts.GameReady = game != null;
                if (UiFaceRules.From(facts) == UiFaceFrom.Game)
                {
                    return Round(game);
                }
                plain = true;
                return UiSprites.Circle;
            }
            return UiSprites.Painted(UiFaceRules.PaintSeed(name, facts), UiFaceRules.PaintAge(facts), hue);
        }

        // The game's sprite as a round face of its own, made once: through a render texture, so a sprite the game did not
        // make readable (an asset's) copies as well as one the portrait camera drew.
        private static Sprite Round(Sprite game)
        {
            int id = game.GetInstanceID();
            if (Rounds.TryGetValue(id, out Sprite made) && made != null)
            {
                return made;
            }
            while (Rounds.Count >= MostRound && RoundOrder.Count > 0)
            {
                int oldest = RoundOrder.Dequeue();
                if (Rounds.TryGetValue(oldest, out Sprite gone))
                {
                    Let(gone);
                }
                Rounds.Remove(oldest);
            }
            Texture source = game.texture;
            Rect rect = game.textureRect;
            RenderTexture target = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture before = RenderTexture.active;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = "UiGameFace", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            try
            {
                Graphics.Blit(source, target, new Vector2(rect.width / source.width, rect.height / source.height), new Vector2(rect.x / source.width, rect.y / source.height));
                RenderTexture.active = target;
                tex.ReadPixels(new Rect(0f, 0f, Size, Size), 0, 0, false);
            }
            finally
            {
                RenderTexture.active = before;
                RenderTexture.ReleaseTemporary(target);
            }
            Color32[] px = tex.GetPixels32();
            const float centre = (Size - 1) / 2f, rim = Size / 2f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float d = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                    px[y * Size + x].a = (byte)Mathf.RoundToInt(Mathf.Clamp01(rim - d) * 255f);   // soft over one texel, as the painted face's edge
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            made = Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            Rounds[id] = made;
            RoundOrder.Enqueue(id);
            return made;
        }

        private static void Let(Sprite sprite)
        {
            if (sprite != null)
            {
                Object.Destroy(sprite.texture);
                Object.Destroy(sprite);
            }
        }
    }
}
