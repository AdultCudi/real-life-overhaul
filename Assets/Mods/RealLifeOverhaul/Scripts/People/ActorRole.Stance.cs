using BigAmbitions.Characters;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>What the person wears while standing: poses, the mod's clips, repeated gestures and a walking clip.</summary>
    public sealed partial class ActorRole
    {
        /// <summary>A looping pose (with an optional prop in the hand) worn whenever the person is standing.</summary>
        public void Hold(PermanentAnimationType pose, string prop = null)
        {
            if (!Present)
            {
                return;
            }
            Drop();
            _hasPose = true;
            _kneels = false;
            _stance = null;
            _fallback = true;
            _pose = pose;
            _prop = prop;
            if (_order == Order.Standing)
            {
                Wear();
            }
        }

        /// <summary>One of the mod's clips worn whenever they stand (directing traffic, say); they simply stand if it cannot be had.</summary>
        public void Work(string clip)
        {
            if (!Present)
            {
                return;
            }
            Drop();
            _hasPose = true;
            _kneels = false;
            _stance = clip;
            _fallback = false;
            if (_order == Order.Standing)
            {
                Wear();
            }
        }

        /// <summary>The same, with the pose of the game's (and its prop) that stood in for it as the fallback.</summary>
        public void Work(string clip, PermanentAnimationType otherwise, string prop = null)
        {
            if (!Present)
            {
                return;
            }
            Drop();
            _hasPose = true;
            _kneels = false;
            _stance = clip;
            _fallback = true;
            _pose = otherwise;
            _prop = prop;
            if (_order == Order.Standing)
            {
                Wear();
            }
        }

        /// <summary>
        /// Down beside someone on the ground whenever they stand: in a kneeling clip, else on one knee in the hand-set pose.
        /// With <paramref name="reach"/> (CPR) the clip is worn only if what they face is that far in front once they get
        /// there; otherwise they kneel at work beside it.
        /// </summary>
        public void Kneel(string clip = SceneClips.Kneel, float reach = 0f)
        {
            if (!Present)
            {
                return;
            }
            Drop();
            _hasPose = true;
            _kneels = true;
            _stance = clip;
            _reach = reach;
            if (_order == Order.Standing)
            {
                Wear();
            }
        }

        /// <summary>Hands up whenever they stand, until Quiet: the two clips made for it, or the gesture that stands in.</summary>
        public void Surrender()
        {
            if (!Present)
            {
                return;
            }
            if (!_surrenders || !_hasPose)
            {
                Drop();
                _hasPose = true;
                _surrenders = true;
                _kneels = false;
                _stance = SceneClips.HandsUp;
                _fallback = false;
            }
            Repeat(SurrenderGesture, 2.4f, 3.2f);
            if (_order == Order.Standing && !_posed)
            {
                Wear();
            }
        }

        /// <summary>True while their hands are up in the clips made for it (not in the gesture that stands in for them).</summary>
        public bool HandsRaised => _surrenders && _posed && _clipWorn && Present;

        private const AnimationType SurrenderGesture = AnimationType.VictoryStanding;

        public bool Act(string clip)
        {
            return Present && !_actor.Limp && _actor.Work(clip);
        }

        /// <summary>The same with how it ends: ClipEnd.Hold keeps the last pose until EndAct, the next move, or Exit.</summary>
        public bool Act(string clip, ClipEnd end)
        {
            return Present && !_actor.Limp && _actor.Work(clip, end);
        }

        public void EndAct()
        {
            if (Present && !_actor.Limp)
            {
                _actor.ReleaseClip();
            }
        }

        public void StowTool()
        {
            if (Present)
            {
                _actor.StowTool();
            }
        }

        /// <summary>Changes their stance through a one-off into a loop (arrest_cuffed, then cuffed_stand); false if it cannot.</summary>
        public bool LeadInto(string once, string then)
        {
            if (!Present || _order != Order.Standing || _actor.Limp || !_actor.Work(once, ClipEnd.Hold, then))
            {
                return false;
            }
            _repeats = false;
            _surrenders = false;
            _kneels = false;
            _fallback = false;
            _reach = 0f;
            _stance = then;
            _hasPose = true;
            _posed = true;
            _clipWorn = true;
            return true;
        }

        /// <summary>How they carry themselves from now on (StreetActor.Carriage); false, walking as before, if the walking clip cannot be had.</summary>
        public bool Gait(string walk, float pace = SceneClips.WalkPace, string idle = null)
        {
            return Present && _actor.Carriage(walk, pace, idle);
        }

        public void Drop()
        {
            _hasPose = false;
            _surrenders = false;
            _clipWorn = false;
            _reach = 0f;
            if (_posed && Present)
            {
                _actor.ClearPose();
            }
            _posed = false;
        }

        public void Repeat(AnimationType gesture, float minGap, float maxGap)
        {
            if (!Present)
            {
                return;
            }
            _repeats = true;
            _hasEmoji = false;
            _repeatClip = null;
            _gesture = gesture;
            _gestureMin = minGap;
            _gestureMax = maxGap;
            _nextGesture = Time.time + Random.Range(0.3f, maxGap);
        }

        public void Repeat(AnimationType preferred, AnimationType otherwise, float minGap, float maxGap)
        {
            if (Present)
            {
                Repeat(_actor.Knows(preferred) ? preferred : otherwise, minGap, maxGap);
            }
        }

        /// <summary>While standing, plays a one-off clip of the mod's every so often, or the first of two gestures when it cannot be had.</summary>
        public void Repeat(string clip, AnimationType preferred, AnimationType otherwise, float minGap, float maxGap)
        {
            Repeat(preferred, otherwise, minGap, maxGap);
            _repeatClip = _repeats ? clip : null;
        }

        public void Repeat(AnimationType gesture, float minGap, float maxGap, CharacterEmojiName emoji, float emojiChance)
        {
            Repeat(gesture, minGap, maxGap);
            _hasEmoji = _repeats;
            _emoji = emoji;
            _emojiChance = emojiChance;
        }

        /// <summary>Stops the repeated gesture, and puts the hands of someone who had them up down again.</summary>
        public void Quiet()
        {
            _repeats = false;
            if (_surrenders)
            {
                Drop();
            }
        }

        public void Emote(CharacterEmojiName emoji, float seconds = 2.5f)
        {
            ActorPool pool = ActorPool.Instance;
            if (Present && pool != null)
            {
                pool.ShowEmoji(_actor, emoji, seconds);
            }
        }

        private void Wear()
        {
            if (_kneels)
            {
                _actor.Kneel(_reach > 0f && !InReach() ? SceneClips.Kneel : _stance);
            }
            else if (_stance == null || !(_clipWorn = _actor.Work(_stance, ClipEnd.Return, _surrenders ? SceneClips.HandsUpHold : null)))
            {
                if (_fallback)
                {
                    _actor.Pose(_pose, _prop);
                }
            }
            _posed = true;
        }

        // Whether what they face is as far in front of them as the clip's hands will work, said in the log each time it is asked.
        private bool InReach()
        {
            Vector3 gap = _face - _actor.Position;
            gap.y = 0f;
            float off = _hasFace ? gap.magnitude : 0f;
            bool reached = _hasFace && Mathf.Abs(off - _reach) <= SceneClips.CprSlack;
            ActorPool.Logger?.Info(System.FormattableString.Invariant($"People: kneeling {off:F2} m from the chest of whoever is down; '{_stance}' is made for {_reach:F2} m: ")
                                   + (reached ? "played." : "not played, they kneel at work beside them instead."));
            return reached;
        }
    }
}
