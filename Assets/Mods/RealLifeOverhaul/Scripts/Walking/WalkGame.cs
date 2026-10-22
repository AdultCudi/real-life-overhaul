using System.Collections.Generic;
using BigAmbitions.InputSystem;
using Helpers;
using UI.Load;
using UI.Notification;
using UnityEngine.AI;

namespace RealLifeOverhaul.Walking
{
    /// <summary>
    /// The only place walking touches the game, one small method per read (Mono compiles a method naming a game member
    /// whole). Only <see cref="Speed"/> runs every frame, and only while walking; the rest on a press or a speed change.
    /// </summary>
    internal static class WalkGame
    {
        private const string NoticeId = "RealLifeOverhaul walking";   // one notice on screen at a time: a new press replaces it
        private const float NoticeSeconds = 3f;            // s

        /// <summary>The local player's body, never another player's; null outside a city.</summary>
        public static ThirdPersonCharacter Player()
        {
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            return gm != null && gm.playerController != null ? gm.playerController.Character : null;
        }

        public static NavMeshAgent Agent(ThirdPersonCharacter body)
        {
            return body != null ? body.navmeshAgent : null;
        }

        public static float Speed(NavMeshAgent agent)
        {
            return agent.speed;
        }

        public static void SetSpeed(NavMeshAgent agent, float speed)
        {
            agent.speed = speed;
        }

        /// <summary>The game's own speed for its pace, written by its own call.</summary>
        public static void GiveBack(ThirdPersonCharacter body)
        {
            body.SetWalkingSpeed(body.walkingSpeed);
        }

        public static WalkPace Pace(ThirdPersonCharacter body)
        {
            switch (body.walkingSpeed)
            {
                case ThirdPersonCharacter.WalkingSpeed.Zombie: return WalkPace.Zombie;
                case ThirdPersonCharacter.WalkingSpeed.Walk: return WalkPace.Walk;
                case ThirdPersonCharacter.WalkingSpeed.Jog: return WalkPace.Jog;
                case ThirdPersonCharacter.WalkingSpeed.Run: return WalkPace.Run;
                default: return WalkPace.Scooter;
            }
        }

        /// <summary>The game's figures on foot; all zero outside a city (walking then does nothing).</summary>
        public static WalkFigures Figures()
        {
            GlobalReferences g = InstanceBehavior<GlobalReferences>.Instance;
            if (g == null)
            {
                return default;
            }
            return new WalkFigures
            {
                Zombie = g.walkingSpeedZombie, Walk = g.walkingSpeedWalk, WalkFast = g.walkingSpeedWalkFast,
                Jog = g.walkingSpeedJog, Run = g.walkingSpeedRun, Scooter = g.walkingSpeedScooter,
                AnimWalk = g.animationSpeedWalk, AnimWalkFast = g.animationSpeedWalkFast, AnimJog = g.animationSpeedJog, AnimRun = g.animationSpeedRun,
            };
        }

        /// <summary>A save is loading or none is loaded: the player's body is going or not there.</summary>
        public static bool Loading()
        {
            return LoadScene.isLoading || !ModState.Loaded;
        }

        /// <summary>Typing in a box, a menu, the phone, the map, or any panel over the world.</summary>
        public static bool WindowOpen()
        {
            return ThirdPersonCamera.IsUiOpen() || GameManager.IsAnyMiniGameActive();
        }

        /// <summary>Driving, or on a two-wheeler, a hand truck or any seat of a vehicle.</summary>
        public static bool InVehicle()
        {
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            return gm == null || gm.selectedVehicle != null || PlayerHelper.IsUsingVehicle;
        }

        /// <summary>Any activity of the game's (its ToggleRunning stands aside then too), or anything holding the player still.</summary>
        public static bool Busy()
        {
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            return gm == null || gm.playerController == null || gm.playerController.NavigationDisabled || EnergyHelper.goingToHospital
                   || (uis != null && uis.playerActivityUI != null && uis.playerActivityUI.GetCurrentActivity != null);
        }

        /// <summary>The game's Toggle Running key held (Left Shift by default).</summary>
        public static bool RunHeld()
        {
            return PlayerAction.ToggleRunning.Pressing();
        }

        /// <summary>A short, quiet notice kept out of the save; one at a time.</summary>
        public static void Notice(string key, Dictionary<string, string> data)
        {
            Notifications.Show(NotificationType.Info, key, data, NoticeSeconds, NoticeId, null, notificationSound: false, trackOnSaveGame: false);
        }
    }
}
