using System;
using System.Collections.Generic;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A game picture the kit has to ask the game's portrait camera for (a rival's, a character's from their data): the
    /// source a screen gives <see cref="UiFaces.Source"/> for the person. It asks the game's own sprite first
    /// (<see cref="Have"/>), and otherwise has <see cref="FaceTurns"/> draw it once, never twice.
    /// </summary>
    internal abstract class FaceAsk : UiFaceSource
    {
        /// <summary>The picture drawn, kept for the session; null while it comes.</summary>
        public Sprite Made;
        public bool Queued;
        public int Tries;
        private Action<Sprite> _back;

        /// <summary>What the game already holds of them, or null: asked before the camera is.</summary>
        protected virtual Sprite Have() => null;

        /// <summary>Starts the game's portrait camera drawing them; false when it cannot now. The answer goes to <paramref name="back"/>.</summary>
        public abstract bool Start(Action<Sprite> back);

        /// <summary>The answer the camera gives for this person: the picture, or null for a failure.</summary>
        public Action<Sprite> Back => _back ??= sprite => FaceTurns.Drawn(this, sprite);

        public override Sprite Get()
        {
            if (Made != null)
            {
                return Made;
            }
            Sprite held = Have();
            if (held != null)
            {
                return held;
            }
            if (!Queued)
            {
                Queued = true;
                FaceTurns.Want(this);
            }
            return null;
        }

        /// <summary>For the pump: the game's own picture if it has made one meanwhile.</summary>
        public Sprite Held() => Have();
    }

    /// <summary>
    /// Draws the game's pictures of the people the kit shows, one at a time and spread over frames, through the portrait
    /// camera's one turn (<see cref="PortraitTurn"/>, shared with the contacts' and Flare's painters): a face is some 10
    /// to 30 ms of one frame, and the next waits <see cref="RestSeconds"/>, so a list of twenty rivals fills in over some
    /// seven seconds and never hitches. Each person is asked once a session; the game's own cache comes first. Switches
    /// itself off with nothing asked. Cost: nothing at rest; a face a frame at most, when its time comes.
    /// </summary>
    public sealed class FaceTurns : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "FaceTurns", typeof(FaceTurns));
        private static readonly int HotUpdate = HotSpots.Slot("Ui: FaceTurns.Update");

        private const float RestSeconds = 0.35f;   // real seconds between two faces, the screen being open and its player looking at it
        private const float Timeout = 15f;         // seconds to wait for the camera's read-back before asking again
        private const int TriesMost = 3;

        private static FaceTurns _instance;
        private static readonly Queue<FaceAsk> Wanted = new Queue<FaceAsk>(32);

        private FaceAsk _drawing;
        private float _since, _next;

        internal static void Want(FaceAsk ask)
        {
            Wanted.Enqueue(ask);
            if (_instance == null)
            {
                var host = new GameObject("RealLife FaceTurns");
                DontDestroyOnLoad(host);
                host.hideFlags = HideFlags.HideAndDontSave;
                _instance = host.AddComponent<FaceTurns>();
            }
            _instance.enabled = true;
        }

        /// <summary>A new city: nobody is waited for.</summary>
        internal static void Clear()
        {
            Wanted.Clear();
            if (_instance != null)
            {
                _instance._drawing = null;
            }
        }

        /// <summary>The camera answered for a person: kept, and the screens showing them laid out again.</summary>
        internal static void Drawn(FaceAsk ask, Sprite sprite)
        {
            bool mine = _instance != null && _instance._drawing == ask;
            if (mine)
            {
                _instance._drawing = null;
                _instance._next = Time.realtimeSinceStartup + RestSeconds;
                PortraitTurn.Give(_instance, Time.realtimeSinceStartup, RestSeconds);
            }
            ask.Queued = false;
            if (sprite != null)
            {
                ask.Made = sprite;
                UiFaces.Arrived();
            }
            else if (++ask.Tries < TriesMost)
            {
                ask.Queued = true;
                Wanted.Enqueue(ask);
            }
        }

        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float now = Time.realtimeSinceStartup;
            if (_drawing != null)
            {
                if (now - _since > Timeout)
                {
                    FaceAsk late = _drawing;
                    _drawing = null;
                    PortraitTurn.Give(this, now, RestSeconds);
                    late.Queued = false;
                    if (++late.Tries < TriesMost)
                    {
                        late.Queued = true;
                        Wanted.Enqueue(late);
                    }
                }
                return;
            }
            if (Wanted.Count == 0)
            {
                enabled = false;   // nothing asked: no cost until a face is wanted
                return;
            }
            if (now < _next || LoadScene.isLoading || !PortraitTurn.Free(now))
            {
                return;
            }
            FaceAsk ask = Wanted.Dequeue();
            if (ask.Made != null)
            {
                ask.Queued = false;   // drawn late by an answer that came after its wait ran out
                return;
            }
            Sprite held = ask.Held();   // the game made it meanwhile (it draws the rival it chooses itself)
            if (held != null)
            {
                ask.Queued = false;
                ask.Made = held;
                UiFaces.Arrived();
                return;
            }
            if (!PortraitTurn.Ready || !PortraitTurn.Take(this, now))
            {
                Wanted.Enqueue(ask);   // the camera is not there yet (a load), or another painter has it
                _next = now + RestSeconds;
                return;
            }
            _drawing = ask;
            _since = now;
            if (!ask.Start(ask.Back))
            {
                _drawing = null;
                PortraitTurn.Give(this, now, 0f);
                ask.Queued = false;   // nothing to draw them from: a few tries, then the screen's next look at them asks again
                if (++ask.Tries < TriesMost)
                {
                    ask.Queued = true;
                    Wanted.Enqueue(ask);
                }
                _next = now + RestSeconds;
            }
        }
    }
}
