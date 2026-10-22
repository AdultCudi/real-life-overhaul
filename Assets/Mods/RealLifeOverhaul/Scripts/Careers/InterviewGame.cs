using System;
using System.Collections;
using System.Collections.Generic;
using BigAmbitions.SoundSystem;
using Buildings;
using Buildings.Outdoors;
using Helpers;
using Streets;
using UI;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>An employer's entrance door as an interview uses it: the step outside it the player walks to and comes back out on, facing the street.</summary>
    internal sealed class InterviewDoor
    {
        public CityBuildingController Building;
        public Vector3 Step;          // where the game itself puts a player coming out of this door (BuildingManager.MovePlayerToSafeExit)
        public Quaternion Facing;     // the door's own heading, outwards: the way a player coming out of it faces
        public Vector3 Outward;       // ...as a level direction
    }

    /// <summary>
    /// What an interview reads and changes of the game: the employer's entrance door, the step outside it, the door's sounds and
    /// the game's black screen at a door (UI.UiFader, its overlay UIs.blackOverlay). Nothing per frame: each is asked once an
    /// interview, on the press, the arrival or the end.
    /// </summary>
    internal static class InterviewGame
    {
        private const float FadeSeconds = 0.2f;   // seconds each way, as the game's own door fades (BuildingManager.EnterBuildingCoroutine)
        private const int StepTries = 10;         // steps out from the door tried for a free one, as the game's own exit tries
        private const float StepLength = 0.2f;    // metres between them

        /// <summary>The employer's door the player is nearest, by street and number; null if the building or its door is not in the city.</summary>
        public static InterviewDoor Door(string street, int number)
        {
            try
            {
                CityManager city = InstanceBehavior<CityManager>.Instance;
                CityBuildingController building = city != null && !string.IsNullOrEmpty(street) ? city.FindCityBuildingController(new Address(street, number)) : null;
                PlayerController player = PlayerHelper.PlayerController;
                if (building == null || building.entranceDoors == null || player == null)
                {
                    return null;
                }
                Vector3 at = player.transform.position;
                Transform door = null;
                float near = float.MaxValue;
                BuildingEntranceDoor[] doors = building.entranceDoors;
                for (int i = 0; i < doors.Length; i++)
                {
                    Transform t = doors[i] != null ? doors[i].doorTransform : null;
                    float d = t != null ? (t.position - at).sqrMagnitude : float.MaxValue;
                    if (d < near)
                    {
                        near = d;
                        door = t;
                    }
                }
                if (door == null)
                {
                    return null;
                }
                Vector3 outward = door.forward;
                outward.y = 0f;
                return new InterviewDoor
                {
                    Building = building, Step = FreeStep(door), Facing = door.rotation,
                    Outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward
                };
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
                return null;
            }
        }

        // The first step out from the door that no building stands in, as the game's own exit picks it (BuildingManager.IsExitPositionFree).
        private static Vector3 FreeStep(Transform door)
        {
            Vector3 from = door.position;
            for (int i = 0; i < StepTries; i++)
            {
                Vector3 step = from + door.forward * (i * StepLength);
                if (!Physics.CheckSphere(step + Vector3.up * 0.9f, 0.45f, LayerHelper.buildingsLayerMask, QueryTriggerInteraction.Ignore))
                {
                    return step;
                }
            }
            return from;
        }

        /// <summary>Metres from the player to the employer's building at an address, for the test that picks the nearest; the largest value if it is not in the city.</summary>
        public static float Distance(string street, int number)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            PlayerController player = PlayerHelper.PlayerController;
            CityBuildingController building = city != null && player != null && !string.IsNullOrEmpty(street) ? city.FindCityBuildingController(new Address(street, number)) : null;
            return building != null ? Vector3.Distance(building.transform.position, player.transform.position) : float.MaxValue;
        }

        /// <summary>True if a walk to the step can be made (the game's own route test), with no notice of the game's.</summary>
        public static bool Reachable(PlayerController player, Vector3 step)
        {
            return player != null && player.ExistsRoute(step, showErrorNotification: false);
        }

        /// <summary>The player out on the step, facing the street, as the game puts a player coming out of a building (BuildingManager.MovePlayerToPoint).</summary>
        public static void PutOut(PlayerController player, InterviewDoor door)
        {
            player.ResetNavigation();
            player.Character.navmeshAgent.Warp(door.Step);
            player.transform.rotation = door.Facing;
        }

        /// <summary>The player turned to the door they are about to go in at.</summary>
        public static void FaceIn(PlayerController player, InterviewDoor door)
        {
            player.transform.rotation = Quaternion.LookRotation(-door.Outward, Vector3.up);
        }

        /// <summary>The building's own sound for going in or coming out, as the game's door plays it (BuildingManager.PlayBuildingSound); none if the game has none for it.</summary>
        public static void DoorSound(InterviewDoor door, bool coming)
        {
            try
            {
                BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
                Building building = door.Building != null ? door.Building.building : null;
                BuildingRegistration reg = door.Building != null ? door.Building.buildingRegistration : null;
                List<BuildingEnterSound> sounds = manager != null ? (coming ? manager.exitSounds : manager.enterSounds) : null;
                SfxManager sfx = InstanceBehavior<SfxManager>.Instance;
                if (sounds == null || building == null || reg == null || sfx == null)
                {
                    return;
                }
                for (int i = 0; i < sounds.Count; i++)
                {
                    BuildingEnterSound s = sounds[i];
                    if (s != null && Array.IndexOf(s.buildingTypes, building.BuildingType) >= 0 && Array.IndexOf(s.businessTypes, reg.businessTypeName) >= 0
                        && Array.IndexOf(s.buildingSizes, building.BuildingSize) >= 0)
                    {
                        sfx.PlayAudio(s.type, door.Step, 1f, isPlayerCreatedSound: false, null, 0.25f);
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);   // the door's sound is a nicety
            }
        }

        /// <summary>The game's black screen at a door, with a line on it (a locale key) or none.</summary>
        public static IEnumerator Fade(string key)
        {
            return UiFader.Fade(FadeSeconds, key);
        }

        /// <summary>The game's black screen taken off, as its door takes it off.</summary>
        public static IEnumerator Unfade()
        {
            return UiFader.UnFade(FadeSeconds);
        }

        /// <summary>The game's black screen as it is now, to tell later whether a black screen is the one put up for an interview.</summary>
        public static UnityEngine.Object Overlay()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null ? uis.blackOverlay : null;
        }

        /// <summary>
        /// The black screen given back where no fade back of the mod's can be run (its host stopping, the city gone). In the same city
        /// the game's own fade back runs on its interface, the overlay's owner, which outlasts the careers and outruns a fade in still
        /// under way. With the city going or gone the game's flag is put down, unless a new city's own black screen is fading
        /// (<paramref name="ours"/> is the overlay the interview faded).
        /// </summary>
        public static void Clear(bool sameCity, UnityEngine.Object ours)
        {
            try
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (sameCity && uis != null && uis.blackOverlay != null && uis.isActiveAndEnabled)
                {
                    uis.StartCoroutine(Guard.Routine(UiFader.UnFade(FadeSeconds)));
                    return;
                }
                bool theirs = uis != null && uis.blackOverlay != null && !ReferenceEquals(uis.blackOverlay, ours) && uis.blackOverlay.color.a > 0.001f;
                if (!theirs)
                {
                    UiFader.isFading = false;   // the old city's black screen goes with it: its flag would stay up for good
                }
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Error(e);
                UiFader.isFading = false;
            }
        }

        /// <summary>True while the game's black screen is fading or drawn: the flag the game's fades keep.</summary>
        public static bool Black => UiFader.isFading;

        /// <summary>True while the game is loading a save or a city: no player of the old city is moved then.</summary>
        public static bool Loading => LoadScene.isLoading;

        /// <summary>The game's skip on its activity panel, pressed for the activity it shows (its own button's press); false if it could not be.</summary>
        public static bool Skip(PlayerActivity.IPlayerActivity activity)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.playerActivityUI == null || uis.timeMachine == null || uis.timeMachine.isRunning
                || !ReferenceEquals(uis.playerActivityUI.GetCurrentActivity, activity))
            {
                return false;
            }
            uis.playerActivityUI.ForceTimeMachine();
            return uis.timeMachine.isRunning;
        }

        /// <summary>The game's skip called off if it runs: the panel then finishes its activity with the minutes passed, as its own cancel does.</summary>
        public static void CancelSkip()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning)
            {
                uis.timeMachine.CancelTimeMachine();
            }
        }

        /// <summary>True while the game's skip runs.</summary>
        public static bool Skipping
        {
            get
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                return uis != null && uis.timeMachine != null && uis.timeMachine.isRunning;
            }
        }
    }
}
