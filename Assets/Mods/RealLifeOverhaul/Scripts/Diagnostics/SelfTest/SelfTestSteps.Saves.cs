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
using UnityEngine.UI;

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
                string before = SelfTestDiag.Scenes();
                while (SelfTestGame.MenuUp() || SelfTestDiag.Scenes() == before)
                {
                    c.Waiting = "the scenes to change after the loading screen began for '" + save.name + "' (a loading flag alone is not taken as a load)";
                    if (Time.realtimeSinceStartup > until + 45f)
                    {
                        c.Fail("loading '" + save.name + "' began but the scenes never changed in 45 s: " + SelfTestDiag.Scenes());
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                string who = save.characterData != null ? save.characterData.name : "?";
                c.Pass("loading '" + save.name + "' (character " + who + ", day " + save.day + "); the menu was left: " + SelfTestDiag.Scenes());
            }
        }

        /// <summary>
        /// The "new_game" step: the menu's own StartNewGame with a fixed character and seed, the tutorial off; then the character screen
        /// as a player meets it: waited until it stands and its Start button is interactive, the name typed into its own field, the
        /// button's own click handler pressed, any question the game asks answered, until the loading screen has begun AND the loaded
        /// scenes have changed. It passes on that evidence only, and fails within 90 s of the press naming where it stood.
        /// </summary>
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
                    c.Fail("the main menu would not start a new game (StartNewGame left no game)");
                    yield break;
                }
                float began = Time.realtimeSinceStartup;
                IntroCharacterCustomizer screen;
                while ((screen = SceneFinds.Any(typeof(IntroCharacterCustomizer), "the self-test's wait for the character screen") as IntroCharacterCustomizer) == null)
                {
                    c.Waiting = "the character screen to stand";
                    if (Time.realtimeSinceStartup - began > 40f)
                    {
                        c.Fail("the character screen never stood within 40 s of StartNewGame: " + SelfTestDiag.Scenes());
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                float stood = Time.realtimeSinceStartup;
                Button start = null;
                while (true)
                {
                    start = SelfTestSaves.StartButton(screen);
                    bool ready = start != null && start.interactable && start.gameObject.activeInHierarchy && Time.realtimeSinceStartup - stood > 2f && !LoadScene.isLoading;
                    if (ready)
                    {
                        break;
                    }
                    c.Waiting = "the character screen's Start button to be interactive (" + (start == null ? "not found" : "found '" + start.name + "', interactable " + start.interactable) + ")";
                    if (Time.realtimeSinceStartup - stood > 30f)
                    {
                        c.Fail("the character screen stood but its Start button was never interactive in 30 s: " + SelfTestSaves.ButtonsWords(screen));
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                TMP_InputField field = screen.characterNamePicker != null ? screen.characterNamePicker.GetComponentInChildren<TMP_InputField>(true) : null;
                if (field == null)
                {
                    c.Fail("the character screen has no name field (NamePicker's TMP_InputField)");
                    yield break;
                }
                field.text = name;
                yield return null;
                if (!screen.characterNamePicker.HasANameSet)
                {
                    c.Fail("the name field did not keep the name '" + name + "'");
                    yield break;
                }
                string before = SelfTestDiag.Scenes();
                float pressed = Time.realtimeSinceStartup;
                start.onClick.Invoke();   // the button's own handler, as a click runs it
                string tried = "pressed Start ('" + start.name + "')";
                bool again = false, loadingSeen = false;
                while (true)
                {
                    float since = Time.realtimeSinceStartup - pressed;
                    if (SelfTestSaves.AnswerQuestion())
                    {
                        tried += "; answered a question box";
                    }
                    loadingSeen |= LoadScene.isLoading;
                    if (loadingSeen && SelfTestDiag.Scenes() != before)
                    {
                        c.Pass("new game '" + name + "', seed " + seed + ": the loading screen began and the scenes changed from [" + before + "] to [" + SelfTestDiag.Scenes() + "] (" + tried + ")");
                        yield break;
                    }
                    if (!again && !loadingSeen && since > 12f && screen != null && screen.gameObject.activeInHierarchy)
                    {
                        again = true;
                        screen.StartGame();   // the click did nothing in 12 s: the screen's own method called directly
                        tried += "; then called StartGame() itself";
                    }
                    c.Waiting = loadingSeen ? "the scenes to change after the loading screen began" : "the game to leave the character screen";
                    if (since > 90f)
                    {
                        string stuck = loadingSeen ? "the loading flag was set but the scenes never changed: the loading screen stalled"
                            : "stuck at the character creation screen: the game never began loading";
                        c.Fail(stuck + " (" + tried + "; name '" + name + "' kept " + screen.characterNamePicker.HasANameSet + "; " + SelfTestSaves.ButtonsWords(screen) + ")");
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                }
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
