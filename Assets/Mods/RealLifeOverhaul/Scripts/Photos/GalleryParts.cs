using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// The Gallery's colours: a light and a night look for the grid, by the game's clock, and the viewer's, always dark.
    /// Neutral greys leave the photos the only colour, with the icon's amber as the accent. No cost: read when painting.
    /// </summary>
    internal sealed class GalleryLook
    {
        public Color Back, Card, Text, Muted, Line, Chip, Placeholder, Accent, AccentText, Heart, Danger, Shade;
        public bool Dark;

        public static readonly GalleryLook Light = new GalleryLook
        {
            Back = C(248, 248, 250), Card = C(255, 255, 255), Text = C(18, 18, 22), Muted = C(112, 115, 124), Line = C(226, 227, 232),
            Chip = C(234, 235, 239), Placeholder = C(222, 224, 229), Accent = C(232, 128, 52), AccentText = C(255, 255, 255),
            Heart = C(255, 59, 92), Danger = C(214, 48, 60), Shade = new Color(0f, 0f, 0f, 0.34f)
        };

        public static readonly GalleryLook Night = new GalleryLook
        {
            Back = C(10, 10, 12), Card = C(44, 44, 48), Text = C(242, 242, 246), Muted = C(150, 152, 160), Line = C(40, 40, 44),
            Chip = C(28, 28, 31), Placeholder = C(34, 34, 38), Accent = C(255, 159, 67), AccentText = C(20, 14, 6),
            Heart = C(255, 69, 100), Danger = C(255, 88, 92), Shade = new Color(0f, 0f, 0f, 0.42f), Dark = true
        };

        public static readonly Color ViewerBack = C(6, 6, 8);
        public static readonly Color ViewerText = C(244, 244, 247);
        public static readonly Color ViewerMuted = C(166, 168, 176);
        public static readonly Color ViewerButton = new Color(1f, 1f, 1f, 0.12f);
        public static readonly Color ViewerLine = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

        public static GalleryLook Now(int hour)
        {
            return GalleryRules.Dark(hour) ? Night : Light;
        }

        private static Color C(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
    }

    /// <summary>
    /// The keys of the photo shown whole: the arrows to step, the cancel key back to the grid, taken before the game's own
    /// handling would put the phone away. On only while a photo is shown whole: three key reads a frame; off, nothing.
    /// </summary>
    [DefaultExecutionOrder(-31000)]
    internal sealed class GalleryKeys : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("photos", "GalleryKeys", typeof(GalleryKeys));

        internal GalleryApp Owner;

        private static readonly int HotUpdate = HotSpots.Slot("Photos: GalleryKeys.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (Owner == null || !Owner.ReadKeys())
            {
                enabled = false;
            }
        }
    }

    /// <summary>
    /// The mouse wheel over the photo shown whole: one photo before or after for each turn, however many notches it gives.
    /// No Update, so no cost a frame: it answers the wheel only.
    /// </summary>
    internal sealed class GalleryWheel : MonoBehaviour, IScrollHandler
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("photos", "GalleryWheel", typeof(GalleryWheel));

        private const float Rest = 0.14f;   // real seconds after a step in which further notches are let go

        internal Action<int> Step;
        private float _next;

        public void OnScroll(PointerEventData data)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                float d = data != null ? data.scrollDelta.y : 0f;
                if (Step == null || d == 0f || Time.unscaledTime < _next)
                {
                    return;
                }
                _next = Time.unscaledTime + Rest;
                Step(d > 0f ? -1 : 1);
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }
    }
}
