using System;
using System.Collections;
using Streets.Pedestrians;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Street
{
    /// <summary>
    /// Somebody held for a conversation: a walker paused, somebody standing turned on the spot (the game's standing figures
    /// have no turn), somebody seated left as posed; all given back exactly as found.
    /// COST: <see cref="Keep"/> is a few field reads on the conversation's tick; a turn is a coroutine of a few frames.
    /// </summary>
    public sealed class StreetHold
    {
        private const string SpeakClip = "talk_a";   // talking with the hands, to somebody in front
        private const float SpeakFade = 0.3f;        // seconds the hands take to begin and to come to rest

        private ActivityAnimator _speaking;          // the clip player of a figure talking with its hands, until Quiet or Release
        private bool _spoke;                         // its hands moved during this hold: its clip player is let go at Release
        private BaseHuman _body;             // who is held
        private Pedestrian _p;               // their walker, or null
        private StreetFigure _kind;
        private bool _ped, _walk, _stay;     // which of the walker's behaviours were on
        private bool _agent;                 // its agent was on the navigation mesh and is stopped by us
        private bool _stopped, _turning;     // the agent's own stop and turning, as found
        private Vector3 _look;               // the figure's look target, as found
        private bool _turned;                // a standing figure was turned by us
        private Quaternion _stood;           // ...and the way it stood
        private Coroutine _turn;             // the turn towards the player under way
        private MonoBehaviour _host;         // what runs the turns
        private BaseHuman _back;             // a standing figure turning back after its talk, the way it stood, and that turn
        private Quaternion _backTo;
        private Coroutine _backTurn;

        public bool Held => _body != null;

        public BaseHuman Body => _body;

        public StreetFigure Kind => _kind;

        /// <summary>A walker's character, for the dialogue panel; null for a standing or sitting figure.</summary>
        public ThirdPersonCharacter Character => _p != null ? _p.tpc : null;

        /// <summary>True if the game took them while held: put away, hit, or held by the ragdolls.</summary>
        public bool Gone => (object)_body != null && (_body == null || !_body.gameObject.activeInHierarchy || RagdollSystem.Holds(_body)
                                                     || ((object)_p != null && (_p == null || _p.tpc == null)));

        /// <summary>Why the last <see cref="Speak"/> showed nothing, for the check line.</summary>
        public string Why { get; private set; }

        /// <summary>They begin to talk with their hands, about to answer; false, nothing changed, for somebody seated or when the clip cannot play.</summary>
        public bool Speak()
        {
            Why = null;
            if (_body == null || _speaking != null)
            {
                Why = _body == null ? "nobody is held" : "already talking with their hands";
                return false;
            }
            if (_kind == StreetFigure.Seated)
            {
                Why = "seated, posed as the game sat them";
                return false;
            }
            ActivityAnimator player = ActivityAnimators.Working && _body.animator != null ? ActivityAnimators.For(_body.animator) : null;
            if (player == null || !player.Usable || !ClipLibrary.TryGet(SpeakClip, out CityClip clip))
            {
                Why = "the clip '" + SpeakClip + "' cannot play on them";
                return false;
            }
            BodyMask mask = clip.Mask == BodyMask.Full ? BodyMask.Upper : clip.Mask;
            if (!player.PlayOver(clip.Clip, mask, SpeakFade, 1f, ClipEnd.Return))
            {
                Why = "the clip '" + SpeakClip + "' did not start";
                return false;
            }
            _speaking = player;
            _spoke = true;
            ClipLibrary.Played(SpeakClip, "a stranger about to answer in the street");
            return true;
        }

        /// <summary>The hands come to rest: the game's own animation again.</summary>
        public void Quiet()
        {
            ActivityAnimator player = _speaking;
            _speaking = null;
            if (player != null && player.Animator != null)
            {
                player.StopOver(SpeakFade);
            }
        }

        // Let go: the game's own animation at once, as the game's pool may reuse the figure.
        private void Unspeak(BaseHuman body)
        {
            bool spoke = _spoke;
            _speaking = null;
            _spoke = false;
            if (spoke && body != null && body.animator != null)
            {
                ActivityAnimators.Forget(body.animator);
            }
        }

        public bool Holds(BaseHuman figure) => _body != null && ReferenceEquals(_body, figure);

        /// <summary>Stops somebody where they are and turns them to the player as their kind allows; false if they cannot be held.</summary>
        public bool Take(BaseHuman body, Pedestrian p, StreetFigure kind, Vector3 player, MonoBehaviour host)
        {
            if (_body != null || body == null || !StreetOffer.Offered(kind) || (p != null ? !StreetGame.Usable(p) : !StreetGame.Usable(body)))
            {
                return false;
            }
            HornReactions.LetGo(body);   // a horn's look round put back first
            _body = body;
            _p = kind == StreetFigure.Walker ? p : null;
            _kind = _p != null ? StreetFigure.Walker : kind == StreetFigure.Walker ? StreetFigure.Seated : kind;
            _host = host;
            _turned = false;
            _agent = false;
            if (_p != null)
            {
                HornReactions.LetGo(_p);
                TakeWalker(_p);
            }
            else if (StreetOffer.TurnsBody(_kind))
            {
                _stood = body.transform.rotation;
                if (ReferenceEquals(_back, body))
                {
                    _stood = _backTo;   // still turning back from an earlier talk: the way it stood then
                    StopBack(false);
                }
                _turned = true;
            }
            Face(player);
            return true;
        }

        private void TakeWalker(Pedestrian p)
        {
            _ped = p.enabled;
            _walk = p.pedestrianWalkingAiBehavior != null && p.pedestrianWalkingAiBehavior.enabled;
            _stay = p.pedestrianStationaryBehavior != null && p.pedestrianStationaryBehavior.enabled;
            p.enabled = false;
            if (p.pedestrianWalkingAiBehavior != null)
            {
                p.pedestrianWalkingAiBehavior.enabled = false;
            }
            if (p.pedestrianStationaryBehavior != null)
            {
                p.pedestrianStationaryBehavior.enabled = false;
            }
            NavMeshAgent agent = p.tpc.navmeshAgent;
            _agent = agent != null && agent.enabled && agent.isOnNavMesh;
            if (_agent)
            {
                _stopped = agent.isStopped;
                _turning = agent.updateRotation;
                agent.isStopped = true;
                agent.updateRotation = false;
            }
            _look = p.tpc.LookTarget;
        }

        public void Face(Vector3 point)
        {
            if (_p != null)
            {
                ThirdPersonCharacter c = _p.tpc;
                if (c == null || !c.isActiveAndEnabled)
                {
                    return;
                }
                Vector3 to = point - c.transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.04f)
                {
                    c.StartCoroutine(Guard.Routine(c.RotateTowards(Quaternion.LookRotation(to), StreetRules.TurnSeconds)));
                }
                if (!_agent)
                {
                    c.LookTarget = new Vector3(point.x, c.transform.position.y, point.z);   // off the mesh the game turns a figure to its look target
                }
                return;
            }
            if (!_turned || _body == null)
            {
                return;   // seated: stays as posed
            }
            Vector3 flat = point - _body.transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.04f)
            {
                StopTurn();
                Turn(_body, Quaternion.LookRotation(flat, Vector3.up));
            }
        }

        /// <summary>On the conversation's tick: undoes a game rule that let the agent go again, such as a crossing's green light.</summary>
        public void Keep()
        {
            if (_p == null || !_agent || _p.tpc == null)
            {
                return;
            }
            NavMeshAgent agent = _p.tpc.navmeshAgent;
            if (agent != null && agent.enabled && agent.isOnNavMesh && !agent.isStopped)
            {
                agent.isStopped = true;
            }
        }

        /// <summary>Gives everything back as found. Safe at any time, also after the game took them.</summary>
        public void Release()
        {
            BaseHuman body = _body;
            Pedestrian p = _p;
            bool turned = _turned;
            Unspeak(body);
            _body = null;
            _p = null;
            _turned = false;
            StopTurn();
            if (p != null)
            {
                ReleaseWalker(p);
                return;
            }
            if (!turned || body == null)
            {
                return;
            }
            try
            {
                StopBack(true);   // another figure still turning back: set as it stood at once
                if (body.gameObject.activeInHierarchy && _host != null && _host.isActiveAndEnabled)
                {
                    _back = body;
                    _backTo = _stood;
                    _backTurn = _host.StartCoroutine(Guard.Routine(Turning(body.transform, _stood)));
                }
                else
                {
                    body.transform.rotation = _stood;   // unloading, or put away by the pool: at once
                }
            }
            catch (Exception e)
            {
                StreetGame.Logger?.Error(e);
            }
        }

        private void ReleaseWalker(Pedestrian p)
        {
            if (p == null || p.tpc == null)
            {
                return;
            }
            try
            {
                p.tpc.LookTarget = _look;
                NavMeshAgent agent = p.tpc.navmeshAgent;
                if (_agent && agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = _stopped;
                    agent.updateRotation = _turning;
                }
                if (p.pedestrianWalkingAiBehavior != null)
                {
                    p.pedestrianWalkingAiBehavior.enabled = _walk;
                }
                if (p.pedestrianStationaryBehavior != null)
                {
                    p.pedestrianStationaryBehavior.enabled = _stay;
                }
                p.enabled = _ped;
            }
            catch (Exception e)
            {
                StreetGame.Logger?.Error(e);
            }
        }

        private void Turn(BaseHuman body, Quaternion to)
        {
            if (_host != null && _host.isActiveAndEnabled)
            {
                _turn = _host.StartCoroutine(Guard.Routine(Turning(body.transform, to)));
            }
            else
            {
                body.transform.rotation = to;
            }
        }

        private void StopTurn()
        {
            if (_turn != null && _host != null)
            {
                _host.StopCoroutine(_turn);
            }
            _turn = null;
        }

        // Stops a turn back under way; with finish the figure is set as it stood at once, else whoever takes it next keeps it.
        private void StopBack(bool finish)
        {
            if (_backTurn != null && _host != null)
            {
                _host.StopCoroutine(_backTurn);
            }
            if (finish && _back != null && _back.gameObject.activeInHierarchy)
            {
                _back.transform.rotation = _backTo;
            }
            _backTurn = null;
            _back = null;
        }

        // A standing figure turned on the spot; the last frame sets the rotation exactly.
        private static IEnumerator Turning(Transform body, Quaternion to)
        {
            float rate = 180f / Mathf.Max(0.1f, StreetRules.TurnSeconds);   // degrees a second: half a turn in the turn's time
            while (body != null && body.gameObject.activeInHierarchy && Quaternion.Angle(body.rotation, to) > 0.5f)
            {
                body.rotation = Quaternion.RotateTowards(body.rotation, to, rate * Time.deltaTime);
                yield return null;
            }
            if (body != null && body.gameObject.activeInHierarchy)
            {
                body.rotation = to;
            }
        }
    }
}
