using System;
using RealLifeOverhaul.Photos;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Where the kit's picture items of photos find their sprites ("photo:" and the photo's name, from the director's small copies
    /// and, for the photo opened, its whole picture; the editor's preview). Hooked into UiPictures once a session, never again, so
    /// no screen wraps another's hook round in a ring. The whole picture and the preview are made by the Gallery, destroyed when
    /// it closes. Cost: one lookup a picture shown.
    /// </summary>
    internal static class GalleryPictures
    {
        public const string EditKey = "photoedit:preview";

        private static bool _hooked;
        private static Func<string, Sprite> _before;
        private static Sprite _whole;
        private static Texture2D _wholeTexture;
        private static string _wholeName;
        private static Sprite _preview;
        private static Texture2D _previewTexture;

        public static void Install()
        {
            if (_hooked)
            {
                return;
            }
            _hooked = true;
            _before = UiPictures.Find;
            UiPictures.Find = Find;
        }

        private static Sprite Find(string key)
        {
            if (key == EditKey)
            {
                return _preview;
            }
            if (key != null && key.StartsWith(PhotoThumbCache.Prefix, StringComparison.Ordinal))
            {
                string name = key.Substring(PhotoThumbCache.Prefix.Length);
                if (_whole != null && name == _wholeName)
                {
                    return _whole;
                }
                PhotosDirector director = PhotosDirector.Instance;
                return director != null ? director.Thumbs.Sprite(name) : null;
            }
            return _before != null ? _before(key) : null;
        }

        /// <summary>The photo opened, whole: replaces the one before.</summary>
        public static void SetWhole(string name, Texture2D texture)
        {
            DropWhole();
            _wholeName = name;
            _wholeTexture = texture;
            _whole = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            _whole.name = "RealLifeOverhaul Photo";
        }

        public static string WholeName => _whole != null ? _wholeName : null;

        public static void DropWhole()
        {
            if (_whole != null)
            {
                UnityEngine.Object.Destroy(_whole);
            }
            if (_wholeTexture != null)
            {
                UnityEngine.Object.Destroy(_wholeTexture);
            }
            _whole = null;
            _wholeTexture = null;
            _wholeName = null;
        }

        /// <summary>The editor's preview, made from RGB24 rows (bottom up); replaces the one before.</summary>
        public static void SetPreview(byte[] rows, int width, int height)
        {
            DropPreview();
            _previewTexture = PhotosGame.SmallTexture(rows, width, height);
            if (_previewTexture != null)
            {
                _preview = Sprite.Create(_previewTexture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
                _preview.name = "RealLifeOverhaul Photo Preview";
            }
        }

        public static void DropPreview()
        {
            if (_preview != null)
            {
                UnityEngine.Object.Destroy(_preview);
            }
            if (_previewTexture != null)
            {
                UnityEngine.Object.Destroy(_previewTexture);
            }
            _preview = null;
            _previewTexture = null;
        }
    }
}
