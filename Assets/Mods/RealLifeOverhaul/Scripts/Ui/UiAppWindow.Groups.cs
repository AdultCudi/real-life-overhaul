using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiAppWindow
    {
        /// <summary>A group's label and the step under it, on the grid. Nothing is added when no entry names a group.</summary>
        public const float GroupLabel = UiTokens.S6 + UiTokens.S1;

        [ThreadStatic] private static float[] _groupBox;
        [ThreadStatic] private static int _groupCount;
        [ThreadStatic] private static float _groupContent;

        /// <summary>True when any entry names a group, so the sidebar draws labels. False leaves every entry where it stood.</summary>
        public static bool Grouped(UiNavEntry[] nav)
        {
            if (nav == null)
            {
                return false;
            }
            for (int i = 0; i < nav.Length; i++)
            {
                if (!string.IsNullOrEmpty(nav[i].Group))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True when this entry is the first of its group.</summary>
        public static bool StartsGroup(UiNavEntry[] nav, int index)
        {
            if (nav == null || index < 0 || index >= nav.Length || string.IsNullOrEmpty(nav[index].Group))
            {
                return false;
            }
            return index == 0 || nav[index].Group != nav[index - 1].Group;
        }

        /// <summary>Measures the sidebar once for a layout. A sidebar with no groups is not measured: its rows stay on the fixed pitch.</summary>
        public static void MeasureGroups(UiNavEntry[] nav)
        {
            int n = nav == null ? 0 : nav.Length;
            if (_groupBox == null || _groupBox.Length < n)
            {
                _groupBox = new float[Math.Max(16, n)];
            }
            float y = 0f;
            string previous = null;
            float row = NavRow + UiTokens.S1;
            for (int i = 0; i < n; i++)
            {
                if (!string.IsNullOrEmpty(nav[i].Group) && nav[i].Group != previous)
                {
                    y += GroupLabel;
                    previous = nav[i].Group;
                }
                _groupBox[i] = y;
                y += row;
            }
            _groupContent = y;
            _groupCount = n;
        }

        /// <summary>The height of a measured sidebar.</summary>
        public static float GroupContent => _groupContent;

        /// <summary>Where a measured entry's row starts.</summary>
        public static float GroupBox(int index) => _groupBox != null && index >= 0 && index < _groupCount ? _groupBox[index] : 0f;

        /// <summary>Where to scroll so an entry and its group label are in view. Call <see cref="MeasureGroups"/> first.</summary>
        public static float GroupBlock(UiNavEntry[] nav, int index)
        {
            float box = GroupBox(index);
            return StartsGroup(nav, index) ? box - GroupLabel : box;
        }

        /// <summary>A group's name in the sidebar's capitals, above the first entry of that group. Not a target.</summary>
        public static void GroupCaption(UiScene s, UiAt at, float x, float y, float w, string label)
        {
            UiDraw.TextMiddle(s, at, x + UiTokens.S3, y, UiTokens.S6, w - UiTokens.S3 * 2f, UiStyle.SectionHeader, UiRole.Muted, label,
                UiFlags.Label | UiFlags.Truncate);
        }
    }
}
