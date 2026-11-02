using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A person of the mod's own with a look (the cast, a partner, a roommate, a catalogue person such as Mom): their
    /// picture is the contact picture ContactPictures keeps, drawn by the game's portrait camera from the look they are
    /// built with in the world (their sex, age and seed, or the kept look of somebody met in the street) and kept in the
    /// mod's portrait cache. It answers once the picture is drawn; while it is not, it asks the painter to draw this one
    /// next, so a face on screen is not left behind the whole cast. Cost: a flag read a call.
    /// </summary>
    internal sealed class PictureFace : UiFaceSource
    {
        private readonly ContactPictures.Picture _picture;

        public PictureFace(ContactPictures.Picture picture)
        {
            _picture = picture;
        }

        public override Sprite Get()
        {
            if (_picture.Drawn && _picture.Sprite != null)
            {
                return _picture.Sprite;
            }
            ContactPainter.Hurry(_picture);
            return null;
        }
    }
}
