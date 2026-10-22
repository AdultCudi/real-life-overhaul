using BigAmbitions.Characters;
using Extensions;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>A city person's clips and what is in their hands, worn or brought along.</summary>
    public sealed partial class CityPerson
    {
        /// <summary>Plays a clip with its tool in hand at its grip, all or nothing; tool false keeps the hands as they are, startAt is seconds in.</summary>
        public bool Work(string clipName, CityProps props, float fade = 0.35f, ClipEnd end = ClipEnd.Return, bool tool = true, float startAt = 0f)
        {
            if (Lost || _player == null || !_player.Usable || !ClipLibrary.TryGet(clipName, out CityClip clip))
            {
                return false;
            }
            if (tool && clip.HasTool && !Hold(clip, props))
            {
                return false;
            }
            Rise();   // a whole-body clip is made for somebody on their feet
            ClearPose();
            if (!_player.Play(clip.Clip, fade, 1f, end, startAt))
            {
                return false;
            }
            Playing = clipName;
            ClipLibrary.Played(clipName, "a street character");
            return true;
        }

        /// <summary>The same on the masked layer, over the game's walk or idle or a Work clip: carrying a box or an umbrella while walking.</summary>
        public bool WorkOver(string clipName, CityProps props, float fade = 0.3f, ClipEnd end = ClipEnd.Return, BodyMask mask = BodyMask.Full, bool tool = true)
        {
            if (Lost || _player == null || !_player.Usable || !ClipLibrary.TryGet(clipName, out CityClip clip))
            {
                return false;
            }
            if (mask == BodyMask.Full)
            {
                mask = clip.Mask == BodyMask.Full ? BodyMask.Upper : clip.Mask;
            }
            if (tool && clip.HasTool && !Hold(clip, props))
            {
                return false;
            }
            if (!_player.PlayOver(clip.Clip, mask, fade, 1f, end))
            {
                return false;
            }
            PlayingOver = clipName;
            ClipLibrary.Played(clipName, "a street character");
            return true;
        }

        /// <summary>The masked layer fades out; what the hands hold stays until put away or released.</summary>
        public void StopOver(float fade = 0.3f)
        {
            _player?.StopOver(fade);
            PlayingOver = null;
            if (Playing == null)
            {
                for (int hand = 0; hand < Hands; hand++)
                {
                    _held[hand].LetGo();   // nothing playing holds anything with both hands
                }
            }
        }

        /// <summary>Puts a prop already out (a cup on a counter) in the hand at the named clip's grip; false leaves it where it is.</summary>
        public bool Take(Transform tool, string clipName, CityProps props)
        {
            if (Lost || tool == null || !ClipLibrary.TryGet(clipName, out CityClip clip) || !clip.HasTool || clip.Prop != tool.name)
            {
                return false;
            }
            int hand = clip.Grip.Hand == HumanBodyBones.RightHand ? 0 : 1;
            Transform before = Release(hand == 0);
            if (before != null && before != tool && props != null)
            {
                props.Return(before);
            }
            if (!_held[hand].Attach(Character.animator, tool, clip.Grip))
            {
                return false;
            }
            _tools[hand] = tool;
            Show(tool, _toolsShown);
            return true;
        }

        /// <summary>Something along with them but not in a hand, two at most: on the ground in front (bone null) or worn on a bone.</summary>
        public bool Bring(Transform prop, HumanBodyBones? bone, Vector3 position, Quaternion rotation)
        {
            if (Lost || prop == null)
            {
                return false;
            }
            Transform parent = bone.HasValue ? Character.animator.GetBoneTransform(bone.Value) : Character.transform;
            int slot = _with[0] == null ? 0 : _with[1] == null ? 1 : -1;
            if (parent == null || slot < 0)
            {
                return false;
            }
            Vector3 scale = parent.lossyScale;
            if (Mathf.Abs(scale.x) < 0.001f || Mathf.Abs(scale.y) < 0.001f || Mathf.Abs(scale.z) < 0.001f)
            {
                return false;
            }
            prop.SetParent(parent, false);
            prop.localPosition = new Vector3(position.x / scale.x, position.y / scale.y, position.z / scale.z);
            prop.localRotation = rotation;
            prop.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
            _with[slot] = prop;
            Show(prop, _toolsShown);
            return true;
        }

        /// <summary>Lets go of something brought along, where it is now; null if they have no such thing.</summary>
        public Transform Leave(string id)
        {
            for (int i = 0; i < _with.Length; i++)
            {
                Transform prop = _with[i];
                if (prop != null && prop.name == id)
                {
                    _with[i] = null;
                    prop.SetParent(null, true);
                    prop.localScale = Vector3.one;
                    Show(prop, true);
                    return prop;
                }
            }
            return null;
        }

        /// <summary>Puts a clip's tool in the hand without playing the clip, for a second tool a later clip assumes.</summary>
        public bool Carry(string clipName, CityProps props)
        {
            if (Lost || !ClipLibrary.TryGet(clipName, out CityClip clip) || !clip.HasTool || !Hold(clip, props))
            {
                return false;
            }
            _held[clip.Grip.Hand == HumanBodyBones.RightHand ? 0 : 1].LetGo();   // carried in the one hand until its own clip plays
            return true;
        }

        /// <summary>Back to the game's own animation, its idle or walking.</summary>
        public void Idle(float fade = 0.35f)
        {
            _player?.Stop(fade);
            Playing = null;
            if (PlayingOver != null)
            {
                return;   // the masked layer still holds what it holds, with both hands if so
            }
            for (int hand = 0; hand < Hands; hand++)
            {
                _held[hand].LetGo();   // whatever they still hold is carried in one hand
            }
        }

        /// <summary>Sets the walking clip's speed from theirs, so the planted foot stays put; from the owner's tick, not per frame.</summary>
        public void KeepStep(float clipSpeed)
        {
            if (_player != null && Playing != null && !Lost)
            {
                _player.MatchSpeed(Speed, clipSpeed);
            }
        }

        /// <summary>Holds one of the game's looping poses (on the phone, texting), with an optional game prop in the hand.</summary>
        public bool Pose(PermanentAnimationType pose, string handObject = null)
        {
            if (Lost || !Character.animator.HasParameter(pose.ToString()))
            {
                return false;
            }
            Idle();
            ClearPose();
            Character.animator.SetBool(pose);
            if (!string.IsNullOrEmpty(handObject))
            {
                Character.AddHandObject(handObject);
            }
            _pose = pose;
            _hasPose = true;
            return true;
        }

        public void ClearPose()
        {
            if (!_hasPose)
            {
                return;
            }
            _hasPose = false;
            Character.animator.SetBool(_pose, false);
            Character.RemoveHandObject();
        }

        /// <summary>Takes the tool out of a hand and gives it to the caller; null if that hand is empty.</summary>
        public Transform Release(bool rightHand)
        {
            int hand = rightHand ? 0 : 1;
            Transform tool = _tools[hand];
            if (tool != null)
            {
                _held[hand].Detach(null);
                Show(tool, true);
                _tools[hand] = null;
            }
            return tool;
        }

        /// <summary>Everything out of their hands and back on the shelf.</summary>
        public void PutAway(CityProps props)
        {
            for (int hand = 0; hand < Hands; hand++)
            {
                Transform tool = Release(hand == 0);
                if (tool != null && props != null)
                {
                    props.Return(tool);
                }
            }
            for (int i = 0; i < _with.Length; i++)
            {
                Transform prop = _with[i];
                _with[i] = null;
                if (prop != null)
                {
                    prop.SetParent(null, true);
                    props?.Return(prop);
                }
            }
        }

        private bool Hold(CityClip clip, CityProps props)
        {
            int hand = clip.Grip.Hand == HumanBodyBones.RightHand ? 0 : 1;
            if (_tools[hand] != null && _tools[hand].name == clip.Prop)
            {
                _held[hand].Regrip(clip.Grip);   // already in hand: every clip for this tool shares its grip, the other hand may change
                return true;
            }
            if (props == null)
            {
                return false;
            }
            Transform tool = props.Take(clip.Prop);
            if (tool == null)
            {
                return false;
            }
            props.Return(Release(hand == 0));
            if (!_held[hand].Attach(Character.animator, tool, clip.Grip))
            {
                props.Return(tool);
                return false;
            }
            _tools[hand] = tool;
            Show(tool, _toolsShown);
            return true;
        }

        private static void Show(Transform tool, bool visible)
        {
            if (tool != null && tool.TryGetComponent(out Renderer renderer))
            {
                renderer.enabled = visible;
            }
        }
    }
}
