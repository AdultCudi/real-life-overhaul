using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A line of a language's fallback characters drawn through the kit's own text, out of sight, and what TextMeshPro
    /// made of it: the proof on a player's own machine that such a line is drawn, where the text engine ends a text that
    /// may be cut when its line stands taller than its box (<see cref="UiFallbackRules"/>). The line is laid as every
    /// label is (one line that may be cut, in its box with its room, <see cref="UiTextRules.Room"/>), on a view and a
    /// canvas made for it and gone again in the same call, before a frame can show them. No engine type of the game: the
    /// offline probe in the preview project runs the same. Cost: once a language, a view of one text; nothing a frame.
    /// </summary>
    public static class UiFontProbe
    {
        public const int Most = 12;                     // characters in the line
        private const float Width = 480f;               // px: room for the line and to spare, so only its height could have it cut
        private const float Height = 64f;               // px: the scene the one line is laid in
        private const UiStyle Style = UiStyle.Small;    // a style whose box is one line of the face and no more, as most labels' are

        /// <summary>
        /// How many characters of a line TextMeshPro drew through the kit's own text; one fewer than given at the most when
        /// it cut the line, and -1 when the kit has no face to write with.
        /// </summary>
        public static int Drawn(string line)
        {
            if (string.IsNullOrEmpty(line) || !UiFonts.CanDraw)
            {
                return -1;
            }
            var root = new GameObject("RealLife Fonts Probe", typeof(RectTransform));
            try
            {
                root.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;   // TextMeshPro lays a text out only under a canvas
                var view = new UiView(root.transform, "Probe") { Motion = UiMotionSetting.Off };   // gone in this call: no arrival is begun for it
                var scene = new UiScene();
                scene.Begin("fonts probe", Width, Height, true);
                int item = UiDraw.Text(scene, UiAt.Screen, 0f, 0f, Width, Style, UiRole.Text, line, UiFlags.Truncate | UiFlags.Label);
                view.Write(scene, false);
                TMP_Text text = view.TextOf(item);
                if (text == null)
                {
                    return -1;
                }
                text.ComputeMarginSize();          // the box as it stands now, whenever the engine tells the text of it
                text.ForceMeshUpdate(true, true);
                TMP_TextInfo info = text.textInfo;
                int drawn = 0;
                for (int i = 0; info != null && i < info.characterCount && i < info.characterInfo.Length; i++)
                {
                    drawn += info.characterInfo[i].isVisible ? 1 : 0;
                }
                return text.isTextTruncated && drawn >= line.Length ? line.Length - 1 : drawn;
            }
            finally
            {
                root.SetActive(false);
                if (Application.isPlaying)
                {
                    Object.Destroy(root);   // gone before the frame is drawn
                }
                else
                {
                    Object.DestroyImmediate(root);   // the offline probe runs in the editor, where nothing is destroyed later
                }
            }
        }
    }
}
