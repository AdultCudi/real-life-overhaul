using System;
using BAModAPI;
using PlayerActivity;
using RealLifeOverhaul.GameUi;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Meals
{
    /// <summary>
    /// "Eat ..." in the game's own activity panel while the player rests on a seat (its rest on a chair or a bench): a copy
    /// of the panel's own button template, made once per city and kept beside the game's buttons, untagged so that the
    /// panel's rebuild of its buttons leaves it be. It shows only while something carried can be eaten there
    /// (<see cref="MealsRules.Offered"/>). Nothing runs per frame: it is decided when the game switches the panel on, when
    /// the player sits down or gets up, and after a press or a meal.
    /// </summary>
    public sealed class TakeoutButton : MonoBehaviour
    {
        public static IModLogger Logger;

        private static TakeoutButton _instance;
        private static bool _failed;

        private GameObject _button;
        private Component _label;
        private string _words;

        /// <summary>True once the button is in the game's panel for this city.</summary>
        public static bool Installed => _instance != null && _instance._button != null;

        /// <summary>Puts the button in the game's activity panel, once per city. False if the panel is not built as expected (said once in Player.log).</summary>
        public static bool Install()
        {
            if (_instance != null)
            {
                return true;
            }
            if (_failed)
            {
                return false;
            }
            try
            {
                PlayerActivityUI panel = TakeoutGame.Panel();
                if (panel == null)
                {
                    return false;   // the interface is not there yet: asked again a second later
                }
                Button template = TakeoutGame.Template(panel);
                if (template == null || template.transform.parent == null)
                {
                    _failed = true;
                    UiClone.Fallback("the game's activity panel has no button to copy, so Eat is not offered at a seat");
                    return false;
                }
                using var piece = SessionWatch.Piece("game UI: the rest panel's Eat button being copied from the game's own");
                Transform list = template.transform.parent;
                Transform holder = UiClone.Dormant(list, "RealLifeOverhaul Takeout Holder");
                GameObject copy = UiClone.Asleep(template.gameObject, holder, "RealLifeOverhaul Takeout Eat");
                UiClone.Rewire(copy.transform, Clicked);
                Image image = copy.GetComponent<Image>();
                Sprite blue = TakeoutGame.Blue();
                if (image != null && blue != null)
                {
                    image.sprite = blue;   // as the game colours its own Start buttons in this panel
                }
                Component label = copy.GetComponentInChildren<TMPro.TMP_Text>(true);
                copy.SetActive(false);
                copy.transform.SetParent(list, false);
                copy.transform.SetAsFirstSibling();
                DestroyImmediate(holder.gameObject);

                _instance = panel.gameObject.AddComponent<TakeoutButton>();
                _instance._button = copy;
                _instance._label = label;
                _instance.Decide();
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Meals check (takeout): 'Eat' is a button in the game's activity panel, copied from its " + template.name + ": PASS.");
                }
                return true;
            }
            catch (Exception e)
            {
                _failed = true;
                Logger?.Error(e);
                UiClone.Fallback("the Eat button could not be put in the game's activity panel, so Eat is not offered at a seat");
                return false;
            }
        }

        public static void Remove()
        {
            try
            {
                if (_instance != null)
                {
                    if (_instance._button != null)
                    {
                        Destroy(_instance._button);
                    }
                    Destroy(_instance);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _instance = null;
            _failed = false;
        }

        /// <summary>Decides the button again now (a sit, a press, a meal over); nothing when it is not made.</summary>
        public static void Refresh()
        {
            if (_instance != null)
            {
                _instance.Decide();
            }
        }

        // The game has just switched the panel on, for whatever activity: the button is shown only for a rest with food.
        private void OnEnable()
        {
            Decide();
        }

        private void Decide()
        {
            try
            {
                if (_button == null)
                {
                    return;
                }
                MealsDirector director = MealsDirector.Instance;
                bool show = false;
                string what = director != null ? director.EatWhat(out show) : null;
                if (show && what != null)
                {
                    string words = JobText.T(MealsText.ButtonEat, "food", what);
                    if (words != _words)
                    {
                        _words = words;
                        UiClone.Words(_label, words);
                    }
                }
                if (_button.activeSelf != show)
                {
                    _button.SetActive(show);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private static void Clicked()
        {
            MealsDirector.Instance?.EatHere();
        }
    }
}
