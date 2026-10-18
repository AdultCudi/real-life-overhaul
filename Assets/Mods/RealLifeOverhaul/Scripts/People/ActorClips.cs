using System.Collections.Generic;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The clips of the mod's own (CityAssets, Bundles/reallife_anim) that the street scenes
    /// play, by the id each has in the bundle, and the figures they were made to.
    /// </summary>
    internal static class SceneClips
    {
        public const string Kneel = "kneel_work";             // right knee down, hands working 0.45 m ahead
        public const string Cpr = "cpr";                      // both knees down, compressions on a chest 0.40 m ahead
        public const string DirectTraffic = "direct_traffic";
        public const string Clipboard = "clipboard_write";    // brings its clipboard (left hand)
        public const string CuffedStand = "cuffed_stand";
        public const string CuffedWalk = "cuffed_walk";
        public const string PushCart = "push_cart";           // both hands forward on a rail
        public const string PullCart = "pull_cart";           // one hand trailing on it
        public const string LiftPatient = "lift_patient";     // plays once
        public const string CarryBag = "carry_bag_walk";
        public const string HandsUp = "hands_up";             // plays once: both hands go up...
        public const string HandsUpHold = "hands_up_hold";    // ...and are kept up beside the head
        public const string RadioTalk = "radio_talk";         // left hand at the radio on the left collar bone, where ServiceKit wears it
        public const string TakeNotes = "take_notes";         // brings its notebook (left hand)
        public const string PointDirect = "point_direct";     // plays once: points right and waves someone in front along
        public const string ArrestCuffed = "arrest_cuffed";   // plays once, from hands_up_hold into cuffed_stand: hands down behind the back
        public const string OfficerCuff = "officer_cuff";     // plays once and ENDS stepped in, a hand on the shoulder (hold it); handcuffs in the right hand
        public const string CuffedCarEnter = "cuffed_car_enter";         // plays once, from cuffed_stand: into a rear door on the car's RIGHT side
        public const string CuffedCarEnterLeft = "cuffed_car_enter_l";   // ...on its LEFT side

        /// <summary>Metres the officer stands behind whoever is cuffed, both facing one way, as arrest_cuffed and officer_cuff are made.</summary>
        public const float CuffBehind = 0.90f;
        public const float CuffSlack = 0.25f;    // metres off that place beyond which the officer's clip is not played (nobody is slid there)
        public const string CuffClick = "click"; // the mark in both clips at which the cuffs are on: from then one pair is shown, on the wrists

        /// <summary>Writing an account down: the notebook clip where the bundle has it, else the clipboard.</summary>
        public static string Notes => ClipLibrary.Has(TakeNotes) ? TakeNotes : Clipboard;

        /// <summary>Metres per second the crews and the cuffed walk at in a walking clip.</summary>
        public const float WalkPace = 1.2f;

        public const float CprReach = 0.40f;   // metres from the kneeler to the middle of the chest the compressions are made on
        public const float CprSlack = 0.12f;   // ...give or take; further off than that the hands would be working on air
    }

    /// <summary>
    /// One street character's use of the mod's clips: a stance while standing, a carriage while walking. A clip that cannot
    /// be had is refused, said once in the log, and the caller keeps its fallback. Nothing runs by itself.
    /// </summary>
    internal sealed class ActorClips
    {
        private const float Fade = 0.3f;            // seconds
        private const float MaxBuild = 0.55f;       // of the game's 0 (slim) to 1 (heavy), as the living city keeps to: see CityPeople.MaxBuild
        private const float Moving = 0.45f;         // m/s: faster than this they are walking again
        private const float Stopped = 0.2f;         // ...slower than this they may have stopped
        private const float StalledAfter = 0.3f;    // ...if it lasts this long (the game holds a walker still a frame whenever a route is asked)
        private const float SetOffGrace = 0.6f;     // seconds after being sent somewhere in which standing still is only setting off
        private const float MatchEvery = 0.2f;      // seconds between the times a walking clip's speed is matched to the ground

        private const int NotLoaded = 1, NoPlayer = 2, Build = 4, NoTool = 8;
        private static readonly Dictionary<string, int> Refused = new Dictionary<string, int>(12);
        private static readonly HashSet<string> Played = new HashSet<string>();

        private readonly ThirdPersonCharacter _character;
        private readonly HeldTool _held = new HeldTool();
        private Transform _tool;          // the one tool this character has ever been given: made once, switched off between uses
        private Renderer _toolRenderer;
        private MeshFilter _toolFilter;
        private string _toolId;
        private bool _toolOut;
        private bool _shown = true;

        private string _stance;
        private string _walk;
        private string _idle;
        private float _pace;
        private float _made;              // m/s the walking clip was made for (CityClip.Speed); 0 if it does not say
        private float _nextMatch;
        private string _leadIn;           // a clip that plays once into the stance (hands_up before hands_up_hold), while it is still playing
        private float _leadUntil;
        private bool _ordered;            // told to go somewhere, and not yet told to stand
        private bool _moving;             // ...and getting there
        private float _orderedAt;
        private float _slowSince = -1f;
        private string _playing;          // the clip last started and not stopped since
        private string _loop;             // the looping clip last started, and when its first turn began: it is taken up again in step
        private float _loopFrom;
        private bool _used;               // this character has had a clip player: End has something to hand back

        public ActorClips(ThirdPersonCharacter character)
        {
            _character = character;
        }

        /// <summary>True while a clip of ours has the body (or is fading in or out of it): the game's gestures would not show.</summary>
        public bool Busy
        {
            get
            {
                if (!_used || _character == null)
                {
                    return false;
                }
                ActivityAnimator player = ActivityAnimators.For(_character.animator);
                return player != null && player.Active;
            }
        }

        public bool Standing => _stance != null;

        /// <summary>True while a clip that plays once is leading into a stance: Sync must be called for the stance to follow it.</summary>
        public bool LeadingIn => _leadIn != null;

        /// <summary>The pace their carriage holds them to, in m/s; 0 when they walk as the game has them walk.</summary>
        public float Pace => _walk != null ? _pace : 0f;

        public string Tool => _toolOut ? _toolId : null;

        /// <summary>
        /// Plays a clip while they stand; false, with nothing changed, if it cannot be had. With <paramref name="then"/>, a
        /// one-off (hands_up) leads into that loop (hands_up_hold), taken up at the first Sync after it ends.
        /// </summary>
        public bool Stand(string id, ClipEnd end = ClipEnd.Return, string then = null)
        {
            _leadIn = null;
            if (then != null)
            {
                if (id == null || !Can(then, out _, out _) || !ClipLibrary.TryGet(id, out CityClip first) || first.Loop || !Start(id, ClipEnd.Hold))
                {
                    return false;
                }
                _leadIn = id;
                _leadUntil = Time.time + Mathf.Max(0f, first.Length - Fade);
                _stance = then;
                return true;
            }
            if (id == null || !Start(id, end))
            {
                return false;
            }
            // A clip that plays once is over by itself: only a loop is something they are still doing.
            _stance = ClipLibrary.TryGet(id, out CityClip clip) && clip.Loop ? id : null;
            return true;
        }

        public void Rest()
        {
            _leadIn = null;
            if (_stance != null)
            {
                _stance = null;
                Resolve();
            }
        }

        /// <summary>From now on they walk in one clip and optionally stand in another; false, walking as the game has them, without it.</summary>
        public bool Carry(string walk, float pace, string idle)
        {
            if (walk == null || !Can(walk, out CityClip made, out _))
            {
                Upright();
                return false;
            }
            _walk = walk;
            _pace = pace;
            _made = made.Speed;
            _idle = idle != null && Can(idle, out _, out _) ? idle : null;
            Resolve();
            return true;
        }

        public void Upright()
        {
            if (_walk == null && _idle == null)
            {
                return;
            }
            _walk = null;
            _idle = null;
            _pace = 0f;
            Resolve();
        }

        public void Going(float now)
        {
            _ordered = true;
            _moving = true;
            _orderedAt = now;
            _slowSince = -1f;
            Resolve();
        }

        public void Stood()
        {
            _ordered = false;
            _moving = false;
            Resolve();
        }

        /// <summary>From the square of their speed: held up, they leave the walking clip; under way again, they take it up in step.</summary>
        public void Sync(float speedSquared, float now)
        {
            if (_leadIn != null && now >= _leadUntil)
            {
                _leadIn = null;
                Resolve();   // the one-off is over: on into the loop it leads into
            }
            if (_walk == null || !_ordered)
            {
                return;
            }
            // The walking clip's playback follows their real speed so the planted foot stays put; at most five times a second.
            if (_moving && _made > 0f && now >= _nextMatch && _playing == _walk)
            {
                _nextMatch = now + MatchEvery;
                ActivityAnimator player = _character != null ? ActivityAnimators.For(_character.animator) : null;
                if (player != null)
                {
                    player.MatchSpeed(Mathf.Sqrt(speedSquared), _made);
                }
            }
            if (!_moving)
            {
                if (speedSquared > Moving * Moving)
                {
                    _moving = true;
                    _slowSince = -1f;
                    Resolve();
                }
                return;
            }
            if (speedSquared > Stopped * Stopped || now - _orderedAt < SetOffGrace)
            {
                _slowSince = -1f;
            }
            else if (_slowSince < 0f)
            {
                _slowSince = now;
            }
            else if (now - _slowSince >= StalledAfter)
            {
                _moving = false;
                Resolve();
            }
        }

        /// <summary>The tool out of the hand while the clip goes on (handcuffs left on somebody's wrists).</summary>
        public void Stow()
        {
            PutAway();
        }

        /// <summary>Lets go of a one-off held on its last pose (officer_cuff's hand on the shoulder); nothing while a stance is on.</summary>
        public void Release()
        {
            if (_stance == null && _leadIn == null && _playing != null)
            {
                Resolve();
            }
        }

        public void Show(bool visible)
        {
            _shown = visible;
            if (_toolOut && _toolRenderer != null)
            {
                _toolRenderer.enabled = visible;
            }
        }

        /// <summary>Everything of ours off the body now, without a fade: before a ragdoll, and for the pool.</summary>
        public void End()
        {
            _stance = null;
            _walk = null;
            _idle = null;
            _pace = 0f;
            _ordered = false;
            _moving = false;
            _playing = null;
            _loop = null;
            _leadIn = null;
            if (_used && _character != null)
            {
                ActivityAnimator player = ActivityAnimators.For(_character.animator);
                if (player != null)
                {
                    player.Dispose();
                }
            }
            PutAway();
        }

        private void Resolve()
        {
            string wanted = _ordered ? (_moving ? _walk : _idle) : _stance ?? _idle;
            if (wanted == _playing || (_leadIn != null && _playing == _leadIn && wanted == _stance))
            {
                return;   // or still leading into it
            }
            if (wanted == null || !Start(wanted, ClipEnd.Return))
            {
                Stop();
            }
        }

        private bool Start(string id, ClipEnd end)
        {
            if (!Can(id, out CityClip clip, out ActivityAnimator player))
            {
                return false;
            }
            if (!clip.HasTool)
            {
                PutAway();
            }
            else if (!Hold(clip))
            {
                Refuse(id, NoTool);
                return false;
            }
            // A loop left a moment ago (held up, a new route) is taken up where it would have been by now, not from its first frame.
            float now = Time.time;
            float from = clip.Loop && id == _loop && clip.Length > 0.01f ? Mathf.Repeat(now - _loopFrom, clip.Length) : 0f;
            if (!player.Play(clip.Clip, Fade, 1f, end, from))
            {
                PutAway();
                Refuse(id, NoPlayer);
                return false;
            }
            _used = true;
            _playing = id;
            if (clip.Loop)
            {
                _loop = id;
                _loopFrom = now - from;
            }
            if (Played.Add(id))
            {
                ActorPool.Logger?.Info("People: the '" + id + "' clip is played by a scene character (said the first time only).");
            }
            return true;
        }

        private void Stop()
        {
            if (_playing != null && _used && _character != null)
            {
                ActivityAnimator player = ActivityAnimators.For(_character.animator);
                if (player != null)
                {
                    player.Stop(Fade);
                }
            }
            _playing = null;
            PutAway();
        }

        // Whether this character can play a clip now; each reason it cannot is said once per clip.
        private bool Can(string id, out CityClip clip, out ActivityAnimator player)
        {
            player = null;
            if (!ClipLibrary.TryGet(id, out clip))
            {
                if (!ClipLibrary.Ready)
                {
                    Refuse(id, NotLoaded);   // a clip the loaded bundle lacks is reported by the library
                }
                return false;
            }
            player = _character != null ? ActivityAnimators.For(_character.animator) : null;
            if (player == null || !player.Usable || !ActivityAnimators.Working)
            {
                Refuse(id, NoPlayer);
                return false;
            }
            // A clip of ours replaces the animator layer that holds a heavy character's arms clear of the body:
            // what is held against the chest or hangs at the hip would sink into it.
            if ((clip.HasTool || id == SceneClips.CarryBag) && Heavy())
            {
                Refuse(id, Build);
                return false;
            }
            if (clip.HasTool && !(PropLibrary.TryGet(clip.Prop, out Mesh mesh, out Material material) && mesh != null && material != null))
            {
                Refuse(id, NoTool);
                return false;
            }
            return true;
        }

        private bool Heavy()
        {
            AppearanceSetter appearance = _character.appearanceSetter;
            return appearance != null && appearance.data != null && appearance.data.fatness > MaxBuild;
        }

        private static void Refuse(string id, int why)
        {
            Refused.TryGetValue(id, out int said);
            if ((said & why) != 0)
            {
                return;
            }
            Refused[id] = said | why;
            string reason = why == NotLoaded ? "the clip bundle has not loaded (yet)"
                : why == Build ? "it is made for a slim to average build and this character is heavier"
                : why == NoTool ? "the tool it holds is not in the prop bundle"
                : "the clip player is off or this character cannot be played on";
            ActorPool.Logger?.Info("People: no '" + id + "' clip for a scene character: " + reason
                                   + "; the hand-set pose or the game's own animation is used instead. (Said once per clip and reason.)");
        }

        private bool Hold(CityClip clip)
        {
            if (_tool == null || _toolId != clip.Prop)
            {
                if (!PropLibrary.TryGet(clip.Prop, out Mesh mesh, out Material material) || mesh == null || material == null)
                {
                    return false;
                }
                if (_tool != null && _toolFilter != null && _toolRenderer != null)
                {
                    // A second kind of tool for the same character: the one object is given the other shared mesh.
                    PutAway();
                    _toolFilter.sharedMesh = mesh;
                    _toolRenderer.sharedMaterial = material;
                    _toolId = clip.Prop;
                }
                else
                {
                    _toolOut = false;
                    var made = new GameObject("RealLifeOverhaul tool");
                    if (LayerHelper.HumanLayerIndex >= 0)
                    {
                        made.layer = LayerHelper.HumanLayerIndex;   // drawn as the body and its equipment are (ServiceKit)
                    }
                    _tool = made.transform;
                    _tool.SetParent(_character.transform, false);
                    Transform piece = Shapes.Piece(clip.Prop, _tool, mesh, material, Vector3.zero, Vector3.one);
                    piece.gameObject.layer = made.layer;
                    _toolRenderer = piece.GetComponent<Renderer>();
                    _toolFilter = piece.GetComponent<MeshFilter>();
                    _toolId = clip.Prop;
                }
            }
            if (_toolOut)
            {
                _held.Regrip(clip.Grip);
                return true;
            }
            if (!_held.Attach(_character.animator, _tool, clip.Grip))
            {
                _tool.gameObject.SetActive(false);
                return false;
            }
            _toolOut = true;
            _tool.gameObject.SetActive(true);
            if (_toolRenderer != null)
            {
                _toolRenderer.enabled = _shown;
            }
            return true;
        }

        // Out of the hand and switched off, kept under the character (so it goes when the character does).
        private void PutAway()
        {
            if (!_toolOut)
            {
                return;
            }
            _toolOut = false;
            _held.Detach(_character != null ? _character.transform : null);
            if (_tool != null)
            {
                _tool.gameObject.SetActive(false);
            }
        }
    }
}
