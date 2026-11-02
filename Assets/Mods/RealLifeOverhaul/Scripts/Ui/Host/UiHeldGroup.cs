using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A game panel's own fade group held at nothing while the kit draws the panel, for a panel nothing may be added to
    /// (Going Public copies the item panel whole, so no leaf of it may be given a group of the kit's): its alpha, and its
    /// clicks so they fall through to the kit, with what the game writes there remembered (its fade in, a HidePanel) and
    /// given back the moment the kit stops. The one thing of the game's the kit writes, in the host's library with the rest
    /// (README, "A panel nothing may be added to"). Cost: an alpha read a call.
    /// </summary>
    public sealed class UiHeldGroup
    {
        private readonly CanvasGroup _group;
        private readonly bool _blocks;
        private float _alpha;
        private bool _holding;

        public UiHeldGroup(CanvasGroup group)
        {
            _group = group;
            _blocks = group == null || group.blocksRaycasts;
            _alpha = group != null && group.alpha > 0.001f ? group.alpha : 1f;
        }

        /// <summary>False once the game's group is gone with its panel.</summary>
        public bool Alive => _group != null;

        /// <summary>The alpha the game last wrote: the kit's face fades with it.</summary>
        public float GameAlpha => _alpha;

        /// <summary>
        /// Each frame after the game's tweens: the group held at nothing while <paramref name="hold"/>, else given back if it
        /// was held; <paramref name="gameShows"/> says whether the game shows its panel now.
        /// </summary>
        public void Hold(bool hold, bool gameShows)
        {
            if (_group == null)
            {
                return;
            }
            float a = _group.alpha;
            if (a > 0.001f)
            {
                _alpha = a;   // the game wrote it this frame
            }
            if (!hold)
            {
                GiveBack(gameShows);
                return;
            }
            if (a != 0f)
            {
                _group.alpha = 0f;
            }
            if (_group.blocksRaycasts)
            {
                _group.blocksRaycasts = false;
            }
            _holding = true;
        }

        /// <summary>The group as the game last wrote it: its alpha while the game shows the panel, its clicks its own again.</summary>
        public void GiveBack(bool gameShows)
        {
            if (!_holding || _group == null)
            {
                _holding = false;
                return;
            }
            _holding = false;
            _group.alpha = gameShows ? Mathf.Clamp01(_alpha) : 0f;
            _group.blocksRaycasts = _blocks;
        }
    }
}
