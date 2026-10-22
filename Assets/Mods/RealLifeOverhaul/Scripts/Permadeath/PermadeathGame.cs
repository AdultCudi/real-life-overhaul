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
    /// The one place Permadeath touches the game. What it relies on there:
    ///   the game's own death   FuneralHelper.PlayerDead: GameManager.Update returns at once (no ticks, keys or autosave),
    ///                          energy never sends the player to hospital, and the mod's features stand down; the next
    ///                          city's PlayerController sets it false again, so nothing of it outlives the session
    ///   the save               SaveGameManager.Save runs the save event first and writes on its own thread;
    ///                          SavingGameInProgress is true until the file is on disk
    ///   the main menu          LoadScene.LoadMainMenuFromCity, run on the GameManager so it outlives the city, as the
    ///                          game's own funeral screen leaves
    ///   hunger                 GameInstance.Hunger, 0 to 100, raised only by eating or a hospital stay; nothing in the
    ///                          game itself kills
    /// Called on events, never per frame, except <see cref="CancelPressed"/> while the obituary is up.
    /// </summary>
    public static class PermadeathGame
    {
        // Outside the game's own blockers, and used by no other hold of the mod's (TextBoxChecks).
        private const NavigationBlocker Blocker = (NavigationBlocker)7313;

        private static bool _blocked;

        public static GameInstance Save => SaveGameManager.Current;

        public static bool Ready => SaveGameManager.Current != null && !LoadScene.isLoading && !GameManager.isCitySceneBeingUnloaded;

        public static int Day => SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;

        public static float NowMinutes => TimeHelper.NowInMinutes();

        public static string CharacterId => SaveGameManager.Current != null ? SaveGameManager.Current.characterId ?? string.Empty : string.Empty;

        /// <summary>The moment as the rules need it; <paramref name="dying"/> if a death is already under way.</summary>
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
            return !Ready || GameMembers.PlayerDead || GameMembers.FuneralActive || EnergyHelper.goingToHospital
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
                            return true;   // an objective of the quest that is on is not done: the tutorial runs
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

        /// <summary>The game's hunger bar, 0 to 100; 100 when no save is loaded.</summary>
        public static float Hunger => SaveGameManager.Current != null ? SaveGameManager.Current.Hunger : 100f;

        /// <summary>True when the game's energy is off in this save, so hunger never falls.</summary>
        public static bool NoEnergy => SaveGameManager.Current == null || SaveGameManager.Current.gameVariables == null || SaveGameManager.Current.gameVariables.disableEnergy;

        /// <summary>True in a moment the game can take a death in: nothing demands the player, and not in a hospital or clinic.</summary>
        public static bool SafeToDie()
        {
            return LifeEvents.IsSafeForEvent() && !InHospital();
        }

        /// <summary>True in a hospital or a clinic, by the building's special service.</summary>
        public static bool InHospital()
        {
            if (!BuildingManager.IsInsideBuilding)
            {
                return false;
            }
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            string service = manager != null && manager.building != null && manager.building.SpecialService != null ? manager.building.SpecialService.businessTypeName : null;
            return service == "ba:businesstype_hospital" || service == "ba:businesstype_clinic";
        }

        private const string StarveDrainId = "rlo-pd-starve";
        private static float _starveDrain;

        /// <summary>
        /// What going without food does after <paramref name="told"/> warnings: from the first a low mood, from the second
        /// a faster energy drain (a game energy spender); 0 takes both off. Cheap when nothing changes.
        /// </summary>
        public static void Weak(int told)
        {
            try
            {
                if (SaveGameManager.Current == null)
                {
                    _starveDrain = 0f;
                    return;
                }
                HoldStarving(told >= 1 && ModState.Loaded);
                float drain = PermadeathRules.StarveDrain(told);
                if (!Mathf.Approximately(drain, _starveDrain))
                {
                    EnergyHelper.RemoveEnergySpender(StarveDrainId);
                    if (drain > 0f)
                    {
                        EnergyHelper.AddEnergySpender(StarveDrainId, drain);
                    }
                    _starveDrain = drain;
                }
            }
            catch (Exception e)
            {
                PermadeathDirector.Logger?.Error(e);
            }
        }

        // The modifier kept on while its cause lasts and taken off when it goes, as Home's needs keep theirs.
        private static void HoldStarving(bool on)
        {
            List<HappinessModifierData> list = SaveGameManager.Current.happinessModifiers;
            HappinessModifierData found = null;
            for (int i = 0; list != null && i < list.Count; i++)
            {
                if (list[i].type == ModState.Starving)
                {
                    found = list[i];
                    break;
                }
            }
            if (on)
            {
                if (found == null)
                {
                    ModState.AddModifier(ModState.Starving, 12);
                }
                else if (found.hoursLeft <= 3)
                {
                    found.hoursLeft = 12;
                }
            }
            else if (found != null)
            {
                ModState.RemoveModifier(ModState.Starving);
            }
        }

        /// <summary>A load or the feature going: the drain is taken off whatever is remembered, as the game's spenders outlive a save.</summary>
        public static void ClearWeak()
        {
            try
            {
                _starveDrain = 0f;
                if (SaveGameManager.Current != null)
                {
                    EnergyHelper.RemoveEnergySpender(StarveDrainId);
                }
            }
            catch (Exception e)
            {
                PermadeathDirector.Logger?.Error(e);
            }
        }

        /// <summary>For the test button: the bar at its bottom now.</summary>
        public static void Starve()
        {
            if (SaveGameManager.Current != null)
            {
                EnergyHelper.SetCurrentHunger(0f);
            }
        }

        /// <summary>A game begun today: what a switch moved in the main menu applies to.</summary>
        public static bool NewGame => Day <= 1;

        /// <summary>The game's own part of a life: name, age, day, worth (as the Persona app shows it), staff, properties, most businesses at once.</summary>
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

        /// <summary>The street the player is in: the nearest building's. One walk over the city's buildings, at a death.</summary>
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

        /// <summary>
        /// The world stops for the player: the game's own death and walking blocked. <paramref name="dead"/> false is the
        /// rehearsal's hold, walking only.
        /// </summary>
        public static void Hold(bool on, bool dead)
        {
            try
            {
                if (on && dead)
                {
                    GameMembers.PlayerDead = true;
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

        /// <summary>Out of a vehicle behind the black screen, so the paper is read on foot: stopped first, as the hospital trip does.</summary>
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

        /// <summary>Back to the main menu, as the game's funeral screen goes; the save's files are left as they are.</summary>
        public static void MainMenu()
        {
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            if (gm != null)
            {
                gm.StartCoroutine(LoadScene.LoadMainMenuFromCity());
            }
        }

        /// <summary>The cancel key: the game reads no keys while the player is dead, so the obituary does, once a frame while it is up.</summary>
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

        /// <summary>True while the game's full menu, which holds the obituary, is open.</summary>
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

        /// <summary>
        /// The characters who have died with Permadeath on this machine, with their records, in the game's preferences
        /// rather than any save: an older save of one is read as ended too. Written once, at a death.
        /// </summary>
        public static class Memorial
        {
            private const string ListKey = "reallife.permadeath.dead";       // not a locale key: a dot, not an underscore
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
