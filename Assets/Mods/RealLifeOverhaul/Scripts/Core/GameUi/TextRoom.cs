using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// On a text of the mod's that the text engine may cut (dots or a plain end), outside the RLO interface's own view:
    /// keeps its box from dropping it. TextMeshPro ends such a text at its first character when a line stands taller
    /// than its box, and a line of Korean, Chinese or Japanese is drawn by the game's fallback fonts, which stand taller
    /// than its interface font, the one the mod's boxes are sized by (<see cref="TextRoomRules"/>). The text's margins
    /// give a box that is too short the room it lacks, on the side where its alignment leaves the words where they
    /// were. The text's own margins are kept under the room, those its maker sets later too. A text that sizes itself
    /// to its box gets none. Put on by <see cref="On"/> where a text is made: the size, alignment and box its maker's
    /// callers give it afterwards are followed, and a copy of the text (a row made from a pattern) keeps its room.
    /// It runs outside play as well, so that the offline preview's fonts check, which is not in play, holds it against
    /// the text engine as the game runs it (RealLifeOverhaul.Build/UiPreview/UiPreviewRunner.Fonts.cs).
    /// Cost: nothing a frame; a few sums when it is put on or switched on, when its box changes size, and each time
    /// the text engine is told that the text changed.
    /// </summary>
    [ExecuteAlways]
    public sealed class TextRoom : UIBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "TextRoom", typeof(TextRoom));   // the engine's calls of it (Core/Guard)

        // Both are kept by a copy of the text's object, which has the text's margins with their room and must tell the two apart.
        [SerializeField] private Vector4 _own;     // the text's margins without any room
        [SerializeField] private Vector4 _given;   // its margins as last written here: any other value is its maker's doing

        private TMP_Text _text;
        private UnityAction _fit;   // made once

        /// <summary>Gives a text its room, now and whenever its box, its size or its alignment changes; a text that has it already is fitted again.</summary>
        public static void On(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }
            TextRoom room = text.GetComponent<TextRoom>();
            if (room == null)
            {
                room = text.gameObject.AddComponent<TextRoom>();
            }
            room.Fit();
        }

        protected override void OnEnable()
        {
            Fit();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            Fit();
        }

        protected override void OnDestroy()
        {
            if (_text != null && _fit != null)
            {
                _text.UnregisterDirtyVerticesCallback(_fit);
            }
        }

        private void Fit()
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                if (_text == null)
                {
                    _text = GetComponent<TMP_Text>();
                    if (_text == null)
                    {
                        return;
                    }
                    // A maker's callers set a text's size and alignment after it is made, in any order with its box: the text
                    // engine says so at each such change, while the text is shown.
                    _fit = Fit;
                    _text.RegisterDirtyVerticesCallback(_fit);
                }
                Vector4 now = _text.margin;
                if (now != _given)
                {
                    _own = now;
                }
                // A text that sizes itself to its box is left alone: room would let its Latin letters grow, and a tall line
                // only makes it smaller, down to its least size.
                TextRoomSide side = _text.enableAutoSizing ? TextRoomSide.None : Side(_text.verticalAlignment);
                float box = _text.rectTransform.rect.height - _own.y - _own.w;
                TextRoomRules.Room(box, _text.fontSize, side, out float over, out float under);
                // Noted before it is written: the engine answers a new margin by calling this again, and must find it given.
                _given = new Vector4(_own.x, _own.y - over, _own.z, _own.w - under);
                if (now != _given)
                {
                    _text.margin = _given;
                }
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // Where the text engine anchors a text by height: only a text centred by its capitals is moved by room on either side.
        private static TextRoomSide Side(VerticalAlignmentOptions vertical)
        {
            switch (vertical)
            {
                case VerticalAlignmentOptions.Top: return TextRoomSide.Under;
                case VerticalAlignmentOptions.Bottom: return TextRoomSide.Over;
                case VerticalAlignmentOptions.Capline: return TextRoomSide.None;
                default: return TextRoomSide.Both;
            }
        }
    }
}
