using System;
using System.Collections.Generic;
using RealLifeOverhaul.Flare;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Flare's kit face, its pictures: a post's photo and a profile's banner by the key the face gives them ("flare:" the
    /// shape and the account's own key, FlareFace.PictureKey). The textures are the old panel's cache (FlarePictures, at
    /// most 16, the oldest destroyed); each is cut to its box's shape once into a sprite, at most <see cref="SpritesMost"/>,
    /// the oldest destroyed, and every one destroyed when the city unloads. Cost: nothing per frame; a picture that comes
    /// in asks for one layout.
    /// </summary>
    public sealed partial class FlareScreen
    {
        private const int SpritesMost = 24;

        private static FlareScreen _current;   // the city's screen: the picture hook asks it
        private static bool _hooked;

        private FlarePictures _pictures;
        private bool _picturesCame;
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>(SpritesMost);
        private readonly List<string> _spriteOrder = new List<string>(SpritesMost);   // used last at the end

        private bool PicturesCame => _picturesCame;

        // Laid: what came in so far is drawn.
        private void PicturesLaid()
        {
            _picturesCame = false;
        }

        // The screen of a new city: the last one's sprites and textures go; the hook is put in once a session.
        private void PicturesOn()
        {
            if (_current != null && _current != this)
            {
                _current.ReleasePictures();
            }
            _current = this;
            if (_flare != null)
            {
                _pictures = new FlarePictures(_flare, () => _picturesCame = true);
            }
            if (!_hooked)
            {
                _hooked = true;
                Func<string, Sprite> before = UiPictures.Find;
                UiPictures.Find = key => key != null && key.StartsWith(FlareFace.PicturePrefix, StringComparison.Ordinal) ? _current?.Picture(key) : before?.Invoke(key);
            }
        }

        /// <summary>The city unloads or Flare stops: every sprite and texture the kit face made is destroyed.</summary>
        public static void Release()
        {
            _current?.ReleasePictures();
        }

        private void ReleasePictures()
        {
            foreach (Sprite sprite in _sprites.Values)
            {
                if (sprite != null)
                {
                    UnityEngine.Object.Destroy(sprite);
                }
            }
            _sprites.Clear();
            _spriteOrder.Clear();
            _pictures?.Clear();
        }

        // A picture item's sprite: the texture cut to the box's shape, made once; null while the texture is still coming.
        private Sprite Picture(string item)
        {
            if (_pictures == null || !FlareFace.PictureOf(item, out float aspect, out string key))
            {
                return null;
            }
            Texture2D texture = _pictures.Of(key);
            if (texture == null)
            {
                return null;
            }
            if (_sprites.TryGetValue(item, out Sprite made))
            {
                if (made != null && made.texture == texture)
                {
                    if (_spriteOrder.Count == 0 || _spriteOrder[_spriteOrder.Count - 1] != item)
                    {
                        _spriteOrder.Remove(item);
                        _spriteOrder.Add(item);
                    }
                    return made;
                }
                if (made != null)
                {
                    UnityEngine.Object.Destroy(made);   // its texture was let go and made again
                }
                _sprites.Remove(item);
                _spriteOrder.Remove(item);
            }
            float w = texture.width, h = texture.height;
            Rect cut = w / h > aspect ? new Rect((w - h * aspect) / 2f, 0f, h * aspect, h) : new Rect(0f, (h - w / aspect) / 2f, w, w / aspect);
            Sprite sprite = Sprite.Create(texture, cut, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "Flare picture";
            _sprites[item] = sprite;
            _spriteOrder.Add(item);
            while (_spriteOrder.Count > SpritesMost)
            {
                string oldest = _spriteOrder[0];
                _spriteOrder.RemoveAt(0);
                if (_sprites.TryGetValue(oldest, out Sprite gone) && gone != null)
                {
                    UnityEngine.Object.Destroy(gone);
                }
                _sprites.Remove(oldest);
            }
            return sprite;
        }
    }
}
