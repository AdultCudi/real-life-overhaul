using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BAModAPI;
using Blueprints;
using Intro;
using TMPro;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>The step table's saves part: its steps, each a nested class (see SelfTestSteps.cs).</summary>
    internal static partial class SelfTestSteps
    {
        /// <summary>
        /// Loading a save from the main menu as the load screen does (the save handed to TransitionToSave, the loading screen
        /// started: Scenes.MainMenu.LoadGame.Load past its question boxes), by its name or the newest one.
        /// </summary>
        internal sealed class LoadSaveStep : SelfTestStep
        {
            public override string Name => "load_save";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestGame.MenuUp())
                {
                    c.Fail("not at the main menu (a save is loaded from there)");
                    yield break;
                }
                SaveGameManager.SaveGameStruct save = SelfTestSaves.Pick(c.Arg("save", "newest"), c.Arg("character"), out string why);
                if (save == null)
                {
                    c.Fail(why);
                    yield break;
                }
                SelfTestSaves.Start(save);
                float until = Time.realtimeSinceStartup + 45f;
                while (!LoadScene.isLoading)
                {
                    c.Waiting = "the game to start loading '" + save.name + "'";
                    if (Time.realtimeSinceStartup > until)
                    {
                        c.Fail("the game did not start loading '" + save.name + "' in 45 s");
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.25f);
                }
                c.Pass("loading '" + save.name + "' (character " + (save.characterData != null ? save.characterData.name : "?") + ", day " + save.day + ")");
            }
        }

        /// <summary>A new game from the main menu: the menu's own StartNewGame with a fixed character and seed, the tutorial off, the character screen finished by its own StartGame.</summary>
        internal sealed class NewGameStep : SelfTestStep
        {
            public override string Name => "new_game";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestGame.MenuUp())
                {
                    c.Fail("not at the main menu (a new game is started from there)");
                    yield break;
                }
                string seed = c.Arg("seed", "selftest");
                string name = c.Arg("name", "Selftest Sam");
                if (!SelfTestSaves.StartNew((int)c.Number("money", 4200f), (int)c.Number("age", 18f), c.Flag("tutorial", false), seed))
                {
                    c.Fail("the main menu would not start a new game");
                    yield break;
                }
                IntroCharacterCustomizer screen;
                while ((screen = SceneFinds.Any(typeof(IntroCharacterCustomizer), "the self-test's wait for the character screen") as IntroCharacterCustomizer) == null)
                {
                    c.Waiting = "the character screen to stand";
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                yield return new WaitForSecondsRealtime(1f);   // its own Start has run
                TMP_InputField field = screen.characterNamePicker != null ? screen.characterNamePicker.GetComponentInChildren<TMP_InputField>(true) : null;
                if (field == null)
                {
                    c.Fail("the character screen has no name field");
                    yield break;
                }
                field.text = name;
                screen.StartGame();
                float until = Time.realtimeSinceStartup + 60f;
                while (!LoadScene.isLoading && !SelfTestGame.CityUp())
                {
                    c.Waiting = "the game to leave the character screen and load the city";
                    if (Time.realtimeSinceStartup > until)
                    {
                        c.Fail("the city did not start loading after the character was made");
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                c.Pass("new game '" + name + "', seed " + seed + ", tutorial " + (c.Flag("tutorial", false) ? "on" : "off") + ": the city is loading");
            }
        }

        /// <summary>Saves the running game under a name through the game's own save, and waits until the file is on disk.</summary>
        internal sealed class SaveGameStep : SelfTestStep
        {
            public override string Name => "save_game";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestGame.CityUp())
                {
                    c.Fail("no city stands: there is nothing to save");
                    yield break;
                }
                string name = c.Arg("name");
                if (!SelfTestSaves.Save(name))
                {
                    c.Fail("the game refused to save (it holds the save while it cannot: a screen, a loading, a minigame)");
                    yield break;
                }
                while (SaveGameManager.SavingGameInProgress)
                {
                    c.Waiting = "the game to finish writing the save";
                    yield return new WaitForSecondsRealtime(0.25f);
                }
                yield return SaveGameManager.JoinSaveGameThreadsCoroutine();
                string file = SelfTestSaves.FileOf(name);
                if (file == null)
                {
                    c.Fail("the save '" + name + "' is not on disk after the game said it saved");
                    yield break;
                }
                c.Pass("saved '" + name + "' (" + new FileInfo(file).Length + " bytes) at " + SelfTestGame.CityWords());
            }
        }

        /// <summary>Leaves the city for the main menu as the game's pause menu does, and waits until the menu stands.</summary>
        internal sealed class ReturnMenuStep : SelfTestStep
        {
            public override string Name => "return_menu";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (SelfTestGame.MenuUp())
                {
                    c.Pass("already at the main menu");
                    yield break;
                }
                if (!SelfTestGame.CityUp())
                {
                    c.Fail("no city stands to leave (and the main menu is not up)");
                    yield break;
                }
                yield return SelfTestGame.Close();
                SelfTestSaves.LeaveCity();
                while (!SelfTestGame.MenuUp())
                {
                    c.Waiting = "the main menu to stand after leaving the city";
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                yield return new WaitForSecondsRealtime(1f);
                c.Pass("the main menu stands");
            }
        }
    }
}
