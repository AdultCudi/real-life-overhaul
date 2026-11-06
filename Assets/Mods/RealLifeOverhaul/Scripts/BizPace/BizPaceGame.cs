using System;
using System.Collections.Generic;
using System.Reflection;
using Streets;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.BizPace
{
    /// <summary>
    /// The one place the BizPace watch touches the game's BizMan: the two kinds of mark that tell it a step has begun (one on the business
    /// view, one on each tab's page; their OnEnable is the game's own move: BizMan's business view switched on, a tab's page switched on
    /// by SetTab), and the few reads the test needs. The marks are put on only while the watch is armed (Developer diagnostics on, or the
    /// test walking) and taken off when it stops, so BizMan carries nothing of the watch as shipped. COST: nothing per frame; a look at the
    /// view every two seconds while armed and not installed or after the game builds its pages again.
    /// </summary>
    internal static class BizPaceGame
    {
        private static BizManBusiness _business;
        private static Address _last;                 // the address the view showed at the last mark: another one is a switch
        private static float _next;                   // unscaled seconds: the next look for the view
        private static bool _installing;              // marks put on a view that is on already are not the view switching on
        private static readonly List<Component> Marks = new List<Component>(24);

        /// <summary>The business the view shows now, in words; empty when there is none.</summary>
        internal static string Where()
        {
            Address address = _business != null ? _business.address : null;
            return address != null ? address.ToFormattedString() : string.Empty;
        }

        /// <summary>Puts the marks on the view and its tabs' pages if they are not there; <paramref name="now"/> skips the two seconds' wait.</summary>
        internal static void Keep(bool now)
        {
            if (!now && Time.unscaledTime < _next)
            {
                return;
            }
            _next = Time.unscaledTime + 2f;
            try
            {
                Install();
            }
            catch (Exception e)
            {
                _next = Time.unscaledTime + 30f;
                BizPaceWatch.Logger?.Error(e);
            }
            BizPaceWatch.Where = Where;
        }

        private static void Install()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            BizManBusiness business = uis != null && uis.fullMenu != null && uis.fullMenu.bizMan != null ? uis.fullMenu.bizMan.business : null;
            if (business == null)
            {
                return;   // the phone's apps are not in yet
            }
            _installing = true;
            try
            {
                _business = business;
                if (business.GetComponent<BizPaceViewMark>() == null)
                {
                    Marks.Add(business.gameObject.AddComponent<BizPaceViewMark>());
                }
                Transform pages = GameContract.BizPaceContainers.Field?.GetValue(business) as Transform;
                for (int i = 0; pages != null && i < pages.childCount; i++)
                {
                    GameObject page = pages.GetChild(i).gameObject;
                    if (page.GetComponent<BizPaceTabMark>() == null)
                    {
                        Marks.Add(page.AddComponent<BizPaceTabMark>());
                    }
                }
            }
            finally
            {
                _installing = false;
            }
        }

        /// <summary>The marks taken off: BizMan is as the game made it.</summary>
        internal static void Remove()
        {
            for (int i = 0; i < Marks.Count; i++)
            {
                if (Marks[i] != null)
                {
                    UnityEngine.Object.Destroy(Marks[i]);
                }
            }
            Marks.Clear();
            _business = null;
            _last = null;
            BizPaceWatch.Where = null;
        }

        /// <summary>The business view was switched on: an open.</summary>
        internal static void ViewShown()
        {
            if (_installing)
            {
                return;
            }
            _last = _business != null ? _business.address : null;
            BizPaceWatch.Mark(BizStep.Open);
        }

        /// <summary>A tab's page was switched on: a switch if the view's business moved since the last mark, else another tab.</summary>
        internal static void TabShown()
        {
            if (_installing)
            {
                return;
            }
            Address now = _business != null ? _business.address : null;
            bool moved = !ReferenceEquals(now, _last);
            _last = now;
            BizPaceWatch.Mark(BizPaceRules.OfTabMark(moved));
        }

        /// <summary>
        /// Gives the code warmer (Core/DayWarm) the handlers of BizMan's steps. Mono compiles a method when it is first called, so without this the first business a session
        /// opens, the first tab it clicks and the first time it leaves a view pay the compiler for the Interior tab's page, the presentation's refusal, the start screen's
        /// cards and the listeners on the registration change event, and the more each release adds to them the longer that first moment takes. A slice a frame, after a load.
        /// </summary>
        internal static void Warm(CodeWarm warm)
        {
            try
            {
                warm.Root(GlobalEvents.onBuildingRegistrationChange);
                Root(warm, typeof(Interiors.InteriorTab), "Write", "Rewrite", "Fix", "Written");
                Root(warm, typeof(Interiors.InteriorTabKeeper), "OnLateUpdate");
                Root(warm, typeof(Interiors.InteriorPageWatch), "OnEnable");
                Root(warm, typeof(ClaimedPresentation), "Look", "Hold", "OnLateUpdate");
                Root(warm, typeof(Tech.StartCards), "Write");
                Root(warm, typeof(Tech.StartCardsWrite), "OnLateUpdate");
            }
            catch (Exception e)
            {
                BizPaceWatch.Logger?.Error(e);   // the day's code is compiled ahead all the same; these handlers are compiled when first called, as before
            }
        }

        private static void Root(CodeWarm warm, Type type, params string[] names)
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    MethodBase method = type.GetMethod(names[i], all);
                    if (method != null)
                    {
                        warm.Root(method);
                    }
                }
                catch (AmbiguousMatchException)
                {
                    // a name with overloads: its callers' reach covers it
                }
            }
        }

        // ---- what the test needs ----

        /// <summary>The game's BizMan app, or null while the phone's apps are not there.</summary>
        internal static global::UI.Smartphone.Apps.BizMan.BizMan App()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null && uis.fullMenu != null ? uis.fullMenu.bizMan : null;
        }

        /// <summary>True while the business view shows changes to its settings the player has not saved (a click on a tab then asks a question).</summary>
        internal static bool Unsaved()
        {
            BizManBusiness business = App() != null ? App().business : null;
            return business != null && business.bizManSettings != null && business.bizManSettings.HasUnsavedChanges;
        }

        /// <summary>True while the business view is on.</summary>
        internal static bool ViewOn()
        {
            BizManBusiness business = App() != null ? App().business : null;
            return business != null && business.isActiveAndEnabled;
        }

        /// <summary>The player's own businesses in BizMan's order, at most <paramref name="most"/>: what the test walks through.</summary>
        internal static void Businesses(List<Address> into, int most)
        {
            into.Clear();
            GameInstance save = SaveGameManager.Current;
            List<BuildingRegistration> all = save != null ? save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count && into.Count < most; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && reg.RentedByPlayer && reg.HasEstablishedBusiness && reg.HasValidAddress && GoingPublic.Mine(reg) && GameRegistrations.Found(reg))
                {
                    into.Add(reg.Address);
                }
            }
        }

        /// <summary>The buttons of the tabs the view shows now, in their order.</summary>
        internal static void Tabs(List<UnityEngine.UI.Button> into)
        {
            into.Clear();
            BizManBusiness business = App() != null ? App().business : null;
            Transform menu = business != null ? GameContract.BizPaceMenu.Field?.GetValue(business) as Transform : null;
            for (int i = 0; menu != null && i < menu.childCount; i++)
            {
                Transform tab = menu.GetChild(i);
                UnityEngine.UI.Button button = tab.gameObject.activeSelf ? tab.GetComponent<UnityEngine.UI.Button>() : null;
                if (button != null && button.interactable)
                {
                    into.Add(button);
                }
            }
        }

        /// <summary>The game's own BizMan opened on a business (its list first when <paramref name="viaList"/>, so the view is switched on afresh: an open).</summary>
        internal static void OpenOn(Address address, bool viaList)
        {
            global::UI.Smartphone.Apps.BizMan.BizMan app = App();
            if (app == null)
            {
                return;
            }
            if (viaList && ViewOn())
            {
                app.Open();   // the list, with no address: the view is switched off
            }
            app.Open(address);
        }

        /// <summary>The menu closed, as the player would close it.</summary>
        internal static void CloseMenu()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.fullMenu != null && global::UI.Smartphone.FullMenu.IsOpen)
            {
                uis.fullMenu.Toggle(false);
            }
        }
    }

    /// <summary>On BizMan's business view while the watch is armed: its being switched on is an open. No cost a frame.</summary>
    public sealed class BizPaceViewMark : MonoBehaviour
    {
        private void OnEnable()
        {
            try
            {
                BizPaceGame.ViewShown();
            }
            catch (Exception e)
            {
                BizPaceWatch.Logger?.Error(e);
            }
        }
    }

    /// <summary>On each tab's page of the view while the watch is armed: its being switched on is a tab change, or a switch of business. No cost a frame.</summary>
    public sealed class BizPaceTabMark : MonoBehaviour
    {
        private void OnEnable()
        {
            try
            {
                BizPaceGame.TabShown();
            }
            catch (Exception e)
            {
                BizPaceWatch.Logger?.Error(e);
            }
        }
    }
}
