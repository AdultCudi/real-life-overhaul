using System.Collections.Generic;
using BigAmbitions.InputSystem;
using Helpers;
using UI.Load;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Walking
{
    /// <summary>
    /// The only place walking touches the game: the local player's body and its navigation agent, the game's figures on
    /// foot, and what says the player cannot walk now. Each read is a small method of its own (Mono compiles a method that
    /// names a game member as a whole). Only <see cref="Speed"/> runs every frame, and only while walking: one engine call.
    /// Everything else runs on a press of the key or when the speed has changed.
    /// </summary>
    internal static class WalkGame
    {
        private const string NoticeId = "RealLifeOverhaul walking";   // one notice of walking on screen at a time: a new press replaces it
        private const float NoticeSeconds = 3f;            // s: long enough to read four words, short enough to leave the screen clear

        /// <summary>The local player's body, or null outside a city. Another player's in a Going Public session is never this.</summary>
        public static ThirdPersonCharacter Player()
        {
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            return gm != null && gm.playerController != null ? gm.playerController.Character : null;
        }

        public static NavMeshAgent Agent(ThirdPersonCharacter body)
        {
            return body != null ? body.navmeshAgent : null;
        }

        /// <summary>The speed in the agent now: what the game's walking and clicking move the player at.</summary>
        public static float Speed(NavMeshAgent agent)
        {
            return agent.speed;
        }

        public static void SetSpeed(NavMeshAgent agent, float speed)
        {
            agent.speed = speed;
        }

        /// <summary>The game's own speed for the pace it has chosen, written by its own call as it writes it on a change.</summary>
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

        /// <summary>The game's figures on foot, as its GlobalReferences has them; all zero outside a city (walking then does nothing).</summary>
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

        /// <summary>A save is loading, or none is: the player's body is about to go, or is not there.</summary>
        public static bool Loading()
        {
            return LoadScene.isLoading || !ModState.Loaded;
        }

        /// <summary>Typing in a box, a menu, the phone, the map, or any panel of the game's or the mod's over the world.</summary>
        public static bool WindowOpen()
        {
            return ThirdPersonCamera.IsUiOpen() || GameManager.IsAnyMiniGameActive();
        }

        /// <summary>Driving, or on a two-wheeler, a hand truck or in any seat of a vehicle: walking's pace is a pace on foot.</summary>
        public static bool InVehicle()
        {
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            return gm == null || gm.selectedVehicle != null || PlayerHelper.IsUsingVehicle;
        }

        /// <summary>
        /// Sitting, working, studying, any activity of the game's (its own pace then, as its ToggleRunning stands aside
        /// too), or anything holding the player still: a seat, a desk or a panel of the mod's, a scene of the game's.
        /// </summary>
        public static bool Busy()
        {
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            return gm == null || gm.playerController == null || gm.playerController.NavigationDisabled || EnergyHelper.goingToHospital
                   || (uis != null && uis.playerActivityUI != null && uis.playerActivityUI.GetCurrentActivity != null);
        }

        /// <summary>The game's Toggle Running key held (Left Shift unless the player changed it): the game's own pace while it is.</summary>
        public static bool RunHeld()
        {
            return PlayerAction.ToggleRunning.Pressing();
        }

        /// <summary>A short notice of the game's, kept out of the save and quiet: one at a time.</summary>
        public static void Notice(string key, Dictionary<string, string> data)
        {
            Notifications.Show(NotificationType.Info, key, data, NoticeSeconds, NoticeId, null, notificationSound: false, trackOnSaveGame: false);
        }
    }
}
