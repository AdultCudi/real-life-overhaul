using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Walking
{
    /// <summary>
    /// Walking on a key (F3 by default): a press on foot puts the player on the game's own walk, a press again gives
    /// the game's pace back. The game still picks its pace and the walk is put back over it each time
    /// (<see cref="WalkRules.Decide"/>); Toggle Running held brings the game's pace back while held. Ends with every load;
    /// nothing is saved or sent (Going Public shows the walk through the body's place and animator already).
    /// Cost: one key read a frame; while walking, also one engine read of the agent's speed and a time comparison.
    /// </summary>
    public sealed partial class WalkDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("walking", "WalkDirector", typeof(WalkDirector));

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static IModLogger Logger;
        public static WalkDirector Instance { get; private set; }

        /// <summary>True while walking by the key, even while the game's own pace is on for a moment (Toggle Running held, a seat).</summary>
        public static bool Walking => Instance != null && Instance._walking;

        private bool _walking;
        private ThirdPersonCharacter _body;   // the local player's, taken on the press that started the walk
        private NavMeshAgent _agent;
        private WalkFigures _figures;          // the game's figures on foot, read on that press
        private float _walk;                   // m/s: the walk put on
        private float _seen = -1f;             // m/s: the agent's speed at the last look; a different one makes a new look
        private bool _again;                   // look again at _lookAt even if the speed is the same
        private float _lookAt;                 // real time

        private void Awake()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            Stop(true, "its updates stopped");
        }

        private void OnDestroy()
        {
            Stop(true, "the mod is closing");
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Walking: WalkDirector.Update");
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
            if (Keys.Down(KeyAction.Walk))
            {
                Press();
            }
            if (_walking)
            {
                Keep();
            }
        }

        private void Press()
        {
            WalkRefusal refusal = WalkRules.CanToggle(Settings.WalkToggle, !WalkGame.Loading(), WalkGame.WindowOpen(), WalkGame.InVehicle(), WalkGame.Busy());
            if (refusal != WalkRefusal.None)
            {
                Logger?.Info("Walk: the key did nothing (" + refusal + ").");
                return;
            }
            if (!WalkRules.Toggled(_walking))
            {
                Stop(true, "the key");
                WalkGame.Notice(WalkText.Off, null);
                return;
            }
            ThirdPersonCharacter body = WalkGame.Player();
            NavMeshAgent agent = WalkGame.Agent(body);
            WalkFigures figures = WalkGame.Figures();
            float walk = WalkRules.WalkSpeed(figures);
            if (agent == null || walk <= 0f)
            {
                string why = agent == null
                    ? "the player has no navigation agent"
                    : "the game's figures leave no walk slower than its jog (walk " + figures.Walk.ToString("0.##", Inv)
                      + ", jog " + figures.Jog.ToString("0.##", Inv) + " m/s)";
                Logger?.Warn("Walk: not started: " + why + ".");
                return;
            }
            _body = body;
            _agent = agent;
            _figures = figures;
            _walk = walk;
            _walking = true;
            Look(WalkGame.Speed(agent));
            WalkGame.Notice(WalkText.On, new Dictionary<string, string>(1) { { "key", Keys.Label(KeyAction.Walk) } });
            Check();
        }

        private void Keep()
        {
            if (WalkGame.Loading() || _agent == null)
            {
                Stop(false, "a load: every save starts at the game's own pace");   // the body is going or gone: nothing to give back
                _walking = WalkRules.AfterLoad();
                return;
            }
            if (!Settings.WalkToggle)
            {
                Stop(true, "its option was switched off");
                return;
            }
            float speed = WalkGame.Speed(_agent);
            if (speed != _seen || (_again && Time.unscaledTime >= _lookAt))
            {
                Look(speed);
            }
        }

        // The speed has changed, or a lasting reason to hold off may have ended.
        private void Look(float speed)
        {
            WalkStep step = WalkRules.Decide(true, !WalkGame.InVehicle(), WalkGame.Busy(), WalkGame.RunHeld(), WalkGame.Pace(_body), speed, _figures, out bool again);
            _again = again;
            _lookAt = Time.unscaledTime + WalkRules.LookAgain;
            switch (step)
            {
                case WalkStep.Hold:
                    WalkGame.SetSpeed(_agent, _walk);
                    _seen = _walk;
                    break;
                case WalkStep.GiveBack:
                    WalkGame.GiveBack(_body);
                    _seen = WalkGame.Speed(_agent);
                    break;
                default:
                    _seen = speed;
                    break;
            }
        }

        /// <summary>Ends walking; with <paramref name="giveBack"/>, the game's own speed goes back if the walk is on the agent. Safe when not walking.</summary>
        public void Stop(bool giveBack, string why)
        {
            if (!_walking)
            {
                return;
            }
            if (giveBack && _agent != null && _body != null && WalkRules.Near(WalkGame.Speed(_agent), _walk))
            {
                WalkGame.GiveBack(_body);
            }
            _walking = false;
            _body = null;
            _agent = null;
            _seen = -1f;
            _again = false;
            Logger?.Info("Walk: back to the game's own pace (" + why + ").");
        }

        // The self-check of a press, with developer diagnostics on.
        private void Check()
        {
            if (!Meta.Dev.Verbose)
            {
                return;
            }
            float speed = WalkGame.Speed(_agent);
            WalkPace pace = WalkGame.Pace(_body);
            bool walks = WalkRules.Near(speed, _walk);
            string held = _again ? "held still, seated, in a vehicle or Toggle Running held" : "another speed is on the agent";
            string result = walks && WalkRules.IsWalk(speed, _figures)
                ? "PASS"
                : walks ? "the walk is outside the game's walk cycle" : "left at the game's own pace (" + held + ")";
            Logger?.Info("Walk check (pace): wanted " + _walk.ToString("0.00", Inv) + " m/s, the game's walk cycle at "
                         + WalkRules.AnimRate(_walk, _figures).ToString("0.00", Inv) + " a second; the agent has "
                         + speed.ToString("0.00", Inv) + " m/s over the game's " + pace + " ("
                         + WalkRules.GameSpeed(pace, _figures).ToString("0.00", Inv) + " m/s): " + result);
        }
    }
}
