using System;
using System.Collections;
using System.Collections.Generic;
using BigAmbitions.InputSystem;
using Helpers;
using Streets;
using UI;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Permadeath
{
    /// <summary>
    /// The one place Permadeath touches the game. Read from the decompiled game:
    ///
    ///   the game's own death    FuneralHelper.PlayerDead (Helpers/FuneralHelper.cs): while it is true GameManager.Update
    ///                           returns at once (GameManager.cs 412: no ticks, no keys, no autosave, CheckAutoSave 539),
    ///                           the mouse does nothing in the world (MouseController.Run), energy never sends the player to
    ///                           hospital (EnergyHelper.GoToHospitalIsEnabled), and every feature of this mod that asks
    ///                           LifeEvents.IsSafeForEvent stands down. PlayerController's start sets it false again on the
    ///                           next city (PlayerController.cs 170), so nothing of it outlives the session.
    ///   the fade                UiFader.Fade / UnFade (UI/UiFader.cs): the game's own black overlay, with a line on it
    ///   the save                SaveGameManager.Save(Default, the save's own name) (SaveGameManager.cs 154): the save event
    ///                           runs first (the mod's state goes in), the file is written on its own thread, and
    ///                           SavingGameInProgress is true until it is on disk
    ///   the main menu           LoadScene.LoadMainMenuFromCity (UI.Load/LoadScene.cs 27), as the game's own funeral screen
    ///                           leaves (UI/FuneralUI.BackToMainMenu), run on the GameManager so that it outlives the city
    ///   the tutorial            TutorialHelper.currentQuest with an objective not yet done (Helpers/TutorialHelper.cs)
    ///   a mission               GameInstance.currentPlayerMission ongoing (Player.PlayerMissions/PlayerMission.cs), or a
    ///                           side quest's entry active (GameInstance.activeSideQuestEntries)
    ///   the age                 TimeHelper.GetYearsByDays(CharacterData.ageInDays)
    ///   what they were worth    PlayerHelper.GetPersonalWealth().CurrentWealth, the figure the Persona app shows
    ///   the staff               GameInstance.EmployeeInstances: the player's own employees (candidates are a list of their own)
    ///
    /// Everything here is called on an event (a death, a load, a button), never per frame, except <see cref="CancelPressed"/>,
    /// which is asked once a frame only while the obituary is up.
    /// </summary>
    public static class PermadeathGame
    {
        // The game blocks walking with named blockers kept in a set: one of the mod's own, outside the game's list.
        private const NavigationBlocker Blocker = (NavigationBlocker)7311;

        private static bool _blocked;

        public static GameInstance Save => SaveGameManager.Current;

        public static bool Ready => SaveGameManager.Current != null && !LoadScene.isLoading && !GameManager.isCitySceneBeingUnloaded;

        public static int Day => SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;

        public static float NowMinutes => TimeHelper.NowInMinutes();

        public static string CharacterId => SaveGameManager.Current != null ? SaveGameManager.Current.characterId ?? string.Empty : string.Empty;

        /// <summary>The moment as the rules need it. <paramref name="dying"/>: a death is already under way.</summary>
        public static Moment Now(bool dying)
        {
            return new Moment
            {
                Armed = PermadeathState.Loaded && PermadeathState.Armed,
                Dead = PermadeathState.Loaded && PermadeathState.Dead,
                InSession = GoingPublic.InSession,
                MissionOrTutorial = MissionOrTutorial(),
                Busy = dying || Busy()
            };
        }

        // The game is elsewhere: no death is started in the middle of it.
        private static bool Busy()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return !Ready || FuneralHelper.PlayerDead || FuneralHelper.IsFuneralActive || EnergyHelper.goingToHospital
                   || uis == null || (uis.timeMachine != null && uis.timeMachine.isRunning);
        }

        /// <summary>True while the game's own story has the player: its tutorial, one of its missions, a side quest.</summary>
        public static bool MissionOrTutorial()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return true;   // not known: nothing happens
            }
            try
            {
                if (save.currentPlayerMission != null && save.currentPlayerMission.IsOngoing())
                {
                    return true;
                }
                if (save.activeSideQuestEntries != null && save.activeSideQuestEntries.Count > 0)
                {
                    return true;
                }
                Tutorial.Quest quest = TutorialHelper.currentQuest;
                if (save.gameVariables != null && save.gameVariables.tutorialEnabled && quest != null && quest.entries != null)
                {
                    for (int i = 0; i < quest.entries.Length; i++)
                    {
                        if (quest.entries[i] != null && !save.CompletedQuestEntries.Contains(quest.entries[i].Id))
                        {
                            return true;   // an objective of the quest that is on is not done yet: the tutorial runs
                        }
                    }
                }
            }
            catch (Exception)
            {
                return true;
            }
            return false;
        }

        /// <summary>A game begun today: what a switch moved in the main menu applies to.</summary>
        public static bool NewGame => Day <= 1;

        // ---------------------------------------------------------------- the story, as far as the game knows it

        /// <summary>The game's own part of a life: the name, the age, the day, the estate, the staff, the properties, the businesses at most at once.</summary>
        public static void Fill(LifeStory s)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            CharacterData data = PlayerHelper.CharacterData ?? (save.charactersData != null && save.charactersData.Count > 0 ? save.charactersData[0] : null);
            s.Name = LifeStory.Clean(data != null ? data.name : string.Empty);
            s.Age = data != null ? TimeHelper.GetYearsByDays(data.ageInDays) : 0;
            s.Day = save.Day;
            s.DaysPerYear = save.gameVariables != null ? Mathf.Max(1, save.gameVariables.daysPerYear) : 60;
            try
            {
                s.Worth = (long)Math.Round(PlayerHelper.GetPersonalWealth().CurrentWealth);
            }
            catch (Exception)
            {
                s.Worth = (long)Math.Round(save.Money);
            }
            int staff = 0;
            for (int i = 0; save.EmployeeInstances != null && i < save.EmployeeInstances.Count; i++)
            {
                staff += save.EmployeeInstances[i] != null ? 1 : 0;
            }
            s.Employees = staff;
            s.Properties = save.realEstate != null ? save.realEstate.Count : 0;
            int most = 0;
            for (int i = 0; save.playerNumberOfBusinessesHistory != null && i < save.playerNumberOfBusinessesHistory.Count; i++)
            {
                Tuple<int, int> week = save.playerNumberOfBusinessesHistory[i];
                most = week != null && week.Item2 > most ? week.Item2 : most;
            }
            s.Founded = Mathf.Max(s.Founded, most);
        }

        /// <summary>The street the player is in, as the game writes it: the nearest building's. A walk over the city's buildings, once, at a death.</summary>
        public static string Street()
        {
            try
            {
                Vector3 where = PlayerHelper.GetPosition();
                CityManager city = InstanceBehavior<CityManager>.Instance;
                CityBuildingController[] buildings = city != null ? city.cityBuildingControllers : null;
                CityBuildingController best = null;
                float bestSqr = float.MaxValue;
                for (int i = 0; buildings != null && i < buildings.Length; i++)
                {
                    CityBuildingController building = buildings[i];
                    if (building == null || building.building == null)
                    {
                        continue;
                    }
                    Vector3 offset = building.transform.position - where;
                    float sqr = offset.x * offset.x + offset.z * offset.z;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = building;
                    }
                }
                string street = best != null && best.building.Address != null ? best.building.Address.GetStreetNameLocalized() : null;
                return LifeStory.Clean(street);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        // ---------------------------------------------------------------- the moment of death

        /// <summary>
        /// The world stops for the player: the game's own "the player is dead" (its ticks, keys, autosave and the hospital
        /// stop with it) and walking blocked. <paramref name="dead"/> false is the rehearsal's hold: walking only.
        /// </summary>
        public static void Hold(bool on, bool dead)
        {
            try
            {
                if (on && dead)
                {
                    FuneralHelper.PlayerDead = true;
                }
                PlayerController player = PlayerHelper.PlayerController;
                if (player == null)
                {
                    _blocked = false;
                    return;
                }
                if (on && !_blocked)
                {
                    player.ResetNavigation();
                    _blocked = true;
                    player.SetNavigationBlocker(Blocker);
                }
                else if (!on && _blocked)
                {
                    _blocked = false;
                    player.UnsetNavigationBlocker(Blocker);
                }
            }
            catch (Exception e)
            {
                PermadeathDirector.Logger?.Error(e);
            }
        }

        /// <summary>A question of the game's left open would hold the keys: it is closed as if dismissed.</summary>
        public static void CloseQuestion()
        {
            try
            {
                if (HudConfirm.isOpen)
                {
                    HudConfirm.onClose?.Invoke();
                }
            }
            catch (Exception e)
            {
                PermadeathDirector.Logger?.Error(e);
            }
        }

        public static IEnumerator Fade(float seconds, string lineKey)
        {
            return UiFader.Fade(seconds, lineKey);
        }

        public static IEnumerator UnFade(float seconds)
        {
            return UiFader.UnFade(seconds);
        }

        /// <summary>The game's own save, under the save's own name. False if the game would not save now.</summary>
        public static bool SaveNow()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return false;
            }
            string name = !string.IsNullOrEmpty(save.SaveGameName) ? save.SaveGameName
                : PlayerHelper.CharacterData != null && !string.IsNullOrEmpty(PlayerHelper.CharacterData.name) ? PlayerHelper.CharacterData.name : "Permadeath";
            return SaveGameManager.Save(SaveGameManager.SaveType.Default, name);
        }

        public static bool Saving => SaveGameManager.SavingGameInProgress;

        /// <summary>Out of a vehicle, behind the black screen, so that the paper is read on foot: the car stopped first, as the hospital trip does it.</summary>
        public static IEnumerator OutOfVehicle()
        {
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null || gm.selectedVehicle == null)
            {
                yield break;
            }
            float waited = 0f;
            while (gm.selectedVehicle != null && gm.selectedVehicle.CurrentSpeed > 1f && waited < 6f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (gm.selectedVehicle != null)
            {
                try
                {
                    gm.selectedVehicle.ExitVehicle();
                }
                catch (Exception e)
                {
                    PermadeathDirector.Logger?.Error(e);
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        /// <summary>Back to the main menu, as the game's own funeral screen goes: the save's files are left as they are.</summary>
        public static void MainMenu()
        {
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            if (gm != null)
            {
                gm.StartCoroutine(LoadScene.LoadMainMenuFromCity());
            }
        }

        /// <summary>The cancel key (Esc by default): the game no longer reads its keys while the player is dead, so the obituary does. Once a frame while it is up.</summary>
        public static bool CancelPressed()
        {
            try
            {
                return PlayerAction.Cancel.Pressed();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>True while the game's full menu is open (the obituary is read in it).</summary>
        public static bool MenuOpen => UI.Smartphone.FullMenu.IsOpen;

        /// <summary>The full menu put down, as its own close button does.</summary>
        public static void CloseMenu()
        {
            try
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (uis != null && uis.fullMenu != null && UI.Smartphone.FullMenu.IsOpen)
                {
                    uis.fullMenu.Toggle(false);
                }
            }
            catch (Exception e)
            {
                PermadeathDirector.Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- the memorial

        /// <summary>
        /// The characters who have died with Permadeath on this machine, kept in the game's preferences (not in any save),
        /// with the record of each: an older save of a character who has since died, from a time it was on, is read as
        /// ended too (Permadeath/PermadeathRules.Ended), and prints the same page. Written once, at a death.
        /// </summary>
        public static class Memorial
        {
            private const string ListKey = "reallife.permadeath.dead";       // (not a locale key: a dot, not an underscore)
            private const string RecordPrefix = "reallife.permadeath.life.";

            public static void Keep(string characterId, LifeStory story)
            {
                if (string.IsNullOrEmpty(characterId) || story == null)
                {
                    return;
                }
                try
                {
                    string list = UnityEngine.PlayerPrefs.GetString(ListKey, string.Empty);
                    if (!Has(list, characterId))
                    {
                        list = list.Length > 0 ? list + "," + characterId.Replace(",", " ") : characterId.Replace(",", " ");
                        UnityEngine.PlayerPrefs.SetString(ListKey, list);
                    }
                    UnityEngine.PlayerPrefs.SetString(RecordPrefix + characterId, story.Write());
                    UnityEngine.PlayerPrefs.Save();
                }
                catch (Exception e)
                {
                    PermadeathDirector.Logger?.Error(e);
                }
            }

            /// <summary>True (and the record read into <paramref name="into"/>) if this character has died with Permadeath on.</summary>
            public static bool Find(string characterId, LifeStory into)
            {
                if (string.IsNullOrEmpty(characterId))
                {
                    return false;
                }
                try
                {
                    if (!Has(UnityEngine.PlayerPrefs.GetString(ListKey, string.Empty), characterId))
                    {
                        return false;
                    }
                    into.Read(UnityEngine.PlayerPrefs.GetString(RecordPrefix + characterId, string.Empty));
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            private static bool Has(string list, string id)
            {
                if (string.IsNullOrEmpty(list))
                {
                    return false;
                }
                string[] ids = list.Split(',');
                for (int i = 0; i < ids.Length; i++)
                {
                    if (ids[i] == id)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
