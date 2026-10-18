using System.Collections.Generic;
using BigAmbitions.Characters;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The motions played with animations the game's street characters and the player already have, each the nearest the
    /// mod found (an arrest's reach, a traffic officer's wave, an argument's gesture). Whether an animator has a trigger is
    /// asked once per controller and remembered.
    /// </summary>
    public sealed class GameJobAnimations : IJobAnimations
    {
        private static readonly int AnimationSpeedId = Animator.StringToHash("AnimationSpeed");

        // The game's animation for each JobClip, and the seconds the beat is given (judged by eye in the mod's other scenes).
        private static readonly (AnimationType animation, float seconds)[] Map =
        {
            (AnimationType.DJEncouraging, 1.6f),   // Hail
            (AnimationType.PressAction, 1.2f),     // HandOver
            (AnimationType.PressAction, 1.2f),     // Receive
            (AnimationType.IDontThinkSo, 1.8f),    // Refuse
            (AnimationType.DJEncouraging, 1.4f),   // Thank
            (AnimationType.PressAction, 1.0f),     // Knock
            (AnimationType.IDontThinkSo, 1.6f),    // Annoyed
            (AnimationType.PressAction, 1.2f),     // HandOverLeft
            (AnimationType.PressAction, 1.2f),     // ReceiveBox
            (AnimationType.PressAction, 0f),       // CarEnter: nothing of the game's stands in for it
            (AnimationType.PressAction, 0f),       // CarExit
            (AnimationType.PressAction, 1.0f),     // LoadBoot
            (AnimationType.PressAction, 1.2f),     // Stock
            (AnimationType.PressAction, 1.0f),     // Serve
            (AnimationType.PressAction, 0f),       // CarEnterLeft
            (AnimationType.PressAction, 0f)        // CarExitLeft
        };

        private readonly Dictionary<long, bool> _known = new Dictionary<long, bool>(16);

        public float Play(BaseHuman who, JobClip clip)
        {
            // Any of the game's people: a full street character, or one of the plainer ones that only stand or sit.
            if (who == null || (who is ThirdPersonCharacter full && (!full.visible || RagdollSystem.Holds(full))))
            {
                return 0f;
            }
            Animator animator = who.animator;
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
            {
                return 0f;
            }
            (AnimationType animation, float seconds) = Map[(int)clip];
            if (seconds <= 0f)
            {
                return 0f;
            }
            long key = ((long)animator.runtimeAnimatorController.GetInstanceID() << 8) | (uint)clip;
            if (!_known.TryGetValue(key, out bool known))
            {
                known = HasTrigger(animator, animation.ToString());
                _known[key] = known;
            }
            if (!known)
            {
                return 0f;
            }
            animator.SetFloat(AnimationSpeedId, 1f);
            animator.SetTrigger(animation);
            return seconds;
        }

        public float Moment(JobClip clip, string mark)
        {
            return -1f;   // the game's gestures say nothing about when a hand closes
        }

        public void Stop(BaseHuman who)
        {
        }

        private static bool HasTrigger(Animator animator, string name)
        {
            AnimatorControllerParameter[] parameters = animator.parameters;   // allocates: asked once per controller and clip
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == name)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
