using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The arrival: the screen rising into place in the arrival time, the rows of an arriving list each a stagger after
    /// the one above, figures counting up to their value and segments filling in turn; and the beat of a loading state's
    /// dots. Game timing from the tokens, the Motion setting obeyed (Calm slower and shorter, Off none). The tweens run
    /// in the one pooled runner (UiTweens).
    /// </summary>
    public sealed partial class UiView
    {
        private CanvasGroup _rootGroup;

        /// <summary>Plays the screen's arrival: the whole rising, then its lists' rows in turn.</summary>
        public void Arrive()
        {
            UiTweens tweens = UiTweens.Instance;
            if (tweens == null || Motion == UiMotionSetting.Off || _scene == null)
            {
                return;
            }
            float now = Time.unscaledTime;
            float enter = UiTokens.Duration(UiMotionKind.Enter, Motion);
            float rise = UiTokens.Distance(Motion) * Scale;
            for (int i = 0; i < _scene.ItemCount; i++)
            {
                ref UiItem it = ref _scene.Items[i];
                Slot slot = SlotOf(i);
                if (slot == null)
                {
                    continue;
                }
                if (it.Order >= 0)
                {
                    tweens.Arrive(slot, now + UiMotionRules.Delay(it.Order, Motion), enter, rise);
                }
                if ((it.Flags & UiFlags.CountUp) != 0 && slot is TextSlot text)
                {
                    tweens.Count(text.Text, it.Text, now, UiTokens.Duration(UiMotionKind.Fill, Motion));
                }
            }
        }

        /// <summary>Lays the screen in, rising a short way as a game screen opens; at once with motion off.</summary>
        public void Open()
        {
            if (_rootGroup == null)
            {
                _rootGroup = _root.gameObject.AddComponent<CanvasGroup>();
            }
            UiTweens tweens = UiTweens.Instance;
            if (tweens == null || Motion == UiMotionSetting.Off)
            {
                _rootGroup.alpha = 1f;
                return;
            }
            tweens.Open(_rootGroup, _root, Time.unscaledTime, UiTokens.Duration(UiMotionKind.Enter, Motion), UiTokens.Distance(Motion) * Scale);
        }

        // A loading state's three dots beat in turn while it shows.
        private void Beat(Image[] dots)
        {
            UiTweens tweens = UiTweens.Instance;
            if (tweens == null || Motion == UiMotionSetting.Off)
            {
                return;
            }
            for (int i = 0; i < dots.Length; i++)
            {
                tweens.Beat(dots[i], i * 0.15f);
            }
        }

        /// <summary>Stops every tween on this view's objects (the screen closing).</summary>
        public void Still()
        {
            UiTweens.Instance?.StopAll(_root);
            if (_rootGroup != null)
            {
                _rootGroup.alpha = 1f;
            }
        }
    }
}
