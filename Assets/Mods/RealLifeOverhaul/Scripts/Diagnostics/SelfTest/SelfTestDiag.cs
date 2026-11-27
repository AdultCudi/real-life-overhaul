using System.Collections.Generic;
using System.Text;
using UI.Load;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>
    /// What a failed step writes into the results so the failure can be read from them alone: the loaded scenes, the loading flag,
    /// the game's own question box, the canvases showing and the kit's screens showing. A search of the scene, done once a failure.
    /// </summary>
    internal static class SelfTestDiag
    {
        /// <summary>The names of the loaded scenes with their load state, in one line; also what a step compares to see the scenes change.</summary>
        public static string Scenes()
        {
            var sb = new StringBuilder(96);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                sb.Append(i > 0 ? ", " : string.Empty).Append(s.name).Append(s.isLoaded ? string.Empty : " (loading)");
            }
            return sb.ToString();
        }

        public static string Dump()
        {
            var sb = new StringBuilder(512);
            sb.Append("scenes: ").Append(Scenes());
            sb.Append(" | LoadScene.isLoading ").Append(LoadScene.isLoading).Append(", timeScale ").Append(Time.timeScale.ToString("F2"));
            sb.Append(" | main menu up ").Append(SelfTestGame.MenuUp()).Append(", city up ").Append(SelfTestGame.CityUp());
            sb.Append(" | question box open ").Append(HudConfirm.isOpen);
            sb.Append(" | ").Append(SelfTestGame.ShowingWords());
            var roots = new List<string>(16);
            foreach (Canvas canvas in SceneFinds.All<Canvas>("the self-test's list of the canvases showing after a failure"))
            {
                if (canvas != null && canvas.isRootCanvas && canvas.enabled && canvas.gameObject.activeInHierarchy)
                {
                    roots.Add(canvas.name + "#" + canvas.sortingOrder);
                }
            }
            sb.Append(" | showing canvases: ").Append(string.Join(", ", roots));
            return sb.ToString();
        }
    }
}
