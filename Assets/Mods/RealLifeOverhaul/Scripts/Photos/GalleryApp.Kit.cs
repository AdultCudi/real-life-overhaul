using UnityEngine;

namespace RealLifeOverhaul.Photos
{
    sealed partial class GalleryApp
    {
        internal int ThumbsReady { get; private set; }

        /// <summary>Asks the worker for a small copy, the same path the grid already uses.</summary>
        internal void WantShown(string name)
        {
            Want(name);
            StartReading();
        }

        internal Texture2D Thumb(string name)
        {
            Texture2D texture;
            return name != null && _thumbs.TryGetValue(name, out texture) ? texture : null;
        }

        internal void NoteThumb()
        {
            ThumbsReady++;
        }
    }
}
